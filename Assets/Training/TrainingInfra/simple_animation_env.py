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
import random
from typing import Tuple, Dict, Optional, Any, Literal

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

    Policy types:
    - "locomotion": Velocity tracking, energy efficiency, smoothness, ground contact, pose consistency
    - "combat": Attack accuracy, damage, dodge, stamina management, target facing
    - "react": Hit reaction quality, knockback/stun/death animation quality, recovery speed
    - "interact": Interaction pose accuracy, naturalness, timing, object alignment
    - "fly": Aerial maneuvering, altitude control, banking turns
    - "swim": Underwater propulsion, buoyancy, streamlined motion
    """

    def __init__(self, cfg: Config, policy_type: Literal["locomotion", "combat", "react", "interact", "fly", "swim", "mount", "climb", "run", "crouch", "large_monster"] = "locomotion"):
        self.cfg = cfg.env
        self.avatar_spec = AvatarSpec.from_name(cfg.avatar)
        self.obs_dim = cfg.obs_dim
        self.act_dim = cfg.act_dim
        self.joint_count = cfg.joint_count
        self.dt = cfg.env.dt
        self.policy_type = policy_type

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
        
        # Curriculum learning
        self._curriculum_enabled = False
        self._curriculum_phase = 0  # 0=easy, 1=medium, 2=hard

        # Combat-specific state
        self.combat_target_pos = np.zeros(3, dtype=np.float32)
        self.combat_attack_cooldown = 0
        self.combat_stamina = 1.0
        self.combat_last_hit_time = -100
        self.combat_hit_streak = 0
        self.combat_target_distance = 5.0

        # React-specific state
        self.react_hit_intensity = 0.0
        self.react_hit_direction = np.zeros(3, dtype=np.float32)
        self.react_recovery_timer = 0
        self.react_is_stunned = False
        self.react_is_knocked_down = False
        self.react_hit_type = "none"  # "light", "heavy", "launch", "stun", "knockdown"

        # Interact-specific state
        self.interact_target_pos = np.zeros(3, dtype=np.float32)
        self.interact_phase = 0.0  # 0: approach, 1: align, 2: interact, 3: retreat
        self.interact_timer = 0
        self.interact_object_type = "gather"  # "gather", "craft", "door", "lever"
        self.interact_success = False

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

        # Policy-specific initialization
        if self.policy_type == "locomotion":
            self._sample_target_velocity()
            self.target_change_timer = 0
        elif self.policy_type == "combat":
            self._reset_combat_state()
        elif self.policy_type == "react":
            self._reset_react_state()
        elif self.policy_type == "interact":
            self._reset_interact_state()

        # Random style embedding
        self.style_embedding = np.random.randn(self.style_embedding_size).astype(np.float32) * 0.1

        # Random terrain
        self._generate_terrain()

        obs = self._encode_observation()
        return obs, {}

    def _reset_combat_state(self):
        """Initialize combat-specific state."""
        # Random target position in front of character
        angle = np.random.uniform(-np.pi/3, np.pi/3)
        distance = np.random.uniform(3.0, 8.0)
        self.combat_target_pos = np.array([
            math.cos(angle) * distance,
            0.0,
            math.sin(angle) * distance
        ], dtype=np.float32)
        self.combat_target_distance = distance
        self.combat_attack_cooldown = 0
        self.combat_stamina = 1.0
        self.combat_last_hit_time = -100
        self.combat_hit_streak = 0

    def _reset_react_state(self):
        """Initialize react-specific state."""
        self.react_hit_intensity = 0.0
        self.react_hit_direction = np.zeros(3, dtype=np.float32)
        self.react_recovery_timer = 0
        self.react_is_stunned = False
        self.react_is_knocked_down = False
        self.react_hit_type = "none"
        # Randomly trigger a hit at the start of some episodes
        if np.random.random() < 0.3:
            self._trigger_react_hit()

    def _reset_interact_state(self):
        """Initialize interact-specific state."""
        # Random interaction target position
        angle = np.random.uniform(0, 2 * np.pi)
        distance = np.random.uniform(1.0, 2.5)
        self.interact_target_pos = np.array([
            math.cos(angle) * distance,
            0.0,
            math.sin(angle) * distance
        ], dtype=np.float32)
        self.interact_phase = 0.0
        self.interact_timer = 0
        self.interact_object_type = np.random.choice(["gather", "craft", "door", "lever"])
        self.interact_success = False

    def _reset_fly_swim_state(self):
        """Initialize fly/swim-specific state."""
        # 3D target position (air/water)
        angle = np.random.uniform(0, 2 * np.pi)
        distance = np.random.uniform(3.0, 10.0)
        height = np.random.uniform(2.0, 8.0)
        self.fly_swim_target_pos = np.array([
            math.cos(angle) * distance,
            height,
            math.sin(angle) * distance
        ], dtype=np.float32)
        self.fly_swim_speed = np.random.uniform(2.0, 5.0)
        self.fly_swim_bank_angle = 0.0

    def _reset_mount_state(self):
        """Initialize mount (horse riding) state."""
        # Mount target velocity (faster than locomotion)
        angle = np.random.uniform(-np.pi/4, np.pi/4)
        distance = np.random.uniform(5.0, 15.0)
        self.mount_target_pos = np.array([
            math.cos(angle) * distance,
            0.0,
            math.sin(angle) * distance
        ], dtype=np.float32)
        self.mount_target_speed = np.random.uniform(3.0, 8.0)
        self.mount_stamina = 1.0

    def _reset_climb_state(self):
        """Initialize climb state."""
        # Climbing target (vertical surface)
        angle = np.random.uniform(-np.pi/6, np.pi/6)
        distance = np.random.uniform(1.0, 3.0)
        self.climb_target_pos = np.array([
            math.cos(angle) * distance,
            np.random.uniform(0.5, 2.0),
            math.sin(angle) * distance
        ], dtype=np.float32)
        self.climb_progress = 0.0
        self.climb_stamina = 1.0

    def _reset_style_state(self):
        """Initialize run/crouch style state."""
        # Run: faster target velocity, Crouch: lower height
        angle = np.random.uniform(-np.pi/4, np.pi/4)
        distance = np.random.uniform(3.0, 10.0)
        self.style_target_pos = np.array([
            math.cos(angle) * distance,
            0.0 if "run" in self.policy_type else np.random.uniform(0.0, 0.5),
            math.sin(angle) * distance
        ], dtype=np.float32)
        self.style_target_speed = np.random.uniform(5.0, 12.0) if "run" in self.policy_type else np.random.uniform(0.5, 1.5)
        self.style_crouch_amount = 0.0 if "run" in self.policy_type else 1.0

    def _reset_large_monster_state(self):
        """Initialize large monster state."""
        # Large monster: slow but powerful, territory-based
        angle = np.random.uniform(0, 2 * np.pi)
        distance = np.random.uniform(5.0, 15.0)
        self.large_monster_target_pos = np.array([
            math.cos(angle) * distance,
            0.0,
            math.sin(angle) * distance
        ], dtype=np.float32)
        self.large_monster_territory_radius = 20.0
        self.large_monster_rage = 0.0
        self.large_monster_stamina = 1.0

    # ══════════════════════════════════════════════════════════════════════════════
    #  Curriculum & Style API
    # ═══════════════════════════════════════════════════════════════════════════════

    def set_curriculum_enabled(self, enabled: bool):
        """Enable/disable curriculum learning (easy -> medium -> hard terrain)."""
        self._curriculum_enabled = enabled
        if enabled:
            self._curriculum_phase = 0  # Start at easy
            self._generate_terrain()  # Regenerate with new difficulty

    def set_curriculum_phase(self, phase: int):
        """Set curriculum phase: 0=easy, 1=medium, 2=hard."""
        if self._curriculum_enabled:
            self._curriculum_phase = max(0, min(2, phase))
            self._generate_terrain()  # Regenerate with new difficulty

    def set_style_embedding(self, index: int):
        """Set style embedding index: 0=walk, 1=run, 2=crouch, 3=custom."""
        self.style_embedding = np.zeros(self.style_embedding_size, dtype=np.float32)
        if 0 <= index < self.style_embedding_size:
            self.style_embedding[index] = 1.0  # One-hot encoding

    def seed(self, seed: int):
        """Set random seed for reproducibility (Gymnasium compatibility)."""
        np.random.seed(seed)

    def _generate_terrain(self):
        """Generate terrain heightmap based on curriculum phase."""
        if not self._curriculum_enabled:
            # Default flat terrain
            for i in range(self.terrain_resolution):
                for j in range(self.terrain_resolution):
                    x = (i - self.terrain_resolution / 2) * 0.5
                    z = (j - self.terrain_resolution / 2) * 0.5
                    height = 0.1 * math.sin(x * 0.5) * math.cos(z * 0.5)
                    self._terrain_map[i, j] = height
            return

        # Curriculum-based terrain
        roughness = [0.1, 0.3, 0.6][self._curriculum_phase]  # easy, medium, hard
        obstacle_density = [0.05, 0.15, 0.3][self._curriculum_phase]
        
        for i in range(self.terrain_resolution):
            for j in range(self.terrain_resolution):
                x = (i - self.terrain_resolution / 2) * 0.5
                z = (j - self.terrain_resolution / 2) * 0.5
                
                # Base noise
                height = roughness * math.sin(x * 0.5) * math.cos(z * 0.5)
                
                # Add obstacles
                if np.random.random() < obstacle_density:
                    height += np.random.uniform(0.2, 0.5)
                
                self._terrain_map[i, j] = height

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

        # Policy-specific state updates
        if self.policy_type == "locomotion":
            self._update_locomotion_state()
        elif self.policy_type == "combat":
            self._update_combat_state(action)
        elif self.policy_type == "react":
            self._update_react_state(action)
        elif self.policy_type == "interact":
            self._update_interact_state(action)

        # Compute reward
        reward = self._compute_reward(action)

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
            "policy_type": self.policy_type,
        }

        self.prev_action = action.copy()

        return obs, reward, terminated, truncated, info

    def _update_locomotion_state(self):
        """Update locomotion-specific state."""
        # Update target velocity periodically
        self.target_change_timer += 1
        if self.target_change_timer >= self.target_change_interval:
            self._sample_target_velocity()
            self.target_change_timer = 0

    def _update_combat_state(self, action: np.ndarray):
        """Update combat-specific state."""
        # Update attack cooldown
        if self.combat_attack_cooldown > 0:
            self.combat_attack_cooldown -= 1

        # Regenerate stamina
        self.combat_stamina = min(1.0, self.combat_stamina + 0.01)

        # Update target distance
        to_target = self.combat_target_pos - self.chain.root_pos
        self.combat_target_distance = np.linalg.norm(to_target[::2])  # XZ distance

        # Check for attack (first action component as attack trigger)
        attack_triggered = action[0] > 0.5 and self.combat_attack_cooldown == 0 and self.combat_stamina > 0.2
        if attack_triggered:
            self.combat_attack_cooldown = 30  # 0.6s cooldown
            self.combat_stamina -= 0.15
            # Check hit (based on distance and facing)
            facing_target = self._is_facing_target()
            if self.combat_target_distance < 2.5 and facing_target:
                self.combat_last_hit_time = self.step_count
                self.combat_hit_streak += 1
            else:
                self.combat_hit_streak = 0

        # Move target occasionally
        if self.step_count % 300 == 0:
            self._reset_combat_state()

    def _update_react_state(self, action: np.ndarray):
        """Update react-specific state."""
        # Recovery timer
        if self.react_recovery_timer > 0:
            self.react_recovery_timer -= 1

        # State transitions
        if self.react_is_knocked_down:
            if self.react_recovery_timer <= 0:
                self.react_is_knocked_down = False
                self.react_recovery_timer = 20  # Getting up time
        elif self.react_is_stunned:
            if self.react_recovery_timer <= 0:
                self.react_is_stunned = False

        # Randomly trigger hits during episode
        if self.react_hit_intensity == 0.0 and np.random.random() < 0.005:
            self._trigger_react_hit()

    def _trigger_react_hit(self):
        """Trigger a hit reaction."""
        hit_type = np.random.choice(["light", "heavy", "launch", "stun", "knockdown"],
                                     p=[0.4, 0.25, 0.1, 0.15, 0.1])
        self.react_hit_type = hit_type

        if hit_type == "light":
            self.react_hit_intensity = np.random.uniform(0.3, 0.6)
            self.react_recovery_timer = np.random.randint(10, 20)
        elif hit_type == "heavy":
            self.react_hit_intensity = np.random.uniform(0.6, 0.9)
            self.react_recovery_timer = np.random.randint(20, 40)
        elif hit_type == "launch":
            self.react_hit_intensity = np.random.uniform(0.8, 1.0)
            self.react_recovery_timer = np.random.randint(40, 60)
        elif hit_type == "stun":
            self.react_hit_intensity = np.random.uniform(0.5, 0.8)
            self.react_is_stunned = True
            self.react_recovery_timer = np.random.randint(30, 50)
        elif hit_type == "knockdown":
            self.react_hit_intensity = 1.0
            self.react_is_knocked_down = True
            self.react_recovery_timer = np.random.randint(60, 100)

        # Random hit direction (from front/side/back)
        angle = np.random.uniform(0, 2 * np.pi)
        self.react_hit_direction = np.array([math.cos(angle), 0.0, math.sin(angle)], dtype=np.float32)

    def _update_interact_state(self, action: np.ndarray):
        """Update interact-specific state."""
        self.interact_timer += 1

        # Distance to interaction target
        to_target = self.interact_target_pos - self.chain.root_pos
        dist = np.linalg.norm(to_target[::2])  # XZ distance

        # Phase progression
        if self.interact_phase == 0.0:  # Approach
            if dist < 1.5:
                self.interact_phase = 1.0  # Align
                self.interact_timer = 0
        elif self.interact_phase == 1.0:  # Align
            facing_target = self._is_facing_interact_target()
            if facing_target and self.interact_timer > 10:
                self.interact_phase = 2.0  # Interact
                self.interact_timer = 0
        elif self.interact_phase == 2.0:  # Interact
            if self.interact_timer > 30:  # Interaction duration
                self.interact_success = True
                self.interact_phase = 3.0  # Retreat
                self.interact_timer = 0
        elif self.interact_phase == 3.0:  # Retreat
            if dist > 3.0 or self.interact_timer > 50:
                # Reset for next interaction
                self._reset_interact_state()

    def _update_fly_swim_state(self, action: np.ndarray):
        """Update fly/swim-specific state."""
        # Move towards 3D target
        to_target = self.fly_swim_target_pos - self.chain.root_pos
        dist = np.linalg.norm(to_target)
        
        if dist < 2.0:
            # Reached target, pick new one
            angle = np.random.uniform(0, 2 * np.pi)
            distance = np.random.uniform(5.0, 15.0)
            height = np.random.uniform(2.0, 10.0)
            self.fly_swim_target_pos = np.array([
                math.cos(angle) * distance,
                height,
                math.sin(angle) * distance
            ], dtype=np.float32)
            self.fly_swim_speed = np.random.uniform(2.0, 5.0)

        # Bank angle for turning (fly only)
        if self.policy_type == "fly":
            to_target_norm = to_target / max(dist, 1e-6)
            forward = np.array([math.sin(self.chain.root_rot[1]), 0.0, math.cos(self.chain.root_rot[1])], dtype=np.float32)
            target_dir = to_target_norm
            target_dir[1] = 0
            target_dir = target_dir / max(np.linalg.norm(target_dir), 1e-6)
            turn = np.cross(forward, target_dir)[1]
            self.fly_swim_bank_angle = np.clip(turn * 2.0, -1.0, 1.0)

    def _update_mount_state(self, action: np.ndarray):
        """Update mount (horse riding) state."""
        # Update mount stamina
        self.mount_stamina = min(1.0, self.mount_stamina + 0.001)
        
        # Check if target reached
        to_target = self.mount_target_pos - self.chain.root_pos
        to_target[1] = 0
        dist = np.linalg.norm(to_target)
        
        if dist < 3.0:
            # Pick new target
            angle = np.random.uniform(-np.pi/4, np.pi/4)
            distance = np.random.uniform(10.0, 20.0)
            self.mount_target_pos = np.array([
                math.cos(angle) * distance,
                0.0,
                math.sin(angle) * distance
            ], dtype=np.float32)
            self.mount_target_speed = np.random.uniform(5.0, 12.0)
        
        # Consume stamina when sprinting
        if np.linalg.norm(self.chain.root_vel) > 5.0:
            self.mount_stamina = max(0.0, self.mount_stamina - 0.005)

    def _update_climb_state(self, action: np.ndarray):
        """Update climb state."""
        # Update climb progress
        self.climb_progress = min(1.0, self.climb_progress + 0.01)
        self.climb_stamina = min(1.0, self.climb_stamina + 0.0005)
        
        # Check if reached top
        if self.climb_progress >= 1.0:
            # Pick new climb target
            angle = np.random.uniform(-np.pi/6, np.pi/6)
            distance = np.random.uniform(1.0, 3.0)
            self.climb_target_pos = np.array([
                math.cos(angle) * distance,
                np.random.uniform(0.5, 2.0),
                math.sin(angle) * distance
            ], dtype=np.float32)
            self.climb_progress = 0.0
            self.climb_stamina = 1.0

    def _update_style_state(self, action: np.ndarray):
        """Update run/crouch style state."""
        # Update style target
        to_target = self.style_target_pos - self.chain.root_pos
        to_target[1] = 0
        dist = np.linalg.norm(to_target)
        
        if dist < 2.0:
            # Pick new style target
            angle = np.random.uniform(-np.pi/4, np.pi/4)
            distance = np.random.uniform(5.0, 15.0)
            self.style_target_pos = np.array([
                math.cos(angle) * distance,
                0.0 if "run" in self.policy_type else np.random.uniform(0.0, 0.5),
                math.sin(angle) * distance
            ], dtype=np.float32)
            self.style_target_speed = np.random.uniform(5.0, 12.0) if "run" in self.policy_type else np.random.uniform(0.5, 1.5)
            self.style_crouch_amount = 0.0 if "run" in self.policy_type else 1.0

    def _update_large_monster_state(self, action: np.ndarray):
        """Update large monster state."""
        # Update rage and stamina
        self.large_monster_rage = min(1.0, self.large_monster_rage + 0.001)
        self.large_monster_stamina = min(1.0, self.large_monster_stamina + 0.0005)
        
        # Territory behavior
        to_target = self.large_monster_target_pos - self.chain.root_pos
        to_target[1] = 0
        dist = np.linalg.norm(to_target)
        
        if dist < 2.0 or np.random.random() < 0.001:
            # Pick new territory target
            angle = np.random.uniform(0, 2 * np.pi)
            distance = np.random.uniform(5.0, 15.0)
            self.large_monster_target_pos = np.array([
                math.cos(angle) * distance,
                0.0,
                math.sin(angle) * distance
            ], dtype=np.float32)
        
        # Rage increases when player nearby (simplified)
        # In real implementation, check player distance

    def _is_facing_target(self) -> bool:
        """Check if character is facing combat target."""
        forward = np.array([math.sin(self.chain.root_rot[1]), 0.0, math.cos(self.chain.root_rot[1])], dtype=np.float32)
        to_target = self.combat_target_pos - self.chain.root_pos
        to_target[1] = 0
        target_dist = np.linalg.norm(to_target)
        if target_dist < 1e-6:
            return True
        to_target = to_target / target_dist
        return np.dot(forward, to_target) > 0.7  # ~45 degree cone

    def _is_facing_interact_target(self) -> bool:
        """Check if character is facing interaction target."""
        forward = np.array([math.sin(self.chain.root_rot[1]), 0.0, math.cos(self.chain.root_rot[1])], dtype=np.float32)
        to_target = self.interact_target_pos - self.chain.root_pos
        to_target[1] = 0
        target_dist = np.linalg.norm(to_target)
        if target_dist < 1e-6:
            return True
        to_target = to_target / target_dist
        return np.dot(forward, to_target) > 0.85  # ~30 degree cone

    def _compute_reward(self, action: np.ndarray) -> float:
        """
        Compute reward signal based on policy type.

        Components:
        1. Locomotion: velocity tracking, energy efficiency, smoothness, ground contact, pose consistency
        2. Combat: attack accuracy, damage, dodge, stamina management, target facing
        3. React: hit reaction quality, knockback/stun/death animation quality, recovery speed
        4. Interact: interaction pose accuracy, naturalness, timing, object alignment
        """
        if self.policy_type == "locomotion":
            return self._compute_locomotion_reward(action)
        elif self.policy_type == "combat":
            return self._compute_combat_reward(action)
        elif self.policy_type == "react":
            return self._compute_react_reward(action)
        elif self.policy_type == "interact":
            return self._compute_interact_reward(action)
        return 0.0

    def _compute_locomotion_reward(self, action: np.ndarray) -> float:
        """Original locomotion reward."""
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

    def _compute_combat_reward(self, action: np.ndarray) -> float:
        """Combat policy reward: attack accuracy, damage, dodge, stamina management, target facing."""
        reward = 0.0

        # 1. Target facing reward (face the enemy)
        facing_reward = 1.0 if self._is_facing_target() else -0.5
        reward += 2.0 * facing_reward

        # 2. Distance management (stay in optimal range 1.5-3.0)
        optimal_dist = 2.0
        dist_error = abs(self.combat_target_distance - optimal_dist)
        dist_reward = math.exp(-dist_error * 0.8)
        reward += 1.5 * dist_reward

        # 3. Attack accuracy (hit when in range and facing)
        attack_triggered = action[0] > 0.5
        if attack_triggered and self.combat_attack_cooldown == 0:
            if self.combat_target_distance < 2.5 and self._is_facing_target():
                reward += 5.0  # Successful hit
                if self.combat_hit_streak > 0:
                    reward += 1.0 * min(self.combat_hit_streak, 5)  # Combo bonus
            else:
                reward -= 1.0  # Missed attack penalty

        # 4. Stamina management
        stamina_reward = self.combat_stamina * 0.5
        reward += stamina_reward
        if self.combat_stamina < 0.2:
            reward -= 2.0  # Low stamina penalty

        # 5. Dodge/evade (move perpendicular to target when close and not attacking)
        if self.combat_target_distance < 3.0 and not attack_triggered:
            to_target = self.combat_target_pos - self.chain.root_pos
            to_target[1] = 0
            if np.linalg.norm(to_target) > 1e-6:
                to_target = to_target / np.linalg.norm(to_target)
                # Check if moving sideways (dodge)
                velocity = self.chain.root_vel
                velocity[1] = 0
                if np.linalg.norm(velocity) > 0.1:
                    velocity = velocity / np.linalg.norm(velocity)
                    dodge_alignment = abs(np.dot(velocity, to_target))
                    if dodge_alignment < 0.3:  # Moving perpendicular
                        reward += 1.0

        # 6. Energy efficiency
        action_magnitude = np.mean(action ** 2)
        reward -= 0.3 * action_magnitude

        # 7. Smoothness
        action_delta = np.mean((action - self.prev_action) ** 2)
        reward -= 0.2 * action_delta

        return reward

    def _compute_react_reward(self, action: np.ndarray) -> float:
        """React policy reward: hit reaction quality, recovery speed, animation naturalness."""
        reward = 0.0

        if self.react_hit_intensity > 0.0:
            # During hit reaction
            hit_intensity = self.react_hit_intensity

            # 1. Reaction magnitude matches hit intensity
            action_magnitude = np.mean(np.abs(action))
            target_magnitude = hit_intensity * 0.8  # Scale to action space
            reaction_accuracy = 1.0 - abs(action_magnitude - target_magnitude)
            reward += 3.0 * max(0.0, reaction_accuracy)

            # 2. Directional reaction (react away from hit)
            if np.linalg.norm(self.react_hit_direction) > 1e-6:
                # Action should push away from hit direction
                # Simplified: check if root velocity opposes hit direction
                velocity = self.chain.root_vel.copy()
                velocity[1] = 0
                if np.linalg.norm(velocity) > 0.05:
                    velocity = velocity / np.linalg.norm(velocity)
                    opposite_alignment = -np.dot(velocity, self.react_hit_direction)
                    if opposite_alignment > 0.3:
                        reward += 2.0 * opposite_alignment

            # 3. Stun/knockdown specific rewards
            if self.react_is_stunned:
                # During stun: minimal movement, character should be unstable
                if action_magnitude < 0.2:
                    reward += 1.0
            elif self.react_is_knocked_down:
                # During knockdown: large reaction, then recovery
                if self.react_recovery_timer > 30:
                    # Initial knockdown: large reaction
                    if action_magnitude > 0.7:
                        reward += 2.0
                else:
                    # Recovery phase: controlled movement
                    if action_magnitude < 0.5:
                        reward += 1.5

            # 4. Recovery speed bonus
            if self.react_recovery_timer <= 0 and self.react_hit_type != "none":
                # Just recovered
                recovery_bonus = max(0, 50 - self.step_count) * 0.1  # Faster recovery = more reward
                reward += recovery_bonus
                self.react_hit_intensity = 0.0
                self.react_hit_type = "none"
        else:
            # No active hit: maintain ready pose, be responsive
            # Small action magnitude (ready stance)
            action_magnitude = np.mean(action ** 2)
            reward += 0.5 * math.exp(-action_magnitude * 5)

            # Smoothness
            action_delta = np.mean((action - self.prev_action) ** 2)
            reward -= 0.3 * action_delta

        return reward

    def _compute_interact_reward(self, action: np.ndarray) -> float:
        """Interact policy reward: pose accuracy, naturalness, timing, object alignment."""
        reward = 0.0

        # Distance to interaction target
        to_target = self.interact_target_pos - self.chain.root_pos
        dist = np.linalg.norm(to_target[::2])

        # 1. Approach phase reward
        if self.interact_phase == 0.0:
            # Move towards target
            velocity = self.chain.root_vel.copy()
            velocity[1] = 0
            if np.linalg.norm(velocity) > 0.1:
                velocity = velocity / np.linalg.norm(velocity)
                target_dir = to_target[::2] / max(dist, 1e-6)
                alignment = np.dot(velocity, target_dir)
                reward += 2.0 * max(0.0, alignment)
            # Distance penalty
            reward -= 0.5 * dist

        # 2. Align phase reward
        elif self.interact_phase == 1.0:
            # Face the target precisely
            facing = self._is_facing_interact_target()
            if facing:
                reward += 3.0
                # Stability bonus (low movement while aligning)
                action_magnitude = np.mean(action ** 2)
                reward += 1.0 * math.exp(-action_magnitude * 10)
            else:
                reward -= 1.0

        # 3. Interact phase reward
        elif self.interact_phase == 2.0:
            # Maintain precise pose for interaction
            facing = self._is_facing_interact_target()
            if facing:
                reward += 4.0
                # Specific pose for interaction type
                pose_reward = self._compute_interact_pose_reward(action)
                reward += 3.0 * pose_reward
            else:
                reward -= 2.0

            # Interaction timing bonus
            if 10 < self.interact_timer < 40:
                reward += 1.0  # Good timing

        # 4. Retreat phase reward
        elif self.interact_phase == 3.0:
            # Move away naturally
            if dist > 1.5:
                reward += 1.0
            if self.interact_success:
                reward += 5.0  # Successful interaction bonus

        # 5. General smoothness and energy
        action_magnitude = np.mean(action ** 2)
        reward -= 0.2 * action_magnitude
        action_delta = np.mean((action - self.prev_action) ** 2)
        reward -= 0.2 * action_delta

        return reward

    def _compute_interact_pose_reward(self, action: np.ndarray) -> float:
        """Compute pose-specific reward for different interaction types."""
        # Simplified: reward specific joint configurations for each interaction type

        action_magnitude = np.mean(action ** 2)
        if self.interact_object_type == "gather":
            # Gather: reach down, stable base
            return math.exp(-action_magnitude * 5) * (1.0 + 0.1 * np.std(action))
        elif self.interact_object_type == "craft":
            # Craft: two-handed, precise movements
            return math.exp(-action_magnitude * 8) * (1.0 + 0.05 * np.std(action))
        elif self.interact_object_type == "door":
            # Door: push/pull, weight transfer
            return math.exp(-action_magnitude * 4) * (1.0 + 0.15 * np.std(action))
        elif self.interact_object_type == "lever":
            # Lever: rotational, one-handed
            return math.exp(-action_magnitude * 6) * (1.0 + 0.1 * np.std(action))
        return 0.5

    def _sample_target_velocity(self):
        """Sample a new random target velocity for locomotion."""
        # Random direction
        angle = np.random.uniform(0, 2 * np.pi)
        speed = np.random.uniform(*self.cfg.target_velocity_range)

        # Smooth transition from current velocity
        current_vel = self.target_velocity
        target_vel = np.array([
            math.cos(angle) * speed,
            0.0,
            math.sin(angle) * speed
        ], dtype=np.float32)

        # Smooth interpolation
        self.target_velocity = current_vel * 0.5 + target_vel * 0.5
        self.target_speed = speed

    def _compute_root_velocity(self, action: np.ndarray) -> np.ndarray:
        """Compute root velocity from action (simplified)."""
        # Simplified: forward velocity proportional to leg joint motion
        leg_joints = min(6, self.act_dim)  # Assume first 6 joints are legs
        leg_motion = np.mean(np.abs(action[:leg_joints]))
        forward_vel = leg_motion * 2.0

        # Direction from target
        target_dir = self.target_velocity.copy()
        target_norm = np.linalg.norm(target_dir)
        if target_norm > 1e-6:
            target_dir = target_dir / target_norm

        # Apply noise
        forward_vel += np.random.randn() * 0.02

        return target_dir * forward_vel

    def _generate_terrain(self):
        """Generate a simple terrain heightmap."""
        for i in range(self.terrain_resolution):
            for j in range(self.terrain_resolution):
                x = (i - self.terrain_resolution / 2) * 0.5
                z = (j - self.terrain_resolution / 2) * 0.5
                height = 0.1 * math.sin(x * 0.5) * math.cos(z * 0.5)
                self._terrain_map[i, j] = height

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
        joint_positions_end = min(self.joint_count * 3, self.obs_dim)
        idx = max(idx, 0)
        expected_joint_positions = self.joint_count * 3
        if idx < expected_joint_positions and idx < self.obs_dim:
            pass  # already zeros

        # 2. Joint velocities
        idx = self.joint_count * 3
        for j in range(self.act_dim):
            if idx < self.obs_dim:
                obs[idx] = self.chain.velocities[j] * 0.1  # scale down
                idx += 1
        # Pad
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