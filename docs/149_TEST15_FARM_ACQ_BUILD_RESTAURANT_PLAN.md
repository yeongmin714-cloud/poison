# 149차 계획 — Test_15 농경 영지 게이트 + 획득 연출 GLB + 인벤 정합 + 건설 게이지 + 음식점 실구매 검증

## §0 사용자 지시 (2026-11-03, 5건 — 확인 답변 반영 확정본)
1. 농경 시스템 = 아군 영지에서만 가능 → Test_15에서만 아군 영지 판정이 통과되도록 하고 농경 적용 확인.
2. **모든 획득 아이템**(낚시 포함 전 종류) — 획득 시 아이템 GLB가 **플레이어 얼굴 위에서 부유**하는 연출. 현재 획득 GLB가 전혀 보이지 않음(사용자 실측).
3. 인벤토리 UI 미정합 = **창의 화면 배치(앵커/위치) 문제**로 확정(사용자 답변) — 내부 갭 아님.
4. 건설 시 위에 건설 게이지가 표시 안 됨 — 확인 요청.
5. 음식점 배치 검증 강화 — Test_15에서 음식+마약 음식 재고 확인 + NPC 1명 추가로 실제 구매 액션 검증. **사용자 실측: "음식점 재고가 뜨지 않음, 판매할 수 있는 음식이 없다고 뜸"** — 재고 0종이 실측 결함.
- "계획부터 먼저" — 본 문서 승인 전 구현 없음.

## §1 현재 구조 실측 (코드 근거)

### 1-A 농경 아군 영지 게이트
- FarmPlot.ValidateOwnership (FarmPlot.cs L116~141): TerritoryDatabase 조회 → `ownership == TerritoryOwnership.PlayerOwned` 아니면 `ResetToGrass("아군 영지에서만 경작 가능")`. Test_15 밭 = `FarmingManager.SpawnPlots(NationType.East, 1, ...)` → East #1 영지.
- 소유 전환 실제 경로: TerritoryBannerSystem.ChangeOwnership(territoryId, name, newOwner, isPlayerOwned) (Systems/TerritoryBannerSystem.cs L36).
- **원인 가설(확정 전)**: Test_15에서 East #1이 PlayerOwned 아님 → 밭이 Grass 강제/경작 불가.

### 1-B 획득 연출 (129/130/142/144차 누적)
- 이미 존재: 획득 시 HarvestResultBridge.Publish(채집 HerbPickup L365 / 벌목·채광 ResourceNode L189) → **AcquisitionCinema** 단일 소비자(UI/Toolkit/AcquisitionCinema.cs) = ①대화 패널("X를 획득했다!") ②**손끝 GLB(AttachHandItem)** ③상반신 클로즈업 카메라(frontView). 환호 애니 = TriggerCheer(Player_AC Cheer 상태, 129차).
- GLB 경로: AcquisitionItemVisualResolver — 약초 5색 실GLB / **mat_wood → stake.glb([130차 임시 폴백])** / mat_stone → Rocks 프리팹(임시).
- **격차**: 현재 GLB는 "손끝" 부착 — 사용자 요구는 "플레이어 위쪽 부유(동물의 숲)". 1차 Play 로그에 [AcquisitionCinema] 발화 없음(당시 채집 미수행 — 미확정, Play 재검증 필요).

### 1-C 인벤토리 정합
- figma_audit_144/inventory.md: 요소 지오메트리(136차 기준선 — EquipmentBody 24,144 / BagViewport 24,390 / Row 간격 91.2 등) **전부 PASS** 실측됨.
- **격차**: 사용자 보고 "여백 면 미정합" — 감사가 창 **내부** 지오메트리였다면 **창의 화면 배치(앵커/여백)** 또는 런타임 USS 간섭(.utk-slot margin 상쇄 누락 등)이 감사 밖일 수 있음. Figma 렌더+소스 열수+런타임 3중 대조 필요(메모리 원칙 — 단정 금지).

### 1-D 건설 게이지
- 이미 구현됨: ConstructionManager.AttachSiteGauge [142차 F] — TryPlace 시 WorkProgressBar.ShowManual(수동 모드, 진행 대입) + [142차 F2] 부착 진단 로그. 베이크 PNG 존재 실측(Assets/Resources/UI/GatherBarTrack/Fill.png).
- **격차**: 142차 당시 "미표출 재보고" → F2 진단 추가 후 Play 실측 없음. 1차 Play 로그에 부착 로그 없음(당시 건설 미수행). 원인 후보: ①barY=pos.y+2.55가 구조물 메시에 가림 ②빌보딩/렌더 상태 문제 ③Build() 실패(머티리얼 null) — **Play 계측으로 확정 필요**.

### 1-E 음식점/마약 경영 — **재고 0종 = 실측 결함**
- 재고 시딩 존재: SetupTest15VillageDrugEconomy → VillageDrugState.AddStock 2건(일반 요리 20 + drugged_cook_01 30, 등록가 30) — 마을 키(nation, index) 기준.
- 표출: RestaurantWindowUTK "판매 가능한 음식" 그리드 — 재고 0이면 "해당 분류의 음식이 없습니다 (0종)" (RestaurantWindowUTK.cs L547).
- **사용자 실측 결함**: 재고가 뜨지 않음 — 시딩→VillageDrugState→창 표시 체인 중단점 진단 필요(후보: ①음식점리셋과의 순서/키 불일치 ②창이 읽는 소스와 시딩 소스 불일치 ③"등록" semantics). Y키 RunAllDaily도 "판매 담당 병사/재고 없음" 스킵 경로와 연결 확인.
- **기존 방문 시각화 발견 [100차]**: RestaurantVisitorSystem — 영업 중(8≤h<22)+재고 있음+마을 주민 일부 → VillagerWanderer.SetVisitTarget → 도착 시 PurchaseVisualized → 머리 위 코인 팝업(RestaurantCoinPopupOverlayUTK). ⚠ 연출 전용(재고/골드 미접촉 — 정산은 밤/틱 일괄 계약).
- **148차 상호작용**: Test_15 주민 0명(148차 제거) → 방문자 후보 없음. 전제 "재고 있음"도 미충족(위 결함).

## §2 Phase 계획

### P1 — 농경 아군 영지 게이트 (Test_15 전용)
- SetupTest15VillageDrugEconomy(또는 livelihood 경로)에서 농경 부지의 영지(East #1 — SpawnPlots 파라미터와 일치 확인 후)에 TerritoryBannerSystem.ChangeOwnership(..., isPlayerOwned: true) 호출 — 실제 프로덕션 경로 사용(직접 필드 조작 금지).
- 부작용 수용: 배너/가드 색상이 아군 표기로 바뀜(검증에 유리).
- 검증: FarmPlot이 "플레이어 소유 영지 — 경작 가능" 로그 + 밭 경작/성장/수확 E2E(Play) + 계약테스트(소유 시딩 소스계약 신설).

### P2 — 획득 연출: 얼굴 위 부유 GLB (전 종류 — 동물의 숲 스타일)
- **전 종류 확정(사용자 답변)**: 낚시 포함 모든 획득에서 아이템 GLB가 플레이어 얼굴 위에서 부유. 기존 "손끝 GLB"(AttachHandItem, 144차 P9-ACQ 계약)는 위쪽 부유로 **대체** — 계약테스트 갱신 승계.
- 부유 사양: 머리 위 오프셋(얼굴 위 — 클로즈업 카메라 frontView 프레임 안에 확실히 들어오도록), 부유 애니(상승/미세 진동+종료 페이드), CheerHoldSeconds 동안 유지. GLB 경로는 AcquisitionItemVisualResolver 재사용(신규 매핑 중복 금지).
- **"GLB 전체가 보이지 않음" 진단 병행**: 원인 후보 ①frontView 클로즈업 프레이밍에서 손끝 위치가 화면 밖 ②resolver 매핑 실패(폴백도 미표출?) ③AttachHandItem 자체 실패. 채집/벌목 성공 시 발화 로그+부착 결과 로그로 구분 후 수리 — 표시 위치가 위쪽으로 바뀌면서 대부분 해소 예상이나, 발화 자체 실패 여부는 로그로 검증.
- 검증: 계약테스트(부유 FX 소스계약+손끝 계약 갱신) + Play(약초=Test_15, 나무/돌=Test_20, 낚시=Test_20 호수).

### P3 — 인벤토리 창 화면 배치 수리 (확정: 창 위치 문제)
- 사용자 답변으로 범위 확정 — **창의 화면 배치(앵커/위치)**가 Figma와 불일치. 내부 갭은 144차 감사 PASS 실측이므로 재손대지 않음.
- 진단: 현재 InventoryWindowUTK 창 앵커/좌표 산출 경로(FigmaCanvasLayout.Place 적용 여부 — 다른 창 선례)와 Figma 인벤 프레임의 디자인공간 좌표 대조 → 차이 수치 확정.
- 수리: 다른 UTK 창의 디자인공간 배치 계약(창박스=figma×k·스케일 래퍼)과 동일 어휘로 앵커 교정. 기존 내부 지오메트리 계약(136차 기준선) 무손상.
- 검증: 계약테스트(창 배치 소스계약 갱신) + Play(스크린샷 비교).

### P4 — 건설 게이지 미표출 진단+수리
- 1차 진단: [142차 F2] 부착 로그(children/pos/barY) + 추가 계측(렌더러 enabled/머티리얼 null/빌보딩 각도/카메라 컬링) — U키 건설 1회 Play에서 수집.
- 수리는 원인 확정 후: 가림이면 게이지 y 상향/렌더 큐, Build 실패면 머티리얼 폴백, 진행 0 상태에서도 트랙 가시 보장.
- 기존 WorkProgressBar/142차 F 계약 무손상(신규 게이지 시스템 신설 금지).

### P5 — 음식점 재고 수리 + 고객 NPC 실구매 검증 (Test_15 전용)
1. **재고 0종 진단·수리(최우선)**: 시딩(L276/281 AddStock) → VillageDrugState → RestaurantWindowUTK 표시 체인을 로그/코드로 추적 — 리셋 순서·키 불일치·소스 불일치 중 원인 확정 후 수리. "음식이 없습니다"가 사라지고 일반/마약 음식 수량이 창에 보이는 것을 1차 목표로 함.
2. **고객 NPC 1명**: VillagerWanderer 유형으로 Test_15 전용 스폰(148차 주민 제거와 정합 — 이 1명은 고객 목적). 기존 RestaurantVisitorSystem의 방문자 선정 대상이 되도록 배치(신규 AI/이동 시스템 금지 — 100차 계약 재사용).
3. **실구매 검증 체계(계약 존중)**: 음식점 U키 건설 → 영업 시간대+재고 → 고객 NPC가 걸어와 구매 연출(코인 팝업) = RestaurantVisitorSystem 경로. **재고 차감/골드 적립은 기존 정산 경로(Y키 RunAllDaily/밤 틱)** — 100차 "정산 진실원천 불변" 계약 유지. 검증 시퀀스: 건설 → 방문 연출 관찰 → Y키 1회 → 재고 차감+pending 확인.
4. 검증: 계약테스트(고객 NPC 스폰+재고 체인 소스계약) + Play(U키 건설 → 방문/코인 팝업 → Y키 → 재고 차감).

## §3 실행 순서와 검증
- 순서: P1 → P2 → P5 (기능 신설) → P4/P3 (진단 선행 — Play 계측 필요 항목은 진단 로그 장착 후 Play에서 확정, 수리는 후속 커밋).
- 공통 검증: 컴파일 error CS 0 + 전수 EditMode baseline 10건 1:1 + 집중(신규/갱신 계약) + 범위 감사(화이트리스트).
- Play 확인 항목(사용자): 농경 경작/성장, 획득 위쪽 GLB 연출, 건설 게이지 표출, 음식점 U키 건설→고객 NPC 구매 액션, 인벤 여백.
- Play·커밋: 별도 승인 게이트.

## §4 결정 기록 (사용자 답변 확정 — 2026-11-03)
1. 인벤 여백 = **창의 화면 배치 문제** → P3 창 앵커 수리로 확정.
2. 획득 GLB = **전 종류**(낚시 포함) 얼굴 위 부유, 현재는 전혀 보이지 않음 → P2 전 종류 부유로 대체+진단 병행.
3. "음식·마약 인벤토리" = 음식점 재고 맞음 + **실측 결함(0종 표출)** → P5-1 재고 체인 진단·수리가 최우선.
