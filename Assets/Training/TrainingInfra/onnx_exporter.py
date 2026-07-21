"""
ONNX exporter for Neural Animation policies.

Exports trained PyTorch models to ONNX format compatible with Unity Sentis/InferenceEngine.

Key compatibility requirements (from ONNXPolicy.cs):
- Input tensor name: "observation"
- Output tensor name: "action"
- Input shape: [1, 1, 1, obs_dim] (NHWC format — Unity Sentis TensorShape)
- Output shape: [1, act_dim]
- FP32 precision
- Dynamic batch dimension (optional)
"""

import os
import logging
from typing import Optional, Tuple
from pathlib import Path

import numpy as np
import torch
import torch.onnx

from config import Config, ONNXConfig
from ppo_trainer import ActorCritic

logger = logging.getLogger(__name__)


# ──────────────────────────────────────────────────────────────────────────────
#  ONNX Export
# ──────────────────────────────────────────────────────────────────────────────

def export_to_onnx(
    model: ActorCritic,
    cfg: Config,
    output_path: str,
    state_dict: Optional[dict] = None,
    verbose: bool = False,
) -> str:
    """
    Export a trained ActorCritic model to ONNX format compatible with Unity Sentis.

    The exported model uses:
    - Input name: "observation"  (matches ONNXPolicy.cs)
    - Output name: "action"       (matches ONNXPolicy.cs)
    - Input shape: [1, 1, 1, obs_dim]  (NHWC-style, Unity Sentis TensorShape)
    - Output shape: [1, act_dim]
    - FP32 precision

    Args:
        model: The ActorCritic model to export.
        cfg: Configuration object (used for obs_dim, act_dim, ONNX settings).
        output_path: Path where the .onnx file will be saved.
        state_dict: Optional state dict to load before exporting.
        verbose: Print detailed export information.

    Returns:
        Absolute path to the exported .onnx file.

    Raises:
        RuntimeError: If ONNX validation fails.
    """
    onnx_cfg = cfg.onnx
    obs_dim = cfg.obs_dim
    act_dim = cfg.act_dim

    # Set model to eval mode
    model.eval()

    # Load state dict if provided
    if state_dict is not None:
        model.load_state_dict(state_dict)

    # Move to CPU for export (ONNX is device-agnostic)
    model_cpu = model.cpu()

    # Create dummy input: shape [1, 1, 1, obs_dim] as Unity Sentis expects
    # Unity Sentis TensorShape(1, 1, 1, obs_dim) = NHWC format
    dummy_input = torch.randn(1, 1, 1, obs_dim, dtype=torch.float32)

    if verbose:
        print(f"  [ONNX Export] Input shape:  {list(dummy_input.shape)}")
        print(f"  [ONNX Export] Output shape: [1, {act_dim}]")
        print(f"  [ONNX Export] Input name:   '{onnx_cfg.input_name}'")
        print(f"  [ONNX Export] Output name:  '{onnx_cfg.output_name}'")

    # We need a wrapper model that reshapes NHWC [1,1,1,N] -> [N] for the ActorCritic
    # and then reshapes output back to [1, act_dim]
    class ONNXWrapper(torch.nn.Module):
        """
        Wrapper that adapts NHWC input format [1,1,1,N] to the flat format
        the ActorCritic expects, and reshapes output to [1, act_dim].
        """

        def __init__(self, actor_critic: ActorCritic, obs_dim: int, act_dim: int):
            super().__init__()
            self.actor_critic = actor_critic
            self.obs_dim = obs_dim
            self.act_dim = act_dim

        def forward(self, x: torch.Tensor) -> torch.Tensor:
            # Reshape from NHWC [1, 1, 1, N] to flat [1, N] then squeeze to [N]
            # x shape: [1, 1, 1, obs_dim]
            x = x.view(1, -1)  # [1, obs_dim]
            # Get action (deterministic mean)
            action = self.actor_critic.get_action(x, deterministic=True)
            # Output shape: [1, act_dim]
            return action.view(1, -1)

    # Create the wrapper
    wrapper = ONNXWrapper(model_cpu, obs_dim, act_dim)
    wrapper.eval()

    # Ensure output directory exists
    os.makedirs(os.path.dirname(output_path) or ".", exist_ok=True)

    # Export to ONNX
    try:
        torch.onnx.export(
            wrapper,
            dummy_input,
            output_path,
            input_names=[onnx_cfg.input_name],
            output_names=[onnx_cfg.output_name],
            opset_version=onnx_cfg.opset_version,
            do_constant_folding=True,
            dynamic_axes={
                onnx_cfg.input_name: {0: "batch_size"},
                onnx_cfg.output_name: {0: "batch_size"},
            } if onnx_cfg.dynamic_batch else None,
            export_params=True,
            verbose=False,
            training=torch.onnx.TrainingMode.EVAL,
        )
        logger.info(f"ONNX model exported to: {output_path}")
        if verbose:
            print(f"  [ONNX Export] ✓ Successfully exported to: {output_path}")
            print(f"  [ONNX Export]   File size: {os.path.getsize(output_path) / 1024:.1f} KB")

    except Exception as e:
        raise RuntimeError(f"ONNX export failed: {e}") from e

    # Validate the exported model
    _validate_onnx(output_path, obs_dim, act_dim, onnx_cfg, verbose=verbose)

    return os.path.abspath(output_path)


# ──────────────────────────────────────────────────────────────────────────────
#  Validation
# ──────────────────────────────────────────────────────────────────────────────

def _validate_onnx(
    onnx_path: str,
    obs_dim: int,
    act_dim: int,
    onnx_cfg: ONNXConfig,
    verbose: bool = False,
):
    """
    Validate the exported ONNX model using onnxruntime or onnx.checker.

    Checks:
    - Model loads correctly
    - Input name matches expected
    - Output name matches expected
    - Input/Output shapes are correct
    - Inference produces expected output shape
    """
    errors = []

    # 1. Try onnx.checker
    try:
        import onnx

        model = onnx.load(onnx_path)
        onnx.checker.check_model(model)
        if verbose:
            print(f"  [ONNX Validation] onnx.checker: ✓ Passed")

        # Check input/output names
        graph = model.graph
        input_names = [inp.name for inp in graph.input]
        output_names = [out.name for out in graph.output]

        if onnx_cfg.input_name not in input_names:
            errors.append(f"Input name '{onnx_cfg.input_name}' not found in model. Found: {input_names}")
        else:
            if verbose:
                print(f"  [ONNX Validation] Input name '{onnx_cfg.input_name}': ✓")

        if onnx_cfg.output_name not in output_names:
            errors.append(f"Output name '{onnx_cfg.output_name}' not found in model. Found: {output_names}")
        else:
            if verbose:
                print(f"  [ONNX Validation] Output name '{onnx_cfg.output_name}': ✓")

        # Check input shape
        for inp in graph.input:
            if inp.name == onnx_cfg.input_name:
                shape = [d.dim_value for d in inp.type.tensor_type.shape.dim]
                if verbose:
                    print(f"  [ONNX Validation] Input shape: {shape} (expected [1,1,1,{obs_dim}] or dynamic)")

        # Check output shape
        for out in graph.output:
            if out.name == onnx_cfg.output_name:
                shape = [d.dim_value for d in out.type.tensor_type.shape.dim]
                if verbose:
                    print(f"  [ONNX Validation] Output shape: {shape} (expected [1,{act_dim}] or dynamic)")

    except ImportError:
        if verbose:
            print(f"  [ONNX Validation] onnx package not available — skipping checker validation")
    except Exception as e:
        errors.append(f"onnx.checker validation failed: {e}")

    # 2. Try onnxruntime inference
    try:
        import onnxruntime as ort

        session = ort.InferenceSession(onnx_path)
        input_name = session.get_inputs()[0].name
        output_name = session.get_outputs()[0].name

        # Check names
        if input_name != onnx_cfg.input_name:
            errors.append(f"ORT: Input name mismatch. Expected '{onnx_cfg.input_name}', got '{input_name}'")
        if output_name != onnx_cfg.output_name:
            errors.append(f"ORT: Output name mismatch. Expected '{onnx_cfg.output_name}', got '{output_name}'")

        # Run inference
        dummy_input = np.random.randn(1, 1, 1, obs_dim).astype(np.float32)
        outputs = session.run([output_name], {input_name: dummy_input})
        output = outputs[0]

        # Check output shape
        expected_shape = (1, act_dim)
        if output.shape != expected_shape:
            errors.append(
                f"ORT: Output shape mismatch. Expected {expected_shape}, got {output.shape}"
            )

        if verbose:
            print(f"  [ONNX Validation] onnxruntime inference: ✓")
            print(f"  [ONNX Validation]   Input:  {dummy_input.shape} → Output: {output.shape}")
            print(f"  [ONNX Validation]   Output range: [{output.min():.4f}, {output.max():.4f}]")

    except ImportError:
        if verbose:
            print(f"  [ONNX Validation] onnxruntime not available — skipping runtime validation")
    except Exception as e:
        errors.append(f"onnxruntime validation failed: {e}")

    # Report errors
    if errors:
        error_msg = "\n".join(errors)
        logger.error(f"ONNX validation failed:\n{error_msg}")
        print(f"  [ONNX Validation] ✗ FAILED:")
        for err in errors:
            print(f"    - {err}")
        raise RuntimeError(f"ONNX validation failed:\n{error_msg}")

    if verbose:
        print(f"  [ONNX Validation] ✓ All checks passed")


# ──────────────────────────────────────────────────────────────────────────────
#  Convenience: export from checkpoint
# ──────────────────────────────────────────────────────────────────────────────

def export_from_checkpoint(
    checkpoint_path: str,
    cfg: Config,
    output_path: Optional[str] = None,
    verbose: bool = False,
) -> str:
    """
    Load a training checkpoint and export it to ONNX.

    Args:
        checkpoint_path: Path to the .pt checkpoint file.
        cfg: Configuration object.
        output_path: ONNX output path (default: same name with .onnx extension).
        verbose: Print detailed output.

    Returns:
        Path to the exported .onnx file.
    """
    if output_path is None:
        output_path = os.path.splitext(checkpoint_path)[0] + ".onnx"

    # Create model
    model = ActorCritic(cfg.network, cfg.obs_dim, cfg.act_dim)

    # Load checkpoint
    checkpoint = torch.load(checkpoint_path, map_location="cpu")
    model.load_state_dict(checkpoint["model_state_dict"])

    if verbose:
        print(f"  [Export] Loaded checkpoint from: {checkpoint_path}")
        print(f"  [Export]   Epoch: {checkpoint.get('epoch', 'N/A')}")
        print(f"  [Export]   Best reward: {checkpoint.get('best_reward', 'N/A'):.2f}")

    return export_to_onnx(model, cfg, output_path, verbose=verbose)