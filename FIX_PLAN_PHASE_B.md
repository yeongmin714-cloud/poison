# Phase B: Player & Terrain Visibility Fix Plan

## Root Causes Identified

### 1. **PlayerHealth._currentHP serialized as 0 in scene file** (CRITICAL)
- Scene file shows: `_currentHP: 0` (line 23675)
- Player starts with 0 HP → appears dead until `Start()` runs
- FixMainScene creates PlayerHealth but doesn't set `_currentHP = _maxHP` before saving

### 2. **Singleton pattern may destroy player at runtime** (HIGH)
- PlayerHealth/Stats/Inventory/Combat Awake() use "first wins" pattern
- If leftover DontDestroyOnLoad Instance exists from previous Play session → new player destroyed
- Other systems (SoundManagerEnhanced, TerritoryManager, TimeManager) have `RuntimeInitializeOnLoadMethod` that persist across Play sessions

### 3. **CharacterController vs MeshCollider collision issues** (HIGH)
- Terrain uses procedural MeshCollider (complex geometry)
- CharacterController works best with primitive colliders
- Player may fall through terrain → camera follows → "falling down" sensation

### 4. **Camera setup incomplete**
- Main Camera at (0,0,0) with CinemachineBrain
- VCam follows Player, but if Player destroyed → camera stays at origin
- Need to ensure VCam properly configured and Player not destroyed

---

## Fix Plan (Sequential Execution)

### Task B1: Fix PlayerHealth serialization in FixMainScene
- In `CreatePlayer()`, after adding PlayerHealth, explicitly set `_currentHP = _maxHP` via reflection or `TakeDamage(-_maxHP)` hack
- Or better: call `health.SetMaxHP(100)` then `health.Heal(100)` before saving scene
- Verify scene file shows `_currentHP: 100`

### Task B2: Add runtime singleton cleanup in GameSetup
- Add `PurgeRuntimeSingletons()` call at start of `GameSetup.Start()`
- Clean up any leftover DontDestroyOnLoad singletons from other systems
- Ensure PlayerHealth/Stats/Inventory/Combat instances are fresh

### Task B3: Fix terrain collision for CharacterController
- Add primitive BoxCollider as "collision floor" under procedural terrain
- Or: Simplify terrain MeshCollider (convex, or lower resolution)
- Or: Add a large invisible Plane collider at y=0
- Test: Player should not fall through ground

### Task B4: Ensure Player not destroyed by singleton conflicts
- Modify PlayerHealth/Stats/Inventory/Combat Awake() to NOT destroy if Instance is from different scene
- Or: Add unique instance ID check
- Or: Ensure Purge runs before scene loads (hard in Play mode)

### Task B5: Verify camera follows player correctly
- Check VCam Follow/LookAt targets set to Player/PlayerModel
- Ensure Main Camera has CinemachineBrain
- Test: Camera should follow player, not stay at origin

### Task B6: Full Play Mode verification
- Run Unity in batchmode with Play Mode test
- Verify: Player in hierarchy, terrain visible, camera follows, no falling
- Check console for errors

---

## Implementation Order

1. **B1** → Fix serialization (immediate fix for 0 HP)
2. **B2** → Runtime singleton cleanup (prevents destruction)
3. **B3** → Terrain collision (prevents falling)
4. **B4** → Singleton robustness (defense in depth)
5. **B5** → Camera verification
6. **B6** → Full test

---

## Verification Criteria

- [ ] Scene file shows `_currentHP: 100` for PlayerHealth
- [ ] Play Mode: Player GameObject exists in Hierarchy
- [ ] Play Mode: Player stays on ground (no falling through)
- [ ] Play Mode: Terrain visible in Game view
- [ ] Play Mode: Camera follows player smoothly
- [ ] Console: Zero errors during 30s Play Mode
- [ ] Batchmode Fix() passes all checks