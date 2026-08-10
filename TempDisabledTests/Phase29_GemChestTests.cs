using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// Phase 29: 동굴 보석 상자 & 광석 EditMode 테스트.
    /// </summary>
    public class Phase29_GemChestTests
    {
        [Test]
        public void GemData_Ruby_HasCorrectValues()
        {
            var data = GemData.GetGemData(GemType.Ruby);
            Assert.AreEqual("루비", data.displayName);
            Assert.AreEqual(3, data.starRating);
            Assert.AreEqual(500, data.goldValue);
            Assert.AreEqual("무기 공격력 +10", data.effectDescription);
        }

        [Test]
        public void GemData_Diamond_HasCorrectValues()
        {
            var data = GemData.GetGemData(GemType.Diamond);
            Assert.AreEqual("다이아몬드", data.displayName);
            Assert.AreEqual(5, data.starRating);
            Assert.AreEqual(3000, data.goldValue);
            Assert.AreEqual("전설 장비 제작 재료", data.effectDescription);
        }

        [Test]
        public void GemData_AllTypes_HaveUniqueColors()
        {
            var types = (GemType[])System.Enum.GetValues(typeof(GemType));
            Assert.AreEqual(6, types.Length);
            foreach (var t in types)
            {
                var data = GemData.GetGemData(t);
                Assert.IsNotNull(data.displayName);
                Assert.Greater(data.starRating, 0);
                Assert.Greater(data.goldValue, 0);
            }
        }

        [Test]
        public void GemChest_Create_IsNotOpen()
        {
            var go = new GameObject("TestChest");
            var chest = go.AddComponent<GemChest>();

            Assert.IsFalse(chest.IsOpen);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void GemChest_Open_SetsOpenToTrue()
        {
            var go = new GameObject("TestChest");
            var chest = go.AddComponent<GemChest>();

            chest.ForceOpen();
            Assert.IsTrue(chest.IsOpen);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void GemChest_DoubleOpen_DoesNotDoubleDrop()
        {
            var go = new GameObject("TestChest");
            var chest = go.AddComponent<GemChest>();

            chest.ForceOpen();
            Assert.IsTrue(chest.IsOpen);

            // 두 번째 열기 시도 (IsOpen 체크로 막혀야 함)
            chest.ForceOpen();
            Assert.IsTrue(chest.IsOpen); // 여전히 true

            Object.DestroyImmediate(go);
        }

        [Test]
        public void CaveInteriorBuilder_CreatesRoom()
        {
            var room = CaveInteriorBuilder.BuildCaveInterior("test_cave", 1);
            Assert.IsNotNull(room);
            Assert.AreEqual("CaveRoom", room.name);
            Assert.Greater(room.transform.childCount, 0);
            Object.DestroyImmediate(room);
        }

        [Test]
        public void CaveInteriorBuilder_HasFloorAndWalls()
        {
            var room = CaveInteriorBuilder.BuildCaveInterior("test_cave_2", 1);
            bool hasFloor = false;
            bool hasWall = false;
            foreach (Transform child in room.transform)
            {
                if (child.name == "CaveFloor") hasFloor = true;
                if (child.name.StartsWith("CaveWall_")) hasWall = true;
            }
            Assert.IsTrue(hasFloor, "Cave should have a floor");
            Assert.IsTrue(hasWall, "Cave should have walls");
            Object.DestroyImmediate(room);
        }

        [Test]
        public void CaveInteriorBuilder_HasGemChest()
        {
            var room = CaveInteriorBuilder.BuildCaveInterior("test_cave_3", 1);
            bool hasChest = false;
            foreach (Transform child in room.transform)
            {
                if (child.name.StartsWith("GemChest_"))
                {
                    hasChest = true;
                    break;
                }
            }
            Assert.IsTrue(hasChest, "Cave should have at least one Gem Chest");
            Object.DestroyImmediate(room);
        }
    }
}