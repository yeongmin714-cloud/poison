#nullable disable
using NUnit.Framework;
using ProjectName.UI.Themes;
using UnityEngine;

namespace ProjectName.Tests.EditMode.ThemeTests
{
    /// <summary>
    /// Phase 33 UI-01: UIDesignTheme ScriptableObject 데이터 테스트.
    /// </summary>
    public class ThemeDataTests
    {
        private UIDesignTheme CreateTestTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.SetColorSet(
                new Color(0f, 0f, 0f, 0.88f),
                new Color(0.85f, 0.65f, 0.15f, 0.8f),
                Color.yellow,
                Color.white,
                Color.gray,
                Color.cyan
            );
            return theme;
        }

        [Test]
        public void UIDesignTheme_CanBeCreated()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            Assert.IsNotNull(theme, "UIDesignTheme 인스턴스 생성 가능");
        }

        [Test]
        public void Theme_HasDefaultValues()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            Assert.AreEqual(6, theme.ColorCount, "ColorSet은 6개 색상");
            Assert.AreEqual(Color.white, theme.GetColor(10), "범위 외 인덱스는 흰색");

            var color0 = theme.GetColor(0);
            Assert.AreNotEqual(Color.clear, color0, "기본 ColorSet 값 존재");
        }

        [Test]
        public void SetColorSet_AppliesAllColors()
        {
            var theme = CreateTestTheme();

            Assert.AreEqual(new Color(0f, 0f, 0f, 0.88f), theme.BgColor, "BgColor 설정");
            Assert.AreEqual(new Color(0.85f, 0.65f, 0.15f, 0.8f), theme.BorderColor, "BorderColor 설정");
            Assert.AreEqual(Color.yellow, theme.TitleColor, "TitleColor 설정");
            Assert.AreEqual(Color.white, theme.TextColor, "TextColor 설정");
            Assert.AreEqual(Color.gray, theme.SubTextColor, "SubTextColor 설정");
            Assert.AreEqual(Color.cyan, theme.AccentColor, "AccentColor 설정");
        }

        [Test]
        public void Theme_ColorSetArray_LengthIs6()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            Assert.AreEqual(6, theme.ColorSet.Length, "ColorSet 배열 길이 6");
        }

        [Test]
        public void GetColor_ReturnsCorrectByIndex()
        {
            var theme = CreateTestTheme();
            Assert.AreEqual(new Color(0f, 0f, 0f, 0.88f), theme.GetColor(0), "Color[0] = Bg");
            Assert.AreEqual(new Color(0.85f, 0.65f, 0.15f, 0.8f), theme.GetColor(1), "Color[1] = Border");
            Assert.AreEqual(Color.yellow, theme.GetColor(2), "Color[2] = Title");
            Assert.AreEqual(Color.white, theme.GetColor(3), "Color[3] = Text");
            Assert.AreEqual(Color.gray, theme.GetColor(4), "Color[4] = SubText");
            Assert.AreEqual(Color.cyan, theme.GetColor(5), "Color[5] = Accent");
        }

        [Test]
        public void PatternType_Enum_Has7Values()
        {
            var names = System.Enum.GetNames(typeof(UIDesignTheme.PatternType));
            Assert.AreEqual(7, names.Length, "PatternType은 7개");
            CollectionAssert.Contains(names, "Parchment");
            CollectionAssert.Contains(names, "Leather");
            CollectionAssert.Contains(names, "Marble");
            CollectionAssert.Contains(names, "Wood");
            CollectionAssert.Contains(names, "Stone");
            CollectionAssert.Contains(names, "Metal");
            CollectionAssert.Contains(names, "Glass");
        }

        [Test]
        public void DecorationType_Enum_Has6Values()
        {
            var names = System.Enum.GetNames(typeof(UIDesignTheme.DecorationType));
            Assert.AreEqual(6, names.Length, "DecorationType은 6개");
            CollectionAssert.Contains(names, "None");
            CollectionAssert.Contains(names, "CornerScroll");
            CollectionAssert.Contains(names, "Rivet");
            CollectionAssert.Contains(names, "Seal");
            CollectionAssert.Contains(names, "Crown");
            CollectionAssert.Contains(names, "Skull");
        }

        [Test]
        public void Theme_Properties_Accessible()
        {
            var theme = CreateTestTheme();

            // 프로퍼티 접근 - null/예외 없음
            Assert.IsNotNull(theme.ThemeName);
            Assert.IsNotNull(theme.IconPrefix);
            Assert.DoesNotThrow(() => { var _ = theme.CurrentPattern; });
            Assert.DoesNotThrow(() => { var _ = theme.CurrentBorder; });
            Assert.DoesNotThrow(() => { var _ = theme.CurrentDecoration; });
            Assert.DoesNotThrow(() => { var _ = theme.CurrentAnimation; });
            Assert.IsTrue(theme.WindowWidth > 0);
            Assert.IsTrue(theme.WindowHeight > 0);
        }

        [Test]
        public void Theme_DefaultWindowSizes_ArePositive()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            Assert.IsTrue(theme.WindowWidth > 0, "WindowWidth > 0");
            Assert.IsTrue(theme.WindowHeight > 0, "WindowHeight > 0");
        }

        [Test]
        public void AnimationType_Default_IsFadeSlide()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            // AnimationType 기본값은 enum 첫 번째 값 (FadeSlide)
            Assert.AreEqual(0, (int)theme.CurrentAnimation, "기본 애니메이션은 FadeSlide (index 0)");
        }

        [Test]
        public void ColorSet_CanBeModified()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.SetColorSet(Color.red, Color.green, Color.blue, Color.white, Color.black, Color.yellow);
            Assert.AreEqual(Color.red, theme.BgColor);
            Assert.AreEqual(Color.green, theme.BorderColor);
            Assert.AreEqual(Color.blue, theme.TitleColor);
            Assert.AreEqual(Color.yellow, theme.AccentColor);
        }

        [Test]
        public void MultipleThemes_Are_IndependentInstances()
        {
            var theme1 = ScriptableObject.CreateInstance<UIDesignTheme>();
            var theme2 = ScriptableObject.CreateInstance<UIDesignTheme>();

            theme1.SetColorSet(Color.red, Color.green, Color.blue, Color.white, Color.black, Color.yellow);
            theme2.SetColorSet(Color.black, Color.gray, Color.white, Color.cyan, Color.magenta, Color.red);

            Assert.AreEqual(Color.red, theme1.BgColor);
            Assert.AreEqual(Color.black, theme2.BgColor);
            Assert.AreNotEqual(theme1.BgColor, theme2.BgColor, "테마 인스턴스 간 독립성");
        }
    }
}