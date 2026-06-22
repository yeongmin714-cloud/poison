#nullable disable
using NUnit.Framework;
using ProjectName.UI.Themes;
using UnityEngine;

namespace ProjectName.Tests.EditMode.ThemeTests
{
    /// <summary>
    /// Phase 33 UI-01: 패턴 타입 생성 테스트.
    /// 각 7종 배경 패턴이 올바르게 생성되는지 검증.
    /// </summary>
    public class PatternTypesTests
    {
        [TearDown]
        public void TearDown()
        {
            ProceduralTextureGenerator.ClearCache();
        }

        [Test]
        public void Parchment_Texture_IsCreated()
        {
            var tex = ProceduralTextureGenerator.GetPatternTexture(UIDesignTheme.PatternType.Parchment);
            Assert.IsNotNull(tex, "Parchment 텍스처가 null이 아님");
            Assert.AreEqual(256, tex.width, "너비 256");
            Assert.AreEqual(256, tex.height, "높이 256");
        }

        [Test]
        public void Leather_Texture_IsCreated()
        {
            var tex = ProceduralTextureGenerator.GetPatternTexture(UIDesignTheme.PatternType.Leather);
            Assert.IsNotNull(tex, "Leather 텍스처가 null이 아님");
            Assert.AreEqual(256, tex.width);
            Assert.AreEqual(256, tex.height);
        }

        [Test]
        public void Marble_Texture_IsCreated()
        {
            var tex = ProceduralTextureGenerator.GetPatternTexture(UIDesignTheme.PatternType.Marble);
            Assert.IsNotNull(tex, "Marble 텍스처가 null이 아님");
            Assert.AreEqual(256, tex.width);
            Assert.AreEqual(256, tex.height);
        }

        [Test]
        public void Wood_Texture_IsCreated()
        {
            var tex = ProceduralTextureGenerator.GetPatternTexture(UIDesignTheme.PatternType.Wood);
            Assert.IsNotNull(tex, "Wood 텍스처가 null이 아님");
            Assert.AreEqual(256, tex.width);
            Assert.AreEqual(256, tex.height);
        }

        [Test]
        public void Stone_Texture_IsCreated()
        {
            var tex = ProceduralTextureGenerator.GetPatternTexture(UIDesignTheme.PatternType.Stone);
            Assert.IsNotNull(tex, "Stone 텍스처가 null이 아님");
            Assert.AreEqual(256, tex.width);
            Assert.AreEqual(256, tex.height);
        }

        [Test]
        public void Metal_Texture_IsCreated()
        {
            var tex = ProceduralTextureGenerator.GetPatternTexture(UIDesignTheme.PatternType.Metal);
            Assert.IsNotNull(tex, "Metal 텍스처가 null이 아님");
            Assert.AreEqual(256, tex.width);
            Assert.AreEqual(256, tex.height);
        }

        [Test]
        public void Glass_Texture_IsCreated()
        {
            var tex = ProceduralTextureGenerator.GetPatternTexture(UIDesignTheme.PatternType.Glass);
            Assert.IsNotNull(tex, "Glass 텍스처가 null이 아님");
            Assert.AreEqual(256, tex.width);
            Assert.AreEqual(256, tex.height);
        }

        [Test]
        public void All_7_Patterns_Are_DifferentInstances()
        {
            var parch = ProceduralTextureGenerator.GetPatternTexture(UIDesignTheme.PatternType.Parchment);
            var leath = ProceduralTextureGenerator.GetPatternTexture(UIDesignTheme.PatternType.Leather);
            var marbl = ProceduralTextureGenerator.GetPatternTexture(UIDesignTheme.PatternType.Marble);
            var wood = ProceduralTextureGenerator.GetPatternTexture(UIDesignTheme.PatternType.Wood);
            var stone = ProceduralTextureGenerator.GetPatternTexture(UIDesignTheme.PatternType.Stone);
            var metal = ProceduralTextureGenerator.GetPatternTexture(UIDesignTheme.PatternType.Metal);
            var glass = ProceduralTextureGenerator.GetPatternTexture(UIDesignTheme.PatternType.Glass);

            Assert.AreNotSame(parch, leath, "Parchment != Leather");
            Assert.AreNotSame(parch, marbl, "Parchment != Marble");
            Assert.AreNotSame(leath, wood, "Leather != Wood");
            Assert.AreNotSame(marbl, glass, "Marble != Glass");
            Assert.AreNotSame(stone, metal, "Stone != Metal");
        }

        [Test]
        public void SamePattern_ReturnsCachedInstance()
        {
            var first = ProceduralTextureGenerator.GetPatternTexture(UIDesignTheme.PatternType.Parchment);
            var second = ProceduralTextureGenerator.GetPatternTexture(UIDesignTheme.PatternType.Parchment);
            Assert.AreSame(first, second, "동일 패턴은 캐싱되어 같은 인스턴스");
        }

        [Test]
        public void ClearCache_RemovesAll()
        {
            ProceduralTextureGenerator.GetPatternTexture(UIDesignTheme.PatternType.Parchment);
            ProceduralTextureGenerator.GetPatternTexture(UIDesignTheme.PatternType.Marble);
            ProceduralTextureGenerator.ClearCache();

            // 캐시 클리어 후 새 인스턴스 생성
            var recreated = ProceduralTextureGenerator.GetPatternTexture(UIDesignTheme.PatternType.Parchment);
            Assert.IsNotNull(recreated, "캐시 클리어 후에도 생성 가능");
        }

        [Test]
        public void MakeTexture_CreatesCorrectSolidColor()
        {
            var tex = ProceduralTextureGenerator.MakeTexture(4, 4, Color.red);
            Assert.IsNotNull(tex);
            Assert.AreEqual(4, tex.width);
            Assert.AreEqual(4, tex.height);
            Assert.AreEqual(Color.red, tex.GetPixel(0, 0), "색상 정확");
            Assert.AreEqual(Color.red, tex.GetPixel(3, 3), "모든 픽셀 동일 색상");
        }

        [Test]
        public void Parchment_HasWarmTone()
        {
            var tex = ProceduralTextureGenerator.GetPatternTexture(UIDesignTheme.PatternType.Parchment);
            var centerPixel = tex.GetPixel(128, 128);
            Assert.IsTrue(centerPixel.r > 0.5f, "Parchment는 붉은 톤 (r > 0.5)");
            Assert.IsTrue(centerPixel.g > 0.4f, "Parchment는 녹색 톤 (g > 0.4)");
        }

        [Test]
        public void Leather_HasDarkBrownTone()
        {
            var tex = ProceduralTextureGenerator.GetPatternTexture(UIDesignTheme.PatternType.Leather);
            var centerPixel = tex.GetPixel(128, 128);
            Assert.IsTrue(centerPixel.r < 0.6f, "Leather는 어두움 (r < 0.6)");
            Assert.IsTrue(centerPixel.g < 0.4f, "Leather는 녹색 낮음 (g < 0.4)");
        }

        [Test]
        public void Glass_HasLowAlpha()
        {
            var tex = ProceduralTextureGenerator.GetPatternTexture(UIDesignTheme.PatternType.Glass);
            var centerPixel = tex.GetPixel(128, 128);
            Assert.IsTrue(centerPixel.a < 0.8f, "Glass는 반투명 (alpha < 0.8)");
        }

        [Test]
        public void Stone_HasGrayTone()
        {
            var tex = ProceduralTextureGenerator.GetPatternTexture(UIDesignTheme.PatternType.Stone);
            var centerPixel = tex.GetPixel(128, 128);
            float avg = (centerPixel.r + centerPixel.g + centerPixel.b) / 3f;
            Assert.IsTrue(avg > 0.2f && avg < 0.8f, $"Stone은 회색 톤 (평균 {avg:F2})");
        }
    }
}