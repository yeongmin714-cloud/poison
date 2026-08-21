#!/usr/bin/env python3
"""
Convert PyTorch PPO model weights to ONNX using protobuf (hand-crafted).

This approach avoids PyTorch 2.13's buggy ONNX exporter by directly
constructing the ONNX graph using protobuf, same as onnx_writer.py.

The ONNX has:
  Input: "observation" shape [1, 1, 1, obs_dim] (NHWC for Unity Sentis)
  Network: Reshape → Gemm → Tanh → Gemm → Tanh → Gemm → action
  Output: "action" shape [1, act_dim]
  Opset: 17
"""

import os
import struct
import numpy as np
from typing import Dict, Any, List, Tuple

try:
    import onnx
    from onnx import helper, TensorProto
    import onnx.numpy_helper as np_helper
    # NP_TYPE_TO_TENSOR_TYPE is in helper in newer onnx versions
    try:
        from onnx.helper import np_dtype_to_tensor_dtype
        def _dtype_lookup(d):
            result = np_dtype_to_tensor_dtype(d)
            if result is not None:
                return result
            return TensorProto.FLOAT
    except ImportError:
        def _dtype_lookup(d):
            return TensorProto.FLOAT
    HAS_ONNX = True
except ImportError:
    HAS_ONNX = False


def _make_tensor(name: str, data: np.ndarray) -> TensorProto:
    """Create an ONNX TensorProto from a numpy array."""
    dtype = _dtype_lookup(data.dtype)
    dims = list(data.shape)
    return helper.make_tensor(name, dtype, dims, data.tobytes(), raw=True)


def _make_gemm_node(name: str, input_name: str, weight: np.ndarray,
                    bias: np.ndarray, trans_b: bool = True) -> Tuple[onnx.NodeProto, str, str, str]:
    """Create a Gemm node (alpha=1, beta=1, transB=1).

    Returns: (node, weight_name, bias_name, output_name)
    """
    w_name = f"{name}_w"
    b_name = f"{name}_b"
    out_name = f"{name}_out"

    w_tensor = _make_tensor(w_name, weight)
    b_tensor = _make_tensor(b_name, bias)

    node = helper.make_node(
        "Gemm",
        inputs=[input_name, w_name, b_name],
        outputs=[out_name],
        name=name,
        alpha=1.0,
        beta=1.0,
        transB=1 if trans_b else 0
    )
    return node, w_tensor, b_tensor, out_name


def export_onnx_manual(
    weights: Dict[str, np.ndarray],
    output_path: str,
    obs_dim: int = 120,
    act_dim: int = 80,
    hidden_sizes: List[int] = None,
    opset_version: int = 17
) -> str:
    """
    Export weights to ONNX using protobuf.

    Args:
        weights: Dict with keys:
            'trunk.0.weight', 'trunk.0.bias',
            'trunk.2.weight', 'trunk.2.bias',
            'trunk.4.weight', 'trunk.4.bias',
            'mean_head.weight', 'mean_head.bias'
        output_path: Path to save ONNX file.
        obs_dim: Observation dimension.
        act_dim: Action dimension.
        hidden_sizes: Hidden layer sizes [h1, h2, h3].
        opset_version: ONNX opset version.

    Returns:
        Absolute path to the written .onnx file.
    """
    if not HAS_ONNX:
        raise ImportError("onnx package is required. Install with: pip install onnx")

    if hidden_sizes is None:
        hidden_sizes = [256, 128, 64]

    os.makedirs(os.path.dirname(output_path) or ".", exist_ok=True)

    # ── Build graph ──────────────────────────────────────────────────────
    nodes = []
    tensors = []
    inputs = []
    outputs = []

    # Input: "observation" shape [1, 1, 1, obs_dim] (NHWC)
    obs_type = helper.make_tensor_value_info("observation", TensorProto.FLOAT,
                                              [1, 1, 1, obs_dim])
    inputs.append(obs_type)

    # Reshape: [1, 1, 1, obs_dim] → [1, obs_dim]
    # We need a shape tensor for Reshape
    shape_data = np.array([1, obs_dim], dtype=np.int64)
    shape_tensor = _make_tensor("reshape_shape", shape_data)
    tensors.append(shape_tensor)

    reshape_node = helper.make_node(
        "Reshape",
        inputs=["observation", "reshape_shape"],
        outputs=["flat_input"],
        name="reshape_input"
    )
    nodes.append(reshape_node)

    # ── Hidden layers ────────────────────────────────────────────────────
    current_input = "flat_input"
    for i, (h_in, h_out) in enumerate(zip([obs_dim] + hidden_sizes[:-1], hidden_sizes)):
        idx = i * 2  # trunk.0, trunk.2, trunk.4 (Tanh layers take trunk.1, trunk.3, trunk.5)
        w_key = f"trunk.{idx}.weight"
        b_key = f"trunk.{idx}.bias"

        w = weights[w_key]  # shape: [h_out, h_in]
        b = weights[b_key]  # shape: [h_out]

        gemm_node, w_t, b_t, gemm_out = _make_gemm_node(
            f"gemm_{i}", current_input, w, b, trans_b=True
        )
        nodes.append(gemm_node)
        tensors.extend([w_t, b_t])

        # Tanh activation
        tanh_out = f"tanh_{i}_out"
        tanh_node = helper.make_node(
            "Tanh",
            inputs=[gemm_out],
            outputs=[tanh_out],
            name=f"tanh_{i}"
        )
        nodes.append(tanh_node)

        current_input = tanh_out

    # ── Mean head (no activation) ────────────────────────────────────────
    w_mean = weights["mean_head.weight"]  # shape: [act_dim, h3]
    b_mean = weights["mean_head.bias"]    # shape: [act_dim]

    # Last Gemm outputs directly to "action" (matches output name)
    w_mean_t = _make_tensor("gemm_mean_w", w_mean)
    b_mean_t = _make_tensor("gemm_mean_b", b_mean)
    tensors.extend([w_mean_t, b_mean_t])

    mean_node = helper.make_node(
        "Gemm",
        inputs=[current_input, "gemm_mean_w", "gemm_mean_b"],
        outputs=["action"],
        name="gemm_mean",
        alpha=1.0, beta=1.0, transB=1
    )
    nodes.append(mean_node)

    # Output: "action" shape [1, act_dim]
    action_type = helper.make_tensor_value_info("action", TensorProto.FLOAT,
                                                 [1, act_dim])
    outputs.append(action_type)

    # ── Create graph ─────────────────────────────────────────────────────
    graph = helper.make_graph(
        nodes=nodes,
        name="ppo_actor",
        inputs=inputs,
        outputs=outputs,
        initializer=tensors
    )

    # ── Create model ─────────────────────────────────────────────────────
    model = helper.make_model(graph, opset_imports=[
        helper.make_opsetid("", opset_version)
    ])
    model.ir_version = onnx.IR_VERSION

    # ── Save ─────────────────────────────────────────────────────────────
    onnx.save(model, output_path)
    return os.path.abspath(output_path)


def export_from_checkpoint(
    checkpoint_path: str,
    output_path: str,
    obs_dim: int = 120,
    act_dim: int = 80,
    hidden_sizes: List[int] = None
) -> str:
    """
    Load a PyTorch checkpoint (.pt) and export to ONNX using protobuf.

    Args:
        checkpoint_path: Path to .pt checkpoint file.
        output_path: Path to save ONNX.
        obs_dim: Observation dimension.
        act_dim: Action dimension.
        hidden_sizes: Hidden layer sizes.

    Returns:
        Absolute path to the written .onnx file.
    """
    import torch

    checkpoint = torch.load(checkpoint_path, map_location='cpu', weights_only=True)
    state_dict = checkpoint.get('model_state_dict', checkpoint)

    # Extract weights
    weights = {}
    for key in ['trunk.0.weight', 'trunk.0.bias',
                'trunk.2.weight', 'trunk.2.bias',
                'trunk.4.weight', 'trunk.4.bias',
                'mean_head.weight', 'mean_head.bias']:
        if key in state_dict:
            weights[key] = state_dict[key].cpu().numpy()
        else:
            # Try with actor_critic prefix
            alt_key = f'actor_critic.{key}' if not key.startswith('actor_critic.') else key
            if alt_key in state_dict:
                weights[key] = state_dict[alt_key].cpu().numpy()
            else:
                raise KeyError(f"Weight key '{key}' not found in checkpoint. "
                               f"Available keys: {[k for k in state_dict.keys() if 'trunk' in k or 'mean' in k]}")

    return export_onnx_manual(weights, output_path, obs_dim, act_dim, hidden_sizes)


# ── CLI entry point ──────────────────────────────────────────────────────
if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description="Export PyTorch PPO checkpoint to ONNX")
    parser.add_argument("checkpoint", help="Path to .pt checkpoint file")
    parser.add_argument("output", help="Path to output .onnx file")
    parser.add_argument("--obs-dim", type=int, default=120, help="Observation dimension")
    parser.add_argument("--act-dim", type=int, default=80, help="Action dimension")
    parser.add_argument("--hidden-sizes", type=int, nargs="+", default=[256, 128, 64],
                        help="Hidden layer sizes")
    args = parser.parse_args()

    path = export_from_checkpoint(
        args.checkpoint, args.output,
        args.obs_dim, args.act_dim, args.hidden_sizes
    )
    print(f"ONNX exported to: {path}")
    print(f"Size: {os.path.getsize(path) / 1024:.1f} KB")