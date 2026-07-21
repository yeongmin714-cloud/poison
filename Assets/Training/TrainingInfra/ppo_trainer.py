"""
Professional PPO (Proximal Policy Optimization) trainer for Neural Animation policies.

Implements:
- Full PPO algorithm with clipped surrogate objective
- Actor-Critic architecture (shared or separate backbone)
- Support for both biped (obs=120, act=80) and quadruped (obs=150, act=100)
- GAE (Generalized Advantage Estimation)
- Experience replay buffer
- Model checkpointing and resumption
- TensorBoard logging
"""

import os
import time
import json
from typing import Optional, Tuple, List, Dict, Any
from dataclasses import asdict
from pathlib import Path

import numpy as np
import torch
import torch.nn as nn
import torch.nn.functional as F
from torch.distributions import Normal, Independent
from torch.utils.tensorboard import SummaryWriter

from config import Config, NetworkConfig, PPOConfig, TrainingConfig


# ──────────────────────────────────────────────────────────────────────────────
#  Activation factory
# ──────────────────────────────────────────────────────────────────────────────

def _get_activation(name: str) -> nn.Module:
    """Return the activation module by name."""
    name = name.lower().strip()
    if name == "tanh":
        return nn.Tanh()
    elif name == "relu":
        return nn.ReLU()
    elif name == "elu":
        return nn.ELU()
    else:
        raise ValueError(f"Unsupported activation: {name}. Use 'tanh', 'relu', or 'elu'.")


# ──────────────────────────────────────────────────────────────────────────────
#  Actor network
# ──────────────────────────────────────────────────────────────────────────────

class Actor(nn.Module):
    """Gaussian policy network — outputs mean and log_std of action distribution."""

    def __init__(
        self,
        obs_dim: int,
        act_dim: int,
        hidden_sizes: Tuple[int, ...] = (256, 128, 64),
        activation: str = "tanh",
        log_std_init: float = -0.5,
    ):
        super().__init__()
        self.obs_dim = obs_dim
        self.act_dim = act_dim

        # Build MLP trunk
        layers = []
        prev = obs_dim
        for h in hidden_sizes:
            layers.append(nn.Linear(prev, h))
            layers.append(_get_activation(activation))
            prev = h
        self.trunk = nn.Sequential(*layers)

        # Output heads
        self.mean_head = nn.Linear(prev, act_dim)
        self.log_std = nn.Parameter(torch.full((act_dim,), log_std_init))

        # Small weight initialization
        self._init_weights()

    def _init_weights(self):
        for m in self.modules():
            if isinstance(m, nn.Linear):
                nn.init.orthogonal_(m.weight, gain=np.sqrt(2))
                nn.init.constant_(m.bias, 0.0)
        # Last layer for mean: smaller gain
        nn.init.orthogonal_(self.mean_head.weight, gain=0.01)
        nn.init.constant_(self.mean_head.bias, 0.0)

    def forward(self, obs: torch.Tensor) -> Normal:
        """Return a Normal distribution over actions."""
        features = self.trunk(obs)
        mean = self.mean_head(features)
        # Clamp log_std to prevent extreme values
        log_std = torch.clamp(self.log_std, min=-5.0, max=2.0)
        std = torch.exp(log_std)
        # Independent normal distribution (diagonal covariance)
        return Independent(Normal(mean, std), reinterpreted_batch_ndims=1)

    def get_action(self, obs: torch.Tensor, deterministic: bool = False) -> torch.Tensor:
        """Sample an action from the policy (or take mean when deterministic)."""
        dist = self.forward(obs)
        if deterministic:
            return dist.mean
        return dist.sample()

    def evaluate(self, obs: torch.Tensor, actions: torch.Tensor) -> Tuple[torch.Tensor, torch.Tensor, torch.Tensor]:
        """
        Evaluate log-probability, entropy, and KL divergence for a batch.
        Returns: (log_probs, entropy, approx_kl)
        """
        dist = self.forward(obs)
        log_probs = dist.log_prob(actions)
        entropy = dist.entropy().mean()
        return log_probs, entropy, dist


# ──────────────────────────────────────────────────────────────────────────────
#  Critic network
# ──────────────────────────────────────────────────────────────────────────────

class Critic(nn.Module):
    """Value function network — estimates state value V(s)."""

    def __init__(
        self,
        obs_dim: int,
        hidden_sizes: Tuple[int, ...] = (256, 128, 64),
        activation: str = "tanh",
    ):
        super().__init__()
        layers = []
        prev = obs_dim
        for h in hidden_sizes:
            layers.append(nn.Linear(prev, h))
            layers.append(_get_activation(activation))
            prev = h
        layers.append(nn.Linear(prev, 1))
        self.net = nn.Sequential(*layers)
        self._init_weights()

    def _init_weights(self):
        for m in self.modules():
            if isinstance(m, nn.Linear):
                nn.init.orthogonal_(m.weight, gain=np.sqrt(2))
                nn.init.constant_(m.bias, 0.0)
        # Last layer: smaller gain
        last = self.net[-1]
        if isinstance(last, nn.Linear):
            nn.init.orthogonal_(last.weight, gain=1.0)

    def forward(self, obs: torch.Tensor) -> torch.Tensor:
        """Return scalar value V(s)."""
        return self.net(obs).squeeze(-1)


# ──────────────────────────────────────────────────────────────────────────────
#  Actor-Critic (shared backbone or separate)
# ──────────────────────────────────────────────────────────────────────────────

class ActorCritic(nn.Module):
    """
    Combined Actor-Critic model.

    If shared_backbone is True, the trunk is shared and only the output
    heads (actor mean, critic value) are separate.
    If False, separate networks are used.
    """

    def __init__(self, cfg: NetworkConfig, obs_dim: int, act_dim: int):
        super().__init__()
        self.cfg = cfg
        self.obs_dim = obs_dim
        self.act_dim = act_dim
        self.shared_backbone = cfg.shared_backbone
        self.activation = cfg.activation
        self.hidden_sizes = cfg.hidden_sizes

        if cfg.shared_backbone:
            # Shared trunk
            layers = []
            prev = obs_dim
            for h in cfg.hidden_sizes:
                layers.append(nn.Linear(prev, h))
                layers.append(_get_activation(cfg.activation))
                prev = h
            self.trunk = nn.Sequential(*layers)

            self.actor_mean = nn.Linear(prev, act_dim)
            self.log_std = nn.Parameter(torch.full((act_dim,), -0.5))
            self.critic_head = nn.Linear(prev, 1)
            self._init_weights()
        else:
            self.actor = Actor(obs_dim, act_dim, cfg.hidden_sizes, cfg.activation)
            self.critic = Critic(obs_dim, cfg.hidden_sizes, cfg.activation)

    def _init_weights(self):
        for m in self.modules():
            if isinstance(m, nn.Linear):
                if m is not self.actor_mean and m is not self.critic_head:
                    nn.init.orthogonal_(m.weight, gain=np.sqrt(2))
                    nn.init.constant_(m.bias, 0.0)
        nn.init.orthogonal_(self.actor_mean.weight, gain=0.01)
        nn.init.constant_(self.actor_mean.bias, 0.0)
        nn.init.orthogonal_(self.critic_head.weight, gain=1.0)
        nn.init.constant_(self.critic_head.bias, 0.0)

    def forward(self, obs: torch.Tensor) -> Tuple[torch.Tensor, torch.Tensor, torch.Tensor]:
        """
        Forward pass through both actor and critic.

        Returns: (action, value, log_prob)
        """
        if self.shared_backbone:
            features = self.trunk(obs)
            mean = self.actor_mean(features)
            log_std = torch.clamp(self.log_std, min=-5.0, max=2.0)
            std = torch.exp(log_std)
            dist = Independent(Normal(mean, std), reinterpreted_batch_ndims=1)
            action = dist.sample()
            log_prob = dist.log_prob(action)
            value = self.critic_head(features).squeeze(-1)
        else:
            dist = self.actor.forward(obs)
            action = dist.sample()
            log_prob = dist.log_prob(action)
            value = self.critic.forward(obs)
        return action, value, log_prob

    def get_value(self, obs: torch.Tensor) -> torch.Tensor:
        """Estimate value for a given observation."""
        if self.shared_backbone:
            features = self.trunk(obs)
            return self.critic_head(features).squeeze(-1)
        return self.critic.forward(obs)

    def evaluate_actions(self, obs: torch.Tensor, action: torch.Tensor) -> Tuple[torch.Tensor, torch.Tensor, torch.Tensor]:
        """
        Evaluate log-probability, entropy, and value for given obs/action pairs.
        Returns: (value, log_prob, entropy)
        """
        if self.shared_backbone:
            features = self.trunk(obs)
            mean = self.actor_mean(features)
            log_std = torch.clamp(self.log_std, min=-5.0, max=2.0)
            std = torch.exp(log_std)
            dist = Independent(Normal(mean, std), reinterpreted_batch_ndims=1)
            log_prob = dist.log_prob(action)
            entropy = dist.entropy().mean()
            value = self.critic_head(features).squeeze(-1)
        else:
            log_prob, entropy, dist = self.actor.evaluate(obs, action)
            value = self.critic.forward(obs)
        return value, log_prob, entropy

    def get_action(self, obs: torch.Tensor, deterministic: bool = False) -> torch.Tensor:
        """Sample or deterministically select an action."""
        if self.shared_backbone:
            features = self.trunk(obs)
            mean = self.actor_mean(features)
            if deterministic:
                return mean
            log_std = torch.clamp(self.log_std, min=-5.0, max=2.0)
            std = torch.exp(log_std)
            dist = Independent(Normal(mean, std), reinterpreted_batch_ndims=1)
            return dist.sample()
        return self.actor.get_action(obs, deterministic)


# ──────────────────────────────────────────────────────────────────────────────
#  Rollout Buffer (with GAE)
# ──────────────────────────────────────────────────────────────────────────────

class RolloutBuffer:
    """
    Experience replay buffer for PPO with GAE (Generalized Advantage Estimation).

    Stores transitions from a rollout and computes advantages and returns
    when retrieve() is called.
    """

    def __init__(self, buffer_size: int, obs_dim: int, act_dim: int, gamma: float, gae_lambda: float,
                 device: torch.device):
        self.buffer_size = buffer_size
        self.gamma = gamma
        self.gae_lambda = gae_lambda
        self.device = device
        self.clear()

        # Pre-allocate tensors on device
        self.observations = torch.zeros((buffer_size, obs_dim), dtype=torch.float32, device=device)
        self.actions = torch.zeros((buffer_size, act_dim), dtype=torch.float32, device=device)
        self.log_probs = torch.zeros(buffer_size, dtype=torch.float32, device=device)
        self.rewards = torch.zeros(buffer_size, dtype=torch.float32, device=device)
        self.dones = torch.zeros(buffer_size, dtype=torch.float32, device=device)
        self.values = torch.zeros(buffer_size, dtype=torch.float32, device=device)
        self.advantages = torch.zeros(buffer_size, dtype=torch.float32, device=device)
        self.returns = torch.zeros(buffer_size, dtype=torch.float32, device=device)

    def clear(self):
        """Reset the buffer position counter."""
        self.pos = 0
        self.full = False

    def store(self, obs: np.ndarray, action: np.ndarray, log_prob: float,
              reward: float, done: bool, value: float):
        """Store a single transition."""
        idx = self.pos % self.buffer_size
        self.observations[idx] = torch.from_numpy(obs).to(self.device)
        self.actions[idx] = torch.from_numpy(action).to(self.device)
        self.log_probs[idx] = log_prob
        self.rewards[idx] = reward
        self.dones[idx] = 1.0 if done else 0.0
        self.values[idx] = value
        self.pos += 1
        if self.pos >= self.buffer_size:
            self.full = True

    def compute_advantages(self, last_value: float, last_done: bool):
        """
        Compute GAE advantages after the rollout is complete.

        Args:
            last_value: Value estimate for the final state (bootstrap).
            last_done: Whether the final state was terminal.
        """
        buffer_size = self.buffer_size if self.full else self.pos
        # GAE computation
        gae = 0.0
        with torch.no_grad():
            for t in reversed(range(buffer_size)):
                if t == buffer_size - 1:
                    next_value = last_value if not last_done else 0.0
                    next_non_terminal = 1.0 - (1.0 if last_done else 0.0)
                else:
                    next_value = self.values[t + 1].item()
                    next_non_terminal = 1.0 - self.dones[t + 1].item()

                delta = self.rewards[t].item() + self.gamma * next_value * next_non_terminal - self.values[t].item()
                gae = delta + self.gamma * self.gae_lambda * next_non_terminal * gae
                self.advantages[t] = gae
                self.returns[t] = gae + self.values[t]

    def get_available_count(self) -> int:
        """Return number of transitions currently stored."""
        return self.buffer_size if self.full else self.pos

    def sample_batch(self, batch_size: int) -> Dict[str, torch.Tensor]:
        """
        Randomly sample a batch of transitions from the buffer.

        Returns dict with keys: observations, actions, log_probs, advantages, returns
        """
        buffer_size = self.buffer_size if self.full else self.pos
        indices = torch.randint(0, buffer_size, (batch_size,), device=self.device)
        return {
            "observations": self.observations[indices],
            "actions": self.actions[indices],
            "log_probs": self.log_probs[indices],
            "advantages": self.advantages[indices],
            "returns": self.returns[indices],
        }

    def iterate_minibatches(self, batch_size: int, num_epochs: int, shuffle: bool = True):
        """
        Generator yielding minibatches for multiple epochs of training.

        Yields dicts with keys: observations, actions, log_probs, advantages, returns
        """
        buffer_size = self.buffer_size if self.full else self.pos
        indices = np.arange(buffer_size)

        for _ in range(num_epochs):
            if shuffle:
                np.random.shuffle(indices)
            for start in range(0, buffer_size, batch_size):
                end = start + batch_size
                batch_indices = indices[start:end]
                yield {
                    "observations": self.observations[batch_indices],
                    "actions": self.actions[batch_indices],
                    "log_probs": self.log_probs[batch_indices],
                    "advantages": self.advantages[batch_indices],
                    "returns": self.returns[batch_indices],
                }


# ──────────────────────────────────────────────────────────────────────────────
#  PPO Trainer
# ──────────────────────────────────────────────────────────────────────────────

class PPOTrainer:
    """
    Full PPO trainer for Neural Animation policies.

    Usage:
        trainer = PPOTrainer(config)
        trainer.train(env)
    """

    def __init__(self, cfg: Config):
        self.cfg = cfg
        self.device = torch.device(cfg.device)

        # Create actor-critic model
        self.model = ActorCritic(cfg.network, cfg.obs_dim, cfg.act_dim).to(self.device)

        # Optimizers
        self.actor_optimizer = torch.optim.Adam(
            self.model.parameters(), lr=cfg.ppo.actor_lr, eps=1e-5
        )
        self.critic_optimizer = self.actor_optimizer  # shared optimizer for simplicity

        # Rollout buffer
        self.buffer = RolloutBuffer(
            buffer_size=cfg.ppo.n_steps,
            obs_dim=cfg.obs_dim,
            act_dim=cfg.act_dim,
            gamma=cfg.ppo.gamma,
            gae_lambda=cfg.ppo.gae_lambda,
            device=self.device,
        )

        # TensorBoard writer
        self.writer = SummaryWriter(
            log_dir=os.path.join(cfg.training.checkpoint_dir, cfg.training.experiment_name, "logs")
        )

        # Tracking
        self.global_step = 0
        self.epoch = 0
        self.best_reward = -float("inf")
        self.start_time = time.time()

        # Save config
        self._save_config()

    def _save_config(self):
        """Save the configuration as JSON alongside training logs."""
        config_dir = os.path.join(
            self.cfg.training.checkpoint_dir, self.cfg.training.experiment_name
        )
        os.makedirs(config_dir, exist_ok=True)
        config_path = os.path.join(config_dir, "config.json")
        with open(config_path, "w") as f:
            json.dump(self.cfg.to_dict(), f, indent=2, default=str)

    def _compute_learning_rate(self, progress: float) -> float:
        """Compute current learning rate based on schedule."""
        schedule = self.cfg.ppo.lr_schedule
        initial_lr = self.cfg.ppo.actor_lr
        final_lr = self.cfg.ppo.lr_final

        if schedule == "constant":
            return initial_lr
        elif schedule == "linear":
            return initial_lr + (final_lr - initial_lr) * progress
        elif schedule == "exponential":
            ratio = final_lr / max(initial_lr, 1e-8)
            return initial_lr * (ratio ** progress)
        return initial_lr

    def _compute_entropy_coef(self, progress: float) -> float:
        """Compute current entropy coefficient (annealing)."""
        if not self.cfg.ppo.anneal_entropy:
            return self.cfg.ppo.entropy_coef
        initial = self.cfg.ppo.entropy_coef
        final = self.cfg.ppo.entropy_final
        return initial + (final - initial) * progress

    @torch.no_grad()
    def collect_rollout(self, env) -> Tuple[float, int]:
        """
        Collect a full rollout of n_steps transitions from the environment.

        Returns: (total_reward, episode_count)
        """
        self.model.eval()
        self.buffer.clear()

        obs, _ = env.reset()
        total_reward = 0.0
        episode_count = 0
        episode_reward = 0.0

        for step in range(self.cfg.ppo.n_steps):
            # Convert observation to tensor
            obs_tensor = torch.from_numpy(obs).float().unsqueeze(0).to(self.device)

            # Get action from policy
            action, value, log_prob = self.model.forward(obs_tensor)

            # Step environment
            action_np = action.cpu().numpy().flatten()
            next_obs, reward, terminated, truncated, info = env.step(action_np)
            done = terminated or truncated

            # Store transition
            self.buffer.store(
                obs, action_np, log_prob.item(), reward, done, value.item()
            )

            total_reward += reward
            episode_reward += reward
            self.global_step += 1

            if done:
                episode_count += 1
                episode_reward = 0.0
                obs, _ = env.reset()
            else:
                obs = next_obs

        # Compute advantages with bootstrap value
        with torch.no_grad():
            obs_tensor = torch.from_numpy(obs).float().unsqueeze(0).to(self.device)
            last_value = self.model.get_value(obs_tensor).item()
        last_done = False  # We treat the last state as non-terminal for bootstrapping
        self.buffer.compute_advantages(last_value, last_done)

        return total_reward, episode_count

    def update(self, progress: float):
        """
        Perform PPO update using the collected rollout buffer.

        Uses clipped surrogate objective, value function loss, and entropy bonus.
        """
        self.model.train()

        # Update learning rates
        lr = self._compute_learning_rate(progress)
        for param_group in self.actor_optimizer.param_groups:
            param_group["lr"] = lr

        entropy_coef = self._compute_entropy_coef(progress)

        total_policy_loss = 0.0
        total_value_loss = 0.0
        total_entropy = 0.0
        total_approx_kl = 0.0
        update_count = 0

        # Normalize advantages
        if self.cfg.ppo.normalize_advantages:
            adv = self.buffer.advantages[:self.buffer.get_available_count()]
            adv_mean = adv.mean()
            adv_std = adv.std() + 1e-8
            self.buffer.advantages[:self.buffer.get_available_count()] = (adv - adv_mean) / adv_std

        # Iterate minibatches
        for batch in self.buffer.iterate_minibatches(
            self.cfg.ppo.batch_size, self.cfg.ppo.mini_epochs
        ):
            # Evaluate current policy
            values, log_probs, entropy = self.model.evaluate_actions(
                batch["observations"], batch["actions"]
            )

            # Ratio = exp(log_prob_new - log_prob_old)
            log_ratio = log_probs - batch["log_probs"]
            ratio = torch.exp(log_ratio)

            # Clipped surrogate objective
            advantages = batch["advantages"]
            surr1 = ratio * advantages
            surr2 = torch.clamp(ratio, 1.0 - self.cfg.ppo.clip_epsilon,
                                1.0 + self.cfg.ppo.clip_epsilon) * advantages
            policy_loss = -torch.min(surr1, surr2).mean()

            # Value function loss (clipped)
            if self.cfg.ppo.clip_value_loss:
                value_clipped = batch["returns"] + torch.clamp(
                    values - batch["returns"],
                    -self.cfg.ppo.clip_epsilon,
                    self.cfg.ppo.clip_epsilon,
                )
                value_loss1 = F.mse_loss(values, batch["returns"])
                value_loss2 = F.mse_loss(value_clipped, batch["returns"])
                value_loss = 0.5 * torch.max(value_loss1, value_loss2).mean()
            else:
                value_loss = 0.5 * F.mse_loss(values, batch["returns"])

            # Entropy bonus
            entropy_loss = -entropy * entropy_coef

            # Total loss
            loss = policy_loss + self.cfg.ppo.value_loss_coef * value_loss + entropy_loss

            # Gradient step
            self.actor_optimizer.zero_grad()
            loss.backward()
            nn.utils.clip_grad_norm_(self.model.parameters(), self.cfg.ppo.max_grad_norm)
            self.actor_optimizer.step()

            # Tracking
            with torch.no_grad():
                approx_kl = ((ratio - 1.0) - log_ratio).mean().item()

            total_policy_loss += policy_loss.item()
            total_value_loss += value_loss.item()
            total_entropy += entropy.item()
            total_approx_kl += approx_kl
            update_count += 1

            # Early stopping by KL divergence
            if self.cfg.ppo.target_kl is not None and approx_kl > 1.5 * self.cfg.ppo.target_kl:
                break

        # Log averages
        if update_count > 0:
            self.writer.add_scalar("train/policy_loss", total_policy_loss / update_count, self.global_step)
            self.writer.add_scalar("train/value_loss", total_value_loss / update_count, self.global_step)
            self.writer.add_scalar("train/entropy", total_entropy / update_count, self.global_step)
            self.writer.add_scalar("train/approx_kl", total_approx_kl / update_count, self.global_step)
            self.writer.add_scalar("train/learning_rate", lr, self.global_step)

    def save_checkpoint(self, path: str, metadata: Optional[dict] = None):
        """
        Save a training checkpoint.

        Args:
            path: File path for the checkpoint.
            metadata: Optional dict of extra info to save.
        """
        os.makedirs(os.path.dirname(path), exist_ok=True)
        checkpoint = {
            "model_state_dict": self.model.state_dict(),
            "optimizer_state_dict": self.actor_optimizer.state_dict(),
            "global_step": self.global_step,
            "epoch": self.epoch,
            "best_reward": self.best_reward,
            "config": asdict(self.cfg),
        }
        if metadata:
            checkpoint.update(metadata)
        torch.save(checkpoint, path)
        print(f"  [Checkpoint] Saved to {path}")

    def load_checkpoint(self, path: str) -> dict:
        """
        Load a training checkpoint and return metadata.

        Args:
            path: File path to load.

        Returns:
            dict of checkpoint metadata.
        """
        checkpoint = torch.load(path, map_location=self.device)
        self.model.load_state_dict(checkpoint["model_state_dict"])
        self.actor_optimizer.load_state_dict(checkpoint["optimizer_state_dict"])
        self.global_step = checkpoint.get("global_step", 0)
        self.epoch = checkpoint.get("epoch", 0)
        self.best_reward = checkpoint.get("best_reward", -float("inf"))
        print(f"  [Checkpoint] Loaded from {path} (epoch {self.epoch}, step {self.global_step})")
        return checkpoint

    def get_state_dict(self) -> dict:
        """Return the model's state dict for ONNX export."""
        return self.model.state_dict()

    def train(self, env, on_eval_callback=None):
        """
        Main training loop.

        Args:
            env: Environment instance following the standard Gymnasium API.
            on_eval_callback: Optional callable(reward, epoch) called after each evaluation.
        """
        total_epochs = self.cfg.training.total_epochs
        steps_per_epoch = self.cfg.training.steps_per_epoch
        eval_interval = self.cfg.training.eval_interval
        save_interval = self.cfg.training.save_interval
        log_interval = self.cfg.training.log_interval

        print(f"\n{'='*60}")
        print(f"Starting PPO Training — {self.cfg.avatar} ({self.cfg.obs_dim} obs → {self.cfg.act_dim} act)")
        print(f"Device: {self.device}  |  Total epochs: {total_epochs}")
        print(f"{'='*60}\n")

        for epoch in range(1, total_epochs + 1):
            self.epoch = epoch
            progress = epoch / total_epochs

            # Collect rollout
            rollout_reward, episode_count = self.collect_rollout(env)

            # PPO update
            self.update(progress)

            # Logging
            if epoch % log_interval == 0:
                elapsed = time.time() - self.start_time
                avg_reward = rollout_reward / max(episode_count, 1)
                self.writer.add_scalar("train/rollout_reward", rollout_reward, self.global_step)
                self.writer.add_scalar("train/avg_episode_reward", avg_reward, self.global_step)
                self.writer.add_scalar("train/progress", progress, self.global_step)

                print(f"  Epoch {epoch:3d}/{total_epochs} | "
                      f"Step {self.global_step:7d} | "
                      f"Rollout reward: {rollout_reward:8.1f} | "
                      f"Avg reward: {avg_reward:6.2f} | "
                      f"Elapsed: {elapsed:.0f}s")

            # Evaluation
            if epoch % eval_interval == 0:
                eval_reward = self._evaluate(env, num_episodes=self.cfg.training.eval_episodes)
                self.writer.add_scalar("eval/avg_reward", eval_reward, self.global_step)
                print(f"  └─ Evaluation: avg reward = {eval_reward:.2f} over {self.cfg.training.eval_episodes} episodes")

                if on_eval_callback:
                    on_eval_callback(eval_reward, epoch)

                # Save best model
                if eval_reward > self.best_reward:
                    self.best_reward = eval_reward
                    best_path = os.path.join(
                        self.cfg.training.checkpoint_dir,
                        self.cfg.training.experiment_name,
                        f"best_model.pt",
                    )
                    self.save_checkpoint(best_path, {"eval_reward": eval_reward})
                    print(f"  └─ ★ New best model! Reward: {eval_reward:.2f}")

            # Save periodic checkpoint
            if epoch % save_interval == 0:
                ckpt_path = os.path.join(
                    self.cfg.training.checkpoint_dir,
                    self.cfg.training.experiment_name,
                    f"checkpoint_epoch_{epoch:04d}.pt",
                )
                self.save_checkpoint(ckpt_path)

        print(f"\n{'='*60}")
        print(f"Training complete! Total time: {time.time() - self.start_time:.0f}s")
        print(f"Best evaluation reward: {self.best_reward:.2f}")
        print(f"Model saved in: {os.path.join(self.cfg.training.checkpoint_dir, self.cfg.training.experiment_name)}")
        print(f"{'='*60}\n")

        self.writer.close()

    @torch.no_grad()
    def _evaluate(self, env, num_episodes: int = 5) -> float:
        """
        Evaluate the current policy over a number of episodes.

        Returns: average reward across episodes.
        """
        self.model.eval()
        total_rewards = []

        for ep in range(num_episodes):
            obs, _ = env.reset()
            episode_reward = 0.0
            done = False
            while not done:
                obs_tensor = torch.from_numpy(obs).float().unsqueeze(0).to(self.device)
                action = self.model.get_action(obs_tensor, deterministic=True)
                obs, reward, terminated, truncated, _ = env.step(action.cpu().numpy().flatten())
                done = terminated or truncated
                episode_reward += reward
            total_rewards.append(episode_reward)

        return float(np.mean(total_rewards))

    @torch.no_grad()
    def infer(self, obs: np.ndarray, deterministic: bool = True) -> np.ndarray:
        """
        Run inference on a single observation.

        Args:
            obs: Observation array of shape (obs_dim,).
            deterministic: If True, use mean action (no sampling).

        Returns:
            Action array of shape (act_dim,).
        """
        self.model.eval()
        obs_tensor = torch.from_numpy(obs).float().unsqueeze(0).to(self.device)
        action = self.model.get_action(obs_tensor, deterministic=deterministic)
        return action.cpu().numpy().flatten()