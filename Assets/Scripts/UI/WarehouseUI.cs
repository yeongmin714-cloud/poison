using UnityEngine;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.Core.Data;
using ProjectName.UI.Themes;

namespace ProjectName.UI
{
    /// <summary>
    /// 영지 창고 UI — IMGUI 기반 4×5 그리드.
    /// UIManager를 통해 열기/닫기.
    /// </summary>
    public class WarehouseUI : UIWindow
    {
        private string _currentTerritoryId = "default";
        private Vector2 _scrollPos;
        private const int SlotsPerRow = 4;
        private const int MaxSlots = 20;
        private const float SlotSize = 60f;
        private const float Padding = 5f;

        // === GC 최적화: 캐시된 필드 ===
        private string _cachedHeader;
        private string _lastHeaderTerritory;
        private int _lastHeaderCount = -1;
        private Rect _itemNameRect = new Rect(0, 0, 0, 18);
        private Rect _countRect = new Rect(0, 0, 18, 18);
        private Rect _transferRect = new Rect(0, 0, 16, 16);
        private Rect _iconRect = new Rect(0, 0, 0, 0);
        private string _countLabel;

        protected override void Awake()
        {
            base.Awake();
            ApplyTheme(Phase33_Themes.CreateWarehouseTheme());
        }

        public void SetTerritory(string territoryId)
        {
            _currentTerritoryId = territoryId ?? "default";
        }

        public override void Show()
        {
            base.Show();
            _scrollPos = Vector2.zero;
        }

        protected override void DrawWindowContent()
        {
            if (WarehouseSystem.Instance == null)
            {
                GUILayout.Label("WarehouseSystem이 없습니다.");
                return;
            }

            var items = WarehouseSystem.Instance.GetItems(_currentTerritoryId);
            int count = items != null ? items.Count : 0;

            // GC 최적화: 헤더 캐싱
            if (_cachedHeader == null || _lastHeaderTerritory != _currentTerritoryId || _lastHeaderCount != count)
            {
                _cachedHeader = $"📦 창고 ({_currentTerritoryId}) — {count}/{MaxSlots}";
                _lastHeaderTerritory = _currentTerritoryId;
                _lastHeaderCount = count;
            }
            GUILayout.Label(_cachedHeader, GUI.skin.box);

            if (items == null || items.Count == 0)
            {
                GUILayout.Label("   창고가 비어 있습니다.");
                return;
            }

            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(300));

            int rows = Mathf.CeilToInt((float)items.Count / SlotsPerRow);
            for (int r = 0; r < rows; r++)
            {
                GUILayout.BeginHorizontal();
                for (int c = 0; c < SlotsPerRow; c++)
                {
                    int idx = r * SlotsPerRow + c;
                    if (idx < items.Count)
                    {
                        var slot = items[idx];
                        if (slot != null && slot.item != null && slot.count > 0)
                        {
                            DrawSlot(idx, slot);
                        }
                        else
                        {
                            GUILayout.Box("", GUILayout.Width(SlotSize), GUILayout.Height(SlotSize));
                        }
                    }
                    else
                    {
                        GUILayout.Box("", GUILayout.Width(SlotSize), GUILayout.Height(SlotSize));
                    }
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
        }

        private void DrawSlot(int index, PlayerInventory.ItemSlot slot)
        {
            var rect = GUILayoutUtility.GetRect(SlotSize, SlotSize);
            GUI.Box(rect, "");

            float iconSize = SlotSize * 0.6f;
            // GC 최적화: 캐시된 Rect 재사용
            _iconRect.x = rect.x + (rect.width - iconSize) / 2;
            _iconRect.y = rect.y + 4;
            _iconRect.width = iconSize;
            _iconRect.height = iconSize;
            DrawItemIcon(_iconRect, slot.item);

            // 아이템 이름 (캐시된 Rect 재사용)
            _itemNameRect.x = rect.x + 2;
            _itemNameRect.y = rect.y + iconSize + 2;
            _itemNameRect.width = rect.width - 4;
            GUI.Label(_itemNameRect, slot.item.displayName);

            // 수량 (캐시된 Rect + 캐시된 문자열)
            if (slot.count > 1)
            {
                _countRect.x = rect.x + rect.width - 20;
                _countRect.y = rect.y + rect.height - 18;
                _countLabel = "x" + slot.count;
                GUI.Label(_countRect, _countLabel);
            }

            // 인벤토리 이동 버튼 (캐시된 Rect 재사용)
            _transferRect.x = rect.x + rect.width - 18;
            _transferRect.y = rect.y + 2;
            if (GUI.Button(_transferRect, "▽"))
            {
                WarehouseSystem.Instance.TransferToInventory(_currentTerritoryId, index, 1);
            }
        }

        private void DrawItemIcon(Rect rect, PlayerInventory.ItemData item)
        {
            if (item == null) return;

            // 카테고리별 색상 배경
            Color color = GetCategoryColor(item.category);
            // Store current GUI color
            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.Box(rect, "");
            GUI.color = oldColor;

            // 카테고리별 심볼
            string symbol = GetCategorySymbol(item.category);
            GUI.Label(rect, symbol);
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
    }
}