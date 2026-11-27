# 89차 스펙 — F키 마을 NPC 상호작용 + 플레이어 애니(idle/walk) NPC 부착 + 대화 정면 카메라 확대

> 발행: 감독자 (2026-10-16). 위임 근거: docs/AGENT_DELEGATION_POLICY.md.
> 이전 차수 미완(87차 F키 상호작용) 해소 + 유저 신규 요구 수용.

## 실측 현황 (갭)
1. **F키 스캔(PlayerMovement 592~659행)**: 병사(GuardPlaceholder) > 영주(InvadedLordEncounter) > NPC(TerritoryNPCBehaviour — 리플렉션) 순. **마을 주민(NPCAmbientDialogue)은 미감지** → 87차 F키 미완 원인.
2. **InteractionPanelUTK**: TargetKind.NPC 그리드=상태보기★/대화하기/선물주기/퀘스트. IsActionSupported "대화하기"는 GuardPlaceholder/InvadedLordEncounter/TerritoryNPCBehaviour만 true — **NPCAmbientDialogue 미지원(버튼 비활성)**. OnTalkRequested도 TerritoryNPCBehaviour만 — NPCAmbientDialogue는 "대화 미지원 대상" 로그.
3. **DialogueCameraDirector**: Begin(GuardPlaceholder) 전용 — _target/MeasureHeadY/ComputeSightlineDir가 GuardPlaceholder 타입. Target/DialogueStarted 소비처 0 실측(프로퍼티·이벤트 자유화 가능).
4. **주민 애니**: SoldierShield_AC(DoubleL: OneHand_Up_Idle/Run) — **플레이어 클립 아님**. 플레이어 Player_AC: Idle=MeshyUser/Idle_02, Walk=MeshyUser/Walking(YAML GUID 역색인 실측), 단일 Base 레이어, default=Idle, Speed 계약 **>0.55 Walk 진입/<0.35 Idle 복귀/>4.5 Run/<2 Walk 복귀**. TalkA/TalkB 상태 = 트리거 "Talk"/"Talk2", HasExitTime 0.92 → 자동 Idle 복귀(전이 dst=Idle 실측).
5. **VillagerWanderer(87차)**: Speed SetFloat(_walking?1f:0f) — Player_AC 계약(>0.55/<0.35)에 값 그대로 충족. 대화 정지 API 없음.
6. F키 감지 반경 _interactionRadius=2.5f, layers=Everything — 주민 CapsuleCollider(0.35/1.8) 존재.

## 구현

### 1) NPC 애니 = 플레이어 클립 부착 (idle+walk 2종)
- `VillageNpcSpawner.SpawnNpc`: Resources.Load → `"Animation/Controllers/Player_AC"` 교체(병사 AC 로드부 교체 + 주석 [89차]). 로드 실패 시 기존 SoldierShield_AC 폴백 유지(경고 로그).
- `VillagerWanderer`: Speed 계약 주석 갱신 — **Player_AC(>0.55 Walk 진입/<0.35 Idle 복귀)**, 값(_walking?1f:0f)은 계약 충족 유지.
- 대화 정지 API 신설: `PauseForDialogue()`(배회 정지+Speed 0 즉시) / `ResumeAfterDialogue()`(휴식 카운터 재시작) — VillagerWanderer.

### 2) DialogueCameraDirector 일반화 (영주/병사와 동일 정면 확대 — 주민 지원)
- `_target`: GuardPlaceholder → **Component**, `Target` 프로퍼티 → Component(소비처 0 실측 — 안전).
- `DialogueStarted/DialogueEnded`: Action<GuardPlaceholder> → **Action<Component>**(소비처 0 실측).
- `MeasureHeadY(Transform target)` / `ComputeSightlineDir(Transform target, Vector3 pos)` — 일반화(로직 무변경).
- 신규 오버로드: `Begin(Component target, string displayName)` — 기존 로직 전이(씬/저장/사선카메라/시선 회전/5s 타이머/입력 감시). 사망 감시(Update)는 `target as GuardPlaceholder`일 때만 IsDead, 그 외 파괴(null) 감시만.
- 기존 `Begin(GuardPlaceholder target)` 유지 → `Begin(target, target.GuardName)` 위임 — **InteractionPanelUTK 소스의 "DialogueCameraDirector.Begin(talkGuard)" 문자열 계약 유지**(DialogueFeedbackTests 어서션).
- 로그/주석에 [89차] 마커.

### 3) F키 마을 주민 감지 (PlayerMovement)
- P3 NPC 블록(TerritoryNPCBehaviour 리플렉션) **이후 추가 단계 P4**: `NPCAmbientDialogue` 최근접 감지(동일 Systems 어셈블리 — 직접 참조, 기존 hits 재사용) → `SoldierInteractBridge.RaiseTargetInteraction(HoverTargetClassifier.TargetKind.NPC, npc)` — 64차 P3 좌클릭 패널과 동일 kind.
- 우선순위 계약 유지: 병사 > 영주 > 마을NPC(TerritoryNPCBehaviour) > 주민(NPCAmbientDialogue). [89차] 마커.
- 실내(TestMovement) 기존 F스캔 변경 금지(주민 없음).

### 4) 대화하기 → NPC 대화 패널 + 카메라 확대 (InteractionPanelUTK + NPCDialoguePanelUTK)
⚠ InteractionPanelUTK.cs/NPCDialoguePanelUTK.cs는 sibling 동시수정 이력 다수 — **patch 전 재-read 필수**.
- `IsActionSupported "대화하기"`: += `target is NPCAmbientDialogue` → true.
- `OnTalkRequested`: NPCAmbientDialogue 분기 신설(CountyNPCBehaviour 분기 전) —
  1. `DialogueCameraDirector.Begin(npc, npc 표시명)` — 정면 카메라 확대(영주/병사와 동일 경험)
  2. `Instance?.Close()` — 패널 겹침 방지(기존 계약)
  3. npc 대화 자세 진입: `npc.BeginDialoguePose()`(신설 — VillagerWanderer.PauseForDialogue + Animator.SetTrigger("Talk") — 플레이어 드라이버 TriggerTalkA 동일 트리거명) + DialogueEnded 구독(구독 전 해제, 1회성)으로 `ResumeAfterDialogue()`
  4. `NPCDialoguePanelUTK.OpenForVillager(npc)`
- `NPCAmbientDialogue` 신설 API: `DisplayName`(NpcDialogueData.NpcName 우선, 폴백 gameObject.name), `GetInteractionLine()`(data.GetRandomInteractionDialogue() → null/데이터 부재 시 기본 문구 폴백 — "안녕하세요, 여행자님." 계열 고정 3종 중 결정론 선택. 위조 금지 표기: 데이터 미구성 폴백), `BeginDialoguePose()`/`EndDialoguePose()`.
- `NPCDialoguePanelUTK.OpenForVillager(NPCAmbientDialogue npc)`(신규 — OpenForGuard 패턴 준용):
  - 이름행 = DisplayName, 1행 = GetInteractionLine(), 2행 = "마을 주민 — 잡담" 보조행(대사 색 계약: 1행 NPC #EAEDEF/2행 보조 #8B949E 유지), 선택지 3개(안녕하세요./근처 소식 있어?/잘 지내?) 클릭 → 1행 응답 갱신(GetInteractionLine 재호출 or 응답 문구) + npc.SetTalkBTrigger()(Animator "Talk2") + PlaceAtAnchor(npc.transform).
  - 기존 Open(NPCInstance)/OpenForGuard 계약·시그니처 불변.

### 5) 테스트 (소스 문자열 어서션 — [89차] 마커 + Does.Contain, Or 체인 `.Or.Contain`)
- `VillageNpcFKeyTests`(신규):
  ① VillageNpcSpawner — "Animation/Controllers/Player_AC" 부착 + Soldier 폴백 유지
  ② VillagerWanderer — PauseForDialogue/ResumeAfterDialogue + "Player_AC" 계약 주석(>0.55/<0.35)
  ③ PlayerMovement — NPCAmbientDialogue 감지 + RaiseTargetInteraction(TargetKind.NPC + "NPCAmbientDialogue")
  ④ InteractionPanelUTK — IsActionSupported "is NPCAmbientDialogue" + OnTalkRequested 분기(Begin+OpenForVillager+Close)
  ⑤ NPCDialoguePanelUTK — OpenForVillager + PlaceAtAnchor + Talk2
  ⑥ DialogueCameraDirector — Begin(Component, string) 오버로드 + Begin(GuardPlaceholder) 유지(위임) + MeasureHeadY/ComputeSightlineDir Transform 시그니처
- DialogueFeedbackTests: 기존 계약 통과 확인(변경 시 [89차] 갱신 — 소비처 0 실측이라 이벤트 타입 변경 무영향 예상).

## 검증
- 컴파일: `rm -f Temp/UnityLockfile && timeout 550 bash compile_test.sh` → error CS 0.
- EditMode(QA): baseline 11 고정 실패 외 0, 88차 1175/1164/11 대비 신규/갱신 반영 총계 보고.
- Play 확인(사용자 게이트): ①주민 걷기 = 플레이어와 동일 idle/walk(눈으로 동일한 보행) ②주민 근접 F키 → 상호작용 패널(대화하기 활성) ③대화하기 → 카메라 정면 확대(영주/병사와 동일) + NPC 대화 패널(대상 앞 배치) + NPC Talk 몸동작 + 배회 정지 ④카메라 복원(5s/ESC/이동) 후 배회 재개 ⑤병사/영주 기존 F키 경로 회귀 없음.
