// F4 (2026-09-24): 아이템 상세창 — Figma DetailPanel 표준(480×608) 재생 + GitHub-dark.
// 구조: PanelHeader('상세 정보'/'SPECIFICATIONS') + ItemNameSection(이름+Tier배지) +
//       ItemImageSection(등급 스트립 + 320px 아이콘) + StatsGrid(수량/카테고리/내구도/세트) + DescriptionSection.
// 기능(ShowItem/Clear/Open/Hide/SyncWithInventory/CenterOnScreen) 기존 동작 유지.
#pragma warning disable 108,114
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;   // PlayerInventory.ItemData, EquipmentRarityData

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit — 아이템 상세창 (F4 Figma DetailPanel 표준 재생).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    ///
    /// 요구사항(2026-09-18): I키 → 인벤창(좌)+설명창(중앙) 쌍 표시.
    /// 인벤 슬롯 클릭 → ShowItem(item, count)로 갱신.
    ///
    /// [F4 DetailPanel 표준] Figma DetailPanel(480×608):
    ///   PanelHeader('상세 정보'/'SPECIFICATIONS') + ItemNameSection(ItemName/TierBadge) +
    ///   ItemImageSection(TierStrip 4px + 클래스 텍스트 + ItemImage 320px) +
    ///   StatsGrid(등급/수량/카테고리/내구도/세트 — SpecBox 2~3칸) + DescriptionSection.
    /// GitHub-dark 팔레트(이 창 한정 인라인). 기능 경로 무수정.
    /// </summary>
    public class ItemDescriptionWindowUTK : UTKWindowBase
    {
        private const float WinW = 480f;
        private const float WinH = 608f;

        private static ItemDescriptionWindowUTK _instance;
        public static ItemDescriptionWindowUTK Instance => _instance;

        // ===== GitHub-dark 팔레트 (이 창 한정) =====
        private static class Dark
        {
            public static readonly Color Bg       = Hex(0x0B0E14);
            public static readonly Color Panel    = Hex(0x161B22);
            public static readonly Color Sub      = Hex(0x21262D);
            public static readonly Color Inset    = Hex(0x1C2128);
            public static readonly Color Accent   = Hex(0x58A6FF);
            public static readonly Color Gold     = Hex(0xE3B341);
            public static readonly Color TextMain = Hex(0xF0F6FC);
            public static readonly Color TextSub  = Hex(0x8B949E);
            public static readonly Color Stroke   = Hex(0x2E343D);

            private static Color Hex(uint rgb) =>
                new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);
        }

        private static Color Hex(uint rgb) =>
            new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);

        // ===== 노드 =====
        private readonly Label _itemName;        // ItemNameSection — 아이템명
        private readonly Label _tierClass;       // TierBadge — "전설 CLASS"
        private readonly VisualElement _tierStrip;   // ItemImageSection 상단 4px 등급 스트립
        private readonly VisualElement _iconPreview; // ItemImage 320px
        private readonly Label _spec1Label; private readonly Label _spec1Val;
        private readonly Label _spec2Label; private readonly Label _spec2Val;
        private readonly Label _spec3Label; private readonly Label _spec3Val;
        private readonly Label _itemDesc;        // DescriptionSection 본문

        private ItemDescriptionWindowUTK() : base("상세 정보", new Vector2(WinW, WinH))
        {

            // ── PanelHeader: subtitle 'SPECIFICATIONS' — 타이틀(창 크롬)은 이미 '상세 정보'  ──
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 10f;
            Content.Add(header);
            var subtitle = new Label("SPECIFICATIONS");
            subtitle.style.fontSize = 13f;
            subtitle.style.color = Dark.TextSub;
            subtitle.style.flexGrow = 1f;
            header.Add(subtitle);

            // ── ItemNameSection: 이름 + Tier 배지 ──
            var nameRow = new VisualElement();
            nameRow.style.flexDirection = FlexDirection.Row;
            nameRow.style.alignItems = Align.Center;
            nameRow.style.marginBottom = 8f;
            Content.Add(nameRow);

            _itemName = new Label("아이템을 선택하세요");
            _itemName.style.fontSize = 24f;
            _itemName.style.color = Dark.TextMain;
            _itemName.style.unityFontStyleAndWeight = FontStyle.Bold;
            _itemName.style.whiteSpace = WhiteSpace.Normal;
            _itemName.style.flexGrow = 1f;
            nameRow.Add(_itemName);

            _tierClass = new Label("");
            _tierClass.style.fontSize = 13f;
            _tierClass.style.color = Dark.Gold;
            _tierClass.style.backgroundColor = Dark.Sub;
            _tierClass.style.borderTopLeftRadius = 4f; _tierClass.style.borderTopRightRadius = 4f;
            _tierClass.style.borderBottomLeftRadius = 4f; _tierClass.style.borderBottomRightRadius = 4f;
            _tierClass.style.paddingTop = 3f; _tierClass.style.paddingBottom = 3f;
            _tierClass.style.paddingLeft = 10f; _tierClass.style.paddingRight = 10f;
            _tierClass.style.marginLeft = 8f;
            nameRow.Add(_tierClass);

            // ── ItemImageSection: TierStrip + 이미지 320px (세로) ──
            var imageSec = new VisualElement();
            imageSec.style.marginBottom = 10f;
            Content.Add(imageSec);

            _tierStrip = new VisualElement();
            _tierStrip.style.height = 4f;
            _tierStrip.style.marginBottom = 4f;
            imageSec.Add(_tierStrip);

            _iconPreview = new VisualElement();
            _iconPreview.style.height = 220f;          // 창 비율상 세로 축소 (608 창에서 320은 과대)
            _iconPreview.style.backgroundColor = Dark.Inset;
            _iconPreview.style.borderTopWidth = 1; _iconPreview.style.borderBottomWidth = 1;
            _iconPreview.style.borderLeftWidth = 1; _iconPreview.style.borderRightWidth = 1;
            _iconPreview.style.borderTopColor = _iconPreview.style.borderBottomColor =
            _iconPreview.style.borderLeftColor = _iconPreview.style.borderRightColor = new StyleColor(Dark.Stroke);
            _iconPreview.style.borderTopLeftRadius = 6f; _iconPreview.style.borderTopRightRadius = 6f;
            _iconPreview.style.borderBottomLeftRadius = 6f; _iconPreview.style.borderBottomRightRadius = 6f;
            _iconPreview.style.alignSelf = Align.Center;
            imageSec.Add(_iconPreview);

            // ── StatsGrid: SpecBox 3칸 (등급/수량/기타) ──
            var specs = new VisualElement();
            specs.style.flexDirection = FlexDirection.Row;
            specs.style.marginBottom = 10f;
            Content.Add(specs);

            BuildSpecBox(specs, "등급", out _spec1Label, out _spec1Val);
            BuildSpecBox(specs, "수량", out _spec2Label, out _spec2Val);
            BuildSpecBox(specs, "카테고리", out _spec3Label, out _spec3Val);

            // ── DescriptionSection ──
            var descSec = new VisualElement();
            descSec.style.flexGrow = 1f;
            descSec.style.backgroundColor = Dark.Inset;
            descSec.style.borderTopLeftRadius = 6f; descSec.style.borderTopRightRadius = 6f;
            descSec.style.borderBottomLeftRadius = 6f; descSec.style.borderBottomRightRadius = 6f;
            descSec.style.paddingTop = 10f; descSec.style.paddingBottom = 10f;
            descSec.style.paddingLeft = 12f; descSec.style.paddingRight = 12f;
            Content.Add(descSec);

            var descHead = new Label("아이템 설명");
            descHead.style.fontSize = 14f;
            descHead.style.color = Dark.Accent;
            descHead.style.marginBottom = 4f;
            descSec.Add(descHead);

            _itemDesc = new Label("");
            _itemDesc.style.fontSize = 14f;
            _itemDesc.style.color = Dark.TextMain;
            _itemDesc.style.whiteSpace = WhiteSpace.Normal;
            descSec.Add(_itemDesc);

            ApplyF4GitHubDarkStyle();   // (창 크롬 GitHub-dark)
            ApplyUIToolkitFont(this);

            style.display = DisplayStyle.None;
            UTKThreeColumnLayout.Place(this, 1);
        }

        private void BuildSpecBox(VisualElement parent, string label, out Label lbl, out Label val)
        {
            var box = new VisualElement();
            box.style.flexGrow = 1f;
            box.style.marginRight = 4f;
            box.style.backgroundColor = Dark.Sub;
            box.style.borderTopLeftRadius = 6f; box.style.borderTopRightRadius = 6f;
            box.style.borderBottomLeftRadius = 6f; box.style.borderBottomRightRadius = 6f;
            box.style.paddingTop = 6f; box.style.paddingBottom = 6f;
            parent.Add(box);

            lbl = new Label(label);
            lbl.style.fontSize = 12f;
            lbl.style.color = Dark.TextSub;
            lbl.style.unityTextAlign = TextAnchor.MiddleCenter;
            box.Add(lbl);

            val = new Label("—");
            val.style.fontSize = 14f;
            val.style.color = Dark.TextMain;
            val.style.unityTextAlign = TextAnchor.MiddleCenter;
            box.Add(val);
        }

        // ===== F4 GitHub-dark 창 크롬 =====
        private void ApplyF4GitHubDarkStyle()
        {
            style.backgroundColor = Dark.Panel;
            style.backgroundImage = new StyleBackground(StyleKeyword.None);
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 1f;
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = new StyleColor(Dark.Stroke);
            style.borderTopLeftRadius = 8f; style.borderTopRightRadius = 8f;
            style.borderBottomLeftRadius = 8f; style.borderBottomRightRadius = 8f;
            style.color = Dark.TextMain;
            style.minWidth = WinW; style.minHeight = WinH;

            var titleBar = this.Q("TitleBar");
            if (titleBar != null)
            {
                titleBar.style.backgroundColor = Dark.Sub;
                titleBar.style.borderTopLeftRadius = 8f; titleBar.style.borderTopRightRadius = 8f;
                titleBar.style.borderBottomWidth = 1f;
                titleBar.style.borderBottomColor = Dark.Stroke;
            }
            if (_titleLabel != null) _titleLabel.style.color = Dark.TextMain;

            var closeBtn = this.Q<Button>("CloseButton");
            if (closeBtn != null)
            {
                closeBtn.style.backgroundColor = Dark.Sub;
                closeBtn.style.borderTopLeftRadius = 4f; closeBtn.style.borderTopRightRadius = 4f;
                closeBtn.style.borderBottomLeftRadius = 4f; closeBtn.style.borderBottomRightRadius = 4f;
                closeBtn.style.color = Dark.TextMain;
            }
        }

        // ===== 공개 API (기존 동작 유지) =====

        public static ItemDescriptionWindowUTK Ensure()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return null;
            if (_instance != null && _instance.panel != null) return _instance;
            if (_instance != null) _instance.RemoveFromHierarchy();
            _instance = new ItemDescriptionWindowUTK();
            root.Add(_instance);
            Debug.Log("[DescUTK] 설명창 인스턴스 생성 (F4 DetailPanel)");
            return _instance;
        }

        public void PlaceCenterColumn() => UTKThreeColumnLayout.Place(this, 1);

        public static void Open()
        {
            var i = Ensure();
            if (i == null) return;
            if (!i.IsOpen) i.Show();
            i.CenterOnScreen();
            i.BringToFront();
        }

        public void CenterOnScreen()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;
            float pw = root.resolvedStyle.width, ph = root.resolvedStyle.height;
            if (pw <= 0f || ph <= 0f) { pw = 1920f; ph = 1080f; }
            float w = style.width.value.value, h = style.height.value.value;
            style.left = Mathf.Max(0f, (pw - w) * 0.5f);
            style.top = Mathf.Max(0f, (ph - h) * 0.5f);
        }

        public static void Hide()
        {
            if (_instance == null) return;
            if (_instance.IsOpen) _instance.Close();
        }

        public static void SyncWithInventory(bool inventoryOpen)
        {
            if (inventoryOpen) Open();
            else Hide();
        }

        /// <summary>아이템 표시 — 인벤 슬롯 클릭 시 호출. F4: 등급/수량/카테고리/내구도/세트를 StatsGrid·TierBadge·설명으로.</summary>
        public static void ShowItem(PlayerInventory.ItemData item, int count)
        {
            var i = Ensure();
            if (i == null || item == null) return;

            var icon = ItemIconDatabase.GetOrCreateIcon(item);
            i._iconPreview.style.backgroundImage = UTKTextureSafe.ToBackground(icon);
            i._itemName.text = item.displayName ?? item.id;

            // 등급 색 + Tier 배지 + 스트립
            Color rc = EquipmentRarityData.GetRarityColor(item.rarity);
            string rarityName = EquipmentRarityData.GetRarityDisplayName(item.rarity);
            i._itemName.style.color = rc == Dark.TextMain ? Dark.TextMain : rc;
            i._tierClass.text = string.IsNullOrEmpty(rarityName) ? "" : $"{rarityName} CLASS";
            i._tierClass.style.color = rc;
            i._tierStrip.style.backgroundColor = rc;

            // StatsGrid — 3칸: 등급 / 수량 / 카테고리
            i._spec1Val.text = string.IsNullOrEmpty(rarityName) ? item.rarity.ToString() : rarityName;
            i._spec1Val.style.color = rc;
            i._spec2Val.text = count.ToString();
            i._spec3Val.text = item.category.ToString();

            // 세트/내구도는 설명 하단에 병기 (C-O1-04 세트 보너스 보존)
            var sb = new System.Text.StringBuilder();
            if (item.maxDurability > 0) sb.AppendLine($"내구도: {item.maxDurability}");
            var setKind = EquipmentTierSet.GetSetForItem(item.id);
            if (setKind.HasValue)
            {
                var completion = EquipmentTierSet.GetActiveSetBonus(setKind.Value, 4);
                sb.AppendLine($"세트: {EquipmentTierSet.GetSetDisplayName(setKind.Value)} — {completion.description}");
            }
            string extra = sb.ToString();
            i._itemDesc.text = (item.description ?? "") + (extra.Length > 0 ? "\n" + extra : "");

            if (!i.IsOpen) Open();
            Debug.Log($"[DescUTK] 설명 갱신 — {item.displayName ?? item.id} x{count}");
        }

        public static void Clear()
        {
            if (_instance == null) return;
            _instance._itemName.text = "아이템을 선택하세요";
            _instance._itemName.style.color = Dark.TextMain;
            _instance._tierClass.text = "";
            _instance._spec1Val.text = "—";
            _instance._spec2Val.text = "—";
            _instance._spec3Val.text = "—";
            _instance._itemDesc.text = "";
        }
    }
}
