#!/usr/bin/env python3
"""
Train all remaining models to target epochs (Phase 68 CPU Animation Quality Maximization).

Biped 10 models: 500 -> 1000 epochs (500 more each)
Quadruped 10 models: 500 -> 1000 epochs (500 more each for locomotion/combat/react/interact, 500 for fly/swim/mount/large_monster/run/crouch)

Total: 20 models * ~500 epochs * ~30-40 min = ~15-20 hours CPU
"""

import os
import sys
import time
import subprocess
import json
from datetime import datetime
from pathlib import Path

PROJECT_PATH = "/mnt/c/Unity/code"
TRAINING_INFRA = os.path.join(PROJECT_PATH, "Assets/Training/TrainingInfra")
CHECKPOINT_DIR = os.path.join(TRAINING_INFRA, "checkpoints")
OUTPUT_DIR = os.path.join(PROJECT_PATH, "Assets/Resources/NeuralModels")

# Model training plan: (avatar_type, policy_type, target_epochs, current_epoch_estimate)
# Biped models need 1000 epochs (currently ~500)
# Quadruped locomotion/combat/react/interact need 1000 (currently ~500/25)
# Quadruped fly/swim/mount/large_monster/run/crouch need 500 (currently ~25/50/75)

MODELS_TO_TRAIN = [
    # Biped: 1000 epochs target
    ("biped", "locomotion", 1000, 500),
    ("biped", "combat", 1000, 500),
    ("biped", "react", 1000, 500),
    ("biped", "interact", 1000, 500),
    ("biped", "fly", 500, 500),  # Only 500 target
    ("biped", "swim", 500, 500),
    ("biped", "mount", 500, 500),
    ("biped", "climb", 500, 500),
    ("biped", "run", 500, 500),
    ("biped", "crouch", 500, 500),
    
    # Quadruped: 1000 for locomotion/combat/react/interact, 500 for others
    ("quadruped", "locomotion", 1000, 475),
    ("quadruped", "combat", 1000, 475),
    ("quadruped", "react", 1000, 25),
    ("quadruped", "interact", 1000, 25),
    ("quadruped", "fly", 500, 75),
    ("quadruped", "swim", 500, 75),
    ("quadruped", "mount", 500, 75),
    ("quadruped", "large_monster", 500, 75),
    ("quadruped", "run", 500, 75),
    ("quadruped", "crouch", 500, 75),
]

def send_telegram(message: str):
    """Send notification via telegram using send_message tool."""
    try:
        # Use the hermes send_message tool if available
        import requests
        # We'll output a marker that the cron system can pick up
        print(f"TELEGRAM_NOTIFICATION:{message}")
        sys.stdout.flush()
    except Exception as e:
        print(f"Failed to send telegram: {e}")

def find_latest_checkpoint(avatar_type: str, policy_type: str) -> str | None:
    """Find the latest checkpoint for a model."""
    prefix = f"{avatar_type}_{policy_type}_policy"
    checkpoints = list(Path(CHECKPOINT_DIR).glob(f"{prefix}_epoch*.pt"))
    if not checkpoints:
        return None
    # Sort by epoch number
    checkpoints.sort(key=lambda p: int(p.stem.split("epoch")[-1].split(".")[0]))
    return str(checkpoints[-1])

def train_model(avatar_type: str, policy_type: str, target_epochs: int, current_epoch: int, curriculum: bool = True) -> bool:
    """Train a single model from current_epoch to target_epochs."""
    remaining = target_epochs - current_epoch
    if remaining <= 0:
        print(f"  [{avatar_type}/{policy_type}] Already at target ({current_epoch}/{target_epochs}), skipping")
        return True
    
    print(f"\n{'='*60}")
    print(f"  Training: {avatar_type}_{policy_type}")
    print(f"  Current: {current_epoch} -> Target: {target_epochs} ({remaining} epochs)")
    print(f"  Curriculum: {curriculum}")
    print(f"{'='*60}")
    
    # Find latest checkpoint to resume from
    checkpoint_path = find_latest_checkpoint(avatar_type, policy_type)
    
    cmd = [
        sys.executable, "train_torch.py",
        "--avatar_type", avatar_type,
        "--policy_type", policy_type,
        "--epochs", str(target_epochs),
        "--curriculum" if curriculum else "",
        "--onnx_export",
        "--checkpoint_interval", "25",
        "--verbose",
    ]
    
    # Add checkpoint if resuming
    if checkpoint_path:
        cmd.extend(["--checkpoint", checkpoint_path])
        print(f"  Resuming from: {checkpoint_path}")
    
    # Filter empty strings
    cmd = [c for c in cmd if c]
    
    print(f"  Command: {' '.join(cmd)}")
    
    start_time = time.time()
    try:
        result = subprocess.run(
            cmd,
            cwd=TRAINING_INFRA,
            capture_output=True,
            text=True,
            timeout=remaining * 60 * 60,  # 1 hour per epoch max
        )
        elapsed = time.time() - start_time
        
        if result.returncode == 0:
            print(f"  ✓ Completed in {elapsed/60:.1f} minutes")
            # Verify ONNX was exported
            onnx_name = f"{policy_type}_{avatar_type}_base.onnx"
            onnx_path = os.path.join(OUTPUT_DIR, onnx_name)
            if os.path.exists(onnx_path):
                size_mb = os.path.getsize(onnx_path) / (1024 * 1024)
                print(f"  ✓ ONNX exported: {onnx_name} ({size_mb:.1f} MB)")
            return True
        else:
            print(f"  ✗ FAILED (exit code {result.returncode})")
            print(f"  stdout: {result.stdout[-2000:]}")
            print(f"  stderr: {result.stderr[-2000:]}")
            return False
            
    except subprocess.TimeoutExpired:
        elapsed = time.time() - start_time
        print(f"  ✗ TIMEOUT after {elapsed/60:.1f} minutes")
        return False
    except Exception as e:
        elapsed = time.time() - start_time if 'start_time' in locals() else 0
        print(f"  ✗ ERROR: {e}")
        return False

def main():
    print("=" * 60)
    print("  Phase 68: CPU Animation Quality Maximization")
    print("  Training all 20 models to target epochs")
    print("=" * 60)
    
    start_total = time.time()
    results = []
    
    for i, (avatar_type, policy_type, target_epochs, current_epoch) in enumerate(MODELS_TO_TRAIN):
        # Use curriculum for locomotion/combat/react/interact
        curriculum = policy_type in ("locomotion", "combat", "react", "interact")
        
        success = train_model(avatar_type, policy_type, target_epochs, current_epoch, curriculum)
        results.append({
            "avatar_type": avatar_type,
            "policy_type": policy_type,
            "target_epochs": target_epochs,
            "current_epoch": current_epoch,
            "success": success,
        })
        
        # Progress update
        elapsed = time.time() - start_total
        completed = i + 1
        remaining_models = len(MODELS_TO_TRAIN) - completed
        avg_time = elapsed / completed if completed > 0 else 0
        eta = avg_time * remaining_models
        
        print(f"\n  Progress: {completed}/{len(MODELS_TO_TRAIN)} models")
        print(f"  Elapsed: {elapsed/3600:.1f}h, ETA: {eta/3600:.1f}h")
    
    # Summary
    total_time = time.time() - start_total
    successful = sum(1 for r in results if r["success"])
    
    print("\n" + "=" * 60)
    print("  TRAINING COMPLETE")
    print("=" * 60)
    print(f"  Total time: {total_time/3600:.1f} hours")
    print(f"  Successful: {successful}/{len(MODELS_TO_TRAIN)}")
    print(f"  Failed: {len(MODELS_TO_TRAIN) - successful}")
    
    for r in results:
        status = "✓" if r["success"] else "✗"
        print(f"  {status} {r['avatar_type']}_{r['policy_type']}: {r['current_epoch']} -> {r['target_epochs']}")
    
    # Send Telegram notification
    msg = f"""🎯 Phase 68 CPU Animation Training Complete
    
✅ {successful}/{len(MODELS_TO_TRAIN)} models trained
⏱️ Total time: {total_time/3600:.1f} hours
📁 ONNX models exported to Assets/Resources/NeuralModels/

Models:
"""
    for r in results:
        status = "✅" if r["success"] else "❌"
        msg += f"{status} {r['avatar_type']}_{r['policy_type']} ({r['current_epoch']}→{r['target_epochs']} epochs)\n"
    
    send_telegram(msg)
    
    # Save results
    results_path = os.path.join(CHECKPOINT_DIR, f"training_results_{datetime.now().strftime('%Y%m%d_%H%M%S')}.json")
    with open(results_path, 'w') as f:
        json.dump({
            "timestamp": datetime.now().isoformat(),
            "total_time_hours": total_time / 3600,
            "successful": successful,
            "total": len(MODELS_TO_TRAIN),
            "results": results
        }, f, indent=2)
    
    print(f"\n  Results saved: {results_path}")
    
    return successful == len(MODELS_TO_TRAIN)

if __name__ == "__main__":
    success = main()
    sys.exit(0 if success else 1)