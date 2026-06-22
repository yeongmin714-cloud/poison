#nullable disable
using NUnit.Framework;
using ProjectName.UI.Themes;
using UnityEngine;

namespace ProjectName.Tests.EditMode.ThemeTests
{
    /// <summary>
    /// Phase 33 UI-01: 그라디언트 렌더러 테스트.
    /// </summary>
    public class GradientTests
    {
        [Test]
        public void Vertical2Color_ReturnsNonNull()
        {
            var tex = GradientBackgroundRenderer.Vertical2Color(Color.red, Color.blue);
            Assert.IsNotNull(tex, "세로 그라디언트 텍스처 null 아님");
            Assert.AreEqual(256, tex.width);
            Assert.AreEqual(256, tex.height);
        }

        [Test]
        public void Horizontal2Color_ReturnsNonNull()
        {
            var tex = GradientBackgroundRenderer.Horizontal2Color(Color.green, Color.yellow);
            Assert.IsNotNull(tex);
            Assert.AreEqual(256, tex.width);
            Assert.AreEqual(256, tex.height);
        }

        [Test]
        public void Radial_ReturnsNonNull()
        {
            var tex = GradientBackgroundRenderer.Radial(Color.white, Color.black);
            Assert.IsNotNull(tex);
            Assert.AreEqual(256, tex.width);
            Assert.AreEqual(256, tex.height);
        }

        [Test]
        public void Vertical2Color_TopIsFirstColor()
        {
            var tex = GradientBackgroundRenderer.Vertical2Color(Color.red, Color.blue, 16, 16);
            var topPixel = tex.GetPixel(8, 0);
            var botPixel = tex.GetPixel(8, 15);
            Assert.AreEqual(Color.red, topPixel, "상단은 첫 번째 색상");
            Assert.AreEqual(Color.blue, botPixel, "하단은 두 번째 색상");
        }

        [Test]
        public void Horizontal2Color_LeftIsFirstColor()
        {
            var tex = GradientBackgroundRenderer.Horizontal2Color(Color.green, Color.yellow, 16, 16);
            var leftPixel = tex.GetPixel(0, 8);
            var rightPixel = tex.GetPixel(15, 8);
            Assert.AreEqual(Color.green, leftPixel, "좌측은 첫 번째 색상");
            Assert.AreEqual(Color.yellow, rightPixel, "우측은 두 번째 색상");
        }

        [Test]
        public void Radial_CenterIsFirstColor()
        {
            var tex = GradientBackgroundRenderer.Radial(Color.white, Color.black, 16, 16);
            var centerPixel = tex.GetPixel(8, 8);
            var edgePixel = tex.GetPixel(0, 0);
            Assert.AreEqual(Color.white, centerPixel, "중심은 첫 번째 색상");
            Assert.AreEqual(Color.black, edgePixel, "가장자리는 두 번째 색상");
        }

        [Test]
        public void Vertical_MiddleIsBlended()
        {
            var tex = GradientBackgroundRenderer.Vertical2Color(Color.red, Color.blue, 16, 16);
            var midPixel = tex.GetPixel(8, 8);
            Assert.AreNotEqual(Color.red, midPixel, "중간은 red가 아님");
            Assert.AreNotEqual(Color.blue, midPixel, "중간은 blue가 아님");
            Assert.IsTrue(midPixel.r > 0f && midPixel.r < 1f, "중간은 블렌드됨");
        }

        [Test]
        public void DrawGradientBackground_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                GradientBackgroundRenderer.DrawGradientBackground(
                    new Rect(0, 0, 100, 100),
                    GradientBackgroundRenderer.GradientMode.Vertical2Color,
                    Color.red, Color.blue);
            }, "DrawGradientBackground 예외 없음");
        }

        [Test]
        public void DrawGradientBackground_InvalidRect_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                GradientBackgroundRenderer.DrawGradientBackground(
                    new Rect(0, 0, -1, -1),
                    GradientBackgroundRenderer.GradientMode.Radial,
                    Color.white, Color.black);
            }, "잘못된 Rect에서 예외 없음");
        }

        [Test]
        public void GradientMode_Enums_Exist()
        {
            Assert.AreEqual(0, (int)GradientBackgroundRenderer.GradientMode.Vertical2Color, "Vertical2Color=0");
            Assert.AreEqual(1, (int)GradientBackgroundRenderer.GradientMode.Horizontal2Color, "Horizontal2Color=1");
            Assert.AreEqual(2, (int)GradientBackgroundRenderer.GradientMode.Radial, "Radial=2");
        }

        [Test]
        public void Radial_GradientIsSymmetric()
        {
            var tex = GradientBackgroundRenderer.Radial(Color.white, Color.black, 16, 16);
            var topLeft = tex.GetPixel(0, 0);
            var topRight = tex.GetPixel(15, 0);
            var botLeft = tex.GetPixel(0, 15);
            var botRight = tex.GetPixel(15, 15);
            // 방사형은 대칭이므로 모서리 색상이 동일
            Assert.AreEqual(topLeft, topRight, "좌상단 == 우상단");
            Assert.AreEqual(topLeft, botLeft, "좌상단 == 좌하단");
            Assert.AreEqual(topLeft, botRight, "좌상단 == 우하단");
        }
    }
}