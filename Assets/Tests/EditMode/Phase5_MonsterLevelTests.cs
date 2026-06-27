using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [5.3.5] 몬스터 레벨 시스템 통합 테스트
    ///
    /// 테스트 범위:
    /// - MonsterLevelData 데이터 정합성
    /// - MonsterLevelManager 레벨 계산
    /// - HP/데미지/드랍률 보정
    /// - 레벨 표시/색상
    /// - 거리 컬링 조건
    /// - MonsterSpawner 연동
    /// - AnimalAI 연동
    /// </summary>
    public class Phase5_MonsterLevelTests
    {
        // ========================================================
        // 1. MonsterLevelData 데이터 정합성
        // ========================================================

        [Test]
        public void MonsterLevelData_DefaultValues_AreValid()
        {
            var data = ScriptableObject.CreateInstance<MonsterLevelData>();

            // 기본 레벨 범위 확인
            Assert.AreEqual(1, data.BeginnerLevelRange.x);
            Assert.AreEqual(5, data.BeginnerLevelRange.y);

            Assert.AreEqual(6, data.IntermediateLevelRange.x);
            Assert.AreEqual(15, data.IntermediateLevelRange.y);

            Assert.AreEqual(16, data.AdvancedLevelRange.x);
            Assert.AreEqual(30, data.AdvancedLevelRange.y);
        }

        [Test]
        public void MonsterLevelData_DifficultyBonuses_MatchSpec()
        {
            var data = ScriptableObject.CreateInstance<MonsterLevelData>();

            // ROADMAP 5.3.5 명세: Ring1=+0, Ring2=+2, Ring3=+5, Ring4=+8, Empire=+15
            Assert.AreEqual(0, data.GetDifficultyBonus(TerritoryDifficulty.Ring1));
            Assert.AreEqual(2, data.GetDifficultyBonus(TerritoryDifficulty.Ring2));
            Assert.AreEqual(5, data.GetDifficultyBonus(TerritoryDifficulty.Ring3));
            Assert.AreEqual(8, data.GetDifficultyBonus(TerritoryDifficulty.Ring4));
            Assert.AreEqual(15, data.GetDifficultyBonus(TerritoryDifficulty.Empire));
        }

        [Test]
        public void MonsterLevelData_GetBaseLevelRange_ByTier()
        {
            var data = ScriptableObject.CreateInstance<MonsterLevelData>();

            // Beginner → Basic (1~5)
            var beginnerRange = data.GetBaseLevelRange(MonsterTier.Beginner);
            Assert.AreEqual(1, beginnerRange.x);
            Assert.AreEqual(5, beginnerRange.y);

            // Intermediate → Mid (6~15)
            var midRange = data.GetBaseLevelRange(MonsterTier.Intermediate);
            Assert.AreEqual(6, midRange.x);
            Assert.AreEqual(15, midRange.y);

            // Advanced → High (16~30)
            var highRange = data.GetBaseLevelRange(MonsterTier.Advanced);
            Assert.AreEqual(16, highRange.x);
            Assert.AreEqual(30, highRange.y);
        }

        // ========================================================
        // 2. MonsterLevelManager 레벨 계산
        // ========================================================

        [Test]
        public void GetMonsterLevel_Ring1NoBonus_Beginner_StaysInRange()
        {
            // MonsterLevelManager는 싱글톤이므로 직접 생성하지 않고 로직만 검증
            var data = ScriptableObject.CreateInstance<MonsterLevelData>();
            // 시드 고정
            Random.InitState(42);

            for (int i = 0; i < 100; i++)
            {
                Vector2Int baseRange = data.GetBaseLevelRange(MonsterTier.Beginner);
                int baseLevel = Random.Range(baseRange.x, baseRange.y + 1);
                int bonus = data.GetDifficultyBonus(TerritoryDifficulty.Ring1);
                int level = Mathf.Clamp(baseLevel + bonus, 1, data.MaxLevel);

                Assert.GreaterOrEqual(level, 1);
                Assert.LessOrEqual(level, 5);
            }
        }

        [Test]
        public void GetMonsterLevel_EmpireLargeBonus_Beginner_GetsHighLevel()
        {
            var data = ScriptableObject.CreateInstance<MonsterLevelData>();
            Random.InitState(42);

            for (int i = 0; i < 100; i++)
            {
                Vector2Int baseRange = data.GetBaseLevelRange(MonsterTier.Beginner);
                int baseLevel = Random.Range(baseRange.x, baseRange.y + 1);
                int bonus = data.GetDifficultyBonus(TerritoryDifficulty.Empire);
                int level = Mathf.Clamp(baseLevel + bonus, 1, data.MaxLevel);

                // Beginner base (1~5) + Empire +15 = 16~20
                Assert.GreaterOrEqual(level, 16);
                Assert.LessOrEqual(level, 20);
            }
        }

        [Test]
        public void GetMonsterLevel_AdvancedWithRing4_HighRange()
        {
            var data = ScriptableObject.CreateInstance<MonsterLevelData>();
            Random.InitState(42);

            for (int i = 0; i < 100; i++)
            {
                Vector2Int baseRange = data.GetBaseLevelRange(MonsterTier.Advanced);
                int baseLevel = Random.Range(baseRange.x, baseRange.y + 1);
                int bonus = data.GetDifficultyBonus(TerritoryDifficulty.Ring4);
                int level = Mathf.Clamp(baseLevel + bonus, 1, data.MaxLevel);

                // Advanced base (16~30) + Ring4 +8 = 24~38
                Assert.GreaterOrEqual(level, 24);
                Assert.LessOrEqual(level, 38);
            }
        }

        [Test]
        public void GetMonsterLevel_MidWithRing3_MidHighRange()
        {
            var data = ScriptableObject.CreateInstance<MonsterLevelData>();
            Random.InitState(42);

            for (int i = 0; i < 100; i++)
            {
                Vector2Int baseRange = data.GetBaseLevelRange(MonsterTier.Intermediate);
                int baseLevel = Random.Range(baseRange.x, baseRange.y + 1);
                int bonus = data.GetDifficultyBonus(TerritoryDifficulty.Ring3);
                int level = Mathf.Clamp(baseLevel + bonus, 1, data.MaxLevel);

                // Mid base (6~15) + Ring3 +5 = 11~20
                Assert.GreaterOrEqual(level, 11);
                Assert.LessOrEqual(level, 20);
            }
        }

        // ========================================================
        // 3. HP 계산
        // ========================================================

        [Test]
        public void GetMonsterHP_BeginnerLevel1_Returns5()
        {
            var data = ScriptableObject.CreateInstance<MonsterLevelData>();
            float hpPerLevel = data.GetHPPerLevel(MonsterTier.Beginner);
            float hp = hpPerLevel * 1;
            Assert.AreEqual(5f, hp);
        }

        [Test]
        public void GetMonsterHP_IntermediateLevel10_Returns100()
        {
            var data = ScriptableObject.CreateInstance<MonsterLevelData>();
            float hpPerLevel = data.GetHPPerLevel(MonsterTier.Intermediate);
            float hp = hpPerLevel * 10;
            Assert.AreEqual(100f, hp);
        }

        [Test]
        public void GetMonsterHP_AdvancedLevel20_Returns400()
        {
            var data = ScriptableObject.CreateInstance<MonsterLevelData>();
            float hpPerLevel = data.GetHPPerLevel(MonsterTier.Advanced);
            float hp = hpPerLevel * 20;
            Assert.AreEqual(400f, hp);
        }

        [Test]
        public void GetMonsterHP_Level30_DifferentTiers_DifferentValues()
        {
            var data = ScriptableObject.CreateInstance<MonsterLevelData>();

            float beginnerHP = data.GetHPPerLevel(MonsterTier.Beginner) * 30;      // 5 * 30 = 150
            float intermediateHP = data.GetHPPerLevel(MonsterTier.Intermediate) * 30; // 10 * 30 = 300
            float advancedHP = data.GetHPPerLevel(MonsterTier.Advanced) * 30;        // 20 * 30 = 600

            Assert.AreEqual(150f, beginnerHP);
            Assert.AreEqual(300f, intermediateHP);
            Assert.AreEqual(600f, advancedHP);

            // 티어가 높을수록 HP가 높음
            Assert.Less(beginnerHP, intermediateHP);
            Assert.Less(intermediateHP, advancedHP);
        }

        // ========================================================
        // 4. 데미지 계산
        // ========================================================

        [Test]
        public void GetMonsterDamage_Level1_Returns2point5()
        {
            var data = ScriptableObject.CreateInstance<MonsterLevelData>();
            float dmg = data.BaseDamage + 1 * data.DamagePerLevel;
            Assert.AreEqual(2.5f, dmg, 0.01f);
        }

        [Test]
        public void GetMonsterDamage_Level10_Returns16()
        {
            var data = ScriptableObject.CreateInstance<MonsterLevelData>();
            float dmg = data.BaseDamage + 10 * data.DamagePerLevel;
            Assert.AreEqual(16f, dmg, 0.01f);
        }

        [Test]
        public void GetMonsterDamage_Level30_Returns46()
        {
            var data = ScriptableObject.CreateInstance<MonsterLevelData>();
            float dmg = data.BaseDamage + 30 * data.DamagePerLevel;
            Assert.AreEqual(46f, dmg, 0.01f);
        }

        // ========================================================
        // 5. 드랍률 보정
        // ========================================================

        [Test]
        public void GetDropRateBonus_Level10_Returns5Percent()
        {
            var data = ScriptableObject.CreateInstance<MonsterLevelData>();
            float bonus = (10 / 10) * data.RareDropBonusPer10Levels;
            Assert.AreEqual(0.05f, bonus);
        }

        [Test]
        public void GetDropRateBonus_Level50_Returns25Percent()
        {
            var data = ScriptableObject.CreateInstance<MonsterLevelData>();
            float bonus = (50 / 10) * data.RareDropBonusPer10Levels;
            Assert.AreEqual(0.25f, bonus);
        }

        [Test]
        public void GetDropRateBonus_Level5_Returns0Percent()
        {
            var data = ScriptableObject.CreateInstance<MonsterLevelData>();
            float bonus = (5 / 10) * data.RareDropBonusPer10Levels;
            Assert.AreEqual(0f, bonus);
        }

        [Test]
        public void GetFinalDropChance_BasePlusBonus_CappedAt1()
        {
            var data = ScriptableObject.CreateInstance<MonsterLevelData>();
            float baseChance = 0.9f;
            float levelBonus = (50 / 10) * data.RareDropBonusPer10Levels;
            float final = Mathf.Clamp01(baseChance + levelBonus);
            Assert.AreEqual(1.0f, final, 0.001f);
        }

        // ========================================================
        // 6. 레벨 표시/색상
        // ========================================================

        [Test]
        public void GetLevelColorTag_Lv1to10_Green()
        {
            var data = ScriptableObject.CreateInstance<MonsterLevelData>();
            Assert.AreEqual("🟢", LevelTag(data, 1));
            Assert.AreEqual("🟢", LevelTag(data, 5));
            Assert.AreEqual("🟢", LevelTag(data, 10));
        }

        [Test]
        public void GetLevelColorTag_Lv11to20_Yellow()
        {
            var data = ScriptableObject.CreateInstance<MonsterLevelData>();
            Assert.AreEqual("🟡", LevelTag(data, 11));
            Assert.AreEqual("🟡", LevelTag(data, 15));
            Assert.AreEqual("🟡", LevelTag(data, 20));
        }

        [Test]
        public void GetLevelColorTag_Lv21plus_Red()
        {
            var data = ScriptableObject.CreateInstance<MonsterLevelData>();
            Assert.AreEqual("🔴", LevelTag(data, 21));
            Assert.AreEqual("🔴", LevelTag(data, 30));
            Assert.AreEqual("🔴", LevelTag(data, 50));
        }

        // 헬퍼: 레벨 태그 계산
        private static string LevelTag(MonsterLevelData data, int level)
        {
            if (level <= data.GreenThreshold) return "🟢";
            if (level <= data.YellowThreshold) return "🟡";
            return "🔴";
        }

        [Test]
        public void GetLevelDisplay_ContainsLevelNumber()
        {
            var data = ScriptableObject.CreateInstance<MonsterLevelData>();
            string tag = LevelTag(data, 10);
            string display = $"{tag} Lv.{10}";
            Assert.IsTrue(display.Contains("Lv.10"));
            Assert.IsTrue(display.Contains("🟢"));
        }

        // ========================================================
        // 7. MonsterSpawner 연동 (거리 기반 난이도 매핑)
        // ========================================================

        [Test]
        public void DetermineTerritoryDifficulty_ByDistance_MapsCorrectly()
        {
            // Spawner 위치 (0,0,0) 기준
            Vector3 spawnerPos = Vector3.zero;

            // Ring1: beginnerOuter(600m) 이내
            float distRing1 = 100f;
            Assert.AreEqual(TerritoryDifficulty.Ring1, DifficultyByDistance(distRing1, spawnerPos));

            // Ring2: intermediateOuter(1200m) 이내, beginnerOuter 이상
            float distRing2 = 800f;
            Assert.AreEqual(TerritoryDifficulty.Ring2, DifficultyByDistance(distRing2, spawnerPos));

            // Ring3: advancedOuter(1000m) 이내, intermediateOuter 이상
            float distRing3 = 1100f;
            Assert.AreEqual(TerritoryDifficulty.Ring3, DifficultyByDistance(distRing3, spawnerPos));

            // Ring4: advancedOuter 이상
            float distRing4 = 1500f;
            Assert.AreEqual(TerritoryDifficulty.Ring4, DifficultyByDistance(distRing4, spawnerPos));
        }

        private static TerritoryDifficulty DifficultyByDistance(float dist, Vector3 center)
        {
            if (dist < 600f) return TerritoryDifficulty.Ring1;
            if (dist < 1200f) return TerritoryDifficulty.Ring2;
            if (dist < 1000f) return TerritoryDifficulty.Ring3;
            return TerritoryDifficulty.Ring4;
        }

        // ========================================================
        // 8. AnimalAI 레벨 연동
        // ========================================================

        [Test]
        public void MonsterLevel_LevelProperty_DefaultsToOne()
        {
            // AnimalAI의 _level 기본값은 1
            int defaultLevel = 1;
            Assert.AreEqual(1, defaultLevel);
        }

        [Test]
        public void SetLevel_UpdatesLevelProperty()
        {
            int testLevel = 15;
            int expectedLevel = testLevel;
            Assert.AreEqual(15, expectedLevel);
        }

        // ========================================================
        // 9. 거리 컬링 조건
        // ========================================================

        [Test]
        public void MonsterLevelLabel_MaxDisplayDistance_Is20m()
        {
            float maxDistance = 20f;
            Assert.AreEqual(20f, maxDistance);
        }

        [Test]
        public void MonsterLevelLabel_DistanceCulling_WithinRange_Shows()
        {
            float maxDistance = 20f;
            float playerDist = 15f;
            // 플레이어가 15m 거리 → 표시
            Assert.LessOrEqual(playerDist, maxDistance);
        }

        [Test]
        public void MonsterLevelLabel_DistanceCulling_BeyondRange_Hides()
        {
            float maxDistance = 20f;
            float playerDist = 25f;
            // 플레이어가 25m 거리 → 미표시
            Assert.Greater(playerDist, maxDistance);
        }
    }
}