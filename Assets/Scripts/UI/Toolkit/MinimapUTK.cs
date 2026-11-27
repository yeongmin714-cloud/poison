using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;        // PlayerInventory 불필요
using ProjectName.Core.Data;   // [15차 A4] BiomeType — 지형 높이 역산
using ProjectName.Systems;     // TemperatureSystem, SoundSystem, TimeWeatherSystem, QuestMarkerSystem, TerrainSplatBaker
using WeatherType = ProjectName.Systems.TimeWeatherSystem.WeatherType;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U7 Round B — 미니맵 (MinimapUI 614줄 IMGUI → UTK 포팅).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/UI/Functions/MinimapUI.cs — 절대 수정 금지.
    ///
    /// [구현 방식]
    ///   원본은 IMGUI로 프레임마다 그렸지만 본 UTK는 VisualElement backgroundImage 방식.
    ///   맵 텍스처 = TerrainSplatBaker.LastWorldSplat(통합 월드 스플랫, BakeWorldSplat이 생성)를
    ///   그대로 재사용 — 추가 베이크 없이 게임 지형과 100% 일치 (원본 TryApplyMapTexture 동일 데이터 경로).
    ///   플레이어/영지/퀘스트/시간/날씨/온도/소음 마커는 원본 데이터(실측)를 폴링으로 갱신.
    ///
    /// [배치] 우상단 (사용자 지정 2026-09-18 — 원본 MinimapUI와 동일 위치).
    ///
    /// 상시 노출 — 순수 VisualElement + Updater로 UIRoot 부착 (원본 Update() 관례: TryApplyMapTexture 지연 재시도).
    /// </summary>
    public class MinimapUTK : VisualElement
    {
        // ===== 싱글턴 / 부트스트랩 =====
        private static MinimapUTK _instance;
        public static MinimapUTK Instance => _instance;

        /// <summary>미니맵 인스턴스 보장 + Updater 부착 (U7 스위치오버 호출점용).</summary>
        public static MinimapUTK Ensure()
        {
            if (_instance == null)
            {
                _instance = new MinimapUTK();
                var go = new GameObject("MinimapUTK");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<Updater>().map = _instance;
                Debug.Log("[MinimapUTK] Ensure()로 인스턴스 생성 — 미니맵 준비");
            }
            return _instance;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            Ensure();
        }

        // ===== 설정 =====
        // [144차 P8-MM #1] Figma empty-circular-minimap 264×264 — 지도 지름 220→264.
        //   연쇄 확인: 중심/반경/오버레이 배치는 전부 Diameter 기반(radius=Diameter*0.5,
        //   topRow.bottom=Diameter+4, 블립 sideLen=Diameter-4 등)이라 그대로 스케일됨.
        //   LocalRadius(120f)는 월드 좌표 반경(픽셀 무관) — 무손상.
        private const float Diameter   = 264f;
        // [144차 P8-MM #2] Figma bezel 여백 Right/Top 20/20 → 24/24.
        //   미사용 dead 상수 FigmaBezel*(264/24/24) 제거 — Diameter=264 통일로 중복.
        private const float MarginLeft = 24f;
        private const float MarginTop  = 24f;
        private const float PlayerMarkerSize = 12f;
        private const float LocalRadius = 120f;
        private float _bezelScaleX = 1f;
        private float _bezelScaleY = 1f;

        private VisualElement _mapCanvas;      // 배경 이미지 (월드 스플랫)
        private VisualElement _mapFrame;       // 원형 마스킹용 오버레이 프레임
        private VisualElement _markerHost;     // 영지/퀘스트 마커 (절대배치)
        private VisualElement _playerMarker;   // 플레이어 방향 마커
        private VisualElement _tempFill;
        private VisualElement _soundFill;
        private VisualElement _bezel;          // [Figma] MinimapBezel.png 베젤 오버레이
        private Label _tempText;               // 온도 수치
        private Label _soundText;              // 소음 수치
        private Label _weatherText;            // 날씨 아이콘

        // 상태 (원본 시스템 데이터에서 실측)
        private float _temperature = -0.2f;
        private float _soundLevel = 0f;
        private WeatherType _weather = WeatherType.Clear;

        private Transform _playerTransform;

        // [15차 A4] 유닛 블립 풀 + 우클릭 명령
        private readonly System.Collections.Generic.List<VisualElement> _unitBlipPool = new System.Collections.Generic.List<VisualElement>();
        private const int MaxUnitBlips = 40;
        private const float UnitBlipSize = 8f;
        private static readonly Color AllyBlipColor = new Color32(88, 166, 255, 255);   // Fluent 액센트
        private static readonly Color EnemyBlipColor = new Color32(248, 81, 73, 255);   // 위험
        private Texture2D _mapTexture;
        private Texture2D _indoorBgApplied;   // [146차 P2] 실내 배경으로 교체 중인 캡처 텍스처(월드 원복 판별)

        private MinimapUTK()
        {
            name = "Minimap";
            AddToClassList("utk-minimap");
            style.position = Position.Absolute;
            style.right = MarginLeft;
            style.top = MarginTop;
            style.width = Diameter;
            style.height = Diameter;

            // 미니맵 캔버스 — 월드 스플랫 backgroundImage + 원형 프레임
            _mapFrame = new VisualElement();
            // [15차 A4] 미니맵 우클릭 = 선택 병사 이동 명령(월드 좌표 역산)
            _mapFrame.RegisterCallback<PointerDownEvent>(OnMapPointerDown);
            _mapFrame.name = "MapFrame";
            _mapFrame.style.position = Position.Absolute;
            _mapFrame.style.left = 0f;
            _mapFrame.style.top = 0f;
            _mapFrame.style.right = 0f;
            _mapFrame.style.bottom = 0f;
            // [144차 P8-MM #4] Figma outer-ring 진실치: stroke #E5E7EB 1.2px (구 white@0.3 2px).
            //   3링 체계(#3)는 MinimapBezel.png 텍스처 위임 — 코드 stroke는 mapFrame 1겹만 담당.
            _mapFrame.style.borderTopWidth = 1.2f;
            _mapFrame.style.borderRightWidth = 1.2f;
            _mapFrame.style.borderBottomWidth = 1.2f;
            _mapFrame.style.borderLeftWidth = 1.2f;
            var mapFrameStroke = new StyleColor(new Color32(229, 231, 235, 255)); // #E5E7EB
            _mapFrame.style.borderTopColor = mapFrameStroke;
            _mapFrame.style.borderRightColor = mapFrameStroke;
            _mapFrame.style.borderBottomColor = mapFrameStroke;
            _mapFrame.style.borderLeftColor = mapFrameStroke;
            _mapFrame.style.overflow = Overflow.Hidden;
            _mapFrame.style.backgroundColor = new StyleColor(new Color(0.05f, 0.05f, 0.05f, 0.85f));
            // [P27] 원형 미니맵 — 프레임 모서리를 완전 라운드로 마스킹(스타크래프트식 원형 지도)
            float radius = Diameter * 0.5f;
            _mapFrame.style.borderTopLeftRadius = radius;
            _mapFrame.style.borderTopRightRadius = radius;
            _mapFrame.style.borderBottomLeftRadius = radius;
            _mapFrame.style.borderBottomRightRadius = radius;
            Add(_mapFrame);

            // [Figma GitHub-dark] MinimapBezel.png 베젤 링 오버레이 — 맵 프레임 테두리를
            // 베젤 텍스처 링이 살짝 덮는 방식 (원형 지도 위 스트랩). pickingMode=Ignore.
            // [144차 P8-MM #3 기록] Figma 링 체계 3겹(bezel-shadow 264 / outer-ring 254.4 /
            //   inner-ring 240, stroke #E5E7EB·#D1D5DB) → MinimapBezel.png 텍스처 1매 오버레이로
            //   구조 근사. 코드 stroke는 mapFrame 1겹만(#4 — outer-ring 진실치).
            // [144차 P8-MM #5 기록] bezel-shadow 그림자(DROP_SHADOW #111827 0.039/0.078)는
            //   MinimapBezel.png 텍스처 내부에 포함 — 코드 무상수(별도 그림자 스타일 없음).
            // [144차 P8-MM #6 기록] 베젤 fill: 게임 다크 유지 vs Figma 라이트 베젤 목업 —
            //   GitHub-dark 톤 유지 판단(맵 캔버스 다크 배경 위 라이트 베젤 부조화 방지).
            _bezel = new VisualElement();
            _bezel.name = "MapBezel";
            _bezel.style.position = Position.Absolute;
            _bezel.style.left = 0f;
            _bezel.style.top = 0f;
            _bezel.style.width = Diameter;
            _bezel.style.height = Diameter;
            _bezel.pickingMode = PickingMode.Ignore;
            var bezelTex = Resources.Load<Texture2D>("UI/MinimapBezel");
            if (bezelTex != null)
            {
                _bezel.style.backgroundImage = UTKTextureSafe.ToBackground(bezelTex);
                _bezel.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            }
            Add(_bezel);

            _mapCanvas = new VisualElement();
            _mapCanvas.name = "MapCanvas";
            _mapCanvas.style.position = Position.Absolute;
            _mapCanvas.style.left = 0f;
            _mapCanvas.style.top = 0f;
            _mapCanvas.style.width = Diameter;
            _mapCanvas.style.height = Diameter;
            _mapCanvas.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
            _mapCanvas.style.opacity = 0.9f;
            _mapFrame.Add(_mapCanvas);

            // 영지/퀘스트 마커 호스트 — 플레이어 중심 로컬뷰 절대배치
            _markerHost = new VisualElement();
            _markerHost.name = "MarkerHost";
            _markerHost.style.position = Position.Absolute;
            _markerHost.style.left = 0f;
            _markerHost.style.top = 0f;
            _markerHost.style.right = 0f;
            _markerHost.style.bottom = 0f;
            _markerHost.pickingMode = PickingMode.Ignore;
            _mapFrame.Add(_markerHost);

            // 플레이어 마커 — 중앙 고정 (로컬뷰: 플레이어는 항상 미니맵 중앙)
            _playerMarker = new VisualElement();
            _playerMarker.name = "PlayerMarker";
            _playerMarker.style.position = Position.Absolute;
            _playerMarker.style.width = PlayerMarkerSize;
            _playerMarker.style.height = PlayerMarkerSize;
            _playerMarker.style.backgroundColor = new StyleColor(new Color(0.3f, 0.7f, 1f, 1f));
            _playerMarker.style.borderTopWidth = 1f;
            _playerMarker.style.borderRightWidth = 1f;
            _playerMarker.style.borderBottomWidth = 1f;
            _playerMarker.style.borderLeftWidth = 1f;
            _playerMarker.style.borderTopColor = new StyleColor(Color.white);
            _playerMarker.style.borderRightColor = new StyleColor(Color.white);
            _playerMarker.style.borderBottomColor = new StyleColor(Color.white);
            _playerMarker.style.borderLeftColor = new StyleColor(Color.white);
            _playerMarker.style.borderTopLeftRadius = PlayerMarkerSize * 0.5f;
            _playerMarker.style.borderTopRightRadius = PlayerMarkerSize * 0.5f;
            _playerMarker.style.borderBottomLeftRadius = PlayerMarkerSize * 0.5f;
            _playerMarker.style.borderBottomRightRadius = PlayerMarkerSize * 0.5f;
            // Legacy DrawPlayerMarker renders a center-to-forward line (13.2px at 12px marker size).
            var heading = new VisualElement { name = "PlayerHeading" };
            heading.style.position = Position.Absolute;
            heading.style.left = 4.5f;
            heading.style.top = -7.2f;
            heading.style.width = 3f;
            heading.style.height = 13.2f;
            heading.style.backgroundColor = new StyleColor(Color.white);
            heading.pickingMode = PickingMode.Ignore;
            _playerMarker.Add(heading);
            _mapFrame.Add(_playerMarker);

            // 시간/날씨 (미니맵 위)
            var topRow = new VisualElement();
            topRow.name = "TimeWeatherRow";
            topRow.style.position = Position.Absolute;
            topRow.style.left = 0f;
            topRow.style.bottom = Diameter + 4f;
            topRow.style.width = Diameter;
            topRow.style.height = 30f;
            topRow.style.flexDirection = FlexDirection.Row;
            topRow.style.alignItems = Align.Center;
            topRow.style.justifyContent = Justify.Center;
            topRow.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.5f));
            Add(topRow);

            _weatherText = new Label(string.Empty);
            _weatherText.name = "WeatherIcon";
            _weatherText.style.fontSize = 21.6f;
            topRow.Add(_weatherText);
            // [P27] 시간은 좌상단 글래스 시계(TimeClockGlassUTK)로 이관 — 미니맵의 중복 HH:MM 제거.

            // 온도 게이지 (좌측) — 상단 값 라벨
            var tempGauge = new VisualElement();
            tempGauge.name = "TempGauge";
            tempGauge.style.position = Position.Absolute;
            tempGauge.style.left = -22f;
            tempGauge.style.top = (Diameter - 160f) * 0.5f;
            tempGauge.style.width = 16f;
            tempGauge.style.height = 160f;
            tempGauge.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.6f));
            Add(tempGauge);

            _tempFill = new VisualElement { name = "TempFill" };
            _tempFill.style.position = Position.Absolute;
            _tempFill.style.left = 1f;
            _tempFill.style.width = 14f;
            _tempFill.style.top = 78f;
            _tempFill.style.height = 4f;
            _tempFill.style.backgroundColor = new StyleColor(new Color(0.5f, 0.8f, 0.3f, 1f));
            tempGauge.Add(_tempFill);

            _tempText = new Label("0°");
            _tempText.name = "TempText";
            _tempText.style.position = Position.Absolute;
            _tempText.style.left = -44f;
            _tempText.style.top = (Diameter * 0.5f) - 8f;
            _tempText.style.width = 60f;
            _tempText.style.height = 18f;
            _tempText.style.fontSize = 13.2f;
            _tempText.style.color = new StyleColor(new Color(0.5f, 0.8f, 0.3f, 1f));
            _tempText.style.unityTextAlign = TextAnchor.MiddleCenter;
            Add(_tempText);

            // 소음 게이지 (우측) — 소음 수치 라벨
            var soundGauge = new VisualElement();
            soundGauge.name = "SoundGauge";
            soundGauge.style.position = Position.Absolute;
            soundGauge.style.right = -22f;
            soundGauge.style.top = (Diameter - 160f) * 0.5f;
            soundGauge.style.width = 16f;
            soundGauge.style.height = 160f;
            soundGauge.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.6f));
            Add(soundGauge);

            _soundFill = new VisualElement { name = "SoundFill" };
            _soundFill.style.position = Position.Absolute;
            _soundFill.style.left = 1f;
            _soundFill.style.width = 14f;
            _soundFill.style.top = 159f;
            _soundFill.style.height = 0f;
            _soundFill.style.backgroundColor = new StyleColor(new Color(0.3f, 0.8f, 1f, 1f));
            soundGauge.Add(_soundFill);

            _soundText = new Label("0");
            _soundText.name = "SoundText";
            _soundText.style.position = Position.Absolute;
            _soundText.style.right = -44f;
            _soundText.style.top = (Diameter * 0.5f) - 8f;
            _soundText.style.width = 60f;
            _soundText.style.height = 18f;
            _soundText.style.fontSize = 13.2f;
            _soundText.style.color = new StyleColor(new Color(0.3f, 0.8f, 1f, 1f));
            _soundText.style.unityTextAlign = TextAnchor.MiddleCenter;
            Add(_soundText);

            UTKWindowBase.ApplyUIToolkitFont(this);

            // 지연 초기화 요청 (텍스처는 부팅 중 스플랫 생성 후 1회 주입)
            _playerTransform = FindPlayer();
        }

        private static Transform FindPlayer()
        {
            var go = GameObject.FindWithTag("Player");
            return go != null ? go.transform : null;
        }

        // =====================================================================
        //  맵 텍스처 주입 (원본 TryApplyMapTexture — 지연 재시도)
        // =====================================================================
        private void TryApplyMapTexture()
        {
            if (_mapTexture != null) return;
            var splat = TerrainSplatBaker.LastWorldSplat;
            if (splat == null) return;

            _mapTexture = splat;
            _mapCanvas.style.backgroundImage = UTKTextureSafe.ToBackground(splat);
            Debug.Log("[MinimapUTK] 지형 텍스처 주입 완료: " + splat.name);
        }

        private void UpdateMapCrop()
        {
            if (_mapCanvas == null) return;

            float side = _mapFrame != null && _mapFrame.contentRect.width > 0f
                ? _mapFrame.contentRect.width
                : Diameter - 4f;
            if (_playerTransform == null || _mapTexture == null)
            {
                // No player: retain the legacy full-map view.
                _mapCanvas.style.left = 0f;
                _mapCanvas.style.top = 0f;
                _mapCanvas.style.width = side;
                _mapCanvas.style.height = side;
                return;
            }

            // Match MinimapUI's UVRect math and TerrainSplatBaker's world mapping:
            // u=0.5+x/W and v=0.5+z/W, with a 2*LocalRadius world-space window.
            float span = Mathf.Clamp01((LocalRadius * 2f) / TerrainSplatBaker.WORLD_SIZE);
            Vector3 p = _playerTransform.position;
            float u = Mathf.Clamp01(0.5f + p.x / TerrainSplatBaker.WORLD_SIZE);
            float v = Mathf.Clamp01(0.5f + p.z / TerrainSplatBaker.WORLD_SIZE);
            float u0 = Mathf.Clamp(u - span * 0.5f, 0f, 1f - span);
            float v0 = Mathf.Clamp(v - span * 0.5f, 0f, 1f - span);
            float imageSide = side / span;

            // A single oversized child, clipped by MapFrame, performs the UV crop without
            // allocating/copying textures. In the old UVRect convention v=0 is the bottom
            // (negative z) and v grows north. UI Toolkit paints textures with row 0 at the
            // top, so the matching northward crop begins at the complementary top offset.
            _mapCanvas.style.left = -u0 * imageSide;
            _mapCanvas.style.top = -(1f - (v0 + span)) * imageSide;
            _mapCanvas.style.width = imageSide;
            _mapCanvas.style.height = imageSide;
        }

        // =====================================================================
        //  폴링 갱신 (0.4초 — 원본 Update 프레임 루프 대체)
        // =====================================================================
        private void Refresh()
        {
            TryApplyMapTexture();

            // 시스템 데이터 실측
            var tempSys = TemperatureSystem.Instance;
            _temperature = tempSys != null ? tempSys.CurrentTemperature : -0.2f;

            var soundSys = SoundSystem.Instance;
            if (soundSys != null)
                _soundLevel = soundSys.CurrentNoiseLevel;
            else if (_playerTransform != null)
                _soundLevel = 0.1f;

            var twSys = TimeWeatherSystem.Instance;
            if (twSys != null)
            {
                _weather = twSys.CurrentWeather;
            }

            if (_playerTransform == null)
                _playerTransform = FindPlayer();

            // [146차 P2] 실내/월드 분기 — 실내는 RT 캡처 평면도(크롭 산식 우회), 월드는 기존 경로 그대로.
            //   월드 경로 로직(UpdateMapCrop 본체)은 무수정 단일소스 계약 — 분기 추가만.
            bool indoorNow = IsIndoorMapActive();
            if (indoorNow)
            {
                UpdateIndoorMap();
            }
            else
            {
                RestoreWorldMapIfIndoorApplied();   // 실내 중 교체한 배경 원복(지오메트리는 UpdateMapCrop이 매 리프레시 재적용)
                UpdateMapCrop();
            }

            // 시간/날씨 — [P27] 시간은 글래스 시계로 이관, 미니맵은 날씨 아이콘만
            switch (_weather)
            {
                case WeatherType.Rain:
                case WeatherType.Storm: _weatherText.text = "🌧"; break;
                case WeatherType.Snow:  _weatherText.text = "❄"; break;
                case WeatherType.Night: _weatherText.text = "🌙"; break;
                default:                _weatherText.text = "☀"; break;
            }

            // 온도 색상 + 수치
            Color tempColor = _temperature < -0.1f
                ? Color.Lerp(new Color(0.5f, 0.8f, 0.3f, 1f), new Color(0.2f, 0.5f, 1f, 1f), Mathf.Abs(_temperature))
                : _temperature > 0.1f
                    ? Color.Lerp(new Color(0.5f, 0.8f, 0.3f, 1f), new Color(1f, 0.3f, 0.2f, 1f), _temperature)
                    : new Color(0.5f, 0.8f, 0.3f, 1f);
            _tempText.style.color = new StyleColor(tempColor);
            _tempText.text = _temperature > 0
                ? $"+{_temperature * 50f:F0}°"
                : $"{_temperature * 50f:F0}°";

            // 소음 색상 + 수치
            Color soundLow = new Color(0.3f, 0.8f, 1f, 1f);
            Color soundMid = new Color(1f, 0.8f, 0.2f, 1f);
            Color soundHigh = new Color(1f, 0.2f, 0.2f, 1f);
            Color soundColor = _soundLevel < 0.3f
                ? soundLow
                : _soundLevel < 0.7f
                    ? Color.Lerp(soundLow, soundMid, (_soundLevel - 0.3f) / 0.4f)
                    : Color.Lerp(soundMid, soundHigh, (_soundLevel - 0.7f) / 0.3f);
            _soundText.style.color = new StyleColor(soundColor);
            _soundText.text = Mathf.RoundToInt(_soundLevel * 100f).ToString();

            // Restore legacy fill geometry: signed temperature around midpoint; sound rises from bottom.
            float tempMagnitude = Mathf.Abs(_temperature);
            if (_temperature < -0.1f)
            {
                _tempFill.style.top = 80f - (78f * tempMagnitude);
                _tempFill.style.height = 78f * tempMagnitude;
                _tempFill.style.backgroundColor = new StyleColor(Color.Lerp(
                    new Color(0.5f, 0.8f, 0.3f, 1f), new Color(0.2f, 0.5f, 1f, 1f), tempMagnitude));
            }
            else if (_temperature > 0.1f)
            {
                _tempFill.style.top = 80f;
                _tempFill.style.height = 78f * tempMagnitude;
                _tempFill.style.backgroundColor = new StyleColor(Color.Lerp(
                    new Color(0.5f, 0.8f, 0.3f, 1f), new Color(1f, 0.3f, 0.2f, 1f), _temperature));
            }
            else
            {
                _tempFill.style.top = 78f;
                _tempFill.style.height = 4f;
                _tempFill.style.backgroundColor = new StyleColor(new Color(0.5f, 0.8f, 0.3f, 1f));
            }

            float soundFillHeight = (160f - 4f) * _soundLevel;
            _soundFill.style.top = 160f - soundFillHeight - 1f;
            _soundFill.style.height = soundFillHeight;
            _soundFill.style.backgroundColor = new StyleColor(soundColor);

            // Keep all spatial overlays hidden until UI Toolkit's vertical texture orientation
            // is source-proven against the legacy GUI UVRect path (north/south must not invert).
            // [146차 P2] 실내에서는 UpdateIndoorMap이 핀을 MapToUv로 배치 — 월드 경로 숨김은 실내 중 스킵
            //   (배경 캡처와 핀이 동일 MapToUv 좌표계 — 자기정합, 방향 근거는 UpdateIndoorMap 주석).
            if (!indoorNow)
                _playerMarker.style.display = DisplayStyle.None;

            // 영지/퀘스트 마커 (matching terrain crop이 확보되기 전까지 숨김)
            // [146차 P2] 실내 중 스킵 — 영지/퀘스트 마커는 월드 전용(실내 무표시 계약) + 블립 보존
            //   (ClearMarkers가 _markerHost 전체를 비워 유닛 블립도 떼어낸다).
            if (!indoorNow)
                RefreshMarkers();

            // [125차-38] 목적지 경로 오버레이 갱신 훅(1곳 계약) — UpdateMapCrop 플레이어 중심 크롭과 동기.
            //   세그먼트 좌표는 FillBlips/크롭과 동일 산식(+z=북=상) — 리프레시마다 재계산으로 플레이어 이동 추종.
            //   경로 비활성/플레이어 부재 시 오버레이 측에서 자동 숨김(무동작 호출).
            // [146차 P2] 실내 중 경로 오버레이 비활성(월드 좌표 오염 방지) — null 플레이어 호출 =
            //   오버레이 자동 숨김(경로 '상태'는 유지 — 퇴출 후 월드 복귀 시 즉시 재표시).
            if (indoorNow)
            {
                MinimapRouteOverlay.OnMinimapRefresh(_mapFrame, null, LocalRadius, Diameter - 4f);
            }
            else
            {
                MinimapRouteOverlay.OnMinimapRefresh(_mapFrame,
                    _playerTransform != null ? _playerTransform.position : (Vector3?)null,
                    LocalRadius, Diameter - 4f);
            }
        }

        // =====================================================================
        //  영지/퀘스트 마커 — 원본 DrawMarkerOverlay 실제 데이터 실측
        //  로컬뷰: +x=우, +z=북(위), 플레이어 중앙. radius 밖 마커는 스킵.
        // =====================================================================
        // =====================================================================
        //  [15차 A4] 유닛 블립 — 크롭 수식과 동일 좌표계(북=상, 동=우)
        // =====================================================================
        public void RefreshUnitBlips()
        {
            if (_markerHost == null || _playerTransform == null) return;
            // [146차 P2] 실내 — 절대 좌표(MapToUv) 배치 분기(월드 로컬뷰 상대 산식은 무수정).
            if (IsIndoorMapActive())
            {
                RefreshUnitBlipsIndoor();
                return;
            }
            float side = _mapFrame != null && _mapFrame.contentRect.width > 0f
                ? _mapFrame.contentRect.width
                : Diameter - 4f;
            float half = side * 0.5f;
            Vector3 pp = _playerTransform.position;

            float sideLen = half * 2f;
            int used = 0;
            used = FillBlips(used, half, sideLen, pp,
                UnityEngine.Object.FindObjectsByType<GuardPlaceholder>(FindObjectsSortMode.None),
                AllyBlipColor);
            used = FillBlips(used, half, sideLen, pp,
                UnityEngine.Object.FindObjectsByType<AnimalAI>(FindObjectsSortMode.None),
                EnemyBlipColor);

            // 미사용 풀 숨김
            for (int i = used; i < _unitBlipPool.Count; i++)
                _unitBlipPool[i].style.display = DisplayStyle.None;
        }

        private int FillBlips(int used, float half, float sideLen, Vector3 playerPos,
            GuardPlaceholder[] units, Color color)
        {
            foreach (var unit in units)
            {
                if (unit == null || !unit.IsAlive || !unit.IsRecruited || used >= MaxUnitBlips) continue;
                Vector3 up = unit.transform.position;
                float dx = up.x - playerPos.x;
                float dz = up.z - playerPos.z;
                if (Mathf.Abs(dx) > LocalRadius || Mathf.Abs(dz) > LocalRadius) continue;
                float px = half + (dx / (2f * LocalRadius)) * sideLen;
                float py = half - (dz / (2f * LocalRadius)) * sideLen;

                VisualElement blip;
                if (used < _unitBlipPool.Count) blip = _unitBlipPool[used];
                else
                {
                    blip = new VisualElement { name = "UnitBlip", pickingMode = PickingMode.Ignore };
                    blip.style.position = Position.Absolute;
                    blip.style.width = UnitBlipSize;
                    blip.style.height = UnitBlipSize;
                    blip.style.borderTopLeftRadius = 4f;
                    blip.style.borderTopRightRadius = 4f;
                    blip.style.borderBottomLeftRadius = 4f;
                    blip.style.borderBottomRightRadius = 4f;
                    _markerHost.Add(blip);
                    _unitBlipPool.Add(blip);
                }
                blip.style.display = DisplayStyle.Flex;
                blip.style.left = px - UnitBlipSize * 0.5f;
                blip.style.top = py - UnitBlipSize * 0.5f;
                blip.style.backgroundColor = color;
                used++;
            }
            return used;
        }

        private int FillBlips(int used, float half, float sideLen, Vector3 playerPos,
            AnimalAI[] units, Color color)
        {
            foreach (var unit in units)
            {
                if (unit == null || !unit.IsAlive || used >= MaxUnitBlips) continue;
                Vector3 up = unit.transform.position;
                float dx = up.x - playerPos.x;
                float dz = up.z - playerPos.z;
                if (Mathf.Abs(dx) > LocalRadius || Mathf.Abs(dz) > LocalRadius) continue;
                float px = half + (dx / (2f * LocalRadius)) * sideLen;
                float py = half - (dz / (2f * LocalRadius)) * sideLen;

                VisualElement blip;
                if (used < _unitBlipPool.Count) blip = _unitBlipPool[used];
                else
                {
                    blip = new VisualElement { name = "UnitBlip", pickingMode = PickingMode.Ignore };
                    blip.style.position = Position.Absolute;
                    blip.style.width = UnitBlipSize;
                    blip.style.height = UnitBlipSize;
                    blip.style.borderTopLeftRadius = 4f;
                    blip.style.borderTopRightRadius = 4f;
                    blip.style.borderBottomLeftRadius = 4f;
                    blip.style.borderBottomRightRadius = 4f;
                    _markerHost.Add(blip);
                    _unitBlipPool.Add(blip);
                }
                blip.style.display = DisplayStyle.Flex;
                blip.style.left = px - UnitBlipSize * 0.5f;
                blip.style.top = py - UnitBlipSize * 0.5f;
                blip.style.backgroundColor = color;
                used++;
            }
            return used;
        }

        // =====================================================================
        //  [146차 P2] 실내 평면도 분기 — RT 캡처 배경 + MapToUv 절대 배치
        //  (월드 경로 UpdateMapCrop/RefreshMarkers/월드 블립 산식 무수정 — 단일소스 계약)
        // =====================================================================

        /// <summary>[146차 P2] 실내 미니맵 모드 판정 단일 소스 — IndoorSceneTransition.IsIndoor(동일 UI
        ///   어셈블리 직접 참조) 또는 테스트씬 경유(Test_21은 UITransitionState.IndoorActive=true만 세팅 —
        ///   IsIndoor=false 실측)는 유효 캡처 존재를 조건으로 실내 판정. Refresh/RefreshUnitBlips 공용.</summary>
        private static bool IsIndoorMapActive()
        {
            return ProjectName.UI.IndoorSceneTransition.IsIndoor
                || (UITransitionState.IndoorActive && IndoorMinimapBaker.HasCapture);
        }

        /// <summary>[146차 P2] 실내 배경+핀 갱신 — 정사각 RT를 지름에 맞춰 중앙 배치(UpdateMapCrop
        ///   플레이어 중심 크롭 산식 우회: 실내는 방 전체가 시야 계약 — 본체는 무변경).
        ///   방향 근거: 캡처 카메라 Euler(90,0,0)로 화면 위=+z(북) — px=u·side, py=(1−v)·side는
        ///   UpdateMapCrop 크롭/MinimapRouteOverlay(py=half−dz·k)의 +z=북=상 규약과 동일 좌표계.</summary>
        private void UpdateIndoorMap()
        {
            var tex = IndoorMinimapBaker.LatestTexture;
            var bounds = IndoorMinimapBaker.LatestBounds;
            if (tex == null)
            {
                // 캡처 소실(리빌드 공백 프레임 등) — 배경 유지, 핀만 숨김(무음 — 크래시 금지 계약)
                _playerMarker.style.display = DisplayStyle.None;
                return;
            }

            // 배경 — 텍스처 교체 시에만 재적용(UTKTextureSafe 캐시 — 매 리프레시 재래핑 방지)
            if (!ReferenceEquals(_indoorBgApplied, tex))
            {
                _mapCanvas.style.backgroundImage = UTKTextureSafe.ToBackground(tex);
                _indoorBgApplied = tex;
            }

            // 지름에 맞춘 중앙 배치 — 정사각 RT(512²) → 원형 프레임에 1:1 수용(월드 크롭 지오메트리 대체)
            float side = _mapFrame != null && _mapFrame.contentRect.width > 0f
                ? _mapFrame.contentRect.width
                : Diameter - 4f;
            _mapCanvas.style.left = 0f;
            _mapCanvas.style.top = 0f;
            _mapCanvas.style.width = side;
            _mapCanvas.style.height = side;

            // 플레이어 핀 — MapToUv 절대 매핑(기존 핀 배치 코드 재사용 — 로컬 UV만 교체)
            if (_playerTransform != null && bounds.size.x > 0f && bounds.size.z > 0f)
            {
                Vector2 uv = IndoorMinimapBaker.MapToUv(_playerTransform.position, bounds);
                _playerMarker.style.display = DisplayStyle.Flex;
                _playerMarker.style.left = uv.x * side - PlayerMarkerSize * 0.5f;
                _playerMarker.style.top = (1f - uv.y) * side - PlayerMarkerSize * 0.5f;
            }
            else
            {
                _playerMarker.style.display = DisplayStyle.None;
            }
        }

        /// <summary>[146차 P2] 월드 복귀 원복 — 실내 중 교체한 배경만 복구. 프레임 지오메트리는
        ///   UpdateMapCrop이 매 리프레시 재적용(월드 경로 상태 복원 계약)하므로 여기서 손대지 않는다.</summary>
        private void RestoreWorldMapIfIndoorApplied()
        {
            if (_indoorBgApplied == null) return;
            _indoorBgApplied = null;
            if (_mapTexture != null)
                _mapCanvas.style.backgroundImage = UTKTextureSafe.ToBackground(_mapTexture);
        }

        /// <summary>[146차 P2] 실내 유닛 블립 — MapToUv 절대 매핑(월드 로컬뷰 상대 산식과 분리).
        ///   필터/풀 운용은 기존 FillBlips 계약 동일(IsAlive/IsRecruited, MaxUnitBlips 상한).</summary>
        private void RefreshUnitBlipsIndoor()
        {
            var bounds = IndoorMinimapBaker.LatestBounds;
            if (IndoorMinimapBaker.LatestTexture == null || bounds.size.x <= 0f || bounds.size.z <= 0f)
            {
                for (int i = 0; i < _unitBlipPool.Count; i++)
                    _unitBlipPool[i].style.display = DisplayStyle.None;
                return;
            }
            float side = _mapFrame != null && _mapFrame.contentRect.width > 0f
                ? _mapFrame.contentRect.width
                : Diameter - 4f;
            int used = 0;
            used = FillBlipsIndoor(used, side, bounds,
                UnityEngine.Object.FindObjectsByType<GuardPlaceholder>(FindObjectsSortMode.None),
                AllyBlipColor);
            used = FillBlipsIndoor(used, side, bounds,
                UnityEngine.Object.FindObjectsByType<AnimalAI>(FindObjectsSortMode.None),
                EnemyBlipColor);
            for (int i = used; i < _unitBlipPool.Count; i++)
                _unitBlipPool[i].style.display = DisplayStyle.None;
        }

        private int FillBlipsIndoor(int used, float side, Bounds bounds, GuardPlaceholder[] units, Color color)
        {
            foreach (var unit in units)
            {
                if (unit == null || !unit.IsAlive || !unit.IsRecruited || used >= MaxUnitBlips) continue;
                used = PlaceIndoorBlip(used, side, bounds, unit.transform.position, color);
            }
            return used;
        }

        private int FillBlipsIndoor(int used, float side, Bounds bounds, AnimalAI[] units, Color color)
        {
            foreach (var unit in units)
            {
                if (unit == null || !unit.IsAlive || used >= MaxUnitBlips) continue;
                used = PlaceIndoorBlip(used, side, bounds, unit.transform.position, color);
            }
            return used;
        }

        /// <summary>블립 1개 절대 배치 — 풀 슬롯 재사용+재부착(월드 경로 ClearMarkers가 떼어낸 슬롯 복원).</summary>
        private int PlaceIndoorBlip(int used, float side, Bounds bounds, Vector3 worldPos, Color color)
        {
            VisualElement blip;
            if (used < _unitBlipPool.Count)
            {
                blip = _unitBlipPool[used];
            }
            else
            {
                blip = new VisualElement { name = "UnitBlip", pickingMode = PickingMode.Ignore };
                blip.style.position = Position.Absolute;
                blip.style.width = UnitBlipSize;
                blip.style.height = UnitBlipSize;
                blip.style.borderTopLeftRadius = 4f;
                blip.style.borderTopRightRadius = 4f;
                blip.style.borderBottomLeftRadius = 4f;
                blip.style.borderBottomRightRadius = 4f;
                _unitBlipPool.Add(blip);
            }
            if (blip.parent == null) _markerHost.Add(blip);   // 풀 슬롯 재부착 — 실내 진입 시 떼어진 슬롯 복원
            Vector2 uv = IndoorMinimapBaker.MapToUv(worldPos, bounds);
            blip.style.display = DisplayStyle.Flex;
            blip.style.left = uv.x * side - UnitBlipSize * 0.5f;
            blip.style.top = (1f - uv.y) * side - UnitBlipSize * 0.5f;
            blip.style.backgroundColor = color;
            return used + 1;
        }

        /// <summary>[15차 A4] 미니맵 우클릭 → 클릭 지점의 월드 좌표 역산 → 선택 병사 이동 명령.</summary>
        private void OnMapPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 1) return;
            // [146차 P2] 실내 평면도 클릭 — 월드 좌표 역산 산식이 성립하지 않아 명령 무시(월드 좌표 오염 방지).
            if (IsIndoorMapActive()) return;
            if (_playerTransform == null) return;
            var rts = RTSCommandSystem.Instance;
            if (rts == null || rts.SelectedCount == 0) return;   // 선택 없음 → 명령 없음(카메라 점프 미구현)

            float sideLen = _mapFrame != null && _mapFrame.contentRect.width > 0f
                ? _mapFrame.contentRect.width
                : Diameter - 4f;
            Vector2 local = evt.localPosition;
            float dx = ((local.x / sideLen) - 0.5f) * 2f * LocalRadius;
            float dz = -((local.y / sideLen) - 0.5f) * 2f * LocalRadius;
            Vector3 world = new Vector3(_playerTransform.position.x + dx, _playerTransform.position.y, _playerTransform.position.z + dz);
            try { world.y = 1f + TerrainGenerator.GetHeightAt(world.x, world.z, BiomeType.Plains, 42); }
            catch { /* 지형 조회 실패 시 플레이어 y 폴백 */ }
            rts.IssueMoveCommandAtWorld(world);
        }

        private void RefreshMarkers()
        {
            if (_markerHost == null) return;
            ClearMarkers();

            // Keep terrain-dependent markers off: the legacy UVRect uses bottom-origin UVs,
            // while UI Toolkit backgroundImage's vertical mapping is not source-proven here.
            // Local crop is rendered, but dots wait until north/south alignment can be guaranteed.
        }

        private void ClearMarkers()
        {
            for (int i = _markerHost.childCount - 1; i >= 0; i--)
                _markerHost[i].RemoveFromHierarchy();
        }


        // =====================================================================
        //  Updater — UIRoot 부착 + 0.4초 폴링
        // =====================================================================
        private class Updater : MonoBehaviour
        {
            public MinimapUTK map;
            private float _tick = 0.4f;
            private float _blipTick = 0.25f;   // [15차 A4] 유닛 블립 전용 고속 틱

            private void Update()
            {
                var root = UIToolkitBootstrap.UIRoot;
                if (root != null && map != null && map.parent == null)
                    root.Add(map);

                if (map == null) return;
                _tick -= Time.unscaledDeltaTime;
                if (_tick <= 0f)
                {
                    _tick = 0.4f;
                    if (map.parent != null)
                        map.Refresh();
                }
                // [15차 A4] 유닛 블립 고속 틱(0.25s) — 유닛 이동 추종
                _blipTick -= Time.unscaledDeltaTime;
                if (_blipTick <= 0f)
                {
                    _blipTick = 0.25f;
                    if (map.parent != null)
                        map.RefreshUnitBlips();
                }
            }
        }
    }
}