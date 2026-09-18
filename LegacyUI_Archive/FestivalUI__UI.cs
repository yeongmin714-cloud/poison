using System.Collections.Generic;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.UI
{
    /// <summary>
    /// Phase 38.4: 영지별 축제 정보 IMGUI 창.
    /// FestivalManager의 이벤트를 구독하여 축제 시작/종료 알림을 표시하고,
    /// M 키 맵 화면에서 현재 축제 중인 영지 정보를 보여줍니다.
    /// </summary>
    public static class FestivalUI
    {
        // ===== 상수 =====
        private const float NOTIFI_WIDTH_RATIO = 0.35f;
        private const float NOTIFI_HEIGHT_RATIO = 0.28f;
        private const float NOTIFI_MIN_WIDTH = 300f;
        private const float NOTIFI_MIN_HEIGHT = 200f;
        private const float AUTO_DISMISS_TIME = 12f;

        private const float PANEL_WIDTH_RATIO = 0.22f;
        private const float PANEL_HEIGHT_RATIO = 0.35f;
        private const float PANEL_MIN_WIDTH = 240f;
        private const float PANEL_MIN_HEIGHT = 180f;

        // ===== 알림 상태 =====
        private static FestivalData _notificationFestival;
        private static float _notificationStartTime;
        private static bool _isNotifVisible;

        // ===== 패널 상태 =====
        private static bool _showPanel;
        private static FestivalData _selectedFestival;

        // ===== 스타일 =====
        private static GUIStyle _styleTitle;
        private static GUIStyle _styleBody;
        private static GUIStyle _styleEffect;
        private static GUIStyle _styleButton;
        private static GUIStyle _styleTimer;
        private static GUIStyle _styleBackground;

        // ===== 프로퍼티 =====

        /// <summary>알림 표시 중 여부</summary>
        public static bool IsNotificationVisible => _isNotifVisible;

        /// <summary>정보 패널 표시 여부</summary>
        public static bool ShowPanel
        {
            get => _showPanel;
            set => _showPanel = value;
        }

        /// <summary>알림 표시 중인 축제</summary>
        public static FestivalData NotificationFestival => _notificationFestival;

        // ===== 초기화 =====

        /// <summary>FestivalManager 이벤트 구독</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            FestivalManager.OnFestivalStarted += OnFestivalStarted;
            FestivalManager.OnFestivalEnded += OnFestivalEnded;
        }

        // ===== 이벤트 핸들러 =====

        private static void OnFestivalStarted(FestivalData festival)
        {
            if (festival == null) return;

            _notificationFestival = festival;
            _notificationStartTime = Time.time;
            _isNotifVisible = true;

            Debug.Log($"[FestivalUI] 🎉 축제 알림 표시: {festival.emoji} {festival.festivalName} at {festival.territoryId}");
        }

        private static void OnFestivalEnded(FestivalData festival)
        {
            // 현재 표시 중인 알림이 이 축제면 닫기
            if (_notificationFestival != null && _notificationFestival.festivalId == festival.festivalId)
            {
                _isNotifVisible = false;
                _notificationFestival = null;
            }

            // 선택된 축제도 초기화
            if (_selectedFestival != null && _selectedFestival.festivalId == festival.festivalId)
            {
                _selectedFestival = null;
                _showPanel = false;
            }

            Debug.Log($"[FestivalUI] 축제 종료 알림: {festival.festivalName}");
        }

        // ===== 알림 닫기 =====

        /// <summary>현재 알림 닫기</summary>
        public static void DismissNotification()
        {
            _isNotifVisible = false;
            _notificationFestival = null;
        }

        /// <summary>정보 패널 닫기</summary>
        public static void ClosePanel()
        {
            _showPanel = false;
            _selectedFestival = null;
        }

        /// <summary>특정 축제 정보 표시 (MapWindow 등 외부 호출)</summary>
        public static void ShowFestivalInfo(FestivalData festival)
        {
            if (festival == null) return;
            _selectedFestival = festival;
            _showPanel = true;
        }

        // ================================================================
        // IMGUI OnGUI
        // ================================================================

        /// <summary>
        /// 축제 시작 알림 팝업을 그립니다.
        /// MonoBehaviour.OnGUI에서 호출해야 합니다.
        /// </summary>
        public static void OnFestivalNotifGUI()
        {
            if (!_isNotifVisible || _notificationFestival == null) return;

            // 자동 종료
            if (Time.time - _notificationStartTime >= AUTO_DISMISS_TIME)
            {
                DismissNotification();
                return;
            }

            InitializeStyles();

            float notifWidth = Mathf.Max(Screen.width * NOTIFI_WIDTH_RATIO, NOTIFI_MIN_WIDTH);
            float notifHeight = Mathf.Max(Screen.height * NOTIFI_HEIGHT_RATIO, NOTIFI_MIN_HEIGHT);
            float notifX = (Screen.width - notifWidth) / 2f;
            float notifY = Screen.height * 0.08f; // 상단에 표시

            // 딤드 배경 (약하게)
            DrawDimmedOverlay(0.3f);

            // 배경
            DrawPopupBox(notifX, notifY, notifWidth, notifHeight, _notificationFestival.festivalColor);

            float cx = notifX + 15f;
            float cy = notifY + 12f;
            float cw = notifWidth - 30f;

            // 제목
            string title = $"{_notificationFestival.emoji} {_notificationFestival.festivalName}";
            GUI.Label(new Rect(cx, cy, cw, 32f), title, _styleTitle);

            // 영지 정보
            GUI.Label(new Rect(cx, cy + 34f, cw, 22f),
                $"📍 {_notificationFestival.territoryId}", _styleBody);

            // 설명
            float descY = cy + 58f;
            float descHeight = 50f;
            GUI.Label(new Rect(cx, descY, cw, descHeight),
                _notificationFestival.description, _styleBody);

            // 효과
            float effectY = descY + descHeight + 4f;
            GUI.Label(new Rect(cx, effectY, cw, 24f),
                $"✨ 효과: {_notificationFestival.GetEffect().GetSummary()}", _styleEffect);

            // 자동 종료 타이머
            float remaining = AUTO_DISMISS_TIME - (Time.time - _notificationStartTime);
            GUI.contentColor = new Color(0.6f, 0.6f, 0.6f);
            GUI.Label(new Rect(cx, effectY + 26f, cw, 18f),
                $"⏰ {remaining:F0}초 후 자동 종료", _styleTimer);
            GUI.contentColor = Color.white;

            // [닫기] 버튼
            float btnW = 100f;
            float btnH = 28f;
            float btnX = notifX + (notifWidth - btnW) / 2f;
            float btnY = notifY + notifHeight - btnH - 12f;

            if (GUI.Button(new Rect(btnX, btnY, btnW, btnH), "확인", _styleButton))
            {
                DismissNotification();
            }
        }

        /// <summary>
        /// 축제 정보 패널을 그립니다 (M 키 맵 화면 등에서 사용).
        /// MonoBehaviour.OnGUI에서 호출해야 합니다.
        /// </summary>
        public static void OnFestivalPanelGUI()
        {
            if (!_showPanel || _selectedFestival == null) return;

            InitializeStyles();

            float panelWidth = Mathf.Max(Screen.width * PANEL_WIDTH_RATIO, PANEL_MIN_WIDTH);
            float panelHeight = Mathf.Max(Screen.height * PANEL_HEIGHT_RATIO, PANEL_MIN_HEIGHT);
            float panelX = Screen.width - panelWidth - 15f;
            float panelY = Screen.height * 0.15f;

            // 배경
            DrawPopupBox(panelX, panelY, panelWidth, panelHeight, _selectedFestival.festivalColor);

            float cx = panelX + 12f;
            float cy = panelY + 10f;
            float cw = panelWidth - 24f;

            // 제목
            string title = $"{_selectedFestival.emoji} {_selectedFestival.festivalName}";
            GUI.Label(new Rect(cx, cy, cw, 28f), title, _styleTitle);

            // 영지
            GUI.Label(new Rect(cx, cy + 30f, cw, 20f),
                $"📍 {_selectedFestival.territoryId}", _styleBody);

            // 기간
            GUI.Label(new Rect(cx, cy + 52f, cw, 20f),
                $"📅 Day {_selectedFestival.startDay} ~ {_selectedFestival.endDay}", _styleBody);

            // 시간대
            GUI.Label(new Rect(cx, cy + 74f, cw, 20f),
                $"⏰ {_selectedFestival.startHour}:00 ~ {_selectedFestival.endHour}:00", _styleBody);

            // 설명
            float descY = cy + 98f;
            float descH = 50f;
            GUI.Label(new Rect(cx, descY, cw, descH),
                _selectedFestival.description, _styleBody);

            // 효과
            float effY = descY + descH + 4f;
            GUI.Label(new Rect(cx, effY, cw, panelHeight - effY + panelY - 50f),
                $"✨ 효과:\n{_selectedFestival.GetEffect().GetSummary()}", _styleEffect);

            // [닫기] 버튼
            float btnW = 80f;
            float btnH = 24f;
            if (GUI.Button(new Rect(panelX + panelWidth - btnW - 10f,
                    panelY + 8f, btnW, btnH), "✕", _styleButton))
            {
                ClosePanel();
            }
        }

        /// <summary>
        /// 현재 활성화된 모든 축제 목록을 화면 좌측에 표시합니다 (HUD용).
        /// </summary>
        public static void OnActiveFestivalsHUD()
        {
            var mgr = FestivalManager.Instance;
            if (mgr == null || !mgr.HasAnyActiveFestival) return;

            InitializeStyles();

            float x = 10f;
            float y = Screen.height * 0.25f;
            float w = Mathf.Max(Screen.width * 0.18f, 180f);
            float lineH = 22f;

            foreach (var festival in mgr.ActiveFestivals)
            {
                Color original = GUI.contentColor;
                GUI.contentColor = festival.festivalColor;
                GUI.Label(new Rect(x, y, w, lineH),
                    $"{festival.emoji} {festival.festivalName}", _styleBody);
                GUI.contentColor = original;
                y += lineH;

                GUI.Label(new Rect(x + 10f, y, w - 10f, lineH),
                    $"  📍 {festival.territoryId}", _styleTimer);
                y += lineH + 4f;
            }
        }

        // ================================================================
        // 내부 드로잉
        // ================================================================

        private static void DrawDimmedOverlay(float alpha = 0.5f)
        {
            Color original = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0f, 0f, 0f, alpha);
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");
            GUI.backgroundColor = original;
        }

        private static void DrawPopupBox(float x, float y, float w, float h, Color accentColor)
        {
            // 테두리 (강조색)
            Color original = GUI.backgroundColor;
            GUI.backgroundColor = accentColor;
            GUI.Box(new Rect(x - 1f, y - 1f, w + 2f, h + 2f), "");

            // 배경
            GUI.backgroundColor = new Color(0.12f, 0.12f, 0.15f, 0.95f);
            GUI.Box(new Rect(x, y, w, h), "");
            GUI.backgroundColor = original;
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
                    fontSize = Mathf.RoundToInt(Screen.height * 0.022f),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    normal = new GUIStyleState { textColor = Color.white }
                };
            }
            else
            {
                _styleTitle.fontSize = Mathf.RoundToInt(Screen.height * 0.022f);
            }

            if (_styleBody == null)
            {
                _styleBody = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(Screen.height * 0.016f),
                    wordWrap = true,
                    alignment = TextAnchor.UpperLeft,
                    normal = new GUIStyleState { textColor = new Color(0.85f, 0.85f, 0.9f) }
                };
            }
            else
            {
                _styleBody.fontSize = Mathf.RoundToInt(Screen.height * 0.016f);
            }

            if (_styleEffect == null)
            {
                _styleEffect = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(Screen.height * 0.015f),
                    wordWrap = true,
                    alignment = TextAnchor.UpperLeft,
                    normal = new GUIStyleState { textColor = new Color(0.6f, 1.0f, 0.6f) }
                };
            }
            else
            {
                _styleEffect.fontSize = Mathf.RoundToInt(Screen.height * 0.015f);
            }

            if (_styleTimer == null)
            {
                _styleTimer = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(Screen.height * 0.014f),
                    alignment = TextAnchor.MiddleLeft,
                    normal = new GUIStyleState { textColor = Color.gray }
                };
            }
            else
            {
                _styleTimer.fontSize = Mathf.RoundToInt(Screen.height * 0.014f);
            }

            if (_styleButton == null)
            {
                _styleButton = new GUIStyle(GUI.skin.button)
                {
                    fontSize = Mathf.RoundToInt(Screen.height * 0.017f),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = new GUIStyleState { textColor = Color.white },
                    hover = new GUIStyleState { textColor = Color.yellow }
                };
            }
            else
            {
                _styleButton.fontSize = Mathf.RoundToInt(Screen.height * 0.017f);
            }
        }

        /// <summary>모든 상태 초기화 (테스트용)</summary>
        public static void ResetAll()
        {
            _notificationFestival = null;
            _isNotifVisible = false;
            _showPanel = false;
            _selectedFestival = null;
        }
    }
}