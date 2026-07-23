# 🧠 Phase 4: Neural/Generative Animation Control System (NEW ROADMAP)

> **목표:** 수학적 프로시저럴 애니메이션 → **각 아바타가 AI(신경망)로 스스로 애니메이션을 제어하는 Generative/Neural Animation Control** 시스템으로 전면 교체
> 
> **핵심 개념:** 각 아바타(플레이어/병사/몬스터/NPC)마다 **자신의 상태/환경/목표를 관찰하고 실시간으로 애니메이션 파라미터를 생성하는 소형 신경망(Policy Network)**을 내장
> 
> **장점:** 수학 공식 하드코딩 불필요, 데이터 기반 자연스러운 모션 생성, 새로운 모션 학습 가능, 물리/환경 적응 자동화

---

## 🏗️ Phase 4.0: 아키텍처 설계 및 기반 인프라

### 4.0.1 — Neural Animation Core Architecture ✅ (컴파일 완료)
- [x] **NeuralAnimationController.cs** — 메인 컨트롤러 (ProceduralAnimationController 대체)
- [x] **AnimationPolicy.cs** — 정책 네트워크 추상화 (IPolicy, ONNXPolicy, ObservationEncoder, ActionDecoder)
- [x] **AnimationObservation.cs** — 관측 공간 정의 (State: velocity, terrain, target, joint states...)
- [x] **AnimationAction.cs** — 행동 공간 정의 (Action: bone rotations, root motion, IK targets, blend weights)
- [x] **ObservationEncoder.cs** — 관측값 정규화/인코딩 (StandardScaler, One-hot encoding)
- [x] **ActionDecoder.cs** — 행동 디코딩 (Bone rotation → Local/Global transform, Root motion extraction)

### 4.0.2 — ML Runtime Integration ✅ (컴파일 완료)
- [x] **MLRuntimeManager.cs** — Unity Inference Engine 싱글톤, 모델 로드/캐시/언로드
- [x] **ModelRegistry.cs** — 아바타 타입별 모델 매핑 (AnimationPolicy.cs의 AvatarType 사용)
- [x] **ModelLoader.cs** — ModelAsset → ModelLoader.Load() 방식
- [x] **InferenceScheduler.cs** — FixedUpdate 동기화, 배치 추론 스케줄링

### 4.0.3 — Training Data Pipeline (Offline)
- [ ] **MotionCaptureImporter.cs** — FBX/MBV/GLB 모션 캡처 데이터 임포트 (Mixamo, CMU, 라바 모션 등)
- [ ] **MotionClipSegmenter.cs** — 클립 분할 (Locomotion/Attack/React/Idle/Transition)
- [ ] **MotionFeatureExtractor.cs** — 특징 추출 (Root trajectory, Joint positions/velocities, Contact labels)
- [ ] **DatasetBuilder.cs** — (Observation, Action) 쌍 데이터셋 생성 → .npz / .tfrecord 저장
- [ ] **DataAugmentation.cs** — Mirroring, Noise injection, Time warping, Terrain variation

### 4.0.4 — Training Infrastructure (Offline/Python)
- [ ] **Training Configs** — PPO / SAC / TD3 / Diffusion Policy 하이퍼파라미터
- [ ] **Reward Functions** — Task Reward (목표 도달) + Style Reward (자연스러움) + Physics Reward (물리 타당성)
- [ ] **Curriculum Learning** — Easy terrain → Rough terrain → Combat → Multi-agent
- [ ] **Distillation Pipeline** — Teacher (Large Policy) → Student (Mobile/Quantized Policy)

---

## 🧠 Phase 4.1: Locomotion Policy (이동 정책 학습)

### 4.1.1 — Biped Locomotion Policy (2족: 플레이어/병사/휴머노이드)
- [ ] **관측 공간:** Root vel (3), Root ang vel (3), Joint positions (N×3), Joint velocities (N×3), Target dir (2), Target speed (1), Terrain heightmap (11×11), Contact flags (4)
- [ ] **행동 공간:** Joint target positions (N×3) 또는 Joint target rotations (N×4 quat), Root motion delta
- [ ] **보상 함수:** 
  - Velocity tracking (Target vel vs Actual vel)
  - Heading tracking (Target dir vs Actual dir) 
  - Energy penalty (Joint torque/velocity^2)
  - Foot contact consistency (Contact label vs Actual contact)
  - Joint limit penalty
  - Smoothness (Action delta penalty)
- [ ] **Curriculum:** Flat → Slopes → Stairs → Rough terrain → Moving platforms
- [ ] **Style Conditioning:** Walk/Run/Sprint/Crouch/Injured/Encumbered 스타일 임베딩

### 4.1.2 — Quadruped Locomotion Policy (4족: 늑대/멧돼지/사슴/용)
- [ ] **관측 공간:** 위 + Spine joints, Gait phase (0~1), Body orientation
- [ ] **Gait Conditioning:** Walk/Trot/Pace/Gallop/Amble 자동 전환 또는 명시적 지정
- [ ] **비대칭 지형 적응:** 경사/계단/장애물에서 다리 독립적 적응

### 4.1.3 — Multi-Leg/Creature Policies (다족/특수 생물)
- [ ] **Spider/Arachnid (8족):** Wave gait, Tripod gait
- [ ] **Centipede/Snake (다분절):** Serpentine, Sidewinding, Concertina
- [ ] **Flying (새/용):** Flapping/Gliding/Hovering 정책
- [ ] **Swimming (물고기/수생):** Undulatory/Thunniform/Oscillatory

### 4.1.4 — Locomotion Policy Export & Quantization
- [ ] **ONNX Export** — PyTorch/TensorFlow → ONNX (opset 17+)
- [ ] **Quantization** — INT8/UINT8 동적 양자화 (ONNX Runtime QDQ)
- [ ] **Model Optimization** — Graph optimization, Constant folding, Operator fusion
- [ ] **Validation** — Inference parity check (Python vs Unity runtime)

---

## ⚔️ Phase 4.2: Action/Interaction Policies (행동/상호작용 정책)

### 4.2.1 — Combat Policy (전투 정책)
- [ ] **Attack Policy:** Target tracking → Windup → Strike → Recovery → Blend back to locomotion
- [ ] **Defense/React Policy:** Block/Parry/Dodge/Stagger 반응 (상대 공격 방향/타입 관측)
- [ ] **Combo Policy:** 연계 공격 시퀀스 학습 (LSTM/Transformer policy)
- [ ] **Weapon-Specific:** Sword/Axe/Spear/Bow/Staff/Magic 각기 다른 정책 또는 공유 정책 + Weapon Embedding

### 4.2.2 — Interaction Policy (상호작용 정책)
- [ ] **Gather/Interact:** 접근 → 자세 잡기 → 상호작용 → 복귀
- [ ] **Climb/Vault:** 높이/거리 관측 → 적절한 모션 생성 (Procedural + Policy hybrid)
- [ ] **Mount/Dismount:** 탈것 탑승/하차 자세 동기화

### 4.2.3 — Social/Emote Policy (사회/감정 표현)
- [ ] **Gesture/Emote:** Wave/Point/Thumbs up/Dance 등
- [ ] **Dialogue Sync:** 립싱크 + 제스처 동기화 (Audio-driven animation)

---

## 🤖 Phase 4.3: High-Level Behavior & Decision Making (상위 행동 결정)

### 4.3.1 — Behavior Selector / Meta-Controller
- [ ] **High-Level Policy (HLP):** Goal → Sub-goal 분해 (Navigation → Combat → Interact)
- [ ] **Behavior Tree / HTN Integration:** Policy를 Leaf node로 사용하는 BT/HTN
- [ ] **LLM/VLM Integration (Optional):** 자연어 지시 → Behavior Plan → Policy 실행

### 4.3.2 — Multi-Agent Coordination
- [ ] **Formation Policy:** 대형 유지하며 이동 (병사 부대)
- [ ] **Flank/Ambush Policy:** 전술적 위치 선정
- [ ] **Communication Policy:** 암묵적/명시적 신호 교환

---

## 🎮 Phase 4.4: Unity Integration & Runtime System

### 4.4.1 — NeuralAnimationController (Runtime)
- [ ] **Policy Switching:** Locomotion ↔ Combat ↔ Interact 매끄러운 전환 (Blend trees in latent space)
- [ ] **Root Motion Handling:** Policy가 출력하는 Root motion → CharacterController/NavMeshAgent 동기화
- [ ] **IK Layer on Top:** Policy 출력 후 Foot/Hand IK 보정 (Ground contact, Target reaching)
- [ ] **Physics Integration:** Predicted pose → Physics simulation (Ragdoll blend, Collider adjustment)

### 4.4.2 — Inference Optimization
- [ ] **Batched Inference:** 동일 모델 공유 아바타들 배치 추론 (GPU/NPU)
- [ ] **Async Inference:** Frame 지연 허용 → Double buffering (Frame N 추론, Frame N+1 적용)
- [ ] **LOD Inference:** 원거리 아바타 → 저해상도 정책 / 추론 스킵 / 단순 Procedural fallback
- [ ] **Model Streaming:** 원거리/미사용 모델 언로드, 필요 시 비동기 로드

### 4.4.3 — Debug & Visualization
- [ ] **Policy Visualizer:** 관측값, 행동값, 보상값 실시간 그래프
- [ ] **Latent Space Explorer:** Style embedding 공간 탐색
- [ ] **Trajectory Comparison:** Policy trajectory vs Reference motion

---

## 📦 Phase 4.5: Model Zoo & Content Pipeline

### 4.5.1 — Pre-trained Model Zoo
| Model | Avatar Types | Input Dim | Output Dim | Size (INT8) |
|-------|-------------|-----------|------------|-------------|
| `Locomotion_Biped_Base.onnx` | Player, Soldier, Humanoid NPC | ~120 | ~80 | ~2MB |
| `Locomotion_Quadruped_Base.onnx` | Wolf, Boar, Deer, Cat | ~150 | ~100 | ~3MB |
| `Combat_Melee_Universal.onnx` | All bipeds with weapons | ~200 | ~120 | ~4MB |
| `Combat_Ranged_Universal.onnx` | Archer, Mage, Turret | ~180 | ~100 | ~3MB |
| `React_Hit_Stagger.onnx` | All creatures | ~100 | ~60 | ~1MB |
| `Interact_Gather.onnx` | Player, NPC | ~80 | ~40 | ~0.5MB |

### 4.5.2 — Avatar-Specific Fine-tuning
- [ ] **Player Personalization:** 플레이어 스타일 학습 (Aggressive/Defensive/Evasive)
- [ ] **Boss/Unique Monsters:** 전용 정책 미세조정
- [ ] **Equipment Adaptation:** 무기/방어구 무게/형상에 따른 정책 적응 (LoRA/Adapter)

### 4.5.3 — Runtime Model Management
- [ ] **Model Versioning:** A/B 테스트, 롤백 지원
- [ ] **Dynamic Model Loading:** Addressables/AssetBundle로 모델 스트리밍
- [ ] **Fallback Chain:** Neural → Procedural → Keyframe (Priority fallback)

---

## 🔄 Phase 4.6: Migration from Procedural → Neural (점진적 마이그레이션)

### 4.6.1 — Hybrid Controller (Phase 3.9 → 4.0 Bridge) ✅
- [x] **HybridAnimationController.cs** — Procedural + Neural 동시 실행, 가중 블렌딩
- [x] **Policy Override System:** 특정 상태(Combat/React/Fly/Swim)에서만 Neural, 나머지는 Procedural
- [x] **A/B Test Framework:** 동일 아바타에 Procedural vs Neural 할당 → 메트릭 비교

### 4.6.2 — Progressive Rollout
| Phase | Scope | Policy | Fallback |
|-------|-------|--------|----------|
| 4.6.1 | Player only | Locomotion | Procedural |
| 4.6.2 | Player + Soldiers | Locomotion + Combat | Procedural |
| 4.6.3 | All Bipeds | All policies | Procedural |
| 4.6.4 | Quadrupeds | Locomotion + React | Procedural |
| 4.6.5 | All Creatures | Full Neural | Keyframe (Cinematics) |

### 4.6.3 — Deprecation Plan
- [ ] Phase 3.9 Procedural 코드 `Obsolete` 속성 표시
- [ ] Test Scene에서 ProceduralController 완전 제거
- [ ] Documentation 마이그레이션 가이드 작성

---

## 📊 Phase 4.7: Evaluation & Quality Assurance

### 4.7.1 — Quantitative Metrics
- [ ] **Motion Quality:** FID (Frechet Inception Distance) vs Motion Capture
- [ ] **Task Success Rate:** Navigation success, Combat hit rate, Interaction completion
- [ ] **Physics Validity:** Penetration depth, Floating feet, Joint limit violation
- [ ] **Performance:** Inference latency (ms), Memory (MB), FPS impact

### 4.7.2 — Qualitative Evaluation
- [ ] **User Study:** 자연스러움/반응성/몰입도 평가 (Likert scale)
- [ ] **A/B Test:** Procedural vs Neural 플레이테스트
- [ ] **Edge Case Catalog:** 계단, 경사, 장애물, 네트워크 지연 등

### 4.7.3 — Regression Testing
- [ ] **Automated Playtest:** 100+ 시나리오 자동 실행 → 메트릭 수집
- [ ] **Golden Master Comparison:** 이전 버전 모션과 시각적 비교 (SSIM/LPIPS)

---

## 🛠️ Phase 4.8: Tools & Editor Integration

### 4.8.1 — Neural Animation Authoring Tools
- [ ] **Policy Inspector:** 모델 입력/출력/가중치 시각화
- [ ] **Style Editor:** Style embedding 슬라이더로 실시간 모션 스타일 변경
- [ ] **Transition Designer:** 정책 간 전이 편집기 (Blend duration, Latent interpolation)
- [ ] **Reward Tuner:** 보상 함수 가중치 실시간 조정 → 재학습 트리거

### 4.8.2 — Training Dashboard (Unity ↔ Python Bridge)
- [ ] **TensorBoard Integration:** Unity에서 학습 곡선 실시간 모니터링
- [ ] **Checkpoint Manager:** 베스트 모델 자동 선택/배포
- [ ] **Hyperparameter Sweep:** Optuna/Ray Tune 연동

---

## 📅 타임라인 (예상)

| Phase | 기간 | 마일스톤 |
|-------|------|----------|
| 4.0 | 2주 | Core Architecture + ML Runtime 통합 |
| 4.1 | 4주 | Locomotion Policy 학습 완료 (Biped/Quadruped) |
| 4.2 | 3주 | Combat/Interaction Policy 학습 |
| 4.3 | 2주 | High-level Behavior + Multi-agent |
| 4.4 | 2주 | Runtime Integration + Optimization |
| 4.5 | 1주 | Model Zoo 구축 + Content Pipeline |
| 4.6 | 2주 | 점진적 마이그레이션 (Hybrid → Full Neural) |
| 4.7 | 1주 | QA + 평가 + 문서화 |
| 4.8 | 1주 | Editor Tools + Training Dashboard |
| **Total** | **~18주** | **Neural Animation System Complete** |

---

## 🎯 성공 기준 (Definition of Done)

1. **Performance:** 추론 지연 < 2ms (Biped), < 3ms (Quadruped) @ 60fps, Batch 32
2. **Quality:** Motion FID < 0.1 vs Motion Capture reference
3. **Coverage:** 모든 아바타 타입(플레이어/병사/몬스터/NPC/탈것) Neural Policy 적용
4. **Fallback:** Neural 실패 시 Procedural → Keyframe 자동 폴백 100% 작동
5. **Workflow:** 새로운 몬스터 추가 시 모션 캡처 데이터만으로 1일 내 정책 학습/배포 가능
6. **No Regression:** 기존 게임플레이(전투/이동/상호작용) 기능 100% 유지

---

## 🔗 참고 자료 및 레퍼런스

### Papers
- **PFNN** (Phase-Functioned Neural Networks) — Holden et al. 2017
- **Mode-Adaptive Neural Networks** — Zhang et al. 2018
- **Neural State Machine** — Starke et al. 2019
- **Local Motion Phases** — Starke et al. 2020
- **ASE (Adversarial Skill Embedding)** — Peng et al. 2022
- **Diffusion Policy** — Chi et al. 2023
- **AMass/CMu Motion Capture Datasets**

### Unity Packages
- **Unity Sentis** (ONNX Runtime on Unity) — 공식 추론 엔진
- **Barracuda** (Legacy, deprecated but still usable)
- **ML-Agents** — 훈련 환경, PPO/SAC 구현체
- **Animation Rigging** — Policy 위에 IK 레이어 얹기

### Assets/Datasets
- **Mixamo** — 무료 리깅/애니메이션 (프로토타입용)
- **CMU Motion Capture** — 학습 데이터
- **LaFAN1 / HumanML3D / AMASS** — 고품질 모션 데이터셋
- **Mixamo + Retargeting** → 독자 캐릭터 리깅 파이프라인

---

## 📝 마이그레이션 체크리스트 (기존 Phase 3.9 코드)

| 기존 파일 | 신규 대체 | 상태 |
|-----------|-----------|------|
| `ProceduralAnimationController.cs` | `NeuralAnimationController.cs` | 🔄 예정 |
| `ProceduralAnimStateMachine.cs` | `PolicySelector.cs` + `BehaviorTree` | 🔄 예정 |
| `BipedLocomotionModules.cs` | `LocomotionPolicy.onnx` + `BipedPolicyRunner.cs` | 🔄 예정 |
| `QuadrupedLocomotionModules.cs` | `QuadrupedPolicy.onnx` + `QuadrupedPolicyRunner.cs` | 🔄 예정 |
| `LimbIKSolver.cs` | `IKLayer.cs` (Policy 위에 애드온) | ✅ 유지/개선 |
| `ProceduralAttack.cs` | `CombatPolicy.onnx` | 🔄 예정 |
| `ProceduralLODManager.cs` | `NeuralLODManager.cs` (Policy LOD) | 🔄 예정 |
| `ModelAnimatorAssigner.cs` | `NeuralModelAssigner.cs` (Model→Policy 매핑) | 🔄 예정 |

---

> **다음 단계:** Phase 4.0.1 ~ 4.0.4 기반 인프라 구축부터 시작합니다.  
> `delegate_task`로 code agent 위임하여 `NeuralAnimationController.cs`, `MLRuntimeManager.cs`, `AnimationPolicy.cs` 등 핵심 파일 생성 진행하겠습니다.