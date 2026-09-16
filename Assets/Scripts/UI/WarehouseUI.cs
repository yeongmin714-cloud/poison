using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.Core.Data;
using ProjectName.Core.Themes;
using System.Linq;
using UnityEngine;
using ProjectName.UI.Themes;
using Game.UI.Core;

namespace ProjectName.UI
{
    /// <summary>
    /// 영지 창고 UI — IMGUI 기반 4×5 그리드.
    /// UIManager를 통해 열기/닫기.
    /// 
    /// [5.6.2] 영지 창고:
    /// - 영지별 독립 창고 인벤토리 (20슬롯)
    /// - 영지 간 아이템 이동 기능 ("다른 영지로 보내기")
    /// - 웨어하우스 UI에서 영지 선택 드롭다운
    /// </summary>
    public class WarehouseUI : UIWindow
    {
        private string _currentTerritoryId = "default";
        private Vector2 _scrollPos;
        private const int SlotsPerRow = 4;
        private const int MaxSlots = 20;

        // ===== [2026-09-15 Phase G-UI] 해상도 비례 스케일 =====
        // HUD._canvasScale/InventoryWindow 선례와 동일 산식. 고정 px 상수는 화면 크기가
        // 바뀌면 비율이 깨지므로 _uiScale 배수 프로퍼티로 전환해 사용처 이름 그대로 자동 비례한다.
        private static float _uiScale = 1f;
        private static int _uiScaleW = -1, _uiScaleH = -1;
        public static float UIScale => _uiScale;
        private static void RefreshUIScale()
        {
            if (Screen.width == _uiScaleW && Screen.height == _uiScaleH) return;
            _uiScaleW = Screen.width; _uiScaleH = Screen.height;
            _uiScale = Mathf.Max(0.35f, Mathf.Sqrt((Screen.width / 1920f) * (Screen.height / 1080f)));
        }
        private static float SlotSize => 64f * _uiScale;   // 슬롯 셀 크기 (해상도 비례)
        private static float Padding => 5f * _uiScale;

        // ===== [2026-09-16] AAA 4레이어 중세 배경 창 rect (LootWindow/InventoryWindow 규약 흡수) =====
        // 원래 GUILayout 콘텐츠는 화면 (0,0) 기준 오토레이아웃 — AAA 프레임을 이 rect에 맞춰 그린다.
        private static float WINDOW_WIDTH => 560f * _uiScale;      // 4열 슬롯 + 헤더/드롭다운 폭 수용
        private static float TITLE_BAR_HEIGHT => 88f * _uiScale;   // 타이틀 배너 높이
        private static float WINDOW_HEIGHT => 680f * _uiScale;     // 디폴트 높이 (화면 초과 시 DrawWindowContent에서 effH 클램프)

        // === 영지 선택 드롭다운 ===
        private string[] _territoryOptions;
        private int _selectedTerritoryIndex = 0;
        private bool _showTerritoryDropdown = false;

        // === 영지 간 이동 ===
        private int _selectedSlotIndex = -1;
        private string _targetTerritoryId;
        private bool _showTransferUI = false;
        private int _transferCount = 1;

        // === 인벤토리 → 창고 ===
        private bool _showInventoryTransfer = false;
#pragma warning disable 0414
        private int _selectedInventorySlot = -1;
#pragma warning restore 0414
        private int _invTransferCount = 1;
        private Vector2 _invScrollPos;

        // === GC 최적화: 캐시된 필드 ===
        private string _cachedHeader;
        private string _lastHeaderTerritory;
#pragma warning disable 0414
        private int _lastHeaderCount = -1;
#pragma warning restore 0414
        private Rect _itemNameRect = new Rect(0, 0, 0, 18);
        private Rect _countRect = new Rect(0, 0, 18, 18);
        private Rect _transferRect = new Rect(0, 0, 16, 16);
        private Rect _iconRect = new Rect(0, 0, 0, 0);
        private string _countLabel;

        // === 스타일 ===
        private GUIStyle _styleTitle;
        private GUIStyle _styleTitleBar;         // [2026-09-16] AAA 배너 위 제목 (UIFont.Title×_uiScale)
        private GUIStyle _styleLabel;
        private GUIStyle _styleButton;
        private GUIStyle _styleSlot;
        private GUIStyle _styleSlotSelected;
        private GUIStyle _styleDropdown;
        // ===== AAA 4레이어 중세 배경 (InventoryArtLibrary 텍스처 — static 캐시 1회 생성, 파기 금지) =====
        private static GUIStyle _styleBackplate;   // Layer 2 스톤 패널 (9-Slice border 24)
        private static GUIStyle _styleMetalFrame;  // Layer 3 금속 프레임 (9-Slice border 16)
        private bool _stylesInitialized;
        private float _uiScaleUsedForStyles = -1f;   // [Phase G-UI] 스타일 생성 시점의 스케일

        // === 테마 컬러 — 2026-09-11 Flat 토큰 (다크 네이비 + 회백 보더 + 스카이블루 액센트; InventoryWindow 통일) ===
        private static readonly Color ColorBg = new Color(0.11f, 0.11f, 0.11f, 0.88f);      // 창 배경 (다크 네이비)
        private static readonly Color ColorTitleBar = new Color(0.055f, 0.078f, 0.125f, 0.95f); // 타이틀 스트립 (더 어두운 네이비)
        private static readonly Color ColorSlotBg = new Color(0.09f, 0.12f, 0.19f, 0.9f);      // 슬롯 배경 (짙은 네이비)
        private static readonly Color ColorSlotHover = new Color(0.14f, 0.20f, 0.30f, 0.9f);   // 슬롯 호버
        private static readonly Color ColorSlotSelected = new Color(0.16f, 0.28f, 0.42f, 1f);  // 슬롯 선택 (스카이블루 틴트)
        private static readonly Color ColorTextPrimary = new Color(1f, 1f, 1f, 1f);            // 기본 텍스트 (흰색)
        private static readonly Color ColorTextSecondary = new Color(0.85f, 0.88f, 0.92f, 1f); // 보조 텍스트
        private static readonly Color ColorTextDim = new Color(0.72f, 0.76f, 0.82f, 1f);       // 흐린 텍스트
        private static readonly Color ColorAccent = new Color(0.29f, 0.48f, 0.81f, 1f);        // 강조 (스카이블루)
        private static readonly Color ColorBorder = new Color(0.62f, 0.70f, 0.78f, 0.85f);     // 테두리 (얇은 회백)

        protected override void Awake()
        {
            base.Awake();
            ApplyTheme(Phase33_Themes.CreateWarehouseTheme());
            RefreshTerritoryList();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_styleTitle != null)
            {
                if (_styleTitle.normal?.background != null) Destroy(_styleTitle.normal.background);
                if (_styleLabel.normal?.background != null) Destroy(_styleLabel.normal.background);
                if (_styleButton?.normal?.background != null) Destroy(_styleButton.normal.background);
                if (_styleButton?.hover?.background != null) Destroy(_styleButton.hover.background);
                if (_styleButton?.active?.background != null) Destroy(_styleButton.active.background);
                if (_styleSlot?.normal?.background != null) Destroy(_styleSlot.normal.background);
                if (_styleSlotSelected?.normal?.background != null) Destroy(_styleSlotSelected.normal.background);
                if (_styleDropdown?.normal?.background != null) Destroy(_styleDropdown.normal.background);
            }
        }

        public void SetTerritory(string territoryId)
        {
            _currentTerritoryId = territoryId ?? "default";
            RefreshTerritoryList();
            // 선택된 인덱스 업데이트
            for (int i = 0; i < _territoryOptions.Length; i++)
            {
                if (_territoryOptions[i] == _currentTerritoryId)
                {
                    _selectedTerritoryIndex = i;
                    break;
                }
            }
        }

        private void RefreshTerritoryList()
        {
            if (TerritoryDatabase.Instance != null)
            {
                var defs = TerritoryDatabase.Instance.GetAllDefinitions();
                if (defs != null)
                {
                    var list = new List<string>();
                    foreach (var def in defs)
                    {
                        string name = string.IsNullOrEmpty(def.territoryName) ? def.id.ToString() : def.territoryName;
                        list.Add(name);
                    }
                    _territoryOptions = list.ToArray();
                    return;
                }
            }
            // 폴백
            _territoryOptions = new string[] { _currentTerritoryId, "default" };
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            Font font = UIFont.Load();   // P7-1: 한글 서포트 커스텀 폰트

            _styleTitle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = (int)(18f * _uiScale),   // [Phase G-UI] 해상도 비례
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ColorTextPrimary },
                padding = new RectOffset(12, 4, 0, 0)
            };

            // [2026-09-16] AAA 배너 위 제목 — UIFont.Title(38)×_uiScale 한글 서포트 (LootWindow 선례)
            _styleTitleBar = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = (int)(UIFont.Title * _uiScale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ColorTextPrimary }
            };

            _styleLabel = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = (int)(13f * _uiScale),   // [Phase G-UI] 해상도 비례
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ColorTextSecondary },
                padding = new RectOffset(8, 4, 0, 0)
            };

            _styleButton = new GUIStyle(GUI.skin.button)
            {
                font = font,
                fontSize = (int)(13f * _uiScale),   // [Phase G-UI] 해상도 비례
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(4, 4, 2, 2),
                normal = { textColor = ColorTextPrimary, background = MakeTexture(1, 1, ColorSlotHover) },
                hover = { textColor = ColorTextPrimary, background = MakeTexture(1, 1, new Color(0.18f, 0.25f, 0.36f, 1f)) },   // Flat: 다크 슬레이트 호버
                active = { textColor = ColorTextPrimary, background = MakeTexture(1, 1, ColorSlotSelected) }
            };

            _styleSlot = new GUIStyle(GUI.skin.box)
            {
                font = font,
                normal = { background = MakeTexture(1, 1, ColorSlotBg), textColor = ColorTextPrimary },
                border = new RectOffset(1, 1, 1, 1),
                padding = new RectOffset(2, 2, 2, 2),
                margin = new RectOffset(1, 1, 1, 1)
            };

            _styleSlotSelected = new GUIStyle(_styleSlot)
            {
                normal = { background = MakeTexture(1, 1, ColorSlotSelected), textColor = ColorAccent }
            };

            _styleDropdown = new GUIStyle(GUI.skin.box)
            {
                font = font,
                fontSize = (int)(13f * _uiScale),   // [Phase G-UI] 해상도 비례
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ColorTextPrimary, background = MakeTexture(1, 1, ColorSlotBg) },
                padding = new RectOffset(8, 4, 4, 4)
            };

            // ===== AAA 4레이어 중세 배경 스타일 (ArtLibrary static 텍스처 — static 캐시 1회 생성) =====
            // Layer 2 백플레이트 — 스톤 패널 (9-Slice, border 24 등소비)
            if (_styleBackplate == null)
            {
                _styleBackplate = new GUIStyle(GUI.skin.box)
                {
                    normal = { background = InventoryArtLibrary.GetBackplate(), textColor = ColorTextPrimary },
                    border = new RectOffset(24, 24, 24, 24),
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0)
                };
            }
            // Layer 3 금속 프레임 — (9-Slice, border 16)
            if (_styleMetalFrame == null)
            {
                _styleMetalFrame = new GUIStyle(GUI.skin.box)
                {
                    normal = { background = InventoryArtLibrary.GetMetalFrame(), textColor = ColorTextPrimary },
                    border = new RectOffset(16, 16, 16, 16),
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0)
                };
            }

            _stylesInitialized = true;
        }

        public override void Show()
        {
            base.Show();
            _scrollPos = Vector2.zero;
            _selectedSlotIndex = -1;
            _showTransferUI = false;
            _showInventoryTransfer = false;
            _selectedInventorySlot = -1;
            _invTransferCount = 1;
            _invScrollPos = Vector2.zero;
            RefreshTerritoryList();
        }

        // ===================================================================
        // OnGUI — IMGUI 렌더링 (UIWindow.OnGUI 오버라이드)
        // base.OnGUI() 호출 생략: 기본 평면 배경(_theme 패턴 텍스처)과 테마 데코레이션
        // (그라디언트+장식 테두리)을 해당 평면 배경으로 남겨 두지 않고, 아래 DrawWindowContent가
        // 그리는 AAA 4레이어 중세 배경으로 완전 대체한다. — 이중 렌더 방지 (InventoryWindow/LootWindow 선례)
        // 드롭/이동/탭 로직은 DrawWindowContent 내부 GUILayout 그대로 유지.
        // ===================================================================
        protected override void OnGUI()
        {
            if (!IsOpen) return;
            DrawWindowContent();
        }

        // ===================================================================
        // DrawWindowContent — IMGUI 렌더링 (UIWindow.DrawWindowContent 오버라이드)
        // ===================================================================
        protected override void DrawWindowContent()
        {
            if (!IsOpen) return;
            RefreshUIScale();   // [Phase G-UI] 해상도 변경 감지 → 비례 스케일 갱신
            if (!Mathf.Approximately(_uiScaleUsedForStyles, _uiScale))   // 스케일 변경 → 폰트 스타일 재생성
            {
                _uiScaleUsedForStyles = _uiScale;
                _stylesInitialized = false;
            }
            InitStyles();

            if (WarehouseSystem.Instance == null)
            {
                GUILayout.Label("WarehouseSystem이 없습니다.");
                return;
            }

            // ===== AAA 4레이어 중세 배경 (InventoryArtLibrary — InventoryWindow/LootWindow 선례) =====
            // 창 rect: 드롭섀도우→백플레이트→금속프레임+4모서리→타이틀 배너 순. 높이는 화면 초과 시 클램프.
            float ww = WINDOW_WIDTH;
            float effH = Mathf.Min(WINDOW_HEIGHT, Screen.height - 30f);   // 화면 초과 클램프

            // ① 드롭섀도우 — 창 rect 12px 사방 확장, 검정 0.55 tint
            DrawWindowDropShadow(0f, 0f, ww, effH);
            // ② 백플레이트 — 스톤 패널 (9-Slice border 24)
            GUI.Box(new Rect(0f, 0f, ww, effH), "", _styleBackplate);
            // ③ 금속 프레임(9-Slice border 16) + 4모서리 로터스 장식 (GUI.matrix 회전·복원 내장)
            DrawWindowFrame(0f, 0f, ww, effH);
            // ④ 타이틀 배너 (제목 텍스트 배경)
            DrawTitleStrip(0f, 0f, ww);

            // 제목 텍스트 — UIFont.Title(38)×_uiScale 한글 서포트 (LootWindow 선례)
            GUI.Label(new Rect(0f, 2f, ww, TITLE_BAR_HEIGHT), "📦 영지 창고", _styleTitleBar);

            // ===== 내용: 기존 GUILayout 로직 (드래그/이동/탭 무수정) — AAA 프레임 안쪽 영역에 배치 =====
            float pad = 14f * _uiScale;
            GUILayout.BeginArea(new Rect(pad, TITLE_BAR_HEIGHT + 4f, ww - pad * 2f, effH - TITLE_BAR_HEIGHT - 10f));

            // ===== 상단 영역: 영지 선택 드롭다운 + 헤더 =====
            DrawHeader();

            // ===== 아이템 슬롯 그리드 =====
            DrawItemGrid();

            // ===== 하단: 영지 간 이동 UI / 액션 버튼 =====
            DrawActionArea();

            // 2026-09-11(3): 인벤 창이 닫혀 드롭 판정 주체가 없으면 드래그 강제 종료 (고스트 잔상 방지)
            if (ItemDragContext.Active &&
                (InventoryWindow.Instance == null || !InventoryWindow.Instance.IsOpen))
                ItemDragContext.Cancel();

            GUILayout.EndArea();
        }

        // ===================================================================
        // 상단: 영지 선택 드롭다운 + 헤더
        // ===================================================================
        private void DrawHeader()
        {
            GUILayout.BeginHorizontal();

            // 영지 선택 드롭다운
            GUILayout.Label("🏰 영지: ", _styleLabel, GUILayout.Width(80f * _uiScale));

            if (GUILayout.Button(_territoryOptions != null && _selectedTerritoryIndex < _territoryOptions.Length
                ? _territoryOptions[_selectedTerritoryIndex] : _currentTerritoryId,
                _styleDropdown, GUILayout.Width(200f * _uiScale), GUILayout.Height(28f * _uiScale)))
            {
                _showTerritoryDropdown = !_showTerritoryDropdown;
            }

            GUILayout.FlexibleSpace();

            // 창고 용량 표시
            var items = WarehouseSystem.Instance.GetItems(_currentTerritoryId);
            int count = items != null ? items.Count : 0;
            GUILayout.Label($"📦 {count}/{MaxSlots}", _styleLabel, GUILayout.Width(100f * _uiScale));

            GUILayout.EndHorizontal();

            // 드롭다운 목록
            if (_showTerritoryDropdown && _territoryOptions != null)
            {
                GUILayout.BeginVertical(_styleDropdown);
                for (int i = 0; i < _territoryOptions.Length; i++)
                {
                    string optionName = _territoryOptions[i];
                    bool isCurrent = i == _selectedTerritoryIndex;

                    if (GUILayout.Button(isCurrent ? $"👉 {optionName}" : $"   {optionName}",
                        _styleButton, GUILayout.Height(24f * _uiScale)))
                    {
                        if (!isCurrent)
                        {
                            _selectedTerritoryIndex = i;
                            // territoryId 찾기
                            if (TerritoryDatabase.Instance != null)
                            {
                                var defsList = TerritoryDatabase.Instance.GetAllDefinitions().ToList();
                                if (defsList != null && i < defsList.Count)
                                {
                                    _currentTerritoryId = defsList[i].id.ToString();
                                }
                            }
                            else
                            {
                                _currentTerritoryId = optionName;
                            }
                            _selectedSlotIndex = -1;
                            _showTransferUI = false;
                            _scrollPos = Vector2.zero;
                        }
                        _showTerritoryDropdown = false;
                        break;
                    }
                }
                GUILayout.EndVertical();
            }

            // 구분선
            GUILayout.Box("", GUILayout.Height(2f * _uiScale), GUILayout.ExpandWidth(true));
        }

        // ===================================================================
        // 아이템 슬롯 그리드 (4×5)
        // ===================================================================
        private void DrawItemGrid()
        {
            // 2026-09-11(3): DnD 드롭 판정 캐시 리빌드 (빈 창고 포함 — 스테일 Rect 방지)
            s_slotRects.Clear();
            s_slotIndices.Clear();
            s_slotTerritoryId = _currentTerritoryId;

            var items = WarehouseSystem.Instance.GetItems(_currentTerritoryId);
            int totalSlots = items != null ? items.Count : 0;

            if (totalSlots == 0)
            {
                GUILayout.Label("   창고가 비어 있습니다.", _styleLabel);
                return;
            }

            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(320f * _uiScale));

            int rows = Mathf.CeilToInt((float)totalSlots / SlotsPerRow);
            for (int r = 0; r < rows; r++)
            {
                GUILayout.BeginHorizontal();
                for (int c = 0; c < SlotsPerRow; c++)
                {
                    int idx = r * SlotsPerRow + c;
                    if (idx < totalSlots)
                    {
                        var slot = items[idx];
                        if (slot != null && slot.item != null && slot.count > 0)
                        {
                            DrawSlot(idx, slot, idx == _selectedSlotIndex);
                        }
                        else
                        {
                            DrawEmptySlot();
                        }
                    }
                    else
                    {
                        DrawEmptySlot();
                    }
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
        }

        private void DrawSlot(int index, PlayerInventory.ItemSlot slot, bool isSelected)
        {
            var rect = GUILayoutUtility.GetRect(SlotSize, SlotSize);
            var style = isSelected ? _styleSlotSelected : _styleSlot;
            GUI.Box(rect, "", style);

            // 2026-09-11(3): DnD 드롭 판정용 슬롯 화면 Rect 캐시
            // 2026-09-11(4) 수리: GUIToScreenPoint 결과(sp.y)는 슬롯 윗변(스크린 y 상승계의 yMax) —
            // yMin = sp.y - height 보정 없으면 유령 영역(한 칸 위)에 판정되어 드롭이 먹히지 않았다.
            Vector2 sp = GUIUtility.GUIToScreenPoint(new Vector2(rect.x, rect.y));
            s_slotRects.Add(new Rect(sp.x, sp.y - rect.height, rect.width, rect.height));
            s_slotIndices.Add(index);

            float iconSize = SlotSize * 0.55f;
            // GC 최적화: 캐시된 Rect 재사용 ([Phase G-UI] 좌표/크기 해상도 비례)
            _iconRect.x = rect.x + (rect.width - iconSize) / 2;
            _iconRect.y = rect.y + 3f * _uiScale;
            _iconRect.width = iconSize;
            _iconRect.height = iconSize;
            DrawItemIcon(_iconRect, slot.item);

            // 아이템 이름
            _itemNameRect.x = rect.x + 2f * _uiScale;
            _itemNameRect.y = rect.y + iconSize + 2f * _uiScale;
            _itemNameRect.width = rect.width - 4f * _uiScale;
            _itemNameRect.height = 14f * _uiScale;
            GUI.Label(_itemNameRect, TruncateText(slot.item.displayName, rect.width - 4f * _uiScale, _styleLabel), _styleLabel);

            // 수량
            if (slot.count > 1)
            {
                _countRect.x = rect.x + rect.width - 22f * _uiScale;
                _countRect.y = rect.y + rect.height - 18f * _uiScale;
                _countRect.width = 20f * _uiScale;
                _countRect.height = 16f * _uiScale;
                _countLabel = "x" + slot.count;
                GUI.Label(_countRect, _countLabel, _styleLabel);
            }

            // 인벤토리 이동 버튼 (▽)
            _transferRect.x = rect.x + rect.width - 18f * _uiScale;
            _transferRect.y = rect.y + 2f * _uiScale;
            _transferRect.width = 16f * _uiScale;
            _transferRect.height = 16f * _uiScale;
            if (GUI.Button(_transferRect, "▽", _styleButton))
            {
                WarehouseSystem.Instance.TransferToInventory(_currentTerritoryId, index, 1);
            }

            // 슬롯 클릭 → 선택 + 좌클릭 드래그 시작 (2026-09-11(3): 누른 채 이동하면 DnD, 클릭만 하면 선택)
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                if (Event.current.button == 0)
                    ItemDragContext.Begin(ItemDragContext.Source.Warehouse, index, slot.item, _currentTerritoryId);

                if (isSelected)
                {
                    _selectedSlotIndex = -1;
                    _showTransferUI = false;
                }
                else
                {
                    _selectedSlotIndex = index;
                    _showTransferUI = true;
                    _transferCount = 1;
                    // 기본 대상 영지: 현재와 다른 첫 번째 영지
                    SetDefaultTargetTerritory();
                }
                Event.current.Use();
            }
        }

        private void DrawEmptySlot()
        {
            var rect = GUILayoutUtility.GetRect(SlotSize, SlotSize);
            GUI.Box(rect, "", _styleSlot);
            // 2026-09-11(3): 빈 슬롯도 드롭 타겟 — 인덱스 -1로 캐시
            // 2026-09-11(4) 수리: 스크린 y 상승계 보정 (DrawSlot 주석 참조)
            Vector2 sp = GUIUtility.GUIToScreenPoint(new Vector2(rect.x, rect.y));
            s_slotRects.Add(new Rect(sp.x, sp.y - rect.height, rect.width, rect.height));
            s_slotIndices.Add(-1);
        }

        // ===================================================================
        // 2026-09-11(3): DnD — ItemDragContext 공유 컨텍스트 (InventoryWindow ProcessDrag가 드롭 판정 대행)
        // ===================================================================
        private static readonly List<Rect> s_slotRects = new List<Rect>(MaxSlots);
        private static readonly List<int> s_slotIndices = new List<int>(MaxSlots);
        private static string s_slotTerritoryId;

        /// <summary>화면(GUI) 좌표가 속한 창고 슬롯 반환. out slotIndex: 아이템 슬롯=인덱스, 빈 슬롯=-1. 미해당 시 false.</summary>
        public static bool TryGetSlotAtScreenPoint(Vector2 guiPoint, out int slotIndex)
        {
            // 2026-09-11(4) 수리: 캐시 Rect는 스크린 좌표계(y 상승) — GUI점(y 하강)을 같은 변환으로 통일.
            Vector2 sp = GUIUtility.GUIToScreenPoint(guiPoint);
            for (int i = 0; i < s_slotRects.Count; i++)
            {
                if (s_slotRects[i].Contains(sp))
                {
                    slotIndex = s_slotIndices[i];
                    return true;
                }
            }
            slotIndex = -1;
            return false;
        }

        /// <summary>
        /// 인벤→창고 드래그 드롭: 인벤에서 1개 제거 후 현재 영지 창고에 보관 (실패 시 인벤 롤백). 성공 true.
        /// </summary>
        public static bool TryDepositFromDrag(PlayerInventory.ItemData item)
        {
            if (WarehouseSystem.Instance == null || PlayerInventory.Instance == null || item == null)
                return false;
            string tid = s_slotTerritoryId;
            if (string.IsNullOrEmpty(tid)) return false;

            bool removed = PlayerInventory.Instance.RemoveItem(item.id, 1);
            if (!removed) return false;

            if (!WarehouseSystem.Instance.AddItem(tid, item, 1))
            {
                // 창고 가득 → 인벤 롤백
                PlayerInventory.Instance.AddItem(item, 1);
                Debug.LogWarning("[WarehouseUI] 창고가 가득 찼습니다 — 드래그 보관 취소");
                return false;
            }
            return true;
        }

        /// <summary>창고→인벤 드래그 드롭: 드래그 중 창고 슬롯 아이템 1개를 인벤으로 이동.</summary>
        public static void TransferDraggedToInventory()
        {
            if (WarehouseSystem.Instance == null) return;
            if (ItemDragContext.SourceType != ItemDragContext.Source.Warehouse) return;
            if (ItemDragContext.SourceIndex < 0 || string.IsNullOrEmpty(ItemDragContext.TerritoryId)) return;

            if (!WarehouseSystem.Instance.TransferToInventory(ItemDragContext.TerritoryId, ItemDragContext.SourceIndex, 1))
                Debug.LogWarning("[WarehouseUI] 인벤으로 이동 실패 (인벤 가득 or 슬롯 상태 변경)");
        }

        /// <summary>창고 내 슬롯↔슬롯 드래그 스왑 (같은 영지 내 위치 교환 — 뒤 인덱스부터 제거해 시프트 오염 방지).</summary>
        public static void SwapDraggedSlots(int targetIndex)
        {
            if (WarehouseSystem.Instance == null) return;
            int src = ItemDragContext.SourceIndex;
            string tid = ItemDragContext.TerritoryId;
            if (src < 0 || targetIndex < 0 || src == targetIndex || string.IsNullOrEmpty(tid)) return;

            var items = WarehouseSystem.Instance.GetItems(tid);
            if (items == null || src >= items.Count || targetIndex >= items.Count) return;

            var srcSlot = items[src];
            var dstSlot = items[targetIndex];
            if (srcSlot?.item == null && dstSlot?.item == null) return;   // 빈↔빈 — 무의미

            int hi = Mathf.Max(src, targetIndex);
            int lo = Mathf.Min(src, targetIndex);
            int hiCount = items[hi]?.item != null ? items[hi].count : 0;
            int loCount = items[lo]?.item != null ? items[lo].count : 0;

            // 뒤 인덱스부터 제거 (RemoveAt 시프트 대응)
            if (hiCount > 0 && !WarehouseSystem.Instance.RemoveItem(tid, hi, hiCount))
                return;
            if (loCount > 0 && !WarehouseSystem.Instance.RemoveItem(tid, lo, loCount))
                return;

            // 교차 재삽입 (dst→src 순서 — src가 먼저 제거됐던 자리와 무관하게 내용만 교환)
            if (dstSlot?.item != null) WarehouseSystem.Instance.AddItem(tid, dstSlot.item, dstSlot.count);
            if (srcSlot?.item != null) WarehouseSystem.Instance.AddItem(tid, srcSlot.item, srcSlot.count);
        }

        // ===================================================================
        // 하단: 인벤토리 ↔ 창고 전송 + 영지 간 이동
        // ===================================================================
        private void DrawActionArea()
        {
            GUILayout.Space(4f * _uiScale);
            GUILayout.Box("", GUILayout.Height(1f * _uiScale), GUILayout.ExpandWidth(true));
            GUILayout.Space(4f * _uiScale);

            // === 인벤토리 → 창고 토글 버튼 ===
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_showInventoryTransfer ? "📦 창고 아이템 (현재)" : "🎒 인벤토리 → 창고",
                _styleButton, GUILayout.Width(200f * _uiScale), GUILayout.Height(28f * _uiScale)))
            {
                _showInventoryTransfer = !_showInventoryTransfer;
                if (_showInventoryTransfer)
                {
                    _selectedSlotIndex = -1;
                    _showTransferUI = false;
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (_showInventoryTransfer)
            {
                DrawInventoryToWarehouse();
            }
            else
            {
                DrawWarehouseTransferUI();
            }

            // 닫기 버튼
            GUILayout.Space(8f * _uiScale);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("닫기", _styleButton, GUILayout.Width(120f * _uiScale), GUILayout.Height(32f * _uiScale)))
            {
                Hide();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        /// <summary>인벤토리 아이템 → 창고로 전송 UI</summary>
        private void DrawInventoryToWarehouse()
        {
            if (PlayerInventory.Instance == null)
            {
                GUILayout.Label("   PlayerInventory가 없습니다.", _styleLabel);
                return;
            }

            var slots = PlayerInventory.Instance.GetAllSlots();
            if (slots == null || slots.Length == 0)
            {
                GUILayout.Label("   인벤토리가 비어 있습니다.", _styleLabel);
                return;
            }

            // 인벤토리 아이템 리스트
            _invScrollPos = GUILayout.BeginScrollView(_invScrollPos, GUILayout.Height(160f * _uiScale));

            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null || slot.item == null || slot.count <= 0) continue;

                GUILayout.BeginHorizontal();

                string symbol = GetCategorySymbol(slot.item.category);
                Color color = GetCategoryColor(slot.item.category);
                var oldColor = GUI.color;
                GUI.color = color;
                GUILayout.Box("", GUILayout.Width(24f * _uiScale), GUILayout.Height(24f * _uiScale));
                GUI.color = oldColor;

                GUILayout.Label($"{symbol} {slot.item.displayName} x{slot.count}", _styleLabel, GUILayout.Width(240f * _uiScale));

                if (GUILayout.Button("창고로", _styleButton, GUILayout.Width(80f * _uiScale), GUILayout.Height(24f * _uiScale)))
                {
                    // 1개를 창고로 이동
                    bool removed = PlayerInventory.Instance.RemoveItem(slot.item.id, 1);
                    if (removed)
                    {
                        bool added = WarehouseSystem.Instance.AddItem(_currentTerritoryId, slot.item, 1);
                        if (!added)
                        {
                            // 창고 가득 참 → 롤백
                            PlayerInventory.Instance.AddItem(slot.item, 1);
                            Debug.LogWarning("[WarehouseUI] 창고가 가득 찼습니다.");
                        }
                        else
                        {

                        }
                    }
                    break; // 슬롯 구조 변경 방지
                }

                // 수량 지정 전송
                GUILayout.Label("x", _styleLabel, GUILayout.Width(12f * _uiScale));
                string countStr = GUILayout.TextField(_invTransferCount.ToString(), GUILayout.Width(36f * _uiScale));
                int.TryParse(countStr, out _invTransferCount);
                _invTransferCount = Mathf.Clamp(_invTransferCount, 1, slot.count);

                if (GUILayout.Button("전송", _styleButton, GUILayout.Width(60f * _uiScale), GUILayout.Height(24f * _uiScale)))
                {
                    int transferCount = Mathf.Min(_invTransferCount, slot.count);
                    bool removed = PlayerInventory.Instance.RemoveItem(slot.item.id, transferCount);
                    if (removed)
                    {
                        bool added = WarehouseSystem.Instance.AddItem(_currentTerritoryId, slot.item, transferCount);
                        if (!added)
                        {
                            PlayerInventory.Instance.AddItem(slot.item, transferCount);
                            Debug.LogWarning("[WarehouseUI] 창고가 가득 찼습니다.");
                        }
                        else
                        {

                        }
                    }
                    break;
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
        }

        /// <summary>기존 창고 → 영지간 이동 / 인벤토리 전송 UI</summary>
        private void DrawWarehouseTransferUI()
        {
            if (_selectedSlotIndex >= 0 && _showTransferUI)
            {
                var items = WarehouseSystem.Instance.GetItems(_currentTerritoryId);
                if (items != null && _selectedSlotIndex < items.Count)
                {
                    var slot = items[_selectedSlotIndex];
                    if (slot != null && slot.item != null)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"선택: {slot.item.displayName} (x{slot.count})", _styleLabel, GUILayout.Width(250f * _uiScale));

                        // 수량 조절
                        if (GUILayout.Button("-", _styleButton, GUILayout.Width(24f * _uiScale), GUILayout.Height(24f * _uiScale)))
                        {
                            _transferCount = Mathf.Max(1, _transferCount - 1);
                        }
                        GUILayout.Label($"{_transferCount}", _styleLabel, GUILayout.Width(30f * _uiScale));
                        if (GUILayout.Button("+", _styleButton, GUILayout.Width(24f * _uiScale), GUILayout.Height(24f * _uiScale)))
                        {
                            _transferCount = Mathf.Min(slot.count, _transferCount + 1);
                        }

                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        // 대상 영지 선택
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("→ 다른 영지로 보내기:", _styleLabel, GUILayout.Width(150f * _uiScale));

                        if (_territoryOptions != null && _territoryOptions.Length > 1)
                        {
                            // 현재 영지 제외한 드롭다운
                            string[] targetOptions = GetOtherTerritoryOptions();
                            int targetIdx = System.Array.IndexOf(targetOptions, _targetTerritoryId);
                            if (targetIdx < 0) targetIdx = 0;

                            if (GUILayout.Button(targetOptions != null && targetIdx < targetOptions.Length
                                ? targetOptions[targetIdx] : "선택...", _styleDropdown, GUILayout.Width(180f * _uiScale), GUILayout.Height(24f * _uiScale)))
                            {
                                // 다음 대상으로 순환
                                CycleTargetTerritory();
                            }

                            if (GUILayout.Button("📦 보내기", _styleButton, GUILayout.Width(100f * _uiScale), GUILayout.Height(28f * _uiScale)))
                            {
                                TransferToOtherTerritory();
                            }
                        }
                        else
                        {
                            GUILayout.Label("다른 영지가 없습니다.", _styleLabel);
                        }

                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                    }
                }
            }
            else
            {
                GUILayout.Label("💡 슬롯을 클릭하여 다른 영지로 아이템을 보낼 수 있습니다.", _styleLabel);
            }
        }

        // ===================================================================
        // 영지 간 이동 로직
        // ===================================================================
        private void SetDefaultTargetTerritory()
        {
            if (_territoryOptions == null || _territoryOptions.Length <= 1)
            {
                _targetTerritoryId = null;
                return;
            }

            for (int i = 0; i < _territoryOptions.Length; i++)
            {
                string opt = _territoryOptions[i];
                // 현재 영지 이름과 다른 첫 번째 옵션 선택
                string currentName = _territoryOptions[_selectedTerritoryIndex];
                if (opt != currentName)
                {
                    _targetTerritoryId = opt;
                    return;
                }
            }
            _targetTerritoryId = null;
        }

        private string[] GetOtherTerritoryOptions()
        {
            if (_territoryOptions == null) return new string[0];
            var others = new List<string>();
            string currentName = _territoryOptions[_selectedTerritoryIndex];
            foreach (var opt in _territoryOptions)
            {
                if (opt != currentName)
                    others.Add(opt);
            }
            return others.ToArray();
        }

        private void CycleTargetTerritory()
        {
            var others = GetOtherTerritoryOptions();
            if (others.Length == 0) return;

            int currentIdx = System.Array.IndexOf(others, _targetTerritoryId);
            int nextIdx = (currentIdx + 1) % others.Length;
            _targetTerritoryId = others[nextIdx];
        }

        private void TransferToOtherTerritory()
        {
            if (WarehouseSystem.Instance == null)
            {
                Debug.LogWarning("[WarehouseUI] WarehouseSystem이 없습니다.");
                return;
            }

            if (string.IsNullOrEmpty(_targetTerritoryId))
            {
                Debug.LogWarning("[WarehouseUI] 대상 영지가 선택되지 않았습니다.");
                return;
            }

            // 현재 영지 창고에서 아이템 제거
            var items = WarehouseSystem.Instance.GetItems(_currentTerritoryId);
            if (items == null || _selectedSlotIndex < 0 || _selectedSlotIndex >= items.Count)
            {
                Debug.LogWarning("[WarehouseUI] 잘못된 슬롯 인덱스입니다.");
                return;
            }

            var slot = items[_selectedSlotIndex];
            if (slot == null || slot.item == null || slot.count < _transferCount)
            {
                Debug.LogWarning("[WarehouseUI] 아이템이 부족합니다.");
                return;
            }

            string itemId = slot.item.id;
            string itemName = slot.item.displayName;

            // 현재 영지에서 제거
            bool removed = WarehouseSystem.Instance.RemoveItem(_currentTerritoryId, _selectedSlotIndex, _transferCount);
            if (!removed)
            {
                Debug.LogWarning("[WarehouseUI] 아이템 제거 실패");
                return;
            }

            // 대상 영지에 추가
            string targetId = _targetTerritoryId;
            if (TerritoryDatabase.Instance != null)
            {
                var allDefs = TerritoryDatabase.Instance.GetAllDefinitions();
                if (allDefs != null)
                {
                    foreach (var def in allDefs)
                    {
                        if (def.territoryName == _targetTerritoryId)
                        {
                            targetId = def.id.ToString();
                            break;
                        }
                    }
                }
            }

            bool added = WarehouseSystem.Instance.AddItem(targetId, slot.item, _transferCount);
            if (!added)
            {
                // 실패 시 롤백 (현재 영지에 다시 추가)
                WarehouseSystem.Instance.AddItem(_currentTerritoryId, slot.item, _transferCount);
                Debug.LogWarning($"[WarehouseUI] 대상 영지({targetId}) 창고가 가득 찼습니다. 전송 취소.");
                return;
            }

            Debug.Log($"[WarehouseUI] {itemName} x{_transferCount} → {_targetTerritoryId} 전송 완료!");

            // UI 업데이트
            _selectedSlotIndex = -1;
            _showTransferUI = false;
        }

        // ===================================================================
        // 아이콘/유틸리티
        // ===================================================================
        private void DrawItemIcon(Rect rect, PlayerInventory.ItemData item)
        {
            if (item == null) return;

            Color color = GetCategoryColor(item.category);
            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.Box(rect, "");
            GUI.color = oldColor;

            string symbol = GetCategorySymbol(item.category);
            GUI.Label(rect, symbol);
        }

        private string TruncateText(string text, float maxWidth, GUIStyle style)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (style.CalcSize(new GUIContent(text)).x <= maxWidth) return text;
            for (int i = text.Length - 1; i > 0; i--)
            {
                string truncated = text.Substring(0, i) + "..";
                if (style.CalcSize(new GUIContent(truncated)).x <= maxWidth)
                    return truncated;
            }
            return "..";
        }

        private Color GetCategoryColor(PlayerInventory.ItemCategory cat)
        {
            switch (cat)
            {
                case PlayerInventory.ItemCategory.Herb: return new Color(0.2f, 0.8f, 0.2f, 0.5f);
                case PlayerInventory.ItemCategory.Meat: return new Color(0.8f, 0.3f, 0.2f, 0.5f);
                case PlayerInventory.ItemCategory.Food: return new Color(0.9f, 0.7f, 0.2f, 0.5f);
                case PlayerInventory.ItemCategory.Potion: return new Color(0.3f, 0.5f, 0.9f, 0.5f);
                case PlayerInventory.ItemCategory.Drug: return new Color(0.9f, 0.2f, 0.8f, 0.5f);
                case PlayerInventory.ItemCategory.Material: return new Color(0.6f, 0.6f, 0.6f, 0.5f);
                case PlayerInventory.ItemCategory.Quest: return new Color(1.0f, 0.8f, 0.0f, 0.5f);
                case PlayerInventory.ItemCategory.Weapon: return new Color(0.8f, 0.4f, 0.2f, 0.5f);
                case PlayerInventory.ItemCategory.Armor: return new Color(0.4f, 0.5f, 0.8f, 0.5f);
                case PlayerInventory.ItemCategory.Tool: return new Color(0.7f, 0.5f, 0.3f, 0.5f);
                case PlayerInventory.ItemCategory.Arrow: return new Color(0.6f, 0.3f, 0.1f, 0.5f);
                default: return new Color(0.5f, 0.5f, 0.5f, 0.5f);
            }
        }

        private string GetCategorySymbol(PlayerInventory.ItemCategory cat)
        {
            switch (cat)
            {
                case PlayerInventory.ItemCategory.Herb: return "🌿";
                case PlayerInventory.ItemCategory.Meat: return "🥩";
                case PlayerInventory.ItemCategory.Food: return "🍲";
                case PlayerInventory.ItemCategory.Potion: return "🧪";
                case PlayerInventory.ItemCategory.Drug: return "💊";
                case PlayerInventory.ItemCategory.Material: return "🪨";
                case PlayerInventory.ItemCategory.Quest: return "⭐";
                case PlayerInventory.ItemCategory.Weapon: return "🗡️";
                case PlayerInventory.ItemCategory.Armor: return "🛡️";
                case PlayerInventory.ItemCategory.Tool: return "🔧";
                case PlayerInventory.ItemCategory.Arrow: return "🏹";
                default: return "📦";
            }
        }

        // ===================================================================
        // AAA 4레이어 드로우 헬퍼 (InventoryArtLibrary 소비 — LootWindow/InventoryWindow 선례)
        // ===================================================================
        /// <summary>드롭섀도우 — 창 rect 12px 사방 확장해 소프트 섀도우 텍스처를 검정 tint로 단일 DrawTexture.</summary>
        private void DrawWindowDropShadow(float wx, float wy, float ww, float wh)
        {
            var prevColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(wx - 12f, wy - 12f, ww + 24f, wh + 24f), InventoryArtLibrary.GetWindowDropShadow());
            GUI.color = prevColor;
        }

        /// <summary>Layer 3 — 금속 프레임(9-Slice Box) + 4모서리 로터스 장식(GUI.matrix 0/90/180/270° 회전, 반드시 복원).</summary>
        private void DrawWindowFrame(float wx, float wy, float ww, float wh)
        {
            GUI.Box(new Rect(wx, wy, ww, wh), "", _styleMetalFrame);

            var orn = InventoryArtLibrary.GetCornerOrnament();
            float ornSize = 96f * _uiScale;
            DrawCornerOrnament(orn, new Rect(wx, wy, ornSize, ornSize), 0f);
            DrawCornerOrnament(orn, new Rect(wx + ww - ornSize, wy, ornSize, ornSize), 90f);
            DrawCornerOrnament(orn, new Rect(wx + ww - ornSize, wy + wh - ornSize, ornSize, ornSize), 180f);
            DrawCornerOrnament(orn, new Rect(wx, wy + wh - ornSize, ornSize, ornSize), 270f);
        }

        /// <summary>모서리 장식 1개 — rect 중심 pivot으로 angle도 회전 후 DrawTexture, matrix 반드시 복원.</summary>
        private void DrawCornerOrnament(Texture2D tex, Rect rect, float angle)
        {
            var prevMatrix = GUI.matrix;
            if (!Mathf.Approximately(angle, 0f))
                GUIUtility.RotateAroundPivot(angle, rect.center);
            GUI.DrawTexture(rect, tex);
            GUI.matrix = prevMatrix;
        }

        /// <summary>Layer 4 — 타이틀 배너 (제목 텍스트 배경).</summary>
        private void DrawTitleStrip(float wx, float wy, float ww)
        {
            float stripW = ww - 4f;
            float stripH = TITLE_BAR_HEIGHT - 8f;
            GUI.DrawTexture(new Rect(wx + 2f, wy + 4f, stripW, stripH), InventoryArtLibrary.GetTitleBanner());
        }

        private static Texture2D MakeTexture(int width, int height, Color color)
        {
            Texture2D tex = new Texture2D(width, height);
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}