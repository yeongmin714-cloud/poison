# Neural Animation Training Data Pipeline

**Poison Project — Unity Neural Animation System**

파이프라인은 `NeuralAnimationController`와 `AnimationPolicy` C# 코드의 관찰/행동 공간과 일치하는 합성 훈련 데이터를 생성합니다. 실제 Unity 환경을 실행하지 않고도 신경 정책 모델의 프로토타이핑, 오프라인 테스트 및 전이 학습을 가능하게 합니다.

---

## 목차

1. [개요](#개요)
2. [디렉토리 구조](#디렉토리-구조)
3. [설치](#설치)
4. [사용법](#사용법)
5. [데이터 형식](#데이터-형식)
6. [관찰 인코딩](#관찰-인코딩)
7. [행동 디코딩](#행동-디코딩)
8. [아바타 설정](#아바타-설정)
9. [보행 유형](#보행-유형)
10. [분석](#분석)
11. [C# Unity 통합](#c-unity-통합)
12. [라이선스](#라이선스)

---

## 개요

이 파이프라인은 다음을 제공합니다:

- **`synthetic_data_generator.py`** — 관찰/행동 공간과 일치하는 합성 훈련 데이터를 생성합니다.
- **`dataset_analyzer.py`** — 생성된 데이터셋을 분석하고 시각화합니다.
- **`output/`** — 생성된 `.npz` 데이터셋과 분석 플롯을 저장합니다.

### 지원되는 아바타

| 아바타 | 관찰 차원 | 행동 차원 | 관절 수 | 지형 해상도 |
|--------|-----------|-----------|---------|-------------|
| **Biped** (Humanoid) | 120 | 80 | 18 | 11×11 |
| **Quadruped** | 150 | 100 | 24 | 11×11 |

---

## 디렉토리 구조

```
Assets/Training/DataPipeline/
├── synthetic_data_generator.py    # 데이터 생성기 (메인)
├── dataset_analyzer.py            # 데이터셋 분석기 및 시각화
├── requirements.txt               # Python 종속성
├── README.md                      # 이 문서
└── output/
    ├── .gitkeep
    ├── locomotion_biped_dataset.npz    # 생성된 바이페드 데이터셋
    ├── locomotion_quadruped_dataset.npz # 생성된 쿼드러페드 데이터셋
    └── plots/
        ├── locomotion_biped_dataset_*.png
        └── locomotion_quadruped_dataset_*.png
```

---

## 설치

### 요구 사항

- Python 3.8+
- pip (Python 패키지 관리자)

### 종속성 설치

```bash
cd Assets/Training/DataPipeline
pip install -r requirements.txt
```

**종속성 목록:**
- `numpy>=1.21.0` — 수치 연산 및 배열 처리
- `matplotlib>=3.4.0` — 시각화 및 플롯 생성
- `scipy>=1.7.0` — 보간, 회전 및 신호 처리
- `scikit-learn>=1.0.0` — 분석 유틸리티 (선택 사항)
- `tqdm>=4.62.0` — 진행률 표시줄

---

## 사용법

### 기본 사용법

```bash
# 기본 설정으로 바이페드 및 쿼드러페드 데이터셋 생성
python synthetic_data_generator.py

# 50,000 샘플의 바이페드 데이터셋만 생성
python synthetic_data_generator.py --avatar biped --num_samples 50000

# 특정 보행 패턴만 생성
python synthetic_data_generator.py --gait run --num_samples 20000

# 생성 후 자동 분석
python synthetic_data_generator.py --num_samples 10000 --analyze --stats
```

### 명령줄 인수

| 인수 | 기본값 | 설명 |
|------|--------|------|
| `--num_samples` | `10000` | 생성할 샘플 수 |
| `--avatar` | `both` | 아바타 유형 (`biped`, `quadruped`, `both`) |
| `--gait` | `mixed` | 보행 유형 (`idle`, `walk`, `jog`, `run`, `jump`, `turn_left`, `turn_right`, `mixed`) |
| `--seed` | `42` | 재현성을 위한 랜덤 시드 |
| `--noise` | `0.02` | 관찰에 추가할 노이즈 수준 |
| `--output_dir` | `output/` | 출력 디렉토리 |
| `--analyze` | — | 생성 후 데이터셋 분석 실행 |
| `--stats` | — | 데이터셋 통계 출력 |

### 데이터셋 분석

```bash
# output/ 디렉토리의 모든 데이터셋 분석
python dataset_analyzer.py

# 특정 데이터셋 분석
python dataset_analyzer.py --dataset output/locomotion_biped_dataset.npz

# 플롯 표시 및 저장
python dataset_analyzer.py --show
```

---

## 데이터 형식

데이터셋은 NumPy 압축 형식(`.npz`)으로 저장됩니다.

### 파일 구조

```python
{
    "observations": np.ndarray,  # shape (N, obs_dim), dtype float32
    "actions":       np.ndarray,  # shape (N, act_dim), dtype float32
    # 메타데이터 (키-값 쌍):
    "gait_type":     str,         # JSON 직렬화된 보행 유형 목록
    "avatar_type":   str,         # "Humanoid" 또는 "Quadruped"
    "joint_count":   int,         # 관절 수
    "version":       str,         # 데이터셋 버전
    "num_samples":   int,         # 총 샘플 수
    "obs_dim":       int,         # 관찰 차원
    "act_dim":       int,         # 행동 차원
}
```

### 로드 예제

```python
import numpy as np

data = np.load("output/locomotion_biped_dataset.npz")
observations = data["observations"]  # (N, 120)
actions = data["actions"]            # (N, 80)
print(f"샘플: {observations.shape[0]}, 관찰: {observations.shape[1]}, 행동: {actions.shape[1]}")
```

---

## 관찰 인코딩

관찰 텐서는 `NeuralAnimationController`의 사양을 따릅니다. 입력 텐서 이름은 `"observation"`입니다.

### 바이페드 (120차원)

| 슬라이스 | 크기 | 구성 요소 | 설명 |
|----------|------|-----------|------|
| `[0:3]` | 3 | 현재 속도 | (전진, 수직, 측면) 로컬 속도 |
| `[3:6]` | 3 | 목표 속도 방향 | 정규화된 목표 이동 방향 |
| `[6]` | 1 | 현재 속력 | 스칼라 속력 값 |
| `[7:10]` | 3 | 몸통 기울기 오프셋 | (피치, 롤, 요) 기울기 |
| `[10:82]` | 72 | 관절 회전 | 18개 관절 × 4개 쿼터니언 |
| `[82:84]` | 2 | 발 지면 접촉 | (왼발, 오른발) 플래그 |
| `[84:90]` | 6 | 발 위치 | 3×2 = (L_xyz, R_xyz) 엉덩이 기준 |
| `[90:93]` | 3 | 머리 시선 방향 | 정규화된 시선 벡터 |
| `[93:96]` | 3 | 행동 목표 | 목표 위치 (x, y, z) |
| `[96:104]` | 8 | 스타일 임베딩 | 보행 특성 벡터 |
| `[104:120]` | 16 | 지형 높이맵 | 4×4 그리드 (평탄화됨) |

### 쿼드러페드 (150차원)

| 슬라이스 | 크기 | 구성 요소 |
|----------|------|-----------|
| `[0:104]` | 104 | 바이페드와 동일한 기본 구성 요소 |
| `[10:106]` | 96 | 관절 회전 (24개 관절 × 4) |
| `[104:150]` | 46 | 추가 지형 + 패딩 |

---

## 행동 디코딩

행동 텐서는 신경망 정책의 출력을 나타냅니다. 출력 텐서 이름은 `"action"`입니다.

### 바이페드 (80차원)

| 슬라이스 | 크기 | 구성 요소 | 설명 |
|----------|------|-----------|------|
| `[0:3]` | 3 | 루트 모션 델타 | (x, y, z) 변위 |
| `[3:7]` | 4 | 루트 회전 델타 | 쿼터니언 (x, y, z, w) |
| `[7:61]` | 54 | 관절 목표 회전 | 18개 관절 × 3 (오일러 각도) |
| `[61:69]` | 8 | 스타일 임베딩 출력 | 보행 특성 출력 |
| `[69:80]` | 11 | 예약/패딩 | 향후 사용 |

### 쿼드러페드 (100차원)

| 슬라이스 | 크기 | 구성 요소 |
|----------|------|-----------|
| `[0:3]` | 3 | 루트 모션 델타 |
| `[3:7]` | 4 | 루트 회전 델타 |
| `[7:79]` | 72 | 관절 목표 회전 (24개 관절 × 3) |
| `[79:87]` | 8 | 스타일 임베딩 출력 |
| `[87:100]` | 13 | 예약/패딩 |

---

## 아바타 설정

설정은 Unity의 `AnimationPolicy.cs`에 정의된 `PolicyMetadata`와 일치합니다.

```python
# 바이페드 (인간형)
{
    "obs_dim": 120,
    "act_dim": 80,
    "joint_count": 18,
    "terrain_resolution": 11,
    "style_dim": 8,
    "name": "Locomotion_Biped_Base",
    "avatar_type": "Humanoid",
}

# 쿼드러페드
{
    "obs_dim": 150,
    "act_dim": 100,
    "joint_count": 24,
    "terrain_resolution": 11,
    "style_dim": 8,
    "name": "Locomotion_Quadruped_Base",
    "avatar_type": "Quadruped",
}
```

---

## 보행 유형

생성기는 7가지 보행 유형을 지원하며, 각각 고유한 운동 패턴이 있습니다:

| 보행 | 설명 | 속도 범위 | 특징 |
|------|------|-----------|------|
| `idle` | 정지 | 0.0 m/s | 최소 움직임, 미세 떨림 |
| `walk` | 걷기 | 0.3–1.5 m/s | 안정적인 양발 교대 |
| `jog` | 조깅 | 1.0–3.5 m/s | 중간 속도, 활발한 보폭 |
| `run` | 달리기 | 2.0–6.0 m/s | 빠른 속도, 큰 진폭 |
| `jump` | 점프 | 0.5–5.0 m/s | 수직 충격, 공중 단계 |
| `turn_left` | 좌회전 | 0.3–2.5 m/s | 측면 기울기, 회전 |
| `turn_right` | 우회전 | 0.3–2.5 m/s | 측면 기울기, 회전 |
| `mixed` | 혼합 | — | 모든 보행을 균등하게 샘플링 |

---

## 분석

`dataset_analyzer.py`는 다음 플롯을 생성합니다:

| 플롯 파일 | 설명 |
|-----------|------|
| `*_obs_distribution.png` | 관찰 값 분포, 채널별 통계, 구성 요소 평균 |
| `*_act_distribution.png` | 행동 값 분포, 채널별 통계, 구성 요소 평균 |
| `*_obs_heatmap.png` | 관찰 채널 히트맵 (첫 80개 채널) |
| `*_act_heatmap.png` | 행동 채널 히트맵 |
| `*_obs_correlation.png` | 관찰 채널 간 상관 행렬 |
| `*_gait_distribution.png` | 보행 유형별 샘플 분포 |
| `*_obs_act_correlation.png` | 관찰-행동 산점도 (속도 구성 요소) |
| `*_gait_patterns.png` | 보행 유형별 속도 프로파일 |

---

## C# Unity 통합

Unity에서 생성된 데이터를 사용하려면:

```csharp
// Python에서 생성된 .npz 파일을 Unity에서 로드하는 예제
// (NativeArray를 사용한 사용자 정의 바이너리 리더 필요)

using UnityEngine;
using Unity.Collections;

public class DatasetLoader : MonoBehaviour
{
    public TextAsset datasetAsset; // .npz를 바이너리로 로드
    
    void LoadDataset()
    {
        byte[] data = datasetAsset.bytes;
        // NPZ 형식(ZIP 아카이브)을 파싱하려면
        // SharpZipLib 또는 Unity의 내장 압축 사용
        // 관찰: float[10000][120]
        // 행동: float[10000][80]
    }
}
```

> **참고:** NPZ는 ZIP 기반 형식입니다. Unity에서 .npz 파일을 로드하려면 `System.IO.Compression` 또는 `SharpZipLib`이 필요합니다. 또는 `numpy.savez_compressed` 대신 사용자 정의 바이너리 형식을 사용할 수 있습니다.

---

## 확장

### 새로운 보행 유형 추가

1. `synthetic_data_generator.py`의 `GAIT_TYPES` 목록에 추가
2. `_gait_phase()` 함수에 주파수 매핑 추가
3. `generate_gait_pattern()`에서 속도 프로파일 구현
4. `_generate_style_embedding()`에 스타일 임베딩 추가

### 새로운 아바타 유형 추가

1. `AVATAR_CONFIGS`에 새 항목 추가
2. 관찰/행동 레이아웃이 올바른지 확인
3. 적절한 관절 회전 생성기 구현

### 실제 Unity 데이터와 통합

Unity에서 실제 데이터를 캡처하려면:
1. `NeuralAnimationController`의 `EncodeObservation()`에서 관찰 버퍼 기록
2. `DecodeActions()` 후 행동 버퍼 기록
3. 바이너리 형식으로 내보내기
4. `synthetic_data_generator.py`의 `load_dataset()`을 사용하여 로드

---

## 문제 해결

### "No module named 'numpy'"
```bash
pip install -r requirements.txt
```

### "No .npz files found"
```bash
python synthetic_data_generator.py --num_samples 10000
```

### 플롯이 표시되지 않음
분석기는 기본적으로 `Agg` matplotlib 백엔드를 사용합니다. 대화형으로 표시하려면:
```bash
python dataset_analyzer.py --show
```

### 데이터셋 차원 불일치
데이터셋 생성 시 `--avatar` 플래그가 Unity 정책 메타데이터와 일치하는지 확인하세요.

---

## 라이선스

이 프로젝트는 Poison Unity 프로젝트의 일부입니다. 내부 사용 및 교육 목적으로 제공됩니다.