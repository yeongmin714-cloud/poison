// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 618
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit — 월드 이름표/체력바 오버레이 (병사 / 몬스터 / NPC 이름표).
    /// 원본 IMGUI 3종(GuardHeadUI / MonsterHeadUI / NameplateDisplay)의 데이터 소스·표시 규칙을
    /// 그대로 계승하고, 체력바는 새로 "입체(음각 트로프 + 세로 그라디언트 채움)" 스타일로 구현.
    ///
    /// [데이터 계승 요약]
    ///  - 뱡사(GuardPlaceholder): 앵커 transform.position + up*2.2f, IsAlive / CurrentHP / MaxHP /
    ///    GuardName / Level, 카메라 뒤(z<=0) 스킵. 채움색: ≥0.6 녹 / 0.3~0.6 노랑 / &lt;0.3 빨강 규약 →
    ///    신규 입체바는 병사=청록·초록 그라디언트, HP&lt;30%시 주황·적 경고 전환로 계승 보강.
    ///  - 몬스터(AnimalAI): 앵커 bounds.extents.y*1.35(최소 1.2), dist&gt;40m·뒷면(vp.z&lt;0)·화면 밖
    ///    마진 40px 스킵. 표시명 MonsterDatabase.displayName, 레벨 MonsterLevelManager.GetLevelDisplay
    ///    ("Lv." 이후), 티어색 EstimateTierByName(녹/노랑/주황/빨강). HP 바 90x8 + cur/max.
    ///  - NPC(NameplateDisplay): 앵커 up*2.0, 카메라 뒤 스킵, 플레이어 3m 이내일 때만 표시(이름만).
    ///    [102차] 영주(InvadedLordEncounter)가 NameplateDisplay 부모에 붙어 있으면 체력바 슬롯으로
    ///    승격 — 병사 그라디언트(_gradGuard/_gradGuardWarn) 계승 + 고스트 트레일 동일 계약.
    ///
    /// [배치] UIToolkitBootstrap.Ensure → UIRoot의 "먼저"(index 0)에 부착되어 모든 창보다 아래 깔린다
    /// (첫 자식이 맨 아래). pickingMode=Ignore 전체로 게임 월드 클릭 통과.
    ///
    /// [성능] 유닛 수집 0.3s 스로틀 FindObjectsByType, 유닛 instanceID→풀 슬롯 고정 매핑(순서 뒤섞임 방지),
    /// 초기 풀 40 / 부족 시 10씩 / 상한 80. UIElement 처리는 Update 전체 속도, 프레임 GC 최소를 위해
    /// 스크래치 리스트 재사용.
    /// </summary>
    public class NameplateOverlayUTK : MonoBehaviour
    {
        // ═══════════════ 싱글턴 / 부트스트랩 ═══════════════
        private static NameplateOverlayUTK _instance;
        private static bool _spawnRequested;

        /// <summary>멱등 부트스트랩 — UIToolkitBootstrap.Ensure가 UIRoot 생성 직후 호출.</summary>
        public static void Ensure()
        {
            if (_instance != null || _spawnRequested) return;
            _spawnRequested = true;
            var go = new GameObject("NameplateOverlayUTK");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<NameplateOverlayUTK>();
        }

        // ═══════════════ 설정 상수 ═══════════════
        private const int    PoolInitial       = 40;    // 초기 풀
        private const int    PoolGrowStep      = 10;    // 부족 시 증분
        private const int    PoolMax           = 80;    // 상한
        private const float  ScanInterval      = 0.30f; // 유닛 수집 주기(s)
        private const float  CamRefreshInterval = 0.5f; // Camera.main 재조회 주기
        private const float  HeadRefreshInterval = 2f;  // 몬스터 bounds 재계산 주기
        private const float  ScreenMargin      = 40f;   // 화면 밖 스킵 마진(px)
        private const float  MonsterMaxDist    = 40f;   // 몬스터 최대 표시거리(m)

        // 이름/바 스펙
        private const float  SlotW             = 120f;  // 슬롯 컨테이너 폭(중앙정렬 기준)
        private const float  NameH             = 18f;   // 이름 라벨 높이
        private const float  LvH               = 16f;   // 몬스터 Lv 라벨 높이
        private const float  BarW              = 78f;   // Figma 101:5 gauge-container 진실치 78 — [80차] PlateW 통일 계약 유지([144차 P8-NP #2] 기록)
        private const float  BarH              = 9.6f;
        // [80차] 이름표 2행(이름 행/바 행) 폭 통일 — "두 줄 크기가 안맞아 어색" 해소(2026-10-15 HUD 영상 실측:
        // 병사 이름 행 114px vs 바 78px로 좌우 돌출). 두 행을 PlateW로 강제하고, 몬스터 배지(20+마진4)만큼
        // 이름 라벨을 PlateW-NameBadgeReserve로 채워 행 외곽선이 정확히 일치하게 한다.
        private const float  PlateW            = 96f;
        private const float  NameBadgeReserve  = 24f;
        private const float  NpcH              = 26f;   // NPC 이름표 높이

        // 라벨 배경 (다크 갈색 아크릴 반투명) — 요구 rgba(30,24,18,0.82)
        // [F-UI Phase1] GitHub-dark 팔레트 — Figma reference-monster-health-bar(어두운 트로프+적색 채움)
        private static readonly Color LabelBgColor   = new Color(0.086f, 0.114f, 0.133f, 0.92f); // #161B22 패널
        private static readonly Color GoldRing       = new Color(46 / 255f, 52 / 255f, 61 / 255f, 1f); // #2E343D 슬레이트 스트로크
        // [144차 P8-NP] 트로프 #1F242C — 101:5 gauge-container 기준. 판별: 코드 HP바 경로(barWrap+trough)는
        // 101:5 monster-health-bar 단독 바 구조(루트+게이지 컨테이너)이며 병사/영주/NPC/몬스터가 공유한다.
        // 110:14 참조판의 hp-bar-track(#451212, 98.4×6 r1.2)은 별도 노드 — 코드에 독립 트랙 없음 → 101:5 기준 적용.
        private static readonly Color TroughBg       = new Color(0x1F / 255f, 0x24 / 255f, 0x2C / 255f, 1f); // #1F242C 트로프(HP바 트랙)
        private static readonly Color BarGlassBg     = new Color(0x16 / 255f, 0x1B / 255f, 0x22 / 255f, 0.702f); // #161B22@0.702 — [144차 P8-NP] 101:5 루트 글래스 배경(BACKGROUND_BLUR:24 UTK 미지원 — 반투명 근사)
        private static readonly Color BarRootStroke  = new Color(0x30 / 255f, 0x36 / 255f, 0x3D / 255f, 0.502f); // #30363D@0.502 — [144차 P8-NP] 101:5 루트 스트로크
        private static readonly Color GaugeStroke    = new Color(0x4F / 255f, 0x59 / 255f, 0x64 / 255f, 1f);     // #4F5964 — [144차 P8-NP] 101:27 gauge-container 스트로크
        private static readonly Color StatPanelStroke = new Color(0x7C / 255f, 0xE2 / 255f, 0xFE / 255f, 0.4f);  // #7CE2FE@0.4 — [144차 P8-NP] 110:143 floating-stats-panel 스트로크
        // [144차 P8-NP] 베벨 하이라이트 — 101:27 스트로크 #4F5964 1.2로 대체됨(참조 잔존)
        private static readonly Color BevelTopHi     = new Color(1f, 1f, 1f, 0.16f);          // 상좌 밝은 하이라이트
        private static readonly Color BevelBottomLo  = new Color(0f, 0f, 0f, 0.45f);          // 하우 어두운 베벨

        // [고스트 체력바 78차] 뒷줄(타격 흔적) 색 — 따뜻한 주황(피격 흔적 가독 — 병사 초록/몬스터 빨강 양쪽 대비)
        private static readonly Color GhostTrailColor = new Color(1f, 0.72f, 0.35f, 1f);

        // 세로 그라디언트 텍스처 해상도 (4x16, 스트레치로 입체 광택)
        private const int    GradW = 4;
        private const int    GradH = 16;

        // 티어색 (MonsterHeadUI 팔레트 계승)
        private static readonly Color TierBeginner     = new Color(0.30f, 0.78f, 0.35f);
        private static readonly Color TierIntermediate = new Color(0.95f, 0.85f, 0.25f);
        private static readonly Color TierAdvanced     = new Color(0.95f, 0.55f, 0.20f);
        private static readonly Color TierBoss         = new Color(0.85f, 0.20f, 0.20f);

        // ═══════════════ 공유 그라디언트 텍스처 (지연 1회 생성) ═══════════════
        private static Texture2D _gradGuard;      // 병사 정상 — 청록·초록 (상밋→하진)
        private static Texture2D _gradGuardWarn;  // 병사 경고 — 주황·적 (HP<30%)

        // ═══════════════ 필드 ═══════════════
        private VisualElement _overlayRoot;   // 풀스크린 절대 오버레이 루트
        private float _rootW;
        private float _rootH;

        private Camera _cam;
        private float _camNextRefresh;

        private readonly List<Slot> _pool = new List<Slot>(PoolInitial);
        private readonly List<Slot> _allSlots = new List<Slot>(PoolInitial);
        private readonly Dictionary<int, Slot> _slotByUnit = new Dictionary<int, Slot>(PoolInitial);
        private readonly List<int> _removeScratch = new List<int>(16);   // 다이너서리 정리용 재사용

        private float _scanNext;

        private Transform _playerRoot;
        private float _playerNextRefresh;

        private MonsterLevelManager _levelMgr;

        // ═══════════════ 유닛 유형 ═══════════════
        private enum Kind { Guard, Monster, Npc, Duelist }   // [125차 후속25 R5] Duelist — 결투선수(병사형 이름표 계약)

        // ═══════════════ 풀 슬롯 ═══════════════
        private sealed class Slot
        {
            public int unitId;
            public bool used;
            public Kind kind;
            public Transform transform;
            public float headOffset = 2.0f;
            public float headHeight = 1.2f;
            public float headNextRefresh;

            // 데이터 참조
            public GuardPlaceholder guard;
            public AnimalAI monster;
            public NameplateDisplay npc;
            public InvadedLordEncounter lord;   // [102차] NPC 슬롯 승격 — 침략 영주 참조(병사와 동일 HP바)
            public ArenaDuelFighterTag duelist; // [125차 후속25 R5] 결투선수 — 병사형 이름표(이름행+HP바) 동일 계약

            // UI 요소
            public VisualElement root;
            public Label nameLabel;
            public Label lvLabel;
            public VisualElement barWrap;
            public VisualElement trough;
            public VisualElement fill;
            public Label ratioLabel;

            // [고스트 체력바 78차] 뒷줄(타격 흔적) — 풀 재사용 시 owner 비교로 리셋
            public VisualElement trailFill;
            public GhostHpDisplay ghost = new GhostHpDisplay();
            public Object ghostOwner;

            // [F5] 몬스터 헤드 — reference-monster-health-bar 구조 (level-badge + 이름 인라인 + 플로팅 스탯)
            public VisualElement nameRow;     // [배지 + 이름] 가로
            public Label levelBadge;          // 티어색 배경 + 흰 레벨 숫자 (110:135 — 15.6×19.2 [144차 P8-NP])
            public VisualElement statPanel;   // floating-stats 3행 수직 (#10151C@0.22 — HP/공격/의약 [144차 P8-NP #9])
            public Label hpStatVal;
            public Label atkStatVal;
            public Label spdStatVal;          // [144차 P8-NP] 3행=의약(medicine) — 실데이터 부재 '—' 표기(필드 키 보존)

            public float estHeight; // 종류별 추정 높이(위치 보정)

            public void Reset()
            {
                unitId = 0;
                used = false;
                guard = null;
                monster = null;
                npc = null;
                lord = null;                    // [102차] 풀 재사용 오염 방지(80차 패턴)
                duelist = null;                 // [125차 후속25 R5] 결투선수 슬롯 재사용 오염 방지
                transform = null;
                ghostOwner = null;              // [고스트 체력바] 재사용 시 다음 바인드에서 리셋 유도
                if (root != null) root.style.display = DisplayStyle.None;
            }
        }

        // ═══════════════ Unity 생명주기 ═══════════════
        private void OnEnable()
        {
            _instance = this;
            EnsureGradientTextures();
        }

        private void Update()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;                       // UIRoot 미준비 — 조용히 스킵

            if (_overlayRoot == null || _overlayRoot.panel == null)
                AttachToRoot(root);
            if (_overlayRoot == null) return;

            RefreshCamera();
            if (_cam == null) return;                       // 카메라 없음 — 조용히 스킵

            // [U8 수리] 창 열리면 오버레이 전체 숨김 (게이트 유지)
            if (ProjectName.Core.UITransitionState.AnyWindowOpen)
            {
                if (_overlayRoot.style.display != DisplayStyle.None)
                    _overlayRoot.style.display = DisplayStyle.None;
                return;
            }
            if (_overlayRoot.style.display != DisplayStyle.Flex)
                _overlayRoot.style.display = DisplayStyle.Flex;

            _rootW = _overlayRoot.worldBound.width;
            _rootH = _overlayRoot.worldBound.height;
            if (_rootW <= 0f || _rootH <= 0f) return;

            RefreshLevelMgr();
            ScanUnitsThrottled();

            // 활성 슬롯 위치/데이터 갱신
            foreach (var slot in _allSlots)
            {
                if (!slot.used) continue;
                RefreshSlot(slot);
            }
        }

        // ═══════════════ 루트 부착 ═══════════════
        private void AttachToRoot(VisualElement root)
        {
            _overlayRoot = new VisualElement();
            _overlayRoot.name = "NameplateOverlay";
            _overlayRoot.style.position = Position.Absolute;
            _overlayRoot.style.left = 0f; _overlayRoot.style.right = 0f;
            _overlayRoot.style.top = 0f;  _overlayRoot.style.bottom = 0f;
            _overlayRoot.pickingMode = PickingMode.Ignore;  // 클릭 통과 필수
            // 먼저(index 0) 부착 → 모든 창보다 아래 깔려 이름표가 창에 겹치지 않도록.
            root.Insert(0, _overlayRoot);
        }

        // ═══════════════ 카메라 / 레벨 / 플레이어 ═══════════════
        private void RefreshCamera()
        {
            if (_cam != null && Time.unscaledTime < _camNextRefresh) return;
            _cam = Camera.main;
            _camNextRefresh = Time.unscaledTime + CamRefreshInterval;
        }

        private void RefreshLevelMgr()
        {
            if (_levelMgr == null)
                _levelMgr = MonsterLevelManager.Instance;   // getter가 부재 시 자동 생성
        }

        private void RefreshPlayer()
        {
            if (_playerRoot != null && Time.unscaledTime < _playerNextRefresh) return;
            var go = GameObject.FindGameObjectWithTag("Player");
            _playerRoot = go != null ? go.transform : null;
            _playerNextRefresh = Time.unscaledTime + 0.5f;
        }

        // ═══════════════ 유닛 수집 (0.3s 스로틀) ═══════════════
        private void ScanUnitsThrottled()
        {
            if (Time.unscaledTime < _scanNext) return;
            _scanNext = Time.unscaledTime + ScanInterval;

            // ── 병사 ──
            var guards = Object.FindObjectsByType<GuardPlaceholder>(FindObjectsSortMode.None);
            foreach (var g in guards)
            {
                if (g == null || !g.IsAlive) continue;   // 사망 스킵 (GuardHeadUI 규칙 계승)
                Slot slot = AcquireOrGetSlot(g.GetInstanceID(), g);
                if (slot != null) BindGuard(slot, g);
            }

            // ── 몬스터 ──
            var monsters = Object.FindObjectsByType<AnimalAI>(FindObjectsSortMode.None);
            foreach (var m in monsters)
            {
                if (m == null || !m.IsAlive) continue;   // 사망 스킵 (MonsterHeadUI 규칙 계승)
                Slot slot = AcquireOrGetSlot(m.GetInstanceID(), m);
                if (slot != null) BindMonster(slot, m);
            }

            // ── NPC ──
            var npcs = Object.FindObjectsByType<NameplateDisplay>(FindObjectsSortMode.None);
            foreach (var n in npcs)
            {
                if (n == null) continue;
                Slot slot = AcquireOrGetSlot(n.transform.GetInstanceID(), n);
                if (slot != null) BindNpc(slot, n);
            }

            // [125차 후속25 R5] 결투선수 — 기존 병사형 이름표(이름행+HP바)에 선수 이름 바인딩(사용자 계약).
            //   커스텀 TextMesh/마커 폐기 — 기존 오버레이 경로만 사용.
            var duelists = Object.FindObjectsByType<ArenaDuelFighterTag>(FindObjectsSortMode.None);
            foreach (var f in duelists)
            {
                if (f == null || !f.IsAlive) continue;
                Slot slot = AcquireOrGetSlot(f.GetInstanceID(), f);
                if (slot != null) BindDuelist(slot, f);
            }

            // ── 미할당(사망/파괴/해제) 슬롯 회수 ──
            _removeScratch.Clear();
            foreach (var kv in _slotByUnit)
            {
                if (kv.Value != null && !kv.Value.used)
                    _removeScratch.Add(kv.Key);
            }
            foreach (int id in _removeScratch)
            {
                if (_slotByUnit.TryGetValue(id, out Slot slot))
                {
                    _slotByUnit.Remove(id);
                    slot.Reset();
                    _pool.Add(slot);
                    if (_allSlots.Remove(slot))
                    {
                        if (slot.root != null) slot.root.RemoveFromHierarchy();
                    }
                }
            }
        }

        /// <summary>instanceID 기준 풀 슬롯 확보(고정 매핑). 없으면 새/여유 슬롯 반환. null이면 null.</summary>
        private Slot AcquireOrGetSlot(int unitId, Component comp)
        {
            if (_slotByUnit.TryGetValue(unitId, out Slot existing))
            {
                if (existing.used) return existing;
                _slotByUnit.Remove(unitId);  // 부실 매핑 재취득
            }

            Slot slot = PopFreeSlot();
            if (slot == null) return null;   // 풀 상한 도달
            _slotByUnit[unitId] = slot;
            slot.unitId = unitId;
            slot.transform = comp.transform;
            slot.used = true;
            return slot;
        }

        private Slot PopFreeSlot()
        {
            // 여유 슬롯 우선
            for (int i = 0; i < _pool.Count; i++)
            {
                Slot free = _pool[i];
                if (!free.used) { _pool.RemoveAt(i); EnsureElements(free); _allSlots.Add(free); return free; }
            }
            // 부족 시 증분 (상한 초과 시 null)
            if (_allSlots.Count >= PoolMax) return null;
            int grow = Mathf.Min(PoolGrowStep, PoolMax - _allSlots.Count);
            for (int i = 0; i < grow; i++)
            {
                var s = new Slot();
                _pool.Add(s);
            }
            Slot got = _pool[0];
            _pool.RemoveAt(0);
            EnsureElements(got);
            _allSlots.Add(got);
            return got;
        }

        // ═══════════════ 바인딩 ═══════════════
        private void BindGuard(Slot slot, GuardPlaceholder g)
        {
            slot.kind = Kind.Guard;
            slot.guard = g;
            slot.monster = null;
            slot.npc = null;
            slot.lord = null;                  // [102차] 풀 재사용 오염 방지
            slot.duelist = null;               // [125차 후속25 R5] 슬롯 재사용 오염 방지
            slot.headOffset = 2.2f;            // GuardHeadUI 앵커 계승
            slot.estHeight = NameH + BarH + 4f;
            slot.nameRow.style.width = PlateW;                 // [80차] 두 행 폭 통일(풀 재사용 오염 방지 재설정)
            slot.nameLabel.style.width = PlateW;               // [80차] 배지 없는 병사 — 라벨이 행 전체를 채움
            slot.lvLabel.style.display = DisplayStyle.None;
            slot.levelBadge.style.display = DisplayStyle.None;   // [F5] 배지 — 병사 미표시
            slot.statPanel.style.display = DisplayStyle.None;    // [F5] 스탯 — 병사 미표시
            slot.barWrap.style.display = DisplayStyle.Flex;
        }

        private void BindMonster(Slot slot, AnimalAI m)
        {
            slot.kind = Kind.Monster;
            slot.monster = m;
            slot.guard = null;
            slot.npc = null;
            slot.lord = null;                  // [102차] 풀 재사용 오염 방지
            slot.duelist = null;               // [125차 후속25 R5] 슬롯 재사용 오염 방지
            // [F5] 배지(18) + 이름(18) 행 + 바(10+여백) + 스탯패널(~26)
            slot.estHeight = NameH + BarH + 4f + 60f + 8f;   // [144차 P8-NP #9] 스탯 패널 3행 수직화 → 여유 30→60
            slot.lvLabel.style.display = DisplayStyle.None;        // [F5] 배지로 대체
            slot.levelBadge.style.display = DisplayStyle.Flex;     // [F5] 티어색 배지
            slot.statPanel.style.display = DisplayStyle.Flex;      // [F5] floating-stats
            slot.barWrap.style.display = DisplayStyle.Flex;
            // bounds 기반 앵커 (MonsterHeadUI 계승)
            if (Time.unscaledTime >= slot.headNextRefresh)
            {
                slot.headHeight = ComputeHeadHeight(m.transform);
                slot.headNextRefresh = Time.unscaledTime + HeadRefreshInterval;
            }
        }

        private void BindDuelist(Slot slot, ArenaDuelFighterTag f)
        {
            // [125차 후속25 R5] 병사형 이름표 계약(BindGuard 동일 레이아웃) — 이름행+HP바, 배지/스탯 없음
            slot.kind = Kind.Duelist;
            slot.duelist = f;
            slot.guard = null;
            slot.monster = null;
            slot.npc = null;
            slot.lord = null;
            slot.headOffset = 2.2f;            // 병사 앵커 계승
            slot.estHeight = NameH + BarH + 4f;
            slot.nameRow.style.width = PlateW;
            slot.nameLabel.style.width = PlateW;
            slot.lvLabel.style.display = DisplayStyle.None;
            slot.levelBadge.style.display = DisplayStyle.None;
            slot.statPanel.style.display = DisplayStyle.None;
            slot.barWrap.style.display = DisplayStyle.Flex;
        }

        private void BindNpc(Slot slot, NameplateDisplay n)
        {
            slot.kind = Kind.Npc;
            slot.npc = n;
            slot.guard = null;
            slot.monster = null;
            slot.duelist = null;               // [125차 후속25 R5] 슬롯 재사용 오염 방지
            // [102차] 영주 판정 — NameplateDisplay 부모 체인의 InvadedLordEncounter. 있으면
            // 병사와 동일하게 이름+체력바 2행 슬롯으로 승격(풀 재사용 오염 방지 — 매 바인드 재판정).
            slot.lord = n != null ? n.GetComponentInParent<InvadedLordEncounter>() : null;
            slot.headOffset = 2.0f;            // NameplateDisplay 앵커 계승
            slot.estHeight = slot.lord != null ? NameH + BarH + 4f : NpcH;   // [102차] 영주=병사 2행 높이
            slot.nameRow.style.width = PlateW;                 // [80차] NPC 이름 pill도 동일 폭(시각 일관성)
            slot.nameLabel.style.width = PlateW;
            slot.lvLabel.style.display = DisplayStyle.None;
            slot.levelBadge.style.display = DisplayStyle.None;   // [F5]
            slot.statPanel.style.display = DisplayStyle.None;    // [F5]
            slot.barWrap.style.display = slot.lord != null
                ? DisplayStyle.Flex                              // [102차] 영주 — 체력바 표시
                : DisplayStyle.None;
        }

        // ═══════════════ 프레임 갱신 ═══════════════
        private void RefreshSlot(Slot slot)
        {
            // 사망/파괴 유닛 — [80차] 즉시 숨김 수리: used=false만 두면 루트가 마지막 위치에 DisplayStyle.Flex로
            // 남아 다음 스캔 회수(최대 0.3s)까지 이름표가 빈 공중에 부유했다(HUD 영상 00:50~ 실측).
            if (!SlotAlive(slot)) { slot.used = false; Hide(slot); return; }

            switch (slot.kind)
            {
                case Kind.Guard:   RefreshGuard(slot); break;
                case Kind.Monster: RefreshMonster(slot); break;
                case Kind.Npc:     RefreshNpc(slot); break;
                case Kind.Duelist: RefreshDuelist(slot); break;   // [125차 후속25 R5]
            }
        }

        private bool SlotAlive(Slot slot)
        {
            if (slot.transform == null) return false;
            switch (slot.kind)
            {
                case Kind.Guard:   return slot.guard != null && slot.guard.IsAlive;
                case Kind.Monster: return slot.monster != null && slot.monster.IsAlive;
                case Kind.Npc:     return slot.npc != null;
                case Kind.Duelist: return slot.duelist != null && slot.duelist.IsAlive;   // [125차 후속25 R5]
                default: return false;
            }
        }

        private void RefreshDuelist(Slot slot)
        {
            // [125차 후속25 R5] 결투선수 — 병사형 그라디언트/고스트 트레일 계약 완전 재사용(신규 UI 없음)
            ArenaDuelFighterTag f = slot.duelist;
            if (f == null) return;

            float ratio = f.HpFraction;
            float front = TickGhost(slot, f, ratio);
            slot.nameLabel.text = f.DisplayName;
            SetFillWidth(slot, front);
            SetTrailWidth(slot, slot.ghost.Trail);
            slot.fill.style.backgroundImage = ratio < 0.3f
                ? UTKTextureSafe.ToBackground(_gradGuardWarn)
                : UTKTextureSafe.ToBackground(_gradGuard);
            slot.ratioLabel.text = Mathf.RoundToInt(ratio * 100f).ToString() + "%";

            PlaceSlot(slot);
        }

        // ─── 뱡사 ───
        private void RefreshGuard(Slot slot)
        {
            GuardPlaceholder g = slot.guard;
            if (g == null) return;

            float ratio = g.MaxHP > 0f ? Mathf.Clamp01(g.CurrentHP / g.MaxHP) : 0f;
            float front = TickGhost(slot, g, ratio);          // [고스트 체력바] 앞줄=실제 즉시, 뒷줄 추격
            slot.nameLabel.text = g.GuardName + " Lv." + g.Level.ToString();
            SetFillWidth(slot, front);
            SetTrailWidth(slot, slot.ghost.Trail);
            // 경고 전환: HP<30% → 주황·적 / 그 외 청록·초록 (몬스터 제외 뱡사만)
            slot.fill.style.backgroundImage = ratio < 0.3f
                ? UTKTextureSafe.ToBackground(_gradGuardWarn)
                : UTKTextureSafe.ToBackground(_gradGuard);
            slot.ratioLabel.text = Mathf.RoundToInt(ratio * 100f).ToString() + "%";

            PlaceSlot(slot);
        }

        // ─── 몬스터 ───
        private void RefreshMonster(Slot slot)
        {
            AnimalAI m = slot.monster;
            if (m == null) return;

            string displayName = ResolveDisplayName(m);
            Color tierColor = ResolveTierColor(displayName);

            slot.nameLabel.text = displayName;
            // [80차] 이름 행 폭 = 바 행 폭 통일 — 배지(20+마진4)를 제외한 라벨이 행 나머지를 채워
            // 행 외곽이 96px로 바와 정확히 일치(구: 라벨 auto 폭 → 이름 길이마다 행 폭 들쭉날쭉).
            slot.nameRow.style.width = PlateW;
            slot.nameLabel.style.width = PlateW - NameBadgeReserve;
            // [F5] 레벨 배지 — 티어색 배경 + 흰 숫자 (reference level-badge)
            slot.levelBadge.text = m.Level.ToString();
            slot.levelBadge.style.backgroundColor = new StyleColor(tierColor);

            float ratio = m.MaxHP > 0f ? Mathf.Clamp01(m.CurrentHP / m.MaxHP) : 0f;
            float front = TickGhost(slot, m, ratio);          // [고스트 체력바] 앞줄=실제 즉시, 뒷줄 추격
            SetFillWidth(slot, front);
            SetTrailWidth(slot, slot.ghost.Trail);
            // [무번호 179] 몬스터 체력바 = 병사 체력바 동일 계약 — 청록·초록 그라디언트 + HP<30% 주황·적 경고
            //   (구: [F5] 플랫 #E53E3E 적색 단일 — 경고 전환 없음. 사용자 지시 "몬스터 체력바도 그냥 병사 체력바와 동일")
            slot.fill.style.backgroundImage = ratio < 0.3f
                ? UTKTextureSafe.ToBackground(_gradGuardWarn)
                : UTKTextureSafe.ToBackground(_gradGuard);
            slot.ratioLabel.text = Mathf.RoundToInt(ratio * 100f).ToString() + "%";

            // [F5] floating-stats — 3행: HP / 공격 / 의약 [144차 P8-NP #9 P0]
            if (slot.hpStatVal != null)
                slot.hpStatVal.text = m.CurrentHP < 0 ? "0" : Mathf.RoundToInt(m.CurrentHP).ToString();
            if (slot.atkStatVal != null)
            {
                var def = MonsterDatabase.Get(m.MonsterId);
                slot.atkStatVal.text = def != null ? def.baseDamage.ToString() : "?";
            }
            if (slot.spdStatVal != null)
                slot.spdStatVal.text = "—";   // [144차 P8-NP #9] 3행 라벨=의약(medicine) — 게임 데이터에 의약 실데이터 부재 → '—' 표기(기록). 구 CurrentSpeed 피드 제거(데이터 키·필드는 무손상 보존)

            PlaceSlot(slot);
        }

        // ─── NPC (플레이어 근접 시만) ───
        private void RefreshNpc(Slot slot)
        {
            NameplateDisplay n = slot.npc;
            if (n == null) return;

            // [102차] 영주 슬롯 — 종결/사망 시 즉시 숨김(SlotAlive의 Npc 경로는 npc 파괴만 보므로
            // RefreshNpc 내부에서 먼저 처리해 RefreshSlot 충돌 없이 정리).
            InvadedLordEncounter lord = slot.lord;
            if (lord != null)
            {
                if (lord.IsDead || lord.State == InvadedLordEncounter.EncounterState.Resolved)
                { slot.used = false; Hide(slot); return; }

                RefreshPlayer();   // 0.5s 캐시
                bool lordNearby = false;
                if (_playerRoot != null)
                {
                    float dSqr = (n.transform.position - _playerRoot.position).sqrMagnitude;
                    lordNearby = dSqr <= n.InteractRange * n.InteractRange;
                }
                if (!lordNearby) { Hide(slot); return; }

                slot.nameLabel.text = n.DisplayName;            // "👑 세드릭 경" 그대로
                float lordRatio = lord.MaxHP > 0f ? Mathf.Clamp01(lord.CurrentHP / lord.MaxHP) : 0f;
                float lordFront = TickGhost(slot, lord, lordRatio);   // [고스트 체력바] 앞줄=실제 즉시, 뒷줄 추격
                SetFillWidth(slot, lordFront);
                SetTrailWidth(slot, slot.ghost.Trail);
                // [102차] 영주=병사 그라디언트 계승(_gradGuard/_gradGuardWarn — EnsureElements의 fill 배경과 공용)
                slot.fill.style.backgroundImage = lordRatio < 0.3f
                    ? UTKTextureSafe.ToBackground(_gradGuardWarn)
                    : UTKTextureSafe.ToBackground(_gradGuard);
                slot.ratioLabel.text = Mathf.RoundToInt(lordRatio * 100f).ToString() + "%";

                PlaceSlot(slot);
                return;
            }

            RefreshPlayer();   // 0.5s 캐시
            bool nearby = false;
            if (_playerRoot != null)
            {
                // NameplateDisplay 상호작용 범위 계승 (기본 3m)
                float range = n.InteractRange;
                float distSqr = (n.transform.position - _playerRoot.position).sqrMagnitude;
                nearby = distSqr <= range * range;
            }
            if (!nearby) { Hide(slot); return; }

            slot.nameLabel.text = n.DisplayName;
            PlaceSlot(slot);
        }

        // ═══════════════ 위치 투영 + cull ═══════════════
        private void PlaceSlot(Slot slot)
        {
            Vector3 anchor = slot.transform.position + Vector3.up
                * (slot.kind == Kind.Monster ? slot.headHeight : slot.headOffset);

            Vector3 vp = _cam.WorldToViewportPoint(anchor);
            if (vp.z <= 0f) { Hide(slot); return; }              // 카메라 뒤

            // 몬스터 거리 스킵 (MonsterHeadUI 40m 계승)
            if (slot.kind == Kind.Monster)
            {
                float dist = Vector3.Distance(_cam.transform.position, anchor);
                if (dist > MonsterMaxDist) { Hide(slot); return; }
            }

            float x = vp.x * _rootW;
            float y = (1f - vp.y) * _rootH;

            // 화면 밖 마진 스킵
            if (x < -ScreenMargin || x > _rootW + ScreenMargin ||
                y < -ScreenMargin || y > _rootH + ScreenMargin) { Hide(slot); return; }

            slot.root.style.display = DisplayStyle.Flex;
            slot.root.style.left = x - SlotW * 0.5f;
            slot.root.style.top = y - slot.estHeight;
        }

        private void SetFillWidth(Slot slot, float ratio)
        {
            float pct = Mathf.Clamp01(ratio) * 100f;
            slot.fill.style.width = new Length(pct, LengthUnit.Percent);
        }

        /// <summary>[고스트 체력바 78차] 뒷줄(타격 흔적) 폭 — 앞줄보다 클 때만 보인다(오른쪽 갭).</summary>
        private void SetTrailWidth(Slot slot, float ratio)
        {
            if (slot.trailFill == null) return;
            float pct = Mathf.Clamp01(ratio) * 100f;
            slot.trailFill.style.width = new Length(pct, LengthUnit.Percent);
        }

        /// <summary>[고스트 체력바 78차] 슬롯 틱 — 유닛 교체(풀 재사용) 리셋 + 앞줄 즉시/뒷줄 추격. 실제 비율 반환.</summary>
        private float TickGhost(Slot slot, Object owner, float actualRatio)
        {
            if (!ReferenceEquals(slot.ghostOwner, owner))
            {
                slot.ghostOwner = owner;
                slot.ghost.Reset(actualRatio);
            }
            slot.ghost.Tick(actualRatio, Time.deltaTime);
            return slot.ghost.Front;
        }

        private void Hide(Slot slot)
        {
            if (slot.root != null && slot.root.style.display != DisplayStyle.None)
                slot.root.style.display = DisplayStyle.None;
        }

        // ═══════════════ 표시명 / 레벨 / 티어 헬퍼 (MonsterHeadUI 계승) ═══════════════
        private static string ResolveDisplayName(AnimalAI ai)
        {
            string id = ai != null ? ai.MonsterId : null;
            if (string.IsNullOrEmpty(id)) return "?";
            MonsterDef def = MonsterDatabase.Get(id);
            if (def != null && !string.IsNullOrEmpty(def.displayName)) return def.displayName;
            return id;
        }

        private string ResolveLevelText(AnimalAI ai)
        {
            if (_levelMgr == null || ai == null) return "Lv.?";
            string raw = _levelMgr.GetLevelDisplay(ai.Level);   // "🟢 Lv.5"
            int idx = raw.IndexOf("Lv.", System.StringComparison.Ordinal);
            return idx >= 0 ? raw.Substring(idx) : raw;
        }

        private Color ResolveTierColor(string displayName)
        {
            MonsterTier tier = _levelMgr != null
                ? _levelMgr.EstimateTierByName(displayName)
                : MonsterTier.Beginner;
            switch (tier)
            {
                case MonsterTier.Beginner:     return TierBeginner;
                case MonsterTier.Intermediate: return TierIntermediate;
                case MonsterTier.Advanced:     return TierAdvanced;
                default:                       return TierBoss;
            }
        }

        /// <summary>몬스터 머리 높이 — Renderer bounds.extents.y * 1.35 (최소 1.2m), MonsterHeadUI 계승.</summary>
        private static float ComputeHeadHeight(Transform t)
        {
            Renderer[] renderers = t.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return 1.2f;
            try
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    if (renderers[i] != null) b.Encapsulate(renderers[i].bounds);
                return Mathf.Max(b.extents.y * 1.35f, 1.2f);
            }
            catch (System.Exception)
            {
                return 1.2f;
            }
        }

        // ═══════════════ 슬롯 UI 구성 ═══════════════
        private void EnsureElements(Slot slot)
        {
            if (slot.root != null)
            {
                // 풀 회수 후 재사용 시 계층 재부착 (Release에서 RemoveFromHierarchy 했으므로)
                if (_overlayRoot != null && slot.root.parent == null)
                    _overlayRoot.Add(slot.root);
                return;
            }

            slot.root = new VisualElement();
            slot.root.name = "NameplateSlot";
            slot.root.style.position = Position.Absolute;
            slot.root.style.width = SlotW;
            slot.root.style.flexDirection = FlexDirection.Column;
            slot.root.style.alignItems = Align.Center;
            slot.root.pickingMode = PickingMode.Ignore;
            slot.root.style.display = DisplayStyle.None;
            _overlayRoot.Add(slot.root);

            // [F5] 이름 행 — 배지 + 이름 가로 (몬스터는 배지 표시, 병사/NPC는 배지 숨김)
            slot.nameRow = new VisualElement();
            slot.nameRow.style.flexDirection = FlexDirection.Row;
            slot.nameRow.style.alignItems = Align.Center;
            slot.nameRow.style.justifyContent = Justify.Center;
            slot.nameRow.pickingMode = PickingMode.Ignore;
            slot.root.Add(slot.nameRow);

            // 레벨 배지 — 티어색 배경 + 흰 숫자 (reference-monster-health-bar level-badge 110:135)
            // [144차 P8-NP] 15.6×19.2(구 20×18) + 스트로크 #FFFFFF@0.502 0.6 + 좌우 pad 4.8 신설, r3 유지
            slot.levelBadge = new Label();
            slot.levelBadge.style.width = 15.6f;
            slot.levelBadge.style.height = 19.2f;
            slot.levelBadge.style.fontSize = 12f;
            slot.levelBadge.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            slot.levelBadge.style.unityTextAlign = TextAnchor.MiddleCenter;
            slot.levelBadge.style.color = new StyleColor(Color.white);
            slot.levelBadge.style.borderTopLeftRadius = 3f; slot.levelBadge.style.borderTopRightRadius = 3f;
            slot.levelBadge.style.borderBottomLeftRadius = 3f; slot.levelBadge.style.borderBottomRightRadius = 3f;
            slot.levelBadge.style.borderTopWidth = 0.6f; slot.levelBadge.style.borderBottomWidth = 0.6f;
            slot.levelBadge.style.borderLeftWidth = 0.6f; slot.levelBadge.style.borderRightWidth = 0.6f;
            slot.levelBadge.style.borderTopColor = new StyleColor(new Color(1f, 1f, 1f, 0.502f));
            slot.levelBadge.style.borderBottomColor = new StyleColor(new Color(1f, 1f, 1f, 0.502f));
            slot.levelBadge.style.borderLeftColor = new StyleColor(new Color(1f, 1f, 1f, 0.502f));
            slot.levelBadge.style.borderRightColor = new StyleColor(new Color(1f, 1f, 1f, 0.502f));
            slot.levelBadge.style.paddingLeft = 4.8f; slot.levelBadge.style.paddingRight = 4.8f;
            slot.levelBadge.style.marginRight = 4f;
            slot.levelBadge.pickingMode = PickingMode.Ignore;
            slot.levelBadge.style.display = DisplayStyle.None;
            slot.nameRow.Add(slot.levelBadge);

            // 이름 라벨 — 다크 아크릴 배경 + 골드 1px + 라운드 4px
            slot.nameLabel = new Label();
            slot.nameLabel.style.backgroundColor = new StyleColor(LabelBgColor);
            slot.nameLabel.style.borderTopWidth = 1f; slot.nameLabel.style.borderBottomWidth = 1f;
            slot.nameLabel.style.borderLeftWidth = 1f; slot.nameLabel.style.borderRightWidth = 1f;
            slot.nameLabel.style.borderTopColor = new StyleColor(GoldRing);
            slot.nameLabel.style.borderBottomColor = new StyleColor(GoldRing);
            slot.nameLabel.style.borderLeftColor = new StyleColor(GoldRing);
            slot.nameLabel.style.borderRightColor = new StyleColor(GoldRing);
            slot.nameLabel.style.borderTopLeftRadius = 4f; slot.nameLabel.style.borderTopRightRadius = 4f;
            slot.nameLabel.style.borderBottomLeftRadius = 4f; slot.nameLabel.style.borderBottomRightRadius = 4f;
            slot.nameLabel.style.width = PlateW;   // [80차] 이름 행 폭 = 바 행 폭 통일
            slot.nameLabel.style.height = NameH;
            slot.nameLabel.style.fontSize = 13.2f;   // [144차 P8-NP] 110:136 monster-name 13.2(lineH 16 — UTK line-height 미지원, NameH=18로 근사)
            slot.nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            slot.nameLabel.style.color = new StyleColor(Color.white);
            slot.nameLabel.style.whiteSpace = WhiteSpace.NoWrap;
            slot.nameLabel.pickingMode = PickingMode.Ignore;
            slot.nameRow.Add(slot.nameLabel);

            // 몬스터 전용 Lv 라벨 (티어색)
            slot.lvLabel = new Label();
            slot.lvLabel.style.width = SlotW - 6f;
            slot.lvLabel.style.height = LvH;
            slot.lvLabel.style.fontSize = 12f;
            slot.lvLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            slot.lvLabel.style.color = new StyleColor(TierBeginner);
            slot.lvLabel.pickingMode = PickingMode.Ignore;
            slot.root.Add(slot.lvLabel);

            // ═══ 입체 HP바 ═══ (101:5 monster-health-bar 구조: 루트 barWrap + 게이지 컨테이너 trough)
            slot.barWrap = new VisualElement();
            slot.barWrap.name = "HpBarFrame";
            // [144차 P8-NP #2] Figma 진실치 78(101:5 gauge-container) — [80차] 두 행 폭 통일 계약 유지
            // (barWrap.width=PlateW=96 의도적 통일, 구 78 — 폭 계약은 유지+기록)
            slot.barWrap.style.width = PlateW;
            slot.barWrap.style.height = BarH;
            slot.barWrap.style.marginTop = 4f;
            slot.barWrap.style.marginBottom = 2f;
            slot.barWrap.style.flexShrink = 0f;
            slot.barWrap.pickingMode = PickingMode.Ignore;
            // [144차 P8-NP #4] 루트 글래스 배경 #161B22@0.702 (101:5 fills — BACKGROUND_BLUR:24 UTK 미지원, 반투명 근사)
            slot.barWrap.style.backgroundColor = new StyleColor(BarGlassBg);
            // [144차 P8-NP #5] 루트 스트로크 #30363D@0.502 0.6 (구 GoldRing #2E343D 1px)
            slot.barWrap.style.borderTopWidth = 0.6f; slot.barWrap.style.borderBottomWidth = 0.6f;
            slot.barWrap.style.borderLeftWidth = 0.6f; slot.barWrap.style.borderRightWidth = 0.6f;
            slot.barWrap.style.borderTopColor = new StyleColor(BarRootStroke);
            slot.barWrap.style.borderBottomColor = new StyleColor(BarRootStroke);
            slot.barWrap.style.borderLeftColor = new StyleColor(BarRootStroke);
            slot.barWrap.style.borderRightColor = new StyleColor(BarRootStroke);
            // [144차 P8-NP #3] 루트 코너 pill — 101:5/101:27 r1198.8 = pill(완전 원형 근사 BarH/2).
            // 110:14 track r1.2와 공존 — barWrap만 pill, 트로프(trough)는 r2 유지(아래).
            slot.barWrap.style.borderTopLeftRadius = BarH * 0.5f; slot.barWrap.style.borderTopRightRadius = BarH * 0.5f;
            slot.barWrap.style.borderBottomLeftRadius = BarH * 0.5f; slot.barWrap.style.borderBottomRightRadius = BarH * 0.5f;
            slot.root.Add(slot.barWrap);

            // 게이지 컨테이너 트로프 — 101:27 gauge-container 대응
            // [144차 P8-NP #6] fill #1F242C(101:5 기준 — 판별 주석은 TroughBg 정의 참조).
            // [144차 P8-NP #10 기록] 110:14 hp-bar-track은 98.4×6 r1.2 vs 101:5 컨테이너 78×9.6 — 코드 바 경로는
            // 병사/영주/NPC/몬스터 공유 단일 트랙이라 [80차] 폭 통일 계약(PlateW) 유지, 98.4×6 별도 적용 안 함.
            // [144차 P8-NP #5] 컨테이너 스트로크 #4F5964 1.2 (구 베벨 하이라이트/음각 대체)
            slot.trough = new VisualElement();
            slot.trough.style.position = Position.Absolute;
            slot.trough.style.left = 1f; slot.trough.style.right = 1f;
            slot.trough.style.top = 1f;  slot.trough.style.bottom = 1f;
            slot.trough.style.overflow = Overflow.Hidden;
            slot.trough.style.backgroundColor = new StyleColor(TroughBg);
            slot.trough.style.borderTopWidth = 1.2f;  slot.trough.style.borderBottomWidth = 1.2f;
            slot.trough.style.borderLeftWidth = 1.2f; slot.trough.style.borderRightWidth = 1.2f;
            slot.trough.style.borderTopColor = new StyleColor(GaugeStroke);
            slot.trough.style.borderBottomColor = new StyleColor(GaugeStroke);
            slot.trough.style.borderLeftColor = new StyleColor(GaugeStroke);
            slot.trough.style.borderRightColor = new StyleColor(GaugeStroke);
            // 트랙 코너 r2 유지(루트 pill화와 공존 — [144차 P8-NP #3])
            slot.trough.style.borderTopLeftRadius = 2f; slot.trough.style.borderTopRightRadius = 2f;
            slot.trough.style.borderBottomLeftRadius = 2f; slot.trough.style.borderBottomRightRadius = 2f;
            slot.trough.pickingMode = PickingMode.Ignore;
            slot.barWrap.Add(slot.trough);

            // [고스트 체력바 78차] 뒷줄(타격 흔적) — 채움보다 먼저 추가(아래 깔림), 오른쪽 갭으로 가시
            slot.trailFill = new VisualElement();
            slot.trailFill.style.position = Position.Absolute;
            slot.trailFill.style.left = 0f; slot.trailFill.style.top = 0f; slot.trailFill.style.bottom = 0f;
            slot.trailFill.style.width = new Length(0f, LengthUnit.Percent);
            slot.trailFill.style.backgroundColor = new StyleColor(GhostTrailColor);
            slot.trailFill.pickingMode = PickingMode.Ignore;
            slot.trough.Add(slot.trailFill);

            // 채움 — 세로 그라디언트 (상밋→하진 광택)
            slot.fill = new VisualElement();
            slot.fill.style.position = Position.Absolute;
            slot.fill.style.left = 0f; slot.fill.style.top = 0f; slot.fill.style.bottom = 0f;
            slot.fill.style.width = new Length(0f, LengthUnit.Percent);
            slot.fill.style.backgroundImage = UTKTextureSafe.ToBackground(_gradGuard);
            slot.fill.style.backgroundSize = new StyleBackgroundSize(
                new BackgroundSize(Length.Percent(100f), Length.Percent(100f)));
            slot.fill.pickingMode = PickingMode.Ignore;
            slot.trough.Add(slot.fill);

            // 우측 남은 HP 비율 텍스트
            slot.ratioLabel = new Label("100%");
            slot.ratioLabel.style.position = Position.Absolute;
            slot.ratioLabel.style.right = 2f;
            slot.ratioLabel.style.top = 0f; slot.ratioLabel.style.bottom = 0f;
            slot.ratioLabel.style.fontSize = 9.6f;
            slot.ratioLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            slot.ratioLabel.style.color = new StyleColor(Color.white);
            slot.ratioLabel.pickingMode = PickingMode.Ignore;
            slot.trough.Add(slot.ratioLabel);

            // [F5] floating-stats 패널 (110:143) — [144차 P8-NP #9 P0] Row 1행 → Vertical 3행(HP/공격/의약).
            // Figma 원문: 30.6×27.2(18.8 아님), VERTICAL gap 2.448, pad 3.672, r1.8,
            // fill #10151C@0.22, stroke #7CE2FE@0.4 0.46.
            slot.statPanel = new VisualElement();
            slot.statPanel.style.flexDirection = FlexDirection.Column;
            slot.statPanel.style.alignItems = Align.Stretch;
            slot.statPanel.style.backgroundColor = new StyleColor(new Color(0x10 / 255f, 0x15 / 255f, 0x1C / 255f, 0.22f)); // #10151C@0.22 [144차 P8-NP #11]
            slot.statPanel.style.borderTopLeftRadius = 1.8f; slot.statPanel.style.borderTopRightRadius = 1.8f;
            slot.statPanel.style.borderBottomLeftRadius = 1.8f; slot.statPanel.style.borderBottomRightRadius = 1.8f;
            slot.statPanel.style.borderTopWidth = 0.46f; slot.statPanel.style.borderBottomWidth = 0.46f;
            slot.statPanel.style.borderLeftWidth = 0.46f; slot.statPanel.style.borderRightWidth = 0.46f;
            slot.statPanel.style.borderTopColor = new StyleColor(StatPanelStroke);
            slot.statPanel.style.borderBottomColor = new StyleColor(StatPanelStroke);
            slot.statPanel.style.borderLeftColor = new StyleColor(StatPanelStroke);
            slot.statPanel.style.borderRightColor = new StyleColor(StatPanelStroke);
            slot.statPanel.style.marginTop = 4f;
            slot.statPanel.style.paddingTop = 3.7f; slot.statPanel.style.paddingBottom = 3.7f;
            slot.statPanel.style.paddingLeft = 3.7f; slot.statPanel.style.paddingRight = 3.7f;
            slot.statPanel.pickingMode = PickingMode.Ignore;
            slot.statPanel.style.display = DisplayStyle.None;
            slot.root.Add(slot.statPanel);

            slot.hpStatVal = BuildStatItem(slot.statPanel, "❤", new Color(0.49f, 0.89f, 1f, 1f));     // #7CE2FE — HP
            slot.atkStatVal = BuildStatItem(slot.statPanel, "⚔", new Color(0.31f, 0.93f, 0.67f, 1f));   // #4EFEAA — 공격
            slot.spdStatVal = BuildStatItem(slot.statPanel, "💊", new Color(0.31f, 0.93f, 0.67f, 1f));  // #4EFEAA — 의약 [144차 P8-NP #9] 속도→의약(라벨만, 데이터 키 무손상)

            UTKWindowBase.ApplyUIToolkitFont(slot.root);
        }

        /// <summary>[F5] 스탯 항목(아이콘+값) 하나 생성 — floating-stats 행. 반환: 값 라벨.</summary>
        private static Label BuildStatItem(VisualElement parent, string iconChar, Color valColor)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 2.4f;   // [144차 P8-NP #9] 110:143 VERTICAL gap 2.448 (구 Row 마진 8)
            row.style.justifyContent = Justify.SpaceBetween;
            row.pickingMode = PickingMode.Ignore;
            parent.Add(row);

            var icon = new Label(iconChar);
            icon.style.fontSize = 12f;
            icon.style.color = new StyleColor(new Color(1f, 1f, 1f, 0.9f));
            icon.style.marginRight = 2f;
            icon.pickingMode = PickingMode.Ignore;
            row.Add(icon);

            var val = new Label("—");
            val.style.fontSize = 12f;
            val.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            val.style.color = new StyleColor(valColor);
            val.pickingMode = PickingMode.Ignore;
            row.Add(val);
            return val;
        }

        // ═══════════════ 공유 그라디언트 생성 ═══════════════
        private static void EnsureGradientTextures()
        {
            if (_gradGuard != null) return;
            _gradGuard =
                CreateGradient(new Color(0.42f, 0.90f, 0.62f), new Color(0.16f, 0.52f, 0.30f)); // 뱡사 정상 청록·초록
            _gradGuardWarn =
                CreateGradient(new Color(1.00f, 0.62f, 0.25f), new Color(0.72f, 0.16f, 0.10f)); // 경고 주황·적
        }

        private static Texture2D CreateGradient(Color top, Color bottom)
        {
            var tex = new Texture2D(GradW, GradH, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < GradH; y++)
            {
                float t = y / (float)(GradH - 1);
                Color c = Color.Lerp(top, bottom, t);
                for (int x = 0; x < GradW; x++)
                    tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return tex;
        }
    }
}