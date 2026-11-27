# 69차 계획 — Test_16 영주 F키 상호작용 + 실내 침대 E키 세이브

> 작성: 2026-10-15. 이 계획은 61~68차의 Test_16/17 실내 테스트씬 구조 위에 작은 2건을 얹는다.
> QAPROGRESS 61차가 "Test_16에도 영주 NPC 배치(동일 체계)"로 배치만 했고, 64차 P3-1의 F키 스캔은
> **PlayerMovement(월드 플레이어)**에 들어갔으나 Test_16/17 플레이어는 **PlayerCastleInteriorTestMovement**라
> F키/E키 상호작용이 실내 테스트씬에서 전부 불능이었다 — 이것이 본 건의 뿌리.

## 진단 (실측)

1. **Test_16 영주**: `IndoorWarTestSetup`이 영주실(10,0,24)에 `InvadedLordEncounter.SpawnForInvadedTerritory(room,"West_01")`로 배치(103-110행) — 배치는 정상. 그러나:
   - F키 감지 = PlayerMovement.HandleInteraction(545-590행)만 보유 → 실내 테스트 무빙(TestMovement 37행)엔 스캔 없음
   - PlayerMovement F스캔의 NPC 블록은 **TerritoryNPCBehaviour만** 감지 — `Lord` 태그/InvadedLordEncounter 감지 블록 자체가 없음(월드 씬도 마찬가지 — Lord는 좌클릭만 가능했음)
2. **침대 세이브**: PlayerCastleInteriorBuilder가 LordBed(-16,0,12)에 `Bed` 컴포넌트+`EInteractionPromptBridge.RaiseRegister("E - 잠자기/세이브")` 자체등록(95행)까지 완비. Bed.OnInteract → UTKWireUp.Wire(AfterSceneLoad 자동) → SleepUTK.Open(bed) → "💾 여기서 세이브" → Bed.SetSpawnPoint + SaveManager.AutoSave. **체인 전부 기존 구현됨** — 빠진 것은 E키 스캔(위 1과 동일 뿌리).
3. UI 게이트: InteractionPanelUTK.Open은 Lord kind 지원(109행), Lord "대화하기"=InvadedLordDialogueUTK.Open(359-365행) — UI 측 무수리.

## Phase 1 — 실내 플레이어 F/E 상호작용 스캔 (PlayerCastleInteriorTestMovement.cs)

파일: `Assets/Scripts/Systems/PlayerCastleInteriorTestMovement.cs`, `Assets/Scripts/Systems/PlayerMovement.cs`, `Assets/Scripts/Systems/IndoorWarTestSetup.cs`, `Assets/Scripts/Systems/LordInteractionTestSetup.cs`, 테스트 1신규.

1. **[69차 P1] TestMovement에 상호작용 스캔 신설** — Update 뒤(35행 `}` 앞) 메서드 추가, Update 끝에서 호출:
   - `HandleInteriorInteractions()` — eKey.wasPressedThisFrame → `TryInteractBed()`, fKey.wasPressedThisFrame → `TryInteractTargets()`.
   - 반경 2.5m OverlapSphere(에디터 인스펙터 필드 `_interactionRadius = 2.5f`, layer -1) — PlayerMovement 31-32행 선례.
   - Bed: `hit.GetComponentInParent<Bed>()` 최근접 1개 → `bed.OnInteract()` (PlayerMovement 533-539행 동일 계약).
   - 병사: `GetComponentInParent<GuardPlaceholder>() && IsAlive` 최근접 → `SoldierInteractBridge.RaiseTargetInteraction(IsRecruited ? Ally : EnemyGuard, guard)` — 68차 P1과 동일 계약.
   - 영주: `GetComponentInParent<InvadedLordEncounter>()`(직접 참조 — 동일 어셈블리 Systems) → `RaiseTargetInteraction(Lord, encounter)`. 병사보다 영주 우선(전투씬 혼재 대비 — 영주실 접근 시 영주 패널 우선).
   - NPC: 리플렉션 TerritoryNPCBehaviour(561-590행 로직 복제) → RaiseTargetInteraction(NPC, npc).
   - 판정 우선순위: Bed(E) / 영주 > 병사 > NPC(F).
2. **[69차 P2] PlayerMovement F스캔에 Lord 감지 추가** — 월드 씬에서도 영주 F키 통일. 545-560행 반복 뒤 병사 미발견 분기에서 Lord(InvadedLordEncounter GetComponentInParent, 태그 Lord 포함) 최근접 → RaiseTargetInteraction(Lord, target). TerritoryNPCBehaviour 감지보다 앞(영주>NPC 우선). 기존 NPC 블록 보존.
3. **[69차 P3] 영주 근접 프롬프트(F)** — IndoorWarTestSetup/LordInteractionTestSetup의 영주 배치 직후: `EInteractionPromptBridge.RaiseRegister(lord.transform, "F키를 눌러 영주와 대화", 3f)` — 63차 근접 프롬프트 라벨 패턴 통일. 인스턴스화된 영주 Transform은 `lord.transform`(Go 루트).
4. **TDD(EditMode — 리플렉션/소스 어서션 하이브리드)**:
   - InteriorInteractionTests 신규: TestMovement 소스에 HandleInteriorInteractions/TryInteractBed/RaiseTargetInteraction(Lord) 마커 + PlayerMovement Lord 감지 마커 + IndoorWar/LordInteraction setup에 RaiseRegister("F키를 눌러 영주와 대화") 마커.
   - 순수 로직: OverlapSphere 시뮬 불가 — 소스 계약(문자열 어서션)을 원칙으로 하되 호출 구조는 기존 선례(SelectionRingFadeTests 소스 어서션) 동일.
   - 기존 회귀 0 필수 — TestMovement 수정이 실내 이동(WASD)을 깨지 않게(이동 로직 무변경 — 메서드 추가만).

## Phase 2 — 세이브 실측 확인 (구현 없음 — 검증)

- Test_16 침대 세이브 체인은 이미 완비(진단 2). Phase 1의 E키 스캔으로 자동 성립.
- SaveManager.Instance 부재 시(EnsureGameManager가 생성 — GameManager.CreateSystemIfMissing "SaveManager" 186-187행) 토스트 "SaveManager 없음" 폴백만 — 씬 세팅상 성립.
- 검증: EditMode 전수(현재 862/851/11 유지) + Play 게이트 기록.

## 공통

- 컴파일 error CS 0 ×2회 / EditMode 862/851/11 유지(신규 회귀 0).
- Play 게이트(사용자): Test_16 ①영주 접근 → F 프롬프트 표시 ②F → 피그마 영주 패널(대화하기★/음식/선물/동맹) → 대화 → InvadedLordDialogueUTK ③침대 접근 → E 프롬프트 ④E → SleepUTK → 💾세이브 → 토스트 "세이브 완료" ⑤WASD 이동 무사고. Test_17 영주 F키도 동시 확인.
- stage/commit/push는 사용자 승인 후.
