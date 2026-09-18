using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;        // PlayerInventory 불필요
using ProjectName.Core.Data;   // TerritoryDatabase/TerritoryDefinition
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
        private const float PlayerMarkerSize = 12f;

        private VisualElement _mapCanvas;      // 배경 이미지 (월드 스플랫)
        private VisualElement _mapFrame;       // 원형 마스킹용 오버레이 프레임
        private VisualElement _markerHost;     // 영지/퀘스트 마커 (절대배치)
        private VisualElement _playerMarker;   // 플레이어 삼각/사각 마커
        private Label _timeText;               // "HH:00"
        private Label _tempText;               // 온도 수치
        private Label _soundText;              // 소음 수치
        private Label _weatherText;            // 날씨 아이콘

        // 상태 (원본 시스템 데이터에서 실측)
        private float _temperature = -0.2f;
        private float _soundLevel = 0f;
        private float _timeOfDay = 0.5f;
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
            Add(_mapFrame);

            _mapCanvas = new VisualElement();
            _mapCanvas.name = "MapCanvas";
            _mapCanvas.style.position = Position.Absolute;
            _mapCanvas.style.left = 0f;
            _mapCanvas.style.top = 0f;
            _mapCanvas.style.right = 0f;
            _mapCanvas.style.bottom = 0f;
            _mapCanvas.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
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
            _weatherText.style.fontSize = 20f;
            topRow.Add(_weatherText);

            _timeText = new Label("12:00");
            _timeText.name = "TimeText";
            _timeText.style.fontSize = 13f;
            _timeText.style.color = new StyleColor(Color.white);
            _timeText.style.unityFontStyleAndWeight = FontStyle.Bold;
            topRow.Add(_timeText);

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

            _tempText = new Label("0°");
            _tempText.name = "TempText";
            _tempText.style.position = Position.Absolute;
            _tempText.style.left = -44f;
            _tempText.style.top = (Diameter * 0.5f) - 8f;
            _tempText.style.width = 60f;
            _tempText.style.height = 18f;
            _tempText.style.fontSize = 13f;
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

            _soundText = new Label("0");
            _soundText.name = "SoundText";
            _soundText.style.position = Position.Absolute;
            _soundText.style.right = -44f;
            _soundText.style.top = (Diameter * 0.5f) - 8f;
            _soundText.style.width = 60f;
            _soundText.style.height = 18f;
            _soundText.style.fontSize = 13f;
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
            _mapCanvas.style.backgroundImage = new StyleBackground(Background.FromTexture2D(splat));
            Debug.Log("[MinimapUTK] 지형 텍스처 주입 완료: " + splat.name);
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
                _timeOfDay = twSys.TimeOfDay;
                _weather = twSys.CurrentWeather;
            }

            if (_playerTransform == null)
                _playerTransform = FindPlayer();

            // 시간/날씨
            int hour = Mathf.FloorToInt(_timeOfDay * 24f);
            _timeText.text = string.Format("{0:D2}:00", hour);
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
            Color soundColor = _soundLevel < 0.3f
                ? new Color(0.3f, 0.8f, 1f, 1f)
                : _soundLevel < 0.7f
                    ? new Color(1f, 0.8f, 0.2f, 1f)
                    : new Color(1f, 0.2f, 0.2f, 1f);
            _soundText.style.color = new StyleColor(soundColor);
            _soundText.text = Mathf.RoundToInt(_soundLevel * 100f).ToString();

            // 플레이어 마커 — 중앙 고정 (로컬뷰). 지형 준비 전엔 숨김.
            _playerMarker.style.display = _mapTexture != null ? DisplayStyle.Flex : DisplayStyle.None;
            _playerMarker.style.left = (Diameter - PlayerMarkerSize) * 0.5f;
            _playerMarker.style.top = (Diameter - PlayerMarkerSize) * 0.5f;

            // 영지/퀘스트 마커 (지형+플레이어 준비 시에만)
            RefreshMarkers();
        }

        // =====================================================================
        //  영지/퀘스트 마커 — 원본 DrawMarkerOverlay 실제 데이터 실측
        //  로컬뷰: +x=우, +z=북(위), 플레이어 중앙. radius 밖 마커는 스킵.
        // =====================================================================
        private const float LocalRadius = 120f;

        private void RefreshMarkers()
        {
            if (_markerHost == null) return;
            ClearMarkers();

            if (_mapTexture == null || _playerTransform == null) return;

            float radius = Diameter * 0.5f;
            float pxPerWorld = Diameter / (LocalRadius * 2f);
            Vector3 pp = _playerTransform.position;
            float mSize = 4f;

            // ① 영지 마커 (검은 점) — 추후 겹침 최소화
            var db = TerritoryDatabase.Instance;
            if (db != null)
            {
                foreach (var def in db.GetAllDefinitions())
                {
                    Vector3 wp = GetTerritoryWorldPosition(def);
                    if (wp == Vector3.zero) continue;
                    Vector3? pos = TryLocalPos(wp, pp, pxPerWorld, radius - mSize);
                    if (!pos.HasValue) continue;
                    AddMarker(pos.Value, mSize, new Color(0f, 0f, 0f, 0.85f));
                }
            }

            // ② 활성 퀘스트 마커 (퀘스트색 점, 더 큼)
            var qms = QuestMarkerSystem.Instance;
            if (qms != null)
            {
                float qSize = 7f;
                foreach (var qm in qms.GetActiveQuestMarkers())
                {
                    Vector3? pos = TryLocalPos(qm.worldPos, pp, pxPerWorld, radius - 2f);
                    if (!pos.HasValue) continue;
                    AddMarker(pos.Value, qSize, qm.markerColor);
                }
            }
        }

        /// <summary>월드 좌표 → 로컬뷰 마커 위치 (캔버스 좌상단 기준). 반경 밖이면 null.</summary>
        private Vector3? TryLocalPos(Vector3 wp, Vector3 pp, float pxPerWorld, float maxDist)
        {
            float dx = (wp.x - pp.x) * pxPerWorld;
            float dy = -(wp.z - pp.z) * pxPerWorld;   // +z=북=위 (UI Toolkit y 하향 보정)
            float distSq = dx * dx + dy * dy;
            if (distSq > maxDist * maxDist) return null;
            float cx = Diameter * 0.5f;
            float cy = Diameter * 0.5f;
            return new Vector3(cx + dx, cy + dy, 0f);
        }

        private void AddMarker(Vector3 center, float size, Color color)
        {
            var dot = new VisualElement();
            dot.name = "MapMarker";
            dot.style.position = Position.Absolute;
            dot.style.width = size;
            dot.style.height = size;
            dot.style.left = center.x - size * 0.5f;
            dot.style.top = center.y - size * 0.5f;
            dot.style.backgroundColor = new StyleColor(color);
            _markerHost.Add(dot);
        }

        private void ClearMarkers()
        {
            for (int i = _markerHost.childCount - 1; i >= 0; i--)
                _markerHost[i].RemoveFromHierarchy();
        }

        /// <summary>영지 정의 → 미니맵용 월드 위치 (원본 GetTerritoryWorldPosition 동일 규칙).</summary>
        private static Vector3 GetTerritoryWorldPosition(TerritoryDefinition def)
        {
            Vector3 dir;
            switch (def.nation)
            {
                case NationType.North: dir = new Vector3(0f, 0f, 1f); break;
                case NationType.East:  dir = new Vector3(1f, 0f, 0f); break;
                case NationType.South: dir = new Vector3(0f, 0f, -1f); break;
                case NationType.West:  dir = new Vector3(-1f, 0f, 0f); break;
                default: return Vector3.zero;   // Empire(중앙) — 플레이어 마커와 겹치므로 스킵
            }
            float distance;
            switch (def.difficulty)
            {
                case TerritoryDifficulty.Ring1: distance = 15f; break;
                case TerritoryDifficulty.Ring2: distance = 30f; break;
                case TerritoryDifficulty.Ring3: distance = 45f; break;
                case TerritoryDifficulty.Ring4: distance = 60f; break;
                default: distance = 20f; break;
            }
            float spreadAngle = (def.id.index % 5) * 18f;
            Vector3 spread = Quaternion.Euler(0f, spreadAngle, 0f) * dir;
            if (spread.sqrMagnitude < 0.01f) spread = dir;
            return spread.normalized * distance;
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