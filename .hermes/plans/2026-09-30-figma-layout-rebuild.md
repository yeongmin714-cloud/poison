# Figma 그리드·레이아웃 재구현 계획 (인벤토리/창고/전리품/상태/장비 클러스터)

> 작성: 2026-09-30 (Figma 실측 기준). 사용자 방향: "피그마 그리드/레이아웃을 단일 기준으로 삼고, 그에 맞게 게임 로직(드래그드롭 등)을 다시 구현. 나머지 창들도 마찬가지로 피그마 레이아웃을 따라가고 클릭/로직을 따라간다."
> 이 문서는 선행 조사(Figma 노드트리 + 렌더 vision) 결과와 창별 재구현 스코프를 확정한다. 구현은 별도 "진행" 승인 후 부모 직접으로 수행.

## 0. 원칙 (사용자 확정, figma-logic-rebuild-examples 스킬과 동일)
- **Figma = 단일 소스. 게임 로직을 Figma 레이아웃에 맞춘다.** (기존 "로직 보존 + 레이아웃 얹기"가 아니라, Figma 규격을 기준으로 로직/배치/동작을 재구성)
- Figma에서 확인되지 않는 치수/동작 = 단정 금지, 확인 후 진행.
- 대형 UTK .cs 재구성은 서브에이전트 위임 실패(600s)→ **부모 직접** 재작성 + 컴파일 0 + EditMode 회귀 0 순으로.
- Play 검증 전 완료 선언 금지.

## 1. Figma 실측 요약 (2026-09-30 직접 추출)

### 1.1 방식
- Figma 파일 `02vPXOGzFJUUYTaEPSVz3R`, 토큰 `~/.hermes/figma_token`.
- `/v1/files` 노드트리 → 프레임 id 확인 → `/v1/images` 렌더(scale 2) → vision_analyze.

### 1.2 프레임 인벤토리 (최상위 FRAME)
| 프레임 | id | 비고 |
|:--|:--|:--|
| fluent-game-inventory-ui | 15:4 | **인벤+상세+창고 3패널 클러스터** |
| loot-panel | 70:4 | 전리품 5×2 그리드 |
| character-status-panel | 83:4 | 상태창 |
| equipment-inventory-panel | 84:4 | 장비/인벤 |
| game-shop-ui | 74:4(+76:7) | 상점 |
| quest-window-ui | 93:230 | 퀘스트 3열 |
| soldier-management-ui | 69:4 | 병사관리 3열 |
| daily-combat-log | 94:6 | 전투로그 카드 |
| crafting-panel | 63:5·64:2·64:337 | 크래프트 |
| interaction-panel | 81:4~82:45 등 | 인터랙션 |
| medieval-world-map-ui | 95:167 | 월드맵 |
| monster-health-bar | 101:5 | 몬스터HP |
| potion-status-panel | 173:4 | **가스 분사기 포션 UI** |
| fishing-panel | 160:4 | 낚시 |

### 1.3 fluent-game-inventory-ui (15:4) — 핵심 실측
하나의 캔버스(1440×900) 안에 **3개의 독립 top-level 패널**이 나란히 배치 (별도 창 분리가 아니라 "한 장면 안에 3패널"):
```
[FRAME] fluent-game-inventory-ui (1440×900)
 ├─ InventoryPanel (좌 420×760)
 │    ├─ PanelHeader (제목+CloseBtn)
 │    ├─ TabContainer (5개 탭)
 │    ├─ EquippedSection (장비 2행 그리드)  ← 장착 아이템 EQ 태그
 │    ├─ Divider
 │    ├─ InventoryGridSection (GridRow 1~5) ← 가방 5열 그리드
 │    └─ InventoryFooter (WeightRow+ProgressBar)
 ├─ DetailPanel (중앙 480×608)
 │    ├─ PanelHeader
 │    ├─ ItemNameSection / ItemImageSection / DescriptionSection
 ├─ StoragePanel (우 420×760)
 │    ├─ PanelHeader
 │    ├─ StorageStats (BadgeItem+SlotCapacity)
 │    ├─ StorageGrid (GridRow 1~6) ← 창고 5열(6행) 그리드
 │    └─ StorageFooter (ActionButtonGroup = 전부 보관/꺼내기)
```
- **가방 그리드**: 5열 (행 수는 아이템 수에 따라 확장)
- **장비**: 5열×2행 (1행: 투구/갑옷/신발/방패/무기 / 2행: 방독면/분사기/물약/반지/목걸이), 장착 아이템에 파란 `EQ` 태그
- **탭 5개**: 전체/무기/방어구/소모품/재료
- **창고**: 5열, 하단 "전부 보관하기/전부 꺼내기" 버튼
- **중앙 DetailPanel**: 선택 아이템 상세 (이름/이미지/설명)
- **InventoryFooter**: 적재량 무게(kg) 프로그레스바

### 1.4 loot-panel (70:4) — 실측
- **5열 × 2행 = 10슬롯** 그리드
- 헤더: "전리품 / LOOT SECURED" + X 닫기
- 서브헤더: 좌 "습득 가능"(파란 강조) + 우 "획득 아이템: 3/10"
- 풋터: 좌 "전부 습득하기"(파란 Primary) / 우 "닫기"(회색)
- 슬롯: 중앙 흰 라인 아이콘 + 우하단 카운트 + **등급색 = 윗면(top edge)만** / 빈 슬롯 = 점선 가이드
- 현재 게임 `LootWindowUTK`는 **단일 열 리스트** → Figma 5열 그리드로 재구성이 필요 (G2에서 이미 5열 그리드로 시도했으나 행/등급/서브헤더 정합은 추가 확인 필요)

## 2. 현재 게임 vs Figma 갭
| 창 | Figma 요구 | 현재 게임 | 갭 |
|:--|:--|:--|:--|
| InventoryWindowUTK | 3패널 클러스터(인벤+상세+창고 한 장면) | UTKThreeColumnLayout으로 인벤(좌)/설명(중)/창고(우) **별도 창**으로 분산 | Figma는 Same-canvas 3패널. 배치 규칙 재검토 |
| |- 가방 5열 | 5열 (이미) | ✅ |
| |- 장비 5×2 + EQ 태그 | 5×2, EQ 태그 표시 없음 | EQ 태그 추가 + 장비 슬롯 2행 |
| |- 탭 5개 | 5열 그리드, 탭 필터 유무 확인 | 캐테고리 탭(전체/무기/방어구/소모품/재료) |
| |- 적재량 WeightBar | 현재 HUD/창에 없음 | InventoryFooter 추가 |
| |- 창고 전부 보관/꺼내기 | `WarehouseWindowUTK`에 없음 | ActionButtonGroup 추가 |
| LootWindowUTK | 5×2 그리드 + 윗면 등급 + 서브헤더 | 단일 열 리스트(또는 5열 시도) | 그리드/등급/서브헤더 정합 |

## 3. 재구현 스코프 (승인 대기)
**Phase A — 인벤토리 클러스터 (3패널)**
1. `InventoryWindowUTK` = InventoryPanel: 5탭 필터 + 장비 5×2(EQ 태그) + 가방 5열 + WeightBar
2. `ItemDescriptionWindowUTK` = DetailPanel (중앙, 이름/이미지/설명)
3. `WarehouseWindowUTK` = StoragePanel (우, 5열 + 전부 보관/꺼내기 버튼)
4. 배치: Figma "한 장면 3패널"을 따를지 vs 현재 독립 창 3열 유지할지 — **Figma 캔버스 배치(좌/중앙/우)를 준수**하되 각각 독립 최상위 창으로 유지(사용자 "별도 최상위 창" 기존 지시). 실제 열림 상태(전리품 뜰 때 등)와 Figma를 대조해 확정.

**Phase B — 주변 다중창**
5. `LootWindowUTK`: 5×2 그리드 + 윗면 등급 선 + 서브헤더("습득가능 n/10") + 전부습득/닫기
6. `StatusWindowUTK`(character-status-panel)·`EquipmentWindowUTK`(equipment-inventory-panel): Figma 구조 대조·재배열

**Phase C — 나머지 Figma 배킹 창 전수 재대조**
7. 상점(game-shop-ui)·재련/요리(crafting-panel)·퀘스트(quest-window-ui)·병사(soldier-management-ui)·상태(character-status-panel)·월드맵(medieval-world-map-ui)·전투로그(daily-combat-log)·인터랙션(interaction-panel)·몬스터HP(monster-health-bar)·낚시(fishing-panel)·포션(potion-status-panel)
8. 각 창마다 **Figma 렌더 → 현재 UTK → 갭 목록 → 재구성**, 로직(드래그드롭/클릭/버튼)을 Figma 동작에 맞춰 재구현

## 4. 검증 게이트 (각 창)
1. `./compile_test.sh` → `error CS` 0
2. EditMode XML → 기존 요리 데이터 2건 실패는 무관, 신규 회귀 0
3. Play → Figma 렌더와 에디터 화면을 나란히 대조 (에디터 Play는 사용자 환경)
4. 커밋(신규 .cs .meta 동반, 파일 지정 add) + QAPROGRESS/ROADMAP 기록

## 5. 리스크
- `InventoryWindowUTK`(950줄)/`WarehouseWindowUTK`(836줄)/`LootWindowUTK` — 드래그앤드롭·금융·양방향 이동 로직이 얽혀 있어, "Figma 그리드로 재배열 + 기존 데이터/드롭 경로 보존"을 분리해 단계적으로.
- 서브에이전트 위임 실패 → 부모 직접 (이 프로젝트 실측).
- Figma 프레임 중 일부는 SF 스타일(사이버펑크) — 중세 게임 컨텐츠와 데이터 모델이 안 맞으면 강제 매핑 금지, 실존 소스로 대체(P3 상태창 '호감도→허기' 선례).