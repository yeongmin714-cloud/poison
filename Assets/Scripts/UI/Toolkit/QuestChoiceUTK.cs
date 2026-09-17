using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U6 Round A — 퀘스트 선택지 윈도우 (UTK).
    /// 원본: Assets/Scripts/UI/QuestChoiceUI.cs (376줄 IMGUI static) — 원본은 절대 수정하지 않는다.
    ///
    /// [구현]
    ///  ① 선택지 표시 — QuestChainNode.title/description/choices 실측 API 직접 사용.
    ///  ② 조건 — QuestChainManager.Instance.IsChoiceAvailable 실측 호출 (불가 시 회색 + failMessage 툴팁).
    ///  ③ 선택 — QuestChainManager.Instance.CompleteCurrentNode(chainId, index) 실측 호출.
    ///  ④ 결과 — QuestChoice.result.resultText 있으면 3초 표시 후 자동 닫힘 (400ms 폴링).
    ///  각 경로에 [QuestChoiceUTK] UnityEngine.Debug 로그.
    /// [진입점] static Show(chainId, node) / ShowResult(text) / Dismiss() / Toggle(). 순수 VisualElement 트리.
    /// </summary>
    public class QuestChoiceUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static QuestChoiceUTK _instance;
        public static QuestChoiceUTK Instance => _instance;

        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new QuestChoiceUTK();
        }

        /// <summary>선택지 UI 표시 (원본 QuestChoiceUI.Show 대응).</summary>
        public static void Show(string chainId, QuestChainNode node)
        {
            if (string.IsNullOrEmpty(chainId))
            {
                Debug.LogWarning("[QuestChoiceUTK] Show: chainId가 null");
                return;
            }
            Ensure();
            _instance.BeginShow(chainId, node);
        }

        /// <summary>결과 텍스트 전용 표시 (선택 후 결과 화면).</summary>
        public static void ShowResult(string text)
        {
            Ensure();
            _instance.BeginResult(text);
        }

        /// <summary>선택지 UI 닫기 (원본 QuestChoiceUI.Dismiss 대응).</summary>
        public static void Dismiss()
        {
            if (_instance != null)
                _instance.HideNow();
        }

        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Close(); return; }
            Ensure();
        }

        // ===== 설정 =====
        private const float WinW = 520f;
        private const float WinH = 480f;
        private const long RefreshMs = 400L;
        private const float ResultDisplaySec = 3f;

        // ===== 상태 =====
        private string _chainId;
        private QuestChainNode _node;
        private QuestChoice[] _choices;
        private bool _showResult;
        private string _resultText;
        private float _resultTimer;

        // ===== 레퍼런스 =====
        private readonly VisualElement _list;
        private Label _summaryLabel;
        private UnityEngine.UIElements.IVisualElementScheduledItem _refreshTask;

        private QuestChoiceUTK() : base("❓ 선택지", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            _summaryLabel = new Label("❓ 선택지");
            _summaryLabel.AddToClassList("utk-title-label");
            _summaryLabel.style.fontSize = 18f;
            _content.Add(_summaryLabel);

            _list = new VisualElement();
            _list.name = "QuestChoiceList";
            _list.style.flexGrow = 1f;
            _list.style.flexDirection = FlexDirection.Column;
            _content.Add(_list);

            ApplyUIToolkitFont(this);

            style.display = DisplayStyle.None;
            style.left = 700f;
            style.top = 260f;
        }

        // =================== 생명주기 ===================

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            style.left = 700f;
            style.top = 260f;
            StartRefreshLoop();
        }

        public override void Hide()
        {
            base.Hide();
            StopRefreshLoop();
            Debug.Log("[QuestChoiceUTK] 선택지 창 닫힘");
        }

        private void StartRefreshLoop()
        {
            if (_refreshTask != null) return;
            _refreshTask = schedule.Execute(() =>
            {
                if (!IsOpen) return;
                if (_showResult)
                {
                    _resultTimer -= RefreshMs / 1000f;
                    if (_resultTimer <= 0f)
                    {
                        HideNow();
                        return;
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

        private void HideNow()
        {
            _chainId = null;
            _node = default;
            _choices = null;
            _showResult = false;
            _resultText = null;
            Close();
        }

        // ===== 진입점 =====

        private void BeginShow(string chainId, QuestChainNode node)
        {
            _chainId = chainId;
            _node = node;
            _choices = node.choices ?? new QuestChoice[0];
            _showResult = false;
            _resultText = null;

            Debug.Log("[QuestChoiceUTK] 선택지 표시: " + node.title + " (" + _choices.Length + "개 선택지)");
            Show();
            Refresh();
        }

        private void BeginResult(string text)
        {
            _resultText = text;
            _showResult = true;
            _resultTimer = ResultDisplaySec;
            Show();
            Refresh();
        }

        // ===== 렌더링 =====

        private void Refresh()
        {
            _list.Clear();
            if (_showResult)
            {
                DrawResult();
                return;
            }
            DrawChoices();
        }

        private void DrawChoices()
        {
            _summaryLabel.text = _node.title;
            _list.Add(MakeLabel(_node.description, UTKColor.TextSecondary, false));

            if (_choices == null || _choices.Length == 0)
                return;

            int index = 0;
            foreach (var choice in _choices)
            {
                int choiceIndex = index;
                bool available = QuestChainManager.Instance != null
                    ? QuestChainManager.Instance.IsChoiceAvailable(choice)
                    : true;

                string prefix = (choiceIndex + 1) + ". " + choice.text;
                if (available)
                {
                    _list.Add(UTKButton.Create(prefix, () =>
                    {
                        OnChoiceSelected(choiceIndex);
                    }, UTKButton.Variant.Secondary));
                }
                else
                {
                    string disabledText = prefix;
                    if (!string.IsNullOrEmpty(choice.condition.failMessage))
                        disabledText += " (" + choice.condition.failMessage + ")";
                    var disabled = MakeLabel(disabledText, UTKColor.TextSecondary, false);
                    disabled.style.opacity = 0.55f;
                    _list.Add(disabled);
                }

                index++;
            }

            _list.Add(MakeLabel("ESC 키로 닫기", UTKColor.TextSecondary, false));
        }

        private void DrawResult()
        {
            _summaryLabel.text = "결과";
            _list.Add(MakeLabel(_resultText ?? "", UTKColor.TextPrimary, true));

            var closeBtn = UTKButton.Create("확인", HideNow, UTKButton.Variant.Primary);
            _list.Add(closeBtn);
        }

        // ===== 선택 처리 =====

        private void OnChoiceSelected(int choiceIndex)
        {
            if (string.IsNullOrEmpty(_chainId))
            {
                HideNow();
                return;
            }

            string resultText = null;
            if (_choices != null && choiceIndex >= 0 && choiceIndex < _choices.Length)
            {
                string candidate = _choices[choiceIndex].result.resultText;
                if (!string.IsNullOrEmpty(candidate))
                    resultText = candidate;
            }

            bool success = QuestChainManager.Instance != null
                ? QuestChainManager.Instance.CompleteCurrentNode(_chainId, choiceIndex)
                : false;
            if (!success)
            {
                Debug.LogWarning("[QuestChoiceUTK] 노드 완료 실패: " + _chainId + ", 선택지 " + choiceIndex);
                HideNow();
                return;
            }

            Debug.Log("[QuestChoiceUTK] 선택지 " + choiceIndex + " 처리 완료 (chain=" + _chainId + ")");

            if (!string.IsNullOrEmpty(resultText))
            {
                _resultText = resultText;
                _showResult = true;
                _resultTimer = ResultDisplaySec;
                _choices = null;
                _node = default;
                Refresh();
            }
            else
            {
                HideNow();
            }
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