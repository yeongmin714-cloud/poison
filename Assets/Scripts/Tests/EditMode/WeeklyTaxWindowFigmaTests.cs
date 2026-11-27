using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ProjectName.UI.Toolkit;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [144차 P7-WTX #34] 주간 세금 창 계약테스트 — 피그마 243:3 "영지별 주간 세금 패널" 624×984 구조 정합.
    /// 구조 위주 어서션(위조 데이터 금지 — 실측 데이터 값 자체는 단정하지 않는다: 보유 영지 수/징수액은 런타임 상태).
    /// 선례: SoldierManagementFigma69Tests(UIDocument/UTKWindowManager Updater 리플렉션 스왑).
    /// </summary>
    public class WeeklyTaxWindowFigmaTests
    {
        private static readonly BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
        private GameObject _documentObject;
        private UIDocument _previousDocument;
        private GameObject _windowManagerObject;
        private Component _previousUpdater;
        private bool _previousWarnedNoUpdater;

        [SetUp]
        public void SetUp()
        {
            typeof(WeeklyTaxWindowUTK).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
                ?.SetValue(null, null);
            var managerFlags = StaticPrivate;
            var updaterType = typeof(UTKWindowManager).GetNestedType("Updater", managerFlags);
            var updaterField = typeof(UTKWindowManager).GetField("_updater", managerFlags);
            var warnedField = typeof(UTKWindowManager).GetField("_warnedNoUpdater", managerFlags);
            _previousUpdater = (Component)updaterField.GetValue(null);
            _previousWarnedNoUpdater = (bool)warnedField.GetValue(null);
            if (_previousUpdater == null) warnedField.SetValue(null, false);
            _windowManagerObject = new GameObject("WeeklyTaxFigmaWindowManager");
            updaterField.SetValue(null, _windowManagerObject.AddComponent(updaterType));
            _previousDocument = (UIDocument)typeof(UIToolkitBootstrap)
                .GetField("_document", StaticPrivate).GetValue(null);
            _documentObject = new GameObject("WeeklyTaxFigmaTestDocument");
            var document = _documentObject.AddComponent<UIDocument>();
            document.panelSettings = Resources.Load<PanelSettings>("UI/PanelSettings");
            typeof(UIToolkitBootstrap).GetField("_document", StaticPrivate).SetValue(null, document);
        }

        [TearDown]
        public void TearDown()
        {
            var instance = WeeklyTaxWindowUTK.Instance;
            if (instance != null)
            {
                if (instance.IsOpen)
                    instance.Hide();
                instance.RemoveFromHierarchy();
            }
            typeof(WeeklyTaxWindowUTK).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
                ?.SetValue(null, null);
            typeof(UIToolkitBootstrap).GetField("_document", StaticPrivate).SetValue(null, _previousDocument);
            typeof(UTKWindowManager).GetField("_updater", StaticPrivate).SetValue(null, _previousUpdater);
            typeof(UTKWindowManager).GetField("_warnedNoUpdater", StaticPrivate).SetValue(null, _previousWarnedNoUpdater);
            if (_windowManagerObject != null) Object.DestroyImmediate(_windowManagerObject);
            if (_documentObject != null) Object.DestroyImmediate(_documentObject);
        }

        [UnityTest]
        public IEnumerator Open_BuildsFigma243WindowStructure()
        {
            yield return null;
            var canvasRoot = UIToolkitBootstrap.UIRoot;
            Assert.That(canvasRoot, Is.SameAs(_documentObject.GetComponent<UIDocument>().rootVisualElement));
            canvasRoot.style.width = 1920f;
            canvasRoot.style.height = 1080f;
            yield return null;

            WeeklyTaxWindowUTK.Open();
            yield return null;
            var window = WeeklyTaxWindowUTK.Instance;
            Assert.That(window, Is.Not.Null, "Open must register a live window instance.");
            Assert.That(window.IsOpen, Is.True);
            Assert.That(window.IsFrameless, Is.True, "Figma 243:3 frame must render frameless chrome (RecommendedTaxWindowUTK 선례).");

            // ── 제목 243:17/18 ──
            var title = window.Q<Label>("WeeklyTaxTitle");
            var subtitle = window.Q<Label>("WeeklyTaxSubtitle");
            Assert.That(title, Is.Not.Null);
            Assert.That(title.text, Is.EqualTo("주간 세금"));
            Assert.That(title.style.fontSize.value.value, Is.EqualTo(21.6f).Within(0.01f));
            Assert.That(subtitle, Is.Not.Null);
            Assert.That(subtitle.text, Is.EqualTo("영지별 세금 징수 현황"));
            Assert.That(subtitle.style.fontSize.value.value, Is.EqualTo(13.2f).Within(0.01f));

            // ── 모서리 데칼 4코너 12×12(UTKTheme.AddCornerDecals — 243:4/7/10/13) ──
            Assert.That(window.Q("DecalTL"), Is.Not.Null);
            Assert.That(window.Q("DecalTR"), Is.Not.Null);
            Assert.That(window.Q("DecalBL"), Is.Not.Null);
            Assert.That(window.Q("DecalBR"), Is.Not.Null);

            // ── 필터 탭 243:19-25 — 3탭 188.8×38.4, 활성 탭 액센트 fill ──
            string[] tabTexts = { "전체 영지", "성·도시", "마을" };
            for (int i = 0; i < 3; i++)
            {
                var tab = window.Q<Button>($"FilterTab_{i}");
                Assert.That(tab, Is.Not.Null, $"Filter tab {i} must exist.");
                Assert.That(tab.text, Is.EqualTo(tabTexts[i]));
                AssertRawRect(tab, 188.8f, 38.4f);
            }
            var tab0 = window.Q<Button>("FilterTab_0");
            AssertFillColor(tab0.style.backgroundColor, 0.345f, 0.651f, 1f, 1f,
                "Default active tab must use the Figma 243:20 accent fill (#58A6FF).");

            // ── 영지 세금 목록 243:26 + 행 구조 ──
            var list = window.Q("TerritoryTaxList");
            Assert.That(list, Is.Not.Null);
            AssertRawRect(list, 576f, 662.4f);
            var row0 = window.Q("TerritoryRow_0");
            Assert.That(row0, Is.Not.Null, "The list must render at least one row (real data or '—' placeholder).");
            AssertRawRect(row0, 576f, 86.4f);
            var gradeBar = window.Q("GradeBar_0");
            Assert.That(gradeBar, Is.Not.Null);
            AssertRawRect(gradeBar, 4.8f, 86.4f);
            var emblem = window.Q("Emblem_0");
            Assert.That(emblem, Is.Not.Null);
            AssertRawRect(emblem, 52.8f, 52.8f);
            Assert.That(window.Q<Label>("WeeklyAmount_0"), Is.Not.Null, "Row must expose its weekly amount label.");
            Assert.That(window.Q<Label>("WeeklyCycle_0").text, Is.EqualTo("매주 징수"), "243:38 weekly cycle label.");
            Assert.That(window.Q<Label>("TaxItems_0"), Is.Not.Null, "Row must expose its tax-item label ('—' when no API).");
            var detailButton = window.Q<Button>("DetailButton_0");
            Assert.That(detailButton, Is.Not.Null);
            AssertRawRect(detailButton, 60f, 36f);
            Assert.That(detailButton.text, Is.EqualTo("상세"), "243:40 button text.");

            // ── 주간 세금 요약 243:125-133 ──
            var summary = window.Q("TaxSummary");
            Assert.That(summary, Is.Not.Null);
            AssertRawRect(summary, 576f, 114.4f);
            Assert.That(window.Q<Label>("TaxScheduleLabel").text, Is.EqualTo("매주 월요일 00:00 자동 징수"), "243:127 schedule text.");
            var countLabel = window.Q<Label>("TaxCountLabel");
            Assert.That(countLabel, Is.Not.Null);
            Assert.That(countLabel.text, Does.Match(@"^전체 \d+개 영지$|^—$"),
                "243:128 count label must be live data ('전체 {n}개 영지') or '—' when no source — no fabricated numbers.");
            var totalBox = window.Q("TaxTotalBox");
            Assert.That(totalBox, Is.Not.Null);
            AssertRawRect(totalBox, 576f, 67.6f);
            Assert.That(totalBox.style.backgroundColor.value.a, Is.EqualTo(0.078f).Within(0.005f),
                "243:129 total box fill must be #58A6FF@0.078.");
            Assert.That(totalBox.style.borderTopWidth.value, Is.EqualTo(1.2f).Within(0.01f),
                "243:129 total box stroke width must be 1.2.");
            Assert.That(window.Q<Label>("TaxTotalLabel").text, Is.EqualTo("전체 주간 세금 합계"), "243:131 total title.");
            var totalValue = window.Q<Label>("TaxTotalValue");
            Assert.That(totalValue, Is.Not.Null);
            Assert.That(totalValue.style.fontSize.value.value, Is.EqualTo(26.4f).Within(0.01f),
                "243:133 total value must render at 26.4 Geist Mono scale.");
        }

        [UnityTest]
        public IEnumerator SelectFilter_TogglesActiveTabVisualOnly()
        {
            yield return null;
            WeeklyTaxWindowUTK.Open();
            yield return null;
            var window = WeeklyTaxWindowUTK.Instance;
            Assert.That(window, Is.Not.Null);

            window.SelectFilter(1);   // "성·도시" 선택
            yield return null;

            var tab0 = window.Q<Button>("FilterTab_0");
            var tab1 = window.Q<Button>("FilterTab_1");
            var tab2 = window.Q<Button>("FilterTab_2");

            // 활성(243:20) fill/stroke #58A6FF — 비활성(243:22/24) fill #13181F
            AssertFillColor(tab1.style.backgroundColor, 0.345f, 0.651f, 1f, 1f, "Selected tab must take the active accent fill.");
            AssertFillColor(tab0.style.backgroundColor, 0.075f, 0.094f, 0.122f, 1f,
                "Deselected tab must fall back to the Figma 243:22 inactive fill (#13181F).");
            AssertFillColor(tab2.style.backgroundColor, 0.075f, 0.094f, 0.122f, 1f, "Third tab must fall back to the inactive fill.");
        }

        // ── 공용 어서션 헬퍼 ──
        private static void AssertRawRect(VisualElement element, float width, float height)
        {
            Assert.That(element.style.width.value.value, Is.EqualTo(width).Within(0.01f),
                $"{element.name} raw width must match the Figma truth.");
            Assert.That(element.style.height.value.value, Is.EqualTo(height).Within(0.01f),
                $"{element.name} raw height must match the Figma truth.");
        }

        private static void AssertFillColor(StyleColor styleColor, float r, float g, float b, float a, string message)
        {
            Color c = styleColor.value;
            Assert.That(c.r, Is.EqualTo(r).Within(0.005f), message);
            Assert.That(c.g, Is.EqualTo(g).Within(0.005f), message);
            Assert.That(c.b, Is.EqualTo(b).Within(0.005f), message);
            Assert.That(c.a, Is.EqualTo(a).Within(0.005f), message);
        }
    }
}
