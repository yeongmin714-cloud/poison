// 2026-10-23 (121차): 아레나 베팅 창 — 피그마 255:647 '투기장 베팅' 1600×1040 전정합.
// 음식점창(RestaurantWindowUTK) 동일 골격: Frameless + 투명 루트 + raw 1600×1040 + ApplyCanvasFit 등배수.
// 좌판(64,78) 480×882 참가 선수 목록 / 우판(568,78) 968×882 선수 상세·베팅 계산기 / 헤더(64,36) / 도움말(64,984).
// 확정 버튼 → ArenaBettingSystem.PlaceBetAndStart (골드 즉시 차감 → 자동 전환 → 듀엔) → 성공 시 창 닫힘.
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>투기장 베팅 창 — 255:647 정합. 관리인 NPC 상호판넬 "베팅하기"에서 개방.</summary>
    public class ArenaWindowUTK : UTKWindowBase
    {
        public const float WinW = 1600f;
        public const float WinH = 1040f;

        // ── 피그마 색 계약 ──
        private static readonly Color Accent = new Color(0x58 / 255f, 0xA6 / 255f, 0xFF / 255f);
        private static readonly Color Stroke = new Color(0x30 / 255f, 0x36 / 255f, 0x3D / 255f);
        private static readonly Color PanelBg = new Color(0x13 / 255f, 0x18 / 255f, 0x1F / 255f);
        private static readonly Color RootBg = new Color(0x0B / 255f, 0x0E / 255f, 0x14 / 255f);
        private static readonly Color TextMain = new Color(0xF0 / 255f, 0xF6 / 255f, 0xFC / 255f);   // [144차 P1 #57] #E6EDF3 → Figma 진실치 #F0F6FC
        private static readonly Color TextSub = new Color(0x8B / 255f, 0x94 / 255f, 0x9E / 255f);
        // [144차 P1 #6/14/15] 카드/판 보조 토큰 — Figma 진실치(JSON 255:659~255:846 원문)
        private static readonly Color StrokeAlpha = new Color(0x30 / 255f, 0x36 / 255f, 0x3D / 255f, 0.502f);   // 테두리 #30363D@0.502
        private static readonly Color CardBg = new Color(0x19 / 255f, 0x1F / 255f, 0x27 / 255f);                 // #191F27 카드/확인카드/닫기버튼
        private static readonly Color CardBgSel = new Color(0x1A / 255f, 0x28 / 255f, 0x39 / 255f);              // #1A2839 선택 카드/수령액 카드
        private static readonly Color CrestBg = new Color(0x20 / 255f, 0x26 / 255f, 0x2E / 255f);                // #20262E 크레스트 프레임/게이지 트랙
        private static readonly Color InputBg = new Color(0x0F / 255f, 0x14 / 255f, 0x1B / 255f);                // #0F141B 금액 입력 박스/개요 문장 프레임
        private static readonly Color TextDim = new Color(0x58 / 255f, 0x64 / 255f, 0x73 / 255f);                // #586473 미선택 상태/×= 기호

        private static readonly int[] QuickAmounts = { 100, 500, 1000, 5000 };   // [계약] 베팅 상한 5000 유지 — Figma 안내 "최대 10,000 G"는 게임 밸런스 계약 우선(사용자 확정)

        private static ArenaWindowUTK _instance;
        public static ArenaWindowUTK Instance => _instance;

        private string _nationStyle = "Western";
        private ArenaBetPair _pair;
        private ArenaBetFighter _selected;
        private int _betAmount = 1000;
        private IVisualElementScheduledItem _pollTask;

        // ── UI 핸들 ──
        private Label _dayLabel, _clockLabel;
        private Label _guideRightLabel;
        private VisualElement _cardWest, _cardEast;
        private Label[] _cardNames = new Label[2];
        private Label[] _cardSubtitles = new Label[2];   // [144차 P1 #18 후행] 카드 부제(fs11 (66,41)) — 기존 tagline 라벨 승격(Figma 255:682/255:697 위치)
        private Label[] _cardOdds = new Label[2];
        private Label[] _cardRecords = new Label[2];
        private Label[] _cardStats = new Label[2];
        private Label[] _cardStates = new Label[2];
        private VisualElement[] _cardCrests = new VisualElement[2];   // [144차 P6-BET #2] 카드 크레스트 프레임(42×44) — 선택 시 배경 틴트 갱신용
        private VisualElement[] _cardTicks = new VisualElement[2];    // [144차 P6-BET #4] 선택 circle-check(18×18) — 선택 카드에만 표시
        private Label[] _cardLevels = new Label[2];   // [144차 P6-BET #3] 카드 레벨 칩(fs10 mono (166,21)) — ArenaBetFighter에 레벨 필드 부재 → 'Lv.—' 폴백(기록)
        private Label _detailName, _detailOrigin, _detailTagline, _detailOddsBig;
        private VisualElement _detailStateChip;   // [144차 P6-BET #6] 개요 카드 "선택 선수" 상태 칩 — 선택 시에만 표시
        private Label[] _recordValues = new Label[3];   // [144차 P0 #7] 기록 요약 3칸 값(통산 전적/통산 승률/최근 5경기)
        private Label _statAtk, _statDef, _statAgi;
        private Label _statValAtk, _statValDef, _statValAgi;   // [144차 P0 #8] 능력치 수치("88 / 100" Geist Mono fs14 우측)
        private VisualElement _barAtk, _barDef, _barAgi;
        private Label _goldLabel, _amountLabel, _oddsCalcLabel, _payoutLabel, _profitLabel, _balanceLabel;
        private Label _amountBoxValue;     // [144차 P6-BET #9] 금액 입력 박스 값(fs25 Geist Mono) — _amountLabel(수령액 카드 원금)과 별개 표시, 동일 실데이터
        private Label _minMaxLabel;        // [144차 P0 #9] 금액 안내 — 실제 상한 동적 표기("최소 100 G · 최대 {max} G")
        private Label _badgeLabel;         // [144차 P6-BET #5] 베팅 섹션 상태 배지 — "{선수명} · 2인 난투" 실데이터 바인딩
        private VisualElement _badge;      // 배지 컨테이너(미선택 시 비표시)
        private Label _summaryLabel;       // [144차 P0 #11] 베팅 확인 카드 요약 라인("{선수명}의 2인 난투 승리에 베팅합니다" fs13 700)
        private Label _portraitName1, _portraitName2;   // [125차 후속13] 선수 사진 카드 이름(Figma 259:7/259:12 계약 — 실제 페어 데이터)
        private Label _portraitEquip1, _portraitEquip2; // [144차 P0 #13] 사진 카드 "장비" 라벨 — 태그라인 1번째 세그먼트(병사 무장 계열 실데이터)
        private VisualElement _portraitCardWest, _portraitCardEast;   // [125차 후속20] 초상화 카드 핸들 — 선택 시 파란 테두리 갱신용
        private Button _confirmButton;
        private Label _errorLabel;

        private ArenaWindowUTK() : base("투기장 베팅", new Vector2(WinW, WinH), UTKWindowChrome.Frameless)
        {
            style.backgroundColor = new StyleColor(Color.clear);
            style.backgroundImage = new StyleBackground(StyleKeyword.None);
            style.overflow = Overflow.Hidden;

            var root = new VisualElement { name = "ArenaBetRoot" };
            root.style.position = Position.Absolute;
            root.style.left = 0f; root.style.top = 0f;
            root.style.width = WinW; root.style.height = WinH;
            // [125차 후속12] 플루언트 계약(사용자 지시) — 불투명 풀캔버스 배경 폐기, 게임 화면이 창 뒤로 비침.
            //   판별 가독성은 각 판의 글래스 배경(아래)과 헤더/도움말 글래스 스트립이 담당.
            root.style.backgroundColor = new StyleColor(Color.clear);
            _content.style.width = WinW;
            _content.style.height = WinH;
            _content.style.position = Position.Relative;
            _content.Add(root);

            // [125차 후속13] 헤더바(전초기지/시간 그리드) 제거 — 사용자 지시("굳이 없어도 될 것 같아")
            //   BuildHeaderBar 호출 폐기(메서드 유지). 시계 라벨 널가드는 RefreshClock 참조.
            BuildLeftPanel(root);
            BuildRightPanel(root);
            BuildHelpBar(root);

            RegisterCallback<AttachToPanelEvent>(_ => UIToolkitBootstrap.UIRoot?.RegisterCallback<KeyDownEvent>(OnRootKeyDown));
            RegisterCallback<DetachFromPanelEvent>(_ => UIToolkitBootstrap.UIRoot?.UnregisterCallback<KeyDownEvent>(OnRootKeyDown));
            ApplyUIToolkitFont(this);
            ApplyMonoFonts();   // [144차 P0 #3/8/9] Geist Mono 라벨 — ApplyUIToolkitFont 전역 오버라이드 이후 재적용(순서 계약)
            DisableWindowShadow();   // [125차 후속13] 창 외곽 드롭 섀도우(음영) 제거 — 플루언트 계약(사용자 지시: 가장 바깥 음영 폐기)
        }

        // ═══════════════ 자가부트 / 개방 API ═══════════════

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => EnsureAttached();

        private static void EnsureAttached()
        {
            if (Application.isBatchMode) return;   // [146차 P0] 배치모드 헤드리스는 창 부트/GLB 초상화 렌더(크래시 실측) 스킵 — 인에디터/플레이 무영향
            var root = UIToolkitBootstrap.UIRoot;
            if (_instance != null)
            {
                bool orphaned = _instance.parent == null;
                if (!orphaned)
                {
                    var p = _instance.parent;
                    while (p != null && p != root) p = p.parent;
                    orphaned = p != root;
                }
                if (orphaned)
                {
                    _instance.RemoveFromHierarchy();
                    _instance = null;
                    Debug.Log("[ArenaWindowUTK] EnsureAttached — 고아 인스턴스 폐기·재구축 [125차 후속9]");
                }
            }
            if (root == null) return;   // 부트 전 조기 반환 — 개방 실패 피드백/진단은 Open() 실패 경로가 담당 [125차 후속9]
            if (_instance == null) _instance = new ArenaWindowUTK();
            if (_instance.parent == null)
            {
                root.Add(_instance);
                Debug.Log("[ArenaWindowUTK] EnsureAttached — UIRoot 부착 완료 [125차 후속9]");
            }
        }

        /// <summary>베팅 창 개방 — 관리인 NPC 상호판넬 "베팅하기" 진입점. 쌍 제공(ArenaBettingSystem.OfferPair).</summary>
        public static void Open(string nationStyle)
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null)
            {
                // [125차 후속9] 개방 실패 경로 피드백(상호작용불가=피드백 필수 계약) + 진단 — 무음 반환 폐기.
                // IsReady=false → 부트 미완(PanelSettings 결여/DisableAllUi 포함), true → UIDocument는 있으나 rootVisualElement 비정상.
                Debug.LogWarning($"[ArenaWindowUTK] 베팅 창 개방 실패 — UIRoot null (IsReady={UIToolkitBootstrap.IsReady}, DisableAllUi={UITransitionState.DisableAllUi}) [125차 후속9]");
                UTKToastService.Show("베팅 창을 열 수 없습니다 — UI 준비 중입니다. 잠시 후 다시 시도하세요.", 2.5f);
                return;
            }
            if (ArenaBettingSystem.DuelRunning)
            {
                UTKToastService.Show("투기가 진행 중입니다 — 잠시 후 다시 시도하세요.", 2.5f);
                return;
            }

            EnsureAttached();
            _instance._nationStyle = string.IsNullOrEmpty(nationStyle) ? "Western" : nationStyle;
            _instance._pair = ArenaBettingSystem.OfferPair();
            _instance._selected = null;
            _instance._betAmount = 1000;
            _instance._errorLabel.text = "";
            bool reopen = _instance.IsOpen;   // [125차 후속9] 신규 표시 vs 재개방 갱신 판별용
            if (reopen) _instance.RefreshAll();
            else _instance.Show();
            // [125차 후속9] 성공 로그에 개방 방식/부착 상태 명시 — "베팅하기 안 됨" 판별용.
            // 이 로그조차 없으면 본 Open이 호출되지 않은 것(라우팅 문제), 실패 경고가 있으면 UIRoot 문제.
            Debug.Log($"[ArenaWindowUTK] 베팅 창 개방 성공 ({nationStyle}) — {_instance._pair.west.name} vs {_instance._pair.east.name} ({(reopen ? "재개방·갱신" : "신규 표시")}, IsOpen={_instance.IsOpen}, 부착됨={_instance.panel != null})");
        }

        // ═══════════════ 창 골격 ═══════════════

        private Label MakeLabel(VisualElement parent, string text, float size, Color color,
            float x = 0f, float y = 0f, float w = 0f, TextAnchor align = TextAnchor.UpperLeft)
        {
            var l = new Label(text);
            l.style.fontSize = size;
            l.style.color = new StyleColor(color);
            if (w > 0f) l.style.width = w;
            if (x > 0f || y > 0f)
            {
                l.style.position = Position.Absolute;
                l.style.left = x; l.style.top = y;
            }
            l.style.unityTextAlign = align;
            l.style.whiteSpace = WhiteSpace.Normal;
            parent.Add(l);
            return l;
        }

        // [144차 P0 #3/8/9] Geist Mono 패밀리 계약 — WorldMapWindowUTK.GetGeistMonoFont 동일 선례.
        //   프로젝트에 Geist Mono 에셋 미보유 실측(2026-10-24) → 부재 시 UIFont.Load(NotoSansKR) 폴백.
        //   없는 폰트를 흉내로 채우지 않는 위조 금지 계약에 따라 폰트 스왑만 기록, 에셋 추가 시 자동 적용.
        private static readonly System.Collections.Generic.List<Label> _monoLabels = new System.Collections.Generic.List<Label>();
        private static Font _geistMono;

        private static Label Mono(Label l)
        {
            if (l != null) _monoLabels.Add(l);
            return l;
        }

        /// <summary>Build 완료 후 1회 — ApplyUIToolkitFont(this)가 전체 트리 폰트를 Noto로 덮은 뒤 모노 라벨 재지정.</summary>
        private void ApplyMonoFonts()
        {
            if (_geistMono == null) _geistMono = Resources.Load<Font>("Fonts/GeistMono");
            var f = _geistMono != null ? _geistMono : UIFont.Load();
            foreach (var l in _monoLabels)
            {
                if (l != null) l.style.unityFont = f;
            }
            _monoLabels.Clear();   // 재구축(고아 폐기→신규) 시 누적 방지
        }

        private void BuildHeaderBar(VisualElement root)
        {
            var header = new VisualElement { name = "Header" };
            header.style.position = Position.Absolute;
            header.style.left = 64f; header.style.top = 36f;
            header.style.width = 1472f; header.style.height = 18f;
            header.style.backgroundColor = new StyleColor(UTKTheme.GlassPanelSoft);   // [125차 후속12] 글래스 스트립 — 루트 투명화로 헤더 가독성 담보
            root.Add(header);

            // 좌 — 현재 위치
            MakeLabel(header, "전초기지", 13f, TextSub, 30f, 0f);
            MakeLabel(header, "/", 12f, TextSub, 90f, 1f);
            MakeLabel(header, "투기장", 13f, TextMain, 107f, 0f);

            // 우 — 게임 시간(실데이터 — TimeManager)
            var day = MakeLabel(header, "", 12f, TextSub, 1355f, 1f, 70f);
            var clock = MakeLabel(header, "", 13f, TextMain, 1433f, 0f, 39f, TextAnchor.UpperRight);
            _dayLabel = day; _clockLabel = clock;
            RefreshClock();
        }

        private void BuildLeftPanel(VisualElement root)
        {
            var panel = new VisualElement { name = "FighterListPanel" };
            panel.style.position = Position.Absolute;
            panel.style.left = 64f; panel.style.top = 78f;
            panel.style.width = 480f; panel.style.height = 882f;
            panel.style.backgroundColor = new StyleColor(UTKTheme.GlassPanel);   // [125차 후속12] 글래스 판 — 게임 화면 비침
            // [144차 P1 #6] 판 테두리 — Figma 진실치 #30363D@0.502 폭 1.0(기존 불투명 0.5f 폐기), r14 유지
            panel.style.borderTopWidth = panel.style.borderBottomWidth =
                panel.style.borderLeftWidth = panel.style.borderRightWidth = 1f;
            panel.style.borderTopColor = panel.style.borderBottomColor =
                panel.style.borderLeftColor = panel.style.borderRightColor = new StyleColor(StrokeAlpha);
            panel.style.borderTopLeftRadius = panel.style.borderTopRightRadius =
                panel.style.borderBottomLeftRadius = panel.style.borderBottomRightRadius = 14f;
            root.Add(panel);

            MakeLabel(panel, "참가 선수", 22f, TextMain, 24f, 24f);
            MakeLabel(panel, "2인 난투 출전 명단", 11f, TextSub, 157f, 24f);   // [144차 P1 #9] 보조 제목 y 32→24(Figma 255:664)
            var guide = MakeLabel(panel, "승리할 선수를 선택하세요", 12f, TextMain, 24f, 88f, 260f);   // [144차 P1 #10] #F0F6FC 700(Figma 255:671)
            guide.style.unityFontStyleAndWeight = FontStyle.Bold;
            _guideRightLabel = MakeLabel(panel, "총 2명 · 단일 승자", 10f, TextSub, 380f, 88f, 76f, TextAnchor.UpperRight);   // [144차 P1 #11] (380,88) right=456

            var list = new VisualElement { name = "FighterCards" };
            list.style.position = Position.Absolute;
            list.style.left = 24f; list.style.top = 118f;
            list.style.width = 432f; list.style.height = 336f;   // [125차 후속13] Figma 정합 — 카드 158×2+간격(20)
            panel.Add(list);

            _cardWest = BuildFighterCard(list, 0, new Vector2(0f, 0f));
            _cardEast = BuildFighterCard(list, 1, new Vector2(0f, 168f));

            // [125차 후속13] Figma "선수 사진"(259:3) — 선수 목록 아래 세로형 초상화 카드 2장(432×360,
            //   카드 209.5/210.5×360, 이름 하단). 병사 전신 이미지 = Figma 255:647 아트 익스포트
            //   (Resources/UI/Arena/Portraits/). 좌판 로컬 (24,454) = 디자인 (88,532) 계약 좌표.
            var portraits = new VisualElement { name = "FighterPortraits" };
            portraits.style.position = Position.Absolute;
            portraits.style.left = 24f; portraits.style.top = 454f;
            portraits.style.width = 432f; portraits.style.height = 360f;
            panel.Add(portraits);
            _portraitName1 = BuildPortraitCard(portraits, "FighterPortrait_West", 0f, 209.5f, west: true);
            _portraitName2 = BuildPortraitCard(portraits, "FighterPortrait_East", 209.5f, 210.5f, west: false);

            // [144차 P1 #28] 경기 규칙 — Figma 255:732: 판 로컬 (110,653) 212×13, info 13×13 + gap7 + 문구 fs11.
            //   기존 (24,853) 텍스트 전용 배치를 Figma 좌표로 이동. info 벡터 아이콘은 Figma 유실 → 원형 프레임+"i"
            //   글리프 형태 대체(감사 §판정 불가 선례 — 코드는 텍스트/바 대체 기록).
            var rules = new VisualElement { name = "MatchRules" };
            rules.style.position = Position.Absolute;
            rules.style.left = 110f; rules.style.top = 653f;
            rules.style.width = 212f; rules.style.height = 13f;
            panel.Add(rules);
            BuildInfoGlyph(rules, 0f, 0f);
            MakeLabel(rules, "마지막까지 생존한 선수 1명이 승리합니다.", 11f, TextSub, 20f, 0f, 192f);
        }

        private VisualElement BuildFighterCard(VisualElement parent, int index, Vector2 pos)
        {
            var card = new VisualElement { name = index == 0 ? "FighterCard_West" : "FighterCard_East" };
            card.style.position = Position.Absolute;
            card.style.left = pos.x; card.style.top = pos.y;
            card.style.width = 432f; card.style.height = 158f;
            // [144차 P1 #14/15] 카드 배경 #191F27 불투명 r6 + 테두리 #30363D@0.502 폭1(기존 white@0.04 r9.6 폐기)
            card.style.backgroundColor = new StyleColor(CardBg);
            card.style.borderTopWidth = card.style.borderBottomWidth =
                card.style.borderLeftWidth = card.style.borderRightWidth = 1f;
            card.style.borderTopColor = card.style.borderBottomColor =
                card.style.borderLeftColor = card.style.borderRightColor = new StyleColor(StrokeAlpha);
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius =
                card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 6f;
            card.RegisterCallback<ClickEvent>(_ => SelectFighter(index == 0));
            parent.Add(card);

            // [144차 P6-BET #2] 선수 문장(크레스트) — Figma 255:676: 카드 로컬 (14,14) 42×44 r6 #20262E
            //   + shield 25×25 벡터. shield 계열 PNG가 Resources/UI/icons에 없어 **프레임만** 구현(기록 —
            //   아이콘 에셋 추가 시 여기에 삽입). 선택 카드 크레스트 배경은 Figma 255:691 #58A6FF@0.078로 갱신(ApplyCard).
            var crest = new VisualElement { name = "FighterCrest" };
            crest.style.position = Position.Absolute;
            crest.style.left = 14f; crest.style.top = 14f;
            crest.style.width = 42f; crest.style.height = 44f;
            crest.style.backgroundColor = new StyleColor(CrestBg);
            crest.style.borderTopWidth = crest.style.borderBottomWidth =
                crest.style.borderLeftWidth = crest.style.borderRightWidth = 1f;
            crest.style.borderTopColor = crest.style.borderBottomColor =
                crest.style.borderLeftColor = crest.style.borderRightColor = new StyleColor(StrokeAlpha);
            crest.style.borderTopLeftRadius = crest.style.borderTopRightRadius =
                crest.style.borderBottomLeftRadius = crest.style.borderBottomRightRadius = 6f;
            crest.style.justifyContent = Justify.Center;
            crest.style.alignItems = Align.Center;
            card.Add(crest);
            _cardCrests[index] = crest;

            // [144차 P1 #18] 선수 이름 — Figma 255:680: 카드 로컬 (66,18) fs16 700 #F0F6FC(기존 (16,12) fs15)
            _cardNames[index] = MakeLabel(card, "", 16f, TextMain, 66f, 18f, 250f);
            _cardNames[index].style.unityFontStyleAndWeight = FontStyle.Bold;

            // [144차 P1 #18 후행] 카드 부제 — Figma 255:682/255:697 (66,41) fs11 #8B949E.
            //   기존 tagline 라벨((16,98) fs9 — Figma 미존재 게임 전용 요소)을 Figma 부제 슬롯으로 승격.
            //   실데이터(f.tagline — 병사 무장·전투 성향 실문구) 바인딩 유지.
            _cardSubtitles[index] = MakeLabel(card, "", 11f, TextSub, 66f, 41f, 280f);

            // [144차 P6-BET #3] 카드 레벨 칩 — Figma 255:680: 카드 로컬 (166,21) fs10 Geist Mono.
            //   ArenaBetFighter에 레벨 필드가 없어 'Lv.—' 폴백(기록 — 데이터 모델에 level 확장 시 ApplyCard에서 실측 바인딩).
            _cardLevels[index] = Mono(MakeLabel(card, "Lv.—", 10f, TextSub, 166f, 21f, 60f));

            // [144차 P1 #21] 카드 전적 — Figma 255:684 (14,77) fs11(기존 (16,48) fs10)
            _cardRecords[index] = MakeLabel(card, "", 11f, TextSub, 14f, 77f, 250f);

            // [144차 P1 #20] 카드 베팅 배율 — Figma 255:685/255:701: fs21 Geist Mono, y70, 우측 패딩 14.
            //   색: 미선택 #F0F6FC / 선택 #58A6FF(JSON 255:685 vs 255:701 fill 차이 — ApplyCard에서 갱신)
            _cardOdds[index] = Mono(MakeLabel(card, "", 21f, TextMain, 328f, 70f, 90f, TextAnchor.UpperRight));

            // [144차 P1 #22] 능력치 요약 스트립 — Figma 255:686: 카드 로컬 (14,109) 404×23 테두리 스트립,
            //   텍스트 fs11 (14,119). 기존 (16,74) 자유 텍스트를 스트립 내부로 이동.
            var strip = new VisualElement { name = "FighterStatStrip" };
            strip.style.position = Position.Absolute;
            strip.style.left = 14f; strip.style.top = 109f;
            strip.style.width = 404f; strip.style.height = 23f;
            strip.style.borderTopWidth = strip.style.borderBottomWidth =
                strip.style.borderLeftWidth = strip.style.borderRightWidth = 1f;
            strip.style.borderTopColor = strip.style.borderBottomColor =
                strip.style.borderLeftColor = strip.style.borderRightColor = new StyleColor(StrokeAlpha);
            card.Add(strip);
            _cardStats[index] = MakeLabel(strip, "", 11f, TextSub, 0f, 5f, 250f);

            // [144차 P1 #23] 카드 상태 — Figma 255:688/255:704: 스트립 우측 fs10, y119.
            //   색: 미선택 #586473 / 선택 #58A6FF(ApplyCard 갱신)
            _cardStates[index] = MakeLabel(strip, "선택 가능", 10f, TextDim, 304f, 5f, 100f, TextAnchor.UpperRight);

            // [144차 P6-BET #4] 선택 circle-check — Figma 255:871: 카드 로컬 (400,27) 18×18, 선택 카드에만 표시.
            //   circle-check 벡터 유실 → Resources/UI/icons/tick.png(실존 에셋) + Accent 틴트 대체(기록).
            var tick = new VisualElement { name = "FighterSelectedTick" };
            tick.style.position = Position.Absolute;
            tick.style.left = 400f; tick.style.top = 27f;
            tick.style.width = 18f; tick.style.height = 18f;
            var tickTex = Resources.Load<Texture2D>("UI/icons/tick");
            if (tickTex != null)
            {
                tick.style.backgroundImage = new StyleBackground(tickTex);
                tick.style.unityBackgroundImageTintColor = new StyleColor(Accent);
            }
            tick.style.display = DisplayStyle.None;
            tick.pickingMode = PickingMode.Ignore;
            card.Add(tick);
            _cardTicks[index] = tick;
            return card;
        }

        private static Texture2D _portraitTexWest, _portraitTexEast;   // [125차 후속16] 세션 캐시 — 동일 soldier GLB, 렌더 1회

        /// <summary>[125차 후속16] 선수 초상화 렌더 — SoldierManagementUTK [39/43/47차] 오프스크린 미리보기
        ///   레시피 이식: soldier GLB 원격 클론(20000,2000,20000) → SoldierShield_AC + HumanoidClipDriver
        ///   (Soldier·정지=A포즈) → CopyMaterialsFromGlb → 1.8m 정규화 → RT 512 MSAA8 → FOV 28 정면
        ///   프레이밍(extents 1.3배) → 키/필/림 3등 전용 라이팅 → Render → ReadPixels. 배경 틴트로 서/동 구분.</summary>
        internal static Texture2D GetFighterPortrait(bool west)
        {
            if (west && _portraitTexWest != null) return _portraitTexWest;
            if (!west && _portraitTexEast != null) return _portraitTexEast;
            var tex = RenderFighterPortrait(west
                ? new Color(0.09f, 0.13f, 0.20f, 1f)
                : new Color(0.20f, 0.09f, 0.09f, 1f));
            if (tex != null)
            {
                if (west) _portraitTexWest = tex; else _portraitTexEast = tex;
            }
            return tex;
        }

        private static Texture2D RenderFighterPortrait(Color backdrop)
        {
            if (!RuntimeModelLoader.TryGetModel("soldier", out var soldierPrefab)) return null;
            const int size = 512;
            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            rt.Create();
            var model = UnityEngine.Object.Instantiate(soldierPrefab);
            model.name = "ArenaPortraitModel";
            Vector3 remote = new Vector3(20000f, 2000f, 20000f);
            model.transform.position = remote;
            try
            {
                var anim = model.GetComponent<Animator>();
                if (anim == null) anim = model.AddComponent<Animator>();
                anim.runtimeAnimatorController = UnityEngine.Resources.Load<RuntimeAnimatorController>("Animation/Controllers/SoldierShield_AC");
                anim.applyRootMotion = false;
                var driver = model.AddComponent<HumanoidClipDriver>();
                driver.mode = HumanoidClipDriver.DriveMode.Soldier;
                driver.enabled = false;   // A포즈 정지 — 프레임 안정([43차] 계약)
                HumanoidClipDriver.CopyMaterialsFromGlb(model, "soldier");
                TerritoryBuilder.NormalizeSoldierHeight(model, TerritoryBuilder.SoldierTargetHeight);   // [51차] 1.8m

                var rends = model.GetComponentsInChildren<Renderer>();
                if (rends.Length == 0) return null;
                Bounds b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);

                var camGo = new GameObject("ArenaPortraitCam");
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = backdrop;
                cam.cullingMask = ~0;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 20f;
                cam.fieldOfView = 28f;
                cam.targetTexture = rt;

                float halfH = b.extents.y * 1.30f + 0.08f;   // [43차 ③] 발 잘림 방지 여유
                float dist = halfH / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                Vector3 aim = new Vector3(b.center.x, b.min.y + b.size.y * 0.5f, b.center.z);
                cam.transform.position = aim + new Vector3(dist * 0.22f, dist * 0.10f, dist);   // +Z 정면·살짝 우측·상단
                cam.transform.LookAt(aim);

                // 전용 라이팅 — [47차] 키/필/림 축소판(무광 실루엣 방지)
                var key = new GameObject("AP_Key");
                key.transform.position = remote + new Vector3(2.5f, 3.5f, 2.5f);
                var keyL = key.AddComponent<Light>();
                keyL.type = LightType.Point; keyL.range = 14f; keyL.intensity = 1.6f; keyL.color = new Color(1f, 0.97f, 0.90f);
                var fill = new GameObject("AP_Fill");
                fill.transform.position = remote + new Vector3(-3f, 2f, 2f);
                var fillL = fill.AddComponent<Light>();
                fillL.type = LightType.Point; fillL.range = 14f; fillL.intensity = 0.7f; fillL.color = new Color(0.80f, 0.85f, 1f);
                var rim = new GameObject("AP_Rim");
                rim.transform.position = remote + new Vector3(0f, 3f, -3.5f);
                var rimL = rim.AddComponent<Light>();
                rimL.type = LightType.Point; rimL.range = 14f; rimL.intensity = 1.0f; rimL.color = new Color(0.90f, 0.95f, 1f);

                cam.Render();
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;

                UnityEngine.Object.Destroy(key);
                UnityEngine.Object.Destroy(fill);
                UnityEngine.Object.Destroy(rim);
                cam.targetTexture = null;
                UnityEngine.Object.Destroy(camGo);
                return tex;
            }
            finally
            {
                UnityEngine.Object.Destroy(model);
                rt.Release();
                UnityEngine.Object.Destroy(rt);
            }
        }

        /// <summary>[125차 후속16] Figma 259:4/259:9 계약 — 병사 전신 초상화 카드. 이미지 = 각 병사 GLB(soldier)
        ///   전신 오프스크린 렌더([47차] SoldierManagementUTK 레시피 이식 — 사용자 지시: Figma 아트가 아니라 GLB 전신).
        ///   이름은 실제 페어 데이터(위조 금지).</summary>
        private Label BuildPortraitCard(VisualElement parent, string name, float x, float w, bool west)
        {
            var card = new VisualElement { name = name };
            card.style.position = Position.Absolute;
            card.style.left = x; card.style.top = 0f;
            card.style.width = w; card.style.height = 360f;
            var tex = GetFighterPortrait(west);
            if (tex != null)
                card.style.backgroundImage = new StyleBackground(tex);
            else
                Debug.LogWarning("[ArenaWindowUTK] [125차 후속16] 초상화 GLB 렌더 실패 — Figma 아트 폴백");
            card.style.borderTopWidth = card.style.borderBottomWidth =
                card.style.borderLeftWidth = card.style.borderRightWidth = 1f;
            // [144차 P1 #25/27] 사진 카드 테두리/코너 — Figma: 비선택 #30363D@0.502 / r10(기존 불투명 Stroke r9.6).
            card.style.borderTopColor = card.style.borderBottomColor =
                card.style.borderLeftColor = card.style.borderRightColor = new StyleColor(StrokeAlpha);
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius =
                card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 10f;
            card.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
            if (west) _portraitCardWest = card; else _portraitCardEast = card;   // [125차 후속20] 초상화 카드 핸들 저장 — 선택 테두리 갱신용
            if (tex == null)
            {
                var fallback = UnityEngine.Resources.Load<Texture2D>(west
                    ? "UI/Arena/Portraits/arena_portrait_1"
                    : "UI/Arena/Portraits/arena_portrait_2");
                if (fallback != null) card.style.backgroundImage = new StyleBackground(fallback);
            }
            // [125차 후속23 F3] 초상화 카드 클릭 → 병사 선택 — 사용자 지시: "병사 모습이 표기된 부분을 클릭해도 병사 선택".
            //   정보카드(BuildFighterCard ClickEvent 선례)와 동일 경로 — 클릭 시 SelectFighter → 정보카드 갱신+
            //   초상화 파란 테두리(RefreshPortraitBorders) 동시 갱신. 이름 라벨 클릭도 버블링으로 카드에 도달.
            card.RegisterCallback<ClickEvent>(_ => SelectFighter(west));
            parent.Add(card);

            // 이름 하단 — Figma (842) 섹션 로컬 310 근방, 카드 좌측 12px 여백
            var nameLabel = new Label("");
            nameLabel.style.fontSize = 13f;
            nameLabel.style.color = new StyleColor(TextMain);
            nameLabel.style.position = Position.Absolute;
            nameLabel.style.left = 12f; nameLabel.style.bottom = 12f;
            nameLabel.style.width = w - 24f;
            card.Add(nameLabel);
            return nameLabel;
        }

        private void BuildRightPanel(VisualElement root)
        {
            var panel = new VisualElement { name = "DetailBetPanel" };
            panel.style.position = Position.Absolute;
            panel.style.left = 568f; panel.style.top = 78f;
            panel.style.width = 968f; panel.style.height = 882f;
            panel.style.backgroundColor = new StyleColor(UTKTheme.GlassPanel);   // [125차 후속12] 글래스 판 — 게임 화면 비침
            // [144차 P1 #6] 우판 테두리 — 좌판과 동일 진실치 #30363D@0.502 폭 1.0(기존 불투명 0.5f 폐기), r14 유지
            panel.style.borderTopWidth = panel.style.borderBottomWidth =
                panel.style.borderLeftWidth = panel.style.borderRightWidth = 1f;
            panel.style.borderTopColor = panel.style.borderBottomColor =
                panel.style.borderLeftColor = panel.style.borderRightColor = new StyleColor(StrokeAlpha);
            panel.style.borderTopLeftRadius = panel.style.borderTopRightRadius =
                panel.style.borderBottomLeftRadius = panel.style.borderBottomRightRadius = 14f;
            root.Add(panel);

            // [144차 P6-BET #1] 우판 닫기 버튼 — Figma 255:654: 판 로컬 (914,24) 30×30 r6 #191F27 + × 아이콘.
            //   Esc 닫기(OnRootKeyDown)와 동일 경로 — Hide() 호출(기존 닫기 로직 재사용, 무손상).
            //   × 전용 PNG가 Resources/UI/icons에 없어 글리프 라벨 대체(기록 — 아이콘 에셋 추가 시 교체).
            var closeBtn = new VisualElement { name = "CloseButton" };
            closeBtn.style.position = Position.Absolute;
            closeBtn.style.left = 914f; closeBtn.style.top = 24f;
            closeBtn.style.width = 30f; closeBtn.style.height = 30f;
            closeBtn.style.backgroundColor = new StyleColor(CardBg);
            closeBtn.style.borderTopWidth = closeBtn.style.borderBottomWidth =
                closeBtn.style.borderLeftWidth = closeBtn.style.borderRightWidth = 1f;
            closeBtn.style.borderTopColor = closeBtn.style.borderBottomColor =
                closeBtn.style.borderLeftColor = closeBtn.style.borderRightColor = new StyleColor(StrokeAlpha);
            closeBtn.style.borderTopLeftRadius = closeBtn.style.borderTopRightRadius =
                closeBtn.style.borderBottomLeftRadius = closeBtn.style.borderBottomRightRadius = 6f;
            closeBtn.style.justifyContent = Justify.Center;
            closeBtn.style.alignItems = Align.Center;
            var closeGlyph = new Label("×");
            closeGlyph.style.fontSize = 14f;
            closeGlyph.style.color = new StyleColor(TextSub);
            closeGlyph.pickingMode = PickingMode.Ignore;
            closeBtn.Add(closeGlyph);
            closeBtn.RegisterCallback<ClickEvent>(_ => Hide());
            panel.Add(closeBtn);

            // ── 선수 정보 섹션 (24,24) 920×371 ──
            MakeLabel(panel, "선수 정보", 22f, TextMain, 24f, 24f);
            MakeLabel(panel, "선택한 선수의 전투 능력 및 2인 난투 베팅 조건", 11f, TextSub, 157f, 24f);   // [144차 P1 #30] y 34→24(Figma 255:669)
            // [144차 P6-BET #6] 선택 선수 개요 카드 — Figma 255:658: 판 로컬 (24,90) 920×144 #191F27 r10
            //   + 문장(크레스트) 94×110. 기존 판에 직접 배치하던 이름/출신/태그 라벨을 카드 내부 좌표로
            //   재배치(값 실데이터 유지 — RefreshDetail 바인딩 무손상). 이름 fs23 700은 Figma 255:660 진실치(#33 근사 동반).
            var overview = new VisualElement { name = "FighterOverviewCard" };
            overview.style.position = Position.Absolute;
            overview.style.left = 24f; overview.style.top = 90f;
            overview.style.width = 920f; overview.style.height = 144f;
            overview.style.backgroundColor = new StyleColor(CardBg);
            overview.style.borderTopLeftRadius = overview.style.borderTopRightRadius =
                overview.style.borderBottomLeftRadius = overview.style.borderBottomRightRadius = 10f;
            panel.Add(overview);

            // 문장(크레스트) 프레임 94×110 — 카드 로컬 (24,17) 세로 중앙 근사. shield PNG 부재 → 프레임만(기록, [144차 P6-BET #2] 동일).
            var ovCrest = new VisualElement { name = "OverviewCrest" };
            ovCrest.style.position = Position.Absolute;
            ovCrest.style.left = 24f; ovCrest.style.top = 17f;
            ovCrest.style.width = 94f; ovCrest.style.height = 110f;
            ovCrest.style.backgroundColor = new StyleColor(CrestBg);
            ovCrest.style.borderTopWidth = ovCrest.style.borderBottomWidth =
                ovCrest.style.borderLeftWidth = ovCrest.style.borderRightWidth = 1f;
            ovCrest.style.borderTopColor = ovCrest.style.borderBottomColor =
                ovCrest.style.borderLeftColor = ovCrest.style.borderRightColor = new StyleColor(StrokeAlpha);
            ovCrest.style.borderTopLeftRadius = ovCrest.style.borderTopRightRadius =
                ovCrest.style.borderBottomLeftRadius = ovCrest.style.borderBottomRightRadius = 6f;
            overview.Add(ovCrest);

            // "선택 선수" 배지 — Figma 255:660 상세 행(레벨 칩·배지 정밀 좌표는 #33 후속 P0 범위, 여기선 프레임+표시 전환만).
            _detailStateChip = new VisualElement { name = "SelectedFighterChip" };
            _detailStateChip.style.position = Position.Absolute;
            _detailStateChip.style.left = 130f; _detailStateChip.style.top = 6f;
            _detailStateChip.style.width = 74f; _detailStateChip.style.height = 20f;
            _detailStateChip.style.backgroundColor = new StyleColor(new Color(Accent.r, Accent.g, Accent.b, 0.078f));
            _detailStateChip.style.borderTopLeftRadius = _detailStateChip.style.borderTopRightRadius =
                _detailStateChip.style.borderBottomLeftRadius = _detailStateChip.style.borderBottomRightRadius = 4f;
            _detailStateChip.style.justifyContent = Justify.Center;
            _detailStateChip.style.alignItems = Align.Center;
            _detailStateChip.style.display = DisplayStyle.None;
            var chipLabel = new Label("선택 선수");
            chipLabel.style.fontSize = 10f;
            chipLabel.style.color = new StyleColor(Accent);
            chipLabel.pickingMode = PickingMode.Ignore;
            _detailStateChip.Add(chipLabel);
            overview.Add(_detailStateChip);

            // [144차 P6-BET #6] 이름/출신/태그 → 개요 카드 내부 좌표 재배치(값 실데이터 유지 — RefreshDetail 무손상).
            _detailName = MakeLabel(overview, "선수를 선택하세요", 23f, TextMain, 130f, 30f, 600f);
            _detailName.style.unityFontStyleAndWeight = FontStyle.Bold;
            _detailOrigin = MakeLabel(overview, "", 12f, TextSub, 130f, 68f, 560f);
            _detailOrigin.style.whiteSpace = WhiteSpace.Normal;
            _detailTagline = MakeLabel(overview, "", 11f, TextSub, 130f, 100f, 560f);

            // [144차 P1 #34] 선택 배율 블록 — Figma 255:760: 판 로컬 (808,123) 120×78, 우측 정렬 right=928.
            MakeLabel(panel, "승리 시 배율", 11f, TextSub, 872f, 123f, 56f, TextAnchor.UpperRight);
            _detailOddsBig = MakeLabel(panel, "×0.00", 30f, Accent, 838f, 143f, 90f, TextAnchor.UpperRight);
            MakeLabel(panel, "2인 난투 승리 시 적용", 10f, TextSub, 839f, 189f, 89f, TextAnchor.UpperRight);

            // [144차 P6-BET #7] 선수 기록 요약 3칸 — Figma 255:765/770/775: 판 로컬 (24,252) 298.7×64 ×3 r10
            //   #191F27 + #30363D@0.502, 카드 간격 311(≒298.7+12). 내부: 트로피 19×19 (12,23) + 라벨 fs10 (43,15)
            //   + 값 fs15 (43,31). 실데이터 바인딩: 통산 전적=wins/losses, 통산 승률=WinRate(ArenaBetFighter 실측).
            //   "최근 5경기"는 모델에 경기 이력 필드 부재 → '—' 표시(가짜 통계 금지 계약 — 기록).
            string[] recordTitles = { "통산 전적", "통산 승률", "최근 5경기" };
            float[] recordX = { 24f, 335f, 645f };
            for (int i = 0; i < 3; i++)
            {
                var rec = new VisualElement { name = $"RecordCard_{i}" };
                rec.style.position = Position.Absolute;
                rec.style.left = recordX[i]; rec.style.top = 252f;
                rec.style.width = 298.7f; rec.style.height = 64f;
                rec.style.backgroundColor = new StyleColor(CardBg);
                rec.style.borderTopWidth = rec.style.borderBottomWidth =
                    rec.style.borderLeftWidth = rec.style.borderRightWidth = 1f;
                rec.style.borderTopColor = rec.style.borderBottomColor =
                    rec.style.borderLeftColor = rec.style.borderRightColor = new StyleColor(StrokeAlpha);
                rec.style.borderTopLeftRadius = rec.style.borderTopRightRadius =
                    rec.style.borderBottomLeftRadius = rec.style.borderBottomRightRadius = 10f;
                panel.Add(rec);
                BuildTrophyGlyph(rec, 12f, 23f);   // trophy 벡터 유실 → 절차 글리프 대체(기록 — #2 크레스트 선례)
                MakeLabel(rec, recordTitles[i], 10f, TextSub, 43f, 15f);
                _recordValues[i] = MakeLabel(rec, "—", 15f, TextMain, 43f, 31f, 240f);
            }

            // [144차 P6-BET #8] 전투 능력치 — Figma 255:781~255:802: 제목 fs15 700 (24,334) + 보조 fs10 (791,334),
            //   목록 (24,364) 가로 3열 gap24 각 290.7×31: 라벨 fs11 좌측 + 수치 fs14 Geist Mono 우측(right=열+290.7)
            //   + 게이지 290.7×4 r2 트랙 #20262E/필 Accent(y391). 기존 세로 3행(240×6 r3 white@0.08) 폐기.
            //   실데이터 바인딩 유지(RefreshDetail 무손상) — 라벨/수치 분리("공격 0" → "공격력" + "88 / 100").
            var statTitle = MakeLabel(panel, "전투 능력치", 15f, TextMain, 24f, 334f);
            statTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            MakeLabel(panel, "기본 능력 기준 · 2인 난투 · 최대 100", 10f, TextSub, 791f, 334f, 153f, TextAnchor.UpperRight);
            float[] statX = { 24f, 338.7f, 653.4f };
            string[] statNames = { "공격력", "방어력", "민첩력" };
            for (int i = 0; i < 3; i++)
            {
                var lab = MakeLabel(panel, statNames[i], 11f, TextSub, statX[i], 364f, 80f);
                var val = Mono(MakeLabel(panel, "0 / 100", 14f, TextMain, statX[i] + 222.7f, 364f, 68f, TextAnchor.UpperRight));
                var bar = MakeStatBar(panel, statX[i], 391f);
                if (i == 0) { _statAtk = lab; _statValAtk = val; _barAtk = bar; }
                else if (i == 1) { _statDef = lab; _statValDef = val; _barDef = bar; }
                else { _statAgi = lab; _statValAgi = val; _barAgi = bar; }
            }

            // ── 베팅 정보 섹션 (24,413) 920×445 — 테두리 박스 ──
            var bet = new VisualElement { name = "BetSection" };
            bet.style.position = Position.Absolute;
            bet.style.left = 24f; bet.style.top = 413f;
            bet.style.width = 920f; bet.style.height = 445f;
            bet.style.borderTopWidth = bet.style.borderBottomWidth =
                bet.style.borderLeftWidth = bet.style.borderRightWidth = 1f;   // [144차 P1 동일 진실치] Figma 255:803 sw1.0(기존 0.5f 폐기)
            bet.style.borderTopColor = bet.style.borderBottomColor =
                bet.style.borderLeftColor = bet.style.borderRightColor = new StyleColor(StrokeAlpha);   // [144차 P1 #15 동일 진실치] Figma 255:803 #30363D@0.502(보고서 행37 PASS 표기였으나 불투명 Stroke는 괴리 — 알파만 정합)
            bet.style.borderTopLeftRadius = bet.style.borderTopRightRadius =
                bet.style.borderBottomLeftRadius = bet.style.borderBottomRightRadius = 9.6f;
            panel.Add(bet);

            MakeLabel(bet, "승리 베팅", 16f, TextMain, 24f, 19f);
            // [144차 P1 #40] 보유 골드 — Figma 255:904/811/812: coins 15×15 (780,21) + 라벨 fs11 (802,22)
            //   + 값 fs14 (851,20) right=919. coins 벡터 유실 → money.png(TextSub 틴트) 대체(기록 — InteractionPanelUTK 'coins→money' 선례).
            var goldIcon = new VisualElement { name = "GoldIcon" };
            goldIcon.style.position = Position.Absolute;
            goldIcon.style.left = 780f; goldIcon.style.top = 21f;
            goldIcon.style.width = 15f; goldIcon.style.height = 15f;
            var goldIconTex = Resources.Load<Texture2D>("UI/icons/money");
            if (goldIconTex != null)
            {
                goldIcon.style.backgroundImage = new StyleBackground(goldIconTex);
                goldIcon.style.unityBackgroundImageTintColor = new StyleColor(TextSub);
            }
            goldIcon.pickingMode = PickingMode.Ignore;
            bet.Add(goldIcon);
            MakeLabel(bet, "보유 골드", 11f, TextSub, 802f, 22f, 43f, TextAnchor.UpperRight);
            _goldLabel = MakeLabel(bet, "0 G", 14f, TextMain, 851f, 20f, 68f, TextAnchor.UpperRight);

            // [144차 P6-BET #5] 베팅 섹션 상태 배지 — Figma 255:724: 섹션 로컬 (73,18) 120×21 α0.078 fs11.
            //   실데이터 바인딩: "{선수명} · 2인 난투"(RefreshDetail). 미선택 시 비표시. r4 근사(기록 — Figma r 미기재).
            var badge = new VisualElement { name = "BetStateBadge" };
            badge.style.position = Position.Absolute;
            badge.style.left = 73f; badge.style.top = 18f;
            badge.style.width = 120f; badge.style.height = 21f;
            badge.style.backgroundColor = new StyleColor(new Color(Accent.r, Accent.g, Accent.b, 0.078f));
            badge.style.borderTopLeftRadius = badge.style.borderTopRightRadius =
                badge.style.borderBottomLeftRadius = badge.style.borderBottomRightRadius = 4f;
            badge.style.justifyContent = Justify.Center;
            badge.style.alignItems = Align.Center;
            badge.style.display = DisplayStyle.None;
            _badgeLabel = new Label("");
            _badgeLabel.style.fontSize = 11f;
            _badgeLabel.style.color = new StyleColor(TextMain);
            _badgeLabel.pickingMode = PickingMode.Ignore;
            badge.Add(_badgeLabel);
            _badge = badge;
            bet.Add(badge);

            // [144차 P6-BET #9] 금액 입력 박스 — Figma 255:814/255:818: bet 로컬 (0,53) 596×72 #0F141B +
            //   액센트 테두리 r6. 기존 캡션("베팅 금액")/값 라벨을 박스 내부로 통합, 값 fs25 Geist Mono (14,30).
            //   Figma 원문은 캡션행(53)+박스(74) 596×51 분리 — 지시 계약에 따라 통합 박스로 구현(기록).
            //   표시 전용 요소 — 클릭 편집 신설 금지(금액 변경은 기존 빠른 금액 칩 경로만 유지).
            var amountBox = new VisualElement { name = "AmountInputBox" };
            amountBox.style.position = Position.Absolute;
            amountBox.style.left = 0f; amountBox.style.top = 53f;
            amountBox.style.width = 596f; amountBox.style.height = 72f;
            amountBox.style.backgroundColor = new StyleColor(InputBg);
            amountBox.style.borderTopWidth = amountBox.style.borderBottomWidth =
                amountBox.style.borderLeftWidth = amountBox.style.borderRightWidth = 1f;
            amountBox.style.borderTopColor = amountBox.style.borderBottomColor =
                amountBox.style.borderLeftColor = amountBox.style.borderRightColor = new StyleColor(Accent);
            amountBox.style.borderTopLeftRadius = amountBox.style.borderTopRightRadius =
                amountBox.style.borderBottomLeftRadius = amountBox.style.borderBottomRightRadius = 6f;
            amountBox.pickingMode = PickingMode.Ignore;
            bet.Add(amountBox);
            MakeLabel(amountBox, "베팅 금액", 11f, TextSub, 14f, 10f);
            // Figma 255:817 "최소/최대" 안내 우측 — 실제 상한 동적 표기(계약: 베팅 상한 5000 유지, 가짜 10,000 표기 금지)
            _minMaxLabel = MakeLabel(amountBox, $"최소 100 G · 최대 {QuickAmounts[QuickAmounts.Length - 1]:N0} G", 10f, TextSub, 479f, 11f, 117f, TextAnchor.UpperRight);
            _amountBoxValue = Mono(MakeLabel(amountBox, "1,000 G", 25f, TextMain, 14f, 30f));

            // [144차 P6-BET #9] 빠른 금액 칩 — Figma 255:821~831 위치 (616,53) 캡션 + 버튼 (616,74) 71.5×51 gap6.
            //   기존 (24,53)/(110+i×130,50) 배치는 신규 입력 박스와 충돌 → Figma 좌표로 이동(충돌 해결 필수).
            //   문구("+100"/"최대")·테두리 등 잔여 세부는 보고서 #43(P1) 후속 범위 — 현재 문구 유지(기록).
            MakeLabel(bet, "금액 빠른 설정", 11f, TextSub, 616f, 53f);
            for (int i = 0; i < QuickAmounts.Length; i++)
                BuildAmountChip(bet, QuickAmounts[i], 616f + i * 77.5f, 74f);

            // [144차 P6-BET #10] 예상 수령액 계산 카드 — Figma 255:832: bet 로컬 (0,139) 920×105 #1A2839 +
            //   stroke #58A6FF@0.251 r10. 기존 bet 직접 배치(캡션/값/×/=/배율/수령액/순이익)를 카드 로컬 좌표로
            //   재배치(bet 좌표 −139). 값 라벨 Geist Mono, 배율/수령액 #58A6FF(Figma 진실치), 수령액 좌정렬.
            var payoutCard = new VisualElement { name = "PayoutCard" };
            payoutCard.style.position = Position.Absolute;
            payoutCard.style.left = 0f; payoutCard.style.top = 139f;
            payoutCard.style.width = 920f; payoutCard.style.height = 105f;
            payoutCard.style.backgroundColor = new StyleColor(CardBgSel);
            payoutCard.style.borderTopWidth = payoutCard.style.borderBottomWidth =
                payoutCard.style.borderLeftWidth = payoutCard.style.borderRightWidth = 1f;
            payoutCard.style.borderTopColor = payoutCard.style.borderBottomColor =
                payoutCard.style.borderLeftColor = payoutCard.style.borderRightColor =
                    new StyleColor(new Color(Accent.r, Accent.g, Accent.b, 0.251f));
            payoutCard.style.borderTopLeftRadius = payoutCard.style.borderTopRightRadius =
                payoutCard.style.borderBottomLeftRadius = payoutCard.style.borderBottomRightRadius = 10f;
            bet.Add(payoutCard);
            MakeLabel(payoutCard, "베팅 금액", 11f, TextSub, 16f, 29f);
            _amountLabel = Mono(MakeLabel(payoutCard, "1,000 G", 21f, TextMain, 16f, 49f));
            MakeLabel(payoutCard, "×", 22f, TextDim, 184f, 38f);
            MakeLabel(payoutCard, "승리 배율", 11f, TextSub, 218f, 29f);
            _oddsCalcLabel = Mono(MakeLabel(payoutCard, "0.00", 21f, Accent, 218f, 49f));
            MakeLabel(payoutCard, "=", 22f, TextDim, 362f, 38f);
            MakeLabel(payoutCard, "승리 시 예상 수령액 · 원금 포함", 11f, Accent, 761f, 15f, 143f, TextAnchor.UpperRight);   // [144차 P1 #48] #58A6FF(Figma 255:842)
            _payoutLabel = Mono(MakeLabel(payoutCard, "0 G", 32f, Accent, 779f, 32f));   // 좌정렬(Figma 255:843) — 폭 미지정 자동 확장
            _profitLabel = MakeLabel(payoutCard, "예상 순이익 +0 G", 10f, TextSub, 813f, 78f, 91f, TextAnchor.UpperRight);   // [144차 P1 #50] Figma 255:844 (813,78) w91 — 우측 정렬 right=904 유지

            // [144차 P6-BET #11] 베팅 확인 카드 — Figma 255:846: bet 로컬 (0,333) 920×80 r10 #191F27 +
            //   #30363D@0.502. circle-check 15×15 (16,23)(tick.png Accent 틴트 — #4 선례) + 요약 fs13 700 (38,23)
            //   + 잔액 문구 fs11 (16,45). 요약 문구는 기존 생성 로직(RefreshCalc — 선택 선수명) 재사용.
            var confirmCard = new VisualElement { name = "BetConfirmCard" };
            confirmCard.style.position = Position.Absolute;
            confirmCard.style.left = 0f; confirmCard.style.top = 333f;
            confirmCard.style.width = 920f; confirmCard.style.height = 80f;
            confirmCard.style.backgroundColor = new StyleColor(CardBg);
            confirmCard.style.borderTopWidth = confirmCard.style.borderBottomWidth =
                confirmCard.style.borderLeftWidth = confirmCard.style.borderRightWidth = 1f;
            confirmCard.style.borderTopColor = confirmCard.style.borderBottomColor =
                confirmCard.style.borderLeftColor = confirmCard.style.borderRightColor = new StyleColor(StrokeAlpha);
            confirmCard.style.borderTopLeftRadius = confirmCard.style.borderTopRightRadius =
                confirmCard.style.borderBottomLeftRadius = confirmCard.style.borderBottomRightRadius = 10f;
            bet.Add(confirmCard);
            var confirmTick = new VisualElement { name = "ConfirmCheck" };
            confirmTick.style.position = Position.Absolute;
            confirmTick.style.left = 16f; confirmTick.style.top = 23f;
            confirmTick.style.width = 15f; confirmTick.style.height = 15f;
            var confirmTickTex = Resources.Load<Texture2D>("UI/icons/tick");
            if (confirmTickTex != null)
            {
                confirmTick.style.backgroundImage = new StyleBackground(confirmTickTex);
                confirmTick.style.unityBackgroundImageTintColor = new StyleColor(Accent);
            }
            confirmTick.pickingMode = PickingMode.Ignore;
            confirmCard.Add(confirmTick);
            _summaryLabel = MakeLabel(confirmCard, "베팅할 선수를 선택하세요", 13f, TextMain, 38f, 23f, 560f);
            _summaryLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            // [144차 P1 #52] 잔액 문구 — Figma 255:851 bet(16,378) → 확인 카드 로컬 (16,45) = 현재치 일치(보고서 행52의 "355"는 오기 — JSON 원문 기준 기록).
            _balanceLabel = MakeLabel(confirmCard, "베팅 후 잔액 — · 2인 난투 배율 —", 11f, TextSub, 16f, 45f, 620f);

            // [144차 P6-BET #12] 베팅 유의사항 — Figma 255:855: bet 로컬 info 13×13 (0,427) + 문구 fs11 (20,427).
            //   JSON 255:857 원문 문구 그대로. _errorLabel(동적 오류)은 별도 유지 계약 — Figma 미정의 게임 전용
            //   요소로 수령액 카드와 확인 카드 사이 공백(24,310)에 배치(기존 (24,405)는 확인 카드 영역과 충돌).
            BuildInfoGlyph(bet, 0f, 427f);
            MakeLabel(bet, "2인 난투 베팅 확정 시 골드가 즉시 차감되며 취소할 수 없습니다. 선택 선수 패배 시 베팅 금액을 잃습니다", 11f, TextSub, 20f, 427f, 900f);
            _errorLabel = MakeLabel(bet, "", 11f, new Color(0xF8 / 255f, 0x71 / 255f, 0x71 / 255f), 24f, 310f, 640f);

            _confirmButton = UTKButton.Create("2인 난투 1,000 G 베팅하기", OnConfirm, UTKButton.Variant.Primary);
            _confirmButton.style.position = Position.Absolute;
            _confirmButton.style.left = 660f; _confirmButton.style.top = 18f;   // [144차 P6-BET #11] 확인 카드 내 Figma 255:852 (660,18) — 기존 right/bottom 앵커는 카드 영역과 충돌
            // [144차 P1 #53] 확정 버튼 244×44 좌표 정합 확인(255:852 일치). coins 16×16 (버튼 로컬 35,14) + fs13 좌정렬 텍스트는
            //   UTKButton 중앙 정렬 구조 변경이 필요 → 스킵(기록 — 기존 인터랙션 무손상 계약 우선).
            _confirmButton.style.width = 244f; _confirmButton.style.height = 44f;
            confirmCard.Add(_confirmButton);
        }

        private void BuildAmountChip(VisualElement parent, int amount, float x, float y)
        {
            var chip = new VisualElement { name = $"AmountChip_{amount}" };
            chip.style.position = Position.Absolute;
            chip.style.left = x; chip.style.top = y;
            // [144차 P6-BET #9] 칩 크기/스타일 — Figma 255:824~831: 71.5×51 r6 #191F27 + #30363D@0.502.
            chip.style.width = 71.5f; chip.style.height = 51f;
            chip.style.backgroundColor = new StyleColor(CardBg);
            chip.style.borderTopWidth = chip.style.borderBottomWidth =
                chip.style.borderLeftWidth = chip.style.borderRightWidth = 1f;
            chip.style.borderTopColor = chip.style.borderBottomColor =
                chip.style.borderLeftColor = chip.style.borderRightColor = new StyleColor(StrokeAlpha);
            chip.style.borderTopLeftRadius = chip.style.borderTopRightRadius =
                chip.style.borderBottomLeftRadius = chip.style.borderBottomRightRadius = 6f;
            chip.RegisterCallback<ClickEvent>(_ => SetAmount(amount));
            parent.Add(chip);
            // [144차 P1 #43] 칩 문구 — Figma 255:825/827/829/831: "+100/+500/+1,000/최대" fs12(유지).
            var l = new Label(amount >= QuickAmounts[QuickAmounts.Length - 1] ? "최대" : $"+{amount:N0}");
            l.style.fontSize = 12f;
            l.style.color = new StyleColor(TextMain);
            l.style.position = Position.Absolute;
            l.style.left = 0f; l.style.top = 0f; l.style.right = 0f; l.style.bottom = 0f;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            chip.Add(l);
        }

        // [144차 P6-BET #8] 능력치 게이지 — Figma 255:789/790: 트랙 290.7×4 r2 #20262E, 필 Accent r2(폭=값/100×290.7).
        private VisualElement MakeStatBar(VisualElement parent, float x, float y)
        {
            var track = new VisualElement { name = "StatTrack" };
            track.style.position = Position.Absolute;
            track.style.left = x; track.style.top = y;
            track.style.width = 290.7f; track.style.height = 4f;
            track.style.backgroundColor = new StyleColor(CrestBg);
            track.style.borderTopLeftRadius = track.style.borderTopRightRadius =
                track.style.borderBottomLeftRadius = track.style.borderBottomRightRadius = 2f;
            parent.Add(track);
            var fill = new VisualElement { name = "StatFill" };
            fill.style.position = Position.Absolute;
            fill.style.left = 0f; fill.style.top = 0f;
            fill.style.width = 0f; fill.style.height = 4f;
            fill.style.backgroundColor = new StyleColor(Accent);
            fill.style.borderTopLeftRadius = fill.style.borderTopRightRadius =
                fill.style.borderBottomLeftRadius = fill.style.borderBottomRightRadius = 2f;
            track.Add(fill);
            return fill;
        }

        // [144차 P6-BET #12] info 글리프 13×13 — Figma 255:913 원형 벡터 유실 → 원형 테두리 + "i" 절차 글리프
        //   대체(감사 §판정 불가 선례, SleepPanelUTK.BuildInfoGlyph 동일 레시피 — P1 #28 호출부 누락 정의 보완).
        private static VisualElement BuildInfoGlyph(VisualElement parent, float x, float y)
        {
            var host = new VisualElement { name = "InfoGlyph" };
            host.style.position = Position.Absolute;
            host.style.left = x; host.style.top = y;
            host.style.width = 13f; host.style.height = 13f;
            host.style.borderTopWidth = host.style.borderBottomWidth = 1.5f;
            host.style.borderLeftWidth = host.style.borderRightWidth = 1.5f;
            host.style.borderTopColor = host.style.borderBottomColor = new StyleColor(TextSub);
            host.style.borderLeftColor = host.style.borderRightColor = new StyleColor(TextSub);
            host.style.borderTopLeftRadius = host.style.borderTopRightRadius = 6.5f;
            host.style.borderBottomLeftRadius = host.style.borderBottomRightRadius = 6.5f;
            host.pickingMode = PickingMode.Ignore;
            var dot = new VisualElement();
            dot.style.width = 2f; dot.style.height = 2f;
            dot.style.backgroundColor = new StyleColor(TextSub);
            dot.style.position = Position.Absolute;
            dot.style.left = 5.5f; dot.style.top = 3f;
            dot.pickingMode = PickingMode.Ignore;
            host.Add(dot);
            var stem = new VisualElement();
            stem.style.width = 2f; stem.style.height = 4.5f;
            stem.style.backgroundColor = new StyleColor(TextSub);
            stem.style.position = Position.Absolute;
            stem.style.left = 5.5f; stem.style.top = 6f;
            stem.pickingMode = PickingMode.Ignore;
            host.Add(stem);
            parent.Add(host);
            return host;
        }

        // [144차 P6-BET #7] trophy 글리프 19×19 — Figma 255:895 벡터 유실 → 컵/받침 절차 글리프 대체(기록).
        private static VisualElement BuildTrophyGlyph(VisualElement parent, float x, float y)
        {
            var host = new VisualElement { name = "TrophyGlyph" };
            host.style.position = Position.Absolute;
            host.style.left = x; host.style.top = y;
            host.style.width = 19f; host.style.height = 19f;
            host.pickingMode = PickingMode.Ignore;
            var bowl = new VisualElement();   // 컵 몸통(U자 개방 테두리)
            bowl.style.width = 11f; bowl.style.height = 8f;
            bowl.style.borderTopWidth = bowl.style.borderLeftWidth = bowl.style.borderRightWidth = 1.6f;
            bowl.style.borderTopColor = bowl.style.borderLeftColor = bowl.style.borderRightColor = new StyleColor(TextSub);
            bowl.style.borderTopLeftRadius = bowl.style.borderTopRightRadius = 2f;
            bowl.style.position = Position.Absolute;
            bowl.style.left = 4f; bowl.style.top = 2f;
            bowl.pickingMode = PickingMode.Ignore;
            host.Add(bowl);
            var stem = new VisualElement();   // 받침대
            stem.style.width = 2f; stem.style.height = 4f;
            stem.style.backgroundColor = new StyleColor(TextSub);
            stem.style.position = Position.Absolute;
            stem.style.left = 8.5f; stem.style.top = 10f;
            stem.pickingMode = PickingMode.Ignore;
            host.Add(stem);
            var base_ = new VisualElement();
            base_.style.width = 9f; base_.style.height = 2f;
            base_.style.backgroundColor = new StyleColor(TextSub);
            base_.style.position = Position.Absolute;
            base_.style.left = 5f; base_.style.top = 14.5f;
            base_.pickingMode = PickingMode.Ignore;
            host.Add(base_);
            parent.Add(host);
            return host;
        }

        private void BuildHelpBar(VisualElement root)
        {
            var help = new VisualElement { name = "HelpBar" };
            help.style.position = Position.Absolute;
            help.style.left = 64f; help.style.top = 984f;
            help.style.width = 1472f; help.style.height = 20f;
            help.style.backgroundColor = new StyleColor(UTKTheme.GlassPanelSoft);   // [125차 후속12] 글래스 스트립
            root.Add(help);
            MakeLabel(help, "선수를 선택해 전투 정보와 예상 수령액을 확인하세요", 12f, TextSub, 0f, 3f, 500f);
            // [144차 P1 #56] 도움말 단축키 — Figma: (1202,3) w270 원문 "클릭 선수 선택 · Enter 베팅 확인 · Esc 닫기".
            MakeLabel(help, "클릭 선수 선택 · Enter 베팅 확인 · Esc 닫기", 12f, TextSub, 1202f, 3f, 270f, TextAnchor.UpperRight);
        }

        // ═══════════════ 상호작용 ═══════════════

        private void SelectFighter(bool west)
        {
            if (_pair == null) return;
            _selected = west ? _pair.west : _pair.east;
            RefreshFighterCards();
            RefreshDetail();
            RefreshCalc();
        }

        private void SetAmount(int amount)
        {
            _betAmount = Mathf.Max(0, amount);
            RefreshCalc();
        }

        private void OnConfirm()
        {
            if (_pair == null) return;
            if (_selected == null)
            {
                _errorLabel.text = "먼저 베팅할 선수를 선택하세요.";
                return;
            }
            if (ArenaBettingSystem.PlaceBetAndStart(_selected, _betAmount, _nationStyle, out string error))
            {
                Debug.Log("[ArenaWindowUTK] 베팅 확정 — 창 닫힘 → 자동 투기장 전환");
                Hide();
            }
            else
            {
                _errorLabel.text = error ?? "베팅에 실패했습니다.";
            }
        }

        private void OnRootKeyDown(KeyDownEvent evt)
        {
            if (!IsOpen) return;
            if (evt.keyCode == KeyCode.Escape) { Hide(); evt.StopPropagation(); }
            else if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) { OnConfirm(); evt.StopPropagation(); }
        }

        // ═══════════════ 갱신 ═══════════════

        protected override void OnWindowOpen()
        {
            RefreshAll();
            _pollTask = schedule.Execute(PollRefresh).Every(1000).StartingIn(1000);
            schedule.Execute(ApplyCanvasFit).StartingIn(16);
            schedule.Execute(ApplyCanvasFit).StartingIn(120);
            schedule.Execute(ApplyCanvasFit).StartingIn(400);
        }

        protected override void OnWindowClosed()
        {
            if (_pollTask != null) { _pollTask.Pause(); _pollTask = null; }
        }

        /// <summary>ApplyCanvasFit — 음식점창 동일 등배수 계약(raw 1600×1040 × k 센터링).</summary>
        private void ApplyCanvasFit()
        {
            var rootEl = UIToolkitBootstrap.UIRoot;
            if (rootEl == null) return;
            float rootW = rootEl.resolvedStyle.width;
            float rootH = rootEl.resolvedStyle.height;
            if (rootW < 1f || rootH < 1f) return;
            float k = Mathf.Min(rootW / WinW, rootH / WinH);
            style.left = (rootW - WinW * k) * 0.5f;
            style.top = (rootH - WinH * k) * 0.5f;
            style.width = WinW * k;
            style.height = WinH * k;
            // [125차 후속11] 원점 좌상단 고정 — 음식점창(L360)/훈련소창(L166) 선례 계약. 구 코드는 scale만
            //   설정해 중심 원점 스케일이 적용됐고, 1600×1040 콘텐츠가 자기 중심 기준으로 축소되면서
            //   좌상단이 우하단으로 밀려남 → 배경/요소가 창 밖으로 삐져나오고 우측 텍스트 잘림
            //   (아레나 테스트 2 영상 12초 프레임 실측). transformOrigin(0,0)으로 선례와 동일 정합.
            _content.style.transformOrigin = new TransformOrigin(0f, 0f, 0f);
            _content.style.scale = new StyleScale(new Scale(new Vector2(k, k)));
        }

        private void PollRefresh()
        {
            RefreshClock();
            _goldLabel.text = $"{(PlayerStats.Instance?.Gold ?? 0):N0} G";
        }

        private void RefreshClock()
        {
            if (_dayLabel == null || _clockLabel == null) return;   // [125차 후속13] 헤더바 제거 — 라벨 부재 널가드
            var tm = TimeManager.Instance;
            if (tm == null)
            {
                _dayLabel.text = "—";
                _clockLabel.text = "--:--";
                return;
            }
            _dayLabel.text = $"{tm.CurrentDay}일차 · {(tm.IsDay ? "낮" : "밤")}";
            _clockLabel.text = $"{tm.Hour:00}:{tm.Minute:00}";
        }

        private void RefreshAll()
        {
            RefreshFighterCards();
            RefreshDetail();
            RefreshCalc();
            RefreshClock();
            _goldLabel.text = $"{(PlayerStats.Instance?.Gold ?? 0):N0} G";
        }

        private void RefreshFighterCards()
        {
            if (_pair == null) return;
            ApplyCard(0, _pair.west, _pair.oddsWest, _cardWest);
            ApplyCard(1, _pair.east, _pair.oddsEast, _cardEast);
            // [125차 후속13] 초상화 카드 이름 = 실제 페어 데이터(위조 금지 계약 — Figma 샘플명 미사용)
            if (_portraitName1 != null) _portraitName1.text = _pair.west.name;
            if (_portraitName2 != null) _portraitName2.text = _pair.east.name;
            RefreshPortraitBorders();   // [125차 후속20] 선택 상태 → 초상화 파란 테두리
            _guideRightLabel.text = "총 2명 · 단일 승자";
        }

        /// <summary>[125차 후속20] 초상화 카드 선택 테두리 — 사용자 지시: UI에서 병사 선택 시
        ///   밑의 병사 그림(초상화 카드)에도 파란 테두리로 선택됨을 표시. 카드 핸들은 BuildPortraitCard 저장.</summary>
        private void RefreshPortraitBorders()
        {
            if (_pair == null) return;
            bool selWest = _selected == _pair.west;
            bool selEast = _selected == _pair.east;
            ApplyPortraitBorder(_portraitCardWest, selWest);
            ApplyPortraitBorder(_portraitCardEast, selEast);
        }

        private static void ApplyPortraitBorder(VisualElement card, bool selected)
        {
            if (card == null) return;
            // [144차 P1 #27] 사진 카드 선택 테두리 — Figma: Accent 1.5 / 비선택 #30363D@0.502(기존 2.5 폐기).
            Color c = selected ? Accent : StrokeAlpha;
            card.style.borderTopColor = card.style.borderBottomColor =
                card.style.borderLeftColor = card.style.borderRightColor = new StyleColor(c);
            card.style.borderTopWidth = card.style.borderBottomWidth =
                card.style.borderLeftWidth = card.style.borderRightWidth = selected ? 1.5f : 1f;
        }

        private void ApplyCard(int index, ArenaBetFighter f, float odds, VisualElement card)
        {
            bool selected = _selected == f;
            _cardNames[index].text = f.name;
            _cardOdds[index].text = $"×{odds:0.00}";
            _cardRecords[index].text = f.RecordText;
            _cardStats[index].text = $"공격 {f.attack:0} · 방어 {f.defense:0} · 민첩 {f.agility:0}";
            // 태그 라인 — 카드 내 5번째 라벨(tagline)
            var tag = card.Q<Label>();
            // 위 MakeLabel 순서: name/odds/record/stats/tagline/state — Q<Label>()는 첫 라벨(name)이라 태그는 인덱스 접근
            Label tagLabel = null;
            int seen = 0;
            foreach (var child in card.Children())
            {
                if (child is Label l)
                {
                    seen++;
                    if (seen == 5) { tagLabel = l; break; }
                }
            }
            if (tagLabel != null) tagLabel.text = f.tagline;
            _cardStates[index].text = selected ? "선택됨" : "선택 가능";
            _cardStates[index].style.color = new StyleColor(selected ? Accent : TextSub);

            // [144차 P1 #14/15/16] 카드 상태 스타일 — Figma 255:673(미선택)/255:689(선택):
            //   미선택 #191F27 + #30363D@0.502 폭1 / 선택 #1A2839 + Accent 폭1.5.
            card.style.backgroundColor = new StyleColor(selected ? CardBgSel : CardBg);
            Color borderColor = selected ? Accent : StrokeAlpha;
            card.style.borderTopColor = card.style.borderBottomColor =
                card.style.borderLeftColor = card.style.borderRightColor = new StyleColor(borderColor);
            card.style.borderTopWidth = card.style.borderBottomWidth =
                card.style.borderLeftWidth = card.style.borderRightWidth = selected ? 1.5f : 1f;
        }

        private void RefreshDetail()
        {
            if (_selected == null)
            {
                _detailName.text = "선수를 선택하세요";
                _detailOrigin.text = "";
                _detailTagline.text = "";
                _detailOddsBig.text = "×0.00";
                SetBar(_barAtk, 0f); SetBar(_barDef, 0f); SetBar(_barAgi, 0f);
                // [144차 P6-BET #8] 라벨/수치 분리 — 수치 라벨(_statVal*)만 갱신, 라벨(_stat*) 고정
                _statValAtk.text = "0 / 100"; _statValDef.text = "0 / 100"; _statValAgi.text = "0 / 100";
                // [144차 P6-BET #7] 기록 요약 — 미선택 시 '—'(가짜 통계 금지)
                _recordValues[0].text = "—"; _recordValues[1].text = "—"; _recordValues[2].text = "—";
                return;
            }
            var f = _selected;
            _detailName.text = f.name;
            _detailOrigin.text = f.origin;
            _detailTagline.text = f.tagline;
            float odds = _pair.west == f ? _pair.oddsWest : _pair.oddsEast;
            _detailOddsBig.text = $"×{odds:0.00}";
            // [144차 P6-BET #8] 수치 형식 — Figma 255:788 원문 "88 / 100" Geist Mono fs14
            _statValAtk.text = $"{f.attack:0} / 100";
            _statValDef.text = $"{f.defense:0} / 100";
            _statValAgi.text = $"{f.agility:0} / 100";
            SetBar(_barAtk, f.attack); SetBar(_barDef, f.defense); SetBar(_barAgi, f.agility);
            // [144차 P6-BET #7] 기록 요약 실데이터 바인딩 — ArenaBetFighter.wins/losses/WinRate 실측.
            //   "최근 5경기"는 모델에 경기 이력 부재 → '—' 유지(가짜 통계 금지 — 기록).
            _recordValues[0].text = $"{f.wins}승 {f.losses}패";
            _recordValues[1].text = $"{f.WinRate * 100f:0}%";
            _recordValues[2].text = "—";
        }

        private static void SetBar(VisualElement fill, float value)
        {
            if (fill == null) return;
            float w = Mathf.Clamp(value, 0f, 100f) / 100f * 290.7f;   // [144차 P6-BET #8] 게이지 폭 240→290.7(Figma 255:790)
            fill.style.width = w;
        }

        private void RefreshCalc()
        {
            float odds = 0f;
            if (_pair != null && _selected != null)
                odds = _pair.west == _selected ? _pair.oddsWest : _pair.oddsEast;
            _amountLabel.text = $"{_betAmount:N0} G";
            _amountBoxValue.text = _amountLabel.text;   // [144차 P6-BET #9] 입력 박스 값 — 동일 실데이터 동기화
            _oddsCalcLabel.text = odds > 0f ? $"{odds:0.00}" : "—";
            int payout = odds > 0f ? Mathf.RoundToInt(_betAmount * odds) : 0;
            _payoutLabel.text = odds > 0f ? $"{payout:N0} G" : "— G";
            int profit = odds > 0f ? payout - _betAmount : 0;
            _profitLabel.text = odds > 0f ? $"예상 순이익 +{profit:N0} G" : "예상 순이익 —";
            int gold = PlayerStats.Instance?.Gold ?? 0;
            _balanceLabel.text = odds > 0f
                ? $"베팅 후 잔액 {Mathf.Max(0, gold - _betAmount):N0} G · 2인 난투 배율 ×{odds:0.00} 적용"
                : "베팅 후 잔액 — · 선수를 선택하세요";
            // [144차 P6-BET #11] 확인 카드 요약 — 기존 문구 생성 로직(선택 선수명) 재사용(Figma 255:850 fs13 700)
            _summaryLabel.text = odds > 0f
                ? $"{_selected.name}의 2인 난투 승리에 베팅합니다"
                : "베팅할 선수를 선택하세요";
            _confirmButton.text = odds > 0f ? $"2인 난투 {_betAmount:N0} G 베팅하기" : "선수를 선택하세요";
        }
    }
}
