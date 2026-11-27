# 128-1 — Test_19_LordPeaceVisit 테스트씬 (병사 포섭 → 생존 영주 성 평화 방문)

> 승인: 기본안(직접 구축형) — clarify 무응답(타임아웃) → 기존검증 최소설계로 진행(프로필 계약).
> 실경로(EnterBuilding) 코드 무수정 = zero-touch. 신규 파일 3종 + meta만 추가.

## 0. 배경/목적

- 사용자 요구: "병사를 포섭해서 영지에 진입했을 때 전쟁으로 이어지지 않고 영주와, 영주 딸과 상호작용할 수 있는 테스트씬".
- 용어계약 [126차]: **포섭 = 병사 포섭**(GuardPlaceholder.BeginRecruit) 전용. 점령(PlayerOwned) 아님 — **생존 영주(비PlayerOwned) 성 방문** 시나리오(126차 영주딸 체계의 전제 시나리오와 동일).
- 실경로 분기(UI/IndoorSceneTransition.cs L350~356): 비PlayerOwned 성 진입 시 `TerritoryBuilder.SpawnInteriorDefenseGuards`(L662 — `SetInCombat(true)`+`HostileToPlayerFaction=true` = 전쟁 경로) + `InvadedLordEncounter.SpawnForInvadedTerritory`(영주 NPC — **공격 버튼을 눌러야만 교전**, 평화 기본값).
- 본 씬은 Test_17_LordInteraction(직접 구축형 — EnterBuilding 미호출) 선례로 성 내부를 씬에 직접 구축하고, **수비병 스폰(전쟁 경로)만 배제**한 평화 상태를 만든다. 126차 영주딸 체계(대화/들꽃 선물/프로포즈)의 전 경로를 검증한다.
- Test_19 슬롯은 비어 있음(기존 씬 Test_01~18 + Test_PlayerCastleInterior 확인 완료).

## 1. 신규 파일 (기존 파일 수정 0건)

| 파일 | 내용 |
|---|---|
| `Assets/Scripts/Systems/LordPeaceVisitTestSetup.cs` | 씬 부트스트래퍼 (sealed MonoBehaviour, namespace ProjectName.Systems) |
| `Assets/Scripts/Tests/EditMode/LordPeaceVisitSceneTests.cs` | 소스계약 테스트 (아래 §4) |
| `Assets/Scenes/TestScenes/Test_19_LordPeaceVisit.unity` | 씬 — Test_17.unity 동일 최소 구조(GameObject 1 + Transform + MonoBehaviour: 부트스트래퍼 스크립트 guid 참조) |
| 위 3종 각각의 `.meta` | 신규 GUID 발급(스크립트 guid는 씬 yaml이 참조하므로 먼저 생성) |

## 2. 씬 사양 — LordPeaceVisitTestSetup

`public const string TestSceneName = "Test_19_LordPeaceVisit";` — Awake 최상단 씬 이름 게이트(불일치 시 즉시 return, Test_17 선례) + `_owned` 패턴 OnDestroy 원복.

### 2-1. 영지 선택 — 결정론 딸 등장 보장
- **West_7 사용**: `LordDaughterNPC.HasLordDaughter("West_7") == true` (FNV-1a hash 0x1743bf9c → roll 32 < 40 — Python 재현으로 사전 검증 완료). 딸 이름 `GetDaughterName("West_7")` = **"다은"**.
- 런타임 구현: West_01..West_20 순회, `HasLordDaughter(key)==true` 첫 키를 채택(예상 West_7) + 선택 키/해시/롤을 로그 출력. 통과 키가 0개면(이론상 불가) West_1 폴백 + 경고 로그.
- 소유권 선행세팅: `TerritoryDatabase.SetOwnership(West_7, LordOwned)` — `RegisterInvadedLord`의 LordOwned 게이트 통과(Test_17 EnsureLordScenarioOwnership 선례). 진입 전 값 기록 → OnDestroy 복원 + `LordSurrenderSystem.ResetAll()`(InvadedLordEncounterTests TearDown 선례 — RegisterInvadedLord 전역 오염 방지 계약).

### 2-2. 실내 구축 (Test_17 선례 복사)
- `UITransitionState.IndoorActive = true`(실내 게이트 계약, OnDestroy 원복)
- `TerritoryBuilder.BuildPaused = true`(성 GLB 82개 생성 차단, 진입 전 값 기록→원복; 자신이 만든 빌더만 OnDestroy에서 제거 — Test_17 선례)
- `NeutralizeSceneSunlights()` + `EnsureGameManager()`(+GuardHostilitySystem/GuardVisualAttachSystem) + `EnsureGuardSystems()`(GuardManager + GuardTaskSystem.Ensure())
- `PlayerCastleInteriorBuilder.BuildPlayerCastleInterior("Western", 0)` + `IndoorMaterialFactory.ApplyToRoom(w,h,d)` + `HideCeiling(room)` + 조명 보정 코루틴(DNC 봉쇄 enabled=false + 방향광 intensity 0/섀도우 None 스냅샷·원복 + 기준 앰비언트 ×1.25 — Test_17 `ApplyTestLightingWhenReady` 그대로)

### 2-3. 영주 + 딸 (실제 본경로 스폰 훅)
- `var lord = InvadedLordEncounter.SpawnForInvadedTerritory(room, "West_7");` — **실제 본경로 함수 1회 호출**. 이 함수 말미의 `TrySpawnLordDaughterForTerritory` 훅이 롤 통과 키라서 딸을 확정 스폰한다(씬에서 딸을 별도 강제 스폰하지 않는다 — 훅 경로 자체가 검증 대상).
- 스폰 직후 `FindAnyObjectByType<LordDaughterNPC>()`로 딸 존재 확인 로그(이름/호감도/위치). 부재 시 LogError(훅 실패 — Play 판정 단서).
- 영주 F 프롬프트: `EInteractionPromptBridge.RaiseRegister(lord.transform, "F키를 눌러 영주와 대화", 3f)`(Test_17 [69차 P3] 선례).

### 2-4. 포섭 대상 병사 1기 (남쪽 로비)
- `TerritoryBuilder.SpawnDeployedGuard("PeaceVisitRecruit_1", 5, NationType.West, pos)` — pos = 남로비(플레이어 스폰 (0,?,-12) 근처, 예: (2.4, 0, -9)); y는 Test_17처럼 실내 바닥 y로 재고정.
- GuardPlaceholder 설정:
  - `SetGuardInfo("수비병 다모", 5, "서")`
  - `SetRecruited(false)` — 미영입(포섭 대상 상태 유지)
  - `HostileToPlayerFaction = false` — 중립(적대 아님. EnemyGuard 분류여도 공격 안 함)
  - `IndoorMoveExempt = true`(실내 이동 게이트 예외 — Test_17 선례)
  - `Loyalty = 75` — `GuardRecruitSystem.LOYALTY_NORMAL(70)` 이상 → 포섭 **결정론 성공**(랜덤 분기 미진입)
- `EnsureCombatCollider(go)`(박스 콜라이더 0.6×1.8×0.6 + kinematic rb + GuardHeadUI — Test_17 선례) + 병사 F 프롬프트 등록.
- ⚠️ 주의점(안내문에도 표기): 미영입 병사 분류 = TargetKind.EnemyGuard(기본 좌클릭 = 공격 과녁) → **포섭은 F키 경로**(SoldierInteractBridge → SoldierInteractUTK 창의 포섭하기 버튼, [72차] BeginRecruit 래퍼 재사용)가 정석.

### 2-5. 플레이어/시드 (ArenaTestSetup 선례)
- 플레이어: PlayerMovement + PlayerInput(defaultActionMap "Player") + PlayerCombat + PlayerHealth + PlayerInventory + PlayerPlaceholder(GLB+애니 패리티 계약 — 125차 후속10 선례), spawn (0, 1.5, -12).
- 골드: PlayerStats 신설(부재 시)+`AddGold(5000, "peace_visit_seed")` — ArenaTestSetup SeedGold/EnsureLobbyGold 선례(입단비 Lv.5 ≈ 75±15% 충분).
- 들꽃: `PlayerInventory.AddItem(PlayerInventory.Flower_Wild, 3)`(선물 3회 가능).
- 딸 호감도 시드: `GetState("West_7").daughterAffection = 70` — 사다리 검증(들꽃 선물 1회 → 80 → 프로포즈 수락 가능). daughterMarried=false 기본 유지.
- 카메라: ArenaTestSetup.SetupCamera 선례 확인 후 동일 적용(직교 탑다운 + IndoorCameraFollow + 휠 ortho 줌; 직접 구축형은 씬 언로드 없음 — DDOL 불요).
- 라우팅: `ContextCommandRouter.Ensure()` + `InteractionPanelUTK.Ensure()`(좌클릭 → 상호작용 패널 — 영주/딸).
- 안내문(OnGUI — Arena 선례) + `[PeaceVisit]` 접두 콘솔 요약 로그: 검증 순서 ①~⑤(아래 §5).

### 2-6. 전쟁 요소 부재 증명 (Play 판정 단서)
- 씬에 `SpawnInteriorDefenseGuards` 호출 없음(소스계약 테스트로 고정).
- Awake 말미 전수 스캔 로그: `HostileToPlayerFaction == true` 병사 수 = 0 확인 로그 + `SetInCombat(true)` 상태 병사 0건.

## 3. 상호작용 경로 (기존 배선 그대로 — 신규 배선 코드 0건)
- **병사 포섭**: F키 → PlayerMovement F키 체인(병사 우선) → SoldierInteractBridge → SoldierInteractUTK → 포섭하기 → `BeginRecruit()` → AttemptRecruit(호감도 75 ≥ 70 성공) + `TryChargeGuardAdmissionFee`(입단비 차감) → 토스트 "포섭 완료".
- **영주**: 좌클릭/F키 → InteractionPanelUTK(대화하기 → InvadedLordDialogueUTK(처형/추궁/풀어주기), 공격하기=NotStarted 게이트, 먹이기=LordFeedTarget→LordFeedWindowUTK) — 공격하지 않는 한 전투 없음(EncounterState.NotStarted 유지).
- **딸**: 좌클릭 → InteractionPanelUTK 3버튼(대화하기 → DialogueCameraDirector.Begin + NPCDialoguePanelUTK.OpenForStatic / 들꽃 선물 → GiftFlower(+10) / 프로포즈 → TryPropose(≥80 → 결혼 ♥ + loyaltyToPlayer +30 멱등)).

## 4. EditMode 소스계약 테스트 — LordPeaceVisitSceneTests
1. 셋업 소스에 `"SpawnInteriorDefenseGuards"` 문자열 부재 단언(전쟁 경로 미호출 계약).
2. `HasLordDaughter("West_7") == true` + `GetDaughterName("West_7") == "다은"`(결정론 고정값) + West_01..20 스캔 첫 통과 키 == "West_7".
3. 중립 병사 계약 소스 단언: `SetRecruited(false)` / `HostileToPlayerFaction = false` / `Loyalty = 75` 리터럴 존재.
4. 시드 계약 소스 단언: `AddGold(5000` / `Flower_Wild, 3` / `daughterAffection = 70`.
5. 씬 파일 존재 + 씬 yaml이 부트스트래퍼 스크립트 guid를 참조하는지(씬-스크립트 결합 계약) 단언.
6. TearDown: `LordSurrenderSystem.ResetAll()`(InvadedLordEncounterTests 선례) — 훅 관련 전역 오염 방지.

## 5. 검증 게이트
- ① 컴파일: `compile_test.sh` → error CS 0(compile.log).
- ② EditMode 전수: `run_editmode_check.sh` → total/passed/failed 파싱(XML 루트 test-run), **baseline 11건 1:1**(baseline 유지/해소/신규 3분류 set 대조 — 신규 테스트 전부 GREEN).
- ③ Play(사용자 승인 게이트 — 별도):
  1. F키 병사 → 포섭하기 → "포섭 완료" 토스트 + 입단비 차감(골드 5,000−fee) + 병사 아군 전환
  2. 영주 좌클릭 → 패널(대화하기/공격하기[/먹이기]) → 대화하기 → InvadedLordDialogueUTK 개방(추궁 응답 확인)
  3. 딸 좌클릭 → 3버튼: 대화하기(카메라 확대+대화창) / 들꽃 선물(70→80) / 프로포즈(수락 → 네임플레이트 "♥ 다은" + 충성도 +30)
  4. 적대 병사 0건 — 전투 미발생([PeaceVisit] 로그로 증명)
  5. 콘솔 `[PeaceVisit]` 요약 로그(선택 영지키/롤/딸 스폰 확인)

## 6. 셀프감사 (계획 단계 완료)
- 용어계약 준수: 포섭≠점령 — 소유권은 LordOwned 유지(점령 전환 없음), PlayerOwned 게이트 미사용.
- 키 충돌: WASD 이동(테스트 무브먼트 계열)·F/E 상호작용·좌클릭 패널 — 신규 키 바인딩 0건.
- 중복 폐기: Test_17은 전투 검증(병사 3명 영입완료+공격 명령), 본 씬은 포섭+딸+평화 — 목적 상이 공존.
- zero-touch: 기존 런타임/테스트 파일 수정 0건 — 씬+부트스트래퍼+신규 테스트만.
- 딸 강제스폰 금지: 실제 훅 경로(롤 통과 키)로 검증 — 계약 흐림 방지.
