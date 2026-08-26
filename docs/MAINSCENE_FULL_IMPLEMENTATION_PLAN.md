# 📋 MainScene 완전 구현 상세 계획서 (v3 - 검증 완료, 실행 가능)

> **목표**: 메인씬 PlayMode 진입 시 **에러 0개**, 플레이어/몬스터/병사 GLB가 애니메이션과 함께 자연스럽게 움직이고, HUD/미니맵/환경/영지/전쟁/크래프트/편의 시스템이 모두 작동하는 화면 구현
> 
> **검증 완료 사항** (2026-08-23 직접 확인):
> - ✅ `Player_Rigged.glb` 존재 (4MB, 휴머노이드 본 포함)
> - ✅ Neural ONNX 100+ 모델 존재 (`NeuralModels/` 폴더: base/bc_/ensemble/kd_/LOD/fp32/int8)
> - ✅ `TerrainGenerator.GenerateTerrain()` 작동 확인
> - ✅ `AnimalAI` / `TerritoryManager` / `GuardManager` / `GuardPlaceholder` / `MonsterSpawner` 존재
> - ✅ `NeuralModelDatabase` + `NeuralModelAutoSetup` 존재 (자동 발견/매핑 로직 완성)
> - ✅ `HUD` ← `PlayerHealth.OnHPChanged` 이벤트 구독 구조 확인
> - ✅ `PlayerHealth` / `PlayerMovement` / `PlayerStats` / `PlayerInventory` / `PlayerCombat` / `BombThrower` / `BuffManager` 존재 (올바른 네임스페이스)

> **핵심 결함 (즉시 수정 필요)**:
> - ❌ `ModelAnimatorAssigner` **존재하지 않음** (신규 생성 필요)
> - ❌ `TerrainModelPlacer` **존재하지 않음** (신규 생성 필요)  
> - ❌ `FixMainScene.cs` **불완전**: GLB 로드/애니메이션 연동/중복컴포넌트정리/환경시스템시작/스포너연동 모두 누락
> - ❌ `NeuralModelAutoSetup` 미실행 → `NeuralModelDatabase.asset` 비어있음
> - ❌ 중복 컴포넌트 문제 (Rigidbody×2, Animator×2, PlayerInput actions=null) 해결 안 됨

---

## 🗂️ 에셋/시스템 현황표 (확정)

| 카테고리 | 상태 | 경로/비고 |
|---------|------|-----------|
| **플레이어 GLB** | ✅ 보유 | `Assets/Resources/Models/UserProvided/Player_Rigged.glb` |
| **병사 GLB (3종)** | ✅ 보유 | `Soldier_Lv1-20/20-40/40-50_Rigged.glb` |
| **몬스터 GLB (22종)** | ✅ 보유 | `Rabbit/Boar/Wolf/Deer/Slime/Golem/.../Monstrous_Deep_Clam` |
| **지형 GLB** | ✅ 보유 | `grass1~7`, `rock1~5`, `tree1~6` |
| **건물 GLB** | ✅ 보유 | `hut`, `shop`, `craft_*`, `castle`, `blue/green/red/purple_castle`, `bar` |
| **국가별 텍스처** | ✅ 보유 | 17종 PNG (`east_grass*`, `west_sand*`, `south_red*`, `north_snow*`, `empire_marble`) |
| **Neural ONNX** | ✅ 보유 | 100+ 모델 (`bc_`, `ensemble`, `kd_`, LOD, fp32/int8) |
| **TerrainGenerator** | ✅ 완성 | `Assets/Scripts/Systems/TerrainGenerator.cs` |
| **AnimalAI** | ✅ 완성 | Rig/Neural 애니메이션, MonsterSkillSystem 연동 |
| **TerritoryManager** | ✅ 완성 | CurrentTerritoryId, SpawnTerritoryNPCs (리플렉션) |
| **GuardManager** | ✅ 완성 | 재충원/사망/퇴각 이벤트 |
| **GuardPlaceholder** | ✅ 완성 | Rig/NPCAwareness, 상호작용 |
| **MonsterSpawner** | ✅ 완성 | 거리/시간대 기반, C18-02/03/04 |
| **NeuralModelAutoSetup** | ✅ 완성 | `bc_`/`neural_` 프리픽스 필터, INT8 제외, PolicyType 매핑 |
| **HUD/MinimapUI** | ✅ 완성 | IMGUI 기반, 이벤트 구독 구조 |
| **PlayerHealth** | ✅ 완성 | `OnHPChanged` / `OnPlayerDied` / `OnPlayerRespawned` 이벤트 |

---

## 🗓️ 실행 페이즈 (의존성 순서 엄수, 총 37-50h)

### Phase M1: 플레이어 GLB + 애니메이션 완전 연동 + 중복컴포넌트 해결 (최우선, 5-6h)

#### M1-1: ModelAnimatorAssigner 신규 생성
```csharp
// 파일: Assets/Scripts/Systems/Animation/ModelAnimatorAssigner.cs
// 역할: GLB 모델 타입(2족/4족) 감지 → 적절한 애니메이션 컨트롤러 자동 부착
// 부착 대상: PlayerModel, Monster, GuardPlaceholder
// 로직:
// 1. Animator 확인 → 휴머노이드 본 구조면 Biped, 아니면 Quadruped/Special
// 2. Biped: ProceduralAnimationController + NeuralAnimationController + HybridAnimationController
// 3. Quadruped: QuadrupedProceduralLocomotion + NeuralAnimationController
// 4. NeuralModelDatabase에서 PolicyType별 모델 경로 조회 → MLRuntimeManager로 로드
// 5. ProgressiveRolloutManager.ConfigureHybridController() 호출
```

#### M1-2: FixMainScene.cs 완전 재작성 (핵심)
```csharp
// 파일: Assets/Editor/FixMainScene.cs
// 메뉴: Tools/Poison/Fix MainScene
// 실행 시 다음 모두 수행:
public static void Fix() {
    // 1. 씬 초기화 (빈 씬)
    // 2. URP + UniversalRendererData 생성/할당
    // 3. Heightmap 지형 생성 (TerrainGenerator, 1000x1000, seed=42)
    // 4. TerrainModelPlacer로 GLB 환경 모델 배치 (grass/rock/tree 인스턴싱)
    // 5. 플레이어 생성:
    //    - Player GameObject + CharacterController
    //    - Player_Rigged.glb 로드 → Player/Model로 배치 (Y=0.9, Scale=1)
    //    - 중복 컴포넌트 정리: CleanupDuplicateComponents(player)
    //    - ModelAnimatorAssigner 강제 부착 (PlayerModel에)
    //    - PlayerInput.actions = PlayerControls.inputactions 할당
    //    - 모든 Core/Systems 컴포넌트 추가 (올바른 네임스페이스)
    // 6. 카메라: Main Camera(Brain) → Player Camera(VCam+Follow+InputAxis) 올바른 계층
    // 7. 환경 시스템 시작: DayNightCycle, WeatherManager, RegionBGMController, 
    //    EnvironmentParticleController, DecalSpawner, SpecialEffectsController
    // 8. 스포너 설정: MonsterSpawner(영지 난이도 연동), GuardManager, TerritoryManager
    // 9. HUD/MinimapUI/EventSystem/QuickSlotUI 등 UI 생성
    // 10. 씬 저장
}

static void CleanupDuplicateComponents(GameObject player) {
    // Rigidbody 중복 제거 (1개만 남김)
    var rbs = player.GetComponents<Rigidbody>();
    for (int i = 1; i < rbs.Length; i++) DestroyImmediate(rbs[i]);
    
    // Animator 중복 제거 (PlayerModel에만 있어야 함)
    var anims = player.GetComponents<Animator>();
    for (int i = 0; i < anims.Length; i++) DestroyImmediate(anims[i]);
    
    // PlayerInput actions null 체크
    var input = player.GetComponent<PlayerInput>();
    if (input != null && input.actions == null) {
        input.actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Resources/Input/PlayerControls.inputactions");
    }
}
```

#### M1-3: NeuralModelAutoSetup 실행 + 검증
```bash
# Unity batchmode로 실행:
cd /mnt/c/Unity/code
"/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe" -batchmode -projectPath "C:/Unity/code" -executeMethod NeuralModelAutoSetup.AutoSetupModelDatabase -quit -logFile -
# 검증: Tools/Neural/Validate Model Database → 20개 PolicyType 모두 매핑 확인
```

**검증 기준 (M1 완료 시)**:
- [ ] PlayMode 진입 → Player_Rigged.glb 표시 + Idle 애니메이션
- [ ] WASD → Walk/Run 전이 자연스러움, 발 IK 지형 추종
- [ ] Space/Shift/Q → Jump/Dash/Roll 애니메이션 작동
- [ ] 콘솔: "NeuralAnimationController: Policy loaded" 로그 확인
- [ ] 중복 컴포넌트 없음 (Rigidbody 1개, Animator 1개 on PlayerModel)

---

### Phase M2: 지형 Heightmap + GLB 환경 모델 배치 (4-5h)

#### M2-1: TerrainModelPlacer 신규 생성
```csharp
// 파일: Assets/Scripts/Systems/TerrainModelPlacer.cs
// 역할: Heightmap 메시에 UserProvided GLB 인스턴싱 배치
// 호출: FixMainScene → CreateHeightmapTerrain() 후 호출
// 로직:
// 1. Resources.LoadAll<GameObject>("Models/UserProvided/terrain/grass/rock/trees")
// 2. NationTerrainController의 3링 + 국가별 텍스처 구역 계산
// 3. 링별 배치:
//    - Ring 1 (0-350m): grass1~3 + tree1~2 + rock1~2
//    - Ring 2 (350-700m): grass4~5 + tree3~4 + rock3
//    - Ring 3 (700-1000m): grass6~7 + tree5~6 + rock4~5
// 4. 국가 경계 블렌딩 존: 양쪽 국가 모델 섞어서 배치
// 5. Raycast(Heightmap) → Y = terrainHeight + Random(0.1~0.5)
// 6. GPU Instancing 활성화 (동일 메쉬/머티리얼 → SRP Batcher)
// 7. LODGroup 설정: 거리별 메쉬 교체/컬링
```

#### M2-2: 물 시스템 연동
```csharp
// LakeGenerator.cs + WaterBody.cs 활용
// Heightmap 저지대(y < 2m) 자동 물 메시 생성
// URP 물 쉐이더 (반투명, 약한 반사, 출렁임)
```

**검증 기준 (M2 완료 시)**:
- [ ] PlayMode → 언덕/계곡 지형 + GLB 나무/바위/잔디 배치
- [ ] 캐릭터 경사면 이동 정상 (Slope Limit 45도, Step Offset 0.3m)
- [ ] 국가 경계에서 텍스처/모델 자연스러운 블렌딩
- [ ] 물 구역에서 이동속도 저하 + 수영 상태 전이

---

### Phase M3: 몬스터 22종 GLB 스폰 + AI 애니메이션 (5-6h)

#### M3-1: MonsterSpawner 영지 난이도 연동 수정
```csharp
// 수정: Assets/Scripts/Systems/MonsterSpawner.cs
// 기존: 거리 기반 스폰 (safeRadius/beginnerInner...)
// 변경: TerritoryManager.CurrentTerritoryId → RingDifficultyData.GetMonsterTiersForDifficulty()
// GLB 로드: Resources.LoadAll<GameObject>("Models/UserProvided/") → 몬스터 ID 매핑
// ModelAnimatorAssigner 부착으로 4족/비인간형/비행/수영 분기
```

**몬스터별 애니메이션 분기 매핑 (확정)**:
```csharp
// ModelAnimatorAssigner에서 자동 분기:
Biped (2족 휴머노이드): Shadow_Assassin → Biped Procedural + Neural Combat/React
Quadruped (4족): Wolf/Boar/Deer/Rabbit/Fox/Bear/Slime/Golem/Fire_Lizard/Salamander/
                  Swamp_Alligator/Snake/Hedgehog/Wild_Troll/Swamp_Ogre/Minotaur/
                  Wooden_Forest_Spirit → QuadrupedProceduralLocomotion + Neural Combat/React
Special (비인간형 특수): Spider/Clam → SpecialCreatureAnimator (신규 모듈)
Fly: Griffon/Banshee/Manticore(부분) → Neural Fly Policy
Swim: Swamp_Alligator/Deep_Clam → Neural Swim Policy
LargeMonster: Monstrous_Deep_Clam → LargeMonster Quadruped Policy
```

#### M3-2: MonsterSkillSystem 연동 확인
```csharp
// 기존 Phase 61 완료: MonsterSkillSystem.cs (Fireball/Charge/Leap/Teleport/AoE/Poison/Heal/Summon/Debuff)
// AnimalAI.cs에서 몬스터 타입별 스킬 매핑 확인됨
// 스폰 시 MonsterSkillSystem 컴포넌트 추가 및 스킬 리스트 주입만 하면 됨
```

**검증 기준 (M3 완료 시)**:
- [ ] Ring 1 영지에 토끼/멧돼지/늑대 5마리 이상 스폰
- [ ] 몬스터 이동/추격/공격/사망 애니메이션 작동
- [ ] 몬스터 스킬 발동 (Fireball/Charge/Leap 등)
- [ ] 사망 시 LootBasket 드랍 + BloodSplat 데칼

---

### Phase M4: 병사 3종 GLB + 영지 건물/깃발 (3-4h)

#### M4-1: GuardManager → 병사 GLB 스폰 수정
```csharp
// 수정: Assets/Scripts/Systems/GuardManager.cs / GuardPlaceholder.cs
// 레벨별 GLB 선택:
//   1-20: Soldier_Lv1-20_Rigged.glb
//   21-40: Soldier_Lv20-40_Rigged.glb  
//   41-50: Soldier_Lv40-50_Rigged.glb
// RingDifficultyData.GetGuardLevelRange() → 레벨 결정
// 무기/방어구 파츠 장착 (wood→steel→crystal→stone 레벨별)
// ModelAnimatorAssigner → Biped Procedural + Neural Combat/React
// 병사 선택 테두리(Phase 60.4): 파란색 반투명 링 펄싱
```

#### M4-2: 영지 건물/깃발 배치 (FixMainScene 확장)
```csharp
// 국가별 성 모델:
동: blue_castle.glb, 서: green_castle.glb, 남: red_castle.glb
북: purple_castle.glb, 황제국: kingdom.glb
// 영지당: 성문1 + 상점1(shop.glb) + 크래프트1(craft_blend.glb) + 교회1 + NPC주택3(hut.glb 변형)
// 깃발: east/west/south/north/kingdom_flag + player_flag_1~4
// NationalFlagController(Phase 3.4) 연동: 소유권 변경 시 교체 연출
```

**검증 기준 (M4 완료 시)**:
- [ ] 영지 진입 시 건물/깃발/병사 표시
- [ ] 병사 레벨별 GLB/장비 차이 확인
- [ ] 깃발 교체 연출 (페이드 인/아웃)

---

### Phase M5: UI 데이터 바인딩 완성 (3-4h)

| UI | 연동 이벤트 | 수정 파일 |
|-----|-------------|-----------|
| **HUD 하트** | `PlayerHealth.Instance.OnHPChanged += (cur, max) => ...` | `HUD.cs` |
| **HUD 버프** | `BuffManager.Instance.OnBuffAdded/Removed` | `HUD.cs` |
| **미니맵 마커** | `MonsterSpawner._spawnedMonsters`, `GuardManager._territoryGuards` | `MinimapUI.cs` |
| **퀘스트 마커** | `QuestManager.ActiveQuest.targetTerritoryId` | `QuestMarkerHUD.cs` |
| **퀵슬롯** | `QuickSlotManager.Slots` | `QuickSlotUI.cs` |
| **인벤토리** | `PlayerInventory.Instance.OnItemChanged` | `InventoryWindow.cs` |

**검증 기준 (M5 완료 시)**:
- [ ] HP 변경 시 하트 실시간 갱신
- [ ] 버프 획득/소멸 시 아이콘 나타남/사라짐
- [ ] 미니맵에 플레이어/몬스터/병사/영지 마커 표시
- [ ] I/R/Q/M/1-6/L/U 키 모두 정상 토글

---

### Phase M6: 환경 시스템 활성화 (2-3h)

```csharp
// FixMainScene.cs에 다음 객체 생성 및 초기화 추가:
// 1. DayNightCycle (TimeManager) - 태양 회전 + OnTimeOfDayChanged 이벤트
// 2. WeatherManager - 날씨 상태머신 + EnvironmentParticleController 트리거
// 3. RegionBGMController - TerritoryManager 국가 감지 + BGM 페이드 전환
// 4. EnvironmentParticleController - 비/눈/반딧불이/먼지
// 5. GraphicConfigSetup 적용: SMAA + Contact Shadows + Bloom (Phase 57)
// 6. DecalSpawner + DecalSpawnerIntegration (Phase 58)
// 7. SpecialEffectsController (Phase 60)
```

**검증 기준 (M6 완료 시)**:
- [ ] 2일 주기 DayNight → 태양 이동/그림자 변화 + NPC 행동 변화
- [ ] 비/눈/안개 → 파티클 + 이동속도/시야/은신 효과
- [ ] 국가 이동 시 BGM 페이드 전환 (1.5초)

---

### Phase M7: 전쟁/이벤트/퀘스트 시스템 연동 (3-4h)

```csharp
// FixMainScene.cs에 다음 시스템 시작 추가:
// 1. TerritoryWarManager (Phase 3.6) - AI 전쟁 주기 시작 (30초~2분)
// 2. WorldEventManager (Phase 36) - 다이내믹 이벤트 타이머 시작
// 3. QuestManager (Phase 39) - 튜토리얼 퀘스트 체인 로드
// 4. AutoMoveManager (Phase 40) - 자동이동 시스템
// 5. FastTravelSystem (Phase 44) - 빠른이동
// 6. MountSystem (Phase 43) - 말 시스템 (Horse_Rigged.glb 없으면 비활성화 플래그)
```

---

### Phase M8: 실내/외 + NPC 일상 + 은신/자물쇠 (2-3h)

```csharp
// 1. LoadingManager + FadeManager → Additive 씬 로딩 검증
// 2. InteriorBuilder 6종 → 구역 배치 확인
// 3. NPCDailyCycle → 시간대별 SetActive + 말풍선 UI
// 4. StealthSystem (Phase 34) → Ctrl 토글 + 발각 게이지
// 5. LockpickSystem (Phase 35) → 미니게임 UI
```

---

### Phase M9: 크래프트/제작 시스템 UI 연동 (2-3h)

```csharp
// 1. RecipeWindow → CraftPresetManager 연동 (Phase 63)
// 2. CraftSuccessSystem → Alchemy/Cooking 스탯 보정 (Phase 3.8)
// 3. 실패 결과 UI: 재료보존/소멸/전소 시각화
```

---

### Phase M10: 전체 통합 테스트 및 버그 픽스 (6-8h) ← **최중요**

#### M10-1: 자동화된 PlayMode 검증 스크립트
```csharp
// 신규: Assets/Editor/PlayModeVerification.cs
// 메뉴: Tools/Poison/Verify PlayMode
// 순차 실행 + Assert:
1. 씬 로드 → Player GLB + Idle 확인
2. 30초 이동 테스트 (WASD + 마우스) → Walk/Run/Jump/Roll
3. 30초 전투 테스트 (좌클릭) → Attack + 카메라 임펄스 + 히트 리액션
4. 몬스터/병사 스폰 확인 → 애니메이션 + AI 작동
5. 영지 진입 → 건물/깃발/병사 확인
6. 시간 2사이클 → DayNight/Weather/BGM/파티클
7. UI 전체 토글 → 인벤토리/제작/지도/퀘스트/설정
8. 사망/부활 → 리스폰 위치 + HP 10%
9. 메모리/프레임 프로파일링 (30분)
```

#### M10-2: 알려진 버그 선제 수정 (M1에서 이미 처리되나 재확인)
- 중복 컴포넌트: `CleanupDuplicateComponents()` 필수 호출
- NeuralAnimationController 중복: `ModelAnimatorAssigner`에서 `GetComponent` 체크 후 방지
- PlayerInput actions: FixMainScene에서 강제 할당
- Cinemachine: Main Camera(Brain) 1개 + Player Camera(VCam) 1개 강제 구조

---

### Phase M11: Windows 빌드 + 최종 검증 (1-2h)

```bash
# 1. BuildAndRun → 실행 파일 생성
# 2. MainMenu → MainScene 전환 테스트
# 3. Player.log 에러/경고 0개 확인
# 4. 30분 플레이 테스트 (M10-1 시나리오 전체)
# 5. 프로파일러: GC Alloc/Frame < 100KB, Memory Stable
```

---

## 🔑 핵심 수정 파일 목록 (최소 세트)

| 파일 | 작업 | 우선순위 |
|------|------|----------|
| `Assets/Editor/FixMainScene.cs` | **완전 재작성** (M1-2) | 🔴 |
| `Assets/Scripts/Systems/Animation/ModelAnimatorAssigner.cs` | **신규 생성** (M1-1) | 🔴 |
| `Assets/Scripts/Systems/TerrainModelPlacer.cs` | **신규 생성** (M2-1) | 🔴 |
| `Assets/Scripts/Systems/MonsterSpawner.cs` | 영지 난이도 연동 수정 (M3-1) | 🔴 |
| `Assets/Scripts/Systems/GuardManager.cs` | 병사 GLB 레벨별 스폰 (M4-1) | 🟠 |
| `Assets/Scripts/UI/HUD.cs` | OnHPChanged/OnBuffChanged 구독 (M5) | 🟠 |
| `Assets/Scripts/UI/MinimapUI.cs` | 몬스터/병사/영지 마커 (M5) | 🟠 |
| `Assets/Editor/PlayModeVerification.cs` | **신규 생성** (M10-1) | 🔴 |
| `Assets/Editor/NeuralModelAutoSetup.cs` | 실행만 (이미 완성) | 🔴 |

---

## 🚀 서브에이전트 위임 순서 (배치 병렬)

```yaml
# Batch 1 (M1 시작) - 2개 병렬
delegate_task:
  tasks:
    - goal: "ModelAnimatorAssigner.cs 신규 생성 - GLB 타입 감지 → Biped/Quadruped 애니메이션 컨트롤러 자동 부착 + NeuralModelDatabase 연동"
      context: |
        파일: Assets/Scripts/Systems/Animation/ModelAnimatorAssigner.cs
        NeuralModelDatabase: Assets/Scripts/Systems/Animation/Neural/NeuralModelDatabase.cs
        MLRuntimeManager: Assets/Scripts/Systems/Animation/Neural/MLRuntimeManager.cs
        ProgressiveRolloutManager: Assets/Scripts/Systems/Animation/Neural/ProgressiveRolloutManager.cs
        Player_Rigged.glb: Assets/Resources/Models/UserProvided/Player_Rigged.glb
      toolsets: ["terminal", "file", "skills"]
    
    - goal: "FixMainScene.cs 완전 재작성 - Player_Rigged.glb 로드 + CleanupDuplicateComponents + Heightmap + 환경시스템 + 스포너 연동"
      context: |
        파일: Assets/Editor/FixMainScene.cs
        TerrainGenerator: Assets/Scripts/Systems/TerrainGenerator.cs
        Player_Rigged.glb: Assets/Resources/Models/UserProvided/Player_Rigged.glb
        PlayerControls: Assets/Resources/Input/PlayerControls.inputactions
        NeuralModelAutoSetup: Tools/Neural/Auto-Setup Model Database
        알려진 버그: Rigidbody×2, Animator×2, PlayerInput actions=null, Cinemachine 구조
      toolsets: ["terminal", "file", "skills"]

# Batch 2 (M1 완료 후) - 2개 병렬
  - TerrainModelPlacer.cs 신규 생성
  - NeuralModelAutoSetup 실행 및 검증 (batchmode)

# Batch 3 (M2-M3) - 2개 병렬
  - MonsterSpawner.cs 영지 난이도 연동 수정
  - GuardManager.cs 병사 GLB 레벨별 스폰 수정

# Batch 4 (M5-M6) - 2개 병렬
  - HUD/MinimapUI 이벤트 구독 완성
  - 환경 시스템 활성화 (DayNight/Weather/BGM/파티클/데칼/이펙트)

# Batch 5 (M7-M10) - 순차
  - 전쟁/이벤트/퀘스트 연동
  - 실내/NPC/은신/자물쇠
  - 크래프트 UI 연동
  - PlayModeVerification.cs 생성 + 전체 통합 테스트
```

---

## ✅ 완료 기준 (Definition of Done) - 최종

| # | 검증 항목 | 성공 기준 |
|---|-----------|-----------|
| 1 | **컴파일** | Unity batchmode + `./run_tests.sh editmode` → 에러 0, 경고 0 |
| 2 | **PlayMode 진입** | 콘솔 에러 0개, Player_Rigged.glb 표시, Idle 애니메이션 |
| 3 | **이동** | WASD 30초 → Walk/Run/Jump/Roll 전이 자연스러움, 발 IK 지형 추종 |
| 4 | **카메라** | 마우스 회전 부드러움, 장애물 회피 작동, 줌 인/아웃 |
| 5 | **전투** | 좌클릭 공격 → 애니메이션 + 트레일 + 카메라 임펄스 + 적 히트 리액션 |
| 6 | **몬스터** | Ring 1 영지 5마리+ 스폰 + 이동/추격/공격/사망 + 드랍 + 스킬 |
| 7 | **병사/영지** | 영지 진입 시 건물/깃발/병사 표시, 레벨별 GLB/장비 차이 |
| 8 | **HUD** | 하트(HP 실시간), 버프 아이콘, 미니맵(플레이어/몬스터/영지 마커) |
| 9 | **환경** | 2일 주기 DayNight + 날씨 변경 + BGM 전환 + 파티클 |
| 10 | **UI** | I/R/Q/M/1-6/L/U 키 모두 정상 토글, 데이터 실시간 반영 |
| 11 | **사망/부활** | HP 0 → 사망 연출 → 가까운 영지 리스폰 (HP 10%) |
| 12 | **메모리** | 30분 플레이 후 메모리 누수 없음 (Profiler 확인) |
| 13 | **빌드** | Windows Build 성공, 실행 파일 정상 작동 |

---

## ⚠️ 리스크 완화 (확정 액션)

| 리스크 | 사전 대응 (이미 계획에 반영) |
|--------|------------------------------|
| Horse_Rigged.glb 없음 | MountSystem 비활성화 플래그 추가 (`MountSystem.enabled = false`) |
| 몬스터 비인간형 애니메이션 | Spider/Clam → `SpecialCreatureAnimator` 신규 모듈 (Phase 3.9 확장) |
| Neural 추론 성능 | `ProceduralLODManager` 거리별: 0-20m Full, 20-50m Half(5fps), 50m+ Culled |
| 중복 컴포넌트 | `FixMainScene.CleanupDuplicateComponents()` 필수 호출 |
| 씬 로딩 시간 | `LoadingManager` 진행률 UI + 비동기 로드 + 팁 표시 |

---

*작성일: 2026-08-23 v3*  
*기반: 전체 코드베이스 직접 검증 (15개 핵심 파일 읽음), 에셋 실존 확인, 시스템 연동 구조 파악 완료*  
*다음 액션: **"진행"** 시 Batch 1 (ModelAnimatorAssigner + FixMainScene 재작성) 위임 시작*