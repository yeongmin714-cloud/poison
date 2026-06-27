using System;
using UnityEngine;

using ProjectName.Core;
namespace ProjectName.Core
{
    /// <summary>
    /// 영지 ID 기반 시드 랜덤 유틸리티.
    /// 같은 영지(territoryId)는 항상 같은 랜덤값을 반환합니다.
    /// </summary>
    public static class InteriorRandomizer
    {
        private const int SALT = 0x5EED_1234;

        /// <summary>
        /// 문자열에 대한 결정적(deterministic) 해시를 계산합니다.
        /// string.GetHashCode()는 .NET Core 3.0+에서 플랫폼/런타임/실행마다
        /// 결과가 달라지므로 사용할 수 없습니다. 대신 Bernstein 해시를 사용합니다.
        /// </summary>
        private static int GetDeterministicHash(string str)
        {
            unchecked
            {
                int hash = 17;
                foreach (char c in str)
                    hash = hash * 31 + c;
                return hash;
            }
        }

        /// <summary>
        /// 영지 ID로 결정적(deterministic) 시드 생성 (Bernstein 해시 + 고정 Salt)
        /// </summary>
        public static int GetSeed(string territoryId)
        {
            if (territoryId == null)
                throw new ArgumentNullException(nameof(territoryId));

            unchecked
            {
                int hash = GetDeterministicHash(territoryId);
                return hash ^ SALT;
            }
        }

        /// <summary>
        /// 지정된 영지의 시드로 [min, max) 범위 랜덤 정수를 반환합니다.
        /// territoryId가 null/empty/"default"이면 min을 반환합니다 (fallback).
        /// </summary>
        public static int Range(string territoryId, int min, int max)
        {
            if (string.IsNullOrEmpty(territoryId) || territoryId == "default")
                return min;

            int seed = GetSeed(territoryId);
            var rng = new System.Random(seed);
            return rng.Next(min, max);
        }

        /// <summary>
        /// 지정된 영지의 시드로 [min, max] 범위 랜덤 실수를 반환합니다.
        /// territoryId가 null/empty/"default"이면 min을 반환합니다 (fallback).
        /// </summary>
        public static float Range(string territoryId, float min, float max)
        {
            if (string.IsNullOrEmpty(territoryId) || territoryId == "default")
                return min;

            int seed = GetSeed(territoryId);
            var rng = new System.Random(seed);
            double t = rng.NextDouble(); // [0.0, 1.0)
            return (float)(min + t * (max - min));
        }

        /// <summary>
        /// 지정된 영지의 시드로 확률 체크 (0.0~1.0).
        /// territoryId가 null/empty/"default"이면 항상 false를 반환합니다 (fallback).
        /// </summary>
        public static bool Chance(string territoryId, float probability)
        {
            if (string.IsNullOrEmpty(territoryId) || territoryId == "default")
                return false;

            int seed = GetSeed(territoryId);
            var rng = new System.Random(seed);
            return rng.NextDouble() < probability;
        }

        /// <summary>
        /// 영지 ID 기반 시드로 System.Random 인스턴스를 생성합니다.
        /// 내부 메서드에서 여러 번 호출할 때 사용합니다.
        /// </summary>
        public static System.Random CreateRandom(string territoryId)
        {
            if (string.IsNullOrEmpty(territoryId))
                throw new ArgumentException("territoryId는 null 또는 빈 문자열일 수 없습니다.", nameof(territoryId));

            int seed = GetSeed(territoryId);
            return new System.Random(seed);
        }

        /// <summary>
        /// 영지 tier(1~5)에 따른 방 크기를 반환합니다.
        /// 반환값: (width, height, depth)
        /// </summary>
        public static (float width, float height, float depth) GetRoomSize(int tier)
        {
            return tier switch
            {
                1 => (8f, 3f, 6f),     // ⭐ 쉬움
                2 => (10f, 3.3f, 7f),   // ⭐⭐ 보통
                3 => (12f, 3.5f, 8f),   // ⭐⭐⭐ 어려움
                4 => (14f, 3.8f, 9f),   // ⭐⭐⭐⭐ 매우어려움
                5 => (16f, 4f, 10f),    // ⭐⭐⭐⭐⭐ 황제국
                _ => (8f, 3f, 6f)       // 기본: Tier 1
            };
        }

        /// <summary>
        /// tier를 1~5 범위로 클램프합니다.
        /// </summary>
        public static int ClampTier(int tier)
        {
            return Mathf.Clamp(tier, 1, 5);
        }
    }
}