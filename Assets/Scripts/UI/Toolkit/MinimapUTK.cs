using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;        // PlayerInventory 불필요
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
        private const float Diameter   = 220f;
        private const float MarginLeft = 20f;
        private const float MarginTop  = 20f;
        private const float FigmaBezelDiameter = 264f;
        private const float FigmaBezelRight = 24f;
        private const float FigmaBezelTop = 24f;
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
        private Texture2D _mapTexture;

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
            _mapFrame.name = "MapFrame";
            _mapFrame.style.position = Position.Absolute;
            _mapFrame.style.left = 0f;
            _mapFrame.style.top = 0f;
            _mapFrame.style.right = 0f;
            _mapFrame.style.bottom = 0f;
            _mapFrame.style.borderTopWidth = 2f;
            _mapFrame.style.borderRightWidth = 2f;
            _mapFrame.style.borderBottomWidth = 2f;
            _mapFrame.style.borderLeftWidth = 2f;
            _mapFrame.style.borderTopColor = new StyleColor(new Color(1f, 1f, 1f, 0.3f));
            _mapFrame.style.borderRightColor = new StyleColor(new Color(1f, 1f, 1f, 0.3f));
            _mapFrame.style.borderBottomColor = new StyleColor(new Color(1f, 1f, 1f, 0.3f));
            _mapFrame.style.borderLeftColor = new StyleColor(new Color(1f, 1f, 1f, 0.3f));
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

            UpdateMapCrop();

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
            _playerMarker.style.display = DisplayStyle.None;

            // 영지/퀘스트 마커 (matching terrain crop이 확보되기 전까지 숨김)
            RefreshMarkers();
        }

        // =====================================================================
        //  영지/퀘스트 마커 — 원본 DrawMarkerOverlay 실제 데이터 실측
        //  로컬뷰: +x=우, +z=북(위), 플레이어 중앙. radius 밖 마커는 스킵.
        // =====================================================================
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
            }
        }
    }
}