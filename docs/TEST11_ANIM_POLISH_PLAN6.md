# 🎬 P-ANIM6 상세 구현 계획 — 2족 회전 보행 전환 + 모니터 재설계 (2026-09-22)

> **원인 확정**: JobTempAlloc 누수 = 2족 컨트롤러(ProceduralAnimationController)의 프레임당
> 커스텀 잡 5종(footPlanner/hipShift/spineCounter/leftIK/rightIK, 912~1042행) temp 할당 미해제.
> 2족 보행의 잡 체인이 곧 구동부라 제거만으론 안 됨 → **회전 기반 보행으로 전환**하며 잡 제거.
> **구조**: 4단계, 각 단계 커밋 분리. 플레이어 무영향이 최우선 제약.

---

## Phase 1 — 2족 회전 기반 보행 전환 (핵심, ProceduralAnimationController.cs)

### Step 1.1 — 회전 보행 코어 (4족 ApplyRotationGait 패턴 이식)
- 신규 메서드 `ApplyBipedRotationGait()`:
  - **다리**: L_Hip/R_Hip에 전후 스윙(±sin, 위상 좌=0/우=0.5 교차), **무릎**(L_Knee/R_Knee)에
    굽힘 성분(스윙 전반부만 굽힘 — 발 들기 흉내) — 곱셈 합성
  - **팔**: L_Shoulder/R_Shoulder에 다리와 **역위상** 스윙(자연스러운 팔 흔들기), 진폭은 다리의 40%
  - 축 불변: 4족과 동일하게 **월드 기준 회전 + 기준 localRotation 캐시**(Dictionary<Transform,Quaternion>)
  - 속도 동기: 위상 진행 = 기존 phaseSpeed 변수 재사용(속도/스텝길이) → 발 미끄러짐 방지 유지
  - 스윙 각도 = 실속도 비례 클램프(4족 수리와 동일)
- 배치 본: 토폴로지 매핑 결과 그대로 소비(L_Hip/Knee, L_Shoulder/Elbow 등 — 매핑은 완성 상태)

### Step 1.2 — 잡 체인 게이트 (누수 원천 차단)
- 신규 필드 `[SerializeField] bool _useJobIK = false`
- UpdateLocomotion 내 잡 스케줄 블록(footPlanner 912 / hipShift 933 / spineCounter 951 /
  leftIK 999 / rightIK 1042)을 `if (_useJobIK)`로 감쌈 — 잡 미스케줄 = JobTempAlloc 0건
- ⚠️ **플레이어 보호 조건**: `_animator.isHuman == true`면(휴머노이드 아바타) 잡 경로 유지
  (플레이어 걷기 무영향) / 익명 리그(isHuman=false, 미노타우르스)만 회전 보행. → 게이트 조건:
  `if (_useJobIK && _animator.isHuman)`
- NativeArray 할당/Dispose 로직은 유지(다른 곳 참조 가능) — 잡 미스케줄 시 미사용일 뿐

### Step 1.3 — 목표 계산 게이트
- 회전 보행 모드에서는 발/손 목표 계산(UpdateFootTargets류)·적용(SetIKPosition/ApplyFootIK) 생략 —
  SetIKPosition은 익명 아바타에서 원래 무효(사실상 죽은 경로)라 제거해도 시각 변화 없음 확인 필요

### Step 1.4 — 검증
- 컴파일 CS=0 + EditMode / Play: JobTempAlloc 경고 0건 / 미노타우르스 걷기(다리 교차) / 팔 자연 스윙
- **회귀 점검**: 플레이어 걷기 변화 없음(잡 경로 유지) 확인

---

## Phase 2 — ShowcaseMonitor 재설계 (진단 신뢰성)

### Step 2.1 — 구동 본 관측
- `Setup(label, family, Transform[] watchBones)` 시그니처 확장 — 셋업이
  `assigner.GetComponent`→boneMap에서 **L_Hip/R_Hip/L_HindHip/R_HindHip/Spine0** 추출해 전달
- 관측 = 이 본들의 localRotation Δ (하나라도 변하면 애니 정상)

### Step 2.2 — 스티키 폴백 해제
- 폴백 활성 중에도 3초마다 재검증 → 변화 감지 시 폴백 자동 해제 + `[ShowcaseMonitor] 폴백 해제` 로그
- 폴백 발동/해제 각 1회 로그(스팸 없음)

### Step 2.3 — Special(slime) 관측 교정
- 스케일 관측 대상을 SMR transform이 아닌 **Root 본**(펄스 실제 대상)으로 교체

### Step 2.4 — 검증
- 정상 구동 몬스터 폴백 경고 0건 / 진짜 고장만 경고 / 폴백 해제 로그 확인

---

## Phase 3 — 보행 튜닝 라운드 (반복 구조)
- 라운드 = [수리 → 에디터 Play → 영상 → 타일시트(연속 프레임) 분석 → 판정]
- 튜닝 파라미터(인스펙터 노출): 다리 스윙 각(속도 비례 클램프), 무릎 굽힘량, 팔 스윙량,
  골반 바운스 진폭, 척추 물결 진폭
- 판정 기준: 미끄러짐 0 / 부유 0 / 역꺾임 0 / 리듬 규칙적 / 무게감

## Phase 4 — 검증 + 정리
- 전 몬스터/병사/NPC 종합 판정, JobTempAlloc 0건, 커밋/푸시/QAPROGRESS/ROADMAP/메모리/텔레그램

## 리스크 & 대응
| 리스크 | 대응 |
|:--|:--|
| 잡 게이트로 2족 다른 기능(공격 애니) 영향 | Action 코드는 본 직접 회전(잡 불의존) — Phase 1 검증 시 확인 |
| 플레이어 걷기 변화 | isHuman 게이트로 잡 경로 유지 — 회귀 점검 필수 |
| Root 바운스와 다리 회전 경합 재발 | 바운스=Root(상위), 스윙=다리 체인 루트 — 별개 본 확인 후 진행 |
| 매핑이 팔/다리를 오배치한 리그 | Phase 1 검증에서 배치 로그(4족 배치/토폴로지)로 확인 → 오배치 시 필터 튜닝 |

## 라운드 운영 규칙
- 각 Phase 커밋 분리 · CS=0 + EditMode · 한 라운드 미흡 시 라운드 반복 · 남은 결함 즉시 보고
- 완료 시 QAPROGRESS/ROADMAP/메모리/텔레그램 4곳 기록
