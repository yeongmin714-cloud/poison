using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using ProjectName.UI;                  // ItemIconDatabase, UIFont
using UnityEngine.UIElements;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U1 — 상점 윈도우 (ShopWindow 769줄 IMGUI → UTK 포팅).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    ///
    /// 원본 프로젝트명: ProjectName.UI.ShopWindow (IMGUI, OnGUI). 절대 수정 금지.
    /// 본 포팅은 동일 게임 데이터 소스를 직접 호출한다:
    ///   - 구매 가격: PlayerStats.BuyDiscount (화술 할인, 최대 20% 클램프)
    ///   - 판매 가격: EconomyPricing.GetSellPrice (등급표 기준가 × 40% 스프레드 — Phase O2, 화술 프리미엄 폐지)
    ///   - 골드: PlayerStats.Gold / SpendGold / AddGold (source 태그 부착 — EconomyAuditSystem 원장)
    ///   - 인벤토리: PlayerInventory.Instance (AddItem/RemoveItem/GetAllSlots)
    ///   - 아이콘: ItemIconDatabase.GetOrCreateIcon
    ///
    /// 기능: 구매/판매 2탭 + 스크롤 행 리스트 + 골드 잔액 표시.
    /// 원본의 "가격(할인가 병기 원가)" 표기 관례를 유지한다.
    /// OnGUI 미사용 — 순수 UI Toolkit VisualElement 트리.
    /// </summary>
    public class ShopWindowUTK : UTKWindowBase
    {
        // Buy/Sell 2-way 탭 상태
        private enum ShopTab { Buy, Sell }

        // [Figma 정합] Store 카테고리 필터 탭 — 6탭(전체/무기/방어구/소모품/재료/레시피)
        private enum StoreCat { All, Weapon, Armor, Consumable, Material, Recipe }

        public static readonly Rect StoreBounds = new Rect(144f, 84f, 504f, 912f);
        public static readonly Rect DetailBounds = new Rect(672f, 84f, 576f, 912f);
        public static readonly Rect InventoryBounds = new Rect(1272f, 84f, 504f, 912f);
        public const float StoreGridCellSize = 81.6f;
        public const int StoreTabCount = 6;
        public const string ShopVariantDifference = "Action label only: 74:4 shows Buy; 76:7 shows Sell. The saved trees share the same store tabs, grid, details, inventory, and geometry; their sample content is not a faction mapping.";

        // ===== 상점 아이템 데이터 (원본 ShopWindow.ShopItem 구조 대응) =====
        [System.Serializable]
        public class ShopItem
        {
            public PlayerInventory.ItemData item; // 판매할 아이템
            public int price;                     // 가격 (골드)
            public int stock;                     // 재고 (-1 무한, 0 품절, 양수 남은 재고)
            public bool isRare;                   // 희귀 아이템 여부
        }

        // ===== C9-27: 씨앗 랜덤 재고 (원본과 동일 확률 상수) =====
        private const float SEED_STOCK_CHANCE = 0.65f;
        private const float SEED_SILVER_CHANCE = 0.35f;

        private static readonly (PlayerInventory.ItemData item, bool rare)[] CommonSeeds =
        {
            (PlayerInventory.Seed_Red,    false),
            (PlayerInventory.Seed_Purple, true),
            (PlayerInventory.Seed_Yellow, false),
            (PlayerInventory.Seed_Green,  false),
        };

        private static Color Hex(uint rgb) =>
            new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);

        // ===== [Figma 74:4 전수 실측 진실치 (2026-10-29 nodes API) — 폰트 제외 전 요소] =====
        private static readonly Color ShopPanelBg = new Color(0x16 / 255f, 0x1B / 255f, 0x22 / 255f, 0.7f);
        // [144차 P1-SHOP] 스트로크 알파 수리 — shop.md "알파 재검증 정정" 재축출 진실치: 모든 #30363D 스트로크가
        //   #30363D/1.0@0.502(실효 0.502) — 구 1.0(불투명)은 알파 유실 진실치로 PASS 오판정(행 #7/#9/#12/#16/#20/#27/#31/#36/#38/#40/#55/#57).
        //   Phase 0 V2 공용 크롬 상수(UTKTheme.BorderChrome = #30363D@0.502)를 재사용 — 이중 상수 방지(규약).
        private static readonly Color ShopStroke = UTKTheme.BorderChrome;
        private static readonly Color ShopAccent = new Color(0x58 / 255f, 0xA6 / 255f, 0xFF / 255f, 1f);
        private static readonly Color ShopGold = new Color(0xE3 / 255f, 0xB3 / 255f, 0x41 / 255f, 1f);
        private static readonly Color ShopPurple = new Color(0xBC / 255f, 0x8C / 255f, 0xFF / 255f, 1f);
        private static readonly Color ShopBlue = new Color(0x1F / 255f, 0x6F / 255f, 0xEB / 255f, 1f);
        private static readonly Color ShopSlotBg = new Color(0x21 / 255f, 0x26 / 255f, 0x2D / 255f, 0.6f);
        private static readonly Color ShopSlotHoverBg = new Color(0x30 / 255f, 0x36 / 255f, 0x3D / 255f, 0.8f);
        private static readonly Color ShopPriceTagBg = new Color(0x1F / 255f, 0x24 / 255f, 0x2C / 255f, 1f);

        /// <summary>[Figma] 패널 3종 공통 — #161B22/0.7 + stroke #30363D/1.2 + r14.4 + 코너 디칼 4개.
        /// [144차 P1-SHOP 기록] Figma StorePanel fx = BACKGROUND_BLUR 36.0 + DROP_SHADOW(#000000/0.251, 반경 38.4)
        /// (shop.md #6) — UI Toolkit은 배경 블러/외부 그림자 미지원 → 수리 보류(감사 보류 축, P1 계약 반영 D1 배경 알파는 유지).</summary>
        private VisualElement MakePanel(string name, Rect bounds)
        {
            var panel = new VisualElement { name = name };
            panel.style.position = Position.Absolute;
            panel.style.left = bounds.x;
            panel.style.top = bounds.y;
            panel.style.width = bounds.width;
            panel.style.height = bounds.height;
            panel.style.backgroundColor = new StyleColor(ShopPanelBg);
            panel.style.borderTopWidth = panel.style.borderBottomWidth =
                panel.style.borderLeftWidth = panel.style.borderRightWidth = 1.2f;
            panel.style.borderTopColor = panel.style.borderBottomColor =
                panel.style.borderLeftColor = panel.style.borderRightColor = new StyleColor(ShopStroke);
            panel.style.borderTopLeftRadius = panel.style.borderTopRightRadius =
                panel.style.borderBottomLeftRadius = panel.style.borderBottomRightRadius = 14.4f;
            panel.style.paddingLeft = panel.style.paddingRight = 0f;
            panel.style.paddingTop = panel.style.paddingBottom = 0f;
            AddPanelCornerDecals(panel, bounds.width, bounds.height);
            return panel;
        }

        /// <summary>[Figma] 패널 코너 디칼 — 4코너 L 브래킷(선 12, #58A6FF 1.8, 프레임 14.4).</summary>
        private static void AddPanelCornerDecals(VisualElement panel, float w, float h)
        {
            void Line(float left, float top, float lw, float lh)
            {
                var l = new VisualElement();
                l.style.position = Position.Absolute;
                l.style.left = left;
                l.style.top = top;
                l.style.width = lw;
                l.style.height = lh;
                l.style.backgroundColor = new StyleColor(ShopAccent);
                l.pickingMode = PickingMode.Ignore;
                panel.Add(l);
            }
            Line(0f, 0f, 12f, 1.8f); Line(0f, 0f, 1.8f, 12f);                       // TL — 오른쪽/아래로
            Line(w - 12f, 0f, 12f, 1.8f); Line(w - 1.8f, 0f, 1.8f, 12f);            // TR — 왼쪽/아래로
            Line(0f, h - 1.8f, 12f, 1.8f); Line(0f, h - 12f, 1.8f, 12f);            // BL — 오른쪽/위로
            Line(w - 12f, h - 1.8f, 12f, 1.8f); Line(w - 1.8f, h - 12f, 1.8f, 12f); // BR — 왼쪽/위로
        }

        /// <summary>[Figma] PanelHeader rel(24,24) w-48×52.8 — 투명+테두리 #30363D 1.2, 타이틀 fs24 fw700,
        /// 서브 fs14.4(좌측 타이틀 우측 +12), CloseBtn 31.2×31.2 white/0.06 r7.2(우측 24-31.2).</summary>
        private void MakePanelHeader(VisualElement panel, Rect bounds, string title, string subtitle)
        {
            var header = new VisualElement { name = "PanelHeader" };
            header.style.position = Position.Absolute;
            header.style.left = 24f; header.style.top = 24f;
            header.style.width = bounds.width - 48f;
            header.style.height = 52.8f;
            header.style.borderTopWidth = header.style.borderBottomWidth =
                header.style.borderLeftWidth = header.style.borderRightWidth = 1.2f;
            header.style.borderTopColor = header.style.borderBottomColor =
                header.style.borderLeftColor = header.style.borderRightColor = new StyleColor(ShopStroke);
            var titleGroup = new VisualElement { name = "TitleGroup" };
            titleGroup.style.position = Position.Absolute;
            titleGroup.style.left = 0f; titleGroup.style.top = 5.2f;   // [Figma] 헤더+5.2
            titleGroup.style.flexDirection = FlexDirection.Row;
            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 24f;
            titleLabel.style.color = UTKTheme.TextMain;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleGroup.Add(titleLabel);
            if (!string.IsNullOrEmpty(subtitle))
            {
                var subLabel = new Label(subtitle);
                subLabel.style.fontSize = 14.4f;
                subLabel.style.color = UTKTheme.TextSub;
                subLabel.style.marginLeft = 12f;   // [Figma] 타이틀 우측 +12
                subLabel.style.marginTop = 9f;     // [Figma] 서브 타이틀 대비 +9 하향(122.2-113.2)
                titleGroup.Add(subLabel);
            }
            header.Add(titleGroup);
            var closeBtn = new Button(Hide) { text = "×", name = "PanelCloseButton" };
            closeBtn.style.position = Position.Absolute;
            closeBtn.style.right = 0f; closeBtn.style.top = 3.6f;   // [Figma] 헤더+3.6
            closeBtn.style.width = 31.2f;
            closeBtn.style.height = 31.2f;
            closeBtn.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.06f));
            closeBtn.style.borderTopWidth = closeBtn.style.borderBottomWidth =
                closeBtn.style.borderLeftWidth = closeBtn.style.borderRightWidth = 1.2f;
            closeBtn.style.borderTopColor = closeBtn.style.borderBottomColor =
                closeBtn.style.borderLeftColor = closeBtn.style.borderRightColor = new StyleColor(ShopStroke);
            closeBtn.style.borderTopLeftRadius = closeBtn.style.borderTopRightRadius =
                closeBtn.style.borderBottomLeftRadius = closeBtn.style.borderBottomRightRadius = 7.2f;
            closeBtn.style.color = UTKTheme.TextSub;   // [Figma] x 벡터 #8B949E
            closeBtn.style.fontSize = 16.8f;
            closeBtn.style.unityTextAlign = TextAnchor.MiddleCenter;
            header.Add(closeBtn);
            panel.Add(header);
        }

        /// <summary>[Figma 74:4] Store 카테고리 탭 — 미선택=투명(#FFFFFF/0)+테두리, 선택=#58A6FF·스트로크 없음, 라벨 fs15.6 fw700.</summary>
        private static void StyleStoreTabButton(Button tab, bool active)
        {
            if (tab == null) return;
            tab.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            tab.style.backgroundColor = active
                ? new StyleColor(ShopAccent)
                : new StyleColor(new Color(1f, 1f, 1f, 0f));   // [Figma] 미선택 fill #FFFFFF/0 — 테두리만
            tab.style.color = active ? new StyleColor(new Color(0x0B / 255f, 0x0E / 255f, 0x14 / 255f, 1f)) : new StyleColor(UTKTheme.TextSub);
            // [144차 P1-SHOP] 활성 탭 테두리 제거(shop.md #17) — Figma 활성 Tab('무기')은 strokes 없음.
            //   구 코드는 모든 상태에 border 1.2 ShopStroke → 활성 탭에 Figma에 없는 테두리가 그려졌다.
            //   미선택 탭 fill(투명)은 144차 재검증 정정으로 PASS — 건드리지 않음.
            float tabBorderW = active ? 0f : 1.2f;
            tab.style.borderTopWidth = tab.style.borderBottomWidth =
                tab.style.borderLeftWidth = tab.style.borderRightWidth = tabBorderW;
            tab.style.borderTopColor = tab.style.borderBottomColor =
                tab.style.borderLeftColor = tab.style.borderRightColor = new StyleColor(ShopStroke);
            tab.style.borderTopLeftRadius = tab.style.borderTopRightRadius =
                tab.style.borderBottomLeftRadius = tab.style.borderBottomRightRadius = 7.2f;
            var label = tab.Q<Label>();
            if (label != null)
            {
                label.style.fontSize = 15.6f;
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                label.style.color = active ? new StyleColor(new Color(0x0B / 255f, 0x0E / 255f, 0x14 / 255f, 1f)) : new StyleColor(UTKTheme.TextSub);
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
            }
        }

        /// <summary>GitHub-dark 버튼 인라인 오버라이드(이 창 한정) — Theme bg_button.png/브론즈 베벨 대체.
        ///   IStyle에 borderWidth 쇼트핸드가 없어 4면 개별 대입한다. 호버는 인라인 배경이 USS :hover를
        ///   가리므로 진입/이탈 콜백으로 밝기 계층만 토글.</summary>
        private static void StyleButton(Button btn, UTKButton.Variant variant)
        {
            if (btn == null) return;
            Color baseBg, hoverBg, textColor;
            switch (variant)
            {
                case UTKButton.Variant.Primary:
                    baseBg = UTKTheme.Accent; hoverBg = UTKTheme.AccentHover; textColor = UTKTheme.BgBase; break;
                case UTKButton.Variant.Danger:
                    baseBg = UTKTheme.Danger; hoverBg = UTKTheme.DangerHover; textColor = UTKTheme.TextMain; break;
                default:
                    baseBg = UTKTheme.PanelSub; hoverBg = UTKTheme.Stroke; textColor = UTKTheme.TextMain; break;
            }

            btn.style.backgroundImage = new StyleBackground(StyleKeyword.None);   // 우드 베이크 이미지 제거
            btn.style.backgroundColor = baseBg;
            btn.style.color = textColor;
            btn.style.borderTopWidth = btn.style.borderBottomWidth = btn.style.borderLeftWidth = btn.style.borderRightWidth = 1f;
            btn.style.borderTopColor = btn.style.borderBottomColor = btn.style.borderLeftColor = btn.style.borderRightColor = new StyleColor(baseBg);
            btn.style.borderTopLeftRadius = 6f;
            btn.style.borderTopRightRadius = 6f;
            btn.style.borderBottomLeftRadius = 6f;
            btn.style.borderBottomRightRadius = 6f;   // 서브 반경 r6

            btn.RegisterCallback<PointerEnterEvent>(_ => btn.style.backgroundColor = hoverBg);
            btn.RegisterCallback<PointerLeaveEvent>(_ => btn.style.backgroundColor = baseBg);
        }

        /// <summary>[GitHub-dark] 탭 버튼 상태 스타일 — SwitchTab마다 인라인 값만 갱신(호버 콜백 미등록).</summary>
        private static void StyleTabButton(Button tab, bool active, bool danger)
        {
            if (tab == null) return;
            Color baseBg = active ? (danger ? UTKTheme.Danger : UTKTheme.Accent) : UTKTheme.PanelSub;
            Color line   = active ? baseBg : UTKTheme.Stroke;
            tab.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            tab.style.backgroundColor = baseBg;
            tab.style.color = active && !danger ? UTKTheme.BgBase : UTKTheme.TextMain;
            tab.style.borderTopWidth = tab.style.borderBottomWidth = tab.style.borderLeftWidth = tab.style.borderRightWidth = 1f;
            tab.style.borderTopColor = tab.style.borderBottomColor = tab.style.borderLeftColor = tab.style.borderRightColor = new StyleColor(line);
            tab.style.borderTopLeftRadius = 6f;
            tab.style.borderTopRightRadius = 6f;
            tab.style.borderBottomLeftRadius = 6f;
            tab.style.borderBottomRightRadius = 6f;
        }

        /// <summary>[GitHub-dark] 상점 리스트 행 — 보조 패널 bg + 1px 스트로크 + r6 (우드 디바이더 대체).</summary>
        private static void StyleListRow(VisualElement row)
        {
            if (row == null) return;
            row.style.backgroundColor = UTKTheme.PanelSub;
            row.style.borderTopWidth = row.style.borderBottomWidth = row.style.borderLeftWidth = row.style.borderRightWidth = 1f;
            row.style.borderTopColor = row.style.borderBottomColor = row.style.borderLeftColor = row.style.borderRightColor = new StyleColor(UTKTheme.Stroke);
            row.style.borderTopLeftRadius = 6f;
            row.style.borderTopRightRadius = 6f;
            row.style.borderBottomLeftRadius = 6f;
            row.style.borderBottomRightRadius = 6f;
        }

        /// <summary>[GitHub-dark] 상점 슬롯 인셋 — Theme bg_slot.png/베벨 제거, 다크 바닥 + 레어도 링(기본 스트로크).</summary>
        private static void StyleShopSlot(UTKSlot slot, Color ring)
        {
            if (slot == null) return;
            slot.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            slot.style.backgroundColor = UTKTheme.BgBase;
            slot.style.borderTopWidth = slot.style.borderBottomWidth = slot.style.borderLeftWidth = slot.style.borderRightWidth = 1f;
            slot.style.borderTopColor = slot.style.borderBottomColor = slot.style.borderLeftColor = slot.style.borderRightColor = new StyleColor(ring);
            slot.style.borderTopLeftRadius = 6f;
            slot.style.borderTopRightRadius = 6f;
            slot.style.borderBottomLeftRadius = 6f;
            slot.style.borderBottomRightRadius = 6f;
            var countLabel = slot.Q<Label>("Count");
            if (countLabel != null) countLabel.style.color = UTKTheme.TextMain;
        }

        /// <summary>GitHub-dark 레어도 링 — uncommon/rare=액센트, epic=프라이머 퍼플, 전설/유니크=금색.</summary>
        private static Color RankRing(int rarityIndex)
        {
            switch (rarityIndex)
            {
                case 1:
                case 2: return UTKTheme.Accent;
                case 3: return Hex(0xA371F7);
                case 4:
                case 5: return UTKTheme.Gold;
                case 0: return UTKTheme.TextSub;
                default: return UTKTheme.Stroke;
            }
        }


        // ===== 상점 인벤토리 (원본과 동일 소스로 초기화) =====
        private readonly List<ShopItem> _shopInventory = new List<ShopItem>();

        // ===== UI 노드 =====
        private readonly Label _goldLabel;
        private readonly Button _tabBuy;
        private readonly Button _tabSell;
        private readonly VisualElement _buyPanel;
        private readonly VisualElement _sellPanel;
        private readonly ScrollView _buyScroll;
        private readonly ScrollView _sellScroll;
        private readonly Label _statusLabel;
        private readonly List<Button> _storeCatTabs = new List<Button>();   // [Figma] 6탭 카테고리 필터

        // 선택 상태
        private int _selectedBuyIndex = -1;
        private StoreCat _storeCat = StoreCat.All;   // [Figma] 활성 Store 카테고리

        /// <summary>테스트/외부 접근용 읽기 전용 인벤토리.</summary>
        public IReadOnlyList<ShopItem> ShopInventory => _shopInventory;

        // ───────────────────────────────────────────────
        // 생성자 — Figma 3열 레이아웃 (Store | Detail | Sell/인벤)
        // ───────────────────────────────────────────────
        public ShopWindowUTK() : base("🏪 상점", new Vector2(1632f, 912f))
        {
            _titleLabel.text = "";
            this.Q("TitleBar").style.display = DisplayStyle.None;
            style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 0f;
            style.width = Length.Percent(100f);
            style.height = Length.Percent(100f);
            style.left = 0f;
            style.top = 0f;
            _content.style.position = Position.Absolute;
            _content.style.left = _content.style.top = 0f;
            // [Figma 정합 v3] 디자인공간 = raw 1920×1080 — ApplyDesignSpace가 scale=k 전파(요소별 재스케일 금지 계약).
            _content.style.width = FigmaCanvasLayout.CanvasWidth;
            _content.style.height = FigmaCanvasLayout.CanvasHeight;
            _content.style.flexDirection = FlexDirection.Row;
            _content.style.flexGrow = 0f;
            _content.style.paddingLeft = _content.style.paddingRight = 0f;
            _content.style.paddingTop = _content.style.paddingBottom = 0f;

            // [Figma 74:4 정합 — 루트 배경] 전체 딤 레이어 #0B0E14/0.8(피그마 root fill IMAGE+동색).
            //   기존 계약 유지: pickingMode.Ignore → 패널 바깥 클릭은 게임으로 통과(호스트 불간섭 계약 무변경).
            var backdrop = new VisualElement { name = "ShopBackdrop" };
            backdrop.style.position = Position.Absolute;
            backdrop.style.left = 0f; backdrop.style.top = 0f;
            backdrop.style.right = 0f; backdrop.style.bottom = 0f;
            backdrop.style.backgroundColor = new StyleColor(new Color(0x0B / 255f, 0x0E / 255f, 0x14 / 255f, 0.8f));
            backdrop.pickingMode = PickingMode.Ignore;
            _content.Add(backdrop);

            // [Figma 정합 폐기] topBar(골드 바) 제거 — 골드는 Storage 패널 StorageStats(rel 24,101)로 이동.
            //   _goldLabel 생성/스타일은 storageStats 블록에서 수행(UpdateGoldDisplay 참조 유지).

            // [Figma 정합] Detail 헤더 우측 구매/판매 전환 — Figma 74:4(구매)/76:7(판매) 두 판본의
            //   모드 스위치. 헤더 "아이템 상세 정보" 좌측과 나란히 컴팩트 배치(rel 300,30 228×33.6).
            var tabRow = new VisualElement { name = "ShopModeTabs" };
            tabRow.style.position = Position.Absolute;
            tabRow.style.left = DetailBounds.x + 300f;
            tabRow.style.top = DetailBounds.y + 30f;
            tabRow.style.width = 228f;
            tabRow.style.height = 33.6f;
            tabRow.style.flexDirection = FlexDirection.Row;
            tabRow.style.marginBottom = 6f;
            _content.Add(tabRow);

            _tabBuy = UTKButton.Create("구매", () => SwitchTab(ShopTab.Buy), UTKButton.Variant.Primary);
            _tabSell = UTKButton.Create("판매", () => SwitchTab(ShopTab.Sell), UTKButton.Variant.Secondary);
            _tabBuy.style.flexGrow = 1f;
            _tabSell.style.flexGrow = 1f;
            tabRow.Add(_tabBuy);
            tabRow.Add(_tabSell);

            // ── 3열 본체 (Figma Store | Detail | Sell) ──
            var columns = new VisualElement();
            columns.style.position = Position.Absolute;
            columns.style.left = 0f;
            columns.style.top = 0f;
            columns.style.width = Length.Percent(100f);
            columns.style.height = Length.Percent(100f);
            _content.Add(columns);

            // [좌] Store 패널 — 구매 목록 ([Figma 74:4 전수 실측 2026-10-29] rel 원본 좌표)
            var storeCol = MakePanel("StorePanel", StoreBounds);
            columns.Add(storeCol);
            MakePanelHeader(storeCol, StoreBounds, "상점", "MERCHANT STORE");

            // TabContainer rel(24,91.2) 456×37.2 — 고정폭 6탭(전체 72.4/무기 70/나머지 72.4), 간격 4.8, r7.2
            var storeCatRow = new VisualElement { name = "TabContainer" };
            storeCatRow.style.position = Position.Absolute;
            storeCatRow.style.left = 24f; storeCatRow.style.top = 91.2f;
            storeCatRow.style.width = StoreBounds.width - 48f;
            storeCatRow.style.height = 37.2f;
            storeCatRow.style.flexDirection = FlexDirection.Row;
            storeCol.Add(storeCatRow);

            float[] catWidths = { 72.4f, 70f, 72.4f, 72.4f, 72.4f, 72.4f };
            string[] catLabels = { "전체", "무기", "방어구", "소모품", "재료", "레시피" };
            for (int ci = 0; ci < catLabels.Length; ci++)
            {
                var cat = (StoreCat)ci;
                var catBtn = UTKButton.Create(catLabels[ci], () => SelectStoreCat(cat), UTKButton.Variant.Secondary);
                catBtn.style.width = catWidths[ci];
                catBtn.style.height = 37.2f;
                catBtn.style.flexShrink = 0f;
                if (ci < catLabels.Length - 1) catBtn.style.marginRight = 4.8f;   // [Figma] 탭 간격 4.8
                StyleStoreTabButton(catBtn, active: ci == 0);
                _storeCatTabs.Add(catBtn);
                storeCatRow.Add(catBtn);
            }
            RefreshStoreCatTabs();   // 초기 활성(전체) 강조

            // StoreGridSection rel(24,142.8) 456×528 — 5열 그리드(셀 81.6/피치 91.2), 스크롤 뷰포트
            _buyPanel = new VisualElement();
            _buyPanel.style.position = Position.Absolute;
            _buyPanel.style.left = 24f; _buyPanel.style.top = 142.8f;
            _buyPanel.style.width = StoreBounds.width - 48f;
            _buyPanel.style.height = 528f;
            _buyScroll = new ScrollView();
            _buyScroll.style.flexGrow = 1f;
            _buyScroll.contentContainer.style.flexDirection = FlexDirection.Row;
            _buyScroll.contentContainer.style.flexWrap = Wrap.Wrap;
            _buyScroll.contentContainer.style.alignContent = Align.FlexStart;
            _buyPanel.Add(_buyScroll);
            storeCol.Add(_buyPanel);

            // StoreFooter rel(24,685.2) 456×31.4 — 테두리 #30363D 1.2 + 텍스트행 y14.4(h17, 하단 정렬 계약)
            var storeFooter = new VisualElement { name = "StoreFooter" };
            storeFooter.style.position = Position.Absolute;
            storeFooter.style.left = 24f; storeFooter.style.top = 685.2f;
            storeFooter.style.width = StoreBounds.width - 48f;
            storeFooter.style.height = 31.4f;
            storeFooter.style.borderTopWidth = storeFooter.style.borderBottomWidth =
                storeFooter.style.borderLeftWidth = storeFooter.style.borderRightWidth = 1.2f;
            storeFooter.style.borderTopColor = storeFooter.style.borderBottomColor =
                storeFooter.style.borderLeftColor = storeFooter.style.borderRightColor =
                    new StyleColor(ShopStroke);
            var footerRow = new VisualElement { name = "FooterStatus" };
            footerRow.style.position = Position.Absolute;
            footerRow.style.left = 0f; footerRow.style.top = 14.4f;
            footerRow.style.width = StoreBounds.width - 48f;
            footerRow.style.height = 17f;
            footerRow.style.flexDirection = FlexDirection.Row;
            var intimacyLabel = new Label("상인 친밀도: Lv. 1");
            intimacyLabel.style.fontSize = 14.4f;
            intimacyLabel.style.color = UTKTheme.TextSub;
            intimacyLabel.style.flexGrow = 1f;
            var discountLabel = new Label("전 상품 5% 할인 적용 중");
            discountLabel.style.fontSize = 14.4f;
            discountLabel.style.color = ShopAccent;   // [Figma 74:4] DiscountText #58A6FF
            footerRow.Add(intimacyLabel);
            footerRow.Add(discountLabel);
            storeFooter.Add(footerRow);
            storeCol.Add(storeFooter);

            // [중앙] Detail 패널 — 선택 아이템 상세 + 액션 ([Figma 74:4 전수 실측] rel 원본 좌표)
            var detailCol = MakePanel("DetailPanel", DetailBounds);
            columns.Add(detailCol);
            MakePanelHeader(detailCol, DetailBounds, "아이템 상세 정보", null);

            BuildDetailPanel(detailCol);

            // [우] Storage 패널 ([Figma 74:4 전수 실측] rel 원본 좌표)
            var sellCol = MakePanel("InventoryPanel", InventoryBounds);
            columns.Add(sellCol);
            MakePanelHeader(sellCol, InventoryBounds, "나의 가방", "PLAYER INVENTORY");

            // StorageStats rel(24,100.8) 456×26.6 — BadgeItem 칩(76.2×26.6 white/0.04 r4.8) + 골드(우측)
            var storageStats = new VisualElement { name = "StorageStats" };
            storageStats.style.position = Position.Absolute;
            storageStats.style.left = 24f; storageStats.style.top = 100.8f;
            storageStats.style.width = InventoryBounds.width - 48f;
            storageStats.style.height = 26.6f;
            storageStats.style.flexDirection = FlexDirection.Row;
            storageStats.style.alignItems = Align.Center;
            var spaceBadge = new VisualElement { name = "BadgeItem" };
            spaceBadge.style.width = 76.2f;
            spaceBadge.style.height = 26.6f;
            spaceBadge.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.04f));
            spaceBadge.style.borderTopLeftRadius = spaceBadge.style.borderTopRightRadius =
                spaceBadge.style.borderBottomLeftRadius = spaceBadge.style.borderBottomRightRadius = 4.8f;
            spaceBadge.style.alignItems = Align.Center;
            spaceBadge.style.justifyContent = Justify.Center;
            var spaceLabel = new Label("휴대 공간");
            spaceLabel.style.fontSize = 14.4f;
            spaceLabel.style.color = UTKTheme.TextMain;   // [Figma] BadgeText #F0F6FC
            spaceBadge.Add(spaceLabel);
            storageStats.Add(spaceBadge);
            var goldSpacer = new VisualElement();
            goldSpacer.style.flexGrow = 1f;
            storageStats.Add(goldSpacer);
            var goldStatus = new VisualElement { name = "GoldStatus" };
            goldStatus.style.flexDirection = FlexDirection.Row;
            goldStatus.style.alignItems = Align.Center;
            var goldCoin = new VisualElement { name = "GoldCoin" };
            goldCoin.style.width = 16.8f;
            goldCoin.style.height = 16.8f;
            goldCoin.style.borderTopWidth = goldCoin.style.borderBottomWidth =
                goldCoin.style.borderLeftWidth = goldCoin.style.borderRightWidth = 2.4f;
            goldCoin.style.borderTopColor = goldCoin.style.borderBottomColor =
                goldCoin.style.borderLeftColor = goldCoin.style.borderRightColor = new StyleColor(ShopGold);
            goldCoin.style.borderTopLeftRadius = goldCoin.style.borderTopRightRadius =
                goldCoin.style.borderBottomLeftRadius = goldCoin.style.borderBottomRightRadius =
                    new Length(8.4f, LengthUnit.Pixel);   // [Figma 근사] coins 벡터 아이콘 → 동전 원형 링
            goldStatus.Add(goldCoin);
            _goldLabel = new Label("0 G");
            _goldLabel.style.fontSize = 15.6f;
            _goldLabel.style.color = ShopGold;   // [Figma] GoldValue #E3B341
            _goldLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _goldLabel.style.marginLeft = 7.8f;
            goldStatus.Add(_goldLabel);
            storageStats.Add(goldStatus);
            sellCol.Add(storageStats);

            // StorageGrid rel(24,151.4) 456×537.6 — 6행 5열 그리드
            _sellPanel = new VisualElement();
            _sellPanel.style.position = Position.Absolute;
            _sellPanel.style.left = 24f; _sellPanel.style.top = 151.4f;
            _sellPanel.style.width = InventoryBounds.width - 48f;
            _sellPanel.style.height = 537.6f;
            _sellScroll = new ScrollView();
            _sellScroll.style.flexGrow = 1f;
            _sellScroll.contentContainer.style.flexDirection = FlexDirection.Row;
            _sellScroll.contentContainer.style.flexWrap = Wrap.Wrap;
            _sellScroll.contentContainer.style.alignContent = Align.FlexStart;
            _sellPanel.Add(_sellScroll);
            sellCol.Add(_sellPanel);

            // StorageFooter rel(24,713) 456×51.2 — 테두리 + 무게행(y14.4) + ProgressBar(y44, 7.2)
            var storageFooter = new VisualElement { name = "StorageFooter" };
            storageFooter.style.position = Position.Absolute;
            storageFooter.style.left = 24f; storageFooter.style.top = 713f;
            storageFooter.style.width = InventoryBounds.width - 48f;
            storageFooter.style.height = 51.2f;
            storageFooter.style.borderTopWidth = storageFooter.style.borderBottomWidth =
                storageFooter.style.borderLeftWidth = storageFooter.style.borderRightWidth = 1.2f;
            storageFooter.style.borderTopColor = storageFooter.style.borderBottomColor =
                storageFooter.style.borderLeftColor = storageFooter.style.borderRightColor =
                    new StyleColor(ShopStroke);
            var weightRow = new VisualElement { name = "WeightRow" };
            weightRow.style.position = Position.Absolute;
            weightRow.style.left = 0f; weightRow.style.top = 14.4f;
            weightRow.style.width = InventoryBounds.width - 48f;
            weightRow.style.height = 20f;
            weightRow.style.flexDirection = FlexDirection.Row;
            weightRow.style.alignItems = Align.Center;
            var weightLabel = new Label("가방 무게");
            weightLabel.style.fontSize = 14.4f;
            weightLabel.style.color = UTKTheme.TextSub;
            weightLabel.style.flexGrow = 1f;
            _storageCountLabel = new Label("0 / 80 칸");
            _storageCountLabel.style.fontSize = 15.6f;
            _storageCountLabel.style.color = ShopAccent;   // [Figma] WeightValue #58A6FF
            _storageCountLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            weightRow.Add(weightLabel);
            weightRow.Add(_storageCountLabel);
            storageFooter.Add(weightRow);
            var weightBar = new VisualElement { name = "ProgressBar" };
            weightBar.style.position = Position.Absolute;
            weightBar.style.left = 0f; weightBar.style.top = 44f;
            weightBar.style.width = InventoryBounds.width - 48f;
            weightBar.style.height = 7.2f;
            weightBar.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.08f));
            weightBar.style.borderTopLeftRadius = weightBar.style.borderTopRightRadius =
                weightBar.style.borderBottomLeftRadius = weightBar.style.borderBottomRightRadius = 3.6f;
            _weightFill = new VisualElement { name = "ProgressFill" };
            _weightFill.style.position = Position.Absolute;
            _weightFill.style.left = 0f; _weightFill.style.top = 0f;
            _weightFill.style.height = 7.2f;
            _weightFill.style.width = 0f;
            _weightFill.style.backgroundColor = new StyleColor(ShopAccent);
            _weightFill.style.borderTopLeftRadius = _weightFill.style.borderTopRightRadius =
                _weightFill.style.borderBottomLeftRadius = _weightFill.style.borderBottomRightRadius = 3.6f;
            weightBar.Add(_weightFill);
            storageFooter.Add(weightBar);
            sellCol.Add(storageFooter);

            // ── 상태 라벨 (Figma: Detail 패널 하단 — 액션 버튼 아래 rel 845) ──
            _statusLabel = new Label("") { name = "ShopStatus" };
            _statusLabel.style.fontSize = 14.4f;
            _statusLabel.style.color = UTKTheme.TextSub;
            _statusLabel.style.minHeight = 20f;
            _statusLabel.style.position = Position.Absolute;
            _statusLabel.style.left = DetailBounds.x + 24f;
            _statusLabel.style.top = DetailBounds.y + 895f;   // [130차] 액션버튼(834.4~888) 아래 — 런타임 전용 요소(Figma 밖)
            _statusLabel.style.width = DetailBounds.width - 48f;
            _content.Add(_statusLabel);

            SwitchTab(ShopTab.Buy);
            ShowDetail(null, true);
            var titleBar = this.Q("TitleBar");
            UTKTheme.ApplyWindowChrome(this, titleBar, _content);
            // 상점은 의도된 다크 패널이며 Theme의 우드 베이크 창 이미지를 사용하지 않는다.
            style.backgroundImage = new StyleBackground(StyleKeyword.None);
            // The full-screen host must not paint over the canvas or intercept clicks;
            // its independently styled descendants remain pickable and visible.
            style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 0f;
            pickingMode = PickingMode.Ignore;
            _content.RegisterCallback<GeometryChangedEvent>(_ => ApplyResponsivePanelLayout());
        }

        /// <summary>[Figma 정합 v3] 등배수 디자인공간 — 창 박스=캔버스(0,0,1920,1080)×k, _content는 raw 유지+scale=k.
        /// 구 X/Y 독립 스케일(ApplyPanelRect sx/sy — 32차 폐기 대상)은 완전 제거. 내부 자식은 raw Figma 좌표 정답.</summary>
        private void ApplyResponsivePanelLayout()
        {
            VisualElement root = parent;
            if (root == null) return;
            FigmaCanvasLayout.ApplyDesignSpace(this, _content,
                new Rect(0f, 0f, FigmaCanvasLayout.CanvasWidth, FigmaCanvasLayout.CanvasHeight), root);
        }

        /// <summary>[Figma Detail 패널] 중앙 컬럼 — 선택 아이템 상세 + 구매/판매 버튼.</summary>
        private UTKSlot _detailSlot;
        private Label _detailCategory;   // [Figma 정합] 등급 라인 좌측 — "전설적인 무기 (LEGENDARY)" 14.4
        private Label _detailType;       // [Figma 정합] 등급 라인 우측 — "양손검" 14.4
        private Label _detailName;
        private Label _detailDesc;
        private Label _storageCountLabel;   // [Figma 정합] StorageFooter 우측 — "N / 80 칸"
        private VisualElement _weightFill;   // [Figma 정합] StorageFooter ProgressBar — 칸 사용률 fill
        private Label[] _statValueLabels;   // [Figma 정합] DescriptionSection StatsGrid 값 라벨 3종
        private Button _detailActionBtn;
        private ShopItem _detailShopItem;      // 구매 대상 (null = 판매/비활성)
        private PlayerInventory.ItemSlot _detailSellSlot = null;   // 판매 대상

        private void BuildDetailPanel(VisualElement parent)
        {
            // [Figma 74:4 전수 실측] Detail = 4섹션 절대 배치 —
            //   ItemNameSection(24,96,528×84) → ItemImageSection(24,199.2,528×336) →
            //   DescriptionSection(24,554.4,528×260.8) → ActionButtonGroup(24,834.4,528×53.6).
            //   공통: 투명톤 배경 + 테두리 #30363D/1.2 + r9.6.

            // ① ItemNameSection — 등급 라인(fs14.4 fw700 등급색 좌 / fs14.4 우) + 아이템명 fs26.4 fw700
            var nameSection = new VisualElement { name = "ItemNameSection" };
            nameSection.style.position = Position.Absolute;
            nameSection.style.left = 24f; nameSection.style.top = 96f;
            nameSection.style.width = DetailBounds.width - 48f;
            nameSection.style.height = 84f;
            nameSection.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.02f));
            nameSection.style.borderTopWidth = nameSection.style.borderBottomWidth =
                nameSection.style.borderLeftWidth = nameSection.style.borderRightWidth = 1.2f;
            nameSection.style.borderTopColor = nameSection.style.borderBottomColor =
                nameSection.style.borderLeftColor = nameSection.style.borderRightColor = new StyleColor(ShopStroke);
            nameSection.style.borderTopLeftRadius = nameSection.style.borderTopRightRadius =
                nameSection.style.borderBottomLeftRadius = nameSection.style.borderBottomRightRadius = 9.6f;
            var catRow = new VisualElement { name = "RarityRow" };
            catRow.style.position = Position.Absolute;
            catRow.style.left = 14.4f; catRow.style.top = 14.4f;   // [Figma] 섹션+14.4
            catRow.style.width = 528f - 28.8f;   // [Figma] RarityRow 499.2 = 528 - 좌우 인셋 14.4×2
            catRow.style.height = 17f;
            catRow.style.flexDirection = FlexDirection.Row;
            catRow.style.alignItems = Align.Center;
            _detailCategory = new Label("");
            _detailCategory.style.fontSize = 14.4f;
            _detailCategory.style.color = UTKTheme.TextSub;   // 선택 시 등급색으로 갱신(ShowDetail)
            _detailCategory.style.unityFontStyleAndWeight = FontStyle.Bold;
            _detailCategory.style.flexGrow = 1f;
            _detailType = new Label("");
            _detailType.style.fontSize = 14.4f;
            _detailType.style.color = UTKTheme.TextSub;
            catRow.Add(_detailCategory);
            catRow.Add(_detailType);
            nameSection.Add(catRow);
            _detailName = new Label("아이템을 선택하세요");
            _detailName.style.position = Position.Absolute;
            _detailName.style.left = 14.4f; _detailName.style.top = 38.6f;   // [Figma] 섹션+38.6
            _detailName.style.fontSize = 26.4f;
            _detailName.style.color = UTKTheme.TextMain;
            _detailName.style.unityFontStyleAndWeight = FontStyle.Bold;
            _detailName.style.whiteSpace = WhiteSpace.Normal;
            nameSection.Add(_detailName);
            parent.Add(nameSection);

            // ② ItemImageSection — 528×336 #0D1117 r9.6, 이미지 288×288 중앙(테두리 프레임 없음 — Figma IMAGE rect)
            var imgSection = new VisualElement { name = "ItemImageSection" };
            imgSection.style.position = Position.Absolute;
            imgSection.style.left = 24f; imgSection.style.top = 199.2f;
            imgSection.style.width = DetailBounds.width - 48f;
            imgSection.style.height = 336f;
            imgSection.style.backgroundColor = new StyleColor(new Color(0x0D / 255f, 0x11 / 255f, 0x17 / 255f, 1f));
            imgSection.style.borderTopWidth = imgSection.style.borderBottomWidth =
                imgSection.style.borderLeftWidth = imgSection.style.borderRightWidth = 1.2f;
            imgSection.style.borderTopColor = imgSection.style.borderBottomColor =
                imgSection.style.borderLeftColor = imgSection.style.borderRightColor = new StyleColor(ShopStroke);
            imgSection.style.borderTopLeftRadius = imgSection.style.borderTopRightRadius =
                imgSection.style.borderBottomLeftRadius = imgSection.style.borderBottomRightRadius = 9.6f;
            imgSection.style.alignItems = Align.Center;
            imgSection.style.justifyContent = Justify.Center;
            _detailSlot = new UTKSlot();
            _detailSlot.style.width = 288f;
            _detailSlot.style.height = 288f;
            ClearSlotFrame(_detailSlot);   // [Figma] 상세 이미지는 슬롯 틀 없음 — 이미지 자체만
            imgSection.Add(_detailSlot);
            parent.Add(imgSection);

            // ③ DescriptionSection — StatsGrid 3행(피치 27.2) + 구분선(y105.6) + 설명(y120)
            var descSection = new VisualElement { name = "DescriptionSection" };
            descSection.style.position = Position.Absolute;
            descSection.style.left = 24f; descSection.style.top = 554.4f;
            descSection.style.width = DetailBounds.width - 48f;
            descSection.style.height = 260.8f;
            descSection.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.01f));
            descSection.style.borderTopWidth = descSection.style.borderBottomWidth =
                descSection.style.borderLeftWidth = descSection.style.borderRightWidth = 1.2f;
            descSection.style.borderTopColor = descSection.style.borderBottomColor =
                descSection.style.borderLeftColor = descSection.style.borderRightColor = new StyleColor(ShopStroke);
            descSection.style.borderTopLeftRadius = descSection.style.borderTopRightRadius =
                descSection.style.borderBottomLeftRadius = descSection.style.borderBottomRightRadius = 9.6f;
            string[] statNames = { "공격력", "치명타 확률", "요구 레벨" };
            _statValueLabels = new Label[3];
            for (int i = 0; i < 3; i++)
            {
                var statRow = new VisualElement { name = $"StatRow{i + 1}" };
                statRow.style.position = Position.Absolute;
                statRow.style.left = 16.8f; statRow.style.top = 16.8f + 27.2f * i;   // [Figma] 섹션+16.8, 피치 27.2
                statRow.style.width = 494.4f;
                statRow.style.height = 20f;
                statRow.style.flexDirection = FlexDirection.Row;
                statRow.style.alignItems = Align.Center;
                var statLabel = new Label(statNames[i]);
                statLabel.style.fontSize = 15.6f;
                statLabel.style.color = UTKTheme.TextSub;
                statLabel.style.flexGrow = 1f;
                var statValue = new Label("-");   // [Figma 구조 정합] 값은 실데이터 연동 — 미지원 항목은 "-"
                statValue.style.fontSize = 15.6f;
                statValue.style.color = UTKTheme.TextMain;
                statValue.style.unityFontStyleAndWeight = FontStyle.Bold;
                _statValueLabels[i] = statValue;
                statRow.Add(statLabel);
                statRow.Add(statValue);
                descSection.Add(statRow);
            }
            var separator = new VisualElement { name = "Separator" };
            separator.style.position = Position.Absolute;
            separator.style.left = 16.8f; separator.style.top = 105.6f;   // [Figma] LINE y744-638.4=105.6
            separator.style.width = 494.4f;
            separator.style.height = 1.2f;
            separator.style.backgroundColor = new StyleColor(ShopStroke);
            descSection.Add(separator);
            _detailDesc = new Label("");
            _detailDesc.style.position = Position.Absolute;
            _detailDesc.style.left = 16.8f; _detailDesc.style.top = 120f;   // [Figma] ItemLore 섹션+120
            _detailDesc.style.width = 494.4f;
            _detailDesc.style.fontSize = 15.6f;
            _detailDesc.style.color = UTKTheme.TextSub;   // [Figma] ItemLore #8B949E
            _detailDesc.style.whiteSpace = WhiteSpace.Normal;
            descSection.Add(_detailDesc);
            parent.Add(descSection);

            // ④ ActionButtonGroup — 전폭 528×53.6, r9.6, 텍스트에 가격 포함("구매하기 (12,500G)")
            _detailActionBtn = UTKButton.Create("구매", () => OnDetailAction(), UTKButton.Variant.Primary);
            _detailActionBtn.style.position = Position.Absolute;
            _detailActionBtn.style.left = 24f; _detailActionBtn.style.top = 834.4f;
            _detailActionBtn.style.height = 53.6f;
            _detailActionBtn.style.width = 528f;
            StyleButton(_detailActionBtn, UTKButton.Variant.Primary);
            // [144차 P1-SHOP] 액션 버튼 텍스트 웨이트 — Figma BuyText/SellText 진실치 "Roboto 700 Bold"(shop.md #42/#46,
            //   진실치 JSON shop_buy__74_4.json BuyText font 필드). 구 코드 무설정(400) 괴리 수리 → Bold 적용.
            //   인라인 스타일은 StyleButton이 웨이트를 건드리지 않아 생성 1회 설정으로 유지된다.
            _detailActionBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            _detailActionBtn.style.borderTopLeftRadius = _detailActionBtn.style.borderTopRightRadius =
                _detailActionBtn.style.borderBottomLeftRadius = _detailActionBtn.style.borderBottomRightRadius = 9.6f;
            parent.Add(_detailActionBtn);
        }

        /// <summary>[Figma] 상세 이미지 슬롯 — 슬롯 틀(배경/테두리/등급링) 제거, 이미지 자체만 표시.</summary>
        private static void ClearSlotFrame(UTKSlot slot)
        {
            if (slot == null) return;
            slot.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            slot.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
            slot.style.borderTopWidth = slot.style.borderBottomWidth =
                slot.style.borderLeftWidth = slot.style.borderRightWidth = 0f;
        }

        /// <summary>[Figma 74:4] 등급 형용사형 — "전설적인 무기 (LEGENDARY)" 형식(표본: 전설적인).</summary>
        private static string RarityAdjectiveKorean(ItemRarity r) => r switch
        {
            ItemRarity.Common => "일반적인",
            ItemRarity.Uncommon => "고급의",
            ItemRarity.Rare => "희귀한",
            ItemRarity.Epic => "영웅의",
            ItemRarity.Legendary => "전설적인",
            ItemRarity.Unique => "유니크한",
            _ => "일반적인",
        };

        /// <summary>[Figma 74:4] TierStrip 등급 색 — 표본(전설 #BC8CFF/희귀 #1F6FEB/일반 #8B949E) + 게임 등급 확장.</summary>
        private static Color TierStripColor(ItemRarity r)
        {
            switch (r)
            {
                case ItemRarity.Legendary:
                case ItemRarity.Unique: return ShopPurple;
                case ItemRarity.Rare:
                case ItemRarity.Epic: return ShopBlue;
                default: return UTKTheme.TextSub;   // Common/Uncommon — Figma 표본 그레이
            }
        }

        /// <summary>[Figma 74:4] StatsGrid 값 갱신 — 실데이터 연동(effects 문자열), 미지원 항목은 "-" 유지.</summary>
        private void ApplyStatValues(PlayerInventory.ItemData item)
        {
            if (_statValueLabels == null) return;
            string effects = item?.effects ?? "";
            string[] values = { ExtractEffectValue(effects, "공격"), ExtractEffectValue(effects, "치명"), ExtractEffectValue(effects, "레벨") };
            for (int i = 0; i < _statValueLabels.Length; i++)
            {
                if (_statValueLabels[i] == null) continue;
                _statValueLabels[i].text = values[i];
                _statValueLabels[i].style.color = i == 0
                    ? new StyleColor(UTKTheme.TextMain)
                    : new StyleColor(i == 1 ? ShopAccent : ShopPurple);   // [Figma] 공격력=본문색/치명타=액센트/요구레벨=퍼플
            }
        }

        /// <summary>effects 문자열에서 키워드 포함 항목의 수치 조각 추출(없으면 "-").</summary>
        private static string ExtractEffectValue(string effects, string keyword)
        {
            if (string.IsNullOrEmpty(effects)) return "-";
            foreach (var part in effects.Split(new[] { ',', '\n', '/' }))
            {
                var seg = part.Trim();
                if (seg.Length > 0 && seg.Contains(keyword))
                    return seg;
            }
            return "-";
        }

        /// <summary>[Figma 정합 118차 후속] 등급 한글 라벨 — ItemRarity → "전설적인 무기 (LEGENDARY)" 형식.</summary>
        private static string RarityKorean(ItemRarity r) => r switch
        {
            ItemRarity.Common => "일반",
            ItemRarity.Uncommon => "고급",
            ItemRarity.Rare => "희귀",
            ItemRarity.Epic => "영웅",
            ItemRarity.Legendary => "전설",
            ItemRarity.Unique => "유니크",
            _ => "일반",
        };

        /// <summary>[Figma 정합 118차 후속] 카테고리 한글 라벨 — ItemCategory → "양손검" 우측 라인.</summary>
        private static string CategoryKorean(PlayerInventory.ItemCategory c) => c switch
        {
            PlayerInventory.ItemCategory.Herb => "약초",
            PlayerInventory.ItemCategory.Meat => "고기",
            PlayerInventory.ItemCategory.Food => "식품",
            PlayerInventory.ItemCategory.Potion => "물약",
            PlayerInventory.ItemCategory.Material => "재료",
            PlayerInventory.ItemCategory.Drug => "마약",
            PlayerInventory.ItemCategory.Quest => "퀘스트",
            PlayerInventory.ItemCategory.Weapon => "무기",
            PlayerInventory.ItemCategory.Armor => "방어구",
            PlayerInventory.ItemCategory.Accessory => "장신구",
            PlayerInventory.ItemCategory.Tool => "도구",
            PlayerInventory.ItemCategory.Arrow => "화살",
            PlayerInventory.ItemCategory.Bomb => "폭탄",
            _ => "기타",
        };

        /// <summary>Detail 패널 갱신. isBuy=true이면 상점 아이템, false이면 판매(인벤) 아이템 상세.</summary>
        private void ShowDetail(ShopItem shopItem, bool isBuy)
        {
            _detailShopItem = isBuy ? shopItem : null;
            _detailSellSlot = isBuy ? null : _detailSellSlot;

            if (isBuy && shopItem != null && shopItem.item != null)
            {
                _detailSlot.SetIcon(ItemIconDatabase.GetOrCreateIcon(shopItem.item));
                _detailSlot.SetRank("");
                ClearSlotFrame(_detailSlot);   // [Figma] 상세 이미지는 슬롯 틀 없음
                _detailName.text = shopItem.item.displayName;
                // [Figma 74:4 정합] RarityText = "전설적인 무기 (LEGENDARY)" — 형용사형+카테고리+영문, 색=등급색 fw700
                _detailCategory.text = $"{RarityAdjectiveKorean(shopItem.item.rarity)} {CategoryKorean(shopItem.item.category)} ({shopItem.item.rarity.ToString().ToUpper()})";
                _detailCategory.style.color = new StyleColor(TierStripColor(shopItem.item.rarity));
                // [144차 P1-SHOP] Figma ItemType='양손검'(shop.md #34) — 실데이터 연결 검토 결과: 상점 아이템 소스는
                //   PlayerInventory.ItemData(필드: id/displayName/description/category/rarity 등)로 서브타입 없음.
                //   WeaponType(Fist/Sword/Spear/Bow)은 WeaponData 전용으로 ItemData와 미연동 — "양손검" 판정 근거 부재.
                //   → 데이터 위조 금지 계약에 따라 슬롯(라벨) 유지·공백. 실데이터 연결 시 본 행 교체.
                _detailType.text = "";   // [Figma] ItemType=세부 유형(양손검) — 게임 데이터에 서브타입 없음(공백 유지)
                _detailDesc.text = shopItem.item.description ?? "";
                int bp = GetBuyPrice(shopItem);
                ApplyStatValues(shopItem.item);
                _detailActionBtn.text = $"구매하기 ({bp:N0}G)";   // [Figma 정합] "구매하기 (12,500G)"
                _detailActionBtn.SetEnabled((PlayerStats.Instance?.Gold ?? 0) >= bp && (shopItem.stock == -1 || shopItem.stock > 0));
                StyleButton(_detailActionBtn, UTKButton.Variant.Primary);
                return;
            }

            // 판매 모드 — 현재 _detailSellSlot 사용
            if (_detailSellSlot != null && _detailSellSlot.item != null)
            {
                var it = _detailSellSlot.item;
                _detailSlot.SetIcon(ItemIconDatabase.GetOrCreateIcon(it));
                _detailSlot.SetRank("");
                ClearSlotFrame(_detailSlot);   // [Figma] 상세 이미지는 슬롯 틀 없음
                _detailName.text = it.displayName;
                _detailCategory.text = $"{RarityAdjectiveKorean(it.rarity)} {CategoryKorean(it.category)} ({it.rarity.ToString().ToUpper()})";   // [Figma 74:4 정합]
                _detailCategory.style.color = new StyleColor(TierStripColor(it.rarity));
                _detailType.text = "";
                _detailDesc.text = it.description ?? "";
                int sp = CalculateSellPrice(it);
                ApplyStatValues(it);
                _detailActionBtn.text = sp > 0 ? $"판매하기 ({sp:N0}G)" : "판매 불가";   // [Figma 정합] "판매하기 (12,500G)"
                _detailActionBtn.SetEnabled(sp > 0);
                // [144차 P1-SHOP] 판매 모드 액션 버튼 색 수리(shop.md #45) — Figma 76:7 SellButton fill #58A6FF/1.0·텍스트
                //   #0B0E14로 구매 판본(74:4)과 동일. 판본 간 차이는 라벨만 교체(ShopVariantDifference 계약) —
                //   구 코드의 Danger(#F85149) 변이는 Figma에 없는 판본 초과 해석 → Primary(Accent)로 정합.
                StyleButton(_detailActionBtn, UTKButton.Variant.Primary);
                return;
            }

            // 빈 상태
            _detailSlot.SetIcon(null);
            _detailSlot.SetRank("");
            ClearSlotFrame(_detailSlot);
            _detailName.text = "아이템을 선택하세요";
            _detailCategory.text = "";
            _detailCategory.style.color = new StyleColor(UTKTheme.TextSub);
            _detailType.text = "";
            _detailDesc.text = "";
            _detailActionBtn.text = "구매";
            _detailActionBtn.SetEnabled(false);
            StyleButton(_detailActionBtn, UTKButton.Variant.Primary);
        }

        private void OnDetailAction()
        {
            if (_detailShopItem != null)
            {
                BuyItem(_detailShopItem);
            }
            else if (_detailSellSlot != null)
            {
                SellSlot(_detailSellSlot);
            }
        }


        // =====================================================================
        // 공개 진입점 — 루트에 부착 + 표시
        // =====================================================================
        /// <summary>상점 창을 UTK 루트 우측에 생성·표시. 열려 있으면 기존 인스턴스를 재사용(멱등).</summary>
        public static ShopWindowUTK Instance { get; private set; }

        public static ShopWindowUTK Open()
        {
            return Open(null);
        }

        /// <summary>[P30-D] 상점 창 표시 — 상점 위치(Vector3) 기반 노출.</summary>
        public static ShopWindowUTK Open(Vector3? shopPosition)
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null)
            {
                Debug.LogWarning("[ShopWindowUTK] UI Toolkit 루트 없음 — 부트스트랩 미완료.");
                return null;
            }

            if (Instance != null)
            {
                Instance.Show();
                Instance.RefreshBuyList();
                Instance.UpdateGoldDisplay();
            }
            else
            {
                var window = new ShopWindowUTK();
                Instance = window;
                // The shop is a full-canvas transparent host for the independently positioned panels.
                window.style.left = 0f;
                window.style.top = 0f;
                root.Add(window);
                window.Show();
            }

            return Instance;
        }

        // =====================================================================
        // UTKWindowBase 훅
        // =====================================================================
        protected override void OnWindowOpen()
        {
            Debug.Log("[ShopWindowUTK] 열림");
            _selectedBuyIndex = -1;
            RenderInventoryAndRefresh();
            UpdateGoldDisplay();
            SetStatus("");
        }

        protected override void OnWindowClosed()
        {
            Debug.Log("[ShopWindowUTK] 닫힘");
            if (Instance == this) Instance = null;
        }

        // =====================================================================
        // 상점 재고 초기화 (원본 InitializeShopInventory + RandomizeSeedStock 대응)
        // =====================================================================
        /// <summary>기본 상품으로 상점 재고 채움 (원본과 동일 게임 데이터 소스).</summary>
        public void InitializeShopInventory()
        {
            if (_shopInventory.Count != 0)
                return;

            _shopInventory.Add(new ShopItem { item = PlayerInventory.Herb_Red,    price = 10,  stock = 99, isRare = false });
            _shopInventory.Add(new ShopItem { item = PlayerInventory.Herb_Purple, price = 15,  stock = 50, isRare = false });
            _shopInventory.Add(new ShopItem { item = PlayerInventory.Herb_Yellow, price = 12,  stock = 75, isRare = false });
            _shopInventory.Add(new ShopItem { item = PlayerInventory.RabbitMeat,  price = 20,  stock = 30, isRare = false });

            _shopInventory.Add(new ShopItem { item = DishDatabase.GetItemData("토끼 허브 구이"),   price = 100, stock = 5, isRare = true });
            _shopInventory.Add(new ShopItem { item = DishDatabase.GetItemData("멧돼지 독구이"),   price = 150, stock = 3, isRare = true });
            _shopInventory.Add(new ShopItem { item = DishDatabase.GetItemData("늑대 환각 스튜"),   price = 200, stock = 2, isRare = true });

            _shopInventory.Add(new ShopItem { item = CreatePotionItem("은빛 회복제", "은빛 이끼 + 피어리", "지속 체력 재생 5 HP/s for 20s"), price = 300, stock = 2, isRare = true });
            _shopInventory.Add(new ShopItem { item = CreatePotionItem("독 엘리트",   "독나물 + 은빛 이끼", "중독 공격 +15 데미지 for 10s"),    price = 250, stock = 3, isRare = true });

            _shopInventory.Add(new ShopItem { item = PlayerInventory.SwordWood,    price = 80,  stock = 5, isRare = false });
            _shopInventory.Add(new ShopItem { item = PlayerInventory.SpearWood,    price = 100, stock = 3, isRare = false });
            _shopInventory.Add(new ShopItem { item = PlayerInventory.BowWood,      price = 120, stock = 3, isRare = false });
            _shopInventory.Add(new ShopItem { item = PlayerInventory.ClothArmor,   price = 50,  stock = 5, isRare = false });
            _shopInventory.Add(new ShopItem { item = PlayerInventory.LeatherArmor, price = 150, stock = 3, isRare = false });
            _shopInventory.Add(new ShopItem { item = PlayerInventory.Pickaxe,      price = 60,  stock = 3, isRare = false });
            _shopInventory.Add(new ShopItem { item = PlayerInventory.Axe,          price = 60,  stock = 3, isRare = false });
            _shopInventory.Add(new ShopItem { item = PlayerInventory.WateringCan,  price = 50,  stock = 3, isRare = false });   // [26차] 물뿌리개 — 내구도 없음, 물 저장량 20회(호수 충전)
            _shopInventory.Add(new ShopItem { item = PlayerInventory.FishingRodItem, price = 40,  stock = 5, isRare = false });   // [P29] 시스템 기준(fishing_rod)과 통일

            // Phase O7: 바드 악기 3종 — basePrice 150 고정가 상점 판매가 (재고 -1 무한 / 양수 유한)
            _shopInventory.Add(new ShopItem { item = InstrumentData.ToItemData(InstrumentData.GetProfile("instrument_bard_lute").Value),   price = 400, stock = -1, isRare = false });
            _shopInventory.Add(new ShopItem { item = InstrumentData.ToItemData(InstrumentData.GetProfile("instrument_flute_war").Value),    price = 350, stock = 3,  isRare = false });
            _shopInventory.Add(new ShopItem { item = InstrumentData.ToItemData(InstrumentData.GetProfile("instrument_drum_march").Value),   price = 350, stock = 3,  isRare = false });

            RandomizeSeedStock();
        }

        /// <summary>C9-27: 씨앗 랜덤 재고 재추첨 (원본과 동일 로직).</summary>
        public void RandomizeSeedStock()
        {
            _shopInventory.RemoveAll(s => s.item != null && s.item.id != null && s.item.id.StartsWith("herb_seed_"));

            var stocked = new List<string>();

            if (Random.value <= SEED_STOCK_CHANCE)
            {
                // Fisher–Yates 셔플 (foreach/while — for..in 금지 준수)
                var order = new List<int>(CommonSeeds.Length);
                for (int i = 0; i < CommonSeeds.Length; i++)
                    order.Add(i);
                for (int i = order.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    int tmp = order[i]; order[i] = order[j]; order[j] = tmp;
                }

                int kinds = Random.Range(1, 3);
                for (int k = 0; k < kinds; k++)
                {
                    var seed = CommonSeeds[order[k]];
                    _shopInventory.Add(new ShopItem
                    {
                        item = seed.item,
                        price = Random.Range(30, 51),
                        stock = Random.Range(3, 6),
                        isRare = seed.rare,
                    });
                    stocked.Add(seed.item.displayName);
                }
            }

            if (Random.value <= SEED_SILVER_CHANCE)
            {
                _shopInventory.Add(new ShopItem
                {
                    item = PlayerInventory.Seed_Silver,
                    price = 150,
                    stock = Random.Range(3, 6),
                    isRare = true,
                });
                stocked.Add(PlayerInventory.Seed_Silver.displayName);
            }

            if (stocked.Count > 0)
                Debug.Log($"[ShopWindowUTK] 🎲 씨앗 랜덤 재고 갱신 — 입고: {string.Join(", ", stocked)}");
            else
                Debug.Log("[ShopWindowUTK] 🎲 씨앗 랜덤 재고 갱신 — 이번 개장은 씨앗 입고 없음");
        }

        /// <summary>임시 물약 아이템 생성 (원본 CreatePotionItem 대응).</summary>
        private static PlayerInventory.ItemData CreatePotionItem(string displayName, string description, string effect)
        {
            return new PlayerInventory.ItemData
            {
                id = $"potion_{displayName.Replace(" ", "_")}",
                displayName = displayName,
                description = description,
                category = PlayerInventory.ItemCategory.Potion,
                maxStack = 99,
            };
        }

        // =====================================================================
        // 가격 계산 (복제 금지 — PlayerStats 화술 소스 직접 호출)
        // =====================================================================
        /// <summary>화술 할인이 적용된 구매 가격 — EconomyPricing 위임 (할인 밴드 ±20% 클램프).</summary>
        public int GetBuyPrice(ShopItem item)
        {
            if (item == null) return 0;
            float discount = PlayerStats.Instance?.BuyDiscount ?? 0f;
            return EconomyPricing.GetBuyPrice(item.price, discount);
        }

        /// <summary>판매 가격 — EconomyPricing 위임 (스프레드 40%, 산출 불가 시 0 = 판매 불가).</summary>
        public int CalculateSellPrice(PlayerInventory.ItemData item)
        {
            return EconomyPricing.GetSellPrice(item);
        }

        // =====================================================================
        // 탭/리뷰
        // =====================================================================
        private void SwitchTab(ShopTab tab)
        {
            _buyPanel.style.display = tab == ShopTab.Buy ? DisplayStyle.Flex : DisplayStyle.None;
            _sellPanel.style.display = tab == ShopTab.Sell ? DisplayStyle.Flex : DisplayStyle.None;

            _tabBuy.RemoveFromClassList("utk-btn--primary");
            _tabBuy.RemoveFromClassList("utk-btn--secondary");
            _tabBuy.RemoveFromClassList("utk-btn--danger");
            _tabSell.RemoveFromClassList("utk-btn--primary");
            _tabSell.RemoveFromClassList("utk-btn--secondary");
            _tabSell.RemoveFromClassList("utk-btn--danger");

            switch (tab)
            {
                case ShopTab.Buy:
                    _tabBuy.AddToClassList("utk-btn--primary");
                    _tabSell.AddToClassList("utk-btn--secondary");
                    break;
                case ShopTab.Sell:
                    _tabBuy.AddToClassList("utk-btn--secondary");
                    _tabSell.AddToClassList("utk-btn--primary");
                    break;
            }

            // [GitHub-dark] 탭 인라인 스타일 — 활성 탭 강조
            StyleTabButton(_tabBuy, tab == ShopTab.Buy, false);
            StyleTabButton(_tabSell, tab == ShopTab.Sell, false);

            RefreshBuyList();
            RefreshSellList();
        }

        // =====================================================================
        //  [Figma 정합] Store 카테고리 필터 — 6탭(전체/무기/방어구/소모품/재료/레시피)
        // =====================================================================

        /// <summary>카테고리 탭 선택 — 필터 변경 후 Store 목록만 갱신(Detail/판매 무관).</summary>
        private void SelectStoreCat(StoreCat cat)
        {
            _storeCat = cat;
            RefreshStoreCatTabs();
            RefreshBuyList();
        }

        /// <summary>카테고리 탭 활성 강조 — GitHub-dark(활성=액센트, 비활성=PanelSub).</summary>
        private void RefreshStoreCatTabs()
        {
            for (int i = 0; i < _storeCatTabs.Count; i++)
            {
                var btn = _storeCatTabs[i];
                if (btn == null) continue;
                bool active = (StoreCat)i == _storeCat;
                StyleStoreTabButton(btn, active);
            }
        }

        /// <summary>ShopItem이 현재 Store 카테고리 필터에 속하는지.</summary>
        private bool MatchesStoreCat(ShopItem shopItem)
        {
            if (shopItem == null || shopItem.item == null) return false;
            var cat = shopItem.item.category;
            switch (_storeCat)
            {
                case StoreCat.All: return true;
                case StoreCat.Weapon:
                    return cat == PlayerInventory.ItemCategory.Weapon
                        || cat == PlayerInventory.ItemCategory.Arrow
                        || cat == PlayerInventory.ItemCategory.Bomb;
                case StoreCat.Armor:
                    return cat == PlayerInventory.ItemCategory.Armor
                        || cat == PlayerInventory.ItemCategory.Accessory;
                case StoreCat.Consumable:
                    return cat == PlayerInventory.ItemCategory.Potion
                        || cat == PlayerInventory.ItemCategory.Food
                        || cat == PlayerInventory.ItemCategory.Herb
                        || cat == PlayerInventory.ItemCategory.Drug;
                case StoreCat.Material:
                    return cat == PlayerInventory.ItemCategory.Material
                        || cat == PlayerInventory.ItemCategory.Meat
                        || cat == PlayerInventory.ItemCategory.Quest;
                case StoreCat.Recipe:
                    // 게임에 Recipe 카테고리 없음 — 재료류 폴백 (빈 경우 하단 안내)
                    return cat == PlayerInventory.ItemCategory.Material;
                default: return true;
            }
        }

        private void RenderInventoryAndRefresh()
        {
            InitializeShopInventory();
            RefreshBuyList();
            RefreshSellList();
        }

        // =====================================================================
        // 구매 목록 빌드
        // =====================================================================
        private void RefreshBuyList()
        {
            _buyScroll.Clear();

            // [Milestone F] 비밀상점 활성 시 최상단에 전용 섹션
            if (SecretShopSystem.Active)
            {
                var head = new Label("🔮 비밀상점 — 은밀한 상인");
                head.style.fontSize = 16.8f;
                head.style.color = UTKTheme.Gold;   // [GitHub-dark] 비밀/희귀=금색
                head.style.marginBottom = 6f;
                head.style.marginTop = 4f;
                _buyScroll.Add(head);

                bool anySecret = false;
                foreach (var s in SecretShopSystem.Stock)
                {
                    var item = PlayerInventory.GetItemById(s.itemId);
                    if (item == null) continue;
                    anySecret = true;
                    _buyScroll.Add(BuildSecretRow(item, s.price));
                }
                if (!anySecret)
                {
                    _buyScroll.Add(new Label("판매 중인 비밀 아이템이 없습니다.") { style = { fontSize = 16f, color = UTKTheme.TextSub } });
                }

                var spacer = new VisualElement { style = { height = 12f } };
                _buyScroll.Add(spacer);
            }

            // [Figma 정합] 카테고리 필터 적용 — 선택 카테고리 상품만 표시. 멱등(빈재고 대비).
            bool anyInCat = false;
            int catIndex = 0;
            foreach (var shopItem in _shopInventory)
            {
                if (!MatchesStoreCat(shopItem))
                    continue;
                anyInCat = true;
                _buyScroll.Add(BuildBuyRow(shopItem, catIndex));
                catIndex++;
            }

            if (!anyInCat)
            {
                string catName = _storeCat == StoreCat.All ? "" : "/" + StoreCatLabel(_storeCat);
                _buyScroll.Add(new Label($"이 카테고리에 판매 중인 아이템이 없습니다{catName}.") { style = { fontSize = 15f, color = UTKTheme.TextSub } });
            }

            _selectedBuyIndex = -1;
        }

        /// <summary>Store 카테고리 표시명 (빈 안내용).</summary>
        private static string StoreCatLabel(StoreCat cat)
        {
            switch (cat)
            {
                case StoreCat.All: return "전체";
                case StoreCat.Weapon: return "무기";
                case StoreCat.Armor: return "방어구";
                case StoreCat.Consumable: return "소모품";
                case StoreCat.Material: return "재료";
                case StoreCat.Recipe: return "레시피";
                default: return "";
            }
        }

        // [Milestone F] 비밀상점 전용 행 — SecretShopSystem.TryBuy 직접 호출
        private VisualElement BuildSecretRow(PlayerInventory.ItemData item, int price)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 6f;
            row.style.paddingTop = 4f;
            row.style.paddingBottom = 4f;
            row.style.paddingLeft = 4f;
            row.style.paddingRight = 4f;
            // [GitHub-dark] 비밀상점 행 — 금색 링 + 보조 패널 bg + r6
            StyleListRow(row);
            row.style.borderTopColor = new StyleColor(UTKTheme.Gold);
            row.style.borderBottomColor = new StyleColor(UTKTheme.Gold);
            row.style.borderLeftColor = new StyleColor(UTKTheme.Gold);
            row.style.borderRightColor = new StyleColor(UTKTheme.Gold);

            var slot = new UTKSlot();
            slot.style.width = 64f;
            slot.style.height = 64f;
            slot.style.marginRight = 8f;
            slot.SetIcon(ItemIconDatabase.GetOrCreateIcon(item));
            slot.SetRank("unique");
            StyleShopSlot(slot, UTKTheme.Gold);   // [GitHub-dark] 비밀 아이템 슬롯 금색 링
            row.Add(slot);

            var info = new VisualElement();
            info.style.flexGrow = 1f;

            var nameLabel = new Label(item.displayName);
            nameLabel.style.fontSize = 16.8f;
            nameLabel.style.color = UTKTheme.Gold;   // [GitHub-dark] 비밀 아이템명=금색
            info.Add(nameLabel);

            if (!string.IsNullOrEmpty(item.description))
            {
                var desc = new Label(item.description);
                desc.style.fontSize = 13.2f;
                desc.style.color = UTKTheme.TextSub;
                desc.style.whiteSpace = WhiteSpace.Normal;
                info.Add(desc);
            }

            var priceLabel = new Label($"가격: {price}G");
            priceLabel.style.fontSize = 14.4f;
            priceLabel.style.color = UTKTheme.Accent;   // [GitHub-dark] 가격=액센트
            info.Add(priceLabel);

            row.Add(info);

            bool canAfford = (PlayerStats.Instance?.Gold ?? 0) >= price;
            var buyBtn = UTKButton.Create("구매", () =>
            {
                if (SecretShopSystem.TryBuy(item.id)) SetStatus($"{item.displayName} 구매 완료!");
                else SetStatus("비밀상점 구매 실패 (골드 부족)");
                UpdateGoldDisplay();
                RefreshBuyList();
            }, UTKButton.Variant.Primary);
            buyBtn.SetEnabled(canAfford);
            StyleButton(buyBtn, UTKButton.Variant.Primary);
            row.Add(buyBtn);

            UTKWindowBase.ApplyUIToolkitFont(row);
            return row;
        }

        // [Figma 74:4 전수 실측] 상점 Store 셀 = TierStrip(3.6) + 아이콘 38.4 중앙 + Count(우측) + PriceTag(하단 16.8 #1F242C).
        // 클릭 → Detail 패널(구매 버튼이 담당). hover/선택 = bg #30363D/0.8 + stroke #58A6FF 1.8(Figma row1col1 상태).
        private VisualElement BuildBuyRow(ShopItem shopItem, int index)
        {
            var slotBox = BuildShopCell(shopItem?.item);
            slotBox.RegisterCallback<PointerDownEvent>(_ =>
            {
                _selectedBuyIndex = index;
                HighlightCell(slotBox, true);
                ShowDetail(shopItem, true);
            });
            SetCellCount(slotBox, shopItem.stock > 0 ? shopItem.stock : 0);   // [Figma] Store 셀 Count=재고
            int buyPrice = GetBuyPrice(shopItem);
            AddPriceTag(slotBox, $"{buyPrice:N0}G");
            UTKWindowBase.ApplyUIToolkitFont(slotBox);
            return slotBox;
        }

        /// <summary>[Figma 74:4] 상점/창고 공용 셀 81.6×81.6 — bg #21262D/0.6 + stroke #30363D 1.2 + r9.6 +
        /// TierStrip 3.6(등급색) + 아이콘 38.4(중앙 21.6) + Count(우측 하단). PriceTag는 호출처가 추가.</summary>
        private VisualElement BuildShopCell(PlayerInventory.ItemData item)
        {
            var slotBox = new VisualElement();
            slotBox.style.width = StoreGridCellSize;   // Figma ItemSlot 81.6px
            slotBox.style.height = StoreGridCellSize;
            slotBox.style.marginRight = 9.6f;   // [Figma] 셀 피치 91.2 = 81.6+9.6
            slotBox.style.marginBottom = 9.6f;
            slotBox.style.backgroundColor = new StyleColor(ShopSlotBg);
            slotBox.style.borderTopWidth = slotBox.style.borderBottomWidth =
                slotBox.style.borderLeftWidth = slotBox.style.borderRightWidth = 1.2f;
            slotBox.style.borderTopColor = slotBox.style.borderBottomColor =
                slotBox.style.borderLeftColor = slotBox.style.borderRightColor = new StyleColor(ShopStroke);
            slotBox.style.borderTopLeftRadius = slotBox.style.borderTopRightRadius =
                slotBox.style.borderBottomLeftRadius = slotBox.style.borderBottomRightRadius = 9.6f;
            slotBox.style.overflow = Overflow.Hidden;   // [Figma] 하단 PriceTag가 셀 라운드에 클립
            slotBox.RegisterCallback<PointerEnterEvent>(_ => HighlightCell(slotBox, true));
            slotBox.RegisterCallback<PointerLeaveEvent>(_ => HighlightCell(slotBox, false));

            // TierStrip — 셀 상단 풀폭 3.6 (등급 색)
            var tier = new VisualElement { name = "TierStrip" };
            tier.style.position = Position.Absolute;
            tier.style.left = 0f; tier.style.top = 0f;
            tier.style.width = StoreGridCellSize;
            tier.style.height = 3.6f;
            tier.style.backgroundColor = new StyleColor(item != null ? TierStripColor(item.rarity) : UTKTheme.TextSub);
            tier.pickingMode = PickingMode.Ignore;
            slotBox.Add(tier);

            // 아이콘 38.4 — 셀 중앙(21.6,21.6), 슬롯 틀 없음
            var slot = new UTKSlot();
            slot.style.position = Position.Absolute;
            slot.style.left = 21.6f; slot.style.top = 21.6f;
            slot.style.width = 38.4f;
            slot.style.height = 38.4f;
            if (item != null) slot.SetIcon(ItemIconDatabase.GetOrCreateIcon(item));
            slot.SetRank("");
            ClearSlotFrame(slot);
            slot.pickingMode = PickingMode.Ignore;
            slotBox.Add(slot);

            // Count — 우측 하단(fs12 fw500 #8B949E, 우측 여백 6.4, y+44.4)
            var countLabel = new Label("") { name = "CellCount" };
            countLabel.style.position = Position.Absolute;
            countLabel.style.right = 6.4f;
            countLabel.style.top = 44.4f;
            countLabel.style.fontSize = 12f;
            countLabel.style.color = UTKTheme.TextSub;
            countLabel.pickingMode = PickingMode.Ignore;
            slotBox.Add(countLabel);
            return slotBox;
        }

        /// <summary>[Figma] PriceTag — 셀 하단 풀폭 16.8 #1F242C, 라벨 fs10.8 fw700 #E3B341 중앙.</summary>
        private static void AddPriceTag(VisualElement cell, string priceText)
        {
            var tag = new VisualElement { name = "PriceTag" };
            tag.style.position = Position.Absolute;
            tag.style.left = 0f; tag.style.bottom = 0f;
            tag.style.width = StoreGridCellSize;
            tag.style.height = 16.8f;
            tag.style.backgroundColor = new StyleColor(ShopPriceTagBg);
            tag.style.alignItems = Align.Center;
            tag.style.justifyContent = Justify.Center;
            tag.pickingMode = PickingMode.Ignore;
            var priceLabel = new Label(priceText ?? "");
            priceLabel.style.fontSize = 10.8f;
            priceLabel.style.color = ShopGold;
            priceLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            priceLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            tag.Add(priceLabel);
            cell.Add(tag);
        }

        /// <summary>[Figma row1col1 상태] 셀 강조 — bg #30363D/0.8 + stroke #58A6FF 1.8 / 해제 = 원복.</summary>
        private static void HighlightCell(VisualElement cell, bool on)
        {
            if (cell == null) return;
            Color bg = on ? ShopSlotHoverBg : ShopSlotBg;
            Color line = on ? ShopAccent : ShopStroke;
            float bw = on ? 1.8f : 1.2f;
            cell.style.backgroundColor = new StyleColor(bg);
            cell.style.borderTopWidth = cell.style.borderBottomWidth =
                cell.style.borderLeftWidth = cell.style.borderRightWidth = bw;
            cell.style.borderTopColor = cell.style.borderBottomColor =
                cell.style.borderLeftColor = cell.style.borderRightColor = new StyleColor(line);
        }

        /// <summary>셀 Count 갱신 헬퍼 — BuildShopCell이 만든 CellCount 라벨.</summary>
        private static void SetCellCount(VisualElement cell, int count)
        {
            var label = cell?.Q<Label>("CellCount");
            if (label == null) return;
            label.text = count > 0 ? count.ToString() : "";
            label.style.display = count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // =====================================================================
        // 판매 목록 빌드 (플레이어 인벤토리)
        // =====================================================================
        private void RefreshSellList()
        {
            _sellScroll.Clear();

            var slots = PlayerInventory.Instance != null ? PlayerInventory.Instance.GetAllSlots() : null;
            if (slots == null)
            {
                _sellScroll.Add(new Label("인벤토리를 불러올 수 없습니다.") { style = { fontSize = 16f, color = UTKTheme.TextSub } });
                return;
            }

            // [142차 G] 가방 = 80칸 고정 그리드(5열 × 16행, Figma 가시 5×6 + 스크롤) — 빈 칸도 셀로 렌더.
            //   기존은 점유 슬롯만 행 추가 → 빈 가방 = 백지("그리드 표현이 안된다" 사용자 보고의 원인).
            bool any = false;
            int usedSlots = 0;
            const int BagCapacity = 80;
            for (int i = 0; i < BagCapacity; i++)
            {
                var slot = i < slots.Length ? slots[i] : null;
                if (slot != null && slot.item != null && slot.count > 0)
                {
                    any = true;
                    usedSlots++;
                    _sellScroll.Add(BuildSellRow(slot));
                }
                else
                {
                    _sellScroll.Add(BuildEmptyBagCell());
                }
            }
            // [Figma 정합] StorageFooter 갱신 — 칸 수 텍스트 + ProgressBar fill(사용률 — 무게 시스템 미구현 대용)
            // [144차 P1-SHOP D7 기록] Figma WeightValue 표기 '42.6 / 80.0 kg'(shop.md #60) vs 코드 'N / 80 칸' —
            //   무게(kg) 실데이터가 게임에 없어 목업 생성 금지(데이터 위조 금지 계약) → D7 확정(2026-10-31)의
            //   "실데이터 부재 항목은 슬롯 표기 유지"에 따라 '칸' 표기 유지. kg 연동은 실무게 시스템 구현 후.
            if (_storageCountLabel != null)
                _storageCountLabel.text = $"{usedSlots} / 80 칸";
            if (_weightFill != null)
                _weightFill.style.width = Mathf.Clamp(usedSlots / 80f, 0f, 1f) * (InventoryBounds.width - 48f);

            if (!any)
                _sellScroll.Add(new Label("판매할 아이템이 없습니다.") { style = { fontSize = 16f, color = UTKTheme.TextSub } });
        }

        /// <summary>[142차 G] 빈 가방 셀 — Figma ItemSlotEmpty는 프레임(fill/stroke/r)만 있고 자식 0(74:4·76:7 각 38칸 전수 확인).
        /// [144차 P1-SHOP P0 수리(shop.md #56)] 구 구현은 회색 TierStrip(81.6×3.6)을 부가 렌더 — Figma에 없는 요소였음.
        /// Phase 0 V3 UTKSlot.SetEmpty(true)(프레임 모드 — 아이콘/카운트/TierStrip 전부 숨김)로 교체, 수동 TierStrip 생성 코드 삭제.
        /// 아이콘·카운트·클릭 없음. Figma Storage 빈 셀 계약.</summary>
        private VisualElement BuildEmptyBagCell()
        {
            var slotBox = new UTKSlot();
            slotBox.style.width = StoreGridCellSize;
            slotBox.style.height = StoreGridCellSize;
            slotBox.style.marginRight = 9.6f;
            slotBox.style.marginBottom = 9.6f;
            slotBox.style.backgroundColor = new StyleColor(ShopSlotBg);
            slotBox.style.borderTopWidth = slotBox.style.borderBottomWidth =
                slotBox.style.borderLeftWidth = slotBox.style.borderRightWidth = 1.2f;
            slotBox.style.borderTopColor = slotBox.style.borderBottomColor =
                slotBox.style.borderLeftColor = slotBox.style.borderRightColor = new StyleColor(ShopStroke);
            slotBox.style.borderTopLeftRadius = slotBox.style.borderTopRightRadius =
                slotBox.style.borderBottomLeftRadius = slotBox.style.borderBottomRightRadius = 9.6f;
            slotBox.pickingMode = PickingMode.Ignore;   // 빈 셀 — 클릭 흡수 금지(패널 드래그 보호)

            // [144차 P1-SHOP P0] 프레임만 렌더 — SetEmpty(true)는 _iconHost/_countLabel/TierStrip을 모두 숨긴다
            // (UTKSlot.RefreshOptionals: 빈셀 모드에서 TierStrip 표시 억제 — ItemSlotEmpty 자식 0 진실).
            slotBox.SetEmpty(true);
            return slotBox;
        }

        // [Figma 74:4 전수 실측] Storage 셀 = TierStrip + 아이콘 + Count(PriceTag 없음 — Figma Storage 슬롯 계약).
        // 클릭 → Detail 패널(판매 버튼이 담당).
        private VisualElement BuildSellRow(PlayerInventory.ItemSlot slot)
        {
            var slotBox = BuildShopCell(slot.item);
            SetCellCount(slotBox, slot.count);
            slotBox.RegisterCallback<PointerDownEvent>(_ => { _detailSellSlot = slot; ShowDetail(null, false); });
            UTKWindowBase.ApplyUIToolkitFont(slotBox);
            return slotBox;
        }

        // =====================================================================
        // 거래 처리 (원본 Buy/Sell 로직 대응 — 동일 소스 직접 호출)
        // =====================================================================
        /// <summary>주어진 인덱스의 상점 아이템 구매 (외부 테스트용 공개).</summary>
        public bool BuyAtIndex(int index)
        {
            if (index < 0 || index >= _shopInventory.Count) return false;
            return BuyItem(_shopInventory[index]);
        }

        /// <summary>상점 아이템 구매. 성공 여부 반환.</summary>
        public bool BuyItem(ShopItem item)
        {
            if (item == null || item.item == null)
            {
                Debug.LogError("[ShopWindowUTK] 구매 실패: 아이템 데이터가 없습니다.");
                return false;
            }

            int price = GetBuyPrice(item);
            if (!(PlayerStats.Instance?.SpendGold(price, "shop_purchase") ?? false))
            {
                SetStatus("골드가 부족합니다!");
                return false;
            }

            if (item.stock > 0)
                item.stock--;

            if (PlayerInventory.Instance.AddItem(item.item, 1))
            {
                Debug.Log($"[ShopWindowUTK] 구매 성공: {item.item.displayName}");
                RefreshBuyListAndGold();
                SetStatus($"{item.item.displayName} 구매 완료");
                return true;
            }

            // 인벤토리 가득 참 → 골드 환불
            PlayerStats.Instance?.AddGold(price, "shop_refund");
            Debug.LogWarning("[ShopWindowUTK] 인벤토리 가득 참! 구매 취소.");
            RefreshBuyListAndGold();
            SetStatus("인벤토리가 가득 찼습니다 (구매 취소)");
            return false;
        }

        /// <summary>주어진 슬롯 판매 (플레이어 인벤토리).</summary>
        public bool SellSlot(PlayerInventory.ItemSlot slot)
        {
            if (slot == null || slot.item == null || PlayerInventory.Instance == null)
            {
                SetStatus("판매할 아이템이 없습니다.");
                return false;
            }

            int sellPrice = CalculateSellPrice(slot.item);
            if (sellPrice <= 0)
            {
                // 0G 판매 방지 (무가격/산출 불가 아이템)
                SetStatus("판매 불가");
                return false;
            }

            bool removed = PlayerInventory.Instance.RemoveItem(slot.item.id, 1);
            if (!removed)
            {
                Debug.LogWarning("[ShopWindowUTK] 판매 실패: 아이템 제거 불가.");
                return false;
            }

            PlayerStats.Instance?.AddGold(sellPrice, "shop_sale");
            Debug.Log($"[ShopWindowUTK] 판매 성공: {slot.item.displayName} → {sellPrice}G");
            RefreshBuyListAndGold();
            SetStatus($"{slot.item.displayName} 판매 → {sellPrice}G");
            return true;
        }

        private void RefreshBuyListAndGold()
        {
            RefreshBuyList();
            RefreshSellList();
            UpdateGoldDisplay();
        }

        // =====================================================================
        // 골드/상태 표시
        // =====================================================================
        private void UpdateGoldDisplay()
        {
            int gold = PlayerStats.Instance?.Gold ?? 0;
            _goldLabel.text = $"{gold:N0} G";   // [Figma 정합] "128,450 G" 형식
        }

        private void SetStatus(string msg)
        {
            _statusLabel.text = msg ?? "";
        }
    }
}