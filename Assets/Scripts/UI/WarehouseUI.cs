using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.Core.Data;
using ProjectName.UI.Themes;
using System.Linq;
using UnityEngine;

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
        private const float SlotSize = 64f;
        private const float Padding = 5f;

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
        private int _selectedInventorySlot = -1;
        private int _invTransferCount = 1;
        private Vector2 _invScrollPos;

        // === GC 최적화: 캐시된 필드 ===
        private string _cachedHeader;
        private string _lastHeaderTerritory;
        private int _lastHeaderCount = -1;
        private Rect _itemNameRect = new Rect(0, 0, 0, 18);
        private Rect _countRect = new Rect(0, 0, 18, 18);
        private Rect _transferRect = new Rect(0, 0, 16, 16);
        private Rect _iconRect = new Rect(0, 0, 0, 0);
        private string _countLabel;

        // === 스타일 ===
        private GUIStyle _styleTitle;
        private GUIStyle _styleLabel;
        private GUIStyle _styleButton;
        private GUIStyle _styleSlot;
        private GUIStyle _styleSlotSelected;
        private GUIStyle _styleDropdown;
        private bool _stylesInitialized;

        // === 테마 컬러 ===
        private static readonly Color ColorBg = new Color(0.18f, 0.13f, 0.16f, 0.92f);
        private static readonly Color ColorTitleBar = new Color(0.12f, 0.09f, 0.11f, 1f);
        private static readonly Color ColorSlotBg = new Color(0.22f, 0.17f, 0.14f, 0.9f);
        private static readonly Color ColorSlotHover = new Color(0.35f, 0.25f, 0.18f, 0.9f);
        private static readonly Color ColorSlotSelected = new Color(0.40f, 0.28f, 0.20f, 1f);
        private static readonly Color ColorTextPrimary = new Color(0.92f, 0.88f, 0.80f, 1f);
        private static readonly Color ColorTextSecondary = new Color(0.70f, 0.65f, 0.60f, 1f);
        private static readonly Color ColorTextDim = new Color(0.50f, 0.45f, 0.40f, 1f);
        private static readonly Color ColorAccent = new Color(0.80f, 0.60f, 0.20f, 1f);
        private static readonly Color ColorBorder = new Color(0.12f, 0.09f, 0.11f, 1f);

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

            _styleTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ColorTextPrimary },
                padding = new RectOffset(12, 4, 0, 0)
            };

            _styleLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ColorTextSecondary },
                padding = new RectOffset(8, 4, 0, 0)
            };

            _styleButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(4, 4, 2, 2),
                normal = { textColor = ColorTextPrimary, background = MakeTexture(1, 1, ColorSlotHover) },
                hover = { textColor = ColorAccent, background = MakeTexture(1, 1, new Color(0.45f, 0.32f, 0.22f, 1f)) },
                active = { textColor = ColorTextPrimary, background = MakeTexture(1, 1, ColorSlotSelected) }
            };

            _styleSlot = new GUIStyle(GUI.skin.box)
            {
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
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ColorTextPrimary, background = MakeTexture(1, 1, ColorSlotBg) },
                padding = new RectOffset(8, 4, 4, 4)
            };

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
        // OnGUI — IMGUI 렌더링 (UIWindow.DrawWindowContent 오버라이드)
        // ===================================================================
        protected override void DrawWindowContent()
        {
            if (!IsOpen) return;
            InitStyles();

            if (WarehouseSystem.Instance == null)
            {
                GUILayout.Label("WarehouseSystem이 없습니다.");
                return;
            }

            // ===== 상단 영역: 영지 선택 드롭다운 + 헤더 =====
            DrawHeader();

            // ===== 아이템 슬롯 그리드 =====
            DrawItemGrid();

            // ===== 하단: 영지 간 이동 UI / 액션 버튼 =====
            DrawActionArea();
        }

        // ===================================================================
        // 상단: 영지 선택 드롭다운 + 헤더
        // ===================================================================
        private void DrawHeader()
        {
            GUILayout.BeginHorizontal();

            // 영지 선택 드롭다운
            GUILayout.Label("🏰 영지: ", _styleLabel, GUILayout.Width(80));

            if (GUILayout.Button(_territoryOptions != null && _selectedTerritoryIndex < _territoryOptions.Length
                ? _territoryOptions[_selectedTerritoryIndex] : _currentTerritoryId,
                _styleDropdown, GUILayout.Width(200), GUILayout.Height(28)))
            {
                _showTerritoryDropdown = !_showTerritoryDropdown;
            }

            GUILayout.FlexibleSpace();

            // 창고 용량 표시
            var items = WarehouseSystem.Instance.GetItems(_currentTerritoryId);
            int count = items != null ? items.Count : 0;
            GUILayout.Label($"📦 {count}/{MaxSlots}", _styleLabel, GUILayout.Width(100));

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
                        _styleButton, GUILayout.Height(24)))
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
            GUILayout.Box("", GUILayout.Height(2), GUILayout.ExpandWidth(true));
        }

        // ===================================================================
        // 아이템 슬롯 그리드 (4×5)
        // ===================================================================
        private void DrawItemGrid()
        {
            var items = WarehouseSystem.Instance.GetItems(_currentTerritoryId);
            int totalSlots = items != null ? items.Count : 0;

            if (totalSlots == 0)
            {
                GUILayout.Label("   창고가 비어 있습니다.", _styleLabel);
                return;
            }

            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(320));

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

            float iconSize = SlotSize * 0.55f;
            // GC 최적화: 캐시된 Rect 재사용
            _iconRect.x = rect.x + (rect.width - iconSize) / 2;
            _iconRect.y = rect.y + 3;
            _iconRect.width = iconSize;
            _iconRect.height = iconSize;
            DrawItemIcon(_iconRect, slot.item);

            // 아이템 이름
            _itemNameRect.x = rect.x + 2;
            _itemNameRect.y = rect.y + iconSize + 2;
            _itemNameRect.width = rect.width - 4;
            _itemNameRect.height = 14;
            GUI.Label(_itemNameRect, TruncateText(slot.item.displayName, rect.width - 4, _styleLabel), _styleLabel);

            // 수량
            if (slot.count > 1)
            {
                _countRect.x = rect.x + rect.width - 22;
                _countRect.y = rect.y + rect.height - 18;
                _countRect.width = 20;
                _countRect.height = 16;
                _countLabel = "x" + slot.count;
                GUI.Label(_countRect, _countLabel, _styleLabel);
            }

            // 인벤토리 이동 버튼 (▽)
            _transferRect.x = rect.x + rect.width - 18;
            _transferRect.y = rect.y + 2;
            _transferRect.width = 16;
            _transferRect.height = 16;
            if (GUI.Button(_transferRect, "▽", _styleButton))
            {
                WarehouseSystem.Instance.TransferToInventory(_currentTerritoryId, index, 1);
            }

            // 슬롯 클릭 → 선택
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
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
        }

        // ===================================================================
        // 하단: 인벤토리 ↔ 창고 전송 + 영지 간 이동
        // ===================================================================
        private void DrawActionArea()
        {
            GUILayout.Space(4);
            GUILayout.Box("", GUILayout.Height(1), GUILayout.ExpandWidth(true));
            GUILayout.Space(4);

            // === 인벤토리 → 창고 토글 버튼 ===
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_showInventoryTransfer ? "📦 창고 아이템 (현재)" : "🎒 인벤토리 → 창고",
                _styleButton, GUILayout.Width(200), GUILayout.Height(28)))
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
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("닫기", _styleButton, GUILayout.Width(120), GUILayout.Height(32)))
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
            _invScrollPos = GUILayout.BeginScrollView(_invScrollPos, GUILayout.Height(160));

            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null || slot.item == null || slot.count <= 0) continue;

                GUILayout.BeginHorizontal();

                string symbol = GetCategorySymbol(slot.item.category);
                Color color = GetCategoryColor(slot.item.category);
                var oldColor = GUI.color;
                GUI.color = color;
                GUILayout.Box("", GUILayout.Width(24), GUILayout.Height(24));
                GUI.color = oldColor;

                GUILayout.Label($"{symbol} {slot.item.displayName} x{slot.count}", _styleLabel, GUILayout.Width(240));

                if (GUILayout.Button("창고로", _styleButton, GUILayout.Width(80), GUILayout.Height(24)))
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
                            Debug.Log($"[WarehouseUI] {slot.item.displayName} x1 → 창고({_currentTerritoryId})");
                        }
                    }
                    break; // 슬롯 구조 변경 방지
                }

                // 수량 지정 전송
                GUILayout.Label("x", _styleLabel, GUILayout.Width(12));
                string countStr = GUILayout.TextField(_invTransferCount.ToString(), GUILayout.Width(36));
                int.TryParse(countStr, out _invTransferCount);
                _invTransferCount = Mathf.Clamp(_invTransferCount, 1, slot.count);

                if (GUILayout.Button("전송", _styleButton, GUILayout.Width(60), GUILayout.Height(24)))
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
                            Debug.Log($"[WarehouseUI] {slot.item.displayName} x{transferCount} → 창고({_currentTerritoryId})");
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
                        GUILayout.Label($"선택: {slot.item.displayName} (x{slot.count})", _styleLabel, GUILayout.Width(250));

                        // 수량 조절
                        if (GUILayout.Button("-", _styleButton, GUILayout.Width(24), GUILayout.Height(24)))
                        {
                            _transferCount = Mathf.Max(1, _transferCount - 1);
                        }
                        GUILayout.Label($"{_transferCount}", _styleLabel, GUILayout.Width(30));
                        if (GUILayout.Button("+", _styleButton, GUILayout.Width(24), GUILayout.Height(24)))
                        {
                            _transferCount = Mathf.Min(slot.count, _transferCount + 1);
                        }

                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        // 대상 영지 선택
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("→ 다른 영지로 보내기:", _styleLabel, GUILayout.Width(150));

                        if (_territoryOptions != null && _territoryOptions.Length > 1)
                        {
                            // 현재 영지 제외한 드롭다운
                            string[] targetOptions = GetOtherTerritoryOptions();
                            int targetIdx = System.Array.IndexOf(targetOptions, _targetTerritoryId);
                            if (targetIdx < 0) targetIdx = 0;

                            if (GUILayout.Button(targetOptions != null && targetIdx < targetOptions.Length
                                ? targetOptions[targetIdx] : "선택...", _styleDropdown, GUILayout.Width(180), GUILayout.Height(24)))
                            {
                                // 다음 대상으로 순환
                                CycleTargetTerritory();
                            }

                            if (GUILayout.Button("📦 보내기", _styleButton, GUILayout.Width(100), GUILayout.Height(28)))
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