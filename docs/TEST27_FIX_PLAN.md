# 📋 TEST27 수정 계획 — 68차 (2026-09-16, 3차 라운드)

> **입력**: 테스트 25 영상(프레임 실측) + Editor.log(67차 진단 로그) + 사용자 리포트.
> **실측 요약**: ①장비 가시성은 해결(워치독 동작 ✓) — 그러나 부품이 **1.1~1.4m**(헬멧 1.37m/부츠 1.36m)로 플레이어(~1.7m)를 덮고 본에서 ~0.5m 공중 부양 ②병사 데미지는 적용됨(로그) — 그러나 **몬스터 HP바가 화면에 안 보임** + 병사 사망 체인 부재 ③화살은 **스폰 3회 성공**(활 발사 성공 로그) — 그러나 **발사체에 메쉬가 없어 안 보임** + 활 걷기가 일반 걷기로 폴백(67차 전이의 복귀 경로 부재) ④GSM이 생성됐는데 **[RTS] 좌클릭 감지 로그 0건** — Update 자체가 죽음(공유 GO 파괴 추정, ArmorVisual 66차와 동일 패턴).

---

## Phase A — 장비 크기/장착감 정규화 (ArmorVisualAttachSystem)

**뿌리 (67차 세션 ✅ 로그 실측)**: 부착은 성공했으나 GLB 원본 스케일이 플레이어 대비 3~4배 — Helmet bounds (1.15,1.03,1.37) / Boots (1.12,1.36,1.37) / Gloves (1.18,0.71,1.19) / 본↔중심 0.44~0.51m(공중 부양).
**수리**:
1. **타겟 크기 정규화**: 슬롯별 목표 최대 치수 정의(Helmet 0.34m / Armor 0.62m / Shoes 0.36m / Gloves 0.24m / Back(방패) 0.85m) — 부착 직후 렌더러 bounds 실측 → 가장 긴 축이 목표 초과 시 `Scale = 목표/실측` 균등 축소(확대도 허용, 하한 0.5 방지).
2. **본 스냅 재정렬**: 스케일 후 비주얼 bounds 중심을 부착 본 원점에 맞추고 슬롯별 미세 오프셋 적용(Helmet +0.06 위 / Shoes 발끝 -0.04 / Back 손 앞 0.08). "본↔중심" 오차 로그로 0.1m 이내 검증.
3. **id별 오버라이드 테이블**(wood_* 우선): 목표 크기/오프셋을 id 단위로 조정 가능한 구조 — Play 로그로 즉시 튜닝.
4. 기존 워치독/동기 즉시 부착 유지.

## Phase B — 병사↔몬스터 킬 체인 + 병사 HP바/레벨

**실측**: 병사 데미지 정상(슬라임 HP=7.7/35, 20.27/35 로그)·병사 TakeDamage/Die() 구현 존재. 그러나 ①몬스터 HP바가 실화면에 안 보임(영상 실측 — MonsterHeadUI 렌더 확인 필요) ②몬스터 공격 대상이 **플레이어뿐**(AnimalAI L755-800 — 병사 타겟 없음) ③병사 사망 시 시각 처리 부재 ④병사에게 HP바/레벨 표시 없음.
**수리**:
1. **`GuardHeadUI` 신규**(MonsterHeadUI 패턴 — IMGUI 헤드 UI): 병사 머리 위 `이름 + Lv.N + HP바(녹/노랑/빨강)` 표시. GuardPlaceholder.CurrentHP/MaxHP/GuardName/Level 직접 폴링. 생성은 CreateGuard에서 AddComponent.
2. **몬스터 공격 대상 확장**: AnimalAI의 근접 공격/스킬 대상 선정을 "플레이어 + 사정거리 내 GuardPlaceholder" 중 가장 가까운 대상으로 확장 — 병사가 어그로를 끌면 몬스터가 병사를 때리고, 병사 HP 0 → Die() (사망 연출: 쓰러짐/페이드 후 파괴 + 로그).
3. **몬스터 HP바 가시화 검증**: MonsterHeadUI가 슬라임에 붙어 그리는지(OnGUI 조건 — 화면 내/거리) 확인 후, 안 보이면 표시 조건 수정 + HP 감소가 바에 즉시 반영되는지 폴링 확인. (병사 데미지 숫자는 67차에 추가됨 — 이번 세션 예외 0건 확인.)
4. **밸런스 게이트**: 병사 dmg 15(=Level×1.5) / 슬라임 HP 35 → 3타 킬. 병사 HP 10 / 슬라임 공격 5 → 2타 사망 — 너무 빠르면 병사 _maxHP 10→25 조정(로그 근거 후).

## Phase C — 활: 발사체 가시화 + 활 걷기 복귀 전이

**실측**: 화살 스폰 3회 성공(활 발사 성공 로그) — 그러나 ArrowProjectile = **콜라이더+리깃바디+옅은 트레일만, 메쉬 없음**(코드 전수) → 날아가는 화살이 안 보임. 활 걷기: 67차가 추가한 `BowAimedF→Idle(Speed<0.2)` 이후 **Idle→일반 Walk로 빠져 복귀 경로가 없어** 활 걷기가 사라짐(사용자 실측 "그것조차 없음").
**수리**:
1. **화살 가시 메쉬**: Spawn 시 시각 바디 구성 — 얇은 샤프트(Capsule 0.02×0.55, URP Lit 갈색) + 촉(원뿔, 회색) + 깃털(작은 박스 2개) 프리팹을 코드 생성으로 부착 + 트레일 강화(width 0.08, startColor=trailColor, Additive) + 진행 방향 정렬(Rigidbody rotation/ LookRotation). (화살 GLB가 Resources에 있으면 우선 사용 — 있으면 교체.)
2. **활 걷기 복귀 전이**(컨트롤러 YAML 추가만): ① `Idle → BowAimedF`(IsBow=1 && Speed>0.55) ② `Walk → BowAimedF`(IsBow=1) — 서면 Idle(67차), 걸면 활 걷기 복귀. 기존 상태/파라미터 삭제 없음.
3. 활 idle/run/strafe 클립은 **사용자가 Meshy로 후속 생성** — 파일 도착 시 동일 패턴으로 배선(별도 항목).

## Phase D — GuardSelectionManager 자가 치유 + 파괴자 추적

**실측**: GSM 생성 로그 ✓ → **[RTS] 좌클릭 감지 로그 0건**(무조건 로그인데도) = Update 실행 중단. ArmorVisual 66차와 동일 패턴(공유 GameManager GO 파괴 추정) — [GroundWatch] "Ground_Inner가 씬에서 사라짐" 경고도 양 세션 동반.
**수리**:
1. **GSM을 자체 GO로 분리**: EnsureGameManager에서 `new GameObject("GuardSelectionManager")` + DontDestroyOnLoad — 공유 GO 파괴와 무관.
2. **셀프힐 워치독**(ArmorVisualSyncWatchdog 패턴): 1s 주기로 GSM.Instance 부재 → 재생성. `public static GuardSelectionManager Instance` 추가.
3. **파괴자 추적**: GameManager.OnDestroy에 스택트레이스 포함 로그 추가("누가 나를 파괴했나" — 다음 Play에서 파괴 경로 확정) + GroundWatch 경고 원인 확인.
4. **독립 클릭 프로브**: PlayerCombat 좌클릭 처리부에 1회성 `[ClickProbe]` 로그(30초 쿨다운) — GSM이 죽어도 클릭 입력 자체는 증거 남김.
5. 우클릭 명령(RTSCommandSystem)도 동일 GO 분리 적용.

## Phase E — (보류) 활 idle/run/strafe 클립 배선

사용자가 Meshy로 생성 후 `Assets/Animations/MeshyUser/`에 넣으면 배선: `Idle_Holding_Bow`(BowAimedF 모션 교체 or 신규 상태) / `Run_Forward_with_Bow_Aimed`(Speed>3 분기) / `Strafe_Left/Right_with_Bow`(MoveX 분기). 도착 즉시 실행.

## 실행 순서
A(장비 정규화) → C(활: 발사체 가시+복귀 전이 — 사용자 체감 1순위) → B(킬 체인+HP바) → D(GSM 자가치유) → 배치컴파일 error CS=0 → ROADMAP/QAPROGRESS/커밋.

## 검증 (Play 판정)
① 장비가 몸 크기에 맞게 부착(헬멧은 머리에, 부츠는 발에 — 본↔중심 ≤0.1m) ② 병사 타격 → 몬스터 HP바 하락+골드 숫자+사망 ③ 몬스터가 병사 공격 → 병사 HP바 하락+사망 ④ 화살이 눈에 보이며 날아가 적중 ⑤ 활 들고 걸으면 활 걷기 애니 복귀 ⑥ `[RTS] 좌클릭 감지` 로그 존재 + Ctrl+드래그 선택 ⑦ GameManager 파괴자 로그 확보
