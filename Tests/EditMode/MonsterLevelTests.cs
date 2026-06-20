using NUnit.Framework;
using ProjectName.Systems;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C9-14 몬스터 레벨 시스템 테스트
    /// </summary>
    public class MonsterLevelTests
    {
        // ===================== 티어별 기본 레벨 =====================

        [Test]
        public void GetBaseLevelRange_Beginner_Returns1to5()
        {
            var range = MonsterLevelSystem.GetBaseLevelRange(MonsterTier.Beginner);
            Assert.AreEqual(1, range.x);
            Assert.AreEqual(5, range.y);
        }

        [Test]
        public void GetBaseLevelRange_Intermediate_Returns6to15()
        {
            var range = MonsterLevelSystem.GetBaseLevelRange(MonsterTier.Intermediate);
            Assert.AreEqual(6, range.x);
            Assert.AreEqual(15, range.y);
        }

        [Test]
        public void GetBaseLevelRange_Advanced_Returns16to30()
        {
            var range = MonsterLevelSystem.GetBaseLevelRange(MonsterTier.Advanced);
            Assert.AreEqual(16, range.x);
            Assert.AreEqual(30, range.y);
        }

        // ===================== 난이도 보정 =====================

        [Test]
        public void GetDifficultyBonus_Ring1_Zero()
        {
            Assert.AreEqual(0, MonsterLevelSystem.GetDifficultyBonus(TerritoryDifficulty.Ring1));
        }

        [Test]
        public void GetDifficultyBonus_Ring3_Plus5()
        {
            Assert.AreEqual(5, MonsterLevelSystem.GetDifficultyBonus(TerritoryDifficulty.Ring3));
        }

        [Test]
        public void GetDifficultyBonus_Empire_Plus15()
        {
            Assert.AreEqual(15, MonsterLevelSystem.GetDifficultyBonus(TerritoryDifficulty.Empire));
        }

        // ===================== 레벨 생성 =====================

        [Test]
        public void GenerateMonsterLevel_WithinRange()
        {
            for (int i = 0; i < 50; i++)
            {
                int level = MonsterLevelSystem.GenerateMonsterLevel(MonsterTier.Beginner, TerritoryDifficulty.Ring1);
                Assert.GreaterOrEqual(level, 1);
                Assert.LessOrEqual(level, 5);
            }
        }

        [Test]
        public void GenerateMonsterLevel_WithBonus_Higher()
        {
            for (int i = 0; i < 50; i++)
            {
                int withBonus = MonsterLevelSystem.GenerateMonsterLevel(MonsterTier.Beginner, TerritoryDifficulty.Empire);
                Assert.GreaterOrEqual(withBonus, 16, "초반 몬스터도 황제국에서는 최소 Lv.16");
            }
        }

        // ===================== 스탯 계산 =====================

        [Test]
        public void CalculateHP_Beginner_Level5_Returns25()
        {
            Assert.AreEqual(25f, MonsterLevelSystem.CalculateHP(MonsterTier.Beginner, 5));
        }

        [Test]
        public void CalculateHP_Intermediate_Level10_Returns100()
        {
            Assert.AreEqual(100f, MonsterLevelSystem.CalculateHP(MonsterTier.Intermediate, 10));
        }

        [Test]
        public void CalculateHP_Advanced_Level20_Returns400()
        {
            Assert.AreEqual(400f, MonsterLevelSystem.CalculateHP(MonsterTier.Advanced, 20));
        }

        [Test]
        public void CalculateDamage_Level1_Returns2point5()
        {
            Assert.AreEqual(2.5f, MonsterLevelSystem.CalculateDamage(1), 0.01f);
        }

        [Test]
        public void CalculateDamage_Level10_Returns16()
        {
            Assert.AreEqual(16f, MonsterLevelSystem.CalculateDamage(10), 0.01f);
        }

        [Test]
        public void CalculateXP_Level1_Returns7()
        {
            Assert.AreEqual(7f, MonsterLevelSystem.CalculateXP(1));
        }

        [Test]
        public void CalculateXP_Level10_Returns25()
        {
            Assert.AreEqual(25f, MonsterLevelSystem.CalculateXP(10));
        }

        // ===================== 레벨 표시 =====================

        [Test]
        public void GetLevelColorTag_LowLevel_Green()
        {
            Assert.AreEqual("🟢", MonsterLevelSystem.GetLevelColorTag(1));
            Assert.AreEqual("🟢", MonsterLevelSystem.GetLevelColorTag(10));
        }

        [Test]
        public void GetLevelColorTag_HighLevel_Red()
        {
            Assert.AreEqual("🔴", MonsterLevelSystem.GetLevelColorTag(30));
            Assert.AreEqual("🔴", MonsterLevelSystem.GetLevelColorTag(50));
        }

        [Test]
        public void GetLevelDisplay_Format()
        {
            string display = MonsterLevelSystem.GetLevelDisplay(10);
            Assert.IsTrue(display.Contains("Lv.10"), "레벨 표시에 Lv.10 포함");
        }

        // ===================== 드랍률 보정 =====================

        [Test]
        public void GetRareDropBonus_Level10_Returns5Percent()
        {
            Assert.AreEqual(0.05f, MonsterLevelSystem.GetRareDropBonus(10));
        }

        [Test]
        public void GetRareDropBonus_Level50_Returns25Percent()
        {
            Assert.AreEqual(0.25f, MonsterLevelSystem.GetRareDropBonus(50));
        }

        [Test]
        public void GetFinalDropChance_BasePlusBonus()
        {
            float result = MonsterLevelSystem.GetFinalDropChance(0.1f, 20);
            Assert.AreEqual(0.2f, result, 0.001f, "기본 10% + 20레벨 보정 10% = 20%");
        }

        [Test]
        public void GetFinalDropChance_CapsAt100()
        {
            float result = MonsterLevelSystem.GetFinalDropChance(0.9f, 50);
            Assert.AreEqual(1.0f, result, 0.001f, "100%를 초과하지 않음");
        }

        // ===================== 티어 추정 =====================

        [Test]
        public void EstimateTierByName_Rabbit_Beginner()
        {
            Assert.AreEqual(MonsterTier.Beginner, MonsterLevelSystem.EstimateTierByName("토끼"));
        }

        [Test]
        public void EstimateTierByName_Wolf_Intermediate()
        {
            Assert.AreEqual(MonsterTier.Intermediate, MonsterLevelSystem.EstimateTierByName("늑대"));
        }

        [Test]
        public void EstimateTierByName_Manticore_Advanced()
        {
            Assert.AreEqual(MonsterTier.Advanced, MonsterLevelSystem.EstimateTierByName("만티코어"));
        }

        [Test]
        public void EstimateTierByName_Unknown_Beginner()
        {
            Assert.AreEqual(MonsterTier.Beginner, MonsterLevelSystem.EstimateTierByName("알 수 없는 몬스터"));
        }
    }
}