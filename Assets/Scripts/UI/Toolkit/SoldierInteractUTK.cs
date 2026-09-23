using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;      // PlayerInventory
using ProjectName.Systems;   // GuardPlaceholder, SoldierInteractBridge
using ProjectName.UI;        // ItemIconDatabase

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// P13 — 병사 통합 상호작용 창 (UI Toolkit).
    /// 원본: GuardPlaceholder.OnGUI 상호작용 패널 + DrawItemSelectionPopup (IMGUI) — 철거 후 이관.
    ///
    /// [진입점]
    ///  F키 → Systems(PlayerMovement) → SoldierInteractBridge.RaiseInteract(guard)
    ///       → 본 창 Open (Systems→UI 순환참조 회피 이벤트 경유 — SoldierInteractBridge 동일 패턴).
    ///  "📋 병사 정보보기" 버튼 → SoldierInteractBridge.Raise(guard) → GuardInfoUTK 정보창 (기존 경로 유지).
    ///
    /// [메뉴] [P30-B] 2분기(RebuildMenuForGuard) — 적병사(!IsAlly): 🗣️ 말걸기 / 💰 뇌물주기 / 💊 약주기 / 🤝 포섭 / 📋 병사 정보보기 / 🔙 닫기
    ///        아군병사(IsAlly): 기존 메뉴 그대로 유지(🥩 음식주기 등) — 아군 전용 신규 기능은 Phase 3.
    ///  - 말걸기/포섭: GuardPlaceholder public 래퍼(BeginTalk/BeginRecruit) → 기존 OnTalk/OnRecruit 로직 재사용.
    ///  - 음식/약: 아이템 선택 팝업을 UTK 스크롤 리스트로 재구현 (IMGUI DrawItemSelectionPopup 대체).
    ///    GuardPlaceholder.BeginFoodSelection/BeginDrugSelection → GetInventoryItemsByMode() 필터 재사용
    ///    → GiveFood/GiveDrug 지급(기존 GiveItemToGuard 경로, 데이터/규칙 불변).
    ///
    /// [자동 닫기] 200ms 자체 폴링(UTKWindowManager.Updater 미사용 — schedule.Execute):
    ///  대상 null / 사망 / 비활성 / 플레이어 거리 > 4.5m 시 Close + 선택 모드 해제.
    ///
    /// [규약] foreach만(읽기 전용) / public 멤버(CS0050) / UnityEngine.Debug /
    ///        IStyle 4면 개별 속성 / IMGUI(GUI.xxx) 금지 / USS gradient·url 절대경로 금지.
    /// </summary>
    public class SoldierInteractUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static SoldierInteractUTK _instance;
        public static SoldierInteractUTK Instance => _instance;

        public static void Ensure()
        {
            if (_instance == null)
                _instance = new SoldierInteractUTK();
        }

        /// <summary>병사 상호작용 창 열기 — 정적 진입점 (F키 브리지 구독 경유).</summary>
        public static void Open(GuardPlaceholder guard)
        {
            if (guard == null || !guard.IsAlive) return;
            Ensure();
            _instance.OpenForGuard(guard);
        }

        /// <summary>
        /// [P13 배선] F키 병사 상호작용 — Systems(PlayerMovement) 이벤트 → UTK 상호작용 창.
        /// GuardInfoUTK.BootstrapBridge와 동일 AfterSceneLoad 패턴.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapBridge()
        {
            SoldierInteractBridge.OnInteractRequested += guard => Open(guard);
        }

        // ===== 설정 =====
        private const float WinW = 360f;
        private const float WinH = 400f;
        private const float MaxInteractDistance = 4.5f;   // IMGUI 시절 _interactRange(3m)×1.5 — 동일 임계 유지
        private const long PollMs = 200L;                 // 유효성/거리/상태메시지 폴링 주기

        // =====================================================================
        //  [Figma GitHub-dark 리스타일] 병사 상호작용 창 한정 인라인 오버라이드 — 기능 무수정, 시각 전용.
        //  Theme.uss / 공용 UTKButton·UTKSlot·UTKWindowBase·타 UTK 창은 절대 수정하지 않는다.
        //  =====================================================================
        private static class GitHubDark
        {
            public static readonly Color BgBase   = Hex(0x0B0E14);   // 최배경 — 슬롯 인셋 바닥
            public static readonly Color Panel    = Hex(0x161B22);   // 창 본체 패널
            public static readonly Color PanelSub = Hex(0x21262D);   // 보조 패널(타이틀바/버튼)
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
                    baseBg = GitHubDark.Accent; hoverBg = new Color32(0x79, 0xC0, 0xFF, 0xFF); textColor = GitHubDark.Panel; break;
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

        /// <summary>다크 슬롯 베이스 — 공용 UTKSlot 인스턴스 한정 인라인(클래스 미수정): 인셋 바닥 #0B0E14.</summary>
        private static void ApplyDarkSlotBase(UTKSlot slot)
        {
            if (slot == null) return;
            slot.style.backgroundImage = new StyleBackground(StyleKeyword.None);   // 우드 베이크 이미지 제거
            slot.style.backgroundColor = GitHubDark.BgBase;                        // 인셋 다크 바닥 #0B0E14
            slot.style.borderTopWidth = slot.style.borderBottomWidth = slot.style.borderLeftWidth = slot.style.borderRightWidth = 1f;
            slot.style.borderTopLeftRadius = 6f;
            slot.style.borderTopRightRadius = 6f;
            slot.style.borderBottomLeftRadius = 6f;
            slot.style.borderBottomRightRadius = 6f;   // 서브 반경 r6
        }

        /// <summary>GitHub-dark 레어도 팔레트 — epic 퍼플 #A371F7, 희귀/전설/유니크 금색 #E3B341(인벤 패턴 동일).</summary>
        private static Color? RankColor(int rarityIndex)
        {
            switch (rarityIndex)
            {
                case 0: return GitHubDark.TextSub;                            // common — 보조그레이
                case 1:
                case 2: return GitHubDark.Accent;                             // uncommon/rare — 액센트
                case 3: return new Color32(0xA3, 0x71, 0xF7, 0xFF);           // epic — 퍼플
                case 4:
                case 5: return GitHubDark.Gold;                               // legendary/unique — 금색
                default: return null;                                         // 범위 밖 — 스트로크 유지
            }
        }

        /// <summary>슬롯 테두리에 레어도 색 인라인 적용(utk-rank--* USS 대체 — 이 창 한정).</summary>
        private static void ApplySlotRankBorder(UTKSlot slot, int rarityIndex)
        {
            if (slot == null) return;
            var rank = RankColor(rarityIndex);
            var c = rank ?? GitHubDark.Stroke;
            slot.style.borderTopColor = slot.style.borderBottomColor = slot.style.borderLeftColor = slot.style.borderRightColor = new StyleColor(c);
        }

        private enum ItemMode { None, Food, Drug }
        private ItemMode _itemMode = ItemMode.None;

        // ===== 상태 =====
        private GuardPlaceholder _guard;
        private GameObject _playerCache;
        private IVisualElementScheduledItem _pollTask;

        // ===== UI 참조 =====
        private Label _headerLabel;
        private Label _statusLabel;
        private VisualElement _menuSection;
        private VisualElement _itemSection;
        private Label _itemTitleLabel;
        private ScrollView _itemList;
        private Label _itemEmptyLabel;

        private SoldierInteractUTK() : base("병사 상호작용", new Vector2(WinW, WinH))
        {
            BuildHeader();
            BuildMenu();
            BuildItemSection();
            ApplyUIToolkitFont(this);
            ApplyGitHubDarkStyle();   // [GitHub-dark] 창 크롬 리스타일 — 이 창 한정 인라인
            style.display = DisplayStyle.None;
        }

        // =====================================================================
        //  콘텐츠 빌드
        // =====================================================================

        private void BuildHeader()
        {
            _headerLabel = new Label("");
            _headerLabel.name = "GuardHeader";
            _headerLabel.style.fontSize = 17f;
            _headerLabel.style.color = new StyleColor(GitHubDark.TextMain);   // [GitHub-dark] 기본 텍스트
            _headerLabel.style.marginLeft = 10f;
            _headerLabel.style.marginTop = 4f;
            _headerLabel.style.whiteSpace = WhiteSpace.Normal;
            _content.Add(_headerLabel);

            _statusLabel = new Label("");
            _statusLabel.name = "GuardStatus";
            _statusLabel.style.fontSize = 13f;
            _statusLabel.style.color = new StyleColor(GitHubDark.Accent);   // [GitHub-dark] 상태 메시지 — 액센트
            _statusLabel.style.marginLeft = 10f;
            _statusLabel.style.marginTop = 2f;
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            _content.Add(_statusLabel);
        }

        private void BuildMenu()
        {
            _menuSection = new VisualElement();
            _menuSection.name = "MenuSection";
            _menuSection.style.flexGrow = 1f;
            _menuSection.style.marginTop = 8f;
            // [F-UI Phase4] Figma interaction-panel — 2×2 액션 그리드(wrap row, 버튼 50%폭)
            _menuSection.style.flexDirection = FlexDirection.Row;
            _menuSection.style.flexWrap = Wrap.Wrap;
            _menuSection.style.alignItems = Align.Stretch;
            _menuSection.style.justifyContent = Justify.FlexStart;
            _content.Add(_menuSection);

            // [P30-B] 버튼은 여기서 채우지 않는다 — 생성 시점엔 _guard가 null(OpenForGuard에서 세팅).
            // RebuildMenuForGuard()가 OpenForGuard/Show 시점에 _guard.IsAlly에 따라 재구성한다.
        }

        /// <summary>
        /// [P30-B] 메뉴 재구성 — _guard.IsAlly에 따라 _menuSection을 다시 채운다 (멱등).
        /// [F-UI Phase4] Figma 액션 그리드 순서: 상태보기 / 대화하기 / [맥락1] / [맥락2]
        ///  적병사(!IsAlly): 상태보기 / 대화하기 / 뇌물주기 / 포섭하기
        ///  아군병사(IsAlly): 상태보기 / 대화하기 / 물약주기 / 음식주기
        /// 상단 헤더 ✕ 버튼이 닫기 역할(메뉴에 별도 닫기 미포함 — Figma 4칸 그리드와 정합).
        /// </summary>
        private void RebuildMenuForGuard()
        {
            if (_menuSection == null) return;
            _menuSection.Clear();

            // 1) 상태보기 / 2) 대화하기 (공통)
            AddMenuButton("상태보기", OnInfoClicked, UTKButton.Variant.Primary);
            AddMenuButton("대화하기", OnTalkClicked, UTKButton.Variant.Secondary);

            if (_guard != null && _guard.IsAlly)
            {
                // 아군병사 — 물약주기 / 음식주기 (맥락 페어)
                AddMenuButton("물약주기", () => ShowItemSection(ItemMode.Drug), UTKButton.Variant.Secondary);
                AddMenuButton("음식주기", () => ShowItemSection(ItemMode.Food), UTKButton.Variant.Secondary);
            }
            else
            {
                // 적병사 — 뇌물주기 / 포섭하기 (맥락 페어), 뇌물 단가 표시
                int bribeCost = _guard != null ? GuardLoyaltySystem.GetBribeCost(_guard.Level) : GuardLoyaltySystem.GetBribeCost(1);
                AddMenuButton($"뇌물주기 ({bribeCost}골드)", OnBribeClicked, UTKButton.Variant.Secondary);
                AddMenuButton("포섭하기", OnRecruitClicked, UTKButton.Variant.Secondary);
            }
        }

        private void AddMenuButton(string text, System.Action onClick, UTKButton.Variant variant)
        {
            var btn = UTKButton.Create(text, onClick, variant);
            StyleButton(btn, variant);   // [GitHub-dark] 메뉴 버튼 인라인 리스타일
            // [F-UI Phase4] Figma 2×2 액션 그리드 — 버튼 50%폭(2열 wrap), compact
            btn.style.width = new Length(50f, LengthUnit.Percent);
            btn.style.height = 48f;
            btn.style.marginTop = 4f;
            btn.style.marginBottom = 2f;
            btn.style.marginLeft = 3f;
            btn.style.marginRight = 3f;
            btn.style.fontSize = 15f;
            _menuSection.Add(btn);
        }

        private void BuildItemSection()
        {
            _itemSection = new VisualElement();
            _itemSection.name = "ItemSection";
            _itemSection.style.flexGrow = 1f;
            _itemSection.style.display = DisplayStyle.None;
            _content.Add(_itemSection);

            _itemTitleLabel = new Label("🥩 음식 선택");
            _itemTitleLabel.name = "ItemTitle";
            _itemTitleLabel.style.fontSize = 17f;
            _itemTitleLabel.style.color = new StyleColor(GitHubDark.Gold);   // [GitHub-dark] 섹션 제목 — 골드
            _itemTitleLabel.style.marginLeft = 10f;
            _itemTitleLabel.style.marginTop = 4f;
            _itemSection.Add(_itemTitleLabel);

            // 인벤토리 아이템 스크롤 리스트 (IMGUI BeginScrollView 대체)
            _itemList = new ScrollView(ScrollViewMode.Vertical);
            _itemList.name = "ItemList";
            _itemList.style.flexGrow = 1f;
            _itemList.style.marginTop = 6f;
            _itemList.style.marginLeft = 10f;
            _itemList.style.marginRight = 10f;
            _itemSection.Add(_itemList);

            _itemEmptyLabel = new Label("보유한 아이템이 없습니다.");
            _itemEmptyLabel.name = "ItemEmpty";
            _itemEmptyLabel.style.fontSize = 13f;
            _itemEmptyLabel.style.color = new StyleColor(GitHubDark.TextSub);   // [GitHub-dark] 보조 텍스트
            _itemEmptyLabel.style.marginLeft = 10f;
            _itemEmptyLabel.style.marginTop = 10f;
            _itemSection.Add(_itemEmptyLabel);

            var cancelBtn = UTKButton.Create("🔙 취소", ShowMenu, UTKButton.Variant.Secondary);
            cancelBtn.style.height = 36f;
            cancelBtn.style.marginTop = 6f;
            cancelBtn.style.marginLeft = 10f;
            cancelBtn.style.marginRight = 10f;
            cancelBtn.style.marginBottom = 6f;
            _itemSection.Add(cancelBtn);
        }

        // =====================================================================
        //  생명주기
        // =====================================================================

        /// <summary>대상 지정 후 열기 — 이미 열려있으면 대상만 교체.</summary>
        public void OpenForGuard(GuardPlaceholder guard)
        {
            if (guard == null) return;
            _guard = guard;
            RebuildMenuForGuard();   // [P30-B] 대상 확정 후 메뉴 2분기 (적=뇌물 / 아군=기존)
            if (IsOpen)
            {
                // 다른 병사로 대상 교체 — UI 즉시 갱신 (base.Show는 IsOpen 시 no-op)
                RefreshHeader();
                RefreshStatus();
            }
            Show();
        }

        public override void Show()
        {
            bool wasOpen = IsOpen;
            base.Show();
            if (wasOpen) return;

            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            CenterOnParent();

            _playerCache = null;
            RefreshHeader();
            RebuildMenuForGuard();   // [P30-B] _guard.IsAlly 기준 메뉴 2분기 (멱등)
            ShowMenu();
            StartPoll();
            PlaceNearGuard();
            Debug.Log("[SoldierInteractUTK] 병사 상호작용 창 열림: " + _guard.GuardName);
        }

        public override void Hide()
        {
            base.Hide();
            StopPoll();
            if (_guard != null)
            {
                _guard.CancelItemSelection();   // 아이템 선택 중이던 모드 해제 (데이터/규칙 불변)
                _guard = null;
            }
            Debug.Log("[SoldierInteractUTK] 병사 상호작용 창 닫힘");
        }

        // =====================================================================
        //  자체 폴링 (200ms) — 유효성/거리/상태메시지
        // =====================================================================

        private void StartPoll()
        {
            if (_pollTask != null) return;
            _pollTask = schedule.Execute(PollTick).Every(PollMs);
        }

        private void StopPoll()
        {
            if (_pollTask != null)
            {
                _pollTask.Pause();
                _pollTask = null;
            }
        }

        private void PollTick()
        {
            if (!IsOpen) return;

            GuardPlaceholder guard = _guard;
            // 대상 무효 (null / 사망 / 비활성) → 자동 닫기
            if (guard == null || !guard.IsAlive || !guard.gameObject.activeInHierarchy)
            {
                Close();
                return;
            }

            // 거리 폴링 — 플레이어 캐시 (GuardPlaceholder와 동일 "Player" 태그)
            if (_playerCache == null || !_playerCache.activeInHierarchy)
                _playerCache = GameObject.FindGameObjectWithTag("Player");
            if (_playerCache != null)
            {
                float dist = Vector3.Distance(guard.transform.position, _playerCache.transform.position);
                if (dist > MaxInteractDistance)
                {
                    Close();
                    return;
                }
            }

            // 상태 메시지 갱신 (말걸기/지급/포섭 응답 + 시스템 메시지 폴링 반영)
            RefreshStatus();

            // [P16-1] 병사 이동 시 창 추적 — 드래그로 사용자가 옮긴 뒤에는 유지하지 않음(단순 폴링 추적)
            PlaceNearGuard();
        }

        // =====================================================================
        //  메뉴 동작
        // =====================================================================

        private void OnTalkClicked()
        {
            if (_guard == null) return;
            _guard.BeginTalk();     // 기존 OnTalk 로직 재사용
            RefreshStatus();
        }

        private void OnRecruitClicked()
        {
            if (_guard == null) return;
            _guard.BeginRecruit();  // 기존 OnRecruit 로직 재사용
            RefreshStatus();
        }

        /// <summary>[P30-B] 💰 뇌물주기 — 적병사 전용(아군이면 무시). GuardPlaceholder.AttemptBribe(골드 차감+호감도) 경유.</summary>
        private void OnBribeClicked()
        {
            if (_guard == null || _guard.IsAlly) return;   // 아군 뇌물 금지 — 클릭 무시
            _guard.AttemptBribe();
            RefreshStatus();
        }

        /// <summary>📋 병사 정보보기 — 기존 GuardInfoUTK 구독 경로(SoldierInteractBridge.Raise) 유지.</summary>
        private void OnInfoClicked()
        {
            if (_guard == null) return;
            SoldierInteractBridge.Raise(_guard);
        }

        // =====================================================================
        //  아이템 선택 (음식/약) — IMGUI DrawItemSelectionPopup의 UTK 재구현
        // =====================================================================

        private void ShowItemSection(ItemMode mode)
        {
            if (_guard == null) return;
            _itemMode = mode;
            if (mode == ItemMode.Food) _guard.BeginFoodSelection();
            else _guard.BeginDrugSelection();

            _menuSection.style.display = DisplayStyle.None;
            _itemSection.style.display = DisplayStyle.Flex;
            _itemTitleLabel.text = mode == ItemMode.Food ? "🥩 음식 선택" : "💊 약 선택";
            RebuildItemList();
        }

        /// <summary>메뉴로 복귀 + 병사 쪽 선택 모드 해제.</summary>
        private void ShowMenu()
        {
            _itemMode = ItemMode.None;
            _menuSection.style.display = DisplayStyle.Flex;
            _itemSection.style.display = DisplayStyle.None;
            if (_guard == null) return;
            _guard.CancelItemSelection();
            RefreshStatus();
        }

        /// <summary>
        /// 아이템 목록 재구성 — GuardPlaceholder.GetInventoryItemsByMode() 필터 재사용
        /// (음식 = Food / 약 = Potion+Drug, 기존 IMGUI와 동일 규칙).
        /// </summary>
        private void RebuildItemList()
        {
            _itemList.Clear();
            var items = _guard != null ? _guard.GetInventoryItemsByMode() : null;
            if (items == null || items.Count == 0)
            {
                _itemEmptyLabel.style.display = DisplayStyle.Flex;
                return;
            }
            _itemEmptyLabel.style.display = DisplayStyle.None;
            foreach (var pair in items)
                _itemList.Add(BuildItemRow(pair.Key, pair.Value));
        }

        private VisualElement BuildItemRow(PlayerInventory.ItemData item, int count)
        {
            var row = new VisualElement();
            row.name = "ItemRow";
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.height = 48f;
            row.style.marginBottom = 4f;
            row.style.paddingLeft = 4f;
            row.style.paddingRight = 4f;

            var slot = new UTKSlot();
            slot.name = "ItemIcon";
            ApplyDarkSlotBase(slot);   // [GitHub-dark] 슬롯 = 인셋 다크 바닥 (bg_slot.png 제거)
            slot.style.width = 38f;
            slot.style.height = 38f;
            slot.style.flexShrink = 0f;
            slot.SetIcon(ItemIconDatabase.GetOrCreateIcon(item));
            slot.SetRank(UTKRarity.ClassForIndex((int)item.rarity));
            ApplySlotRankBorder(slot, (int)item.rarity);   // [GitHub-dark] 레어도 테두리 인라인
            row.Add(slot);

            var name = new Label(!string.IsNullOrEmpty(item.displayName) ? item.displayName : item.id);
            name.style.fontSize = 14f;
            name.style.color = new StyleColor(GitHubDark.TextMain);   // [GitHub-dark] 기본 텍스트
            name.style.marginLeft = 8f;
            name.style.flexGrow = 1f;
            row.Add(name);

            var countLabel = new Label("x" + count);
            countLabel.style.fontSize = 13f;
            countLabel.style.color = new StyleColor(GitHubDark.TextSub);   // [GitHub-dark] 보조 텍스트
            countLabel.style.marginRight = 8f;
            row.Add(countLabel);

            var giveBtn = UTKButton.Create("주기", () => GiveItem(item), UTKButton.Variant.Primary);
            StyleButton(giveBtn, UTKButton.Variant.Primary);   // [GitHub-dark] 지급 버튼 인라인 리스타일
            giveBtn.style.width = 64f;
            giveBtn.style.height = 30f;
            giveBtn.style.flexShrink = 0f;
            row.Add(giveBtn);

            return row;
        }

        /// <summary>
        /// 아이템 지급 — [P30-C] 아군 분기: 음식=GiveAllyFood(회복+호감 유지) / 약=ApplyAllyPotion(PotionBuffData 버프 실행 —
        /// 중독 아님, 아이템 제거는 래퍼 내부). 적병사: 기존 GiveFood/GiveDrug(중독) 경로 그대로.
        /// </summary>
        private void GiveItem(PlayerInventory.ItemData item)
        {
            if (_guard == null || item == null) return;
            if (_guard.IsAlly)
            {
                // [P30-C] 아군 — 약은 버프 실행(적병사와 달리 중독 경로 미사용)
                if (_itemMode == ItemMode.Food) _guard.GiveAllyFood(item);
                else _guard.ApplyAllyPotion(item);
            }
            else
            {
                if (_itemMode == ItemMode.Food) _guard.GiveFood(item);
                else _guard.GiveDrug(item);
            }
            ShowMenu();   // 지급 로직이 _selectionMode를 해제하지 않으므로 복귀 시 해제
        }

        // =====================================================================
        //  표시 갱신 / 헬퍼
        // =====================================================================

        private void RefreshHeader()
        {
            if (_guard == null) return;
            _headerLabel.text = $"⚔️ {_guard.GuardName} Lv.{_guard.Level} ({_guard.Nation}) — 호감도 {_guard.Loyalty:F0}";
        }

        /// <summary>
        /// 병사 상태메시지 표시 (GuardPlaceholder.StatusMessage public 프로퍼티).
        /// [P30-C] 아군이고 임시 버프 활성 중이면 상태메시지 위에 버프 잔여 한 줄 추가.
        /// </summary>
        private void RefreshStatus()
        {
            if (_guard == null) return;
            string msg = _guard.StatusMessage;
            msg = string.IsNullOrEmpty(msg) ? "무슨 일이냐?" : msg;

            if (_guard.IsAlly && _guard.AllyBuffRemaining > 0f)
                msg = $"⚡공격+{_guard.AllyAttackBuff:0} 🛡방어+{_guard.AllyDefenseBuff:0} 💨민첩+{_guard.AllyAgilityBuff:0} ({_guard.AllyBuffRemaining:0}초)\n{msg}";

            _statusLabel.text = msg;
        }

        /// <summary>
        /// [P16-1] 병사 바로 옆(오른쪽) 배치 — 병사 월드좌표 → 패널 좌표 변환(UTKDragDrop.GetPanelPointerPos
        /// 동일 수식: 마우스 실측×스케일 + y플립). 화면 밖 보정 포함.
        /// </summary>
        private void PlaceNearGuard()
        {
            var root = UIToolkitBootstrap.UIRoot;
            var cam = Camera.main;
            if (root == null || cam == null || _guard == null) { CenterOnParent(); return; }

            Vector3 sp = cam.WorldToScreenPoint(_guard.transform.position + Vector3.up * 1.2f);
            if (sp.z < 0f) { CenterOnParent(); return; }   // 카메라 뒤 — 중앙 폴백

            float scale = root.worldBound.width / (float)Screen.width;
            if (scale <= 0f) scale = 1f;
            float px = sp.x * scale;
            float py = root.worldBound.height - sp.y * scale;

            float winW = resolvedStyle.width > 0f ? resolvedStyle.width : WinW;
            float winH = resolvedStyle.height > 0f ? resolvedStyle.height : WinH;

            // 병사 오른쪽 20px 오프셋 — 화면 밖이면 왼쪽/클램프
            float left = px + 20f;
            if (left + winW > root.worldBound.width - 8f)
                left = px - winW - 20f;
            left = Mathf.Clamp(left, 8f, Mathf.Max(8f, root.worldBound.width - winW - 8f));
            float top = Mathf.Clamp(py - winH * 0.5f, 8f, Mathf.Max(8f, root.worldBound.height - winH - 8f));

            style.left = left;
            style.top = top;
        }

        private void CenterOnParent()
        {
            var par = parent;
            if (par == null) return;
            float pw = par.resolvedStyle.width;
            float ph = par.resolvedStyle.height;
            if (pw <= 0f || ph <= 0f) return;
            style.left = Mathf.Max(0f, (pw - resolvedStyle.width) * 0.5f);
            style.top = Mathf.Max(0f, (ph - resolvedStyle.height) * 0.5f);
        }
    }
}
