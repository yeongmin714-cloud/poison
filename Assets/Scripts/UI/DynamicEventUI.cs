using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.UI
{
    /// <summary>
    /// Phase 36: 다이내믹 월드 이벤트 알림 UI.
    /// IMGUI 기반 팝업 창으로 이벤트 정보를 표시합니다.
    /// [이동하기] [무시] 버튼을 제공하며,
    /// 타이머 기반으로 자동 종료됩니다.
    /// </summary>
    public static class DynamicEventUI
    {
        /// <summary>런타임 초기화 — WorldEventManager 이벤트 구독</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            WorldEventManager.OnEventStarted += (evt) => ShowEvent(evt);
        }

        // ===== 상수 =====
        private const float POPUP_WIDTH_RATIO = 0.4f;
        private const float POPUP_HEIGHT_RATIO = 0.3f;
        private const float POPUP_MIN_WIDTH = 320f;
        private const float POPUP_MIN_HEIGHT = 240f;
        private const float AUTO_DISMISS_TIME = 15f; // 버튼 없이 15초 후 자동 종료
        private const float FONT_SIZE_TITLE_RATIO = 0.025f;
        private const float FONT_SIZE_BODY_RATIO = 0.018f;
        private const float FONT_SIZE_BUTTON_RATIO = 0.02f;

        // ===== 상태 =====
        private static WorldEventManager.ActiveEvent _currentEvent;
        private static float _showTime;
        private static bool _isVisible;
        private static Vector2 _buttonSize;

        // 캐시된 스타일 (GC Alloc 방지)
        private static GUIStyle _styleTitle;
        private static GUIStyle _styleBody;
        private static GUIStyle _styleTimer;
        private static GUIStyle _styleButton;
        private static GUIStyle _styleBackground;
        private static GUIStyle _styleBorder;

        /// <summary>UI 표시 활성화 여부</summary>
        public static bool IsVisible
        {
            get => _isVisible;
            set => _isVisible = value;
        }

        /// <summary>현재 표시 중인 이벤트</summary>
        public static WorldEventManager.ActiveEvent CurrentEvent => _currentEvent;

        // ================================================================
        // 퍼블릭 메서드
        // ================================================================

        /// <summary>
        /// 이벤트 알림 팝업을 표시합니다.
        /// WorldEventManager가 이벤트 시작 시 호출합니다.
        /// </summary>
        /// <param name="evt">표시할 활성 이벤트</param>
        public static void ShowEvent(WorldEventManager.ActiveEvent evt)
        {
            if (evt == null) return;

            _currentEvent = evt;
            _showTime = Time.time;
            _isVisible = true;

            Debug.Log($"[DynamicEventUI] 🌍 이벤트 팝업 표시: {WorldEventManager.GetEventEmoji(evt.type)} {WorldEventManager.GetEventDisplayName(evt.type)}");
        }

        /// <summary>
        /// 현재 표시된 이벤트 팝업을 즉시 닫습니다.
        /// </summary>
        public static void Dismiss()
        {
            _currentEvent = null;
            _isVisible = false;
        }

        /// <summary>
        /// 현재 표시된 이벤트 팝업을 강제로 닫고 이벤트를 무시 처리합니다.
        /// </summary>
        public static void DismissAndIgnore()
        {
            if (_currentEvent != null && WorldEventManager.Instance != null)
            {
                WorldEventManager.Instance.IgnoreEvent(_currentEvent);
            }
            Dismiss();
        }

        // ================================================================
        // IMGUI OnGUI
        // ================================================================

        /// <summary>
        /// 매 프레임 호출하여 이벤트 팝업을 그립니다.
        /// MonoBehaviour.OnGUI에서 호출해야 합니다.
        /// </summary>
        public static void OnEventGUI()
        {
            if (!_isVisible || _currentEvent == null) return;

            // 이벤트가 더 이상 활성 상태가 아니면 팝업 종료
            if (_currentEvent.phase != WorldEventManager.EventPhase.Active)
            {
                Dismiss();
                return;
            }

            // 자동 종료 타이머 (15초 후에도 플레이어가 아무 행동 안하면 종료)
            if (Time.time - _showTime >= AUTO_DISMISS_TIME)
            {
                // 자동 무시 (실패 처리)
                if (WorldEventManager.Instance != null)
                {
                    WorldEventManager.Instance.IgnoreEvent(_currentEvent);
                }
                Dismiss();
                return;
            }

            // 화면 크기 기반 팝업 크기 계산
            float popupWidth = Mathf.Max(Screen.width * POPUP_WIDTH_RATIO, POPUP_MIN_WIDTH);
            float popupHeight = Mathf.Max(Screen.height * POPUP_HEIGHT_RATIO, POPUP_MIN_HEIGHT);
            float popupX = (Screen.width - popupWidth) / 2f;
            float popupY = (Screen.height - popupHeight) / 2f;

            // 버튼 크기
            float buttonWidth = popupWidth * 0.3f;
            float buttonHeight = popupHeight * 0.12f;
            _buttonSize = new Vector2(buttonWidth, buttonHeight);

            // ===== 스타일 초기화 =====
            InitializeStyles();

            // ===== 딤드 배경 (반투명 검정) =====
            DrawDimmedBackground();

            // ===== 팝업 테두리 =====
            Color borderColor = GetEventBorderColor(_currentEvent.type);
            DrawPopupBorder(popupX, popupY, popupWidth, popupHeight, borderColor);

            // ===== 팝업 배경 =====
            Color bgColor = new Color(0.12f, 0.12f, 0.15f, 0.92f);
            Color originalBg = GUI.backgroundColor;
            GUI.backgroundColor = bgColor;
            GUI.Box(new Rect(popupX + 2f, popupY + 2f, popupWidth - 4f, popupHeight - 4f), "", _styleBackground);
            GUI.backgroundColor = originalBg;

            // ===== 컨텐츠 영역 =====
            float contentX = popupX + 20f;
            float contentY = popupY + 15f;
            float contentWidth = popupWidth - 40f;
            float remainingContentHeight = popupHeight - 30f;

            // 1. 제목 (이벤트 유형)
            string title = $"🌍 {WorldEventManager.GetEventDisplayName(_currentEvent.type)}";
            GUI.Label(new Rect(contentX, contentY, contentWidth, 35f), title, _styleTitle);

            // 2. 이벤트 설명
            float descriptionY = contentY + 40f;
            float descriptionHeight = 60f;
            GUI.Label(new Rect(contentX, descriptionY, contentWidth, descriptionHeight), _currentEvent.description, _styleBody);

            // 3. 영지 정보
            float territoryY = descriptionY + descriptionHeight + 5f;
            string territoryInfo = $"📍 영지: {_currentEvent.territoryName} ({_currentEvent.territoryId})";
            GUI.Label(new Rect(contentX, territoryY, contentWidth, 25f), territoryInfo, _styleBody);

            // 4. 남은 시간 (타이머)
            float timerY = territoryY + 28f;
            float remaining = _currentEvent.RemainingTime;
            string timerText = FormatRemainingTime(remaining);
            Color timerColor = remaining < 60f ? Color.red : (remaining < 180f ? Color.yellow : Color.white);
            GUI.contentColor = timerColor;
            GUI.Label(new Rect(contentX, timerY, contentWidth, 25f), timerText, _styleTimer);
            GUI.contentColor = Color.white;

            // 5. 자동 종료 카운트다운
            float autoDismissRemaining = AUTO_DISMISS_TIME - (Time.time - _showTime);
            if (autoDismissRemaining > 0f)
            {
                string autoText = $"⏰ {autoDismissRemaining:F0}초 후 자동 종료";
                GUI.contentColor = new Color(0.6f, 0.6f, 0.6f);
                GUI.Label(new Rect(contentX, timerY + 22f, contentWidth, 20f), autoText, _styleTimer);
                GUI.contentColor = Color.white;
            }

            // 6. 버튼 영역
            float buttonY = timerY + 50f;
            float buttonSpacing = 20f;
            float totalButtonsWidth = buttonWidth * 2 + buttonSpacing;
            float firstButtonX = popupX + (popupWidth - totalButtonsWidth) / 2f;

            // [이동하기] 버튼
            Color buttonColor = new Color(0.2f, 0.6f, 0.3f, 0.9f); // 초록색
            GUI.backgroundColor = buttonColor;
            if (GUI.Button(new Rect(firstButtonX, buttonY, buttonWidth, buttonHeight), "⚔️ 이동하기", _styleButton))
            {
                OnMoveToEvent();
            }

            // [무시] 버튼
            Color ignoreButtonColor = new Color(0.5f, 0.2f, 0.2f, 0.9f); // 빨간색
            GUI.backgroundColor = ignoreButtonColor;
            if (GUI.Button(new Rect(firstButtonX + buttonWidth + buttonSpacing, buttonY, buttonWidth, buttonHeight), "❌ 무시", _styleButton))
            {
                OnIgnoreEvent();
            }

            GUI.backgroundColor = originalBg;
        }

        // ================================================================
        // 버튼 핸들러
        // ================================================================

        /// <summary>
        /// [이동하기] 버튼 클릭 처리.
        /// 플레이어가 이벤트를 수락하고 해당 영지로 이동을 시도합니다.
        /// </summary>
        private static void OnMoveToEvent()
        {
            if (_currentEvent == null || WorldEventManager.Instance == null) return;

            Debug.Log($"[DynamicEventUI] 🚀 플레이어가 {_currentEvent.territoryName} 이벤트로 이동합니다.");

            // 이벤트 수락 처리
            WorldEventManager.Instance.AcceptEvent(_currentEvent);

            // TODO: 실제 영지 이동 로직 (MapWindow 연동 등)
            // 현재는 수락 처리 후 팝업 종료

            Dismiss();
        }

        /// <summary>
        /// [무시] 버튼 클릭 처리.
        /// 플레이어가 이벤트를 거절하고 무시합니다.
        /// </summary>
        private static void OnIgnoreEvent()
        {
            if (_currentEvent == null || WorldEventManager.Instance == null) return;

            Debug.Log($"[DynamicEventUI] ❌ 플레이어가 {_currentEvent.territoryName} 이벤트를 무시했습니다.");

            // 이벤트 무시 처리 (실패 처리됨)
            WorldEventManager.Instance.IgnoreEvent(_currentEvent);

            Dismiss();
        }

        // ================================================================
        // 내부 드로잉
        // ================================================================

        /// <summary>
        /// 반투명 검정 배경을 그립니다.
        /// </summary>
        private static void DrawDimmedBackground()
        {
            Color originalColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");
            GUI.backgroundColor = originalColor;
        }

        /// <summary>
        /// 팝업 테두리를 그립니다.
        /// </summary>
        private static void DrawPopupBorder(float x, float y, float width, float height, Color borderColor)
        {
            Color originalColor = GUI.backgroundColor;
            GUI.backgroundColor = borderColor;
            // 테두리 (약간 큰 박스)
            GUI.Box(new Rect(x, y, width, height), "");
            GUI.backgroundColor = originalColor;
        }

        // ================================================================
        // 스타일 초기화
        // ================================================================

        private static void InitializeStyles()
        {
            if (_styleTitle == null)
            {
                _styleTitle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(Screen.height * FONT_SIZE_TITLE_RATIO),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    normal = new GUIStyleState { textColor = Color.white }
                };
            }
            else
            {
                _styleTitle.fontSize = Mathf.RoundToInt(Screen.height * FONT_SIZE_TITLE_RATIO);
            }

            if (_styleBody == null)
            {
                _styleBody = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(Screen.height * FONT_SIZE_BODY_RATIO),
                    wordWrap = true,
                    alignment = TextAnchor.UpperLeft,
                    normal = new GUIStyleState { textColor = new Color(0.85f, 0.85f, 0.9f) }
                };
            }
            else
            {
                _styleBody.fontSize = Mathf.RoundToInt(Screen.height * FONT_SIZE_BODY_RATIO);
            }

            if (_styleTimer == null)
            {
                _styleTimer = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(Screen.height * FONT_SIZE_BODY_RATIO * 0.9f),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    normal = new GUIStyleState { textColor = Color.white }
                };
            }
            else
            {
                _styleTimer.fontSize = Mathf.RoundToInt(Screen.height * FONT_SIZE_BODY_RATIO * 0.9f);
            }

            if (_styleButton == null)
            {
                _styleButton = new GUIStyle(GUI.skin.button)
                {
                    fontSize = Mathf.RoundToInt(Screen.height * FONT_SIZE_BUTTON_RATIO),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = new GUIStyleState { textColor = Color.white },
                    hover = new GUIStyleState { textColor = Color.yellow }
                };
            }
            else
            {
                _styleButton.fontSize = Mathf.RoundToInt(Screen.height * FONT_SIZE_BUTTON_RATIO);
            }

            if (_styleBackground == null)
            {
                _styleBackground = new GUIStyle(GUI.skin.box)
                {
                    normal = new GUIStyleState { background = MakeTexture(2, 2, new Color(0.12f, 0.12f, 0.15f)) }
                };
            }
        }

        // ================================================================
        // 헬퍼
        // ================================================================

        /// <summary>
        /// 이벤트 유형별 테두리 색상을 반환합니다.
        /// </summary>
        private static Color GetEventBorderColor(WorldEventManager.EventType type)
        {
            return type switch
            {
                WorldEventManager.EventType.MonsterRaid => new Color(0.8f, 0.2f, 0.2f), // 빨강
                WorldEventManager.EventType.TravelingMerchant => new Color(0.2f, 0.7f, 0.3f), // 초록
                WorldEventManager.EventType.Plague => new Color(0.5f, 0.1f, 0.5f), // 보라
                WorldEventManager.EventType.FireFestival => new Color(0.9f, 0.7f, 0.1f), // 금색
                WorldEventManager.EventType.AssassinationContract => new Color(0.8f, 0.2f, 0.6f), // 핑크
                WorldEventManager.EventType.Fire => new Color(0.9f, 0.4f, 0.1f), // 주황
                WorldEventManager.EventType.RoyalEnvoy => new Color(0.3f, 0.5f, 0.9f), // 블루
                WorldEventManager.EventType.Storm => new Color(0.5f, 0.6f, 0.7f), // 회색
                _ => new Color(0.5f, 0.5f, 0.5f)
            };
        }

        /// <summary>
        /// 남은 시간을 포맷팅합니다.
        /// </summary>
        private static string FormatRemainingTime(float seconds)
        {
            if (seconds <= 0f)
                return "⏱️ 시간 초과";

            int totalSec = Mathf.CeilToInt(seconds);
            int minutes = totalSec / 60;
            int secs = totalSec % 60;

            if (minutes > 0)
                return $"⏱️ 남은 시간: {minutes}분 {secs:D2}초";
            else
                return $"⏱️ 남은 시간: {secs}초";
        }

        /// <summary>
        /// 단색 텍스처를 생성합니다. (GUIStyle 배경용)
        /// </summary>
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

        /// <summary>
        /// 모든 상태 초기화 (테스트용)
        /// </summary>
        public static void ResetAll()
        {
            _currentEvent = null;
            _isVisible = false;
            _showTime = 0f;
        }
    }
}