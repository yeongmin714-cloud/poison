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
  python train_lightweight.py --avatar_type biped --policy_type combat --curriculum --tensorboard
  python train_lightweight.py --avatar_type quadruped --policy_type fly --ensemble_seeds "42,123,456" --style_embedding 1
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
    policy_type: str = "locomotion",
    epochs: int = 50,
    quick: bool = False,
    verbose: bool = True,
    curriculum: bool = False,
    style_embedding: int = 0,
    ensemble_seeds: str = "",
    tensorboard_log: bool = False,
) -> str:
    """
    Train a policy using numpy PPO and export to ONNX.

    Args:
        avatar_type: "biped" or "quadruped".
        policy_type: "locomotion", "combat", "react", "interact", "fly", "swim".
        epochs: Number of training epochs.
        quick: If True, use 10 epochs and reduced steps.
        verbose: Print progress.
        curriculum: Enable curriculum learning (easy -> medium -> hard terrain).
        style_embedding: Style embedding index for conditional policy.
        ensemble_seeds: Comma-separated seeds for ensemble training.
        tensorboard_log: Enable TensorBoard logging.

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
        print(f"  Policy type:       {policy_type}")
        print(f"  Observation dim:   {obs_dim}")
        print(f"  Action dim:        {act_dim}")
        print(f"  Joint count:       {joint_count}")
        print(f"  Epochs:            {epochs}")
        print(f"  Hidden sizes:      [64, 64]")
        print(f"  Quick mode:        {quick}")
        print(f"  Curriculum:        {curriculum}")
        print(f"  Style embedding:   {style_embedding}")
        print(f"  Ensemble seeds:    {ensemble_seeds if ensemble_seeds else 'none'}")
        print(f"  TensorBoard:       {tensorboard_log}")
        print("=" * 60)

    # ── Environment ──
    env = SimpleAnimationEnv(cfg, policy_type=policy_type)

    # Configure curriculum if enabled
    if curriculum:
        env.set_curriculum_enabled(True)
        if verbose:
            print("  [Curriculum] Enabled: Easy -> Medium -> Hard terrain progression")

    # Configure style embedding if specified
    if style_embedding > 0:
        env.set_style_embedding(style_embedding)
        if verbose:
            print(f"  [Style] Conditional policy with embedding index: {style_embedding}")

    # ── TensorBoard ──
    tb_writer = None
    if tensorboard_log and verbose:
        try:
            from torch.utils.tensorboard import SummaryWriter
            tb_writer = SummaryWriter(f"runs/{policy_type}_{avatar_type}_{int(time.time())}")
            if verbose:
                print(f"  [TensorBoard] Logging to runs/")
        except ImportError:
            if verbose:
                print("  [TensorBoard] torch not available, skipping")

    # ── Trainer ──
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

        # Update curriculum phase based on progress
        if curriculum:
            if progress < 0.33:
                env.set_curriculum_phase(0)  # Easy
            elif progress < 0.66:
                env.set_curriculum_phase(1)  # Medium
            else:
                env.set_curriculum_phase(2)  # Hard

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

        # TensorBoard logging
        if tb_writer:
            tb_writer.add_scalar("Reward/avg", avg_reward, epoch)
            tb_writer.add_scalar("Loss/policy", policy_loss, epoch)
            tb_writer.add_scalar("Loss/value", value_loss, epoch)
            tb_writer.add_scalar("Entropy", entropy, epoch)
            tb_writer.add_scalar("KL", approx_kl, epoch)

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

    # Close TensorBoard
    if tb_writer:
        tb_writer.close()

    # Get actor weights from main trainer (used if no ensemble)
    actor_weights = trainer.model.get_actor_weights()

    # ── Ensemble Training (if seeds provided) ──
    if ensemble_seeds:
        if verbose:
            print(f"\n  [Ensemble] Training with seeds: {ensemble_seeds}")
        seed_list = [int(s.strip()) for s in ensemble_seeds.split(",")]
        ensemble_weights = []

        for seed in seed_list:
            if verbose:
                print(f"  [Ensemble] Training seed {seed}...")
            np.random.seed(seed)
            env.seed(seed)

            # Retrain from scratch with different seed
            ensemble_trainer = PPOTrainer(
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

            # Quick training for ensemble member
            for e in range(epochs):
                ensemble_progress = e / max(epochs - 1, 1)
                ensemble_trainer.train_epoch(env, ensemble_progress)

            ensemble_weights.append(ensemble_trainer.model.get_actor_weights())

        # Average ensemble weights (list of tuples format)
        if verbose:
            print(f"  [Ensemble] Averaging {len(ensemble_weights)} models...")
        avg_weights = []
        for i in range(len(ensemble_weights[0])):
            w_avg = np.mean([w[i][0] for w in ensemble_weights], axis=0)
            b_avg = np.mean([w[i][1] for w in ensemble_weights], axis=0)
            avg_weights.append((w_avg, b_avg))

        actor_weights = avg_weights

    # ── Save checkpoint ──
    os.makedirs(CHECKPOINT_DIR, exist_ok=True)
    checkpoint_path = os.path.join(CHECKPOINT_DIR, f"{avatar_type}_{policy_type}_policy.npz")
    trainer.save(checkpoint_path)

    if verbose:
        print(f"  [Checkpoint] Saved to: {checkpoint_path}")

    # ── Export to ONNX ──
    if verbose:
        print(f"\n  Exporting to ONNX...")

    # Ensure output dir exists
    os.makedirs(OUTPUT_DIR, exist_ok=True)

    onnx_filename = f"{policy_type}_{avatar_type}_base.onnx"
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
        "--policy_type", "-p",
        type=str,
        default="locomotion",
        choices=["locomotion", "combat", "react", "interact", "fly", "swim", "mount", "climb", "run", "crouch", "large_monster"],
        help="Policy type: locomotion, combat, react, interact, fly, swim, mount, climb, run, crouch, large_monster (default: locomotion)",
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
        "--curriculum", "-c",
        action="store_true",
        help="Enable curriculum learning: easy -> medium -> hard terrain progression",
    )
    parser.add_argument(
        "--style_embedding", "-s",
        type=int,
        default=0,
        help="Style embedding index (0=walk, 1=run, 2=crouch, 3=custom). Adds conditional input to policy.",
    )
    parser.add_argument(
        "--ensemble_seeds", "-es",
        type=str,
        default="",
        help="Comma-separated seeds for ensemble training (e.g., '42,123,456'). Trains multiple models and averages weights.",
    )
    parser.add_argument(
        "--tensorboard", "-tb",
        action="store_true",
        help="Enable TensorBoard logging for training curves",
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
        policy_type=args.policy_type,
        epochs=args.epochs,
        quick=args.quick,
        verbose=args.verbose,
        curriculum=args.curriculum,
        style_embedding=args.style_embedding,
        ensemble_seeds=args.ensemble_seeds,
        tensorboard_log=args.tensorboard,
    )

    if args.verbose:
        print(f"\n  ✓ Training complete! ONNX model at: {onnx_path}")


if __name__ == "__main__":
    main()