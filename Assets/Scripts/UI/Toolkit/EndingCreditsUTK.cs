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
    /// 크레딧 롤은 스케줄 틱(400ms)마다 translate.Y 감소 = 자동 위로 스크롤 + 스킵 버튼.
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
        private readonly VisualElement _creditsHost;  // translate 이동 대상
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
            style.backgroundColor = new StyleColor(new Color(0.02f, 0.02f, 0.02f, 0.97f));
            style.display = DisplayStyle.None;
            pickingMode = PickingMode.Position;   // 뒤 클릭 차단

            _fullScreen = _content;               // UTKWindowBase 콘텐츠 영역 재활용
            _fullScreen.style.paddingTop = 60f;
            _fullScreen.style.flexDirection = FlexDirection.Column;
            _fullScreen.style.alignItems = Align.Center;

            // 크레딧 호스트 (translate 대상)
            _creditsHost = new VisualElement();
            _creditsHost.name = "CreditsHost";
            _creditsHost.style.position = Position.Absolute;
            _creditsHost.style.left = 60f; _creditsHost.style.right = 60f;
            _creditsHost.style.top = 0f;
            _creditsHost.style.alignItems = Align.Center;
            BuildCreditsLabels();
            Add(_creditsHost);

            // 스킵 버튼 (하단 고정)
            var skipBtn = UTKButton.Create("⏭ 크레딧 건너뛰기", OnSkip, UTKButton.Variant.Primary);
            skipBtn.style.position = Position.Absolute;
            skipBtn.style.bottom = 24f;
            skipBtn.style.right = 24f;
            skipBtn.style.width = 180f;
            skipBtn.style.height = 42f;
            Add(skipBtn);

            ApplyUIToolkitFont(this);
            style.display = DisplayStyle.None;
        }

        private void BuildCreditsLabels()
        {
            for (int i = 0; i < CreditsLines.Length; i++)
            {
                string line = CreditsLines[i];
                Label l = new Label(line);
                l.style.fontSize = 20f;
                l.style.unityTextAlign = TextAnchor.MiddleCenter;
                l.style.marginBottom = 14f;
                l.style.color = new StyleColor(UTKColor.TextPrimary);
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
            _creditsHost.style.translate = new Translate(0f, 0f);
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
                _creditsHost.style.translate = new Translate(0f, -_offset);
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