using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.UI.Themes;
using ProjectName.UI.Windows;

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
        }

        /// <summary>
        /// 하단 컨트롤 — 확대/축소 버튼과 현재 위치 표시.
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
    }
}