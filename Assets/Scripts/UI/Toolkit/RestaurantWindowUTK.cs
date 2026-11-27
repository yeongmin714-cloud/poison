using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;            // PlayerInventory/PlayerStats — 보유 음식/소지금 실측
using ProjectName.Core.Data;       // VillageDrugState/VillageDrugSnapshot/RestaurantStockItem/NationType/TerritoryDatabase
using ProjectName.Core.Utils;      // VillageDrugMath — 권장가 순수 수학(위조 금지)
using ProjectName.Systems;         // RestaurantSystem/RestaurantPlacementMarker/RestaurantPropInteractable/TimeManager
using ProjectName.UI;              // ItemIconDatabase — 인벤 아이콘 실측 렌더 선례

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// [88차 Part B] 음식점 관리 창 (UI Toolkit) — Figma 237:792 "음식점 판매 관리" 1600×1040 전면 수용.
    ///
    /// [진입점 — 시그니처 불변]
    ///  · 음식점 실물 근접 E키 → Systems(RestaurantPropInteractable) → OpenRequestedUTK(nation, villageIndex)
    ///    → 본 창 Open(nation, villageIndex). Bootstrap 자가부트(AfterSceneLoad) + EnsureAttached 멱등.
    ///  · [88차 B3] RestaurantPlacementMarker.PlacementAttempted 구독 → UTKToastService.Show(message)
    ///    (구독 전 해제 패턴 — 도메인 리로드/재진입 중복 방지).
    ///
    /// [레이아웃 — ApplyCanvasFit 84차 계약]
    ///  · 창박스 = figma(1600×1040)×k, 내부 raw 유지 + transform.scale=k, k=min(rootW/1920, rootH/1080).
    ///  · 헤더 @(64,36) 1472×18 / 좌 인벤토리 480×882 @(64,78) / 우 판매 관리 968×882 @(568,78) / 도움말 @(64,984).
    ///  · 패널 bg #13181F r14, stroke #30363D/0.5. 반경 14(패널)/10(카드)/6(버튼·박스).
    ///
    /// [데이터 규약 — 위조 금지, 전부 실측 API 단일 소스]
    ///  · VillageDrugState(재고/pending 적립금) · VillageDrugMath(권장가) · RestaurantSystem(입고/수령/상한 6) ·
    ///    PlayerInventory(Food+Drug 슬롯/40슬롯) · PlayerStats(소지금) · TimeManager(영업 배지/일차/시각).
    ///  · 기존 섹션(병사 배치/중독 게이지/수요 문구)은 Figma 부재로 제거 — 시스템 로직(A3/A4 자동병사 포함)은 유지.
    ///
    /// [단축키] Enter = 선택 음식 추가 배치 실행, Esc = 창 닫기(KeyDownEvent — 창 표시 중만, UIRoot 경유
    ///  SleepPanelUTK 910~919행 선례 + UTKWindowManager.TopWindow 게이트).
    /// </summary>
    public class RestaurantWindowUTK : UTKWindowBase
    {
        // ═══════════════ [88차 B2] Figma 237:792 진실치 (2026-10-16T01:01:11Z 개정본 실측) ═══════════════

        private const float WinW = 1600f;        // 창 1600×1040
        private const float WinH = 1040f;
        private const float RadiusPanel = 14f;   // 패널 r14
        private const float RadiusCard = 10f;    // 카드 r10
        private const float RadiusBox = 6f;      // 버튼·박스 r6

        // 창 자식 박스 — 헤더 @(64,36) / 좌 @(64,78) / 우 @(568,78) / 도움말 @(64,984)
        private const float HeaderX = 64f, HeaderY = 36f, HeaderW = 1472f, HeaderH = 18f;
        private const float LeftX = 64f, LeftY = 78f, LeftW = 480f, LeftH = 882f;
        private const float RightX = 568f, RightY = 78f, RightW = 968f, RightH = 882f;
        private const float HelpX = 64f, HelpY = 984f, HelpW = 1472f, HelpH = 14f;
        private const float PanelPad = 24f;      // 패널 내측 여백(좌우 24 — 탭/슬롯 x 기준)

        // ── 색 토큰 (Figma 실측 hex) ──
        // [134차 C3] 패널 fill — 플루언트 글래스 #161B22@0.80(구 #13181F 불투명 → 알파 근사).
        //   백드롭블러(BACKGROUND_BLUR) 미지원 환경이므로 0.80 알파 단색 근사(월드맵 125후속38 선례).
        private static readonly Color PanelGlassBg = new Color32(0x16, 0x1B, 0x22, 0xCC);   // #161B22@0.80 패널 글래스
        private static readonly Color StrokeCol = new Color32(0x30, 0x36, 0x3D, 0x80); // #30363D/0.5 보더
        private static readonly Color SurfaceCol = new Color32(0x19, 0x1F, 0x27, 0xFF); // #191F27 표면
        private static readonly Color InputCol = new Color32(0x0F, 0x14, 0x1B, 0xFF);  // #0F141B 입력/어두운 표면
        private static readonly Color SlotCol = new Color32(0x20, 0x26, 0x2E, 0xFF);   // #20262E 슬롯
        private static readonly Color SlotSelectedCol = new Color32(0x25, 0x33, 0x46, 0xFF); // #253346 선택 슬롯
        private static readonly Color TextMain = new Color32(0xF0, 0xF6, 0xFC, 0xFF);  // #F0F6FC 본문
        private static readonly Color TextSub = new Color32(0x8B, 0x94, 0x9E, 0xFF);   // #8B949E 보조
        private static readonly Color TextDim = new Color32(0x58, 0x64, 0x73, 0xFF);   // #586473 placeholder/구분자
        private static readonly Color Accent = new Color32(0x58, 0xA6, 0xFF, 0xFF);    // #58A6FF 액센트
        private const float AccentAlpha = 0.078f;                                      // 액센트 배경 알파
        private static readonly Color AccentBg = new Color(0x58 / 255f, 0xA6 / 255f, 0xFF / 255f, AccentAlpha);
        private static readonly Color BtnTextDark = new Color32(0x0B, 0x0E, 0x14, 0xFF); // #0B0E14 강조 버튼 글자
        private static readonly Color GradeBarIdle = new Color(0x65 / 255f, 0x76 / 255f, 0x8A / 255f, 0.4f); // #65768A/0.4
        private static readonly Color RowStrokeSelected = new Color(0x58 / 255f, 0xA6 / 255f, 0xFF / 255f, 0.25f);

        // ── 우 패널 (음식점 배치) 진실치 ──
        // [144차 P7-RST #82] 열 x 7종 — 패널 로컬 38/418/524/634/704/792/912 → 리스트 로컬 14/394/500/610/680/768/888(−24px 전체 시프트).
        // 관리열(888)은 목록 우단 944 내부로.
        private const float ColXName = 14f;        // 열 x — 음식
        private const float ColXRecommended = 394f; // 권장 가격
        private const float ColXSell = 500f;        // 판매 가격
        private const float ColXPlaced = 610f;      // 배치
        private const float ColXProgress = 680f;    // 판매 / 배치
        private const float ColXGold = 768f;        // 판매금
        private const float ColXManage = 888f;      // 관리
        private const float ListW = 920f, ListH = 330f, ColHeaderH = 34f;
        private const float StockRowH = 74f;        // 행 920×74
        private const float RowPreviewSize = 44f;   // 미리보기 44×44
        private const float PriceBoxW = 98f, PriceBoxH = 32f; // 가격박스 98×32
        private const float ProgressW = 66f, ProgressH = 3f;  // 진행바 66×3
        private const float MetricW = 221f, MetricH = 80f, MetricGap = 12f; // 지표 221×80 gap12
        private const float NoticeH = 42f;          // 추가 배치 안내 920×42
        private const float CollectBoxH = 92f;      // 판매금 수령 920×92
        private const float CollectButtonW = 250f, CollectButtonH = 46f; // 버튼 250×46

        // ── 좌 패널 (인벤토리) 진실치 ──
        private const float TabW = 81.6f, TabH = 36f, TabGap = 6f;    // 탭 81.6×36 gap6
        private const float SearchH = 34f;          // 검색 432×34
        private const float SlotW = 80f, SlotH = 86f, SlotGap = 10f;  // [134차 F] 슬롯 80×86 행간격 10(피치 96, 진실치 문서 ①)
        private const float SlotColGap = 8f;                          // [144차 P7-RST #36] 슬롯 열간격 8 — 피치 88, 5열=5×80+4×8=432(237:830 gap8)
        private const int SlotCols = 5, SlotRows = 3;                 // 5열×3행
        private const float SlotGradeBarW = 68f, SlotGradeBarH = 2f;  // 등급바 68×2
        private const float NoteY = 502f;           // '판매대에 배치되지 않은...' @(24,502)
        private const float SelectBoxW = 432f, SelectBoxH = 213f;     // 선택 음식 배치 432×213
        private const float SelectPreviewSize = 48f;                  // 미리보기 48×48
        private const float StepperW = 116f, StepperH = 32f;          // 수량조절 116×32
        private const float StepperSideW = 32f, StepperMidW = 52f;    // [144차 P7-RST #53] 스테퍼 3분할 −32/값52/+32 — 합 116(237:910/911/912)
        private const float PlaceButtonW = 400f, PlaceButtonH = 46f;  // 실행 버튼 400×46
        private const float UsageBarW = 432f, UsageBarH = 4f;         // 사용량 바 432×4

        private const long PollMs = 1000L;   // 개방 중 경량 폴링 — 기존 계약 유지(1000ms)

        // ═══════════════ 상태 ═══════════════

        /// <summary>좌 패널 분류 탭 — Figma 5탭(전체/요리/디저트/음료/재료).</summary>
        private enum InvTab { All, Cooked, Dessert, Drink, Ingredient }

        /// <summary>보유 음식 1건 — 좌 슬롯 그리드/선택 박스 공용 뷰모델(실측 ItemData 래핑).</summary>
        private struct InventoryFoodEntry
        {
            public PlayerInventory.ItemData item;
            public int count;
            public bool isDruggedFood;   // drugged_ 접두(마약 요리 — CookingDrugHelper/D3-A 규약)
            public string DisplayName => !string.IsNullOrEmpty(item.displayName) ? item.displayName : item.id;
        }

        private static RestaurantWindowUTK _instance;
        public static RestaurantWindowUTK Instance => _instance;

        private NationType _nation;
        private int _villageIndex = -1;      // -1 = 미지정 — RefreshAll 무동작 가드

        private InvTab _activeTab = InvTab.All;
        private string _searchText = string.Empty;

        // 선택 음식(좌 슬롯 클릭) — 배치 수량/예상 판매금 계약의 입력
        private string _selectedItemId;
        private string _selectedDisplayName = string.Empty;
        private string _selectedCategoryLabel = string.Empty;
        private bool _selectedIsDrugged;
        private int _selectedHeld;
        private int _placeQty = 1;

        private string _selectedStockKey;    // 우 행 클릭 선택 상태(병합 키 문자열) — Figma 선택 행 색 계약

        private float _lastFitRootW = -1f, _lastFitRootH = -1f;   // 루트 리사이즈 시에만 재적용(드래그 보존)

        // ── UI 핸들(갱신용) ──
        private Label _headerVillageLabel, _headerDayLabel, _headerClockLabel;
        private Button[] _tabButtons;
        private Label[] _tabLabels;
        private TextField _searchField;
        private Label _searchHintLabel;   // 검색 placeholder 근사 라벨
        private Label _slotSummaryLabel;
        private ScrollView _slotGrid;
        private Label _invGoldLabel, _invSlotLabel;
        private VisualElement _invUsageFill;
        private Label _selNameLabel, _selInfoLabel, _selPriceLabel;
        private Label _qtyValueLabel, _maxQtyLabel, _placeButtonLabel, _predictLabel;
        private Button _minusButton, _plusButton, _placeButton;
        private Label _opsTitleLabel, _opsBadgeLabel;
        private VisualElement _opsBadgeDot;
        private Label _stockCountLabel;
        private ScrollView _stockList;
        private Label _freeSlotNoticeLabel;
        private Label[] _metricValueLabels;
        private Label _collectAmountLabel, _collectWalletLabel;
        private Button _collectButton;
        private IVisualElementScheduledItem _pollTask;
        // [151차 P0] 가격 필드 키보드 브리지 상태 — 편집 중인 필드 + 백스페이스/Enter 틱(스코프드 최후수단).
        private TextField _bridgeField;
        private IVisualElementScheduledItem _bridgeTick;

        // ═══════════════ 생성 — 창 골격(B1) ═══════════════

        private RestaurantWindowUTK() : base("음식점", new Vector2(WinW, WinH), UTKWindowChrome.Frameless)
        {
            // 창 루트 — Figma 캔버스(투명 바탕)에 헤더/패널/도움말이 올라가는 구성
            // [134차 C3] 루트 배경 = 투명(제거 상태 확인) — Figma 프레임 배경(#0B0E14) 이식분 없음
            //   (진실치 문서 ① '투명 바탕' 상등 — FigmaRootBg 계열 적용 지점 부재, 후속 차수 투명 역전 방지 주석).
            style.backgroundColor = new StyleColor(Color.clear);
            style.backgroundImage = new StyleBackground(StyleKeyword.None);
            style.overflow = Overflow.Hidden;

            _content.style.width = WinW;    // 내부 raw 유지 — ApplyCanvasFit이 scale=k 적용
            _content.style.height = WinH;
            _content.style.position = Position.Relative;
            _content.style.paddingLeft = 0f;
            _content.style.paddingRight = 0f;
            _content.style.paddingTop = 0f;
            _content.style.paddingBottom = 0f;
            _content.style.flexGrow = 0f;
            _content.style.flexShrink = 0f;

            BuildHeaderBar();
            BuildLeftPanel();
            BuildRightPanel();
            BuildHelpBar();

            // [88차 B3] 단축키 — UIRoot 경유 KeyDownEvent(IsOpen+TopWindow 게이트, SleepPanelUTK 선례).
            RegisterCallback<AttachToPanelEvent>(_ => UIToolkitBootstrap.UIRoot?.RegisterCallback<KeyDownEvent>(OnRootKeyDown));
            RegisterCallback<DetachFromPanelEvent>(_ => UIToolkitBootstrap.UIRoot?.UnregisterCallback<KeyDownEvent>(OnRootKeyDown));

            ApplyUIToolkitFont(this);
            DisableWindowShadow();   // [144차 P7-RST #9] Figma 프레임(237:792) drop shadow 부재 — 창 외곽 rim 음영 제거(ArenaResultWindowUTK/ConstructionWindowUTK 선례)
        }

        // ═══════════════ 자가부트 / 개방 API (시그니처 불변) ═══════════════

        /// <summary>[88차] 자가부트 — NPCDialoguePanelUTK AfterSceneLoad 패턴 준용. 구독 전 해제 패턴.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EnsureAttached();

            RestaurantPropInteractable.OpenRequestedUTK -= OnRestaurantInteractRequested;
            RestaurantPropInteractable.OpenRequestedUTK += OnRestaurantInteractRequested;

            // [88차 B3] 배치 마커 결과 → 토스트(Systems→UI 순환참조 회피 정적 이벤트 규약)
            RestaurantPlacementMarker.PlacementAttempted -= OnPlacementAttempted;
            RestaurantPlacementMarker.PlacementAttempted += OnPlacementAttempted;
        }

        private static void EnsureAttached()
        {
            var root = UIToolkitBootstrap.UIRoot;
            // [117차 R3] 고아 가드 — parent != null이어도 UIRoot 후손이 아니면 비렌더
            //   (영상 실측: 개방 로그 발생+렌더 0프레임). 114차 WorldMap/115차 RecommendedTax 선례.
            if (_instance != null)
            {
                bool orphaned = _instance.parent == null;
                if (!orphaned)
                {
                    var p = _instance.parent;
                    while (p != null && p != root) p = p.parent;
                    orphaned = p != root;
                }
                if (orphaned)
                {
                    Debug.LogWarning("[RestaurantWindowUTK] 고아 인스턴스 감지 — 제거 후 재생성 ([117차 R3])");
                    _instance.RemoveFromHierarchy();
                    _instance = null;
                }
            }
            if (root == null) return;   // 기존 계약 — 루트 부재 시 생성 생략
            if (_instance == null)
                _instance = new RestaurantWindowUTK();
            if (_instance.parent == null)
                root.Add(_instance);
        }

        private static void OnRestaurantInteractRequested(NationType nation, int villageIndex)
        {
            Open(nation, villageIndex);
        }

        /// <summary>[88차 B3] 배치 시도 결과 토스트 — 성공 "건설 시작! ..." / 실패 사유 문구 그대로 표시.</summary>
        private static void OnPlacementAttempted(bool success, string message)
        {
            if (!string.IsNullOrEmpty(message))
                UTKToastService.Show(message);
        }

        /// <summary>
        /// 음식점 창 개방 — (nation, villageIndex)는 실물 부착 시 캡처된 마을 키.
        /// 미건설 마을은 로그만 남기고 안전 반환한다(창 미생성 — 멱등/EditMode 무해).
        /// </summary>
        public static void Open(NationType nation, int villageIndex)
        {
            VillageDrugSnapshot snapshot = VillageDrugState.GetSnapshot(nation, villageIndex);
            if (!snapshot.restaurantBuilt)
            {
                Debug.Log($"[RestaurantWindowUTK] {nation}_v{villageIndex} 음식점 미건설 — 창 개방 생략");
                return;
            }

            var root = UIToolkitBootstrap.UIRoot;
            if (root == null)
            {
                Debug.LogWarning("[RestaurantWindowUTK] UIRoot 미준비 — 음식점 창 개방 불가");
                return;
            }

            EnsureAttached();
            if (_instance._villageIndex != villageIndex || _instance._nation != nation)
            {
                _instance._nation = nation;
                _instance._villageIndex = villageIndex;
                _instance.ClearSelection();   // 마을 전환 — 선택/수량 초기화
            }
            if (_instance.IsOpen)
                _instance.RefreshAll();       // 이미 개방 — 대상 마을 갱신만(Show 재호출 무효 가드)
            else
                _instance.Show();             // OnWindowOpen → RefreshAll + 폴링/캔버스핏 스케줄
            Debug.Log($"[RestaurantWindowUTK] 음식점 창 개방: {nation}_v{villageIndex}");
            // [117차 R3] 개방 상태 진단 — 개방 로그와 렌더 불일치 판별 데이터(부착/부모 체인/개방 플래그).
            Debug.Log($"[RestaurantWindowUTK] 개방 상태 — attached={(_instance.parent != null)} parent={(_instance.parent != null ? _instance.parent.name : "없음")} isOpen={_instance.IsOpen}");
            _instance.LogGeometry("개방즉시");
            _instance.schedule.Execute(() => _instance.LogGeometry("250ms")).StartingIn(250);   // [118차 R1] ApplyCanvasFit 적용 완료 시점 재실측
        }

        private void ClearSelection()
        {
            _selectedItemId = null;
            _selectedDisplayName = string.Empty;
            _selectedCategoryLabel = string.Empty;
            _selectedIsDrugged = false;
            _selectedHeld = 0;
            _placeQty = 1;
            _selectedStockKey = null;
        }

        /// <summary>[118차 R1] 창 기하 실측 — attached/isOpen=True인데 비렌더인 상태(2026-10-23 실측)의
        ///   판정 데이터: 창/콘텐츠/첫 패널의 화면 좌표+크기+display. 판정 분기: 화면 밖→배치 수리,
        ///   size 0/display None→표시 수리, 정상+미표시→자식 렌더 원인 추적.</summary>
        private void LogGeometry(string phase)
        {
            var content = Content;
            var left = content != null ? content.Q("LeftPanel") : null;
            string leftInfo = left != null ? $"{left.worldBound} children={left.childCount}" : "미발견";
            Debug.Log($"[118차 R1] 음식점창 기하({phase}) — winBound={worldBound} display={style.display.value} " +
                      $"winStyle=({style.left.value.value:F0},{style.top.value.value:F0} {style.width.value.value:F0}x{style.height.value.value:F0}) " +
                      $"contentBound={(content != null ? content.worldBound.ToString() : "null")} " +
                      $"leftPanel={leftInfo}");
        }

        // ═══════════════ UTKWindowBase 라이프사이클 ═══════════════

        protected override void OnWindowOpen()
        {
            RefreshAll();
            _pollTask = schedule.Execute(PollRefresh).Every(PollMs).StartingIn(PollMs);
            // [88차 B1] ApplyCanvasFit — 개방 즉시 + 레이아웃 지연 대응 재시도(16/120/400ms — 수면 패널 센터링 선례)
            schedule.Execute(ApplyCanvasFit).StartingIn(16);
            schedule.Execute(ApplyCanvasFit).StartingIn(120);
            schedule.Execute(ApplyCanvasFit).StartingIn(400);
        }

        protected override void OnWindowClosed()
        {
            DetachPriceKeyboardBridge();   // [151차 P0] 창 닫힘 — 편집 브리지 정리(포커스 유실 대비)
            if (_pollTask != null)
            {
                _pollTask.Pause();
                _pollTask = null;
            }
        }

        private void PollRefresh()
        {
            if (!IsOpen) return;
            ApplyCanvasFit();   // 루트 리사이즈 시에만 실제 재적용(내부 가드) — 드래그 위치 보존
            RefreshAll();
        }

        /// <summary>
        /// [84차] 등배수 창박스 적용 — "창박스=figma×k, 내부 raw·scale=k" 계약(SleepPanelUTK 동일 패턴).
        /// k=min(rootW/1920, rootH/1080). 창=중앙 배치(Figma 캔버스 1920×1080 내 1600×1040 중앙 상당),
        /// 내부 콘텐츠는 raw 유지+transform.scale=k(글자/간격 왜곡 없음).
        /// </summary>
        private void ApplyCanvasFit()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;
            float rootW = root.resolvedStyle.width;
            float rootH = root.resolvedStyle.height;
            if (rootW <= 1f || rootH <= 1f) return;   // 레이아웃 미확정 — 다음 폴링 시 재적용
            if (Mathf.Approximately(rootW, _lastFitRootW) && Mathf.Approximately(rootH, _lastFitRootH))
                return;   // 루트 불변 — 재적용 생략(사용자 드래그 위치 보존)
            float k = Mathf.Min(rootW / FigmaCanvasLayout.CanvasWidth, rootH / FigmaCanvasLayout.CanvasHeight);
            if (k <= 0f) return;

            _lastFitRootW = rootW;
            _lastFitRootH = rootH;
            style.width = WinW * k;
            style.height = WinH * k;
            style.left = Mathf.Max(0f, (rootW - WinW * k) * 0.5f);    // 중앙 배치
            style.top = Mathf.Max(0f, (rootH - WinH * k) * 0.5f);
            _content.style.transformOrigin = new TransformOrigin(0f, 0f, 0f);
            _content.style.scale = new StyleScale(new Scale(new Vector2(k, k)));

            // [118차 R1] 오프스크린 자가 클램프 — 창이 루트 밖으로 밀렸으면 화면 내로 보정(실패 모드 자가 소멸).
            var rootCheck = UIToolkitBootstrap.UIRoot;
            if (rootCheck != null && rootCheck.resolvedStyle.width > 1f && parent != null)
            {
                float rW = rootCheck.resolvedStyle.width, rH = rootCheck.resolvedStyle.height;
                if (style.left.value.value < 0f || style.top.value.value < 0f ||
                    style.left.value.value + style.width.value.value > rW ||
                    style.top.value.value + style.height.value.value > rH)
                {
                    style.left = Mathf.Clamp(style.left.value.value, 0f, Mathf.Max(0f, rW - style.width.value.value));
                    style.top = Mathf.Clamp(style.top.value.value, 0f, Mathf.Max(0f, rH - style.height.value.value));
                    Debug.Log($"[118차 R1] 오프스크린 감지 → 클램프 적용 left={style.left.value.value:F0} top={style.top.value.value:F0}");
                }
            }
        }

        // ═══════════════ 단축키 (KeyDownEvent — 창 표시 중만) ═══════════════

        private void OnRootKeyDown(KeyDownEvent evt)
        {
            if (!IsOpen) return;
            if (UTKWindowManager.TopWindow != this) return;   // 최상단 창이 아닐 때 무간섭(타 창 입력 보호)
            if (evt.keyCode == KeyCode.Escape)
            {
                if (IsAnyTextFieldFocused()) return;           // [151차 P0] 가격 편집 중 Esc 오닫기 방지(Enter 가드 준용)
                evt.StopPropagation();
                Hide();                                        // Esc = 창 닫기
            }
            else if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                if (IsAnyTextFieldFocused()) return;           // 가격/검색 입력 중 — 편집 확정은 필드 자체 핸들러
                evt.StopPropagation();
                OnPlaceButtonClicked();                        // Enter = 선택 음식 추가 배치 실행
            }
        }

        /// <summary>텍스트 필드(가격/검색) 편집 중 판정 — Enter 오발 방지 가드.</summary>
        private bool IsAnyTextFieldFocused()
        {
            if (panel == null || panel.focusController == null) return false;
            return panel.focusController.focusedElement is TextField;
        }

        // ═══════════════ 갱신 ═══════════════

        private void RefreshAll()
        {
            if (_villageIndex < 0) return; // 미지정 — 무동작 가드

            VillageDrugSnapshot snapshot = VillageDrugState.GetSnapshot(_nation, _villageIndex);

            RefreshHeaderTime();
            RefreshLeftPanel();
            RefreshRightPanel(snapshot);
        }

        /// <summary>헤더 우측 — '{N}일차 · 낮/밤' + 'HH:MM' (TimeManager 실측 — 73차 수면 패널 선례).</summary>
        private void RefreshHeaderTime()
        {
            TimeManager tm = TimeManager.Instance;
            _headerVillageLabel.text = ResolveVillageName();
            _headerDayLabel.text = tm != null ? $"{tm.CurrentDay}일차 · {(tm.IsDay ? "낮" : "밤")}" : "-";
            _headerClockLabel.text = tm != null ? tm.GetFormattedTime() : "-";   // 현재 HH:MM — TimeManager 실측
        }

        // ── 좌 패널 갱신 ──

        private void RefreshLeftPanel()
        {
            // 요약 — 소지금(PlayerStats 실측) + 'N / 40 슬롯'(실측 _maxSlots=40 — Figma 목업치 15 미채용)
            PlayerStats stats = PlayerStats.Instance;
            int gold = stats != null ? stats.Gold : 0;
            _invGoldLabel.text = $"소지금  {gold} G";

            PlayerInventory inventory = PlayerInventory.Instance;
            int usedSlots = 0, totalSlots = 0;
            if (inventory != null)
            {
                PlayerInventory.ItemSlot[] all = inventory.GetAllSlots();
                if (all != null)
                {
                    totalSlots = all.Length;   // [실측] _maxSlots(40) = 내부 슬롯 배열 길이 — GetAllSlots 경유 조회
                    foreach (PlayerInventory.ItemSlot slot in all)
                    {
                        if (slot != null && slot.item != null)
                            usedSlots++;
                    }
                }
            }
            _invSlotLabel.text = FormatSlotSummary(usedSlots, totalSlots);
            _invUsageFill.style.width = totalSlots > 0
                ? Length.Percent(Mathf.Clamp01(usedSlots / (float)totalSlots) * 100f)
                : new Length(0f, LengthUnit.Pixel);

            // 슬롯 그리드 — 탭 필터 + 검색(이름 contains)
            List<InventoryFoodEntry> entries = CollectInventoryFoods();
            var filtered = new List<InventoryFoodEntry>();
            int totalCount = 0;
            foreach (InventoryFoodEntry entry in entries)
            {
                if (!PassesTabFilter(_activeTab, entry)) continue;
                if (!string.IsNullOrEmpty(_searchText)
                    && entry.DisplayName.IndexOf(_searchText, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                filtered.Add(entry);
                totalCount += entry.count;
            }
            _slotSummaryLabel.text = $"{filtered.Count}종 · {totalCount}개";

            RebuildSlotGrid(filtered);
            RefreshSelectionBox();
        }

        /// <summary>보유 음식 수집 — Food 카테고리 + Drug 카테고리(drugged_ 접두 실물만).
        /// [88차 실측] drugged_ 아이템은 조리 시 기반 요리의 category를 승계(CookingUI 601행)하므로
        /// Food 필터에 포함된다. Drug 카테고리는 drugged_ 접두만 수용(생약 원료 drug_*는 음식이 아님 — 제외).</summary>
        private static List<InventoryFoodEntry> CollectInventoryFoods()
        {
            var result = new List<InventoryFoodEntry>();
            PlayerInventory inventory = PlayerInventory.Instance;
            if (inventory == null)
                return result;

            AddCategorySlots(result, inventory, PlayerInventory.ItemCategory.Food, requireDruggedPrefix: false);
            AddCategorySlots(result, inventory, PlayerInventory.ItemCategory.Drug, requireDruggedPrefix: true);
            return result;
        }

        private static void AddCategorySlots(
            List<InventoryFoodEntry> result, PlayerInventory inventory,
            PlayerInventory.ItemCategory category, bool requireDruggedPrefix)
        {
            PlayerInventory.ItemSlot[] slots = inventory.GetSlotsByCategory(category);
            if (slots == null)
                return;
            foreach (PlayerInventory.ItemSlot slot in slots)
            {
                if (slot == null || slot.item == null || slot.count <= 0)
                    continue;
                bool isDruggedFood = !string.IsNullOrEmpty(slot.item.id)
                    && slot.item.id.StartsWith("drugged_", System.StringComparison.Ordinal);
                if (requireDruggedPrefix && !isDruggedFood)
                    continue;   // Drug 카테고리 중 drugged_ 실물만(원료 drug_* 제외)
                result.Add(new InventoryFoodEntry { item = slot.item, count = slot.count, isDruggedFood = isDruggedFood });
            }
        }

        /// <summary>탭 필터 — 전체=Food+Drug(drugged_)/요리=Food/디저트·음료=서브카테고리 부재(0종)/재료=Meat+Herb.
        /// [실측] ItemCategory에 Crop/Dessert/Drink 멤버 없음(PlayerInventory.cs 39~54행) — 작물은 Food 카테고리.</summary>
        private static bool PassesTabFilter(InvTab tab, InventoryFoodEntry entry)
        {
            switch (tab)
            {
                case InvTab.All:
                    return true;   // 수집 단계에서 Food+Drug(drugged_)로 한정됨
                case InvTab.Cooked:
                    return entry.item.category == PlayerInventory.ItemCategory.Food;
                case InvTab.Dessert:
                case InvTab.Drink:
                    return false;  // 서브카테고리 부재 — '0종' 표시
                case InvTab.Ingredient:
                    return entry.item.category == PlayerInventory.ItemCategory.Meat
                        || entry.item.category == PlayerInventory.ItemCategory.Herb;
                default:
                    return false;
            }
        }

        private void RebuildSlotGrid(List<InventoryFoodEntry> filtered)
        {
            _slotGrid.Clear();

            if (filtered.Count == 0)
            {
                var hint = MakeLabel(
                    _activeTab == InvTab.Dessert || _activeTab == InvTab.Drink
                        ? "해당 분류의 음식이 없습니다 (0종)"
                        : "조건에 맞는 음식이 없습니다",
                    11f, 400, TextSub, "SlotGridHint");
                hint.style.unityTextAlign = TextAnchor.MiddleCenter;
                hint.style.height = SlotH;
                hint.style.minHeight = SlotH;
                _slotGrid.Add(hint);
                return;
            }

            // 5열×3행 그리드 — 행 컨테이너(h86 + margin-bottom 10) = 절대 y 214/310/406 상당 · 열간격 8(#36)
            for (int index = 0; index < filtered.Count || index < SlotCols * SlotRows; index++)
            {
                int row = index / SlotCols;
                if (row >= SlotRows && index >= filtered.Count)
                    break;   // 빈 슬롯 채움은 가시 3행까지

                if (index % SlotCols == 0)
                {
                    var gridRow = new VisualElement { name = "SlotRow" + row };
                    gridRow.style.flexDirection = FlexDirection.Row;
                    gridRow.style.height = SlotH;
                    gridRow.style.minHeight = SlotH;
                    gridRow.style.marginBottom = row < SlotRows - 1 ? SlotGap : 0f;
                    gridRow.style.flexShrink = 0f;
                    _slotGrid.Add(gridRow);
                }

                var slotRow = _slotGrid[_slotGrid.childCount - 1];
                if (index < filtered.Count)
                    slotRow.Add(BuildInventorySlot(filtered[index], IsSelected(filtered[index])));
                else
                    slotRow.Add(BuildEmptySlot());
            }
        }

        private bool IsSelected(InventoryFoodEntry entry)
        {
            return !string.IsNullOrEmpty(_selectedItemId) && entry.item != null && entry.item.id == _selectedItemId;
        }

        /// <summary>보유 슬롯 1칸 — 80×86 r6(#20262E, 선택 #253346) + 등급바 68×2 + 아이콘 32×32 + 이름/수량.</summary>
        private VisualElement BuildInventorySlot(InventoryFoodEntry entry, bool selected)
        {
            var slot = new VisualElement { name = "FoodSlot_" + entry.item.id };
            slot.style.width = SlotW;
            slot.style.minWidth = SlotW;
            slot.style.height = SlotH;
            slot.style.minHeight = SlotH;
            slot.style.flexShrink = 0f;
            slot.style.marginRight = SlotColGap;   // [144차 P7-RST #36] 열간격 8 — 행간격 10은 SlotRow marginBottom 유지
            slot.style.backgroundColor = new StyleColor(selected ? SlotSelectedCol : SlotCol);
            if (selected)
                UTKTheme.ApplyElementBorder(slot, Accent, 1.5f);   // [144차 P7-RST #37] 선택 슬롯 stroke #58A6FF w1.5(237:831)
            ApplyRadius(slot, RadiusBox);
            slot.style.overflow = Overflow.Hidden;
            slot.tooltip = $"{entry.DisplayName} ×{entry.count}";

            // 등급바 (x+6, y) 68×2 — 선택 #58A6FF / 일반 #65768A/0.4
            var gradeBar = new VisualElement { name = "GradeBar" };
            gradeBar.style.position = Position.Absolute;
            gradeBar.style.left = 6f;
            gradeBar.style.top = 0f;
            gradeBar.style.width = SlotGradeBarW;
            gradeBar.style.height = SlotGradeBarH;
            gradeBar.style.backgroundColor = new StyleColor(selected ? Accent : GradeBarIdle);
            gradeBar.pickingMode = PickingMode.Ignore;
            slot.Add(gradeBar);

            // 아이콘 32×32 — ItemIconDatabase 실측 선례(InventoryWindowUTK/UTKTextureSafe). 베이크 전/부재 시 생략.
            var iconHost = new VisualElement { name = "SlotIcon" };
            iconHost.style.position = Position.Absolute;
            iconHost.style.left = (SlotW - 32f) * 0.5f;
            iconHost.style.top = 14f;
            iconHost.style.width = 32f;
            iconHost.style.height = 32f;
            iconHost.pickingMode = PickingMode.Ignore;
            Texture2D icon = ItemIconDatabase.GetOrCreateIcon(entry.item);
            if (icon != null)
            {
                iconHost.style.backgroundImage = UTKTextureSafe.ToBackground(icon);
                iconHost.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            }
            slot.Add(iconHost);

            // 이름 10/400 y50(아이콘+gap5) / 수량 10/500 (우상단 y4) — [144차 P7-RST #40/#41]
            var nameLabel = MakeLabel(entry.DisplayName, 10f, 400, selected ? TextMain : TextSub, "SlotName");   // #40 비선택 #8B949E(237:839)
            nameLabel.style.position = Position.Absolute;
            nameLabel.style.left = 4f;
            nameLabel.style.right = 4f;
            nameLabel.style.top = 50f;   // [144차 P7-RST #40] 아이콘 직하 y50(237:834) — 하단 고정(bottom6) 폐기
            nameLabel.style.overflow = Overflow.Hidden;
            nameLabel.style.whiteSpace = WhiteSpace.NoWrap;
            nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            slot.Add(nameLabel);

            var countLabel = MakeLabel($"{entry.count}", 10f, 500, selected ? Accent : TextSub, "SlotCount");   // [144차 P7-RST #41] 선택 #58A6FF/비선택 #8B949E(237:835/840) — 500은 <600 Normal 근사(관행)
            countLabel.style.position = Position.Absolute;
            countLabel.style.right = 6f;
            countLabel.style.top = 4f;   // [144차 P7-RST #41] y4(237:835)
            slot.Add(countLabel);

            slot.RegisterCallback<PointerDownEvent>(_ => OnSlotClicked(entry.item.id, entry));
            return slot;
        }

        /// <summary>빈 슬롯 — dashed 보더 근사(#30363D/0.5 1px — UITK dash 미지원 근사 표기)
        /// + square-dashed 아이콘 25×25(벡터 18.8) #586473 중앙([144차 P7-RST #43]).</summary>
        private static VisualElement BuildEmptySlot()
        {
            var slot = new VisualElement { name = "EmptySlot" };
            slot.style.width = SlotW;
            slot.style.minWidth = SlotW;
            slot.style.height = SlotH;
            slot.style.minHeight = SlotH;
            slot.style.flexShrink = 0f;
            slot.style.marginRight = SlotColGap;   // [144차 P7-RST #36] 열간격 8
            slot.style.backgroundColor = new StyleColor(SlotCol);   // [144차 P7-RST #42] 빈 슬롯 fill #20262E(237:883) — 기존 stroke 유지
            slot.style.borderTopWidth = slot.style.borderBottomWidth = 1f;
            slot.style.borderLeftWidth = slot.style.borderRightWidth = 1f;
            slot.style.borderTopColor = slot.style.borderBottomColor =
                slot.style.borderLeftColor = slot.style.borderRightColor = new StyleColor(StrokeCol);
            ApplyRadius(slot, RadiusBox);
            // [144차 P7-RST #43] 빈 슬롯 square-dashed 아이콘 25×25(벡터 18.8) #586473 중앙(237:1110/1113/1116/1119/1122)
            //   — Resources/UI/icons에 dashed/square 계열 PNG 부재 → 사각 보더 근사(UTKControls V3 SetEmptyIcon
            //   square-dashed 사각 아웃라인 선례, dashed 미지원 기록).
            var emptyIconHost = new VisualElement { name = "EmptySlotIcon" };
            emptyIconHost.style.position = Position.Absolute;
            emptyIconHost.style.left = (SlotW - 25f) * 0.5f;
            emptyIconHost.style.top = (SlotH - 25f) * 0.5f;
            emptyIconHost.style.width = 25f;
            emptyIconHost.style.height = 25f;
            emptyIconHost.style.alignItems = Align.Center;
            emptyIconHost.style.justifyContent = Justify.Center;
            emptyIconHost.pickingMode = PickingMode.Ignore;
            var emptyIcon = new VisualElement { name = "EmptySlotIconSquare" };
            emptyIcon.style.width = 18.8f;   // 237:1111 벡터 실측 18.8×18.8
            emptyIcon.style.height = 18.8f;
            emptyIcon.style.borderTopWidth = emptyIcon.style.borderBottomWidth =
                emptyIcon.style.borderLeftWidth = emptyIcon.style.borderRightWidth = 1.4f;   // 237:1111 strokeW 1.4
            emptyIcon.style.borderTopColor = emptyIcon.style.borderBottomColor =
                emptyIcon.style.borderLeftColor = emptyIcon.style.borderRightColor = new StyleColor(TextDim);   // #586473
            emptyIcon.style.backgroundColor = new StyleColor(Color.clear);
            emptyIcon.pickingMode = PickingMode.Ignore;
            emptyIconHost.Add(emptyIcon);
            slot.Add(emptyIconHost);
            slot.pickingMode = PickingMode.Ignore;
            return slot;
        }

        /// <summary>선택 음식 배치 박스 — 이름/보유/권장가 + 수량조절(−/N/+) + 'N개 추가 배치' + 예상 문구.</summary>
        private void RefreshSelectionBox()
        {
            PlayerInventory inventory = PlayerInventory.Instance;

            if (!string.IsNullOrEmpty(_selectedItemId) && inventory != null)
            {
                _selectedHeld = inventory.GetItemCount(_selectedItemId);
                if (_selectedHeld <= 0)
                    ClearSelection();   // 보유 소진 — 선택 해제
            }

            bool hasSelection = !string.IsNullOrEmpty(_selectedItemId);
            VillageDrugSnapshot snapshot = _villageIndex >= 0
                ? VillageDrugState.GetSnapshot(_nation, _villageIndex)
                : default;

            if (hasSelection)
            {
                int basePrice = ResolveDisplayBasePrice(_selectedIsDrugged, GetSelectedBasePrice());
                int recommended = ResolveRowRecommendedPrice(_selectedIsDrugged, basePrice, snapshot.addiction);
                _selNameLabel.text = _selectedDisplayName;
                _selInfoLabel.text = $"보유 {_selectedHeld}개 · {_selectedCategoryLabel}";
                _selPriceLabel.text = $"권장가 {recommended} G";
                UpdateSelectionPreviewIcon();

                if (_placeQty > _selectedHeld)
                    _placeQty = Mathf.Max(_selectedHeld, 1);
                if (_placeQty < 1)
                    _placeQty = 1;

                int salePrice = ResolveRowSalePrice(_selectedIsDrugged, basePrice, snapshot.registeredPrice, snapshot.normalRegisteredPrice); // [100차] 일반 등록가 반영
                _qtyValueLabel.text = $"{_placeQty}";
                _maxQtyLabel.text = $"최대 {_selectedHeld}개";
                _placeButtonLabel.text = $"{_placeQty}개 추가 배치";
                _predictLabel.text = FormatAfterPlaceLine(_selectedHeld, _placeQty, salePrice);
                _placeButton.SetEnabled(true);
                _minusButton.SetEnabled(_placeQty > 1);
                _plusButton.SetEnabled(_placeQty < _selectedHeld);
            }
            else
            {
                _selNameLabel.text = "선택된 음식 없음";
                _selInfoLabel.text = "왼쪽 슬롯을 클릭해 음식을 선택하세요";
                _selPriceLabel.text = "-";
                _qtyValueLabel.text = "0";
                _maxQtyLabel.text = "최대 0개";
                _placeButtonLabel.text = "음식을 선택하세요";
                _predictLabel.text = "배치 후 보유 -개 · 예상 판매금 - G";
                _placeButton.SetEnabled(false);
                _minusButton.SetEnabled(false);
                _plusButton.SetEnabled(false);
            }
        }

        /// <summary>선택 미리보기(48×48) 아이콘 — ItemIconDatabase 실측 선례. 베이크 전/부재 시 박스만 표시(아이콘 생략).</summary>
        private void UpdateSelectionPreviewIcon()
        {
            if (_selectPreview == null) return;
            PlayerInventory.ItemData itemData = !string.IsNullOrEmpty(_selectedItemId)
                ? PlayerInventory.GetItemById(_selectedItemId)
                : null;
            Texture2D icon = itemData != null ? ItemIconDatabase.GetOrCreateIcon(itemData) : null;
            if (icon != null)
            {
                _selectPreview.style.backgroundImage = UTKTextureSafe.ToBackground(icon);
                _selectPreview.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            }
            else
            {
                _selectPreview.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            }
        }

        /// <summary>선택 항목 basePrice 캐시 조회 — 표시 폴백(0=무가격 규약 → DefaultFoodBasePrice 10G)은
        /// ResolveDisplayBasePrice에서 RestaurantSystem 입고 경로와 동일 규약 적용.</summary>
        private int _selectedBasePrice;

        private int GetSelectedBasePrice() => _selectedBasePrice;

        /// <summary>표시용 기본가 — 0=무가격 규약(O2 C-O2-02)이면 입고 폴백가(DefaultFoodBasePrice 10G) 근사.</summary>
        private static int ResolveDisplayBasePrice(bool isDrugged, int basePrice)
        {
            return basePrice > 0 ? basePrice : RestaurantSystem.DefaultFoodBasePrice;
        }

        // ── 우 패널 갱신 ──

        private void RefreshRightPanel(VillageDrugSnapshot snapshot)
        {
            // 운영 정보 — 마을명 실측 + 영업 배지(TimeManager.Hour 8~22 실측 판정)
            _opsTitleLabel.text = $"{ResolveVillageName()} · 모닥불 식당";
            TimeManager tm = TimeManager.Instance;
            int hour = tm != null ? tm.Hour : 0;
            bool open = IsOpenHour(hour);
            _opsBadgeLabel.text = FormatBusinessBadge(hour);
            _opsBadgeLabel.style.color = new StyleColor(open ? Accent : TextSub);
            _opsBadgeDot.style.backgroundColor = new StyleColor(open ? Accent : TextSub);

            List<RestaurantStockItem> stock = VillageDrugState.GetStock(_nation, _villageIndex);

            // '판매할 음식  N / 6' — RestaurantSystem.MaxStockSlots 실측 상한
            _stockCountLabel.text = FormatStockCounter(stock.Count, RestaurantSystem.MaxStockSlots);

            // 행 재구성 — 가격 TextField 편집 중이면 스킵(입력 덮어쓰기/포커스 상실 방지 — 기존 계약 패턴)
            if (!IsElementOrChildFocused(_stockList))
                RebuildStockRows(stock, snapshot);

            // 추가 배치 안내 — 'N칸 사용 가능' = 상한 - 현재 슬롯 수
            _freeSlotNoticeLabel.text =
                $"왼쪽 인벤토리에서 음식을 선택해 추가 배치하세요.  {ComputeFreeStockSlots(stock.Count)}칸 사용 가능";

            // 4지표 — 총 배치=ΣplacedTotal / 총 판매=ΣsoldTotal / 남은 음식=Σcount / 총 판매금=GetPendingRevenue 합
            int totalPlaced = 0, totalSold = 0, remaining = 0;
            foreach (RestaurantStockItem slot in stock)
            {
                totalPlaced += Mathf.Max(slot.placedTotal, 0);
                totalSold += Mathf.Max(slot.soldTotal, 0);
                remaining += Mathf.Max(slot.count, 0);
            }
            VillageDrugState.GetPendingRevenue(_nation, _villageIndex, out int pendingDrugGold, out int pendingNormalGold);
            int pendingTotal = pendingDrugGold + pendingNormalGold;

            _metricValueLabels[0].text = $"{totalPlaced}개";
            _metricValueLabels[1].text = $"{totalSold}개";
            _metricValueLabels[2].text = $"{remaining}개";
            _metricValueLabels[3].text = $"{pendingTotal} G";

            // 판매금 수령 박스 — '수령 후 소지금  X → Y G' (PlayerStats 실측)
            PlayerStats stats = PlayerStats.Instance;
            int wallet = stats != null ? stats.Gold : 0;
            _collectAmountLabel.text = $"받을 수 있는 판매금  {pendingTotal} G";
            _collectWalletLabel.text = $"수령 후 소지금  {wallet} → {wallet + pendingTotal} G  · 수수료 없음";
            _collectButton.SetEnabled(pendingTotal > 0);
        }

        private void RebuildStockRows(List<RestaurantStockItem> stock, VillageDrugSnapshot snapshot)
        {
            _stockList.Clear();

            if (stock.Count == 0)
            {
                var hint = MakeLabel("판매대가 비어 있습니다 — 왼쪽 인벤토리에서 음식을 배치하세요", 12f, 400, TextSub, "StockEmptyHint");
                hint.style.unityTextAlign = TextAnchor.MiddleCenter;
                hint.style.height = StockRowH;
                hint.style.minHeight = StockRowH;
                _stockList.Add(hint);
                return;
            }

            foreach (RestaurantStockItem slot in stock)
            {
                string key = $"{slot.foodItemId}|{slot.isDrugged}|{slot.drugStage}";
                var row = BuildStockRow(slot, snapshot, key == _selectedStockKey);
                _stockList.Add(row);
            }
        }

        /// <summary>판매 목록 행 920×74 — 열 x 14/394/500/610/680/768/888(리스트 로컬 [144차 P7-RST #82]).</summary>
        private VisualElement BuildStockRow(RestaurantStockItem slot, VillageDrugSnapshot snapshot, bool selected)
        {
            string key = $"{slot.foodItemId}|{slot.isDrugged}|{slot.drugStage}";   // 병합 키 — 행 선택 상태 식별자
            var row = new VisualElement { name = "StockRow_" + slot.foodItemId };
            row.style.width = ListW;
            row.style.minWidth = ListW;
            row.style.height = StockRowH;
            row.style.minHeight = StockRowH;
            row.style.flexShrink = 0f;
            row.style.backgroundColor = new StyleColor(selected ? AccentBg : SurfaceCol);
            if (selected)
            {
                // 선택 행 — 액센트 배경 + stroke 0.25
                row.style.borderTopWidth = row.style.borderBottomWidth = 1f;
                row.style.borderLeftWidth = row.style.borderRightWidth = 1f;
                row.style.borderTopColor = row.style.borderBottomColor =
                    row.style.borderLeftColor = row.style.borderRightColor = new StyleColor(RowStrokeSelected);
            }
            else
            {
                // [144차 P7-RST #86] 비선택 행 stroke — #30363D@0.502 (구 무보더 폐기). 선택행 stroke는 기존 유지.
                row.style.borderTopWidth = row.style.borderBottomWidth = 1f;
                row.style.borderLeftWidth = row.style.borderRightWidth = 1f;
                row.style.borderTopColor = row.style.borderBottomColor =
                    row.style.borderLeftColor = row.style.borderRightColor = new StyleColor(StrokeCol);
            }
            row.style.overflow = Overflow.Hidden;

            int displayBasePrice = ResolveDisplayBasePrice(slot.isDrugged, slot.basePrice);
            int recommended = ResolveRowRecommendedPrice(slot.isDrugged, displayBasePrice, snapshot.addiction);
            int salePrice = ResolveRowSalePrice(slot.isDrugged, displayBasePrice, snapshot.registeredPrice, snapshot.normalRegisteredPrice); // [100차] 일반 등록가 반영
            int rowGold = ComputeRowSaleGold(slot.soldTotal, salePrice);

            // 음식(14) — 미리보기 44×44 #20262E r6 + 이름 14/700 + 설명 10/400
            var preview = new VisualElement { name = "RowPreview" };
            preview.style.position = Position.Absolute;
            preview.style.left = ColXName;
            preview.style.top = (StockRowH - RowPreviewSize) * 0.5f;
            preview.style.width = RowPreviewSize;
            preview.style.height = RowPreviewSize;
            preview.style.backgroundColor = new StyleColor(SlotCol);
            ApplyRadius(preview, RadiusBox);
            preview.pickingMode = PickingMode.Ignore;
            PlayerInventory.ItemData itemData = PlayerInventory.GetItemById(slot.foodItemId);
            if (itemData != null)
            {
                Texture2D icon = ItemIconDatabase.GetOrCreateIcon(itemData);
                if (icon != null)
                {
                    preview.style.backgroundImage = UTKTextureSafe.ToBackground(icon);
                    preview.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                }
            }
            row.Add(preview);

            string displayName = string.IsNullOrEmpty(slot.displayName) ? slot.foodItemId : slot.displayName;
            var nameLabel = MakeLabel(displayName, 14f, 700, TextMain, "RowName");
            nameLabel.style.position = Position.Absolute;
            nameLabel.style.left = ColXName + RowPreviewSize + 12f;
            nameLabel.style.top = 20f;   // [144차 P7-RST #88] top16→20 — 이름 14/700
            nameLabel.style.right = ColXRecommended - 12f;
            nameLabel.style.overflow = Overflow.Hidden;
            nameLabel.style.whiteSpace = WhiteSpace.NoWrap;
            row.Add(nameLabel);

            // [144차 P7-RST #88] 부제 형식 실측 '요리 · …' — 소스(마약 stage N)는 코드 기존 유지, 형식만 '· ' 결합으로 조정.
            string desc = slot.isDrugged ? $"요리 · 마약 stage {slot.drugStage}" : "요리";
            var descLabel = MakeLabel(desc, 10f, 400, TextSub, "RowDesc");
            descLabel.style.position = Position.Absolute;
            descLabel.style.left = ColXName + RowPreviewSize + 12f;
            descLabel.style.top = 42f;   // [144차 P7-RST #88] top40→42 — 설명 10/400
            row.Add(descLabel);

            // 권장 가격(394) — 마약=RecommendedPrice(basePrice, addiction) / 일반=basePrice
            var recommendedLabel = MakeLabel($"{recommended} G", 13f, 400, TextSub, "RowRecommended");
            recommendedLabel.style.unityFont = GetGeistMonoFont();   // [144차 P7-RST #89] TextMain→#8B949E 13 GeistMono
            recommendedLabel.style.position = Position.Absolute;
            recommendedLabel.style.left = ColXRecommended;
            recommendedLabel.style.top = (StockRowH - 16f) * 0.5f;
            row.Add(recommendedLabel);

            // 판매 가격(500) — 가격박스 98×32 #0F141B r6. 마약행=registeredPrice TextField / 일반행=normalRegisteredPrice TextField.
            var priceBox = new VisualElement { name = "RowPriceBox" };
            priceBox.style.position = Position.Absolute;
            priceBox.style.left = ColXSell;
            priceBox.style.top = (StockRowH - PriceBoxH) * 0.5f;
            priceBox.style.width = PriceBoxW;
            priceBox.style.height = PriceBoxH;
            priceBox.style.backgroundColor = new StyleColor(InputCol);
            ApplyRadius(priceBox, RadiusBox);
            row.Add(priceBox);

            if (slot.isDrugged)
            {
                var priceField = new TextField { name = "RegisteredPriceField_" + slot.foodItemId, value = Mathf.Max(snapshot.registeredPrice, 0).ToString() };
                priceField.style.position = Position.Absolute;
                priceField.style.left = 8f;
                priceField.style.top = 2f;
                priceField.style.right = 22f;
                priceField.style.bottom = 2f;
                priceField.style.fontSize = 13f;
                priceField.style.backgroundColor = new StyleColor(Color.clear);
                priceField.style.borderTopWidth = priceField.style.borderBottomWidth = 0f;
                priceField.style.borderLeftWidth = priceField.style.borderRightWidth = 0f;
                priceField.style.color = new StyleColor(TextMain);
                priceField.RegisterCallback<FocusOutEvent>(_ => CommitRegisteredPrice(priceField));
                // [151차 P0] 키보드 전달 수리 — [150차 P4] 실측(FocusIn 2회/키다운 0회)의 확정 원인은 UITK 트리
                //   내부 소비가 아니라 전달 계층이다: 씬 EventSystem이 레거시 StandaloneInputModule이고
                //   activeInputHandler=Both 환경에서 상시 OnGUI 오버레이가 IMGUI 키 큐를 선소비하여
                //   PanelEventHandler.Update(키→포커스 요소 전달)가 빈손, LateUpdate는 target=null로 소각
                //   (ugui 패키지 PanelEventHandler.cs 실측). → 이 필드 편집 중에만 Keyboard.current.onTextInput
                //   주입(최후수단 준용), 실 KeyDownEvent 도달 감지 시 자가 은퇴(이중 입력 방지).
                //   전역 입력 모듈 교체(InputSystemUIInputModule)는 별도 과제 — 본 파일 소유권 밖.
                priceField.RegisterCallback<FocusInEvent>(_ => AttachPriceKeyboardBridge(priceField));
                priceField.RegisterCallback<FocusOutEvent>(_ => DetachPriceKeyboardBridge());
                priceField.RegisterCallback<KeyDownEvent>(_ =>
                {
                    if (_bridgeField != null)
                        DetachPriceKeyboardBridge();   // 실 전달 확인 — 브리지 자가 은퇴(전달 경로 정상화 대응)
                });
                priceField.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                    {
                        evt.StopPropagation();
                        CommitRegisteredPrice(priceField);
                    }
                });
                priceBox.Add(priceField);

                var unitLabel = MakeLabel("G", 11f, 400, TextSub, "RowPriceUnit");
                unitLabel.style.position = Position.Absolute;
                unitLabel.style.right = 10f;   // [144차 P7-RST #91] 화폐 right 8→10 — 가격 편집 TextField 기능 유지(표시만)
                unitLabel.style.top = (PriceBoxH - 14f) * 0.5f;
                unitLabel.pickingMode = PickingMode.Ignore;
                priceBox.Add(unitLabel);
            }
            else
            {
                // [100차] 일반행 가격 — basePrice 고정 표시 → normalRegisteredPrice TextField
                //         (마약행 동일 패턴 — 같은 행 가격박스에 편집 필드 추가, Figma 형태 최소 변경).
                var priceField = new TextField { name = "NormalRegisteredPriceField_" + slot.foodItemId, value = Mathf.Max(snapshot.normalRegisteredPrice, 0).ToString() };
                priceField.style.position = Position.Absolute;
                priceField.style.left = 8f;
                priceField.style.top = 2f;
                priceField.style.right = 22f;
                priceField.style.bottom = 2f;
                priceField.style.fontSize = 13f;
                priceField.style.backgroundColor = new StyleColor(Color.clear);
                priceField.style.borderTopWidth = priceField.style.borderBottomWidth = 0f;
                priceField.style.borderLeftWidth = priceField.style.borderRightWidth = 0f;
                priceField.style.color = new StyleColor(TextMain);
                priceField.RegisterCallback<FocusOutEvent>(_ => CommitNormalRegisteredPrice(priceField));
                // [151차 P0] 마약행 동일 패턴 — onTextInput 브리지(FocusIn 부착 / FocusOut 해제 /
                //   실 KeyDownEvent 감지 시 자가 은퇴 — 이중 입력 방지).
                priceField.RegisterCallback<FocusInEvent>(_ => AttachPriceKeyboardBridge(priceField));
                priceField.RegisterCallback<FocusOutEvent>(_ => DetachPriceKeyboardBridge());
                priceField.RegisterCallback<KeyDownEvent>(_ =>
                {
                    if (_bridgeField != null)
                        DetachPriceKeyboardBridge();   // 실 전달 확인 — 브리지 자가 은퇴
                });
                priceField.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                    {
                        evt.StopPropagation();
                        CommitNormalRegisteredPrice(priceField);
                    }
                });
                priceBox.Add(priceField);

                var unitLabel = MakeLabel("G", 11f, 400, TextSub, "RowPriceUnit");
                unitLabel.style.position = Position.Absolute;
                unitLabel.style.right = 10f;   // [144차 P7-RST #91] 화폐 right 8→10
                unitLabel.style.top = (PriceBoxH - 14f) * 0.5f;
                unitLabel.pickingMode = PickingMode.Ignore;
                priceBox.Add(unitLabel);
            }

            // 배치(610) — 현재 배치 수량
            var placedLabel = MakeLabel($"{Mathf.Max(slot.count, 0)}", 13f, 400, TextSub, "RowPlaced");
            placedLabel.style.unityFont = GetGeistMonoFont();   // [144차 P7-RST #92] TextMain→#8B949E 13 GeistMono
            placedLabel.style.position = Position.Absolute;
            placedLabel.style.left = ColXPlaced;
            placedLabel.style.top = (StockRowH - 16f) * 0.5f;
            row.Add(placedLabel);

            // 판매 / 배치(680) — 진행 76×27: '판매 / 배치' 13/400 + 바 66×3 r2
            // [144차 P7-RST #94] 바 트랙 abs (704,43.5)→(690,48) — 호스트는 열 x(−24 시프트) 유지, 트랙 인셋 (10, 24.5).
            var progressHost = new VisualElement { name = "RowProgress" };
            progressHost.style.position = Position.Absolute;
            progressHost.style.left = ColXProgress;
            progressHost.style.top = (StockRowH - 27f) * 0.5f;
            progressHost.style.width = 76f;
            progressHost.style.height = 27f;
            progressHost.pickingMode = PickingMode.Ignore;
            var progressLabel = MakeLabel($"{Mathf.Max(slot.soldTotal, 0)} / {Mathf.Max(slot.placedTotal, 0)}", 13f, 400, TextMain, "RowProgressLabel");
            progressLabel.style.position = Position.Absolute;
            progressLabel.style.left = 0f;
            progressLabel.style.top = 0f;
            progressHost.Add(progressLabel);

            var progressTrack = new VisualElement { name = "RowProgressTrack" };
            progressTrack.style.position = Position.Absolute;
            progressTrack.style.left = 10f;    // [144차 P7-RST #94] 바 abs x 690 = 680 + 10
            progressTrack.style.top = 24.5f;   // [144차 P7-RST #94] 바 abs y 48 = 23.5 + 24.5
            progressTrack.style.width = ProgressW;
            progressTrack.style.height = ProgressH;
            progressTrack.style.backgroundColor = new StyleColor(SlotCol);
            ApplyRadius(progressTrack, 2f);
            var progressFill = new VisualElement { name = "RowProgressFill" };
            float ratio = slot.placedTotal > 0 ? Mathf.Clamp01(slot.soldTotal / (float)slot.placedTotal) : 0f;
            progressFill.style.width = ProgressW * ratio;
            progressFill.style.height = ProgressH;
            progressFill.style.backgroundColor = new StyleColor(Accent);
            ApplyRadius(progressFill, 2f);
            progressTrack.Add(progressFill);
            progressHost.Add(progressTrack);
            row.Add(progressHost);

            // 판매금(768) — soldTotal × 가격, 14/500 #58A6FF
            var goldLabel = MakeLabel($"{rowGold} G", 14f, 500, Accent, "RowGold");
            goldLabel.style.position = Position.Absolute;
            goldLabel.style.left = ColXGold;
            goldLabel.style.top = (StockRowH - 18f) * 0.5f;
            row.Add(goldLabel);

            // 관리(888) — ellipsis 18×18(장식 — Figma 형태 계약 · 목록 우단 944 내부)
            var manageGlyph = BuildEllipsisGlyph(18f);
            manageGlyph.style.position = Position.Absolute;
            manageGlyph.style.left = ColXManage;
            manageGlyph.style.top = (StockRowH - 18f) * 0.5f;
            row.Add(manageGlyph);

            row.RegisterCallback<PointerDownEvent>(evt =>
            {
                // 가격 TextField 편집 클릭 — 행 선택 토글/리빌드가 편집을 파괴하지 않도록 가드
                for (var walker = evt.target as VisualElement; walker != null && walker != row; walker = walker.parent)
                {
                    if (walker is TextField)
                        return;
                }
                if (IsAnyTextFieldFocused())
                    return;
                _selectedStockKey = _selectedStockKey == key ? null : key;
                RefreshAll();
            });
            return row;
        }

        /// <summary>마을 등록가 커밋 — TextField 값 → SetRegisteredPrice(기존 계약 유지). 0 이상 정수만 수용.</summary>
        private void CommitRegisteredPrice(TextField field)
        {
            if (_villageIndex < 0 || field == null) return;
            string raw = field.value;
            if (!int.TryParse(raw?.Trim(), out int price) || price < 0)
            {
                UTKToastService.Show("판매 가격 등록 실패 — 0 이상의 정수를 입력하세요");
                return;
            }

            VillageDrugSnapshot snapshot = VillageDrugState.GetSnapshot(_nation, _villageIndex);
            if (snapshot.registeredPrice == price)
                return;   // 변경 없음 — 재등록 생략(폴링 토스트 스팸 방지)

            VillageDrugState.SetRegisteredPrice(_nation, _villageIndex, price);
            UTKToastService.Show($"판매 가격 등록: {price}G");
            RefreshAll();
        }

        /// <summary>
        /// [100차] 일반 음식 등록가 커밋 — TextField 값 → SetNormalRegisteredPrice(마약행 동일 패턴).
        /// 0 이상 정수만 수용, 0=미설정(재고 basePrice 사용 규약). 마약 registeredPrice와 독립 축.
        /// </summary>
        private void CommitNormalRegisteredPrice(TextField field)
        {
            if (_villageIndex < 0 || field == null) return;
            string raw = field.value;
            if (!int.TryParse(raw?.Trim(), out int price) || price < 0)
            {
                UTKToastService.Show("일반 음식 가격 등록 실패 — 0 이상의 정수를 입력하세요");
                return;
            }

            VillageDrugSnapshot snapshot = VillageDrugState.GetSnapshot(_nation, _villageIndex);
            if (snapshot.normalRegisteredPrice == price)
                return;   // 변경 없음 — 재등록 생략(폴링 토스트 스팸 방지)

            VillageDrugState.SetNormalRegisteredPrice(_nation, _villageIndex, price);
            UTKToastService.Show($"일반 음식 가격 등록: {price}G");
            RefreshAll();
        }

        // ═════════ [151차 P0] 가격 필드 키보드 브리지(최후수단 — 스코프드) ═════════
        // 근거: 씬 EventSystem = 레거시 StandaloneInputModule + activeInputHandler=Both + 상시 OnGUI 오버레이
        //   다수 → IMGUI 키 큐 선소비로 UITK KeyDownEvent 미전달([150차 P4] Editor.log 실측 일치).
        //   트리 내부는 전수 조사 무죄: TrickleDown 키 핸들러 0건 / OnRootKeyDown bubble+TextField 가드 /
        //   폴링 재빌드 IsElementOrChildFocused(_stockList) 가드 정상 / 행 클릭 가드 정상.
        // 범위: 가격 TextField 편집 중에만 Keyboard.current.onTextInput → 말미 추가 + 백스페이스/삭제/
        //   Enter(Blur → FocusOut 커밋 단일 경로). 커서 중간 삽입/선택치환 미지원(짧은 정수열 규약).
        // 은퇴: 실 KeyDownEvent 도달 즉시 해제(이중 입력 방지) · FocusOut · 창 닫힘 · 포커스 상실 자가 점검.

        private void AttachPriceKeyboardBridge(TextField field)
        {
            DetachPriceKeyboardBridge();   // 이중 부착 방지(멱등)
            if (UnityEngine.InputSystem.Keyboard.current == null) return;   // Input System 부재 — 기존 경로 의존
            _bridgeField = field;
            UnityEngine.InputSystem.Keyboard.current.onTextInput += OnPriceBridgeTextInput;
            _bridgeTick = schedule.Execute(PriceBridgeTick).Every(33);      // ≒1프레임 — wasPressedThisFrame 폴링
        }

        private void DetachPriceKeyboardBridge()
        {
            _bridgeField = null;
            if (_bridgeTick != null)
            {
                _bridgeTick.Pause();
                _bridgeTick = null;
            }
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
                kb.onTextInput -= OnPriceBridgeTextInput;   // 멱등 해제(구독 전 해제 패턴 준용)
        }

        private void OnPriceBridgeTextInput(char c)
        {
            TextField f = _bridgeField;
            if (f == null || !IsElementOrChildFocused(f))
            {
                DetachPriceKeyboardBridge();   // 포커스 상실 후 도달한 지연 이벤트 방어
                return;
            }
            f.value += c;   // 가격 입력 특성(짧은 정수열) — 말미 추가 규약
        }

        private void PriceBridgeTick()
        {
            TextField f = _bridgeField;
            if (f == null || !IsElementOrChildFocused(f))
            {
                DetachPriceKeyboardBridge();   // 편집 종료 — 틱 자가 소명
                return;
            }
            UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null)
            {
                DetachPriceKeyboardBridge();
                return;
            }
            if (kb.backspaceKey.wasPressedThisFrame)
            {
                if (f.value.Length > 0)
                    f.value = f.value.Substring(0, f.value.Length - 1);
            }
            else if (kb.deleteKey.wasPressedThisFrame)
            {
                f.value = string.Empty;
            }
            else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                f.Blur();   // FocusOut → CommitRegisteredPrice/CommitNormalRegisteredPrice 단일 커밋 경유
            }
        }

        // ═══════════════ 액션 핸들러 ═══════════════

        private void OnTabClicked(InvTab tab)
        {
            _activeTab = tab;
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                bool active = (InvTab)i == tab;
                _tabButtons[i].style.backgroundColor = new StyleColor(active ? Accent : SurfaceCol);
                _tabLabels[i].style.color = new StyleColor(active ? BtnTextDark : TextSub);
                _tabLabels[i].style.unityFontStyleAndWeight = FontStyle.Bold;   // [144차 P7-RST #28] 라벨 700 고정(비선택 포함)
                if (!active)
                    UTKTheme.ApplyElementBorder(_tabButtons[i]);   // 비선택 탭 크롬 보더 — 빌드 시와 동일 기준
                else
                {
                    var tb = _tabButtons[i].style;
                    tb.borderTopWidth = tb.borderBottomWidth = tb.borderLeftWidth = tb.borderRightWidth = 0f;
                }
            }
            RefreshLeftPanel();
        }

        private void OnSlotClicked(string itemId, InventoryFoodEntry entry)
        {
            _selectedItemId = itemId;
            _selectedDisplayName = entry.DisplayName;
            _selectedIsDrugged = entry.isDruggedFood;
            _selectedBasePrice = entry.item != null ? entry.item.basePrice : 0;
            _selectedCategoryLabel = entry.isDruggedFood
                ? "마약 요리"
                : entry.item.category == PlayerInventory.ItemCategory.Food ? "요리" : "재료";
            _placeQty = 1;
            _selectedStockKey = null;
            RefreshAll();
        }

        private void OnMinusClicked()
        {
            if (_placeQty > 1)
            {
                _placeQty--;
                RefreshSelectionBox();
            }
        }

        /// <summary>검색 placeholder 근사 — 값 공백 시에만 힌트 라벨 표시.</summary>
        private void RefreshSearchPlaceholder()
        {
            if (_searchHintLabel != null)
                _searchHintLabel.style.display = string.IsNullOrEmpty(_searchText) ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnPlusClicked()
        {
            if (!string.IsNullOrEmpty(_selectedItemId) && _placeQty < _selectedHeld)
            {
                _placeQty++;
                RefreshSelectionBox();
            }
        }

        /// <summary>'N개 추가 배치' → RestockFromPlayer(out reason 오버로드) → 성공/실패 토스트 + 즉시 RefreshAll.</summary>
        private void OnPlaceButtonClicked()
        {
            if (_villageIndex < 0 || string.IsNullOrEmpty(_selectedItemId)) return;

            bool ok = RestaurantSystem.RestockFromPlayer(_nation, _villageIndex, _selectedItemId, _placeQty, out string reason);
            UTKToastService.Show(ok
                ? $"{_selectedDisplayName} {_placeQty}개 배치 완료"
                : $"배치 실패 — {reason}");
            RefreshAll();
        }

        /// <summary>'판매금 받기' → CollectRevenue → 토스트 '판매금 N G 수령' + 즉시 갱신.</summary>
        private void OnCollectButtonClicked()
        {
            if (_villageIndex < 0) return;
            int collected = RestaurantSystem.CollectRevenue(_nation, _villageIndex);
            UTKToastService.Show(collected > 0 ? $"판매금 {collected} G 수령" : "수령할 판매금이 없습니다");
            RefreshAll();
        }

        // ═══════════════ UI 빌더 — 창 골격 ═══════════════

        /// <summary>헤더 @(64,36) 1472×18 — house 18×18 + '{마을명}' / '음식점 관리' | 우측 시각/일차.</summary>
        private void BuildHeaderBar()
        {
            var header = Block(HeaderX, HeaderY, HeaderW, HeaderH, "HeaderBar");
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;

            var house = BuildHouseGlyph(18f);
            header.Add(house);

            _headerVillageLabel = MakeLabel("-", 13f, 400, TextSub, "HeaderVillage");
            _headerVillageLabel.style.marginLeft = 12f;   // [144차 P7-RST #12] house→'전초기지' gap12(237:794)
            header.Add(_headerVillageLabel);

            var slash = MakeLabel("/", 12f, 400, TextDim, "HeaderSlash");   // [144차 P7-RST #14] fs12(237:797)
            slash.style.marginLeft = 12f;   // [144차 P7-RST #15] 구분 좌우 gap12(237:794)
            slash.style.marginRight = 12f;
            header.Add(slash);

            var crumb = MakeLabel("음식점 관리", 13f, 500, TextMain, "HeaderCrumb");
            header.Add(crumb);

            var spacer = new VisualElement { name = "HeaderSpacer" };
            spacer.style.flexGrow = 1f;
            spacer.pickingMode = PickingMode.Ignore;
            header.Add(spacer);

            var clock = BuildClockGlyph(14f);
            clock.style.marginRight = 10f;   // [144차 P7-RST #18] clock→날짜 gap10(237:799)
            header.Add(clock);

            _headerDayLabel = MakeLabel("-", 12f, 400, TextSub, "HeaderDay");
            _headerDayLabel.style.marginRight = 10f;
            header.Add(_headerDayLabel);

            _headerClockLabel = MakeLabel("-", 13f, 400, TextMain, "HeaderClock");
            header.Add(_headerClockLabel);

            // [134차 F] 부착 수리 — 빌더 루트를 _content에 직결 부착(기존 미부착 detached가 비표출 원인, 4빌더 동일)
            _content.Add(header);
        }

        /// <summary>좌 패널 480×882 @(64,78) — 인벤토리.</summary>
        private void BuildLeftPanel()
        {
            var panel = Block(LeftX, LeftY, LeftW, LeftH, "LeftPanel");
            panel.style.backgroundColor = new StyleColor(PanelGlassBg);   // [134차 C3] 플루언트 글래스 — #161B22@0.80 + stroke #30363D@0.502 w1.2(백드롭블러 미지원 알파 근사, 125후속38 선례)
            ApplyRadius(panel, RadiusPanel);
            ApplyStroke(panel);
            // [144차 P7-RST #8] 패널 fx(237:804) DROP_SHADOW 38.0 #000000/0.251 — 기록 전용(미적용): UTK 인라인 스타일에
            //   외곽 그림자 렌더 경로 부재 + 창레벨 DisableWindowShadow()(#9)와 충돌 여지(ConstructionWindowUTK [144차 P7-CON #5] 선례).
            panel.style.overflow = Overflow.Hidden;

            // ① 제목행 — backpack 23×23 + '인벤토리' 22/700 + '보유 음식' 11/400 + 닫기 30×30
            var backpack = BuildBackpackGlyph(23f);
            backpack.style.position = Position.Absolute;
            backpack.style.left = 24f;
            backpack.style.top = 28f;   // [134차 F] 진실치 backpack(24,28)
            panel.Add(backpack);

            var title = MakeLabel("인벤토리", 22f, 700, TextMain, "LeftTitle");
            title.style.position = Position.Absolute;
            title.style.left = 59f;   // [134차 F] 진실치 제목(59,26)
            title.style.top = 26f;
            panel.Add(title);

            var sub = MakeLabel("보유 음식", 11f, 400, TextSub, "LeftSub");
            sub.style.position = Position.Absolute;
            sub.style.left = 152f;   // [134차 F] 진실치 보조(152,32)
            sub.style.top = 32f;
            panel.Add(sub);

            panel.Add(BuildPanelCloseButton(LeftW - 24f - 30f, 24f, "LeftCloseButton"));   // [144차 P7-RST #23] 닫기 y24(237:809)

            // ② 분류 탭 5개 81.6×36 r6 gap6 — x 24/111.6/199.2/286.8/374.4
            string[] tabNames = { "전체", "요리", "디저트", "음료", "재료" };
            _tabButtons = new Button[tabNames.Length];
            _tabLabels = new Label[tabNames.Length];
            for (int i = 0; i < tabNames.Length; i++)
            {
                int tabIndex = i;   // 클로저 캡처 — for 루프 변수 공유 버그 방지(반복 변수 안전화)
                var tab = new Button(() => OnTabClicked((InvTab)tabIndex)) { name = "InvTab_" + tabNames[i] };
                tab.style.position = Position.Absolute;
                tab.style.left = 24f + i * (TabW + TabGap);
                tab.style.top = 88f;   // [134차 F] 진실치 분류탭 top88
                tab.style.width = TabW;
                tab.style.height = TabH;
                tab.style.backgroundColor = new StyleColor((InvTab)i == _activeTab ? Accent : SurfaceCol);
                ApplyRadius(tab, RadiusBox);
                if ((InvTab)i != _activeTab)
                    UTKTheme.ApplyElementBorder(tab);   // [144차 P7-RST #28] 비선택 탭 stroke #30363D@0.502(237:813/815/817/819/821)
                tab.style.paddingLeft = tab.style.paddingRight = 0f;
                tab.style.paddingTop = tab.style.paddingBottom = 0f;
                var tabLabel = MakeLabel(tabNames[i], 12f, 700,   // [144차 P7-RST #28] 라벨 12/700 — 비선택 포함(237:813 등)
                    (InvTab)i == _activeTab ? BtnTextDark : TextSub, "InvTabLabel");
                tabLabel.style.position = Position.Absolute;
                tabLabel.style.left = 0f;
                tabLabel.style.top = 0f;
                tabLabel.style.width = TabW;
                tabLabel.style.height = TabH;
                tabLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                tabLabel.pickingMode = PickingMode.Ignore;
                _tabLabels[i] = tabLabel;
                tab.Add(tabLabel);
                _tabButtons[i] = tab;
                panel.Add(tab);
            }

            // ③ 검색 432×34 #0F141B r6 — search 15×15 + '음식 이름 검색' + 정렬 16×16
            var searchBox = new VisualElement { name = "SearchBox" };
            searchBox.style.position = Position.Absolute;
            searchBox.style.left = 24f;
            searchBox.style.top = 140f;   // [134차 F] 진실치 검색 top140
            searchBox.style.width = 432f;
            searchBox.style.height = SearchH;
            searchBox.style.backgroundColor = new StyleColor(InputCol);
            ApplyRadius(searchBox, RadiusBox);
            var searchGlyph = BuildSearchGlyph(15f);
            searchGlyph.style.position = Position.Absolute;
            searchGlyph.style.left = 12f;
            searchGlyph.style.top = (SearchH - 15f) * 0.5f;
            searchBox.Add(searchGlyph);

            _searchField = new TextField { name = "FoodSearchField" };
            _searchField.style.position = Position.Absolute;
            _searchField.style.left = 36f;
            _searchField.style.right = 36f;
            _searchField.style.top = 2f;
            _searchField.style.bottom = 2f;
            _searchField.style.fontSize = 12f;
            _searchField.style.color = new StyleColor(TextMain);
            _searchField.style.backgroundColor = new StyleColor(Color.clear);
            _searchField.style.borderTopWidth = _searchField.style.borderBottomWidth = 0f;
            _searchField.style.borderLeftWidth = _searchField.style.borderRightWidth = 0f;
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _searchText = evt.newValue ?? string.Empty;
                RefreshSearchPlaceholder();
                RefreshLeftPanel();
            });
            searchBox.Add(_searchField);

            // placeholder 근사 — UITK TextField placeholder 미지원 → 값 공백 시 힌트 라벨(#586473 12/400)
            _searchHintLabel = MakeLabel("음식 이름 검색", 12f, 400, TextDim, "SearchHint");
            _searchHintLabel.style.position = Position.Absolute;
            _searchHintLabel.style.left = 35f;   // [144차 P7-RST #32] 박스-로컬 x35(237:824)
            _searchHintLabel.style.top = (SearchH - 16f) * 0.5f;
            _searchHintLabel.pickingMode = PickingMode.Ignore;
            searchBox.Add(_searchHintLabel);

            var sortGlyph = BuildSortGlyph(16f);
            sortGlyph.style.position = Position.Absolute;
            sortGlyph.style.right = 12f;
            sortGlyph.style.top = (SearchH - 16f) * 0.5f;
            searchBox.Add(sortGlyph);
            panel.Add(searchBox);

            // ④ '판매 가능한 음식' 12/700 + 'N종 · M개' 11/400
            var gridTitle = MakeLabel("판매 가능한 음식", 12f, 700, TextMain, "GridTitle");
            gridTitle.style.position = Position.Absolute;
            gridTitle.style.left = 24f;
            gridTitle.style.top = 190f;   // [134차 F] 진실치 판매 가능한 음식 top190
            panel.Add(gridTitle);

            _slotSummaryLabel = MakeLabel("0종 · 0개", 11f, 400, TextSub, "SlotSummary");
            _slotSummaryLabel.style.position = Position.Absolute;
            _slotSummaryLabel.style.right = 24f;
            _slotSummaryLabel.style.top = 190f;   // [144차 P7-RST #34] 진실치 y190 — 제목과 동행(237:829)
            panel.Add(_slotSummaryLabel);

            // ⑤ 슬롯 그리드 — 5열×3행 80×86 r6 gap10 [134차 F] (y 214/310/406: 214 + 행(86+10), 그리드 높이 278)
            _slotGrid = new ScrollView(ScrollViewMode.Vertical) { name = "FoodSlotGrid" };
            _slotGrid.style.position = Position.Absolute;
            _slotGrid.style.left = 24f;
            _slotGrid.style.top = 214f;
            _slotGrid.style.width = 432f;
            _slotGrid.style.height = SlotRows * SlotH + (SlotRows - 1) * SlotGap;
            _slotGrid.style.flexShrink = 0f;
            panel.Add(_slotGrid);

            // ⑥ 안내 @(24,502)
            var note = MakeLabel("판매대에 배치되지 않은 보유 수량입니다.", 11f, 400, TextSub, "SlotNote");
            note.style.position = Position.Absolute;
            note.style.left = 24f;
            note.style.top = NoteY;
            panel.Add(note);

            // ⑦ 선택 음식 배치 432×213 #58A6FF/0.078 r10
            var selectBox = new VisualElement { name = "SelectionBox" };
            selectBox.style.position = Position.Absolute;
            selectBox.style.left = 24f;
            selectBox.style.top = 531f;   // [134차 F] 진실치 선택음식배치 top531(재고안내 502와 29 간격)
            selectBox.style.width = SelectBoxW;
            selectBox.style.height = SelectBoxH;
            selectBox.style.backgroundColor = new StyleColor(AccentBg);
            ApplyRadius(selectBox, RadiusCard);
            UTKTheme.ApplyElementBorder(selectBox, RowStrokeSelected);   // [144차 P7-RST #46] stroke #58A6FF@0.251(237:899) — RowStrokeSelected 0.25 근사(85행 PASS 선례)
            panel.Add(selectBox);

            var preview = new VisualElement { name = "SelectPreview" };
            preview.style.position = Position.Absolute;
            preview.style.left = 16f;
            preview.style.top = 16f;
            preview.style.width = SelectPreviewSize;
            preview.style.height = SelectPreviewSize;
            preview.style.backgroundColor = new StyleColor(SlotCol);
            ApplyRadius(preview, RadiusBox);
            preview.pickingMode = PickingMode.Ignore;
            selectBox.Add(preview);
            _selectPreview = preview;

            _selNameLabel = MakeLabel("선택된 음식 없음", 15f, 700, TextMain, "SelectName");
            _selNameLabel.style.position = Position.Absolute;
            _selNameLabel.style.left = 76f;
            _selNameLabel.style.top = 22f;   // [144차 P7-RST #48] 음식명 (76,22)(237:904)
            _selNameLabel.style.right = 16f;
            _selNameLabel.style.overflow = Overflow.Hidden;
            _selNameLabel.style.whiteSpace = WhiteSpace.NoWrap;
            selectBox.Add(_selNameLabel);

            _selInfoLabel = MakeLabel("보유 0개 · -", 11f, 400, TextSub, "SelectInfo");
            _selInfoLabel.style.position = Position.Absolute;
            _selInfoLabel.style.left = 76f;
            _selInfoLabel.style.top = 44f;   // [144차 P7-RST #48] 보유량 (76,44)(237:905)
            selectBox.Add(_selInfoLabel);

            _selPriceLabel = MakeLabel("-", 15f, 400, Accent, "SelectPrice");
            _selPriceLabel.style.position = Position.Absolute;
            _selPriceLabel.style.right = 16f;   // [144차 P7-RST #49] 우측정렬(박스-로컬 371~416 → right16) · y30(237:906)
            _selPriceLabel.style.top = 30f;
            selectBox.Add(_selPriceLabel);

            // 수량행 — '배치 수량' 12/400 + 조절 116×32 #0F141B r6(−/N/+) + '최대 N개' 11/400 #58A6FF
            var qtyTitle = MakeLabel("배치 수량", 12f, 400, TextSub, "QtyTitle");   // [144차 P7-RST #50] #8B949E(237:908)
            qtyTitle.style.position = Position.Absolute;
            qtyTitle.style.left = 16f;
            qtyTitle.style.top = 87f;   // [144차 P7-RST #50] (16,87)(237:908)
            selectBox.Add(qtyTitle);

            var stepper = new VisualElement { name = "QtyStepper" };
            stepper.style.position = Position.Absolute;
            stepper.style.left = 158.5f;   // Figma 수량조절 rel 158.5
            stepper.style.top = 78f;
            stepper.style.width = StepperW;
            stepper.style.height = StepperH;
            stepper.style.backgroundColor = new StyleColor(InputCol);
            ApplyRadius(stepper, RadiusBox);
            UTKTheme.ApplyElementBorder(stepper, RowStrokeSelected);   // [144차 P7-RST #52] stroke #58A6FF@0.251(237:909) — RowStrokeSelected 0.25 근사(#46/#85 선례)
            _minusButton = new Button(OnMinusClicked) { name = "QtyMinus", text = "−" };
            StyleStepperButton(_minusButton, TextSub);
            PlaceStepperButton(_minusButton, plusSide: false);
            _qtyValueLabel = MakeLabel("0", 14f, 400, TextMain, "QtyValue");
            _qtyValueLabel.style.position = Position.Absolute;
            _qtyValueLabel.style.left = StepperSideW;    // [144차 P7-RST #53] 값칸 52 — −32/값52/+32(237:911 w52)
            _qtyValueLabel.style.right = StepperSideW;
            _qtyValueLabel.style.top = 0f;
            _qtyValueLabel.style.bottom = 0f;
            _qtyValueLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _qtyValueLabel.pickingMode = PickingMode.Ignore;
            _plusButton = new Button(OnPlusClicked) { name = "QtyPlus", text = "+" };
            StyleStepperButton(_plusButton, Accent);
            PlaceStepperButton(_plusButton, plusSide: true);
            stepper.Add(_minusButton);
            stepper.Add(_qtyValueLabel);
            stepper.Add(_plusButton);
            selectBox.Add(stepper);

            _maxQtyLabel = MakeLabel("최대 0개", 11f, 400, Accent, "MaxQty");
            _maxQtyLabel.style.position = Position.Absolute;
            _maxQtyLabel.style.right = 16f;
            _maxQtyLabel.style.top = 88f;   // [144차 P7-RST #55] y88(237:913 ay700−612) — 구 86 폐기
            selectBox.Add(_maxQtyLabel);

            // 실행 버튼 400×46 #58A6FF/0.078 r6 — arrow-right 18×18 + 'N개 추가 배치' 14/700 #58A6FF
            _placeButton = new Button(OnPlaceButtonClicked) { name = "PlaceButton", text = "" };
            _placeButton.style.position = Position.Absolute;
            _placeButton.style.left = 16f;
            _placeButton.style.top = 124f;   // [144차 P7-RST #56] (16,124)(237:914 ay736−612) — 구 120 폐기
            _placeButton.style.width = PlaceButtonW;
            _placeButton.style.height = PlaceButtonH;
            _placeButton.style.backgroundColor = new StyleColor(AccentBg);
            ApplyRadius(_placeButton, RadiusBox);
            UTKTheme.ApplyElementBorder(_placeButton, RowStrokeSelected);   // [144차 P7-RST #56] stroke #58A6FF@0.251(237:914) — RowStrokeSelected 0.25 근사(#46/#85 선례)
            _placeButton.style.paddingLeft = _placeButton.style.paddingRight = 0f;
            _placeButton.style.paddingTop = _placeButton.style.paddingBottom = 0f;
            var arrow = BuildArrowRightGlyph(18f);
            arrow.style.position = Position.Absolute;
            arrow.style.left = (PlaceButtonW - 114f) * 0.5f;   // [144차 P7-RST #57] 콘텐츠 그룹 중앙 — icon 18(143~161)+gap8+문구 88(169~257)=114 → left 143, 문구 전폭중앙(중심 200) 유지
            arrow.style.top = (PlaceButtonH - 18f) * 0.5f;
            _placeButton.Add(arrow);
            _placeButtonLabel = MakeLabel("음식을 선택하세요", 14f, 700, Accent, "PlaceButtonLabel");
            _placeButtonLabel.style.position = Position.Absolute;
            _placeButtonLabel.style.left = 0f;
            _placeButtonLabel.style.right = 0f;
            _placeButtonLabel.style.top = 0f;
            _placeButtonLabel.style.bottom = 0f;
            _placeButtonLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _placeButton.Add(_placeButtonLabel);
            selectBox.Add(_placeButton);

            _predictLabel = MakeLabel("배치 후 보유 -개 · 예상 판매금 - G", 11f, 400, TextSub, "PredictLine");
            _predictLabel.style.position = Position.Absolute;
            _predictLabel.style.left = 16f;
            _predictLabel.style.top = 184f;   // [144차 P7-RST #59] (16,184) — 구 176 폐기
            _predictLabel.style.width = PlaceButtonW;   // [144차 P7-RST #59] 400폭
            _predictLabel.style.unityTextAlign = TextAnchor.MiddleCenter;   // [144차 P7-RST #59] CENTER — 11/400 #8B949E(TextSub)
            selectBox.Add(_predictLabel);

            // ⑧ 요약 — [144차 P7-RST #63] 인벤 요약 스트립 432×44 @(24,814) 컨테이너 통합 + stroke #30363D@0.502
            var invSummary = new VisualElement { name = "InvSummary" };
            invSummary.style.position = Position.Absolute;
            invSummary.style.left = 24f;
            invSummary.style.top = 814f;   // [144차 P7-RST #63] (24,814) 432×44
            invSummary.style.width = 432f;
            invSummary.style.height = 44f;
            UTKTheme.ApplyElementBorder(invSummary);   // [144차 P7-RST #63] stroke #30363D@0.502(탭 #28/닫기 #24 선례)

            _invGoldLabel = MakeLabel("소지금  0 G", 12f, 400, TextSub, "WalletLine");   // [144차 P7-RST #60] #8B949E(TextSub) — 구 TextMain 폐기
            _invGoldLabel.style.position = Position.Absolute;
            _invGoldLabel.style.left = 0f;    // 컨테이너 로컬 (0,14) = 패널 절대 (24,828) 유지
            _invGoldLabel.style.top = 14f;
            invSummary.Add(_invGoldLabel);

            _invSlotLabel = MakeLabel("0 / 0 슬롯", 12f, 400, Accent, "SlotUsageLine");
            _invSlotLabel.style.position = Position.Absolute;
            _invSlotLabel.style.right = 0f;   // 컨테이너 로컬 우0 = 패널 우24(480−24=456=컨테이너 우측) 절대위치 유지
            _invSlotLabel.style.top = 14f;
            invSummary.Add(_invSlotLabel);

            var usageTrack = new VisualElement { name = "SlotUsageTrack" };
            usageTrack.style.position = Position.Absolute;
            usageTrack.style.left = 0f;
            usageTrack.style.top = 40f;   // 컨테이너 로컬 y40 = 패널 y854([134차 F]) 절대위치 유지
            usageTrack.style.width = UsageBarW;
            usageTrack.style.height = UsageBarH;
            usageTrack.style.backgroundColor = new StyleColor(SlotCol);
            ApplyRadius(usageTrack, 2f);
            _invUsageFill = new VisualElement { name = "SlotUsageFill" };
            _invUsageFill.style.width = 0f;
            _invUsageFill.style.height = UsageBarH;
            _invUsageFill.style.backgroundColor = new StyleColor(Accent);
            ApplyRadius(_invUsageFill, 2f);
            usageTrack.Add(_invUsageFill);
            invSummary.Add(usageTrack);
            panel.Add(invSummary);

            // [134차 F] 부착 수리 — 좌 패널 루트 _content 직결(선례: ConstructionWindowUTK [99차 F-E] _content.Add 패턴)
            _content.Add(panel);
        }

        private VisualElement _selectPreview;   // 선택 미리보기(48×48) 아이콘 갱신용

        /// <summary>우 패널 968×882 @(568,78) — 음식점 배치(판매 관리).</summary>
        private void BuildRightPanel()
        {
            // [134차 C3] 우패널(음식점 배치) 그리드 교정 대조 — 진실치(134_FIGMA_4FRAMES_TRUTH.md ①): 박스
            //   968×882 @(568,78) r14 + 열 x 38/418/524/634/704/792/912 · 행 920×74 · 목록 920×330 ·
            //   지표 221×80 gap12(88차 B2 dump 동일치) — 현재 상수와 ±4px 초과 차이 0건 → 기하 무수정(형식 유지).
            var panel = Block(RightX, RightY, RightW, RightH, "RightPanel");
            panel.style.backgroundColor = new StyleColor(PanelGlassBg);   // [134차 C3] 플루언트 글래스 — #161B22@0.80 + stroke #30363D@0.502 w1.2(백드롭블러 미지원 알파 근사, 125후속38 선례)
            ApplyRadius(panel, RadiusPanel);
            ApplyStroke(panel);
            // [144차 P7-RST #8] 패널 fx(237:925) DROP_SHADOW 38.0 #000000/0.251 — 기록 전용(미적용, 좌패널 #8 주석과 동일 근거).
            panel.style.overflow = Overflow.Hidden;

            // ① 제목행 — store 23×23 + '음식점 배치' 22/700 + '음식 판매 및 정산' 11/400 + 닫기 30×30
            var store = BuildStoreGlyph(23f);
            store.style.position = Position.Absolute;
            store.style.left = 24f;
            store.style.top = 27f;   // [144차 P7-RST #64] 우패널 로컬 (24,27) — 구 34 폐기
            panel.Add(store);

            var title = MakeLabel("음식점 배치", 22f, 700, TextMain, "RightTitle");
            title.style.position = Position.Absolute;
            title.style.left = 59f;   // [144차 P7-RST #65] (59,26) — 구 (57,24) 폐기
            title.style.top = 26f;
            panel.Add(title);

            var sub = MakeLabel("음식 판매 및 정산", 11f, 400, TextSub, "RightSub");
            sub.style.position = Position.Absolute;
            sub.style.left = 178f;   // [144차 P7-RST #65] 보조제목 우측 인라인 (178,33) 11/400 — 구 (57,52) 폐기
            sub.style.top = 33f;
            panel.Add(sub);

            panel.Add(BuildPanelCloseButton(RightW - 24f - 30f, 24f, "RightCloseButton"));   // [144차 P7-RST #23] 닫기 y24(237:930 — 좌우 공통, #66 동일 괴리 동반 해소)

            // ② 운영 정보 920×62 #191F27 r10 — utensils 36×36 + 마을명 + 영업 시간 + 영업 배지 62×23
            var opsCard = new VisualElement { name = "OpsCard" };
            opsCard.style.position = Position.Absolute;
            opsCard.style.left = 24f;
            opsCard.style.top = 90f;   // [144차 P7-RST #67] top90 — 구 92 폐기
            opsCard.style.width = 920f;
            opsCard.style.height = 62f;
            opsCard.style.backgroundColor = new StyleColor(SurfaceCol);
            ApplyRadius(opsCard, RadiusCard);
            UTKTheme.ApplyElementBorder(opsCard);   // [144차 P7-RST #68] stroke #30363D@0.502 — 구 무보더 폐기
            panel.Add(opsCard);

            var utensilsTile = new VisualElement { name = "UtensilsTile" };
            utensilsTile.style.position = Position.Absolute;
            utensilsTile.style.left = 16f;   // [144차 P7-RST #70] 카드로컬 (16,13) — 구 left14 폐기
            utensilsTile.style.top = 13f;
            utensilsTile.style.width = 36f;
            utensilsTile.style.height = 36f;
            utensilsTile.style.backgroundColor = new StyleColor(SlotCol);   // [144차 P7-RST #69] 타일 #20262E r6
            ApplyRadius(utensilsTile, RadiusBox);
            var utensils = BuildUtensilsGlyph(36f);   // [144차 P7-RST #69] 글리프색 Accent(#58A6FF)
            utensils.style.position = Position.Absolute;
            utensils.style.left = 0f;
            utensils.style.top = 0f;
            utensilsTile.Add(utensils);
            opsCard.Add(utensilsTile);

            _opsTitleLabel = MakeLabel("-", 14f, 700, TextMain, "OpsTitle");
            _opsTitleLabel.style.position = Position.Absolute;
            _opsTitleLabel.style.left = 64f;
            _opsTitleLabel.style.top = 15f;   // [144차 P7-RST #71] (64,15) 14/700 — 구 12 폐기
            opsCard.Add(_opsTitleLabel);

            var opsDesc = MakeLabel("영업 시간 08:00–22:00 · 배치한 음식이 자동으로 판매됩니다.", 11f, 400, TextSub, "OpsDesc");
            opsDesc.style.position = Position.Absolute;
            opsDesc.style.left = 64f;
            opsDesc.style.top = 34f;
            opsCard.Add(opsDesc);

            var badge = new VisualElement { name = "OpenBadge" };
            badge.style.position = Position.Absolute;
            badge.style.right = 16f;
            badge.style.top = 19f;
            badge.style.width = 62f;
            badge.style.height = 23f;
            badge.style.backgroundColor = new StyleColor(AccentBg);   // [144차 P7-RST #73] fill #58A6FF@0.078(AccentBg = Color(0.345,0.65,1,0.078f)) — 구 InputCol 폐기 · 62×23 right16/top19 진실치 유지
            ApplyRadius(badge, RadiusBox);   // [144차 P7-RST #73] r6(RadiusBox) — 구 r11.5(완전 pill) 폐기
            _opsBadgeDot = new VisualElement { name = "OpenBadgeDot" };
            _opsBadgeDot.style.position = Position.Absolute;
            _opsBadgeDot.style.left = 10f;
            _opsBadgeDot.style.top = 9f;
            _opsBadgeDot.style.width = 5f;
            _opsBadgeDot.style.height = 5f;
            _opsBadgeDot.style.borderTopLeftRadius = _opsBadgeDot.style.borderTopRightRadius = 2.5f;
            _opsBadgeDot.style.borderBottomLeftRadius = _opsBadgeDot.style.borderBottomRightRadius = 2.5f;
            _opsBadgeDot.style.backgroundColor = new StyleColor(Accent);
            _opsBadgeDot.pickingMode = PickingMode.Ignore;
            badge.Add(_opsBadgeDot);
            _opsBadgeLabel = MakeLabel("영업 중", 11f, 500, Accent, "OpenBadgeLabel");
            _opsBadgeLabel.style.position = Position.Absolute;
            _opsBadgeLabel.style.left = 20f;   // [144차 P7-RST #75] (20,6) — 구 (21,4) 폐기 · 11/500 #58A6FF(Accent)
            _opsBadgeLabel.style.top = 6f;
            _opsBadgeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;   // [144차 P7-RST #75] 볼드체 수리 — MakeLabel <600 Normal 근사 폐기(ConstructionWindowUTK #16 선례)
            _opsBadgeLabel.pickingMode = PickingMode.Ignore;
            badge.Add(_opsBadgeLabel);
            opsCard.Add(badge);

            // ③ '판매할 음식' 16/700 + 'N / 6' 12/400 #58A6FF + 보조 문구
            var listTitle = MakeLabel("판매할 음식", 16f, 700, TextMain, "StockTitle");
            listTitle.style.position = Position.Absolute;
            listTitle.style.left = 24f;
            listTitle.style.top = 170f;
            panel.Add(listTitle);

            _stockCountLabel = MakeLabel("0 / 6", 12f, 400, Accent, "StockCount");   // [144차 P7-RST #77] 12 GeistMono #58A6FF — 실데이터 갱신 경로(RefreshStockCounter) 유지
            _stockCountLabel.style.position = Position.Absolute;
            _stockCountLabel.style.left = 112f;   // [144차 P7-RST #77] 제목 우측 인라인 (112,171) — 구 우측정렬 right24/top173 폐기
            _stockCountLabel.style.top = 171f;
            _stockCountLabel.style.unityFont = GetGeistMonoFont();   // [144차 P7-RST #77] GeistMono — 에셋 부재 시 UIFont 폴백(WorldMap/WeaponForge 선례)
            panel.Add(_stockCountLabel);

            var listSub = MakeLabel("판매 가격은 개당 기준 · 수량 단위: 개", 11f, 400, TextSub, "StockSub");
            listSub.style.position = Position.Absolute;
            listSub.style.right = 24f;   // [144차 P7-RST #78] 우측정렬 right24 y173 — 구 좌측 (24,192) 폐기 · 11/400 유지
            listSub.style.top = 173f;
            panel.Add(listSub);

            // ④ 목록 920×330 r10 — 열헤더 920×34 #0F141B + 행 920×74
            var listHost = new VisualElement { name = "StockListHost" };
            listHost.style.position = Position.Absolute;
            listHost.style.left = 24f;
            listHost.style.top = 201f;   // [144차 P7-RST #79] top201 — 구 214 폐기 · 920×330 r10 불변
            listHost.style.width = ListW;
            listHost.style.height = ListH;
            ApplyRadius(listHost, RadiusCard);
            UTKTheme.ApplyElementBorder(listHost);   // [144차 P7-RST #80] stroke #30363D@0.502 — 구 무보더 폐기
            listHost.style.overflow = Overflow.Hidden;
            // [144차 P7-RST #80] fill 제거(투명) — 행이 배경 위에 오므로 호스트 배경은 비노출(구 InputCol #0F141B 폐기).
            panel.Add(listHost);

            var colHeader = new VisualElement { name = "StockColHeader" };
            colHeader.style.position = Position.Absolute;
            colHeader.style.left = 0f;
            colHeader.style.top = 0f;
            colHeader.style.width = ListW;
            colHeader.style.height = ColHeaderH;
            colHeader.style.backgroundColor = new StyleColor(InputCol);
            colHeader.pickingMode = PickingMode.Ignore;
            // [144차 P7-RST #83] 5열 정렬 라벨 — 권장R · 가격C · 배치R · 판매/배치R · 판매금R(unityTextAlign).
            // 정렬 가시화 위해 라벨 폭 = 다음 열 x − 열 x − 12 (98=가격박스, 76=진행호스트 폭과 일치).
            (string, float, TextAnchor)[] columns =
            {
                ("음식", ColXName, TextAnchor.MiddleLeft),
                ("권장 가격", ColXRecommended, TextAnchor.MiddleRight),
                ("판매 가격", ColXSell, TextAnchor.MiddleCenter),
                ("배치", ColXPlaced, TextAnchor.MiddleRight),
                ("판매 / 배치", ColXProgress, TextAnchor.MiddleRight),
                ("판매금", ColXGold, TextAnchor.MiddleRight),
                ("관리", ColXManage, TextAnchor.MiddleLeft),
            };
            for (int i = 0; i < columns.Length; i++)
            {
                (string colName, float colX, TextAnchor colAlign) = columns[i];
                var colLabel = MakeLabel(colName, 11f, 400, TextSub, "ColHeader_" + colName);
                colLabel.style.position = Position.Absolute;
                colLabel.style.left = colX;
                colLabel.style.top = (ColHeaderH - 14f) * 0.5f;
                colLabel.style.unityTextAlign = colAlign;
                if (colAlign != TextAnchor.MiddleLeft && i + 1 < columns.Length)
                    colLabel.style.width = columns[i + 1].Item2 - colX - 12f;
                colHeader.Add(colLabel);
            }
            listHost.Add(colHeader);

            _stockList = new ScrollView(ScrollViewMode.Vertical) { name = "StockList" };
            _stockList.style.position = Position.Absolute;
            _stockList.style.left = 0f;
            _stockList.style.top = ColHeaderH;
            _stockList.style.width = ListW;
            _stockList.style.height = ListH - ColHeaderH;
            _stockList.style.flexShrink = 0f;
            listHost.Add(_stockList);

            // ⑤ 추가 배치 안내 920×42 #0F141B r6 — plus 15×15 + 문구
            var notice = new VisualElement { name = "PlaceNotice" };
            notice.style.position = Position.Absolute;
            notice.style.left = 24f;
            notice.style.top = 543f;   // [144차 P7-RST #97] top556→543(237:1013 ay−81) — 920×42 #0F141B r6 불변
            notice.style.width = 920f;
            notice.style.height = NoticeH;
            notice.style.backgroundColor = new StyleColor(InputCol);
            ApplyRadius(notice, RadiusBox);
            UTKTheme.ApplyElementBorder(notice);   // [144차 P7-RST #97] stroke #30363D@0.502 — 구 무보더 폐기(#80/#92 선례)
            // [144차 P7-RST #98] plus+문구 콘텐츠 중앙 — 구 좌측 절대배치(left14/40) 폐기 · flex row Center + gap8 · 12/400 #8B949E 유지
            var noticeRow = new VisualElement { name = "NoticeRow" };
            noticeRow.style.flexDirection = FlexDirection.Row;
            noticeRow.style.alignItems = Align.Center;
            noticeRow.style.justifyContent = Justify.Center;
            noticeRow.style.width = Length.Percent(100f);
            noticeRow.style.height = Length.Percent(100f);
            noticeRow.pickingMode = PickingMode.Ignore;
            noticeRow.Add(BuildPlusGlyph(15f));   // [144차 P7-RST #99] 벡터색 TextDim(#586473) — BuildPlusGlyph 내 적용
            _freeSlotNoticeLabel = MakeLabel(
                "왼쪽 인벤토리에서 음식을 선택해 추가 배치하세요.  0칸 사용 가능", 12f, 400, TextSub, "FreeSlotNotice");
            _freeSlotNoticeLabel.style.marginLeft = 8f;   // [144차 P7-RST #98] gap8(plus→문구)
            noticeRow.Add(_freeSlotNoticeLabel);
            notice.Add(noticeRow);
            panel.Add(notice);

            // ⑥ '전체 판매 요약' 16/700 + '마지막 수령 이후 · 오늘 08:00부터' 11/400
            // [144차 P7-RST #101] 정산 컨테이너 920×262 @(24,603) — 제목/카드/수령박스 개별 절대배치 통합
            //   + stroke #30363D@0.502(구 무보더 폐기). 자식 절대좌표 보존 → 컨테이너 로컬 환산(−24, −603) 주석.
            var summaryHost = new VisualElement { name = "SummaryHost" };
            summaryHost.style.position = Position.Absolute;
            summaryHost.style.left = 24f;
            summaryHost.style.top = 603f;
            summaryHost.style.width = 920f;
            summaryHost.style.height = 262f;
            UTKTheme.ApplyElementBorder(summaryHost);   // [144차 P7-RST #101] stroke #30363D@0.502
            var summaryTitle = MakeLabel("전체 판매 요약", 16f, 700, TextMain, "SummaryTitle");
            summaryTitle.style.position = Position.Absolute;
            summaryTitle.style.left = 0f;   // [144차 P7-RST #100/#101] 컨테이너 로컬(창로컬 x24 − 24)
            summaryTitle.style.top = 16f;   // [144차 P7-RST #100] top614→619 → 컨테이너 로컬 619−603 · 16/700 유지
            summaryHost.Add(summaryTitle);

            var summarySub = MakeLabel("마지막 수령 이후 · 오늘 08:00부터", 11f, 400, TextSub, "SummarySub");
            summarySub.style.position = Position.Absolute;
            summarySub.style.right = 0f;   // [144차 P7-RST #100] 우측정렬 right24(창로컬) → 컨테이너 로컬 0 — 구 좌측(24,638) 폐기
            summarySub.style.top = 16f;    // [144차 P7-RST #100] y619 → 컨테이너 로컬 619−603 · 11/400 유지
            summaryHost.Add(summarySub);
            panel.Add(summaryHost);

            // ⑦ 4지표 카드 221×80 gap12 — 마지막 카드만 액센트 테두리·수치 #58A6FF
            string[] metricNames = { "총 배치 개수", "총 판매 개수", "남은 음식", "총 판매금" };
            _metricValueLabels = new Label[metricNames.Length];
            for (int i = 0; i < metricNames.Length; i++)
            {
                bool accentCard = i == metricNames.Length - 1;
                var card = new VisualElement { name = "MetricCard_" + metricNames[i] };
                card.style.position = Position.Absolute;
                card.style.left = i * (MetricW + MetricGap);   // [144차 P7-RST #102] 컨테이너 로컬(창로컬 x24/257/490/723 − 24) · 221×80 gap12 r10 불변
                card.style.top = 49f;   // [144차 P7-RST #102] top662→652 → 컨테이너 로컬 652−603
                card.style.width = MetricW;
                card.style.height = MetricH;
                // [144차 P7-RST #103] 일반 3개 fill #191F27 유지 + stroke #30363D@0.502 · 액센트 fill #191F27→#58A6FF@0.078(AccentBg)
                card.style.backgroundColor = accentCard ? new StyleColor(AccentBg) : new StyleColor(SurfaceCol);
                ApplyRadius(card, RadiusCard);
                if (accentCard)
                {
                    UTKTheme.ApplyElementBorder(card, RowStrokeSelected);   // [144차 P7-RST #103] stroke Accent1.0→#58A6FF@0.251 — RowStrokeSelected 0.25 근사(#46/#52/#56 선례)
                }
                else
                {
                    UTKTheme.ApplyElementBorder(card);   // [144차 P7-RST #103] stroke #30363D@0.502 — 구 무보더 폐기
                }
                var metricLabel = MakeLabel(metricNames[i], 11f, 400, TextSub, "MetricLabel");
                metricLabel.style.position = Position.Absolute;
                metricLabel.style.left = 14f;
                metricLabel.style.top = 14f;   // [144차 P7-RST #104] top12→14 — 11/400 유지
                card.Add(metricLabel);
                var metricValue = MakeLabel("0", 23f, 500, accentCard ? Accent : TextMain, "MetricValue");
                metricValue.style.position = Position.Absolute;
                metricValue.style.left = 14f;
                metricValue.style.top = 34f;   // [144차 P7-RST #104] top38→34 — 23 유지
                metricValue.style.unityFont = GetGeistMonoFont();   // [144차 P7-RST #104] GeistMono — 에셋 부재 시 UIFont 폴백(#77 선례)
                metricValue.style.unityFontStyleAndWeight = FontStyle.Bold;   // [144차 P7-RST #104] 500(볼드 근사)
                card.Add(metricValue);
                _metricValueLabels[i] = metricValue;
                summaryHost.Add(card);   // [144차 P7-RST #102] 정산 컨테이너 편입
            }

            // ⑧ 판매금 수령 920×92 #58A6FF/0.078 r10 — '받을 수 있는 판매금' + 버튼 250×46 '판매금 받기'
            var collectBox = new VisualElement { name = "CollectBox" };
            collectBox.style.position = Position.Absolute;
            collectBox.style.left = 0f;    // [144차 P7-RST #101] 컨테이너 로컬(창로컬 x24 − 24)
            collectBox.style.top = 143f;   // [144차 P7-RST #105] top758→746 → 컨테이너 로컬 746−603 · 920×92 r10 불변
            collectBox.style.width = 920f;
            collectBox.style.height = CollectBoxH;
            collectBox.style.backgroundColor = new StyleColor(AccentBg);
            ApplyRadius(collectBox, RadiusCard);
            UTKTheme.ApplyElementBorder(collectBox, RowStrokeSelected);   // [144차 P7-RST #105] stroke #58A6FF@0.251 — 구 무보더 폐기(RowStrokeSelected 0.25 근사)
            summaryHost.Add(collectBox);   // [144차 P7-RST #101] 정산 컨테이너 편입

            _collectAmountLabel = MakeLabel("받을 수 있는 판매금  0 G", 13f, 400, TextMain, "CollectAmount");
            _collectAmountLabel.style.position = Position.Absolute;
            _collectAmountLabel.style.left = 16f;   // [144차 P7-RST #106] (20,18)→(16,23) — 13/400 유지(pt16)
            _collectAmountLabel.style.top = 23f;
            collectBox.Add(_collectAmountLabel);

            _collectWalletLabel = MakeLabel("수령 후 소지금  0 → 0 G  · 수수료 없음", 11f, 400, TextSub, "CollectWallet");
            _collectWalletLabel.style.position = Position.Absolute;
            _collectWalletLabel.style.left = 16f;   // [144차 P7-RST #106] (20,44)→(16,55) — 11/400 유지(pt16 · 행간 gap5)
            _collectWalletLabel.style.top = 55f;
            collectBox.Add(_collectWalletLabel);

            _collectButton = new Button(OnCollectButtonClicked) { name = "CollectButton" };
            _collectButton.style.position = Position.Absolute;
            _collectButton.style.left = 920f - CollectButtonW - 20f;
            _collectButton.style.top = (CollectBoxH - CollectButtonH) * 0.5f;
            _collectButton.style.width = CollectButtonW;
            _collectButton.style.height = CollectButtonH;
            _collectButton.style.backgroundColor = new StyleColor(Accent);
            ApplyRadius(_collectButton, RadiusBox);
            _collectButton.style.fontSize = 14f;
            _collectButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            _collectButton.style.color = new StyleColor(BtnTextDark);
            _collectButton.style.paddingLeft = _collectButton.style.paddingRight = 0f;
            _collectButton.style.paddingTop = _collectButton.style.paddingBottom = 0f;
            // [144차 P7-RST #108] wallet 아이콘 18×18 #0B0E14 w1.7(237:1167-1168) — 버튼(250×46) 내 gap8 그룹
            //   중앙(아이콘 x78..96 + 문구 x104..170, 그룹 94px 중앙) 근사. Resources/UI/icons/wallet.png 실존하나
            //   #0B0E14 단색 아님(컬러 아이콘, 색상 불일치) → 글리프 근사(절차 아이콘 관행).
            _collectButton.text = string.Empty;
            var collectContent = new VisualElement { name = "CollectButtonContent" };
            collectContent.style.flexDirection = FlexDirection.Row;
            collectContent.style.alignItems = Align.Center;
            collectContent.style.justifyContent = Justify.Center;
            collectContent.style.width = Length.Percent(100f);
            collectContent.style.height = Length.Percent(100f);
            collectContent.pickingMode = PickingMode.Ignore;
            collectContent.Add(BuildWalletGlyph(18f));
            var collectLabel = MakeLabel("판매금 받기", 14f, 700, BtnTextDark, "CollectButtonLabel");
            collectLabel.style.marginLeft = 8f;   // gap8 — 237:1052 그룹(아이콘→문구 간격 47802+18→47828)
            collectContent.Add(collectLabel);
            _collectButton.Add(collectContent);
            collectBox.Add(_collectButton);

            var collectNote = MakeLabel(
                "판매금 수령 후에도 영업은 계속되며, 남은 음식은 판매대에 유지됩니다.", 11f, 400, TextSub, "CollectNote");
            collectNote.style.position = Position.Absolute;
            collectNote.style.left = 44f;    // [144차 P7-RST #110] info 아이콘 우측 — 237:1057 문구 x(47090−47046)
            collectNote.style.top = 852f;    // P1 보정 병행 — 860→852(237:1055 그룹 ay 933−81)
            panel.Add(collectNote);
            // [144차 P7-RST #110] info 아이콘 13×13 #8B949E @패널로컬(24,852)(237:1170-1171) — 라벨 좌측.
            var infoGlyph = BuildInfoGlyph(13f);
            infoGlyph.style.position = Position.Absolute;
            infoGlyph.style.left = 24f;
            infoGlyph.style.top = 852f;
            panel.Add(infoGlyph);

            // [134차 F] 부착 수리 — 우 패널 루트 _content 직결(좌 패널 동일 패턴)
            _content.Add(panel);
        }

        /// <summary>도움말 @(64,984) 1472×14 — 좌 안내 / 우 단축키.</summary>
        private void BuildHelpBar()
        {
            var help = Block(HelpX, HelpY, HelpW, HelpH, "HelpBar");
            help.style.flexDirection = FlexDirection.Row;
            help.style.alignItems = Align.Center;

            var left = MakeLabel("음식을 선택하고 수량을 정해 판매 목록에 배치하세요.", 12f, 400, TextSub, "HelpLeft");
            help.Add(left);

            var spacer = new VisualElement { name = "HelpSpacer" };
            spacer.style.flexGrow = 1f;
            spacer.pickingMode = PickingMode.Ignore;
            help.Add(spacer);

            var right = MakeLabel("클릭  음식 선택     ·     Enter  배치 확인     ·     Esc  닫기", 12f, 400, TextSub, "HelpRight");
            help.Add(right);

            // [134차 F] 부착 수리 — 도움말 바 루트 _content 직결(4빌더 부착 마무리)
            _content.Add(help);
        }

        /// <summary>패널 닫기 버튼 30×30(#20262E r6) — X 절차 글리프. 창 닫기(Hide).</summary>
        private Button BuildPanelCloseButton(float panelLocalX, float y, string buttonName)
        {
            var closeButton = new Button(Hide) { name = buttonName, text = "" };
            closeButton.style.position = Position.Absolute;
            closeButton.style.left = panelLocalX;
            closeButton.style.top = y;
            closeButton.style.width = 30f;
            closeButton.style.height = 30f;
            closeButton.style.backgroundColor = new StyleColor(SurfaceCol);   // [144차 P7-RST #24] fill #191F27(237:809/930)
            UTKTheme.ApplyElementBorder(closeButton);   // [144차 P7-RST #24] stroke #30363D@0.502(237:809/930)
            ApplyRadius(closeButton, RadiusBox);
            closeButton.style.paddingLeft = closeButton.style.paddingRight = 0f;
            closeButton.style.paddingTop = closeButton.style.paddingBottom = 0f;
            var xGlyph = BuildCloseGlyph(10f);
            xGlyph.style.position = Position.Absolute;
            xGlyph.style.left = 10f;
            xGlyph.style.top = 10f;
            closeButton.Add(xGlyph);
            return closeButton;
        }

        private static void StyleStepperButton(Button button, Color textColor)
        {
            button.style.position = Position.Absolute;
            button.style.top = 0f;
            button.style.bottom = 0f;
            button.style.width = StepperW / 3f;
            button.style.fontSize = 16f;
            button.style.color = new StyleColor(textColor);
            button.style.backgroundColor = new StyleColor(Color.clear);
            button.style.borderTopWidth = button.style.borderBottomWidth = 0f;
            button.style.borderLeftWidth = button.style.borderRightWidth = 0f;
            button.style.paddingLeft = button.style.paddingRight = 0f;
            button.style.paddingTop = button.style.paddingBottom = 0f;
        }

        /// <summary>수량조절 −/+ 버튼 x 배치 — 3등분(− 좌측, + 우측, 중앙 값).</summary>
        private static void PlaceStepperButton(Button button, bool plusSide)
        {
            button.style.left = plusSide ? StepperW * 2f / 3f : 0f;
        }

        // ═══════════════ 순수 표시 헬퍼 (EditMode 테스트 계약 — RestaurantWindowTests) ═══════════════

        /// <summary>좌 패널 슬롯 요약 — 'N / M 슬롯' (실측 _maxSlots=40 — Figma 목업치 15 미채용).</summary>
        public static string FormatSlotSummary(int usedSlots, int totalSlots)
            => $"{Mathf.Max(usedSlots, 0)} / {Mathf.Max(totalSlots, 0)} 슬롯";

        /// <summary>우 패널 재고 카운터 — 'N / 6' (RestaurantSystem.MaxStockSlots 실측 상한).</summary>
        public static string FormatStockCounter(int slots, int maxSlots)
            => $"{Mathf.Max(slots, 0)} / {Mathf.Max(maxSlots, 0)}";

        /// <summary>영업 시간 판정 — TimeManager.Hour 실측 8≤h<22 (Figma 영업 배지 계약).</summary>
        public static bool IsOpenHour(int hour) => hour >= 8 && hour < 22;

        /// <summary>영업 배지 문구 — '영업 중' / '영업 종료'.</summary>
        public static string FormatBusinessBadge(int hour) => IsOpenHour(hour) ? "영업 중" : "영업 종료";

        /// <summary>행 권장가 — 마약=VillageDrugMath.RecommendedPrice(basePrice, addiction) / 일반=basePrice.</summary>
        public static int ResolveRowRecommendedPrice(bool isDrugged, int basePrice, float addiction)
            => isDrugged ? VillageDrugMath.RecommendedPrice(basePrice, addiction) : basePrice;

        /// <summary>행 판매 가격 — 마약=마을 registeredPrice / 일반=basePrice 고정.</summary>
        public static int ResolveRowSalePrice(bool isDrugged, int basePrice, int registeredPrice)
            => isDrugged ? registeredPrice : basePrice;

        /// <summary>
        /// [100차] 행 판매 가격(일반 등록가 확장) — 마약=마을 registeredPrice /
        /// 일반=normalRegisteredPrice > 0 ? normalRegisteredPrice : basePrice(미설정 폴백). <!-- [151차 P0] 엔티티 누출 수리 -->
        /// 기존 3인자 오버로드는 [88차] 계약 유지(위임 없이 본문 그대로 보존 — 기존 어서션 보호).
        /// </summary>
        public static int ResolveRowSalePrice(bool isDrugged, int basePrice, int registeredPrice, int normalRegisteredPrice)
            => isDrugged ? registeredPrice : (normalRegisteredPrice > 0 ? normalRegisteredPrice : basePrice);

        /// <summary>행 판매금 — soldTotal × 판매 가격(음수 방어).</summary>
        public static int ComputeRowSaleGold(int soldTotal, int salePrice)
            => Mathf.Max(soldTotal, 0) * Mathf.Max(salePrice, 0);

        /// <summary>'배치 후 보유 N개 · 예상 판매금 N G'(=N×가격) 문구.</summary>
        public static string FormatAfterPlaceLine(int heldCount, int qty, int salePrice)
            => $"배치 후 보유 {Mathf.Max(heldCount - Mathf.Max(qty, 0), 0)}개 · 예상 판매금 {Mathf.Max(qty, 0) * Mathf.Max(salePrice, 0)} G";

        /// <summary>추가 배치 가능 슬롯 수 — 상한(RestaurantSystem.MaxStockSlots=6) − 현재 슬롯 수(하한 0).</summary>
        public static int ComputeFreeStockSlots(int stockCount)
            => Mathf.Max(RestaurantSystem.MaxStockSlots - Mathf.Max(stockCount, 0), 0);

        // ═══════════════ 내부 헬퍼 ═══════════════

        /// <summary>헤더/운영 정보 마을명 — 영지 정의명(TerritoryDatabase 실측). 정의 부재 시 국가+마을 번호 폴백.</summary>
        private string ResolveVillageName()
        {
            var db = TerritoryDatabase.Instance;
            string territoryName = db != null
                ? db.GetDefinition(_nation, _villageIndex + 1).territoryName   // [실측] villageIndex 0~19 ↔ 영지 인덱스 1~20
                : null;
            return !string.IsNullOrEmpty(territoryName)
                ? territoryName
                : $"{NationLabel(_nation)} 마을 #{_villageIndex + 1}";
        }

        /// <summary>국가 표기 라벨 — NationType 실측 멤버(East/West/South/North/Empire) 매핑(기존 패턴 재사용).</summary>
        private static string NationLabel(NationType nation)
        {
            switch (nation)
            {
                case NationType.East: return "동국";
                case NationType.West: return "서국";
                case NationType.South: return "남국";
                case NationType.North: return "북국";
                case NationType.Empire: return "황제국";
                default: return nation.ToString();
            }
        }

        /// <summary>포커스 판정 — 요소 자신 또는 하위에 포커스 존재(기존 계약: 6000.4.10f1 focusController.focusedElement 실측).</summary>
        private static bool IsElementOrChildFocused(VisualElement element)
        {
            if (element == null || element.panel == null || element.panel.focusController == null)
                return false;
            var focused = element.panel.focusController.focusedElement as VisualElement;
            return focused != null && (focused == element || element.Contains(focused));
        }

        // ── 절차 아이콘(벡터 이미지 미사용 — rect/circle 구성, SleepPanelUTK 73차 선례) ──

        /// <summary>house 18×18 — 지붕 사각 + 본체 사각.</summary>
        private static VisualElement BuildHouseGlyph(float size)
        {
            var host = new VisualElement { name = "HouseGlyph" };
            host.style.width = size;
            host.style.height = size;
            host.pickingMode = PickingMode.Ignore;
            var roof = new VisualElement();
            roof.style.width = size * 0.78f;
            roof.style.height = size * 0.28f;
            roof.style.backgroundColor = new StyleColor(TextSub);
            roof.style.position = Position.Absolute;
            roof.style.left = size * 0.11f;
            roof.style.top = size * 0.06f;
            roof.style.rotate = new Rotate(new Angle(45f, AngleUnit.Degree));
            roof.pickingMode = PickingMode.Ignore;
            host.Add(roof);
            var body = new VisualElement();
            body.style.width = size * 0.6f;
            body.style.height = size * 0.5f;
            body.style.backgroundColor = new StyleColor(TextSub);
            body.style.position = Position.Absolute;
            body.style.left = size * 0.2f;
            body.style.top = size * 0.44f;
            body.pickingMode = PickingMode.Ignore;
            host.Add(body);
            return host;
        }

        /// <summary>wallet 18×18 — 본체 스트로크 사각(14.2×13.5 w1.7) + 우측 clasp 사각
        /// ([144차 P7-RST #108] 237:1168 벡터 근사, #0B0E14 = BtnTextDark).</summary>
        private static VisualElement BuildWalletGlyph(float size)
        {
            var host = new VisualElement { name = "WalletGlyph" };
            host.style.width = size;
            host.style.height = size;
            host.pickingMode = PickingMode.Ignore;
            var body = new VisualElement();
            body.style.width = size * 0.79f;    // 14.2 — 237:1168 벡터 실측
            body.style.height = size * 0.75f;   // 13.5
            body.style.position = Position.Absolute;
            body.style.left = (size - size * 0.79f) * 0.5f;
            body.style.top = (size - size * 0.75f) * 0.5f;
            body.style.borderTopWidth = body.style.borderBottomWidth =
                body.style.borderLeftWidth = body.style.borderRightWidth = 1.7f;   // 237:1168 strokeW 1.7
            body.style.borderTopColor = body.style.borderBottomColor =
                body.style.borderLeftColor = body.style.borderRightColor = new StyleColor(BtnTextDark);
            body.style.borderTopLeftRadius = body.style.borderTopRightRadius =
                body.style.borderBottomLeftRadius = body.style.borderBottomRightRadius = size * 0.14f;
            body.style.backgroundColor = new StyleColor(Color.clear);
            body.pickingMode = PickingMode.Ignore;
            host.Add(body);
            var clasp = new VisualElement();
            clasp.style.width = size * 0.22f;
            clasp.style.height = size * 0.17f;
            clasp.style.position = Position.Absolute;
            clasp.style.right = size * 0.08f;
            clasp.style.top = size * 0.42f;
            clasp.style.backgroundColor = new StyleColor(BtnTextDark);
            clasp.pickingMode = PickingMode.Ignore;
            host.Add(clasp);
            return host;
        }

        /// <summary>info 13×13 — 원 테두리(10.8 w1.5) + 점/막대
        /// ([144차 P7-RST #110] 237:1171 벡터 근사, #8B949E = TextSub).</summary>
        private static VisualElement BuildInfoGlyph(float size)
        {
            var host = new VisualElement { name = "InfoGlyph" };
            host.style.width = size;
            host.style.height = size;
            host.pickingMode = PickingMode.Ignore;
            var circle = new VisualElement();
            circle.style.width = size * 0.83f;    // 10.8 — 237:1171 벡터 실측
            circle.style.height = size * 0.83f;
            circle.style.position = Position.Absolute;
            circle.style.left = (size - size * 0.83f) * 0.5f;
            circle.style.top = (size - size * 0.83f) * 0.5f;
            circle.style.borderTopWidth = circle.style.borderBottomWidth =
                circle.style.borderLeftWidth = circle.style.borderRightWidth = 1.5f;   // 237:1171 strokeW 1.5
            circle.style.borderTopColor = circle.style.borderBottomColor =
                circle.style.borderLeftColor = circle.style.borderRightColor = new StyleColor(TextSub);
            circle.style.borderTopLeftRadius = circle.style.borderTopRightRadius =
                circle.style.borderBottomLeftRadius = circle.style.borderBottomRightRadius = size * 0.5f;
            circle.style.backgroundColor = new StyleColor(Color.clear);
            circle.pickingMode = PickingMode.Ignore;
            host.Add(circle);
            var dot = new VisualElement();
            dot.style.width = 1.5f;
            dot.style.height = 1.5f;
            dot.style.position = Position.Absolute;
            dot.style.left = size * 0.5f - 0.75f;
            dot.style.top = size * 0.3f;
            dot.style.backgroundColor = new StyleColor(TextSub);
            dot.pickingMode = PickingMode.Ignore;
            host.Add(dot);
            var stem = new VisualElement();
            stem.style.width = 1.5f;
            stem.style.height = size * 0.34f;
            stem.style.position = Position.Absolute;
            stem.style.left = size * 0.5f - 0.75f;
            stem.style.top = size * 0.49f;
            stem.style.backgroundColor = new StyleColor(TextSub);
            stem.pickingMode = PickingMode.Ignore;
            host.Add(stem);
            return host;
        }

        /// <summary>clock 14×14 — 원 테두리 + 시침/분침 사각.</summary>
        private static VisualElement BuildClockGlyph(float size)
        {
            var host = new VisualElement { name = "ClockGlyph" };
            host.style.width = size;
            host.style.height = size;
            host.pickingMode = PickingMode.Ignore;
            var face = new VisualElement();
            face.style.width = size;
            face.style.height = size;
            face.style.borderTopLeftRadius = face.style.borderTopRightRadius = size * 0.5f;
            face.style.borderBottomLeftRadius = face.style.borderBottomRightRadius = size * 0.5f;
            face.style.borderTopWidth = face.style.borderBottomWidth =
                face.style.borderLeftWidth = face.style.borderRightWidth = 1.2f;
            face.style.borderTopColor = face.style.borderBottomColor =
                face.style.borderLeftColor = face.style.borderRightColor = new StyleColor(TextSub);
            face.style.backgroundColor = new StyleColor(Color.clear);
            face.style.position = Position.Absolute;
            face.pickingMode = PickingMode.Ignore;
            host.Add(face);
            var hand = new VisualElement();
            hand.style.width = 1.2f;
            hand.style.height = size * 0.32f;
            hand.style.backgroundColor = new StyleColor(TextSub);
            hand.style.position = Position.Absolute;
            hand.style.left = size * 0.5f - 0.6f;
            hand.style.top = size * 0.18f;
            hand.pickingMode = PickingMode.Ignore;
            host.Add(hand);
            return host;
        }

        /// <summary>store 23×23 — 차양 사각 3개 + 본체.</summary>
        private static VisualElement BuildStoreGlyph(float size)
        {
            var host = new VisualElement { name = "StoreGlyph" };
            host.style.width = size;
            host.style.height = size;
            host.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < 3; i++)
            {
                var awning = new VisualElement();
                awning.style.width = size * 0.26f;
                awning.style.height = size * 0.3f;
                awning.style.backgroundColor = new StyleColor(Accent);
                awning.style.position = Position.Absolute;
                awning.style.left = size * (0.08f + i * 0.3f);
                awning.style.top = 0f;
                awning.pickingMode = PickingMode.Ignore;
                host.Add(awning);
            }
            var body = new VisualElement();
            body.style.width = size * 0.74f;
            body.style.height = size * 0.56f;
            body.style.backgroundColor = new StyleColor(TextSub);
            body.style.position = Position.Absolute;
            body.style.left = size * 0.13f;
            body.style.top = size * 0.42f;
            body.pickingMode = PickingMode.Ignore;
            host.Add(body);
            return host;
        }

        /// <summary>utensils 36×36 — 포크(세로 막대 3) + 나이프. [144차 P7-RST #69] 글리프색 Accent(#58A6FF) — 구 TextSub 폐기.</summary>
        private static VisualElement BuildUtensilsGlyph(float size)
        {
            var host = new VisualElement { name = "UtensilsGlyph" };
            host.style.width = size;
            host.style.height = size;
            host.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < 3; i++)
            {
                var tine = new VisualElement();
                tine.style.width = 1.6f;
                tine.style.height = size * 0.42f;
                tine.style.backgroundColor = new StyleColor(Accent);
                tine.style.position = Position.Absolute;
                tine.style.left = size * 0.16f + i * 2.6f;
                tine.style.top = size * 0.12f;
                tine.pickingMode = PickingMode.Ignore;
                host.Add(tine);
            }
            var forkHandle = new VisualElement();
            forkHandle.style.width = 2f;
            forkHandle.style.height = size * 0.44f;
            forkHandle.style.backgroundColor = new StyleColor(Accent);
            forkHandle.style.position = Position.Absolute;
            forkHandle.style.left = size * 0.26f;
            forkHandle.style.top = size * 0.5f;
            forkHandle.pickingMode = PickingMode.Ignore;
            host.Add(forkHandle);

            var knife = new VisualElement();
            knife.style.width = size * 0.14f;
            knife.style.height = size * 0.8f;
            knife.style.backgroundColor = new StyleColor(Accent);
            knife.style.position = Position.Absolute;
            knife.style.left = size * 0.62f;
            knife.style.top = size * 0.1f;
            knife.style.borderTopLeftRadius = knife.style.borderTopRightRadius = size * 0.07f;
            knife.style.borderBottomLeftRadius = knife.style.borderBottomRightRadius = 1f;
            knife.pickingMode = PickingMode.Ignore;
            host.Add(knife);
            return host;
        }

        /// <summary>backpack 23×23 — 둥근 본체 + 상단 손잡이.</summary>
        private static VisualElement BuildBackpackGlyph(float size)
        {
            var host = new VisualElement { name = "BackpackGlyph" };
            host.style.width = size;
            host.style.height = size;
            host.pickingMode = PickingMode.Ignore;
            var strap = new VisualElement();
            strap.style.width = size * 0.4f;
            strap.style.height = size * 0.2f;
            strap.style.borderTopLeftRadius = strap.style.borderTopRightRadius = size * 0.2f;
            strap.style.borderBottomLeftRadius = strap.style.borderBottomRightRadius = 0f;
            strap.style.borderTopWidth = strap.style.borderBottomWidth =
                strap.style.borderLeftWidth = strap.style.borderRightWidth = 1.4f;
            strap.style.borderTopColor = strap.style.borderBottomColor =
                strap.style.borderLeftColor = strap.style.borderRightColor = new StyleColor(TextSub);
            strap.style.backgroundColor = new StyleColor(Color.clear);
            strap.style.position = Position.Absolute;
            strap.style.left = size * 0.3f;
            strap.style.top = 0f;
            strap.pickingMode = PickingMode.Ignore;
            host.Add(strap);
            var body = new VisualElement();
            body.style.width = size * 0.86f;
            body.style.height = size * 0.72f;
            body.style.backgroundColor = new StyleColor(TextSub);
            body.style.position = Position.Absolute;
            body.style.left = size * 0.07f;
            body.style.top = size * 0.26f;
            body.style.borderTopLeftRadius = body.style.borderTopRightRadius = size * 0.16f;
            body.style.borderBottomLeftRadius = body.style.borderBottomRightRadius = size * 0.16f;
            body.pickingMode = PickingMode.Ignore;
            host.Add(body);
            return host;
        }

        /// <summary>search 15×15 — 원 테두리 + 손잡이. [144차 P7-RST #30] 벡터색 #8B949E(237:1075).</summary>
        private static VisualElement BuildSearchGlyph(float size)
        {
            var host = new VisualElement { name = "SearchGlyph" };
            host.style.width = size;
            host.style.height = size;
            host.pickingMode = PickingMode.Ignore;
            var lens = new VisualElement();
            lens.style.width = size * 0.72f;
            lens.style.height = size * 0.72f;
            lens.style.borderTopLeftRadius = lens.style.borderTopRightRadius = size * 0.36f;
            lens.style.borderBottomLeftRadius = lens.style.borderBottomRightRadius = size * 0.36f;
            lens.style.borderTopWidth = lens.style.borderBottomWidth =
                lens.style.borderLeftWidth = lens.style.borderRightWidth = 1.4f;
            lens.style.borderTopColor = lens.style.borderBottomColor =
                lens.style.borderLeftColor = lens.style.borderRightColor = new StyleColor(TextSub);   // [144차 P7-RST #30] #8B949E(237:1075)
            lens.style.backgroundColor = new StyleColor(Color.clear);
            lens.style.position = Position.Absolute;
            lens.style.left = 0f;
            lens.style.top = 0f;
            lens.pickingMode = PickingMode.Ignore;
            host.Add(lens);
            var handle = new VisualElement();
            handle.style.width = 1.6f;
            handle.style.height = size * 0.36f;
            handle.style.backgroundColor = new StyleColor(TextSub);   // [144차 P7-RST #30]
            handle.style.position = Position.Absolute;
            handle.style.left = size * 0.78f;
            handle.style.top = size * 0.66f;
            handle.style.rotate = new Rotate(new Angle(-45f, AngleUnit.Degree));
            handle.pickingMode = PickingMode.Ignore;
            host.Add(handle);
            return host;
        }

        /// <summary>sort 16×16 — 세로 바 2개(정렬 표시 근사). [144차 P7-RST #30] 벡터색 #8B949E(237:1078).</summary>
        private static VisualElement BuildSortGlyph(float size)
        {
            var host = new VisualElement { name = "SortGlyph" };
            host.style.width = size;
            host.style.height = size;
            host.pickingMode = PickingMode.Ignore;
            var barA = new VisualElement();
            barA.style.width = 2f;
            barA.style.height = size;
            barA.style.backgroundColor = new StyleColor(TextSub);   // [144차 P7-RST #30] #8B949E(237:1078)
            barA.style.position = Position.Absolute;
            barA.style.left = size * 0.28f;
            barA.style.top = 0f;
            barA.pickingMode = PickingMode.Ignore;
            host.Add(barA);
            var barB = new VisualElement();
            barB.style.width = 2f;
            barB.style.height = size * 0.6f;
            barB.style.backgroundColor = new StyleColor(TextSub);   // [144차 P7-RST #30]
            barB.style.position = Position.Absolute;
            barB.style.left = size * 0.64f;
            barB.style.top = size * 0.4f;
            barB.pickingMode = PickingMode.Ignore;
            host.Add(barB);
            return host;
        }

        /// <summary>plus 15×15 — 십자 사각 2개.</summary>
        private static VisualElement BuildPlusGlyph(float size)
        {
            var host = new VisualElement { name = "PlusGlyph" };
            host.style.width = size;
            host.style.height = size;
            host.pickingMode = PickingMode.Ignore;
            var horizontal = new VisualElement();
            horizontal.style.width = size;
            horizontal.style.height = 2f;
            horizontal.style.backgroundColor = new StyleColor(TextDim);   // [144차 P7-RST #99] TextSub(#8B949E)→TextDim(#586473)
            horizontal.style.position = Position.Absolute;
            horizontal.style.top = (size - 2f) * 0.5f;
            horizontal.pickingMode = PickingMode.Ignore;
            host.Add(horizontal);
            var vertical = new VisualElement();
            vertical.style.width = 2f;
            vertical.style.height = size;
            vertical.style.backgroundColor = new StyleColor(TextDim);   // [144차 P7-RST #99] TextSub(#8B949E)→TextDim(#586473)
            vertical.style.position = Position.Absolute;
            vertical.style.left = (size - 2f) * 0.5f;
            vertical.pickingMode = PickingMode.Ignore;
            host.Add(vertical);
            return host;
        }

        /// <summary>arrow-right 18×18 — 화살표 축 + 촉(45° 회전 사각).</summary>
        private static VisualElement BuildArrowRightGlyph(float size)
        {
            var host = new VisualElement { name = "ArrowRightGlyph" };
            host.style.width = size;
            host.style.height = size;
            host.pickingMode = PickingMode.Ignore;
            var shaft = new VisualElement();
            shaft.style.width = size * 0.7f;
            shaft.style.height = 2f;
            shaft.style.backgroundColor = new StyleColor(Accent);
            shaft.style.position = Position.Absolute;
            shaft.style.left = 0f;
            shaft.style.top = (size - 2f) * 0.5f;
            shaft.pickingMode = PickingMode.Ignore;
            host.Add(shaft);
            var head = new VisualElement();
            head.style.width = size * 0.34f;
            head.style.height = size * 0.34f;
            head.style.borderTopWidth = 2f;
            head.style.borderRightWidth = 2f;
            head.style.borderTopColor = new StyleColor(Accent);
            head.style.borderRightColor = new StyleColor(Accent);
            head.style.backgroundColor = new StyleColor(Color.clear);
            head.style.position = Position.Absolute;
            head.style.left = size * 0.42f;
            head.style.top = size * 0.16f;
            head.style.rotate = new Rotate(new Angle(45f, AngleUnit.Degree));
            head.pickingMode = PickingMode.Ignore;
            host.Add(head);
            return host;
        }

        /// <summary>close X 10×10 — 45° 회전 사각 2개.</summary>
        private static VisualElement BuildCloseGlyph(float size)
        {
            var host = new VisualElement { name = "CloseGlyph" };
            host.style.width = size;
            host.style.height = size;
            host.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < 2; i++)
            {
                var bar = new VisualElement();
                bar.style.width = size * 1.2f;
                bar.style.height = 1.6f;
                bar.style.backgroundColor = new StyleColor(TextSub);
                bar.style.position = Position.Absolute;
                bar.style.left = -size * 0.1f;
                bar.style.top = (size - 1.6f) * 0.5f;
                bar.style.rotate = new Rotate(new Angle(i == 0 ? 45f : -45f, AngleUnit.Degree));
                bar.pickingMode = PickingMode.Ignore;
                host.Add(bar);
            }
            return host;
        }

        /// <summary>ellipsis 18×18 — 점 3개(관리 열 장식 — Figma 형태 계약).</summary>
        private static VisualElement BuildEllipsisGlyph(float size)
        {
            var host = new VisualElement { name = "EllipsisGlyph" };
            host.style.width = size;
            host.style.height = size;
            host.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < 3; i++)
            {
                var dot = new VisualElement();
                dot.style.width = 3f;
                dot.style.height = 3f;
                dot.style.borderTopLeftRadius = dot.style.borderTopRightRadius = 1.5f;
                dot.style.borderBottomLeftRadius = dot.style.borderBottomRightRadius = 1.5f;
                dot.style.backgroundColor = new StyleColor(TextSub);
                dot.style.position = Position.Absolute;
                dot.style.left = 3f + i * 4.5f;
                dot.style.top = (size - 3f) * 0.5f;
                dot.pickingMode = PickingMode.Ignore;
                host.Add(dot);
            }
            return host;
        }

        // ── 공용 스타일 헬퍼 ──

        /// <summary>절대 배치 블록 컨테이너(창/패널-로컬 좌표) — SleepPanelUTK Block 선례.</summary>
        private static VisualElement Block(float x, float y, float w, float h, string blockName)
        {
            var ve = new VisualElement { name = blockName };
            ve.style.position = Position.Absolute;
            ve.style.left = x;
            ve.style.top = y;
            ve.style.width = w;
            ve.style.height = h;
            return ve;
        }

        /// <summary>라벨 생성 — Figma 타이포 규약(웨이트 600 이상 = Bold, 미만 = Normal — FontStyle.Medium 부재 규약).</summary>
        private static Label MakeLabel(string text, float fontSize, int weight, Color color, string labelName)
        {
            var label = new Label(text ?? "") { name = labelName };
            label.style.fontSize = fontSize;
            label.style.unityFontStyleAndWeight = weight >= 600 ? FontStyle.Bold : FontStyle.Normal;
            label.style.color = new StyleColor(color);
            return label;
        }

        /// <summary>모서리 반경 일괄 적용.</summary>
        private static void ApplyRadius(VisualElement ve, float radius)
        {
            ve.style.borderTopLeftRadius = ve.style.borderTopRightRadius = radius;
            ve.style.borderBottomLeftRadius = ve.style.borderBottomRightRadius = radius;
        }

        /// <summary>패널 보더 — #30363D/0.5 1px.</summary>
        private static void ApplyStroke(VisualElement ve)
        {
            ve.style.borderTopWidth = ve.style.borderBottomWidth = 1f;
            ve.style.borderLeftWidth = ve.style.borderRightWidth = 1f;
            ve.style.borderTopColor = ve.style.borderBottomColor =
                ve.style.borderLeftColor = ve.style.borderRightColor = new StyleColor(StrokeCol);
        }

        /// <summary>[144차 P7-RST #77] Geist Mono 로드(Figma 패밀리 계약) — 에셋 부재 시 UIFont 폴백
        /// (WorldMapWindowUTK/WeaponForgeUTK GetGeistMonoFont 선례 — 없는 폰트를 흉내 내지 않는 위조 금지 계약).</summary>
        private static Font _geistMono;

        private static Font GetGeistMonoFont()
        {
            if (_geistMono == null)
                _geistMono = Resources.Load<Font>("Fonts/GeistMono");
            return _geistMono != null ? _geistMono : UIFont.Load();
        }
    }
}
