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
        public static StatusWindowUTK Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            _instance = new StatusWindowUTK();
            var go = new GameObject("StatusWindowUTK");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Updater>().window = _instance;
            Debug.Log("[StatusWindowUTK] 초기화 완료 — P키로 토글");
        }

        // ===== 설정 =====
        private const float WinW = 1180f;
        private const float WinH = 700f;

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
        private static readonly EquipmentManager.EquipmentSlot[] _equipOrder =
        {
            EquipmentManager.EquipmentSlot.Helmet, EquipmentManager.EquipmentSlot.Armor,
            EquipmentManager.EquipmentSlot.Weapon, EquipmentManager.EquipmentSlot.Gloves,
            EquipmentManager.EquipmentSlot.Shoes,  EquipmentManager.EquipmentSlot.Back
        };
        private static readonly string[] _equipNames = { "투구", "상의", "무기", "장갑", "신발", "등" };

        // ===== 갱신 대상 레퍼런스 =====
        private Label _pendingLabel;
        private readonly Label[] _allocValueLabels = new Label[4];
        private readonly Button[] _allocButtons = new Button[4];
        private readonly Label[] _statValueLabels = new Label[8];
        private readonly Label[] _bonusNoteLabels = new Label[8];
        private readonly Label[] _equipSlotLabels = new Label[8];   // index = EquipmentSlot enum
        private Label _bonusListLabel;
        private Label _addictionLabel;
        private Label _titleValueLabel;   // [O6] 칭호 표시
        private bool _titleSubscribed;   // [O6] 칭호 이벤트 구독 플래그
        private Label _expValueLabel, _hpValueLabel;
        private VisualElement _expFill, _hpFill;
        private Label _hungerValueLabel;  // [O10] 허기 게이지 표시
        private VisualElement _hungerFill;
        private bool _hungerSubscribed;  // [O10] 허기 정적 이벤트 구독 플래그

        private PlayerStats _subscribedStats;
        private EquipmentManager _subscribedEquip;
        private float _refreshTimer;

        private StatusWindowUTK() : base("상태", new Vector2(WinW, WinH))
        {
            BuildContent();
            ApplyGitHubDarkStyle();   // [GitHub-dark] 창 크롬(본체/타이틀바/닫기버튼) 리스타일 — 이 창 한정
            ApplyUIToolkitFont(this);
            // 원본과 동일: 생성 시엔 숨김, Toggle로만 표시
            style.display = DisplayStyle.None;
        }

        // =====================================================================
        //  콘텐츠 빌드
        // =====================================================================

        private void BuildContent()
        {
            _content.style.flexDirection = FlexDirection.Row;
            _content.style.flexGrow = 1f;

            BuildLeftZone();
            BuildRightZone();
        }

        private void BuildLeftZone()
        {
            var left = new VisualElement();
            left.name = "LeftZone";
            left.style.width = 330f;
            left.style.flexShrink = 0;
            left.style.flexDirection = FlexDirection.Column;
            left.style.paddingRight = 14f;
            _content.Add(left);

            // ── 뷰포트 Placeholder (원본 3D 프리뷰 자리 — 시각 요소이므로 자리만 유지) ──
            var viewport = new VisualElement();
            viewport.name = "Viewport";
            viewport.AddToClassList("utk-slot");
            ApplyDarkSlotStyle(viewport);   // [GitHub-dark] 우드 배경 제거 → 다크 인셋 패널
            viewport.style.height = 190f;
            var vpLabel = MkLabel("3D 미리보기", 15, GitHubDark.TextSub, TextAnchor.MiddleCenter);
            viewport.Add(vpLabel);
            left.Add(viewport);

            // ── 장비슬롯 6개 (좌3열 + 우3열 그리드) ──
            var grid = new VisualElement();
            grid.name = "EquipGrid";
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.marginTop = 16f;
            left.Add(grid);

            var colA = new VisualElement();
            colA.style.flexDirection = FlexDirection.Column;
            colA.style.flexGrow = 1f;
            var colB = new VisualElement();
            colB.style.flexDirection = FlexDirection.Column;
            colB.style.flexGrow = 1f;
            grid.Add(colA);
            grid.Add(colB);

            for (int r = 0; r < 3; r++)
                colA.Add(BuildEquipSlotBox(_equipOrder[r], _equipNames[r]));
            for (int r = 0; r < 3; r++)
                colB.Add(BuildEquipSlotBox(_equipOrder[3 + r], _equipNames[3 + r]));

            // ── 장비 보너스 내역 ──
            var bonusHeader = MkLabel("장비 보너스", 16, GitHubDark.Gold, TextAnchor.MiddleLeft);
            bonusHeader.style.marginTop = 20f;
            left.Add(bonusHeader);

            _bonusListLabel = MkLabel("착용 장비 보너스 없음", 13, GitHubDark.TextSub, TextAnchor.UpperLeft);
            _bonusListLabel.style.whiteSpace = WhiteSpace.Normal;
            _bonusListLabel.style.flexGrow = 1f;
            left.Add(_bonusListLabel);
        }

        /// <summary>장비슬롯 1개 (박스 + 슬롯명 + 아이템명). slot = EquipmentSlot enum.</summary>
        private VisualElement BuildEquipSlotBox(EquipmentManager.EquipmentSlot slot, string label)
        {
            var wrap = new VisualElement();
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
            right.name = "RightZone";
            right.style.flexGrow = 1f;
            right.style.flexDirection = FlexDirection.Column;
            _content.Add(right);

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
            StyleButton(titleBtn, UTKButton.Variant.Secondary);   // [GitHub-dark] 버튼 인라인 오버라이드
            titleBtn.style.marginTop = 6f;
            titleBtn.style.height = 26f;
            right.Add(titleBtn);

            // ── [P5] 경제 감사 리포트 버튼 — 골드 원장 즉석 확인 ──
            var auditBtn = UTKButton.Create("감사 리포트", ShowAuditReport, UTKButton.Variant.Secondary);
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
            CenterOnParent();
            EnsureSubscriptions();
            RefreshDisplay();
            Debug.Log("[StatusWindowUTK] 캐릭터 정보 창 열림 (키: P)");
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
            ShowLevelUpPopup(newLevel);
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

            // --- 허기 [O10] — PlayerStats와 무관하게 항상 갱신 (읽기 전용) ---
            var hungerSys = HungerSystem.Instance;
            UpdateHungerGauge(hungerSys != null ? hungerSys.Hunger : HungerSystem.MaxHunger);

            PlayerStats stats = PlayerStats.Instance;
            if (stats == null)
            {
                if (_expValueLabel != null) _expValueLabel.text = "-";
                if (_hpValueLabel != null) _hpValueLabel.text = "-";
                return;
            }

            // --- 경험치 ---
            int level = stats.Level;
            if (level >= PlayerStats.MaxLevel)
            {
                if (_expValueLabel != null) _expValueLabel.text = "MAX";
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
            }

            // --- 체력 ---
            PlayerHealth health = PlayerHealth.Instance;
            float maxHP = health != null ? health.MaxHP : stats.HPBase;
            float curHP = health != null ? health.CurrentHP : maxHP;
            if (_hpValueLabel != null) _hpValueLabel.text = $"{curHP:F0} / {stats.HPBase:F0}";
            float hpRatio = stats.HPBase > 0 ? Mathf.Clamp01(curHP / stats.HPBase) : 0f;
            SetGaugeFill(_hpFill, hpRatio);

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

            // --- 장비슬롯 아이템명 (6슬롯: Helmet..Back) ---
            var em = EquipmentManager.Instance;
            for (int i = 0; i < _equipOrder.Length; i++)
            {
                EquipmentManager.EquipmentSlot slot = _equipOrder[i];
                Label label = _equipSlotLabels[(int)slot];
                if (label == null) continue;
                string itemId = null;
                if (em != null)
                {
                    var data = em.GetSlotData(slot);
                    if (data != null) itemId = data.itemId;
                }
                label.text = string.IsNullOrEmpty(itemId) ? "—" : EquipmentStatBonusApplier.DisplayName(itemId);
            }

            // --- 장비 보너스 내역 ---
            if (_bonusListLabel != null)
            {
                var labels = EquipmentStatBonusApplier.GetActiveBonusLabels();
                _bonusListLabel.text = labels.Count > 0 ? string.Join("\n", labels) : "착용 장비 보너스 없음";
            }

            // --- 중독 (정보 섹션) ---
            if (_addictionLabel != null)
                _addictionLabel.text = $"중독: {DrugEffectSystem.DrugAddictionLevel:F0}% ({DrugEffectSystem.GetAddictionLabel()})";
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

            private void Update()
            {
                // 부트스트랩 완료 전이어도 최초 부착을 멱등으로 시도
                var root = UIToolkitBootstrap.UIRoot;
                if (root != null && window != null && window.parent == null)
                    root.Add(window);

                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null)
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