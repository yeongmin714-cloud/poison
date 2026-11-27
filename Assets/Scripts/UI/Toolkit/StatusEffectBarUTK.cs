using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;   // PlayerStatusEffectSystem/StatusEffectId — [63-B] 상태이상 단일 소스

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// [63-B] 상태이상 표시 바 — 현재 시간 UI(TimeClockGlassUTK) 바로 아래.
    /// 계획서: docs/2026-11-16_STATUS_EFFECTS_14_PLAN.md §3.
    ///
    /// 형태: 체력바/스태미너 바와 동일 계열의 동그란 아크 게이지(UTKGaugeStripArc — HP/스태미너 선례,
    /// 84차 벡터메시 금지 계약 준수, 시계방향 소모 내장) + 가운데 절차 글리프(SleepPanelUTK BuildXxxGlyph 선례).
    ///  - 1개면 1개, 여러 개면 여백 6px 고정 가로 배열(사용자 계약).
    ///  - 원형 바 = 남은 시간 비율(remaining/duration) — 시계 방향대로 줄어든다(스트립 내장 규약).
    ///  - 커서 호버 시 남은 시간 라벨 표시("골절 — 37초").
    ///  - 해제: 시간 경과 자동(시스템 TickCore) 또는 수면/붕대/해독제 등 치료 경로(시스템 Cure*).
    /// 데이터 소스: PlayerStatusEffectSystem(단일 소스) — UI는 표시 전용(상태 변경 로직 없음).
    /// </summary>
    public class StatusEffectBarUTK : VisualElement
    {
        // ── 배치 진실치 ──
        private const float BarLeft = 14f;   // 시계(TimeClockGlassUTK left=14)와 동일 열 정렬
        private const float BarTop = 96f;    // 시계 하단(top 12 + height GlyphHeight46+30 = 88) + 8px 간격
        private const float IconSize = 44f;  // HP/스태미너 게이지(137.6)보다 작은 동일 계열 원형
        private const float IconGap = 6f;    // [63-B] 아이콘 간 여백 고정(사용자 계약 — 일정하게)
        private const long PollMs = 250;     // HUD 폴링 선례(RestaurantWindowUTK PollRefresh) — IVisualElementScheduler는 long(ms)

        private static StatusEffectBarUTK _instance;

        private readonly VisualElement _row;
        private readonly Label _tooltip;
        private readonly Dictionary<StatusEffectId, VisualElement> _icons = new Dictionary<StatusEffectId, VisualElement>();
        private readonly Dictionary<StatusEffectId, UTKGaugeStripArc> _arcs = new Dictionary<StatusEffectId, UTKGaugeStripArc>();
        private StatusEffectId _tooltipTarget;
        private bool _tooltipVisible;
        private bool _systemSubscribed;

        private StatusEffectBarUTK()
        {
            name = "StatusEffectBar";
            style.position = Position.Absolute;
            style.left = BarLeft;
            style.top = BarTop;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.FlexStart;
            style.paddingLeft = 0f;
            style.paddingTop = 0f;
            // [P23 규약 준수] HUD 앵커 요소는 클릭 흡수 금지 — 개별 아이콘 호스트만 Position(호버용).
            pickingMode = PickingMode.Ignore;

            _row = new VisualElement();
            _row.name = "StatusEffectRow";
            _row.style.flexDirection = FlexDirection.Row;
            _row.style.alignItems = Align.Center;
            _row.pickingMode = PickingMode.Ignore;
            Add(_row);

            // 호버 툴팁 — 아이콘 위 표시(이름 + 남은 초). 커서 진입 시에만 가시.
            _tooltip = new Label("");
            _tooltip.name = "StatusTooltip";
            _tooltip.style.position = Position.Absolute;
            _tooltip.style.top = -40f;
            _tooltip.style.maxWidth = 240f;
            _tooltip.style.backgroundColor = new StyleColor(new Color(0x13 / 255f, 0x18 / 255f, 0x1F / 255f, 0.92f)); // #13181F/92%
            _tooltip.style.borderTopLeftRadius = _tooltip.style.borderTopRightRadius = 5f;
            _tooltip.style.borderBottomLeftRadius = _tooltip.style.borderBottomRightRadius = 5f;
            _tooltip.style.paddingLeft = 6f;
            _tooltip.style.paddingRight = 6f;
            _tooltip.style.fontSize = 12f;
            _tooltip.style.color = new StyleColor(new Color(0.941f, 0.965f, 0.988f, 1f)); // #F0F6FC
            _tooltip.style.unityTextAlign = TextAnchor.MiddleCenter;
            _tooltip.style.whiteSpace = WhiteSpace.Normal;
            _tooltip.style.display = DisplayStyle.None;
            _tooltip.pickingMode = PickingMode.Ignore;
            Add(_tooltip);

            RegisterCallback<AttachToPanelEvent>(OnAttached);
            RegisterCallback<DetachFromPanelEvent>(OnDetached);

            UTKWindowBase.ApplyUIToolkitFont(this);   // 한글 폰트 — 툴팁 라벨
        }

        // ─────────────────────── 자가부트(멱등) ───────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => Ensure();

        /// <summary>멱등 부트 — UIRoot에 부착. UTKWireUp.Wire()에서도 호출(중복 무해).</summary>
        public static void Ensure()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;   // UIRoot 미준비 — 조용히 스킵(UTK 창 선례)
            if (_instance != null && _instance.panel != null) return;   // 이미 부착

            if (_instance != null) _instance.RemoveFromHierarchy();   // 분리 잔재 재부착
            else _instance = new StatusEffectBarUTK();
            root.Add(_instance);
            _instance.style.display = DisplayStyle.Flex;
            Debug.Log("[StatusEffectBar] 상태이상 바 부착 — 시계 하단");
        }

        private void OnAttached(AttachToPanelEvent evt)
        {
            if (!_systemSubscribed)
            {
                PlayerStatusEffectSystem.OnChanged += OnSystemChanged;
                _systemSubscribed = true;
            }
            // 폴링 — HUD 폴링 선례(RestaurantWindowUTK). OnChanged 외에도 250ms 주기 안전망.
            schedule.Execute(Refresh).Every(PollMs).StartingIn(PollMs);
            Refresh();
        }

        private void OnDetached(DetachFromPanelEvent evt)
        {
            if (_systemSubscribed)
            {
                PlayerStatusEffectSystem.OnChanged -= OnSystemChanged;
                _systemSubscribed = false;
            }
            HideTooltip();
        }

        private void OnSystemChanged() => Refresh();   // 즉시 갱신(폴링 보조)

        // ─────────────────────── 표시명/색 ───────────────────────

        /// <summary>상태별 표시명(툴팁용) — 한국어 위조 금지 규약상 게임 문구.</summary>
        internal static string DisplayName(StatusEffectId id)
        {
            switch (id)
            {
                case StatusEffectId.Bleeding: return "출혈";
                case StatusEffectId.Poisoning: return "중독";
                case StatusEffectId.Slowness: return "둔화";
                case StatusEffectId.Exhaustion: return "탈진";
                case StatusEffectId.Fracture: return "골절";
                case StatusEffectId.Daze: return "혼탁";
                case StatusEffectId.Hunger: return "허기";
                case StatusEffectId.Burn: return "화상";
                case StatusEffectId.Hypothermia: return "저체온";
                case StatusEffectId.Terror: return "공포";
                case StatusEffectId.Infection: return "감염";
                case StatusEffectId.Weaken: return "약화";
                case StatusEffectId.Drunkenness: return "취기";
                case StatusEffectId.BrittleDefense: return "방어 취약";
                default: return id.ToString();
            }
        }

        /// <summary>[63-C] 상태별 해결 방법 문구(툴팁 2행) — 실제 구현된 치료 계약 기준(위조 금지).</summary>
        internal static string CureHint(StatusEffectId id)
        {
            switch (id)
            {
                case StatusEffectId.Bleeding: return "해제 · 붕대 · 시간 경과";
                case StatusEffectId.Poisoning: return "해제 · 해독제 · 시간 경과";
                case StatusEffectId.Slowness: return "해제 · 시간 경과";
                case StatusEffectId.Exhaustion: return "해제 · 수면 · 시간 경과";
                case StatusEffectId.Fracture: return "해제 · 부목(붕대) · 수면 · 시간 경과";
                case StatusEffectId.Daze: return "해제 · 시간 경과 (마스크 착용 시 면역)";
                case StatusEffectId.Hunger: return "해제 · 식사 (시간 경과로는 미해제 — 갱신형)";
                case StatusEffectId.Burn: return "해제 · 물 접촉(수영) · 시간 경과";
                case StatusEffectId.Hypothermia: return "해제 · 실내·모닥불 · 수면 · 시간 경과";
                case StatusEffectId.Terror: return "해제 · 진정제 · 이탈 · 시간 경과";
                case StatusEffectId.Infection: return "해제 · 붕대 · 수면 · 시간 경과";
                case StatusEffectId.Weaken: return "해제 · 시간 경과";
                case StatusEffectId.Drunkenness: return "해제 · 시간 경과";
                case StatusEffectId.BrittleDefense: return "해제 · 시간 경과";
                default: return "해제 · 시간 경과";
            }
        }

        /// <summary>상태별 아크 색 — 계획서 §3 매핑(#RRGGBB).</summary>
        internal static Color EffectColor(StatusEffectId id)
        {
            switch (id)
            {
                case StatusEffectId.Bleeding: return new Color(0.753f, 0.224f, 0.169f); // #C0392B
                case StatusEffectId.Poisoning: return new Color(0.153f, 0.682f, 0.376f); // #27AE60
                case StatusEffectId.Slowness: return new Color(0.498f, 0.549f, 0.553f); // #7F8C8D
                case StatusEffectId.Exhaustion: return new Color(0.161f, 0.502f, 0.725f); // #2980B9
                case StatusEffectId.Fracture: return new Color(0.725f, 0.361f, 0.314f); // #B95C50
                case StatusEffectId.Daze: return new Color(0.557f, 0.604f, 0.651f); // #8E9AA6
                case StatusEffectId.Hunger: return new Color(0.902f, 0.494f, 0.133f); // #E67E22
                case StatusEffectId.Burn: return new Color(0.906f, 0.298f, 0.235f); // #E74C3C
                case StatusEffectId.Hypothermia: return new Color(0.365f, 0.678f, 0.886f); // #5DADE2
                case StatusEffectId.Terror: return new Color(0.608f, 0.349f, 0.714f); // #9B59B6
                case StatusEffectId.Infection: return new Color(0.420f, 0.557f, 0.137f); // #6B8E23
                case StatusEffectId.Weaken: return new Color(0.831f, 0.675f, 0.051f); // #D4AC0D
                case StatusEffectId.Drunkenness: return new Color(0.945f, 0.769f, 0.059f); // #F1C40F
                case StatusEffectId.BrittleDefense: return new Color(0.584f, 0.647f, 0.647f); // #95A5A6
                default: return Color.white;
            }
        }

        // ─────────────────────── 갱신 ───────────────────────

        private void Refresh()
        {
            var active = PlayerStatusEffectSystem.GetActiveEffects();
            var seen = new HashSet<StatusEffectId>(active);

            // 제거 — 만료/치료된 상태의 아이콘 철거(재배치는 Row 플렉스가 자동).
            var stale = new List<StatusEffectId>();
            foreach (var kv in _icons)
                if (!seen.Contains(kv.Key))
                    stale.Add(kv.Key);
            foreach (var id in stale)
            {
                _icons[id].RemoveFromHierarchy();
                _icons.Remove(id);
                _arcs.Remove(id);
            }

            // 추가 — 신규 활성 상태의 아이콘 생성(추가 순 유지).
            foreach (var id in active)
                if (!_icons.ContainsKey(id))
                    _icons[id] = BuildIcon(id);

            // 남은 시간 → 아크(시계방향 소모). 개별 원형 바가 초침처럼 줄어든다.
            foreach (var id in active)
            {
                float dur = PlayerStatusEffectSystem.GetDurationSeconds(id);
                float rem = PlayerStatusEffectSystem.GetRemainingSeconds(id);
                if (_arcs.TryGetValue(id, out var arc))
                {
                    arc.Fraction = dur > 0f ? Mathf.Clamp01(rem / dur) : 0f;
                    arc.CommitIfDirty();
                }
            }

            // 툴팁 추적 갱신 — 표시 중인 상태가 남아 있으면 남은 초 실시간.
            if (_tooltipVisible)
                UpdateTooltipText(_tooltipTarget);

            _row.style.display = active.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>아이콘 1개 구성 — 호스트(호버용) + 배경 링 + 아크(남은 시간) + 중앙 글리프.</summary>
        private VisualElement BuildIcon(StatusEffectId id)
        {
            var host = new VisualElement();
            host.name = $"StatusIcon_{id}";
            host.style.width = IconSize;
            host.style.height = IconSize;
            host.style.marginRight = IconGap;   // 여백 고정 — 개수 무관 일정 간격
            host.pickingMode = PickingMode.Position;   // 호버 전용 — 클릭 흡수는 피하되 이벤트 수신(P23 컨테이너는 Ignore 유지)

            // 배경 링 — HUD와 동일 텍스처(UI/HudGaugeRing). 부재 시 생략(아크만으로 식별 가능).
            var ringTex = Resources.Load<Texture2D>("UI/HudGaugeRing");
            if (ringTex != null)
            {
                var ring = new VisualElement();
                ring.name = "StatusRing";
                ring.style.position = Position.Absolute;
                ring.style.left = 0f; ring.style.top = 0f;
                ring.style.width = IconSize; ring.style.height = IconSize;
                ring.style.backgroundImage = UTKTextureSafe.ToBackground(ringTex);
                ring.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
                ring.style.unityBackgroundImageTintColor = new StyleColor(new Color(0.08f, 0.10f, 0.12f, 0.85f)); // 어두운 배경 링
                ring.pickingMode = PickingMode.Ignore;
                host.Add(ring);
            }

            // 아크 — 남은 시간(시계방향 소모 내장). 색 = 상태별 고정색.
            var arc = new UTKGaugeStripArc();
            arc.name = $"StatusArc_{id}";
            arc.style.position = Position.Absolute;
            arc.style.left = 0f; arc.style.top = 0f;
            arc.style.width = IconSize;
            arc.style.height = IconSize;
            var c = EffectColor(id);
            arc.style.unityBackgroundImageTintColor = new StyleColor(new Color(c.r, c.g, c.b, 0.95f));
            arc.Fraction = 1f;
            arc.CommitIfDirty();
            host.Add(arc);
            _arcs[id] = arc;

            // 중앙 글리프 — 절차 조합(20×20 중앙).
            var glyph = BuildGlyph(id);
            glyph.style.position = Position.Absolute;
            glyph.style.left = (IconSize - 20f) * 0.5f;
            glyph.style.top = (IconSize - 20f) * 0.5f;
            host.Add(glyph);

            // 호버 — 남은 시간 표시(사용자 계약).
            var captured = id;
            host.RegisterCallback<MouseEnterEvent>(_ => ShowTooltip(captured));
            host.RegisterCallback<MouseLeaveEvent>(_ => HideTooltip());

            _row.Add(host);
            return host;
        }

        // ─────────────────────── 호버 툴팁 ───────────────────────

        private void ShowTooltip(StatusEffectId id)
        {
            _tooltipTarget = id;
            _tooltipVisible = true;
            // 아이콘 x = 인덱스 × (크기+여백) — 활성 순서 기준(IReadOnlyList에 IndexOf 부재 — 루프 탐색).
            var active = PlayerStatusEffectSystem.GetActiveEffects();
            int idx = 0;
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i].Equals(id)) { idx = i; break; }
            }
            _tooltip.style.left = idx * (IconSize + IconGap);
            UpdateTooltipText(id);
            _tooltip.style.display = DisplayStyle.Flex;
        }

        private void HideTooltip()
        {
            _tooltipVisible = false;
            _tooltip.style.display = DisplayStyle.None;
        }

        private void UpdateTooltipText(StatusEffectId id)
        {
            float rem = PlayerStatusEffectSystem.GetRemainingSeconds(id);
            // [63-C] 툴팁 2행 계약 — 1행: 이름 + 남은 시간, 2행: 해결 방법(CureHint).
            _tooltip.text = $"{DisplayName(id)} — {Mathf.CeilToInt(rem):0}초\n{CureHint(id)}";
        }

        // ─────────────────────── 절차 글리프(14종) ───────────────────────
        // 규약: 벡터메시 금지(84차) — rect/circle/border 조합만(SleepPanelUTK BuildXxxGlyph 선례).
        // 20×20 영역, 식별 가능 수준. 색 = EffectColor(id).

        private static VisualElement GlyphHost()
        {
            var host = new VisualElement();
            host.style.width = 20f; host.style.height = 20f;
            host.pickingMode = PickingMode.Ignore;
            return host;
        }

        private static VisualElement Rect(float x, float y, float w, float h, Color c, float angleDeg = 0f, float radius = 0f)
        {
            var ve = new VisualElement();
            ve.style.position = Position.Absolute;
            ve.style.left = x; ve.style.top = y;
            ve.style.width = w; ve.style.height = h;
            ve.style.backgroundColor = new StyleColor(c);
            if (angleDeg != 0f) ve.style.rotate = new Rotate(new Angle(angleDeg, AngleUnit.Degree));
            if (radius > 0f)
            {
                ve.style.borderTopLeftRadius = ve.style.borderTopRightRadius = radius;
                ve.style.borderBottomLeftRadius = ve.style.borderBottomRightRadius = radius;
            }
            ve.pickingMode = PickingMode.Ignore;
            return ve;
        }

        private static VisualElement Circle(float x, float y, float d, Color c)
        {
            var ve = Rect(x, y, d, d, c);
            ve.style.borderTopLeftRadius = ve.style.borderTopRightRadius = d * 0.5f;
            ve.style.borderBottomLeftRadius = ve.style.borderBottomRightRadius = d * 0.5f;
            return ve;
        }

        private static VisualElement BuildGlyph(StatusEffectId id)
        {
            var c = EffectColor(id);
            switch (id)
            {
                case StatusEffectId.Bleeding: return BuildBleedingGlyph(c);
                case StatusEffectId.Poisoning: return BuildPoisoningGlyph(c);
                case StatusEffectId.Slowness: return BuildSlownessGlyph(c);
                case StatusEffectId.Exhaustion: return BuildExhaustionGlyph(c);
                case StatusEffectId.Fracture: return BuildFractureGlyph(c);
                case StatusEffectId.Daze: return BuildDazeGlyph(c);
                case StatusEffectId.Hunger: return BuildHungerGlyph(c);
                case StatusEffectId.Burn: return BuildBurnGlyph(c);
                case StatusEffectId.Hypothermia: return BuildHypothermiaGlyph(c);
                case StatusEffectId.Terror: return BuildTerrorGlyph(c);
                case StatusEffectId.Infection: return BuildInfectionGlyph(c);
                case StatusEffectId.Weaken: return BuildWeakenGlyph(c);
                case StatusEffectId.Drunkenness: return BuildDrunkennessGlyph(c);
                case StatusEffectId.BrittleDefense: return BuildBrittleDefenseGlyph(c);
                default: return GlyphHost();
            }
        }

        /// <summary>출혈 — 물방울(상단 원 + 하단 45° 사각 뾰족).</summary>
        private static VisualElement BuildBleedingGlyph(Color c)
        {
            var host = GlyphHost();
            host.Add(Circle(6f, 2f, 8f, c));                 // 머리
            host.Add(Rect(6.5f, 8f, 7f, 7f, c, 45f, 2f));    // 뾰족 아래(회전 사각)
            return host;
        }

        /// <summary>중독 — 두개(원 + 안구 2점 + 턱).</summary>
        private static VisualElement BuildPoisoningGlyph(Color c)
        {
            var host = GlyphHost();
            host.Add(Circle(5f, 2f, 10f, c));                                    // 두상
            host.Add(Rect(7.5f, 6f, 2f, 2f, new Color(0.08f, 0.10f, 0.12f)));    // 안구 L
            host.Add(Rect(10.5f, 6f, 2f, 2f, new Color(0.08f, 0.10f, 0.12f)));   // 안구 R
            host.Add(Rect(7.5f, 12.5f, 5f, 3f, c, 0f, 1f));                      // 턱
            return host;
        }

        /// <summary>둔화 — 달팽이(대각 선 + 소라 원).</summary>
        private static VisualElement BuildSlownessGlyph(Color c)
        {
            var host = GlyphHost();
            host.Add(Rect(3f, 15f, 14f, 2f, c, -25f));   // 기어간 몸통
            host.Add(Circle(3f, 4f, 9f, c));             // 소라
            return host;
        }

        /// <summary>탈진 — 번개(다이아 + 꼬리 — SleepPanelUTK zap 선례).</summary>
        private static VisualElement BuildExhaustionGlyph(Color c)
        {
            var host = GlyphHost();
            host.Add(Rect(6f, 2f, 8f, 8f, c, 45f, 1f));   // 다이아몬드
            host.Add(Rect(8f, 10f, 4f, 6f, c));           // 꼬리
            return host;
        }

        /// <summary>골절 — 뼈(단관 2원 + 간판 1사각, 중앙 골절선).</summary>
        private static VisualElement BuildFractureGlyph(Color c)
        {
            var host = GlyphHost();
            host.Add(Circle(1f, 6f, 6f, c));
            host.Add(Circle(13f, 6f, 6f, c));
            host.Add(Rect(6f, 8f, 8f, 3f, c));            // 골간
            host.Add(Rect(9.25f, 5f, 1.5f, 10f, new Color(0.08f, 0.10f, 0.12f), 20f)); // 골절 균열
            return host;
        }

        /// <summary>혼탁 — 물결(사각 3단 계단).</summary>
        private static VisualElement BuildDazeGlyph(Color c)
        {
            var host = GlyphHost();
            host.Add(Rect(2f, 4f, 8f, 2.5f, c));
            host.Add(Rect(6f, 9f, 8f, 2.5f, c));
            host.Add(Rect(10f, 14f, 8f, 2.5f, c));
            return host;
        }

        /// <summary>허기 — Drumstick(고기 원 + 대각 뼈대).</summary>
        private static VisualElement BuildHungerGlyph(Color c)
        {
            var host = GlyphHost();
            host.Add(Circle(3f, 3f, 11f, c));                 // 고기
            host.Add(Rect(12f, 12f, 8f, 3f, c, -45f, 1.5f));  // 뼈대 대각
            host.Add(Circle(16.5f, 15.5f, 3.5f, c));          // 뼈 끝
            return host;
        }

        /// <summary>화상 — 불꽃(다이아 + 하단 사각).</summary>
        private static VisualElement BuildBurnGlyph(Color c)
        {
            var host = GlyphHost();
            host.Add(Rect(5f, 1f, 10f, 10f, c, 45f, 1f));   // 화염 몸통(다이아)
            host.Add(Rect(7.5f, 11f, 5f, 6f, c, 0f, 2f));   // 하단 불티
            return host;
        }

        /// <summary>저체온 — 눈结晶(십자 + 대각 = 얇은 사각 3개).</summary>
        private static VisualElement BuildHypothermiaGlyph(Color c)
        {
            var host = GlyphHost();
            host.Add(Rect(2f, 9f, 16f, 1.8f, c, 0f));    // 가로
            host.Add(Rect(9.1f, 2f, 1.8f, 16f, c));      // 세로
            host.Add(Rect(2f, 9f, 16f, 1.8f, c, 45f));   // 대각
            host.Add(Rect(2f, 9f, 16f, 1.8f, c, -45f));  // 반대 대각
            return host;
        }

        /// <summary>공포 — 눈(타원 + 중심 동공).</summary>
        private static VisualElement BuildTerrorGlyph(Color c)
        {
            var host = GlyphHost();
            host.Add(Rect(2f, 5f, 16f, 10f, c, 0f, 8f));                       // 눈 폭
            host.Add(Circle(7.5f, 7.5f, 5f, new Color(0.08f, 0.10f, 0.12f)));  // 동공
            return host;
        }

        /// <summary>감염 — 세균(원 + 돌기 4).</summary>
        private static VisualElement BuildInfectionGlyph(Color c)
        {
            var host = GlyphHost();
            host.Add(Circle(5.5f, 5.5f, 9f, c));           // 몸통
            host.Add(Circle(8.5f, 0.5f, 3f, c));           // 돌기 상
            host.Add(Circle(8.5f, 16.5f, 3f, c));          // 돌기 하
            host.Add(Circle(0.5f, 8.5f, 3f, c));           // 돌기 좌
            host.Add(Circle(16.5f, 8.5f, 3f, c));          // 돌기 우
            return host;
        }

        /// <summary>약화 — 아래 화살(세로대 + 대각 2개).</summary>
        private static VisualElement BuildWeakenGlyph(Color c)
        {
            var host = GlyphHost();
            host.Add(Rect(8.5f, 1f, 3f, 11f, c));                  // 대
            host.Add(Rect(4f, 11f, 6f, 2.5f, c, 45f));             // 날개 L
            host.Add(Rect(10f, 11f, 6f, 2.5f, c, -45f));           // 날개 R
            return host;
        }

        /// <summary>취기 — 잔(몸통 + 받침).</summary>
        private static VisualElement BuildDrunkennessGlyph(Color c)
        {
            var host = GlyphHost();
            host.Add(Rect(5f, 2f, 10f, 9f, c, 0f, 1f));    // 잔 몸
            host.Add(Rect(3f, 12f, 14f, 1.8f, c));         // 받침
            host.Add(Rect(9.1f, 13.8f, 1.8f, 4f, c));      // 다리
            return host;
        }

        /// <summary>방어 취약 — 방盾(테두리 사각 + 중앙 금).</summary>
        private static VisualElement BuildBrittleDefenseGlyph(Color c)
        {
            var host = GlyphHost();
            var shield = new VisualElement();
            shield.style.position = Position.Absolute;
            shield.style.left = 3f; shield.style.top = 2f;
            shield.style.width = 14f; shield.style.height = 16f;
            shield.style.borderTopWidth = shield.style.borderBottomWidth = 1.8f;
            shield.style.borderLeftWidth = shield.style.borderRightWidth = 1.8f;
            shield.style.borderTopColor = shield.style.borderBottomColor = new StyleColor(c);
            shield.style.borderLeftColor = shield.style.borderRightColor = new StyleColor(c);
            shield.style.borderTopLeftRadius = shield.style.borderTopRightRadius = 3f;
            shield.style.borderBottomLeftRadius = shield.style.borderBottomRightRadius = 7f;
            shield.pickingMode = PickingMode.Ignore;
            host.Add(shield);
            host.Add(Rect(9.25f, 4f, 1.5f, 9f, c, 25f));   // 균열(금)
            return host;
        }
    }
}
