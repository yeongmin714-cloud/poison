#!/usr/bin/env python3
"""
PyTorch PPO Training Pipeline for Neural Animation.

Integrates:
- config.py: Configuration (avatar specs, network [256,128,64], PPO hyperparams)
- simple_animation_env.py: Simulated animation environment
- torch_ppo.py: PyTorch PPO with [256,128,64] MLP, GAE, clipped surrogate
- torch_to_onnx.py: ONNX export (torch.onnx.export + manual protobuf fallback)

Usage:
  python train_torch.py --avatar_type biped --quick
  python train_torch.py --avatar_type quadruped --epochs 100 --policy_type combat --curriculum
  python train_torch.py --avatar_type biped --policy_type locomotion --epochs 100 --tensorboard --onnx_export
  python train_torch.py --avatar_type quadruped --policy_type fly --ensemble_seeds "42,123,456" --onnx_export
  python train_torch.py --checkpoint models/biped_locomotion_policy.pt --onnx_export --export_only
"""

import os
import sys
import argparse
import time
import math
import json
from pathlib import Path
from typing import Optional, Dict, Any, List, Tuple, Literal

import numpy as np

# Import torch after path setup to avoid CUDA issues
try:
    import torch
    HAS_TORCH = True
except ImportError:
    HAS_TORCH = False
    torch = None

# Add project path
PROJECT_PATH = "/mnt/c/Unity/code"
sys.path.insert(0, os.path.join(PROJECT_PATH, "Assets/Training/TrainingInfra"))

# Import config (force CPU before torch import in config)
from config import Config, AvatarSpec

# Force CPU in config to avoid CUDA init issues on headless systems
original_post_init = Config.__post_init__
def _cpu_post_init(self):
    self.device = "cpu"
Config.__post_init__ = _cpu_post_init

from simple_animation_env import SimpleAnimationEnv
from torch_ppo import PPOTrainer, validate_onnx
from torch_to_onnx import export_from_checkpoint, export_onnx_manual

# ──────────────────────────────────────────────────────────────────────────────
# Paths
# ──────────────────────────────────────────────────────────────────────────────

OUTPUT_DIR = os.path.join(PROJECT_PATH, "Assets/Resources/NeuralModels")
CHECKPOINT_DIR = os.path.join(PROJECT_PATH, "Assets/Training/TrainingInfra/checkpoints")
TENSORBOARD_DIR = os.path.join(PROJECT_PATH, "Assets/Training/TrainingInfra/runs")


# ──────────────────────────────────────────────────────────────────────────────
# Training Function
# ──────────────────────────────────────────────────────────────────────────────

def train(
    avatar_type: str = "biped",
    policy_type: str = "locomotion",
    epochs: int = 100,
    quick: bool = False,
    verbose: bool = True,
    curriculum: bool = False,
    style_embedding: int = 0,
    ensemble_seeds: str = "",
    tensorboard_log: bool = False,
    checkpoint_interval: int = 25,
    onnx_export: bool = True,
    export_only: bool = False,
    checkpoint_path: Optional[str] = None,
    opset_version: int = 17,
) -> str:
    """
    Train a PyTorch PPO policy and export to ONNX.

    Args:
        avatar_type: "biped" or "quadruped" (also "fly", "swim" for quadruped variants)
        policy_type: "locomotion", "combat", "react", "interact", "fly", "swim", "mount", "climb", "run", "crouch", "large_monster"
        epochs: Number of training epochs
        quick: Quick mode - 10 epochs, reduced steps
        verbose: Print progress
        curriculum: Enable curriculum learning (easy -> medium -> hard terrain)
        style_embedding: Style embedding index (0=walk, 1=run, 2=crouch, 3=custom)
        ensemble_seeds: Comma-separated seeds for ensemble training
        tensorboard_log: Enable TensorBoard logging
        checkpoint_interval: Save checkpoint every N epochs
        onnx_export: Export ONNX after training
        export_only: Skip training, only export ONNX from checkpoint
        checkpoint_path: Path to checkpoint for export_only mode
        opset_version: ONNX opset version

    Returns:
        Path to exported ONNX file
    """
    if quick:
        epochs = 10

    # ── Configuration ──
    if verbose:
        print("=" * 60)
        print("  Neural Animation - PyTorch PPO Training")
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
        print(f"  Hidden sizes:      {cfg.network.hidden_sizes}")
        print(f"  Quick mode:        {quick}")
        print(f"  Curriculum:        {curriculum}")
        print(f"  Style embedding:   {style_embedding}")
        print(f"  Ensemble seeds:    {ensemble_seeds if ensemble_seeds else 'none'}")
        print(f"  TensorBoard:       {tensorboard_log}")
        print(f"  ONNX export:       {onnx_export}")
        print(f"  Checkpoint dir:    {CHECKPOINT_DIR}")
        print(f"  Output dir:        {OUTPUT_DIR}")
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
            run_name = f"{policy_type}_{avatar_type}_{int(time.time())}"
            tb_writer = SummaryWriter(os.path.join(TENSORBOARD_DIR, run_name))
            if verbose:
                print(f"  [TensorBoard] Logging to: {tb_writer.log_dir}")
        except ImportError:
            if verbose:
                print("  [TensorBoard] torch.utils.tensorboard not available, skipping")

    # ── Trainer ──
    n_steps = cfg.ppo.n_steps if not quick else 1024
    mini_epochs = cfg.ppo.mini_epochs if not quick else 5

    trainer = PPOTrainer(
        obs_dim=obs_dim,
        act_dim=act_dim,
        hidden_sizes=cfg.network.hidden_sizes,
        actor_lr=cfg.ppo.actor_lr,
        critic_lr=cfg.ppo.critic_lr,
        clip_epsilon=cfg.ppo.clip_epsilon,
        entropy_coef=cfg.ppo.entropy_coef,
        value_loss_coef=cfg.ppo.value_loss_coef,
        gamma=cfg.ppo.gamma,
        gae_lambda=cfg.ppo.gae_lambda,
        n_steps=n_steps,
        batch_size=cfg.ppo.batch_size,
        mini_epochs=mini_epochs,
        normalize_advantages=cfg.ppo.normalize_advantages,
        max_grad_norm=cfg.ppo.max_grad_norm,
        target_kl=cfg.ppo.target_kl,
        device=cfg.device,
    )

    # ── Export Only Mode ──
    if export_only:
        if not checkpoint_path or not os.path.exists(checkpoint_path):
            raise FileNotFoundError(f"Checkpoint not found: {checkpoint_path}")
        
        if verbose:
            print(f"\n  [Export Only] Loading checkpoint: {checkpoint_path}")
        
        trainer.load(checkpoint_path)
        
        if onnx_export:
            onnx_path = export_onnx_model(
                trainer, cfg, policy_type, avatar_type, opset_version, verbose
            )
        return onnx_path if onnx_export else checkpoint_path

    # ── Training Loop ──
    start_time = time.time()
    best_reward = -float("inf")
    best_checkpoint_path = None

    os.makedirs(CHECKPOINT_DIR, exist_ok=True)

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
        actor_lr = metrics.get("actor_lr", 0.0)
        critic_lr = metrics.get("critic_lr", 0.0)

        # Track best reward
        if avg_reward > best_reward:
            best_reward = avg_reward
            # Save best checkpoint
            best_checkpoint_path = os.path.join(
                CHECKPOINT_DIR, f"{avatar_type}_{policy_type}_policy_best.pt"
            )
            trainer.save(best_checkpoint_path)
            if verbose:
                print(f"  [Checkpoint] New best model saved: {best_checkpoint_path}")

        # Save periodic checkpoint
        if epoch % checkpoint_interval == 0:
            checkpoint_path_epoch = os.path.join(
                CHECKPOINT_DIR, f"{avatar_type}_{policy_type}_policy_epoch{epoch}.pt"
            )
            trainer.save(checkpoint_path_epoch)
            if verbose:
                print(f"  [Checkpoint] Saved epoch {epoch}: {checkpoint_path_epoch}")

        # TensorBoard logging
        if tb_writer:
            tb_writer.add_scalar("Reward/avg", avg_reward, epoch)
            tb_writer.add_scalar("Reward/total", total_reward, epoch)
            tb_writer.add_scalar("Loss/policy", policy_loss, epoch)
            tb_writer.add_scalar("Loss/value", value_loss, epoch)
            tb_writer.add_scalar("Entropy", entropy, epoch)
            tb_writer.add_scalar("KL/approx", approx_kl, epoch)
            tb_writer.add_scalar("LR/actor", actor_lr, epoch)
            tb_writer.add_scalar("LR/critic", critic_lr, epoch)
            tb_writer.add_scalar("Time/epoch", epoch_time, epoch)

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

    # ── Save Final Checkpoint ──
    final_checkpoint_path = os.path.join(
        CHECKPOINT_DIR, f"{avatar_type}_{policy_type}_policy_final.pt"
    )
    trainer.save(final_checkpoint_path)
    if verbose:
        print(f"  [Checkpoint] Final model saved: {final_checkpoint_path}")

    # ── Ensemble Training (if seeds provided) ──
    if ensemble_seeds:
        if verbose:
            print(f"\n  [Ensemble] Training with seeds: {ensemble_seeds}")
        
        seed_list = [int(s.strip()) for s in ensemble_seeds.split(",")]
        ensemble_checkpoints = []

        for seed in seed_list:
            if verbose:
                print(f"  [Ensemble] Training seed {seed}...")
            
            # Reset seeds
            np.random.seed(seed)
            torch.manual_seed(seed)
            env.seed(seed)

            # Create new trainer with same config
            ensemble_trainer = PPOTrainer(
                obs_dim=obs_dim,
                act_dim=act_dim,
                hidden_sizes=cfg.network.hidden_sizes,
                actor_lr=cfg.ppo.actor_lr,
                critic_lr=cfg.ppo.critic_lr,
                clip_epsilon=cfg.ppo.clip_epsilon,
                entropy_coef=cfg.ppo.entropy_coef,
                value_loss_coef=cfg.ppo.value_loss_coef,
                gamma=cfg.ppo.gamma,
                gae_lambda=cfg.ppo.gae_lambda,
                n_steps=n_steps,
                batch_size=cfg.ppo.batch_size,
                mini_epochs=mini_epochs,
                normalize_advantages=cfg.ppo.normalize_advantages,
                max_grad_norm=cfg.ppo.max_grad_norm,
                target_kl=cfg.ppo.target_kl,
                device=cfg.device,
            )

            # Quick training for ensemble member
            for e in range(epochs):
                ensemble_progress = e / max(epochs - 1, 1)
                ensemble_trainer.train_epoch(env, ensemble_progress)

            # Save ensemble member checkpoint
            ensemble_path = os.path.join(
                CHECKPOINT_DIR, f"{avatar_type}_{policy_type}_ensemble_seed{seed}.pt"
            )
            ensemble_trainer.save(ensemble_path)
            ensemble_checkpoints.append(ensemble_path)

        if verbose:
            print(f"  [Ensemble] Trained {len(ensemble_checkpoints)} models")

    # ── Export to ONNX ──
    onnx_path = ""
    if onnx_export:
        if verbose:
            print(f"\n  Exporting to ONNX...")

        os.makedirs(OUTPUT_DIR, exist_ok=True)

        onnx_path = export_onnx_model(
            trainer, cfg, policy_type, avatar_type, opset_version, verbose
        )

        # Validate
        if verbose:
            print(f"\n  Validating ONNX model...")
        
        valid = validate_onnx(onnx_path, obs_dim, act_dim, verbose=verbose)

        if verbose:
            if valid:
                print(f"  ✓ ONNX model is valid: {onnx_path}")
            else:
                print(f"  ✗ ONNX model validation failed!")

    # Save training config for reference
    config_path = os.path.join(CHECKPOINT_DIR, f"{avatar_type}_{policy_type}_config.json")
    with open(config_path, 'w') as f:
        json.dump(cfg.to_dict(), f, indent=2)

    return onnx_path if onnx_export else final_checkpoint_path


def export_onnx_model(
    trainer: 'PPOTrainer',
    cfg: Config,
    policy_type: str,
    avatar_type: str,
    opset_version: int = 17,
    verbose: bool = True
) -> str:
    """Export the trained model to ONNX using both torch.onnx and manual protobuf."""
    
    onnx_filename = f"{policy_type}_{avatar_type}_base.onnx"
    onnx_path = os.path.join(OUTPUT_DIR, onnx_filename)
    
    # Try torch.onnx.export first (native)
    try:
        if verbose:
            print(f"  [ONNX] Attempting torch.onnx.export...")
        exported_path = trainer.export_onnx(onnx_path, opset_version)
        if verbose:
            print(f"  [ONNX] Native export successful: {exported_path}")
        return exported_path
    except Exception as e:
        if verbose:
            print(f"  [ONNX] Native export failed: {e}")
            print(f"  [ONNX] Falling back to manual protobuf export...")
        
        # Fallback to manual protobuf export
        try:
            # Save temporary checkpoint
            temp_checkpoint = os.path.join(OUTPUT_DIR, f"temp_{avatar_type}_{policy_type}.pt")
            trainer.save(temp_checkpoint)
            
            # Export using manual protobuf method
            exported_path = export_from_checkpoint(
                temp_checkpoint, onnx_path, cfg.obs_dim, cfg.act_dim, cfg.network.hidden_sizes
            )
            
            # Clean up temp file
            if os.path.exists(temp_checkpoint):
                os.remove(temp_checkpoint)
            
            if verbose:
                print(f"  [ONNX] Manual export successful: {exported_path}")
            return exported_path
            
        except Exception as e2:
            if verbose:
                print(f"  [ONNX] Manual export also failed: {e2}")
            raise RuntimeError(f"Both ONNX export methods failed. Native: {e}, Manual: {e2}")


# ──────────────────────────────────────────────────────────────────────────────
# CLI
# ──────────────────────────────────────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser(
        description="PyTorch PPO Training for Neural Animation",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Quick test (10 epochs)
  python train_torch.py --avatar_type biped --quick

  # Full training (100 epochs) with curriculum
  python train_torch.py --avatar_type quadruped --epochs 100 --policy_type combat --curriculum

  # Full training with TensorBoard and ONNX export
  python train_torch.py --avatar_type biped --policy_type locomotion --epochs 100 --tensorboard --onnx_export

  # Ensemble training
  python train_torch.py --avatar_type quadruped --policy_type fly --ensemble_seeds "42,123,456" --onnx_export

  # Export ONNX only from existing checkpoint
  python train_torch.py --checkpoint checkpoints/biped_locomotion_policy_final.pt --onnx_export --export_only

  # Custom ONNX opset version
  python train_torch.py --avatar_type biped --quick --onnx_export --opset 17
        """
    )
    
    parser.add_argument(
        "--avatar_type", "-a",
        type=str,
        default="biped",
        choices=["biped", "quadruped", "fly", "swim"],
        help="Avatar type: biped (obs=120, act=80) or quadruped/fly/swim (obs=150, act=100) (default: biped)"
    )
    parser.add_argument(
        "--policy_type", "-p",
        type=str,
        default="locomotion",
        choices=["locomotion", "combat", "react", "interact", "fly", "swim", "mount", "climb", "run", "crouch", "large_monster"],
        help="Policy type (default: locomotion)"
    )
    parser.add_argument(
        "--epochs", "-e",
        type=int,
        default=100,
        help="Number of training epochs (default: 100)"
    )
    parser.add_argument(
        "--quick", "-q",
        action="store_true",
        help="Quick training: 10 epochs with reduced steps"
    )
    parser.add_argument(
        "--curriculum", "-c",
        action="store_true",
        help="Enable curriculum learning: easy -> medium -> hard terrain progression"
    )
    parser.add_argument(
        "--style_embedding", "-s",
        type=int,
        default=0,
        help="Style embedding index (0=walk, 1=run, 2=crouch, 3=custom)"
    )
    parser.add_argument(
        "--ensemble_seeds", "-es",
        type=str,
        default="",
        help="Comma-separated seeds for ensemble training (e.g., '42,123,456')"
    )
    parser.add_argument(
        "--tensorboard", "-tb",
        action="store_true",
        help="Enable TensorBoard logging"
    )
    parser.add_argument(
        "--onnx_export", "-oe",
        action="store_true",
        default=True,
        help="Export ONNX after training (default: True)"
    )
    parser.add_argument(
        "--no_onnx_export",
        action="store_true",
        help="Disable ONNX export"
    )
    parser.add_argument(
        "--checkpoint_interval", "-ci",
        type=int,
        default=25,
        help="Save checkpoint every N epochs (default: 25)"
    )
    parser.add_argument(
        "--checkpoint", "-ckpt",
        type=str,
        default="",
        help="Path to checkpoint for export_only mode"
    )
    parser.add_argument(
        "--export_only", "-eo",
        action="store_true",
        help="Skip training, only export ONNX from checkpoint"
    )
    parser.add_argument(
        "--opset",
        type=int,
        default=17,
        help="ONNX opset version (default: 17)"
    )
    parser.add_argument(
        "--verbose", "-v",
        action="store_true",
        default=True,
        help="Print progress (default: True)"
    )
    parser.add_argument(
        "--quiet", "-Q",
        action="store_true",
        help="Suppress all output"
    )

    args = parser.parse_args()

    if args.quiet:
        args.verbose = False

    # Handle onnx_export flag
    onnx_export = args.onnx_export and not args.no_onnx_export

    try:
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
            checkpoint_interval=args.checkpoint_interval,
            onnx_export=onnx_export,
            export_only=args.export_only,
            checkpoint_path=args.checkpoint if args.checkpoint else None,
            opset_version=args.opset,
        )

        if args.verbose:
            if onnx_path:
                print(f"\n  ✓ Training complete! ONNX model at: {onnx_path}")
            else:
                print(f"\n  ✓ Training complete! Checkpoint at: {CHECKPOINT_DIR}")

    except Exception as e:
        if args.verbose:
            print(f"\n  ✗ Error: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)


if __name__ == "__main__":
    main()