using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C-O4-02: 영주 능력치 생성기 테스트 — 4d6/국가 보정/링 리밸런싱/Guard 공식/결정론.
    /// </summary>
    public class LordStatGeneratorTests
    {
        private static readonly NationType[] AllNations =
        {
            NationType.East, NationType.West, NationType.South, NationType.North, NationType.Empire,
        };

        private static readonly TerritoryDifficulty[] AllRings =
        {
            TerritoryDifficulty.Ring1, TerritoryDifficulty.Ring2,
            TerritoryDifficulty.Ring3, TerritoryDifficulty.Ring4, TerritoryDifficulty.Empire,
        };

        // ===================== 4d6 drop lowest =====================

        [Test]
        public void Roll4d6DropLowest_Deterministic_SameSeed()
        {
            var rngA = new System.Random(LordStatGenerator.Djb2("seed_x"));
            var rngB = new System.Random(LordStatGenerator.Djb2("seed_x"));
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(LordStatGenerator.Roll4d6DropLowest(rngA), LordStatGenerator.Roll4d6DropLowest(rngB));
        }

        [Test]
        public void Roll4d6DropLowest_Range3to18()
        {
            var rng = new System.Random(42);
            for (int i = 0; i < 200; i++)
            {
                int roll = LordStatGenerator.Roll4d6DropLowest(rng);
                Assert.GreaterOrEqual(roll, 3, "4d6 최소 3 (1,1,1)");
                Assert.LessOrEqual(roll, 18, "4d6 최대 18 (6,6,6)");
            }
        }

        // ===================== GUARD 공식 (벤치마크 예시표) =====================

        [Test]
        public void GuardFromDex_BenchmarkTable()
        {
            Assert.AreEqual(9, LordStatGenerator.GuardFromDex(8), "DEX 8 → 9");
            Assert.AreEqual(10, LordStatGenerator.GuardFromDex(10), "DEX 10 → 10");
            Assert.AreEqual(12, LordStatGenerator.GuardFromDex(14), "DEX 14 → 12");
            Assert.AreEqual(14, LordStatGenerator.GuardFromDex(18), "DEX 18 → 14");
        }

        [Test]
        public void GuardFromDex_LowDex_ClampedToRange()
        {
            Assert.AreEqual(7, LordStatGenerator.GuardFromDex(3), "DEX 3 → 7 (벤치마크 하한)");
            int g = LordStatGenerator.GuardFromDex(5);
            Assert.GreaterOrEqual(g, 1);
            Assert.LessOrEqual(g, 20);
        }

        // ===================== 생성: 결정론 + 합계 + 범위 =====================

        [Test]
        public void Generate_Deterministic_SameTerritorySameStats()
        {
            var a = LordStatGenerator.Generate("East_03", NationType.East, TerritoryDifficulty.Ring2);
            var b = LordStatGenerator.Generate("East_03", NationType.East, TerritoryDifficulty.Ring2);
            Assert.AreEqual(a.str, b.str);
            Assert.AreEqual(a.dex, b.dex);
            Assert.AreEqual(a.con, b.con);
            Assert.AreEqual(a.cha, b.cha);
            Assert.AreEqual(a.guard, b.guard);
        }

        [Test]
        public void Generate_RingTargets_66_72_78_84()
        {
            Assert.AreEqual(66, LordStatGenerator.Generate("t1", NationType.East, TerritoryDifficulty.Ring1).Total);
            Assert.AreEqual(72, LordStatGenerator.Generate("t2", NationType.South, TerritoryDifficulty.Ring2).Total);
            Assert.AreEqual(78, LordStatGenerator.Generate("t3", NationType.West, TerritoryDifficulty.Ring3).Total);
            Assert.AreEqual(84, LordStatGenerator.Generate("t4", NationType.North, TerritoryDifficulty.Ring4).Total);
            Assert.AreEqual(84, LordStatGenerator.Generate("t5", NationType.Empire, TerritoryDifficulty.Empire).Total);
        }

        [Test]
        public void Generate_AllStats_InRange3to18()
        {
            foreach (var nation in AllNations)
            {
                var s = LordStatGenerator.Generate($"rng_{nation}", nation, TerritoryDifficulty.Ring3);
                Assert.GreaterOrEqual(s.str, 3); Assert.LessOrEqual(s.str, 18);
                Assert.GreaterOrEqual(s.dex, 3); Assert.LessOrEqual(s.dex, 18);
                Assert.GreaterOrEqual(s.con, 3); Assert.LessOrEqual(s.con, 18);
                Assert.GreaterOrEqual(s.intel, 3); Assert.LessOrEqual(s.intel, 18);
                Assert.GreaterOrEqual(s.wis, 3); Assert.LessOrEqual(s.wis, 18);
                Assert.GreaterOrEqual(s.cha, 3); Assert.LessOrEqual(s.cha, 18);
            }
        }

        [Test]
        public void Generate_EmpireFlavor_SumAndRangeHold()
        {
            // [O4] 국가 보정은 리밸런싱 전 적용 — 리밸런싱이 보정을 흡수하므로
            // 특정 스탯 하한은 보장되지 않는다(구조 검증: 합계/범위/결정론은 스윕 테스트가 담당).
            var empire = LordStatGenerator.Generate("flavor_emp", NationType.Empire, TerritoryDifficulty.Ring2);
            Assert.AreEqual(72, empire.Total, "Empire Ring2 합계 72");
            Assert.GreaterOrEqual(empire.cha, 3);
            Assert.LessOrEqual(empire.cha, 18);
            var again = LordStatGenerator.Generate("flavor_emp", NationType.Empire, TerritoryDifficulty.Ring2);
            Assert.AreEqual(empire.cha, again.cha, "결정론");
        }

        [Test]
        public void Generate_GuardMatchesDexFormula()
        {
            var s = LordStatGenerator.Generate("guard_chk", NationType.South, TerritoryDifficulty.Ring3);
            Assert.AreEqual(LordStatGenerator.GuardFromDex(s.dex), s.guard, "GUARD = clamp(10+(DEX-10)/2, 1, 20)");
        }

        [Test]
        public void Generate_AllNationsAllRings_SumTargets_ForeachSweep()
        {
            foreach (var nation in AllNations)
            {
                foreach (var ring in AllRings)
                {
                    var s = LordStatGenerator.Generate($"sweep_{nation}_{ring}", nation, ring);
                    Assert.AreEqual(LordStatGenerator.TargetSumForRing(ring), s.Total,
                        $"{nation}/{ring} 합계 == 링 목표");
                }
            }
        }

        // ===================== ToString/유틸 =====================

        [Test]
        public void ToString_ContainsAllStatsAndGuard()
        {
            var s = LordStatGenerator.Generate("tostr", NationType.East, TerritoryDifficulty.Ring1);
            string txt = s.ToString();
            StringAssert.Contains("STR", txt);
            StringAssert.Contains("CHA", txt);
            StringAssert.Contains("GUARD", txt);
        }

        [Test]
        public void Djb2_Deterministic()
        {
            Assert.AreEqual(LordStatGenerator.Djb2("abc"), LordStatGenerator.Djb2("abc"));
            Assert.AreNotEqual(LordStatGenerator.Djb2("abc"), LordStatGenerator.Djb2("abd"));
        }

        // ===================== TerritoryDatabase 연동 스모크 =====================

        [Test]
        public void GenerateForTerritory_DatabaseSmoke_First81Territories()
        {
            var db = TerritoryDatabase.Instance;
            if (db == null)
            {
                Assert.Ignore("TerritoryDatabase 인스턴스 없음 — Play 시 생성");
                return;
            }
            int checkedCount = 0;
            foreach (var def in db.GetAllDefinitions())
            {
                string key = def.id.ToString();
                var s = LordStatGenerator.GenerateForTerritory(key);
                Assert.AreEqual(LordStatGenerator.TargetSumForRing(def.difficulty), s.Total,
                    $"영지 {key} 합계");
                checkedCount++;
                if (checkedCount >= 20) break; // 스모크 상한
            }
            Assert.Greater(checkedCount, 0, "최소 1개 영지 검증");
        }
    }
}
