// [90차 Part B] 건설 관리 창 — Figma 239:683 "전초기지 건설 관리" 1600×1040 전면 재작성.
// 기존 O8 설계도 나열형 창을 Figma 정합 관리 UI로 교체:
//  · 좌 '건설 목록' — 시설 6종(Figma 항목 + [152차 P0] 훈련소) + '기타 8' 보존 탭(기존 O8 8종 — 기능 회귀 방지).
//  · 우 '건설 정보' — 개요/건설 조건 3카드/필요 재료 표/건설 실행 박스.
//  · 배치 흐름은 기존 계약 유지(GetPlacePosition → ResolveTerritoryAt → TryPlace → 반환 메시지 토스트).
//  · [90차 B2] FacilityPropBuilder.FacilityOpenRequested 구독 → 시설 창 라우팅(Bootstrap 구독 전 해제 패턴).
#pragma warning disable 114   // static Toggle()/Open()가 베이스 멤버를 숨김 — 의도됨(기존 계약)
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;            // PlayerInventory/PlayerStats
using ProjectName.Core.Data;       // BlueprintData/TerritoryDatabase
using ProjectName.Systems;         // ConstructionManager/FacilityPropBuilder/TimeManager/GuardManager
using ProjectName.UI;              // ItemIconDatabase — 재료 아이콘 실측 렌더(88차 선례)

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// [90차 B1] 건설 관리 창 (UTK) — Figma 239:683 "전초기지 건설 관리" 1600×1040.
    ///
    /// [레이아웃 — ApplyCanvasFit 84차 계약]
    ///  · 창박스 = figma(1600×1040)×k, 내부 raw 유지 + transform.scale=k, k=min(rootW/1920, rootH/1080).
    ///  · [100차 F-4] figma 239:683 재실측 확정 — 헤더 @(64,36) 1472×18 / 좌 '건설 목록' 480×882 @(64,78) /
    ///    우 '건설 정보' 968×882 @(568,78) / 도움말 @(64,984) 1472×20. 채움률 검증 로그([100차 F-4] 채움)는
    ///    MeasureGeometryAndDefend 사이클마다 worldBound 기준 기록.
    ///  · 패널 bg #13181F r14, stroke #30363D/0.5. 반경 14(패널)/10(카드)/6(버튼·박스).
    ///
    /// [데이터 규약 — 위조 금지, 전부 실측 API 단일 소스]
    ///  · 보유량 = PlayerInventory.GetItemCount(5종 — ConstructionManager 재료 id 상수 단일소스).
    ///  · 충족 판정/부족 카운트 = ConstructionManager.HasEnoughMaterials 10인자 규약(요구 ≤ 0 = 무관 충족).
    ///  · 건설 시간/부지 = BlueprintDef(buildTimeSeconds/footprintRadius). 유휴 인원 = GuardManager 가용 병사 실측.
    ///  · Figma 목업 수치(보유 180/120/24/18/8, 부지 3×4, 유휴 3명) 미채용 — 전부 실측치.
    ///
    /// [배치 흐름 — 기존 O8 계약 유지] 선택 시설 '건설하기'/Enter → GetPlacePosition(카메라 forward×5)
    ///  → TerritoryDatabase.ResolveTerritoryAt → ConstructionManager.TryPlace(재료 5종 소비/롤백은 Systems 몫)
    ///  → 반환 메시지 토스트 + 즉시 Refresh. 폴링 500ms(Updater 패턴 유지).
    ///
    /// [단축키] U = 토글(기존 계약), Enter = 선택 시설 건설 실행, Esc = 창 닫기.
    /// </summary>
    public class ConstructionWindowUTK : UTKWindowBase
    {
        // ═══════════════ [90차 B1] Figma 239:683 진실치 ═══════════════

        private const float WinW = 1600f;        // 창 1600×1040
        private const float WinH = 1040f;
        private const float RadiusPanel = 14f;   // 패널 r14
        private const float RadiusCard = 10f;    // 카드 r10
        private const float RadiusBox = 6f;      // 버튼·박스 r6

        // 창 자식 박스 — 헤더 @(64,36) / 좌 @(64,78) / 우 @(568,78) / 도움말 @(64,984)
        // [100차 F-4] figma 239:683 재실측(프레임 raw 1600×1040, 프레임 원점 차감 로컬 좌표) 확정치 —
        //   HeaderBar(관리 화면 헤더) 1472×18 @(64,36) / LeftPanel(건설 목록) 480×882 @(64,78) /
        //   RightPanel(선택 시설 건설 정보) 968×882 @(568,78) / HelpBar(화면 도움말) 1472×20 @(64,984).
        //   기존 좌표와의 차이는 HelpBar 높이 14→20뿐(재실측로 헤더/좌/우 패널 좌표는 기존값으로 재확인) —
        //   4블록이 창박스 내 설계 영역을 정확히 채운다(채움률 검증 로그는 MeasureGeometryAndDefend 경로).
        private const float HeaderX = 64f, HeaderY = 36f, HeaderW = 1472f, HeaderH = 18f;
        private const float LeftX = 64f, LeftY = 78f, LeftW = 480f, LeftH = 882f;
        private const float RightX = 568f, RightY = 78f, RightW = 968f, RightH = 882f;
        private const float HelpX = 64f, HelpY = 984f, HelpW = 1472f, HelpH = 20f;   // [100차 F-4] 14→20 재실측치
        private const float PanelPad = 24f;      // 패널 내측 여백(좌우 24)

        // ── 색 토큰 (Figma 실측 hex) ──
        private static readonly Color PanelBg = new Color32(0x13, 0x18, 0x1F, 0xFF);   // #13181F 패널 bg
        private static readonly Color StrokeCol = new Color32(0x30, 0x36, 0x3D, 0x80); // #30363D/0.5 보더
        private static readonly Color SurfaceCol = new Color32(0x19, 0x1F, 0x27, 0xFF); // #191F27 표면
        private static readonly Color InputCol = new Color32(0x0F, 0x14, 0x1B, 0xFF);  // #0F141B 어두운 표면
        private static readonly Color SlotCol = new Color32(0x20, 0x26, 0x2E, 0xFF);   // #20262E 아이콘 박스
        private static readonly Color ScrollTrackCol = new Color32(0x20, 0x26, 0x2E, 0xFF); // #20262E 스크롤바 트랙 [125차 후속37] — SlotCol 동값(Figma scrollbar-track 실측)
        private static readonly Color ItemSelectedCol = new Color32(0x1A, 0x28, 0x39, 0xFF); // #1A2839 선택 항목 bg
        private static readonly Color DisabledBoxCol = new Color32(0x25, 0x2D, 0x38, 0xFF);  // #252D38 비활성 버튼
        private static readonly Color TextMain = new Color32(0xF0, 0xF6, 0xFC, 0xFF);  // #F0F6FC 본문
        private static readonly Color TextSub = new Color32(0x8B, 0x94, 0x9E, 0xFF);   // #8B949E 보조
        private static readonly Color TextDim = new Color32(0x58, 0x64, 0x73, 0xFF);   // #586473 잠금/구분자
        private static readonly Color Accent = new Color32(0x58, 0xA6, 0xFF, 0xFF);    // #58A6FF 액센트
        private static readonly Color Warn = new Color32(0xE5, 0xAD, 0x65, 0xFF);      // #E5AD65 경고(재료 부족)
        private static readonly Color BtnTextDark = new Color32(0x0B, 0x0E, 0x14, 0xFF); // #0B0E14 강조 버튼 글자
        private static readonly Color FigmaRootBg = new Color32(0x0B, 0x0E, 0x14, 0xFF); // #0B0E14 Figma 루트 캔버스 fill [112차 F-A]
        private const float AccentAlpha = 0.078f;                                      // 액센트 배경 알파 #58A6FF/0.078
        private static readonly Color AccentBg = new Color(0x58 / 255f, 0xA6 / 255f, 0xFF / 255f, AccentAlpha);
        private const float WarnAlpha = 0.063f;                                        // 경고 배경 알파 #E5AD65/0.063
        private static readonly Color WarnBg = new Color(0xE5 / 255f, 0xAD / 255f, 0x65 / 255f, WarnAlpha);
        private const float AccentStrokeAlpha = 0.25f;                                 // 실행 박스 충족 stroke 0.25
        private const float WarnStrokeAlpha = 0.22f;                                   // 부족 행/박스 stroke 0.22

        // [94차 F2] 코드 배경 폴백 톤 — 스펙 결함 2 수리 1. ThemeUss 배경 리소스(PNG) 누락과 무관하게
        //   창 패널이 항상 보이도록 하는 인라인 단색+보더(스펙 지정 Figma 다크 톤: 패널 #1B1F27/0.92, 보더 #3A4150).
        private static readonly Color FallbackPanelBg = new Color(0x1B / 255f, 0x1F / 255f, 0x27 / 255f, 0.92f);
        private static readonly Color FallbackPanelBorder = new Color32(0x3A, 0x41, 0x50, 0xFF);

        // ── 좌 패널 (건설 목록) 진실치 ──
        private const float FilterTabW = 140f, FilterTabH = 36f, FilterTabGap = 6f;   // 필터 탭 140×36 r6 gap6
        private const float FilterTabY = 88f;      // @(88,166) figma → 패널 로컬 y 88
        // [125차 후속37] Figma 재실측(사용자 수정치) — 항목 432→420(-12px, 스크롤바 공간). 내부 시설 정보
        // 408→396 / 시설 소개·시설명·시설 역할 276→264 / 재료 보유량과 필요량 408→396 — 좌/우 앵커 기반
        // 배치라 항목 폭 축소가 내부 전체 폭에 -12px로 그대로 전파된다(Figma 239:683 현재값 전부 반영).
        private const float FacilityItemW = 420f, FacilityItemH = 101f, FacilityItemGap = 8f; // 항목 420×101 r6 gap8
        private const float FacilityListY = 168f;  // @(88,246) figma → 패널 로컬 y 168
        // [125차 후속37] 시설 목록 스크롤바 — Figma scrollbar-track 4×537 r2 #20262E + scrollbar-thumb
        // 4×208 r2 #58A6FF(썸 208은 스크롤 가능 상태 정지값 예시 — 구현은 실측 content/viewport 비율 동기화).
        // 트랙은 목록 프레임(432 = 항목 420 + 간격 8 + 트랙 4 — Figma '시설 목록' 432×565 실측) 우측 가장자리 정렬.
        private const float ScrollbarTrackW = 4f;       // 트랙/썸 폭 4
        private const float ScrollbarGap = 8f;          // 항목 우측 → 트랙 간격 8
        private const float ScrollbarRadius = 2f;       // 트랙/썸 r2
        private const float FacilityListViewportH = 5f * FacilityItemH + 4f * FacilityItemGap; // 목록 뷰포트 537
        private const float ItemIconSize = 36f;    // 아이콘 36×36 #20262E r6
        private const float StatusBadgeW = 76f, StatusBadgeH = 21f;   // 상태 배지 76×21 r6
        private const float SummaryBoxW = 432f, SummaryBoxH = 110f;   // 보유 재료 요약 432×110 @(88,826)
        private const float SummaryBoxY = 748f;    // figma 826 → 패널 로컬 y 748
        private const float SummaryCellW = 81.6f, SummaryCellH = 70f, SummaryCellGap = 6f; // 5칸 81.6×70 gap6 @(88,866)
        private const float SummaryCellY = 40f;    // figma 866 − 826 = 박스 로컬 y 40

        // ── 우 패널 (건설 정보) 진실치 ──
        private const float OverviewW = 920f, OverviewH = 158f, OverviewY = 90f;      // 개요 920×158 [104차 F4] 92→90 재실측
        private const float BlueprintBoxW = 234f, BlueprintBoxH = 98f;                // 설계도 박스 234×98
        private const float ConditionCardW = 298.7f, ConditionCardH = 60f, ConditionGap = 12f; // 3카드 298.7×60 gap12
        private const float ConditionY = 266f;                                     // [104차 F4] 262→266 재실측
        private const float MaterialTableW = 920f, MaterialTableH = 288f, MaterialTableY = 378f; // 재료 표 920×288 [104차 F4] 394→378
        private const float MatColHeaderH = 32f;   // 열헤더 920×32 #0F141B
        private const float MaterialRowH = 64f;    // 행 920×64
        private const float MatIconSize = 32f;     // 행 아이콘 32×32
        // 열 x — [144차 P7-CON #59] Figma 열 제목(239:849~853) 실측 재매핑: 캔버스 x 606/966/1082/1198/1344
        // = (재료/보유량/필요량/잔여/상태). 구 코드는 상수 좌표가 한 열씩 어긋나 '재료'·'보유량' 헤더가 모두
        // x=14에 겹침(#59 P0) + 값 열 전면 어긋남(#65 보유량 Δ360 / #66 필요량·#67 잔여 Δ116).
        // 표 좌상단 캔버스 x(592)를 뺀 패널 로컬값.
        private const float MatTableCanvasX = 592f;                     // 표 x = 568 + 24
        private const float ColXHeld = 966f - MatTableCanvasX;     // 보유량 (로컬 374) [#59/#65]
        private const float ColXNeed = 1082f - MatTableCanvasX;    // 필요량 (로컬 490) [#59/#66]
        private const float ColXRemain = 1198f - MatTableCanvasX;  // 건설 후 잔여 (로컬 606) [#59/#67]
        private const float ColXStatus = 1344f - MatTableCanvasX;  // 충족 상태 (로컬 752) [#59] — 구 '예비 열' 상수 폐기
        private const float ExecuteW = 920f, ExecuteH = 106f, ExecuteY = 718f;        // 건설 실행 920×106 @(592,796)
        private const float BuildButtonW = 224f, BuildButtonH = 42f;                  // '건설하기' 224×42 r6
        private const float SourceButtonW = 108f, SourceButtonH = 42f;                // '재료 획득처' 108×42
        private const float CheckLineY = 788f;     // [105차 C] 조건 확인 — Figma execute-local (16,70) → 패널 로컬 (40,788). 구 834 폐기
        private const float NoticeY = 842f;        // [105차 C] 유의사항 — Figma execute-local (0,124) → 패널 로컬 y 842. 구 856 폐기

        private const float RefreshInterval = 0.5f;   // 폴링 500ms — 기존 Updater 계약 유지(스펙 B1)

        // ═══════════════ 시설 메타 (Figma 5항목 + [152차 P0] 훈련소 — 요리/장비/음식점/병사배치/연금술/훈련소) ═══════════════

        /// <summary>시설 6종 설계도 id — Figma 시설 목록 순서(요리/장비/음식점/병사배치/연금술) + [152차 P0] 훈련소(말미). [90차 A2]/[125차] 실측 id.</summary>
        public static readonly string[] FacilityIds =
        {
            "bp_cooking", "bp_forge", "bp_restaurant", "bp_barracks", "bp_alchemy", "bp_traininghall",   // [152차 P0] 훈련소 추가(5종→6종)
        };

        private enum FacilityTab { All, Buildable, ShortMaterial, Others }

        /// <summary>시설 역할 문구 — Figma 항목 부제목 11/400 (#8B949E).</summary>
        private static string FacilityRoleLabel(string blueprintId) => blueprintId switch
        {
            "bp_cooking" => "생산 · 식사와 회복 음식 제작",
            "bp_forge" => "생산 · 무기와 방어구 제작",
            "bp_restaurant" => "운영 · 음식 판매 및 수익 창출",
            "bp_barracks" => "방어 · 병력 주둔과 거점 수비",
            "bp_alchemy" => "생산 · 물약과 강화 재료 제작",
            "bp_traininghall" => "훈련 · 병사 훈련과 전투 훈련",   // [152차 P0] 훈련소
            _ => "전초기지 시설",
        };

        /// <summary>효과(sparkles) 문구 — '… 제작 해금' 형식(Figma '기본 무기 · 가죽 방어구 제작 해금' 형식 준거).</summary>
        private static string FacilityUnlockText(string blueprintId) => blueprintId switch
        {
            "bp_cooking" => "기본 요리 · 회복 음식 제작 해금",
            "bp_forge" => "기본 무기 · 가죽 방어구 제작 해금",
            "bp_restaurant" => "음식 판매 · 마을 수익 창출 해금",
            "bp_barracks" => "병력 주둔 · 거점 수비 지원 해금",
            "bp_alchemy" => "물약 · 강화 재료 제작 해금",
            "bp_traininghall" => "병사 훈련 · 전투 능력 강화 해금",   // [152차 P0] 훈련소
            _ => "시설 기능 해금",
        };

        /// <summary>보유 재료 요약 5칸 — (재료 id, 폴백 라벨). id는 ConstructionManager 상수 단일소스,
        /// 표기는 실측 displayName(GetItemById) 우선. [144차 P7-CON #30] 표시 문자열 '마력석'(239:767/955
        /// Figma 진실치) 복원 — 데이터 키(MaterialManaItemId=crystal_shard) 무손상. 단 실측 displayName이
        /// '수정석'(PlayerInventory.CrystalShard)이면 우선 표기되고, 획득처 토스트 '수정석'은
        /// ConstructionManagementFigmaTests 353행 문자열 고정으로 유지한다.</summary>
        private static readonly (string itemId, string fallback)[] SummaryMaterials =
        {
            (ConstructionManager.MaterialWoodItemId, "나무"),
            (ConstructionManager.MaterialStoneItemId, "돌"),
            (ConstructionManager.MaterialIronItemId, "철"),
            (ConstructionManager.MaterialLeatherItemId, "가죽"),
            (ConstructionManager.MaterialManaItemId, "마력석"),   // [#30] 표시 폴백 '마력석' — id 키 무손상
        };

        // ═══════════════ 상태 ═══════════════

        private static ConstructionWindowUTK _instance;
        public static ConstructionWindowUTK Instance => _instance;

        private FacilityTab _activeTab = FacilityTab.All;
        private string _selectedBlueprintId;   // 선택 설계도(시설/기타 공용) — null = 미선택
        private float _refreshTimer;           // 기존 Updater 폴링 계약
        private bool _structureSubscribed;     // 정적 이벤트 구독 플래그(중복 방지 — 기존 계약)

        // ── UI 핸들(갱신용) ──
        private Label _headerDayLabel, _headerClockLabel;
        private Button[] _tabButtons;
        private Label[] _tabLabels;
        private ScrollView _itemList;                 // 좌 시설/기타 항목 목록
        private VisualElement _itemScrollbarThumb;    // [125차 후속37] 시설 목록 스크롤바 썸(실측 동기화)
        private Label[] _summaryCountLabels;          // 보유 재료 요약 5칸 보유량 라벨
        private Label _selNameLabel, _selLvLabel, _selDescLabel, _selUnlockLabel, _selOpsLabel;
        private VisualElement _selBadgeHost;          // 개요 상태 배지(재빌드 컨테이너)
        private Label _bpBoxLabel, _bpFootprintLabel; // 설계도 박스 'FACILITY / LV.1' + 부지
        private Label[] _conditionValueLabels;        // 건설 시간/부지/작업 인원
        private Label _matSatisfyLabel;               // [144차 P7-CON #56] '필요 재료' 우 충족 수 "N / M"(실데이터)
        private ScrollView _materialRows;             // 필요 재료 표 행
        private VisualElement _executeBox;            // 건설 실행 박스(상태별 재빌드)
        private Label _checkLineLabel;                // 조건 확인
        private Label _checkWarnLabel;                // [144차 P7-CON #79] 미충족 조건 병기 라인 '! 재료 N종 부족'

        // ═══════════════ 자가부트 / 개방 API ═══════════════

        /// <summary>[90차 B2] 자가부트 — 기존 O8 Updater 패턴 유지 + FacilityOpenRequested 구독(구독 전 해제).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // [90차 B2] Systems→UI 순환참조 회피 정적 이벤트 — 구독 전 해제(도메인 리로드 중복 방지,
            // 88차 RestaurantWindowUTK 선례). 무구독 시 Systems는 로그만 남긴다.
            // [실측] 이벤트 선언부 = FacilityStationInteractable(FacilityPropBuilder.cs 331행) — E키 상호작용 클래스 소유.
            FacilityStationInteractable.FacilityOpenRequested -= OnFacilityOpenRequested;
            FacilityStationInteractable.FacilityOpenRequested += OnFacilityOpenRequested;

            // [104차 F1] 홀로그램 배치 토스트 브리지 — Systems(ConstructionHologram) → UI 라우팅(구독 전 해제).
            ConstructionHologram.ToastRequested -= OnHologramToastRequested;
            ConstructionHologram.ToastRequested += OnHologramToastRequested;

            if (_instance != null) return;
            _instance = new ConstructionWindowUTK();
            var go = new GameObject("ConstructionWindowUTK");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Updater>().window = _instance;
            // [99차 F-E] E-1 UIRoot 직결 부착 — NPCDialoguePanelUTK.EnsureAttached 패턴 준용(부트 시점 직결 부착).
            // stale detach 가드(91차 F2, Open/Show 동일 가드) 적용 — UIRoot 미준비면 개방 경로(Open/Show)가 부착.
            var uiRoot = UIToolkitBootstrap.UIRoot;
            if (uiRoot != null && (_instance.parent == null || _instance.parent.panel == null))
                uiRoot.Add(_instance);
            Debug.Log("[ConstructionUTK] 건설 관리 창 초기화 완료 — U키로 토글");
        }

        /// <summary>[104차 F1] 홀로그램 토스트 브리지 — Systems 메시지를 UTKToastService로 라우팅.</summary>
        private static void OnHologramToastRequested(string result)
        {
            UTKToastService.Show(result);
        }

        /// <summary>[90차 B2] 시설 E키 개방 라우팅 — effectId → 창 Open()(전부 실측 시그니처).</summary>
        private static void OnFacilityOpenRequested(string effectId)
        {
            switch (effectId)
            {
                case "cooking_facility": CookingWindowUTK.Open(); break;
                case "forge": CraftingWindowUTK.Open(); break;
                case "barracks": AttackFormationUTK.Open(); break;
                case "alchemy_facility": AlchemyBenchUTK.Open(); break;
                case "training_hall": TrainingHallWindowUTK.Open(); break;   // [125차] 훈련소 — 훈련 UI 개방
                default:
                    Debug.Log($"[ConstructionUTK] 미매핑 시설 effectId: {effectId}");
                    break;
            }
        }

        /// <summary>팩토리 — 멱등(기존 계약 유지).</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new ConstructionWindowUTK();
            var root = UIToolkitBootstrap.UIRoot;
            // [91차 F2] 재부착 경화 — stale detach(parent 잔존 + panel 끊김)에서도 재부착(Open/Show 동일 가드).
            if (root != null && (_instance.parent == null || _instance.parent.panel == null))
                root.Add(_instance);
        }

        /// <summary>건설 관리 창 열기.</summary>
        public static void Open()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null)
            {
                Debug.LogWarning("[ConstructionUTK] UIRoot 미준비 — 건설 창 개방 불가");
                return;
            }
            Ensure();
            // [91차 F2] 재부착 경화 — stale detach 대비(Ensure/Show 동일 가드).
            if (_instance.parent == null || _instance.parent.panel == null) root.Add(_instance);
            _instance.Show();
        }

        /// <summary>
        /// [90차 B2] 창 개방 + 설계도 자동 선택 — AttackFormationUTK '음식점 배치' 버튼용 진입점.
        /// 시설 6종이면 '전체' 탭, 기타 8종이면 '기타' 탭에서 선택 상태로 우측 패널을 갱신한다.
        /// </summary>
        public static void SelectBlueprint(string blueprintId)
        {
            Open();
            if (_instance == null || string.IsNullOrEmpty(blueprintId)) return;
            _instance._activeTab = IsFacilityBlueprint(blueprintId) ? FacilityTab.All : FacilityTab.Others;
            _instance._selectedBlueprintId = blueprintId;
            _instance.Refresh();
            Debug.Log($"[ConstructionUTK] 설계도 자동 선택: {blueprintId}");
        }

        /// <summary>토글(닫혀있으면 열고, 열려있으면 닫음) — 기존 계약 유지.</summary>
        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Close(); return; }
            Open();
        }

        // ═══════════════ 생성 — 창 골격(B1) ═══════════════

        private ConstructionWindowUTK() : base("건설 관리", new Vector2(WinW, WinH), UTKWindowChrome.Frameless)
        {
            // 창 루트 — [125차 후속37] Figma 239:683 현재값 재실측 원복(후속12 투명 루트 역전): 루트 프레임
            // fill SOLID #0B0E14@1.00 불투명(Figma API 실측). [112차 F-A] 섀도우 대책 DisableWindowShadow()는
            // Figma 프레임 drop shadow 부재로 유지. 레이아웃/좌표는 Figma 계약 그대로 유지.
            style.backgroundColor = new StyleColor(FigmaRootBg);
            style.backgroundImage = new StyleBackground(StyleKeyword.None);
            style.overflow = Overflow.Hidden;
            DisableWindowShadow();   // [112차 F-A] Figma 프레임 drop shadow 없음 — 창 레벨 12px rim 음영 제거

            _content.style.width = WinW;    // 내부 raw 유지 — ApplyCanvasFit이 scale=k 적용
            _content.style.height = WinH;
            _content.style.position = Position.Relative;
            _content.style.paddingLeft = 0f;
            _content.style.paddingRight = 0f;
            _content.style.paddingTop = 0f;
            _content.style.paddingBottom = 0f;
            _content.style.flexGrow = 0f;
            _content.style.flexShrink = 0f;

            // [125차 후속13] 헤더바(전초기지 경로 그리드) 제거 — 사용자 지시(아레나창과 동일 처리)
            BuildLeftPanel();
            BuildRightPanel();
            BuildHelpBar();

            // [90차 B1] 단축키 — UIRoot 경유 KeyDownEvent(IsOpen+TopWindow 게이트, 88차 선례).
            // Enter = 선택 시설 건설 실행 / Esc = 창 닫기(U키 토글은 기존 Updater 계약 유지).
            RegisterCallback<AttachToPanelEvent>(_ => UIToolkitBootstrap.UIRoot?.RegisterCallback<KeyDownEvent>(OnRootKeyDown));
            RegisterCallback<DetachFromPanelEvent>(_ => UIToolkitBootstrap.UIRoot?.UnregisterCallback<KeyDownEvent>(OnRootKeyDown));

            ApplyUIToolkitFont(this);
        }

        // ═══════════════ UTKWindowBase 라이프사이클 ═══════════════

        public override void Show()
        {
            var root = UIToolkitBootstrap.UIRoot;
            // [91차 F2] 재부착 경화 — stale detach(parent 잔존 + panel 끊김)에서도 재부착(Ensure/Open 동일 가드).
            if (root != null && (parent == null || parent.panel == null))
                root.Add(this);
            base.Show();
            SubscribeStructureCompleted();
            // [93차 F2] 이번 개방 판정용 리셋 — 직전 세션의 적용값 잔존 방지(지연 재시도가 성공하면 갱신됨).
            _lastFitApplied = false;
            _lastFitK = float.NaN;
            // [99차 F-E] E-4 폴백 판정 상태 리셋 — 이번 개방 세션 기준 재판정(직전 개방 잔존 방지).
            _geometryReinjected = false;
            _contentZeroCycles = 0;
            ApplyCanvasFit();   // 개방 즉시 1회 — 레이아웃 지연 재시도는 OnWindowOpen 스케줄
            LogOpenScreenState();   // [93차 F2] 개방 직후 화면 상태 진단(재판정 장치) — k/배치/onScreen 즉격
            LogUidocumentInventory();   // [97차 F4] UIDocument 전수 진단 — 이중 부트 확정용.
            // [99차 F-E] E-3 — 97차 F4 마젠타 강제 표시(임시 진단)는 원인 확정 완료로 제거(창 루트 렌더 정상,
            // 콘텐츠 미부착 확정). 개방 시 콘텐츠 실측은 ApplyCanvasFit → MeasureGeometryAndDefend 경로가 담당.
            // [91차 F2] 부착 상태 진단 — 재개방 미표시(영상 15.2~16.4s 실측) 판별용.
            Debug.Log($"[ConstructionUTK] 건설 관리 창 열림 (키: U, attached={parent != null && parent.panel != null})");
        }

        public override void Hide()
        {
            RestoreContentToWindowIfFallback();   // [99차 F-E] E-4 폴백 해제 — 콘텐츠 루트 창 복귀(화면 잔존 방지).
            base.Hide();
            Debug.Log("[ConstructionUTK] 건설 관리 창 닫힘");
        }

        protected override void OnWindowOpen()
        {
            Refresh();
            // [90차 B1] ApplyCanvasFit — 개방 즉시 + 레이아웃 지연 대응 재시도(16/120/400ms — 수면 패널 선례)
            schedule.Execute(ApplyCanvasFit).StartingIn(16);
            schedule.Execute(ApplyCanvasFit).StartingIn(120);
            schedule.Execute(ApplyCanvasFit).StartingIn(400);
        }

        /// <summary>
        /// [84차] 등배수 창박스 적용 — "창박스=figma×k, 내부 raw·scale=k" 계약(SleepPanelUTK/88차 동일 패턴).
        /// k=min(rootW/1920, rootH/1080). 창=중앙 배치, 내부 콘텐츠는 raw 유지+transform.scale=k(글자 왜곡 없음).
        /// </summary>
        // [93차 F2] 개방 진단 상태 — ApplyCanvasFit 최종 적용 결과(Show 개방 진단 로그용).
        //   레이아웃 미확정 등으로 미적용 시 false/NaN 유지 → 진단 로그가 "미적용"을 즉격한다.
        private bool _lastFitApplied;
        private float _lastFitK = float.NaN;
        private float _lastFitLeft, _lastFitTop;

        // [99차 F-E] E-4 폴백 판정 상태 — 이번 개방 세션 기준(Show에서 리셋).
        private bool _geometryReinjected;      // 이번 개방에서 96차 F3 기하 재주입 실행됨(폴백 게이트)
        private int _contentZeroCycles;        // 콘텐츠 worldBound 0x0 연속 실측 사이클 수
        private bool _contentFallbackActive;   // 콘텐츠 루트 UIRoot 직결 폴백 활성(닫힘 시 창 복귀)

        private void ApplyCanvasFit()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;
            float rootW = root.resolvedStyle.width;
            float rootH = root.resolvedStyle.height;
            if (rootW <= 1f || rootH <= 1f) return;   // 레이아웃 미확정 — 다음 폴링 시 재적용
            float k = Mathf.Min(rootW / FigmaCanvasLayout.CanvasWidth, rootH / FigmaCanvasLayout.CanvasHeight);
            if (k <= 0f) return;

            float left = Mathf.Max(0f, (rootW - WinW * k) * 0.5f);    // 중앙 배치
            float top = Mathf.Max(0f, (rootH - WinH * k) * 0.5f);

            // [93차 F2] 폴백 방어(무해 우선) — 스케일 0/오프스크린 배치 방어.
            //   ① 계산 k≤0.2 = 사실상 소멸 스케일(창이 점으로 수렴 — 레이아웃 미확정/극단 치수 유력)
            //   ② 배치 결과가 패널 밖(전체가 화면 밖) — 유한 중앙 배치에서는 발생하지 않지만 NaN/극단치 방어.
            //   → 폴백: 중앙 배치 + k=panel.min/WinW 상한(Figma 1600 기준 — 창 폭이 패널 짧은 변 이하,
            //     높이는 1040/1600×panel.min로 항상 수납) 재적용. 리페인트 정지 원인은 유저 환경 확인 후 별도.
            float panelMin = Mathf.Min(rootW, rootH);
            bool fullyOutside = left >= rootW || top >= rootH
                || left + WinW * k <= 0f || top + WinH * k <= 0f;
            if (k <= 0.2f || fullyOutside)
            {
                k = panelMin / WinW;   // [93차 F2] 상한 폴백 스케일(panel.min/1600)
                left = Mathf.Max(0f, (rootW - WinW * k) * 0.5f);
                top = Mathf.Max(0f, (rootH - WinH * k) * 0.5f);
                Debug.Log($"[ConstructionUTK] ApplyCanvasFit 폴백 — k≤0.2/오프스크린 감지 → k={k:F3} 중앙 재배치");
            }

            style.width = WinW * k;
            style.height = WinH * k;
            style.left = left;
            style.top = top;
            _content.style.transformOrigin = new TransformOrigin(0f, 0f, 0f);
            _content.style.scale = new StyleScale(new Scale(new Vector2(k, k)));

            _lastFitApplied = true;   // [93차 F2] 진단 상태 기록(개방 로그가 실측값을 인용)
            _lastFitK = k;
            _lastFitLeft = left;
            _lastFitTop = top;

            MeasureGeometryAndDefend(k, left, top);   // [96차 F3] worldBound 재실측+기하 0 방어 — 개방 즉시+16/120/400ms 스케줄 전 호출 경로.
        }

        /// <summary>
        /// [96차 F3] worldBound 재실측+기하 0 방어 — ApplyCanvasFit 전 호출 경로(개방 즉시+16/120/400ms 스케줄)마다
        /// 자기 적용 style.width/height(레이아웃 확정치=figma×k)+resolvedStyle+worldBound 크기+직계 부모
        /// worldBound를 즉격한다(스펙 결함 3 수리 1 — worldBound 0x0이 언제/왜 생기는지 즉격). worldBound가
        /// 0 또는 적용치 대비 비정상 소이면 창 루트에 position=absolute+width/height 재주입(스펙 결함 3 수리 2 —
        /// "레이아웃은 있고 기하 0" 탈출, 93차 폴백 k≤0.2/오프스크린→중앙과 조합). DOM 체인 진단/배경 폴백(94차) 유지.
        /// </summary>
        private void MeasureGeometryAndDefend(float k, float left, float top)
        {
            if (parent == null)
            {
                Debug.Log("[ConstructionUTK] [96차 F3] worldBound 재실측 — 미부착(측정 불가 — 부착 후 스케줄 16/120/400ms 재실측)");
                return;
            }
            float appliedW = style.width.value.value;   // 적용 확정치 = figma×k
            float appliedH = style.height.value.value;
            var rs = resolvedStyle;                     // 레이아웃 패스 전 구값(지연 실측 대비 병기)
            Rect wb = worldBound;
            Rect pwb = parent.worldBound;
            bool zeroGeom = wb.width <= 0.5f || wb.height <= 0.5f;
            bool tinyGeom = !zeroGeom && (wb.width < appliedW * 0.5f || wb.height < appliedH * 0.5f);
            Debug.Log($"[ConstructionUTK] [96차 F3] worldBound 재실측 — style=({appliedW:F0}x{appliedH:F0}) resolved=({rs.width:F0}x{rs.height:F0}) world=({wb.x:F0},{wb.y:F0},{wb.width:F0}x{wb.height:F0}) parent=({pwb.x:F0},{pwb.y:F0},{pwb.width:F0}x{pwb.height:F0}) k={k:F3} 판정={(zeroGeom ? "기하0" : tinyGeom ? "비정상소" : "정상")}");
            if (pwb.width <= 0.5f || pwb.height <= 0.5f)
                Debug.LogWarning("[ConstructionUTK] [96차 F3] 부모 worldBound 0 — 부모(루트 컨테이너) 레이아웃이 자식 크기 미반영(근본 원인 후보 — 절대배치/측정 누락)");
            // [99차 F-E] E-3 콘텐츠 실측 — content worldBound + 직계 자식 display 요약(개방+16/120/400ms 사이클 공통).
            Rect cwb = _content.worldBound;
            bool contentZero = cwb.width <= 0.5f || cwb.height <= 0.5f;
            _contentZeroCycles = contentZero ? _contentZeroCycles + 1 : 0;
            Debug.Log($"[99차 F-E] 콘텐츠 렌더 — worldBound=({cwb.x:F0},{cwb.y:F0},{cwb.width:F0}x{cwb.height:F0}) children={_content.childCount} display={BuildContentDisplaySummary()} k={k:F3} 판정={(contentZero ? "콘텐츠0" : "정상")}");
            LogContentFillRate(k);   // [100차 F-4] 콘텐츠-창박스 채움률 실측(사이클마다 — 영상 눈대실 % 대신 수치 판정)
            if (!zeroGeom && !tinyGeom && !contentZero)
                return;
            // 방어 — 창 루트 기하 재주입(position=absolute + 레이아웃 확정치 width/height/left/top 재적용).
            style.position = Position.Absolute;
            style.left = left;
            style.top = top;
            style.width = appliedW;
            style.height = appliedH;
            _geometryReinjected = true;   // [99차 F-E] 재주입 실행 기록 — E-4 콘텐츠 폴백 게이트.
            Debug.LogWarning($"[ConstructionUTK] [96차 F3] 기하 재주입 — worldBound {wb.width:F0}x{wb.height:F0} 비정상 → position=absolute+({appliedW:F0}x{appliedH:F0}) @({left:F0},{top:F0}) 재적용 (콘텐츠0={contentZero})");
            // [99차 F-E] E-4 폴백 — 재주입 후에도 콘텐츠 0x0이면 콘텐츠 루트를 UIRoot에 직결 재부착한다.
            // worldBound는 레이아웃 패스 후 갱신되므로 동일 틱 재측정은 무의미 — 후속 실측 사이클(16/120/400ms)
            // 에서 콘텐츠 0x0이 연속 2회 실측되면 발동(재주입 1회 경과 조건).
            if (contentZero && _contentZeroCycles >= 2)
                ReattachContentToUiRootFallback(k, left, top);
        }

        /// <summary>
        /// [100차 F-4] 콘텐츠-창박스 정렬 검증 — 4블록(HeaderBar/LeftPanel/RightPanel/HelpBar) worldBound를
        /// 창박스 대비 실측한다. 창박스 = _content worldBound(90차 계약 — 콘텐츠가 창박스를 단독 차지, E-4
        /// 폴백 UIRoot 직결 경로에서도 동일). 채움률 = 블록 worldBound 면적 ÷ 창박스 면적×100. 유니온(4블록
        /// bbox) 선형 채움(가로x세로 %)은 영상 눈대실(창 85×80% vs 콘텐츠 75×75%)과 직접 대비되는 판정값.
        /// 설계 rect(figma 239:683 재실측 좌표×k + 창박스 원점) 대비 1.5px 초과 이탈 블록은 판정에 나열한다.
        /// MeasureGeometryAndDefend 사이클(개방 즉시+16/120/400ms+폴링)마다 기록 — 등배수 계약 무변경.
        /// </summary>
        private void LogContentFillRate(float k)
        {
            Rect box = _content.worldBound;
            if (box.width <= 0.5f || box.height <= 0.5f) return;   // 창박스 미확정 — 다음 사이클 재실측
            float boxArea = box.width * box.height;
            float[] bx = { HeaderX, LeftX, RightX, HelpX };        // figma 재실측 로컬 좌표(프레임 원점 차감)
            float[] by = { HeaderY, LeftY, RightY, HelpY };
            float[] bw = { HeaderW, LeftW, RightW, HelpW };
            float[] bh = { HeaderH, LeftH, RightH, HelpH };
            string[] blockNames = { "HeaderBar", "LeftPanel", "RightPanel", "HelpBar" };
            var fills = new string[4];
            var mismatched = new System.Collections.Generic.List<string>();
            float ux0 = float.PositiveInfinity, uy0 = float.PositiveInfinity;
            float ux1 = float.NegativeInfinity, uy1 = float.NegativeInfinity;
            const float TolPx = 1.5f;   // 정렬 판정 허용차(스크린 px — 스케일 k 환산 후 소수 반올림 여유)
            for (int i = 0; i < 4; i++)
            {
                var el = _content.Q(blockNames[i]);
                if (el == null)
                {
                    fills[i] = $"{blockNames[i]}=미빌드";
                    mismatched.Add(blockNames[i]);
                    continue;
                }
                Rect b = el.worldBound;
                fills[i] = $"{blockNames[i]}={(b.width * b.height) / boxArea * 100f:F1}%";
                if (b.width <= 0.5f || b.height <= 0.5f)
                {
                    mismatched.Add(blockNames[i]);   // 기하 0 — 정렬 판정 불가
                    continue;
                }
                // 설계 rect 대비 정렬 판정 — 창박스 원점 + figma 좌표×k(등배수 계약 환산)
                Rect designed = new Rect(box.x + bx[i] * k, box.y + by[i] * k, bw[i] * k, bh[i] * k);
                if (Mathf.Abs(b.x - designed.x) > TolPx || Mathf.Abs(b.y - designed.y) > TolPx
                    || Mathf.Abs(b.width - designed.width) > TolPx
                    || Mathf.Abs(b.height - designed.height) > TolPx)
                    mismatched.Add(blockNames[i]);
                ux0 = Mathf.Min(ux0, b.x); uy0 = Mathf.Min(uy0, b.y);
                ux1 = Mathf.Max(ux1, b.x + b.width); uy1 = Mathf.Max(uy1, b.y + b.height);
            }
            bool anyMeasured = ux1 > ux0;
            string union = anyMeasured
                ? $"유니온 {(ux1 - ux0) / box.width * 100f:F0}%x{(uy1 - uy0) / box.height * 100f:F0}%"
                : "유니온 측정불가";
            string verdict = mismatched.Count == 0 ? "정렬" : $"이탈({string.Join(",", mismatched)})";
            Debug.Log($"[100차 F-4] 채움 — {fills[0]} {fills[1]} {fills[2]} {fills[3]} (worldBound 면적/창박스 {box.width:F0}x{box.height:F0} · {union} · 판정={verdict} · k={k:F3})");
        }

        /// <summary>
        /// [99차 F-E] E-4 콘텐츠 루트 UIRoot 직결 재부착 폴백 — 96차 F3 기하 재주입 후에도 콘텐츠 worldBound가
        /// 0x0(연속 2사이클)이면 콘텐츠 트리를 창 루트 대신 UIRoot에 직결 부착해 렌더 경로를 단축한다
        /// (NPCDialoguePanelUTK 검증 구성 = UIRoot 직결). 창박스=figma×k·내부 raw·scale=k 계약 유지(90차 E-2) —
        /// 콘텐츠가 창과 동일 창박스를 단독으로 차지한다. 멱등(활성 중 재진입 무연산).
        /// </summary>
        private void ReattachContentToUiRootFallback(float k, float left, float top)
        {
            if (_contentFallbackActive) return;
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;
            if (_content.parent == this)
                _content.RemoveFromHierarchy();   // 창 루트에서 탈착 — UIRoot 직결로 전환
            if (_content.parent == null)
                root.Add(_content);
            _content.style.position = Position.Absolute;
            _content.style.left = left;
            _content.style.top = top;
            _content.style.width = WinW;    // 내부 raw 유지(E-2)
            _content.style.height = WinH;
            _content.style.transformOrigin = new TransformOrigin(0f, 0f, 0f);
            _content.style.scale = new StyleScale(new Scale(new Vector2(k, k)));
            _contentFallbackActive = true;
            Debug.LogWarning("[99차 F-E] 콘텐츠 루트 UIRoot 직결 재부착 폴백 — 재주입 후에도 콘텐츠 0x0");
        }

        /// <summary>[99차 F-E] 폴백 해제 — 닫힘 시 콘텐츠 루트를 창으로 복귀(Hide 화면 잔존 방지 — 멱등).</summary>
        private void RestoreContentToWindowIfFallback()
        {
            if (!_contentFallbackActive) return;
            if (_content.parent != null)
                _content.RemoveFromHierarchy();
            Add(_content);
            _contentFallbackActive = false;
            Debug.Log("[99차 F-E] 콘텐츠 루트 창 복귀 — 폴백 해제(닫힘)");
        }

        /// <summary>[99차 F-E] E-3 직계 자식 display 요약 — "이름:display" 쉼표 나열(창 내용 구조 즉격).</summary>
        private string BuildContentDisplaySummary()
        {
            var sb = new System.Text.StringBuilder();
            int n = 0;
            foreach (var child in _content.Children())
            {
                if (n++ > 0) sb.Append(',');
                sb.Append(child.name).Append(':').Append(child.resolvedStyle.display);
            }
            return sb.Length == 0 ? "(없음)" : sb.ToString();
        }

        /// <summary>[93차 F2] 개방 직후 화면 상태 진단(재판정 장치) — Show당 1회. panel 실측(resolvedStyle),
        /// ApplyCanvasFit이 적용한 k, 배치 rect(스타일 산술값 — 레이아웃 패스 전에도 확정)와 worldBound 실측,
        /// 온스크린 판정을 즉격한다(U키 동결/미표시 재발 시 스케일 0·오프스크린 여부 1로그 분리).</summary>
        private void LogOpenScreenState()
        {
            var root = UIToolkitBootstrap.UIRoot;
            float pw = root != null ? root.resolvedStyle.width : 0f;
            float ph = root != null ? root.resolvedStyle.height : 0f;
            if (!_lastFitApplied)
            {
                Debug.Log($"[ConstructionUTK] 개방 직후 화면 상태 — panel=({pw:F0}x{ph:F0}) k=미적용(레이아웃 미확정) windowRect=(-,-) onScreen=False");
                LogDomAncestorChain();   // [94차 F2] 미적용 경우에도 조상 체인 실측(가림/죽은 패널 즉격)
                return;
            }
            float w = WinW * _lastFitK;
            float h = WinH * _lastFitK;
            bool onScreen = _lastFitLeft < pw && _lastFitTop < ph
                && _lastFitLeft + w > 0f && _lastFitTop + h > 0f;   // 패널과 한 픽셀이라도 겹침 = 온스크린
            Rect wb = worldBound;   // [93차 F2] worldBound 실측(레이아웃 패스 전 구값일 수 있음 — 산술값과 병기)
            Debug.Log($"[ConstructionUTK] 개방 직후 화면 상태 — panel=({pw:F0}x{ph:F0}) k={_lastFitK:F3} windowRect=({_lastFitLeft:F0},{_lastFitTop:F0},{w:F0}x{h:F0}) world=({wb.x:F0},{wb.y:F0},{wb.width:F0}x{wb.height:F0}) onScreen={onScreen}");
            LogDomAncestorChain();   // [94차 F2] DOM 조상 체인 실측 — 가림/죽은 패널 즉격
        }

        /// <summary>
        /// [94차 F2] DOM 조상 체인 실측 — 개방 직후 self→루트까지 각 요소의 부착 위치(부모 내 자식 인덱스/
        /// 형제 수)와 display·visibility·opacity·pickingMode를 즉격한다. 근거: 열림 로그 3회 사이클에서
        /// 레이아웃 완전 정상(panel 1440×909·onScreen=True)인데 렌더 부재 — 가림/죽은 패널/시각 리소스
        /// 누락을 1로그로 격리(스펙 결함 2 수리 2 — DOM 진단).
        /// </summary>
        private void LogDomAncestorChain()
        {
            if (panel == null)
            {
                Debug.Log("[ConstructionUTK] DOM 조상 체인 — panel=null(미부착) — 렌더 불가 상태 즉격");
                return;
            }
            VisualElement el = this;
            int depth = 0;
            while (el != null)
            {
                int idx = el.parent != null ? el.parent.IndexOf(el) : 0;   // 부모 내 자식 인덱스
                int sib = el.parent != null ? el.parent.childCount : 1;    // 형제 수
                var rs = el.resolvedStyle;
                Debug.Log($"[ConstructionUTK] DOM 조상 체인[{depth}] name={el.name} idx={idx}/{sib} display={rs.display} visibility={rs.visibility} opacity={rs.opacity:F2} picking={el.pickingMode}");
                el = el.parent;
                depth++;
            }
        }


        // ═══════════════ [97차 F4] UIDocument 전수 진단 — 이중 부트 확정용 ═══════════════

        /// <summary>
        /// [97차 F4] UIDocument 전수 진단 — 개방 시 씬의 모든 UIDocument(이름/패널 크기/sortingOrder/enabled)를
        /// 로그하고, 창이 붙은 루트가 어느 UIDocument/PanelSettings 소속인지 대조한다. 근거: 렌더되는 UI는 전부
        /// UIRoot 직결 패널, 안 뜨는 것은 유일하게 UTKWindowBase 창 — 이중 부트(UIDocument 2개/PanelSettings
        /// 분리) 여부를 1로그로 확정(스펙 결함 4 바이섹트 2). FindObjectsOfType 전수(스펙 지정 — non-alloc 아님).
        /// </summary>
        private void LogUidocumentInventory()
        {
            var docs = Object.FindObjectsOfType<UIDocument>();   // [97차 F4] 전수(스펙 지정 — non-alloc 아님)
            Debug.Log($"[ConstructionUTK] [97차 F4] UIDocument 전수 진단 — count={docs.Length} 창부착={(parent != null && parent.panel != null)}");
            VisualElement winRoot = GetTopAncestor(this);
            for (int i = 0; i < docs.Length; i++)
            {
                var doc = docs[i];
                if (doc == null)
                {
                    Debug.Log($"[ConstructionUTK] [97차 F4] UIDocument[{i}] = null(파괴 잔존)");
                    continue;
                }
                var ps = doc.panelSettings;
                var docRoot = doc.rootVisualElement;
                float rw = docRoot != null ? docRoot.layout.width : 0f;
                float rh = docRoot != null ? docRoot.layout.height : 0f;
                bool owns = docRoot != null && ReferenceEquals(docRoot, winRoot);
                // [97차 F4] 보간 구멍 내 따옴표 리터럴 금지(C# 9 계약) — 조건 문자열은 로컬로 산출 후 보간.
                string psName = ps != null ? ps.name : "null";
                string psSorting = ps != null ? ps.sortingOrder.ToString() : "-";
                string psRefRes = ps != null ? $"{ps.referenceResolution.x}x{ps.referenceResolution.y}" : "-";
                string ownsMark = owns ? "YES(본 창 패널)" : "no";
                Debug.Log($"[ConstructionUTK] [97차 F4] UIDocument[{i}] go={doc.gameObject.name} enabled={doc.enabled} goActive={doc.gameObject.activeInHierarchy} panelSettings={psName} sortingOrder={psSorting} refRes={psRefRes} panelSize=({rw:F0}x{rh:F0}) 창루트소속={ownsMark}");
            }
            if (winRoot == null)
                Debug.Log("[ConstructionUTK] [97차 F4] 창 루트 대조 — 미부착(소속 판별 불가 — 부착 경로 재개방)");
            else
                Debug.Log($"[ConstructionUTK] [97차 F4] 창 루트 대조 — 최상위 name={winRoot.name} (위 목록 중 창루트소속=YES가 본 창 소속 UIDocument/PanelSettings)");
        }

        /// <summary>[97차 F4] 요소의 최상위 조상(UIDocument rootVisualElement) 탐색 — 소속 대조용.</summary>
        private static VisualElement GetTopAncestor(VisualElement ve)
        {
            VisualElement top = ve;
            while (top.parent != null)
                top = top.parent;
            return top;
        }

        // ═══════════════ 단축키 (KeyDownEvent — 창 표시 중만) ═══════════════

        private void OnRootKeyDown(KeyDownEvent evt)
        {
            if (!IsOpen) return;
            if (UTKWindowManager.TopWindow != this) return;   // 최상단 창이 아닐 때 무간섭
            if (evt.keyCode == KeyCode.Escape)
            {
                evt.StopPropagation();
                Hide();                                        // Esc = 창 닫기(Updater 경로와 중복 무해 — 멱등)
            }
            else if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                evt.StopPropagation();
                OnBuildButtonClicked();                        // Enter = 선택 시설 건설 실행
            }
        }

        // ═══════════════ 구독 관리 (Subscribe/Unsubscribe 쌍 — 기존 계약) ═══════════════

        private void SubscribeStructureCompleted()
        {
            if (_structureSubscribed) return;
            ConstructionManager.StructureCompleted += OnStructureCompleted;
            _structureSubscribed = true;
        }

        private void UnsubscribeStructureCompleted()
        {
            if (!_structureSubscribed) return;
            ConstructionManager.StructureCompleted -= OnStructureCompleted;
            _structureSubscribed = false;
        }

        private void OnStructureCompleted(ConstructionManager.StructureEntry entry)
        {
            if (IsOpen) Refresh();
            UTKToastService.Show($"✅ 건설 완료: {DescribeBlueprint(entry.blueprintId)}");
        }

        // ═══════════════ 새로고침 (public — 이벤트/폴링/버튼 공용 경로) ═══════════════

        public void Refresh()
        {
            RefreshHeaderTime();
            RefreshFilterTabs();
            RefreshBlueprintList();
            RefreshMaterialSummary();
            RefreshRightPanel();
        }

        /// <summary>[90차 B1] 명칭 별칭 — 스펙 B1 'RefreshAll' 계약(동일 경로).</summary>
        public void RefreshAll() => Refresh();

        /// <summary>헤더 우측 — '{N}일차 · 낮/밤' + 'HH:MM' (TimeManager 실측 — 88차 선례).</summary>
        private void RefreshHeaderTime()
        {
            // [125차 후속37] 널가드 — 후속13으로 BuildHeaderBar 미호출(헤더 소실 유지 계약) 상태에서
            // _headerDayLabel/_headerClockLabel 널 역참조 NRE가 Refresh 전체를 중단시켜 좌 목록
            // (RefreshBlueprintList)까지 도달하지 못했던 원인 수리. 헤더는 소실 상태 유지가 정답 —
            // BuildHeaderBar 호출 복원 금지. 헤더 복원 시(미래 후속) 가드는 자동으로 무해화된다.
            if (_headerDayLabel == null || _headerClockLabel == null) return;
            TimeManager tm = TimeManager.Instance;
            _headerDayLabel.text = tm != null ? $"{tm.CurrentDay}일차 · {(tm.IsDay ? "낮" : "밤")}" : "-";
            _headerClockLabel.text = tm != null ? tm.GetFormattedTime() : "-";
        }

        /// <summary>필터 탭 — '전체 5'(활성) / '건설 가능 N' / '재료 부족 M' / '기타 8'. 카운트 실측 동적.</summary>
        private void RefreshFilterTabs()
        {
            (int heldWood, int heldStone, int heldIron, int heldLeather, int heldMana, int gold) = CollectMaterialSnapshot();
            int buildable = 0, shortage = 0;
            foreach (string id in FacilityIds)
            {
                if (!BlueprintData.TryGet(id, out var def)) continue;
                if (EvaluateMaterialsEnough(def, heldWood, heldStone, heldIron, heldLeather, heldMana))
                {
                    if (gold >= def.goldCost) buildable++;
                }
                else
                {
                    shortage++;
                }
            }

            string[] labels =
            {
                $"전체 {FacilityIds.Length}",
                $"건설 가능 {buildable}",
                $"재료 부족 {shortage}",
                $"기타 {Mathf.Max(BlueprintData.Count - FacilityIds.Length, 0)}",
            };
            for (int i = 0; i < _tabLabels.Length; i++)
            {
                bool active = (FacilityTab)i == _activeTab;
                _tabLabels[i].text = labels[i];
                _tabLabels[i].style.color = new StyleColor(active ? BtnTextDark : TextSub);
                // [144차 P7-CON #16] 비활성 필터명도 700 Bold(239:705/707) — Normal 폐기.
                _tabLabels[i].style.unityFontStyleAndWeight = FontStyle.Bold;
                _tabButtons[i].style.backgroundColor = new StyleColor(active ? Accent : SurfaceCol);
            }
        }

        /// <summary>좌 목록 — 탭 필터 적용 후 항목 카드 재빌드(시설 6종 / 기타 8종). [152차 P0]</summary>
        private void RefreshBlueprintList()
        {
            _itemList.Clear();

            if (_activeTab == FacilityTab.Others)
            {
                foreach (var def in BlueprintData.GetAll())
                {
                    if (IsFacilityBlueprint(def.id)) continue;   // 기타 탭 = 기존 O8 8종만
                    _itemList.Add(BuildOtherItem(def));
                }
                UpdateItemScrollbar();   // [125차 후속37] 스크롤바 실측 동기화(기타 8종 — 스크롤 발생)
                return;
            }

            (int heldWood, int heldStone, int heldIron, int heldLeather, int heldMana, int gold) = CollectMaterialSnapshot();
            foreach (string id in FacilityIds)
            {
                if (!BlueprintData.TryGet(id, out var def)) continue;
                bool materialsOk = EvaluateMaterialsEnough(def, heldWood, heldStone, heldIron, heldLeather, heldMana);
                if (_activeTab == FacilityTab.Buildable && !(materialsOk && gold >= def.goldCost)) continue;
                if (_activeTab == FacilityTab.ShortMaterial && materialsOk) continue;
                _itemList.Add(BuildFacilityItem(def, materialsOk));
            }
            UpdateItemScrollbar();   // [125차 후속37] 스크롤바 실측 동기화
        }

        /// <summary>
        /// [125차 후속37] 시설 목록 스크롤바 썸 동기화 — 실측 content/viewport 비율(Figma scrollbar-thumb
        /// 4×208 정지값은 스크롤 가능 상태 예시). content = 항목 수×(101+8) 설계 상수 산출 — 레이아웃
        /// 미확정 시점에도 판정 가능. 스크롤 불필요(content ≤ viewport) 시 썸 = 뷰포트 전체.
        /// </summary>
        private void UpdateItemScrollbar()
        {
            if (_itemList == null || _itemScrollbarThumb == null) return;
            int count = _itemList.contentContainer != null ? _itemList.contentContainer.childCount : 0;
            float content = count > 0 ? count * (FacilityItemH + FacilityItemGap) - FacilityItemGap : 0f;
            float viewport = FacilityListViewportH;
            float scrollMax = Mathf.Max(content - viewport, 0f);
            float thumbH = scrollMax > 0f ? Mathf.Max(40f, viewport * viewport / content) : viewport;
            _itemScrollbarThumb.style.height = thumbH;
            float offset = Mathf.Clamp(_itemList.scrollOffset.y, 0f, scrollMax);
            _itemScrollbarThumb.style.top = scrollMax > 0f ? offset / scrollMax * (viewport - thumbH) : 0f;
        }

        /// <summary>보유 재료 요약 5칸 — GetItemCount 실측(Figma 목업 보유치 미채용).</summary>
        private void RefreshMaterialSummary()
        {
            for (int i = 0; i < SummaryMaterials.Length; i++)
            {
                var item = PlayerInventory.GetItemById(SummaryMaterials[i].itemId);
                int count = PlayerInventory.Instance != null
                    ? PlayerInventory.Instance.GetItemCount(SummaryMaterials[i].itemId)
                    : 0;
                _summaryCountLabels[i].text = $"{Mathf.Max(count, 0)}";
                _summaryCountLabels[i].tooltip = item != null ? item.description : null;
            }
        }

        // ═══════════════ 액션 핸들러 ═══════════════

        private void OnTabClicked(FacilityTab tab)
        {
            _activeTab = tab;
            Refresh();
        }

        private void OnItemClicked(string blueprintId)
        {
            _selectedBlueprintId = blueprintId;
            Refresh();
        }

        /// <summary>'건설하기'/Enter — 선택 설계도 배치 실행(기존 OnPlaceBlueprint 흐름 유지).</summary>
        private void OnBuildButtonClicked()
        {
            if (string.IsNullOrEmpty(_selectedBlueprintId))
            {
                UTKToastService.Show("먼저 건설할 시설을 선택하세요");
                return;
            }
            OnPlaceBlueprint(_selectedBlueprintId);
        }

        /// <summary>
        /// 배치 버튼 — 기존 계약: 현재 위치(Camera.main 있으면 position + forward×5, 없으면 Vector3.zero)에서
        /// TerritoryDatabase.ResolveTerritoryAt → ConstructionManager.TryPlace. 반환 메시지는 토스트로 표시.
        /// </summary>
        private void OnPlaceBlueprint(string blueprintId)
        {
            var mgr = ConstructionManager.Instance;
            if (mgr == null)
            {
                UTKToastService.Show("⚠️ ConstructionManager가 없습니다");
                return;
            }

            // [104차 F1] 홀로그램 배치 모드 진행 — 즉시 TryPlace 대신 위치 선택 모드로 전환.
            // 기존 즉시 배치(GetPlacePosition → TryPlace)는 ConstructionHologram(좌클릭 확정/Esc 취소)으로 이관.
            ConstructionHologram.BeginPlacement(blueprintId);
            UTKToastService.Show("위치를 선택하세요 — 클릭 확정 / Esc 취소");
            Close();   // 기존 닫힘 경로
        }

        private static Vector3 GetPlacePosition()
        {
            var cam = Camera.main;
            if (cam != null)
                return cam.transform.position + cam.transform.forward * 5f;
            return Vector3.zero;   // 카메라 없음(부팅 직전 등) 폴백
        }

        /// <summary>'재료 획득처' 버튼 — 부족 재료 획득처 안내 토스트(실측 아이템 description 기반 문구).</summary>
        private void OnSourceHintClicked()
        {
            if (string.IsNullOrEmpty(_selectedBlueprintId) || !BlueprintData.TryGet(_selectedBlueprintId, out var def))
                return;
            (int heldWood, int heldStone, int heldIron, int heldLeather, int heldMana, _) = CollectMaterialSnapshot();
            var lacking = CollectLackingItemIds(def, heldWood, heldStone, heldIron, heldLeather, heldMana);
            if (lacking.Count == 0)
            {
                UTKToastService.Show("부족한 재료가 없습니다");
                return;
            }
            UTKToastService.Show(BuildMaterialSourceToast(lacking), 4f);
        }

        // ═══════════════ UI 빌더 — 창 골격 ═══════════════

        /// <summary>헤더 @(64,36) 1472×18 — hammer 18×18 + '전초기지' / '건설 관리' | 우측 시각/일차.</summary>
        private void BuildHeaderBar()
        {
            var header = Block(HeaderX, HeaderY, HeaderW, HeaderH, "HeaderBar");
            // [112차 F-A] Figma 헤더 fills=[] — 배경/폴백 없음(투명, 루트 캔버스 #0B0E14가 그대로 비침).
            // 기존 [94차 F2] EnsureInlineBackgroundFallback(header)는 투명 루트 시절 보이지 않던 헤더를
            // 띄우기 위한 보간이었으나 루트 불투명화로 폴백 배경이 사실상 유령 바가 되어 제거.
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;

            var hammer = BuildHammerGlyph(18f);
            hammer.style.marginRight = 8f;
            header.Add(hammer);

            var crumbRoot = MakeLabel("전초기지", 13f, 400, TextSub, "HeaderRoot");
            header.Add(crumbRoot);

            var slash = MakeLabel("/", 13f, 400, TextDim, "HeaderSlash");
            slash.style.marginLeft = 6f;
            slash.style.marginRight = 6f;
            header.Add(slash);

            var crumb = MakeLabel("건설 관리", 13f, 500, TextMain, "HeaderCrumb");
            header.Add(crumb);

            var spacer = new VisualElement { name = "HeaderSpacer" };
            spacer.style.flexGrow = 1f;
            spacer.pickingMode = PickingMode.Ignore;
            header.Add(spacer);

            var clock = BuildClockGlyph(14f);
            clock.style.marginRight = 6f;
            header.Add(clock);

            _headerDayLabel = MakeLabel("-", 12f, 400, TextSub, "HeaderDay");
            _headerDayLabel.style.marginRight = 10f;
            header.Add(_headerDayLabel);

            _headerClockLabel = MakeLabel("-", 13f, 400, TextMain, "HeaderClock");
            header.Add(_headerClockLabel);
            // [99차 F-E] E-1 부착 수리 — NPCDialoguePanelUTK 패턴 준용: 창 내용 블록을 콘텐츠 루트(_content)에
            // 직결 부착한다. 기존은 Block() 생성 후 미부착(고아) — 창 루트(마젠타 배경)만 렌더되고 내용물 0이던 원인.
            _content.Add(header);
        }

        /// <summary>좌 패널 480×882 @(64,78) — 건설 목록.</summary>
        private void BuildLeftPanel()
        {
            var panel = Block(LeftX, LeftY, LeftW, LeftH, "LeftPanel");
            panel.style.backgroundColor = new StyleColor(PanelBg);   // [125차 후속37] Figma 현재값 #13181F@1.00(후속12 글래스 원복) — ApplyStroke 보더 유지
            EnsureInlineBackgroundFallback(panel);   // [94차 F2] 가드 — 인라인 배경 기존(PanelBg)이라 무수정
            ApplyRadius(panel, RadiusPanel);
            ApplyStroke(panel);
            panel.style.overflow = Overflow.Hidden;

            // [144차 P7-CON #5] 좌/우 패널 fx(239:695/799) DROP_SHADOW 38px #000000/0.251 — 기록 전용(미적용).
            //   UTK 인라인 스타일에는 외곽 그림자 렌더 경로가 없고, 창레벨 DisableWindowShadow()([112차 F-A])
            //   계약과 충돌 여지 — 근사 레이어를 신설하지 않고 미구현 상태를 기록으로 남긴다(위조 금지).
            // ① 제목행 — blocks 23×23 + '건설 목록' 22/700 + '전초기지 시설' 11/400
            //    ([144차 P7-CON #11] Figma 좌 제목행(239:696)에 닫기 버튼 무존재 — 구 닫기 30×30 생성 폐기.
            //     창 종료 기능은 우 패널 RightCloseButton으로 유지 — Close 로직 무손상, 좌측 버튼만 제거)
            //    [144차 P7-CON #7/#8/#9] 재실측(239:922/699/700) — blocks @(24,27), 제목 @x59,
            //    보조 제목은 제목 우측 병렬 @(157,24)(구 세로 스택 (57,52) 폐기).
            var blocks = BuildBlocksGlyph(23f);
            blocks.style.position = Position.Absolute;
            blocks.style.left = 24f;
            blocks.style.top = 27f;      // [#7] 34→27 (239:922 패널로컬 y27)
            panel.Add(blocks);

            var title = MakeLabel("건설 목록", 22f, 700, TextMain, "LeftTitle");
            title.style.position = Position.Absolute;
            title.style.left = 59f;      // [#8] 57→59 (239:699 @59,24)
            title.style.top = 24f;
            panel.Add(title);

            var sub = MakeLabel("전초기지 시설", 11f, 400, TextSub, "LeftSub");
            sub.style.position = Position.Absolute;
            sub.style.left = 157f;       // [#9] 제목 우측 병렬(239:700 @157,24) — 구 세로 스택 폐기
            sub.style.top = 24f;
            panel.Add(sub);

            // [144차 P7-CON #11] 좌 패널 닫기 버튼 제거 — 구 BuildPanelCloseButton(426,30) 좌측 생성은
            // Figma 좌 제목행(239:696)에 무존재. 버튼 생성만 제거 — 창 닫기 기능은 우측 버튼이 담당.

            // ② 필터 탭 140×36 r6 gap6 @(88,166) — Figma 3탭(전체/건설 가능/재료 부족) 진실치 +
            //    '기타 8' 신규 보존 탭(480 패널에 4×140 불가 → 2행 배치 — 기능 보존 우선, 보고 사항).
            //    [144차 P7-CON #14] '기타' 탭은 Figma 3탭 체계(239:701~) 외 요소 — 기능 보존 계약으로 유지
            //    (기존 O8 8종 설치 흐름 의존, ConstructionManagementFigmaTests OthersTab 계약).
            //    역반영 제안: Figma 3탭에 '기타 N' 추가(또는 기타 항목을 '전체'에만 혼합) — 디자인 협의 후 반영.
            string[] tabNames = { "전체", "건설 가능", "재료 부족", "기타" };
            _tabButtons = new Button[tabNames.Length];
            _tabLabels = new Label[tabNames.Length];
            for (int i = 0; i < tabNames.Length; i++)
            {
                int tabIndex = i;   // 클로저 캡처 — 반복 변수 안전화
                var tab = new Button(() => OnTabClicked((FacilityTab)tabIndex)) { name = "FilterTab_" + tabNames[i] };
                tab.style.position = Position.Absolute;
                tab.style.left = PanelPad + (i % 3) * (FilterTabW + FilterTabGap);
                tab.style.top = FilterTabY + (i / 3) * (FilterTabH + FilterTabGap);
                tab.style.width = FilterTabW;
                tab.style.height = FilterTabH;
                tab.style.backgroundColor = new StyleColor((FacilityTab)i == _activeTab ? Accent : SurfaceCol);
                // [144차 P7-CON #15] 비활성 필터(239:704/706) — stroke #30363D/0.502 1.0 신설(활성 탭은 무보더 유지).
                if ((FacilityTab)i != _activeTab) ApplyStroke(tab);
                ApplyRadius(tab, RadiusBox);
                tab.style.paddingLeft = tab.style.paddingRight = 0f;
                tab.style.paddingTop = tab.style.paddingBottom = 0f;
                // [144차 P7-CON #16] 비활성 필터명(239:705/707) — 12/700 Bold(구 400 폐기, 활성과 동일 굵기).
                var tabLabel = MakeLabel(tabNames[i], 12f, 700,
                    (FacilityTab)i == _activeTab ? BtnTextDark : TextSub, "FilterTabLabel");
                tabLabel.style.position = Position.Absolute;
                tabLabel.style.left = 0f;
                tabLabel.style.top = 0f;
                tabLabel.style.width = FilterTabW;
                tabLabel.style.height = FilterTabH;
                tabLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                tabLabel.pickingMode = PickingMode.Ignore;
                _tabLabels[i] = tabLabel;
                tab.Add(tabLabel);
                _tabButtons[i] = tab;
                panel.Add(tab);
            }

            // [104차 F4] 목록 헤더행 신설 — '건설할 시설 선택' + '재료 수치: 보유 / 필요' @(24,140)
            // figma 239:683 재실측: 탭(124 끝)과 항목(168) 사이 44px 공백에 헤더행이 존재(로컬 y=140~142).
            var listHeader = new VisualElement { name = "ListHeaderRow" };
            listHeader.style.position = Position.Absolute;
            listHeader.style.left = PanelPad;
            listHeader.style.top = 140f;
            listHeader.style.width = 432f;   // [144차 P7-CON #18] 420→432 (239:709 — 우측 기준문구 우단 정렬 회복)
            listHeader.style.height = 20f;
            panel.Add(listHeader);

            var listHeaderTitle = MakeLabel("건설할 시설 선택", 12f, 700, TextMain, "ListHeaderTitle");
            listHeaderTitle.style.position = Position.Absolute;
            listHeaderTitle.style.left = 0f;
            listHeaderTitle.style.top = 2f;   // [144차 P7-CON #19] y140→142 (239:710)
            listHeader.Add(listHeaderTitle);

            var listHeaderMat = MakeLabel("재료 수치: 보유 / 필요", 10f, 400, TextSub, "ListHeaderMat");
            listHeaderMat.style.position = Position.Absolute;
            listHeaderMat.style.right = 0f;
            listHeaderMat.style.top = 2f;
            listHeader.Add(listHeaderMat);

            // ③ 시설 항목 목록 — [125차 후속37] 프레임 432폭 @(24,168)(항목 420 + 간격 8 + 트랙 4 — Figma
            //    '시설 목록' 432×565 실측), 뷰포트 5행 = 5×101+4×8 = 537 ([152차 P0] 시설 6종·기타 8종 모두 스크롤)
            _itemList = new ScrollView(ScrollViewMode.Vertical) { name = "BlueprintList" };
            _itemList.style.position = Position.Absolute;
            _itemList.style.left = PanelPad;
            _itemList.style.top = FacilityListY;
            _itemList.style.width = FacilityItemW + ScrollbarGap + ScrollbarTrackW;
            _itemList.style.height = FacilityListViewportH;
            _itemList.style.flexShrink = 0f;
            // [125차 후속37] 기본 수직 스크롤러 숨김 — Figma 커스텀 트랙/썸(③-b)로 대체. 휠/터치 스크롤은 유지.
            _itemList.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            panel.Add(_itemList);

            // ③-b 스크롤바 가시화 — [125차 후속37] Figma scrollbar-track/thumb 신규 실측 반영.
            //    트랙 4×537 r2 #20262E @(452,168)(패널 로컬 — 목록 프레임 우측 가장자리 정렬),
            //    썸 4×r2 #58A6FF(높이/위치는 UpdateItemScrollbar가 실측 비율 동기화).
            var scrollbarTrack = new VisualElement { name = "ItemScrollbarTrack" };
            scrollbarTrack.style.position = Position.Absolute;
            scrollbarTrack.style.left = PanelPad + FacilityItemW + ScrollbarGap;   // 452 — 목록 프레임 우측 정렬
            scrollbarTrack.style.top = FacilityListY;
            scrollbarTrack.style.width = ScrollbarTrackW;
            scrollbarTrack.style.height = FacilityListViewportH;
            scrollbarTrack.style.backgroundColor = new StyleColor(ScrollTrackCol);
            ApplyRadius(scrollbarTrack, ScrollbarRadius);
            scrollbarTrack.pickingMode = PickingMode.Ignore;
            panel.Add(scrollbarTrack);

            _itemScrollbarThumb = new VisualElement { name = "ItemScrollbarThumb" };
            _itemScrollbarThumb.style.position = Position.Absolute;
            _itemScrollbarThumb.style.left = 0f;
            _itemScrollbarThumb.style.top = 0f;
            _itemScrollbarThumb.style.width = ScrollbarTrackW;
            _itemScrollbarThumb.style.backgroundColor = new StyleColor(Accent);   // #58A6FF — Figma scrollbar-thumb fill 실측
            ApplyRadius(_itemScrollbarThumb, ScrollbarRadius);
            _itemScrollbarThumb.pickingMode = PickingMode.Ignore;
            scrollbarTrack.Add(_itemScrollbarThumb);
            if (_itemList.verticalScroller != null && _itemList.verticalScroller.slider != null)
                _itemList.verticalScroller.slider.RegisterValueChangedCallback(_ => UpdateItemScrollbar());   // 스크롤 실측 동기화

            // ④ 보유 재료 요약 432×110 @(88,826) — '공용 창고' + '건설 전 보유량' + 5칸 81.6×70 gap6
            var summaryBox = new VisualElement { name = "MaterialSummaryBox" };
            summaryBox.style.position = Position.Absolute;
            summaryBox.style.left = PanelPad;
            summaryBox.style.top = SummaryBoxY;
            summaryBox.style.width = SummaryBoxW;
            summaryBox.style.height = SummaryBoxH;
            panel.Add(summaryBox);

            var summaryTitle = MakeLabel("공용 창고", 11f, 700, TextSub, "SummaryTitle");
            summaryTitle.style.position = Position.Absolute;
            summaryTitle.style.left = 0f;
            summaryTitle.style.top = 14f;     // [144차 P7-CON #34] y8→14 (239:770 박스로컬 y14)
            summaryBox.Add(summaryTitle);

            var summarySub = MakeLabel("건설 전 보유량", 10f, 400, TextDim, "SummarySub");
            summarySub.style.position = Position.Absolute;
            summarySub.style.right = 0f;
            summarySub.style.top = 14f;       // [#34] y10→14 — 제목과 동일 y 병렬
            summaryBox.Add(summarySub);

            _summaryCountLabels = new Label[SummaryMaterials.Length];
            for (int i = 0; i < SummaryMaterials.Length; i++)
            {
                var cell = new VisualElement { name = "SummaryCell_" + SummaryMaterials[i].itemId };
                cell.style.position = Position.Absolute;
                cell.style.left = i * (SummaryCellW + SummaryCellGap);
                cell.style.top = SummaryCellY;
                cell.style.width = SummaryCellW;
                cell.style.height = SummaryCellH;
                cell.style.backgroundColor = new StyleColor(InputCol);
                ApplyRadius(cell, RadiusBox);
                summaryBox.Add(cell);

                // 아이콘 12×12 — 실측 아이템 아이콘(베이크 전/부재 시 생략)
                // [144차 P7-CON #37] 셀로컬 (8,8)(239:955) — 구 (8,10) 폐기.
                var icon = new VisualElement { name = "SummaryIcon" };
                icon.style.position = Position.Absolute;
                icon.style.left = 8f;
                icon.style.top = 8f;
                icon.style.width = 12f;
                icon.style.height = 12f;
                icon.pickingMode = PickingMode.Ignore;
                var itemData = PlayerInventory.GetItemById(SummaryMaterials[i].itemId);
                Texture2D iconTex = itemData != null ? ItemIconDatabase.GetOrCreateIcon(itemData) : null;
                if (iconTex != null)
                {
                    icon.style.backgroundImage = UTKTextureSafe.ToBackground(iconTex);
                    icon.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                }
                cell.Add(icon);

                // 재료명 10/400 — 실측 displayName 우선(폴백 = Figma 라벨)
                // [144차 P7-CON #38] 아이콘 우측 병렬 (25,8)(239:777) — 구 아이콘 하단 (8,26) 폐기.
                string materialName = itemData != null && !string.IsNullOrEmpty(itemData.displayName)
                    ? itemData.displayName
                    : SummaryMaterials[i].fallback;
                var nameLabel = MakeLabel(materialName, 10f, 400, TextSub, "SummaryName");
                nameLabel.style.position = Position.Absolute;
                nameLabel.style.left = 25f;
                nameLabel.style.top = 8f;
                nameLabel.style.right = 4f;
                nameLabel.style.overflow = Overflow.Hidden;
                nameLabel.style.whiteSpace = WhiteSpace.NoWrap;
                cell.Add(nameLabel);

                // 보유량 15/500 — GetItemCount 실측
                // [144차 P7-CON #39] 셀로컬 (8,29)(239:778) — 구 (8,44) 폐기.
                var countLabel = MakeLabel("0", 15f, 500, TextMain, "SummaryCount");
                countLabel.style.position = Position.Absolute;
                countLabel.style.left = 8f;
                countLabel.style.top = 29f;
                cell.Add(countLabel);
                _summaryCountLabels[i] = countLabel;
            }
            // [99차 F-E] E-1 부착 수리 — 좌 패널 블록 _content 직결 부착(선례: NPCDialoguePanelUTK _content.Add 패턴).
            _content.Add(panel);
        }

        /// <summary>우 패널 968×882 @(568,78) — 건설 정보.</summary>
        private void BuildRightPanel()
        {
            var panel = Block(RightX, RightY, RightW, RightH, "RightPanel");
            panel.style.backgroundColor = new StyleColor(PanelBg);   // [125차 후속37] Figma 현재값 #13181F@1.00(후속12 글래스 원복) — ApplyStroke 보더 유지
            EnsureInlineBackgroundFallback(panel);   // [94차 F2] 가드 — 인라인 배경 기존(PanelBg)이라 무수정
            ApplyRadius(panel, RadiusPanel);
            ApplyStroke(panel);
            panel.style.overflow = Overflow.Hidden;

            // [144차 P7-CON #5] 우 패널 fx(239:799) DROP_SHADOW 38px #000000/0.251 — 기록 전용(좌 패널 동일 — BuildLeftPanel 참조).
            // ① 제목행 — hammer 23×23 + '건설 정보' 22/700 + '선택한 시설의 재료 및 조건' 11/400 + 닫기 30×30
            //    [144차 P7-CON #10] 재실측(239:800~804) — hammer @(24,27)(239:801/970 ay=108), 제목 @x59,
            //    보조 제목은 제목 우측 병렬 @(157,24)(구 세로 스택 (57,52) 폐기).
            var hammer = BuildHammerGlyph(23f);
            hammer.style.position = Position.Absolute;
            hammer.style.left = 24f;
            hammer.style.top = 27f;      // [#10] 34→27 (239:970 ay=108 − 패널 81)
            panel.Add(hammer);

            var title = MakeLabel("건설 정보", 22f, 700, TextMain, "RightTitle");
            title.style.position = Position.Absolute;
            title.style.left = 59f;      // [#10] 57→59 (239:803 ax=48745)
            title.style.top = 24f;
            panel.Add(title);

            var sub = MakeLabel("선택한 시설의 재료 및 조건", 11f, 400, TextSub, "RightSub");
            sub.style.position = Position.Absolute;
            sub.style.left = 157f;       // [#10] 제목 우측 병렬(239:804 ax=48843 @157,24)
            sub.style.top = 24f;
            panel.Add(sub);

            // [144차 P7-CON #12] 우 닫기(239:805) — 패널로컬 (914,24)(구 y30 폐기), 30×30 fill #191F27
            // + stroke #30363D/0.502 r6, X아이콘 15×15(239:806 — BuildPanelCloseButton 규격 참조).
            panel.Add(BuildPanelCloseButton(RightW - PanelPad - 30f, 24f, "RightCloseButton"));

            // ② 선택 시설 개요 920×158 #191F27 r10 @(24,92)
            var overview = new VisualElement { name = "OverviewCard" };
            overview.style.position = Position.Absolute;
            overview.style.left = PanelPad;
            overview.style.top = OverviewY;
            overview.style.width = OverviewW;
            overview.style.height = OverviewH;
            overview.style.backgroundColor = new StyleColor(SurfaceCol);
            ApplyRadius(overview, RadiusCard);
            UTKTheme.ApplyElementBorder(overview);   // [144차 P7-CON #40] 개요 stroke(239:807) — #30363D/0.502 신설(테두리 누락 수리, V2 크롬)
            panel.Add(overview);

            var nameRow = new VisualElement { name = "OverviewNameRow" };
            nameRow.style.position = Position.Absolute;
            nameRow.style.left = 16f;    // [144차 P7-CON #41] 개요로컬 (16,30)(239:810) — 구 (24,16) 폐기
            nameRow.style.top = 30f;
            nameRow.style.right = BlueprintBoxW + 40f;
            nameRow.style.flexDirection = FlexDirection.Row;
            nameRow.style.alignItems = Align.Center;
            _selNameLabel = MakeLabel("선택된 시설 없음", 21f, 700, TextMain, "OverviewName");
            nameRow.Add(_selNameLabel);
            _selLvLabel = MakeLabel("Lv.1", 11f, 400, Accent, "OverviewLevel");
            _selLvLabel.style.marginLeft = 10f;   // [144차 P7-CON #42] 이름 gap10(239:811) — 구 12 폐기
            nameRow.Add(_selLvLabel);
            overview.Add(nameRow);

            _selBadgeHost = new VisualElement { name = "OverviewBadgeHost" };
            _selBadgeHost.style.position = Position.Absolute;
            _selBadgeHost.style.left = 184f;   // [144차 P7-CON #43] 개요로컬 (184,32)(239:812) — 이름행 하단 (24,48) 폐기
            _selBadgeHost.style.top = 32f;
            overview.Add(_selBadgeHost);

            _selDescLabel = MakeLabel("왼쪽 목록에서 시설을 선택하세요", 12f, 400, TextSub, "OverviewDesc");
            _selDescLabel.style.position = Position.Absolute;
            _selDescLabel.style.left = 16f;        // [144차 P7-CON #44] 개요로컬 (16,64)(239:815) — 구 (24,80) 폐기
            _selDescLabel.style.top = 64f;
            // [#44] Figma lineH 20 — IStyle.lineHeight 미지원(Unity 6000.x) → white-space 최대 4행 캡으로 근사 [144차 P7-CON #44 보정]
            _selDescLabel.style.whiteSpace = WhiteSpace.Normal;
            _selDescLabel.style.maxHeight = 80f;
            _selDescLabel.style.right = BlueprintBoxW + 40f;
            overview.Add(_selDescLabel);

            var sparkles = BuildSparklesGlyph(13f);   // [144차 P7-CON #45] 13×13(239:816) — 구 12 폐기
            sparkles.style.position = Position.Absolute;
            sparkles.style.left = 16f;   // [#45] 개요로컬 (16,93)
            sparkles.style.top = 93f;
            overview.Add(sparkles);

            _selUnlockLabel = MakeLabel("효과 -", 11f, 400, Accent, "OverviewUnlock");
            _selUnlockLabel.style.position = Position.Absolute;
            _selUnlockLabel.style.left = 35f;   // [#45] 텍스트 (35,93) — sparkles 우 6px 간격
            _selUnlockLabel.style.top = 93f;
            overview.Add(_selUnlockLabel);

            _selOpsLabel = MakeLabel("운영 조건  완공 후 E키로 시설을 사용하세요", 11f, 400, TextSub, "OverviewOps");
            _selOpsLabel.style.position = Position.Absolute;
            _selOpsLabel.style.left = 16f;   // [144차 P7-CON #46] 개요로컬 (16,115)(239:819) — 구 (24,126) 폐기
            _selOpsLabel.style.top = 115f;
            overview.Add(_selOpsLabel);

            // 설계도 박스 234×98 #0F141B r6 @(670,30) [#47] — 'FACILITY / LV.1' 8/400 #586473 @(8,7) [#49]
            // + 부지 '{칸} × {칸}' 9/400 @(199,80) [#50]
            var bpBox = new VisualElement { name = "BlueprintBox" };
            bpBox.style.position = Position.Absolute;
            bpBox.style.left = 670f;   // [144차 P7-CON #47] 개요로컬 x670(239:820) — 구 산식 920-24-234=662 폐기
            bpBox.style.top = 30f;
            bpBox.style.width = BlueprintBoxW;
            bpBox.style.height = BlueprintBoxH;
            bpBox.style.backgroundColor = new StyleColor(InputCol);
            ApplyRadius(bpBox, RadiusBox);
            // [144차 P7-CON #48] 설계도 벡터(239:821/822) — 구 코드는 라벨 2개만.
            //  · 설계 그리드(239:821): 박스 전체 234×98 stroke #58A6FF@0.047 w1 — 외곽 Rect + 십자 그리드선 근사.
            //  · 대장간 입체 설계(239:822): 160×88 stroke #58A6FF w1.3 @박스로컬(37,5) — 입체선 외곽 Rect 근사
            //    (Figma 벡터 패스 전체 재현 불가 — 프레임 구성 요소로 근사, 라벨은 벡터 위 렌더).
            var bpGridCol = new Color(0x58 / 255f, 0xA6 / 255f, 0xFF / 255f, 0.047f);
            var bpGrid = new VisualElement { name = "BlueprintGridRect" };
            bpGrid.style.position = Position.Absolute;
            bpGrid.style.left = 0f;
            bpGrid.style.top = 0f;
            bpGrid.style.width = BlueprintBoxW;
            bpGrid.style.height = BlueprintBoxH;
            bpGrid.pickingMode = PickingMode.Ignore;
            UTKTheme.ApplyElementBorder(bpGrid, bpGridCol, 1f);
            bpBox.Add(bpGrid);
            for (int gi = 0; gi < 2; gi++)
            {
                var gridLineV = new VisualElement { name = "BlueprintGridLineV" + gi };
                gridLineV.style.width = 1f;
                gridLineV.style.height = BlueprintBoxH;
                gridLineV.style.backgroundColor = new StyleColor(bpGridCol);
                gridLineV.style.position = Position.Absolute;
                gridLineV.style.left = BlueprintBoxW * (gi + 1) / 3f;
                gridLineV.pickingMode = PickingMode.Ignore;
                bpBox.Add(gridLineV);
                var gridLineH = new VisualElement { name = "BlueprintGridLineH" + gi };
                gridLineH.style.width = BlueprintBoxW;
                gridLineH.style.height = 1f;
                gridLineH.style.backgroundColor = new StyleColor(bpGridCol);
                gridLineH.style.position = Position.Absolute;
                gridLineH.style.top = BlueprintBoxH * (gi + 1) / 3f;
                gridLineH.pickingMode = PickingMode.Ignore;
                bpBox.Add(gridLineH);
            }
            var bpIso = new VisualElement { name = "BlueprintIsoRect" };
            bpIso.style.position = Position.Absolute;
            bpIso.style.left = 37f;
            bpIso.style.top = 5f;
            bpIso.style.width = 160f;
            bpIso.style.height = 88f;
            bpIso.pickingMode = PickingMode.Ignore;
            UTKTheme.ApplyElementBorder(bpIso, Accent, 1.3f);
            bpBox.Add(bpIso);
            // [144차 P7-CON #49] 설계도 표기(239:823) — 8 Geist Mono #586473 @박스로컬 (8,7)
            //   (구 TextSub(#8B949E) @(16,14) 폐기). P0 #48 벡터 위 렌더 순서 유지(라벨은 벡터 뒤 추가).
            _bpBoxLabel = MakeLabel("FACILITY / LV.1", 8f, 400, TextDim, "BlueprintBoxLabel");
            _bpBoxLabel.style.position = Position.Absolute;
            _bpBoxLabel.style.left = 8f;
            _bpBoxLabel.style.top = 7f;
            bpBox.Add(_bpBoxLabel);
            // [144차 P7-CON #50] 부지 표기(239:824) — '{칸} × {칸}' 9 Geist Mono #58A6FF @박스로컬 (199,80).
            //   '부지 ' 접두어 폐기(Figma 텍스트 '3 × 3' 전무) — 실측치는 RefreshRightPanel의 FormatFootprint 산출.
            _bpFootprintLabel = MakeLabel("-", 9f, 400, Accent, "BlueprintFootprint");
            _bpFootprintLabel.style.position = Position.Absolute;
            _bpFootprintLabel.style.left = 199f;
            _bpFootprintLabel.style.top = 80f;
            bpBox.Add(_bpFootprintLabel);
            overview.Add(bpBox);

            // ③ 건설 조건 3카드 298.7×60 gap12 — 건설 시간/필요 부지/필요 작업 인원
            string[] conditionNames = { "건설 시간", "필요 부지", "필요 작업 인원" };
            _conditionValueLabels = new Label[conditionNames.Length];
            for (int i = 0; i < conditionNames.Length; i++)
            {
                var card = new VisualElement { name = "ConditionCard_" + conditionNames[i] };
                card.style.position = Position.Absolute;
                card.style.left = PanelPad + i * (ConditionCardW + ConditionGap);
                card.style.top = ConditionY;
                card.style.width = ConditionCardW;
                card.style.height = ConditionCardH;
                card.style.backgroundColor = new StyleColor(SurfaceCol);
                ApplyRadius(card, RadiusCard);
                UTKTheme.ApplyElementBorder(card);   // [144차 P7-CON #51] 조건 카드 stroke(239:825) — #30363D/0.502 신설(테두리 누락 수리)

                // [144차 P7-CON #52] 조건 카드 아이콘군(239:979/982/985) — timer/grid-3x3/users 19×19
                // @카드로컬(12,21) stroke #8B949E 1.6. Resources/UI/icons에 대응 PNG(timer/grid/users) 없음 실측
                // → 글리프 근사(timer=기존 BuildClockGlyph, grid/users=신설). 텍스트 블록(항목/수치)은
                //   P1 #53/#54로 x 16→43 이동 — 아이콘(12..31)과 겹침 해소.
                VisualElement cardIcon = i switch
                {
                    0 => BuildClockGlyph(19f),      // timer 근사 — 원 테두리+바늘(#8B949E)
                    1 => BuildGridGlyph(19f),       // grid-3x3 근사 — 외곽+내부 3분할선
                    _ => BuildUsersGlyph(19f),      // users 근사 — 2인 헤드+보디
                };
                cardIcon.style.position = Position.Absolute;
                cardIcon.style.left = 12f;
                cardIcon.style.top = 21f;
                card.Add(cardIcon);

                // [144차 P7-CON #53] 조건 항목(239:829/834/839) — 10/400 @카드로컬 (43,11)(구 11/400 @(16,9) 폐기)
                var cardTitle = MakeLabel(conditionNames[i], 10f, 400, TextSub, "ConditionTitle");
                cardTitle.style.position = Position.Absolute;
                cardTitle.style.left = 43f;
                cardTitle.style.top = 11f;
                card.Add(cardTitle);

                var cardValue = MakeLabel("-", 18f, 400, TextMain, "ConditionValue");
                cardValue.style.position = Position.Absolute;
                cardValue.style.left = 43f;   // [144차 P7-CON #54] 조건 수치(239:830/835/840) @카드로컬 (43,26) — 구 (16,28) 폐기
                cardValue.style.top = 26f;
                card.Add(cardValue);
                _conditionValueLabels[i] = cardValue;
                panel.Add(card);
            }

            // ④ '필요 재료' 16/700 + 보조 문구
            var matTitle = MakeLabel("필요 재료", 16f, 700, TextMain, "MaterialTitle");
            matTitle.style.position = Position.Absolute;
            matTitle.style.left = PanelPad;
            matTitle.style.top = 346f;
            panel.Add(matTitle);

            // [144차 P7-CON #56] 충족 수(239:845) — '필요 재료' 제목(63×19) 우 gap10: Figma ax 48783(=48710+63+10)
            // → 패널 로컬 (97,348). 12 Geist Mono #58A6FF. 실데이터(충족 재료 종류 수/요구 종류 수)는
            // RebuildMaterialRows에서 행별 IsMaterialSufficient 산출 — 고정 가짜값 금지.
            // 미선택/재료 요구 없음 시 표기 스킵(display none — 데이터 없으면 스킵 규약).
            _matSatisfyLabel = MakeLabel("", 12f, 400, Accent, "MatSatisfyCount");
            _matSatisfyLabel.style.position = Position.Absolute;
            _matSatisfyLabel.style.left = 97f;
            _matSatisfyLabel.style.top = 348f;
            _matSatisfyLabel.style.display = DisplayStyle.None;
            panel.Add(_matSatisfyLabel);

            var matSub = MakeLabel("공용 창고 기준 · 수량 단위: 개", 11f, 400, TextSub, "MaterialSub");
            matSub.style.position = Position.Absolute;
            matSub.style.right = PanelPad;   // [104차 F4] 우측 정렬 — figma x=1376(패널 우측 24 여백)
            matSub.style.top = 349.5f;       // [104차 F4] y 교정 370→349.5 재실측
            panel.Add(matSub);

            // ⑤ 필요 재료 표 920×288 r10 @(24,394) — 열헤더 920×32 + 행 920×64
            var tableHost = new VisualElement { name = "MaterialTableHost" };
            tableHost.style.position = Position.Absolute;
            tableHost.style.left = PanelPad;
            tableHost.style.top = MaterialTableY;
            tableHost.style.width = MaterialTableW;
            tableHost.style.height = MaterialTableH;
            tableHost.style.backgroundColor = new StyleColor(InputCol);
            ApplyRadius(tableHost, RadiusCard);
            UTKTheme.ApplyElementBorder(tableHost);   // [144차 P7-CON #58] 재료 표 stroke(239:847) — #30363D/0.502 신설(테두리 누락 수리)
            tableHost.style.overflow = Overflow.Hidden;
            panel.Add(tableHost);

            var colHeader = new VisualElement { name = "MaterialColHeader" };
            colHeader.style.position = Position.Absolute;
            colHeader.style.left = 0f;
            colHeader.style.top = 0f;
            colHeader.style.width = MaterialTableW;
            colHeader.style.height = MatColHeaderH;
            colHeader.style.backgroundColor = new StyleColor(InputCol);
            colHeader.pickingMode = PickingMode.Ignore;
            // [144차 P7-CON #59] 열 헤더 x = 재료 14 / 보유량 374 / 필요량 490 / 잔여 606 / 상태 752
            // (Figma 239:849~853 — 구 '재료'·'보유량' x=14 겹침 폐기). 행 셀도 동일 상수(ColX*) 정렬.
            (string, float)[] columns =
            {
                ("재료", 14f), ("보유량", ColXHeld), ("필요량", ColXNeed),
                ("건설 후 잔여", ColXRemain), ("충족 상태", ColXStatus),
            };
            foreach ((string colName, float colX) in columns)
            {
                var colLabel = MakeLabel(colName, 11f, 400, TextSub, "MatColHeader_" + colName);
                colLabel.style.position = Position.Absolute;
                colLabel.style.left = colX;
                colLabel.style.top = (MatColHeaderH - 14f) * 0.5f;
                colHeader.Add(colLabel);
            }
            tableHost.Add(colHeader);

            _materialRows = new ScrollView(ScrollViewMode.Vertical) { name = "MaterialRows" };
            _materialRows.style.position = Position.Absolute;
            _materialRows.style.left = 0f;
            _materialRows.style.top = MatColHeaderH;
            _materialRows.style.width = MaterialTableW;
            _materialRows.style.height = MaterialTableH - MatColHeaderH;
            _materialRows.style.flexShrink = 0f;
            tableHost.Add(_materialRows);

            // ⑥ 건설 실행 920×106 @(592,796) r10 — 상태별 재빌드(충족/부족)
            _executeBox = new VisualElement { name = "ExecuteBox" };
            _executeBox.style.position = Position.Absolute;
            _executeBox.style.left = PanelPad;
            _executeBox.style.top = ExecuteY;
            _executeBox.style.width = ExecuteW;
            _executeBox.style.height = ExecuteH;
            ApplyRadius(_executeBox, RadiusCard);
            panel.Add(_executeBox);

            // ⑦ 조건 확인 + 유의사항
            _checkLineLabel = MakeLabel("", 10f, 400, TextSub, "CheckLine");
            _checkLineLabel.style.position = Position.Absolute;
            // [105차 C] Figma '조건 확인' execute-local x=16 → 패널 로컬 PanelPad+16
            _checkLineLabel.style.left = PanelPad + 16f;
            _checkLineLabel.style.top = CheckLineY;
            panel.Add(_checkLineLabel);

            // [144차 P7-CON #79] 미충족 조건 병기(239:914) — '! 재료 1종 부족' 10/400 #E5AD65 @패널로컬 x185
            //   (JSON 실측 ax 48871 = ✓라인 48726 + 145, ay 동일 869). 충족 ✓ 라인은 소실시키지 않고 우측 병기 —
            //   구 단일 라벨 스왑(부족 시 ✓ 라인 소실) 폐기.
            _checkWarnLabel = MakeLabel("", 10f, 400, Warn, "CheckWarnLine");
            _checkWarnLabel.style.position = Position.Absolute;
            _checkWarnLabel.style.left = 185f;   // ✓라인(패널로컬 40) + JSON 간격 145
            _checkWarnLabel.style.top = CheckLineY;
            _checkWarnLabel.style.display = DisplayStyle.None;
            panel.Add(_checkWarnLabel);

            // [105차 C] Figma 유의사항 전문('전방 5m' 구문 폐기) — 텍스트 execute-local x=20(패널 로컬 PanelPad+20, info 아이콘 13px 뒤 7px)
            // [144차 P7-CON #81] 유의사항 info 아이콘(239:1021) — 13×13 stroke #8B949E 1.6 @패널로컬(24,843).
            //   구 코드는 텍스트만 — 원 테두리+i 점/막대 글리프 근사. 텍스트 x=44(아이콘 13px 우 7px 간격) 기존 유지.
            var noticeIcon = BuildInfoGlyph(13f);
            noticeIcon.style.position = Position.Absolute;
            noticeIcon.style.left = PanelPad;      // 패널 로컬 x 24
            noticeIcon.style.top = 843f;           // Figma ay 924 − 패널 ay 81 = 843
            panel.Add(noticeIcon);
            var notice = MakeLabel(
                "건설 시작 시 재료가 즉시 차감됩니다. 재료가 부족한 동안에는 건설할 수 없습니다.",
                11f, 400, TextSub, "NoticeLine");
            notice.style.position = Position.Absolute;
            notice.style.left = PanelPad + 20f;
            notice.style.top = NoticeY;
            panel.Add(notice);
            // [99차 F-E] E-1 부착 수리 — 우 패널 블록 _content 직결 부착(선례: NPCDialoguePanelUTK _content.Add 패턴).
            _content.Add(panel);
        }

        /// <summary>도움말 @(64,984) — 안내 + 키 조작.</summary>
        private void BuildHelpBar()
        {
            var help = Block(HelpX, HelpY, HelpW, HelpH, "HelpBar");
            // [112차 F-A] Figma 도움말 바 fills=[] — 배경/폴백 없음(투명, 루트 캔버스 #0B0E14가 그대로 비침).
            // 기존 [94차 F2] EnsureInlineBackgroundFallback(help)는 투명 루트 시절 보이지 않던 도움말을
            // 띄우기 위한 보간이었으나 루트 불투명화로 폴백 배경이 사실상 유령 바가 되어 제거.
            help.style.flexDirection = FlexDirection.Row;
            help.style.alignItems = Align.Center;

            var hint = MakeLabel("시설을 선택해 필요한 재료와 건설 조건을 확인하세요.", 12f, 400, TextSub, "HelpHint");
            hint.style.flexGrow = 1f;
            help.Add(hint);

            var keys = MakeLabel("클릭  시설 선택     ·     Enter  건설 확인     ·     Esc  닫기", 12f, 400, TextSub, "HelpKeys");
            help.Add(keys);
            // [99차 F-E] E-1 부착 수리 — 도움말 블록 _content 직결 부착(선례: NPCDialoguePanelUTK _content.Add 패턴).
            _content.Add(help);
        }

        // ═══════════════ 항목 카드 빌더 ═══════════════

        /// <summary>시설 항목 420×101 r6 [125차 후속37] — 아이콘 36×36 + 시설명 15/700 + 역할 11/400 + 상태 배지 76×21 + 재료 행.</summary>
        private VisualElement BuildFacilityItem(BlueprintData.BlueprintDef def, bool materialsOk)
        {
            (int heldWood, int heldStone, int heldIron, int heldLeather, int heldMana, int gold) = CollectMaterialSnapshot();
            bool selected = _selectedBlueprintId == def.id;

            var item = new VisualElement { name = "FacilityItem_" + def.id };
            item.style.width = FacilityItemW;
            item.style.minWidth = FacilityItemW;
            item.style.height = FacilityItemH;
            item.style.minHeight = FacilityItemH;
            item.style.flexShrink = 0f;
            item.style.marginBottom = FacilityItemGap;
            item.style.backgroundColor = new StyleColor(selected ? ItemSelectedCol : SurfaceCol);
            ApplyRadius(item, RadiusBox);
            if (selected)
            {
                // 선택 항목 — stroke #58A6FF/1.5
                item.style.borderTopWidth = item.style.borderBottomWidth = 1.5f;
                item.style.borderLeftWidth = item.style.borderRightWidth = 1.5f;
                item.style.borderTopColor = item.style.borderBottomColor =
                    item.style.borderLeftColor = item.style.borderRightColor = new StyleColor(Accent);
            }
            else
            {
                // [144차 P7-CON #21] 미선택 항목(239:713) — stroke #30363D/0.502 1.0 신설.
                ApplyStroke(item);
            }
            item.style.overflow = Overflow.Hidden;

            // 아이콘 36×36 #20262E r6 — [144차 P7-CON #23] 항목로컬 (12,14)(239:715 — 수직중앙 32.5 폐기)
            var icon = new VisualElement { name = "ItemIcon" };
            icon.style.position = Position.Absolute;
            icon.style.left = 12f;
            icon.style.top = 14f;
            icon.style.width = ItemIconSize;
            icon.style.height = ItemIconSize;
            icon.style.backgroundColor = new StyleColor(SlotCol);
            ApplyRadius(icon, RadiusBox);
            icon.pickingMode = PickingMode.Ignore;
            item.Add(icon);

            // 시설명 15/700 + 역할 11/400 #8B949E — [144차 P7-CON #24/#25] (58,15)/(58,35)(239:718/719)
            var nameLabel = MakeLabel(def.displayName, 15f, 700, TextMain, "ItemName");
            nameLabel.style.position = Position.Absolute;
            nameLabel.style.left = 58f;
            nameLabel.style.top = 15f;
            nameLabel.style.right = StatusBadgeW + 28f;
            nameLabel.style.overflow = Overflow.Hidden;
            nameLabel.style.whiteSpace = WhiteSpace.NoWrap;
            item.Add(nameLabel);

            var roleLabel = MakeLabel(FacilityRoleLabel(def.id), 11f, 400, TextSub, "ItemRole");
            roleLabel.style.position = Position.Absolute;
            roleLabel.style.left = 58f;
            roleLabel.style.top = 35f;
            roleLabel.style.right = StatusBadgeW + 28f;
            roleLabel.style.overflow = Overflow.Hidden;
            roleLabel.style.whiteSpace = WhiteSpace.NoWrap;
            item.Add(roleLabel);

            // 상태 배지 76×21 — 건설 가능(#58A6FF/0.078 + check) / 재료 부족(#E5AD65/0.063 + circle-alert)
            // [144차 P7-CON #26] 배지 항목로컬 (332,21) 고정(239:720) — 플로우 배치(0,0) 폐기.
            var itemBadge = BuildStatusBadge(materialsOk && gold >= def.goldCost, materialsOk);
            itemBadge.style.position = Position.Absolute;
            itemBadge.style.left = 332f;
            itemBadge.style.top = 21f;
            item.Add(itemBadge);

            // 재료 행 '나무 B/N · 돌 B/N' 11/400 #8B949E — 보유/필요 실측(요구 0 재료 생략)
            var parts = new List<string>(5);
            if (def.materialWoodCost > 0)
                parts.Add(FormatMaterialPair("나무", heldWood, def.materialWoodCost));
            if (def.materialStoneCost > 0)
                parts.Add(FormatMaterialPair("돌", heldStone, def.materialStoneCost));
            if (def.materialIronCost > 0)
                parts.Add(FormatMaterialPair("철", heldIron, def.materialIronCost));
            if (def.materialLeatherCost > 0)
                parts.Add(FormatMaterialPair("가죽", heldLeather, def.materialLeatherCost));
            if (def.materialManaCost > 0)
                parts.Add(FormatMaterialPair("마력석", heldMana, def.materialManaCost));   // [#30] 표기 '마력석'(239:767)
            var matRow = MakeLabel(parts.Count > 0 ? string.Join(" · ", parts) : "재료 요구 없음", 11f, 400, TextSub, "ItemMaterials");
            // [144차 P7-CON #29] 재료행 항목로컬 (12,60)(239:723 — 아이콘 하단 좌선 정렬) — 구 (60,72) 폐기.
            matRow.style.position = Position.Absolute;
            matRow.style.left = 12f;
            matRow.style.top = 60f;
            matRow.style.right = 16f;
            matRow.style.overflow = Overflow.Hidden;
            matRow.style.whiteSpace = WhiteSpace.NoWrap;
            item.Add(matRow);

            item.RegisterCallback<PointerDownEvent>(_ => OnItemClicked(def.id));
            return item;
        }

        /// <summary>기타(기존 O8 8종) 항목 — 시설 항목 스타일 재사용, 재료 표기 없음/골드만 + 배치 버튼.</summary>
        private VisualElement BuildOtherItem(BlueprintData.BlueprintDef def)
        {
            bool selected = _selectedBlueprintId == def.id;
            var mgr = ConstructionManager.Instance;
            bool installed = false;
            if (mgr != null)
            {
                foreach (var s in mgr.Structures)
                {
                    if (s.blueprintId == def.id && s.isComplete) { installed = true; break; }
                }
            }

            var item = new VisualElement { name = "OtherItem_" + def.id };
            item.style.width = FacilityItemW;
            item.style.minWidth = FacilityItemW;
            item.style.height = FacilityItemH;
            item.style.minHeight = FacilityItemH;
            item.style.flexShrink = 0f;
            item.style.marginBottom = FacilityItemGap;
            item.style.backgroundColor = new StyleColor(selected ? ItemSelectedCol : SurfaceCol);
            ApplyRadius(item, RadiusBox);
            if (selected)
            {
                item.style.borderTopWidth = item.style.borderBottomWidth = 1.5f;
                item.style.borderLeftWidth = item.style.borderRightWidth = 1.5f;
                item.style.borderTopColor = item.style.borderBottomColor =
                    item.style.borderLeftColor = item.style.borderRightColor = new StyleColor(Accent);
            }
            item.style.overflow = Overflow.Hidden;

            var icon = new VisualElement { name = "ItemIcon" };
            icon.style.position = Position.Absolute;
            icon.style.left = 12f;
            icon.style.top = (FacilityItemH - ItemIconSize) * 0.5f;
            icon.style.width = ItemIconSize;
            icon.style.height = ItemIconSize;
            icon.style.backgroundColor = new StyleColor(SlotCol);
            ApplyRadius(icon, RadiusBox);
            icon.pickingMode = PickingMode.Ignore;
            item.Add(icon);

            var nameLabel = MakeLabel(def.displayName, 15f, 700, TextMain, "ItemName");
            nameLabel.style.position = Position.Absolute;
            nameLabel.style.left = 60f;
            nameLabel.style.top = 20f;
            nameLabel.style.right = StatusBadgeW + 28f;
            nameLabel.style.overflow = Overflow.Hidden;
            nameLabel.style.whiteSpace = WhiteSpace.NoWrap;
            item.Add(nameLabel);

            var roleLabel = MakeLabel("기타 · 기존 건설 설계도", 11f, 400, TextSub, "ItemRole");
            roleLabel.style.position = Position.Absolute;
            roleLabel.style.left = 60f;
            roleLabel.style.top = 46f;
            roleLabel.style.right = StatusBadgeW + 28f;
            roleLabel.style.overflow = Overflow.Hidden;
            roleLabel.style.whiteSpace = WhiteSpace.NoWrap;
            item.Add(roleLabel);

            // 상태 배지 — 설치 여부 표기(기타 항목 계약)
            item.Add(BuildInstallBadge(installed));

            // 골드만 표기(재료 표기 없음 — 기타 항목 계약)
            var goldRow = MakeLabel($"💰 {def.goldCost}G · ⏱️ {def.buildTimeSeconds}초", 11f, 400, TextSub, "ItemGold");
            goldRow.style.position = Position.Absolute;
            goldRow.style.left = 60f;
            goldRow.style.top = 72f;
            goldRow.style.right = 16f;
            goldRow.style.overflow = Overflow.Hidden;
            goldRow.style.whiteSpace = WhiteSpace.NoWrap;
            item.Add(goldRow);

            // 배치 버튼 — 기존 OnPlaceBlueprint 흐름
            var placeButton = UTKButton.Create("배치", () => OnPlaceBlueprint(def.id), UTKButton.Variant.Primary);
            placeButton.name = "PlaceButton_" + def.id;
            placeButton.style.position = Position.Absolute;
            placeButton.style.right = 12f;
            placeButton.style.bottom = 10f;
            placeButton.style.width = 64f;
            placeButton.style.height = 26f;
            placeButton.style.fontSize = 11f;
            item.Add(placeButton);

            item.RegisterCallback<PointerDownEvent>(_ => OnItemClicked(def.id));
            return item;
        }

        /// <summary>상태 배지 76×21 r6 — canBuild('건설 가능' #58A6FF/check) / materialsOk-but-gold('골드 부족') / 부족('재료 부족' #E5AD65/alert).</summary>
        private static VisualElement BuildStatusBadge(bool canBuild, bool materialsOk)
        {
            var badge = new VisualElement { name = "StatusBadge" };
            badge.style.width = StatusBadgeW;
            badge.style.height = StatusBadgeH;
            Color textColor;
            VisualElement glyph;
            if (canBuild)
            {
                badge.style.backgroundColor = new StyleColor(AccentBg);
                textColor = Accent;
                glyph = BuildCheckGlyph(12f, Accent);   // [144차 P7-CON #27] 9→12 (239:928 12×12 stroke 1.6)
            }
            else if (materialsOk)
            {
                badge.style.backgroundColor = new StyleColor(WarnBg);
                textColor = Warn;
                glyph = BuildAlertGlyph(12f, Warn);     // [#27] 배지 동일 아이콘 슬롯 — check 규격 준용
            }
            else
            {
                badge.style.backgroundColor = new StyleColor(WarnBg);
                textColor = Warn;
                glyph = BuildAlertGlyph(12f, Warn);     // [#27] 배지 동일 아이콘 슬롯 — check 규격 준용
            }
            ApplyRadius(badge, RadiusBox);
            badge.pickingMode = PickingMode.Ignore;

            glyph.style.position = Position.Absolute;
            glyph.style.left = 8f;                      // [#27] 배지로컬 x8 (구 7 폐기)
            glyph.style.top = (StatusBadgeH - 12f) * 0.5f;
            badge.Add(glyph);

            string text = canBuild ? "건설 가능" : materialsOk ? "골드 부족" : "재료 부족";
            var label = MakeLabel(text, 11f, 500, textColor, "StatusBadgeLabel");
            // [144차 P7-CON #28] 배지로컬 x25(239:722 — 패드 8 + 아이콘 12 + 간격 5) — 구 x18 폐기.
            label.style.position = Position.Absolute;
            label.style.left = 25f;
            label.style.top = 0f;
            label.style.right = 2f;
            label.style.height = StatusBadgeH;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            badge.Add(label);
            return badge;
        }

        /// <summary>설치 여부 배지 76×21 — '설치 완료'(#58A6FF/check) / '미설치'(#20262E 배경).</summary>
        private static VisualElement BuildInstallBadge(bool installed)
        {
            var badge = new VisualElement { name = "InstallBadge" };
            badge.style.width = StatusBadgeW;
            badge.style.height = StatusBadgeH;
            badge.style.backgroundColor = new StyleColor(installed ? AccentBg : SlotCol);
            ApplyRadius(badge, RadiusBox);
            badge.pickingMode = PickingMode.Ignore;

            if (installed)
            {
                var glyph = BuildCheckGlyph(9f, Accent);
                glyph.style.position = Position.Absolute;
                glyph.style.left = 7f;
                glyph.style.top = (StatusBadgeH - 9f) * 0.5f;
                badge.Add(glyph);
            }

            var label = MakeLabel(installed ? "설치 완료" : "미설치", 11f, 500, installed ? Accent : TextSub, "InstallBadgeLabel");
            label.style.position = Position.Absolute;
            label.style.left = installed ? 18f : 10f;
            label.style.top = 0f;
            label.style.right = 2f;
            label.style.height = StatusBadgeH;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            badge.Add(label);
            return badge;
        }

        // ═══════════════ 우 패널 갱신 ═══════════════

        private void RefreshRightPanel()
        {
            (int heldWood, int heldStone, int heldIron, int heldLeather, int heldMana, int gold) = CollectMaterialSnapshot();

            // 미선택/무효 id — 조기 반환(CS0165 definite-assignment 안전 패턴: || 단락 + out var).
            if (string.IsNullOrEmpty(_selectedBlueprintId)
                || !BlueprintData.TryGet(_selectedBlueprintId, out var def))
            {
                _selNameLabel.text = "선택된 시설 없음";
                _selLvLabel.text = "Lv.-";
                _selBadgeHost.Clear();
                _selDescLabel.text = "왼쪽 목록에서 시설을 선택하세요";
                _selUnlockLabel.text = "효과 -";
                _bpBoxLabel.text = "- / LV.-";
                _bpFootprintLabel.text = "-";   // [#50] 미선택 폴백 — '부지 ' 접두어 폐기 정합(239:824)
                _conditionValueLabels[0].text = "-";
                _conditionValueLabels[1].text = "-";
                _conditionValueLabels[2].text = "-";
                RebuildMaterialRows(null);
                RebuildExecuteBox(null, heldWood, heldStone, heldIron, heldLeather, heldMana, gold);
                _checkLineLabel.text = "";
                _checkWarnLabel.style.display = DisplayStyle.None;   // [#79] 미선택 — 병기 라인 숨김
                return;
            }

            // 개요 — 시설명/Lv.1/상태 배지/설명/효과/운영 조건/설계도 박스
            _selNameLabel.text = def.displayName;
            _selLvLabel.text = "Lv.1";
            _selBadgeHost.Clear();
            bool materialsOk = EvaluateMaterialsEnough(def, heldWood, heldStone, heldIron, heldLeather, heldMana);
            _selBadgeHost.Add(BuildStatusBadge(materialsOk && gold >= def.goldCost, materialsOk));
            _selDescLabel.text = string.IsNullOrEmpty(def.description) ? "-" : def.description;
            _selUnlockLabel.text = $"효과  {FacilityUnlockText(def.id)}";
            _bpBoxLabel.text = (IsFacilityBlueprint(def.id) ? "FACILITY / LV.1" : "STRUCTURE / LV.1");
            _bpFootprintLabel.text = FormatFootprint(def.footprintRadius);   // [#50] '{칸} × {칸}' — '부지 ' 접두어 폐기(239:824 '3 × 3')

            // 건설 조건 3카드 — BlueprintDef/가용 병사 실측
            _conditionValueLabels[0].text = FormatBuildTime(def.buildTimeSeconds);
            _conditionValueLabels[1].text = $"{FormatFootprint(def.footprintRadius)} 칸";
            _conditionValueLabels[2].text = FormatWorkforce(CountAvailableGuards());

            RebuildMaterialRows(def);
            RebuildExecuteBox(def, heldWood, heldStone, heldIron, heldLeather, heldMana, gold);
        }

        /// <summary>필요 재료 표 행 재빌드 — 요구 0 재료는 행 생략, 전체 0이면 안내 행(기타 설계도).</summary>
        private void RebuildMaterialRows(BlueprintData.BlueprintDef? defOrNull)
        {
            _materialRows.Clear();

            if (defOrNull == null)
            {
                var empty = MakeLabel("선택된 시설이 없습니다", 12f, 400, TextSub, "MaterialRowsEmpty");
                empty.style.unityTextAlign = TextAnchor.MiddleCenter;
                empty.style.height = MaterialRowH;
                empty.style.minHeight = MaterialRowH;
                _materialRows.Add(empty);
                // [144차 P7-CON #56] 데이터 없음 — 충족 수 라벨 스킵(표기 숨김).
                if (_matSatisfyLabel != null) _matSatisfyLabel.style.display = DisplayStyle.None;
                return;
            }

            var def = defOrNull.Value;
            var rows = new List<(string itemId, string label, int held, int need)>(5);
            foreach (var (itemId, fallback) in SummaryMaterials)
            {
                int need = itemId switch
                {
                    ConstructionManager.MaterialWoodItemId => def.materialWoodCost,
                    ConstructionManager.MaterialStoneItemId => def.materialStoneCost,
                    ConstructionManager.MaterialIronItemId => def.materialIronCost,
                    ConstructionManager.MaterialLeatherItemId => def.materialLeatherCost,
                    ConstructionManager.MaterialManaItemId => def.materialManaCost,
                    _ => 0,
                };
                if (need <= 0) continue;   // 요구 0 = 무관 충족 규약 — 행 미표시
                var item = PlayerInventory.GetItemById(itemId);
                string label = item != null && !string.IsNullOrEmpty(item.displayName) ? item.displayName : fallback;
                int held = PlayerInventory.Instance != null ? PlayerInventory.Instance.GetItemCount(itemId) : 0;
                rows.Add((itemId, label, held, need));
            }

            if (rows.Count == 0)
            {
                var none = MakeLabel($"재료 요구 없음 — 골드 {def.goldCost}G만으로 건설됩니다", 12f, 400, TextSub, "MaterialRowsNone");
                none.style.unityTextAlign = TextAnchor.MiddleCenter;
                none.style.height = MaterialRowH;
                none.style.minHeight = MaterialRowH;
                _materialRows.Add(none);
                // [144차 P7-CON #56] 재료 요구 없음 — 충족 수 분모 0 → 라벨 스킵(표기 숨김).
                if (_matSatisfyLabel != null) _matSatisfyLabel.style.display = DisplayStyle.None;
                return;
            }

            foreach (var (itemId, label, held, need) in rows)
                _materialRows.Add(BuildMaterialRow(itemId, label, held, need));

            // [144차 P7-CON #56] 충족 수 "N / M" — 실데이터(충족 종류 수/요구 종류 수). Figma '3 / 4'는
            // 동일 산출식의 목업값 — 고정값 대신 행별 IsMaterialSufficient 실측 합산.
            int satisfiedCount = 0;
            foreach (var row in rows)
                if (IsMaterialSufficient(row.held, row.need)) satisfiedCount++;
            if (_matSatisfyLabel != null)
            {
                _matSatisfyLabel.text = $"{satisfiedCount} / {rows.Count}";
                _matSatisfyLabel.style.display = DisplayStyle.Flex;
            }
        }

        /// <summary>재료 행 920×64 — 아이콘 32×32+이름 13/500 / 보유 14/400 / 필요 14/400 #8B949E / 잔여 13/400 / 충족 상태.</summary>
        private static VisualElement BuildMaterialRow(string itemId, string label, int held, int need)
        {
            int shortage = ComputeShortage(held, need);
            bool sufficient = IsMaterialSufficient(held, need);

            var row = new VisualElement { name = "MaterialRow_" + itemId };
            row.style.width = MaterialTableW;
            row.style.minWidth = MaterialTableW;
            row.style.height = MaterialRowH;
            row.style.minHeight = MaterialRowH;
            row.style.flexShrink = 0f;
            row.style.backgroundColor = new StyleColor(sufficient ? SurfaceCol : WarnBg);   // [#61] 충분 행 fill #191F27(239:854) — 투명 폐기
            if (sufficient)
            {
                // [144차 P7-CON #61] 충분 행(239:854) — stroke #30363D/0.502 신설(무보더 폐기, UTKTheme V2 크롬)
                UTKTheme.ApplyElementBorder(row);
            }
            else
            {
                // 부족 행 — bg #E5AD65/0.063 + stroke 0.22
                row.style.borderTopWidth = row.style.borderBottomWidth = 1f;
                row.style.borderLeftWidth = row.style.borderRightWidth = 1f;
                var warnStroke = new Color(0xE5 / 255f, 0xAD / 255f, 0x65 / 255f, WarnStrokeAlpha);
                row.style.borderTopColor = row.style.borderBottomColor =
                    row.style.borderLeftColor = row.style.borderRightColor = new StyleColor(warnStroke);
            }
            row.style.overflow = Overflow.Hidden;

            // 아이콘 32×32 — 실측 아이템 아이콘(베이크 전/부재 시 박스만)
            var icon = new VisualElement { name = "RowIcon" };
            icon.style.position = Position.Absolute;
            icon.style.left = 14f;
            icon.style.top = (MaterialRowH - MatIconSize) * 0.5f;
            icon.style.width = MatIconSize;
            icon.style.height = MatIconSize;
            icon.style.backgroundColor = new StyleColor(SlotCol);
            ApplyRadius(icon, RadiusBox);
            icon.pickingMode = PickingMode.Ignore;
            var itemData = PlayerInventory.GetItemById(itemId);
            Texture2D iconTex = itemData != null ? ItemIconDatabase.GetOrCreateIcon(itemData) : null;
            if (iconTex != null)
            {
                icon.style.backgroundImage = UTKTextureSafe.ToBackground(iconTex);
                icon.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            }
            row.Add(icon);

            var nameLabel = MakeLabel(label, 13f, 500, TextMain, "RowName");
            nameLabel.style.position = Position.Absolute;
            nameLabel.style.left = 56f;
            nameLabel.style.top = (MaterialRowH - 18f) * 0.5f;
            row.Add(nameLabel);

            // [144차 P7-CON #65] 보유량 열 x — Figma 239:859 열 x 374(ColXHeld 재매핑 #59)
            var heldLabel = MakeLabel($"{Mathf.Max(held, 0)}", 14f, 400, TextMain, "RowHeld");
            heldLabel.style.position = Position.Absolute;
            heldLabel.style.left = ColXHeld;
            heldLabel.style.top = (MaterialRowH - 18f) * 0.5f;
            row.Add(heldLabel);

            // [144차 P7-CON #66] 필요량 열 x — Figma 239:860 열 x 490(ColXNeed 재매핑 #59)
            var needLabel = MakeLabel($"{Mathf.Max(need, 0)}", 14f, 400, TextSub, "RowNeed");
            needLabel.style.position = Position.Absolute;
            needLabel.style.left = ColXNeed;
            needLabel.style.top = (MaterialRowH - 18f) * 0.5f;
            row.Add(needLabel);

            // 건설 후 잔여 — 부족 시 '—' #586473
            // [144차 P7-CON #67] 잔여 열 x — Figma 239:861 열 x 606(ColXRemain 재매핑 #59)
            var remainLabel = MakeLabel(sufficient ? $"{held - need}" : "—", 13f, 400, sufficient ? TextMain : TextDim, "RowRemain");
            remainLabel.style.position = Position.Absolute;
            remainLabel.style.left = ColXRemain;
            remainLabel.style.top = (MaterialRowH - 17f) * 0.5f;
            row.Add(remainLabel);

            // 충족 상태 — check '충분' #58A6FF / circle-alert 'N개 부족' #E5AD65
            var statusHost = new VisualElement { name = "RowStatus" };
            statusHost.style.position = Position.Absolute;
            statusHost.style.left = ColXStatus;
            statusHost.style.top = (MaterialRowH - 15f) * 0.5f;   // [#68] 상태 셀 보정 — glyph 13/라벨 11px
            statusHost.style.flexDirection = FlexDirection.Row;
            statusHost.style.alignItems = Align.Center;
            statusHost.pickingMode = PickingMode.Ignore;
            // [144차 P7-CON #68] 재료 충족 상태(239:862) — check 13×13 + 11/500 gap6(구 10×10 13/400 gap5 폐기).
            //   열 x는 ColXStatus=752(#59 재매핑)로 정합.
            var statusGlyph = sufficient ? BuildCheckGlyph(13f, Accent) : BuildAlertGlyph(13f, Warn);
            statusGlyph.style.marginRight = 6f;
            statusHost.Add(statusGlyph);
            statusHost.Add(MakeLabel(sufficient ? "충분" : $"{shortage}개 부족", 11f, 500, sufficient ? Accent : Warn, "RowStatusLabel"));
            row.Add(statusHost);

            return row;
        }

        /// <summary>건설 실행 박스 920×106 — 충족(건설하기)/부족(사유+획득처+불가) 상태별 재빌드.</summary>
        private void RebuildExecuteBox(
            BlueprintData.BlueprintDef? defOrNull,
            int heldWood, int heldStone, int heldIron, int heldLeather, int heldMana, int gold)
        {
            _executeBox.Clear();

            if (defOrNull == null)
            {
                _executeBox.style.backgroundColor = new StyleColor(SurfaceCol);
                ApplyExecuteStroke(_executeBox, TextDim);
                var hint = MakeLabel("시설을 선택하면 건설 실행 버튼이 활성화됩니다", 13f, 400, TextSub, "ExecuteHint");
                hint.style.position = Position.Absolute;
                hint.style.left = 0f;
                hint.style.right = 0f;
                hint.style.top = 0f;
                hint.style.bottom = 0f;
                hint.style.unityTextAlign = TextAnchor.MiddleCenter;
                _executeBox.Add(hint);
                _checkLineLabel.text = "";
                return;
            }

            var def = defOrNull.Value;
            bool materialsOk = EvaluateMaterialsEnough(def, heldWood, heldStone, heldIron, heldLeather, heldMana);
            bool canBuild = materialsOk && gold >= def.goldCost;

            if (canBuild)
            {
                // 충족 — bg #58A6FF/0.078 + stroke 0.25 + '건설하기' 224×42 #58A6FF r6(글자 #0B0E14)
                _executeBox.style.backgroundColor = new StyleColor(AccentBg);
                ApplyExecuteStroke(_executeBox, new Color(0x58 / 255f, 0xA6 / 255f, 0xFF / 255f, AccentStrokeAlpha));

                var buildButton = new Button(OnBuildButtonClicked) { name = "BuildButton", text = "" };
                buildButton.style.position = Position.Absolute;
                // [105차 C] Figma 239:683 '건설하기': 버튼 슬롯 우측 고정 — execute-local x=680(=920-224-16), y=16
                buildButton.style.left = 680f;
                buildButton.style.top = 16f;
                buildButton.style.width = BuildButtonW;
                buildButton.style.height = BuildButtonH;
                buildButton.style.backgroundColor = new StyleColor(Accent);
                ApplyRadius(buildButton, RadiusBox);
                buildButton.style.paddingLeft = buildButton.style.paddingRight = 0f;
                buildButton.style.paddingTop = buildButton.style.paddingBottom = 0f;
                var buildLabel = MakeLabel("건설하기", 13f, 700, BtnTextDark, "BuildButtonLabel");
                buildLabel.style.position = Position.Absolute;
                buildLabel.style.left = 0f;
                buildLabel.style.right = 0f;
                buildLabel.style.top = 0f;
                buildLabel.style.bottom = 0f;
                buildLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                buildButton.Add(buildLabel);
                _executeBox.Add(buildButton);

                _checkLineLabel.text = "✓ 부지 확보     ✓ 작업 인원 충분";
                _checkLineLabel.style.color = new StyleColor(TextSub);
                _checkWarnLabel.style.display = DisplayStyle.None;   // [#79] 충족 — 병기 라인 숨김
                return;
            }

            // 부족 — bg #E5AD65/0.063 + stroke 0.22 + 사유/해결 방법/획득처/불가 버튼
            _executeBox.style.backgroundColor = new StyleColor(WarnBg);
            ApplyExecuteStroke(_executeBox, new Color(0xE5 / 255f, 0xAD / 255f, 0x65 / 255f, WarnStrokeAlpha));

            var lacking = CollectLackingItemIds(def, heldWood, heldStone, heldIron, heldLeather, heldMana);
            string reason;
            if (!materialsOk)
            {
                var names = new List<string>(lacking.Count);
                var shorts = new List<int>(lacking.Count);
                foreach (string itemId in lacking)
                {
                    int need = ItemNeed(def, itemId);
                    int held = PlayerInventory.Instance != null ? PlayerInventory.Instance.GetItemCount(itemId) : 0;
                    var item = PlayerInventory.GetItemById(itemId);
                    names.Add(item != null && !string.IsNullOrEmpty(item.displayName) ? item.displayName : itemId);
                    shorts.Add(ComputeShortage(held, need));
                }
                reason = FormatShortfallReason(names.ToArray(), shorts.ToArray());
            }
            else
            {
                reason = $"골드 {def.goldCost - gold}G가 더 필요합니다";
            }

            // '재료 획득처' 108×42 #58A6FF/0.078 r6 — 클릭 → 획득처 안내 토스트(재료 부족 시만)
            if (!materialsOk)
            {
                var sourceButton = new Button(OnSourceHintClicked) { name = "SourceHintButton", text = "" };
                sourceButton.style.position = Position.Absolute;
                // [105차 C] Figma: 획득처 버튼 execute-local x=552, y=16(구 좌측 24·수직중앙 폐기)
                sourceButton.style.left = 552f;
                sourceButton.style.top = 16f;
                sourceButton.style.width = SourceButtonW;
                sourceButton.style.height = SourceButtonH;
                sourceButton.style.backgroundColor = new StyleColor(AccentBg);
                ApplyRadius(sourceButton, RadiusBox);
                // [144차 P7-CON #73] 획득처 버튼 stroke(239:906) — #58A6FF/0.251 1px 신설(테두리 누락 수리)
                UTKTheme.ApplyElementBorder(sourceButton,
                    new Color(0x58 / 255f, 0xA6 / 255f, 0xFF / 255f, AccentStrokeAlpha), 1f);
                sourceButton.style.paddingLeft = sourceButton.style.paddingRight = 0f;
                sourceButton.style.paddingTop = sourceButton.style.paddingBottom = 0f;
                var sourceLabel = MakeLabel("재료 획득처", 12f, 500, Accent, "SourceHintLabel");
                sourceLabel.style.position = Position.Absolute;
                sourceLabel.style.left = 0f;
                sourceLabel.style.right = 0f;
                sourceLabel.style.top = 0f;
                sourceLabel.style.bottom = 0f;
                sourceLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                sourceButton.Add(sourceLabel);
                _executeBox.Add(sourceButton);
            }

            // [105차 C] Figma '부족 안내': 사유 텍스트 execute-local (38,19.5) — 아이콘(16~31) 뒤 7px 간격, 영역 우단 532(획득처 버튼 552 앞 20px)
            // [144차 P7-CON #70] 부족 안내 circle-alert(239:1012) — 15×15 stroke #E5AD65 1.6 @실행로컬(16,21).
            //   구 코드는 사유 텍스트만 — 기존 BuildAlertGlyph(원 테두리+느낌표) 15px 근사. 텍스트 (38,19.5) 기존 유지.
            var shortageIcon = BuildAlertGlyph(15f, Warn);
            shortageIcon.style.position = Position.Absolute;
            shortageIcon.style.left = 16f;
            shortageIcon.style.top = 21f;
            _executeBox.Add(shortageIcon);
            var reasonLabel = MakeLabel(reason, 14f, 700, TextMain, "ShortageReason");
            reasonLabel.style.position = Position.Absolute;
            reasonLabel.style.left = 38f;
            reasonLabel.style.top = 19.5f;
            reasonLabel.style.right = 388f;
            reasonLabel.style.overflow = Overflow.Hidden;
            reasonLabel.style.whiteSpace = WhiteSpace.NoWrap;
            _executeBox.Add(reasonLabel);

            // [105차 C] Figma '해결 방법' execute-local (16,41.5) — 아이콘 좌선 정렬
            // [144차 P7-CON #72] 정적 문구 폐기 — Figma 239:905 '철광산에서 재료를 확보하면 건설할 수 있습니다.'
            //   는 동적 예시 → 최우선 부족 재료의 실측 획득처(FormatMaterialSourceHint 단일소스)로 문장 조립.
            //   골드 부족 분기는 게임 전용(기록 유지)으로 문형만 정합.
            string resolveText = materialsOk
                ? "골드를 확보하면 건설할 수 있습니다"
                : FormatResolveHint(lacking.Count > 0 ? lacking[0] : null);
            var resolveLabel = MakeLabel(resolveText, 11f, 400, TextSub, "ResolveHint");
            resolveLabel.style.position = Position.Absolute;
            resolveLabel.style.left = 16f;
            resolveLabel.style.top = 41.5f;
            resolveLabel.style.right = 388f;
            resolveLabel.style.overflow = Overflow.Hidden;
            resolveLabel.style.whiteSpace = WhiteSpace.NoWrap;
            _executeBox.Add(resolveLabel);

            // '건설 불가 · 재료 부족' 224×42 #252D38 r6 + lock #586473
            var disabledButton = new VisualElement { name = "DisabledBuildButton" };
            disabledButton.style.position = Position.Absolute;
            // [105차 C] Figma: 동일 슬롯(우측 16 여백, 상단 16) — 구 right=24·수직중앙 폐기
            disabledButton.style.right = 16f;
            disabledButton.style.top = 16f;
            disabledButton.style.width = BuildButtonW;
            disabledButton.style.height = BuildButtonH;
            disabledButton.style.backgroundColor = new StyleColor(DisabledBoxCol);
            ApplyRadius(disabledButton, RadiusBox);
            UTKTheme.ApplyElementBorder(disabledButton);   // [144차 P7-CON #75] 비활성 버튼 stroke(239:909) — #30363D/0.502 신설
            // [144차 P7-CON #76] lock-keyhole(239:1018) — 17×17 @버튼로컬 (42,13)(구 12 @(74,15) 폐기)
            var lockGlyph = BuildLockGlyph(17f);
            lockGlyph.style.position = Position.Absolute;
            lockGlyph.style.left = 42f;
            lockGlyph.style.top = 13f;
            disabledButton.Add(lockGlyph);
            var disabledLabel = MakeLabel(
                materialsOk ? "건설 불가 · 골드 부족" : "건설 불가 · 재료 부족",
                13f, 700, TextDim, "DisabledBuildLabel");   // [144차 P7-CON #77] 13/700 #586473(239:911) — 구 12/500 폐기
            disabledLabel.style.position = Position.Absolute;
            disabledLabel.style.left = 62f;   // [#77] lock(42..59) 뒤 3px 간격 — 구 30은 락과 겹침
            disabledLabel.style.right = 8f;
            disabledLabel.style.top = 0f;
            disabledLabel.style.height = BuildButtonH;
            disabledLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            disabledButton.Add(disabledLabel);
            _executeBox.Add(disabledButton);

            // [144차 P7-CON #79] 충족/미충족 병기(239:914) — ✓ 라인(#8B949E) 유지 + '! …'(#E5AD65) 우측 병기.
            //   '! 골드 부족' 분기는 게임 전용(Figma 미등재 — 기록 유지).
            int lackingKinds = materialsOk ? 0 : LackingKindCount(def, heldWood, heldStone, heldIron, heldLeather, heldMana);
            _checkLineLabel.text = "✓ 부지 확보     ✓ 작업 인원 충분";
            _checkLineLabel.style.color = new StyleColor(TextSub);
            _checkWarnLabel.text = materialsOk ? "! 골드 부족" : $"! 재료 {lackingKinds}종 부족";
            _checkWarnLabel.style.display = DisplayStyle.Flex;
        }

        // ═══════════════ 순수 표시 헬퍼 (EditMode 테스트 계약 — public static) ═══════════════

        /// <summary>건설 시간 'mm:ss' — BlueprintDef.buildTimeSeconds 실측 (90 → "01:30").</summary>
        public static string FormatBuildTime(int seconds)
        {
            int s = Mathf.Max(seconds, 0);
            return $"{s / 60:00}:{s % 60:00}";
        }

        /// <summary>필요 부지 '{N} × {N}' — footprintRadius 실측에서 산출(반경 m → 칸 수 절상). Figma '3 × 3' 형식.</summary>
        public static string FormatFootprint(float footprintRadius)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(footprintRadius));
            return $"{n} × {n}";
        }

        /// <summary>필요 작업 인원 '1 명 · 유휴 인원 N명' — N = 가용 병사 실측(CountAvailableGuards).</summary>
        public static string FormatWorkforce(int availableGuards)
            => $"1 명 · 유휴 인원 {Mathf.Max(availableGuards, 0)}명";

        /// <summary>재료 부족 수 — 요구 ≤ 0 = 무관(0 반환 규약).</summary>
        public static int ComputeShortage(int held, int need)
            => need <= 0 ? 0 : Mathf.Max(need - Mathf.Max(held, 0), 0);

        /// <summary>충족 판정 — 요구 ≤ 0 = 보유 무관 충족(ConstructionManager 10인자 규약 동일).</summary>
        public static bool IsMaterialSufficient(int held, int need) => need <= 0 || held >= need;

        /// <summary>재료 행 한 쌍 '나무 B/N' — 보유/필요 실측.</summary>
        public static string FormatMaterialPair(string label, int held, int need)
            => $"{label} {Mathf.Max(held, 0)}/{Mathf.Max(need, 0)}";

        /// <summary>부족 사유 '{재료} {N}개가 더 필요합니다' — 복수 재료는 ' · ' 연결.</summary>
        public static string FormatShortfallReason(string[] materialNames, int[] shortages)
        {
            if (materialNames == null || materialNames.Length == 0)
                return "재료가 더 필요합니다";
            var parts = new List<string>(materialNames.Length);
            for (int i = 0; i < materialNames.Length; i++)
            {
                int n = shortages != null && i < shortages.Length ? Mathf.Max(shortages[i], 1) : 1;
                parts.Add($"{materialNames[i]} {n}개");
            }
            return $"{string.Join(" · ", parts)}가 더 필요합니다";
        }

        /// <summary>재료 획득처 안내 문구 — 실측 아이템 description 기반(채광/벌목/사냥 처). 토스트 1행.</summary>
        public static string FormatMaterialSourceHint(string itemId) => itemId switch
        {
            ConstructionManager.MaterialWoodItemId => "통나무: 벌목 — 나무 오브젝트 채집",
            ConstructionManager.MaterialStoneItemId => "석재: 채광 — 바위 오브젝트 채집",
            ConstructionManager.MaterialIronItemId => "철광석: 채광(철광 광맥)",
            ConstructionManager.MaterialLeatherItemId => "멧돼지 가죽: 멧돼지 사냥",
            // [#30] 이 행의 '수정석' 표기 유지 — ConstructionManagementFigmaTests 353행이 토스트 문구에
            // '수정석' 포함을 고정(문자열 계약). 목록/요약 표기는 '마력석'(239:767/955)으로 통일.
            ConstructionManager.MaterialManaItemId => "수정석: 광맥 채광(수정)",
            _ => itemId,
        };

        /// <summary>
        /// 해결 방법 동적 문구 — Figma 239:905 '철광산에서 재료를 확보하면 건설할 수 있습니다.'의 실측 구현
        /// ([144차 P7-CON #72]). 최우선 부족 재료의 획득처(FormatMaterialSourceHint 실측 단일소스)를 문두에
        /// 병기 — 신규 지명 창작 금지 규약(위조 방지). 정적 문구 '해결 방법 — 재료 획득처 버튼에서…' 폐기.
        /// </summary>
        public static string FormatResolveHint(string firstLackingItemId)
        {
            if (string.IsNullOrEmpty(firstLackingItemId))
                return "재료를 확보하면 건설할 수 있습니다";
            return $"{FormatMaterialSourceHint(firstLackingItemId)} — 재료를 확보하면 건설할 수 있습니다";
        }

        /// <summary>획득처 토스트 문구 — 부족 재료 목록을 ' / ' 연결.</summary>
        public static string BuildMaterialSourceToast(IList<string> lackingItemIds)
        {
            if (lackingItemIds == null || lackingItemIds.Count == 0)
                return "부족한 재료가 없습니다";
            var parts = new List<string>(lackingItemIds.Count);
            foreach (string id in lackingItemIds)
                parts.Add(FormatMaterialSourceHint(id));
            return string.Join(" / ", parts);
        }

        /// <summary>시설 설계도 여부 — 시설 6종(bp_cooking/bp_forge/bp_restaurant/bp_barracks/bp_alchemy/bp_traininghall)이면 true. [152차 P0]</summary>
        public static bool IsFacilityBlueprint(string blueprintId)
            => !string.IsNullOrEmpty(blueprintId) && System.Array.IndexOf(FacilityIds, blueprintId) >= 0;

        /// <summary>재료/골드 종합 건설 가능 판정 — HasEnoughMaterials 10인자 + 골드(시공 착수 비용) 실측.</summary>
        public static bool EvaluateBuildable(
            BlueprintData.BlueprintDef def, int wood, int stone, int iron, int leather, int mana, int gold)
        {
            if (string.IsNullOrEmpty(def.id)) return false;
            return EvaluateMaterialsEnough(def, wood, stone, iron, leather, mana) && gold >= def.goldCost;
        }

        /// <summary>재료 충족 판정 — ConstructionManager.HasEnoughMaterials 10인자 단일소스(위조 금지).</summary>
        public static bool EvaluateMaterialsEnough(
            BlueprintData.BlueprintDef def, int wood, int stone, int iron, int leather, int mana)
        {
            return ConstructionManager.HasEnoughMaterials(
                wood, stone, iron, leather, mana,
                def.materialWoodCost, def.materialStoneCost, def.materialIronCost,
                def.materialLeatherCost, def.materialManaCost);
        }

        /// <summary>가용(유휴) 병사 수 — 모집됨(IsRecruited)+생존(IsAlive) 실측. GuardManager 부재 시 0(안전 무동작).</summary>
        public static int CountAvailableGuards()
        {
            var gm = GuardManager.Instance;
            if (gm == null) return 0;
            var guards = gm.GetAllPlayerGuards();
            if (guards == null) return 0;
            int count = 0;
            foreach (var guard in guards)
            {
                if (guard == null || !guard.IsRecruited || !guard.IsAlive) continue;
                count++;
            }
            return count;
        }

        // ═══════════════ 내부 헬퍼 ═══════════════

        /// <summary>보유 재료/골드 스냅샷 — GetItemCount/PlayerStats 실측 1회 수집(파생 갱신 공용).</summary>
        private static (int wood, int stone, int iron, int leather, int mana, int gold) CollectMaterialSnapshot()
        {
            var inv = PlayerInventory.Instance;
            var stats = PlayerStats.Instance;
            return (
                inv != null ? inv.GetItemCount(ConstructionManager.MaterialWoodItemId) : 0,
                inv != null ? inv.GetItemCount(ConstructionManager.MaterialStoneItemId) : 0,
                inv != null ? inv.GetItemCount(ConstructionManager.MaterialIronItemId) : 0,
                inv != null ? inv.GetItemCount(ConstructionManager.MaterialLeatherItemId) : 0,
                inv != null ? inv.GetItemCount(ConstructionManager.MaterialManaItemId) : 0,
                stats != null ? stats.Gold : 0);
        }

        /// <summary>부족 재료 id 목록 — 요구 0 재료 제외, Figma 재료 순서(나무→돌→철→가죽→마력석)[#30 표기].</summary>
        private static List<string> CollectLackingItemIds(
            BlueprintData.BlueprintDef def, int wood, int stone, int iron, int leather, int mana)
        {
            var result = new List<string>(5);
            void AddIfLacking(string itemId, int heldCount, int needCount)
            {
                if (needCount > 0 && heldCount < needCount) result.Add(itemId);
            }
            AddIfLacking(ConstructionManager.MaterialWoodItemId, wood, def.materialWoodCost);
            AddIfLacking(ConstructionManager.MaterialStoneItemId, stone, def.materialStoneCost);
            AddIfLacking(ConstructionManager.MaterialIronItemId, iron, def.materialIronCost);
            AddIfLacking(ConstructionManager.MaterialLeatherItemId, leather, def.materialLeatherCost);
            AddIfLacking(ConstructionManager.MaterialManaItemId, mana, def.materialManaCost);
            return result;
        }

        /// <summary>부족 재료 종수 — '재료 N종 부족' 표시용.</summary>
        private static int LackingKindCount(
            BlueprintData.BlueprintDef def, int wood, int stone, int iron, int leather, int mana)
        {
            int count = 0;
            if (def.materialWoodCost > 0 && wood < def.materialWoodCost) count++;
            if (def.materialStoneCost > 0 && stone < def.materialStoneCost) count++;
            if (def.materialIronCost > 0 && iron < def.materialIronCost) count++;
            if (def.materialLeatherCost > 0 && leather < def.materialLeatherCost) count++;
            if (def.materialManaCost > 0 && mana < def.materialManaCost) count++;
            return count;
        }

        /// <summary>설계도의 해당 재료 필요량 — id 매핑(요구 0 = 무관).</summary>
        private static int ItemNeed(BlueprintData.BlueprintDef def, string itemId) => itemId switch
        {
            ConstructionManager.MaterialWoodItemId => def.materialWoodCost,
            ConstructionManager.MaterialStoneItemId => def.materialStoneCost,
            ConstructionManager.MaterialIronItemId => def.materialIronCost,
            ConstructionManager.MaterialLeatherItemId => def.materialLeatherCost,
            ConstructionManager.MaterialManaItemId => def.materialManaCost,
            _ => 0,
        };

        private static string DescribeBlueprint(string blueprintId)
        {
            return BlueprintData.TryGet(blueprintId, out var def)
                ? def.displayName
                : (string.IsNullOrEmpty(blueprintId) ? "(알 수 없음)" : blueprintId);
        }

        /// <summary>패널 닫기 버튼 30×30 — [144차 P7-CON #12] fill #191F27 + stroke #30363D/0.502 r6,
        /// X 절차 글리프 15×15 중앙(239:805/806 — stroke #8B949E). 창 닫기(Hide) — 인터랙션 무손상.</summary>
        private Button BuildPanelCloseButton(float panelLocalX, float y, string buttonName)
        {
            var closeButton = new Button(Hide) { name = buttonName, text = "" };
            closeButton.style.position = Position.Absolute;
            closeButton.style.left = panelLocalX;
            closeButton.style.top = y;
            closeButton.style.width = 30f;
            closeButton.style.height = 30f;
            closeButton.style.backgroundColor = new StyleColor(SurfaceCol);   // [#12] #20262E→#191F27
            ApplyRadius(closeButton, RadiusBox);
            ApplyStroke(closeButton);                                          // [#12] 테두리 신설(239:805)
            closeButton.style.paddingLeft = closeButton.style.paddingRight = 0f;
            closeButton.style.paddingTop = closeButton.style.paddingBottom = 0f;
            var xGlyph = BuildCloseGlyph(15f);                                 // [#12] 10→15 (239:806 15×15)
            xGlyph.style.position = Position.Absolute;
            xGlyph.style.left = 7.5f;                                          // 30×30 중앙(mainAlign CENTER)
            xGlyph.style.top = 7.5f;
            closeButton.Add(xGlyph);
            return closeButton;
        }

        /// <summary>실행 박스 보더 — 충족 #58A6FF/0.25 / 부족 #E5AD65/0.22.</summary>
        private static void ApplyExecuteStroke(VisualElement ve, Color strokeColor)
        {
            ve.style.borderTopWidth = ve.style.borderBottomWidth = 1f;
            ve.style.borderLeftWidth = ve.style.borderRightWidth = 1f;
            ve.style.borderTopColor = ve.style.borderBottomColor =
                ve.style.borderLeftColor = ve.style.borderRightColor = new StyleColor(strokeColor);
        }

        // ═══════════════ 키 토글 / 폴링용 Updater — 기존 O8 패턴 유지 ═══════════════

        private class Updater : MonoBehaviour
        {
            public ConstructionWindowUTK window;

            private void Update()
            {
                // 부트스트랩 완료 전이어도 최초 부착을 멱등으로 시도
                var root = UIToolkitBootstrap.UIRoot;
                if (root != null && window != null && window.parent == null)
                    root.Add(window);

                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && window != null)
                {
                    // [O8 C-O8-04] U키 — [91차 F1] 건설 관리 창 전용 단일화(GameStatsUTK는 O키로 이동).
                    if (kb.uKey.wasPressedThisFrame) ConstructionWindowUTK.Toggle();
                    if (kb.escapeKey.wasPressedThisFrame && window.IsOpen) window.Close();
                }

                if (window == null || !window.IsOpen) return;
                window._refreshTimer += Time.unscaledDeltaTime;
                if (window._refreshTimer < RefreshInterval) return;
                window._refreshTimer = 0f;
                window.OnUpdaterPoll();
            }

            private void OnDestroy()
            {
                if (window != null)
                {
                    window.UnsubscribeStructureCompleted();
                    window.RemoveFromHierarchy();
                }
            }
        }

        /// <summary>Updater 폴링 경로 — 캔버스핏 가드 재적용 + 전체 갱신.</summary>
        private void OnUpdaterPoll()
        {
            ApplyCanvasFit();
            Refresh();
        }

        // ═══════════════ 절차 아이콘(벡터 이미지 미사용 — rect/circle 구성, 88차 선례) ═══════════════

        /// <summary>hammer 18×18 — 머리 사각 + 손잡이(45° 회전).</summary>
        private static VisualElement BuildHammerGlyph(float size)
        {
            var host = new VisualElement { name = "HammerGlyph" };
            host.style.width = size;
            host.style.height = size;
            host.pickingMode = PickingMode.Ignore;
            var head = new VisualElement();
            head.style.width = size * 0.52f;
            head.style.height = size * 0.3f;
            head.style.backgroundColor = new StyleColor(TextSub);
            head.style.position = Position.Absolute;
            head.style.left = size * 0.06f;
            head.style.top = size * 0.08f;
            head.pickingMode = PickingMode.Ignore;
            host.Add(head);
            var handle = new VisualElement();
            handle.style.width = size * 0.18f;
            handle.style.height = size * 0.62f;
            handle.style.backgroundColor = new StyleColor(TextSub);
            handle.style.position = Position.Absolute;
            handle.style.left = size * 0.5f;
            handle.style.top = size * 0.24f;
            handle.style.rotate = new Rotate(new Angle(-45f, AngleUnit.Degree));
            handle.pickingMode = PickingMode.Ignore;
            host.Add(handle);
            return host;
        }

        /// <summary>blocks 23×23 — 사각 3개(목록 표시 근사).</summary>
        private static VisualElement BuildBlocksGlyph(float size)
        {
            var host = new VisualElement { name = "BlocksGlyph" };
            host.style.width = size;
            host.style.height = size;
            host.pickingMode = PickingMode.Ignore;
            (float x, float y, float s)[] blocks =
            {
                (0.06f, 0.06f, 0.4f), (0.54f, 0.06f, 0.4f), (0.06f, 0.54f, 0.4f),
            };
            foreach (var (bx, by, bs) in blocks)
            {
                var block = new VisualElement();
                block.style.width = size * bs;
                block.style.height = size * bs;
                block.style.backgroundColor = new StyleColor(TextSub);
                block.style.position = Position.Absolute;
                block.style.left = size * bx;
                block.style.top = size * by;
                block.pickingMode = PickingMode.Ignore;
                host.Add(block);
            }
            var accentBlock = new VisualElement();
            accentBlock.style.width = size * 0.4f;
            accentBlock.style.height = size * 0.4f;
            accentBlock.style.backgroundColor = new StyleColor(Accent);
            accentBlock.style.position = Position.Absolute;
            accentBlock.style.left = size * 0.54f;
            accentBlock.style.top = size * 0.54f;
            accentBlock.pickingMode = PickingMode.Ignore;
            host.Add(accentBlock);
            return host;
        }

        /// <summary>clock 14×14 — 원 테두리 + 시침(88차 선례).</summary>
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

        /// <summary>check 10×10 — ✓ 2막대(상태 배지/충족 열).</summary>
        private static VisualElement BuildCheckGlyph(float size, Color color)
        {
            var host = new VisualElement { name = "CheckGlyph" };
            host.style.width = size;
            host.style.height = size;
            host.pickingMode = PickingMode.Ignore;
            var shortBar = new VisualElement();
            shortBar.style.width = size * 0.42f;
            shortBar.style.height = 1.6f;
            shortBar.style.backgroundColor = new StyleColor(color);
            shortBar.style.position = Position.Absolute;
            shortBar.style.left = 0f;
            shortBar.style.top = size * 0.6f;
            shortBar.style.rotate = new Rotate(new Angle(45f, AngleUnit.Degree));
            shortBar.pickingMode = PickingMode.Ignore;
            host.Add(shortBar);
            var longBar = new VisualElement();
            longBar.style.width = size * 0.78f;
            longBar.style.height = 1.6f;
            longBar.style.backgroundColor = new StyleColor(color);
            longBar.style.position = Position.Absolute;
            longBar.style.left = size * 0.24f;
            longBar.style.top = size * 0.52f;
            longBar.style.rotate = new Rotate(new Angle(-45f, AngleUnit.Degree));
            longBar.pickingMode = PickingMode.Ignore;
            host.Add(longBar);
            return host;
        }

        /// <summary>circle-alert 10×10 — 원 테두리 + 느낌표(재료 부족 배지/부족 행).</summary>
        private static VisualElement BuildAlertGlyph(float size, Color color)
        {
            var host = new VisualElement { name = "AlertGlyph" };
            host.style.width = size;
            host.style.height = size;
            host.pickingMode = PickingMode.Ignore;
            var circle = new VisualElement();
            circle.style.width = size;
            circle.style.height = size;
            circle.style.borderTopLeftRadius = circle.style.borderTopRightRadius = size * 0.5f;
            circle.style.borderBottomLeftRadius = circle.style.borderBottomRightRadius = size * 0.5f;
            circle.style.borderTopWidth = circle.style.borderBottomWidth =
                circle.style.borderLeftWidth = circle.style.borderRightWidth = 1.2f;
            circle.style.borderTopColor = circle.style.borderBottomColor =
                circle.style.borderLeftColor = circle.style.borderRightColor = new StyleColor(color);
            circle.style.backgroundColor = new StyleColor(Color.clear);
            circle.style.position = Position.Absolute;
            circle.pickingMode = PickingMode.Ignore;
            host.Add(circle);
            var bar = new VisualElement();
            bar.style.width = 1.4f;
            bar.style.height = size * 0.36f;
            bar.style.backgroundColor = new StyleColor(color);
            bar.style.position = Position.Absolute;
            bar.style.left = size * 0.5f - 0.7f;
            bar.style.top = size * 0.18f;
            bar.pickingMode = PickingMode.Ignore;
            host.Add(bar);
            var dot = new VisualElement();
            dot.style.width = 1.4f;
            dot.style.height = 1.4f;
            dot.style.backgroundColor = new StyleColor(color);
            dot.style.position = Position.Absolute;
            dot.style.left = size * 0.5f - 0.7f;
            dot.style.top = size * 0.66f;
            dot.pickingMode = PickingMode.Ignore;
            host.Add(dot);
            return host;
        }

        /// <summary>sparkles 12×12 — 십자 별(효과 행).</summary>
        private static VisualElement BuildSparklesGlyph(float size)
        {
            var host = new VisualElement { name = "SparklesGlyph" };
            host.style.width = size;
            host.style.height = size;
            host.pickingMode = PickingMode.Ignore;
            var horizontal = new VisualElement();
            horizontal.style.width = size;
            horizontal.style.height = 1.6f;
            horizontal.style.backgroundColor = new StyleColor(Accent);
            horizontal.style.position = Position.Absolute;
            horizontal.style.top = (size - 1.6f) * 0.5f;
            horizontal.pickingMode = PickingMode.Ignore;
            host.Add(horizontal);
            var vertical = new VisualElement();
            vertical.style.width = 1.6f;
            vertical.style.height = size;
            vertical.style.backgroundColor = new StyleColor(Accent);
            vertical.style.position = Position.Absolute;
            vertical.style.left = (size - 1.6f) * 0.5f;
            vertical.pickingMode = PickingMode.Ignore;
            host.Add(vertical);
            return host;
        }

        /// <summary>lock 12×12 — 쇠심(테두리 아치) + 본체(건설 불가 버튼).</summary>
        private static VisualElement BuildLockGlyph(float size)
        {
            var host = new VisualElement { name = "LockGlyph" };
            host.style.width = size;
            host.style.height = size;
            host.pickingMode = PickingMode.Ignore;
            var shackle = new VisualElement();
            shackle.style.width = size * 0.6f;
            shackle.style.height = size * 0.42f;
            shackle.style.borderTopLeftRadius = shackle.style.borderTopRightRadius = size * 0.3f;
            shackle.style.borderTopWidth = 1.4f;
            shackle.style.borderLeftWidth = 1.4f;
            shackle.style.borderRightWidth = 1.4f;
            shackle.style.borderTopColor = shackle.style.borderLeftColor =
                shackle.style.borderRightColor = new StyleColor(TextDim);
            shackle.style.backgroundColor = new StyleColor(Color.clear);
            shackle.style.position = Position.Absolute;
            shackle.style.left = size * 0.2f;
            shackle.style.top = 0f;
            shackle.pickingMode = PickingMode.Ignore;
            host.Add(shackle);
            var body = new VisualElement();
            body.style.width = size * 0.84f;
            body.style.height = size * 0.58f;
            body.style.backgroundColor = new StyleColor(TextDim);
            body.style.position = Position.Absolute;
            body.style.left = size * 0.08f;
            body.style.top = size * 0.42f;
            body.pickingMode = PickingMode.Ignore;
            host.Add(body);
            return host;
        }

        /// <summary>close X 10×10 — 45° 회전 사각 2개(88차 선례).</summary>
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

        /// <summary>grid-3x3 19×19 — 외곽 테두리 + 3분할 내부선(조건 카드 '필요 부지' 아이콘).
        /// [144차 P7-CON #52] Figma 239:982/983 Vector stroke #8B949E 1.6 근사(내부선 1.2 — 프레임 구성 요소).</summary>
        private static VisualElement BuildGridGlyph(float size)
        {
            var host = new VisualElement { name = "GridGlyph" };
            host.style.width = size;
            host.style.height = size;
            host.pickingMode = PickingMode.Ignore;
            var frame = new VisualElement();
            frame.style.width = size;
            frame.style.height = size;
            frame.style.borderTopLeftRadius = frame.style.borderTopRightRadius = size * 0.1f;
            frame.style.borderBottomLeftRadius = frame.style.borderBottomRightRadius = size * 0.1f;
            frame.style.borderTopWidth = frame.style.borderBottomWidth =
                frame.style.borderLeftWidth = frame.style.borderRightWidth = 1.6f;
            frame.style.borderTopColor = frame.style.borderBottomColor =
                frame.style.borderLeftColor = frame.style.borderRightColor = new StyleColor(TextSub);
            frame.style.backgroundColor = new StyleColor(Color.clear);
            frame.style.position = Position.Absolute;
            frame.pickingMode = PickingMode.Ignore;
            host.Add(frame);
            for (int gi = 1; gi <= 2; gi++)
            {
                var lineV = new VisualElement();
                lineV.style.width = 1.2f;
                lineV.style.height = size;
                lineV.style.backgroundColor = new StyleColor(TextSub);
                lineV.style.position = Position.Absolute;
                lineV.style.left = size * gi / 3f - 0.6f;
                lineV.pickingMode = PickingMode.Ignore;
                host.Add(lineV);
                var lineH = new VisualElement();
                lineH.style.width = size;
                lineH.style.height = 1.2f;
                lineH.style.backgroundColor = new StyleColor(TextSub);
                lineH.style.position = Position.Absolute;
                lineH.style.top = size * gi / 3f - 0.6f;
                lineH.pickingMode = PickingMode.Ignore;
                host.Add(lineH);
            }
            return host;
        }

        /// <summary>users 19×19 — 2인 헤드(원 테두리)+보디(상단 라운드 테두리)(조건 카드 '필요 작업 인원' 아이콘).
        /// [144차 P7-CON #52] Figma 239:985/986 Vector stroke #8B949E 1.6 근사 — 후경 인물 반쪽 산출.</summary>
        private static VisualElement BuildUsersGlyph(float size)
        {
            var host = new VisualElement { name = "UsersGlyph" };
            host.style.width = size;
            host.style.height = size;
            host.pickingMode = PickingMode.Ignore;
            // 후경 인물(우측, 작게)
            var backHead = new VisualElement();
            backHead.style.width = size * 0.26f;
            backHead.style.height = size * 0.26f;
            backHead.style.borderTopLeftRadius = backHead.style.borderTopRightRadius = size * 0.13f;
            backHead.style.borderBottomLeftRadius = backHead.style.borderBottomRightRadius = size * 0.13f;
            backHead.style.borderTopWidth = backHead.style.borderBottomWidth =
                backHead.style.borderLeftWidth = backHead.style.borderRightWidth = 1.6f;
            backHead.style.borderTopColor = backHead.style.borderBottomColor =
                backHead.style.borderLeftColor = backHead.style.borderRightColor = new StyleColor(TextSub);
            backHead.style.backgroundColor = new StyleColor(Color.clear);
            backHead.style.position = Position.Absolute;
            backHead.style.left = size * 0.62f;
            backHead.style.top = size * 0.16f;
            backHead.pickingMode = PickingMode.Ignore;
            host.Add(backHead);
            var backBody = new VisualElement();
            backBody.style.width = size * 0.44f;
            backBody.style.height = size * 0.3f;
            backBody.style.borderTopLeftRadius = backBody.style.borderTopRightRadius = size * 0.22f;
            backBody.style.borderTopWidth = backBody.style.borderLeftWidth = backBody.style.borderRightWidth = 1.6f;
            backBody.style.borderTopColor = backBody.style.borderLeftColor =
                backBody.style.borderRightColor = new StyleColor(TextSub);
            backBody.style.backgroundColor = new StyleColor(Color.clear);
            backBody.style.position = Position.Absolute;
            backBody.style.left = size * 0.53f;
            backBody.style.top = size * 0.5f;
            backBody.pickingMode = PickingMode.Ignore;
            host.Add(backBody);
            // 전경 인물(좌측, 크게)
            var head = new VisualElement();
            head.style.width = size * 0.34f;
            head.style.height = size * 0.34f;
            head.style.borderTopLeftRadius = head.style.borderTopRightRadius = size * 0.17f;
            head.style.borderBottomLeftRadius = head.style.borderBottomRightRadius = size * 0.17f;
            head.style.borderTopWidth = head.style.borderBottomWidth =
                head.style.borderLeftWidth = head.style.borderRightWidth = 1.6f;
            head.style.borderTopColor = head.style.borderBottomColor =
                head.style.borderLeftColor = head.style.borderRightColor = new StyleColor(TextSub);
            head.style.backgroundColor = new StyleColor(SurfaceCol);   // 후경 선 가림 — 전경 실루엣 분리
            head.style.position = Position.Absolute;
            head.style.left = size * 0.16f;
            head.style.top = size * 0.1f;
            head.pickingMode = PickingMode.Ignore;
            host.Add(head);
            var body = new VisualElement();
            body.style.width = size * 0.58f;
            body.style.height = size * 0.36f;
            body.style.borderTopLeftRadius = body.style.borderTopRightRadius = size * 0.29f;
            body.style.borderTopWidth = body.style.borderLeftWidth = body.style.borderRightWidth = 1.6f;
            body.style.borderTopColor = body.style.borderLeftColor =
                body.style.borderRightColor = new StyleColor(TextSub);
            body.style.backgroundColor = new StyleColor(SurfaceCol);
            body.style.position = Position.Absolute;
            body.style.left = size * 0.05f;
            body.style.top = size * 0.54f;
            body.pickingMode = PickingMode.Ignore;
            host.Add(body);
            return host;
        }

        /// <summary>info 13×13 — 원 테두리 + i 점/막대(유의사항 아이콘).
        /// [144차 P7-CON #81] Figma 239:1021/1022 Vector stroke #8B949E 1.6 근사.</summary>
        private static VisualElement BuildInfoGlyph(float size)
        {
            var host = new VisualElement { name = "InfoGlyph" };
            host.style.width = size;
            host.style.height = size;
            host.pickingMode = PickingMode.Ignore;
            var circle = new VisualElement();
            circle.style.width = size;
            circle.style.height = size;
            circle.style.borderTopLeftRadius = circle.style.borderTopRightRadius = size * 0.5f;
            circle.style.borderBottomLeftRadius = circle.style.borderBottomRightRadius = size * 0.5f;
            circle.style.borderTopWidth = circle.style.borderBottomWidth =
                circle.style.borderLeftWidth = circle.style.borderRightWidth = 1.6f;
            circle.style.borderTopColor = circle.style.borderBottomColor =
                circle.style.borderLeftColor = circle.style.borderRightColor = new StyleColor(TextSub);
            circle.style.backgroundColor = new StyleColor(Color.clear);
            circle.style.position = Position.Absolute;
            circle.pickingMode = PickingMode.Ignore;
            host.Add(circle);
            var dot = new VisualElement();
            dot.style.width = 1.4f;
            dot.style.height = 1.4f;
            dot.style.backgroundColor = new StyleColor(TextSub);
            dot.style.position = Position.Absolute;
            dot.style.left = size * 0.5f - 0.7f;
            dot.style.top = size * 0.24f;
            dot.pickingMode = PickingMode.Ignore;
            host.Add(dot);
            var stem = new VisualElement();
            stem.style.width = 1.4f;
            stem.style.height = size * 0.34f;
            stem.style.backgroundColor = new StyleColor(TextSub);
            stem.style.position = Position.Absolute;
            stem.style.left = size * 0.5f - 0.7f;
            stem.style.top = size * 0.44f;
            stem.pickingMode = PickingMode.Ignore;
            host.Add(stem);
            return host;
        }

        // ═══════════════ 공용 스타일 헬퍼 ═══════════════

        /// <summary>절대 배치 블록 컨테이너(창-로컬 좌표) — SleepPanelUTK Block 선례.</summary>
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

        /// <summary>
        /// [94차 F2] 인라인 배경 폴백 — USS/테마 배경 리소스(ThemeUss PNG) 누락과 무관하게 창 패널이
        /// 항상 보이도록 단색(#1B1F27/0.92)+보더(#3A4150 1px)를 인라인 적용(스펙 결함 2 수리 1).
        /// 멱등 가드: 이미 인라인 배경이 있는 요소(좌/우 패널 — PanelBg 명시)는 무수정 — 누락 곳만 적용
        /// (StyleKeyword.Null = 인라인 미설정 판정 — USS 클래스 의존 배경은 폴백 대상).
        /// </summary>
        private static void EnsureInlineBackgroundFallback(VisualElement ve)
        {
            if (ve == null) return;
            if (ve.style.backgroundColor.keyword != StyleKeyword.Null) return;   // 인라인 배경 기존 — 건드리지 않음
            ve.style.backgroundColor = new StyleColor(FallbackPanelBg);
            ve.style.borderTopWidth = ve.style.borderBottomWidth = 1f;
            ve.style.borderLeftWidth = ve.style.borderRightWidth = 1f;
            ve.style.borderTopColor = ve.style.borderBottomColor =
                ve.style.borderLeftColor = ve.style.borderRightColor = new StyleColor(FallbackPanelBorder);
        }
    }
}
