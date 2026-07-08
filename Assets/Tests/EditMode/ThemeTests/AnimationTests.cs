#nullable disable
using NUnit.Framework;
using ProjectName.UI.Themes;
using System.Collections;
using UnityEngine;

namespace ProjectName.Tests.EditMode.ThemeTests
{
    /// <summary>
    /// Phase 33 UI-01: 창 애니메이션 프로필 테스트.
    /// 각 애니메이션 타입별 GetOpenAnimation/GetCloseAnimation 코루틴 반환 테스트.
    /// </summary>
    public class AnimationTests
    {
        private CanvasGroup CreateMockCanvasGroup()
        {
            var go = new GameObject("MockCanvas");
            return go.AddComponent<CanvasGroup>();
        }

        private RectTransform CreateMockRectTransform()
        {
            var go = new GameObject("MockRect");
            return go.AddComponent<RectTransform>();
        }

        [TearDown]
        public void TearDown()
        {
            var objects = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var obj in objects)
            {
                if (obj.name.StartsWith("Mock"))
                    Object.DestroyImmediate(obj);
            }
        }

        [Test]
        public void FadeSlide_OpenAnimation_ReturnsCoroutine()
        {
            var cg = CreateMockCanvasGroup();
            var rt = CreateMockRectTransform();

            var coroutine = WindowAnimationProfile.GetOpenAnimation(
                UIDesignTheme.AnimationType.FadeSlide,
                cg, rt, null, 0.5f, 0.2f, 20f);

            Assert.IsNotNull(coroutine, "FadeSlide 열기 코루틴 null 아님");
            Assert.IsTrue(coroutine is IEnumerator, "IEnumerator 타입");
        }

        [Test]
        public void Scale_OpenAnimation_ReturnsCoroutine()
        {
            var cg = CreateMockCanvasGroup();
            var rt = CreateMockRectTransform();

            var coroutine = WindowAnimationProfile.GetOpenAnimation(
                UIDesignTheme.AnimationType.Scale,
                cg, rt, null, 0.5f, 0.2f, 20f);

            Assert.IsNotNull(coroutine);
        }

        [Test]
        public void Flip_OpenAnimation_ReturnsCoroutine()
        {
            var cg = CreateMockCanvasGroup();
            var rt = CreateMockRectTransform();

            var coroutine = WindowAnimationProfile.GetOpenAnimation(
                UIDesignTheme.AnimationType.Flip,
                cg, rt, null, 0.5f, 0.2f, 20f);

            Assert.IsNotNull(coroutine);
        }

        [Test]
        public void Shatter_OpenAnimation_ReturnsCoroutine()
        {
            var cg = CreateMockCanvasGroup();
            var rt = CreateMockRectTransform();

            var coroutine = WindowAnimationProfile.GetOpenAnimation(
                UIDesignTheme.AnimationType.Shatter,
                cg, rt, null, 0.5f, 0.2f, 20f);

            Assert.IsNotNull(coroutine);
        }

        [Test]
        public void Spin_OpenAnimation_ReturnsCoroutine()
        {
            var cg = CreateMockCanvasGroup();
            var rt = CreateMockRectTransform();

            var coroutine = WindowAnimationProfile.GetOpenAnimation(
                UIDesignTheme.AnimationType.Spin,
                cg, rt, null, 0.5f, 0.2f, 20f);

            Assert.IsNotNull(coroutine);
        }

        [Test]
        public void Bounce_OpenAnimation_ReturnsCoroutine()
        {
            var cg = CreateMockCanvasGroup();
            var rt = CreateMockRectTransform();

            var coroutine = WindowAnimationProfile.GetOpenAnimation(
                UIDesignTheme.AnimationType.Bounce,
                cg, rt, null, 0.5f, 0.2f, 20f);

            Assert.IsNotNull(coroutine);
        }

        [Test]
        public void Reveal_OpenAnimation_ReturnsCoroutine()
        {
            var cg = CreateMockCanvasGroup();
            var rt = CreateMockRectTransform();

            var coroutine = WindowAnimationProfile.GetOpenAnimation(
                UIDesignTheme.AnimationType.Reveal,
                cg, rt, null, 0.5f, 0.2f, 20f);

            Assert.IsNotNull(coroutine);
        }

        [Test]
        public void Zoom_OpenAnimation_ReturnsCoroutine()
        {
            var cg = CreateMockCanvasGroup();
            var rt = CreateMockRectTransform();

            var coroutine = WindowAnimationProfile.GetOpenAnimation(
                UIDesignTheme.AnimationType.Zoom,
                cg, rt, null, 0.5f, 0.2f, 20f);

            Assert.IsNotNull(coroutine);
        }

        [Test]
        public void All_8_OpenAnimations_ReturnNonNull()
        {
            var cg = CreateMockCanvasGroup();
            var rt = CreateMockRectTransform();

            foreach (UIDesignTheme.AnimationType type in System.Enum.GetValues(typeof(UIDesignTheme.AnimationType)))
            {
                var coroutine = WindowAnimationProfile.GetOpenAnimation(type, cg, rt, null, 0.5f, 0.2f, 20f);
                Assert.IsNotNull(coroutine, $"{type} 열기 코루틴 null 아님");
            }
        }

        [Test]
        public void All_8_CloseAnimations_ReturnNonNull()
        {
            var cg = CreateMockCanvasGroup();
            var rt = CreateMockRectTransform();

            foreach (UIDesignTheme.AnimationType type in System.Enum.GetValues(typeof(UIDesignTheme.AnimationType)))
            {
                var coroutine = WindowAnimationProfile.GetCloseAnimation(type, cg, rt, null, 0.5f, 0.2f, 20f);
                Assert.IsNotNull(coroutine, $"{type} 닫기 코루틴 null 아님");
            }
        }

        [Test]
        public void FadeSlide_CloseAnimation_ReturnsCoroutine()
        {
            var cg = CreateMockCanvasGroup();
            var rt = CreateMockRectTransform();

            var coroutine = WindowAnimationProfile.GetCloseAnimation(
                UIDesignTheme.AnimationType.FadeSlide,
                cg, rt, null, 0.5f, 0.2f, 20f);

            Assert.IsNotNull(coroutine, "FadeSlide 닫기 코루틴 null 아님");
            Assert.IsTrue(coroutine is IEnumerator, "IEnumerator 타입");
        }

        [Test]
        public void AnimationType_Enum_Has8Values()
        {
            var names = System.Enum.GetNames(typeof(UIDesignTheme.AnimationType));
            Assert.AreEqual(8, names.Length, "AnimationType은 8개 값");
            CollectionAssert.Contains(names, "FadeSlide");
            CollectionAssert.Contains(names, "Scale");
            CollectionAssert.Contains(names, "Flip");
            CollectionAssert.Contains(names, "Shatter");
            CollectionAssert.Contains(names, "Spin");
            CollectionAssert.Contains(names, "Bounce");
            CollectionAssert.Contains(names, "Reveal");
            CollectionAssert.Contains(names, "Zoom");
        }

        [Test]
        public void Animations_WithNullCanvasGroup_DoNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                var coroutine = WindowAnimationProfile.GetOpenAnimation(
                    UIDesignTheme.AnimationType.Scale,
                    null, null, null, 0.5f, 0.2f, 20f);
            }, "null CanvasGroup에서 예외 없음");

            Assert.DoesNotThrow(() =>
            {
                var coroutine = WindowAnimationProfile.GetCloseAnimation(
                    UIDesignTheme.AnimationType.Flip,
                    null, null, null, 0.5f, 0.2f, 20f);
            }, "null CanvasGroup에서 닫기 예외 없음");
        }
    }
}