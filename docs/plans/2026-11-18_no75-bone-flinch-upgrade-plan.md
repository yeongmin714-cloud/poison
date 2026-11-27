# 계획서: 피격 본 반응 승격 (무번호 75 — 리그 상체 플린치)

**사용자 지시(2026-11-18)**: "진행하고 끝나면 설계도까지" — 공격·피격 고품질 레버 3종 검토 후 본 항목 착지 + VFX Graph 설계도 별도 산출.

**착지 기록(2026-11-18)**: FxHitFlinchBone.cs 신규(177행) + HitReactionDriver +8행 최소 통합 + P75 테스트 8건. 컴파일 error CS 0, EditMode 2556/2546/10 — baseline(qa_74) 실패집합 1:1, 신규 8/8 GREEN. 킥 슬로우 기각 확정(H1 중복). VFX Graph 설계도: docs/plans/2026-11-18_no76-vfx-graph-gpu-upgrade-design.md

---

## 중복 감사 (실측 — 기각 판정 포함)

- **몬스터 피격 플린치 transform 계층 = 이미 착지** — `HitReactionDriver.Apply`(Phase C): severity 3단계(Light 9°/Heavy 16°/Crit 22°, 0.18~0.34s) 시각 모델 전체 젖힘+넉백+애니 트리거. **본 반응이 없는 것이 아니라, transform(모델 전체) 계층까지만 구현돼 있음**.
- **강타격 시간 왜곡 = 이미 착지** — [37-H1] 강타격 프리즈(피해≥15, 0.05s@0.06 — PlayerCombat 572·1901행) + 패링 히트스톱(159-P3). **킥 슬로우(BulletTime 슬로우)는 시간 왜곡 제3계층 중복 → 기각**.
- HitReaction(KinematicJolt 절트·ScalePulse)·FxHitSilhouette·FxDistortionWave·FxDebrisSpawner·FxGroundCrackFX — 전부 착지. 재작성 금지.

## 본 항목 — 본(리그) 단위 플린치로 승격

**현재 한계**: HitReactionDriver가 시각 모델(자식) 트랜스폼 전체를 젖힘 — 전신이 판처럼 기움. 실제 리깅에서 피격은 **상체 본(Spine 체인)만 꺾이고 하체는 지면에 붙어 있어야** 자연스럽다(젤다/엘더스크롤 히트 반응의 기준).

**승격 설계**:
- 신규 `FxHitFlinchBone.cs` — static `Trigger(GameObject target, Vector3 hitDirection, HitSeverity severity)`:
  - 본 탐색: 자식 SkinnedMeshRenderer의 bones 중 상체 패턴 매치(Spine/Spine1/Spine2/Chest/Torso — 대소문자 무시, FxDeathRelax 본 패턴 매칭 선례). 하체(Hips 이하)는 건드리지 않음.
  - 반응: 맞은 방향 반대로 상체 후방 기움 — Spine 루트 본의 localRotation을 수평 법선 축 기준 severity 각도(카이드 루트 본 기준 Light 6°/Heavy 10°/Crit 15°, 0.28s)로 Slerp 후 원복(FXMotion.Progress 이징 — 28-U3 재구현 금지 규약).
  - 체인 적용: Spine1/Spine2 등 하위 척추 본은 루트 본 각도의 비율 배분(0.5/0.35 등 결정론 상수) — 전체 각도 합이 카이드 각도와 정합.
- **HitReactionDriver 통합 계약(대체+폴백)**: 본 매치 성공 시 **기존 transform lean을 스킵**(본 반응이 대체 — 이중 기움 방지), 본 0건(GLB 비리그 등)이면 **기존 transform lean 그대로**(폴백 — 현행 무손상). 넉백·경직·애니 트리거는 무관하게 유지.
- 사망 충돌 가드: 72-W2 FxDeathRelax는 팔/다리 본만 다룸(Spine 미매치) — 본 경합 없음 확인(실측: FxDeathRelax 본 패턴 Arm/Leg/Hand/Foot). driver의 inactive 가드가 이미 사망 처리중 스킵을 보장.
- 결정론: Random 0건. EditMode는 소스 계약만(코루틴 기동 없음 — driver의 activeInHierarchy 가드 준용).

## 파일

- **신규** `Assets/Scripts/Systems/FxHitFlinchBone.cs`
- **수정** `Assets/Scripts/Systems/HitReactionDriver.cs` — Apply 본문에 본 반응 분기 1블록(기존 lean 경로는 폴백으로 보존, 상수·시그니처 무손상)
- **신규 테스트** `Assets/Scripts/Tests/EditMode/FxBoneFlinchP75Tests.cs`

## 계약(잠금 무손상)

- HitSeverity enum·HitReactionDriver 기존 상수(Light/Heavy/Crit Duration·LeanDeg·Push) 무변경 — 테스트(FxQuality 계열) 문자열 카운트 보존
- HitReaction.cs 무접촉(KinematicJolt·ScalePulse·Stun 계약 유지)
- FxDeathRelax·AnimalAI 무접촉
- 73-E2 FxSwayRunner(오브젝트 스윙)와 대상 중복 없음(피격자=몬스터/병사, E2=채집 오브젝트)
- 결정론(Random 0건)·EditMode 무동작 가드

## 테스트 (P75 소스 계약)

- 마커/본 패턴 매칭/severity 각도 위계/FXMotion 재사용/transform lean 폴백 분기/Random 0건/HitReactionDriver 기존 상수 문자열 카운트 보존

## QA 게이트

- 컴파일 error CS 0 + 전수 EditMode **baseline=qa_74(2548/2538/10) 실패집합 1:1** + P75 GREEN
- 배치 background+폴링. Play·커밋 별도 승인.
