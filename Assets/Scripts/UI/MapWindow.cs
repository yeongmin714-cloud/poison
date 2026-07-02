using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using ProjectName.UI.Themes;

namespace ProjectName.UI
{
    /// <summary>
    /// Phase 3.5: World Map UI — IMGUI-based strategic map.
    /// Shows all 81 territories across 5 regions (4 nations + Empire).
    /// Supports two zoom levels: Overview (all regions) and Nation (single nation's 20 territories).
    /// Each territory displays: name, difficulty stars, owner flag color + emoji,
    /// player position indicator, and fog of war for undiscovered Empire.
    /// </summary>
    public class MapWindow : UIWindow
    {
        [Header("Map Window")]
        [SerializeField] private Transform _mapContainer;       // 지도가 표시될 영역
        [SerializeField] private float _zoomSpeed = 1f;        // 줌 속도

        [Header("Map Layout")]
        [SerializeField] private float _windowPadding = 15f;
        [SerializeField] private float _regionCardWidth = 315;
        [SerializeField] private float _regionCardHeight = 225;
        [SerializeField] private float _territoryCellWidth = 292;
        [SerializeField] private float _territoryCellHeight = 82.5f;
        [SerializeField] private float _gridSpacing = 5f;

        private float _currentZoom = 1f;
        private NationType _selectedNation = NationType.None; // None = overview
        private static bool _empireDiscovered = false;
        private const string EMPIRE_DISCOVERED_KEY = "MapWindow_EmpireDiscovered";

        // Cached territory data per nation for display
        private readonly Dictionary<NationType, List<TerritoryDefinition>> _nationTerritories =
            new Dictionary<NationType, List<TerritoryDefinition>>();

        // Current player position
        private TerritoryId? _playerTerritoryId;

        // Style state
        private GUIStyle _titleStyle;
        private GUIStyle _regionButtonStyle;
        private GUIStyle _territoryCellStyle;
        private GUIStyle _infoLabelStyle;
        private GUIStyle _starStyle;
        private GUIStyle _guardCountStyle; // 캐싱: DrawTerritoryCell의 병사 수 스타일 (GC 방지)
        private bool _stylesInitialized;

        // Phase 40: 자동 이동 관련
        private TerritoryId? _selectedTerritoryId;          // 좌클릭 선택된 영지
        private Vector3 _selectedTerritoryWorldPos;         // 선택된 영지의 월드 좌표
        private bool _showContextMenu = false;              // 우클릭 컨텍스트 메뉴 표시
        private Rect _contextMenuRect;                      // 컨텍스트 메뉴 위치
        private string _contextMenuTerritoryName = "";      // 컨텍스트 메뉴용 영지 이름
        private TerritoryId? _contextMenuTerritoryId;       // 컨텍스트 메뉴용 영지 ID
        private Vector3 _contextMenuWorldPos;               // 컨텍스트 메뉴용 월드 좌표
        private const float TERRITORY_WORLD_Y = 0f;         // 영지의 기본 Y 좌표 (지면)

        // Phase 44: 영지 필터 모드
        private enum TerritoryFilterMode
        {
            All,            // 전체 보기
            MyTerritory,    // 내 영지만
            WarOnly,        // 전쟁 중만
            UnoccupiedOnly, // 미점령만
            ByNation        // 국가별 (현재 선택된 국가 기준)
        }
        private TerritoryFilterMode _currentFilter = TerritoryFilterMode.All;
        private static readonly string[] FilterLabels =
        {
            "🔍 필터: 전체 보기",
            "🔍 필터: 내 영지",
            "🔍 필터: 전쟁 중",
            "🔍 필터: 미점령",
            "🔍 필터: 국가별"
        };
        private int _filterModeCount = 5;

        // Phase 44: 호버 툴팁 상태
        private bool _isHoveringTerritory = false;
        private string _hoverTooltipText = "";
        private Vector2 _hoverMousePos;
        private float _hoverStartTime = 0f;
        private const float HOVER_DELAY = 0.4f; // 0.4초 후 툴팁 표시

        // Phase 44: 우클릭 메뉴에서 영지 소유주 캐시
        private TerritoryOwnership _contextMenuOwnership;

        protected override void Awake()
        {
            base.Awake();
            ApplyTheme(Phase33_Themes.CreateMedievalMapTheme());
            CacheTerritories();
            _empireDiscovered = PlayerPrefs.GetInt(EMPIRE_DISCOVERED_KEY, 0) == 1;
        }

        protected override void OnShow()
        {
            base.OnShow(); // UIWindow.OnShow에서 theme 배경纹理를 처리하므로 중복 DrawTexture 제거

            if (TerritoryDatabase.Instance == null)
            {
                Debug.LogWarning("[MapWindow] TerritoryDatabase.Instance가 null입니다 — 지도를 갱신할 수 없습니다.");
                return;
            }

            Debug.Log("[MapWindow] 열림 — 지도 갱신");
            RefreshMap();
        }

        protected override void OnHide()
        {
            Debug.Log("[MapWindow] 닫힘");
        }

        /// <summary>
        /// 지도 갱신 — 모든 영지 소유권 상태를 TerritoryDatabase에서 다시 로드합니다.
        /// </summary>
        public void RefreshMap()
        {
            CacheTerritories();
            UpdatePlayerPosition();
            UpdateFlagPoleStates();
        }

        /// <summary>
        /// 영지 정의를 캐싱합니다.
        /// </summary>
        private void CacheTerritories()
        {
            _nationTerritories.Clear();

            NationType[] nations = { NationType.East, NationType.West, NationType.South, NationType.North, NationType.Empire, NationType.Dracula };
            foreach (var nation in nations)
            {
                var list = new List<TerritoryDefinition>();
                var defs = TerritoryDatabase.Instance?.GetDefinitionsByNation(nation);
                if (defs != null)
                {
                    foreach (var def in defs)
                    {
                        list.Add(def);
                    }

                    // Sort by index
                    list.Sort((a, b) => a.id.index.CompareTo(b.id.index));
                }
                _nationTerritories[nation] = list;
            }
        }

        /// <summary>
        /// 플레이어 현재 위치를 업데이트합니다.
        /// TerritoryManager.CurrentTerritoryId에서 직접 읽습니다 (리플렉션 제거 — GC 방지).
        /// </summary>
        private void UpdatePlayerPosition()
        {
            // PlayerHealth에 LastTerritoryId 필드가 없으므로(PlayerHealth.cs 확인 완료)
            // TerritoryManager.CurrentTerritoryId를 직접 사용합니다.
            if (TerritoryManager.Instance != null)
            {
                _playerTerritoryId = TerritoryManager.Instance.CurrentTerritoryId;
            }
            else
            {
                _playerTerritoryId = null;
            }
        }

        /// <summary>
        /// 깃대 상태를 업데이트합니다. (FlagManager에 등록된 깃대의 현재 소유주 색상을 실시간 반영)
        /// </summary>
        private void UpdateFlagPoleStates()
        {
            // FlagPoleDisplay updates are handled in real-time by FlagManager.
            // This method exists to force a refresh of territory ownership if needed.
            // The MapWindow reads from TerritoryDatabase states, which FlagManager keeps in sync.
        }

        /// <summary>
        /// 황제국 발견 상태를 설정합니다. (PlayerPrefs 저장)
        /// </summary>
        public static void SetEmpireDiscovered(bool discovered)
        {
            _empireDiscovered = discovered;
            PlayerPrefs.SetInt(EMPIRE_DISCOVERED_KEY, discovered ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 황제국이 발견되었는지 확인합니다.
        /// </summary>
        public static bool IsEmpireDiscovered => _empireDiscovered;

        /// <summary>
        /// 현재 확대/축소 수준을 반환합니다.
        /// </summary>
        public float CurrentZoom => _currentZoom;

        /// <summary>
        /// 현재 선택된 국가 (None = 개요).
        /// </summary>
        public NationType SelectedNation => _selectedNation;

        // ===== IMGUI Rendering =====

        protected override void OnGUI()
        {
            if (!_isOpen) return;

            InitializeStyles();

            // Calculate window area centered on screen
            float windowWidth = Screen.width * 0.85f;
            float windowHeight = Screen.height * 0.8f;
            float windowX = (Screen.width - windowWidth) * 0.5f;
            float windowY = (Screen.height - windowHeight) * 0.5f;

            Rect windowRect = new Rect(windowX, windowY, windowWidth, windowHeight);

            // Background
            GUI.Box(windowRect, "");

            // Phase 33: 테마 데코레이션 (그라디언트 + 장식 테두리)
            DrawThemeDecorations(windowRect);

            // Inner area with padding
            Rect innerRect = new Rect(
                windowRect.x + _windowPadding,
                windowRect.y + _windowPadding,
                windowRect.width - _windowPadding * 2,
                windowRect.height - _windowPadding * 2
            );

            // Title
            Rect titleRect = new Rect(innerRect.x, innerRect.y, innerRect.width, 100f);
            GUI.Label(titleRect, "🗺️ 포이즌 대륙", _titleStyle);

            // Phase 44: 필터 버튼 (좌측 상단, 타이틀 아래)
            Rect filterRect = new Rect(innerRect.x, titleRect.y + titleRect.height - 30f, 260f, 26f);
            Color origFilterBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.15f, 0.25f, 0.4f);
            if (GUI.Button(filterRect, FilterLabels[(int)_currentFilter]))
            {
                _currentFilter = (TerritoryFilterMode)(((int)_currentFilter + 1) % _filterModeCount);
                Debug.Log($"[MapWindow] 🔍 필터 변경: {_currentFilter}");
            }
            GUI.backgroundColor = origFilterBg;

            float contentY = titleRect.y + titleRect.height + 5f;
            Rect contentRect = new Rect(innerRect.x, contentY, innerRect.width, innerRect.height - (contentY - innerRect.y));

            if (_selectedNation == NationType.None)
            {
                DrawOverview(contentRect);
            }
            else
            {
                DrawNationDetail(contentRect);
            }

            // Bottom controls
            float controlsY = windowRect.y + windowRect.height - 35f;
            DrawControls(new Rect(windowRect.x + _windowPadding, controlsY, windowRect.width - _windowPadding * 2, 45f));

            // Phase 40: 우클릭 컨텍스트 메뉴 (자동 이동)
            DrawAutoMoveContextMenu();

            // Phase 44: 호버 툴팁 표시
            DrawTerritoryHoverTooltip();

            // Phase 40: 선택된 영지까지 점선 경로 표시
            DrawMapPathLine(windowRect);
        }

        /// <summary>
        /// Phase 40: 우클릭 컨텍스트 메뉴 — "자동 이동" 옵션
        /// Phase 44: 확장 — 빠른 이동 / 영지 정보 추가
        /// </summary>
        private void DrawAutoMoveContextMenu()
        {
            if (!_showContextMenu) return;

            // 컨텍스트 메뉴 배경
            Color origBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.1f, 0.1f, 0.15f); // 어두운 배경
            GUI.Box(_contextMenuRect, "");
            GUI.backgroundColor = origBg;

            // 테두리
            Color origBorder = GUI.color;
            GUI.color = new Color(0.3f, 0.3f, 0.5f);
            GUI.Box(_contextMenuRect, "");
            GUI.color = origBorder;

            // 메뉴 제목
            float margin = 5f;
            Rect titleRect = new Rect(_contextMenuRect.x + margin, _contextMenuRect.y + margin,
                _contextMenuRect.width - margin * 2, 25f);
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(titleRect, $"📌 {_contextMenuTerritoryName}", titleStyle);

            float btnY = titleRect.y + titleRect.height + 2f;
            float btnHeight = 28f;
            float btnSpacing = 2f;

            // "📍 자동 이동" 버튼
            Rect moveBtnRect = new Rect(_contextMenuRect.x + margin, btnY,
                _contextMenuRect.width - margin * 2, btnHeight);
            Color origBtnColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0f, 0.4f, 0f);
            if (GUI.Button(moveBtnRect, "📍 자동 이동"))
            {
                if (_contextMenuTerritoryId.HasValue)
                {
                    StartAutoMoveToTerritory(_contextMenuTerritoryId.Value, _contextMenuWorldPos);
                }
                _showContextMenu = false;
            }
            GUI.backgroundColor = origBtnColor;

            // "⚡ 빠른 이동" 버튼 (Phase 44: 소유한 영지만)
            bool isOwned = _contextMenuOwnership == TerritoryOwnership.PlayerOwned;
            Rect ftBtnRect = new Rect(_contextMenuRect.x + margin, moveBtnRect.y + moveBtnRect.height + btnSpacing,
                _contextMenuRect.width - margin * 2, btnHeight);
            Color origFtColor = GUI.backgroundColor;
            GUI.backgroundColor = isOwned ? new Color(0.5f, 0.3f, 0.0f) : new Color(0.2f, 0.2f, 0.2f);
            GUI.enabled = isOwned;
            if (GUI.Button(ftBtnRect, isOwned ? "⚡ 빠른 이동" : "⚡ 빠른 이동 (미소유)"))
            {
                if (isOwned && _contextMenuTerritoryId.HasValue)
                {
                    FastTravelUI.Hide(); // 기존 UI 닫고
                    FastTravelUI.Show(); // 새로 열기 — 사용자가 영지 선택
                }
                _showContextMenu = false;
            }
            GUI.enabled = true;
            GUI.backgroundColor = origFtColor;

            // "ℹ️ 영지 정보" 버튼 (Phase 44: 상세 팝업)
            Rect infoBtnRect = new Rect(_contextMenuRect.x + margin, ftBtnRect.y + ftBtnRect.height + btnSpacing,
                _contextMenuRect.width - margin * 2, btnHeight);
            Color origInfoColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.2f, 0.2f, 0.5f);
            if (GUI.Button(infoBtnRect, "ℹ️ 영지 정보"))
            {
                if (_contextMenuTerritoryId.HasValue)
                {
                    TerritoryInfoPopup.Show(_contextMenuTerritoryId.Value);
                }
                _showContextMenu = false;
            }
            GUI.backgroundColor = origInfoColor;

            // 취소 버튼
            Rect cancelBtnRect = new Rect(_contextMenuRect.x + margin, infoBtnRect.y + infoBtnRect.height + btnSpacing,
                _contextMenuRect.width - margin * 2, 22f);
            if (GUI.Button(cancelBtnRect, "취소"))
            {
                _showContextMenu = false;
            }

            // 컨텍스트 메뉴 외부 클릭 시 닫기
            Event evt = Event.current;
            if (evt != null && evt.type == EventType.MouseDown && evt.button == 0)
            {
                if (!_contextMenuRect.Contains(evt.mousePosition))
                {
                    _showContextMenu = false;
                    evt.Use();
                }
            }
        }

        /// <summary>
        /// Phase 40: 지도 위에 선택된 영지까지 점선 경로를 그립니다.
        /// 현재 위치 → 선택/이동 목표까지 시각적 경로 표시.
        /// </summary>
        private void DrawMapPathLine(Rect windowRect)
        {
            // 컨텍스트 메뉴가 열려있을 때는 그리지 않음 (겹침 방지)
            if (_showContextMenu) return;

            // 자동 이동 목표 또는 선택된 영지가 있을 때 경로 표시
            Vector3? targetPos = null;
            string targetName = "";

            // 우선순위: 자동 이동 목표 > 선택된 영지
            if (AutoMoveManager.Instance != null && AutoMoveManager.Instance.HasDestination)
            {
                targetPos = AutoMoveManager.Instance.Destination;
                targetName = "자동 이동 목표";
            }
            else if (_selectedTerritoryId.HasValue)
            {
                targetPos = _selectedTerritoryWorldPos;
                var db = TerritoryDatabase.Instance;
                if (db != null)
                {
                    var def = db.GetDefinition(_selectedTerritoryId.Value);
                    targetName = !string.IsNullOrEmpty(def.territoryName) ? def.territoryName : "선택 영지";
                }
                else
                {
                    targetName = "선택 영지";
                }
            }

            if (!targetPos.HasValue) return;

            // 지도 좌표계에서의 플레이어 위치 (개요/상세 화면에 따라 다름)
            Vector2 playerMapPos = GetPlayerMapPosition(windowRect);
            Vector2 targetMapPos = GetTerritoryMapPosition(targetPos.Value, windowRect);

            // 점선 경로 그리기 (세로로 흐르는 점선)
            Color pathColor = new Color(0f, 1f, 0.8f, 0.6f); // 청록색 반투명
            int segments = 8;
            for (int i = 0; i < segments; i++)
            {
                float t1 = (float)i / segments;
                float t2 = (float)(i + 1) / segments;

                float x1 = Mathf.Lerp(playerMapPos.x, targetMapPos.x, t1);
                float y1 = Mathf.Lerp(playerMapPos.y, targetMapPos.y, t1);
                float x2 = Mathf.Lerp(playerMapPos.x, targetMapPos.x, t2);
                float y2 = Mathf.Lerp(playerMapPos.y, targetMapPos.y, t2);

                // 점선 효과: 짝수 세그먼트만 그림
                if (i % 2 == 0)
                {
                    DrawIMGUILine(x1, y1, x2, y2, pathColor, 2f);
                }
            }

            // 목표 마커
            GUIStyle markerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.green }
            };
            GUI.Label(new Rect(targetMapPos.x - 30f, targetMapPos.y - 15f, 60f, 30f), "📍", markerStyle);
        }

        /// <summary>
        /// Phase 40: 지도 위 플레이어 위치 좌표를 반환합니다. (개요/상세 화면별 추정)
        /// </summary>
        private Vector2 GetPlayerMapPosition(Rect windowRect)
        {
            // 개요 화면: 중앙 하단 (플레이어가 위치한 국가 지역 근처)
            if (_selectedNation == NationType.None)
            {
                return new Vector2(windowRect.x + windowRect.width * 0.5f,
                    windowRect.y + windowRect.height * 0.7f);
            }
            // 상세 화면: 현재 위치 영지 셀 위치 (대략 왼쪽 상단)
            else
            {
                return new Vector2(windowRect.x + 50f, windowRect.y + windowRect.height * 0.4f);
            }
        }

        /// <summary>
        /// Phase 40: 월드 좌표를 지도 좌표로 변환합니다.
        /// </summary>
        private Vector2 GetTerritoryMapPosition(Vector3 worldPos, Rect windowRect)
        {
            // 지도 중심: 화면 중앙
            // 월드 좌표를 지도 좌표로 매핑 (스케일 10 유닛 = 1 지도 픽셀로 가정)
            float centerX = windowRect.x + windowRect.width * 0.5f;
            float centerY = windowRect.y + windowRect.height * 0.3f;

            float mapX = centerX + worldPos.x * 1.5f;
            float mapZ = centerY + worldPos.z * 1.5f;

            // 화면 경계 내 클램프
            mapX = Mathf.Clamp(mapX, windowRect.x + 30f, windowRect.x + windowRect.width - 30f);
            mapZ = Mathf.Clamp(mapZ, windowRect.y + 50f, windowRect.y + windowRect.height - 50f);

            return new Vector2(mapX, mapZ);
        }

        /// <summary>
        /// Phase 40: 선택된 영지로 자동 이동을 시작합니다.
        /// </summary>
        private void StartAutoMoveToTerritory(TerritoryId territoryId, Vector3 worldPos)
        {
            if (AutoMoveManager.Instance == null)
            {
                Debug.LogWarning("[MapWindow] AutoMoveManager 인스턴스가 없습니다! Scene에 AutoMoveManager를 추가해주세요.");
                return;
            }

            // 지도 닫기 (이동 중에는 지도가 필요 없음)
            Hide();

            // 자동 이동 시작
            AutoMoveManager.Instance.SetDestination(worldPos);

            Debug.Log($"[MapWindow] 🚶 자동 이동 시작 → 영지 {territoryId}");
        }

        /// <summary>
        /// Phase 40: IMGUI에서 선을 그리는 헬퍼 메서드.
        /// GUI.DrawTexture와 회전을 사용하여 얇은 선을 그립니다.
        /// </summary>
        private void DrawIMGUILine(float x1, float y1, float x2, float y2, Color color, float thickness)
        {
            float dx = x2 - x1;
            float dy = y2 - y1;
            float length = Mathf.Sqrt(dx * dx + dy * dy);
            if (length < 0.001f) return;

            float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            float cx = (x1 + x2) * 0.5f;
            float cy = (y1 + y2) * 0.5f;

            Color origColor = GUI.color;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(angle, new Vector2(cx, cy));
            GUI.DrawTexture(new Rect(x1, cy - thickness * 0.5f, length, thickness), Texture2D.whiteTexture);
            GUIUtility.RotateAroundPivot(-angle, new Vector2(cx, cy));
            GUI.color = origColor;
        }

        /// <summary>
        /// GUI 스타일을 초기화합니다.
        /// </summary>
        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 96,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _regionButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 56,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                hover = { textColor = Color.yellow },
                active = { textColor = Color.green }
            };

            _territoryCellStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 44,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white },
                richText = true
            };

            _infoLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 52,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) },
                richText = true
            };

            _starStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 56,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.yellow }
            };

            _guardCountStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 36,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) },
                richText = true
            };

            _stylesInitialized = true;
        }

        /// <summary>
        /// 개요 화면 — 5개 지역 카드와 황제국을 표시합니다.
        /// </summary>
        private void DrawOverview(Rect area)
        {
            float totalWidth = area.width;
            float cardWidth = Mathf.Min(_regionCardWidth, (totalWidth - _gridSpacing * 4) / 4f);
            float cardHeight = Mathf.Min(_regionCardHeight, area.height * 0.4f);

            // 4 nation cards in a row
            float startX = area.x + (totalWidth - (cardWidth * 4 + _gridSpacing * 3)) * 0.5f;
            float cardY = area.y + 20f;

            NationType[] nations = { NationType.North, NationType.East, NationType.South, NationType.West };
            string[] labels = { "북부 ❄️", "동부 🌅", "남부 🔥", "서부 🌿" };
            Color[] bgColors = {
                new Color(0.4f, 0.1f, 0.6f), // North purple
                new Color(0.0f, 0.3f, 0.8f), // East blue
                new Color(0.7f, 0.1f, 0.1f), // South red
                new Color(0.1f, 0.5f, 0.1f)  // West green
            };

            for (int i = 0; i < 4; i++)
            {
                Rect cardRect = new Rect(startX + i * (cardWidth + _gridSpacing), cardY, cardWidth, cardHeight);
                Color originalColor = GUI.backgroundColor;
                GUI.backgroundColor = bgColors[i];

                // Get territory count for this nation
                int count = 0;
                if (_nationTerritories.TryGetValue(nations[i], out var defs))
                    count = defs.Count;

                if (GUI.Button(cardRect, $"{labels[i]}\n{count}영지"))
                {
                    _selectedNation = nations[i];
                    Debug.Log($"[MapWindow] 국가 선택: {nations[i]}");
                }

                GUI.backgroundColor = originalColor;
            }

            // Empire card centered below
            float empireCardY = cardY + cardHeight + 20f;
            float empireCardWidth = Mathf.Min(cardWidth * 1.5f, totalWidth * 0.5f);
            float empireCardX = area.x + (totalWidth - empireCardWidth) * 0.5f;
            float empireCardHeight = Mathf.Min(70f, area.height - (empireCardY - area.y) - 30f);

            Rect empireRect = new Rect(empireCardX, empireCardY, empireCardWidth, empireCardHeight);
            Color origBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.6f, 0.5f, 0.1f);

            string empireLabel = _empireDiscovered
                ? "👑 황제국\n(최종 영지)"
                : "👑 황제국\n(안개/미확인) ❓";

            if (GUI.Button(empireRect, empireLabel))
            {
                if (_empireDiscovered)
                {
                    _selectedNation = NationType.Empire;
                    Debug.Log("[MapWindow] 황제국 선택");
                }
                else
                {
                    Debug.Log("[MapWindow] 황제국은 아직 발견되지 않았습니다!");
                }
            }

            GUI.backgroundColor = origBg;

            // 드라큘라 영지 카드 (Night Dracula)
            float draculaCardY = empireCardY + empireCardHeight + 10f;
            float draculaCardWidth = Mathf.Min(cardWidth * 1.5f, totalWidth * 0.5f);
            float draculaCardX = area.x + (totalWidth - draculaCardWidth) * 0.5f;
            float draculaCardHeight = Mathf.Min(60f, area.height - (draculaCardY - area.y) - 30f);

            Rect draculaRect = new Rect(draculaCardX, draculaCardY, draculaCardWidth, draculaCardHeight);
            Color origBg2 = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.3f, 0.0f, 0.1f); // dark red/purple

            string draculaLabel = "🧛 드라큘라의 성\n(야간 전용)";

            if (GUI.Button(draculaRect, draculaLabel))
            {
                _selectedNation = NationType.Dracula;
                Debug.Log("[MapWindow] 드라큘라 영지 선택");
            }

            GUI.backgroundColor = origBg2;
        }

        /// <summary>
        /// 국가 상세 화면 — 20개 영지를 5×4 그리드로 표시합니다.
        /// </summary>
        private void DrawNationDetail(Rect area)
        {
            if (!_nationTerritories.TryGetValue(_selectedNation, out var definitions))
            {
                GUI.Label(area, "영지 데이터 없음", _infoLabelStyle);
                return;
            }

            // Nation header
            string nationHeader = _selectedNation switch
            {
                NationType.East => "🌅 동부 (East)",
                NationType.West => "🌿 서부 (West)",
                NationType.South => "🔥 남부 (South)",
                NationType.North => "❄️ 북부 (North)",
                NationType.Empire => "👑 황제국 (Empire)",
                NationType.Dracula => "🧛 드라큘라 (Night Dracula)",
                _ => "알 수 없음"
            };

            Rect headerRect = new Rect(area.x, area.y, area.width, 45f);
            GUI.Label(headerRect, nationHeader, _titleStyle);

            // Territory grid
            float gridY = area.y + 35f;
            float gridWidth = area.width;
            float gridHeight = area.height - 65f;

            float cellWidth = _territoryCellWidth * _currentZoom;
            float cellHeight = _territoryCellHeight * _currentZoom;

            // Calculate columns based on available width
            int cols = Mathf.Max(1, Mathf.FloorToInt((gridWidth + _gridSpacing) / (cellWidth + _gridSpacing)));
            if (cols > 5) cols = 5;

            float totalGridWidth = cols * cellWidth + (cols - 1) * _gridSpacing;
            float startX = area.x + (gridWidth - totalGridWidth) * 0.5f;

            for (int i = 0; i < definitions.Count; i++)
            {
                var def = definitions[i];
                int row = i / cols;
                int col = i % cols;

                float cellX = startX + col * (cellWidth + _gridSpacing);
                float cellY = gridY + row * (cellHeight + _gridSpacing);

                // Check if cell is within visible area
                if (cellY + cellHeight > gridY + gridHeight) break;

                Rect cellRect = new Rect(cellX, cellY, cellWidth, cellHeight);
                DrawTerritoryCell(cellRect, def);
            }

            // Back button
            string backLabel = "[ ← 뒤로 ]";
            Rect backRect = new Rect(area.x, area.y + area.height - 25f, 150f, 33f);
            if (GUI.Button(backRect, backLabel))
            {
                _selectedNation = NationType.None;
            }
        }

        /// <summary>
        /// 단일 영지 셀을 그립니다.
        /// </summary>
        private void DrawTerritoryCell(Rect rect, TerritoryDefinition def)
        {
            if (TerritoryDatabase.Instance == null)
            {
                GUI.Label(rect, "DB 없음", _infoLabelStyle);
                return;
            }

            TerritoryState state = TerritoryDatabase.Instance.GetState(def.id);

            // 야간 전용 영지 비활성화 처리 (ND-06)
            if (state != null && !state.isActive)
            {
                Color dimBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.1f, 0.1f, 0.15f); // 어두운 안개
                GUI.Box(rect, "");
                GUI.backgroundColor = dimBg;

                float cellMargin = 3f;
                Rect labelRect = new Rect(rect.x + cellMargin, rect.y + cellMargin, rect.width - cellMargin * 2, rect.height - cellMargin * 2);
                GUI.Label(labelRect, "🌙 잠김 (밤에만 출현)", _infoLabelStyle);
                return;
            }

            // Phase 44: 필터 적용 — 조건에 맞지 않으면 흐리게 표시
            bool passesFilter = PassesFilter(def, state);
            float dimAlpha = passesFilter ? 1.0f : 0.3f;

            // 필터 불일치 시 빈 셀로 표시
            if (!passesFilter)
            {
                Color dimBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.08f, 0.08f, 0.1f);
                Color origColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.3f);
                GUI.Box(rect, "");
                GUI.color = origColor;
                GUI.backgroundColor = dimBg;
                return;
            }

            // Determine background color based on difficulty
            Color bgColor = def.difficulty switch
            {
                TerritoryDifficulty.Ring1 => new Color(0.2f, 0.4f, 0.2f),
                TerritoryDifficulty.Ring2 => new Color(0.4f, 0.4f, 0.2f),
                TerritoryDifficulty.Ring3 => new Color(0.5f, 0.3f, 0.1f),
                TerritoryDifficulty.Ring4 => new Color(0.5f, 0.1f, 0.1f),
                TerritoryDifficulty.Empire => new Color(0.4f, 0.35f, 0.1f),
                _ => new Color(0.2f, 0.2f, 0.2f)
            };

            // Owner flag display
            string flagText = "";
            Color flagColor = Color.clear;
            bool showFlag = false;

            if (def.nation == NationType.Empire && !_empireDiscovered)
            {
                flagText = "❓";
            }
            else if (state != null)
            {
                switch (state.ownership)
                {
                    case TerritoryOwnership.Unoccupied:
                        // Show nation's default flag
                        var nationFlag = NationFlagDatabase.GetFlag(def.nation);
                        flagText = nationFlag.symbolEmoji;
                        flagColor = nationFlag.flagColor;
                        showFlag = true;
                        break;
                    case TerritoryOwnership.PlayerOwned:
                        // Player flag
                        if (EmblemManager.Instance != null)
                        {
                            flagText = EmblemManager.GetEmblemSymbol(EmblemManager.Instance.CurrentEmblem.shape);
                            flagColor = EmblemManager.GetEmblemColor(EmblemManager.Instance.CurrentEmblem.primaryColor);
                        }
                        else
                        {
                            flagText = "⚔️";
                            flagColor = Color.cyan;
                        }
                        showFlag = true;
                        break;
                    case TerritoryOwnership.LordOwned:
                        var lordFlag = NationFlagDatabase.GetFlag(def.nation);
                        flagText = lordFlag.symbolEmoji;
                        flagColor = lordFlag.flagColor;
                        showFlag = true;
                        break;
                    case TerritoryOwnership.Contested:
                        flagText = "⚔️";
                        flagColor = Color.yellow;
                        showFlag = true;
                        break;
                }
            }
            else
            {
                // No state yet — show default nation flag
                var defFlag = NationFlagDatabase.GetFlag(def.nation);
                flagText = defFlag.symbolEmoji;
                flagColor = defFlag.flagColor;
                showFlag = true;
            }

            // Player position indicator
            bool isPlayerHere = _playerTerritoryId.HasValue &&
                                _playerTerritoryId.Value.nation == def.nation &&
                                _playerTerritoryId.Value.index == def.id.index;

            // Draw cell background
            Color origBg = GUI.backgroundColor;
            GUI.backgroundColor = bgColor;
            GUI.Box(rect, "");
            GUI.backgroundColor = origBg;

            // Draw cell content
            float margin = 3f;
            float cellInnerX = rect.x + margin;
            float cellInnerY = rect.y + margin;
            float cellInnerW = rect.width - margin * 2;
            float lineHeight = 21f;

            // Line 1: Territory name + difficulty stars
            string difficultyStars = def.difficulty switch
            {
                TerritoryDifficulty.Ring1 => "⭐",
                TerritoryDifficulty.Ring2 => "⭐⭐",
                TerritoryDifficulty.Ring3 => "⭐⭐⭐",
                TerritoryDifficulty.Ring4 => "⭐⭐⭐⭐",
                TerritoryDifficulty.Empire => "👑",
                _ => ""
            };

            string nameText = $"{def.territoryName} {difficultyStars}";
            Rect nameRect = new Rect(cellInnerX, cellInnerY, cellInnerW, lineHeight);
            GUI.Label(nameRect, nameText, _infoLabelStyle);

            // Line 2: Flag + ownership text
            float flagY = cellInnerY + lineHeight + 1f;
            if (showFlag)
            {
                // Draw flag color rectangle
                Rect flagColorRect = new Rect(cellInnerX, flagY, 24f, 18f);
                Color origGuiColor = GUI.color;
                GUI.color = flagColor;
                GUI.Box(flagColorRect, "");
                GUI.color = origGuiColor;

                // Flag emoji text
                Rect flagTextRect = new Rect(cellInnerX + 20f, flagY, cellInnerW - 20f, lineHeight);
                string ownerText = state != null ? state.ownership.ToString() : def.nation.ToString();
                GUI.Label(flagTextRect, $"{flagText} {ownerText}", _infoLabelStyle);
            }
            else
            {
                // Fog of war
                Rect fogRect = new Rect(cellInnerX, flagY, cellInnerW, lineHeight);
                GUI.Label(fogRect, "❓ 미확인", _infoLabelStyle);
            }

            // Line 3: Player position indicator
            if (isPlayerHere)
            {
                float posY = flagY + lineHeight + 1f;
                Rect posRect = new Rect(cellInnerX, posY, cellInnerW, lineHeight);
                GUI.Label(posRect, "📍 현재 위치", _infoLabelStyle);

                // Highlight cell border
                Color origBorderColor = GUI.color;
                GUI.color = Color.cyan;
                GUI.Box(rect, "");
                GUI.color = origBorderColor;
            }

            // Difficulty/guard count info
            float guardY = rect.y + rect.height - lineHeight - margin;
            Rect guardRect = new Rect(cellInnerX, guardY, cellInnerW, lineHeight);
            GUI.Label(guardRect, $"병사: {def.guardCount}명", _guardCountStyle);

            // Phase 44: 호버 툴팁 감지
            CheckTerritoryHover(rect, def, state);

            // Phase 40: 클릭 감지 (좌클릭 = 선택, 우클릭 = 자동 이동 컨텍스트 메뉴)
            HandleTerritoryCellClick(rect, def, state);
        }

        /// <summary>
        /// Phase 40: 영지 셀 클릭 처리 — 좌클릭 선택, 우클릭 컨텍스트 메뉴.
        /// Phase 44: 우클릭 메뉴에 빠른 이동/영지 정보 추가.
        /// </summary>
        private void HandleTerritoryCellClick(Rect cellRect, TerritoryDefinition def, TerritoryState state)
        {
            Event evt = Event.current;
            if (evt == null) return;
            if (evt.type != EventType.MouseDown) return;

            // 셀 영역 내 클릭인지 확인
            if (!cellRect.Contains(evt.mousePosition)) return;

            if (evt.button == 0) // 좌클릭 — 영지 선택
            {
                _selectedTerritoryId = def.id;
                _selectedTerritoryWorldPos = new Vector3(
                    def.id.index * 10f, // 영지의 대략적 X 좌표 (실제 월드 좌표로 대체 필요)
                    TERRITORY_WORLD_Y,
                    (int)def.nation * 10f // 영지의 대략적 Z 좌표
                );

                Debug.Log($"[MapWindow] 🎯 영지 선택: {def.territoryName} (ID: {def.id})");
                evt.Use();
            }
            else if (evt.button == 1) // 우클릭 — 컨텍스트 메뉴
            {
                // 컨텍스트 메뉴 위치 설정 (마우스 위치) — Phase 44: 높이 증가
                _contextMenuRect = new Rect(evt.mousePosition.x, evt.mousePosition.y, 220f, 150f);
                _contextMenuTerritoryName = def.territoryName;
                _contextMenuTerritoryId = def.id;
                _contextMenuOwnership = state != null ? state.ownership : TerritoryOwnership.Unoccupied;
                _contextMenuWorldPos = new Vector3(
                    def.id.index * 10f,
                    TERRITORY_WORLD_Y,
                    (int)def.nation * 10f
                );

                _showContextMenu = true;
                Debug.Log($"[MapWindow] 📌 우클릭 — {def.territoryName} 컨텍스트 메뉴");
                evt.Use();
            }
        }

        /// <summary>
        /// 하단 컨트롤 — 확대/축소 버튼, 자동 이동 버튼, 현재 위치 표시.
        /// </summary>
        private void DrawControls(Rect area)
        {
            // Zoom buttons
            float btnWidth = 90;
            float btnHeight = 36f;

            Rect zoomOutRect = new Rect(area.x, area.y, btnWidth, btnHeight);
            if (GUI.Button(zoomOutRect, "[-]"))
            {
                _currentZoom = Mathf.Max(0.5f, _currentZoom - 0.25f * _zoomSpeed);
            }

            Rect zoomResetRect = new Rect(area.x + btnWidth + 5f, area.y, btnWidth, btnHeight);
            if (GUI.Button(zoomResetRect, "[현재]"))
            {
                _currentZoom = 1f;
            }

            Rect zoomInRect = new Rect(area.x + (btnWidth + 5f) * 2, area.y, btnWidth, btnHeight);
            if (GUI.Button(zoomInRect, "[+]"))
            {
                _currentZoom = Mathf.Min(2f, _currentZoom + 0.25f * _zoomSpeed);
            }

            // ⚡ Fast Travel: 빠른 이동 버튼 (항상 표시)
            float ftBtnWidth = 130f;
            float ftBtnX = area.x + area.width - ftBtnWidth - 260f; // 위치 레이블 왼쪽
            Rect ftBtnRect = new Rect(ftBtnX, area.y, ftBtnWidth, btnHeight);
            Color origFtColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.5f, 0.3f, 0.0f); // 황금색
            if (GUI.Button(ftBtnRect, "⚡ 빠른 이동"))
            {
                FastTravelUI.Show();
            }
            GUI.backgroundColor = origFtColor;

            // Phase 40: 자동 이동 버튼 (영지 선택 시 활성화)
            if (_selectedTerritoryId.HasValue)
            {
                float moveBtnWidth = 180f;
                float moveBtnX = area.x + (btnWidth + 5f) * 3 + 10f;
                Rect moveBtnRect = new Rect(moveBtnX, area.y, moveBtnWidth, btnHeight);

                string selectedName = "선택된 영지";
                var db = TerritoryDatabase.Instance;
                if (db != null)
                {
                    var def = db.GetDefinition(_selectedTerritoryId.Value);
                    if (!string.IsNullOrEmpty(def.territoryName))
                        selectedName = def.territoryName;
                }

                Color origBtnColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0f, 0.5f, 0f); // 녹색

                if (GUI.Button(moveBtnRect, $"🚶 {selectedName}으로 이동"))
                {
                    StartAutoMoveToTerritory(_selectedTerritoryId.Value, _selectedTerritoryWorldPos);
                }

                GUI.backgroundColor = origBtnColor;

                // 선택 초기화 버튼
                Rect clearBtnRect = new Rect(moveBtnX + moveBtnWidth + 5f, area.y, 60f, btnHeight);
                if (GUI.Button(clearBtnRect, "✕ 취소"))
                {
                    _selectedTerritoryId = null;
                }
            }

            // Current position label
            string posText = "📍 현재 위치: ";
            if (_playerTerritoryId.HasValue)
            {
                var db = TerritoryDatabase.Instance;
                var def = db != null ? db.GetDefinition(_playerTerritoryId.Value) : default;
                posText += !string.IsNullOrEmpty(def.territoryName) ? def.territoryName : "알 수 없음";
            }
            else
            {
                posText += "알 수 없음";
            }

            Rect posRect = new Rect(area.x + area.width - 250f, area.y, 375f, btnHeight);
            GUI.Label(posRect, posText, _infoLabelStyle);
        }

        // ===== Phase 44: Filtermethod =====

        /// <summary>
        /// 현재 필터 모드에 따라 영지 표시 여부를 결정합니다.
        /// </summary>
        private bool PassesFilter(TerritoryDefinition def, TerritoryState state)
        {
            if (_currentFilter == TerritoryFilterMode.All)
                return true;

            if (_currentFilter == TerritoryFilterMode.ByNation)
            {
                // 국가별 필터: 현재 선택된 국가의 영지만 표시
                if (_selectedNation != NationType.None)
                    return def.nation == _selectedNation;
                return true; // 개요 화면에서는 전체 표시
            }

            // MyTerritory, WarOnly, UnoccupiedOnly — 상태 기반
            if (state == null) return false;

            switch (_currentFilter)
            {
                case TerritoryFilterMode.MyTerritory:
                    return state.ownership == TerritoryOwnership.PlayerOwned;
                case TerritoryFilterMode.WarOnly:
                    return state.isUnderAttack || state.ownership == TerritoryOwnership.Contested;
                case TerritoryFilterMode.UnoccupiedOnly:
                    return state.ownership == TerritoryOwnership.Unoccupied;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Phase 44: 마우스 호버 시 영지 정보 툴팁을 준비합니다.
        /// </summary>
        private void CheckTerritoryHover(Rect cellRect, TerritoryDefinition def, TerritoryState state)
        {
            Event evt = Event.current;
            if (evt == null) return;
            if (evt.type != EventType.Repaint) return;

            Vector2 mousePos = evt.mousePosition;
            bool mouseOver = cellRect.Contains(mousePos);

            if (mouseOver)
            {
                if (!_isHoveringTerritory)
                {
                    // 호버 시작
                    _isHoveringTerritory = true;
                    _hoverStartTime = Time.realtimeSinceStartup;
                    _hoverMousePos = mousePos;
                    _hoverTooltipText = BuildHoverTooltipText(def, state);
                }
                else
                {
                    // 마우스 위치 업데이트
                    _hoverMousePos = mousePos;
                }
            }
            else if (_isHoveringTerritory)
            {
                // 같은 프레임에 다른 영지로 이동했는지 확인
                _isHoveringTerritory = false;
            }
        }

        /// <summary>
        /// Phase 44: 호버 툴팁 문자열을 생성합니다.
        /// </summary>
        private string BuildHoverTooltipText(TerritoryDefinition def, TerritoryState state)
        {
            string name = def.territoryName;
            string nationStr = def.nation switch
            {
                NationType.East => "동 (East)",
                NationType.West => "서 (West)",
                NationType.South => "남 (South)",
                NationType.North => "북 (North)",
                NationType.Empire => "황제국 (Empire)",
                NationType.Dracula => "드라큘라",
                _ => "알 수 없음"
            };
            string difficultyStr = def.difficulty switch
            {
                TerritoryDifficulty.Ring1 => "⭐ (Ring 1)",
                TerritoryDifficulty.Ring2 => "⭐⭐ (Ring 2)",
                TerritoryDifficulty.Ring3 => "⭐⭐⭐ (Ring 3)",
                TerritoryDifficulty.Ring4 => "⭐⭐⭐⭐ (Ring 4)",
                TerritoryDifficulty.Empire => "👑 (Empire)",
                _ => ""
            };
            string ownerStr = "미점령";
            if (state != null)
            {
                ownerStr = state.ownership switch
                {
                    TerritoryOwnership.Unoccupied => $"미점령 ({def.nation} 영토)",
                    TerritoryOwnership.PlayerOwned => "👤 플레이어",
                    TerritoryOwnership.LordOwned => $"🔴 {def.lord.lordName}",
                    TerritoryOwnership.Contested => "⚔️ 전쟁 중",
                    _ => "알 수 없음"
                };
            }

            // 병사 수 요약
            int guardCount = def.guardCount;
            string guardSummary = $"{guardCount}명";
            if (state != null)
            {
                float aliveRatio = state.guardAliveRatio;
                if (aliveRatio < 1f)
                {
                    int alive = Mathf.Max(0, Mathf.RoundToInt(guardCount * aliveRatio));
                    guardSummary = $"{alive}/{guardCount}명 (생존)";
                }
            }

            // 상태 아이콘
            string statusIcons = "";
            bool isPlayerHere = _playerTerritoryId.HasValue &&
                                _playerTerritoryId.Value.nation == def.nation &&
                                _playerTerritoryId.Value.index == def.id.index;
            if (isPlayerHere)
                statusIcons += " 📍";
            if (state != null && state.isUnderAttack)
                statusIcons += " ⚔️";
            if (state != null && state.ownership == TerritoryOwnership.Contested)
                statusIcons += " ⚔️";

            // 축제 확인
            if (FestivalManager.Instance != null)
            {
                var festival = FestivalManager.Instance.GetActiveFestivalAtTerritory(def.id);
                if (festival != null)
                    statusIcons += " 🎪";
            }

            return $"{name} ({nationStr})\n{difficultyStr}\n소유주: {ownerStr}\n병사: {guardSummary}{statusIcons}";
        }

        /// <summary>
        /// Phase 44: 호버 툴팁을 화면에 그립니다.
        /// </summary>
        private void DrawTerritoryHoverTooltip()
        {
            if (!_isHoveringTerritory) return;
            if (string.IsNullOrEmpty(_hoverTooltipText)) return;

            // 호버 지연 확인
            if (Time.realtimeSinceStartup - _hoverStartTime < HOVER_DELAY) return;

            float tooltipWidth = 320f;
            float lineHeight = 22f;
            int lineCount = _hoverTooltipText.Split('\n').Length;
            float tooltipHeight = lineCount * lineHeight + 10f;

            // 마우스 위치 기준 툴팁 위치 (마우스 오른쪽 아래)
            float tooltipX = _hoverMousePos.x + 15f;
            float tooltipY = _hoverMousePos.y + 10f;

            // 화면 경계 내로 보정
            if (tooltipX + tooltipWidth > Screen.width)
                tooltipX = _hoverMousePos.x - tooltipWidth - 10f;
            if (tooltipY + tooltipHeight > Screen.height)
                tooltipY = Screen.height - tooltipHeight - 5f;

            Rect tooltipRect = new Rect(tooltipX, tooltipY, tooltipWidth, tooltipHeight);

            // 배경
            Color origBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 0.95f);
            GUI.Box(tooltipRect, "");
            GUI.backgroundColor = origBg;

            // 테두리
            Color origColor = GUI.color;
            GUI.color = new Color(0.3f, 0.4f, 0.6f, 0.8f);
            GUI.Box(tooltipRect, "");
            GUI.color = origColor;

            // 텍스트
            Rect textRect = new Rect(tooltipRect.x + 5f, tooltipRect.y + 5f, tooltipRect.width - 10f, tooltipRect.height - 10f);
            GUIStyle tooltipStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = Color.white },
                richText = true,
                wordWrap = true
            };
            GUI.Label(textRect, _hoverTooltipText, tooltipStyle);
        }
    }
}