using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.UI.Toolkit;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    public class WarehouseFigmaTransferTests
    {
        private const string Territory = "warehouse-figma-transfer-regression";
        private PlayerInventory _inventory;
        private WarehouseSystem _warehouse;
        private GameObject _inventoryObject;
        private GameObject _warehouseObject;
        private WarehouseWindowUTK _window;
        private FieldInfo _warehouseInstanceField;
        private FieldInfo _windowInstanceField;

        [SetUp]
        public void SetUp()
        {
            PlayerInventory.ResetInstance();
            _warehouseInstanceField = typeof(WarehouseSystem).GetField(
                "<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            _windowInstanceField = typeof(WarehouseWindowUTK).GetField(
                "_instance", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(_warehouseInstanceField, Is.Not.Null);
            Assert.That(_windowInstanceField, Is.Not.Null);
            _warehouseInstanceField.SetValue(null, null);
            _windowInstanceField.SetValue(null, null);

            _inventoryObject = new GameObject("WarehouseFigmaTransferTestInventory");
            _inventory = _inventoryObject.AddComponent<PlayerInventory>();
            if (PlayerInventory.Instance != _inventory)
                typeof(PlayerInventory).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(_inventory, null);

            _warehouseObject = new GameObject("WarehouseFigmaTransferTestSystem");
            _warehouse = _warehouseObject.AddComponent<WarehouseSystem>();
            _warehouseInstanceField.SetValue(null, _warehouse);
            _warehouse.Clear();

            // Ensure builds the VisualElement-only window and does not bootstrap runtime dependencies.
            WarehouseWindowUTK.Ensure();
            _window = WarehouseWindowUTK.Instance;
            Assert.That(_window, Is.Not.Null);
            _window.SetTerritory(Territory);
        }

        [TearDown]
        public void TearDown()
        {
            if (_window != null && _window.IsOpen)
                _window.Hide();
            if (_windowInstanceField != null)
                _windowInstanceField.SetValue(null, null);
            if (_warehouseObject != null)
                Object.DestroyImmediate(_warehouseObject);
            PlayerInventory.ResetInstance();
            if (_warehouseInstanceField != null)
                _warehouseInstanceField.SetValue(null, null);
        }

        [Test]
        public void CanDrop_AcceptsInventoryPayload()
        {
            var payload = new UTKDragPayload
            {
                Source = UTKDragSourceKind.Inventory,
                Item = Item("inventory-drop", 99)
            };

            Assert.That(_window.CanDrop(payload), Is.True);
        }

        [Test]
        public void CanDrop_RejectsWarehousePayload()
        {
            var payload = new UTKDragPayload
            {
                Source = UTKDragSourceKind.Warehouse,
                Item = Item("warehouse-drop", 99),
                SourceIndex = 0,
                TerritoryId = Territory
            };

            Assert.That(_window.CanDrop(payload), Is.False);
        }

        [Test]
        public void Drop_WarehousePayloadFromDifferentTerritory_DoesNotWithdrawCurrentTerritorySlot()
        {
            const string otherTerritory = "warehouse-figma-transfer-other-territory";
            PlayerInventory.ItemData currentItem = Item("current-territory-item", 99);
            PlayerInventory.ItemData payloadItem = Item("other-territory-item", 99);
            Assert.That(_warehouse.AddItem(Territory, currentItem), Is.True);
            Assert.That(_warehouse.AddItem(otherTerritory, payloadItem), Is.True);
            var payload = new UTKDragPayload
            {
                Source = UTKDragSourceKind.Warehouse,
                SourceIndex = 0,
                TerritoryId = otherTerritory,
                Item = payloadItem
            };

            bool moved = _window.Drop(payload);

            Assert.That(moved, Is.False);
            Assert.That(CountWarehouse(currentItem.id), Is.EqualTo(1));
            Assert.That(_inventory.GetItemCount(currentItem.id), Is.Zero);
            Assert.That(_warehouse.GetItems(otherTerritory).Count, Is.EqualTo(1));
            Assert.That(_warehouse.GetItems(otherTerritory)[0].item.id, Is.EqualTo(payloadItem.id));
        }

        [Test]
        public void DepositFromInventory_MovesExactlyOneAndConservesStack()
        {
            PlayerInventory.ItemData item = Item("single-deposit", 99);
            Assert.That(_inventory.AddItem(item, 3), Is.True);

            bool moved = _window.DepositFromInventory(0, item);

            Assert.That(moved, Is.True);
            Assert.That(_inventory.GetItemCount(item.id), Is.EqualTo(2));
            Assert.That(CountWarehouse(item.id), Is.EqualTo(1));
            Assert.That(TotalCount(item.id), Is.EqualTo(3), "A one-item deposit must conserve the stack total.");
        }

        [Test]
        public void DepositFromInventory_WhenWarehouseFull_FailsWithoutChangingEitherSide()
        {
            FillWarehouseToCapacity();
            PlayerInventory.ItemData item = Item("full-warehouse-deposit", 99);
            Assert.That(_inventory.AddItem(item, 2), Is.True);
            int inventoryBefore = _inventory.GetItemCount(item.id);
            int warehouseBefore = CountWarehouse(item.id);

            bool moved = _window.DepositFromInventory(0, item);

            Assert.That(moved, Is.False);
            Assert.That(_inventory.GetItemCount(item.id), Is.EqualTo(inventoryBefore));
            Assert.That(CountWarehouse(item.id), Is.EqualTo(warehouseBefore));
            Assert.That(TotalCount(item.id), Is.EqualTo(2));
            Assert.That(_warehouse.GetItemCount(Territory), Is.EqualTo(_warehouse.GetSlotCapacity(Territory)));
        }

        [Test]
        public void StoreAll_WhenWarehouseFillsMidBatch_AbortsWithoutLosingItems()
        {
            int capacity = _warehouse.GetSlotCapacity(Territory);
            for (int i = 0; i < capacity - 1; i++)
                Assert.That(_warehouse.AddItem(Territory, Item("preexisting-" + i, 1), 1), Is.True);
            // maxStack=1 gives this ID two inventory slots, so the second deposit hits a full destination.
            PlayerInventory.ItemData stacked = Item("bulk-stack", 1);
            PlayerInventory.ItemData afterStack = Item("bulk-after-stack", 99);
            Assert.That(_inventory.AddItem(stacked, 2), Is.True);
            Assert.That(_inventory.AddItem(afterStack, 1), Is.True);
            int totalBefore = TotalCount(stacked.id) + TotalCount(afterStack.id);

            InvokeWindowMethod("OnStoreAllClicked");

            Assert.That(_warehouse.GetItemCount(Territory), Is.EqualTo(capacity));
            Assert.That(_inventory.GetItemCount(stacked.id), Is.EqualTo(1), "The untransferred item stays in inventory.");
            Assert.That(CountWarehouse(stacked.id), Is.EqualTo(1));
            Assert.That(_inventory.GetItemCount(afterStack.id), Is.EqualTo(1), "A later inventory slot is untouched after abort.");
            Assert.That(CountWarehouse(afterStack.id), Is.Zero);
            Assert.That(TotalCount(stacked.id) + TotalCount(afterStack.id), Is.EqualTo(totalBefore));
        }

        [Test]
        public void RetrieveAll_UsesOriginalDescendingIndicesAndLeavesBlockedItems()
        {
            PlayerInventory.ItemData first = Item("retrieve-first", 1);
            PlayerInventory.ItemData middle = Item("retrieve-middle", 1);
            PlayerInventory.ItemData last = Item("retrieve-last", 1);
            Assert.That(_warehouse.AddItem(Territory, first), Is.True);
            Assert.That(_warehouse.AddItem(Territory, middle), Is.True);
            Assert.That(_warehouse.AddItem(Territory, last), Is.True);

            // Leave exactly two free inventory slots; the descending pass should move last, then middle,
            // even though each successful transfer removes an entry and shifts the remaining indices.
            for (int i = 0; i < 38; i++)
                Assert.That(_inventory.AddItem(Item("filler-" + i, 1)), Is.True);

            InvokeWindowMethod("OnRetrieveAllClicked");

            Assert.That(_inventory.GetItemCount(last.id), Is.EqualTo(1));
            Assert.That(_inventory.GetItemCount(middle.id), Is.EqualTo(1));
            Assert.That(_inventory.GetItemCount(first.id), Is.Zero);
            List<PlayerInventory.ItemSlot> remaining = _warehouse.GetItems(Territory);
            Assert.That(remaining.Count, Is.EqualTo(1));
            Assert.That(remaining[0].item.id, Is.EqualTo(first.id));
            Assert.That(remaining[0].count, Is.EqualTo(1));
            Assert.That(TotalCount(first.id) + TotalCount(middle.id) + TotalCount(last.id), Is.EqualTo(3));
        }

        private void FillWarehouseToCapacity()
        {
            for (int i = 0; i < _warehouse.GetSlotCapacity(Territory); i++)
                Assert.That(_warehouse.AddItem(Territory, Item("full-slot-" + i, 1), 1), Is.True);
        }

        private int CountWarehouse(string itemId)
        {
            int count = 0;
            foreach (PlayerInventory.ItemSlot slot in _warehouse.GetItems(Territory))
                if (slot != null && slot.item != null && slot.item.id == itemId)
                    count += slot.count;
            return count;
        }

        private int TotalCount(string itemId)
            => _inventory.GetItemCount(itemId) + CountWarehouse(itemId);

        private void InvokeWindowMethod(string methodName)
        {
            MethodInfo method = typeof(WarehouseWindowUTK).GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Expected production bulk action method " + methodName + ".");
            method.Invoke(_window, null);
        }

        private static PlayerInventory.ItemData Item(string id, int maxStack)
            => new PlayerInventory.ItemData { id = id, displayName = id, maxStack = maxStack };
    }
}
