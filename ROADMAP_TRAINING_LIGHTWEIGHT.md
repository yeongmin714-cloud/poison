# 🏋️ Phase 4.0.3L — 경량 CPU 학습 파이프라인 (Lightweight Training)

> **목표:** GPU 없이 현재 WSL(CPU) 환경에서 실제 동작하는 ONNX 신경망 정책을 학습하고, 기존 더미 ONNX 파일을 교체한다.
>
> **핵심:** PyTorch/ONNX Runtime 없이 **순수 numpy + scipy + protobuf**만으로 PPO 학습 + ONNX 내보내기

---

## ⚙️ 시스템 구성

```
┌──────────────────────────────────────────────────────────────────┐
│  train_lightweight.py (CLI 진입점)                                │
│  ┌─────────────────┐   ┌────────────────────┐   ┌──────────────┐ │
│  │  numpy_ppo.py    │   │  onnx_writer.py    │   │Config/Env    │ │
│  │  - ActorCritic    │──▶│  - protobuf ONNX   │   │(기존 파일)   │ │
│  │  - RolloutBuffer  │   │  - Gemm+Tanh MLP   │   │              │ │
│  │  - PPO Trainer    │   │  - Validation      │   │              │ │
│  │  - Adam Optimizer │   │  - Unity Sentis 호환│   │              │ │
│  └─────────────────┘   └────────────────────┘   └──────────────┘ │
└──────────────────────────────────────────────────────────────────┘
```

### 파일 목록

| 파일 | 경로 | 설명 |
|------|------|------|
| `numpy_ppo.py` | `Assets/Training/TrainingInfra/` | 순수 numpy PPO 학습 (Actor-Critic, GAE, Adam) |
| `onnx_writer.py` | `Assets/Training/TrainingInfra/` | protobuf 기반 ONNX 모델 생성기 |
| `train_lightweight.py` | `Assets/Training/TrainingInfra/` | CLI 진입점 (학습 → ONNX 내보내기) |
| 체크포인트 | `Assets/Training/TrainingInfra/checkpoints/` | `.npz` 형식 학습 중간 저장 |
| ONNX 출력 | `Assets/Resources/NeuralModels/` | Unity Sentis 로드 대상 |

### 네트워크 구조

```
Input: "observation" shape [1, 1, 1, obs_dim]  (NHWC, Unity Sentis)
  │
  ▼ Reshape
[1, obs_dim]
  │
  ▼ Gemm1 (w0: [64, obs_dim], b0: [64])  ← transB=1
[1, 64]
  │
  ▼ Tanh
[1, 64]
  │
  ▼ Gemm2 (w1: [64, 64], b1: [64])  ← transB=1
[1, 64]
  │
  ▼ Tanh
[1, 64]
  │
  ▼ Gemm3 (w2: [act_dim, 64], b2: [act_dim])  ← transB=1
[1, act_dim]
  │
Output: "action" shape [1, act_dim]
```

### 하이퍼파라미터

| 파라미터 | Quick 모드 | Full 모드 |
|----------|-----------|-----------|
| Epochs | 10 | 50 |
| Steps/epoch | 1,024 | 2,048 |
| Mini-epochs | 5 | 10 |
| Hidden sizes | [64, 64] | [64, 64] |
| Learning rate | 3e-4 | 3e-4 (linear decay) |
| Clip ε | 0.2 | 0.2 |
| γ / λ | 0.99 / 0.95 | 0.99 / 0.95 |
| 소요 시간 (biped) | ~4초 | ~20초 |
| 소요 시간 (quadruped) | ~5초 | ~25초 |

---

## 📋 실행 로드맵

### Phase 1: CPU 학습 실행 검증 ✅ (완료)

| # | 태스크 | 상태 | 비고 |
|---|--------|------|------|
| 1.1 | numpy_ppo 단위 테스트 | ✅ | ActorCritic forward/evaluate/checkpoint |
| 1.2 | onnx_writer 단위 테스트 | ✅ | biped + quadruped ONNX 생성/검증 |
| 1.3 | train_lightweight.py quick 실행 | ✅ | biped 10 epoch + ONNX export ~4초 |
| 1.4 | ONNX 파일 Unity 호환성 검증 | ✅ | input/output name/shape 확인 |

### Phase 2: 본 학습 — 8개 ONNX 모델 생성 ✅ (완료)

| # | 태스크 | ONNX 파일 | Epoch | 소요시간 |
|---|--------|-----------|-------|---------|
| 2.1 | **locomotion_biped_base** 학습 | `locomotion_biped_base.onnx` | 50 | ~66초 |
| 2.2 | **locomotion_quadruped** 학습 | `locomotion_quadruped_base.onnx` | 50 | ~80초 |
| 2.3 | **combat_biped** 학습 | `combat_biped_base.onnx` | 50 | ~68초 |
| 2.4 | **combat_quadruped** 학습 | `combat_quadruped_base.onnx` | 50 | ~75초 |
| 2.5 | **react_biped** 학습 | `react_biped_base.onnx` | 50 | ~66초 |
| 2.6 | **react_quadruped** 학습 | `react_quadruped_base.onnx` | 50 | ~74초 |
| 2.7 | **interact_biped** 학습 | `interact_biped_base.onnx` | 50 | ~75초 |
| 2.8 | **interact_quadruped** 학습 | `interact_quadruped_base.onnx` | 50 | ~78초 |

### Phase 3: 통합 검증 및 배포 ✅ (완료)

| # | 태스크 | 설명 |
|---|--------|------|
| 3.1 | Unity C# 컴파일 확인 | 기존 ONNXPolicy.cs 그대로 사용 가능 확인 |
| 3.2 | 기존 더미 ONNX 백업 | `NeuralModels/` 내 5개 파일 `.bak` 백업 |
| 3.3 | 학습 ONNX 배치 | 생성된 8개 ONNX를 `Resources/NeuralModels/`에 복사 |
| 3.4 | git commit + push | 변경사항 저장 (`af8344d`) |

### Phase 4: (선택) 고도화

| # | 태스크 | 설명 |
|---|--------|------|
| 4.1 | Policy별 네트워크 구조 차별화 | Combat은 더 깊은 네트워크, React는 더 얕은 네트워크 |
| 4.2 | Curriculum Learning | Easy terrain → Rough terrain 순차 학습 |
| 4.3 | Style Embedding 학습 | Walk/Run/Crouch 등 스타일 조건부 정책 |
| 4.4 | TensorBoard 로깅 | 학습 곡선 시각화 (numpy 전용) |

---

## 🚀 사용법

```bash
# Quick 테스트 (10 epoch, ~4초)
python train_lightweight.py --avatar_type biped --quick

# 본 학습 (50 epoch, ~20초)
python train_lightweight.py --avatar_type biped --epochs 50

# Quadruped 학습
python train_lightweight.py --avatar_type quadruped --epochs 50

# 모든 ONNX 모델 한 번에 생성
python train_lightweight.py --avatar_type biped --epochs 50
python train_lightweight.py --avatar_type quadruped --epochs 50
```

### 출력 파일

| 파일 | 경로 |
|------|------|
| 학습된 ONNX | `Assets/Resources/NeuralModels/locomotion_{type}_base.onnx` |
| 체크포인트 | `Assets/Training/TrainingInfra/checkpoints/{type}_policy.npz` |

---

## 🔍 Unity Sentis 호환성

생성된 ONNX는 다음 조건을 만족합니다:

| 조건 | 값 |
|------|-----|
| Input name | `"observation"` |
| Output name | `"action"` |
| Input shape | `[1, 1, 1, N]` (NHWC) |
| Output shape | `[1, M]` |
| Opset | 17 |
| Ops | Reshape, Gemm, Tanh |
| Precision | FP32 |
| Weight 저장 형식 | `[out_dim, in_dim]` (transB=1로 transpose) |

기존 `ONNXPolicy.cs`의 `ONNXPolicy` 클래스가 그대로 로드 가능합니다:
```csharp
// ONNXPolicy.cs — 변경 불필요
var policy = new ONNXPolicy(metadata, BackendType.CPU);
policy.Infer(observation, action);
```

---

## 📊 성능 예상 (CPU, WSL Ubuntu)

| 모델 | Obs Dim | Act Dim | 파일 크기 | 학습 시간 (50 epoch) | 추론 시간 (예상) |
|------|---------|---------|-----------|---------------------|-----------------|
| Biped | 120 | 80 | ~68 KB | ~20초 | < 1ms (CPU) |
| Quadruped | 150 | 100 | ~80 KB | ~25초 | < 1ms (CPU) |

> **참고:** 네트워크가 매우 작아 ([64, 64] hidden) 파일 크기가 작고 추론이 빠릅니다.
> 향후 GPU 환경에서 더 큰 네트워크 ([256, 128, 64])로 재학습하여 품질 향상 가능.

---

## ⚠️ 알려진 제한사항

1. **numpy 기반 학습** — autodiff 없이 수동 역전파 → 복잡한 네트워크 확장 어려움
2. **작은 네트워크** — [64, 64] hidden → 품질 한계 (GPU 환경에서 [256, 128, 64]로 재학습 권장)
3. **단순 환경** — SimpleAnimationEnv는 2D kinematic chain → 실제 Unity 물리 환경과 차이
4. **PPO only** — SAC/TD3/Diffusion Policy 미지원
5. **CPU only** — GPU 가속 없음 (의도적, 현재 환경 제약)

---

## 🔗 관련 파일

- `ROADMAP_NEURAL_ANIMATION.md` — 기존 Neural Animation 전체 로드맵
- `Assets/Training/TrainingInfra/config.py` — 기존 설정 (그대로 사용)
- `Assets/Training/TrainingInfra/simple_animation_env.py` — 기존 환경 (그대로 사용)
- `Assets/Scripts/Systems/Animation/Neural/AnimationPolicy.cs` — ONNXPolicy (변경 불필요)