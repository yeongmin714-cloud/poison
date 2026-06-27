using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C10-15: 전쟁 알림 UI 시스템 — AI 전쟁 이벤트를 IMGUI로 표시합니다.
    /// 
    /// 오른쪽 상단에 스크롤되는 알림을 표시하며, 최대 5개까지 노출됩니다.
    /// 각 알림은 8초 후 자동으로 사라집니다.
    /// /war 명령어 또는 버튼으로 전체 이력 로그에 접근할 수 있습니다.
    /// 
    /// 사용법:
    ///   WarNotificationUI.ShowNotification("메시지", NotificationType.WarStart);
    ///   WarNotificationUI.ToggleHistory(); // 이력 로그 토글
    /// </summary>
    public static class WarNotificationUI
    {
        // ===== 열거형 =====

        /// <summary>알림 유형</summary>
        public enum NotificationType
        {
            /// <summary>전쟁 시작</summary>
            WarStart,
            /// <summary>전쟁 종료</summary>
            WarEnd,
            /// <summary>영토 상실</summary>
            TerritoryLost,
            /// <summary>영토 획득</summary>
            TerritoryGained,
            /// <summary>일반 정보</summary>
            Info
        }

        // ===== 데이터 =====

        /// <summary>알림 항목 구조체</summary>
        public struct NotificationEntry
        {
            /// <summary>알림 메시지</summary>
            public string message;
            /// <summary>생성 시간 (Time.time)</summary>
            public float timestamp;
            /// <summary>알림 유형</summary>
            public NotificationType type;
        }

        // ===== 상수 =====

        /// <summary>최대 표시 알림 수</summary>
        public const int MAX_VISIBLE_NOTIFICATIONS = 5;

        /// <summary>알림 자동 제거 시간 (초)</summary>
        public const float NOTIFICATION_DISMISS_TIME = 8f;

        /// <summary>알림 너비 (화면 비율)</summary>
        public const float NOTIFICATION_WIDTH_RATIO = 0.35f;

        /// <summary>알림 상단 여백</summary>
        public const float NOTIFICATION_TOP_MARGIN = 60f;

        /// <summary>알림 높이</summary>
        public const float NOTIFICATION_HEIGHT = 50f;

        /// <summary>알림 간격</summary>
        public const float NOTIFICATION_SPACING = 4f;

        /// <summary>최대 이력 저장 수</summary>
        public const int MAX_HISTORY_COUNT = 100;

        // ===== 내부 상태 =====

        private static readonly List<NotificationEntry> _activeNotifications = new List<NotificationEntry>();
        private static readonly List<NotificationEntry> _history = new List<NotificationEntry>();
        private static bool _showHistory;
        private static Vector2 _historyScrollPosition;

        // 캐시된 GUIStyle (GC Alloc 방지)
        private static GUIStyle _notificationLabelStyle;
        private static GUIStyle _historyTitleStyle;
        private static GUIStyle _historyEntryStyle;

        /// <summary>UI 표시 활성화 여부</summary>
        public static bool IsVisible { get; set; } = true;

        /// <summary>현재 표시 중인 알림 목록 (읽기 전용 복사본)</summary>
        public static IReadOnlyList<NotificationEntry> ActiveNotifications => _activeNotifications.AsReadOnly();

        /// <summary>전체 이력 목록 (읽기 전용 복사본)</summary>
        public static IReadOnlyList<NotificationEntry> History => _history.AsReadOnly();

        // ===== 메인 퍼블릭 메서드 =====

        /// <summary>
        /// 새 알림을 표시합니다. 알림 큐에 추가되고 8초 후 자동으로 사라집니다.
        /// </summary>
        /// <param name="message">표시할 메시지</param>
        /// <param name="type">알림 유형</param>
        public static void ShowNotification(string message, NotificationType type)
        {
            if (string.IsNullOrEmpty(message)) return;

            var entry = new NotificationEntry
            {
                message = message,
                timestamp = Time.time,
                type = type
            };

            _activeNotifications.Add(entry);
            _history.Add(entry);

            // 이력 제한
            if (_history.Count > MAX_HISTORY_COUNT)
            {
                _history.RemoveAt(0);
            }

            Debug.Log($"[WarNotification] {GetTypePrefix(type)} {message}");
        }

        /// <summary>
        /// 매 프레임 호출하여 오래된 알림을 제거합니다.
        /// </summary>
        public static void UpdateNotifications()
        {
            float currentTime = Time.time;
            _activeNotifications.RemoveAll(n => currentTime - n.timestamp >= NOTIFICATION_DISMISS_TIME);

            // 최대 표시 수 제한
            while (_activeNotifications.Count > MAX_VISIBLE_NOTIFICATIONS)
            {
                _activeNotifications.RemoveAt(0);
            }
        }

        /// <summary>
        /// 모든 활성 알림을 즉시 제거합니다.
        /// </summary>
        public static void ClearAll()
        {
            _activeNotifications.Clear();
        }

        /// <summary>
        /// 전체 이력 로그를 초기화합니다.
        /// </summary>
        public static void ClearHistory()
        {
            _activeNotifications.Clear();
            _history.Clear();
        }

        /// <summary>
        /// 이력 로그 표시를 토글합니다.
        /// </summary>
        public static void ToggleHistory()
        {
            _showHistory = !_showHistory;
        }

        /// <summary>
        /// 이력 로그 표시 상태를 설정합니다.
        /// </summary>
        public static void SetHistoryVisible(bool visible)
        {
            _showHistory = visible;
        }

        /// <summary>
        /// 이력 로그가 표시 중인지 확인합니다.
        /// </summary>
        public static bool IsHistoryVisible => _showHistory;

        // ===== IMGUI OnGUI =====

        /// <summary>
        /// 알림 UI를 그립니다. MonoBehaviour.OnGUI에서 호출합니다.
        /// </summary>
        public static void OnNotificationGUI()
        {
            if (!IsVisible) return;

            // 오래된 알림 제거
            UpdateNotifications();

            // 활성 알림 표시 (오른쪽 상단, 최신순)
            float notificationWidth = Screen.width * NOTIFICATION_WIDTH_RATIO;
            float startX = Screen.width - notificationWidth - 10f;
            float startY = NOTIFICATION_TOP_MARGIN;

            for (int i = _activeNotifications.Count - 1; i >= 0; i--)
            {
                var notif = _activeNotifications[i];
                float yPos = startY + (_activeNotifications.Count - 1 - i) * (NOTIFICATION_HEIGHT + NOTIFICATION_SPACING);

                // 알림 배경
                Color bgColor = GetTypeColor(notif.type);
                bgColor.a = 0.85f;

                DrawNotificationBox(startX, yPos, notificationWidth, NOTIFICATION_HEIGHT, bgColor, notif.message);
            }

            // 이력 로그 표시
            if (_showHistory)
            {
                DrawHistoryWindow();
            }
        }

        // ===== 내부 =====

        /// <summary>
        /// 개별 알림 박스를 그립니다.
        /// </summary>
        private static void DrawNotificationBox(float x, float y, float width, float height, Color bgColor, string message)
        {
            // 배경
            Color originalColor = GUI.backgroundColor;
            Color originalContent = GUI.contentColor;

            GUI.backgroundColor = bgColor;
            GUI.Box(new Rect(x, y, width, height), "");

            // 텍스트 — 캐시된 스타일 사용
            if (_notificationLabelStyle == null)
            {
                _notificationLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(Screen.height * 0.018f),
                    fontStyle = FontStyle.Bold,
                    wordWrap = true,
                    alignment = TextAnchor.MiddleLeft
                };
            }
            else
            {
                _notificationLabelStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.018f);
            }
            GUI.contentColor = Color.white;
            GUI.Label(new Rect(x + 8f, y + 2f, width - 16f, height - 4f), message, _notificationLabelStyle);

            GUI.backgroundColor = originalColor;
            GUI.contentColor = originalContent;
        }

        /// <summary>
        /// 이력 로그 창을 그립니다.
        /// </summary>
        private static void DrawHistoryWindow()
        {
            float windowWidth = Screen.width * 0.4f;
            float windowHeight = Screen.height * 0.5f;
            float windowX = (Screen.width - windowWidth) / 2f;
            float windowY = (Screen.height - windowHeight) / 2f;

            // 배경
            Color originalColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            GUI.Box(new Rect(windowX, windowY, windowWidth, windowHeight), "");

            // 제목 — 캐시된 스타일
            if (_historyTitleStyle == null)
            {
                _historyTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(Screen.height * 0.025f),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = new GUIStyleState { textColor = Color.white }
                };
            }
            else
            {
                _historyTitleStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.025f);
            }
            GUI.Label(new Rect(windowX, windowY + 5f, windowWidth, 30f), "📜 전쟁 이력", _historyTitleStyle);

            // 닫기 버튼
            if (GUI.Button(new Rect(windowX + windowWidth - 30f, windowY + 5f, 25f, 25f), "X"))
            {
                _showHistory = false;
            }

            // 스크롤 뷰
            float contentHeight = _history.Count * 25f;
            float viewHeight = windowHeight - 45f;
            float scrollWidth = windowWidth - 10f;
            _historyScrollPosition = GUI.BeginScrollView(
                new Rect(windowX + 5f, windowY + 40f, scrollWidth, viewHeight),
                _historyScrollPosition,
                new Rect(0, 0, scrollWidth - 15f, contentHeight));

            // 엔트리 스타일 — 캐시된 스타일
            if (_historyEntryStyle == null)
            {
                _historyEntryStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(Screen.height * 0.016f),
                    wordWrap = true,
                    richText = true
                };
            }
            else
            {
                _historyEntryStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.016f);
            }

            for (int i = 0; i < _history.Count; i++)
            {
                var entry = _history[_history.Count - 1 - i]; // 최신순
                string prefix = GetTypePrefix(entry.type);
                string timeStr = System.TimeSpan.FromSeconds(Time.time - entry.timestamp).ToString(@"mm\:ss");
                Color entryColor = GetTypeColor(entry.type);

                GUI.contentColor = entryColor;
                GUI.Label(new Rect(5f, i * 25f, scrollWidth - 25f, 25f), $"[{timeStr}] {prefix} {entry.message}", _historyEntryStyle);
            }

            GUI.contentColor = Color.white;
            GUI.EndScrollView();
            GUI.backgroundColor = originalColor;
        }

        /// <summary>
        /// 알림 유형별 색상 반환
        /// </summary>
        private static Color GetTypeColor(NotificationType type)
        {
            switch (type)
            {
                case NotificationType.WarStart: return new Color(0.8f, 0.2f, 0.2f, 1f); // 빨강
                case NotificationType.WarEnd: return new Color(0.9f, 0.6f, 0.1f, 1f); // 주황
                case NotificationType.TerritoryLost: return new Color(0.5f, 0.0f, 0.5f, 1f); // 보라
                case NotificationType.TerritoryGained: return new Color(0.1f, 0.7f, 0.2f, 1f); // 초록
                case NotificationType.Info:
                default: return new Color(0.3f, 0.5f, 0.8f, 1f); // 파랑
            }
        }

        /// <summary>
        /// 알림 유형별 접두사 반환
        /// </summary>
        private static string GetTypePrefix(NotificationType type)
        {
            switch (type)
            {
                case NotificationType.WarStart: return "⚔️";
                case NotificationType.WarEnd: return "🏁";
                case NotificationType.TerritoryLost: return "🏴";
                case NotificationType.TerritoryGained: return "🏳️";
                case NotificationType.Info:
                default: return "📢";
            }
        }

        /// <summary>
        /// 모든 상태 초기화 (테스트용)
        /// </summary>
        public static void ResetAll()
        {
            _activeNotifications.Clear();
            _history.Clear();
            _showHistory = false;
            IsVisible = true;
            _historyScrollPosition = Vector2.zero;
        }
    }
}