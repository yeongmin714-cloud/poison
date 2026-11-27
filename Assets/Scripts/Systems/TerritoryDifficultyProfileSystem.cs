using System;
using UnityEngine;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// Per-territory difficulty tier. Assigned once from a stable hash of the territory ID.
    /// </summary>
    public enum TerritoryDifficultyProfileTier
    {
        Standard = 0,
        Veteran = 1,
        Hard = 2
    }

    /// <summary>
    /// All scaling knobs for the territory difficulty/tax profile policy, kept in one place.
    /// Tier buckets: bucket = StableHash(territoryId, channel 0) % BucketCount.
    /// Standard: bucket &lt; StandardBucketEnd (40%), Veteran: bucket &lt; VeteranBucketEnd (35%), Hard: rest (25%).
    /// </summary>
    public static class TerritoryDifficultyProfileKnobs
    {
        public const int BucketCount = 100;
        public const int StandardBucketEnd = 40;
        public const int VeteranBucketEnd = 75;

        /// <summary>Base tax in percent (100 = 1.0x).</summary>
        public const int BaseTaxPercent = 100;

        /// <summary>Within-ring base tax variation, applied as +/- this many percent.</summary>
        public const int MaxTaxVariationPercent = 10;

        /// <summary>Tier tax uplift in percent. Must be strictly increasing across tiers.</summary>
        public const int StandardTaxUpliftPercent = 0;
        public const int VeteranTaxUpliftPercent = 10;
        public const int HardTaxUpliftPercent = 20;

        /// <summary>Lowest allowed tax percent after clamping (never zero or negative).</summary>
        public const int MinTaxPercent = 1;

        /// <summary>
        /// Guard level draws: result is the maximum of N uniform draws within the range.
        /// N=1 is uniform (Standard), higher N biases toward the upper end of the band.
        /// </summary>
        public const int StandardGuardDraws = 1;
        public const int VeteranGuardDraws = 2;
        public const int HardGuardDraws = 3;
    }

    /// <summary>
    /// Resolved, immutable difficulty/tax profile for one territory.
    /// </summary>
    public readonly struct TerritoryDifficultyProfile
    {
        public readonly int TerritoryId;
        public readonly NationType Nation;
        public readonly TerritoryDifficulty Ring;
        public readonly TerritoryDifficultyProfileTier Tier;
        public readonly int Bucket;
        public readonly int TaxVariationPercent;
        public readonly int TaxPercent;
        public readonly float TaxMultiplier;
        public readonly Vector2Int GuardLevelRange;

        public TerritoryDifficultyProfile(
            int territoryId,
            NationType nation,
            TerritoryDifficulty ring,
            TerritoryDifficultyProfileTier tier,
            int bucket,
            int taxVariationPercent,
            int taxPercent,
            Vector2Int guardLevelRange)
        {
            TerritoryId = territoryId;
            Nation = nation;
            Ring = ring;
            Tier = tier;
            Bucket = bucket;
            TaxVariationPercent = taxVariationPercent;
            TaxPercent = taxPercent;
            TaxMultiplier = taxPercent / 100f;
            GuardLevelRange = guardLevelRange;
        }

        /// <summary>
        /// Selects a guard level for the given soldier index. Deterministic for the same
        /// territory ID, tier, range, and soldier index. Always within GuardLevelRange.
        /// </summary>
        public int SelectGuardLevel(int soldierIndex)
        {
            return TerritoryDifficultyProfileSystem.SelectGuardLevel(TerritoryId, Tier, GuardLevelRange, soldierIndex);
        }
    }

    /// <summary>
    /// Pure, deterministic per-territory difficulty/tax profile policy.
    /// No Unity runtime state, no UnityEngine.Random, no string.GetHashCode.
    /// Not yet integrated with AI tax, spawning, save, or UI.
    /// </summary>
    public static class TerritoryDifficultyProfileSystem
    {
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;
        private const uint GoldenRatio = 0x9E3779B9u;

        private const uint ChannelTier = 0u;
        private const uint ChannelTaxVariation = 1u;
        private const uint ChannelGuard = 2u;

        /// <summary>
        /// Resolves the profile for a territory. Country and ring are retained as given and
        /// the guard level range is taken from RingDifficultyData (not modified here).
        /// </summary>
        public static TerritoryDifficultyProfile Resolve(int territoryId, NationType nation, TerritoryDifficulty ring)
        {
            int bucket = GetBucket(territoryId);
            var tier = GetTierFromBucket(bucket);
            int variation = GetTaxVariationPercent(territoryId);
            int taxPercent = GetTaxPercent(tier, variation);
            Vector2Int range = RingDifficultyData.GetGuardLevelRange(nation, ring);

            return new TerritoryDifficultyProfile(territoryId, nation, ring, tier, bucket, variation, taxPercent, range);
        }

        /// <summary>Tier bucket in [0, BucketCount).</summary>
        public static int GetBucket(int territoryId)
        {
            return (int)(StableHash(territoryId, ChannelTier) % (uint)TerritoryDifficultyProfileKnobs.BucketCount);
        }

        public static TerritoryDifficultyProfileTier GetTierFromBucket(int bucket)
        {
            if (bucket < TerritoryDifficultyProfileKnobs.StandardBucketEnd)
                return TerritoryDifficultyProfileTier.Standard;
            if (bucket < TerritoryDifficultyProfileKnobs.VeteranBucketEnd)
                return TerritoryDifficultyProfileTier.Veteran;
            return TerritoryDifficultyProfileTier.Hard;
        }

        /// <summary>Within-ring base tax variation in [-MaxTaxVariationPercent, +MaxTaxVariationPercent].</summary>
        public static int GetTaxVariationPercent(int territoryId)
        {
            int span = TerritoryDifficultyProfileKnobs.MaxTaxVariationPercent * 2 + 1;
            return (int)(StableHash(territoryId, ChannelTaxVariation) % (uint)span)
                   - TerritoryDifficultyProfileKnobs.MaxTaxVariationPercent;
        }

        public static int GetTierUpliftPercent(TerritoryDifficultyProfileTier tier)
        {
            switch (tier)
            {
                case TerritoryDifficultyProfileTier.Veteran: return TerritoryDifficultyProfileKnobs.VeteranTaxUpliftPercent;
                case TerritoryDifficultyProfileTier.Hard:    return TerritoryDifficultyProfileKnobs.HardTaxUpliftPercent;
                default:                                     return TerritoryDifficultyProfileKnobs.StandardTaxUpliftPercent;
            }
        }

        /// <summary>
        /// Tax percent = base + tier uplift + variation, clamped to at least MinTaxPercent.
        /// All integer math so same-ring tier differences are never rounded away.
        /// </summary>
        public static int GetTaxPercent(TerritoryDifficultyProfileTier tier, int variationPercent)
        {
            int value = TerritoryDifficultyProfileKnobs.BaseTaxPercent
                        + GetTierUpliftPercent(tier)
                        + variationPercent;
            return Math.Max(TerritoryDifficultyProfileKnobs.MinTaxPercent, value);
        }

        /// <summary>
        /// Deterministic guard level within [range.x, range.y]. Higher tiers take the max of
        /// more uniform draws, which biases toward the upper end without exceeding the band.
        /// </summary>
        public static int SelectGuardLevel(int territoryId, TerritoryDifficultyProfileTier tier, Vector2Int range, int soldierIndex)
        {
            int lo = Math.Min(range.x, range.y);
            int hi = Math.Max(range.x, range.y);
            if (lo == hi)
                return lo;

            int draws = GetGuardDraws(tier);
            int seed = (int)HashCombine(StableHash(territoryId, ChannelGuard), unchecked((uint)soldierIndex));
            var rng = new System.Random(seed);

            int result = lo;
            for (int i = 0; i < draws; i++)
            {
                int candidate = rng.Next(lo, hi + 1);
                if (candidate > result)
                    result = candidate;
            }
            return Math.Clamp(result, lo, hi);
        }

        public static int GetGuardDraws(TerritoryDifficultyProfileTier tier)
        {
            switch (tier)
            {
                case TerritoryDifficultyProfileTier.Veteran: return TerritoryDifficultyProfileKnobs.VeteranGuardDraws;
                case TerritoryDifficultyProfileTier.Hard:    return TerritoryDifficultyProfileKnobs.HardGuardDraws;
                default:                                     return TerritoryDifficultyProfileKnobs.StandardGuardDraws;
            }
        }

        /// <summary>
        /// Stable 32-bit hash of (territoryId, channel). FNV-1a over the little-endian ID bytes,
        /// mixed with the channel, then finalized with murmur3 fmix32. Platform-independent.
        /// </summary>
        public static uint StableHash(int territoryId, uint channel)
        {
            unchecked
            {
                uint h = FnvOffsetBasis;
                uint id = (uint)territoryId;
                for (int i = 0; i < 4; i++)
                {
                    h ^= (id >> (8 * i)) & 0xFFu;
                    h *= FnvPrime;
                }
                h ^= channel * GoldenRatio;
                return FinalMix(h);
            }
        }

        private static uint HashCombine(uint a, uint b)
        {
            unchecked
            {
                return FinalMix(a ^ (b * GoldenRatio + 0x7F4A7C15u + (a << 6) + (a >> 2)));
            }
        }

        private static uint FinalMix(uint h)
        {
            unchecked
            {
                h ^= h >> 16;
                h *= 0x85EBCA6Bu;
                h ^= h >> 13;
                h *= 0xC2B2AE35u;
                h ^= h >> 16;
                return h;
            }
        }
    }
}
