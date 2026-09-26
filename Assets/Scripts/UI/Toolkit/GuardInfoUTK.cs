using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;   // GuardPlaceholder, GuardEquipmentSystem
using ProjectName.Core;      // PlayerInventory, PotionBuffData, PotionBuffEffect
using ProjectName.UI;        // ItemIconDatabase

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// 병사 정보 창 v2 — UI Toolkit 포팅 (Phase U5 Round 1-A).
    /// 원본: Assets/Scripts/UI/GuardInfoWindow.cs (IMGUI, 972줄) — 병사(Guard) 2분할 창만 이식.
    /// 원본은 절대 수정하지 않는다.
    ///
    /// [정보 계층 — 원본 2-Pane과 동일]
    ///  - 좌측 (외형 + 장착 장비): 이름/직합/국가 + 장비 6슬롯(무기/투구/갑옷/신발/장갑/방패).
    ///  - 우측 (능력치 + 물약 버프): HP바, 전투력, 공격/방어/최대체력/민첩(+기본/장비 분해), 물약 버프 표.
    ///
    /// [진입점]
    ///  원본 GuardInfoWindow.OpenForGuard(guard) → 본 정적 Open(GuardPlaceholder guard) 패리티.
    ///  원본의 우클릭/정보 버튼 경로가 OpenForGuard를 호출하므로, 후속 라운드에서 해당 호출부를
    ///  GuardInfoUTK.Open(guard)로 교체하면 동일 데이터가 그대로 전달된다.
    ///
    /// [갱신]
    ///  250ms 폴링 (UTKWindowBase.Show 훅에서 schedule.Execute().Every() 시작, Hide에서 Pause).
    ///  HP 등 실시간 값은 폴링으로 반영. 장비 변경/역할 변경 이벤트 구독은 폴링이 대체(단순화).
    ///
    /// [규약] foreach만 / 클래스 public(CS0050) / UnityEngine.Debug /
    ///        IStyle 4면 개별 속성(Top/Bottom/Left/Right) / static 진입점.
    ///
    /// [GitHub-dark 리스타일] 이 창 한정 인라인 오버라이드 — 공용 UTKColor(브론즈/우드 톤) 대신
    /// 로컬 GitHubDark 팔레트(배경 #0B0E14 / 패널 #161B22 / 보조 #21262D / 액센트 #58A6FF /
    /// 골드 #E3B341 / 텍스트 #F0F6FC / 보조텍스트 #8B949E / 스트로크 #2E343D) 사용.
    /// 기능 로직(장비 등록·데이터 갱신·수명주기)은 무수정. 공용 파일/타 창은 건드리지 않는다.
    /// </summary>
    public class GuardInfoUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static GuardInfoUTK _instance;
        public static GuardInfoUTK Instance => _instance;

        /// <summary>팩토리 — 멱등.</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new GuardInfoUTK();
        }

        /// <summary>병사 정보창 열기 (원본 OpenForGuard 패리티).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapBridge()
        {
            // [U8 배선] F키 병사 상호작용 브리지 구독 — Systems 이벤트 → UTK 정보창
            SoldierInteractBridge.OnSoldierInfoRequested += guard => Open(guard);
        }

        public static void Open(GuardPlaceholder guard)
        {
            if (guard == null) return;
            Ensure();
            _instance.OpenForGuard(guard);
        }

        // ===== 설정 =====
        private const float WinW = 780f;
        private const float WinH = 560f;
        private const long RefreshMs = 250L;
        private const float SlotSize = 56f;
        private const float LeftW = 300f;

        // =====================================================================
        //  [Figma GitHub-dark 리스타일] 병사 정보 창 한정 인라인 오버라이드 — 기능 무수정, 시각 전용.
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

        // ===== 장비 슬롯 정의 (원본 좌측 6슬롯 순서) =====
        private class GearDef
        {
            public string label;   // 무기/투구/...
            public System.Func<GuardPlaceholder, PlayerInventory.ItemData> get;
        }

        private static readonly GearDef[] GearDefs = new GearDef[]
        {
            new GearDef { label = "무기",  get = g => g.WeaponItem },
            new GearDef { label = "투구",  get = g => g.HelmetItem },
            new GearDef { label = "갑옷",  get = g => g.ArmorItem },
            new GearDef { label = "신발",  get = g => g.BootsItem },
            new GearDef { label = "장갑",  get = g => g.GlovesItem },
            new GearDef { label = "방패",  get = g => g.ShieldItem },
        };

        // ===== 갱신 대상 =====
        private GuardPlaceholder _currentGuard;
        private Label _nameLabel;
        private Label _levelNationLabel;
        private Label _addictionLabel;   // [P30-B] 중독도 라인 (적병사 중독 현황 표기, 0이면 '중독 없음')
        private readonly Label[] _gearNameLabels = new Label[GearDefs.Length];
        private readonly UTKSlot[] _gearSlotViews = new UTKSlot[GearDefs.Length];
        private Label _hpValueLabel;
        private VisualElement _hpFill;
        private Label _combatLabel;
        private readonly Label[] _statValueLabels = new Label[4];   // 공격/방어/최대체력/민첩
        private readonly Label[] _statNoteLabels = new Label[4];
        private Label _buffLabel;
        private Button _equipBtn;                // [P30-C] 🛠️ 장비 등록 토글 버튼 (UTKButton.Create 반환 — 아군 전용, 적병사 창에서는 숨김)
        private VisualElement _equipSection;     // [P30-C] 장비 등록 스크롤 섹션 (플레이어 인벤토리 장비 목록)
        private ScrollView _equipList;
        private Label _equipEmptyLabel;
        private bool _equipOpen;
        private IVisualElementScheduledItem _refreshTask;

        private GuardInfoUTK() : base("병사 정보", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Row;
            BuildLeftZone();
            BuildRightZone();
            ApplyGitHubDarkStyle();   // [GitHub-dark] 창 크롬(본체/타이틀바/닫기버튼) 리스타일 — 이 창 한정
            ApplyUIToolkitFont(this);
            style.display = DisplayStyle.None;
        }

        // =====================================================================
        //  콘텐츠 빌드
        // =====================================================================

        private void BuildLeftZone()
        {
            var left = new VisualElement();
            left.name = "LeftZone";
            left.style.width = LeftW;
            left.style.flexShrink = 0f;
            left.style.flexGrow = 0f;
            left.style.flexDirection = FlexDirection.Column;
            left.style.paddingRight = 14f;
            _content.Add(left);

            // ── 모습 + 이름/직합 ──
            var face = new VisualElement();
            face.name = "Face";
            face.AddToClassList("utk-slot");
            ApplyDarkSlotStyle(face);   // [GitHub-dark] 우드 배경 제거 → 다크 인셋 패널
            face.style.height = 120f;
            face.style.justifyContent = Justify.Center;
            var faceLabel = MkLabel("🧍 병사 외형", 16, GitHubDark.TextMain, TextAnchor.MiddleCenter);
            face.Add(faceLabel);
            left.Add(face);

            _nameLabel = MkLabel("", 20, GitHubDark.Accent, TextAnchor.MiddleLeft);
            _nameLabel.style.marginTop = 10f;
            left.Add(_nameLabel);

            _levelNationLabel = MkLabel("", 14, GitHubDark.TextSub, TextAnchor.MiddleLeft);
            _levelNationLabel.style.whiteSpace = WhiteSpace.Normal;
            left.Add(_levelNationLabel);

            // [P30-B] 중독도 라인 — 레벨 라인 아래 1줄 추가(화면 넘침 방지: 라벨 1개 추가분뿐)
            _addictionLabel = MkLabel("", 13, GitHubDark.RankEpic, TextAnchor.MiddleLeft);
            _addictionLabel.style.whiteSpace = WhiteSpace.Normal;
            left.Add(_addictionLabel);

            // ── 장착 장비 6슬롯 ──
            var gearHeader = MkLabel("📦 장착 장비", 16, GitHubDark.Gold, TextAnchor.MiddleLeft);
            gearHeader.style.marginTop = 14f;
            gearHeader.style.marginBottom = 6f;
            left.Add(gearHeader);

            var grid = new VisualElement();
            grid.name = "GearGrid";
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            left.Add(grid);

            for (int i = 0; i < GearDefs.Length; i++)
            {
                var wrap = new VisualElement();
                wrap.style.flexDirection = FlexDirection.Column;
                wrap.style.alignItems = Align.Center;
                wrap.style.marginRight = 8f;
                wrap.style.marginBottom = 8f;

                var slot = new UTKSlot();
                slot.name = "GearSlot_" + GearDefs[i].label;
                slot.style.width = SlotSize;
                slot.style.height = SlotSize;
                slot.style.flexShrink = 0f;
                ApplyDarkSlotStyle(slot);   // [GitHub-dark] 슬롯 다크 인셋 + r6
                _gearSlotViews[i] = slot;
                wrap.Add(slot);

                var nameLabel = new Label("—");
                nameLabel.name = "GearName_" + GearDefs[i].label;
                nameLabel.style.fontSize = 11f;
                nameLabel.style.color = new StyleColor(GitHubDark.TextSub);
                nameLabel.style.whiteSpace = WhiteSpace.Normal;
                nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                nameLabel.style.width = 62f;
                _gearNameLabels[i] = nameLabel;
                wrap.Add(nameLabel);

                grid.Add(wrap);
            }

            // [P30-C] 🛠️ 장비 등록 — 아군병사 전용(적병사 창에서는 숨김). 클릭 시 장비 등록 스크롤 섹션 토글.
            _equipBtn = UTKButton.Create("🛠️ 장비 등록", ToggleEquipSection, UTKButton.Variant.Secondary);
            StyleButton(_equipBtn, UTKButton.Variant.Secondary);   // [GitHub-dark] 버튼 인라인 오버라이드
            _equipBtn.style.height = 34f;
            _equipBtn.style.marginTop = 6f;
            _equipBtn.style.display = DisplayStyle.None;   // RefreshDisplay에서 아군일 때만 표시
            left.Add(_equipBtn);

            _equipSection = new VisualElement();
            _equipSection.name = "EquipRegisterSection";
            _equipSection.style.display = DisplayStyle.None;
            left.Add(_equipSection);

            var equipTitle = MkLabel("플레이어 인벤토리 장비 선택", 12, GitHubDark.TextSub, TextAnchor.MiddleLeft);
            equipTitle.style.marginTop = 4f;
            _equipSection.Add(equipTitle);

            _equipList = new ScrollView(ScrollViewMode.Vertical);
            _equipList.name = "EquipRegisterList";
            _equipList.style.height = 132f;
            _equipList.style.marginTop = 4f;
            _equipList.style.marginBottom = 4f;
            _equipSection.Add(_equipList);

            _equipEmptyLabel = MkLabel("장착 가능한 장비가 없습니다.", 12, GitHubDark.TextSub, TextAnchor.MiddleLeft);
            _equipSection.Add(_equipEmptyLabel);
        }

        /// <summary>[P30-C] 장비 등록 섹션 토글 — 열 때마다 플레이어 인벤토리 장비 목록 재구성(멱등).</summary>
        private void ToggleEquipSection()
        {
            var guard = _currentGuard;
            if (guard == null || !guard.IsAlly) return;   // 적병사 — 등록 금지(토글 무시)
            _equipOpen = !_equipOpen;
            _equipSection.style.display = _equipOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (_equipOpen) RebuildEquipmentList();
        }

        /// <summary>
        /// [P30-C] 플레이어 인벤토리의 장비(무기/갑옷 카테고리) 목록 재구성 — 각 행 = 아이템명 x수량 + [장착] 버튼.
        /// 투구/신발/장갑/방패는 Armor 카테고리 하위(id 접두사/표시명 분류 — EquipAllyItem 참조).
        /// </summary>
        private void RebuildEquipmentList()
        {
            _equipList.Clear();
            bool found = false;
            var inv = PlayerInventory.Instance;
            if (inv != null)
            {
                var slots = inv.GetAllSlots();
                foreach (var slot in slots)
                {
                    if (slot == null || slot.item == null || slot.count <= 0) continue;
                    var item = slot.item;
                    if (item.category != PlayerInventory.ItemCategory.Weapon
                        && item.category != PlayerInventory.ItemCategory.Armor) continue;
                    found = true;
                    _equipList.Add(BuildEquipRow(item, slot.count));
                }
            }
            _equipEmptyLabel.style.display = found ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private VisualElement BuildEquipRow(PlayerInventory.ItemData item, int count)
        {
            var row = new VisualElement();
            row.name = "EquipRow";
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.height = 34f;
            row.style.marginBottom = 2f;
            row.style.marginRight = 6f;
            row.style.backgroundColor = GitHubDark.PanelSub;   // [GitHub-dark] 행 보조패널
            row.style.paddingLeft = 6f;
            row.style.borderTopLeftRadius = 4f;
            row.style.borderTopRightRadius = 4f;
            row.style.borderBottomLeftRadius = 4f;
            row.style.borderBottomRightRadius = 4f;            // 작은 배지 r4

            var name = new Label($"{item.displayName} x{count}");
            name.style.fontSize = 13f;
            name.style.color = new StyleColor(GitHubDark.TextMain);
            name.style.whiteSpace = WhiteSpace.Normal;
            name.style.flexGrow = 1f;
            row.Add(name);

            var equipBtn = UTKButton.Create("장착", () => OnEquipClicked(item), UTKButton.Variant.Primary);
            StyleButton(equipBtn, UTKButton.Variant.Primary);   // [GitHub-dark] 버튼 인라인 오버라이드
            equipBtn.style.width = 56f;
            equipBtn.style.height = 26f;
            equipBtn.style.flexShrink = 0f;
            row.Add(equipBtn);
            return row;
        }

        /// <summary>[P30-C] [장착] 클릭 — 아군 병사 장비 직접 등록(EquipAllyItem) 후 표시/목록 갱신.</summary>
        private void OnEquipClicked(PlayerInventory.ItemData item)
        {
            var guard = _currentGuard;
            if (guard == null || !guard.IsAlly) return;
            guard.EquipAllyItem(item);
            RefreshDisplay();
            RebuildEquipmentList();   // 인벤토리 변화(제거/교체 장비 반환) 반영
        }

        private void BuildRightZone()
        {
            var right = new VisualElement();
            right.name = "RightZone";
            right.style.flexGrow = 1f;
            right.style.flexDirection = FlexDirection.Column;
            right.style.paddingLeft = 6f;
            _content.Add(right);

            // ── HP 바 ──
            var header = MkLabel("📊 능력치", 16, GitHubDark.Accent, TextAnchor.MiddleLeft);
            right.Add(header);

            var gauge = BuildGaugeRow("❤️ HP:");
            _hpValueLabel = gauge.value;
            _hpFill = gauge.fill;
            right.Add(gauge.root);
            right.Add(AddSep(8f, 2f));

            // ── 전투력 ──
            _combatLabel = MkLabel("⚡ 전투력:", 16, GitHubDark.TextMain, TextAnchor.MiddleLeft);
            _combatLabel.style.marginTop = 4f;
            right.Add(_combatLabel);
            right.Add(AddSep(10f, 2f));

            // ── 스탯 4행 (공격/방어/최대체력/민첩) ──
            string[] statNames = { "⚔️ 공격력:", "🛡️ 방어력:", "💚 최대체력:", "💨 민첩:" };
            for (int i = 0; i < 4; i++)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginTop = 4f;

                var name = MkLabel(statNames[i], 15, GitHubDark.TextSub, TextAnchor.MiddleLeft);
                name.style.width = 100f;
                row.Add(name);

                _statValueLabels[i] = MkLabel("-", 16, GitHubDark.TextMain, TextAnchor.MiddleRight);
                _statValueLabels[i].style.width = 90f;
                row.Add(_statValueLabels[i]);

                _statNoteLabels[i] = MkLabel("", 12, GitHubDark.TextSub, TextAnchor.MiddleLeft);
                _statNoteLabels[i].style.flexGrow = 1f;
                _statNoteLabels[i].style.whiteSpace = WhiteSpace.Normal;
                row.Add(_statNoteLabels[i]);

                right.Add(row);
            }

            right.Add(AddSep(10f, 2f));

            // ── 물약 버프 표 ──
            var buffHeader = MkLabel("💊 물약 버프", 16, GitHubDark.Gold, TextAnchor.MiddleLeft);
            buffHeader.style.marginTop = 6f;
            right.Add(buffHeader);

            _buffLabel = MkLabel("", 12, GitHubDark.TextMain, TextAnchor.UpperLeft);
            _buffLabel.style.whiteSpace = WhiteSpace.Normal;
            _buffLabel.style.flexGrow = 1f;
            right.Add(_buffLabel);
        }

        private static GaugeParts BuildGaugeRow(string labelName)
        {
            var parts = new GaugeParts();

            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Row;
            root.style.alignItems = Align.Center;
            root.style.marginTop = 6f;
            root.style.marginRight = 8f;
            parts.root = root;

            var name = MkLabel(labelName, 15, GitHubDark.TextSub, TextAnchor.MiddleLeft);
            name.style.width = 90f;
            root.Add(name);

            parts.value = MkLabel("-", 14, GitHubDark.TextMain, TextAnchor.MiddleRight);
            parts.value.style.width = 130f;
            root.Add(parts.value);

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
            parts.fill.style.backgroundColor = new StyleColor(GitHubDark.Health);   // [GitHub-dark] HP 건강색
            gaugeBg.Add(parts.fill);

            root.Add(gaugeBg);
            return parts;
        }

        private sealed class GaugeParts
        {
            public VisualElement root;
            public Label value;
            public VisualElement fill;
        }

        private static VisualElement AddSep(float marginTop, float marginBottom)
        {
            var sep = new VisualElement();
            sep.style.height = 1f;
            sep.style.backgroundColor = new StyleColor(GitHubDark.Stroke);
            sep.style.marginTop = marginTop;
            sep.style.marginBottom = marginBottom;
            return sep;
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
        //  생명주기
        // =====================================================================

        public void OpenForGuard(GuardPlaceholder guard)
        {
            if (guard == null) return;
            _currentGuard = guard;
            // [P30-C] 대상 교체 시 장비 등록 섹션 닫기(멱등 리셋)
            _equipOpen = false;
            if (_equipSection != null) _equipSection.style.display = DisplayStyle.None;
            Show();
        }

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            CenterOnParent();
            StartRefreshLoop();
            RefreshDisplay();
            Debug.Log("[GuardInfoUTK] 병사 정보창 열림");
        }

        public override void Hide()
        {
            base.Hide();
            StopRefreshLoop();
            _currentGuard = null;
            Debug.Log("[GuardInfoUTK] 병사 정보창 닫힘");
        }

        // =====================================================================
        //  폴링 (250ms)
        // =====================================================================

        private void StartRefreshLoop()
        {
            if (_refreshTask != null) return;
            _refreshTask = schedule.Execute(() =>
            {
                if (IsOpen) RefreshDisplay();
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

        // =====================================================================
        //  데이터 갱신 — 원본 데이터 경로 그대로
        // =====================================================================

        private void RefreshDisplay()
        {
            var guard = _currentGuard;
            if (guard == null)
            {
                if (IsOpen) Close();
                return;
            }

            // --- 좌측: 이름/직합/국가/레벨 ---
            string gradeStr = GetGuardGradeString(guard);
            string typeStr = GetGuardTypeLabel(guard);
            _nameLabel.text = $"⚔️ {guard.GuardName} {gradeStr} | {typeStr}";
            string nationDisplay = string.IsNullOrEmpty(guard.Nation) ? "" : $" ({guard.Nation})";
            // [P30-B] 호감도 — 수치 + 4단계 등급(호감/보통/경계/위험) 함께 표기
            _levelNationLabel.text = $"Lv.{guard.Level} | {guard.JobTitle}{nationDisplay} | ⚖️ {GuardLoyaltySystem.GetAffinityGradeName(guard.Loyalty)} {guard.Loyalty:F0}/100";

            // [P30-B] 중독도 표기 — 0이면 '💊 중독 없음', 그 외 단계명(정상/가벼운 의존/중독/...) 포함
            float addiction = guard.Addiction;
            _addictionLabel.text = addiction > 0f
                ? $"💊 중독 {addiction:F0}/100 · {GuardAddictionSystem.GetStageName(GuardAddictionSystem.GetAddictionStage(addiction))}"
                : "💊 중독 없음";

            // [P30-C] 🛠️ 장비 등록 버튼 — 아군 전용 표시(적병사 창에서는 숨김 + 섹션 강제 닫기)
            bool ally = guard.IsAlly;
            _equipBtn.style.display = ally ? DisplayStyle.Flex : DisplayStyle.None;
            if (!ally && _equipOpen)
            {
                _equipOpen = false;
                _equipSection.style.display = DisplayStyle.None;
            }

            // --- 좌측: 장비 6슬롯 ---
            for (int i = 0; i < GearDefs.Length; i++)
            {
                var item = GearDefs[i].get(guard);
                if (item != null && !string.IsNullOrEmpty(item.id))
                {
                    _gearSlotViews[i].SetIcon(ItemIconDatabase.GetOrCreateIcon(item));
                    _gearSlotViews[i].SetRank(UTKRarity.ClassForIndex((int)item.rarity));
                    _gearNameLabels[i].text = item.displayName;
                }
                else
                {
                    _gearSlotViews[i].SetIcon(null);
                    _gearSlotViews[i].SetRank("common");
                    _gearNameLabels[i].text = "—";
                }
            }

            // --- 우측: HP 바 ---
            float maxHp = guard.GetMaxHP();
            float hpRatio = maxHp > 0f ? Mathf.Clamp01(guard.HP / maxHp) : 0f;
            _hpValueLabel.text = $"{(int)guard.HP}/{(int)maxHp}";
            SetGaugeFill(_hpFill, hpRatio);

            // --- 우측: 전투력 ---
            float combatPower = 0f;
            if (GuardEquipmentSystem.Instance != null)
                combatPower = GuardEquipmentSystem.Instance.CalculateGuardCombatPower(guard);
            _combatLabel.text = $"⚡ 전투력: {combatPower:F0} (장비 반영)";

            // --- 우측: 스탯 + 기본/장비 분해 ---
            int rawAtk = guard.GetStatAttack();
            int totalAtk = guard.GetAttack();
            int gearAtk = totalAtk - rawAtk;
            int rawDef = guard.GetStatDefense();
            int totalDef = guard.GetDefense();
            int gearDef = totalDef - rawDef;

            _statValueLabels[0].text = totalAtk.ToString();
            _statNoteLabels[0].text = gearAtk > 0 ? $"기본 {rawAtk} + 무기 {gearAtk}" : $"기본 {rawAtk}";

            _statValueLabels[1].text = totalDef.ToString();
            _statNoteLabels[1].text = gearDef > 0 ? $"기본 {rawDef} + 장비 {gearDef}" : $"기본 {rawDef}";

            _statValueLabels[2].text = maxHp.ToString("F0");
            _statNoteLabels[2].text = $"기본 {guard.MaxHP:F0} + 체력 {guard.GetStatVitality() * 2}";

            _statValueLabels[3].text = guard.GetAgility().ToString();
            _statNoteLabels[3].text = $"기본 {guard.GetStatAgility()}";

            // --- 우측: 물약 버프 표 ---
            var buffLines = new System.Collections.Generic.List<string>(PotionBuffData.AllEffects.Count);
            foreach (var kv in PotionBuffData.AllEffects)
                buffLines.Add(DescribePotionBuff(kv.Key, kv.Value));
            _buffLabel.text = buffLines.Count > 0 ? string.Join("\n", buffLines) : "(물약 버프 없음)";
        }

        private static string DescribePotionBuff(string potionId, PotionBuffEffect effect)
        {
            if (effect.healFlat > 0f)
                return $"{potionId}: 체력 즉시 +{effect.healFlat:0}";
            if (effect.healPercent > 0f)
                return $"{potionId}: 체력 +{effect.healPercent * 100f:0}%";
            var parts = new System.Collections.Generic.List<string>(3);
            if (effect.attackBuff > 0f)  parts.Add($"공격+{effect.attackBuff:0}");
            if (effect.defenseBuff > 0f) parts.Add($"방어+{effect.defenseBuff:0}");
            if (effect.agilityBuff > 0f) parts.Add($"민첩+{effect.agilityBuff:0}");
            if (parts.Count == 0) return $"{potionId}: (효과 없음)";
            return $"{potionId}: {string.Join(" / ", parts.ToArray())} ({effect.buffSeconds:0}초)";
        }

        // ===== 헬퍼 =====

        private static void SetGaugeFill(VisualElement fill, float ratio)
        {
            if (fill == null) return;
            fill.style.width = new Length(Mathf.Clamp01(ratio) * 100f, LengthUnit.Percent);
        }

        private static string GetGuardTypeLabel(GuardPlaceholder guard)
        {
            if (guard == null) return "병사";
            string job = guard.JobTitle;
            if (string.IsNullOrEmpty(job) || job == "병사") return "병사";
            return job;
        }

        private static string GetGuardGradeString(GuardPlaceholder guard)
        {
            int lv = guard.Level;
            if (lv >= 35) return "★★★★";
            if (lv >= 20) return "★★★";
            if (lv >= 10) return "★★";
            return "★";
        }

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