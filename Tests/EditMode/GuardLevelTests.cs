using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;
using ProjectName.Core.Data;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C9-13 병사 레벨 시스템 테스트
    /// </summary>
    public class GuardLevelTests
    {
        // ===================== 레벨 범위 테스트 =====================

        [Test]
        public void GetLevelRange_Ring1_Returns1to10()
        {
            var range = GuardLevelSystem.GetLevelRange(TerritoryDifficulty.Ring1);
            Assert.AreEqual(1, range.x, "Ring1 최소 레벨 = 1");
            Assert.AreEqual(10, range.y, "Ring1 최대 레벨 = 10");
        }

        [Test]
        public void GetLevelRange_Ring2_Returns11to25()
        {
            var range = GuardLevelSystem.GetLevelRange(TerritoryDifficulty.Ring2);
            Assert.AreEqual(11, range.x, "Ring2 최소 레벨 = 11");
            Assert.AreEqual(25, range.y, "Ring2 최대 레벨 = 25");
        }

        [Test]
        public void GetLevelRange_Ring4_Returns26to40()
        {
            var range = GuardLevelSystem.GetLevelRange(TerritoryDifficulty.Ring4);
            Assert.AreEqual(26, range.x, "Ring4 최소 레벨 = 26");
            Assert.AreEqual(40, range.y, "Ring4 최대 레벨 = 40");
        }

        [Test]
        public void GetLevelRange_Empire_Returns41to50()
        {
            var range = GuardLevelSystem.GetLevelRange(TerritoryDifficulty.Empire);
            Assert.AreEqual(41, range.x, "Empire 최소 레벨 = 41");
            Assert.AreEqual(50, range.y, "Empire 최대 레벨 = 50");
        }

        // ===================== 초기 레벨 생성 테스트 =====================

        [Test]
        public void GenerateInitialLevel_WithinRange()
        {
            for (int i = 0; i < 100; i++)
            {
                int level = GuardLevelSystem.GenerateInitialLevel(TerritoryDifficulty.Ring1);
                Assert.GreaterOrEqual(level, 1, "Ring1 레벨 >= 1");
                Assert.LessOrEqual(level, 10, "Ring1 레벨 <= 10");
            }
        }

        // ===================== 스탯 계산 테스트 =====================

        [Test]
        public void CalculateMaxHP_Level1_Returns10()
        {
            Assert.AreEqual(10f, GuardLevelSystem.CalculateMaxHP(1), "Lv.1 HP = 10");
        }

        [Test]
        public void CalculateMaxHP_Level10_Returns100()
        {
            Assert.AreEqual(100f, GuardLevelSystem.CalculateMaxHP(10), "Lv.10 HP = 10 + 9*10 = 100");
        }

        [Test]
        public void CalculateMaxHP_Level50_Returns500()
        {
            Assert.AreEqual(500f, GuardLevelSystem.CalculateMaxHP(50), "Lv.50 HP = 10 + 49*10 = 500");
        }

        [Test]
        public void CalculateDamage_Level1_Returns2()
        {
            Assert.AreEqual(2f, GuardLevelSystem.CalculateDamage(1), "Lv.1 데미지 = 2");
        }

        [Test]
        public void CalculateDamage_Level10_Returns11()
        {
            Assert.AreEqual(11f, GuardLevelSystem.CalculateDamage(10), "Lv.10 데미지 = 2 + 9 = 11");
        }

        [Test]
        public void CalculateDefense_Level1_Returns0()
        {
            Assert.AreEqual(0f, GuardLevelSystem.CalculateDefense(1), "Lv.1 방어력 = 0");
        }

        [Test]
        public void CalculateDefense_Level10_Returns4point5()
        {
            Assert.AreEqual(4.5f, GuardLevelSystem.CalculateDefense(10), "Lv.10 방어력 = 9*0.5 = 4.5");
        }

        // ===================== 경험치 테스트 =====================

        [Test]
        public void GetXPRequiredForLevel_Level1_IsZero()
        {
            Assert.AreEqual(0f, GuardLevelSystem.GetXPRequiredForLevel(1));
        }

        [Test]
        public void GetXPRequiredForLevel_Level2_IsBase()
        {
            Assert.AreEqual(100f, GuardLevelSystem.GetXPRequiredForLevel(2), "Lv.2 필요 XP = 100");
        }

        [Test]
        public void GetXPRequiredForLevel_Level3_IsScaled()
        {
            Assert.AreEqual(100f + 120f, GuardLevelSystem.GetXPRequiredForLevel(3), 0.01f, "Lv.3 필요 XP = 100 + 120");
        }

        [Test]
        public void GetXPToNextLevel_Level1_ReturnsBase()
        {
            Assert.AreEqual(100f, GuardLevelSystem.GetXPToNextLevel(1));
        }

        [Test]
        public void GetXPToNextLevel_LevelMax_ReturnsZero()
        {
            Assert.AreEqual(0f, GuardLevelSystem.GetXPToNextLevel(50));
        }

        // ===================== 레벨 계산 테스트 =====================

        [Test]
        public void CalculateLevelFromXP_Zero_Returns1()
        {
            Assert.AreEqual(1, GuardLevelSystem.CalculateLevelFromXP(0));
        }

        [Test]
        public void CalculateLevelFromXP_100_Returns2()
        {
            Assert.AreEqual(2, GuardLevelSystem.CalculateLevelFromXP(100));
        }

        [Test]
        public void CalculateLevelFromXP_HighXP_Returns50()
        {
            // 매우 높은 XP는 최대 레벨 반환
            int level = GuardLevelSystem.CalculateLevelFromXP(999999f);
            Assert.AreEqual(50, level, "매우 높은 XP는 Lv.50");
        }

        // ===================== 영지 기반 적용 테스트 =====================

        [Test]
        public void ApplyLevelFromTerritory_SetsLevel()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();

            GuardLevelSystem.ApplyLevelFromTerritory(guard, TerritoryDifficulty.Ring1);

            Assert.GreaterOrEqual(guard.Level, 1, "Ring1에 배치된 병사 레벨 >= 1");
            Assert.LessOrEqual(guard.Level, 10, "Ring1에 배치된 병사 레벨 <= 10");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ApplyLevelFromTerritory_Empire_HighLevel()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();

            GuardLevelSystem.ApplyLevelFromTerritory(guard, TerritoryDifficulty.Empire);

            Assert.GreaterOrEqual(guard.Level, 41, "황제국 병사 레벨 >= 41");

            Object.DestroyImmediate(go);
        }

        // ===================== 스탯 적용 테스트 =====================

        [Test]
        public void ApplyStatsFromLevel_DoesNotThrow()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();

            GuardLevelSystem.ApplyStatsFromLevel(guard);
            // 예외 없이 실행되어야 함

            Object.DestroyImmediate(go);
        }

        // ===================== XP 상수 =====================

        [Test]
        public void Constants_AreDefined()
        {
            Assert.AreEqual(10f, GuardLevelSystem.HP_PER_LEVEL);
            Assert.AreEqual(1f, GuardLevelSystem.DAMAGE_PER_LEVEL);
            Assert.AreEqual(0.5f, GuardLevelSystem.DEFENSE_PER_LEVEL);
            Assert.AreEqual(100f, GuardLevelSystem.BASE_XP_REQUIRED);
            Assert.AreEqual(10f, GuardLevelSystem.XP_PER_COMBAT);
        }
    }
}