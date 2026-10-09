using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;       // PlayerHealth / PlayerStats / PlayerInventory
using ProjectName.Systems;    // PlayerMovement

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U7 — 메인 HUD HP/스태미나 원형 게이지 (통합판 2026-09-24).
    ///
    /// [사용자 설계 09-24] 체력바 중복 정리:
    ///  - 남길 것 = Figma 원형 게이지 하나만: 핫바 바로 옆 조그맣게,
    ///    하트(체력, 빨강 링) + 번개(스태미나, 노랑 링), 시계방향으로 줄어든다.
    ///  - 제거: StatusGaugesUTK(좌하단 132px) / 기존 BuildRings(하단 ❤/⚡ 사각) /
    ///          기존 BuildCircularGauges(우상단 240px) / IMGUI HUD 하트(은퇴 게이트 이미).
    ///
    /// 데이터: PlayerHealth.CurrentHP/MaxHP + PlayerMovement.Stamina/MaxStamina(250ms 폴링).
    /// UTKCircularGauge 벡터 호 — 12시 시작 시계방향 fill(값 감소 = 시계방향 소모).
    /// 아이콘: UI/GaugeHeart(하트), UI/GaugeBolt(번개) — 피그마에서 만든 자산.
    /// </summary>
    public class HUDUTK : VisualElement
    {
        // ===== 싱글턴 / 부트스트랩 =====
        private static HUDUTK _instance;
        public static HUDUTK Instance => _instance;

        /// <summary>멱등 부트스트랩 보장 — UIToolkitBootstrap.Ensure 스타일.</summary>
        public static void Ensure()
        {
            if (_instance != null && _instance.parent != null) return;
            _instance = new HUDUTK();
            var go = new GameObject("HUDUTK");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<MonoKeeper>().hud = _instance;
            Debug.Log("[HUDUTK] 초기화 완료 — 핫바 옆 원형 HP/스태미나 게이지 준비");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null && _instance.parent != null) return;
            Ensure();
        }

        // ===== 설정 =====
        private const int   PollMs     = 250;
        private const float Bottom     = 14f;   // 하단 정렬 (핫바와 같은 기준)

        // [Figma 23:6] 캔버스 내 게이지 bounds와 아이콘 중심 크기 — 계약 단정용 public const(계획 v2 Phase 2 승격).
        public  const float GaugeSize     = 137.6f; // Figma 23:6 gauge bounds
        public  const float GaugeGap      = 24f;    // Figma gauge-to-gauge gap (게이지 간격 24 raw 유지)
        public  const float GaugeIconSize = 68.8f;  // Figma center icon bounds

        // [계획 v2 Phase 2] 화면 하단 고정 앵커 여백 — 스케일 후 게이지 시각 하단이 화면 하단에서 24px 위에 고정.
        // 하단 고정 앵커는 사용자 요구(캡처 간 세로 편차 제거) — Figma 캔버스 y=855.2 좌표 규약과 의도적 차이(계획 v2 Phase 2 기록).
        public  const float GaugeBottomMarginPx = 24f;

        // [Figma GitHub-dark + 사용자 설계] 링 색
        private readonly Color _hpColor = new Color(1f, 0.30f, 0.30f, 1f);       // 체력 링 — 레드빛 (하트)
        private readonly Color _stColor = new Color(1f, 0.83f, 0.30f, 1f);       // 스태미나 링 — 노란빛 (번개)

        private VisualElement    _gaugeHost;      // 핫바 옆 호스트
        private VisualElement    _hpRoot;         // 하트 게이지 루트 (7층 스택)
        private VisualElement    _stRoot;         // 번개 게이지 루트
        private UTKCircularGauge _hpGauge;        // 하트 arc 레이어 (체력)
        private UTKCircularGauge _staminaGauge;   // 번개 arc 레이어 (스태미나)
        private Texture2D        _ringTex;        // UI/HudGaugeRing
        private Texture2D        _heartTex;       // UI/GaugeHeart
        private Texture2D        _boltTex;        // UI/GaugeBolt
        private IVisualElementScheduledItem _pollTask;

        private HUDUTK()
        {
            // 풀스크린 Ignore 오버레이 — 자식 절대좌표가 화면 기준으로 잡히고 클릭 차단 없음
            style.position = Position.Absolute;
            style.left = 0f; style.right = 0f; style.top = 0f; style.bottom = 0f;
            pickingMode = PickingMode.Ignore;

            BuildHotbarGauges();   // [통합] 핫바 옆 원형 하트/번개 게이지

            UTKWindowBase.ApplyUIToolkitFont(this);

            StartPolling();
        }

        /// <summary>Archived 23:6 gauge pair. Figma node names both say stamina-gauge; runtime semantics use heart=HP then bolt=stamina.</summary>
        private void BuildHotbarGauges()
        {
            _ringTex  = Resources.Load<Texture2D>("UI/HudGaugeRing");
            _heartTex = Resources.Load<Texture2D>("UI/GaugeHeart");
            _boltTex  = Resources.Load<Texture2D>("UI/GaugeBolt");

            _gaugeHost = new VisualElement();
            _gaugeHost.name = "HotbarGauges";
            _gaugeHost.style.position = Position.Absolute;
            _gaugeHost.style.top = 855.2f; // Figma HUD_Canvas local coordinates
            _gaugeHost.style.flexDirection = FlexDirection.Row;
            _gaugeHost.pickingMode = PickingMode.Ignore;
            Add(_gaugeHost);

            // The archive node labels both gauges "stamina-gauge" and its child labels do not settle the runtime meter identity.
            // Preserve the existing user-approved game mapping (red heart=HP, yellow bolt=stamina), positioned explicitly at Figma bounds.
            _hpRoot      = BuildOneGauge("HpGauge",      _heartTex, _hpColor,      out _hpGauge);
            _stRoot      = BuildOneGauge("StaminaGauge", _boltTex,  _stColor,      out _staminaGauge);

            // 폴링 첫 틱 전 기본값 (가득 찬 오표시 방지)
            if (_hpGauge != null)      _hpGauge.Fraction = 0f;
            if (_staminaGauge != null) _staminaGauge.Fraction = 0f;
        }

        /// <summary>단일 원형 게이지 빌드 — [계획 2026-10-09 Phase B] Figma 23:6 7층 스택:
        /// ①bg-glow(게이지색 저알파 뒤광) ②outer-track(백색 저알파 링) ③UTKCircularGauge 진행 arc
        /// ④progress-tip은 값 연동 위치 갱신이 필요해 생략(판단 기록 — arc 자체로 진행 구분 충분)
        /// ⑤inner-plate(96.8 흰 원판) ⑥rim-highlight(88.9 스트로크) ⑦중앙 아이콘(68.8).
        /// 시맨틱 색은 게임 매핑 유지(HP 레드/스타미나 옐로 — 09-24 사용자 설계 계약). Figma 목업의
        /// 오렌지 그라데이션은 미채택. 호스트 transform scale=k가 등비 확대 담당 → 각 층 raw 고정 크기.</summary>
        private VisualElement BuildOneGauge(string gaugeName, Texture2D iconTex, Color fill, out UTKCircularGauge arcGauge)
        {
            var gauge = new VisualElement();
            gauge.name = gaugeName;
            gauge.style.width = GaugeSize;
            gauge.style.height = GaugeSize;
            gauge.style.marginRight = GaugeGap;
            gauge.pickingMode = PickingMode.Ignore;
            if (_ringTex != null)
            {
                gauge.style.backgroundImage = UTKTextureSafe.ToBackground(_ringTex);
                gauge.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            }
            _gaugeHost.Add(gauge);

            // ① bg-radial-glow — radial 그라데이션 미지원 → 게이지색 저알파 단색 원(판단 기록)
            var glow = new VisualElement();
            glow.name = gaugeName + "BgGlow";
            glow.style.position = Position.Absolute;
            float glowSize = 264f;
            glow.style.left = (GaugeSize - glowSize) * 0.5f;
            glow.style.top = (GaugeSize - glowSize) * 0.5f;
            glow.style.width = glowSize;
            glow.style.height = glowSize;
            var glowColor = fill; glowColor.a = 0.10f;
            glow.style.backgroundColor = new StyleColor(glowColor);
            SetCircleRadius(glow);
            glow.pickingMode = PickingMode.Ignore;
            gauge.Add(glow);

            // ② outer-track — 백색 저알파 fill + 스트로크(Figma 1.32)
            var track = new VisualElement();
            track.name = gaugeName + "OuterTrack";
            track.style.position = Position.Absolute;
            track.style.left = 0f;
            track.style.top = 0f;
            track.style.width = GaugeSize;
            track.style.height = GaugeSize;
            track.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.08f));
            track.style.borderTopWidth = track.style.borderBottomWidth = 1.32f;
            track.style.borderLeftWidth = track.style.borderRightWidth = 1.32f;
            var trackStroke = new StyleColor(new Color(1f, 1f, 1f, 0.10f));
            track.style.borderTopColor = track.style.borderBottomColor = trackStroke;
            track.style.borderLeftColor = track.style.borderRightColor = trackStroke;
            SetCircleRadius(track);
            track.pickingMode = PickingMode.Ignore;
            gauge.Add(track);

            // ③ 진행 arc — 기존 UTKCircularGauge 벡터 호(시맨틱 색 계약 유지)
            arcGauge = new UTKCircularGauge();
            arcGauge.name = gaugeName + "Arc";
            arcGauge.style.position = Position.Absolute;
            arcGauge.style.left = 0f;
            arcGauge.style.top = 0f;
            arcGauge.style.width = GaugeSize;
            arcGauge.style.height = GaugeSize;
            arcGauge.FillColor = fill;
            arcGauge.pickingMode = PickingMode.Ignore;
            gauge.Add(arcGauge);

            // ⑤ inner-plate — 흰 원판(아이콘 뒤 배경판, Figma 96.8@20.4 + stroke 1.32)
            var plate = new VisualElement();
            plate.name = gaugeName + "InnerPlate";
            plate.style.position = Position.Absolute;
            float plateSize = 96.8f;
            float plateOff = (GaugeSize - plateSize) * 0.5f;
            plate.style.left = plateOff;
            plate.style.top = plateOff;
            plate.style.width = plateSize;
            plate.style.height = plateSize;
            plate.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.85f));
            plate.style.borderTopWidth = plate.style.borderBottomWidth = 1.32f;
            plate.style.borderLeftWidth = plate.style.borderRightWidth = 1.32f;
            var plateStroke = new StyleColor(new Color(1f, 1f, 1f, 0.20f));
            plate.style.borderTopColor = plate.style.borderBottomColor = plateStroke;
            plate.style.borderLeftColor = plate.style.borderRightColor = plateStroke;
            SetCircleRadius(plate);
            plate.pickingMode = PickingMode.Ignore;
            gauge.Add(plate);

            // ⑥ inner-plate-rim-highlight — 88.9 스트로크 링
            var rim = new VisualElement();
            rim.name = gaugeName + "RimHighlight";
            rim.style.position = Position.Absolute;
            float rimSize = 88.9f;
            float rimOff = (GaugeSize - rimSize) * 0.5f;
            rim.style.left = rimOff;
            rim.style.top = rimOff;
            rim.style.width = rimSize;
            rim.style.height = rimSize;
            rim.style.borderTopWidth = rim.style.borderBottomWidth = 1.98f;
            rim.style.borderLeftWidth = rim.style.borderRightWidth = 1.98f;
            var rimStroke = new StyleColor(new Color(1f, 1f, 1f, 0.35f));
            rim.style.borderTopColor = rim.style.borderBottomColor = rimStroke;
            rim.style.borderLeftColor = rim.style.borderRightColor = rimStroke;
            SetCircleRadius(rim);
            rim.pickingMode = PickingMode.Ignore;
            gauge.Add(rim);

            // ⑦ 중앙 아이콘 — 기존 68.8@34.4 계약 유지
            if (iconTex != null)
            {
                var icon = new VisualElement();
                icon.name = gaugeName + "Icon";
                icon.style.position = Position.Absolute;
                float off = (GaugeSize - GaugeIconSize) * 0.5f;
                icon.style.left = off;
                icon.style.top = off;
                icon.style.width = GaugeIconSize;
                icon.style.height = GaugeIconSize;
                icon.style.backgroundImage = UTKTextureSafe.ToBackground(iconTex);
                icon.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                icon.pickingMode = PickingMode.Ignore;
                gauge.Add(icon);
            }
            return gauge;
        }

        /// <summary>원형 클리핑 — border-radius 전원 큰 값(클램프로 반지름 처리).</summary>
        private static void SetCircleRadius(VisualElement element)
        {
            element.style.borderTopLeftRadius = 9999f;
            element.style.borderTopRightRadius = 9999f;
            element.style.borderBottomLeftRadius = 9999f;
            element.style.borderBottomRightRadius = 9999f;
        }

        /// <summary>핫바 왼쪽 옆에 배치 — 폴링 첫 틱에 panel width 실측으로 핫바(중앙) 좌측 여백 확보.
        ///  호스트 너비 = 2게이지 + 간격 (게이지는 오른쪽→왼쪽 순서로 추가됐으니 왼쪽정렬 위해 역방향 계산).</summary>
        private void PositionHost()
        {
            if (panel == null) return;
            // [계획 v2 Phase 2] 루트=화면 스트레치 유지. 게이지 Y는 화면 하단 고정 앵커로 일원화,
            // X만 기존 축 분율 앵커 유지 — 크기는 등배수 k로 통일(종횡비 왜곡 제거).
            var uiRoot = UIToolkitBootstrap.UIRoot;
            Vector2 rootSize = uiRoot != null
                ? new Vector2(uiRoot.resolvedStyle.width, uiRoot.resolvedStyle.height)
                : new Vector2(panel.visualTree.worldBound.width, panel.visualTree.worldBound.height);
            if (rootSize.x <= 0f || rootSize.y <= 0f) return;
            float anchorX = rootSize.x / FigmaCanvasLayout.CanvasWidth;
            float anchorY = rootSize.y / FigmaCanvasLayout.CanvasHeight;
            float k = Mathf.Min(anchorX, anchorY);
            if (k <= 0f) return;

            // 게이지 호스트 = X 분율 앵커 + Y 하단 고정 앵커 + raw 크기 + 등비 scale.
            // [계획 v2 Phase 2] top = rootSize.y - GaugeSize * k - GaugeBottomMarginPx:
            //   transformOrigin(0,0)+scale=k라서 스케일 후 시각 높이 = GaugeSize*k → 시각 하단이 화면 하단에서 24px 위에 고정.
            //   anchorY는 등배수 k 산출에만 사용(게이지 Y 배치에는 미사용).
            // 하단 고정 앵커는 사용자 요구(캡처 간 세로 편차 제거) — Figma 캔버스 y=855.2 좌표 규약과 의도적 차이(계획 v2 Phase 2 기록).
            _gaugeHost.style.left = 306.4f * anchorX;
            _gaugeHost.style.top = rootSize.y - GaugeSize * k - GaugeBottomMarginPx;
            _gaugeHost.style.width = GaugeSize * 2f + GaugeGap;
            _gaugeHost.style.height = GaugeSize;
            _gaugeHost.style.transformOrigin = new TransformOrigin(0f, 0f, 0f);
            _gaugeHost.style.scale = new StyleScale(new Scale(new Vector2(k, k)));
            ApplyGaugeScale(_hpRoot, 0f, 1f, 1f);
            ApplyGaugeScale(_stRoot, GaugeSize + GaugeGap, 1f, 1f);
            // [FigmaV3 진단] 값 변화 시에만 로그 — 스테일/겹침 판정용.
            if (Mathf.Abs(k - _lastLoggedK) > 0.001f)
            {
                _lastLoggedK = k;
                Debug.Log($"[FigmaV3] HUD gaugeHost left={_gaugeHost.style.left.value.value:F1} top={_gaugeHost.style.top.value.value:F1} w={_gaugeHost.style.width.value.value:F1} scale={k:F3} root={rootSize}");
            }
        }

        private float _lastLoggedK = -1f;

        private static void ApplyGaugeScale(VisualElement gauge, float left, float sx, float sy)
        {
            if (gauge == null) return;
            gauge.style.position = Position.Absolute;
            gauge.style.left = left * sx;
            gauge.style.top = 0f;
            gauge.style.width = GaugeSize * sx;
            gauge.style.height = GaugeSize * sy;
            var icon = gauge.Q<VisualElement>(gauge.name + "Icon");
            if (icon != null)
            {
                float iconInsetX = (GaugeSize - GaugeIconSize) * 0.5f;
                float iconInsetY = iconInsetX;
                icon.style.left = iconInsetX * sx;
                icon.style.top = iconInsetY * sy;
                icon.style.width = GaugeIconSize * sx;
                icon.style.height = GaugeIconSize * sy;
            }
        }

        // ===== 폴링 =====
        private void StartPolling()
        {
            if (_pollTask != null) return;
            _pollTask = schedule.Execute(() =>
            {
                if (parent != null) RefreshAll();
            }).Every(PollMs);
        }

        private void StopPolling()
        {
            if (_pollTask != null)
            {
                _pollTask.Pause();
                _pollTask = null;
            }
        }

        private void RefreshAll()
        {
            PositionHost();

            var ph = PlayerHealth.Instance;
            float hpRatio = ph != null && ph.MaxHP > 0f ? Mathf.Clamp01(ph.CurrentHP / ph.MaxHP) : 0f;

            var pm = Object.FindAnyObjectByType<ProjectName.Systems.PlayerMovement>();
            float stRatio = pm != null && pm.MaxStamina > 0f ? Mathf.Clamp01(pm.Stamina / pm.MaxStamina) : 0f;

            if (_hpGauge != null)
            {
                _hpGauge.Fraction = hpRatio;
                _hpGauge.CommitIfDirty();
            }
            if (_staminaGauge != null)
            {
                _staminaGauge.Fraction = stRatio;
                _staminaGauge.CommitIfDirty();
            }
        }

        // ===== 부착용 MonoKeeper =====
        private class MonoKeeper : MonoBehaviour
        {
            public HUDUTK hud;

            private void Update()
            {
                var root = UIToolkitBootstrap.UIRoot;
                if (root != null && hud != null && hud.parent == null)
                    root.Add(hud);
            }
        }
    }
}
