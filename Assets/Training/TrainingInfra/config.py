"""
Configuration dataclass for PPO training of Neural Animation policies.

All hyperparameters are centralized here for easy experimentation.
"""

from dataclasses import dataclass, field, asdict
from typing import Tuple, List, Optional


# ──────────────────────────────────────────────────────────────────────────────
# Avatar-specific specifications (matching Unity C# PolicyMetadata)
# ──────────────────────────────────────────────────────────────────────────────

@dataclass
class AvatarSpec:
    """Observation/action dimensions for a given avatar type."""
    observation_size: int
    action_size: int
    joint_count: int
    name: str

    @classmethod
    def biped(cls) -> "AvatarSpec":
        """Biped (Humanoid) spec — matches PolicyMetadata.CreateLocomotionBipedBase."""
        return cls(
            observation_size=120,
            action_size=80,
            joint_count=18,
            name="biped",
        )

    @classmethod
    def quadruped(cls) -> "AvatarSpec":
        """Quadruped spec — matches PolicyMetadata.CreateLocomotionQuadrupedBase."""
        return cls(
            observation_size=150,
            action_size=100,
            joint_count=24,
            name="quadruped",
        )

    @classmethod
    def fly(cls) -> "AvatarSpec":
        """Flying creature spec (e.g., dragon, bird)."""
        return cls(
            observation_size=150,
            action_size=100,
            joint_count=24,
            name="fly",
        )

    @classmethod
    def swim(cls) -> "AvatarSpec":
        """Swimming creature spec (e.g., fish, aquatic)."""
        return cls(
            observation_size=150,
            action_size=100,
            joint_count=24,
            name="swim",
        )

    @classmethod
    def from_name(cls, name: str) -> "AvatarSpec":
        """Dispatch by avatar type name string."""
        name = name.strip().lower()
        if name in ("biped", "humanoid"):
            return cls.biped()
        elif name in ("quadruped", "quad", "fly", "swim"):
            return cls.quadruped()
        else:
            raise ValueError(f"Unknown avatar type: {name}. Choose 'biped' or 'quadruped' (or 'fly', 'swim').")


# ──────────────────────────────────────────────────────────────────────────────
# Network architecture
# ──────────────────────────────────────────────────────────────────────────────

@dataclass
class NetworkConfig:
    """Actor-Critic network architecture settings."""
    hidden_sizes: Tuple[int, ...] = (256, 128, 64)
    activation: str = "tanh"  # "tanh" | "relu" | "elu"
    use_layer_norm: bool = False
    shared_backbone: bool = True
    # If shared_backbone is True, actor and critic share the trunk
    # and only have separate output heads.


# ──────────────────────────────────────────────────────────────────────────────
# PPO hyperparameters
# ──────────────────────────────────────────────────────────────────────────────

@dataclass
class PPOConfig:
    """Proximal Policy Optimization hyperparameters."""
    # Learning rates
    actor_lr: float = 3e-4
    critic_lr: float = 3e-4
    lr_schedule: str = "linear"  # "constant" | "linear" | "exponential"
    lr_final: float = 1e-5      # final learning rate (for linear/exponential decay)

    # PPO clipping & regularisation
    clip_epsilon: float = 0.2
    entropy_coef: float = 0.01
    value_loss_coef: float = 0.5
    max_grad_norm: float = 0.5
    target_kl: Optional[float] = 0.02  # early stopping if KL divergence exceeds this

    # GAE (Generalized Advantage Estimation)
    gamma: float = 0.99
    gae_lambda: float = 0.95

    # Training
    n_steps: int = 2048          # steps per rollout before update
    batch_size: int = 64
    mini_epochs: int = 10        # number of epochs per update
    normalize_advantages: bool = True
    clip_value_loss: bool = True

    # Annealing
    anneal_entropy: bool = True
    entropy_final: float = 0.001


# ──────────────────────────────────────────────────────────────────────────────
# Training loop settings
# ──────────────────────────────────────────────────────────────────────────────

@dataclass
class TrainingConfig:
    """High-level training loop configuration."""
    total_epochs: int = 100
    steps_per_epoch: int = 50_000   # total environment steps per epoch
    eval_interval: int = 10         # evaluate every N epochs
    eval_episodes: int = 5
    save_interval: int = 25         # save checkpoint every N epochs
    log_interval: int = 1           # log to TensorBoard every N epochs
    checkpoint_dir: str = "models"
    experiment_name: str = "neural_animation_ppo"
    seed: int = 42


# ──────────────────────────────────────────────────────────────────────────────
# ONNX export settings
# ──────────────────────────────────────────────────────────────────────────────

@dataclass
class ONNXConfig:
    """Settings for exporting trained model to ONNX (Unity Sentis compatible)."""
    input_name: str = "observation"
    output_name: str = "action"
    opset_version: int = 17
    dynamic_batch: bool = True
    export_fp16: bool = False
    input_shape_nhwc: Tuple[int, ...] = (1, 1, 1, -1)  # -1 will be filled at export


# ──────────────────────────────────────────────────────────────────────────────
# Environment settings
# ──────────────────────────────────────────────────────────────────────────────

@dataclass
class EnvConfig:
    """Simulated animation environment settings."""
    dt: float = 0.02  # 50 Hz simulation step
    physics_steps: int = 2
    reward_velocity_weight: float = 1.0
    reward_energy_weight: float = 0.1
    reward_smoothness_weight: float = 0.05
    reward_pose_weight: float = 0.3
    reward_contact_weight: float = 0.1
    max_episode_length: int = 1000
    observation_noise: float = 0.01
    action_noise: float = 0.0
    target_velocity_range: Tuple[float, float] = (0.5, 5.0)  # m/s


# ──────────────────────────────────────────────────────────────────────────────
# Master configuration
# ──────────────────────────────────────────────────────────────────────────────

@dataclass
class Config:
    """Master configuration combining all sub-configs."""
    avatar: str = "biped"
    network: NetworkConfig = field(default_factory=NetworkConfig)
    ppo: PPOConfig = field(default_factory=PPOConfig)
    training: TrainingConfig = field(default_factory=TrainingConfig)
    onnx: ONNXConfig = field(default_factory=ONNXConfig)
    env: EnvConfig = field(default_factory=EnvConfig)
    device: str = "auto"  # "auto" | "cpu" | "cuda"

    def __post_init__(self):
        """Resolve derived fields."""
        if self.device == "auto":
            import torch
            self.device = "cuda" if torch.cuda.is_available() else "cpu"

    @property
    def avatar_spec(self) -> AvatarSpec:
        return AvatarSpec.from_name(self.avatar)

    @property
    def obs_dim(self) -> int:
        return self.avatar_spec.observation_size

    @property
    def act_dim(self) -> int:
        return self.avatar_spec.action_size

    @property
    def joint_count(self) -> int:
        return self.avatar_spec.joint_count

    def to_dict(self) -> dict:
        """Convert entire config to a flat dictionary for logging."""
        d = {}
        d["avatar"] = self.avatar
        d["obs_dim"] = self.obs_dim
        d["act_dim"] = self.act_dim
        d["joint_count"] = self.joint_count
        d["device"] = self.device
        d.update(asdict(self.network))
        d.update(asdict(self.ppo))
        d.update(asdict(self.training))
        d.update(asdict(self.onnx))
        d.update(asdict(self.env))
        return d

    def print_summary(self):
        """Print a human-readable summary of the configuration."""
        import json
        print("=" * 60)
        print("Neural Animation PPO Training Configuration")
        print("=" * 60)
        print(f"  Avatar type:       {self.avatar}")
        print(f"  Observation dim:   {self.obs_dim}")
        print(f"  Action dim:        {self.act_dim}")
        print(f"  Joint count:       {self.joint_count}")
        print(f"  Device:            {self.device}")
        print(f"  Network:           {self.network.hidden_sizes} ({self.network.activation})")
        print(f"  Shared backbone:   {self.network.shared_backbone}")
        print(f"  Total epochs:      {self.training.total_epochs}")
        print(f"  Steps per epoch:   {self.training.steps_per_epoch}")
        print(f"  Batch size:        {self.ppo.batch_size}")
        print(f"  PPO clip ε:        {self.ppo.clip_epsilon}")
        print(f"  Entropy coef:      {self.ppo.entropy_coef}")
        print(f"  γ / λ:             {self.ppo.gamma} / {self.ppo.gae_lambda}")
        print(f"  ONNX input:        '{self.onnx.input_name}' → output: '{self.onnx.output_name}'")
        print("=" * 60)