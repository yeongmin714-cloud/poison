using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;
using ProjectName.Systems;   // FarmPlot [26차]

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// [26차 농경시스템 업그레이드] 농장 플레이트 오버레이 — 밭(FarmPlot) 위 월드스페이스 가로바 2행.
    /// 디자인 언어는 NameplateOverlayUTK 몬스터 체력바를 계승: 다크 트로프 트랙 + 세로 그라디언트 채움 +
    /// 베벨(상좌 밝음/하우 어두움) + 슬레이트 스트로크. 2행 스택 — 위 행=성장도(초록 그라디언트),
    /// 아래 행=수분도(푸른색 #58A6FF 계열 — 성장바 바로 위에 붙은 아래 행).
    ///
    /// [계약] HUD 원형 게이지(HUDUTK)는 플레이어 HP/스태미나 전용 — 무변경. 농경 게이지는 여기(월드 가로바)만.
    ///
    /// [표시 규칙] 플레이어 근접(FarmPlateRangeM, 기본 8m) + Planted/Growing/Ready 단계만.
    /// 카메라 뒤(z<=0)/화면 밖 마진 40px 스킵 — NameplateOverlayUTK 계약 동일.
    /// 투영은 TargetScreenProjector 공용 산식(단일소스) 사용 — 자체 투영 산식 신설 금지.
    /// 풀: 초기 16 / 부족 시 8씩 / 상한 40. 수집 0.3s 스로틀. pickingMode=Ignore 전체.
    ///
    /// [배치] UIToolkitBootstrap.Ensure → UIRoot에 NameplateOverlayUTK 다음(순서 1)에 부착 —
    /// 이름표보다 위, 창보다 아래.
    /// </summary>
    public class FarmPlateOverlayUTK : MonoBehaviour
    {
        // ═══════════════ 싱글턴 / 부트스트랩 ═══════════════
        private static FarmPlateOverlayUTK _instance;
        private static bool _spawnRequested;

        /// <summary>멱등 부트스트랩 — UIToolkitBootstrap.Ensure가 UIRoot 생성 후 호출.</summary>
        public static void Ensure()
        {
            if (_instance != null || _spawnRequested) return;
            _spawnRequested = true;
            var go = new GameObject("FarmPlateOverlayUTK");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<FarmPlateOverlayUTK>();
        }

        // ═══════════════ 설정 상수 ═══════════════
        private const int   PoolInitial       = 16;
        private const int   PoolGrowStep      = 8;
        private const int   PoolMax           = 40;
        private const float ScanInterval      = 0.30f;   // 플롯 수집 주기(s)
        private const float CamRefreshInterval = 0.5f;   // Camera.main 재조회 주기
        private const float ScreenMargin      = 40f;     // 화면 밖 스킵 마진(px)
        private const float PlateRangeM       = 8f;      // 플레이어 근접 표시 거리(m)

        // 바 스펙 — NameplateOverlayUTK 계약 계승(78px 바/96px 플레이트, 2행 스택)
        private const float PlateW = 96f;    // 플레이트 폭(이름표 PlateW 통일 계약)
        private const float BarW   = 78f;    // 바 폭(Figma 101:5 gauge-container 참조치)
        private const float BarH   = 9.6f;   // 바 높이(몬스터 체력바 동일)
        private const float BarGap = 3f;     // 두 행 사이 간격(수분바가 성장바 바로 아래에 붙음)

        // [Figma GitHub-dark + 사용자 확정] 트로프/채움 색 — NameplateOverlayUTK 계승
        private static readonly Color LabelBgColor  = new Color(0.086f, 0.114f, 0.133f, 0.92f); // #161B22 패널
        private static readonly Color SlateRing     = new Color(46 / 255f, 52 / 255f, 61 / 255f, 1f);  // #2E343D 스트로크
        private static readonly Color BevelTopHi    = new Color(1f, 1f, 1f, 0.16f);     // 상좌 밝은 하이라이트
        private static readonly Color BevelBottomLo = new Color(0f, 0f, 0f, 0.45f);     // 하우 어두운 베벨
        private static readonly Color GrowthFillColor = new Color(0x3F / 255f, 0xB9 / 255f, 0x50 / 255f, 1f); // #3FB950 성장 초록
        private static readonly Color MoistureFillColor = new Color(0x58 / 255f, 0xA6 / 255f, 0xFF / 255f, 1f); // #58A6FF 수분 파랑
        private static readonly Color DryTrackColor  = new Color(0x0E / 255f, 0x22 / 255f, 0x33 / 255f, 1f);    // 수분 트랙(짙은 청록)
        private static readonly Color GrowTrackColor = new Color(0x0E / 255f, 0x2A / 255f, 0x16 / 255f, 1f);    // 성장 트랙(짙은 초록)

        // 세로 그라디언트 텍스처 해상도 (4x16, 스트레치로 입체 광택 — NameplateOverlayUTK 동일)
        private const int GradW = 4;
        private const int GradH = 16;

        private static Texture2D _gradGrowth;    // 성장 채움 — 초록 세로 그라디언트
        private static Texture2D _gradMoisture;  // 수분 채움 — 파랑 세로 그라디언트

        // ═══════════════ 필드 ═══════════════
        private VisualElement _overlayRoot;   // 풀스크린 절대 오버레이 루트
        private float _rootW;
        private float _rootH;

        private Camera _cam;
        private float _camNextRefresh;

        private sealed class Slot
        {
            public int unitId;
            public bool used;
            public FarmPlot plot;
            public Transform transform;

            public VisualElement root;
            public Label nameLabel;      // 작물명+상태 행
            public VisualElement growWrap;   // 성장 바 봉투
            public VisualElement growTrough; // 성장 트로프
            public VisualElement growFill;   // 성장 채움
            public Label growPct;            // 성장 % 라벨
            public VisualElement moistWrap;  // 수분 바 봉투
            public VisualElement moistTrough;// 수분 트로프
            public VisualElement moistFill;  // 수분 채움
            public Label moistPct;           // 수분 % 라벨

            public void Reset()
            {
                unitId = 0;
                used = false;
                plot = null;
                transform = null;
                if (root != null) root.style.display = DisplayStyle.None;
            }
        }

        private readonly List<Slot> _pool = new List<Slot>(PoolInitial);
        private readonly Dictionary<int, Slot> _slotByUnit = new Dictionary<int, Slot>(PoolInitial);
        private readonly List<FarmPlot> _scanScratch = new List<FarmPlot>(16);
        private readonly List<int> _removeScratch = new List<int>(8);

        private float _scanNext;

        private Transform _playerRoot;
        private float _playerNextRefresh;

        // ═══════════════ Unity 생명주기 ═══════════════
        private void OnEnable()
        {
            _instance = this;
            EnsureGradientTextures();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
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

            RefreshPlayer();
            ScanPlotsThrottled();

            foreach (var slot in _pool)
            {
                if (!slot.used) continue;
                RefreshSlot(slot);
            }
        }

        // ═══════════════ 루트 부착 ═══════════════
        private void AttachToRoot(VisualElement root)
        {
            _overlayRoot = new VisualElement();
            _overlayRoot.name = "FarmPlateOverlay";
            _overlayRoot.style.position = Position.Absolute;
            _overlayRoot.style.left = 0f; _overlayRoot.style.right = 0f;
            _overlayRoot.style.top = 0f;  _overlayRoot.style.bottom = 0f;
            _overlayRoot.pickingMode = PickingMode.Ignore;   // 클릭 통과 필수
            // NameplateOverlayUTK 다음(순서 1) 부착 — 이름표보다 위, 창보다 아래.
            root.Insert(System.Math.Min(1, root.childCount), _overlayRoot);
        }

        // ═══════════════ 카메라 / 플레이어 ═══════════════
        private void RefreshCamera()
        {
            if (_cam != null && Time.unscaledTime < _camNextRefresh) return;
            _cam = Camera.main;
            _camNextRefresh = Time.unscaledTime + CamRefreshInterval;
        }

        private void RefreshPlayer()
        {
            if (_playerRoot != null && Time.unscaledTime < _playerNextRefresh) return;
            var go = GameObject.FindGameObjectWithTag("Player");
            _playerRoot = go != null ? go.transform : null;
            _playerNextRefresh = Time.unscaledTime + 0.5f;
        }

        // ═══════════════ 플롯 수집 (0.3s 스로틀) ═══════════════
        private void ScanPlotsThrottled()
        {
            if (Time.unscaledTime < _scanNext) return;
            _scanNext = Time.unscaledTime + ScanInterval;

            _scanScratch.Clear();
            var plots = Object.FindObjectsByType<FarmPlot>(FindObjectsSortMode.None);
            foreach (var p in plots)
            {
                if (p == null) continue;
                // 표시 단계: Planted/Growing/Ready만 (빈 밭/구멍은 게이지 대상 아님)
                var ph = p.Phase;
                if (ph != FarmPlot.CropPhase.Planted && ph != FarmPlot.CropPhase.Growing
                    && ph != FarmPlot.CropPhase.Ready) continue;
                // 근접 게이트 — 플레이어 근처 밭만
                if (_playerRoot == null) continue;
                if ((p.transform.position - _playerRoot.position).sqrMagnitude > PlateRangeM * PlateRangeM) continue;
                _scanScratch.Add(p);
            }

            // 풀 동기화 — 스캔에 없던 슬롯 회수
            _removeScratch.Clear();
            foreach (var kv in _slotByUnit)
            {
                bool found = false;
                for (int i = 0; i < _scanScratch.Count; i++)
                {
                    if (_scanScratch[i].GetInstanceID() == kv.Key) { found = true; break; }
                }
                if (!found) _removeScratch.Add(kv.Key);
            }
            for (int i = 0; i < _removeScratch.Count; i++)
            {
                if (_slotByUnit.TryGetValue(_removeScratch[i], out Slot s)) Release(s);
            }

            for (int i = 0; i < _scanScratch.Count; i++)
            {
                FarmPlot p = _scanScratch[i];
                Slot slot = AcquireOrGetSlot(p.GetInstanceID(), p);
                if (slot != null) Bind(slot, p);
            }
        }

        // ═══════════════ 풀 ═══════════════
        private Slot AcquireOrGetSlot(int unitId, FarmPlot p)
        {
            if (_slotByUnit.TryGetValue(unitId, out Slot existing)) return existing;
            foreach (var s in _pool)
            {
                if (!s.used)
                {
                    s.unitId = unitId;
                    s.used = true;
                    s.plot = p;
                    s.transform = p.transform;
                    s.Reset();
                    s.used = true;
                    _slotByUnit[unitId] = s;
                    EnsureElements(s);
                    s.root.style.display = DisplayStyle.Flex;
                    return s;
                }
            }
            if (_pool.Count < PoolMax)
            {
                for (int i = 0; i < PoolGrowStep && _pool.Count < PoolMax; i++)
                {
                    var ns = new Slot();
                    EnsureElements(ns);
                    _pool.Add(ns);
                }
                return AcquireOrGetSlot(unitId, p);
            }
            return null;
        }

        private void Release(Slot slot)
        {
            _slotByUnit.Remove(slot.unitId);
            slot.Reset();
        }

        private void Bind(Slot slot, FarmPlot p)
        {
            slot.plot = p;
            slot.transform = p.transform;
        }

        // ═══════════════ 프레임 갱신 ═══════════════
        private void RefreshSlot(Slot slot)
        {
            FarmPlot p = slot.plot;
            if (p == null || slot.transform == null) { slot.used = false; Hide(slot); return; }

            var ph = p.Phase;
            if (ph != FarmPlot.CropPhase.Planted && ph != FarmPlot.CropPhase.Growing
                && ph != FarmPlot.CropPhase.Ready) { slot.used = false; Hide(slot); return; }

            // 근접 재판정 — 플레이어가 멀어지면 즉시 숨김
            if (_playerRoot == null
                || (slot.transform.position - _playerRoot.position).sqrMagnitude > PlateRangeM * PlateRangeM)
            { Hide(slot); return; }

            string label;
            if (ph == FarmPlot.CropPhase.Planted) label = "🌱 " + p.CropType + " 발아";
            else if (ph == FarmPlot.CropPhase.Growing) label = "🌱 " + p.CropType + " 성장";
            else label = "🧺 " + p.CropType + " 수확";

            slot.nameLabel.text = label;

            float grow = p.GrowDurationDay > 0
                ? Mathf.Clamp01(GetGrowProgress(p))
                : 0f;
            float moist = GetMoisture(p);

            slot.growFill.style.width = new Length(Mathf.Clamp01(grow) * 100f, LengthUnit.Percent);
            slot.moistFill.style.width = new Length(Mathf.Clamp01(moist) * 100f, LengthUnit.Percent);
            slot.growPct.text = Mathf.RoundToInt(grow * 100f).ToString() + "%";
            slot.moistPct.text = Mathf.RoundToInt(moist * 100f).ToString() + "%";

            PlaceSlot(slot);
        }

        // ═══════════════ 데이터 프록시 (FarmPlot 상태 기반) ═══════════════
        private static float GetGrowProgress(FarmPlot p)
        {
            // 리플렉션 없이: FarmPlot의 공개 시각 상태에서 판독 — 성장 진행은 phase+planted 시각으로 산출.
            // FarmPlot.GrowProgress01은 private이므로 여기서는 상태 스냅샷을 리플렉션으로 읽는다
            // (FarmingManager/HUDUTK 선례의 private SerializeField 리플렉션 관례와 동일).
            var t = typeof(FarmPlot);
            var pf = t.GetField("_plantedGameTime",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var gf = t.GetField("_growDurationDay",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var mf = t.GetMethod("CurrentClock",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (pf == null || gf == null) return 0f;
            double planted = pf.GetValue(p) is double d ? d : 0.0;
            int growDays = gf.GetValue(p) is int g ? g : 3;
            if (planted <= 0.0) return 0f;
            double now = mf != null ? System.Convert.ToDouble(mf.Invoke(null, null)) : 0.0;
            return FarmPlot.ComputeGrowProgress01(now - planted, growDays);
        }

        private static float GetMoisture(FarmPlot p)
        {
            var t = typeof(FarmPlot);
            var mf = t.GetField("_lastMoistureTick",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cm = t.GetMethod("CurrentClock",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (mf == null) return 0f;
            double last = mf.GetValue(p) is double d ? d : 0.0;
            double now = cm != null ? System.Convert.ToDouble(cm.Invoke(null, null)) : 0.0;
            return FarmPlot.ComputeMoistureFraction(now - last);
        }

        // ═══════════════ 위치 투영 + cull ═══════════════
        private void PlaceSlot(Slot slot)
        {
            Vector3 anchor = slot.transform.position + Vector3.up * 0.6f;   // 흙 위 살짝 위

            Vector3 vp = _cam.WorldToViewportPoint(anchor);
            if (vp.z <= 0f) { Hide(slot); return; }              // 카메라 뒤

            float x = vp.x * _rootW;
            float y = (1f - vp.y) * _rootH;

            // 화면 밖 마진 스킵
            if (x < -ScreenMargin || x > _rootW + ScreenMargin ||
                y < -ScreenMargin || y > _rootH + ScreenMargin) { Hide(slot); return; }

            slot.root.style.display = DisplayStyle.Flex;
            slot.root.style.left = x - PlateW * 0.5f;
            slot.root.style.top = y - PlateH();
        }

        private static float PlateH()
        {
            return 14f + BarH + BarGap + BarH + 4f;   // 이름행 14 + 성장바 + 간격 + 수분바 + 여백
        }

        private void Hide(Slot slot)
        {
            if (slot.root != null && slot.root.style.display != DisplayStyle.None)
                slot.root.style.display = DisplayStyle.None;
        }

        // ═══════════════ 슬롯 UI 구성 ═══════════════
        private void EnsureElements(Slot slot)
        {
            if (slot.root != null)
            {
                if (_overlayRoot != null && slot.root.parent == null)
                    _overlayRoot.Add(slot.root);
                return;
            }

            slot.root = new VisualElement();
            slot.root.name = "FarmPlateSlot";
            slot.root.style.position = Position.Absolute;
            slot.root.style.width = PlateW;
            slot.root.style.flexDirection = FlexDirection.Column;
            slot.root.style.alignItems = Align.Center;
            slot.root.pickingMode = PickingMode.Ignore;
            slot.root.style.display = DisplayStyle.None;
            _overlayRoot.Add(slot.root);

            // 이름 행 — 다크 아크릴 배경 + 슬레이트 1px + 라운드 4px (NameplateOverlayUTK 계승)
            slot.nameLabel = new Label();
            slot.nameLabel.style.backgroundColor = new StyleColor(LabelBgColor);
            slot.nameLabel.style.borderTopWidth = 1f; slot.nameLabel.style.borderBottomWidth = 1f;
            slot.nameLabel.style.borderLeftWidth = 1f; slot.nameLabel.style.borderRightWidth = 1f;
            slot.nameLabel.style.borderTopColor = new StyleColor(SlateRing);
            slot.nameLabel.style.borderBottomColor = new StyleColor(SlateRing);
            slot.nameLabel.style.borderLeftColor = new StyleColor(SlateRing);
            slot.nameLabel.style.borderRightColor = new StyleColor(SlateRing);
            slot.nameLabel.style.borderTopLeftRadius = 4f; slot.nameLabel.style.borderTopRightRadius = 4f;
            slot.nameLabel.style.borderBottomLeftRadius = 4f; slot.nameLabel.style.borderBottomRightRadius = 4f;
            slot.nameLabel.style.width = PlateW;   // [80차] 이름 행 폭 = 바 행 폭 통일
            slot.nameLabel.style.height = 14f;
            slot.nameLabel.style.fontSize = 10f;
            slot.nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            slot.nameLabel.style.color = new StyleColor(Color.white);
            slot.nameLabel.style.whiteSpace = WhiteSpace.NoWrap;
            slot.nameLabel.pickingMode = PickingMode.Ignore;
            slot.root.Add(slot.nameLabel);

            // ═══ 성장 바 (위 행) ═══ — 음각 트로프 + 세로 그라디언트 채움 + 베벨 (몬스터 체력바 계승)
            BuildOneBar(slot, isGrowth: true);
            // ═══ 수분 바 (아래 행 — 성장바 바로 아래 붙음) ═══
            BuildOneBar(slot, isGrowth: false);

            UTKWindowBase.ApplyUIToolkitFont(slot.root);
        }

        private void BuildOneBar(Slot slot, bool isGrowth)
        {
            var wrap = new VisualElement();
            wrap.name = isGrowth ? "GrowBarFrame" : "MoistureBarFrame";
            wrap.style.width = PlateW;   // [80차] 바 행 폭 = 이름 행 폭 통일
            wrap.style.height = BarH;
            wrap.style.marginTop = isGrowth ? 3f : BarGap;
            wrap.style.flexShrink = 0f;
            wrap.pickingMode = PickingMode.Ignore;
            wrap.style.borderTopWidth = 1f; wrap.style.borderBottomWidth = 1f;
            wrap.style.borderLeftWidth = 1f; wrap.style.borderRightWidth = 1f;
            wrap.style.borderTopColor = new StyleColor(SlateRing);
            wrap.style.borderBottomColor = new StyleColor(SlateRing);
            wrap.style.borderLeftColor = new StyleColor(SlateRing);
            wrap.style.borderRightColor = new StyleColor(SlateRing);
            wrap.style.borderTopLeftRadius = 2f; wrap.style.borderTopRightRadius = 2f;
            wrap.style.borderBottomLeftRadius = 2f; wrap.style.borderBottomRightRadius = 2f;
            slot.root.Add(wrap);

            var trough = new VisualElement();
            trough.style.position = Position.Absolute;
            trough.style.left = 1f; trough.style.right = 1f;
            trough.style.top = 1f;  trough.style.bottom = 1f;
            trough.style.overflow = Overflow.Hidden;
            trough.style.backgroundColor = new StyleColor(isGrowth ? GrowTrackColor : DryTrackColor);
            trough.style.borderTopWidth = 1f;  trough.style.borderBottomWidth = 1f;
            trough.style.borderLeftWidth = 1f; trough.style.borderRightWidth = 1f;
            trough.style.borderTopColor = new StyleColor(BevelTopHi);
            trough.style.borderBottomColor = new StyleColor(BevelBottomLo);
            trough.style.borderLeftColor = new StyleColor(BevelTopHi);
            trough.style.borderRightColor = new StyleColor(BevelBottomLo);
            trough.pickingMode = PickingMode.Ignore;
            wrap.Add(trough);

            var fill = new VisualElement();
            fill.style.position = Position.Absolute;
            fill.style.left = 0f; fill.style.top = 0f; fill.style.bottom = 0f;
            fill.style.width = new Length(0f, LengthUnit.Percent);
            fill.style.backgroundImage = UTKTextureSafe.ToBackground(isGrowth ? _gradGrowth : _gradMoisture);
            fill.style.backgroundSize = new StyleBackgroundSize(
                new BackgroundSize(Length.Percent(100f), Length.Percent(100f)));
            fill.pickingMode = PickingMode.Ignore;
            trough.Add(fill);

            var pct = new Label("0%");
            pct.style.position = Position.Absolute;
            pct.style.right = 2f;
            pct.style.top = 0f; pct.style.bottom = 0f;
            pct.style.fontSize = 8.4f;
            pct.style.unityTextAlign = TextAnchor.MiddleRight;
            pct.style.color = new StyleColor(Color.white);
            pct.pickingMode = PickingMode.Ignore;
            trough.Add(pct);

            if (isGrowth)
            {
                slot.growWrap = wrap; slot.growTrough = trough; slot.growFill = fill; slot.growPct = pct;
            }
            else
            {
                slot.moistWrap = wrap; slot.moistTrough = trough; slot.moistFill = fill; slot.moistPct = pct;
            }
        }

        // ═══════════════ 공유 그라디언트 생성 (NameplateOverlayUTK.CreateGradient 동일 산식) ═══════════════
        private static void EnsureGradientTextures()
        {
            if (_gradGrowth != null) return;
            _gradGrowth =
                CreateGradient(new Color(0.45f, 0.90f, 0.55f), new Color(0.16f, 0.55f, 0.26f));   // 성장 초록 (상밋→하진)
            _gradMoisture =
                CreateGradient(new Color(0.45f, 0.72f, 1.00f), new Color(0.15f, 0.42f, 0.80f));   // 수분 파랑 (상밋→하진)
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
