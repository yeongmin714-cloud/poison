# 📖 Phase 4.6 — Procedural → Neural Animation 마이그레이션 가이드

> **목적:** Phase 3.9 (Procedural Animation) → Phase 4.6 (Neural Animation) 마이그레이션 안내.
> **마이그레이션 상태:** HybridController 브리지 구축 완료, 점진적 롤아웃 설정 완료.

---

## 1. 개요

Phase 4.6은 **점진적 마이그레이션** 방식입니다. 기존 Procedural 코드를 한 번에 제거하지 않고, **HybridAnimationController**를 통해 Neural과 Procedural을 동시에 실행하며 단계별로 전환합니다.

### 마이그레이션 Phase

| Phase | 대상 | Neural 정책 | Neural Weight |
|:-----:|:-----|:------------|:-------------:|
| 4.6.1 | Player만 | Locomotion | 0.3 |
| 4.6.2 | Player + Soldiers | Locomotion + Combat | 0.5 |
| 4.6.3 | All Bipeds | 모든 정책 | 0.8 |
| 4.6.4 | Quadrupeds | Locomotion + React | 0.6 |
| 4.6.5 | All Creatures | Full Neural | 1.0 |

---

## 2. 교체 방법

### 2.1 — ProceduralAnimationController → HybridAnimationController

**기존 (Procedural):**
```csharp
// 캐릭터에 ProceduralAnimationController 추가
var ctrl = gameObject.AddComponent<ProceduralAnimationController>();
ctrl.SetVelocityProvider(velocityProvider);
```

**변경 (Hybrid):**
```csharp
// 캐릭터에 HybridAnimationController + NeuralAnimationController 추가
var hybrid = gameObject.AddComponent<HybridAnimationController>();
var neural = gameObject.AddComponent<NeuralAnimationController>();

// HybridController가 자동으로 두 컨트롤러를 찾아 연결
hybrid.SetVelocityProvider(velocityProvider);
hybrid.SetBoneMap(boneMap);

// ProgressiveRolloutManager로 Phase 설정
ProgressiveRolloutManager.Instance.ConfigureHybridController(hybrid, RolloutPhase.Phase2_PlayerSoldiers);
```

### 2.2 — ProgressiveRolloutManager 설정

씬에 ProgressiveRolloutManager를 추가합니다:

```csharp
// 아무 GameObject에 ProgressiveRolloutManager 추가
var manager = new GameObject("ProgressiveRolloutManager").AddComponent<ProgressiveRolloutManager>();
manager.SetPhase(RolloutPhase.Phase2_PlayerSoldiers);
```

### 2.3 — Policy Override 직접 설정

수동으로 정책 오버라이드를 설정하려면:

```csharp
hybrid.SetBaseWeights(0.5f, 0.5f);  // procedural 50%, neural 50%
hybrid.SetPolicyOverride(NeuralAnimationController.PolicyType.Locomotion, true);  // Locomotion은 Neural
hybrid.SetPolicyOverride(NeuralAnimationController.PolicyType.Combat, true);      // Combat은 Neural
hybrid.ClearAllPolicyOverrides();  // 모든 오버라이드 초기화
```

---

## 3. 주요 API

| API | 설명 |
|-----|------|
| `HybridAnimationController.SetBaseWeights(procedural, neural)` | 기본 블렌드 가중치 설정 (자동 정규화) |
| `HybridAnimationController.SetPolicyOverride(policy, useNeural)` | 특정 정책을 Neural/Procedural로 강제 |
| `HybridAnimationController.ClearPolicyOverride(policy)` | 특정 정책 오버라이드 해제 |
| `HybridAnimationController.ClearAllPolicyOverrides()` | 모든 오버라이드 초기화 |
| `HybridAnimationController.SetLODThreshold(threshold)` | LOD 거리 임계값 설정 |
| `HybridAnimationController.GetEffectiveControlMode(policy)` | 현재 정책의 제어 모드 조회 |
| `ProgressiveRolloutManager.SetPhase(phase)` | 롤아웃 Phase 설정 + 모든 컨트롤러 재설정 |
| `ProgressiveRolloutManager.AdvancePhase()` | 다음 Phase로 자동 전환 |
| `ProgressiveRolloutManager.ConfigureHybridController(ctrl, phase)` | 특정 컨트롤러를 Phase에 맞게 설정 |
| `NeuralAnimationController.SwitchPolicy(policy)` | 활성 정책 변경 (부드러운 블렌딩) |
| `NeuralAnimationController.RequestPolicySwitch(policy, duration)` | 정적 이벤트를 통한 정책 전환 요청 |
| `PolicySelector.SelectPolicy(state, avatarType, combatCtx)` | 상황에 맞는 정책 자동 선택 |

---

## 4. Phase별 설정 예시

### Phase 1: Player만 Locomotion Neural
```csharp
hybrid.SetBaseWeights(0.7f, 0.3f);
hybrid.SetPolicyOverride(NeuralAnimationController.PolicyType.Locomotion, true);
// Combat, React, Interact 등은 자동으로 Procedural
```

### Phase 2: Player + Soldiers, Locomotion + Combat Neural
```csharp
hybrid.SetBaseWeights(0.5f, 0.5f);
hybrid.SetPolicyOverride(NeuralAnimationController.PolicyType.Locomotion, true);
hybrid.SetPolicyOverride(NeuralAnimationController.PolicyType.Combat, true);
```

### Phase 3: All Bipeds, 모든 정책 Neural
```csharp
hybrid.SetBaseWeights(0.2f, 0.8f);
hybrid.SetPolicyOverride(NeuralAnimationController.PolicyType.Locomotion, true);
hybrid.SetPolicyOverride(NeuralAnimationController.PolicyType.Combat, true);
hybrid.SetPolicyOverride(NeuralAnimationController.PolicyType.React, true);
hybrid.SetPolicyOverride(NeuralAnimationController.PolicyType.Interact, true);
```

---

## 5. 문제 해결

### 5.1 — 컴파일 에러: 'IVelocityProvider' 관련
`IVelocityProvider`는 `ProceduralAnimationController` 클래스 내부가 아닌 **namespace 레벨**에 정의되어 있습니다.
```csharp
using ProjectName.Systems.Animation.Procedural;  // 필수
// IVelocityProvider 직접 사용 (ProceduralAnimationController.IVelocityProvider 아님)
```

### 5.2 — 정책 전환이 안 됨
- ONNX 모델이 `Resources/NeuralModels/`에 배포되어 있는지 확인
- `NeuralAnimationController`에 `_policyAssets`가 올바르게 할당되었는지 확인
- `ProgressiveRolloutManager`의 `ConfigureHybridController()`가 호출되었는지 확인

### 5.3 — Neural 추론이 너무 느림
- `_lodNeuralWeightThreshold`를 낮춰서 원거리 Neural 비중 감소
- `BackendType.CPU` 대신 `BackendType.GPUCompute` 사용 (GPU 환경에서)
- Worker Pooling 활성화 확인

### 5.4 — Procedural 컨트롤러가 Deprecated 경고 표시
경고는 정상입니다. `[Obsolete]` 속성은 Phase 4.6.3에서 의도적으로 추가되었습니다.
경고를 숨기려면:
```csharp
#pragma warning disable CS0618  // Obsolete 경고 억제
var ctrl = gameObject.AddComponent<ProceduralAnimationController>();
#pragma warning restore CS0618
```

---

## 6. 롤백 절차

Neural 시스템에 문제가 발생하면 Phase 4.6 이전 상태로 되돌릴 수 있습니다:

### 방법 1 — ProgressiveRolloutManager Phase 변경
```csharp
ProgressiveRolloutManager.Instance.SetPhase(RolloutPhase.Phase1_PlayerLocomotion);
// 또는
ProgressiveRolloutManager.Instance.SetPhase((RolloutPhase)(-1));  // 모든 Procedural
```

### 방법 2 — HybridController 비활성화
```csharp
hybrid.SetBaseWeights(1f, 0f);  // 100% Procedural
hybrid.ClearAllPolicyOverrides();  // 모든 오버라이드 해제
```

### 방법 3 — Git 롤백
```bash
git revert HEAD --no-edit  # 마지막 커밋 되돌리기
# 또는 특정 커밋으로 롤백
git reset --hard <Phase3.9-commit-hash>
```

---

## 7. 관련 파일

| 파일 | 설명 |
|------|------|
| `Assets/Scripts/Systems/Animation/Neural/HybridAnimationController.cs` | 메인 브리지 컨트롤러 |
| `Assets/Scripts/Systems/Animation/Neural/NeuralAnimationController.cs` | Neural 추론 컨트롤러 |
| `Assets/Scripts/Systems/Animation/Neural/PolicySelector.cs` | 정책 선택/우선순위 |
| `Assets/Scripts/Systems/Animation/Neural/ProgressiveRolloutManager.cs` | 롤아웃 Phase 관리 |
| `Assets/Scripts/Systems/Animation/Neural/RolloutPhaseConfig.cs` | Phase 설정 데이터 |
| `Assets/Scripts/Systems/Animation/Neural/AnimationPolicy.cs` | 정책 메타데이터 |
| `Assets/Scripts/Systems/Animation/Procedural/ProceduralAnimationController.cs` | (Deprecated) 기존 컨트롤러 |
| `Assets/Resources/NeuralModels/` | ONNX 정책 모델 (20종) |
| `ROADMAP_NEURAL_ANIMATION.md` | 전체 Neural Animation 로드맵 |
| `QAPROGRESS.md` | QA 진행 상황 |