using System;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.UI
{
    /// <summary>
    /// Phase 39: 퀘스트 선택지 UI — IMGUI 기반 팝업.
    /// QuestWindow 확장 또는 별도 팝업으로 동작합니다.
    /// 1~4개 선택지 버튼, 조건 표시(회색=선택 불가), 선택 시 결과 텍스트 표시.
    /// </summary>
    public static class QuestChoiceUI
    {
        // ===== 상수 =====
        private const float POPUP_WIDTH_RATIO = 0.35f;
        private const float POPUP_HEIGHT_RATIO = 0.45f;
        private const float POPUP_MIN_WIDTH = 350f;
        private const float POPUP_MIN_HEIGHT = 300f;
        private const float FONT_SIZE_TITLE = 20f;
        private const float FONT_SIZE_DESC = 16f;
        private const float FONT_SIZE_CHOICE = 15f;
        private const float FONT_SIZE_RESULT = 16f;
        private const float RESULT_DISPLAY_TIME = 3f;

        // ===== 상태 =====
        private static string _chainId;
        private static QuestChainNode _currentNode;
        private static QuestChoice[] _choices;
        private static bool _isVisible;
        private static bool _showResult;
        private static string _resultText;
        private static float _resultStartTime;

        // 캐시된 스타일
        private static GUIStyle _styleTitle;
        private static GUIStyle _styleDesc;
        private static GUIStyle _styleChoice;
        private static GUIStyle _styleChoiceDisabled;
        private static GUIStyle _styleResult;
        private static GUIStyle _styleBackground;

        // ===== 속성 =====
        public static bool IsVisible => _isVisible;
        public static string CurrentChainId => _chainId;

        // ================================================================
        // 퍼블릭 메서드
        // ================================================================

        /// <summary>
        /// 선택지 UI를 표시합니다.
        /// </summary>
        /// <param name="chainId">체인 ID</param>
        /// <param name="node">현재 노드</param>
        public static void Show(string chainId, QuestChainNode node)
        {
            if (string.IsNullOrEmpty(chainId))
            {
                Debug.LogWarning("[QuestChoiceUI] Show: chainId가 null");
                return;
            }

            _chainId = chainId;
            _currentNode = node;
            _choices = node.choices ?? Array.Empty<QuestChoice>();
            _isVisible = true;
            _showResult = false;
            _resultText = null;

            Debug.Log($"[QuestChoiceUI] 선택지 표시: {node.title} ({_choices.Length}개 선택지)");
        }

        /// <summary>
        /// 선택지 UI를 닫습니다.
        /// </summary>
        public static void Dismiss()
        {
            _chainId = null;
            _currentNode = default;
            _choices = null;
            _isVisible = false;
            _showResult = false;
            _resultText = null;
        }

        /// <summary>
        /// 결과 텍스트만 표시 (선택 후 결과 화면)
        /// </summary>
        public static void ShowResult(string text)
        {
            _resultText = text;
            _showResult = true;
            _resultStartTime = Time.time;
        }

        // ================================================================
        // IMGUI OnGUI
        // ================================================================

        /// <summary>
        /// 매 프레임 호출하여 선택지 팝업을 그립니다.
        /// MonoBehaviour.OnGUI 또는 QuestWindow.OnGUI에서 호출해야 합니다.
        /// </summary>
        public static void OnChoiceGUI()
        {
            if (!_isVisible) return;

            // 결과 표시 모드 — RESULT_DISPLAY_TIME 후 자동 종료
            if (_showResult)
            {
                DrawResultPopup();
                if (Time.time - _resultStartTime >= RESULT_DISPLAY_TIME)
                {
                    Dismiss();
                }
                return;
            }

            // 화면 크기 기반 팝업 크기 계산
            float popupWidth = Mathf.Max(Screen.width * POPUP_WIDTH_RATIO, POPUP_MIN_WIDTH);
            float popupHeight = Mathf.Max(Screen.height * POPUP_HEIGHT_RATIO, POPUP_MIN_HEIGHT);
            float popupX = (Screen.width - popupWidth) / 2f;
            float popupY = (Screen.height - popupHeight) / 2f;

            // 스타일 초기화
            EnsureStyles();

            // 딤드 배경
            DrawDimmedBackground();

            // 팝업 배경
            Color originalBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.12f, 0.12f, 0.15f, 0.92f);
            GUI.Box(new Rect(popupX, popupY, popupWidth, popupHeight), "", _styleBackground);
            GUI.backgroundColor = originalBg;

            // 금색 테두리
            DrawBorder(popupX, popupY, popupWidth, popupHeight, new Color(0.85f, 0.70f, 0.15f, 1f), 2f);

            float contentX = popupX + 20f;
            float contentWidth = popupWidth - 40f;
            float cy = popupY + 15f;

            // 1. 제목
            GUI.Label(new Rect(contentX, cy, contentWidth, 30f), _currentNode.title, _styleTitle);
            cy += 35f;

            // 2. 설명
            GUI.Label(new Rect(contentX, cy, contentWidth, 50f), _currentNode.description, _styleDesc);
            cy += 55f;

            // 3. 선택지 버튼 (1~4개)
            if (_choices != null && _choices.Length > 0)
            {
                float buttonHeight = 45f;
                float buttonSpacing = 8f;
                float totalButtonsHeight = _choices.Length * (buttonHeight + buttonSpacing) - buttonSpacing;
                float buttonWidth = contentWidth * 0.85f;
                float buttonX = popupX + (popupWidth - buttonWidth) / 2f;

                for (int i = 0; i < _choices.Length; i++)
                {
                    var choice = _choices[i];
                    bool isAvailable = QuestChainManager.Instance.IsChoiceAvailable(choice);

                    Rect btnRect = new Rect(buttonX, cy, buttonWidth, buttonHeight);

                    if (isAvailable)
                    {
                        // 선택 가능 — 버튼
                        GUI.backgroundColor = new Color(0.2f, 0.4f, 0.6f, 0.9f);
                        if (GUI.Button(btnRect, $"  {i + 1}. {choice.text}", _styleChoice))
                        {
                            OnChoiceSelected(i);
                        }
                        GUI.backgroundColor = originalBg;
                    }
                    else
                    {
                        // 선택 불가 — 회색 표시 + 조건 툴팁
                        string label = $"  {i + 1}. {choice.text}";
                        if (!string.IsNullOrEmpty(choice.condition.failMessage))
                            label += $" ({choice.condition.failMessage})";
                        GUI.Button(btnRect, label, _styleChoiceDisabled);
                    }

                    cy += buttonHeight + buttonSpacing;
                }
            }

            // 닫기 안내
            GUI.Label(
                new Rect(popupX + popupWidth - 160, popupY + popupHeight - 22f, 150f, 18f),
                "ESC 키로 닫기",
                new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = new Color(0.5f, 0.5f, 0.5f) } }
            );
        }

        // ================================================================
        // 결과 팝업
        // ================================================================

        private static void DrawResultPopup()
        {
            float popupWidth = Mathf.Max(Screen.width * POPUP_WIDTH_RATIO, POPUP_MIN_WIDTH);
            float popupHeight = 200f;
            float popupX = (Screen.width - popupWidth) / 2f;
            float popupY = (Screen.height - popupHeight) / 2f;

            EnsureStyles();

            // 딤드 배경
            DrawDimmedBackground();

            // 팝업 배경
            Color originalBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.12f, 0.12f, 0.15f, 0.92f);
            GUI.Box(new Rect(popupX, popupY, popupWidth, popupHeight), "", _styleBackground);
            GUI.backgroundColor = originalBg;

            // 금색 테두리
            DrawBorder(popupX, popupY, popupWidth, popupHeight, new Color(0.85f, 0.70f, 0.15f, 1f), 2f);

            // 결과 텍스트
            if (!string.IsNullOrEmpty(_resultText))
            {
                GUI.Label(
                    new Rect(popupX + 20f, popupY + 20f, popupWidth - 40f, popupHeight - 40f),
                    _resultText,
                    _styleResult
                );
            }
        }

        // ================================================================
        // 버튼 핸들러
        // ================================================================

        private static void OnChoiceSelected(int choiceIndex)
        {
            if (string.IsNullOrEmpty(_chainId))
            {
                Dismiss();
                return;
            }

            // 선택 결과 텍스트 저장
            if (_choices != null && choiceIndex >= 0 && choiceIndex < _choices.Length)
            {
                string resultText = _choices[choiceIndex].result.resultText;
                if (!string.IsNullOrEmpty(resultText))
                {
                    _resultText = resultText;
                }
            }

            // 체인 매니저에 노드 완료 통보
            bool success = QuestChainManager.Instance.CompleteCurrentNode(_chainId, choiceIndex);
            if (!success)
            {
                Debug.LogWarning($"[QuestChoiceUI] 노드 완료 실패: {_chainId}, 선택지 {choiceIndex}");
                Dismiss();
                return;
            }

            // 결과 텍스트가 있으면 결과 팝업 표시, 없으면 바로 종료
            if (!string.IsNullOrEmpty(_resultText))
            {
                _showResult = true;
                _resultStartTime = Time.time;
                _choices = null;
                _currentNode = default;
            }
            else
            {
                Dismiss();
            }
        }

        // ================================================================
        // 드로잉 헬퍼
        // ================================================================

        private static void EnsureStyles()
        {
            if (_styleTitle != null) return;

            _styleTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(FONT_SIZE_TITLE),
                fontStyle = FontStyle.Bold,
                richText = true,
                normal = { textColor = new Color(1f, 0.85f, 0.3f, 1f) }
            };

            _styleDesc = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(FONT_SIZE_DESC),
                wordWrap = true,
                normal = { textColor = new Color(0.85f, 0.85f, 0.9f, 1f) }
            };

            _styleChoice = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(FONT_SIZE_CHOICE),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white },
                hover = { textColor = Color.yellow }
            };

            _styleChoiceDisabled = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(FONT_SIZE_CHOICE),
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.4f, 0.4f, 0.4f, 0.8f) }
            };

            _styleResult = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(FONT_SIZE_RESULT),
                wordWrap = true,
                richText = true,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.9f, 0.9f, 0.95f, 1f) }
            };

            _styleBackground = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTexture(2, 2, new Color(0.12f, 0.12f, 0.15f)) }
            };
        }

        private static void DrawDimmedBackground()
        {
            Color originalColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");
            GUI.backgroundColor = originalColor;
        }

        private static void DrawBorder(float x, float y, float w, float h, Color color, float thickness)
        {
            Color originalColor = GUI.backgroundColor;
            GUI.backgroundColor = color;
            GUI.Box(new Rect(x, y, w, thickness), "");
            GUI.Box(new Rect(x, y + h - thickness, w, thickness), "");
            GUI.Box(new Rect(x, y, thickness, h), "");
            GUI.Box(new Rect(x + w - thickness, y, thickness, h), "");
            GUI.backgroundColor = originalColor;
        }

        private static Texture2D MakeTexture(int width, int height, Color color)
        {
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;
            var texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>모든 상태 초기화 (테스트용)</summary>
        public static void ResetAll()
        {
            _chainId = null;
            _currentNode = default;
            _choices = null;
            _isVisible = false;
            _showResult = false;
            _resultText = null;
        }
    }
}