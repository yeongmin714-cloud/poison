# 🎮 프로시저럴 애니메이션 시스템 (Full Procedural Animation) - 구현 로드맵

> **목표**: GLB 모델(본 구조만)만 넣으면 **모든 애니메이션을 런타임에 프로시저럴로 생성**
> - 걷기/달리기/기본이동: 다리 위상(Phase) 기반 IK 보행 + 지형 적응
> - 점프/낙하: 탄도 역학 + 착지 IK
> - 공격/채집/상호작용: 상체 프로시저럴 IK + 타겟 추적
> - 구르기/회피: 루트 모션 + 전신 프로시저럴
> - 병사/몬스터/플레이어 공통 시스템 (본 구조만 다르면 자동 적용)

> **핵심 원칙**: **애니메이션 클립(.anim) 0개 사용** — 모든 모션은 코드에서 수학적으로 합성

---

## 📦 1단계: 기반 인프라 (Week 1) ✅ **완료**

### 1.1 공통 데이터 구조 ✅
- [x] `ProceduralBoneUtility.cs` — 본 이름 매핑 자동화 (spine/thigh/shin/foot/arm/hand 등)
- [x] `ProceduralBoneMap.cs` — 본 이름 자동 매핑 (Blender/Mixamo/Unity 명명 규칙 지원 + 번호 본 휴리스틱)
- [x] `LimbIKSolver.cs` — 2본/3본 FABRIK+CCD 하이브리드 IK 솔버 (Burst 호환)
- [x] `ProceduralAnimStateMachine.cs` — 상태 머신 (Locomotion/Jump/Attack/Gather/Roll/Climb/Stagger/Death)

### 1.2 Animation Rigging 완전 활용 ⏸️ **보류 (커스텀 구현으로 대체)**
- [ ] `MultiAimConstraint` — 시선/상체 방향 제어 (향후 확장)
- [ ] `MultiPositionConstraint` — 손/발 타겟 위치 제어
- [ ] `DampedTransform` — 부드러운 추적 (스프링-댐퍼)
- [ ] `ChainIKConstraint` — 팔/다리 체인 IK (커스텀 `LimbIKSolver`로 대체 구현됨)

---

## 🦵 2단계: 프로시저럴 로코모션 (Week 1-2) ✅ **완료 (2족)**

### 2.1 다리 위상 기반 보행 (Bipedal) ✅
```
Phase 0.0 ~ 0.5: 오른쪽 다리 스윙, 왼쪽 다리 스탠스
Phase 0.5 ~ 1.0: 왼쪽 다리 스윙, 오른쪽 다리 스탠스
```
- [x] `FootPlanner` — 다음 발 디딤 위치 계산 (속도, 회전, 지형 고려)
- [x] `SwingTrajectory` — 발 궤적 (포물선 + 지면 클리어런스)
- [x] `StanceStabilizer` — 스탠스 다리 지면 고정 + 몸체 지지
- [x] `HipShift` — 골반 좌우/상하 이동 (무게중심 이동)
- [x] `SpineCounterRotation` — 상체 반대 회전 (자연스러운 보행)

### 2.2 속도별 자동 전이 ✅
| 속도 | 위상 주기 | 스트라이드 길이 | 듀티 사이클 |
|------|----------|----------------|-------------|
| Idle (0) | 정지 | - | - |
| Walk (0.5) | 1.0s | 0.8m | 0.6 |
| Run (1.0) | 0.5s | 1.6m | 0.4 |
| Sprint (1.5+) | 0.35s | 2.2m | 0.3 |

### 2.3 지형 적응 ✅
- [x] 경사면 보행 (발목/무릎 각도 보정)
- [x] 계단/턱 감지 → 자동 스텝 업/다운
- [x] 불규칙 지형 → 발 독립적 높이 조절

---

## 🦘 3단계: 점프/낙하/착지 (Week 2) ✅ **완료**

### 3.1 점프 역학 ✅
- [x] `JumpArc` — 탄도 계산 (초속도, 중력, 목표 높이)
- [x] `PreJumpCrouch` — 착지 전 구부림 (에너지 저장 시각화)
- [x] `AirbornePose` — 공중 자세 (팔 벌림, 다리 접기)

### 3.2 착지 IK ✅
- [x] `LandingPredictor` — 착지 시점/위치 예측 (레이캐스트)
- [x] `ImpactAbsorption` — 착지 순간 무릎/발목/고관절 순차 굽힘
- [x] `RecoveryBlend` — 착지 후 보행 위상으로 부드러운 복귀

---

## ⚔️ 4단계: 상체 프로시저럴 액션 (Week 2-3) ✅ **완료**

### 4.1 공격 (Melee/Range) ✅
- [x] `AttackPhase` — 준비(챠지) → 휘두름(스윙) → 회수(리커버리)
- [x] `WeaponIK` — 무기 끝단이 타겟 향하도록 팔 체인 IK
- [x] `BodyTorque` — 골반/척추 회전으로 힘 전달 표현
- [x] `HitReaction` — 피격 시 프로시저럴 넉백/히트스탑

### 4.2 채집/상호작용 ✅
- [x] `ReachIK` — 손이 타겟(약초/상자/문) 닿도록 전신 IK
- [x] `GatherMotion` — 숙이기 → 잡기 → 일어나기 연속 모션
- [x] `LookAtTarget` — 시선/고개 자동 추적

### 4.3 구르기/회피 ✅
- [x] `RollTrajectory` — 구르기 호(아크) 계산
- [x] `InvertedPendulum` — 구르기 중 몸체 회전 (역진자 모델)
- [x] `InvincibilityFrames` — 무적 판정 동기화

---

## 🐺 5단계: 4족/몬스터 지원 (Week 3) ✅ **완료**

### 5.1 4족 보행 (Quadruped) ✅
- [x] `GaitSelector` — Walk(대각) → Trot(대각) → Pace(동측) → Gallop(비대칭)
- [x] `SpineWave` — 척추 파동 운동 (S자 곡선)
- [x] `LegPhaseOffset` — 전후좌우 다리 위상 오프셋

### 5.2 몬스터 전용 ✅ **완료**
- [x] `QuadrupedProceduralLocomotion` — 걸음걸이 자동 선택 (Walk/Trot/Pace/Gallop)
- [x] `QuadrupedProceduralAnimation` — 4다리 IK + 척추 파동 + 목 안정화
- [x] 점프/공격/피격 액션 오버라이드
- [x] **대형 몬스터: 중심 낮게, 보폭 크게** — `QuadrupedLargeMonster` 모듈 구현 완료 (Stomp 모드, 무게중심 하강, 착지 충격파)
- [x] **비행: 별도 모듈** — `QuadrupedFlying` 구현 완료 (Glide/Flap/Hover, 3본 날개 IK, 양력/항력 물리, 기류: 상승기류/돌풍)
- [x] **수영: 별도 모듈** — `QuadrupedSwimming` 구현 완료 (부력, 진행파 척추, 지느러미 IK, 항력 추진, 유체역학: 추가질량/회전항력)

---

## 🔧 6단계: 통합 & 폴리싱 (Week 3-4) ✅ **완료**

### 6.1 상태 머신 (Procedural State Machine) ✅
```
Locomotion(Walk/Run/Idle) ↔ Jump ↔ Airborne ↔ Landing
                       ↘ Attack/Gather/Roll (상체 오버라이드)
```
- [x] 전이 조건/블렌드 시간 정의
- [x] 상체/하체 레이어 분리 (애니메이션 레이어링)

### 6.2 디버그/튠 툴 ✅ **완료**
- [x] `ProceduralAnimDebugger` — Scene 뷰: 7가지 기즈모 (위상 원, IK 타겟/힌트, 발 배치 아크, 척추 웨이브, 걸음걸이 다이어그램, 속도 화살표, 지면 접촉)
- [x] 런타임 파라미터 트윈 (IMGUI 윈도우, 10개 파라미터 실시간 조절, 전체 컨트롤러 일괄 적용/리셋)

### 6.3 성능 최적화 ✅ **완료**
- [x] Job System + Burst Compiler (IK 솔버 병렬화 — `IJobParallelFor` 배치 처리)
- [x] 레이캐스트 배치 처리 (LOD 기반 스킵/감소)
- [x] LOD: 거리 멀면 간소화 (`ProceduralLODManager` — 4단계: Full → No Spine Counter → Foot IK Only → Simple Sine)

---

## 📋 현재 진행 상태

| 단계 | 상태 | 비고 |
|------|------|------|
| 1.1 공통 데이터 | ✅ **완료** | `ProceduralBoneUtility`, `LimbIKSolver` 신규 생성 |
| 1.2 Rigging 설정 | ⏸️ **보류** | 기존 `ProceduralPoseController` 완전 교체로 대체 (커스텀 IK로 충분) |
| 2.1 이족 보행 | ✅ **완료** | 핵심 — `ProceduralAnimationController` 구현 완료 |
| 2.2 속도 전이 | ✅ **완료** | |
| 2.3 지형 적응 | ✅ **완료** | |
| 3.1 점프 | ✅ **완료** | |
| 3.2 착지 | ✅ **완료** | |
| 4.1 공격 | ✅ **완료** | `ProceduralAttack` 신규 생성 |
| 4.2 채집 | ✅ **완료** | |
| 4.3 구르기 | ✅ **완료** | |
| 5.1 4족 | ✅ **완료** | `QuadrupedProceduralLocomotion`, `QuadrupedProceduralAnimation` 구현 완료 |
| 5.2 몬스터 | ✅ **완료** | 대형/비행/수영 모듈 추가 완료 |
| 6.1 상태 머신 | ✅ **완료** | `ProceduralAnimStateMachine` 구현 완료 |
| 6.2 디버그 툴 | ✅ **완료** | `ProceduralAnimDebugger` Scene 뷰 7가지 기즈모 + IMGUI 파라미터 창 |
| 6.3 성능 최적화 | ✅ **완료** | Job System + Burst 병렬화, 4단계 LOD 시스템 |

---

## 🚫 사용 금지/제거 대상

- `ProceduralPoseController` → **완전 교체** (보정만 하는 것)
- `RigAnimationController` → **완전 교체** (클립 재생기)
- `Player_Animator.controller` / 모든 `.anim` 클립 → **사용 안 함**
- `ModelAnimatorAssigner.AssignController` → **새 시스템으로 교체**

---

## ✅ 다음 액션 (즉시 시작 가능)

- ✅ **`ProceduralAnimDebugger.cs`** — Scene 뷰: 위상, IK 타겟, 위상 다이어그램 (완료)
- ✅ **Job System + Burst** — `LimbIKSolver` 병렬화 (100+ 캐릭터 60fps 목표) (완료)
- ✅ **대형 몬스터/비행/수영** — 별도 모듈 확장 (완료)
- ✅ **LOD 시스템** — 거리 멀면 간소화 (완료)

**모든 로드맵 항목 완료.** 다음 단계는 게임플레이 시스템(전투, 퀘스트, 월드맵) 연동입니다.

---

## 📝 메모

- **GLB 추가 불필요**: 현재 `Player_Rigged.glb` 본 구조만 있으면 됨
- **Animation Rigging 패키지** 필수 (이미 설치됨: `com.unity.animation.rigging@1.4.1`)
- **Burst/Job System** 사용으로 100+ 캐릭터도 60fps 목표
- **테스트 씬**: `Test_01_Player`에서 바로 검증 가능