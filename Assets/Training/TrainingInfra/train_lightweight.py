"""
Lightweight CPU-based training pipeline for Neural Animation.

Entry point script that:
  - Uses SimpleAnimationEnv and Config
  - Trains with numpy_ppo
  - Exports to ONNX using onnx_writer
  - Copies trained model to Unity Resources

Usage:
  python train_lightweight.py --avatar_type biped --quick
  python train_lightweight.py --avatar_type quadruped --epochs 50
"""

import os
import sys
import argparse
import time
import math
from pathlib import Path

import numpy as np

# Add project path to sys.path
PROJECT_PATH = "/mnt/c/Unity/code"
sys.path.insert(0, os.path.join(PROJECT_PATH, "Assets/Training/TrainingInfra"))

# Import Config (avoid torch import by setting device='cpu' first)
# We need to monkey-patch __post_init__ to avoid torch import
from config import Config, AvatarSpec
# Override __post_init__ to be safe
original_post_init = Config.__post_init__
def _safe_post_init(self):
    self.device = "cpu"  # Force CPU
Config.__post_init__ = _safe_post_init

from simple_animation_env import SimpleAnimationEnv
from numpy_ppo import PPOTrainer, ActorCritic
from onnx_writer import export_policy_to_onnx, validate_onnx


# ══════════════════════════════════════════════════════════════════════════════
#  Paths
# ══════════════════════════════════════════════════════════════════════════════

OUTPUT_DIR = os.path.join(PROJECT_PATH, "Assets/Resources/NeuralModels")
CHECKPOINT_DIR = os.path.join(PROJECT_PATH, "Assets/Training/TrainingInfra/checkpoints")


# ══════════════════════════════════════════════════════════════════════════════
#  Training
# ══════════════════════════════════════════════════════════════════════════════

def train(
    avatar_type: str = "biped",
    epochs: int = 50,
    quick: bool = False,
    verbose: bool = True,
) -> str:
    """
    Train a policy using numpy PPO and export to ONNX.

    Args:
        avatar_type: "biped" or "quadruped".
        epochs: Number of training epochs.
        quick: If True, use 10 epochs and reduced steps.
        verbose: Print progress.

    Returns:
        Path to the exported ONNX file.
    """
    if quick:
        epochs = 10

    # ── Configuration ──
    if verbose:
        print("=" * 60)
        print("  Neural Animation - Lightweight CPU Training")
        print("=" * 60)

    cfg = Config(avatar=avatar_type, device="cpu")
    obs_dim = cfg.obs_dim
    act_dim = cfg.act_dim
    joint_count = cfg.joint_count

    if verbose:
        print(f"  Avatar type:       {avatar_type}")
        print(f"  Observation dim:   {obs_dim}")
        print(f"  Action dim:        {act_dim}")
        print(f"  Joint count:       {joint_count}")
        print(f"  Epochs:            {epochs}")
        print(f"  Hidden sizes:      [64, 64]")
        print(f"  Quick mode:        {quick}")
        print("=" * 60)

    # ── Environment ──
    env = SimpleAnimationEnv(cfg)

    # ── Trainer ──
    # Use reduced steps for quick mode
    n_steps = 1024 if quick else 2048
    mini_epochs = 5 if quick else 10

    trainer = PPOTrainer(
        obs_dim=obs_dim,
        act_dim=act_dim,
        hidden_sizes=(64, 64),
        actor_lr=3e-4,
        critic_lr=3e-4,
        clip_epsilon=0.2,
        entropy_coef=0.01,
        value_loss_coef=0.5,
        gamma=0.99,
        gae_lambda=0.95,
        n_steps=n_steps,
        batch_size=64,
        mini_epochs=mini_epochs,
        normalize_advantages=True,
        max_grad_norm=0.5,
        target_kl=0.02,
    )

    # ── Training Loop ──
    start_time = time.time()
    best_reward = -float("inf")

    for epoch in range(1, epochs + 1):
        progress = (epoch - 1) / max(epochs - 1, 1)

        epoch_start = time.time()
        metrics = trainer.train_epoch(env, progress)
        epoch_time = time.time() - epoch_start

        total_reward = metrics.get("total_reward", 0.0)
        episode_count = metrics.get("episode_count", 1)
        avg_reward = total_reward / max(episode_count, 1)
        policy_loss = metrics.get("policy_loss", 0.0)
        value_loss = metrics.get("value_loss", 0.0)
        entropy = metrics.get("entropy", 0.0)
        approx_kl = metrics.get("approx_kl", 0.0)

        # Track best reward
        if avg_reward > best_reward:
            best_reward = avg_reward

        if verbose:
            elapsed = time.time() - start_time
            print(
                f"  Epoch {epoch:3d}/{epochs} | "
                f"Reward: {avg_reward:7.2f} | "
                f"Policy: {policy_loss:.4f} | "
                f"Value: {value_loss:.4f} | "
                f"Entropy: {entropy:.4f} | "
                f"KL: {approx_kl:.4f} | "
                f"Time: {epoch_time:.1f}s | "
                f"Total: {elapsed:.1f}s"
            )

    total_time = time.time() - start_time
    if verbose:
        print("=" * 60)
        print(f"  Training complete!")
        print(f"  Total time: {total_time:.1f}s ({total_time/60:.1f} min)")
        print(f"  Best avg reward: {best_reward:.2f}")
        print("=" * 60)

    # ── Save checkpoint ──
    os.makedirs(CHECKPOINT_DIR, exist_ok=True)
    checkpoint_path = os.path.join(CHECKPOINT_DIR, f"{avatar_type}_policy.npz")
    trainer.save(checkpoint_path)

    if verbose:
        print(f"  [Checkpoint] Saved to: {checkpoint_path}")

    # ── Export to ONNX ──
    if verbose:
        print(f"\n  Exporting to ONNX...")

    actor_weights = trainer.model.get_actor_weights()

    # Ensure output dir exists
    os.makedirs(OUTPUT_DIR, exist_ok=True)

    onnx_filename = f"locomotion_{avatar_type}_base.onnx"
    onnx_path = os.path.join(OUTPUT_DIR, onnx_filename)

    export_policy_to_onnx(
        weights=actor_weights,
        obs_dim=obs_dim,
        act_dim=act_dim,
        output_path=onnx_path,
        verbose=verbose,
    )

    # ── Validate ──
    if verbose:
        print(f"\n  Validating ONNX model...")

    valid = validate_onnx(onnx_path, obs_dim, act_dim, verbose=verbose)

    if verbose:
        if valid:
            print(f"  ✓ ONNX model is valid: {onnx_path}")
        else:
            print(f"  ✗ ONNX model validation failed!")

    return onnx_path


# ══════════════════════════════════════════════════════════════════════════════
#  CLI
# ══════════════════════════════════════════════════════════════════════════════

def main():
    parser = argparse.ArgumentParser(
        description="Lightweight CPU-based PPO training for Neural Animation",
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    parser.add_argument(
        "--avatar_type", "-a",
        type=str,
        default="biped",
        choices=["biped", "quadruped"],
        help="Avatar type: biped (obs=120, act=80) or quadruped (obs=150, act=100) (default: biped)",
    )
    parser.add_argument(
        "--epochs", "-e",
        type=int,
        default=50,
        help="Number of training epochs (default: 50)",
    )
    parser.add_argument(
        "--quick", "-q",
        action="store_true",
        help="Quick training: 10 epochs with reduced steps (for testing)",
    )
    parser.add_argument(
        "--verbose", "-v",
        action="store_true",
        default=True,
        help="Print progress (default: True)",
    )
    parser.add_argument(
        "--quiet", "-Q",
        action="store_true",
        help="Suppress all output",
    )

    args = parser.parse_args()

    if args.quiet:
        args.verbose = False

    onnx_path = train(
        avatar_type=args.avatar_type,
        epochs=args.epochs,
        quick=args.quick,
        verbose=args.verbose,
    )

    if args.verbose:
        print(f"\n  ✓ Training complete! ONNX model at: {onnx_path}")


if __name__ == "__main__":
    main()