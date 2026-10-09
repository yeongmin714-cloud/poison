using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// Figma 173:4 (288x432) general potion inventory/status panel.
    /// This window has a public Open() entry point only; no gameplay caller or hotkey is
    /// currently wired. It is deliberately separate from the I-key inventory and gas dose UI.
    /// Inventory exposes slot occupancy, not item weight, so the gauge is labeled accordingly.
    /// </summary>
    public sealed class PotionStatusUTK : UTKWindowBase
    {
        private const float PanelWidth = 288f;
        private const float PanelHeight = 432f;
        private static PotionStatusUTK _instance;

        private readonly VisualElement _itemList;
        private readonly Image _itemIcon;
        private readonly Label _itemName;
        private readonly Label _itemCount;
        private readonly Label _itemDescription;
        private readonly Label _occupancyPercent;
        private readonly Label _occupancyFraction;
        private readonly VisualElement _occupancyFill;
        private readonly Label _feedback;
        private readonly Button _useButton;
        private readonly Dictionary<string, PlayerInventory.ItemData> _potions = new Dictionary<string, PlayerInventory.ItemData>();
        private string _selectedId;

        private PotionStatusUTK() : base("잔여 물약", new Vector2(PanelWidth, PanelHeight), UTKWindowChrome.Frameless)
        {
            style.width = PanelWidth;
            style.height = PanelHeight;
            style.left = StyleKeyword.Auto;
            style.right = StyleKeyword.Auto;
            style.top = StyleKeyword.Auto;
            style.bottom = StyleKeyword.Auto;
            style.backgroundColor = new StyleColor(new Color(0.043f, 0.055f, 0.078f, 0.96f));
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 1f;
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = new StyleColor(UTKTheme.Stroke);
            style.borderTopLeftRadius = style.borderTopRightRadius = 14f;
            style.borderBottomLeftRadius = style.borderBottomRightRadius = 14f;
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;
            _content.style.paddingLeft = 19f;
            _content.style.paddingRight = 19f;
            _content.style.paddingTop = 19f;
            _content.style.paddingBottom = 19f;

            var header = new VisualElement { name = "PotionHeader" };
            header.style.height = 30f;
            header.style.flexShrink = 0f;
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            _content.Add(header);

            var flask = new Label("⚗");
            flask.style.width = 22f;
            flask.style.fontSize = 16.8f;
            flask.style.color = new StyleColor(UTKTheme.Accent);
            header.Add(flask);
            var title = MakeLabel("잔여 물약", 16.8f, UTKTheme.TextMain, true);
            title.style.flexGrow = 1f;
            header.Add(title);
            var badge = MakeLabel("IN BAG", 9.6f, UTKTheme.Success, true);
            badge.style.paddingLeft = 6f;
            badge.style.paddingRight = 6f;
            badge.style.paddingTop = 3f;
            badge.style.paddingBottom = 3f;
            badge.style.backgroundColor = new StyleColor(new Color(0.10f, 0.22f, 0.18f, 1f));
            badge.style.borderTopLeftRadius = badge.style.borderTopRightRadius = 5f;
            badge.style.borderBottomLeftRadius = badge.style.borderBottomRightRadius = 5f;
            header.Add(badge);

            var middle = new VisualElement { name = "PotionStatusContent" };
            middle.style.flexGrow = 1f;
            middle.style.minHeight = 0f;
            middle.style.marginTop = 10f;
            middle.style.marginBottom = 10f;
            middle.style.flexDirection = FlexDirection.Row;
            middle.style.alignItems = Align.Stretch;
            _content.Add(middle);

            var detail = new VisualElement { name = "PotionItemDetail" };
            detail.style.flexGrow = 1f;
            detail.style.minWidth = 0f;
            detail.style.marginRight = 14f;
            detail.style.paddingLeft = 10f;
            detail.style.paddingRight = 10f;
            detail.style.paddingTop = 10f;
            detail.style.paddingBottom = 10f;
            detail.style.flexDirection = FlexDirection.Column;
            detail.style.alignItems = Align.Center;
            detail.style.backgroundColor = new StyleColor(new Color(0.09f, 0.11f, 0.14f, 0.72f));
            detail.style.borderTopLeftRadius = detail.style.borderTopRightRadius = 8f;
            detail.style.borderBottomLeftRadius = detail.style.borderBottomRightRadius = 8f;
            middle.Add(detail);

            _itemIcon = new Image { name = "PotionItemIcon", scaleMode = ScaleMode.ScaleToFit };
            _itemIcon.style.width = 88f;
            _itemIcon.style.height = 88f;
            _itemIcon.style.marginTop = 4f;
            _itemIcon.style.marginBottom = 8f;
            detail.Add(_itemIcon);
            _itemName = MakeLabel("물약 없음", 14.4f, UTKTheme.TextMain, true);
            _itemName.style.whiteSpace = WhiteSpace.Normal;
            _itemName.style.unityTextAlign = TextAnchor.MiddleCenter;
            detail.Add(_itemName);
            _itemCount = MakeLabel("보유 수량 0", 12f, UTKTheme.Accent, true);
            _itemCount.style.marginTop = 5f;
            detail.Add(_itemCount);
            _itemDescription = MakeLabel("복용 가능한 물약을 선택하세요.", 12f, UTKTheme.TextSub, false);
            _itemDescription.style.whiteSpace = WhiteSpace.Normal;
            _itemDescription.style.unityTextAlign = TextAnchor.MiddleCenter;
            _itemDescription.style.marginTop = 5f;
            detail.Add(_itemDescription);

            _itemList = new VisualElement { name = "PotionInventoryList" };
            _itemList.style.flexGrow = 1f;
            _itemList.style.width = Length.Percent(100f);
            _itemList.style.minHeight = 0f;
            _itemList.style.marginTop = 8f;
            _itemList.style.flexDirection = FlexDirection.Column;
            detail.Add(_itemList);

            var gaugeColumn = new VisualElement { name = "InventorySlotOccupancy" };
            gaugeColumn.style.width = 48f;
            gaugeColumn.style.flexShrink = 0f;
            gaugeColumn.style.alignItems = Align.Center;
            gaugeColumn.style.justifyContent = Justify.Center;
            gaugeColumn.style.flexDirection = FlexDirection.Column;
            middle.Add(gaugeColumn);

            var track = new VisualElement { name = "SlotOccupancyTrack" };
            track.style.width = 14f;
            track.style.flexGrow = 1f;
            track.style.maxHeight = 235f;
            track.style.backgroundColor = new StyleColor(new Color(0.12f, 0.15f, 0.19f, 1f));
            track.style.borderTopLeftRadius = track.style.borderTopRightRadius = 7f;
            track.style.borderBottomLeftRadius = track.style.borderBottomRightRadius = 7f;
            track.style.justifyContent = Justify.FlexEnd;
            gaugeColumn.Add(track);
            _occupancyFill = new VisualElement { name = "SlotOccupancyFill" };
            _occupancyFill.style.width = 10f;
            _occupancyFill.style.alignSelf = Align.Center;
            _occupancyFill.style.backgroundColor = new StyleColor(UTKTheme.Accent);
            _occupancyFill.style.borderTopLeftRadius = _occupancyFill.style.borderTopRightRadius = 5f;
            _occupancyFill.style.borderBottomLeftRadius = _occupancyFill.style.borderBottomRightRadius = 5f;
            track.Add(_occupancyFill);
            _occupancyPercent = MakeLabel("0%", 13.2f, UTKTheme.Accent, true);
            _occupancyPercent.style.marginTop = 8f;
            gaugeColumn.Add(_occupancyPercent);
            _occupancyFraction = MakeLabel("0/0", 12f, UTKTheme.TextSub, false);
            gaugeColumn.Add(_occupancyFraction);
            var occupancyLabel = MakeLabel("슬롯 사용", 9.6f, UTKTheme.TextSub, false);
            occupancyLabel.style.marginTop = 3f;
            gaugeColumn.Add(occupancyLabel);

            _feedback = MakeLabel("", 12f, UTKTheme.TextSub, false);
            _feedback.style.height = 15f;
            _feedback.style.flexShrink = 0f;
            _feedback.style.unityTextAlign = TextAnchor.MiddleCenter;
            _content.Add(_feedback);

            var actions = new VisualElement { name = "PotionActions" };
            actions.style.height = 50f;
            actions.style.flexShrink = 0f;
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.alignItems = Align.Stretch;
            actions.style.justifyContent = Justify.SpaceBetween;
            _content.Add(actions);
            _useButton = MakeButton("복용", UTKTheme.Accent, OnUseClicked);
            _useButton.style.width = 120f;
            actions.Add(_useButton);
            var closeButton = MakeButton("닫기", UTKTheme.PanelSub, Close);
            closeButton.style.width = 120f;
            actions.Add(closeButton);

            ApplyUIToolkitFont(this);
            RefreshInventory();
        }

        /// <summary>Create and show this separate panel. No hotkey or existing gameplay route invokes it yet.</summary>
        public static void Open()
        {
            if (UIToolkitBootstrap.UIRoot == null)
            {
                Debug.LogWarning("[PotionStatusUTK] UIRoot unavailable; panel not opened.");
                return;
            }
            if (_instance == null)
                _instance = new PotionStatusUTK();
            if (_instance.parent == null)
                UIToolkitBootstrap.UIRoot.Add(_instance);
            _instance.RefreshInventory();
            _instance.Show();
        }

        public static void Ensure() { if (_instance == null) _instance = new PotionStatusUTK(); }

        protected override void OnWindowOpen() => RefreshInventory();

        private void RefreshInventory()
        {
            PlayerInventory inventory = PlayerInventory.Instance;
            PlayerInventory.ItemSlot[] slots = inventory != null ? inventory.GetAllSlots() : null;
            int capacity = slots != null ? slots.Length : 0;
            int occupied = 0;
            _potions.Clear();
            if (slots != null)
            {
                foreach (PlayerInventory.ItemSlot slot in slots)
                {
                    if (slot == null || slot.item == null || slot.count <= 0) continue;
                    occupied++;
                    if ((slot.item.category == PlayerInventory.ItemCategory.Potion ||
                         slot.item.category == PlayerInventory.ItemCategory.Drug) &&
                        !string.IsNullOrEmpty(slot.item.id) && !_potions.ContainsKey(slot.item.id))
                        _potions.Add(slot.item.id, slot.item);
                }
            }

            float ratio = capacity > 0 ? Mathf.Clamp01((float)occupied / capacity) : 0f;
            _occupancyFill.style.height = Length.Percent(ratio * 100f);
            _occupancyPercent.text = Mathf.RoundToInt(ratio * 100f) + "%";
            _occupancyFraction.text = occupied + "/" + capacity;

            if (string.IsNullOrEmpty(_selectedId) || !_potions.ContainsKey(_selectedId))
            {
                _selectedId = null;
                foreach (string id in _potions.Keys) { _selectedId = id; break; }
            }
            RebuildPotionList(inventory);
            UpdateSelectedItem(inventory);
        }

        private void RebuildPotionList(PlayerInventory inventory)
        {
            _itemList.Clear();
            if (_potions.Count == 0)
            {
                var empty = MakeLabel("인벤토리에 물약이 없습니다.", 12f, UTKTheme.TextSub, false);
                empty.style.whiteSpace = WhiteSpace.Normal;
                empty.style.unityTextAlign = TextAnchor.MiddleCenter;
                _itemList.Add(empty);
                return;
            }

            foreach (KeyValuePair<string, PlayerInventory.ItemData> entry in _potions)
            {
                string id = entry.Key;
                PlayerInventory.ItemData item = entry.Value;
                int count = inventory != null ? inventory.GetItemCount(id) : 0;
                var row = new Button(() =>
                {
                    _selectedId = id;
                    _feedback.text = "";
                    RefreshInventory();
                }) { name = "PotionRow_" + id, text = (item.displayName ?? id) + "   ×" + count };
                row.style.height = 28f;
                row.style.flexShrink = 0f;
                row.style.marginBottom = 3f;
                row.style.paddingLeft = 7f;
                row.style.paddingRight = 7f;
                row.style.backgroundColor = new StyleColor(id == _selectedId ? new Color(0.12f, 0.24f, 0.37f, 1f) : UTKTheme.PanelSub);
                row.style.color = new StyleColor(UTKTheme.TextMain);
                row.style.fontSize = 12f;
                row.style.unityTextAlign = TextAnchor.MiddleLeft;
                _itemList.Add(row);
            }
        }

        private void UpdateSelectedItem(PlayerInventory inventory)
        {
            PlayerInventory.ItemData item = null;
            bool hasItem = inventory != null && !string.IsNullOrEmpty(_selectedId) &&
                           _potions.TryGetValue(_selectedId, out item) && inventory.GetItemCount(_selectedId) > 0;
            if (!hasItem)
            {
                _itemIcon.image = null;
                _itemName.text = "물약 없음";
                _itemCount.text = "보유 수량 0";
                _itemDescription.text = "복용 가능한 물약을 선택하세요.";
                _useButton.SetEnabled(false);
                return;
            }

            _itemIcon.image = item.icon != null ? item.icon.texture : null;
            _itemName.text = string.IsNullOrEmpty(item.displayName) ? item.id : item.displayName;
            _itemCount.text = "보유 수량 " + inventory.GetItemCount(item.id);
            _itemDescription.text = string.IsNullOrEmpty(item.description) ? "선택한 물약을 복용합니다." : item.description;
            _useButton.SetEnabled(true);
        }

        private void OnUseClicked()
        {
            PlayerInventory inventory = PlayerInventory.Instance;
            if (inventory == null || string.IsNullOrEmpty(_selectedId) || !_potions.TryGetValue(_selectedId, out PlayerInventory.ItemData item))
                return;
            if (inventory.GetItemCount(item.id) <= 0)
            {
                RefreshInventory();
                return;
            }

            // Consume first so an unavailable item can never grant a free effect.
            if (!inventory.RemoveItem(item.id, 1))
            {
                _feedback.text = "인벤토리에서 물약을 제거할 수 없습니다.";
                _feedback.style.color = new StyleColor(UTKTheme.Warn);
                RefreshInventory();
                return;
            }

            Transform player = PlayerHealth.Instance != null ? PlayerHealth.Instance.transform : null;
            if (!PotionUseSystem.Use(item, player, out string effectText))
            {
                if (inventory.AddItem(item, 1))
                {
                    _feedback.text = "복용 효과를 적용할 수 없어 물약을 반환했습니다.";
                }
                else
                {
                    _feedback.text = "복용 실패 · 물약 반환도 실패했습니다.";
                    Debug.LogError($"[PotionStatusUTK] Potion use failed and refund failed for item '{item.id}'.");
                }
                _feedback.style.color = new StyleColor(UTKTheme.Warn);
                RefreshInventory();
                return;
            }

            _feedback.text = string.IsNullOrEmpty(effectText) ? "복용했습니다." : effectText;
            _feedback.style.color = new StyleColor(UTKTheme.Success);
            RefreshInventory();
        }

        private static Label MakeLabel(string text, float size, Color color, bool bold)
        {
            var label = new Label(text);
            label.style.fontSize = size;
            label.style.color = new StyleColor(color);
            label.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
            return label;
        }

        private static Button MakeButton(string text, Color color, System.Action callback)
        {
            var button = new Button(callback) { text = text };
            button.style.height = 50f;
            button.style.backgroundColor = new StyleColor(color);
            button.style.color = new StyleColor(UTKTheme.TextMain);
            button.style.fontSize = 12f;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.borderTopLeftRadius = button.style.borderTopRightRadius = 7f;
            button.style.borderBottomLeftRadius = button.style.borderBottomRightRadius = 7f;
            return button;
        }
    }
}
