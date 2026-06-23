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
    /// </summary>
    public class LootWindow : UIWindow
    {
        [Header("Loot Window")]
        [SerializeField] private ILootBasket _currentBasket;
        [SerializeField] private string _windowTitle = "🎁 전리품";

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
        private GUIStyle _styleTakeAllBtnHover;
        private bool _stylesInitialized;
        private Texture2D _texWhite;

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
            if (_theme == null)
                ApplyTheme(Phase33_Themes.LootTheme());
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
            if (_currentBasket == null || _currentBasket.IsEmpty)
            {
                _cachedItems = null;
                // 비었으면 자동 닫기
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

            _styleTakeAllBtnHover = new GUIStyle(_styleTakeAllBtn)
            {
                normal = { textColor = ColorTextPrimary, background = MakeTexture(1, 1, ColorBtnTakeAllHover) }
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

            // 바스켓이 없거나 비었으면 닫기
            if (_currentBasket == null || _currentBasket.IsEmpty)
            {
                Hide();
                return;
            }

            // 혹시 모를 캐시 업데이트
            if (_cachedItems == null || _cachedItems.Length != _currentBasket.ItemCount)
                RefreshLoot();

            float x = (Screen.width - WINDOW_WIDTH) / 2;
            float y = (Screen.height - WINDOW_HEIGHT) / 2;

            // === 배경 박스 ===
            GUI.Box(new Rect(x, y, WINDOW_WIDTH, WINDOW_HEIGHT), "", _stylePanelBox);

            // === 상단 테두리 ===
            DrawColoredRect(new Rect(x, y, WINDOW_WIDTH, 2), ColorBorder);

            // === 타이틀 바 ===
            DrawColoredRect(new Rect(x, y + 2, WINDOW_WIDTH, TITLE_BAR_HEIGHT), ColorTitleBar);
            string basketName = _currentBasket != null ? _currentBasket.BasketName : "전리품";
            GUI.Label(new Rect(x, y + 2, WINDOW_WIDTH, TITLE_BAR_HEIGHT), $"  🎁 {basketName}", _styleTitle);

            // 구분선
            DrawColoredRect(new Rect(x, y + TITLE_BAR_HEIGHT + 2, WINDOW_WIDTH, 2), ColorBorder);

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

            DrawColoredRect(new Rect(panelX, gridY, WINDOW_WIDTH, gridHeight), ColorBg);

            float slotWidth = (innerWidth - SLOT_MARGIN * (GRID_COLUMNS + 1)) / GRID_COLUMNS;
            float slotHeight = 162;
            float rowHeight = slotHeight + SLOT_MARGIN;

            int totalSlots = _cachedItems != null ? _cachedItems.Length : 0;
            int totalRows = Mathf.Max(1, Mathf.CeilToInt((float)totalSlots / GRID_COLUMNS));
            float contentHeight = totalRows * rowHeight + SLOT_MARGIN;
            float viewHeight = gridHeight - 4;

            _scrollPosition = GUI.BeginScrollView(
                new Rect(innerX, innerY, innerWidth, viewHeight),
                _scrollPosition,
                new Rect(0, 0, innerWidth - 20, contentHeight)
            );

            if (_cachedItems == null || _cachedItems.Length == 0)
            {
                GUI.Label(new Rect(0, 20, innerWidth - 20, 45), "(전리품이 없습니다)", _styleEmptyText);
            }
            else
            {
                for (int i = 0; i < _cachedItems.Length; i++)
                {
                    var entry = _cachedItems[i];
                    if (entry == null || entry.item == null || entry.count <= 0) continue;

                    int col = i % GRID_COLUMNS;
                    int row = i / GRID_COLUMNS;

                    float sx = SLOT_MARGIN + col * (slotWidth + SLOT_MARGIN);
                    float sy = SLOT_MARGIN + row * rowHeight;

                    Rect slotRect = new Rect(sx, sy, slotWidth, slotHeight);
                    bool isSelected = (i == _selectedIndex);

                    var slotStyle = isSelected ? _styleSlotSelected : _styleSlot;
                    GUI.Box(slotRect, "", slotStyle);

                    // 아이콘 (ItemIconDatabase 사용)
                    Texture2D iconTex = ItemIconDatabase.GetOrCreateIcon(entry.item);
                    if (iconTex != null)
                    {
                        GUI.DrawTexture(new Rect(sx + 6, sy + 4, 90, 90), iconTex);
                    }
                    else
                    {
                        // 폴백: 카테고리 색상 사각형
                        Color iconColor = GetItemColor(entry.item.category);
                        GUI.color = iconColor;
                        GUI.DrawTexture(new Rect(sx + 6, sy + 4, 90, 90), _texWhite);
                        GUI.color = Color.white;
                    }

                    // 이름
                    float nameY = sy + 38;
                    float nameWidth = slotWidth - 12;
                    GUI.Label(new Rect(sx + 6, nameY, nameWidth, 24),
                        TruncateText(entry.item.displayName, nameWidth, _styleSlotLabel),
                        _styleSlotLabel);

                    // 개수
                    GUI.Label(new Rect(sx + 6, nameY + 14, nameWidth, 21),
                        $"x{entry.count}",
                        _styleItemCount);

                    // 클릭 → 획득
                    if (Event.current.type == EventType.MouseDown && slotRect.Contains(Event.current.mousePosition))
                    {
                        _selectedIndex = i;
                        Event.current.Use();
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
            DrawColoredRect(new Rect(panelX, bottomY, WINDOW_WIDTH, BOTTOM_BAR_HEIGHT), ColorBottomBar);
            DrawColoredRect(new Rect(panelX, bottomY, WINDOW_WIDTH, 1), ColorBorder);

            float btnWidth = 360;
            float btnHeight = 48f;
            float btnX = panelX + (WINDOW_WIDTH - btnWidth) / 2;
            float btnY = bottomY + (BOTTOM_BAR_HEIGHT - btnHeight) / 2;

            // 아이템 개수 표시
            int totalItems = _cachedItems != null ? _cachedItems.Length : 0;
            GUI.Label(new Rect(panelX + 10, bottomY + 4, 270, 30),
                $"아이템 {totalItems}개", _styleEmptyText);

            // 전부 획득 버튼
            if (GUI.Button(new Rect(btnX, btnY, btnWidth, btnHeight), "📥 전부 획득", _styleTakeAllBtn))
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
                Debug.Log($"[LootWindow] 아이템 획득 완료");
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
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, color);
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

        private string TruncateText(string text, float maxWidth, GUIStyle style)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var content = new GUIContent(text);
            float width = style.CalcSize(content).x;
            if (width <= maxWidth) return text;

            for (int i = text.Length - 1; i > 0; i--)
            {
                string truncated = text.Substring(0, i) + "..";
                content.text = truncated;
                if (style.CalcSize(content).x <= maxWidth)
                    return truncated;
            }
            return text.Length > 0 ? text[0] + ".." : "..";
        }
    }
}