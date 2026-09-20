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
    ///  ④ [O9 C-O9-03] LLM NPC — 대면창 열림 시 NPCDialogueAdapter.RequestDialogue(게이트: LLMConfig.IsConfigured()),
    ///     DialogueReady(Subscribe/Unsubscribe 쌍) 수신 시 기초 인사를 LLM 텍스트로 교체. 미설정 환경 0 변경.
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
        private string _heroicTale;   // [O6] 성취사 3문장
        private Label _taleLabel;      // [O6] 성취사 표시 라벨
        private AudienceOption[] _options;
        private bool _showOptions = true;
        private string _dialogueText = "";
        private bool _llmSubscribed;   // [O9] DialogueReady 구독 중 플래그 (Subscribe/Unsubscribe 쌍)

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

            // [O6 C-O6-04] 성취사 라벨 — 대면창에 영주 이력 3문장 표시
            _taleLabel = new Label("");
            _taleLabel.style.fontSize = 12f;
            _taleLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            _taleLabel.style.whiteSpace = WhiteSpace.Normal;
            _taleLabel.style.marginBottom = 8f;
            _content.Add(_taleLabel);

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
            UnsubscribeLordDialogue(); // [O9] 창 닫힘 — DialogueReady 구독 해제 (쌍 유지)
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

            // [O6 C-O6-04] 영주 성취사 — 결정론 3문장(territoryId 부재 → 이름 키 임시 사용, 추후 호출부 확장)
            _heroicTale = ProjectName.Core.Data.HeroicTaleGenerator.GetTaleForLord(_lordName, _lordName);

            Debug.Log("[LordUTK] " + _lordName + " 대면 시작");
            Show();
            Refresh();

            // [O9 C-O9-03] LLM NPC 배선 — 설정된 환경에서만 요청(미설정/어댑터 부재 시 0 변경).
            // 초기 인사(_dialogueText 규칙 텍스트)는 그대로 표시, LLM 응답 도착 시 OnLordDialogueReady가 교체한다.
            // NPCDialogueAdapter가 존재하지 않으면 Instance getter가 null — 완전 무영향.
            SubscribeLordDialogue();
            if (LLMConfig.IsConfigured() && NPCDialogueAdapter.Instance != null)
            {
                NPCDialogueAdapter.Instance.RequestDialogue(
                    _lordName,                                                   // npcKey — DialogueReady 필터용
                    NPCDialogueAdapter.BuildLordSystemPromptForLord(_lordName),  // territoryId 부재 → 영주 이름으로 재료 조회(성취사 임시 관례와 동일)
                    "인사");
            }
        }

        // =================== LLM NPC 구독 관리 (Subscribe/Unsubscribe 쌍 — O9) ===================

        private void SubscribeLordDialogue()
        {
            if (_llmSubscribed) return;
            NPCDialogueAdapter.Subscribe(OnLordDialogueReady); // 정적 이벤트 — 어댑터 미부재와 무관하게 안전
            _llmSubscribed = true;
        }

        private void UnsubscribeLordDialogue()
        {
            if (!_llmSubscribed) return;
            NPCDialogueAdapter.Unsubscribe(OnLordDialogueReady);
            _llmSubscribed = false;
        }

        /// <summary>LLM 응답(또는 규칙 폴백) 수신 — 기초 인사를 LLM 텍스트로 교체. 다른 영주 응답은 무시.</summary>
        private void OnLordDialogueReady(string npcKey, string llmText)
        {
            if (!string.Equals(npcKey, _lordName)) return;
            if (string.IsNullOrEmpty(llmText)) return;
            if (!IsOpen || !_showOptions) return; // 결과 표시 중에는 결과 텍스트 보존

            _dialogueText = llmText;
            if (_dialogueLabel != null) _dialogueLabel.text = _dialogueText;
            Debug.Log("[LordUTK] LLM 대사 수신: " + npcKey);
        }

        // ===== 렌더링 =====

        private void Refresh()
        {
            _headlineLabel.text = "👑 " + _lordTitle + " — " + _lordName;
            _dialogueLabel.text = _dialogueText;
            if (_taleLabel != null) _taleLabel.text = _heroicTale;
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