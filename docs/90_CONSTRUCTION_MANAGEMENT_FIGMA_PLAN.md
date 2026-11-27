# 90차 스펙 — 건설 관리 UI(Figma 239:683) + 크래프트 시설 설계도 + 음식점 실내 금지 + 병사 건설 지원

> 발행: 감독자 (2026-10-16). 위임 근거: docs/AGENT_DELEGATION_POLICY.md.
> Figma 실측(2026-10-16T03:36:43Z 개정): 신규 프레임 **239:683 '전초기지 건설 관리' 1600×1040**, 기존 **204:1963 '공격조 전투 배치' 1920×1080**(40차 AttackFormationUTK 구현 존재).

## 사용자 요구 (5건)
1. 각 크래프트(시설)도 건설 가능하게 — Figma 239:683 형태대로 로직+UI 재구현.
2. **음식점만 실내 배치 금지** (다른 시설은 실내 포함 배치 가능).
3. Figma 재료 표기 과다(60~120개) → **적당한 수량으로 로직 설계** (감독자 확정치 아래).
4. 병사 배치 UI(공격조 전투 배치 창)에 **'건물짓기'/'음식점 배치' 항목 추가**.
5. 병사가 플레이어와 함께 있을 때 **건설 자동 지원**.

## Part A — Systems (구현 1차)

### A1. 재료 시스템 확장 — BlueprintData/ConstructionManager
- `BlueprintDef` 신규 필드(기존 materialWoodCost/materialStoneCost 이후): `materialIronCost`(철광석), `materialLeatherCost`(가죽), `materialManaCost`(수정석) — 0 기본값(기존 8종 무영향).
- 재료 id 단일소스 상수(ConstructionManager): 기존 MaterialWoodItemId="mat_wood"/MaterialStoneItemId="mat_stone" + 신규 `MaterialIronItemId="iron_ore"`(철광석 — PlayerInventory 실측)/`MaterialLeatherItemId="mat_boar_leather"`/`MaterialManaItemId="crystal_shard"`.
- `HasEnoughMaterials` 기존 4인자 시그니처 유지(테스트 호환) + 8인자 오버로드 신설(나무/돌/철/가죽/수정석 + 필요 4종... 실제로는 보유5/필요5 → (wood, stone, iron, leather, mana, needWood, needStone, needIron, needLeather, needMana) 10인자. 규약 동일: 요구 ≤0 = 무관 충족).
- `TryPlace` 재료 소비/롤백 확장 — 기존 나무/돌 롤백 패턴(144~170행)에 철/가죽/수정석 동일 적용(부분 소비 금지 원칙 유지). 사전 합산 판정 → 성공 시에만 소비.

### A2. 신규 시설 설계도 4종 + 음식점 (재료 = 감독자 확정 설계 — 유저 위임)
| 설계도 id | 표시명 | 역할 문구(Figma) | footprint | 골드 | 재료 | 시공 |
|---|---|---|---|---|---|---|
| bp_cooking | 요리 크래프트 | 생산 · 식사와 회복 음식 제작 | 2.0 | 50 | 나무 8 · 돌 4 | 45s |
| bp_forge | 장비 크래프트 | 생산 · 무기와 방어구 제작 | 3.0 | 100 | 나무 12 · 돌 8 · 철 6 · 가죽 4 | 90s |
| bp_restaurant | 마을 음식점(기존) | 운영 · 음식 판매 및 수익 창출 | 2.5 | 200+허가세 | 나무 5 · 돌 3 (기존 계약 유지) | 60s |
| bp_barracks | 병사배치 | 방어 · 병력 주둔과 거점 수비 | 3.5 | 150 | 나무 10 · 돌 8 · 철 4 | 120s |
| bp_alchemy | 연금술 크래프트 | 생산 · 물약과 강화 재료 제작 | 2.0 | 120 | 나무 7 · 돌 5 · 수정석 2 | 75s |
- effectId: cooking_facility/forge/restaurant(기존)/barracks/alchemy_facility. description에 재료 반영.
- bp_restaurant 골드/재료/시공 기존값 유지(88차 D4 계약 — RestaurantConstructionTests 보호).

### A3. 완공 실물 + E키 시설 개방 — `Systems/FacilityPropBuilder.cs` (신규)
- RestaurantPropBuilder 패턴 준용(RuntimeInitializeOnLoadMethod 구독 전 해제/StructureCompleted/점유 캐시/국가 틴트/접지 GetHeightAt+1) — **bp_restaurant는 스킵**(기존 RestaurantPropBuilder 소유).
- 프리미티브 조합(결정론): 요리=모닥불 원+솥 실린더 / 장비=모루 박스+화덕 / 병사배치=막사 박스+지붕 / 연금술=탁자+플라스크 원통. BuildingPlaceholder(BuildingType.Other, 시설명) 부착.
- E키 상호작용(`FacilityStationInteractable` — RestaurantPropInteractable 패턴 준용): 프롬프트 "E: {시설명}" → 창 개방 — 요리=CookingWindowUTK.Open() / 장비=CraftingWindowUTK.Open() / 병사배치=AttackFormationUTK.Open() / 연금술=AlchemyBenchUTK.Open() (전부 Open() 실측 존재).
- Systems→UI 순환참조 회피: 개방 요청 정적 이벤트(FacilityOpenRequested(effectId)) + UI측 구독은 Part B 창이 담당(무구독 시 로그만). 단, Systems가 UI 어셈블리를 참조할 수 없으므로 실제 창 개방 라우팅은 UI 쪽 정적 구독으로(88차 RestaurantPropInteractable.OpenRequestedUTK 동일 정신).
- 세이브 복원: RestaurantPropBuilder.RebuildFromSave 선례 — ApplySaveEntries 훅에서 4종 복원(실물+상호작용).

### A4. 음식점 실내 배치 금지
- `ConstructionManager.TryPlace` 음식점 분기 최상단: `IsIndoorPlacement(pos)` → 거부 "음식점은 실내에 배치할 수 없습니다 — 마을에서 건설하세요".
- `IsIndoorPlacement`: 최근접 영지 중심(TerritoryDatabase)까지 거리 ≤ **성 내부 반경** — 실측값은 InteriorLayoutDefinition.CreateKeepOctagon 정의에서 산출(구현 서브가 실측 후 상수화, 예: keep 바닥 정점 최대 반경 — 근거 주석). 마을 반경(40m)보다 작은 값이어야 하며, 마을 배치(반경 40m)와 충돌하지 않도록 반경 ≤ 25m 권장.
- 음식점 마을 반경 밖 실패 메시지도 실내 맥락 포함 유지(기존 메시지 유지 — 추가 안내).

### A5. 병사 건설 자동 지원
- `ConstructionManager.Update` 시공 진행 중 구조물: 반경 12m 내 플레이어 소유 병사(GuardPlaceholder — IsRecruited && IsAlive, GetAllPlayerGuards 소비처 실측 준용) 수 N → **진행 배율 = 1 + 0.25×N (상한 2.0)**.
- 로그: 지원 시작/변화 시 1회성("병사 N명 건설 지원 — 배율 1.5"). 지원 상태 조회 API: `GetAssistMultiplier(structureId)` / `GetAssistCount(structureId)` (UI 표시용 public).
- 지원 문구: Figma '필요 작업 인원 1 명 · 유휴 인원 N명' — 유휴 인원 = 가용 병사 수(실측).

### A6. 테스트 (Phase A)
- `BlueprintMaterialTests.cs` (신규): ①5종 설계도 존재+재료 확정치 어서션(bp_cooking 나무8/돌4 등 위 표 값) ②HasEnoughMaterials 10인자 규약(0=무관) ③TryPlace 재료 소비/롤백 소스 어서션(철/가죽/수정석 포함) ④실내 금지 — IsIndoorPlacement 소스 + 거부 메시지 어서션 ⑤건설 지원 — 배율 1+0.25N/상한 2.0 소스 어서션 ⑥FacilityPropBuilder — 4종 실물+Open 라우팅(이벤트/프롬프트 문구) 어서션.
- 기존 테스트 충돌 시(ConstructionTests 등): [90차] 갱신(의미 보존).

## Part B — UI (구현 2차 — Phase A 컴파일 GREEN 후)

### B1. ConstructionWindowUTK 전면 재작성 (Figma 239:683 — 단일소스)
- 창 1600×1040, ApplyCanvasFit(k=min(rootW/1920,rootH/1080), 창박스=figma×k·내부 raw·scale=k), U키 토글/Esc 닫기 유지.
- **헤더 @(64,36)**: 전초기지(13/400 #8B949E) + '/' #586473 + '건설 관리'(13/500 #F0F6FC) | 우측 시각(N일차 · 낮/밤 + HH:MM — TimeManager 실측).
- **좌 '건설 목록' 480×882 @(64,78)** bg #13181F r14 stroke #30363D/0.5: 제목 blocks 23×23 + '건설 목록' 22/700 + '전초기지 시설' 11/400 | **필터 4탭 140×36 r6 gap6** @(88,166): '전체 5'(활성 #58A6FF/글자 #0B0E14 12/700)/'건설 가능 N'/'재료 부족 M'(비활성 #191F27·글자 #8B949E)/**'기타 8'(신규 보존 탭 — 기존 O8 설계도 8종, Figma에 없는 유저 기능 보존 요소 — 보고에 명시)**. 필터 카운트는 실측 동적.
- **시설 항목 432×101 r6** @(88,246) 세로 스택 gap8(246/355/464/573/682): 아이콘 36×36 #20262E r6 / 시설명 15/700 #F0F6FC + 역할 11/400 #8B949E / 상태 배지 76×21 r6(건설 가능=#58A6FF/0.078+check·글자 #58A6FF 11/500 / 재료 부족=#E5AD65/0.063+circle-alert·글자 #E5AD65 / 기타 항목=설치 여부 표기) / **재료 행 '나무 B/60 · 돌 B/40 · 철 B/N' 11/400 #8B949E** — 보유 B/필요 N 실측(Figma 목업 보유치 180/120/24/18/8 미채용 — 실측 인벤 수량). 선택 항목 = bg #1A2839 + stroke #58A6FF/1.5.
- **보유 재료 요약 432×110 @(88,826)**: '공용 창고' 11/700 #8B949E + '건설 전 보유량' 10/400 #586473 | 5칸 81.6×70 #0F141B r6 gap6: 나무/돌/철(철광석)/가죽/수정석 — 아이콘 12×12 + 재료명 10/400(실측 displayName) + 보유량 15/500 #F0F6FC.
- **우 '건설 정보' 968×882 @(568,78)**: 제목 hammer + '건설 정보' 22/700 + '선택한 시설의 재료 및 조건' + 닫기 30×30 | **선택 시설 개요 920×158 #191F27 r10**: 시설명 21/700 + 'Lv.1' 11/400 #58A6FF + 상태 배지 76×21, 설명 12/400 #8B949E, 효과 sparkles + '제작 해금' 문구 11/400 #58A6FF(Figma '기본 무기 · 가죽 방어구 제작 해금' 형식 — 시설별 해금 문구), 운영 조건 11/400('완공 후 E키로 시설을 사용하세요' 실측 문구 — Figma '제작 슬롯 2칸…' 목업치는 기타 시설 형태 참고), 설계도 박스 234×98 #0F141B r6 + 'FACILITY / LV.1' 8/400 + 부지 '{칸} × {칸}' 9/400 #58A6FF | **건설 조건 3카드 298.7×60 gap12**: 건설 시간 mm:ss 18/400 / 필요 부지 '{N} × {N} 칸' 18/400 / 필요 작업 인원 '1 명 · 유휴 인원 {N}명' 18/400(유휴=가용 병사 실측) | **필요 재료 표 920×288 r10**: 열헤더 920×32 #0F141B(열 x 606/966/1082/1198/1344 — 재료/보유량/필요량/건설 후 잔여/충족 상태) + 행 920×64(아이콘 32×32+이름 13/500, 보유 14/400, 필요 14/400 #8B949E, 잔여 13/400(부족 시 '—' #586473), 충족 check '충분' #58A6FF/circle-alert 'N개 부족' #E5AD65 — 부족 행 bg #E5AD65/0.063+stroke 0.22) | **건설 실행 920×106 @(592,796)** r10: 충족=bg #58A6FF/0.078+stroke #58A6FF/0.25 + '건설하기' 224×42 #58A6FF r6(글자 #0B0E14 13/700) / 부족=bg #E5AD65/0.063+stroke 0.22 + 사유 '{재료} {N}개가 더 필요합니다' 14/700 + 해결 방법 11/400 + '재료 획득처' 108×42 #58A6FF/0.078 r6(클릭→토스트 획득처 안내 — 채광/수렵/채집 실측 문구) + '건설 불가 · 재료 부족' 224×42 #252D38 r6 lock #586473 | 조건 확인 '✓ 부지 확보 ✓ 작업 인원 충분'/'! 재료 N종 부족' 10/400 | 유의사항 '건설 시작 시 재료가 즉시 차감됩니다…' 11/400 #8B949E.
- 도움말 @(64,984): '시설을 선택해 필요한 재료와 건설 조건을 확인하세요.' + '클릭 시설 선택 · Enter 건설 확인 · Esc 닫기' 12/400 #8B949E.
- **데이터 매핑(위조 금지)**: 보유량=PlayerInventory.GetItemCount 실측 5종, 충족 판정/건설 후 잔여/상태 배지 = HasEnoughMaterials 규약, 건설 시간/부지/작업 인원 = BlueprintDef+ConstructionManager 실측, 건설하기 = 기존 OnPlaceBlueprint 흐름(카메라 forward×5+ResolveTerritoryAt+TryPlace — 음식점은 마을/실내 검증 적용) → 반환 메시지 토스트/상태 표시. 배치 실행 직후 RefreshAll + 폴링 500ms(기존 RefreshInterval 계약 유지 가능).
- Enter = 선택 시설 건설 실행, Esc = 닫기.

### B2. AttackFormationUTK — 항목 2개 추가 (Figma 204:1963 기존 구현 보존 + 유저 직접 요구)
- 계획 도구 행(y139) 우측 — 구분선(#30363D/0.6 1×22) 후 **'건물짓기'/'음식점 배치' 버튼 2개**(UTKButton.Secondary, 높이 32, 글자 13/500 #8B949E — hover/활성 #F0F6FC):
  - '건물짓기' → ConstructionWindowUTK.Open() (신규 건설 관리 창).
  - '음식점 배치' → ConstructionWindowUTK.Open() 후 음식점 항목 자동 선택(선택 시설=bp_restaurant 표시).
  - Systems→UI 참조 불가 — AttackFormationUTK(UI 어셈블리)에서 ConstructionWindowUTK(UI) 직접 호출은 동일 어셈블리 OK.
- 기존 명부/배치도 로직 무변경 — 버튼만 추가.

### B3. 테스트
- `ConstructionManagementFigmaTests.cs` (신규): [90차] 마커 소스 어서션 — 1600×1040/480×882 @(64,78)/968×882 @(568,78)/필터 140×36/시설 항목 432×101/재료 요약 81.6×70/조건 298.7×60 gap12/재료 행 920×64·열 x 606·966·1082·1198·1344/실행 920×106·버튼 224×42·획득처 108×42/#13181F·#191F27·#0F141B·#20262E·#58A6FF·#E5AD65·#30363D/'건설 관리'/'건설 목록'/'건설 정보'/'필요 재료'/'건설하기'/'재료 획득처'/'건설 불가 · 재료 부족'/ApplyCanvasFit/기타 탭/'유휴 인원'(A5 실측 연계)/Enter-Esc.
- `AttackFormationButtonTests` — 기존 AttackFormation 테스트 파일에 추가 or 신규: '건물짓기'/'음식점 배치' 소스 어서션 + ConstructionWindowUTK.Open 호출.
- 기존 ConstructionWindowTests — 전면 갱신([90차]).

## 검증/증거 (공통)
- 컴파일: rm -f Temp/UnityLockfile && timeout 550 bash compile_test.sh → error CS 0.
- EditMode(QA): baseline 11 고정 실패 외 0. 89차 기준 1181/1170/11 대비 총계 보고.
- Play 확인(사용자 게이트): ①U키/병사 배치 UI '건물짓기' → 신규 건설 창(Figma 정합) ②시설 선택→재료 표 실측 ③재료 있으면 건설하기 → 실물 생성(E키 시설 개방) ④음식점: 마을 외(성 내부 포함) 배치 시 거부 메시지 ⑤병사 2명+ 시공 옆 → 진행 가속(로그) ⑥공격조 UI '음식점 배치' → 음식점 선택된 건설 창.

## 확정 사항(감독자 결정 — 보고 명시)
- 재료 수치 = 위 표(감독자 설계) — Figma 목업 수량(40~120) 미채용.
- '마력석' 라벨 → 실측 아이템 '수정석'(crystal_shard) 표기 — 원장 단일소스 우선.
- 필터 '기타 8' 탭 = 기존 O8 설계도(외벽/대문/감시탑/창고 증축/축사/대장간/정원/망루) 보존 — Figma 3탭에 추가된 요소(기능 회귀 방지).
- 기존 8종 설계도는 재료 0 유지(기존 골드 비용만) — 신규 재료 시스템 적용 대상 아님.
- 시설 '해금' = 시설 완공 시 해당 크래프트 창 E키 개방 스테이션 제공(기존 성 내부 스테이션 변경 없음 — 게이팅 추가 안 함, 최소검증 설계).
