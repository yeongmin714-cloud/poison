using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U6 Round D1 — 축제 UI (진행/참여/보상) UTK 윈도우.
    /// 원본: Assets/Scripts/UI/FestivalUI.cs (402줄 IMGUI static) — 원본은 절대 수정하지 않는다.
    ///
    /// [구현]
    ///   ① 진행 중 축제 목록 — FestivalManager.Instance.ActiveFestivals 실측 API 직접 사용.
    ///   ② 참여/정보 패널 — ShowFestivalInfo()로 선택한 축제 상세 (territoryId, Day 기간, 시간대, 효과).
    ///   ③ 보상(효과) — FestivalData.GetEffect().GetSummary() 실측 호출.
    ///   시작 알림 — FestivalManager.OnFestivalStarted 구독 → 12초 자동 종료 알림 표시 (400ms 폴링).
    ///   각 경로에 [FestivalUTK] UnityEngine.Debug 로그.
    /// [진입점] static Ensure() / Open() / ShowFestivalInfo(festival) / Toggle() / Close(). 순수 VisualElement 트리.
    /// </summary>
    public class FestivalUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static FestivalUTK _instance;
        public static FestivalUTK Instance => _instance;

        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new FestivalUTK();
        }

        /// <summary>축제 정보/참여 패널 열기.</summary>
        public static void Open()
        {
            Ensure();
            _instance.Show();
        }

        /// <summary>특정 축제 정보 표시 (MapWindow 등 외부 호출, 원본 ShowFestivalInfo 대응).</summary>
        public static void ShowFestivalInfo(FestivalData festival)
        {
            if (festival == null)
            {
                Debug.LogWarning("[FestivalUTK] ShowFestivalInfo: festival이 null");
                return;
            }
            Ensure();
            _instance._selected = festival;
            _instance._notification = null;
            _instance._notificationTimer = 0f;
            Debug.Log("[FestivalUTK] 축제 정보 표시: " + festival.festivalName + " (territory=" + festival.territoryId + ")");
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

        /// <summary>축제 시작 알림 구독 (원본 Initialize 대응).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            FestivalManager.OnFestivalStarted += OnFestivalStartedStatic;
        }

        private static void OnFestivalStartedStatic(FestivalData festival)
        {
            if (festival == null) return;
            if (_instance == null) Ensure();
            _instance._notification = festival;
            _instance._notificationTimer = NotifySeconds;
            Debug.Log("[FestivalUTK] 🎉 축제 시작 알림: " + festival.emoji + " " + festival.festivalName + " at " + festival.territoryId);
            _instance.Show();
        }

        // ===== 설정 =====
        private const float WinW = 460f;
        private const float WinH = 540f;
        private const long RefreshMs = 400L;
        private const float NotifySeconds = 12f;

        // ===== 상태 =====
        private FestivalData _selected;
        private FestivalData _notification;      // 시작 알림용 임시 축제
        private float _notificationTimer;

        // ===== 레퍼런스 =====
        private readonly VisualElement _list;
        private readonly VisualElement _detail;
        private Label _notifyLabel;
        private IVisualElementScheduledItem _refreshTask;

        private FestivalUTK() : base("🎪 축제", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;
            _content.style.paddingTop = 6f;
            _content.style.paddingBottom = 6f;

            // 시작 알림 배너
            _notifyLabel = new Label("");
            _notifyLabel.AddToClassList("utk-slot--hover");
            _notifyLabel.style.fontSize = 14f;
            _notifyLabel.style.color = new StyleColor(UTKColor.BorderGold);
            _notifyLabel.style.display = DisplayStyle.None;
            _content.Add(_notifyLabel);

            _list = new VisualElement();
            _list.name = "FestivalList";
            _list.style.flexDirection = FlexDirection.Column;
            _content.Add(_list);

            _detail = new VisualElement();
            _detail.name = "FestivalDetail";
            _detail.style.flexGrow = 1f;
            _detail.style.flexDirection = FlexDirection.Column;
            _detail.style.marginTop = 8f;
            _content.Add(_detail);

            ApplyUIToolkitFont(this);

            style.display = DisplayStyle.None;
            style.left = 760f;
            style.top = 220f;
        }

        // ===== 구독 =====
        // 이벤트 구독은 [RuntimeInitializeOnLoadMethod] Initialize()에서 처리.

        // ===== 생명주기 =====
        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            style.left = 760f;
            style.top = 220f;
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
                if (_notification != null && _notificationTimer > 0f)
                {
                    _notificationTimer -= RefreshMs / 1000f;
                    if (_notificationTimer <= 0f)
                    {
                        _notification = null;
                        _notifyLabel.style.display = DisplayStyle.None;
                    }
                }
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
            // 알림 배너 갱신
            if (_notification != null)
            {
                _notifyLabel.text = _notification.emoji + " " + _notification.festivalName +
                    " — " + _notification.territoryId + " (잠시 후 자동 종료)";
                _notifyLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _notifyLabel.style.display = DisplayStyle.None;
            }

            // 진행 중 축제 목록
            var mgr = FestivalManager.Instance;
            DrawDetect(mgr);

            // 상세 패널 (선택된 축제 or 첫 활성 축제)
            if (_selected == null && mgr != null && mgr.ActiveFestivals.Count > 0)
                _selected = mgr.ActiveFestivals[0];
            DrawDetail();
        }

        private void DrawDetect(FestivalManager mgr)
        {
            _list.Clear();
            var title = MakeLabel("🎪 진행 중인 축제", UTKColor.BorderGold, false);
            title.style.fontSize = 15f;
            _list.Add(title);

            if (mgr == null || mgr.ActiveFestivals.Count == 0)
            {
                _list.Add(MakeLabel("현재 진행 중인 축제가 없습니다.", UTKColor.TextSecondary, true));
                return;
            }

            foreach (var festival in mgr.ActiveFestivals)
            {
                var btn = UTKButton.Create(festival.emoji + " " + festival.festivalName + "  (" + festival.territoryId + ")",
                    () =>
                    {
                        _selected = festival;
                        Refresh();
                    }, UTKButton.Variant.Secondary);
                _list.Add(btn);
            }
        }

        private void DrawDetail()
        {
            _detail.Clear();
            if (_selected == null)
            {
                _detail.Add(MakeLabel("축제를 선택하면 세부 정보가 표시됩니다.", UTKColor.TextSecondary, true));
                return;
            }

            var f = _selected;
            var title = MakeLabel(f.emoji + " " + f.festivalName, UTKColor.TextPrimary, false);
            title.style.fontSize = 17f;
            _detail.Add(title);

            _detail.Add(MakeLabel("📍 영지: " + f.territoryId, UTKColor.TextSecondary, false));
            _detail.Add(MakeLabel("📅 기간: Day " + f.startDay + " ~ " + f.endDay, UTKColor.TextSecondary, false));
            _detail.Add(MakeLabel("⏰ 시간대: " + f.startHour + ":00 ~ " + f.endHour + ":00", UTKColor.TextSecondary, false));

            if (!string.IsNullOrEmpty(f.description))
                _detail.Add(MakeLabel("📝 " + f.description, UTKColor.TextPrimary, true));

            // 보상/효과
            string effect = f.GetEffect().GetSummary();
            var eff = MakeLabel("✨ 효과: " + effect, new Color(0.6f, 1f, 0.6f), true);
            _detail.Add(eff);

            var closeBtn = UTKButton.Create("닫기", () => { _selected = null; Close(); }, UTKButton.Variant.Primary);
            _detail.Add(closeBtn);
        }

        // ===== 헬퍼 =====
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