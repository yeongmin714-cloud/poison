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
        // [2026-10-11 27차] arc = 베이크 스트립 게이지(UTKGaugeStripArc) — 벡터 메시의
        //   transform.scale/재생성 불안정(26차 실측)을 규약대로 베이크 PNG로 대체.
        private UTKGaugeStripArc _hpGauge;        // 하트 arc 레이어 (체력)
        private UTKGaugeStripArc _staminaGauge;   // 번개 arc 레이어 (스태미나)
        // [2026-10-11 26차 arc 수리] arc 레퍼런스 — 스택(스케일 서브트리)에서 호스트 직속으로 이동.
        private UTKGaugeStripArc _hpArc;
        private UTKGaugeStripArc _stArc;
        // [2026-10-11 28차] 하트 링 아래 남은/전체 체력 라벨(Fluent 수치 배지 — 사용자 제안 채택, 스타미나 제외 확정).
        private Label _hpValueLabel;
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

            // [2026-10-11 28차] 하트 링 아래 "67 / 100" 수치 라벨 — 게이지 열 왼쪽(하트) 아래만.
            _hpValueLabel = new Label("100 / 100");
            _hpValueLabel.name = "HpValueLabel";
            _hpValueLabel.style.position = Position.Absolute;
            _hpValueLabel.style.left = 0f;
            _hpValueLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _hpValueLabel.style.color = new StyleColor(new Color(0.941f, 0.965f, 0.988f, 1f)); // #F0F6FC 본문 흰색(31차 — 회색 보조색은 흐릿함)
            _hpValueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;   // [31차] 진하게 — 가시성
            _hpValueLabel.style.whiteSpace = WhiteSpace.NoWrap;
            _hpValueLabel.pickingMode = PickingMode.Ignore;
            _gaugeHost.Add(_hpValueLabel);

            // [2026-10-11 26차 arc 수리] arc(벡터 메시)를 스택에서 호스트 직속으로 이동 —
            //   transform.scale 아래의 generateVisualContent 메시가 좌표를 스케일에 맞춰 그리지 않아
            //   링 일부만 렌더되거나 어긋난다(테스트 15 영상 실측: 링이 하단 짧은 호만 표시).
            //   메시는 무스케일 서브트리에서 레이아웃 등비 크기로 스스로 그린다(resolvedStyle 기반).
            _hpArc = _hpGauge;
            _stArc = _staminaGauge;
            _hpArc.RemoveFromHierarchy();
            _stArc.RemoveFromHierarchy();
            _gaugeHost.Add(_hpArc);
            _gaugeHost.Add(_stArc);

            // 폴링 첫 틱 전 기본값 (가득 찬 오표시 방지)
            // [19차] 초기값 = 만원(가득 참) — 폴링 실패/시스템 부재와 무관하게 "빈 링=고장"으로 읽히지 않게.
            if (_hpGauge != null)      _hpGauge.Fraction = 1f;
            if (_staminaGauge != null) _staminaGauge.Fraction = 1f;
        }

        /// <summary>단일 원형 게이지 빌드 — [2026-10-10 Figma 23:6 폼 정합] 원본 스펙(figma-file.json) 계약:
        /// ①bg-radial-glow(264 방사형 페이드 — 게이지색 틴트, 그라데이션 미지원 판단을 절차 텍스처로 해소)
        /// ②outer-track(백색 저알파 도넛+스트로크 1.32 — 도넛 채움은 gauge 배경링(백색 0.08) 담당, 패널은 스트로크만)
        /// ③진행 arc(UTKCircularGauge Figma opt-in — 두께 0.2×r·외곽 밀착·2중 글로우·백색 tip)
        /// ⑤inner-plate(96.8 세로 그라데이션 0.8→0.4 + 스트로크 백색, Figma inner shadow 근사)
        /// ⑥rim-highlight(88.9 스트로크 — 각도 그라데이션 미지원 단색 유지, 판단 기록)
        /// ⑦center-light(68.8 백색 방사 광 — 신규, Figma 노드 복원)
        /// ⑧중앙 아이콘(68.8). 시맨틱 색은 게임 매핑 유지(HP 레드/스타미나 옐로 — 09-24 사용자 설계 계약).
        /// Figma 목업의 오렌지 그라데이션은 미채택. 호스트 transform scale=k가 등비 확대 담당 → 각 층 raw 고정 크기.</summary>
        private VisualElement BuildOneGauge(string gaugeName, Texture2D iconTex, Color fill, out UTKGaugeStripArc arcGauge)
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

            // ① bg-radial-glow — Figma 방사형 페이드(LAYER_BLUR 48) 근사: 절차 방사 텍스처 + 게이지색 틴트
            //   (과거 단색 저알파 원 → radial 그라데이션 미지원 판단을 텍스처 레벨로 해소)
            var glow = new VisualElement();
            glow.name = gaugeName + "BgGlow";
            glow.style.position = Position.Absolute;
            float glowSize = 264f;
            glow.style.left = (GaugeSize - glowSize) * 0.5f;
            glow.style.top = (GaugeSize - glowSize) * 0.5f;
            glow.style.width = glowSize;
            glow.style.height = glowSize;
            var radialTex = GetRadialFadeTexture();
            if (radialTex != null)
            {
                glow.style.backgroundImage = UTKTextureSafe.ToBackground(radialTex);
                glow.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                var glowTint = fill; glowTint.a = 0.30f;
                glow.style.unityBackgroundImageTintColor = new StyleColor(glowTint);
            }
            else
            {
                var glowColor = fill; glowColor.a = 0.10f;
                glow.style.backgroundColor = new StyleColor(glowColor);
                SetCircleRadius(glow);
            }
            glow.pickingMode = PickingMode.Ignore;
            gauge.Add(glow);

            // ② outer-track — Figma 도넛(0.8r..1.0r 백색 0.08)은 gauge 배경링이 담당하고,
            //   패널 스트로크(1.32 백색)만 여기 유지 — 도넛과 스트로크 이중 표현 정합.
            var track = new VisualElement();
            track.name = gaugeName + "OuterTrack";
            track.style.position = Position.Absolute;
            track.style.left = 0f;
            track.style.top = 0f;
            track.style.width = GaugeSize;
            track.style.height = GaugeSize;
            track.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0f));
            track.style.borderTopWidth = track.style.borderBottomWidth = 1.32f;
            track.style.borderLeftWidth = track.style.borderRightWidth = 1.32f;
            var trackStroke = new StyleColor(new Color(1f, 1f, 1f, 0.28f));
            track.style.borderTopColor = track.style.borderBottomColor = trackStroke;
            track.style.borderLeftColor = track.style.borderRightColor = trackStroke;
            SetCircleRadius(track);
            track.pickingMode = PickingMode.Ignore;
            gauge.Add(track);

            // ③ 진행 arc — [2026-10-11 27차] 베이크 스트립(UTKGaugeArcStrip 65프레임) —
            //   시계방향 소모 프레임 굽힘 + 흰 코어에 틴트(fill)로 HP 레드/스타미나 옐로.
            //   Figma donut 스펙(0.2×r 밴드·tip)은 스트립 프레임에 반영. 트랙 링은 스택의
            //   HudGaugeRing 텍스처/외곽 스트로크 층이 담당(기존 유지).
            arcGauge = new UTKGaugeStripArc();
            arcGauge.DrainClockwise = true;
            arcGauge.name = gaugeName + "Arc";
            arcGauge.style.position = Position.Absolute;
            arcGauge.style.left = 0f;
            arcGauge.style.top = 0f;
            arcGauge.style.width = GaugeSize;
            arcGauge.style.height = GaugeSize;
            arcGauge.style.unityBackgroundImageTintColor = new StyleColor(fill);
            arcGauge.pickingMode = PickingMode.Ignore;
            // [2026-10-11 26차 arc 수리] 스택에 넣지 않는다 — 호출부가 호스트 직속으로 이동
            // (메시 무스케일 서브트리 계약: transform.scale 아래 벡터 메시 어긋남 방지).

            // ⑤ inner-plate — 세로 그라데이션 원판(Figma stops 0.8→0.4) + 스트로크 백색(Figma stroke white INSIDE 1.32)
            var plate = new VisualElement();
            plate.name = gaugeName + "InnerPlate";
            plate.style.position = Position.Absolute;
            float plateSize = 96.8f;
            float plateOff = (GaugeSize - plateSize) * 0.5f;
            plate.style.left = plateOff;
            plate.style.top = plateOff;
            plate.style.width = plateSize;
            plate.style.height = plateSize;
            var plateGrad = GetPlateGradientTexture();
            if (plateGrad != null)
            {
                plate.style.backgroundImage = UTKTextureSafe.ToBackground(plateGrad);
                plate.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            }
            else
            {
                plate.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.85f));
            }
            plate.style.borderTopWidth = plate.style.borderBottomWidth = 1.32f;
            plate.style.borderLeftWidth = plate.style.borderRightWidth = 1.32f;
            var plateStroke = new StyleColor(new Color(1f, 1f, 1f, 0.92f));
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

            // ⑦ center-light — 아이콘 뒤 백색 방사 광(Figma GRADIENT_RADIAL 노드 복원, 68.8)
            var centerLight = new VisualElement();
            centerLight.name = gaugeName + "CenterLight";
            centerLight.style.position = Position.Absolute;
            float lightOff = (GaugeSize - GaugeIconSize) * 0.5f;
            centerLight.style.left = lightOff;
            centerLight.style.top = lightOff;
            centerLight.style.width = GaugeIconSize;
            centerLight.style.height = GaugeIconSize;
            if (radialTex != null)
            {
                centerLight.style.backgroundImage = UTKTextureSafe.ToBackground(radialTex);
                centerLight.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                centerLight.style.unityBackgroundImageTintColor = new StyleColor(new Color(1f, 1f, 1f, 0.62f));
            }
            else
            {
                centerLight.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.30f));
                SetCircleRadius(centerLight);
            }
            centerLight.pickingMode = PickingMode.Ignore;
            gauge.Add(centerLight);

            // ⑧ 중앙 아이콘 — 기존 68.8@34.4 계약 유지
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

        // ===== 절차 그라데이션 텍스처 — UTK radial/linear 미지원 한계를 텍스처 레벨로 해소 =====
        // [2026-10-10 Figma 23:6 폼 정합] bg-radial-glow / center-light / inner-plate 공용.
        private static Texture2D _radialFadeTex;
        private static Texture2D _plateGradientTex;

        /// <summary>백색 방사 페이드(64×64, 중심 불투명 → 가장자리 0, 지수 감쇠) — 게이지색 틴트로 조합.</summary>
        private static Texture2D GetRadialFadeTexture()
        {
            if (_radialFadeTex != null) return _radialFadeTex;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "UTK_GaugeRadialFade" };
            var pixels = new Color[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float a = Mathf.Pow(1f - d, 1.8f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            _radialFadeTex = tex;
            return tex;
        }

        /// <summary>inner-plate 세로 그라데이션(64×64 정방 — 위 0.8 → 아래 0.4, Figma GRADIENT_LINEAR stops 근사).
        /// [2026-10-10 수리] 구 4×64 램프는 ScaleToFit이 종횡비를 유지해 원판 중앙에 6px 세로 스트립만
        ///   렌더링됐다(아이콘 관통 세로줄의 근본 원인) — 정방 텍스처로 원판 전체를 채운다.</summary>
        private static Texture2D GetPlateGradientTexture()
        {
            if (_plateGradientTex != null) return _plateGradientTex;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "UTK_GaugePlateGradient" };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float t = (float)y / (size - 1);                // 0=아래, 1=위
                float a = Mathf.Lerp(0.40f, 0.80f, t);
                var c = new Color(1f, 1f, 1f, a);
                for (int x = 0; x < size; x++) pixels[y * size + x] = c;
            }
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            _plateGradientTex = tex;
            return tex;
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
            // [2026-10-11 26차 arc 수리] 호스트 transform.scale 폐지 → 레이아웃 등비 확대.
            //   배경이미지 층(스택)은 스택 루트의 transform.scale이 담당(이미지는 스케일 정상),
            //   벡터 메시(arc)는 무스케일 서브트리에서 레이아웃 크기로 스스로 계산.
            // [2026-10-11 28차→30차] 호스트 높이에 라벨 층 추가(하단 24px 여백 안에 20×k = 15px 수납).
            _gaugeHost.style.width = (GaugeSize * 2f + GaugeGap) * k;
            _gaugeHost.style.height = GaugeSize * k + 20f * k;
            ScaleStack(_hpRoot, 0f, k);
            ScaleStack(_stRoot, GaugeSize + GaugeGap, k);
            PositionArc(_hpArc, 0f, k);
            PositionArc(_stArc, GaugeSize + GaugeGap, k);
            if (_hpValueLabel != null)
            {
                _hpValueLabel.style.top = GaugeSize * k;
                _hpValueLabel.style.width = GaugeSize * k;   // 하트 게이지 열 아래만(스타미나 제외)
                _hpValueLabel.style.height = 20f * k;
                _hpValueLabel.style.fontSize = 22f * k;      // [2026-10-11 30차] 사용자 요구 — 기존 11×k의 2배
            }
            // [FigmaV3 진단] 값 변화 시에만 로그 — 스테일/겹침 판정용.
            if (Mathf.Abs(k - _lastLoggedK) > 0.001f)
            {
                _lastLoggedK = k;
                Debug.Log($"[FigmaV3] HUD gaugeHost left={_gaugeHost.style.left.value.value:F1} top={_gaugeHost.style.top.value.value:F1} w={_gaugeHost.style.width.value.value:F1} scale={k:F3} root={rootSize}");
            }
        }

        private float _lastLoggedK = -1f;

        /// <summary>스택 루트 배치 — 레이아웃 위치 등비 + transform.scale=k(이미지 7층 등비 확대 담당).
        /// [2026-10-11 26차] 기존 ApplyGaugeScale(sx=1+호스트 스케일)을 대체.</summary>
        private void ScaleStack(VisualElement gauge, float slotLeft, float k)
        {
            if (gauge == null) return;
            gauge.style.position = Position.Absolute;
            gauge.style.left = slotLeft * k;
            gauge.style.top = 0f;
            gauge.style.width = GaugeSize;
            gauge.style.height = GaugeSize;
            gauge.style.marginRight = 0f;
            gauge.style.transformOrigin = new TransformOrigin(0f, 0f, 0f);
            gauge.style.scale = new StyleScale(new Scale(new Vector2(k, k)));
        }

        /// <summary>arc(벡터 메시) 배치 — 무스케일 서브트리: 레이아웃 크기를 등비로 두고
        /// 메시는 resolvedStyle로 스스로 계산(조상 transform.scale 의존 제거).
        /// [2026-10-11 26차 신설].</summary>
        private void PositionArc(UTKGaugeStripArc arc, float slotLeft, float k)
        {
            if (arc == null) return;
            arc.style.position = Position.Absolute;
            arc.style.left = slotLeft * k;
            arc.style.top = 0f;
            arc.style.width = GaugeSize * k;
            arc.style.height = GaugeSize * k;
            arc.Refresh();   // 크기 변경 → 슬라이싱 재계산(리사이즈 안전)
        }

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

        private static bool _hpMissingLogged;
        private static bool _stMissingLogged;

        private void RefreshAll()
        {
            // [19차 수리] 게이지 데이터 갱신을 PositionHost보다 먼저 — 배치 계산 예외가
            //   데이터 갱신을 막지 않게(순서 교체).

            // [18차 수리] Instance 부재/해제(fake-null) 폴백 — 컴포넌트 직접 탐색(확립 패턴).
            var ph = PlayerHealth.Instance;
            if (ph == null) ph = Object.FindAnyObjectByType<PlayerHealth>();
            float hpRatio;
            if (ph != null && ph.MaxHP > 0f)
                hpRatio = Mathf.Clamp01(ph.CurrentHP / ph.MaxHP);
            else
            {
                hpRatio = 1f;   // 시스템 부재 씬 — 만원 기본 표시(빈 링은 고장처럼 읽힘)
                if (!_hpMissingLogged)
                {
                    _hpMissingLogged = true;
                    Debug.LogWarning("[HUDUTK] PlayerHealth 부재 — 체력 게이지 만원 기본 표시(씬 플레이어 셋업 확인 요망)");
                }
            }

            var pm = Object.FindAnyObjectByType<ProjectName.Systems.PlayerMovement>();
            float stRatio;
            if (pm != null && pm.MaxStamina > 0f)
                stRatio = Mathf.Clamp01(pm.Stamina / pm.MaxStamina);
            else
            {
                stRatio = 1f;   // 시스템 부재 씬 — 만원 기본 표시
                if (!_stMissingLogged)
                {
                    _stMissingLogged = true;
                    Debug.LogWarning("[HUDUTK] PlayerMovement 부재 — 스태미나 게이지 만원 기본 표시");
                }
            }

            if (_hpGauge != null)
            {
                _hpGauge.Fraction = hpRatio;
                _hpGauge.CommitIfDirty();
            }
            // [2026-10-11 28차] 하트 링 아래 수치 라벨 — 남은/전체 체력(시스템 부재 시 만원 기준).
            if (_hpValueLabel != null)
            {
                float max = ph != null && ph.MaxHP > 0f ? ph.MaxHP : 100f;
                float cur = ph != null && ph.MaxHP > 0f ? ph.CurrentHP : max;
                _hpValueLabel.text = $"{Mathf.RoundToInt(cur):0} / {Mathf.RoundToInt(max):0}";
            }
            if (_staminaGauge != null)
            {
                _staminaGauge.Fraction = stRatio;
                _staminaGauge.CommitIfDirty();
            }

            // [20차 수리 — 게이지 미표시 근본 원인] 미커밋 편집(18·19차 RefreshAll 재구성)에서
            //   PositionHost() 호출이 통째로 삭제됐다(HEAD c10ad649~에는 RefreshAll 첫 줄에 존재).
            //   → 호스트가 Figma raw 좌표 top=855.2에 고정되는데 PanelSettings 1440×900(match width)
            //   패널 높이는 810 → 게이지가 화면 밖(데이터·렌더 모두 정상, 그릴 위치만 없었음).
            //   본 코멘트의 원래 의도대로 데이터 갱신 "뒤"에 배치를 실행 — 250ms 폴링마다 재앵커
            //   (리사이즈 대응 포함). 좌표 계산 예외가 데이터 갱신을 막지 않게 순서도 유지.
            PositionHost();
        }

        // ===== 폴링(직접 틱) =====
        private static bool _polledOnce;
        private static float _diagTimer;

        /// <summary>[17차 게이지 수리] MonoKeeper 직접 폴링 — 스케줄러(부착 전 생성) 미실행 환경 대비
        ///   이중 보험. 부착 상태에서만 RefreshAll을 수행하고, 첫 1회는 진단 로그를 남긴다.
        ///   [19차] 2s마다 게이지 실측 진단(값/부착/데이터 원천) — 채움 공백 원인을 Play에서 즉시 판별.</summary>
        public void PollTick()
        {
            if (panel == null) return;
            RefreshAll();
            if (!_polledOnce)
            {
                _polledOnce = true;
                var ph = PlayerHealth.Instance ?? Object.FindAnyObjectByType<PlayerHealth>();
                Debug.Log($"[HUDUTK] 첫 폴링 — PlayerHealth={(ph != null)}, hpRatio={(ph != null ? ph.HPRatio : 1f)}(부재 시 만원 기본)");
            }
            _diagTimer += 0.25f;
            if (_diagTimer >= 2f)
            {
                _diagTimer = 0f;
                var ph = PlayerHealth.Instance ?? Object.FindAnyObjectByType<PlayerHealth>();
                float hpF = _hpGauge != null ? _hpGauge.Fraction : -1f;
                float stF = _staminaGauge != null ? _staminaGauge.Fraction : -1f;
                // [20-2차] 렌더 판정 보강 — 호스트 실제 화면 rect + 표시 상태. attached=True인데도
                //   미표시 보고가 이어지면 오프스크린/None 표시/겹침을 한 줄로 판별한다.
                var hb = _gaugeHost != null ? _gaugeHost.worldBound : Rect.zero;
                var panelRect = panel.visualTree.worldBound;
                bool onScreen = _gaugeHost != null && hb.width > 0f && panelRect.width > 0f
                                && hb.yMin < panelRect.yMax && hb.yMax > panelRect.yMin
                                && hb.xMin < panelRect.xMax && hb.xMax > panelRect.xMin;
                Debug.Log($"[HUDUTK] 진단 — hpFraction={hpF:0.00} stFraction={stF:0.00} ph={(ph != null)} hpRatio={(ph != null ? ph.HPRatio : -1f)} attached={(parent != null)} hostRect=({hb.xMin:F0},{hb.yMin:F0})-({hb.xMax:F0},{hb.yMax:F0}) panel=({panelRect.width:F0}x{panelRect.height:F0}) onScreen={onScreen}");
            }
        }

        // ===== 부착용 MonoKeeper =====
        private class MonoKeeper : MonoBehaviour
        {
            public HUDUTK hud;
            private float _pollAccum;

            private void Update()
            {
                var root = UIToolkitBootstrap.UIRoot;
                if (root != null && hud != null && hud.parent == null)
                    root.Add(hud);

                // [17차 게이지 수리] 0.25s 직접 폴링 — HP/스태미나 게이지가 0으로 고정되던 공백 수리
                _pollAccum += Time.deltaTime;
                if (_pollAccum >= 0.25f)
                {
                    _pollAccum = 0f;
                    if (hud != null) hud.PollTick();
                }
            }
        }
    }
}
