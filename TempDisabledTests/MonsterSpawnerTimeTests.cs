using System.Collections.Generic;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C18-01 ~ C18-04: 주야별 몬스터 출현 EditMode 테스트
    ///
    /// 테스트 대상:
    /// - ActiveTime / GetByActiveTime 동작 (C18-01)
    /// - MonsterSpawner TimePeriod 계산 (C18-02)
    /// - 스폰 확률표 기본값 (C18-02)
    /// - 밤 리스폰 속도 배율 (C18-03)
    /// </summary>
    public class MonsterSpawnerTimeTests
    {
        private GameObject _spawnerGo;
        private GameObject _timeManagerGo;
        private MonsterSpawner _spawner;
        private TimeManager _timeManager;

        // ================================================================
        // Helper: reflection-based singleton setup
        // ================================================================

        private void SetManagerInstance(TimeManager instance)
        {
            var field = typeof(TimeManager).GetField("Instance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, instance);
        }

        private void ClearManagerInstance()
        {
            var field = typeof(TimeManager).GetField("Instance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, null);
        }

        // ================================================================
        // Setup / Teardown
        // ================================================================

        [SetUp]
        public void Setup()
        {
            _timeManagerGo = new GameObject("TestTimeManager");
            _timeManager = _timeManagerGo.AddComponent<TimeManager>();
            SetManagerInstance(_timeManager);

            _spawnerGo = new GameObject("TestMonsterSpawner");
            _spawner = _spawnerGo.AddComponent<MonsterSpawner>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_spawnerGo != null)
                UnityEngine.Object.DestroyImmediate(_spawnerGo);
            if (_timeManagerGo != null)
                UnityEngine.Object.DestroyImmediate(_timeManagerGo);
            ClearManagerInstance();
        }

        // ================================================================
        // C18-01: GetByActiveTime
        // ================================================================

        [Test]
        public void GetByActiveTime_ReturnsAllMonstersForBoth()
        {
            var bothMonsters = MonsterDatabase.GetByActiveTime(ActiveTime.Both);
            Assert.IsNotNull(bothMonsters, "Both 결과는 null이 아니어야 함");
            Assert.Greater(bothMonsters.Count, 0, "Both 몬스터가 최소 1개 이상 있어야 함");
        }

        [Test]
        public void GetByActiveTime_Day_ReturnsDayAndBothMonsters()
        {
            var dayMonsters = MonsterDatabase.GetByActiveTime(ActiveTime.Day);
            Assert.IsNotNull(dayMonsters, "Day 결과는 null이 아니어야 함");
            Assert.Greater(dayMonsters.Count, 0, "Day 몬스터가 최소 1개 이상 있어야 함");

            // Day 몬스터는 Day 또는 Both만 포함해야 함
            foreach (var def in dayMonsters)
            {
                Assert.IsTrue(def.activeTime == ActiveTime.Day || def.activeTime == ActiveTime.Both,
                    $"{def.id}는 Day 또는 Both여야 함 (actual: {def.activeTime})");
            }
        }

        [Test]
        public void GetByActiveTime_Night_ReturnsNightAndBothMonsters()
        {
            var nightMonsters = MonsterDatabase.GetByActiveTime(ActiveTime.Night);
            Assert.IsNotNull(nightMonsters, "Night 결과는 null이 아니어야 함");
            Assert.Greater(nightMonsters.Count, 0, "Night 몬스터가 최소 1개 이상 있어야 함");

            foreach (var def in nightMonsters)
            {
                Assert.IsTrue(def.activeTime == ActiveTime.Night || def.activeTime == ActiveTime.Both,
                    $"{def.id}는 Night 또는 Both여야 함 (actual: {def.activeTime})");
            }
        }

        [Test]
        public void GetByActiveTime_Counts_AreReasonable()
        {
            var all = MonsterDatabase.GetByActiveTime(ActiveTime.Both);
            var day = MonsterDatabase.GetByActiveTime(ActiveTime.Day);
            var night = MonsterDatabase.GetByActiveTime(ActiveTime.Night);

            // Day에만 있는 몬스터와 Night에만 있는 몬스터가 모두 존재해야 함
            Assert.Less(day.Count, all.Count + 1, "Day 목록은 전체보다 클 수 없음");
            Assert.Less(night.Count, all.Count + 1, "Night 목록은 전체보다 클 수 없음");
            Assert.Greater(day.Count, 0);
            Assert.Greater(night.Count, 0);
        }

        [Test]
        public void Rabbit_IsDayMonster()
        {
            var rabbit = MonsterDatabase.Get("rabbit");
            Assert.IsNotNull(rabbit);
            Assert.AreEqual(ActiveTime.Day, rabbit.activeTime, "토끼는 Day 활성");
        }

        [Test]
        public void Bat_IsNightMonster()
        {
            var bat = MonsterDatabase.Get("bat");
            Assert.IsNotNull(bat);
            Assert.AreEqual(ActiveTime.Night, bat.activeTime, "박쥐는 Night 활성");
        }

        [Test]
        public void Slime_IsNightMonster()
        {
            var slime = MonsterDatabase.Get("slime");
            Assert.IsNotNull(slime);
            Assert.AreEqual(ActiveTime.Night, slime.activeTime, "슬라임은 Night 활성");
        }

        [Test]
        public void Ogre_IsBothMonster()
        {
            var ogre = MonsterDatabase.Get("ogre");
            Assert.IsNotNull(ogre);
            Assert.AreEqual(ActiveTime.Both, ogre.activeTime, "오우거는 Both 활성");
        }

        // ================================================================
        // C18-02: TimePeriod 계산
        // ================================================================

        [Test]
        public void GetTimePeriod_Day_ReturnsDay()
        {
            // 6~18시 = Day
            Assert.AreEqual(MonsterSpawner.TimePeriod.Day, _spawner.GetTimePeriod(6), "6시 = Day");
            Assert.AreEqual(MonsterSpawner.TimePeriod.Day, _spawner.GetTimePeriod(12), "12시 = Day");
            Assert.AreEqual(MonsterSpawner.TimePeriod.Day, _spawner.GetTimePeriod(17), "17시 = Day");
        }

        [Test]
        public void GetTimePeriod_Evening_ReturnsEvening()
        {
            // 18~20 = Evening
            Assert.AreEqual(MonsterSpawner.TimePeriod.Evening, _spawner.GetTimePeriod(18), "18시 = Evening");
            Assert.AreEqual(MonsterSpawner.TimePeriod.Evening, _spawner.GetTimePeriod(19), "19시 = Evening");
            // 4~6 = Evening
            Assert.AreEqual(MonsterSpawner.TimePeriod.Evening, _spawner.GetTimePeriod(4), "4시 = Evening");
            Assert.AreEqual(MonsterSpawner.TimePeriod.Evening, _spawner.GetTimePeriod(5), "5시 = Evening");
        }

        [Test]
        public void GetTimePeriod_Night_ReturnsNight()
        {
            // 20~4 = Night
            Assert.AreEqual(MonsterSpawner.TimePeriod.Night, _spawner.GetTimePeriod(20), "20시 = Night");
            Assert.AreEqual(MonsterSpawner.TimePeriod.Night, _spawner.GetTimePeriod(23), "23시 = Night");
            Assert.AreEqual(MonsterSpawner.TimePeriod.Night, _spawner.GetTimePeriod(0), "0시 = Night");
            Assert.AreEqual(MonsterSpawner.TimePeriod.Night, _spawner.GetTimePeriod(3), "3시 = Night");
        }

        [Test]
        public void GetTimePeriod_AllHours_NoGaps()
        {
            // 모든 시간(0~23)이 Day/Evening/Night 중 하나여야 함
            for (int h = 0; h < 24; h++)
            {
                MonsterSpawner.TimePeriod period = _spawner.GetTimePeriod(h);
                Assert.IsTrue(
                    period == MonsterSpawner.TimePeriod.Day ||
                    period == MonsterSpawner.TimePeriod.Evening ||
                    period == MonsterSpawner.TimePeriod.Night,
                    $"{h}시는 유효한 TimePeriod여야 함");
            }
        }

        // ================================================================
        // C18-02: 스폰 확률표 기본값
        // ================================================================

        [Test]
        public void DayProb_DefaultValues_AreCorrect()
        {
            Assert.AreEqual(0.80f, _spawner.DayProb.common, 0.001f, "Day Prob common = 0.80");
            Assert.AreEqual(0.15f, _spawner.DayProb.elite, 0.001f, "Day Prob elite = 0.15");
            Assert.AreEqual(0.05f, _spawner.DayProb.boss, 0.001f, "Day Prob boss = 0.05");
        }

        [Test]
        public void EveningProb_DefaultValues_AreCorrect()
        {
            Assert.AreEqual(0.50f, _spawner.EveningProb.common, 0.001f, "Evening Prob common = 0.50");
            Assert.AreEqual(0.40f, _spawner.EveningProb.elite, 0.001f, "Evening Prob elite = 0.40");
            Assert.AreEqual(0.10f, _spawner.EveningProb.boss, 0.001f, "Evening Prob boss = 0.10");
        }

        [Test]
        public void NightProb_DefaultValues_AreCorrect()
        {
            Assert.AreEqual(0.20f, _spawner.NightProb.common, 0.001f, "Night Prob common = 0.20");
            Assert.AreEqual(0.60f, _spawner.NightProb.elite, 0.001f, "Night Prob elite = 0.60");
            Assert.AreEqual(0.20f, _spawner.NightProb.boss, 0.001f, "Night Prob boss = 0.20");
        }

        [Test]
        public void Probabilities_SumToOne()
        {
            // 각 확률표의 합이 1.0에 가까워야 함
            float daySum = _spawner.DayProb.common + _spawner.DayProb.elite + _spawner.DayProb.boss;
            float eveSum = _spawner.EveningProb.common + _spawner.EveningProb.elite + _spawner.EveningProb.boss;
            float nightSum = _spawner.NightProb.common + _spawner.NightProb.elite + _spawner.NightProb.boss;

            Assert.AreEqual(1.0f, daySum, 0.001f, "Day 확률 합 = 1.0");
            Assert.AreEqual(1.0f, eveSum, 0.001f, "Evening 확률 합 = 1.0");
            Assert.AreEqual(1.0f, nightSum, 0.001f, "Night 확률 합 = 1.0");
        }

        // ================================================================
        // C18-02: CurrentPeriod 변화 (TimeManager 기반)
        // ================================================================

        [Test]
        public void CurrentPeriod_ChangesWithHour_Day()
        {
            _timeManager.GameTime = 12 * 3600f; // 12:00 (Day)
            _spawner.SpawnAll();
            Assert.AreEqual(MonsterSpawner.TimePeriod.Day, _spawner.CurrentPeriod, "12시 = Day");
        }

        [Test]
        public void CurrentPeriod_ChangesWithHour_Night()
        {
            _timeManager.GameTime = 22 * 3600f; // 22:00 (Night)
            _spawner.SpawnAll();
            Assert.AreEqual(MonsterSpawner.TimePeriod.Night, _spawner.CurrentPeriod, "22시 = Night");
        }

        [Test]
        public void CurrentPeriod_ChangesWithHour_Evening()
        {
            _timeManager.GameTime = 19 * 3600f; // 19:00 (Evening)
            _spawner.SpawnAll();
            Assert.AreEqual(MonsterSpawner.TimePeriod.Evening, _spawner.CurrentPeriod, "19시 = Evening");
        }

        // ================================================================
        // C18-03: 밤 리스폰 속도 배율
        // ================================================================

        [Test]
        public void NightRespawnRateMultiplier_DefaultIs1Point5()
        {
            Assert.AreEqual(1.5f, _spawner.NightRespawnRateMultiplier, 0.001f,
                "_nightRespawnRateMultiplier 기본값 = 1.5");
        }

        [Test]
        public void GetCurrentProbabilities_Day_ReturnsDayProb()
        {
            _timeManager.GameTime = 12 * 3600f; // 12:00 (Day)
            _spawner.SpawnAll();

            // Day/Evening/Night에 따라 확률표가 달라지는지 확인
            Assert.AreEqual(MonsterSpawner.TimePeriod.Day, _spawner.CurrentPeriod,
                "12시는 Day여야 함");
        }

        [Test]
        public void GetCurrentProbabilities_Night_ReturnsNightProb()
        {
            _timeManager.GameTime = 0 * 3600f; // 0:00 (Night)
            _spawner.SpawnAll();

            Assert.AreEqual(MonsterSpawner.TimePeriod.Night, _spawner.CurrentPeriod,
                "0시는 Night여야 함");
        }

        [Test]
        public void GetCurrentProbabilities_Evening_ReturnsEveningProb()
        {
            _timeManager.GameTime = 18 * 3600f + 30 * 60f; // 18:30 (Evening)
            _spawner.SpawnAll();

            Assert.AreEqual(MonsterSpawner.TimePeriod.Evening, _spawner.CurrentPeriod,
                "18:30은 Evening이어야 함");
        }
    }
}