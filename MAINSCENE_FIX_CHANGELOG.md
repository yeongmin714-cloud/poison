# Phase B: MainScene Fix Complete - Full Change Log

## Overview
Fixed "Player and terrain not visible, camera falling down" issues in MainScene through systematic root cause analysis and targeted fixes.

---

## Root Causes Identified

1. **PlayerHealth._currentHP = 0** in scene file → Player spawns dead
2. **DontDestroyOnLoad on Player components** → Player moves to DontDestroyOnLoad scene → GameSetup.Purge destroys it
3. **CollisionFloor at y=-0.1** → Top at y=0.4, but CharacterController bottom at y=1.1 → 0.7 unit gap
4. **VCam Follow/LookAt not serializing** in batchmode
5. **PlayerInput added in batchmode** → InputActionAsset actionMaps empty at runtime
6. **RuntimeInitializeOnLoadMethod on systems** → Creates stale singletons across Play sessions
7. **Wrong execution order** → LightingSystem runs before DayNightCycle exists
8. **DayNightCycle Sun/Moon references** → SerializedObject doesn't persist cross-object refs in batchmode

---

## Files Modified

### 1. `Assets/Editor/FixMainScene.cs`
| Line | Change |
|------|--------|
| 623 | Added `health.Heal(100f)` after PlayerHealth creation |
| 500-508 | CollisionFloor localPosition: -0.1 → 0.5 (BoxCollider center, top=1.0) |
| 825 | Added `EditorUtility.SetDirty(vcamObj)` for VCam serialization |
| 630-677 | **Removed** PlayerInput creation in batchmode (serialization issues) |
| 723-740 | **Removed** CleanupDuplicateComponents PlayerInput logic |
| 816-838 | Enhanced DayNightCycle connection with reflection + SerializedObject + MarkSceneDirty |
| 220-250 | **Reordered**: CreateCoreGameSystems() now runs BEFORE CreateLightingSystem() |

### 2. `Assets/Scripts/Core/PlayerHealth.cs`
| Line | Change |
|------|--------|
| 70 | Commented out `DontDestroyOnLoad(gameObject)` |

### 3. `Assets/Scripts/Core/PlayerStats.cs`
| Line | Change |
|------|--------|
| 112 | Commented out `DontDestroyOnLoad(gameObject)` |

### 4. `Assets/Scripts/Core/PlayerInventory.cs`
| Line | Change |
|------|--------|
| 64 | Commented out `DontDestroyOnLoad(gameObject)` |

### 5. `Assets/GameSetup.cs`
| Area | Change |
|------|--------|
| PurgeRuntimeSingletons() | **Excluded** PlayerHealth, PlayerStats, PlayerInventory, PlayerCombat, BuffManager. Only purges: SoundManagerEnhanced, TerritoryManager, TimeManager, GameManager |
| SetupPlayerComponents() | PlayerInputHelper.SetupPlayerInputFromResources() with **fallback** to CreateFallbackPlayerInput() |
| CreateFallbackPlayerInput() | **New method**: Creates InputActionAsset programmatically with all actions (Move, Jump, Attack, Dash, Interact, Roll) |

### 6. `Assets/Scripts/Core/PlayerInputHelper.cs`
| Change |
|--------|
| Added `using UnityEngine; using UnityEngine.InputSystem; using System.Linq;` |
| SetupPlayerInput(): Returns null if actionMaps empty (triggers fallback) |
| SetupPlayerInputFromResources(): Checks `actions.actionMaps.Count == 0` and returns null |

### 7. `Assets/Scripts/Systems/TimeManager.cs`
| Change |
|--------|
| **Removed** all `[RuntimeInitializeOnLoadMethod]` attributes |
| Simplified to scene-based singleton with GetOrCreate() |

### 8. `Assets/Scripts/Systems/TerritoryManager.cs`
| Change |
|--------|
| **Removed** all `[RuntimeInitializeOnLoadMethod]` attributes |
| Simplified to scene-based singleton with GetOrCreate() |

### 9. `Assets/Scripts/Systems/SoundManagerEnhanced.cs`
| Change |
|--------|
| **Removed** all `[RuntimeInitializeOnLoadMethod]` attributes |
| Fixed GetOrCreate() to not call removed EnsureInstanceBeforeSceneLoad() |

### 10. `Assets/Editor/DayNightCycleReferenceFixer.cs` (NEW)
| Purpose |
|---------|
| `[InitializeOnLoad]` editor script that runs when MainScene opens |
| Uses reflection to find DayNightCycle and set _sunLight/_moonLight from scene objects |
| Fixes batchmode limitation where cross-object references don't persist |

---

## Scene Verification Results

| Check | Status |
|-------|--------|
| PlayerHealth `_currentHP: 100` / `_maxHP: 100` | ✅ |
| Player tag active (`m_IsActive: 1`) | ✅ |
| Player position (0, 2, 0) | ✅ |
| PlayerModel as child | ✅ |
| **No PlayerInput in scene** (added at runtime) | ✅ |
| CollisionFloor BoxCollider at y=0.5 (size 2000×1×2000) | ✅ |
| VCam TrackingTarget = Player | ✅ |
| VCam LookAtTarget = Player | ✅ |
| All Player components present | ✅ |
| Compilation errors: **0** | ✅ |
| DayNightCycle references | ⚠️ Auto-fixed on Editor scene open |

---

## Expected Play Mode Behavior

1. Scene loads → Player at (0, 2, 0) with 100 HP
2. GameSetup.Start():
   - Purges only system singletons (SoundManagerEnhanced, TerritoryManager, TimeManager)
   - Finds Player by tag → Adds PlayerInput via fallback (programmatic InputActionAsset)
3. CharacterController lands on CollisionFloor (top at y=1.0)
4. VCam follows Player smoothly
5. DayNightCycle references fixed automatically when scene opens in Editor

---

## Commands to Reproduce Fix

```bash
# Run FixMainScene in batchmode
cd /mnt/c/Unity/code
"/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe" -batchmode -projectPath "C:/Unity/code" -executeMethod FixMainScene.Fix -quit -logFile -
```

---

## Next Steps if Issues Persist

1. Open scene in Unity Editor → DayNightCycleReferenceFixer runs → saves scene
2. Enter Play Mode → Verify no console errors
3. Check: Player visible, terrain visible, camera follows, no falling