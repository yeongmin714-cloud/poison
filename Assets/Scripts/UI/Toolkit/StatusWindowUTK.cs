using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// 캐릭터 정보 창 v2 — UI Toolkit 포팅 (Phase U1 파일럿).
    /// 원본: Assets/Scripts/UI/StatusWindowUI.cs (IMGUI/Canvas, 802줄) — 본 파일은 그 데이터 소스와
    /// 갱신 로직만 이식한 UTKWindowBase 파생. 원본은 절대 수정하지 않는다.
    ///
    /// [정보 계층 — 원본과 동일]
    ///  - 좌측: 3D 미리보기 자리(Viewport Placeholder) + ㄷ자 장비슬롯 6개(투구/상의/무기/장갑/신발/등) + 장비 보너스 내역
    ///  - 우측: 주스탯(힘/민첩/지능/체력 + [+] 분배, 남은 포인트) → 전투 스탯 8행(공격/방어/치명/속도/연금/요리/화술/골드
    ///           + 계산 근거 노트) → 경험치/체력 게이지 → 중독 정보.
    ///  - 3D 뷰포트(전용 카메라 + 모델 클론)는 데이터가 아닌 시각 요소라 Phase U1에서는 Viewport Placeholder로 축약
    ///    (복잡도 허용 규약). 레벨업 팝업은 UTKToastService 토스트로 대응.
    ///
    /// [P3 Figma character-status-panel 재구성] 표시 레이아웃만 Figma 구조로 재배열 — 게임 로직 100% 보존:
    ///  - 좌측열: IdentityRow(등급/레벨 배지; 캐릭터명 데이터 없음) → Portrait(3D placeholder + HP 오버레이)
    ///    → StatsSection(LevelBlock "LEVEL &lt;n&gt; EXP &lt;비율%&gt;" + CoreStatsGrid 공격/방어/최대체력/민첩)
    ///    → 특수 상태(허기/중독도 컴팩트 게이지)
    ///  - 중앙열: 기존 장비슬롯 6개 + 장비 보너스 내역 (이동 배치 — 로직/구성 무수정)
    ///  - 우측열: 기존 상세 전부 보존 (주스탯 분배/전투 스탯/게이지/중독/칭호/감사 리포트)
    ///  - 데이터 소스 동일: TitleManager/PlayerStats/PlayerHealth/HungerSystem/DrugEffectSystem/EquipmentManager
    ///
    /// [소유권]
    ///  PlayerStats / PlayerHealth / DrugEffectSystem / EquipmentManager / EquipmentStatBonusApplier는 타 소유
    ///  — 공개 API 소비만. 비즈니스 로직 복제 금지.
    ///
    /// [GitHub-dark 리스타일] 이 창 한정 인라인 오버라이드 — 공용 UTKColor(브론즈/우드 톤) 대신
    /// 로컬 GitHubDark 팔레트(배경 #0B0E14 / 패널 #161B22 / 보조 #21262D / 액센트 #58A6FF /
    /// 골드 #E3B341 / 텍스트 #F0F6FC / 보조텍스트 #8B949E / 스트로크 #2E343D) 사용.
    /// 기능 로직(스탯/칭호/레벨 표시, 구독 위생, 폴링 갱신)은 무수정. 공용 파일/타 창은 건드리지 않는다.
    /// </summary>
    public class StatusWindowUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 부트스트랩 =====
        private static StatusWindowUTK _instance;
        private static StatusWindowUTK _statusOnlyInstance;
        public static StatusWindowUTK Instance => _instance;

        /// <summary>Explicit Figma 83:4 status-only route. Does not alter the P-key 84:4 composition.</summary>
        public static void OpenStatusOnly()
        {
            StatusWindowUTK window = EnsureStatusOnlyInstance();
            if (window != null) window.Show();
        }

        /// <summary>Toggle only the explicitly selected Figma 83:4 status-only route.</summary>
        public static void ToggleStatusOnly()
        {
            StatusWindowUTK window = EnsureStatusOnlyInstance();
            if (window != null) window.Toggle();
        }

        private static StatusWindowUTK EnsureStatusOnlyInstance()
        {
            if (_statusOnlyInstance != null) return _statusOnlyInstance;
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return null;

            _statusOnlyInstance = new StatusWindowUTK(true);
            root.Add(_statusOnlyInstance);
            var go = new GameObject("StatusWindowUTK_StatusOnly");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Updater>().Configure(_statusOnlyInstance, false);
            return _statusOnlyInstance;
        }

        /// <summary>Figma 83:4 frame-local CharacterStatusPanel bounds.</summary>
        public static readonly Rect StatusOnlyCanvasBounds = new Rect(672f, 72f, 576f, 936f);
        private const float StatusOnlyFrameWidth = 1920f;
        private const float StatusOnlyFrameHeight = 1080f;
        private readonly bool _statusOnly;


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            // StatusWindowUI owns the legacy fallback when the Toolkit root could not be built.
            // Do not install a second P/ESC poller with no renderable UTK route.
            if (UIToolkitBootstrap.UIRoot == null) return;
            _instance = new StatusWindowUTK();
            var go = new GameObject("StatusWindowUTK");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Updater>().Configure(_instance, true);
            Debug.Log("[StatusWindowUTK] 초기화 완료 — P키로 토글");
        }

        // ===== 설정 =====
        // Figma 84:4 frame-local panel bounds (frame absolute origin is x=29618, y=3).
        public static readonly Rect CharacterStatusCanvasBounds = new Rect(391.8f, 72f, 576f, 936f);
        public static readonly Rect EquipmentCanvasBounds = new Rect(996.6f, 72f, 531.6f, 936f);
        public static readonly Rect CompositionCanvasBounds = new Rect(391.8f, 72f, 1136.4f, 936f);
        private static readonly Rect StatusPanelLocalBounds = new Rect(0f, 0f, 576f, 936f);
        private static readonly Rect EquipmentPanelLocalBounds = new Rect(604.8f, 0f, 531.6f, 936f);
        private const float WinW = 1136.4f;
        private const float WinH = 936f;

        // =====================================================================
        //  [GitHub-dark 리스타일] 캐릭터 정보 창 한정 인라인 오버라이드 — 기능 무수정, 시각 전용.
        //  Theme.uss / 공용 UTKColor·UTKButton·UTKSlot·UTKWindowBase·타 UTK 창은 절대 수정하지 않는다.
        //  =====================================================================
        private static class GitHubDark
        {
            public static readonly Color BgBase   = Hex(0x0B0E14);   // 최배경 — 슬롯/게이지 인셋
            public static readonly Color Panel    = Hex(0x161B22);   // 창 본체 패널
            public static readonly Color PanelSub = Hex(0x21262D);   // 보조 패널(타이틀바/행/게이지 트랙)
            public static readonly Color Accent   = Hex(0x58A6FF);   // 강조(액센트)
            public static readonly Color Gold     = Hex(0xE3B341);   // 섹션 헤더 골드
            public static readonly Color TextMain = Hex(0xF0F6FC);   // 기본 텍스트(값)
            public static readonly Color TextSub  = Hex(0x8B949E);   // 보조 텍스트(라벨)
            public static readonly Color Stroke   = Hex(0x2E343D);   // 테두리/구분선
            public static readonly Color Danger   = Hex(0xF85149);   // danger 톤
            public static readonly Color RankEpic = Hex(0xA371F7);   // epic 퍼플 — 중독/상태 라인
            public static readonly Color Health   = Hex(0x3FB950);   // GitHub success green — HP 게이지(건강색)
            public static readonly Color SysStable = Hex(0x56E39F);  // [Figma 84:73] SYS_EQ_STABLE — [144차 P1-EQ B11] #3FB950→#56E39F

            private static Color Hex(uint rgb) =>
                new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);
        }

        /// <summary>[Figma TierStrip] 장착 아이템 희귀색 — WarehouseWindowUTK와 동일 매핑(창 한정 인라인).</summary>
        private static Color RarityColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Uncommon: return new Color32(0x8B, 0x94, 0x9E, 0xFF);
                case ItemRarity.Rare: return new Color32(0x1F, 0x6F, 0xEB, 0xFF);
                case ItemRarity.Epic: return new Color32(0xBC, 0x8C, 0xFF, 0xFF);
                case ItemRarity.Legendary: return new Color32(0xE3, 0xB3, 0x41, 0xFF);
                default: return new Color32(0x58, 0xA6, 0xFF, 0xFF);   // Common
            }
        }

        // ===== 전역 주소 지정 테이블 =====
        private static readonly PlayerStats.StatKind[] _statKinds =
        {
            PlayerStats.StatKind.Str, PlayerStats.StatKind.Agi, PlayerStats.StatKind.Int, PlayerStats.StatKind.Vit
        };
        private static readonly string[] _statKindNames = { "힘", "민첩", "지능", "체력" };
        private static readonly string[] _statKindEffects =
            { "공격 +2/pt", "치명+0.5% 속도+0.05 /pt", "연금·요리 +0.5% /pt", "최대체력 +10 /pt" };
        private static readonly string[] _rowNames =
            { "공격력", "방어력", "치명타", "이동속도", "연금술 성공", "요리 성공", "화술", "골드" };
        // [Figma 84:4 + 사용자 확정 2026-10-07] 10슬롯 5×2 — 방패=Back 실존 슬롯, Ring/Necklace 신설(말단, 직렬화 인덱스 보존).
        private static readonly EquipmentManager.EquipmentSlot[] _equipOrder =
        {
            EquipmentManager.EquipmentSlot.Helmet, EquipmentManager.EquipmentSlot.Armor,
            EquipmentManager.EquipmentSlot.Weapon, EquipmentManager.EquipmentSlot.Shoes,
            EquipmentManager.EquipmentSlot.Gloves, EquipmentManager.EquipmentSlot.Back,
            EquipmentManager.EquipmentSlot.Mask, EquipmentManager.EquipmentSlot.Bag,
            EquipmentManager.EquipmentSlot.Ring, EquipmentManager.EquipmentSlot.Necklace
        };
        private static readonly string[] _equipNames = { "투구", "갑옷", "무기", "신발", "장갑", "방패", "방독면", "가방", "반지", "목걸이" };
        private VisualElement[] _equippedIconHosts = new VisualElement[10];   // [144차 P1-EQ B27] EP 셀 아이콘 호스트(38.4 중앙 — 툴팁=아이템명)
        private readonly UTKSlot[] _inventoryPackCells = new UTKSlot[25];     // [144차 P0-② B37] 전술 배낭 25셀 — UTKSlot(TierStrip/Count 내장)
        private VisualElement _inventoryPackGrid;
        private VisualElement _inventoryPackScroll;

        // [144차 P1-CSP #34] Figma 83:45 라벨 문구 — 괄호 단위 표기 포함.
        private static readonly string[] _coreStatNames = { "공격력 (ATK)", "방어력 (DEF)", "최대 체력 (HP)", "민첩성 (AGI)" };

        // ===== 갱신 대상 레퍼런스 =====
        private Label _pendingLabel;
        private readonly Label[] _allocValueLabels = new Label[4];
        private readonly Button[] _allocButtons = new Button[4];
        private readonly Label[] _statValueLabels = new Label[8];
        private readonly Label[] _bonusNoteLabels = new Label[8];
        private readonly Label[] _equipSlotLabels = new Label[10];   // index = EquipmentSlot enum (Ring=8, Necklace=9)
        private readonly VisualElement[] _equipTierStrips = new VisualElement[10];   // [Figma] TierStrip per slot
        private Label _bonusListLabel;
        private Label _addictionLabel;
        private Label _titleValueLabel;   // [O6] 칭호 표시
        private bool _titleSubscribed;   // [O6] 칭호 이벤트 구독 플래그
        private Label _expValueLabel, _hpValueLabel;
        private VisualElement _expFill, _hpFill;
        private Label _hungerValueLabel;  // [O10] 허기 게이지 표시
        private VisualElement _hungerFill;
        private bool _hungerSubscribed;  // [O10] 허기 정적 이벤트 구독 플래그

        // ===== [P3 Figma] character-status-panel 구조 레퍼런스 (표시 전용 — 데이터 소스는 기존과 동일) =====
        private Label _identityTitleLabel;     // IdentityRow 등급 배지 (TitleManager.GetTitleText)
        private Label _identityLevelLabel;     // IdentityRow 레벨 배지 (PlayerStats.Level)
        private Label _levelBlockValueLabel;   // LevelBlock 큰 레벨 숫자
        private Label _expPercentLabel;        // LevelBlock "EXP <비율%>"
        private readonly Label[] _coreValueLabels = new Label[4];   // CoreStatsGrid 공격/방어/최대체력/민첩
        private Label _figHungerValueLabel;    // 특수 상태 게이지1 허기 — 우측 허기 게이지와 동일 소스
        private VisualElement _figHungerFill;
        private Label _figAddictionLabel;      // 특수 상태 게이지2 중독도 — 우측 중독 라벨과 동일 소스
        private VisualElement _figAddictionFill;
        private Label _portraitHpLabel;        // Portrait HP 오버레이 텍스트
        private VisualElement _portraitHpFill;
        private VisualElement _equipmentContent;
        private VisualElement _statusContent;
        private VisualElement _figSpecialSection;
        private VisualElement _statusPanel;
        private VisualElement _equipmentPanel;
        private Button _statusCloseButton;
        private VisualElement _scaleRoot;
        private IVisualElementScheduledItem _scaleRefresh;

        private PlayerStats _subscribedStats;
        private EquipmentManager _subscribedEquip;
        private float _refreshTimer;

        private StatusWindowUTK() : this(false)
        {
        }

        private StatusWindowUTK(bool statusOnly)
            : base("상태", statusOnly ? new Vector2(StatusOnlyFrameWidth, StatusOnlyFrameHeight) : new Vector2(WinW, WinH), UTKWindowChrome.Frameless)
        {
            _statusOnly = statusOnly;
            BuildContent();
            ApplyGitHubDarkStyle();
            ApplyUIToolkitFont(this);
            style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
            style.backgroundImage = new StyleBackground(StyleKeyword.None);
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 0f;
            style.left = statusOnly ? 0f : CompositionCanvasBounds.x;
            style.top = statusOnly ? 0f : CompositionCanvasBounds.y;
            _content.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
            _content.style.flexGrow = 0f;
            _content.style.width = statusOnly ? StatusOnlyFrameWidth : WinW;
            _content.style.height = statusOnly ? StatusOnlyFrameHeight : WinH;
            style.display = DisplayStyle.None;
        }

        // =====================================================================
        //  콘텐츠 빌드
        // =====================================================================

        private void BuildContent()
        {
            _content.style.position = Position.Relative;
            _content.style.flexDirection = FlexDirection.Row;
            _content.style.flexGrow = 0f;
            _content.style.flexShrink = 0f;
            _content.style.width = _statusOnly ? StatusOnlyFrameWidth : WinW;
            _content.style.height = _statusOnly ? StatusOnlyFrameHeight : WinH;

            _statusPanel = BuildPanel("CharacterStatusPanel", _statusOnly ? StatusOnlyCanvasBounds : StatusPanelLocalBounds);
            _content.Add(_statusPanel);
            var statusScroll = new ScrollView(ScrollViewMode.Vertical);
            statusScroll.name = "StatusScrollView";
            statusScroll.style.width = new Length(100f, LengthUnit.Percent);
            statusScroll.style.height = new Length(100f, LengthUnit.Percent);
            statusScroll.style.flexGrow = 1f;
            statusScroll.style.minHeight = 0f;
            _statusPanel.Add(statusScroll);
            _statusContent = statusScroll.contentContainer;
            _statusContent.style.flexDirection = FlexDirection.Column;
            _statusContent.style.width = new Length(100f, LengthUnit.Percent);
            _statusContent.style.flexGrow = 1f;
            BuildLeftZone();
            BuildRightZone();

            if (_statusOnly)
            {
                ApplyStatusOnlyComposition();
                return;
            }
            else
            {
                // [Figma 84:8] CloseBtn은 컴포지션에서도 PanelHeader 우측에 실존 — 트리 제거 금지(디자인/테스트 계약)
                _statusContent.style.paddingTop = 0f;
                _statusContent.style.paddingBottom = 0f;
                _statusContent.style.paddingLeft = 0f;
                _statusContent.style.paddingRight = 0f;
                _figSpecialSection.style.position = Position.Absolute;
                _figSpecialSection.style.left = 40.8f;
                _figSpecialSection.style.top = 708.4f;
                _figSpecialSection.style.width = 494.4f;
                _figSpecialSection.style.height = 157f;
                var oldHeading = _figSpecialSection.ElementAt(0);
                oldHeading.style.display = DisplayStyle.None;
                var figHeader = new VisualElement { name = "StatusSpecialVitalsFigmaHeader" };
                figHeader.style.flexDirection = FlexDirection.Row;
                figHeader.style.alignItems = Align.Center;
                figHeader.style.height = 17f;
                figHeader.Add(MkLabel("특수 상태 게이지", 14.4f, GitHubDark.TextMain, TextAnchor.MiddleLeft));
                var sv = MkLabel("SPECIAL VITALS", 12, GitHubDark.TextSub, TextAnchor.MiddleRight);
                sv.style.flexGrow = 1f;
                figHeader.Add(sv);
                _figSpecialSection.Insert(0, figHeader);
            }

            _equipmentPanel = BuildPanel("EquipmentPanel", EquipmentPanelLocalBounds);
            _content.Add(_equipmentPanel);
            // [144차 P0-① B7] EP 코너 데칼 4개(84:56~67) — 14.4×14.4 L자 #58A6FF 1.8, [144차 P0 V1] 공용 어휘.
            //   인벤 창(144차 P1-INV)·아이템 설명창 선례와 동일 outsideCorner:false(코너 안쪽 걸침) — EP 임베드 패널 전용, CSP 본체 무관.
            UTKTheme.AddCornerDecals(_equipmentPanel, 14.4f, 1.8f, "#58A6FF", outsideCorner: false);
            var equipmentScroll = new ScrollView(ScrollViewMode.Vertical);
            equipmentScroll.name = "EquipmentScrollView";
            equipmentScroll.style.width = new Length(100f, LengthUnit.Percent);
            equipmentScroll.style.height = new Length(100f, LengthUnit.Percent);
            equipmentScroll.style.flexGrow = 1f;
            equipmentScroll.style.minHeight = 0f;
            _equipmentPanel.Add(equipmentScroll);
            _equipmentContent = equipmentScroll.contentContainer;
            _equipmentContent.style.flexDirection = FlexDirection.Column;
            _equipmentContent.style.flexGrow = 1f;
            _equipmentContent.style.width = new Length(100f, LengthUnit.Percent);
            BuildMiddleZone();
            BuildInventoryPack();
        }

        private static VisualElement BuildPanel(string panelName, Rect bounds)
        {
            var panel = new VisualElement { name = panelName };
            panel.style.position = Position.Absolute;
            panel.style.left = bounds.x;
            panel.style.top = bounds.y;
            panel.style.width = bounds.width;
            panel.style.height = bounds.height;
            panel.style.flexDirection = FlexDirection.Column;
            panel.style.overflow = Overflow.Hidden;
            UTKTheme.ApplyFigmaGlass(panel);   // [Figma 글래스] #161B22@0.702([144차 P0 V8] D1 전 창 통일) + #30363D@0.5 1.2px + r14.4 — [144차 P1-EQ B4] EP 글래스 실효앰파 0.702 정합(주석 갱신)
            panel.style.paddingLeft = panel.style.paddingRight = 24f;
            panel.style.paddingTop = panel.style.paddingBottom = 24f;   // [Figma] 패널 pad 24
            return panel;
        }

        // ---------------------------------------------------------------------
        //  [P3 Figma] 좌측 열 = character-status-panel 상단 구조:
        //  IdentityRow → Portrait(3D placeholder + HP 오버레이) → StatsSection(LevelBlock + CoreStatsGrid)
        //  → 특수 상태(허기/중독도). 데이터 소스는 기존과 동일 — 표시 레이아웃만 재배열.
        // ---------------------------------------------------------------------
        private void BuildLeftZone()
        {
            // [Figma 84:8 PanelHeader 528×52.8] '상태 정보'(20) + 'CHARACTER STATUS'(11) + CloseBtn 31.2×31.2 — 컴포지션에서도 트리 유지
            var header = new VisualElement { name = "StatusPanelHeader" };
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.height = 52.8f;
            header.style.minHeight = 52.8f;
            var statusTitle = MkLabel("상태 정보", UTKTheme.FontTitleLarge, GitHubDark.TextMain, TextAnchor.MiddleLeft);   // [Figma] 24/700
            statusTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(statusTitle);
            var headerSub = MkLabel("CHARACTER STATUS", UTKTheme.FontSubtitle, GitHubDark.TextSub, TextAnchor.MiddleLeft);   // [Figma] 13.2/500
            headerSub.style.marginLeft = 12f;
            headerSub.style.marginTop = 5f;
            headerSub.style.flexGrow = 1f;
            header.Add(headerSub);
            _statusCloseButton = new Button(() => Close()) { text = "×" };
            _statusCloseButton.name = "StatusCloseButton";
            _statusCloseButton.style.width = 31.2f;
            _statusCloseButton.style.height = 31.2f;
            StyleButton(_statusCloseButton, UTKButton.Variant.Secondary);
            header.Add(_statusCloseButton);
            _statusContent.Add(header);

            var left = new VisualElement();
            left.name = "LeftZone";
            left.style.width = new Length(100f, LengthUnit.Percent);
            left.style.flexShrink = 0;
            left.style.flexDirection = FlexDirection.Column;
            _statusContent.Add(left);

            // ── [144차 P0-CSP #14] CharacterIdentitySection 카드 83:28 — 528×84, #FFFFFF@0.02, r9.6, pad 14.4, gap 7.2 ──
            //   RarityText 83:30(14.4/700 #BC8CFF) + ClassType 83:31(14.4/400 #8B949E 우측). CharacterName 83:32은
            //   데이터 위조 금지 계약으로 의도 생략 유지(주석 L387). Lv.- 배지는 게임 전용 요소(보존, 기록).
            var identity = new VisualElement();
            identity.name = "CharacterIdentitySection";
            identity.style.height = 84f;
            identity.style.flexShrink = 0;
            identity.style.flexDirection = FlexDirection.Column;
            identity.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.02f));
            identity.style.borderTopLeftRadius = identity.style.borderTopRightRadius = 9.6f;
            identity.style.borderBottomLeftRadius = identity.style.borderBottomRightRadius = 9.6f;
            identity.style.paddingLeft = identity.style.paddingRight = 14.4f;
            identity.style.paddingTop = identity.style.paddingBottom = 14.4f;
            left.Add(identity);

            var identityRow1 = new VisualElement { name = "IdentityRow" };
            identityRow1.style.flexDirection = FlexDirection.Row;
            identityRow1.style.alignItems = Align.Center;
            identity.Add(identityRow1);

            _identityTitleLabel = MkLabel(GetTitleDisplay(), 14.4f, new Color32(0xBC, 0x8C, 0xFF, 0xFF), TextAnchor.MiddleLeft);   // [144차 P1-CSP #15] 14.4/700 #BC8CFF(등급 퍼플)
            _identityTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            identityRow1.Add(_identityTitleLabel);

            _identityLevelLabel = MkLabel("Lv.-", UTKTheme.FontRowLabel, GitHubDark.Accent, TextAnchor.MiddleLeft);   // 게임 전용 배지(Figma 부재 — 보존)
            _identityLevelLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _identityLevelLabel.style.marginLeft = 6f;
            StyleBadge(_identityLevelLabel);
            identityRow1.Add(_identityLevelLabel);

            var identitySpacer = new VisualElement();
            identitySpacer.style.flexGrow = 1f;
            identityRow1.Add(identitySpacer);

            // [144차 P0-CSP #16] ClassType 83:31 — 14.4/400 #8B949E 우측 배치. 플레이어 클래스 데이터 소스 부재 →
            //   실데이터 바인딩 불가(데이터 위조 금지) — 플레이스홀더 "클래스: —" 표기(기록).
            var classType = MkLabel("클래스: —", 14.4f, GitHubDark.TextSub, TextAnchor.MiddleRight);
            classType.name = "ClassType";
            classType.style.marginTop = 7.2f;
            identity.Add(classType);

            // No character-name source exists in this owner; do not invent an identity value.

            // ── [Figma] Portrait: 3D 뷰포트 placeholder (원본 3D 프리뷰 자리 — 시각 요소이므로 자리만 유지) ──
            var viewport = new VisualElement();
            viewport.name = "Viewport";
            viewport.AddToClassList("utk-slot");
            ApplyDarkSlotStyle(viewport);   // [GitHub-dark] 우드 배경 제거 → 다크 인셋 패널
            viewport.style.height = 312f;   // [Figma Portrait 528×312]
            viewport.style.borderTopLeftRadius = viewport.style.borderTopRightRadius = 9.6f;   // [144차 P1-CSP #19] r6→9.6 (공용 ApplyDarkSlotStyle 무수정 — 요소 단위 override)
            viewport.style.borderBottomLeftRadius = viewport.style.borderBottomRightRadius = 9.6f;
            var vpLabel = MkLabel("3D 미리보기", UTKTheme.FontBody, GitHubDark.TextSub, TextAnchor.MiddleCenter);   // [Figma] 14.4/400
            viewport.Add(vpLabel);
            left.Add(viewport);

            // [Figma HudOverlay 528×44.8 @ Portrait 상단] 좌 자리표시 + 우 HP(실데이터) — 트랙은 오버레이 하단 절대 배치
            var hpOverlay = new VisualElement();
            hpOverlay.name = "HpOverlay";
            hpOverlay.style.position = Position.Absolute;
            hpOverlay.style.left = 1f;
            hpOverlay.style.right = 1f;
            hpOverlay.style.top = 1f;
            hpOverlay.style.height = 44.8f;
            // [144차 P1-CSP #21] HudOvelay 83:35 — fills 없음(투명). 추가 배경판 제거.
            hpOverlay.style.flexDirection = FlexDirection.Row;
            hpOverlay.style.alignItems = Align.Center;
            hpOverlay.style.paddingLeft = 14.4f;
            hpOverlay.style.paddingRight = 14.4f;
            viewport.Add(hpOverlay);

            var portraitHint = MkLabel("3D 미리보기", 12, GitHubDark.Accent, TextAnchor.MiddleLeft);   // [144차 P1-CSP #22] 색 #58A6FF (텍스트는 실데이터 힌트 유지)
            portraitHint.style.flexGrow = 1f;
            hpOverlay.Add(portraitHint);

            _portraitHpLabel = MkLabel("-", 12, GitHubDark.Accent, TextAnchor.MiddleRight);   // [144차 P1-CSP #23] 색 #58A6FF (텍스트는 실데이터 — 위조 금지)
            hpOverlay.Add(_portraitHpLabel);

            var portraitHpTrack = new VisualElement();
            portraitHpTrack.style.position = Position.Absolute;
            portraitHpTrack.style.left = 14.4f;
            portraitHpTrack.style.right = 14.4f;
            portraitHpTrack.style.bottom = 6f;
            portraitHpTrack.style.height = 6f;
            portraitHpTrack.style.backgroundColor = new StyleColor(GitHubDark.BgBase);
            portraitHpTrack.style.borderTopLeftRadius = 3f;
            portraitHpTrack.style.borderTopRightRadius = 3f;
            portraitHpTrack.style.borderBottomLeftRadius = 3f;
            portraitHpTrack.style.borderBottomRightRadius = 3f;
            hpOverlay.Add(portraitHpTrack);

            _portraitHpFill = new VisualElement();
            _portraitHpFill.style.height = new Length(100f, LengthUnit.Percent);
            _portraitHpFill.style.width = new Length(0f, LengthUnit.Percent);
            _portraitHpFill.style.backgroundColor = new StyleColor(GitHubDark.Health);
            _portraitHpFill.style.borderTopLeftRadius = 3f;
            _portraitHpFill.style.borderTopRightRadius = 3f;
            _portraitHpFill.style.borderBottomLeftRadius = 3f;
            _portraitHpFill.style.borderBottomRightRadius = 3f;
            portraitHpTrack.Add(_portraitHpFill);

            // ── [Figma Stats 528×157.8] 좌 LevelBlock 120(세로: LEVEL/레벨/EXP) + 우 CoreStatsGrid ──
            // ── [144차 P0-CSP #25] StatsSection 카드 83:38 — 528×174.4, #FFFFFF@0.012, r9.6, pad 14.4, gap 19.2 ──
            var statsRow = new VisualElement { name = "StatsSection" };
            statsRow.style.flexDirection = FlexDirection.Row;
            statsRow.style.marginTop = 19.2f;
            statsRow.style.flexShrink = 0f;
            statsRow.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.012f));
            statsRow.style.borderTopLeftRadius = statsRow.style.borderTopRightRadius = 9.6f;
            statsRow.style.borderBottomLeftRadius = statsRow.style.borderBottomRightRadius = 9.6f;
            statsRow.style.paddingLeft = statsRow.style.paddingRight = 14.4f;
            statsRow.style.paddingTop = statsRow.style.paddingBottom = 14.4f;
            left.Add(statsRow);

            var levelBlock = new VisualElement { name = "LevelBlock" };
            levelBlock.style.width = 120f;
            levelBlock.style.flexShrink = 0f;
            levelBlock.style.flexDirection = FlexDirection.Column;
            levelBlock.style.alignItems = Align.Center;
            levelBlock.style.justifyContent = Justify.Center;
            levelBlock.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.02f));   // [144차 P1-CSP #27] #21262D→#FFFFFF@0.02
            levelBlock.style.borderTopLeftRadius = levelBlock.style.borderTopRightRadius = 7.2f;   // [144차 P1-CSP #27] r8→7.2
            levelBlock.style.borderBottomLeftRadius = levelBlock.style.borderBottomRightRadius = 7.2f;
            levelBlock.style.marginRight = 19.2f;   // [144차 P1-CSP #25] 카드 내부 gap 19.2
            statsRow.Add(levelBlock);

            var lvCaption = MkLabel("LEVEL", 13.2f, GitHubDark.TextSub, TextAnchor.MiddleCenter);   // [144차 P1-CSP #28] 12→13.2/500근사
            lvCaption.style.unityFontStyleAndWeight = FontStyle.Normal;
            levelBlock.Add(lvCaption);
            _levelBlockValueLabel = MkLabel("1", UTKTheme.FontBigNumber, GitHubDark.Accent, TextAnchor.MiddleCenter);   // [Figma] 38.4/800 #58A6FF
            _levelBlockValueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            levelBlock.Add(_levelBlockValueLabel);
            _expPercentLabel = MkLabel("EXP 0%", 12f, GitHubDark.SysStable, TextAnchor.MiddleCenter);   // [144차 P1-CSP #30] 13.2/500 Gold→12/400 #56E39F
            _expPercentLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
            _expPercentLabel.style.marginTop = 6f;
            levelBlock.Add(_expPercentLabel);

            var coreGrid = new VisualElement { name = "CoreStatsGrid" };
            coreGrid.style.flexGrow = 1f;
            // [144차 P1-CSP #31] CoreStatsGrid 83:43 — pad 없음·배경 없음 (추가 배경판/패딩 제거, 카드는 StatsSection이 담당)
            statsRow.Add(coreGrid);

            // [144차 P1-CSP #36] 값 색 다색 — Figma 83:46/50/54/58: ATK #58A6FF / DEF #F0F6FC / HP #FF7B72 / AGI #F2C94C
            var coreValueColors = new Color32[]
            {
                GitHubDark.Accent,                                   // 공격력
                GitHubDark.TextMain,                                 // 방어력
                new Color32(0xFF, 0x7B, 0x72, 0xFF),                 // 최대 체력
                new Color32(0xF2, 0xC9, 0x4C, 0xFF),                 // 민첩성
            };
            for (int i = 0; i < _coreStatNames.Length; i++)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.height = 22f;   // [Figma StatsGrid 행 22]

                var name = MkLabel(_coreStatNames[i], UTKTheme.FontRowLabel, GitHubDark.TextSub, TextAnchor.MiddleLeft);   // [Figma] 15.6/500
                name.name = "CoreStat_" + new[] { "Attack", "Defense", "MaxHealth", "Agility" }[i];
                // [144차 P1-CSP #37] 라벨 폭 자동(고정 72 제거) + 세로 구분선 제거(Figma에 없음)
                name.style.flexGrow = 1f;
                row.Add(name);

                _coreValueLabels[i] = MkLabel("-", UTKTheme.FontRowValue, coreValueColors[i], TextAnchor.MiddleRight);   // [Figma] 16.8/700 다색
                _coreValueLabels[i].style.unityFontStyleAndWeight = FontStyle.Bold;
                _coreValueLabels[i].style.flexGrow = 1.2f;
                row.Add(_coreValueLabels[i]);

                coreGrid.Add(row);
                if (i < _coreStatNames.Length - 1)
                {
                    var hsep = new VisualElement();   // [Figma] 행 사이 라인(전후 gap 9.6)
                    hsep.style.height = 1f;
                    hsep.style.backgroundColor = new StyleColor(UTKTheme.GlassPanelStroke);   // [144차 P1-CSP #38] #2E343D→#30363D@0.502
                    hsep.style.marginTop = 9.6f;
                    hsep.style.marginBottom = 9.6f;
                    coreGrid.Add(hsep);
                }
            }

            // ── [Figma] SpecialStatsSection "특수 상태" 게이지 2개 ──
            AddSep(left, 12f, 10f);
            _figSpecialSection = new VisualElement { name = "SpecialStatsSection" };
            _figSpecialSection.AddToClassList("special-gauge-root");   // [144차] Q 조회 경로 확보 — 클래스 미부여 시 폴백 워크가 _statusContent를 반환하는 잠재버그 수리
            _figSpecialSection.style.flexDirection = FlexDirection.Column;
            var specialTitle = MkLabel("특수 상태", UTKTheme.FontTab, GitHubDark.TextSub, TextAnchor.MiddleLeft);   // [Figma] 14.4/700 #8B949E
            specialTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _figSpecialSection.Add(specialTitle);

            // 게이지1: 허기 — Figma '호감도' 자리는 플레이어 실존 '허기'(HungerSystem)로 대체 [O10 동일 소스]
            var figHunger = BuildCompactGauge("허기", GitHubDark.Accent);
            _figHungerValueLabel = figHunger.value;
            _figHungerFill = figHunger.fill;
            _figSpecialSection.Add(figHunger.root);

            // 게이지2: 중독도 — DrugEffectSystem (우측 중독 라벨과 동일 소스)
            var figAddiction = BuildCompactGauge("중독도", GitHubDark.RankEpic);
            _figAddictionLabel = figAddiction.value;
            _figAddictionFill = figAddiction.fill;
            _figSpecialSection.Add(figAddiction.root);
            left.Add(_figSpecialSection);
        }

        /// <summary>[P3 Figma] 중앙 열 — 기존 좌측 상세(장비슬롯 6개 + 장비 보너스)를 이동 배치. 로직/구성 무수정.</summary>
        private void ApplyStatusOnlyComposition()
        {
            _statusPanel.style.left = 672f;
            _statusPanel.style.top = 72f;
            _statusPanel.style.width = 576f;
            _statusPanel.style.height = 936f;
            // [144차 P1-CSP #2/#3/#5] 패널 크롬은 BuildPanel의 Figma 글래스(#161B22@0.702 + #30363D@0.502 1.2 4면 균일 + r14.4)와
            //   pad 24 전면을 그대로 상속 — 구체 override(0.85·좌우 1.0·pt/pb 18) 제거.
            _statusPanel.style.paddingLeft = _statusPanel.style.paddingRight = 24f;
            _statusPanel.style.paddingTop = _statusPanel.style.paddingBottom = 24f;

            if (_statusCloseButton != null) _statusCloseButton.RemoveFromHierarchy();
            var title = new VisualElement { name = "CharacterStatusTitleGroup" };
            title.style.position = Position.Absolute;
            title.style.left = 24f;
            title.style.top = 24f;
            title.style.width = 528f;
            title.style.height = 52.8f;
            title.style.flexDirection = FlexDirection.Row;
            title.style.alignItems = Align.Center;
            var titleText = MkLabel("상태 정보", 24f, GitHubDark.TextMain, TextAnchor.MiddleLeft);   // [144차 P1-CSP #9] 21.6→24
            title.Add(titleText);
            var subtitle = MkLabel("CHARACTER STATUS", 13.2f, GitHubDark.TextSub, TextAnchor.MiddleLeft);   // [144차 P1-CSP #10] 12→13.2
            subtitle.style.marginLeft = 12f;
            title.Add(subtitle);
            var close = new Button(Close) { name = "StatusCloseButton", text = "×" };
            close.style.position = Position.Absolute;
            close.style.right = 0f;
            close.style.top = 10f;
            close.style.width = 31.2f;
            close.style.height = 31.2f;
            // [144차 P1-CSP #12] CloseBtn 83:25 — fill #FFFFFF@0.059, r7.2, stroke #30363D@0.502, pad 7.2 (StyleButton Secondary 대체)
            close.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.059f));
            close.style.borderTopLeftRadius = close.style.borderTopRightRadius = 7.2f;
            close.style.borderBottomLeftRadius = close.style.borderBottomRightRadius = 7.2f;
            close.style.borderTopWidth = close.style.borderBottomWidth = 1.2f;
            close.style.borderLeftWidth = close.style.borderRightWidth = 1.2f;
            close.style.borderTopColor = close.style.borderBottomColor = new StyleColor(UTKTheme.GlassPanelStroke);
            close.style.borderLeftColor = close.style.borderRightColor = new StyleColor(UTKTheme.GlassPanelStroke);
            close.style.paddingLeft = close.style.paddingRight = 7.2f;
            close.style.paddingTop = close.style.paddingBottom = 7.2f;
            _statusCloseButton = close;   // 필드 갱신 — 리프레시 경로가 살아있는 요소를 참조하도록
            title.Add(close);
            _statusPanel.Add(title);

            // [144차 P0-CSP #7] CSP 코너 데칼 4개 83:9~20 — 14.4×14.4, Line #58A6FF w1.8, 코너 밖(공용 어휘 기본 모드).
            UTKTheme.AddCornerDecals(_statusPanel, 14.4f, 1.8f);

            // [144차 P1-CSP #41] 특수헤더 2분할 83:61/62 — 좌 14.4/700 #8B949E + 우 12/400 #484F58 우측정렬.
            var vitalsHeaderLeft = MkLabel("특수 상태 게이지", 14.4f, GitHubDark.TextSub, TextAnchor.MiddleLeft);
            vitalsHeaderLeft.style.unityFontStyleAndWeight = FontStyle.Bold;
            var vitalsHeaderRight = MkLabel("SPECIAL VITALS", 12f, new Color32(0x48, 0x4F, 0x58, 0xFF), TextAnchor.MiddleRight);
            var special = _statusContent.Q<VisualElement>(className: "special-gauge-root");
            if (_figHungerFill != null)
            {
                var parent = _figHungerFill.parent;
                while (parent != null && parent.parent != _statusContent) parent = parent.parent;
                if (parent != null) { special = parent.parent; }
            }
            if (special != null)
            {
                // [144차 P1-CSP #39+#40] SpecialStatsSection 83:59 — 패널 내부 (24,724) 528×157, 카드 #FFFFFF@0.012 r9.6 pad 16.8.
                special.style.marginTop = 0f;
                special.style.position = Position.Absolute;
                special.style.left = 24f;
                special.style.top = 724f;
                special.style.width = 528f;
                special.style.height = 157f;
                special.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.012f));
                special.style.borderTopLeftRadius = special.style.borderTopRightRadius = 9.6f;
                special.style.borderBottomLeftRadius = special.style.borderBottomRightRadius = 9.6f;
                special.style.paddingLeft = special.style.paddingRight = 16.8f;
                special.style.paddingTop = special.style.paddingBottom = 16.8f;
                if (special.childCount > 0) special[0].style.display = DisplayStyle.None;   // 구 타이틀 숨김 유지
                if (special.childCount > 1) special[1].style.marginTop = 10.2f;   // 헤더→게이지1 간격 (pad16.8+헤더17+10.2=44)
                if (special.childCount > 2) special[2].style.marginTop = 7.2f;    // 게이지 간 gap 7.2
                var specialHeading = new VisualElement { name = "SpecialVitalsHeader" };
                specialHeading.style.flexDirection = FlexDirection.Row;
                specialHeading.style.alignItems = Align.Center;
                specialHeading.style.height = 17f;
                specialHeading.Add(vitalsHeaderLeft);
                vitalsHeaderRight.style.flexGrow = 1f;
                specialHeading.Add(vitalsHeaderRight);
                special.Insert(1, specialHeading);   // 숨긴 타이틀 다음, 게이지1 위
            }

            _statusContent.style.paddingTop = 74f;   // [144차 P1-CSP #5] pt 18→24 보상 재계산(80−6) — 첫 컨텐츠 절대 y 불변
            _statusContent.style.paddingBottom = 0f;
            _statusContent.style.paddingLeft = 0f;
            _statusContent.style.paddingRight = 0f;
        }


        private void BuildMiddleZone()
        {
            var content = _equipmentContent;
            content.style.paddingLeft = 24f;
            content.style.paddingRight = 24f;
            content.style.paddingTop = 24f;
            content.style.paddingBottom = 24f;

            var header = new VisualElement { name = "EquipmentSectionHeader" };
            header.style.height = 52.8f;
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            var title = MkLabel("장착 장비", UTKTheme.FontTitleLarge, GitHubDark.TextMain, TextAnchor.MiddleLeft);   // [Figma] 24/700
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(title);
            var subtitle = MkLabel("LOADOUT MATRIX", UTKTheme.FontSubtitle, GitHubDark.TextSub, TextAnchor.MiddleLeft);   // [Figma] 13.2/500
            subtitle.style.unityFontStyleAndWeight = FontStyle.Normal;
            subtitle.style.marginLeft = 10f;
            header.Add(subtitle);
            var stable = MkLabel("SYS_EQ_STABLE", UTKTheme.FontBadge, GitHubDark.SysStable, TextAnchor.MiddleRight);   // [Figma 84:73] 12/400 — [144차 P1-EQ B11] 색 #56E39F 정합
            stable.style.unityFontStyleAndWeight = FontStyle.Normal;
            stable.style.flexGrow = 1f;
            header.Add(stable);
            content.Add(header);

            var equipped = new VisualElement { name = "EquippedSection" };
            equipped.style.marginTop = 19.2f;   // [144차 P1-EQ B12] 24→19.2 — 84:68 gap 진실치(섹션 y=24+52.8+19.2=96)
            equipped.style.height = 230.4f;     // [144차 P1-EQ B29] 진실치 고정(93:4 483.6×230.4) — 전술 배낭 y 345.6 정합
            equipped.style.paddingLeft = 14.4f;
            equipped.style.paddingRight = 14.4f;
            equipped.style.paddingTop = 14.4f;
            equipped.style.paddingBottom = 14.4f;
            // [144차 P1-EQ B13] 섹션 카드(93:4) — fill #FFFFFF@0.02 저알파 화이트 글래스 + 크롬 테두리(#30363D@0.502 1.2) + r9.6
            equipped.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.02f));
            UTKTheme.ApplyElementBorder(equipped);   // [144차 P0 V2] 공용 어휘
            UTKTheme.ApplyRadius(equipped, 9.6f);
            var equippedTitle = MkLabel("장착 중인 장비", 19.2f, GitHubDark.Accent, TextAnchor.MiddleLeft);   // [144차 P1-EQ B15] 24→19.2/700 #58A6FF(93:5)
            equippedTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            equipped.Add(equippedTitle);
            content.Add(equipped);

            // [Figma 84:4] EquipGrid 427.2×172.8 — ItemSlot 81.6×81.6 ×10(5열×2행), gap 4.8(열)·9.6(행).
            var grid = new VisualElement { name = "EquipGrid" };
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.justifyContent = Justify.FlexStart;
            grid.style.width = 427.2f;
            grid.style.alignSelf = Align.Center;   // [144차 P1-EQ B16] 섹션 내 중앙정렬 — Figma x=28(내여백 (454.8−427.2)/2=13.8)
            grid.style.marginTop = 12f;
            equipped.Add(grid);
            for (int i = 0; i < _equipOrder.Length && i < _equipNames.Length; i++)
                grid.Add(BuildFigmaEquipSlotBox(_equipOrder[i], _equipNames[i], i));

            var pack = new VisualElement { name = "TacticalPackSection" };
            pack.style.marginTop = 19.2f;
            pack.style.paddingLeft = 19.2f;
            pack.style.paddingRight = 19.2f;
            pack.style.paddingTop = 14.4f;
            pack.style.paddingBottom = 14.4f;
            // [144차 P1-EQ B36 동일계열] 섹션 카드(93:88) — fill #FFFFFF@0.012 저알파 화이트 글래스 + 크롬 테두리 + r9.6
            //   (93:4와 동일 계열 괴리 — 감사 보고서 '참고 기록' 행, EquippedSection B13 수리와 쌍 수리)
            pack.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.012f));
            UTKTheme.ApplyElementBorder(pack);
            UTKTheme.ApplyRadius(pack, 9.6f);
            var packHeader = new VisualElement { name = "TacticalPackHeader" };
            packHeader.style.height = 58.2f;
            packHeader.style.flexDirection = FlexDirection.Column;
            var packTitle = MkLabel("전술 배낭", 19.2f, GitHubDark.TextMain, TextAnchor.MiddleLeft);   // [144차 P1-EQ B33] 24→19.2/700 #F0F6FC(93:90)
            packTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            packHeader.Add(packTitle);
            var packSub = MkLabel("TACTICAL PACK", 12f, GitHubDark.TextSub, TextAnchor.MiddleLeft);   // [144차 P1-EQ B34] 13.2→12/400 #8B949E(93:91)
            packHeader.Add(packSub);
            pack.Add(packHeader);
            var packScroll = new ScrollView(ScrollViewMode.Vertical) { name = "InventoryGridScroll" };
            packScroll.style.height = 465.6f;
            packScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            var packGrid = new VisualElement { name = "InventoryPackGrid" };
            packGrid.style.flexDirection = FlexDirection.Row;
            packGrid.style.flexWrap = Wrap.Wrap;
            packGrid.style.justifyContent = Justify.SpaceBetween;
            packGrid.style.width = 445.2f;
            packScroll.contentContainer.Add(packGrid);
            pack.Add(packScroll);
            content.Add(pack);
            for (int i = 0; i < 25; i++) packGrid.Add(BuildInventoryCell(i));
            RefreshInventoryVisuals();
        }

        /// <summary>
        /// [Figma 84:4 ItemSlot 정합] 81.6×81.6 슬롯 — EQ 배지 박스(4.8,4.8 · 반지·목걸이 제외 8슬롯) +
        /// TierStrip(81.6×3.6, 장착 시 희귀색 — 빈 슬롯 숨김: 데이터 위조 금지) + 아이콘 38.4 중앙(툴팁=아이템명) +
        /// 하단 슬롯명 라벨. 간격 4.8(열)·9.6(행), 5열×2행.
        /// [144차 P1-EQ] B17 행갭 · B20 셀 스타일 · B22 배지 박스 · B23 배지 부착집합 · B27 아이콘 수리.
        /// </summary>
        private VisualElement BuildFigmaEquipSlotBox(EquipmentManager.EquipmentSlot slot, string label, int orderIndex)
        {
            var cell = new VisualElement { name = "EquipSlot_" + slot };
            cell.AddToClassList("utk-slot");
            ApplyFigmaSlotStyle(cell);   // [144차 P1-EQ B20] bg #21262D@0.6 · 크롬 테두리(#30363D@0.502 1.2) · r9.6
            cell.style.width = 81.6f;
            cell.style.height = 81.6f;
            cell.style.flexShrink = 0f;
            int column = orderIndex % 5;
            int row = orderIndex / 5;
            cell.style.marginRight = column == 4 ? 0f : 4.8f;
            cell.style.marginBottom = row == 1 ? 0f : 9.6f;   // [144차 P1-EQ B17] 행갭 4.8→9.6(행 피치 91.2)

            // [144차 P1-EQ B22·B23] EQ 배지 박스 22.6×15.8 r3.6 fill #58A6FF + "EQ" 9.6/700 #0B0E14 @(4.8,4.8) — 반지·목걸이 제외 8슬롯(93:8)
            if (slot != EquipmentManager.EquipmentSlot.Ring && slot != EquipmentManager.EquipmentSlot.Necklace)
            {
                var badge = new VisualElement { name = "EquipBadge_" + slot };
                badge.style.position = Position.Absolute;
                badge.style.left = 4.8f;
                badge.style.top = 4.8f;
                badge.style.width = 22.6f;
                badge.style.height = 15.8f;
                badge.style.backgroundColor = new StyleColor(GitHubDark.Accent);
                UTKTheme.ApplyRadius(badge, 3.6f);
                badge.pickingMode = PickingMode.Ignore;
                var equip = MkLabel("EQ", 9.6f, GitHubDark.BgBase, TextAnchor.MiddleCenter);   // [Figma 93:9] #0B0E14
                equip.style.unityFontStyleAndWeight = FontStyle.Bold;
                badge.Add(equip);
                cell.Add(badge);
            }

            var strip = new VisualElement { name = "EquipTierStrip_" + slot };
            strip.style.position = Position.Absolute;
            strip.style.left = 0f;
            strip.style.top = 0f;
            strip.style.width = 81.6f;
            strip.style.height = 3.6f;
            strip.style.display = DisplayStyle.None;
            _equipTierStrips[(int)slot] = strip;
            cell.Add(strip);

            // [144차 P1-EQ B27] 아이콘 38.4×38.4 중앙(93:11) — 실아이콘(ItemIconDatabase)+툴팁(아이템명).
            //   기존 중앙 아이템명 라벨(텍스트 대체)은 인벤 장비 임베드 PASS 선례([P20-5])에 따라 폐지 — 표현 수단 정합.
            var icon = new VisualElement { name = "EquippedIcon_" + slot };
            icon.style.position = Position.Absolute;
            icon.style.left = 21.6f;
            icon.style.top = 21.6f;
            icon.style.width = 38.4f;
            icon.style.height = 38.4f;
            icon.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            icon.pickingMode = PickingMode.Ignore;
            _equippedIconHosts[(int)slot] = icon;
            cell.Add(icon);

            // 하단 슬롯명 — Figma '투구' 9.6/500 @(하단 중앙) — [B28 PASS 유지]
            var slotName = MkLabel(label, 9.6f, GitHubDark.TextSub, TextAnchor.MiddleCenter);
            slotName.style.unityFontStyleAndWeight = FontStyle.Normal;
            slotName.name = "EquipSlotName_" + slot;
            slotName.style.position = Position.Absolute;
            slotName.style.left = 0f;
            slotName.style.bottom = 4.6f;
            slotName.style.width = 81.6f;
            cell.Add(slotName);
            return cell;
        }

        private void BuildInventoryPack()
        {
            // Replaced by BuildMiddleZone's Figma Tactical Pack composition.
        }

        /// <summary>
        /// [144차 P0-② B37] 전술 배낭 셀 — UTKSlot 전환(25셀 전부): 상단 TierStrip h3.6(93:92 — [144차 P0 V3]
        /// SetTierStrip 어휘), 아이콘 38.4 중앙(85:104 — 실아이콘·툴팁), Count 우하단 13.2/500 #8B949E
        /// Geist Mono(93:93 — 우 7.6/하 4.6). 셀 스타일 bg #21262D@0.6 · 크롬 1.2 · r9.6(B36).
        /// 표시 조건은 실데이터 전용 — 빈 셀은 프레임만(데이터 위조 금지 — B26 정책 승계).
        /// </summary>
        private VisualElement BuildInventoryCell(int index)
        {
            var cell = new UTKSlot { name = "InventoryCell_" + index };   // [144차 P0-② B37] V3 어휘 — TierStrip/Count 내장
            ApplyFigmaSlotStyle(cell);   // [144차 P1-EQ B36] bg #21262D@0.6 · 크롬 테두리 1.2 · r9.6
            cell.style.width = 81.6f;
            cell.style.height = 81.6f;
            cell.style.flexShrink = 0f;
            cell.style.marginLeft = 0f;
            cell.style.marginTop = 0f;
            int column = index % 5;
            int row = index / 5;
            cell.style.marginRight = column == 4 ? 0f : 9.6f;
            cell.style.marginBottom = row == 4 ? 0f : 14.4f;
            cell.pickingMode = PickingMode.Ignore;   // 호버 강조 미사용(현행 유지)

            // [144차 P1-EQ B39] 아이콘 38.4×38.4 중앙(85:104) — 인벤 장비 카드 선례와 동일 기하
            var icon = cell.Q<VisualElement>("Icon");
            if (icon != null)
            {
                icon.style.position = Position.Absolute;
                icon.style.left = 21.6f;
                icon.style.top = 21.6f;
                icon.style.width = 38.4f;
                icon.style.height = 38.4f;
                icon.style.flexGrow = 0f;
                icon.pickingMode = PickingMode.Ignore;
            }

            // [144차 P1-EQ B38] Count 우하단 (right 7.6, bottom 4.6) — 13.2/500 #8B949E Geist Mono(93:93, 에셋 미보유 → UIFont 폴백)
            var count = cell.Q<Label>("Count");
            if (count != null)
            {
                count.style.position = Position.Absolute;
                count.style.right = 7.6f;
                count.style.bottom = 4.6f;
                count.style.height = 17f;
                count.style.fontSize = 13.2f;
                count.style.unityFontStyleAndWeight = FontStyle.Normal;
                count.style.color = new StyleColor(UTKTheme.TextSub);
                count.style.unityFont = GetGeistMonoFont();
                count.style.marginRight = 0f;
                count.style.marginBottom = 0f;
                count.style.unityTextAlign = TextAnchor.MiddleRight;
            }

            // [144차 P0-② B37] TierStrip 상시 존재(25셀 전부) — 표시/등급색은 갱신 루프에서 실데이터로 판정
            cell.SetTierStrip(true, UTKTheme.Accent);
            _inventoryPackCells[index] = cell;
            return cell;
        }

        private void RefreshInventoryVisuals()
        {
            PlayerInventory inventory = PlayerInventory.Instance;
            PlayerInventory.ItemSlot[] slots = inventory != null ? inventory.GetAllSlots() : null;
            for (int i = 0; i < _inventoryPackCells.Length; i++)
            {
                var cell = _inventoryPackCells[i];
                if (cell == null) continue;
                PlayerInventory.ItemSlot slot = slots != null && i < slots.Length ? slots[i] : null;
                var item = slot != null ? slot.item : null;
                if (item != null)
                {
                    // [144차 P1-EQ B39] 실아이콘(ItemIconDatabase) + 툴팁(아이템명 — 인벤 [P20-5] 선례)
                    cell.SetEmpty(false);
                    cell.SetIcon(ItemIconDatabase.GetOrCreateIcon(item));
                    cell.tooltip = item.displayName ?? item.id ?? "?";
                    cell.SetCount(slot.count);
                    // [144차 P0-② B37] TierStrip — 등급 실데이터색(RarityColor, B25 매핑 유지)
                    cell.SetTierStrip(true, RarityColor(item.rarity));
                }
                else
                {
                    // 빈 셀 — 프레임만(아이콘/카운트/스트립 비표시 — 데이터 위조 금지 계약, B26 정책 승계)
                    cell.SetEmpty(true);
                    cell.SetIcon(null);
                    cell.tooltip = null;
                    cell.SetCount(0);
                    cell.SetTierStrip(false, Color.clear);
                }
            }
        }

        /// <summary>장비슬롯 1개 (박스 + 슬롯명 + 아이템명). slot = EquipmentSlot enum.</summary>
        private VisualElement BuildEquipSlotBox(EquipmentManager.EquipmentSlot slot, string label)
        {
            var wrap = new VisualElement { name = "EquipSlot_" + slot };
            wrap.style.flexDirection = FlexDirection.Column;
            wrap.style.alignItems = Align.Center;
            wrap.style.marginTop = 3f;
            wrap.style.marginBottom = 3f;
            wrap.style.marginLeft = 3f;
            wrap.style.marginRight = 3f;

            var slotBox = new VisualElement();
            slotBox.AddToClassList("utk-slot");
            ApplyDarkSlotStyle(slotBox);   // [GitHub-dark] 우드 배경 제거 → 다크 인셋 패널
            slotBox.style.width = 92f;
            slotBox.style.height = 92f;
            var slotName = MkLabel(label, 13.2f, GitHubDark.TextSub, TextAnchor.MiddleCenter);
            slotBox.Add(slotName);
            wrap.Add(slotBox);

            _equipSlotLabels[(int)slot] = MkLabel("—", 13.2f, GitHubDark.TextMain, TextAnchor.MiddleCenter);
            wrap.Add(_equipSlotLabels[(int)slot]);
            return wrap;
        }

        private void BuildRightZone()
        {
            var right = new VisualElement();
            right.name = "LegacySupplementaryDetails";
            right.style.width = new Length(100f, LengthUnit.Percent);
            right.style.flexShrink = 0;
            right.style.flexDirection = FlexDirection.Column;
            right.style.marginTop = 18f;
            right.style.paddingTop = 12f;
            AddSep(right, 0f, 10f);
            right.Add(MkLabel("상세 정보 · 스탯 배분", 14.4f, GitHubDark.Gold, TextAnchor.MiddleLeft));
            _statusContent.Add(right);

            // ── 남은 포인트 ──
            _pendingLabel = MkLabel("남은 포인트: 0", 16.8f, GitHubDark.TextSub, TextAnchor.MiddleRight);
            right.Add(_pendingLabel);

            // ── 주스탯 4행 (이름 / 할당치 / 효과 / [+] 버튼) ──
            for (int i = 0; i < 4; i++)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginTop = 6f;

                var name = MkLabel(_statKindNames[i], 21.6f, GitHubDark.TextSub, TextAnchor.MiddleLeft);
                name.style.width = 64f;
                row.Add(name);

                _allocValueLabels[i] = MkLabel("5", 21.6f, GitHubDark.TextMain, TextAnchor.MiddleLeft);
                _allocValueLabels[i].style.width = 70f;
                row.Add(_allocValueLabels[i]);

                var desc = MkLabel(_statKindEffects[i], 12, GitHubDark.TextMain, TextAnchor.MiddleLeft);
                desc.style.opacity = 0.7f;
                desc.style.flexGrow = 1f;
                row.Add(desc);

                PlayerStats.StatKind kind = _statKinds[i];
                var plus = UTKButton.Create("+", () =>
                {
                    var st = PlayerStats.Instance;
                    if (st != null && st.AllocateStat(kind)) RefreshDisplay();
                }, UTKButton.Variant.Primary);
                plus.style.width = 40f;
                plus.style.height = 28f;
                plus.name = "Allocate_" + new[] { "Str", "Agi", "Int", "Vit" }[i];
                StyleButton(plus, UTKButton.Variant.Primary);   // [GitHub-dark] 버튼 인라인 오버라이드
                _allocButtons[i] = plus;
                row.Add(plus);

                right.Add(row);
            }

            AddSep(right, 10f, 6f);

            // ── 전투 스탯 8행 (이름 / 값 / 근거 노트) ──
            for (int i = 0; i < 8; i++)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginTop = 2f;

                var name = MkLabel(_rowNames[i], 16.8f, GitHubDark.TextSub, TextAnchor.MiddleLeft);
                name.style.width = 110f;
                row.Add(name);

                _statValueLabels[i] = MkLabel("-", 16.8f, GitHubDark.TextMain, TextAnchor.MiddleRight);
                _statValueLabels[i].style.width = 120f;
                row.Add(_statValueLabels[i]);

                _bonusNoteLabels[i] = MkLabel("", 12, GitHubDark.TextSub, TextAnchor.MiddleLeft);
                _bonusNoteLabels[i].style.flexGrow = 1f;
                _bonusNoteLabels[i].style.whiteSpace = WhiteSpace.Normal;
                row.Add(_bonusNoteLabels[i]);

                right.Add(row);
            }

            AddSep(right, 8f, 6f);

            // ── 체력 / 경험치 게이지 + 값 ──
            var hpGauge = BuildGaugeRow("체력", GitHubDark.Health);
            _hpValueLabel = hpGauge.value;
            _hpFill = hpGauge.fill;
            right.Add(hpGauge.root);

            var expGauge = BuildGaugeRow("경험치", GitHubDark.Accent);
            _expValueLabel = expGauge.value;
            _expFill = expGauge.fill;
            right.Add(expGauge.root);

            // ── 허기 게이지 [O10] — 읽기 전용 (HungerSystem이 소스, 조작 없음) ──
            var hungerGauge = BuildGaugeRow("허기", GitHubDark.Accent);
            _hungerValueLabel = hungerGauge.value;
            _hungerFill = hungerGauge.fill;
            right.Add(hungerGauge.root);

            // ── 중독 ──
            _addictionLabel = MkLabel("중독: 0%", 14.4f, GitHubDark.RankEpic, TextAnchor.MiddleLeft);
            _addictionLabel.style.marginTop = 6f;
            right.Add(_addictionLabel);

            // ── 칭호 [Phase O6 C-O6-03] — 해금 순환 장착 ──
            AddSep(right, 10f, 6f);
            var titleHeader = MkLabel("칭호", 16.8f, GitHubDark.Gold, TextAnchor.MiddleLeft);
            titleHeader.style.marginTop = 4f;
            right.Add(titleHeader);

            _titleValueLabel = MkLabel(GetTitleDisplay(), 14.4f, GitHubDark.TextMain, TextAnchor.MiddleLeft);
            _titleValueLabel.style.whiteSpace = WhiteSpace.Normal;
            right.Add(_titleValueLabel);

            var titleBtn = UTKButton.Create("칭호 변경", CycleEquipTitle, UTKButton.Variant.Secondary);
            titleBtn.name = "TitleCycleButton";
            StyleButton(titleBtn, UTKButton.Variant.Secondary);   // [GitHub-dark] 버튼 인라인 오버라이드
            titleBtn.style.marginTop = 6f;
            titleBtn.style.height = 26f;
            right.Add(titleBtn);

            // ── [P5] 경제 감사 리포트 버튼 — 골드 원장 즉석 확인 ──
            var auditBtn = UTKButton.Create("감사 리포트", ShowAuditReport, UTKButton.Variant.Secondary);
            auditBtn.name = "AuditReportButton";
            StyleButton(auditBtn, UTKButton.Variant.Secondary);   // [GitHub-dark] 버튼 인라인 오버라이드
            auditBtn.style.marginTop = 6f;
            auditBtn.style.height = 26f;
            right.Add(auditBtn);
        }

        /// <summary>[O6] 현재 칭호 표시 문자열 (미장착 = "—").</summary>
        private string GetTitleDisplay()
        {
            var tm = TitleManager.Instance;
            if (tm == null) return "—";
            string text = tm.GetTitleText();
            return string.IsNullOrEmpty(text) ? "—" : text;
        }

        /// <summary>[O6] 해금 칭호 순환 장착 — 현재 장착 id의 다음(끝이면 첫번째).</summary>
        private void CycleEquipTitle()
        {
            var tm = TitleManager.Instance;
            if (tm == null) return;

            string[] unlocked = tm.GetUnlockedIds();
            if (unlocked == null || unlocked.Length == 0)
            {
                _titleValueLabel.text = "해금된 칭호 없음";
                return;
            }

            string current = tm.EquippedTitleId;
            int next = 0;
            for (int i = 0; i < unlocked.Length; i++)
            {
                if (unlocked[i] == current)
                {
                    next = (i + 1) % unlocked.Length;
                    break;
                }
            }
            tm.EquipTitle(unlocked[next]);
            _titleValueLabel.text = GetTitleDisplay();
            Debug.Log($"[StatusUTK] 칭호 변경 → {tm.GetTitleText()}");
        }

        /// <summary>[P5] 경제 감사 리포트 표시 — EconomyAuditLedger 원장을 토스트로 출력.</summary>
        private void ShowAuditReport()
        {
            var sys = EconomyAuditSystem.Instance;
            float minutes = sys != null ? sys.SessionMinutes : 0f;
            string report = EconomyAuditLedger.GetReport(minutes);
            UTKToastService.Show(report, 6f);
            Debug.Log(report);
        }

        /// <summary>[O6] 칭호 해금 시 라벨 갱신 (TitleManager 구독 — Subscribe/Unsubscribe 쌍).</summary>
        private void OnTitleUnlocked(ProjectName.Core.Data.TitleDef def)
        {
            _titleValueLabel.text = GetTitleDisplay();
        }

        /// <summary>[O10] 허기 변경 시 게이지 갱신 (HungerSystem 정적 이벤트 — Subscribe/Unsubscribe 쌍).</summary>
        private void OnHungerChanged(float hunger)
        {
            UpdateHungerGauge(hunger);
        }

        /// <summary>[O10] 허기 게이지 갱신 — 값 라벨 + fill 너비(100% 기준).</summary>
        private void UpdateHungerGauge(float hunger)
        {
            if (_hungerValueLabel != null) _hungerValueLabel.text = $"{hunger:F0} / 100";
            SetGaugeFill(_hungerFill, hunger / HungerSystem.MaxHunger);

            // [P3 Figma] 특수 상태 게이지1 허기 — 우측 허기 게이지와 동일 소스(HungerSystem) 병행 갱신
            if (_figHungerValueLabel != null) _figHungerValueLabel.text = $"{hunger:F0} / {HungerSystem.MaxHunger:F0}";
            SetGaugeFill(_figHungerFill, hunger / HungerSystem.MaxHunger);
        }

        /// <summary>게이지 행 컨테이너 (root: [라벨][값][게이지배경>fill]).</summary>
        private static GaugeParts BuildGaugeRow(string labelName, Color fillColor)
        {
            var parts = new GaugeParts();

            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Row;
            root.style.alignItems = Align.Center;
            root.style.marginTop = 6f;
            parts.root = root;

            var name = MkLabel(labelName, 16.8f, GitHubDark.TextSub, TextAnchor.MiddleLeft);
            name.style.width = 110f;
            root.Add(name);

            parts.value = MkLabel("-", 15.6f, GitHubDark.TextMain, TextAnchor.MiddleRight);
            parts.value.style.width = 200f;
            root.Add(parts.value);

            // 게이지 (배경 + fill). fill 너비를 percent로 갱신.
            var gaugeBg = new VisualElement();
            gaugeBg.style.flexGrow = 1f;
            gaugeBg.style.height = 10f;
            gaugeBg.style.backgroundColor = new StyleColor(GitHubDark.PanelSub);   // [GitHub-dark] 게이지 트랙
            gaugeBg.style.borderTopWidth = 1f;
            gaugeBg.style.borderBottomWidth = 1f;
            gaugeBg.style.borderTopColor = new StyleColor(GitHubDark.Stroke);
            gaugeBg.style.borderBottomColor = new StyleColor(GitHubDark.Stroke);

            parts.fill = new VisualElement();
            parts.fill.style.height = new Length(100f, LengthUnit.Percent);
            parts.fill.style.width = new Length(0f, LengthUnit.Percent);
            parts.fill.style.backgroundColor = new StyleColor(fillColor);   // [GitHub-dark] fill 색 — 호출부 지정(HP=Health, EXP/허기=Accent)
            gaugeBg.Add(parts.fill);

            root.Add(gaugeBg);
            return parts;
        }

        /// <summary>게이지 행 조립 결과.</summary>
        private sealed class GaugeParts
        {
            public VisualElement root;
            public Label value;
            public VisualElement fill;
        }

        private static void AddSep(VisualElement parent, float marginTop, float marginBottom)
        {
            var sep = new VisualElement();
            sep.style.height = 1f;
            sep.style.backgroundColor = new StyleColor(GitHubDark.Stroke);
            sep.style.marginTop = marginTop;
            sep.style.marginBottom = marginBottom;
            parent.Add(sep);
        }

        /// <summary>[P3 Figma] IdentityRow 배지 스타일 — 보조 패널 배경 + r4 (IStyle 쇼트핸드 없음 → 4면 개별 대입).</summary>
        private static void StyleBadge(VisualElement badge)
        {
            if (badge == null) return;
            badge.style.backgroundColor = new StyleColor(GitHubDark.PanelSub);
            badge.style.paddingLeft = 7f;
            badge.style.paddingRight = 7f;
            badge.style.paddingTop = 3f;
            badge.style.paddingBottom = 3f;
            badge.style.borderTopLeftRadius = 4f;
            badge.style.borderTopRightRadius = 4f;
            badge.style.borderBottomLeftRadius = 4f;
            badge.style.borderBottomRightRadius = 4f;   // 작은배지 r4
        }

        /// <summary>[P3 Figma] 특수 상태 컴팩트 게이지 행 — [라벨][트랙>fill][값]. (GaugeParts 재사용)</summary>
        private static GaugeParts BuildCompactGauge(string labelName, Color fillColor)
        {
            var parts = new GaugeParts();

            // [144차 P0-CSP #42] GaugeContainer 83:63/73 — VERTICAL 2단(라벨행 위 + BarBg 아래) gap 7.2, h36.4.
            //   기존 가로 1행(라벨 52|트랙|값 122) 체계 폐기. [#43] 도트 7.2×7.2 — 색은 [O10] 치환 계약(허기=Accent, 중독도=RankEpic).
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;
            parts.root = root;

            var labelRow = new VisualElement();
            labelRow.style.flexDirection = FlexDirection.Row;
            labelRow.style.alignItems = Align.Center;
            labelRow.style.height = 22f;
            root.Add(labelRow);

            var dot = new VisualElement();
            dot.name = "GaugeDot";
            dot.style.width = 7.2f;
            dot.style.height = 7.2f;
            dot.style.backgroundColor = new StyleColor(fillColor);
            dot.style.borderTopLeftRadius = dot.style.borderTopRightRadius = 3.6f;
            dot.style.borderBottomLeftRadius = dot.style.borderBottomRightRadius = 3.6f;
            dot.style.marginRight = 7.2f;
            labelRow.Add(dot);

            var name = MkLabel(labelName, 15.6f, GitHubDark.TextMain, TextAnchor.MiddleLeft);   // [144차 P1-CSP #44] 12/TextSub→15.6/700 #F0F6FC
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.flexGrow = 1f;
            labelRow.Add(name);

            parts.value = MkLabel("-", 16.8f, GitHubDark.TextMain, TextAnchor.MiddleRight);   // [144차 P0-CSP #45] 값 16.8/700 — 괄호 상태 서브라벨(83:68-70)은 상태 문자열 실데이터 소스 부재로 생략(기록)
            parts.value.style.unityFontStyleAndWeight = FontStyle.Bold;
            labelRow.Add(parts.value);

            var track = new VisualElement();   // [144차 P1-CSP #46] BarBg 83:71/81 — 494.4×7.2, #FFFFFF@0.059, r3.6
            track.style.height = 7.2f;
            track.style.marginTop = 7.2f;
            track.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.059f));
            track.style.borderTopLeftRadius = track.style.borderTopRightRadius = 3.6f;
            track.style.borderBottomLeftRadius = track.style.borderBottomRightRadius = 3.6f;
            parts.fill = new VisualElement();
            parts.fill.style.height = new Length(100f, LengthUnit.Percent);
            parts.fill.style.width = new Length(0f, LengthUnit.Percent);
            parts.fill.style.backgroundColor = new StyleColor(fillColor);   // 허기=Accent, 중독도=RankEpic([O10] 치환 계약)
            parts.fill.style.borderTopLeftRadius = parts.fill.style.borderTopRightRadius = 3.6f;
            parts.fill.style.borderBottomLeftRadius = parts.fill.style.borderBottomRightRadius = 3.6f;
            track.Add(parts.fill);
            root.Add(track);

            return parts;
        }

        // =====================================================================
        //  [GitHub-dark] 스타일 헬퍼 — 시각 전용(기능 로직과 무관), 이 창 한정
        //  =====================================================================

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

        /// <summary>GitHub-dark 슬롯 인셋 — 배경 이미지 제거 + 다크 바탕 + 1px 스트로크 + r6 (이 창 한정).</summary>
        private static void ApplyDarkSlotStyle(VisualElement slot)
        {
            if (slot == null) return;
            slot.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            slot.style.backgroundColor = GitHubDark.BgBase;
            slot.style.borderTopWidth = 1f;
            slot.style.borderBottomWidth = 1f;
            slot.style.borderLeftWidth = 1f;
            slot.style.borderRightWidth = 1f;
            slot.style.borderTopColor = new StyleColor(GitHubDark.Stroke);
            slot.style.borderBottomColor = new StyleColor(GitHubDark.Stroke);
            slot.style.borderLeftColor = new StyleColor(GitHubDark.Stroke);
            slot.style.borderRightColor = new StyleColor(GitHubDark.Stroke);
            slot.style.borderTopLeftRadius = 6f;
            slot.style.borderTopRightRadius = 6f;
            slot.style.borderBottomLeftRadius = 6f;
            slot.style.borderBottomRightRadius = 6f;   // 서브 반경 r6
        }

        /// <summary>[144차 P1-EQ B20/B36] EP 셀 Figma 진실 스타일 — bg #21262D@0.6(93:7/85:102 실효앰파) ·
        /// 테두리 #30363D@0.502 1.2([144차 P0 V2] 어휘) · r9.6. ApplyDarkSlotStyle(CSP Portrait·레거시 공유)과
        /// 분리 — CSP 본체 무수정(Phase 1-② 소관).</summary>
        private static void ApplyFigmaSlotStyle(VisualElement slot)
        {
            if (slot == null) return;
            slot.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            slot.style.backgroundColor = new StyleColor(new Color(0x21 / 255f, 0x26 / 255f, 0x2D / 255f, 0.6f));   // #21262D@0.6
            UTKTheme.ApplyElementBorder(slot);   // [144차 P0 V2] #30363D@0.502 · 1.2
            UTKTheme.ApplyRadius(slot, 9.6f);
        }

        // [144차 P1-EQ B38] Geist Mono 로드(Figma 패밀리 계약) — 에셋 미보유 실측(2026-10-24) → UIFont 폴백
        // (LootWindowUTK/ItemDescriptionWindowUTK 선례). Resources/Fonts/GeistMono 추가 시 자동 적용.
        private static Font _geistMono;

        private static Font GetGeistMonoFont()
        {
            if (_geistMono == null)
                _geistMono = Resources.Load<Font>("Fonts/GeistMono");
            return _geistMono != null ? _geistMono : UIFont.Load();
        }

        // =====================================================================
        //  생명주기 / 토글 패리티
        // =====================================================================

        public override void Show()
        {
            base.Show();
            if (_scaleRoot == null) _scaleRoot = parent;
            ApplyCanvasScale(_scaleRoot);
            if (_scaleRefresh == null)
                _scaleRefresh = schedule.Execute(() => ApplyCanvasScale(_scaleRoot)).Every(250);
            EnsureSubscriptions();
            RefreshDisplay();
            Debug.Log(_statusOnly
                ? "[StatusWindowUTK] status-only route opened"
                : "[StatusWindowUTK] 캐릭터 정보 창 열림 (키: P)");
        }

        public override void Hide()
        {
            base.Hide();
            Debug.Log("[StatusWindowUTK] 캐릭터 정보 창 닫힘");
        }

        /// <summary>원본 정적 진입점 패리티 — 레벨업 토스트 표시.</summary>
        public static void ShowLevelUpPopup(int newLevel)
        {
            if (_instance == null) return;
            UTKToastService.Show($"LEVEL UP!  Lv.{newLevel}   (+{PlayerStats.StatPointsPerLevel} 포인트)", 2.4f);
        }


        internal void ApplyCanvasScale(VisualElement canvasRoot)
        {
            if (canvasRoot == null) return;
            _scaleRoot = canvasRoot;
            // [Figma 정합 v3] 등배수 디자인공간 — 창 박스=프레임×k, _content는 raw Figma px + scale=k.
            // 패널은 빌드 시의 raw Figma 좌표 그대로(542~) — 기존 X/Y 독립 재스케일(비-16:9 신장 원인)은 제거.
            Rect frame = _statusOnly
                ? new Rect(0f, 0f, StatusOnlyFrameWidth, StatusOnlyFrameHeight)
                : CompositionCanvasBounds;
            FigmaCanvasLayout.ApplyDesignSpace(this, _content, frame, canvasRoot);
        }

        // =====================================================================
        //  구독 관리 (씬 전환 위생 — 원본과 동일)
        // =====================================================================

        private void EnsureSubscriptions()
        {
            var stats = PlayerStats.Instance;
            if (!ReferenceEquals(_subscribedStats, stats))
            {
                if (_subscribedStats != null) _subscribedStats.OnLevelChanged -= OnStatsLevelChanged;
                _subscribedStats = stats;
                if (_subscribedStats != null) _subscribedStats.OnLevelChanged += OnStatsLevelChanged;
            }

            var em = EquipmentManager.Instance;
            if (!ReferenceEquals(_subscribedEquip, em))
            {
                if (_subscribedEquip != null) _subscribedEquip.OnEquipmentChanged -= OnEquipmentChanged;
                _subscribedEquip = em;
                if (_subscribedEquip != null) _subscribedEquip.OnEquipmentChanged += OnEquipmentChanged;
            }

            // [O6] 칭호 해금 — 정적 이벤트 1회 구독(플래그 중복 방지)
            if (!_titleSubscribed)
            {
                TitleManager.TitleUnlocked += OnTitleUnlocked;
                _titleSubscribed = true;
                if (_titleValueLabel != null) _titleValueLabel.text = GetTitleDisplay();
            }

            // [O10] 허기 변경 — 정적 이벤트 1회 구독(플래그 중복 방지, 칭호 패턴 동일)
            if (!_hungerSubscribed)
            {
                HungerSystem.HungerChanged += OnHungerChanged;
                _hungerSubscribed = true;
                var hs = HungerSystem.Instance;
                if (hs != null) UpdateHungerGauge(hs.Hunger);
            }
        }

        private void UnsubscribeAll()
        {
            if (_titleSubscribed)
            {
                TitleManager.TitleUnlocked -= OnTitleUnlocked;
                _titleSubscribed = false;
            }

            // [O10] 허기 정적 이벤트 해제
            if (_hungerSubscribed)
            {
                HungerSystem.HungerChanged -= OnHungerChanged;
                _hungerSubscribed = false;
            }

            if (_subscribedStats != null)
            {
                _subscribedStats.OnLevelChanged -= OnStatsLevelChanged;
                _subscribedStats = null;
            }
            if (_subscribedEquip != null)
            {
                _subscribedEquip.OnEquipmentChanged -= OnEquipmentChanged;
                _subscribedEquip = null;
            }
        }

        private void OnStatsLevelChanged(int newLevel, int oldLevel)
        {
            if (IsOpen) RefreshDisplay();
            if (this == _instance) ShowLevelUpPopup(newLevel);
        }

        private void OnEquipmentChanged(EquipmentManager.EquipmentSlot slot, string itemId)
        {
            if (IsOpen) RefreshDisplay();
        }

        // =====================================================================
        //  데이터 갱신 — 원본 데이터 경로 그대로
        // =====================================================================

        private void RefreshDisplay()
        {
            EnsureSubscriptions();

            // --- [P3 Figma] IdentityRow 등급 배지 — TitleManager 소스 (우측 칭호 라벨과 동일 데이터) ---
            if (_identityTitleLabel != null) _identityTitleLabel.text = GetTitleDisplay();

            // --- 허기 [O10] — PlayerStats와 무관하게 항상 갱신 (읽기 전용) ---
            var hungerSys = HungerSystem.Instance;
            if (_statusOnly && hungerSys == null)
            {
                if (_hungerValueLabel != null) _hungerValueLabel.text = "-";
                if (_figHungerValueLabel != null) _figHungerValueLabel.text = "-";
                SetGaugeFill(_hungerFill, 0f);
                SetGaugeFill(_figHungerFill, 0f);
            }
            else
            {
                UpdateHungerGauge(hungerSys != null ? hungerSys.Hunger : HungerSystem.MaxHunger);
            }

            PlayerStats stats = PlayerStats.Instance;
            if (stats == null)
            {
                if (_expValueLabel != null) _expValueLabel.text = "-";
                if (_hpValueLabel != null) _hpValueLabel.text = "-";
                if (_identityLevelLabel != null) _identityLevelLabel.text = "Lv.-";
                if (_levelBlockValueLabel != null) _levelBlockValueLabel.text = "-";
                if (_expPercentLabel != null) _expPercentLabel.text = "EXP -%";
                if (_statusOnly)
                {
                    if (_pendingLabel != null) _pendingLabel.text = "남은 포인트: -";
                    for (int i = 0; i < _allocValueLabels.Length; i++)
                    {
                        if (_allocValueLabels[i] != null) _allocValueLabels[i].text = "-";
                        if (_allocButtons[i] != null) _allocButtons[i].SetEnabled(false);
                    }
                }
                return;
            }

            // --- 경험치 ---
            int level = stats.Level;
            // --- [P3 Figma] IdentityRow 레벨 배지 + LevelBlock 숫자 ---
            if (_identityLevelLabel != null) _identityLevelLabel.text = $"Lv.{level}";
            if (_levelBlockValueLabel != null) _levelBlockValueLabel.text = level.ToString();
            if (level >= PlayerStats.MaxLevel)
            {
                if (_expValueLabel != null) _expValueLabel.text = "MAX";
                if (_expPercentLabel != null) _expPercentLabel.text = "EXP MAX";
                SetGaugeFill(_expFill, 1f);
            }
            else
            {
                int curExp = stats.CurrentEXP;
                int nextExp = stats.GetExpForLevel(level + 1);
                int needExp = Mathf.Max(0, nextExp - curExp);
                if (_expValueLabel != null)
                    _expValueLabel.text = $"{curExp:N0} / {nextExp:N0}  (남은 {needExp:N0})";
                int prevExp = stats.GetExpForLevel(level);
                int span = Mathf.Max(1, nextExp - prevExp);
                float ratio = Mathf.Clamp01((curExp - prevExp) / (float)span);
                SetGaugeFill(_expFill, ratio);
                if (_expPercentLabel != null) _expPercentLabel.text = $"EXP {ratio * 100f:F0}%";   // [P3 Figma] LevelBlock EXP 비율
            }

            // --- 체력 ---
            PlayerHealth health = PlayerHealth.Instance;
            float maxHP = health != null ? health.MaxHP : stats.HPBase;
            bool hasAuthoritativeHealth = health != null || !_statusOnly;
            float curHP = health != null ? health.CurrentHP : maxHP;
            if (_hpValueLabel != null) _hpValueLabel.text = hasAuthoritativeHealth ? $"{curHP:F0} / {stats.HPBase:F0}" : "-";
            float hpRatio = hasAuthoritativeHealth && stats.HPBase > 0 ? Mathf.Clamp01(curHP / stats.HPBase) : 0f;
            SetGaugeFill(_hpFill, hpRatio);
            // --- [P3 Figma] Portrait HP 오버레이 (표시 전용 — 우측 체력 게이지와 동일 데이터) ---
            if (_portraitHpLabel != null) _portraitHpLabel.text = hasAuthoritativeHealth ? $"{curHP:F0} / {stats.HPBase:F0}" : "-";
            SetGaugeFill(_portraitHpFill, hpRatio);

            // --- [P3 Figma] CoreStatsGrid: 공격/방어/최대체력/민첩 (전투 행과 동일 API 소비) ---
            if (_coreValueLabels[0] != null) _coreValueLabels[0].text = $"{stats.FinalAttackDamage:F1}";
            if (_coreValueLabels[1] != null) _coreValueLabels[1].text = $"{stats.FinalDefense:F1}";
            if (_coreValueLabels[2] != null) _coreValueLabels[2].text = $"{stats.HPBase:F0}";
            if (_coreValueLabels[3] != null) _coreValueLabels[3].text = $"{stats.FinalMoveSpeed:F1}";

            // --- 주스탯 + [+] 버튼 ---
            bool canAllocate = stats.PendingStatPoints > 0;
            if (_pendingLabel != null)
            {
                _pendingLabel.text = $"남은 포인트: {stats.PendingStatPoints}";
                _pendingLabel.style.color = new StyleColor(stats.PendingStatPoints > 0 ? GitHubDark.Gold : GitHubDark.TextSub);
            }
            for (int i = 0; i < 4; i++)
            {
                if (_allocValueLabels[i] != null)
                    _allocValueLabels[i].text = $"{_statKindNames[i]} {stats.GetAllocatedStat(_statKinds[i])}";
                if (_allocButtons[i] != null)
                    _allocButtons[i].SetEnabled(canAllocate);
            }

            // --- 전투/정보 행 ---
            SetRow(0, $"{stats.FinalAttackDamage:F1}");
            SetRow(1, $"{stats.FinalDefense:F1}");
            SetRow(2, $"{stats.FinalCritChance * 100f:F1}%");
            SetRow(3, $"{stats.FinalMoveSpeed:F1}");
            SetRow(4, $"+{stats.FinalAlchemyBonus * 100f:F1}%");
            SetRow(5, $"+{stats.FinalCookingBonus * 100f:F1}%");
            SetRow(6, $"+{stats.SpeechSkill}");
            SetRow(7, $"{stats.Gold:N0} G");

            SetBonusNote(0, "힘 → 공격 +2/pt");
            SetBonusNote(1, "민첩 → 치명/속도");
            SetBonusNote(2, "지능 → 제조 성공");
            SetBonusNote(3, "체력 → 최대HP");
            SetBonusNote(4, $"장비 공+{EquipmentStatBonusApplier.GetAttackBonus():F0} 방+{EquipmentStatBonusApplier.GetDefenseBonus():F0}");
            SetBonusNote(5, $"장비 치+{EquipmentStatBonusApplier.GetCritBonus() * 100f:F1}% 속+{EquipmentStatBonusApplier.GetSpeedBonus():F1}");
            SetBonusNote(6, $"레벨 보정 공+{level * 0.5f:F0} 방+{level * 0.2f:F0}");
            SetBonusNote(7, $"전투 보너스 +{stats.CombatDamageBonus * 100f:F0}%");

            // --- 장비 슬롯 갱신 (EquipmentSlot enum 전체) ---
            var em = EquipmentManager.Instance;
            for (int i = 0; i < System.Enum.GetValues(typeof(EquipmentManager.EquipmentSlot)).Length; i++)
            {
                EquipmentManager.EquipmentSlot slot = (EquipmentManager.EquipmentSlot)i;
                var data = em != null ? em.GetSlotData(slot) : null;
                var item = data != null ? data.itemData : null;
                bool hasItem = item != null && !string.IsNullOrEmpty(data.itemId);
                // [144차 P1-EQ B27] 아이콘 38.4 중앙 — 실아이콘(ItemIconDatabase)·툴팁(아이템명). 빈 슬롯 아이콘 제거([P20-5] 선례).
                var icon = _equippedIconHosts[(int)slot];
                if (icon != null)
                {
                    icon.style.backgroundImage = UTKTextureSafe.ToBackground(hasItem ? ItemIconDatabase.GetOrCreateIcon(item) : null);
                    icon.tooltip = hasItem ? (item.displayName ?? data.itemId) : null;
                }
                // 레거시 상태전용 라벨(존재 시 유지 — 컴포지션 루트는 아이콘+툴팁으로 대체)
                Label label = _equipSlotLabels[(int)slot];
                if (label != null)
                    label.text = hasItem ? EquipmentStatBonusApplier.DisplayName(data.itemId) : "—";
                // [Figma 93:10 / B24·B26] TierStrip — 장착 시 등급색, 빈 슬롯 숨김(데이터 위조 금지 계약)
                var strip = _equipTierStrips[(int)slot];
                if (strip != null)
                {
                    strip.style.display = hasItem ? DisplayStyle.Flex : DisplayStyle.None;
                    if (hasItem) strip.style.backgroundColor = new StyleColor(RarityColor(item.rarity));
                }
            }

            // --- 장비 보너스 내역 ---
            if (_bonusListLabel != null)
            {
                var labels = EquipmentStatBonusApplier.GetActiveBonusLabels();
                _bonusListLabel.text = labels.Count > 0 ? string.Join("\n", labels) : "착용 장비 보너스 없음";
            }

            RefreshInventoryVisuals();

            // --- 중독 (정보 섹션) ---
            if (_addictionLabel != null)
                _addictionLabel.text = $"중독: {DrugEffectSystem.DrugAddictionLevel:F0}% ({DrugEffectSystem.GetAddictionLabel()})";
            // --- [P3 Figma] 특수 상태 게이지2 중독도 (우측 라벨과 동일 소스) ---
            if (_figAddictionLabel != null)
                _figAddictionLabel.text = $"{DrugEffectSystem.DrugAddictionLevel:F0}% ({DrugEffectSystem.GetAddictionLabel()})";
            SetGaugeFill(_figAddictionFill, Mathf.Clamp01(DrugEffectSystem.DrugAddictionLevel / 100f));
        }

        private void SetRow(int i, string value)
        {
            if (i < 0 || i >= _statValueLabels.Length) return;
            if (_statValueLabels[i] != null) _statValueLabels[i].text = value;
        }

        private void SetBonusNote(int i, string note)
        {
            if (i < 0 || i >= _bonusNoteLabels.Length) return;
            if (_bonusNoteLabels[i] != null) _bonusNoteLabels[i].text = note;
        }

        private static void SetGaugeFill(VisualElement fill, float ratio)
        {
            if (fill == null) return;
            fill.style.width = new Length(Mathf.Clamp01(ratio) * 100f, LengthUnit.Percent);
        }

        // =====================================================================
        //  폴링 / 토글용 Updater — 원본 Update() 대응 (씬과 무관)
        // =====================================================================

        private class Updater : MonoBehaviour
        {
            public StatusWindowUTK window;
            private bool _listenForInput = true;

            public void Configure(StatusWindowUTK target, bool listenForInput)
            {
                window = target;
                _listenForInput = listenForInput;
            }

            private void Update()
            {
                // 부트스트랩 완료 전이어도 최초 부착을 멱등으로 시도
                var root = UIToolkitBootstrap.UIRoot;
                if (root != null && window != null && window.parent == null)
                    root.Add(window);
                if (root != null && window != null)
                    window.ApplyCanvasScale(root);

                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (_listenForInput && kb != null)
                {
                    if (kb.pKey.wasPressedThisFrame && window != null) window.Toggle();
                    if (kb.escapeKey.wasPressedThisFrame && window != null && window.IsOpen) window.Close();
                }

                if (window == null || !window.IsOpen) return;
                window.RefreshDisplay();   // 구독 위생 (씬 전환 대비)
                window._refreshTimer += Time.unscaledDeltaTime;
                if (window._refreshTimer < 0.25f) return;
                window._refreshTimer = 0f;
                window.RefreshDisplay();
            }

            private static bool IsToggleKey(string keyName) => keyName == "pKey";

            private void OnDestroy()
            {
                if (window != null)
                {
                    window.UnsubscribeAll();
                    window.RemoveFromHierarchy();
                }
            }
        }

        // =====================================================================
        //  내부 헬퍼
        // =====================================================================

        private static Label MkLabel(string text, float size, Color color, TextAnchor align)
        {
            var l = new Label(text ?? "");
            l.style.fontSize = size;
            l.style.color = new StyleColor(color);
            l.style.unityTextAlign = align;
            return l;
        }
    }
}