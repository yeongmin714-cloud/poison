using System.Collections.Generic;
using UnityEngine;
using ProjectName.UI.Themes;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Core.Themes;
#pragma warning disable 0414

namespace ProjectName.UI
{
    /// <summary>
    /// 전리품 창 (Loot Window) — LootBasket 열었을 때 표시.
    /// 2026-09-11(6): 화면 우측 1/3 구획 고정 배치 (인벤 창 제3구획과 동일 좌표 규약).
    /// 인벤 그리드 스타일의 "전리품" 헤더, "전부 획득" 버튼.
    /// 슬롯 좌클릭 = 드래그 시작(ItemDragContext.Source.Loot) — 인벤 그리드에 드롭 시 이동,
    /// 전리품 슬롯 위에서 그냥 뗌 = 즉시 획득(기존 클릭 UX 유지). 비면 자동 닫힘.
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
        // 2026-09-11(6): 우측 구획 고정 배치 — 기존 중앙 1000×1000 팝업 폐기.
        // 폭/높이는 InventoryWindow의 창 크기 규약(WINDOW_WIDTH/WINDOW_HEIGHT)을 그대로 따른다.
        private static float WINDOW_WIDTH => InventoryWindow.WINDOW_WIDTH;   // Screen.width/3 - 12
        private static float WINDOW_HEIGHT => Screen.height - 180f;          // InventoryWindow.WINDOW_HEIGHT 규약 (하단 핫바 170 여백)
        private const float TITLE_BAR_HEIGHT = 90f;
        private const float BOTTOM_BAR_HEIGHT = 120f;
        private const int GRID_COLUMNS = 3;
        private const float SLOT_MARGIN = 12f;

        // ===== 다크 테마 색상 (인벤토리와 통일) =====
        private static readonly Color ColorBg = new Color(0.063f, 0.086f, 0.133f, 0.92f);   // 다크네이비
        private static readonly Color ColorTitleBar = new Color(0.063f, 0.086f, 0.133f, 1f);
        private static readonly Color ColorSlotBg = new Color(0.10f, 0.14f, 0.19f, 0.9f);
        private static readonly Color ColorSlotHover = new Color(0.16f, 0.22f, 0.30f, 0.9f);
        private static readonly Color ColorSlotSelected = new Color(0.20f, 0.28f, 0.38f, 1f);
        private static readonly Color ColorBottomBar = new Color(0.063f, 0.086f, 0.133f, 1f);
        private static readonly Color ColorTextPrimary = new Color(1f, 1f, 1f, 1f);          // 흰색
        private static readonly Color ColorTextSecondary = new Color(0.62f, 0.70f, 0.78f, 1f); // 회백
        private static readonly Color ColorTextDim = new Color(0.45f, 0.52f, 0.60f, 1f);
        private static readonly Color ColorAccent = new Color(0.35f, 0.65f, 0.90f, 1f);      // 스카이블루
        private static readonly Color ColorBorder = new Color(0.063f, 0.086f, 0.133f, 1f);
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

        // ===== 2026-09-11(6): 싱글턴 — InventoryWindow.ProcessDrag의 Loot 소스 드롭 판정이 참조 =====
        private static LootWindow _instance;
        public static LootWindow Instance => _instance;

        // ===== 2026-09-12(P7): InventoryWindow Loot 컨텍스트 데이터 API =====
        // 바구니 열림이 통합창(InventoryWindow.ContextMode.Loot)으로 리다이렉트된 후에도
        // 렌더/획득 데이터는 기존 캐시/획득 파이프라인을 그대로 재사용한다.
        // (LootWindow 자체 OnGUI 팝업 경로는 호출되지 않음 — 클래스/필드/로직 유지)

        /// <summary>캐시된 전리품 항목 수 (RefreshLoot 결과 — 빈 바구니 판정용). 캐시 null = 0.</summary>
        public int CachedItemCount => (_cachedItems != null) ? _cachedItems.Length : 0;

        /// <summary>캐시된 전리품 항목 반환 (통합창 우측 그리드 렌더용). 범위 밖/캐시 null = null.</summary>
        public LootEntry GetCachedItem(int index)
            => (_cachedItems != null && index >= 0 && index < _cachedItems.Length) ? _cachedItems[index] : null;

        /// <summary>
        /// 통합창 Loot 컨텍스트 [전부 획득] 버튼 위임 — 기존 TakeAllItems(전체 획득 + RefreshLoot)를
        /// 그대로 실행한다. 바구니가 비면 RefreshLoot이 _cachedItems=null로 만들어
        /// InventoryWindow 측 CachedItemCount==0 판정 → Loot 컨텍스트가 닫힌다.
        /// </summary>
        public void TakeAllFromBasket() => TakeAllItems();

        // ===== 2026-09-11(6): DnD 드롭 판정용 슬롯 화면 Rect 캐시 (정적 — GC 캐시 관례, WarehouseUI 선례) =====
        private static readonly List<Rect> s_slotScreenRects = new List<Rect>(32);   // 아이템 있는 전리품 슬롯
        private static readonly List<int> s_slotScreenIndices = new List<int>(32);   // 바구니 항목 인덱스

        protected override void Awake()
        {
            base.Awake();
            _instance = this;
        }

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

            // 2026-09-11(6): 창이 닫히면 Loot 소스 드래그 잔여 정리 + 슬롯 Rect 캐시 무효화
            // (스테일 Rect 오드롭/고스트 잔상 방지 — WarehouseUI 잔여 드래그 정리 선례)
            if (ItemDragContext.Active && ItemDragContext.SourceType == ItemDragContext.Source.Loot)
                ItemDragContext.Cancel();
            s_slotScreenRects.Clear();
            s_slotScreenIndices.Clear();
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
        protected override void OnDestroy()
        {
            if (_texWhite != null)
            {
                Destroy(_texWhite);
                _texWhite = null;
            }
            base.OnDestroy();
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
        // DrawWindowContent — IMGUI 렌더링 (UIWindow.OnGUI → DrawWindowContent 파이프라인)
        // ===================================================================
        protected override void DrawWindowContent()
        {
            InitStyles();

            if (_currentBasket == null || _currentBasket.IsEmpty || !_currentBasket.IsAvailable)
            {
                Hide();
                return;
            }

            if (_cachedItems == null || _cachedItems.Length != _currentBasket.ItemCount)
                RefreshLoot();

            // 2026-09-11(6): 우측 1/3 구획 고정 배치 — 인벤 창 제3구획(컨텍스트)과 동일 좌표 규약.
            // x = Screen.width*2/3 + 6 (InventoryWindow.GetContextX 선례), y = 10 (인벤 창 상단 여백 규약).
            float x = InventoryWindow.GetContextX(WINDOW_WIDTH);
            float y = 10f;

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

            // === 2026-09-11(6): Loot 소스 드래그 보조 — 인벤이 닫혀 판정 주체가 없을 때 자체 처리 ===
            if (ItemDragContext.Active && ItemDragContext.SourceType == ItemDragContext.Source.Loot
                && (InventoryWindow.Instance == null || !InventoryWindow.Instance.IsOpen))
            {
                if (Event.current.type == EventType.MouseUp)
                {
                    // 전리품 슬롯 위 MouseUp은 DrawItemGrid의 폴백(클릭=획득)이 소비했으므로
                    // 여기 온 MouseUp은 슬롯 밖 드롭 = 취소 (WarehouseUI 잔여 드래그 정리 선례)
                    ItemDragContext.Cancel();
                    Event.current.Use();
                }
                ItemDragContext.DrawGhost();   // 인벤이 렌더 주체가 아니므로 여기서 고스트 (프레임 가드 내장 — 이중 렌더 무해)
            }
            // 인벤이 열려 있으면 InventoryWindow.ProcessDrag의 Loot 분기가 고스트/드롭 판정을 담당한다.
        }

        // ===================================================================
        // 아이템 그리드
        // ===================================================================
        private void DrawItemGrid(float panelX, float gridY, float gridHeight)
        {
            float innerX = panelX + 4;
            float innerY = gridY + 2;
            float innerWidth = WINDOW_WIDTH - 8;

            // 2026-09-11(6): DnD 드롭 판정용 슬롯 화면 Rect 캐시 리빌드 (매 프레임 — InventoryWindow/WarehouseUI 선례)
            s_slotScreenRects.Clear();
            s_slotScreenIndices.Clear();

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

                    // 2026-09-11(6): 드롭 판정용 스크린 Rect 캐시 — GUIToScreenPoint y 상승계 보정
                    // (sp.y는 슬롯 윗변 → yMin = sp.y - height. InventoryWindow L710 수리 선례 동일)
                    Vector2 slotScreenPos = GUIUtility.GUIToScreenPoint(new Vector2(sx, sy));
                    s_slotScreenRects.Add(new Rect(slotScreenPos.x, slotScreenPos.y - slotHeight, slotWidth, slotHeight));
                    s_slotScreenIndices.Add(i);

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

                    // 2026-09-11(6): 좌클릭 MouseDown → 드래그 시작 (기존 "즉시 TakeItem" 대체).
                    // - 인벤 열림: InventoryWindow.ProcessDrag의 Loot 소스 분기가 MouseUp 드롭 판정 대행
                    //   (인벤 그리드 위 드롭=이동, 전리품 슬롯 위 뗌=획득, 그 외=취소)
                    // - 인벤 닫힘: 아래 MouseUp 자체 폴백이 클릭=획득을 유지
                    if (isMouseDown && currentEvent.button == 0 && !ItemDragContext.Active
                        && _rectSlot.Contains(currentEvent.mousePosition))
                    {
                        _selectedIndex = i;
                        ItemDragContext.Begin(ItemDragContext.Source.Loot, i, entry.Item);
                        currentEvent.Use();
                    }
                    else if (currentEvent.type == EventType.MouseUp && currentEvent.button == 0
                             && _rectSlot.Contains(currentEvent.mousePosition)
                             && ItemDragContext.Active && ItemDragContext.SourceType == ItemDragContext.Source.Loot
                             && ItemDragContext.SourceIndex == i
                             && (InventoryWindow.Instance == null || !InventoryWindow.Instance.IsOpen))
                    {
                        // 인벤이 닫혀 판정 주체(InventoryWindow.ProcessDrag)가 없으면 자체 폴백 — 클릭=획득 유지
                        _selectedIndex = i;
                        currentEvent.Use();
                        TakeSelectedItem(i);
                        if (ItemDragContext.Active && ItemDragContext.SourceType == ItemDragContext.Source.Loot)
                            ItemDragContext.Cancel();

                        // 2026-09-11(6) 하드닝: 획득으로 바구니가 비면 RefreshLoot이 _cachedItems=null로 만들고
                        // 창이 자동 Hide된다 — 루프 조건/후속 접근 NRE 방지를 위해 즉시 탈출.
                        if (_cachedItems == null || i >= _cachedItems.Length) break;
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
        // 2026-09-11(6): DnD — ItemDragContext 공유 컨텍스트 (InventoryWindow.ProcessDrag의 Loot 분기와 짝)
        // ===================================================================

        /// <summary>
        /// 화면(GUI) 좌표가 전리품 슬롯 위인지 (InventoryWindow ProcessDrag의 Loot 소스 MouseUp 판정용).
        /// out slotIndex: 바구니 항목 인덱스. 캐시는 그리드 렌더 프레임에 리빌드된다 (WarehouseUI 선례).
        /// </summary>
        public static bool TryGetSlotAtScreenPoint(Vector2 guiPoint, out int slotIndex)
        {
            // 캐시 Rect는 GUIToScreenPoint 결과(스크린 좌표계 — 좌하단 원점, y 상승) — GUI점을 같은 변환으로 통일
            Vector2 sp = GUIUtility.GUIToScreenPoint(guiPoint);
            for (int i = 0; i < s_slotScreenRects.Count; i++)
            {
                if (s_slotScreenRects[i].Contains(sp))
                {
                    slotIndex = s_slotScreenIndices[i];
                    return true;
                }
            }
            slotIndex = -1;
            return false;
        }

        /// <summary>
        /// 전리품→인벤 드래그 드롭: 드래그 중 전리품 슬롯(ItemDragContext.SourceIndex)의 아이템을
        /// 플레이어 인벤토리로 이동 (LootBasket.TakeItem이 PlayerInventory.AddItem을 대행).
        /// 성공 시 RefreshLoot(바구니가 비면 기존 규약대로 자동 Hide). 성공 true.
        /// </summary>
        public static bool TryTakeDraggedToInventory()
        {
            if (ItemDragContext.SourceType != ItemDragContext.Source.Loot) return false;
            if (ItemDragContext.SourceIndex < 0) return false;
            if (_instance == null) return false;
            var basket = _instance._currentBasket;
            if (basket == null) return false;

            if (!basket.TakeItem(ItemDragContext.SourceIndex))
                return false;   // 인벤 가득 참 등 — 변경 없음 (드래그는 Cancel만)

            Debug.Log($"[LootWindow] 드래그 획득: {ItemDragContext.Item?.displayName ?? "?"}");
            _instance.RefreshLoot();   // 비었으면 자동 Hide (기존 규약 유지)
            return true;
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
                PlayerInventory.ItemCategory.Drug => new Color(0.9f, 0.2f, 0.6f),
                PlayerInventory.ItemCategory.Weapon => new Color(0.8f, 0.2f, 0.2f),
                PlayerInventory.ItemCategory.Armor => new Color(0.3f, 0.4f, 0.8f),
                PlayerInventory.ItemCategory.Tool => new Color(0.6f, 0.5f, 0.3f),
                PlayerInventory.ItemCategory.Arrow => new Color(0.6f, 0.3f, 0.1f),
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