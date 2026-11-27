// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 114
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core.Data;   // TerritoryDatabase, TerritoryDefinition, NationType, TerritoryDifficulty, TerritoryOwnership, TerritoryState

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U4 Round A-1 — 월드맵 (양피지 + 영지/플레이어 마커 + M키 토글).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md (Phase U4 전략/영지)
    /// 원본: Assets/Scripts/UI/WorldMapWindow.cs (1087줄 IMGUI) — 절대 수정 금지. 이 파일은 별도 신규 구현.
    ///
    /// [원본 실측 → UTK 이식]
    ///  - 정규화 좌표: u = 0.5 + worldPos.x / 3200f, v = 0.5 + worldPos.z / 3200f (WORLD_SPAN=3200f, extent 1600m).
    ///    UI 좌표: left% = u*100, top% = (1-v)*100 (GUI y 반전 — +z 북 = 위).
    ///  - 마커는 지도 캔버스 위 절대배치(Length.Percent)로 — 캔버스 크기 무관 비율 유지.
    ///  - 영지 데이터: TerritoryDatabase.GetAllDefinitions() / GetState(id).ownership
    ///  - 색/크기 헬퍼(난이도별 크기, 국가색, 소유 표기)는 원본과 동일 산식.
    ///    런타임에 비활성화(원본 소스 비수정 — 런타임 조율)하고 자체 schedule 폴링으로 M키를 처리.
    ///  - 양피지 텍스처: 원본 BuildParchment(절차 결정론 노이즈)를 동일 산식으로 복제(외부 에셋 bg_paper.png 없음 실측).
    ///
    /// [검증 의무 완료]
    ///  - IMGUI 렌더링 루프 미사용, 컴포넌트 클래스 public(TerritoryMarker).
    ///  - IStyle 셧스루헛 없음 — margin/padding/border 4면 개별 속성.
    ///  - 폴링: schedule.Execute(Action).Every(ms) → IVisualElementScheduledItem, Pause() 대신 부착 유지.
    /// </summary>
    public class WorldMapWindowUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static WorldMapWindowUTK _instance;
        public static WorldMapWindowUTK Instance => _instance;

        /// <summary>월드맵 open (팩토리 겸용). M키/외부 호출용.</summary>
        public static void Open()
        {
            Ensure();
            _instance.Show();
        }

        /// <summary>토글 (닫혀 있으면 열고, 열려 있으면 닫음).</summary>
        public static void Toggle()
        {
            Ensure();
            _instance.ToggleWindow();
        }

        /// <summary>
        /// 생성 가드. UIRoot에 부착(display:NONE 유지) + 스케줄 폴링 기동 + 원본 핫키 런타임 조율.
        /// M키가 닫힌 상태에서도 동작해야 하므로 Insure에서 계속 부착해 둔다(숨김=display None).
        /// </summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new WorldMapWindowUTK();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null) root.Add(_instance);
            _instance.StartLoops();
            SuppressLegacyHotkey();
            UnityEngine.Debug.Log("[WorldMapUTK] 월드맵 UTK 생성 (UIRoot 부착 + M키 폴링 기동)");
        }

        // ===== 설정 =====
        private const float WinW = 1920f;
        private const float WinH = 1080f;

        /// <summary>정규화 분모 = 2×extent. u = 0.5 + x/WORLD_SPAN (원본 실측: 3200f, extent 1600m).</summary>
        private static readonly float WORLD_SPAN = 3200f;

        private const long KeyMs = 50L;    // M키/ESC 폴링
        private const long PollMs = 250L;  // 상태(소유/펄스/플레이어) 갱신

        // ===== 원본 정규화 산식 (실측 후 동일 적용) =====
        private static float WorldU(Vector3 p) => 0.5f + p.x / WORLD_SPAN;
        private static float WorldV(Vector3 p) => 0.5f + p.z / WORLD_SPAN;

        // ===== 상태 =====
        private readonly VisualElement _mapCanvas;
        private readonly VisualElement _playerMarker;
        private VisualElement _playerRing;      // [124차-ImplB] 플레이어 핀 외곽 링(53×53 헤일로) — 구 _playerHalo 폐기
        private Label _playerPinLabel;          // [124차-ImplB] 플레이어 핀 라벨 "{영지명} (현재지)"
        private readonly List<TerritoryMarker> _territoryMarkers = new List<TerritoryMarker>(96);
        private TerritoryDefinition _selectedDef;   // [124차-ImplB] 선택 영지 (핀/지역 행 클릭) — struct라 _hasSelection 플래그 병행
        private TerritoryMarker _selectedMarker;    // [124차-ImplB] 선택 핀 강조 대상
        private bool _trackingEnabled = true;       // [124차-ImplB] 지도 상 실시간 좌표 추적 토글(기본 ON — Figma knob 오른쪽)
        private bool _hasSelection;                 // [124차-ImplB] 선택 여부 — TerritoryDefinition은 struct(CS0019 방지)
        private VisualElement _intelPanel;          // [124차-ImplB] 우측 QuestDetailPanel 루트
        private Label _intelName;                   // 영지명 (QuestHeroHeader)
        private Label _intelBadge;                  // 소유 상태 배지 (r4.8 fill 소유색)
        private Label _intelSubtitle;               // 부제 = def.description
        private Label _intelBriefing;               // "해당 구역 주요 상태" 본문
        private Label _intelLordValue;              // 영주 이름
        private Label _intelSoldierValue;           // 병사 수
        private Label _intelAvgValue;               // 병사 평균레벨
        private Label _intelPowerValue;             // 전투력
        private VisualElement _intelKnob;           // 추적 토글 knob (ON=오른쪽 x26.4 / OFF=좌측 x2.4)

        // [124차 Phase A] HUDHeader CoordinatesGroup 라벨 — 구 위치줄(_locLabel/_coordLabel) 폐기 후 데이터 경로 승계
        private Label _hudTerritoryNameLabel;   // 현재 영지명 (기존 위치줄 소스: TerritoryDatabase.ResolveTerritoryAt 실시간)
        private Label _hudCoordValueLabel;      // "X: {x:F2} / Y: {z:F2}" (플레이어 transform.position)
        // [124차 Phase A] Figma 아트 배경 2레이어 — _content 소속(_mapCanvas 자식 아님: RebuildMarkers Clear() 보호)
        private VisualElement _bgArtHost;
        private VisualElement _bgLayer1;
        private VisualElement _bgOverlay;
        private VisualElement _bgLayer2;
        // [124차 Phase A] 좌측 QuestListPanel (탭 전환 + 퀘스트 플레이스홀더 + 지역 실데이터 리스트)
        private ScrollView _questSlots;
        private VisualElement _questPlaceholderRoot;
        private VisualElement _regionListRoot;
        private Button _questTab;
        private Button _regionTab;
        private bool _regionTabActive;   // false = 퀘스트 탭 활성(Figma 기본 상태)
        private readonly List<RegionRowView> _regionRows = new List<RegionRowView>(96);

        // [F5] TacticalCompass 나침반
        private readonly VisualElement _compass;
        private readonly Label _compassN;
        private VisualElement _compassRose;

        private UnityEngine.UIElements.IVisualElementScheduledItem _keyTask;
        private UnityEngine.UIElements.IVisualElementScheduledItem _pollTask;

        private Transform _playerTransform;
        private float _nextPlayerProbe;

        // 원본 핫키 런타임 조율 플래그 (소스 무수정)
        private static bool _legacyHotkeySuppressed;

        // =====================================================================
        //  [Figma GitHub-dark 리스타일] 월드맵 시각 스펙 — 기능 무수정, 이 창 한정 인라인 오버라이드.
        //  Theme.uss / 공용 UTKWindowBase·타 UTK 창은 절대 수정하지 않는다.
        //  창 크롬/헤더/마커 가시 요소는 GitHub-dark 팔레트로 — 단, 지도 양피지 배경 텍스처는
        //  사용자 확정에 따라 그대로 유지(제거/교체 없음). 좌표 산식·텍스처 로직·마커 데이터 무수정.
        //  =====================================================================
        private static class GitHubDark
        {
            public static readonly Color Panel    = Hex(0x161B22);   // 창 본체 패널
            public static readonly Color PanelSub = Hex(0x21262D);   // 보조 패널(타이틀바/범례 스트립)
            public static readonly Color Accent   = Hex(0x58A6FF);   // 강조 — 플레이어 마커/펄스
            public static readonly Color Gold     = Hex(0xE3B341);   // 몬스터/영지 마커 강조(★ 내 영지·황제국)
            public static readonly Color TextMain = Hex(0xF0F6FC);   // 기본 텍스트
            public static readonly Color TextSub  = Hex(0x8B949E);   // 보조 텍스트(범례)
            public static readonly Color Stroke   = Hex(0x2E343D);   // 테두리/지도 프레임 stroke

            // [124차 Phase A] 소유 상태 계약색(지역 리스트/Impl-B 핀·배지 공용) — 플레이어=#39D353/분쟁=#FF9E2C.
            //   영주 소유=기존 빨강 ColorWar 재사용, 무주=TextSub(#8B949E) 재사용 — OwnershipContractColor 참조.
            public static readonly Color StatusGreen  = Hex(0x39D353);
            public static readonly Color StatusOrange = Hex(0xFF9E2C);

            private static Color Hex(uint rgb) =>
                new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);
        }

        // 전쟁(Contested) 펄스 붉은 테두리 — 범례 문구("붉은 테두리")와 정합, 스펙 불명이라 원본 톤 유지
        private static readonly Color ColorWar = new Color(0.90f, 0.18f, 0.14f, 1f);

        // =====================================================================
        //  생성
        // =====================================================================
        private WorldMapWindowUTK() : base("🗺️ 포이즌 월드맵", new Vector2(WinW, WinH))
        {
            SetChrome(UTKWindowChrome.Frameless);
            _content.style.position = Position.Relative;
            _content.style.width = WinW;
            _content.style.height = WinH;

            // [124차 Phase A] 폐기: 범례 스트립(구 L134~163 블록) — Figma 95:167은 범례를 아트 배경 장식으로만 보유(계획서 진실치).
            //   배경 2레이어 IMAGE가 그 대체 — 아래 EnsureBackgroundLayers() 참조.

            // [124차 Phase A] 폐기: 위치줄 글래스 스트립(121차 — hudRow + _locLabel/_coordLabel 블록).
            //   Figma 정합: 위치 데이터 경로는 신설 HUDHeader CoordinatesGroup으로 승계(BuildHudHeader 참조).

            // [124차 Phase A] Figma 아트 배경 2레이어 — _content 소속(마커 RebuildMarkers Clear()로부터 보호)
            EnsureBackgroundLayers();

            // 지도 캔버스 — Figma 아트 2레이어 배경 위 마커 절대배치 호스트
            _mapCanvas = new VisualElement();
            _mapCanvas.name = "MapCanvas";
            _mapCanvas.AddToClassList("wm-map-canvas");
            // [117차 R1] 지도 캔버스 = 창 전체 — flow(flexGrow) 폐기, absolute inset 0.
            //   범례/위치줄이 flow에서 지도를 아래로 밀어 상단 36.8%가 빈 공간이던 실측(스크린샷 픽셀 분석) 제거.
            //   [124차 Phase A] 두 스트립은 Figma 정합으로 폐기되어 오버레이 부재 — 계약 기록 주석만 유지.
            _mapCanvas.style.position = Position.Absolute;
            _mapCanvas.style.left = 0f;
            _mapCanvas.style.top = 0f;
            _mapCanvas.style.right = 0f;
            _mapCanvas.style.bottom = 0f;
            // [124차 Phase A] 배경 교체 — 양피지 절차 텍스처(GetParchment) 이미지 배경 폐기 → Figma 아트 2레이어
            //   (EnsureBackgroundLayers: WorldMapBackground(95:168) 위 SOLID #0B0E14/0.45 오버레이 위 ParchmentTextureBackground(97:5)).
            _mapCanvas.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
            // [124차 Phase A] 지형 베이크 호출 폐기 — 구 CreateTerrainOverlay() 호출부 제거
            //   (메서드 자체와 WorldMapTerrainBaker.cs는 잔존 — [124차] 복귀 가능성 대비 표기).
            //   Figma 아트 2레이어(그려진 지형/지명/나침반/범례)가 절차 지형 장식을 대체 — 마커는 실제 좌표 배치 유지.
            // CreateTerrainOverlay();   // [124차 Phase A] 폐기 — Figma 아트 2레이어 배경으로 대체(메서드 잔존)
            _mapCanvas.style.borderLeftWidth = 2f;
            _mapCanvas.style.borderRightWidth = 2f;
            _mapCanvas.style.borderTopWidth = 2f;
            _mapCanvas.style.borderBottomWidth = 2f;
            _mapCanvas.style.borderLeftColor = new StyleColor(GitHubDark.Stroke);
            _mapCanvas.style.borderRightColor = new StyleColor(GitHubDark.Stroke);
            _mapCanvas.style.borderTopColor = new StyleColor(GitHubDark.Stroke);
            _mapCanvas.style.borderBottomColor = new StyleColor(GitHubDark.Stroke);
            _mapCanvas.style.overflow = Overflow.Hidden;
            _content.Add(_mapCanvas);

            // [124차 Phase A] HUDHeader — (0,0) 1920×77 하단보더 #30363D/0.50 w1.2 (좌 SystemGroup + 우 CoordinatesGroup)
            BuildHudHeader();

            // [124차 Phase A] 좌측 QuestListPanel — (48,109) 456×888 r14.4 (FilterTabs + 퀘스트 플레이스홀더 + 지역 실데이터 리스트)
            BuildQuestListPanel();

            // [124차-ImplB] 우측 QuestDetailPanel — (1392,109) 480×888 (선택 지역 정보/TACTICAL INTEL + 영주 정보/Fast Travel)
            BuildQuestDetailPanel();

            // [F5] TacticalCompass — 우상단 원형 나침반(N 고정) + 플레이어 yaw 회전 장미
            _compass = new VisualElement();
            _compass.name = "TacticalCompass";
            _compass.style.position = Position.Absolute;
            _compass.style.width = 192f;
            _compass.style.height = 192f;
            _compass.style.left = 696f;
            _compass.style.top = 168f;
            _compass.style.right = StyleKeyword.Auto;
            _compass.style.backgroundColor = new StyleColor(GitHubDark.PanelSub);
            _compass.style.borderTopWidth = 1f; _compass.style.borderBottomWidth = 1f;
            _compass.style.borderLeftWidth = 1f; _compass.style.borderRightWidth = 1f;
            _compass.style.borderTopColor = new StyleColor(GitHubDark.Stroke);
            _compass.style.borderBottomColor = new StyleColor(GitHubDark.Stroke);
            _compass.style.borderLeftColor = new StyleColor(GitHubDark.Stroke);
            _compass.style.borderRightColor = new StyleColor(GitHubDark.Stroke);
            _compass.style.borderTopLeftRadius = 32f; _compass.style.borderTopRightRadius = 32f;
            _compass.style.borderBottomLeftRadius = 32f; _compass.style.borderBottomRightRadius = 32f;
            _compass.pickingMode = PickingMode.Ignore;
            // [116차 R1] 나침반을 디자인 공간(_content) 소속으로 이동 — raw 좌표(696,168)가 k 스케일을
            //   자동 태도록(구: 창 루트 직속 + ApplyCanvasBounds 수동 sx/sy — 스케일 이중화 원천 제거).
            _content.Add(_compass);
            _compass.style.display = DisplayStyle.None;   // [118차 R2] 방위 표시 폐기 — 사용자 지시 "방위 표시 없이 지도만"

            _compassRose = new VisualElement();
            _compassRose.style.flexGrow = 1f;
            _compassRose.style.alignItems = Align.Center;
            _compassRose.style.justifyContent = Justify.Center;
            _compassRose.pickingMode = PickingMode.Ignore;
            _compass.Add(_compassRose);

            _compassN = new Label("N");
            _compassN.style.position = Position.Absolute;
            _compassN.style.left = 0f; _compassN.style.right = 0f;
            _compassN.style.top = 4f;
            _compassN.style.fontSize = 14.4f;
            _compassN.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            _compassN.style.unityTextAlign = TextAnchor.MiddleCenter;
            _compassN.style.color = new StyleColor(GitHubDark.TextMain);
            _compassN.pickingMode = PickingMode.Ignore;
            _compass.Add(_compassN);

            // 컨테이너 폰트 (이미지 배경/Label용) — 생성 시점 레이어 포함
            ApplyUIToolkitFont(this);

            // [125차-38] 플레이어 핀 — Figma 95:238 '지역 마커 · 현재지' 2026-10-26T03:19Z 재실측 진실치:
            // PinBase 38.4×38.4 r19.2 fill #39D353/0.10 + stroke #39D353 w1.8, 내부 원 14.4 solid #39D353,
            // 외부 펄스 링 52.8 stroke #39D353 w1.2(핀 밖 넘침 — UTK overflow 기본 visible, 클립 없음),
            // PinLabel h=24.6 fill #161B22/0.80 + stroke #30363D/0.50 w1.2 r4.8, 텍스트 15px "{영지명} (현재지)" #F0F6FC.
            // 위치식=BuildTerritoryMarker/아레나 핀 공용 WorldU/WorldV 단일 소스(left%=u*100, top%=(1-v)*100),
            // 라벨=TerritoryDatabase.ResolveTerritoryAt 실시간(UpdateHeaderAndCompass), 250ms PollTick 갱신 계약.
            // 앵커 = PinBase 중심(루트 translate(-50%, -19.2px)) — 구형 점+헤일로 렌더 폐기.
            _playerMarker = new VisualElement();
            _playerMarker.name = "PlayerMarker";
            _playerMarker.style.position = Position.Absolute;
            _playerMarker.style.translate = new Translate(Length.Percent(-50f), new Length(-19.2f, LengthUnit.Pixel));
            _playerMarker.style.flexDirection = FlexDirection.Column;
            _playerMarker.style.alignItems = Align.Center;
            _playerMarker.style.display = DisplayStyle.None;
            _playerMarker.pickingMode = PickingMode.Ignore;

            var pBase = new VisualElement { name = "PlayerPinBase" };
            pBase.pickingMode = PickingMode.Ignore;
            pBase.style.width = 38.4f;   // [125차-38] 재실측 진실치(구 38)
            pBase.style.height = 38.4f;
            SetCornerRadius(pBase, 19.2f);
            Color pGreen = GitHubDark.StatusGreen;   // #39D353 — 소유색 계약 플레이어 상수 재사용
            Color pFill = pGreen; pFill.a = 0.10f;
            pBase.style.backgroundColor = new StyleColor(pFill);
            var pStroke = new StyleColor(pGreen);
            pBase.style.borderTopWidth = 1.8f; pBase.style.borderBottomWidth = 1.8f;
            pBase.style.borderLeftWidth = 1.8f; pBase.style.borderRightWidth = 1.8f;
            pBase.style.borderTopColor = pStroke; pBase.style.borderBottomColor = pStroke;
            pBase.style.borderLeftColor = pStroke; pBase.style.borderRightColor = pStroke;
            _playerMarker.Add(pBase);

            var pDot = new VisualElement { name = "PlayerPinDot" };
            pDot.pickingMode = PickingMode.Ignore;
            pDot.style.position = Position.Absolute;
            pDot.style.left = 12f; pDot.style.top = 12f;   // (38.4-14.4)/2 = 12 — 중앙 정렬
            pDot.style.width = 14.4f; pDot.style.height = 14.4f;   // [125차-38] 내부 원 14.4 재실측
            SetCornerRadius(pDot, 7.2f);
            pDot.style.backgroundColor = new StyleColor(pGreen);
            pBase.Add(pDot);

            _playerRing = new VisualElement { name = "PlayerPinRing" };
            _playerRing.pickingMode = PickingMode.Ignore;
            _playerRing.style.position = Position.Absolute;
            _playerRing.style.left = -7.2f; _playerRing.style.top = -7.2f;   // (38.4-52.8)/2 — 핀 밖 넘침 링
            _playerRing.style.width = 52.8f; _playerRing.style.height = 52.8f;   // [125차-38] 외부 펄스 링 52.8 재실측(구 53)
            SetCornerRadius(_playerRing, 26.4f);
            _playerRing.style.borderTopWidth = 1.2f; _playerRing.style.borderBottomWidth = 1.2f;
            _playerRing.style.borderLeftWidth = 1.2f; _playerRing.style.borderRightWidth = 1.2f;
            var ringC = new StyleColor(pGreen);
            _playerRing.style.borderTopColor = ringC; _playerRing.style.borderBottomColor = ringC;
            _playerRing.style.borderLeftColor = ringC; _playerRing.style.borderRightColor = ringC;
            pBase.Add(_playerRing);

            _playerPinLabel = new Label("—");
            _playerPinLabel.name = "PlayerPinLabel";
            _playerPinLabel.style.fontSize = 13.2f;   // [144차 P1-WM] 144차 재실측 진실치 13.2(95:241) — 구 15 정정
            _playerPinLabel.style.height = 24.6f;   // PinLabel h=24.6 — 고정 높이 + MiddleCenter 수직 정합
            _playerPinLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _playerPinLabel.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            _playerPinLabel.style.color = new StyleColor(GitHubDark.TextMain);
            _playerPinLabel.style.backgroundColor = new StyleColor(new Color(0x16 / 255f, 0x1B / 255f, 0x22 / 255f, 0.80f));
            _playerPinLabel.style.paddingLeft = 10f; _playerPinLabel.style.paddingRight = 10f;
            _playerPinLabel.style.paddingTop = 0f; _playerPinLabel.style.paddingBottom = 0f;   // h24.6 고정 — 수직 여백은 정합 정렬로
            SetCornerRadius(_playerPinLabel, 4.8f);
            var lblStroke = new StyleColor(new Color(0x30 / 255f, 0x36 / 255f, 0x3D / 255f, 0.50f));
            _playerPinLabel.style.borderTopWidth = 1.2f; _playerPinLabel.style.borderBottomWidth = 1.2f;
            _playerPinLabel.style.borderLeftWidth = 1.2f; _playerPinLabel.style.borderRightWidth = 1.2f;
            _playerPinLabel.style.borderTopColor = lblStroke; _playerPinLabel.style.borderBottomColor = lblStroke;
            _playerPinLabel.style.borderLeftColor = lblStroke; _playerPinLabel.style.borderRightColor = lblStroke;
            _playerPinLabel.style.marginTop = 8f;
            _playerPinLabel.style.whiteSpace = WhiteSpace.NoWrap;
            _playerPinLabel.pickingMode = PickingMode.Ignore;
            _playerMarker.Add(_playerPinLabel);
            _mapCanvas.Add(_playerMarker);

            // 창 크롬(본체/타이틀바/닫기) GitHub-dark 리스타일 — 생성 시 1회
            ApplyGitHubDarkWindowStyle();

            // 기본 숨김 + 중앙 배치 (드래그 전까지)
            style.display = DisplayStyle.None;
            ApplyCanvasBounds();
        }

        private void ApplyCanvasBounds()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;
            float rw = root.resolvedStyle.width, rh = root.resolvedStyle.height;
            if (rw <= 1f || rh <= 1f || float.IsNaN(rw) || float.IsNaN(rh)) return;
            // [117차 R1] 월드맵 = 풀스크린 오버레이(피그마 패리티 창 아님 — 사용자 지시 "화면 전체 창에 지도").
            //   116차 등배수(ApplyDesignSpace) 대체: 게임뷰가 16:9가 아니면 창 하단에 게임이 노출되던 실측
            //   (k=0.750 root=1440×909 → 창 810px, 하단 99px 노출)을 창=루트 전체(비등비)로 소멸시킨다.
            //   디자인 공간(_content)은 raw 1920×1080 유지 + 축별 스케일 — authored 좌표(나침반 696,168 등) 보존.
            style.position = Position.Absolute;
            style.left = 0f; style.top = 0f;
            style.width = rw; style.height = rh;
            style.minWidth = rw; style.minHeight = rh;
            _content.style.width = 1920f;
            _content.style.height = 1080f;
            // [127차] 플렉스 수축 금지 — 뿌리 수리. _content(position=Relative, height 1080)는 윈도우 본체의
            //   기본 컬럼 플렉스 자식이라 flex-shrink 기본값 1이 본체 가용 높이(~909)로 눌러버린다. 그러면
            //   배경 호스트(inset 0)도 909에 클립 → 1920×1080 배경 아트 하단 171px 소실(2026-10-27 ui4 실측:
            //   양피지가 화면 84.2%에서 절단+그 아래 창 본체 #161B22 노출 — 3레벨 수치 전부 이 모델로 정합).
            //   디자인 공간 1920×1080 보존이 [117차 R1] 축별 스케일 계약의 전제 — 수축 원천 차단.
            _content.style.flexShrink = 0f;
            _content.style.flexGrow = 0f;
            _content.style.scale = new Scale(new Vector2(rw / 1920f, rh / 1080f));
            _content.style.transformOrigin = new TransformOrigin(Length.Percent(0f), Length.Percent(0f));
            Debug.Log($"[117차 R1] 월드맵 풀스크린 — root=({rw:F0}x{rh:F0}) 창=루트 전체(비등비), 지도 캔버스=창 전체");
        }

        /// <summary>[127차] 개방 후 레이아웃 프로브(1회, 500ms) — root/창/_content/배경/캔버스 resolved 실측 덤프.
        /// 플렉스 수축 등 레이아웃 간섭이 배경 클립을 재현하는지 수치로 확정하는 1급 단서(ConstructionUTK DOM 진단 선례).</summary>
        private void LogLayoutProbe()
        {
            var root = UIToolkitBootstrap.UIRoot;
            var sb = new System.Text.StringBuilder("[127차] 월드맵 레이아웃 프로브 —");
            sb.Append($" root=({root?.resolvedStyle.width:F0}x{root?.resolvedStyle.height:F0})");
            sb.Append($" window=({resolvedStyle.width:F0}x{resolvedStyle.height:F0})");
            sb.Append($" content=({_content.resolvedStyle.width:F0}x{_content.resolvedStyle.height:F0} shrink={_content.resolvedStyle.flexShrink})");
            sb.Append($" bgHost=({_bgArtHost?.resolvedStyle.width:F0}x{_bgArtHost?.resolvedStyle.height:F0})");
            sb.Append($" bgL1=({_bgLayer1?.resolvedStyle.width:F0}x{_bgLayer1?.resolvedStyle.height:F0})");
            sb.Append($" mapCanvas=({_mapCanvas.resolvedStyle.width:F0}x{_mapCanvas.resolvedStyle.height:F0})");
            var lp = this.Q("QuestListPanel");
            var dp = this.Q("QuestDetailPanel");
            if (lp != null) sb.Append($" listPanel=(y{lp.resolvedStyle.top:F0}+{lp.resolvedStyle.height:F0})");
            if (dp != null) sb.Append($" detailPanel=(y{dp.resolvedStyle.top:F0}+{dp.resolvedStyle.height:F0})");
            Debug.Log(sb.ToString());
        }

        /// <summary>창 크롬(본체/타이틀바/닫기버튼) GitHub-dark 리스타일 — 이 창 한정 인라인 오버라이드(생성 시 1회).</summary>
        private void ApplyGitHubDarkWindowStyle()
        {
            // 창 본체: 우드 베이크 이미지/브론즈 베벨 2px → 다크 패널 + 1px 스트로크 + r8 (이 창에서만.
            // 지도 양피지 배경(_mapCanvas backgroundImage)은 유지 — 본체 크롬만 교체)
            style.backgroundColor = new StyleColor(new Color32(0x0B, 0x0E, 0x14, 0xFF));   // [144차 P1-WM #4] #161B22→#0B0E14 (95:167 루트 fill)
            style.backgroundImage = new StyleBackground(StyleKeyword.None);
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 1f;
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = GitHubDark.Stroke;
            // [144차 P1-WM #5] 루트 코너 r 없음(0) — r8 폐기
            style.borderTopLeftRadius = 0f;
            style.borderTopRightRadius = 0f;
            style.borderBottomLeftRadius = 0f;
            style.borderBottomRightRadius = 0f;
            style.color = GitHubDark.TextMain;   // 명시색 없는 라벨 상속색 — 기본 텍스트

            // 타이틀 바(헤더): 보조 패널 #21262D + 하단 1px 스트로크 (상단 코너 r8 — 창 클리핑 정합)
            var titleBar = this.Q("TitleBar");
            if (titleBar != null)
            {
                titleBar.style.backgroundColor = GitHubDark.PanelSub;
                titleBar.style.borderTopLeftRadius = 8f;
                titleBar.style.borderTopRightRadius = 8f;
                titleBar.style.borderBottomWidth = 1f;
                titleBar.style.borderBottomColor = GitHubDark.Stroke;
            }

            // 타이틀 라벨: 기본 텍스트
            if (_titleLabel != null)
                _titleLabel.style.color = GitHubDark.TextMain;

            // 닫기 버튼: [125차-38] Figma 진실치 — CloseButton 32×32 fill #161B22@0.80 r=16(구 PanelSub r4).
            // 베이크 텍스처 미사용 — 기본 ✕ 텍스트 유지. Frameless 크롬에선 타이틀바가 숨겨지지만
            // Standard 복귀 시에도 Figma 정합치가 적용되도록 창 로컬 오버라이드로 명시.
            var closeBtn = this.Q<Button>("CloseButton");
            if (closeBtn != null)
            {
                closeBtn.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                closeBtn.style.width = 32f;
                closeBtn.style.height = 32f;
                closeBtn.style.backgroundColor = new StyleColor(new Color(0x16 / 255f, 0x1B / 255f, 0x22 / 255f, 0.80f));   // #161B22@0.80 — 패널 글래스 동일 알파 근사
                closeBtn.style.borderTopWidth = closeBtn.style.borderBottomWidth = closeBtn.style.borderLeftWidth = closeBtn.style.borderRightWidth = 0f;
                closeBtn.style.borderTopColor = closeBtn.style.borderBottomColor = closeBtn.style.borderLeftColor = closeBtn.style.borderRightColor = new StyleColor(new Color(0x16 / 255f, 0x1B / 255f, 0x22 / 255f, 0.80f));
                closeBtn.style.borderTopLeftRadius = 16f;
                closeBtn.style.borderTopRightRadius = 16f;
                closeBtn.style.borderBottomLeftRadius = 16f;
                closeBtn.style.borderBottomRightRadius = 16f;           // 원형 칩 r16
                closeBtn.style.color = GitHubDark.TextMain;
            }
        }

        // =====================================================================
        //  생명주기
        // =====================================================================
        public override void Show()
        {
            // Attach before base.Show invokes OnWindowOpen, so canvas-local bounds are applied
            // against an attached window and each actual closed→open transition refreshes once.
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            base.Show();   // Register + 표시 + OnWindowOpen
            EnsureSidePanelWindows();
            _listPanelWindow?.ShowPanel();     // [144차 P0-WM] 패널 창 동기 개방(맵 개방 = 2패널 개복)
            _intelPanelWindow?.ShowPanel();
            UnityEngine.Debug.Log("[WorldMapUTK] 월드맵 열림");
        }

        public override void Hide()
        {
            _listPanelWindow?.HidePanel();     // [144차 P0-WM] 패널 창 동기 닫힘(맵 토글 계약 유지)
            _intelPanelWindow?.HidePanel();
            base.Hide();
            UnityEngine.Debug.Log("[WorldMapUTK] 월드맵 닫힘");
        }

        // [144차 P0-WM #1] 좌/우 패널 독립 창 — 패널 트리 재부착(무재배선) + 개별 CloseButton 단독 닫힘.
        private WorldMapSidePanelWindow _listPanelWindow;
        private WorldMapSidePanelWindow _intelPanelWindow;
        private VisualElement _questListPanel;
        private bool _questListPanelNeedsWindow;
        private bool _intelPanelNeedsWindow;

        private void EnsureSidePanelWindows()
        {
            if (_questListPanelNeedsWindow && _questListPanel != null)
            {
                _questListPanelNeedsWindow = false;
                _listPanelWindow = WorldMapSidePanelWindow.Attach("지역 탐색 리스트", _questListPanel, 48f, 106f);
                AddPanelCloseButton(_questListPanel, 410f, () =>
                {
                    _listPanelWindow?.HidePanel();   // [144차 P0-WM #2] 288:7 — 좌패널 단독 닫기(재개방=맵 토글, 기록)
                });
            }
            if (_intelPanelNeedsWindow)
            {
                _intelPanelNeedsWindow = false;
                _intelPanelWindow = WorldMapSidePanelWindow.Attach("선택 지역 정보", _intelPanel, 1392f, 106f);
                AddPanelCloseButton(_intelPanel, 434f, () =>
                {
                    _intelPanelWindow?.HidePanel();   // [144차 P0-WM #2] 288:9 — 우패널 단독 닫기
                });
            }
        }

        /// <summary>[144차 P0-WM #2] 패널 CloseButton(288:7/9) — 32×32 r16 원형 칩 #161B22@0.8 +
        /// stroke #30363D@0.502 + x-circle 글리프 16 #8B949E(글리프 근사 기록).</summary>
        private static void AddPanelCloseButton(VisualElement panel, float x, System.Action onClick)
        {
            if (panel == null) return;
            var close = new Button(onClick) { name = "PanelCloseButton" };
            close.style.position = Position.Absolute;
            close.style.left = x;
            close.style.top = 14f;
            close.style.width = 32f;
            close.style.height = 32f;
            close.style.backgroundColor = new StyleColor(new Color(0x16 / 255f, 0x1B / 255f, 0x22 / 255f, 0.8f));
            close.style.borderTopLeftRadius = close.style.borderTopRightRadius = 16f;
            close.style.borderBottomLeftRadius = close.style.borderBottomRightRadius = 16f;
            close.style.borderTopWidth = close.style.borderBottomWidth = 1.2f;
            close.style.borderLeftWidth = close.style.borderRightWidth = 1.2f;
            var stroke = new StyleColor(new Color(0x30 / 255f, 0x36 / 255f, 0x3D / 255f, 0.502f));
            close.style.borderTopColor = close.style.borderBottomColor = stroke;
            close.style.borderLeftColor = close.style.borderRightColor = stroke;
            var xIcon = new Label("⊗");   // x-circle 16 — 벡터→글리프 근사(기록)
            xIcon.style.fontSize = 16f;
            xIcon.style.color = new StyleColor(GitHubDark.TextSub);
            xIcon.style.unityTextAlign = TextAnchor.MiddleCenter;
            xIcon.pickingMode = PickingMode.Ignore;
            close.Add(xIcon);
            panel.Add(close);
        }

        private bool _legacyMapPreviouslyOpen;

        private void ToggleWindow()
        {
            if (IsOpen) { Hide(); return; }
            Show();
        }

        // =====================================================================
        //  [124차 Phase A] Figma 정합 (95:167 medieval-world-map-ui) — 배경 2레이어 + HUDHeader + 좌측 QuestListPanel
        // =====================================================================

        /// <summary>[124차 Phase A] Figma 아트 배경 2레이어 — 원본 계층 그대로: WorldMapBackground(95:168) 위
        /// SOLID #0B0E14/0.45 오버레이, 그 위 ParchmentTextureBackground(97:5). 3 VisualElement absolute inset 0,
        /// background-image=Resources.Load, scale 모드 none/1:1 (PNG 실측 1920×1080 = authored 좌표계, 전체 축별 k는
        /// ApplyCanvasBounds 담당). _content 소속 — RebuildMarkers의 _mapCanvas.Clear()로부터 보호, 생성자에서 1회 구축.</summary>
        private void EnsureBackgroundLayers()
        {
            if (_bgArtHost != null) return;
            var tex1 = LoadMapResourceTexture("UI/WorldMap/WorldMapBackground", "95:168");
            var tex2 = LoadMapResourceTexture("UI/WorldMap/ParchmentTextureBackground", "97:5");

            _bgArtHost = new VisualElement { name = "MapBackgroundArt" };
            _bgArtHost.style.position = Position.Absolute;
            _bgArtHost.style.left = 0f; _bgArtHost.style.top = 0f;
            _bgArtHost.style.right = 0f; _bgArtHost.style.bottom = 0f;
            _bgArtHost.pickingMode = PickingMode.Ignore;

            _bgLayer1 = new VisualElement { name = "MapBackgroundLayer1" };
            _bgOverlay = new VisualElement { name = "MapBackgroundOverlay" };
            _bgLayer2 = new VisualElement { name = "MapBackgroundLayer2" };
            ApplyBackgroundImage(_bgLayer1, tex1);
            ApplyBackgroundImage(_bgLayer2, tex2);
            _bgOverlay.style.backgroundColor = new StyleColor(new Color(
                0x0B / 255f, 0x0E / 255f, 0x14 / 255f, 0.45f));   // SOLID #0B0E14/0.45 — 배경1과 배경2 사이
            _bgArtHost.Add(_bgLayer1);
            _bgArtHost.Add(_bgOverlay);
            _bgArtHost.Add(_bgLayer2);
            _content.Insert(0, _bgArtHost);

            if (tex1 == null || tex2 == null)
                Debug.Log("[124차 Phase A] 월드맵 배경 PNG 일부 미로드 — 폴백 흉내 없음(위조 금지), 부재 레이어는 투명(Unity 임포트 전 임시 가능)");
        }

        private static void ApplyBackgroundImage(VisualElement layer, Texture2D tex)
        {
            layer.style.position = Position.Absolute;
            layer.style.left = 0f; layer.style.top = 0f;
            layer.style.right = 0f; layer.style.bottom = 0f;
            layer.pickingMode = PickingMode.Ignore;
            if (tex != null) layer.style.backgroundImage = new StyleBackground(tex);
            // scale 모드 none/1:1 — explicit 1920×1080 px (PNG 실측 — authored 좌표계 유지)
            layer.style.backgroundSize = new BackgroundSize(new Length(1920f), new Length(1080f));
        }

        private static Texture2D LoadMapResourceTexture(string resPath, string figmaNode)
        {
            var tex = Resources.Load<Texture2D>(resPath);
            if (tex == null)
                Debug.LogWarning($"[124차 Phase A] 배경 리소스 미로드: Resources/{resPath} (Figma {figmaNode} export) — 첫 에디터 임포트 전 임시일 수 있음");
            return tex;
        }

        /// <summary>[124차 Phase A] HUDHeader — (0,0) 1920×77, 하단 보더 #30363D/0.50 w1.2.
        /// 좌 SystemGroup(48,29) 299×25: "세계 지도" 21.6/700 #F0F6FC + Divider(104,3) 1×19 #30363D/0.50
        /// + "WORLD MAP SYSTEM"(125,4) 15.6/500 #58A6FF. 우 CoordinatesGroup(1416,31) 456×20:
        /// "현재 위치:" 14.4/400 #8B949E + 현재 영지명(Geist Mono 15.6/700 #39D353) + "좌표:" + 좌표값
        /// "X: {x:F2} / Y: {z:F2}"(Geist Mono 15.6/700 #58A6FF) — 갱신은 UpdateHeaderAndCompass(기존 250ms PollTick 경로 승계).
        /// pickingMode Ignore — 지도 마커 클릭 투과 유지.</summary>
        private void BuildHudHeader()
        {
            var header = new VisualElement { name = "HudHeader" };
            header.style.position = Position.Absolute;
            header.style.left = 0f;
            header.style.top = 0f;
            header.style.width = 1920f;
            header.style.height = 77f;
            // [127차] Figma 95:175 진실치 — HUDHeader fill #0D1117@0.502(반투명 다크 바). 코드가 fill 미설정해
            //   투명 렌더 실측(2026-10-27 ui4: 헤더 영역 양피지 투과) → 백프로 정합 추가.
            header.style.backgroundColor = new StyleColor(new Color(0x0D / 255f, 0x11 / 255f, 0x17 / 255f, 0.502f));
            header.style.borderBottomWidth = 1.2f;
            header.style.borderBottomColor = new StyleColor(new Color(0x30 / 255f, 0x36 / 255f, 0x3D / 255f, 0.50f));
            header.pickingMode = PickingMode.Ignore;

            // 좌 SystemGroup (48,29) 299×25 — 내부 절대배치(43차 선례: 라벨 명시 height + MiddleLeft 수직 정합)
            var systemGroup = new VisualElement { name = "SystemGroup" };
            systemGroup.style.position = Position.Absolute;
            systemGroup.style.left = 48f;
            systemGroup.style.top = 26f;   // [144차 P1-WM #4] 29→26 (ay 상대 26)
            systemGroup.style.width = 299f;
            systemGroup.style.height = 25f;
            systemGroup.pickingMode = PickingMode.Ignore;

            var title = new Label("세계 지도");
            title.style.position = Position.Absolute;
            title.style.left = 0f;
            title.style.top = 0f;
            title.style.height = 25f;
            title.style.fontSize = 21.6f;
            title.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            title.style.color = new StyleColor(GitHubDark.TextMain);
            title.style.unityTextAlign = TextAnchor.MiddleLeft;
            title.pickingMode = PickingMode.Ignore;
            systemGroup.Add(title);

            var divider = new VisualElement { name = "TitleDivider" };
            divider.style.position = Position.Absolute;
            divider.style.left = 104f;
            divider.style.top = 3f;
            divider.style.width = 1f;
            divider.style.height = 19f;
            divider.style.backgroundColor = new StyleColor(new Color(0x30 / 255f, 0x36 / 255f, 0x3D / 255f, 0.50f));
            divider.pickingMode = PickingMode.Ignore;
            systemGroup.Add(divider);

            var subtitle = new Label("WORLD MAP SYSTEM");
            // [144차 P1-WM #12] letterSp 1.2 (95:184)
            subtitle.style.position = Position.Absolute;
            subtitle.style.left = 125f;
            subtitle.style.top = 0f;
            subtitle.style.height = 25f;
            subtitle.style.fontSize = 15.6f;
            subtitle.style.letterSpacing = 1.2f;   // [144차 P1-WM #12] letterSp 1.2 (95:184)
            // Figma 500(SemiBold) — 프로젝트 폰트 weight 한계로 Normal 근사(UIFont 선례: 700만 Bold 표현)
            subtitle.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Normal;
            subtitle.style.color = new StyleColor(GitHubDark.Accent);
            subtitle.style.unityTextAlign = TextAnchor.MiddleLeft;
            subtitle.pickingMode = PickingMode.Ignore;
            systemGroup.Add(subtitle);
            header.Add(systemGroup);

            // 우 CoordinatesGroup (1416,31) 456×20 — Row: 위치 캡션 + 영지명 + 좌표 캡션 + 좌표값
            var coords = new VisualElement { name = "CoordinatesGroup" };
            coords.style.position = Position.Absolute;
            coords.style.left = 1416f;
            coords.style.top = 28f;   // [144차 P1-WM #4] 31→28 (ay 상대 28)
            coords.style.width = 456f;
            coords.style.height = 20f;
            coords.style.flexDirection = FlexDirection.Row;
            coords.style.alignItems = Align.Center;
            coords.pickingMode = PickingMode.Ignore;

            var locCaption = new Label("현재 위치:");
            locCaption.style.fontSize = 14.4f;
            locCaption.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Normal;
            locCaption.style.color = new StyleColor(GitHubDark.TextSub);
            locCaption.style.marginRight = 8f;
            locCaption.style.whiteSpace = WhiteSpace.NoWrap;
            locCaption.pickingMode = PickingMode.Ignore;
            coords.Add(locCaption);

            _hudTerritoryNameLabel = new Label("—");
            _hudTerritoryNameLabel.style.fontSize = 15.6f;
            _hudTerritoryNameLabel.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            _hudTerritoryNameLabel.style.color = new StyleColor(GitHubDark.StatusGreen);   // 계획서 #39D353
            _hudTerritoryNameLabel.style.unityFont = GetGeistMonoFont();
            _hudTerritoryNameLabel.style.marginRight = 10f;
            _hudTerritoryNameLabel.style.whiteSpace = WhiteSpace.NoWrap;
            _hudTerritoryNameLabel.pickingMode = PickingMode.Ignore;
            coords.Add(_hudTerritoryNameLabel);

            var coordCaption = new Label("좌표:");
            coordCaption.style.fontSize = 14.4f;
            coordCaption.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Normal;
            coordCaption.style.color = new StyleColor(GitHubDark.TextSub);
            coordCaption.style.marginRight = 8f;
            coordCaption.style.whiteSpace = WhiteSpace.NoWrap;
            coordCaption.pickingMode = PickingMode.Ignore;
            coords.Add(coordCaption);

            _hudCoordValueLabel = new Label("X: 0.00 / Y: 0.00");
            _hudCoordValueLabel.style.fontSize = 15.6f;
            _hudCoordValueLabel.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            _hudCoordValueLabel.style.color = new StyleColor(GitHubDark.Accent);   // 계획서 #58A6FF
            _hudCoordValueLabel.style.unityFont = GetGeistMonoFont();
            _hudCoordValueLabel.style.whiteSpace = WhiteSpace.NoWrap;
            _hudCoordValueLabel.pickingMode = PickingMode.Ignore;
            coords.Add(_hudCoordValueLabel);
            header.Add(coords);

            _content.Add(header);
        }

        private static Font _geistMono;

        /// <summary>[124차 Phase A] Geist Mono 로드(Figma 패밀리 계약) — 프로젝트에 Geist Mono 에셋 미보유 실측(2026-10-24),
        /// 부재 시 UI 전역 폰트(UIFont.Load — NotoSansKR)로 폴백 렌더. 없는 폰트를 흉내로 채우지 않는 위조 금지 계약에
        /// 따라 폰트 스왑만 기록하며, 에셋 추가(Resources/Fonts/GeistMono) 시 자동 적용된다.</summary>
        private static Font GetGeistMonoFont()
        {
            if (_geistMono == null)
                _geistMono = Resources.Load<Font>("Fonts/GeistMono");
            return _geistMono != null ? _geistMono : UIFont.Load();
        }

        /// <summary>[124차 Phase A] 좌측 QuestListPanel — (48,109) 456×888 r14.4 fill #161B22 stroke #30363D/0.50 w1.2
        /// + Decal 코너 4개(14×14 L자 #58A6FF w1.8) + PanelHeader(24,24) + FilterTabs(24,81) 408×36(퀘스트/지역 클릭 전환)
        /// + QuestSlotsContainer(24,136) 408×728 ScrollView(퀘스트 플레이스홀더 3카드 / 지역 실데이터 행 82).</summary>
        private void BuildQuestListPanel()
        {
            var panel = new VisualElement { name = "QuestListPanel" };
            panel.style.position = Position.Absolute;
            panel.style.left = 48f;
            panel.style.top = 109f;
            panel.style.width = 456f;
            panel.style.height = 888f;
            // [125차] 플루언트 글래스 — Figma #161B22@0.80 + BACKGROUND_BLUR. UTK 백드롭블러 미지원 →
            // 알파 0.80 근사만 적용(GlassPanel 선례 동일 취급, docs/125_38_WORLDMAP_FLUENT_PINS_ROUTE.md).
            panel.style.backgroundColor = new StyleColor(new Color(0x16 / 255f, 0x1B / 255f, 0x22 / 255f, 0.80f));   // #161B22@0.80
            panel.style.borderTopLeftRadius = 14.4f;
            panel.style.borderTopRightRadius = 14.4f;
            panel.style.borderBottomLeftRadius = 14.4f;
            panel.style.borderBottomRightRadius = 14.4f;
            panel.style.borderTopWidth = 1.2f;
            panel.style.borderBottomWidth = 1.2f;
            panel.style.borderLeftWidth = 1.2f;
            panel.style.borderRightWidth = 1.2f;
            var strokeHalf = new StyleColor(new Color(0x30 / 255f, 0x36 / 255f, 0x3D / 255f, 0.50f));
            panel.style.borderTopColor = strokeHalf;
            panel.style.borderBottomColor = strokeHalf;
            panel.style.borderLeftColor = strokeHalf;
            panel.style.borderRightColor = strokeHalf;

            // Decal 코너 4개 — TL(0,0)/TR(442,0)/BL(0,874)/BR(442,874), 14×14 L자 #58A6FF w1.8
            BuildCornerDecal(panel, 0f, 0f, top: true, left: true);
            BuildCornerDecal(panel, 442f, 0f, top: true, left: false);
            BuildCornerDecal(panel, 0f, 874f, top: false, left: true);
            BuildCornerDecal(panel, 442f, 874f, top: false, left: false);

            // PanelHeader (24,24) 408×37 — "지역 탐색 리스트" 19.2/700 #F0F6FC + "REGIONS" 13.2/500 #8B949E (세로 스택)
            var panelHeader = new VisualElement { name = "PanelHeader" };
            panelHeader.style.position = Position.Absolute;
            panelHeader.style.left = 24f;
            panelHeader.style.top = 24f;
            panelHeader.style.width = 408f;
            panelHeader.style.height = 37f;
            panelHeader.style.flexDirection = FlexDirection.Row;   // [144차 P0-WM #3] 가로 조합
            panelHeader.style.alignItems = Align.Center;           // Figma BASELINE — Center 근사(기록)
            panelHeader.pickingMode = PickingMode.Ignore;

            // [144차 P0-WM #3] TitleGroup 가로 조합(95:202) — HORIZONTAL gap12 BASELINE(세로 스택 폐기)
            var headerTitle = new Label("지역 탐색 리스트");
            headerTitle.style.position = Position.Relative;
            headerTitle.style.left = 0f;
            headerTitle.style.top = 0f;
            headerTitle.style.height = 23f;
            headerTitle.style.fontSize = 19.2f;
            headerTitle.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            headerTitle.style.color = new StyleColor(GitHubDark.TextMain);
            headerTitle.style.unityTextAlign = TextAnchor.MiddleLeft;
            headerTitle.pickingMode = PickingMode.Ignore;
            panelHeader.Add(headerTitle);

            var headerSub = new Label("REGIONS");
            headerSub.style.position = Position.Relative;
            headerSub.style.marginLeft = 12f;   // [144차 P0-WM #3] gap 12
            headerSub.style.height = 14f;
            headerSub.style.fontSize = 13.2f;
            headerSub.style.letterSpacing = 1.2f;   // [144차 P1-WM #5] letterSp 1.2 (95:204)
            // Figma 500 — Normal 근사(폰트 weight 한계 — BuildHudHeader subtitle 주석 동일)
            headerSub.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Normal;
            headerSub.style.color = new StyleColor(GitHubDark.TextSub);
            headerSub.style.unityTextAlign = TextAnchor.MiddleLeft;
            headerSub.pickingMode = PickingMode.Ignore;
            panelHeader.Add(headerSub);
            panel.Add(panelHeader);

            // FilterTabs (24,81) 408×36 — 퀘스트 200×36 + gap 5 + 지역 203×36 (Figma 진실치 고정 폭, 스타일만 스왑)
            var tabs = new VisualElement { name = "FilterTabs" };
            tabs.style.position = Position.Absolute;
            tabs.style.left = 24f;
            tabs.style.top = 81f;
            tabs.style.width = 408f;
            tabs.style.height = 36f;
            tabs.style.flexDirection = FlexDirection.Row;
            tabs.style.justifyContent = Justify.SpaceBetween;   // 200+203=403 — gap 5 자동 정합

            _questTab = new Button(OnQuestTabClicked) { name = "QuestTab", text = "퀘스트" };
            _questTab.style.width = 200f;
            _questTab.style.height = 36f;
            _questTab.style.fontSize = 14.4f;
            tabs.Add(_questTab);

            _regionTab = new Button(OnRegionTabClicked) { name = "RegionTab", text = "지역" };
            _regionTab.style.width = 203f;
            _regionTab.style.height = 36f;
            _regionTab.style.fontSize = 14.4f;
            tabs.Add(_regionTab);
            panel.Add(tabs);

            // QuestSlotsContainer (24,136) 408×728 — ScrollView (세로 스크롤)
            _questSlots = new ScrollView(ScrollViewMode.Vertical) { name = "QuestSlotsContainer" };
            _questSlots.style.position = Position.Absolute;
            _questSlots.style.left = 24f;
            _questSlots.style.top = 136f;
            _questSlots.style.width = 408f;
            _questSlots.style.height = 728f;
            _questSlots.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

            _questPlaceholderRoot = new VisualElement { name = "QuestPlaceholderRoot" };
            _questPlaceholderRoot.style.flexDirection = FlexDirection.Column;
            _questSlots.contentContainer.Add(_questPlaceholderRoot);
            BuildQuestPlaceholderCards(_questPlaceholderRoot);

            _regionListRoot = new VisualElement { name = "RegionListRoot" };
            _regionListRoot.style.flexDirection = FlexDirection.Column;
            _questSlots.contentContainer.Add(_regionListRoot);

            panel.Add(_questSlots);
            // [144차 P0-WM #1] 좌측 패널 독립 창 이식 — _content 직접 부착 폐기.
            //   패널 트리는 그대로(필드/갱신 경로 무재배선) — WorldMapSidePanelWindow가 호스팅하고
            //   월드맵 Show/Hide에 동기 개폐된다. top 109→106(+3px 정정 [144차 P1-WM #4]).
            _questListPanel = panel;
            _questListPanel.style.top = 106f;
            _questListPanelNeedsWindow = true;

            // Figma 기본 상태 — 퀘스트 탭 활성 / 지역 리스트 비표시(개방 시 RebuildRegionRows로 데이터 준비)
            _regionTabActive = false;
            _regionListRoot.style.display = DisplayStyle.None;
            ApplyFilterTabStyles();
        }

        /// <summary>[124차 Phase A] 패널 Decal 코너 — 14×14 L자 2면 보더 #58A6FF w1.8(코너에 해당하는 2면만 표시).</summary>
        private static void BuildCornerDecal(VisualElement host, float x, float y, bool top, bool left)
        {
            var decal = new VisualElement { name = "DecalCorner" };
            decal.style.position = Position.Absolute;
            decal.style.left = x;
            decal.style.top = y;
            decal.style.width = 12f;   // [144차 P1-WM #8] Decal Line 12 (95:194)
            decal.style.height = 12f;   // [144차 P1-WM #8] L자 12 (95:194)
            var accent = new StyleColor(GitHubDark.Accent);
            decal.style.borderTopColor = accent;
            decal.style.borderBottomColor = accent;
            decal.style.borderLeftColor = accent;
            decal.style.borderRightColor = accent;
            decal.style.borderTopWidth = top ? 1.8f : 0f;
            decal.style.borderBottomWidth = top ? 0f : 1.8f;
            decal.style.borderLeftWidth = left ? 1.8f : 0f;
            decal.style.borderRightWidth = left ? 0f : 1.8f;
            decal.pickingMode = PickingMode.Ignore;
            host.Add(decal);
        }

        /// <summary>[124차 Phase A] FilterTabs 활성/비활성 스타일 스왑 — active: r7.2 fill #58A6FF, text 14.4/700 #0B0E14 /
        /// inactive: r7.2 stroke #30363D/0.5 w1.2, text 14.4/700 #8B949E. 폭은 Figma 진실치 고정(200/203).</summary>
        private void ApplyFilterTabStyles()
        {
            StyleTab(_questTab, !_regionTabActive);
            StyleTab(_regionTab, _regionTabActive);
        }

        private static void StyleTab(Button tab, bool active)
        {
            var accent = new StyleColor(GitHubDark.Accent);
            var strokeHalf = new StyleColor(new Color(0x30 / 255f, 0x36 / 255f, 0x3D / 255f, 0.50f));
            tab.style.backgroundColor = active ? accent : new StyleColor(new Color(0f, 0f, 0f, 0f));
            tab.style.borderTopColor = strokeHalf;
            tab.style.borderBottomColor = strokeHalf;
            tab.style.borderLeftColor = strokeHalf;
            tab.style.borderRightColor = strokeHalf;
            tab.style.borderTopWidth = active ? 0f : 1.2f;
            tab.style.borderBottomWidth = active ? 0f : 1.2f;
            tab.style.borderLeftWidth = active ? 0f : 1.2f;
            tab.style.borderRightWidth = active ? 0f : 1.2f;
            tab.style.borderTopLeftRadius = 7.2f;
            tab.style.borderTopRightRadius = 7.2f;
            tab.style.borderBottomLeftRadius = 7.2f;
            tab.style.borderBottomRightRadius = 7.2f;
            tab.style.color = active ? new StyleColor(new Color(0x0B / 255f, 0x0E / 255f, 0x14 / 255f, 1f))
                                     : new StyleColor(GitHubDark.TextSub);
            tab.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;   // Figma 14.4/700 — 탭 텍스트 700 공통
        }

        private void OnQuestTabClicked()
        {
            if (!_regionTabActive) return;
            _regionTabActive = false;
            ApplyFilterTabStyles();
            _regionListRoot.style.display = DisplayStyle.None;
            _questPlaceholderRoot.style.display = DisplayStyle.Flex;
            _questSlots.scrollOffset = Vector2.zero;
            Debug.Log("[124차 Phase A] 퀘스트 탭 활성 — Figma 그대로 정적 플레이스홀더 3카드");
        }

        private void OnRegionTabClicked()
        {
            if (_regionTabActive) return;
            _regionTabActive = true;
            ApplyFilterTabStyles();
            _questPlaceholderRoot.style.display = DisplayStyle.None;
            _regionListRoot.style.display = DisplayStyle.Flex;
            _questSlots.scrollOffset = Vector2.zero;
            RebuildRegionRows();
            Debug.Log("[124차 Phase A] 지역 탭 활성 — 실데이터 영지 리스트(클릭=SelectTerritory)");
        }

        /// <summary>[124차 Phase A] 퀘스트 탭 콘텐츠 — Figma 그대로 정적 플레이스홀더 3카드(계획서 진실치 원문 전부).
        /// QuestSlot 408×121 r9.6 fill #FFFFFF/0.08 stroke #58A6FF/1.0 w1.8 + TierStrip 5×121 + CategoryPill
        /// 62×19 r4.8 #0B0E14 + 제목 16.8/700 + 설명 14.4/400. 퀘스트 시스템은 후속(QuestPin/경로선 동일 제외 계약).</summary>
        private void BuildQuestPlaceholderCards(VisualElement host)
        {
            BuildQuestPlaceholderCard(host,
                "현재 진행", GitHubDark.StatusGreen, "Lv.35 - 45", GitHubDark.Accent,
                "고원 레이더 방해 해제",
                "잊혀진 혹성 고원에서 송출되는 레이더 방해 전파를 차단하고 관측망을 복구",
                GitHubDark.StatusGreen);
            BuildQuestPlaceholderCard(host,
                "메인 임무", GitHubDark.StatusOrange, "Lv.45", GitHubDark.StatusOrange,
                "차원 문 요새 작전",
                "부서진 차원의 문 요새에 침투해 크로노스 연료 장치를 회수하고 작전지 지휘소를 무력화하라.",   // [144차 P1-WM #19] 리터럴 절단 복원(Figma 전문)
                GitHubDark.StatusOrange);
            BuildQuestPlaceholderCard(host,
                "보조 임무", GitHubDark.TextSub, "Lv.25 - 30", GitHubDark.TextSub,
                "공업지구 보급 지원",
                "엘리시온 정착 공업지구로 중수소 운송선과 보급 물자를 안전하게 인도하라.",
                GitHubDark.Accent);
        }

        /// <summary>[124차 Phase A] 플레이스홀더 카드 1장 — 계획서 진실치: TierStrip(0,0) 5×121 / CardHeader(14,14) 379×19
        /// (CategoryPill 62×19 r4.8 fill #0B0E14 stroke #30363D/0.5 w1.2 + text 12/700 + "Lv…" 14.4/700 우측) /
        /// CardBody(14,48) 379×59 (제목 16.8/700 #F0F6FC + 설명 14.4/400 #8B949E).</summary>
        private static void BuildQuestPlaceholderCard(VisualElement host, string categoryText, Color categoryColor,
            string lvText, Color lvColor, string titleText, string descText, Color tierColor)
        {
            var card = new VisualElement { name = "QuestSlot" };
            card.style.width = 408f;
            card.style.height = 121f;
            // [144차 P1-WM #6] 비선택 카드(95:220/229) — fill #FFFFFF@0.02 + stroke #30363D@0.502 w1.2.
            //   accent 1.8 밝은 스타일은 선택 상태(95:211 QuestSlot1) 전용 — 전 카드 오적용 정정.
            card.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.02f));
            card.style.borderTopWidth = 1.2f;
            card.style.borderBottomWidth = 1.2f;
            card.style.borderLeftWidth = 1.2f;
            card.style.borderRightWidth = 1.2f;
            var halfStroke = new StyleColor(new Color(0x30 / 255f, 0x36 / 255f, 0x3D / 255f, 0.502f));
            card.style.borderTopColor = halfStroke;
            card.style.borderBottomColor = halfStroke;
            card.style.borderLeftColor = halfStroke;
            card.style.borderRightColor = halfStroke;
            card.style.borderTopLeftRadius = 9.6f;
            card.style.borderTopRightRadius = 9.6f;
            card.style.borderBottomLeftRadius = 9.6f;
            card.style.borderBottomRightRadius = 9.6f;
            card.style.marginBottom = 9.6f;   // [144차 P1-WM #7] 12→9.6 (95:206 gap)
            card.style.overflow = Overflow.Hidden;   // TierStrip 코너 클리핑 — 카드 r9.6 정합
            card.pickingMode = PickingMode.Ignore;   // 정적 플레이스홀더 — 클릭 투과
            host.Add(card);

            var strip = new VisualElement { name = "TierStrip" };
            strip.style.position = Position.Absolute;
            strip.style.left = 0f;
            strip.style.top = 0f;
            strip.style.width = 5f;
            strip.style.height = 121f;
            strip.style.backgroundColor = new StyleColor(tierColor);
            strip.pickingMode = PickingMode.Ignore;
            card.Add(strip);

            var cardHeader = new VisualElement { name = "CardHeader" };
            cardHeader.style.position = Position.Absolute;
            cardHeader.style.left = 14f;
            cardHeader.style.top = 14f;
            cardHeader.style.width = 379f;
            cardHeader.style.height = 19f;
            cardHeader.pickingMode = PickingMode.Ignore;
            card.Add(cardHeader);

            var pill = new VisualElement { name = "CategoryPill" };
            pill.style.position = Position.Absolute;
            pill.style.left = 0f;
            pill.style.top = 0f;
            pill.style.width = 62f;
            pill.style.height = 19f;
            pill.style.backgroundColor = new StyleColor(new Color(0x0B / 255f, 0x0E / 255f, 0x14 / 255f, 1f));
            pill.style.borderTopWidth = 1.2f;
            pill.style.borderBottomWidth = 1.2f;
            pill.style.borderLeftWidth = 1.2f;
            pill.style.borderRightWidth = 1.2f;
            var pillStroke = new StyleColor(new Color(0x30 / 255f, 0x36 / 255f, 0x3D / 255f, 0.50f));
            pill.style.borderTopColor = pillStroke;
            pill.style.borderBottomColor = pillStroke;
            pill.style.borderLeftColor = pillStroke;
            pill.style.borderRightColor = pillStroke;
            pill.style.borderTopLeftRadius = 4.8f;
            pill.style.borderTopRightRadius = 4.8f;
            pill.style.borderBottomLeftRadius = 4.8f;
            pill.style.borderBottomRightRadius = 4.8f;
            pill.pickingMode = PickingMode.Ignore;
            cardHeader.Add(pill);

            var pillText = new Label(categoryText);
            pillText.style.position = Position.Absolute;
            pillText.style.left = 0f; pillText.style.right = 0f;
            pillText.style.top = 0f; pillText.style.bottom = 0f;
            pillText.style.fontSize = 12f;
            pillText.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            pillText.style.color = new StyleColor(categoryColor);
            pillText.style.unityTextAlign = TextAnchor.MiddleCenter;
            pillText.pickingMode = PickingMode.Ignore;
            pill.Add(pillText);

            var lv = new Label(lvText);
            lv.style.position = Position.Absolute;
            lv.style.right = 0f;   // CardHeader 우측 정렬(x 진실치 없음 — 카테고리 pill 좌/Lv 우 대칭 구조)
            lv.style.top = 0f;
            lv.style.height = 19f;
            lv.style.fontSize = 14.4f;
            lv.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            lv.style.color = new StyleColor(lvColor);
            lv.style.unityTextAlign = TextAnchor.MiddleRight;
            lv.style.whiteSpace = WhiteSpace.NoWrap;
            lv.pickingMode = PickingMode.Ignore;
            cardHeader.Add(lv);

            var cardBody = new VisualElement { name = "CardBody" };
            cardBody.style.position = Position.Absolute;
            cardBody.style.left = 14f;
            cardBody.style.top = 48f;
            cardBody.style.width = 379f;
            cardBody.style.height = 59f;
            cardBody.pickingMode = PickingMode.Ignore;
            card.Add(cardBody);

            var title = new Label(titleText);
            title.style.position = Position.Absolute;
            title.style.left = 0f;
            title.style.top = 0f;
            title.style.height = 23f;
            title.style.fontSize = 16.8f;
            title.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            title.style.color = new StyleColor(GitHubDark.TextMain);
            title.style.unityTextAlign = TextAnchor.MiddleLeft;
            title.pickingMode = PickingMode.Ignore;
            cardBody.Add(title);

            var desc = new Label(descText);
            desc.style.position = Position.Absolute;
            desc.style.left = 0f;
            desc.style.top = 26f;
            desc.style.width = 379f;
            desc.style.fontSize = 14.4f;
            desc.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Normal;
            desc.style.color = new StyleColor(GitHubDark.TextSub);
            desc.style.whiteSpace = WhiteSpace.Normal;
            desc.pickingMode = PickingMode.Ignore;
            cardBody.Add(desc);
        }

        /// <summary>[124차 Phase A] 지역 탭 행 재구성 — TerritoryDatabase 전수(계획서 82) 실데이터.
        /// 창 개방/지역 탭 전환 시 1회, 소유 변동은 PollTick의 RegionRowView.Refresh 실시간.
        /// 행 클릭=SelectTerritory(def) — 우패널 배선은 [124차-ImplB].</summary>
        private void RebuildRegionRows()
        {
            if (_regionListRoot == null) return;
            _regionListRoot.Clear();
            _regionRows.Clear();
            var db = TerritoryDatabase.Instance;
            if (db == null) return;
            var iter = db.GetAllDefinitions();
            if (iter == null) return;
            int count = 0;
            foreach (var def in iter)
            {
                var row = new RegionRowView(def);
                row.RegisterCallback<ClickEvent>(evt => SelectTerritory(def));
                _regionListRoot.Add(row);
                _regionRows.Add(row);
                count++;
            }
            ApplyUIToolkitFont(_regionListRoot);
            Debug.Log($"[124차 Phase A] 지역 탭 행 재구성 — {count}개 영지(TerritoryDatabase 전수 실데이터)");
        }

        /// <summary>[124차-ImplB] 선택 진입점 — 핀/지역 행 클릭 공용. 선택 저장 + 핀 강조 + 우측 패널 갱신.</summary>
        public void SelectTerritory(TerritoryDefinition def)
        {
            if (def.id.Equals(default(TerritoryId)) && string.IsNullOrEmpty(def.territoryName)) return;
            _selectedDef = def;
            _hasSelection = true;
            if (_selectedMarker != null)
                _selectedMarker.SetSelected(false);
            _selectedMarker = null;
            for (int i = 0; i < _territoryMarkers.Count; i++)
            {
                if (_territoryMarkers[i].Def.id.Equals(def.id))
                {
                    _selectedMarker = _territoryMarkers[i];
                    _selectedMarker.SetSelected(true);
                    break;
                }
            }
            RefreshQuestDetailPanel();
            UnityEngine.Debug.Log($"[124차-ImplB] 영지 선택: {def.territoryName} — 우측 선택 지역 정보 갱신");
        }

        // =====================================================================
        //  [124차-ImplB] 우측 QuestDetailPanel — Figma 95:255 "선택 지역 정보 / TACTICAL INTEL"
        // =====================================================================
        /// <summary>우측 패널 구축(생성 시 1회) — 진실치: (1392,109) 480×888 r14.4 fill #161B22 stroke #30363D/0.50 w1.2,
        /// Decal 코너(TR/BR x=466), PanelHeader (24,24), QuestHeroHeader (24,81) 432×80, BriefingCard (24,180) 432×119,
        /// LordSection (24,318) 432×210(4행 — LordItems y=32부터 47.4 간격), DeploymentFooter (24,548) 432×124.</summary>
        private void BuildQuestDetailPanel()
        {
            _intelPanel = new VisualElement { name = "QuestDetailPanel" };
            _intelPanel.style.position = Position.Absolute;
            _intelPanel.style.left = 1392f;
            _intelPanel.style.top = 106f;   // [144차 P1-WM #4] 109→106 (+3px 정정 — ay 상대 106)
            _intelPanel.style.width = 480f;
            _intelPanel.style.height = 888f;
            // [125차] 플루언트 글래스 — 좌패널(95:188)과 동일 사양: #161B22@0.80 + BACKGROUND_BLUR 알파 근사.
            _intelPanel.style.backgroundColor = new StyleColor(new Color(0x16 / 255f, 0x1B / 255f, 0x22 / 255f, 0.80f));   // #161B22@0.80
            SetCornerRadius(_intelPanel, 14.4f);
            SetHalfStroke(_intelPanel);
            // [144차 P0-WM #1] 우측 패널 독립 창 이식 — _content 직접 부착 폐기(좌패널과 동일 방식).
            _intelPanelNeedsWindow = true;
            BuildCornerDecal(_intelPanel, 0f, 0f, top: true, left: true);
            BuildCornerDecal(_intelPanel, 466f, 0f, top: true, left: false);
            BuildCornerDecal(_intelPanel, 0f, 874f, top: false, left: true);
            BuildCornerDecal(_intelPanel, 466f, 874f, top: false, left: false);

            // PanelHeader (24,24) — "선택 지역 정보" 19.2/700 #F0F6FC + "TACTICAL INTEL" 13.2/500 #8B949E
            var header = new VisualElement { name = "PanelHeader" };
            header.style.position = Position.Absolute;
            header.style.left = 24f; header.style.top = 24f; header.style.width = 432f; header.style.height = 37f;
            header.style.flexDirection = FlexDirection.Row;   // [144차 P0-WM #3] 가로 조합
            header.style.alignItems = Align.Center;           // Figma BASELINE — Center 근사(기록)
            header.pickingMode = PickingMode.Ignore;
            // [144차 P0-WM #3] TitleGroup 가로 조합(95:269) — HORIZONTAL gap12 BASELINE(세로 스택 폐기)
            var intelTitle = new Label("선택 지역 정보");
            intelTitle.style.fontSize = 19.2f;
            intelTitle.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            intelTitle.style.color = new StyleColor(GitHubDark.TextMain);
            intelTitle.pickingMode = PickingMode.Ignore;
            header.Add(intelTitle);
            var intelSub = new Label("TACTICAL INTEL");
            intelSub.style.fontSize = 13.2f;
            intelSub.style.letterSpacing = 1.2f;   // [144차 P1-WM #5] letterSp 1.2 (95:270)
            intelSub.style.marginLeft = 12f;
            intelSub.style.color = new StyleColor(GitHubDark.TextSub);
            intelSub.pickingMode = PickingMode.Ignore;
            header.Add(intelSub);
            _intelPanel.Add(header);

            // QuestHeroHeader (24,81) 432×80 r9.6 fill #FFFFFF/0.02 — 영지명 21.6/700 + 소유 배지 r4.8 + 부제 14.4/400
            var hero = new VisualElement { name = "QuestHeroHeader" };
            hero.style.position = Position.Absolute;
            hero.style.left = 24f; hero.style.top = 81f; hero.style.width = 432f; hero.style.height = 80f;
            hero.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.02f));
            SetCornerRadius(hero, 9.6f);
            SetHalfStroke(hero);
            _intelName = AbsLabel("선택된 영지 없음", 14f, 14f, 25f, 21.6f, true, GitHubDark.TextMain);
            hero.Add(_intelName);
            _intelBadge = new Label("—");
            _intelBadge.name = "Badge";
            _intelBadge.style.position = Position.Absolute;
            _intelBadge.style.right = 14f; _intelBadge.style.top = 14f; _intelBadge.style.height = 19f;
            _intelBadge.style.paddingLeft = 8f; _intelBadge.style.paddingRight = 8f;
            _intelBadge.style.fontSize = 12f;
            _intelBadge.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            _intelBadge.style.color = new StyleColor(new Color(0x0B / 255f, 0x0E / 255f, 0x14 / 255f, 1f));
            SetCornerRadius(_intelBadge, 4.8f);
            _intelBadge.style.backgroundColor = new StyleColor(GitHubDark.StatusGreen);
            _intelBadge.style.unityTextAlign = TextAnchor.MiddleCenter;
            _intelBadge.pickingMode = PickingMode.Ignore;
            hero.Add(_intelBadge);
            _intelSubtitle = new Label("지도의 핀 또는 좌측 지역 목록을 클릭하세요");
            _intelSubtitle.style.position = Position.Absolute;
            _intelSubtitle.style.left = 14f; _intelSubtitle.style.top = 49f; _intelSubtitle.style.height = 17f;
            _intelSubtitle.style.fontSize = 14.4f;
            _intelSubtitle.style.color = new StyleColor(GitHubDark.TextSub);
            _intelSubtitle.style.whiteSpace = WhiteSpace.NoWrap;
            _intelSubtitle.pickingMode = PickingMode.Ignore;
            hero.Add(_intelSubtitle);
            _intelPanel.Add(hero);

            // BriefingCard (24,180) 432×119 r9.6 fill #FFFFFF/0.02 — "해당 구역 주요 상태" + 본문(lineHeight 21.6)
            var brief = new VisualElement { name = "BriefingCard" };
            brief.style.position = Position.Absolute;
            brief.style.left = 24f; brief.style.top = 180f; brief.style.width = 432f; brief.style.height = 119f;
            brief.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.02f));
            SetCornerRadius(brief, 9.6f);
            SetHalfStroke(brief);
            brief.Add(AbsLabel("해당 구역 주요 상태", 14f, 14f, 18f, 15.6f, true, GitHubDark.Accent));
            _intelBriefing = new Label("—");
            // [144차 P1-WM #11 기록] 브리핑 lineH 21.6 (95:262) — IStyle lineHeight 미지원(컴파일 실측) → 기록만.
            _intelBriefing.style.position = Position.Absolute;
            _intelBriefing.style.left = 14f; _intelBriefing.style.top = 40f; _intelBriefing.style.right = 14f;
            _intelBriefing.style.fontSize = 14.4f;
            _intelBriefing.style.color = new StyleColor(GitHubDark.TextSub);
            _intelBriefing.style.whiteSpace = WhiteSpace.Normal;
            _intelBriefing.pickingMode = PickingMode.Ignore;
            brief.Add(_intelBriefing);
            _intelPanel.Add(brief);

            // LordSection (24,318) 432×210 — "영주 정보 (LORD STATUS)" + 4행(섹션 기준 y=32/79.4/126.8/174.2, 행 35.4)
            var lord = new VisualElement { name = "LordSection" };
            lord.style.position = Position.Absolute;
            lord.style.left = 24f; lord.style.top = 318f; lord.style.width = 432f; lord.style.height = 210f;
            lord.pickingMode = PickingMode.Ignore;
            lord.Add(AbsLabel("영주 정보 (LORD STATUS)", 0f, 0f, 18f, 15.6f, true, GitHubDark.Accent));
            _intelLordValue = BuildLordRow(lord, "UI/WorldMap/Icons/crown", "crown", "영주 이름", 32f);
            _intelSoldierValue = BuildLordRow(lord, "UI/WorldMap/Icons/users", "users", "병사 수", 79.4f);
            _intelAvgValue = BuildLordRow(lord, "UI/WorldMap/Icons/star", "star", "병사 평균레벨", 126.8f);
            _intelPowerValue = BuildLordRow(lord, "UI/WorldMap/Icons/shield", "shield", "전투력", 174.2f);
            _intelPanel.Add(lord);

            // DeploymentFooter (24,548) 432×124 — TrackingRow(0,0) 432×48 + ButtonContainer(0,77) 432×47
            var footer = new VisualElement { name = "DeploymentFooter" };
            footer.style.position = Position.Absolute;
            footer.style.left = 24f; footer.style.top = 548f; footer.style.width = 432f; footer.style.height = 124f;
            footer.pickingMode = PickingMode.Ignore;
            var track = new VisualElement { name = "TrackingRow" };
            track.style.position = Position.Absolute;
            track.style.left = 0f; track.style.top = 14.4f; track.style.width = 432f; track.style.height = 48f;   // [144차 P1-WM #18] pt 14.4 — 상단 쏠림 정정
            track.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.02f));
            SetCornerRadius(track, 9.6f);
            SetHalfStroke(track);
            var radar = new VisualElement { name = "RadarIcon" };
            radar.style.position = Position.Absolute;
            radar.style.left = 14f; radar.style.top = 16f; radar.style.width = 19.2f; radar.style.height = 19.2f;   // [144차 P1-WM #19] y 14→16 (95:307)
            var radarTex = LoadMapResourceTexture("UI/WorldMap/Icons/radar", "95:307");
            if (radarTex != null)
            {
                radar.style.backgroundImage = new StyleBackground(radarTex);
                radar.style.backgroundSize = new BackgroundSize(Length.Percent(100f), Length.Percent(100f));
            }
            radar.pickingMode = PickingMode.Ignore;
            track.Add(radar);
            track.Add(AbsLabel("지도 상 실시간 좌표 추적", 44f, 14f, 19f, 15.6f, true, GitHubDark.TextMain));
            var pill = new Button(ToggleTracking) { name = "TogglePill" };
            pill.style.position = Position.Absolute;
            pill.style.left = 365f; pill.style.top = 10f; pill.style.width = 53f; pill.style.height = 29f;
            SetCornerRadius(pill, 14.4f);
            pill.style.backgroundColor = new StyleColor(GitHubDark.Accent);
            _intelKnob = new VisualElement { name = "ToggleKnob" };
            _intelKnob.style.position = Position.Absolute;
            _intelKnob.style.left = 26.4f; _intelKnob.style.top = 2.4f;   // Figma ON(오른쪽) 기본 — knob 24×24, pill 내부 여백 2.4
            _intelKnob.style.width = 24f; _intelKnob.style.height = 24f;
            SetCornerRadius(_intelKnob, 12f);
            _intelKnob.style.backgroundColor = new StyleColor(new Color(0x0B / 255f, 0x0E / 255f, 0x14 / 255f, 1f));
            _intelKnob.pickingMode = PickingMode.Ignore;
            pill.Add(_intelKnob);
            track.Add(pill);
            footer.Add(track);

            var btns = new VisualElement { name = "ButtonContainer" };
            btns.style.position = Position.Absolute;
            btns.style.left = 0f; btns.style.top = 77f; btns.style.width = 432f; btns.style.height = 47f;
            btns.style.flexDirection = FlexDirection.Row;
            var abort = new Button(CloseWindow) { name = "AbortButton", text = "닫기" };
            abort.style.width = 212f; abort.style.height = 47f;
            abort.style.fontSize = 15.6f;
            abort.style.color = new StyleColor(GitHubDark.TextSub);
            SetCornerRadius(abort, 7.2f);
            SetHalfStroke(abort);
            btns.Add(abort);
            var apply = new Button(ExecuteFastTravel) { name = "ApplyButton", text = "지점 전송 (Fast Travel)" };
            apply.style.width = 210f; apply.style.height = 47f;
            apply.style.fontSize = 15.6f;
            apply.style.color = new StyleColor(new Color(0x0B / 255f, 0x0E / 255f, 0x14 / 255f, 1f));
            SetCornerRadius(apply, 7.2f);
            apply.style.backgroundColor = new StyleColor(GitHubDark.Accent);
            btns.Add(apply);
            footer.Add(btns);
            _intelPanel.Add(footer);
            ApplyUIToolkitFont(_intelPanel);
            RefreshQuestDetailPanel();   // 초기 상태(미선택 안내) 렌더
        }

        /// <summary>[124차-ImplB] 영주 정보 행 — IconCol 29×29 r7.2 #161B22/0.80(top3) + 아이콘 17×17 + 라벨/값 2단(행 432×35.4).</summary>
        private Label BuildLordRow(VisualElement parent, string iconResource, string iconName, string labelText, float y)
        {
            var row = new VisualElement { name = "LordItem_" + iconName };
            row.style.position = Position.Absolute;
            row.style.left = 0f; row.style.top = y; row.style.width = 432f; row.style.height = 35.4f;
            row.pickingMode = PickingMode.Ignore;
            var iconCol = new VisualElement { name = "IconCol" };
            iconCol.style.position = Position.Absolute;
            iconCol.style.left = 0f; iconCol.style.top = 3f; iconCol.style.width = 29f; iconCol.style.height = 29f;
            iconCol.style.backgroundColor = new StyleColor(new Color(0x16 / 255f, 0x1B / 255f, 0x22 / 255f, 0.80f));
            SetCornerRadius(iconCol, 7.2f);
            SetHalfStroke(iconCol);
            var icon = new VisualElement { name = "Icon" };
            icon.style.position = Position.Absolute;
            icon.style.left = 6f; icon.style.top = 6f; icon.style.width = 17f; icon.style.height = 17f;
            var tex = LoadMapResourceTexture(iconResource, iconName);
            if (tex != null)
            {
                icon.style.backgroundImage = new StyleBackground(tex);
                icon.style.backgroundSize = new BackgroundSize(Length.Percent(100f), Length.Percent(100f));
            }
            icon.pickingMode = PickingMode.Ignore;
            iconCol.Add(icon);
            row.Add(iconCol);
            row.Add(AbsLabel(labelText, 41f, 0f, 18f, 15.6f, true, GitHubDark.TextMain));
            var value = new Label("—");
            value.style.position = Position.Absolute;
            value.style.left = 43.2f; value.style.top = 20.4f; value.style.height = 15f;   // [144차 P1-WM #14/#15] x 41→43.2, y 18→20.4 (95:296/300)
            value.style.fontSize = 13.2f;
            value.style.color = new StyleColor(GitHubDark.TextSub);
            value.style.whiteSpace = WhiteSpace.NoWrap;
            value.pickingMode = PickingMode.Ignore;
            row.Add(value);
            parent.Add(row);
            return value;
        }

        /// <summary>[124차-ImplB] 우측 패널 갱신 — 선택 영지 실데이터(위조 금지: WorldMapTerritoryIntelContent 포맷터 경유).
        /// 미선택 상태: 안내 텍스트 + "—". PollTick 250ms 폴링에서도 호출(소유 변동/수비 집계 실시간).</summary>
        private void RefreshQuestDetailPanel()
        {
            if (_intelPanel == null) return;
            var db = TerritoryDatabase.Instance;
            if (!_hasSelection)
            {
                if (_intelName != null) _intelName.text = "선택된 영지 없음";
                if (_intelBadge != null) _intelBadge.style.display = DisplayStyle.None;
                if (_intelSubtitle != null) _intelSubtitle.text = "지도의 핀 또는 좌측 지역 목록을 클릭하세요";
                if (_intelBriefing != null) _intelBriefing.text = "—";
                SetLordValues("—", "—", "—", "—");
                return;
            }
            TerritoryDefinition def = _selectedDef;
            TerritoryState state = db != null ? db.GetState(def.id) : null;
            TerritoryOwnership own = state != null ? state.ownership : TerritoryOwnership.Unoccupied;

            // 실배치 수비 집계 — GuardManager.GetGuardsInTerritory(기존 API) + GuardEquipmentSystem.CalculateGuardCombatPower(기존 산식)
            int deployedCount = 0;
            var levels = new System.Collections.Generic.List<int>();
            float powerSum = 0f;
            var gm = ProjectName.Systems.GuardManager.Instance;
            var guards = gm != null ? gm.GetGuardsInTerritory(def.id) : null;
            if (guards != null)
            {
                deployedCount = guards.Count;
                var ges = ProjectName.Systems.GuardEquipmentSystem.Instance;
                for (int i = 0; i < guards.Count; i++)
                {
                    var g = guards[i];
                    if (g == null) continue;
                    levels.Add(g.Level);
                    if (ges != null) powerSum += ges.CalculateGuardCombatPower(g);
                }
            }

            if (_intelName != null) _intelName.text = def.territoryName;
            if (_intelBadge != null)
            {
                _intelBadge.style.display = DisplayStyle.Flex;
                _intelBadge.style.backgroundColor = new StyleColor(OwnershipContractColor(own));
                _intelBadge.text = WorldMapTerritoryIntelContent.OwnershipBadgeText(own);
            }
            if (_intelSubtitle != null)
                _intelSubtitle.text = !string.IsNullOrEmpty(def.description) ? def.description : def.territoryName;
            if (_intelBriefing != null)
                _intelBriefing.text = WorldMapTerritoryIntelContent.BriefingBody(def, state, deployedCount);
            SetLordValues(
                WorldMapTerritoryIntelContent.LordNameValue(def, state),
                WorldMapTerritoryIntelContent.SoldierCountValue(deployedCount, def.guardCount),
                WorldMapTerritoryIntelContent.SoldierAvgLevelValue(levels),
                WorldMapTerritoryIntelContent.CombatPowerValue(powerSum));
        }

        private void SetLordValues(string lord, string soldiers, string avgLevel, string power)
        {
            if (_intelLordValue != null) _intelLordValue.text = lord;
            if (_intelSoldierValue != null) _intelSoldierValue.text = soldiers;
            if (_intelAvgValue != null) _intelAvgValue.text = avgLevel;
            if (_intelPowerValue != null) _intelPowerValue.text = power;
        }

        /// <summary>[124차-ImplB] 실시간 좌표 추적 토글 — ON(기본, knob 오른쪽 x26.4)/OFF(knob 좌측 x2.4). 좌표 텍스트는 항상 갱신.</summary>
        private void ToggleTracking()
        {
            _trackingEnabled = !_trackingEnabled;
            if (_intelKnob != null)
                _intelKnob.style.left = _trackingEnabled ? 26.4f : 2.4f;
            UnityEngine.Debug.Log($"[124차-ImplB] 실시간 좌표 추적 = {(_trackingEnabled ? "ON" : "OFF")}");
        }

        /// <summary>[125차-38] 지점 전송 (Fast Travel) — 선택 영지 경로를 미니맵에 표시하는 신계약 배선.
        /// ① 경로 계산: 플레이어 현재 위치→_selectedDef.worldPosition(영지 핀 월드좌표 단일 진실원) 직선.
        ///     시작점은 MinimapRouteOverlay가 미니맵 리프레시마다 플레이어 실측 위치로 재계산(직선 대시 기본안).
        /// ② 창 닫기: 지도가 미니맵을 가리면 경로가 보이지 않으므로 표시 전에 닫는다(Hide 경로 — M키/ESC와 동일 게이트).
        /// ③ MinimapRouteOverlay.ShowRouteTo(목적지, 경로색) — 색=Figma 95:167 Line #58A6FF w2.4(선택 핀 강조색과 동일,
        ///     '경로=목적지 핀 색' 계약: 목적지 핀은 선택 시 #58A6FF 렌더).
        /// ④ 토스트 피드백 '경로 표시: {영지명}'(UTKToastService 기존 계약 재사용 — 신규 토스트 로직 신설 금지).
        /// 상호작용불가=피드백 필수 계약: 선택 없음 → 토스트 안내(구: 로그만).
        /// 기존 텔레포트 로직 보존/통합 판단(실측: FastTravelSystem.ExecuteFastTravel는 유료 골드 차감+3초 로딩 후
        /// 소유 영지 중심 즉시 텔레포트): 소유 영지 선택 시 기존 위임을 그대로 유지 — 도착 직후 경로는 오버레이의
        /// 도착 판정(24m)으로 자동 소멸. 미소유 영지는 기존 시스템이 무음 거부하므로 경로 표시가 실효 피드백이 된다.
        /// 전용 창(FastTravelUI)의 텔레포트 진입점은 무변경 — 기능 보존. [144차] FastTravelUTK는 폐기(D5+D6).</summary>
        private void ExecuteFastTravel()
        {
            if (!_hasSelection)
            {
                UTKToastService.Show("영지를 먼저 선택하세요 — 지도 핀 또는 지역 목록 클릭");
                UnityEngine.Debug.Log("[125차-38] 지점 전송 실패 — 선택된 영지 없음 (토스트 안내)");
                return;
            }
            // ① 경로 목적지 — 선택 영지 월드좌표(지도 핀과 동일 진실원)
            Vector3 dest = _selectedDef.worldPosition;
            // ② 창 닫기 — 미니맵 경로 가림 방지
            Hide();
            // ③ 미니맵 경로 표시 — 색 #58A6FF(Figma Line w2.4, 선택 목적지 핀 색 계약)
            MinimapRouteOverlay.ShowRouteTo(dest, GitHubDark.Accent);
            // ④ 토스트 피드백
            UTKToastService.Show($"경로 표시: {_selectedDef.territoryName}");
            // 기존 텔레포트 위임 보존 — [124차-ImplB] 경로 유지(비용/소유 가드는 FastTravelSystem 존중)
            var ft = ProjectName.Systems.FastTravelSystem.Instance;
            if (ft != null)
            {
                ft.ExecuteFastTravel(_selectedDef.id);
                UnityEngine.Debug.Log($"[125차-38] 지점 전송 요청 — {_selectedDef.territoryName} (기존 텔레포트 위임 보존 + 미니맵 경로 표시)");
            }
            else
            {
                UnityEngine.Debug.LogWarning("[125차-38] FastTravelSystem 부재 — 텔레포트 생략, 경로 표시는 유지");
            }
        }

        /// <summary>[124차-ImplB] 닫기 버튼 — 창 닫기(기존 Hide 경로, M키/ESC와 동일 게이트).</summary>
        private void CloseWindow()
        {
            Hide();
        }

        /// <summary>[124차-ImplB] 4면 코너 반경 공용 헬퍼 — 핀/패널/카드 공용(IStyle 셧스루헛 금지 규약 준수).</summary>
        private static void SetCornerRadius(VisualElement el, float r)
        {
            el.style.borderTopLeftRadius = r;
            el.style.borderTopRightRadius = r;
            el.style.borderBottomLeftRadius = r;
            el.style.borderBottomRightRadius = r;
        }

        /// <summary>[124차-ImplB] 4면 보더 #30363D/0.50 w1.2 공용 헬퍼 — 패널/카드 공용.</summary>
        private static void SetHalfStroke(VisualElement el)
        {
            var sc = new StyleColor(new Color(0x30 / 255f, 0x36 / 255f, 0x3D / 255f, 0.50f));
            el.style.borderTopWidth = 1.2f; el.style.borderBottomWidth = 1.2f;
            el.style.borderLeftWidth = 1.2f; el.style.borderRightWidth = 1.2f;
            el.style.borderTopColor = sc; el.style.borderBottomColor = sc;
            el.style.borderLeftColor = sc; el.style.borderRightColor = sc;
        }

        /// <summary>[124차-ImplB] 절대배치 라벨 헬퍼 — 패널 내 (left,top,h,fontSize,bold,color).</summary>
        private static Label AbsLabel(string text, float left, float top, float height, float fontSize, bool bold, Color color)
        {
            var l = new Label(text);
            l.style.position = Position.Absolute;
            l.style.left = left; l.style.top = top; l.style.height = height;
            l.style.fontSize = fontSize;
            l.style.unityFontStyleAndWeight = bold ? UnityEngine.FontStyle.Bold : UnityEngine.FontStyle.Normal;
            l.style.color = new StyleColor(color);
            l.style.whiteSpace = WhiteSpace.NoWrap;
            l.pickingMode = PickingMode.Ignore;
            return l;
        }

        /// <summary>[124차 Phase A] 소유 상태 계약색 — 플레이어=#39D353/영주=기존 빨강(ColorWar)/분쟁=#FF9E2C/무주=#8B949E.
        /// 지역 리스트 소유색/TierStrip + [124차-ImplB] 핀·배지 공용(계획서 소유색 매핑 그대로).</summary>
        private static Color OwnershipContractColor(TerritoryOwnership o)
        {
            switch (o)
            {
                case TerritoryOwnership.PlayerOwned: return GitHubDark.StatusGreen;
                case TerritoryOwnership.LordOwned: return ColorWar;
                case TerritoryOwnership.Contested: return GitHubDark.StatusOrange;
                default: return GitHubDark.TextSub;
            }
        }

        /// <summary>[124차 Phase A] 지형 오버레이 필드 — CreateTerrainOverlay/RefreshTerrainOverlay 메서드 잔존 계약 유지.
        ///   [124차 Phase A] 호출부 폐기(Figma 아트 2레이어 배경으로 대체) — 메서드와 WorldMapTerrainBaker.cs는 잔존.</summary>
        private VisualElement _terrainOverlay;   // [118차 후속] 지형 오버레이 — 개방 시 리베이크 갱신용
        private Texture2D _terrainTex;

        /// <summary>[118차 R3+후속] 지형 오버레이 생성 — 베이크 텍스처를 양피지 위 Insert(0)로 부착.</summary>
        private void CreateTerrainOverlay()
        {
            var terrainTex = WorldMapTerrainBaker.GetOrBake();
            if (terrainTex == null) return;
            _terrainOverlay = new VisualElement { name = "TerrainOverlay" };
            _terrainOverlay.style.position = Position.Absolute;
            _terrainOverlay.style.left = 0f; _terrainOverlay.style.top = 0f;
            _terrainOverlay.style.right = 0f; _terrainOverlay.style.bottom = 0f;
            _terrainOverlay.style.backgroundImage = new StyleBackground(terrainTex);
            _terrainOverlay.style.backgroundSize = new BackgroundSize(
                Length.Percent(100f), Length.Percent(100f));
            _terrainOverlay.pickingMode = PickingMode.Ignore;
            _terrainTex = terrainTex;
            _mapCanvas.Insert(0, _terrainOverlay);   // 양피지(bg) 위, 마커(후속 Add) 아래
            Debug.Log($"[118차 R3] 지형 오버레이 추가 — tex {terrainTex.width}x{terrainTex.height}");
        }

        /// <summary>[118차 후속] 개방 시점 갱신 — 나무 스폰 후 리베이크된 텍스처를 재적용.</summary>
        private void RefreshTerrainOverlay()
        {
            var terrainTex = WorldMapTerrainBaker.GetOrBake();   // 0그루 캐시 → 리베이크 트리거
            if (terrainTex == null) return;
            if (_terrainOverlay == null) { CreateTerrainOverlay(); return; }
            if (ReferenceEquals(terrainTex, _terrainTex)) return;   // 동일 인스턴스 — 갱신 불요
            _terrainTex = terrainTex;
            _terrainOverlay.style.backgroundImage = new StyleBackground(terrainTex);
            Debug.Log("[118차 R3] 지형 오버레이 갱신 — 리베이크 텍스처 적용");
        }

        protected override void OnWindowOpen()
        {
            ApplyCanvasBounds();
            // [117차 R1] 루트 크기 미확정 대응 재시도(16/120/400ms — RestaurantWindowUTK 88차 선례). 멱등.
            schedule.Execute(ApplyCanvasBounds).StartingIn(16);
            schedule.Execute(ApplyCanvasBounds).StartingIn(120);
            schedule.Execute(ApplyCanvasBounds).StartingIn(400);
            schedule.Execute(LogLayoutProbe).StartingIn(500);   // [127차] 레이아웃 프로브 1회 — 수축 수리 수치 확정
            RebuildMarkers();
            // [124차 Phase A] 지형 베이크 호출 폐기 — 구 RefreshTerrainOverlay() (Figma 아트 2레이어 배경으로 대체, 메서드 잔존)
            RebuildRegionRows();   // [124차 Phase A] 지역 탭 행 재구성(실데이터 소유 상태) — 창 개방 시 1회
            // [폐기 완료] 원본 WorldMapWindow는 아카이브됨 — UTK가 유일 맵 (억제 코드 제거)
        }

        // =====================================================================
        //  원본 핫키 런타임 조율 (소스 비수정 — static 플래그 + 자체 키 폴링)
        // =====================================================================
        /// <summary>
        /// M키 이중 토글을 방지한다. 원본 소스는 수정하지 않고 런타임에만 끈다. (후속 라운드에서 원본 제거 시 제거 대상)
        /// </summary>
        private static void SuppressLegacyHotkey()
        {
            // [폐기 완료] 원본 핫키는 아카이브됨 — no-op (호출부 호환 유지)
        }

        // =====================================================================
        //  폴링 (schedule 규약)
        // =====================================================================
        private void StartLoops()
        {
            if (_keyTask == null)
                _keyTask = schedule.Execute(() => KeyTick()).Every(KeyMs);
            if (_pollTask == null)
                _pollTask = schedule.Execute(() => PollTick()).Every(PollMs);
        }

        /// <summary>M키/ESC — 닫힌 상태에서도 동작하도록 창이 부착되어 있으면 계속 tick.</summary>
        private void KeyTick()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.mKey.wasPressedThisFrame)
                HandleMKey();
            if (IsOpen && kb.escapeKey.wasPressedThisFrame)
                Hide();
        }

        private void HandleMKey()
        {
            ToggleWindow();
            UnityEngine.Debug.Log($"[WorldMapUTK] M키 토글 → {(IsOpen ? "열림" : "닫힘")}");
        }

        /// <summary>상태 갱신 — 열려 있을 때만. 플레이어 마커 + 소유/펄스.</summary>
        private void PollTick()
        {
            if (!IsOpen) return;
            UpdatePlayerMarker();
            float now = Time.realtimeSinceStartup;
            for (int i = 0; i < _territoryMarkers.Count; i++)
                _territoryMarkers[i].RefreshVisual(now);
            ApplyPlayerPulse(now);
            // [124차 Phase A] 지역 탭 행 소유색/소유상태/영주명 실시간 갱신 — 마커 RefreshVisual과 동일 폴링 계약
            for (int i = 0; i < _regionRows.Count; i++)
                _regionRows[i].Refresh();
            // [124차-ImplB] 우측 패널 실시간 갱신 — 소유 변동/수비 집계 반영(선택 영지 1건만 조회, 250ms 폴링 계약)
            RefreshQuestDetailPanel();
        }

        // =====================================================================
        //  마커 구성
        // =====================================================================
        /// <summary>영지 마커 재구성 — 창이 열릴 때마다 1회 (원본 RefreshDefinitions: 바깥 링 큰 순 정렬).</summary>
        private void RebuildMarkers()
        {
            // Rebuild only map contents: player and territory markers remain MapCanvas descendants.
            // TacticalCompass is a window-root sibling, so Clear() cannot remove or reparent it.
            _mapCanvas.Clear();
            _mapCanvas.Add(_playerMarker);

            _territoryMarkers.Clear();
            var db = TerritoryDatabase.Instance;
            if (db == null) return;

            var defs = new List<TerritoryDefinition>();
            var iter = db.GetAllDefinitions();
            if (iter != null)
            {
                var it = iter.GetEnumerator();
                while (it.MoveNext())
                    defs.Add(it.Current);
            }
            // 바깥 링(반경 큰 순) 먼저 배치 → 안쪽이 위(맨 나중 Add) — 원본 Sort 동일 효과
            defs.Sort((a, b) => CompareMag(b.worldPosition, a.worldPosition));

            for (int i = 0; i < defs.Count; i++)
            {
                var m = BuildTerritoryMarker(defs[i]);
                _mapCanvas.Add(m);
                _territoryMarkers.Add(m);
            }
            // [125차 후속7] 4방위국 아레나 핀 4곳 — 영지 핀과 동일 좌표 산식(WorldU/WorldV),
            //   월드 좌표는 TerritoryBuilder.GetArenaWorldPosition 단일 진실원(메인씬 임시 경기장 배치와 동일 좌표).
            BuildArenaMarkers();
            ApplyUIToolkitFont(_mapCanvas);
        }

        private static int CompareMag(Vector3 a, Vector3 b)
        {
            float sa = a.x * a.x + a.z * a.z;
            float sb = b.x * b.x + b.z * b.z;
            if (sa < sb) return -1;
            if (sa > sb) return 1;
            return 0;
        }

        private TerritoryMarker BuildTerritoryMarker(TerritoryDefinition def)
        {
            float u = WorldU(def.worldPosition);
            float v = WorldV(def.worldPosition);

            var m = new TerritoryMarker(def);
            m.style.position = Position.Absolute;
            m.style.left = Length.Percent(u * 100f);
            m.style.top = Length.Percent((1f - v) * 100f);   // GUI y 반전
            // [124차-ImplB] 핀 앵커 = PinBase 중심 — translate(-50%, -19px)(base 반높이), 라벨은 앵커 하단에 걸림
            m.style.translate = new Translate(Length.Percent(-50f), new Length(-19f, LengthUnit.Pixel));
            m.style.alignItems = Align.Center;
            m.style.flexDirection = FlexDirection.Column;
            m.pickingMode = PickingMode.Position;
            m.RegisterCallback<ClickEvent>(evt => SelectTerritory(def));   // [124차-ImplB] 클릭 선택 — 우측 패널 갱신
            return m;
        }

        // =====================================================================
        //  [125차 후속7] 아레나 핀 — 4방위국 임시 외관 경기장 지도 표기 (정적 핀, 호버 툴팁)
        // =====================================================================
        /// <summary>
        /// [125차 후속7] 4방위국 아레나 핀 4곳(동/북/서/남) 구성 — 메인씬 임시 경기장 외관
        ///   (TerritoryBuilder.BuildArenaRegion)의 월드 좌표를 영지 핀과 동일 산식
        ///   (WorldU/WorldV → left%/top% GUI y 반전)으로 표기한다.
        ///   소유 상태가 없는 건물 핀이므로 PollTick 폴링 대상에서 제외(정적 표기 + 호버 툴팁만).
        /// </summary>
        private void BuildArenaMarkers()
        {
            AddArenaMarker(NationType.East);
            AddArenaMarker(NationType.North);
            AddArenaMarker(NationType.West);
            AddArenaMarker(NationType.South);
            Debug.Log("[125차 후속7][WorldMapUTK] 아레나 핀 4곳 재구성 (동/북/서/남 — TerritoryBuilder.GetArenaWorldPosition 좌표 공유)");
        }

        /// <summary>[125차 후속7] 아레나 핀 1곳 배치 — BuildTerritoryMarker와 동일 산식(left% = u*100, top% = (1-v)*100, 앵커 -50%/-19px).</summary>
        private void AddArenaMarker(NationType nation)
        {
            Vector3 world = ProjectName.Systems.TerritoryBuilder.GetArenaWorldPosition(nation);
            float u = WorldU(world);
            float v = WorldV(world);

            var m = new ArenaMarker(nation);
            m.style.position = Position.Absolute;
            m.style.left = Length.Percent(u * 100f);
            m.style.top = Length.Percent((1f - v) * 100f);   // GUI y 반전 (영지 핀 동일 산식)
            // [125차 후속7] 핀 앵커 = PinBase 중심 — TerritoryMarker와 동일 translate(-50%, -19px)
            m.style.translate = new Translate(Length.Percent(-50f), new Length(-19f, LengthUnit.Pixel));
            m.style.alignItems = Align.Center;
            m.style.flexDirection = FlexDirection.Column;
            _mapCanvas.Add(m);
        }

        /// <summary>[125차 후속7] 국가 → 아레나 핀 라벨 ("동국 아레나" 등).</summary>
        private static string ArenaPinLabel(NationType n)
        {
            switch (n)
            {
                case NationType.East: return "동국 아레나";
                case NationType.West: return "서국 아레나";
                case NationType.South: return "남국 아레나";
                case NationType.North: return "북국 아레나";
                default: return "아레나";
            }
        }

        // =====================================================================
        //  플레이어 마커 (스로틀 재탐색 + 폴링 갱신)
        // =====================================================================
        private void UpdatePlayerMarker()
        {
            TryFindPlayer();
            if (_playerTransform == null)
            {
                _playerMarker.style.display = DisplayStyle.None;
                return;
            }
            _playerMarker.style.display = DisplayStyle.Flex;
            float u = WorldU(_playerTransform.position);
            float v = WorldV(_playerTransform.position);
            // [124차-ImplB] 실시간 좌표 추적 토글 — OFF면 핀 위치 고정(좌표 텍스트는 계속 갱신)
            if (_trackingEnabled)
            {
                _playerMarker.style.left = Length.Percent(u * 100f);
                _playerMarker.style.top = Length.Percent((1f - v) * 100f);
            }

            UpdateHeaderAndCompass();
        }

        // [F5] HUDHeader 좌표/위치 + TacticalCompass 회전 (플레이어 yaw → N 위쪽)
        private void UpdateHeaderAndCompass()
        {
            Vector3 p = _playerTransform.position;

            // 좌표 — [124차 Phase A] HUDHeader CoordinatesGroup 승계(구 위치줄 _coordLabel 폐기). Figma 계약: F2 실시간.
            if (_hudCoordValueLabel != null)
                _hudCoordValueLabel.text = $"X: {p.x:F2} / Y: {p.z:F2}";

            // 위치명 — 영지 해석 (TerritoryDatabase.ResolveTerritoryAt)
            //   [124차 Phase A] 구 위치줄 데이터 경로 승계 — 값만 HUDHeader 영지명 라벨로 갱신.
            //   [124차-ImplB] 플레이어 핀 라벨 동일 소스 승계 — "{영지명} (현재지)" (Figma PlayerPin 계약).
            {
                string loc = "야외";
                var td = TerritoryDatabase.Instance;
                if (td != null)
                {
                    var tid = td.ResolveTerritoryAt(p, 80f);
                    if (tid.HasValue)
                    {
                        var def = td.GetDefinition(tid.Value);
                        if (!string.IsNullOrEmpty(def.territoryName))
                            loc = def.territoryName;
                    }
                }
                if (_hudTerritoryNameLabel != null)
                    _hudTerritoryNameLabel.text = loc;
                if (_playerPinLabel != null)
                    _playerPinLabel.text = loc + " (현재지)";
            }

            if (_compass.style.display == DisplayStyle.None) return;   // [118차 R2] 나침반 폐기 — 회전 갱신 무동작

            // 나침반 — 플레이어 forward의 XZ yaw로 회전 (위쪽이 북쪽)
            if (_compass != null && _playerTransform != null)
            {
                float yaw = _playerTransform.eulerAngles.y;   // 0 = -Z (북) 기준
                _compass.style.rotate = new Rotate(new Angle(yaw, AngleUnit.Degree));
            }
        }

        private void TryFindPlayer()
        {
            if (Time.unscaledTime < _nextPlayerProbe) return;
            _nextPlayerProbe = Time.unscaledTime + 0.7f;
            var go = GameObject.FindWithTag("Player");
            _playerTransform = go != null ? go.transform : null;
        }

        private void ApplyPlayerPulse(float now)
        {
            if (_playerRing == null) return;
            float pulse = 0.5f + 0.5f * UnityEngine.Mathf.Sin(now * 4f);
            Color ring = GitHubDark.StatusGreen;   // [124차-ImplB] 핀 신형 — 헤일로 링 펄스(구 헤일로 펄스 승계)
            ring.a = 0.35f + 0.45f * pulse;
            var c = new StyleColor(ring);
            _playerRing.style.borderLeftColor = c;
            _playerRing.style.borderRightColor = c;
            _playerRing.style.borderTopColor = c;
            _playerRing.style.borderBottomColor = c;
        }

        // =====================================================================
        //  표기 헬퍼 (원본과 동일 산식)
        // =====================================================================
        private static string NationLabel(NationType n)
        {
            switch (n)
            {
                case NationType.East: return "동국(East)";
                case NationType.West: return "서국(West)";
                case NationType.South: return "남국(South)";
                case NationType.North: return "북국(North)";
                case NationType.Empire: return "황제국(Empire)";
                case NationType.Dracula: return "드라큘라(Dracula)";
                default: return "미소속";
            }
        }

        /// <summary>[124차-ImplB] public 승격 — WorldMapTerritoryIntelContent.BriefingBody가 난이도 라벨 소비(동일 어셈블리).</summary>
        public static string DifficultyLabel(TerritoryDifficulty d)
        {
            switch (d)
            {
                case TerritoryDifficulty.Ring1: return "링1 · 쉬움(외곽 1450m)";
                case TerritoryDifficulty.Ring2: return "링2 · 보통(1000m)";
                case TerritoryDifficulty.Ring3: return "링3 · 어려움(550m)";
                case TerritoryDifficulty.Ring4: return "링4 · 매우 어려움(150m)";
                case TerritoryDifficulty.Empire: return "황제국 · 최종";
                default: return "알 수 없음";
            }
        }

        private static string OwnershipLabel(TerritoryOwnership o)
        {
            switch (o)
            {
                case TerritoryOwnership.Unoccupied: return "미점령";
                case TerritoryOwnership.PlayerOwned: return "★ 내 영지";
                case TerritoryOwnership.LordOwned: return "영주 소유";
                case TerritoryOwnership.Contested: return "전쟁 중";
                default: return "미점령";
            }
        }

        private static float DifficultyMarkerSize(TerritoryDifficulty d)
        {
            switch (d)
            {
                case TerritoryDifficulty.Ring1: return 9f;
                case TerritoryDifficulty.Ring2: return 12f;
                case TerritoryDifficulty.Ring3: return 15f;
                case TerritoryDifficulty.Ring4: return 18f;
                case TerritoryDifficulty.Empire: return 26f;
                default: return 10f;
            }
        }

        private static Color NationColor(NationType n)
        {
            switch (n)
            {
                case NationType.East: return new Color(0.26f, 0.52f, 0.90f, 1f);
                case NationType.West: return new Color(0.26f, 0.66f, 0.30f, 1f);
                case NationType.South: return new Color(0.88f, 0.30f, 0.22f, 1f);
                case NationType.North: return new Color(0.58f, 0.36f, 0.85f, 1f);
                case NationType.Empire: return GitHubDark.Gold;   // 황제국 — 영지 마커 강조 골드 #E3B341
                case NationType.Dracula: return new Color(0.36f, 0.07f, 0.10f, 1f);
                default: return new Color(0.45f, 0.40f, 0.35f, 1f);
            }
        }

        // =====================================================================
        //  양피지 절차 텍스처 (원본 BuildParchment 동일 산식 복제 — 결정론 노이즈)
        // =====================================================================
        private static Texture2D s_parchment;

        private static Texture2D GetParchment()
        {
            if (s_parchment == null) s_parchment = BuildParchment(512);
            return s_parchment;
        }

        private static Texture2D BuildParchment(int size)
        {
            var tex = MakeCanvas(size, size, "WorldMapUTK_Parchment");
            var buf = new Color[size * size];
            Color cream = new Color(0.918f, 0.866f, 0.722f);
            Color sepiaShade = new Color(0.62f, 0.50f, 0.34f);
            Color sepiaInk = new Color(0.30f, 0.20f, 0.115f);
            float inv = 1f / (size - 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = x * inv, ny = y * inv;
                    float blotch = Fbm3(nx * 6f, ny * 6f);
                    float grain = Hash01(x * 7 + 1, y * 13 + 5);
                    float foldV = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(nx * Mathf.PI * 6f + blotch * 4f), 6f) * 0.09f;
                    float foldH = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(ny * Mathf.PI * 4f + blotch * 3f), 6f) * 0.06f;
                    float edgePx = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                    float warp = (Fbm3(nx * 7f + 11f, ny * 7f + 29f) - 0.5f) * 30f;
                    float edgeT = Mathf.Clamp01((edgePx + warp) / 52f);
                    float edgeDark = Mathf.Pow(1f - edgeT, 1.6f) * (0.45f + 0.40f * Fbm3(nx * 11f + 3f, ny * 11f + 7f));
                    float dx = nx - 0.5f, dy = ny - 0.5f;
                    float vig = Mathf.SmoothStep(0.55f, 1.0f, Mathf.Sqrt(dx * dx + dy * dy) * 1.4142f) * 0.20f;

                    float shade = 1f + (grain - 0.5f) * 0.14f + (blotch - 0.5f) * 0.10f + foldV + foldH;
                    Color c = cream * shade;
                    c = Color.Lerp(c, sepiaShade, Mathf.Clamp01(edgeDark * 0.8f + vig));

                    float border = 1f - Mathf.Clamp01((edgePx - 2f) / 3f);
                    c = Color.Lerp(c, sepiaInk * (0.85f + grain * 0.3f), border * 0.9f);

                    c.a = 1f;
                    buf[y * size + x] = c;
                }
            }
            tex.SetPixels(buf);
            tex.Apply(false, false);
            return tex;
        }

        private static Texture2D MakeCanvas(int w, int h, string name)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        private static float Hash01(int x, int y)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0x7FFFFFFF) / (float)0x7FFFFFFF;
            }
        }

        private static float ValueNoise(float x, float y)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            float sx = fx * fx * (3f - 2f * fx);
            float sy = fy * fy * (3f - 2f * fy);
            float n00 = Hash01(ix, iy);
            float n10 = Hash01(ix + 1, iy);
            float n01 = Hash01(ix, iy + 1);
            float n11 = Hash01(ix + 1, iy + 1);
            return Mathf.Lerp(Mathf.Lerp(n00, n10, sx), Mathf.Lerp(n01, n11, sx), sy);
        }

        private static float Fbm3(float x, float y)
        {
            float sum = 0f, amp = 0.5f, freq = 1f, norm = 0f;
            for (int o = 0; o < 3; o++)
            {
                sum += ValueNoise(x * freq, y * freq) * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2f;
            }
            return sum / norm;
        }

        // =====================================================================
        //  영지 마커 (public 컴포넌트 — CS0050 방지)
        // =====================================================================
        /// <summary>지도 캔버스 위 절대배치되는 영지 마커. 소유 상태/전쟁 펄스를 폴링으로 갱신.</summary>
        /// <summary>
        /// [124차-ImplB] 신형 핀 마커 — Figma 95:167 핀 3종(PlayerPin/QuestPin/IndustrialPin) 계약:
        /// PinBase 38×38 r19.2 fill {소유색}/0.10 + stroke {소유색} w1.8(앵커=PinBase 중심 — 루트 translate(-50%,-19px)),
        /// 내부 도트 10×10 solid(플레이어 소유=14×14 + 외곽 링 53×53 stroke #39D353 w1.2 중앙 헤일로),
        /// PinLabel base 하단(marginTop 8 → rel y 46) r4.8 fill #161B22/0.80 stroke #30363D/0.50 w1.2, text 13.2/700 #F0F6FC.
        /// 소유색=OwnershipContractColor(계획서 매핑: 플레이어 #39D353/영주 빨강/분쟁 #FF9E2C/무주 #8B949E).
        /// [123차] 호버 툴팁·클릭·250ms 폴링 계약 무변경. 구형 점+★+라벨 렌더 폐기(Figma 완전 정합).
        /// </summary>
        public sealed class TerritoryMarker : VisualElement
        {
            public readonly TerritoryDefinition Def;
            private readonly TerritoryDatabase _db;
            private readonly VisualElement _base;
            private readonly VisualElement _dot;
            private readonly VisualElement _ring;
            private readonly Label _name;
            private VisualElement _labelChip;           // [144차 P0-WM #4] 2행 라벨 칩(크롬 소유) — [147차] 생성자 미대입 결함(RefreshVisual 가드 참조)
            private bool _labelChipNullLogged;          // [147차] NRE 소음 차단 — 원인 경고 로그 1회만(Play 세션 1,692회 실측, 계획 §0-D)
            private Label _pinDifficultyLabel;          // [144차 P0-WM #4] 제2행 난이도(실데이터 링 표기)
            private bool _selected;

            public TerritoryMarker(TerritoryDefinition def)
            {
                Def = def;
                _db = TerritoryDatabase.Instance;
                name = "TerritoryMarker_" + def.id;

                // PinBase — 앵커 원 (38×38 r19.2)
                _base = new VisualElement { name = "PinBase" };
                _base.pickingMode = PickingMode.Position;
                _base.style.width = 38f;
                _base.style.height = 38f;
                SetCornerRadius(_base, 19.2f);
                _base.style.borderTopWidth = 1.8f;
                _base.style.borderBottomWidth = 1.8f;
                _base.style.borderLeftWidth = 1.8f;
                _base.style.borderRightWidth = 1.8f;
                _base.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
                Add(_base);

                // 내부 도트 — PinBase 중앙 (플레이어 소유 14×14, 그 외 10×10 — RefreshVisual에서 스왑)
                _dot = new VisualElement { name = "PinDot" };
                _dot.pickingMode = PickingMode.Ignore;
                _dot.style.position = Position.Absolute;
                _dot.style.left = 12f;
                _dot.style.top = 12f;
                _dot.style.width = 14f;
                _dot.style.height = 14f;
                SetCornerRadius(_dot, 7f);
                _base.Add(_dot);

                // 외곽 링 — 플레이어 소유 전용 헤일로 (53×53, base 중심 rel(-7,-7))
                _ring = new VisualElement { name = "PinRing" };
                _ring.pickingMode = PickingMode.Ignore;
                _ring.style.position = Position.Absolute;
                _ring.style.left = -7.5f;
                _ring.style.top = -7.5f;
                _ring.style.width = 53f;
                _ring.style.height = 53f;
                SetCornerRadius(_ring, 26.5f);
                _ring.style.borderTopWidth = 1.2f;
                _ring.style.borderBottomWidth = 1.2f;
                _ring.style.borderLeftWidth = 1.2f;
                _ring.style.borderRightWidth = 1.2f;
                _ring.style.display = DisplayStyle.None;
                _base.Add(_ring);

                // PinLabel — 라벨 칩 (base 하단, marginTop 8 → rel y 46)
                // [144차 P0-WM #4] 핀 라벨 2행화(95:249) — 영지명 + 제2행 난이도(Geist Mono 근사 12/700 #FF9E2C).
                //   Figma 샘플 "Lv.45"의 수치 데이터는 부재(난이도=링 계약) → 실데이터 링 표기("링N"/"황제국") 바인딩
                //   (프로토콜: 샘플 데이터 치환은 괴리 미산정 — 실데이터 우선).
                var labelChip = new VisualElement { name = "PinLabel" };
                labelChip.style.backgroundColor = new StyleColor(new Color(0x16 / 255f, 0x1B / 255f, 0x22 / 255f, 0.80f));
                labelChip.style.paddingLeft = 10f;
                labelChip.style.paddingRight = 10f;
                labelChip.style.paddingTop = 5f;
                labelChip.style.paddingBottom = 5f;
                SetCornerRadius(labelChip, 4.8f);
                SetLabelStroke(labelChip, new Color(0x30 / 255f, 0x36 / 255f, 0x3D / 255f, 0.50f));
                labelChip.style.marginTop = 8f;
                labelChip.pickingMode = PickingMode.Ignore;

                _name = new Label(def.territoryName);
                _name.style.fontSize = 13.2f;
                _name.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
                _name.style.color = new StyleColor(GitHubDark.TextMain);
                _name.style.unityTextAlign = TextAnchor.MiddleCenter;
                _name.style.whiteSpace = WhiteSpace.NoWrap;
                _name.pickingMode = PickingMode.Ignore;
                labelChip.Add(_name);

                _pinDifficultyLabel = new Label(DifficultyPinLabel(def.difficulty));
                _pinDifficultyLabel.name = "PinDifficulty";
                _pinDifficultyLabel.style.fontSize = 12f;
                _pinDifficultyLabel.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
                _pinDifficultyLabel.style.color = new StyleColor(new Color(0xFF / 255f, 0x9E / 255f, 0x2C / 255f, 1f));   // #FF9E2C (95:249)
                _pinDifficultyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                _pinDifficultyLabel.style.marginTop = 2.4f;
                _pinDifficultyLabel.pickingMode = PickingMode.Ignore;
                labelChip.Add(_pinDifficultyLabel);

                Add(labelChip);

                // [123차] P2-1 호버 영주 툴팁 — PointerEnter 표시 / PointerMove 커서 추적 / PointerLeave 숨김 (계약 무변경).
                RegisterCallback<PointerEnterEvent>(_ => ShowTerritoryTooltip());
                RegisterCallback<PointerMoveEvent>(_ => ShowTerritoryTooltip());
                RegisterCallback<PointerLeaveEvent>(_ => UTKTooltip.Hide());
            }

            /// <summary>[124차-ImplB] 선택 강조 — PinBase stroke → #58A6FF + PinLabel 보더 액센트(Figma 카드 선택 언어 재사용).</summary>
            public void SetSelected(bool selected)
            {
                _selected = selected;
                RefreshVisual(UnityEngine.Time.realtimeSinceStartup);
            }

            // ===== [123차] P2-1/P2-2 호버 영주 툴팁 — 계약 무변경 =====
            /// <summary>호버 시점 실시간 상태 조회 → 4줄 툴팁 표시. PointerMove마다 재호출로 커서 추적.</summary>
            private void ShowTerritoryTooltip()
            {
                var db = _db != null ? _db : TerritoryDatabase.Instance;
                TerritoryState state = db != null ? db.GetState(Def.id) : null;
                UTKTooltip.Show(WorldMapTerritoryTooltipContent.Build(Def, state), GetPanelPointerPos());
            }

            /// <summary>[U8 수리 v2 검증 수식 동일 적용] 마우스 실측 → 패널 좌표 변환 (UTKDragDrop.GetPanelPointerPos 동일 산식).</summary>
            private static Vector2 GetPanelPointerPos()
            {
                var root = UIToolkitBootstrap.UIRoot;
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (root == null || mouse == null) return Vector2.zero;
                var screen = mouse.position.ReadValue();               // 실제 픽셀, y 하단 원점
                float scale = root.worldBound.width / (float)Screen.width;
                if (scale <= 0f) scale = 1f;
                return new Vector2(screen.x * scale, root.worldBound.height - screen.y * scale);
            }

            /// <summary>소유 상태/전쟁 펄스/선택 강조를 실시간 반영 (폴링 호출) — 핀 신형 색 갱신.</summary>
            public void RefreshVisual(float now)
            {
                TerritoryOwnership own = _db != null ? (_db.GetState(Def.id)?.ownership ?? TerritoryOwnership.Unoccupied)
                                                     : TerritoryOwnership.Unoccupied;
                Color ownColor = OwnershipContractColor(own);
                float pulse = 0.5f + 0.5f * UnityEngine.Mathf.Sin(now * 6f);

                // PinBase fill/stroke — 소유색/0.10 + 소유색 w1.8 (Contested 펄스, 선택 시 #58A6FF 고정)
                Color fill = ownColor;
                fill.a = 0.10f;
                _base.style.backgroundColor = new StyleColor(fill);
                Color stroke = _selected
                    ? GitHubDark.Accent
                    : (own == TerritoryOwnership.Contested
                        ? new Color(ColorWar.r, ColorWar.g, ColorWar.b, 0.30f + 0.60f * pulse)
                        : ownColor);
                SetBaseStroke(stroke);

                // 도트 — 소유색 solid (플레이어 소유 14×14 / 그 외 10×10)
                bool player = own == TerritoryOwnership.PlayerOwned;
                float d = player ? 14f : 10f;
                float off = player ? 12f : 14f;
                _dot.style.left = off;
                _dot.style.top = off;
                _dot.style.width = d;
                _dot.style.height = d;
                SetCornerRadius(_dot, d * 0.5f);
                _dot.style.backgroundColor = new StyleColor(ownColor);

                // 외곽 링 — 플레이어 소유 전용
                _ring.style.display = player ? DisplayStyle.Flex : DisplayStyle.None;

                // PinLabel 보더 — 선택 시 액센트, 그 외 #30363D/0.50
                // [147차] NRE 수리(동작 무변경) — _labelChip은 생성자가 로컬 labelChip만 만들고 미대입 → 항상 null
                //   (PollTick 250ms마다 NRE로 폴링 전체 중단 — Play 세션 1,692회 실측, 계획 §0-D/§7 확정사실 1).
                //   null 시 1회 경고 로그 후 SetLabelStroke 스킵(예외 소멸만). 필드 대입(_labelChip = labelChip)은
                //   표기 변화(동작 변경)라 금지.
                if (_labelChip == null)
                {
                    if (!_labelChipNullLogged)
                    {
                        _labelChipNullLogged = true;
                        UnityEngine.Debug.LogWarning("[147차] [WorldMapUTK] TerritoryMarker.RefreshVisual — _labelChip null(생성자 로컬 labelChip 미대입), SetLabelStroke 스킵(동작 무변경 가드)");
                    }
                }
                else
                {
                    SetLabelStroke(_labelChip, _selected ? GitHubDark.Accent : new Color(0x30 / 255f, 0x36 / 255f, 0x3D / 255f, 0.50f));   // [144차] 크롬이 칩으로 이동
                }
            }

            private void SetBaseStroke(Color c)
            {
                var sc = new StyleColor(c);
                _base.style.borderTopColor = sc;
                _base.style.borderBottomColor = sc;
                _base.style.borderLeftColor = sc;
                _base.style.borderRightColor = sc;
            }

            private void SetLabelStroke(VisualElement target, Color c)
            {
                var sc = new StyleColor(c);
                target.style.borderTopColor = sc;
                target.style.borderBottomColor = sc;
                target.style.borderLeftColor = sc;
                target.style.borderRightColor = sc;
            }

            /// <summary>[144차 P0-WM #4] 핀 제2행 난이도 축약 — 실데이터 링 표기(Figma 샘플 "Lv.45" 수치 부재).</summary>
            private static string DifficultyPinLabel(TerritoryDifficulty d)
            {
                switch (d)
                {
                    case TerritoryDifficulty.Ring1: return "링1";
                    case TerritoryDifficulty.Ring2: return "링2";
                    case TerritoryDifficulty.Ring3: return "링3";
                    case TerritoryDifficulty.Ring4: return "링4";
                    case TerritoryDifficulty.Empire: return "황제국";
                    default: return "—";
                }
            }
        }

        // =====================================================================
        //  [125차 후속7] 아레나 핀 컴포넌트 — 영지 핀(TerritoryMarker)과 시각 구별되는 건물 핀
        // =====================================================================
        /// <summary>
        /// [125차 후속7] 아레나 핀 — TerritoryMarker 신형 핀 언어(Figma 계약: 38×38 r19.2 베이스 + 도트 + 라벨 칩) 재사용,
        ///   베이스를 45° 다이아몬드로 회전해 영지 원형 핀과 시각 구별. 색 = 국가색(NationColor), 라벨 = "{국가명} 아레나".
        ///   소유 개념이 없는 건물 핀이므로 RefreshVisual/폴링 없음 — 호버 툴팁만
        ///   (마우스→패널 좌표 산식은 TerritoryMarker.GetPanelPointerPos와 동일 복제).
        /// </summary>
        private sealed class ArenaMarker : VisualElement
        {
            private readonly NationType _nation;

            public ArenaMarker(NationType nation)
            {
                _nation = nation;
                name = "ArenaMarker_" + nation;

                Color nColor = NationColor(nation);   // 국가색 — 영지 핀 NationColor 동일 사용

                // ArenaPinBase — 45° 다이아몬드 (TerritoryMarker PinBase 38×38 r19.2 동일 크기 + rotate 45°)
                var baseEl = new VisualElement { name = "ArenaPinBase" };
                baseEl.pickingMode = PickingMode.Position;
                baseEl.style.width = 38f;
                baseEl.style.height = 38f;
                SetCornerRadius(baseEl, 19.2f);
                Color fill = nColor; fill.a = 0.10f;
                baseEl.style.backgroundColor = new StyleColor(fill);
                var stroke = new StyleColor(nColor);
                baseEl.style.borderTopWidth = 1.8f; baseEl.style.borderBottomWidth = 1.8f;
                baseEl.style.borderLeftWidth = 1.8f; baseEl.style.borderRightWidth = 1.8f;
                baseEl.style.borderTopColor = stroke; baseEl.style.borderBottomColor = stroke;
                baseEl.style.borderLeftColor = stroke; baseEl.style.borderRightColor = stroke;
                baseEl.style.rotate = new Rotate(new Angle(45f, AngleUnit.Degree));
                Add(baseEl);

                // 도트 — 베이스 중앙(회전 상쇄 -45°). 원형 도트 대신 작은 사각 도트로 영지 핀과 구별.
                var dot = new VisualElement { name = "ArenaPinDot" };
                dot.pickingMode = PickingMode.Ignore;
                dot.style.position = Position.Absolute;
                dot.style.left = 14f; dot.style.top = 14f;
                dot.style.width = 10f; dot.style.height = 10f;
                SetCornerRadius(dot, 2f);
                dot.style.backgroundColor = new StyleColor(nColor);
                dot.style.rotate = new Rotate(new Angle(-45f, AngleUnit.Degree));
                baseEl.Add(dot);

                // ArenaPinLabel — 라벨 칩 (TerritoryMarker PinLabel 동일 스펙: 13.2/700 + r4.8 + 보더)
                var label = new Label(ArenaPinLabel(nation));
                label.name = "ArenaPinLabel";
                label.style.fontSize = 13.2f;
                label.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
                label.style.color = new StyleColor(GitHubDark.TextMain);
                label.style.backgroundColor = new StyleColor(new Color(0x16 / 255f, 0x1B / 255f, 0x22 / 255f, 0.80f));
                label.style.paddingLeft = 10f; label.style.paddingRight = 10f;
                label.style.paddingTop = 5f; label.style.paddingBottom = 5f;
                SetCornerRadius(label, 4.8f);
                var lblStroke = new StyleColor(new Color(0x30 / 255f, 0x36 / 255f, 0x3D / 255f, 0.50f));
                label.style.borderTopWidth = 1.2f; label.style.borderBottomWidth = 1.2f;
                label.style.borderLeftWidth = 1.2f; label.style.borderRightWidth = 1.2f;
                label.style.borderTopColor = lblStroke; label.style.borderBottomColor = lblStroke;
                label.style.borderLeftColor = lblStroke; label.style.borderRightColor = lblStroke;
                label.style.marginTop = 8f;
                label.style.whiteSpace = WhiteSpace.NoWrap;
                label.pickingMode = PickingMode.Ignore;
                Add(label);

                // [125차 후속7] 호버 툴팁 — 영지 핀과 동일 패턴(Enter/Move 커서 추적, Leave 숨김)
                RegisterCallback<PointerEnterEvent>(_ => ShowArenaTooltip());
                RegisterCallback<PointerMoveEvent>(_ => ShowArenaTooltip());
                RegisterCallback<PointerLeaveEvent>(_ => UTKTooltip.Hide());
            }

            /// <summary>[125차 후속7] 아레나 툴팁 4줄 — WorldMapTerritoryTooltipContent.Build 4줄 계약 형식 준수.</summary>
            private void ShowArenaTooltip()
            {
                string angle = ProjectName.Systems.TerritoryBuilder.GetArenaAngleDeg(_nation).ToString("F0");
                string dist = ProjectName.Systems.TerritoryBuilder.ArenaRingDistance.ToString("F0");
                UTKTooltip.Show(
                    ArenaPinLabel(_nation) + "\n"
                    + NationLabel(_nation) + " 국별 투기장\n"
                    + "방위각 " + angle + "° · 링 중간 거리 " + dist + "m\n"
                    + "현장 문(E키)에서 실내 입장",
                    GetPanelPointerPos());
            }

            /// <summary>[U8 수리 v2 검증 수식 동일 적용] 마우스 실측 → 패널 좌표 변환 (TerritoryMarker.GetPanelPointerPos 동일 복제).</summary>
            private static Vector2 GetPanelPointerPos()
            {
                var root = UIToolkitBootstrap.UIRoot;
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (root == null || mouse == null) return Vector2.zero;
                var screen = mouse.position.ReadValue();               // 실제 픽셀, y 하단 원점
                float scale = root.worldBound.width / (float)Screen.width;
                if (scale <= 0f) scale = 1f;
                return new Vector2(screen.x * scale, root.worldBound.height - screen.y * scale);
            }
        }

        /// <summary>[124차 Phase A] 지역 탭 행 뷰 — 계획서 카드 언어 동일(r9.6 + #FFFFFF/0.08)의 컴팩트 행:
        /// TierStrip 5×행높이(소유색) + 영지명 16.8/700 #F0F6FC + 소유 상태 13.2(계약색) + 영주명 13.2/400 #8B949E.
        /// 소유/영주 데이터는 [123차] 툴팁 포맷터와 동일 소스(GetState → ownerLordName) — PollTick Refresh 실시간.</summary>
        private sealed class RegionRowView : VisualElement
        {
            public readonly TerritoryDefinition Def;
            private readonly TerritoryDatabase _db;
            private readonly VisualElement _strip;
            private readonly Label _name;
            private readonly Label _state;
            private readonly Label _lord;

            public RegionRowView(TerritoryDefinition def)
            {
                Def = def;
                _db = TerritoryDatabase.Instance;
                name = "RegionRow_" + def.id;

                style.height = 56f;   // 컴팩트 행 — 계획서에 행높이 진실치 없음, 카드 언어 동일 컴팩트(텍스트 1행 기준)
                style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.08f));
                style.borderTopLeftRadius = 9.6f;
                style.borderTopRightRadius = 9.6f;
                style.borderBottomLeftRadius = 9.6f;
                style.borderBottomRightRadius = 9.6f;
                style.marginBottom = 8f;
                style.overflow = Overflow.Hidden;   // TierStrip 코너 클리핑 — 행 r9.6 정합
                style.flexDirection = FlexDirection.Row;
                style.alignItems = Align.Center;
                pickingMode = PickingMode.Position;   // 행 클릭 = SelectTerritory

                _strip = new VisualElement { name = "TierStrip" };
                _strip.style.width = 5f;
                _strip.style.height = 56f;
                _strip.pickingMode = PickingMode.Ignore;
                Add(_strip);

                _name = new Label(def.territoryName);
                _name.style.fontSize = 16.8f;
                _name.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
                _name.style.color = new StyleColor(GitHubDark.TextMain);
                _name.style.marginLeft = 12f;
                _name.style.whiteSpace = WhiteSpace.NoWrap;
                _name.style.flexShrink = 1f;
                _name.pickingMode = PickingMode.Ignore;
                Add(_name);

                _state = new Label("—");
                _state.style.fontSize = 13.2f;
                _state.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
                _state.style.marginLeft = 8f;
                _state.style.whiteSpace = WhiteSpace.NoWrap;
                _state.pickingMode = PickingMode.Ignore;
                Add(_state);

                _lord = new Label("—");
                _lord.style.fontSize = 13.2f;
                _lord.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Normal;
                _lord.style.color = new StyleColor(GitHubDark.TextSub);
                _lord.style.marginLeft = 8f;
                _lord.style.marginRight = 12f;
                _lord.style.flexGrow = 1f;
                _lord.style.unityTextAlign = TextAnchor.MiddleRight;
                _lord.style.whiteSpace = WhiteSpace.NoWrap;
                _lord.pickingMode = PickingMode.Ignore;
                Add(_lord);

                Refresh();
            }

            /// <summary>소유 상태/색/영주명 실시간 갱신 — 툴팁([123차])과 동일 데이터 경로(GetState → ownerLordName).</summary>
            public void Refresh()
            {
                var db = _db != null ? _db : TerritoryDatabase.Instance;
                TerritoryState state = db != null ? db.GetState(Def.id) : null;
                TerritoryOwnership own = state != null ? state.ownership : TerritoryOwnership.Unoccupied;

                Color ownColor = OwnershipContractColor(own);
                _strip.style.backgroundColor = new StyleColor(ownColor);
                _state.text = WorldMapTerritoryTooltipContent.OwnershipStateLine(own);   // 기존 계약 재사용
                _state.style.color = new StyleColor(ownColor);

                // 영주명 — OwnerLordLine("영주: X") 계약 재사용, 행 표기는 이름만(계획서: 영주명 항목)
                string lordLine = WorldMapTerritoryTooltipContent.OwnerLordLine(Def, state);
                _lord.text = lordLine.StartsWith("영주: ") ? lordLine.Substring("영주: ".Length) : lordLine;
            }
        }
    }
}