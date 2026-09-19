# 🧪 OpenMMO 벤치마크 통합 로드맵 (Phase O1~O11)

> **For Hermes:** subagent-driven-development로 사이클 단위 실행. 사이클마다 code agent(delegate_task) + QA agent(delegate_task) 필수.
>
> **출처:** `docs/reference/openmmo/` (github.com/Julian-adv/OpenMMO doc/ 38종 — 설계 벤치마킹 전용, 코드/텍스트 복사 금지)
> **인덱스:** `docs/reference/openmmo/README.md` (Tier 분류표 — 서브에이전트에 해당 문서 1~2개만 주입)
> **전제:** Phase 68 U8(UTK 마이그레이션) Play 검증·폐기 완료 후 개시. UTK 이후 모든 신규 UI는 UTK(UXML/USS)만.

---

## 전역 규약 (전 Phase 공통)

| 규칙 | 내용 |
|:-----|:-----|
| UI | 신규 UI = UTK 전용(IMGUI 금지). Theme.uss 디자인시스템 상속(세로gradient+베벨+radial글로우, 섀도우=shadow_glow 9슬라이스 형제) |
| 계층 | Core는 Systems 참조 불가(CS0234) — 시스템 간 액션은 UI 계층 또는 이벤트 브리지 |
| 검증 | 사이클마다 Unity batchmode 컴파일 error CS=0 + 신규 테스트(EditMode) 통과 |
| 컴파일 패턴 | Debug 정규화, foreach(for..in 금지), 컴포넌트 클래스 public(CS0050) |
| 기록 | 사이클 완료 → PROGRESS.md + CYCLE.md + 메모리 3곳 저장 + 텔레그램 알림 |
| 수치 | 모든 밸런스 변경은 근거 문서 값과 "포이즌 실측값" 병기. Numbers not vibes |

---

## 실행 순서 (의존성 기반)

```
밸런스 라인:  O1 티어/드랍 → O2 경제 → O3 레벨곡선
전투 라인:    O4 스탯 → O5 군집회피        (O4는 O1과 병렬 가능)
컨텐츠 라인:  O6 칭호 → O7 악기
대형:         O8 성 건설 (O2 완료 후)
심화:         O9 LLM NPC → O10 생활 컨텐츠 (O2 완료 후)
보류(PARKED): O11 지형/강/날씨 — 좌표함정 재설계 전까지 금지
```

---

## Phase O1: 🧬 아이템 티어 세트 & 드랍 재설계 (ITEM_TIERS.md)

> **목표:** wood~crystal 등급 위에 "세트" 개념과 시그니처 드랍 롤을 도입해 전리품 명확성 상승.
> **참고:** openmmo/ITEM_TIERS.md — 세트 구성(가죽/체인/판금), 시그니처+아이템별 독립 롤, 던전당 기대 ~5회.

| 사이클 | 내용 | 파일 |
|:-------|:-----|:-----|
| C-O1-01 | TierSetData 신규 — 세트 3종(가죽=wood~iron, 체인=iron~gold, 판금=gold~crystal)×부위(투구/갑옷/신발/장갑), 세트ID+보너스 정의 | 신규 `Core/Data/EquipmentTierSet.cs` + `Assets/Scripts/Tests/...` 테스트 |
| C-O1-02 | DropTable에 시그니처 슬롯 추가 — 세트 보증 드랍(체스트 풀 규칙) + 일반 독립 롤 분리. 기존 개체별 DropTable(토끼/멧돼지/늑대)·병사 티어 표는 유지하고 보강만 | 수정 `Core/DropSystem/DropTable.cs`, `Core/DropSystem/DropTableManager.cs` |
| C-O1-03 | 드랍 확률 튜닝 — 던전(동굴)/보석상자 기대 드랍 수 정의(기대 ~5회/던전), 희소 월드 드랍 별도 풀 | 수정 `Systems/GemChest` 계열, DropTable SO 4종 갱신 |
| C-O1-04 | 세트 보너스 계산(2세트/4세트 효과 → 전투력 계수) + 툴팁 반영(TooltipWindowUTK 등급별 테두리 유지) | 수정 `Systems/GuardEquipmentSystem.cs`, 툴팁 UTK |
| C-O1-05 | Play 판정 — 드랍 명확성/세트 표시/밸런스 스냅샷 기록 | QAPROGRESS 스냅샷 |

**테스트:** TierSetTests(세트 구성/등급 매핑), DropSignatureTests(보증 드랍/독립 롤/기대값), SetBonusTests.

---

## Phase O2: 💰 경제 리밸런싱 (ECONOMY.md + PRICING.md)

> **목표:** 금화 유입≫유출 구조(복수명부 +1000, 점령보상 8배)에 유출구와 원장을 설치해 시간당 순유입 안정화.
> **참고:** openmmo/ECONOMY.md — 머니 펌프(재활용), 인플레이션 감사. openmmo/PRICING.md.

| 사이클 | 내용 | 파일 |
|:-------|:-----|:-----|
| C-O2-01 | EconomyAuditSystem 신규 — 금화 원장(유입: 처치/판매/퀘스트/점령보상, 유출: 상점구매/수리/고용/기부/국기변경), 일별 집계+SaveData 기록 | 신규 `Systems/EconomyAuditSystem.cs` |
| C-O2-02 | 가격 공식 정리 — 판매 스프레드 검토(ShopWindowUTK CalculateSellPrice), 티어 배율×보상 multiplier(1x~8x) 매핑표 확정 | 수정 `UI/Toolkit/ShopWindowUTK.cs`, 가격 데이터 |
| C-O2-03 | 유출구 설치 — ①창고 슬롯 확장 비용(20슬롯→구매형) ②수리비에 골드 성분 추가 ③칭호 등록비(O6 선결제) | 수정 `Systems/WarehouseSystem.cs`, `EquipmentRepairSystem` |
| C-O2-04 | 감사 리포트 + 튜닝 — 목표: 플레이 1시간 기준 순유입 ±10% 내. 리포트는 스크린샷+표로 기록 | EconomyAuditSystem 리포트 출력 |
| C-O2-05 | Play 판정 — 상점/수리/확장 전체 흐름 회귀 | QAPROGRESS 스냅샷 |

**테스트:** EconomyAuditTests(원장 집계), PricingSpreadTests, WarehouseExpansionTests(멱등성 — SeedDefaultBombs 패턴 준수).

---

## Phase O3: 📈 레벨 곡선 페이스 (LEVEL_CURVE.md)

> **목표:** Lv1~50 곡선을 "시간당 레벨 페이스"로 재검증하고 컨텐츠 의존성 맵 확정.
> **참고:** openmmo/LEVEL_CURVE.md — 목표 페이스 표, 콘텐츠 의존성, 마이그레이션.

| 사이클 | 내용 | 파일 |
|:-------|:-----|:-----|
| C-O3-01 | 현재 곡선 추출 — GuardLevelSystem(레벨당 HP+10/공+1/방+0.5, XP 1.2배율)·MonsterLevelSystem 표화 + 실측 킬시간 | 읽기 `Systems/GuardLevelSystem.cs`, `Systems/MonsterLevelSystem.cs` |
| C-O3-02 | 목표 페이스 정의 — 링별 도달 시간 표(예: Ring1 도달 10분~Lv10 등) + XP 테이블 재조정 | 수정 GuardLevelSystem/MonsterLevelSystem 상수 |
| C-O3-03 | 컨텐츠 의존성 맵 — 어느 레벨대에서 어떤 컨텐츠(던전/선술집/건설/황제국) 개방되는지 문서+게이팅 코드 | `Core/Data/TerritoryDatabase.cs`, 문서 |
| C-O3-04 | 회귀 테스트 갱신 (GuardLevelTests 22/MonsterLevelTests 22) | 테스트 갱신 |

---

## Phase O4: ⚔️ 컴뱃 스탯 고도화 (COMBAT.md)

> **목표:** HP 재생 통일 + 영주 능력치 생성 공식화.
> **참고:** openmmo/COMBAT.md — 4d6 스탯 생성, HP 재생(주기/조건/량), Guard 계산.

| 사이클 | 내용 | 파일 |
|:-------|:-----|:-----|
| C-O4-01 | HP 재생 정비 — Phase 27의 "10% 부활+30초 자동회복"을 공식화(재생 주기/전투 중 조건/량) 하여 플레이어/병사/몬스터 동일 규칙 적용 | 수정 `Systems/...` 재생 관련, Phase27 계열 |
| C-O4-02 | 영주 능력치 생성 — 4d6-드롭로우스트 + 링/국가 가중치 보정 → LordInfo 확장(입맛/지병/충성심과 함께 능력치 6종) | 수정 `Core/Data/TerritoryDatabase.cs` (LordInfo) |
| C-O4-03 | Guard 방어 계산 검증 — RingDifficultyData defenseRating→multiplier(0.8x~1.6x)와 COMBAT.md Guard 산식 대조 | `Systems/RingDifficultyData` |
| C-O4-04 | 테스트 — LordStatTests(4d6 분포/링 가중), RegenTests |

---

## Phase O5: 🧍 군집 회피 (MONSTER_SEPARATION.md)

> **목표:** Ring4(21~40명 병사) RTS 뭉침 해소.
> **참고:** openmmo/MONSTER_SEPARATION.md.

| 사이클 | 내용 | 파일 |
|:-------|:-----|:-----|
| C-O5-01 | SeparationSystem 신규 — 반발 벡터 분리, 근거리(30m)만 갱신하는 스로틀, 병사+몬스터 양쪽 적용 | 신규 `Systems/SeparationSystem.cs` |
| C-O5-02 | RTS 결합 — 부대 이동 시 산개 배치(우클릭 목표 주위 링 분산), GuardSelectionManager/RTSCommandSystem 연계 | 수정 `Systems/GuardSelectionManager.cs` |
| C-O5-03 | 성능 검증 — 40명 동시 이동 프레임 측정(스크린샷 67 룰: 순번 스냅샷+Editor.log) | QAPROGRESS |

---

## Phase O6: 🏅 칭호 시스템 (TITLES.md + HEROIC_TALES.md)

> **목표:** 영주 처형/암살/점령/크래프트 이력 → 칭호 획득+표시. 복수명부와 자연 결합.
> **참고:** openmmo/TITLES.md, HEROIC_TALES.md.

| 사이클 | 내용 | 파일 |
|:-------|:-----|:-----|
| C-O6-01 | TitleData+TitleManager — 칭호 20종+획득 조건(처형N회/암살/점령N개/세트완성/O2 감사 골드 등) | 신규 `Core/Data/TitleData.cs`, `Systems/TitleManager.cs` |
| C-O6-02 | 발급 hook — RevengeListManager(처형), AchievementSystem, TerritoryCapture 이벤트 연결 | 수정 `Core/Data/RevengeListData.cs`, `UI/AchievementSystem.cs` |
| C-O6-03 | UI — StatusWindowUTK 칭호 장착 선택 + 플레이어 이름표/영주대면에 표시(NameplateOverlayUTK 규약 준수) | 수정 `UI/Toolkit/StatusWindowUTK.cs` |
| C-O6-04 | 영주 성취사 — 81영주(이름풀 100개) 배경 텍스트 데이터 + 영주대면 대사 확장 | `Core/Data/TerritoryDatabase.cs` |

---

## Phase O7: 🎻 바드 악기 확장 (INSTRUMENT.md)

> **목표:** BardMercenary(류트 15m 버프)를 악기 아이템 체계로 확장.
> **참고:** openmmo/INSTRUMENT.md.

| 사이클 | 내용 | 파일 |
|:-------|:-----|:-----|
| C-O7-01 | InstrumentData 5종(류트/피리/드럼/하프/나팔) — 악기별 버프(공/방/이속/재생/사기) 상이 | 신규 `Core/Data/InstrumentData.cs` |
| C-O7-02 | 연주 상호작용 — 선술집 무대(TavernInteriorBuilder) E키 연주 → 60초 파티 버프 + BardMercenary 버프 로직 재구성 | 수정 `Systems/BardMercenary.cs`, `Systems/MercenaryManager.cs` |
| C-O7-03 | 드랍/상점 연계 — O1 시그니처 드랍 풀에 악기 추가 + 상점 재고 | DropTable, ShopWindowUTK |

---

## Phase O8: 🏗️ 성 영지 건설 (HOUSE_BUILDING.md + HOUSING_SYSTEM.md) — 대형

> **목표:** 설계도 기반 영지 건설(외벽/탑/창고 확장/축사 등) → 성 내부 시스템과 접합.
> **참고:** openmmo/HOUSE_BUILDING.md — 설계도 아이템/재료/배치 검증/소유/성능. HOUSING_SYSTEM.md — 모듈러 구조.

| 사이클 | 내용 | 파일 |
|:-------|:-----|:-----|
| C-O8-01 | BlueprintData + ConstructionManager — 설계도 8종(외벽/문/탑/창고확장/축사/대장간/정원/망루), 영지 경계+겹침+지형 배치 검증 | 신규 `Core/Data/BlueprintData.cs`, `Systems/ConstructionManager.cs` |
| C-O8-02 | 건설 실행 — 재료 소모(기존 자원 체계)+진행 시간(오프라인 완료 포함)+완성 연출 | ConstructionManager |
| C-O8-03 | 인테리어 접합 — 축사=탈것(O10), 대장간=수리 비용감소, 창고확장=O2 슬롯 구매와 통합. 침실/Exit 복귀·침대세이브 스폰 회귀 없음 확인 | `Systems/PlayerCastleInteriorBuilder` 연계 |
| C-O8-04 | UI — ConstructionWindowUTK (설계도 선택/고스트 배치 미리보기/해체/환불 50%) | 신규 `UI/Toolkit/ConstructionWindowUTK.cs` |
| C-O8-05 | 저장/복원 — SaveData에 건설물 영구화 + 세션 재입장 회귀 테스트 | `Core/SaveManager` 계열 |
| C-O8-06 | 성능/회귀 — 건설물 다수(20+) 배치 시 드로콜/충돌 검증 + 전 Phase 회귀 | QAPROGRESS |

---

## Phase O9: 🗣️ LLM NPC (NPC_MONSTER_AI.md)

> **목표:** 영주/병사 대화에 LLM 계층 도입 (2계층: 규칙 폴백 필수 — 오프라인에서도 게임 성립).
> **참고:** openmmo/NPC_MONSTER_AI.md — 2계층 AI, 3계층 프롬프트, LLM 스케줄러.

| 사이클 | 내용 | 파일 |
|:-------|:-----|:-----|
| C-O9-01 | NPCDialogueAdapter — 규칙 기반 응답(현행 대사) + LLM 응답(옵션) 이중 경로. 응답 큐/캐시/타임아웃 폴백 | 신규 `Systems/NPCDialogueAdapter.cs` |
| C-O9-02 | 영주 프롬프트 템플릿 — 국가/링/성격/지병/입맛/능력치(O4) → 시스템 프롬프트 자동 생성 | 템플릿 데이터 |
| C-O9-03 | 암살 퍼즐 심화 — 대화 단서(입맛/지병)를 LLM 대화에서 얻도록 재설계, 의심 확률 로직 유지 | 대화/독살 시스템 |
| C-O9-04 | UI — 대화창 UTK 폴링 개선 + 대화 로그 저장 | 대화 UTK |

**게이트:** API 키/프로바이더는 config 기반, 유저 미설정 시 규칙 폴백만 동작(회귀 0).

---

## Phase O10: 🎣 생활 컨텐츠 번들 (FISHING + HUNGER + MOUNTS)

> **목표:** 저강도 컨텐츠 3종으로 O2 경제 유출구+소모 재료 순환 완성.
> **참고:** openmmo/FISHING.md, HUNGER.md, MOUNTS.md.

| 사이클 | 내용 | 파일 |
|:-------|:-----|:-----|
| C-O10-01 | FishingSystem — 계곡/수역 근처 E키 낚시, 미니게임, 물고기 6종→요리 12종 연계 | 신규 `Systems/FishingSystem.cs` |
| C-O10-02 | HungerSystem — TimeManager 일과 연계, 허기 저하→회복/이속 감소(전투 불능은 아님). 옵션 OFF 기본? NO — 기본 ON, 죽음 없음(패널티만) | 신규 `Systems/HungerSystem.cs` |
| C-O10-03 | MountSystem — 탈것 3종(말/노새/전투마), 이속 버프+인벤 확장(노새), 축사(O8) 소요 | 신규 `Systems/MountSystem.cs` |
| C-O10-04 | UI/Play — 핫바/상호작용 통합 + Play 판정 | 관련 UTK |

---

## Phase O11: 🏞️ 지형/강/날씨 리워크 — 🅿️ PARKED (조건부)

> **전제 없이 개시 금지.** 기존 좌표함정(월드 확장 1450m vs 스플랫 1000m, 지형텍스처 금지) 재설계가 선행.
> **참고:** TERRAIN_GENERATION(2단 해상도), SPLATMAP_V2, RIVER_SYSTEM(Gerstner/삼각주), WEATHER_SYSTEM.
> 재설계 통과 시에만 Phase T 시리즈로 분해하여 개시.

---

## 진행 현황

| Phase | 이름 | 상태 |
|:------|:-----|:----:|
| O1 | 아이템 티어 세트 & 드랍 | ⬜ |
| O2 | 경제 리밸런싱 | ⬜ |
| O3 | 레벨 곡선 페이스 | ⬜ |
| O4 | 컴뱃 스탯 고도화 | ⬜ |
| O5 | 군집 회피 | ⬜ |
| O6 | 칭호 시스템 | ⬜ |
| O7 | 바드 악기 | ⬜ |
| O8 | 성 영지 건설 | ⬜ |
| O9 | LLM NPC | ⬜ |
| O10 | 생활 컨텐츠 번들 | ⬜ |
| O11 | 지형/강/날씨 | 🅿️ PARKED |
