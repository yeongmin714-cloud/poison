#!/usr/bin/env python3
"""
Synthetic Data Generator for Neural Animation System
=====================================================

Generates synthetic training data that matches the observation/action space
of the Unity Neural Animation Controller (Poison project).

Supports two avatar types:
  - Biped (Humanoid):  ObservationSize=120, ActionSize=80,  JointCount=18
  - Quadruped:         ObservationSize=150, ActionSize=100, JointCount=24

Observation Encoding (120/150 dims):
  [0-2]     Current velocity          (x=forward, z=lateral, y=up)
  [3-5]     Target velocity direction (x, y, z)
  [6]       Current speed             (scalar)
  [7-9]     Body lean offset          (pitch, roll, yaw)
  [10-81]   Joint rotations           (JointCount x 4 quaternions)
  [82-83]   Foot ground contact       (2 floats: left, right)
  [84-89]   Foot positions relative   (3x2 = 6 floats: L_pos, R_pos)
  [90-92]   Head look target direction (x, y, z)
  [93-95]   Action target             (x, y, z)
  [96-103]  Style embedding           (8 floats)
  [104-119] Terrain heightmap         (4x4 = 16 floats, flattened)

Action Encoding (80/100 dims):
  [0-2]     Root motion delta         (x, y, z displacement)
  [3-6]     Root rotation delta       (quaternion: x, y, z, w)
  [7-60]    Joint target rotations    (JointCount x 3 = 54 for biped, 72 for quad)
  [61-68]   Style embedding output    (8 floats)
  [69-79]   Reserved / padding

Gait Types: walk, run, jog, jump, idle, turn_left, turn_right

Usage:
    python synthetic_data_generator.py
    python synthetic_data_generator.py --num_samples 50000 --gait walk
    python synthetic_data_generator.py --analyze  (generate + analyze)
"""

import os
import sys
import argparse
import warnings
from typing import Dict, List, Optional, Tuple

import numpy as np
from scipy.spatial.transform import Rotation
from tqdm import tqdm

warnings.filterwarnings("ignore", category=UserWarning)

# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------

PROJECT_ROOT = os.path.dirname(os.path.abspath(__file__))
OUTPUT_DIR = os.path.join(PROJECT_ROOT, "output")
os.makedirs(OUTPUT_DIR, exist_ok=True)

VERSION = "1.0.0"

# Policy metadata matching Unity's AnimationPolicy.cs
AVATAR_CONFIGS = {
    "biped": {
        "obs_dim": 120,
        "act_dim": 80,
        "joint_count": 18,
        "terrain_resolution": 11,  # terrain system resolution (4x4 used in obs)
        "style_dim": 8,
        "name": "Locomotion_Biped_Base",
        "avatar_type": "Humanoid",
    },
    "quadruped": {
        "obs_dim": 150,
        "act_dim": 100,
        "joint_count": 24,
        "terrain_resolution": 11,
        "style_dim": 8,
        "name": "Locomotion_Quadruped_Base",
        "avatar_type": "Quadruped",
    },
}

GAIT_TYPES = ["idle", "walk", "jog", "run", "jump", "turn_left", "turn_right"]

# Observation layout indices (biped, 120-dim)
# These are computed dynamically based on joint_count
OBS_LAYOUT = {
    "velocity": (0, 3),           # [0:3]
    "target_vel_dir": (3, 6),     # [3:6]
    "speed": (6, 7),              # [6]
    "body_lean": (7, 10),         # [7:10]
    # joint_rotations: (10, 10 + joint_count*4)
    "foot_contact": None,         # computed after joints
    "foot_positions": None,
    "head_look": None,
    "action_target": None,
    "style_embedding": None,
    "terrain": None,
}


def _compute_obs_layout(joint_count: int, style_dim: int = 8) -> Dict[str, Tuple[int, int]]:
    """Compute observation slice indices for a given joint count."""
    idx = 0
    layout = {}
    layout["velocity"] = (idx, idx + 3);           idx += 3
    layout["target_vel_dir"] = (idx, idx + 3);     idx += 3
    layout["speed"] = (idx, idx + 1);              idx += 1
    layout["body_lean"] = (idx, idx + 3);          idx += 3
    layout["joint_rotations"] = (idx, idx + joint_count * 4); idx += joint_count * 4
    layout["foot_contact"] = (idx, idx + 2);       idx += 2
    layout["foot_positions"] = (idx, idx + 6);     idx += 6
    layout["head_look"] = (idx, idx + 3);          idx += 3
    layout["action_target"] = (idx, idx + 3);      idx += 3
    layout["style_embedding"] = (idx, idx + style_dim); idx += style_dim
    layout["terrain"] = (idx, idx + 16);           idx += 16
    layout["total"] = idx
    return layout


def _compute_act_layout(joint_count: int, style_dim: int = 8) -> Dict[str, Tuple[int, int]]:
    """Compute action slice indices for a given joint count."""
    idx = 0
    layout = {}
    layout["root_motion"] = (idx, idx + 3);        idx += 3
    layout["root_rotation"] = (idx, idx + 4);      idx += 4
    layout["joint_targets"] = (idx, idx + joint_count * 3); idx += joint_count * 3
    layout["style_output"] = (idx, idx + style_dim); idx += style_dim
    layout["reserved"] = (idx, None)  # remainder is padding
    layout["total"] = idx
    return layout


# ---------------------------------------------------------------------------
# Gait Pattern Generators
# ---------------------------------------------------------------------------


def _gait_phase(t: float, speed: float, gait: str) -> float:
    """Return a phase angle [0, 2pi) for the given gait and time."""
    if gait == "idle":
        return 0.0
    base_freq = {"walk": 1.5, "jog": 2.5, "run": 4.0, "jump": 3.0, "turn_left": 1.8, "turn_right": 1.8}
    freq = base_freq.get(gait, 1.0) * max(speed, 0.1)
    return (t * freq) % (2 * np.pi)


def generate_gait_pattern(
    num_samples: int,
    gait: str,
    dt: float = 1 / 60.0,
    speed_mean: float = 2.0,
    speed_std: float = 0.5,
    rng: Optional[np.random.Generator] = None,
) -> Tuple[np.ndarray, np.ndarray, np.ndarray, np.ndarray]:
    """
    Generate time-series gait patterns.

    Returns:
        velocities:     (N, 3)  local velocity (forward, vertical, lateral)
        target_dirs:    (N, 3)  target velocity direction
        speeds:         (N, 1)  scalar speeds
        ground_contact: (N, 2)  left/right foot contact flags
    """
    if rng is None:
        rng = np.random.default_rng(42)

    t = np.arange(num_samples, dtype=np.float32) * dt

    # --- Speed profile ---
    if gait == "idle":
        speeds = np.abs(rng.normal(0, 0.05, num_samples)).astype(np.float32)
    elif gait == "walk":
        speeds = np.clip(
            rng.normal(speed_mean * 0.5, speed_std * 0.3, num_samples), 0.3, 1.5
        ).astype(np.float32)
    elif gait == "jog":
        speeds = np.clip(
            rng.normal(speed_mean * 0.8, speed_std * 0.4, num_samples), 1.0, 3.5
        ).astype(np.float32)
    elif gait == "run":
        speeds = np.clip(
            rng.normal(speed_mean * 1.2, speed_std * 0.5, num_samples), 2.0, 6.0
        ).astype(np.float32)
    elif gait == "jump":
        # Jump: short burst of high speed
        speeds = np.clip(
            rng.normal(speed_mean * 1.0, speed_std * 0.8, num_samples), 0.5, 5.0
        ).astype(np.float32)
    elif gait in ("turn_left", "turn_right"):
        speeds = np.clip(
            rng.normal(speed_mean * 0.6, speed_std * 0.3, num_samples), 0.3, 2.5
        ).astype(np.float32)
    else:
        speeds = np.full(num_samples, speed_mean, dtype=np.float32)

    # --- Velocity (forward, up, lateral) ---
    phase = np.array([_gait_phase(ti, speeds[i], gait) for i, ti in enumerate(t)])

    # Base forward velocity
    forward_vel = speeds * (0.9 + 0.1 * np.sin(phase * 0.5))

    # Vertical bob (more pronounced at higher speeds)
    vertical_bob = speeds * 0.05 * np.sin(phase * 2.0)
    if gait == "jump":
        # Jump: large vertical impulse
        vertical_bob = np.where(
            np.sin(phase) > 0.7,
            speeds * 0.3 * np.sin(phase),
            vertical_bob * 0.1,
        )

    # Lateral sway
    lateral_sway = speeds * 0.03 * np.sin(phase * 1.5 + 0.5)
    if gait in ("turn_left", "turn_right"):
        turn_sign = 1.0 if gait == "turn_left" else -1.0
        lateral_sway += turn_sign * speeds * 0.15 * np.sin(phase * 0.8)

    velocities = np.stack([forward_vel, vertical_bob, lateral_sway], axis=1)

    # --- Target velocity direction ---
    target_dirs = velocities / (np.linalg.norm(velocities, axis=1, keepdims=True) + 1e-8)

    # --- Ground contact flags ---
    # Sinusoidal foot alternation: left foot contacts when sin(phase) < 0, right when > 0
    left_contact = (np.sin(phase) < 0.0).astype(np.float32)
    right_contact = (np.sin(phase) >= 0.0).astype(np.float32)
    if gait == "idle":
        left_contact[:] = 1.0
        right_contact[:] = 1.0
    if gait == "jump":
        # Both feet off ground during jump apex
        in_air = np.sin(phase) > 0.5
        left_contact[in_air] = 0.0
        right_contact[in_air] = 0.0

    ground_contact = np.stack([left_contact, right_contact], axis=1)

    # Apply smoothing
    from scipy.ndimage import uniform_filter1d

    if num_samples > 10:
        for i in range(3):
            velocities[:, i] = uniform_filter1d(velocities[:, i], size=5, mode="reflect")
        for i in range(2):
            ground_contact[:, i] = uniform_filter1d(ground_contact[:, i], size=3, mode="reflect")

    return velocities, target_dirs, speeds.reshape(-1, 1), ground_contact


# ---------------------------------------------------------------------------
# Observation Builder
# ---------------------------------------------------------------------------


def build_observation(
    config: Dict,
    velocities: np.ndarray,
    target_dirs: np.ndarray,
    speeds: np.ndarray,
    ground_contact: np.ndarray,
    rng: np.random.Generator,
    t: float = 0.0,
    gait: str = "walk",
) -> np.ndarray:
    """
    Build a single observation vector matching the neural animation spec.

    Args:
        config: Avatar configuration dict
        velocities: (N, 3) local velocity
        target_dirs: (N, 3) target velocity direction
        speeds: (N, 1) current speed
        ground_contact: (N, 2) foot contact flags
        rng: Random number generator
        t: Normalized time [0, 1)
        gait: Gait type string

    Returns:
        obs: (N, obs_dim) observation array
    """
    N = velocities.shape[0]
    joint_count = config["joint_count"]
    style_dim = config["style_dim"]
    layout = _compute_obs_layout(joint_count, style_dim)
    obs_dim = config["obs_dim"]
    obs = np.zeros((N, obs_dim), dtype=np.float32)

    # [0:3] Current velocity
    obs[:, layout["velocity"][0]:layout["velocity"][1]] = velocities

    # [3:6] Target velocity direction
    obs[:, layout["target_vel_dir"][0]:layout["target_vel_dir"][1]] = target_dirs

    # [6] Current speed
    obs[:, layout["speed"][0]:layout["speed"][1]] = speeds

    # [7:10] Body lean offset — proportional to velocity + turn
    lean_pitch = velocities[:, 0] * 0.02  # lean forward with speed
    lean_roll = velocities[:, 2] * 0.03   # lean into turns
    lean_yaw = np.zeros(N, dtype=np.float32)
    if gait in ("turn_left", "turn_right"):
        turn_sign = 1.0 if gait == "turn_left" else -1.0
        lean_yaw = turn_sign * speeds[:, 0] * 0.01
    body_lean = np.stack([lean_pitch, lean_roll, lean_yaw], axis=1)
    obs[:, layout["body_lean"][0]:layout["body_lean"][1]] = body_lean

    # [10:10+joint_count*4] Joint rotations as quaternions
    joint_start = layout["joint_rotations"][0]
    joint_end = layout["joint_rotations"][1]
    joint_quats = _generate_joint_rotations(N, joint_count, velocities, speeds, ground_contact, gait, rng)
    obs[:, joint_start:joint_end] = joint_quats.reshape(N, -1)

    # Foot contact
    fc_start = layout["foot_contact"][0]
    fc_end = layout["foot_contact"][1]
    obs[:, fc_start:fc_end] = ground_contact

    # Foot positions relative to hip (6 floats)
    fp_start = layout["foot_positions"][0]
    fp_end = layout["foot_positions"][1]
    foot_positions = _generate_foot_positions(N, ground_contact, velocities, gait, rng)
    obs[:, fp_start:fp_end] = foot_positions

    # Head look target direction
    hl_start = layout["head_look"][0]
    hl_end = layout["head_look"][1]
    head_look = _generate_head_look(N, velocities, gait, rng)
    obs[:, hl_start:hl_end] = head_look

    # Action target
    at_start = layout["action_target"][0]
    at_end = layout["action_target"][1]
    action_target = velocities[:, :3] * 5.0 + rng.normal(0, 0.1, (N, 3)).astype(np.float32)
    obs[:, at_start:at_end] = action_target

    # Style embedding
    se_start = layout["style_embedding"][0]
    se_end = layout["style_embedding"][1]
    style_emb = _generate_style_embedding(N, style_dim, gait, rng)
    obs[:, se_start:se_end] = style_emb

    # Terrain heightmap (4x4 = 16 floats)
    terrain_start = layout["terrain"][0]
    terrain_end = layout["terrain"][1]
    terrain = _generate_terrain_heightmap(N, velocities, gait, rng)
    obs[:, terrain_start:terrain_end] = terrain

    return obs


def _generate_joint_rotations(
    N: int, joint_count: int, velocities: np.ndarray,
    speeds: np.ndarray, ground_contact: np.ndarray,
    gait: str, rng: np.random.Generator,
) -> np.ndarray:
    """Generate joint rotations as quaternions (N, joint_count, 4)."""
    quats = np.zeros((N, joint_count, 4), dtype=np.float32)
    quats[:, :, 3] = 1.0  # identity quaternion w=1

    speed_ratio = np.clip(speeds[:, 0] / 5.0, 0.0, 1.0)

    for i in range(N):
        # Create a base rotation for each joint based on gait
        for j in range(joint_count):
            # Different joints have different phase offsets
            phase_offset = j * 0.3
            joint_angle = speed_ratio[i] * 0.3 * np.sin(i * 0.05 + phase_offset)

            if gait == "idle":
                joint_angle = 0.0

            # Convert to quaternion (rotate around local axes)
            rot = Rotation.from_euler("xyz", [joint_angle, joint_angle * 0.5, joint_angle * 0.3])
            q = rot.as_quat()  # [x, y, z, w]
            quats[i, j, :] = q.astype(np.float32)

    # Add controlled noise
    noise = rng.normal(0, 0.02, (N, joint_count, 4)).astype(np.float32)
    quats += noise
    # Renormalize
    norms = np.linalg.norm(quats, axis=2, keepdims=True)
    quats = quats / (norms + 1e-8)

    return quats


def _generate_foot_positions(
    N: int, ground_contact: np.ndarray,
    velocities: np.ndarray, gait: str, rng: np.random.Generator,
) -> np.ndarray:
    """Generate foot positions relative to hip (N, 6). Left=xyz, Right=xyz."""
    positions = np.zeros((N, 6), dtype=np.float32)

    # Left foot position relative to hip
    # Stride length proportional to speed
    stride = velocities[:, 0:1] * 0.05
    lateral_stance = 0.15
    foot_height = 0.0

    for i in range(N):
        # Left foot
        positions[i, 0] = stride[i, 0] + rng.normal(0, 0.01)  # forward
        positions[i, 1] = -0.5  # down from hip
        positions[i, 2] = lateral_stance + rng.normal(0, 0.005)  # lateral

        # Right foot
        positions[i, 3] = -stride[i, 0] + rng.normal(0, 0.01)  # forward
        positions[i, 4] = -0.5  # down from hip
        positions[i, 5] = -lateral_stance + rng.normal(0, 0.005)  # lateral

    # Adjust foot height during swing phase (when not in contact)
    swing_phase = 1.0 - ground_contact
    positions[:, 1] -= swing_phase[:, 0] * 0.1  # lift left foot
    positions[:, 4] -= swing_phase[:, 1] * 0.1  # lift right foot

    return positions


def _generate_head_look(
    N: int, velocities: np.ndarray, gait: str, rng: np.random.Generator,
) -> np.ndarray:
    """Generate head look target direction (N, 3)."""
    # Look roughly in the direction of movement + slight random offset
    look_dir = velocities / (np.linalg.norm(velocities, axis=1, keepdims=True) + 1e-8)
    # Add slight upward bias
    look_dir[:, 1] += 0.1
    # Normalize
    look_dir = look_dir / (np.linalg.norm(look_dir, axis=1, keepdims=True) + 1e-8)
    # Add noise
    look_dir += rng.normal(0, 0.02, look_dir.shape).astype(np.float32)
    return look_dir.astype(np.float32)


def _generate_style_embedding(
    N: int, style_dim: int, gait: str, rng: np.random.Generator,
) -> np.ndarray:
    """Generate style embedding vector (N, style_dim)."""
    # Each gait has a characteristic style embedding
    gait_style_map = {
        "idle": np.array([0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0], dtype=np.float32),
        "walk": np.array([0.2, 0.8, 0.1, 0.3, 0.5, 0.1, 0.2, 0.4], dtype=np.float32),
        "jog":  np.array([0.5, 0.6, 0.4, 0.5, 0.3, 0.4, 0.5, 0.6], dtype=np.float32),
        "run":  np.array([0.8, 0.3, 0.7, 0.8, 0.1, 0.7, 0.8, 0.9], dtype=np.float32),
        "jump": np.array([0.9, 0.1, 0.9, 0.1, 0.9, 0.8, 0.1, 0.2], dtype=np.float32),
        "turn_left":  np.array([0.3, 0.5, 0.8, 0.2, 0.6, 0.3, 0.7, 0.1], dtype=np.float32),
        "turn_right": np.array([0.3, 0.5, 0.2, 0.8, 0.6, 0.3, 0.1, 0.7], dtype=np.float32),
    }
    base = gait_style_map.get(gait, gait_style_map["walk"])
    if style_dim != 8:
        # Pad or truncate
        base = np.resize(base, style_dim)

    embedding = np.tile(base, (N, 1))
    noise = rng.normal(0, 0.05, (N, style_dim)).astype(np.float32)
    embedding += noise
    return np.clip(embedding, -1.0, 1.0).astype(np.float32)


def _generate_terrain_heightmap(
    N: int, velocities: np.ndarray, gait: str, rng: np.random.Generator,
) -> np.ndarray:
    """Generate terrain heightmap (N, 16) — 4x4 grid sampled around character."""
    grid_size = 4
    heights = np.zeros((N, grid_size * grid_size), dtype=np.float32)

    for i in range(N):
        # Flat terrain with slight undulations
        grid = rng.normal(0, 0.02, (grid_size, grid_size)).astype(np.float32)
        # Add a gentle slope in the direction of movement
        slope = velocities[i, 0] * 0.01
        for y in range(grid_size):
            for x in range(grid_size):
                grid[y, x] += slope * (x - grid_size / 2) / grid_size
        heights[i, :] = grid.flatten()

    return heights


# ---------------------------------------------------------------------------
# Action Builder
# ---------------------------------------------------------------------------


def build_action(
    config: Dict,
    velocities: np.ndarray,
    speeds: np.ndarray,
    gait: str,
    rng: np.random.Generator,
    t: float = 0.0,
) -> np.ndarray:
    """
    Build action vectors from the observation/state.

    The action encodes:
      - Root motion delta (3): x,y,z displacement
      - Root rotation delta (4): quaternion
      - Joint target rotations (joint_count * 3): axis-angle / euler-like
      - Style embedding output (8)
      - Reserved padding

    Args:
        config: Avatar configuration dict
        velocities: (N, 3) local velocity
        speeds: (N, 1) current speed
        gait: Gait type
        rng: Random number generator
        t: Normalized time

    Returns:
        actions: (N, act_dim) action array
    """
    N = velocities.shape[0]
    joint_count = config["joint_count"]
    style_dim = config["style_dim"]
    layout = _compute_act_layout(joint_count, style_dim)
    act_dim = config["act_dim"]
    actions = np.zeros((N, act_dim), dtype=np.float32)

    speed_ratio = np.clip(speeds[:, 0] / 5.0, 0.0, 1.0)

    # [0:3] Root motion delta — velocity * dt
    # Simulate at 60 FPS, so dt ≈ 1/60
    dt = 1.0 / 60.0
    actions[:, 0] = velocities[:, 0] * dt  # forward displacement
    actions[:, 1] = velocities[:, 1] * dt  # vertical displacement
    actions[:, 2] = velocities[:, 2] * dt  # lateral displacement

    # [3:7] Root rotation delta as quaternion
    for i in range(N):
        if gait in ("turn_left", "turn_right"):
            turn_rate = 30.0 * dt * speed_ratio[i]  # degrees
            if gait == "turn_right":
                turn_rate = -turn_rate
            rot = Rotation.from_euler("y", turn_rate)
            q = rot.as_quat()  # [x, y, z, w]
            actions[i, 3:7] = q.astype(np.float32)
        else:
            # Small random rotation noise for straight gaits
            rot = Rotation.from_euler("y", rng.normal(0, 1.0) * dt)
            q = rot.as_quat()
            actions[i, 3:7] = q.astype(np.float32)

    # [7:7+joint_count*3] Joint target rotations (3-axis euler-like)
    jt_start = layout["joint_targets"][0]
    jt_end = layout["joint_targets"][1]
    joint_targets = _generate_joint_targets(N, joint_count, speeds, gait, rng)
    actions[:, jt_start:jt_end] = joint_targets.reshape(N, -1)

    # Style embedding output (8)
    se_start = layout["style_output"][0]
    se_end = layout["style_output"][1]
    style_emb = _generate_style_embedding(N, style_dim, gait, rng)
    actions[:, se_start:se_end] = style_emb

    # Reserved padding — already zero

    return actions


def _generate_joint_targets(
    N: int, joint_count: int, speeds: np.ndarray,
    gait: str, rng: np.random.Generator,
) -> np.ndarray:
    """Generate joint target rotations (N, joint_count, 3) — euler angles."""
    targets = np.zeros((N, joint_count, 3), dtype=np.float32)
    speed_ratio = np.clip(speeds[:, 0] / 5.0, 0.0, 1.0)

    for i in range(N):
        for j in range(joint_count):
            # Periodic joint motion synchronized with gait
            phase = i * 0.05 + j * 0.3
            amplitude = speed_ratio[i] * 20.0  # degrees

            # Hip joints have larger range
            hip_factor = 1.5 if j < 4 else 1.0  # first 4 joints are hips
            targets[i, j, 0] = amplitude * hip_factor * np.sin(phase)
            targets[i, j, 1] = amplitude * 0.5 * hip_factor * np.sin(phase + 0.5)
            targets[i, j, 2] = amplitude * 0.3 * hip_factor * np.sin(phase + 1.0)

    if gait == "idle":
        targets *= 0.1

    # Add noise
    noise = rng.normal(0, 0.5, targets.shape).astype(np.float32)
    targets += noise

    return targets


# ---------------------------------------------------------------------------
# Full Dataset Generation
# ---------------------------------------------------------------------------


def generate_dataset(
    avatar_type: str = "biped",
    num_samples: int = 10000,
    gait: str = "mixed",
    seed: int = 42,
    noise_level: float = 0.02,
    verbose: bool = True,
) -> Dict:
    """
    Generate a complete synthetic dataset.

    Args:
        avatar_type: 'biped' or 'quadruped'
        num_samples: Number of samples to generate
        gait: Gait type or 'mixed' for all gaits
        seed: Random seed
        noise_level: Amount of noise to add (fraction)
        verbose: Show progress bar

    Returns:
        dict with keys: 'observations', 'actions', 'metadata'
    """
    config = AVATAR_CONFIGS[avatar_type]
    rng = np.random.default_rng(seed)

    if verbose:
        print(f"[Generator] Generating {num_samples} samples for {avatar_type}")
        print(f"[Generator]  Obs dim: {config['obs_dim']}, Act dim: {config['act_dim']}")
        print(f"[Generator]  Joint count: {config['joint_count']}, Gait: {gait}")

    if gait == "mixed":
        # Sample evenly from all gaits
        samples_per_gait = num_samples // len(GAIT_TYPES)
        all_obs = []
        all_acts = []
        all_gait_labels = []
        all_metadata = []

        for g in GAIT_TYPES:
            n = samples_per_gait
            g_obs, g_acts, g_meta = _generate_single_gait(
                config, n, g, rng, noise_level, verbose
            )
            all_obs.append(g_obs)
            all_acts.append(g_acts)
            all_gait_labels.extend([g] * n)
            all_metadata.append(g_meta)

        # Add remaining samples
        remainder = num_samples - samples_per_gait * len(GAIT_TYPES)
        if remainder > 0:
            g = rng.choice(GAIT_TYPES)
            g_obs, g_acts, g_meta = _generate_single_gait(
                config, remainder, g, rng, noise_level, False
            )
            all_obs.append(g_obs)
            all_acts.append(g_acts)
            all_gait_labels.extend([g] * remainder)
            all_metadata.append(g_meta)

        observations = np.concatenate(all_obs, axis=0)
        actions = np.concatenate(all_acts, axis=0)
        metadata = {
            "gait_type": all_gait_labels,
            "avatar_type": config["avatar_type"],
            "joint_count": config["joint_count"],
            "version": VERSION,
            "num_samples": num_samples,
            "obs_dim": config["obs_dim"],
            "act_dim": config["act_dim"],
            "seed": seed,
            "noise_level": noise_level,
            "gait_distribution": {g: samples_per_gait for g in GAIT_TYPES},
        }
        # Merge metadata dicts
        for m in all_metadata:
            for k, v in m.items():
                if k not in ("gait_type",) and isinstance(v, (int, float, str)):
                    metadata[k] = v
    else:
        observations, actions, metadata = _generate_single_gait(
            config, num_samples, gait, rng, noise_level, verbose
        )
        metadata["gait_type"] = [gait] * num_samples

    # Shuffle dataset
    perm = rng.permutation(num_samples)
    observations = observations[perm]
    actions = actions[perm]
    metadata["gait_type"] = [metadata["gait_type"][i] for i in perm]

    if verbose:
        print(f"[Generator]  Done. Shape: obs={observations.shape}, act={actions.shape}")

    return {
        "observations": observations,
        "actions": actions,
        "metadata": metadata,
    }


def _generate_single_gait(
    config: Dict,
    num_samples: int,
    gait: str,
    rng: np.random.Generator,
    noise_level: float = 0.02,
    verbose: bool = False,
) -> Tuple[np.ndarray, np.ndarray, Dict]:
    """Generate data for a single gait type."""
    # Generate gait patterns
    speed_mean = {"idle": 0.0, "walk": 1.5, "jog": 3.0, "run": 5.0,
                  "jump": 3.0, "turn_left": 1.5, "turn_right": 1.5}.get(gait, 2.0)
    speed_std = 0.3 if gait != "idle" else 0.02

    velocities, target_dirs, speeds, ground_contact = generate_gait_pattern(
        num_samples=num_samples,
        gait=gait,
        dt=1.0 / 60.0,
        speed_mean=speed_mean,
        speed_std=speed_std,
        rng=rng,
    )

    # Build observations
    observations = build_observation(
        config=config,
        velocities=velocities,
        target_dirs=target_dirs,
        speeds=speeds,
        ground_contact=ground_contact,
        rng=rng,
        t=0.0,
        gait=gait,
    )

    # Build actions
    actions = build_action(
        config=config,
        velocities=velocities,
        speeds=speeds,
        gait=gait,
        rng=rng,
    )

    # Add controlled noise to observations
    if noise_level > 0:
        obs_noise = rng.normal(0, noise_level, observations.shape).astype(np.float32)
        observations += obs_noise

    metadata = {
        "gait_type": gait,
        "avatar_type": config["avatar_type"],
        "joint_count": config["joint_count"],
        "version": VERSION,
        "num_samples": num_samples,
        "obs_dim": config["obs_dim"],
        "act_dim": config["act_dim"],
    }

    return observations, actions, metadata


# ---------------------------------------------------------------------------
# Save / Load
# ---------------------------------------------------------------------------


def save_dataset(data: Dict, filepath: str, verbose: bool = True) -> str:
    """
    Save dataset to NPZ file.

    Args:
        data: dict with 'observations', 'actions', 'metadata'
        filepath: Output file path

    Returns:
        Path to saved file
    """
    import json

    obs = data["observations"]
    acts = data["actions"]
    meta = data["metadata"]

    # Convert metadata to JSON-serializable format
    meta_serializable = {}
    for k, v in meta.items():
        if isinstance(v, list):
            meta_serializable[k] = json.dumps(v)
        else:
            meta_serializable[k] = v

    np.savez_compressed(filepath, observations=obs, actions=acts, **meta_serializable)

    if verbose:
        file_size = os.path.getsize(filepath) / (1024 * 1024)
        print(f"[Save] Saved to {filepath} ({file_size:.2f} MB)")

    return filepath


def load_dataset(filepath: str) -> Dict:
    """
    Load dataset from NPZ file.

    Args:
        filepath: Path to NPZ file

    Returns:
        dict with 'observations', 'actions', 'metadata'
    """
    import json

    data = np.load(filepath, allow_pickle=True)
    observations = data["observations"]
    actions = data["actions"]

    metadata = {}
    for key in data.files:
        if key in ("observations", "actions"):
            continue
        val = data[key]
        if isinstance(val, np.ndarray) and val.ndim == 0:
            val = val.item()
        # Try to parse JSON strings
        if isinstance(val, str) and val.startswith("["):
            try:
                val = json.loads(val)
            except (json.JSONDecodeError, TypeError):
                pass
        metadata[key] = val

    print(f"[Load] Loaded {observations.shape[0]} samples from {filepath}")
    print(f"[Load]  Obs shape: {observations.shape}, Act shape: {actions.shape}")

    return {
        "observations": observations,
        "actions": actions,
        "metadata": metadata,
    }


# ---------------------------------------------------------------------------
# Dataset Statistics
# ---------------------------------------------------------------------------


def compute_statistics(data: Dict) -> Dict:
    """
    Compute basic statistics for the dataset.

    Args:
        data: Dataset dict

    Returns:
        dict of statistics
    """
    obs = data["observations"]
    acts = data["actions"]
    meta = data["metadata"]

    stats = {
        "num_samples": obs.shape[0],
        "obs_dim": obs.shape[1],
        "act_dim": acts.shape[1],
        "obs_mean": float(np.mean(obs)),
        "obs_std": float(np.std(obs)),
        "obs_min": float(np.min(obs)),
        "obs_max": float(np.max(obs)),
        "act_mean": float(np.mean(acts)),
        "act_std": float(np.std(acts)),
        "act_min": float(np.min(acts)),
        "act_max": float(np.max(acts)),
        "obs_channel_means": np.mean(obs, axis=0).tolist(),
        "obs_channel_stds": np.std(obs, axis=0).tolist(),
        "act_channel_means": np.mean(acts, axis=0).tolist(),
        "act_channel_stds": np.std(acts, axis=0).tolist(),
    }

    if "gait_type" in meta:
        gait_types = meta["gait_type"]
        if isinstance(gait_types, list):
            from collections import Counter
            stats["gait_distribution"] = dict(Counter(gait_types))

    return stats


# ---------------------------------------------------------------------------
# Main Entry Point
# ---------------------------------------------------------------------------


def main():
    parser = argparse.ArgumentParser(
        description="Generate synthetic training data for Neural Animation System"
    )
    parser.add_argument(
        "--num_samples", type=int, default=10000,
        help="Number of samples to generate (default: 10000)"
    )
    parser.add_argument(
        "--avatar", type=str, choices=["biped", "quadruped", "both"], default="both",
        help="Avatar type to generate (default: both)"
    )
    parser.add_argument(
        "--gait", type=str, choices=GAIT_TYPES + ["mixed"], default="mixed",
        help="Gait type (default: mixed — all gaits evenly sampled)"
    )
    parser.add_argument(
        "--seed", type=int, default=42,
        help="Random seed (default: 42)"
    )
    parser.add_argument(
        "--noise", type=float, default=0.02,
        help="Observation noise level (default: 0.02)"
    )
    parser.add_argument(
        "--output_dir", type=str, default=OUTPUT_DIR,
        help=f"Output directory (default: {OUTPUT_DIR})"
    )
    parser.add_argument(
        "--analyze", action="store_true",
        help="Run analysis after generation"
    )
    parser.add_argument(
        "--stats", action="store_true",
        help="Print dataset statistics"
    )

    args = parser.parse_args()

    print("=" * 60)
    print("  Neural Animation — Synthetic Data Generator")
    print("=" * 60)
    print(f"  Samples:  {args.num_samples}")
    print(f"  Avatar:   {args.avatar}")
    print(f"  Gait:     {args.gait}")
    print(f"  Seed:     {args.seed}")
    print(f"  Noise:    {args.noise}")
    print("=" * 60)

    avatars = ["biped", "quadruped"] if args.avatar == "both" else [args.avatar]

    for avatar in avatars:
        print(f"\n{'─' * 50}")
        print(f"  Generating {avatar} dataset...")
        print(f"{'─' * 50}")

        data = generate_dataset(
            avatar_type=avatar,
            num_samples=args.num_samples,
            gait=args.gait,
            seed=args.seed,
            noise_level=args.noise,
            verbose=True,
        )

        # Save
        filename = f"locomotion_{avatar}_dataset.npz"
        filepath = os.path.join(args.output_dir, filename)
        save_dataset(data, filepath)

        # Statistics
        if args.stats:
            stats = compute_statistics(data)
            print(f"\n  Statistics for {avatar}:")
            print(f"    Samples:    {stats['num_samples']}")
            print(f"    Obs dim:    {stats['obs_dim']}")
            print(f"    Act dim:    {stats['act_dim']}")
            print(f"    Obs range:  [{stats['obs_min']:.4f}, {stats['obs_max']:.4f}]")
            print(f"    Act range:  [{stats['act_min']:.4f}, {stats['act_max']:.4f}]")
            if "gait_distribution" in stats:
                print(f"    Gaits:      {stats['gait_distribution']}")

    print(f"\n{'=' * 60}")
    print(f"  Generation complete. Files saved to: {args.output_dir}")
    print(f"{'=' * 60}")

    # Run analysis if requested
    if args.analyze:
        print("\n  Running dataset analysis...\n")
        from dataset_analyzer import analyze_dataset
        for avatar in avatars:
            filename = f"locomotion_{avatar}_dataset.npz"
            filepath = os.path.join(args.output_dir, filename)
            analyze_dataset(filepath, show_plots=False)


if __name__ == "__main__":
    main()