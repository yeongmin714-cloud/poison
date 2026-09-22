# 🎬 P-ANIM7 상세 구현 계획 — 4족 회전 보행 접선 + 절차 idle 전 계열 (2026-09-22)

> **기반**: 애니메이션 테스트 13 영상(12.4s) 타일시트 분석 + Editor.log 실측.
> **성공 확인**: 미노타우르스 걷기 정상(사용자 판정+영상 4.5~5.5s 다리 교차/팔 스윙 확인) · 정지 시 포즈 복원(10.5s) ·
> 폴백 경고 0건 · JobTempAlloc **0건** — P-ANIM6 Phase1~2 유효.
> **남은 결함(사용자 보고 "나머지 몬스터 제대로 안 됨")의 코드 레벨 원인 확정 — 아래 4건.

---

## 진단 (영상 13 + 로그 교차로 확정한 뿌리)

| # | 증상 | 뿌리 (코드 실측) |
|:--|:---|:---|
| 1 | **4족(rabbit/swamp_croc/griffin/manticore) 다리가 뻗은 채 경직/미끄러짐** | `ApplyRotationGait()`(QuadrupedProceduralAnimation.cs:522)가 **호출부 0건의 사장 코드** — 실제 다리 구동은 여전히 FABRIK IK(`ApplyFootIK`→`Solve`)가 foot target(Locomotion.UpdateGaitTargets)을 향해 풀리는 구식 경로. 속도 피드가 흔들리면 과신전/비틀림/미끄러짐. 본 회전 스윙은 아예 안 나감 |
| 2 | **정지 시 전 몬스터 바인드 포즈로 얼어붙음(T포즈 조각상)** | 절차 idle 부재 — 4족은 speed≈0 → phase 진행 0 + IK 타겟 정지 = 조각상(영상: 그리핀 7s 이후 완전 고정). 2족도 P-ANIM6 RestoreGaitBase가 바인드 포즈로 복원(idle 미구현). ShowcaseMonitor는 "정지 중 무변화=정상"이라 미감지 |
| 3 | **날개(griffin/manticore) 경직** | 4족 경로에 날개 본(L_Shoulder/R_Shoulder) 구동 경로 없음 — 펼친 채 고정(영상 2.5s/7.5s 실측) |
| 4 | **슬라임 이동=미끄러짐** | 펄스는 작동(영상 4.5~5.5s 스쿼시 확인)하나 이동은 슬라이딩 — 홉 바운스 없음 |

> ⚠️ P-ANIM5-B에서 "4족 회전 전환 완료"로 기록됐으나 실제 호출부가 존재하지 않았음(49차 리팩터에서 유실 추정) — 본 계획으로 재접선.

---

## Phase 1 — 4족 회전 기반 보행 실제 접선 (핵심, QuadrupedProceduralAnimation.cs)

### Step 1.1 — 구동 경로 스위치
- `ApplyProceduralPose()`에서 `if (_rotationGait && _currentSpeed > 0.1f) ApplyRotationGait(); else ApplyFootIK();` —
  IK와 회전 스윙의 이중 구동(경합) 제거. 2족 P-ANIM6 게이트와 동일 패턴.
- 회전 모드에서 IK 타겟 계산(Locomotion.UpdateGaitTargets)은 유지하되 적용만 생략(다른 참조 가능성 보존).

### Step 1.2 — 다리 체인 굽힘 (2족 SwingBipedLeg 이식)
- 힙 스윙(±sin)에 더해 **무릎/발목** 굽힘 성분: 스윙 전반부만 굽힘(`max(0,-cos(2πφ))`) — 발 들기.
- 캐시: `_gaitBaseRot` Dictionary 별도 필드(2족과 분리 — 파일 다름).
- 속도 비례 클램프 유지(Clamp(speed×6, 10, 32)) + stride 동기(기존 UpdateLegPhases 위상 재사용).

### Step 1.3 — 정지 복원 + idle 게이트
- speed ≤ 0.1 → base 복원(캐시 클리어, 2족 RestoreGaitBase 동일) → Phase 2 idle로 인수.
- 액션/사망 중 스윙 스킵(AnimalAI 액션 본 회전과 경합 방지).

### Step 1.4 — 검증
- 컴파일 CS=0 + EditMode / 4족 걷기(다리 교차+무릎 굽힘) / 정지 시 포즈 복원 / 미노타우르스 회귀 없음.

## Phase 2 — 절차 idle 전 계열 (정지 시 조각상 해소)

### Step 2.1 — 4족 idle (QuadrupedProceduralAnimation)
- 정지(speed≤0.1) 시: 호흡(척추 Spine0 미세 롤/피치 sin, ±1.5°@1.1Hz) + 꼬리 끝 본 좌우 완만 스윙 +
  머리 하이룩 유지. base 캐시 재사용 — 보행 재개 시 자동 인수인계.
### Step 2.2 — 2족 idle (ProceduralAnimationController)
- RestoreGaitBase 대신: 정지 시 어깨 호흡(±2°)+골반 미세 체중 이동(±0.5°) — 바인드 포즈 대체.
### Step 2.3 — 슬라임 이동 홉 (SpecialCreatureAnimator)
- 이동 중: 스쿼시 스트레치 + 수직 홉 바운스(y 싱 — _bodyIsSelf 가드 유지). 정지 중 기존 펄스 유지.
### Step 2.4 — 검증
- 정지 3초 시점 스크린샷: 전 몬스터 미세 호흡 확인 / ShowcaseMonitor 폴백 오탐 0건(정지 중 무변화=정상 규약 유지).

## Phase 3 — 날개 + 종별 미세 튜닝
- griffin/manticore: 날개 플랩(L_Shoulder/R_Shoulder sin 주기, 이동 중 강·정지 중 약) — 축/위상은 다리와 독립.
- 종별 stride 주기: rabbit 빠른 소보(stepLength↓), croc 저속 장보, gallop 강제 해제(속도 구간별 Walk/Trot/Gallop 재확인).
- 파라미터 인스펙터 노출(날개 진폭/호흡 진폭/꼬리 스윙).

## Phase 4 — 라운드 검증 + 정리
- 라운드 = [수리 → 에디터 Play → 영상 → 타일시트 분석 → 판정] 반복 (P-ANIM6 동일 운영).
- 합격 기준: 4족 다리 교차 보행(미끄러짐/부유 0) / 정지 시 idle(조각상 0) / 날개 플랩 / 슬라임 홉 /
  미노타우르스 회귀 없음 / JobTempAlloc 0건 / 폴백 오탐 0건.
- 완료 시 QAPROGRESS/ROADMAP/메모리/텔레그램 4곳 기록 + Phase별 커밋 분리.

## 리스크 & 대응
| 리스크 | 대응 |
|:--|:--|
| Locomotion.UpdateGaitTargets가 계산한 foot target과 회전 스윙 경합 | Step 1.1 게이트로 IK 적용 자체를 생략 — 타겟 계산은 유지(참조 보존) |
| 4족 무릎/발목 굽힘 부호가 리그마다 반대 | Phase 4 라운드에서 영상 판정 → 굽힘 부호/진폭 파라미터 튜닝 |
| idle이 ShowcaseMonitor 무변화 오타임 유발 | idle은 관측 본(워처=구동 본)을 흔들어 Δ>0.05 → 오탐 없음(오히려 정상화 판정 강화) |
| 슬라임 홉이 ShowcaseWanderDriver 위치 이동과 경합 | y만 홉(wander는 수평 이동 소유) — 규약 분리 동일 |
| 미노타우르스 회귀 | 2족 idle은 RestoreGaitBase 자리를 대체할 뿐, 게이트/걷기 로직 무변경 |

## 라운드 운영 규칙
- 각 Phase 커밋 분리 · CS=0 + EditMode · 한 라운드 미흡 시 라운드 반복 · 남은 결함 즉시 보고
- 완료 시 QAPROGRESS/ROADMAP/메모리/텔레그램 4곳 기록