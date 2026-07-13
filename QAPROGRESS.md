# ✅ 포이즌 (Poison) — QA 진행 상황 (런타임 오류 점검)

> **목표:** 431개 스크립트를 하나씩 점검하며 런타임 오류(NullReference, MissingReference, IndexOutOfRange, 무한루프 등)를 잡아냅니다.
>
> **진행 방식:** 폴더별로 순차 점검. 컴파일 오류는 조용히 수정, Breaking Change만 중단 보고.
>
> **최종 갱신:** 2026-07-13

---

## 📊 전체 현황

| 폴더 | 전체 파일 | 점검 완료 | 남은 파일 | 진행률 |
|:----|:--------:|:--------:|:--------:|:-----:|
| **Core/Data** | 32 | 0 | 32 | 0% |
| **Core** (root) | 45 | 0 | 45 | 0% |
| **Systems** | 213 | 0 | 213 | 0% |
| **UI** | 84 | 0 | 84 | 0% |
| **UI/Core** | 23 | 0 | 23 | 0% |
| **UI/Themes** | 10 | 0 | 10 | 0% |
| **Effects** | 1 | 0 | 1 | 0% |
| **Utils** | 1 | 0 | 1 | 0% |
| **기타 (루트)** | 22 | 0 | 22 | 0% |
| **합계** | **431** | **0** | **431** | **0%** |

---

## ✅ 이미 수정 완료된 컴파일 오류 (ROADMAP.md 기준)

> 아래는 과거에 수정된 **컴파일 오류/경고**입니다. 런타임 점검과 별개로 기록 보존용입니다.

### 🔧 Core/Data 폴더 (2026-07-03)
- `PlayerStats.cs` — CS0105 중복 using 제거
- `BuffManager.cs` — CS0618 FindObjectOfType→FindAnyObjectByType
- `GameManager.cs` — CS0234 리플렉션 전환 + CS0618 ×4
- `PlayerHealth.cs` — CS0103 HapticFeedback→Debug.Log

### 🔧 Systems 폴더 (2026-07-03)
- ERROR 수정: `ArenaSystem`, `DraculaLord`, `GameEndingManager`, `NPCDailyCycle`, `NewGamePlusSystem`, `SpySystem`
- CS0618 경고 ~50개: FindObjectOfType/FindFirstObjectByType/FindObjectsOfType 마이그레이션
- 기타: `AIWarSystem`(nullable), `AutoRouteSystem`(unused var), 10개 파일 CS0414

### 🔧 MainScene 통합 (2026-07-08)
- 컴파일 오류 42건 전면 수정
- Player 컴포넌트 정리 (SnakeSlitherMotion 제거, 본 설정)
- Ground Mesh Cube→Plane 교체
- 씬 최종 점검 ✅

---

## 📋 런타임 오류 점검 진행표

> ⬜ = 미점검, 🔄 = 점검 중, ✅ = 점검 완료 (오류 없음), ⚠️ = 오류 발견+수정 완료

### Phase 1: Core/Data (32개)

| # | 파일 | 상태 | 발견된 오류 | 비고 |
|:-:|:----|:----:|:----------|:-----|
| 1 | `Core/Data/BiomeData.cs` | ⬜ | | |
| 2 | `Core/Data/ComboTester.cs` | ⬜ | | |
| 3 | `Core/Data/ConsumableSystem.cs` | ⬜ | | |
| 4 | `Core/Data/ConsumableTester.cs` | ⬜ | | |
| 5 | `Core/Data/CookingDatabase.cs` | ⬜ | | |
| 6 | `Core/Data/CookingTester.cs` | ⬜ | | |
| 7 | `Core/Data/DifficultyMode.cs` | ⬜ | | |
| 8 | `Core/Data/DishDatabase.cs` | ⬜ | | |
| 9 | `Core/Data/DishInfo.cs` | ⬜ | | |
| 10 | `Core/Data/DishTester.cs` | ⬜ | | |
| 11 | `Core/Data/DropTable.cs` | ⬜ | | |
| 12 | `Core/Data/DrugDatabase.cs` | ⬜ | | |
| 13 | `Core/Data/EncyclopediaData.cs` | ⬜ | | |
| 14 | `Core/Data/FoodEffectData.cs` | ⬜ | | |
| 15 | `Core/Data/GourmetDatabase.cs` | ⬜ | | |
| 16 | `Core/Data/Herb.cs` | ⬜ | | |
| 17 | `Core/Data/HerbComboDatabase.cs` | ⬜ | | |
| 18 | `Core/Data/HerbData.cs` | ⬜ | | |
| 19 | `Core/Data/HerbTester.cs` | ⬜ | | |
| 20 | `Core/Data/LevelGroupData.cs` | ⬜ | | |
| 21 | `Core/Data/MonsterData.cs` | ⬜ | | |
| 22 | `Core/Data/MonsterLevelData.cs` | ⬜ | | |
| 23 | `Core/Data/NPCData.cs` | ⬜ | | |
| 24 | `Core/Data/NationFlagData.cs` | ⬜ | | |
| 25 | `Core/Data/NationNames.cs` | ⬜ | | |
| 26 | `Core/Data/QuestChainData.cs` | ⬜ | | |
| 27 | `Core/Data/QuestData.cs` | ⬜ | | |
| 28 | `Core/Data/RevengeListData.cs` | ⬜ | | |
| 29 | `Core/Data/RingDifficultyData.cs` | ⬜ | | |
| 30 | `Core/Data/TerritoryData.cs` | ⬜ | | |
| 31 | `Core/Data/TerritoryDatabase.cs` | ⬜ | | |
| 32 | `Core/Data/TutorialGuideData.cs` | ⬜ | | |

### Phase 2: Core/root (45개)

| # | 파일 | 상태 | 발견된 오류 | 비고 |
|:-:|:----|:----:|:----------|:-----|
| 1 | `Core/AggroState.cs` | ⬜ | | |
| 2 | `Core/ArrowData.cs` | ⬜ | | |
| 3 | `Core/AudioConfig.cs` | ⬜ | | |
| 4 | `Core/BuffManager.cs` | ⬜ | | |
| 5 | `Core/BuildingEvents.cs` | ⬜ | | |
| 6 | `Core/CompileTestHelper.cs` | ⬜ | | |
| 7 | `Core/CraftingHelper.cs` | ⬜ | | |
| 8 | `Core/DifficultyManager.cs` | ⬜ | | |
| 9 | `Core/DrugEffectSystem.cs` | ⬜ | | |
| 10 | `Core/EquipmentPartConfig.cs` | ⬜ | | |
| 11 | `Core/EquipmentRarityData.cs` | ⬜ | | |
| 12 | `Core/GameManager.cs` | ⬜ | (컴파일 오류 수정완료) | |
| 13 | `Core/GameSetup.cs` | ⬜ | | |
| 14 | `Core/GasMaskSystem.cs` | ⬜ | | |
| 15 | `Core/HitVFX.cs` | ⬜ | | |
| 16 | `Core/IAggroable.cs` | ⬜ | | |
| 17 | `Core/IDamageable.cs` | ⬜ | | |
| 18 | `Core/ILevelLabel.cs` | ⬜ | | |
| 19 | `Core/ILootBasket.cs` | ⬜ | | |
| 20 | `Core/IWorldSpaceHUD.cs` | ⬜ | | |
| 21 | `Core/IndoorTextureGenerator.cs` | ⬜ | | |
| 22 | `Core/InteriorRandomizer.cs` | ⬜ | | |
| 23 | `Core/ItemRarity.cs` | ⬜ | | |
| 24 | `Core/LabelFactory.cs` | ⬜ | | |
| 25 | `Core/LevelGroupManager.cs` | ⬜ | | |
| 26 | `Core/LuckyRollSystem.cs` | ⬜ | | |
| 27 | `Core/MonsterData.cs` | ⬜ | | |
| 28 | `Core/NationFlagDatabase.cs` | ⬜ | | |
| 29 | `Core/PersistentManager.cs` | ⬜ | | |
| 30 | `Core/PlayerHealth.cs` | ⬜ | (컴파일 오류 수정완료) | |
| 31 | `Core/PlayerInventory.cs` | ⬜ | | |
| 32 | `Core/PlayerStats.cs` | ⬜ | (컴파일 오류 수정완료) | |
| 33 | `Core/ProceduralIconGenerator.cs` | ⬜ | | |
| 34 | `Core/QuestManager.cs` | ⬜ | | |
| 35 | `Core/RarityProbabilityTable.cs` | ⬜ | | |
| 36 | `Core/Recipe.cs` | ⬜ | | |
| 37 | `Core/RecipeDatabase.cs` | ⬜ | | |
| 38 | `Core/RecipeDiscoverySystem.cs` | ⬜ | | |
| 39 | `Core/SoundClipData.cs` | ⬜ | | |
| 40 | `Core/SoundManager.cs` | ⬜ | | |
| 41 | `Core/SoundType.cs` | ⬜ | | |
| 42 | `Core/Systems/EmpireAccessRule.cs` | ⬜ | | |
| 43 | `Core/TestILootBasket.cs` | ⬜ | | |
| 44 | `Core/TipDatabase.cs` | ⬜ | | |
| 45 | `Core/WeaponData.cs` | ⬜ | | |
| 46 | `Core/WaveformType.cs` | ⬜ | | |
| 47 | `Core/DropSystem/DropTable.cs` | ⬜ | | |
| 48 | `Core/DropSystem/DropTableManager.cs` | ⬜ | | |

### Phase 3: Systems (213개) — 가장 큰 폴더

> Systems 폴더는 아래 하위 그룹으로 나누어 점검합니다.

#### 3-A: 플레이어 & 전투 (Player, Combat)

| # | 파일 | 상태 | 발견된 오류 | 비고 |
|:-:|:----|:----:|:----------|:-----|
| 1 | `Systems/PlayerMovement.cs` | ⬜ | | |
| 2 | `Systems/PlayerPlaceholder.cs` | ⬜ | | |
| 3 | `Systems/PlayerCombat.cs` | ⬜ | | |
| 4 | `Systems/AttackSystem.cs` | ⬜ | | |
| 5 | `Systems/CombatCameraEffects.cs` | ⬜ | | |
| 6 | `Systems/CombatLog.cs` | ⬜ | | |
| 7 | `Systems/CombatSystem.cs` | ⬜ | | |
| 8 | `Systems/CombatVFXController.cs` | ⬜ | | |
| 9 | `Systems/HitReaction.cs` | ⬜ | | |
| 10 | `Systems/StealthAssassination.cs` | ⬜ | | |
| 11 | `Systems/StealthSystem.cs` | ⬜ | | |
| 12 | `Systems/DamageFont.cs` | ⬜ | | |
| 13 | `Systems/DeathEffects.cs` | ⬜ | | |
| 14 | `Systems/LootBasket.cs` | ⬜ | | |

#### 3-B: AI & 몬스터 (AI, Animals, Monsters)

| # | 파일 | 상태 | 발견된 오류 | 비고 |
|:-:|:----|:----:|:----------|:-----|
| 15 | `Systems/AnimalAI.cs` | ⬜ | | |
| 16 | `Systems/AIWarSystem.cs` | ⬜ | | |
| 17 | `Systems/MonsterAggroSystem.cs` | ⬜ | | |
| 18 | `Systems/MonsterSpawner.cs` | ⬜ | | |
| 19 | `Systems/MonsterManager.cs` | ⬜ | | |
| 20 | `Systems/MonsterAIData.cs` | ⬜ | | |

#### 3-C: 가드 & 병사 (Guard)

| # | 파일 | 상태 | 발견된 오류 | 비고 |
|:-:|:----|:----:|:----------|:-----|
| 21 | `Systems/GuardPlaceholder.cs` | ⬜ | | |
| 22 | `Systems/GuardManager.cs` | ⬜ | | |
| 23 | `Systems/GuardAddictionSystem.cs` | ⬜ | | |
| 24 | `Systems/GuardCombatAI.cs` | ⬜ | | |
| 25 | `Systems/GuardEquipmentSpawner.cs` | ⬜ | | |
| 26 | `Systems/GuardEquipmentSystem.cs` | ⬜ | | |
| 27 | `Systems/GuardHostilitySystem.cs` | ⬜ | | |
| 28 | `Systems/GuardLevelSystem.cs` | ⬜ | | |
| 29 | `Systems/GuardLoyaltySystem.cs` | ⬜ | | |
| 30 | `Systems/GuardRecruitSystem.cs` | ⬜ | | |
| 31 | `Systems/GuardResurrectionSystem.cs` | ⬜ | | |
| 32 | `Systems/GuardSelectionManager.cs` | ⬜ | | |
| 33 | `Systems/GuardStatusSystem.cs` | ⬜ | | |
| 34 | `Systems/GateGuardPlaceholder.cs` | ⬜ | | |
| 35 | `Systems/SkeletonGuardPlaceholder.cs` | ⬜ | | |

#### 3-D: 영지 & 전쟁 (Territory, War)

| # | 파일 | 상태 | 발견된 오류 | 비고 |
|:-:|:----|:----:|:----------|:-----|
| 36 | `Systems/TerritoryManager.cs` | ⬜ | | |
| 37 | `Systems/TerritoryBuilder.cs` | ⬜ | | |
| 38 | `Systems/TerritoryCaptureSystem.cs` | ⬜ | | |
| 39 | `Systems/TerritoryBattleManager.cs` | ⬜ | | |
| 40 | `Systems/TerritoryWarManager.cs` | ⬜ | | |
| 41 | `Systems/TerritoryBannerSystem.cs` | ⬜ | | |
| 42 | `Systems/TerritoryBiomeMapper.cs` | ⬜ | | |
| 43 | `Systems/TerritoryQuestDefinitions.cs` | ⬜ | | |
| 44 | `Systems/WarNotificationUI.cs` | ⬜ | | |
| 45 | `Systems/AlarmSystem.cs` | ⬜ | | |
| 46 | `Systems/LordSurrenderSystem.cs` | ⬜ | | |
| 47 | `Systems/PoisonTakeoverSystem.cs` | ⬜ | | |

#### 3-E: NPC & 퀘스트

| # | 파일 | 상태 | 발견된 오류 | 비고 |
|:-:|:----|:----:|:----------|:-----|
| 48 | `Systems/NpcQuestGiver.cs` | ⬜ | | |
| 49 | `Systems/NpcDialogueData.cs` | ⬜ | | |
| 50 | `Systems/QuestChainManager.cs` | ⬜ | | |
| 51 | `Systems/QuestMarkerSystem.cs` | ⬜ | | |
| 52 | `Systems/TutorialQuestManager.cs` | ⬜ | | |
| 53 | `Systems/TutorialQuestChainDefinitions.cs` | ⬜ | | |
| 54 | `Systems/TutorialGuideSystem.cs` | ⬜ | | |
| 55 | `Systems/DraculaLord.cs` | ⬜ | (컴파일 오류 수정완료) | |
| 56 | `Systems/DraculaTerritoryController.cs` | ⬜ | | |

#### 3-F: 제작 & 인벤토리 (Craft, Inventory)

| # | 파일 | 상태 | 발견된 오류 | 비고 |
|:-:|:----|:----:|:----------|:-----|
| 57 | `Systems/CraftSuccessSystem.cs` | ⬜ | | |
| 58 | `Systems/CraftResultPopup.cs` | ⬜ | | |
| 59 | `Systems/CraftPresetManager.cs` | ⬜ | | |
| 60 | `Systems/EquipmentManager.cs` | ⬜ | | |
| 61 | `Systems/EquipmentDurabilitySystem.cs` | ⬜ | | |
| 62 | `Systems/EquipmentRepairSystem.cs` | ⬜ | | |
| 63 | `Systems/BackSlotSystem.cs` | ⬜ | | |
| 64 | `Systems/QuickSlotManager.cs` | ⬜ | | |
| 65 | `Systems/WeaponPartsSystem.cs` | ⬜ | | |
| 66 | `Systems/HerbPickup.cs` | ⬜ | | |
| 67 | `Systems/HerbVisualState.cs` | ⬜ | | |
| 68 | `Systems/HerbGatheringMission.cs` | ⬜ | | |
| 69 | `Systems/HuntingMission.cs` | ⬜ | | |
| 70 | `Systems/ResourceNode.cs` | ⬜ | | |

#### 3-G: 카메라 & 지형 (Camera, Terrain)

| # | 파일 | 상태 | 발견된 오류 | 비고 |
|:-:|:----|:----:|:----------|:-----|
| 71 | `Systems/TopDownCameraController.cs` | ⬜ | | |
| 72 | `Systems/TerrainGenerator.cs` | ⬜ | | |
| 73 | `Systems/TerrainHeightApplier.cs` | ⬜ | | |
| 74 | `Systems/TerrainPathGenerator.cs` | ⬜ | | |
| 75 | `Systems/TerrainPropPlacer.cs` | ⬜ | | |
| 76 | `Systems/TerrainTextureApplier.cs` | ⬜ | | |
| 77 | `Systems/TerrainTextureGenerator.cs` | ⬜ | | |
| 78 | `Systems/TerrainTransitionManager.cs` | ⬜ | | |
| 79 | `Systems/NationTerrainController.cs` | ⬜ | | |
| 80 | `Systems/LakeGenerator.cs` | ⬜ | | |
| 81 | `Systems/WaterBody.cs` | ⬜ | | |
| 82 | `Systems/WaterSystem.cs` | ⬜ | | |
| 83 | `Systems/WaterMaterialUpgrader.cs` | ⬜ | | |
| 84 | `Systems/GrassRenderer.cs` | ⬜ | | |
| 85 | `Systems/SwayController.cs` | ⬜ | | |

#### 3-H: 시간 & 날씨 (Time, Weather)

| # | 파일 | 상태 | 발견된 오류 | 비고 |
|:-:|:----|:----:|:----------|:-----|
| 86 | `Systems/TimeManager.cs` | ⬜ | | |
| 87 | `Systems/DayNightCycle.cs` | ⬜ | | |
| 88 | `Systems/TimeDisplayUI.cs` | ⬜ | | |
| 89 | `Systems/StarField.cs` | ⬜ | | |
| 90 | `Systems/TimeOfDayEffects.cs` | ⬜ | | |
| 91 | `Systems/WeatherManager.cs` | ⬜ | | |
| 92 | `Systems/WeatherEffects.cs` | ⬜ | | |
| 93 | `Systems/WeatherParticleController.cs` | ⬜ | | |
| 94 | `Systems/EnvironmentParticleController.cs` | ⬜ | | |

#### 3-I: 애니메이션 & 모션 (Animation, Motion)

| # | 파일 | 상태 | 발견된 오류 | 비고 |
|:-:|:----|:----:|:----------|:-----|
| 95 | `Systems/RigAnimationController.cs` | ⬜ | | |
| 96 | `Systems/AnimationRiggingSetup.cs` | ⬜ | | |
| 97 | `Systems/AnimationBoneDefinitions.cs` | ⬜ | | |
| 98 | `Systems/AnimationMotionController.cs` | ⬜ | | |
| 99 | `Systems/TwoBoneIKController.cs` | ⬜ | | |
| 100 | `Systems/BoneDefs.cs` | ⬜ | | |
| 101 | `Systems/Motions/*` (13 files) | ⬜ | | |

#### 3-J: 가스 & 폭탄 & 특수장비

| # | 파일 | 상태 | 발견된 오류 | 비고 |
|:-:|:----|:----:|:----------|:-----|
| 102 | `Systems/GasSprayer.cs` | ⬜ | | |
| 103 | `Systems/GasSprayerController.cs` | ⬜ | | |
| 104 | `Systems/GasSprayerSystem.cs` | ⬜ | | |
| 105 | `Systems/GasPotionLoader.cs` | ⬜ | | |
| 106 | `Systems/GasMaskController.cs` | ⬜ | | |
| 107 | `Systems/SprayInputHandler.cs` | ⬜ | | |
| 108 | `Systems/SprayVFX.cs` | ⬜ | | |
| 109 | `Systems/Bomb.cs` | ⬜ | | |
| 110 | `Systems/BombThrower.cs` | ⬜ | | |

#### 3-K: 건물 & 인테리어

| # | 파일 | 상태 | 발견된 오류 | 비고 |
|:-:|:----|:----:|:----------|:-----|
| 111 | `Systems/IndoorBuilder.cs` | ⬜ | | |
| 112 | `Systems/IndoorFurniturePlacer.cs` | ⬜ | | |
| 113 | `Systems/IndoorLighting.cs` | ⬜ | | |
| 114 | `Systems/IndoorTransitionSetup.cs` | ⬜ | | |
| 115 | `Systems/BuildingPlaceholder.cs` | ⬜ | | |
| 116 | `Systems/BuildingTrigger.cs` | ⬜ | | |
| 117 | `Systems/CastleInteriorBuilder.cs` | ⬜ | | |
| 118 | `Systems/CaveInteriorBuilder.cs` | ⬜ | | |
| 119 | `Systems/HouseInteriorBuilder.cs` | ⬜ | | |
| 120 | `Systems/TavernInteriorBuilder.cs` | ⬜ | | |
| 121 | `Systems/BarnInteriorBuilder.cs` | ⬜ | | |
| 122 | `Systems/TownBuilder.cs` | ⬜ | | |
| 123 | `Systems/FadeManager.cs` | ⬜ | | |

#### 3-L: 저장 & 로딩 & UI 인프라

| # | 파일 | 상태 | 발견된 오류 | 비고 |
|:-:|:----|:----:|:----------|:-----|
| 124 | `Systems/SaveManager.cs` | ⬜ | | |
| 125 | `Systems/SaveData.cs` | ⬜ | | |
| 126 | `Systems/SaveSlotUI.cs` | ⬜ | | |
| 127 | `Systems/LoadingManager.cs` | ⬜ | | |
| 128 | `Systems/LoadingScreenUI.cs` | ⬜ | | |
| 129 | `Systems/LoadGameUI.cs` | ⬜ | | |
| 130 | `Systems/MainMenuUI.cs` | ⬜ | | |
| 131 | `Systems/EscMenuUI.cs` | ⬜ | | |
| 132 | `Systems/SettingsMenuUI.cs` | ⬜ | | |
| 133 | `Systems/DeathScreenUI.cs` | ⬜ | | |
| 134 | `Systems/OptionsUI.cs` | ⬜ | | |

#### 3-M: 상점 & 용병 & 시스템

| # | 파일 | 상태 | 발견된 오류 | 비고 |
|:-:|:----|:----:|:----------|:-----|
| 135 | `Systems/WanderingMerchant.cs` | ⬜ | | |
| 136 | `Systems/MercenaryManager.cs` | ⬜ | | |
| 137 | `Systems/MercenaryData.cs` | ⬜ | | |
| 138 | `Systems/BardMercenary.cs` | ⬜ | | |
| 139 | `Systems/BardBuffManager.cs` | ⬜ | | |
| 140 | `Systems/ChurchSystem.cs` | ⬜ | | |
| 141 | `Systems/WarehouseSystem.cs` | ⬜ | | |
| 142 | `Systems/ArenaSystem.cs` | ⬜ | (컴파일 오류 수정완료) | |
| 143 | `Systems/EnvoySystem.cs` | ⬜ | | |
| 144 | `Systems/SpySystem.cs` | ⬜ | (컴파일 오류 수정완료) | |
| 145 | `Systems/NewGamePlusSystem.cs` | ⬜ | (컴파일 오류 수정완료) | |
| 146 | `Systems/GameEndingManager.cs` | ⬜ | (컴파일 오류 수정완료) | |
| 147 | `Systems/NPCDailyCycle.cs` | ⬜ | (컴파일 오류 수정완료) | |

#### 3-N: 사운드 & VFX

| # | 파일 | 상태 | 발견된 오류 | 비고 |
|:-:|:----|:----:|:----------|:-----|
| 148 | `Systems/SoundManagerEnhanced.cs` | ⬜ | | |
| 149 | `Systems/SoundEffectManager.cs` | ⬜ | | |
| 150 | `Systems/SoundRefinement.cs` | ⬜ | | |
| 151 | `Systems/BackgroundMusicManager.cs` | ⬜ | | |
| 152 | `Systems/RegionBGMController.cs` | ⬜ | | |
| 153 | `Systems/UISoundManager.cs` | ⬜ | | |
| 154 | `Systems/CombatVFXController.cs` | ⬜ | | |
| 155 | `Systems/SpecialEffectsController.cs` | ⬜ | | |
| 156 | `Systems/BiomeEffectController.cs` | ⬜ | | |
| 157 | `Systems/AmbientEffectManager.cs` | ⬜ | | |

#### 3-O: 기타 Systems

| # | 파일 | 상태 | 발견된 오류 | 비고 |
|:-:|:----|:----:|:----------|:-----|
| 158 | `Systems/AccessibilityManager.cs` | ⬜ | | |
| 159 | `Systems/AutoMissionManager.cs` | ⬜ | | |
| 160 | `Systems/AutoMoveManager.cs` | ⬜ | | |
| 161 | `Systems/AutoRouteSystem.cs` | ⬜ | | |
| 162 | `Systems/FastTravelSystem.cs` | ⬜ | | |
| 163 | `Systems/BackSlotUI.cs` | ⬜ | | |
| 164 | `Systems/ControllerSupport.cs` | ⬜ | | |
| 165 | `Systems/FestivalManager.cs` | ⬜ | | |
| 166 | `Systems/FestivalData.cs` | ⬜ | | |
| 167 | `Systems/FestivalDefinitions.cs` | ⬜ | | |
| 168 | `Systems/FestivalNPC.cs` | ⬜ | | |
| 169 | `Systems/FishingSystem.cs` | ⬜ | | |
| 170 | `Systems/FlagManager.cs` | ⬜ | | |
| 171 | `Systems/FlagPoleDisplay.cs` | ⬜ | | |
| 172 | `Systems/NationFlagVisualData.cs` | ⬜ | | |
| 173 | `Systems/NationReputationSystem.cs` | ⬜ | | |
| 174 | `Systems/GameStatsCollector.cs` | ⬜ | | |
| 175 | `Systems/HapticFeedback.cs` | ⬜ | | |
| 176 | `Systems/InteractableDocument.cs` | ⬜ | | |
| 177 | `Systems/ItemTooltipData.cs` | ⬜ | | |
| 178 | `Systems/LockpickingSystem.cs` | ⬜ | | |
| 179 | `Systems/LockpickItem.cs` | ⬜ | | |
| 180 | `Systems/LockedDoor.cs` | ⬜ | | |
| 181 | `Systems/RTSCommandSystem.cs` | ⬜ | | |
| 182 | `Systems/ReadableDocument.cs` | ⬜ | | |
| 183 | `Systems/RevengeListIntegration.cs` | ⬜ | | |
| 184 | `Systems/RuntimeModelLoader.cs` | ⬜ | | |
| 185 | `Systems/SleepUI.cs` | ⬜ | | |
| 186 | `Systems/StealthAssassination.cs` | ⬜ | | |
| 187 | `Systems/Transitions.cs` | ⬜ | | |
| 188 | `Systems/WorldEventManager.cs` | ⬜ | | |
| 189 | `Systems/ArrowManager.cs` | ⬜ | | |
| 190 | `Systems/ArrowProjectile.cs` | ⬜ | | |
| 191 | `Systems/AssassinationCutscene.cs` | ⬜ | | |
| 192 | `Systems/BloodStain.cs` | ⬜ | | |
| 193 | `Systems/CursedObject.cs` | ⬜ | | |
| 194 | `Systems/DeadBodyWithNote.cs` | ⬜ | | |
| 195 | `Systems/DecalSpawner.cs` | ⬜ | | |
| 196 | `Systems/DecalSpawnerIntegration.cs` | ⬜ | | |
| 197 | `Systems/Gravestone.cs` | ⬜ | | |
| 198 | `Systems/EmblemManager.cs` | ⬜ | | |
| 199 | `Systems/Bed.cs` | ⬜ | | |
| 200 | `Systems/AmbientDialogueManager.cs` | ⬜ | | |
| 201 | `Systems/EncyclopediaManager.cs` | ⬜ | | |
| 202 | `Systems/EncyclopediaDataInitializer.cs` | ⬜ | | |
| 203 | `Systems/OpeningCutscene.cs` | ⬜ | | |
| 204 | `Systems/PhaseG2_SkyboxSetup.cs` | ⬜ | | |
| 205 | `Systems/PhaseG2_VolumetricFogSetup.cs` | ⬜ | | |
| 206 | `Systems/VolumeOverrides/PhaseG2_PostProcessingSetup.cs` | ⬜ | | |
| 207 | `Systems/GemChest.cs` | ⬜ | | |
| 208 | `Systems/GemData.cs` | ⬜ | | |
| 209 | `Systems/MonsterLevelLabel.cs` | ⬜ | | |
| 210 | `Systems/GuardWorldSpaceHUD.cs` | ⬜ | | |
| 211 | `Systems/EmblemMeshBuilder.cs` | ⬜ | | |
| 212 | `Systems/TutorialActionDetector.cs` | ⬜ | | |
| 213 | `Systems/OpeningCutscene.cs` | ⬜ | | |

### Phase 4: UI (84개)

| # | 파일 | 상태 | 발견된 오류 | 비고 |
|:-:|:----|:----:|:----------|:-----|
| 1 | `UI/UIWindow.cs` | ⬜ | | |
| 2 | `UI/UIStyleManager.cs` | ⬜ | | |
| 3 | `UI/UIManager.cs` (UI/Core) | ⬜ | | |
| 4 | `UI/KeyBindings.cs` | ⬜ | | |
| 5 | `UI/HUD.cs` | ⬜ | | |
| 6 | `UI/MinimapUI.cs` | ⬜ | | |
| 7 | `UI/InventoryWindow.cs` | ⬜ | | |
| 8 | `UI/QuestWindow.cs` | ⬜ | | |
| 9 | `UI/RecipeWindow.cs` | ⬜ | | |
| 10 | `UI/MapWindow.cs` | ⬜ | | |
| 11 | `UI/LootWindow.cs` | ⬜ | | |
| 12 | `UI/ShopWindow.cs` | ⬜ | | |
| 13 | `UI/CraftingUI.cs` | ⬜ | | |
| 14 | `UI/CookingUI.cs` | ⬜ | | |
| 15 | `UI/AlchemyUI.cs` | ⬜ | | |
| 16 | `UI/TooltipWindow.cs` | ⬜ | | |
| 17 | `UI/PlayerStatusWindow.cs` (UI/Windows) | ⬜ | | |
| 18 | `UI/EquipmentWindow.cs` | ⬜ | | |
| 19 | `UI/WarehouseUI.cs` | ⬜ | | |
| 20 | `UI/ChurchUI.cs` | ⬜ | | |
| 21 | `UI/ChurchSystemUI.cs` | ⬜ | | |
| 22 | `UI/RevengeListWindow.cs` | ⬜ | | |
| 23 | `UI/LordAudienceUI.cs` | ⬜ | | |
| 24 | `UI/NPCDialogueWindow.cs` | ⬜ | | |
| 25 | `UI/QuestJournalUI.cs` | ⬜ | | |
| 26 | `UI/MercenaryHireUI.cs` | ⬜ | | |
| 27 | `UI/QuestChoiceUI.cs` | ⬜ | | |
| 28 | `UI/QuestRewardPreview.cs` | ⬜ | | |
| 29+ | `UI/*` (55개 추가) | ⬜ | | |

### Phase 5: UI/Core (23개)

| # | 파일 | 상태 | 발견된 오류 | 비고 |
|:-:|:----|:----:|:----------|:-----|
| 1 | `UI/Core/UIManager.cs` | ⬜ | | |
| 2 | `UI/Core/CanvasController.cs` | ⬜ | | |
| 3 | `UI/Core/ScreenManager.cs` | ⬜ | | |
| 4 | `UI/Core/ThemeManager.cs` | ⬜ | | |
| 5 | `UI/Core/SignalManager.cs` | ⬜ | | |
| 6 | `UI/Core/MessageSystem.cs` | ⬜ | | |
| 7 | `UI/Core/EventSystemManager.cs` | ⬜ | | |
| 8 | `UI/Core/ComponentManager.cs` | ⬜ | | |
| 9 | `UI/Core/LocalizationManager.cs` | ⬜ | | |
| 10 | `UI/Core/DragDropManager.cs` | ⬜ | | |
| 11 | `UI/Core/AbilityManager.cs` | ⬜ | | |
| 12 | `UI/Core/IUIComponent.cs` | ⬜ | | |
| 13 | `UI/Core/ICanvasComponent.cs` | ⬜ | | |
| 14 | `UI/Core/IDragDropHandler.cs` | ⬜ | | |
| 15 | `UI/Core/ColorPalette.cs` | ⬜ | | |
| 16 | `UI/Core/ToolTipManager.cs` | ⬜ | | |
| 17 | `UI/Core/GameEventSystem.cs` | ⬜ | | |
| 18 | `UI/Core/Transitions/AnimatedPanel.cs` | ⬜ | | |
| 19 | `UI/Core/Transitions/ColorTransition.cs` | ⬜ | | |
| 20 | `UI/Core/Transitions/PanelTransition.cs` | ⬜ | | |
| 21 | `UI/Core/Transitions/Transition.cs` | ⬜ | | |
| 22 | `UI/Core/Transitions/TransitionManager.cs` | ⬜ | | |
| 23 | `UI/Core/Transitions/TransitionType.cs` | ⬜ | | |

### Phase 6: UI/Themes (10개)

| # | 파일 | 상태 | 발견된 오류 | 비고 |
|:-:|:----|:----:|:----------|:-----|
| 1 | `UI/Themes/UIDesignTheme.cs` | ⬜ | | |
| 2 | `UI/Themes/UIDesignManager.cs` | ⬜ | | |
| 3 | `UI/Themes/Phase33_Themes.cs` | ⬜ | | |
| 4 | `UI/Themes/ProceduralTextureGenerator.cs` | ⬜ | | |
| 5 | `UI/Themes/MedievalUIResources.cs` | ⬜ | | |
| 6 | `UI/Themes/MedievalBackgroundRenderer.cs` | ⬜ | | |
| 7 | `UI/Themes/GradientBackgroundRenderer.cs` | ⬜ | | |
| 8 | `UI/Themes/DecorativeBorderRenderer.cs` | ⬜ | | |
| 9 | `UI/Themes/UIImageThemeExtensions.cs` | ⬜ | | |
| 10 | `UI/Themes/WindowAnimationProfile.cs` | ⬜ | | |

### Phase 7: Effects & Utils (2개)

| # | 파일 | 상태 | 발견된 오류 | 비고 |
|:-:|:----|:----:|:----------|:-----|
| 1 | `Effects/PoisonVFX.cs` | ⬜ | | |
| 2 | `Utils/MaterialHelper.cs` | ⬜ | | |

---

## 📝 발견된 런타임 오류 로그

| 일시 | 파일 | 오류 유형 | 내용 | 상태 |
|:----:|:----|:---------|:-----|:----:|
| — | — | — | — | — |

---

## 📐 점검 기준 (체크리스트)

각 파일 점검 시 아래 항목을 확인합니다:

- [ ] **NullReferenceException** — `.` 호출 전 null 체크 누락
- [ ] **MissingReferenceException** — Destroy된 오브젝트 참조
- [ ] **IndexOutOfRangeException** — 배열/리스트 인덱스 검증
- [ ] **ArgumentNullException** — null 파라미터 전달
- [ ] **InfiniteLoop/StackOverflow** — 재귀/while(true) 무한루프
- [ ] **DivideByZeroException** — 0으로 나누기
- [ ] **InvalidCastException** — 타입 캐스팅 실패
- [ ] **MissingComponentException** — GetComponent 실패
- [ ] **UnassignedReferenceException** — SerializeField 미할당
- [ ] **ArgumentException (경로/키)** — Dictionary 키 없음, 경로 오류
- [ ] **Coroutine 누수** — 중단되지 않은 코루틴
- [ ] **Event 구독 해제 누락** — OnDestroy/OnDisable에서 -= 누락