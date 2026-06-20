using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Core.Systems;
using System.Collections.Generic;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// Phase 3.3: 81개 영지 정의 데이터 + 황제국 규칙 EditMode 테스트.
    /// </summary>
    public class Phase33_TerritoryDataTests
    {
        [SetUp]
        public void Setup()
        {
            // 싱글톤 초기화 (이미 초기화되었으면 재초기화를 위해 인스턴스 리셋)
            // TerritoryDatabase는 최초 접근 시 자동 초기화
            var _ = TerritoryDatabase.Instance;
        }

        [Test]
        public void TerritoryDatabase_TotalDefinitions_Is81()
        {
            var db = TerritoryDatabase.Instance;
            var allDefs = new List<TerritoryDefinition>(db.GetAllDefinitions());
            Assert.AreEqual(81, allDefs.Count, "총 81개 영지 정의가 있어야 함 (4국가×20 + 황제국1)");
        }

        [Test]
        public void TerritoryDatabase_EachNation_Has20Definitions()
        {
            var db = TerritoryDatabase.Instance;

            foreach (NationType nation in new[] { NationType.East, NationType.West, NationType.South, NationType.North })
            {
                var defs = new List<TerritoryDefinition>(db.GetDefinitionsByNation(nation));
                Assert.AreEqual(20, defs.Count, $"{nation} 국가는 20개 영지 정의가 있어야 함");
            }
        }

        [Test]
        public void TerritoryDatabase_Empire_Has1Definition()
        {
            var db = TerritoryDatabase.Instance;
            var empireDefs = new List<TerritoryDefinition>(db.GetDefinitionsByNation(NationType.Empire));
            Assert.AreEqual(1, empireDefs.Count, "황제국은 1개 영지 정의가 있어야 함");
        }

        [TestCase(NationType.East, 3)]
        [TestCase(NationType.West, 4)]
        [TestCase(NationType.South, 4)]
        [TestCase(NationType.North, 5)]
        public void TerritoryDatabase_Ring1_GuardCount_Matches(NationType nation, int expectedGuards)
        {
            var db = TerritoryDatabase.Instance;

            for (int i = 1; i <= 5; i++)
            {
                var def = db.GetDefinition(nation, i);
                Assert.AreEqual(TerritoryDifficulty.Ring1, def.difficulty,
                    $"{nation} Ring1 영지 #{i} 난이도 불일치");
                Assert.AreEqual(expectedGuards, def.guardCount,
                    $"{nation} Ring1 영지 #{i} 병사 수 불일치 (기대: {expectedGuards})");
            }
        }

        [TestCase(NationType.East, 5)]
        [TestCase(NationType.West, 7)]
        [TestCase(NationType.South, 8)]
        [TestCase(NationType.North, 10)]
        public void TerritoryDatabase_Ring2_GuardCount_Matches(NationType nation, int expectedGuards)
        {
            var db = TerritoryDatabase.Instance;

            for (int i = 6; i <= 10; i++)
            {
                var def = db.GetDefinition(nation, i);
                Assert.AreEqual(TerritoryDifficulty.Ring2, def.difficulty,
                    $"{nation} Ring2 영지 #{i} 난이도 불일치");
                Assert.AreEqual(expectedGuards, def.guardCount,
                    $"{nation} Ring2 영지 #{i} 병사 수 불일치 (기대: {expectedGuards})");
            }
        }

        [TestCase(NationType.East, 8)]
        [TestCase(NationType.West, 12)]
        [TestCase(NationType.South, 15)]
        [TestCase(NationType.North, 18)]
        public void TerritoryDatabase_Ring3_GuardCount_Matches(NationType nation, int expectedGuards)
        {
            var db = TerritoryDatabase.Instance;

            for (int i = 11; i <= 15; i++)
            {
                var def = db.GetDefinition(nation, i);
                Assert.AreEqual(TerritoryDifficulty.Ring3, def.difficulty,
                    $"{nation} Ring3 영지 #{i} 난이도 불일치");
                Assert.AreEqual(expectedGuards, def.guardCount,
                    $"{nation} Ring3 영지 #{i} 병사 수 불일치 (기대: {expectedGuards})");
            }
        }

        [TestCase(NationType.East, 12)]
        [TestCase(NationType.West, 18)]
        [TestCase(NationType.South, 25)]
        [TestCase(NationType.North, 35)]
        public void TerritoryDatabase_Ring4_GuardCount_Matches(NationType nation, int expectedGuards)
        {
            var db = TerritoryDatabase.Instance;

            for (int i = 16; i <= 20; i++)
            {
                var def = db.GetDefinition(nation, i);
                Assert.AreEqual(TerritoryDifficulty.Ring4, def.difficulty,
                    $"{nation} Ring4 영지 #{i} 난이도 불일치");
                Assert.AreEqual(expectedGuards, def.guardCount,
                    $"{nation} Ring4 영지 #{i} 병사 수 불일치 (기대: {expectedGuards})");
            }
        }

        [Test]
        public void TerritoryDatabase_Empire_GuardCount_Is50()
        {
            var db = TerritoryDatabase.Instance;
            var def = db.GetDefinition(NationType.Empire, 1);
            Assert.AreEqual(TerritoryDifficulty.Empire, def.difficulty, "황제국 난이도는 Empire여야 함");
            Assert.AreEqual(50, def.guardCount, "황제국 병사 수는 50이어야 함");
        }

        [Test]
        public void TerritoryDatabase_AllLords_HaveName()
        {
            var db = TerritoryDatabase.Instance;
            var allDefs = db.GetAllDefinitions();

            foreach (var def in allDefs)
            {
                Assert.IsNotNull(def.lord.lordName, $"{def.id} 영주의 이름이 null");
                Assert.IsNotEmpty(def.lord.lordName, $"{def.id} 영주의 이름이 비어있음");
            }
        }

        [Test]
        public void TerritoryDatabase_AllLords_HavePreferredFood()
        {
            var db = TerritoryDatabase.Instance;
            var allDefs = db.GetAllDefinitions();

            foreach (var def in allDefs)
            {
                Assert.IsNotNull(def.lord.preferredFood, $"{def.id} 영주의 선호 음식이 null");
                Assert.IsNotEmpty(def.lord.preferredFood, $"{def.id} 영주의 선호 음식이 비어있음");
            }
        }

        [Test]
        public void TerritoryDatabase_AllLords_HaveValidLoyalty()
        {
            var db = TerritoryDatabase.Instance;
            var allDefs = db.GetAllDefinitions();

            foreach (var def in allDefs)
            {
                Assert.IsTrue(def.lord.loyalty >= 0 && def.lord.loyalty <= 100,
                    $"{def.id} 영주({def.lord.lordName}) 충성심 범위 초과: {def.lord.loyalty}");
            }
        }

        [Test]
        public void TerritoryDatabase_NoDuplicateIDs()
        {
            var db = TerritoryDatabase.Instance;
            var allDefs = new List<TerritoryDefinition>(db.GetAllDefinitions());
            var ids = new HashSet<string>();

            foreach (var def in allDefs)
            {
                string key = def.id.ToString();
                Assert.IsFalse(ids.Contains(key), $"중복 영지 ID 발견: {key}");
                ids.Add(key);
            }
        }

        [Test]
        public void TerritoryDatabase_AllTerritoriesHaveDescription()
        {
            var db = TerritoryDatabase.Instance;
            var allDefs = db.GetAllDefinitions();

            foreach (var def in allDefs)
            {
                Assert.IsNotNull(def.description, $"{def.id} 설명이 null");
                Assert.IsNotEmpty(def.description, $"{def.id} 설명이 비어있음");
            }
        }

        // ===== EmpireAccessRule Tests =====

        [Test]
        public void EmpireAccessRule_Initially_ReturnsFalse()
        {
            // 초기 상태에서는 모든 영지가 Unoccupied이므로 황제국 입장 불가
            bool canAccess = EmpireAccessRule.CanAccessEmpire();
            Assert.IsFalse(canAccess, "초기 상태에서는 황제국 입장이 불가능해야 함");
        }

        [Test]
        public void EmpireAccessRule_InitialProgress_IsZero()
        {
            float progress = EmpireAccessRule.GetProgress();
            Assert.AreEqual(0f, progress, 0.001f, "초기 진행률은 0이어야 함");
        }

        [Test]
        public void EmpireAccessRule_FullyConquered_ReturnsTrue()
        {
            var db = TerritoryDatabase.Instance;

            // 모든 80개 영지 완전 점령 상태로 설정
            foreach (NationType nation in new[] { NationType.East, NationType.West, NationType.South, NationType.North })
            {
                for (int i = 1; i <= 20; i++)
                {
                    var state = db.GetState(nation, i);
                    state.ownership = TerritoryOwnership.PlayerOwned;
                    state.lordDefeated = true;
                }
            }

            bool canAccess = EmpireAccessRule.CanAccessEmpire();
            Assert.IsTrue(canAccess, "모든 영지 완전 점령 시 황제국 입장 가능해야 함");

            float progress = EmpireAccessRule.GetProgress();
            Assert.AreEqual(1f, progress, 0.001f, "완전 점령 시 진행률은 1.0이어야 함");

            int count = EmpireAccessRule.GetConqueredCount();
            Assert.AreEqual(80, count, "완전 점령 시 점령 수는 80이어야 함");
        }

        [Test]
        public void EmpireAccessRule_PartialProgress_ReturnsCorrect()
        {
            var db = TerritoryDatabase.Instance;

            // 초기화: 모든 상태 리셋 (먼저 모든 영지를 Unoccupied로)
            foreach (NationType nation in new[] { NationType.East, NationType.West, NationType.South, NationType.North })
            {
                for (int i = 1; i <= 20; i++)
                {
                    var state = db.GetState(nation, i);
                    state.ownership = TerritoryOwnership.Unoccupied;
                    state.lordDefeated = false;
                    state.lordExecuted = false;
                }
            }

            // 동(East) 20개만 점령
            for (int i = 1; i <= 20; i++)
            {
                var state = db.GetState(NationType.East, i);
                state.ownership = TerritoryOwnership.PlayerOwned;
                state.lordDefeated = true;
            }

            bool canAccess = EmpireAccessRule.CanAccessEmpire();
            Assert.IsFalse(canAccess, "20/80만 점령 시 황제국 입장 불가");

            float progress = EmpireAccessRule.GetProgress();
            Assert.AreEqual(20f / 80f, progress, 0.001f, "진행률은 20/80이어야 함");

            int count = EmpireAccessRule.GetConqueredCount();
            Assert.AreEqual(20, count, "점령 수는 20이어야 함");
        }

        [Test]
        public void EmpireAccessRule_LordAlive_NotCounted()
        {
            var db = TerritoryDatabase.Instance;

            // 모든 영지 PlayerOwned지만 영주가 살아있음
            foreach (NationType nation in new[] { NationType.East, NationType.West, NationType.South, NationType.North })
            {
                for (int i = 1; i <= 20; i++)
                {
                    var state = db.GetState(nation, i);
                    state.ownership = TerritoryOwnership.PlayerOwned;
                    state.lordDefeated = false;
                    state.lordExecuted = false;
                }
            }

            bool canAccess = EmpireAccessRule.CanAccessEmpire();
            Assert.IsFalse(canAccess, "영주가 살아있으면 완전 점령으로 간주하지 않음");

            float progress = EmpireAccessRule.GetProgress();
            Assert.AreEqual(0f, progress, 0.001f, "영주 생존 시 진행률 0");
        }

        [Test]
        public void EmpireAccessRule_LordExecuted_CountsAsConquered()
        {
            var db = TerritoryDatabase.Instance;

            // 동(East) 20개: lordExecuted = true
            foreach (NationType nation in new[] { NationType.East, NationType.West, NationType.South, NationType.North })
            {
                for (int i = 1; i <= 20; i++)
                {
                    var state = db.GetState(nation, i);
                    state.ownership = TerritoryOwnership.Unoccupied;
                    state.lordDefeated = false;
                    state.lordExecuted = false;
                }
            }

            for (int i = 1; i <= 20; i++)
            {
                var state = db.GetState(NationType.East, i);
                state.ownership = TerritoryOwnership.PlayerOwned;
                state.lordExecuted = true;
            }

            float progress = EmpireAccessRule.GetProgress();
            Assert.AreEqual(20f / 80f, progress, 0.001f, "lordExecuted도 점령으로 인정되어야 함");

            int count = EmpireAccessRule.GetConqueredCount();
            Assert.AreEqual(20, count, "lordExecuted도 점령 카운트에 포함");
        }

        [Test]
        public void TerritoryDatabase_GetState_ReturnsStateForAllDefinitions()
        {
            var db = TerritoryDatabase.Instance;
            var allDefs = db.GetAllDefinitions();

            foreach (var def in allDefs)
            {
                var state = db.GetState(def.id);
                Assert.IsNotNull(state, $"{def.id} 상태가 null");
                Assert.AreEqual(def.id.ToString(), state.id.ToString(),
                    $"{def.id} 상태의 ID 불일치");
            }
        }
    }
}