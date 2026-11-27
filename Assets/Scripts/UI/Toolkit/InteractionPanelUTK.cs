using System;
using System.Collections.Generic;   // [144차 P0-INT] 액션 아이콘 캐시(Dictionary)
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;
using ProjectName.UI;
using ProjectName.Core;   // [126차] PlayerInventory(들꽃 선물 소비 경로) — UI asmdef Core 참조 확인됨.

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// Compact target interaction panel. Button behavior is exposed as an event so game-specific
    /// actions can be connected without introducing a Systems-to-UI dependency.
    /// </summary>
    public class InteractionPanelUTK : UTKWindowBase
    {
        // The source-supported four-action frames (81:91, 81:134 and 82:2) share
        // 288x98.4 Figma geometry. At the 1.2x canonical scale, the logical panel is
        // 240x82 and ActionGrid is (8,8,224,66), enclosing a 2x2 set of buttons.
        private const float PanelWidth = 240f;
        private const float StandardPanelHeight = 82f;
        private const float GridLeft = 8f;
        private const float GridTop = 8f;
        private const float GridWidth = 224f;
        private const float GridHeight = 66f;
        // The four Figma buttons are 130.8x36 with a 7.2px column gap;
        // at 1.2x canonical scale their logical bounds are 109x30 with a 6px gap.
        // Two columns at x=0/115 and rows at y=0/36 fit the 224x66 grid exactly.
        // Existing UTKButton padding/radius stays inside those fixed outer bounds.
        private const float ActionWidth = 109f;
        private const float ActionHeight = 30f;
        private const float ColumnGap = 6f;
        private const float SecondRowTop = 36f;
        // [63차] 93:185 제압 영주 1행 패널 — 그리드 높이 축소(66 → 30 — [144차 P1-INT #31] Figma 36.0px=논리 30), 패널은 82 → 54.
        private const float SingleRowGridHeight = 30f;
        private const float SingleRowPanelHeight = 54f;
        // [63차 — 피그마 실측] 하이라이트 #58A6FF(0.345,0.651,1.0)/700, 일반 #8B949E(0.545,0.580,0.620)/500 — [144차 P2-INT] LSB 정정.
        private static readonly Color HighlightText = new Color(0.345f, 0.651f, 1.00f, 1f);
        private static readonly Color NormalText = new Color(0.545f, 0.580f, 0.620f, 1f);
        // [101차 F-1] 본체 단일 앵커 — [91차 F3] 대상 위 오프셋(구 산식 Vector3.up * 0.9f)/[94차 F4] 정면앵커
        //   (bounds center + forward 0.6m + 높이 클램프 1.2~2.2m)/[100차 F-1] bounds maxY + 0.25m 산식 폐기.
        //   근거: 오빗 카메라에서 정면 지점 투영 x/y가 NPC 본체 중심/머리에서 이탈(테스트씬 15 실측 — 패널이
        //   NPC 측면에 표시). 단일 앵커: x = NPC 렌더러 bounds 중심 투영 x, y = (Foot 본 y + 1.95m) 투영 y(머리 위).
        private const float BodyAnchorFootUpM = 1.95f;      // Foot 본 y 기준 상승(m) — 머리 위 앵커 고도(스펙 지정)
        private const float BodyAnchorBoundsPadM = 0.15f;   // Foot 본 부재 폴백 — bounds maxY + 0.15m(스펙 지정)
        private static Transform _bodyAnchorProxy;    // 본체 앵커 지점 투영 프록시(렌더러 없음 — 공용 유틸 폴백 산식 유도, 파괴 시 재생성)

        // [98차 F3] F3-4 부착 판정 거리(px) — 배치 후 npc screenPx와 패널 중심 거리 ≤ 120px = 부착 성공(스펙 지정).
        private const float AttachVerifyMaxDistancePx = 120f;
        // [100차 F-1 → 101차 F-1] 부착 계약 — 패널 하단 중앙 = 본체 앵커(bounds 중심 x + Foot 본 y + 1.95m —
        //   머리 위) 투영점 위에 얹힌다(y = 앵커 screenPx.y − panelH − HeadAnchorGapPx, x 중앙 정렬 — 화면 클램프 유지).
        //   근거: [97차 F3] 부착 로그 실측 — 시맨틱 판정(머리부착=6px 성공)이 배치에 쓴 같은 앵커끼리의 거리라
        //   자기참조 성공(화면은 패널이 NPC 측면). 시맨틱 판정 — 패널하단중앙↔앵커 투영점 거리
        //   ≤ HeadAttachMaxDistancePx(24px) = 부착(성공) 병기 + [101차 F-1] 본체 실측 대조 판정 병기
        //   (몸중심 x 편차 ≤ 20px AND 패널하단↔머리 y ≤ 24px — x축 판정 신설, LogPlacementVerify).
        //   [99차 F-A] 비가시 숨김 계약은 유지.
        private const float HeadAnchorGapPx = 6f;           // 패널 하단 ↔ 앵커 투영점 간격(px)
        private const float HeadAttachMaxDistancePx = 24f;  // 시맨틱 부착 판정 거리(px) — ≤24px = 부착(성공)
        private const float BodyCenterMaxDeviationPx = 20f; // [101차 F-1] 몸중심 x 편차 판정(px) — ≤20px(x축 판정 신설)
        // [99차 F-A] A-2 화면 경계 여유(px) — 산출 npc screenPx가 화면 경계 밖 이 여유(±200px)를 벗어나면
        //   패널 비가시 숨김(투영 회복 시 재표시). [98차 F3] 눌러투영의 ±10만 px급 비정상 좌표
        //   (screenPx=(-295899,-53622) → 클램프 (0,841) 좌하단 고정 — 마을씬 테스트 10 재현) 재발 차단.
        private const float ScreenBoundsMarginPx = 200f;
        // [98차 F3] F3-1 이전 실측 패널 크기 캐시 — resolvedStyle NaN(레이아웃 미확정) 시 폴백 2단.
        private Vector2 _lastPanelSize;
        // [98차 F3] F3-2 최근 재교정에 사용한 실측 크기 — GeometryChangedEvent 무루프 가드(크기 변화 없으면 무재배치).
        private Vector2 _lastRecenteredSize;
        // [99차 F-A] A-2 비가시 숨김 상태 — display만 None(IsOpen/배치 추적 폴링 유지 — Hide/Close 계약
        //   미경유로 배회 재개·감시 해제 없음). 투영 회복(화면 안 복귀) 시 Flex 재표시.
        private bool _occludedHidden;

        private static InteractionPanelUTK _instance;

        private VisualElement _actionGrid;
        private HoverTargetClassifier.TargetKind _targetKind;
        private object _target;
        private IVisualElementScheduledItem _autoCloseTask;   // [142차 D] 대상 이탈 자동 닫힘 감시(250ms 폴링)

        /// <summary>[142차 D] 대상(NPC 등)에서 이 거리(m)를 벗어나면 패널 자동 닫힘 — 사용자 지시.</summary>
        internal const float AutoCloseDistanceM = 5f;
        private float _uiScaleX = 1f;
        private float _uiScaleY = 1f;
        // [72차] 개방 중 이동 추적 폴링 — 1s 간격(대상 재배치/소멸 감시).
        private const long PlacePollMs = 1000L;
        private IVisualElementScheduledItem _placeTask;
        private object _trackedTarget;
        // [94차 F4] F키 개방 즉시 정지 — 정지 대상 추적(패널 닫힘/대상 변경 시 ResumeAfterDialogue 재개 데이터).
        private object _wanderPausedTarget;

        /// <summary>Raised when a panel action is clicked. TargetKind and target match the current panel.</summary>
        public static event Action<HoverTargetClassifier.TargetKind, string, object> ActionRequested;

        public static InteractionPanelUTK Instance => _instance;

        public static void Ensure()
        {
            if (_instance == null)
            {
                _instance = new InteractionPanelUTK();
                // [144차 D2-FISH] 낚시 근접 패널 폴링 — FishingSystem(Systems)은 UI 역참조 금지(asmdef 단방향)라
                //   UI 측 250ms 폴링으로 개방/해제한다(호버 레이캐스트 경로와 독립 — 물가는 근접 판정 대상).
                _instance.schedule.Execute(PollFishingPanel).Every(250L);
            }
        }

        /// <summary>[144차 D2-FISH] 물가 근접 시 피그마 163:4 낚시 패널 개방, 이탈 시 해제.
        /// 다른 종류 패널(호버)이 표시 중이면 개방을 건너뛴다(호버 패널 소유권 보호).</summary>
        private static void PollFishingPanel()
        {
            bool want = FishingSystem.ShouldShowFishingPanel;
            bool showingFishing = _instance != null
                && _instance._targetKind == HoverTargetClassifier.TargetKind.Fishing
                && _instance.panel != null
                && _instance.style.display == DisplayStyle.Flex;
            bool otherPanelShowing = _instance != null
                && _instance.panel != null
                && _instance.style.display == DisplayStyle.Flex
                && !showingFishing;
            if (want && !showingFishing && !otherPanelShowing)
            {
                var anchor = FishingSystem.PanelAnchor;
                if (anchor != null)
                    Open(HoverTargetClassifier.TargetKind.Fishing, anchor);
            }
            else if (!want && showingFishing)
            {
                CloseIfKind(HoverTargetClassifier.TargetKind.Fishing);
            }
        }

        /// <summary>Open the compact panel for a classified target. Click-trigger wiring is intentionally external.</summary>
        public static void Open(HoverTargetClassifier.TargetKind kind, object target)
        {
            if (!IsSupportedKind(kind))
                return;

            Ensure();
            _instance.OpenForTarget(kind, target);
        }

        private InteractionPanelUTK() : base("상호작용", new Vector2(PanelWidth, StandardPanelHeight))
        {
            SetChrome(UTKWindowChrome.Frameless);
            // [142차 B 원복 — 사용자 판정] 투명화 시도보다 기존 피그마 스타일(배경판 유지)이 낫다는 재판정.
            // Frameless 기본(UTKTheme.Panel 배경+그림자) = 피그마 정합 상태 — 그대로 유지.
            style.width = PanelWidth;
            style.height = StandardPanelHeight;
            style.overflow = Overflow.Visible;

            _content.style.width = PanelWidth;
            _content.style.height = StandardPanelHeight;
            _content.style.paddingLeft = 0f;
            _content.style.paddingRight = 0f;
            _content.style.paddingTop = 0f;
            _content.style.paddingBottom = 0f;
            _content.style.flexGrow = 0f;
            _content.style.flexShrink = 0f;

            _actionGrid = new VisualElement { name = "ActionGrid" };
            _actionGrid.style.position = Position.Absolute;
            _actionGrid.style.left = GridLeft;
            _actionGrid.style.top = GridTop;
            _actionGrid.style.width = GridWidth;
            _actionGrid.style.height = GridHeight;
            _actionGrid.style.flexGrow = 0f;
            _actionGrid.style.flexShrink = 0f;
            _actionGrid.style.flexDirection = FlexDirection.Row;
            _actionGrid.style.flexWrap = Wrap.NoWrap;
            _actionGrid.style.alignItems = Align.FlexStart;
            _actionGrid.style.justifyContent = Justify.FlexStart;
            _actionGrid.style.overflow = Overflow.Visible;
            _content.Add(_actionGrid);

            ApplyUIToolkitFont(this);
            // [96차 F2] 부모 DetachFromPanel 경로 수렴 — Hide 미경유 닫힘(외부 RemoveFromHierarchy/
            //   UIRoot 재구성 등)에서도 정지 대상 재개 보장(EnsureResumeOnAnyClose 단일 수렴).
            RegisterCallback<DetachFromPanelEvent>(_ => EnsureResumeOnAnyClose());
            // [98차 F3] F3-2 후배치 보정 — 레이아웃 확정(실측 크기 산출 가능) 시점 중앙 정렬 재교정.
            //   [97차 F3] 다음 프레임 스케줄러 계승 + GeometryChangedEvent 보강 — 크기 변화(영주 1행 축소 등)
            //   에도 실측 크기로 재정렬. NaN 크기는 스킵하지 않고 기본 크기 사용(ResolvePanelBoundsPx 폴백 계약).
            RegisterCallback<GeometryChangedEvent>(OnGeometryChangedRecenter);
            style.display = DisplayStyle.None;
        }

        /// <summary>[144차 D2-FISH] 피그마 163:4 낚시 패널 크롬 적용/복원.
        /// Fishing: fill #161B22@0.7 · stroke #30363D@0.5 1.2 · r9.6 · 코너 데칼 4각(#58A6FF 1.8 — 163:5~16).
        /// 그 외 종류: Frameless 기본(UTKTheme.Panel 불투명 · 무테두리 · 무코너)으로 복원(142차 B' 배경판 유지).</summary>
        private void ApplyFishingChrome(bool fishing)
        {
            if (fishing)
            {
                style.backgroundColor = new StyleColor(new Color(0x16 / 255f, 0x1B / 255f, 0x22 / 255f, 0.7f));
                UTKTheme.ApplyElementBorder(this, UTKTheme.BorderChrome, UTKTheme.BorderChromeWidth);
                UTKTheme.ApplyRadius(this, 9.6f);
                // 코너 데칼 중복 방지 — 기존 V1 데칼 제거 후 재부착(OpenForTarget 재개방 멱등).
                this.Q("DecalTL")?.RemoveFromHierarchy();
                this.Q("DecalTR")?.RemoveFromHierarchy();
                this.Q("DecalBL")?.RemoveFromHierarchy();
                this.Q("DecalBR")?.RemoveFromHierarchy();
                UTKTheme.AddCornerDecals(this, 9.6f, 1.8f, "#58A6FF");   // [144차 P1-DLG #42 승계] 코너 밖(공용 기본 모드) — 구 inside:false 정정
            }
            else
            {
                style.backgroundColor = new StyleColor(UTKTheme.Panel);
                style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 0f;
                style.borderTopLeftRadius = style.borderTopRightRadius = 0f;
                style.borderBottomLeftRadius = style.borderBottomRightRadius = 0f;
                this.Q("DecalTL")?.RemoveFromHierarchy();
                this.Q("DecalTR")?.RemoveFromHierarchy();
                this.Q("DecalBL")?.RemoveFromHierarchy();
                this.Q("DecalBR")?.RemoveFromHierarchy();
            }
        }

        /// <summary>[144차 D2-FISH] 지정 종류 패널이 개방 중이면 닫기 — FishingSystem 근접 이탈 시 사용
        /// (다른 종류 패널이 떠 있으면 건드리지 않음 — 호버 패널 소유권 보호).</summary>
        public static void CloseIfKind(HoverTargetClassifier.TargetKind kind)
        {
            if (_instance != null && _instance._targetKind == kind && _instance.panel != null)
                _instance.Close();
        }

        private static bool IsSupportedKind(HoverTargetClassifier.TargetKind kind)
        {
            return kind == HoverTargetClassifier.TargetKind.EnemyGuard ||
                   kind == HoverTargetClassifier.TargetKind.Ally ||
                   kind == HoverTargetClassifier.TargetKind.NPC ||
                   kind == HoverTargetClassifier.TargetKind.ShopNPC ||
                   kind == HoverTargetClassifier.TargetKind.Lord ||
                   kind == HoverTargetClassifier.TargetKind.LordDaughter ||   // [126차] 영주 딸(들꽃 선물/프로포즈)
                   kind == HoverTargetClassifier.TargetKind.ArenaManager ||   // [121차] 아레나 관리인(261:3)
                   kind == HoverTargetClassifier.TargetKind.Fishing ||        // [144차 D2-FISH] 낚시(163:4)
                   kind == HoverTargetClassifier.TargetKind.Bed ||            // [144차 D2-SLEEP] 침대(163:45)
                   kind == HoverTargetClassifier.TargetKind.Door;             // [144차 D2-LOCK] 잠긴 문(163:64/83)
        }

        private void OpenForTarget(HoverTargetClassifier.TargetKind kind, object target)
        {
            _targetKind = kind;
            _target = target;

            // [94차 F4 → 96차 F2] F키 개방 즉시 정지 — 이전 대상 재개 후 신규 대상 배회 정지(대상 변경 시 재개 계약).
            //   주민=VillagerWanderer.PauseForDialogue 재사용(멱등 — '대화하기' 클릭 경로와 중복 무해).
            EnsureResumeOnAnyClose();   // [96차 F2] 개방 진입 수렴 — 이전 대상 재개(대상 클리어/변경 경로 공용).
            PauseTargetWander(target);

            // [142차 D] 거리 이탈 자동 닫힘 — 개방 중 250ms 폴링, 대상에서 5m 초과 이탈 시 Hide()
            // (Hide = EnsureResumeOnAnyClose 경유 재개 계약 — 배회 정지 유출 없음).
            _autoCloseTask?.Pause();
            _autoCloseTask = schedule.Execute(AutoCloseDistanceTick).Every(250L);

            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);

            ApplyRootScale(root);
            RebuildActions();
            // [63차] 93:185 제압 영주(1행 2버튼) — 패널 높이 축소 82→54(그리드 38+상하 여백 16).
            if (kind == HoverTargetClassifier.TargetKind.Lord
                && InvadedLordEncounter.FindOnTarget(target) is InvadedLordEncounter lord && lord.IsDead)
            {
                float scaleX = _uiScaleX, scaleY = _uiScaleY;
                style.height = SingleRowPanelHeight * scaleY;
                _content.style.height = SingleRowPanelHeight * scaleY;
            }
            // [144차 D2-FISH] 피그마 163:4 낚시 패널 크롬 — fill #161B22@0.7 · stroke #30363D@0.5 1.2 · r9.6 ·
            //   코너 데칼 4각(#58A6FF 1.8). 타 종류 개방 시 기본 크롬(UTKTheme.Panel 불투명·무테두리·무코너)으로 복원.
            ApplyFishingChrome(kind == HoverTargetClassifier.TargetKind.Fishing);
            // [99차 F-A] A-2 순서 교환 — Show 후 배치: 개방 첫 PlaceAtTarget의 비가시 숨김 판정이
            //   뒤이은 Show로 덮여 1프레임 플래시되는 것을 막는다(경계 밖 대상 개방 → 즉시 숨김 상태 유지).
            _occludedHidden = false;   // [99차 F-A] 개방 시 숨김 상태 리셋(신규 대상 — 재표시 기준 초기화).
            Show();
            PlaceAtTarget(target);   // [72차] 대상 바로 앞 배치 — 마우스 위치 배치 폐기.
            StartPlacementTracking(target);   // [72차] 개방 중 이동 추적(1s 폴링).
            ScheduleNextFrameReplacement();   // [97차 F3] 다음 프레임 재배치 1회 — 레이아웃 확정 후 보정.
        }

        private void ApplyRootScale(VisualElement root)
        {
            if (root == null)
                return;

            float rootWidth = root.resolvedStyle.width;
            float rootHeight = root.resolvedStyle.height;
            if (rootWidth <= 0f || rootHeight <= 0f)
                return;

            // Panel/root coordinates and screen input share the canonical 1920x1080
            // canvas. Scale the logical 240x82 panel directly against that root.
            float uiScaleX = rootWidth / FigmaCanvasLayout.CanvasWidth;
            float uiScaleY = rootHeight / FigmaCanvasLayout.CanvasHeight;
            _uiScaleX = uiScaleX;
            _uiScaleY = uiScaleY;
            Vector2 size = GetLayoutSize(_targetKind);
            style.width = size.x * uiScaleX;
            style.height = size.y * uiScaleY;
            _content.style.width = size.x * uiScaleX;
            _content.style.height = size.y * uiScaleY;
            _actionGrid.style.left = GridLeft * uiScaleX;
            _actionGrid.style.top = GridTop * uiScaleY;
            _actionGrid.style.width = GridWidth * uiScaleX;
            _actionGrid.style.height = GridHeight * uiScaleY;

            // [72차] 배치는 마우스 위치(폐기) → PlaceAtTarget(대상 기준)으로 이관 — 여기선 크기 스케일만 담당.
        }

        /// <summary>
        /// [72차] 대상 바로 앞 배치 — [91차 F3 → 92차 결함2] 월드→스크린 투영을 TargetScreenProjector
        /// 공용 유틸로 일원화. [92차 결함2] 계약: 유효 = z&gt;0만(화면 밖 좌표도 반환) — 받은 screenPx를
        /// 루트 좌표로 변환한 뒤 <b>패널 전체가 화면 안에 들도록 클램프 후 배치</b>(하단 중앙 기준 좌표 —
        /// 패널 크기 고정 클램프). [101차 F-1] 본체 단일 앵커 — 패널 하단 중앙을 본체 앵커(bounds 중심 x +
        /// Foot 본 y + 1.95m, 머리 위) 투영점 위 6px에 얹는다(x 중앙 정렬 — 클램프 유지). [94차 F4] 정면앵커/
        /// [98차 F3] 본체 재투영·눌러투영 경로는 폐기(오빗 카메라에서 정면 지점 투영이 본체에서 이탈 — 단일 앵커 수렴).
        /// GuardPlaceholder/TerritoryNPCBehaviour/InvadedLordEncounter 공통 — Component.transform 경유.
        /// </summary>
        private void PlaceAtTarget(object target)
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null)
                return;
            var tracked = target as Component;
            if (tracked == null)
                return;

            float rootWidth = root.resolvedStyle.width;
            float rootHeight = root.resolvedStyle.height;
            if (rootWidth <= 0f || rootHeight <= 0f || float.IsNaN(rootWidth) || float.IsNaN(rootHeight))
                return;

            // [101차 F-1] 본체 단일 앵커 — 지점 투영은 렌더러 없는 프록시 Transform으로 공용 유틸(z>0 가드/
            //   카메라 폴백)을 통과(NPCDialoguePanelUTK 프록시 선례 — TargetScreenProjector 무수정 계약 유지).
            //   false = 전 카메라 뒤(z<0)/카메라 부재 — 무배치(구 총 투영 실패 계약 승계).
            if (!TryProjectBodyAnchor(tracked, out Vector2 screen))
            {
                LogPlacementVerify(null, "폴백");   // [97차 F3] 총 투영 실패(z<0 지속/카메라 부재) — 무배치 즉격.
                return;
            }

            // [99차 F-A] A-2 본체앵커 산출 좌표 가시성 가드 — 경계 밖 ±200px 초과 → 비가시 숨김/회복(계약 유지).
            if (!UpdateOcclusionVisibility(screen, "본체앵커")) return;
            ApplyPlacementFromScreenPx(screen, rootWidth, rootHeight);   // [98차 F3] F3-1 worldBound 비의존 배치(공용 경로)
            LogPlacementVerify(screen, "본체앵커", tracked);   // [97차 F3 → 101차 F-1] 부착 검증 + 본체 실측 대조 판정.
        }

        /// <summary>
        /// [98차 F3] F3-1 배치 산출 worldBound 비의존화 — npc screenPx 기준 직접 산출(공용 경로 — 정면앵커/
        /// 폴백 공통). y축 반전 산식(165-166행 계약) 유지 + [100차 F-1] 머리 위 부착(패널 하단 중앙 = 앵커
        /// 투영점 위 6px — x 중앙 정렬) + 루트 클램프(패널 전체 화면 안).
        /// 패널 폭/높이 = ResolvePanelBoundsPx(resolvedStyle → 이전 실측 캐시 → 기본 크기 폴백 — NaN이면
        /// 기본 크기 사용, 중앙 폴백 배치 금지). worldBound는 배치 산출에 조회하지 않는다(진단 로그 전용).
        /// </summary>
        private void ApplyPlacementFromScreenPx(Vector2 screen, float rootWidth, float rootHeight)
        {
            // 화면 px → 캔버스(1920×1080) → 루트 px — 기존 165-166행 산식 유지(y축 반전 포함).
            float canvasX = screen.x * FigmaCanvasLayout.CanvasWidth / Screen.width;
            float canvasY = (Screen.height - screen.y) * FigmaCanvasLayout.CanvasHeight / Screen.height;
            Rect scaledPoint = FigmaCanvasLayout.ScaleRect(
                new Rect(canvasX, canvasY, 0f, 0f), new Vector2(rootWidth, rootHeight));

            // [98차 F3] F3-1 패널 실측 크기 — worldBound 비의존(resolvedStyle/캐시/기본 크기 폴백).
            Rect panelBounds = ResolvePanelBoundsPx(rootWidth, rootHeight);

            // [92차 결함2] 루트 좌표 클램프 후 배치 — 패널 전체가 화면 안(하단 중앙 기준 — 패널 크기 고정).
            //   대상이 화면 가장자리여도 패널이 대상 방향에 붙어 유지(PlacementTick 폴백 튐 제거).
            //   [98차 F3] z<0 폴백(눌러투영)까지 전 경로 공용 — NPC 최근접 가장자리 클램프 배치.
            style.position = Position.Absolute;
            style.left = Mathf.Clamp(scaledPoint.x - panelBounds.width * 0.5f, 0f, Mathf.Max(0f, rootWidth - panelBounds.width));
            // [100차 F-1] 머리 위 배치 — 패널 하단이 앵커 투영점 위 6px에 얹힌다(y = 앵커 y − panelH − 6px).
            //   구 산식(top = 앵커 y − panelH)은 패널 하단이 앵커에 닿는 배치 = 패널이 NPC 지점 아래로 안양
            //   ([97차 F3] 실측 — 패널이 NPC 지점보다 화면상 138px 아래). x 중앙 정렬/화면 클램프 유지.
            style.top = Mathf.Clamp(scaledPoint.y - panelBounds.height - HeadAnchorGapPx, 0f, Mathf.Max(0f, rootHeight - panelBounds.height));
        }

        /// <summary>
        /// [98차 F3] F3-1 패널 실측 크기(px) — worldBound 비의존 3단 폴백: ①resolvedStyle(레이아웃 확정
        /// 실측값 — 유효 시 캐시 갱신) ②이전 실측값 캐시 ③기본 크기(GetLayoutSize 논리치 × 루트 스케일 —
        /// 스케일 무효 시 논리치 원값). NaN이면 기본 크기 사용 — 스킵/중앙 폴백 배치 금지(F3-1 계약).
        /// </summary>
        private Rect ResolvePanelBoundsPx(float rootWidth, float rootHeight)
        {
            float scaleX = rootWidth / FigmaCanvasLayout.CanvasWidth;
            float scaleY = rootHeight / FigmaCanvasLayout.CanvasHeight;
            bool scaleValid = !float.IsNaN(scaleX) && !float.IsNaN(scaleY) && scaleX > 0f && scaleY > 0f;

            // ① resolvedStyle 실측 — 레이아웃 확정값(worldBound와 무관하게 resolvedStyle만 조회).
            float w = resolvedStyle.width;
            float h = resolvedStyle.height;
            if (IsValidPanelSize(w) && IsValidPanelSize(h))
            {
                _lastPanelSize = new Vector2(w, h);   // [98차 F3] 이전 실측 캐시 갱신
                return new Rect(0f, 0f, w, h);
            }
            // ② 이전 실측값 캐시 — 레이아웃 미확정 프레임 보호.
            if (IsValidPanelSize(_lastPanelSize.x) && IsValidPanelSize(_lastPanelSize.y))
                return new Rect(0f, 0f, _lastPanelSize.x, _lastPanelSize.y);
            // ③ 기본 크기 폴백 — 논리치×스케일(NaN이면 스킵하지 않고 기본 크기 사용 — F3-1 계약).
            Vector2 logical = GetLayoutSize(_targetKind);
            float bw = scaleValid ? logical.x * scaleX : logical.x;
            float bh = scaleValid ? logical.y * scaleY : logical.y;
            return new Rect(0f, 0f, bw, bh);
        }

        /// <summary>[98차 F3] F3-1 패널 크기 유효 판정 — NaN/∞/음수·0 배제(기본 크기 폴백 트리거).</summary>
        private static bool IsValidPanelSize(float value)
            => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

        /// <summary>
        /// [99차 F-A] A-2 대상 화면 가시성 가드 — 산출 npc screenPx가 화면 경계 밖 여유(±200px)를 벗어나면
        /// 패널을 비가시 숨김(display만 None — IsOpen/배치 추적 폴링 유지, Hide/Close 계약 미경유 → 배회
        /// 재개·감시 해제 없음)하고 false를 반환한다(호출부 무배치). 투영 회복(화면 안 복귀) 시 Flex 재표시+
        /// BringToFront 후 true — 회복 프레임에 정상 배치가 이어진다. 근거: [98차 F3] F3-3 눌러투영의
        /// screenPx=(-295899,-53622)급 비정상 좌표 → 클램프 (0,841) 좌하단 고정(마을씬 테스트 10 재현) —
        /// 대상 비가시성에 패널을 붙잡아 두지 않는다. 숨김/재표시 사유 로그는 A-3(LogPlacementVerify reason).
        /// </summary>
        private bool UpdateOcclusionVisibility(Vector2 screen, string path)
        {
            bool inBounds = screen.x >= -ScreenBoundsMarginPx && screen.x <= Screen.width + ScreenBoundsMarginPx
                            && screen.y >= -ScreenBoundsMarginPx && screen.y <= Screen.height + ScreenBoundsMarginPx;
            if (!inBounds)
            {
                if (_occludedHidden) return false;   // 이미 숨김 — 무로그(1s 틱 스팸 방지).
                _occludedHidden = true;
                style.display = DisplayStyle.None;   // 비가시 숨김 — IsOpen 유지(회복 폴링 생존).
                LogPlacementVerify(screen, path, "폴백=비가시숨김(화면경계±200px 초과)");   // [99차 F-A] A-3
                return false;
            }
            if (_occludedHidden)
            {
                _occludedHidden = false;
                style.display = DisplayStyle.Flex;   // 투영 회복 — 자동 재표시(Show 경유 아님 — IsOpen 유지).
                BringToFront();
                LogPlacementVerify(screen, path, "폴백=재표시(투영 회복)");   // [99차 F-A] A-3 회복 사유
            }
            return true;
        }

        /// <summary>[99차 F-A] A-1 투영 좌표 유효 판정 — NaN/∞ 배제(눌러투영 스케일 폭발 방어).</summary>
        private static bool IsValidScreenPoint(Vector3 vp)
            => !float.IsNaN(vp.x) && !float.IsNaN(vp.y) && !float.IsInfinity(vp.x) && !float.IsInfinity(vp.y);

        /// <summary>
        /// [101차 F-1] 본체 단일 앵커 투영 — x = NPC 렌더러 bounds 중심 x, y = 본체 앵커 고도(Foot 본 y +
        /// 1.95m — 머리 위)인 월드 지점을 렌더러 없는 프록시 Transform으로 공용 유틸에 실어 투영(z>0 가드/
        /// 카메라 폴백 일원화 계약 승계 — TargetScreenProjector 무수정). [94차 F4] 정면앵커(정면 오프셋 0.6m +
        /// 높이 클램프)/[98차 F3] 본체 재투영·눌러투영 경로 폐기 — 오빗 카메라에서 정면 지점 투영 x/y가 NPC
        /// 본체 중심/머리에서 이탈한 실측(테스트씬 15 — 패널 측면 표시)의 근본 수리. 반환 false = 전 카메라
        /// z&lt;0(뒤)/카메라 부재 — 호출부 무배치.
        /// </summary>
        private static bool TryProjectBodyAnchor(Component tracked, out Vector2 screen)
        {
            Bounds b = GetTargetVisualBounds(tracked, out bool has);
            Vector3 baseXZ = has ? b.center : tracked.transform.position;
            Vector3 anchor = new Vector3(baseXZ.x, ComputeBodyAnchorHeight(tracked, b, has), baseXZ.z);

            EnsureBodyAnchorProxy();
            _bodyAnchorProxy.position = anchor;
            return TargetScreenProjector.TryProject(_bodyAnchorProxy, 0f, out screen, out _);
        }

        /// <summary>[101차 F-1] 본체 앵커 고도(m) 단일소스 — Foot 본 y + 1.95m(머리 위). Foot 본 부재 시
        /// bounds maxY + 0.15m 폴백, bounds 부재 시 transform.y + 1.95m 폴백(1.8m 캐릭터 키 계약).
        /// 정면 오프셋/높이 클램프 없음(본체 단일 앵커 계약 — [94차 F4] 클램프 폐기 승계).</summary>
        private static float ComputeBodyAnchorHeight(Component tracked, Bounds b, bool has)
        {
            if (TryMeasureFootBoneY(tracked, out float footY))
                return footY + BodyAnchorFootUpM;
            if (has)
                return b.max.y + BodyAnchorBoundsPadM;
            return tracked.transform.position.y + BodyAnchorFootUpM;
        }

        /// <summary>[101차 F-1] Foot 본 y 실측 — 이름에 foot/toe 포함 본(좌우 — 대소문자 무시) 중 최저 y.
        /// DialogueCameraDirector.TryMeasureFootBottomY(시스템 측 — private)와 동일 규칙의 호출처 미러
        /// (GetTargetVisualBounds 선례). 발 본 부재 시 false — 호출처 폴백(bounds maxY + 0.15m).</summary>
        private static bool TryMeasureFootBoneY(Component target, out float footY)
        {
            footY = float.MaxValue;
            bool found = false;
            var bones = target.GetComponentsInChildren<Transform>();
            for (int i = 0; i < bones.Length; i++)
            {
                Transform t = bones[i];
                if (t == null) continue;
                string lower = t.name.ToLowerInvariant();
                if (string.IsNullOrEmpty(lower)) continue;
                if (!lower.Contains("foot") && !lower.Contains("toe")) continue;
                if (t.position.y < footY) { footY = t.position.y; found = true; }
            }
            return found;
        }

        /// <summary>
        /// [101차 F-1] 본체 실측 직접 투영 — 앵커(proxy + 공용 유틸 경유)와 별도 경로로 Camera.WorldToScreenPoint
        /// 직접 산출(자기참조 방지 — 앵커 좌표와 본체 실측 좌표를 별도 산출해 대조). 카메라 = Camera.main 우선 →
        /// allCameras[0] 폴백(공용 유틸 카메라 결정 순서 동일). z≤0/NaN/∞ = false.
        /// </summary>
        private static bool TryProjectBodyMeasuredPoint(Vector3 world, out Vector2 screen)
        {
            screen = default;
            Camera cam = Camera.main;
            if (cam == null)
            {
                var all = Camera.allCameras;
                if (all != null && all.Length > 0) cam = all[0];
            }
            if (cam == null) return false;
            Vector3 vp = cam.WorldToScreenPoint(world);
            if (!IsValidScreenPoint(vp) || vp.z <= 0f) return false;
            screen = new Vector2(vp.x, vp.y);
            return true;
        }

        // [101차 F-1] 구 [98차 F3] 눌러투영 폴백/정면앵커 투영 메서드는 삭제 — 본체 단일 앵커로 수렴.
        //   [99차 F-A] A-1 프러스텀 반폭 눌러투영 산식/스케일 폭발 방어 이력은 본 주석 이력으로 보존.

        /// <summary>[94차 F4] 대상 시각 bounds 합산 — TargetScreenProjector.GetTargetBounds 동일 규칙의
        /// 호출처 미러(NPCDialoguePanelUTK GetAnchorVisualBounds 선례). 프로젝터는 무수정 — 본체 앵커 산출용([101차 F-1]).</summary>
        private static Bounds GetTargetVisualBounds(Component target, out bool has)
        {
            has = false;
            Bounds b = default;
            var rends = target.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < rends.Length; i++)
            {
                if (rends[i] == null) continue;
                if (!has) { b = rends[i].bounds; has = true; }
                else b.Encapsulate(rends[i].bounds);
            }
            return b;
        }

        /// <summary>[94차 F4 → 101차 F-1] 본체 앵커 투영 프록시 확보 — 렌더러 없는 Transform(NPCDialoguePanelUTK
        /// EnsureAnchorProxy 선례 — 씬 전환 파괴 시 재생성). 공용 유틸 폴백 산식(position+up×0)이
        /// 프록시 지점을 그대로 투영하도록 유도한다.</summary>
        private static void EnsureBodyAnchorProxy()
        {
            if (_bodyAnchorProxy != null) return;
            var go = new GameObject("InteractionPanelBodyAnchorProxy");
            _bodyAnchorProxy = go.transform;
        }

        /// <summary>
        /// [94차 F4] F키 개방 즉시 정지 — 주민(NPCAmbientDialogue)=VillagerWanderer.PauseForDialogue 재사용
        /// (멱등 — '대화하기' 클릭 경로/PlayerMovement 브리지와 중복 무해). 마을NPC(TerritoryNPCBehaviour)=
        /// 배회 로직 없음(실측 — TerritoryNPCSpawner 354행 정적 상호작용 전용 컴포넌트) → 정지 API 불요(로그만).
        /// 병사/영주는 기존 정지 체계 존재 — 무간섭(정지 대상 추적에서 제외).
        /// </summary>
        private void PauseTargetWander(object target)
        {
            if (target is NPCAmbientDialogue villager)
            {
                var wanderer = villager.GetComponent<VillagerWanderer>();   // UI→Systems 참조(asmdef 계약) — 캐스트 접근
                if (wanderer != null)
                {
                    wanderer.PauseForDialogue();
                    _wanderPausedTarget = target;
                    ArmPauseWatchdog(villager);   // [96차 F2] 패널 소유 정지 — 안전망 감시 개시(15s 초과 자동 재개).
                    Debug.Log($"[InteractionPanelUTK] [94차 F4] 패널 개방 즉시 정지 — 주민 {villager.DisplayName} (PauseForDialogue)");
                }
            }
            else if (target is TerritoryNPCBehaviour townNpc)
            {
                _wanderPausedTarget = null;   // 정지 불요(배회 없음 — 실측) — 재개 대상 아님
                Debug.Log($"[InteractionPanelUTK] [94차 F4] 패널 개방 — 마을NPC {townNpc.NPCData.NpcName} kind={_targetKind} (배회 로직 없음 — 정지 불요)");
            }
            else
            {
                _wanderPausedTarget = null;   // 병사/영주 — 기존 로직 유지(이미 정지 체계)
            }
        }

        /// <summary>
        /// [94차 F4 → 96차 F2] 패널 닫힘 전(全) 경로 수렴 재개(EnsureResumeOnAnyClose) — Hide 오버라이드
        /// (Close/자동 닫힘 공통), PlacementTick 자동 닫힘, 부모 DetachFromPanel, 개방 진입(대상 클리어/변경)
        /// 전부 이 단일 메서드로 수렴(스펙 결함 2 수리 1 — 봉인 해소). 재개 로그 신설(스펙 수리 2 — 기존
        /// 재개 로그 부재가 판별 불가의 원인): `[94차 F4] 패널 닫힘 — 주민 ... 배회 재개`.
        /// 멱등: 미정지/파괴 대상은 무재개. 대화 소유권 가드(94차) 유지 — 해당 대상이 대화 카메라 세션 중이면
        /// 재개를 대화 종료(NPCAmbientDialogue.EndDialoguePose)에 위임(대화 중 재개 부작용 금지).
        /// </summary>
        private void EnsureResumeOnAnyClose()
        {
            var paused = _wanderPausedTarget as NPCAmbientDialogue;
            _wanderPausedTarget = null;
            if (paused == null)
            {
                DisarmPauseWatchdog();   // [96차 F2] 수렴 시 안전망 해제(미정지/파괴 — 멱등).
                return;   // 미정지/파괴 — 무재개(멱등)
            }
            if (DialogueCameraDirector.IsActive && DialogueCameraDirector.Target == paused)
            {
                // [98차 F1] F1-4 위임 경로 안전망 이중화 — 구 DisarmPauseWatchdog(소유권 이전) 폐기:
                //   위임 대상(EndDialoguePose 수신처) 감시 유지 — 위임 수신처가 죽어도 15s 워치독이 재개 보장
                //   (근거: 테스트 9 — 위임 재개 로그 전무로 성패 판별 불가 + Disarm으로 안전망까지 해제).
                Debug.Log($"[InteractionPanelUTK] [96차 F2] 닫힘 재개 위임 — 주민 {paused.DisplayName} (대화 카메라 세션 소유 — EndDialoguePose 재개 / [98차 F1] 워치독 유지)");
                return;   // 대화 소유 — 위임(안전망 유지 — [98차 F1])
            }
            DisarmPauseWatchdog();   // [96차 F2] 직접 재개 경로 — 즉시 재개 후 소유권 패널 유지(감시 해제).
            var wanderer = paused.GetComponent<VillagerWanderer>();
            if (wanderer == null) return;
            wanderer.ResumeAfterDialogue();
            // [96차 F2] 재개 로그 — 정지 로그와 쌍 대조 가능 형식(스펙 지정 문구: 패널 닫힘 — 주민 ... 배회 재개).
            Debug.Log($"[InteractionPanelUTK] [94차 F4] 패널 닫힘 — 주민 {paused.DisplayName} 배회 재개 (ResumeAfterDialogue)");
        }

        // ── [96차 F2] 정지 안전망 — 15s 초과 자동 재개(누락 닫힘 경로 최후 방어) ──

        /// <summary>[96차 F2] 안전망 타임아웃 — 패널 소유 정지의 최대 허용 시간(초). 초과 시 자동 재개+경고 로그
        /// (스펙 결함 2 수리 3 — 어떤 누락 경로에도 회복). 간단한 정적 타이머(패널 측 PlacementTick/스케줄 감시).</summary>
        private const float PauseWatchdogTimeoutSeconds = 15f;
        private static NPCAmbientDialogue _watchTarget;         // 감시 대상(패널 소유 정지 — 수렴/위임 시 해제)
        private static float _watchStartedRealtime;             // 정지 개시 시각(realtime — timeScale 무관 실측)
        private static bool _watchActive;                       // 감시 활성 플래그
        private IVisualElementScheduledItem _pauseWatchTask;    // 1s 주기 감시 — Hide(display:none) 후에도 요소가
                                                                //   패널에 부착 유지되므로 스케줄러 틱 지속(닫힘 누락 감시).

        /// <summary>[96차 F2] 안전망 가동 — 정지 개시 시각 기록 + 1s 주기 감시 개시(멱등 — 기존 태스크 재사용).</summary>
        private void ArmPauseWatchdog(NPCAmbientDialogue villager)
        {
            _watchTarget = villager;
            _watchStartedRealtime = Time.realtimeSinceStartup;
            _watchActive = true;
            if (_pauseWatchTask == null)
                _pauseWatchTask = schedule.Execute(PauseWatchdogTick).Every(1000L);
            else
                _pauseWatchTask.Resume();
        }

        /// <summary>[96차 F2] 안전망 해제 — 수렴 재개/대화 위임 시(소유권이 패널을 떠남 → 이중 강제 재개 방지).</summary>
        private void DisarmPauseWatchdog()
        {
            _watchActive = false;
            _watchTarget = null;
            _pauseWatchTask?.Pause();
        }

        private void PauseWatchdogTick()
        {
            if (!_watchActive)
            {
                _pauseWatchTask?.Pause();   // 감시 비활성 — 자가 정지(스케줄 스팸 방지).
                return;
            }
            PauseWatchdogCheck();
        }

        /// <summary>[96차 F2 → 98차 F1] 안전망 판정 — 패널 소유 정지 15s 초과 시 자동 재개+경고. 대화 소유권
        /// 가드 정합 — 대화 카메라 세션 중에는 강제 재개 금지(94차 위임 계약 — EndDialoguePose가 재개 담당).
        /// [98차 F1] F1-4 안전망 이중화 갱신: ①대화 소유 중에도 감시 유지(구 무조건 클리어 폐기 — End 후
        /// 재판정) ②이미 재개된 위임(EndDialoguePose 성공)은 조용히 해제(스팸/이중 재개 방지)
        /// ③위임 수신처 실패(_paused 잔존) 시 대화 종료 후 자동 재개(영구 정지 봉합).</summary>
        private static void PauseWatchdogCheck()
        {
            if (!_watchActive) return;
            if (Time.realtimeSinceStartup - _watchStartedRealtime < PauseWatchdogTimeoutSeconds) return;
            var paused = _watchTarget;
            if (paused == null)
            {
                _watchActive = false;   // 파괴 — 기록 해제만.
                _watchTarget = null;
                return;
            }
            var wanderer = paused.GetComponent<VillagerWanderer>();
            if (wanderer == null)
            {
                _watchActive = false;
                _watchTarget = null;
                return;
            }
            if (DialogueCameraDirector.IsActive && DialogueCameraDirector.Target == paused)
                return;   // 대화 소유 — 강제 재개 금지([98차 F1] 감시 유지 — 세션 종료 후 재판정).
            if (!wanderer.IsDialoguePaused)
            {
                // [98차 F1] 위임 재개 성공(EndDialoguePose 완료) — 감시 완료(조용히 해제 — 로그 스팸 방지).
                _watchActive = false;
                _watchTarget = null;
                if (_instance != null) _instance._wanderPausedTarget = null;   // 잔존 소유 정리(멱등).
                return;
            }
            _watchActive = false;
            _watchTarget = null;
            wanderer.ResumeAfterDialogue();
            if (_instance != null) _instance._wanderPausedTarget = null;   // [96차 F2] 소유 해제 — 재개 시 IsHoldingPause 잔존 방지(북키핑).
            Debug.LogWarning($"[InteractionPanelUTK] [96차 F2] 안전망 — 정지 {PauseWatchdogTimeoutSeconds:F0}s 초과 자동 재개 — 주민 {paused.DisplayName} (누락 닫힘 경로 회복)");
        }

        /// <summary>[72차] 개방 중 이동 추적 — 1s 폴링: 대상 생존 시 재배치, 사망/파괴/비활성 시 패널 닫힘.</summary>
        private void StartPlacementTracking(object target)
        {
            _trackedTarget = target;
            StopPlacementTracking();
            _placeTask = schedule.Execute(PlacementTick).Every(PlacePollMs);
        }

        private void StopPlacementTracking()
        {
            if (_placeTask == null)
                return;
            _placeTask.Pause();
            _placeTask = null;
        }

        private void PlacementTick()
        {
            PauseWatchdogCheck();   // [96차 F2] 패널 측 감시 경로 — 개방 중 안전망 공동 감시(스케줄 틱과 동일 로직).
            if (!IsOpen)
            {
                StopPlacementTracking();
                EnsureResumeOnAnyClose();   // [96차 F2] 이례적 개방 플래그 소실 — 재개 수렴(멱등).
                return;
            }
            var tracked = _trackedTarget as Component;
            if (tracked == null || !tracked.gameObject.activeInHierarchy)
            {
                EnsureResumeOnAnyClose();   // [96차 F2] 대상 소멸/클리어 자동 닫힘 — 재개 수렴(Close→Hide와 이중 무해·멱등).
                Close();
                return;
            }
            if (tracked is GuardPlaceholder guard && !guard.IsAlive)
            {
                EnsureResumeOnAnyClose();   // [96차 F2] 사망 자동 닫힘 — 재개 수렴(멱등).
                Close();
                return;
            }
            PlaceAtTarget(_trackedTarget);
        }

        // ── [97차 F3] 다음 프레임 재배치 1회 + 부착 검증 로그 ──

        /// <summary>[97차 F3] 다음 프레임 재배치 1회성 태스크 — 개방 시마다 재스케줄(이전 태스크 Pause — 멱등).</summary>
        private IVisualElementScheduledItem _nextFrameReplaceTask;

        /// <summary>
        /// [97차 F3] 다음 프레임 재배치 1회 — 개방 첫 프레임은 레이아웃 미확정(루트 스케일/스타일 방금 적용)으로
        /// 배치가 어긋나 보이던 순간 제거(스펙 결함 3 수리 1). 스케줄러 1회(다음 프레임 — StartingIn(16),
        /// SleepPanelUTK CenterOnParent 16ms 선례). 개방 프레임 즉시 배치([94차 F4] 기존 계약)는 그대로 —
        /// 본 보정은 추가 1회. 재개방 시 이전 1회성 태스크 Pause 후 재스케줄(멱등).
        /// </summary>
        private void ScheduleNextFrameReplacement()
        {
            _nextFrameReplaceTask?.Pause();
            _nextFrameReplaceTask = schedule.Execute(NextFrameReplacementTick).StartingIn(16);
        }

        /// <summary>
        /// [97차 F3] 다음 프레임 재배치 틱 — 개방 유지 시에만 PlaceAtTarget 재호출(StartingIn 단발 — 1회 후 소멸).
        /// 닫힘/대상 소실 시 무배치(스케줄 스팸 방지). 재배치도 부착 검증 로그 동반(PlaceAtTarget 내부 —
        /// 개방 프레임(레이아웃 전) vs 다음 프레임(레이아웃 후) worldBound 대조가 본 진단의 사용법).
        /// </summary>
        private void NextFrameReplacementTick()
        {
            if (!IsOpen || _trackedTarget == null)
                return;   // [97차 F3] 닫힘/대상 소실 — 무배치(멱등).
            PlaceAtTarget(_trackedTarget);   // [97차 F3] 레이아웃 확정 후 재배치 1회(부착 검증 로그 동반).
        }

        /// <summary>
        /// [97차 F3 → 98차 F3] 부착 검증 로그 — 배치 시 패널 worldBound와 NPC screenPx를 같이 즉격(스펙 결함 3 수리 2 —
        /// 부착 실패 체감 시 개방 순간 배치 로그와 NPC 화면좌표 대조 즉격). worldBound는 레이아웃 패스 전
        /// 구값일 수 있음 — 다음 프레임 재배치 로그와 대조. screenPx null = 총 투영 실패(무배치) 경로 즉격.
        /// [98차 F3] F3-4 부착 판정 추가 — 배치 후 npc screenPx와 패널 중심 거리 ≤ 120px = 부착 성공
        /// (거리 산출은 worldBound 비의존 — 스타일 배치값 + ResolvePanelBoundsPx 실측 크기, F3-1 계약).
        /// [99차 F-A] A-3 폴백 숨김 사유 — reason 인수(`폴백=비가시숨김…`/`폴백=재표시…`)를 로그 말미에 병기.
        /// [100차 F-1] 시맨틱 부착 판정 병기 — 패널하단중앙↔머리앵커(투영점) 거리 ≤ 24px = 부착(성공) —
        /// 기존 중심 거리 판정(≤120px)과 병기(머리 위 시맨틱 계약 전용 판정).
        /// [101차 F-1] 본체 실측 대조 판정 병기 — 배치에 쓴 앵커 좌표와 본체 실측 좌표(직접 투영 경로)를
        /// 별도 산출해 대조(자기참조 방지): 몸중심 x 편차 ≤20px AND 패널하단↔머리 y ≤24px = 부착(성공).
        /// </summary>
        private void LogPlacementVerify(Vector2? screenPx, string path, string reason = null)
        {
            LogPlacementVerify(screenPx, path, null, reason);   // [101차 F-1] 본체 실측 대상 부재 — 대조 판정 생략 위임.
        }

        private void LogPlacementVerify(Vector2? screenPx, string path, Component measuredTarget, string reason = null)
        {
            Rect wb = worldBound;
            string px = screenPx.HasValue ? $"({screenPx.Value.x:F0},{screenPx.Value.y:F0})" : "투영실패(z<0)";
            // [98차 F3] F3-4 부착 판정 — npc screenPx → 루트 px 변환(npc 최근접 가장자리 클램프 전 좌표 대조).
            string verdict;
            string semantic = "머리부착=판정불가";   // [100차 F-1] 시맨틱 판정(투영 실패/루트 크기 미확정 시 불가).
            string bodyVerdict = "본체부착=판정불가(투영실패 — 무배치)";   // [101차 F-1] 본체 실측 대조 판정.
            if (!screenPx.HasValue)
            {
                verdict = "부착=판정불가(투영실패 — 무배치)";
            }
            else
            {
                var root = UIToolkitBootstrap.UIRoot;
                float rootWidth = root != null ? root.resolvedStyle.width : 0f;
                float rootHeight = root != null ? root.resolvedStyle.height : 0f;
                bool rootValid = rootWidth > 0f && rootHeight > 0f
                                 && !float.IsNaN(rootWidth) && !float.IsNaN(rootHeight);
                if (rootValid)
                {
                    float npcCanvasX = screenPx.Value.x * FigmaCanvasLayout.CanvasWidth / Screen.width;
                    float npcCanvasY = (Screen.height - screenPx.Value.y) * FigmaCanvasLayout.CanvasHeight / Screen.height;
                    Rect scaledNpc = FigmaCanvasLayout.ScaleRect(
                        new Rect(npcCanvasX, npcCanvasY, 0f, 0f), new Vector2(rootWidth, rootHeight));
                    Rect panelBounds = ResolvePanelBoundsPx(rootWidth, rootHeight);
                    float centerX = style.left.value.value + panelBounds.width * 0.5f;
                    float centerY = style.top.value.value + panelBounds.height * 0.5f;
                    float dist = Vector2.Distance(
                        new Vector2(centerX, centerY), new Vector2(scaledNpc.x, scaledNpc.y));
                    verdict = dist <= AttachVerifyMaxDistancePx
                        ? $"부착={dist:F0}px(성공)"
                        : $"부착={dist:F0}px(실패 — {AttachVerifyMaxDistancePx:F0}px 초과)";
                    // [100차 F-1] 시맨틱 부착 판정 — 패널 하단 중앙 ↔ 머리앵커(투영점) 거리 ≤ 24px = 부착(성공).
                    //   배치 산식(하단 중앙 = 앵커 위 6px)과 짝 — 화면 클램프로 패널이 밀리면 실패 판정.
                    float bottomCenterX = style.left.value.value + panelBounds.width * 0.5f;
                    float bottomCenterY = style.top.value.value + panelBounds.height;
                    float headDist = Vector2.Distance(
                        new Vector2(bottomCenterX, bottomCenterY), new Vector2(scaledNpc.x, scaledNpc.y));
                    semantic = headDist <= HeadAttachMaxDistancePx
                        ? $"머리부착={headDist:F0}px(성공)"
                        : $"머리부착={headDist:F0}px(실패 — {HeadAttachMaxDistancePx:F0}px 초과)";
                    // [101차 F-1] 본체 실측 대조 판정 — 본체 실측 좌표(직접 투영)와 앵커 경유 배치를 별도 산출해 대조.
                    bodyVerdict = ComputeBodyAttachVerdict(measuredTarget, bottomCenterX, bottomCenterY);
                }
                else
                {
                    verdict = "부착=루트크기미확정";
                    bodyVerdict = "본체부착=루트크기미확정";
                }
            }
            // [99차 F-A] A-3 폴백 숨김 사유 부기 — reason 존재 시 검증 로그 말미에 병기(비가시숨김/재표시).
            string reasonSuffix = string.IsNullOrEmpty(reason) ? "" : $" 사유={reason}";
            Debug.Log($"[InteractionPanelUTK] [97차 F3] 부착 검증 — 경로={path} npc screenPx={px} panel worldBound=({wb.x:F0},{wb.y:F0},{wb.width:F0}x{wb.height:F0}) 배치=({style.left.value.value:F0},{style.top.value.value:F0}) {verdict} [100차 F-1] {semantic}{reasonSuffix} [101차 F-1] {bodyVerdict}");
        }

        /// <summary>
        /// [101차 F-1] 본체 실측 대조 판정 — 배치에 쓴 앵커 좌표(프록시+공용 유틸 경유)와 본체 실측 좌표
        /// (직접 투영 경로 — TryProjectBodyMeasuredPoint)를 별도 산출해 대조(자기참조 방지 — 시맨틱 판정이
        /// 같은 앵커끼리 대조해 화면 측면 이탈을 못 잡은 [97차 F3] 실측의 수리). ①몸중심 x 편차 ≤20px
        /// (bounds 중심 실측 x — x축 판정 신설) ②패널하단↔머리 y ≤24px(본체 앵커 고도 실측 y — Foot 본 y +
        /// 1.95m/Foot 부재 시 bounds maxY + 0.15m). AND 성립 = 본체부착(성공). 측정 불가(대상 부재/직접 투영
        /// z≤0/루트 크기 미확정) 시 사유 문자열(판정 불명).
        /// </summary>
        private static string ComputeBodyAttachVerdict(Component measuredTarget, float bottomCenterX, float bottomCenterY)
        {
            if (measuredTarget == null)
                return "본체부착=측정대상부재";
            var root = UIToolkitBootstrap.UIRoot;
            float rootWidth = root != null ? root.resolvedStyle.width : 0f;
            float rootHeight = root != null ? root.resolvedStyle.height : 0f;
            if (rootWidth <= 0f || rootHeight <= 0f || float.IsNaN(rootWidth) || float.IsNaN(rootHeight))
                return "본체부착=루트크기미확정";
            Bounds mb = GetTargetVisualBounds(measuredTarget, out bool mHas);
            Vector3 baseXZ = mHas ? mb.center : measuredTarget.transform.position;
            Vector3 headPoint = new Vector3(baseXZ.x, ComputeBodyAnchorHeight(measuredTarget, mb, mHas), baseXZ.z);
            if (!TryProjectBodyMeasuredPoint(baseXZ, out Vector2 bodyPx)
                || !TryProjectBodyMeasuredPoint(headPoint, out Vector2 headPx))
                return "본체부착=측정실패(직접투영 z≤0)";
            // 본체 실측 screenPx → 루트 px(배치 좌표계 동일 변환 — y축 반전/캔버스 스케일).
            float bodyRootY = (Screen.height - bodyPx.y) * FigmaCanvasLayout.CanvasHeight / Screen.height;
            float headRootY = (Screen.height - headPx.y) * FigmaCanvasLayout.CanvasHeight / Screen.height;
            Rect scaledBody = FigmaCanvasLayout.ScaleRect(
                new Rect(bodyPx.x * FigmaCanvasLayout.CanvasWidth / Screen.width, bodyRootY, 0f, 0f), new Vector2(rootWidth, rootHeight));
            Rect scaledHead = FigmaCanvasLayout.ScaleRect(
                new Rect(headPx.x * FigmaCanvasLayout.CanvasWidth / Screen.width, headRootY, 0f, 0f), new Vector2(rootWidth, rootHeight));
            float devX = Mathf.Abs(bottomCenterX - scaledBody.x);
            float devY = Mathf.Abs(bottomCenterY - scaledHead.y);
            bool attached = devX <= BodyCenterMaxDeviationPx && devY <= HeadAttachMaxDistancePx;
            return attached
                ? $"본체부착=x편차={devX:F0}px y={devY:F0}px(성공 — x≤{BodyCenterMaxDeviationPx:F0}·y≤{HeadAttachMaxDistancePx:F0})"
                : $"본체부착=x편차={devX:F0}px y={devY:F0}px(실패 — x≤{BodyCenterMaxDeviationPx:F0}·y≤{HeadAttachMaxDistancePx:F0} 필요)";
        }

        /// <summary>
        /// [98차 F3] F3-2 후배치 보정 — GeometryChangedEvent에서 실측 크기로 중앙 정렬 재교정([97차 F3]
        /// 다음 프레임 스케줄러 계승 + 보강). 크기 실측 불가(NaN)여도 스킵하지 않고 기본 크기 사용
        /// (ResolvePanelBoundsPx ③단 폴백). 무루프 가드 — 실측 크기가 이전 재교정과 동일(0.5px 미만 차)이면
        /// 무재배치(재배치가 크기를 바꾸지 않는 한 이벤트 체인 종료).
        /// </summary>
        private void OnGeometryChangedRecenter(GeometryChangedEvent evt)
        {
            if (!IsOpen || _trackedTarget == null) return;
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;
            float rootWidth = root.resolvedStyle.width;
            float rootHeight = root.resolvedStyle.height;
            if (rootWidth <= 0f || rootHeight <= 0f || float.IsNaN(rootWidth) || float.IsNaN(rootHeight))
                return;
            Rect panelBounds = ResolvePanelBoundsPx(rootWidth, rootHeight);
            if (Mathf.Abs(panelBounds.width - _lastRecenteredSize.x) < 0.5f &&
                Mathf.Abs(panelBounds.height - _lastRecenteredSize.y) < 0.5f)
                return;   // [98차 F3] 크기 변화 없음 — 무재배치(이벤트 루프 방지).
            _lastRecenteredSize = new Vector2(panelBounds.width, panelBounds.height);
            PlaceAtTarget(_trackedTarget);   // [98차 F3] 실측 크기로 중앙 정렬 재교정(부착 검증 로그 동반).
        }

        private static Vector2 GetLayoutSize(HoverTargetClassifier.TargetKind kind)
        {
            // The current Systems dispatcher routes five kinds to these four-action
            // frames; variants without a proven caller are not exposed here.
            switch (kind)
            {
                case HoverTargetClassifier.TargetKind.EnemyGuard: // 81:4
                case HoverTargetClassifier.TargetKind.Ally:       // 81:51
                case HoverTargetClassifier.TargetKind.NPC:        // 81:91
                case HoverTargetClassifier.TargetKind.ShopNPC:    // 81:134
                case HoverTargetClassifier.TargetKind.Lord:       // 82:2
                    return new Vector2(PanelWidth, StandardPanelHeight);
                case HoverTargetClassifier.TargetKind.ArenaManager:   // [121차] 261:3 — 288×65
                    return new Vector2(288f, 65f);
                case HoverTargetClassifier.TargetKind.Fishing:        // [144차 D2-FISH] 163:4 — 288×60
                    return new Vector2(288f, 60f);
                case HoverTargetClassifier.TargetKind.Bed:            // [144차 D2-SLEEP] 163:45 — 288×60
                    return new Vector2(288f, 60f);
                case HoverTargetClassifier.TargetKind.Door:           // [144차 D2-LOCK] 163:64/83 — 288×64.8
                    return new Vector2(288f, 64.8f);
                default:
                    return new Vector2(PanelWidth, StandardPanelHeight);
            }
        }

        private void RebuildActions()
        {
            if (_actionGrid == null)
                return;

            _actionGrid.Clear();
            // [63차 — 피그마 5종 패널 정합] 대상별 라벨 구성 + 첫 버튼(1행 좌측) 하이라이트 #58A6FF/700.
            //   81:4 적 병사=상태보기★/대화하기/뇌물주기/포섭하기 · 81:51 아군=상태보기★/대화하기/물약주기/음식주기
            //   [144차 P0-INT #26] Ally 분기 정정 — 뇌물주기/포섭하기(잘못된 하드코딩) → 물약주기/음식주기(Figma 81:51).
            //   · 81:91 NPC=상태보기★/대화하기/선물주기/퀘스트 · 81:134 상점=상태보기★/대화하기/상점/세금 제안
            //   · 82:2 영주(Encounter 부재)=대화하기★/음식주기/선물주기/동맹제안 · 93:185 제압 영주=처형하기★/풀어주기 1행.
            switch (_targetKind)
            {
                case HoverTargetClassifier.TargetKind.EnemyGuard:
                    AddAction("상태보기", 0, true);
                    AddAction("대화하기", 1);
                    AddAction("뇌물주기", 2);
                    AddAction("포섭하기", 3);
                    break;
                case HoverTargetClassifier.TargetKind.Ally:
                    // [144차 P0-INT #26] Figma 81:51 조합 — 물약주기/음식주기(IsActionSupported·핸들러 실존 경로 연결).
                    AddAction("상태보기", 0, true);
                    AddAction("대화하기", 1);
                    AddAction("물약주기", 2);
                    AddAction("음식주기", 3);
                    break;
                case HoverTargetClassifier.TargetKind.NPC:
                    AddAction("상태보기", 0, true);
                    AddAction("대화하기", 1);
                    AddAction("선물주기", 2);
                    AddAction("퀘스트", 3);
                    break;
                case HoverTargetClassifier.TargetKind.ShopNPC:
                    AddAction("상태보기", 0, true);
                    AddAction("대화하기", 1);
                    AddAction("상점", 2);
                    AddAction("세금 제안", 3);   // [144차 P2-INT #28] Figma 라벨 공백 정합 — 라우팅 키 3곳 동기 갱신
                    break;
                case HoverTargetClassifier.TargetKind.Lord:
                    if (InvadedLordEncounter.FindOnTarget(_target) is InvadedLordEncounter encounter)
                    {
                        if (encounter.IsDead)
                        {
                            // [63차] 93:185 제압 영주 — 처형하기★/풀어주기 1행 2버튼(패널 축소 240×54).
                            AddAction("처형하기", 0, true);
                            AddAction("풀어주기", 1);
                            _actionGrid.style.height = SingleRowGridHeight * _uiScaleY;
                        }
                        else
                        {
                            AddAction("대화하기", 0, true);
                            AddAction("공격하기", 1);
                        }
                    }
                    else
                    {
                        AddAction("대화하기", 0, true);
                        AddAction("음식주기", 1);
                        AddAction("선물주기", 2);
                        AddAction("동맹제안", 3);
                    }
                    break;
                case HoverTargetClassifier.TargetKind.LordDaughter:
                    // [126차] 영주 딸 — 240×82 기본 그리드 재사용(3버튼: 대화/선물/프로포즈).
                    //   대화하기(항상)/들꽃 선물(호감도<100)/프로포즈(호감도≥80) — 활성 판정은 IsActionSupported.
                    AddAction("대화하기", 0, true);
                    AddAction("들꽃 선물", 1);
                    AddAction("프로포즈", 2);
                    break;
                case HoverTargetClassifier.TargetKind.ArenaManager:
                    // [121차] 피그마 261:3 — 288×65 컴팩트 2액션(대화하기 액센트/베팅하기 뮤트).
                    AddAction("대화하기", 0, true);
                    AddAction("베팅하기", 1);
                    // 261:3 그리드 재조정 — 액션 행 (10,10) 269×36, 버튼 131×36 @ x=0/138.
                    //   [144차 P1-ACT #6 승계] 베팅하기 x=138.0(=130.8+gap 7.2) — 구 148(gap 17, 행 우단 279>269 오버플로) 정정.
                    _actionGrid.style.left = 10f * _uiScaleX;
                    _actionGrid.style.top = 10f * _uiScaleY;
                    _actionGrid.style.width = 269f * _uiScaleX;
                    _actionGrid.style.height = 36f * _uiScaleY;
                    int arenaBtn = 0;
                    foreach (var child in _actionGrid.Children())
                    {
                        if (child is Button arenaBtnEl)
                        {
                            arenaBtnEl.style.left = (arenaBtn == 0 ? 0f : 138f) * _uiScaleX;
                            arenaBtnEl.style.top = 0f;
                            arenaBtnEl.style.width = 131f * _uiScaleX;
                            arenaBtnEl.style.height = 36f * _uiScaleY;
                            arenaBtn++;
                        }
                    }
                    break;
                case HoverTargetClassifier.TargetKind.Fishing:
                {
                    // [144차 D2-FISH] 피그마 163:4 — 낚시 패널은 표준 2열 그리드가 아닌 단일 액션 카드.
                    //   ActionFishing 카드 266.4×38.4 @(그리드 0,0) fill #30363D@0.8 · stroke #58A6FF 1.2 · r4.8,
                    //   hook 아이콘 16.8(163:20/21 — 벡터→이모지 근사), "낚시하기" 13.2/700 #58A6FF(163:22).
                    _actionGrid.style.left = 11f * _uiScaleX;    // 163:17 — 패널-로컬 (11,11)
                    _actionGrid.style.top = 11f * _uiScaleY;
                    _actionGrid.style.width = 266.4f * _uiScaleX;
                    _actionGrid.style.height = 38.4f * _uiScaleY;

                    var card = new VisualElement { name = "ActionFishing" };
                    card.style.position = Position.Absolute;
                    card.style.left = 0f; card.style.top = 0f;
                    card.style.width = 266.4f * _uiScaleX;
                    card.style.height = 38.4f * _uiScaleY;
                    card.style.flexDirection = FlexDirection.Row;
                    card.style.alignItems = Align.Center;
                    card.style.backgroundColor = new Color(0x30 / 255f, 0x36 / 255f, 0x3D / 255f, 0.8f);
                    UTKTheme.ApplyElementBorder(card, UTKTheme.Accent);   // #58A6FF 1.2
                    UTKTheme.ApplyRadius(card, 4.8f);
                    // 커서: UI Toolkit 런타임 pointer 커서는 텍스처 요구 — 미적용(기록).

                    var hookIcon = new Label("🎣");
                    hookIcon.style.fontSize = 16.8f;   // 163:20 — 16.8(이모지 근사 기록)
                    hookIcon.style.marginLeft = 11f * _uiScaleX;   // 카드-로컬 x11
                    hookIcon.style.unityTextAlign = TextAnchor.MiddleCenter;
                    card.Add(hookIcon);

                    var fishLabel = new Label("낚시하기");
                    fishLabel.style.fontSize = 13.2f * Mathf.Min(_uiScaleX, _uiScaleY);
                    fishLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    fishLabel.style.color = UTKTheme.Accent;
                    fishLabel.style.marginLeft = 7.2f * _uiScaleX;   // 아이콘(16.8) 후 라벨 x35 — 11+16.8+7.2=35
                    card.Add(fishLabel);

                    card.RegisterCallback<ClickEvent>(_ =>
                        ActionRequested?.Invoke(_targetKind, "낚시하기", _target));
                    _actionGrid.Add(card);
                    break;
                }
                case HoverTargetClassifier.TargetKind.Bed:
                {
                    // [144차 D2-SLEEP] 피그마 163:45 — 수면 컴팩트 패널(288×60): 전폭 단일 카드 "잠자기"
                    //   (266.4×38.4 @(11,11), fill #30363D@0.8 · stroke #58A6FF 1.2 · r4.8, moon 아이콘 16.8 —
                    //   벡터→글리프 ☾ 근사 기록, "잠자기" 13.2/700 #58A6FF 163:63). 클릭 = Bed.OnInteract() —
                    //   E키(잠자기/세이브)와 동일 경로, 수면 창/스폰 저장 계약 유지.
                    _actionGrid.style.left = 11f * _uiScaleX;    // 163:46 그리드 (11,11)
                    _actionGrid.style.top = 11f * _uiScaleY;
                    _actionGrid.style.width = 266.4f * _uiScaleX;
                    _actionGrid.style.height = 38.4f * _uiScaleY;

                    var sleepCard = new VisualElement { name = "ActionSleep" };
                    sleepCard.style.position = Position.Absolute;
                    sleepCard.style.left = 0f; sleepCard.style.top = 0f;
                    sleepCard.style.width = 266.4f * _uiScaleX;
                    sleepCard.style.height = 38.4f * _uiScaleY;
                    sleepCard.style.flexDirection = FlexDirection.Row;
                    sleepCard.style.alignItems = Align.Center;
                    sleepCard.style.backgroundColor = new Color(0x30 / 255f, 0x36 / 255f, 0x3D / 255f, 0.8f);
                    UTKTheme.ApplyElementBorder(sleepCard, UTKTheme.Accent);   // #58A6FF 1.2
                    UTKTheme.ApplyRadius(sleepCard, 4.8f);

                    var moonIcon = new Label("☾");   // 163:61 moon 16.8 — 글리프 근사(기록)
                    moonIcon.style.fontSize = 16.8f;
                    moonIcon.style.color = UTKTheme.Accent;
                    moonIcon.style.marginLeft = 11f * _uiScaleX;
                    moonIcon.style.unityTextAlign = TextAnchor.MiddleCenter;
                    sleepCard.Add(moonIcon);

                    var sleepLabel = new Label("잠자기");   // 163:63 — 13.2/700 #58A6FF
                    sleepLabel.style.fontSize = 13.2f * Mathf.Min(_uiScaleX, _uiScaleY);
                    sleepLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    sleepLabel.style.color = UTKTheme.Accent;
                    sleepLabel.style.marginLeft = 7.2f * _uiScaleX;
                    sleepCard.Add(sleepLabel);

                    sleepCard.RegisterCallback<ClickEvent>(_ =>
                        ActionRequested?.Invoke(_targetKind, "잠자기", _target));
                    _actionGrid.Add(sleepCard);
                    break;
                }
                case HoverTargetClassifier.TargetKind.Door:
                {
                    // [144차 D2-LOCK] 피그마 163:64/83 — 자물쇠 컴팩트 패널(288×64.8): keycap-E 칩 + 라벨 단일 행
                    //   (266.4×43.2 @(11,11), keycap 21.6 r4.8 #58A6FF · "E" 12/700 #161B22, 라벨 13.2/700 #58A6FF).
                    //   163:64 영주실 문(LocationId=house_lord_room)="E키를 눌러 영주실 들어가기",
                    //   163:83 일반 잠긴 문="E키를 눌러 자물쇠 따기". 클릭 = E키와 동일(OnLockpickRequested 발화).
                    _actionGrid.style.left = 11f * _uiScaleX;
                    _actionGrid.style.top = 11f * _uiScaleY;
                    _actionGrid.style.width = 266.4f * _uiScaleX;
                    _actionGrid.style.height = 43.2f * _uiScaleY;

                    bool isLordRoom = _target is LockedDoor lordDoorCheck &&
                                      lordDoorCheck.LocationId == "house_lord_room";

                    var doorCard = new VisualElement { name = "ActionInteract" };
                    doorCard.style.position = Position.Absolute;
                    doorCard.style.left = 0f; doorCard.style.top = 0f;
                    doorCard.style.width = 266.4f * _uiScaleX;
                    doorCard.style.height = 43.2f * _uiScaleY;
                    doorCard.style.flexDirection = FlexDirection.Row;
                    doorCard.style.alignItems = Align.Center;
                    doorCard.style.backgroundColor = new Color(0x30 / 255f, 0x36 / 255f, 0x3D / 255f, 0.8f);
                    UTKTheme.ApplyElementBorder(doorCard, UTKTheme.Accent);
                    UTKTheme.ApplyRadius(doorCard, 4.8f);

                    var keycap = new VisualElement { name = "KeycapE" };   // 163:80/99 — 21.6×21.6 r4.8 #58A6FF
                    keycap.style.width = 21.6f;
                    keycap.style.height = 21.6f;
                    keycap.style.backgroundColor = UTKTheme.Accent;
                    UTKTheme.ApplyRadius(keycap, 4.8f);
                    keycap.style.marginLeft = 11f * _uiScaleX;
                    var keycapText = new Label("E");   // "E" 12/700 #161B22
                    keycapText.style.fontSize = 12f;
                    keycapText.style.unityFontStyleAndWeight = FontStyle.Bold;
                    keycapText.style.color = new Color(0x16 / 255f, 0x1B / 255f, 0x22 / 255f, 1f);
                    keycapText.style.unityTextAlign = TextAnchor.MiddleCenter;
                    keycap.Add(keycapText);
                    doorCard.Add(keycap);

                    var doorLabel = new Label(isLordRoom ? "E키를 눌러 영주실 들어가기" : "E키를 눌러 자물쇠 따기");   // 163:82/101
                    doorLabel.style.fontSize = 13.2f * Mathf.Min(_uiScaleX, _uiScaleY);
                    doorLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    doorLabel.style.color = UTKTheme.Accent;
                    doorLabel.style.marginLeft = 7.2f * _uiScaleX;
                    doorCard.Add(doorLabel);

                    doorCard.RegisterCallback<ClickEvent>(_ =>
                        ActionRequested?.Invoke(_targetKind, isLordRoom ? "영주실 들어가기" : "자물쇠 따기", _target));
                    _actionGrid.Add(doorCard);
                    break;
                }
            }
        }

        private void AddAction(string label, int index, bool highlighted = false)
        {
            int row = index / 2;
            int column = index % 2;
            var button = UTKButton.Create(label, () => ActionRequested?.Invoke(_targetKind, label, _target), UTKButton.Variant.Secondary);
            button.name = "Action_" + label;
            button.style.position = Position.Absolute;
            button.style.left = column * (ActionWidth + ColumnGap) * _uiScaleX;
            button.style.top = (row == 0 ? 0f : SecondRowTop) * _uiScaleY;
            button.style.width = ActionWidth * _uiScaleX;
            button.style.height = ActionHeight * _uiScaleY;
            button.style.marginTop = 0f;
            button.style.marginBottom = 0f;
            button.style.marginLeft = 0f;
            button.style.marginRight = 0f;
            button.style.flexShrink = 0f;
            button.style.fontSize = 13.2f * Mathf.Min(_uiScaleX, _uiScaleY);
            button.style.paddingLeft = 0f;
            button.style.paddingRight = 0f;
            button.style.whiteSpace = WhiteSpace.NoWrap;
            // [63차 — 피그마 82:2 등] 첫 버튼(하이라이트) = #58A6FF 700, 일반 = #8B949E 500.
            //   UTKButton의 내부 Label도 직접 칠함(버튼 루트 style.color는 하위 라벨에 상속되지 않을 수 있음).
            var textColor = highlighted ? HighlightText : NormalText;
            button.style.color = textColor;
            button.style.unityFontStyleAndWeight = highlighted ? FontStyle.Bold : FontStyle.Normal;
            var labelEl = button.Q<Label>();
            if (labelEl != null)
            {
                labelEl.style.color = textColor;
                labelEl.style.unityFontStyleAndWeight = highlighted ? FontStyle.Bold : FontStyle.Normal;
            }
            // [144차 P1-INT #15/#16/#18] 하이라이트 버튼 크롬 — bg #30363D@0.8 + 보더 #58A6FF 1.2 (Figma 81:4 등).
            if (highlighted)
            {
                button.style.backgroundColor = new StyleColor(new Color(0x30 / 255f, 0x36 / 255f, 0x3D / 255f, 0.8f));
                button.style.borderTopColor = button.style.borderBottomColor =
                    button.style.borderLeftColor = button.style.borderRightColor =
                        new StyleColor(new Color(0x58 / 255f, 0xA6 / 255f, 0xFF / 255f, 1f));
                button.style.borderTopWidth = button.style.borderBottomWidth =
                    button.style.borderLeftWidth = button.style.borderRightWidth = 1.2f;
                button.style.borderTopLeftRadius = button.style.borderTopRightRadius = 4.8f;
                button.style.borderBottomLeftRadius = button.style.borderBottomRightRadius = 4.8f;
            }
            // [144차 P0-INT #20/#21] 버튼 아이콘 16.8px(논리 14×k) — Resources/UI/icons 실에셋 근사 매핑 +
            //   좌측행 정렬(pad 9.6·gap 7.2 → 논리 8/6). UTKButton 텍스트 전용 한계 → 인라인 Row 정렬로 보강.
            var iconTex = LoadActionIcon(label);
            if (iconTex != null)
            {
                var icon = new VisualElement { name = "ActionIcon" };
                float s = Mathf.Min(_uiScaleX, _uiScaleY);
                icon.style.width = 14f * s;
                icon.style.height = 14f * s;
                icon.style.flexShrink = 0f;
                icon.style.backgroundImage = UTKTextureSafe.ToBackground(iconTex);
                icon.pickingMode = PickingMode.Ignore;
                button.style.flexDirection = FlexDirection.Row;      // 중앙정렬(uss) → 좌측행(Figma 7판본 공통)
                button.style.alignItems = Align.Center;
                button.style.justifyContent = Justify.FlexStart;
                button.style.paddingLeft = 8f;
                button.style.paddingRight = 8f;
                button.style.paddingTop = 8f;
                button.style.paddingBottom = 8f;
                button.Insert(0, icon);
                if (labelEl != null) labelEl.style.marginLeft = 6f;
            }
            button.SetEnabled(IsActionSupported(_targetKind, label, _target));
            _actionGrid.Add(button);
        }

        /// <summary>[144차 P0-INT #20] 액션 버튼 아이콘 로더 — Figma lucide 아이콘을 Resources/UI/icons 실에셋으로
        ///   근사 매핑(기록: eye→zoom, message-square→chat, coins→money, user-plus→tick, flask→icon_potion,
        ///   utensils→tea-cup, gift→heart, clipboard-list→icon_scroll, store→wallet, handshake→puzzle,
        ///   skull→axe, key→key, dices→money, sword→axe, flower-gift→leaf, crown-tax→icon_crown).</summary>
        private static readonly Dictionary<string, Texture2D> _iconCache = new Dictionary<string, Texture2D>();
        private static Texture2D LoadActionIcon(string label)
        {
            if (_iconCache.TryGetValue(label, out var cached))
                return cached;
            string path;
            switch (label)
            {
                case "상태보기":  path = "UI/icons/zoom";       break;
                case "대화하기":  path = "UI/icons/chat";       break;
                case "뇌물주기":  path = "UI/icons/money";      break;
                case "포섭하기":  path = "UI/icons/tick";       break;
                case "물약주기":  path = "UI/icon_potion";      break;
                case "음식주기":  path = "UI/icons/tea-cup";    break;
                case "선물주기":  path = "UI/icons/heart";      break;
                case "퀘스트":    path = "UI/icon_scroll";      break;
                case "상점":      path = "UI/icons/wallet";     break;
                case "세금 제안": path = "UI/icon_crown";       break;
                case "동맹제안":  path = "UI/icons/puzzle";     break;
                case "공격하기":  path = "UI/icons/axe";        break;
                case "베팅하기":  path = "UI/icons/money";      break;
                case "처형하기":  path = "UI/icons/axe";        break;
                case "풀어주기":  path = "UI/icons/key";        break;
                case "들꽃 선물": path = "UI/icons/leaf";       break;
                default:          path = null;                  break;
            }
            var tex = path != null ? Resources.Load<Texture2D>(path) : null;
            _iconCache[label] = tex;
            return tex;
        }

        private static bool IsActionSupported(HoverTargetClassifier.TargetKind kind, string label, object target)
        {
            switch (label)
            {
                case "상태보기":
                    return (kind == HoverTargetClassifier.TargetKind.EnemyGuard
                            || kind == HoverTargetClassifier.TargetKind.Ally)
                           && target is GuardPlaceholder;
                case "대화하기":
                    // [72차] 병사 액션 5종 활성화 — GuardPlaceholder 대상(아군/적병사 공통).
                    if (target is GuardPlaceholder talkGuard)
                        return true;   // 병사 대화 — 구창 동일 래퍼(BeginTalk) 경유.
                    if (target is InvadedLordEncounter invadedLord)
                        return invadedLord != null && invadedLord.State != InvadedLordEncounter.EncounterState.Resolved;
                    // [89차] 마을 주민(NPCAmbientDialogue) — F키 대화 경로(OpenForVillager) 활성.
                    if (target is NPCAmbientDialogue)
                        return true;
                    // [125차 후속9] 아레나 베팅 관리인 — Test_15 통일 경로(대화카메라+OpenForStatic) 활성.
                    //   근거: 구 IsActionSupported에 관리인 분기 부재 → 대화하기 버튼 SetEnabled(false)로
                    //   통일 경로 도달 불가. 베팅하기도 활성화(아래 case) — 261:3 뮤트 표기는 시각 스타일이지 기능 비활성이 아님 [125차 후속9].
                    if (target is ArenaBetManager)
                        return true;
                    // [126차] 영주 딸 — 대화하기는 항상 활성.
                    if (target is LordDaughterNPC)
                        return true;
                    return target is TerritoryNPCBehaviour npc
                           && !string.IsNullOrEmpty(npc.NPCData.NpcId);
                case "베팅하기":
                    // [125차 후속9] 베팅하기 활성화 — 구 IsActionSupported에 case 부재로 SetEnabled(false)
                    //   상태였던 것이 "베팅하기 안 됨"의 근본 원인(사용자 Play 실측). 클릭 시
                    //   ArenaWindowUTK.Open이 DuelRunning 등 실패 사유를 토스트로 피드백(피드백 필수 계약).
                    return target is ArenaBetManager;
                case "뇌물주기":
                    return target is GuardPlaceholder bribeGuard && !bribeGuard.IsAlly;   // 적병사 전용.
                case "포섭하기":
                    return target is GuardPlaceholder recruitGuard;   // 병사 공통.
                case "물약주기":
                case "음식주기":
                    return target is GuardPlaceholder itemGuard && itemGuard.IsAlly;   // 아군 전용 — 구창 아이템 선택 경유.
                case "들꽃 선물":
                    // [126차] 영주 딸 선물 — 호감도 100 미달에서만 활성(100에서 소비 없이 거부 계약).
                    return target is LordDaughterNPC giftDaughter && giftDaughter.Affection < 100;
                case "프로포즈":
                    // [126차] 영주 딸 프로포즈 — 호감도 80 이상에서만 활성.
                    return target is LordDaughterNPC proposeDaughter && proposeDaughter.Affection >= 80;
                case "공격하기":
                    return kind == HoverTargetClassifier.TargetKind.Lord
                           && target is InvadedLordEncounter lordEncounter
                           && lordEncounter.State == InvadedLordEncounter.EncounterState.NotStarted;
                case "상점":
                    return kind == HoverTargetClassifier.TargetKind.ShopNPC;
                case "세금 제안":
                    // [115차] 81:134 상점 패널 — 밀매제안 대체. 상전 NPC(ShopNPC) 전용. [144차] 라벨 공백 정합.
                    return kind == HoverTargetClassifier.TargetKind.ShopNPC;
                case "처형하기":
                case "풀어주기":
                    // [63차] 93:185 제압 영주 — Encounter Defeated 후에만 유효(Resolve 내부 1회성 가드 중복).
                    return kind == HoverTargetClassifier.TargetKind.Lord
                           && target is InvadedLordEncounter defeatedLord
                           && defeatedLord.IsDead;
                case "잠자기":
                    // [144차 D2-SLEEP] 163:45 — 침대 대상 항상 활성(Bed.OnInteract 경로).
                    return target is Bed;
                case "영주실 들어가기":
                case "자물쇠 따기":
                    // [144차 D2-LOCK] 163:64/83 — 잠긴 문만 활성(개방된 문은 비활성 피드백 계약).
                    return target is LockedDoor lockDoorTarget && lockDoorTarget.IsLocked;
                default:
                    return false;
            }
        }

        public override void Show()
        {
            bool wasOpen = IsOpen;
            base.Show();
            if (wasOpen)
                return;

            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);

            // [144차 P0-INT #7] 7판본 공통 코너 데칼 4각 — 9.6×9.6, Line #58A6FF w1.8, 코너 밖(공용 어휘).
            //   Fishing은 전용 크롬 경로(ApplyFishingChrome) 소관 — 중복 부착 방지.
            if (_targetKind != HoverTargetClassifier.TargetKind.Fishing)
            {
                this.Q("DecalTL")?.RemoveFromHierarchy();
                this.Q("DecalTR")?.RemoveFromHierarchy();
                this.Q("DecalBL")?.RemoveFromHierarchy();
                this.Q("DecalBR")?.RemoveFromHierarchy();
                UTKTheme.AddCornerDecals(this, 9.6f, 1.8f);
            }
        }

        public override void Hide()
        {
            EnsureResumeOnAnyClose();   // [94차 F4 → 96차 F2] 닫힘 전 경로 수렴(Close/자동 닫힘/Detach 공용 — 멱등/대화 가드).
            StopPlacementTracking();   // [72차] 닫힘 시 이동 추적 폴링 해제.
            _autoCloseTask?.Pause();   // [142차 D] 닫힘 시 거리 감시 정지(멱등).
            base.Hide();
        }

        /// <summary>[142차 D] 거리 이탈 자동 닫힘 틱 — 대상(Component)과 플레이어 수평 거리 > 5m → Hide().
        /// 대상이 Component가 아니거나(순수 데이터) 플레이어 부재면 무기입(기존 수동 닫힘만).</summary>
        private void AutoCloseDistanceTick()
        {
            if (style.display == DisplayStyle.None) return;   // 비가시(오크루전 숨김 포함) — 무기입
            var comp = _target as Component;
            if (comp == null) return;
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo == null) return;
            Vector3 d = comp.transform.position - playerGo.transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > AutoCloseDistanceM * AutoCloseDistanceM)
            {
                Debug.Log($"[142차 D] 상호작용 패널 자동 닫힘 — 대상 이탈 {d.magnitude:0.0}m > {AutoCloseDistanceM:0}m");
                Hide();
            }
        }

        /// <summary>[96차 F2] 패널 소유 확인 — 정지 대상이 현재 패널 소유인지. Systems 브리지 안전망이
        /// 리플렉션 경유 소유 이전 확인용으로 소비(PlayerMovement 64차 P3 리플렉션 선례 — asmdef 계약).</summary>
        public static bool IsHoldingPause(object target)
        {
            return _instance != null && _instance._wanderPausedTarget == target;
        }

        // ===== [Phase D] 시스템 브리지 구독 + 액션 라우팅 =====

        /// <summary>정적 구독 등록 — 클릭 트리거 → 패널 표시, 패널 액션 → 실제 창 개폐.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            _instance = new InteractionPanelUTK();
            SoldierInteractBridge.OnTargetInteractionRequested += OnSystemTargetRequested;
            ActionRequested += OnActionRequested;
        }

        /// <summary>Systems(ContextCommandRouter)가 분류한 대상으로 패널 열기.</summary>
        private static void OnSystemTargetRequested(HoverTargetClassifier.TargetKind kind, object target)
        {
            Open(kind, target);
        }

        /// <summary>
        /// [Phase D] 패널 버튼 액션 라우팅. 이번 단계에서 동작하는 것은:
        ///   대화하기 → NPCInstance(NPCDialoguePanelUTK 선택지 패널), 상태보기(병사) → GuardInfoUTK,
        ///   상점(ShopNPC) → ShopWindowUTK. 세금제안(ShopNPC) → RecommendedTaxWindowUTK.
        ///   뇌물/포섭/물약/음식/선물/퀘스트/동맹은 미구현(로그만).
        ///   [125차 후속9] 대화하기 전 대상 통일 — 아레나 베팅 관리인/영주 딸도 병사·주민과 동일
        ///   Test_15 계열(대화카메라 정면 확대 + NPCDialoguePanelUTK.OpenForStatic 대화창)로 개방.
        /// </summary>
        private static void OnActionRequested(HoverTargetClassifier.TargetKind kind, string label, object target)
        {
            switch (label)
            {
                case "낚시하기":
                    // [144차 D2-FISH] 피그마 163:4 — 낚시 패널 액션 → FishingSystem 시작(실동작 계약 — 무동작 표기 금지).
                    //   근접 판정은 FishingSystem 측에서 재검증(물가 이탈 후 클릭 방지). 패널 닫힘은 다른 액션 선례와 동일.
                    FishingSystem.RequestStartFromPanel();
                    Instance?.Close();
                    Debug.Log("[InteractionPanelUTK] 낚시하기 — FishingSystem 시작 요청(163:4)");
                    break;

                case "대화하기":
                    // [121차 → 125차 후속9] 아레나 베팅 관리인 — Test_15 동일 계열 통일(대화카메라 정면+대화창).
                    //   구: 인사 토스트 후 패널 닫힘(카메라/대화창 부재). 신: 병사·주민 선례와 동일 3단계 —
                    //   ① DialogueCameraDirector.Begin(target, 표시명) 전신 정면 확대(제네릭 오버로드, fullBody:true)
                    //   ② Instance?.Close() 패널 겹침 방지(기존 계약)
                    //   ③ NPCDialoguePanelUTK.OpenForStatic 대화창(1행=인사, 2행=보조정보, 선택지 3개, 대상 앞 배치).
                    if (target is ArenaBetManager arenaMgrTalk)
                    {
                        DialogueCameraDirector.Begin(arenaMgrTalk, "아레나 베팅 관리인");
                        Instance?.Close();
                        NPCDialoguePanelUTK.OpenForStatic(arenaMgrTalk, "아레나 베팅 관리인",
                            arenaMgrTalk.Greeting, "아레나 베팅 — 배당 안내");
                        Debug.Log("[InteractionPanelUTK] 대화하기 — 아레나 베팅 관리인 (카메라 확대+대화 패널)");
                        break;
                    }
                    // [126차 → 125차 후속9] 영주 딸 — Test_15 동일 계열 통일(아레나 관리인·병사·주민과 동일 3단계).
                    //   1행 = GetGreetingBody(이름 접두사 제외 인사 본문), 2행 = 호감도 실측 보조행(병사 호감 행 계열).
                    if (target is LordDaughterNPC daughterTalk)
                    {
                        DialogueCameraDirector.Begin(daughterTalk, daughterTalk.DisplayName);
                        Instance?.Close();
                        NPCDialoguePanelUTK.OpenForStatic(daughterTalk, daughterTalk.DisplayName,
                            daughterTalk.GetGreetingBody(), $"영주 딸 — 호감도 {daughterTalk.Affection}/100");
                        Debug.Log($"[InteractionPanelUTK] 대화하기 — 영주 딸 {daughterTalk.DisplayName} (카메라 확대+대화 패널)");
                        break;
                    }
                    // [72차] 병사 대화 — 구창 동일 래퍼(GuardPlaceholder.BeginTalk) 경유.
                    // [73차 Phase B] 대화 카메라 확대 선행(DialogueCameraDirector.Begin) + 패널 닫힘(공격하기/처형과 동일 —
                    //   패널 겹침 방지). 말풍선은 GuardTalkBubble가 StatusMessageChanged 구독으로 표시(4초),
                    //   카메라는 5초 타이머/이동·ESC 입력/대상 사망으로 복원.
                    if (target is GuardPlaceholder talkGuard)
                    {
                        DialogueCameraDirector.Begin(talkGuard);
                        Instance?.Close();
                        talkGuard.BeginTalk();
                        // [74차] 대화 패널(피그마 163:23/187:14) 연결 — 카메라 확대+말풍선과 병행(대사/선택지 UI).
                        NPCDialoguePanelUTK.OpenForGuard(talkGuard);
                        Debug.Log($"[InteractionPanelUTK] 대화하기 — 병사 {talkGuard.GuardName} (카메라 확대+대화 패널)");
                        break;
                    }
                    if (target is InvadedLordEncounter lordEncounter)
                    {
                        InvadedLordDialogueUTK.Open(lordEncounter);
                        Instance?.Close();   // [대화 즉시 개방] 공격하기/처형과 동일 — 패널 겹침 방지.
                        break;
                    }
                    OnTalkRequested(kind, target);   // NPC 대화 → NPCDialoguePanelUTK.Open (경로 내 Close 포함).
                    break;
                case "베팅하기":
                    // [121차] 아레나 베팅 — 관리인 상호판넬(261:3) → ArenaWindowUTK(255:647) 개방.
                    if (target is ArenaBetManager arenaMgrBet)
                    {
                        ArenaWindowUTK.Open(arenaMgrBet.NationStyle);
                        Instance?.Close();   // 패널 겹침 방지 — 공격하기/대화 전환과 동일 규약.
                        Debug.Log("[InteractionPanelUTK] 베팅하기 — 아레나 베팅 창 개방");
                    }
                    break;
                case "공격하기":
                    if (target is InvadedLordEncounter targetLord && targetLord.BeginCombat())
                    {
                        Instance?.Close();
                        Debug.Log($"[InteractionPanelUTK] 공격하기 — {targetLord.LordName}");
                    }
                    break;
                case "뇌물주기":
                    // [72차] 병사 뇌물 — 구창 동일 래퍼(GuardPlaceholder.AttemptBribe, !IsAlly).
                    // [73차 Phase B] 조용한 실패 제거 — 결과 bool을 토스트로(상세 대사는 말풍선(StatusMessageChanged) 경유).
                    if (target is GuardPlaceholder bribeGuard && !bribeGuard.IsAlly)
                    {
                        bool bribed = bribeGuard.AttemptBribe();
                        UTKToastService.Show(bribed
                            ? "뇌물 지급 — 호감도 상승"
                            : "골드 부족 — 뇌물 실패(30+Lv×25 골드 필요)");
                        Debug.Log($"[InteractionPanelUTK] 뇌물주기 — 병사 {bribeGuard.GuardName} (성공={bribed})");
                    }
                    break;
                case "포섭하기":
                    // [72차] 병사 포섭 — 구창 동일 래퍼(GuardPlaceholder.BeginRecruit).
                    // [73차 Phase B] 조용한 실패 제거 — 결과 토스트(BeginRecruit 시그니처 불변 → IsAlly 전이 + 입단비 응답으로 판정).
                    if (target is GuardPlaceholder recruitGuard)
                    {
                        bool wasRecruited = recruitGuard.IsAlly;
                        recruitGuard.BeginRecruit();
                        if (!wasRecruited && recruitGuard.IsAlly)
                            UTKToastService.Show("포섭 완료");
                        else if (!recruitGuard.IsAlly && (recruitGuard.StatusMessage ?? "").Contains("입단비"))
                            UTKToastService.Show("입단비 부족");
                        Debug.Log($"[InteractionPanelUTK] 포섭하기 — 병사 {recruitGuard.GuardName}");
                    }
                    break;
                case "물약주기":
                case "음식주기":
                    // [72차] 아군 병사 아이템 지급 — 구창(SoldierInteractUTK) 아이템 선택 UI 경유 개방(패널은 창을 여는 다리).
                    // [73차 Phase B] 지급 완료 후 말풍선은 StatusMessageChanged 구독(GuardTalkBubble)으로 자동 표시 — 경로 유지.
                    if (target is GuardPlaceholder itemGuard && itemGuard.IsAlly)
                    {
                        var itemMode = label == "물약주기"
                            ? SoldierInteractUTK.ItemMode.Drug
                            : SoldierInteractUTK.ItemMode.Food;
                        SoldierInteractUTK.OpenWithItemMode(itemGuard, itemMode);
                        Instance?.Close();   // 창 개방 — 패널 겹침 방지.
                    }
                    break;
                case "상태보기":
                    if ((kind == HoverTargetClassifier.TargetKind.EnemyGuard
                         || kind == HoverTargetClassifier.TargetKind.Ally) && target is GuardPlaceholder guard)
                    {
                        // [72차 Phase A] 피그마 2종 패널(GuardStatusWindowUTK) 개방 — 구창 GuardInfoUTK 대체.
                        //   아군=CharacterStatusPanel+EquipmentPanel 결합 / 적병사=CharacterStatusPanel 단독.
                        GuardStatusWindowUTK.Open(guard);
                        Instance?.Close();   // 패널 겹침 방지 — 상태보기 창 개방과 동시 패널 닫힘.
                    }
                    else
                        Debug.Log($"[InteractionPanelUTK] 상태보기 미지원 대상: {kind}");
                    break;
                case "상점":
                    if (kind == HoverTargetClassifier.TargetKind.ShopNPC)
                        ShopWindowUTK.Open();
                    else
                        Debug.Log($"[InteractionPanelUTK] 상점 액션 대상 오류: {kind}");
                    break;
                case "세금 제안":
                    // [115차] 81:134 상점 패널 — 권장세금 UI(세금제안) 개방. 상전 NPC 전용. [144차] 라벨 공백 정합.
                    // [117차 R2] 피그마 244:281 = "단일 영지" 세금 관리 — 상점 NPC의 TerritoryId를
                    //   영지 컨텍스트로 전달(이름/난이도/세율/호감도가 그 영지 실측값으로 채워진다).
                    if (kind == HoverTargetClassifier.TargetKind.ShopNPC && target is TerritoryNPCBehaviour taxShopNpc)
                        RecommendedTaxWindowUTK.Open(taxShopNpc.NPCData.TerritoryId);
                    else if (kind == HoverTargetClassifier.TargetKind.ShopNPC)
                        RecommendedTaxWindowUTK.Open();   // 컨텍스트 부재 폴백 — PlayerOwned 첫 영지
                    else
                        Debug.Log($"[InteractionPanelUTK] 세금제안 액션 대상 오류: {kind}");
                    break;
                case "처형하기":
                case "풀어주기":
                    // [63차 — 93:185] 제압 영주 결정 — InvadedLordDialogueUTK 경로(처형/추궁/풀어주기 3선택지)와
                    //   동일 계약(Resolve 1회성). 패널에서 직접 선택 시에도 기존 Resolve로 처리.
                    if (target is InvadedLordEncounter defeatedLord && defeatedLord.IsDead)
                    {
                        bool executed = defeatedLord.Resolve(label == "처형하기");
                        Instance?.Close();
                        Debug.Log($"[InteractionPanelUTK] {label} — {defeatedLord.LordName} (resolve={executed})");
                    }
                    break;
                case "들꽃 선물":
                    // [126차] 영주 딸 선물 — 들꽃 1개 소비 + 호감도 +10(결과별 토스트 계약).
                    if (target is LordDaughterNPC daughterGift)
                    {
                        var giftResult = daughterGift.GiftFlower(PlayerInventory.Instance);
                        switch (giftResult)
                        {
                            case LordDaughterNPC.GiftResult.Success:
                                UTKToastService.Show($"{daughterGift.DisplayName}: \"고마워요. 향기가 참 좋네요.\" (호감도 {daughterGift.Affection}/100)", 2.5f);
                                Instance?.RebuildActions();   // 버튼 갱신(프로포즈 활성 등) 계약.
                                break;
                            case LordDaughterNPC.GiftResult.NoInventory:
                                UTKToastService.Show("인벤토리를 찾을 수 없습니다.", 2.5f);
                                break;
                            case LordDaughterNPC.GiftResult.NoFlower:
                                UTKToastService.Show("선물할 들꽃이 없습니다.", 2.5f);
                                break;
                            case LordDaughterNPC.GiftResult.AffectionMaxed:
                                UTKToastService.Show($"{daughterGift.DisplayName}: \"당신의 마음은 이미 충분해요.\" (호감도 100/100)", 2.5f);
                                break;
                            case LordDaughterNPC.GiftResult.NoTerritory:
                            default:
                                UTKToastService.Show("영지 상태를 찾을 수 없습니다.", 2.5f);
                                break;
                        }
                        Debug.Log($"[InteractionPanelUTK] 들꽃 선물 — 영주 딸 {daughterGift.DisplayName} (결과={giftResult})");
                    }
                    break;
                case "프로포즈":
                    // [126차] 영주 딸 프로포즈 — 호감도 80 이상 게이트(결과별 토스트 계약).
                    if (target is LordDaughterNPC daughterPropose)
                    {
                        var proposeResult = daughterPropose.TryPropose();
                        switch (proposeResult)
                        {
                            case LordDaughterNPC.ProposeResult.Accepted:
                                UTKToastService.Show($"{daughterPropose.DisplayName}: \"네, 받아들일게요. 앞으로 함께해요.\" (♥ 결혼 · 충성도 +30)", 2.5f);
                                Instance?.Close();   // 수락 시 패널 닫힘(계약).
                                break;
                            case LordDaughterNPC.ProposeResult.AlreadyMarried:
                                UTKToastService.Show($"{daughterPropose.DisplayName}: \"우리는 이미 부부예요.\"", 2.5f);
                                break;
                            case LordDaughterNPC.ProposeResult.NotReady:
                            default:
                                UTKToastService.Show($"{daughterPropose.DisplayName}: \"아직 마음을 다 전하지 못했네요.\" (호감도 80 이상 필요)", 2.5f);
                                break;
                        }
                        Debug.Log($"[InteractionPanelUTK] 프로포즈 — 영주 딸 {daughterPropose.DisplayName} (결과={proposeResult})");
                    }
                    break;
                case "잠자기":
                    // [144차 D2-SLEEP] 163:45 — Bed.OnInteract() = E키와 동일 경로(수면/스폰 저장 계약 유지).
                    if (target is Bed sleepBed)
                    {
                        sleepBed.OnInteract();
                        Instance?.Close();   // 창/수면 흐름 개방 — 패널 겹침 방지.
                        Debug.Log("[InteractionPanelUTK] 잠자기 — Bed.OnInteract (163:45)");
                    }
                    break;
                case "영주실 들어가기":
                case "자물쇠 따기":
                    // [144차 D2-LOCK] 163:64/83 — LockedDoor.OnLockpickRequested 발화 = E키와 동일 경로
                    //   (LockpickingUI 구독 — 자물쇠 미니게임 개방). 영주실 문도 잠김 상태에선 동일 경로.
                    if (target is LockedDoor lockDoor && lockDoor.IsLocked)
                    {
                        lockDoor.RequestLockpick();   // [144차] E키와 동일 처리(마스터 키 우선 → OnLockpickRequested)
                        Instance?.Close();
                        Debug.Log($"[InteractionPanelUTK] 자물쇠 따기 — {lockDoor.LocationId} (163:64/83)");
                    }
                    break;
                default:
                    Debug.Log($"[InteractionPanelUTK] 미구현 액션: {kind}/{label}");
                    break;
            }
        }

        /// <summary>대화하기 — NPCInstance면 선택지 패널, 영주/병사는 무해 처리. [89차] 마을 주민(NPCAmbientDialogue) 분기 추가.</summary>
        private static void OnTalkRequested(HoverTargetClassifier.TargetKind kind, object target)
        {
            // [89차] 마을 주민(NPCAmbientDialogue) 분기 — TerritoryNPCBehaviour 분기 전 선판정.
            //   ① DialogueCameraDirector.Begin(villager, villager.DisplayName) — [92차 결함3] 주민 전신 정면
            //     카메라 확대(FOV 40/bounds center 조준 — NPCAmbientDialogue 타입 오버로드로 fullBody:true 주입)
            //   ② Instance?.Close() — 패널 겹침 방지(기존 계약)
            //   ③ npc.BeginDialoguePose() — 배회 정지+Talk 트리거(Player_AC TalkA), DialogueEnded 구독(구독 전 해제·1회성)
            //     → 카메라 복원 시 ResumeAfterDialogue(배회 재개)
            //   ④ NPCDialoguePanelUTK.OpenForVillager(npc) — 대상 앞 배치 NPC 대화 패널.
            if (target is NPCAmbientDialogue villager)
            {
                DialogueCameraDirector.Begin(villager, villager.DisplayName);
                Instance?.Close();
                villager.BeginDialoguePose();
                NPCDialoguePanelUTK.OpenForVillager(villager);
                Debug.Log($"[InteractionPanelUTK] 대화하기 — 마을 주민 {villager.DisplayName} (카메라 확대+대화 패널)");
                return;
            }
            var npc = target as TerritoryNPCBehaviour;
            if (npc != null
                && !string.IsNullOrEmpty(npc.NPCData.NpcId))
            {
                Instance?.Close();   // [대화 즉시 개방] NPC 대화도 공격하기/처형과 동일 — 패널 겹침 방지.
                // [74차] NPC 대화 패널도 대상 바로 앞 배치(anchor 전달).
                NPCDialoguePanelUTK.Open(npc.NPCData, npc.transform);
                return;
            }
            Debug.Log($"[InteractionPanelUTK] 대화 미지원 대상: {kind}");
        }
    }
}
