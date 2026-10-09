using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U5 Round 1-C — 로딩 화면 오버레이 (UTK).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/Systems/LoadingScreenUI.cs (369줄 IMGUI) — 원본은 절대 수정하지 않는다.
    ///
    /// [구현]
    ///  ① LoadingManager.IsLoading/Progress 감시 (폴링) — 로딩 중이면 전체화면 오버레이 표시.
    ///  ② 진행바 — Progress 실측 폭 반영 (부드러운 보간), 진행률 % 텍스트.
    ///  ③ 팁 텍스트 — TipDatabase.GetTwoRandomTips() 실측 (카테고리 + 텍스트 2개).
    ///  ④ 셀프 부트스트랩 + Updater로 UIRoot 부착 (HotbarUIUTK 선례).
    ///  각 경로에 [LoadingUTK] UnityEngine.Debug 로그.
    /// [진입점] [RuntimeInitializeOnLoadMethod] 자동 부트스트랩 + Ensure().
    /// </summary>
    public class LoadingScreenUTK : VisualElement
    {
        // ===== 싱글턴 / 부트스트랩 =====
        private static LoadingScreenUTK _instance;
        public static LoadingScreenUTK Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            _instance = new LoadingScreenUTK();
            var go = new GameObject("LoadingScreenUTK");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Updater>().screen = _instance;
            Debug.Log("[LoadingUTK] 부트스트랩 완료 — 로딩 오버레이 준비");
        }

        public static void Ensure()
        {
            if (_instance != null) return;
            Bootstrap();
        }

        // ===== Figma GitHub-dark 로딩 카드 =====
        private const float BarW = 400f;
        private const float BarH = 12f;
        private static readonly Color CardColor = new Color32(0x16, 0x1B, 0x22, 0xFF);
        private static readonly Color StrokeColor = new Color32(0x2E, 0x34, 0x3D, 0xFF);
        private static readonly Color TextMainColor = new Color32(0xF0, 0xF6, 0xFC, 0xFF);
        private static readonly Color TextSubColor = new Color32(0x8B, 0x94, 0x9E, 0xFF);
        private static readonly Color AccentColor = new Color32(0x58, 0xA6, 0xFF, 0xFF);

        // ===== 레퍼런스 =====
        private readonly VisualElement _barFill;
        private readonly Label _pctLabel;
        private readonly Label _tipCatLabel;
        private readonly Label _tipTextLabel;
        private readonly Label _tip2TextLabel;
        private float _animatedProgress;
        private string _tipText1;
        private string _tipCat1;
        private string _tipText2;

        private LoadingScreenUTK()
        {
            name = "LoadingScreenUTK";
            AddToClassList("utk-window");
            pickingMode = PickingMode.Position;
            style.display = DisplayStyle.None;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;
            style.backgroundColor = new StyleColor(new Color32(0x0D, 0x11, 0x17, 0xF2));

            // 중앙 GitHub-dark 카드: 로딩 문구와 실제 진행률 게이지를 한 묶음으로 구성.
            var card = new VisualElement();
            card.name = "LoadingCard";
            card.style.width = 520f;
            card.style.maxWidth = new Length(90f, LengthUnit.Percent);
            card.style.backgroundColor = new StyleColor(CardColor);
            card.style.paddingTop = 28f;
            card.style.paddingBottom = 24f;
            card.style.paddingLeft = 30f;
            card.style.paddingRight = 30f;
            card.style.borderTopWidth = 1f;
            card.style.borderBottomWidth = 1f;
            card.style.borderLeftWidth = 1f;
            card.style.borderRightWidth = 1f;
            card.style.borderTopColor = new StyleColor(StrokeColor);
            card.style.borderBottomColor = new StyleColor(StrokeColor);
            card.style.borderLeftColor = new StyleColor(StrokeColor);
            card.style.borderRightColor = new StyleColor(StrokeColor);
            card.style.borderTopLeftRadius = 8f;
            card.style.borderTopRightRadius = 8f;
            card.style.borderBottomLeftRadius = 8f;
            card.style.borderBottomRightRadius = 8f;
            card.style.alignItems = Align.Center;
            Add(card);

            var logo = new Label("Crusader Kingdom");
            logo.style.fontSize = 31.2f;
            logo.style.unityFontStyleAndWeight = FontStyle.Bold;
            logo.style.color = new StyleColor(TextMainColor);
            card.Add(logo);

            var sub = new Label("⚔️ 크루세이더 킹덤");
            sub.style.color = new StyleColor(TextSubColor);
            sub.style.fontSize = 13.2f;
            sub.style.marginTop = 4f;
            sub.style.marginBottom = 26f;
            card.Add(sub);

            var progressRow = new VisualElement();
            progressRow.style.width = BarW;
            progressRow.style.maxWidth = new Length(100f, LengthUnit.Percent);
            progressRow.style.flexDirection = FlexDirection.Row;
            progressRow.style.justifyContent = Justify.SpaceBetween;
            progressRow.style.alignItems = Align.Center;
            progressRow.style.marginBottom = 8f;
            card.Add(progressRow);

            var loadingLabel = new Label("게임을 불러오는 중…");
            loadingLabel.style.color = new StyleColor(TextMainColor);
            loadingLabel.style.fontSize = 14.4f;
            progressRow.Add(loadingLabel);

            _pctLabel = new Label("0%");
            _pctLabel.style.color = new StyleColor(AccentColor);
            _pctLabel.style.fontSize = 14.4f;
            _pctLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            progressRow.Add(_pctLabel);

            // 실측 진행률로 갱신되는 게이지 — 보간/폴링 로직은 기존과 동일.
            var barBg = new VisualElement();
            barBg.name = "BarBackground";
            barBg.style.width = BarW;
            barBg.style.maxWidth = new Length(100f, LengthUnit.Percent);
            barBg.style.height = BarH;
            barBg.style.backgroundColor = new StyleColor(new Color32(0x21, 0x26, 0x2D, 0xFF));
            barBg.style.overflow = Overflow.Hidden;
            barBg.style.borderTopLeftRadius = 6f;
            barBg.style.borderTopRightRadius = 6f;
            barBg.style.borderBottomLeftRadius = 6f;
            barBg.style.borderBottomRightRadius = 6f;
            card.Add(barBg);

            _barFill = new VisualElement();
            _barFill.name = "BarFill";
            _barFill.style.width = 0f;
            _barFill.style.height = BarH;
            _barFill.style.backgroundColor = new StyleColor(AccentColor);
            _barFill.style.borderTopLeftRadius = 6f;
            _barFill.style.borderBottomLeftRadius = 6f;
            barBg.Add(_barFill);

            var tipTitle = new Label("게임 팁");
            tipTitle.style.color = new StyleColor(TextSubColor);
            tipTitle.style.fontSize = 12f;
            tipTitle.style.marginTop = 24f;
            tipTitle.style.alignSelf = Align.FlexStart;
            card.Add(tipTitle);

            _tipCatLabel = new Label("");
            _tipCatLabel.style.color = new StyleColor(AccentColor);
            _tipCatLabel.style.fontSize = 12f;
            _tipCatLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _tipCatLabel.style.marginTop = 8f;
            _tipCatLabel.style.whiteSpace = WhiteSpace.Normal;
            _tipCatLabel.style.alignSelf = Align.FlexStart;
            card.Add(_tipCatLabel);

            _tipTextLabel = new Label("");
            _tipTextLabel.style.color = new StyleColor(TextSubColor);
            _tipTextLabel.style.fontSize = 12f;
            _tipTextLabel.style.whiteSpace = WhiteSpace.Normal;
            _tipTextLabel.style.width = new Length(100f, LengthUnit.Percent);
            _tipTextLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _tipTextLabel.style.marginTop = 4f;
            card.Add(_tipTextLabel);

            _tip2TextLabel = new Label("");
            _tip2TextLabel.style.color = new StyleColor(TextSubColor);
            _tip2TextLabel.style.fontSize = 12f;
            _tip2TextLabel.style.whiteSpace = WhiteSpace.Normal;
            _tip2TextLabel.style.width = new Length(100f, LengthUnit.Percent);
            _tip2TextLabel.style.marginTop = 6f;
            card.Add(_tip2TextLabel);

            UTKWindowBase.ApplyUIToolkitFont(this);
        }

        // ===== 표시/숨김 =====

        private void ShowLoading()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;
            if (parent == null)
                root.Add(this);
            style.display = DisplayStyle.Flex;
            style.position = Position.Absolute;
            style.left = 0f;
            style.right = 0f;
            style.top = 0f;
            style.bottom = 0f;
            BringToFront();
            LoadTips();
            _animatedProgress = 0f;
            Debug.Log("[LoadingUTK] 로딩 오버레이 표시");
        }

        private void HideLoading()
        {
            style.display = DisplayStyle.None;
            _animatedProgress = 0f;
            Debug.Log("[LoadingUTK] 로딩 오버레이 숨김");
        }

        // ===== 로딩 갱신 (원본 Update 로직) =====

        private void Tick(LoadingManager manager)
        {
            _animatedProgress = Mathf.Lerp(_animatedProgress, manager.Progress, Time.deltaTime * 3f);
            if (Mathf.Abs(_animatedProgress - manager.Progress) < 0.001f)
                _animatedProgress = manager.Progress;

            float fill = Mathf.Clamp01(_animatedProgress);
            _barFill.style.width = BarW * fill;
            _pctLabel.text = $"{(fill * 100f):F0}%";
        }

        // ===== 팁 로드 (원본 TipDatabase 실측) =====

        private void LoadTips()
        {
            var (t1, c1, t2, _) = TipDatabase.GetTwoRandomTips();
            _tipText1 = t1;
            _tipCat1 = TipCategoryIcon(c1);
            _tipText2 = t2;
            _tipCatLabel.text = _tipCat1;
            _tipTextLabel.text = _tipText1;
            _tip2TextLabel.text = _tipText2;
            Debug.Log($"[LoadingUTK] 팁 로드: {_tipCat1}");
        }

        private string TipCategoryIcon(TipCategory cat)
        {
            switch (cat)
            {
                case TipCategory.Combat:   return "⚔️ Combat";
                case TipCategory.Strategy: return "🧠 Strategy";
                case TipCategory.Lore:     return "📖 Lore";
                default:                   return "🎮 Gameplay";
            }
        }

        // ===== 폴링 Updater — LoadingManager.IsLoading 감시 (HotbarUIUTK 선례) =====

        private class Updater : MonoBehaviour
        {
            public LoadingScreenUTK screen;
            private float _tick = 0.25f;

            private void Update()
            {
                if (screen == null) return;
                var mgr = LoadingManager.Instance;
                if (mgr == null) return;

                bool loading = mgr.IsLoading;
                if (loading && screen.parent == null)
                {
                    screen.ShowLoading();
                }
                else if (!loading && screen.parent != null)
                {
                    screen.HideLoading();
                }

                if (!loading) return;
                // 원본 Update — 진행률 보간 (폴링 주기)
                _tick -= Time.deltaTime;
                if (_tick <= 0f)
                {
                    _tick = 0.25f;
                    screen.Tick(mgr);
                }
            }
        }
    }
}