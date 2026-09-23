using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;            // ILootBasket, LootEntry, PlayerInventory
using ProjectName.UI;              // ItemIconDatabase

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U2 Round 2A — 전리품 윈도우 (LootWindow 796줄 IMGUI → UTK 포팅).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/UI/LootWindow.cs — 절대 수정하지 않는다.
    ///
    /// [포팅 범위]
    ///  ① 바구니 항목 리스트 — ILootBasket.Items(전리품 바구니 소스)의 각 LootEntry를
    ///                        UTKSlot(아이콘+이름+카운트) 행으로 표시. 매 갱신 재조회(즉시 반영).
    ///  ② 행 드래그 → 인벤  — IUTKDragSource 구현: 행 좌클릭 드래그가
    ///                        UTKDragPayload(SourceKind.Loot, SourceIndex=항목 인덱스)를 생성.
    ///                        인벤토리(InventoryWindowUTK)가 수신해 PlayerInventory.AddItem 수행.
    ///  ③ 행 우클릭 획득    — 원본 TakeSelectedItem(→ ILootBasket.TakeItem → PlayerInventory.AddItem) 동일 데이터 경로.
    ///  ④ 좌측 히스토리 없음 — 단순 목록 + 닫기(제공되는 닫기 버튼).
    ///  ⑤ 변화 폴링 갱신    — 바구니 항목 배열을 주기 재조회해 즉시 갱신.
    ///  ⑥ 빈바구니 처리     — 원본 재조회 규약: 바구니가 비어있거나 회수 불가하면 자동 Hide + 참조 해제.
    ///  static Open(basket)/Ensure 진입점. 우측 배치 관례(화면 2/3 + 6, 높이 Screen-180).
    ///  각 경로에 [LootUTK] Debug.Log 실측 로그.
    /// </summary>
    public class LootWindowUTK : UTKWindowBase, IUTKDragSource, IUTKDropTarget
    {
        // ===== 싱글턴 / 팩토리 =====
        private static LootWindowUTK _instance;
        public static LootWindowUTK Instance => _instance;

        /// <summary>팩토리 — UIRoot 우측 배치 관례(화면 2/3 + 6). 멱등.</summary>
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

        // ===== 설정 =====
        private const long RefreshMs = 250L;
        private const float RowHeight = 56f;
        private const float IconSize = 44f;

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
            public static readonly Color Gold     = Hex(0xE3B341);   // 희귀/활성/골드
            public static readonly Color TextMain = Hex(0xF0F6FC);   // 기본 텍스트
            public static readonly Color TextSub  = Hex(0x8B949E);   // 보조 텍스트
            public static readonly Color Stroke   = Hex(0x2E343D);   // 테두리/구분선
            public static readonly Color Danger   = Hex(0xF85149);   // danger 버튼

            private static Color Hex(uint rgb) =>
                new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);
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
            // 창 본체: bg_window.png/브론즈 베벨 2px → 다크 패널 + 1px 스트로크 + r8 (이 창에서만)
            style.backgroundColor = GitHubDark.Panel;
            style.backgroundImage = new StyleBackground(StyleKeyword.None);
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 1f;
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = GitHubDark.Stroke;
            style.borderTopLeftRadius = 8f;
            style.borderTopRightRadius = 8f;
            style.borderBottomLeftRadius = 8f;
            style.borderBottomRightRadius = 8f;   // 메인 반경 r8
            style.color = GitHubDark.TextMain;   // 명시색 없는 라벨 상속색 — 기본 텍스트

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

        /// <summary>GitHub-dark 리스트 행 — 보조 패널 #21262D 바탕 + 1px 스트로크 + r6 (이 창 한정).</summary>
        private static void ApplyDarkRowStyle(VisualElement row)
        {
            if (row == null) return;
            row.style.backgroundImage = new StyleBackground(StyleKeyword.None);   // 우드 슬롯 베이크 이미지 제거
            row.style.backgroundColor = GitHubDark.PanelSub;
            row.style.borderTopWidth = row.style.borderBottomWidth = row.style.borderLeftWidth = row.style.borderRightWidth = 1f;
            row.style.borderTopColor = row.style.borderBottomColor = row.style.borderLeftColor = row.style.borderRightColor = new StyleColor(GitHubDark.Stroke);
            row.style.borderTopLeftRadius = 6f;
            row.style.borderTopRightRadius = 6f;
            row.style.borderBottomLeftRadius = 6f;
            row.style.borderBottomRightRadius = 6f;   // 서브 반경 r6
        }

        // ===== 레퍼런스 =====
        private ILootBasket _basket;
        private readonly VisualElement _list;
        private readonly Label _countLabel;
        private readonly Label _emptyLabel;
        private UnityEngine.UIElements.IVisualElementScheduledItem _refreshTask;

        private LootWindowUTK() : base("🎁 전리품", new Vector2(380f, 520f))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            _countLabel = new Label("");
            _countLabel.style.fontSize = 13f;
            _countLabel.style.color = new StyleColor(GitHubDark.TextSub);   // [GitHub-dark] 보조 텍스트
            _content.Add(_countLabel);

            _list = new VisualElement();
            _list.name = "LootList";
            _list.style.flexDirection = FlexDirection.Column;
            _list.style.flexGrow = 1f;
            _list.style.marginTop = 6f;
            _content.Add(_list);

            _emptyLabel = new Label("(전리품이 없습니다)");
            _emptyLabel.style.fontSize = 14f;
            _emptyLabel.style.color = new StyleColor(GitHubDark.TextSub);   // [GitHub-dark] 보조 텍스트
            _emptyLabel.style.marginTop = 12f;
            _content.Add(_emptyLabel);

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
            RefreshList();
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
            if (IsOpen) RefreshList();
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
        //  ① 바구니 항목 리스트 — 매 갱신 재조회 (⑥ 빈바구니 자동 Hide)
        // =====================================================================

        /// <summary>[U8] 우클릭 즉시 획득 — 기존 TakeSelectedItem 데이터 경로.</summary>
        private void TakeLootRow(int index)
        {
            TakeSelectedItem(index);
        }

        private void RefreshList()
        {
            // ⑥ 원본 RefreshLoot 규약 — 바구니 없음/빈/회수 불가 → 참조 해제 + 닫힘
            if (_basket == null || _basket.IsEmpty || !_basket.IsAvailable)
            {
                _basket = null;
                _list.Clear();
                if (IsOpen)
                    Hide();
                return;
            }

            var items = _basket.Items;
            int total = items != null ? items.Count : 0;

            // 이전 행 이벤트/타겟 정리
            _list.Clear();

            for (int i = 0; i < total; i++)
            {
                var entry = items[i];
                if (entry == null || entry.Item == null || entry.Count <= 0) continue;
                _list.Add(BuildRow(entry, i));
            }

            _countLabel.text = "아이템 " + total + "개";
            Debug.Log("[LootUTK] 바구니 목록 갱신: " + total + "개 항목");
        }

        private VisualElement BuildRow(LootEntry entry, int index)
        {
            var row = new VisualElement();
            row.name = "LootRow_" + index;
            row.AddToClassList("utk-slot");
            ApplyDarkRowStyle(row);   // [GitHub-dark] 행 = 보조 패널 리스트 아이템 (bg #21262D + 1px 스트로크 + r6)
            row.style.flexDirection = FlexDirection.Row;
            row.style.height = RowHeight;
            row.style.marginBottom = 4f;
            row.style.alignItems = Align.Center;

            // 아이콘
            var slotIcon = new VisualElement();
            slotIcon.style.width = IconSize;
            slotIcon.style.height = IconSize;
            slotIcon.style.marginRight = 8f;
            var icon = ItemIconDatabase.GetOrCreateIcon(entry.Item);
            slotIcon.style.backgroundImage = UTKTextureSafe.ToBackground(icon);
            row.Add(slotIcon);

            // 이름
            var nameLabel = new Label(entry.Item.displayName);
            nameLabel.style.flexGrow = 1f;
            nameLabel.style.fontSize = 15f;
            nameLabel.style.color = new StyleColor(GitHubDark.TextMain);   // [GitHub-dark] 기본 텍스트
            row.Add(nameLabel);

            // 카운트
            var countLabel = new Label("x" + entry.Count);
            countLabel.style.fontSize = 13f;
            countLabel.style.color = new StyleColor(GitHubDark.Gold);   // [GitHub-dark] 카운트 — 골드
            row.Add(countLabel);

            // ② 행 드래그 소스 (좌클릭) — 임계거리 미만 클릭 = 우클릭 아닌 좌클릭 획득은 하지 않음(원본은 인벤 열림 시 드롭 판정)
            UTKDragDrop.MakeDraggable(row, () => MakePayload(index, entry.Item), null, () => TakeLootRow(index));

            return row;
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
            Debug.Log("[LootUTK] 행 드래그 시작: " + (payload != null && payload.Item != null ? payload.Item.displayName : "?"));
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
            RefreshList();
        }
    }
}