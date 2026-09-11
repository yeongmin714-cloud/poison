using System.Collections.Generic;
using UnityEngine;
using ProjectName.UI.Themes;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using ProjectName.Core.Themes;
#pragma warning disable 0414

namespace ProjectName.UI
{
    /// <summary>
    /// 인벤토리 윈도우 — I 키로 열기/닫기.
    /// 레퍼런스 스타일의 다크 테마 IMGUI로 구성.
    /// 상단 타이틀 바 → 카테고리 탭 → 아이템 슬롯 그리드 → 하단 상세 정보 패널
    /// </summary>
    public class InventoryWindow : UIWindow
    {
        [Header("Inventory Window")]
        [SerializeField] private Transform _itemGridContainer;  // (Canvas 모드용, 현재 미사용)
        [SerializeField] private GameObject _itemSlotPrefab;    // (Canvas 모드용, 현재 미사용)

        [Header("Categories")]
        [SerializeField] private PlayerInventory.ItemCategory _selectedCategory = PlayerInventory.ItemCategory.Herb;

        [Header("Info Panel")]
        [SerializeField] private string _selectedItemName = "";
        [SerializeField] private string _selectedItemDesc = "";
        [SerializeField] private int _selectedItemCount = 0;

        // 현재 표시중인 아이템 데이터
        private PlayerInventory.ItemSlot[] _currentSlots;
        private Vector2 _scrollPosition;
        private Vector2 _infoScrollPosition;
        private int _selectedSlotIndex = -1;

        // ===== 🗺️ 오토루트 컨텍스트 메뉴 =====
        private bool _showRouteContextMenu = false;
        private Rect _routeContextMenuRect;
        private string _routeContextItemName = "";
        private string _routeContextItemId = "";
        private string _routeContextTerritoryName = "";
        private string _routeContextTerritoryId = "";
        private PlayerInventory.ItemSlot _routeContextSlot;

        // ===== 2026-09-09 재구조화: 싱글턴/컨텍스트/설명패널/드래그 =====
        public enum ContextMode { None, Warehouse, Shop, Loot }
        private static InventoryWindow _instance;
        public static InventoryWindow Instance => _instance;
        private static ContextMode _pendingContextMode = ContextMode.None;
        private ContextMode _contextMode = ContextMode.None;

        // ===== 2026-09-11(4): 창고 컨텍스트 실데이터 렌더 =====
        private string _warehouseTerritoryId;                 // Warehouse 컨텍스트에서 렌더할 창고 territoryId
        private static string s_pendingWarehouseTerritoryId;  // 인스턴스 생성 전 SetContextMode 대기 값
        private int[] _warehouseSlotIndices = System.Array.Empty<int>();  // 필터 뷰 idx → 창고 전역 슬롯 idx

        private PlayerInventory.ItemData _selectedItemData;   // 설명 패널 표시용
        private bool _dragActive;                             // 그리드→드롭 타겟 드래그 (핫바/창고/슬롯 스왑)
        private PlayerInventory.ItemData _dragItemData;
        private int _dragSlotGlobalIndex = -1;                // 2026-09-11(3): 드래그 시작 전역 슬롯 인덱스 (슬롯↔슬롯 스왑용)
        private float _lastInvX;                              // 컨텍스트 창(상점)이 참조하는 인벤 좌측 x

        // ===== 2026-09-11(3): DnD 드롭 판정용 화면 Rect 캐시 (정적 — GC 캐시 관례) =====
        private static readonly List<Rect> s_slotScreenRects = new List<Rect>(80);   // 아이템 있는 슬롯 (전역 인덱스)
        private static readonly List<int> s_slotScreenIndices = new List<int>(80);
        private static Rect s_gridScreenRect;                                        // 그리드 전체 영역 (창고→인벤 드롭 타겟)

        /// <summary>상점 등 컨텍스트 창의 x 좌표 — 제3구획(화면 우측 1/3) 시작점</summary>
        public static float GetContextX(float contextWidth)
        {
            return Screen.width * 2f / 3f + 6f;
        }

        /// <summary>삼분활 패널 폭 (상점 등 컨텍스트 창이 사용)</summary>
        public static float PanelWidth => Screen.width / 3f - 12f;

        /// <summary>
        /// 2026-09-11(3): 화면(GUI) 좌표가 속한 인벤 슬롯의 전역 인덱스 반환 (DnD 드롭 판정용).
        /// 아이템 있는 슬롯 = 전역 인덱스(>=0). 미해당 시 -1. 후순위 매칭(아이템 슬롯 우선).
        /// </summary>
        public static int GetInventorySlotIndexAtScreenPoint(Vector2 guiPoint)
        {
            for (int i = s_slotScreenRects.Count - 1; i >= 0; i--)
            {
                if (s_slotScreenRects[i].Contains(guiPoint))
                    return s_slotScreenIndices[i];
            }
            return -1;
        }

        /// <summary>2026-09-11(3): 화면(GUI) 좌표가 인벤 그리드 영역 안인지 (창고→인벤 드롭 타겟).</summary>
        public static bool IsPointOverInventoryGrid(Vector2 guiPoint)
        {
            return s_gridScreenRect.width > 0f && s_gridScreenRect.Contains(guiPoint);
        }

        // ===== 정렬 =====
        private enum SortMode { None, Category, Name, Rarity, Quantity }
        private SortMode _sortMode = SortMode.None;
        private string[] _sortModeLabels = { "정렬 안함", "카테고리순", "이름순", "등급순", "수량순" };

        // ===== 레퍼런스 스타일 상수 (2026-09-11 Flat: 얇은 타이틀 스트립 + 컴팩트 탭) =====
        // 화면 정확히 삼분활 — 패널 폭 = Screen.width/3 - 12, 하단 핫바(150+12) 공간 확보
        private static float WINDOW_WIDTH => Screen.width / 3f - 12f;
        private static float WINDOW_HEIGHT => Screen.height - 180f;   // 상단 10 + 하단 핫바 170 여백
        private const float TITLE_BAR_HEIGHT = 64f;    // Flat: 얇은 상단 스트립
        private const float TAB_BAR_HEIGHT = 72f;      // Flat: 컴팩트 탭
        private const float INFO_PANEL_HEIGHT = 272f;   // (레거시 — 미사용)
        private const float WEAPON_SECTION_HEIGHT = 112f;  // (레거시 — 미사용)
        private const float EQUIP_ROW_HEIGHT = 268f;    // 장비창 5칸씩 2줄 (박스 86 + 라벨 44 × 2)
        private const float DESC_PANEL_HEIGHT = 620f;   // 2026-09-09(3): 설명창 세로 축소 (드래그는 하단 실제 핫바로)
        private const float DESC_GAP = 12f;             // 구획 간 미세 여백
        private const int GRID_COLUMNS = 5;                // 가방 5칸씩 (6줄 + 스크롤)
        private const float SLOT_MARGIN = 6f;              // 슬롯 간격
        private const float SLOT_ICON_SIZE = 96f;          // 슬롯 내 아이콘 크기 (레거시, 동적 크기 사용 권장)
        private const int GRID_MIN_ROWS = 6;               // 가방 가시 행 수 (6줄, 초과분 스크롤)
        private const float PREVIEW_PANEL_WIDTH = 0f;    // (제거됨)
        private static float GRID_AREA_WIDTH => WINDOW_WIDTH - PREVIEW_PANEL_WIDTH; // 그리드 영역 폭

        // 포커스/강조 색상 — Flat 모드: 스카이블루 글로우/엣지 (기존 민트 대체)
        private static readonly Color ColorMintGlow = new Color(0.35f, 0.65f, 0.90f, 0.30f);
        private static readonly Color ColorMintEdge = new Color(0.45f, 0.72f, 0.95f, 0.9f);
        private static readonly Color ColorSlotEmptyCell = new Color(0.10f, 0.10f, 0.13f, 0.55f); // 빈 슬롯 가이드 셀
        private static readonly Color ColorGridLine = new Color(0.30f, 0.30f, 0.34f, 0.5f);       // 그리드 가이드라인

        // ===== 2026-09-11: Flat 테마 색상 (인벤토리 예시 2 — 플랫 다크 네이비 + 스카이블루) =====
        private static readonly Color ColorBg = new Color(0.063f, 0.086f, 0.133f, 0.88f);      // 전체 배경 (다크 네이비 반투명)
        private static readonly Color ColorTitleBar = new Color(0.055f, 0.078f, 0.125f, 0.95f); // 타이틀 스트립 (더 어두운 네이비)
        private static readonly Color ColorTabActive = new Color(0.35f, 0.65f, 0.90f, 1f);     // 활성 탭 (스카이블루)
        private static readonly Color ColorTabInactive = new Color(0.11f, 0.15f, 0.23f, 0.95f);// 비활성 탭 (다크 네이비)
        private static readonly Color ColorSlotBg = new Color(0.09f, 0.12f, 0.19f, 0.72f);     // 슬롯 배경 (짙은 반투명)
        private static readonly Color ColorSlotHover = new Color(0.14f, 0.20f, 0.30f, 0.90f);  // 슬롯 호버
        private static readonly Color ColorSlotSelected = new Color(0.16f, 0.28f, 0.42f, 1f);  // 슬롯 선택 (스카이블루 틴트)
        private static readonly Color ColorInfoBg = new Color(0.063f, 0.086f, 0.133f, 0.88f);  // 정보 패널 배경
        private static readonly Color ColorTextPrimary = new Color(1f, 1f, 1f, 1f);            // 기본 텍스트 (흰색)
        private static readonly Color ColorTextSecondary = new Color(0.85f, 0.88f, 0.92f, 1f); // 보조 텍스트
        private static readonly Color ColorTextDim = new Color(0.72f, 0.76f, 0.82f, 1f);       // 흐린 텍스트
        private static readonly Color ColorAccent = new Color(0.35f, 0.65f, 0.90f, 1f);        // 강조 (스카이블루)
        private static readonly Color ColorBorder = new Color(0.62f, 0.70f, 0.78f, 0.85f);     // 테두리 (얇은 회백)
        private static readonly Color ColorBtnBg = new Color(0.12f, 0.16f, 0.24f, 1f);         // 버튼 배경 (다크 슬레이트)
        private static readonly Color ColorBtnHover = new Color(0.18f, 0.25f, 0.36f, 1f);      // 버튼 호버
        private static readonly Color ColorBtnEquippedBg = new Color(0.16f, 0.30f, 0.44f, 1f); // 장착 중 버튼 배경 (스카이블루 틴트)

        // ===== 커스텀 GUIStyle 캐시 =====
        private GUIStyle _styleTitle;
        private GUIStyle _styleTab;
        private GUIStyle _styleTabActive;
        private GUIStyle _styleSlot;
        private GUIStyle _styleSlotSelected;
        private GUIStyle _styleSlotLabel;
        private GUIStyle _styleItemName;
        private GUIStyle _styleItemCount;
        private GUIStyle _styleInfoName;
        private GUIStyle _styleInfoDesc;
        private GUIStyle _styleInfoLabel;
        private GUIStyle _styleEmptyText;
        private GUIStyle _stylePanelBox;
        private GUIStyle _styleButton;          // 공용 버튼 (정렬/수리/사용 — 흰색 굵은 텍스트)
        private GUIStyle _styleWeaponBtn;       // 무기 장착 버튼 (다크 배경 + 흰 테두리)
        private GUIStyle _styleWeaponBtnEquipped; // 장착 중 버튼 (금색 테두리)
        // ===== AAA 4레이어 스타일 (InventoryArtLibrary static 캐시 텍스처 — 파기 금지) =====
        private GUIStyle _styleBackplate;       // Layer 1: 스톤 백플레이트 (9-Slice border 24)
        private GUIStyle _styleMetalFrame;      // Layer 4: 금속 프레임 (9-Slice border 16)
        private bool _stylesInitialized;
        private Texture2D _texWhite;
        private Texture2D _texSlotBg;           // 슬롯 배경 (다크 + 흰 테두리)
        private Texture2D _texSlotBgHover;      // 슬롯 호버 배경
        private Texture2D _texSlotBgSelected;   // 슬롯 선택 배경 (금색 테두리)
        private Texture2D _texBtnBg;            // 버튼 배경 (다크 + 밝은 테두리)
        private Texture2D _texBtnBgHover;       // 버튼 호버 배경
        private Texture2D _texBtnBgEquipped;    // 장착 중 버튼 배경 (금색 테두리)
        private Texture2D _texSlotEmptyGuide;   // T3B-1: 빈 슬롯 가이드 셀 (라운드 다크 셀 + 그리드라인 보더)

        // ===== 3D 캐릭터 프리뷰 (RenderTexture) — 월드 플레이어와 별개의 fresh 인스턴스 =====
        private GameObject _previewModel;       // 프리뷰 전용 플레이어 모델 인스턴스 (원격 위치, 월드 플레이어 무영향)
        private Camera _previewCamera;          // 프리뷰 전용 카메라
        private RenderTexture _previewRT;       // 프리뷰 전용 RenderTexture (null = 프리뷰 없음 → 플레이스홀더 유지)
        private GameObject _previewWeapon;      // 프리뷰 손에 부착한 검 인스턴스
        private string _previewWeaponId;        // 프리뷰에 부착된 검 id (중복 부착 방지)
        private bool _previewSetupTried;        // 이번 열림에서 생성 시도 완료 플래그 (실패 재시도 스팸 방지)

        protected override void Awake()
        {
            base.Awake();
            ApplyTheme(Phase33_Themes.CreateInventoryTheme());
            _instance = this;
            // 대기 중 컨텍스트 모드 적용 (창고/상점/전리품이 인벤보다 먼저 연 경우)
            if (_pendingContextMode != ContextMode.None)
            {
                _contextMode = _pendingContextMode;
                if (_contextMode == ContextMode.Warehouse)
                    _warehouseTerritoryId = s_pendingWarehouseTerritoryId;
                _pendingContextMode = ContextMode.None;
                s_pendingWarehouseTerritoryId = null;
            }

            // 2026-09-11(4): 셋업이 핫키를 못 단 경우(Test_10 등 부착만 하고 Bind 없음) 자가 등록 — I키 토글 보장.
            // 기존 UIInventoryHotkey 클래스 재활용. 이미 씬에 핫키가 있으면 중복 등록하지 않는다.
            if (FindAnyObjectByType<UIInventoryHotkey>(FindObjectsInactive.Include) == null)
            {
                var hotkey = gameObject.AddComponent<UIInventoryHotkey>();
                hotkey.Bind(this);
                Debug.Log("[InventoryWindow] UIInventoryHotkey 자가 등록 (I키 토글 — 셋업 바인딩 부재 폴백)");
            }
        }

        /// <summary>컨텍스트 모드 지정 (창고/상점/전리품 상호작용 시 호출 — 인벤 창도 함께 열어줌)</summary>
        public static void SetContextMode(ContextMode mode) => SetContextMode(mode, null);

        /// <summary>
        /// 2026-09-11(4): territoryId 전달 오버로드 — Warehouse 컨텍스트는 이 ID의
        /// WarehouseSystem.GetItems(...) 내용을 렌더한다(플레이어 인벤 대체).
        /// </summary>
        public static void SetContextMode(ContextMode mode, string territoryId)
        {
            if (_instance != null)
            {
                _instance._contextMode = mode;
                _instance._warehouseTerritoryId = (mode == ContextMode.Warehouse) ? territoryId : null;
                if (!_instance.IsOpen) _instance.Show();
                else _instance.RefreshInventory();   // 이미 열림 — 즉시 소스 전환
            }
            else
            {
                _pendingContextMode = mode;
                s_pendingWarehouseTerritoryId = (mode == ContextMode.Warehouse) ? territoryId : null;
            }
        }

        /// <summary>
        /// 2026-09-11(2): 컨텍스트 종료 (창고 E토글/ESC/이탈 닫기에서 호출) —
        /// 컨텍스트 해제 + 창 닫기. SetContextMode와 달리 재오픈하지 않는다.
        /// </summary>
        public static void CloseContext()
        {
            _pendingContextMode = ContextMode.None;
            s_pendingWarehouseTerritoryId = null;
            if (_instance != null)
            {
                _instance._contextMode = ContextMode.None;
                _instance._warehouseTerritoryId = null;
                if (_instance.IsOpen) _instance.Hide();
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_texWhite != null)
            {
                Destroy(_texWhite);
                _texWhite = null;
            }
            if (_texSlotBg != null) { Destroy(_texSlotBg); _texSlotBg = null; }
            if (_texSlotBgHover != null) { Destroy(_texSlotBgHover); _texSlotBgHover = null; }
            if (_texSlotBgSelected != null) { Destroy(_texSlotBgSelected); _texSlotBgSelected = null; }
            if (_texBtnBg != null) { Destroy(_texBtnBg); _texBtnBg = null; }
            if (_texBtnBgHover != null) { Destroy(_texBtnBgHover); _texBtnBgHover = null; }
            if (_texBtnBgEquipped != null) { Destroy(_texBtnBgEquipped); _texBtnBgEquipped = null; }
            if (_texSlotEmptyGuide != null) { Destroy(_texSlotEmptyGuide); _texSlotEmptyGuide = null; }
            ReleasePreview();   // 3D 프리뷰 자원 정리 (RT/카메라 누수 방지)
        }

        protected override void OnShow()
        {
            // 2026-09-09: 3D 캐릭터 프리뷰 제거 (아바타 구조 폐지 — 스탯창 v2가 담당)

            _selectedSlotIndex = -1;
            RefreshInventory();
        }

        protected override void OnHide()
        {
            // 2026-09-09: 프리뷰 제거로 정리 불필요 — OnDestroy의 ReleasePreview는 안전망으로 유지
            // 2026-09-11(3): 창 닫힘 시 드래그 상태 정리 (고스트 잔상/스테일 컨텍스트 방지)
            _dragItemData = null;
            _dragActive = false;
            _dragSlotGlobalIndex = -1;
            ItemDragContext.Cancel();
            // 2026-09-11(4): 창 닫힘 시 컨텍스트 해제 — 재오픈(I키)은 플레이어 인벤 모드로 시작
            _contextMode = ContextMode.None;
            _warehouseTerritoryId = null;
        }

        /// <summary>
        /// 스타일 초기화 (OnGUI에서 최초 1회) — 젤다 토탈코딩 스타일:
        /// 반투명 다크 패널 / 흰색 굵은 텍스트 / 금색 선택 하이라이트 / 흰 테두리 슬롯
        /// </summary>
        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _texWhite = MakeTexture(1, 1, Color.white);

            // 테두리 텍스처 (8×8, 가장자리 2px)
            _texSlotBg        = MakeBorderedTexture(8, 8, ColorSlotBg,     new Color(0.80f, 0.80f, 0.82f, 0.95f), 1); // 다크 + 흰 테두리
            _texSlotBgHover   = MakeBorderedTexture(8, 8, ColorSlotHover,  new Color(0.90f, 0.90f, 0.92f, 1f),    1);
            // T3C-2: 포커스 슬롯 — 민트 글로우 배경 + 금색 테두리 (투명 민트가 가이드 셀 위에서 글로우로 보임)
            _texSlotBgSelected= MakeBorderedTexture(8, 8, ColorMintGlow, ColorAccent,                       1);
            _texBtnBg         = MakeBorderedTexture(8, 8, ColorBtnBg,      new Color(0.72f, 0.72f, 0.75f, 1f),    1);
            _texBtnBgHover    = MakeBorderedTexture(8, 8, ColorBtnHover,   new Color(0.85f, 0.85f, 0.88f, 1f),    1);
            _texBtnBgEquipped = MakeBorderedTexture(8, 8, ColorBtnEquippedBg, ColorAccent,                       1); // 장착 중 = 금색 테두리

            // T3B-1: 빈 슬롯 가이드 셀 — 라운드 사각 (다크 셀 + 그리드라인 보더)
            _texSlotEmptyGuide = MakeRoundedBorderedTexture(48, 48, ColorSlotEmptyCell, ColorGridLine, 1, 10);

            // ===== AAA 4레이어: ArtLibrary 텍스처 기반 스타일 (static 캐시 — OnDisable/파괴 시 파기 금지) =====
            // Layer 1 백플레이트 — 스톤 패널 (9-Slice, border 24 등소비)
            _styleBackplate = new GUIStyle(GUI.skin.box)
            {
                normal = { background = InventoryArtLibrary.GetBackplate(), textColor = ColorTextPrimary },
                border = new RectOffset(24, 24, 24, 24),
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };
            // Layer 4 금속 프레임 — 두께 14px 금속 (9-Slice, border 16)
            _styleMetalFrame = new GUIStyle(GUI.skin.box)
            {
                normal = { background = InventoryArtLibrary.GetMetalFrame(), textColor = ColorTextPrimary },
                border = new RectOffset(16, 16, 16, 16),
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };

            // 타이틀 — Flat: 흰색 40px (2026-09-11: 기존 64px 골드톤 대체 — 얇은 타이틀 스트립에 맞춤)
            _styleTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 40,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(0.92f, 0.95f, 1f, 1f) },   // Flat: 흰색 톤 (골드 대체)
                padding = new RectOffset(21, 4, 0, 0)
            };

            // 탭 (비활성) — 다크 배경 + 흰 텍스트 (C-UP: 18→22, 탭 폭 ~130px로 확대되어 잘림 없음)
            _styleTab = new GUIStyle(GUI.skin.button)
            {
                fontSize = 22,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                padding = new RectOffset(2, 2, 4, 4),
                normal = { textColor = ColorTextPrimary, background = MakeTexture(1, 1, ColorTabInactive) },
                hover = { textColor = ColorTextPrimary, background = MakeTexture(1, 1, ColorBtnHover) },
                active = { textColor = ColorTextPrimary, background = MakeTexture(1, 1, ColorTabActive) },
                border = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(1, 1, 2, 2)
            };

            // 탭 (활성) — 금색 배경 + 흰색 굵게
            _styleTabActive = new GUIStyle(_styleTab)
            {
                normal = { textColor = ColorTextPrimary, background = MakeTexture(1, 1, ColorTabActive) },
                fontStyle = FontStyle.Bold
            };

            // 슬롯 배경 — 다크 박스 + 흰 테두리
            _styleSlot = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _texSlotBg, textColor = ColorTextPrimary },
                hover = { background = _texSlotBgHover, textColor = ColorTextPrimary },
                border = new RectOffset(2, 2, 2, 2),
                padding = new RectOffset(4, 4, 4, 4),
                margin = new RectOffset(0, 0, 0, 0),
                fontSize = 36,
                alignment = TextAnchor.MiddleCenter
            };

            // 선택된 슬롯 배경 — 금색 테두리 하이라이트
            _styleSlotSelected = new GUIStyle(_styleSlot)
            {
                normal = { background = _texSlotBgSelected, textColor = ColorTextPrimary }
            };

            // 슬롯 라벨 (아이템 이름) — 흰색 굵게
            _styleSlotLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ColorTextPrimary },
                wordWrap = false
            };

            // 아이템 이름 (목록형 / 무기 섹션 라벨) — 흰색 굵게
            _styleItemName = new GUIStyle(GUI.skin.label)
            {
                fontSize = 60,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ColorTextPrimary }
            };

            // 아이템 개수 — 작게
            _styleItemCount = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = ColorAccent }
            };

            // 정보 패널 - 이름 (흰색 굵게, 크게)
            _styleInfoName = new GUIStyle(GUI.skin.label)
            {
                fontSize = 76,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ColorTextPrimary },
                padding = new RectOffset(0, 0, 2, 0)
            };

            // 정보 패널 - 설명 (밝은 회색, 가독성 상향)
            _styleInfoDesc = new GUIStyle(GUI.skin.label)
            {
                fontSize = 54,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = ColorTextSecondary },
                wordWrap = true,
                padding = new RectOffset(0, 0, 2, 0)
            };

            // 정보 패널 - 레이블 (이탤릭 제거 + 밝은 텍스트)
            _styleInfoLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 48,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ColorTextSecondary }
            };

            // 빈 목록 텍스트 (이탤릭 제거 + 흰색)
            _styleEmptyText = new GUIStyle(GUI.skin.label)
            {
                fontSize = 54,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ColorTextPrimary }
            };

            // 패널 외곽 박스 (반투명 다크)
            _stylePanelBox = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTexture(1, 1, ColorBg), textColor = ColorTextPrimary },
                border = new RectOffset(2, 2, 2, 2),
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };

            // 공용 버튼 (정렬/수리/사용) — 다크 배경 + 흰색 굵은 텍스트
            _styleButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ColorTextPrimary, background = _texBtnBg },
                hover = { textColor = ColorTextPrimary, background = _texBtnBgHover },
                active = { textColor = ColorTextPrimary, background = _texBtnBgHover },
                border = new RectOffset(2, 2, 2, 2)
            };

            // 무기 장착 버튼 — 다크 배경 + 밝은 테두리, 흰색 굵은 텍스트
            _styleWeaponBtn = new GUIStyle(_styleButton);

            // 무기 장착 중 버튼 — 금색 테두리
            _styleWeaponBtnEquipped = new GUIStyle(_styleButton)
            {
                normal = { textColor = ColorTextPrimary, background = _texBtnBgEquipped },
                hover = { textColor = ColorTextPrimary, background = _texBtnBgEquipped },
                active = { textColor = ColorTextPrimary, background = _texBtnBgEquipped }
            };

            _stylesInitialized = true;
        }

        // ===================================================================
        // OnGUI — IMGUI 렌더링
        // ===================================================================
        protected override void OnGUI()
        {
            if (!IsOpen) return;

            base.OnGUI();

            // G3-05: 통일 스타일 — 딤드 오버레이 (창 자체는 아래 AAA 4레이어가 그림)
            UIStyleManager.DrawDimOverlay();
            // 2026-09-09(2): 화면 정확히 삼분활 — 제1구획=인벤(x=6), 제2=설명(S/3+6), 제3=컨텍스트(2S/3+6)
            float x = 6f;
            _lastInvX = x;
            float y = 10f;
            Rect winRect = new Rect(x, y, WINDOW_WIDTH, WINDOW_HEIGHT);
            // (구 UIStyleManager.DrawWindowBackground/DrawTitle 제거 — AAA 백플레이트+배너로 대체, 이중 렌더 방지)
            if (UIStyleManager.DrawCloseButton(winRect))
            {
                Hide();
                return;
            }

            InitStyles();

            // ===================================================================
            // AAA 4레이어 z-order: ①드롭섀도우 → ②백플레이트 → ③그리드/컨텐츠 → ④프레임
            // ===================================================================

            // === 드롭섀도우: 창 rect 12px 사방 확장, 검정 tint 단일 DrawTexture (화면 팝업감) ===
            DrawWindowDropShadow(x, y, WINDOW_WIDTH, WINDOW_HEIGHT);

            // === Layer 1: 스톤 백플레이트 (9-Slice) — 기존 평면 패널/타이틀 띠 대체 ===
            GUI.Box(winRect, "", _styleBackplate);

            // === 타이틀 스트립 (Flat: 얇은 상단 스트립 + 좌측 타이틀 텍스트) — 정렬 버튼 왼쪽 가용 영역 ===
            float sortBtnWidth = 300f;
            float sortBtnHeight = 48f;
            float sortBtnX = x + WINDOW_WIDTH - sortBtnWidth - 12f;
            float sortBtnY = y + (TITLE_BAR_HEIGHT - sortBtnHeight) * 0.5f;
            DrawTitleStrip(x, y, WINDOW_WIDTH, sortBtnX);
            // 2026-09-11(4): 창고 컨텍스트 — 타이틀 "창고" 표기
            GUI.Label(new Rect(x, y + 4, sortBtnX - x - 12f, TITLE_BAR_HEIGHT),
                _contextMode == ContextMode.Warehouse ? " 창고" : " 인벤토리", _styleTitle);

            // 정렬 버튼 (타이틀 스트립 우측) — 기존 로직 유지 (다크 배경 + 흰색 굵은 텍스트)
            if (GUI.Button(new Rect(sortBtnX, sortBtnY, sortBtnWidth, sortBtnHeight), $"정렬: {_sortModeLabels[(int)_sortMode]}", _styleButton))
            {
                _sortMode = (SortMode)(((int)_sortMode + 1) % 5);
                if (_sortMode != SortMode.None)
                {
                    SortInventory();
                    RefreshInventory();
                }
                else
                {
                    RefreshInventory();
                }
            }

            // 타이틀 하단 구분선
            DrawColoredRect(new Rect(x, y + TITLE_BAR_HEIGHT + 2, WINDOW_WIDTH, 2), ColorBorder);

            // === 카테고리 탭 ===
            float tabY = y + TITLE_BAR_HEIGHT + 4;
            // (기존 ColorTitleBar 평면 띠 제거 — 스톤 백플레이트가 그대로 비쳐 보임)
            DrawCategoryTabs(x, tabY);
            DrawColoredRect(new Rect(x, tabY + TAB_BAR_HEIGHT, WINDOW_WIDTH, 1), ColorBorder);

            // === 아이템 슬롯 그리드 (스크롤 가능) ===
            float gridY = tabY + TAB_BAR_HEIGHT + 1;
            float gridHeight = WINDOW_HEIGHT - (gridY - y) - EQUIP_ROW_HEIGHT - 8;
            DrawItemGrid(x, gridY, gridHeight);

            // === 하단 장비슬롯 6종 (2026-09-09: 무기버튼/프리뷰 대체, 우클릭 해제) ===
            float equipY = gridY + gridHeight + 2;
            DrawEquipRow(x, equipY);

            // === Layer 4: 금속 프레임(9-Slice) + 4모서리 로터스 장식 — 컨텐츠 위 투명 텍스처로 안착 ===
            DrawWindowFrame(x, y, WINDOW_WIDTH, WINDOW_HEIGHT);

            // === 중앙 아이템 설명 패널 (2026-09-09(2): 제2구획 — 설명 + 핫바 미니패드) ===
            DrawDescriptionPanel(x + WINDOW_WIDTH + DESC_GAP, y);

            // === 🗺️ 오토루트 컨텍스트 메뉴 ===
            DrawRouteContextMenu();

            // === 드래그 고스트 + 패드 드롭 판정 ===
            ProcessDrag();
        }

        // ===================================================================
        // 카테고리 탭 그리기
        // ===================================================================
        private void DrawCategoryTabs(float panelX, float tabY)
        {
            string[] tabNames = { "🌿 약초", "🥩 고기", "🍲 요리", "🧪 약", "🧱 재료", "🗡️ 무기", "🛡️ 방어구", "🔧 도구" };
            PlayerInventory.ItemCategory[] categories =
            {
                PlayerInventory.ItemCategory.Herb,
                PlayerInventory.ItemCategory.Meat,
                PlayerInventory.ItemCategory.Food,
                PlayerInventory.ItemCategory.Potion,
                PlayerInventory.ItemCategory.Material,
                PlayerInventory.ItemCategory.Weapon,
                PlayerInventory.ItemCategory.Armor,
                PlayerInventory.ItemCategory.Tool
            };

            float tabWidth = (WINDOW_WIDTH - 8) / tabNames.Length;
            float tx = panelX + 4;

            for (int i = 0; i < tabNames.Length; i++)
            {
                bool isActive = _selectedCategory == categories[i];
                var style = isActive ? _styleTabActive : _styleTab;

                if (GUI.Button(new Rect(tx, tabY + 2, tabWidth - 2, TAB_BAR_HEIGHT - 4), tabNames[i], style))
                {
                    if (!isActive)
                    {
                        _selectedCategory = categories[i];
                        _selectedSlotIndex = -1;
                        RefreshInventory();
                    }
                }
                tx += tabWidth;
            }
        }

        // ===================================================================
        // 아이템 슬롯 그리드
        // ===================================================================
        private void DrawItemGrid(float panelX, float gridY, float gridHeight)
        {
            float innerX = panelX + 4;
            float innerY = gridY + 2;
            float innerWidth = GRID_AREA_WIDTH - 8;   // T3B-2: 우측 프리뷰 패널을 제외한 좌측 그리드 영역

            // 스크롤 뷰 — 젤다 스타일 5열 정사각 슬롯 (간격 6px)
            float slotTotalWidth = innerWidth;
            float slotWidth = (slotTotalWidth - SLOT_MARGIN * (GRID_COLUMNS + 1)) / GRID_COLUMNS;
            float slotHeight = slotWidth;   // 정사각형 슬롯
            float rowHeight = slotHeight + SLOT_MARGIN;

            int totalSlots = _currentSlots != null ? _currentSlots.Length : 0;
            int totalRows = Mathf.Max(1, Mathf.CeilToInt((float)totalSlots / GRID_COLUMNS));
            int guideRows = Mathf.Max(totalRows, GRID_MIN_ROWS);   // T3B-1: 빈 상태에서도 최소 행수 가이드 표시
            float contentHeight = guideRows * rowHeight + SLOT_MARGIN;
            float viewHeight = gridHeight - 4;

            // 2026-09-11(3): DnD 드롭 판정용 Rect 캐시 리빌드 (매 프레임)
            s_slotScreenRects.Clear();
            s_slotScreenIndices.Clear();
            Vector2 gridScreenPos = GUIUtility.GUIToScreenPoint(new Vector2(innerX, innerY));
            s_gridScreenRect = new Rect(gridScreenPos.x, gridScreenPos.y, innerWidth, viewHeight);

            // 배경 (좌측 그리드 영역만 — 우측은 캐릭터 프리뷰 패널)
            DrawColoredRect(new Rect(panelX, gridY, GRID_AREA_WIDTH, gridHeight), ColorBg);

            _scrollPosition = GUI.BeginScrollView(
                new Rect(innerX, innerY, innerWidth, viewHeight),
                _scrollPosition,
                new Rect(0, 0, innerWidth - 20, contentHeight)
            );

            // === AAA Layer 2: 빈 슬롯 가이드 그리드 — 엠보싱 셀 텍스처(순백)로 항상 표시 (기존 _texSlotEmptyGuide 대체) ===
            Texture2D slotCellTex = InventoryArtLibrary.GetSlotCell();
            int guideCells = guideRows * GRID_COLUMNS;
            var prevGuideColor = GUI.color;
            GUI.color = Color.white;
            for (int g = 0; g < guideCells; g++)
            {
                int gCol = g % GRID_COLUMNS;
                int gRow = g / GRID_COLUMNS;
                float gx = SLOT_MARGIN + gCol * (slotWidth + SLOT_MARGIN);
                float gy = SLOT_MARGIN + gRow * rowHeight;
                GUI.DrawTexture(new Rect(gx, gy, slotWidth, slotHeight), slotCellTex);
            }
            GUI.color = prevGuideColor;

            if (_currentSlots == null || _currentSlots.Length == 0)
            {
                GUI.Label(new Rect(0, 24, innerWidth - 20, 60), "(이 카테고리에 아이템이 없습니다)", _styleEmptyText);
            }
            else
            {
                for (int i = 0; i < _currentSlots.Length; i++)
                {
                    var slot = _currentSlots[i];
                    if (slot == null || slot.item == null || slot.count <= 0) continue;

                    int col = i % GRID_COLUMNS;
                    int row = i / GRID_COLUMNS;

                    float sx = SLOT_MARGIN + col * (slotWidth + SLOT_MARGIN);
                    float sy = SLOT_MARGIN + row * rowHeight;

                    Rect slotRect = new Rect(sx, sy, slotWidth, slotHeight);
                    bool isSelected = (i == _selectedSlotIndex);
                    bool isHover = slotRect.Contains(Event.current.mousePosition);

                    // 2026-09-11(3): 드롭 판정용 슬롯 화면 Rect 캐시 (전역 인덱스)
                    // 2026-09-11(4): 창고 컨텍스트에서는 창고 전역 슬롯 인덱스를 캐시 (인벤 전역 인덱스와 계열 구분)
                    int dndGlobalIdx = _contextMode == ContextMode.Warehouse
                        ? GetWarehouseSlotIndex(i)
                        : GetGlobalSlotIndex(_selectedCategory, i);
                    Vector2 slotScreenPos = GUIUtility.GUIToScreenPoint(new Vector2(sx, sy));
                    s_slotScreenRects.Add(new Rect(slotScreenPos.x, slotScreenPos.y, slotWidth, slotHeight));
                    s_slotScreenIndices.Add(dndGlobalIdx);

                    // === AAA Layer 2: 엠보싱 셀(흰색) → 희귀도 글로우 tint (아이템 있는 슬롯만) ===
                    Color prevSlotColor = GUI.color;
                    GUI.color = Color.white;
                    GUI.DrawTexture(slotRect, InventoryArtLibrary.GetSlotCell());
                    int rarityIdx = Mathf.Clamp((int)slot.item.rarity, 0, InventoryArtLibrary.RarityColors.Length - 1);
                    GUI.color = InventoryArtLibrary.RarityColors[rarityIdx];
                    GUI.DrawTexture(slotRect, InventoryArtLibrary.GetSlotGlow());
                    GUI.color = prevSlotColor;

                    // 아이콘 — 슬롯 상단 중앙 (C-UP: 슬롯 폭 비례, 상한 SLOT_ICON_SIZE)
                    float iconSize = Mathf.Min(SLOT_ICON_SIZE, slotWidth * 0.62f);
                    float iconX = sx + (slotWidth - iconSize) * 0.5f;
                    float iconY = sy + 8f;
                    Texture2D iconTex = ItemIconDatabase.GetOrCreateIcon(slot.item);
                    if (iconTex != null)
                    {
                        GUI.DrawTexture(new Rect(iconX, iconY, iconSize, iconSize), iconTex);
                    }
                    else
                    {
                        // 폴백: 카테고리 색상 사각형
                        Color iconColor = GetCategoryColor(slot.item.category);
                        GUI.color = iconColor;
                        GUI.DrawTexture(new Rect(iconX, iconY, iconSize, iconSize), _texWhite);
                        GUI.color = Color.white;
                    }

                    // 아이템 이름 — 흰색 굵게, 슬롯 하단부 중앙
                    float nameY = iconY + iconSize + 4f;
                    float nameWidth = slotWidth - 12f;
                    GUI.Label(new Rect(sx + 6, nameY, nameWidth, 40),
                        TruncateText(slot.item.displayName, nameWidth, _styleSlotLabel),
                        _styleSlotLabel);

                    // 수치 — 우상단 코너 (수량 / 무기는 공격력 수치 함께 표시; 젤다식 코너 오버레이)
                    string valueText = $"x{slot.count}";
                    if (slot.item.category == PlayerInventory.ItemCategory.Weapon)
                    {
                        string atk = ExtractFirstNumber(slot.item.effects);
                        if (!string.IsNullOrEmpty(atk)) valueText = $"x{slot.count}  ⚔{atk}";
                    }
                    GUI.Label(new Rect(sx + slotWidth - 70f, sy + 8f, 64f, 30),
                        valueText,
                        _styleItemCount);

                    // C9-18: 내구도 표시 (장비 아이템만)
                    if (slot.item.maxDurability > 0)
                    {
                        float durability = ProjectName.Systems.EquipmentDurabilitySystem.GetDurabilityRatio(slot);
                        Color durColor = durability >= 0.6f ? Color.green :
                                         durability >= 0.3f ? Color.yellow : Color.red;
                        float barWidth = slotWidth - 12;
                        float barHeight = 4f;
                        float barY = sy + slotHeight - 6f;

                        // 배경
                        DrawColoredRect(new Rect(sx + 6, barY, barWidth, barHeight), new Color(0.15f, 0.15f, 0.15f, 0.8f));
                        // 내구도 채움
                        DrawColoredRect(new Rect(sx + 6, barY, barWidth * Mathf.Clamp01(durability), barHeight), durColor);
                    }

                    // === AAA Layer 3: 호버/선택 하이라이트 — 아이콘 위 최상단 라운드 링 오버레이 ===
                    if (isSelected)
                        DrawSlotTint(InventoryArtLibrary.GetSlotHighlight(), slotRect, ColorAccent);                    // 선택 = 골드
                    else if (isHover)
                        DrawSlotTint(InventoryArtLibrary.GetSlotHighlight(), slotRect, new Color(1f, 1f, 0.9f, 0.9f));  // 호버 = 따뜻한 백색

                    // 클릭 처리 (호버 영역) — 🗺️ 오토루트 우클릭 연동
                    if (Event.current.type == EventType.MouseDown && slotRect.Contains(Event.current.mousePosition))
                    {
                        if (Event.current.button == 0) // 좌클릭 — 선택 + 드래그 준비 (설명 패널 표시)
                        {
                            _selectedSlotIndex = i;
                            _selectedItemName = slot.item.displayName;
                            _selectedItemDesc = slot.item.description;
                            _selectedItemCount = slot.count;
                            _selectedItemData = slot.item;

                            if (_contextMode == ContextMode.Warehouse)
                            {
                                // 2026-09-11(4): 창고 컨텍스트 — 창고 소스 드래그를 MouseDown에서 즉시 시작
                                // (WarehouseUI 선례. ProcessDrag의 창고 소스 분기가 MouseUp 판정 대행)
                                _dragItemData = null;
                                _dragActive = false;
                                _dragSlotGlobalIndex = -1;
                                ItemDragContext.Begin(ItemDragContext.Source.Warehouse,
                                    GetWarehouseSlotIndex(i), slot.item, _warehouseTerritoryId);
                            }
                            else
                            {
                                _dragItemData = slot.item;   // ProcessDrag: MouseDrag Begin → MouseUp에서 드롭 판정
                                _dragActive = false;
                                _dragSlotGlobalIndex = GetGlobalSlotIndex(_selectedCategory, i);
                            }
                            Event.current.Use();
                        }
                        else if (Event.current.button == 1) // 우클릭 — 인벤: 장비 장착/오토루트, 창고: 인벤 이동
                        {
                            if (_contextMode == ContextMode.Warehouse)
                            {
                                // 2026-09-11(4): 창고 컨텍스트 우클릭 — 1개를 인벤으로 이동
                                // (창고 아이템 직접 장착은 인벤에 없는 장비가 장착되는 부작용 — TryEquipItem은 인벤 소유 아이템에만 유지)
                                int whIdx = GetWarehouseSlotIndex(i);
                                if (whIdx >= 0 && WarehouseSystem.Instance != null
                                    && !string.IsNullOrEmpty(_warehouseTerritoryId)
                                    && WarehouseSystem.Instance.TransferToInventory(_warehouseTerritoryId, whIdx, 1))
                                {
                                    Debug.Log($"[InventoryWindow] 창고→인벤 이동(우클릭): {slot.item.displayName}");
                                    RefreshInventory();
                                }
                            }
                            else if (CompareTooltip.IsEquipmentCategory(slot.item.category))
                            {
                                // 2026-09-09: 무기 선택창 폐지 → 우클릭 장착으로 대체
                                TryEquipItem(slot);
                            }
                            else if (AutoRouteSystem.Instance != null)
                            {
                                // AutoRouteSystem에서 경로 조회
                                if (AutoRouteSystem.Instance != null)
                                {
                                    var route = AutoRouteSystem.Instance.GetRouteForItem(slot.item.id);
                                    if (route.HasValue)
                                    {
                                        var routeData = route.Value;
                                        // 영지 이름 조회
                                        string territoryName = routeData.territoryId;
                                        if (TerritoryDatabase.Instance != null)
                                        {
                                            var def = TerritoryDatabase.Instance.GetDefinition(routeData.territoryId);
                                            if (def.id.nation != NationType.None && !string.IsNullOrEmpty(def.territoryName))
                                            {
                                                territoryName = def.territoryName;
                                            }
                                        }

                                        _showRouteContextMenu = true;
                                        _routeContextMenuRect = new Rect(Event.current.mousePosition.x, Event.current.mousePosition.y, 360f, 110f);
                                        _routeContextItemName = slot.item.displayName;
                                        _routeContextItemId = slot.item.id;
                                        _routeContextTerritoryName = territoryName;
                                        _routeContextTerritoryId = routeData.territoryId;
                                        _routeContextSlot = slot;
                                    }
                                }
                            }
                            Event.current.Use();
                        }
                    }
                    // 툴팁 (마우스 호버 시)
                    if (isHover)
                    {
                        DrawSlotTooltip(Event.current.mousePosition + new Vector2(22, 22), slot);
                    }
                }
            }

            GUI.EndScrollView();
        }

        private void DrawSlotTooltip(Vector2 position, PlayerInventory.ItemSlot slot)
        {
            if (slot == null || slot.item == null) return;

            float tooltipWidth = 450;
            float baseHeight = slot.item.maxDurability > 0 ? 80f : 60f;

            // 장비 비교 섹션 높이 계산
            float compareHeight = 0f;
            bool isEquipment = CompareTooltip.IsEquipmentCategory(slot.item.category);
            if (isEquipment)
            {
                compareHeight = CompareTooltip.CalculateCompareHeight(tooltipWidth - 10);
            }

            float tooltipHeight = baseHeight + compareHeight;
            Rect tooltipRect = new Rect(position.x, position.y, tooltipWidth, tooltipHeight);
            if (tooltipRect.xMax > Screen.width) tooltipRect.x = Screen.width - tooltipWidth;
            if (tooltipRect.yMax > Screen.height) tooltipRect.y = Screen.height - tooltipHeight;
            GUI.Box(tooltipRect, "", _styleSlot);
            GUI.Label(new Rect(tooltipRect.x + 5, tooltipRect.y + 5, tooltipWidth - 10, 30), slot.item.displayName, _styleSlotLabel);
            GUI.Label(new Rect(tooltipRect.x + 5, tooltipRect.y + 25, tooltipWidth - 10, 30), slot.item.description, _styleInfoDesc);
            GUI.Label(new Rect(tooltipRect.x + 5, tooltipRect.y + 45, tooltipWidth - 10, 30), $"x{slot.count}", _styleItemCount);
            if (slot.item.maxDurability > 0)
            {
                string durStr = ProjectName.Systems.EquipmentDurabilitySystem.GetDurabilityString(slot);
                GUI.Label(new Rect(tooltipRect.x + 5, tooltipRect.y + 62, tooltipWidth - 10, 24), $"내구도: {durStr}", _styleItemCount);
            }

            // ──── 장비 비교 섹션 ────
            if (isEquipment)
            {
                var equippedData = CompareTooltip.GetEquippedCompareData(slot.item.category);
                if (equippedData.HasValue)
                {
                    float cx = tooltipRect.x + 5;
                    float cy = tooltipRect.y + baseHeight + 2;
                    float cw = tooltipWidth - 10;
                    var newItemData = new ItemTooltipData
                    {
                        itemName = slot.item.displayName,
                        description = slot.item.description,
                        effects = slot.item.effects,
                        rarity = slot.item.rarity,
                        category = slot.item.category,
                        maxDurability = slot.item.maxDurability,
                        currentDurability = slot.currentDurability,
                        count = slot.count
                    };
                    CompareTooltip.DrawComparison(cx, cy, cw, newItemData, equippedData.Value, _texWhite);
                }
            }
        }


        // ===================================================================
        // 하단 정보 패널
        // ===================================================================
        private void DrawInfoPanel(float panelX, float infoY)
        {
            DrawColoredRect(new Rect(panelX, infoY, WINDOW_WIDTH, INFO_PANEL_HEIGHT), ColorBg);
            DrawColoredRect(new Rect(panelX, infoY, WINDOW_WIDTH, 1), ColorBorder);

            float innerX = panelX + 8;
            float innerY = infoY + 4;
            float innerWidth = WINDOW_WIDTH - 16;

            if (!string.IsNullOrEmpty(_selectedItemName))
            {
                // 아이콘 (ItemIconDatabase 사용)
                Texture2D iconTex = null;
                if (_selectedSlotIndex >= 0 && _currentSlots != null && _selectedSlotIndex < _currentSlots.Length)
                {
                    var selSlot = _currentSlots[_selectedSlotIndex];
                    if (selSlot != null && selSlot.item != null)
                        iconTex = ItemIconDatabase.GetOrCreateIcon(selSlot.item);
                }

                if (iconTex != null)
                {
                    GUI.DrawTexture(new Rect(innerX, innerY + 2, 48, 48), iconTex);
                }
                else
                {
                    // 폴백: 카테고리 색상 사각형
                    Color iconColor = GetCategoryColorForSelected();
                    GUI.color = iconColor;
                    float iconSize = _selectedSlotIndex >= 0 && _currentSlots != null && _selectedSlotIndex < _currentSlots.Length ? 48 : 36;
                    GUI.DrawTexture(new Rect(innerX, innerY + 2, iconSize, iconSize), _texWhite);
                    GUI.color = Color.white;
                }

                // 이름
                GUI.Label(new Rect(innerX + 44, innerY, innerWidth - 44, 40), _selectedItemName, _styleInfoName);

                // 설명
                GUI.Label(new Rect(innerX + 44, innerY + 32, innerWidth - 44, 68), _selectedItemDesc, _styleInfoDesc);

                // 보유 개수
                GUI.Label(new Rect(innerX, innerY + 80, innerWidth, 38),
                    $"보유: {_selectedItemCount}개", _styleInfoLabel);

                // C9-18: 선택된 아이템 내구도 정보 (장비만)
                if (_selectedSlotIndex >= 0 && _currentSlots != null &&
                    _selectedSlotIndex < _currentSlots.Length && _currentSlots[_selectedSlotIndex].item.maxDurability > 0)
                {
                    var selSlot = _currentSlots[_selectedSlotIndex];
                    string durStr = ProjectName.Systems.EquipmentDurabilitySystem.GetDurabilityString(selSlot);
                    float ratio = ProjectName.Systems.EquipmentDurabilitySystem.GetDurabilityRatio(selSlot);
                    Color durColor = ratio >= 0.6f ? Color.green :
                                     ratio >= 0.3f ? Color.yellow : Color.red;
                    var oldColor = GUI.color;
                    GUI.color = durColor;
                    GUI.Label(new Rect(innerX + 150, innerY + 80, innerWidth - 150, 38),
                        $"내구도: {durStr}", _styleInfoLabel);
                    GUI.color = oldColor;

                    // C9-19: 수리 버튼 (내구도가 가득 차지 않았을 때만)
                    if (ratio < 1f)
                    {
                        // _selectedSlotIndex는 필터링된 _currentSlots의 인덱스 — 전역 인덱스로 변환
                        int globalSlotIdx = GetGlobalSlotIndex(_selectedCategory, _selectedSlotIndex);
                        if (globalSlotIdx >= 0 && GUI.Button(new Rect(innerX + innerWidth - 125, innerY + 80, 252, 42), "🔧 수리"))
                        {
                            var result = ProjectName.Systems.EquipmentRepairSystem.RepairInventorySlot(globalSlotIdx);

                            _selectedItemDesc = result.message;
                            if (result.success)
                            {
                                // 인벤토리 갱신
                                RefreshInventory();
                                // 선택된 슬롯 다시 찾기 (갱신 후)
                                FindAndSelectSlot(selSlot.item.id);
                            }
                        }
                    }
                } // end if (maxDurability > 0)

                // 사용 버튼 (소비 가능한 아이템만) — durability 블록 외부에서 독립적 처리
                if (IsConsumable(_selectedCategory))
                {
                    if (GUI.Button(new Rect(innerX, innerY + 85, innerWidth - 16, 38), "사용"))
                    {
                        if (_selectedSlotIndex >= 0 && _currentSlots != null && _selectedSlotIndex < _currentSlots.Length)
                        {
                            PlayerInventory.Instance.UseItemFromCategory(_selectedCategory, _selectedSlotIndex);
                            // 선택 초기화
                            _selectedItemName = "";
                            _selectedItemDesc = "";
                            _selectedItemCount = 0;
                        }
                    }
                }

            } // end if (!string.IsNullOrEmpty(_selectedItemName))
            else
            {
                GUI.Label(new Rect(innerX, innerY + 20, innerWidth, 45),
                    "아이템을 선택하면 상세 정보가 표시됩니다.", _styleEmptyText);
            }
        }

        // ===================================================================
        // 무기 슬롯 섹션 (장착/해제) — WeaponEquipManager 연동
        // "무기" 라벨 + 강철검/크리스탈검/돌검/나무검/해제 버튼 5개.
        // 현재 장착 중인 검 버튼은 배경색(황금색 강조) 하이라이트.
        // ===================================================================
        private void DrawWeaponSection(float panelX, float sectionY)
        {
            DrawColoredRect(new Rect(panelX, sectionY, WINDOW_WIDTH, WEAPON_SECTION_HEIGHT), ColorBg);
            DrawColoredRect(new Rect(panelX, sectionY, WINDOW_WIDTH, 1), ColorBorder);

            // 좌측 "무기" 라벨
            float labelWidth = 180f;
            GUI.Label(new Rect(panelX + 16f, sectionY + 20f, labelWidth, WEAPON_SECTION_HEIGHT - 36f),
                "무기", _styleItemName);

            // 버튼 정의: id (null = 해제 버튼)
            string[] weaponIds   = { "steel", "crystal", "stone", "wood", null };
            string[] weaponLabels = { "강철검", "크리스탈검", "돌검", "나무검", "해제" };

            float btnAreaX = panelX + 16f + labelWidth;
            float btnAreaWidth = WINDOW_WIDTH - 32f - labelWidth;
            float btnGap = 8f;
            float btnWidth = (btnAreaWidth - btnGap * (weaponIds.Length - 1)) / weaponIds.Length;
            float btnY = sectionY + 16f;
            float btnHeight = WEAPON_SECTION_HEIGHT - 34f;

            // 플레이어 트랜스폰 (캐시) — Equip에 전달
            Transform playerT = GetPlayerTransform();

            string equippedId = WeaponEquipManager.CurrentId;
            for (int i = 0; i < weaponIds.Length; i++)
            {
                string id = weaponIds[i];
                bool isEquippedThis = id != null && equippedId == id;
                string label = isEquippedThis ? $"✔ {weaponLabels[i]}" : weaponLabels[i];

                Rect btnRect = new Rect(btnAreaX + i * (btnWidth + btnGap), btnY, btnWidth, btnHeight);

                // 현재 장착 중인 검 버튼 하이라이트 (배경색 변경)
                Color prevBg = GUI.backgroundColor;
                if (isEquippedThis) GUI.backgroundColor = ColorAccent;
                if (GUI.Button(btnRect, label))
                {
                    if (id != null)
                    {
                        if (playerT != null)
                            WeaponEquipManager.Equip(id, playerT);
                    }
                    else
                    {
                        WeaponEquipManager.Unequip();
                    }
                }
                GUI.backgroundColor = prevBg;
            }
        }

        // ===================================================================
        // 우측 캐릭터 프리뷰 패널 틀 (T3B-2) — 2분할 레이아웃의 우측 영역.
        // 이번 단계는 패널 골자만: 배경 + 헤더 + 3D 프리뷰 자리(플레이스홀더) + 장착 무기 표시.
        // 실제 3D 프리뷰(RenderTexture)는 다음 단계에서 previewRect 자리에 연결.
        // ===================================================================
        private void DrawPreviewPanel(float panelX, float panelY, float panelHeight)
        {
            // 패널 배경
            DrawColoredRect(new Rect(panelX, panelY, PREVIEW_PANEL_WIDTH, panelHeight), ColorTitleBar);
            // 좌측 구분선 (그리드 영역과의 경계 — 얇은 금색)
            DrawColoredRect(new Rect(panelX, panelY, 1, panelHeight), ColorBorder);

            float pad = 16f;
            float innerW = PREVIEW_PANEL_WIDTH - pad * 2;

            // 헤더 라벨
            GUI.Label(new Rect(panelX + pad, panelY + 8f, innerW, 70f), "🧝 캐릭터", _styleItemName);
            DrawColoredRect(new Rect(panelX + pad, panelY + 86f, innerW, 1), ColorGridLine);

            // 3D 프리뷰 (RenderTexture) — 준비 실패 시 기존 "캐릭터 프리뷰" 플레이스홀더 유지
            float previewTop = panelY + 104f;
            float previewHeight = panelHeight - 104f - 180f;
            Rect previewRect = new Rect(panelX + pad, previewTop, innerW, Mathf.Max(60f, previewHeight));
            DrawColoredRect(previewRect, ColorSlotEmptyCell);
            DrawRectBorder(previewRect, ColorGridLine, 1f);
            EnsurePreviewSetup();   // OnShow 누락 대비 지연 생성 (이미 준비됐거나 실패했으면 no-op)
            if (_previewRT != null)
            {
                RefreshPreviewWeapon();   // 장착 무기 변경 즉시 반영 (내부 가드로 중복 부착 방지)
                GUI.DrawTexture(previewRect, _previewRT, ScaleMode.ScaleToFit);
            }
            else
            {
                GUI.Label(new Rect(previewRect.x, previewRect.y + previewRect.height * 0.5f - 22f, previewRect.width, 44f),
                    "캐릭터 프리뷰", _styleEmptyText);
            }

            // 장착 무기 표시
            float equipY = panelY + panelHeight - 170f;
            GUI.Label(new Rect(panelX + pad, equipY, innerW, 38f), "장착 무기", _styleInfoLabel);
            string equippedId = WeaponEquipManager.CurrentId;
            bool hasEquipped = !string.IsNullOrEmpty(equippedId);
            var oldColor = GUI.color;
            GUI.color = hasEquipped ? ColorMintEdge : ColorTextSecondary;
            GUI.Label(new Rect(panelX + pad, equipY + 46f, innerW, 70f),
                hasEquipped ? TruncateText(GetEquippedWeaponDisplayName(equippedId), innerW, _styleItemName) : "장착하지 않음",
                _styleItemName);
            GUI.color = oldColor;
        }

        // ===================================================================
        // 3D 캐릭터 프리뷰 (RenderTexture) — 인벤토리 열 때(OnShow) 생성,
        // 닫을 때(OnHide)/파괴 시(OnDestroy) 정리. 월드 플레이어 인스턴스에는
        // 영향 없음(fresh clone + 원격 위치 + 전용 카메라가 개체만 비춤).
        // ===================================================================

        /// <summary>프리뷰 개체/카메라/RT 생성. 실패 시 자원 정리 후 플레이스홀더 폴백(크래시 없음).</summary>
        private void EnsurePreviewSetup()
        {
            if (_previewRT != null) return;   // 이미 성공
            if (_previewSetupTried) return;   // 이번 열림에서 실패한 적 있음 → 재시도 없음(스팸 방지)
            _previewSetupTried = true;

            try
            {
                // ① 플레이어 모델 fresh instance (월드 플레이어와 별개)
                var prefab = Resources.Load<GameObject>("Models/UserProvided/fbx/Player_Rigged_Heat");
                if (prefab == null)
                {
                    Debug.LogWarning("[InventoryWindow] 프리뷰 플레이어 모델 로드 실패 — 플레이스홀더 유지");
                    return;
                }
                _previewModel = Instantiate(prefab);
                _previewModel.name = "InventoryPreviewBody";
                // 씬 물체와 겹치지 않는 원격 위치 (전용 카메라가 개체만 비추도록 far clip과 조합)
                Vector3 remotePos = new Vector3(10000f, 1000f, 10000f);
                _previewModel.transform.position = remotePos;

                // 스케일 정규화 (GameSetup과 동일: 키 ~1.8m) + 원격 위치 바닥 기준 정렬
                var rends = _previewModel.GetComponentsInChildren<Renderer>();
                if (rends.Length > 0)
                {
                    var b = rends[0].bounds;
                    foreach (var r in rends) b.Encapsulate(r.bounds);
                    float h = b.size.y;
                    if (h > 0.01f) _previewModel.transform.localScale *= 1.8f / h;
                    var b2 = rends[0].bounds;
                    foreach (var r in rends) b2.Encapsulate(r.bounds);
                    _previewModel.transform.position += new Vector3(0f, remotePos.y - b2.min.y, 0f);
                }

                // 머티리얼 복사(월드 플레이어와 동일 외형) + 애니 컨트롤러(실패 시 스태틱 포즈 허용)
                try { HumanoidClipDriver.CopyMaterialsFromGlb(_previewModel, "Models/UserProvided/Player_Rigged"); }
                catch (System.Exception matEx) { Debug.LogWarning("[InventoryWindow] 프리뷰 머티리얼 복사 실패(무시): " + matEx.Message); }
                var anim = _previewModel.GetComponent<Animator>();
                if (anim == null) anim = _previewModel.AddComponent<Animator>();
                var ctrl = Resources.Load<RuntimeAnimatorController>("Animation/Controllers/Player_AC");
                if (ctrl != null)
                {
                    anim.runtimeAnimatorController = ctrl;
                    anim.applyRootMotion = false;
                    anim.updateMode = AnimatorUpdateMode.UnscaledTime;   // 일시정지 중에도 프리뷰 애니 유지
                }

                // ② RenderTexture + 전용 카메라 (원격 위치 + 짧은 far clip → 프리뷰 개체만 촬영)
                _previewRT = new RenderTexture(512, 640, 24, RenderTextureFormat.ARGB32);
                _previewRT.name = "InventoryPreviewRT";
                _previewRT.Create();

                var camGo = new GameObject("InventoryPreviewCamera");
                _previewCamera = camGo.AddComponent<Camera>();
                _previewCamera.clearFlags = CameraClearFlags.SolidColor;
                _previewCamera.backgroundColor = new Color(0.06f, 0.06f, 0.09f, 1f);
                _previewCamera.cullingMask = ~0;
                _previewCamera.nearClipPlane = 0.1f;
                _previewCamera.farClipPlane = 20f;
                _previewCamera.fieldOfView = 30f;
                _previewCamera.targetTexture = _previewRT;

                // 프레이밍: 렌더러 bounds 중심을 살짝 사선에서 바라보도록 카메라 배치
                var rb = rends[0].bounds;
                foreach (var r in rends) rb.Encapsulate(r.bounds);
                Vector3 center = rb.center;
                float halfH = Mathf.Max(0.5f, rb.extents.y * 1.25f);
                float dist = halfH / Mathf.Tan(_previewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                _previewCamera.transform.position = center + new Vector3(dist * 0.35f, 0f, -dist);
                _previewCamera.transform.LookAt(center);

                // ③ 장착 무기 반영 (실패 시 무시)
                RefreshPreviewWeapon();

                // 첫 프레임 내용 보장 (이후에는 카메라 자동 렌더로 idle 애니 반영)
                try { _previewCamera.Render(); }
                catch { /* 첫 렌더 실패 무시 — 자동 렌더가 이어서 처리 */ }

                Debug.Log("[InventoryWindow] ✅ 3D 캐릭터 프리뷰 준비 완료 (RenderTexture 512×640)");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[InventoryWindow] 3D 프리뷰 생성 실패 — 플레이스홀더 유지: " + e.Message);
                ReleasePreview();
            }
        }

        /// <summary>프리뷰 개체 손에 현재 장착 무기 부착 (WeaponEquipManager와 별개 인스턴스, 실패 시 무시).</summary>
        private void RefreshPreviewWeapon()
        {
            if (_previewModel == null) return;
            string id = WeaponEquipManager.CurrentId;
            // 동일 id + (부착 완료 또는 비장착)면 no-op — 매 프레임 호출 안전
            if (id == _previewWeaponId && (_previewWeapon != null || string.IsNullOrEmpty(id))) return;

            // 기존 프리뷰 검 제거
            if (_previewWeapon != null) { Destroy(_previewWeapon); _previewWeapon = null; }
            _previewWeaponId = id;

            if (string.IsNullOrEmpty(id)) return;

            try
            {
                var prefab = Resources.Load<GameObject>("Models/UserProvided/" + id + "_sword");
                if (prefab == null) return;   // 무기 프리팹 없음 — 스킵 (규격 준수)

                var animator = _previewModel.GetComponentInChildren<Animator>();
                var handBone = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
                if (handBone == null) return;

                _previewWeapon = Instantiate(prefab, handBone);
                _previewWeapon.name = "Preview_" + id + "_sword";
                // 부착 규격: WeaponEquipManager와 동일 튜닝 값
                _previewWeapon.transform.localPosition = new Vector3(0f, 0.12f, 0.02f);
                _previewWeapon.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

                var wRends = _previewWeapon.GetComponentsInChildren<Renderer>();
                if (wRends.Length > 0)
                {
                    var wb = wRends[0].bounds;
                    foreach (var r in wRends) wb.Encapsulate(r.bounds);
                    float len = Mathf.Max(wb.size.x, Mathf.Max(wb.size.y, wb.size.z));
                    if (len > 0.01f) _previewWeapon.transform.localScale *= 0.9f / len;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[InventoryWindow] 프리뷰 무기 부착 실패(무시): " + e.Message);
                if (_previewWeapon != null) { Destroy(_previewWeapon); _previewWeapon = null; }
            }
        }

        /// <summary>프리뷰 자원 정리 (OnHide/OnDestroy에서 호출 — RT/카메라/개체 파괴, 다음 열림에서 재시도 가능).</summary>
        private void ReleasePreview()
        {
            _previewSetupTried = false;
            if (_previewWeapon != null) { Destroy(_previewWeapon); _previewWeapon = null; }
            _previewWeaponId = null;
            if (_previewModel != null) { Destroy(_previewModel); _previewModel = null; }
            if (_previewCamera != null) { Destroy(_previewCamera.gameObject); _previewCamera = null; }
            if (_previewRT != null)
            {
                _previewRT.Release();
                Destroy(_previewRT);
                _previewRT = null;
            }
        }

        /// <summary>WeaponEquipManager 무기 ID → 표시 이름 (DrawWeaponSection 버튼과 동일 매핑).</summary>
        private string GetEquippedWeaponDisplayName(string id)
        {
            switch (id)
            {
                case "steel": return "강철검";
                case "crystal": return "크리스탈검";
                case "stone": return "돌검";
                case "wood": return "나무검";
                default: return id;
            }
        }

        /// <summary>문자열에서 첫 번째 연속 숫자 추출 (무기 공격력 수치 표시용). 없으면 null.</summary>
        private string ExtractFirstNumber(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            int start = -1, end = -1;
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsDigit(text[i]))
                {
                    if (start < 0) start = i;
                    end = i;
                }
                else if (start >= 0) break;
            }
            return start >= 0 ? text.Substring(start, end - start + 1) : null;
        }

        // 플레이어 트랜스폰 캐시 (무기 슬롯용)
        private Transform _cachedPlayerT;
        private float _playerCacheTime;

        /// <summary>
        /// 플레이어 트랜스폰 획득 — PlayerMovement(InChildren Animator 보유) 우선,
        /// 실패 시 Player 태그 폴백. IMGUI 프레임 호출 스팸 방지를 위해 1초 캐시.
        /// </summary>
        private Transform GetPlayerTransform()
        {
            if (_cachedPlayerT != null && Time.unscaledTime - _playerCacheTime < 1f)
                return _cachedPlayerT;

            _cachedPlayerT = null;
            var pm = FindAnyObjectByType<PlayerMovement>();
            if (pm != null) _cachedPlayerT = pm.transform;
            if (_cachedPlayerT == null)
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) _cachedPlayerT = tagged.transform;
            }
            _playerCacheTime = Time.unscaledTime;
            return _cachedPlayerT;
        }

        // ===== 인벤토리 정렬 =====
        private void SortInventory()
        {
            if (_contextMode == ContextMode.Warehouse) return;   // 2026-09-11(4): 창고 컨텍스트는 시딩 순서 유지 (플레이어 인벤 정렬 아님)
            if (PlayerInventory.Instance == null) return;
            var allSlots = PlayerInventory.Instance.GetAllSlots();
            if (allSlots == null) return;

            // null이 아닌 슬롯만 리스트로 추출
            var nonEmpty = new System.Collections.Generic.List<PlayerInventory.ItemSlot>();
            for (int i = 0; i < allSlots.Length; i++)
            {
                if (allSlots[i] != null && allSlots[i].item != null && allSlots[i].count > 0)
                    nonEmpty.Add(allSlots[i]);
            }

            // 정렬 기준에 따라 정렬
            switch (_sortMode)
            {
                case SortMode.Category:
                    nonEmpty.Sort((a, b) =>
                    {
                        int catCompare = GetCategorySortOrder(a.item.category).CompareTo(GetCategorySortOrder(b.item.category));
                        if (catCompare != 0) return catCompare;
                        return string.Compare(a.item.displayName, b.item.displayName, System.StringComparison.Ordinal);
                    });
                    break;
                case SortMode.Name:
                    nonEmpty.Sort((a, b) => string.Compare(a.item.displayName, b.item.displayName, System.StringComparison.Ordinal));
                    break;
                case SortMode.Rarity:
                    nonEmpty.Sort((a, b) =>
                    {
                        int rCompare = a.item.rarity.CompareTo(b.item.rarity);
                        if (rCompare != 0) return rCompare;
                        return string.Compare(a.item.displayName, b.item.displayName, System.StringComparison.Ordinal);
                    });
                    break;
                case SortMode.Quantity:
                    nonEmpty.Sort((a, b) => b.count.CompareTo(a.count));
                    break;
            }

            // 배열 재구성: 정렬된 아이템 → 빈 슬롯
            for (int i = 0; i < allSlots.Length; i++)
            {
                if (i < nonEmpty.Count)
                    allSlots[i] = nonEmpty[i];
                else
                    allSlots[i] = null;
            }
        }

        private int GetCategorySortOrder(PlayerInventory.ItemCategory category)
        {
            return category switch
            {
                PlayerInventory.ItemCategory.Herb => 0,
                PlayerInventory.ItemCategory.Meat => 1,
                PlayerInventory.ItemCategory.Food => 2,
                PlayerInventory.ItemCategory.Potion => 3,
                PlayerInventory.ItemCategory.Material => 4,
                PlayerInventory.ItemCategory.Drug => 5,
                PlayerInventory.ItemCategory.Quest => 6,
                PlayerInventory.ItemCategory.Weapon => 7,
                PlayerInventory.ItemCategory.Armor => 8,
                PlayerInventory.ItemCategory.Tool => 9,
                PlayerInventory.ItemCategory.Arrow => 10,
                _ => 99,
            };
        }

        // ===================================================================
        // 헬퍼
        // ===================================================================

        private void SelectCategory(PlayerInventory.ItemCategory category)
        {
            if (_selectedCategory != category)
            {
                _selectedCategory = category;
                _selectedSlotIndex = -1;
                RefreshInventory();
            }
        }

        /// <summary>
        /// 인벤토리 내용 갱신
        /// </summary>
        public void RefreshInventory()
        {
            // 2026-09-11(4): 창고 컨텍스트 — WarehouseSystem.GetItems(territoryId)를 렌더한다.
            // (기존엔 컨텍스트와 무관하게 플레이어 인벤을 그려 E키 창고가 플레이어 가방을 보여줬던 원인)
            if (_contextMode == ContextMode.Warehouse)
            {
                RefreshFromWarehouse();
                return;
            }

            if (PlayerInventory.Instance == null) return;

            _currentSlots = PlayerInventory.Instance.GetSlotsByCategory(_selectedCategory);
            _selectedItemName = "";
            _selectedItemDesc = "";
            _selectedItemCount = 0;
        }

        /// <summary>2026-09-11(4): 창고 소스 — 선택 카테고리로 필터링 + 원본 슬롯 인덱스 병행 캐시(DnD용).</summary>
        private void RefreshFromWarehouse()
        {
            _currentSlots = System.Array.Empty<PlayerInventory.ItemSlot>();
            _warehouseSlotIndices = System.Array.Empty<int>();
            _selectedItemName = "";
            _selectedItemDesc = "";
            _selectedItemCount = 0;

            if (WarehouseSystem.Instance == null || string.IsNullOrEmpty(_warehouseTerritoryId)) return;

            var items = WarehouseSystem.Instance.GetItems(_warehouseTerritoryId);
            if (items == null || items.Count == 0) return;

            var filtered = new List<PlayerInventory.ItemSlot>(items.Count);
            var srcIdx = new List<int>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                var s = items[i];
                if (s != null && s.item != null && s.count > 0 && s.item.category == _selectedCategory)
                {
                    filtered.Add(s);
                    srcIdx.Add(i);
                }
            }
            _currentSlots = filtered.ToArray();
            _warehouseSlotIndices = srcIdx.ToArray();
        }

        /// <summary>2026-09-11(4): 필터 뷰 인덱스 → 창고 전역 슬롯 인덱스 (DnD 스왑/회수용). 미해당 -1.</summary>
        private int GetWarehouseSlotIndex(int filteredIndex)
        {
            if (_warehouseSlotIndices == null || filteredIndex < 0 || filteredIndex >= _warehouseSlotIndices.Length)
                return -1;
            return _warehouseSlotIndices[filteredIndex];
        }

        private Color GetCategoryColor(PlayerInventory.ItemCategory category)
        {
            return category switch
            {
                PlayerInventory.ItemCategory.Herb => new Color(0.3f, 0.8f, 0.3f),    // 초록
                PlayerInventory.ItemCategory.Meat => new Color(0.8f, 0.4f, 0.2f),    // 주황
                PlayerInventory.ItemCategory.Food => new Color(0.9f, 0.8f, 0.2f),    // 노랑
                PlayerInventory.ItemCategory.Potion => new Color(0.7f, 0.3f, 0.8f),  // 보라
                PlayerInventory.ItemCategory.Material => new Color(0.5f, 0.5f, 0.5f),// 회색
                PlayerInventory.ItemCategory.Quest => new Color(0.2f, 0.7f, 0.8f),   // 청록
                PlayerInventory.ItemCategory.Weapon => new Color(0.8f, 0.3f, 0.3f),  // 빨강
                PlayerInventory.ItemCategory.Armor => new Color(0.3f, 0.3f, 0.8f),   // 파랑
                PlayerInventory.ItemCategory.Tool => new Color(0.6f, 0.4f, 0.2f),    // 갈색
                _ => Color.gray,
            };
        }

        private Color GetCategoryColorForSelected()
        {
            if (_selectedSlotIndex >= 0 && _currentSlots != null && _selectedSlotIndex < _currentSlots.Length)
            {
                var slot = _currentSlots[_selectedSlotIndex];
                if (slot != null && slot.item != null)
                    return GetCategoryColor(slot.item.category);
            }
            return Color.gray;
        }

        private bool IsConsumable(PlayerInventory.ItemCategory category)
        {
            return category == PlayerInventory.ItemCategory.Herb ||
                   category == PlayerInventory.ItemCategory.Meat ||
                   category == PlayerInventory.ItemCategory.Food ||
                   category == PlayerInventory.ItemCategory.Potion;
        }

        /// <summary>1x1 텍스처 생성</summary>
        private Texture2D MakeTexture(int w, int h, Color color)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, color);
            tex.Apply();
            return tex;
        }

        /// <summary>테두리가 있는 단색 텍스처 생성 (슬롯/버튼 배경용 — 젤다 스타일 흰 테두리 박스).</summary>
        private Texture2D MakeBorderedTexture(int w, int h, Color background, Color border, int borderPx)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool isBorder = x < borderPx || y < borderPx || x >= w - borderPx || y >= h - borderPx;
                    tex.SetPixel(x, y, isBorder ? border : background);
                }
            }
            tex.Apply();
            return tex;
        }

        /// <summary>라운드 코너 + 테두리 텍스처 생성 (T3B-1 빈 슬롯 가이드 셀용 — 라운드 사각 SDF).</summary>
        private Texture2D MakeRoundedBorderedTexture(int w, int h, Color background, Color border, int borderPx, int corner)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            float halfW = (w - 1) * 0.5f, halfH = (h - 1) * 0.5f;
            float radW = halfW - corner, radH = halfH - corner;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float qx = Mathf.Abs(x - halfW) - radW;
                    float qy = Mathf.Abs(y - halfH) - radH;
                    float ox = Mathf.Max(qx, 0f);
                    float oy = Mathf.Max(qy, 0f);
                    float dist = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - corner;
                    if (dist > 0.5f) tex.SetPixel(x, y, Color.clear);                  // 라운드 밖 — 투명
                    else if (dist > -borderPx - 0.5f) tex.SetPixel(x, y, border);      // 테두리
                    else tex.SetPixel(x, y, background);                               // 내부
                }
            }
            tex.Apply();
            return tex;
        }

        /// <summary>필터링된 카테고리 인덱스를 전역 슬롯 인덱스로 변환</summary>
        private int GetGlobalSlotIndex(PlayerInventory.ItemCategory category, int filteredIndex)
        {
            if (PlayerInventory.Instance == null || filteredIndex < 0) return -1;
            int count = -1;
            var allSlots = PlayerInventory.Instance.GetAllSlots();
            for (int i = 0; i < allSlots.Length; i++)
            {
                var slot = allSlots[i];
                if (slot != null && slot.item != null && slot.item.category == category)
                {
                    count++;
                    if (count == filteredIndex)
                        return i;
                }
            }
            return -1;
        }

        // ===== 퀵슬롯 연동 (QuickSlotUI에서 호출) =====

        /// <summary>
        /// 현재 선택된 아이템의 ItemData 반환 (없으면 null)
        /// </summary>
        public PlayerInventory.ItemData GetSelectedItemData()
        {
            if (_selectedSlotIndex < 0 || _currentSlots == null || _selectedSlotIndex >= _currentSlots.Length)
                return null;
            var slot = _currentSlots[_selectedSlotIndex];
            if (slot == null) return null;
            return slot.item;
        }

        /// <summary>
        /// 현재 선택된 아이템의 개수 반환
        /// </summary>
        public int GetSelectedItemCount()
        {
            if (_selectedSlotIndex < 0 || _currentSlots == null || _selectedSlotIndex >= _currentSlots.Length)
                return 0;
            var slot = _currentSlots[_selectedSlotIndex];
            if (slot == null) return 0;
            return slot.count;
        }

        /// <summary>
        /// 현재 선택된 아이템이 있는지 확인
        /// </summary>
        public bool HasSelectedItem()
        {
            if (_selectedSlotIndex < 0 || _currentSlots == null || _selectedSlotIndex >= _currentSlots.Length)
                return false;
            var slot = _currentSlots[_selectedSlotIndex];
            return slot != null && slot.item != null && slot.count > 0;
        }

        /// <summary>아이템 ID로 슬롯 찾아 선택</summary>
        private void FindAndSelectSlot(string itemId)
        {
            if (_currentSlots == null) return;
            for (int i = 0; i < _currentSlots.Length; i++)
            {
                if (_currentSlots[i] != null && _currentSlots[i].item.id == itemId)
                {
                    _selectedSlotIndex = i;
                    _selectedItemName = _currentSlots[i].item.displayName;
                    _selectedItemDesc = _currentSlots[i].item.description;
                    _selectedItemCount = _currentSlots[i].count;
                    return;
                }
            }
            _selectedSlotIndex = -1;
        }

        // 캐시된 GUIContent (TruncateText GC 절감)
        private readonly GUIContent _truncateContent = new GUIContent();

        /// <summary>컬러 사각형 그리기</summary>
        // ===================================================================
        // 2026-09-09: 하단 장비슬롯 6종 (우클릭 해제)
        // ===================================================================
        private void DrawEquipRow(float panelX, float rowY)
        {
            // 2026-09-09(2): 장비창 5칸씩 2줄 (예시2) — 실장비 6종 + 예약 4칸
            var em = ProjectName.Systems.EquipmentManager.Instance;
            string[] slotNames = { "투구", "상의", "무기", "신발", "장갑", "등", "예약", "예약", "예약", "예약" };
            float boxW = (WINDOW_WIDTH - 16f - 4f * 10f) / 5f;
            float boxH = 86f;

            for (int i = 0; i < 10; i++)
            {
                int col = i % 5;
                int row = i / 5;
                bool reserved = i >= 6;
                float sx = panelX + 8f + col * (boxW + 10f);
                float sy = rowY + row * (boxH + 44f);
                Rect boxRect = new Rect(sx, sy, boxW, boxH);

                string itemId = null;
                if (!reserved && em != null)
                {
                    var data = em.GetSlotData((ProjectName.Systems.EquipmentManager.EquipmentSlot)i);
                    if (data != null) itemId = data.itemId;
                }
                bool has = !string.IsNullOrEmpty(itemId);

                // === AAA Layer 2/3: 엠보싱 셀 + 장착 중 골드 테두리 (기존 박스 스타일 대체) ===
                var prevEquipColor = GUI.color;
                GUI.color = reserved ? new Color(1f, 1f, 1f, 0.55f) : Color.white;   // 예약 칸은 딤 처리
                GUI.DrawTexture(boxRect, InventoryArtLibrary.GetSlotCell());
                if (has)
                    DrawSlotTint(InventoryArtLibrary.GetSlotHighlight(), boxRect, ColorAccent);   // 장착 중 = 골드 테두리
                GUI.color = prevEquipColor;
                GUI.Label(new Rect(sx, sy + boxH + 2f, boxW, 20f), slotNames[i], _styleSlotLabel);
                if (has)
                    GUI.Label(new Rect(sx - 8f, sy + boxH + 20f, boxW + 16f, 20f),
                        ProjectName.Systems.EquipmentStatBonusApplier.DisplayName(itemId), _styleItemName);

                // 우클릭 해제 (실장비 슬롯만)
                if (has && !reserved && Event.current.type == EventType.MouseDown && Event.current.button == 1
                    && boxRect.Contains(Event.current.mousePosition))
                {
                    em?.UnequipSlot((ProjectName.Systems.EquipmentManager.EquipmentSlot)i);
                    Event.current.Use();
                }
            }
        }

        // ===================================================================
        // AAA 4레이어 드로우 헬퍼 (InventoryArtLibrary 소비)
        // ===================================================================

        /// <summary>드롭섀도우 — 창 rect를 12px 사방 확장해 소프트 섀도우 텍스처를 검정 tint로 단일 DrawTexture.</summary>
        private void DrawWindowDropShadow(float wx, float wy, float ww, float wh)
        {
            var prevColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(wx - 12f, wy - 12f, ww + 24f, wh + 24f), InventoryArtLibrary.GetWindowDropShadow());
            GUI.color = prevColor;
        }

        /// <summary>Layer 4 — 금속 프레임(9-Slice Box) + 4모서리 로터스 장식(GUI.matrix 0/90/180/270° 회전, 반드시 복원).</summary>
        private void DrawWindowFrame(float wx, float wy, float ww, float wh)
        {
            // 금속 프레임 — 9-Slice (border 16)
            GUI.Box(new Rect(wx, wy, ww, wh), "", _styleMetalFrame);

            // 4모서리 장식 — 좌상단 기준 텍스처를 모서리 중심 pivot 회전으로 4방향 배치 (투명 배경이라 컨텐츠 위 안착)
            var orn = InventoryArtLibrary.GetCornerOrnament();
            const float ornSize = 96f;
            DrawCornerOrnament(orn, new Rect(wx, wy, ornSize, ornSize), 0f);
            DrawCornerOrnament(orn, new Rect(wx + ww - ornSize, wy, ornSize, ornSize), 90f);
            DrawCornerOrnament(orn, new Rect(wx + ww - ornSize, wy + wh - ornSize, ornSize, ornSize), 180f);
            DrawCornerOrnament(orn, new Rect(wx, wy + wh - ornSize, ornSize, ornSize), 270f);
        }

        /// <summary>모서리 장식 1개 — rect 중심 pivot으로 angle도 회전 후 DrawTexture, matrix 반드시 복원.</summary>
        private void DrawCornerOrnament(Texture2D tex, Rect rect, float angle)
        {
            var prevMatrix = GUI.matrix;
            if (!Mathf.Approximately(angle, 0f))
                GUIUtility.RotateAroundPivot(angle, rect.center);
            GUI.DrawTexture(rect, tex);
            GUI.matrix = prevMatrix;   // 회전 후 반드시 복원
        }

        /// <summary>타이틀 스트립 — Flat 모드: 얇은 상단 스트립(정렬 버튼 왼쪽 영역) + 하단 2px 액센트 라인.</summary>
        private void DrawTitleStrip(float wx, float wy, float ww, float sortBtnX)
        {
            float stripW = sortBtnX - wx - 4f;
            float stripH = TITLE_BAR_HEIGHT - 8f;
            GUI.DrawTexture(new Rect(wx + 2f, wy + 4f, stripW, stripH), InventoryArtLibrary.GetTitleBanner());
            DrawColoredRect(new Rect(wx, wy + TITLE_BAR_HEIGHT, ww, 2f), ColorBorder);
        }

        /// <summary>셀 위 오버레이(글로우/하이라이트) — GUI.color tint 후 반드시 복원.</summary>
        private void DrawSlotTint(Texture2D tex, Rect rect, Color tint)
        {
            var prevColor = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(rect, tex);
            GUI.color = prevColor;
        }

        // ===================================================================
        // 2026-09-09: 중앙 아이템 설명 패널 (설명 + 핫바 미니패드 드래그 지정)
        // ===================================================================
        private void DrawDescriptionPanel(float dx, float dy)
        {
            // 2026-09-09(3): 세로 축소(620) — 패드 제거, 드래그는 하단 실제 핫바로 직접 드롭
            Rect panelRect = new Rect(dx, dy, WINDOW_WIDTH, DESC_PANEL_HEIGHT);
            GUI.Box(panelRect, "", _stylePanelBox);
            DrawColoredRect(new Rect(dx, dy, WINDOW_WIDTH, 2), ColorBorder);
            DrawColoredRect(new Rect(dx, dy + DESC_PANEL_HEIGHT - 2, WINDOW_WIDTH, 2), ColorBorder);
            DrawColoredRect(new Rect(dx + WINDOW_WIDTH - 2, dy, 2, DESC_PANEL_HEIGHT), ColorBorder);
            GUI.Label(new Rect(dx, dy + 2, WINDOW_WIDTH, TITLE_BAR_HEIGHT), "  📋 아이템 정보", _styleTitle);
            DrawColoredRect(new Rect(dx, dy + TITLE_BAR_HEIGHT + 2, WINDOW_WIDTH, 2), ColorBorder);

            float cy = dy + TITLE_BAR_HEIGHT + 16f;
            if (_selectedItemData == null)
            {
                // 2026-09-11(4): 창고 컨텍스트 안내 문구 분기
                GUI.Label(new Rect(dx + 16f, cy, WINDOW_WIDTH - 32f, 40f),
                    _contextMode == ContextMode.Warehouse
                        ? "좌클릭: 선택 / 드래그: 창고 내 이동\n우클릭: 인벤토리로 1개 이동"
                        : "좌클릭: 아이템 선택 / 드래그: 하단 핫바 지정\n우클릭: 장비 장착",
                    _styleItemName);
                return;
            }

            var item = _selectedItemData;
            // 이름 + 등급
            GUI.Label(new Rect(dx + 16f, cy, WINDOW_WIDTH - 32f, 34f), item.displayName, _styleItemName);
            cy += 40f;
            GUI.Label(new Rect(dx + 16f, cy, WINDOW_WIDTH - 32f, 24f),
                $"[{item.category}]  수량: {_selectedItemCount}  등급: {item.rarity}", _styleSlotLabel);
            cy += 30f;

            // 아이콘 (있으면)
            if (item.icon != null)
            {
                GUI.DrawTexture(new Rect(dx + (WINDOW_WIDTH - 128f) / 2f, cy, 128f, 128f), item.icon.texture, ScaleMode.ScaleToFit);
                cy += 136f;
            }

            // 설명
            GUI.Label(new Rect(dx + 16f, cy, WINDOW_WIDTH - 32f, 120f), item.description ?? "", _styleSlotLabel);
            cy += 128f;

            // 효과 문자열 (있으면)
            if (!string.IsNullOrEmpty(item.effects))
                GUI.Label(new Rect(dx + 16f, cy, WINDOW_WIDTH - 32f, 60f), $"효과: {item.effects}", _styleItemName);

            // 2026-09-09(3): 미니패드 제거 — 드래그 드롭은 하단 상시 핫바(HotbarUI.GetSlotIndexAtScreenPoint)로
        }

        /// <summary>
        /// 2026-09-11(3): 드래그 고스트 + MouseUp 드롭 판정 — ItemDragContext 공유 컨텍스트 연동.
        /// - 인벤 소스: MouseDrag에서 Begin → MouseUp에 ①창고 슬롯(보관) ②다른 인벤 슬롯(스왑) ③핫바(등록),
        ///   그 외 영역은 드롭 실패로 Cancel (변경 없음)
        /// - 창고 소스: 창고 창에서 시작한 드래그의 드롭 판정을 인벤 창이 대행 (창고→인벤 이동 / 창고 내 스왑)
        /// - 고스트는 ItemDragContext.DrawGhost()가 프레임당 1회 렌더
        /// </summary>
        private void ProcessDrag()
        {
            // === 창고 소스 드래그: 인벤 창이 드롭 판정 대행 ===
            if (ItemDragContext.Active && ItemDragContext.SourceType == ItemDragContext.Source.Warehouse)
            {
                if (Event.current.type == EventType.MouseDrag)
                    Event.current.Use();
                else if (Event.current.type == EventType.MouseUp)
                {
                    Vector2 p = Event.current.mousePosition;
                    if (_contextMode == ContextMode.Warehouse)
                    {
                        // 2026-09-11(4): 창고 컨텍스트 — 그리드가 곧 창고 뷰이므로 그리드 드롭 = 창고 내 스왑.
                        // (기존 "그리드 드롭 = 인벤 이동"은 그리드가 플레이어 인벤일 때만 성립)
                        int whTarget = GetInventorySlotIndexAtScreenPoint(p);   // 창고 전역 슬롯 인덱스 캐시
                        if (whTarget >= 0 && whTarget != ItemDragContext.SourceIndex)
                        {
                            WarehouseUI.SwapDraggedSlots(whTarget);
                            RefreshInventory();
                        }
                        else if (WarehouseUI.TryGetSlotAtScreenPoint(p, out int whTarget2)
                                 && whTarget2 >= 0 && whTarget2 != ItemDragContext.SourceIndex)
                        {
                            // 별도 창고 창(WarehouseUI)이 함께 열린 경우 — 그쪽 슬롯과의 스왑
                            WarehouseUI.SwapDraggedSlots(whTarget2);
                        }
                    }
                    else if (IsPointOverInventoryGrid(p))
                    {
                        // 창고 → 인벤 이동 (1개)
                        WarehouseUI.TransferDraggedToInventory();
                        Debug.Log($"[InventoryWindow] 창고→인벤 이동(드래그): {ItemDragContext.Item?.displayName ?? "?"}");
                    }
                    else if (WarehouseUI.TryGetSlotAtScreenPoint(p, out int whTarget3)
                             && whTarget3 >= 0 && whTarget3 != ItemDragContext.SourceIndex)
                    {
                        // 창고 내 슬롯↔슬롯 스왑
                        WarehouseUI.SwapDraggedSlots(whTarget3);
                    }
                    // 그 외 영역 = 드롭 실패 → Cancel (변경 없음)
                    ItemDragContext.Cancel();
                    Event.current.Use();
                }
                ItemDragContext.DrawGhost();
                return;
            }

            if (_dragItemData == null) return;

            // === 인벤 소스: MouseDrag에서 드래그 시작 (좌클릭 누른 채 이동) ===
            if (!_dragActive && Event.current.type == EventType.MouseDrag)
            {
                _dragActive = true;
                ItemDragContext.Begin(ItemDragContext.Source.Inventory, _dragSlotGlobalIndex, _dragItemData);
                Event.current.Use();
            }

            if (_dragActive && Event.current.type == EventType.MouseDrag)
                Event.current.Use();

            if (Event.current.type == EventType.MouseUp)
            {
                if (_dragActive)
                {
                    Vector2 guiPoint = Event.current.mousePosition;
                    bool consumed = false;

                    // ① 창고 슬롯 위 드롭 → 인벤에서 1개 창고로 이동
                    if (WarehouseUI.TryGetSlotAtScreenPoint(guiPoint, out _))
                    {
                        if (WarehouseUI.TryDepositFromDrag(_dragItemData))
                        {
                            RefreshInventory();
                            Debug.Log($"[InventoryWindow] 창고 보관(드래그): {_dragItemData.displayName}");
                        }
                        consumed = true;   // 실패 시에도 롤백 완료 — 드래그 종료
                    }
                    else
                    {
                        // ② 다른 인벤 슬롯 위 드롭 → 슬롯 교체(스왑)
                        int target = GetInventorySlotIndexAtScreenPoint(guiPoint);
                        if (target >= 0 && target != _dragSlotGlobalIndex && PlayerInventory.Instance != null)
                        {
                            var all = PlayerInventory.Instance.GetAllSlots();
                            if (all != null && _dragSlotGlobalIndex >= 0
                                && _dragSlotGlobalIndex < all.Length && target < all.Length)
                            {
                                var tmp = all[_dragSlotGlobalIndex];
                                all[_dragSlotGlobalIndex] = all[target];
                                all[target] = tmp;
                                RefreshInventory();
                                Debug.Log($"[InventoryWindow] 슬롯 교체(드래그): {_dragSlotGlobalIndex} ↔ {target}");
                            }
                            consumed = true;
                        }
                    }

                    // ③ 핫바 위 드롭 → 기존 경로 유지 (등록)
                    if (!consumed)
                    {
                        int slot = HotbarUI.GetSlotIndexAtScreenPoint(Input.mousePosition);
                        if (slot >= 0)
                        {
                            HotbarUI.AssignItem(slot, _dragItemData.id, _dragItemData.displayName);
                            Debug.Log($"[InventoryWindow] 핫바 슬롯 {slot + 1}에 '{_dragItemData.displayName}' 지정");
                            consumed = true;
                        }
                    }

                    // ④ 그 외 = 드롭 실패 → 취소 (변경 없음)
                    if (!consumed)
                        Debug.Log($"[InventoryWindow] 드롭 실패 — 드래그 취소: {_dragItemData.displayName}");
                }
                ItemDragContext.Cancel();
                _dragItemData = null;
                _dragActive = false;
                _dragSlotGlobalIndex = -1;
                Event.current.Use();
                return;
            }

            // 고스트 — 공유 컨텍스트가 렌더 (프레임당 1회 가드 내장)
            ItemDragContext.DrawGhost();
        }

        // ===================================================================
        // 2026-09-09: 우클릭 장착/해제 (무기=WeaponEquipManager, 방어구=EquipmentManager)
        // ===================================================================
        private static readonly Dictionary<string, (string equipId, WeaponType type)> _weaponIdMap =
            new Dictionary<string, (string, WeaponType)>
            {
                { "steel_sword", ("steel", WeaponType.Sword) },
                { "iron_sword", ("iron", WeaponType.Sword) },
                { "crystal_bow", ("crystal", WeaponType.Bow) },
                { "wood_bow", ("wood", WeaponType.Bow) },
                { "wood_spear", ("wood", WeaponType.Spear) },
                { "spear", ("wood", WeaponType.Spear) },
            };

        private void TryEquipItem(PlayerInventory.ItemSlot slot)
        {
            if (slot?.item == null) return;
            var item = slot.item;
            var playerT = GameObject.FindWithTag("Player")?.transform;
            if (playerT == null)
            {
                Debug.LogWarning("[InventoryWindow] Player 없음 — 장착 스킵");
                return;
            }

            if (item.category == PlayerInventory.ItemCategory.Weapon)
            {
                if (_weaponIdMap.TryGetValue(item.id, out var w))
                {
                    ProjectName.Systems.WeaponEquipManager.Equip(w.equipId, playerT, w.type);
                    Debug.Log($"[InventoryWindow] 무기 장착: {item.displayName}");
                }
                else
                {
                    Debug.LogWarning($"[InventoryWindow] 무기 장착 매핑 없음: {item.id}");
                }
                return;
            }

            if (item.category == PlayerInventory.ItemCategory.Armor)
            {
                var em = ProjectName.Systems.EquipmentManager.Instance;
                if (em == null) return;
                var equipSlot = MapArmorSlot(item.id);
                em.EquipItem(slot, equipSlot);
                Debug.Log($"[InventoryWindow] 방어구 장착: {item.displayName} → {equipSlot}");
                return;
            }

            Debug.Log($"[InventoryWindow] 장착 불가 카테고리: {item.category}");
        }

        private static ProjectName.Systems.EquipmentManager.EquipmentSlot MapArmorSlot(string id)
        {
            string s = (id ?? "").ToLowerInvariant();
            if (s.Contains("helmet") || s.Contains("투구")) return ProjectName.Systems.EquipmentManager.EquipmentSlot.Helmet;
            if (s.Contains("shoe") || s.Contains("boot") || s.Contains("신발")) return ProjectName.Systems.EquipmentManager.EquipmentSlot.Shoes;
            if (s.Contains("glove") || s.Contains("장갑")) return ProjectName.Systems.EquipmentManager.EquipmentSlot.Gloves;
            if (s.Contains("cape") || s.Contains("망토") || s.EndsWith("_back")) return ProjectName.Systems.EquipmentManager.EquipmentSlot.Back;
            return ProjectName.Systems.EquipmentManager.EquipmentSlot.Armor;
        }

        private void DrawColoredRect(Rect rect, Color color)
        {
            var oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _texWhite);
            GUI.color = oldColor;
        }

        /// <summary>사각형 테두리만 그리기 (4면 얇은 스트립 — 포커스 금테/가이드 보더용)</summary>
        private void DrawRectBorder(Rect rect, Color color, float thickness)
        {
            DrawColoredRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawColoredRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawColoredRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawColoredRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        /// <summary>텍스트 길이 제한</summary>
        private string TruncateText(string text, float maxWidth, GUIStyle style)
        {
            if (string.IsNullOrEmpty(text)) return "";
            _truncateContent.text = text;
            float width = style.CalcSize(_truncateContent).x;
            if (width <= maxWidth) return text;

            for (int i = text.Length - 1; i > 0; i--)
            {
                string truncated = text.Substring(0, i) + "..";
                _truncateContent.text = truncated;
                if (style.CalcSize(_truncateContent).x <= maxWidth)
                    return truncated;
            }
            return text.Length > 0 ? text[0] + ".." : "..";
        }

        // ===================================================================
        // 🗺️ 오토루트 컨텍스트 메뉴
        // ===================================================================

        /// <summary>
        /// 우클릭 시 표시되는 오토루트 컨텍스트 메뉴를 그립니다.
        /// </summary>
        private void DrawRouteContextMenu()
        {
            if (!_showRouteContextMenu) return;

            // ESC 또는 다른 클릭으로 닫기
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                _showRouteContextMenu = false;
                Event.current.Use();
                return;
            }

            // 컨텍스트 메뉴 위치 보정 (화면 밖으로 나가지 않도록)
            float menuX = _routeContextMenuRect.x;
            float menuY = _routeContextMenuRect.y;
            float menuWidth = _routeContextMenuRect.width;
            float menuHeight = _routeContextMenuRect.height;

            if (menuX + menuWidth > Screen.width)
                menuX = Screen.width - menuWidth - 10;
            if (menuY + menuHeight > Screen.height)
                menuY = Screen.height - menuHeight - 10;
            if (menuX < 0) menuX = 10;
            if (menuY < 0) menuY = 10;

            Rect menuRect = new Rect(menuX, menuY, menuWidth, menuHeight);

            // 배경
            Color oldBg = GUI.color;
            GUI.color = new Color(0.12f, 0.10f, 0.14f, 0.95f);
            GUI.DrawTexture(menuRect, _texWhite);
            // 테두리
            GUI.color = new Color(0.70f, 0.50f, 0.15f, 1f);
            DrawColoredRect(new Rect(menuX, menuY, menuWidth, 2), new Color(0.70f, 0.50f, 0.15f, 1f));
            DrawColoredRect(new Rect(menuX, menuY + menuHeight - 2, menuWidth, 2), new Color(0.70f, 0.50f, 0.15f, 1f));
            GUI.color = oldBg;

            // 메뉴 항목
            string routeLabel = $"📍 오토루트: {_routeContextTerritoryName}";
            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 48,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.92f, 0.88f, 0.80f, 1f) },
                padding = new RectOffset(8, 4, 2, 2)
            };
            GUI.Label(new Rect(menuX + 4, menuY + 6, menuWidth - 8, 44), routeLabel, labelStyle);

            // [이동] 버튼
            float btnWidth = menuWidth - 16;
            float btnHeight = 44;
            float btnY = menuY + menuHeight - btnHeight - 6;
            var btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 46,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.80f, 0.60f, 0.20f, 1f),
                           background = MakeTexture(1, 1, new Color(0.30f, 0.22f, 0.16f, 1f)) },
                hover = { textColor = Color.white,
                          background = MakeTexture(1, 1, new Color(0.45f, 0.32f, 0.22f, 1f)) },
                border = new RectOffset(2, 2, 2, 2)
            };

            if (GUI.Button(new Rect(menuX + 8, btnY, btnWidth, btnHeight), "🚶 이동", btnStyle))
            {
                ConfirmRouteAction();
                _showRouteContextMenu = false;
                Event.current.Use();
            }
        }

        /// <summary>
        /// 오토루트 이동 확인 — RouteConfirmationUI 팝업 표시 후 AutoMoveManager 실행
        /// </summary>
        private void ConfirmRouteAction()
        {
            if (string.IsNullOrEmpty(_routeContextTerritoryId))
            {
                Debug.LogWarning("[InventoryWindow] 영지 ID가 없어 오토루트를 실행할 수 없습니다.");
                return;
            }

            // 영지 ID 파싱
            string[] parts = _routeContextTerritoryId.Split('_');
            if (parts.Length != 2)
            {
                Debug.LogWarning($"[InventoryWindow] 영지 ID 형식 오류: {_routeContextTerritoryId}");
                return;
            }

            if (!System.Enum.TryParse<NationType>(parts[0], out var nation))
            {
                Debug.LogWarning($"[InventoryWindow] 국가 파싱 실패: {parts[0]}");
                return;
            }

            if (!int.TryParse(parts[1], out int index))
            {
                Debug.LogWarning($"[InventoryWindow] 영지 인덱스 파싱 실패: {parts[1]}");
                return;
            }

            // AutoMoveManager 확인
            if (AutoMoveManager.Instance == null)
            {
                Debug.LogError("[InventoryWindow] AutoMoveManager 인스턴스가 없습니다! Scene에 AutoMoveManager를 추가해주세요.");
                return;
            }

            // 영지 월드 좌표 계산 (MapWindow와 동일한 방식)
            Vector3 worldPos = new Vector3(
                index * 10f,
                0f,
                (int)nation * 10f
            );

            // 자동 이동 시작
            AutoMoveManager.Instance.SetDestination(worldPos);

            // 확인 메시지
            string confirmMsg = $"📍 {_routeContextTerritoryName} (으)로 자동 이동합니다";


            // RouteConfirmationUI 팝업 표시
            if (RouteConfirmationUI.Instance != null)
            {
                RouteConfirmationUI.Instance.Show(
                    _routeContextItemName,
                    _routeContextTerritoryName,
                    _routeContextTerritoryId
                );
            }
        }
    }
}