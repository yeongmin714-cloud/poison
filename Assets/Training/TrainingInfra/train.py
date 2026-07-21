#!/usr/bin/env python3
"""
Main entry point for Neural Animation PPO training.

Usage:
    python train.py --avatar_type biped --epochs 100 --batch_size 64
    python train.py --avatar_type quadruped --epochs 200 --device cuda
    python train.py --avatar_type biped --resume models/neural_animation_ppo/checkpoint_epoch_0050.pt
    python train.py --avatar_type biped --export-only models/neural_animation_ppo/best_model.pt

This script:
1. Creates or loads configuration
2. Initializes the environment
3. Sets up the PPO trainer
4. Runs the training loop with periodic evaluation
5. Exports the best model to ONNX
"""

import argparse
import os
import sys
import logging
from pathlib import Path

# Ensure the training infra directory is in the path
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
if SCRIPT_DIR not in sys.path:
    sys.path.insert(0, SCRIPT_DIR)

from config import Config, AvatarSpec
from simple_animation_env import SimpleAnimationEnv, create_env
from ppo_trainer import PPOTrainer
from onnx_exporter import export_to_onnx, export_from_checkpoint


# ──────────────────────────────────────────────────────────────────────────────
#  Argument parsing
# ──────────────────────────────────────────────────────────────────────────────

def parse_args() -> argparse.Namespace:
    """Parse command-line arguments."""
    parser = argparse.ArgumentParser(
        description="Neural Animation PPO Training — Train animation policies for Unity Sentis",
        formatter_class=argparse.ArgumentDefaultsHelpFormatter,
    )

    # Avatar type
    parser.add_argument(
        "--avatar_type", "-a",
        type=str,
        default="biped",
        choices=["biped", "humanoid", "quadruped", "quad"],
        help="Avatar type to train for (determines obs/act dimensions)",
    )

    # Training parameters
    parser.add_argument(
        "--epochs", "-e",
        type=int,
        default=None,
        help="Total training epochs (overrides config default)",
    )
    parser.add_argument(
        "--steps_per_epoch",
        type=int,
        default=None,
        help="Environment steps per epoch (overrides config default)",
    )
    parser.add_argument(
        "--batch_size", "-b",
        type=int,
        default=None,
        help="PPO minibatch size (overrides config default)",
    )
    parser.add_argument(
        "--n_steps",
        type=int,
        default=None,
        help="Steps per rollout before PPO update (overrides config default)",
    )
    parser.add_argument(
        "--lr",
        type=float,
        default=None,
        help="Learning rate (overrides config default)",
    )

    # Device
    parser.add_argument(
        "--device", "-d",
        type=str,
        default="auto",
        choices=["auto", "cpu", "cuda"],
        help="Device to use for training",
    )

    # Experiment name
    parser.add_argument(
        "--experiment_name", "-n",
        type=str,
        default=None,
        help="Experiment name for logging and checkpoints",
    )

    # Resume from checkpoint
    parser.add_argument(
        "--resume", "-r",
        type=str,
        default=None,
        help="Resume training from a checkpoint file (.pt)",
    )

    # Export only (no training)
    parser.add_argument(
        "--export-only",
        type=str,
        default=None,
        metavar="CHECKPOINT_PATH",
        help="Skip training, just export a checkpoint to ONNX",
    )

    # ONNX output path
    parser.add_argument(
        "--onnx_output",
        type=str,
        default=None,
        help="Output path for the exported ONNX model",
    )

    # Seed
    parser.add_argument(
        "--seed",
        type=int,
        default=None,
        help="Random seed for reproducibility",
    )

    # Verbose
    parser.add_argument(
        "--verbose", "-v",
        action="store_true",
        help="Enable verbose output",
    )

    return parser.parse_args()


# ──────────────────────────────────────────────────────────────────────────────
#  Main
# ──────────────────────────────────────────────────────────────────────────────

def main():
    """Main entry point."""
    args = parse_args()

    # Setup logging
    log_level = logging.DEBUG if args.verbose else logging.INFO
    logging.basicConfig(
        level=log_level,
        format="[%(asctime)s] %(levelname)s: %(message)s",
        datefmt="%H:%M:%S",
    )
    logger = logging.getLogger(__name__)

    print()
    print("=" * 60)
    print("  Neural Animation System — PPO Training Pipeline")
    print("  Unity Sentis compatible ONNX export")
    print("=" * 60)

    # ── Configuration ────────────────────────────────────────────────────────

    cfg = Config()
    cfg.avatar = args.avatar_type

    # Override from command line
    if args.epochs is not None:
        cfg.training.total_epochs = args.epochs
    if args.steps_per_epoch is not None:
        cfg.training.steps_per_epoch = args.steps_per_epoch
    if args.batch_size is not None:
        cfg.ppo.batch_size = args.batch_size
    if args.n_steps is not None:
        cfg.ppo.n_steps = args.n_steps
    if args.lr is not None:
        cfg.ppo.actor_lr = args.lr
        cfg.ppo.critic_lr = args.lr
    if args.device != "auto":
        cfg.device = args.device
    if args.experiment_name is not None:
        cfg.training.experiment_name = args.experiment_name
    if args.seed is not None:
        cfg.training.seed = args.seed

    # Resolve device
    cfg.__post_init__()

    # Print configuration
    cfg.print_summary()

    # Set random seeds
    import random
    import numpy as np
    import torch

    seed = cfg.training.seed
    random.seed(seed)
    np.random.seed(seed)
    torch.manual_seed(seed)
    if torch.cuda.is_available():
        torch.cuda.manual_seed_all(seed)
    logger.info(f"Random seed set to {seed}")

    # ── Export-only mode ─────────────────────────────────────────────────────

    if args.export_only:
        logger.info(f"Export-only mode: exporting {args.export_only} to ONNX")
        output_path = args.onnx_output or os.path.join(
            cfg.training.checkpoint_dir,
            cfg.training.experiment_name,
            f"{cfg.avatar}_policy.onnx",
        )
        onnx_path = export_from_checkpoint(
            args.export_only, cfg, output_path=output_path, verbose=True
        )
        print(f"\n  ✓ ONNX model exported to: {onnx_path}")
        print(f"  ✓ Ready for Unity Sentis: input='{cfg.onnx.input_name}', output='{cfg.onnx.output_name}'")
        return

    # ── Create environment ───────────────────────────────────────────────────

    logger.info(f"Creating environment for avatar type: {cfg.avatar}")
    env = create_env(cfg)
    logger.info(
        f"  Observation space: {env.observation_space.shape}  "
        f"Action space: {env.action_space.shape}"
    )

    # ── Create trainer ───────────────────────────────────────────────────────

    trainer = PPOTrainer(cfg)

    # Resume from checkpoint if specified
    if args.resume:
        if os.path.isfile(args.resume):
            logger.info(f"Resuming from checkpoint: {args.resume}")
            trainer.load_checkpoint(args.resume)
        else:
            logger.warning(f"Checkpoint not found: {args.resume}. Starting fresh.")

    # ── Training loop ────────────────────────────────────────────────────────

    def on_eval(reward: float, epoch: int):
        """Callback after each evaluation — export to ONNX when new best is found."""
        if reward >= trainer.best_reward:
            onnx_path = os.path.join(
                cfg.training.checkpoint_dir,
                cfg.training.experiment_name,
                f"{cfg.avatar}_policy_best.onnx",
            )
            try:
                export_to_onnx(trainer.model, cfg, onnx_path, verbose=False)
                logger.info(f"Exported best model to ONNX: {onnx_path}")
            except Exception as e:
                logger.warning(f"ONNX export failed during training: {e}")

    # Run training
    print()
    trainer.train(env, on_eval_callback=on_eval)

    # ── Final ONNX export ────────────────────────────────────────────────────

    print("\n" + "=" * 60)
    print("  Exporting final model to ONNX...")
    print("=" * 60)

    onnx_path = os.path.join(
        cfg.training.checkpoint_dir,
        cfg.training.experiment_name,
        f"{cfg.avatar}_policy.onnx",
    )

    try:
        final_path = export_to_onnx(
            trainer.model, cfg, onnx_path, verbose=True
        )
        print(f"\n  ✓ Final ONNX model: {final_path}")
        print(f"  ✓ Input name:  '{cfg.onnx.input_name}'")
        print(f"  ✓ Output name: '{cfg.onnx.output_name}'")
        print(f"  ✓ Input shape:  [1, 1, 1, {cfg.obs_dim}]")
        print(f"  ✓ Output shape: [1, {cfg.act_dim}]")
        print(f"\n  Ready to use in Unity with ONNXPolicy.cs!")
    except Exception as e:
        logger.error(f"Final ONNX export failed: {e}")
        print(f"\n  ✗ ONNX export failed: {e}")
        print(f"  The PyTorch checkpoint is still available for manual export.")

    print("\n" + "=" * 60)
    print("  Training complete!")
    print(f"  Best reward: {trainer.best_reward:.2f}")
    print(f"  Checkpoints: {os.path.join(cfg.training.checkpoint_dir, cfg.training.experiment_name)}")
    print("=" * 60)
    print()


if __name__ == "__main__":
    main()