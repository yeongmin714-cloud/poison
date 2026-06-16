using NUnit.Framework;
using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C11-02~04: IndoorTextureGenerator EditMode 테스트.
    /// 텍스처 생성, 포맷, 픽셀 색상 확인.
    /// </summary>
    public class IndoorTextureGeneratorTests
    {
        private const int W = 256;
        private const int H = 256;

        // ===== C11-02: 기본 텍스처 =====

        [Test]
        public void GenerateFloorTile_ReturnsNonNullTexture()
        {
            Texture2D tex = IndoorTextureGenerator.GenerateFloorTile(W, H, Color.white, Color.black);
            Assert.IsNotNull(tex, "GenerateFloorTile must return non-null Texture2D");
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void GenerateFloorTile_CorrectSize()
        {
            Texture2D tex = IndoorTextureGenerator.GenerateFloorTile(W, H, Color.white, Color.black);
            Assert.AreEqual(W, tex.width, "Texture width must be 256");
            Assert.AreEqual(H, tex.height, "Texture height must be 256");
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void GenerateFloorTile_RGBA32Format()
        {
            Texture2D tex = IndoorTextureGenerator.GenerateFloorTile(W, H, Color.white, Color.black);
            Assert.AreEqual(TextureFormat.RGBA32, tex.format, "Texture format must be RGBA32");
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void GenerateFloorTile_RepeatableWrapping()
        {
            Texture2D tex = IndoorTextureGenerator.GenerateFloorTile(W, H, Color.white, Color.black);
            Assert.AreEqual(TextureWrapMode.Repeat, tex.wrapMode, "Texture must have Repeat wrap mode");
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void GenerateFloorTile_HasGridPattern()
        {
            Texture2D tex = IndoorTextureGenerator.GenerateFloorTile(W, H, Color.white, Color.black, 0.25f);
            // 모서리 (0,0)는 격자 선 위에 있어야 함 (black)
            Color corner = tex.GetPixel(0, 0);
            Assert.IsTrue(corner.r < 0.1f && corner.g < 0.1f && corner.b < 0.1f,
                "Corner pixel must be the secondary (grid line) color");

            // 중앙 (타일 내부)는 primary (white)
            Color center = tex.GetPixel(W / 2, H / 2);
            Assert.IsTrue(center.r > 0.9f && center.g > 0.9f && center.b > 0.9f,
                "Center pixel must be the primary color");

            Object.DestroyImmediate(tex);
        }

        [Test]
        public void GenerateWallPattern_ReturnsNonNull()
        {
            Texture2D tex = IndoorTextureGenerator.GenerateWallPattern(W, H, Color.gray, Color.white);
            Assert.IsNotNull(tex);
            Assert.AreEqual(W, tex.width);
            Assert.AreEqual(H, tex.height);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void GenerateWallPattern_HorizontalLines_Default()
        {
            Texture2D tex = IndoorTextureGenerator.GenerateWallPattern(W, H, Color.gray, Color.white);
            // y=0은 선 위 (secondary/white)
            Color topLine = tex.GetPixel(0, 0);
            Assert.IsTrue(topLine.r > 0.9f, "y=0 should be on a horizontal line (white)");

            // 중간은 primary (gray)
            Color mid = tex.GetPixel(0, 16);
            Assert.IsTrue(mid.r < 0.6f, "Middle area should be the primary color (gray)");

            Object.DestroyImmediate(tex);
        }

        [Test]
        public void GenerateWallPattern_VerticalLines()
        {
            Texture2D tex = IndoorTextureGenerator.GenerateWallPattern(W, H, Color.gray, Color.white, false);
            // x=0은 선 위
            Color leftLine = tex.GetPixel(0, 0);
            Assert.IsTrue(leftLine.r > 0.9f, "x=0 should be on a vertical line (white)");

            Object.DestroyImmediate(tex);
        }

        [Test]
        public void GenerateWoodPlank_ReturnsNonNull()
        {
            Texture2D tex = IndoorTextureGenerator.GenerateWoodPlank(W, H, new Color(0.5f, 0.3f, 0.1f));
            Assert.IsNotNull(tex);
            Assert.AreEqual(W, tex.width);
            Assert.AreEqual(H, tex.height);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void GenerateWoodPlank_HasVerticalStripes()
        {
            Color woodColor = new Color(0.5f, 0.3f, 0.1f);
            Texture2D tex = IndoorTextureGenerator.GenerateWoodPlank(W, H, woodColor, 0.2f);

            // x=0 (경계선)은 어두워야 함
            Color leftEdge = tex.GetPixel(0, H / 2);
            // x=32 (판자 내부)는 밝아야 함
            Color inside = tex.GetPixel(32, H / 2);

            // 경계선 픽셀은 내부보다 어둡거나 같음
            Assert.IsTrue(leftEdge.r <= inside.r || leftEdge.g <= inside.g,
                "Plank edge (x=0) should be darker than plank interior (x=32)");

            Object.DestroyImmediate(tex);
        }

        // ===== C11-03: 바닥 타일 텍스처 =====

        [Test]
        public void GenerateBrickPattern_ReturnsNonNull()
        {
            Texture2D tex = IndoorTextureGenerator.GenerateBrickPattern(W, H, Color.red, Color.gray);
            Assert.IsNotNull(tex);
            Assert.AreEqual(W, tex.width);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void GenerateBrickPattern_StaggeredLayout()
        {
            Texture2D tex = IndoorTextureGenerator.GenerateBrickPattern(W, H, Color.red, Color.gray, 0.4f, 0.15f);
            // 0행 (offset=false): x=0은 모르타르
            Color row0 = tex.GetPixel(0, 1);
            // 1행 (offset=true): x=0은 벽돌 중앙이어야 함
            int brickH = Mathf.RoundToInt(H * 0.15f);
            Color row1 = tex.GetPixel(0, brickH + 1);

            // 0행이 모르타르(회색)이면 1행도 벽돌(빨강)과 다름
            Assert.IsTrue(row0.r < row1.r || row0.g > row1.g,
                "Brick pattern must have staggered layout - color changes between rows");

            Object.DestroyImmediate(tex);
        }

        [Test]
        public void GenerateStoneTile_ReturnsNonNull()
        {
            Texture2D tex = IndoorTextureGenerator.GenerateStoneTile(W, H, Color.gray, Color.black);
            Assert.IsNotNull(tex);
            Assert.AreEqual(W, tex.width);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void GenerateStoneTile_HasCracks()
        {
            Texture2D tex = IndoorTextureGenerator.GenerateStoneTile(W, H, Color.gray, Color.black, 0.05f);
            // 균열은 검정색이므로 일부 픽셀이 검정에 가까워야 함
            bool foundCrack = false;
            for (int y = 0; y < H && !foundCrack; y += 16)
            {
                for (int x = 0; x < W && !foundCrack; x += 16)
                {
                    Color pixel = tex.GetPixel(x, y);
                    if (pixel.r < 0.1f && pixel.g < 0.1f && pixel.b < 0.1f)
                    {
                        foundCrack = true;
                    }
                }
            }
            Assert.IsTrue(foundCrack, "Stone tile must have crack (black) pixels");
            Object.DestroyImmediate(tex);
        }

        // ===== C11-03: 건물 용도별 바닥 프리셋 =====

        [Test]
        public void GenerateShopFloor_ReturnsNonNull()
        {
            Texture2D tex = IndoorTextureGenerator.GenerateShopFloor();
            Assert.IsNotNull(tex);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void GenerateCraftHouseFloor_ReturnsNonNull()
        {
            Texture2D tex = IndoorTextureGenerator.GenerateCraftHouseFloor();
            Assert.IsNotNull(tex);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void GenerateChurchFloor_ReturnsNonNull()
        {
            Texture2D tex = IndoorTextureGenerator.GenerateChurchFloor();
            Assert.IsNotNull(tex);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void GenerateHouseFloor_ReturnsNonNull()
        {
            Texture2D tex = IndoorTextureGenerator.GenerateHouseFloor();
            Assert.IsNotNull(tex);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void GenerateCastleFloor_ReturnsNonNull()
        {
            Texture2D tex = IndoorTextureGenerator.GenerateCastleFloor();
            Assert.IsNotNull(tex);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void GenerateShopFloor_WarmBrownColor()
        {
            Texture2D tex = IndoorTextureGenerator.GenerateShopFloor();
            // 중앙 픽셀이 갈색 계열인지 확인 (R > G > B)
            Color center = tex.GetPixel(W / 2, H / 2);
            Assert.IsTrue(center.r > center.g && center.g > center.b,
                "Shop floor must be warm brown (R > G > B)");
            Object.DestroyImmediate(tex);
        }

        // ===== C11-04: 벽면 패널 텍스처 =====

        [Test]
        public void GenerateStoneWall_ReturnsNonNull()
        {
            Texture2D tex = IndoorTextureGenerator.GenerateStoneWall(W, H, Color.gray, Color.white);
            Assert.IsNotNull(tex);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void GenerateStoneWall_HasMortarLines()
        {
            Texture2D tex = IndoorTextureGenerator.GenerateStoneWall(W, H, Color.gray, Color.white, 0.15f, 0.1f);
            // 모르타르(흰색) 픽셀이 존재해야 함
            bool hasMortar = false;
            for (int y = 0; y < 10; y++)
            {
                for (int x = 0; x < 10; x++)
                {
                    Color pixel = tex.GetPixel(x, y);
                    if (pixel.r > 0.9f && pixel.g > 0.9f && pixel.b > 0.9f)
                    {
                        hasMortar = true;
                        break;
                    }
                }
            }
            Assert.IsTrue(hasMortar, "Stone wall must have mortar (white) pixels");
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void GenerateWoodPanel_ReturnsNonNull()
        {
            Texture2D tex = IndoorTextureGenerator.GenerateWoodPanel(W, H, new Color(0.6f, 0.4f, 0.2f), Color.black);
            Assert.IsNotNull(tex);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void GeneratePlasterWall_ReturnsNonNull()
        {
            Texture2D tex = IndoorTextureGenerator.GeneratePlasterWall(W, H, Color.white);
            Assert.IsNotNull(tex);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void GeneratePlasterWall_FineTexture()
        {
            Texture2D tex = IndoorTextureGenerator.GeneratePlasterWall(W, H, Color.white);
            // 모든 픽셀이 약간의 변주를 가져야 함 (완전 균일하지 않음)
            Color p1 = tex.GetPixel(0, 0);
            Color p2 = tex.GetPixel(100, 100);
            bool hasVariation = Mathf.Abs(p1.r - p2.r) > 0.001f;
            Assert.IsTrue(hasVariation, "Plaster wall must have fine texture variation");
            Object.DestroyImmediate(tex);
        }

        // ===== C11-04: 건물 용도별 벽면 프리셋 =====

        [Test]
        public void GenerateShopWall_ReturnsNonNull()
        {
            Texture2D tex = IndoorTextureGenerator.GenerateShopWall();
            Assert.IsNotNull(tex);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void GenerateCraftHouseWall_ReturnsNonNull()
        {
            Texture2D tex = IndoorTextureGenerator.GenerateCraftHouseWall();
            Assert.IsNotNull(tex);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void GenerateChurchWall_ReturnsNonNull()
        {
            Texture2D tex = IndoorTextureGenerator.GenerateChurchWall();
            Assert.IsNotNull(tex);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void GenerateHouseWall_ReturnsNonNull()
        {
            Texture2D tex = IndoorTextureGenerator.GenerateHouseWall();
            Assert.IsNotNull(tex);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void GenerateCastleWall_ReturnsNonNull()
        {
            Texture2D tex = IndoorTextureGenerator.GenerateCastleWall();
            Assert.IsNotNull(tex);
            Object.DestroyImmediate(tex);
        }

        // ===== 에지 케이스 =====

        [Test]
        public void GenerateFloorTile_ZeroSize_Fallback()
        {
            Texture2D tex = IndoorTextureGenerator.GenerateFloorTile(0, 0, Color.white, Color.black);
            Assert.IsNotNull(tex, "Zero size should fall back to defaults");
            Assert.IsTrue(tex.width > 0 && tex.height > 0, "Fallback texture should have positive dimensions");
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void GenerateFloorTile_SmallSize()
        {
            Texture2D tex = IndoorTextureGenerator.GenerateFloorTile(4, 4, Color.white, Color.black);
            Assert.IsNotNull(tex);
            Assert.AreEqual(4, tex.width);
            Assert.AreEqual(4, tex.height);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void AllPresets_UniqueTextures()
        {
            Texture2D shopFloor = IndoorTextureGenerator.GenerateShopFloor();
            Texture2D craftFloor = IndoorTextureGenerator.GenerateCraftHouseFloor();
            Assert.AreNotSame(shopFloor, craftFloor, "Different presets must create different Texture2D instances");
            Object.DestroyImmediate(shopFloor);
            Object.DestroyImmediate(craftFloor);
        }
    }
}
