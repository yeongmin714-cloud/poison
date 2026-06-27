using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// 국가(NationType)와 영지 인덱스 → BiomeType 매핑
    /// 결정론적 랜덤으로 국가별 확률 분포에 따라 Biome 결정
    /// </summary>
    public static class TerritoryBiomeMapper
    {
        /// <summary>
        /// TerritoryDatabase의 NationType + index → BiomeType
        /// </summary>
        /// <param name="nation">소속 국가</param>
        /// <param name="index">영지 인덱스 (1~20)</param>
        /// <returns>해당 영지의 BiomeType</returns>
        public static BiomeType GetBiome(NationType nation, int index)
        {
            // index 범위 검증 (1~20)
            if (index < 1 || index > 20)
            {
                UnityEngine.Debug.LogWarning($"[TerritoryBiomeMapper] 영지 인덱스 범위 초과: {index} (허용: 1~20), 1로 보정");
                index = UnityEngine.Mathf.Clamp(index, 1, 20);
            }

            // None 국가 조기 반환
            if (nation == NationType.None)
            {
                UnityEngine.Debug.LogWarning("[TerritoryBiomeMapper] NationType.None 입력, 기본값 Plains 반환");
                return BiomeType.Plains;
            }

            // 결정론적 시드: (nation * 100 + index)
            int hash = ((int)nation * 100 + index);
            System.Random rng = new System.Random(hash);
            float roll = (float)rng.NextDouble();

            switch (nation)
            {
                case NationType.East:
                    // East: 70% Plains, 30% Forest
                    if (roll < 0.70f) return BiomeType.Plains;
                    return BiomeType.Forest;

                case NationType.West:
                    // West: 50% Reed, 30% Rocky, 20% Swamp
                    if (roll < 0.50f) return BiomeType.Reed;
                    if (roll < 0.80f) return BiomeType.Rocky;
                    return BiomeType.Swamp;

                case NationType.South:
                    // South: 60% Desert, 30% Volcanic, 10% Plains
                    if (roll < 0.60f) return BiomeType.Desert;
                    if (roll < 0.90f) return BiomeType.Volcanic;
                    return BiomeType.Plains;

                case NationType.North:
                    // North: 60% Tundra, 30% Mountain, 10% Plains
                    if (roll < 0.60f) return BiomeType.Tundra;
                    if (roll < 0.90f) return BiomeType.Mountain;
                    return BiomeType.Plains;

                case NationType.Empire:
                    // Empire: 100% Empire
                    return BiomeType.Empire;

                case NationType.Dracula:
                    // Dracula: 70% Volcanic, 30% Swamp
                    if (roll < 0.70f) return BiomeType.Volcanic;
                    return BiomeType.Swamp;

                default:
                    UnityEngine.Debug.LogWarning($"[TerritoryBiomeMapper] 알 수 없는 국가: {nation}, 기본값 Plains 반환");
                    return BiomeType.Plains;
            }
        }
    }
}