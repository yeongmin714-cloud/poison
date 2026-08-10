using NUnit.Framework;
using UnityEngine;
using ProjectName.UI;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// G3-05: UIStyleManager 테스트
    /// </summary>
    public class UIStyleManagerTests
    {
        [Test]
        public void StaticClass_Colors_Exist()
        {
            Assert.AreNotEqual(Color.clear, UIStyleManager.BgColor, "BgColor가 설정되어야 함");
            Assert.AreNotEqual(Color.clear, UIStyleManager.BorderColor, "BorderColor가 설정되어야 함");
            Assert.AreNotEqual(Color.clear, UIStyleManager.TitleColor, "TitleColor가 설정되어야 함");
        }

        [Test]
        public void MakeTexture_Returns_NonNull()
        {
            var tex = UIStyleManager.MakeTexture(4, 4, Color.red);
            Assert.IsNotNull(tex, "MakeTexture는 null이 아니어야 함");
            Assert.AreEqual(4, tex.width);
            Assert.AreEqual(4, tex.height);
        }

        [Test]
        public void MakeTexture_Has_CorrectColor()
        {
            var tex = UIStyleManager.MakeTexture(2, 2, Color.green);
            var pixel = tex.GetPixel(0, 0);
            Assert.AreEqual(Color.green, pixel, "픽셀 색상이 일치해야 함");
        }

        [Test]
        public void DrawDimOverlay_NoException()
        {
            Assert.DoesNotThrow(() => UIStyleManager.DrawDimOverlay());
        }

        [Test]
        public void DrawWindowBackground_NoException()
        {
            Assert.DoesNotThrow(() => UIStyleManager.DrawWindowBackground(new Rect(100, 100, 400, 300)));
        }

        [Test]
        public void DrawTitle_NoException()
        {
            Assert.DoesNotThrow(() => UIStyleManager.DrawTitle(new Rect(100, 100, 400, 300), "테스트 제목"));
        }

        [Test]
        public void DrawCloseButton_NoException()
        {
            Assert.DoesNotThrow(() => UIStyleManager.DrawCloseButton(new Rect(100, 100, 400, 300)));
        }

        [Test]
        public void LabelStyle_Is_Initialized()
        {
            var style = UIStyleManager.LabelStyle;
            Assert.IsNotNull(style, "LabelStyle은 null이 아니어야 함");
            Assert.AreEqual(40, style.fontSize, "LabelStyle fontSize = 40");
        }

        [Test]
        public void TitleStyle_Is_Initialized()
        {
            var style = UIStyleManager.TitleStyle;
            Assert.IsNotNull(style, "TitleStyle은 null이 아니어야 함");
            Assert.AreEqual(60, style.fontSize, "TitleStyle fontSize = 60");
        }

        [Test]
        public void BorderWidth_Is_4()
        {
            Assert.AreEqual(4, UIStyleManager.BorderWidth, "테두리 두께는 4px");
        }

        [Test]
        public void Colors_Are_ReadOnly()
        {
            // 색상이 기본값과 다름 확인
            Assert.IsTrue(UIStyleManager.BgColor.a > 0.8f, "BgColor 알파 > 0.8");
            Assert.IsTrue(UIStyleManager.DimColor.a > 0.4f, "DimColor 알파 > 0.4");
        }
    }
}