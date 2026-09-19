// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 114
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;   // GameStatsCollector

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U5 Round 2 — 게임 통계 창 포팅.
    /// 원본: Assets/Scripts/UI/GameStatsWindow.cs (405줄, IMGUI) — 본 파일은 그것의 데이터 경로만
    /// UTKWindowBase 파생으로 이식한 별도 파일. 원본은 절대 수정하지 않는다.
    ///
    /// [데이터 경로 — 원본 실측]
    ///  - 모든 항목: GameStatsCollector 정적 속성 + Format 헬퍼
    ///    ⏱ 플레이타임:   FormatTime(PlayTime)
    ///    ⚔ 전투:         Kills / Deaths
    ///    🏆 퀘스트/탐험: CompletedQuests / FormatDistance(DistanceTraveled)
    ///    👑 영지/전쟁:   OwnedTerritories / WarParticipations / CompletedRevenge
    ///    🏟 아레나:      ArenaWins / ArenaLosses / ArenaBestStreak / ArenaTotal
    ///    🐟 수집:        FishCaught
    ///    💰 경제:        FormatGold(GoldEarned) / FormatGold(GoldSpent) / FormatGold(GoldNet)
    ///
    /// 폴링 400ms(schedule.Execute().Every, Pause 정지) + U키 토글 / ESC 닫기 (Updater).
    /// [GameStatsUTK] 로그.
    /// </summary>
    public class GameStatsUTK : UTKWindowBase
    {
        private static GameStatsUTK _instance;

        private const float WinW = 460f;
        private const float WinH = 600f;
        private const long RefreshMs = 400L;

        private readonly ScrollView _list;
        private IVisualElementScheduledItem _refreshTask;

        public static GameStatsUTK Instance => _instance;

        /// <summary>팩토리 — 멱등 생성.</summary>
        public static void Ensure()
        {
            if (_instance == null)
            {
                _instance = new GameStatsUTK();
                var go = new GameObject("GameStatsUTK_UPD");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<Updater>().window = _instance;
                Debug.Log("[GameStatsUTK] 인스턴스 생성 — U키로 토글");
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

        private GameStatsUTK() : base("📊 게임 통계", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            _list = new ScrollView { name = "StatsList" };
            _list.style.flexGrow = 1f;
            _list.style.marginTop = 6f;
            _content.Add(_list);

            ApplyUIToolkitFont(this);
            style.display = DisplayStyle.None;
            style.left = 60f;
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
            Debug.Log("[GameStatsUTK] 통계 창 열림 (키: U)");
        }

        public override void Hide()
        {
            base.Hide();
            StopRefreshLoop();
            Debug.Log("[GameStatsUTK] 통계 창 닫힘");
        }

        // ===== 폴링 루프 (schedule.Execute().Every, Pause 정지) =====

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

            // ⏱ 플레이타임
            _list.Add(SectionHeader("⏱️ 플레이타임", "플레이타임"));
            _list.Add(StatRow("총 플레이 시간", GameStatsCollector.FormatTime(GameStatsCollector.PlayTime)));

            // ⚔ 전투
            _list.Add(SectionHeader("⚔️ 전투", "전투"));
            _list.Add(StatRow("처치한 몬스터", GameStatsCollector.Kills.ToString("N0")));
            _list.Add(StatRow("사망 횟수", GameStatsCollector.Deaths.ToString("N0")));

            // 🏆 퀘스트 & 탐험
            _list.Add(SectionHeader("🏆 퀘스트 & 탐험", "퀘스트 & 탐험"));
            _list.Add(StatRow("완료한 퀘스트", GameStatsCollector.CompletedQuests.ToString("N0")));
            _list.Add(StatRow("이동 거리", GameStatsCollector.FormatDistance(GameStatsCollector.DistanceTraveled)));

            // 👑 영지 & 전쟁
            _list.Add(SectionHeader("👑 영지 & 전쟁", "영지 & 전쟁"));
            _list.Add(StatRow("점령 영지", GameStatsCollector.OwnedTerritories.ToString("N0")));
            _list.Add(StatRow("전쟁 참여", GameStatsCollector.WarParticipations.ToString("N0")));
            _list.Add(StatRow("암살한 영주", GameStatsCollector.CompletedRevenge.ToString("N0")));

            // 🏟 아레나
            _list.Add(SectionHeader("🏟️ 아레나", "아레나"));
            _list.Add(StatRow("승리", GameStatsCollector.ArenaWins.ToString("N0")));
            _list.Add(StatRow("패배", GameStatsCollector.ArenaLosses.ToString("N0")));
            _list.Add(StatRow("최고 연승", GameStatsCollector.ArenaBestStreak.ToString("N0")));
            string arenaTotal = GameStatsCollector.ArenaTotal > 0
                ? $"{GameStatsCollector.ArenaWins} / {GameStatsCollector.ArenaTotal}"
                : "0 / 0";
            _list.Add(StatRow("전적", arenaTotal));

            // 🐟 수집
            _list.Add(SectionHeader("🐟 수집", "수집"));
            _list.Add(StatRow("획득 물고기", GameStatsCollector.FishCaught.ToString("N0")));

            // 💰 경제
            _list.Add(SectionHeader("💰 경제", "경제"));
            _list.Add(StatRow("획득 골드", GameStatsCollector.FormatGold(GameStatsCollector.GoldEarned)));
            _list.Add(StatRow("사용 골드", GameStatsCollector.FormatGold(GameStatsCollector.GoldSpent)));
            _list.Add(StatRow("순수익", GameStatsCollector.FormatGold(GameStatsCollector.GoldNet)));

            // 닫기 버튼
            var closeBtn = UTKButton.Create("닫기 (ESC)", () => Close(), UTKButton.Variant.Secondary);
            closeBtn.style.marginTop = 10f;
            closeBtn.style.alignSelf = Align.Center;
            _list.Add(closeBtn);

            Debug.Log("[GameStatsUTK] 통계 목록 갱신 완료");
        }

        private static VisualElement SectionHeader(string iconTitle, string _ignored)
        {
            var h = new Label(iconTitle);
            h.style.fontSize = 16f;
            h.style.unityFontStyleAndWeight = FontStyle.Bold;
            h.style.color = new StyleColor(UTKColor.AccentMagic);
            h.style.marginTop = 8f;
            h.style.marginBottom = 2f;
            return h;
        }

        private static VisualElement StatRow(string label, string value)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 2f;

            var l = new Label(label);
            l.style.fontSize = 14f;
            l.style.color = new StyleColor(UTKColor.TextSecondary);
            l.style.flexGrow = 1f;
            row.Add(l);

            var v = new Label(value);
            v.style.fontSize = 14f;
            v.style.color = new StyleColor(UTKColor.GuildGreen);
            v.style.unityTextAlign = TextAnchor.MiddleRight;
            v.style.width = 150f;
            row.Add(v);

            return row;
        }

        // ===== 키 토글 / ESC용 Updater =====

        private class Updater : MonoBehaviour
        {
            public GameStatsUTK window;

            private void Update()
            {
                var root = UIToolkitBootstrap.UIRoot;
                if (root != null && window != null && window.parent == null)
                    root.Add(window);

                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && window != null)
                {
                    if (kb.uKey.wasPressedThisFrame)
                    {
                        if (window.IsOpen) window.Close(); else GameStatsUTK.Open();
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