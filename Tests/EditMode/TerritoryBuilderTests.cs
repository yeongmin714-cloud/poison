using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;
using ProjectName.Core.Data;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C9-02~04 영지 건물 & 병사 Placeholder 배치 테스트
    /// 새로운 82개 영지 빌드 동작에 맞춰 업데이트됨
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

        // ===================== 헬퍼 메서드 =====================

        /// <summary>
        /// 테스트용 TerritoryBuilder 세팅 및 정리 액션 반환
        /// </summary>
        private (GameObject mgrGo, TerritoryBuilder builder, System.Action cleanup) SetupBuilder()
        {
            var mgrGo = new GameObject("TestManager");
            mgrGo.AddComponent<TerritoryManager>();
            var builder = mgrGo.AddComponent<TerritoryBuilder>();
            
            System.Action cleanup = () =>
            {
                // 모든 Territory_* 부모 오브젝트 정리
                var allTransforms = Object.FindObjectsOfType<Transform>();
                foreach (var t in allTransforms)
                {
                    if (t.name.StartsWith("Territory_"))
                    {
                        Object.DestroyImmediate(t.gameObject);
                    }
                }
                Object.DestroyImmediate(mgrGo);
            };
            
            return (mgrGo, builder, cleanup);
        }

        /// <summary>
        /// 특정 영지 부모(GameObject) 찾기
        /// </summary>
        private GameObject FindTerritoryParent(string nation, int index)
        {
            string parentName = $"Territory_{nation}_{index:D2}";
            var allTransforms = Object.FindObjectsOfType<Transform>();
            foreach (var t in allTransforms)
            {
                if (t.name == parentName)
                    return t.gameObject;
            }
            return null;
        }

        // ===================== 건물 생성 테스트 (단일 영지 기준) =====================

        [Test]
        public void TerritoryBuilder_BuildBuildings_CorrectCount()
        {
            var (mgrGo, builder, cleanup) = SetupBuilder();
            
            try
            {
                // 수동으로 건물 생성 (이제 전체 82개 영지 생성)
                builder.BuildTerritory();

                // 동방 1번 영지(Territory_East_01) 내부 건물만 카운트
                var east01Parent = FindTerritoryParent("East", 1);
                Assert.IsNotNull(east01Parent, "Territory_East_01 부모 오브젝트가 생성되어야 합니다");

                var buildings = east01Parent.GetComponentsInChildren<BuildingPlaceholder>();
                // Shop + CraftHouse + Church + NPCHouse1~4 + TownSquare = 7개 (TownSquare는 BuildingType.Other)
                Assert.AreEqual(7, buildings.Length, "각 영지당 7개의 건물이 생성되어야 합니다 (C9-02:3 + C9-03:4)");

                // 건물 타입 확인
                int shopCount = 0, craftCount = 0, churchCount = 0, houseCount = 0, squareCount = 0;
                foreach (var b in buildings)
                {
                    switch (b.buildingType)
                    {
                        case BuildingPlaceholder.BuildingType.Shop: shopCount++; break;
                        case BuildingPlaceholder.BuildingType.CraftHouse: craftCount++; break;
                        case BuildingPlaceholder.BuildingType.Church: churchCount++; break;
                        case BuildingPlaceholder.BuildingType.NPCHouse: houseCount++; break;
                        case BuildingPlaceholder.BuildingType.Other: squareCount++; break;
                    }
                }
                Assert.AreEqual(1, shopCount, "상점 1개");
                Assert.AreEqual(1, craftCount, "크래프트하우스 1개");
                Assert.AreEqual(1, churchCount, "교회 1개");
                Assert.AreEqual(4, houseCount, "NPC 주택 4채");
                Assert.AreEqual(1, squareCount, "중앙 광장 1개 (Other 타입)");
            }
            finally
            {
                cleanup();
            }
        }

        [Test]
        public void TerritoryBuilder_BuildBuildings_HasLabels()
        {
            var (mgrGo, builder, cleanup) = SetupBuilder();
            
            try
            {
                builder.BuildTerritory();

                var east01Parent = FindTerritoryParent("East", 1);
                Assert.IsNotNull(east01Parent);

                var buildings = east01Parent.GetComponentsInChildren<BuildingPlaceholder>();
                foreach (var b in buildings)
                {
                    var textMesh = b.GetComponentInChildren<TextMesh>();
                    Assert.IsNotNull(textMesh, $"건물 '{b.name}'에 TextMesh 라벨이 있어야 합니다");
                    Assert.IsNotEmpty(textMesh.text, "라벨 텍스트가 비어있지 않아야 합니다");
                }
            }
            finally
            {
                cleanup();
            }
        }

        // ===================== 병사 생성 테스트 (C9-04) =====================

        [Test]
        public void TerritoryBuilder_BuildGuards_CorrectCount()
        {
            var (mgrGo, builder, cleanup) = SetupBuilder();
            
            try
            {
                builder.BuildTerritory();

                var east01Parent = FindTerritoryParent("East", 1);
                Assert.IsNotNull(east01Parent);

                var guards = east01Parent.GetComponentsInChildren<GuardPlaceholder>();
                // 동방 Ring1 = 3명
                Assert.AreEqual(3, guards.Length, "동방 1번 영지(Ring1)는 3명의 병사가 있어야 합니다 (C9-04)");

                // 이름 확인 (Guard_1, Guard_2, Guard_3)
                Assert.IsTrue(guards[0].name.StartsWith("Guard_"), "병사 이름이 'Guard_'로 시작해야 합니다");
            }
            finally
            {
                cleanup();
            }
        }

        [Test]
        public void TerritoryBuilder_Guards_HaveNationEast()
        {
            var (mgrGo, builder, cleanup) = SetupBuilder();
            
            try
            {
                builder.BuildTerritory();

                var east01Parent = FindTerritoryParent("East", 1);
                Assert.IsNotNull(east01Parent);

                var guards = east01Parent.GetComponentsInChildren<GuardPlaceholder>();
                Assert.GreaterOrEqual(guards.Length, 1, "최소 1명 이상의 병사가 있어야 합니다");
                
                // 모든 병사가 동쪽 국가 소속인지 확인 (GuardPlaceholder.SetGuardInfo로 설정된 nation string 확인)
                foreach (var guard in guards)
                {
                    var nationField = typeof(GuardPlaceholder).GetField("nation",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    Assert.IsNotNull(nationField, "GuardPlaceholder에 nation 필드가 있어야 합니다");
                    string nation = (string)nationField.GetValue(guard);
                    Assert.AreEqual("동", nation, "동방 영지의 병사는 '동' 국가 소속이어야 합니다");
                }
            }
            finally
            {
                cleanup();
            }
        }

        [Test]
        public void TerritoryBuilder_Guards_HaveLabels()
        {
            var (mgrGo, builder, cleanup) = SetupBuilder();
            
            try
            {
                builder.BuildTerritory();

                var east01Parent = FindTerritoryParent("East", 1);
                Assert.IsNotNull(east01Parent);

                var guards = east01Parent.GetComponentsInChildren<GuardPlaceholder>();
                foreach (var guard in guards)
                {
                    var textMesh = guard.GetComponentInChildren<TextMesh>();
                    Assert.IsNotNull(textMesh, $"병사 '{guard.name}'에 TextMesh 라벨이 있어야 합니다");
                    Assert.IsTrue(textMesh.text.Contains("Lv."), "라벨에 레벨 정보가 포함되어야 합니다");
                }
            }
            finally
            {
                cleanup();
            }
        }

        // ===================== 중복 방지 테스트 =====================

        [Test]
        public void TerritoryBuilder_DoesNotDuplicate()
        {
            var (mgrGo, builder, cleanup) = SetupBuilder();
            
            try
            {
                builder.BuildTerritory();
                
                // 첫 번째 빌드 후 Territory_* 부모 개수
                int firstParentCount = 0;
                var allTransforms = Object.FindObjectsOfType<Transform>();
                foreach (var t in allTransforms)
                {
                    if (t.name.StartsWith("Territory_"))
                        firstParentCount++;
                }
                Assert.AreEqual(82, firstParentCount, "첫 빌드 시 82개 영지 부모가 생성되어야 합니다");

                // 두 번째 호출 (_hasBuilt 플래그로 인해 아무 것도 안 함)
                builder.BuildTerritory();
                
                int secondParentCount = 0;
                allTransforms = Object.FindObjectsOfType<Transform>();
                foreach (var t in allTransforms)
                {
                    if (t.name.StartsWith("Territory_"))
                        secondParentCount++;
                }
                
                Assert.AreEqual(firstParentCount, secondParentCount, "두 번째 BuildTerritory 호출 시 중복 생성되지 않아야 합니다");
            }
            finally
            {
                cleanup();
            }
        }

        // ===================== ClearAll 테스트 =====================

        [Test]
        public void TerritoryBuilder_ClearAll_RemovesPlaceholders()
        {
            var (mgrGo, builder, cleanup) = SetupBuilder();
            
            try
            {
                builder.BuildTerritory();

                builder.ClearAll();

                int buildingCount = Object.FindObjectsOfType<BuildingPlaceholder>().Length;
                int guardCount = Object.FindObjectsOfType<GuardPlaceholder>().Length;
                int territoryParentCount = 0;
                var allTransforms = Object.FindObjectsOfType<Transform>();
                foreach (var t in allTransforms)
                {
                    if (t.name.StartsWith("Territory_"))
                        territoryParentCount++;
                }
                
                Assert.AreEqual(0, buildingCount, "ClearAll 후 건물이 없어야 합니다");
                Assert.AreEqual(0, guardCount, "ClearAll 후 병사가 없어야 합니다");
                Assert.AreEqual(0, territoryParentCount, "ClearAll 후 Territory_* 부모 오브젝트도 없어야 합니다");
            }
            finally
            {
                // ClearAll이 이미 정리했으므로 mgrGo만 정리
                Object.DestroyImmediate(mgrGo);
            }
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

            // 정리: GameManager를 먼저 제거한 후 Territory_* 부모와 TerritoryManager GO 정리
            Object.DestroyImmediate(go);

            // GameManager가 생성한 TerritoryManager GO 제거
            var tmGo = GameObject.Find("TerritoryManager");
            if (tmGo != null) Object.DestroyImmediate(tmGo);

            // TerritoryBuilder가 생성한 82개 영지 부모 정리
            var allTransforms = Object.FindObjectsOfType<Transform>();
            foreach (var t in allTransforms)
            {
                if (t.name.StartsWith("Territory_"))
                    Object.DestroyImmediate(t.gameObject);
            }
        }

        // ===================== 새로운 82개 영지 테스트 =====================

        [Test]
        public void TerritoryBuilder_AllTerritories_Has82Parents()
        {
            var (mgrGo, builder, cleanup) = SetupBuilder();
            
            try
            {
                builder.BuildTerritory();

                int territoryParentCount = 0;
                var allTransforms = Object.FindObjectsOfType<Transform>();
                foreach (var t in allTransforms)
                {
                    if (t.name.StartsWith("Territory_"))
                        territoryParentCount++;
                }
                
                // 4개 국가 × 20개 + 황제국 1 + 드라큘라 1 = 82
                Assert.AreEqual(82, territoryParentCount, "전체 82개 영지 부모 오브젝트가 생성되어야 합니다 (East/West/South/North 각 20 + Empire 1 + Dracula 1)");
            }
            finally
            {
                cleanup();
            }
        }

        [Test]
        public void TerritoryDatabase_WorldPosition_RingDistance()
        {
            var db = TerritoryDatabase.Instance;
            
            // 동방 Ring1 (index 1~5) - 거리 약 1450m
            var eastRing1 = db.GetDefinition(NationType.East, 1);
            float eastRing1Dist = eastRing1.worldPosition.magnitude;
            Assert.AreEqual(1450f, eastRing1Dist, 10f, "동방 Ring1 영지는 중심에서 약 1450m 거리에 있어야 합니다");

            // 동방 Ring4 (index 16~20) - 거리 약 150m
            var eastRing4 = db.GetDefinition(NationType.East, 16);
            float eastRing4Dist = eastRing4.worldPosition.magnitude;
            Assert.AreEqual(150f, eastRing4Dist, 10f, "동방 Ring4 영지는 중심에서 약 150m 거리에 있어야 합니다");

            // 황제국 - 중심 (0,0,0)
            var empire = db.GetDefinition(NationType.Empire, 1);
            Assert.AreEqual(Vector3.zero, empire.worldPosition, "황제국은 월드 중심 (0,0,0)에 있어야 합니다");

            // 드라큘라 - 약 1350m 거리 (North-North-East 방향)
            var dracula = db.GetDefinition(NationType.Dracula, 1);
            float draculaDist = dracula.worldPosition.magnitude;
            Assert.AreEqual(1350f, draculaDist, 10f, "드라큘라 영지는 중심에서 약 1350m 거리에 있어야 합니다");
        }

        [Test]
        public void TerritoryBuilder_Empire_Has50Guards()
        {
            var (mgrGo, builder, cleanup) = SetupBuilder();
            
            try
            {
                builder.BuildTerritory();

                var empireParent = FindTerritoryParent("Empire", 1);
                Assert.IsNotNull(empireParent, "Territory_Empire_01 부모 오브젝트가 생성되어야 합니다");

                var guards = empireParent.GetComponentsInChildren<GuardPlaceholder>();
                // 황제국 = 50명
                Assert.AreEqual(50, guards.Length, "황제국 영지는 50명의 친위대가 있어야 합니다");
            }
            finally
            {
                cleanup();
            }
        }
    }
}