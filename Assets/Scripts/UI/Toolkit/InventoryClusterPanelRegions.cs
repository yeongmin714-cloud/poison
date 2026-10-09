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
        public static readonly Rect EquippedSection = new Rect(168f, 211.4f, 456f, 230.4f);
        public static readonly Rect EquipGrid = new Rect(182.4f, 254.8f, 427.2f, 172.8f);
        public static readonly Rect BagGrid = new Rect(168f, 457.4f, 456f, 446.4f);
        public static readonly Rect BagRow1 = new Rect(168f, 457.4f, 456f, 81.6f);
        public static readonly Rect BagRow2 = new Rect(168f, 548.6f, 456f, 81.6f);
        public static readonly Rect BagRow3 = new Rect(168f, 639.8f, 456f, 81.6f);
        public static readonly Rect BagRow4 = new Rect(168f, 731f, 456f, 81.6f);
        public static readonly Rect BagRow5 = new Rect(168f, 822.2f, 456f, 81.6f);
        public static readonly Rect InventoryFooter = new Rect(168f, 911f, 456f, 56f);

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
        public static readonly Rect InventoryEquipmentBody = new Rect(24f, 127.4f, 456f, 230.4f);
        public static readonly Rect InventoryEquipGridBody = new Rect(38.4f, 170.8f, 427.2f, 172.8f);
        public static readonly Rect InventoryBagViewport = new Rect(24f, 373.4f, 456f, 446.4f);
        public static readonly Rect InventoryFooterBody = new Rect(24f, 827f, 456f, 56f);
        public const int InventoryVisibleBagRows = 5;
        public const float EquipCellGap = 4.8f;

        // ── [Figma 15:4 카테고리 탭 바] 윈도우-로컬 — TabContainer(캔버스 168,168,456,36.2) → (24, 84, 456, 36.2).
        //    탭 5개 각 87.4×36.2, x stride 92.2(갭 4.8): 로컬 x = 24, 116.2, 208.3, 300.5, 392.6.
        public static readonly Rect InventoryTabsBody = new Rect(24f, 84f, 456f, 36.2f);
        public const float InventoryTabWidth = 87.4f;
        public const float InventoryTabHeight = 36.2f;
        public const float InventoryTabStride = 92.2f;
        public const int InventoryTabCount = 5;

        // ── [Figma 15:4 WeightRow] 윈도우-로컬 — InventoryFooter(24, 827, 456, 56) 내부.
        //    WeightLabel: x=48, 105×18, #8B949E 15.6/400 "적재량" / WeightValue: x=339, 141×20, #58A6FF 15.6/700 우측정렬.
        public static readonly Rect InventoryWeightLabel = new Rect(48f, 841.4f, 105f, 18f);
        public static readonly Rect InventoryWeightValue = new Rect(339f, 841.4f, 141f, 20f);

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
