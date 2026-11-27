# 98차 계획 — 마을씬 테스트 9 미해결 3건 수리 (주민 애니 프리징 / 대화 카메라 조기 복원 / F키 프롬프트 미부착)

제정: 2026-10-17 / 근거: Screenshots/마을씬 테스트 9.mp4 (82.4s, 유효 세션 0~39s) + Editor.log 실측
상태: 스펙 확정 — 구현은 사용자 승인("수정해줘")으로 진행. Play·커밋은 별도 승인 게이트 유지.

---

## 1. 감독자 실측 증거 (프레임 추출 + Editor.log)

### 애니 (프리징)
- 영상 20~24s 2fps 960px 크롭 실측: 주민 전원 **포즈 완전 고정 + 화면 내 이동**(걷기 사이클 관찰 0).
- Editor.log 라인 2775: `[VillagerWanderer] 믹사모 클립 — walk='mixamo.com' idle='mixamo.com'`
  → 두 클립 모두 테이크명 'mixamo.com'이라 **로그만으로 객체 동일성 판별 불가**(동일 객체면 Walk 상태가 Idle 클립 재생 = 서있는 포즈+이동 증상과 정합).
- Editor.log 라인 5954~6145: 스트라이드 정합 **성공** 다수(girl1/man1/man2/girl2/girl3/oldman1/oldman2, 샘플 22~38, 실측 0.81~1.20m/s)
  → 캘리브레이션 윈도우 동안 발 본 이동 실측됨 = **애니 자체는 특정 구간 재생 실존**. 프리징은 상시가 아니라 **특정 시점(유력: 대화 정지→재개 사이클) 이후 발생**.
- Editor.log: `배회 재개` 로그 전무 / `[96차 F2] 닫힘 재개 위임 … EndDialoguePose 재개` 2건 존재
  → 재개가 **위임 경로(EndDialoguePose)**로 넘어갔는데 그 경로에는 재개 로그 자체가 없어 성패 판별 불가. 위임 시 `DisarmPauseWatchdog()`로 안전망까지 해제됨(소유권 이전) — 위임 수신처가 죽으면 **영구 정지 + 안전망 부재**.
- 97차 F1(loopTime=1)은 .meta 실측상 정상 반영(Walking/Idle 둘 다 loopTime: 1) — 루프 자체는 해소. 잔여 프리징은 별개 원인.

### 카메라 (대화 중 조기 복원)
- 영상 29~35s: Villager_8 대화 클로즈업 정상 → **36s 대화창이 화면에 남아 있는 상태로 카메라만 이탈**.
- Editor.log: 대화 2건 모두 `대화 카메라 시작(fullBody=True)` 후 수 초 내 `End()`(DialogueCameraDirector.cs:381 = `!IsDialoguePanelOpen()` 분기) → 복원.
- 패널은 NPCDialoguePanelUTK.OpenForVillager로 실제 개방됨(`[TargetProject] target=NPCDialogueAnchorProxy` 투영 로그 실존) → **IsOpen 폴링 결과와 실제 표시 상태 불일치**. 원인 불명(진단 로그 부재).
- End는 `DialogueEnded` 이벤트 발화(325라인) → EndDialoguePose → ResumeAfterDialogue 체인은 존재. 단 BeginDialoguePose 미호출/구독 해제 상태면 체인 단절.

### 프롬프트 (F키 미부착)
- `[97차 F3] 부착 검증 — 경로=정면앵커 npc screenPx=(571,318) panel worldBound=(0,0,NaNxNaN) 배치=(573,471)`
  → worldBound NaN → 중앙 정렬 산출 불가 → NPC(screenPx 571,318)가 아닌 (573,471)에 배치(약 150px 하방 이탈).
- `[97차 F3] 부착 검증 — 경로=폴백 npc screenPx=투영실패(z<0) … 배치=(630,760)` 다수
  → 정면앵커 프록시가 카메라 뒤(z<0)로 판정 → 하단 고정 배치. 대화 클로즈업 카메라가 NPC 정면 근접 시 프록시가 프러스텀 밖으로 나가는 오판 유력.

---

## 2. 수리 스펙

### F1 — 주민 애니 프리징 근본 수리 (최우선)
파일: `Assets/Scripts/Systems/VillageNpcSpawner.cs`, `Assets/Scripts/Systems/VillagerWanderer.cs`
- F1-1 객체 동일성 강제 검증: ResolveMixamoOverrideController에서 `_mixamoWalk == _mixamoIdle`(참조 동일)이면 폴더 LoadAll 결과를 폐기하고 Walking/Idle **파일별 LoadAll 재해석**으로 확정. 해석 로그에 `instanceID`, `isLooping`, `length` 포함(walk/idle 서로 다른 객체 + isLooping=true 실측).
- F1-2 런타임 애니 진단(프리징 판별 데이터): VillagerWanderer에 1Hz 진단(스폰 후 30s 한정 또는 상시 저비용) — `animator.enabled / speed / 현재 상태명·normalizedTime / Speed 파라미터 / active clip name·isLooping`. 로그 태그 `[98차 F1] 애니 진단`.
- F1-3 프리징 자가수리: 걷기 진입 후 1.5s 셀프체크 — 발 본 Δ≈0 + 루트 이동 지속 시 `animator.Rebind()` + 오버라이드 컨트롤러 재적용 + Speed 파라미터 재설정(91차 Rebind 경로 확장). 수리 로그 1회(`[98차 F1] 프리징 자가수리 — Rebind`).
- F1-4 재개 봉합(위임 경로 가시화+폴백):
  - `NPCAmbientDialogue.EndDialoguePose`에 재개 로그 추가(`[98차 F1] 대화 종료 재개 — {이름}`).
  - 카메라 End가 DialogueEnded를 발화해도 구독자(OnDialogueEndedForPose)가 없으면 재개 누락 → `DialogueCameraDirector.End` 경로에서 대상이 NPCAmbientDialogue이고 `VillagerWanderer._paused` 잔존 시 직접 ResumeAfterDialogue 폴백(구독 유무 무관 안전).
  - 위임 경로에서도 15s 워치독 유지(소유권 이전해도 안전망 이중화 — DisarmPauseWatchdog 제거 대신 위임 대상 감시 유지).

### F2 — 대화 카메라 조기 복원 수리
파일: `Assets/Scripts/UI/Toolkit/NPCDialoguePanelUTK.cs`, `Assets/Scripts/Systems/DialogueCameraDirector.cs`
- F2-1 UI 측 폴링 강화(단일소스 계약 유지 — 위임 람다 내부에서 실측): `DialoguePanelOpenOverride` 람다를 `_instance != null && (_instance.IsOpen || 실측표시)`로 확장. 실측표시 = parent 부착 + `resolvedStyle.display != DisplayStyle.None`(IsOpen 플래그와 UI 실측 불일치 방어).
- F2-2 End 분기 진단 로그(1회/세션): End 진입 시 원인 상세 — `override=null 여부 / _instance 존재 / IsOpen / resolvedStyle.display / parent 부첞性 / Time.time`. 태그 `[98차 F2] 카메라 End 원인`. 다음 Play에서 원인 확정 데이터 확보.
- F2-3 병사/영주 경로(73차 5s 타이머 계약) 무변경.

### F3 — F키 프롬프트 대상 부착 수리
파일: `Assets/Scripts/UI/Toolkit/InteractionPanelUTK.cs`
- F3-1 배치 산출 worldBound 비의존화: 정면앵커 경로 배치를 npc screenPx 기준 직접 산출(패널 폭/높이는 resolvedStyle 또는 이전 실측값 사용, NaN이면 기본 크기 폴백) — worldBound NaN 시 중앙 폴백 배치 금지.
- F3-2 후배치 보정 유지: GeometryChangedEvent/다음 프레임 재배치에서 실측 크기로 중앙 정렬 재교정(기존 97차 스케줄러 계승 — NaN이면 스킵하지 않고 기본 크기 사용).
- F3-3 폴백 오판 수리: 투영 대상을 정면앵커 프록시 → NPC 본체(머리/bounds 중심)로 변경. z<0이어도 **하단 고정 폐기** — 화면 클램프 후 NPC 최근접 가장자리 배치. 폴백 배치(630,760) 상수 경로 제거.
- F3-4 부착 검증 로그 유지(판정 기준: 배치 후 npc screenPx와 패널 중심 거리 ≤ 120px = 부착 성공).

---

## 3. 검증 (QA 서브에이전트)
- 컴파일 error CS 0.
- EditMode 전수: baseline 1294/1283/11 1:1 대조 — 회귀 0.
- 신규 테스트(최소): ① F1-1 walk≠idle 객체 동일성 + isLooping(EditMode — MixamoClipLoopTests 확장) ② F2-1 폴링 폴백 실측(IsOpen=false + display=Flex → 열림 판정) ③ F3-3 z<0 클램프 배치(하단 고정 폐기 회귀 핀).
- Play 확인은 사용자 승인 후 별도(승인 게이트).

## 4. 위임
- 구현 서브에이전트: §2 전체 + 컴파일 확인 → diff/경로 제출.
- QA 서브에이전트: §3 실행 → 증거 로그 제출.
- 감독자: 증거 대조 검증 후 보고. Play는 별도 승인.
