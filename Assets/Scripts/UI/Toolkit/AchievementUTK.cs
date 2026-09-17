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

            _statLabel = new Label("");
            _statLabel.style.fontSize = 14f;
            _statLabel.style.color = new StyleColor(UTKColor.AccentRare);
            _statLabel.style.marginBottom = 4f;
            _content.Add(_statLabel);

            _list = new ScrollView { name = "AchievementList" };
            _list.style.flexGrow = 1f;
            _list.style.marginTop = 4f;
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
            var box = new VisualElement();
            box.AddToClassList("utk-slot");
            box.style.flexDirection = FlexDirection.Column;
            box.style.marginTop = 3f;
            box.style.marginBottom = 3f;
            box.style.paddingTop = 5f;
            box.style.paddingBottom = 5f;

            // 아이콘 + 제목 + 달성 표시
            var row1 = new VisualElement();
            row1.style.flexDirection = FlexDirection.Row;
            row1.style.alignItems = Align.Center;

            var icon = new Label(unlocked ? def.icon : "🔒");
            icon.style.fontSize = 22f;
            icon.style.width = 34f;
            row1.Add(icon);

            var title = new Label(string.IsNullOrEmpty(def.title) ? def.id : def.title);
            title.style.fontSize = 15f;
            title.style.flexGrow = 1f;
            title.style.color = new StyleColor(unlocked ? UTKColor.AccentRare : UTKColor.TextSecondary);
            row1.Add(title);

            var state = new Label(unlocked ? "달성" : "미달성");
            state.style.fontSize = 12f;
            state.style.color = new StyleColor(unlocked ? UTKColor.GuildGreen : UTKColor.TextSecondary);
            state.style.width = 60f;
            state.style.unityTextAlign = TextAnchor.MiddleRight;
            row1.Add(state);

            box.Add(row1);

            // 설명
            var desc = new Label(string.IsNullOrEmpty(def.description) ? "" : def.description);
            desc.style.fontSize = 13f;
            desc.style.color = new StyleColor(UTKColor.TextSecondary);
            desc.style.whiteSpace = WhiteSpace.Normal;
            desc.style.marginLeft = 34f;
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