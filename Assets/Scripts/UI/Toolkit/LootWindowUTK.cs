using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;            // ILootBasket, LootEntry, PlayerInventory
using ProjectName.UI;              // ItemIconDatabase

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit — 전리품 윈도우 (Figma loot-panel 정합 개편).
    /// Figma 규격: 5열×2행 그리드 + 서브헤더(습득가능 배지 + 획득 n/10) + 풋터(전부 습득/닫기).
    /// 기존 UTK Phase U2 Round 2A 로직(빈바구니 자동Hide·드래그→인벤·우클릭 획득) 100% 보존.
    ///  ① 항목 그리드  — ILootBasket.Items의 각 LootEntry를 5열 그리드 슬롯(아이콘+카운트)으로 표시. 매 갱신 재조회.
    ///  ② 슬롯 드래그 → 인벤 — IUTKDragSource 구현: 좌클릭 드래그가 UTKDragPayload(SourceKind.Loot, SourceIndex=항목 인덱스) 생성.
    ///                         InventoryWindowUTK가 수신해 PlayerInventory.AddItem 수행.
    ///  ③ 슬롯 우클릭 획득 — TakeSelectedItem(→ ILootBasket.TakeItem → PlayerInventory.AddItem) 동일 데이터 경로.
    ///  ⑤ 변화 폴링 갱신    — 바구니 항목 배열을 주기 재조회해 즉시 갱신.
    ///  ⑥ 빈바구니 처리     — 바구니가 비어있거나 회수 불가하면 자동 Hide + 참조 해제.
    /// </summary>
    public class LootWindowUTK : UTKWindowBase, IUTKDragSource, IUTKDropTarget
    {
        // ===== 싱글턴 / 팩토리 =====
        private static LootWindowUTK _instance;
        public static LootWindowUTK Instance => _instance;

        /// <summary>팩토리 — 멱등.</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new LootWindowUTK();
        }

        /// <summary>특정 바스켓 열기 (원본 OpenForBasket과 동일 역할 + 열기 보장).</summary>
        public static void Open(ILootBasket basket)
        {
            if (basket == null) return;
            Ensure();
            _instance.OpenForBasket(basket);
        }

        // ===== 설정 (Figma loot-panel 420x360, 그리드 5열) =====
        private const long RefreshMs = 250L;
        private const int GridColumns = 5;   // Figma 5열
        private const float SlotSize = 64f;
        private const float SlotGap = 6f;

        // =====================================================================
        //  [Figma GitHub-dark 리스타일] 전리품 창 한정 인라인 오버라이드 — 기능 무수정, 시각 전용.
        //  Theme.uss / 공용 UTKButton·UTKWindowBase·타 UTK 창은 절대 수정하지 않는다.
        //  =====================================================================
        private static class GitHubDark
        {
            public static readonly Color BgBase   = Hex(0x0B0E14);   // 최배경 — 아이콘 인셋 바닥
            public static readonly Color Panel    = Hex(0x161B22);   // 창 본체 패널
            public static readonly Color PanelSub = Hex(0x21262D);   // 보조 패널(행/버튼)
            public static readonly Color Accent   = Hex(0x58A6FF);   // 강조(액센트)
            public static readonly Color Gold     = Hex(0xE3B341);   // 희귀/카운트/골드
            public static readonly Color TextMain = Hex(0xF0F6FC);   // 기본 텍스트
            public static readonly Color TextSub  = Hex(0x8B949E);   // 보조 텍스트
            public static readonly Color Stroke   = Hex(0x2E343D);   // 테두리/구분선
            public static readonly Color Danger   = Hex(0xF85149);   // danger 버튼
            public static readonly Color RankEpic = Hex(0xA371F7);   // epic 퍼플

            private static Color Hex(uint rgb) =>
                new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);
        }

        /// <summary>등급 테두리 색 — Figma/GitHub-dark 팔레트(4~5=금/3=퍼플/1~2=액센트/0=보조).</summary>
        private static Color RankColor(int rarityIndex)
        {
            switch (rarityIndex)
            {
                case 0: return GitHubDark.TextSub;
                case 1:
                case 2: return GitHubDark.Accent;
                case 3: return GitHubDark.RankEpic;
                default: return GitHubDark.Gold;
            }
        }

        /// <summary>GitHub-dark 버튼 인라인 오버라이드(이 창 한정) — IStyle 쇼트핸드 없음 → 4면 개별 대입.</summary>
        private static void StyleButton(Button btn, UTKButton.Variant variant)
        {
            if (btn == null) return;
            Color baseBg, hoverBg, textColor;
            switch (variant)
            {
                case UTKButton.Variant.Primary:
                    baseBg = GitHubDark.Accent; hoverBg = new Color32(0x79, 0xC0, 0xFF, 0xFF); textColor = GitHubDark.BgBase; break;
                case UTKButton.Variant.Danger:
                    baseBg = GitHubDark.Danger; hoverBg = new Color32(0xDA, 0x36, 0x33, 0xFF); textColor = GitHubDark.TextMain; break;
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

        /// <summary>창 크롬(본체/타이틀바/닫기버튼) GitHub-dark 리스타일 — 생성 시 1회.</summary>
        private void ApplyGitHubDarkStyle()
        {
            style.backgroundColor = GitHubDark.Panel;
            style.backgroundImage = new StyleBackground(StyleKeyword.None);
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 1f;
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = GitHubDark.Stroke;
            style.borderTopLeftRadius = 8f;
            style.borderTopRightRadius = 8f;
            style.borderBottomLeftRadius = 8f;
            style.borderBottomRightRadius = 8f;   // 메인 반경 r8
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
                closeBtn.style.borderBottomRightRadius = 4f;            // 작은배지 r4
                closeBtn.style.color = GitHubDark.TextMain;
            }
        }

        /// <summary>GitHub-dark 그리드 슬롯 — 다크 인셋 + 1px 스트로크 + r6 + 등급 상단 테두리 (이 창 한정).</summary>
        private static void ApplyDarkSlotStyle(VisualElement slot, int rarityIndex)
        {
            if (slot == null) return;
            slot.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            slot.style.backgroundColor = GitHubDark.BgBase;
            slot.style.borderTopWidth = 2f;   // 등급색 상단 강조선 (피그마 슬롯 상단 유색 선)
            slot.style.borderBottomWidth = 1f;
            slot.style.borderLeftWidth = 1f;
            slot.style.borderRightWidth = 1f;
            var rank = RankColor(rarityIndex);
            slot.style.borderTopColor = new StyleColor(rank);
            slot.style.borderBottomColor = slot.style.borderLeftColor = slot.style.borderRightColor = new StyleColor(GitHubDark.Stroke);
            slot.style.borderTopLeftRadius = 6f;
            slot.style.borderTopRightRadius = 6f;
            slot.style.borderBottomLeftRadius = 6f;
            slot.style.borderBottomRightRadius = 6f;   // 서브 반경 r6
        }

        // ===== 레퍼런스 =====
        private ILootBasket _basket;
        private readonly VisualElement _grid;            // 5열 그리드
        private readonly Label _countLabel;              // "획득 아이템: n / 10"
        private readonly Label _badgeLabel;              // "습득 가능" 배지
        private readonly Label _emptyLabel;
        private readonly Button _acquireAllBtn;          // 풋터 "전부 습득하기"
        private UnityEngine.UIElements.IVisualElementScheduledItem _refreshTask;

        private LootWindowUTK() : base("🎁 전리품", new Vector2(420f, 360f))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            // ── 서브헤더: 좌측 "습득 가능" 배지 + 우측 카운트 ──
            var subHeader = new VisualElement();
            subHeader.style.flexDirection = FlexDirection.Row;
            subHeader.style.justifyContent = Justify.SpaceBetween;
            subHeader.style.alignItems = Align.Center;
            subHeader.style.marginBottom = 6f;
            _content.Add(subHeader);

            _badgeLabel = new Label("습득 가능");
            _badgeLabel.style.backgroundColor = GitHubDark.Accent;
            _badgeLabel.style.color = GitHubDark.BgBase;
            _badgeLabel.style.fontSize = 12f;
            _badgeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _badgeLabel.style.paddingTop = 2f;
            _badgeLabel.style.paddingBottom = 2f;
            _badgeLabel.style.paddingLeft = 8f;
            _badgeLabel.style.paddingRight = 8f;
            _badgeLabel.style.borderTopLeftRadius = 4f;
            _badgeLabel.style.borderTopRightRadius = 4f;
            _badgeLabel.style.borderBottomLeftRadius = 4f;
            _badgeLabel.style.borderBottomRightRadius = 4f;
            subHeader.Add(_badgeLabel);

            _countLabel = new Label("");
            _countLabel.style.fontSize = 13f;
            _countLabel.style.color = new StyleColor(GitHubDark.TextSub);
            subHeader.Add(_countLabel);

            // ── 그리드 본문 (5열 wrap) ──
            _grid = new VisualElement();
            _grid.name = "LootGrid";
            _grid.style.flexDirection = FlexDirection.Row;
            _grid.style.flexWrap = Wrap.Wrap;
            _grid.style.marginTop = 4f;
            _grid.style.flexGrow = 1f;
            _content.Add(_grid);

            _emptyLabel = new Label("(전리품이 없습니다)");
            _emptyLabel.style.fontSize = 14f;
            _emptyLabel.style.color = new StyleColor(GitHubDark.TextSub);
            _emptyLabel.style.marginTop = 12f;
            _content.Add(_emptyLabel);

            // ── 풋터: "전부 습득하기" + "닫기" ──
            var footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.marginTop = 8f;
            footer.style.paddingTop = 6f;
            footer.style.borderTopWidth = 1f;
            footer.style.borderTopColor = new StyleColor(GitHubDark.Stroke);
            _content.Add(footer);

            _acquireAllBtn = new Button(AcquireAll);
            _acquireAllBtn.text = "전부 습득하기";
            _acquireAllBtn.style.flexGrow = 1f;
            _acquireAllBtn.style.height = 32f;
            _acquireAllBtn.style.marginRight = 8f;
            StyleButton(_acquireAllBtn, UTKButton.Variant.Primary);
            footer.Add(_acquireAllBtn);

            var closeBtnFooter = new Button(Hide);
            closeBtnFooter.text = "닫기";
            closeBtnFooter.style.flexGrow = 1f;
            closeBtnFooter.style.height = 32f;
            StyleButton(closeBtnFooter, UTKButton.Variant.Secondary);
            footer.Add(closeBtnFooter);

            ApplyUIToolkitFont(this);
            ApplyGitHubDarkStyle();   // [GitHub-dark] 창 크롬 리스타일 — 이 창 한정 인라인

            style.display = DisplayStyle.None;
            style.left = 16f;   // Show()에서 우측 배치로 덮어씀
            style.top = 10f;
        }

        // =====================================================================
        //  공개 진입점 — 원본 OpenForBasket 바스켓 설정 경로
        // =====================================================================

        /// <summary>특정 바스켓 열기. 빈/회수 불가 바구니는 무시(원본 OpenForBasket 규약).</summary>
        public void OpenForBasket(ILootBasket basket)
        {
            if (basket == null || basket.IsEmpty || !basket.IsAvailable) return;
            _basket = basket;
            Show();
        }

        /// <summary>현재 바스켓 (외부/테스트용 읽기 전용).</summary>
        public ILootBasket Basket => _basket;

        // =====================================================================
        //  생명주기 (UTKWindowBase 훅)
        // =====================================================================

        public override void Show()
        {
            if (_basket == null) return;
            if (_basket.IsEmpty || !_basket.IsAvailable)
            {
                Hide();
                return;
            }
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            ApplyRightPlacement();
            StartRefreshLoop();
            RefreshGrid();
            Debug.Log("[LootUTK] 전리품 창 열림 (" + (_basket != null ? _basket.BasketName : "?") + ")");
        }

        public override void Hide()
        {
            base.Hide();
            StopRefreshLoop();
            Debug.Log("[LootUTK] 전리품 창 닫힘");
        }

        /// <summary>창이 UIRoot에 부착된 후 이 창을 드롭 타겟으로 등록 (소스 재드롭=취소 소비).</summary>
        protected override void OnWindowOpen()
        {
            UTKDragDrop.RegisterDropTarget(this, this);
        }

        /// <summary>창 닫힘 — 드롭 타겟 해제(재열림 시 재등록).</summary>
        protected override void OnWindowClosed()
        {
            UTKDragDrop.UnregisterDropTarget(this);
            _basket = null;
        }

        /// <summary>우측 배치 — 화면 2/3 + 6 (원본 GetContextX(WINDOW_WIDTH) 관례), 높이 Screen-180</summary>
        private void ApplyRightPlacement()
        {
            var root = UIToolkitBootstrap.UIRoot;
            float sw = root != null ? root.worldBound.width : 1920f;
            float sh = root != null ? root.worldBound.height : 1080f;
            style.left = sw * 2f / 3f + 6f;
            style.top = 10f;
            style.width = sw / 3f - 12f;
            style.height = sh - 180f;
        }

        // =====================================================================
        //  ⑤ 변화 폴링 갱신
        // =====================================================================

        private void StartRefreshLoop()
        {
            if (_refreshTask != null) return;
            _refreshTask = schedule.Execute(() =>
            {
                if (UTKDragDrop.Active) return;   // [U8 수리] 드래그 중 재생성 금지 — 캡처 상실 차단
                if (IsOpen) RefreshGrid();
            }).Every(RefreshMs);
        }

        private void StopRefreshLoop()
        {
            if (_refreshTask != null)
            {
                _refreshTask.Pause();
                _refreshTask = null;
            }
        }

        // =====================================================================
        //  ① 바구니 항목 그리드 — 매 갱신 재조회 (⑥ 빈바구니 자동 Hide)
        // =====================================================================

        /// <summary>[U8] 우클릭 즉시 획득 — 기존 TakeSelectedItem 데이터 경로.</summary>
        private void TakeLootRow(int index)
        {
            TakeSelectedItem(index);
        }

        private void RefreshGrid()
        {
            // ⑥ 원본 RefreshLoot 규약 — 바구니 없음/빈/회수 불가 → 참조 해제 + 닫힘
            if (_basket == null || _basket.IsEmpty || !_basket.IsAvailable)
            {
                _basket = null;
                _grid.Clear();
                if (IsOpen)
                    Hide();
                return;
            }

            var items = _basket.Items;
            int total = items != null ? items.Count : 0;

            _grid.Clear();

            for (int i = 0; i < total; i++)
            {
                var entry = items[i];
                if (entry == null || entry.Item == null || entry.Count <= 0) continue;
                _grid.Add(BuildSlot(entry, i));
            }

            _countLabel.text = "획득 아이템: " + total + " / 10";
            Debug.Log("[LootUTK] 바구니 그리드 갱신: " + total + "개 항목");
        }

        private VisualElement BuildSlot(LootEntry entry, int index)
        {
            var slot = new VisualElement();
            slot.name = "LootSlot_" + index;
            slot.style.width = SlotSize;
            slot.style.height = SlotSize;
            slot.style.marginRight = SlotGap;
            slot.style.marginBottom = SlotGap;
            slot.style.alignItems = Align.Center;
            slot.style.justifyContent = Justify.Center;
            ApplyDarkSlotStyle(slot, (int)entry.Item.rarity);   // [GitHub-dark] 슬롯 + 등급 상단 테두리

            // 아이콘 (중앙)
            var icon = ItemIconDatabase.GetOrCreateIcon(entry.Item);
            var slotIcon = new VisualElement();
            slotIcon.style.position = Position.Absolute;
            slotIcon.style.left = 0f;
            slotIcon.style.top = 0f;
            slotIcon.style.right = 0f;
            slotIcon.style.bottom = 0f;
            slotIcon.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            slotIcon.style.backgroundImage = UTKTextureSafe.ToBackground(icon);
            slot.Add(slotIcon);

            // 카운트 (우하단)
            var countLabel = new Label("x" + entry.Count);
            countLabel.style.position = Position.Absolute;
            countLabel.style.right = 4f;
            countLabel.style.bottom = 2f;
            countLabel.style.fontSize = 12f;
            countLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            countLabel.style.color = new StyleColor(GitHubDark.Gold);   // [GitHub-dark] 카운트 — 골드
            slot.Add(countLabel);

            // ② 슬롯 드래그 소스 (좌클릭) + 좌클릭 획득 없음/우클릭 획득
            UTKDragDrop.MakeDraggable(slot, () => MakePayload(index, entry.Item), null, () => TakeLootRow(index));

            return slot;
        }

        /// <summary>행 드래그 페이로드 — SourceKind.Loot, SourceIndex=바구니 항목 인덱스.</summary>
        private UTKDragPayload MakePayload(int index, PlayerInventory.ItemData item)
        {
            var p = new UTKDragPayload();
            p.Source = UTKDragSourceKind.Loot;
            p.SourceIndex = index;
            p.Item = item;
            p.Icon = item != null ? ItemIconDatabase.GetOrCreateIcon(item) : null;
            return p;
        }

        // =====================================================================
        //  IUTKDragSource — ② 드래그 시작 통지
        // =====================================================================

        public void BeginDrag(UTKDragPayload payload)
        {
            Debug.Log("[LootUTK] 슬롯 드래그 시작: " + (payload != null && payload.Item != null ? payload.Item.displayName : "?"));
        }

        // =====================================================================
        //  IUTKDropTarget — 이 창 위 드롭 = 소스 재드롭 소비(취소, 아이템 유지)
        // =====================================================================

        public bool CanDrop(UTKDragPayload payload)
        {
            return payload != null && payload.Item != null;
        }

        public bool Drop(UTKDragPayload payload)
        {
            if (payload == null || payload.Item == null) return false;
            // Loot 소스를 자기 창 위에 드롭 = 재드롭 취소(소비) — 아이템 유지. 인벤 이동은 InventoryWindowUTK가 담당.
            if (payload.Source == UTKDragSourceKind.Loot)
            {
                Debug.Log("[LootUTK] 전리품 소스 재드롭(창 위) — 취소 소비, 아이템 유지");
                return true;
            }
            return false;
        }

        // =====================================================================
        //  ③ 우클릭 즉시 획득 — 원본 TakeSelectedItem(→ ILootBasket.TakeItem → PlayerInventory.AddItem)
        // =====================================================================

        private void TakeSelectedItem(int index)
        {
            if (_basket == null) return;
            if (_basket.TakeItem(index))
            {
                Debug.Log("[LootUTK] 아이템 획득 완료 (우클릭)");
            }
            else
            {
                Debug.Log("[LootUTK] 아이템 획득 실패 (우클릭) — 인벤 가득 참 또는 바구니 소멸");
            }
            RefreshGrid();
        }

        /// <summary>풋터 "전부 습득하기" — 모든 항목 역순 TakeItem(Count 감소 대비).</summary>
        private void AcquireAll()
        {
            if (_basket == null) return;
            var items = _basket.Items;
            if (items == null) return;
            int n = items.Count;
            for (int i = n - 1; i >= 0; i--)
            {
                if (i < _basket.Items.Count)
                    TakeSelectedItem(i);
            }
            RefreshGrid();
            Debug.Log("[LootUTK] 전부 습득 요청 — " + n + "개 항목 처리");
        }
    }
}
