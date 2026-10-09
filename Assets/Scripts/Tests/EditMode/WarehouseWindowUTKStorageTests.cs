using System.Reflection;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.UI.Toolkit;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    public class WarehouseWindowUTKStorageTests
    {
        private PlayerInventory _inventory;
        private WarehouseSystem _warehouse;
        private GameObject _inventoryObject;
        private GameObject _warehouseObject;
        private PlayerInventory _previousInventory;
        private WarehouseSystem _previousWarehouse;
        private WarehouseWindowUTK _window;
        private const string Territory = "storage-transfer-tests";

        [SetUp]
        public void SetUp()
        {
            _previousInventory = PlayerInventory.Instance;
            _previousWarehouse = WarehouseSystem.Instance;
            SetSingleton(typeof(PlayerInventory), null);
            SetSingleton(typeof(WarehouseSystem), null);

            _inventoryObject = new GameObject("WarehouseStorageTestInventory");
            _inventory = _inventoryObject.AddComponent<PlayerInventory>();
            typeof(PlayerInventory).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_inventory, null);
            _warehouseObject = new GameObject("WarehouseStorageTestSystem");
            _warehouse = _warehouseObject.AddComponent<WarehouseSystem>();
            SetSingleton(typeof(WarehouseSystem), _warehouse);
            _window = (WarehouseWindowUTK)typeof(WarehouseWindowUTK)
                .GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, System.Type.EmptyTypes, null)
                .Invoke(null);
            _window.SetTerritory(Territory);
            _warehouse.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            if (_inventoryObject != null) Object.DestroyImmediate(_inventoryObject);
            if (_warehouseObject != null) Object.DestroyImmediate(_warehouseObject);
            SetSingleton(typeof(PlayerInventory), _previousInventory != null ? _previousInventory : null);
            SetSingleton(typeof(WarehouseSystem), _previousWarehouse != null ? _previousWarehouse : null);
        }

        [Test]
        public void DepositFromInventory_MovesExactlyOneAndRemovesExactlyOne()
        {
            var item = Item("deposit-one");
            Assert.That(_inventory.AddItem(item, 2), Is.True);

            bool moved = _window.DepositFromInventory(0, item);

            Assert.That(moved, Is.True);
            Assert.That(_inventory.GetItemCount(item.id), Is.EqualTo(1),
                "The transfer removes one item, not one in the wrapper plus another in DepositItem.");
            Assert.That(Count(_warehouse.GetItems(Territory), item.id), Is.EqualTo(1));
            Assert.That(TotalCount(item.id), Is.EqualTo(2));
        }

        [Test]
        public void DepositFromInventory_FullWarehouseFailureConservesTotalCount()
        {
            for (int i = 0; i < _warehouse.GetSlotCapacity(Territory); i++)
                Assert.That(_warehouse.AddItem(Territory, Item("existing-" + i), 1), Is.True);
            var item = Item("blocked-deposit");
            Assert.That(_inventory.AddItem(item, 2), Is.True);
            int before = TotalCount(item.id);

            bool moved = _window.DepositFromInventory(0, item);

            Assert.That(moved, Is.False);
            Assert.That(TotalCount(item.id), Is.EqualTo(before));
            Assert.That(_inventory.GetItemCount(item.id), Is.EqualTo(2));
            Assert.That(Count(_warehouse.GetItems(Territory), item.id), Is.Zero);
        }

        private int TotalCount(string itemId)
            => _inventory.GetItemCount(itemId) + Count(_warehouse.GetItems(Territory), itemId);

        private static int Count(System.Collections.Generic.List<PlayerInventory.ItemSlot> slots, string itemId)
        {
            int total = 0;
            foreach (var slot in slots)
                if (slot != null && slot.item != null && slot.item.id == itemId)
                    total += slot.count;
            return total;
        }

        private static PlayerInventory.ItemData Item(string id)
            => new PlayerInventory.ItemData { id = id, displayName = id, maxStack = 99 };

        private static void SetSingleton(System.Type type, UnityEngine.Object value)
        {
            var field = type.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(null, value);
        }
    }
}
