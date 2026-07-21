"""
Simple simulated environment for Neural Animation PPO training.

This environment mimics the animation task with a kinematic chain proxy:
- Takes action tensor as input (joint angle targets)
- Simulates a simple physics proxy (kinematic chain with PD control)
- Computes reward based on: velocity tracking, energy efficiency,
  smoothness, ground contact, and pose consistency
- Provides observation space matching the Unity observation encoding

The environment follows the Gymnasium (Gym v0.26+) API.
"""

import math
from typing import Tuple, Dict, Optional, Any

import numpy as np

from config import Config, EnvConfig, AvatarSpec


# ──────────────────────────────────────────────────────────────────────────────
#  Simple Kinematic Chain Physics Proxy
# ──────────────────────────────────────────────────────────────────────────────

class KinematicChain:
    """
    A simple kinematic chain simulating a character's skeleton.

    Each joint has a position, velocity, and target position.
    Uses PD control to track target positions.
    """

    def __init__(self, num_joints: int, dt: float = 0.02):
        self.num_joints = num_joints
        self.dt = dt

        # Joint states (angles, velocities)
        self.angles = np.zeros(num_joints, dtype=np.float32)
        self.velocities = np.zeros(num_joints, dtype=np.float32)

        # Target angles (from action)
        self.target_angles = np.zeros(num_joints, dtype=np.float32)

        # PD gains
        self.kp = 50.0  # Proportional gain
        self.kd = 5.0   # Derivative gain

        # Joint limits (± radians)
        self.limits = np.full(num_joints, np.pi * 0.5, dtype=np.float32)

        # Random default pose (rest angles)
        self.default_pose = np.zeros(num_joints, dtype=np.float32)
        for i in range(num_joints):
            # Create a natural-looking default pose
            self.default_pose[i] = 0.05 * np.sin(i * 1.7)

        # Root state (position, velocity, rotation)
        self.root_pos = np.zeros(3, dtype=np.float32)
        self.root_vel = np.zeros(3, dtype=np.float32)
        self.root_rot = np.zeros(3, dtype=np.float32)  # euler angles
        self.root_ang_vel = np.zeros(3, dtype=np.float32)

    def reset(self):
        """Reset the kinematic chain to default pose."""
        self.angles = self.default_pose.copy()
        self.velocities = np.zeros(self.num_joints, dtype=np.float32)
        self.target_angles = self.default_pose.copy()
        self.root_pos = np.zeros(3, dtype=np.float32)
        self.root_vel = np.zeros(3, dtype=np.float32)
        self.root_rot = np.zeros(3, dtype=np.float32)
        self.root_ang_vel = np.zeros(3, dtype=np.float32)
        return self._get_joint_obs()

    def step(self, action: np.ndarray, dt: float):
        """
        Step the physics simulation with PD control.

        Args:
            action: Joint angle target commands (normalized [-1, 1]).
            dt: Timestep in seconds.
        """
        # Map action to target angles (scale by joint limits)
        # Action is normalized [-1, 1]; scale to joint limits
        num_action = min(len(action), self.num_joints)
        self.target_angles[:num_action] = action[:num_action] * self.limits[:num_action]

        # PD control: torque = kp * (target - current) - kd * velocity
        error = self.target_angles - self.angles
        torques = self.kp * error - self.kd * self.velocities

        # Simple Euler integration
        self.velocities += torques * dt
        # Damping
        self.velocities *= 0.99
        self.angles += self.velocities * dt

        # Clamp joint positions
        self.angles = np.clip(self.angles, -self.limits, self.limits)

    def _get_joint_obs(self) -> np.ndarray:
        """Return flattened joint observation: positions + velocities."""
        return np.concatenate([self.angles, self.velocities]).astype(np.float32)


# ──────────────────────────────────────────────────────────────────────────────
#  Simple Box Space (gymnasium-free)
# ──────────────────────────────────────────────────────────────────────────────

class _Box:
    """Minimal Box space compatible with Gymnasium's API, without requiring gymnasium install."""

    def __init__(self, low: float, high: float, shape: Tuple[int, ...], dtype: type):
        self.low = np.full(shape, low, dtype=dtype)
        self.high = np.full(shape, high, dtype=dtype)
        self.shape = shape
        self.dtype = dtype


# ──────────────────────────────────────────────────────────────────────────────
#  Simple Animation Environment
# ──────────────────────────────────────────────────────────────────────────────

class SimpleAnimationEnv:
    """
    Gymnasium-compatible simulated environment for Neural Animation training.

    Observation space layout (matching Unity's ObservationEncoder):
    - Joint positions (joint_count * 3 floats)
    - Joint velocities (joint_count * 3 floats)
    - Root velocity (3 floats)
    - Root angular velocity (3 floats)
    - Ground contact flags (2 floats)
    - Target direction (3 floats)
    - Target velocity (1 float)
    - Terrain heightmap (terrain_resolution^2 floats)
    - Style embedding (style_embedding_size floats)

    Action space:
    - Joint target angles (action_size floats normalized to [-1, 1])

    Reward components:
    - Velocity tracking
    - Energy efficiency
    - Smoothness
    - Ground contact
    - Pose consistency
    """

    def __init__(self, cfg: Config):
        self.cfg = cfg.env
        self.avatar_spec = AvatarSpec.from_name(cfg.avatar)
        self.obs_dim = cfg.obs_dim
        self.act_dim = cfg.act_dim
        self.joint_count = cfg.joint_count
        self.dt = cfg.env.dt

        # Kinematic chain
        self.chain = KinematicChain(self.act_dim, self.dt)

        # Target velocity (changes periodically)
        self.target_velocity = np.zeros(3, dtype=np.float32)
        self.target_speed = 0.0
        self.target_change_timer = 0
        self.target_change_interval = 200  # steps

        # Episode tracking
        self.step_count = 0
        self.max_episode_length = cfg.env.max_episode_length

        # Previous action for smoothness reward
        self.prev_action = np.zeros(self.act_dim, dtype=np.float32)

        # Terrain heightmap placeholder
        self.terrain_resolution = 11
        self._terrain_map = np.zeros((self.terrain_resolution, self.terrain_resolution), dtype=np.float32)

        # Style embedding placeholder
        self.style_embedding_size = 8
        self.style_embedding = np.zeros(self.style_embedding_size, dtype=np.float32)

        # Observation & action space definition
        self.observation_space = _Box(
            low=-5.0, high=5.0, shape=(self.obs_dim,), dtype=np.float32
        )
        self.action_space = _Box(
            low=-1.0, high=1.0, shape=(self.act_dim,), dtype=np.float32
        )

        # For Gymnasium compatibility
        self.metadata = {"render_modes": []}

    def reset(self, seed: Optional[int] = None, options: Optional[dict] = None) -> Tuple[np.ndarray, dict]:
        """
        Reset the environment to initial state.

        Returns: (observation, info_dict)
        """
        if seed is not None:
            np.random.seed(seed)

        self.chain.reset()
        self.step_count = 0
        self.prev_action = np.zeros(self.act_dim, dtype=np.float32)

        # Random target velocity
        self._sample_target_velocity()
        self.target_change_timer = 0

        # Random style embedding
        self.style_embedding = np.random.randn(self.style_embedding_size).astype(np.float32) * 0.1

        # Random terrain
        self._generate_terrain()

        obs = self._encode_observation()
        return obs, {}

    def step(self, action: np.ndarray) -> Tuple[np.ndarray, float, bool, bool, dict]:
        """
        Step the environment.

        Args:
            action: Action array of shape (act_dim,), values in [-1, 1].

        Returns: (observation, reward, terminated, truncated, info)
        """
        # Clip action to valid range
        action = np.clip(action, -1.0, 1.0).astype(np.float32)

        # Add action noise (exploration)
        if self.cfg.action_noise > 0:
            action += np.random.randn(self.act_dim).astype(np.float32) * self.cfg.action_noise
            action = np.clip(action, -1.0, 1.0)

        # Physics step
        self.chain.step(action, self.dt)

        # Root motion (simplified — forward velocity from joint motion)
        root_forward = self._compute_root_velocity(action)
        self.chain.root_vel = root_forward * 0.5 + self.chain.root_vel * 0.5  # smooth
        self.chain.root_pos += self.chain.root_vel * self.dt

        # Root rotation (simplified)
        turn_amount = float(np.mean(action[0:3])) * 0.1  # subtle turning from first few joints
        self.chain.root_ang_vel = np.array([0.0, turn_amount, 0.0], dtype=np.float32)
        self.chain.root_rot += self.chain.root_ang_vel * self.dt

        # Compute reward
        reward = self._compute_reward(action)

        # Update target velocity periodically
        self.target_change_timer += 1
        if self.target_change_timer >= self.target_change_interval:
            self._sample_target_velocity()
            self.target_change_timer = 0

        # Build observation
        obs = self._encode_observation()

        # Episode termination
        self.step_count += 1
        terminated = False
        truncated = self.step_count >= self.max_episode_length

        # Info
        info = {
            "target_speed": self.target_speed,
            "root_velocity": np.linalg.norm(self.chain.root_vel),
            "step_count": self.step_count,
        }

        self.prev_action = action.copy()

        return obs, reward, terminated, truncated, info

    def render(self):
        """No-op render (not implemented for this simulated environment)."""
        pass

    def close(self):
        """Cleanup."""
        pass

    # ── Private helpers ──────────────────────────────────────────────────────

    def _sample_target_velocity(self):
        """Sample a new target velocity for the character."""
        min_speed, max_speed = self.cfg.target_velocity_range
        self.target_speed = np.random.uniform(min_speed, max_speed)
        # Random direction in XZ plane
        angle = np.random.uniform(0, 2 * np.pi)
        self.target_velocity = np.array([
            math.cos(angle) * self.target_speed,
            0.0,
            math.sin(angle) * self.target_speed,
        ], dtype=np.float32)

    def _generate_terrain(self):
        """Generate a simple heightmap terrain."""
        res = self.terrain_resolution
        self._terrain_map = np.random.randn(res, res).astype(np.float32) * 0.05
        # Smooth with gaussian-like pattern
        for i in range(res):
            for j in range(res):
                cx, cy = res // 2, res // 2
                dist = math.sqrt((i - cx) ** 2 + (j - cy) ** 2)
                self._terrain_map[i, j] += 0.1 * math.exp(-dist / (res * 0.3))

    def _compute_root_velocity(self, action: np.ndarray) -> np.ndarray:
        """
        Compute root velocity from joint action.

        Simplified model: aggregate joint motion into forward velocity.
        """
        # Average joint velocity magnitude as a proxy for "effort"
        joint_effort = np.mean(np.abs(action - self.chain.angles / self.chain.limits))
        # Forward velocity proportional to effort and target alignment
        forward_vel = joint_effort * self.target_speed * 0.3

        # Direction from target
        target_dir = self.target_velocity.copy()
        target_norm = np.linalg.norm(target_dir)
        if target_norm > 1e-6:
            target_dir = target_dir / target_norm

        # Apply noise
        forward_vel += np.random.randn() * 0.02

        return target_dir * forward_vel

    def _compute_reward(self, action: np.ndarray) -> float:
        """
        Compute reward signal.

        Components:
        1. Velocity tracking: match target velocity
        2. Energy efficiency: penalize large joint movements
        3. Smoothness: penalize jerky actions
        4. Ground contact: simple foot contact reward
        5. Pose consistency: stay near default pose
        """
        reward = 0.0

        # 1. Velocity tracking reward
        current_vel = self.chain.root_vel
        target_vel = self.target_velocity
        vel_error = np.linalg.norm(current_vel - target_vel)
        vel_reward = math.exp(-vel_error * 0.5)  # Gaussian-like reward
        reward += self.cfg.reward_velocity_weight * vel_reward

        # 2. Energy efficiency (penalize large actions)
        action_magnitude = np.mean(action ** 2)
        energy_penalty = -action_magnitude
        reward += self.cfg.reward_energy_weight * energy_penalty

        # 3. Smoothness (penalize large changes in action)
        action_delta = np.mean((action - self.prev_action) ** 2)
        smoothness_penalty = -action_delta
        reward += self.cfg.reward_smoothness_weight * smoothness_penalty

        # 4. Ground contact (simple: reward alternating foot contact)
        # Simulate foot contact based on phase
        phase = (self.step_count % 30) / 30.0
        left_contact = 1.0 if phase < 0.5 else 0.0
        right_contact = 1.0 if phase >= 0.5 else 0.0
        contact_reward = (left_contact + right_contact) * 0.5
        reward += self.cfg.reward_contact_weight * contact_reward

        # 5. Pose consistency (stay near default pose)
        pose_error = np.mean((self.chain.angles - self.chain.default_pose) ** 2)
        pose_reward = math.exp(-pose_error)
        reward += self.cfg.reward_pose_weight * pose_reward

        return reward

    def _encode_observation(self) -> np.ndarray:
        """
        Encode the full observation vector matching Unity's ObservationEncoder.

        Layout:
        [0:joint_count*3]:      Joint positions (3 per joint: sin, cos, normalized)
        [joint_count*3:joint_count*6]:  Joint velocities (3 per joint)
        [joint_count*6:joint_count*6+3]: Root velocity
        [joint_count*6+3:joint_count*6+6]: Root angular velocity
        [joint_count*6+6:joint_count*6+8]: Ground contact flags (2)
        [joint_count*6+8:joint_count*6+11]: Target direction (3)
        [joint_count*6+11]:     Target speed (1)
        [joint_count*6+12:joint_count*6+12+terrain_res^2]: Terrain heightmap
        [remaining:]:           Style embedding
        """
        obs = np.zeros(self.obs_dim, dtype=np.float32)
        idx = 0

        # 1. Joint positions (3 per joint: sin, cos, normalized angle)
        for j in range(self.act_dim):
            angle = self.chain.angles[j]
            limit = self.chain.limits[j]
            if idx < self.obs_dim:
                obs[idx] = math.sin(angle)
                idx += 1
            if idx < self.obs_dim:
                obs[idx] = math.cos(angle)
                idx += 1
            if idx < self.obs_dim:
                obs[idx] = angle / max(limit, 1e-6)  # normalized [-1, 1]
                idx += 1
        # Pad remaining joint slots if act_dim < joint_count * 3 / 3
        # (joint_count * 3 positions expected, but we have act_dim joints)
        joint_positions_end = min(self.joint_count * 3, self.obs_dim)
        idx = max(idx, 0)
        # If we have fewer joints than expected, fill with zeros
        expected_joint_positions = self.joint_count * 3
        if idx < expected_joint_positions and idx < self.obs_dim:
            # Pad with zeros for remaining joints
            pass  # already zeros

        # 2. Joint velocities
        idx = self.joint_count * 3
        for j in range(self.act_dim):
            if idx < self.obs_dim:
                obs[idx] = self.chain.velocities[j] * 0.1  # scale down
                idx += 1
        # Pad (velocity values smaller than position block)
        idx = max(idx, self.joint_count * 3 + self.act_dim)

        # 3. Root velocity
        if idx < self.obs_dim:
            obs[idx] = self.chain.root_vel[0] * 0.1
            idx += 1
        if idx < self.obs_dim:
            obs[idx] = self.chain.root_vel[1] * 0.1
            idx += 1
        if idx < self.obs_dim:
            obs[idx] = self.chain.root_vel[2] * 0.1
            idx += 1

        # 4. Root angular velocity
        if idx < self.obs_dim:
            obs[idx] = self.chain.root_ang_vel[0] * 0.01
            idx += 1
        if idx < self.obs_dim:
            obs[idx] = self.chain.root_ang_vel[1] * 0.01
            idx += 1
        if idx < self.obs_dim:
            obs[idx] = self.chain.root_ang_vel[2] * 0.01
            idx += 1

        # 5. Ground contact flags (2)
        phase = (self.step_count % 30) / 30.0
        if idx < self.obs_dim:
            obs[idx] = 1.0 if phase < 0.5 else 0.0
            idx += 1
        if idx < self.obs_dim:
            obs[idx] = 1.0 if phase >= 0.5 else 0.0
            idx += 1

        # 6. Target direction (3)
        target_norm = np.linalg.norm(self.target_velocity)
        if target_norm > 1e-6:
            target_dir = self.target_velocity / target_norm
        else:
            target_dir = np.zeros(3)
        if idx < self.obs_dim:
            obs[idx] = target_dir[0]
            idx += 1
        if idx < self.obs_dim:
            obs[idx] = target_dir[1]
            idx += 1
        if idx < self.obs_dim:
            obs[idx] = target_dir[2]
            idx += 1

        # 7. Target speed (1)
        if idx < self.obs_dim:
            obs[idx] = self.target_speed * 0.1  # scale
            idx += 1

        # 8. Terrain heightmap (terrain_resolution^2)
        terrain_flat = self._terrain_map.flatten()
        for t in range(len(terrain_flat)):
            if idx < self.obs_dim:
                obs[idx] = terrain_flat[t]
                idx += 1

        # 9. Style embedding (remaining space)
        for s in range(self.style_embedding_size):
            if idx < self.obs_dim:
                obs[idx] = self.style_embedding[s]
                idx += 1

        # Add observation noise
        if self.cfg.observation_noise > 0:
            noise = np.random.randn(self.obs_dim).astype(np.float32) * self.cfg.observation_noise
            obs += noise

        # Normalize observation to reasonable range
        obs = np.clip(obs, -5.0, 5.0)

        return obs


# ──────────────────────────────────────────────────────────────────────────────
#  Convenience factory
# ──────────────────────────────────────────────────────────────────────────────

def create_env(cfg: Config) -> SimpleAnimationEnv:
    """Create a SimpleAnimationEnv from a Config."""
    return SimpleAnimationEnv(cfg)