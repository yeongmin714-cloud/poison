using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C-O2-03b: 창고 슬롯 확장 시스템 테스트.
    /// 용량 공식 / 비용 표 / SaveData 라운드트립 / 경계 / 지갑 미연결 안전 실패.
    /// PlayerStats 싱글턴은 EditMode에서 미생성 — 골드 차감 경로는 Play 판정에서 검증.
    /// </summary>
    public class WarehouseExpansionTests
    {
        private WarehouseSystem _ws;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("WarehouseSystemTest");
            _ws = go.AddComponent<WarehouseSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_ws != null && _ws.gameObject != null)
                Object.DestroyImmediate(_ws.gameObject);
        }

        [Test]
        public void Capacity_Level0_ReturnsBase20()
        {
            Assert.AreEqual(20, _ws.GetSlotCapacity("test_terr"));
        }

        [Test]
        public void Capacity_ExpansionLevel2_Returns30()
        {
            var data = new WarehouseSaveData
            {
                warehouseData = { new WarehouseSaveEntry { territoryId = "test_terr", expansionLevel = 2 } }
            };
            _ws.LoadFromSaveData(data);
            Assert.AreEqual(30, _ws.GetSlotCapacity("test_terr"), "기본20 + 5×2");
        }

        [Test]
        public void SaveLoad_Roundtrip_PreservesExpansionLevel()
        {
            var data = new WarehouseSaveData
            {
                warehouseData = { new WarehouseSaveEntry { territoryId = "rt_terr", expansionLevel = 1 } }
            };
            _ws.LoadFromSaveData(data);

            var saved = _ws.GetSaveData();
            int found = 0;
            foreach (var entry in saved.warehouseData)
            {
                if (entry.territoryId == "rt_terr")
                {
                    found++;
                    Assert.AreEqual(1, entry.expansionLevel);
                }
            }
            Assert.AreEqual(1, found, "라운드트립 후 확장 단계 보존");
        }

        [Test]
        public void LoadFromSaveData_AboveMax_ClampedToMax()
        {
            var data = new WarehouseSaveData
            {
                warehouseData = { new WarehouseSaveEntry { territoryId = "clamp_terr", expansionLevel = 99 } }
            };
            _ws.LoadFromSaveData(data);
            Assert.AreEqual(WarehouseSystem.MaxExpansions, _ws.GetExpansionLevel("clamp_terr"), "비정상 단계 클램프");
            Assert.AreEqual(35, _ws.GetSlotCapacity("clamp_terr"), "최대 확장 용량 35");
        }

        [Test]
        public void CostTable_100_200_300()
        {
            Assert.AreEqual(100, _ws.GetNextExpansionCost("c1"), "0단계 → 100G");

            var data = new WarehouseSaveData
            {
                warehouseData = { new WarehouseSaveEntry { territoryId = "c1", expansionLevel = 2 } }
            };
            _ws.LoadFromSaveData(data);
            Assert.AreEqual(300, _ws.GetNextExpansionCost("c1"), "2단계 → 300G");
        }

        [Test]
        public void CanExpand_Boundary_Level3_False()
        {
            var data = new WarehouseSaveData
            {
                warehouseData = { new WarehouseSaveEntry { territoryId = "max_terr", expansionLevel = 3 } }
            };
            _ws.LoadFromSaveData(data);
            Assert.IsFalse(_ws.CanExpand("max_terr"), "3단계 = 최대");
            Assert.IsTrue(_ws.CanExpand("fresh_terr"), "미확장 영지는 확장 가능");
        }

        [Test]
        public void TryExpandSlots_NoWallet_FailsSafely()
        {
            // EditMode에서 PlayerStats.Instance == null → 안전 실패 (예외 없음)
            string result = _ws.TryExpandSlots("nosave_terr");
            StringAssert.Contains("지갑", result);
            Assert.AreEqual(0, _ws.GetExpansionLevel("nosave_terr"), "실패 시 단계 불변");
        }

        [Test]
        public void TryExpandSlots_MaxReached_Message()
        {
            var data = new WarehouseSaveData
            {
                warehouseData = { new WarehouseSaveEntry { territoryId = "full_terr", expansionLevel = 3 } }
            };
            _ws.LoadFromSaveData(data);
            StringAssert.Contains("최대", _ws.TryExpandSlots("full_terr"));
        }

        [Test]
        public void GetExpansionLevel_UnknownOrEmpty_Returns0()
        {
            Assert.AreEqual(0, _ws.GetExpansionLevel("never_seen"));
            Assert.AreEqual(0, _ws.GetExpansionLevel(null));
            Assert.AreEqual(0, _ws.GetExpansionLevel(""));
        }

        [Test]
        public void Clear_ResetsExpansionLevels()
        {
            var data = new WarehouseSaveData
            {
                warehouseData = { new WarehouseSaveEntry { territoryId = "clr_terr", expansionLevel = 2 } }
            };
            _ws.LoadFromSaveData(data);
            Assert.AreEqual(2, _ws.GetExpansionLevel("clr_terr"));

            _ws.Clear();
            Assert.AreEqual(0, _ws.GetExpansionLevel("clr_terr"), "Clear 시 확장 단계 초기화");
            Assert.AreEqual(20, _ws.GetSlotCapacity("clr_terr"));
        }
    }
}
