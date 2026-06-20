using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// Biome별 절차적 텍스처 생성기 (64×64)
    /// surfaceColor 기반 + Perlin noise 패턴
    /// </summary>
    public static class TerrainTextureGenerator
    {
        private const int TEXTURE_SIZE = 64;

        /// <summary>
        /// Biome 타입에 맞는 절차적 텍스처 생성
        /// </summary>
        /// <param name="biome">Biome 타입</param>
        /// <returns>64×64 Texture2D</returns>
        public static Texture2D GenerateTexture(BiomeType biome)
        {
            BiomeDefinition def = BiomeData.GetDefinition(biome);
            return GenerateTextureFromDefinition(def, Random.Range(0, 10000));
        }

        /// <summary>
        /// BiomeDefinition 기반 텍스처 생성 (시드 지정 가능)
        /// </summary>
        /// <param name="def">Biome 정의</param>
        /// <param name="seed">노이즈 시드</param>
        /// <returns>64×64 Texture2D</returns>
        public static Texture2D GenerateTextureFromDefinition(BiomeDefinition def, int seed = 0)
        {
            // Empire Biome 특별 처리: Poly Haven 텍스처 로드 시도
            if (def.type == BiomeType.Empire)
            {
                Texture2D loaded = TryLoadEmpireTexture();
                if (loaded != null)
                    return loaded;
                // 실패 시 Procedural 황금 패턴 생성
                return GenerateEmpireProceduralTexture(def, seed);
            }

            Texture2D texture = new Texture2D(TEXTURE_SIZE, TEXTURE_SIZE, TextureFormat.RGBA32, false);
            texture.name = $"Tex_{def.displayName}";
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[TEXTURE_SIZE * TEXTURE_SIZE];
            Color baseColor = def.surfaceColor;

            for (int y = 0; y < TEXTURE_SIZE; y++)
            {
                for (int x = 0; x < TEXTURE_SIZE; x++)
                {
                    // Perlin noise로 텍스처 변형
                    float noiseX = (float)x / TEXTURE_SIZE * 4.0f + seed * 0.01f;
                    float noiseY = (float)y / TEXTURE_SIZE * 4.0f + seed * 0.01f;
                    float noise = Mathf.PerlinNoise(noiseX, noiseY);

                    // 두 번째 octave로 디테일 추가
                    float detailX = (float)x / TEXTURE_SIZE * 8.0f + seed * 0.1f;
                    float detailY = (float)y / TEXTURE_SIZE * 8.0f + seed * 0.1f;
                    float detail = Mathf.PerlinNoise(detailX, detailY) * 0.3f;

                    // noise로 색상 변형 (±15%)
                    float variation = (noise - 0.5f) * 0.3f + detail;
                    Color pixelColor = new Color(
                        Mathf.Clamp01(baseColor.r + variation),
                        Mathf.Clamp01(baseColor.g + variation * 0.8f),
                        Mathf.Clamp01(baseColor.b + variation * 0.6f),
                        1.0f
                    );

                    pixels[y * TEXTURE_SIZE + x] = pixelColor;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return texture;
        }

        /// <summary>
        /// Empire 텍스처 로드 시도 (Poly Haven 다운로드한 파일)
        /// </summary>
        private static Texture2D TryLoadEmpireTexture()
        {
            string[] candidates = new string[]
            {
                "Textures/Empire/empire_stone_col",
                "Textures/Empire/empire_marble_col"
            };

            foreach (string path in candidates)
            {
                Texture2D tex = Resources.Load<Texture2D>(path);
                if (tex != null)
                {
                    Debug.Log($"[TerrainTextureGenerator] Empire 텍스처 로드 성공: {path}");
                    return tex;
                }
            }

            return null;
        }

        /// <summary>
        /// Empire Procedural 텍스처 생성 (회색+황금 패턴)
        /// Resources 로드 실패 시 폴백
        /// </summary>
        private static Texture2D GenerateEmpireProceduralTexture(BiomeDefinition def, int seed)
        {
            Texture2D texture = new Texture2D(TEXTURE_SIZE, TEXTURE_SIZE, TextureFormat.RGBA32, false);
            texture.name = $"Tex_{def.displayName}_Procedural";
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[TEXTURE_SIZE * TEXTURE_SIZE];
            Color stoneGray = new Color(0.5f, 0.5f, 0.55f);
            Color goldAccent = new Color(0.9f, 0.8f, 0.3f);

            for (int y = 0; y < TEXTURE_SIZE; y++)
            {
                for (int x = 0; x < TEXTURE_SIZE; x++)
                {
                    // 돌 블록 패턴 (격자)
                    float blockX = (float)x / TEXTURE_SIZE * 4.0f + seed * 0.01f;
                    float blockY = (float)y / TEXTURE_SIZE * 4.0f + seed * 0.01f;
                    float blockNoise = Mathf.PerlinNoise(blockX, blockY);

                    // 황금 줄무늬 패턴
                    float stripeX = (float)x / TEXTURE_SIZE * 8.0f + seed * 0.1f;
                    float stripeY = (float)y / TEXTURE_SIZE * 8.0f + seed * 0.1f;
                    float stripeNoise = Mathf.PerlinNoise(stripeX, stripeY);

                    // 회색+황금 혼합
                    float goldFactor = Mathf.Clamp01(stripeNoise * 0.6f);
                    Color pixelColor = Color.Lerp(stoneGray, goldAccent, goldFactor);

                    // 돌 질감 변형
                    float variation = (blockNoise - 0.5f) * 0.2f;
                    pixelColor = new Color(
                        Mathf.Clamp01(pixelColor.r + variation),
                        Mathf.Clamp01(pixelColor.g + variation * 0.8f),
                        Mathf.Clamp01(pixelColor.b + variation * 0.6f),
                        1.0f
                    );

                    pixels[y * TEXTURE_SIZE + x] = pixelColor;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}