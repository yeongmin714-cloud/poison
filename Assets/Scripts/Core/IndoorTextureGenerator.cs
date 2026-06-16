using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>
    /// C11-02~04: 실내 맵 텍스처 생성기.
    /// 바닥 타일, 벽 패턴, 나무 판자, 벽돌, 돌 타일 등
    /// 절차적 텍스처를 256x256 RGBA32로 생성.
    /// ProceduralIconGenerator와 유사한 패턴 사용.
    /// </summary>
    public static class IndoorTextureGenerator
    {
        private const int DEFAULT_SIZE = 256;

        // ===================================================================
        // C11-02: 기본 텍스처
        // ===================================================================

        /// <summary>격자 패턴 타일 (primary 배경 + secondary 선)</summary>
        public static Texture2D GenerateFloorTile(int width, int height, Color primary, Color secondary, float tileSize = 0.25f)
        {
            var tex = CreateBaseTexture(width, height);
            int gridPixels = Mathf.RoundToInt(width * tileSize);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int gx = x % gridPixels;
                    int gy = y % gridPixels;
                    bool isLine = gx < 1 || gy < 1 || gx >= gridPixels - 1 || gy >= gridPixels - 1;
                    tex.SetPixel(x, y, isLine ? secondary : primary);
                }
            }

            tex.Apply();
            return tex;
        }

        /// <summary>석재/패널 라인 패턴</summary>
        public static Texture2D GenerateWallPattern(int width, int height, Color primary, Color secondary, bool horizontalLines = true)
        {
            var tex = CreateBaseTexture(width, height);
            int lineSpacing = 32;
            int lineWidth = 2;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool isLine;
                    if (horizontalLines)
                    {
                        isLine = (y % lineSpacing) < lineWidth;
                    }
                    else
                    {
                        isLine = (x % lineSpacing) < lineWidth;
                    }
                    tex.SetPixel(x, y, isLine ? secondary : primary);
                }
            }

            tex.Apply();
            return tex;
        }

        /// <summary>나무 판자 패턴 (세로 줄무늬)</summary>
        public static Texture2D GenerateWoodPlank(int width, int height, Color woodColor, float plankWidth = 0.2f)
        {
            var tex = CreateBaseTexture(width, height);
            int plankPixels = Mathf.RoundToInt(width * plankWidth);
            if (plankPixels < 2) plankPixels = 2;

            Color darkColor = new Color(
                woodColor.r * 0.7f,
                woodColor.g * 0.7f,
                woodColor.b * 0.7f,
                woodColor.a
            );

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int px = x % plankPixels;
                    // 판자 경계선 (어둡게)
                    bool isGap = px < 1;
                    // 나뭇결 노이즈
                    float grain = Mathf.PerlinNoise(x * 0.05f, y * 0.1f) * 0.15f;
                    Color baseColor = isGap ? darkColor : woodColor;
                    Color finalColor = new Color(
                        Mathf.Clamp01(baseColor.r + grain),
                        Mathf.Clamp01(baseColor.g + grain * 0.5f),
                        Mathf.Clamp01(baseColor.b + grain * 0.3f),
                        baseColor.a
                    );
                    tex.SetPixel(x, y, finalColor);
                }
            }

            tex.Apply();
            return tex;
        }

        // ===================================================================
        // C11-03: 바닥 타일 텍스처 추가
        // ===================================================================

        /// <summary>벽돌 패턴 (엇갈림)</summary>
        public static Texture2D GenerateBrickPattern(int width, int height, Color brickColor, Color mortarColor,
            float brickWidth = 0.4f, float brickHeight = 0.15f)
        {
            var tex = CreateBaseTexture(width, height);
            int bw = Mathf.RoundToInt(width * brickWidth);
            int bh = Mathf.RoundToInt(height * brickHeight);
            if (bw < 4) bw = 4;
            if (bh < 3) bh = 3;

            for (int y = 0; y < height; y++)
            {
                int row = y / bh;
                bool offset = (row % 2) == 1;
                int offsetX = offset ? bw / 2 : 0;

                for (int x = 0; x < width; x++)
                {
                    int localX = (x + offsetX) % bw;
                    int localY = y % bh;

                    bool isMortar = localX < 1 || localX >= bw - 1 || localY < 1 || localY >= bh - 1;
                    tex.SetPixel(x, y, isMortar ? mortarColor : brickColor);
                }
            }

            tex.Apply();
            return tex;
        }

        /// <summary>돌 타일 (불규칙 균열)</summary>
        public static Texture2D GenerateStoneTile(int width, int height, Color stoneColor, Color crackColor,
            float crackDensity = 0.05f)
        {
            var tex = CreateBaseTexture(width, height);

            // 기본 돌색 채우기
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    tex.SetPixel(x, y, stoneColor);
                }
            }

            // 불규칙 균열 (Perlin 노이즈 기반)
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float noise = Mathf.PerlinNoise(x * 0.1f, y * 0.1f);
                    float noise2 = Mathf.PerlinNoise(x * 0.05f + 100f, y * 0.05f + 100f);

                    // 균열: 노이즈가 특정 임계값 근처일 때
                    float crackThreshold = 0.5f - crackDensity;
                    bool isCrack = noise > crackThreshold && noise < crackThreshold + crackDensity * 2f;
                    isCrack = isCrack || (noise2 > 0.45f && noise2 < 0.47f);

                    if (isCrack)
                    {
                        tex.SetPixel(x, y, crackColor);
                    }

                    // 표면 텍스처 (미세 변주)
                    float surfaceNoise = (Mathf.PerlinNoise(x * 0.2f, y * 0.2f) - 0.5f) * 0.1f;
                    Color existing = tex.GetPixel(x, y);
                    if (!isCrack && existing.r > 0.01f)
                    {
                        tex.SetPixel(x, y, new Color(
                            Mathf.Clamp01(stoneColor.r + surfaceNoise),
                            Mathf.Clamp01(stoneColor.g + surfaceNoise),
                            Mathf.Clamp01(stoneColor.b + surfaceNoise),
                            stoneColor.a
                        ));
                    }
                }
            }

            tex.Apply();
            return tex;
        }

        // ===================================================================
        // C11-03: 건물 용도별 바닥 프리셋
        // ===================================================================

        /// <summary>상점 바닥 — 따뜻한 갈색 나무 바닥</summary>
        public static Texture2D GenerateShopFloor(int width = DEFAULT_SIZE, int height = DEFAULT_SIZE)
        {
            Color woodColor = new Color(0.55f, 0.35f, 0.15f);
            return GenerateWoodPlank(width, height, woodColor, 0.15f);
        }

        /// <summary>크래프트하우스 바닥 — 회색 돌 타일</summary>
        public static Texture2D GenerateCraftHouseFloor(int width = DEFAULT_SIZE, int height = DEFAULT_SIZE)
        {
            Color stone = new Color(0.45f, 0.45f, 0.45f);
            Color crack = new Color(0.25f, 0.25f, 0.25f);
            return GenerateStoneTile(width, height, stone, crack, 0.06f);
        }

        /// <summary>교회 바닥 — 대리석 흰색 타일</summary>
        public static Texture2D GenerateChurchFloor(int width = DEFAULT_SIZE, int height = DEFAULT_SIZE)
        {
            Color marble = new Color(0.85f, 0.82f, 0.78f);
            Color line = new Color(0.70f, 0.67f, 0.63f);
            return GenerateFloorTile(width, height, marble, line, 0.2f);
        }

        /// <summary>주택 바닥 — 나무 판자 (짙은 갈색)</summary>
        public static Texture2D GenerateHouseFloor(int width = DEFAULT_SIZE, int height = DEFAULT_SIZE)
        {
            Color woodColor = new Color(0.40f, 0.25f, 0.12f);
            return GenerateWoodPlank(width, height, woodColor, 0.18f);
        }

        /// <summary>성 바닥 — 화려한 패턴 타일</summary>
        public static Texture2D GenerateCastleFloor(int width = DEFAULT_SIZE, int height = DEFAULT_SIZE)
        {
            Color primary = new Color(0.60f, 0.40f, 0.20f);
            Color secondary = new Color(0.75f, 0.60f, 0.30f);
            return GenerateFloorTile(width, height, primary, secondary, 0.125f);
        }

        // ===================================================================
        // C11-04: 벽면 패널 텍스처
        // ===================================================================

        /// <summary>돌벽 패턴</summary>
        public static Texture2D GenerateStoneWall(int width, int height, Color stoneColor, Color mortarColor,
            float stoneWidth = 0.15f, float stoneHeight = 0.1f)
        {
            var tex = CreateBaseTexture(width, height);
            int sw = Mathf.Max(4, Mathf.RoundToInt(width * stoneWidth));
            int sh = Mathf.Max(3, Mathf.RoundToInt(height * stoneHeight));

            for (int y = 0; y < height; y++)
            {
                int row = y / sh;
                bool offset = (row % 2) == 1;
                int offsetX = offset ? sw / 2 : 0;

                for (int x = 0; x < width; x++)
                {
                    int localX = (x + offsetX) % sw;
                    int localY = y % sh;

                    bool isMortar = localX < 1 || localX >= sw - 1 || localY < 1 || localY >= sh - 1;

                    // 돌 표면 미세 텍스처
                    float surfaceNoise = (Mathf.PerlinNoise(x * 0.3f, y * 0.3f) - 0.5f) * 0.08f;
                    if (isMortar)
                    {
                        tex.SetPixel(x, y, mortarColor);
                    }
                    else
                    {
                        tex.SetPixel(x, y, new Color(
                            Mathf.Clamp01(stoneColor.r + surfaceNoise),
                            Mathf.Clamp01(stoneColor.g + surfaceNoise),
                            Mathf.Clamp01(stoneColor.b + surfaceNoise),
                            stoneColor.a
                        ));
                    }
                }
            }

            tex.Apply();
            return tex;
        }

        /// <summary>나무 패널 벽</summary>
        public static Texture2D GenerateWoodPanel(int width, int height, Color panelColor, Color lineColor,
            float panelWidth = 0.2f, float panelHeight = 0.3f)
        {
            var tex = CreateBaseTexture(width, height);
            int pw = Mathf.Max(4, Mathf.RoundToInt(width * panelWidth));
            int ph = Mathf.Max(4, Mathf.RoundToInt(height * panelHeight));

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int px = x % pw;
                    int py = y % ph;

                    // 패널 테두리
                    bool isEdge = px < 2 || px >= pw - 2 || py < 2 || py >= ph - 2;

                    if (isEdge)
                    {
                        tex.SetPixel(x, y, lineColor);
                    }
                    else
                    {
                        // 패널 내부 나뭇결
                        float grain = Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 0.12f;
                        tex.SetPixel(x, y, new Color(
                            Mathf.Clamp01(panelColor.r + grain),
                            Mathf.Clamp01(panelColor.g + grain * 0.5f),
                            Mathf.Clamp01(panelColor.b + grain * 0.3f),
                            panelColor.a
                        ));
                    }
                }
            }

            tex.Apply();
            return tex;
        }

        /// <summary>회반죽 벽 (미세 텍스처)</summary>
        public static Texture2D GeneratePlasterWall(int width, int height, Color plasterColor)
        {
            var tex = CreateBaseTexture(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // 미세한 요철감
                    float noise = (Mathf.PerlinNoise(x * 0.15f, y * 0.15f) - 0.5f) * 0.12f;
                    float noise2 = (Mathf.PerlinNoise(x * 0.3f + 50f, y * 0.3f + 50f) - 0.5f) * 0.06f;

                    tex.SetPixel(x, y, new Color(
                        Mathf.Clamp01(plasterColor.r + noise + noise2),
                        Mathf.Clamp01(plasterColor.g + noise + noise2),
                        Mathf.Clamp01(plasterColor.b + noise + noise2),
                        plasterColor.a
                    ));
                }
            }

            tex.Apply();
            return tex;
        }

        // ===================================================================
        // C11-04: 건물 용도별 벽면 프리셋
        // ===================================================================

        /// <summary>상점 벽 — 회반죽 (따뜻한 크림색)</summary>
        public static Texture2D GenerateShopWall(int width = DEFAULT_SIZE, int height = DEFAULT_SIZE)
        {
            Color plaster = new Color(0.85f, 0.78f, 0.65f); // 따뜻한 크림
            return GeneratePlasterWall(width, height, plaster);
        }

        /// <summary>크래프트하우스 벽 — 돌벽 (회색)</summary>
        public static Texture2D GenerateCraftHouseWall(int width = DEFAULT_SIZE, int height = DEFAULT_SIZE)
        {
            Color stone = new Color(0.50f, 0.45f, 0.40f);
            Color mortar = new Color(0.35f, 0.30f, 0.25f);
            return GenerateStoneWall(width, height, stone, mortar, 0.2f, 0.12f);
        }

        /// <summary>교회 벽 — 나무 패널 (밝은 나무)</summary>
        public static Texture2D GenerateChurchWall(int width = DEFAULT_SIZE, int height = DEFAULT_SIZE)
        {
            Color panel = new Color(0.70f, 0.60f, 0.45f);
            Color line = new Color(0.50f, 0.40f, 0.30f);
            return GenerateWoodPanel(width, height, panel, line, 0.25f, 0.35f);
        }

        /// <summary>주택 벽 — 회반죽 (밝은 베이지)</summary>
        public static Texture2D GenerateHouseWall(int width = DEFAULT_SIZE, int height = DEFAULT_SIZE)
        {
            Color plaster = new Color(0.80f, 0.75f, 0.65f);
            return GeneratePlasterWall(width, height, plaster);
        }

        /// <summary>성 벽 — 돌벽 (짙은 회색)</summary>
        public static Texture2D GenerateCastleWall(int width = DEFAULT_SIZE, int height = DEFAULT_SIZE)
        {
            Color stone = new Color(0.35f, 0.32f, 0.30f);
            Color mortar = new Color(0.20f, 0.18f, 0.15f);
            return GenerateStoneWall(width, height, stone, mortar, 0.18f, 0.10f);
        }

        // ===================================================================
        // C11-13: 국가별 성 바닥/벽 프리셋
        // ===================================================================

        /// <summary>동부국가 성 바닥 — 붉은 벽돌 + 황금 장식</summary>
        public static Texture2D GenerateCastleFloorEastern(int width = DEFAULT_SIZE, int height = DEFAULT_SIZE)
        {
            Color primary = new Color(0.65f, 0.20f, 0.15f); // 붉은 벽돌
            Color secondary = new Color(0.85f, 0.70f, 0.15f); // 황금 장식
            return GenerateFloorTile(width, height, primary, secondary, 0.15f);
        }

        /// <summary>서부국가 성 바닥 — 회색 석재 + 파란 장식</summary>
        public static Texture2D GenerateCastleFloorWestern(int width = DEFAULT_SIZE, int height = DEFAULT_SIZE)
        {
            Color primary = new Color(0.50f, 0.50f, 0.52f); // 회색 석재
            Color secondary = new Color(0.20f, 0.30f, 0.70f); // 파란 장식
            return GenerateFloorTile(width, height, primary, secondary, 0.15f);
        }

        /// <summary>남부국가 성 바닥 — 사암 + 녹색 장식</summary>
        public static Texture2D GenerateCastleFloorSouthern(int width = DEFAULT_SIZE, int height = DEFAULT_SIZE)
        {
            Color primary = new Color(0.76f, 0.65f, 0.42f); // 사암
            Color secondary = new Color(0.20f, 0.55f, 0.15f); // 녹색 식물
            return GenerateFloorTile(width, height, primary, secondary, 0.15f);
        }

        /// <summary>북부국가 성 바닥 — 어두운 돌 + 모피 장식</summary>
        public static Texture2D GenerateCastleFloorNorthern(int width = DEFAULT_SIZE, int height = DEFAULT_SIZE)
        {
            Color primary = new Color(0.25f, 0.22f, 0.20f); // 어두운 돌
            Color secondary = new Color(0.55f, 0.40f, 0.25f); // 모피 갈색
            return GenerateFloorTile(width, height, primary, secondary, 0.15f);
        }

        /// <summary>황제국 성 바닥 — 대리석 + 자주색 장식</summary>
        public static Texture2D GenerateCastleFloorEmpire(int width = DEFAULT_SIZE, int height = DEFAULT_SIZE)
        {
            Color primary = new Color(0.85f, 0.82f, 0.78f); // 대리석
            Color secondary = new Color(0.55f, 0.20f, 0.55f); // 자주색
            return GenerateFloorTile(width, height, primary, secondary, 0.125f);
        }

        /// <summary>동부국가 성 벽 — 붉은 벽돌 + 황금 장식</summary>
        public static Texture2D GenerateCastleWallEastern(int width = DEFAULT_SIZE, int height = DEFAULT_SIZE)
        {
            Color stone = new Color(0.60f, 0.22f, 0.18f);
            Color mortar = new Color(0.80f, 0.65f, 0.10f);
            return GenerateStoneWall(width, height, stone, mortar, 0.18f, 0.10f);
        }

        /// <summary>서부국가 성 벽 — 회색 석재 + 파란 태피스트리</summary>
        public static Texture2D GenerateCastleWallWestern(int width = DEFAULT_SIZE, int height = DEFAULT_SIZE)
        {
            Color primary = new Color(0.45f, 0.45f, 0.48f);
            Color secondary = new Color(0.20f, 0.25f, 0.55f);
            return GenerateWallPattern(width, height, primary, secondary, true);
        }

        /// <summary>남부국가 성 벽 — 사암 + 녹색 식물</summary>
        public static Texture2D GenerateCastleWallSouthern(int width = DEFAULT_SIZE, int height = DEFAULT_SIZE)
        {
            Color stone = new Color(0.72f, 0.62f, 0.40f);
            Color mortar = new Color(0.25f, 0.50f, 0.20f);
            return GenerateStoneWall(width, height, stone, mortar, 0.18f, 0.10f);
        }

        /// <summary>북부국가 성 벽 — 어두운 돌 + 모피</summary>
        public static Texture2D GenerateCastleWallNorthern(int width = DEFAULT_SIZE, int height = DEFAULT_SIZE)
        {
            Color stone = new Color(0.28f, 0.25f, 0.22f);
            Color mortar = new Color(0.50f, 0.35f, 0.20f);
            return GenerateStoneWall(width, height, stone, mortar, 0.20f, 0.12f);
        }

        /// <summary>황제국 성 벽 — 대리석 + 자주색</summary>
        public static Texture2D GenerateCastleWallEmpire(int width = DEFAULT_SIZE, int height = DEFAULT_SIZE)
        {
            Color primary = new Color(0.80f, 0.78f, 0.75f);
            Color secondary = new Color(0.50f, 0.18f, 0.50f);
            return GenerateWallPattern(width, height, primary, secondary, false);
        }

        // ===================================================================
        // 유틸
        // ===================================================================

        private static Texture2D CreateBaseTexture(int width, int height)
        {
            if (width <= 0) width = DEFAULT_SIZE;
            if (height <= 0) height = DEFAULT_SIZE;
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
        }
    }
}
