using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using ProjectName.UI.Themes;
#pragma warning disable 0414

namespace ProjectName.UI
{
    /// <summary>
    /// 인벤토리 윈도우 — I 키로 열기/닫기.
    /// 레퍼런스 스타일의 다크 테마 IMGUI로 구성.
    /// 상단 타이틀 바 → 카테고리 탭 → 아이템 슬롯 그리드 → 하단 상세 정보 패널
    /// </summary>
    public class InventoryWindow : UIWindow
    {
        [Header("Inventory Window")]
        [SerializeField] private Transform _itemGridContainer;  // (Canvas 모드용, 현재 미사용)
        [SerializeField] private GameObject _itemSlotPrefab;    // (Canvas 모드용, 현재 미사용)

        [Header("Categories")]
        [SerializeField] private PlayerInventory.ItemCategory _selectedCategory = PlayerInventory.ItemCategory.Herb;

        [Header("Info Panel")]
        [SerializeField] private string _selectedItemName = "";
        [SerializeField] private string _selectedItemDesc = "";
        [SerializeField] private int _selectedItemCount = 0;

        // 현재 표시중인 아이템 데이터
        private PlayerInventory.ItemSlot[] _currentSlots;
        private Vector2 _scrollPosition;
        private Vector2 _infoScrollPosition;
        private int _selectedSlotIndex = -1;

        // ===== 🗺️ 오토루트 컨텍스트 메뉴 =====
        private bool _showRouteContextMenu = false;
        private Rect _routeContextMenuRect;
        private string _routeContextItemName = "";
        private string _routeContextItemId = "";
        private string _routeContextTerritoryName = "";
        private string _routeContextTerritoryId = "";
        private PlayerInventory.ItemSlot _routeContextSlot;

        // ===== 정렬 =====
        private enum SortMode { None, Category, Name, Rarity, Quantity }
        private SortMode _sortMode = SortMode.None;
        private string[] _sortModeLabels = { "정렬 안함", "카테고리순", "이름순", "등급순", "수량순" };

        // ===== 레퍼런스 스타일 상수 =====
        private const float WINDOW_WIDTH = 1000f;
        private const float WINDOW_HEIGHT = 1000f;
        private const float TITLE_BAR_HEIGHT = 90f;
        private const float TAB_BAR_HEIGHT = 81f;
        private const float INFO_PANEL_HEIGHT = 240f;
        private const int GRID_COLUMNS = 3;
        private const float SLOT_MARGIN = 12f;

        // ===== 다크 테마 색상 =====
        private static readonly Color ColorBg = new Color(0.18f, 0.13f, 0.16f, 0.92f);        // 전체 배경 (어두운 보라빛 회색)
        private static readonly Color ColorTitleBar = new Color(0.12f, 0.09f, 0.11f, 1f);      // 타이틀 바 (더 어두움)
        private static readonly Color ColorTabActive = new Color(0.35f, 0.25f, 0.20f, 1f);     // 활성 탭 (갈색빛)
        private static readonly Color ColorTabInactive = new Color(0.20f, 0.16f, 0.14f, 1f);   // 비활성 탭
        private static readonly Color ColorSlotBg = new Color(0.22f, 0.17f, 0.14f, 0.9f);      // 슬롯 배경
        private static readonly Color ColorSlotHover = new Color(0.35f, 0.25f, 0.18f, 0.9f);   // 슬롯 호버
        private static readonly Color ColorSlotSelected = new Color(0.40f, 0.28f, 0.20f, 1f);  // 슬롯 선택
        private static readonly Color ColorInfoBg = new Color(0.15f, 0.11f, 0.13f, 0.95f);     // 정보 패널 배경
        private static readonly Color ColorTextPrimary = new Color(0.92f, 0.88f, 0.80f, 1f);   // 기본 텍스트
        private static readonly Color ColorTextSecondary = new Color(0.70f, 0.65f, 0.60f, 1f); // 보조 텍스트
        private static readonly Color ColorTextDim = new Color(0.50f, 0.45f, 0.40f, 1f);       // 흐린 텍스트
        private static readonly Color ColorAccent = new Color(0.80f, 0.60f, 0.20f, 1f);        // 강조 (황금색)
        private static readonly Color ColorBorder = new Color(0.12f, 0.09f, 0.11f, 1f);        // 테두리

        // ===== 커스텀 GUIStyle 캐시 =====
        private GUIStyle _styleTitle;
        private GUIStyle _styleTab;
        private GUIStyle _styleTabActive;
        private GUIStyle _styleSlot;
        private GUIStyle _styleSlotSelected;
        private GUIStyle _styleSlotLabel;
        private GUIStyle _styleItemName;
        private GUIStyle _styleItemCount;
        private GUIStyle _styleInfoName;
        private GUIStyle _styleInfoDesc;
        private GUIStyle _styleInfoLabel;
        private GUIStyle _styleEmptyText;
        private GUIStyle _stylePanelBox;
        private bool _stylesInitialized;
        private Texture2D _texWhite;

        protected override void Awake()
        {
            base.Awake();
            ApplyTheme(Phase33_Themes.CreateInventoryTheme());
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_texWhite != null)
            {
                Destroy(_texWhite);
                _texWhite = null;
            }
        }

        protected override void OnShow()
        {

            _selectedSlotIndex = -1;
            RefreshInventory();
        }

        protected override void OnHide()
        {

        }

        /// <summary>
        /// 스타일 초기화 (OnGUI에서 최초 1회)
        /// </summary>
        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _texWhite = MakeTexture(1, 1, Color.white);

            // 타이틀
            _styleTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 72,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ColorTextPrimary },
                padding = new RectOffset(21, 4, 0, 0)
            };

            // 탭 (비활성)
            _styleTab = new GUIStyle(GUI.skin.button)
            {
                fontSize = 48,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(6, 6, 4, 4),
                normal = { textColor = ColorTextSecondary, background = MakeTexture(1, 1, ColorTabInactive) },
                hover = { textColor = ColorTextPrimary, background = MakeTexture(1, 1, ColorSlotHover) },
                active = { textColor = ColorTextPrimary, background = MakeTexture(1, 1, ColorTabActive) },
                border = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(1, 1, 2, 2)
            };

            // 탭 (활성)
            _styleTabActive = new GUIStyle(_styleTab)
            {
                normal = { textColor = ColorTextPrimary, background = MakeTexture(1, 1, ColorTabActive) },
                fontStyle = FontStyle.Bold
            };

            // 슬롯 배경
            _styleSlot = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTexture(1, 1, ColorSlotBg), textColor = ColorTextPrimary },
                hover = { background = MakeTexture(1, 1, ColorSlotHover), textColor = ColorTextPrimary },
                border = new RectOffset(2, 2, 2, 2),
                padding = new RectOffset(4, 4, 4, 4),
                margin = new RectOffset(2, 2, 2, 2),
                fontSize = 48,
                alignment = TextAnchor.MiddleCenter
            };

            // 선택된 슬롯 배경
            _styleSlotSelected = new GUIStyle(_styleSlot)
            {
                normal = { background = MakeTexture(1, 1, ColorSlotSelected), textColor = ColorTextPrimary }
            };

            // 슬롯 라벨 (이름)
            _styleSlotLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 44,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ColorTextPrimary },
                wordWrap = true
            };

            // 아이템 이름 (목록형)
            _styleItemName = new GUIStyle(GUI.skin.label)
            {
                fontSize = 48,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ColorTextPrimary }
            };

            // 아이템 개수
            _styleItemCount = new GUIStyle(GUI.skin.label)
            {
                fontSize = 44,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = ColorAccent }
            };

            // 정보 패널 - 이름
            _styleInfoName = new GUIStyle(GUI.skin.label)
            {
                fontSize = 60,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ColorAccent },
                padding = new RectOffset(0, 0, 2, 0)
            };

            // 정보 패널 - 설명
            _styleInfoDesc = new GUIStyle(GUI.skin.label)
            {
                fontSize = 48,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = ColorTextSecondary },
                wordWrap = true,
                padding = new RectOffset(0, 0, 2, 0)
            };

            // 정보 패널 - 레이블
            _styleInfoLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 44,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ColorTextDim }
            };

            // 빈 목록 텍스트
            _styleEmptyText = new GUIStyle(GUI.skin.label)
            {
                fontSize = 52,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ColorTextDim }
            };

            // 패널 외곽 박스
            _stylePanelBox = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTexture(1, 1, ColorBg), textColor = ColorTextPrimary },
                border = new RectOffset(2, 2, 2, 2),
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };

            _stylesInitialized = true;
        }

        // ===================================================================
        // OnGUI — IMGUI 렌더링
        // ===================================================================
        protected override void OnGUI()
        {
            if (!IsOpen) return;

            base.OnGUI();

            // G3-05: 통일 스타일 — 딤드 오버레이 + 배경 + 타이틀 + 닫기 버튼
            UIStyleManager.DrawDimOverlay();
            float x = (Screen.width - WINDOW_WIDTH) / 2;
            float y = (Screen.height - WINDOW_HEIGHT) / 2;
            Rect winRect = new Rect(x, y, WINDOW_WIDTH, WINDOW_HEIGHT);
            UIStyleManager.DrawWindowBackground(winRect);
            UIStyleManager.DrawTitle(winRect, "  📦 인벤토리");
            if (UIStyleManager.DrawCloseButton(winRect))
            {
                Hide();
                return;
            }

            InitStyles();

            // === 배경 + 외곽 박스 ===
            GUI.Box(new Rect(x, y, WINDOW_WIDTH, WINDOW_HEIGHT), "", _stylePanelBox);

            // === 상단 테두리 라인 ===
            DrawColoredRect(new Rect(x, y, WINDOW_WIDTH, 2), ColorBorder);

            // === 타이틀 바 ===
            DrawColoredRect(new Rect(x, y + 2, WINDOW_WIDTH, TITLE_BAR_HEIGHT), ColorTitleBar);
            GUI.Label(new Rect(x, y + 2, WINDOW_WIDTH, TITLE_BAR_HEIGHT), "  📦 인벤토리", _styleTitle);

            // 정렬 버튼 (타이틀 바 우측)
            float sortBtnWidth = 280f;
            float sortBtnHeight = 66f;
            float sortBtnX = x + WINDOW_WIDTH - sortBtnWidth - 12f;
            float sortBtnY = y + 12f;
            if (GUI.Button(new Rect(sortBtnX, sortBtnY, sortBtnWidth, sortBtnHeight), $"📊 {_sortModeLabels[(int)_sortMode]}"))
            {
                _sortMode = (SortMode)(((int)_sortMode + 1) % 5);
                if (_sortMode != SortMode.None)
                {
                    SortInventory();
                    RefreshInventory();
                }
                else
                {
                    RefreshInventory();
                }
            }

            // 타이틀 하단 구분선
            DrawColoredRect(new Rect(x, y + TITLE_BAR_HEIGHT + 2, WINDOW_WIDTH, 2), ColorBorder);

            // === 카테고리 탭 ===
            float tabY = y + TITLE_BAR_HEIGHT + 4;
            DrawColoredRect(new Rect(x, tabY, WINDOW_WIDTH, TAB_BAR_HEIGHT), ColorTitleBar);
            DrawCategoryTabs(x, tabY);
            DrawColoredRect(new Rect(x, tabY + TAB_BAR_HEIGHT, WINDOW_WIDTH, 1), ColorBorder);

            // === 아이템 슬롯 그리드 (스크롤 가능) ===
            float gridY = tabY + TAB_BAR_HEIGHT + 1;
            float gridHeight = WINDOW_HEIGHT - (gridY - y) - INFO_PANEL_HEIGHT - 4;
            DrawItemGrid(x, gridY, gridHeight);

            // === 하단 정보 패널 ===
            float infoY = gridY + gridHeight + 2;
            DrawInfoPanel(x, infoY);

            // === 🗺️ 오토루트 컨텍스트 메뉴 ===
            DrawRouteContextMenu();
        }

        // ===================================================================
        // 카테고리 탭 그리기
        // ===================================================================
        private void DrawCategoryTabs(float panelX, float tabY)
        {
            string[] tabNames = { "🌿 약초", "🥩 고기", "🍲 요리", "🧪 약", "🧱 재료", "📜 퀘스트", "🗡️ 무기", "🛡️ 방어구", "🔧 도구" };
            PlayerInventory.ItemCategory[] categories =
            {
                PlayerInventory.ItemCategory.Herb,
                PlayerInventory.ItemCategory.Meat,
                PlayerInventory.ItemCategory.Food,
                PlayerInventory.ItemCategory.Potion,
                PlayerInventory.ItemCategory.Material,
                PlayerInventory.ItemCategory.Quest,
                PlayerInventory.ItemCategory.Weapon,
                PlayerInventory.ItemCategory.Armor,
                PlayerInventory.ItemCategory.Tool
            };

            float tabWidth = (WINDOW_WIDTH - 8) / tabNames.Length;
            float tx = panelX + 4;

            for (int i = 0; i < tabNames.Length; i++)
            {
                bool isActive = _selectedCategory == categories[i];
                var style = isActive ? _styleTabActive : _styleTab;

                if (GUI.Button(new Rect(tx, tabY + 2, tabWidth - 2, TAB_BAR_HEIGHT - 4), tabNames[i], style))
                {
                    if (!isActive)
                    {
                        _selectedCategory = categories[i];
                        _selectedSlotIndex = -1;
                        RefreshInventory();
                    }
                }
                tx += tabWidth;
            }
        }

        // ===================================================================
        // 아이템 슬롯 그리드
        // ===================================================================
        private void DrawItemGrid(float panelX, float gridY, float gridHeight)
        {
            float innerX = panelX + 4;
            float innerY = gridY + 2;
            float innerWidth = WINDOW_WIDTH - 8;

            // 스크롤 뷰
            float slotTotalWidth = innerWidth;
            float slotWidth = (slotTotalWidth - SLOT_MARGIN * (GRID_COLUMNS + 1)) / GRID_COLUMNS;
            float slotHeight = 162;
            float rowHeight = slotHeight + SLOT_MARGIN;

            int totalSlots = _currentSlots != null ? _currentSlots.Length : 0;
            int totalRows = Mathf.Max(1, Mathf.CeilToInt((float)totalSlots / GRID_COLUMNS));
            float contentHeight = totalRows * rowHeight + SLOT_MARGIN;
            float viewHeight = gridHeight - 4;

            // 배경
            DrawColoredRect(new Rect(panelX, gridY, WINDOW_WIDTH, gridHeight), ColorBg);

            _scrollPosition = GUI.BeginScrollView(
                new Rect(innerX, innerY, innerWidth, viewHeight),
                _scrollPosition,
                new Rect(0, 0, innerWidth - 20, contentHeight)
            );

            if (_currentSlots == null || _currentSlots.Length == 0)
            {
                GUI.Label(new Rect(0, 20, innerWidth - 20, 45), "(이 카테고리에 아이템이 없습니다)", _styleEmptyText);
            }
            else
            {
                for (int i = 0; i < _currentSlots.Length; i++)
                {
                    var slot = _currentSlots[i];
                    if (slot == null || slot.item == null || slot.count <= 0) continue;

                    int col = i % GRID_COLUMNS;
                    int row = i / GRID_COLUMNS;

                    float sx = SLOT_MARGIN + col * (slotWidth + SLOT_MARGIN);
                    float sy = SLOT_MARGIN + row * rowHeight;

                    Rect slotRect = new Rect(sx, sy, slotWidth, slotHeight);
                    bool isSelected = (i == _selectedSlotIndex);

                    // 슬롯 배경
                    var slotStyle = isSelected ? _styleSlotSelected : _styleSlot;
                    GUI.Box(slotRect, "", slotStyle);

                    // 아이콘 (ItemIconDatabase 사용 — 64×64 생성, 슬롯에 맞게 40×40 표시)
                    Texture2D iconTex = ItemIconDatabase.GetOrCreateIcon(slot.item);
                    if (iconTex != null)
                    {
                        GUI.DrawTexture(new Rect(sx + 6, sy + 4, 90, 90), iconTex);
                    }
                    else
                    {
                        // 폴백: 카테고리 색상 사각형
                        Color iconColor = GetCategoryColor(slot.item.category);
                        GUI.color = iconColor;
                        GUI.DrawTexture(new Rect(sx + 6, sy + 4, 90, 90), _texWhite);
                        GUI.color = Color.white;
                    }

                    // 아이템 이름
                    float nameY = sy + 38;
                    float nameWidth = slotWidth - 12;
                    GUI.Label(new Rect(sx + 6, nameY, nameWidth, 24),
                        TruncateText(slot.item.displayName, nameWidth, _styleSlotLabel),
                        _styleSlotLabel);

                    // 개수
                    GUI.Label(new Rect(sx + 6, nameY + 14, nameWidth, 21),
                        $"x{slot.count}",
                        _styleItemCount);

                    // C9-18: 내구도 표시 (장비 아이템만)
                    if (slot.item.maxDurability > 0)
                    {
                        float durability = ProjectName.Systems.EquipmentDurabilitySystem.GetDurabilityRatio(slot);
                        Color durColor = durability >= 0.6f ? Color.green :
                                         durability >= 0.3f ? Color.yellow : Color.red;
                        float barWidth = slotWidth - 12;
                        float barHeight = 4f;
                        float barY = sy + slotHeight - 6f;

                        // 배경
                        DrawColoredRect(new Rect(sx + 6, barY, barWidth, barHeight), new Color(0.15f, 0.15f, 0.15f, 0.8f));
                        // 내구도 채움
                        DrawColoredRect(new Rect(sx + 6, barY, barWidth * Mathf.Clamp01(durability), barHeight), durColor);
                    }

                    // 클릭 처리 (호버 영역) — 🗺️ 오토루트 우클릭 연동
                    if (Event.current.type == EventType.MouseDown && slotRect.Contains(Event.current.mousePosition))
                    {
                        if (Event.current.button == 0) // 좌클릭 — 선택
                        {
                            _selectedSlotIndex = i;
                            _selectedItemName = slot.item.displayName;
                            _selectedItemDesc = slot.item.description;
                            _selectedItemCount = slot.count;
                            Event.current.Use();
                        }
                        else if (Event.current.button == 1) // 우클릭 — 🗺️ 오토루트 컨텍스트 메뉴
                        {
                            // 장비 아이템이 아닌 경우만 오토루트 표시
                            if (!CompareTooltip.IsEquipmentCategory(slot.item.category))
                            {
                                // AutoRouteSystem에서 경로 조회
                                if (AutoRouteSystem.Instance != null)
                                {
                                    var route = AutoRouteSystem.Instance.GetRouteForItem(slot.item.id);
                                    if (route.HasValue)
                                    {
                                        var routeData = route.Value;
                                        // 영지 이름 조회
                                        string territoryName = routeData.territoryId;
                                        if (TerritoryDatabase.Instance != null)
                                        {
                                            var def = TerritoryDatabase.Instance.GetDefinition(routeData.territoryId);
                                            if (def.id.nation != NationType.None && !string.IsNullOrEmpty(def.territoryName))
                                            {
                                                territoryName = def.territoryName;
                                            }
                                        }

                                        _showRouteContextMenu = true;
                                        _routeContextMenuRect = new Rect(Event.current.mousePosition.x, Event.current.mousePosition.y, 360f, 110f);
                                        _routeContextItemName = slot.item.displayName;
                                        _routeContextItemId = slot.item.id;
                                        _routeContextTerritoryName = territoryName;
                                        _routeContextTerritoryId = routeData.territoryId;
                                        _routeContextSlot = slot;
                                    }
                                }
                            }
                            Event.current.Use();
                        }
                    }
                    // 툴팁 (마우스 호버 시)
                    if (slotRect.Contains(Event.current.mousePosition))
                    {
                        DrawSlotTooltip(Event.current.mousePosition + new Vector2(22, 22), slot);
                    }
                }
            }

            GUI.EndScrollView();
        }

        private void DrawSlotTooltip(Vector2 position, PlayerInventory.ItemSlot slot)
        {
            if (slot == null || slot.item == null) return;

            float tooltipWidth = 450;
            float baseHeight = slot.item.maxDurability > 0 ? 80f : 60f;

            // 장비 비교 섹션 높이 계산
            float compareHeight = 0f;
            bool isEquipment = CompareTooltip.IsEquipmentCategory(slot.item.category);
            if (isEquipment)
            {
                compareHeight = CompareTooltip.CalculateCompareHeight(tooltipWidth - 10);
            }

            float tooltipHeight = baseHeight + compareHeight;
            Rect tooltipRect = new Rect(position.x, position.y, tooltipWidth, tooltipHeight);
            if (tooltipRect.xMax > Screen.width) tooltipRect.x = Screen.width - tooltipWidth;
            if (tooltipRect.yMax > Screen.height) tooltipRect.y = Screen.height - tooltipHeight;
            GUI.Box(tooltipRect, "", _styleSlot);
            GUI.Label(new Rect(tooltipRect.x + 5, tooltipRect.y + 5, tooltipWidth - 10, 30), slot.item.displayName, _styleSlotLabel);
            GUI.Label(new Rect(tooltipRect.x + 5, tooltipRect.y + 25, tooltipWidth - 10, 30), slot.item.description, _styleInfoDesc);
            GUI.Label(new Rect(tooltipRect.x + 5, tooltipRect.y + 45, tooltipWidth - 10, 30), $"x{slot.count}", _styleItemCount);
            if (slot.item.maxDurability > 0)
            {
                string durStr = ProjectName.Systems.EquipmentDurabilitySystem.GetDurabilityString(slot);
                GUI.Label(new Rect(tooltipRect.x + 5, tooltipRect.y + 62, tooltipWidth - 10, 24), $"내구도: {durStr}", _styleItemCount);
            }

            // ──── 장비 비교 섹션 ────
            if (isEquipment)
            {
                var equippedData = CompareTooltip.GetEquippedCompareData(slot.item.category);
                if (equippedData.HasValue)
                {
                    float cx = tooltipRect.x + 5;
                    float cy = tooltipRect.y + baseHeight + 2;
                    float cw = tooltipWidth - 10;
                    var newItemData = new ItemTooltipData
                    {
                        itemName = slot.item.displayName,
                        description = slot.item.description,
                        effects = slot.item.effects,
                        rarity = slot.item.rarity,
                        category = slot.item.category,
                        maxDurability = slot.item.maxDurability,
                        currentDurability = slot.currentDurability,
                        count = slot.count
                    };
                    CompareTooltip.DrawComparison(cx, cy, cw, newItemData, equippedData.Value, _texWhite);
                }
            }
        }


        // ===================================================================
        // 하단 정보 패널
        // ===================================================================
        private void DrawInfoPanel(float panelX, float infoY)
        {
            DrawColoredRect(new Rect(panelX, infoY, WINDOW_WIDTH, INFO_PANEL_HEIGHT), ColorBg);
            DrawColoredRect(new Rect(panelX, infoY, WINDOW_WIDTH, 1), ColorBorder);

            float innerX = panelX + 8;
            float innerY = infoY + 4;
            float innerWidth = WINDOW_WIDTH - 16;

            if (!string.IsNullOrEmpty(_selectedItemName))
            {
                // 아이콘 (ItemIconDatabase 사용)
                Texture2D iconTex = null;
                if (_selectedSlotIndex >= 0 && _currentSlots != null && _selectedSlotIndex < _currentSlots.Length)
                {
                    var selSlot = _currentSlots[_selectedSlotIndex];
                    if (selSlot != null && selSlot.item != null)
                        iconTex = ItemIconDatabase.GetOrCreateIcon(selSlot.item);
                }

                if (iconTex != null)
                {
                    GUI.DrawTexture(new Rect(innerX, innerY + 2, 48, 48), iconTex);
                }
                else
                {
                    // 폴백: 카테고리 색상 사각형
                    Color iconColor = GetCategoryColorForSelected();
                    GUI.color = iconColor;
                    float iconSize = _selectedSlotIndex >= 0 && _currentSlots != null && _selectedSlotIndex < _currentSlots.Length ? 48 : 36;
                    GUI.DrawTexture(new Rect(innerX, innerY + 2, iconSize, iconSize), _texWhite);
                    GUI.color = Color.white;
                }

                // 이름
                GUI.Label(new Rect(innerX + 32, innerY, innerWidth - 32, 33), _selectedItemName, _styleInfoName);

                // 설명
                GUI.Label(new Rect(innerX + 32, innerY + 24, innerWidth - 32, 54), _selectedItemDesc, _styleInfoDesc);

                // 보유 개수
                GUI.Label(new Rect(innerX, innerY + 62, innerWidth, 30),
                    $"보유: {_selectedItemCount}개", _styleInfoLabel);

                // C9-18: 선택된 아이템 내구도 정보 (장비만)
                if (_selectedSlotIndex >= 0 && _currentSlots != null &&
                    _selectedSlotIndex < _currentSlots.Length && _currentSlots[_selectedSlotIndex].item.maxDurability > 0)
                {
                    var selSlot = _currentSlots[_selectedSlotIndex];
                    string durStr = ProjectName.Systems.EquipmentDurabilitySystem.GetDurabilityString(selSlot);
                    float ratio = ProjectName.Systems.EquipmentDurabilitySystem.GetDurabilityRatio(selSlot);
                    Color durColor = ratio >= 0.6f ? Color.green :
                                     ratio >= 0.3f ? Color.yellow : Color.red;
                    var oldColor = GUI.color;
                    GUI.color = durColor;
                    GUI.Label(new Rect(innerX + 120, innerY + 62, innerWidth - 120, 30),
                        $"내구도: {durStr}", _styleInfoLabel);
                    GUI.color = oldColor;

                    // C9-19: 수리 버튼 (내구도가 가득 차지 않았을 때만)
                    if (ratio < 1f)
                    {
                        // _selectedSlotIndex는 필터링된 _currentSlots의 인덱스 — 전역 인덱스로 변환
                        int globalSlotIdx = GetGlobalSlotIndex(_selectedCategory, _selectedSlotIndex);
                        if (globalSlotIdx >= 0 && GUI.Button(new Rect(innerX + innerWidth - 100, innerY + 62, 202, 33), "🔧 수리"))
                        {
                            var result = ProjectName.Systems.EquipmentRepairSystem.RepairInventorySlot(globalSlotIdx);

                            _selectedItemDesc = result.message;
                            if (result.success)
                            {
                                // 인벤토리 갱신
                                RefreshInventory();
                                // 선택된 슬롯 다시 찾기 (갱신 후)
                                FindAndSelectSlot(selSlot.item.id);
                            }
                        }
                    }
                } // end if (maxDurability > 0)

                // 사용 버튼 (소비 가능한 아이템만) — durability 블록 외부에서 독립적 처리
                if (IsConsumable(_selectedCategory))
                {
                    if (GUI.Button(new Rect(innerX, innerY + 85, innerWidth - 16, 38), "사용"))
                    {
                        if (_selectedSlotIndex >= 0 && _currentSlots != null && _selectedSlotIndex < _currentSlots.Length)
                        {
                            PlayerInventory.Instance.UseItemFromCategory(_selectedCategory, _selectedSlotIndex);
                            // 선택 초기화
                            _selectedItemName = "";
                            _selectedItemDesc = "";
                            _selectedItemCount = 0;
                        }
                    }
                }

            } // end if (!string.IsNullOrEmpty(_selectedItemName))
            else
            {
                GUI.Label(new Rect(innerX, innerY + 20, innerWidth, 45),
                    "아이템을 선택하면 상세 정보가 표시됩니다.", _styleEmptyText);
            }
        }

        // ===== 인벤토리 정렬 =====
        private void SortInventory()
        {
            if (PlayerInventory.Instance == null) return;
            var allSlots = PlayerInventory.Instance.GetAllSlots();
            if (allSlots == null) return;

            // null이 아닌 슬롯만 리스트로 추출
            var nonEmpty = new System.Collections.Generic.List<PlayerInventory.ItemSlot>();
            for (int i = 0; i < allSlots.Length; i++)
            {
                if (allSlots[i] != null && allSlots[i].item != null && allSlots[i].count > 0)
                    nonEmpty.Add(allSlots[i]);
            }

            // 정렬 기준에 따라 정렬
            switch (_sortMode)
            {
                case SortMode.Category:
                    nonEmpty.Sort((a, b) =>
                    {
                        int catCompare = GetCategorySortOrder(a.item.category).CompareTo(GetCategorySortOrder(b.item.category));
                        if (catCompare != 0) return catCompare;
                        return string.Compare(a.item.displayName, b.item.displayName, System.StringComparison.Ordinal);
                    });
                    break;
                case SortMode.Name:
                    nonEmpty.Sort((a, b) => string.Compare(a.item.displayName, b.item.displayName, System.StringComparison.Ordinal));
                    break;
                case SortMode.Rarity:
                    nonEmpty.Sort((a, b) =>
                    {
                        int rCompare = a.item.rarity.CompareTo(b.item.rarity);
                        if (rCompare != 0) return rCompare;
                        return string.Compare(a.item.displayName, b.item.displayName, System.StringComparison.Ordinal);
                    });
                    break;
                case SortMode.Quantity:
                    nonEmpty.Sort((a, b) => b.count.CompareTo(a.count));
                    break;
            }

            // 배열 재구성: 정렬된 아이템 → 빈 슬롯
            for (int i = 0; i < allSlots.Length; i++)
            {
                if (i < nonEmpty.Count)
                    allSlots[i] = nonEmpty[i];
                else
                    allSlots[i] = null;
            }
        }

        private int GetCategorySortOrder(PlayerInventory.ItemCategory category)
        {
            return category switch
            {
                PlayerInventory.ItemCategory.Herb => 0,
                PlayerInventory.ItemCategory.Meat => 1,
                PlayerInventory.ItemCategory.Food => 2,
                PlayerInventory.ItemCategory.Potion => 3,
                PlayerInventory.ItemCategory.Material => 4,
                PlayerInventory.ItemCategory.Drug => 5,
                PlayerInventory.ItemCategory.Quest => 6,
                PlayerInventory.ItemCategory.Weapon => 7,
                PlayerInventory.ItemCategory.Armor => 8,
                PlayerInventory.ItemCategory.Tool => 9,
                PlayerInventory.ItemCategory.Arrow => 10,
                _ => 99,
            };
        }

        // ===================================================================
        // 헬퍼
        // ===================================================================

        private void SelectCategory(PlayerInventory.ItemCategory category)
        {
            if (_selectedCategory != category)
            {
                _selectedCategory = category;
                _selectedSlotIndex = -1;
                RefreshInventory();
            }
        }

        /// <summary>
        /// 인벤토리 내용 갱신
        /// </summary>
        public void RefreshInventory()
        {
            if (PlayerInventory.Instance == null) return;

            _currentSlots = PlayerInventory.Instance.GetSlotsByCategory(_selectedCategory);
            _selectedItemName = "";
            _selectedItemDesc = "";
            _selectedItemCount = 0;
        }

        private Color GetCategoryColor(PlayerInventory.ItemCategory category)
        {
            return category switch
            {
                PlayerInventory.ItemCategory.Herb => new Color(0.3f, 0.8f, 0.3f),    // 초록
                PlayerInventory.ItemCategory.Meat => new Color(0.8f, 0.4f, 0.2f),    // 주황
                PlayerInventory.ItemCategory.Food => new Color(0.9f, 0.8f, 0.2f),    // 노랑
                PlayerInventory.ItemCategory.Potion => new Color(0.7f, 0.3f, 0.8f),  // 보라
                PlayerInventory.ItemCategory.Material => new Color(0.5f, 0.5f, 0.5f),// 회색
                PlayerInventory.ItemCategory.Quest => new Color(0.2f, 0.7f, 0.8f),   // 청록
                PlayerInventory.ItemCategory.Weapon => new Color(0.8f, 0.3f, 0.3f),  // 빨강
                PlayerInventory.ItemCategory.Armor => new Color(0.3f, 0.3f, 0.8f),   // 파랑
                PlayerInventory.ItemCategory.Tool => new Color(0.6f, 0.4f, 0.2f),    // 갈색
                _ => Color.gray,
            };
        }

        private Color GetCategoryColorForSelected()
        {
            if (_selectedSlotIndex >= 0 && _currentSlots != null && _selectedSlotIndex < _currentSlots.Length)
            {
                var slot = _currentSlots[_selectedSlotIndex];
                if (slot != null && slot.item != null)
                    return GetCategoryColor(slot.item.category);
            }
            return Color.gray;
        }

        private bool IsConsumable(PlayerInventory.ItemCategory category)
        {
            return category == PlayerInventory.ItemCategory.Herb ||
                   category == PlayerInventory.ItemCategory.Meat ||
                   category == PlayerInventory.ItemCategory.Food ||
                   category == PlayerInventory.ItemCategory.Potion;
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

        /// <summary>필터링된 카테고리 인덱스를 전역 슬롯 인덱스로 변환</summary>
        private int GetGlobalSlotIndex(PlayerInventory.ItemCategory category, int filteredIndex)
        {
            if (PlayerInventory.Instance == null || filteredIndex < 0) return -1;
            int count = -1;
            var allSlots = PlayerInventory.Instance.GetAllSlots();
            for (int i = 0; i < allSlots.Length; i++)
            {
                var slot = allSlots[i];
                if (slot != null && slot.item != null && slot.item.category == category)
                {
                    count++;
                    if (count == filteredIndex)
                        return i;
                }
            }
            return -1;
        }

        // ===== 퀵슬롯 연동 (QuickSlotUI에서 호출) =====

        /// <summary>
        /// 현재 선택된 아이템의 ItemData 반환 (없으면 null)
        /// </summary>
        public PlayerInventory.ItemData GetSelectedItemData()
        {
            if (_selectedSlotIndex < 0 || _currentSlots == null || _selectedSlotIndex >= _currentSlots.Length)
                return null;
            var slot = _currentSlots[_selectedSlotIndex];
            if (slot == null) return null;
            return slot.item;
        }

        /// <summary>
        /// 현재 선택된 아이템의 개수 반환
        /// </summary>
        public int GetSelectedItemCount()
        {
            if (_selectedSlotIndex < 0 || _currentSlots == null || _selectedSlotIndex >= _currentSlots.Length)
                return 0;
            var slot = _currentSlots[_selectedSlotIndex];
            if (slot == null) return 0;
            return slot.count;
        }

        /// <summary>
        /// 현재 선택된 아이템이 있는지 확인
        /// </summary>
        public bool HasSelectedItem()
        {
            if (_selectedSlotIndex < 0 || _currentSlots == null || _selectedSlotIndex >= _currentSlots.Length)
                return false;
            var slot = _currentSlots[_selectedSlotIndex];
            return slot != null && slot.item != null && slot.count > 0;
        }

        /// <summary>아이템 ID로 슬롯 찾아 선택</summary>
        private void FindAndSelectSlot(string itemId)
        {
            if (_currentSlots == null) return;
            for (int i = 0; i < _currentSlots.Length; i++)
            {
                if (_currentSlots[i] != null && _currentSlots[i].item.id == itemId)
                {
                    _selectedSlotIndex = i;
                    _selectedItemName = _currentSlots[i].item.displayName;
                    _selectedItemDesc = _currentSlots[i].item.description;
                    _selectedItemCount = _currentSlots[i].count;
                    return;
                }
            }
            _selectedSlotIndex = -1;
        }

        // 캐시된 GUIContent (TruncateText GC 절감)
        private readonly GUIContent _truncateContent = new GUIContent();

        /// <summary>컬러 사각형 그리기</summary>
        private void DrawColoredRect(Rect rect, Color color)
        {
            var oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _texWhite);
            GUI.color = oldColor;
        }

        /// <summary>텍스트 길이 제한</summary>
        private string TruncateText(string text, float maxWidth, GUIStyle style)
        {
            if (string.IsNullOrEmpty(text)) return "";
            _truncateContent.text = text;
            float width = style.CalcSize(_truncateContent).x;
            if (width <= maxWidth) return text;

            for (int i = text.Length - 1; i > 0; i--)
            {
                string truncated = text.Substring(0, i) + "..";
                _truncateContent.text = truncated;
                if (style.CalcSize(_truncateContent).x <= maxWidth)
                    return truncated;
            }
            return text.Length > 0 ? text[0] + ".." : "..";
        }

        // ===================================================================
        // 🗺️ 오토루트 컨텍스트 메뉴
        // ===================================================================

        /// <summary>
        /// 우클릭 시 표시되는 오토루트 컨텍스트 메뉴를 그립니다.
        /// </summary>
        private void DrawRouteContextMenu()
        {
            if (!_showRouteContextMenu) return;

            // ESC 또는 다른 클릭으로 닫기
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                _showRouteContextMenu = false;
                Event.current.Use();
                return;
            }

            // 컨텍스트 메뉴 위치 보정 (화면 밖으로 나가지 않도록)
            float menuX = _routeContextMenuRect.x;
            float menuY = _routeContextMenuRect.y;
            float menuWidth = _routeContextMenuRect.width;
            float menuHeight = _routeContextMenuRect.height;

            if (menuX + menuWidth > Screen.width)
                menuX = Screen.width - menuWidth - 10;
            if (menuY + menuHeight > Screen.height)
                menuY = Screen.height - menuHeight - 10;
            if (menuX < 0) menuX = 10;
            if (menuY < 0) menuY = 10;

            Rect menuRect = new Rect(menuX, menuY, menuWidth, menuHeight);

            // 배경
            Color oldBg = GUI.color;
            GUI.color = new Color(0.12f, 0.10f, 0.14f, 0.95f);
            GUI.DrawTexture(menuRect, _texWhite);
            // 테두리
            GUI.color = new Color(0.70f, 0.50f, 0.15f, 1f);
            DrawColoredRect(new Rect(menuX, menuY, menuWidth, 2), new Color(0.70f, 0.50f, 0.15f, 1f));
            DrawColoredRect(new Rect(menuX, menuY + menuHeight - 2, menuWidth, 2), new Color(0.70f, 0.50f, 0.15f, 1f));
            GUI.color = oldBg;

            // 메뉴 항목
            string routeLabel = $"📍 오토루트: {_routeContextTerritoryName}";
            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 42,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.92f, 0.88f, 0.80f, 1f) },
                padding = new RectOffset(8, 4, 2, 2)
            };
            GUI.Label(new Rect(menuX + 4, menuY + 6, menuWidth - 8, 38), routeLabel, labelStyle);

            // [이동] 버튼
            float btnWidth = menuWidth - 16;
            float btnHeight = 44;
            float btnY = menuY + menuHeight - btnHeight - 6;
            var btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 38,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.80f, 0.60f, 0.20f, 1f),
                           background = MakeTexture(1, 1, new Color(0.30f, 0.22f, 0.16f, 1f)) },
                hover = { textColor = Color.white,
                          background = MakeTexture(1, 1, new Color(0.45f, 0.32f, 0.22f, 1f)) },
                border = new RectOffset(2, 2, 2, 2)
            };

            if (GUI.Button(new Rect(menuX + 8, btnY, btnWidth, btnHeight), "🚶 이동", btnStyle))
            {
                ConfirmRouteAction();
                _showRouteContextMenu = false;
                Event.current.Use();
            }
        }

        /// <summary>
        /// 오토루트 이동 확인 — RouteConfirmationUI 팝업 표시 후 AutoMoveManager 실행
        /// </summary>
        private void ConfirmRouteAction()
        {
            if (string.IsNullOrEmpty(_routeContextTerritoryId))
            {
                Debug.LogWarning("[InventoryWindow] 영지 ID가 없어 오토루트를 실행할 수 없습니다.");
                return;
            }

            // 영지 ID 파싱
            string[] parts = _routeContextTerritoryId.Split('_');
            if (parts.Length != 2)
            {
                Debug.LogWarning($"[InventoryWindow] 영지 ID 형식 오류: {_routeContextTerritoryId}");
                return;
            }

            if (!System.Enum.TryParse<NationType>(parts[0], out var nation))
            {
                Debug.LogWarning($"[InventoryWindow] 국가 파싱 실패: {parts[0]}");
                return;
            }

            if (!int.TryParse(parts[1], out int index))
            {
                Debug.LogWarning($"[InventoryWindow] 영지 인덱스 파싱 실패: {parts[1]}");
                return;
            }

            // AutoMoveManager 확인
            if (AutoMoveManager.Instance == null)
            {
                Debug.LogError("[InventoryWindow] AutoMoveManager 인스턴스가 없습니다! Scene에 AutoMoveManager를 추가해주세요.");
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

            // 확인 메시지
            string confirmMsg = $"📍 {_routeContextTerritoryName} (으)로 자동 이동합니다";


            // RouteConfirmationUI 팝업 표시
            if (RouteConfirmationUI.Instance != null)
            {
                RouteConfirmationUI.Instance.Show(
                    _routeContextItemName,
                    _routeContextTerritoryName,
                    _routeContextTerritoryId
                );
            }
        }
    }
}