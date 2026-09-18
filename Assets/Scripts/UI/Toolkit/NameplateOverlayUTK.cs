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
        private const float  BarW              = 72f;   // 입체 HP바 폭
        private const float  BarH              = 10f;   // 입체 HP바 높이
        private const float  NpcH              = 26f;   // NPC 이름표 높이

        // 라벨 배경 (다크 갈색 아크릴 반투명) — 요구 rgba(30,24,18,0.82)
        private static readonly Color LabelBgColor   = new Color(30 / 255f, 24 / 255f, 18 / 255f, 0.82f);
        private static readonly Color GoldRing       = new Color(0xC9 / 255f, 0xA2 / 255f, 0x27 / 255f, 1f);
        private static readonly Color TroughBg       = new Color(0.045f, 0.033f, 0.022f, 1f); // 거의 검정 트로프
        private static readonly Color BevelTopHi     = new Color(1f, 1f, 1f, 0.16f);          // 상좌 밝은 하이라이트
        private static readonly Color BevelBottomLo  = new Color(0f, 0f, 0f, 0.45f);          // 하우 어두운 베벨

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
        private static Texture2D _gradMonster;    // 몬스터 — 적색 계열

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
        private enum Kind { Guard, Monster, Npc }

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

            // UI 요소
            public VisualElement root;
            public Label nameLabel;
            public Label lvLabel;
            public VisualElement barWrap;
            public VisualElement trough;
            public VisualElement fill;
            public Label ratioLabel;

            public float estHeight; // 종류별 추정 높이(위치 보정)

            public void Reset()
            {
                unitId = 0;
                used = false;
                guard = null;
                monster = null;
                npc = null;
                transform = null;
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
            slot.headOffset = 2.2f;            // GuardHeadUI 앵커 계승
            slot.estHeight = NameH + BarH + 4f;
            slot.lvLabel.style.display = DisplayStyle.None;
            slot.barWrap.style.display = DisplayStyle.Flex;
        }

        private void BindMonster(Slot slot, AnimalAI m)
        {
            slot.kind = Kind.Monster;
            slot.monster = m;
            slot.guard = null;
            slot.npc = null;
            slot.estHeight = NameH + LvH + BarH + 8f;
            slot.lvLabel.style.display = DisplayStyle.Flex;
            slot.barWrap.style.display = DisplayStyle.Flex;
            // bounds 기반 앵커 (MonsterHeadUI 계승)
            if (Time.unscaledTime >= slot.headNextRefresh)
            {
                slot.headHeight = ComputeHeadHeight(m.transform);
                slot.headNextRefresh = Time.unscaledTime + HeadRefreshInterval;
            }
        }

        private void BindNpc(Slot slot, NameplateDisplay n)
        {
            slot.kind = Kind.Npc;
            slot.npc = n;
            slot.guard = null;
            slot.monster = null;
            slot.headOffset = 2.0f;            // NameplateDisplay 앵커 계승
            slot.estHeight = NpcH;
            slot.lvLabel.style.display = DisplayStyle.None;
            slot.barWrap.style.display = DisplayStyle.None;
        }

        // ═══════════════ 프레임 갱신 ═══════════════
        private void RefreshSlot(Slot slot)
        {
            // 사망/파괴 유닛 — 해제 대상으로 표시(다음 스캔에서 회수)
            if (!SlotAlive(slot)) { slot.used = false; return; }

            switch (slot.kind)
            {
                case Kind.Guard:   RefreshGuard(slot); break;
                case Kind.Monster: RefreshMonster(slot); break;
                case Kind.Npc:     RefreshNpc(slot); break;
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
                default: return false;
            }
        }

        // ─── 뱡사 ───
        private void RefreshGuard(Slot slot)
        {
            GuardPlaceholder g = slot.guard;
            if (g == null) return;

            float ratio = g.MaxHP > 0f ? Mathf.Clamp01(g.CurrentHP / g.MaxHP) : 0f;
            slot.nameLabel.text = g.GuardName + " Lv." + g.Level.ToString();
            SetFillWidth(slot, ratio);
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
            slot.lvLabel.text = ResolveLevelText(m);
            slot.lvLabel.style.color = new StyleColor(tierColor);

            float ratio = m.MaxHP > 0f ? Mathf.Clamp01(m.CurrentHP / m.MaxHP) : 0f;
            SetFillWidth(slot, ratio);
            slot.fill.style.backgroundImage = UTKTextureSafe.ToBackground(_gradMonster);
            slot.ratioLabel.text = Mathf.RoundToInt(ratio * 100f).ToString() + "%";

            PlaceSlot(slot);
        }

        // ─── NPC (플레이어 근접 시만) ───
        private void RefreshNpc(Slot slot)
        {
            NameplateDisplay n = slot.npc;
            if (n == null) return;

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
            slot.nameLabel.style.width = SlotW - 6f;
            slot.nameLabel.style.height = NameH;
            slot.nameLabel.style.fontSize = 12f;
            slot.nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            slot.nameLabel.style.color = new StyleColor(Color.white);
            slot.nameLabel.style.whiteSpace = WhiteSpace.NoWrap;
            slot.nameLabel.pickingMode = PickingMode.Ignore;
            slot.root.Add(slot.nameLabel);

            // 몬스터 전용 Lv 라벨 (티어색)
            slot.lvLabel = new Label();
            slot.lvLabel.style.width = SlotW - 6f;
            slot.lvLabel.style.height = LvH;
            slot.lvLabel.style.fontSize = 12f;
            slot.lvLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            slot.lvLabel.style.color = new StyleColor(TierBeginner);
            slot.lvLabel.pickingMode = PickingMode.Ignore;
            slot.root.Add(slot.lvLabel);

            // ═══ 입체 HP바 ═══ (요구: 외곽 진흙 테두리 + 상좌 하이라이트/하우 베벨 음각 → 골드 링)
            slot.barWrap = new VisualElement();
            slot.barWrap.name = "HpBarFrame";
            slot.barWrap.style.width = BarW;
            slot.barWrap.style.height = BarH;
            slot.barWrap.style.marginTop = 4f;
            slot.barWrap.style.marginBottom = 2f;
            slot.barWrap.style.flexShrink = 0f;
            slot.barWrap.pickingMode = PickingMode.Ignore;
            // 골드 외곽 링 (봉투 — 창 표시 경계)
            slot.barWrap.style.borderTopWidth = 1f; slot.barWrap.style.borderBottomWidth = 1f;
            slot.barWrap.style.borderLeftWidth = 1f; slot.barWrap.style.borderRightWidth = 1f;
            slot.barWrap.style.borderTopColor = new StyleColor(GoldRing);
            slot.barWrap.style.borderBottomColor = new StyleColor(GoldRing);
            slot.barWrap.style.borderLeftColor = new StyleColor(GoldRing);
            slot.barWrap.style.borderRightColor = new StyleColor(GoldRing);
            slot.barWrap.style.borderTopLeftRadius = 2f; slot.barWrap.style.borderTopRightRadius = 2f;
            slot.barWrap.style.borderBottomLeftRadius = 2f; slot.barWrap.style.borderBottomRightRadius = 2f;
            slot.root.Add(slot.barWrap);

            // 음각 트로프 — 거의 검정 배경 + 내부 상좌 밝은/하우 어두운 베벨
            slot.trough = new VisualElement();
            slot.trough.style.position = Position.Absolute;
            slot.trough.style.left = 1f; slot.trough.style.right = 1f;
            slot.trough.style.top = 1f;  slot.trough.style.bottom = 1f;
            slot.trough.style.overflow = Overflow.Hidden;
            slot.trough.style.backgroundColor = new StyleColor(TroughBg);
            slot.trough.style.borderTopWidth = 1f;  slot.trough.style.borderBottomWidth = 1f;
            slot.trough.style.borderLeftWidth = 1f; slot.trough.style.borderRightWidth = 1f;
            slot.trough.style.borderTopColor = new StyleColor(BevelTopHi);
            slot.trough.style.borderBottomColor = new StyleColor(BevelBottomLo);
            slot.trough.style.borderLeftColor = new StyleColor(BevelTopHi);
            slot.trough.style.borderRightColor = new StyleColor(BevelBottomLo);
            slot.trough.pickingMode = PickingMode.Ignore;
            slot.barWrap.Add(slot.trough);

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
            slot.ratioLabel.style.fontSize = 8f;
            slot.ratioLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            slot.ratioLabel.style.color = new StyleColor(Color.white);
            slot.ratioLabel.pickingMode = PickingMode.Ignore;
            slot.trough.Add(slot.ratioLabel);

            UTKWindowBase.ApplyUIToolkitFont(slot.root);
        }

        // ═══════════════ 공유 그라디언트 생성 ═══════════════
        private static void EnsureGradientTextures()
        {
            if (_gradGuard != null) return;
            _gradGuard =
                CreateGradient(new Color(0.42f, 0.90f, 0.62f), new Color(0.16f, 0.52f, 0.30f)); // 뱡사 정상 청록·초록
            _gradGuardWarn =
                CreateGradient(new Color(1.00f, 0.62f, 0.25f), new Color(0.72f, 0.16f, 0.10f)); // 경고 주황·적
            _gradMonster =
                CreateGradient(new Color(0.95f, 0.42f, 0.34f), new Color(0.52f, 0.10f, 0.08f)); // 몬스터 적색
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