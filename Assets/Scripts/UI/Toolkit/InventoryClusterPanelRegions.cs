using UnityEngine;
using System.Collections.Generic;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// Figma 15:4 child-region bounds in the shared 1920x1080 canvas coordinate space.
    /// These are geometry only; convert them to a window's local space with ToPanelLocal.
    /// </summary>
    public static class InventoryClusterPanelRegions
    {
        public static readonly Rect InventoryHeader = new Rect(168f, 108f, 456f, 52.8f);
        public static readonly Rect InventoryTabs = new Rect(168f, 168f, 456f, 36.2f);
        // [136차 B1] Figma 15:5 재실측 — EquippedSection/EquipGrid/BagGrid·Row1~5 y +16.6 등간격
        //    (섹션 기원 y 127.4→144 재기원). Storage*·Detail* 리전 무수정.
        //    [144차 P0 V7] InventoryFooter 리전은 144차 재실측 진실치로 교정(아래).
        public static readonly Rect EquippedSection = new Rect(168f, 228f, 456f, 230.4f);
        public static readonly Rect EquipGrid = new Rect(182.4f, 271.4f, 427.2f, 172.8f);
        public static readonly Rect BagGrid = new Rect(168f, 474f, 456f, 446.4f);
        public static readonly Rect BagRow1 = new Rect(168f, 474f, 456f, 81.6f);
        public static readonly Rect BagRow2 = new Rect(168f, 565.2f, 456f, 81.6f);
        public static readonly Rect BagRow3 = new Rect(168f, 656.4f, 456f, 81.6f);
        public static readonly Rect BagRow4 = new Rect(168f, 747.6f, 456f, 81.6f);
        public static readonly Rect BagRow5 = new Rect(168f, 838.8f, 456f, 81.6f);
        // [144차 P0 V7] Figma 15:133 재실측 진실치 — 캔버스 (168,928) 456×48.2
        //    (JSON ay=931 − 15:4 루트 ay=3 = 928 / ax=1359 − 1191 = 168. 구 71차 이전 값 (168,911,456,56)은
        //    y −17 / h +7.8 괴리의 stale치 — inventory.md A51 정정). 런타임은 InventoryFooterBody 사용(무수정).
        public static readonly Rect InventoryFooter = new Rect(168f, 928f, 456f, 48.2f);

        public static readonly Rect DetailHeader = new Rect(696f, 199.2f, 528f, 52.8f);
        public static readonly Rect DetailItemName = new Rect(696f, 271.2f, 528f, 67.2f);
        public static readonly Rect DetailItemImage = new Rect(696f, 357.6f, 528f, 384f);
        public static readonly Rect DetailDescription = new Rect(696f, 760.8f, 528f, 120f);

        public static readonly Rect StorageHeader = new Rect(1296f, 108f, 456f, 52.8f);
        public static readonly Rect StorageStats = new Rect(1296f, 184.8f, 456f, 26.6f);
        public static readonly Rect StorageGrid = new Rect(1296f, 235.4f, 456f, 537.6f);
        public static readonly Rect StorageRow1 = new Rect(1296f, 235.4f, 456f, 81.6f);
        public static readonly Rect StorageRow2 = new Rect(1296f, 326.6f, 456f, 81.6f);
        public static readonly Rect StorageRow3 = new Rect(1296f, 417.8f, 456f, 81.6f);
        public static readonly Rect StorageRow4 = new Rect(1296f, 509f, 456f, 81.6f);
        public static readonly Rect StorageRow5 = new Rect(1296f, 600.2f, 456f, 81.6f);
        public static readonly Rect StorageRow6 = new Rect(1296f, 691.4f, 456f, 81.6f);
        public static readonly Rect StorageFooter = new Rect(1296f, 797f, 456f, 61.2f);
        public static readonly Rect StoreAllButton = new Rect(1296f, 811.4f, 223.2f, 46.8f);
        public static readonly Rect RetrieveAllButton = new Rect(1528.8f, 811.4f, 223.2f, 46.8f);

        public const int BagRowCount = 5;
        public const int StorageRowCount = 6;
        public const float CellSize = 81.6f;
        public const float CellGap = 9.6f;

        // Inventory body coordinates exclude the inherited base titlebar and are local to _content.
        // [136차 B1] Figma 15:5 진실치 — EquipmentBody y 127.4→144 / EquipGridBody (38.4,170.8)→(38.4,187.4) /
        //    BagViewport y 373.4→390 (+16.6 등간격). FooterBody(843.6) 무수정.
        public static readonly Rect InventoryEquipmentBody = new Rect(24f, 144f, 456f, 230.4f);
        public static readonly Rect InventoryEquipGridBody = new Rect(38.4f, 187.4f, 427.2f, 172.8f);
        public static readonly Rect InventoryBagViewport = new Rect(24f, 390f, 456f, 446.4f);
        public static readonly Rect InventoryFooterBody = new Rect(24f, 843.6f, 456f, 48.2f);
        public const int InventoryVisibleBagRows = 5;
        public const float EquipCellGap = 4.8f;

        // ── [Figma 15:4 카테고리 탭 바] 윈도우-로컬 — TabContainer(캔버스 168,168,456,36.2) → (24, 84, 456, 36.2).
        //    탭 5개 각 87.4×36.2, x stride 92.2(갭 4.8): 로컬 x = 24, 116.2, 208.3, 300.5, 392.6.
        public static readonly Rect InventoryTabsBody = new Rect(24f, 84f, 456f, 36.2f);
        public const float InventoryTabWidth = 87.4f;
        public const float InventoryTabHeight = 36.2f;
        public const float InventoryTabStride = 92.2f;
        public const int InventoryTabCount = 5;

        // ── [Figma 15:4 WeightRow] 윈도우-로컬 — InventoryFooterBody(24, 843.6, 456, 48.2) 내부
        //    ([144차 P0 V7] 주석 교정 — 구 "InventoryFooter(24,827,456,56) 내부" 표기는 stale치 참조였음).
        //    WeightLabel: x=48, 105×18, #8B949E 15.6/400 "적재량" / WeightValue: x=339, 141×20, #58A6FF 15.6/700 우측정렬.
        //    [71차] 구 윈도우-로컬값은 기존 기하 계약 테스트 참조로 병행 유지 — 레이아웃 적용부는 아래
        //    WeightRow-로컬 Rect로 전환(윈도우-로컬 2중 계산 방지).
        public static readonly Rect InventoryWeightLabel = new Rect(48f, 841.4f, 105f, 18f);
        public static readonly Rect InventoryWeightValue = new Rect(339f, 841.4f, 141f, 20f);

        // ── [71차 — Figma 225:4~11 개정 인벤 푸터] footerBody (24,843.6) 456×48.2 개정 +
        //    내부 요소는 WeightRow-로컬(footer-로컬) Rect로 신설한다 — 적용부에서 부모-로컬 직접 배치.
        //    WeightRow: footer-로컬 (0,7.2) 456×26.6 / GoldBalance: WeightRow-로컬 (0,0) 97.4×26.6.
        //    GoldValue (53.4,3.8)은 컨테이너 원점 (0,0) 덕에 WeightRow-로컬 == 컨테이너-로컬.
        //    WeightTitle (141.7,4.3) 129×18 = dumbbell(16.8×16.8, #8B949E) + "인벤토리 적재량" 라벨(아이콘 우측).
        //    ProgressRow: footer-로컬 (0,41) 456×7.2 — fill 폭 = 456×Clamp01(used/capacity).
        public static readonly Rect InventoryWeightRow = new Rect(0f, 7.2f, 456f, 26.6f);   // footer-로컬
        public static readonly Rect GoldBalanceRow = new Rect(0f, 0f, 97.4f, 26.6f);        // WeightRow-로컬
        public static readonly Rect GoldValueRow = new Rect(53.4f, 3.8f, 44f, 19f);         // WeightRow-로컬
        public static readonly Rect WeightTitleRow = new Rect(141.7f, 4.3f, 129f, 18f);     // WeightRow-로컬
        public static readonly Rect DumbbellRow = new Rect(141.7f, 4.9f, 16.8f, 16.8f);     // WeightRow-로컬
        public static readonly Rect WeightLabelRow = new Rect(165.7f, 2.7f, 105f, 21.6f);    // [185차 후속] 18px 폰트 — 하우징 18→21.6 (사용자 가독성 조정)
        public static readonly Rect WeightValueRow = new Rect(315f, 2.3f, 141f, 24f);         // [185차 후속] 18px 폰트 — 하우징 20→24
        public static readonly Rect ProgressRow = new Rect(0f, 41f, 456f, 7.2f);            // footer-로컬

        // ── [71차 — Figma 225:4~11 창고 StorageStats] GoldBalance 삽입 — storageStats-로컬 (147.4,0) 97.4×26.6
        //    (availability(9.6,4.8)와 capacityLabel 사이). 인벤 푸터와 동일 공용 위젯(단일소스).
        public static readonly Rect StorageGoldBalanceBody = new Rect(147.4f, 0f, 97.4f, 26.6f);

        // ── [68차 — Figma 15:4 개정 EquippedSection] 윈도우-로컬 신규 요소 실측치.
        //    SectionLabel "장착 중인 장비" 14.4/700 #58A6FF / Divider #30363D 50% /
        //    WeaponSlotNumbers 바(스페이서 3칸 + 뱃지 2칸 = 카드열 4·5 중앙, 18×18 radius 3.6).
        public static readonly Rect EquippedSectionLabel = new Rect(38.4f, 158.4f, 87f, 17f);   // [136차 B2-L2] 141.8→158.4 (+16.6 등간격)
        public static readonly Rect InventoryDivider = new Rect(24f, 381.6f, 456f, 1.2f);        // [136차 B2-L2] 365→381.6 (+16.6 등간격)
        public static readonly Rect EquipNumbersBar = new Rect(38.4f, 354f, 427.2f, 18f);       // [136차 B2-L2] 337.4→354 (+16.6 등간격)
        public const float EquippedStripHeight = 3.6f;   // TierStrip 상단 가로 높이(카드·가방 셀 공통)

        // [Figma 15:4 적재량 푸터] PlayerInventory._maxSlots 기본값 40과 동일한 상수 동기.
        // public 접근자가 없어 상수로 동기하되, 런타임 표기는 GetAllSlots().Length(실제 배열 크기)를 우선 사용한다.
        public const int InventoryMaxSlots = 40;

        // Storage body coordinates exclude the inherited base titlebar; keep distinct from canvas-space regions above.
        public static readonly Rect StorageHeaderBody = new Rect(24f, 24f, 456f, 52.8f);
        public static readonly Rect StorageStatsBody = new Rect(24f, 100.8f, 456f, 26.6f);
        public static readonly Rect StorageGridBody = new Rect(24f, 151.4f, 456f, 537.6f);
        public static readonly Rect StorageRow1Body = new Rect(24f, 151.4f, 456f, 81.6f);
        public static readonly Rect StorageRow2Body = new Rect(24f, 242.6f, 456f, 81.6f);
        public static readonly Rect StorageRow3Body = new Rect(24f, 333.8f, 456f, 81.6f);
        public static readonly Rect StorageRow4Body = new Rect(24f, 425f, 456f, 81.6f);
        public static readonly Rect StorageRow5Body = new Rect(24f, 516.2f, 456f, 81.6f);
        public static readonly Rect StorageRow6Body = new Rect(24f, 607.4f, 456f, 81.6f);
        public static readonly Rect StorageFooterBody = new Rect(24f, 713f, 456f, 61.2f);
        public static readonly Rect StoreAllButtonBody = new Rect(24f, 727.4f, 223.2f, 46.8f);
        public static readonly Rect RetrieveAllButtonBody = new Rect(256.8f, 727.4f, 223.2f, 46.8f);
        public const int StorageVisibleColumns = 5;
        public const int StorageVisibleRows = 6;

        /// <summary>Converts panel-local layout geometry to root units from the resolved UIRoot size.</summary>
        public static Rect ScaleStorageBody(Rect panelLocalRect, Vector2 resolvedRootSize)
            => FigmaCanvasLayout.ScaleRect(panelLocalRect, resolvedRootSize);

        /// <summary>Converts panel-local layout geometry to root units from the resolved UIRoot size.</summary>
        public static Rect ScalePanelLocal(Rect panelLocalRect, Vector2 resolvedRootSize)
            => FigmaCanvasLayout.ScaleRect(panelLocalRect, resolvedRootSize);

        /// <summary>Returns original storage indices in descending order so RemoveAt cannot shift pending sources.</summary>
        public static List<int> GetDescendingStorageIndices(int slotCount)
        {
            var indices = new List<int>(Mathf.Max(0, slotCount));
            for (int index = slotCount - 1; index >= 0; index--)
                indices.Add(index);
            return indices;
        }

        public static int GetVisibleBagRowCount() => InventoryVisibleBagRows;

        /// <summary>Returns complete scroll-content height for storage items using the fixed grid metrics.</summary>
        public static float GetStorageGridContentHeight(int itemCount)
        {
            int safeItemCount = Mathf.Max(0, itemCount);
            int rows = safeItemCount == 0
                ? 0
                : (safeItemCount + StorageVisibleColumns - 1) / StorageVisibleColumns;
            return rows * CellSize + Mathf.Max(0, rows - 1) * CellGap;
        }

        /// <summary>Returns zero bottom spacing for cells on the final content row; other rows retain the standard gap.</summary>
        public static float GetStorageCellBottomMargin(int index, int itemCount)
        {
            int safeItemCount = Mathf.Max(0, itemCount);
            if (index < 0 || index >= safeItemCount)
                return 0f;

            int rows = (safeItemCount + StorageVisibleColumns - 1) / StorageVisibleColumns;
            int finalRowStartIndex = (rows - 1) * StorageVisibleColumns;
            return index >= finalRowStartIndex ? 0f : CellGap;
        }

        /// <summary>Converts canvas-space bounds to coordinates relative to a panel's canvas bounds.</summary>
        public static Rect ToPanelLocal(Rect canvasRect, Rect panelCanvasBounds)
            => new Rect(canvasRect.x - panelCanvasBounds.x, canvasRect.y - panelCanvasBounds.y, canvasRect.width, canvasRect.height);
    }
}
