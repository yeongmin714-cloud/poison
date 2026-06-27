using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using ProjectName.UI.Themes;
using UnityEngine;

namespace ProjectName.UI
{
    /// <summary>
    /// G2-09: 미니맵 UI — 항상 표시되는 우측 상단 원형 미니맵.
    /// 
    /// [개요]
    /// IMGUI OnGUI 기반으로 화면 우측 상단에 200×200 원형 미니맵을 렌더링합니다.
    /// 플레이어를 중앙에 고정하고 카메라 방향으로 회전하며,
    /// 주변 영지 아이콘(국가별 점/텍스트)을 표시합니다.
    /// 마우스 휠로 0.5×~3× 확대/축소할 수 있고,
    /// 클릭 시 MapWindow(전체 지도)가 열립니다.
    /// 
    /// [표시 배율]
    /// minimap 반지름 = 50m (기본 zoom = 1.0)
    /// 반지름(월드) = 50m * _currentZoom
    /// 
    /// [작동 방식]
    /// - UIWindow를 상속하지만 항상 표시됩니다 (Start()에서 Show() 호출)
    /// - OnGUI에서 EventType.ScrollWheel 감지하여 zoom 조정
    /// - 영지 위치는 각 국가의 방향 벡터에 기반한 가상 월드 좌표 사용
    /// - 카메라의 yaw (Y축 회전)이 minimap 회전에 반영됨
    /// </summary>
    public class MinimapUI : UIWindow
    {
        [Header("Minimap Layout")]
        [SerializeField] private float _minimapSize = 400f;
        [SerializeField] private float _marginRight = 40f;
        [SerializeField] private float _marginTop = 40f;

        [Header("Zoom")]
        [SerializeField] private float _minZoom = 0.5f;
        [SerializeField] private float _maxZoom = 3f;
        [SerializeField] private float _defaultZoom = 1f;
        [SerializeField] private float _zoomStep = 0.25f;

        [Header("Map Data")]
        [SerializeField] private float _mapRadiusMeters = 50f; // minimap 반지름 = 50m

        // 런타임 상태
        private float _currentZoom = 1f;
        private bool _stylesInitialized;
        private Transform _playerTransform;
        private Camera _mainCamera;
        private MapWindow _mapWindow;

        // 국가별 방향 벡터 (월드 좌표, Y up)
        private static readonly Dictionary<NationType, Vector3> NationDirections = new Dictionary<NationType, Vector3>
        {
            { NationType.North, new Vector3(0f, 0f, 1f) },
            { NationType.East,  new Vector3(1f, 0f, 0f) },
            { NationType.South, new Vector3(0f, 0f, -1f) },
            { NationType.West,  new Vector3(-1f, 0f, 0f) },
            { NationType.Empire, Vector3.zero },
        };

        // 국가별 표시 라벨/아이콘
        private static readonly Dictionary<NationType, string> NationLabels = new Dictionary<NationType, string>
        {
            { NationType.North, "N" },
            { NationType.East,  "E" },
            { NationType.South, "S" },
            { NationType.West,  "W" },
            { NationType.Empire, "★" },
        };

        // 국가별 색상 (static readonly — GC 회피, 매 프레임 new Color 방지)
        private static readonly Color ColorNorth    = new Color(0.4f, 0.1f, 0.6f);
        private static readonly Color ColorEast     = new Color(0.0f, 0.3f, 0.8f);
        private static readonly Color ColorSouth    = new Color(0.7f, 0.1f, 0.1f);
        private static readonly Color ColorWest     = new Color(0.1f, 0.5f, 0.1f);
        private static readonly Color ColorEmpire   = new Color(1f, 0.85f, 0.2f);
        private static readonly Color ColorBgMinimap = new Color(0.1f, 0.1f, 0.15f, 0.85f);
        private static readonly Color ColorBorder   = new Color(0.6f, 0.6f, 0.7f, 0.8f);
        private static readonly Color ColorScaleBar = new Color(1f, 1f, 1f, 0.7f);

        private static readonly Dictionary<NationType, Color> NationColors = new Dictionary<NationType, Color>
        {
            { NationType.North, ColorNorth },
            { NationType.East,  ColorEast },
            { NationType.South, ColorSouth },
            { NationType.West,  ColorWest },
            { NationType.Empire, ColorEmpire },
        };

        // 캐싱된 영지 정의
        private readonly Dictionary<NationType, List<TerritoryDefinition>> _nationTerritories =
            new Dictionary<NationType, List<TerritoryDefinition>>();

        // ===== Styles =====

        private GUIStyle _territoryDotStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _zoomLabelStyle;

        // ===== 캐싱된 OnGUI 재사용 변수 (GC 회피) =====
        private string _lastZoomText;
        private float _lastZoomValue;

        // ===== Lifecycle =====

        protected override void Awake()
        {
            base.Awake();
            CacheTerritories();
        }

        private void Start()
        {
            _currentZoom = _defaultZoom;

            // Phase 33 UI-02: 미니맵 테마 적용
            ApplyTheme(Phase33_Themes.CreateMinimapTheme());

            // Find runtime references
            _mainCamera = Camera.main;
            if (_mainCamera == null)
                _mainCamera = FindObjectOfType<Camera>();
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
                _playerTransform = playerGo.transform;
            else
                Debug.LogWarning("[MinimapUI] Player를 찾을 수 없습니다! Transform 기본값 사용.");
            _mapWindow = FindObjectOfType<MapWindow>();

            // Minimap is always visible — force show
            if (!_isOpen)
            {
                _isOpen = true;
                if (_windowRoot != null)
                    _windowRoot.SetActive(true);
                if (_canvasGroup != null)
                    _canvasGroup.alpha = 1f;
            }
        }

        /// <summary>
        /// 영지 데이터를 캐싱합니다.
        /// </summary>
        private void CacheTerritories()
        {
            _nationTerritories.Clear();

            NationType[] nations = { NationType.East, NationType.West, NationType.South, NationType.North, NationType.Empire };
            foreach (var nation in nations)
            {
                var list = new List<TerritoryDefinition>();
                var defs = TerritoryDatabase.Instance?.GetDefinitionsByNation(nation);
                if (defs != null)
                {
                    foreach (var def in defs)
                        list.Add(def);
                }
                _nationTerritories[nation] = list;
            }
        }

        // ===== Public API for tests =====

        /// <summary>현재 확대/축소 비율 (0.5 ~ 3.0)</summary>
        public float CurrentZoom => _currentZoom;

        /// <summary>기본 확대/축소 비율</summary>
        public float DefaultZoom => _defaultZoom;

        /// <summary>최소 확대/축소 비율</summary>
        public float MinZoom => _minZoom;

        /// <summary>최대 확대/축소 비율</summary>
        public float MaxZoom => _maxZoom;

        /// <summary>미니맵 크기 (픽셀)</summary>
        public float MinimapSize => _minimapSize;

        /// <summary>미니맵 반지름 (미터, zoom=1 기준)</summary>
        public float MapRadiusMeters => _mapRadiusMeters;

        /// <summary>현재 미니맵 사각 영역 (Screen 좌표)</summary>
        public Rect MinimapRect => new Rect(
            Screen.width - _minimapSize - _marginRight,
            _marginTop,
            _minimapSize,
            _minimapSize
        );

        /// <summary>미니맵 중심 (Screen 좌표, 픽셀)</summary>
        public Vector2 MinimapCenter => new Vector2(
            Screen.width - _marginRight - _minimapSize * 0.5f,
            _marginTop + _minimapSize * 0.5f
        );

        /// <summary>미니맵 반지름 (픽셀)</summary>
        public float MinimapRadiusPx => _minimapSize * 0.5f;

        /// <summary>미니맵 배율 (미터/픽셀)</summary>
        public float MetersPerPixel => (_mapRadiusMeters * _currentZoom) / MinimapRadiusPx;

        /// <summary>현재 플레이어 월드 위치</summary>
        public Vector3 PlayerWorldPosition =>
            _playerTransform != null ? _playerTransform.position : Vector3.zero;

        /// <summary>현재 카메라 Yaw 각도 (라디안)</summary>
        public float CameraYawRad =>
            _mainCamera != null ? _mainCamera.transform.eulerAngles.y * Mathf.Deg2Rad : 0f;

        /// <summary>확대 비율을 증가시킵니다. (테스트에서 호출 가능)</summary>
        public void ZoomIn()
        {
            _currentZoom = Mathf.Min(_maxZoom, _currentZoom + _zoomStep);
        }

        /// <summary>확대 비율을 감소시킵니다. (테스트에서 호출 가능)</summary>
        public void ZoomOut()
        {
            _currentZoom = Mathf.Max(_minZoom, _currentZoom - _zoomStep);
        }

        /// <summary>확대 비율을 기본값(1.0)으로 리셋합니다.</summary>
        public void ResetZoom()
        {
            _currentZoom = _defaultZoom;
        }

        /// <summary>
        /// 영지의 가상 월드 위치를 반환합니다.
        /// 각 국가는 카디널 방향에 위치하고, 영지는 Ring에 따라 거리가 달라집니다.
        /// </summary>
        public Vector3 GetTerritoryWorldPosition(TerritoryDefinition def)
        {
            // NationDirections 딕셔너리에 없는 국가(None, Dracula 등)는 안전 폴백
            if (!NationDirections.TryGetValue(def.nation, out Vector3 dir))
                return Vector3.zero;

            // Empire is at center
            if (def.nation == NationType.Empire)
                return Vector3.zero;

            // Distance from center based on difficulty ring
            float distance = (int)def.difficulty switch
            {
                0 => 15f,  // Ring1
                1 => 30f,  // Ring2
                2 => 45f,  // Ring3
                3 => 60f,  // Ring4
                _ => 20f,
            };

            // Add slight offset based on index so territories in same ring spread a bit
            float spreadAngle = (def.id.index % 5) * 18f * Mathf.Deg2Rad;
            Vector3 spreadDir = Quaternion.Euler(0f, spreadAngle * Mathf.Rad2Deg, 0f) * dir;
            if (spreadDir.sqrMagnitude < 0.01f)
                spreadDir = dir;

            return spreadDir.normalized * distance;
        }

        /// <summary>
        /// 월드 위치를 미니맵 로컬 좌표(-1~1, 반지름 기준)로 변환합니다.
        /// 플레이어 위치를 기준으로 상대 위치를 계산하고, 카메라 회전을 반영합니다.
        /// </summary>
        public Vector2 WorldToMinimapLocal(Vector3 worldPos)
        {
            Vector3 playerPos = PlayerWorldPosition;
            Vector3 relative = worldPos - playerPos;

            // 현재 zoom 반영 — zoom이 클수록 같은 월드 거리가 더 멀리 표시됨
            float worldRadius = _mapRadiusMeters * _currentZoom;

            // Y 성분은 무시 (top-down)
            Vector2 flatRelative = new Vector2(relative.x, relative.z);

            // 카메라 회전 반영 (회전 행렬)
            float yaw = CameraYawRad;
            float cos = Mathf.Cos(-yaw); // 반대 방향 회전 (minimap은 위가 북쪽)
            float sin = Mathf.Sin(-yaw);
            Vector2 rotated = new Vector2(
                flatRelative.x * cos - flatRelative.y * sin,
                flatRelative.x * sin + flatRelative.y * cos
            );

            // 반지름 기준 정규화
            return new Vector2(rotated.x / worldRadius, rotated.y / worldRadius);
        }

        // ===== IMGUI Rendering =====

        private void OnGUI()
        {
            if (!_isOpen) return;

            DrawMinimap();
        }

        /// <summary>
        /// 미니맵 전체를 렌더링합니다.
        /// </summary>
        private void DrawMinimap()
        {
            InitializeStyles();

            float radius = MinimapRadiusPx;
            // Group-local center: BeginGroup 내에서는 rect 기준 상대 좌표
            float cx = _minimapSize * 0.5f;
            float cy = cx; // square => same

            // ---------- Handle mouse wheel zoom ----------
            Vector2 mousePos = Event.current.mousePosition;
            float rectX = Screen.width - _minimapSize - _marginRight;
            float rectY = _marginTop;
            if (mousePos.x >= rectX && mousePos.x <= rectX + _minimapSize &&
                mousePos.y >= rectY && mousePos.y <= rectY + _minimapSize)
            {
                if (Event.current.type == EventType.ScrollWheel)
                {
                    float delta = -Event.current.delta.y * 0.1f;
                    if (delta > 0f)
                        ZoomIn();
                    else if (delta < 0f)
                        ZoomOut();
                    Event.current.Use();
                }

                // Mouse click → open MapWindow
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    if (_mapWindow != null)
                    {
                        _mapWindow.Show();
                    }
                    else
                    {
                        Debug.LogWarning("[MinimapUI] MapWindow를 찾을 수 없습니다!");
                    }
                    Event.current.Use();
                }
            }

            // ---------- Background (Screen 좌표, BeginGroup 밖) ----------
            Rect bgRect = new Rect(rectX, rectY, _minimapSize, _minimapSize);
            Color origBg = GUI.backgroundColor;
            GUI.backgroundColor = ColorBgMinimap;
            GUI.Box(bgRect, "");
            GUI.backgroundColor = origBg;

            // ---------- Circular clip via GUI.BeginGroup ----------
            GUI.BeginGroup(bgRect);

            // Inner area
            Rect innerRect = new Rect(0f, 0f, _minimapSize, _minimapSize);
            GUI.Box(innerRect, "");

            // ---------- Draw territory icons (Group-local 좌표) ----------
            DrawTerritoryIcons(cx, cy, radius);

            // ---------- Draw player icon ----------
            DrawPlayerIcon(cx, cy);

            // ---------- Draw zoom level label (캐싱으로 GC 회피) ----------
            if (_currentZoom != _lastZoomValue)
            {
                _lastZoomValue = _currentZoom;
                _lastZoomText = $"x{_currentZoom:F1}";
            }
            Rect zoomRect = new Rect(_minimapSize - 100f, _minimapSize - 36f, 96f, 32f);
            GUI.Label(zoomRect, _lastZoomText ?? $"x{_currentZoom:F1}", _zoomLabelStyle);

            // ---------- Draw scale bar ----------
            DrawScaleBar(cx, cy, radius);

            GUI.EndGroup();

            // ---------- Circular border (Screen 좌표, BeginGroup 밖) ----------
            DrawCircularBorder(bgRect);
        }

        /// <summary>
        /// 영지 아이콘을 미니맵에 그립니다. (Group-local 좌표: cx, cy)
        /// </summary>
        private void DrawTerritoryIcons(float cx, float cy, float radius)
        {
            foreach (var kvp in _nationTerritories)
            {
                NationType nation = kvp.Key;
                List<TerritoryDefinition> territories = kvp.Value;

                // Empire is only shown if discovered
                if (nation == NationType.Empire)
                {
                    if (!MapWindow.IsEmpireDiscovered)
                        continue;
                }

                // Draw nation label at the nation's center direction
                Vector3 nationCenterWorld = NationDirections[nation] * 30f;
                Vector2 localPos = WorldToMinimapLocal(
                    PlayerWorldPosition + nationCenterWorld);

                float px = localPos.x * radius;
                float py = localPos.y * radius;

                // Only draw if within circle radius (with small margin)
                // GC: sqrMagnitude 비교로 Mathf.Sqrt 제거
                if (px * px + py * py > (radius - 4f) * (radius - 4f))
                    continue;

                float iconX = cx + px - 16f;
                float iconY = cy + py - 16f;

                // Draw colored dot
                Color origColor = GUI.color;
                GUI.color = NationColors[nation];
                Rect dotRect = new Rect(iconX, iconY, 32f, 32f);
                GUI.Box(dotRect, "");
                GUI.color = origColor;

                // Draw label text
                Rect labelRect = new Rect(iconX + 36f, iconY, 40f, 32f);
                GUI.Label(labelRect, NationLabels[nation], _territoryDotStyle);

                // Draw individual territory dots for nearby territories
                for (int i = 0; i < territories.Count; i++)
                {
                    var def = territories[i];
                    Vector3 twp = GetTerritoryWorldPosition(def);
                    Vector2 tLocal = WorldToMinimapLocal(twp);
                    float tPx = tLocal.x * radius;
                    float tPy = tLocal.y * radius;
                    // GC: sqrMagnitude 비교로 Mathf.Sqrt 회피
                    if (tPx * tPx + tPy * tPy > (radius - 2f) * (radius - 2f))
                        continue;

                    // NationColors[nation] * 0.7f — Color struct, stack only, no GC
                    GUI.color = NationColors[nation] * 0.7f;
                    Rect tDotRect = new Rect(
                        cx + tPx - 4f,
                        cy + tPy - 4f,
                        8f, 8f);
                    GUI.Box(tDotRect, "");
                    GUI.color = origColor;
                }
            }
        }

        /// <summary>
        /// 플레이어 아이콘 (삼각형/화살표)을 미니맵 중앙에 회전하여 그립니다.
        /// </summary>
        private void DrawPlayerIcon(float cx, float cy)
        {
            Color origColor = GUI.color;
            GUI.color = Color.cyan;

            float yaw = CameraYawRad;
            float cos = Mathf.Cos(yaw);
            float sin = Mathf.Sin(yaw);

            // 삼각형 점 3개 (위쪽 방향 = forward)
            float size = 16f;
            Vector2 tip = new Vector2(0f, -size);
            Vector2 left = new Vector2(-size * 0.5f, size);
            Vector2 right = new Vector2(size * 0.5f, size);

            // 회전 (Group-local center 기준)
            Vector2 tipR = new Vector2(
                tip.x * cos - tip.y * sin,
                tip.x * sin + tip.y * cos);
            tipR.x += cx;
            tipR.y += cy;

            Vector2 leftR = new Vector2(
                left.x * cos - left.y * sin,
                left.x * sin + left.y * cos);
            leftR.x += cx;
            leftR.y += cy;

            Vector2 rightR = new Vector2(
                right.x * cos - right.y * sin,
                right.x * sin + right.y * cos);
            rightR.x += cx;
            rightR.y += cy;

            // Draw triangle using lines
            DrawLine(tipR, leftR, Color.cyan);
            DrawLine(leftR, rightR, Color.cyan);
            DrawLine(rightR, tipR, Color.cyan);

            // Center dot (Group-local)
            GUI.color = Color.white;
            Rect centerDot = new Rect(cx - 4f, cy - 4f, 8f, 8f);
            GUI.Box(centerDot, "");
            GUI.color = origColor;
        }

        /// <summary>
        /// 두 점 사이에 선을 그립니다. (IMGUI에는 기본 선 그리기가 없으므로
        /// 여러 개의 작은 점으로 근사)
        /// </summary>
        private static void DrawLine(Vector2 a, Vector2 b, Color color)
        {
            Color orig = GUI.color;
            GUI.color = color;
            float dist = Vector2.Distance(a, b);
            int steps = Mathf.Max(2, Mathf.RoundToInt(dist / 4f));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float px = a.x + (b.x - a.x) * t;
                float py = a.y + (b.y - a.y) * t;
                Rect r = new Rect(px - 2f, py - 2f, 4f, 4f);
                GUI.Box(r, "");
            }
            GUI.color = orig;
        }

        /// <summary>
        /// 미니맵의 원형 테두리를 그립니다.
        /// </summary>
        private void DrawCircularBorder(Rect rect)
        {
            Vector2 center = rect.center;
            float radius = _minimapSize * 0.5f;
            Color orig = GUI.color;
            GUI.color = ColorBorder;

            int segments = 32;
            for (int i = 0; i < segments; i++)
            {
                float angle1 = (i / (float)segments) * Mathf.PI * 2f;
                float angle2 = ((i + 1) / (float)segments) * Mathf.PI * 2f;
                float cos1 = Mathf.Cos(angle1);
                float sin1 = Mathf.Sin(angle1);
                float cos2 = Mathf.Cos(angle2);
                float sin2 = Mathf.Sin(angle2);
                DrawLine(
                    new Vector2(center.x + cos1 * radius, center.y + sin1 * radius),
                    new Vector2(center.x + cos2 * radius, center.y + sin2 * radius),
                    ColorBorder);
            }

            GUI.color = orig;
        }

        /// <summary>
        /// 미니맵 하단에 배율 막대(50m)를 표시합니다.
        /// </summary>
        private void DrawScaleBar(float cx, float cy, float radius)
        {
            // 50m = 50 / (_mapRadiusMeters * _currentZoom) * radius pixels
            float scalePixels = 50f / (_mapRadiusMeters * _currentZoom) * radius;
            scalePixels = Mathf.Min(scalePixels, radius * 1.5f);

            float barY = cy + radius - 48f;
            float barX = cx - scalePixels * 0.5f;

            Color orig = GUI.color;
            GUI.color = ColorScaleBar;

            // Horizontal line
            DrawLine(new Vector2(barX, barY), new Vector2(barX + scalePixels, barY), ColorScaleBar);
            // End ticks
            DrawLine(new Vector2(barX, barY - 6f), new Vector2(barX, barY + 6f), ColorScaleBar);
            DrawLine(new Vector2(barX + scalePixels, barY - 6f), new Vector2(barX + scalePixels, barY + 6f), ColorScaleBar);

            GUI.color = orig;

            // Label
            Rect labelRect = new Rect(barX, barY + 8f, scalePixels, 28f);
            GUI.Label(labelRect, "50m", _labelStyle);
        }

        /// <summary>
        /// GUI 스타일을 초기화합니다.
        /// </summary>
        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _territoryDotStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ColorScaleBar },
            };

            _zoomLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = Color.white },
            };

            _stylesInitialized = true;
        }
    }
}
