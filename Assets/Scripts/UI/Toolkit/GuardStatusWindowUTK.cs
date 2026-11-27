using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// [72차 Phase A] 병사 상태보기 창 — 피그마 2종 패널 수용:
    ///   · 84:4 equipment-inventory-panel(아군) = CharacterStatusPanel(576×936) + EquipmentPanel(531.6×936) 결합
    ///   · 83:4 character-status-panel(적병사) = CharacterStatusPanel 단독
    /// 계획서: docs/2026-10-15_72TH_SOLDIER_STATUS_PANELS_PLAN.md — Figma 진실치(2026-10-15 실측) 전부 하드코딩.
    ///
    /// [3D Portrait] 대상 병사를 씬에서 그대로 촬영(RT 512×288, 사선 카메라) — 장착 장비/애니 자세까지 실측 반영.
    ///   캡처는 개방 시 + 1초 폴링 재촬영(전투 중 자세 변화 반영). 대상 파괴 시 창 닫힘.
    /// [위조 금지] 데이터는 GuardPlaceholder 실측 API만 — 없는 값은 "-"(빈 장비 슬롯/EXP 등).
    /// [단일소스] 장비 카드 구조는 68차 인벤과 동일 규약(TierStrip 상단 가로/EQ 배지/아이콘 38.4).
    /// </summary>
    public class GuardStatusWindowUTK : UTKWindowBase
    {
        private static GuardStatusWindowUTK _instance;
        public static GuardStatusWindowUTK Instance => _instance;

        /// <summary>멱등 부트 — InteractionPanelUTK 상태보기 경유 개방.</summary>
        public static void Open(GuardPlaceholder guard)
        {
            if (guard == null) return;
            if (_instance == null)
            {
                _instance = new GuardStatusWindowUTK();
                var root = UIToolkitBootstrap.UIRoot;
                if (root == null)
                {
                    UIToolkitBootstrap.Ensure();
                    root = UIToolkitBootstrap.UIRoot;
                }
                if (root != null) root.Add(_instance);
            }
            _instance.OpenForGuard(guard);
        }

        // ===== [피그마 진실치 — 84:4/83:4 실측] =====
        private const float PanelW = 576f;            // CharacterStatusPanel
        private const float PanelH = 936f;
        private const float EquipPanelW = 531.6f;     // EquipmentPanel(아군 결합)
        private const float PanelGap = 28.8f;         // Figma 패널간 갭
        private const float BodyInset = 24f;          // PanelHeader/섹션 좌우 여백

        // 색상 — Figma 실측
        private static readonly Color GlassBg   = new Color(0.086f, 0.106f, 0.133f, 0.7f);   // #161B22/70
        private static readonly Color SectBg    = new Color(1f, 1f, 1f, 0.02f);              // #FFFFFF/2
        private static readonly Color SectBg2   = new Color(1f, 1f, 1f, 0.012f);             // #FFFFFF/1.2
        private static readonly Color TextMain  = UTKTheme.TextMain;                         // #F0F6FC
        private static readonly Color TextSub   = UTKTheme.TextSub;                          // #8B949E
        private static readonly Color Accent    = UTKTheme.Accent;                           // #58A6FF
        private static readonly Color Epic      = new Color(0.737f, 0.549f, 1f);             // #BC8CFF
        private static readonly Color GoldY     = new Color(0.949f, 0.788f, 0.298f);         // #F2C94C
        private static readonly Color GreenY    = new Color(0.337f, 0.890f, 0.624f);         // #56E39F
        private static readonly Color HpRed     = new Color(1f, 0.482f, 0.447f);             // #FF7B72
        private static readonly Color Faint     = new Color(0.282f, 0.310f, 0.345f);         // #484F58
        private static readonly Color BarBg     = new Color(1f, 1f, 1f, 0.06f);              // #FFFFFF/6
        private static readonly Color PortraitBg = new Color(0.043f, 0.055f, 0.078f);        // #0B0E14

        // ===== 갱신 대상 =====
        private GuardPlaceholder _guard;
        private Label _rarityText, _classText, _nameText, _hpOverlay;
        private Label _levelValue, _expValue;
        private readonly Label[] _statValues = new Label[3];
        private Label _affLabel, _affValue, _affNote, _toxLabel, _toxValue, _toxNote;
        private VisualElement _affFill, _toxFill;
        private VisualElement _portraitHost;
        private readonly UTKSlot[] _gearSlots = new UTKSlot[10];
        private readonly VisualElement[] _gearStrips = new VisualElement[10];
        private readonly Label[] _gearCountLabels = new Label[10];

        // ===== 3D Portrait 촬영 =====
        private RenderTexture _rt;
        private Camera _capCam;

        private IVisualElementScheduledItem _refreshTask;
        private float _pollTimer;

        private GuardStatusWindowUTK() : base("상태 정보", new Vector2(PanelW, PanelH), UTKWindowChrome.Frameless)
        {
            BuildPanel();
            style.display = DisplayStyle.None;
        }

        // =====================================================================
        //  개방 / 닫힘
        // =====================================================================

        private void OpenForGuard(GuardPlaceholder guard)
        {
            if (guard == null) return;
            _guard = guard;

            bool ally = guard.IsAlly;
            // Combined(아군) = 상태정보+장비 결합 폭 / StatusOnly(적) = 상태정보 단독
            float totalW = ally ? PanelW + PanelGap + EquipPanelW : PanelW;
            style.width = totalW;
            style.height = PanelH;
            _content.style.width = totalW;
            _content.style.height = PanelH;
            var equipPanel = this.Q<VisualElement>("EquipmentPanel");
            if (equipPanel != null)
                equipPanel.style.display = ally ? DisplayStyle.Flex : DisplayStyle.None;

            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);

            // 화면 중앙 배치 — 상태보기 전용 창(3분할 레이아웃 미사용)
            float rootW = root != null ? root.resolvedStyle.width : Screen.width;
            float rootH = root != null ? root.resolvedStyle.height : Screen.height;
            style.position = Position.Absolute;
            style.left = Mathf.Max(0f, (rootW - totalW) * 0.5f);
            style.top = Mathf.Max(0f, (rootH - PanelH) * 0.5f - 24f);

            StartCapture();
            RefreshData();
            StartRefreshLoop();
            Show();
            Debug.Log($"[GuardStatusUTK] 상태보기 개방 — {guard.GuardName} ({(ally ? "아군+장비" : "적병사")})");
        }

        public override void Hide()
        {
            StopRefreshLoop();
            StopCapture();
            base.Hide();
        }

        // =====================================================================
        //  본문 빌드 (피그마 진실치)
        // =====================================================================

        private void BuildPanel()
        {
            style.backgroundColor = new StyleColor(GlassBg);
            style.borderTopLeftRadius = 14.4f; style.borderTopRightRadius = 14.4f;
            style.borderBottomLeftRadius = 14.4f; style.borderBottomRightRadius = 14.4f;
            _content.style.position = Position.Absolute;
            _content.style.left = 0f; _content.style.top = 0f;

            BuildCornerDecals();
            BuildHeader();
            BuildIdentity();
            BuildPortrait();
            BuildStats();
            BuildSpecialStats();
            BuildEquipmentPanel();
            ApplyUIToolkitFont(this);
        }

        private void BuildCornerDecals()
        {
            void L(float l, float t, float lw, float lh)
            {
                var e = new VisualElement();
                e.style.position = Position.Absolute;
                e.style.left = l; e.style.top = t; e.style.width = lw; e.style.height = lh;
                e.style.backgroundColor = new StyleColor(UTKTheme.Stroke);
                e.pickingMode = PickingMode.Ignore;
                _content.Add(e);
            }
            L(0, 0, 12f, 1f); L(0, 0, 1f, 12f);
            L(PanelW - 12f, 0, 12f, 1f); L(PanelW - 1f, 0, 1f, 12f);
            L(0, PanelH - 1f, 12f, 1f); L(0, PanelH - 12f, 1f, 12f);
            // TR/BL 데칼은 EquipmentPanel 쪽(Custom 결합 폭) — 개방 시 폭 확정 후 배치(개방부 처리).
        }

        private void BuildHeader()
        {
            var header = new VisualElement { name = "PanelHeader" };
            header.style.position = Position.Absolute;
            header.style.left = BodyInset; header.style.top = 24f;
            header.style.width = PanelW - BodyInset * 2f; header.style.height = 52.8f;
            var title = new Label("상태 정보") { name = "PanelTitle" };
            title.style.position = Position.Absolute; title.style.left = 0f; title.style.top = 5.2f;
            UTKTheme.StyleFigmaText(title, 24f, 700, TextMain);
            header.Add(title);
            var subtitle = new Label("CHARACTER STATUS") { name = "PanelSubtitle" };
            subtitle.style.position = Position.Absolute; subtitle.style.left = 107f; subtitle.style.top = 14.2f;
            UTKTheme.StyleFigmaText(subtitle, 13.2f, 500, TextSub);
            header.Add(subtitle);
            var close = new Button(() => Close()) { text = "✕", name = "StatusCloseBtn" };
            close.style.position = Position.Absolute;
            close.style.right = 0f; close.style.top = 3.6f;
            close.style.width = 31.2f; close.style.height = 31.2f;
            close.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.06f));
            close.style.borderBottomLeftRadius = 7.2f; close.style.borderBottomRightRadius = 7.2f;
            close.style.borderTopLeftRadius = 7.2f; close.style.borderTopRightRadius = 7.2f;
            close.style.borderTopWidth = close.style.borderBottomWidth = close.style.borderLeftWidth = close.style.borderRightWidth = 0f;
            close.style.color = new StyleColor(TextSub);
            close.style.fontSize = 14f;
            header.Add(close);
            _content.Add(header);
        }

        private void BuildIdentity()
        {
            var sect = new VisualElement { name = "CharacterIdentitySection" };
            sect.style.position = Position.Absolute;
            sect.style.left = BodyInset; sect.style.top = 96f;
            sect.style.width = PanelW - BodyInset * 2f; sect.style.height = 84f;
            sect.style.backgroundColor = new StyleColor(SectBg);
            Round(sect, 9.6f);
            _rarityText = new Label("") { name = "RarityText" };
            _rarityText.style.position = Position.Absolute; _rarityText.style.left = 14.4f; _rarityText.style.top = 14.4f;
            UTKTheme.StyleFigmaText(_rarityText, 15.6f, 700, Epic);
            sect.Add(_rarityText);
            _classText = new Label("") { name = "ClassType" };
            _classText.style.position = Position.Absolute; _classText.style.right = 14.4f; _classText.style.top = 14.4f;
            UTKTheme.StyleFigmaText(_classText, 15.6f, 400, TextSub, TextAnchor.MiddleRight);
            sect.Add(_classText);
            _nameText = new Label("") { name = "CharacterName" };
            _nameText.style.position = Position.Absolute; _nameText.style.left = 14.4f; _nameText.style.top = 38.6f;
            _nameText.style.width = 499.2f - 28.8f; _nameText.style.height = 31f;
            UTKTheme.StyleFigmaText(_nameText, 26.4f, 700, TextMain);
            sect.Add(_nameText);
            _content.Add(sect);
        }

        private void BuildPortrait()
        {
            var sect = new VisualElement { name = "PortraitSection" };
            sect.style.position = Position.Absolute;
            sect.style.left = BodyInset; sect.style.top = 199.2f;
            sect.style.width = PanelW - BodyInset * 2f; sect.style.height = 312f;
            sect.style.backgroundColor = new StyleColor(PortraitBg);
            Round(sect, 9.6f);
            // 오버레이 — SYS_SCAN_ACTIVE(좌상) / HP %(우상)
            var scan = new Label("SYS_SCAN_ACTIVE") { name = "PortraitScan" };
            scan.style.position = Position.Absolute; scan.style.left = 14.4f; scan.style.top = 14.4f;
            UTKTheme.StyleFigmaText(scan, 12f, 400, Accent);
            sect.Add(scan);
            _hpOverlay = new Label("") { name = "PortraitHP" };
            _hpOverlay.style.position = Position.Absolute; _hpOverlay.style.right = 14.4f; _hpOverlay.style.top = 14.4f;
            UTKTheme.StyleFigmaText(_hpOverlay, 12f, 400, Accent, TextAnchor.MiddleRight);
            sect.Add(_hpOverlay);
            _portraitHost = new VisualElement { name = "CharacterPortraitHost" };
            _portraitHost.style.position = Position.Absolute;
            _portraitHost.style.left = 0f; _portraitHost.style.top = 0f;
            _portraitHost.style.width = sect.style.width.value.value;
            _portraitHost.style.height = 312f;
            _portraitHost.pickingMode = PickingMode.Ignore;
            sect.Add(_portraitHost);
            _content.Add(sect);
        }

        private void BuildStats()
        {
            var sect = new VisualElement { name = "StatsSection" };
            sect.style.position = Position.Absolute;
            sect.style.left = BodyInset; sect.style.top = 530.4f;
            sect.style.width = PanelW - BodyInset * 2f; sect.style.height = 174.4f;
            sect.style.backgroundColor = new StyleColor(SectBg2);
            Round(sect, 9.6f);
            // LevelBlock
            var lb = new VisualElement { name = "LevelBlock" };
            lb.style.position = Position.Absolute; lb.style.left = 14.4f; lb.style.top = 14.4f;
            lb.style.width = 120f; lb.style.height = 145.6f;
            lb.style.backgroundColor = new StyleColor(SectBg);
            Round(lb, 7.2f);
            var lvLabel = new Label("LEVEL");
            lvLabel.style.position = Position.Absolute; lvLabel.style.left = 41f; lvLabel.style.top = 27.5f;
            UTKTheme.StyleFigmaText(lvLabel, 13.2f, 500, TextSub);
            lb.Add(lvLabel);
            _levelValue = new Label("-");
            _levelValue.style.position = Position.Absolute; _levelValue.style.left = 0f; _levelValue.style.top = 47.3f;
            _levelValue.style.width = 120f; _levelValue.style.height = 50f;
            UTKTheme.StyleFigmaText(_levelValue, 38.4f, 700, Accent, TextAnchor.MiddleCenter);
            lb.Add(_levelValue);
            _expValue = new Label("") { name = "ExpLabel" };
            _expValue.style.position = Position.Absolute; _expValue.style.left = 0f; _expValue.style.top = 102.1f;
            _expValue.style.width = 120f; _expValue.style.height = 16f;
            UTKTheme.StyleFigmaText(_expValue, 12f, 400, GreenY, TextAnchor.MiddleCenter);
            lb.Add(_expValue);
            sect.Add(lb);
            // CoreStatsGrid — StatRow 3(공격/방어/최대체력) + 구분선
            float gx = 153.6f, gy = 14.4f, rowH = 41.2f;
            string[] labels = { "공격력 (ATK)", "방어력 (DEF)", "최대 체력 (HP)" };
            Color[] colors = { Accent, TextMain, HpRed };
            for (int i = 0; i < 3; i++)
            {
                float ry = gy + i * rowH;
                var rowLabel = new Label(labels[i]);
                rowLabel.style.position = Position.Absolute;
                rowLabel.style.left = gx; rowLabel.style.top = ry + 2f; rowLabel.style.width = 140f; rowLabel.style.height = 18f;
                UTKTheme.StyleFigmaText(rowLabel, 15.6f, 500, TextSub);
                sect.Add(rowLabel);
                var value = new Label("-") { name = "Stat_" + labels[i] };
                value.style.position = Position.Absolute;
                value.style.left = gx + 259f; value.style.top = ry; value.style.width = 75f; value.style.height = 22f;
                UTKTheme.StyleFigmaText(value, 16.8f, 700, colors[i], TextAnchor.MiddleRight);
                sect.Add(value);
                _statValues[i] = value;
                if (i < 2)
                {
                    var line = new VisualElement { name = "StatLine" };
                    line.style.position = Position.Absolute;
                    line.style.left = gx; line.style.top = ry + rowH - 1f; line.style.width = 334f; line.style.height = 1f;
                    line.style.backgroundColor = new StyleColor(UTKTheme.Stroke);
                    line.pickingMode = PickingMode.Ignore;
                    sect.Add(line);
                }
            }
            _content.Add(sect);
        }

        private void BuildSpecialStats()
        {
            var sect = new VisualElement { name = "SpecialStatsSection" };
            sect.style.position = Position.Absolute;
            sect.style.left = BodyInset; sect.style.top = 724f;
            sect.style.width = PanelW - BodyInset * 2f; sect.style.height = 157f;
            sect.style.backgroundColor = new StyleColor(SectBg2);
            Round(sect, 9.6f);
            var headLabel = new Label("특수 상태 게이지");
            headLabel.style.position = Position.Absolute; headLabel.style.left = 16.8f; headLabel.style.top = 16.8f;
            UTKTheme.StyleFigmaText(headLabel, 14.4f, 700, TextSub);
            sect.Add(headLabel);
            var headEn = new Label("SPECIAL VITALS");
            headEn.style.position = Position.Absolute; headEn.style.right = 20.2f; headEn.style.top = 17.3f;
            UTKTheme.StyleFigmaText(headEn, 12f, 400, Faint, TextAnchor.MiddleRight);
            sect.Add(headEn);
            // 게이지 2 — 호감도(#BC8CFF) / 중독도(#F2C94C)
            _affLabel = Gauge(sect, 0, "호감도 (Affection)", Epic, out _affValue, out _affNote, out _affFill);
            _toxLabel = Gauge(sect, 1, "중독도 (Toxicity)", GoldY, out _toxValue, out _toxNote, out _toxFill);
            _content.Add(sect);
        }

        private Label Gauge(VisualElement sect, int index, string labelText, Color fill,
            out Label value, out Label note, out VisualElement fillEl)
        {
            float gy = 50.6f + index * 53.2f;
            var label = new Label(labelText);
            label.style.position = Position.Absolute; label.style.left = 33.6f; label.style.top = gy + 1.4f;
            UTKTheme.StyleFigmaText(label, 15.6f, 700, TextMain);
            sect.Add(label);
            value = new Label("-");
            value.style.position = Position.Absolute; value.style.right = 78.6f; value.style.top = gy - 0.6f;
            UTKTheme.StyleFigmaText(value, 16.8f, 700, fill, TextAnchor.MiddleRight);
            sect.Add(value);
            note = new Label("");
            note.style.position = Position.Absolute; note.style.right = 20.2f; note.style.top = gy + 4.4f;
            UTKTheme.StyleFigmaText(note, 13.2f, 400, TextSub, TextAnchor.MiddleRight);
            sect.Add(note);
            var bg = new VisualElement { name = "BarBg_" + labelText };
            bg.style.position = Position.Absolute; bg.style.left = 16.8f; bg.style.top = gy + 29.2f;
            bg.style.width = PanelW - BodyInset * 2f - 33.6f; bg.style.height = 7.2f;
            bg.style.backgroundColor = new StyleColor(BarBg);
            Round(bg, 3.6f);
            fillEl = new VisualElement { name = "BarFill_" + labelText };
            fillEl.style.height = 7.2f;
            fillEl.style.backgroundColor = new StyleColor(fill);
            Round(fillEl, 3.6f);
            fillEl.pickingMode = PickingMode.Ignore;
            bg.Add(fillEl);
            sect.Add(bg);
            return label;
        }

        // =====================================================================
        //  EquipmentPanel (아군 결합 — 피그마 84:4 우측)
        // =====================================================================

        private void BuildEquipmentPanel()
        {
            var panel = new VisualElement { name = "EquipmentPanel" };
            panel.style.position = Position.Absolute;
            panel.style.left = PanelW + PanelGap;
            panel.style.top = 0f;
            panel.style.width = EquipPanelW; panel.style.height = PanelH;
            panel.style.backgroundColor = new StyleColor(GlassBg);
            Round(panel, 14.4f);
            _content.Add(panel);
            _equipPanelRoot = panel;

            // 코너 데칼 — EquipmentPanel 자체 4코너
            void L(float l, float t, float lw, float lh)
            {
                var e = new VisualElement();
                e.style.position = Position.Absolute;
                e.style.left = l; e.style.top = t; e.style.width = lw; e.style.height = lh;
                e.style.backgroundColor = new StyleColor(UTKTheme.Stroke);
                e.pickingMode = PickingMode.Ignore;
                panel.Add(e);
            }
            L(0, 0, 12f, 1f); L(0, 0, 1f, 12f);
            L(EquipPanelW - 12f, 0, 12f, 1f); L(EquipPanelW - 1f, 0, 1f, 12f);
            L(0, PanelH - 1f, 12f, 1f); L(0, PanelH - 12f, 1f, 12f);
            L(EquipPanelW - 12f, PanelH - 1f, 12f, 1f); L(EquipPanelW - 1f, PanelH - 12f, 1f, 12f);

            // SectionHeader — "장착 장비" 24/700 + LOADOUT MATRIX + SYS_EQ_STABLE
            var title = new Label("장착 장비") { name = "EquipPanelTitle" };
            title.style.position = Position.Absolute; title.style.left = 24f; title.style.top = 29.2f;
            UTKTheme.StyleFigmaText(title, 24f, 700, TextMain);
            panel.Add(title);
            var sub = new Label("LOADOUT MATRIX");
            sub.style.position = Position.Absolute; sub.style.left = 131f; sub.style.top = 38.2f;
            UTKTheme.StyleFigmaText(sub, 13.2f, 500, TextSub);
            panel.Add(sub);
            var stable = new Label("SYS_EQ_STABLE") { name = "EquipStable" };
            stable.style.position = Position.Absolute; stable.style.left = 389.6f; stable.style.top = 35.2f;
            UTKTheme.StyleFigmaText(stable, 12f, 400, GreenY);
            panel.Add(stable);

            // EquippedSection — (24,96) 483.6×230.4
            var eqSect = new VisualElement { name = "EquippedSection" };
            eqSect.style.position = Position.Absolute; eqSect.style.left = 24f; eqSect.style.top = 96f;
            eqSect.style.width = 483.6f; eqSect.style.height = 230.4f;
            eqSect.style.backgroundColor = new StyleColor(SectBg);
            Round(eqSect, 9.6f);
            panel.Add(eqSect);
            var eqLabel = new Label("장착 중인 장비");
            eqLabel.style.position = Position.Absolute; eqLabel.style.left = 14.4f; eqLabel.style.top = 14.4f;
            UTKTheme.StyleFigmaText(eqLabel, 19.2f, 700, Accent);
            eqSect.Add(eqLabel);
            // EquipGrid — 10슬롯 5×2 (81.6, 열 스트라이드 86.4 / 행 스트라이드 91.2) — 라벨 있음(피그마 84:4)
            string[] slotLabels = { "투구", "갑옷", "신발", "방패", "무기", "방독면", "분사기", "물약", "반지", "목걸이" };
            for (int i = 0; i < 10; i++)
            {
                int col = i % 5, row = i / 5;
                var slot = new UTKSlot { name = "GuardEquip_" + slotLabels[i] };
                slot.style.position = Position.Absolute;
                slot.style.left = 28.2f + col * 86.4f;
                slot.style.top = 49.2f + row * 91.2f;
                slot.style.width = 81.6f; slot.style.height = 81.6f;
                slot.style.backgroundColor = new StyleColor(new Color(0.129f, 0.149f, 0.176f, 0.6f));
                Round(slot, 9.6f);
                SetSlotBorderWidth(slot, 0f);
                slot.SetRank("common");
                // 아이콘 38.4 중앙
                var host = slot.IconHost;
                if (host != null)
                {
                    host.style.position = Position.Absolute;
                    host.style.left = 21.6f; host.style.top = 21.6f;
                    host.style.width = 38.4f; host.style.height = 38.4f;
                    host.style.flexGrow = 0f;
                }
                // 상단 TierStrip
                var strip = new VisualElement { name = "GuardEquipStrip_" + slotLabels[i] };
                strip.style.position = Position.Absolute;
                strip.style.left = 0f; strip.style.top = 0f;
                strip.style.width = 81.6f; strip.style.height = 3.6f;
                strip.style.backgroundColor = new StyleColor(InventoryWindowUTKAccess.StripCommon);
                strip.pickingMode = PickingMode.Ignore;
                slot.Add(strip);
                _gearStrips[i] = strip;
                // 하단 라벨(피그마 84:4 — 9.6/500 #8B949E)
                var lab = new Label(slotLabels[i]);
                lab.style.position = Position.Absolute;
                lab.style.left = 0f; lab.style.bottom = 1f; lab.style.width = 81.6f; lab.style.height = 11f;
                lab.style.fontSize = 9.6f; lab.style.color = new StyleColor(TextSub);
                lab.style.unityTextAlign = TextAnchor.MiddleCenter;
                lab.pickingMode = PickingMode.Ignore;
                slot.Add(lab);
                // Count 라벨 우하단(전술 배낭과 동일 스타일 — 장비는 무장착이면 공란)
                var cnt = new Label("");
                cnt.style.position = Position.Absolute;
                cnt.style.right = 7.6f; cnt.style.bottom = 4.6f; cnt.style.width = 30f; cnt.style.height = 17f;
                cnt.style.fontSize = 13.2f; cnt.style.color = new StyleColor(TextSub);
                cnt.style.unityTextAlign = TextAnchor.MiddleRight;
                cnt.pickingMode = PickingMode.Ignore;
                slot.Add(cnt);
                _gearCountLabels[i] = cnt;
                _gearSlots[i] = slot;
                eqSect.Add(slot);
            }

            // TacticalPackSection — (24,345.6) 483.6×567 — 플레이어 인벤 전체(5×5, Count 실측)
            var tp = new VisualElement { name = "TacticalPackSection" };
            tp.style.position = Position.Absolute; tp.style.left = 24f; tp.style.top = 345.6f;
            tp.style.width = 483.6f; tp.style.height = 567f;
            tp.style.backgroundColor = new StyleColor(SectBg2);
            Round(tp, 9.6f);
            panel.Add(tp);
            var tpTitle = new Label("전술 배낭") { name = "TacticalPackTitle" };
            tpTitle.style.position = Position.Absolute; tpTitle.style.left = 19.2f; tpTitle.style.top = 14.4f;
            UTKTheme.StyleFigmaText(tpTitle, 19.2f, 700, TextMain);
            tp.Add(tpTitle);
            var tpSub = new Label("TACTICAL PACK");
            tpSub.style.position = Position.Absolute; tpSub.style.left = 19.2f; tpSub.style.top = 42.2f;
            UTKTheme.StyleFigmaText(tpSub, 12f, 400, TextSub);
            tp.Add(tpSub);
            _packGrid = new VisualElement { name = "TacticalPackGrid" };
            _packGrid.style.position = Position.Absolute;
            _packGrid.style.left = 19.2f; _packGrid.style.top = 87f;
            _packGrid.style.width = 445.2f; _packGrid.style.height = 465.6f;
            tp.Add(_packGrid);
        }

        private VisualElement _equipPanelRoot;
        private VisualElement _packGrid;

        // =====================================================================
        //  데이터 갱신 (실측 API만 — 위조 금지)
        // =====================================================================

        private void RefreshData()
        {
            var g = _guard;
            if (g == null) return;
            bool ally = g.IsAlly;

            // Identity — 등급 문자열(★) + 직업/국가 + 코드네임
            string grade = g.Level >= 35 ? "★★★★" : g.Level >= 20 ? "★★★" : g.Level >= 10 ? "★★" : "★";
            _rarityText.text = $"{grade} ({GetRarityWord(g.Level)})";
            _classText.text = string.IsNullOrEmpty(g.JobTitle) ? "-" : $"클래스: {g.JobTitle}";
            _nameText.text = $"코드네임: {g.GuardName}";

            // Stats — 실측
            _levelValue.text = g.Level.ToString();
            _expValue.text = "-";   // 병사 EXP 데이터 부재 — 위조 금지("-")
            _statValues[0].text = "+" + g.GetAttack().ToString("#,##0");
            _statValues[1].text = "+" + g.GetDefense().ToString("#,##0");
            _statValues[2].text = g.GetMaxHP().ToString("#,##0");

            // HP 오버레이
            float maxHp = g.GetMaxHP();
            _hpOverlay.text = maxHp > 0 ? $"HP {Mathf.RoundToInt(Mathf.Clamp01(g.CurrentHP / maxHp) * 100f)}%" : "HP -";

            // 특수 상태 — 호감도(실측 등급명) / 중독도(실측 단계명)
            _affValue.text = $"{g.Loyalty:F0}";
            _affNote.text = GuardLoyaltySystem.GetAffinityGradeName(g.Loyalty);
            _affFill.style.width = (PanelW - BodyInset * 2f - 33.6f) * Mathf.Clamp01(Mathf.Abs(g.Loyalty) / 100f);
            _toxValue.text = $"{g.Addiction:F0}";
            _toxNote.text = g.Addiction <= 0f
                ? "중독 없음"
                : GuardAddictionSystem.GetStageName(GuardAddictionSystem.GetAddictionStage(g.Addiction));
            _toxFill.style.width = (PanelW - BodyInset * 2f - 33.6f) * Mathf.Clamp01(g.Addiction / 100f);

            // 장비 10슬롯 — 실측 6종 + 비지원 4종(빈 셀 — 라벨만)
            PlayerInventory.ItemData[] gear =
            {
                g.HelmetItem, g.ArmorItem, g.BootsItem, g.ShieldItem, g.WeaponItem,
                null, null, null, null, null,   // 방독면/분사기/물약/반지 — 병사 장비 슬롯 부재(빈 셀)
            };
            for (int i = 0; i < 10; i++)
            {
                var slot = _gearSlots[i];
                if (slot == null) continue;
                var item = gear[i];
                if (item != null)
                {
                    slot.SetIcon(ItemIconDatabase.GetOrCreateIcon(item));
                    slot.SetRank(UTKRarity.ClassForIndex((int)item.rarity));
                    if (_gearStrips[i] != null)
                        _gearStrips[i].style.backgroundColor = new StyleColor(StripColor((int)item.rarity));
                    _gearCountLabels[i].text = "";
                    slot.tooltip = item.displayName;
                }
                else
                {
                    slot.SetIcon(null);
                    slot.SetRank("common");
                    if (_gearStrips[i] != null)
                        _gearStrips[i].style.backgroundColor = new StyleColor(InventoryWindowUTKAccess.StripCommon);
                    _gearCountLabels[i].text = "";
                    slot.tooltip = null;
                }
            }

            // 전술 배낭 — 플레이어 인벤 실측(5×5, Count)
            RebuildPack();
        }

        private void RebuildPack()
        {
            if (_packGrid == null) return;
            _packGrid.Clear();
            var inv = PlayerInventory.Instance;
            var slots = inv != null ? inv.GetAllSlots() : null;
            int shown = 0;
            int total = slots != null ? slots.Length : 0;
            for (int i = 0; i < total && shown < 25; i++)
            {
                var s = slots[i];
                if (s == null || s.item == null || s.count <= 0) continue;
                if (s.item.id == "gold") continue;   // [70차] 골드는 지갑 단일소스 — 배낭 미표시
                int col = shown % 5, row = shown / 5;
                var slot = new UTKSlot { name = "PackSlot_" + s.item.id };
                slot.style.position = Position.Absolute;
                slot.style.left = col * 91.2f;
                slot.style.top = row * 96f;
                slot.style.width = 81.6f; slot.style.height = 81.6f;
                slot.style.backgroundColor = new StyleColor(new Color(0.129f, 0.149f, 0.176f, 0.6f));
                Round(slot, 9.6f);
                SetSlotBorderWidth(slot, 0f);
                slot.SetIcon(ItemIconDatabase.GetOrCreateIcon(s.item));
                slot.SetRank(UTKRarity.ClassForIndex((int)s.item.rarity));
                slot.tooltip = $"{s.item.displayName} ×{s.count}";
                var strip = new VisualElement { name = "PackStrip_" + s.item.id };
                strip.style.position = Position.Absolute;
                strip.style.left = 0f; strip.style.top = 0f;
                strip.style.width = 81.6f; strip.style.height = 3.6f;
                strip.style.backgroundColor = new StyleColor(StripColor((int)s.item.rarity));
                strip.pickingMode = PickingMode.Ignore;
                slot.Add(strip);
                var cnt = new Label(s.count.ToString("#,##0"));
                cnt.style.position = Position.Absolute;
                cnt.style.right = 7.6f; cnt.style.bottom = 4.6f; cnt.style.width = 40f; cnt.style.height = 17f;
                cnt.style.fontSize = 13.2f; cnt.style.color = new StyleColor(TextSub);
                cnt.style.unityTextAlign = TextAnchor.MiddleRight;
                cnt.pickingMode = PickingMode.Ignore;
                slot.Add(cnt);
                _packGrid.Add(slot);
                shown++;
            }
        }

        private static string GetRarityWord(int level)
        {
            if (level >= 35) return "영웅적인 에이전트 (HEROIC)";
            if (level >= 20) return "희귀한 에이전트 (RARE)";
            return "일반 병사 (COMMON)";
        }

        private static Color StripColor(int rarity)
        {
            return rarity >= 3 ? new Color(0.737f, 0.549f, 1f)
                 : rarity == 2 ? new Color(0.122f, 0.435f, 0.922f)
                 : new Color(0.545f, 0.580f, 0.620f);
        }

        private static void Round(VisualElement ve, float r)
        {
            ve.style.borderTopLeftRadius = r;
            ve.style.borderTopRightRadius = r;
            ve.style.borderBottomLeftRadius = r;
            ve.style.borderBottomRightRadius = r;
        }

        private static void SetSlotBorderWidth(VisualElement ve, float w)
        {
            ve.style.borderTopWidth = ve.style.borderBottomWidth = ve.style.borderLeftWidth = ve.style.borderRightWidth = new StyleFloat(w);
        }

        // =====================================================================
        //  3D Portrait — 대상 병사 씬 실물 촬영(RT)
        // =====================================================================

        private void StartCapture()
        {
            StopCapture();
            _rt = new RenderTexture(512, 288, 24, RenderTextureFormat.ARGB32);
            _rt.name = "GuardStatusRT";
            _rt.Create();
            var camGo = new GameObject("GuardStatusCam");
            _capCam = camGo.AddComponent<Camera>();
            _capCam.clearFlags = CameraClearFlags.SolidColor;
            _capCam.backgroundColor = PortraitBg;
            _capCam.cullingMask = ~0;
            _capCam.nearClipPlane = 0.1f;
            _capCam.farClipPlane = 30f;
            _capCam.fieldOfView = 30f;
            _capCam.targetTexture = _rt;
            CaptureOnce();
        }

        /// <summary>[72차] RT → Texture2D 복사(GblItemIconRenderer.BakeFromRT 선례) — UIElements
        ///   background-image는 RenderTexture 직접 수용 불가(Texture2D만) — ReadPixels 경유.</summary>
        private void CaptureOnce()
        {
            if (_guard == null || _capCam == null || _rt == null) return;
            var t = _guard.transform;
            var rends = _guard.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return;
            var b = rends[0].bounds;
            foreach (var r in rends) if (r.enabled) b.Encapsulate(r.bounds);
            Vector3 center = new Vector3(t.position.x, b.center.y, t.position.z);
            // 정면 우측 35도/약간 위 — 실물 전신 프레이밍
            float dist = Mathf.Max(1.2f, b.extents.y * 2.6f);
            Vector3 dir = Quaternion.Euler(0f, 35f, 0f) * new Vector3(0f, 0.25f, -1f);
            _capCam.transform.position = center + dir * dist;
            _capCam.transform.LookAt(center);
            try
            {
                _capCam.Render();
                // RT → Texture2D(GblItemIconRenderer.BakeFromRT 선례 — ReadPixels)
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = _rt;
                var copy = new Texture2D(512, 288, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, 512, 288), 0, 0);
                copy.Apply();
                RenderTexture.active = prev;
                copy.wrapMode = TextureWrapMode.Clamp;
                copy.hideFlags = HideFlags.HideAndDontSave;
                if (_captured != null) Object.Destroy(_captured);
                _captured = copy;
                _portraitHost.style.backgroundImage = new StyleBackground(_captured);
            }
            catch { /* 첫 렌더 실패 무시 — 폴링이 이어서 */ }
        }

        private Texture2D _captured;

        private void StopCapture()
        {
            if (_capCam != null)
            {
                Object.Destroy(_capCam.gameObject);
                _capCam = null;
            }
            if (_rt != null)
            {
                _rt.Release();
                Object.Destroy(_rt);
                _rt = null;
            }
            if (_captured != null)
            {
                Object.Destroy(_captured);
                _captured = null;
            }
            if (_portraitHost != null)
                _portraitHost.style.backgroundImage = new StyleBackground(StyleKeyword.None);
        }

        // =====================================================================
        //  폴링 — 스탯 0.5s / Portrait 재촬영 1s / 대상 생존 감시
        // =====================================================================

        private void StartRefreshLoop()
        {
            if (_refreshTask != null) return;
            _refreshTask = schedule.Execute(() =>
            {
                if (!IsOpen || _guard == null) return;
                _pollTimer += 0.5f;
                if (_guard.gameObject == null || !_guard.IsAlive)
                {
                    Close();
                    return;
                }
                RefreshData();
                if (_pollTimer >= 1f)
                {
                    _pollTimer = 0f;
                    CaptureOnce();
                }
            }).Every(500);
        }

        private void StopRefreshLoop()
        {
            if (_refreshTask == null) return;
            _refreshTask.Pause();
            _refreshTask = null;
        }
    }

    /// <summary>[72차] 인벤 68차 공용 상수 접근 헬퍼 — InventoryWindowUTK private 색상 상수 재선언 방지용.
    ///   (TierStrip common 색 — #8B949E)</summary>
    internal static class InventoryWindowUTKAccess
    {
        public static readonly Color StripCommon = new Color(0.545f, 0.580f, 0.620f);   // #8B949E
    }
}
