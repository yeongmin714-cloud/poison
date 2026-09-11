using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core.Data;
#pragma warning disable 0414

namespace ProjectName.UI
{
    /// <summary>
    /// 양피지 월드맵 창 — M 키 토글 (UIWindow 파생, IMGUI). (2026-09-11 신규)
    ///
    /// [개요]
    /// - M 키 토글: UIInventoryHotkey 방식의 자가 등록 핫키(UIWorldMapHotkey)를 Awake에서
    ///   AddComponent+Bind로 부착(중복 방지). ESC/M키/X 버튼으로 닫힘.
    /// - UIWindow.Hide()는 root GameObject가 아니라 _windowRoot만 비활성화한다.
    ///   → Awake에서 비어 있는 자식 GO("WorldMapPanel")를 _windowRoot로 지정해
    ///   닫아도 루트 컴포넌트(핫키/OnGUI)가 살아있게 한다(InventoryWindow 선례 준수).
    /// - 배경은 외부 에셋 없이 절차 생성한 밝은 크림·세피아 양피지 텍스처(static 캐시 1회).
    ///   같은 Texture2D를 (a) 지도 본체 (b) 타이틀/힌트 스트립(창 프레임/장식)에 재사용.
    /// - 영지 마커: TerritoryDatabase.GetAllDefinitions()를 방사형 배치.
    ///   좌표 규약: u = 0.5 + worldPos.x/3200f, v = 0.5 + worldPos.z/3200f (extent 1600m).
    ///   Empire(0,0)=중앙, Ring1(1450m)/드라큘라(1350m)=가장자리. 지형 텍스처 미사용
    ///   (MinimapUI의 ±1000m 스플랫 크롭 함정 회피 — 영지는 최대 1450m까지 배치되므로
    ///   순수 양피지 위에 정규화 좌표로 그린다).
    /// - 플레이어 위치: GameObject.FindWithTag("Player") 위치를 같은 정규화로 표시(펄스 강조).
    /// - 마우스 휠 3단 줌 + 좌클릭 드래그 팬. 마커/링 가이드 모두 같은 좌표계에서 함께 이동.
    /// - 호버 툴팁: 영지명/국가/난이도/병사 수/소유 상태/설명.
    /// - 부트 자동생성: [RuntimeInitializeOnLoadMethod]로 씬 로드 후 스스로 등록 +
    ///   public static EnsureCreated() 생성 가드(GameSetup 류에서 호출 가능, 기존 부트 수정 불요).
    ///
    /// [GC 캐시 관례]
    /// - GUIStyle/Texture2D는 최초 1회 생성 후 캐시(OnGUI 상시 할당 0).
    ///   양피지/흰색 텍스처는 static 캐시(파기 금지 — InventoryArtLibrary 규약).
    /// </summary>
    public class WorldMapWindow : UIWindow
    {
        // ===================================================================
        // 싱글턴 / 부트 자동생성
        // ===================================================================
        private static WorldMapWindow _instance;
        /// <summary>현재 살아있는 월드맵 인스턴스 (핫키 폴백 조회용).</summary>
        public static WorldMapWindow Instance => _instance;

        /// <summary>
        /// 생성 가드 — 살아있는(비활성 포함) 인스턴스가 있으면 그대로 반환,
        /// 없으면 DontDestroyOnLoad 루트 GO를 만들어 부착한다.
        /// GameSetup 류 부트에서 AddComponent 대신 호출하면 중복 없이 안전.
        /// </summary>
        public static WorldMapWindow EnsureCreated()
        {
            if (_instance != null) return _instance;
            var existing = FindAnyObjectByType<WorldMapWindow>(FindObjectsInactive.Include);
            if (existing != null)
            {
                _instance = existing;
                return existing;
            }
            var go = new GameObject("WorldMapWindow");
            DontDestroyOnLoad(go);
            Debug.Log("[WorldMapWindow] 부트 자동생성 (EnsureCreated — M키 월드맵)");
            return go.AddComponent<WorldMapWindow>();
        }

        /// <summary>씬 로드 후 자가 등록 — 기존 부트 파일 수정 없이 M키 월드맵 보장.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap() => EnsureCreated();

        // ===================================================================
        // 좌표 규약 / 레이아웃 상수
        // ===================================================================
        /// <summary>지도 반폭(±m). 영지 최대 배치 반경 Ring1=1450m + 여유.</summary>
        private const float WORLD_EXTENT = 1600f;
        /// <summary>정규화 분모 = 2×extent. u = 0.5 + x/WORLD_SPAN.</summary>
        private const float WORLD_SPAN = 3200f;

        private const float TITLE_H = 46f;   // 타이틀 스트립 높이
        private const float HINT_H = 24f;    // 하단 힌트 바 높이

        /// <summary>휠 줌 3단 (콘텐츠 배율 — 1.0=전체 대륙, 2.5=확대).</summary>
        private static readonly float[] _zoomScales = { 1f, 1.6f, 2.5f };

        /// <summary>링 가이드 반경(m) — TerritoryDatabase.GetRingDistance와 동일 값.</summary>
        private static readonly float[] _ringDistances = { 1450f, 1000f, 550f, 150f };
        private static readonly string[] _ringLabels =
        {
            "링1 · 1450m", "링2 · 1000m", "링3 · 550m", "링4 · 150m"
        };

        private const string HINT_TEXT = "🖱 휠: 줌 · 좌클릭 드래그: 지도 이동 · ESC / M: 닫기";
        private const string LEGEND_TEXT = "● 영지   ★ 내 영지   붉은 테두리: 전쟁 중   👑 황제국   🧛 드라큘라";

        // ===================================================================
        // 상태
        // ===================================================================
        private int _zoomIndex = 0;                       // 0=1.0× 1=1.6× 2=2.5×
        private Vector2 _panOffset = Vector2.zero;        // 드래그 팬 오프셋(GUI px, 마커도 함께 이동)
        private bool _dragging;

        private Transform _playerTransform;               // FindWithTag("Player") 캐시
        private float _nextPlayerProbe;                   // 재탐색 스로틀 (프레임당 FindWithTag 방지)

        // 영지 정의 캐시 (static 데이터 — OnShow에서 1회 재구성. 상태(소유)는 매 프레임 live 조회)
        private readonly List<TerritoryDefinition> _defs = new List<TerritoryDefinition>(96);
        private int _hoverIndex = -1;                     // 호버 중인 defs 인덱스 (툴팁용)

        // 마커 화면 Rect 캐시 (정적 — GC 캐시 관례, 호버 판정용)
        private static readonly List<Rect> s_markerRects = new List<Rect>(96);
        private static readonly List<int> s_markerIndices = new List<int>(96);
        private static readonly GUIContent s_tipContent = new GUIContent();

        // ===================================================================
        // 색상 팔레트 (밝은 크림·세피아 양피지 — 진한 판타지 X)
        // ===================================================================
        private static readonly Color ColorInk = new Color(0.243f, 0.173f, 0.098f, 1f);        // 짙은 세피아 잉크(텍스트)
        private static readonly Color ColorInkSoft = new Color(0.36f, 0.28f, 0.18f, 0.9f);     // 보조 텍스트
        private static readonly Color ColorSepiaFrame = new Color(0.366f, 0.255f, 0.145f, 1f); // 창 테두리 짙은 갈색
        private static readonly Color ColorGold = new Color(0.95f, 0.76f, 0.28f, 1f);          // 플레이어 소유 강조
        private static readonly Color ColorWar = new Color(0.90f, 0.18f, 0.14f, 1f);           // 전쟁 중(펄스)
        private static readonly Color ColorPlayer = new Color(0.30f, 0.72f, 1f, 1f);           // 플레이어 마커(하늘색)
        private static readonly Color ColorGuide = new Color(0.30f, 0.22f, 0.12f, 0.16f);      // 링/방위 가이드라인

        // ===================================================================
        // GUIStyle 캐시 (OnGUI 최초 프레임 1회 생성 — 프로젝트 규약)
        // ===================================================================
        private GUIStyle _styleTitle;       // 창 제목
        private GUIStyle _styleZoom;        // 우상단 줌 배지
        private GUIStyle _styleMapLabel;    // 영지 이름 라벨 (11px)
        private GUIStyle _styleMapTiny;     // 링4 등 촘촘 구역 라벨 (9px)
        private GUIStyle _styleGlyph;       // 👑/🧛/★ 특별 표식 글리프
        private GUIStyle _styleTipTitle;    // 툴팁 제목
        private GUIStyle _styleTipBody;     // 툴팁 본문
        private GUIStyle _styleTipDesc;     // 툴팁 설명(웹랩)
        private GUIStyle _styleHint;        // 하단 힌트
        private GUIStyle _styleCompass;     // N/E/S/W 방위
        private bool _stylesInitialized;

        // ===================================================================
        // 절차 텍스처 static 캐시 (외부 에셋 금지 — 1회 생성, 파기 금지)
        // ===================================================================
        private static Texture2D s_parchment;   // 밝은 양피지 (지도 본체 + 프레임/장식 공용)
        private static Texture2D s_texWhite;    // 1×1 흰색 (색조 Rect 드로잉용)

        /// <summary>양피지 텍스처 — static 캐시 1회 생성. 지도 본체와 창 프레임/장식이 같은 텍스처를 재사용.</summary>
        private static Texture2D GetParchment()
        {
            if (s_parchment == null) s_parchment = BuildParchment(512);
            return s_parchment;
        }

        /// <summary>
        /// 양피지 절차 텍스처 생성 (512×512, 결정론 노이즈 — UnityEngine.Random 미사용).
        /// ① 크림 베이스(밝은 톤) ② fBm 얼룩 + 미세 그레인 ③ 접힘 자국(세로/가로 코사인 리플)
        /// ④ 가장자리 불규칙 어둡게(노이즈 워프 엣지) + 방사 비네트 ⑤ 테두리 짙은 갈색 라인.
        /// </summary>
        private static Texture2D BuildParchment(int size)
        {
            var tex = MakeCanvas(size, size, "WorldMap_Parchment");
            var buf = new Color[size * size];
            Color cream = new Color(0.918f, 0.866f, 0.722f);   // 밝은 크림 베이스
            Color sepiaShade = new Color(0.62f, 0.50f, 0.34f); // 가장자리 세피아 섀도
            Color sepiaInk = new Color(0.30f, 0.20f, 0.115f);  // 테두리 짙은 갈색
            float inv = 1f / (size - 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = x * inv, ny = y * inv;

                    // ①② 큰 얼룩(fBm) + 미세 그레인(해시)
                    float blotch = Fbm3(nx * 6f, ny * 6f);
                    float grain = Hash01(x * 7 + 1, y * 13 + 5);

                    // ③ 접힘 자국 — 밝은 세로/가로 코사인 리플(위상은 노이즈로 흔듦)
                    float foldV = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(nx * Mathf.PI * 6f + blotch * 4f), 6f) * 0.09f;
                    float foldH = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(ny * Mathf.PI * 4f + blotch * 3f), 6f) * 0.06f;

                    // ④ 가장자리 어둡게 + 불규칙 — 노이즈로 엣지를 워프시켜 고르지 않게
                    float edgePx = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                    float warp = (Fbm3(nx * 7f + 11f, ny * 7f + 29f) - 0.5f) * 30f;
                    float edgeT = Mathf.Clamp01((edgePx + warp) / 52f);
                    float edgeDark = Mathf.Pow(1f - edgeT, 1.6f) * (0.45f + 0.40f * Fbm3(nx * 11f + 3f, ny * 11f + 7f));
                    float dx = nx - 0.5f, dy = ny - 0.5f;
                    float vig = Mathf.SmoothStep(0.55f, 1.0f, Mathf.Sqrt(dx * dx + dy * dy) * 1.4142f) * 0.20f;

                    float shade = 1f + (grain - 0.5f) * 0.14f + (blotch - 0.5f) * 0.10f + foldV + foldH;
                    Color c = cream * shade;
                    c = Color.Lerp(c, sepiaShade, Mathf.Clamp01(edgeDark * 0.8f + vig));

                    // ⑤ 테두리 라인 — 가장자리 ~5px 짙은 갈색(그레인으로 미세 요철)
                    float border = 1f - Mathf.Clamp01((edgePx - 2f) / 3f);
                    c = Color.Lerp(c, sepiaInk * (0.85f + grain * 0.3f), border * 0.9f);

                    c.a = 1f;
                    buf[y * size + x] = c;
                }
            }
            tex.SetPixels(buf);
            Apply(tex);
            return tex;
        }

        // ===================================================================
        // Unity 라이프사이클
        // ===================================================================
        protected override void Awake()
        {
            // 중복 생성 가드 (EnsureCreated / 부트 AddComponent 경합 방지)
            if (_instance != null && _instance != this)
            {
                Debug.Log("[WorldMapWindow] 중복 인스턴스 감지 — 신규 인스턴스 파기");
                Destroy(gameObject);
                return;
            }

            // ★ 핫키 생존 핵심: _windowRoot를 루트가 아닌 자식 패널로 지정.
            //   UIWindow.Hide()의 CloseAnimation은 _windowRoot만 SetActive(false)하므로
            //   루트 컴포넌트(핫키/OnGUI)는 활성 상태 유지된다(InventoryWindow 선례).
            EnsureVisibilityRoot();

            base.Awake();
            _instance = this;

            // UIInventoryHotkey 방식의 자가 등록 — 씬에 핫키가 없으면 스스로 부착(M키 토글 보장)
            if (FindAnyObjectByType<UIWorldMapHotkey>(FindObjectsInactive.Include) == null)
            {
                var hotkey = gameObject.AddComponent<UIWorldMapHotkey>();
                hotkey.Bind(this);
                Debug.Log("[WorldMapWindow] UIWorldMapHotkey 자가 등록 (M키 토글)");
            }

            // 시작은 항상 닫힌 상태 (자식 패널만 비활성화 — 루트는 살아있음)
            if (_windowRoot != null)
                _windowRoot.SetActive(false);
        }

        /// <summary>빈 자식 패널을 만들어 _windowRoot로 지정 (Hide 시 이것만 비활성화됨).</summary>
        private void EnsureVisibilityRoot()
        {
            if (_windowRoot != null) return;
            var panel = new GameObject("WorldMapPanel");
            panel.transform.SetParent(transform, false);
            _windowRoot = panel;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_instance == this) _instance = null;
            // static 텍스처(s_parchment/s_texWhite)는 파기 금지 관례 — 정리하지 않는다.
        }

        protected override void OnShow()
        {
            base.OnShow(); // 테마 미적용(null) → 기본 배경 드로잉 없음, 양피지가 배경 전담

            RefreshDefinitions();
            TryFindPlayer();
            _zoomIndex = 0;
            _panOffset = Vector2.zero;
            _dragging = false;
            _hoverIndex = -1;
        }

        protected override void OnHide()
        {
            base.OnHide();
            _hoverIndex = -1;
            _dragging = false;
        }

        private void Update()
        {
            // ESC로 닫기 (M 토글은 UIWorldMapHotkey가 담당)
            if (IsOpen)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && kb.escapeKey.wasPressedThisFrame)
                    Hide();
            }

            // 플레이어 재탐색 (0.7초 스로틀 — 프레임당 FindWithTag 방지)
            if (_playerTransform == null && Time.unscaledTime >= _nextPlayerProbe)
                TryFindPlayer();
        }

        /// <summary>플레이어 캐싱 (OnShow + 스로틀 재탐색).</summary>
        private void TryFindPlayer()
        {
            _nextPlayerProbe = Time.unscaledTime + 0.7f;
            var go = GameObject.FindWithTag("Player");
            _playerTransform = go != null ? go.transform : null;
        }

        /// <summary>영지 정의 캐시 재구성 (OnShow 1회 — 상태(소유)는 매 프레임 live 조회).</summary>
        private void RefreshDefinitions()
        {
            _defs.Clear();
            var db = TerritoryDatabase.Instance;
            if (db == null) return;
            foreach (var def in db.GetAllDefinitions())
                _defs.Add(def);
            // 바깥 링(반경 큰 순)부터 그려 안쪽 마커가 위에 오도록 정렬 (구조체 정렬 — 오픈 시 1회)
            _defs.Sort((a, b) => b.worldPosition.sqrMagnitude.CompareTo(a.worldPosition.sqrMagnitude));
        }

        // ===================================================================
        // OnGUI — IMGUI 렌더링
        // ===================================================================
        protected override void OnGUI()
        {
            if (!IsOpen) return;
            base.OnGUI(); // 테마 없음 → 실드(빈 DrawWindowContent)만, 배경은 아래 양피지가 전담

            InitStyles();

            // 창 rect — 중앙 배치, 화면 클램프(H>1080 클램프 함정 대응)
            float winW = Mathf.Min(Screen.width - 24f, 1660f);
            float winH = Mathf.Min(Screen.height - 24f, 1080f);
            var winRect = new Rect((Screen.width - winW) * 0.5f, (Screen.height - winH) * 0.5f, winW, winH);

            UIStyleManager.DrawDimOverlay();

            // 닫기(X) 버튼 — 프로젝트 공용 헬퍼
            if (UIStyleManager.DrawCloseButton(winRect))
            {
                Hide();
                return;
            }

            var mapRect = new Rect(
                winRect.x + 14f,
                winRect.y + TITLE_H + 12f,
                winRect.width - 28f,
                winRect.height - TITLE_H - 12f - (HINT_H + 8f) - 12f);

            // ── 레이어 ①: 양피지 배경 (창 프레임 존 = 어두운 틴트, 지도 본체 = 밝게) ──
            DrawParchmentBackground(winRect, mapRect);

            // ── 입력: 휠 줌 + 드래그 팬 (마커 이동 좌표계와 공유) ──
            HandleMapInput(mapRect);

            // ── 레이어 ②: 지도 컨텐츠 (그룹 클리핑 — 링 가이드/마커/플레이어) ──
            DrawMapContent(mapRect);

            // ── 레이어 ③: 타이틀 스트립 + 하단 힌트 바 (양피지 텍스처 재사용) ──
            DrawTitleBar(winRect);
            DrawHintBar(winRect, mapRect);

            // ── 레이어 ④: 호버 툴팁 (그룹 밖 — 클리핑 없음) ──
            UpdateHover(mapRect);
            DrawTooltip(winRect);

            // ── 레이어 ⑤: 창 프레임 (짙은 갈색 이중 테두리) ──
            DrawWindowFrame(winRect);
        }

        /// <summary>GUIStyle 지연 초기화 (OnGUI 최초 1회 — GUI.skin 접근 규약).</summary>
        private void InitStyles()
        {
            if (_stylesInitialized) return;

            if (s_texWhite == null)
            {
                s_texWhite = MakeCanvas(1, 1, "WorldMap_White");
                s_texWhite.SetPixel(0, 0, Color.white);
                Apply(s_texWhite);
            }

            Font font = UIFont.Load();

            _styleTitle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = ColorInk }
            };
            _styleZoom = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = ColorInkSoft }
            };
            _styleMapLabel = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                wordWrap = false,
                normal = { textColor = ColorInk }
            };
            _styleMapTiny = new GUIStyle(_styleMapLabel) { fontSize = 9 };
            _styleGlyph = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ColorInk }
            };
            _styleTipTitle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = ColorInk }
            };
            _styleTipBody = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = ColorInkSoft }
            };
            _styleTipDesc = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 11,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                normal = { textColor = ColorInkSoft }
            };
            _styleHint = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 10,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = ColorInkSoft }
            };
            _styleCompass = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ColorInk }
            };

            _stylesInitialized = true;
        }

        // ===================================================================
        // 입력 — 휠 줌(3단) / 좌클릭 드래그 팬
        // ===================================================================
        private void HandleMapInput(Rect mapRect)
        {
            Event evt = Event.current;
            if (evt == null) return;

            // 드래그는 지도 밖으로 마우스가 나가도 계속 (끊김 방지)
            if (evt.type == EventType.MouseUp && evt.button == 0)
            {
                _dragging = false;
                return;
            }
            if (evt.type == EventType.MouseDrag && _dragging)
            {
                _panOffset += evt.delta;   // 좌클릭 드래그 — 마커 포함 지도 전체가 함께 이동
                ClampPan(mapRect);
                evt.Use();
                return;
            }

            if (!mapRect.Contains(evt.mousePosition)) return;

            if (evt.type == EventType.ScrollWheel)
            {
                // 휠 올림(delta.y<0)=확대, 내림=축소 — MinimapUI 3단 순환 규약과 동일 방향
                int dir = evt.delta.y > 0f ? -1 : 1;
                int next = Mathf.Clamp(_zoomIndex + dir, 0, _zoomScales.Length - 1);
                if (next != _zoomIndex)
                {
                    _zoomIndex = next;
                    ClampPan(mapRect);
                }
                evt.Use();
            }
            else if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                _dragging = true;
                evt.Use();
            }
        }

        /// <summary>팬 클램프 — 지도 콘텐츠가 항상 맵 영역을 덮도록 (줌 1.0이면 팬 0).</summary>
        private void ClampPan(Rect mapRect)
        {
            float zoom = _zoomScales[_zoomIndex];
            float maxX = Mathf.Max(0f, (mapRect.width * zoom - mapRect.width) * 0.5f);
            float maxY = Mathf.Max(0f, (mapRect.height * zoom - mapRect.height) * 0.5f);
            _panOffset.x = Mathf.Clamp(_panOffset.x, -maxX, maxX);
            _panOffset.y = Mathf.Clamp(_panOffset.y, -maxY, maxY);
        }

        // ===================================================================
        // 좌표 변환 — worldPosition → 맵 픽셀
        // ===================================================================
        /// <summary>
        /// 월드 좌표 → 지도(그룹 로컬) 픽셀. 정규화: u = 0.5 + x/3200, v = 0.5 + z/3200.
        /// +z(북)가 화면 위 — GUI y는 아래로 증가하므로 v를 반전. 팬 오프셋이 마커에도 동일 적용됨.
        /// </summary>
        private Vector2 WorldToMapLocal(Vector3 worldPos, Rect mapRect)
        {
            float u = 0.5f + worldPos.x / WORLD_SPAN;
            float v = 0.5f + worldPos.z / WORLD_SPAN;
            float zoom = _zoomScales[_zoomIndex];
            float lx = mapRect.width * 0.5f + _panOffset.x + (u - 0.5f) * mapRect.width * zoom;
            float ly = mapRect.height * 0.5f + _panOffset.y - (v - 0.5f) * mapRect.height * zoom;
            return new Vector2(lx, ly);
        }

        // ===================================================================
        // 드로잉 — 배경 / 프레임 / 타이틀 / 힌트
        // ===================================================================
        /// <summary>
        /// 양피지 배경 — 같은 Texture2D를 (a) 지도 본체(밝게) (b) 창 프레임 존(세피아 틴트)에 재사용.
        /// </summary>
        private void DrawParchmentBackground(Rect winRect, Rect mapRect)
        {
            Texture2D tex = GetParchment();
            if (tex == null)
            {
                // 안전망 폴백 (생성 실패 시 단색 크림)
                DrawColoredRect(winRect, new Color(0.86f, 0.80f, 0.64f, 1f));
                return;
            }

            Color old = GUI.color;
            // (b) 창 프레임 존 — 어둡게 틴트한 양피지 (프레임/장식 재사용)
            GUI.color = new Color(0.78f, 0.70f, 0.56f, 1f);
            GUI.DrawTexture(winRect, tex, ScaleMode.StretchToFill, true);
            // (a) 지도 본체 — 원본 밝기 그대로
            GUI.color = Color.white;
            GUI.DrawTexture(mapRect, tex, ScaleMode.StretchToFill, true);
            // 지도 내곽 세피아 라인 (본체와 프레임 경계)
            DrawRectBorder(mapRect, new Color(0.45f, 0.33f, 0.20f, 0.85f), 2f);
            GUI.color = old;
        }

        /// <summary>창 외곽 프레임 — 짙은 갈색 이중 테두리(+안쪽 밝은 라인).</summary>
        private void DrawWindowFrame(Rect winRect)
        {
            DrawRectBorder(new Rect(winRect.x - 3f, winRect.y - 3f, winRect.width + 6f, winRect.height + 6f),
                ColorSepiaFrame, 3f);
            DrawRectBorder(winRect, new Color(0.98f, 0.94f, 0.86f, 0.35f), 1f);
        }

        /// <summary>타이틀 스트립 — 양피지 텍스처 재사용(어두운 틴트) + 제목/줌 배지.</summary>
        private void DrawTitleBar(Rect winRect)
        {
            Texture2D tex = GetParchment();
            var strip = new Rect(winRect.x + 8f, winRect.y + 4f, winRect.width - 16f, TITLE_H - 8f);

            Color old = GUI.color;
            if (tex != null)
            {
                GUI.color = new Color(0.72f, 0.64f, 0.52f, 1f);
                GUI.DrawTexture(strip, tex, ScaleMode.StretchToFill, true);
            }
            else
            {
                DrawColoredRect(strip, new Color(0.62f, 0.54f, 0.40f, 1f));
            }
            GUI.color = old;
            DrawRectBorder(strip, new Color(0.40f, 0.29f, 0.17f, 0.9f), 1f);

            // 제목 (우측 70px는 X 버튼 여백)
            GUI.Label(new Rect(strip.x + 14f, strip.y, strip.width - 100f, strip.height),
                "🗺️ 양피지 월드맵 — 포이즌 대륙", _styleTitle);
            // 줌 배지 (우측 끝은 X 버튼 영역[winRect.xMax-66]에서 70px 이격)
            GUI.Label(new Rect(strip.xMax - 330f, strip.y, 258f, strip.height),
                $"확대 ×{_zoomScales[_zoomIndex]:0.0}", _styleZoom);
        }

        /// <summary>하단 힌트 바 — 조작 안내 + 범례 (양피지 텍스처 재사용).</summary>
        private void DrawHintBar(Rect winRect, Rect mapRect)
        {
            Texture2D tex = GetParchment();
            var strip = new Rect(winRect.x + 8f, mapRect.yMax + 8f, winRect.width - 16f, HINT_H);

            Color old = GUI.color;
            if (tex != null)
            {
                GUI.color = new Color(0.80f, 0.73f, 0.60f, 1f);
                GUI.DrawTexture(strip, tex, ScaleMode.StretchToFill, true);
            }
            else
            {
                DrawColoredRect(strip, new Color(0.70f, 0.63f, 0.48f, 1f));
            }
            GUI.color = old;
            DrawRectBorder(strip, new Color(0.40f, 0.29f, 0.17f, 0.7f), 1f);

            float half = strip.width * 0.5f;
            GUI.Label(new Rect(strip.x + 10f, strip.y, half - 16f, strip.height), HINT_TEXT, _styleHint);
            GUI.Label(new Rect(strip.x + half, strip.y, half - 12f, strip.height), LEGEND_TEXT, _styleHint);
        }

        // ===================================================================
        // 지도 컨텐츠 — 링 가이드 / 영지 마커 / 플레이어
        // ===================================================================
        private void DrawMapContent(Rect mapRect)
        {
            if (mapRect.width < 80f || mapRect.height < 80f) return;

            s_markerRects.Clear();
            s_markerIndices.Clear();

            // 그룹 클리핑 — 맵 밖으로 나가는 마커/라벨은 지도 경계에서 잘림
            GUI.BeginGroup(mapRect);
            DrawRingGuides(mapRect);
            DrawAllTerritoryMarkers(mapRect);
            DrawPlayerMarker(mapRect);
            GUI.EndGroup();
        }

        /// <summary>방사형 링 가이드 — 4개 링 도트 + 링 라벨(NE 방향) + 중심 방위선.</summary>
        private void DrawRingGuides(Rect mapRect)
        {
            Color old = GUI.color;

            // 링 도트 (결정론적 24방위 — heap alloc 없음)
            GUI.color = ColorGuide;
            for (int r = 0; r < _ringDistances.Length; r++)
            {
                float d = _ringDistances[r];
                for (int k = 0; k < 24; k++)
                {
                    float a = k * (Mathf.PI * 2f) / 24f;
                    Vector2 lp = WorldToMapLocal(new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d), mapRect);
                    GUI.DrawTexture(new Rect(lp.x - 1.5f, lp.y - 1.5f, 3f, 3f), s_texWhite);
                }
            }

            // 링 라벨 (NE 45° 방향, 정적 문자열 — alloc 0)
            GUI.color = new Color(0.32f, 0.24f, 0.14f, 0.75f);
            for (int r = 0; r < _ringDistances.Length; r++)
            {
                float d = _ringDistances[r] * 0.7071f;
                Vector2 nlp = WorldToMapLocal(new Vector3(d, 0f, d), mapRect);
                GUI.Label(new Rect(nlp.x + 6f, nlp.y - 9f, 110f, 16f), _ringLabels[r], _styleMapTiny);
            }

            // 중심 → N/E/S/W 방위선 (가느다란 세피아)
            Color line = new Color(0.30f, 0.22f, 0.12f, 0.12f);
            Vector2 c = WorldToMapLocal(Vector3.zero, mapRect);
            DrawMapLine(c, WorldToMapLocal(new Vector3(0f, 0f, WORLD_EXTENT), mapRect), line, 1f);
            DrawMapLine(c, WorldToMapLocal(new Vector3(WORLD_EXTENT, 0f, 0f), mapRect), line, 1f);
            DrawMapLine(c, WorldToMapLocal(new Vector3(0f, 0f, -WORLD_EXTENT), mapRect), line, 1f);
            DrawMapLine(c, WorldToMapLocal(new Vector3(-WORLD_EXTENT, 0f, 0f), mapRect), line, 1f);

            // 방위 글자 (맵에 고정 — 팬과 무관)
            GUI.color = new Color(0.35f, 0.26f, 0.15f, 0.9f);
            GUI.Label(new Rect(mapRect.width * 0.5f - 8f, 3f, 16f, 16f), "N", _styleCompass);
            GUI.Label(new Rect(mapRect.width - 20f, mapRect.height * 0.5f - 8f, 16f, 16f), "E", _styleCompass);
            GUI.Label(new Rect(mapRect.width * 0.5f - 8f, mapRect.height - 19f, 16f, 16f), "S", _styleCompass);
            GUI.Label(new Rect(4f, mapRect.height * 0.5f - 8f, 16f, 16f), "W", _styleCompass);

            GUI.color = old;
        }

        /// <summary>
        /// 영지 마커 일괄 드로잉 — TerritoryDatabase.GetAllDefinitions() 캐시 순회.
        /// 국가색 점 + 이름 라벨 + 난이도별 크기 + 소유 상태 표시 + Empire/드라큘라 특별 표식.
        /// </summary>
        private void DrawAllTerritoryMarkers(Rect mapRect)
        {
            var db = TerritoryDatabase.Instance;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 6f);   // 전쟁 펄스(0~1)
            float zoom = _zoomScales[_zoomIndex];

            for (int i = 0; i < _defs.Count; i++)
            {
                TerritoryDefinition def = _defs[i];
                Vector2 lp = WorldToMapLocal(def.worldPosition, mapRect);
                TerritoryState state = db != null ? db.GetState(def.id) : null;
                TerritoryOwnership own = state != null ? state.ownership : TerritoryOwnership.Unoccupied;

                float size = DifficultyMarkerSize(def.difficulty);
                Color nat = NationColor(def.nation);
                var r = new Rect(lp.x - size * 0.5f, lp.y - size * 0.5f, size, size);

                Color old = GUI.color;

                // ── 본체: 소유 상태별 ──
                if (own == TerritoryOwnership.Unoccupied)
                {
                    DrawRectBorder(r, nat, 2f);                                  // 속 빈 마커
                }
                else
                {
                    DrawColoredRect(r, nat);                                     // 소유(영주/플레이어) 채움
                    DrawColoredRect(new Rect(lp.x - size * 0.18f, lp.y - size * 0.18f,
                        size * 0.36f, size * 0.36f),
                        new Color(0.97f, 0.92f, 0.78f, 0.9f));                    // 중앙 코어
                }
                DrawRectBorder(r, new Color(0.22f, 0.15f, 0.08f, 0.75f), 1f);     // 공통 외곽 윤곽

                // ── 소유 상태 오버레이 ──
                if (own == TerritoryOwnership.PlayerOwned)
                {
                    DrawRectBorder(ExpandRect(r, 4f), ColorGold, 2f);            // 금테 강조
                    GUI.Label(new Rect(lp.x + size * 0.4f, lp.y - size * 1.1f, 18f, 18f), "★", _styleGlyph);
                }
                else if (own == TerritoryOwnership.Contested)
                {
                    Color war = ColorWar;
                    war.a = 0.30f + 0.60f * pulse;                               // 빨강 펄스
                    DrawRectBorder(ExpandRect(r, 3f), war, 2f);
                }

                // ── 특별 표식: 황제국(중앙) / 드라큘라(북동 성채) ──
                if (def.nation == NationType.Empire)
                {
                    Color goldPulse = ColorGold;
                    goldPulse.a = 0.45f + 0.35f * pulse;
                    DrawRectBorder(ExpandRect(r, 8f), goldPulse, 2f);
                    DrawRectBorder(ExpandRect(r, 12f), new Color(0.55f, 0.42f, 0.16f, 0.7f), 1f);
                    GUI.Label(new Rect(lp.x - 13f, lp.y - size * 0.5f - 26f, 26f, 22f), "👑", _styleGlyph);
                }
                else if (def.nation == NationType.Dracula)
                {
                    Color vamp = new Color(0.35f, 0.05f, 0.08f, 0.55f + 0.35f * pulse);
                    DrawRectBorder(ExpandRect(r, 5f), vamp, 2f);
                    GUI.Label(new Rect(lp.x - 13f, lp.y - size * 0.5f - 25f, 26f, 22f), "🧛", _styleGlyph);
                }

                // ── 영지 이름 라벨 (방사 바깥쪽으로 부채꼴 배치 — 링 간섭 최소화) ──
                Vector2 dir = lp - new Vector2(mapRect.width * 0.5f + _panOffset.x,
                                               mapRect.height * 0.5f + _panOffset.y);
                if (dir.sqrMagnitude < 1f) dir = new Vector2(0f, -1f);           // 중앙(Empire)은 아래쪽
                Vector2 lpos = lp + dir.normalized * (size * 0.5f + 8f);
                float lx = Mathf.Clamp(lpos.x, 72f, mapRect.width - 72f);
                float ly = Mathf.Clamp(lpos.y, 10f, mapRect.height - 14f);
                GUIStyle labelStyle = (def.difficulty == TerritoryDifficulty.Ring4 && zoom <= 1.01f)
                    ? _styleMapTiny : _styleMapLabel;
                GUI.Label(new Rect(lx - 72f, ly - 8f, 144f, 16f), def.territoryName, labelStyle);

                // ── 호버 판정용 화면 Rect 등록 (그룹 로컬 → 스크린 좌표) ──
                s_markerRects.Add(new Rect(
                    mapRect.x + lp.x - size * 0.5f - 4f,
                    mapRect.y + lp.y - size * 0.5f - 4f,
                    size + 8f, size + 8f));
                s_markerIndices.Add(i);

                GUI.color = old;
            }
        }

        /// <summary>플레이어 위치 마커 — 펄스 헤일 + 방향 화살표(+z=북=위 보정).</summary>
        private void DrawPlayerMarker(Rect mapRect)
        {
            if (_playerTransform == null) return;

            Vector2 lp = WorldToMapLocal(_playerTransform.position, mapRect);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 4f);

            Color old = GUI.color;

            // 펄스 헤일로 (확장 링)
            Color halo = ColorPlayer;
            halo.a = 0.35f + 0.45f * pulse;
            DrawRectBorder(ExpandRect(new Rect(lp.x - 6f, lp.y - 6f, 12f, 12f), 3f + pulse * 3f), halo, 2f);
            // 코어
            DrawColoredRect(new Rect(lp.x - 5f, lp.y - 5f, 10f, 10f), new Color(1f, 1f, 1f, 0.95f));
            DrawColoredRect(new Rect(lp.x - 3.5f, lp.y - 3.5f, 7f, 7f), ColorPlayer);

            // 진행 방향 화살표 — MinimapUI와 동일 계산(0°=북=위, GUI y 반전)
            Vector3 fwd = _playerTransform.forward;
            float guiAngle = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            Vector2 end = lp + new Vector2(Mathf.Sin(guiAngle * Mathf.Deg2Rad),
                                           -Mathf.Cos(guiAngle * Mathf.Deg2Rad)) * 17f;
            DrawMapLine(lp, end, ColorPlayer, 3f);

            GUI.color = old;
        }

        // ===================================================================
        // 호버 / 툴팁
        // ===================================================================
        private void UpdateHover(Rect mapRect)
        {
            _hoverIndex = -1;
            if (_dragging) return;
            Event evt = Event.current;
            if (evt == null || !mapRect.Contains(evt.mousePosition)) return;
            for (int i = s_markerRects.Count - 1; i >= 0; i--)
            {
                if (s_markerRects[i].Contains(evt.mousePosition))
                {
                    _hoverIndex = s_markerIndices[i];
                    break;
                }
            }
        }

        /// <summary>마커 호버 툴팁 — 영지명/국가/난이도/병사 수/소유 상태/설명(작은 양피지 박스).</summary>
        private void DrawTooltip(Rect winRect)
        {
            if (_hoverIndex < 0 || _hoverIndex >= _defs.Count) return;
            TerritoryDefinition def = _defs[_hoverIndex];
            var db = TerritoryDatabase.Instance;
            TerritoryState state = db != null ? db.GetState(def.id) : null;
            TerritoryOwnership own = state != null ? state.ownership : TerritoryOwnership.Unoccupied;

            const float W = 320f;
            s_tipContent.text = def.description ?? string.Empty;
            float descH = Mathf.Min(_styleTipDesc.CalcHeight(s_tipContent, W - 28f), 110f);
            float h = 20f + 20f + 20f + descH + 18f;

            Vector2 m = Event.current.mousePosition;
            var box = new Rect(m.x + 18f, m.y + 20f, W, h);
            if (box.xMax > winRect.xMax - 8f) box.x = m.x - W - 18f;           // 우측 클램프 → 좌측 플립
            if (box.yMax > winRect.yMax - 8f) box.y = winRect.yMax - h - 8f;   // 하단 클램프
            box.x = Mathf.Max(box.x, winRect.x + 8f);

            Color old = GUI.color;

            // 양피지 텍스처 재사용 툴팁 박스 (진한 세피아 틴트)
            Texture2D tex = GetParchment();
            if (tex != null)
            {
                GUI.color = new Color(0.95f, 0.90f, 0.78f, 1f);
                GUI.DrawTexture(box, tex, ScaleMode.StretchToFill, true);
            }
            else
            {
                DrawColoredRect(box, new Color(0.93f, 0.88f, 0.74f, 1f));
            }
            GUI.color = old;
            DrawRectBorder(box, ColorSepiaFrame, 2f);

            float ix = box.x + 12f, iw = W - 24f;
            GUI.Label(new Rect(ix, box.y + 7f, iw, 20f), TitleLine(def), _styleTipTitle);
            GUI.Label(new Rect(ix, box.y + 27f, iw, 20f),
                $"국가: {NationLabel(def.nation)}   난이도: {DifficultyLabel(def.difficulty)}", _styleTipBody);
            GUI.Label(new Rect(ix, box.y + 47f, iw, 20f),
                $"병사: {def.guardCount}명 · {OwnershipLabel(own)}{(def.isNightOnly ? " · 🌙 야간 전용" : "")}",
                _styleTipBody);
            s_tipContent.text = def.description ?? string.Empty;
            GUI.Label(new Rect(ix, box.y + 67f, iw, descH), s_tipContent, _styleTipDesc);

            GUI.color = old;
        }

        /// <summary>툴팁 제목 문자열 (황제국 특별 접두).</summary>
        private static string TitleLine(TerritoryDefinition def)
        {
            string prefix = def.nation == NationType.Empire ? "👑 " :
                            def.nation == NationType.Dracula ? "🧛 " : "";
            return prefix + def.territoryName;
        }

        // ===================================================================
        // 표기 헬퍼 (정적 문자열 반환 — 호출 빈도 낮음)
        // ===================================================================
        private static string NationLabel(NationType n)
        {
            switch (n)
            {
                case NationType.East: return "동국(East)";
                case NationType.West: return "서국(West)";
                case NationType.South: return "남국(South)";
                case NationType.North: return "북국(North)";
                case NationType.Empire: return "황제국(Empire)";
                case NationType.Dracula: return "드라큘라(Dracula)";
                default: return "미소속";
            }
        }

        private static string DifficultyLabel(TerritoryDifficulty d)
        {
            switch (d)
            {
                case TerritoryDifficulty.Ring1: return "링1 · 쉬움(외곽 1450m)";
                case TerritoryDifficulty.Ring2: return "링2 · 보통(1000m)";
                case TerritoryDifficulty.Ring3: return "링3 · 어려움(550m)";
                case TerritoryDifficulty.Ring4: return "링4 · 매우 어려움(150m)";
                case TerritoryDifficulty.Empire: return "황제국 · 최종";
                default: return "알 수 없음";
            }
        }

        private static string OwnershipLabel(TerritoryOwnership o)
        {
            switch (o)
            {
                case TerritoryOwnership.Unoccupied: return "미점령";
                case TerritoryOwnership.PlayerOwned: return "★ 내 영지";
                case TerritoryOwnership.LordOwned: return "영주 소유";
                case TerritoryOwnership.Contested: return "전쟁 중";
                default: return "미점령";
            }
        }

        /// <summary>난이도(링)별 마커 크기(px) — 링1(쉬움) 작게 → 황제국 최대.</summary>
        private static float DifficultyMarkerSize(TerritoryDifficulty d)
        {
            switch (d)
            {
                case TerritoryDifficulty.Ring1: return 9f;
                case TerritoryDifficulty.Ring2: return 12f;
                case TerritoryDifficulty.Ring3: return 15f;
                case TerritoryDifficulty.Ring4: return 18f;
                case TerritoryDifficulty.Empire: return 26f;
                default: return 10f;
            }
        }

        private static Color NationColor(NationType n)
        {
            switch (n)
            {
                case NationType.East: return new Color(0.26f, 0.52f, 0.90f, 1f);    // 파랑
                case NationType.West: return new Color(0.26f, 0.66f, 0.30f, 1f);    // 초록
                case NationType.South: return new Color(0.88f, 0.30f, 0.22f, 1f);   // 빨강
                case NationType.North: return new Color(0.58f, 0.36f, 0.85f, 1f);   // 보라
                case NationType.Empire: return new Color(0.92f, 0.74f, 0.26f, 1f);  // 금
                case NationType.Dracula: return new Color(0.36f, 0.07f, 0.10f, 1f); // 검강
                default: return new Color(0.45f, 0.40f, 0.35f, 1f);
            }
        }

        // ===================================================================
        // 드로잉 프리미티브 (InventoryWindow/MinimapUI 패턴 — GUI.color 복원 엄수)
        // ===================================================================
        private static void DrawColoredRect(Rect rect, Color color)
        {
            if (s_texWhite == null) return;
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, s_texWhite);
            GUI.color = old;
        }

        /// <summary>사각 테두리만 그리기 (4면 스트립).</summary>
        private static void DrawRectBorder(Rect rect, Color color, float thickness)
        {
            DrawColoredRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawColoredRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawColoredRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawColoredRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        /// <summary>회전된 얇은 박스로 선 그리기 (MinimapUI.DrawLine 패턴).</summary>
        private static void DrawMapLine(Vector2 from, Vector2 to, Color color, float width)
        {
            if (s_texWhite == null) return;
            Vector2 dir = to - from;
            float len = dir.magnitude;
            if (len < 1f) return;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            Color old = GUI.color;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(angle, from);
            GUI.DrawTexture(new Rect(from.x, from.y - width * 0.5f, len, width), s_texWhite);
            GUI.matrix = Matrix4x4.identity;
            GUI.color = old;
        }

        private static Rect ExpandRect(Rect r, float px)
            => new Rect(r.x - px, r.y - px, r.width + px * 2f, r.height + px * 2f);

        // ===================================================================
        // 양피지 텍스처 저수준 헬퍼 (InventoryArtLibrary 규약 복제 — 결정론 노이즈)
        // ===================================================================
        private static Texture2D MakeCanvas(int w, int h, string name)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            return tex;
        }

        private static void Apply(Texture2D tex) => tex.Apply(false, false);

        /// <summary>결정론 정수 해시 — 고정시드 LCG 스타일(0..1).</summary>
        private static float Hash01(int x, int y)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0x7FFFFFFF) / (float)0x7FFFFFFF;
            }
        }

        /// <summary>결정론 value noise — 격자 해시의 smoothstep 보간.</summary>
        private static float ValueNoise(float x, float y)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            float sx = fx * fx * (3f - 2f * fx);
            float sy = fy * fy * (3f - 2f * fy);
            float n00 = Hash01(ix, iy);
            float n10 = Hash01(ix + 1, iy);
            float n01 = Hash01(ix, iy + 1);
            float n11 = Hash01(ix + 1, iy + 1);
            return Mathf.Lerp(Mathf.Lerp(n00, n10, sx), Mathf.Lerp(n01, n11, sx), sy);
        }

        /// <summary>결정론 fBm — 3옥타브 합성(0..1).</summary>
        private static float Fbm3(float x, float y)
        {
            float sum = 0f, amp = 0.5f, freq = 1f, norm = 0f;
            for (int o = 0; o < 3; o++)
            {
                sum += ValueNoise(x * freq, y * freq) * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2f;
            }
            return sum / norm;
        }
    }

    // =======================================================================
    // M 키 토글 핫키 — UIInventoryHotkey 선례 복제(선착순 단일 처리 + Bind 폴백)
    // =======================================================================
    public class UIWorldMapHotkey : MonoBehaviour
    {
        private WorldMapWindow _map;

        // 살아있는 핫키 레지스트리 (선착순 단일 처리 — GC 캐시 관례: 정적 리스트)
        private static readonly List<UIWorldMapHotkey> s_live = new List<UIWorldMapHotkey>(4);

        public void Bind(WorldMapWindow map) => _map = map;

        private void OnEnable() => s_live.Add(this);
        private void OnDisable() => s_live.Remove(this);
        private void OnDestroy() => s_live.Remove(this);

        private void Update()
        {
            // 중복 토글 방지: 여러 핫키 인스턴스가 있어도 첫 번째(선착순)만 처리
            if (s_live.Count == 0 || s_live[0] != this) return;

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;

            // Bind 누락 폴백 — 싱글턴 인스턴스로 토글
            var map = _map != null ? _map : WorldMapWindow.Instance;
            if (map == null) return;

            // M 키 상승 에지 1회 → 토글
            if (kb.mKey.wasPressedThisFrame)
            {
                map.Toggle();
                Debug.Log($"[UIWorldMapHotkey] 월드맵 토글 → {(map.IsOpen ? "열림" : "닫힘")}");
            }
        }
    }
}