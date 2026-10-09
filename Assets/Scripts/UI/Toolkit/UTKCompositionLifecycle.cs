using System.Collections.Generic;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// Minimal, host-independent lifecycle contract for sibling UI panels that form one
    /// screen composition. It deliberately does not attach elements, position panels,
    /// register ESC windows, or replace legacy Open/Toggle entry points.
    /// </summary>
    public interface IUTKCompositionPanel
    {
        bool IsOpen { get; }
        void Open();
        void Close();
    }

    /// <summary>
    /// Coordinates a stable set of sibling windows. The operation works on a snapshot, so
    /// callbacks may unregister/reconfigure a composition without invalidating its iteration.
    /// Each UTKWindowBase still registers/unregisters itself with UTKWindowManager, preserving
    /// per-window ESC ordering and independent close buttons.
    /// </summary>
    public sealed class UTKCompositionLifecycle
    {
        private readonly List<IUTKCompositionPanel> _panels = new List<IUTKCompositionPanel>();

        public int Count => _panels.Count;
        public bool AnyOpen
        {
            get
            {
                for (int i = 0; i < _panels.Count; i++)
                    if (_panels[i] != null && _panels[i].IsOpen) return true;
                return false;
            }
        }
        public bool AreAllOpen
        {
            get
            {
                if (_panels.Count == 0) return false;
                for (int i = 0; i < _panels.Count; i++)
                    if (_panels[i] == null || !_panels[i].IsOpen) return false;
                return true;
            }
        }

        public bool Register(IUTKCompositionPanel panel)
        {
            if (panel == null || _panels.Contains(panel)) return false;
            _panels.Add(panel);
            return true;
        }

        public bool Unregister(IUTKCompositionPanel panel)
        {
            return panel != null && _panels.Remove(panel);
        }

        public void OpenAll()
        {
            IUTKCompositionPanel[] snapshot = _panels.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                var panel = snapshot[i];
                if (panel != null && !panel.IsOpen) panel.Open();
            }
        }

        public void CloseAll()
        {
            IUTKCompositionPanel[] snapshot = _panels.ToArray();
            for (int i = snapshot.Length - 1; i >= 0; i--)
            {
                var panel = snapshot[i];
                if (panel != null && panel.IsOpen) panel.Close();
            }
        }

        public void ToggleAll()
        {
            if (AnyOpen) CloseAll();
            else OpenAll();
        }
    }

    /// <summary>
    /// The existing inventory entry routes are the compatibility owner for the Figma 15:4
    /// sibling panels. This coordinator composes their established UTKWindowBase instances;
    /// it does not merge their data owners or replace each window's ESC/close behavior.
    /// </summary>
    public static class UTKInventoryClusterComposition
    {
        private static UTKCompositionLifecycle _lifecycle;
        private static bool _containsWarehouse;

        public static void OpenInventory()
        {
            InventoryWindowUTK.Ensure();
            ItemDescriptionWindowUTK.Ensure();
            WarehouseWindowUTK warehouse = WarehouseWindowUTK.Instance;
            if (warehouse != null && warehouse.IsOpen)
                Configure(true, InventoryWindowUTK.Instance, ItemDescriptionWindowUTK.Ensure(), warehouse);
            else
                Configure(false, InventoryWindowUTK.Instance, ItemDescriptionWindowUTK.Ensure());
            _lifecycle.OpenAll();
        }

        public static void OpenWarehouse(string territoryId)
        {
            InventoryWindowUTK.Ensure();
            ItemDescriptionWindowUTK.Ensure();
            WarehouseWindowUTK.Ensure();
            if (WarehouseWindowUTK.Instance == null)
                return;
            WarehouseWindowUTK.Instance.SetTerritory(territoryId);
            Configure(true, InventoryWindowUTK.Instance, ItemDescriptionWindowUTK.Ensure(), WarehouseWindowUTK.Instance);
            _lifecycle.OpenAll();
        }

        public static void ToggleInventory()
        {
            if (_lifecycle == null)
            {
                OpenInventory();
                return;
            }

            _lifecycle.ToggleAll();
            if (!_lifecycle.AnyOpen)
            {
                _lifecycle = null;
                _containsWarehouse = false;
            }
        }

        public static void ToggleWarehouse(string territoryId)
        {
            if (_lifecycle == null || !_containsWarehouse)
            {
                OpenWarehouse(territoryId);
                return;
            }

            _lifecycle.ToggleAll();
            if (!_lifecycle.AnyOpen)
            {
                _lifecycle = null;
                _containsWarehouse = false;
            }
        }

        private static void Configure(bool containsWarehouse, params UTKWindowBase[] panels)
        {
            var composition = new UTKCompositionLifecycle();
            for (int i = 0; i < panels.Length; i++)
                composition.Register(panels[i]);
            _lifecycle = composition;
            _containsWarehouse = containsWarehouse;
        }
    }
}
