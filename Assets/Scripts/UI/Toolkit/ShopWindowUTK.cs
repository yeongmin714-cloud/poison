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

        // 선택 상태
        private int _selectedBuyIndex = -1;

        /// <summary>테스트/외부 접근용 읽기 전용 인벤토리.</summary>
        public IReadOnlyList<ShopItem> ShopInventory => _shopInventory;

        // ───────────────────────────────────────────────
        // 생성자 — 트리 구성
        // ───────────────────────────────────────────────
        public ShopWindowUTK() : base("🏪 상점", new Vector2(440f, 600f))
        {
            // ── 골드 잔액 ──
            _goldLabel = new Label("골드: 0");
            _goldLabel.style.fontSize = 20f;
            _goldLabel.style.color = UTKColor.AccentRare;
            _goldLabel.style.marginBottom = 6f;
            _goldLabel.AddToClassList("utk-title-label");
            Content.Add(_goldLabel);

            // ── 탭 행 ──
            var tabRow = new VisualElement();
            tabRow.style.flexDirection = FlexDirection.Row;
            tabRow.style.marginBottom = 6f;
            Content.Add(tabRow);

            _tabBuy = UTKButton.Create("구매", () => SwitchTab(true), UTKButton.Variant.Primary);
            _tabSell = UTKButton.Create("판매", () => SwitchTab(false), UTKButton.Variant.Secondary);
            _tabBuy.style.flexGrow = 1f;
            _tabSell.style.flexGrow = 1f;
            tabRow.Add(_tabBuy);
            tabRow.Add(_tabSell);

            // ── 구매 패널 ──
            _buyPanel = new VisualElement();
            _buyPanel.style.flexGrow = 1f;
            _buyScroll = new ScrollView();
            _buyScroll.style.flexGrow = 1f;
            _buyPanel.Add(_buyScroll);
            Content.Add(_buyPanel);

            // ── 판매 패널 ──
            _sellPanel = new VisualElement();
            _sellPanel.style.flexGrow = 1f;
            _sellScroll = new ScrollView();
            _sellScroll.style.flexGrow = 1f;
            _sellPanel.Add(_sellScroll);
            Content.Add(_sellPanel);

            // ── 상태 라벨 ──
            _statusLabel = new Label("");
            _statusLabel.style.fontSize = 14f;
            _statusLabel.style.color = UTKColor.TextSecondary;
            _statusLabel.style.marginTop = 6f;
            _statusLabel.style.minHeight = 20f;
            Content.Add(_statusLabel);

            SwitchTab(true);
        }

        // =====================================================================
        // 공개 진입점 — 루트에 부착 + 표시
        // =====================================================================
        /// <summary>상점 창을 UTK 루트 우측에 생성·표시. 열려 있으면 기존 인스턴스를 재사용(멱등).</summary>
        public static ShopWindowUTK Instance { get; private set; }

        public static ShopWindowUTK Open()
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
                return Instance;
            }

            var window = new ShopWindowUTK();
            Instance = window;
            // 우측 3분할 영역 근사 (InventoryWindow.GetContextX 선례) — 화면 폭 대비 60% 지점
            window.style.left = Length.Percent(60f);
            window.style.top = 10f;
            root.Add(window);
            window.Show();
            return window;
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
        private void SwitchTab(bool buy)
        {
            _buyPanel.style.display = buy ? DisplayStyle.Flex : DisplayStyle.None;
            _sellPanel.style.display = buy ? DisplayStyle.None : DisplayStyle.Flex;
            _tabBuy.RemoveFromClassList("utk-btn--primary");
            _tabBuy.RemoveFromClassList("utk-btn--secondary");
            _tabSell.RemoveFromClassList("utk-btn--primary");
            _tabSell.RemoveFromClassList("utk-btn--secondary");
            if (buy) { _tabBuy.AddToClassList("utk-btn--primary");   _tabSell.AddToClassList("utk-btn--secondary"); }
            else     { _tabBuy.AddToClassList("utk-btn--secondary"); _tabSell.AddToClassList("utk-btn--primary"); }

            RefreshBuyList();
            RefreshSellList();
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
                head.style.color = UTKColor.AccentRare;
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
                    _buyScroll.Add(new Label("판매 중인 비밀 아이템이 없습니다.") { style = { fontSize = 16f, color = UTKColor.TextSecondary } });
                }

                var spacer = new VisualElement { style = { height = 12f } };
                _buyScroll.Add(spacer);
            }

            if (_shopInventory.Count == 0)
            {
                _buyScroll.Add(new Label("판매 중인 아이템이 없습니다.") { style = { fontSize = 16f, color = UTKColor.TextSecondary } });
                return;
            }

            int index = 0;
            foreach (var shopItem in _shopInventory)
            {
                _buyScroll.Add(BuildBuyRow(shopItem, index));
                index++;
            }
            _selectedBuyIndex = -1;
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
            row.style.borderTopWidth = 1f;
            row.style.borderTopColor = new Color(0.7f, 0.5f, 0.1f);

            var slot = new UTKSlot();
            slot.style.width = 64f;
            slot.style.height = 64f;
            slot.style.marginRight = 8f;
            slot.SetIcon(ItemIconDatabase.GetOrCreateIcon(item));
            slot.SetRank("unique");
            row.Add(slot);

            var info = new VisualElement();
            info.style.flexGrow = 1f;

            var nameLabel = new Label(item.displayName);
            nameLabel.style.fontSize = 18f;
            nameLabel.style.color = UTKColor.AccentRare;
            info.Add(nameLabel);

            if (!string.IsNullOrEmpty(item.description))
            {
                var desc = new Label(item.description);
                desc.style.fontSize = 13f;
                desc.style.color = UTKColor.TextSecondary;
                desc.style.whiteSpace = WhiteSpace.Normal;
                info.Add(desc);
            }

            var priceLabel = new Label($"가격: {price}G");
            priceLabel.style.fontSize = 14f;
            priceLabel.style.color = UTKColor.AccentMagic;
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
            row.Add(buyBtn);

            UTKWindowBase.ApplyUIToolkitFont(row);
            return row;
        }

        private VisualElement BuildBuyRow(ShopItem shopItem, int index)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 6f;
            row.style.paddingTop = 4f;
            row.style.paddingBottom = 4f;
            row.style.paddingLeft = 4f;
            row.style.paddingRight = 4f;
            row.style.borderTopWidth = 1f;
            row.style.borderTopColor = UTKColor.IronLine;
            row.RegisterCallback<PointerDownEvent>(_ => { _selectedBuyIndex = index; });

            // 아이콘 (UTKSlot — 등급 테두리 + 호버 글로우)
            var slot = new UTKSlot();
            slot.style.width = 64f;
            slot.style.height = 64f;
            slot.style.marginRight = 8f;
            slot.SetIcon(ItemIconDatabase.GetOrCreateIcon(shopItem.item));
            slot.SetRank(shopItem.isRare ? "unique" : "common");
            row.Add(slot);

            // 정보 컬럼
            var info = new VisualElement();
            info.style.flexGrow = 1f;

            var nameLabel = new Label(shopItem.item != null ? shopItem.item.displayName : "[데이터 없음]");
            nameLabel.style.fontSize = 18f;
            nameLabel.style.color = shopItem.isRare ? UTKColor.AccentRare : UTKColor.TextPrimary;
            info.Add(nameLabel);

            if (shopItem.isRare)
            {
                var rare = new Label("[희귀]");
                rare.style.fontSize = 13f;
                rare.style.color = UTKColor.AccentMagic;
                info.Add(rare);
            }

            if (shopItem.item != null && !string.IsNullOrEmpty(shopItem.item.description))
            {
                var desc = new Label(shopItem.item.description);
                desc.style.fontSize = 13f;
                desc.style.color = UTKColor.TextSecondary;
                desc.style.whiteSpace = WhiteSpace.Normal;
                info.Add(desc);
            }

            // 가격 (할인가 병기 원가 관례 유지) + 재고
            int buyPrice = GetBuyPrice(shopItem);
            string priceText = buyPrice < shopItem.price
                ? $"가격: {buyPrice}G (원가 {shopItem.price}G)"
                : $"가격: {buyPrice}G";
            string stockText = shopItem.stock == -1 ? "재고: 무한" : $"재고: {shopItem.stock}개";

            var priceLabel = new Label($"{priceText}  {stockText}");
            priceLabel.style.fontSize = 14f;
            priceLabel.style.color = UTKColor.AccentMagic;
            info.Add(priceLabel);

            row.Add(info);

            // 구매 버튼
            bool canAfford = (PlayerStats.Instance?.Gold ?? 0) >= buyPrice;
            bool inStock = shopItem.stock == -1 || shopItem.stock > 0;
            var buyBtn = UTKButton.Create("구매", () => BuyAtIndex(index), UTKButton.Variant.Primary);
            buyBtn.SetEnabled(canAfford && inStock);
            row.Add(buyBtn);

            UTKWindowBase.ApplyUIToolkitFont(row);
            return row;
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
                _sellScroll.Add(new Label("인벤토리를 불러올 수 없습니다.") { style = { fontSize = 16f, color = UTKColor.TextSecondary } });
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
                _sellScroll.Add(new Label("판매할 아이템이 없습니다.") { style = { fontSize = 16f, color = UTKColor.TextSecondary } });
        }

        private VisualElement BuildSellRow(PlayerInventory.ItemSlot slot)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 6f;
            row.style.paddingTop = 4f;
            row.style.paddingBottom = 4f;
            row.style.paddingLeft = 4f;
            row.style.paddingRight = 4f;
            row.style.borderTopWidth = 1f;
            row.style.borderTopColor = UTKColor.IronLine;

            var slotBox = new UTKSlot();
            slotBox.style.width = 64f;
            slotBox.style.height = 64f;
            slotBox.style.marginRight = 8f;
            slotBox.SetIcon(ItemIconDatabase.GetOrCreateIcon(slot.item));
            slotBox.SetRank(slot.item.rarity.ToString());
            slotBox.SetCount(slot.count);
            row.Add(slotBox);

            var info = new VisualElement();
            info.style.flexGrow = 1f;

            var nameLabel = new Label(slot.item.displayName);
            nameLabel.style.fontSize = 18f;
            nameLabel.style.color = UTKColor.TextPrimary;
            info.Add(nameLabel);

            int sellPrice = CalculateSellPrice(slot.item);
            string priceText = sellPrice > 0
                ? $"x{slot.count}   판매: {sellPrice}G"
                : $"x{slot.count}   판매 불가";
            var priceLabel = new Label(priceText);
            priceLabel.style.fontSize = 14f;
            priceLabel.style.color = UTKColor.GuildGreen;
            info.Add(priceLabel);

            row.Add(info);

            var sellBtn = UTKButton.Create("판매", () => SellSlot(slot), UTKButton.Variant.Danger);
            sellBtn.SetEnabled(sellPrice > 0); // 0G 판매 방지
            row.Add(sellBtn);

            UTKWindowBase.ApplyUIToolkitFont(row);
            return row;
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