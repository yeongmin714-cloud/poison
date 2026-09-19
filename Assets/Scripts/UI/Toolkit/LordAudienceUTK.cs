// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 114
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U6 Round A — 영주 알현 윈도우 (UTK).
    /// 원본: Assets/Scripts/UI/LordAudienceUI.cs (241줄 IMGUI) — 원본은 절대 수정하지 않는다.
    ///
    /// [구현]
    ///  ① 대화 선택지 — AudienceOption 실측 사용 (text / speechDifficulty / successResult / failResult / onSuccess / onFail).
    ///  ② 화술 판정 — PlayerStats.Instance.Level >= speechDifficulty (0 = 자동 성공, 원본 로직 대응).
    ///  ③ 결과 — 성공/실패 Result 표시(색상 구분) + onSuccess/onFail 콜백 호출, 3초 후 선택지 재표시(400ms 폴링).
    ///  각 경로에 [LordUTK] UnityEngine.Debug 로그.
    /// [진입점] static Open(lordName, lordTitle, options) / Ensure() / Toggle(). 순수 VisualElement 트리.
    /// </summary>
    public class LordAudienceUTK : UTKWindowBase
    {
        /// <summary>영주 대화 선택지 데이터 (원본 LordAudienceUI.AudienceOption 대응).</summary>
        public class AudienceOption
        {
            public string text;
            public int speechDifficulty;
            public string successResult;
            public string failResult;
            public System.Action onSuccess;
            public System.Action onFail;
        }

        // ===== 싱글턴 / 팩토리 =====
        private static LordAudienceUTK _instance;
        public static LordAudienceUTK Instance => _instance;

        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new LordAudienceUTK();
        }

        /// <summary>영주 알현 열기 (원본 LordAudienceUI Show/OnShow 대응).</summary>
        public static void Open(string lordName, string lordTitle, AudienceOption[] options)
        {
            Ensure();
            _instance.BeginAudience(lordName, lordTitle, options);
        }

        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Close(); return; }
            Ensure();
        }

        // ===== 설정 =====
        private const float WinW = 720f;
        private const float WinH = 600f;
        private const long RefreshMs = 400L;
        private const float ResultDisplaySec = 3f;
        private const int MaxOptions = 4;

        // ===== 상태 =====
        private string _lordName;
        private string _lordTitle;
        private AudienceOption[] _options;
        private bool _showOptions = true;
        private string _dialogueText = "";

        // ===== 레퍼런스 =====
        private Label _headlineLabel;
        private Label _dialogueLabel;
        private readonly VisualElement _list;
        private UnityEngine.UIElements.IVisualElementScheduledItem _refreshTask;

        private LordAudienceUTK() : base("👑 영주 알현", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            _headlineLabel = new Label("👑 영주 알현");
            _headlineLabel.AddToClassList("utk-title-label");
            _headlineLabel.style.fontSize = 18f;
            _content.Add(_headlineLabel);

            _dialogueLabel = new Label("");
            _dialogueLabel.style.fontSize = 16f;
            _dialogueLabel.style.color = new StyleColor(UTKColor.TextPrimary);
            _dialogueLabel.style.whiteSpace = WhiteSpace.Normal;
            _dialogueLabel.style.marginTop = 8f;
            _dialogueLabel.style.marginBottom = 8f;
            _content.Add(_dialogueLabel);

            _list = new VisualElement();
            _list.name = "LordOptionList";
            _list.style.flexGrow = 1f;
            _list.style.flexDirection = FlexDirection.Column;
            _content.Add(_list);

            ApplyUIToolkitFont(this);

            style.display = DisplayStyle.None;
            style.left = 600f;
            style.top = 100f;
        }

        // =================== 생명주기 ===================

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            style.left = 600f;
            style.top = 100f;
            StartRefreshLoop();
        }

        public override void Hide()
        {
            base.Hide();
            StopRefreshLoop();
            Debug.Log("[LordUTK] 알현 종료");
        }

        private void StartRefreshLoop()
        {
            if (_refreshTask != null) return;
            _refreshTask = schedule.Execute(() =>
            {
                if (!IsOpen) return;
                if (!_showOptions)
                {
                    _resultCountdown -= RefreshMs / 1000f;
                    if (_resultCountdown <= 0f)
                        ResetDialogue();
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

        // ===== 진입점 =====

        private void BeginAudience(string lordName, string lordTitle, AudienceOption[] options)
        {
            _lordName = lordName ?? "영주";
            _lordTitle = lordTitle ?? "동부 영지 영주";
            _options = options;
            _showOptions = true;
            _dialogueText = _lordName + ": \"무슨 일로 왔느냐?\"";

            Debug.Log("[LordUTK] " + _lordName + " 대면 시작");
            Show();
            Refresh();
        }

        // ===== 렌더링 =====

        private void Refresh()
        {
            _headlineLabel.text = "👑 " + _lordTitle + " — " + _lordName;
            _dialogueLabel.text = _dialogueText;
            _list.Clear();

            if (!_showOptions || _options == null)
                return;

            int displayCount = Mathf.Min(_options.Length, MaxOptions);
            if (_options.Length > MaxOptions)
                Debug.LogWarning("[LordUTK] 선택지가 " + MaxOptions + "개를 초과합니다 (" + _options.Length + "개). 초과분은 표시되지 않습니다.");

            int idx = 0;
            foreach (AudienceOption opt in _options)
            {
                if (idx >= displayCount) break;
                int index = idx;

                string label = (index + 1) + ". " + opt.text;
                if (opt.speechDifficulty > 0)
                    label += " [화술 " + opt.speechDifficulty + "]";

                _list.Add(UTKButton.Create(label, () =>
                {
                    ExecuteOption(index);
                }, UTKButton.Variant.Secondary));

                idx++;
            }

            _list.Add(UTKButton.Create("닫기 ✕", Close, UTKButton.Variant.Danger));
        }

        // ===== 선택 처리 =====

        private float _resultCountdown;

        private void ExecuteOption(int index)
        {
            if (_options == null || index < 0 || index >= _options.Length) return;

            AudienceOption opt = _options[index];
            _showOptions = false;

            int speechLevel = PlayerStats.Instance != null ? PlayerStats.Instance.Level : 1;
            bool success = opt.speechDifficulty <= 0 || speechLevel >= opt.speechDifficulty;

            if (success)
            {
                _dialogueText = _lordName + ": \"" + opt.successResult + "\"";
                _dialogueLabel.style.color = new StyleColor(UTKColor.GuildGreen);
                opt.onSuccess?.Invoke();
                Debug.Log("[LordUTK] 화술 성공! (레벨 " + speechLevel + " ≥ 필요 " + opt.speechDifficulty + ")");
            }
            else
            {
                _dialogueText = _lordName + ": \"" + opt.failResult + "\"";
                _dialogueLabel.style.color = new StyleColor(UTKColor.HealthRed);
                opt.onFail?.Invoke();
                Debug.Log("[LordUTK] 화술 실패! (레벨 " + speechLevel + " < 필요 " + opt.speechDifficulty + ")");
            }

            _resultCountdown = ResultDisplaySec;
            Refresh();
        }

        private void ResetDialogue()
        {
            _showOptions = true;
            _dialogueText = _lordName + ": \"또 무슨 일이냐?\"";
            _dialogueLabel.style.color = new StyleColor(UTKColor.TextPrimary);
            Refresh();
        }
    }
}