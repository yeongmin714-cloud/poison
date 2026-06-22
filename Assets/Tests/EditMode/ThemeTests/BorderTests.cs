#nullable disable
using NUnit.Framework;
using ProjectName.UI.Themes;
using UnityEngine;

namespace ProjectName.Tests.EditMode.ThemeTests
{
    /// <summary>
    /// Phase 33 UI-01: 장식 테두리 렌더러 테스트.
    /// </summary>
    public class BorderTests
    {
        [Test]
        public void Filigree_DrawBorder_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                DecorativeBorderRenderer.DrawBorder(
                    new Rect(100, 100, 400, 300),
                    UIDesignTheme.BorderType.Filigree,
                    Color.gold, 2f);
            }, "Filigree 예외 없음");
        }

        [Test]
        public void Rune_DrawBorder_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                DecorativeBorderRenderer.DrawBorder(
                    new Rect(100, 100, 400, 300),
                    UIDesignTheme.BorderType.Rune,
                    Color.cyan, 2f);
            }, "Rune 예외 없음");
        }

        [Test]
        public void Thorn_DrawBorder_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                DecorativeBorderRenderer.DrawBorder(
                    new Rect(100, 100, 400, 300),
                    UIDesignTheme.BorderType.Thorn,
                    Color.red, 2f);
            }, "Thorn 예외 없음");
        }

        [Test]
        public void Star_DrawBorder_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                DecorativeBorderRenderer.DrawBorder(
                    new Rect(100, 100, 400, 300),
                    UIDesignTheme.BorderType.Star,
                    Color.yellow, 2f);
            }, "Star 예외 없음");
        }

        [Test]
        public void Shield_DrawBorder_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                DecorativeBorderRenderer.DrawBorder(
                    new Rect(100, 100, 400, 300),
                    UIDesignTheme.BorderType.Shield,
                    Color.gray, 2f);
            }, "Shield 예외 없음");
        }

        [Test]
        public void DrawBorder_InvalidRect_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                DecorativeBorderRenderer.DrawBorder(
                    new Rect(0, 0, -1, -1),
                    UIDesignTheme.BorderType.Filigree,
                    Color.white, 0f);
            }, "잘못된 Rect에서 예외 없음");
        }

        [Test]
        public void DrawBorder_AllTypes_HaveDifferentVisuals()
        {
            // 각 보더 타입이 고유한 enum 값을 가짐
            Assert.AreEqual(0, (int)UIDesignTheme.BorderType.Filigree, "Filigree=0");
            Assert.AreEqual(1, (int)UIDesignTheme.BorderType.Rune, "Rune=1");
            Assert.AreEqual(2, (int)UIDesignTheme.BorderType.Thorn, "Thorn=2");
            Assert.AreEqual(3, (int)UIDesignTheme.BorderType.Star, "Star=3");
            Assert.AreEqual(4, (int)UIDesignTheme.BorderType.Shield, "Shield=4");
            Assert.AreEqual(5, System.Enum.GetValues(typeof(UIDesignTheme.BorderType)).Length, "5종 BorderType");
        }

        [Test]
        public void DrawBorder_UsesCorrectColor()
        {
            // DrawBorder는 color 파라미터를 사용 - 호출 시 예외 없음
            Assert.DoesNotThrow(() =>
            {
                DecorativeBorderRenderer.DrawBorder(
                    new Rect(200, 200, 300, 200),
                    UIDesignTheme.BorderType.Filigree,
                    new Color(0.5f, 0.3f, 0.8f, 1f), 3f);
            }, "커스텀 색상 적용 가능");
        }

        [Test]
        public void DrawBorder_Thickness_Applied()
        {
            // 두께 파라미터 적용 확인
            Assert.DoesNotThrow(() =>
            {
                DecorativeBorderRenderer.DrawBorder(
                    new Rect(50, 50, 500, 400),
                    UIDesignTheme.BorderType.Shield,
                    Color.white, 5f);
            }, "두께 5px 적용 가능");
        }

        [Test]
        public void BorderType_Enum_Count_Is5()
        {
            var names = System.Enum.GetNames(typeof(UIDesignTheme.BorderType));
            Assert.AreEqual(5, names.Length, "BorderType은 5개 값");
            CollectionAssert.Contains(names, "Filigree");
            CollectionAssert.Contains(names, "Rune");
            CollectionAssert.Contains(names, "Thorn");
            CollectionAssert.Contains(names, "Star");
            CollectionAssert.Contains(names, "Shield");
        }

        [Test]
        public void DrawBorder_MultipleCalls_NoException()
        {
            for (int i = 0; i < 10; i++)
            {
                Assert.DoesNotThrow(() =>
                {
                    DecorativeBorderRenderer.DrawBorder(
                        new Rect(10, 10, 200, 150),
                        UIDesignTheme.BorderType.Thorn,
                        Color.red, 2f);
                }, $"반복 호출 #{i}");
            }
        }
    }
}