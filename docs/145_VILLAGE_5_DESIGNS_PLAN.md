# 145차 계획 — 마을 설계도 5종 기반 5마을 디자인 + 마을 GLB 46종 전수 배치 (계획 전용 — 승인 대기)

작성: 2026-11-01. 사용자 요청: Screenshots/마을 설계도 1~5.png + Assets/새로운 glb/마을 GLB/ 46종을 기반으로
마을 5개를 설계·배치. **계획만 — 구현/컴파일/테스트 미실행.**

## §0 사용자 요청 이해 (재확인용)

1. **입력 자산**: ①마을 설계도 1~5.png(5장 — 각각 별개의 마을 설계도, 한국어 라벨 포함 아이소메트릭 일러스트)
   ②`Assets/새로운 glb/마을 GLB/` 신규 46종(11-01 00:13 유입, 영문명 — tavern/bakery/blacksmith/potion shop/church/
   clock tower/windmill/watermill/gatehouse/dock/well/fountain/gazebo/mushroom house/tree house/cottages 등).
2. **설계도의 역할**: "설계도대로 만들 필요는 없다" — 정확 복제가 아니라 **지형/구역 성격 파악용 참조**.
   강·호수·언덕·숲·성벽 같은 지형 특징과 구역(상업/생산/주거/종교) 구성을 읽어내서 각 마을의 테마로 삼는다.
3. **배치 규칙 (사용자 명시)**:
   - 마을 설계 **5개**.
   - 마을 GLB 건물 **46종 전수 배치** — 한 건물도 빠지면 안 됨.
   - 한 마을에 모든 건물을 넣을 필요 없음 — 마을마다 **적당히(부분집합) 배분**.
   - 각 마을이 설계도 지형 성격에 맞게 **자연스러운 배치** (과밀 배제 — 기존 설계 선호 계약).
4. **산출물**: 5마을 디자인(마을별 지형 성격 + 건물 배치). 승인 게이트·검증은 기존 계약 준용.

## §1 자산 실측 (2026-11-01)

### 1-1 설계도 5종 판독 요약 (vision 실측)

| # | 테마 | 지형 특징 | 핵심 구역 |
|---|---|---|---|
| 1 | 강변 자급자족 복합 마을 | 은빛 강(동), 호수+신비 신전(북서), 숲(북), 언덕(서) | 성문/상업(남) — 광장+고목(중앙) — 주거(서) — 생산·부두(동) — 벌목·광산(북) |
| 2 | 강과 종교의 교역 마을 | 굽이진 강(중앙 종단)+다리 2, 동굴 입구 2, 고목/소원나무(서) | 중앙 광장(분수대) — 상업(중앙) — 생산(과수원/풍차/부두) — 종교(성당 2곳, 동) |
| 3 | 광산·군사 생산 기지 | 바위산+광산(북), 시냇물(동), 연못 2, 침엽수림 둘레 | 벌목장·병영(북서) — 광산(북) — 농장·울타리(북동) — 광장(중앙) — 마구간·용광로(남) — 수원나무·과수원(동) |
| 4 | 성벽 요새 행정 도시 | 육각 성벽+감시탑+성문 2, 강(우하단)+석다리 | 군사 본부·훈련장·병영(좌) — 시청·주거·금고(중앙) — 연금술·창고·방앗간·과수원(우) |
| 5 | 숲속 휴양·약초 마을 | 강(중앙 수직)+다리 3, 언덕(북/동, 성당·트리하우스 고지) | 상업 광장(좌) — 성당(북 고지) — 주거·트리하우스(우) — 휴식·허브 정원(중앙/하단) |

### 1-2 마을 GLB 46종 (마을 GLB/ 실측, 46파일)

건물류 30종: Earthen Hill House · Fantasy Bakery · Fantasy Blacksmith Forge · Fantasy Potion Shop ·
Fantasy Round House · Fantasy Tree House · Fantasy Mushroom House ×2 · Fantasy Crystal Gazebo ·
Grey Stylized Church · Isometric Bakery Shop · Medieval Cartoon Blacksmith · Medieval Clock Tower ·
Medieval Cottage · Medieval Stone Gatehouse · Medieval Timber Cottage · Purple Wizard Tower ·
Stylized Bell Tower ×3(파일 3개 — 동일 프리팹 중복 포함 1건) · Stylized Fantasy Tavern ·
Stylized General Store · Stylized Post Office · Stylized Tudor House · Stone Watermill House ·
Stone Wither... (Stone Wishing Well) · Stone Fountain Plaza · Stone Cave Entrance ·
Stylized Wooden Windmill · Thatched Roof Cottage · Whimsical Fantasy Village(복합 세트) ·
Wooden Farm Barn · Wooden Garden Greenhouse · Stone Arch Bell

소품/조경류 16종: Campfire Cooking Pot · Farm Animal Diorama · Fenced Vegetable Garden ·
Horse Stable Diorama · Mossy Rock Benches · Mossy Stone Pillar · Stone Knight Statue ·
Stone Wishing Well · Stone Arch Bell · Treasure Chest Gold Gems · Wooden Dock · Wooden Dock Fishing Rod ·
Wooden Produce Cart · Wooden Signpost · Stylized Wooden Dock · Farm Animal Diorama

⚠ 실측 주의: 파일명에서 파생한 대분류일 뿐 — **P0에서 GLB 파서로 실제 바운드/스케일/구성 전수 검증** 후
역할 분류표 확정(파서 = references/fbx-glb-asset-validation 선례). "Stylized Bell Tower (1).glb" 중복 파일 처리
(동일 에셋이면 1종으로 간주 — 나머지 1개는 다른 마을에 재배치 허용).

### 1-3 기존 시스템 (재사용 대상)

- `VillageBuilder`(Systems): 24마을(4국가×6) 절차 배치 — 결정론 시드(SeedBase 9130), GetHeightAt+GROUND_BASE(1)
  접지, MinSpacing 6m, 광장 반경 6/4m, [ExecuteInEditMode] 가드(Play에서만 생성).
- `VillageBuildingCatalog`: `Assets/새로운 glb/건물/` 23종 역할 풀(집 11/부자집 6/쉼터 3/상점 3)+목표높이 정규화
  (House 4.5/Rich 6.0/Shop 5.2/Shelter 4.0m)+결정론 인덱스 매핑. **에디터 AssetDatabase 직접 로드 + 프리미티브 폴백**.
- fence: `새로운 glb/fence/` 스플릿 로그 펜스(1.1m) [86차].
- 호수: Test_15 선례(원판 r12) — 강/호수 시각 재사용 후보.
- 농경: FarmPlateOverlayUTK 8상태 체계(26차) — 농경지 GLB와의 연계는 기능 배선 항목(§5 Q3).

## §2 마을 5종 설계안 (테마 + 건물 배분 초안)

> 배분 원칙: ①46종 전수(중복 파일 1건 제외 45종 유효 — 각 1회 이상) ②랜드마크급(성당/시계탑/마법사 탑/성문/
> 수차 방앗간)은 마을당 1개 ③소품(벤치/이정표/우물/분수/석상/카트/화롯발)은 복수 마을 재사용 허용(현실적 마을
> 구성 — 사용자 "나무수준" 배분 선호와 정합) ④각 마을 = 설계도 테마와 어울리는 부분집합.
> 좌표는 P1 카탈로그에서 확정(하드코딩 — 동굴 17-각형 선례, Random 금지).

### 마을 1 — 강변 자급자족 마을 (설계도 1)
- 지형: 동쪽 강 + 북서 호수 + 북쪽 숲 경계 + 서쪽 완만 언덕.
- 배치: Medieval Stone Gatehouse(남 성문) · Stone Fountain Plaza(중앙 광장) · Wooden Signpost(성문 앞) ·
  Medieval Timber Cottage + Medieval Cottage + Thatched Roof Cottage(서 주거 언덕) · Fantasy Crystal Gazebo
  (북서 호수변 "신비한 신전" 역할) · Purple Wizard Tower(동 강가 "연금술사의 탑") · Stone Watermill House(강가
  방앗간) · Wooden Farm Barn + Fenced Vegetable Garden + Farm Animal Diorama(동 농장) · Stylized Wooden Dock +
  Wooden Dock Fishing Rod(강 하류 부두/낚시터) · Stone Cave Entrance(북동 광산 입구) · Mossy Rock Benches(광장).

### 마을 2 — 종교·교역 마을 (설계도 2)
- 지형: 중앙 종단 강 + 다리 2 + 서쪽 고목 숲.
- 배치: Grey Stylized Church + Stylized Bell Tower 2개(동쪽 성당 지구 — 설계도 "성당 2곳") · Stone Fountain Plaza
  (중앙 광장 분수대) · Stylized General Store(잡화점) · Isometric Bakery Shop + Fantasy Bakery(빵집 2곳) ·
  Fantasy Potion Shop(물약 상점) · Stylized Fantasy Tavern(여관) · Stone Wishing Well(소원 나무 자리) ·
  Stylized Wooden Windmill(풍차) · Wooden Farm Barn + Fenced Vegetable Garden(과수원/농장) ·
  Medieval Cottage ×2 + Thatched Roof Cottage(주거) · Stone Arch Bell(교회 앞).

### 마을 3 — 광산·군사 생산 기지 (설계도 3)
- 지형: 북쪽 바위산 + 시냇물(동) + 연못 2 + 침엽수 숲 둘레.
- 배치: Stone Cave Entrance(북 광산 입구 — 기존 동굴 시스템과 연계 후보) · Fantasy Blacksmith Forge +
  Medieval Cartoon Blacksmith(남 용광로/무기 제작소 — 대장간 계약 연계 후보) · Horse Stable Diorama +
  Farm Animal Diorama(마구간/동물 울타리) · Wooden Farm Barn(창고) · Mossy Stone Pillar + Stone Knight Statue
  (병영 훈련장 장식 — 병영 본체는 기존 건물 풀 사용 검토) · Wooden Produce Cart(물류) · Treasure Chest Gold Gems
  (물자 보관 연출) · Earthen Hill House + Fantasy Round House(주거) · Wooden Garden Greenhouse(과수원 온실) ·
  Campfire Cooking Pot(광장 취사) · Wooden Signpost.

### 마을 4 — 성벽 요새 행정 도시 (설계도 4)
- 지형: 평탄 + 성벽(기존 성 시스템/울타리 조합 검토) + 우하단 강.
- 배치: Medieval Stone Gatehouse(남 성문) · Stylized Post Office(시청 행정) · Stylized Tudor House ×2(주거 밀집) ·
  Stylized General Store(잡화) · Treasure Chest Gold Gems(왕실 금고 연출) · Fantasy Potion Shop +
  Wooden Garden Greenhouse(허브 연금술 작업실) · Wooden Farm Barn(곡물 창고) · Stone Watermill House(강가
  방앗간) · Fenced Vegetable Garden(과수원) · Stylized Bell Tower(감시탑 겸 종탑) · Mossy Rock Benches ·
  Stone Knight Statue(광장).

### 마을 5 — 숲속 휴양·약초 마을 (설계도 5)
- 지형: 중앙 수직 강 + 다리 3 + 북/동 언덕(고지).
- 배치: Fantasy Tree House(동 절벽 트리하우스) · Fantasy Mushroom House ×2(버섯 주택 — 2동 모두 소진) ·
  Earthen Hill House + Fantasy Round House(언덕 돌집/통나무집) · Grey Stylized Church 또는 Stylized Bell Tower
  (북 고지 성당 — 마을2와의 중복 배분 P1에서 확정) · Fantasy Potion Shop(약방) · Stone Fountain Plaza +
  Mossy Rock Benches(휴식 공간) · Campfire Cooking Pot(취사) · Wooden Garden Greenhouse + Fenced Vegetable
  Garden(꽃밭 허브 정원) · Wooden Signpost(입구) · Thatched Roof Cottage(코티지).

### 배분 검산 (초안 기준)
- 45종 유효 전수 여부: P1 카탈로그 확정 시 체크리스트로 검산(마을×건물 매트릭스) — 누락 0 목표.
- 랜드마크 중복 조정: Church/Gazebo/Bell Tower/Gatehouse/Watermill 등은 위 초안에서 마을2·5가 겹치는 항목 존재
  → P1에서 45종 매트릭스로 최종 확정(중복 허용 개수 = 소품류만).
- Whimsical Fantasy Village(마을 전체 미니어처 세트)는 단독 배치 어색할 수 있음 → 마을 1 광장 외곽 "기념물/디오라마"
  로 배치하거나, 파서 검증 결과가 마을 세트 형태면 **생략 후보로 기록 후 사용자 판정** (위조 배치 금지).

## §3 구현 방식 (기존 계약 준수)

1. **결정론 하드코딩**: 마을 5개의 건물 배치표(로컬 좌표/회전/목표높이)를 신규 카탈로그
   `Assets/Scripts/Systems/Village5DesignCatalog.cs`에 하드코딩(동굴 17-각형 정점 선례 — Random 0건).
   접지 = VillageBuilder.GetHeightAt+GROUND_BASE 재사용, MinSpacing 6m 위반 0 검증.
2. **렌더러**: `Village5DesignRenderer`(신규 — VillageBuilder 패턴 준용, 에디터 AssetDatabase 로드+프리미티브 폴백,
   렌더러 bounds 높이 정규화, 중복 생성 가드). 기존 24마을/건물 풀(VillageBuildingCatalog) **무수정** — 병행 운영.
3. **지형 반영 수준**: 설계도 지형은 "테마 적응" — 기존 절차 지형 위에 배치하며, 호수/시냇물은 기존 물 원판 선례로
   국소 반영, 성벽/다리는 대응 GLB가 없어 신규 조형 금지(위조 방지) → **생략 + 기록** (사용자가 원하면 별도 에셋
   요청). 지형 변형(heightmap 수정)은 이번 사이클 미포함(회귀 위험 — 별도 승인).
4. **테스트 씬 우선**: Test_22 마을설계 씬 신설 — 5마을 플롯(간격 충분, 예: 120m 피치)을 나란히 배치, 플레이어
   스폰+마을 간 이동 텔레포트(기존 텔레포트 계약 경유). NPC/병사 스폰 없음(시각 검증 전용 — 기존 NPC 배회 계약
   충돌 방지). 씬 기능 연속성 계약(61차) 준수.
5. **국가색/영지 계약**: 5마을은 우선 "디자인 쇼케이스" — 프로덕션 영지 귀속 시 국가색 유지 계약(설계 선호)과
   링 난이도 계약(링 무수정) 적용은 §5 Q2 승인 후.

## §4 실행 단계 (승인 후)

- **P0 자산 검증(배치모드)**: 마을 GLB 46종 파서 검증(바운드/메시/머티리얼/텍스처 — 고품질 계약 141차 준수 확인)
  → 스케일 표 + 역할 분류표 확정 + 중복 파일 판정. 산출: TestOutput/village5_glb_audit.txt.
- **P1 카탈로그 확정**: Village5DesignCatalog — 마을 5종 배치표(좌표/회전/높이) + 45종 전수 매트릭스 검산
  (누락 0) + MinSpacing 검산. 설계도 테마 주석으로 근거 기록(설계도 N 참조).
- **P2 렌더러 + 테스트 씬**: Village5DesignRenderer + Test_22 씬 구성 + 접지/간격/중복 가드 테스트
  (EditMode 계약테스트 — 배치표 결정론/전수/간격 어서션).
- **P3 검증**: 컴파일 CS 0 + EditMode baseline 1:1(신규 0) + 배치모드 스크린샷 선검증(마을별 1컷 ×5 —
  vision 판독, 건물 누락/침하/과밀 판정) → 사용자 재Play 판정.
- **P4(별도 승인)**: 프로덕션 마을 반영 — 24마을 중 5마을 선정·교체, 기능 배선(상점/포션/교회 상호작용 등),
  NPC 배회 경로 정합.

## §5 승인 게이트 / 사용자 결정 항목

- **Q1**: 검증 씬(Test_22) 우선 — 기본안(테스트씬 선호 계약). 바로 프로덕션 반영을 원하면 지정.
- **Q2**: 프로덕션 반영 시 5마을 선정 기준(예: 대표 마을 4곳+1, 국가별 1곳+1) — P4 승인 시.
- **Q3**: 신규 건물의 기능 배선(약방→물약, 용광로→대장간, 교회→?) — 이번 사이클은 배치/시각만, 배선은 별도 승인.
- **Q4**: 성벽/다리 등 GLB에 없는 설계도 요소 — 생략+기록(기본) 또는 별도 에셋 요청.
- Play·커밋은 상시 별도 승인.

## §6 고도차(고산) 변형 — 5종 추가 (사용자 요청 2026-11-01 2차)

사용자 추가 요청: "지형 고도를 다르게 해서 고산 지형으로도, 밑에 호수가 지나고 다리를 건너면 마을이 형성되는
방식으로 5개 추가 제작 가능한가" → **가능 판정 (실측 근거)**:

- **지형 레버**: `TerrainHeightApplier`(Systems) = 런타임 절차 heightmap **메시**(Perlin FBM, 201×201/2000m 선례)
  — Unity Terrain 에셋이 아니므로 고도 프로파일 자유 설계 가능. 높이 조회 = 캐시 2D 배열(GetHeightAt 계열) →
  **메시 생성 함수와 접지 샘플러가 같은 함수를 공유하면** 고원 위 건물 침하 0.
- **물 레버**: 호수 원판 선례(Test_15 r12) — 고도차 시나리오는 **수면 y를 분지 바닥과 고원 상면 사이에 고정**.
- **다리 레버**: 마을 GLB 46종에 다리 없음(실측) → 위조 금지 원칙상 두 경로: ①**절차 목교**(데크 박스+난간 기둥 —
  동굴 배면벽 박스/지면 패치 확립 선례의 2D 확장, 치수 결정론 하드코딩) ②**Wooden Dock GLB 교각 세그먼트 재활용**
  (부두=판자 통로 구조 — P0 바운드 실측 후 경로 확정). 기본안 = ①(치수 제어 자유), GLB 재활용은 검증 후 선택.

### 6-1 고산 5종 설계안 (표준 5종과 별개 세트 — 총 10마을)

공통 구조: **분지(저지)에 호수 + 고원(고지)에 마을 + 다리로 접속**. 건물 배분은 §2의 45종 전수 원칙 그대로
(고산 세트는 표준 세트와 **같은 46종을 재사용** — 세트별 재배치, 중복 허용은 소품류만. 두 세트가 한 마을 GLB를
"전부 써야 하는" 의무는 공유).

1. **호수반도 고지 마을**: 3면이 호수로 둘러싸인 고원 반도, 단일 목교로만 진입. Gatehouse를 교묘 끝에 — "성문을
   건너야 마을" 구조. 주거+상점 소규모(과밀 배제).
2. **협곡 다리 마을**: 고원 두 개가 협곡(하부 강/호수)으로 갈라짐 — 큰 돌교 1 + 옆 나무교 1. 광산 입구(Cave
   Entrance)가 반대편 고원에 → "다리를 건너 광산 출근" 동선. 대장간/마구간 배치(생산 기지 고산판).
3. **단애 테라스 마을**: 계단식 테라스 2~3단 — 하단 호수변 부두/농경, 중단 주거, 상단 성당/종탑. 다리 1 + 급경사
   구간은 경사 완화(보행 가능 기울기 ≤ ~25° 유지 — 이동 계약 보호).
4. **호수 섬 마을**: 호수 한가운데 섬 고원, 긴 부두형 목교 1로 접속. Fishing dock/부두/낚시 관련 소품 집중 배치
   — 어촌 고산판. 주거는 균등 소형.
5. **산성 고원 마을**: 가장 높은 고원(성벽 없는 산성) — 성문+감시탑(종탑)+기사 석상, 분지 호수는 전망 대상.
   접근 = 스위치백 경사로+교량 1. 행정/요새 성격(설계도 4 고산판).

### 6-2 기술 구성

- **독립 미니 지형**: 고산 5종은 각 플롯이 **자체 heightmap 메시**(분지+고원 프로파일 — 가우시안 고원/분지 함수
  합성, 파라미터 하드코딩 결정론)를 가짐 — 공용 월드 지형과 분리(회귀 0), 표준 5종 플롯과 같은 테스트 씬에 배치.
  프로파일 함수 = 렌더 메시와 접지 샘플러 단일소스 공유.
- **수면**: 분지 수면 y 고정(고원 상면보다 낮고 분지 바닥보다 높음) — 호수 원판 스케일 조정 선례.
- **보행성**: 경사로 최대 기울기 제약을 프로파일 파라미터에 명시(플레이어 이동 계약 — 경사 통과 실측 항목).
- **접지**: 건물/소품 전부 프로파일 샘플러로 접지 — GROUND_BASE 상속 아님(플롯 로컬 기준), MinSpacing 6m 유지.

### 6-3 계획 반영 (§4 단계 수정)

- P0: GLB 검증에 **다리 후보(Wooden Dock) 바운드 실측** 추가.
- P1: 카탈로그에 표준 5종 + **고산 5종 배치표** 추가(프로파일 파라미터 포함) — 45종 전수 매트릭스는 세트별 검산.
- P2: 렌더러에 **미니 지형 생성기(고산 프로파일)** + 절차 목교 빌더 추가.
- P3: 스크린샷 선검증 10컷(마을별 1컷) — 고산 5종은 "호수가 분지에 보이는지/다리가 물을 건너는지/고원 침하 없음"
  판정 항목 추가.

## §7 호수/길 = Idyllic 에셋 + Test_15 검증 지형 (사용자 지정 2026-11-01 3차)

사용자 지정: ①호수 = Idyllic 에셋 활용 ②사람이 다니는 길 = Idyllic 흙길로 표현 ③**Test_15 씬을 고산 마을
검증 씬으로** — 메인씬과 동일한 Idyllic 에셋으로 지형을 구성해 실적용 확인(15씬만 확인).

### 7-1 실측 — 메인씬 Idyllic 지형 스택 (신규 제작 불필요, 재사용)

- **호수**: `LakeGenerator` — Perlin 임계 샘플링 수면 + **Idyllic Water 텍스처 플로우**(WaterBody 웨이브 스타일
  공유) + 수변 Shore 프리팹(Cattail/Reeds ×다수 — `Resources/IdyllicPrefabs/Shore/`). 고산 시나리오 = 분지 바닥과
  고원 상면 사이에 수면 y 고정 + 수변 갈대 밴드.
- **흙길**: `TerrainPathGenerator` — DirtRoadMask/정점색 경로 시스템 + `TerrainLayerDef` L4 dirt_path(Idyllic
  텍스처 textures_idyllic 폴더, 흙길 좌표 = 형태와 색 같은 데이터 단일소스). 고산 마을 접근로/광장 보행로를 같은
  시스템으로 배선 — 메인씬과 동일 어휘.
- **지형 스플랫**: Baker + TerrainLayerDef 5레이어(국가당) — 메인씬과 동일 Idyllic 텍스처 구성.

### 7-2 Test_15 고산 검증 씬 구성 (기본안)

- Test_15의 지면을 **메인씬 지형 스택 축소판**(Idyllic 스플랫 + LakeGenerator 호수 + TerrainPathGenerator 흙길)으로
  교체하고, 그 위에 **고산 설계 1종(§6-1에서 선정 — 기본안: 1번 호수반도 고지 마을)**을 실배치:
  분지 호수(수변 갈대) → 목교 → 고원 마을(마을 GLB 건물+흙길 보행로).
- 기존 Test_15 생활기술 클러스터(호수/밭/약초/나무/돌 — 142차 C2 마을가장자리 앵커)는 **폐기하지 않고** 고원
  마을 주변으로 앵커 이동(계약 유지 — 관련 EditMode 계약 파손 방지). 충돌 시 계약테스트 갱신은 94차 규칙.
- 고산 5종 전체는 §6대로 별도 쇼케이스 플롯으로, **15씬에는 대표 1종만** 먼저 실적용 — 사용자가 15씬만 Play해서
  "고도차+호수+다리+흙길+건물 배치" 전 파이프라인을 실게임 환경에서 판정.
- 다리 = 절차 목교(§6 레버 ①) — 흙길이 다리 양끝과 연결되도록 TerrainPathGenerator 좌표에 다리 진입점 포함.

### 7-3 단계 반영 (§4 수정)

- P0: GLB 검증(기존) + LakeGenerator/PathGenerator의 고산 플롯 적용 파라미터 실측.
- P1: 카탈로그(표준 5+고산 5) + **Test_15 고산 플롯 지형 파라미터**(분지/고원/수면/흙길 경로 — 하드코딩 결정론).
- P2: Test_15 지형 교체+고산 마을 1종 실배치(생활기술 앵커 이동 포함) + 쇼케이스 플롯 렌더러.
- P3: 검증 — 컴파일 0 + EditMode baseline 1:1 + 배치모드 스크린샷(15씬 고산 마을 포함) → **사용자 15씬 Play
  판정**(고도차/호수/다리/흙길/건물 접지 실감 확인) → 승인 후 나머지 9마을(표준 5+고산 4) 쇼케이스/프로덕션 진행.
