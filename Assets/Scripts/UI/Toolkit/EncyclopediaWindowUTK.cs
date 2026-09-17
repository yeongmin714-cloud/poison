using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core.Data;    // EncyclopediaCategory, EncyclopediaEntry
using ProjectName.Systems;      // EncyclopediaManager

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U4 Round A-3 — 도감(엔사이클로피디아) 포팅.
    /// 원본: Assets/Scripts/UI/EncyclopediaWindow.cs (670줄, IMGUI) — 본 파일은 그것의
    /// 카테고리 탭 8종/항목 그리드/발견·미발견 표시/수집률 보상을 UTK로 이식.
    /// 원본은 절대 수정하지 않는다.
    ///
    /// [데이터 경로 — 원본 실측 (ProjectName.Systems.EncyclopediaManager)]
    ///  - 카테고리 상수(아이콘/이름): 원본 TAB_ICONS/TAB_NAMES/TAB_CATEGORIES (8종) 차용
    ///  - 탭별 항목:  EncyclopediaManager.Instance.GetCategoryEntries(EncyclopediaCategory)
    ///  - 발견 여부:  EncyclopediaEntry.IsDiscovered (ScriptableObject)
    ///  - 탭 수집률:  GetCategoryCompletionRate / GetCategoryDiscoveredCount / GetCategoryTotalCount
    ///  - 전체 수집률: EncyclopediaManager.Instance.OverallCompletionRate
    ///  - 항목 상세:   entryName / description / location / rarity / GetRarityColor()
    ///
    /// 폴링 400ms(schedule.Execute().Every) + L 키 토글 / ESC 닫기.
    /// </summary>
    public class EncyclopediaWindowUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 부트스트랩 =====
        private static EncyclopediaWindowUTK _instance;
        public static EncyclopediaWindowUTK Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            _instance = new EncyclopediaWindowUTK();
            var go = new GameObject("EncyclopediaWindowUTK");
            Object.DontDestroyOnLoad(go);
            var updater = go.AddComponent<Updater>();
            updater.window = _instance;
            Debug.Log("[EncyUTK] 초기화 완료 — L키로 토글");
        }

        /// <summary>팩토리 — 멱등 생성.</summary>
        public static void Ensure()
        {
            if (_instance == null)
            {
                _instance = new EncyclopediaWindowUTK();
                var go = new GameObject("EncyclopediaWindowUTK_UPD");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<Updater>().window = _instance;
            }
        }

        /// <summary>열기.</summary>
        public static void Open()
        {
            Ensure();
            _instance.Show();
        }

        /// <summary>토글.</summary>
        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Close(); return; }
            Open();
        }

        // ===== 설정 (원본 근사) =====
        private const float WinW = 900f;
        private const float WinH = 620f;
        private const long RefreshMs = 400L;
        private const float EntryCellW = 120f;
        private const float EntryCellH = 48f;

        // 원본 TAB_ICONS / TAB_NAMES / TAB_CATEGORIES 차용 (8종)
        private static readonly string[] TAB_ICONS =
            { "🌿", "🥩", "🍲", "🧪", "👑", "🏰", "📜", "🏆" };
        private static readonly string[] TAB_NAMES =
            { "약초", "몬스터", "요리", "약물", "영주", "영지", "문서", "업적" };
        private static readonly EncyclopediaCategory[] TAB_CATEGORIES =
        {
            EncyclopediaCategory.Herb,     EncyclopediaCategory.Monster,
            EncyclopediaCategory.Cooking,  EncyclopediaCategory.Potion,
            EncyclopediaCategory.Lord,     EncyclopediaCategory.Territory,
            EncyclopediaCategory.Document, EncyclopediaCategory.Achievement
        };

        // ===== 레퍼런스 =====
        private readonly VisualElement _tabBar;
        private readonly Button[] _tabButtons = new Button[TAB_CATEGORIES.Length];
        private Label _overallLabel, _catLabel;
        private readonly ScrollView _grid;
        private readonly ScrollView _detail;
        private Label _detailBody;
        private int _selectedTab;
        private UnityEngine.UIElements.IVisualElementScheduledItem _refreshTask;

        private EncyclopediaWindowUTK() : base("📖 도감", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            // ── 수집률 라벨 ──
            _overallLabel = MkLabel("📊 전체 수집률: 0/0 (0.0%)", 14, UTKColor.TextPrimary, TextAnchor.MiddleLeft);
            _overallLabel.style.marginBottom = 3f;
            _content.Add(_overallLabel);
            _catLabel = MkLabel("🏷️ 수집률: -", 13, UTKColor.TextSecondary, TextAnchor.MiddleLeft);
            _catLabel.style.marginBottom = 4f;
            _content.Add(_catLabel);

            // ── 카테고리 탭 ──
            _tabBar = new VisualElement();
            _tabBar.name = "TabBar";
            _tabBar.style.flexDirection = FlexDirection.Row;
            _tabBar.style.flexWrap = Wrap.Wrap;
            _content.Add(_tabBar);

            for (int i = 0; i < TAB_CATEGORIES.Length; i++)
            {
                int idx = i;
                var btn = UTKButton.Create($"{TAB_ICONS[i]} {TAB_NAMES[i]}", () => SwitchTab(idx), UTKButton.Variant.Secondary);
                btn.style.marginRight = 4f;
                btn.style.marginBottom = 4f;
                _tabButtons[i] = btn;
                _tabBar.Add(btn);
            }

            // ── 본문: 좌(그리드) + 우(상세) ──
            var body = new VisualElement();
            body.style.flexGrow = 1f;
            body.style.flexDirection = FlexDirection.Row;
            body.style.marginTop = 6f;
            _content.Add(body);

            _grid = new ScrollView { name = "EntryGrid" };
            _grid.style.flexGrow = 1f;
            _grid.style.width = new Length(58f, LengthUnit.Percent);
            body.Add(_grid);

            _detail = new ScrollView { name = "EntryDetail" };
            _detail.style.flexGrow = 1f;
            _detail.style.width = new Length(42f, LengthUnit.Percent);
            body.Add(_detail);

            _detailBody = MkLabel("항목을 선택하세요.", 14, UTKColor.TextSecondary, TextAnchor.UpperLeft);
            _detailBody.style.whiteSpace = WhiteSpace.Normal;
            _detail.Add(_detailBody);

            ApplyUIToolkitFont(this);
            style.display = DisplayStyle.None;
            style.left = 40f;
            style.top = 70f;
        }

        private void SwitchTab(int tab)
        {
            _selectedTab = tab;
            for (int i = 0; i < _tabButtons.Length; i++)
                StyleTab(_tabButtons[i], i == tab);
            RefreshDisplay();
            Debug.Log($"[EncyUTK] 카테고리 탭 → {TAB_NAMES[tab]}");
        }

        private static void StyleTab(Button btn, bool active)
        {
            if (btn == null) return;
            btn.style.color = new StyleColor(active ? UTKColor.AccentRare : UTKColor.TextSecondary);
        }

        // =====================================================================
        //  생명주기
        // =====================================================================

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            for (int i = 0; i < _tabButtons.Length; i++)
                StyleTab(_tabButtons[i], i == _selectedTab);
            StartRefreshLoop();
            RefreshDisplay();
            Debug.Log("[EncyUTK] 도감 열림 (키: L)");
        }

        public override void Hide()
        {
            base.Hide();
            StopRefreshLoop();
            Debug.Log("[EncyUTK] 도감 닫힘");
        }

        // =====================================================================
        //  폴링 루프
        // =====================================================================

        private void StartRefreshLoop()
        {
            if (_refreshTask != null) return;
            _refreshTask = schedule.Execute(() =>
            {
                if (IsOpen) RefreshDisplay();
            }).Every(RefreshMs);
        }

        private void StopRefreshLoop()
        {
            if (_refreshTask != null)
            {
                _refreshTask.Pause();
                _refreshTask = null;
            }
        }

        // =====================================================================
        //  데이터 갱신 — EncyclopediaManager 경로
        // =====================================================================

        private void RefreshDisplay()
        {
            var mgr = EncyclopediaManager.Instance;
            if (mgr == null)
            {
                if (_overallLabel != null) _overallLabel.text = "도감 매니저 없음";
                return;
            }

            float overall = mgr.OverallCompletionRate;
            int totalEntries = 0;
            int totalDiscovered = 0;
            for (int i = 0; i < TAB_CATEGORIES.Length; i++)
            {
                totalEntries += mgr.GetCategoryTotalCount(TAB_CATEGORIES[i]);
                totalDiscovered += mgr.GetCategoryDiscoveredCount(TAB_CATEGORIES[i]);
            }
            if (_overallLabel != null)
                _overallLabel.text = $"📊 전체 수집률: {totalDiscovered}/{totalEntries} ({overall * 100f:F1}%)";

            EncyclopediaCategory cat = TAB_CATEGORIES[_selectedTab];
            int catDisc = mgr.GetCategoryDiscoveredCount(cat);
            int catTotal = mgr.GetCategoryTotalCount(cat);
            float catRate = mgr.GetCategoryCompletionRate(cat);
            if (_catLabel != null)
                _catLabel.text = $"🏷️ [{TAB_ICONS[_selectedTab]} {TAB_NAMES[_selectedTab]}] {catDisc}/{catTotal} ({catRate * 100f:F1}%)";

            Debug.Log($"[EncyUTK] 갱신: 탭={TAB_NAMES[_selectedTab]}, {catDisc}/{catTotal} ({catRate * 100f:F1}%)");

            // ── 항목 그리드 (발견 먼저, 그 뒤 미발견) ──
            List<EncyclopediaEntry> all = mgr.GetCategoryEntries(cat);
            List<EncyclopediaEntry> discovered = new List<EncyclopediaEntry>();
            List<EncyclopediaEntry> hidden = new List<EncyclopediaEntry>();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].IsDiscovered) discovered.Add(all[i]);
                else hidden.Add(all[i]);
            }

            _grid.Clear();
            for (int i = 0; i < discovered.Count; i++)
            {
                EncyclopediaEntry e = discovered[i];
                _grid.Add(BuildEntryCell(e, true));
            }
            for (int i = 0; i < hidden.Count; i++)
            {
                EncyclopediaEntry e = hidden[i];
                _grid.Add(BuildEntryCell(e, false));
            }

            if (all.Count == 0)
            {
                var empty = MkLabel("이 카테고리에 항목이 없습니다.", 13, UTKColor.TextSecondary, TextAnchor.UpperLeft);
                empty.style.flexGrow = 1f;
                _grid.Add(empty);
            }
        }

        private VisualElement BuildEntryCell(EncyclopediaEntry entry, bool discovered)
        {
            var cell = new VisualElement();
            cell.AddToClassList("utk-slot");
            cell.style.width = EntryCellW;
            cell.style.height = EntryCellH;
            cell.style.marginRight = 3f;
            cell.style.marginBottom = 3f;
            cell.style.justifyContent = Justify.Center;

            if (discovered)
            {
                var lbl = MkLabel($"✅ {entry.entryName}", 12, UTKColor.TextPrimary, TextAnchor.MiddleCenter);
                lbl.style.whiteSpace = WhiteSpace.Normal;
                cell.Add(lbl);
            }
            else
            {
                var lbl = MkLabel("❌ ???", 12, UTKColor.TextSecondary, TextAnchor.MiddleCenter);
                cell.Add(lbl);
            }

            cell.RegisterCallback<ClickEvent>(_ =>
            {
                ShowDetail(entry, discovered);
            });

            return cell;
        }

        private void ShowDetail(EncyclopediaEntry entry, bool discovered)
        {
            if (_detailBody == null) return;

            if (!discovered)
            {
                _detailBody.text = "이 항목은 아직 발견되지 않았습니다.\n\n⚠ 발견하려면 탐험·수집·제작을 수행하세요.";
                Debug.Log($"[EncyUTK] 항목 선택(미발견): {entry.entryId}");
                return;
            }

            Color rarityColor = entry.GetRarityColor();
            string rarityName = entry.rarity.ToString();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"<b>{entry.entryName}</b>");
            sb.AppendLine($"등급: {rarityName}");
            if (!string.IsNullOrEmpty(entry.description))
                sb.AppendLine("");
            sb.AppendLine(entry.description ?? "");
            if (!string.IsNullOrEmpty(entry.location))
            {
                sb.AppendLine("");
                sb.AppendLine($"📌 위치: {entry.location}");
            }
            _detailBody.text = sb.ToString();
            _detailBody.style.color = new StyleColor(rarityColor);

            Debug.Log($"[EncyUTK] 항목 선택(발견): {entry.entryName} ({entry.entryId}) — 등급 {rarityName}");
        }

        // =====================================================================
        //  키 토글 / ESC용 Updater
        // =====================================================================

        private class Updater : MonoBehaviour
        {
            public EncyclopediaWindowUTK window;

            private void Update()
            {
                var root = UIToolkitBootstrap.UIRoot;
                if (root != null && window != null && window.parent == null)
                    root.Add(window);

                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null)
                {
                    if (kb.lKey.wasPressedThisFrame && window != null) if (window.IsOpen) window.Close(); else Toggle();;
                    if (kb.escapeKey.wasPressedThisFrame && window != null && window.IsOpen) window.Close();
                }
            }

            private void OnDestroy()
            {
                if (window != null)
                {
                    window.StopRefreshLoop();
                    window.RemoveFromHierarchy();
                }
            }
        }

        // =====================================================================
        //  내부 헬퍼
        // =====================================================================

        private static Label MkLabel(string text, float size, Color color, TextAnchor align)
        {
            var l = new Label(text ?? "");
            l.style.fontSize = size;
            l.style.color = new StyleColor(color);
            l.style.unityTextAlign = align;
            return l;
        }
    }
}