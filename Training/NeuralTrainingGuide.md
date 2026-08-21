# 🧠 Neural Animation — 학습 가이드

> **프로젝트:** 포이즌 (Poison)
> **최종 갱신:** 2026-07-21
> **대상:** ONNX 정책 모델 학습 및 Unity 통합

---

## 개요

Neural Animation 시스템은 PPO(Proximal Policy Optimization) 강화학습을 통해
캐릭터의 자연스러운 움직임을 생성합니다. 학습된 정책은 ONNX 형식으로 내보내져
Unity Sentis(Unity.InferenceEngine)에서 실시간 추론됩니다.

### 정책 모델 종류

| 정책 | 설명 | 관측(obs) | 행동(act) | 관절 | 아바타 |
|:-----|:-----|:---------:|:---------:|:----:|:-----:|
| Locomotion (Biped) | 2족 보행/달리기/방향전환 | 120 | 80 | 18 | Humanoid |
| Combat (Biped) | 2족 전투/회피/공격 | 120 | 80 | 18 | Humanoid |
| React (Biped) | 2족 반응/넘어짐/일어나기 | 120 | 80 | 18 | Humanoid |
| Interact (Biped) | 2족 상호작용/물건집기/밀기 | 120 | 80 | 18 | Humanoid |
| Locomotion (Quadruped) | 4족 보행/달리기 | 150 | 100 | 24 | Quadruped |

---

## 1. 환경 설정

### 1.1 Python 설치

Python 3.10 이상이 필요합니다. GPU 가속을 위해 CUDA 11.8+ 호환 GPU를 권장합니다.

```bash
# Python 버전 확인
python --version  # 3.10.x 이상 권장

# 가상환경 생성 (권장)
python -m venv neural_env
source neural_env/bin/activate  # Linux/Mac
# 또는
neural_env\Scripts\activate     # Windows
```

### 1.2 의존성 설치

```bash
# 프로젝트 루트에서
cd Assets/Training/

# 필수 패키지 설치
pip install torch==2.1.0 torchvision==0.16.0 --index-url https://download.pytorch.org/whl/cu118
pip install numpy==1.24.3
pip install onnx==1.15.0
pip install onnxruntime==1.17.0
pip install gymnasium==0.29.0
pip install tqdm
pip install tensorboard
pip install matplotlib
pip install scipy
```

> **참고:** CUDA 버전에 맞는 PyTorch 설치는 [pytorch.org](https://pytorch.org)에서 확인하세요.
> GPU가 없는 경우 `--index-url https://download.pytorch.org/whl/cpu`로 CPU 전용 설치 가능
> (학습 속도가 10~50배 느려집니다).

### 1.3 디렉토리 구조

```
Assets/Training/
├── DataPipeline/
│   ├── synthetic_data_generator.py  # 합성 데이터 생성기
│   ├── dataset_analyzer.py          # 데이터셋 분석기
│   └── README.md                    # 파이프라인 설명
├── TrainingInfra/
│   ├── config.py                    # 학습 설정
│   ├── simple_animation_env.py      # 강화학습 환경
│   ├── ppo_trainer.py               # PPO 학습기
│   ├── train.py                     # 메인 학습 스크립트
│   ├── onnx_exporter.py             # ONNX 내보내기
│   └── models/                      # 내보내진 ONNX 모델 저장
└── NeuralTrainingGuide.md           # 이 파일
```

---

## 2. 합성 데이터 생성

학습 전에 다양한 움직임 패턴을 포함한 합성 데이터를 생성합니다.

```bash
cd Assets/Training/DataPipeline/

# 합성 데이터 생성 (기본 설정)
python synthetic_data_generator.py \
    --output ../TrainingInfra/data/synthetic \
    --num_episodes 1000 \
    --max_steps 500

# 추가 옵션:
#   --terrain uneven    # 울퉁불퉁 지형 (기본: flat)
#   --speed_range 0.5 3.0  # 속도 범위 (기본: 0.5~5.0)
#   --inclination 0.3   # 경사로 포함 비율
#   --random_seed 42    # 시드 고정
```

### 생성되는 데이터

- `episode_*.npz` — 각 에피소드의 관측/행동/보상 기록
- `metadata.json` — 생성 설정 및 통계
- `dataset_stats.json` — 정규화에 필요한 평균/표준편차

### 데이터 분석

```bash
python dataset_analyzer.py \
    --data ../TrainingInfra/data/synthetic \
    --output ../TrainingInfra/data/analysis
```

---

## 3. PPO 학습 실행

### 3.1 설정 확인

`Assets/Training/TrainingInfra/config.py`에서 주요 하이퍼파라미터를 확인합니다:

```python
# config.py (주요 설정)
class TrainingConfig:
    # 환경
    env_name = "SimpleAnimationEnv"
    max_episode_steps = 500
    
    # PPO
    learning_rate = 3e-4
    gamma = 0.99
    gae_lambda = 0.95
    clip_epsilon = 0.2
    ent_coef = 0.01
    vf_coef = 0.5
    max_grad_norm = 0.5
    
    # 네트워크
    hidden_dim = 256
    num_layers = 3
    
    # 학습
    num_timesteps = 10_000_000  # 총 타임스텝
    num_envs = 8                 # 병렬 환경 수
    batch_size = 2048
    mini_batch_size = 256
    update_epochs = 10
    
    # 저장
    save_interval = 100_000      # 타임스텝마다 체크포인트 저장
    log_interval = 10_000        # 타임스텝마다 로깅
```

### 3.2 학습 실행

```bash
cd Assets/Training/TrainingInfra/

# 기본 학습 (2족 보행)
python train.py \
    --policy locomotion_biped \
    --obs-dim 120 \
    --act-dim 80 \
    --joints 18 \
    --num-timesteps 10000000 \
    --num-envs 8

# 4족 보행 학습
python train.py \
    --policy locomotion_quadruped \
    --obs-dim 150 \
    --act-dim 100 \
    --joints 24 \
    --num-timesteps 15000000 \
    --num-envs 4

# 전투 정책 학습 (사전학습된 locomotion 모델에서 시작)
python train.py \
    --policy combat_biped \
    --obs-dim 120 \
    --act-dim 80 \
    --joints 18 \
    --pretrained checkpoints/locomotion_biped_base/best.pt \
    --num-timesteps 8000000 \
    --num-envs 8
```

### 3.3 학습 모니터링

```bash
# TensorBoard로 모니터링 (별도 터미널)
tensorboard --logdir runs/

# 웹브라우저에서 http://localhost:6006 열기
```

TensorBoard에서 확인할 수 있는 지표:
- **episode_reward**: 에피소드당 평균 보상 (증가해야 함)
- **policy_loss**: 정책 손실 (수렴해야 함)
- **value_loss**: 가치 손실 (수렴해야 함)
- **entropy**: 탐험 엔트로피 (적절히 감소해야 함)
- **explained_variance**: 가치 예측 정확도 (0.9 이상 목표)

### 3.4 예상 학습 시간

| GPU | 2족 보행 (10M timesteps) | 4족 보행 (15M timesteps) |
|:---|:-----------------------:|:------------------------:|
| **RTX 4090** | ~30분 | ~50분 |
| **RTX 3090** | ~45분 | ~1시간 15분 |
| **RTX 3060** | ~1.5시간 | ~2.5시간 |
| **CPU only** | ~10시간 | ~20시간 |

> **참고:** 8개의 병렬 환경 기준입니다. `num_envs`를 늘리면 GPU 활용도가 높아져
> 더 빠른 학습이 가능하지만 VRAM 사용량이 증가합니다.

---

## 4. ONNX 모델 내보내기

학습이 완료된 PyTorch 모델을 ONNX 형식으로 변환합니다.

```bash
cd Assets/Training/TrainingInfra/

# 체크포인트에서 ONNX 내보내기
python onnx_exporter.py \
    --checkpoint checkpoints/locomotion_biped_base/best.pt \
    --output models/locomotion_biped_base.onnx \
    --obs-dim 120 \
    --act-dim 80 \
    --quantize int8  # INT8 양자화 (선택, 파일 크기 75% 감소)
```

### 옵션

| 옵션 | 설명 | 기본값 |
|:-----|:------|:------:|
| `--checkpoint` | PyTorch 체크포인트 경로 (필수) | — |
| `--output` | 출력 ONNX 파일 경로 (필수) | — |
| `--obs-dim` | 관측 차원 | 120 |
| `--act-dim` | 행동 차원 | 80 |
| `--quantize` | 양자화 방식 (fp32/fp16/int8) | fp32 |
| `--opset` | ONNX opset 버전 | 17 |
| `--dynamic-batch` | 동적 배치 크기 허용 | False |

### 양자화 권장사항

| 양자화 | 파일 크기 | 추론 속도 | 정확도 |
|:------|:--------:|:---------:|:-----:|
| FP32 | 100% | 1.0x | 기준 |
| FP16 | ~50% | 1.5~2.0x | 거의 동일 |
| INT8 | ~25% | 2.0~3.0x | 약간 저하 (보통 허용) |

> **권장:** Unity 배포용은 INT8 양자화를 사용하여 파일 크기와 추론 속도를 최적화하세요.
> 정확도 저하가 문제가 된다면 FP16을 사용하세요.

---

## 5. Unity 통합

### 5.1 ONNX 모델 배포

내보낸 ONNX 모델을 Unity 프로젝트에 복사합니다:

```bash
# Windows 예시
copy Assets\Training\TrainingInfra\models\*.onnx Assets\Resources\NeuralModels\

# Linux/Mac
cp Assets/Training/TrainingInfra/models/*.onnx Assets/Resources/NeuralModels/
```

### 5.2 모델 데이터베이스 자동 설정

1. Unity Editor에서 **Tools → Neural → Auto-Setup Model Database** 실행
2. `Assets/Resources/NeuralModels/` 폴더에서 ONNX 파일 자동 검색
3. `NeuralModelDatabase.asset` 생성 또는 업데이트
4. 각 모델의 메타데이터(obs/act/joint 크기) 자동 설정

### 5.3 NeuralAnimationController 설정

1. 캐릭터 GameObject에 `NeuralAnimationController` 컴포넌트 추가
2. `Animator` 및 `ProceduralBoneMap` 컴포넌트도 필요 (자동 추가)
3. 인스펙터에서 각 정책 모델 에셋 할당:

```
[Neural Policy Models]
  ▸ Locomotion Policy: locomotion_biped_base.onnx (ModelAsset)
  ▸ Combat Policy:     combat_biped.onnx (ModelAsset)
  ▸ React Policy:      react_biped.onnx (ModelAsset)
  ▸ Interact Policy:   interact_biped.onnx (ModelAsset)
```

### 5.4 런타임 정책 전환

```csharp
// 스크립트에서 정책 전환
var controller = GetComponent<NeuralAnimationController>();

// 걷기 → 전투
controller.SwitchPolicy(NeuralAnimationController.PolicyType.Combat);

// 전투 → 반응 (넘어짐 등)
controller.SwitchPolicy(NeuralAnimationController.PolicyType.React);

// 상호작용 (물건 집기)
controller.SwitchPolicy(NeuralAnimationController.PolicyType.Interact);

// 다시 걷기
controller.SwitchPolicy(NeuralAnimationController.PolicyType.Locomotion);
```

---

## 6. 문제 해결

### 6.1 학습 관련

**Q: 보상이 전혀 증가하지 않아요.**
- 보상 함수 설계를 확인하세요. 너무 복잡하거나 희소하면 학습이 어렵습니다.
- `--num-timesteps`를 늘려보세요 (최소 5M 이상 권장).
- 학습률을 낮춰보세요 (`--learning-rate 1e-4`).

**Q: 학습이 불안정해요 (보상이 크게 출렁임).**
- `clip_epsilon`을 낮춰보세요 (0.1~0.15).
- `gae_lambda`를 낮춰보세요 (0.9).
- 미니배치 크기를 늘려보세요.

**Q: CUDA out of memory 오류가 발생해요.**
- `--num-envs`를 줄이세요 (4 또는 2).
- `--hidden-dim`을 줄이세요 (128).
- 배치 크기를 줄이세요.

### 6.2 ONNX 내보내기 관련

**Q: ONNX 내보내기가 실패해요.**
- PyTorch와 ONNX 버전 호환성을 확인하세요.
- `--opset 17` 대신 `--opset 14`를 시도해보세요.
- `--quantize fp32`로 먼저 내보내기 성공 후 양자화를 적용하세요.

**Q: ONNX 모델 크기가 너무 커요.**
- `--quantize int8`로 양자화하세요.
- 불필요한 레이어를 제거하세요.
- hidden_dim을 줄여보세요.

### 6.3 Unity 통합 관련

**Q: Unity에서 모델을 로드할 수 없어요.**
- ONNX 파일이 `Assets/Resources/NeuralModels/`에 있는지 확인하세요.
- Unity.InferenceEngine 패키지가 설치되었는지 확인하세요.
- 모델의 obs/act 차원이 코드와 일치하는지 확인하세요.

**Q: 추론이 너무 느려요.**
- INT8 양자화 모델을 사용하세요.
- `_inferenceRateHz`를 낮추세요 (30Hz도 충분한 경우가 많습니다).
- LOD 시스템을 활성화하세요 (먼 거리에서 추론 생략).
- GPU 추론(BackendType.GPUCompute)을 사용하세요.

**Q: 캐릭터가 이상하게 움직여요.**
- 관절 개수(jointCount)가 모델과 일치하는지 확인하세요.
- 관측 정규화 상수가 올바른지 확인하세요.
- 모델이 충분히 학습되었는지 확인하세요 (보상 수렴 확인).

---

## 7. 고급 주제

### 7.1 커스텀 정책 학습

새로운 정책을 추가하려면:

1. `config.py`에 새 정책 설정 추가
2. 필요시 `simple_animation_env.py`에 새로운 환경 로직 추가
3. 학습 실행
4. ONNX 내보내기
5. Unity `NeuralModelAutoSetup.cs`의 `KnownSpecs`에 새 항목 추가
6. Tools/Neural/Auto-Setup Model Database 실행

### 7.2 전이 학습

기존 정책을 기반으로 새로운 정책을 학습할 수 있습니다:

```bash
# locomotion 모델에서 combat 모델로 전이 학습
python train.py \
    --policy combat_biped \
    --pretrained checkpoints/locomotion_biped_base/best.pt \
    --freeze-backbone  # 초반에 backbone 레이어 고정
```

### 7.3 앙상블 추론

여러 정책의 출력을 혼합하여 더 부드러운 동작을 생성:

```csharp
// Locomotion과 Combat 정책을 7:3 비율로 혼합
var locoAction = new float[80];
var combatAction = new float[80];
var blendedAction = new float[80];

locoPolicy.Infer(observation, locoAction);
combatPolicy.Infer(observation, combatAction);

for (int i = 0; i < 80; i++)
    blendedAction[i] = locoAction[i] * 0.7f + combatAction[i] * 0.3f;
```

---

## 8. 참고 자료

- [PPO 논문 (Schulman et al., 2017)](https://arxiv.org/abs/1707.06347)
- [Unity Sentis (InferenceEngine) 문서](https://docs.unity3d.com/Packages/com.unity.ai.inference@latest)
- [ONNX 런타임 문서](https://onnxruntime.ai/docs/)
- [Stable-Baselines3 PPO](https://stable-baselines3.readthedocs.io/en/master/modules/ppo.html)
- [DeepMimic (모방 학습 기반 캐릭터 애니메이션)](https://arxiv.org/abs/1804.02717)