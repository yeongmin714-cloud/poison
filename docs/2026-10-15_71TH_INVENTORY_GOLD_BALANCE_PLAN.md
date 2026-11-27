# 71차 계획 — 인벤/창고 GoldBalance(피그마 225:4~11) 수용 + 별도 지갑 배지 폐기

> 작성: 2026-10-15. 사용자 요구: "피그마에 골드 표시 창을 만들었다. 지갑을 따로 두지 말고 인벤과 창고창을
> 피그마 형태와 같도록 다시 수리, 전리품 골드 수령 시 그 인벤토리 UI의 골드가 늘어나도록".
> 해석: 70차의 별도 핫바 배지(GoldBadgeUTK)를 폐기하고, 골드 표시는 인벤 푸터+창고 스탯의 GoldBalance 위젯으로
> 통합(피그마 진실치). 골드 데이터는 PlayerStats 지갑 단일소스 유지(70차 인터셉트 — 전리품 수령 → 지갑 증가 →
> 창 개방 중이면 GoldChanged로 라벨 즉시 증가, 닫혀 있으면 개방 시 최신값).

## Figma 15:4 현신 진실치 (2026-10-15 실측 — lastModified 01:19Z, 225:4~225:11 신규)

### 인벤 푸터 (변경됨 — 68차 값 대비 y/높이/내부 배치 전부 갱신)
- InventoryFooter: 윈도우-로컬 **(24, 843.6) 456×48.2** (구 24,827 456×56)
- WeightRow: footer-local (0,7.2) 456×26.6
- GoldBalance: WeightRow-local **(0,0) 97.4×26.6**:
  - GoldBadge pill (0,0) 46.2×26.6 — bg **#FFFFFF/4%** · radius 4.8 · 테두리 없음
  - GoldLabel "골드" — pill-local (9.6,4.8) 27×17, 14.4/400 **#F0F6FC**
  - GoldValue "1,240" — WeightRow-local (53.4,3.8) 44×19, 14.4/700 #F0F6FC
- WeightTitle: WeightRow-local (141.7,4.3) 129×18 — dumbbell 아이콘(16.8×16.8, #8B949E) + WeightLabel
- WeightLabel "인벤토리 적재량": WeightRow-local (165.7,4.3) 105×18, 15.6/400 #8B949E(아이콘 우측)
- WeightValue: WeightRow-local (315,3.3) 141×20 — 런타임 "사용 N/40" 규약 유지(위조 금지)
- ProgressBar: footer-local **(0,41) 456×7.2** — bg #FFFFFF/8% radius 3.6 · ProgressFill **#58A6FF** radius 3.6
  (높이 사용률 시각화 — fill 폭 = 456 × used/capacity, WeightValue 규약 동일 소스)

### 창고 StorageStats (위치 불변 (24,100.8) 456×26.6 — GoldBalance 삽입)
- BadgeItem (0,0) 76.2×26.6 (기존 availability 라벨)
- **GoldBalance (147.4,0) 97.4×26.6** — 인벤과 동일 위젯(pill+값)
- SlotCapacity (316,3.8) 140×19 (기존)

## Phase 1 — 별도 지갑 배지 폐기 (GoldBadgeUTK)

- 파일 삭제: `Assets/Scripts/UI/Toolkit/GoldBadgeUTK.cs` + `.meta` (70차 미커밋 신규 — 안전 삭제).
- GoldEconomyTests 케이스 5(GoldBadgeUTK_Wired) 삭제 → Phase 4 신규 어서션으로 교체.

## Phase 2 — 공용 위젯 UTKGoldBalance 신설 (단일소스)

파일: 신규 `Assets/Scripts/UI/Toolkit/UTKGoldBalance.cs`.
- `public static VisualElement Build(out Label valueLabel, bool includeBadge = true)` — pill(46.2×26.6, bg #FFFFFF/4%,
  radius 4.8) + "골드" 14.4/400 #F0F6FC + valueLabel(44×19, 14.4/700 #F0F6FC, `#,##0`). 컨테이너 97.4×26.6.
- `public static void Refresh(Label valueLabel)` — PlayerStats.Instance?.Gold.ToString("#,##0") (null="0").
- `public static IDisposable Subscribe(Label valueLabel)` 또는 창별 구독 — GoldChanged → Refresh.
  (구독 중복 누수 방지: 창 Hide/탈부착 시 해제 — InventoryWindowUTK Equip 구독 선례.)

## Phase 3 — 인벤 푸터 현신 수리 (InventoryWindowUTK.cs + Regions)

1. `InventoryClusterPanelRegions`:
   - `InventoryFooterBody = (24, 843.6, 456, 48.2)` 갱신
   - 신설: `InventoryGoldBadge=(33.6,855.6,46.2,26.6)`·`InventoryGoldLabel=(33.6,855.6,27,17)`은 badge-local 개념이라
     창 기준 2중 계산 피하기 — **WeightRow-로컬 Rect 신설**: `InventoryWeightRow=(0,7.2,456,26.6)`(footer-local),
     `GoldBalanceRow=(0,0,97.4,26.6)`(WeightRow-로컬), `GoldValueRow=(53.4,3.8,44,19)`,
     `WeightTitleRow=(141.7,4.3,129,18)`, `DumbbellRow=(141.7,4.9,16.8,16.8)`, `WeightLabelRow=(165.7,4.3,105,18)`,
     `WeightValueRow=(315,3.3,141,20)`, `ProgressRow=(0,41,456,7.2)`(footer-local). 구 Rect(InventoryWeightLabel/
     InventoryWeightValue)는 다른 참조 없으면 교체, 있으면 유지+신규 병행(참조 확인 후).
2. `InventoryWindowUTK`:
   - `_footer` 내부 재구성: WeightRow 컨테이너 신설 → [UTKGoldBalance.Build | dumbbell+WeightLabel | WeightValue | ProgressBar]
     — 절대 배치(footer-local) 계약 유지: ApplyInventoryBodyLayout에서 footer rect 갱신 + 자식 SetLocalRect.
   - dumbbell 절차 아이콘: 3rect(좌우 추 3.2×10.4 + 중앙 바 10.4×2.4) #8B949E — 프리미티브(위조 아님 — Figma 벡터의 기하 모사).
   - ProgressBar: bg #FFFFFF/8% radius 3.6 + fill #58A6FF radius 3.6 — 폭 = 456 × Mathf.Clamp01(used/capacity).
   - 갱신: UpdateWeightFooter → RefreshFooter(골드+사용량+fill 통합). GoldChanged 구독(Show/Attach 시, Hide/Detach 해제).
   - RefreshGrid(force)에도 RefreshFooter 호출(폴백 갱신 — GoldChanged 누락 방지).
3. 구 GoldBadgeUTK 관련 잔존 참조 0건 확인(grep).

## Phase 4 — 창고 StorageStats GoldBalance (WarehouseWindowUTK.cs)

- `_storageStats` 자식에 GoldBalance 삽입 — storageStats-로컬 (147.4,0) 97.4×26.6 (availability(0,0)과 capacity(316,3.8) 사이).
- availability는 기존 absolute (9.6,4.8) — 그대로. capacity는 기존 flex row → 절대 배치 전환하지 말고 **현재 구조 존중**:
  GoldBalance만 absolute (147.4,0) 삽입 + capacityLabel 텍스트 갱신 경로 유지.
- 갱신: RefreshCapacity에서 UTKGoldBalance.Refresh(골드 라벨) + GoldChanged 구독(Show/Hide 수명 동기).

## Phase 5 — 전리품 골드 수령 → 인벤 UI 골드 증가 (연결 확인 — 신규 코드 최소)

- 70차 인터셉트(AddItem("gold")→지갑) 유지 — 전리품 수령 시 지갑 증가.
- 인벤 창 개방 중 수령 → GoldChanged → GoldValue 즉시 증가(Phase 3 구독).
- 인벤 창 닫힌 상태 수령 → 개방 시 Show→RefreshFooter로 최신값. 별도 코드 불필요 — 테스트로 계약 고정.

## Phase 6 — TDD (GoldEconomyTests 갱신)

- 삭제: GoldBadgeUTK_Wired 케이스.
- 신규/갱신: ①FooterGoldBalance_Markers(소스에 GoldBalance/골드/#,##0/GoldChanged 구독/ProgressFill #58A6FF 마커)
  ②FooterGeometry_NewTruth(Regions 새 값: 843.6/48.2/41.0/53.4/147.4) ③WarehouseGoldBalance_Markers
  ④StandaloneBadgeRemoved(GoldBadgeUTK 파일 부재 + 참조 0건) ⑤UtkGoldBalance_SingleSource(공용 빌더 존재+2창 사용 마커).
- 기존 케이스(인터셉트/그리드 스킵/지갑 통일) 유지.

## 공통

- 컴파일 error CS 0 ×2회 / EditMode 전수(871/860/11 유지 — 신규 회귀 0).
- Play 게이트(사용자): ①인벤 푸터에 "골드 | 1,240" 형태 배지+값(실제 지갑값) ②전리품 골드 수령 시 창 열려 있으면 즉시 증가 ③창고 열면 스탯 행에 골드 ④적재량 진행바(파랑 fill) ⑤핫바 옆 별도 배지 소멸 ⑥나머지 UI 무사고.
- stage/commit/push는 사용자 승인 후.
