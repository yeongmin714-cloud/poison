using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.UI
{
    /// <summary>
    /// 🗺️ 오토루트 확인 팝업 — IMGUI 기반 싱글톤.
    /// "📍 [아이템명] — [영지명] (으)로 이동"
    /// [이동] [취소] 버튼
    /// 3초 표시 후 자동 닫힘, ESC 닫기
    /// </summary>
    public class RouteConfirmationUI : MonoBehaviour
    {
        private static RouteConfirmationUI _instance;
        public static RouteConfirmationUI Instance => _instance;

        [Header("Popup Settings")]
        [SerializeField] private float _autoCloseTime = 3f;
        [SerializeField] private float _popupWidth = 700f;
        [SerializeField] private float _popupHeight = 280f;

        // 상태
        private bool _isOpen = false;
        private string _currentItemName = "";
        private string _currentTerritoryName = "";
        private string _currentTerritoryId = "";
        private float _openTime = 0f;

        // 스타일 캐시
        private bool _stylesInitialized;
        private GUIStyle _styleBg;
        private GUIStyle _styleTitle;
        private GUIStyle _styleDescription;
        private GUIStyle _styleButton;
        private GUIStyle _styleCancelButton;
        private Texture2D _texWhite;

        // 색상
        private static readonly Color ColorBg = new Color(0.12f, 0.10f, 0.14f, 0.92f);
        private static readonly Color ColorBorder = new Color(0.70f, 0.50f, 0.15f, 1f);
        private static readonly Color ColorTextPrimary = new Color(0.92f, 0.88f, 0.80f, 1f);
        private static readonly Color ColorAccent = new Color(0.80f, 0.60f, 0.20f, 1f);
        private static readonly Color ColorButtonBg = new Color(0.30f, 0.22f, 0.16f, 1f);
        private static readonly Color ColorButtonHover = new Color(0.45f, 0.32f, 0.22f, 1f);
        private static readonly Color ColorCancelBg = new Color(0.30f, 0.16f, 0.16f, 1f);
        private static readonly Color ColorCancelHover = new Color(0.45f, 0.22f, 0.22f, 1f);

        // ===== Unity Lifecycle =====

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[RouteConfirmationUI] 중복 인스턴스 감지 — 제거합니다.");
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;

            if (_texWhite != null)
            {
                Destroy(_texWhite);
                _texWhite = null;
            }
        }

        private void OnGUI()
        {
            if (!_isOpen) return;

            InitStyles();

            // ESC 닫기
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                Close();
                Event.current.Use();
                return;
            }

            // 3초 자동 닫힘
            if (Time.time - _openTime >= _autoCloseTime)
            {
                Close();
                return;
            }

            // 팝업 위치 계산 (화면 중앙)
            float popupX = (Screen.width - _popupWidth) * 0.5f;
            float popupY = (Screen.height - _popupHeight) * 0.5f;
            Rect popupRect = new Rect(popupX, popupY, _popupWidth, _popupHeight);

            // 딤드 오버레이
            DrawDimOverlay();

            // 배경
            GUI.Box(popupRect, "", _styleBg);

            // 외곽선 (황금색 테두리)
            DrawColoredRect(new Rect(popupX, popupY, _popupWidth, 3), ColorBorder);
            DrawColoredRect(new Rect(popupX, popupY + _popupHeight - 3, _popupWidth, 3), ColorBorder);

            // 아이콘 + 타이틀
            float titleY = popupY + 20;
            GUI.Label(new Rect(popupX + 20, titleY, _popupWidth - 40, 50), "📍 오토루트", _styleTitle);

            // 아이템명 + 영지명 설명
            float descY = titleY + 55;
            string description = $"📍 {_currentItemName} — {_currentTerritoryName} (으)로 이동";
            GUI.Label(new Rect(popupX + 20, descY, _popupWidth - 40, 55), description, _styleDescription);

            // 남은 시간 표시
            float remainingTime = _autoCloseTime - (Time.time - _openTime);
            if (remainingTime < 0f) remainingTime = 0f;
            float timerY = descY + 55;
            GUI.Label(new Rect(popupX + 20, timerY, _popupWidth - 40, 30),
                $"⏱️ {remainingTime:F1}초 후 자동 닫힘", _styleDescription);

            // 버튼 영역
            float btnY = timerY + 40;
            float btnWidth = 200f;
            float btnHeight = 55f;
            float totalBtnWidth = btnWidth * 2 + 20f;
            float btnStartX = popupX + (_popupWidth - totalBtnWidth) * 0.5f;

            // [이동] 버튼
            Rect moveBtnRect = new Rect(btnStartX, btnY, btnWidth, btnHeight);
            if (DrawButton(moveBtnRect, "🚶 이동", _styleButton, ColorButtonBg, ColorButtonHover, ColorAccent))
            {
                ExecuteAutoMove();
                Close();
                Event.current.Use();
                return;
            }

            // [취소] 버튼
            Rect cancelBtnRect = new Rect(btnStartX + btnWidth + 20f, btnY, btnWidth, btnHeight);
            if (DrawButton(cancelBtnRect, "취소", _styleCancelButton, ColorCancelBg, ColorCancelHover, ColorTextPrimary))
            {
                Close();
                Event.current.Use();
                return;
            }
        }

        // ===== Public API =====

        /// <summary>
        /// 오토루트 확인 팝업을 표시합니다.
        /// </summary>
        /// <param name="itemName">아이템 표시 이름</param>
        /// <param name="territoryName">영지 표시 이름</param>
        /// <param name="territoryId">영지 ID (TerritoryId.ToString())</param>
        public void Show(string itemName, string territoryName, string territoryId)
        {
            if (string.IsNullOrEmpty(itemName) || string.IsNullOrEmpty(territoryId))
            {
                Debug.LogWarning("[RouteConfirmationUI] Show: 파라미터가 null입니다.");
                return;
            }

            _currentItemName = itemName;
            _currentTerritoryName = territoryName ?? "알 수 없는 영지";
            _currentTerritoryId = territoryId;
            _isOpen = true;
            _openTime = Time.time;

            Debug.Log($"[RouteConfirmationUI] 📍 팝업 표시: {itemName} → {territoryName}");
        }

        /// <summary>
        /// 팝업을 닫습니다.
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            _currentItemName = "";
            _currentTerritoryName = "";
            _currentTerritoryId = "";
        }

        /// <summary>
        /// 팝업이 열려있는지 확인합니다.
        /// </summary>
        public bool IsOpen => _isOpen;

        // ===== Internal =====

        /// <summary>
        /// [이동] 버튼 실행 — AutoMoveManager.SetDestination 호출
        /// </summary>
        private void ExecuteAutoMove()
        {
            if (string.IsNullOrEmpty(_currentTerritoryId))
            {
                Debug.LogWarning("[RouteConfirmationUI] 영지 ID가 없어 이동할 수 없습니다.");
                return;
            }

            // 영지 ID 파싱
            string[] parts = _currentTerritoryId.Split('_');
            if (parts.Length != 2)
            {
                Debug.LogWarning($"[RouteConfirmationUI] 영지 ID 형식 오류: {_currentTerritoryId}");
                return;
            }

            if (!System.Enum.TryParse<NationType>(parts[0], out var nation))
            {
                Debug.LogWarning($"[RouteConfirmationUI] 국가 파싱 실패: {parts[0]}");
                return;
            }

            if (!int.TryParse(parts[1], out int index))
            {
                Debug.LogWarning($"[RouteConfirmationUI] 영지 인덱스 파싱 실패: {parts[1]}");
                return;
            }

            // AutoMoveManager 확인
            if (AutoMoveManager.Instance == null)
            {
                Debug.LogError("[RouteConfirmationUI] AutoMoveManager 인스턴스가 없습니다! Scene에 AutoMoveManager를 추가해주세요.");
                return;
            }

            // 영지 월드 좌표 계산 (MapWindow와 동일한 방식)
            Vector3 worldPos = new Vector3(
                index * 10f,
                0f,
                (int)nation * 10f
            );

            // 자동 이동 시작
            AutoMoveManager.Instance.SetDestination(worldPos);

            string message = $"📍 {_currentTerritoryName} (으)로 자동 이동합니다";
            Debug.Log($"[RouteConfirmationUI] {message}");
        }

        // ===== IMGUI 헬퍼 =====

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _texWhite = MakeTexture(1, 1, Color.white);

            _styleBg = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTexture(1, 1, ColorBg), textColor = ColorTextPrimary },
                border = new RectOffset(4, 4, 4, 4),
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(0, 0, 0, 0)
            };

            _styleTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 56,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ColorAccent }
            };

            _styleDescription = new GUIStyle(GUI.skin.label)
            {
                fontSize = 44,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ColorTextPrimary },
                wordWrap = true
            };

            _styleButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 44,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ColorAccent, background = MakeTexture(1, 1, ColorButtonBg) },
                hover = { textColor = Color.white, background = MakeTexture(1, 1, ColorButtonHover) },
                active = { textColor = ColorAccent, background = MakeTexture(1, 1, ColorButtonHover) },
                border = new RectOffset(2, 2, 2, 2),
                margin = new RectOffset(2, 2, 2, 2)
            };

            _styleCancelButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 40,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ColorTextPrimary, background = MakeTexture(1, 1, ColorCancelBg) },
                hover = { textColor = Color.white, background = MakeTexture(1, 1, ColorCancelHover) },
                active = { textColor = ColorTextPrimary, background = MakeTexture(1, 1, ColorCancelHover) },
                border = new RectOffset(2, 2, 2, 2),
                margin = new RectOffset(2, 2, 2, 2)
            };

            _stylesInitialized = true;
        }

        /// <summary>딤드 오버레이 그리기</summary>
        private void DrawDimOverlay()
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _texWhite);
            GUI.color = oldColor;
        }

        /// <summary>컬러 사각형 그리기</summary>
        private void DrawColoredRect(Rect rect, Color color)
        {
            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _texWhite);
            GUI.color = oldColor;
        }

        /// <summary>1x1 텍스처 생성</summary>
        private Texture2D MakeTexture(int w, int h, Color color)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, color);
            tex.Apply();
            return tex;
        }

        /// <summary>커스텀 스타일 버튼 그리기 (호버/액티브 색상 적용)</summary>
        private bool DrawButton(Rect rect, string text, GUIStyle style, Color normalBg, Color hoverBg, Color textColor)
        {
            bool isHover = rect.Contains(Event.current.mousePosition);

            // 배경색 동적 변경
            if (isHover)
            {
                style.normal.background = MakeTexture(1, 1, hoverBg);
                style.normal.textColor = Color.white;
            }
            else
            {
                style.normal.background = MakeTexture(1, 1, normalBg);
                style.normal.textColor = textColor;
            }

            return GUI.Button(rect, text, style);
        }
    }
}