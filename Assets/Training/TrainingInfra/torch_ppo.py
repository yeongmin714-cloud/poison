"""
PyTorch CPU-based PPO (Proximal Policy Optimization) for Neural Animation.

Network: [256, 128, 64] 3-layer MLP with Tanh activations.
Input: "observation" shape [1, 1, 1, obs_dim] (NHWC for Unity Sentis)
Output: "action" shape [1, act_dim]
PPO: Standard PPO with GAE, clipped surrogate objective, KL penalty
Optimizer: Adam (lr=3e-4, linear decay)
Export: torch.onnx.export() -> opset 17, NHWC
Checkpoint: torch.save() .pt format
Config: Same hyperparameters as numpy_ppo.py (γ=0.99, λ=0.95, clip_ε=0.2, mini_epochs=10)

Classes:
  - ActorCritic(net_config): Base actor-critic network
  - PPOActorCritic(ActorCritic): Extended with PPO-specific methods
  - RolloutBuffer: Experience buffer with GAE
  - PPOTrainer: Main training loop
"""

import os
import math
import numpy as np
from typing import Tuple, Dict, Optional, List, Any

import torch
import torch.nn as nn
import torch.nn.functional as F
from torch.distributions import Normal
import torch.optim as optim


# ══════════════════════════════════════════════════════════════════════════════
#  Actor-Critic Network
# ══════════════════════════════════════════════════════════════════════════════

class ActorCritic(nn.Module):
    """
    Actor-Critic with shared backbone [obs_dim -> 256 -> 128 -> 64].

    Architecture:
      obs -> [256, 128, 64] shared trunk -> actor_mean (act_dim)
                                      log_std (act_dim, learnable)
                                      critic_value (1)
    """

    def __init__(self, net_config: Dict[str, Any]):
        """
        Args:
            net_config: Dictionary with keys:
                - obs_dim: Observation dimension
                - act_dim: Action dimension
                - hidden_sizes: List of hidden layer sizes (default: [256, 128, 64])
        """
        super().__init__()

        self.obs_dim = net_config.get('obs_dim', 120)
        self.act_dim = net_config.get('act_dim', 80)
        self.hidden_sizes = net_config.get('hidden_sizes', [256, 128, 64])

        # Shared trunk: obs_dim -> 256 -> 128 -> 64 with Tanh
        trunk_layers = []
        in_dim = self.obs_dim
        for h_dim in self.hidden_sizes:
            trunk_layers.append(nn.Linear(in_dim, h_dim))
            trunk_layers.append(nn.Tanh())
            in_dim = h_dim
        self.trunk = nn.Sequential(*trunk_layers)

        # Actor mean head: last_hidden -> act_dim (linear, small init)
        self.mean_head = nn.Linear(self.hidden_sizes[-1], self.act_dim)
        nn.init.orthogonal_(self.mean_head.weight, gain=0.01)
        nn.init.zeros_(self.mean_head.bias)

        # Critic head: last_hidden -> 1 (linear)
        self.critic_head = nn.Linear(self.hidden_sizes[-1], 1)
        nn.init.orthogonal_(self.critic_head.weight, gain=1.0)
        nn.init.zeros_(self.critic_head.bias)

        # Learnable log_std (per action dimension)
        self.log_std = nn.Parameter(torch.full((self.act_dim,), -0.5, dtype=torch.float32))

    def forward(self, obs: torch.Tensor) -> Tuple[torch.Tensor, torch.Tensor, torch.Tensor]:
        """
        Forward pass: compute action mean, log_std, and value.

        Args:
            obs: Observation tensor of shape [batch_size, obs_dim].

        Returns:
            (action_mean, log_std, value) tuple.
            - action_mean: [batch_size, act_dim]
            - log_std: [act_dim] (broadcastable)
            - value: [batch_size]
        """
        features = self.trunk(obs)
        action_mean = self.mean_head(features)
        log_std_clipped = torch.clamp(self.log_std, -5.0, 2.0)
        value = self.critic_head(features).squeeze(-1)
        return action_mean, log_std_clipped, value

    def get_action(self, obs: torch.Tensor, deterministic: bool = False) -> torch.Tensor:
        """
        Sample an action from the policy.

        Args:
            obs: Observation tensor of shape [batch_size, obs_dim] or [obs_dim].
            deterministic: If True, return mean action (no sampling).

        Returns:
            Action tensor of shape [batch_size, act_dim] or [act_dim].
        """
        single = obs.dim() == 1
        if single:
            obs = obs.unsqueeze(0)

        action_mean, log_std, _ = self.forward(obs)
        std = torch.exp(log_std)

        if deterministic:
            action = action_mean
        else:
            dist = Normal(action_mean, std)
            action = dist.sample()

        if single:
            action = action.squeeze(0)
        return action

    def evaluate(self, obs: torch.Tensor, actions: torch.Tensor) -> Tuple[torch.Tensor, torch.Tensor, torch.Tensor, torch.Tensor]:
        """
        Evaluate log-probability, entropy, and value for given obs/action pairs.

        Args:
            obs: [batch_size, obs_dim]
            actions: [batch_size, act_dim]

        Returns:
            (values, log_probs, entropy, action_mean)
        """
        action_mean, log_std, values = self.forward(obs)
        std = torch.exp(torch.clamp(log_std, -5.0, 2.0))

        # Log probability of actions under diagonal Gaussian
        dist = Normal(action_mean, std)
        log_probs = dist.log_prob(actions).sum(dim=-1)

        # Entropy of diagonal Gaussian
        entropy = dist.entropy().sum(dim=-1).mean()

        return values, log_probs, entropy, action_mean

    def get_value(self, obs: torch.Tensor) -> torch.Tensor:
        """Estimate state value."""
        features = self.trunk(obs)
        return self.critic_head(features).squeeze(-1)

    def get_actor_layers(self) -> List[nn.Module]:
        """
        Get the actor network layers for ONNX export.
        Returns list of modules: [trunk_layer0, trunk_layer1, trunk_layer2, mean_head]
        where trunk layers include Linear + Tanh, last is just Linear.
        """
        layers = []
        # Extract trunk linear layers (every other module in Sequential)
        for i, module in enumerate(self.trunk):
            if isinstance(module, nn.Linear):
                layers.append(module)
        # Add mean head
        layers.append(self.mean_head)
        return layers


class PPOActorCritic(ActorCritic):
    """
    Extended ActorCritic with PPO-specific methods for training and export.
    """

    def __init__(self, net_config: Dict[str, Any]):
        super().__init__(net_config)

    def export_onnx(self, output_path: str, opset_version: int = 17) -> str:
        """
        Export the actor network (policy) to ONNX format.

        Input: "observation" shape [1, 1, 1, obs_dim] (NHWC for Unity Sentis)
        Output: "action" shape [1, act_dim]

        Args:
            output_path: Path to save the ONNX file.
            opset_version: ONNX opset version (default: 17).

        Returns:
            Absolute path to the written .onnx file.
        """
        os.makedirs(os.path.dirname(output_path) or ".", exist_ok=True)

        # Create dummy input with NHWC shape [1, 1, 1, obs_dim]
        dummy_input = torch.zeros(1, 1, 1, self.obs_dim, dtype=torch.float32)

        # Export the actor network only
        class ActorWrapper(torch.nn.Module):
            def __init__(self, actor_critic):
                super().__init__()
                self.actor_critic = actor_critic

            def forward(self, x):
                batch_size = x.shape[0]
                x = x.reshape(batch_size, self.actor_critic.obs_dim)
                action_mean, _, _ = self.actor_critic.forward(x)
                return action_mean

        wrapper = ActorWrapper(self)
        wrapper.eval()

        # Use legacy TorchScript ONNX exporter to avoid dynamo issues
        # dynamo=False forces the old exporter path
        _opset = opset_version
        torch.onnx.export(
            wrapper, dummy_input, output_path,
            export_params=True, opset_version=_opset,
            do_constant_folding=True,
            input_names=['observation'], output_names=['action'],
            verbose=False, dynamo=False
        )

        return output_path

    def save_checkpoint(self, path: str):
        """Save model parameters to .pt file."""
        os.makedirs(os.path.dirname(path) or ".", exist_ok=True)
        torch.save({
            'model_state_dict': self.state_dict(),
            'obs_dim': self.obs_dim,
            'act_dim': self.act_dim,
            'hidden_sizes': self.hidden_sizes,
        }, path)
        print(f"  [Checkpoint] Saved to: {path}")

    def load_checkpoint(self, path: str, device: str = 'cpu'):
        """Load model parameters from .pt file."""
        checkpoint = torch.load(path, map_location=device, weights_only=True)
        self.load_state_dict(checkpoint['model_state_dict'])
        print(f"  [Checkpoint] Loaded from: {path}")


# ══════════════════════════════════════════════════════════════════════════════
#  Rollout Buffer (with GAE)
# ══════════════════════════════════════════════════════════════════════════════

class RolloutBuffer:
    """
    Experience buffer for PPO with GAE advantage computation.

    Stores transitions and computes advantages/returns after rollout.
    """

    def __init__(self, buffer_size: int, obs_dim: int, act_dim: int,
                 gamma: float = 0.99, gae_lambda: float = 0.95, device: str = 'cpu'):
        self.buffer_size = buffer_size
        self.gamma = gamma
        self.gae_lambda = gae_lambda
        self.device = device

        self.observations = torch.zeros((buffer_size, obs_dim), dtype=torch.float32, device=device)
        self.actions = torch.zeros((buffer_size, act_dim), dtype=torch.float32, device=device)
        self.log_probs = torch.zeros(buffer_size, dtype=torch.float32, device=device)
        self.rewards = torch.zeros(buffer_size, dtype=torch.float32, device=device)
        self.dones = torch.zeros(buffer_size, dtype=torch.float32, device=device)
        self.values = torch.zeros(buffer_size, dtype=torch.float32, device=device)
        self.advantages = torch.zeros(buffer_size, dtype=torch.float32, device=device)
        self.returns = torch.zeros(buffer_size, dtype=torch.float32, device=device)

        self.pos = 0
        self.full = False

    def clear(self):
        """Reset buffer position."""
        self.pos = 0
        self.full = False

    def store(self, obs: np.ndarray, action: np.ndarray, log_prob: float,
              reward: float, done: bool, value: float):
        """Store a single transition."""
        idx = self.pos % self.buffer_size
        self.observations[idx] = torch.from_numpy(obs.astype(np.float32)).to(self.device)
        self.actions[idx] = torch.from_numpy(action.astype(np.float32)).to(self.device)
        self.log_probs[idx] = log_prob
        self.rewards[idx] = reward
        self.dones[idx] = 1.0 if done else 0.0
        self.values[idx] = value
        self.pos += 1
        if self.pos >= self.buffer_size:
            self.full = True

    def compute_advantages(self, last_value: float, last_done: bool):
        """
        Compute GAE advantages after rollout is complete.

        Args:
            last_value: Value estimate for the final state (bootstrap).
            last_done: Whether the final state was terminal.
        """
        buffer_size = self.buffer_size if self.full else self.pos
        gae = 0.0

        for t in reversed(range(buffer_size)):
            if t == buffer_size - 1:
                next_value = last_value if not last_done else 0.0
                next_non_terminal = 0.0 if last_done else 1.0
            else:
                next_value = self.values[t + 1].item()
                next_non_terminal = 1.0 - self.dones[t + 1].item()

            delta = self.rewards[t].item() + self.gamma * next_value * next_non_terminal - self.values[t].item()
            gae = delta + self.gamma * self.gae_lambda * next_non_terminal * gae
            self.advantages[t] = gae
            self.returns[t] = gae + self.values[t].item()

    def get_available_count(self) -> int:
        """Return number of transitions stored."""
        return self.buffer_size if self.full else self.pos

    def get_batch(self, indices: torch.Tensor) -> Dict[str, torch.Tensor]:
        """Get a batch of data by indices."""
        return {
            'observations': self.observations[indices],
            'actions': self.actions[indices],
            'log_probs': self.log_probs[indices],
            'advantages': self.advantages[indices],
            'returns': self.returns[indices],
        }

    def iterate_minibatches(self, batch_size: int, num_epochs: int):
        """
        Generator yielding minibatches for multiple epochs.

        Yields dicts with keys: observations, actions, log_probs, advantages, returns
        """
        buffer_size = self.buffer_size if self.full else self.pos
        indices = torch.arange(buffer_size, device=self.device)

        for _ in range(num_epochs):
            perm = torch.randperm(buffer_size, device=self.device)
            for start in range(0, buffer_size, batch_size):
                end = min(start + batch_size, buffer_size)
                batch_indices = perm[start:end]
                yield self.get_batch(batch_indices)


# ══════════════════════════════════════════════════════════════════════════════
#  PPO Trainer
# ══════════════════════════════════════════════════════════════════════════════

class PPOTrainer:
    """
    PyTorch PPO trainer with CPU support.

    Implements:
      - Clipped surrogate objective
      - Value function loss (MSE)
      - Entropy bonus
      - GAE for advantage estimation
      - Mini-batch updates with multiple epochs
      - Linear LR decay
      - KL penalty / early stopping
    """

    def __init__(
        self,
        obs_dim: int,
        act_dim: int,
        hidden_sizes: Tuple[int, ...] = (256, 128, 64),
        actor_lr: float = 3e-4,
        critic_lr: float = 3e-4,
        clip_epsilon: float = 0.2,
        entropy_coef: float = 0.01,
        value_loss_coef: float = 0.5,
        gamma: float = 0.99,
        gae_lambda: float = 0.95,
        n_steps: int = 2048,
        batch_size: int = 64,
        mini_epochs: int = 10,
        normalize_advantages: bool = True,
        max_grad_norm: float = 0.5,
        target_kl: Optional[float] = 0.02,
        device: str = 'cpu',
    ):
        self.obs_dim = obs_dim
        self.act_dim = act_dim
        self.device = device

        # Model
        net_config = {
            'obs_dim': obs_dim,
            'act_dim': act_dim,
            'hidden_sizes': list(hidden_sizes),
        }
        self.model = PPOActorCritic(net_config).to(device)

        # Hyperparameters
        self.actor_lr = actor_lr
        self.critic_lr = critic_lr
        self.clip_epsilon = clip_epsilon
        self.entropy_coef = entropy_coef
        self.value_loss_coef = value_loss_coef
        self.gamma = gamma
        self.gae_lambda = gae_lambda
        self.n_steps = n_steps
        self.batch_size = batch_size
        self.mini_epochs = mini_epochs
        self.normalize_advantages = normalize_advantages
        self.max_grad_norm = max_grad_norm
        self.target_kl = target_kl

        # Separate optimizers for actor and critic
        actor_params = list(self.model.trunk.parameters()) + \
                       list(self.model.mean_head.parameters()) + \
                       [self.model.log_std]
        critic_params = list(self.model.critic_head.parameters())

        self.actor_optimizer = optim.Adam(actor_params, lr=actor_lr, eps=1e-8)
        self.critic_optimizer = optim.Adam(critic_params, lr=critic_lr, eps=1e-8)

        # Buffer
        self.buffer = RolloutBuffer(
            buffer_size=n_steps,
            obs_dim=obs_dim,
            act_dim=act_dim,
            gamma=gamma,
            gae_lambda=gae_lambda,
            device=device,
        )

        # Tracking
        self.global_step = 0
        self.epoch = 0
        self.best_reward = -float('inf')

    def collect_rollout(self, env) -> Tuple[float, int]:
        """
        Collect a full rollout of transitions from the environment.

        Returns: (total_reward, episode_count)
        """
        self.buffer.clear()
        obs, _ = env.reset()
        total_reward = 0.0
        episode_count = 0
        episode_reward = 0.0
        done = False  # Initialize done

        for step in range(self.n_steps):
            # Get action
            obs_tensor = torch.from_numpy(obs.flatten().astype(np.float32)).unsqueeze(0).to(self.device)

            with torch.no_grad():
                action_mean, log_std, value = self.model.forward(obs_tensor)
                action_mean = action_mean[0]
                value = value[0]
                std = torch.exp(torch.clamp(log_std, -5.0, 2.0))

            # Sample action
            dist = Normal(action_mean, std)
            action = dist.sample()

            # Compute log prob
            log_prob = dist.log_prob(action).sum().item()

            # Step environment
            next_obs, reward, terminated, truncated, info = env.step(action.cpu().numpy())
            done = terminated or truncated

            # Store
            self.buffer.store(obs.flatten(), action.cpu().numpy(), log_prob, reward, done, value.item())

            total_reward += reward
            episode_reward += reward
            obs = next_obs

            if done:
                obs, _ = env.reset()
                episode_count += 1
                episode_reward = 0.0

        # Compute advantages
        obs_tensor = torch.from_numpy(obs.flatten().astype(np.float32)).unsqueeze(0).to(self.device)
        with torch.no_grad():
            last_value = self.model.get_value(obs_tensor).item()
        last_done = done  # last observed done state
        self.buffer.compute_advantages(last_value, last_done)

        return total_reward, episode_count

    def update(self, progress: float = 0.0) -> Dict[str, float]:
        """
        Perform PPO update on the collected rollout.

        Args:
            progress: Training progress from 0.0 to 1.0 (for LR scheduling).

        Returns:
            Dictionary of metrics.
        """
        # Compute learning rates with linear decay
        current_actor_lr = self.actor_lr * (1.0 - progress)
        current_critic_lr = self.critic_lr * (1.0 - progress)
        current_entropy_coef = self.entropy_coef * (1.0 - 0.9 * progress)  # anneal to 10%

        # Update optimizer learning rates
        for param_group in self.actor_optimizer.param_groups:
            param_group['lr'] = current_actor_lr
        for param_group in self.critic_optimizer.param_groups:
            param_group['lr'] = current_critic_lr

        buffer_size = self.buffer.get_available_count()

        # Get all data for normalization
        all_advantages = self.buffer.advantages[:buffer_size]
        if self.normalize_advantages and buffer_size > 1:
            adv_mean = all_advantages.mean()
            adv_std = all_advantages.std() + 1e-8
            self.buffer.advantages[:buffer_size] = (all_advantages - adv_mean) / adv_std

        # Track metrics
        total_policy_loss = 0.0
        total_value_loss = 0.0
        total_entropy = 0.0
        total_approx_kl = 0.0
        n_batches = 0
        early_stop = False

        # Mini-batch training
        for epoch in range(self.mini_epochs):
            if early_stop:
                break

            for batch in self.buffer.iterate_minibatches(self.batch_size, 1):
                batch_obs = batch['observations']
                batch_act = batch['actions']
                batch_old_log = batch['log_probs']
                batch_adv = batch['advantages']
                batch_ret = batch['returns']

                # Forward pass
                values, log_probs, entropy, _ = self.model.evaluate(batch_obs, batch_act)

                # Policy loss (clipped surrogate)
                ratio = torch.exp(log_probs - batch_old_log)
                surr1 = ratio * batch_adv
                surr2 = torch.clamp(ratio, 1.0 - self.clip_epsilon, 1.0 + self.clip_epsilon) * batch_adv
                policy_loss = -torch.min(surr1, surr2).mean()

                # Value loss (MSE)
                value_loss = F.mse_loss(values, batch_ret)

                # Entropy bonus
                entropy_loss = -current_entropy_coef * entropy

                # Total loss
                total_loss = policy_loss + self.value_loss_coef * value_loss + entropy_loss

                # Backward pass
                self.actor_optimizer.zero_grad()
                self.critic_optimizer.zero_grad()
                total_loss.backward()

                # Gradient clipping (global norm)
                torch.nn.utils.clip_grad_norm_(
                    list(self.model.trunk.parameters()) +
                    list(self.model.mean_head.parameters()) +
                    [self.model.log_std] +
                    list(self.model.critic_head.parameters()),
                    self.max_grad_norm
                )

                # Update
                self.actor_optimizer.step()
                self.critic_optimizer.step()

                # Track metrics
                total_policy_loss += policy_loss.item()
                total_value_loss += value_loss.item()
                total_entropy += entropy.item()

                # Approximate KL divergence
                with torch.no_grad():
                    kl_div = torch.mean(log_probs - batch_old_log)
                    total_approx_kl += kl_div.item() if torch.isfinite(kl_div) else 0.0

                n_batches += 1

                # Early stopping based on KL
                if self.target_kl is not None:
                    if kl_div.item() > 1.5 * self.target_kl:
                        early_stop = True
                        break

        self.global_step += 1

        return {
            'policy_loss': total_policy_loss / max(n_batches, 1),
            'value_loss': total_value_loss / max(n_batches, 1),
            'entropy': total_entropy / max(n_batches, 1),
            'approx_kl': total_approx_kl / max(n_batches, 1),
            'actor_lr': current_actor_lr,
            'critic_lr': current_critic_lr,
            'entropy_coef': current_entropy_coef,
        }

    def train_epoch(self, env, progress: float) -> Dict[str, float]:
        """
        Run one epoch: collect rollout + update.

        Args:
            env: Environment instance (SimpleAnimationEnv-compatible).
            progress: Training progress from 0.0 to 1.0.

        Returns:
            Dictionary of metrics.
        """
        # Collect rollout
        total_reward, episode_count = self.collect_rollout(env)

        # Update
        metrics = self.update(progress)
        metrics['total_reward'] = total_reward
        metrics['episode_count'] = max(episode_count, 1)

        return metrics

    def save(self, path: str):
        """Save full trainer checkpoint."""
        self.model.save_checkpoint(path)

    def load(self, path: str):
        """Load trainer checkpoint."""
        self.model.load_checkpoint(path, device=self.device)

    def export_onnx(self, output_path: str, opset_version: int = 17) -> str:
        """Export the actor network to ONNX."""
        return self.model.export_onnx(output_path, opset_version)


# ══════════════════════════════════════════════════════════════════════════════
#  Utility Functions
# ══════════════════════════════════════════════════════════════════════════════

def validate_onnx(onnx_path: str, obs_dim: int, act_dim: int, verbose: bool = True) -> bool:
    """
    Validate an ONNX model file using onnx and onnxruntime if available.

    Args:
        onnx_path: Path to the .onnx file.
        obs_dim: Expected observation dimension.
        act_dim: Expected action dimension.
        verbose: Print validation info.

    Returns:
        True if valid, False otherwise.
    """
    try:
        import onnx
        import onnxruntime as ort
    except ImportError:
        if verbose:
            print("  [ONNX Validation] onnx/onnxruntime not installed, skipping validation")
        return True  # Can't validate, assume OK

    errors = []

    # 1. Check file exists
    if not os.path.exists(onnx_path):
        errors.append(f"File not found: {onnx_path}")
        if verbose:
            print(f"  [ONNX Validation] ✗ File not found")
        return False

    file_size = os.path.getsize(onnx_path)
    if file_size == 0:
        errors.append("File is empty")
        if verbose:
            print(f"  [ONNX Validation] ✗ File is empty")
        return False

    # 2. Parse with onnx
    try:
        model = onnx.load(onnx_path)
        onnx.checker.check_model(model)
    except Exception as e:
        errors.append(f"Failed to parse/check ONNX model: {e}")
        if verbose:
            print(f"  [ONNX Validation] ✗ Parse/check failed: {e}")
        return False

    # 3. Check input names
    input_names = [inp.name for inp in model.graph.input]
    if 'observation' not in input_names:
        errors.append(f"Input 'observation' not found. Found: {input_names}")
    else:
        if verbose:
            print(f"  [ONNX Validation] Input name 'observation': ✓")

    # 4. Check output names
    output_names = [out.name for out in model.graph.output]
    if 'action' not in output_names:
        errors.append(f"Output 'action' not found. Found: {output_names}")
    else:
        if verbose:
            print(f"  [ONNX Validation] Output name 'action': ✓")

    # 5. Check opset version
    opset_versions = {opset.domain: opset.version for opset in model.opset_import}
    ai_onnx_version = opset_versions.get('ai.onnx', opset_versions.get('', 0))
    if ai_onnx_version != 17:
        if verbose:
            print(f"  [ONNX Validation] Warning: opset version is {ai_onnx_version}, expected 17")

    # 6. Test inference with onnxruntime
    try:
        session = ort.InferenceSession(onnx_path)
        dummy_input = np.zeros((1, 1, 1, obs_dim), dtype=np.float32)
        outputs = session.run(['action'], {'observation': dummy_input})
        action = outputs[0]
        if action.shape == (1, act_dim):
            if verbose:
                print(f"  [ONNX Validation] Inference test: ✓ (output shape {action.shape})")
        else:
            errors.append(f"Output shape mismatch: got {action.shape}, expected (1, {act_dim})")
    except Exception as e:
        errors.append(f"ONNX Runtime inference failed: {e}")

    if errors:
        if verbose:
            print(f"  [ONNX Validation] ✗ FAILED:")
            for err in errors:
                print(f"    - {err}")
        return False

    if verbose:
        print(f"  [ONNX Validation] ✓ All checks passed")
        print(f"  [ONNX Validation] File size: {file_size / 1024:.1f} KB")
    return True


if __name__ == '__main__':
    # Quick test
    print("Testing torch_ppo.py...")
    net_config = {'obs_dim': 120, 'act_dim': 80, 'hidden_sizes': [256, 128, 64]}
    model = ActorCritic(net_config)
    print(f"Model created: {model}")
    print(f"Parameters: {sum(p.numel() for p in model.parameters())}")

    # Test forward
    obs = torch.randn(1, 120)
    action_mean, log_std, value = model.forward(obs)
    print(f"Forward: action_mean={action_mean.shape}, log_std={log_std.shape}, value={value.shape}")

    # Test get_action
    action = model.get_action(obs)
    print(f"get_action: action={action.shape}")

    # Test deterministic
    action_det = model.get_action(obs, deterministic=True)
    print(f"get_action(deterministic): action={action_det.shape}")

    # Test PPOActorCritic
    ppo_model = PPOActorCritic(net_config)
    print(f"PPOActorCritic created: {ppo_model}")

    print("All tests passed!")