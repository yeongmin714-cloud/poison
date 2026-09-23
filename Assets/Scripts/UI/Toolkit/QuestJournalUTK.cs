// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 114
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;          // QuestManager, PlayerStats
using ProjectName.Core.Data;     // QuestData, QuestState, QuestObjective
using ProjectName.UI;            // QuestRewardPreview

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U4 Round A-3 — 퀘스트 저널(일지) 포팅.
    /// 원본: Assets/Scripts/UI/QuestJournalUI.cs (627줄, IMGUI) — 본 파일은 그 탭 구조(진행/완료)와
    /// 보상 연동을 차용하되, 데이터 소스는 원본의 자체 관리 리스트가 아닌 살아있는 QuestManager
    /// 실데이터 경로로 연결한다. 원본은 절대 수정하지 않는다.
    ///
    /// [탭 구조 — 원본 실측]
    ///  원본 _activeTab: 0=Active(진행 중), 1=Completed(완료). DrawTabButton으로 전환.
    ///  - 진행 탭: QuestManager.GetActiveQuests()      → 목표 진행도(current/required) + 보상 요약
    ///  - 완료 탭: QuestManager.GetCompletedQuests()   → "✅ 완료" 표시 + 보상 요약
    ///  - 보상 요약: QuestRewardPreview.GetRewardSummary(QuestData)  (ProjectName.UI)
    ///
    /// 폴링 400ms(schedule.Execute().Every) + J 키 토글 / ESC 닫기.
    /// </summary>
    public class QuestJournalUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 부트스트랩 =====
        private static QuestJournalUTK _instance;
        public static QuestJournalUTK Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            _instance = new QuestJournalUTK();
            var go = new GameObject("QuestJournalUTK");
            Object.DontDestroyOnLoad(go);
            var updater = go.AddComponent<Updater>();
            updater.window = _instance;
            Debug.Log("[JournalUTK] 초기화 완료 — J키로 토글");
        }

        /// <summary>팩토리 — 멱등 생성.</summary>
        public static void Ensure()
        {
            if (_instance == null)
            {
                _instance = new QuestJournalUTK();
                var go = new GameObject("QuestJournalUTK_UPD");
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

        // ===== 설정 (원본 WINDOW_WIDTH/HEIGHT 근사) =====
        private const float WinW = 620f;
        private const float WinH = 520f;
        private const long RefreshMs = 400L;
        private const int TabActive = 0;
        private const int TabCompleted = 1;

        // =====================================================================
        //  [Figma GitHub-dark 리스타일] 퀘스트 저널 한정 인라인 오버라이드 — 기능 무수정, 시각 전용.
        //  Theme.uss / 공용 UTKButton·UTKWindowBase·타 UTK 창은 절대 수정하지 않는다.
        //  =====================================================================
        private static class GitHubDark
        {
            public static readonly Color Panel    = Hex(0x161B22);   // 창 본체 패널
            public static readonly Color PanelSub = Hex(0x21262D);   // 보조 패널(행/버튼)
            public static readonly Color Accent   = Hex(0x58A6FF);   // 강조(액센트) — 활성 탭/진행
            public static readonly Color Gold     = Hex(0xE3B341);   // 희귀/보상/골드
            public static readonly Color TextMain = Hex(0xF0F6FC);   // 기본 텍스트
            public static readonly Color TextSub  = Hex(0x8B949E);   // 보조 텍스트
            public static readonly Color Stroke   = Hex(0x2E343D);   // 테두리/구분선
            public static readonly Color Success  = Hex(0x3FB950);   // success 그린 — 완료

            private static Color Hex(uint rgb) =>
                new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);
        }

        /// <summary>GitHub-dark 버튼 인라인 오버라이드(이 창 한정) — IStyle 쇼트핸드 없음 → 4면 개별 대입.</summary>
        private static void StyleButton(Button btn, UTKButton.Variant variant)
        {
            if (btn == null) return;
            Color baseBg, hoverBg, textColor;
            switch (variant)
            {
                case UTKButton.Variant.Primary:
                    baseBg = GitHubDark.Accent; hoverBg = new Color32(0x79, 0xC0, 0xFF, 0xFF); textColor = GitHubDark.Panel; break;
                case UTKButton.Variant.Danger:
                    baseBg = new Color32(0xF8, 0x51, 0x49, 0xFF); hoverBg = new Color32(0xDA, 0x36, 0x33, 0xFF); textColor = GitHubDark.TextMain; break;
                default:
                    baseBg = GitHubDark.PanelSub; hoverBg = GitHubDark.Stroke; textColor = GitHubDark.TextMain; break;
            }

            btn.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            btn.style.backgroundColor = baseBg;
            btn.style.color = textColor;
            btn.style.borderTopWidth = btn.style.borderBottomWidth = btn.style.borderLeftWidth = btn.style.borderRightWidth = 1f;
            btn.style.borderTopColor = btn.style.borderBottomColor = btn.style.borderLeftColor = btn.style.borderRightColor = new StyleColor(baseBg);
            btn.style.borderTopLeftRadius = 6f;
            btn.style.borderTopRightRadius = 6f;
            btn.style.borderBottomLeftRadius = 6f;
            btn.style.borderBottomRightRadius = 6f;

            btn.RegisterCallback<PointerEnterEvent>(_ => btn.style.backgroundColor = hoverBg);
            btn.RegisterCallback<PointerLeaveEvent>(_ => btn.style.backgroundColor = baseBg);
        }

        /// <summary>창 크롬(본체/타이틀바/닫기버튼) GitHub-dark 리스타일 — 생성 시 1회.</summary>
        private void ApplyGitHubDarkStyle()
        {
            style.backgroundColor = GitHubDark.Panel;
            style.backgroundImage = new StyleBackground(StyleKeyword.None);
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 1f;
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = GitHubDark.Stroke;
            style.borderTopLeftRadius = 8f;
            style.borderTopRightRadius = 8f;
            style.borderBottomLeftRadius = 8f;
            style.borderBottomRightRadius = 8f;   // 메인 반경 r8
            style.color = GitHubDark.TextMain;   // 명시색 없는 라벨 상속색 — 기본 텍스트

            var titleBar = this.Q("TitleBar");
            if (titleBar != null)
            {
                titleBar.style.backgroundColor = GitHubDark.PanelSub;
                titleBar.style.borderTopLeftRadius = 8f;
                titleBar.style.borderTopRightRadius = 8f;
                titleBar.style.borderBottomWidth = 1f;
                titleBar.style.borderBottomColor = GitHubDark.Stroke;
            }

            if (_titleLabel != null)
                _titleLabel.style.color = GitHubDark.TextMain;

            var closeBtn = this.Q<Button>("CloseButton");
            if (closeBtn != null)
            {
                closeBtn.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                closeBtn.style.backgroundColor = GitHubDark.PanelSub;
                closeBtn.style.borderTopWidth = closeBtn.style.borderBottomWidth = closeBtn.style.borderLeftWidth = closeBtn.style.borderRightWidth = 0f;
                closeBtn.style.borderTopColor = closeBtn.style.borderBottomColor = closeBtn.style.borderLeftColor = closeBtn.style.borderRightColor = new StyleColor(GitHubDark.PanelSub);
                closeBtn.style.borderTopLeftRadius = 4f;
                closeBtn.style.borderTopRightRadius = 4f;
                closeBtn.style.borderBottomLeftRadius = 4f;
                closeBtn.style.borderBottomRightRadius = 4f;            // 작은배지 r4
                closeBtn.style.color = GitHubDark.TextMain;
            }
        }

        /// <summary>GitHub-dark 리스트 행(퀘스트 항목 박스) — 보조 패널 #21262D + 1px 스트로크 + r6 (이 창 한정).</summary>
        private static void ApplyDarkRowStyle(VisualElement row)
        {
            if (row == null) return;
            row.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            row.style.backgroundColor = GitHubDark.PanelSub;
            row.style.borderTopWidth = row.style.borderBottomWidth = row.style.borderLeftWidth = row.style.borderRightWidth = 1f;
            row.style.borderTopColor = row.style.borderBottomColor = row.style.borderLeftColor = row.style.borderRightColor = new StyleColor(GitHubDark.Stroke);
            row.style.borderTopLeftRadius = 6f;
            row.style.borderTopRightRadius = 6f;
            row.style.borderBottomLeftRadius = 6f;
            row.style.borderBottomRightRadius = 6f;   // 서브 반경 r6
        }

        // ===== 레퍼런스 =====
        private readonly VisualElement _tabBar;
        private Button _btnActive, _btnCompleted;
        private Label _statLabel;
        private readonly ScrollView _list;
        private int _activeTab = TabActive;
        private UnityEngine.UIElements.IVisualElementScheduledItem _refreshTask;

        private QuestJournalUTK() : base("📜 퀘스트 저널", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            // ── 통계 ──
            _statLabel = MkLabel("🔄 진행 중: 0개  |  ✅ 완료: 0개", 13, GitHubDark.TextSub, TextAnchor.MiddleRight);   // [GitHub-dark] 보조 텍스트
            _statLabel.style.marginBottom = 4f;
            _content.Add(_statLabel);

            // ── 탭 ──
            _tabBar = new VisualElement();
            _tabBar.name = "TabBar";
            _tabBar.style.flexDirection = FlexDirection.Row;
            _content.Add(_tabBar);

            _btnActive = UTKButton.Create("🔄 진행 중", () => SwitchTab(TabActive), UTKButton.Variant.Secondary);
            _btnCompleted = UTKButton.Create("✅ 완료", () => SwitchTab(TabCompleted), UTKButton.Variant.Secondary);
            StyleButton(_btnActive, UTKButton.Variant.Secondary);     // [GitHub-dark] 탭 버튼 인라인 리스타일
            StyleButton(_btnCompleted, UTKButton.Variant.Secondary);
            _tabBar.Add(_btnActive);
            _tabBar.Add(_btnCompleted);

            // ── 목록 ──
            _list = new ScrollView { name = "JournalList" };
            _list.style.flexGrow = 1f;
            _list.style.marginTop = 6f;
            _content.Add(_list);

            ApplyUIToolkitFont(this);
            ApplyGitHubDarkStyle();   // [GitHub-dark] 창 크롬 리스타일 — 이 창 한정 인라인
            style.display = DisplayStyle.None;
            style.left = 60f;
            style.top = 90f;
        }

        private void SwitchTab(int tab)
        {
            _activeTab = tab;
            StyleTab(_btnActive, _activeTab == TabActive);
            StyleTab(_btnCompleted, _activeTab == TabCompleted);
            RefreshDisplay();
            Debug.Log($"[JournalUTK] 탭 전환 → {(tab == TabActive ? "진행 중" : "완료")}");
        }

        private static void StyleTab(Button btn, bool active)
        {
            if (btn == null || btn.resolvedStyle == null) return;
            var c = active ? GitHubDark.Accent : GitHubDark.TextSub;   // [GitHub-dark] 활성 탭=액센트 / 비활성=보조
            btn.style.color = new StyleColor(c);
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
            StyleTab(_btnActive, _activeTab == TabActive);
            StyleTab(_btnCompleted, _activeTab == TabCompleted);
            StartRefreshLoop();
            RefreshDisplay();
            Debug.Log("[JournalUTK] 저널 열림 (키: J)");
        }

        public override void Hide()
        {
            base.Hide();
            StopRefreshLoop();
            Debug.Log("[JournalUTK] 저널 닫힘");
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
        //  데이터 갱신 — QuestManager 실데이터 경로
        // =====================================================================

        private void RefreshDisplay()
        {
            List<QuestData> active = QuestManager.GetActiveQuests();
            List<QuestData> completed = QuestManager.GetCompletedQuests();

            if (_statLabel != null)
                _statLabel.text = $"🔄 진행 중: {active.Count}개  |  ✅ 완료: {completed.Count}개";
            Debug.Log($"[JournalUTK] 목록 갱신(탭={(_activeTab == TabActive ? "진행" : "완료")}): 진행 {active.Count} / 완료 {completed.Count}");

            List<QuestData> filtered = _activeTab == TabActive ? active : completed;
            _list.Clear();

            if (filtered.Count == 0)
            {
                string emptyMsg = _activeTab == TabActive
                    ? "진행 중인 퀘스트가 없습니다.\nNPC를 찾아 퀘스트를 수락하세요."
                    : "완료된 퀘스트가 없습니다.\n퀘스트를 완료하면 여기에 표시됩니다.";
                var empty = MkLabel(emptyMsg, 14, GitHubDark.TextSub, TextAnchor.UpperLeft);   // [GitHub-dark] 보조 텍스트
                empty.style.flexGrow = 1f;
                empty.style.whiteSpace = WhiteSpace.Normal;
                _list.Add(empty);
                return;
            }

            for (int i = 0; i < filtered.Count; i++)
                _list.Add(BuildQuestEntry(filtered[i]));
        }

        private VisualElement BuildQuestEntry(QuestData quest)
        {
            var box = new VisualElement();
            box.AddToClassList("utk-slot");
            ApplyDarkRowStyle(box);   // [GitHub-dark] 항목 박스 = 보조 패널 리스트 아이템 (bg #21262D + 스트로크 + r6)
            box.style.flexDirection = FlexDirection.Column;
            box.style.marginTop = 4f;
            box.style.marginBottom = 4f;
            box.style.paddingTop = 6f;
            box.style.paddingBottom = 6f;

            // 이름 + 보상 요약(우측)
            var row1 = new VisualElement();
            row1.style.flexDirection = FlexDirection.Row;
            row1.style.alignItems = Align.Center;
            var nameL = MkLabel(quest.questName, 16, GitHubDark.TextMain, TextAnchor.MiddleLeft);   // [GitHub-dark] 기본 텍스트
            nameL.style.flexGrow = 1f;
            row1.Add(nameL);
            string reward = QuestRewardPreview.GetRewardSummary(quest);
            if (!string.IsNullOrEmpty(reward))
            {
                var rw = MkLabel(reward, 13, GitHubDark.Gold, TextAnchor.MiddleRight);   // [GitHub-dark] 보상 — 골드
                rw.style.width = 150f;
                row1.Add(rw);
            }
            box.Add(row1);

            // 설명
            if (!string.IsNullOrEmpty(quest.description))
            {
                var d = MkLabel(quest.description, 13, GitHubDark.TextSub, TextAnchor.MiddleLeft);   // [GitHub-dark] 보조 텍스트
                d.style.whiteSpace = WhiteSpace.Normal;
                box.Add(d);
            }

            if (_activeTab == TabCompleted)
            {
                var done = MkLabel("✅ 완료", 13, GitHubDark.Success, TextAnchor.MiddleLeft);   // [GitHub-dark] success 그린
                box.Add(done);
                return box;
            }

            // 진행 — 목표 진행도
            if (quest.objectives != null)
            {
                for (int i = 0; i < quest.objectives.Count; i++)
                {
                    QuestObjective obj = quest.objectives[i];
                    string objDesc = !string.IsNullOrEmpty(obj.description) ? obj.description : obj.type.ToString();
                    string progress = $"{obj.currentCount}/{obj.requiredCount}";
                    Color pc = obj.IsMet ? GitHubDark.Success : GitHubDark.Accent;   // [GitHub-dark] 달성=그린 / 진행=액센트
                    var row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Row;
                    var o = MkLabel($"▸ {objDesc}", 13, GitHubDark.TextMain, TextAnchor.MiddleLeft);   // [GitHub-dark] 기본 텍스트
                    o.style.flexGrow = 1f;
                    row.Add(o);
                    var p = MkLabel(progress, 13, pc, TextAnchor.MiddleRight);
                    p.style.width = 70f;
                    row.Add(p);
                    box.Add(row);
                }
            }

            Debug.Log($"[JournalUTK] 항목 렌더: {quest.questName} ({quest.questId})");
            return box;
        }

        // =====================================================================
        //  키 토글 / ESC용 Updater
        // =====================================================================

        private class Updater : MonoBehaviour
        {
            public QuestJournalUTK window;

            private void Update()
            {
                var root = UIToolkitBootstrap.UIRoot;
                if (root != null && window != null && window.parent == null)
                    root.Add(window);

                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null)
                {
                    if (kb.jKey.wasPressedThisFrame && window != null) if (window.IsOpen) window.Close(); else Toggle();;
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