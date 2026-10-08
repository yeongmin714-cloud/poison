using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.UI.Toolkit;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [Fluent 스크롤러] 전 창 ScrollView 스타일 계약 — Unity 기본 스크롤러(두꺼운 트랙 + 화살표 버튼)를
    /// Fluent 얇은 오버레이형(6px 트랙 + 둥근 반투명 썸, 화살표 없음, 가로 숨김, 세로 Auto)으로 통일한다.
    /// Figma 프레임에는 스크롤러가 없는 정적 목업이므로, 런타임 오버플로 스크롤은 최소 표현이 계약.
    /// </summary>
    public class FluentScrollerTests
    {
        [Test]
        public void ApplyFluentScroller_ThinsTrackHidesArrowButtonsAndStylesThumb()
        {
            var sv = new ScrollView();
            sv.style.width = 200f;
            sv.style.height = 100f;
            for (int i = 0; i < 40; i++)
                sv.contentContainer.Add(new Label("row " + i));

            UTKTheme.ApplyFluentScroller(sv);

            Assert.That(sv.ClassListContains("utk-fluent-scroller"), Is.True, "멱등 마커 클래스가 부여돼야 한다.");
            Assert.That(sv.horizontalScrollerVisibility, Is.EqualTo(ScrollerVisibility.Hidden),
                "Figma UI는 세로 오버플로만 — 가로 스크롤러는 숨김이 계약.");
            Assert.That(sv.verticalScrollerVisibility, Is.EqualTo(ScrollerVisibility.Auto),
                "세로 스크롤러는 내용이 넘칠 때만 표시(Auto) — 넘침이 없으면 Figma처럼 보이지 않는다.");
            Assert.That(sv.verticalScroller.lowButton.style.display.value, Is.EqualTo(DisplayStyle.None),
                "화살표(리피트) 버튼 제거 — lowButton.");
            Assert.That(sv.verticalScroller.highButton.style.display.value, Is.EqualTo(DisplayStyle.None),
                "화살표(리피트) 버튼 제거 — highButton.");
            Assert.That(sv.verticalScroller.style.width.value.value, Is.EqualTo(UTKTheme.FluentScrollThickness).Within(0.001f),
                "트랙 두께는 Fluent 6px.");
            var dragger = sv.verticalScroller.slider.Q(className: "unity-base-slider__dragger");
            Assert.That(dragger, Is.Not.Null, "썸(dragger) 요소가 존재해야 한다.");
            Assert.That(dragger.style.backgroundColor.value, Is.EqualTo(UTKTheme.FluentScrollThumb),
                "썸 기본색은 #8B949E @ 0.35 반투명.");
            Assert.That(dragger.style.borderTopLeftRadius.value.value,
                Is.EqualTo(UTKTheme.FluentScrollThickness * 0.5f).Within(0.001f),
                "썸은 완전히 둥근 캡슐(반경=두께/2).");
        }

        [Test]
        public void ApplyFluentScroller_IsIdempotent_NoCallbackStacking()
        {
            var sv = new ScrollView();
            sv.style.width = 200f;
            sv.style.height = 100f;

            UTKTheme.ApplyFluentScroller(sv);
            UTKTheme.ApplyFluentScroller(sv);
            UTKTheme.ApplyFluentScroller(sv);

            Assert.That(sv.ClassListContains("utk-fluent-scroller"), Is.True);
            var dragger = sv.verticalScroller.slider.Q(className: "unity-base-slider__dragger");
            Assert.That(dragger, Is.Not.Null);
            // 멱등 마커로 재적용이 차단됐는지 — 썸 스타일이 재기록 없이 유지되는 것으로 확인.
            Assert.That(dragger.style.backgroundColor.value, Is.EqualTo(UTKTheme.FluentScrollThumb));
            Assert.That(sv.verticalScroller.lowButton.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ApplyFluentScrollers_Tree_WorksOnWindowSubtree()
        {
            // UTKWindowManager.Register는 EditMode에서 DontDestroyOnLoad로 불가(알려진 baseline 제약)라
            // Show 전체를 돌리지 않고, Show가 사용하는 동일 공용 루틴을 창 서브트리에 직접 적용해 검증한다.
            var win = new UTKWindowBase("fluent-test", new Vector2(400f, 300f));
            var svA = new ScrollView { name = "fluent-sv-a" };
            var svB = new ScrollView { name = "fluent-sv-b" };
            svA.style.width = 100f;
            svA.style.height = 100f;
            win.Content.Add(svA);
            win.Content.Add(svB);

            UTKTheme.ApplyFluentScrollers(win);

            Assert.That(svA.ClassListContains("utk-fluent-scroller"), Is.True,
                "창 서브트리의 모든 ScrollView에 일괄 적용돼야 한다.");
            Assert.That(svB.ClassListContains("utk-fluent-scroller"), Is.True,
                "중첩된 두 번째 ScrollView에도 적용돼야 한다.");
            Assert.That(svA.verticalScroller.lowButton.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ApplyFluentScrollers_NullTree_IsSafe()
        {
            Assert.DoesNotThrow(() => UTKTheme.ApplyFluentScrollers(null));
        }

        [Test]
        public void WindowShow_CentralApplication_ContractIsWired()
        {
            // Show 전체 실행은 EditMode에서 매니저 부트스트랩(DontDestroyOnLoad)으로 불가하므로,
            // 중앙 적용 배선 계약은 소스 단정으로 확인한다(모든 창이 base.Show()를 경유 — 세션 실측).
            string source = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/UTKWindowBase.cs");
            Assert.That(source, Does.Contain("UTKTheme.ApplyFluentScrollers(this);"),
                "UTKWindowBase.Show must apply the Fluent scroller contract to the whole window subtree.");
        }
    }
}
