using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// WarehouseSystem EditMode 테스트 — 영지 창고 시스템 검증.
    /// </summary>
    public class WarehouseSystemTests
    {
        private GameObject _warehouseGo;
        private GameObject _inventoryGo;

        // ================================================================
        // 헬퍼: 리플렉션 Instance 설정
        // ================================================================

        private void SetWarehouseInstance(WarehouseSystem instance)
        {
            var field = typeof(WarehouseSystem).GetField("Instance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, instance);
        }

        private void ClearWarehouseInstance()
        {
            var field = typeof(WarehouseSystem).GetField("Instance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, null);
        }

        private void SetInventoryInstance(PlayerInventory instance)
        {
            var field = typeof(PlayerInventory).GetField("Instance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, instance);
        }

        private void ClearInventoryInstance()
        {
            var field = typeof(PlayerInventory).GetField("Instance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, null);
        }

        // ================================================================
        // Setup / Teardown
        // ================================================================

        [SetUp]
        public void Setup()
        {
            _warehouseGo = new GameObject("TestWarehouse");
            var warehouse = _warehouseGo.AddComponent<WarehouseSystem>();
            SetWarehouseInstance(warehouse);

            _inventoryGo = new GameObject("TestInventory");
            var inventory = _inventoryGo.AddComponent<PlayerInventory>();
            SetInventoryInstance(inventory);
        }

        [TearDown]
        public void Teardown()
        {
            if (_warehouseGo != null)
                Object.DestroyImmediate(_warehouseGo);
            ClearWarehouseInstance();

            if (_inventoryGo != null)
                Object.DestroyImmediate(_inventoryGo);
            ClearInventoryInstance();
        }

        // ================================================================
        // AddItem 테스트
        // ================================================================

        [Test]
        public void AddItem_AddsToWarehouse()
        {
            // Act
            bool result = WarehouseSystem.Instance.AddItem("territory_1", PlayerInventory.Herb_Red, 5);

            // Assert
            Assert.IsTrue(result, "AddItem은 성공 시 true를 반환해야 함");
            var items = WarehouseSystem.Instance.GetItems("territory_1");
            Assert.AreEqual(1, items.Count, "창고에 1개 아이템 슬롯이 있어야 함");
            Assert.AreEqual(5, items[0].count, "아이템 수량이 5여야 함");
        }

        [Test]
        public void AddItem_StacksSameItem()
        {
            // Arrange
            WarehouseSystem.Instance.AddItem("territory_1", PlayerInventory.Herb_Red, 10);

            // Act
            bool result = WarehouseSystem.Instance.AddItem("territory_1", PlayerInventory.Herb_Red, 10);

            // Assert
            Assert.IsTrue(result, "스택 추가는 성공해야 함");
            var items = WarehouseSystem.Instance.GetItems("territory_1");
            Assert.AreEqual(1, items.Count, "같은 아이템은 1개 슬롯에 스택되어야 함");
            Assert.AreEqual(20, items[0].count, "총 수량이 20이어야 함 (maxStack=20)");
        }

        [Test]
        public void AddItem_FullWarehouse_ReturnsFalse()
        {
            // Arrange: 20개의 다른 아이템으로 창고를 가득 채움
            for (int i = 0; i < 20; i++)
            {
                var item = new PlayerInventory.ItemData
                {
                    id = $"test_item_{i}",
                    displayName = $"TestItem{i}",
                    category = PlayerInventory.ItemCategory.Material,
                    maxStack = 99
                };
                WarehouseSystem.Instance.AddItem("territory_1", item, 1);
            }

            // Act
            bool result = WarehouseSystem.Instance.AddItem("territory_1", PlayerInventory.Herb_Red, 1);

            // Assert
            Assert.IsFalse(result, "가득 찬 창고에 추가 시 false를 반환해야 함");
        }

        // ================================================================
        // RemoveItem 테스트
        // ================================================================

        [Test]
        public void RemoveItem_RemovesCount()
        {
            // Arrange
            WarehouseSystem.Instance.AddItem("territory_1", PlayerInventory.Herb_Red, 10);

            // Act
            bool result = WarehouseSystem.Instance.RemoveItem("territory_1", 0, 3);

            // Assert
            Assert.IsTrue(result, "RemoveItem은 성공 시 true를 반환해야 함");
            var items = WarehouseSystem.Instance.GetItems("territory_1");
            Assert.AreEqual(1, items.Count, "슬롯이 아직 존재해야 함");
            Assert.AreEqual(7, items[0].count, "제거 후 수량이 7이어야 함");
        }

        [Test]
        public void RemoveItem_RemoveAll_RemovesSlot()
        {
            // Arrange
            WarehouseSystem.Instance.AddItem("territory_1", PlayerInventory.Herb_Red, 5);

            // Act
            bool result = WarehouseSystem.Instance.RemoveItem("territory_1", 0, 5);

            // Assert
            Assert.IsTrue(result, "전량 제거는 성공해야 함");
            var items = WarehouseSystem.Instance.GetItems("territory_1");
            Assert.AreEqual(0, items.Count, "전량 제거 후 슬롯이 제거되어야 함");
        }

        // ================================================================
        // TransferToInventory 테스트
        // ================================================================

        [Test]
        public void TransferToInventory_MovesToPlayerInventory()
        {
            // Arrange
            WarehouseSystem.Instance.AddItem("territory_1", PlayerInventory.Herb_Red, 5);

            // Act
            bool result = WarehouseSystem.Instance.TransferToInventory("territory_1", 0, 3);

            // Assert
            Assert.IsTrue(result, "TransferToInventory는 성공 시 true를 반환해야 함");

            // 창고에 남은 아이템 확인
            var items = WarehouseSystem.Instance.GetItems("territory_1");
            Assert.AreEqual(1, items.Count, "창고에 슬롯이 아직 존재해야 함");
            Assert.AreEqual(2, items[0].count, "창고에 2개가 남아있어야 함");

            // 플레이어 인벤토리로 이동됐는지 확인
            int invCount = PlayerInventory.Instance.GetItemCount("herb_red");
            Assert.AreEqual(3, invCount, "플레이어 인벤토리에 3개가 있어야 함");
        }

        // ================================================================
        // GetItems 테스트
        // ================================================================

        [Test]
        public void GetItems_ReturnsCorrectList()
        {
            // Arrange
            WarehouseSystem.Instance.AddItem("territory_1", PlayerInventory.Herb_Red, 3);
            WarehouseSystem.Instance.AddItem("territory_1", PlayerInventory.Herb_Purple, 7);
            WarehouseSystem.Instance.AddItem("territory_1", PlayerInventory.Herb_Yellow, 1);

            // Act
            var items = WarehouseSystem.Instance.GetItems("territory_1");

            // Assert
            Assert.AreEqual(3, items.Count, "3개 아이템이 있어야 함");
            Assert.AreEqual("herb_red", items[0].item.id);
            Assert.AreEqual(3, items[0].count);
            Assert.AreEqual("herb_purple", items[1].item.id);
            Assert.AreEqual(7, items[1].count);
            Assert.AreEqual("herb_yellow", items[2].item.id);
            Assert.AreEqual(1, items[2].count);
        }

        // ================================================================
        // Save/Load 테스트
        // ================================================================

        [Test]
        public void GetSaveData_ContainsData()
        {
            // Arrange
            WarehouseSystem.Instance.AddItem("territory_1", PlayerInventory.Herb_Red, 5);
            WarehouseSystem.Instance.AddItem("territory_2", PlayerInventory.Herb_Purple, 10);

            // Act
            var saveData = WarehouseSystem.Instance.GetSaveData();

            // Assert
            Assert.IsNotNull(saveData, "GetSaveData는 null이 아니어야 함");
            Assert.IsNotNull(saveData.warehouseData, "warehouseData는 null이 아니어야 함");
            Assert.AreEqual(2, saveData.warehouseData.Count, "2개 영지 데이터가 있어야 함");

            var entry1 = saveData.warehouseData.Find(e => e.territoryId == "territory_1");
            Assert.IsNotNull(entry1, "territory_1 데이터가 있어야 함");
            Assert.AreEqual(1, entry1.slots.Count, "territory_1에 1개 슬롯");
            Assert.AreEqual("herb_red", entry1.slots[0].item.id);
            Assert.AreEqual(5, entry1.slots[0].count);

            var entry2 = saveData.warehouseData.Find(e => e.territoryId == "territory_2");
            Assert.IsNotNull(entry2, "territory_2 데이터가 있어야 함");
            Assert.AreEqual(1, entry2.slots.Count, "territory_2에 1개 슬롯");
            Assert.AreEqual("herb_purple", entry2.slots[0].item.id);
            Assert.AreEqual(10, entry2.slots[0].count);
        }

        [Test]
        public void LoadFromSaveData_RestoresData()
        {
            // Arrange: 저장 데이터 준비
            var saveData = new WarehouseSaveData();
            var entry = new WarehouseSaveEntry
            {
                territoryId = "territory_1",
                slots = new System.Collections.Generic.List<PlayerInventory.ItemSlot>
                {
                    new PlayerInventory.ItemSlot { item = PlayerInventory.Herb_Red, count = 7, currentDurability = 0 },
                    new PlayerInventory.ItemSlot { item = PlayerInventory.Herb_Yellow, count = 3, currentDurability = 0 }
                }
            };
            saveData.warehouseData.Add(entry);

            // Act
            WarehouseSystem.Instance.LoadFromSaveData(saveData);

            // Assert
            var items = WarehouseSystem.Instance.GetItems("territory_1");
            Assert.AreEqual(2, items.Count, "로드 후 2개 아이템이 있어야 함");
            Assert.AreEqual("herb_red", items[0].item.id);
            Assert.AreEqual(7, items[0].count);
            Assert.AreEqual("herb_yellow", items[1].item.id);
            Assert.AreEqual(3, items[1].count);
        }
    }
}
