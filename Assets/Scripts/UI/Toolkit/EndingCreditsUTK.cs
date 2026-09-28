using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;   // GameStatsCollector (통계 요약용)

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U5 Round 2 — 엔딩 크레딧 전용 (전체화면 오버레이).
    /// 원본: Assets/Scripts/UI/EndingCreditsUI.cs (448줄, IMGUI) — 본 파일은 그것의 데이터만
    /// UTKWindowBase 파생으로 이식한 별도 파일. 원본은 절대 수정하지 않는다.
    ///
    /// [데이터 경로 — 원본 실측]
    ///  - 크레딧 라인:   EndingCreditsUI.CreditsLines 하드코딩 배열 (리치텍스트 태그 제거 후 표기)
    ///  - 스크롤 속도:   CREDITS_TOTAL_HEIGHT(1200f) / CREDITS_SCROLL_DURATION(15f) = 80 단위/초
    ///  - 통계 요약:     GameStatsCollector.* (FormatTime/FormatGold/FormatDistance + 정수 속성)
    ///  - 전환:         EndingText → CreditsScroll → StatsSummary → Choice (ESC는 즉시 Choice)
    ///
    /// 크레딧 롤은 스케줄 틱(400ms)마다 스크롤 카드 오프셋을 갱신하며, 진행 표시와 스킵 버튼을 제공한다.
    /// [CreditsUTK] 로그.
    /// </summary>
    public class EndingCreditsUTK : UTKWindowBase
    {
        private static EndingCreditsUTK _instance;

        // ===== 원본 하드코딩 크레딧 (태그 제거) =====
        private static readonly string[] CreditsLines = new string[]
        {
            "KOREA 1420", "",
            "— 제작진 —", "",
            "기획 및 디자인", "Nous Research Team", "",
            "프로그래밍", "Hermes Agent AI", "",
            "게임 디자인", "Project Hermes", "",
            "레벨 디자인", "AI Generative Design", "",
            "— 특별 감사 —", "",
            "모든 테스터분들께 감사드립니다", "피드백을 주신 모든 분들께 감사드립니다", "",
            "— 에셋 출처 —", "",
            "Unity Asset Store", "Open Source Libraries", "CC0 License Assets", "", "",
            "감사합니다!", "끝까지 플레이해 주셔서 감사합니다.",
        };

        private const float ScrollSpeed = 80f;        // 1200f / 15f (원본 SCROLL_SPEED)
        private const long TickMs = 400L;

        private readonly VisualElement _fullScreen;   // 절대배치 전체 오버레이 래퍼
        private readonly VisualElement _creditsHost;  // 스크롤 콘텐츠
        private readonly ScrollView _creditsScroll;
        private readonly VisualElement _progressFill;
        private IVisualElementScheduledItem _tickTask;
        private float _offset;
        private bool _running;

        public System.Action OnNewGamePlusClicked;
        public System.Action OnMainMenuClicked;

        public static EndingCreditsUTK Instance => _instance;

        private EndingCreditsUTK() : base("🌅 엔딩 크레딧", new Vector2(Screen.width, Screen.height))
        {
            // UTKWindowBase의 고정 폭/높이를 무시하고 전체화면 오버레이로 전환
            style.position = Position.Absolute;
            style.left = 0f; style.right = 0f; style.top = 0f; style.bottom = 0f;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;
            style.backgroundColor = new StyleColor(new Color32(0x0B, 0x0E, 0x14, 0xF2));
            style.display = DisplayStyle.None;
            pickingMode = PickingMode.Position;   // 뒤 클릭 차단

            _fullScreen = _content;               // UTKWindowBase 콘텐츠 영역 재활용
            _fullScreen.style.flexDirection = FlexDirection.Column;
            _fullScreen.style.alignItems = Align.Center;
            _fullScreen.style.justifyContent = Justify.Center;
            _fullScreen.style.flexGrow = 1f;
            _fullScreen.style.height = Length.Percent(100f);
            _fullScreen.style.paddingLeft = 24f;
            _fullScreen.style.paddingRight = 24f;
            _fullScreen.style.paddingTop = 20f;
            _fullScreen.style.paddingBottom = 20f;

            // GitHub-dark 크레딧 카드. 바깥은 #161B22, 스크롤 본문은 #21262D.
            var card = new VisualElement { name = "CreditsCard" };
            card.style.width = Length.Percent(78f);
            card.style.height = Length.Percent(88f);
            card.style.minWidth = 420f;
            card.style.maxWidth = 900f;
            card.style.flexDirection = FlexDirection.Column;
            card.style.backgroundColor = new StyleColor(new Color32(0x16, 0x1B, 0x22, 0xFF));
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius = 12f;
            card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 12f;
            card.style.borderLeftWidth = card.style.borderRightWidth = 1f;
            card.style.borderTopWidth = card.style.borderBottomWidth = 1f;
            card.style.borderLeftColor = card.style.borderRightColor = new StyleColor(new Color32(0x2E, 0x34, 0x3D, 0xFF));
            card.style.borderTopColor = card.style.borderBottomColor = new StyleColor(new Color32(0x2E, 0x34, 0x3D, 0xFF));
            card.style.paddingLeft = card.style.paddingRight = 24f;
            card.style.paddingTop = 20f;
            card.style.paddingBottom = 18f;
            Add(card);

            var heading = new Label("🌅 엔딩 크레딧") { name = "CreditsHeading" };
            heading.style.fontSize = 24f;
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.color = new StyleColor(UTKColor.AccentRare);
            heading.style.unityTextAlign = TextAnchor.MiddleCenter;
            heading.style.marginBottom = 4f;
            card.Add(heading);

            var subtitle = new Label("KOREA 1420 · 제작진과 함께한 여정") { name = "CreditsSubtitle" };
            subtitle.style.fontSize = 12f;
            subtitle.style.color = new StyleColor(UTKColor.TextSecondary);
            subtitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            subtitle.style.marginBottom = 14f;
            card.Add(subtitle);

            _creditsScroll = new ScrollView { name = "CreditsScroll" };
            _creditsScroll.mode = ScrollViewMode.Vertical;
            _creditsScroll.style.flexGrow = 1f;
            _creditsScroll.style.minHeight = 0f;
            _creditsScroll.style.backgroundColor = new StyleColor(new Color32(0x21, 0x26, 0x2D, 0xFF));
            _creditsScroll.style.borderTopLeftRadius = _creditsScroll.style.borderTopRightRadius = 8f;
            _creditsScroll.style.borderBottomLeftRadius = _creditsScroll.style.borderBottomRightRadius = 8f;
            _creditsScroll.style.paddingLeft = _creditsScroll.style.paddingRight = 16f;
            _creditsScroll.style.paddingTop = _creditsScroll.style.paddingBottom = 14f;
            _creditsScroll.style.flexShrink = 1f;
            card.Add(_creditsScroll);

            // 크레딧 호스트 (자동 롤은 ScrollView의 스크롤 오프셋으로 진행)
            _creditsHost = new VisualElement();
            _creditsHost.name = "CreditsHost";
            _creditsHost.style.flexDirection = FlexDirection.Column;
            _creditsHost.style.width = Length.Percent(100f);
            _creditsHost.style.alignItems = Align.Center;
            BuildCreditsLabels();
            _creditsScroll.Add(_creditsHost);

            var footer = new VisualElement { name = "CreditsFooter" };
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.alignItems = Align.Center;
            footer.style.marginTop = 14f;
            card.Add(footer);

            var progressTrack = new VisualElement { name = "CreditsProgressTrack" };
            progressTrack.style.flexGrow = 1f;
            progressTrack.style.height = 6f;
            progressTrack.style.marginRight = 14f;
            progressTrack.style.backgroundColor = new StyleColor(new Color32(0x30, 0x36, 0x3D, 0xFF));
            progressTrack.style.borderTopLeftRadius = progressTrack.style.borderTopRightRadius = 3f;
            progressTrack.style.borderBottomLeftRadius = progressTrack.style.borderBottomRightRadius = 3f;
            progressTrack.style.overflow = Overflow.Hidden;
            footer.Add(progressTrack);

            _progressFill = new VisualElement { name = "CreditsProgressFill" };
            _progressFill.style.width = Length.Percent(0f);
            _progressFill.style.height = Length.Percent(100f);
            _progressFill.style.backgroundColor = new StyleColor(UTKColor.AccentRare);
            progressTrack.Add(_progressFill);

            // 스킵 버튼
            var skipBtn = UTKButton.Create("⏭ 크레딧 건너뛰기", OnSkip, UTKButton.Variant.Primary);
            skipBtn.style.width = 180f;
            skipBtn.style.height = 42f;
            footer.Add(skipBtn);

            ApplyUIToolkitFont(this);
            style.display = DisplayStyle.None;
        }

        private void BuildCreditsLabels()
        {
            for (int i = 0; i < CreditsLines.Length; i++)
            {
                string line = CreditsLines[i];
                Label l = new Label(line);
                l.style.fontSize = 18f;
                l.style.unityTextAlign = TextAnchor.MiddleCenter;
                l.style.marginBottom = string.IsNullOrEmpty(line) ? 8f : 12f;
                l.style.color = new StyleColor(string.IsNullOrEmpty(line) ? UTKColor.TextSecondary : UTKColor.TextPrimary);
                if (line.StartsWith("—") || line == "KOREA 1420" || line == "감사합니다!")
                {
                    l.style.fontSize = line == "KOREA 1420" ? 26f : 20f;
                    l.style.unityFontStyleAndWeight = FontStyle.Bold;
                    l.style.color = new StyleColor(UTKColor.AccentRare);
                    l.style.marginTop = 8f;
                }
                else if (line == "Nous Research Team" || line == "Hermes Agent AI" || line == "Project Hermes" || line == "AI Generative Design")
                {
                    l.style.color = new StyleColor(UTKColor.TextSecondary);
                    l.style.fontSize = 16f;
                }
                _creditsHost.Add(l);
            }
        }

        // ===== 팩토리 =====

        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new EndingCreditsUTK();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null) root.Add(_instance);
            Debug.Log("[CreditsUTK] 엔딩 크레딧 인스턴스 생성");
        }

        /// <summary>크레딧 롤 시작 (전체화면 표시 + 틱 기동).</summary>
        public static void Open()
        {
            Ensure();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && _instance.parent == null)
                root.Add(_instance);
            _instance.StartRoll();
        }

        public static void CloseSequence()
        {
            if (_instance == null) return;
            _instance.StopRoll();
            _instance.style.display = DisplayStyle.None;
            Debug.Log("[CreditsUTK] 엔딩 크레딧 종료");
        }

        // ===== 롤 동작 =====

        private void StartRoll()
        {
            if (IsOpen) return;
            _running = true;
            _offset = 0f;
            _creditsScroll.scrollOffset = Vector2.zero;
            _creditsScroll.schedule.Execute(() => _creditsScroll.scrollOffset = Vector2.zero);
            _progressFill.style.width = Length.Percent(0f);
            style.display = DisplayStyle.Flex;
            BringToFront();
            StartTick();
            Debug.Log("[CreditsUTK] 크레딧 롤 시작");
        }

        private void StopRoll()
        {
            _running = false;
            StopTick();
        }

        private void StartTick()
        {
            if (_tickTask != null) return;
            _tickTask = schedule.Execute(() =>
            {
                if (!_running) return;
                _offset += ScrollSpeed * (TickMs / 1000f);
                _creditsScroll.scrollOffset = new Vector2(0f, _offset);
                _progressFill.style.width = Length.Percent(Mathf.Clamp01(_offset / 1200f) * 100f);
                Debug.Log($"[CreditsUTK] 롤 오프셋 {_offset:F0}");
            }).Every(TickMs);
        }

        private void StopTick()
        {
            if (_tickTask != null)
            {
                _tickTask.Pause();
                _tickTask = null;
            }
        }

        private void OnSkip()
        {
            Debug.Log("[CreditsUTK] 스킵 요청");
            CloseSequence();
        }

        // ===== 통계 요약 화면 표시 (선택지로 넘기기 전) =====
        private void ShowStatsSummary()
        {
            Debug.Log("[CreditsUTK] 통계 요약 화면");
        }
    }
}