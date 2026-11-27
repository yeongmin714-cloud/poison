using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Core
{
    /// <summary>
    /// C11-02~04: 실내 맵 텍스처 생성기.
    /// 바닥 타일, 벽 패턴, 나무 판자, 벽돌, 돌 타일 등
    /// 절차적 텍스처를 256x256 RGBA32로 생성.
    /// ProceduralIconGenerator와 유사한 패턴 사용.
    ///
    /// C10-07: 실내 텍스처 차별화 — 외부 지형 텍스처와 동일한 경우 색상을 조정합니다.
    /// </summary>
    public static class IndoorTextureGenerator
    {
        // [바닥/벽 품질 v2] 256→512 — 줄눈/결/얼룩 판독력 상향(9/20 예시 품질바). 절차 생성 — 임포트 비용 0.
        private const int DEFAULT_SIZE = 512;

        // ===================================================================
        // C11-02: 기본 텍스처
        // ===================================================================

        /// <summary>격자 패턴 타일 (primary 배경 + secondary 선)</summary>
        public static Texture2D GenerateFloorTile(int width, int height, Color primary, Color secondary, float tileSize = 0.25f)
        {
            // [바닥 품질 v2] 타일별 톤 편차(해시 — 결정론) + 미세 스펙클 + 줄눈 측 마모 어두움.
            var tex = CreateBaseTexture(width, height);
            int gridPixels = Mathf.Max(8, Mathf.RoundToInt(width * tileSize));
            int grout = Mathf.Max(2, gridPixels / 32);   // 해상도 비례 줄눈(512에서 ~4px)

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int gx = x % gridPixels;
                    int gy = y % gridPixels;
                    bool isLine = gx < grout || gy < grout || gx >= gridPixels - grout || gy >= gridPixels - grout;
                    Color c;
                    if (isLine)
                    {
                        c = secondary;
                    }
                    else
                    {
                        int tileH = StableTileHash(x / gridPixels, y / gridPixels);
                        float variance = ((tileH & 0x3F) / 63f - 0.5f) * 0.08f;   // 타일별 ±4% 톤
                        int px = (x * 7 + y * 13) & 0x1F;
                        float speckle = (px / 31f - 0.5f) * 0.035f;
                        c = primary + new Color(variance + speckle, variance + speckle, variance * 0.8f + speckle, 0f);
                        // 줄눈 측 마모 어두움(에지 8% 밴드)
                        int edge = Mathf.Min(Mathf.Min(gx, gy), Mathf.Min(gridPixels - 1 - gx, gridPixels - 1 - gy));
                        int wearBand = Mathf.Max(2, gridPixels / 16);
                        if (edge < wearBand)
                        {
                            float wear = 1f - (float)edge / wearBand;
                            c -= new Color(wear * 0.05f, wear * 0.05f, wear * 0.05f, 0f);
                        }
                    }
                    tex.SetPixel(x, y, new Color(
                        Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), c.a));
                }
            }

            tex.Apply();
            return tex;
        }

        /// <summary>타일 좌표 결정론 해시 — 엔진 난수 미사용(FNV-1a 변형).</summary>
        private static int StableTileHash(int tx, int ty)
        {
            uint h = 2166136261u;
            h = (h ^ (uint)tx) * 16777619u;
            h = (h ^ (uint)ty) * 16777619u;
            return (int)h;
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
            int gap = Mathf.Max(2, plankPixels / 24);   // 해상도 비례 틈(512에서 ~4px)

            Color darkColor = new Color(
                woodColor.r * 0.62f,
                woodColor.g * 0.62f,
                woodColor.b * 0.62f,
                woodColor.a
            );
            // 판자별 톤 편차 — 판 인덱스 해시(결정론)
            int plankCount = Mathf.Max(1, width / plankPixels);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int px = x % plankPixels;
                    bool isGap = px < gap;
                    // 나뭇결 — 두 주파수 중첩(세로 결 방향 강조)
                    float grain = Mathf.PerlinNoise(x * 0.05f, y * 0.1f) * 0.12f
                                + Mathf.PerlinNoise(x * 0.01f, y * 0.03f) * 0.06f;
                    Color baseColor = isGap ? darkColor : woodColor;
                    if (!isGap)
                    {
                        int plank = x / plankPixels;
                        int plankH = StableTileHash(plank, y / Mathf.Max(1, height / 2));
                        float variance = ((plankH & 0x1F) / 31f - 0.5f) * 0.10f;   // 판별 ±5%
                        baseColor = baseColor + new Color(variance, variance, variance * 0.8f, 0f);
                        // 매듭 — 낮은 빈도 어두운 타원(해시 그리드)
                        int knotCell = 64;
                        int kx = StableTileHash(x / knotCell, y / knotCell);
                        if ((kx & 0x0F) == 0)
                        {
                            float dx = (x % knotCell) - knotCell * 0.5f;
                            float dy = (y % knotCell) - ((kx >> 4) % knotCell);
                            if (dx * dx + dy * dy * 4f < 40f)
                                baseColor = baseColor * 0.72f;
                        }
                    }
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

                    bool isMortar = localX < 2 || localX >= bw - 2 || localY < 2 || localY >= bh - 2;
                    if (isMortar)
                    {
                        // 모르타르 깊이감 — 벽돌 에지에서 살짝 어둡게(그림자 오목)
                        tex.SetPixel(x, y, mortarColor * 0.9f);
                    }
                    else
                    {
                        // 벽돌별 톤 편차(행+열 해시 — 결정론) + 표면 미세 노이즈
                        int bx = (x + offsetX) / bw;
                        int by = y / bh;
                        int bh2 = StableTileHash(bx, by);
                        float variance = ((bh2 & 0x3F) / 63f - 0.5f) * 0.12f;
                        float surface = (Mathf.PerlinNoise(x * 0.25f, y * 0.25f) - 0.5f) * 0.06f;
                        Color c = brickColor + new Color(variance + surface, variance + surface, variance * 0.7f + surface, 0f);
                        tex.SetPixel(x, y, new Color(
                            Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), c.a));
                    }
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

                    // 표면 텍스처 (미세 변주) + 저주파 얼룩 패치
                    float surfaceNoise = (Mathf.PerlinNoise(x * 0.2f, y * 0.2f) - 0.5f) * 0.1f;
                    float blotch = (Mathf.PerlinNoise(x * 0.03f + 13f, y * 0.03f + 13f) - 0.5f) * 0.08f;
                    Color existing = tex.GetPixel(x, y);
                    if (!isCrack && existing.r > 0.01f)
                    {
                        tex.SetPixel(x, y, new Color(
                            Mathf.Clamp01(stoneColor.r + surfaceNoise + blotch),
                            Mathf.Clamp01(stoneColor.g + surfaceNoise + blotch),
                            Mathf.Clamp01(stoneColor.b + surfaceNoise + blotch * 0.7f),
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

        /// <summary>
        /// [125차 후속24 — A1] 모래 텍스처 — 격자 없는 미세 입자 노이즈(결정론 해시).
        /// 결투장 모래 계약: GenerateFloorTile 격자 패턴은 실내 타일 재질이라 실외 모래 부적합 → 대체.
        /// 밝은 톤(125차 후속18 계승) + 어두운 입자 밀도 + 저주파 패치(밟아 다진 자국 띠).
        /// </summary>
        public static Texture2D GenerateSandTexture(int width = DEFAULT_SIZE, int height = DEFAULT_SIZE)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.name = "ArenaSandTex";
            Color baseCol = new Color(0.88f, 0.80f, 0.62f);   // 후속18 밝은 모래 톤 계승
            Color grain = new Color(0.78f, 0.69f, 0.50f);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // 결정론 해시 노이즈 — 시드 불변(빌더 결정론 로그 규약과 동일)
                    int h = (x * 73856093) ^ (y * 19349663);
                    float n1 = ((h >> 7) & 0xFF) / 255f;
                    float n2 = (((h >> 15) & 0xFF) + ((h >> 23) & 0x1F)) / 287f;
                    // 저주파 패치 — 밟아 다진/흩어진 모래 띠(두 사인 혼합)
                    float patch = Mathf.Sin((x + y * 0.7f) * 0.05f) * 0.5f
                                + Mathf.Sin((x * 0.3f - y) * 0.04f) * 0.5f;
                    float mix = Mathf.Clamp01(0.55f + n1 * 0.35f + patch * 0.18f);
                    tex.SetPixel(x, y, Color.Lerp(baseCol, grain, mix * 0.85f + n2 * 0.15f));
                }
            }
            tex.Apply();
            return tex;
        }

        /// <summary>[125차 후속24 — A1] 모래 높이장 — 색/노멀 양쪽이 공유하는 결정론 함수(질감 일관성).</summary>
        private static float SandHeight(int x, int y)
        {
            int hash = (x * 73856093) ^ (y * 19349663);
            float n1 = ((hash >> 7) & 0xFF) / 255f;
            float patch = Mathf.Sin((x + y * 0.7f) * 0.05f) * 0.5f
                        + Mathf.Sin((x * 0.3f - y) * 0.04f) * 0.5f;
            return n1 * 0.35f + patch * 0.18f;
        }

        /// <summary>
        /// [125차 후속24 — A1] 모래 노멀맵 — SandHeight 중심차분에서 탄젠트 공간 노멀 생성(결정론).
        /// **AG 스위즐 계약**: 런타임 생성 노멀은 임포터 스위즐이 없으므로 URP UnpackNormalmapRGorAG
        /// 데스크톱 규약(x=알파, y=그린, z=재구성)에 맞춰 기록 — 리니어 텍스처 필수.
        /// </summary>
        public static Texture2D GenerateSandNormalTexture(int width = DEFAULT_SIZE, int height = DEFAULT_SIZE)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, linear: true);
            tex.name = "ArenaSandNormalTex";
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float hx = SandHeight((x + 1) % width, y) - SandHeight((x - 1 + width) % width, y);
                    float hy = SandHeight(x, (y + 1) % height) - SandHeight(x, (y - 1 + height) % height);
                    Vector3 n = new Vector3(-hx * 2.2f, -hy * 2.2f, 1f).normalized;
                    tex.SetPixel(x, y, new Color(1f, n.y * 0.5f + 0.5f, 0f, n.x * 0.5f + 0.5f));
                }
            }
            tex.Apply();
            return tex;
        }

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

                    // 돌 표면 미세 텍스처 + 돌별 톤 편차(해시 — 결정론)
                    float surfaceNoise = (Mathf.PerlinNoise(x * 0.3f, y * 0.3f) - 0.5f) * 0.08f;
                    if (isMortar)
                    {
                        tex.SetPixel(x, y, mortarColor * 0.92f);
                    }
                    else
                    {
                        int sx = (x + offsetX) / sw;
                        int sy = y / sh;
                        int sH = StableTileHash(sx, sy);
                        float variance = ((sH & 0x3F) / 63f - 0.5f) * 0.10f;
                        // 하부 그라데이션 — 벽 하단 25% 살짝 어둡게(실측 사진 투톤 느낌)
                        float bottom = y < height * 0.25f ? -(1f - y / (height * 0.25f)) * 0.05f : 0f;
                        tex.SetPixel(x, y, new Color(
                            Mathf.Clamp01(stoneColor.r + surfaceNoise + variance + bottom),
                            Mathf.Clamp01(stoneColor.g + surfaceNoise + variance + bottom),
                            Mathf.Clamp01(stoneColor.b + surfaceNoise + variance * 0.7f + bottom),
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
                    // 미세한 요철감 + 저주파 얼룩(수분/그을음 흔적 — 결정론 펄린)
                    float noise = (Mathf.PerlinNoise(x * 0.15f, y * 0.15f) - 0.5f) * 0.12f;
                    float noise2 = (Mathf.PerlinNoise(x * 0.3f + 50f, y * 0.3f + 50f) - 0.5f) * 0.06f;
                    float stain = (Mathf.PerlinNoise(x * 0.02f + 7f, y * 0.02f + 7f) - 0.5f) * 0.09f;
                    float stain2 = Mathf.Max(0f, Mathf.PerlinNoise(x * 0.05f + 31f, y * 0.05f + 31f) - 0.62f) * 0.5f;

                    tex.SetPixel(x, y, new Color(
                        Mathf.Clamp01(plasterColor.r + noise + noise2 + stain - stain2),
                        Mathf.Clamp01(plasterColor.g + noise + noise2 + stain - stain2 * 0.8f),
                        Mathf.Clamp01(plasterColor.b + noise + noise2 + stain * 0.8f - stain2 * 0.6f),
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
        // C10-07: 외부 지형 텍스처 차별화
        // ===================================================================

        /// <summary>
        /// 외부 지형 텍스처와 색상이 동일한지 확인합니다.
        /// 실내 텍스처가 외부 지형과 유사한 경우(색상 차이가 0.05 미만), 색상을 변주하여
        /// 실내/외 구분이 명확하도록 합니다.
        /// </summary>
        /// <param name="indoorColor">실내 텍스처 기본 색상</param>
        /// <param name="exteriorColor">외부 지형 색상 (기본값: 녹색 계열 잔디)</param>
        /// <param name="minDifference">최소 색상 차이 임계값 (기본값: 0.05)</param>
        /// <returns>조정된 실내 색상 (차이가 충분하면 원본 유지)</returns>
        public static Color EnsureDifferentFromExterior(Color indoorColor, Color exteriorColor, float minDifference = 0.05f)
        {
            float diff = Mathf.Abs(indoorColor.r - exteriorColor.r)
                       + Mathf.Abs(indoorColor.g - exteriorColor.g)
                       + Mathf.Abs(indoorColor.b - exteriorColor.b);

            if (diff < minDifference * 3f)
            {
                // 외부 지형과 유사하면 색상을 변주
                return new Color(
                    Mathf.Clamp01(indoorColor.r + (indoorColor.r > 0.5f ? -0.15f : 0.15f)),
                    Mathf.Clamp01(indoorColor.g + (indoorColor.g > 0.5f ? -0.15f : 0.15f)),
                    Mathf.Clamp01(indoorColor.b + (indoorColor.b > 0.5f ? -0.10f : 0.10f)),
                    indoorColor.a
                );
            }

            return indoorColor;
        }

        /// <summary>
        /// 외부 지형 텍스처 기준 색상과 실내 텍스처가 동일한지 확인하고,
        /// 필요한 경우 텍스처를 자동 조정합니다.
        /// </summary>
        /// <param name="texture">확인할 실내 텍스처</param>
        /// <param name="exteriorGroundColor">외부 지형 색상 (기본값: 잔디색 (0.3, 0.6, 0.2))</param>
        /// <returns>조정된 텍스처 (변경 불필요 시 원본 반환)</returns>
        public static Texture2D DifferentiateFromExterior(Texture2D texture, Color? exteriorGroundColor = null)
        {
            if (texture == null) return null;

            Color extColor = exteriorGroundColor ?? new Color(0.3f, 0.6f, 0.2f); // 기본 잔디색

            // 텍스처 중앙 픽셀 샘플링
            Color sample = texture.GetPixel(texture.width / 2, texture.height / 2);

            // 이미 차이가 있으면 원본 반환
            float diff = Mathf.Abs(sample.r - extColor.r)
                       + Mathf.Abs(sample.g - extColor.g)
                       + Mathf.Abs(sample.b - extColor.b);

            if (diff > 0.15f)
                return texture;

            // 색상 변주가 필요하면 텍스처 전체 톤 시프트
            Color shift = new Color(
                sample.r > 0.5f ? -0.1f : 0.1f,
                sample.g > 0.5f ? -0.12f : 0.12f,
                sample.b > 0.5f ? -0.08f : 0.08f,
                0f
            );

            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    Color pixel = texture.GetPixel(x, y);
                    texture.SetPixel(x, y, new Color(
                        Mathf.Clamp01(pixel.r + shift.r),
                        Mathf.Clamp01(pixel.g + shift.g),
                        Mathf.Clamp01(pixel.b + shift.b),
                        pixel.a
                    ));
                }
            }

            texture.Apply();
            Debug.Log($"[IndoorTextureGenerator] 텍스처 차별화 적용 — 외부 지형색과 유사하여 톤 시프트 수행");
            return texture;
        }

        /// <summary>
        /// 빌더에서 생성된 텍스처가 외부 지형과 충분히 다른지 검증하고
        /// 필요한 경우 자동 조정하여 반환합니다.
        /// </summary>
        /// <param name="buildingType">건물 유형 (\&quot;Shop\&quot;, \&quot;House\&quot;, \&quot;CraftHouse\&quot;, \&quot;Church\&quot;, \&quot;Castle\&quot;)</param>
        /// <param name="exteriorColor">외부 지형 기준 색상 (기본 잔디색)</param>
        /// <returns>조정된 실내 바닥 텍스처 (외부 지형과 충분히 다른 경우 원본 유지)</returns>
        public static Texture2D ValidateAndFixFloorTexture(string buildingType, Color? exteriorColor = null)
        {
            Color extColor = exteriorColor ?? new Color(0.3f, 0.6f, 0.2f);

            Texture2D floorTex = buildingType.ToLower() switch
            {
                "shop" => GenerateShopFloor(),
                "house" => GenerateHouseFloor(),
                "crafthouse" => GenerateCraftHouseFloor(),
                "church" => GenerateChurchFloor(),
                "castle" => GenerateCastleFloor(),
                _ => null
            };

            if (floorTex != null)
            {
                DifferentiateFromExterior(floorTex, extColor);
            }

            return floorTex;
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

        // ===================================================================
        // [125차 후속35-R P3] 런타임 맵 합성 — 파일 교체 0 (AO 파생 + 미세 노이즈)
        // ===================================================================

        /// <summary>AO 파생 캐시 — 원본 텍스처별 1회 합성(정적 캐시, 파일 쓰기 없음).</summary>
        private static readonly System.Collections.Generic.Dictionary<Texture2D, Texture2D> _aoCache
            = new System.Collections.Generic.Dictionary<Texture2D, Texture2D>();

        /// <summary>
        /// [125차 후속35-R P3-1] 런타임 AO 맵 합성 — 알베도 휘도 하위 구간을 크레비스로 판정해
        /// 블러 후 AO 맵 생성(RGB 전채 동일 값 — URP Lit는 G 채널로 오클루전 판독).
        /// Blit 경유 판독이라 원본 readable 여부 무관. 파일 교체 0, 원본 무손상.
        /// 실패 시 null(호출부 폴백 계약 — AO 없는 기존 머티리얼 유지).
        /// </summary>
        public static Texture2D SynthesizeOcclusionFromAlbedo(Texture2D source)
        {
            if (source == null) return null;
            if (_aoCache.TryGetValue(source, out var cached) && cached != null) return cached;
            try
            {
                const int size = 256;
                var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
                var prevActive = RenderTexture.active;
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                var sampled = new Texture2D(size, size, TextureFormat.RGBA32, false);
                sampled.ReadPixels(new Rect(0f, 0f, size, size), 0, 0);
                sampled.Apply(false, false);
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);

                var pixels = sampled.GetPixels32();
                // 휘도 → 하위 30분위를 크레비스 후보로 판정(결정론 — RNG 없음)
                var lum = new float[pixels.Length];
                var hist = new int[32];
                for (int i = 0; i < pixels.Length; i++)
                {
                    float l = (pixels[i].r * 0.299f + pixels[i].g * 0.587f + pixels[i].b * 0.114f) / 255f;
                    lum[i] = l;
                    hist[Mathf.Clamp((int)(l * 31.99f), 0, 31)]++;
                }
                int acc = 0, cutoffIdx = 0;
                int target = pixels.Length * 3 / 10;
                for (int b = 0; b < 32; b++)
                {
                    acc += hist[b];
                    if (acc >= target) { cutoffIdx = b; break; }
                }
                float cutoff = (cutoffIdx + 1) / 32f;

                var ao = new float[pixels.Length];
                for (int i = 0; i < pixels.Length; i++)
                {
                    // 어두운 픽셀일수록 낮은 AO — 선형 페이드(하한 0.55로 클램프해 뭉개짐 방지)
                    float a = Mathf.Clamp01(lum[i] / Mathf.Max(0.02f, cutoff));
                    ao[i] = Mathf.Lerp(0.55f, 1f, a);
                }
                // 3×3 박스 블러 2패스 — 크레비스 가장자리 부드럽게(랩 어드레싱 — 시임리스)
                for (int pass = 0; pass < 2; pass++)
                {
                    var src = pass == 0 ? ao : (float[])ao.Clone();
                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            float sum = 0f;
                            for (int dy = -1; dy <= 1; dy++)
                                for (int dx = -1; dx <= 1; dx++)
                                    sum += src[((y + dy + size) % size) * size + ((x + dx + size) % size)];
                            ao[y * size + x] = sum / 9f;
                        }
                    }
                }

                var outTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "RuntimeAO_From_" + source.name,
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear
                };
                var outPx = new Color32[size * size];
                for (int i = 0; i < outPx.Length; i++)
                {
                    byte v = (byte)Mathf.RoundToInt(Mathf.Clamp01(ao[i]) * 255f);
                    outPx[i] = new Color32(v, v, v, 255);   // G 채널 판독 계약 — 전채 동일 값
                }
                outTex.SetPixels32(outPx);
                outTex.Apply(false, false);
                Object.DestroyImmediate(sampled);   // [125차 후속35-R] EditMode 안전 — Destroy는 에디트모드 금지(테스트 로그 에러 원인)
                _aoCache[source] = outTex;
                return outTex;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[IndoorTextureGenerator] AO 합성 실패 — 무AO 폴백: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// [125차 후속35-R P3-3] 미세 노이즈(타일러블 벨류 노이즈) — 디테일 알베도 맵.
        /// 반복 타일 위 미세 밝기 변이로 타일 반복감 완화. 32셀 랩 보간으로 좌우/상하 시임리스.
        /// 결정론(정수 해시) — RNG 없음.
        /// </summary>
        public static Texture2D GenerateMicroNoiseTexture(int size = 256)
        {
            const int cells = 32;
            float[] cellValues = new float[cells * cells];
            for (int cy = 0; cy < cells; cy++)
                for (int cx = 0; cx < cells; cx++)
                {
                    // 정수 해시 — 결정론 시임리스 노이즈
                    int h = cx * 374761393 + cy * 668265263;
                    h = (h ^ (h >> 13)) * 1274126177;
                    h = h ^ (h >> 16);
                    cellValues[cy * cells + cx] = (h & 0xFFFF) / 65535f;
                }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "RuntimeMicroNoise",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size * cells;
                    float v = (float)y / size * cells;
                    int x0 = (int)u % cells, y0 = (int)v % cells;
                    int x1 = (x0 + 1) % cells, y1 = (y0 + 1) % cells;
                    float fx = u - Mathf.Floor(u), fy = v - Mathf.Floor(v);
                    fx = fx * fx * (3f - 2f * fx);   // 스무스스텝 보간
                    fy = fy * fy * (3f - 2f * fy);
                    float n = Mathf.Lerp(
                        Mathf.Lerp(cellValues[y0 * cells + x0], cellValues[y0 * cells + x1], fx),
                        Mathf.Lerp(cellValues[y1 * cells + x0], cellValues[y1 * cells + x1], fx), fy);
                    byte g = (byte)Mathf.RoundToInt((0.5f + (n - 0.5f) * 0.18f) * 255f);   // ±9% 미세 변이
                    px[y * size + x] = new Color32(g, g, g, 255);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>
        /// [125차 후속36 A4] 모래→에이프런 경계 블렌드 그라데이션 — 가로 방향 페이드(모래색→다진 흙색)
        /// + 미세 노이즈. 링 메시의 u(내연→외연)에 매핑. 결정론, 시임리스(세로 랩).
        /// </summary>
        public static Texture2D GenerateEdgeBlendTexture(int width = 128, int height = 8)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "RuntimeEdgeBlend",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            var sand = new Color32(219, 200, 155, 255);     // 모래 베이스(GenerateSand 평균 톤)
            var apron = new Color32(120, 111, 95, 255);     // 다진 흙 베이스(CreateApron 평균 톤)
            var px = new Color32[width * height];
            for (int x = 0; x < width; x++)
            {
                float t = (float)x / (width - 1);
                // 스무스스텝 — 내연(모래)에서 빠르게 시작해 외연(흙)으로 부드럽게
                float g = t * t * (3f - 2f * t);
                // 정수 해시 노이즈 ±4% — 기계적 그라데이션 방지(가로 방향만 — 세로는 균일)
                int h = (int)(x * 2654435761L);   // [수리] 승수가 int 범위 초과 — long 곱 후 캐스트
                h = (h ^ (h >> 13)) * 1274126177;
                h = h ^ (h >> 16);
                float noise = ((h & 0xFF) / 255f - 0.5f) * 0.08f;
                for (int y = 0; y < height; y++)
                {
                    byte r = (byte)Mathf.Clamp(Mathf.Lerp(sand.r, apron.r, g) + noise * 255f, 0f, 255f);
                    byte gg = (byte)Mathf.Clamp(Mathf.Lerp(sand.g, apron.g, g) + noise * 255f, 0f, 255f);
                    byte b = (byte)Mathf.Clamp(Mathf.Lerp(sand.b, apron.b, g) + noise * 255f, 0f, 255f);
                    px[y * width + x] = new Color32(r, gg, b, 255);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }
    }
}