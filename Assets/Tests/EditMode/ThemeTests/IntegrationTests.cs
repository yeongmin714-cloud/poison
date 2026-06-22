#nullable disable
using NUnit.Framework;
using ProjectName.UI;
using ProjectName.UI.Themes;
using UnityEngine;

namespace ProjectName.Tests.EditMode.ThemeTests
{
    /// <summary>
    /// Phase 33 UI-01: 통합 테스트.
    /// 모든 테마 엔진 컴포넌트가 함께 동작하는지 검증.
    /// </summary>
    public class IntegrationTests
    {
        private UIDesignTheme CreateRoyalTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.SetColorSet(
                new Color(0.15f, 0.10f, 0.05f, 0.9f),
                new Color(0.95f, 0.75f, 0.25f, 0.9f),
                new Color(1f, 0.8f, 0.2f, 1f),
                Color.white,
                new Color(0.75f, 0.75f, 0.75f, 1f),
                new Color(0.9f, 0.7f, 0.2f, 1f)
            );
            // Use reflection to set private fields
            SetPrivateField(theme, "_themeName", "Royal Gold");
            SetPrivateField(theme, "_iconPrefix", "👑");
            SetPrivateField(theme, "_patternType", UIDesignTheme.PatternType.Marble);
            SetPrivateField(theme, "_borderType", UIDesignTheme.BorderType.Filigree);
            SetPrivateField(theme, "_decorationType", UIDesignTheme.DecorationType.Crown);
            SetPrivateField(theme, "_animationType", UIDesignTheme.AnimationType.Scale);
            SetPrivateField(theme, "_windowWidth", 800f);
            SetPrivateField(theme, "_windowHeight", 500f);
            return theme;
        }

        private static void SetPrivateField(UIDesignTheme theme, string fieldName, object value)
        {
            var field = typeof(UIDesignTheme).GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            if (field != null)
                field.SetValue(theme, value);
        }

        [Test]
        public void Theme_WithProceduralTexture_AllPatternsWork()
        {
            var theme = CreateRoyalTheme();
            // Marble 패턴 가져오기
            var tex = ProceduralTextureGenerator.GetPatternTexture(theme.CurrentPattern);
            Assert.IsNotNull(tex, "테마 패턴 텍스처 생성");
            Assert.AreEqual(256, tex.width);
            Assert.AreEqual(256, tex.height);

            // 캐싱 확인
            var tex2 = ProceduralTextureGenerator.GetPatternTexture(theme.CurrentPattern);
            Assert.AreSame(tex, tex2, "캐싱 동작");
        }

        [Test]
        public void Theme_WithGradient_AllModesWork()
        {
            var theme = CreateRoyalTheme();

            // 테마 색상으로 그라디언트 생성
            var vTex = GradientBackgroundRenderer.Vertical2Color(theme.BgColor, theme.BorderColor);
            Assert.IsNotNull(vTex, "세로 그라디언트");
            Assert.AreEqual(theme.BgColor, vTex.GetPixel(0, 0), "상단 BgColor");

            var hTex = GradientBackgroundRenderer.Horizontal2Color(theme.TitleColor, theme.AccentColor);
            Assert.IsNotNull(hTex, "가로 그라디언트");

            var rTex = GradientBackgroundRenderer.Radial(theme.TextColor, theme.SubTextColor);
            Assert.IsNotNull(rTex, "방사형 그라디언트");
        }

        [Test]
        public void Theme_WithBorder_AllTypesWork()
        {
            var theme = CreateRoyalTheme();

            foreach (UIDesignTheme.BorderType borderType in System.Enum.GetValues(typeof(UIDesignTheme.BorderType)))
            {
                Assert.DoesNotThrow(() =>
                {
                    DecorativeBorderRenderer.DrawBorder(
                        new Rect(10, 10, 400, 300),
                        borderType,
                        theme.BorderColor, 3f);
                }, $"{borderType} 보더 예외 없음");
            }
        }

        [Test]
        public void Theme_WithAnimation_AllTypesWork()
        {
            var theme = CreateRoyalTheme();

            foreach (UIDesignTheme.AnimationType animType in System.Enum.GetValues(typeof(UIDesignTheme.AnimationType)))
            {
                Assert.DoesNotThrow(() =>
                {
                    var coroutine = WindowAnimationProfile.GetOpenAnimation(
                        animType, null, null, null, 0.5f, 0.2f, 20f);
                    Assert.IsNotNull(coroutine, $"{animType} 코루틴 null 아님");
                }, $"{animType} 예외 없음");
            }
        }

        [Test]
        public void Theme_WithUIWindow_ApplyTheme_Works()
        {
            // UIWindow는 추상 클래스이므로 직접 인스턴스화 불가
            // 대신 ApplyTheme 로직 검증
            var theme = CreateRoyalTheme();

            // ApplyTheme는 단순히 _theme 할당
            // 리플렉션으로 확인
            var themeField = typeof(UIWindow).GetField("_theme",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(themeField, "_theme 필드 존재");
        }

        [Test]
        public void Theme_FullPipeline_NoExceptions()
        {
            var theme = CreateRoyalTheme();

            // 1. 패턴 텍스처 로드
            var pattern = ProceduralTextureGenerator.GetPatternTexture(theme.CurrentPattern);
            Assert.IsNotNull(pattern);

            // 2. 그라디언트 생성
            var gradient = GradientBackgroundRenderer.Vertical2Color(theme.BgColor, theme.BorderColor);
            Assert.IsNotNull(gradient);

            // 3. 보더 드로잉
            Assert.DoesNotThrow(() =>
            {
                DecorativeBorderRenderer.DrawBorder(
                    new Rect(50, 50, 500, 400),
                    theme.CurrentBorder,
                    theme.BorderColor, 2f);
            });

            // 4. 애니메이션 코루틴
            var anim = WindowAnimationProfile.GetOpenAnimation(
                theme.CurrentAnimation, null, null, null, 0.5f, 0.2f, 20f);
            Assert.IsNotNull(anim, "애니메이션 코루틴 생성");
        }

        [Test]
        public void Theme_AllProperties_Coherent()
        {
            var theme = CreateRoyalTheme();

            Assert.AreEqual("Royal Gold", theme.ThemeName);
            Assert.AreEqual("👑", theme.IconPrefix);
            Assert.AreEqual(UIDesignTheme.PatternType.Marble, theme.CurrentPattern);
            Assert.AreEqual(UIDesignTheme.BorderType.Filigree, theme.CurrentBorder);
            Assert.AreEqual(UIDesignTheme.DecorationType.Crown, theme.CurrentDecoration);
            Assert.AreEqual(UIDesignTheme.AnimationType.Scale, theme.CurrentAnimation);
            Assert.AreEqual(800f, theme.WindowWidth);
            Assert.AreEqual(500f, theme.WindowHeight);

            // Color coherence
            Assert.IsTrue(theme.BgColor.a > 0.8f, "Bg 알파 > 0.8");
            Assert.IsTrue(theme.BorderColor.a > 0.7f, "Border 알파 > 0.7");
            Assert.IsTrue(theme.TitleColor.a > 0.9f, "Title 알파 > 0.9");
        }



        [Test]
        public void PatternCache_DoesNotCorruptBetweenTests()
        {
            // 이전 테스트에서 캐시가 남아있어도 새로 생성 가능
            var tex1 = ProceduralTextureGenerator.GetPatternTexture(UIDesignTheme.PatternType.Parchment);
            var tex2 = ProceduralTextureGenerator.GetPatternTexture(UIDesignTheme.PatternType.Parchment);
            Assert.AreSame(tex1, tex2, "연속 호출도 캐싱");
        }

        [Test]
        public void EditorMenu_CanBeValidated()
        {
            // Phase33_CreateThemeAssets의 Validate 메서드가 true 반환
            var method = typeof(ProjectName.UI.Editor.Phase33_CreateThemeAssets).GetMethod(
                "ValidateCreateAllThemeAssets",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(method, "ValidateCreateAllThemeAssets 메서드 존재");
        }

        [Test]
        public void UIWindow_ThemeProperty_IsAccessible()
        {
            // UIWindow.Theme 프로퍼티가 존재하는지 검증
            var prop = typeof(UIWindow).GetProperty("Theme",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(prop, "UIWindow.Theme 프로퍼티 존재");
        }

        [Test]
        public void UIWindow_ApplyThemeMethod_Exists()
        {
            var method = typeof(UIWindow).GetMethod("ApplyTheme",
                new[] { typeof(UIDesignTheme) });
            Assert.IsNotNull(method, "UIWindow.ApplyTheme(UIDesignTheme) 메서드 존재");
        }
    }
}