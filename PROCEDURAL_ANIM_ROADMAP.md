# 🎮 프로시저럴 애니메이션 시스템 (Full Procedural Animation) - 구현 로드맵

> **목표**: GLB 모델(본 구조만)만 넣으면 **모든 애니메이션을 런타임에 프로시저럴로 생성**
> - 걷기/달리기/기본이동: 다리 위상(Phase) 기반 IK 보행 + 지형 적응
> - 점프/낙하: 탄도 역학 + 착지 IK
> - 공격/채집/상호작용: 상체 프로시저럴 IK + 타겟 추적
> - 구르기/회피: 루트 모션 + 전신 프로시저럴
> - 병사/몬스터/플레이어 공통 시스템 (본 구조만 다르면 자동 적용)

> **핵심 원칙**: **애니메이션 클립(.anim) 0개 사용** — 모든 모션은 코드에서 수학적으로 합성
> **폴더 구조**: `Assets/Scripts/Systems/Animation/Procedural/` 아래 모듈별 정리

---

## 📦 1단계: 기반 인프라 ✅ **완료**

### 1.1 공통 데이터 구조 ✅
- [x] `Bones/BoneRole.cs` — 본 역할 열거형 분리
- [x] `Bones/ProceduralBoneUtility.cs` — 본 이름 매핑 자동화 (Blender/Mixamo/Unity 명명 규칙 지원 + 번호 본 휴리스틱)
- [x] `Bones/ProceduralBoneMap.cs` — 런타임 본 캐싱 MonoBehaviour
- [x] `IK/LimbIKSolver.cs` — 2본/3본 FABRIK+CCD 하이브리드 IK 솔버 (Burst 호환)
- [x] `ProceduralAnimStateMachine.cs` — 상태 머신 (Locomotion/Jump/Attack/Gather/Roll/Climb/Stagger/Death)

### 1.2 Animation Rigging 완전 활용
- [x] `LimbIKSolver.cs` — ChainIKConstraint 대체 (커스텀 IK)

---

## 🦵 2단계: 프로시저럴 로코모션 (2족) ✅ **완료**

### 2.1 다리 위상 기반 보행 (Bipedal) ✅
- [x] `Locomotion/Biped/BipedLocomotionModules.cs` — 발 디딤, 궤적, 스탠스, 골반/척추
- [x] `ProceduralAnimationController.cs` — 메인 컨트롤러 (2족 로코모션 통합)

### 2.2 속도별 자동 전이 ✅
- [x] Idle(0) / Walk(0.5) / Run(1.0) / Sprint(1.5+)

### 2.3 지형 적응 ✅
- [x] 경사면 보행 (발목/무릎 각도 보정)
- [x] 계단/턱 감지 → 자동 스텝 업/다운
- [x] 불규칙 지형 → 발 독립적 높이 조절

---

## 🦘 3단계: 점프/낙하/착지 ✅ **완료**

### 3.1 점프 역학 ✅
- [x] `Actions/JumpLandModules.cs` — 점프 탄도 + 공중 자세

### 3.2 착지 IK ✅
- [x] `Actions/JumpLandModules.cs` — 착지 예측/충격 흡수/보행 복귀

---

## ⚔️ 4단계: 상체 프로시저럴 액션 ✅ **완료**

### 4.1 공격 (Melee/Range) ✅
- [x] `Actions/ActionModules.cs` — 챠지→스윙→회수, 무기 IK, BodyTorque
- [x] `Combat/ProceduralAttack.cs` — 콤보, 타이밍, 히트박스, 히트스탑
- [x] `Combat/AttackData.cs` — 공격 데이터 ScriptableObject
- [x] `Combat/Damageable.cs` — IDamageable + 데미지 처리

### 4.2 채집/상호작용 ✅
- [x] `Actions/ActionModules.cs` — ReachIK, GatherMotion, LookAtTarget

### 4.3 구르기/회피 ✅
- [x] `Actions/ActionModules.cs` — RollTrajectory, InvertedPendulum, InvincibilityFrames

---

## 🐺 5단계: 4족/몬스터 지원 ✅ **99% 완료**

### 5.1 4족 보행 (Quadruped) ✅
- [x] `Locomotion/Quadruped/QuadrupedLocomotionModules.cs` — GaitSelector, SpineWave, LegPhaseOffset
- [x] `QuadrupedProceduralLocomotion.cs` — Walk/Trot/Pace/Gallop 자동 전이
- [x] `QuadrupedProceduralAnimation.cs` — 4다리 IK + 척추 파동 + 목 안정화

### 5.2 몬스터 전용 ✅
- [x] `Locomotion/Quadruped/Extensions/QuadrupedFlying.cs` — **비행 모듈**
- [x] `Locomotion/Quadruped/Extensions/QuadrupedSwimming.cs` — **수영 모듈**
- [x] `Locomotion/Quadruped/Extensions/QuadrupedLargeMonster.cs` — **대형 몬스터**
- [x] 점프/공격/피격 액션 오버라이드

---

## 🔧 6단계: 통합 & 폴리싱 ✅ **99% 완료**

### 6.1 상태 머신 ✅
- [x] `ProceduralAnimStateMachine.cs` — 전체 상태 전이 관리
- [x] 상체/하체 레이어 분리

### 6.2 디버그/튠 툴 ✅
- [x] `Debug/ProceduralAnimDebugger.cs` — Scene 뷰: 위상, IK 타겟, 위상 다이어그램
- [x] `ParentVelocityProvider.cs` — 부모 속도 제공 유틸리티

### 6.3 성능 최적화 🔄
- [x] `LOD/ProceduralLODSystem.cs` — 거리 기반 LOD 간소화
- [ ] Job System + Burst Compiler (IK 솔버 병렬화 — 향후)
- [ ] 레이캐스트 배치 처리 (향후)

---

## 📋 현재 진행 상태

| 단계 | 상태 | 비고 |
|------|------|------|
| 1.1 공통 데이터 | ✅ **완료** | `BoneRole`, `ProceduralBoneUtility`, `ProceduralBoneMap`, `LimbIKSolver` |
| 1.2 Rigging 설정 | ✅ **완료** | `LimbIKSolver`로 대체 구현 |
| 2.1 이족 보행 | ✅ **완료** | `BipedLocomotionModules` + `ProceduralAnimationController` |
| 2.2 속도 전이 | ✅ **완료** | |
| 2.3 지형 적응 | ✅ **완료** | |
| 3.1 점프 | ✅ **완료** | `JumpLandModules` |
| 3.2 착지 | ✅ **완료** | |
| 4.1 공격 | ✅ **완료** | `ActionModules`, `ProceduralAttack`, `AttackData`, `Damageable` |
| 4.2 채집 | ✅ **완료** | |
| 4.3 구르기 | ✅ **완료** | |
| 5.1 4족 | ✅ **완료** | `QuadrupedLocomotionModules`, `QuadrupedProceduralAnimation` |
| 5.2 몬스터 | ✅ **99%** | **비행/수영/대형 몬스터 모듈 추가 완료** |
| 6.1 상태 머신 | ✅ **완료** | |
| 6.2 디버그 툴 | ✅ **완료** | `ProceduralAnimDebugger` + `ParentVelocityProvider` |
| 6.3 성능 최적화 | 🔄 **70%** | LOD 완료, Job System/Burst는 향후 |

---

## 🚫 사용 금지/제거 대상 (기존 애니메이션 시스템과의 충돌 방지)

- `ProceduralPoseController` → **완전 교체** (보정만 하는 것, 프로시저럴로 대체)
- `RigAnimationController` → **완전 교체** (클립 재생기)
- `Player_Animator.controller` / 모든 `.anim` 클립 → **사용 안 함** (프로시저럴만 사용)
- `ModelAnimatorAssigner.AssignController` → **새 시스템으로 교체**

---

## ✅ 남은 작업 (소소)

1. **Job System + Burst** — `LimbIKSolver` 병렬화 (100+ 캐릭터 60fps 목표, 성능 최적화)
2. **레이캐스트 배치 처리** — 지형 샘플링 최적화
3. **실제 테스트 씬 연동** — `Test_01_Player`에서 `ProceduralAnimationController` 사용하도록 연결
4. **QA 점검** — 나머지 테스트 씬 7개 (Test_02_UI ~ Test_09_AllInOne)

---

## 📝 메모

- **GLB 추가 불필요**: 현재 `Player_Rigged.glb` 본 구조만 있으면 됨
- **Animation Rigging** 패키지 설치됨 (`com.unity.animation.rigging@1.4.1`)
- **폴더 구조**: `Assets/Scripts/Systems/Animation/Procedural/`
  - `Bones/` — 본 매핑
  - `IK/` — IK 솔버
  - `Locomotion/Biped/` — 2족 로코모션
  - `Locomotion/Quadruped/Extensions/` — 4족 비행/수영/대형
  - `Actions/` — 공격/점프/채집/구르기
  - `Debug/` — 디버거
  - `LOD/` — LOD 시스템
- **Burst/Job System** 사용으로 100+ 캐릭터도 60fps 목표
- **테스트 씬**: `Test_01_Player`에서 바로 검증 가능