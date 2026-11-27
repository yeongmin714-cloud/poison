using System;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;      // PlayerHealth — Heal(282행)/MaxHP/CurrentHP 실측
using ProjectName.Systems;   // TimeManager/Bed/SaveManager/PlayerMovement/SleepAutoSaveToggle

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// [73차] 수면 시스템 패널 — 피그마 227:262 전면 수용(2026-10-15 실측 진실치 그대로).
    /// 계획서: docs/2026-10-15_73TH_SLEEP_PANEL_AND_DIALOGUE_PLAN.md Phase A-1.
    ///
    /// 구조: UTKWindowBase Frameless(InteractionPanelUTK 74행 SetChrome 선례) — 침대 상호작용 전용,
    /// 화면 중앙 absolute 배치, UTKThreeColumnLayout 미사용(피그마 단일 컬럼 절대 배치).
    /// 창 624×1101 bg #13181F radius 14.
    ///
    /// 데이터 소스(실측만 — 위조 금지, 없는 값은 "-"):
    ///   - TimeManager.Hour/CurrentDay/IsNight/GetFormattedTime — 시간/일차/현재시간/기상 계산(24h wrap+일차 가산)
    ///   - Bed.BedName — 휴식 장소명
    ///   - PlayerHealth.MaxHP/CurrentHP — Heal API(282행) 회복 적용
    ///   - PlayerMovement.Stamina/MaxStamina(1670~1672행 — Systems 내부, 실내 씬 부재 시 "-")
    ///
    /// 수면 실행: 자동저장 ON → SaveManager.AutoSave() → TimeManager.SleepFor(hours, onComplete)
    /// (107행 — 시간 점프+오버레이 0.3~2s 클램프. GameTime setter 직접 조작 금지 — 원본 규약)
    /// → onComplete에서 회복 적용(체력 Heal 8h 기준 36% 누락분 clamp / 기력 스태미너 회복) + 토스트 + 창 닫힘.
    /// </summary>
    public class SleepPanelUTK : UTKWindowBase
    {
        // ───────────────────── 피그마 진실치 (227:262 — 2026-10-15 실측) ─────────────────────
        private const float WinW = 624f;         // 창 624×1101
        private const float WinH = 1101f;
        private const float WinRadius = 14f;     // radius 14
        // 패널 로컬 좌표(블록 원점) — 계획서 표 그대로
        private const float TitleXY = 24f;       // 패널 제목 (24,24) 576×48
        private const float RestPlaceY = 92f;    // 휴식 장소 (24,92) 576×70
        private const float SleepTimeY = 182f;   // 수면 시간 (24,182) 576×193
        private const float WakePreviewY = 213f; // 기상 미리보기 (24,213) 576×92
        private const float TimeChoiceY = 317f;  // 수면 시간 선택 (24,317) 576×58
        private const float RecoveryY = 395f;    // 회복 예상 (24,395) 576×149
        private const float SleepNoteY = 530f;   // 수면 안내 (24,530)
        private const float SaveY = 564f;        // 세이브 (24,564) 576×403
        private const float SleepActionY = 987f; // 수면 실행 (24,987) 576×90
        private const float InnerW = 576f;       // 24 좌우 여백 기준 내부 폭
        // 회복 계수 — 8h 기준 체력 36% / 기력 72%(피그마 "+36"/"+72" 목업치를 계수로 채용 — 계획서 A-2)
        private const float HealRatio8h = 0.36f;
        private const float StaminaRatio8h = 0.72f;
        // 스위치 38×22 radius 11 — 손잡이 16×16 (ON 우측 19 / OFF 좌측 3)
        private const float SwitchW = 38f, SwitchH = 22f, KnobSize = 16f, KnobOnX = 19f, KnobOffX = 3f;

        // 색상 — 피그마 헥스 그대로
        private static readonly Color WinBg      = new Color(0x13 / 255f, 0x18 / 255f, 0x1F / 255f, 1f); // #13181F
        private static readonly Color CardBg     = new Color(1f, 1f, 1f, 0.02f);                        // #FFFFFF/2%
        private static readonly Color IconBg     = new Color(0x1C / 255f, 0x23 / 255f, 0x2C / 255f, 1f); // #1C232C
        private static readonly Color Accent     = new Color(0x58 / 255f, 0xA6 / 255f, 0xFF / 255f, 1f); // #58A6FF
        private static readonly Color AccentBg   = new Color(0x58 / 255f, 0xA6 / 255f, 0xFF / 255f, 0.08f); // #58A6FF/8%
        private static readonly Color TextMain   = new Color(0xF0 / 255f, 0xF6 / 255f, 0xFC / 255f, 1f); // #F0F6FC
        private static readonly Color TextSub    = new Color(0x8B / 255f, 0x94 / 255f, 0x9E / 255f, 1f); // #8B949E
        private static readonly Color BtnTextDark = new Color(0x0B / 255f, 0x0E / 255f, 0x14 / 255f, 1f); // #0B0E14
        // [144차 P7-SLP] 테두리 진실치 — 루트/카드/칩 #30363D@0.502, 강조 테두리 #58A6FF@0.251(227:262/270/283/294/300/308)
        private static readonly Color StrokeSub    = new Color(0x30 / 255f, 0x36 / 255f, 0x3D / 255f, 0.502f); // #30363D/50.2%
        private static readonly Color AccentStroke = new Color(0x58 / 255f, 0xA6 / 255f, 0xFF / 255f, 0.251f); // #58A6FF/25.1%

        // ───────────────────── 상태 ─────────────────────
        private static SleepPanelUTK _instance;

        private Bed _bed;
        private float _selectedHours = 8f;   // 피그마 기본 — "8시간 수면"
        private bool _busy;                  // 수면 중 이중 클릭 방지
        private VisualElement _sleepOverlay;

        // UI 핸들(갱신용)
        private Label _nowDateLabel;         // "N일차 · 밤"
        private Label _nowTimeLabel;         // 현재 HH:MM 32/500
        private Label _wakeTimeLabel;        // 기상 HH:MM 32/500
        private Label _wakeDayLabel;         // "기상 예정 · N일차"
        private Label _sleepDurationLabel;   // "N시간 수면"
        private Label _recoveryRangeLabel;   // "N시간 수면 기준"
        private Button[] _timeButtons;
        private Label[] _timeBtnNames;
        private Label[] _timeBtnSubs;
        // 회복 예상 — 체력
        private Label _hpDeltaLabel, _hpCurLabel, _hpEstLabel;
        private VisualElement _hpFill;
        // 회복 예상 — 기력(스태미너 부재 시 "-" 폴백)
        private VisualElement _stRow, _stMissing;
        private Label _stDeltaLabel, _stCurLabel, _stEstLabel;
        private VisualElement _stFill;
        // 스위치
        private VisualElement _switchTrack, _switchKnob;
        private Label _switchLabel;
        // 슬롯
        private Label[] _slotTitleLabels;
        private Label[] _slotStateLabels;      // [144차 P7-SLP #2] 저장 상태 행("최근 저장" 11/400) — 구 info(일차/타임스탬프) 통합, 3행 구성 마감
        private Label[] _slotPlayTimeLabels;   // [144차 P7-SLP #2] 플레이 시간 행(11/400 — Geist Mono 근사) 신설
        private Label _slotsCountLabel;

        // 스태미너 회복 — PlayerMovement 직접(리플렉션 _stamina 필드 — public API 부재, Systems 동일 규약)
        private static readonly System.Reflection.FieldInfo StaminaField =
            typeof(PlayerMovement).GetField("_stamina",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        private SleepPanelUTK() : base("수면 시스템", new Vector2(WinW, WinH))
        {
            SetChrome(UTKWindowChrome.Frameless);   // InteractionPanelUTK 74행 SetChrome 선례

            style.width = WinW;
            style.height = WinH;
            style.backgroundColor = new StyleColor(WinBg);          // #13181F
            style.borderTopLeftRadius = style.borderTopRightRadius = WinRadius;
            style.borderBottomLeftRadius = style.borderBottomRightRadius = WinRadius;

            // [144차 P7-SLP #4] 루트 테두리 — strokes #30363D/1.0@0.502 sw1.2(227:262). Frameless 경로는
            // UTKWindowBase ctor/ApplyChrome(:288)이 border 0으로 깔리므로 서브클래스에서 재지정(V2 경로 테두리).
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 1.2f;
            style.borderTopColor = style.borderBottomColor = new StyleColor(StrokeSub);
            style.borderLeftColor = style.borderRightColor = new StyleColor(StrokeSub);
            // [144차 P7-SLP #5] 기록 전용(미적용) — fx BACKGROUND_BLUR:36 + DROP_SHADOW #000000/0.251(227:262).
            // UTK 스타일 API상 blur/그림자 무상수 — 144차 §4 공통(전 Phase 선례) 기록만 유지.
            style.overflow = Overflow.Hidden;

            _content.style.width = WinW;
            _content.style.height = WinH;
            _content.style.paddingLeft = 0f;
            _content.style.paddingRight = 0f;
            _content.style.paddingTop = 0f;
            _content.style.paddingBottom = 0f;
            _content.style.flexGrow = 0f;
            _content.style.flexShrink = 0f;

            BuildTitleRow();
            BuildRestPlaceRow();
            BuildSleepTimeBlock();
            BuildRecoveryBlock();
            BuildSleepNote();
            BuildSaveSection();
            BuildSleepActionRow();

            // [73차] Enter 확인 단축키 — UIRoot 경유(ESC 닫기는 UTKWindowManager ESC 스택 경유).
            // IsOpen 가드 — 패널 닫힘 시 무시. 부착/분리 시 등록/해제(멱등).
            RegisterCallback<AttachToPanelEvent>(_ => UIToolkitBootstrap.UIRoot?.RegisterCallback<KeyDownEvent>(OnRootKeyDown));
            RegisterCallback<DetachFromPanelEvent>(_ => UIToolkitBootstrap.UIRoot?.UnregisterCallback<KeyDownEvent>(OnRootKeyDown));

            ApplyUIToolkitFont(this);
        }

        // ═════════════════════════ ① 패널 제목 (24,24) 576×48 ═════════════════════════
        private void BuildTitleRow()
        {
            var row = Block(TitleXY, TitleXY, InnerW, 48f);

            var moon = BuildMoonIcon(24f, Accent);         // [144차 P7-SLP #13] moon Vector stroke #58A6FF sw1.8(227:366) — 절차 근사 채색
            moon.style.position = Position.Absolute;
            moon.style.left = 0f; moon.style.top = 3f;     // [144차 P7-SLP #13] yΔ9 수리 — 피그마 (24,27)→행 로컬 (0,3)
            row.Add(moon);

            var title = MakeLabel("수면 시스템", 22f, FontStyle.Bold, TextMain);   // 22/700
            title.style.position = Position.Absolute;
            title.style.left = 34f; title.style.top = 4f;
            row.Add(title);

            var sub = MakeLabel("휴식 및 진행 상황 저장", 12f, FontStyle.Normal, TextSub);  // 12/400
            // [144차 P7-SLP #9] 보조 제목 — 피그마 (179,32)→행 로컬 (155,8), 제목 우측 인라인(하단 배치 → 우측 부착)
            sub.style.position = Position.Absolute;
            sub.style.left = 155f; sub.style.top = 8f;
            row.Add(sub);

            var closeBtn = new Button(Hide) { text = "" }; // 닫기 30×30 #FFFFFF/2% radius 5
            closeBtn.name = "SleepCloseButton";
            closeBtn.style.position = Position.Absolute;
            closeBtn.style.right = 0f; closeBtn.style.top = 0f;   // [144차 P7-SLP #11] yΔ9 수리 — 피그마 (570,24)→행 로컬 (546,0)
            closeBtn.style.width = 30f; closeBtn.style.height = 30f;
            closeBtn.style.backgroundColor = new StyleColor(CardBg);
            // [144차 P7-SLP #12] 닫기 테두리 — strokes #30363D/1.0@0.502 sw1.0(227:267)
            closeBtn.style.borderTopWidth = closeBtn.style.borderBottomWidth = 1f;
            closeBtn.style.borderLeftWidth = closeBtn.style.borderRightWidth = 1f;
            closeBtn.style.borderTopColor = closeBtn.style.borderBottomColor = new StyleColor(StrokeSub);
            closeBtn.style.borderLeftColor = closeBtn.style.borderRightColor = new StyleColor(StrokeSub);
            closeBtn.style.borderTopLeftRadius = closeBtn.style.borderTopRightRadius = 5f;
            closeBtn.style.borderBottomLeftRadius = closeBtn.style.borderBottomRightRadius = 5f;
            closeBtn.style.paddingLeft = closeBtn.style.paddingRight = 0f;
            closeBtn.style.paddingTop = closeBtn.style.paddingBottom = 0f;
            closeBtn.Add(BuildCloseGlyph());
            row.Add(closeBtn);
        }

        // ═════════════════════════ ② 휴식 장소 (24,92) 576×70 ═════════════════════════
        private void BuildRestPlaceRow()
        {
            var row = Card(RestPlaceY, InnerW, 70f, 10f);   // 576×70 #FFFFFF/2% radius 10

            var iconHost = new VisualElement();             // 장소 아이콘 42×42 #1C232C radius 7 — bed 절차 아이콘(bed=rect 2개)
            iconHost.name = "RestPlaceIcon";
            iconHost.style.position = Position.Absolute;
            iconHost.style.left = 14f; iconHost.style.top = 14f;
            iconHost.style.width = 42f; iconHost.style.height = 42f;
            iconHost.style.backgroundColor = new StyleColor(IconBg);
            iconHost.style.borderTopLeftRadius = iconHost.style.borderTopRightRadius = 7f;
            iconHost.style.borderBottomLeftRadius = iconHost.style.borderBottomRightRadius = 7f;
            iconHost.pickingMode = PickingMode.Ignore;
            var bedGlyph = BuildBedGlyph();
            bedGlyph.style.position = Position.Absolute;
            bedGlyph.style.left = 9f; bedGlyph.style.top = 9f;
            iconHost.Add(bedGlyph);
            row.Add(iconHost);

            _placeNameLabel = MakeLabel("-", 14f, FontStyle.Bold, TextMain);   // 장소 이름 14/700 — BedName 실측(위조 금지)
            _placeNameLabel.style.position = Position.Absolute;
            _placeNameLabel.style.left = 68f; _placeNameLabel.style.top = 17f;   // [144차 P7-SLP #17] 피그마 (92,109)→카드 로컬 (68,17)
            row.Add(_placeNameLabel);

            _placeDescLabel = MakeLabel("-", 12f, FontStyle.Normal, TextSub);  // 설명 12/400
            _placeDescLabel.style.position = Position.Absolute;
            _placeDescLabel.style.left = 68f; _placeDescLabel.style.top = 39f;   // [144차 P7-SLP #18] 피그마 (92,131)→카드 로컬 (68,39)
            row.Add(_placeDescLabel);

            // 안전 상태 배지 (504,116)→row 로컬 (480,24) 82×22 #58A6FF/8% radius 5
            var badge = new VisualElement();
            badge.name = "SafeZoneBadge";
            badge.style.position = Position.Absolute;
            badge.style.right = 14f; badge.style.top = 24f;
            badge.style.width = 82f; badge.style.height = 22f;
            badge.style.backgroundColor = new StyleColor(AccentBg);
            badge.style.borderTopLeftRadius = badge.style.borderTopRightRadius = 5f;
            badge.style.borderBottomLeftRadius = badge.style.borderBottomRightRadius = 5f;
            badge.pickingMode = PickingMode.Ignore;
            var shield = BuildShieldGlyph();
            shield.style.position = Position.Absolute;
            shield.style.left = 8f; shield.style.top = 4f;   // [144차 P7-SLP #20] shield-check 14×14 — 피그마 (512,120)→배지 로컬 (8,4)
            badge.Add(shield);
            var badgeText = MakeLabel("안전 지역", 12f, FontStyle.Normal, Accent);   // 12/500 #58A6FF
            badgeText.style.position = Position.Absolute;
            badgeText.style.left = 28f; badgeText.style.top = 2f;
            badge.Add(badgeText);
            row.Add(badge);
        }

        private Label _placeNameLabel;
        private Label _placeDescLabel;

        // ═════════════════════════ ③ 수면 시간 (24,182) 576×193 ═════════════════════════
        private void BuildSleepTimeBlock()
        {
            var block = Block(TitleXY, SleepTimeY, InnerW, 193f);

            var header = MakeLabel("수면 시간", 16f, FontStyle.Bold, TextMain);   // 16/700
            header.style.position = Position.Absolute;
            header.style.left = 0f; header.style.top = 0f;
            block.Add(header);

            _nowDateLabel = MakeLabel("-", 12f, FontStyle.Normal, TextSub);       // 현재 날짜 우측 — "N일차 · 밤" 실측
            _nowDateLabel.style.position = Position.Absolute;
            _nowDateLabel.style.right = 0f; _nowDateLabel.style.top = 4f;
            block.Add(_nowDateLabel);

            // ── 기상 미리보기 (24,213) 576×92 #58A6FF/8% radius 10 ──
            var preview = new VisualElement();
            preview.name = "WakePreview";
            preview.style.position = Position.Absolute;
            preview.style.left = 0f; preview.style.top = WakePreviewY - SleepTimeY;   // 31
            preview.style.width = InnerW; preview.style.height = 92f;
            preview.style.backgroundColor = new StyleColor(AccentBg);
            // [144차 P7-SLP #24] 기상 미리보기 테두리 — strokes #58A6FF/1.0@0.251 sw1.0(227:283)
            preview.style.borderTopWidth = preview.style.borderBottomWidth = 1f;
            preview.style.borderLeftWidth = preview.style.borderRightWidth = 1f;
            preview.style.borderTopColor = preview.style.borderBottomColor = new StyleColor(AccentStroke);
            preview.style.borderLeftColor = preview.style.borderRightColor = new StyleColor(AccentStroke);
            preview.style.borderTopLeftRadius = preview.style.borderTopRightRadius = 10f;
            preview.style.borderBottomLeftRadius = preview.style.borderBottomRightRadius = 10f;
            preview.pickingMode = PickingMode.Ignore;
            block.Add(preview);

            var nowCap = MakeLabel("현재 시간", 12f, FontStyle.Normal, TextSub);   // 시간항목 12/400
            nowCap.style.position = Position.Absolute;
            nowCap.style.left = 20f; nowCap.style.top = 14f;
            preview.Add(nowCap);

            _nowTimeLabel = MakeLabel("-", 32f, FontStyle.Normal, TextMain);       // 현재 HH:MM 32/500 — Geist Mono는 패밀리 공용계약 보류, 굵기 500→Normal 근사(기록)
            _nowTimeLabel.style.position = Position.Absolute;
            _nowTimeLabel.style.left = 20f; _nowTimeLabel.style.top = 34f;         // [144차 P7-SLP #26] 피그마 (44,247)→미리보기 로컬 (20,34) — yΔ4 수리
            preview.Add(_nowTimeLabel);

            var midCol = new VisualElement();                                      // 중앙 "8시간 수면" + 화살표
            midCol.name = "WakePreviewMiddle";
            midCol.style.position = Position.Absolute;
            midCol.style.left = 261f; midCol.style.top = 26f;                      // [144차 P7-SLP #27] 피그마 선택 시간 (285,239)→로컬 (261,26) — xΔ15 수리
            midCol.style.width = 102f; midCol.style.height = 52f;                  // 폭 132→102 — 라벨 박스 좌측 261 정렬 + 중앙 312 유지
            midCol.pickingMode = PickingMode.Ignore;
            _sleepDurationLabel = MakeLabel("8시간 수면", 12f, FontStyle.Normal, Accent);   // 12/500 #58A6FF
            _sleepDurationLabel.style.position = Position.Absolute;
            _sleepDurationLabel.style.left = 0f; _sleepDurationLabel.style.top = 0f;
            _sleepDurationLabel.style.width = 102f;                                // [144차 P7-SLP #27] midCol 폭 동기화 — 중앙 312 유지
            _sleepDurationLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            midCol.Add(_sleepDurationLabel);
            var arrow = BuildArrowGlyph();
            arrow.style.position = Position.Absolute;
            arrow.style.left = 43f; arrow.style.top = 18f;                         // [144차 P7-SLP #27] midCol 이동 보상 — 절대 (304,44) 불변(#28 현행 유지)
            midCol.Add(arrow);
            preview.Add(midCol);

            _wakeDayLabel = MakeLabel("-", 12f, FontStyle.Normal, TextSub);        // "기상 예정 · N일차"
            _wakeDayLabel.style.position = Position.Absolute;
            _wakeDayLabel.style.right = 20f; _wakeDayLabel.style.top = 14f;
            preview.Add(_wakeDayLabel);

            _wakeTimeLabel = MakeLabel("-", 32f, FontStyle.Normal, Accent);        // [144차 P7-SLP #29] 기상 HH:MM 32/500 — 피그마 #58A6FF(227:292, 강조색)
            _wakeTimeLabel.style.position = Position.Absolute;
            _wakeTimeLabel.style.right = 20f; _wakeTimeLabel.style.top = 34f;      // [144차 P7-SLP #29] yΔ4 수리 — 피그마 (484,247)→로컬 y34
            preview.Add(_wakeTimeLabel);

            // ── 수면 시간 선택 (24,317) 576×58 — 3버튼 188×58 radius 7 ──
            var choiceRow = new VisualElement();
            choiceRow.name = "SleepTimeChoice";
            choiceRow.style.position = Position.Absolute;
            choiceRow.style.left = 0f; choiceRow.style.top = TimeChoiceY - SleepTimeY;   // 135
            choiceRow.style.width = InnerW; choiceRow.style.height = 58f;
            block.Add(choiceRow);

            _timeButtons = new Button[3];
            _timeBtnNames = new Label[3];
            _timeBtnSubs = new Label[3];
            var options = new[] { (2f, "2시간", "짧은 휴식"), (4f, "4시간", "가벼운 수면"), (8f, "8시간", "충분한 수면") };
            for (int i = 0; i < 3; i++)
            {
                float hours = options[i].Item1;
                var btn = new Button(() => SelectHours(hours)) { text = "" };      // 188×58 radius 7
                btn.name = $"SleepTimeButton_{i}";
                btn.style.position = Position.Absolute;
                btn.style.left = i * (188f + 6f);                                   // 564+간격 6×2 = 576
                btn.style.top = 0f;
                btn.style.width = 188f; btn.style.height = 58f;
                btn.style.paddingLeft = btn.style.paddingRight = 0f;
                btn.style.paddingTop = btn.style.paddingBottom = 0f;
                btn.style.borderTopLeftRadius = btn.style.borderTopRightRadius = 7f;
                btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 7f;
                var nameLbl = MakeLabel(options[i].Item2, 14f, FontStyle.Bold, TextMain);   // 텍스트 14/700
                nameLbl.style.position = Position.Absolute;
                nameLbl.style.left = 0f; nameLbl.style.top = 13f; nameLbl.style.width = 188f;
                nameLbl.style.unityTextAlign = TextAnchor.MiddleCenter;
                btn.Add(nameLbl);
                var subLbl = MakeLabel(options[i].Item3, 11f, FontStyle.Normal, TextSub);   // 11/400
                subLbl.style.position = Position.Absolute;
                subLbl.style.left = 0f; subLbl.style.top = 33f; subLbl.style.width = 188f;
                subLbl.style.unityTextAlign = TextAnchor.MiddleCenter;
                btn.Add(subLbl);
                choiceRow.Add(btn);
                _timeButtons[i] = btn;
                _timeBtnNames[i] = nameLbl;
                _timeBtnSubs[i] = subLbl;
            }
            RefreshTimeButtons();
        }

        private void SelectHours(float hours)
        {
            if (_busy) return;
            _selectedHours = hours;
            RefreshTimeButtons();
            RefreshPreview();
            RefreshRecovery();
            _recoveryRangeLabel.text = $"{hours:0}시간 수면 기준";
            Debug.Log($"[SleepPanelUTK] 수면 시간 선택 — {hours:0}시간");
        }

        // ═════════════════════════ ④ 회복 예상 (24,395) 576×149 ═════════════════════════
        private void BuildRecoveryBlock()
        {
            var block = Block(TitleXY, RecoveryY, InnerW, 149f);

            var header = MakeLabel("회복 예상", 16f, FontStyle.Bold, TextMain);   // 16/700
            header.style.position = Position.Absolute;
            header.style.left = 0f; header.style.top = 0f;
            block.Add(header);

            _recoveryRangeLabel = MakeLabel("8시간 수면 기준", 12f, FontStyle.Normal, TextSub);   // 우측 12/400
            _recoveryRangeLabel.style.position = Position.Absolute;
            _recoveryRangeLabel.style.right = 0f; _recoveryRangeLabel.style.top = 4f;
            block.Add(_recoveryRangeLabel);

            // 회복 항목 2개 (283×92 #FFFFFF/2% radius 10) — 체력(0,31) / 기력(293,31)
            // [144차 P7-SLP #1] 항목명 라벨 신설 — "체력"/"기력" 14/400 #F0F6FC (227:311/322 카드 로컬 (38,14))
            var hpCard = BuildRecoveryCard(0f, 31f, BuildHeartGlyph(), "체력", out _hpDeltaLabel, out _hpCurLabel, out _hpEstLabel, out _hpFill);
            block.Add(hpCard);

            var stCard = BuildRecoveryCard(293f, 31f, BuildZapGlyph(), "기력", out _stDeltaLabel, out _stCurLabel, out _stEstLabel, out _stFill);
            block.Add(stCard);

            // 기력 실측 부재 폴백 — "-" (위조 금지 규약)
            _stMissing = MakeLabel("-", 18f, FontStyle.Normal, TextSub);
            _stMissing.style.position = Position.Absolute;
            _stMissing.style.left = 293f; _stMissing.style.top = 31f; _stMissing.style.width = 283f; _stMissing.style.height = 92f;
            _stMissing.style.unityTextAlign = TextAnchor.MiddleCenter;
            _stMissing.style.display = DisplayStyle.None;
            stCard.parent.Add(_stMissing);
            _stRow = stCard;
        }

        private VisualElement BuildRecoveryCard(float x, float y, VisualElement icon, string itemName,
            out Label delta, out Label cur, out Label est, out VisualElement fill)
        {
            var card = Card(x, y, 283f, 92f, 10f);   // 283×92 #FFFFFF/2% radius 10

            icon.style.position = Position.Absolute;
            icon.style.left = 16f; icon.style.top = 12f;
            card.Add(icon);

            // [144차 P7-SLP #1] 항목명 라벨 — "체력"/"기력" fs14 400 #F0F6FC 카드 로컬 (38,14) — 227:311/322
            var name = MakeLabel(itemName, 14f, FontStyle.Normal, TextMain);   // 14/400 #F0F6FC
            name.style.position = Position.Absolute;
            name.style.left = 38f; name.style.top = 14f;
            card.Add(name);

            delta = MakeLabel("+", 12f, FontStyle.Normal, Accent);   // "+36" 12/400 #58A6FF — 실측 계산 (227:312 카드 로컬 (247,15))
            delta.style.position = Position.Absolute;
            delta.style.left = 247f; delta.style.top = 15f;   // [144차 P7-SLP #1] (38,11)→(247,15) — 항목명 우측 정렬 피그마 원좌표
            card.Add(delta);

            cur = MakeLabel("-", 18f, FontStyle.Normal, TextSub);    // 현재 18/400
            cur.style.position = Position.Absolute;
            cur.style.left = 14f; cur.style.top = 41f;               // [144차 P7-SLP #40] 피그마 (38,467)→카드 로컬 (14,41) — yΔ7 수리
            card.Add(cur);

            var arrow = BuildArrowGlyph();
            arrow.style.position = Position.Absolute;
            arrow.style.left = 54f; arrow.style.top = 42f;
            card.Add(arrow);

            est = MakeLabel("-", 18f, FontStyle.Bold, TextMain);     // 예상 18/700
            est.style.position = Position.Absolute;
            est.style.left = 66f; est.style.top = 41f;               // [144차 P7-SLP #41] 피그마 (90,467)→카드 로컬 (66,41) — yΔ7 수리
            card.Add(est);

            var gauge = new VisualElement();                         // 게이지 255×4 #58A6FF/8% bg radius 2
            gauge.name = "RecoveryGauge";
            gauge.style.position = Position.Absolute;
            gauge.style.left = 14f; gauge.style.top = 74f;           // [144차 P7-SLP #42] 피그마 (38,500)→카드 로컬 (14,74) — yΔ6 수리
            gauge.style.width = 255f; gauge.style.height = 4f;
            gauge.style.backgroundColor = new StyleColor(AccentBg);
            gauge.style.borderTopLeftRadius = gauge.style.borderTopRightRadius = 2f;
            gauge.style.borderBottomLeftRadius = gauge.style.borderBottomRightRadius = 2f;
            gauge.pickingMode = PickingMode.Ignore;
            card.Add(gauge);

            fill = new VisualElement();                              // fill #58A6FF radius 2
            fill.name = "RecoveryGaugeFill";
            fill.style.position = Position.Absolute;
            fill.style.left = 0f; fill.style.top = 0f;
            fill.style.width = 0f; fill.style.height = 4f;
            fill.style.backgroundColor = new StyleColor(Accent);
            fill.style.borderTopLeftRadius = fill.style.borderTopRightRadius = 2f;
            fill.style.borderBottomLeftRadius = fill.style.borderBottomRightRadius = 2f;
            fill.pickingMode = PickingMode.Ignore;
            gauge.Add(fill);

            return card;
        }

        // ═════════════════════════ ⑤ 수면 안내 (24,530) ═════════════════════════
        private void BuildSleepNote()
        {
            var note = Block(TitleXY, SleepNoteY, InnerW, 18f);

            var info = BuildInfoGlyph();
            info.style.position = Position.Absolute;
            info.style.left = 0f; info.style.top = 2f;
            note.Add(info);

            var text = MakeLabel("수면 중에는 게임 시간이 흐르며, 부대원의 임무는 계속됩니다.", 12f, FontStyle.Normal, TextSub);   // 12/400 — [144차 P7-SLP #45] 말미 절단 수리(227:332 전문)
            text.style.position = Position.Absolute;
            text.style.left = 22f; text.style.top = 0f;
            note.Add(text);
        }

        // ═════════════════════════ ⑥ 세이브 (24,564) 576×403 ═════════════════════════
        private void BuildSaveSection()
        {
            var block = Block(TitleXY, SaveY, InnerW, 403f);

            // 항목 제목 (24,584)→로컬 (0,20) — save 아이콘 18×18 + "세이브" 16/700 + 우측 설명
            var saveIcon = BuildSaveGlyph();
            saveIcon.style.position = Position.Absolute;
            saveIcon.style.left = 0f; saveIcon.style.top = 22f;
            block.Add(saveIcon);

            var saveTitle = MakeLabel("세이브", 16f, FontStyle.Bold, TextMain);
            saveTitle.style.position = Position.Absolute;
            saveTitle.style.left = 26f; saveTitle.style.top = 18f;
            block.Add(saveTitle);

            var saveDesc = MakeLabel("현재 진행 상황을 안전하게 보관", 12f, FontStyle.Normal, TextSub);   // 우측 12/400
            saveDesc.style.position = Position.Absolute;
            saveDesc.style.right = 0f; saveDesc.style.top = 22f;
            block.Add(saveDesc);

            // ── 자동 저장 설정 (24,615)→로컬 (0,51) 576×63 #FFFFFF/2% radius 10 ──
            var autoRow = Card(0f, 51f, InnerW, 63f, 10f);   // [144차 P7-SLP #49] 테두리 선충족 — Card() 공용(StrokeSub sw1.0, 선행 A 수리) 중복 수리 스킵
            var autoTitle = MakeLabel("수면 전 자동 저장", 14f, FontStyle.Normal, TextMain);   // 14/500
            autoTitle.style.position = Position.Absolute;
            autoTitle.style.left = 16f; autoTitle.style.top = 12f;
            autoRow.Add(autoTitle);

            var autoDesc = MakeLabel("수면을 시작하면 자동 저장 슬롯에 기록합니다.", 12f, FontStyle.Normal, TextSub);   // 12/400 — [144차 P7-SLP #51] 문구 수리(227:341)
            autoDesc.style.position = Position.Absolute;
            autoDesc.style.left = 16f; autoDesc.style.top = 33f;
            autoRow.Add(autoDesc);

            // 스위치 (548,635.5)→row 로컬 (524,20.5) 38×22 radius 11 — ON #58A6FF 손잡이 우측 / OFF #FFFFFF/2% 손잡이 좌측
            _switchTrack = new VisualElement();
            _switchTrack.name = "SleepAutoSaveSwitch";
            _switchTrack.style.position = Position.Absolute;
            _switchTrack.style.left = 524f; _switchTrack.style.top = 20.5f;
            _switchTrack.style.width = SwitchW; _switchTrack.style.height = SwitchH;
            _switchTrack.style.borderTopLeftRadius = _switchTrack.style.borderTopRightRadius = 11f;
            _switchTrack.style.borderBottomLeftRadius = _switchTrack.style.borderBottomRightRadius = 11f;
            var switchBtn = new Button(() => { if (!_busy) SleepAutoSaveToggle.Toggle(); }) { text = "" };
            switchBtn.style.position = Position.Absolute;
            switchBtn.style.left = 524f; switchBtn.style.top = 20.5f;
            switchBtn.style.width = SwitchW; switchBtn.style.height = SwitchH;
            switchBtn.style.backgroundColor = StyleKeyword.Null;
            switchBtn.style.borderTopWidth = switchBtn.style.borderBottomWidth = 0f;
            switchBtn.style.borderLeftWidth = switchBtn.style.borderRightWidth = 0f;
            switchBtn.style.paddingLeft = switchBtn.style.paddingRight = 0f;
            switchBtn.style.paddingTop = switchBtn.style.paddingBottom = 0f;
            _switchKnob = new VisualElement();
            _switchKnob.name = "SleepAutoSaveKnob";
            _switchKnob.style.position = Position.Absolute;
            _switchKnob.style.width = KnobSize; _switchKnob.style.height = KnobSize;
            _switchKnob.style.top = 3.5f;   // [144차 P7-SLP #54] 손잡이 16×16 — left는 ON/OFF 전환(RefreshSwitch)
            _switchKnob.style.borderTopLeftRadius = _switchKnob.style.borderTopRightRadius = 8f;
            _switchKnob.style.borderBottomLeftRadius = _switchKnob.style.borderBottomRightRadius = 8f;
            _switchKnob.style.backgroundColor = new StyleColor(BtnTextDark);   // [144차 P7-SLP #54] 손잡이 #0B0E14 16×16(227:343-344)
            _switchKnob.pickingMode = PickingMode.Ignore;
            _switchTrack.Add(_switchKnob);
            autoRow.Add(_switchTrack);
            autoRow.Add(switchBtn);

            _switchLabel = MakeLabel("켬", 12f, FontStyle.Normal, Accent);   // "켬"/"끔" 12/400 — [144차 P7-SLP #52] 피그마 (500,24) 좌측정렬(227:342)
            _switchLabel.style.position = Position.Absolute;
            _switchLabel.style.left = 500f; _switchLabel.style.top = 24f;
            autoRow.Add(_switchLabel);
            block.Add(autoRow);

            // ── 수동 저장 목록 (24,690)→로컬 (0,126) ──
            var slotHeader = MakeLabel("수동 저장 슬롯", 14f, FontStyle.Bold, TextMain);   // 14/700
            slotHeader.style.position = Position.Absolute;
            slotHeader.style.left = 0f; slotHeader.style.top = 126f;
            block.Add(slotHeader);

            _slotsCountLabel = MakeLabel("5개 슬롯", 12f, FontStyle.Normal, TextSub);      // 우측 12/400
            _slotsCountLabel.style.position = Position.Absolute;
            _slotsCountLabel.style.right = 0f; _slotsCountLabel.style.top = 129f;
            block.Add(_slotsCountLabel);

            // 목록 영역 (24,719)→로컬 (0,155) 556×248 스크롤 — 슬롯 행 556×81 간격 10 (5행 전체 노출은 스크롤)
            var list = new ScrollView(ScrollViewMode.Vertical);
            list.name = "ManualSaveSlotList";
            list.style.position = Position.Absolute;
            list.style.left = 0f; list.style.top = 155f;
            list.style.width = 556f; list.style.height = 248f;
            list.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            block.Add(list);

            // [144차 P7-SLP #57] 스크롤바 — track 8×248 #FFFFFF/2% + stroke(227:459) / thumb 8×96 #58A6FF r999(227:460)
            var scroller = list.verticalScroller;
            scroller.style.width = 8f;
            var tracker = scroller.Q(className: "unity-base-slider__tracker");
            if (tracker != null)
            {
                tracker.style.backgroundColor = new StyleColor(CardBg);
                tracker.style.borderTopWidth = tracker.style.borderBottomWidth = 1f;
                tracker.style.borderLeftWidth = tracker.style.borderRightWidth = 1f;
                tracker.style.borderTopColor = tracker.style.borderBottomColor = new StyleColor(StrokeSub);
                tracker.style.borderLeftColor = tracker.style.borderRightColor = new StyleColor(StrokeSub);
            }
            var dragger = scroller.Q(className: "unity-base-slider__dragger");
            if (dragger != null)
            {
                dragger.style.width = 8f; dragger.style.height = 96f;
                dragger.style.backgroundColor = new StyleColor(Accent);
                dragger.style.borderTopLeftRadius = dragger.style.borderTopRightRadius = 999f;
                dragger.style.borderBottomLeftRadius = dragger.style.borderBottomRightRadius = 999f;
            }
            var lowBtn = scroller.Q(className: "unity-scroller__lowButton");
            var highBtn = scroller.Q(className: "unity-scroller__highButton");
            if (lowBtn != null) lowBtn.style.display = DisplayStyle.None;
            if (highBtn != null) highBtn.style.display = DisplayStyle.None;

            _slotTitleLabels = new Label[5];
            _slotStateLabels = new Label[5];       // [144차 P7-SLP #2] 슬롯 3행 구성 — 타이틀/상태/플레이 시간
            _slotPlayTimeLabels = new Label[5];
            for (int i = 0; i < 5; i++)
            {
                var row = BuildSlotRow(i);
                row.style.marginBottom = 10f;   // 간격 10
                list.contentContainer.Add(row);
            }
        }

        /// <summary>슬롯 행 556×81 #FFFFFF/2% radius 10 — "지금 저장" 버튼은 인덱스 i 고정(오버라이트 규약).</summary>
        private VisualElement BuildSlotRow(int i)
        {
            var row = new VisualElement();
            row.name = $"SaveSlotRow_{i}";
            row.style.width = 556f; row.style.height = 81f;
            row.style.backgroundColor = new StyleColor(CardBg);
            row.style.borderTopLeftRadius = row.style.borderTopRightRadius = 10f;
            row.style.borderBottomLeftRadius = row.style.borderBottomRightRadius = 10f;
            row.style.borderTopWidth = row.style.borderBottomWidth = 1f;   // [144차 P7-SLP #58] 슬롯 행 테두리 #30363D@0.502 sw1.0(227:345)
            row.style.borderLeftWidth = row.style.borderRightWidth = 1f;
            row.style.borderTopColor = row.style.borderBottomColor = new StyleColor(StrokeSub);
            row.style.borderLeftColor = row.style.borderRightColor = new StyleColor(StrokeSub);
            row.pickingMode = PickingMode.Position;

            var icon = new VisualElement();             // 아이콘 40×44 #1C232C radius 5
            icon.name = "SaveSlotIcon";
            icon.style.position = Position.Absolute;
            icon.style.left = 16f; icon.style.top = 18f;
            icon.style.width = 40f; icon.style.height = 44f;
            icon.style.backgroundColor = new StyleColor(IconBg);
            icon.style.borderTopLeftRadius = icon.style.borderTopRightRadius = 5f;
            icon.style.borderBottomLeftRadius = icon.style.borderBottomRightRadius = 5f;
            icon.pickingMode = PickingMode.Ignore;
            row.Add(icon);
            var fileGlyph = BuildFileCheckGlyph();   // [144차 P7-SLP #3] file-check-2 글리프 20×20 #8B949E — PNG 부재(UI/icons 실측), 절차 근사
            fileGlyph.style.position = Position.Absolute;
            fileGlyph.style.left = 14f;              // xΔ2 보정 (피그마 16→14)
            fileGlyph.style.top = 12f;               // 40×44 호스트 세로 중앙
            icon.Add(fileGlyph);

            var title = MakeLabel($"수동 저장 {i + 1:00}", 14f, FontStyle.Bold, TextMain);   // [144차 P7-SLP #60] "수동 저장 01" 명명(227:350, 실측 슬롯 번호 N) — 슬롯 정보 381×53
            title.style.position = Position.Absolute;
            title.style.left = 52f; title.style.top = 14f; title.style.width = 381f;   // [144차 P7-SLP #60] xΔ18 수리
            row.Add(title);

            var state = MakeLabel("저장 기록 없음", 11f, FontStyle.Normal, TextSub);   // [144차 P7-SLP #2] 행2 — "최근 저장" 상태 11/400 (일차/타임스탬프 실측 통합)
            state.style.position = Position.Absolute;
            state.style.left = 70f; state.style.top = 35f; state.style.width = 381f;
            state.style.whiteSpace = WhiteSpace.Normal;
            row.Add(state);

            var playTime = MakeLabel("플레이 시간 -", 11f, FontStyle.Normal, TextSub);   // [144차 P7-SLP #2] 행3 — 플레이 시간 11/400 (Geist Mono 근사; SaveData 실측 부재 → "-" 위조 금지 규약)
            playTime.style.position = Position.Absolute;
            playTime.style.left = 70f; playTime.style.top = 50f; playTime.style.width = 381f;
            row.Add(playTime);

            var saveBtn = new Button(() => SaveSlot(i)) { text = "" };   // "지금 저장" 83×36 #58A6FF/8% radius 7
            saveBtn.name = $"SaveNowButton_{i}";
            saveBtn.style.position = Position.Absolute;
            saveBtn.style.left = 459f; saveBtn.style.top = 22.5f;        // (483,741.5) 행 로컬
            saveBtn.style.width = 83f; saveBtn.style.height = 36f;
            saveBtn.style.backgroundColor = new StyleColor(AccentBg);
            saveBtn.style.borderTopLeftRadius = saveBtn.style.borderTopRightRadius = 7f;
            saveBtn.style.borderBottomLeftRadius = saveBtn.style.borderBottomRightRadius = 7f;
            saveBtn.style.borderTopWidth = saveBtn.style.borderBottomWidth = 1f;   // [144차 P7-SLP #63] 지금 저장 테두리 #58A6FF@0.251 sw1.0(227:354)
            saveBtn.style.borderLeftWidth = saveBtn.style.borderRightWidth = 1f;
            saveBtn.style.borderTopColor = saveBtn.style.borderBottomColor = new StyleColor(AccentStroke);
            saveBtn.style.borderLeftColor = saveBtn.style.borderRightColor = new StyleColor(AccentStroke);
            saveBtn.style.paddingLeft = saveBtn.style.paddingRight = 0f;
            saveBtn.style.paddingTop = saveBtn.style.paddingBottom = 0f;
            var saveBtnText = MakeLabel("지금 저장", 14f, FontStyle.Bold, Accent);   // [144차 P7-SLP #64] 텍스트 14/700 #58A6FF(227:355)
            saveBtnText.style.position = Position.Absolute;
            saveBtnText.style.left = 0f; saveBtnText.style.top = 0f;
            saveBtnText.style.width = 83f; saveBtnText.style.height = 36f;
            saveBtnText.style.unityTextAlign = TextAnchor.MiddleCenter;
            saveBtn.Add(saveBtnText);
            row.Add(saveBtn);

            _slotTitleLabels[i] = title;
            _slotStateLabels[i] = state;             // [144차 P7-SLP #2] 3행 배선
            _slotPlayTimeLabels[i] = playTime;
            return row;
        }

        private void SaveSlot(int i)
        {
            if (SaveManager.Instance == null)
            {
                UTKToastService.Show("SaveManager 없음 — 저장 불가", 2f);
                return;
            }
            SaveManager.Instance.Save(i);   // [73차] SaveManager.Save(slotIndex)(103행) — 버튼마다 인덱스 고정(0~4) 오버라이트
            RefreshSlots();
            UTKToastService.Show($"슬롯 {i + 1} 저장 완료", 2f);
            Debug.Log($"[SleepPanelUTK] 수동 저장 — 슬롯 {i}");
        }

        // ═════════════════════════ ⑦ 수면 실행 (24,987) 576×90 ═════════════════════════
        private void BuildSleepActionRow()
        {
            var block = Block(TitleXY, SleepActionY, InnerW, 90f);

            var cancelBtn = new Button(Hide) { text = "" };              // 취소 (24,1005)→(0,18) 124×46 #FFFFFF/2% radius 7
            cancelBtn.name = "SleepCancelButton";
            cancelBtn.style.position = Position.Absolute;
            cancelBtn.style.left = 0f; cancelBtn.style.top = 18f;
            cancelBtn.style.width = 124f; cancelBtn.style.height = 46f;
            cancelBtn.style.backgroundColor = new StyleColor(CardBg);
            cancelBtn.style.borderTopLeftRadius = cancelBtn.style.borderTopRightRadius = 7f;
            cancelBtn.style.borderBottomLeftRadius = cancelBtn.style.borderBottomRightRadius = 7f;
            cancelBtn.style.borderTopWidth = cancelBtn.style.borderBottomWidth = 1f;   // [144차 P7-SLP #65] 취소 테두리 #30363D@0.502 sw1.0(227:358)
            cancelBtn.style.borderLeftWidth = cancelBtn.style.borderRightWidth = 1f;
            cancelBtn.style.borderTopColor = cancelBtn.style.borderBottomColor = new StyleColor(StrokeSub);
            cancelBtn.style.borderLeftColor = cancelBtn.style.borderRightColor = new StyleColor(StrokeSub);
            cancelBtn.style.paddingLeft = cancelBtn.style.paddingRight = 0f;
            cancelBtn.style.paddingTop = cancelBtn.style.paddingBottom = 0f;
            var cancelText = MakeLabel("취소", 14f, FontStyle.Bold, TextMain);   // "취소" 14/700
            cancelText.style.position = Position.Absolute;
            cancelText.style.left = 0f; cancelText.style.top = 0f;
            cancelText.style.width = 124f; cancelText.style.height = 46f;
            cancelText.style.unityTextAlign = TextAnchor.MiddleCenter;
            cancelBtn.Add(cancelText);
            block.Add(cancelBtn);

            var startBtn = new Button(StartSleep) { text = "" };         // 수면 시작 (160,1005)→(136,18) 440×46 #58A6FF radius 7
            startBtn.name = "SleepStartButton";
            startBtn.style.position = Position.Absolute;
            startBtn.style.left = 136f; startBtn.style.top = 18f;
            startBtn.style.width = 440f; startBtn.style.height = 46f;
            startBtn.style.backgroundColor = new StyleColor(Accent);
            startBtn.style.borderTopLeftRadius = startBtn.style.borderTopRightRadius = 7f;
            startBtn.style.borderBottomLeftRadius = startBtn.style.borderBottomRightRadius = 7f;
            startBtn.style.paddingLeft = startBtn.style.paddingRight = 0f;
            startBtn.style.paddingTop = startBtn.style.paddingBottom = 0f;
            var startMoon = BuildMoonIcon(18f, BtnTextDark, Accent);     // [144차 P7-SLP #69] moon 18×18 #0B0E14 sw1.8(227:401-402) — 컷아웃은 버튼 배경색(Accent)으로
            startMoon.style.position = Position.Absolute;
            startMoon.style.left = 157f; startMoon.style.top = 14f;      // [144차 P7-SLP #69] xΔ9 수리
            startBtn.Add(startMoon);
            var startText = MakeLabel("저장 후 수면 시작", 14f, FontStyle.Bold, BtnTextDark);   // 14/700 #0B0E14
            startText.style.position = Position.Absolute;
            startText.style.left = 0f; startText.style.top = 0f;
            startText.style.width = 440f; startText.style.height = 46f;
            startText.style.unityTextAlign = TextAnchor.MiddleCenter;
            startBtn.Add(startText);
            block.Add(startBtn);

            var shortcut = MakeLabel("Enter  확인     ·     Esc  닫기", 12f, FontStyle.Normal, TextSub);   // 단축키 안내 12/400
            shortcut.style.position = Position.Absolute;
            shortcut.style.left = 0f; shortcut.style.top = 76f;
            block.Add(shortcut);
        }

        // ═════════════════════════ 데이터 실측/갱신 ═════════════════════════

        private void RefreshData()
        {
            // 휴식 장소 — Bed.BedName 실측(위조 금지: 없는 값은 "-", 없는 장소명도 벤치마크 금지)
            string bedName = _bed != null ? _bed.BedName : null;
            _placeNameLabel.text = string.IsNullOrEmpty(bedName) ? "전초기지 · 개인 숙소" : bedName;
            _placeDescLabel.text = _bed != null
                ? "부활 지점(스폰핀)으로 사용되는 안전한 수면 장소입니다"
                : "-";

            var tm = TimeManager.Instance;
            _nowDateLabel.text = tm != null
                ? $"{tm.CurrentDay:N0}일차 · {(tm.IsNight ? "밤" : "낮")}"   // "N일차 · 밤" 실측
                : "-";
            RefreshPreview();
            RefreshRecovery();
            RefreshSwitch();
            RefreshSlots();
        }

        /// <summary>기상 미리보기 갱신 — 현재 HH:MM(32/500) + "N시간 수면" + 기상 HH:MM/일차(24h wrap + 일차 가산).</summary>
        private void RefreshPreview()
        {
            var tm = TimeManager.Instance;
            if (tm == null)
            {
                _nowTimeLabel.text = "-";
                _wakeTimeLabel.text = "-";
                _wakeDayLabel.text = "-";
                _sleepDurationLabel.text = $"{_selectedHours:0}시간 수면";
                return;
            }
            _nowTimeLabel.text = tm.GetFormattedTime();   // 현재 HH:MM — TimeManager 실측
            ComputeWakeTime(tm.Hour, tm.Minute, _selectedHours, out int wakeHour, out int wakeMinute, out int dayDelta);
            _wakeTimeLabel.text = $"{wakeHour:D2}:{wakeMinute:D2}";
            _wakeDayLabel.text = $"기상 예정 · {(tm.CurrentDay + dayDelta):N0}일차";
            _sleepDurationLabel.text = $"{_selectedHours:0}시간 수면";
        }

        private void RefreshRecovery()
        {
            // 체력 — PlayerHealth 실측(Heal API 282행과 동일 데이터)
            var health = PlayerHealth.Instance;
            if (health != null && health.MaxHP > 0f)
            {
                float ratio = ComputeHealRatio(health.MaxHP, health.CurrentHP, _selectedHours);
                _hpDeltaLabel.text = $"+{Mathf.RoundToInt(ratio * 100f)}";   // "+36" — 8h 기준 36%(누락분 clamp)
                float cur = health.CurrentHP;
                float est = Mathf.Min(health.MaxHP, cur + ratio * health.MaxHP);
                _hpCurLabel.text = $"{Mathf.RoundToInt(cur):N0}";
                _hpEstLabel.text = $"{Mathf.RoundToInt(est):N0}";
                _hpFill.style.width = 255f * Mathf.Clamp01(est / health.MaxHP);
            }
            else
            {
                _hpDeltaLabel.text = "-";
                _hpCurLabel.text = "-";
                _hpEstLabel.text = "-";
                _hpFill.style.width = 0f;
            }

            // 기력 — Systems.PlayerMovement 실측(1670~1672행). 실내 씬 부재 시 "-" 폴백.
            var mv = FindPlayerMovement();
            bool hasStamina = mv != null && mv.MaxStamina > 0f;
            _stRow.style.display = hasStamina ? DisplayStyle.Flex : DisplayStyle.None;
            _stMissing.style.display = hasStamina ? DisplayStyle.None : DisplayStyle.Flex;
            if (hasStamina)
            {
                float ratio = ComputeStaminaRatio(mv.MaxStamina, mv.Stamina, _selectedHours);
                _stDeltaLabel.text = $"+{Mathf.RoundToInt(ratio * 100f)}";   // "+72" — 8h 기준 72%(누락분 clamp)
                float cur = mv.Stamina;
                float est = Mathf.Min(mv.MaxStamina, cur + ratio * mv.MaxStamina);
                _stCurLabel.text = $"{Mathf.RoundToInt(cur):N0}";
                _stEstLabel.text = $"{Mathf.RoundToInt(est):N0}";
                _stFill.style.width = 255f * Mathf.Clamp01(est / mv.MaxStamina);
            }
        }

        private void RefreshSwitch()
        {
            bool on = SleepAutoSaveToggle.Enabled;                       // SleepAutoSaveToggle 시스템 실측
            _switchTrack.style.backgroundColor = new StyleColor(on ? Accent : CardBg);   // ON #58A6FF / OFF #FFFFFF/2%
            _switchKnob.style.left = on ? KnobOnX : KnobOffX;            // 손잡이 ON 우측(19) / OFF 좌측(3)
            _switchLabel.text = on ? "켬" : "끔";
            _switchLabel.style.color = new StyleColor(on ? Accent : TextSub);   // "켬" #58A6FF / "끔" #8B949E
        }

        private void RefreshSlots()
        {
            if (SaveManager.Instance != null)
                _slotsCountLabel.text = $"{SaveManager.Instance.SlotCount}개 슬롯";
            for (int i = 0; i < 5; i++)
            {
                bool has = SaveManager.Instance != null && SaveManager.Instance.HasSave(i);   // HasSave(253행) 실측
                // [144차 P7-SLP #2] 슬롯 3행 갱신 — 행2 상태(최근 저장/일차/타임스탬프 실측), 행3 플레이 시간
                if (!has)
                {
                    _slotStateLabels[i].text = "저장 기록 없음";
                    _slotPlayTimeLabels[i].text = "플레이 시간 -";
                    continue;
                }
                var data = SaveManager.Instance.GetSlotInfo(i);   // 타임스탬프/일차 실측 조회
                if (data != null && data.time != null)
                    _slotStateLabels[i].text = $"{data.time.day:N0}일차 · 최근 저장 {data.timestamp}";
                else if (data != null)
                    _slotStateLabels[i].text = $"최근 저장 {data.timestamp}";
                else
                    _slotStateLabels[i].text = "최근 저장 -";   // 파일 있으나 파싱 실패 — 실측 폴백
                _slotPlayTimeLabels[i].text = "플레이 시간 -";   // SaveData 실측 부재 — 위조 금지 규약("-")
            }
        }

        private void RefreshTimeButtons()
        {
            for (int i = 0; i < 3; i++)
            {
                float h = i == 0 ? 2f : i == 1 ? 4f : 8f;
                bool active = Mathf.Approximately(h, _selectedHours);
                // 활성 #58A6FF 텍스트 #0B0E14 / 비활성 #FFFFFF/2% 텍스트 #F0F6FC+#8B949E
                _timeButtons[i].style.backgroundColor = new StyleColor(active ? Accent : CardBg);
                // [144차 P7-SLP #32/#33] 칩 테두리 — 비활성 strokes #30363D/1.0@0.502(227:294/297) / 활성 strokes #58A6FF(227:300), sw1.0
                var chipStroke = active ? Accent : StrokeSub;
                _timeButtons[i].style.borderTopWidth = _timeButtons[i].style.borderBottomWidth = 1f;
                _timeButtons[i].style.borderLeftWidth = _timeButtons[i].style.borderRightWidth = 1f;
                _timeButtons[i].style.borderTopColor = _timeButtons[i].style.borderBottomColor = new StyleColor(chipStroke);
                _timeButtons[i].style.borderLeftColor = _timeButtons[i].style.borderRightColor = new StyleColor(chipStroke);
                _timeBtnNames[i].style.color = new StyleColor(active ? BtnTextDark : TextMain);
                _timeBtnSubs[i].style.color = new StyleColor(active ? BtnTextDark : TextSub);
            }
        }

        // ═════════════════════════ 수면 실행 ═════════════════════════

        private void StartSleep()
        {
            if (_busy) return;   // 수면 중 이중 클릭 방지
            if (TimeManager.Instance == null)
            {
                UTKToastService.Show("TimeManager 없음 — 수면 불가", 2f);
                return;
            }

            _busy = true;
            SetActionsEnabled(false);   // 트랩 ③ — 수면 중 버튼 인터랙티브 비활성

            // [73차] 자동 저장 — 스위치 ON이면 수면 시작 직전 SaveManager.AutoSave()(토큰 "sleep_autosave")
            if (SleepAutoSaveToggle.Enabled)
            {
                if (SaveManager.Instance != null)
                    SaveManager.Instance.AutoSave();
                else
                    Debug.LogWarning("[SleepPanelUTK] SaveManager 없음 — 자동 저장 스킵");
            }

            // 수면 오버레이(기존 SleepUI DoSleep 동일 외형 — 실제 지속 0.3~2s 클램프는 SleepFor 내부)
            ShowSleepOverlay();

            // 수면 실행 — TimeManager.SleepFor(hours, onComplete)(107행) 콜백 체계.
            // GameTime setter 직접 조작 금지 — 원본 규약(트랩 ①).
            TimeManager.Instance.SleepFor(_selectedHours, OnSleepComplete);
            Debug.Log($"[SleepPanelUTK] 수면 시작 — {_selectedHours:0}시간 (자동저장 {(SleepAutoSaveToggle.Enabled ? "켬" : "끔")})");
        }

        private void OnSleepComplete()
        {
            HideSleepOverlay();
            ApplySleepRecovery(_selectedHours);
            UTKToastService.Show("휴식 완료 — HP 회복", 2.5f);
            _busy = false;
            SetActionsEnabled(true);
            Hide();   // 창 닫힘
            Debug.Log("[SleepPanelUTK] 기상 완료 — 회복 적용 + 패널 닫힘");
        }

        /// <summary>수면 완료 회복 — 체력 Heal(8h 기준 36% 비례, 누락분 clamp) + 기력 스태미너 회복(실측).</summary>
        private static void ApplySleepRecovery(float hours)
        {
            var health = PlayerHealth.Instance;
            if (health != null && health.MaxHP > 0f)
            {
                float heal = ComputeHealAmount(health.MaxHP, health.CurrentHP, hours);
                if (heal > 0f)
                    health.Heal(heal);   // PlayerHealth.Heal(282행) — 누락분 clamp 내부 적용
            }

            var mv = FindPlayerMovement();
            if (mv != null && mv.MaxStamina > 0f)
            {
                float restore = ComputeStaminaAmount(mv.MaxStamina, mv.Stamina, hours);
                RestoreStamina(mv, restore);
            }
        }

        /// <summary>스태미너 회복 — PlayerMovement 직접(리플렉션 _stamina — 공개 세터 부재, Systems 내부 필드).</summary>
        private static void RestoreStamina(PlayerMovement mv, float restore)
        {
            if (StaminaField == null || restore <= 0f)
                return;
            float current = mv.Stamina;
            float max = mv.MaxStamina;
            StaminaField.SetValue(mv, Mathf.Min(current + restore, max));   // 누락분 clamp
            Debug.Log($"[SleepPanelUTK] ⚡ 기력 회복 +{restore:0.0} → {Mathf.Min(current + restore, max):0.0}/{max:0.0}");
        }

        private static PlayerMovement FindPlayerMovement()
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            return go != null ? go.GetComponent<PlayerMovement>() : null;
        }

        private void SetActionsEnabled(bool enabled)
        {
            _timeButtons[0].SetEnabled(enabled);
            _timeButtons[1].SetEnabled(enabled);
            _timeButtons[2].SetEnabled(enabled);
            _content.Q<Button>("SleepStartButton")?.SetEnabled(enabled);
            _content.Q<Button>("SleepCancelButton")?.SetEnabled(enabled);
        }

        // ── 수면 오버레이(UIRoot 절대 full-screen — 구 SleepUI DoSleep 동일 외형, 이중 방지: SleepUI 미호출) ──
        private void ShowSleepOverlay()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;
            HideSleepOverlay();   // 멱등
            _sleepOverlay = new VisualElement { name = "SleepOverlay73" };
            _sleepOverlay.style.position = Position.Absolute;
            _sleepOverlay.style.left = 0f; _sleepOverlay.style.right = 0f;
            _sleepOverlay.style.top = 0f; _sleepOverlay.style.bottom = 0f;
            _sleepOverlay.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.7f));   // 구 SleepUI 오버레이 동일
            _sleepOverlay.pickingMode = PickingMode.Position;
            var label = new Label("수면 중...")
            {
                name = "SleepOverlayLabel",
                style = { fontSize = 24f, unityFontStyleAndWeight = FontStyle.Bold, unityTextAlign = TextAnchor.MiddleCenter }
            };
            label.style.color = new StyleColor(TextMain);
            label.style.position = Position.Absolute;
            label.style.left = 0f; label.style.right = 0f;
            label.style.top = 0f; label.style.bottom = 0f;
            _sleepOverlay.Add(label);
            root.Add(_sleepOverlay);
            _sleepOverlay.BringToFront();
        }

        private void HideSleepOverlay()
        {
            if (_sleepOverlay != null)
            {
                _sleepOverlay.RemoveFromHierarchy();
                _sleepOverlay = null;
            }
        }

        // ── 단축키: Enter = 수면 시작(저장 후 수면 시작과 동일 경로) — Esc 닫기는 UTKWindowManager ESC 스택 ──
        private void OnRootKeyDown(KeyDownEvent evt)
        {
            if (!IsOpen) return;
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                evt.StopPropagation();
                StartSleep();
            }
        }

        // ═════════════════════════ UTKWindowBase 라이프사이클 ═════════════════════════

        protected override void OnWindowOpen()
        {
            RefreshData();
            SleepAutoSaveToggle.OnChanged += OnAutoSaveChanged;   // 스위치 시스템 → UI 동기화
            // [63차] 등배수 창박스 적용 — 침대 상호작용 전용 창(피그마 227:262). 구 raw 624×1101 기준
            // 중앙배치 스케줄이 Open()의 ApplyCanvasFit([84차])을 16ms 뒤 덮어써 k≠1 화면(예: 2560×1440)
            // 에서 하단 ~197px 절단 재발 — 세이브 UI 하단 절단 루트원인(84차 수리 자가 무효화 경합).
            // [88차 B1] 선례(훈련소/음식점창) 동일 패턴 — 개방 즉시 + 지연 대응 재시도(16/120/400ms).
            schedule.Execute(ApplyCanvasFit).StartingIn(16);
            schedule.Execute(ApplyCanvasFit).StartingIn(120);
            schedule.Execute(ApplyCanvasFit).StartingIn(400);
        }

        protected override void OnWindowClosed()
        {
            SleepAutoSaveToggle.OnChanged -= OnAutoSaveChanged;
            HideSleepOverlay();   // 안전망 — ESC 기상/외부 닫기 시 오버레이 잔존 방지
            if (_busy)
            {
                _busy = false;    // ESC 기상(WakeUp→콜백) 외 닫기 — 수면은 TimeManager 코루틴이 계속 진행되며 완료 시 콜백 회복 적용
                SetActionsEnabled(true);
            }
        }

        private void OnAutoSaveChanged(bool on)
        {
            _switchLabel.text = on ? "켬" : "끔";
            _switchTrack.style.backgroundColor = new StyleColor(on ? Accent : CardBg);
            _switchKnob.style.left = on ? KnobOnX : KnobOffX;
            _switchLabel.style.color = new StyleColor(on ? Accent : TextSub);
        }

        // ═════════════════════════ 정적 계산 API (기상 미리보기/회복 예상 — 위조 없는 실측 계산) ═════════════════════════

        /// <summary>
        /// [63차] 등배수 창박스 배치 산식 순수 추출 — ApplyCanvasFit이 위임(ComputeWakeTime out 3개 선례 스타일).
        /// k=min(rootW/1920, rootH/1080)(FigmaCanvasLayout 캔버스 진실치), left/top=스케일 창박스 중앙(0 하한 클램프).
        /// 레이아웃 미확정(NaN/≤1)·k≤0 입력은 k=0·left=top=0 반환 — 호출부가 적용을 스킵(기존 가드 유지).
        /// </summary>
        public static void ComputeFitBox(float rootW, float rootH, out float k, out float left, out float top)
        {
            k = 0f; left = 0f; top = 0f;
            if (float.IsNaN(rootW) || float.IsNaN(rootH) || rootW <= 1f || rootH <= 1f) return;
            k = Mathf.Min(rootW / FigmaCanvasLayout.CanvasWidth, rootH / FigmaCanvasLayout.CanvasHeight);
            if (k <= 0f) { k = 0f; left = 0f; top = 0f; return; }
            left = Mathf.Max(0f, (rootW - WinW * k) * 0.5f);
            top = Mathf.Max(0f, (rootH - WinH * k) * 0.5f);
        }

        /// <summary>
        /// 기상 미리보기 계산 — 현재 시각 + 수면 시간(24h wrap + 일차 가산).
        /// 예: 23:30 + 8h → 07:30, dayDelta +1.
        /// </summary>
        public static void ComputeWakeTime(int hour, int minute, float sleepHours, out int wakeHour, out int wakeMinute, out int dayDelta)
        {
            int totalMinutes = Mathf.RoundToInt(hour * 60f + minute + Mathf.Max(0f, sleepHours) * 60f);
            dayDelta = Mathf.FloorToInt(totalMinutes / 1440f);
            int remaining = totalMinutes % 1440;
            wakeHour = remaining / 60;
            wakeMinute = remaining % 60;
        }

        /// <summary>체력 회복 비율 — 8h 기준 36%(피그마 "+36") 비례, 누락분 clamp. 0~1.</summary>
        public static float ComputeHealRatio(float maxHp, float currentHp, float sleepHours)
        {
            if (maxHp <= 0f) return 0f;
            float target = HealRatio8h * Mathf.Clamp01(sleepHours / 8f);
            float missing = Mathf.Clamp01((maxHp - currentHp) / maxHp);
            return Mathf.Min(target, missing);
        }

        /// <summary>체력 회복량(HP) — Heal API 적용값(누락분 clamp).</summary>
        public static float ComputeHealAmount(float maxHp, float currentHp, float sleepHours)
        {
            return ComputeHealRatio(maxHp, currentHp, sleepHours) * maxHp;
        }

        /// <summary>기력 회복 비율 — 8h 기준 72%(피그마 "+72") 비례, 누락분 clamp. 0~1.</summary>
        public static float ComputeStaminaRatio(float maxStamina, float currentStamina, float sleepHours)
        {
            if (maxStamina <= 0f) return 0f;
            float target = StaminaRatio8h * Mathf.Clamp01(sleepHours / 8f);
            float missing = Mathf.Clamp01((maxStamina - currentStamina) / maxStamina);
            return Mathf.Min(target, missing);
        }

        /// <summary>기력 회복량 — 스태미너 적용값(누락분 clamp).</summary>
        public static float ComputeStaminaAmount(float maxStamina, float currentStamina, float sleepHours)
        {
            return ComputeStaminaRatio(maxStamina, currentStamina, sleepHours) * maxStamina;
        }

        // ═════════════════════════ 개방 API ═════════════════════════

        /// <summary>수면 시스템 패널 표시 — Bed 참조 필수(UTKWireUp 침대 라우팅 경유).</summary>
        public static void Open(Bed bed)
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null)
            {
                Debug.LogWarning("[SleepPanelUTK] UIRoot 미준비 — 수면 패널 개방 불가");
                return;
            }
            if (_instance == null || _instance.panel == null)
            {
                if (_instance != null) _instance.RemoveFromHierarchy();
                _instance = new SleepPanelUTK();
                root.Add(_instance);
                Debug.Log("[SleepPanelUTK] 수면 시스템 패널 인스턴스 생성");
            }
            _instance._bed = bed;
            _instance.Show();
            _instance.ApplyCanvasFit();   // [84차] 등배수 창박스 — raw 통짜 렌더의 하단 절단 수리(아래 요약)
            _instance.RefreshData();
            Debug.Log($"[SleepPanelUTK] 수면 시스템 패널 표시 — 침대 {bed?.BedName ?? "-"}");
        }

        /// <summary>
        /// [144차 D6] 구 SleepUTK.SaveAtBed 이식 — 침대 저장 기능 보존(스폰핀 기록 + SaveManager 자동저장).
        /// 폐기된 SleepUTK의 리플렉션 계약(GetMethod("SaveAtBed", NonPublic|Static)) 대상을 본 패널로 교체
        /// (PlayerCastleInteriorCollisionAndKitchenTests). UI는 73차 피그마 슬롯 저장 UX 유지 —
        /// 침대 스폰핀 저장은 본 정적 메서드로 계약 보존(상실 금지).
        /// </summary>
        private static void SaveAtBed()
        {
            var bed = _instance != null ? _instance._bed : null;
            if (bed == null)
            {
                Debug.LogWarning("[SleepPanelUTK] 세이브 실패: 현재 침대 참조 없음");
                return;
            }
            Bed.SetSpawnPoint(bed.transform.position);
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.AutoSave();
                UTKToastService.Show("💾 세이브 완료! 사망 시 이 침대에서 부활", 2.5f);
                Debug.Log("[SleepPanelUTK] 침대 세이브 완료");
            }
            else
            {
                Debug.LogWarning("[SleepPanelUTK] SaveManager 없음 — 스폰핀만 설정됨");
            }
        }

        /// <summary>
        /// [84차] 등배수 창박스 적용 — "창박스=figma×k, 내부 raw·scale=k" 계약(인벤 등 기존 창과 동일).
        /// 구: raw 624×1101 통짜 렌더 → 909 높이 화면에서 하단 ~192px 절단 — 수동 저장 슬롯 3~5·
        /// "저장 후 수면 시작" 버튼이 화면 밖("침대에서 세이브 UI가 없다" 사용자 보고 2026-10-16 실측).
        /// k=min(rootW/1920, rootH/1080). 창=중앙 배치, 내부 콘텐츠는 raw 유지+transform.scale=k
        /// (글자/간격 왜곡 없음). k≥1이면 위치만 중앙 보정.
        /// [63차] 배치 산식은 ComputeFitBox 순수 헬퍼로 위임(편집기 테스트 계약 대상).
        /// </summary>
        private void ApplyCanvasFit()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;
            ComputeFitBox(root.resolvedStyle.width, root.resolvedStyle.height, out float k, out float left, out float top);
            if (k <= 0f) return;   // 레이아웃 미확정 — 다음 개방 시 재적용(기존 가드 유지)

            style.width = WinW * k;
            style.height = WinH * k;
            style.left = left;     // 중앙 배치
            style.top = top;
            _content.style.transformOrigin = new TransformOrigin(0f, 0f, 0f);
            _content.style.scale = new StyleScale(new Scale(new Vector2(k, k)));
        }

        /// <summary>현재 개방 인스턴스(테스트/디버그 조회용).</summary>
        public static SleepPanelUTK Instance => _instance;

        // ═════════════════════════ 절차 아이콘(벡터 이미지 미사용 — rect/circle 구성) ═════════════════════════

        /// <summary>moon 절차 아이콘 — 원 + 컷아웃 원(이클립스 초승달). size = 아이콘 영역.</summary>
        private static VisualElement BuildMoonIcon(float size, Color bodyColor, Color? cutColor = null)
        {
            var host = new VisualElement { name = "MoonIcon" };
            host.style.width = size; host.style.height = size;
            host.pickingMode = PickingMode.Ignore;
            var body = new VisualElement();
            body.style.width = size * 0.78f; body.style.height = size * 0.78f;
            body.style.borderTopLeftRadius = body.style.borderTopRightRadius = size * 0.39f;
            body.style.borderBottomLeftRadius = body.style.borderBottomRightRadius = size * 0.39f;
            body.style.backgroundColor = new StyleColor(bodyColor);   // 초승달 채색 — #13 제목 #58A6FF / 시작버튼 #8B949E(호출부 지정)
            body.style.position = Position.Absolute;
            body.style.left = 0f; body.style.top = size * 0.22f;
            body.pickingMode = PickingMode.Ignore;
            host.Add(body);
            var cut = new VisualElement();   // 컷아웃 — 배경색 원으로 우상단을 깎아 초승달 구성
            cut.style.width = size * 0.78f; cut.style.height = size * 0.78f;
            cut.style.borderTopLeftRadius = cut.style.borderTopRightRadius = size * 0.39f;
            cut.style.borderBottomLeftRadius = cut.style.borderBottomRightRadius = size * 0.39f;
            cut.style.backgroundColor = new StyleColor(cutColor ?? WinBg);   // 시작버튼(#69) — 버튼 배경색으로 컷아웃
            cut.style.position = Position.Absolute;
            cut.style.left = size * 0.42f; cut.style.top = size * 0.02f;
            cut.pickingMode = PickingMode.Ignore;
            host.Add(cut);
            return host;
        }

        /// <summary>bed 절차 아이콘 — rect 2개(침대 본체 + 베개/머리판). 24×24 영역.</summary>
        private static VisualElement BuildBedGlyph()
        {
            var host = new VisualElement { name = "BedGlyph" };
            host.style.width = 24f; host.style.height = 24f;
            host.pickingMode = PickingMode.Ignore;
            var body = new VisualElement();   // rect 1 — 침대 본체 20×9 (하단)
            body.style.width = 20f; body.style.height = 9f;
            body.style.backgroundColor = new StyleColor(Accent);   // [144차 P7-SLP #16] bed Vector stroke #58A6FF sw1.6(227:372) — 절차 근사 채색
            body.style.position = Position.Absolute;
            body.style.left = 2f; body.style.top = 11f;
            body.style.borderTopLeftRadius = body.style.borderTopRightRadius = 2f;
            body.style.borderBottomLeftRadius = body.style.borderBottomRightRadius = 2f;
            body.pickingMode = PickingMode.Ignore;
            host.Add(body);
            var head = new VisualElement();   // rect 2 — 베개/머리판 8×8 (좌상단)
            head.style.width = 8f; head.style.height = 8f;
            head.style.backgroundColor = new StyleColor(Accent);   // [144차 P7-SLP #16] bed 채색 동일
            head.style.position = Position.Absolute;
            head.style.left = 2f; head.style.top = 4f;
            head.style.borderTopLeftRadius = head.style.borderTopRightRadius = 2f;
            head.style.borderBottomLeftRadius = head.style.borderBottomRightRadius = 2f;
            head.pickingMode = PickingMode.Ignore;
            host.Add(head);
            return host;
        }

        /// <summary>heart 절차 아이콘 — 45° 회전 사각 2개 교차(하트 러브 근사). 16×16.</summary>
        private static VisualElement BuildHeartGlyph()
        {
            var host = new VisualElement { name = "HeartGlyph" };
            host.style.width = 16f; host.style.height = 16f;
            host.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < 2; i++)
            {
                var quad = new VisualElement();
                quad.style.width = 9f; quad.style.height = 9f;
                quad.style.backgroundColor = new StyleColor(Accent);
                quad.style.position = Position.Absolute;
                quad.style.left = 3.5f; quad.style.top = 3.5f;
                quad.style.rotate = new Rotate(new Angle(i == 0 ? 45f : -45f, AngleUnit.Degree));
                quad.style.borderTopLeftRadius = quad.style.borderTopRightRadius = 2f;
                quad.style.borderBottomLeftRadius = quad.style.borderBottomRightRadius = 2f;
                quad.pickingMode = PickingMode.Ignore;
                host.Add(quad);
            }
            return host;
        }

        /// <summary>zap 절차 아이콘 — 다이아몬드(45° 회전 사각) + 하단 사각(번개 근사). 16×16.</summary>
        private static VisualElement BuildZapGlyph()
        {
            var host = new VisualElement { name = "ZapGlyph" };
            host.style.width = 16f; host.style.height = 16f;
            host.pickingMode = PickingMode.Ignore;
            var diamond = new VisualElement();
            diamond.style.width = 10f; diamond.style.height = 10f;
            diamond.style.backgroundColor = new StyleColor(Accent);
            diamond.style.position = Position.Absolute;
            diamond.style.left = 3f; diamond.style.top = 3f;
            diamond.style.rotate = new Rotate(new Angle(45f, AngleUnit.Degree));
            diamond.pickingMode = PickingMode.Ignore;
            host.Add(diamond);
            var tail = new VisualElement();
            tail.style.width = 4f; tail.style.height = 6f;
            tail.style.backgroundColor = new StyleColor(Accent);
            tail.style.position = Position.Absolute;
            tail.style.left = 6f; tail.style.top = 8f;
            tail.pickingMode = PickingMode.Ignore;
            host.Add(tail);
            return host;
        }

        /// <summary>save 절차 아이콘 — 디스켓: 테두리 + 상단 라벨 + 하단 슬라이더. 18×18.</summary>
        private static VisualElement BuildSaveGlyph()
        {
            var host = new VisualElement { name = "SaveGlyph" };
            host.style.width = 18f; host.style.height = 18f;
            host.style.borderTopWidth = host.style.borderBottomWidth = 1.6f;   // [144차 P7-SLP #48] save Vector stroke #58A6FF sw1.6(227:395-396)
            host.style.borderLeftWidth = host.style.borderRightWidth = 1.6f;
            host.style.borderTopColor = host.style.borderBottomColor = new StyleColor(Accent);
            host.style.borderLeftColor = host.style.borderRightColor = new StyleColor(Accent);
            host.style.borderTopLeftRadius = host.style.borderTopRightRadius = 2f;
            host.style.borderBottomLeftRadius = host.style.borderBottomRightRadius = 2f;
            host.pickingMode = PickingMode.Ignore;
            var label = new VisualElement();
            label.style.width = 8f; label.style.height = 4f;
            label.style.backgroundColor = new StyleColor(Accent);   // [144차 P7-SLP #48] save 내부 글리프 동색
            label.style.position = Position.Absolute;
            label.style.left = 5f; label.style.top = 3f;
            label.pickingMode = PickingMode.Ignore;
            host.Add(label);
            var slider = new VisualElement();
            slider.style.width = 10f; slider.style.height = 6f;
            slider.style.backgroundColor = new StyleColor(Accent);   // [144차 P7-SLP #48] save 내부 글리프 동색
            slider.style.position = Position.Absolute;
            slider.style.left = 4f; slider.style.top = 9f;
            slider.pickingMode = PickingMode.Ignore;
            host.Add(slider);
            return host;
        }

        /// <summary>info 절차 아이콘 — 원형 테두리 + i(점 + 몸통). 14×14.</summary>
        private static VisualElement BuildInfoGlyph()
        {
            var host = new VisualElement { name = "InfoGlyph" };
            host.style.width = 14f; host.style.height = 14f;
            host.style.borderTopWidth = host.style.borderBottomWidth = 1.5f;
            host.style.borderLeftWidth = host.style.borderRightWidth = 1.5f;
            host.style.borderTopColor = host.style.borderBottomColor = new StyleColor(TextSub);
            host.style.borderLeftColor = host.style.borderRightColor = new StyleColor(TextSub);
            host.style.borderTopLeftRadius = host.style.borderTopRightRadius = 7f;
            host.style.borderBottomLeftRadius = host.style.borderBottomRightRadius = 7f;
            host.pickingMode = PickingMode.Ignore;
            var dot = new VisualElement();
            dot.style.width = 2f; dot.style.height = 2f;
            dot.style.backgroundColor = new StyleColor(TextSub);
            dot.style.position = Position.Absolute;
            dot.style.left = 6f; dot.style.top = 3.5f;
            dot.pickingMode = PickingMode.Ignore;
            host.Add(dot);
            var stem = new VisualElement();
            stem.style.width = 2f; stem.style.height = 5f;
            stem.style.backgroundColor = new StyleColor(TextSub);
            stem.style.position = Position.Absolute;
            stem.style.left = 6f; stem.style.top = 7f;
            stem.pickingMode = PickingMode.Ignore;
            host.Add(stem);
            return host;
        }

        /// <summary>shield 절차 아이콘 — 테두리 사각(radius 3) + 중앙 체크 대각 rect. 12×12.</summary>
        private static VisualElement BuildShieldGlyph()
        {
            var host = new VisualElement { name = "ShieldGlyph" };
            host.style.width = 14f; host.style.height = 14f;   // [144차 P7-SLP #20] shield 12→14 — 피그마 14×14(227:374)
            host.style.borderTopWidth = host.style.borderBottomWidth = 1.5f;
            host.style.borderLeftWidth = host.style.borderRightWidth = 1.5f;
            host.style.borderTopColor = host.style.borderBottomColor = new StyleColor(Accent);
            host.style.borderLeftColor = host.style.borderRightColor = new StyleColor(Accent);
            host.style.borderTopLeftRadius = host.style.borderTopRightRadius = 3f;
            host.style.borderBottomLeftRadius = host.style.borderBottomRightRadius = 3f;
            host.pickingMode = PickingMode.Ignore;
            var check = new VisualElement();
            check.style.width = 6f; check.style.height = 2f;
            check.style.backgroundColor = new StyleColor(Accent);
            check.style.position = Position.Absolute;
            check.style.left = 4f; check.style.top = 6f;   // [144차 P7-SLP #20] 14×14 중앙 보정
            check.style.rotate = new Rotate(new Angle(-30f, AngleUnit.Degree));
            check.pickingMode = PickingMode.Ignore;
            host.Add(check);
            return host;
        }

        /// <summary>닫기 글리프 — rect 2개 45° 교차(✕). 30×30 버튼 중앙.</summary>
        private static VisualElement BuildCloseGlyph()
        {
            var host = new VisualElement { name = "CloseGlyph" };
            host.style.width = 16f; host.style.height = 16f;
            host.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < 2; i++)
            {
                var bar = new VisualElement();
                bar.style.width = 14f; bar.style.height = 1.5f;
                bar.style.backgroundColor = new StyleColor(TextSub);
                bar.style.position = Position.Absolute;
                bar.style.left = 1f; bar.style.top = 7.25f;
                bar.style.rotate = new Rotate(new Angle(i == 0 ? 45f : -45f, AngleUnit.Degree));
                bar.pickingMode = PickingMode.Ignore;
                host.Add(bar);
            }
            return host;
        }

        /// <summary>화살표 절차 글리프 — 가로선 + 우측 다이아몬드(→). 28×10.</summary>
        private static VisualElement BuildArrowGlyph()
        {
            var host = new VisualElement { name = "ArrowGlyph" };
            host.style.width = 28f; host.style.height = 10f;
            host.pickingMode = PickingMode.Ignore;
            var line = new VisualElement();
            line.style.width = 20f; line.style.height = 2f;
            line.style.backgroundColor = new StyleColor(Accent);
            line.style.position = Position.Absolute;
            line.style.left = 0f; line.style.top = 4f;
            line.pickingMode = PickingMode.Ignore;
            host.Add(line);
            var head = new VisualElement();
            head.style.width = 6f; head.style.height = 6f;
            head.style.backgroundColor = new StyleColor(Accent);
            head.style.position = Position.Absolute;
            head.style.left = 21f; head.style.top = 2f;
            head.style.rotate = new Rotate(new Angle(45f, AngleUnit.Degree));
            head.pickingMode = PickingMode.Ignore;
            host.Add(head);
            return host;
        }

        /// <summary>file-check-2 절차 글리프 — 문서 테두리(우상단 접힘 절단) + 내부 체크 대각 rect. 20×20 #8B949E. PNG 부재 — 절차 근사.</summary>
        private static VisualElement BuildFileCheckGlyph()
        {
            var host = new VisualElement { name = "FileCheckGlyph" };
            host.style.width = 20f; host.style.height = 20f;
            host.pickingMode = PickingMode.Ignore;
            var body = new VisualElement();   // 문서 본체 — 테두리 사각 14×17 radius 2 (BuildSaveGlyph 선례)
            body.style.width = 14f; body.style.height = 17f;
            body.style.borderTopWidth = body.style.borderBottomWidth = 1.5f;
            body.style.borderLeftWidth = body.style.borderRightWidth = 1.5f;
            body.style.borderTopColor = body.style.borderBottomColor = new StyleColor(TextSub);   // #8B949E
            body.style.borderLeftColor = body.style.borderRightColor = new StyleColor(TextSub);
            body.style.borderTopLeftRadius = body.style.borderTopRightRadius = 2f;
            body.style.borderBottomLeftRadius = body.style.borderBottomRightRadius = 2f;
            body.style.position = Position.Absolute;
            body.style.left = 3f; body.style.top = 1.5f;
            body.pickingMode = PickingMode.Ignore;
            host.Add(body);
            var fold = new VisualElement();   // 우상단 접힌 귀퉁이 — 슬롯 아이콘 배경색(#1C232C) 사각으로 테두리 절단 근사
            fold.style.width = 4f; fold.style.height = 4f;
            fold.style.backgroundColor = new StyleColor(IconBg);
            fold.style.position = Position.Absolute;
            fold.style.left = 13f; fold.style.top = 1.5f;
            fold.pickingMode = PickingMode.Ignore;
            host.Add(fold);
            for (int i = 0; i < 2; i++)       // 체크 — 대각 rect 2개(45°/-45°, BuildCloseGlyph 선례)
            {
                var bar = new VisualElement();
                bar.style.width = i == 0 ? 3.5f : 6f; bar.style.height = 1.5f;
                bar.style.backgroundColor = new StyleColor(TextSub);
                bar.style.position = Position.Absolute;
                bar.style.left = i == 0 ? 6f : 8.5f; bar.style.top = i == 0 ? 11f : 10.5f;
                bar.style.rotate = new Rotate(new Angle(i == 0 ? 45f : -45f, AngleUnit.Degree));
                bar.pickingMode = PickingMode.Ignore;
                host.Add(bar);
            }
            return host;
        }

        // ───────────────────── 공용 레이아웃/라벨 헬퍼 ─────────────────────

        /// <summary>절대 배치 블록 컨테이너(패널-로컬 좌표).</summary>
        private static VisualElement Block(float x, float y, float w, float h)
        {
            var ve = new VisualElement();
            ve.style.position = Position.Absolute;
            ve.style.left = x; ve.style.top = y;
            ve.style.width = w; ve.style.height = h;
            ve.pickingMode = PickingMode.Ignore;
            return ve;
        }

        /// <summary>카드(#FFFFFF/2% radius + 테두리 #30363D@0.502 sw1.0 — [144차 P7-SLP #14/#37]) — 절대 배치.</summary>
        private static VisualElement Card(float x, float y, float w, float h, float radius = 10f)
        {
            var ve = Block(x, y, w, h);
            ve.style.backgroundColor = new StyleColor(CardBg);
            ve.style.borderTopWidth = ve.style.borderBottomWidth = 1f;
            ve.style.borderLeftWidth = ve.style.borderRightWidth = 1f;
            ve.style.borderTopColor = ve.style.borderBottomColor = new StyleColor(StrokeSub);
            ve.style.borderLeftColor = ve.style.borderRightColor = new StyleColor(StrokeSub);
            ve.style.borderTopLeftRadius = ve.style.borderTopRightRadius = radius;
            ve.style.borderBottomLeftRadius = ve.style.borderBottomRightRadius = radius;
            return ve;
        }

        private static Label MakeLabel(string text, float fontSize, FontStyle style, Color color)
        {
            var lbl = new Label(text ?? "");
            lbl.style.fontSize = fontSize;
            lbl.style.unityFontStyleAndWeight = style;
            lbl.style.color = new StyleColor(color);
            lbl.pickingMode = PickingMode.Ignore;
            return lbl;
        }
    }
}