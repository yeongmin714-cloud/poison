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
        private readonly Label[] _inventoryCellLabels = new Label[40];
        private VisualElement _inventoryPackGrid;
        private VisualElement _inventoryPackScroll;

        // [P3 Figma] CoreStatsGrid 4행 라벨 — 값은 FinalAttackDamage/FinalDefense/HPBase/FinalMoveSpeed
        private static readonly string[] _coreStatNames = { "공격", "방어", "최대체력", "민첩" };

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
                figHeader.Add(MkLabel("특수 상태 게이지", 14, GitHubDark.TextMain, TextAnchor.MiddleLeft));
                var sv = MkLabel("SPECIAL VITALS", 11, GitHubDark.TextSub, TextAnchor.MiddleRight);
                sv.style.flexGrow = 1f;
                figHeader.Add(sv);
                _figSpecialSection.Insert(0, figHeader);
            }

            _equipmentPanel = BuildPanel("EquipmentPanel", EquipmentPanelLocalBounds);
            _content.Add(_equipmentPanel);
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
            UTKTheme.ApplyFigmaGlass(panel);   // [Figma 글래스] #161B22@0.85(사용자 조정) + #30363D@0.5 1.2px + r14.4
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

            // ── [Figma] IdentityRow: live title/level badges; no character-name source is mapped ──
                        var identity = new VisualElement();
            identity.name = "IdentityRow";
            identity.style.flexDirection = FlexDirection.Row;
            identity.style.alignItems = Align.Center;
            identity.style.height = 85f;   // [Figma Identity 528×85] 흐름 체인 유지 — Portrait 200.2 착좌
            left.Add(identity);

            _identityTitleLabel = MkLabel(GetTitleDisplay(), UTKTheme.FontRowLabel, GitHubDark.Gold, TextAnchor.MiddleLeft);   // [Figma] 15.6/700(등급색 유지)
            _identityTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            StyleBadge(_identityTitleLabel);
            identity.Add(_identityTitleLabel);

            _identityLevelLabel = MkLabel("Lv.-", UTKTheme.FontRowLabel, GitHubDark.Accent, TextAnchor.MiddleLeft);   // [Figma] 15.6/700
            _identityLevelLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _identityLevelLabel.style.marginLeft = 6f;
            StyleBadge(_identityLevelLabel);
            identity.Add(_identityLevelLabel);

            var identitySpacer = new VisualElement();
            identitySpacer.style.flexGrow = 1f;
            identity.Add(identitySpacer);

            // No character-name source exists in this owner; do not invent an identity value.

            // ── [Figma] Portrait: 3D 뷰포트 placeholder (원본 3D 프리뷰 자리 — 시각 요소이므로 자리만 유지) ──
            var viewport = new VisualElement();
            viewport.name = "Viewport";
            viewport.AddToClassList("utk-slot");
            ApplyDarkSlotStyle(viewport);   // [GitHub-dark] 우드 배경 제거 → 다크 인셋 패널
            viewport.style.height = 312f;   // [Figma Portrait 528×312]
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
            hpOverlay.style.backgroundColor = new StyleColor(new Color32(0x0B, 0x0E, 0x14, 0xD9));   // 반투명 최배경
            hpOverlay.style.flexDirection = FlexDirection.Row;
            hpOverlay.style.alignItems = Align.Center;
            hpOverlay.style.paddingLeft = 14.4f;
            hpOverlay.style.paddingRight = 14.4f;
            viewport.Add(hpOverlay);

            var portraitHint = MkLabel("3D 미리보기", 11, GitHubDark.TextSub, TextAnchor.MiddleLeft);
            portraitHint.style.flexGrow = 1f;
            hpOverlay.Add(portraitHint);

            _portraitHpLabel = MkLabel("-", 11, GitHubDark.TextMain, TextAnchor.MiddleRight);
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
            var statsRow = new VisualElement { name = "StatsSection" };
            statsRow.style.flexDirection = FlexDirection.Row;
            statsRow.style.marginTop = 19.2f;
            statsRow.style.flexShrink = 0f;
            left.Add(statsRow);

            var levelBlock = new VisualElement { name = "LevelBlock" };
            levelBlock.style.width = 120f;
            levelBlock.style.flexShrink = 0f;
            levelBlock.style.flexDirection = FlexDirection.Column;
            levelBlock.style.alignItems = Align.Center;
            levelBlock.style.justifyContent = Justify.Center;
            levelBlock.style.backgroundColor = new StyleColor(GitHubDark.PanelSub);
            levelBlock.style.borderTopLeftRadius = 8f;
            levelBlock.style.borderTopRightRadius = 8f;
            levelBlock.style.borderBottomLeftRadius = 8f;
            levelBlock.style.borderBottomRightRadius = 8f;
            levelBlock.style.marginRight = 24f;
            statsRow.Add(levelBlock);

            var lvCaption = MkLabel("LEVEL", UTKTheme.FontBadge, GitHubDark.TextSub, TextAnchor.MiddleCenter);   // [Figma] 12/400
            lvCaption.style.unityFontStyleAndWeight = FontStyle.Normal;
            levelBlock.Add(lvCaption);
            _levelBlockValueLabel = MkLabel("1", UTKTheme.FontBigNumber, GitHubDark.Accent, TextAnchor.MiddleCenter);   // [Figma] 38.4/800 #58A6FF
            _levelBlockValueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            levelBlock.Add(_levelBlockValueLabel);
            _expPercentLabel = MkLabel("EXP 0%", UTKTheme.FontSubtitle, GitHubDark.Gold, TextAnchor.MiddleCenter);   // [Figma] 13.2/500
            _expPercentLabel.style.marginTop = 6f;
            levelBlock.Add(_expPercentLabel);

            var coreGrid = new VisualElement { name = "CoreStatsGrid" };
            coreGrid.style.flexGrow = 1f;
            coreGrid.style.backgroundColor = new StyleColor(GitHubDark.PanelSub);
            coreGrid.style.borderTopLeftRadius = 8f;
            coreGrid.style.borderTopRightRadius = 8f;
            coreGrid.style.borderBottomLeftRadius = 8f;
            coreGrid.style.borderBottomRightRadius = 8f;
            coreGrid.style.paddingLeft = coreGrid.style.paddingRight = 14.4f;
            coreGrid.style.paddingTop = coreGrid.style.paddingBottom = 12f;
            statsRow.Add(coreGrid);

            for (int i = 0; i < _coreStatNames.Length; i++)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.height = 22f;   // [Figma StatsGrid 행 22]

                var name = MkLabel(_coreStatNames[i], UTKTheme.FontRowLabel, GitHubDark.TextSub, TextAnchor.MiddleLeft);   // [Figma] 15.6/500
                name.name = "CoreStat_" + new[] { "Attack", "Defense", "MaxHealth", "Agility" }[i];
                name.style.width = 72f;
                row.Add(name);

                var vsep = new VisualElement();   // 이름 | 값 세로 구분선
                vsep.style.width = 1f;
                vsep.style.height = 12f;
                vsep.style.backgroundColor = new StyleColor(GitHubDark.Stroke);
                vsep.style.marginRight = 10f;
                row.Add(vsep);

                _coreValueLabels[i] = MkLabel("-", UTKTheme.FontRowValue, GitHubDark.Accent, TextAnchor.MiddleRight);   // [Figma] 16.8/700 #58A6FF
                _coreValueLabels[i].style.unityFontStyleAndWeight = FontStyle.Bold;
                _coreValueLabels[i].style.flexGrow = 1f;
                row.Add(_coreValueLabels[i]);

                coreGrid.Add(row);
                if (i < _coreStatNames.Length - 1)
                {
                    var hsep = new VisualElement();   // [Figma] 행 사이 라인(전후 gap 9.6)
                    hsep.style.height = 1f;
                    hsep.style.backgroundColor = new StyleColor(GitHubDark.Stroke);
                    hsep.style.marginTop = 9.6f;
                    hsep.style.marginBottom = 9.6f;
                    coreGrid.Add(hsep);
                }
            }

            // ── [Figma] SpecialStatsSection "특수 상태" 게이지 2개 ──
            AddSep(left, 12f, 10f);
            _figSpecialSection = new VisualElement { name = "SpecialStatsSection" };
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
            _statusPanel.style.backgroundColor = new StyleColor(UTKTheme.GlassPanelFill);   // [Figma 글래스 0.85]
            _statusPanel.style.borderTopWidth = _statusPanel.style.borderBottomWidth = 1.2f;
            _statusPanel.style.borderLeftWidth = _statusPanel.style.borderRightWidth = 1f;
            _statusPanel.style.borderTopColor = _statusPanel.style.borderBottomColor = new StyleColor(GitHubDark.Stroke);
            _statusPanel.style.borderLeftColor = _statusPanel.style.borderRightColor = new StyleColor(GitHubDark.Stroke);
            _statusPanel.style.paddingLeft = _statusPanel.style.paddingRight = 24f;
            _statusPanel.style.paddingTop = _statusPanel.style.paddingBottom = 18f;

            if (_statusCloseButton != null) _statusCloseButton.RemoveFromHierarchy();
            var title = new VisualElement { name = "CharacterStatusTitleGroup" };
            title.style.position = Position.Absolute;
            title.style.left = 24f;
            title.style.top = 24f;
            title.style.width = 528f;
            title.style.height = 52.8f;
            title.style.flexDirection = FlexDirection.Row;
            title.style.alignItems = Align.Center;
            var titleText = MkLabel("상태 정보", 20f, GitHubDark.TextMain, TextAnchor.MiddleLeft);
            title.Add(titleText);
            var subtitle = MkLabel("CHARACTER STATUS", 12f, GitHubDark.TextSub, TextAnchor.MiddleLeft);
            subtitle.style.marginLeft = 12f;
            title.Add(subtitle);
            var close = new Button(Close) { name = "StatusCloseButton", text = "×" };
            close.style.position = Position.Absolute;
            close.style.right = 0f;
            close.style.top = 10f;
            close.style.width = 31.2f;
            close.style.height = 31.2f;
            StyleButton(close, UTKButton.Variant.Secondary);
            title.Add(close);
            _statusPanel.Add(title);

            var vitalsHeading = MkLabel("특수 상태 게이지   SPECIAL VITALS", 13f, GitHubDark.Gold, TextAnchor.MiddleLeft);
            vitalsHeading.name = "SpecialVitalsHeading";
            vitalsHeading.style.marginTop = 8f;
            var special = _statusContent.Q<VisualElement>(className: "special-gauge-root");
            if (_figHungerFill != null)
            {
                var parent = _figHungerFill.parent;
                while (parent != null && parent.parent != _statusContent) parent = parent.parent;
                if (parent != null) { special = parent.parent; }
            }
            if (special != null)
            {
                special.style.marginTop = 0f;
                special.style.position = Position.Absolute;
                special.style.left = 40.8f;
                special.style.top = 708.4f;
                special.style.width = 494.4f;
                special.style.height = 157f;
                if (special.childCount > 0) special[0].style.display = DisplayStyle.None;
                if (special.childCount > 1)
                {
                    VisualElement firstGauge = special[1];
                    firstGauge.style.position = Position.Absolute;
                    firstGauge.style.left = 0f;
                    firstGauge.style.top = 33.8f;
                    firstGauge.style.width = 494.4f;
                    firstGauge.style.height = 36.4f;
                }
                if (special.childCount > 2)
                {
                    VisualElement secondGauge = special[2];
                    secondGauge.style.position = Position.Absolute;
                    secondGauge.style.left = 0f;
                    secondGauge.style.top = 87f;
                    secondGauge.style.width = 494.4f;
                    secondGauge.style.height = 36.4f;
                }
                var specialHeading = new VisualElement { name = "SpecialVitalsHeader" };
                specialHeading.style.position = Position.Absolute;
                specialHeading.style.left = 0f;
                specialHeading.style.top = 17f;
                specialHeading.style.width = 494.4f;
                specialHeading.style.height = 17f;
                special.Add(specialHeading);
                specialHeading.Add(vitalsHeading);
            }

            _statusContent.style.paddingTop = 80f;
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
            var stable = MkLabel("SYS_EQ_STABLE", UTKTheme.FontBadge, GitHubDark.Health, TextAnchor.MiddleRight);   // [Figma] 12/500
            stable.style.unityFontStyleAndWeight = FontStyle.Normal;
            stable.style.flexGrow = 1f;
            header.Add(stable);
            content.Add(header);

            var equipped = new VisualElement { name = "EquippedSection" };
            equipped.style.marginTop = 24f;
            equipped.style.paddingLeft = 14.4f;
            equipped.style.paddingRight = 14.4f;
            equipped.style.paddingTop = 14.4f;
            equipped.style.paddingBottom = 14.4f;
            equipped.style.borderTopWidth = equipped.style.borderBottomWidth = 1f;
            equipped.style.borderLeftWidth = equipped.style.borderRightWidth = 1f;
            equipped.style.borderTopColor = equipped.style.borderBottomColor = new StyleColor(GitHubDark.Stroke);
            equipped.style.borderLeftColor = equipped.style.borderRightColor = new StyleColor(GitHubDark.Stroke);
            var equippedTitle = MkLabel("장착 중인 장비", UTKTheme.FontTitleLarge, GitHubDark.Accent, TextAnchor.MiddleLeft);   // [Figma] 19.2/700 #58A6FF
            equippedTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            equipped.Add(equippedTitle);
            content.Add(equipped);

            // [Figma 84:4] EquipGrid 427.2×172.8 — ItemSlot 81.6×81.6 ×10(5열×2행), gap 4.8(양축).
            var grid = new VisualElement { name = "EquipGrid" };
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.justifyContent = Justify.FlexStart;
            grid.style.width = 427.2f;
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
            pack.style.borderTopWidth = pack.style.borderBottomWidth = 1f;
            pack.style.borderLeftWidth = pack.style.borderRightWidth = 1f;
            pack.style.borderTopColor = pack.style.borderBottomColor = new StyleColor(GitHubDark.Stroke);
            pack.style.borderLeftColor = pack.style.borderRightColor = new StyleColor(GitHubDark.Stroke);
            var packHeader = new VisualElement { name = "TacticalPackHeader" };
            packHeader.style.height = 58.2f;
            packHeader.style.flexDirection = FlexDirection.Column;
            var packTitle = MkLabel("전술 배낭", UTKTheme.FontTitleLarge, GitHubDark.TextMain, TextAnchor.MiddleLeft);   // [Figma] 19.2/700
            packTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            packHeader.Add(packTitle);
            var packSub = MkLabel("TACTICAL PACK", UTKTheme.FontSubtitle, GitHubDark.TextSub, TextAnchor.MiddleLeft);   // [Figma] 13.2/500
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
        /// [Figma 84:4 ItemSlot 정합] 81.6×81.6 슬롯 — EQ배지(4.8,4.8) + TierStrip(81.6×3.6, 장착 시 희귀색,
        /// 빈 슬롯은 숨김 — 데이터 위조 금지) + 중앙 장착 아이템명 + 하단 슬롯명 라벨. 간격 4.8, 5열×2행.
        /// </summary>
        private VisualElement BuildFigmaEquipSlotBox(EquipmentManager.EquipmentSlot slot, string label, int orderIndex)
        {
            var cell = new VisualElement { name = "EquipSlot_" + slot };
            cell.AddToClassList("utk-slot");
            ApplyDarkSlotStyle(cell);
            cell.style.width = 81.6f;
            cell.style.height = 81.6f;
            cell.style.flexShrink = 0f;
            int column = orderIndex % 5;
            int row = orderIndex / 5;
            cell.style.marginRight = column == 4 ? 0f : 4.8f;
            cell.style.marginBottom = row == 1 ? 0f : 4.8f;

            var equip = MkLabel("EQ", 9.6f, GitHubDark.Accent, TextAnchor.UpperLeft);   // [Figma] 9.6/700
            equip.style.unityFontStyleAndWeight = FontStyle.Bold;
            equip.name = "EquipBadge_" + slot;
            equip.style.position = Position.Absolute;
            equip.style.left = 4.8f;
            equip.style.top = 4.8f;
            cell.Add(equip);

            var strip = new VisualElement { name = "EquipTierStrip_" + slot };
            strip.style.position = Position.Absolute;
            strip.style.left = 0f;
            strip.style.top = 0f;
            strip.style.width = 81.6f;
            strip.style.height = 3.6f;
            strip.style.display = DisplayStyle.None;
            _equipTierStrips[(int)slot] = strip;
            cell.Add(strip);

            // 중앙 — 장착 아이템명(실존 데이터). [Figma] 13.2/500 규격 계열
            var center = MkLabel("—", UTKTheme.FontSubtitle, GitHubDark.TextMain, TextAnchor.MiddleCenter);
            center.name = "EquippedName_" + slot;
            center.style.position = Position.Absolute;
            center.style.left = 3.8f;
            center.style.top = 30f;
            center.style.width = 74f;
            center.style.whiteSpace = WhiteSpace.Normal;
            _equipSlotLabels[(int)slot] = center;
            cell.Add(center);

            // 하단 슬롯명 — Figma '투구' 9.6/500 @(하단 중앙)
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

        private VisualElement BuildInventoryCell(int index)
        {
            var cell = new VisualElement { name = "InventoryCell_" + index };
            cell.AddToClassList("utk-slot");
            ApplyDarkSlotStyle(cell);
            cell.style.width = 81.6f;
            cell.style.height = 81.6f;
            cell.style.flexShrink = 0f;
            cell.style.marginLeft = 0f;
            cell.style.marginTop = 0f;
            int column = index % 5;
            int row = index / 5;
            cell.style.marginRight = column == 4 ? 0f : 9.6f;
            cell.style.marginBottom = row == 4 ? 0f : 14.4f;
            cell.style.alignItems = Align.Center;
            cell.style.justifyContent = Justify.Center;
            cell.pickingMode = PickingMode.Ignore;
            var label = MkLabel("—", UTKTheme.FontSubtitle, GitHubDark.TextSub, TextAnchor.MiddleCenter);   // [Figma] 13.2/500
            label.name = "InventoryCellLabel_" + index;
            label.style.whiteSpace = WhiteSpace.Normal;
            cell.Add(label);
            _inventoryCellLabels[index] = label;
            return cell;
        }

        private void RefreshInventoryVisuals()
        {
            PlayerInventory inventory = PlayerInventory.Instance;
            PlayerInventory.ItemSlot[] slots = inventory != null ? inventory.GetAllSlots() : null;
            for (int i = 0; i < _inventoryCellLabels.Length; i++)
            {
                Label label = _inventoryCellLabels[i];
                if (label == null) continue;
                PlayerInventory.ItemSlot slot = slots != null && i < slots.Length ? slots[i] : null;
                label.text = slot != null && slot.item != null
                    ? (slot.item.displayName ?? slot.item.id ?? "?") + (slot.count > 1 ? " ×" + slot.count : "")
                    : "—";
                label.style.color = new StyleColor(slot != null && slot.item != null ? GitHubDark.TextMain : GitHubDark.TextSub);
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
            var slotName = MkLabel(label, 13, GitHubDark.TextSub, TextAnchor.MiddleCenter);
            slotBox.Add(slotName);
            wrap.Add(slotBox);

            _equipSlotLabels[(int)slot] = MkLabel("—", 13, GitHubDark.TextMain, TextAnchor.MiddleCenter);
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
            right.Add(MkLabel("상세 정보 · 스탯 배분", 14, GitHubDark.Gold, TextAnchor.MiddleLeft));
            _statusContent.Add(right);

            // ── 남은 포인트 ──
            _pendingLabel = MkLabel("남은 포인트: 0", 18, GitHubDark.TextSub, TextAnchor.MiddleRight);
            right.Add(_pendingLabel);

            // ── 주스탯 4행 (이름 / 할당치 / 효과 / [+] 버튼) ──
            for (int i = 0; i < 4; i++)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginTop = 6f;

                var name = MkLabel(_statKindNames[i], 19, GitHubDark.TextSub, TextAnchor.MiddleLeft);
                name.style.width = 64f;
                row.Add(name);

                _allocValueLabels[i] = MkLabel("5", 21, GitHubDark.TextMain, TextAnchor.MiddleLeft);
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

                var name = MkLabel(_rowNames[i], 16, GitHubDark.TextSub, TextAnchor.MiddleLeft);
                name.style.width = 110f;
                row.Add(name);

                _statValueLabels[i] = MkLabel("-", 16, GitHubDark.TextMain, TextAnchor.MiddleRight);
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
            _addictionLabel = MkLabel("중독: 0%", 14, GitHubDark.RankEpic, TextAnchor.MiddleLeft);
            _addictionLabel.style.marginTop = 6f;
            right.Add(_addictionLabel);

            // ── 칭호 [Phase O6 C-O6-03] — 해금 순환 장착 ──
            AddSep(right, 10f, 6f);
            var titleHeader = MkLabel("칭호", 16, GitHubDark.Gold, TextAnchor.MiddleLeft);
            titleHeader.style.marginTop = 4f;
            right.Add(titleHeader);

            _titleValueLabel = MkLabel(GetTitleDisplay(), 14, GitHubDark.TextMain, TextAnchor.MiddleLeft);
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

            var name = MkLabel(labelName, 16, GitHubDark.TextSub, TextAnchor.MiddleLeft);
            name.style.width = 110f;
            root.Add(name);

            parts.value = MkLabel("-", 15, GitHubDark.TextMain, TextAnchor.MiddleRight);
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

            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Row;
            root.style.alignItems = Align.Center;
            root.style.marginTop = 8f;
            parts.root = root;

            var name = MkLabel(labelName, 12, GitHubDark.TextSub, TextAnchor.MiddleLeft);
            name.style.width = 52f;
            root.Add(name);

            var track = new VisualElement();
            track.style.flexGrow = 1f;
            track.style.height = 8f;
            track.style.backgroundColor = new StyleColor(GitHubDark.BgBase);
            track.style.borderTopLeftRadius = 4f;
            track.style.borderTopRightRadius = 4f;
            track.style.borderBottomLeftRadius = 4f;
            track.style.borderBottomRightRadius = 4f;
            parts.fill = new VisualElement();
            parts.fill.style.height = new Length(100f, LengthUnit.Percent);
            parts.fill.style.width = new Length(0f, LengthUnit.Percent);
            parts.fill.style.backgroundColor = new StyleColor(fillColor);   // 허기=Accent, 중독도=RankEpic
            parts.fill.style.borderTopLeftRadius = 4f;
            parts.fill.style.borderTopRightRadius = 4f;
            parts.fill.style.borderBottomLeftRadius = 4f;
            parts.fill.style.borderBottomRightRadius = 4f;
            track.Add(parts.fill);
            root.Add(track);

            parts.value = MkLabel("-", 12, GitHubDark.TextMain, TextAnchor.MiddleRight);
            parts.value.style.width = 122f;
            parts.value.style.marginLeft = 8f;
            root.Add(parts.value);

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
            float sx = canvasRoot.resolvedStyle.width / FigmaCanvasLayout.CanvasWidth;
            float sy = canvasRoot.resolvedStyle.height / FigmaCanvasLayout.CanvasHeight;
            if (sx <= 0f || sy <= 0f) return;
            if (_statusOnly)
            {
                style.left = 0f;
                style.top = 0f;
                style.width = StatusOnlyFrameWidth * sx;
                style.height = StatusOnlyFrameHeight * sy;
                _content.style.width = StatusOnlyFrameWidth * sx;
                _content.style.height = StatusOnlyFrameHeight * sy;
                _statusPanel.style.left = StatusOnlyCanvasBounds.x * sx;
                _statusPanel.style.top = StatusOnlyCanvasBounds.y * sy;
                _statusPanel.style.width = StatusOnlyCanvasBounds.width * sx;
                _statusPanel.style.height = StatusOnlyCanvasBounds.height * sy;
                return;
            }
            style.left = CompositionCanvasBounds.x * sx;
            style.top = CompositionCanvasBounds.y * sy;
            style.width = CompositionCanvasBounds.width * sx;
            style.height = CompositionCanvasBounds.height * sy;
            _content.style.width = CompositionCanvasBounds.width * sx;
            _content.style.height = CompositionCanvasBounds.height * sy;
            _statusPanel.style.left = StatusPanelLocalBounds.x * sx;
            _statusPanel.style.top = StatusPanelLocalBounds.y * sy;
            _statusPanel.style.width = StatusPanelLocalBounds.width * sx;
            _statusPanel.style.height = StatusPanelLocalBounds.height * sy;
            _equipmentPanel.style.left = EquipmentPanelLocalBounds.x * sx;
            _equipmentPanel.style.top = EquipmentPanelLocalBounds.y * sy;
            _equipmentPanel.style.width = EquipmentPanelLocalBounds.width * sx;
            _equipmentPanel.style.height = EquipmentPanelLocalBounds.height * sy;
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

            // --- 장비 슬롯 아이템명 (EquipmentSlot enum 전체) ---
            var em = EquipmentManager.Instance;
            for (int i = 0; i < System.Enum.GetValues(typeof(EquipmentManager.EquipmentSlot)).Length; i++)
            {
                EquipmentManager.EquipmentSlot slot = (EquipmentManager.EquipmentSlot)i;
                Label label = _equipSlotLabels[(int)slot];
                if (label == null) continue;
                string itemId = null;
                if (em != null)
                {
                    var data = em.GetSlotData(slot);
                    if (data != null) itemId = data.itemId;
                }
                label.text = string.IsNullOrEmpty(itemId) ? "—" : EquipmentStatBonusApplier.DisplayName(itemId);
                var strip = _equipTierStrips[(int)slot];
                if (strip != null)
                {
                    var data = em != null ? em.GetSlotData(slot) : null;
                    var item = data != null ? data.itemData : null;
                    bool show = item != null && !string.IsNullOrEmpty(itemId);
                    strip.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                    if (show) strip.style.backgroundColor = new StyleColor(RarityColor(item.rarity));
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