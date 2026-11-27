# 침략 영지 영주실·상호작용·전투 구현 계획

## 확정 요청/규칙
- 적 영주 영지 내부에서 최북단 방(현재 공용 토폴로지상 Bedroom)을 영주실로 지정하고 영주 NPC를 둔다.
- 침략 진입 중 영주 상호작용 패널: `대화하기`, `공격하기`.
- 대화: 별도 대화 패널에 `처형하기`, `추궁하기`, `풀어주기`.
- 공격: 영주를 실제로 타격·피격 가능한 적으로 만든다.
- 사용자 확정 선택 결과: 처형/풀어주기는 기존 `LordSurrenderSystem.ExecuteLord` / `SpareLord` 의미 그대로(소유권 이전/몰수 등 기존 부작용 포함). 추궁은 기존 RevengeListManager.Interrogate를 호출하지 않고 별도 서사 응답만 표시하며 영지 소유권·복수명부를 바꾸지 않는다. 영주가 쓰러지기 전에 처형/풀어주기 선택은 무효 안내. 전투 패배는 자동 점령 처리하지 않는다. 기존 처형/풀어주기 선택을 통해서만 기존 resolution API를 호출.

## 소스 조사 사실
- 침략 영지 진입: `BuildingTrigger` → `BuildingEvents` territoryKey → `UI/IndoorSceneTransition.OnIndoorSceneLoaded`; 모든 성은 `PlayerCastleInteriorBuilder` 공유 빌더, 적 성이면 `TerritoryBuilder.SpawnInteriorDefenseGuards` 호출.
- 북쪽은 +Z, Bedroom 존(`|x|<18 && z>14`); 방은 z=14 seam으로 Lobby에서 접근. 플레이어 위치 원점은 Lobby. 북쪽 침실 기존 침대/가구 및 적 병력 수가 존과 겹칠 수 있으므로 스폰 여유 좌표/적 군집 겹침을 검토한다.
- `InteractionPanelUTK` 이미 `Lord` target kind를 지원하지만 현재 lord action은 대화/음식/선물/동맹이고 대화는 `TerritoryNPCBehaviour`만 지원되어 Lord 대상 버튼 disable된다. `ContextCommandRouter`는 plain 좌클릭으로 Lord interaction panel을 연다.
- `PlayerCombat.IsPointerOverNpcInteraction`는 Lord 포인터 공격을 항상 막으므로 공격 시작 후 전투 타격을 허용하는 gate/state가 필요.
- 기존 비-Dracula Lord Placeholder는 Collider/IDamageable/AI 없음. DraculaLord는 강한 특수 AI이므로 그대로 일반화 금지.
- `LordSurrenderSystem.TrySummonLord`는 자동 surrender/final-state를 호출하므로 침공 입장용으로 사용 금지. `ExecuteLord` / `SpareLord` 기존 후속 효과는 사용자 확인으로 승인됨. 비-Dracula encounter 초기화/신분·대상 저장 seam을 안전히 추가해야 한다.
- 워킹트리 광범위 dirty, 특히 `PlayerCastleInteriorBuilder.cs`, `IndoorWarTestSetup.cs`, `TerritoryBuilder.cs`, `PlayerCombat.cs`, `GuardPlaceholder.cs`, `InteractionPanelUTK.cs` 변경을 보호한다.

## 실행 Phase 및 scope

### Phase A — 영주실 앵커 + 침략 Lord encounter 모델
화이트리스트(필요한 최소만):
- `Assets/Scripts/UI/IndoorSceneTransition.cs`
- `Assets/Scripts/Systems/PlayerCastleInteriorBuilder.cs` (더티, baseline diff 보호) 또는 한정된 post-build helper
- `Assets/Scripts/Systems/LordSurrenderSystem.cs` (기존 resolution registry 안전 초기화 seam 필요할 때만)
- 신규 `Assets/Scripts/Systems/InvadedLordEncounter.cs` + `.meta`
- `Assets/Scripts/Tests/EditMode/InvadedLordEncounterTests.cs` + `.meta`

수용 기준:
- castle/LordOwned 침략 진입에서만 Encounter 생성, 해당 실제 territory id+lord name이 설정됨. PlayerOwned enter에는 적 영주 생성/영주실 개조 없음.
- 북쪽 Bedroom에 실제 충돌체·이름표 가진 영주 배치. 침략 성 북쪽 방 표지판 영주실. 지역 원점 기준 최북단 +Z 안쪽에 배치되고 플레이어 Lobby/수비병과 비겹침.
- Encounter는 일반 영주 이름/HP/IDamageable와 독립된 `NotStarted/InCombat/Defeated/Resolved` state machine (필요 최소 states). 기본 공격 동작 없음; Attack 버튼 선택만 전투 시작.
- Spawn Encounter must register corresponding existing LordSurrenderSystem data *unsurrendered* so post-combat ExecuteLord/SpareLord work without calling TrySummonLord. Keep one-shot resolution and cleanup safe.

### Phase B — 영주 상호작용 + UTK 대화 및 세 선택
화이트리스트:
- `Assets/Scripts/UI/Toolkit/InteractionPanelUTK.cs` (dirty? 확인 후 hunk만)
- 신규 `Assets/Scripts/UI/Toolkit/InvadedLordDialogueUTK.cs` (+meta), or very narrow proven reuse of `LordAudienceUTK` (existing but no caller, already supports callback-based choices)
- `Assets/Scripts/Systems/InvadedLordEncounter.cs`
- `Assets/Scripts/Tests/EditMode/InvadedLordInteractionTests.cs` or focused existing test file

수용 기준:
- encounter component = Lord classifier; Lord panel only this NPC shows exactly Talk/Attack, other generic Lord panel stays unchanged.
- Talk opens UI Toolkit panel (no new IMGUI) with exactly 3 enabled choices. `추궁하기` gives separate scripted encounter text; no RevengeList mutation. Before Defeated, Execute/Release choices return a polite no-side-effect message. After Defeated: execute invokes `LordSurrenderSystem.ExecuteLord`, release invokes `SpareLord` exactly once. Close/reopen safe.

### Phase C — Attack transition to actual player-vs-lord combat
화이트리스트:
- `Assets/Scripts/Systems/InvadedLordEncounter.cs`
- `Assets/Scripts/Systems/PlayerCombat.cs` (only exact NPC/Lord pointer gate / damage hook, preserve dirty diff)
- `Assets/Scripts/UI/Toolkit/InteractionPanelUTK.cs` (Attack button routing/close)
- relevant EditMode tests

수용 기준:
- Attack option begins encounter and closes panel; subsequent ordinary PlayerCombat attacks can damage target (not accidentally ignored by Lord NPC gate). Encounter can attack/reply only according to simple melee range/cooldown, dies at zero, sets Defeated once. No Dracula teleport/skill behavior. No auto resolution/ownership change until an explicit choice. Ordinary NPC click/interaction/other Lord behavior unaffected.
- Consider all independent player mouse consumers `PlayerCombat` vs `AttackSystem`; prove one authoritative behavior and add precise UI/pointer gate so one click opening interaction does not damage same frame.

## Verification and gates
- Implement tests first and record assertion-based RED (compile failures are not valid RED); GREEN after implementation.
- Implementation agent compile evidence; independent QA agent fresh compile + EditMode, separate from implementer.
- Play required to assert generated room placement, mouse interact, visible dialogue, actual swing/HP/death. Play is approval-gated and NOT authorized by this request. No Play, no commit until separate approval.
- Final report separates Compile, EditMode, Play-unverified; precise paths, interactions and consequences.
