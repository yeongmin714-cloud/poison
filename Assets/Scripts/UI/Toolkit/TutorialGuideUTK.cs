// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 114
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;          // TutorialGuideData (상수/FindById)
using ProjectName.Core.Data;     // GuideEntry
using ProjectName.Systems;       // TutorialGuideSystem

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U5 Round 2 — 튜토리얼 가이드 목록 창 포팅.
    /// 원본: Assets/Scripts/Systems/TutorialGuideSystem.cs (266줄, IMGUI) — 본 파일은 그 가이드
    /// 데이터만 UTKWindowBase 파생으로 이식한 별도 가이드북 창. 원본 시스템은 절대 수정하지 않는다.
    ///
    /// [데이터 경로 — 원본 실측]
    ///  - 기본 조작(BasicGuides) 11종 + 영지(TerritoryGuides) 10종 — ID는 TutorialGuideData.ID_* 상수
    ///  - 완료/확인 여부: PlayerPrefs.HasKey("guide_{id}")  (TutorialGuideSystem.HasGuideBeenShown)
    ///  - 전체 가이드 재구성: TutorialGuideData.AllGuides / FindById 로 보강
    ///
    /// 폴링 400ms(schedule.Execute().Every, Pause 정지) + T키 토글 / ESC 닫기 (Updater).
    /// [TutoUTK] 로그.
    /// </summary>
    public class TutorialGuideUTK : UTKWindowBase
    {
        private static TutorialGuideUTK _instance;

        private const float WinW = 520f;
        private const float WinH = 620f;
        private const long RefreshMs = 400L;

        private readonly ScrollView _list;
        private Label _statLabel;
        private IVisualElementScheduledItem _refreshTask;

        // ===== ID 그룹 (원본 BasicGuides/TerritoryGuides의 id 목록) =====
        private static readonly string[] BasicIds = new string[]
        {
            TutorialGuideData.ID_01_MOVEMENT, TutorialGuideData.ID_02_CAMERA,
            TutorialGuideData.ID_03_ATTACK, TutorialGuideData.ID_04_DASH,
            TutorialGuideData.ID_05_ROLL, TutorialGuideData.ID_06_CHOP_TREE,
            TutorialGuideData.ID_07_MINE_STONE, TutorialGuideData.ID_08_HERB_PICK,
            TutorialGuideData.ID_09_INVENTORY, TutorialGuideData.ID_10_CRAFT,
            TutorialGuideData.ID_11_RECIPE_BOOK,
        };

        private static readonly string[] TerritoryIds = new string[]
        {
            TutorialGuideData.ID_12_GUARD_INTERACT, TutorialGuideData.ID_13_GUARD_INFO,
            TutorialGuideData.ID_14_GUARD_EQUIP, TutorialGuideData.ID_15_GASMASK,
            TutorialGuideData.ID_16_GAS_SPRAYER, TutorialGuideData.ID_17_GUARD_MISSION,
            TutorialGuideData.ID_18_SHOP, TutorialGuideData.ID_19_WORLD_MAP,
            TutorialGuideData.ID_20_STATUS, TutorialGuideData.ID_22_BUILDING_ENTER,
        };

        public static TutorialGuideUTK Instance => _instance;

        /// <summary>팩토리 — 멱등 생성.</summary>
        public static void Ensure()
        {
            if (_instance == null)
            {
                _instance = new TutorialGuideUTK();
                var go = new GameObject("TutorialGuideUTK_UPD");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<Updater>().window = _instance;
                Debug.Log("[TutoUTK] 인스턴스 생성 — T키로 토글");
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

        private TutorialGuideUTK() : base("📖 튜토리얼 가이드", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            _statLabel = new Label("");
            _statLabel.style.fontSize = 14f;
            _statLabel.style.color = new StyleColor(UTKColor.GuildGreen);
            _statLabel.style.marginBottom = 4f;
            _content.Add(_statLabel);

            _list = new ScrollView { name = "GuideList" };
            _list.style.flexGrow = 1f;
            _list.style.marginTop = 4f;
            _content.Add(_list);

            ApplyUIToolkitFont(this);
            style.display = DisplayStyle.None;
            style.left = 100f;
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
            Debug.Log("[TutoUTK] 튜토리얼 창 열림 (키: T)");
        }

        public override void Hide()
        {
            base.Hide();
            StopRefreshLoop();
            Debug.Log("[TutoUTK] 튜토리얼 창 닫힘");
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
            _list.Clear();

            // 통계
            int done = 0;
            int total = BasicIds.Length + TerritoryIds.Length;
            foreach (var id in BasicIds)
                if (HasShown(id)) done++;
            foreach (var id in TerritoryIds)
                if (HasShown(id)) done++;
            if (_statLabel != null)
                _statLabel.text = $"📈 확인 완료: {done} / {total} 가이드";

            _list.Add(GroupHeader("🎮 기본 조작"));
            foreach (var id in BasicIds)
                _list.Add(BuildGuideBox(id, "basic"));
            _list.Add(GroupHeader("🏰 영지"));
            foreach (var id in TerritoryIds)
                _list.Add(BuildGuideBox(id, "territory"));

            Debug.Log($"[TutoUTK] 가이드 목록 갱신: 완료 {done}/{total}");
        }

        private static bool HasShown(string id)
        {
            return PlayerPrefs.HasKey("guide_" + id);
        }

        private VisualElement BuildGuideBox(string id, string group)
        {
            // title/desc 재구성: TutorialGuideData.AllGuides → 개별 FindById
            string title = id;
            string desc = "";
            var entry = TutorialGuideData.FindById(id);
            if (entry.HasValue)
            {
                if (!string.IsNullOrEmpty(entry.Value.Title)) title = entry.Value.Title;
                if (!string.IsNullOrEmpty(entry.Value.Description)) desc = entry.Value.Description;
            }

            bool done = HasShown(id);

            var box = new VisualElement();
            box.AddToClassList("utk-slot");
            box.style.flexDirection = FlexDirection.Column;
            box.style.marginTop = 3f;
            box.style.marginBottom = 3f;
            box.style.paddingTop = 5f;
            box.style.paddingBottom = 5f;

            var row1 = new VisualElement();
            row1.style.flexDirection = FlexDirection.Row;
            row1.style.alignItems = Align.Center;

            var step = new Label(done ? "✅" : "⬜");
            step.style.fontSize = 16f;
            step.style.width = 26f;
            row1.Add(step);

            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 15f;
            titleLabel.style.flexGrow = 1f;
            titleLabel.style.color = new StyleColor(done ? UTKColor.TextPrimary : UTKColor.TextSecondary);
            row1.Add(titleLabel);

            var state = new Label(done ? "완료" : "미확인");
            state.style.fontSize = 12f;
            state.style.color = new StyleColor(done ? UTKColor.GuildGreen : UTKColor.TextSecondary);
            state.style.width = 56f;
            state.style.unityTextAlign = TextAnchor.MiddleRight;
            row1.Add(state);

            box.Add(row1);

            if (!string.IsNullOrEmpty(desc))
            {
                var descLabel = new Label(desc);
                descLabel.style.fontSize = 13f;
                descLabel.style.color = new StyleColor(UTKColor.TextSecondary);
                descLabel.style.whiteSpace = WhiteSpace.Normal;
                descLabel.style.marginLeft = 26f;
                box.Add(descLabel);
            }

            Debug.Log($"[TutoUTK] 항목 [{group}]: {id} ({title}) [{(done ? "완료" : "미확인")}]");
            return box;
        }

        private static VisualElement GroupHeader(string text)
        {
            var h = new Label(text);
            h.style.fontSize = 16f;
            h.style.unityFontStyleAndWeight = FontStyle.Bold;
            h.style.color = new StyleColor(UTKColor.AccentMagic);
            h.style.marginTop = 8f;
            h.style.marginBottom = 2f;
            return h;
        }

        // ===== 키 토글 / ESC용 Updater =====

        private class Updater : MonoBehaviour
        {
            public TutorialGuideUTK window;

            private void Update()
            {
                var root = UIToolkitBootstrap.UIRoot;
                if (root != null && window != null && window.parent == null)
                    root.Add(window);

                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && window != null)
                {
                    if (kb.tKey.wasPressedThisFrame)
                    {
                        if (window.IsOpen) window.Close(); else TutorialGuideUTK.Open();
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