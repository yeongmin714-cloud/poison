using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// Figma 15:4 absolute canvas bounds for the inventory, item detail, and warehouse cluster.
    /// Bounds are applied in the coordinate space of the supplied canvas root, without reparenting.
    /// </summary>
    public static class InventoryClusterFigmaLayout
    {
        public static readonly Rect InventoryBounds = new Rect(144f, 84f, 504f, 912f);
        public static readonly Rect DetailBounds = new Rect(672f, 175.2f, 576f, 729.6f);
        public static readonly Rect WarehouseBounds = new Rect(1272f, 84f, 504f, 912f);

        public static bool ApplyInventory(VisualElement inventory, VisualElement canvasRoot)
            => FigmaCanvasLayout.Apply(inventory, InventoryBounds, canvasRoot);

        public static bool ApplyDetail(VisualElement detail, VisualElement canvasRoot)
            => FigmaCanvasLayout.Apply(detail, DetailBounds, canvasRoot);

        /// <summary>Scales detail-window child geometry from panel-local Figma pixels to root units.</summary>
        public static Rect ScaleDetailRegion(Rect panelLocal, Vector2 rootSize)
            => FigmaCanvasLayout.ScaleRect(panelLocal, rootSize);

        public static bool ApplyWarehouse(VisualElement warehouse, VisualElement canvasRoot)
            => FigmaCanvasLayout.Apply(warehouse, WarehouseBounds, canvasRoot);

        // ── [Figma 정합 v3] 등배수 디자인공간 계약 — 창 박스=rect×k, designSpace raw px+scale=k. ──
        public static bool ApplyInventory(VisualElement inventory, VisualElement designSpace, VisualElement canvasRoot)
            => FigmaCanvasLayout.ApplyDesignSpace(inventory, designSpace, InventoryBounds, canvasRoot);

        public static bool ApplyDetail(VisualElement detail, VisualElement designSpace, VisualElement canvasRoot)
            => FigmaCanvasLayout.ApplyDesignSpace(detail, designSpace, DetailBounds, canvasRoot);

        public static bool ApplyWarehouse(VisualElement warehouse, VisualElement designSpace, VisualElement canvasRoot)
            => FigmaCanvasLayout.ApplyDesignSpace(warehouse, designSpace, WarehouseBounds, canvasRoot);

    }
}
