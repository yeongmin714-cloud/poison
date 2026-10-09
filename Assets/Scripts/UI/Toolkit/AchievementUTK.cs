// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 114
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U5 Round 2 — 업적(도전과제) 목록 창 포팅.
    /// 원본: Assets/Scripts/UI/AchievementSystem.cs (200줄, IMGUI) — 본 파일은 그 업적 데이터만
    /// UTKWindowBase 파생으로 이식한 별도 창. 원본 시스템은 절대 수정하지 않는다.
    ///
    /// [데이터 경로 — 원본 실측]
    ///  - 전체 목록: AchievementSystem.AllAchievements (static readonly AchievementDef[])
    ///              → id / title / description / icon (이모지)
    ///  - 달성 여부: AchievementSystem.Instance.IsUnlocked(id)
    ///  - 카운트:    Instance.GetUnlockedCount() / GetTotalCount()
    ///
    /// 폴링 400ms(schedule.Execute().Every, Pause 정지) + A키 토글 / ESC 닫기 (Updater).
    /// [AchieveUTK] 로그.
    /// </summary>
    public class AchievementUTK : UTKWindowBase
    {
        private static AchievementUTK _instance;

        private const float WinW = 480f;
        private const float WinH = 600f;
        private const long RefreshMs = 400L;

        private readonly ScrollView _list;
        private Label _statLabel;
        private IVisualElementScheduledItem _refreshTask;

        public static AchievementUTK Instance => _instance;

        /// <summary>팩토리 — 멱등 생성.</summary>
        public static void Ensure()
        {
            if (_instance == null)
            {
                _instance = new AchievementUTK();
                var go = new GameObject("AchievementUTK_UPD");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<Updater>().window = _instance;
                Debug.Log("[AchieveUTK] 인스턴스 생성 — A키로 토글");
            }
        }

        /// <summary>열기 (팩토리 겸용).</summary>
        public static void Open()
        {
            Ensure();
            _instance.Show();
        }

        /// <summary>토글(닫혀있으면 열고, 열려있으면 닫음).</summary>
        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Close(); return; }
            Open();
        }

        private AchievementUTK() : base("🏆 업적", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            // ── 달성 요약 헤더 카드 (GitHub-dark) ──
            var headerCard = new VisualElement { name = "AchievementSummaryCard" };
            headerCard.style.flexDirection = FlexDirection.Column;
            headerCard.style.backgroundColor = new StyleColor(new Color32(0x16, 0x1B, 0x22, 0xFF));
            headerCard.style.borderTopLeftRadius = headerCard.style.borderTopRightRadius = 8f;
            headerCard.style.borderBottomLeftRadius = headerCard.style.borderBottomRightRadius = 8f;
            headerCard.style.paddingLeft = 12f;
            headerCard.style.paddingRight = 12f;
            headerCard.style.paddingTop = 9f;
            headerCard.style.paddingBottom = 9f;
            headerCard.style.marginBottom = 8f;
            _content.Add(headerCard);

            _statLabel = new Label("");
            _statLabel.style.fontSize = 14.4f;
            _statLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _statLabel.style.color = new StyleColor(UTKColor.AccentRare);
            headerCard.Add(_statLabel);

            _list = new ScrollView { name = "AchievementList" };
            _list.style.flexGrow = 1f;
            _list.style.marginTop = 2f;
            _content.Add(_list);

            ApplyUIToolkitFont(this);
            style.display = DisplayStyle.None;
            style.left = 80f;
            style.top = 60f;
        }

        // ===== 생명주기 =====

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            StartRefreshLoop();
            RefreshDisplay();
            Debug.Log("[AchieveUTK] 업적 창 열림 (키: A)");
        }

        public override void Hide()
        {
            base.Hide();
            StopRefreshLoop();
            Debug.Log("[AchieveUTK] 업적 창 닫힘");
        }

        // ===== 폴링 루프 =====

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

        // ===== 데이터 갱신 — (원본 실측 경로) =====

        private void RefreshDisplay()
        {
            var sys = AchievementSystem.Instance;
            int unlocked = sys != null ? sys.GetUnlockedCount() : 0;
            int total = sys != null ? sys.GetTotalCount() : 0;
            if (_statLabel != null)
                _statLabel.text = $"📈 달성: {unlocked} / {total}";

            _list.Clear();
            if (sys == null)
            {
                _list.Add(new Label("업적 시스템을 사용할 수 없습니다."));
                return;
            }

            for (int i = 0; i < AchievementSystem.AllAchievements.Length; i++)
            {
                var def = AchievementSystem.AllAchievements[i];
                _list.Add(BuildAchievementBox(def, sys.IsUnlocked(def.id)));
            }

            Debug.Log($"[AchieveUTK] 업적 목록 갱신: {unlocked}/{total}");
        }

        private static VisualElement BuildAchievementBox(AchievementSystem.AchievementDef def, bool unlocked)
        {
            var box = new VisualElement { name = "AchievementCard" };
            box.style.flexDirection = FlexDirection.Column;
            box.style.backgroundColor = new StyleColor(new Color32(0x21, 0x26, 0x2D, 0xFF));
            box.style.borderTopColor = box.style.borderBottomColor = box.style.borderLeftColor = box.style.borderRightColor =
                new StyleColor(new Color32(0x2E, 0x34, 0x3D, 0xFF));
            box.style.borderTopWidth = box.style.borderBottomWidth = box.style.borderLeftWidth = box.style.borderRightWidth = 1f;
            box.style.borderTopLeftRadius = box.style.borderTopRightRadius = 8f;
            box.style.borderBottomLeftRadius = box.style.borderBottomRightRadius = 8f;
            box.style.marginTop = 4f;
            box.style.marginBottom = 4f;
            box.style.paddingLeft = 12f;
            box.style.paddingRight = 12f;
            box.style.paddingTop = 9f;
            box.style.paddingBottom = 9f;

            // 아이콘 + 제목 + 달성 표시
            var row1 = new VisualElement();
            row1.style.flexDirection = FlexDirection.Row;
            row1.style.alignItems = Align.Center;

            var icon = new Label(unlocked ? def.icon : "🔒");
            icon.style.fontSize = 21.6f;
            icon.style.width = 34f;
            row1.Add(icon);

            var title = new Label(string.IsNullOrEmpty(def.title) ? def.id : def.title);
            title.style.fontSize = 15.6f;
            title.style.flexGrow = 1f;
            title.style.color = new StyleColor(UTKColor.TextPrimary);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            row1.Add(title);

            var state = new Label(unlocked ? "달성" : "미달성");
            state.style.fontSize = 12f;
            state.style.color = new StyleColor(unlocked ? UTKColor.AccentRare : UTKColor.TextSecondary);
            state.style.width = 60f;
            state.style.unityTextAlign = TextAnchor.MiddleRight;
            row1.Add(state);

            box.Add(row1);

            // 설명
            var desc = new Label(string.IsNullOrEmpty(def.description) ? "" : def.description);
            desc.style.fontSize = 13.2f;
            desc.style.color = new StyleColor(UTKColor.TextSecondary);
            desc.style.whiteSpace = WhiteSpace.Normal;
            desc.style.marginLeft = 34f;
            desc.style.marginTop = 3f;
            box.Add(desc);

            Debug.Log($"[AchieveUTK] 항목: {def.id} ({def.title}) [{(unlocked ? "달성" : "미달성")}]");
            return box;
        }

        // ===== 키 토글 / ESC용 Updater =====

        private class Updater : MonoBehaviour
        {
            public AchievementUTK window;

            private void Update()
            {
                var root = UIToolkitBootstrap.UIRoot;
                if (root != null && window != null && window.parent == null)
                    root.Add(window);

                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && window != null)
                {
                    if (kb.aKey.wasPressedThisFrame)
                    {
                        if (window.IsOpen) window.Close(); else AchievementUTK.Open();
                    }
                    if (kb.escapeKey.wasPressedThisFrame && window.IsOpen) window.Close();
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
    }
}