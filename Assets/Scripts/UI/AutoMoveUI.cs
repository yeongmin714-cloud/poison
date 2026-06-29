using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.UI
{
    /// <summary>
    /// Phase 40: 자동 이동 UI (Auto Move UI)
    /// IMGUI 기반 HUD 오버레이로 자동 이동 상태를 표시합니다.
    /// - 이동 중: 목적지, 남은 거리, 취소 안내
    /// - 도착 시: 알림 팝업
    /// - 취소 시: 취소 메시지
    /// - 지도(MapWindow) 우클릭 컨텍스트 메뉴와 연동
    /// </summary>
    [DefaultExecutionOrder(100)] // 다른 UI보다 늦게 실행되어 최상단 표시
    public class AutoMoveUI : MonoBehaviour
    {
        [Header("Notification Settings")]
        [SerializeField] private float _notificationDuration = 3f;  // 알림 표시 시간
        [SerializeField] private float _notificationFadeDuration = 0.5f; // 페이드 시간

        [Header("Display Settings")]
        [SerializeField] private float _panelWidth = 350f;
        [SerializeField] private float _panelHeight = 80f;
        [SerializeField] private float _panelMargin = 10f;

        [Header("Colors")]
        [SerializeField] private Color _panelBgColor = new Color(0f, 0f, 0f, 0.7f);
        [SerializeField] private Color _movingTextColor = Color.cyan;
        [SerializeField] private Color _arrivedTextColor = Color.green;
        [SerializeField] private Color _cancelledTextColor = new Color(1f, 0.7f, 0.3f); // 주황
        [SerializeField] private Color _pausedTextColor = Color.yellow;

        // 상태
        private string _currentMessage = "";
        private Color _currentMessageColor = Color.white;
        private float _messageTimer = 0f;
        private bool _hasActiveNotification = false;

        // 도착 메시지 표시 타이머
        private float _arrivalMessageTimer = 0f;
        private bool _showArrivalMessage = false;

        // 스타일
        private GUIStyle _messageStyle;
        private GUIStyle _infoStyle;
        private GUIStyle _destinationStyle;
        private bool _stylesInitialized;
        private GUIStyle _notificationStyle;
        private GUIStyle _notificationSubStyle;

        // ===== Unity Lifecycle =====

        private void Awake()
        {
            // AutoMoveManager 이벤트 구독
            AutoMoveManager.OnAutoMoveNotification += HandleAutoMoveNotification;
        }

        private void Start()
        {
            // 필요한 경우 AutoMoveManager 인스턴스 확인
            if (AutoMoveManager.Instance == null)
            {
                Debug.LogWarning("[AutoMoveUI] AutoMoveManager 인스턴스가 없습니다 — 일부 기능이 동작하지 않을 수 있습니다.");
            }
        }

        private void OnDestroy()
        {
            AutoMoveManager.OnAutoMoveNotification -= HandleAutoMoveNotification;
        }

        private void Update()
        {
            // 알림 타이머 업데이트
            if (_hasActiveNotification)
            {
                _messageTimer -= Time.deltaTime;
                if (_messageTimer <= 0f)
                {
                    _hasActiveNotification = false;
                    _currentMessage = "";
                }
            }

            // 도착 메시지 타이머
            if (_showArrivalMessage)
            {
                _arrivalMessageTimer -= Time.deltaTime;
                if (_arrivalMessageTimer <= 0f)
                {
                    _showArrivalMessage = false;
                }
            }
        }

        // ===== IMGUI Rendering =====

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint && Event.current.type != EventType.Layout)
                return;

            InitializeStyles();

            // 자동 이동 진행 중 → HUD 표시
            if (AutoMoveManager.Instance != null && AutoMoveManager.Instance.IsMoving)
            {
                DrawAutoMoveHUD();
            }

            // 일시 정지 상태
            if (AutoMoveManager.Instance != null && AutoMoveManager.Instance.IsPaused)
            {
                DrawPausedHUD();
            }

            // 일반 알림 표시 (취소/기타)
            if (_hasActiveNotification && !string.IsNullOrEmpty(_currentMessage))
            {
                DrawNotification(_currentMessage, _currentMessageColor);
            }

            // 도착 알림 (가장 위에 표시)
            if (_showArrivalMessage)
            {
                DrawArrivalNotification();
            }
        }

        /// <summary>
        /// GUI 스타일을 초기화합니다.
        /// </summary>
        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _messageStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _infoStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            };

            _destinationStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.cyan }
            };

            _notificationStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.green }
            };

            _notificationSubStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _stylesInitialized = true;
        }

        /// <summary>
        /// 자동 이동 중 HUD를 그립니다. (화면 상단 중앙)
        /// </summary>
        private void DrawAutoMoveHUD()
        {
            float x = (Screen.width - _panelWidth) * 0.5f;
            float y = _panelMargin;

            // 배경 패널
            Rect bgRect = new Rect(x, y, _panelWidth, _panelHeight);
            Color origColor = GUI.backgroundColor;
            GUI.backgroundColor = _panelBgColor;
            GUI.Box(bgRect, "");
            GUI.backgroundColor = origColor;

            // 상태 텍스트
            Rect statusRect = new Rect(x + 5f, y + 5f, _panelWidth - 10f, 28f);
            GUI.Label(statusRect, "🚶 자동 이동 중... [WASD로 취소]", _messageStyle);

            // 목적지 정보
            string destText = $"🎯 목적지: ({AutoMoveManager.Instance.Destination.x:F1}, {AutoMoveManager.Instance.Destination.z:F1})";
            Rect destRect = new Rect(x + 5f, y + 33f, _panelWidth - 10f, 22f);
            GUI.Label(destRect, destText, _destinationStyle);

            // 남은 거리
            float dist = AutoMoveManager.Instance.RemainingDistance;
            string distText = $"📏 남은 거리: {dist:F1}m";
            Rect distRect = new Rect(x + 5f, y + 55f, _panelWidth - 10f, 22f);
            GUI.Label(distRect, distText, _infoStyle);
        }

        /// <summary>
        /// 일시 정지 상태 HUD를 그립니다.
        /// </summary>
        private void DrawPausedHUD()
        {
            float panelW = 300f;
            float panelH = 40f;
            float x = (Screen.width - panelW) * 0.5f;
            float y = _panelMargin + _panelHeight + 5f;

            Rect bgRect = new Rect(x, y, panelW, panelH);
            Color origColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
            GUI.Box(bgRect, "");
            GUI.backgroundColor = origColor;

            Rect labelRect = new Rect(x + 5f, y + 5f, panelW - 10f, panelH - 10f);
            GUI.Label(labelRect, "⏸️ 전투 중 - 자동 이동 일시 정지", _messageStyle);
        }

        /// <summary>
        /// 일반 알림 메시지를 화면 하단에 표시합니다.
        /// </summary>
        private void DrawNotification(string message, Color color)
        {
            float alpha = Mathf.Clamp01(_messageTimer / _notificationFadeDuration);
            Color fadedColor = new Color(color.r, color.g, color.b, alpha);

            float panelW = 400f;
            float panelH = 40f;
            float x = (Screen.width - panelW) * 0.5f;
            float y = Screen.height - panelH - _panelMargin;

            Rect bgRect = new Rect(x, y, panelW, panelH);
            Color origBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0f, 0f, 0f, alpha * 0.6f);
            GUI.Box(bgRect, "");
            GUI.backgroundColor = origBg;

            Rect labelRect = new Rect(x + 5f, y + 5f, panelW - 10f, panelH - 10f);
            Color origTextColor = GUI.color;
            GUI.color = fadedColor;
            GUI.Label(labelRect, message, _messageStyle);
            GUI.color = origTextColor;
        }

        /// <summary>
        /// 도착 알림을 화면 중앙에 크게 표시합니다.
        /// </summary>
        private void DrawArrivalNotification()
        {
            float alpha = Mathf.Clamp01(_arrivalMessageTimer / _notificationFadeDuration);

            float panelW = 500f;
            float panelH = 120f;
            float x = (Screen.width - panelW) * 0.5f;
            float y = (Screen.height - panelH) * 0.5f;

            // 배경
            Rect bgRect = new Rect(x, y, panelW, panelH);
            Color origBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0f, 0.3f, 0f, alpha * 0.8f);
            GUI.Box(bgRect, "");
            GUI.backgroundColor = origBg;

            // 메인 텍스트
            Rect mainRect = new Rect(x + 10f, y + 15f, panelW - 20f, 50f);
            Color origText = GUI.color;
            GUI.color = new Color(0f, 1f, 0f, alpha);
            GUI.Label(mainRect, "✅ 도착했습니다!", _notificationStyle);
            GUI.color = origText;

            // 서브 텍스트
            Rect subRect = new Rect(x + 10f, y + 70f, panelW - 20f, 35f);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(subRect, "목표 지점에 도달했습니다.", _notificationSubStyle);
            GUI.color = origText;
        }

        // ===== Event Handlers =====

        /// <summary>
        /// AutoMoveManager의 알림 이벤트를 처리합니다.
        /// </summary>
        private void HandleAutoMoveNotification(string message)
        {
            _currentMessage = message;
            _messageTimer = _notificationDuration;
            _hasActiveNotification = true;

            // 메시지 내용에 따라 색상 결정
            if (message.Contains("도착"))
            {
                _currentMessageColor = _arrivedTextColor;

                // 도착 알림 별도 표시
                _showArrivalMessage = true;
                _arrivalMessageTimer = _notificationDuration * 1.5f; // 조금 더 길게
            }
            else if (message.Contains("취소"))
            {
                _currentMessageColor = _cancelledTextColor;
            }
            else if (message.Contains("일시 정지") || message.Contains("전투"))
            {
                _currentMessageColor = _pausedTextColor;
            }
            else
            {
                _currentMessageColor = _movingTextColor;
            }

            Debug.Log($"[AutoMoveUI] 알림: {message}");
        }
    }
}