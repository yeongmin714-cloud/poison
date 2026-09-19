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
            _statLabel = MkLabel("🔄 진행 중: 0개  |  ✅ 완료: 0개", 13, UTKColor.TextSecondary, TextAnchor.MiddleRight);
            _statLabel.style.marginBottom = 4f;
            _content.Add(_statLabel);

            // ── 탭 ──
            _tabBar = new VisualElement();
            _tabBar.name = "TabBar";
            _tabBar.style.flexDirection = FlexDirection.Row;
            _content.Add(_tabBar);

            _btnActive = UTKButton.Create("🔄 진행 중", () => SwitchTab(TabActive), UTKButton.Variant.Secondary);
            _btnCompleted = UTKButton.Create("✅ 완료", () => SwitchTab(TabCompleted), UTKButton.Variant.Secondary);
            _tabBar.Add(_btnActive);
            _tabBar.Add(_btnCompleted);

            // ── 목록 ──
            _list = new ScrollView { name = "JournalList" };
            _list.style.flexGrow = 1f;
            _list.style.marginTop = 6f;
            _content.Add(_list);

            ApplyUIToolkitFont(this);
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
            var c = active ? UTKColor.AccentRare : UTKColor.TextSecondary;
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
                var empty = MkLabel(emptyMsg, 14, UTKColor.TextSecondary, TextAnchor.UpperLeft);
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
            box.style.flexDirection = FlexDirection.Column;
            box.style.marginTop = 4f;
            box.style.marginBottom = 4f;
            box.style.paddingTop = 6f;
            box.style.paddingBottom = 6f;

            // 이름 + 보상 요약(우측)
            var row1 = new VisualElement();
            row1.style.flexDirection = FlexDirection.Row;
            row1.style.alignItems = Align.Center;
            var nameL = MkLabel(quest.questName, 16, UTKColor.TextPrimary, TextAnchor.MiddleLeft);
            nameL.style.flexGrow = 1f;
            row1.Add(nameL);
            string reward = QuestRewardPreview.GetRewardSummary(quest);
            if (!string.IsNullOrEmpty(reward))
            {
                var rw = MkLabel(reward, 13, UTKColor.AccentRare, TextAnchor.MiddleRight);
                rw.style.width = 150f;
                row1.Add(rw);
            }
            box.Add(row1);

            // 설명
            if (!string.IsNullOrEmpty(quest.description))
            {
                var d = MkLabel(quest.description, 13, UTKColor.TextSecondary, TextAnchor.MiddleLeft);
                d.style.whiteSpace = WhiteSpace.Normal;
                box.Add(d);
            }

            if (_activeTab == TabCompleted)
            {
                var done = MkLabel("✅ 완료", 13, UTKColor.GuildGreen, TextAnchor.MiddleLeft);
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
                    Color pc = obj.IsMet ? UTKColor.GuildGreen : UTKColor.AccentMagic;
                    var row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Row;
                    var o = MkLabel($"▸ {objDesc}", 13, UTKColor.TextPrimary, TextAnchor.MiddleLeft);
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