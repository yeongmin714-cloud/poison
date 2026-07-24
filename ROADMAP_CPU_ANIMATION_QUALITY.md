# 🖥️ Phase 68: CPU 애니메이션 품질 최대화 (CPU-Training Roadmap)

> **목표:** GPU 없이 순수 CPU 환경에서 애니메이션 품질을 최상으로 끌어올린다.
> **현재:** numpy PPO [64, 64] hidden, 50 epoch → **목표:** PyTorch CPU [256, 128, 64] hidden, 1000 epoch
> 
> **Unity 6000.4.10f1**, URP 17.4.0, WSL Ubuntu (CPU only)

---

## 🏗️ Phase 68.0: CPU 학습 파이프라인 업그레이드 (기반)

### 68.0.1 — PyTorch CPU 설치
> numpy 수동 역전파 한계 극복. PyTorch CPU로 autodiff + 대형 네트워크 지원.

| 작업 | 설명 | 예상시간 |
|:-----|:------|:--------:|
| PyTorch CPU 설치 | `pip install torch --index-url https://download.pytorch.org/whl/cpu` | ~5분 |
| 기존 numpy_ppo 호환성 유지 | numpy_ppo.py는 fallback으로 보존, 새 PyTorch 버전과 병행 | ~10분 |
| torch_ppo.py 작성 | numpy_ppo 기반 → PyTorch autodiff PPO 구현 | ~30분 |
| onnx_export 통합 | torch.onnx.export()로 바로 ONNX 변환 | ~10분 |

- [ ] PyTorch CPU 설치 완료
- [ ] torch_ppo.py (torch PPO 학습기) 작성
- [ ] 기존 numpy_ppo fallback 유지

### 68.0.2 — 네트워크 확장

| 네트워크 | Hidden | 파라미터 | 파일 크기 | CPU 추론 시간 |
|:---------|:-------|:--------:|:---------:|:-------------:|
| 🟢 **현재 (numpy)** | [64, 64] | ~15K | ~69KB | < 0.1ms |
| 🟡 **업그레이드1** | [128, 128] | ~55K | ~220KB | < 0.3ms |
| 🔴 **업그레이드2** | [256, 128, 64] | ~100K | ~400KB | < 0.5ms |
| 🟣 **고급** | [512, 256, 128] | ~330K | ~1.3MB | < 1ms |

> **선택:** [256, 128, 64] 3-layer가 CPU에서 최적의 품질/속도 밸런스

- [ ] 네트워크 [256, 128, 64] 3-layer PPO 구현
- [ ] Unity Sentis NHWC 호환성 유지

### 68.0.3 — 3D 물리 시뮬레이션 환경 (CPU)
> 현재 2D kinematic chain → 3D 물리 시뮬레이션으로 업그레이드

| 기능 | 설명 |
|:-----|:------|
| 3D Joint Chain | 18개 본(biped) / 24개 본(quadruped) 3D 시뮬레이션 |
| 중력/관성 | 실제 중력 (-9.81) + 관성 텐서 |
| 지형 높이맵 | 평지/경사/계단/장애물 11×11 샘플링 |
| 접촉 감지 | 발/발굽 접촉 → Ground Contact Label |
| 자유도 | 관절별 3DOF 회전 |

- [ ] 3D 물리 환경 구현 (simple_animation_env_v2.py)
- [ ] 지형 높이맵 + 접촉 감지 통합
- [ ] Biped/Quadruped 공통 환경

### 68.0.4 — 보상 함수 고도화

| 보상 종류 | 가중치 | 설명 |
|:----------|:------:|:------|
| 🎯 **Velocity Tracking** | 1.0 | 목표 속도 vs 실제 속도 일치 |
| 🧭 **Heading Tracking** | 0.8 | 목표 방향 vs 실제 이동 방향 |
| ⚡ **Energy Penalty** | 0.1 | 토크 제곱합 (자연스러움) |
| 🦶 **Foot Contact** | 0.5 | 접촉 패턴 vs 기대 패턴 일치 |
| 📏 **Joint Limit** | 0.3 | 관절 한계 초과 시 페널티 |
| 🔄 **Smoothness** | 0.2 | 연속 액션 간 차이 페널티 |
| 🏔️ **Terrain Adaptation** | 0.4 | 지형 높이 변화에 따른 발 위치 적응 |
| 🧘 **Pose Regularization** | 0.3 | 기준 자세(Reference pose)와 유사도 |

- [ ] 보상 함수 8종 구현 + 가중치 합성
- [ ] Curriculum Learning 연동 (Easy→Medium→Hard)

---

## 🧠 Phase 68.1: Biped 10종 고품질 재학습

> **목표:** [256, 128, 64] 네트워크, 1000 epoch, 3D 물리 환경

| # | 모델 | Epoch | 예상시간 | 현재 품질 | 목표 품질 |
|:-:|:-----|:-----:|:--------:|:---------:|:---------:|
| 1 | locomotion_biped | 1000 | ~30분 | ⭐⭐ | ⭐⭐⭐⭐⭐ |
| 2 | combat_biped | 1000 | ~30분 | ⭐⭐ | ⭐⭐⭐⭐⭐ |
| 3 | react_biped | 1000 | ~30분 | ⭐⭐ | ⭐⭐⭐⭐⭐ |
| 4 | interact_biped | 1000 | ~30분 | ⭐⭐ | ⭐⭐⭐⭐⭐ |
| 5 | fly_biped | 500 | ~15분 | ⭐ | ⭐⭐⭐⭐ |
| 6 | swim_biped | 500 | ~15분 | ⭐ | ⭐⭐⭐⭐ |
| 7 | mount_biped | 500 | ~15분 | ⭐ | ⭐⭐⭐⭐ |
| 8 | climb_biped | 500 | ~15분 | ⭐ | ⭐⭐⭐⭐ |
| 9 | run_biped (Style) | 500 | ~15분 | ⭐⭐ | ⭐⭐⭐⭐ |
| 10 | crouch_biped (Style) | 500 | ~15분 | ⭐⭐ | ⭐⭐⭐⭐ |

**총 예상시간:** ~3.5시간 (CPU, WSL)

- [ ] Biped locomotion 1000 epoch 학습
- [ ] Biped combat/react/interact 1000 epoch 학습
- [ ] Biped fly/swim/mount/climb 500 epoch 학습
- [ ] Biped style (run/crouch) 500 epoch 학습

---

## 🐾 Phase 68.2: Quadruped 10종 고품질 재학습

| # | 모델 | Epoch | 예상시간 |
|:-:|:-----|:-----:|:--------:|
| 1 | locomotion_quadruped | 1000 | ~40분 |
| 2 | combat_quadruped | 1000 | ~40분 |
| 3 | react_quadruped | 1000 | ~40분 |
| 4 | interact_quadruped | 1000 | ~40분 |
| 5 | fly_quadruped | 500 | ~20분 |
| 6 | swim_quadruped | 500 | ~20분 |
| 7 | mount_quadruped | 500 | ~20분 |
| 8 | large_monster_quadruped | 500 | ~20분 |
| 9 | run_quadruped (Style) | 500 | ~20분 |
| 10 | crouch_quadruped (Style) | 500 | ~20분 |

**총 예상시간:** ~4.5시간 (CPU, WSL)

- [ ] Quadruped locomotion 1000 epoch 학습
- [ ] Quadruped combat/react/interact 1000 epoch 학습
- [ ] Quadruped fly/swim/mount/large_monster 500 epoch 학습
- [ ] Quadruped style (run/crouch) 500 epoch 학습

---

## 🔬 Phase 68.3: 고급 학습 기법 (CPU 최적화)

### 68.3.1 — Curriculum Learning 고도화

| 단계 | 지형 | 속도 | 장애물 | 기간 |
|:----:|:-----|:----:|:------:|:----:|
| 1️⃣ Easy | 평지 | Walk | 없음 | 10% |
| 2️⃣ Medium | 경사 10° | Walk/Run | 돌 1개 | 20% |
| 3️⃣ Hard | 경사 20° | Run/Sprint | 돌 3개 | 30% |
| 4️⃣ Expert | 계단/턱 | Full | 장애물 5개 | 25% |
| 5️⃣ Master | 혼합 지형 | Full | 동적 장애물 | 15% |

### 68.3.2 — Ensemble 학습
- 3개 시드(42, 123, 456) 독립 학습 → 가중치 평균 앙상블
- 앙상블 모델의 Outlier 제거로 안정성 ↑

### 68.3.3 — Style Embedding 고도화
- 5개 스타일 (Walk/Run/Crouch/Sprint/Creep)
- 8차원 Style Embedding (현재 3차원 → 확장)

### 68.3.4 — Behavior Cloning (모방 학습)
- 기존 프로시저럴 애니메이션 출력을 Teacher로 사용
- Student 네트워크가 Teacher 모방 → 더 자연스러운 초기화

### 68.3.5 — Knowledge Distillation
- Large Teacher (1024, 512, 256) → Small Student (256, 128, 64)
- Teacher의 품질을 Student로 압축

---

## ⚡ Phase 68.4: 추론 최적화 (CPU 실시간)

### 68.4.1 — LOD 4단계 최적화

| LOD | 거리 | Network | 추론 주기 | FPS 영향 |
|:---:|:----:|:--------|:---------:|:--------:|
| LOD0 | 0~20m | Full [256,128,64] | 매 프레임 | < 1ms |
| LOD1 | 20~50m | Medium [128,64] | 2프레임마다 | < 0.3ms |
| LOD2 | 50~100m | Small [64,64] | 4프레임마다 | < 0.1ms |
| LOD3 | 100m+ | Procedural fallback | 없음 | 0ms |

### 68.4.2 — Batch Inference
- 동일 모델 사용 아바타 → 배치 추론
- CPU에서는 batch_size=4~8 최적

### 68.4.3 — INT8 양자화
- ONNX Runtime INT8 동적 양자화
- 파일 크기 75% 감소, 추론 속도 2배 향상

### 68.4.4 — Worker Pooling
- 정책별 Worker 캐싱 (이미 구현)
- 메모리 사용량 최적화

---

## 🎮 Phase 68.5: 게임 개발 지속

### 68.5.1 — 컴파일 에러 0 유지보수
- Unity 6 API 변경 대응 (FindObjectOfType → FindAnyObjectByType 등)
- 최신 패키지 호환성 유지

### 68.5.2 — QA 전수 점검
- 9개 테스트 씬 Play 테스트
- Neural Animation 런타임 검증
- 프로시저럴 → Neural 전환 품질 비교

### 68.5.3 — 밸런스 조정
- 몬스터/병사 전투력 밸런스
- 크래프트 성공률
- 영지 점령 난이도

### 68.5.4 — 성능 최적화
- CPU 프로파일링
- Neural Animation 병목 제거
- HybridController LOD 튜닝

### 68.5.5 — GLB 모델 대응
- 신규 GLB 투입 시 ModelSwapper 자동 교체
- 리깅 GLB → NeuralAnimationController 자동 연결

---

## 📊 예상 일정

| Phase | 내용 | 예상 시간 | 상태 |
|:------|:-----|:---------:|:----:|
| 68.0.1 | PyTorch CPU 설치 + torch_ppo.py | 1시간 | 📝 |
| 68.0.2 | 네트워크 확장 ([256,128,64]) | 30분 | 📝 |
| 68.0.3 | 3D 물리 환경 | 1시간 | 📝 |
| 68.0.4 | 보상 함수 고도화 | 30분 | 📝 |
| 68.1 | Biped 10종 고품질 학습 | 3.5시간 | 📝 |
| 68.2 | Quadruped 10종 고품질 학습 | 4.5시간 | 📝 |
| 68.3 | 고급 학습 기법 | 1시간 | 📝 |
| 68.4 | 추론 최적화 | 1시간 | 📝 |
| 68.5 | 게임 개발 지속 | 2시간 | 📝 |
| **Total** | | **~15시간** | 📝 |

---

## ⚠️ 알려진 제약

1. **CPU 학습 속도:** GPU 대비 10~50배 느림 (1000 epoch = ~30분~1시간)
2. **네트워크 크기:** 큰 네트워크([1024,512,256])는 CPU 추론에서 2ms+ → LOD 분리 필요
3. **PyTorch → ONNX:** torch.onnx.export()로 opset 17, NHWC 유지 필수
4. **Unity Sentis 호환:** Gemm/Tanh/Reshape만 사용 (복잡한 op 미지원)

---

## 🔗 관련 파일

- `ROADMAP_NEURAL_ANIMATION.md` — 기존 Neural Animation 로드맵
- `ROADMAP_TRAINING_LIGHTWEIGHT.md` — 기존 경량 CPU 학습 로드맵
- `Assets/Training/TrainingInfra/numpy_ppo.py` — 기존 numpy PPO
- `Assets/Training/TrainingInfra/train_lightweight.py` — 기존 학습 CLI
- `Assets/Scripts/Systems/Animation/Neural/` — Neural Animation C# 코드