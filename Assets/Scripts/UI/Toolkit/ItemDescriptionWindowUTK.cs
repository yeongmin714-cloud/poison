// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 108,114
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
        private const float WinW = 440f;
        private const float WinH = 700f;
        // 인벤(16+560) 오른쪽 바로 다음 — 예시 2 행 배치
        private const float PosX = 596f;
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
            UTKThreeColumnLayout.Place(this, 1);   // [P12] 중앙 1/3 (기존 고정 PosX/PosY 대체)
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

        /// <summary>[P12] 3분할 — 중앙 1/3 컬럼 정렬.</summary>
        public void PlaceCenterColumn() => UTKThreeColumnLayout.Place(this, 1);

        /// <summary>
        /// [P11 수리] 설명창 표시 — base.Show()/Hide() 경유로 전환.
        ///   기존 style.display 직접 조작은 UTKWindowManager 스택에 등록되지 않아
        ///   ESC(스택 최상단 Close)와 X 버튼(UTKWindowBase._isOpen 상태)이 모두 무효였다(실측).
        ///   base.Show()가 Register+IsOpen 동기 → ESC/X/I 3경로 전부 닫힘.
        /// </summary>
        /// <summary>
        /// [P20-1 근본 수리] 진입점 개명 Show→Open + 재귀 제거.
        ///   기존 `if (!i.IsOpen) ItemDescriptionWindowUTK.Show();`는 자기 자신 재귀 호출 —
        ///   IsOpen은 base.Show()가 실행돼야 true가 되는데 재귀가 그 전에 반복되어
        ///   스택 오버플로우로 설명창이 아예 안 떴다(테스트37 실측).
        /// </summary>
        public static void Open()
        {
            var i = Ensure();
            if (i == null) return;
            if (!i.IsOpen) i.Show();   // UTKWindowBase.Show(인스턴스) — 등록+표시
            i.CenterOnScreen();        // [P20-1] 요구: 정중앙 배치(아이콘+설명)
            i.BringToFront();
        }

        /// <summary>[P20-1] 화면 정중앙 배치 — 3분할 컬럼 대신 요구된 중앙 위치.</summary>
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
            if (_instance.IsOpen) _instance.Close();   // CS0176 회피 — instance Hide 경유
        }

        /// <summary>인벤 열림 여부와 쌍 — 인벤 닫힘 시 함께 숨김.</summary>
        public static void SyncWithInventory(bool inventoryOpen)
        {
            if (inventoryOpen) Open();
            else Hide();
        }

        /// <summary>아이템 표시 — 인벤 슬롯 클릭 시 호출.</summary>
        public static void ShowItem(PlayerInventory.ItemData item, int count)
        {
            var i = Ensure();
            if (i == null || item == null) return;

            var icon = ItemIconDatabase.GetOrCreateIcon(item);
            i._iconPreview.style.backgroundImage = UTKTextureSafe.ToBackground(icon);
            i._itemName.text = item.displayName ?? item.id;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"수량: {count}");
            sb.AppendLine($"카테고리: {item.category}");
            sb.AppendLine($"등급: {item.rarity}");
            if (item.maxDurability > 0) sb.AppendLine($"내구도: {item.maxDurability}");
            // C-O1-04: 세트 소속 아이템 — 세트명 + 완성 보너스 표시
            var setKind = EquipmentTierSet.GetSetForItem(item.id);
            if (setKind.HasValue)
            {
                var completion = EquipmentTierSet.GetActiveSetBonus(setKind.Value, 4);
                sb.AppendLine($"세트: {EquipmentTierSet.GetSetDisplayName(setKind.Value)} — {completion.description}");
            }
            i._itemMeta.text = sb.ToString();
            i._itemDesc.text = string.IsNullOrEmpty(item.description) ? "(설명 없음)" : item.description;

            if (!i.IsOpen) Open();   // [P20-1] 재귀 제거 — Open 경유
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
