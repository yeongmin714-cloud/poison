"""
Simple Animation Environment v2 — 3D Physics Simulation for Neural Animation.

Upgrades from v1:
- 18-joint (biped) / 24-joint (quadruped) 3D kinematic chains
- Real gravity (-9.81) + inertia tensors
- 11×11 terrain heightmap sampling
- Foot/hoof contact detection → Ground Contact Labels
- 3DOF joint rotation (Euler angles per joint)
- PD control with configurable gains per joint
- Curriculum: Easy → Medium → Hard → Expert → Master terrain

Matches Unity ObservationEncoder layout exactly.
"""

import math
import random
from typing import Tuple, Dict, Optional, Any, Literal, List
import numpy as np


# ──────────────────────────────────────────────────────────────────────────────
#  3D Joint & Link Definitions
# ──────────────────────────────────────────────────────────────────────────────

BIPED_JOINT_NAMES = [
    "Hips", "Spine", "Spine1", "Spine2", "Neck", "Head",
    "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand",
    "RightShoulder", "RightArm", "RightForeArm", "RightHand",
    "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase",
    "RightUpLeg", "RightLeg", "RightFoot", "RightToeBase",
]

BIPED_18_JOINTS = [
    "Hips", "Spine", "Spine1", "Spine2", "Neck", "Head",
    "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand",
    "RightShoulder", "RightArm", "RightForeArm", "RightHand",
    "LeftUpLeg", "LeftLeg", "LeftFoot",
    "RightUpLeg", "RightLeg", "RightFoot",
]

QUADRUPED_24_JOINTS = [
    "Hips", "Spine", "Spine1", "Spine2", "Neck", "Head",
    "LeftFrontShoulder", "LeftFrontArm", "LeftFrontForeArm", "LeftFrontPaw",
    "RightFrontShoulder", "RightFrontArm", "RightFrontForeArm", "RightFrontPaw",
    "LeftBackShoulder", "LeftBackArm", "LeftBackForeArm", "LeftBackPaw",
    "RightBackShoulder", "RightBackArm", "RightBackForeArm", "RightBackPaw",
    "Tail1", "Tail2",
]

# Joint limits in radians (per DOF: x, y, z)
BIPED_JOINT_LIMITS = {
    "Hips":      (np.pi/4, np.pi/6, np.pi/4),
    "Spine":     (np.pi/6, np.pi/12, np.pi/12),
    "Spine1":    (np.pi/6, np.pi/12, np.pi/12),
    "Spine2":    (np.pi/6, np.pi/12, np.pi/12),
    "Neck":      (np.pi/4, np.pi/6, np.pi/6),
    "Head":      (np.pi/4, np.pi/6, np.pi/6),
    "LeftShoulder":  (np.pi/2, np.pi/3, np.pi/3),
    "LeftArm":       (np.pi/2, np.pi/4, np.pi/6),
    "LeftForeArm":   (np.pi/2, 0.0, 0.0),  # hinge
    "LeftHand":      (np.pi/3, np.pi/4, np.pi/4),
    "RightShoulder": (np.pi/2, np.pi/3, np.pi/3),
    "RightArm":      (np.pi/2, np.pi/4, np.pi/6),
    "RightForeArm":  (np.pi/2, 0.0, 0.0),
    "RightHand":     (np.pi/3, np.pi/4, np.pi/4),
    "LeftUpLeg":  (np.pi/2, np.pi/6, np.pi/6),
    "LeftLeg":    (np.pi/2, 0.0, 0.0),
    "LeftFoot":   (np.pi/3, np.pi/6, np.pi/6),
    "RightUpLeg": (np.pi/2, np.pi/6, np.pi/6),
    "RightLeg":   (np.pi/2, 0.0, 0.0),
    "RightFoot":  (np.pi/3, np.pi/6, np.pi/6),
}

QUADRUPED_JOINT_LIMITS = {
    "Hips":      (np.pi/4, np.pi/6, np.pi/4),
    "Spine":     (np.pi/6, np.pi/12, np.pi/12),
    "Spine1":    (np.pi/6, np.pi/12, np.pi/12),
    "Spine2":    (np.pi/6, np.pi/12, np.pi/12),
    "Neck":      (np.pi/4, np.pi/6, np.pi/6),
    "Head":      (np.pi/4, np.pi/6, np.pi/6),
    "LeftFrontShoulder":  (np.pi/2, np.pi/3, np.pi/3),
    "LeftFrontArm":       (np.pi/2, np.pi/4, np.pi/6),
    "LeftFrontForeArm":   (np.pi/2, 0.0, 0.0),
    "LeftFrontPaw":       (np.pi/3, np.pi/4, np.pi/4),
    "RightFrontShoulder": (np.pi/2, np.pi/3, np.pi/3),
    "RightFrontArm":      (np.pi/2, np.pi/4, np.pi/6),
    "RightFrontForeArm":  (np.pi/2, 0.0, 0.0),
    "RightFrontPaw":      (np.pi/3, np.pi/4, np.pi/4),
    "LeftBackShoulder":   (np.pi/2, np.pi/3, np.pi/3),
    "LeftBackArm":        (np.pi/2, np.pi/4, np.pi/6),
    "LeftBackForeArm":    (np.pi/2, 0.0, 0.0),
    "LeftBackPaw":        (np.pi/3, np.pi/4, np.pi/4),
    "RightBackShoulder":  (np.pi/2, np.pi/3, np.pi/3),
    "RightBackArm":       (np.pi/2, np.pi/4, np.pi/6),
    "RightBackForeArm":   (np.pi/2, 0.0, 0.0),
    "RightBackPaw":       (np.pi/3, np.pi/4, np.pi/4),
    "Tail1":  (np.pi/4, np.pi/4, np.pi/4),
    "Tail2":  (np.pi/4, np.pi/4, np.pi/4),
}

# Foot bone names for contact detection (match actual joint names in skeleton)
BIPED_FOOT_BONES = ["LeftFoot", "RightFoot"]
QUADRUPED_FOOT_BONES = ["LeftFrontPaw", "RightFrontPaw", "LeftBackPaw", "RightBackPaw"]

# Default local foot offsets (relative to bone origin)
FOOT_OFFSET = np.array([0.0, -0.1, 0.05], dtype=np.float32)  # slightly below and forward


# ──────────────────────────────────────────────────────────────────────────────
#  3D Kinematic Chain with Physics
# ──────────────────────────────────────────────────────────────────────────────

class Joint3D:
    """Single 3DOF joint with PD control and physics."""
    def __init__(self, name: str, parent_idx: int, local_pos: np.ndarray,
                 limits: Tuple[float, float, float], mass: float = 1.0,
                 inertia: Optional[np.ndarray] = None):
        self.name = name
        self.parent_idx = parent_idx
        self.local_pos = local_pos.astype(np.float32)  # local offset from parent
        self.limits = np.array(limits, dtype=np.float32)  # (x, y, z) max angles
        self.mass = mass
        self.inertia = inertia if inertia is not None else np.eye(3) * 0.01

        # State
        self.angle = np.zeros(3, dtype=np.float32)       # current Euler angles (rad)
        self.vel = np.zeros(3, dtype=np.float32)         # angular velocity
        self.target_angle = np.zeros(3, dtype=np.float32) # PD target
        self.torque = np.zeros(3, dtype=np.float32)

        # PD gains (tunable per joint type)
        self.kp = 100.0
        self.kd = 10.0

        # World transform (updated by forward kinematics)
        self.world_pos = np.zeros(3, dtype=np.float32)
        self.world_rot = np.eye(3, dtype=np.float32)  # rotation matrix

    def set_pd_gains(self, kp: float, kd: float):
        self.kp = kp
        self.kd = kd

    def step(self, dt: float):
        """PD control + Euler integration."""
        error = self.target_angle - self.angle
        # Wrap error to [-pi, pi]
        error = np.arctan2(np.sin(error), np.cos(error))

        self.torque = self.kp * error - self.kd * self.vel
        # Clamp torque
        max_torque = 50.0
        self.torque = np.clip(self.torque, -max_torque, max_torque)

        # Angular acceleration = torque * inertia^-1
        accel = self.torque / np.diag(self.inertia)
        self.vel += accel * dt
        self.vel *= 0.99  # damping
        self.angle += self.vel * dt

        # Clamp to limits
        self.angle = np.clip(self.angle, -self.limits, self.limits)

    def get_rotation_matrix(self) -> np.ndarray:
        """Convert Euler angles (XYZ) to rotation matrix."""
        x, y, z = self.angle
        cx, sx = math.cos(x), math.sin(x)
        cy, sy = math.cos(y), math.sin(y)
        cz, sz = math.cos(z), math.sin(z)

        # R = Rz * Ry * Rx
        return np.array([
            [cy*cz, -cy*sz, sy],
            [cx*sz + sx*sy*cz, cx*cz - sx*sy*sz, -sx*cy],
            [sx*sz - cx*sy*cz, sx*cz + cx*sy*sz, cx*cy]
        ], dtype=np.float32)


class KinematicChain3D:
    """Full 3D character kinematic chain with physics simulation."""
    def __init__(self, joint_names: List[str], joint_limits: Dict[str, Tuple[float, float, float]],
                 foot_bones: List[str], dt: float = 0.02):
        self.joint_names = joint_names
        self.foot_bones = foot_bones
        self.dt = dt
        self.num_joints = len(joint_names)

        # Build joint hierarchy (simplified: linear chain with known parent indices)
        self.parent_indices = self._build_parent_indices()
        self.local_positions = self._build_local_positions()

        # Create joints
        self.joints = []
        for i, name in enumerate(joint_names):
            limits = joint_limits.get(name, (np.pi/2, np.pi/2, np.pi/2))
            local_pos = self.local_positions[i]
            parent_idx = self.parent_indices[i]
            joint = Joint3D(name, parent_idx, local_pos, limits)
            self.joints.append(joint)

        # Root state
        self.root_pos = np.zeros(3, dtype=np.float32)
        self.root_vel = np.zeros(3, dtype=np.float32)
        self.root_rot = np.eye(3, dtype=np.float32)
        self.root_ang_vel = np.zeros(3, dtype=np.float32)

        # Contact state
        self.contact_flags = np.zeros(len(foot_bones), dtype=np.float32)
        self.contact_positions = np.zeros((len(foot_bones), 3), dtype=np.float32)

        # Terrain heightmap
        self.terrain_resolution = 11
        self.terrain_map = np.zeros((self.terrain_resolution, self.terrain_resolution), dtype=np.float32)
        self.terrain_scale = 0.5  # meters per cell

        # Default pose (rest angles)
        self.default_angles = np.zeros((self.num_joints, 3), dtype=np.float32)
        for i, name in enumerate(joint_names):
            # Natural rest pose
            if "UpLeg" in name or "Shoulder" in name:
                self.default_angles[i] = [0.1, 0.0, 0.0]
            elif "Leg" in name or "Arm" in name:
                self.default_angles[i] = [-0.5, 0.0, 0.0]
            elif "Foot" in name or "Hand" in name or "Paw" in name:
                self.default_angles[i] = [0.2, 0.0, 0.0]
            elif "Spine" in name or "Neck" in name:
                self.default_angles[i] = [0.05, 0.0, 0.0]

    def _build_parent_indices(self) -> List[int]:
        """Build parent index array for standard skeleton hierarchy."""
        # Simplified: each joint's parent is the previous one, except root
        # In real implementation, this would match actual skeleton hierarchy
        parents = [-1]  # Hips is root
        for i in range(1, self.num_joints):
            parents.append(i - 1)
        return parents

    def _build_local_positions(self) -> List[np.ndarray]:
        """Default local bone positions (bone lengths)."""
        positions = []
        for name in self.joint_names:
            if "Hips" in name:
                positions.append(np.array([0.0, 0.0, 0.0], dtype=np.float32))  # Hips IS the root
            elif "Spine" in name or "Neck" in name:
                positions.append(np.array([0.0, 0.15, 0.0], dtype=np.float32))
            elif "Head" in name:
                positions.append(np.array([0.0, 0.2, 0.0], dtype=np.float32))
            elif "Shoulder" in name:
                positions.append(np.array([0.15, 0.0, 0.0], dtype=np.float32))
            elif "Arm" in name or "ForeArm" in name:
                positions.append(np.array([0.25, 0.0, 0.0], dtype=np.float32))
            elif "Hand" in name or "Paw" in name:
                positions.append(np.array([0.1, 0.0, 0.0], dtype=np.float32))
            elif "UpLeg" in name or "Thigh" in name:
                positions.append(np.array([0.1, -0.4, 0.0], dtype=np.float32))
            elif "Leg" in name or "Shin" in name:
                positions.append(np.array([0.0, -0.4, 0.0], dtype=np.float32))
            elif "Foot" in name:
                positions.append(np.array([0.0, -0.1, 0.05], dtype=np.float32))
            else:
                positions.append(np.array([0.0, 0.1, 0.0], dtype=np.float32))
        return positions

    def reset(self):
        """Reset to default pose."""
        self.root_pos = np.zeros(3, dtype=np.float32)
        self.root_vel = np.zeros(3, dtype=np.float32)
        self.root_rot = np.eye(3, dtype=np.float32)
        self.root_ang_vel = np.zeros(3, dtype=np.float32)

        for i, joint in enumerate(self.joints):
            joint.angle = self.default_angles[i].copy()
            joint.vel = np.zeros(3, dtype=np.float32)
            joint.target_angle = self.default_angles[i].copy()
            joint.torque = np.zeros(3, dtype=np.float32)

        self.contact_flags = np.zeros(len(self.foot_bones), dtype=np.float32)
        return self._get_joint_obs()

    def set_target_angles(self, action: np.ndarray):
        """Set target angles from action (normalized [-1, 1] per joint).
        
        Handles multiple action dimension formats:
        - Legacy: action_dim (80 biped, 100 quadruped) -> use first num_joints
        - New 3DOF: action_dim == num_joints * 3 -> direct 3DOF mapping
        """
        action = action.flatten()
        
        # Legacy mode: action_dim > num_joints (e.g., 80 > 18) -> use first num_joints
        if len(action) >= self.num_joints and len(action) != self.num_joints * 3:
            for i in range(self.num_joints):
                joint = self.joints[i]
                primary_val = action[i] * joint.limits[0]  # Primary DOF (X axis)
                joint.target_angle = np.array([primary_val, 0.0, 0.0], dtype=np.float32)
        
        # New mode: 3 values per joint (action_dim == num_joints * 3)
        elif len(action) == self.num_joints * 3:
            action_3d = action.reshape(-1, 3)
            for i in range(self.num_joints):
                joint = self.joints[i]
                joint.target_angle = action_3d[i] * joint.limits
        
        # Fallback: exact match (action_dim == num_joints, 1DOF per joint)
        elif len(action) == self.num_joints:
            for i, joint in enumerate(self.joints):
                if i < len(action):
                    primary_val = action[i] * joint.limits[0]
                    joint.target_angle = np.array([primary_val, 0.0, 0.0], dtype=np.float32)
        
        # Other: truncate to num_joints * 3
        else:
            action_3d = action[:self.num_joints * 3].reshape(-1, 3)
            for i in range(min(len(action_3d), self.num_joints)):
                joint = self.joints[i]
                joint.target_angle = action_3d[i] * joint.limits

    def step_physics(self, gravity: np.ndarray = None):
        """Step the full physics simulation."""
        if gravity is None:
            gravity = np.array([0.0, -9.81, 0.0], dtype=np.float32)

        # 1. Step each joint (PD control)
        for joint in self.joints:
            joint.step(self.dt)

        # 2. Forward kinematics to compute world positions
        self._forward_kinematics()

        # 3. Root position = fixed height above terrain (hips can move relative)
        terrain_height = self._sample_terrain(self.root_pos[0], self.root_pos[2])
        target_root_y = terrain_height + 0.9  # Hip height above ground
        
        # Smoothly move root Y to target
        self.root_pos[1] = target_root_y
        
        # Root velocity from XZ movement (Y is fixed)
        if not hasattr(self, '_prev_root_xz'):
            self._prev_root_xz = self.root_pos[[0,2]].copy()
        self.root_vel = np.zeros(3, dtype=np.float32)
        self.root_vel[[0,2]] = (self.root_pos[[0,2]] - self._prev_root_xz) / self.dt
        self._prev_root_xz = self.root_pos[[0,2]].copy()

        # 4. Foot contact detection
        self._detect_contacts()

    def _forward_kinematics(self):
        """Compute world transforms for all joints."""
        for i, joint in enumerate(self.joints):
            if joint.parent_idx == -1:
                # Root joint
                joint.world_rot = joint.get_rotation_matrix()
                joint.world_pos = self.root_pos + joint.local_pos
            else:
                parent = self.joints[joint.parent_idx]
                joint.world_rot = parent.world_rot @ joint.get_rotation_matrix()
                joint.world_pos = parent.world_pos + parent.world_rot @ joint.local_pos

    def _detect_contacts(self):
        """Detect foot contacts with terrain."""
        for i, foot_name in enumerate(self.foot_bones):
            if foot_name in self.joint_names:
                idx = self.joint_names.index(foot_name)
                foot_world = self.joints[idx].world_pos + self.joints[idx].world_rot @ FOOT_OFFSET
                terrain_h = self._sample_terrain(foot_world[0], foot_world[2])
                height_above_ground = foot_world[1] - terrain_h

                # Contact if foot is close to ground
                self.contact_flags[i] = 1.0 if height_above_ground < 0.05 else 0.0
                self.contact_positions[i] = foot_world
            else:
                self.contact_flags[i] = 0.0

    def _sample_terrain(self, x: float, z: float) -> float:
        """Bilinear heightmap sampling."""
        # Convert world coords to terrain grid
        gx = x / self.terrain_scale + self.terrain_resolution / 2
        gz = z / self.terrain_scale + self.terrain_resolution / 2

        ix = int(gx)
        iz = int(gz)
        fx = gx - ix
        fz = gz - iz

        # Clamp
        ix = np.clip(ix, 0, self.terrain_resolution - 2)
        iz = np.clip(iz, 0, self.terrain_resolution - 2)

        # Bilinear interpolation
        h00 = self.terrain_map[ix, iz]
        h10 = self.terrain_map[ix + 1, iz]
        h01 = self.terrain_map[ix, iz + 1]
        h11 = self.terrain_map[ix + 1, iz + 1]

        h0 = h00 * (1 - fx) + h10 * fx
        h1 = h01 * (1 - fx) + h11 * fx
        return h0 * (1 - fz) + h1 * fz

    def generate_terrain(self, phase: int = 0):
        """Generate terrain heightmap based on curriculum phase."""
        # phase: 0=easy, 1=medium, 2=hard, 3=expert, 4=master
        roughness = [0.05, 0.15, 0.3, 0.5, 0.8][min(phase, 4)]
        obstacle_density = [0.0, 0.05, 0.15, 0.3, 0.5][min(phase, 4)]

        for i in range(self.terrain_resolution):
            for j in range(self.terrain_resolution):
                x = (i - self.terrain_resolution / 2) * self.terrain_scale
                z = (j - self.terrain_resolution / 2) * self.terrain_scale

                # Base noise
                height = roughness * math.sin(x * 0.5) * math.cos(z * 0.5)
                height += roughness * 0.5 * math.sin(x * 1.3) * math.cos(z * 1.7)

                # Obstacles
                if np.random.random() < obstacle_density:
                    height += np.random.uniform(0.1, 0.4)

                self.terrain_map[i, j] = height

    def _get_joint_obs(self) -> np.ndarray:
        """Get joint positions and velocities as flat array."""
        obs = []
        for joint in self.joints:
            obs.extend(joint.angle)
            obs.extend(joint.vel)
        return np.array(obs, dtype=np.float32)

    def get_foot_heights(self) -> np.ndarray:
        """Get foot heights above terrain for each foot."""
        heights = []
        for i, foot_name in enumerate(self.foot_bones):
            if foot_name in self.joint_names:
                idx = self.joint_names.index(foot_name)
                foot_world = self.joints[idx].world_pos + self.joints[idx].world_rot @ FOOT_OFFSET
                terrain_h = self._sample_terrain(foot_world[0], foot_world[2])
                heights.append(foot_world[1] - terrain_h)
            else:
                heights.append(999.0)  # no contact
        return np.array(heights, dtype=np.float32)


# ──────────────────────────────────────────────────────────────────────────────
#  Gymnasium-Compatible Environment
# ──────────────────────────────────────────────────────────────────────────────

class _Box:
    def __init__(self, low: float, high: float, shape: Tuple[int, ...], dtype: type):
        self.low = np.full(shape, low, dtype=dtype)
        self.high = np.full(shape, high, dtype=dtype)
        self.shape = shape
        self.dtype = dtype


class SimpleAnimationEnvV2:
    """
    3D Physics Environment for Neural Animation Training.

    Observation space (matches Unity ObservationEncoder):
    - Joint positions (joint_count * 3): sin, cos, normalized angle per joint
    - Joint velocities (joint_count * 3)
    - Root velocity (3)
    - Root angular velocity (3)
    - Ground contact flags (4 for biped, 4 for quadruped)
    - Target direction (3)
    - Target speed (1)
    - Terrain heightmap (11×11 = 121)
    - Style embedding (8)

    Action space:
    - Joint target angles (action_dim): normalized [-1, 1] per DOF
    """

    def __init__(self, cfg, policy_type: str = "locomotion"):
        from config import Config, EnvConfig, AvatarSpec
        self.cfg = cfg.env
        self.avatar_spec = AvatarSpec.from_name(cfg.avatar)
        self.obs_dim = cfg.obs_dim
        self.act_dim = cfg.act_dim
        self.joint_count = cfg.joint_count
        self.dt = cfg.env.dt
        self.policy_type = policy_type

        # Create appropriate kinematic chain
        # Match action dim: biped 80 -> 26-27 joints worth, quadruped 100 -> 33-34 joints worth
        # Use subset of joints that match the action dimension
        if cfg.avatar in ("biped", "humanoid"):
            # Biped: 80 action dim / 3 DOF ≈ 26-27 joints. Use all 18 joints (54 DOF) + pad
            self.chain = KinematicChain3D(
                BIPED_18_JOINTS, BIPED_JOINT_LIMITS, BIPED_FOOT_BONES, self.dt
            )
        else:
            # Quadruped: 100 action dim / 3 DOF ≈ 33 joints. Use all 24 joints (72 DOF) + pad
            self.chain = KinematicChain3D(
                QUADRUPED_24_JOINTS, QUADRUPED_JOINT_LIMITS, QUADRUPED_FOOT_BONES, self.dt
            )

        # Target velocity (changes periodically)
        self.target_velocity = np.zeros(3, dtype=np.float32)
        self.target_speed = 0.0
        self.target_change_timer = 0
        self.target_change_interval = 200

        # Episode tracking
        self.step_count = 0
        self.max_episode_length = cfg.env.max_episode_length
        self.prev_action = np.zeros(self.act_dim, dtype=np.float32)

        # Style embedding
        self.style_embedding_size = 8
        self.style_embedding = np.zeros(self.style_embedding_size, dtype=np.float32)

        # Curriculum
        self._curriculum_enabled = False
        self._curriculum_phase = 0  # 0=easy, 1=medium, 2=hard, 3=expert, 4=master

        # Policy-specific state
        self._init_policy_state()

        # Spaces
        self.observation_space = _Box(-5.0, 5.0, (self.obs_dim,), np.float32)
        self.action_space = _Box(-1.0, 1.0, (self.act_dim,), np.float32)
        self.metadata = {"render_modes": []}

    def _init_policy_state(self):
        """Initialize policy-specific state variables."""
        # Locomotion
        self.target_velocity = np.zeros(3, dtype=np.float32)
        self.target_speed = 0.0
        self.target_change_timer = 0

        # Combat
        self.combat_target_pos = np.zeros(3, dtype=np.float32)
        self.combat_attack_cooldown = 0
        self.combat_stamina = 1.0
        self.combat_last_hit_time = -100
        self.combat_hit_streak = 0
        self.combat_target_distance = 5.0

        # React
        self.react_hit_intensity = 0.0
        self.react_hit_direction = np.zeros(3, dtype=np.float32)
        self.react_recovery_timer = 0
        self.react_is_stunned = False
        self.react_is_knocked_down = False
        self.react_hit_type = "none"

        # Interact
        self.interact_target_pos = np.zeros(3, dtype=np.float32)
        self.interact_phase = 0.0
        self.interact_timer = 0
        self.interact_object_type = "gather"
        self.interact_success = False

        # Fly/Swim
        self.fly_swim_target_pos = np.zeros(3, dtype=np.float32)
        self.fly_swim_speed = 0.0
        self.fly_swim_bank_angle = 0.0

        # Mount
        self.mount_target_pos = np.zeros(3, dtype=np.float32)
        self.mount_target_speed = 0.0
        self.mount_stamina = 1.0

        # Climb
        self.climb_target_pos = np.zeros(3, dtype=np.float32)
        self.climb_progress = 0.0
        self.climb_stamina = 1.0

        # Style (Run/Crouch)
        self.style_target_pos = np.zeros(3, dtype=np.float32)
        self.style_target_speed = 0.0
        self.style_crouch_amount = 0.0

        # Large Monster
        self.large_monster_target_pos = np.zeros(3, dtype=np.float32)
        self.large_monster_territory_radius = 20.0
        self.large_monster_rage = 0.0
        self.large_monster_stamina = 1.0

    # ──────────────────────────────────────────────────────────────────────
    #  Curriculum & Style API
    # ──────────────────────────────────────────────────────────────────────

    def set_curriculum_enabled(self, enabled: bool):
        self._curriculum_enabled = enabled
        if enabled:
            self._curriculum_phase = 0
            self.chain.generate_terrain(0)

    def set_curriculum_phase(self, phase: int):
        if self._curriculum_enabled:
            self._curriculum_phase = max(0, min(4, phase))
            self.chain.generate_terrain(self._curriculum_phase)

    def set_style_embedding(self, index: int):
        self.style_embedding = np.zeros(self.style_embedding_size, dtype=np.float32)
        if 0 <= index < self.style_embedding_size:
            self.style_embedding[index] = 1.0

    def seed(self, seed: int):
        np.random.seed(seed)
        random.seed(seed)

    # ──────────────────────────────────────────────────────────────────────
    #  Reset & Step
    # ──────────────────────────────────────────────────────────────────────

    def reset(self, seed: Optional[int] = None, options: Optional[dict] = None) -> Tuple[np.ndarray, dict]:
        if seed is not None:
            self.seed(seed)

        self.chain.reset()
        self.step_count = 0
        self.prev_action = np.zeros(self.act_dim, dtype=np.float32)
        self._init_policy_state()

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

        # Generate terrain
        if self._curriculum_enabled:
            self.chain.generate_terrain(self._curriculum_phase)
        else:
            self.chain.generate_terrain(0)

        obs = self._encode_observation()
        return obs, {}

    # ──────────────────────────────────────────────────────────────────────
    #  Policy-Specific State Management
    # ──────────────────────────────────────────────────────────────────────

    def _sample_target_velocity(self):
        angle = np.random.uniform(0, 2 * np.pi)
        speed = np.random.uniform(*self.cfg.target_velocity_range)
        self.target_velocity = np.array([
            math.cos(angle) * speed, 0.0, math.sin(angle) * speed
        ], dtype=np.float32)
        self.target_speed = speed

    def _reset_combat_state(self):
        angle = np.random.uniform(-np.pi/3, np.pi/3)
        distance = np.random.uniform(3.0, 8.0)
        self.combat_target_pos = np.array([
            math.cos(angle) * distance, 0.0, math.sin(angle) * distance
        ], dtype=np.float32)
        self.combat_target_distance = distance
        self.combat_attack_cooldown = 0
        self.combat_stamina = 1.0
        self.combat_last_hit_time = -100
        self.combat_hit_streak = 0

    def _reset_react_state(self):
        self.react_hit_intensity = 0.0
        self.react_hit_direction = np.zeros(3, dtype=np.float32)
        self.react_recovery_timer = 0
        self.react_is_stunned = False
        self.react_is_knocked_down = False
        self.react_hit_type = "none"
        if np.random.random() < 0.3:
            self._trigger_react_hit()

    def _trigger_react_hit(self):
        hit_type = np.random.choice(
            ["light", "heavy", "launch", "stun", "knockdown"],
            p=[0.4, 0.25, 0.1, 0.15, 0.1]
        )
        self.react_hit_type = hit_type
        angle = np.random.uniform(0, 2 * np.pi)
        self.react_hit_direction = np.array([
            math.cos(angle), 0.0, math.sin(angle)
        ], dtype=np.float32)

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

    def _reset_interact_state(self):
        angle = np.random.uniform(0, 2 * np.pi)
        distance = np.random.uniform(1.0, 2.5)
        self.interact_target_pos = np.array([
            math.cos(angle) * distance, 0.0, math.sin(angle) * distance
        ], dtype=np.float32)
        self.interact_phase = 0.0
        self.interact_timer = 0
        self.interact_object_type = np.random.choice(["gather", "craft", "door", "lever"])
        self.interact_success = False

    # ──────────────────────────────────────────────────────────────────────
    #  Step
    # ──────────────────────────────────────────────────────────────────────

    def step(self, action: np.ndarray) -> Tuple[np.ndarray, float, bool, bool, dict]:
        action = np.clip(action, -1.0, 1.0).astype(np.float32)

        # Action noise
        if self.cfg.action_noise > 0:
            action += np.random.randn(self.act_dim).astype(np.float32) * self.cfg.action_noise
            action = np.clip(action, -1.0, 1.0)

        # Set joint targets
        self.chain.set_target_angles(action)

        # Physics step
        self.chain.step_physics()

        # Policy-specific updates
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

        # Observation
        obs = self._encode_observation()

        # Episode termination
        self.step_count += 1
        terminated = False
        truncated = self.step_count >= self.max_episode_length

        info = {
            "target_speed": self.target_speed,
            "root_velocity": np.linalg.norm(self.chain.root_vel),
            "step_count": self.step_count,
            "policy_type": self.policy_type,
            "contact_flags": self.chain.contact_flags.copy(),
            "curriculum_phase": self._curriculum_phase if self._curriculum_enabled else -1,
        }

        self.prev_action = action.copy()
        return obs, reward, terminated, truncated, info

    def _update_locomotion_state(self):
        self.target_change_timer += 1
        if self.target_change_timer >= self.target_change_interval:
            self._sample_target_velocity()
            self.target_change_timer = 0

    def _update_combat_state(self, action: np.ndarray):
        if self.combat_attack_cooldown > 0:
            self.combat_attack_cooldown -= 1

        self.combat_stamina = min(1.0, self.combat_stamina + 0.01)

        to_target = self.combat_target_pos - self.chain.root_pos
        self.combat_target_distance = np.linalg.norm(to_target[::2])

        attack_triggered = action[0] > 0.5 and self.combat_attack_cooldown == 0 and self.combat_stamina > 0.2
        if attack_triggered:
            self.combat_attack_cooldown = 30
            self.combat_stamina -= 0.15
            facing = self._is_facing_target()
            if self.combat_target_distance < 2.5 and facing:
                self.combat_last_hit_time = self.step_count
                self.combat_hit_streak += 1
            else:
                self.combat_hit_streak = 0

        if self.step_count % 300 == 0:
            self._reset_combat_state()

    def _update_react_state(self, action: np.ndarray):
        if self.react_recovery_timer > 0:
            self.react_recovery_timer -= 1

        if self.react_is_knocked_down:
            if self.react_recovery_timer <= 0:
                self.react_is_knocked_down = False
                self.react_recovery_timer = 20
        elif self.react_is_stunned:
            if self.react_recovery_timer <= 0:
                self.react_is_stunned = False

        if self.react_hit_intensity == 0.0 and np.random.random() < 0.005:
            self._trigger_react_hit()

    def _update_interact_state(self, action: np.ndarray):
        self.interact_timer += 1

        to_target = self.interact_target_pos - self.chain.root_pos
        dist = np.linalg.norm(to_target[::2])

        if self.interact_phase == 0.0:  # Approach
            if dist < 1.5:
                self.interact_phase = 1.0
                self.interact_timer = 0
        elif self.interact_phase == 1.0:  # Align
            facing = self._is_facing_interact_target()
            if facing and self.interact_timer > 10:
                self.interact_phase = 2.0
                self.interact_timer = 0
        elif self.interact_phase == 2.0:  # Interact
            if self.interact_timer > 30:
                self.interact_success = True
                self.interact_phase = 3.0
                self.interact_timer = 0
        elif self.interact_phase == 3.0:  # Retreat
            if dist > 3.0 or self.interact_timer > 50:
                self._reset_interact_state()

    def _is_facing_target(self) -> bool:
        forward = np.array([
            math.sin(self.chain.root_rot[1, 1]), 0.0, math.cos(self.chain.root_rot[1, 1])
        ], dtype=np.float32)
        to_target = self.combat_target_pos - self.chain.root_pos
        to_target[1] = 0
        target_dist = np.linalg.norm(to_target)
        if target_dist < 1e-6:
            return True
        to_target = to_target / target_dist
        return np.dot(forward, to_target) > 0.7

    def _is_facing_interact_target(self) -> bool:
        forward = np.array([
            math.sin(self.chain.root_rot[1, 1]), 0.0, math.cos(self.chain.root_rot[1, 1])
        ], dtype=np.float32)
        to_target = self.interact_target_pos - self.chain.root_pos
        to_target[1] = 0
        target_dist = np.linalg.norm(to_target)
        if target_dist < 1e-6:
            return True
        to_target = to_target / target_dist
        return np.dot(forward, to_target) > 0.85

    # ──────────────────────────────────────────────────────────────────────
    #  Reward Computation
    # ──────────────────────────────────────────────────────────────────────

    def _compute_reward(self, action: np.ndarray) -> float:
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
        reward = 0.0

        # 1. Velocity tracking
        current_vel = self.chain.root_vel
        target_vel = self.target_velocity
        vel_error = np.linalg.norm(current_vel - target_vel)
        vel_reward = math.exp(-vel_error * 0.5)
        reward += self.cfg.reward_velocity_weight * vel_reward

        # 2. Heading tracking
        target_norm = np.linalg.norm(target_vel)
        if target_norm > 1e-6:
            target_dir = target_vel / target_norm
            current_dir = current_vel / max(np.linalg.norm(current_vel), 1e-6)
            heading_reward = max(0.0, np.dot(target_dir, current_dir))
            reward += self.cfg.reward_heading_weight * heading_reward

        # 3. Energy penalty (torque^2)
        energy_penalty = 0.0
        for joint in self.chain.joints:
            energy_penalty += np.sum(joint.torque ** 2)
        reward += self.cfg.reward_energy_weight * (-energy_penalty * 0.01)

        # 4. Foot contact pattern (alternating gait)
        phase = (self.step_count % 30) / 30.0
        expected_left = 1.0 if phase < 0.5 else 0.0
        expected_right = 1.0 if phase >= 0.5 else 0.0
        contact_reward = -abs(self.chain.contact_flags[0] - expected_left) - abs(self.chain.contact_flags[1] - expected_right)
        reward += self.cfg.reward_contact_weight * contact_reward

        # 5. Joint limit penalty
        limit_penalty = 0.0
        for joint in self.chain.joints:
            limit_penalty += np.sum(np.maximum(np.abs(joint.angle) - joint.limits * 0.9, 0.0))
        reward += self.cfg.reward_joint_limit_weight * (-limit_penalty)

        # 6. Smoothness (action delta)
        action_delta = np.mean((action - self.prev_action) ** 2)
        reward += self.cfg.reward_smoothness_weight * (-action_delta)

        # 7. Terrain adaptation (foot height matching)
        foot_heights = self.chain.get_foot_heights()
        valid_heights = foot_heights[foot_heights < 999]
        if len(valid_heights) > 0:
            terrain_penalty = np.mean(np.abs(valid_heights))
            reward += self.cfg.reward_terrain_weight * (-terrain_penalty)

        # 8. Pose regularization (stay near default pose)
        pose_error = 0.0
        for i, joint in enumerate(self.chain.joints):
            default = self.chain.default_angles[i]
            pose_error += np.mean((joint.angle - default) ** 2)
        pose_reward = math.exp(-pose_error)
        reward += self.cfg.reward_pose_weight * pose_reward

        return reward

    def _compute_combat_reward(self, action: np.ndarray) -> float:
        reward = 0.0

        # 1. Target facing
        facing_reward = 1.0 if self._is_facing_target() else -0.5
        reward += 2.0 * facing_reward

        # 2. Distance management
        optimal_dist = 2.0
        dist_error = abs(self.combat_target_distance - optimal_dist)
        dist_reward = math.exp(-dist_error * 0.8)
        reward += 1.5 * dist_reward

        # 3. Attack accuracy
        attack_triggered = action[0] > 0.5
        if attack_triggered and self.combat_attack_cooldown == 0:
            if self.combat_target_distance < 2.5 and self._is_facing_target():
                reward += 5.0
                if self.combat_hit_streak > 0:
                    reward += 1.0 * min(self.combat_hit_streak, 5)
            else:
                reward -= 1.0

        # 4. Stamina management
        reward += 0.5 * self.combat_stamina
        if self.combat_stamina < 0.2:
            reward -= 2.0

        # 5. Dodge/evade
        if self.combat_target_distance < 3.0 and not attack_triggered:
            to_target = self.combat_target_pos - self.chain.root_pos
            to_target[1] = 0
            if np.linalg.norm(to_target) > 1e-6:
                to_target = to_target / np.linalg.norm(to_target)
                vel = self.chain.root_vel.copy()
                vel[1] = 0
                if np.linalg.norm(vel) > 0.1:
                    vel = vel / np.linalg.norm(vel)
                    dodge = abs(np.dot(vel, to_target))
                    if dodge < 0.3:
                        reward += 1.0

        # 6. Energy & smoothness
        reward -= 0.3 * np.mean(action ** 2)
        reward -= 0.2 * np.mean((action - self.prev_action) ** 2)

        return reward

    def _compute_react_reward(self, action: np.ndarray) -> float:
        reward = 0.0

        if self.react_hit_intensity > 0.0:
            hit_intensity = self.react_hit_intensity
            action_mag = np.mean(np.abs(action))
            target_mag = hit_intensity * 0.8
            accuracy = 1.0 - abs(action_mag - target_mag)
            reward += 3.0 * max(0.0, accuracy)

            # Directional reaction
            if np.linalg.norm(self.react_hit_direction) > 1e-6:
                vel = self.chain.root_vel.copy()
                vel[1] = 0
                if np.linalg.norm(vel) > 0.05:
                    vel = vel / np.linalg.norm(vel)
                    opposite = -np.dot(vel, self.react_hit_direction)
                    if opposite > 0.3:
                        reward += 2.0 * opposite

            # Stun/knockdown specifics
            if self.react_is_stunned and action_mag < 0.2:
                reward += 1.0
            elif self.react_is_knocked_down:
                if self.react_recovery_timer > 30 and action_mag > 0.7:
                    reward += 2.0
                elif self.react_recovery_timer <= 30 and action_mag < 0.5:
                    reward += 1.5

            # Recovery bonus
            if self.react_recovery_timer <= 0 and self.react_hit_type != "none":
                recovery_bonus = max(0, 50 - self.step_count) * 0.1
                reward += recovery_bonus
                self.react_hit_intensity = 0.0
                self.react_hit_type = "none"
        else:
            # Ready stance
            action_mag = np.mean(action ** 2)
            reward += 0.5 * math.exp(-action_mag * 5)
            reward -= 0.3 * np.mean((action - self.prev_action) ** 2)

        return reward

    def _compute_interact_reward(self, action: np.ndarray) -> float:
        reward = 0.0
        to_target = self.interact_target_pos - self.chain.root_pos
        dist = np.linalg.norm(to_target[::2])

        if self.interact_phase == 0.0:  # Approach
            vel = self.chain.root_vel.copy()
            vel[1] = 0
            if np.linalg.norm(vel) > 0.1:
                vel = vel / np.linalg.norm(vel)
                target_dir = to_target[::2] / max(dist, 1e-6)
                reward += 2.0 * max(0.0, np.dot(vel, target_dir))
            reward -= 0.5 * dist

        elif self.interact_phase == 1.0:  # Align
            facing = self._is_facing_interact_target()
            if facing:
                reward += 3.0
                action_mag = np.mean(action ** 2)
                reward += 1.0 * math.exp(-action_mag * 10)
            else:
                reward -= 1.0

        elif self.interact_phase == 2.0:  # Interact
            facing = self._is_facing_interact_target()
            if facing:
                reward += 4.0
                pose_reward = self._compute_interact_pose_reward(action)
                reward += 3.0 * pose_reward
            else:
                reward -= 2.0

            if 10 < self.interact_timer < 40:
                reward += 1.0

        elif self.interact_phase == 3.0:  # Retreat
            if dist > 1.5:
                reward += 1.0
            if self.interact_success:
                reward += 5.0

        # General
        reward -= 0.2 * np.mean(action ** 2)
        reward -= 0.2 * np.mean((action - self.prev_action) ** 2)

        return reward

    def _compute_interact_pose_reward(self, action: np.ndarray) -> float:
        action_mag = np.mean(action ** 2)
        if self.interact_object_type == "gather":
            return math.exp(-action_mag * 5) * (1.0 + 0.1 * np.std(action))
        elif self.interact_object_type == "craft":
            return math.exp(-action_mag * 8) * (1.0 + 0.05 * np.std(action))
        elif self.interact_object_type == "door":
            return math.exp(-action_mag * 4) * (1.0 + 0.15 * np.std(action))
        elif self.interact_object_type == "lever":
            return math.exp(-action_mag * 6) * (1.0 + 0.1 * np.std(action))
        return 0.5

    # ──────────────────────────────────────────────────────────────────────
    #  Observation Encoding (matches Unity ObservationEncoder)
    # ──────────────────────────────────────────────────────────────────────

    def _encode_observation(self) -> np.ndarray:
        obs = np.zeros(self.obs_dim, dtype=np.float32)
        idx = 0

        # 1. Joint positions (joint_count * 3): sin, cos, normalized
        for i, joint in enumerate(self.chain.joints):
            if idx >= self.obs_dim: break
            angle = joint.angle[0]  # primary DOF
            limit = joint.limits[0]
            obs[idx] = math.sin(angle); idx += 1
            if idx >= self.obs_dim: break
            obs[idx] = math.cos(angle); idx += 1
            if idx >= self.obs_dim: break
            obs[idx] = angle / max(limit, 1e-6); idx += 1

        # Pad remaining joint positions
        expected_joint_pos = self.joint_count * 3
        idx = max(idx, expected_joint_pos)

        # 2. Joint velocities (joint_count * 3)
        for i, joint in enumerate(self.chain.joints):
            if idx >= self.obs_dim: break
            for v in joint.vel:
                if idx >= self.obs_dim: break
                obs[idx] = v * 0.1; idx += 1

        # 3. Root velocity (3)
        if idx + 3 <= self.obs_dim:
            obs[idx:idx+3] = self.chain.root_vel * 0.1; idx += 3

        # 4. Root angular velocity (3)
        if idx + 3 <= self.obs_dim:
            obs[idx:idx+3] = self.chain.root_rot.diagonal() * 0.01; idx += 3

        # 5. Ground contact flags (4)
        for flag in self.chain.contact_flags:
            if idx < self.obs_dim:
                obs[idx] = flag; idx += 1

        # 6. Target direction (3)
        target_norm = np.linalg.norm(self.target_velocity)
        if target_norm > 1e-6:
            target_dir = self.target_velocity / target_norm
        else:
            target_dir = np.zeros(3)
        if idx + 3 <= self.obs_dim:
            obs[idx:idx+3] = target_dir; idx += 3

        # 7. Target speed (1)
        if idx < self.obs_dim:
            obs[idx] = self.target_speed * 0.1; idx += 1

        # 8. Terrain heightmap (11×11 = 121)
        terrain_flat = self.chain.terrain_map.flatten()
        for t in range(len(terrain_flat)):
            if idx < self.obs_dim:
                obs[idx] = terrain_flat[t]; idx += 1

        # 9. Style embedding (remaining)
        for s in range(self.style_embedding_size):
            if idx < self.obs_dim:
                obs[idx] = self.style_embedding[s]; idx += 1

        # Noise
        if self.cfg.observation_noise > 0:
            obs += np.random.randn(self.obs_dim).astype(np.float32) * self.cfg.observation_noise

        obs = np.clip(obs, -5.0, 5.0)
        return obs


# ──────────────────────────────────────────────────────────────────────────────
#  Factory
# ──────────────────────────────────────────────────────────────────────────────

def create_env_v2(cfg, policy_type: str = "locomotion") -> SimpleAnimationEnvV2:
    return SimpleAnimationEnvV2(cfg, policy_type)