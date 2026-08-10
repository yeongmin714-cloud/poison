using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// Phase 25: 선술집 & 용병 시스템 EditMode 테스트.
    /// </summary>
    public class Phase25_MercenaryTests
    {
        [Test]
        public void MercenaryData_DefaultValues_AreValid()
        {
            var data = new MercenaryData(
                "test_01", "테스트용병", MercenaryGrade.Normal,
                100f, 15f, 10f, 4f, 200,
                "테스트 능력", "테스트 배경 스토리", "Soldier"
            );

            Assert.AreEqual("test_01", data.id);
            Assert.AreEqual("테스트용병", data.mercenaryName);
            Assert.AreEqual(MercenaryGrade.Normal, data.grade);
            Assert.AreEqual(100f, data.maxHP);
            Assert.AreEqual(15f, data.attack);
            Assert.AreEqual(10f, data.defense);
            Assert.AreEqual(4f, data.moveSpeed);
            Assert.AreEqual(200, data.hireCost);
            Assert.AreEqual("테스트 능력", data.specialAbility);
            Assert.AreEqual("테스트 배경 스토리", data.backStory);
            Assert.AreEqual("Soldier", data.jobType);
        }

        [Test]
        public void MercenaryData_GradeStars_ReturnsCorrectFormat()
        {
            Assert.AreEqual("★", new MercenaryData("t", "t", MercenaryGrade.Normal, 0, 0, 0, 0, 0, "", "", "").GradeStars);
            Assert.AreEqual("★★", new MercenaryData("t", "t", MercenaryGrade.High, 0, 0, 0, 0, 0, "", "", "").GradeStars);
            Assert.AreEqual("★★★", new MercenaryData("t", "t", MercenaryGrade.Elite, 0, 0, 0, 0, 0, "", "", "").GradeStars);
            Assert.AreEqual("★★★★", new MercenaryData("t", "t", MercenaryGrade.Legendary, 0, 0, 0, 0, 0, "", "", "").GradeStars);
        }

        [Test]
        public void MercenaryData_StatMultiplier_ScalesWithGrade()
        {
            Assert.AreEqual(1.5f, new MercenaryData("t", "t", MercenaryGrade.Normal, 0, 0, 0, 0, 0, "", "", "").StatMultiplier);
            Assert.AreEqual(2.0f, new MercenaryData("t", "t", MercenaryGrade.High, 0, 0, 0, 0, 0, "", "", "").StatMultiplier);
            Assert.AreEqual(2.5f, new MercenaryData("t", "t", MercenaryGrade.Elite, 0, 0, 0, 0, 0, "", "", "").StatMultiplier);
            Assert.AreEqual(3.0f, new MercenaryData("t", "t", MercenaryGrade.Legendary, 0, 0, 0, 0, 0, "", "", "").StatMultiplier);
        }

        [Test]
        public void MercenaryManager_Singleton_Works()
        {
            var go = new GameObject("TestMercManager");
            var manager = go.AddComponent<MercenaryManager>();

            Assert.IsNotNull(MercenaryManager.Instance);
            Assert.AreEqual(manager, MercenaryManager.Instance);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void MercenaryManager_InitializeDatabase_HasEntries()
        {
            var go = new GameObject("TestMercManager");
            var manager = go.AddComponent<MercenaryManager>();

            // 리플렉션으로 database 확인
            var dbField = typeof(MercenaryManager).GetField("_mercenaryDatabase",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(dbField);
            var db = dbField.GetValue(manager) as System.Collections.Generic.Dictionary<string, MercenaryData>;
            Assert.IsNotNull(db);
            Assert.Greater(db.Count, 0); // 최소한 몇 개의 용병이 등록되어야 함

            Object.DestroyImmediate(go);
        }

        [Test]
        public void BardMercenary_HasCorrectDefaults()
        {
            var go = new GameObject("TestBard");
            var bard = go.AddComponent<BardMercenary>();

            Assert.AreEqual(15f, bard.BuffRange);
            Assert.IsTrue(bard.IsActive);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void MercenaryPlaceholder_CreatesGoldMaterial()
        {
            var data = new MercenaryData("t", "t", MercenaryGrade.Normal, 0, 0, 0, 0, 0, "", "", "Soldier");
            var go = MercenaryPlaceholder.CreateMercenaryPlaceholder(data, Vector3.zero);

            Assert.IsNotNull(go);
            Assert.AreEqual("Mercenary_t", go.name);

            var renderer = go.GetComponent<MeshRenderer>();
            Assert.IsNotNull(renderer);
            Assert.IsNotNull(renderer.material);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void MercenaryPlaceholder_DifferentGrades_HaveDifferentColors()
        {
            var normal = MercenaryPlaceholder.CreateMercenaryPlaceholder(
                new MercenaryData("n", "n", MercenaryGrade.Normal, 0, 0, 0, 0, 0, "", "", "Soldier"), Vector3.zero);
            var legendary = MercenaryPlaceholder.CreateMercenaryPlaceholder(
                new MercenaryData("l", "l", MercenaryGrade.Legendary, 0, 0, 0, 0, 0, "", "", "Soldier"), Vector3.one);

            var normalColor = normal.GetComponent<MeshRenderer>().material.color;
            var legendaryColor = legendary.GetComponent<MeshRenderer>().material.color;

            Assert.AreNotEqual(normalColor, legendaryColor);

            Object.DestroyImmediate(normal);
            Object.DestroyImmediate(legendary);
        }

        [Test]
        public void TavernInteriorBuilder_CreatesRoom()
        {
            var room = TavernInteriorBuilder.BuildTavernInterior("test_tavern", 1);

            Assert.IsNotNull(room);
            Assert.AreEqual("TavernRoom", room.name);
            Assert.Greater(room.transform.childCount, 0);

            Object.DestroyImmediate(room);
        }

        [Test]
        public void TavernInteriorBuilder_HasFloorAndWalls()
        {
            var room = TavernInteriorBuilder.BuildTavernInterior("test_tavern_2", 1);

            bool hasFloor = false;
            bool hasWall = false;
            foreach (Transform child in room.transform)
            {
                if (child.name == "Floor") hasFloor = true;
                if (child.name.StartsWith("Wall_")) hasWall = true;
            }

            Assert.IsTrue(hasFloor, "Tavern should have a Floor");
            Assert.IsTrue(hasWall, "Tavern should have walls");

            Object.DestroyImmediate(room);
        }

        [Test]
        public void TavernInteriorBuilder_HasCounterAndStage()
        {
            var room = TavernInteriorBuilder.BuildTavernInterior("test_tavern_3", 2);

            bool hasCounter = false;
            bool hasStage = false;
            foreach (Transform child in room.transform)
            {
                if (child.name == "Counter") hasCounter = true;
                if (child.name == "Stage") hasStage = true;
            }

            Assert.IsTrue(hasCounter, "Tavern should have a Counter");
            Assert.IsTrue(hasStage, "Tavern should have a Stage");

            Object.DestroyImmediate(room);
        }

        [Test]
        public void TavernInteriorBuilder_TierAffectsRoomSize()
        {
            var smallRoom = TavernInteriorBuilder.BuildTavernInterior("tier1", 1);
            var largeRoom = TavernInteriorBuilder.BuildTavernInterior("tier5", 5);

            var smallFloor = smallRoom.transform.Find("Floor");
            var largeFloor = largeRoom.transform.Find("Floor");

            Assert.IsNotNull(smallFloor);
            Assert.IsNotNull(largeFloor);

            // 큰 티어의 방이 더 넓어야 함
            Assert.Greater(largeFloor.localScale.x, smallFloor.localScale.x);

            Object.DestroyImmediate(smallRoom);
            Object.DestroyImmediate(largeRoom);
        }

        [Test]
        public void TavernInteriorBuilder_HasStageLight()
        {
            var room = TavernInteriorBuilder.BuildTavernInterior("test_light", 1);

            bool hasLight = false;
            foreach (Transform child in room.transform)
            {
                if (child.name.Contains("StageLight") || child.name.Contains("PointLight"))
                {
                    hasLight = true;
                    break;
                }
            }

            Assert.IsTrue(hasLight, "Tavern should have stage lighting");

            Object.DestroyImmediate(room);
        }
    }
}