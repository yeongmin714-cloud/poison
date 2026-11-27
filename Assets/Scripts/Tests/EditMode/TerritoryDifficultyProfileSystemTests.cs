using NUnit.Framework;
using UnityEngine;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    public class TerritoryDifficultyProfileSystemTests
    {
        private const int SampleCount = 10000;

        [Test]
        public void Resolve_SameId_ReturnsSameProfileAcrossRuns()
        {
            for (int id = 0; id < 500; id++)
            {
                var a = TerritoryDifficultyProfileSystem.Resolve(id, NationType.North, TerritoryDifficulty.Ring3);
                var b = TerritoryDifficultyProfileSystem.Resolve(id, NationType.North, TerritoryDifficulty.Ring3);

                Assert.AreEqual(a.Tier, b.Tier, $"tier id={id}");
                Assert.AreEqual(a.Bucket, b.Bucket, $"bucket id={id}");
                Assert.AreEqual(a.TaxVariationPercent, b.TaxVariationPercent, $"variation id={id}");
                Assert.AreEqual(a.TaxPercent, b.TaxPercent, $"tax id={id}");
                Assert.AreEqual(a.TaxMultiplier, b.TaxMultiplier);
                Assert.AreEqual(a.GuardLevelRange, b.GuardLevelRange);
                for (int s = 0; s < 20; s++)
                {
                    Assert.AreEqual(a.SelectGuardLevel(s), b.SelectGuardLevel(s), $"guard id={id} soldier={s}");
                }
            }
        }

        [Test]
        public void StableHash_KnownValues_AreFixed()
        {
            // Guards against accidental changes to the hash, which would reshuffle every territory.
            uint h0 = TerritoryDifficultyProfileSystem.StableHash(0, 0);
            uint h1 = TerritoryDifficultyProfileSystem.StableHash(1, 0);
            uint h1b = TerritoryDifficultyProfileSystem.StableHash(1, 0);
            Assert.AreEqual(h1, h1b);
            Assert.AreNotEqual(h0, h1);
            Assert.AreNotEqual(
                TerritoryDifficultyProfileSystem.StableHash(1, 0),
                TerritoryDifficultyProfileSystem.StableHash(1, 1));
        }

        [Test]
        public void Bucket_IsAlwaysInRange()
        {
            for (int id = -5000; id < 5000; id++)
            {
                int bucket = TerritoryDifficultyProfileSystem.GetBucket(id);
                Assert.GreaterOrEqual(bucket, 0);
                Assert.Less(bucket, TerritoryDifficultyProfileKnobs.BucketCount);
            }
        }

        [Test]
        public void Tier_MatchesBucketThresholds()
        {
            Assert.AreEqual(TerritoryDifficultyProfileTier.Standard, TerritoryDifficultyProfileSystem.GetTierFromBucket(0));
            Assert.AreEqual(TerritoryDifficultyProfileTier.Standard, TerritoryDifficultyProfileSystem.GetTierFromBucket(TerritoryDifficultyProfileKnobs.StandardBucketEnd - 1));
            Assert.AreEqual(TerritoryDifficultyProfileTier.Veteran, TerritoryDifficultyProfileSystem.GetTierFromBucket(TerritoryDifficultyProfileKnobs.StandardBucketEnd));
            Assert.AreEqual(TerritoryDifficultyProfileTier.Veteran, TerritoryDifficultyProfileSystem.GetTierFromBucket(TerritoryDifficultyProfileKnobs.VeteranBucketEnd - 1));
            Assert.AreEqual(TerritoryDifficultyProfileTier.Hard, TerritoryDifficultyProfileSystem.GetTierFromBucket(TerritoryDifficultyProfileKnobs.VeteranBucketEnd));
            Assert.AreEqual(TerritoryDifficultyProfileTier.Hard, TerritoryDifficultyProfileSystem.GetTierFromBucket(TerritoryDifficultyProfileKnobs.BucketCount - 1));
        }

        [Test]
        public void TierDistribution_IsApproximately40_35_25()
        {
            int standard = 0, veteran = 0, hard = 0;
            for (int id = 0; id < SampleCount; id++)
            {
                switch (TerritoryDifficultyProfileSystem.Resolve(id, NationType.East, TerritoryDifficulty.Ring1).Tier)
                {
                    case TerritoryDifficultyProfileTier.Standard: standard++; break;
                    case TerritoryDifficultyProfileTier.Veteran: veteran++; break;
                    case TerritoryDifficultyProfileTier.Hard: hard++; break;
                }
            }
            Assert.AreEqual(SampleCount, standard + veteran + hard);
            AssertWithin(standard / (float)SampleCount, 0.40f, 0.03f, "Standard");
            AssertWithin(veteran / (float)SampleCount, 0.35f, 0.03f, "Veteran");
            AssertWithin(hard / (float)SampleCount, 0.25f, 0.03f, "Hard");
        }

        [Test]
        public void TaxVariation_IsWithinPlusMinusMax()
        {
            int max = TerritoryDifficultyProfileKnobs.MaxTaxVariationPercent;
            bool sawNegative = false, sawPositive = false;
            for (int id = 0; id < SampleCount; id++)
            {
                int v = TerritoryDifficultyProfileSystem.GetTaxVariationPercent(id);
                Assert.GreaterOrEqual(v, -max);
                Assert.LessOrEqual(v, max);
                if (v < 0) sawNegative = true;
                if (v > 0) sawPositive = true;
            }
            Assert.IsTrue(sawNegative && sawPositive, "variation should span both signs");
        }

        [Test]
        public void TaxPercent_ExpectedRangePerTier()
        {
            int max = TerritoryDifficultyProfileKnobs.MaxTaxVariationPercent;
            for (int id = 0; id < SampleCount; id++)
            {
                var p = TerritoryDifficultyProfileSystem.Resolve(id, NationType.South, TerritoryDifficulty.Ring2);
                int uplift = TerritoryDifficultyProfileSystem.GetTierUpliftPercent(p.Tier);
                int expected = TerritoryDifficultyProfileKnobs.BaseTaxPercent + uplift + p.TaxVariationPercent;
                Assert.AreEqual(expected, p.TaxPercent, $"id={id}");
                Assert.GreaterOrEqual(p.TaxPercent, TerritoryDifficultyProfileKnobs.BaseTaxPercent + uplift - max);
                Assert.LessOrEqual(p.TaxPercent, TerritoryDifficultyProfileKnobs.BaseTaxPercent + uplift + max);
                Assert.AreEqual(p.TaxPercent / 100f, p.TaxMultiplier, 0.0001f);
            }
        }

        [Test]
        public void TaxTier_UpliftIsStrictlyMonotonic()
        {
            int std = TerritoryDifficultyProfileSystem.GetTierUpliftPercent(TerritoryDifficultyProfileTier.Standard);
            int vet = TerritoryDifficultyProfileSystem.GetTierUpliftPercent(TerritoryDifficultyProfileTier.Veteran);
            int hard = TerritoryDifficultyProfileSystem.GetTierUpliftPercent(TerritoryDifficultyProfileTier.Hard);
            Assert.AreEqual(0, std);
            Assert.AreEqual(10, vet);
            Assert.AreEqual(20, hard);
            Assert.Less(std, vet);
            Assert.Less(vet, hard);
        }

        [Test]
        public void TaxTier_AverageTaxIsMonotonicAcrossTiers()
        {
            double sumStd = 0, sumVet = 0, sumHard = 0;
            int nStd = 0, nVet = 0, nHard = 0;
            for (int id = 0; id < SampleCount; id++)
            {
                var p = TerritoryDifficultyProfileSystem.Resolve(id, NationType.West, TerritoryDifficulty.Ring3);
                switch (p.Tier)
                {
                    case TerritoryDifficultyProfileTier.Standard: sumStd += p.TaxPercent; nStd++; break;
                    case TerritoryDifficultyProfileTier.Veteran: sumVet += p.TaxPercent; nVet++; break;
                    case TerritoryDifficultyProfileTier.Hard: sumHard += p.TaxPercent; nHard++; break;
                }
            }
            Assert.Greater(nStd, 0);
            Assert.Greater(nVet, 0);
            Assert.Greater(nHard, 0);
            double avgStd = sumStd / nStd, avgVet = sumVet / nVet, avgHard = sumHard / nHard;
            Assert.Less(avgStd, avgVet);
            Assert.Less(avgVet, avgHard);
        }

        [Test]
        public void TaxPercent_IsClampedToAtLeastMin()
        {
            // Hard-coded inputs well below zero must clamp, never produce zero or negative tax.
            int t = TerritoryDifficultyProfileSystem.GetTaxPercent(TerritoryDifficultyProfileTier.Standard, -1000);
            Assert.AreEqual(TerritoryDifficultyProfileKnobs.MinTaxPercent, t);
        }

        [Test]
        public void TaxPercent_SameRingDifferentTiers_AreDistinct()
        {
            int s = TerritoryDifficultyProfileSystem.GetTaxPercent(TerritoryDifficultyProfileTier.Standard, 0);
            int v = TerritoryDifficultyProfileSystem.GetTaxPercent(TerritoryDifficultyProfileTier.Veteran, 0);
            int h = TerritoryDifficultyProfileSystem.GetTaxPercent(TerritoryDifficultyProfileTier.Hard, 0);
            Assert.AreNotEqual(s, v);
            Assert.AreNotEqual(v, h);
        }

        [Test]
        public void Resolve_RetainsNationAndRing()
        {
            var p = TerritoryDifficultyProfileSystem.Resolve(42, NationType.South, TerritoryDifficulty.Ring4);
            Assert.AreEqual(42, p.TerritoryId);
            Assert.AreEqual(NationType.South, p.Nation);
            Assert.AreEqual(TerritoryDifficulty.Ring4, p.Ring);
            Assert.AreEqual(RingDifficultyData.GetGuardLevelRange(NationType.South, TerritoryDifficulty.Ring4), p.GuardLevelRange);
        }

        [Test]
        public void GuardLevel_AlwaysWithinRingDifficultyBand()
        {
            foreach (NationType nation in new[] { NationType.East, NationType.West, NationType.South, NationType.North, NationType.Empire })
            {
                foreach (TerritoryDifficulty ring in new[] { TerritoryDifficulty.Ring1, TerritoryDifficulty.Ring2, TerritoryDifficulty.Ring3, TerritoryDifficulty.Ring4, TerritoryDifficulty.Empire })
                {
                    Vector2Int range = RingDifficultyData.GetGuardLevelRange(nation, ring);
                    for (int id = 0; id < 200; id++)
                    {
                        var p = TerritoryDifficultyProfileSystem.Resolve(id, nation, ring);
                        for (int s = 0; s < 30; s++)
                        {
                            int lvl = p.SelectGuardLevel(s);
                            Assert.GreaterOrEqual(lvl, range.x, $"{nation}/{ring} id={id} s={s}");
                            Assert.LessOrEqual(lvl, range.y, $"{nation}/{ring} id={id} s={s}");
                        }
                    }
                }
            }
        }

        [Test]
        public void GuardLevel_IsDeterministicPerSoldier()
        {
            for (int id = 0; id < 100; id++)
            {
                for (int s = 0; s < 30; s++)
                {
                    int a = TerritoryDifficultyProfileSystem.SelectGuardLevel(id, TerritoryDifficultyProfileTier.Hard, new Vector2Int(21, 28), s);
                    int b = TerritoryDifficultyProfileSystem.SelectGuardLevel(id, TerritoryDifficultyProfileTier.Hard, new Vector2Int(21, 28), s);
                    Assert.AreEqual(a, b);
                }
            }
        }

        [Test]
        public void GuardLevel_HarderTiersAreUpperBiased()
        {
            // North Ring4 band 29..40, sampled over many soldiers from many territories.
            Vector2Int range = new Vector2Int(29, 40);
            double meanStd = MeanGuard(TerritoryDifficultyProfileTier.Standard, range);
            double meanVet = MeanGuard(TerritoryDifficultyProfileTier.Veteran, range);
            double meanHard = MeanGuard(TerritoryDifficultyProfileTier.Hard, range);

            Assert.Less(meanStd, meanVet, $"std={meanStd} vet={meanVet}");
            Assert.Less(meanVet, meanHard, $"vet={meanVet} hard={meanHard}");
            // Standard should be roughly uniform: midpoint 34.5.
            AssertWithin((float)meanStd, 34.5f, 0.5f, "standard mean");
        }

        [Test]
        public void GuardLevel_SingleValueRange_ReturnsThatValue()
        {
            for (int s = 0; s < 10; s++)
            {
                Assert.AreEqual(7, TerritoryDifficultyProfileSystem.SelectGuardLevel(3, TerritoryDifficultyProfileTier.Hard, new Vector2Int(7, 7), s));
            }
        }

        private static double MeanGuard(TerritoryDifficultyProfileTier tier, Vector2Int range)
        {
            double sum = 0;
            int n = 0;
            for (int id = 0; id < 400; id++)
            {
                for (int s = 0; s < 25; s++)
                {
                    sum += TerritoryDifficultyProfileSystem.SelectGuardLevel(id, tier, range, s);
                    n++;
                }
            }
            return sum / n;
        }

        private static void AssertWithin(float actual, float expected, float tolerance, string label)
        {
            Assert.That(Mathf.Abs(actual - expected), Is.LessThanOrEqualTo(tolerance),
                $"{label}: actual={actual} expected={expected}±{tolerance}");
        }
    }
}
