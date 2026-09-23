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
        private const float WinW = 780f;
        private const float WinH = 660f;

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
        private VisualElement _playerHalo;
        private readonly List<TerritoryMarker> _territoryMarkers = new List<TerritoryMarker>(96);

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
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            // 제목 + 범례 힌트
            var legend = new Label("● 영지   ★ 내 영지   붉은 테두리: 전쟁 중   👑 황제국   🧛 드라큘라");
            legend.AddToClassList("utk-title-label");
            legend.style.fontSize = 13f;
            legend.style.color = new StyleColor(GitHubDark.TextSub);
            legend.style.whiteSpace = WhiteSpace.Normal;
            // 헤더 범례 스트립 — 보조 패널 바탕 + 1px 스트로크 + r6 (GitHub-dark 서브 헤더)
            legend.style.backgroundColor = new StyleColor(GitHubDark.PanelSub);
            legend.style.paddingTop = 6f;
            legend.style.paddingBottom = 6f;
            legend.style.paddingLeft = 8f;
            legend.style.paddingRight = 8f;
            legend.style.borderTopWidth = 1f;
            legend.style.borderBottomWidth = 1f;
            legend.style.borderLeftWidth = 1f;
            legend.style.borderRightWidth = 1f;
            legend.style.borderTopColor = new StyleColor(GitHubDark.Stroke);
            legend.style.borderBottomColor = new StyleColor(GitHubDark.Stroke);
            legend.style.borderLeftColor = new StyleColor(GitHubDark.Stroke);
            legend.style.borderRightColor = new StyleColor(GitHubDark.Stroke);
            legend.style.borderTopLeftRadius = 6f;
            legend.style.borderTopRightRadius = 6f;
            legend.style.borderBottomLeftRadius = 6f;
            legend.style.borderBottomRightRadius = 6f;
            _content.Add(legend);

            // 지도 캔버스 — 양피지 배경 + 마커 절대배치 호스트
            _mapCanvas = new VisualElement();
            _mapCanvas.name = "MapCanvas";
            _mapCanvas.AddToClassList("wm-map-canvas");
            _mapCanvas.style.flexGrow = 1f;
            _mapCanvas.style.marginTop = 6f;
            _mapCanvas.style.position = Position.Relative;
            _mapCanvas.style.aspectRatio = 1f;                       // 양피지(정사각) 비율 유지
            _mapCanvas.style.backgroundImage = UTKTextureSafe.ToBackground(GetParchment());
            _mapCanvas.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
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

            // 컨테이너 폰트 (이미지 배경/Label용) — 생성 시점 레이어 포함
            ApplyUIToolkitFont(this);

            // 플레이어 마커 (캔버스 위 절대배치 — 좌표는 폴링으로 갱신)
            _playerMarker = new VisualElement();
            _playerMarker.name = "PlayerMarker";
            _playerMarker.style.position = Position.Absolute;
            _playerMarker.style.translate = new Translate(Length.Percent(-50f), Length.Percent(-50f));
            _playerMarker.style.display = DisplayStyle.None;

            _playerHalo = new VisualElement();
            _playerHalo.name = "PlayerHalo";
            _playerHalo.style.width = 14f;
            _playerHalo.style.height = 14f;
            _playerHalo.style.borderLeftWidth = 2f;
            _playerHalo.style.borderRightWidth = 2f;
            _playerHalo.style.borderTopWidth = 2f;
            _playerHalo.style.borderBottomWidth = 2f;
            _playerHalo.style.borderLeftColor = new StyleColor(GitHubDark.Accent);
            _playerHalo.style.borderRightColor = new StyleColor(GitHubDark.Accent);
            _playerHalo.style.borderTopColor = new StyleColor(GitHubDark.Accent);
            _playerHalo.style.borderBottomColor = new StyleColor(GitHubDark.Accent);
            _playerHalo.style.borderTopLeftRadius = 8f;
            _playerHalo.style.borderTopRightRadius = 8f;
            _playerHalo.style.borderBottomLeftRadius = 8f;
            _playerHalo.style.borderBottomRightRadius = 8f;
            _playerMarker.Add(_playerHalo);
            _mapCanvas.Add(_playerMarker);

            // 창 크롬(본체/타이틀바/닫기) GitHub-dark 리스타일 — 생성 시 1회
            ApplyGitHubDarkWindowStyle();

            // 기본 숨김 + 중앙 배치 (드래그 전까지)
            style.display = DisplayStyle.None;
            style.left = Length.Percent(50f);
            style.top = Length.Percent(50f);
            style.translate = new Translate(Length.Percent(-50f), Length.Percent(-50f));
        }

        /// <summary>창 크롬(본체/타이틀바/닫기버튼) GitHub-dark 리스타일 — 이 창 한정 인라인 오버라이드(생성 시 1회).</summary>
        private void ApplyGitHubDarkWindowStyle()
        {
            // 창 본체: 우드 베이크 이미지/브론즈 베벨 2px → 다크 패널 + 1px 스트로크 + r8 (이 창에서만.
            // 지도 양피지 배경(_mapCanvas backgroundImage)은 유지 — 본체 크롬만 교체)
            style.backgroundColor = GitHubDark.Panel;
            style.backgroundImage = new StyleBackground(StyleKeyword.None);
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 1f;
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = GitHubDark.Stroke;
            style.borderTopLeftRadius = 8f;
            style.borderTopRightRadius = 8f;
            style.borderBottomLeftRadius = 8f;
            style.borderBottomRightRadius = 8f;   // 메인 반경 r8
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

            // 닫기 버튼: 보조 패널 바탕 + r4(작은배지). 베이크 텍스처 미사용 — 기본 ✕ 텍스트 유지.
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

        // =====================================================================
        //  생명주기
        // =====================================================================
        public override void Show()
        {
            base.Show();   // Register + 표시 + OnWindowOpen
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            RebuildMarkers();
            UnityEngine.Debug.Log("[WorldMapUTK] 월드맵 열림");
        }

        public override void Hide()
        {
            base.Hide();
            UnityEngine.Debug.Log("[WorldMapUTK] 월드맵 닫힘");
        }

        private bool _legacyMapPreviouslyOpen;

        private void ToggleWindow()
        {
            if (IsOpen) { Hide(); return; }
            Show();
        }

        protected override void OnWindowOpen()
        {
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
        }

        // =====================================================================
        //  마커 구성
        // =====================================================================
        /// <summary>영지 마커 재구성 — OnShow 1회 (원본 RefreshDefinitions: 바깥 링 큰 순 정렬).</summary>
        private void RebuildMarkers()
        {
            _mapCanvas.Clear();
            _mapCanvas.Add(_playerMarker);   // 플레이어 마커는 최상위 유지

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
            m.style.translate = new Translate(Length.Percent(-50f), Length.Percent(-50f));
            m.style.alignItems = Align.Center;
            m.style.flexDirection = FlexDirection.Column;
            m.pickingMode = PickingMode.Position;
            m.RegisterCallback<ClickEvent>(evt => OnTerritoryClicked(def));
            return m;
        }

        private void OnTerritoryClicked(TerritoryDefinition def)
        {
            var db = TerritoryDatabase.Instance;
            TerritoryState state = db != null ? db.GetState(def.id) : null;
            TerritoryOwnership own = state != null ? state.ownership : TerritoryOwnership.Unoccupied;
            UnityEngine.Debug.Log($"[WorldMapUTK] 영지 클릭: {def.territoryName} " +
                $"(국가={NationLabel(def.nation)}, 난이도={DifficultyLabel(def.difficulty)}, 병사={def.guardCount}명, 소유={OwnershipLabel(own)}) — TerritoryInfoPopup 연동은 후속 라운드");
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
            _playerMarker.style.left = Length.Percent(u * 100f);
            _playerMarker.style.top = Length.Percent((1f - v) * 100f);
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
            if (_playerTransform == null) return;
            float pulse = 0.5f + 0.5f * UnityEngine.Mathf.Sin(now * 4f);
            Color halo = GitHubDark.Accent;
            halo.a = 0.35f + 0.45f * pulse;
            var c = new StyleColor(halo);
            _playerHalo.style.borderLeftColor = c;
            _playerHalo.style.borderRightColor = c;
            _playerHalo.style.borderTopColor = c;
            _playerHalo.style.borderBottomColor = c;
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

        private static string DifficultyLabel(TerritoryDifficulty d)
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
        public sealed class TerritoryMarker : VisualElement
        {
            public readonly TerritoryDefinition Def;
            private readonly TerritoryDatabase _db;
            private readonly VisualElement _dot;
            private readonly Label _name;
            private readonly Label _star;

            public TerritoryMarker(TerritoryDefinition def)
            {
                Def = def;
                _db = TerritoryDatabase.Instance;
                name = "TerritoryMarker_" + def.id;

                float size = DifficultyMarkerSize(def.difficulty);
                _dot = new VisualElement();
                _dot.name = "Dot";
                _dot.pickingMode = PickingMode.Position;
                _dot.style.width = size;
                _dot.style.height = size;
                // 테두리 (4면 개별 — IStyle 셧스루헛 금지)
                SetDotBorderWidth(2f);
                _dot.style.borderTopLeftRadius = size * 0.5f;
                _dot.style.borderTopRightRadius = size * 0.5f;
                _dot.style.borderBottomLeftRadius = size * 0.5f;
                _dot.style.borderBottomRightRadius = size * 0.5f;
                _dot.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
                Add(_dot);

                // ★ 내 영지 표식 (소유 시에만 표시) — 강조 골드 #E3B341 + 다크 칩(양피지 위 가독성)
                _star = new Label("★");
                _star.style.fontSize = 13f;
                _star.style.color = new StyleColor(GitHubDark.Gold);
                _star.style.backgroundColor = new StyleColor(MarkerChipBg());
                _star.style.paddingLeft = 3f;
                _star.style.paddingRight = 3f;
                _star.style.borderTopWidth = 1f;
                _star.style.borderBottomWidth = 1f;
                _star.style.borderLeftWidth = 1f;
                _star.style.borderRightWidth = 1f;
                _star.style.borderTopColor = new StyleColor(GitHubDark.Stroke);
                _star.style.borderBottomColor = new StyleColor(GitHubDark.Stroke);
                _star.style.borderLeftColor = new StyleColor(GitHubDark.Stroke);
                _star.style.borderRightColor = new StyleColor(GitHubDark.Stroke);
                _star.style.borderTopLeftRadius = 4f;
                _star.style.borderTopRightRadius = 4f;
                _star.style.borderBottomLeftRadius = 4f;
                _star.style.borderBottomRightRadius = 4f;
                _star.style.display = DisplayStyle.None;
                _star.pickingMode = PickingMode.Ignore;
                Add(_star);

                // 영지 이름 (Empire/Dracula 특별 접두 — 원본 TitleLine)
                string prefix = def.nation == NationType.Empire ? "👑 " :
                                def.nation == NationType.Dracula ? "🧛 " : "";
                _name = new Label(prefix + def.territoryName);
                _name.style.fontSize = 11f;
                // 이름 칩 — 다크 패널 바탕 + 1px 스트로크 + r4 (양피지 위 가독성 확보, GitHub-dark 서브 배지)
                _name.style.color = new StyleColor(GitHubDark.TextMain);
                _name.style.backgroundColor = new StyleColor(MarkerChipBg());
                _name.style.paddingLeft = 4f;
                _name.style.paddingRight = 4f;
                _name.style.paddingTop = 1f;
                _name.style.paddingBottom = 1f;
                _name.style.borderTopWidth = 1f;
                _name.style.borderBottomWidth = 1f;
                _name.style.borderLeftWidth = 1f;
                _name.style.borderRightWidth = 1f;
                _name.style.borderTopColor = new StyleColor(GitHubDark.Stroke);
                _name.style.borderBottomColor = new StyleColor(GitHubDark.Stroke);
                _name.style.borderLeftColor = new StyleColor(GitHubDark.Stroke);
                _name.style.borderRightColor = new StyleColor(GitHubDark.Stroke);
                _name.style.borderTopLeftRadius = 4f;
                _name.style.borderTopRightRadius = 4f;
                _name.style.borderBottomLeftRadius = 4f;
                _name.style.borderBottomRightRadius = 4f;
                _name.style.whiteSpace = WhiteSpace.NoWrap;
                _name.style.marginTop = 2f;
                _name.pickingMode = PickingMode.Ignore;
                Add(_name);
            }

            /// <summary>소유 상태/전쟁 펄스를 실시간 반영 (폴링 호출).</summary>
            public void RefreshVisual(float now)
            {
                TerritoryOwnership own = _db != null ? (_db.GetState(Def.id)?.ownership ?? TerritoryOwnership.Unoccupied)
                                                     : TerritoryOwnership.Unoccupied;
                float pulse = 0.5f + 0.5f * UnityEngine.Mathf.Sin(now * 6f);

                if (own == TerritoryOwnership.Unoccupied)
                {
                    // 속 빈 마커 — 테두리만 국가색
                    _dot.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
                    SetDotBorder(NationColor(Def.nation), 2f);
                }
                else
                {
                    _dot.style.backgroundColor = new StyleColor(NationColor(Def.nation));
                    if (own == TerritoryOwnership.PlayerOwned)
                    {
                        SetDotBorder(GitHubDark.Gold, 2f);   // 내 영지 — 강조 골드 #E3B341
                    }
                    else if (own == TerritoryOwnership.Contested)
                    {
                        Color war = ColorWar;
                        war.a = 0.30f + 0.60f * pulse;
                        SetDotBorder(war, 2f);
                    }
                    else
                    {
                        SetDotBorder(GitHubDark.Stroke, 1f);   // 영주 소유 — 보더 스트로크 #2E343D
                    }
                }

                _star.style.display = (own == TerritoryOwnership.PlayerOwned)
                    ? DisplayStyle.Flex : DisplayStyle.None;
            }

            /// <summary>양피지 위 가독성용 마커 칩 바닥 — 패널 #161B22 @ 85% 알파 (지도 배경 위 칩 전용).</summary>
            private static Color MarkerChipBg()
            {
                Color c = GitHubDark.Panel;
                c.a = 0.85f;
                return c;
            }

            private void SetDotBorder(Color c, float w)
            {
                SetDotBorderWidth(w);
                var sc = new StyleColor(c);
                _dot.style.borderLeftColor = sc;
                _dot.style.borderRightColor = sc;
                _dot.style.borderTopColor = sc;
                _dot.style.borderBottomColor = sc;
            }

            private void SetDotBorderWidth(float w)
            {
                _dot.style.borderLeftWidth = w;
                _dot.style.borderRightWidth = w;
                _dot.style.borderTopWidth = w;
                _dot.style.borderBottomWidth = w;
            }
        }
    }
}