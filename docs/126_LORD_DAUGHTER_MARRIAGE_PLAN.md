# 126차 계획 — 영주 딸 등장·꽃 선물 호감도·프로포즈 결혼 시스템

> 2026-10-24 · 사용자 요청: "포섭(점령) 성공으로 영주 씬 진입 시 확률적으로 영주 딸 등장, 꽃 채집→선물→호감도 상승→호감도 충족 시 프로포즈 수락 → 결혼"
> 설계 확정: clarify 10분 무응답 → **기존검증 최소설계(기본안) 진행** (60차 선례). Play·커밋은 별도 승인 게이트.
> 실측 근거: `InvadedLordEncounter.cs` 82~132(영주실 캡슐 스폰), `IndoorSceneTransition.cs:292`(런타임 호출), `HerbPickup.cs`(E키 채집·HerbType→ItemData 매핑 84행), `NaturalResourceSpawner.cs`(야생 노드 결정론 배치), `InteractionPanelUTK.cs:858`(침략 영주 분기), `HoverTargetClassifier.cs:129`(Lord 판정), `TerritoryData.cs:164`(113차 필드 선례), `docs/113_LORD_FOOD_GIFT_BONUS_HINT_PENALTY_PLAN.md`(저장 패턴).

## 0. 기본 설계 결정 (기본안)

- **딸 등장**: [10-24 사용자 정정] **생존 영주(비PlayerOwned — 타 소속 영주 살아있음)의 성** 내부 영주실 진입 시 **결정론 확률 40%** 등장(병사 포섭 후 적성 영지 진입 경로). 점령(PlayerOwned) 완료 성에는 미등장. 영지ID 시드 해시 기반 — 저장 불요(재계산), 동일 영지는 항상 동일 결과.
- **딸 위치/외형**: 영주실(LordBed 앵커) 인접 오프셋, 영주와 동일 캡슐 프리미티브 파이프라인(그 씬의 영주 자체가 캡슐이므로 일관). 네임플레이트 `🌸 {딸이름}`. 딸 이름은 영지ID 결정론 선택(풀 12개).
- **꽃**: 야생 꽃 노드 신설 — 들꽃(flower_wild) 1종. **HerbPickup에 Flower 타입 확장**으로 기존 채집 파이프라인(E키 수확·약초꾼 자동 채집·호버 Gather 분류·스택) 전부 재사용.
- **호감도**: 영지별 0~100 int, 들꽃 1개 선물 **+10**. 저장 대상.
- **프로포즈**: 호감도 ≥80 시 상호작용 패널에 버튼 노출 → 수락(결정론) → 결혼 상태 저장 + 해당 영지 `loyaltyToPlayer` **+30 (1회, 멱등)**.
- **결혼 효과 확장**(배우자 동행·식사 보너스 등)은 별도 사이클 — 이번 사이클 범위 아님.

## 1. Phase A — 꽃 채집 (선물 재료)

- `PlayerInventory.cs`: 신규 아이템 `Flower_Wild` — `id="flower_wild"`, `displayName="들꽃"`, `category=ItemCategory.Material`, `maxStack=50`, 설명 "들에서 피는 꽃. 정성을 담아 선물할 수 있다."
- `HerbPickup.cs`: `HerbType` 열거형에 `Flower_Wild` 추가 + 84행 매핑 테이블/switch(131행 씨앗 폴백 등)에 꽃 항목 반영. 꽃은 씨앗 없음(수확 시 들꽃만). **열거형 순회/완전성 switch를 검사하는 기존 테스트(HerbPickupTests 등)가 있다면 결과 확인 후 보정.**
- `NaturalResourceSpawner.cs`: 꽃 노드 카테고리 추가 — 목표 수는 허브(HERB_TARGET)와 동일 수준, **전 국가 균등 배치**(사용자 설계 계약), 기존 결정론 시드/순환/최소간격/스폰 제외존 준수.
  - 시각 프리팹: 기존 허브 시각 프리팹 로딩 경로를 실측해 동일 방식으로 꽃 시각을 시도. 전용 프리팹이 없으면 **절차적 시각**(줄기 실린더 + 꽃잎 쿼드 2교차, 파스텔 3색 결정론 순환)을 `SpawnNode` 재사용으로 구성. 프리팹 부재로 배치 생략되면 안 됨(로그로 확인 가능하게).

## 2. Phase B — 딸 스폰·데이터

- 신규 `Assets/Scripts/Systems/LordDaughterNPC.cs` (static 스포너 + 상태 API):
  - `public static bool HasLordDaughter(string territoryKey)` — FNV-1a 해시 % 100 < 40. **결정론(동일 키 항상 동일)**.
  - `public static LordDaughterNPC TrySpawnForTerritory(GameObject room, string territoryKey)` — 영주실 스폰. 위치: `GetNorthernBedroomAnchor(room)` + 오프셋 (1.6, 0, 0.6), `IsNorthernBedroomPosition(room, …)` 검증 실패 시 (−1.6, 0, −0.6) 폴백, 그래도 실패 시 스폰 생략 + 로그. 호버 타기 캡슐콜라이더(높이 1.75) + Rigidbody(kinematic) + `NameplateDisplay` 구성 — 영주 스폰 코드(InvadedLordEncounter 99~128행) 동일 패턴. **신규 태그 생성 금지**(분류는 컴포넌트 기반).
  - 딸 이름: 풀 12개(한국풍 여성명)에서 영지ID 결정론 선택, `DisplayName` 프로퍼티 노출.
  - `GiftFlower(PlayerInventory inventory)` → 선물 실행(§1 꽃 1개 소비, 성공/실패+사유 반환), `TryPropose()` → 게이트 판정(§0).
- `TerritoryData.cs` (TerritoryState, 113차 `lastPreferredFeedDay` 선례 그대로):
  - `[SerializeField] private int _daughterAffection = 0;` + 프로퍼티(클램프 0~100)
  - `[SerializeField] private bool _daughterMarried = false;` + 프로퍼티
- `InvadedLordEncounter.cs`: `SpawnForInvadedTerritory` 말미에 `LordDaughterNPC.TrySpawnForTerritory` 훅 — **ownership==PlayerOwned && HasLordDaughter(키)** 조건 충족 시에만.
- **[10-24 1차 개정 → 2차 정정 — 훅 최종 형상]** 1차 개정에서 PlayerOwned 분기에 훅을 추가했으나, 사용자 정정("점령이 아니라 포섭을 한 뒤 영주에 진입" — 생존 영주 성 시나리오)으로 **PlayerOwned 분기 훅은 제거**. 최종: `SpawnForInvadedTerritory` 내부 훅 1곳만 — `IndoorSceneTransition` 비PlayerOwned 분기(292행, 생존 영주+수비병사 스폰 본경로)에서 호출되며 훅 게이트는 `ownership == PlayerOwned → 조기 차단`(생존 영주 전용) + `HasLordDaughter` 결정론 롤.
- 호감도/결혼 상태 접근은 TerritoryState 경유(territoryKey → GetState), 딸 GameObject는 재진입 시 재스폰되므로 상태의 단일소스는 DB.

## 3. Phase C — 상호작용 UI (선물/프로포즈)

- `HoverTargetClassifier.cs`: Lord 판정(129~134행) 뒤에 `LordDaughterNPC` 컴포넌트 판정 추가 → 신규 `TargetKind.LordDaughter` (Enemy 태그 폴백보다 먼저).
- `InteractionPanelUTK.cs`: 딸 대상 분기 추가 — 버튼 구성:
  - 항상: `대화하기`(짧은 인사 + 현재 호감도 힌트 대사)
  - 호감도 <100: `들꽃 선물` — 성공 시 토스트/말풍선 피드백(호감도 +N), 인벤에 꽃 없으면 **소비 없음 + "선물할 들꽃이 없다" 피드백**(상호작용불가=피드백 필수 계약)
  - 호감도 ≥80: `프로포즈` — 수락 → 결혼 토스트 + 네임플레이트 `♥ {이름}` 갱신 + 충성도 +30. 이미 부부 → 멱등 토스트
  - 패널 크기: 63차 영주 1행(240×54)/2행(240×82) 선례 준수. 피드백은 기존 토스트 서비스/`GuardTalkBubble` 계열에서 **프로젝트에 실장된 방식 실측 후 동일 패턴** 사용(신규 UI 프레임워크 도입 금지).

## 4. Phase D — 저장 (구세이브 안전)

- `SaveData.cs`(TerritorySaveData): `public int daughterAffection = 0;` / `public bool daughterMarried = false;` **필드 초기값 기본**(JsonUtility 누락 시 구세이브 안전 — 113차 선례).
- `SaveManager.cs`: `CollectTerritoryData` 수집 + `ApplyTerritoryData` 반영.

## 5. 테스트 명세 (TDD — 구현 서브에이전트 작성, 실행 판정은 QA 서브에이전트)

신규 `Assets/Scripts/Tests/EditMode/LordDaughterSystemTests.cs` (namespace ProjectName.Tests.EditMode, 기존 클래스명 충돌 실측 후 신설):

1. **결정론**: HasLordDaughter 동일 키 3회 동일값 + 47영지 전수에서 true/false 분포 양쪽 존재(전부 true/전부 false 금지).
2. **호감도**: 선물 +10, 100 클램프, 100에서 선물 거부(꽃 소비 없음).
3. **프로포즈 게이트**: <80 → false + married 불변; ≥80 → true + married + `loyaltyToPlayer` +30; 재호출 멱등(충성도 재가 없음).
4. **저장 왕복**: affection/married Collect→Apply 동일성.
5. **아이템**: flower_wild 정의 존재 + HerbPickup Flower_Wild→ItemData 매핑.
6. **위치 계약**: 딸 스폰 위치가 Bedroom 존 내부(IsNorthernBedroomPosition true).

## 6. 범위 파일 (화이트리스트 — 이 외 수정 금지)

| 구분 | 파일 |
|---|---|
| 신규 | `Assets/Scripts/Systems/LordDaughterNPC.cs` |
| 신규 | `Assets/Scripts/Tests/EditMode/LordDaughterSystemTests.cs` |
| 수정 | `Assets/Scripts/Systems/HerbPickup.cs` |
| 수정 | `Assets/Scripts/Core/PlayerInventory.cs` (아이템 정의만) |
| 수정 | `Assets/Scripts/Systems/NaturalResourceSpawner.cs` |
| 수정 | `Assets/Scripts/Core/Data/TerritoryData.cs` (필드/프로퍼티만) |
| 수정 | `Assets/Scripts/Systems/SaveData.cs`, `Assets/Scripts/Systems/SaveManager.cs` |
| 수정 | `Assets/Scripts/Systems/InvadedLordEncounter.cs` (스폰 훅만) |
| 수정 | `Assets/Scripts/UI/IndoorSceneTransition.cs` (PlayerOwned 진입 분기 딸 스폰 훅 1회 — 10-24 개정 승인) |
| 수정 | `Assets/Scripts/Systems/PlayerCombat.cs` (커서 NPC-family 억제 목록에 LordDaughterNPC 1행 추가 — 클릭 공격 스윙 미억제 방지, 10-24 개정 승인) |
| 수정 | `Assets/Scripts/Systems/HoverTargetClassifier.cs` (판정 1분기) |
| 수정 | `Assets/Scripts/UI/Toolkit/InteractionPanelUTK.cs` (딸 분기) |
| 신규 | `Assets/Scripts/Systems/LordDaughterNPC.cs.meta` 등 신규 .meta (Unity 생성분) |

범위 밖 필요 발생 시 **먼저 보고하고 승인 후** 진행. 무관한 더티 워킹트리 파일 손대지 않기(작업 전 `git status` 기록).

## 7. 완료 기준·증거 요건

- 구현 제출물: 수정 파일 전체 경로 + 파일별 diff + `compile_test.sh` exit 0(`compile.log` 경로, error CS=0) + 테스트 명세 + 범위 준수 선언.
- QA 제출물: ①컴파일(`compile.log`) ②전체 EditMode XML(총/통과/실패/스킵 + 9건 baseline 실패군 동일 유지, 신규 실패 0) — 경로 명시. ③Play는 사용자 승인 전이므로 **미실행 명시**.
- 승인 게이트: Play 모드 검증·커밋 모두 사용자 승인 후.
