using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using ProjectName.UI;                  // ItemIconDatabase, UIFont
using UnityEngine.UIElements;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit — 밀매 윈도우 (P30-D 밀매 로직을 상점에서 분리한 독립 창).
    /// 적 영지 상점에서 Drug(마약) 아이템을 비싼 값에 판매 → 영지 마약 오염도 상승 → 병사 중독 트리거.
    /// 상점(ShopWindowUTK)은 Buy/Sell 3열만 남기고, 밀매는 본 창이 전담한다.
    ///
    /// 진입점: 상점 Open 시 밀매 가능(적 영지) 여부를 판별해 함께 노출.
    ///   - 구매/판매 가격 소스는 상점과 동일 계열(EconomyPricing/PlayerStats)을 그대로 사용.
    ///   - 밀매 성공 시 TerritoryDrugSystem.AddDrug → 영지 오염↑ → 시간 경과로 병사 중독↑.
    /// </summary>
    public class SmuggleWindowUTK : UTKWindowBase
    {
        /// <summary>밀매 희귀도 가중 — 최종가 = 기준가 × (1 + rarity × 0.25) × SmuggleGainMultiplier. 희귀도 높을수록 비싸다.</summary>
        public const float SMUGGLE_RARITY_WEIGHT_PER_LEVEL = 0.25f;
        /// <summary>거래소 기준가 산출 불가 시 폴백 기준가 = 20 + rarity × 40.</summary>
        public const int SMUGGLE_BASE_FALLBACK = 20;
        public const int SMUGGLE_BASE_FALLBACK_PER_RARITY = 40;

        /// <summary>밀매 대상 영지 — Open(Vector3?) 시 상점/플레이어 위치로 ResolveTerritoryAt 판별. null = 영지 밖.</summary>
        private TerritoryId? _smuggleTerritoryId;

        // ===== UI 노드 =====
        private readonly Label _goldLabel;
        private readonly ScrollView _smuggleScroll;
        private readonly Label _statusLabel;

        // ===== GitHub-dark 리스타일 (상점 창과 동일한 이 창 한정 인라인 오버라이드) =====
        private static class GitHubDark
        {
            public static readonly Color BgBase   = Hex(0x0B0E14);
            public static readonly Color Panel    = Hex(0x161B22);
            public static readonly Color PanelSub = Hex(0x21262D);
            public static readonly Color Accent   = Hex(0x58A6FF);
            public static readonly Color Gold     = Hex(0xE3B341);
            public static readonly Color TextMain = Hex(0xF0F6FC);
            public static readonly Color TextSub  = Hex(0x8B949E);
            public static readonly Color Stroke   = Hex(0x2E343D);
            public static readonly Color Danger   = Hex(0xF85149);

            private static Color Hex(uint rgb) =>
                new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);
        }

        private static Color Hex(uint rgb) =>
            new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);

        /// <summary>[GitHub-dark] 인라인 버튼 스타일 — 상점 창 StyleButton과 동일.</summary>
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

            btn.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            btn.style.backgroundColor = baseBg;
            btn.style.color = textColor;
            btn.style.borderTopWidth = btn.style.borderBottomWidth = btn.style.borderLeftWidth = btn.style.borderRightWidth = 1f;
            btn.style.borderTopColor = btn.style.borderBottomColor = btn.style.borderLeftColor = btn.style.borderRightColor = new StyleColor(baseBg);
            btn.style.borderTopLeftRadius = 6f;
            btn.style.borderTopRightRadius = 6f;
            btn.style.borderBottomLeftRadius = 6f;
            btn.style.borderBottomRightRadius = 6f;

            btn.RegisterCallback<PointerEnterEvent>(_ => btn.style.backgroundColor = hoverBg);
            btn.RegisterCallback<PointerLeaveEvent>(_ => btn.style.backgroundColor = baseBg);
        }

        /// <summary>[GitHub-dark] 리스트 행 — 보조 패널 bg + 1px 스트로크 + r6.</summary>
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

        /// <summary>[GitHub-dark] 슬롯 인셋 — Theme bg_slot.png 제거, 다크 바닥 + 레어도 링.</summary>
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

        /// <summary>GitHub-dark 레어도 링 — 상점 창 RankRing과 동일 규칙.</summary>
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

        /// <summary>창 크롬 GitHub-dark 리스타일 — 생성 시 1회 (상점 창과 동일).</summary>
        private void ApplyGitHubDarkStyle()
        {
            style.backgroundColor = GitHubDark.Panel;
            style.backgroundImage = new StyleBackground(StyleKeyword.None);
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 1f;
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = GitHubDark.Stroke;
            style.borderTopLeftRadius = 8f;
            style.borderTopRightRadius = 8f;
            style.borderBottomLeftRadius = 8f;
            style.borderBottomRightRadius = 8f;
            style.color = GitHubDark.TextMain;

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

        // ───────────────────────────────────────────────
        // 생성자 — 밀매 목록 스크롤 + 골드/상태 표시
        // ───────────────────────────────────────────────
        public SmuggleWindowUTK() : base("💊 밀매", new Vector2(720f, 560f))
        {
            ApplyGitHubDarkStyle();

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

            var warning = new Label("🕵️ 적 영지 비밀거래 — 마약성 약물을 고가에 밀매합니다");
            warning.style.fontSize = 13f;
            warning.style.color = GitHubDark.Danger;
            warning.style.whiteSpace = WhiteSpace.Normal;
            Content.Add(warning);

            _smuggleScroll = new ScrollView();
            _smuggleScroll.style.flexGrow = 1f;
            Content.Add(_smuggleScroll);

            _statusLabel = new Label("");
            _statusLabel.style.fontSize = 14f;
            _statusLabel.style.color = GitHubDark.TextSub;
            _statusLabel.style.minHeight = 20f;
            _statusLabel.style.marginTop = 6f;
            Content.Add(_statusLabel);

            RefreshSmuggleList();
        }

        // =====================================================================
        // 공개 진입점
        // =====================================================================
        public static SmuggleWindowUTK Instance { get; private set; }

        /// <summary>상점 위치 기반 밀매 창 표시. 적 영지가 아니면 열려 있던 창을 닫고 null 반환.</summary>
        public static SmuggleWindowUTK Open(Vector3? pos)
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null)
            {
                Debug.LogWarning("[SmuggleWindowUTK] UI Toolkit 루트 없음 — 부트스트랩 미완료.");
                return null;
            }

            // 위치 폴백
            Vector3? resolvePos = pos;
            if (!resolvePos.HasValue)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) resolvePos = player.transform.position;
            }

            TerritoryId? territory = ResolveSmuggleTerritory(resolvePos);
            if (!IsTerritorySmuggleable(territory))
            {
                // 적 영지 아님 → 밀매 창 유지 중이면 닫기
                if (Instance != null)
                {
                    Instance.Hide();
                    Instance = null;
                }
                return null;
            }

            if (Instance != null)
            {
                Instance._smuggleTerritoryId = territory;
                Instance.Show();
                Instance.RefreshSmuggleList();
                Instance.UpdateGoldDisplay();
                return Instance;
            }

            var window = new SmuggleWindowUTK();
            Instance = window;
            window._smuggleTerritoryId = territory;
            window.style.right = 10f;
            window.style.top = 10f;
            root.Add(window);
            window.Show();
            window.RefreshSmuggleList();
            window.UpdateGoldDisplay();
            return window;
        }

        // =====================================================================
        // UTKWindowBase 훅
        // =====================================================================
        protected override void OnWindowOpen()
        {
            Debug.Log("[SmuggleWindowUTK] 열림");
            UpdateGoldDisplay();
            SetStatus("");
        }

        protected override void OnWindowClosed()
        {
            Debug.Log("[SmuggleWindowUTK] 닫힘");
            if (Instance == this) Instance = null;
        }

        // =====================================================================
        // 영지 판별
        // =====================================================================
        /// <summary>[P30-D] 위치 기반 밀매 가능 영지 판별 (위치 없으면 null).</summary>
        private static TerritoryId? ResolveSmuggleTerritory(Vector3? pos)
        {
            if (!pos.HasValue) return null;
            return TerritoryDatabase.Instance.ResolveTerritoryAt(pos.Value, 80f);
        }

        /// <summary>영지가 적 영지(밀매 가능)인지 여부. null/아군/상태없음 = false.</summary>
        private static bool IsTerritorySmuggleable(TerritoryId? territory)
        {
            if (!territory.HasValue) return false;
            var state = TerritoryDatabase.Instance.GetState(territory.Value);
            if (state == null) return false;
            if (state.ownership == TerritoryOwnership.PlayerOwned) return false;
            return true;
        }

        /// <summary>[P30-D] 현재 밀매 가능한가 + 이유 반환.</summary>
        private bool IsSmuggleAllowed(out string reason)
        {
            reason = "";
            if (!_smuggleTerritoryId.HasValue)
            {
                reason = "영지 밖 — 밀매 불가";
                return false;
            }
            var state = TerritoryDatabase.Instance.GetState(_smuggleTerritoryId.Value);
            if (state == null)
            {
                reason = "영지 상태 없음";
                return false;
            }
            if (state.ownership == TerritoryOwnership.PlayerOwned)
            {
                reason = "아군 영지 — 밀매 불가";
                return false;
            }
            return true;
        }

        // =====================================================================
        // 가격 / 목록
        // =====================================================================
        /// <summary>[P30-D] 마약 밀매 가격 — 희귀도(ItemRarity 0~5)↑ → 비싼 값, 화술 스탯(SmuggleGainMultiplier) 반영.</summary>
        private static int CalculateSmugglePrice(PlayerInventory.ItemData item)
        {
            if (item == null) return 0;

            int rarity = Mathf.Clamp((int)item.rarity, 0, 5);

            int sellPrice = EconomyPricing.GetSellPrice(item);   // 거래소 기준가 (산출 불가 시 0)
            float basePrice = sellPrice > 0 ? (float)sellPrice : (SMUGGLE_BASE_FALLBACK + rarity * SMUGGLE_BASE_FALLBACK_PER_RARITY);

            float mult = PlayerStats.Instance?.SmuggleGainMultiplier ?? 1f;
            float weighted = basePrice * (1f + rarity * SMUGGLE_RARITY_WEIGHT_PER_LEVEL) * mult;
            int price = Mathf.Max(1, Mathf.CeilToInt(weighted));
            return price;
        }

        /// <summary>[P30-D] 밀매 목록 — 플레이어 인벤 Drug(마약) 아이템만.</summary>
        private void RefreshSmuggleList()
        {
            _smuggleScroll.Clear();

            string denyReason;
            if (!IsSmuggleAllowed(out denyReason))
            {
                _smuggleScroll.Add(new Label(denyReason) { style = { fontSize = 16f, color = GitHubDark.TextSub } });
                return;
            }

            var slots = PlayerInventory.Instance != null ? PlayerInventory.Instance.GetAllSlots() : null;
            if (slots == null)
            {
                _smuggleScroll.Add(new Label("인벤토리를 불러올 수 없습니다.") { style = { fontSize = 16f, color = GitHubDark.TextSub } });
                return;
            }

            bool any = false;
            foreach (var slot in slots)
            {
                if (slot == null || slot.item == null || slot.count <= 0) continue;
                if (slot.item.category != PlayerInventory.ItemCategory.Drug) continue;   // 마약성 약물만
                any = true;
                _smuggleScroll.Add(BuildSmuggleRow(slot));
            }

            if (!any)
                _smuggleScroll.Add(new Label("밀매할 마약성 약물이 없습니다.") { style = { fontSize = 16f, color = GitHubDark.TextSub } });
        }

        private VisualElement BuildSmuggleRow(PlayerInventory.ItemSlot slot)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 6f;
            row.style.paddingTop = 4f;
            row.style.paddingBottom = 4f;
            row.style.paddingLeft = 4f;
            row.style.paddingRight = 4f;
            StyleListRow(row);

            var slotBox = new UTKSlot();
            slotBox.style.width = 64f;
            slotBox.style.height = 64f;
            slotBox.style.marginRight = 8f;
            slotBox.SetIcon(ItemIconDatabase.GetOrCreateIcon(slot.item));
            slotBox.SetRank(slot.item.rarity.ToString());
            slotBox.SetCount(slot.count);
            StyleShopSlot(slotBox, RankRing((int)slot.item.rarity));
            row.Add(slotBox);

            var info = new VisualElement();
            info.style.flexGrow = 1f;

            var nameLabel = new Label(slot.item.displayName);
            nameLabel.style.fontSize = 18f;
            nameLabel.style.color = GitHubDark.TextMain;
            info.Add(nameLabel);

            int price = CalculateSmugglePrice(slot.item);
            var priceLabel = new Label($"{slot.item.rarity.ToString()} · 밀매가 {price}G");
            priceLabel.style.fontSize = 14f;
            priceLabel.style.color = GitHubDark.Gold;
            info.Add(priceLabel);

            row.Add(info);

            var smuggleBtn = UTKButton.Create("밀매", () => SmuggleSlot(slot), UTKButton.Variant.Danger);
            smuggleBtn.SetEnabled(price > 0);
            StyleButton(smuggleBtn, UTKButton.Variant.Danger);
            row.Add(smuggleBtn);

            UTKWindowBase.ApplyUIToolkitFont(row);
            return row;
        }

        /// <summary>[P30-D] 밀매 실행 — Drug 제거 → 골드 획득 → 영지 마약 오염도 상승(시간 경과 따라 병사 중독 증가 트리거).</summary>
        private bool SmuggleSlot(PlayerInventory.ItemSlot slot)
        {
            if (slot == null || slot.item == null || PlayerInventory.Instance == null) return false;
            if (slot.item.category != PlayerInventory.ItemCategory.Drug)
            {
                SetStatus("마약성 약물만 밀매할 수 있습니다.");
                return false;
            }

            string denyReason;
            if (!IsSmuggleAllowed(out denyReason))
            {
                SetStatus(denyReason);
                return false;
            }

            int price = CalculateSmugglePrice(slot.item);
            bool removed = PlayerInventory.Instance.RemoveItem(slot.item.id, 1);
            if (!removed)
            {
                SetStatus("밀매 실패: 아이템 제거 불가.");
                return false;
            }

            PlayerStats.Instance?.AddGold(price, "smuggle");
            TerritoryDrugSystem.AddDrug(_smuggleTerritoryId.Value, Mathf.Clamp((int)slot.item.rarity, 0, 5));   // 밀매 성공 → 영지 오염↑ → 병사 중독↑
            Debug.Log($"[SmuggleWindowUTK] 💊 밀매 성공: {slot.item.displayName}(희귀도 {(int)slot.item.rarity}) → {price}G, 영지 오염 +");
            SetStatus($"{slot.item.displayName} 밀매 → {price}G (영지 병사 중독 상승)");
            RefreshSmuggleList();
            UpdateGoldDisplay();
            return true;
        }

        // =====================================================================
        // 표시
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
