using System.Collections.Generic;
using ProjectName.Core;
using UnityEngine;
using UnityEngine.InputSystem;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// C9-20: RTS 기본 명령 — 드래그 선택, 우클릭 공격/이동, H키 중단
    /// 
    /// 사용법:
    ///   GuardSelectionManager.Instance.SelectAllInRect(rect) // 드래그 선택
    ///   GuardSelectionManager.Instance.IssueCommand(targetPos, targetEnemy) // 우클릭 명령
    /// </summary>
    public class GuardSelectionManager : MonoBehaviour
    {
        public static GuardSelectionManager Instance { get; private set; }

        [Header("선택 설정")]
           [SerializeField] private Color _selectionBoxColor = new Color(0.2f, 0.5f, 1.0f, 0.2f);   // 파란색 반투명
           [SerializeField] private Color _selectionBorderColor = new Color(0.2f, 0.5f, 1.0f, 0.8f); // 파란색 테두리
           [SerializeField] private float _clickThreshold = 10f;// 드래그 vs 클릭 판정 거리

        [Header("선택 표시")]
        [SerializeField] private Material _selectedIndicatorMaterial;

        // 현재 선택된 병사 목록
        private readonly List<GuardPlaceholder> _selectedGuards = new List<GuardPlaceholder>();

        // 드래그 상태
        private bool _isDragging = false;
        private Vector2 _dragStartMouse;
        private Rect _selectionRect;

        // 캐시된 흰색 텍스처 (MakeTex 메모리 누수 방지)
        private static Texture2D _cachedWhiteTex;

        // 캐시된 ReadOnlyCollection (매 접근마다 생성 방지)
        private System.Collections.ObjectModel.ReadOnlyCollection<GuardPlaceholder> _selectedGuardsReadOnly;

        // 카메라 참조
        private Camera _mainCamera;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _mainCamera = Camera.main;
            _selectedGuardsReadOnly = _selectedGuards.AsReadOnly();
        }

        private void OnEnable()
        {
            // 씬 전환 시 Camera.main이 stale될 수 있으므로 갱신
            if (_mainCamera == null || !_mainCamera.gameObject.activeInHierarchy)
                _mainCamera = Camera.main;

            // 캐시된 ReadOnlyCollection 갱신 (리스트가 바뀌었을 수 있음)
            _selectedGuardsReadOnly = _selectedGuards.AsReadOnly();
        }

        private void Update()
        {
            if (Mouse.current == null) return;

            // 좌클릭 드래그 시작
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                _dragStartMouse = Mouse.current.position.ReadValue();
                _isDragging = true;
            }

            // 드래그 중
            if (_isDragging && Mouse.current.leftButton.isPressed)
            {
                Vector2 currentMouse = Mouse.current.position.ReadValue();
                float dragDist = Vector2.Distance(_dragStartMouse, currentMouse);

                if (dragDist > _clickThreshold)
                {
                    UpdateSelectionRect(_dragStartMouse, currentMouse);
                }
            }

            // 좌클릭 드래그 종료
            if (_isDragging && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                _isDragging = false;

                Vector2 releasePos = Mouse.current.position.ReadValue();
                float dragDist = Vector2.Distance(_dragStartMouse, releasePos);

                if (dragDist > _clickThreshold)
                {
                    // C9-22: Shift 누르면 추가 선택, 아니면 새 선택
                    bool additive = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
                    SelectGuardsInRect(_selectionRect, additive);
                }
                // 단순 클릭은 무시 (좌클릭은 공격용)
            }

            // 우클릭 명령 (RTSCommandSystem에 위임)
            if (Mouse.current.rightButton.wasPressedThisFrame && _selectedGuards.Count > 0)
            {
                bool ctrlHeld = Keyboard.current != null &&
                    (Keyboard.current.ctrlKey.isPressed || Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed);
                Vector2 mousePos = Mouse.current.position.ReadValue();

                if (RTSCommandSystem.Instance != null)
                    RTSCommandSystem.Instance.IssueRightClickCommand(mousePos, ctrlHeld);
            }

            // H키 공격 중단 (RTSCommandSystem에 위임)
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
            {
                if (RTSCommandSystem.Instance != null)
                    RTSCommandSystem.Instance.StopAllSelectedGuards();
            }
        }

        private void OnGUI()
        {
            if (_isDragging)
                DrawSelectionBoxGUI();

            // 선택된 병사 위에 파란색 원 표시
            DrawSelectionIndicators();
        }

        // ===== 선택 표시 (파란색 원) =====
        private void DrawSelectionIndicators()
        {
            if (_mainCamera == null || _selectedGuards.Count == 0) return;

            var oldColor = GUI.color;
            var oldMatrix = GUI.matrix;

            foreach (var guard in _selectedGuards)
            {
                if (guard == null || !guard.IsAlive || !guard.gameObject.activeInHierarchy) continue;

                Vector3 worldPos = guard.transform.position + Vector3.up * 1.8f;
                Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);
                if (screenPos.z < 0) continue;

                screenPos.y = Screen.height - screenPos.y;

                // 파란색 원 (원형 텍스처 대신 4개의 선으로 원 표현)
                float radius = 18f;
                float segments = 16;
                float thickness = 3f;

                GUI.color = new Color(0.2f, 0.5f, 1.0f, 0.9f);

                for (int i = 0; i < segments; i++)
                {
                    float angle1 = (i / segments) * Mathf.PI * 2f;
                    float angle2 = ((i + 1) / segments) * Mathf.PI * 2f;

                    float x1 = screenPos.x + Mathf.Cos(angle1) * radius - thickness / 2f;
                    float y1 = screenPos.y + Mathf.Sin(angle1) * radius - thickness / 2f;
                    float x2 = screenPos.x + Mathf.Cos(angle2) * radius - thickness / 2f;
                    float y2 = screenPos.y + Mathf.Sin(angle2) * radius - thickness / 2f;

                    float len = Mathf.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
                    if (len < 1f) continue;

                    float angleDeg = Mathf.Atan2(y2 - y1, x2 - x1) * Mathf.Rad2Deg;
                    GUIUtility.RotateAroundPivot(angleDeg, new Vector2(x1, y1));
                    GUI.DrawTexture(new Rect(x1, y1, len, thickness), MakeTex(1, 1, Color.white));
                    GUI.matrix = oldMatrix;
                }

                // 내부 반투명 원
                GUI.color = new Color(0.2f, 0.5f, 1.0f, 0.15f);
                GUI.DrawTexture(new Rect(screenPos.x - radius, screenPos.y - radius, radius * 2f, radius * 2f), MakeTex(1, 1, Color.white));

                GUI.color = oldColor;
                GUI.matrix = oldMatrix;
            }
        }

        // ===== 선택 박스 (IMGUI) =====
        private void DrawSelectionBoxGUI()
        {
            var color = GUI.color;
            GUI.color = _selectionBoxColor;
            GUI.DrawTexture(_selectionRect, MakeTex(1, 1, Color.white));
            GUI.color = _selectionBorderColor;
            // 테두리 (4면)
            GUI.DrawTexture(new Rect(_selectionRect.x, _selectionRect.y, _selectionRect.width, 2), MakeTex(1, 1, Color.white));
            GUI.DrawTexture(new Rect(_selectionRect.x, _selectionRect.yMax - 2, _selectionRect.width, 2), MakeTex(1, 1, Color.white));
            GUI.DrawTexture(new Rect(_selectionRect.x, _selectionRect.y, 2, _selectionRect.height), MakeTex(1, 1, Color.white));
            GUI.DrawTexture(new Rect(_selectionRect.xMax - 2, _selectionRect.y, 2, _selectionRect.height), MakeTex(1, 1, Color.white));
            GUI.color = color;
        }

        private Texture2D MakeTex(int w, int h, Color c)
        {
            if (_cachedWhiteTex == null)
            {
                _cachedWhiteTex = new Texture2D(1, 1);
                _cachedWhiteTex.SetPixel(0, 0, Color.white);
                _cachedWhiteTex.Apply();
            }
            return _cachedWhiteTex;
        }

        // ===== 드래그 Rect 계산 =====
        private void UpdateSelectionRect(Vector2 start, Vector2 end)
        {
            float x = Mathf.Min(start.x, end.x);
            float y = Mathf.Min(start.y, end.y);
            float w = Mathf.Abs(start.x - end.x);
            float h = Mathf.Abs(start.y - end.y);
            _selectionRect = new Rect(x, y, w, h);
        }

        // ===== 화면 좌표의 선택 Rect로 드래그 선택 =====
        /// <summary>
        /// 화면 Rect 내의 모든 GuardPlaceholder 선택
        /// </summary>
        public void SelectGuardsInRect(Rect screenRect, bool additive = false)
        {
            if (_mainCamera == null) return;

            if (!additive) ClearSelection();

            var guards = FindObjectsByType<GuardPlaceholder>();
            foreach (var guard in guards)
            {
                if (!guard.IsAlive || guard.IsRecruited == false) continue;

                Vector3 worldPos = guard.transform.position;
                Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);

                // Unity Screen 좌표계 변환 (y 반전)
                screenPos.y = Screen.height - screenPos.y;

                if (screenRect.Contains(screenPos))
                {
                    AddToSelection(guard);
                }
            }

            Debug.Log($"[RTS] {_selectedGuards.Count}명 선택됨");
        }

        /// <summary>
        /// 선택에 병사 추가
        /// </summary>
        public void AddToSelection(GuardPlaceholder guard)
        {
            if (guard == null || _selectedGuards.Contains(guard)) return;
            _selectedGuards.Add(guard);
            guard.SetSelected(true);
        }

        /// <summary>
        /// 선택 해제
        /// </summary>
        public void ClearSelection()
        {
            foreach (var guard in _selectedGuards)
            {
                if (guard != null)
                    guard.SetSelected(false);
            }
            _selectedGuards.Clear();
        }

        /// <summary>
        /// 현재 선택된 병사 목록
        /// </summary>
        public IReadOnlyList<GuardPlaceholder> SelectedGuards => _selectedGuardsReadOnly;
        public int SelectedCount => _selectedGuards.Count;
    }
}