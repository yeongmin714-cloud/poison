using NUnit.Framework;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// LevelGroupManager 및 LevelGroup 데이터 구조에 대한 EditMode 테스트.
    /// C9-31: 아바타 레벨 그룹 데이터 — 5단계 레벨 범위 정의 + Placeholder 시각 구분자.
    /// </summary>
    public class LevelGroupTests
    {
        [SetUp]
        public void Setup()
        {
            // Ensure manager is initialized before each test
            LevelGroupManager.Initialize();
        }

        // ===== 5개 레벨 범위가 올바른 그룹을 반환 =====

        [Test]
        public void Level1_Returns_Novice()
        {
            LevelGroup group = LevelGroupManager.GetGroup(1);
            Assert.AreEqual(LevelGroupId.Novice, group.groupId);
        }

        [Test]
        public void Level5_Returns_Novice()
        {
            LevelGroup group = LevelGroupManager.GetGroup(5);
            Assert.AreEqual(LevelGroupId.Novice, group.groupId);
        }

        [Test]
        public void Level11_Returns_Adept()
        {
            LevelGroup group = LevelGroupManager.GetGroup(11);
            Assert.AreEqual(LevelGroupId.Adept, group.groupId);
        }

        [Test]
        public void Level15_Returns_Adept()
        {
            LevelGroup group = LevelGroupManager.GetGroup(15);
            Assert.AreEqual(LevelGroupId.Adept, group.groupId);
        }

        [Test]
        public void Level21_Returns_Veteran()
        {
            LevelGroup group = LevelGroupManager.GetGroup(21);
            Assert.AreEqual(LevelGroupId.Veteran, group.groupId);
        }

        [Test]
        public void Level25_Returns_Veteran()
        {
            LevelGroup group = LevelGroupManager.GetGroup(25);
            Assert.AreEqual(LevelGroupId.Veteran, group.groupId);
        }

        [Test]
        public void Level31_Returns_Elite()
        {
            LevelGroup group = LevelGroupManager.GetGroup(31);
            Assert.AreEqual(LevelGroupId.Elite, group.groupId);
        }

        [Test]
        public void Level35_Returns_Elite()
        {
            LevelGroup group = LevelGroupManager.GetGroup(35);
            Assert.AreEqual(LevelGroupId.Elite, group.groupId);
        }

        [Test]
        public void Level41_Returns_Legendary()
        {
            LevelGroup group = LevelGroupManager.GetGroup(41);
            Assert.AreEqual(LevelGroupId.Legendary, group.groupId);
        }

        [Test]
        public void Level45_Returns_Legendary()
        {
            LevelGroup group = LevelGroupManager.GetGroup(45);
            Assert.AreEqual(LevelGroupId.Legendary, group.groupId);
        }

        // ===== 경계값 테스트 =====

        [Test]
        public void Boundary_Level1_MinNovice()
        {
            LevelGroup group = LevelGroupManager.GetGroup(1);
            Assert.AreEqual(LevelGroupId.Novice, group.groupId);
        }

        [Test]
        public void Boundary_Level10_MaxNovice()
        {
            LevelGroup group = LevelGroupManager.GetGroup(10);
            Assert.AreEqual(LevelGroupId.Novice, group.groupId);
        }

        [Test]
        public void Boundary_Level11_MinAdept()
        {
            LevelGroup group = LevelGroupManager.GetGroup(11);
            Assert.AreEqual(LevelGroupId.Adept, group.groupId);
        }

        [Test]
        public void Boundary_Level20_MaxAdept()
        {
            LevelGroup group = LevelGroupManager.GetGroup(20);
            Assert.AreEqual(LevelGroupId.Adept, group.groupId);
        }

        [Test]
        public void Boundary_Level21_MinVeteran()
        {
            LevelGroup group = LevelGroupManager.GetGroup(21);
            Assert.AreEqual(LevelGroupId.Veteran, group.groupId);
        }

        [Test]
        public void Boundary_Level30_MaxVeteran()
        {
            LevelGroup group = LevelGroupManager.GetGroup(30);
            Assert.AreEqual(LevelGroupId.Veteran, group.groupId);
        }

        [Test]
        public void Boundary_Level31_MinElite()
        {
            LevelGroup group = LevelGroupManager.GetGroup(31);
            Assert.AreEqual(LevelGroupId.Elite, group.groupId);
        }

        [Test]
        public void Boundary_Level40_MaxElite()
        {
            LevelGroup group = LevelGroupManager.GetGroup(40);
            Assert.AreEqual(LevelGroupId.Elite, group.groupId);
        }

        [Test]
        public void Boundary_Level41_MinLegendary()
        {
            LevelGroup group = LevelGroupManager.GetGroup(41);
            Assert.AreEqual(LevelGroupId.Legendary, group.groupId);
        }

        [Test]
        public void Boundary_Level50_MaxLegendary()
        {
            LevelGroup group = LevelGroupManager.GetGroup(50);
            Assert.AreEqual(LevelGroupId.Legendary, group.groupId);
        }

        // ===== 범위 외 레벨 =====

        [Test]
        public void Level0_Returns_Novice()
        {
            LevelGroup group = LevelGroupManager.GetGroup(0);
            Assert.AreEqual(LevelGroupId.Novice, group.groupId);
        }

        [Test]
        public void LevelNegative_Returns_Novice()
        {
            LevelGroup group = LevelGroupManager.GetGroup(-5);
            Assert.AreEqual(LevelGroupId.Novice, group.groupId);
        }

        [Test]
        public void Level50_Returns_Legendary()
        {
            LevelGroup group = LevelGroupManager.GetGroup(50);
            Assert.AreEqual(LevelGroupId.Legendary, group.groupId);
        }

        [Test]
        public void LevelAbove50_Returns_Legendary()
        {
            LevelGroup group = LevelGroupManager.GetGroup(99);
            Assert.AreEqual(LevelGroupId.Legendary, group.groupId);
        }

        // ===== GetVisualVariantName 테스트 =====

        [Test]
        public void GetVisualVariantName_Level5_ReturnsSoldier_Tier1()
        {
            string name = LevelGroupManager.GetVisualVariantName("soldier", 5);
            Assert.AreEqual("soldier_tier1", name);
        }

        [Test]
        public void GetVisualVariantName_Level15_ReturnsSoldier_Tier2()
        {
            string name = LevelGroupManager.GetVisualVariantName("soldier", 15);
            Assert.AreEqual("soldier_tier2", name);
        }

        [Test]
        public void GetVisualVariantName_Level25_ReturnsSoldier_Tier3()
        {
            string name = LevelGroupManager.GetVisualVariantName("soldier", 25);
            Assert.AreEqual("soldier_tier3", name);
        }

        [Test]
        public void GetVisualVariantName_Level35_ReturnsSoldier_Tier4()
        {
            string name = LevelGroupManager.GetVisualVariantName("soldier", 35);
            Assert.AreEqual("soldier_tier4", name);
        }

        [Test]
        public void GetVisualVariantName_Level45_ReturnsSoldier_Tier5()
        {
            string name = LevelGroupManager.GetVisualVariantName("soldier", 45);
            Assert.AreEqual("soldier_tier5", name);
        }

        // ===== GetPlaceholderColor 테스트 =====

        [Test]
        public void GetPlaceholderColor_Novice_NotDefault()
        {
            Color color = LevelGroupManager.GetPlaceholderColor(5);
            Assert.AreNotEqual(default(Color), color);
            Assert.AreNotEqual(Color.clear, color);
        }

        [Test]
        public void GetPlaceholderColor_Adept_NotDefault()
        {
            Color color = LevelGroupManager.GetPlaceholderColor(15);
            Assert.AreNotEqual(default(Color), color);
            Assert.AreNotEqual(Color.clear, color);
        }

        [Test]
        public void GetPlaceholderColor_Veteran_NotDefault()
        {
            Color color = LevelGroupManager.GetPlaceholderColor(25);
            Assert.AreNotEqual(default(Color), color);
            Assert.AreNotEqual(Color.clear, color);
        }

        [Test]
        public void GetPlaceholderColor_Elite_NotDefault()
        {
            Color color = LevelGroupManager.GetPlaceholderColor(35);
            Assert.AreNotEqual(default(Color), color);
            Assert.AreNotEqual(Color.clear, color);
        }

        [Test]
        public void GetPlaceholderColor_Legendary_NotDefault()
        {
            Color color = LevelGroupManager.GetPlaceholderColor(45);
            Assert.AreNotEqual(default(Color), color);
            Assert.AreNotEqual(Color.clear, color);
        }

        // ===== 모든 그룹 정의 검증 =====

        [Test]
        public void AllGroups_Exist_And_Have_Unique_Ranges()
        {
            LevelGroup[] groups = LevelGroupManager.GetLevelGroups();
            Assert.AreEqual(5, groups.Length, "There should be exactly 5 level groups");

            // 각 그룹이 고유한 ID를 가지고 있는지 확인
            bool[] idSeen = new bool[5];
            foreach (LevelGroup g in groups)
            {
                int idx = (int)g.groupId;
                Assert.IsFalse(idSeen[idx], $"Duplicate group ID: {g.groupId}");
                idSeen[idx] = true;
            }

            // 레벨 범위가 겹치지 않는지 확인
            for (int i = 0; i < groups.Length; i++)
            {
                for (int j = i + 1; j < groups.Length; j++)
                {
                    bool overlap = groups[i].minLevel <= groups[j].maxLevel
                                   && groups[j].minLevel <= groups[i].maxLevel;
                    Assert.IsFalse(overlap, $"Level ranges overlap between {groups[i].groupName} and {groups[j].groupName}");
                }
            }
        }

        [Test]
        public void GetLevelGroups_ReturnsAll5()
        {
            LevelGroup[] groups = LevelGroupManager.GetLevelGroups();
            Assert.AreEqual(5, groups.Length);
        }

        [Test]
        public void GetGroupById_ReturnsCorrectGroup()
        {
            LevelGroup novice = LevelGroupManager.GetGroup(LevelGroupId.Novice);
            Assert.AreEqual(LevelGroupId.Novice, novice.groupId);
            Assert.AreEqual(1, novice.minLevel);
            Assert.AreEqual(10, novice.maxLevel);

            LevelGroup legendary = LevelGroupManager.GetGroup(LevelGroupId.Legendary);
            Assert.AreEqual(LevelGroupId.Legendary, legendary.groupId);
            Assert.AreEqual(41, legendary.minLevel);
            Assert.AreEqual(50, legendary.maxLevel);
        }

        [Test]
        public void AllGroups_Have_NonEmpty_Suffix()
        {
            LevelGroup[] groups = LevelGroupManager.GetLevelGroups();
            foreach (LevelGroup g in groups)
            {
                Assert.IsFalse(string.IsNullOrEmpty(g.visualSuffix),
                    $"{g.groupName} should have a non-empty visualSuffix");
            }
        }

        [Test]
        public void AllGroups_Have_Proper_Ranges()
        {
            LevelGroup[] groups = LevelGroupManager.GetLevelGroups();
            foreach (LevelGroup g in groups)
            {
                Assert.GreaterOrEqual(g.maxLevel, g.minLevel,
                    $"{g.groupName} maxLevel should be >= minLevel");
                Assert.Greater(g.minLevel, 0,
                    $"{g.groupName} minLevel should be > 0");
            }
        }
    }
}