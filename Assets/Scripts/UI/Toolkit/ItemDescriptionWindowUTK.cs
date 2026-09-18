using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;   // PlayerInventory.ItemData

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U8 — 아이템 설명창 (독립 창, 3분할 중앙).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    ///
    /// 요구사항(2026-09-18): I키 → 인벤창(좌)+설명창(중앙) 쌍 표시 — 각각 독립 창.
    /// 우측 슬롯(창고/전리품)은 컨텍스트 상호작용 시에만 별도 창으로 표시.
    /// 인벤 슬롯 클릭 → ShowItem(item, count)로 갱신.
    /// [DescUTK] 실측 로그.
    /// </summary>
    public class ItemDescriptionWindowUTK : UTKWindowBase
    {
        private const float WinW = 420f;
        private const float WinH = 680f;
        // 인벤(16+520) 오른쪽 바로 다음 — 예시 2 행 배치
        private const float PosX = 544f;
        private const float PosY = 96f;

        private static ItemDescriptionWindowUTK _instance;
        public static ItemDescriptionWindowUTK Instance => _instance;

        private readonly Label _itemName;
        private readonly Label _itemMeta;
        private readonly Label _itemDesc;
        private readonly VisualElement _iconPreview;   // [U8 요구] 아이템 아이콘 프리뷰
        private Texture2D _currentIcon;

        private ItemDescriptionWindowUTK() : base("아이템 설명", new Vector2(WinW, WinH))
        {
            // [U8 요구] 아이템 아이콘 프리뷰 — 예시 2의 대형 이미지 영역
            _iconPreview = new VisualElement();
            _iconPreview.style.width = 140f;
            _iconPreview.style.height = 140f;
            _iconPreview.style.backgroundColor = new StyleColor(new Color(0.03f, 0.08f, 0.16f, 0.6f));
            _iconPreview.style.borderTopWidth = 1; _iconPreview.style.borderBottomWidth = 1;
            _iconPreview.style.borderLeftWidth = 1; _iconPreview.style.borderRightWidth = 1;
            _iconPreview.style.borderTopColor = new StyleColor(UTKColor.IronLine);
            _iconPreview.style.borderBottomColor = new StyleColor(UTKColor.IronLine);
            _iconPreview.style.borderLeftColor = new StyleColor(UTKColor.IronLine);
            _iconPreview.style.borderRightColor = new StyleColor(UTKColor.IronLine);
            _iconPreview.style.marginBottom = 10f;
            _content.Add(_iconPreview);

            _itemName = new Label("아이템을 선택하세요");
            _itemName.style.fontSize = 24f;
            _itemName.style.color = new StyleColor(UTKColor.AccentRare);
            _itemName.style.unityFontStyleAndWeight = FontStyle.Bold;
            _itemName.style.whiteSpace = WhiteSpace.Normal;
            _content.Add(_itemName);

            _itemMeta = new Label("");
            _itemMeta.style.fontSize = 17f;
            _itemMeta.style.color = new StyleColor(UTKColor.TextSecondary);
            _itemMeta.style.whiteSpace = WhiteSpace.Normal;
            _itemMeta.style.marginTop = 6f;
            _content.Add(_itemMeta);

            _itemDesc = new Label("");
            _itemDesc.style.fontSize = 17f;
            _itemDesc.style.color = new StyleColor(UTKColor.TextPrimary);
            _itemDesc.style.whiteSpace = WhiteSpace.Normal;
            _itemDesc.style.marginTop = 10f;
            _itemDesc.style.flexGrow = 1f;
            _content.Add(_itemDesc);

            ApplyUIToolkitFont(this);

            style.display = DisplayStyle.None;
            style.left = PosX;
            style.top = PosY;
        }

        /// <summary>UTK 루트 부착(멱등) — 인벤과 쌍으로 사용.</summary>
        public static ItemDescriptionWindowUTK Ensure()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return null;
            if (_instance != null && _instance.panel != null) return _instance;
            if (_instance != null) _instance.RemoveFromHierarchy();
            _instance = new ItemDescriptionWindowUTK();
            root.Add(_instance);
            Debug.Log("[DescUTK] 설명창 인스턴스 생성");
            return _instance;
        }

        /// <summary>설명창 표시 (인벤 열림 쌍).</summary>
        public static void Show()
        {
            var i = Ensure();
            if (i == null) return;
            i.style.display = DisplayStyle.Flex;
            i.BringToFront();
        }

        public static void Hide()
        {
            if (_instance == null) return;
            _instance.style.display = DisplayStyle.None;
        }

        /// <summary>인벤 열림 여부와 쌍 — 인벤 닫힘 시 함께 숨김.</summary>
        public static void SyncWithInventory(bool inventoryOpen)
        {
            if (inventoryOpen) Show();
            else Hide();
        }

        /// <summary>아이템 표시 — 인벤 슬롯 클릭 시 호출.</summary>
        public static void ShowItem(PlayerInventory.ItemData item, int count)
        {
            var i = Ensure();
            if (i == null || item == null) return;

            var icon = ItemIconDatabase.GetOrCreateIcon(item);
            i._iconPreview.style.backgroundImage = icon != null
                ? new StyleBackground(Background.FromTexture2D(icon))
                : i._iconPreview.style.backgroundImage;
            i._itemName.text = item.displayName ?? item.id;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"수량: {count}");
            sb.AppendLine($"카테고리: {item.category}");
            sb.AppendLine($"등급: {item.rarity}");
            if (item.maxDurability > 0) sb.AppendLine($"내구도: {item.maxDurability}");
            i._itemMeta.text = sb.ToString();
            i._itemDesc.text = string.IsNullOrEmpty(item.description) ? "(설명 없음)" : item.description;

            if (i.style.display == DisplayStyle.None) Show();
            Debug.Log($"[DescUTK] 설명 갱신 — {item.displayName ?? item.id} x{count}");
        }

        /// <summary>선택 해제.</summary>
        public static void Clear()
        {
            if (_instance == null) return;
            _instance._itemName.text = "아이템을 선택하세요";
            _instance._itemMeta.text = "";
            _instance._itemDesc.text = "";
        }
    }
}
