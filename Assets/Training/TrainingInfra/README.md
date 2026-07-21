# Neural Animation PPO Training Infrastructure

Professional PPO (Proximal Policy Optimization) training pipeline for the Neural Animation System in Unity's **Poison** project.

Trains animation policies (actor-critic neural networks) and exports them to ONNX format compatible with **Unity Sentis / InferenceEngine**.

---

## Architecture

```
Assets/Training/TrainingInfra/
├── config.py              # Configuration dataclass (all hyperparameters)
├── ppo_trainer.py         # Full PPO implementation (Actor-Critic, GAE, Rollout Buffer)
├── simple_animation_env.py# Simulated animation environment (physics proxy, reward shaping)
├── onnx_exporter.py       # ONNX export to Unity Sentis-compatible format
├── train.py               # Main entry point script
├── requirements.txt       # Python dependencies
├── README.md              # This file
└── models/                # Saved checkpoints and exported ONNX models
    └── neural_animation_ppo/
        ├── logs/          # TensorBoard logs
        ├── config.json    # Training configuration
        ├── best_model.pt  # Best model checkpoint
        ├── checkpoint_epoch_*.pt  # Periodic checkpoints
        └── biped_policy.onnx     # Final exported ONNX model
```

## Quick Start

### 1. Install dependencies

```bash
cd Assets/Training/TrainingInfra
pip install -r requirements.txt
```

### 2. Train a biped locomotion policy

```bash
python train.py --avatar_type biped --epochs 100 --batch_size 64
```

### 3. Train a quadruped policy

```bash
python train.py --avatar_type quadruped --epochs 200 --device cuda
```

### 4. Export an existing checkpoint to ONNX (no training)

```bash
python train.py --avatar_type biped --export-only models/neural_animation_ppo/best_model.pt
```

### 5. Resume training from a checkpoint

```bash
python train.py --avatar_type biped --resume models/neural_animation_ppo/checkpoint_epoch_0050.pt
```

---

## Command-Line Arguments

| Argument | Short | Default | Description |
|----------|-------|---------|-------------|
| `--avatar_type` | `-a` | `biped` | Avatar type: `biped`, `humanoid`, `quadruped`, `quad` |
| `--epochs` | `-e` | `100` | Total training epochs |
| `--steps_per_epoch` | | `50000` | Environment steps per epoch |
| `--batch_size` | `-b` | `64` | PPO minibatch size |
| `--n_steps` | | `2048` | Steps per rollout before PPO update |
| `--lr` | | `3e-4` | Learning rate |
| `--device` | `-d` | `auto` | Device: `auto`, `cpu`, `cuda` |
| `--experiment_name` | `-n` | `neural_animation_ppo` | Experiment name for logging |
| `--resume` | `-r` | `None` | Resume from checkpoint file |
| `--export-only` | | `None` | Export checkpoint to ONNX (skip training) |
| `--onnx_output` | | `None` | Custom ONNX output path |
| `--seed` | | `42` | Random seed |
| `--verbose` | `-v` | `False` | Verbose output |

---

## Configuration

All hyperparameters are centralized in `config.py` as dataclasses:

- **NetworkConfig**: Hidden layer sizes, activation function, shared backbone
- **PPOConfig**: Learning rates, clip epsilon, entropy coefficient, GAE lambda, mini-epochs
- **TrainingConfig**: Epochs, steps per epoch, evaluation/save intervals
- **ONNXConfig**: Input/output tensor names, opset version, dynamic batch
- **EnvConfig**: Reward weights, physics timestep, noise levels

### Custom configuration

Modify `config.py` directly or create a wrapper script:

```python
from config import Config

cfg = Config()
cfg.avatar = "quadruped"
cfg.network.hidden_sizes = (512, 256, 128)
cfg.ppo.clip_epsilon = 0.25
cfg.ppo.entropy_coef = 0.005
cfg.training.total_epochs = 200
```

---

## Avatar Specifications

| Avatar | Observation Size | Action Size | Joint Count |
|--------|-----------------|-------------|-------------|
| Biped (Humanoid) | 120 | 80 | 18 |
| Quadruped | 150 | 100 | 24 |

These match the `PolicyMetadata` in `AnimationPolicy.cs`:

- `PolicyMetadata.CreateLocomotionBipedBase()` → ObservationSize=120, ActionSize=80
- `PolicyMetadata.CreateLocomotionQuadrupedBase()` → ObservationSize=150, ActionSize=100

---

## ONNX Export — Unity Sentis Compatibility

The exported ONNX model is fully compatible with **Unity Sentis / InferenceEngine**:

| Property | Value | Match |
|----------|-------|-------|
| Input tensor name | `"observation"` | `ONNXPolicy` default `inputName` |
| Output tensor name | `"action"` | `ONNXPolicy` default `outputName` |
| Input shape | `[1, 1, 1, N]` | `TensorShape(1, 1, 1, obsSize)` |
| Output shape | `[1, M]` | Flat array of `actionSize` |
| Precision | FP32 | `Tensor<float>` |
| Opset version | 17 | Broad Unity Sentis support |

### Verification

The exporter validates the ONNX model using:
1. `onnx.checker` — structural validation
2. `onnxruntime` — runtime inference test with correct I/O shapes

### Using in Unity

```csharp
var metadata = PolicyMetadata.CreateLocomotionBipedBase("path/to/biped_policy.onnx");
var policy = new ONNXPolicy(metadata);

float[] observation = encoder.Encode();
float[] action = new float[metadata.ActionSize];

policy.Infer(observation, action);
// action now contains the neural network output
```

---

## Reward Components

The simulated environment computes rewards from:

1. **Velocity tracking** — Gaussian reward centered on target velocity
2. **Energy efficiency** — Penalizes large joint movements
3. **Smoothness** — Penalizes jerky action changes
4. **Ground contact** — Rewards alternating foot contact
5. **Pose consistency** — Keeps character near default pose

Reward weights are configurable in `EnvConfig`.

---

## Extending

### Adding a new avatar type

1. Add dimensions in `AvatarSpec` (config.py)
2. Update the observation encoder in `simple_animation_env.py`
3. Add the corresponding `PolicyMetadata` factory in `AnimationPolicy.cs`

### Using a real physics engine

Replace `SimpleAnimationEnv` with a Gymnasium-compatible MuJoCo, Isaac Gym, or custom Unity environment wrapper. The PPO trainer and exporter work with any environment following the standard API.

---

## File Overview

| File | Purpose |
|------|---------|
| `config.py` | Centralized configuration with avatar specs, network, PPO, and training settings |
| `ppo_trainer.py` | Full PPO implementation: Actor-Critic, GAE, RolloutBuffer, checkpointing, TensorBoard |
| `simple_animation_env.py` | Simulated environment with kinematic chain physics proxy and reward shaping |
| `onnx_exporter.py` | Exports PyTorch models to Unity Sentis-compatible ONNX with validation |
| `train.py` | Main entry point with CLI argument parsing, training loop, and ONNX export |