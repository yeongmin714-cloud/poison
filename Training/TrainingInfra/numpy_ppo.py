"""
Pure NumPy PPO (Proximal Policy Optimization) implementation for Neural Animation.

No PyTorch or ONNX dependency — only numpy.
Compatible with SimpleAnimationEnv and Config.

Architecture:
  - Actor-Critic with shared backbone [obs_dim -> 64 -> 64 -> action_mean, log_std, value]
  - Tanh activations
  - GAE (Generalized Advantage Estimation)
  - Clipped surrogate objective with entropy bonus
"""

import os
import math
import numpy as np
from typing import Tuple, Dict, Optional, List, Callable


# ══════════════════════════════════════════════════════════════════════════════
#  MLP Network (numpy-based)
# ══════════════════════════════════════════════════════════════════════════════

class MLP:
    """
    Multi-layer perceptron with Tanh activations.

    Stores weights and biases as numpy arrays.
    Provides forward() for inference and get_params()/set_params() for checkpointing.
    """

    def __init__(self, layer_sizes: List[int], activation: str = "tanh"):
        """
        Args:
            layer_sizes: List of layer dimensions, e.g. [obs_dim, 64, 64, act_dim].
                         Includes input and output dimensions.
            activation: Activation function name ("tanh" or "relu").
        """
        self.layer_sizes = layer_sizes
        self.activation = activation.lower()
        self.num_layers = len(layer_sizes) - 1

        # Initialize weights and biases
        self.weights = []
        self.biases = []
        rng = np.random.RandomState(42)

        for i in range(self.num_layers):
            in_dim = layer_sizes[i]
            out_dim = layer_sizes[i + 1]

            # Orthogonal-like initialization (numpy version)
            weight = rng.randn(in_dim, out_dim).astype(np.float32)
            # Approximate orthogonal init
            u, s, vt = np.linalg.svd(weight, full_matrices=False)
            weight = u @ vt
            # Gain: sqrt(2) for tanh, 1.0 for last layer
            gain = 1.0 if i == self.num_layers - 1 else math.sqrt(2.0)
            weight = (weight * gain).astype(np.float32)
            bias = np.zeros(out_dim, dtype=np.float32)

            self.weights.append(weight)
            self.biases.append(bias)

    def forward(self, x: np.ndarray) -> np.ndarray:
        """
        Forward pass through the network.

        Args:
            x: Input array of shape [batch_size, input_dim].

        Returns:
            Output array of shape [batch_size, output_dim].
        """
        h = x
        for i in range(self.num_layers):
            h = h @ self.weights[i] + self.biases[i]
            if i < self.num_layers - 1:
                if self.activation == "tanh":
                    h = np.tanh(h)
                elif self.activation == "relu":
                    h = np.maximum(0.0, h)
        return h

    def get_params(self) -> dict:
        """Return all parameters as a dictionary of numpy arrays."""
        params = {}
        for i, (w, b) in enumerate(zip(self.weights, self.biases)):
            params[f"w{i}"] = w
            params[f"b{i}"] = b
        return params

    def set_params(self, params: dict):
        """Set parameters from a dictionary."""
        for i in range(self.num_layers):
            self.weights[i] = params[f"w{i}"].astype(np.float32)
            self.biases[i] = params[f"b{i}"].astype(np.float32)

    def num_params(self) -> int:
        """Return total number of parameters."""
        return sum(w.size + b.size for w, b in zip(self.weights, self.biases))


# ══════════════════════════════════════════════════════════════════════════════
#  Actor-Critic
# ══════════════════════════════════════════════════════════════════════════════

class ActorCritic:
    """
    Actor-Critic with shared backbone.

    Architecture:
      obs -> [64, 64] shared trunk -> actor_mean (act_dim)
                                      log_std (act_dim, learnable)
                                      critic_value (1)
    """

    def __init__(self, obs_dim: int, act_dim: int, hidden_sizes: Tuple[int, ...] = (64, 64)):
        self.obs_dim = obs_dim
        self.act_dim = act_dim
        self.hidden_sizes = list(hidden_sizes)

        # Shared trunk: obs_dim -> h1 -> h2
        trunk_sizes = [obs_dim] + self.hidden_sizes
        self.trunk = MLP(trunk_sizes, activation="tanh")

        # Actor mean head: last_hidden -> act_dim
        self.mean_head = MLP([self.hidden_sizes[-1], act_dim], activation="linear")

        # Critic head: last_hidden -> 1
        self.critic_head = MLP([self.hidden_sizes[-1], 1], activation="linear")

        # Learnable log_std (per action dimension)
        self.log_std = np.full(act_dim, -0.5, dtype=np.float32)

        # Initialize mean head with small weights (like orthogonal with gain=0.01)
        self.mean_head.weights[0] *= 0.01
        self.mean_head.biases[0].fill(0.0)

    def forward(self, obs: np.ndarray) -> Tuple[np.ndarray, np.ndarray, np.ndarray]:
        """
        Forward pass: compute action mean, log_std, and value.

        Args:
            obs: Observation array of shape [batch_size, obs_dim].

        Returns:
            (action_mean, log_std, value) tuple.
            - action_mean: [batch_size, act_dim]
            - log_std: [act_dim] (broadcastable)
            - value: [batch_size]
        """
        features = self.trunk.forward(obs)
        action_mean = self.mean_head.forward(features)
        log_std_clipped = np.clip(self.log_std, -5.0, 2.0)
        value = self.critic_head.forward(features).squeeze(-1)
        return action_mean, log_std_clipped, value

    def get_action(self, obs: np.ndarray, deterministic: bool = False) -> np.ndarray:
        """
        Sample an action from the policy.

        Args:
            obs: Observation array of shape [batch_size, obs_dim] or [obs_dim].
            deterministic: If True, return mean action (no sampling).

        Returns:
            Action array of shape [batch_size, act_dim] or [act_dim].
        """
        single = obs.ndim == 1
        if single:
            obs = obs.reshape(1, -1)

        action_mean, log_std, _ = self.forward(obs)
        std = np.exp(log_std)

        if deterministic:
            action = action_mean
        else:
            action = action_mean + np.random.randn(*action_mean.shape) * std

        if single:
            action = action.flatten()
        return action

    def evaluate(self, obs: np.ndarray, actions: np.ndarray) -> Tuple[np.ndarray, np.ndarray, np.ndarray, np.ndarray]:
        """
        Evaluate log-probability, entropy, and value for given obs/action pairs.

        Args:
            obs: [batch_size, obs_dim]
            actions: [batch_size, act_dim]

        Returns:
            (values, log_probs, entropy, action_mean)
        """
        action_mean, log_std, values = self.forward(obs)
        std = np.exp(np.clip(log_std, -5.0, 2.0))

        # Log probability of actions under diagonal Gaussian
        # log N(x|mu,sigma) = -0.5 * sum((x-mu)^2/sigma^2 + log(2*pi*sigma^2))
        var = std ** 2
        log_probs = -0.5 * np.sum(
            ((actions - action_mean) ** 2) / var + np.log(2.0 * math.pi * var),
            axis=-1
        )

        # Entropy of diagonal Gaussian
        # H = 0.5 * sum(log(2*pi*e*sigma^2))
        entropy = 0.5 * np.sum(
            np.log(2.0 * math.pi * math.e * var),
            axis=-1
        ).mean()

        return values, log_probs, entropy, action_mean

    def get_value(self, obs: np.ndarray) -> np.ndarray:
        """Estimate state value."""
        features = self.trunk.forward(obs)
        return self.critic_head.forward(features).squeeze(-1)

    def get_params(self) -> dict:
        """Return all parameters as a flat dictionary."""
        params = {}
        trunk_params = self.trunk.get_params()
        for k, v in trunk_params.items():
            params[f"trunk_{k}"] = v
        mean_params = self.mean_head.get_params()
        for k, v in mean_params.items():
            params[f"mean_{k}"] = v
        critic_params = self.critic_head.get_params()
        for k, v in critic_params.items():
            params[f"critic_{k}"] = v
        params["log_std"] = self.log_std.copy()
        return params

    def set_params(self, params: dict):
        """Set all parameters from a dictionary."""
        trunk_params = {}
        mean_params = {}
        critic_params = {}
        for k, v in params.items():
            if k.startswith("trunk_"):
                trunk_params[k[6:]] = v
            elif k.startswith("mean_"):
                mean_params[k[5:]] = v
            elif k.startswith("critic_"):
                critic_params[k[7:]] = v
            elif k == "log_std":
                self.log_std = v.copy()
        self.trunk.set_params(trunk_params)
        self.mean_head.set_params(mean_params)
        self.critic_head.set_params(critic_params)

    def save_checkpoint(self, path: str):
        """Save model parameters to .npz file."""
        params = self.get_params()
        np.savez_compressed(path, **params)
        print(f"  [Checkpoint] Saved to: {path}")

    def load_checkpoint(self, path: str):
        """Load model parameters from .npz file."""
        data = np.load(path)
        params = {k: data[k] for k in data.files}
        self.set_params(params)
        print(f"  [Checkpoint] Loaded from: {path}")

    def get_actor_weights(self) -> List[Tuple[np.ndarray, np.ndarray]]:
        """
        Get the actor network weights for ONNX export.
        Returns list of (weight, bias) tuples for layers:
          [trunk_w0, trunk_b0, trunk_w1, trunk_b1, mean_w0, mean_b0]
        where the first two are trunk layers with tanh, last is mean head.
        """
        layers = []
        # Trunk layer 0
        layers.append((self.trunk.weights[0], self.trunk.biases[0]))
        # Trunk layer 1
        layers.append((self.trunk.weights[1], self.trunk.biases[1]))
        # Mean head
        layers.append((self.mean_head.weights[0], self.mean_head.biases[0]))
        return layers


# ══════════════════════════════════════════════════════════════════════════════
#  Rollout Buffer (with GAE)
# ══════════════════════════════════════════════════════════════════════════════

class RolloutBuffer:
    """
    Experience buffer for PPO with GAE advantage computation.

    Stores transitions and computes advantages/returns after rollout.
    """

    def __init__(self, buffer_size: int, obs_dim: int, act_dim: int,
                 gamma: float = 0.99, gae_lambda: float = 0.95):
        self.buffer_size = buffer_size
        self.gamma = gamma
        self.gae_lambda = gae_lambda

        self.observations = np.zeros((buffer_size, obs_dim), dtype=np.float32)
        self.actions = np.zeros((buffer_size, act_dim), dtype=np.float32)
        self.log_probs = np.zeros(buffer_size, dtype=np.float32)
        self.rewards = np.zeros(buffer_size, dtype=np.float32)
        self.dones = np.zeros(buffer_size, dtype=np.float32)
        self.values = np.zeros(buffer_size, dtype=np.float32)
        self.advantages = np.zeros(buffer_size, dtype=np.float32)
        self.returns = np.zeros(buffer_size, dtype=np.float32)

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
        self.observations[idx] = obs.astype(np.float32)
        self.actions[idx] = action.astype(np.float32)
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
                next_value = self.values[t + 1]
                next_non_terminal = 1.0 - self.dones[t + 1]

            delta = self.rewards[t] + self.gamma * next_value * next_non_terminal - self.values[t]
            gae = delta + self.gamma * self.gae_lambda * next_non_terminal * gae
            self.advantages[t] = gae
            self.returns[t] = gae + self.values[t]

    def get_available_count(self) -> int:
        """Return number of transitions stored."""
        return self.buffer_size if self.full else self.pos

    def iterate_minibatches(self, batch_size: int, num_epochs: int):
        """
        Generator yielding minibatches for multiple epochs.

        Yields dicts with keys: observations, actions, log_probs, advantages, returns
        """
        buffer_size = self.buffer_size if self.full else self.pos
        indices = np.arange(buffer_size)

        for _ in range(num_epochs):
            np.random.shuffle(indices)
            for start in range(0, buffer_size, batch_size):
                end = min(start + batch_size, buffer_size)
                batch_indices = indices[start:end]
                yield {
                    "observations": self.observations[batch_indices],
                    "actions": self.actions[batch_indices],
                    "log_probs": self.log_probs[batch_indices],
                    "advantages": self.advantages[batch_indices],
                    "returns": self.returns[batch_indices],
                }


# ══════════════════════════════════════════════════════════════════════════════
#  PPO Trainer
# ══════════════════════════════════════════════════════════════════════════════

class PPOTrainer:
    """
    Pure NumPy PPO trainer.

    Implements:
      - Clipped surrogate objective
      - Value function loss (MSE)
      - Entropy bonus
      - GAE for advantage estimation
      - Mini-batch updates with multiple epochs
    """

    def __init__(
        self,
        obs_dim: int,
        act_dim: int,
        hidden_sizes: Tuple[int, ...] = (64, 64),
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
    ):
        self.obs_dim = obs_dim
        self.act_dim = act_dim

        # Model
        self.model = ActorCritic(obs_dim, act_dim, hidden_sizes)

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

        # Buffer
        self.buffer = RolloutBuffer(
            buffer_size=n_steps,
            obs_dim=obs_dim,
            act_dim=act_dim,
            gamma=gamma,
            gae_lambda=gae_lambda,
        )

        # Adam optimizer state (per-parameter)
        self._adam_state = {}
        self._init_adam()

        # Tracking
        self.global_step = 0
        self.epoch = 0
        self.best_reward = -float("inf")

    def _init_adam(self):
        """Initialize Adam optimizer state for all parameters."""
        for name, param in self._iter_params():
            self._adam_state[name] = {
                "m": np.zeros_like(param),
                "v": np.zeros_like(param),
                "t": 0,
            }

    def _iter_params(self):
        """Iterate over all trainable parameters with their names."""
        params = self.model.get_params()
        for name, param in params.items():
            yield name, param

    def _adam_update(self, grads: Dict[str, np.ndarray], lr: float, beta1: float = 0.9,
                     beta2: float = 0.999, eps: float = 1e-8):
        """Apply Adam update to model parameters."""
        params = self.model.get_params()
        for name in params:
            if name in grads and name in self._adam_state:
                state = self._adam_state[name]
                state["t"] += 1
                state["m"] = beta1 * state["m"] + (1.0 - beta1) * grads[name]
                state["v"] = beta2 * state["v"] + (1.0 - beta2) * (grads[name] ** 2)

                m_hat = state["m"] / (1.0 - beta1 ** state["t"])
                v_hat = state["v"] / (1.0 - beta2 ** state["t"])

                params[name] -= lr * m_hat / (np.sqrt(v_hat) + eps)

        self.model.set_params(params)

    def _compute_gae(self, rewards: np.ndarray, values: np.ndarray,
                     dones: np.ndarray, last_value: float, last_done: bool) -> np.ndarray:
        """Compute GAE advantages."""
        n = len(rewards)
        advantages = np.zeros(n, dtype=np.float32)
        gae = 0.0
        for t in reversed(range(n)):
            if t == n - 1:
                next_val = last_value if not last_done else 0.0
                next_non_term = 0.0 if last_done else 1.0
            else:
                next_val = values[t + 1]
                next_non_term = 1.0 - dones[t + 1]
            delta = rewards[t] + self.gamma * next_val * next_non_term - values[t]
            gae = delta + self.gamma * self.gae_lambda * next_non_term * gae
            advantages[t] = gae
        return advantages

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

        for step in range(self.n_steps):
            # Get action
            obs_flat = obs.flatten()
            action_mean, log_std, value = self.model.forward(obs_flat.reshape(1, -1))
            action_mean = action_mean[0]
            value = value[0]
            std = np.exp(np.clip(log_std, -5.0, 2.0))

            # Sample action
            action = action_mean + np.random.randn(self.act_dim) * std

            # Compute log prob
            log_prob = -0.5 * np.sum(
                ((action - action_mean) ** 2) / (std ** 2) + np.log(2.0 * math.pi * std ** 2)
            )

            # Step environment
            next_obs, reward, terminated, truncated, info = env.step(action)
            done = terminated or truncated

            # Store
            self.buffer.store(obs_flat, action, log_prob, reward, done, value)

            total_reward += reward
            episode_reward += reward
            obs = next_obs

            if done:
                obs, _ = env.reset()
                episode_count += 1
                episode_reward = 0.0

        # Compute advantages
        obs_flat = obs.flatten()
        last_value = self.model.get_value(obs_flat.reshape(1, -1))[0]
        self.buffer.compute_advantages(last_value, done)

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

        buffer_size = self.buffer.get_available_count()
        observations = self.buffer.observations[:buffer_size]
        actions = self.buffer.actions[:buffer_size]
        old_log_probs = self.buffer.log_probs[:buffer_size]
        advantages = self.buffer.advantages[:buffer_size]
        returns = self.buffer.returns[:buffer_size]

        # Normalize advantages
        if self.normalize_advantages and len(advantages) > 1:
            advantages = (advantages - advantages.mean()) / (advantages.std() + 1e-8)

        # Track metrics
        total_policy_loss = 0.0
        total_value_loss = 0.0
        total_entropy = 0.0
        total_approx_kl = 0.0
        n_batches = 0

        # Mini-batch training
        indices = np.arange(buffer_size)
        for _ in range(self.mini_epochs):
            np.random.shuffle(indices)
            for start in range(0, buffer_size, self.batch_size):
                end = min(start + self.batch_size, buffer_size)
                batch_idx = indices[start:end]

                batch_obs = observations[batch_idx]
                batch_act = actions[batch_idx]
                batch_old_log = old_log_probs[batch_idx]
                batch_adv = advantages[batch_idx]
                batch_ret = returns[batch_idx]

                # Forward pass
                values, log_probs, entropy, _ = self.model.evaluate(batch_obs, batch_act)

                # Policy loss (clipped surrogate)
                ratio = np.exp(log_probs - batch_old_log)
                surr1 = ratio * batch_adv
                surr2 = np.clip(ratio, 1.0 - self.clip_epsilon, 1.0 + self.clip_epsilon) * batch_adv
                policy_loss = -np.mean(np.minimum(surr1, surr2))

                # Value loss (MSE)
                value_loss = np.mean((values - batch_ret) ** 2)

                # Entropy bonus
                entropy_mean = np.mean(entropy)
                entropy_loss = -current_entropy_coef * entropy_mean

                # Total loss
                total_loss = policy_loss + self.value_loss_coef * value_loss + entropy_loss

                # Compute gradients via finite differences approximation
                # We use the analytical gradients computed manually
                grads = self._compute_gradients(
                    batch_obs, batch_act, batch_old_log, batch_adv, batch_ret,
                    values, log_probs, ratio, self.clip_epsilon,
                    self.value_loss_coef, current_entropy_coef
                )

                # Apply gradient clipping (global norm)
                total_norm = math.sqrt(sum(np.sum(g ** 2) for g in grads.values()))
                if total_norm > self.max_grad_norm:
                    scale = self.max_grad_norm / (total_norm + 1e-8)
                    for k in grads:
                        grads[k] *= scale

                # Update actor with actor_lr
                actor_grads = {k: v for k, v in grads.items()
                              if not k.startswith("critic_")}
                self._adam_update(actor_grads, current_actor_lr)

                # Update critic with critic_lr
                critic_grads = {k: v for k, v in grads.items()
                               if k.startswith("critic_")}
                self._adam_update(critic_grads, current_critic_lr)

                # Track metrics
                total_policy_loss += policy_loss
                total_value_loss += value_loss
                total_entropy += entropy_mean
                with np.errstate(all='ignore'):
                    kl = np.mean(np.log(ratio.clip(1e-10, None)))
                    total_approx_kl += kl if np.isfinite(kl) else 0.0
                n_batches += 1

        self.global_step += 1

        return {
            "policy_loss": total_policy_loss / max(n_batches, 1),
            "value_loss": total_value_loss / max(n_batches, 1),
            "entropy": total_entropy / max(n_batches, 1),
            "approx_kl": total_approx_kl / max(n_batches, 1),
            "actor_lr": current_actor_lr,
            "critic_lr": current_critic_lr,
            "entropy_coef": current_entropy_coef,
        }

    def _compute_gradients(
        self, obs: np.ndarray, actions: np.ndarray, old_log_probs: np.ndarray,
        advantages: np.ndarray, returns: np.ndarray,
        values: np.ndarray, log_probs: np.ndarray, ratio: np.ndarray,
        clip_epsilon: float, vf_coef: float, entropy_coef: float
    ) -> Dict[str, np.ndarray]:
        """
        Compute analytical gradients for PPO loss.

        This is a simplified gradient computation. For a production system,
        autodiff would be preferred, but this works for our purposes.
        """
        n = len(obs)
        grads = {}

        # We compute gradients by backpropagating through the network
        # For simplicity, we use a numerical approximation approach
        # combined with analytical gradient formulas

        # --- Critic gradients ---
        # Value loss: L = mean((V - returns)^2)
        # dV = 2 * (V - returns) / n
        d_values = 2.0 * (values - returns) / n

        # Backprop through critic head
        features = self.model.trunk.forward(obs)
        d_critic_w = features.T @ d_values.reshape(-1, 1)
        d_critic_b = d_values.sum()
        d_features_from_critic = d_values.reshape(-1, 1) @ self.model.critic_head.weights[0].T

        # --- Actor gradients ---
        # Policy loss: L = -mean(min(ratio*adv, clip(ratio)*adv))
        # where ratio = exp(log_prob - old_log_prob)
        # dL/dratio = -mean(adv) if ratio*adv < clip(ratio)*adv
        # For the clipped surrogate:
        #   if adv > 0: use min(ratio, 1+eps) -> gradient = adv if ratio < 1+eps else 0
        #   if adv < 0: use max(ratio, 1-eps) -> gradient = adv if ratio > 1-eps else 0
        surr1 = ratio * advantages
        surr2 = np.clip(ratio, 1.0 - clip_epsilon, 1.0 + clip_epsilon) * advantages
        use_first = surr1 < surr2  # which one is the minimum

        # dL/dratio
        d_ratio = np.where(use_first, -advantages, 0.0) / n

        # dL/dlog_prob = dL/dratio * dratio/dlog_prob = d_ratio * ratio
        d_log_prob = d_ratio * ratio

        # Entropy gradient: d(entropy)/d(log_std)
        # entropy = 0.5 * sum(log(2*pi*e*sigma^2)) = 0.5 * sum(1 + log(2*pi) + 2*log_std)
        # d(entropy)/d(log_std) = 1.0
        # But we minimize -entropy_coef * entropy, so gradient = -entropy_coef * 1
        d_entropy = -entropy_coef / n

        # For log_prob: log N(x|mu,sigma) = -0.5 * sum((x-mu)^2/sigma^2 + log(2*pi*sigma^2))
        # d(log_prob)/d(mu) = (x-mu)/sigma^2
        # d(log_prob)/d(log_std) = -1 + (x-mu)^2/sigma^2
        action_mean = self.model.mean_head.forward(features)
        log_std = np.clip(self.model.log_std, -5.0, 2.0)
        std = np.exp(log_std)
        var = std ** 2

        # Combined gradient w.r.t. action_mean
        d_mu_combined = np.zeros_like(action_mean)
        for i in range(self.act_dim):
            d_mu_combined[:, i] = d_log_prob * (actions[:, i] - action_mean[:, i]) / var[i]

        # Gradient w.r.t. log_std
        d_log_std = np.zeros(self.act_dim, dtype=np.float32)
        for i in range(self.act_dim):
            d_log_std[i] = np.mean(
                d_log_prob * (-1.0 + (actions[:, i] - action_mean[:, i]) ** 2 / var[i])
            ) + d_entropy

        # Backprop through mean head
        d_mean_w = features.T @ d_mu_combined
        d_mean_b = d_mu_combined.sum(axis=0)

        # Backprop through trunk (from both mean head and critic head)
        d_features = d_mu_combined @ self.model.mean_head.weights[0].T
        d_features += d_features_from_critic

        # Trunk layer 1 (tanh, then linear)
        # features = tanh(h1), where h1 = h0 @ w1 + b1
        # h0 = tanh(obs @ w0 + b0)
        # For trunk layer 1: h1 = h0 @ w1 + b1, features = tanh(h1)
        h0 = np.tanh(obs @ self.model.trunk.weights[0] + self.model.trunk.biases[0])
        h1 = h0 @ self.model.trunk.weights[1] + self.model.trunk.biases[1]
        # d_features/d_h1 = d(tanh)/dh1 = 1 - tanh^2 = 1 - features^2
        d_h1 = d_features * (1.0 - features ** 2)

        d_trunk_w1 = h0.T @ d_h1
        d_trunk_b1 = d_h1.sum(axis=0)

        # Trunk layer 0 (tanh, then linear)
        d_h0 = d_h1 @ self.model.trunk.weights[1].T
        # h0 = tanh(obs @ w0 + b0)
        d_pre_act = d_h0 * (1.0 - h0 ** 2)
        d_trunk_w0 = obs.T @ d_pre_act
        d_trunk_b0 = d_pre_act.sum(axis=0)

        # Collect gradients
        grads["trunk_w0"] = d_trunk_w0
        grads["trunk_b0"] = d_trunk_b0
        grads["trunk_w1"] = d_trunk_w1
        grads["trunk_b1"] = d_trunk_b1
        grads["mean_w0"] = d_mean_w
        grads["mean_b0"] = d_mean_b
        grads["log_std"] = d_log_std
        grads["critic_w0"] = d_critic_w
        grads["critic_b0"] = d_critic_b.flatten()

        return grads

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
        metrics["total_reward"] = total_reward
        metrics["episode_count"] = max(episode_count, 1)

        return metrics

    def save(self, path: str):
        """Save full trainer checkpoint."""
        self.model.save_checkpoint(path)

    def load(self, path: str):
        """Load trainer checkpoint."""
        self.model.load_checkpoint(path)