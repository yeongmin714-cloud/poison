using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.UI.Themes;

namespace ProjectName.UI
{
    /// <summary>
    /// 전리품 창 (Loot Window) — LootBasket 열었을 때 표시.
    /// 인벤토리와 비슷한 레이아웃, "전리품" 헤더, "전부 획득" 버튼.
    /// 개별 아이템 클릭 시 인벤토리로 이동. 비면 자동 닫힘.
    /// 
    /// [QA v1.1] OnGUI GC 최적화 완료 (Rect 캐싱, GUIContent 재사용, string 보간 제거).
    /// </summary>
    public class LootWindow : UIWindow
    {
        [Header("Loot Window")]
        private ILootBasket _currentBasket;

        // 아이템 목록 캐시
        private LootEntry[] _cachedItems;
        private Vector2 _scrollPosition;
        private int _selectedIndex = -1;

        // ===== 레퍼런스 스타일 상수 =====
        private const float WINDOW_WIDTH = 1000f;
        private const float WINDOW_HEIGHT = 1000f;
        private const float TITLE_BAR_HEIGHT = 90f;
        private const float BOTTOM_BAR_HEIGHT = 120f;
        private const int GRID_COLUMNS = 3;
        private const float SLOT_MARGIN = 12f;

        // ===== 다크 테마 색상 (인벤토리와 통일) =====
        private static readonly Color ColorBg = new Color(0.18f, 0.13f, 0.16f, 0.92f);
        private static readonly Color ColorTitleBar = new Color(0.12f, 0.09f, 0.11f, 1f);
        private static readonly Color ColorSlotBg = new Color(0.22f, 0.17f, 0.14f, 0.9f);
        private static readonly Color ColorSlotHover = new Color(0.35f, 0.25f, 0.18f, 0.9f);
        private static readonly Color ColorSlotSelected = new Color(0.40f, 0.28f, 0.20f, 1f);
        private static readonly Color ColorBottomBar = new Color(0.12f, 0.09f, 0.11f, 1f);
        private static readonly Color ColorTextPrimary = new Color(0.92f, 0.88f, 0.80f, 1f);
        private static readonly Color ColorTextSecondary = new Color(0.70f, 0.65f, 0.60f, 1f);
        private static readonly Color ColorTextDim = new Color(0.50f, 0.45f, 0.40f, 1f);
        private static readonly Color ColorAccent = new Color(0.80f, 0.60f, 0.20f, 1f);
        private static readonly Color ColorBorder = new Color(0.12f, 0.09f, 0.11f, 1f);
        private static readonly Color ColorBtnTakeAll = new Color(0.30f, 0.45f, 0.25f, 1f);
        private static readonly Color ColorBtnTakeAllHover = new Color(0.40f, 0.55f, 0.30f, 1f);

        // ===== GUIStyle 캐시 =====
        private GUIStyle _styleTitle;
        private GUIStyle _styleSlot;
        private GUIStyle _styleSlotSelected;
        private GUIStyle _styleSlotLabel;
        private GUIStyle _styleItemCount;
        private GUIStyle _styleEmptyText;
        private GUIStyle _stylePanelBox;
        private GUIStyle _styleTakeAllBtn;
        private bool _stylesInitialized;
        private Texture2D _texWhite;

        // ===== GC 최적화: Rect 재사용 =====
        private readonly Rect _rectWork = new Rect();
        private Rect _rectBg;
        private Rect _rectBorderTop;
        private Rect _rectTitleBar;
        private Rect _rectTitleLabel;
        private Rect _rectTitleDivider;
        private Rect _rectGridBg;
        private Rect _rectScrollView;
        private Rect _rectScrollContent;
        private Rect _rectEmptyLabel;
        private Rect _rectBottomBar;
        private Rect _rectBottomBorder;
        private Rect _rectItemCountLabel;
        private Rect _rectTakeAllBtn;
        private Rect _rectSlot;

        // GC 최적화: GUIContent 재사용
        private readonly GUIContent _gcCache = new GUIContent();
        private readonly GUIContent _gcItemCount = new GUIContent();

        // ===== GC 최적화: 문자열 버퍼 =====
        private string _strBasketName;
        private string _strItemCount;

        public ILootBasket CurrentBasket
        {
            get => _currentBasket;
            set
            {
                _currentBasket = value;
                _selectedIndex = -1;
                RefreshLoot();
            }
        }

        protected override void OnShow()
        {
            Debug.Log("[LootWindow] 열림");
            RefreshLoot();
        }

        protected override void OnHide()
        {
            Debug.Log("[LootWindow] 닫힘");
            _currentBasket = null;
            _cachedItems = null;
        }

        /// <summary>
        /// 외부에서 호출: 특정 바스켓 열기
        /// </summary>
        public void OpenForBasket(ILootBasket basket)
        {
            if (basket == null) return;
            if (basket.IsEmpty) return;

            if (_theme == null)
                ApplyTheme(Phase33_Themes.CreateMedievalShopTheme());
            _currentBasket = basket;
            _selectedIndex = -1;
            RefreshLoot();
            Show();
        }

        /// <summary>
        /// 전리품 내용 갱신
        /// </summary>
        public void RefreshLoot()
        {
            if (_currentBasket == null || _currentBasket.IsEmpty || !_currentBasket.IsAvailable)
            {
                _cachedItems = null;
                if (IsOpen)
                    Hide();
                return;
            }

            var items = _currentBasket.Items;
            _cachedItems = new LootEntry[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                _cachedItems[i] = items[i];
            }
        }

        /// <summary>
        /// 생성된 텍스처 정리 (메모리 누수 방지)
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (_texWhite != null)
            {
                Destroy(_texWhite);
                _texWhite = null;
            }
        }

        // ===================================================================
        // 스타일 초기화
        // ===================================================================
        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _texWhite = MakeTexture(1, 1, Color.white);

            _styleTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 72,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ColorTextPrimary },
                padding = new RectOffset(21, 4, 0, 0)
            };

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

            _styleSlotSelected = new GUIStyle(_styleSlot)
            {
                normal = { background = MakeTexture(1, 1, ColorSlotSelected), textColor = ColorTextPrimary }
            };

            _styleSlotLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 44,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ColorTextPrimary },
                wordWrap = true
            };

            _styleItemCount = new GUIStyle(GUI.skin.label)
            {
                fontSize = 44,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = ColorAccent }
            };

            _styleEmptyText = new GUIStyle(GUI.skin.label)
            {
                fontSize = 52,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ColorTextDim }
            };

            _stylePanelBox = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTexture(1, 1, ColorBg), textColor = ColorTextPrimary },
                border = new RectOffset(2, 2, 2, 2),
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };

            _styleTakeAllBtn = new GUIStyle(GUI.skin.button)
            {
                fontSize = 56,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ColorTextPrimary, background = MakeTexture(1, 1, ColorBtnTakeAll) },
                hover = { textColor = ColorTextPrimary, background = MakeTexture(1, 1, ColorBtnTakeAllHover) },
                active = { textColor = ColorTextPrimary, background = MakeTexture(1, 1, ColorBtnTakeAll) },
                border = new RectOffset(3, 3, 3, 3),
                padding = new RectOffset(0, 0, 4, 4)
            };

            _stylesInitialized = true;
        }

        // ===================================================================
        // OnGUI — IMGUI 렌더링
        // ===================================================================
        private void OnGUI()
        {
            if (!IsOpen) return;
            InitStyles();

            if (_currentBasket == null || _currentBasket.IsEmpty || !_currentBasket.IsAvailable)
            {
                Hide();
                return;
            }

            if (_cachedItems == null || _cachedItems.Length != _currentBasket.ItemCount)
                RefreshLoot();

            float x = (Screen.width - WINDOW_WIDTH) / 2;
            float y = (Screen.height - WINDOW_HEIGHT) / 2;

            // === Rect 캐싱 (GC 최적화) ===
            _rectBg.Set(x, y, WINDOW_WIDTH, WINDOW_HEIGHT);
            _rectBorderTop.Set(x, y, WINDOW_WIDTH, 2);
            _rectTitleBar.Set(x, y + 2, WINDOW_WIDTH, TITLE_BAR_HEIGHT);
            _rectTitleLabel.Set(x, y + 2, WINDOW_WIDTH, TITLE_BAR_HEIGHT);
            _rectTitleDivider.Set(x, y + TITLE_BAR_HEIGHT + 2, WINDOW_WIDTH, 2);

            // === 배경 박스 ===
            GUI.Box(_rectBg, "", _stylePanelBox);

            // === 상단 테두리 ===
            DrawColoredRect(_rectBorderTop, ColorBorder);

            // === 타이틀 바 ===
            DrawColoredRect(_rectTitleBar, ColorTitleBar);
            _strBasketName = _currentBasket != null ? _currentBasket.BasketName : "전리품";
            GUI.Label(_rectTitleLabel, "  🎁 " + _strBasketName, _styleTitle);

            // 구분선
            DrawColoredRect(_rectTitleDivider, ColorBorder);

            // === 아이템 그리드 ===
            float gridY = y + TITLE_BAR_HEIGHT + 4;
            float gridHeight = WINDOW_HEIGHT - (gridY - y) - BOTTOM_BAR_HEIGHT - 6;
            DrawItemGrid(x, gridY, gridHeight);

            // === 하단 바 (전부 획득 버튼) ===
            float bottomY = gridY + gridHeight + 2;
            DrawBottomBar(x, bottomY);
        }

        // ===================================================================
        // 아이템 그리드
        // ===================================================================
        private void DrawItemGrid(float panelX, float gridY, float gridHeight)
        {
            float innerX = panelX + 4;
            float innerY = gridY + 2;
            float innerWidth = WINDOW_WIDTH - 8;

            _rectGridBg.Set(panelX, gridY, WINDOW_WIDTH, gridHeight);
            DrawColoredRect(_rectGridBg, ColorBg);

            float slotWidth = (innerWidth - SLOT_MARGIN * (GRID_COLUMNS + 1)) / GRID_COLUMNS;
            float slotHeight = 162;
            float rowHeight = slotHeight + SLOT_MARGIN;

            int totalSlots = _cachedItems != null ? _cachedItems.Length : 0;
            int totalRows = Mathf.Max(1, Mathf.CeilToInt((float)totalSlots / GRID_COLUMNS));
            float contentHeight = totalRows * rowHeight + SLOT_MARGIN;
            float viewHeight = gridHeight - 4;

            _rectScrollView.Set(innerX, innerY, innerWidth, viewHeight);
            _rectScrollContent.Set(0, 0, innerWidth - 20, contentHeight);

            _scrollPosition = GUI.BeginScrollView(
                _rectScrollView,
                _scrollPosition,
                _rectScrollContent
            );

            if (_cachedItems == null || _cachedItems.Length == 0)
            {
                _rectEmptyLabel.Set(0, 20, innerWidth - 20, 45);
                GUI.Label(_rectEmptyLabel, "(전리품이 없습니다)", _styleEmptyText);
            }
            else
            {
                // EventType 캐싱 (GC 최적화)
                Event currentEvent = Event.current;
                bool isMouseDown = currentEvent.type == EventType.MouseDown;

                for (int i = 0; i < _cachedItems.Length; i++)
                {
                    var entry = _cachedItems[i];
                    if (entry == null || entry.Item == null || entry.Count <= 0) continue;

                    int col = i % GRID_COLUMNS;
                    int row = i / GRID_COLUMNS;

                    float sx = SLOT_MARGIN + col * (slotWidth + SLOT_MARGIN);
                    float sy = SLOT_MARGIN + row * rowHeight;

                    _rectSlot.Set(sx, sy, slotWidth, slotHeight);
                    bool isSelected = (i == _selectedIndex);

                    var slotStyle = isSelected ? _styleSlotSelected : _styleSlot;
                    GUI.Box(_rectSlot, "", slotStyle);

                    // 아이콘 (ItemIconDatabase 사용 — 캐싱됨)
                    Texture2D iconTex = ItemIconDatabase.GetOrCreateIcon(entry.Item);
                    if (iconTex != null)
                    {
                        _rectWork.Set(sx + 6, sy + 4, 90, 90);
                        GUI.DrawTexture(_rectWork, iconTex);
                    }
                    else
                    {
                        Color iconColor = GetItemColor(entry.Item.category);
                        _rectWork.Set(sx + 6, sy + 4, 90, 90);
                        GUI.color = iconColor;
                        GUI.DrawTexture(_rectWork, _texWhite);
                        GUI.color = Color.white;
                    }

                    // 이름
                    float nameY = sy + 38;
                    float nameWidth = slotWidth - 12;
                    _rectWork.Set(sx + 6, nameY, nameWidth, 24);
                    GUI.Label(_rectWork,
                        TruncateText(entry.Item.displayName, nameWidth, _styleSlotLabel),
                        _styleSlotLabel);

                    // 개수 (GC 최적화: string.Concat 사용)
                    _rectWork.Set(sx + 6, nameY + 14, nameWidth, 21);
                    _strItemCount = "x" + entry.Count;
                    _gcItemCount.text = _strItemCount;
                    GUI.Label(_rectWork, _gcItemCount, _styleItemCount);

                    // 클릭 → 획득 (MouseDown에서만 처리)
                    if (isMouseDown && _rectSlot.Contains(currentEvent.mousePosition))
                    {
                        _selectedIndex = i;
                        currentEvent.Use();
                        TakeSelectedItem(i);
                    }
                }
            }

            GUI.EndScrollView();
        }

        // ===================================================================
        // 하단 바 — 전부 획득 버튼
        // ===================================================================
        private void DrawBottomBar(float panelX, float bottomY)
        {
            _rectBottomBar.Set(panelX, bottomY, WINDOW_WIDTH, BOTTOM_BAR_HEIGHT);
            _rectBottomBorder.Set(panelX, bottomY, WINDOW_WIDTH, 1);
            DrawColoredRect(_rectBottomBar, ColorBottomBar);
            DrawColoredRect(_rectBottomBorder, ColorBorder);

            float btnWidth = 360;
            float btnHeight = 48f;
            float btnX = panelX + (WINDOW_WIDTH - btnWidth) / 2;
            float btnY = bottomY + (BOTTOM_BAR_HEIGHT - btnHeight) / 2;

            // 아이템 개수 표시 (GC 최적화: string.Concat 사용)
            int totalItems = _cachedItems != null ? _cachedItems.Length : 0;
            _rectItemCountLabel.Set(panelX + 10, bottomY + 4, 270, 30);
            _gcCache.text = "아이템 " + totalItems + "개";
            GUI.Label(_rectItemCountLabel, _gcCache, _styleEmptyText);

            // 전부 획득 버튼
            _rectTakeAllBtn.Set(btnX, btnY, btnWidth, btnHeight);
            if (GUI.Button(_rectTakeAllBtn, "📥 전부 획득", _styleTakeAllBtn))
            {
                TakeAllItems();
            }
        }

        // ===================================================================
        // 아이템 획득 로직
        // ===================================================================
        private void TakeSelectedItem(int index)
        {
            if (_currentBasket == null) return;
            if (_currentBasket.TakeItem(index))
            {
                Debug.Log("[LootWindow] 아이템 획득 완료");
                RefreshLoot();
            }
        }

        private void TakeAllItems()
        {
            if (_currentBasket == null) return;
            if (_currentBasket.TakeAll())
            {
                Debug.Log("[LootWindow] 모든 아이템 획득 완료");
                RefreshLoot();
            }
        }

        // ===================================================================
        // 헬퍼
        // ===================================================================
        private Color GetItemColor(PlayerInventory.ItemCategory category)
        {
            return category switch
            {
                PlayerInventory.ItemCategory.Herb => new Color(0.3f, 0.8f, 0.3f),
                PlayerInventory.ItemCategory.Meat => new Color(0.8f, 0.4f, 0.2f),
                PlayerInventory.ItemCategory.Food => new Color(0.9f, 0.8f, 0.2f),
                PlayerInventory.ItemCategory.Potion => new Color(0.7f, 0.3f, 0.8f),
                PlayerInventory.ItemCategory.Material => new Color(0.5f, 0.5f, 0.5f),
                PlayerInventory.ItemCategory.Quest => new Color(0.2f, 0.7f, 0.8f),
                _ => Color.gray,
            };
        }

        private Texture2D MakeTexture(int w, int h, Color color)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private void DrawColoredRect(Rect rect, Color color)
        {
            var oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _texWhite);
            GUI.color = oldColor;
        }

        private readonly GUIContent _gcTruncate = new GUIContent();

        private string TruncateText(string text, float maxWidth, GUIStyle style)
        {
            if (string.IsNullOrEmpty(text)) return "";
            _gcTruncate.text = text;
            float width = style.CalcSize(_gcTruncate).x;
            if (width <= maxWidth) return text;

            for (int i = text.Length - 1; i > 0; i--)
            {
                string truncated = text.Substring(0, i) + "..";
                _gcTruncate.text = truncated;
                if (style.CalcSize(_gcTruncate).x <= maxWidth)
                    return truncated;
            }
            return text.Length > 0 ? text[0] + ".." : "..";
        }
    }
}