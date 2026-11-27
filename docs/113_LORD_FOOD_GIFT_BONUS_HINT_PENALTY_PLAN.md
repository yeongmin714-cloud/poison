# 113차 계획 — 영주 선호 음식 기능확장 A/B/C (선물 보너스 / 재료 힌트 / 미지급 페널티)

> 2026-10-22 · 승인 근거: 10-21 세션 "기능확장 진행해줘" + "순서대로 진행"(A→B→C 순차)
> 선행 상태: 선호 음식 링 가중(TerritoryDatabase.GenerateLordInfo 10슬롯·rng 단일 드로우)·인텔 7일주기(EnvoySystem)·지도 툴팁 노출 구현 완료. EditMode baseline 1406/1395/11.

## 0. 핵심 설계 결정 (사용자 보고 사항)

**선호 음식 10종(구운 고기·생선 스튜·버섯 수프·야채 샐러드·빵과 치즈·포도주·꿀 케이크·로스트 치킨·양고기 스테이크·해산물 파이)은 인벤 아이템으로 미실장이다.**
실측: PlayerInventory Food/Meat 실장 항목 = 사과(fruit_apple)·작물(crop_carrot/corn/pumpkin/radish)·고기(meat_rabbit/boar/wolf)·생선(fish_common/rare/legendary). `grep "구운 고기"`는 TerritoryDatabase/테스트 주석뿐.

→ "선호 음식 일치"를 **음식 계열(재료 소속) 일치**로 구현한다(현실 재료 반영 설계 선호와 일치).
- 고기류 선호(구운 고기·로스트 치킨·양고기 스테이크) ← `meat_` 접두
- 어패류 선호(생선 스튜·해산물 파이) ← `fish_` 접두
- 채소·과일 선호(야채 샐러드) ← `crop_` 접두 + `fruit_apple`
- 재료 미실장 음식(빵과 치즈·버섯 수프·포도주·꿀 케이크·붉은 포도주) ← 매칭 불가(빈 집합, 카탈로그에 명시 — "조리/교역 확장 시 갱신")
- 요리 아이템이 실장되면 카탈로그 단일소스에 정확 일치로 확장한다.

## 1. A — 선호 음식 선물 보너스 (`TerritoryLordDoorSystem.TryFeedLord`)

- 신규 **`Assets/Scripts/Core/Data/LordFoodCatalog.cs`**(static, namespace ProjectName.Core.Data) — 음식 계약 단일소스:
  - `GetTier(string preferredFood)` → 1/2/3. T1=구운 고기·야채 샐러드·빵과 치즈·버섯 수프, T2=생선 스튜·로스트 치킨·포도주, T3=꿀 케이크·양고기 스테이크·해산물 파이. 미등록(붉은 포도주 등 특이 음식)=3 폴백.
  - `IsPreferredGift(string preferredFood, string foodItemId)` → bool(§0 계열 매핑, 접두사 판정).
  - `GetMainIngredients(string preferredFood)` → 주재료 문자열(§2 힌트용. 예: 해산물 파이="해산물·버터").
  - `IsIngredientObtainable(string preferredFood)` → bool(§0 실장 여부).
  - `BuildSourceHint(string preferredFood)` → §2 힌트 문구 조립(순수 정적 — 계약 테스트 대상).
- `TryFeedLord` 증가치 규칙:
  - 미일치 → **+5** (기존 유지)
  - 일치 → **+15**
  - 일치 && GetTier(preferredFood)==3 && difficulty ∈ {Ring3, Ring4, Empire} → **+20** (재료 구하기 어려운 고급 음식)
- 시그니처: 기존 2인자 `TryFeedLord(string, string)` 계약 보존(=out 버전 위임) + 신규 `TryFeedLord(string, string, out float loyaltyDelta)`.
- 일치 지급 시 `state.lastPreferredFeedDay = TimeManager.GetOrCreate().CurrentDay` 갱신(§3 연동).
- 로그: `[LordDoor] 🍗 ... 충성도 +N (+보너스: 선호 음식 계열 일치 / 고급 음식+강한 링)` 형태 — 증가치·사유 명시.
- `LordFeedWindowUTK.FeedSlot`: out 버전 사용 → 상태 라벨 `"지급 완료 — 영지 충성도 +N"`(+보너스 문구), 기존 "+5" 하드코딩 폐기.
- ⚠️ `GenerateLordInfo`(10슬롯 배열·rng 추첨)는 **전혀 손대지 않는다** — 결정론·TerritoryDataTests 계약 유지.

## 2. B — 인텔 보고 재료 힌트 (`EnvoySystem.DeliverIntelReport`)

- info 문자열에 **라인 추가(기존 라인 불변 — EnvoyIntelTests 단언 보호)**:
  `🥄 재료 힌트: {LordFoodCatalog.BuildSourceHint(def.lord.preferredFood)}`
- BuildSourceHint 문구 규칙(정직성 원칙 — 없는 획득처를 말하지 않는다):
  - obtainable=true: T1 → "사냥·농사·채집으로 흔히 조달 — 인근 마을"; T2 → "낚시·교역으로 확보 — 인근 교역소"; T3 → "희귀 재료 — 큰 교역소나 황제국 상단이 필요"
  - obtainable=false: "재료 미실장 — 교역·조리 확장 후 입수 가능" (+주재료명 병기)
  - 문구에는 주재료(GetMainIngredients) 포함.

## 3. C — 음식 미지급 페널티 (일일 틱)

- `TerritoryState` 필드: `[SerializeField] private int _lastPreferredFeedDay = -1;` + 프로퍼티 `lastPreferredFeedDay`(setter `Mathf.Max(-1, value)`).
- `TerritorySaveData`(SaveData.cs): `public int lastPreferredFeedDay = -1;` **필드 초기값 -1**(JsonUtility 누락 시 초기값 유지 → 구세이브 안전).
- `SaveManager`: `CollectTerritoryData` 수집 + `ApplyTerritoryData` 반영(값 < 0 → -1 유지).
- `TerritoryLordDoorSystem`:
  - 상수 `FEED_PENALTY_GRACE_DAYS = 7`, `FEED_PENALTY_PER_DAY = 2f`.
  - `EnsureFeedLoyaltyHook()` — `TimeManager.GetOrCreate().OnDayStart += TickFeedLoyalty` (멱등 가드, EnvoySystem.EnsureIntelDayHook 패턴). `UpdateDoors`에서 1회 보장 호출.
  - `public static void TickFeedLoyalty()` [테스트 훅]: 전 영지 순회 — **ownership==LordOwned && IsLordDoorOpen(키)** 만 대상:
    - lastPreferredFeedDay < 0 → = today(초기 앵커, 감소 없음)
    - else `today - last > GRACE` → `loyaltyToPlayer -= 2f` (Clamp 0~100 자동) + 감소 로그
  - 문 잠김/플레이어 점령 영지는 앵커도 감소도 없음(공정성: 줄 수 없는 영지에 페널티 금지).

## 4. 테스트 (TDD — 구현 서브에이전트 작성, 실행 판정은 QA 서브에이전트)

신규 3파일 (namespace ProjectName.Tests.EditMode, 기존 클래스명 충돌 없음 실측):
1. `LordFoodCatalogTests.cs` — 티어 표(10종+붉은 포도주=3), 계열 매칭(meat_boar→구운 고기 O / crop_carrot·fruit_apple→야채 샐러드 O / fish_common→생선 스튜 O / meat_*→생선 스튜 X / 미실장 음식 매칭 불가), BuildSourceHint 문구(obtainable/미실장 2계열), 재료 문자열.
2. `LordFeedGiftTests.cs` — 행위: TryFeedLord 3인자 out — 미일치 +5 / 일치 +15 / Ring4+T3 일치 +20 / Empire+T3 일치 +20 / Ring1+T3 음식(꿀 케이크가 Ring1 선호인 케이스는 슬롯상 없음 — 미일치 경로로 검증) / 일치 시 lastPreferredFeedDay 갱신. 문 개방 게이트 유지(잠김 → 실패).
3. `LordFeedPenaltyTests.cs` — TickFeedLoyalty: 앵커 초기화 무감소 / grace 이내 무감소 / 초과 -2/일 누적 / 문 잠김 무영향 / PlayerOwned 무영향 / lastPreferredFeedDay 세이브 왕복 보존(SaveV4PersistenceTests 패턴 참조).

테스트 시 유의: TimeManager.GetOrCreate()는 EditMode 동작(EnvoyIntelTests 선례), GameTime 절댓값 재대입 금지(덜타 경과), TerritoryDrugSystem 오염도 세팅으로 문 개방 시키는 방법은 기존 테스트 grep 후 동일 패턴 사용.

## 5. 범위 파일(화이트리스트) — 이 외 수정 금지

1. `Assets/Scripts/Core/Data/LordFoodCatalog.cs` (+.meta, 신규, GUID 새 생성)
2. `Assets/Scripts/Core/Data/TerritoryData.cs` — TerritoryState 필드 1
3. `Assets/Scripts/Systems/TerritoryLordDoorSystem.cs`
4. `Assets/Scripts/Systems/EnvoySystem.cs`
5. `Assets/Scripts/Systems/SaveData.cs` — TerritorySaveData 필드 1
6. `Assets/Scripts/Systems/SaveManager.cs` — Collect/Apply 2곳
7. `Assets/Scripts/UI/Toolkit/LordFeedWindowUTK.cs` — 표시 갱신
8. `Assets/Scripts/Tests/EditMode/LordFoodCatalogTests.cs` / `LordFeedGiftTests.cs` / `LordFeedPenaltyTests.cs` (+.meta, 신규)

## 6. 완료 기준 & 증거

- 컴파일: `rm -f Temp/UnityLockfile && timeout 550 bash compile_test.sh` → `grep -c 'error CS' compile.log` = 0.
- 구현 후 `Tests/EditMode`에서 변경 대상 문자열 전수 grep — 기존 계약 단언 파괴 0 확인(unity-source-contract-tests 규약).
- 제출물: 수정 파일 목록 + diff + 컴파일 증거 경로 + 테스트 명세 + 범위 준수 선언.
- QA(별개): `timeout 590 bash run_editmode_check.sh` 전수 → baseline 11건 1:1 + 신규 GREEN 증거.
- Play 미실행(게이트): A 보너스 로그/라벨, B 스크롤 힌트 라인, C 충성도 감소 로그 — 사용자 Play 승인 후 확인.