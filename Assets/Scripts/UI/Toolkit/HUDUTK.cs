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

        // [Figma 23:6] 캔버스 내 게이지 bounds와 아이콘 중심 크기
        private const float GaugeSize     = 137.6f; // Figma 23:6 gauge bounds
        private const float GaugeGap      = 24f;    // Figma gauge-to-gauge gap
        private const float GaugeIconSize = 68.8f;  // Figma center icon bounds

        // [Figma GitHub-dark + 사용자 설계] 링 색
        private readonly Color _hpColor = new Color(1f, 0.30f, 0.30f, 1f);       // 체력 링 — 레드빛 (하트)
        private readonly Color _stColor = new Color(1f, 0.83f, 0.30f, 1f);       // 스태미나 링 — 노란빛 (번개)

        private VisualElement    _gaugeHost;      // 핫바 옆 호스트
        private UTKCircularGauge _hpGauge;        // 하트 (체력)
        private UTKCircularGauge _staminaGauge;   // 번개 (스태미나)
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
            _hpGauge      = BuildOneGauge("HpGauge",      _heartTex, _hpColor);
            _staminaGauge = BuildOneGauge("StaminaGauge", _boltTex, _stColor);

            // 폴링 첫 틱 전 기본값 (가득 찬 오표시 방지)
            if (_hpGauge != null)      _hpGauge.Fraction = 0f;
            if (_staminaGauge != null) _staminaGauge.Fraction = 0f;
        }

        /// <summary>단일 원형 게이지 빌드 — 링 배경 + UTKCircularGauge 벡터 아크 + 중앙 아이콘.</summary>
        private UTKCircularGauge BuildOneGauge(string gaugeName, Texture2D iconTex, Color fill)
        {
            var gauge = new UTKCircularGauge();
            gauge.name = gaugeName;
            gauge.style.width = GaugeSize;
            gauge.style.height = GaugeSize;
            gauge.style.marginRight = GaugeGap;
            gauge.FillColor = fill;
            gauge.pickingMode = PickingMode.Ignore;
            if (_ringTex != null)
            {
                gauge.style.backgroundImage = UTKTextureSafe.ToBackground(_ringTex);
                gauge.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            }
            _gaugeHost.Add(gauge);

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

        /// <summary>핫바 왼쪽 옆에 배치 — 폴링 첫 틱에 panel width 실측으로 핫바(중앙) 좌측 여백 확보.
        ///  호스트 너비 = 2게이지 + 간격 (게이지는 오른쪽→왼쪽 순서로 추가됐으니 왼쪽정렬 위해 역방향 계산).</summary>
        private void PositionHost()
        {
            if (panel == null) return;
            // [Figma 정합 v3 / 화면 앵커 복귀(사용자 확정)] 루트=화면 스트레치 유지(예전 앵커),
            // 게이지 위치는 X/Y 축 분율 앵커(예전과 동일 배치) — 크기만 등배수 k로 통일(종횡비 왜곡 제거).
            var uiRoot = UIToolkitBootstrap.UIRoot;
            Vector2 rootSize = uiRoot != null
                ? new Vector2(uiRoot.resolvedStyle.width, uiRoot.resolvedStyle.height)
                : new Vector2(panel.visualTree.worldBound.width, panel.visualTree.worldBound.height);
            if (rootSize.x <= 0f || rootSize.y <= 0f) return;
            float anchorX = rootSize.x / FigmaCanvasLayout.CanvasWidth;
            float anchorY = rootSize.y / FigmaCanvasLayout.CanvasHeight;
            float k = Mathf.Min(anchorX, anchorY);
            if (k <= 0f) return;

            // 게이지 호스트 = 예전 화면 앵커(축 분율) + raw 크기 + 등비 scale
            _gaugeHost.style.left = 306.4f * anchorX;
            _gaugeHost.style.top = 855.2f * anchorY;
            _gaugeHost.style.width = GaugeSize * 2f + GaugeGap;
            _gaugeHost.style.height = GaugeSize;
            _gaugeHost.style.transformOrigin = new TransformOrigin(0f, 0f, 0f);
            _gaugeHost.style.scale = new StyleScale(new Scale(new Vector2(k, k)));
            ApplyGaugeScale(_hpGauge, 0f, 1f, 1f);
            ApplyGaugeScale(_staminaGauge, GaugeSize + GaugeGap, 1f, 1f);
        }

        private static void ApplyGaugeScale(VisualElement gauge, float left, float sx, float sy)
        {
            if (gauge == null) return;
            gauge.style.position = Position.Absolute;
            gauge.style.left = left * sx;
            gauge.style.top = 0f;
            gauge.style.width = GaugeSize * sx;
            gauge.style.height = GaugeSize * sy;
            var icon = gauge.Q<VisualElement>();
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
