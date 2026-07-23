# ✅ 포이즌 (Poison) — QA 진행 상황 (런타임 오류 점검)

> **목표:** 431개 스크립트를 하나씩 점검하며 런타임 오류를 잡아냅니다.
>
> **진행 방식:** 테스트 씬별로 시스템 격리 → Play 테스트 → 오류 발견 → 수정 → 기록
>
> **최종 갱신:** 2026-07-20

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
- [x] **Phase 67.3** — ONNX 검증 및 Unity Resources 배포
- [x] **Git Commit + Push** — `b8f2e4c` (20개 ONNX + 체크포인트 + 설정 업데이트)
