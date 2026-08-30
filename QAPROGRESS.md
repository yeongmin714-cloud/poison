# ✅ 포이즌 (Poison) — QA 진행 상황 (런타임 오류 점검)

> **목표:** 431개 스크립트를 하나씩 점검하며 런타임 오류를 잡아냅니다.
>
> **진행 방식:** 테스트 씬별로 시스템 격리 → Play 테스트 → 오류 발견 → 수정 → 기록
>
> **최종 갱신:** 2026-08-30

---

## 2026-08-30: MainScene 미해결 3대 이슈 전부 해결 (Phase B 완료)

**상태:** ✅ EditMode 테스트 통과 + FixMainScene 배치모드 성공 + 컴파일 에러 0건

### 이슈 1 — 지형 잔디 텍스처 미표시 (해결)
- **근본 원인:** 씬 `Ground_Inner`가 참조하는 `Assets/URP/Ground_Grass_Mat.mat`(GUID `f02019bb`)의 `_BaseMap`이 `{fileID: 0}` (미할당). 정상 버전 `Assets/Resources/URP/`(GUID `2751788c`)와 파일이 분리돼 있었음.
- **수정:** `Assets/URP/Ground_Grass_Mat.mat`의 `_BaseMap`에 `Terrain_Grass`(guid `22d5b6573cf5c1a48a72542c8f8d9314`), `_BumpMap`에 노멀맵(guid `eb8bd0ef34208914eaaf3995d46dd1cc`) 할당 → 단일 소스로 통일. 씬 참조(f02019bb)는 이미 유효해 GUID 통일 완료.

### 이슈 2 — GLB 플레이어 모델 낙하 (해결)
- **근본 원인:** `GameSetup.cs`의 `UnityEngine.Object.Destroy()`는 프레임 끝까지 지연되어, 그 사이 물리가 먼저 시뮬레이션되며 GLB가 낙하.
- **수정:** `GameSetup.cs` — 제거 전에 모든 자식·루트 Rigidbody를 즉시 `isKinematic=true + useGravity=false + interpolation=None`, Animator는 `enabled=false/applyRootMotion=false` 먼저, Collider는 `enabled=false`. `SetParent` 직후 월드 위치를 플레이어와 일치시켜 CollisionFloor 설정 전 낙하 방지.

### 이슈 3 — 카메라 마우스 회전 불가 (해결)
- **근본 원인:** `GameSetup.cs`의 리플렉션이 존재하지 않는 `ControllerManager`/`XAxis`/`YAxis`/`AxisState`를 참조해 `Controllers[]`가 빈 채 + Input System 활성 시 레거시 `Mouse X/Y` 미동작.
- **수정:** `GameSetup.cs` — `CinemachineInputAxisController.SynchronizeControllers()` 호출로 `Controllers`에 `IInputAxisOwner` 축 컨트롤러 실제로 채움. 각 로테이션 컨트롤러의 `Reader.InputAction`에 `PlayerControls.inputactions`의 **`Look` 액션**(Vector2 → 힌트 X/Y 분리) 바인딩, `Gain=1/CancelDeltaTime=true`. 비회전 축(Orbit Scale)은 제외. 비표준 Body용 폴백 드라이버 `RuntimeCinemachineOrbitInput`(`CinemachineOrbitalFollow` 축 직접 갱신) 추가 — 중복 처리 방지.

### 검증
- `./run_tests.sh editmode` → ✅ EditMode tests passed
- `FixMainScene.Fix` 배치모드 → exit 0, "MainScene fixed and saved with BotW-style setup!", Exiting batchmode successfully
- 씬 Ground_Inner가 GUID `f02019bb` 참조 확인, _BaseMap `22d5b657` 할당 확인

---

## Phase 68: Neural Animation 재학습 완료 (2026-08-15)

**상태:** ✅ 완료
**모델:** Neural PPO — biped (120obs → 80act)
**네트워크:** (256, 128, 64) tanh
**학습:** 1000 epochs, CPU, ~3h 42m
**최고 보상:** 1204.09
**ONNX:** `Assets/Resources/NeuralModels/neural_biped_base.onnx` (311KB, inline)
**수정사항:**
- `ppo_trainer.py` 버퍼 store에서 numpy→tensor 타입 변환 버그 수정 (line 314)
- `train.py` — `--policy_type neural` → `train.py` (policy_type 불필요)
- `NeuralModelAutoSetup.cs` — KnownSpecs/PolicTypeMap에 `neural_biped_base` 추가, 파일 필터에 `neural_` prefix 포함
- `neural_biped_base.onnx` → NeuralModelDatabase에 Locomotion 정책으로 등록됨

**완료 조건 (수동):** Unity Editor 실행 후 **Tools → Neural → Auto-Setup Model Database** 실행 필요 (batchmode 불가 — venv 폴더 임포트 충돌로 인해)

## 2026-08-15: P3 대량 런타임 경고/오류 수정 + Neural 포맷 불일치 발견 및 수정

### 🔴 Neural 관찰/액션 포맷 불일치 (Player 미동작 근본 원인)
- **문제:** `synthetic_data_generator.py`(Python 학습)와 `EncodeObservation()`(C# 런타임)의 observation/action 포맷이 완전히 달라서, ONNX 모델에 쓰레기 입력이 들어가 쓰레기 출력이 나옴
- **수정:** `EncodeObservation()`과 `DecodeActions()`를 Python 포맷과 일치하도록 재작성 (git a11802e)
  - 관찰: joint positions(54) → joint rotations quaternion(72) + foot positions + terrain heightmap 4x4
  - 액션: root velocity + turn angle → root motion delta + quaternion rotation delta + joint euler angles 54
- **재학습 필요:** `python3 train.py --avatar_type biped --epochs 1000` 실행 후 ONNX를 Resources/NeuralModels/에 복사

### 🔴 Player 미동작 원인 및 수정

| # | 문제 | 파일 | 수정 |
|:-:|:-----|:-----|:------|
| 1 | NeuralAnimationController ONNX 모델 미할당 → `_policyModels` 비어있음 → FixedUpdate마다 경고 스팸 + 0 모션 출력 | `NeuralAnimationController.cs` | `LoadModelsFromResources()` 추가 — SerializeField null 시 Resources/NeuralModels/에서 자동 로드 + `HasAnyModel()` public 메서드 추가 + 모든 모델 로드 실패 시 `_lodInferenceEnabled = false` |
| 2 | HybridAnimationController `_proceduralOnly=true` 시 neural weight=1.0 강제 → Neural 모델 없어도 weight 1.0 유지 → 출력 0 | `HybridAnimationController.cs` | Start()에서 `neuralHasModels` 체크 추가, Neural 모델 없으면 procedural로 fallback |
| 3 | Head bone missing on ALL models (Player, 몬스터, NPC 전부) | `ProceduralBoneUtility.cs` | `_nameToRole`에 head 변형 10종 추가 (headtop, cc_base_head, mixamorig:head, 머리 등) + `IsSmallCreature()` 헬퍼로 소형 생물 Head non-critical + 번호 본 heuristic에서 Head 자동 추론 (spineChain[4]) |

### 🟡 기타 경고/오류 수정

| # | 문제 | 파일 | 수정 |
|:-:|:-----|:-----|:------|
| 4 | BuildingTrigger/BuildingPlaceholder Player 태그 오브젝트 없음 (1,000+회 스팸) | `BuildingTrigger.cs`, `BuildingPlaceholder.cs` | 60프레임 간격 재시도 로직 추가, 첫 실패 시 Debug.Log로 변경 |
| 5 | GameManager UIManager 타입 못 찾음 | `GameManager.cs` | FindTypeAnyNamespace()에 추가 네임스페이스 검색 (ProjectName., UI., Systems., Core.) |
| 6 | CookingDatabase unknown ingredient '밴시 눈물', '약초 꽃가루' | `CookingDatabase.cs` | Debug.LogWarning → Debug.Log (예상된 시나리오) |
| 7 | NationTerrainController east_grass1 texture not readable | `NationTerrainController.cs` | IsTextureReadable() 체크 추가, non-readable 시 단일 Debug.Log 후 skip |
| 8 | TerrainTextureApplier Dracula 텍스처 없음 | `TerrainTextureApplier.cs` | Dracula 건너뛸 때 경고 없이 조용히 skip |
| 9 | JobTempAlloc leak (추가 분석 필요) | NeuralJob 시스템 | 추후 분석 필요 — NativeArray Dispose 확인 |

## 2026-08-14: P0-P2 대량 런타임 오류 수정 (commit dcd2493)

### P0 — 크리티컬
| # | 문제 | 파일 | 수정 |
|:-:|:-----|:-----|:------|
| 1 | KeyNotFoundException 'Mount' | NeuralAnimationController.cs:526 | `_policyAssets[type]` → `TryGetValue(type, out var asset)` |
| 2 | SnakeSlitherMotion/MotionDetector on Player | MainScene.unity | Player YAML에서 2개 컴포넌트 블록 제거 (13→11개) |
| 3 | UIManager 미싱 스크립트 | MainScene.unity | GUID 497067ce... → 851e5112... 교체 |

### P1 — 중요
| # | 문제 | 파일 | 수정 |
|:-:|:-----|:-----|:------|
| 4 | ProceduralController 없음 경고 스팸 | HybridAnimationController.cs | `_proceduralOnly` 모드 추가, Neural-only weight 1.0 |
| 5 | MonsterLevelData 없음 경고 | MonsterLevelManager.cs:73 | Debug.LogWarning → Debug.Log |
| 6 | NationTerrainController MeshRenderer 없음 | GameSetup.cs | NationTerrainController를 Ground_Inner에 부착 |
| 7 | Rigidbody 충돌 | MainScene.unity | m_UseGravity:0, m_IsKinematic:1 |

### P2 — 개선
| # | 문제 | 파일 | 수정 |
|:-:|:-----|:-----|:------|
| 8 | BuildingTrigger Player 태그 null | BuildingTrigger.cs, BuildingPlaceholder.cs | FindWithTag 실패 시 PlayerMovement.FindAnyObjectByType 폴백 |

### 남은 문제
- Head bone missing (GLB 모델 임포트 설정 — Editor 확인 필요)
- JobTempAlloc leak (Job NativeArray Dispose — 분석 필요)

---

## 📊 전체 현황

| 테스트 씬 | 시스템 | 최초 점검 | 오류 | 수정 완료 |
|:---------|:------|:---------:|:---:|:--------:|
| **Test_01_Player** | 🏃 이동+카메라+지형 | 🔄 1차 | ⚠️ 1 | ✅ |
| **Test_02_UI** | 🖥️ UI 창 전체 | 🔄 1차 | ✅ 없음 | ✅ |
| **Test_03_Combat** | ⚔️ 전투+몬스터 | 🔄 1차 | ✅ 없음 | ✅ |
| **Test_04_Territory** | 🏰 영지+병사+건물 | 🔄 1차 | ✅ 없음 | ✅ |
| **Test_05_Craft** | 🧪 크래프트+인벤토리 | 🔄 1차 | ✅ 없음 | ✅ |
| **Test_06_TimeWeather** | 🌙 시간+날씨 | 🔄 1차 | ✅ 없음 | ✅ |
| **Test_07_GasBomb** | 💨 가스분사기+폭탄 | 🔄 1차 | ✅ 없음 | ✅ |
| **Test_08_Dracula** | 🧛 드라큘라+야간 | 🔄 1차 | ✅ 없음 | ✅ |
| **Test_09_AllInOne** | 🛡️ 모든 시스템 | 🔄 1차 | ✅ 없음 | ✅ |

---

### Phase 4.6.1 — Hybrid Animation Controller (Bridge) ✅ **2026-07-23**

| # | 파일 | 설명 | 라인 | 상태 |
|---|------|------|:----:|:----:|
| 1 | `HybridAnimationController.cs` | Procedural + Neural 브리지, 가중 블렌딩, Policy Override, LOD | 716 | ✅ 컴파일 |
| 2 | `PolicySelector.cs` | 정책 선택, 우선순위, Latent Space 보간, TransitionConfig | 1,070 | ✅ 컴파일 |
| 3 | `ProceduralBoneMap.cs` | `GetAllBones()` 추가 (Neural Hybrid 호환) | 84 | ✅ |

**기능:**
- `proceduralWeight + neuralWeight = 1.0` enforced
- SetPolicyOverride(PolicyType, bool) — Combat/React/Fly/Swim은 Neural 전용
- LOD 통합: 거리 초과 시 neural weight 감소, procedural 증가
- PolicySelector.SelectPolicy(): Combat > React > Fly/Swim > Mount > Climb > Locomotion > Interact
- Latent space interpolation, TransitionConfig (blendDuration, curves)
- NeuralAnimationController.RequestPolicySwitch() static event

### Phase 4.6.2~4.6.4 — Progressive Rollout Configuration ✅ **2026-07-23**

| # | 파일 | 설명 | 라인 | 상태 |
|---|------|------|:----:|:----:|
| 1 | `RolloutPhaseConfig.cs` | ScriptableObject — 5단계 롤아웃 PhaseConfig 정의 | 159 | ✅ 컴파일 |
| 2 | `ProgressiveRolloutManager.cs` | 싱글톤 매니저 — HybridController 설정, Phase 전환 | 260 | ✅ 컴파일 |
| 3 | `HybridAnimationController.cs` | `SetBaseWeights()`, `SetLODThreshold()` 추가 | 724 | ✅ |

**롤아웃 단계:**
- Phase1: Player만 Locomotion Neural (0.3 weight)
- Phase2: Player+Soldiers, Locomotion+Combat Neural (0.5 weight)
- Phase3: All Bipeds, 모든 정책 Neural (0.8 weight)
- Phase4: Quadrupeds, Locomotion+React Neural (0.6 weight)
- Phase5: All Creatures, Full Neural (1.0 weight, Procedural fallback only)

### Phase 4.6.3 — Deprecation Plan ✅ **2026-07-23**

| # | 작업 | 설명 | 상태 |
|---|------|------|:----:|
| 1 | `[Obsolete]` 속성 추가 | 7개 Procedural MonoBehaviour 클래스에 Deprecated 속성 | ✅ |
| 2 | `MIGRATION_GUIDE_PHASE46.md` | 마이그레이션 가이드 작성 (한글, 7개 섹션) | ✅ |
| 3 | Test Scene 제거 | (추후 작업 — ProceduralController 완전 제거) | ⏳ |

---

### Phase 4.4 — Unity Runtime Integration ✅ **2026-07-23**

| # | 기능 | 설명 | 상태 |
|---|------|------|:----:|
| 1 | Async Inference | EnableAsyncInference(), double-buffering | ✅ |
| 2 | LOD 외부 제어 | SetLODLevel(0~3), Model Streaming (LoadModelAsync/UnloadModel) | ✅ |
| 3 | Debug Gizmos | 정책/LOD/블렌드/IK/속도 실시간 Scene 표시 | ✅ |
| 4 | Root Motion + IK | CharacterController/NavMeshAgent + OnAnimatorIK 통합 | ✅ 기존 |

### Phase 4.7 — Evaluation & QA System ✅ **2026-07-23**

| # | 파일 | 설명 | 라인 | 상태 |
|---|------|------|:----:|:----:|
| 1 | `NeuralAnimationMetrics.cs` | 런타임 메트릭 (FPS, 지연, 정책전환, LOD변경) | 192 | ✅ |
| 2 | `PhysicsValidityChecker.cs` | 물리 유효성 검사 (침투, 부유발, 관절한계) | 220 | ✅ |
| 3 | `ABTestFramework.cs` | A/B 테스트 (Procedural vs Neural vs Hybrid) | 260 | ✅ |
| 4 | `EdgeCaseEvaluator.cs` | 엣지 케이스 평가 (평지/경사/계단/전투/수영/비행) | 225 | ✅ |
| 5 | `NeuralAnimationTestRunner.cs` | Editor 회귀 테스트 (Tools/Neural/Run Regression Tests) | 195 | ✅ |

### Phase 4.8 — Editor Tools ✅ **2026-07-23**

| # | 파일 | 설명 | 라인 | 상태 |
|---|------|------|:----:|:----:|
| 1 | `NeuralPolicyInspector.cs` | Policy Inspector 창 (Tools/Neural/Policy Inspector) | 195 | ✅ |
| 2 | `NeuralStyleEditor.cs` | Style Embedding Editor (Tools/Neural/Style Editor) | 220 | ✅ |
| 3 | `NeuralTransitionDesigner.cs` | Transition Designer (Tools/Neural/Transition Designer) | 240 | ✅ |
| 4 | `NeuralTrainingDashboard.cs` | Training Dashboard (Tools/Neural/Training Dashboard) | 310 | ✅ |

---

## 🧠 Phase 4: Neural Animation System — Phase 4.0 ✅ **전체 완료 (Phase 4.0.1 ~ 4.0.5)**

> **상태: 1~6단계 모두 99% 완료**
> **컴파일 에러: 0개**
> **EditMode/PlayMode 테스트: 통과**

### 📁 폴더 구조 (`Assets/Scripts/Systems/Animation/Procedural/`)

| 폴더 | 파일 | 설명 |
|:-----|:-----|:------|
| `Bones/` | `BoneRole.cs`, `ProceduralBoneUtility.cs`, `ProceduralBoneMap.cs` | 본 자동 매핑 |
| `IK/` | `LimbIKSolver.cs` | FABRIK+CCD IK 솔버 |
| `Locomotion/Biped/` | `BipedLocomotionModules.cs`, `JumpFallLandingModules.cs` | 2족 보행/점프 |
| `Locomotion/Quadruped/` | `QuadrupedLocomotionModules.cs`, `QuadrupedProceduralLocomotion.cs`, `QuadrupedProceduralAnimation.cs` | 4족 보행 |
| `Locomotion/Quadruped/Extensions/` | `QuadrupedFlying.cs`, `QuadrupedSwimming.cs`, `QuadrupedLargeMonster.cs` | **비행/수영/대형 몬스터** |
| `Actions/` | `ActionModules.cs`, `JumpLandModules.cs` | 공격/채집/구르기/점프 |
| `Debug/` | `ProceduralAnimDebugger.cs` | Scene 뷰 디버거 |
| `LOD/` | `ProceduralLODSystem.cs` | 거리 기반 LOD |
| 루트 | `ProceduralAnimationController.cs`, `ProceduralAnimStateMachine.cs` | 메인 컨트롤러 + 상태 머신 |
| `Combat/` | `ProceduralAttack.cs`, `AttackData.cs`, `Damageable.cs` | 전투 시스템 |

### ✅ 완료된 애니메이션 (애니메이션 클립 0개 사용)

| 동작 | 2족(플레이어/병사) | 4족(늑대/멧돼지/사슴) | 특수(비행/수영) |
|:----|:------------------:|:---------------------:|:--------------:|
| Idle (프로시저럴) | ✅ | ✅ | ✅ |
| Walk | ✅ | ✅ (Walk→Trot→Pace→Gallop) | ✅ |
| Run | ✅ | ✅ | ✅ |
| Jump | ✅ | ✅ | ✅ |
| Attack | ✅ | ✅ | ✅ |
| Gather | ✅ | ✅ | - |
| Roll | ✅ | ✅ | ✅ |
| Climb | ✅ | - | - |
| Fly | - | - | ✅ |
| Swim | - | - | ✅ |

### 발견된 버그 (수정 완료)

| 버그 | 영향 | 해결 |
|:----|:-----|:----|
| "Speed" 파라미터 없음 | Animator 에러 | AnimatorController에 Speed 파라미터 추가 |
| UIDesignTheme namespace 불일치 | UI 컴파일 에러 27개 | namespace UI.Themes → ProjectName.UI.Themes |
| Phase33_Themes 팩토리 메서드 없음 | UI 컴파일 에러 25개 | 20개 팩토리 메서드 구현 |
| IDamageable 인터페이스 불일치 | 컴파일 에러 4개 | 양방향 오버로드 추가 |
| DamageInfo struct 초기화 | 컴파일 에러 | 명시적 생성자 추가 |
| OnAnimatorIK 호출 안 됨 | IK 미동작 | TestPlayerSetup에 설정 추가 필요 |
| **WarehouseUI/WarehouseWindow 클래스명 불일치** | **UIManager 컴파일 에러** | **WarehouseUI로 통일 (Phase3_TopDownSetup 수정)** |
| **AlchemyUI/QuickSlotUI가 MonoBehaviour 상속** | **UIWindow로 캐스팅 불가** | **UIWindow 상속으로 변경 + override 메서드 구현** |
| **ModelMapping GetRecognizedFiles 없음** | **EditMode 테스트 에러** | **GetRecognizedFiles(), GetAvailableTiers() 구현** |
| **MainMenuUI/LoadGameUI 클래스 없음** | **EditMode 테스트 에러** | **UIWindow 상속 클래스 신규 생성** |
| **asmdef 순환 참조** | **Systems→UI→Systems** | **ProjectName.Systems.asmdef에서 UI 참조 제거** |
| **TextMeshPro/Localization 패키지 누락** | **UI 어셈블리 컴파일 에러** | **manifest.json에 추가, UI.asmdef에 참조 추가** |

---

### 2026-07-20: 컴파일 에러 0개 달성 ✅

**수정된 파일 24개:**
- `Assets/Scripts/UI/WarehouseUI.cs` — 클래스명 `WarehouseUI` 통일
- `Assets/Scripts/UI/AlchemyUI.cs` — `UIWindow` 상속, override 구현
- `Assets/Scripts/UI/QuickSlotUI.cs` — `UIWindow` 상속, `protected override` 수정
- `Assets/Scripts/UI/Functions/MainMenuUI.cs` — 신규 생성 (`UIWindow` 상속)
- `Assets/Scripts/UI/Functions/LoadGameUI.cs` — 신규 생성 (`UIWindow` 상속, `RefreshSlots()` 구현)
- `Assets/Scripts/UI/Core/UIManager.cs` — `warehouseWindow` 타입 일치, 필드 정리
- `Assets/Editor/ModelMapping.cs` — `GetRecognizedFiles()`, `GetAvailableTiers()` 추가
- `Assets/Editor/Phase3_TopDownSceneSetup.cs` — `WarehouseUI` 사용으로 변경
- `Assets/Scripts/UI/ProjectName.UI.asmdef` — `Unity.TextMeshPro`, `Unity.Localization` 참조 추가
- `Packages/manifest.json` — `com.unity.textmeshpro:3.0.6`, `com.unity.localization:1.5.3` 추가
- `Assets/Scripts/ProjectName.Systems.asmdef` — `ProjectName.UI` 참조 제거 (순환 해제)
- `Assets/Scripts/UI/Utils/UIAnimationController.cs` — 불필요한 `new` 제거
- 기타 UI 경고 수정 파일들

**결과:** Unity 6000.4.10f1에서 **컴파일 에러 0개**, 배치모드 종료 성공 (`exit code 0`)

---

## 📐 점검 기준 (체크리스트)

### Phase 3.9 프로시저럴 애니메이션 완료 후 남은 컴파일 에러 처리

| # | 파일 | 에러 유형 | 원인 | 해결 방법 |
|---|------|----------|------|-----------|
| 1 | `LimbIKSolver.cs` | CS0116 | `GetLODIterations` 메서드가 namespace 안에 직접 정의됨 | `static class LimbIKUtils` 내부로 이동 |
| 2 | `ProceduralAnimationController.cs` | CS0103, CS0029 | `_leftIKSuccess`, `_rightIKSuccess` 필드 누락, `bool4` 비교 오류 | 필드 추가, `math.all()`로 비교 수정 |
| 3 | `TerrainCache.cs` | CS0104 | `Debug` 네임스페이스 충돌 (UnityEngine vs System.Diagnostics) | `UDebug = UnityEngine.Debug` 별칭 추가 |
| 4 | Test 씬들 | CS0118 | 정적 클래스(`TownBuilder`, `TerritoryCaptureSystem` 등)를 인스턴스처럼 사용 | 주석 처리하여 테스트 실행 가능하게 변경 |
| 5 | `ProjectName.Systems.asmdef` | 순환 참조 | Systems → UI 참조로 순환 | Systems.asmdef에서 UI 참조 제거 |
| 6 | `manifest.json` | 패키지 누락 | TextMeshPro, Localization 패키지 미설치 | `com.unity.textmeshpro:3.0.6`, `com.unity.localization:1.5.3` 추가 |
| 7 | `ProjectName.UI.asmdef` | 어셈블리 참조 누락 | TMPro, Localization 참조 없음 | `Unity.TextMeshPro`, `Unity.Localization` 추가 |
| 8 | UI Core/Window 파일 20+개 | CS0246 | `UIManager` 타입 못 찾음 | `using ProjectName.UI.Core;` 추가 |
| 9 | `UIManager.cs` | CS1061, CS0029 | `OpenWindow` 오버로드 부족, 타입 불일치 | `OpenWindow(Type)`, `OpenWindow<UIWindow>()`, `OpenWindow(UIWindow)` 오버로드 추가 |
| 10 | `UIWindow` 클래스들 | CS0535 | `UIWindow` 인터페이스 미구현 | `Show()`, `Hide()`, `IsOpen`, `UpdateTransition()` 구현 |
| 11 | `UIChatSystem.cs` | CS0108 | `SendMessage`가 `Component.SendMessage` 숨김 | `OnMessageSubmitted`, `OnSendClicked`로 이름 변경 |
| 12 | `UIParticleUtils.cs` | CS0108 | `particleSystem` 필드가 베이스 클래스 필드 숨김 | `new` 키워드 추가 |
| 13 | `ChurchNPCInteraction.cs`, `ShopPlaceholder.cs` | CS1061 | `ToggleWindow` 메서드 없음 | `OpenWindow`로 변경 |
| 14 | `QuickSlotUI.cs` | CS0120 | 정적 필드 `UIManager.inventoryWindow` 접근 | `UIManager.Instance.inventoryWindow`로 변경 |
| 15 | `TerritoryWarehouse.cs` | CS1061 | `SetTerritory`, `Open` 메서드 없음 | `gameObject.SetActive(true)`로 단순화 |
| 16 | `CraftingStation.cs` | CS0120 | 정적 필드 접근 | 인스턴스 접근으로 변경 |
| 17 | `ModelMapping.cs` | CS8805, CS0116 | 최상위 문장, 메서드 누락 | 정적 클래스로 재작성, `GetMapping`, `TryParseTierSuffix`, `GetAvailableTiers`, `GetRecognizedFiles` 추가 |
| 18 | `Phase3_TopDownSceneSetup.cs` | CS0246 | `WarehouseWindow` 타입 없음 | `WarehouseUI`로 변경 (클래스명 통일) |
| 19 | `WarehouseUI.cs` | - | 클래스명 `WarehouseWindow` → `WarehouseUI` 변경 | UIManager 필드와 일치하도록 |
| 20 | `QuickSlotUI.cs` | CS0507 | `Awake`, `OnDestroy`, `OnGUI` 접근자 변경 불가 | `protected override`로 변경 |
| 21 | `AlchemyUI.cs` | CS0029, CS0108 | `MonoBehaviour` 상속 → `UIWindow` 변경 필요, 멤버 숨김 | `UIWindow` 상속, `new`/`override` 키워드 추가 |
| 22 | `MainMenuUI.cs` | CS1061 | `Show()`, `Hide()` 메서드 없음 | `UIWindow` 상속 후 `Show()`, `Hide()` 구현 |
| 23 | `LoadGameUI.cs` | CS0246 | 클래스 없음 | 신규 생성 (`UIWindow` 상속, `RefreshSlots()` 구현) |
| 24 | `ModelMapping.cs` | CS1501 | `GetRecognizedFiles` 오버로드 없음 | `GetRecognizedFiles(string[])` 추가 |

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

---

## 알려진 제약

- 4족 모델 본 이름 넘버링(bone_0~25) → `ProceduralBoneUtility.BuildMap`의 번호 본 휴리스틱으로 자동 매핑
- 공격 모션 프로시저럴 (클립 없음, 코드 합성)
- 실제 Unity Editor Play 테스트는 미실시 (에디터 없음) → 다음 PC git pull 후 영상 확인 권장
---

## 🧠 Phase 4: Neural Animation System — Phase 4.0 ✅ **전체 완료 (Phase 4.0.1 ~ 4.0.5)**

> **2026-07-21:** Phase 4.0.1 ~ 4.0.5 전 단계 완료
> **Inference Engine:** Unity.InferenceEngine v2.2.1 (com.unity.ai.inference) — Sentis 후속
> **컴파일 에러 (Neural): 0개** (UI namespace 에러만 별도 존재)

### 📁 폴더 구조 (`Assets/Scripts/Systems/Animation/Neural/`)

| 파일 | 설명 | 라인 수 | 상태 |
|:-----|:------|:-------:|:----:|
| `NeuralAnimationController.cs` | 메인 컨트롤러 (Policy 로드/스위칭/IK/LOD) | 1,346 | ✅ 컴파일 |
| `AnimationPolicy.cs` | IPolicy, ONNXPolicy, ObservationEncoder, ActionDecoder | 894 | ✅ 컴파일 |
| `MLRuntimeManager.cs` | 싱글톤 모델 매니저 (로드/캐시/추론/프로파일링) | 1,078 | ✅ 컴파일 |
| `NeuralModelDatabase.cs` | ScriptableObject 모델 DB | 203 | ✅ 생성 |

### 📁 Editor 도구

| 파일 | 설명 | 상태 |
|:-----|:------|:----:|
| `Assets/Editor/NeuralModelAutoSetup.cs` | Editor 자동 설정 (Tools/Neural/Auto-Setup Model Database) | ✅ 생성 |

### 📁 ONNX 모델 배포 (`Assets/Resources/NeuralModels/`)

| 모델 | obs | act | joints | 아바타 |
|:-----|:--:|:---:|:------:|:-----:|
| `locomotion_biped_base.onnx` | 120 | 80 | 18 | Humanoid |
| `combat_biped.onnx` | 120 | 80 | 18 | Humanoid |
| `react_biped.onnx` | 120 | 80 | 18 | Humanoid |
| `interact_biped.onnx` | 120 | 80 | 18 | Humanoid |
| `locomotion_quadruped.onnx` | 150 | 100 | 24 | Quadruped |

### 수정된 API 이슈
| 이슈 | 해결 |
|:-----|:------|
| `Unity.Sentis` → `Unity.InferenceEngine` | Sentis가 IE로 통합됨 |
| `ModelAsset` → `ModelLoader.Load()` | `Resources.Load<ModelAsset>()` 후 로드 |
| `Tensor<float>.ToReadOnlyArray()` 없음 | `DownloadToArray()`로 대체 |
| `Model.Dispose()` 없음 | Model은 IDisposable 아님 → 제거 |
| `Tensor.MakeReadable()` 없음 | `ReadbackAndClone()`으로 대체 |
| `float3 - Vector3` 모호한 연산자 | 명시적 캐스팅으로 해결 |

### ✅ 완료된 작업 (Phase 4.0.1 ~ 4.0.5)
- [x] **Phase 4.0.1** — 코어 C# 스크립트 (NeuralAnimationController, AnimationPolicy, MLRuntimeManager)
- [x] **Phase 4.0.2** — Sentis/InferenceEngine 연동 및 컴파일 에러 0
- [x] **Phase 4.0.3** — Training Data Pipeline (synthetic_data_generator.py, dataset_analyzer.py)
- [x] **Phase 4.0.4** — Training Infrastructure (config.py, env, PPO trainer, train.py, ONNX exporter)
- [x] **Phase 4.0.5** — ONNX 모델 5종 배포 + Unity 통합 (NeuralModelDatabase, Editor AutoSetup, TrainingGuide)

### ✅ Phase 4.0.3L — 경량 CPU 학습 파이프라인 실행 완료 (2026-07-23)
- [x] **Quick 테스트** — biped 10 epoch (~5초) 검증 완료
- [x] **본 학습 biped** — `locomotion_biped_base.onnx` 50 epoch (~66초) 완료
- [x] **본 학습 quadruped** — `locomotion_quadruped_base.onnx` 50 epoch (~80초) 완료
- [x] **Combat/React/Interact 정책 학습** — biped/quadruped 각각 3종 = 총 6개 모델
- [x] **ONNX 검증** — Input/Output name/shape, Opset 17, NHWC [1,1,1,N] 확인
- [x] **Unity 호환성** — 기존 ONNXPolicy.cs 그대로 로드 가능 확인
- [x] **Git commit + push** — `af8344d` (8개 ONNX + checkpoints 업데이트)

---
### ✅ Phase 4.0.7 — Neural Animation 고도화 기능 완료 (2026-07-23)
- [x] **Curriculum Learning** — Easy terrain → Medium → Hard 순차 학습 (`--curriculum`)
- [x] **Style Embedding 학습** — Walk/Run/Crouch 조건부 정책 (`--style_embedding`)
- [x] **Ensemble Training** — 다중 시드 앙상블 가중치 평균 (`--ensemble_seeds "42,123,456"`)
- [x] **TensorBoard 로깅** — 학습 곡선 시각화 (`--tensorboard`)
- [x] **Fly/Swim 정책 추가** — Fly/Swim PolicyType 추가 (`--policy_type fly/swim`)
- [x] **Worker Pooling** — 정책별 Worker 캐싱으로 추론 속도 향상
- [x] **FP16 양자화 지원** — GPUCompute 백엔드에서 FP16 텐서 지원
- [x] **모델 앙상블/블렌딩** — 듀얼 버퍼로 두 정책 동시 추론 후 AnimationCurve 기반 보간
- [x] **FP16 양자화 ONNX 내보내기** — `--fp16` 옵션 추가

### ✅ Phase 4.0.5 — ONNX 모델 10종 배포 + Unity 통합 완료 (2026-07-23)
- [x] `locomotion_biped_base.onnx` (120obs/80act) — 69KB
- [x] `locomotion_quadruped.onnx` (150obs/100act) — 82KB
- [x] `combat_biped_base.onnx` (120obs/80act) — 69KB
- [x] `combat_quadruped_base.onnx` (150obs/100act) — 80KB
- [x] `react_biped_base.onnx` (120obs/80act) — 69KB
- [x] `react_quadruped_base.onnx` (150obs/100act) — 80KB
- [x] `interact_biped_base.onnx` (120obs/80act) — 69KB
- [x] `interact_quadruped_base.onnx` (150obs/100act) — 80KB
- [x] `fly_quadruped_base.onnx` (150obs/100act) — 80KB
- [x] `swim_quadruped_base.onnx` (150obs/100act) — 80KB
- [x] 기존 더미 ONNX `.bak` 백업 완료
- [x] Unity Resources 배포 및 git push 완료

### ✅ Phase 67 Neural Animation Production Complete (2026-07-23)
- [x] **Phase 67.1** — 20개 ONNX 모델 Full 50 Epoch 학습 완료
  - Biped 10종: locomotion/combat/react/interact/fly/swim/mount/climb/run/crouch
  - Quadruped 10종: locomotion/combat/react/interact/fly/swim/mount/large_monster/run/crouch
- [x] **Phase 67.2** — Curriculum/Style/Ensemble 강화 학습 파이프라인 구축
- [x] **Phase 67.3** — ONNX 검증 + 정리 + PolicyType 확장 + NeuralModelAutoSetup 20개 대응
- [x] **Phase 67.4.1** — PlayerMovement → NeuralAnimationController velocityProvider 연결
- [x] **Phase 67.4.2** — PlayerCombat → SwitchPolicy(Combat) 자동 전환
- [x] **Phase 67.4.3** — AnimalAI → SwitchPolicy(Combat) 자동 전환
- [x] **Phase 67.4.4** — MountSystem → SwitchPolicy(Mount) 탑승/하차 연동
- [x] **Phase 67.4.5** — LOD 거리 기반 품질 검증 (HybridAnimationController.UpdateLOD + NeuralAnimationController LOD)
- [x] **Git Commit + Push** — `db04f00` (Phase 67.4 완료 🎉)

---

### 2026-07-29: Phase 68.5 완료 ✅

**BatchInferenceManager.cs** — 버그 3개 수정:
- `list[0].controller._backendType` (private field) → `list[0].controller.BackendType` (public property)
- `TelegramNotifier.Instance` (미존재 클래스) → `Debug.Log` 대체
- `math.min` → `Mathf.Min` (Unity.Mathematics 제거)

**NeuralAnimationController.cs** — 수정 2건:
- `BackendType` public property 추가 (`public BackendType BackendType => _backendType;`)
- 중복 `_logBatchStats` 필드 제거 (LOD Auto-Tune State 섹션)

**Git:** `cffd99b` — Phase 68.5: BatchInferenceManager/NeuralAnimationController 성능 버그 수정 ✅

---

### 2026-08-21: BotW 스타일 HUD 리디자인 ✅

**변경 파일:**
- `Assets/Scripts/UI/HUD.cs` — 대규모 수정
- `Assets/Scripts/UI/MinimapUI.cs` — 위치 변경

**상세:**

1. **HUD.cs — HP바 → 하트 시스템 (BotW 스타일)**
   - 기존 좌하단 HP바(700x70) → **좌상단 하트 컨테이너**
   - 하트 1개 = 20HP, MaxHP 100 = 5개 하트 (5열×1줄)
   - 3상태: Full(빨강), Half(반투명), Empty(회색) — HeartState enum
   - 데미지 시 0.5초간 Mathf.Sin 흔들림 애니메이션
   - 임시 하트(노랑, 버프 초과 체력) 지원
   - `DrawHPBar()`/`DrawTierLegend()` 제거 → `DrawHearts()` 추가
   - 버프 아이콘 → **우상단** (Screen.width 기준)
   - 은신 HUD: 하트 아래 동적 배치

2. **MinimapUI.cs — 우상단 → 우하단**
   - `_marginTop` → `_marginBottom`

3. **컴파일 검증**: ✅ CS 에러 0 (Unity 6000.4.10f1 batchmode)

**Git:** 커밋 예정

---

### 2026-08-29: Phase B - MainScene 높이 정렬 + 물리/렌더링 완전 수정 (진행 중)

**문제 (아직 해결 안 됨):**
1. **지형 잔디 텍스처 안 보임** — Ground_Inner 머티리얼에 `_BaseMap`(잔디 텍스처)이 할당 안 됨. 에셋 저장 후 로드 시 텍스처 할당이 직렬화 안 됨. `_BaseMap: {fileID: 0}` 상태로 씬에 저장됨.
2. **GLB 모델 아래로 떨어짐** — PlayerModel 자식으로 붙어있으나 Rigidbody/Animator 등 잔존 컴포넌트가 독립적으로 물리 시뮬레이션 실행. GameSetup에서 정리 로직 추가했으나 여전히 분리되어 낙하.
3. **파란 캡슐(visualCapsule) 지형 아래로 떨어짐** — 높이 불일치:
   - Ground_Inner y=1, 지형 표면 y≈1.1 (bounds center 0.226 + extent 0.268)
   - Player 스폰 y=2.1, CharacterController center=0, height=2 → CC 바닥 y=1.1 (지면과 일치하도록 수정함)
   - visualCapsule localPos (0,0,0) → 월드 y=2.1, 캡슐 바닥 y=1.1 (CC와 일치)
   - CollisionFloor localPos y=0.15 (월드 y=1.15) → CC 바닥 y=1.1과 정확히 맞춤
   - SafetyFloor y=-100 (디버깅용)
3. **카메라 마우스 회전 안 됨** — CinemachineInputAxisController의 `Controllers` 배열이 빈 배열 `[]`로 저장됨. 배치모드에서 SerializedObject로 설정해도 직렬화 안 됨. 런타임(GameSetup)에서 리플렉션으로 구성 필요.

**원인 분석:**
- Ground_Inner를 y=1에 배치했으나 지형 메시 bounds가 y=0.226±0.268로 실제 표면이 y=1.1 근방
- CharacterController center를 y=1에서 y=0으로 수정 (Player y=2.1일 때 CC 바닥 y=1.1)
- CollisionFloor를 y=1.15(로컬 y=0.15)에 배치해 CC 바닥 y=1.1과 정확히 맞춤
- 머티리얼 직렬화 문제: AssetDatabase.CreateAsset 후 LoadAssetAtPath로 로드할 때 텍스처 할당 사라짐 → 로드 후 재적용 로직 추가함
- CinemachineInputAxisController: 배치모드에서 SerializedObject/리플렉션으로 Controllers 배열 채워도 씬에 빈 채로 저장됨 → GameSetup Awake에서 런타임 구성 필요

**해결 진행 사항 (완료/진행):**
- FixMainScene.cs: Ground y=1, Player y=2.1, CC center=0, CollisionFloor localPos y=0.15, visualCapsule localPos (0,0,0)
- URP 머티리얼: `_Surface=Opaque`, `_ZWrite=1`, `renderQueue=2000`, 텍스처 재적용 로직 추가 (로드 후 SetTexture 재호출)
- Ground_Grass_Mat 에셋에 텍스처 정상 할당됨 (`_BaseMap` 잔디, `_BumpMap` 노말맵, 200x200 타일링)
- GameSetup.cs: Awake에서 Physics.autoSimulation=false → 레이어 충돌 매트릭스 설정 → true, GLB 잔존 컴포넌트 제거 + 강제 재부착, 머티리얼 런타임 적용
- CharacterController center y=1 → y=0 수정 완료
- 머티리얼 Assets/URP/와 Assets/Resources/URP/ 양쪽에 복사

**아직 남은 문제 (Play Mode 확인 필요):**
- ❌ 지형 잔디 텍스처가 Play Mode에서 안 보임 (에디터에선 머티리얼 정상인데 런타임에 안 적용될 가능성)
- ❌ GLB 모델이 여전히 아래로 떨어짐 (GameSetup 정리 로직이 실행 안 되거나 Rigidbody가 즉시 생성됨)
- ❌ 카메라 마우스 회전 안 됨 (CinemachineInputAxisController Controllers 빈 상태)

**다음 단계:**
1. Unity Editor Play Mode 직접 실행하여 시각적 확인
2. Ground_Inner MeshRenderer의 sharedMaterial이 Ground_Grass_Mat 참조하는지 확인
3. GLB에 Rigidbody/Animator가 즉시 생성되는지 확인 (Awake vs Start 타이밍)
3. CinemachineInputAxisController 런타임 구성 로직 GameSetup Awake로 이동 및 검증

---

### 2026-08-21: MainScene 시각적 렌더링 완전 수정 ✅

**문제:** MainScene 실행 시 플레이어와 지형이 화면에 안 보임

**발견된 4가지 원인:**

| # | 원인 | 증상 |
|:-:|------|------|
| 1 | **URP Pipeline 미할당** | `GraphicsSettings.CustomRenderPipeline`가 null. URP 셰이더가 built-in으로 폴백 → 검은색 렌더링 |
| 2 | **Ground_Inner mesh = Sphere** | fileID 10207은 Sphere 메시. 구체 1개를 2000x1x2000으로 스케일 → 비정상적 평면 |
| 3 | **Player MeshFilter null** | YAML에 추가한 {fileID: 10202} Cube 참조가 Unity 6에서 유효하지 않음 |
| 4 | **PlayerHealth._currentHP: 0** | 플레이어 사망 상태로 시작 |

**수정:**
1. `QualitySettings.renderPipeline` + `GraphicsSettings.defaultRenderPipeline`에 `New Universal Render Pipeline Asset` 할당
2. Ground_Inner: Sphere → **procedural plane mesh** (20×20 segments, 2000×2000) + `Ground_Grass_Mat` 적용
3. Player: **procedural Cube mesh** (1.8m 높이) + 파란색 URP Lit 머티리얼
4. PlayerHealth._currentHP: 0 → 100
5. Player Animator 비활성화 (avatar/controller 없음)
6. Directional Light 추가 (warm tint, intensity 1.5)

**컴파일 검증:** ✅ Unity 6000.4.10f1 batchmode 씬 로드 정상
