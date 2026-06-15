using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;
using ProjectName.Core.Data;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C9-02~04 영지 건물 & 병사 Placeholder 배치 테스트
    /// </summary>
    public class TerritoryBuilderTests
    {
        // ===================== TerritoryBuilder 기본 테스트 =====================

        [Test]
        public void TerritoryBuilder_Type_Exists()
        {
            Assert.IsNotNull(typeof(TerritoryBuilder), "TerritoryBuilder 타입이 존재해야 합니다");
        }

        [Test]
        public void TerritoryBuilder_IsMonoBehaviour()
        {
            Assert.IsTrue(typeof(TerritoryBuilder).IsSubclassOf(typeof(MonoBehaviour)),
                "TerritoryBuilder는 MonoBehaviour를 상속해야 합니다");
        }

        [Test]
        public void TerritoryBuilder_HasRequireComponent_TerritoryManager()
        {
            var attributes = typeof(TerritoryBuilder).GetCustomAttributes(typeof(RequireComponent), false);
            bool hasManagerReq = false;
            foreach (RequireComponent attr in attributes)
            {
                if (attr.m_Type0 == typeof(TerritoryManager))
                {
                    hasManagerReq = true;
                    break;
                }
            }
            Assert.IsTrue(hasManagerReq, "TerritoryBuilder에 [RequireComponent(typeof(TerritoryManager))]가 있어야 합니다");
        }

        // ===================== 건물 생성 테스트 =====================

        [Test]
        public void TerritoryBuilder_BuildBuildings_CorrectCount()
        {
            // TerritoryManager 생성
            var mgrGo = new GameObject("TestManager");
            mgrGo.AddComponent<TerritoryManager>();
            var builder = mgrGo.AddComponent<TerritoryBuilder>();

            // 수동으로 건물 생성
            builder.BuildTerritory();

            var buildings = Object.FindObjectsOfType<BuildingPlaceholder>();
            // Shop + CraftHouse + Church + NPCHouse1~4 = 7개
            Assert.AreEqual(7, buildings.Length, "7개의 건물이 생성되어야 합니다 (C9-02:3 + C9-03:4)");

            // 건물 타입 확인
            int shopCount = 0, craftCount = 0, churchCount = 0, houseCount = 0;
            foreach (var b in buildings)
            {
                switch (b.buildingType)
                {
                    case BuildingPlaceholder.BuildingType.Shop: shopCount++; break;
                    case BuildingPlaceholder.BuildingType.CraftHouse: craftCount++; break;
                    case BuildingPlaceholder.BuildingType.Church: churchCount++; break;
                    case BuildingPlaceholder.BuildingType.NPCHouse: houseCount++; break;
                }
            }
            Assert.AreEqual(1, shopCount, "상점 1개");
            Assert.AreEqual(1, craftCount, "크래프트하우스 1개");
            Assert.AreEqual(1, churchCount, "교회 1개");
            Assert.AreEqual(4, houseCount, "NPC 주택 4채");

            // 정리
            Object.DestroyImmediate(mgrGo);
        }

        [Test]
        public void TerritoryBuilder_BuildBuildings_HasLabels()
        {
            var mgrGo = new GameObject("TestManager");
            mgrGo.AddComponent<TerritoryManager>();
            var builder = mgrGo.AddComponent<TerritoryBuilder>();
            builder.BuildTerritory();

            var buildings = Object.FindObjectsOfType<BuildingPlaceholder>();
            foreach (var b in buildings)
            {
                var textMesh = b.GetComponentInChildren<TextMesh>();
                Assert.IsNotNull(textMesh, $"건물 '{b.name}'에 TextMesh 라벨이 있어야 합니다");
                Assert.IsNotEmpty(textMesh.text, "라벨 텍스트가 비어있지 않아야 합니다");
            }

            Object.DestroyImmediate(mgrGo);
        }

        // ===================== 병사 생성 테스트 (C9-04) =====================

        [Test]
        public void TerritoryBuilder_BuildGuards_CorrectCount()
        {
            var mgrGo = new GameObject("TestManager");
            mgrGo.AddComponent<TerritoryManager>();
            var builder = mgrGo.AddComponent<TerritoryBuilder>();
            builder.BuildTerritory();

            var guards = Object.FindObjectsOfType<GuardPlaceholder>();
            // Guard_Entrance1~3 = 3명
            Assert.AreEqual(3, guards.Length, "3명의 병사가 생성되어야 합니다 (C9-04)");

            // 이름 확인
            Assert.IsTrue(guards[0].name.StartsWith("Guard_"), "병사 이름이 'Guard_'로 시작해야 합니다");

            Object.DestroyImmediate(mgrGo);
        }

        [Test]
        public void TerritoryBuilder_Guards_HaveNationEast()
        {
            var mgrGo = new GameObject("TestManager");
            mgrGo.AddComponent<TerritoryManager>();
            var builder = mgrGo.AddComponent<TerritoryBuilder>();
            builder.BuildTerritory();

            var guards = Object.FindObjectsOfType<GuardPlaceholder>();
            Assert.GreaterOrEqual(guards.Length, 1, "최소 1명 이상의 병사가 있어야 합니다");
            // 모든 병사가 동쪽 국가 소속인지 확인 (리플렉션으로 private field 확인)
            foreach (var guard in guards)
            {
                var nationField = typeof(GuardPlaceholder).GetField("nation",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.IsNotNull(nationField, "GuardPlaceholder에 nation 필드가 있어야 합니다");
                string nation = (string)nationField.GetValue(guard);
                Assert.AreEqual("동", nation, "병사는 '동' 국가 소속이어야 합니다");
            }

            Object.DestroyImmediate(mgrGo);
        }

        [Test]
        public void TerritoryBuilder_Guards_HaveLabels()
        {
            var mgrGo = new GameObject("TestManager");
            mgrGo.AddComponent<TerritoryManager>();
            var builder = mgrGo.AddComponent<TerritoryBuilder>();
            builder.BuildTerritory();

            var guards = Object.FindObjectsOfType<GuardPlaceholder>();
            foreach (var guard in guards)
            {
                var textMesh = guard.GetComponentInChildren<TextMesh>();
                Assert.IsNotNull(textMesh, $"병사 '{guard.name}'에 TextMesh 라벨이 있어야 합니다");
                Assert.IsTrue(textMesh.text.Contains("Lv."), "라벨에 레벨 정보가 포함되어야 합니다");
            }

            Object.DestroyImmediate(mgrGo);
        }

        // ===================== 중복 방지 테스트 =====================

        [Test]
        public void TerritoryBuilder_DoesNotDuplicate()
        {
            var mgrGo = new GameObject("TestManager");
            mgrGo.AddComponent<TerritoryManager>();
            var builder = mgrGo.AddComponent<TerritoryBuilder>();

            builder.BuildTerritory();
            int firstCount = Object.FindObjectsOfType<BuildingPlaceholder>().Length;

            // 두 번째 호출
            builder.BuildTerritory();
            int secondCount = Object.FindObjectsOfType<BuildingPlaceholder>().Length;

            Assert.AreEqual(firstCount, secondCount, "두 번째 BuildTerritory 호출 시 중복 생성되지 않아야 합니다");

            Object.DestroyImmediate(mgrGo);
        }

        // ===================== ClearAll 테스트 =====================

        [Test]
        public void TerritoryBuilder_ClearAll_RemovesPlaceholders()
        {
            var mgrGo = new GameObject("TestManager");
            mgrGo.AddComponent<TerritoryManager>();
            var builder = mgrGo.AddComponent<TerritoryBuilder>();
            builder.BuildTerritory();

            builder.ClearAll();

            int buildingCount = Object.FindObjectsOfType<BuildingPlaceholder>().Length;
            int guardCount = Object.FindObjectsOfType<GuardPlaceholder>().Length;
            Assert.AreEqual(0, buildingCount, "ClearAll 후 건물이 없어야 합니다");
            Assert.AreEqual(0, guardCount, "ClearAll 후 병사가 없어야 합니다");

            Object.DestroyImmediate(mgrGo);
        }

        // ===================== GameManager 통합 테스트 =====================

        [Test]
        public void GameManager_CreatesTerritoryManager()
        {
            var go = new GameObject("TestGameManager");
            var gm = go.AddComponent<Core.GameManager>();

            // Start() 호출
            var startMethod = typeof(Core.GameManager).GetMethod("Start",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (startMethod != null)
                startMethod.Invoke(gm, null);

            // TerritoryManager가 생성되었는지 확인
            var mgr = TerritoryManager.Instance;
            Assert.IsNotNull(mgr, "GameManager.Start() 후 TerritoryManager.Instance가 null이 아니어야 합니다");

            // TerritoryBuilder도 함께 생성
            var builder = mgr.GetComponent<TerritoryBuilder>();
            Assert.IsNotNull(builder, "TerritoryManager와 함께 TerritoryBuilder가 생성되어야 합니다");

            Object.DestroyImmediate(go);
        }
    }
}