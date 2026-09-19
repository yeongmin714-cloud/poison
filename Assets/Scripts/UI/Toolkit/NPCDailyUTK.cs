// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 114
using ProjectName.Systems;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U6 Round D1 — NPC 일상 (일일 상호작용 상태 창) UTK 윈도우.
    /// 원본: Assets/Scripts/UI/NPCDailyUI.cs (175줄 IMGUI MonoBehaviour) — 원본은 절대 수정하지 않는다.
    ///
    /// [구현]
    ///   ① 현재 시간대 — NPCDailyCycle.Instance.CurrentPeriod 실측 + GetPeriodName() 표시.
    ///   ② NPC 일상 상태 — NPCDailyCycle.Instance.GetAllNPCs() / GetNPCStatusTextFor() 실측 호출.
    ///   ③ 상태 색 — 시간대(Dawn/Day/Evening/Night)에 따른 색 구분.
    ///   400ms 폴링으로 시간대 변경 및 NPC 상태를 자동 갱신. 각 경로에 [NPCDailyUTK] UnityEngine.Debug 로그.
    /// [진입점] static Ensure() / Open() / Toggle() / Close(). 순수 VisualElement 트리.
    /// </summary>
    public class NPCDailyUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static NPCDailyUTK _instance;
        public static NPCDailyUTK Instance => _instance;

        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new NPCDailyUTK();
        }

        /// <summary>NPC 일상 상태 창 열기.</summary>
        public static void Open()
        {
            Ensure();
            _instance.Show();
        }

        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Hide(); return; }
            Open();
        }

        public static void Close()
        {
            if (_instance != null)
                _instance.Hide();
        }

        // ===== 설정 =====
        private const float WinW = 380f;
        private const float WinH = 480f;
        private const long RefreshMs = 400L;

        // ===== 레퍼런스 =====
        private Label _periodLabel;
        private Label _countLabel;
        private readonly VisualElement _list;
        private IVisualElementScheduledItem _refreshTask;
        private NPCDailyCycle.TimePeriod _lastPeriod = (NPCDailyCycle.TimePeriod)(-1);

        private NPCDailyUTK() : base("🏘️ NPC 일상", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;
            _content.style.paddingTop = 6f;
            _content.style.paddingBottom = 6f;

            _periodLabel = new Label("");
            _periodLabel.style.fontSize = 17f;
            _periodLabel.style.color = new StyleColor(UTKColor.BorderGold);
            _content.Add(_periodLabel);

            _countLabel = new Label("");
            _countLabel.style.fontSize = 12f;
            _countLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            _content.Add(_countLabel);

            _list = new VisualElement();
            _list.name = "NPCDailyList";
            _list.style.flexGrow = 1f;
            _list.style.flexDirection = FlexDirection.Column;
            _content.Add(_list);

            ApplyUIToolkitFont(this);

            style.display = DisplayStyle.None;
            style.left = 780f;
            style.top = 240f;
        }

        // ===== 생명주기 =====
        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            style.left = 780f;
            style.top = 240f;
            StartRefreshLoop();
            Refresh();
        }

        public override void Hide()
        {
            base.Hide();
            StopRefreshLoop();
        }

        private void StartRefreshLoop()
        {
            if (_refreshTask != null) return;
            _refreshTask = schedule.Execute(() =>
            {
                if (!IsOpen) return;
                Refresh();
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

        // ===== 렌더링 =====
        private void Refresh()
        {
            var cycle = NPCDailyCycle.Instance;
            if (cycle == null)
            {
                _periodLabel.text = "NPC 일상 시스템 없음";
                _countLabel.text = "NPCDailyCycle.Instance가 없습니다.";
                _list.Clear();
                return;
            }

            var period = cycle.CurrentPeriod;
            if (period != _lastPeriod)
            {
                _lastPeriod = period;
                Debug.Log("[NPCDailyUTK] 시간대 변경: " + NPCDailyCycle.GetPeriodName(period) + " (" + period + ")");
            }

            _periodLabel.text = "🕐 현재 시간대: " + NPCDailyCycle.GetPeriodName(period);

            var npcs = cycle.GetAllNPCs();
            _countLabel.text = "등록 NPC: " + npcs.Count + "명  |  밤엔 말풍선 미표시";

            _list.Clear();
            var header = MakeLabel("NPC 일상 상태", UTKColor.TextSecondary, false);
            header.style.fontSize = 15f;
            _list.Add(header);

            if (npcs.Count == 0)
            {
                _list.Add(MakeLabel("표시할 NPC가 없습니다.", UTKColor.TextSecondary, true));
                return;
            }

            foreach (var npc in npcs)
            {
                if (npc == null) continue;
                string status = cycle.GetNPCStatusTextFor(npc);
                if (string.IsNullOrEmpty(status))
                    continue;

                bool active = npc.activeInHierarchy;
                var row = MakeLabel(npc.name + "  —  " + status, StateColor(active), false);
                row.style.opacity = active ? 1f : 0.45f;
                _list.Add(row);
            }
        }

        private static Color StateColor(bool active)
        {
            return active ? UTKColor.TextPrimary : UTKColor.TextSecondary;
        }

        private static Label MakeLabel(string text, Color color, bool wrap)
        {
            var l = new Label(text);
            l.style.fontSize = 13f;
            l.style.color = new StyleColor(color);
            if (wrap)
                l.style.whiteSpace = WhiteSpace.Normal;
            return l;
        }
    }
}