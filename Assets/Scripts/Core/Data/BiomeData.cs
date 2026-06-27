using UnityEngine;

namespace ProjectName.Core.Data
{
    /// <summary>
    /// 생물군계(Biome) 열거형 — 11종
    /// </summary>
    public enum BiomeType
    {
        Plains = 0,       // 초원 — 동(East)
        Forest = 1,       // 숲 — 동(East)
        Lake = 2,         // 호수 — 동/서
        Rocky = 3,        // 바위 — 서(West)
        Swamp = 4,        // 늪 — 서(West)
        Desert = 5,       // 사막 — 남(South)
        Volcanic = 6,     // 화산 — 남(South)
        Tundra = 7,       // 툰드라 — 북(North)
        Mountain = 8,     // 산악 — 북(North)
        Empire = 9,       // 황제국
        Reed = 10         // 노란 갈대 — 서(West)
    }

    /// <summary>
    /// Biome별 상세 정의 — 지형 생성 파라미터 포함
    /// </summary>
    [System.Serializable]
    public struct BiomeDefinition
    {
        public BiomeType type;
        public string displayName;
        public Color surfaceColor;          // 지형 색상
        public float noiseAmplitude;        // Perlin noise 진폭
        public float noiseFrequency;        // Perlin noise 주파수
        public float waterThreshold;        // 0이면 물 없음
        public Color waterColor;
        public float moveSpeedModifier;     // 이동 속도 배율 (1.0=기본)
    }

    /// <summary>
    /// Biome 데이터 저장소 — 정의 조회 및 영지→Biome 매핑
    /// </summary>
    public static class BiomeData
    {
        private static BiomeDefinition[] _definitions;

        private static void EnsureInitialized()
        {
            if (_definitions != null)
                return;

            int count = System.Enum.GetValues(typeof(BiomeType)).Length;
            _definitions = new BiomeDefinition[count];

            _definitions[(int)BiomeType.Plains] = new BiomeDefinition
            {
                type = BiomeType.Plains,
                displayName = "초원",
                surfaceColor = new Color(0.3f, 0.7f, 0.3f),
                noiseAmplitude = 0.5f,
                noiseFrequency = 3.0f,
                waterThreshold = 0f,
                waterColor = Color.clear,
                moveSpeedModifier = 1.0f
            };

            _definitions[(int)BiomeType.Forest] = new BiomeDefinition
            {
                type = BiomeType.Forest,
                displayName = "숲",
                surfaceColor = new Color(0.2f, 0.5f, 0.2f),
                noiseAmplitude = 1.0f,
                noiseFrequency = 4.0f,
                waterThreshold = 0f,
                waterColor = Color.clear,
                moveSpeedModifier = 0.8f
            };

            _definitions[(int)BiomeType.Lake] = new BiomeDefinition
            {
                type = BiomeType.Lake,
                displayName = "호수",
                surfaceColor = new Color(0.4f, 0.3f, 0.2f),
                noiseAmplitude = 0.3f,
                noiseFrequency = 2.0f,
                waterThreshold = 0.4f,
                waterColor = new Color(0.2f, 0.4f, 0.8f, 0.8f),
                moveSpeedModifier = 0.6f
            };

            _definitions[(int)BiomeType.Rocky] = new BiomeDefinition
            {
                type = BiomeType.Rocky,
                displayName = "바위",
                surfaceColor = new Color(0.5f, 0.5f, 0.5f),
                noiseAmplitude = 2.0f,
                noiseFrequency = 3.5f,
                waterThreshold = 0f,
                waterColor = Color.clear,
                moveSpeedModifier = 0.7f
            };

            _definitions[(int)BiomeType.Swamp] = new BiomeDefinition
            {
                type = BiomeType.Swamp,
                displayName = "늪",
                surfaceColor = new Color(0.3f, 0.2f, 0.1f),
                noiseAmplitude = 0.5f,
                noiseFrequency = 2.5f,
                waterThreshold = 0.3f,
                waterColor = new Color(0.1f, 0.3f, 0.1f, 0.7f),
                moveSpeedModifier = 0.5f
            };

            _definitions[(int)BiomeType.Desert] = new BiomeDefinition
            {
                type = BiomeType.Desert,
                displayName = "사막",
                surfaceColor = new Color(0.8f, 0.7f, 0.4f),
                noiseAmplitude = 1.0f,
                noiseFrequency = 2.0f,
                waterThreshold = 0f,
                waterColor = Color.clear,
                moveSpeedModifier = 0.9f
            };

            _definitions[(int)BiomeType.Volcanic] = new BiomeDefinition
            {
                type = BiomeType.Volcanic,
                displayName = "화산",
                surfaceColor = new Color(0.5f, 0.1f, 0.1f),
                noiseAmplitude = 2.5f,
                noiseFrequency = 3.0f,
                waterThreshold = 0f,
                waterColor = Color.clear,
                moveSpeedModifier = 0.6f
            };

            _definitions[(int)BiomeType.Tundra] = new BiomeDefinition
            {
                type = BiomeType.Tundra,
                displayName = "툰드라",
                surfaceColor = new Color(0.9f, 0.9f, 0.9f),
                noiseAmplitude = 0.5f,
                noiseFrequency = 2.0f,
                waterThreshold = 0f,
                waterColor = Color.clear,
                moveSpeedModifier = 0.8f
            };

            _definitions[(int)BiomeType.Mountain] = new BiomeDefinition
            {
                type = BiomeType.Mountain,
                displayName = "산악",
                surfaceColor = new Color(0.6f, 0.6f, 0.6f),
                noiseAmplitude = 4.0f,
                noiseFrequency = 1.5f,
                waterThreshold = 0f,
                waterColor = Color.clear,
                moveSpeedModifier = 0.4f
            };

            _definitions[(int)BiomeType.Empire] = new BiomeDefinition
            {
                type = BiomeType.Empire,
                displayName = "황제국",
                surfaceColor = new Color(0.9f, 0.8f, 0.3f),
                noiseAmplitude = 0.3f,
                noiseFrequency = 1.0f,
                waterThreshold = 0f,
                waterColor = Color.clear,
                moveSpeedModifier = 1.2f
            };

            _definitions[(int)BiomeType.Reed] = new BiomeDefinition
            {
                type = BiomeType.Reed,
                displayName = "노란 갈대밭",
                surfaceColor = new Color(0.85f, 0.75f, 0.35f),
                noiseAmplitude = 0.8f,
                noiseFrequency = 3.0f,
                waterThreshold = 0f,
                waterColor = Color.clear,
                moveSpeedModifier = 0.8f
            };
        }

        /// <summary>
        /// BiomeType에 맞는 BiomeDefinition 반환
        /// </summary>
        public static BiomeDefinition GetDefinition(BiomeType type)
        {
            EnsureInitialized();
            int index = (int)type;
            if (index < 0 || index >= _definitions.Length)
            {
                Debug.LogWarning($"[BiomeData] 유효하지 않은 BiomeType: {type}");
                return _definitions[(int)BiomeType.Plains];
            }
            return _definitions[index];
        }

        /// <summary>
        /// 국가(NationType)와 영지 인덱스에 따라 Biome 결정
        /// 국가별 확률 분포:
        ///   East:   70% Plains, 30% Forest
        ///   West:   50% Reed, 30% Rocky, 20% Swamp
        ///   South:  60% Desert, 30% Volcanic, 10% Plains
        ///   North:  60% Tundra, 30% Mountain, 10% Plains
        ///   Empire: 100% Empire
        /// </summary>
        public static BiomeType GetBiomeForTerritory(NationType nation, int index)
        {
            // 결정론적 시드: (nation * 100 + index)
            int hash = ((int)nation * 100 + index);
            System.Random rng = new System.Random(hash);
            float roll = (float)rng.NextDouble();

            switch (nation)
            {
                case NationType.East:
                    if (roll < 0.70f) return BiomeType.Plains;
                    return BiomeType.Forest;

                case NationType.West:
                    if (roll < 0.50f) return BiomeType.Reed;
                    if (roll < 0.80f) return BiomeType.Rocky;
                    return BiomeType.Swamp;

                case NationType.South:
                    if (roll < 0.60f) return BiomeType.Desert;
                    if (roll < 0.90f) return BiomeType.Volcanic;
                    return BiomeType.Plains;

                case NationType.North:
                    if (roll < 0.60f) return BiomeType.Tundra;
                    if (roll < 0.90f) return BiomeType.Mountain;
                    return BiomeType.Plains;

                case NationType.Empire:
                    return BiomeType.Empire;

                default:
                    Debug.LogWarning($"[BiomeData] 알 수 없는 국가: {nation}, 기본값 Plains 반환");
                    return BiomeType.Plains;
            }
        }
    }
}