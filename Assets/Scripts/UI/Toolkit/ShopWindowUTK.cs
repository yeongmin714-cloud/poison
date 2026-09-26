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
        // [밀매는 SmuggleWindowUTK로 분리] Buy/Sell 2-way 탭 상태
        private enum ShopTab { Buy, Sell }

        // [Figma 정합] Store 카테고리 필터 탭 — 6탭(전체/무기/방어구/소모품/재료/레시피)
        private enum StoreCat { All, Weapon, Armor, Consumable, Material, Recipe }

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

        // =====================================================================
        //  [Figma GitHub-dark 리스타일] 이 창 한정 인라인 오버라이드 — 기능 무수정, 시각 전용.
        //  Theme.uss / 공용 UTKButton·UTKSlot·UTKWindowBase·타 UTK 창은 절대 수정하지 않는다.
        //  우드 배경 이미지 제거 → 다크 #161B22 패널 + 보조 #21262D + 스트로크 #2E343D.
        //  =====================================================================
        private static class GitHubDark
        {
            public static readonly Color BgBase   = Hex(0x0B0E14);   // 최배경 — 슬롯 인셋 바닥
            public static readonly Color Panel    = Hex(0x161B22);   // 창 본체 패널
            public static readonly Color PanelSub = Hex(0x21262D);   // 보조 패널(타이틀바/버튼/행)
            public static readonly Color Accent   = Hex(0x58A6FF);   // 강조(액센트)
            public static readonly Color Gold     = Hex(0xE3B341);   // 희귀/활성/골드(가격·잔액)
            public static readonly Color TextMain = Hex(0xF0F6FC);   // 기본 텍스트
            public static readonly Color TextSub  = Hex(0x8B949E);   // 보조 텍스트
            public static readonly Color Stroke   = Hex(0x2E343D);   // 테두리/행 구분선
            public static readonly Color Danger   = Hex(0xF85149);   // danger 버튼(GitHub-dark danger 토큰)

            private static Color Hex(uint rgb) =>
                new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);
        }

        private static Color Hex(uint rgb) =>
            new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);

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
                    baseBg = GitHubDark.Accent; hoverBg = Hex(0x79C0FF); textColor = GitHubDark.BgBase; break;
                case UTKButton.Variant.Danger:
                    baseBg = GitHubDark.Danger; hoverBg = Hex(0xDA3633); textColor = GitHubDark.TextMain; break;
                default:
                    baseBg = GitHubDark.PanelSub; hoverBg = GitHubDark.Stroke; textColor = GitHubDark.TextMain; break;
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
            Color baseBg = active ? (danger ? GitHubDark.Danger : GitHubDark.Accent) : GitHubDark.PanelSub;
            Color line   = active ? baseBg : GitHubDark.Stroke;
            tab.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            tab.style.backgroundColor = baseBg;
            tab.style.color = active && !danger ? GitHubDark.BgBase : GitHubDark.TextMain;
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
            row.style.backgroundColor = GitHubDark.PanelSub;
            row.style.borderTopWidth = row.style.borderBottomWidth = row.style.borderLeftWidth = row.style.borderRightWidth = 1f;
            row.style.borderTopColor = row.style.borderBottomColor = row.style.borderLeftColor = row.style.borderRightColor = new StyleColor(GitHubDark.Stroke);
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
            slot.style.backgroundColor = GitHubDark.BgBase;
            slot.style.borderTopWidth = slot.style.borderBottomWidth = slot.style.borderLeftWidth = slot.style.borderRightWidth = 1f;
            slot.style.borderTopColor = slot.style.borderBottomColor = slot.style.borderLeftColor = slot.style.borderRightColor = new StyleColor(ring);
            slot.style.borderTopLeftRadius = 6f;
            slot.style.borderTopRightRadius = 6f;
            slot.style.borderBottomLeftRadius = 6f;
            slot.style.borderBottomRightRadius = 6f;
            var countLabel = slot.Q<Label>("Count");
            if (countLabel != null) countLabel.style.color = GitHubDark.TextMain;
        }

        /// <summary>GitHub-dark 레어도 링 — uncommon/rare=액센트, epic=프라이머 퍼플, 전설/유니크=금색.</summary>
        private static Color RankRing(int rarityIndex)
        {
            switch (rarityIndex)
            {
                case 1:
                case 2: return GitHubDark.Accent;
                case 3: return Hex(0xA371F7);
                case 4:
                case 5: return GitHubDark.Gold;
                case 0: return GitHubDark.TextSub;
                default: return GitHubDark.Stroke;
            }
        }

        /// <summary>창 크롬(본체/타이틀바/닫기버튼) GitHub-dark 리스타일 — 생성 시 1회.</summary>
        private void ApplyGitHubDarkStyle()
        {
            // 창 본체: bg_window.png/브론즈 베벨 2px → 다크 패널 + 1px 스트로크 + r8 (이 창에서만)
            style.backgroundColor = GitHubDark.Panel;
            style.backgroundImage = new StyleBackground(StyleKeyword.None);
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 1f;
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = GitHubDark.Stroke;
            style.borderTopLeftRadius = 8f;
            style.borderTopRightRadius = 8f;
            style.borderBottomLeftRadius = 8f;
            style.borderBottomRightRadius = 8f;   // 메인 반경 r8
            style.color = GitHubDark.TextMain;

            // 타이틀 바: 보조 패널 + 하단 1px 스트로크 (상단 코너 r8 — 창 클리핑 정합)
            var titleBar = this.Q("TitleBar");
            if (titleBar != null)
            {
                titleBar.style.backgroundColor = GitHubDark.PanelSub;
                titleBar.style.borderTopLeftRadius = 8f;
                titleBar.style.borderTopRightRadius = 8f;
                titleBar.style.borderBottomWidth = 1f;
                titleBar.style.borderBottomColor = GitHubDark.Stroke;
            }
            if (_titleLabel != null)
                _titleLabel.style.color = GitHubDark.TextMain;

            // 닫기 버튼: 보조 패널 바탕 + r4(작은배지)
            var closeBtn = this.Q<Button>("CloseButton");
            if (closeBtn != null)
            {
                closeBtn.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                closeBtn.style.backgroundColor = GitHubDark.PanelSub;
                closeBtn.style.borderTopWidth = closeBtn.style.borderBottomWidth = closeBtn.style.borderLeftWidth = closeBtn.style.borderRightWidth = 0f;
                closeBtn.style.borderTopColor = closeBtn.style.borderBottomColor = closeBtn.style.borderLeftColor = closeBtn.style.borderRightColor = new StyleColor(GitHubDark.PanelSub);
                closeBtn.style.borderTopLeftRadius = 4f;
                closeBtn.style.borderTopRightRadius = 4f;
                closeBtn.style.borderBottomLeftRadius = 4f;
                closeBtn.style.borderBottomRightRadius = 4f;
                closeBtn.style.color = GitHubDark.TextMain;
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
        public ShopWindowUTK() : base("🏪 상점", new Vector2(1120f, 620f))
        {
            // [Figma GitHub-dark 리스타일] 다크 창 크롬 — 시각 전용, 기능 경로 무관(테스트 범위: 이 창 한정)
            ApplyGitHubDarkStyle();

            // ── 공용 상단 바 (골드 + 탭) ──
            var topBar = new VisualElement();
            topBar.style.flexDirection = FlexDirection.Row;
            topBar.style.alignItems = Align.Center;
            topBar.style.marginBottom = 8f;
            Content.Add(topBar);

            _goldLabel = new Label("골드: 0");
            _goldLabel.style.fontSize = 20f;
            _goldLabel.style.color = GitHubDark.Gold;
            _goldLabel.style.flexGrow = 1f;
            _goldLabel.AddToClassList("utk-title-label");
            topBar.Add(_goldLabel);

            var tabRow = new VisualElement();
            tabRow.style.flexDirection = FlexDirection.Row;
            tabRow.style.marginBottom = 6f;
            topBar.Add(tabRow);

            _tabBuy = UTKButton.Create("구매", () => SwitchTab(ShopTab.Buy), UTKButton.Variant.Primary);
            _tabSell = UTKButton.Create("판매", () => SwitchTab(ShopTab.Sell), UTKButton.Variant.Secondary);
            _tabBuy.style.flexGrow = 1f;
            _tabSell.style.flexGrow = 1f;
            tabRow.Add(_tabBuy);
            tabRow.Add(_tabSell);

            // ── 3열 본체 (Figma Store | Detail | Sell) ──
            var columns = new VisualElement();
            columns.style.flexDirection = FlexDirection.Row;
            columns.style.flexGrow = 1f;
            Content.Add(columns);

            // [좌] Store 패널 — 구매 목록
            var storeCol = new VisualElement();
            storeCol.style.flexGrow = 1f;
            storeCol.style.width = Length.Percent(44f);
            storeCol.style.marginRight = 8f;
            columns.Add(storeCol);
            var storeTitle = new Label("── 상점 재고 ──");
            storeTitle.style.fontSize = 14f;
            storeTitle.style.color = GitHubDark.TextSub;
            storeCol.Add(storeTitle);

            // [Figma 정합] Store 카테고리 필터 — 6탭(전체/무기/방어구/소모품/재료/레시피) row+wrap
            var storeCatRow = new VisualElement();
            storeCatRow.style.flexDirection = FlexDirection.Row;
            storeCatRow.style.flexWrap = Wrap.Wrap;
            storeCatRow.style.marginTop = 4f;
            storeCatRow.style.marginBottom = 6f;
            storeCol.Add(storeCatRow);

            string[] catLabels = { "전체", "무기", "방어구", "소모품", "재료", "레시피" };
            for (int ci = 0; ci < catLabels.Length; ci++)
            {
                var cat = (StoreCat)ci;
                var catBtn = UTKButton.Create(catLabels[ci], () => SelectStoreCat(cat), UTKButton.Variant.Secondary);
                catBtn.style.flexGrow = 1f;
                catBtn.style.height = 26f;
                catBtn.style.marginRight = 3f;
                catBtn.style.marginBottom = 3f;
                _storeCatTabs.Add(catBtn);
                storeCatRow.Add(catBtn);
            }
            RefreshStoreCatTabs();   // 초기 활성(전체) 강조

            _buyPanel = new VisualElement();
            _buyPanel.style.flexGrow = 1f;
            _buyScroll = new ScrollView();
            _buyScroll.style.flexGrow = 1f;
            // [Figma 정합] Store 패널 = 5열 그리드 — 스크롤 콘텐츠를 row+wrap 격자로.
            _buyScroll.contentContainer.style.flexDirection = FlexDirection.Row;
            _buyScroll.contentContainer.style.flexWrap = Wrap.Wrap;
            _buyScroll.contentContainer.style.alignContent = Align.FlexStart;
            _buyPanel.Add(_buyScroll);
            storeCol.Add(_buyPanel);

            // [중앙] Detail 패널 — 선택 아이템 상세 + 액션
            var detailCol = new VisualElement();
            detailCol.style.flexGrow = 1f;
            detailCol.style.width = Length.Percent(28f);
            detailCol.style.marginRight = 8f;
            columns.Add(detailCol);
            BuildDetailPanel(detailCol);

            // [우] Sell/인벤토리 패널 + 밀매 패널 (탭으로 전환)
            var sellCol = new VisualElement();
            sellCol.style.flexGrow = 1f;
            sellCol.style.width = Length.Percent(28f);
            columns.Add(sellCol);
            var sellTitle = new Label("── 내 인벤토리 ──");
            sellTitle.style.fontSize = 14f;
            sellTitle.style.color = GitHubDark.TextSub;
            sellCol.Add(sellTitle);

            _sellPanel = new VisualElement();
            _sellPanel.style.flexGrow = 1f;
            _sellScroll = new ScrollView();
            _sellScroll.style.flexGrow = 1f;
            // [Figma 정합] 판매/인벤 패널 = 5열 그리드
            _sellScroll.contentContainer.style.flexDirection = FlexDirection.Row;
            _sellScroll.contentContainer.style.flexWrap = Wrap.Wrap;
            _sellScroll.contentContainer.style.alignContent = Align.FlexStart;
            _sellPanel.Add(_sellScroll);
            sellCol.Add(_sellPanel);

            // ── 상태 라벨 ──
            _statusLabel = new Label("");
            _statusLabel.style.fontSize = 14f;
            _statusLabel.style.color = GitHubDark.TextSub;
            _statusLabel.style.minHeight = 20f;
            _statusLabel.style.marginTop = 6f;
            Content.Add(_statusLabel);

            SwitchTab(ShopTab.Buy);
            ShowDetail(null, true);
        }

        /// <summary>[Figma Detail 패널] 중앙 컬럼 — 선택 아이템 상세 + 구매/판매 버튼.</summary>
        private UTKSlot _detailSlot;
        private Label _detailName;
        private Label _detailDesc;
        private Label _detailPrice;
        private Button _detailActionBtn;
        private ShopItem _detailShopItem;      // 구매 대상 (null = 판매/비활성)
        private PlayerInventory.ItemSlot _detailSellSlot = null;   // 판매 대상

        private void BuildDetailPanel(VisualElement parent)
        {
            var title = new Label("── 아이템 정보 ──");
            title.style.fontSize = 14f;
            title.style.color = GitHubDark.TextSub;
            parent.Add(title);

            var card = new VisualElement();
            card.style.flexGrow = 1f;
            card.style.backgroundColor = GitHubDark.PanelSub;
            card.style.borderTopWidth = card.style.borderBottomWidth = card.style.borderLeftWidth = card.style.borderRightWidth = 1f;
            card.style.borderTopColor = card.style.borderBottomColor = card.style.borderLeftColor = card.style.borderRightColor = new StyleColor(GitHubDark.Stroke);
            card.style.borderTopLeftRadius = 8f; card.style.borderTopRightRadius = 8f;
            card.style.borderBottomLeftRadius = 8f; card.style.borderBottomRightRadius = 8f;
            card.style.paddingTop = 12f; card.style.paddingBottom = 12f;
            card.style.paddingLeft = 12f; card.style.paddingRight = 12f;
            parent.Add(card);

            _detailSlot = new UTKSlot();
            _detailSlot.style.width = 96f;
            _detailSlot.style.height = 96f;
            _detailSlot.style.alignSelf = Align.Center;
            _detailSlot.style.marginBottom = 8f;
            card.Add(_detailSlot);

            _detailName = new Label("아이템을 선택하세요");
            _detailName.style.fontSize = 18f;
            _detailName.style.color = GitHubDark.TextMain;
            _detailName.style.unityTextAlign = TextAnchor.MiddleCenter;
            _detailName.style.whiteSpace = WhiteSpace.Normal;
            card.Add(_detailName);

            _detailDesc = new Label("");
            _detailDesc.style.fontSize = 13f;
            _detailDesc.style.color = GitHubDark.TextSub;
            _detailDesc.style.whiteSpace = WhiteSpace.Normal;
            _detailDesc.style.marginTop = 6f;
            card.Add(_detailDesc);

            _detailPrice = new Label("");
            _detailPrice.style.fontSize = 15f;
            _detailPrice.style.color = GitHubDark.Accent;
            _detailPrice.style.marginTop = 8f;
            card.Add(_detailPrice);

            _detailActionBtn = UTKButton.Create("구매", () => OnDetailAction(), UTKButton.Variant.Primary);
            _detailActionBtn.style.marginTop = 10f;
            StyleButton(_detailActionBtn, UTKButton.Variant.Primary);
            card.Add(_detailActionBtn);
        }

        /// <summary>Detail 패널 갱신. isBuy=true이면 상점 아이템, false이면 판매(인벤) 아이템 상세.</summary>
        private void ShowDetail(ShopItem shopItem, bool isBuy)
        {
            _detailShopItem = isBuy ? shopItem : null;
            _detailSellSlot = isBuy ? null : _detailSellSlot;

            if (isBuy && shopItem != null && shopItem.item != null)
            {
                _detailSlot.SetIcon(ItemIconDatabase.GetOrCreateIcon(shopItem.item));
                _detailSlot.SetRank(shopItem.isRare ? "unique" : "common");
                StyleShopSlot(_detailSlot, shopItem.item != null ? RankRing((int)shopItem.item.rarity) : GitHubDark.Stroke);
                _detailName.text = shopItem.item.displayName;
                _detailDesc.text = shopItem.item.description ?? "";
                int bp = GetBuyPrice(shopItem);
                string st = shopItem.stock == -1 ? "무한" : $"{shopItem.stock}개";
                _detailPrice.text = bp < shopItem.price ? $"가격: {bp}G (원가 {shopItem.price}G)  ·  재고 {st}" : $"가격: {bp}G  ·  재고 {st}";
                _detailActionBtn.text = "구매하기";
                _detailActionBtn.SetEnabled((PlayerStats.Instance?.Gold ?? 0) >= bp && (shopItem.stock == -1 || shopItem.stock > 0));
                StyleButton(_detailActionBtn, UTKButton.Variant.Primary);
                return;
            }

            // 판매 모드 — 현재 _detailSellSlot 사용
            if (_detailSellSlot != null && _detailSellSlot.item != null)
            {
                var it = _detailSellSlot.item;
                _detailSlot.SetIcon(ItemIconDatabase.GetOrCreateIcon(it));
                _detailSlot.SetRank(it.rarity.ToString());
                StyleShopSlot(_detailSlot, RankRing((int)it.rarity));
                _detailName.text = it.displayName;
                _detailDesc.text = it.description ?? "";
                int sp = CalculateSellPrice(it);
                _detailPrice.text = sp > 0 ? $"판매가: {sp}G  ·  x{_detailSellSlot.count}" : "판매 불가";
                _detailActionBtn.text = "판매하기";
                _detailActionBtn.SetEnabled(sp > 0);
                StyleButton(_detailActionBtn, UTKButton.Variant.Danger);
                return;
            }

            // 빈 상태
            _detailSlot.SetIcon(null);
            _detailSlot.SetRank("");
            _detailName.text = "아이템을 선택하세요";
            _detailDesc.text = "";
            _detailPrice.text = "";
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

        /// <summary>[P30-D] 상점 창 표시 — 상점 위치(Vector3) 기반으로 밀매 가능 영지 판별해 밀매 창(별도)도 함께 노출.</summary>
        public static ShopWindowUTK Open(Vector3? shopPosition)
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null)
            {
                Debug.LogWarning("[ShopWindowUTK] UI Toolkit 루트 없음 — 부트스트랩 미완료.");
                return null;
            }

            // 재사용/신규 공통: 밀매 영지 컨텍스트는 SmuggleWindowUTK.Open 내부에서 동일 위치로 판별
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
                // 우측 3분할 영역 근사 (InventoryWindow.GetContextX 선례) — 화면 폭 대비 60% 지점
                window.style.left = Length.Percent(60f);
                window.style.top = 10f;
                root.Add(window);
                window.Show();
            }

            // 밀매 창 — 적 영지면 함께 열고, 아니면 닫힘 처리 (ShopWindowUTK와 무관 독립 생명주기)
            SmuggleWindowUTK.Open(shopPosition);

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
                StyleTabButton(btn, active, false);
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
                head.style.fontSize = 18f;
                head.style.color = GitHubDark.Gold;   // [GitHub-dark] 비밀/희귀=금색
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
                    _buyScroll.Add(new Label("판매 중인 비밀 아이템이 없습니다.") { style = { fontSize = 16f, color = GitHubDark.TextSub } });
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
                _buyScroll.Add(new Label($"이 카테고리에 판매 중인 아이템이 없습니다{catName}.") { style = { fontSize = 15f, color = GitHubDark.TextSub } });
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
            row.style.borderTopColor = new StyleColor(GitHubDark.Gold);
            row.style.borderBottomColor = new StyleColor(GitHubDark.Gold);
            row.style.borderLeftColor = new StyleColor(GitHubDark.Gold);
            row.style.borderRightColor = new StyleColor(GitHubDark.Gold);

            var slot = new UTKSlot();
            slot.style.width = 64f;
            slot.style.height = 64f;
            slot.style.marginRight = 8f;
            slot.SetIcon(ItemIconDatabase.GetOrCreateIcon(item));
            slot.SetRank("unique");
            StyleShopSlot(slot, GitHubDark.Gold);   // [GitHub-dark] 비밀 아이템 슬롯 금색 링
            row.Add(slot);

            var info = new VisualElement();
            info.style.flexGrow = 1f;

            var nameLabel = new Label(item.displayName);
            nameLabel.style.fontSize = 18f;
            nameLabel.style.color = GitHubDark.Gold;   // [GitHub-dark] 비밀 아이템명=금색
            info.Add(nameLabel);

            if (!string.IsNullOrEmpty(item.description))
            {
                var desc = new Label(item.description);
                desc.style.fontSize = 13f;
                desc.style.color = GitHubDark.TextSub;
                desc.style.whiteSpace = WhiteSpace.Normal;
                info.Add(desc);
            }

            var priceLabel = new Label($"가격: {price}G");
            priceLabel.style.fontSize = 14f;
            priceLabel.style.color = GitHubDark.Accent;   // [GitHub-dark] 가격=액센트
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

        // [Figma 정합] 상점 Store = 5열 그리드 슬롯 — 아이콘 + 하단 가격태그. 클릭 → Detail 패널(구매 버튼이 담당).
        private VisualElement BuildBuyRow(ShopItem shopItem, int index)
        {
            var slotBox = new VisualElement();
            slotBox.style.width = 78f;
            slotBox.style.height = 92f;
            slotBox.style.marginRight = 6f;
            slotBox.style.marginBottom = 6f;
            slotBox.style.flexDirection = FlexDirection.Column;
            slotBox.style.alignItems = Align.Center;
            slotBox.style.justifyContent = Justify.Center;
            // [GitHub-dark] 슬롯 인셋 + 레어도 링
            StyleListRow(slotBox);
            // 레어도 링 갱신(행 스타일 스트로크 위에 레어도 색 오버레이)
            Color ring = shopItem.item != null ? RankRing((int)shopItem.item.rarity) : GitHubDark.Stroke;
            slotBox.style.borderTopColor = slotBox.style.borderBottomColor = slotBox.style.borderLeftColor = slotBox.style.borderRightColor = new StyleColor(ring);
            slotBox.RegisterCallback<PointerEnterEvent>(_ => slotBox.style.backgroundColor = GitHubDark.Stroke);
            slotBox.RegisterCallback<PointerLeaveEvent>(_ => slotBox.style.backgroundColor = GitHubDark.PanelSub);
            slotBox.RegisterCallback<PointerDownEvent>(_ => { _selectedBuyIndex = index; ShowDetail(shopItem, true); });

            // 아이콘 (UTKSlot — 등급 테두리)
            var slot = new UTKSlot();
            slot.style.width = 62f;
            slot.style.height = 62f;
            slot.SetIcon(ItemIconDatabase.GetOrCreateIcon(shopItem.item));
            slot.SetRank(shopItem.isRare ? "unique" : "common");
            StyleShopSlot(slot, ring);   // [GitHub-dark] 레어도 링
            slotBox.Add(slot);

            // 희귀 표시 배지
            if (shopItem.isRare)
            {
                var rare = new Label("★");
                rare.style.fontSize = 12f;
                rare.style.color = GitHubDark.Gold;
                slotBox.Add(rare);
            }

            // 하단 가격 태그 (피그마 Store 슬롯 가격 바)
            int buyPrice = GetBuyPrice(shopItem);
            var priceBar = new Label($"{buyPrice}G");
            priceBar.style.backgroundColor = GitHubDark.PanelSub;
            priceBar.style.color = GitHubDark.Gold;
            priceBar.style.fontSize = 12f;
            priceBar.style.unityTextAlign = TextAnchor.MiddleCenter;
            priceBar.style.width = 66f;
            priceBar.style.marginTop = 4f;
            priceBar.style.borderTopLeftRadius = 4f;
            priceBar.style.borderTopRightRadius = 4f;
            priceBar.style.borderBottomLeftRadius = 4f;
            priceBar.style.borderBottomRightRadius = 4f;
            slotBox.Add(priceBar);

            UTKWindowBase.ApplyUIToolkitFont(slotBox);
            return slotBox;
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
                _sellScroll.Add(new Label("인벤토리를 불러올 수 없습니다.") { style = { fontSize = 16f, color = GitHubDark.TextSub } });
                return;
            }

            bool any = false;
            foreach (var slot in slots)
            {
                if (slot == null || slot.item == null || slot.count <= 0)
                    continue;
                any = true;
                _sellScroll.Add(BuildSellRow(slot));
            }

            if (!any)
                _sellScroll.Add(new Label("판매할 아이템이 없습니다.") { style = { fontSize = 16f, color = GitHubDark.TextSub } });
        }

        // [Figma 정합] 판매/인벤 패널 = 5열 그리드 슬롯 — 아이콘+카운트. 클릭 → Detail 패널(판매 버튼이 담당).
        private VisualElement BuildSellRow(PlayerInventory.ItemSlot slot)
        {
            var slotBox = new VisualElement();
            slotBox.style.width = 78f;
            slotBox.style.height = 84f;
            slotBox.style.marginRight = 6f;
            slotBox.style.marginBottom = 6f;
            slotBox.style.flexDirection = FlexDirection.Column;
            slotBox.style.alignItems = Align.Center;
            slotBox.style.justifyContent = Justify.Center;
            StyleListRow(slotBox);   // [GitHub-dark] 보조 패널 + 1px 스트로크 + r6
            Color ring = RankRing((int)slot.item.rarity);   // [GitHub-dark] 레어도 링
            slotBox.style.borderTopColor = slotBox.style.borderBottomColor = slotBox.style.borderLeftColor = slotBox.style.borderRightColor = new StyleColor(ring);
            slotBox.RegisterCallback<PointerEnterEvent>(_ => slotBox.style.backgroundColor = GitHubDark.Stroke);
            slotBox.RegisterCallback<PointerLeaveEvent>(_ => slotBox.style.backgroundColor = GitHubDark.PanelSub);
            slotBox.RegisterCallback<PointerDownEvent>(_ => { _detailSellSlot = slot; ShowDetail(null, false); });

            var itemSlot = new UTKSlot();
            itemSlot.style.width = 62f;
            itemSlot.style.height = 62f;
            itemSlot.SetIcon(ItemIconDatabase.GetOrCreateIcon(slot.item));
            itemSlot.SetRank(slot.item.rarity.ToString());
            itemSlot.SetCount(slot.count);
            StyleShopSlot(itemSlot, ring);   // [GitHub-dark] 레어도 링
            slotBox.Add(itemSlot);

            // 하단 판매가 태그 (피그마 인벤 슬롯 하단 가격/상태 바)
            int sellPrice = CalculateSellPrice(slot.item);
            var priceBar = new Label(sellPrice > 0 ? $"{sellPrice}G" : "판매불가");
            priceBar.style.backgroundColor = GitHubDark.PanelSub;
            priceBar.style.color = sellPrice > 0 ? GitHubDark.Gold : GitHubDark.Danger;
            priceBar.style.fontSize = 11f;
            priceBar.style.unityTextAlign = TextAnchor.MiddleCenter;
            priceBar.style.width = 66f;
            priceBar.style.marginTop = 4f;
            priceBar.style.borderTopLeftRadius = 4f;
            priceBar.style.borderTopRightRadius = 4f;
            priceBar.style.borderBottomLeftRadius = 4f;
            priceBar.style.borderBottomRightRadius = 4f;
            slotBox.Add(priceBar);

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
            _goldLabel.text = $"골드: {gold}";
        }

        private void SetStatus(string msg)
        {
            _statusLabel.text = msg ?? "";
        }
    }
}