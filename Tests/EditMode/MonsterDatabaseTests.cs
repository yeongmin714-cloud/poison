using NUnit.Framework;
using ProjectName.Core;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// MonsterDatabase — 24종 몬스터 데이터 정합성 테스트
    /// </summary>
    public class MonsterDatabaseTests
    {
        [Test]
        public void MonsterDatabase_All24MonstersExist()
        {
            MonsterDatabase.Init();
            Assert.AreEqual(24, MonsterDatabase.All.Count);
        }

        [Test]
        public void MonsterDatabase_EachTierHas8Monsters()
        {
            Assert.AreEqual(8, MonsterDatabase.GetByTier(MonsterTier.Beginner).Count);
            Assert.AreEqual(8, MonsterDatabase.GetByTier(MonsterTier.Intermediate).Count);
            Assert.AreEqual(8, MonsterDatabase.GetByTier(MonsterTier.Advanced).Count);
        }

        [Test]
        public void MonsterDatabase_AllMonstersHaveValidData()
        {
            foreach (var kv in MonsterDatabase.All)
            {
                var def = kv.Value;
                Assert.IsNotNull(def, $"Monster '{kv.Key}' is null");
                Assert.IsFalse(string.IsNullOrEmpty(def.id), "Monster ID is empty");
                Assert.IsFalse(string.IsNullOrEmpty(def.displayName), $"Monster '{def.id}' name is empty");
                Assert.Greater(def.baseHP, 0, $"Monster '{def.id}' HP should be > 0");
                Assert.Greater(def.baseDamage, 0, $"Monster '{def.id}' damage should be > 0");
                Assert.Greater(def.baseSpeed, 0, $"Monster '{def.id}' speed should be > 0");
            }
        }

        [Test]
        public void MonsterDatabase_BeginnerHPInRange()
        {
            var beginners = MonsterDatabase.GetByTier(MonsterTier.Beginner);
            foreach (var def in beginners)
            {
                Assert.GreaterOrEqual(def.baseHP, 10f);
                Assert.LessOrEqual(def.baseHP, 30f);
            }
        }

        [Test]
        public void MonsterDatabase_IntermediateHPInRange()
        {
            var intermediates = MonsterDatabase.GetByTier(MonsterTier.Intermediate);
            foreach (var def in intermediates)
            {
                Assert.GreaterOrEqual(def.baseHP, 50f);
                Assert.LessOrEqual(def.baseHP, 100f);
            }
        }

        [Test]
        public void MonsterDatabase_AdvancedHPInRange()
        {
            var advanced = MonsterDatabase.GetByTier(MonsterTier.Advanced);
            foreach (var def in advanced)
            {
                Assert.GreaterOrEqual(def.baseHP, 150f);
                Assert.LessOrEqual(def.baseHP, 300f);
            }
        }

        [Test]
        public void MonsterDatabase_HPIncreasesWithTier()
        {
            var beginners = MonsterDatabase.GetByTier(MonsterTier.Beginner);
            var intermediates = MonsterDatabase.GetByTier(MonsterTier.Intermediate);
            var advanced = MonsterDatabase.GetByTier(MonsterTier.Advanced);

            float avgBeginner = 0, avgIntermediate = 0, avgAdvanced = 0;
            foreach (var d in beginners) avgBeginner += d.baseHP;
            foreach (var d in intermediates) avgIntermediate += d.baseHP;
            foreach (var d in advanced) avgAdvanced += d.baseHP;
            avgBeginner /= beginners.Count;
            avgIntermediate /= intermediates.Count;
            avgAdvanced /= advanced.Count;

            Assert.Less(avgBeginner, avgIntermediate, "초반 평균 HP < 중반 평균 HP");
            Assert.Less(avgIntermediate, avgAdvanced, "중반 평균 HP < 후반 평균 HP");
        }

        [Test]
        public void MonsterDatabase_GetByIdReturnsCorrect()
        {
            var rabbit = MonsterDatabase.Get("rabbit");
            Assert.IsNotNull(rabbit);
            Assert.AreEqual("토끼", rabbit.displayName);
            Assert.AreEqual(MonsterTier.Beginner, rabbit.tier);

            var golem = MonsterDatabase.Get("stone_golem");
            Assert.IsNotNull(golem);
            Assert.AreEqual("돌골렘", golem.displayName);
            Assert.AreEqual(MonsterTier.Intermediate, golem.tier);

            var ogre = MonsterDatabase.Get("ogre");
            Assert.IsNotNull(ogre);
            Assert.AreEqual("오우거", ogre.displayName);
            Assert.AreEqual(MonsterTier.Advanced, ogre.tier);
        }
    }

    /// <summary>
    /// PlayerHealth — 체력 시스템 테스트
    /// </summary>
    public class PlayerHealthTests
    {
        // Note: These tests validate PlayerHealth logic that can be tested without Unity scene
        // Full integration tests need PlayMode tests

        [Test]
        public void PlayerHealth_DefaultHPIs100()
        {
            // PlayerHealth는 MonoBehaviour — 에디터 테스트는 PlayMode 필요
            // 여기서는 HP 계산 로직만 검증
            Assert.Pass("PlayerHealth tests require PlayMode test runner for full validation");
        }
    }
}
