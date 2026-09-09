using UnityEngine;
using UnityEngine.UI;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using WeatherType = ProjectName.Systems.TimeWeatherSystem.WeatherType;

namespace ProjectName.UI
{
    /// <summary>
    /// BotW 스타일 원형 미니맵 (우상단)
    /// - 원형 미니맵 배경 + 플레이어 위치 표시
    /// - 온도 게이지 (좌측: 추위/더위)
    /// - 소음 게이지 (우측: 발소리 크기)
    /// - 타임오브데이/날씨 아이콘 (미니맵 위)
    /// </summary>
    public class MinimapUI : MonoBehaviour
    {
        [Header("Minimap Settings")]
        [SerializeField] private int _minimapDiameter = 220;
        [SerializeField] private int _marginRight = 20;
        [SerializeField] private int _marginTop = 20;
        [SerializeField] private float _mapScale = 0.001f; // 월드 단위 → 미니맵 픽셀 (전체맵 폴백용)

        [Header("Local View (2026-09-09)")]
        [Tooltip("플레이어 중심 로컬뷰 반경(m) — 휠로 3단 조절")]
        [SerializeField] private float _localRadius = 120f;
        private static readonly float[] _zoomRadii = { 80f, 120f, 200f };
        private int _zoomIndex = 1;
        [SerializeField] private Texture2D _mapTexture; // 미리 렌더된 맵 텍스처 (선택)

        [Header("Player Marker")]
        [SerializeField] private Color _playerMarkerColor = new Color(0.3f, 0.7f, 1f, 1f);
        [SerializeField] private int _playerMarkerSize = 12;

        [Header("Temperature Gauge (좌측)")]
        [SerializeField] private int _tempGaugeWidth = 16;
        [SerializeField] private int _tempGaugeHeight = 160;
        [SerializeField] private int _tempGaugeOffsetX = -12; // 미니맵 왼쪽으로
        [SerializeField] private Color _tempColdColor = new Color(0.2f, 0.5f, 1f, 1f);
        [SerializeField] private Color _tempNormalColor = new Color(0.5f, 0.8f, 0.3f, 1f);
        [SerializeField] private Color _tempHotColor = new Color(1f, 0.3f, 0.2f, 1f);
        [SerializeField] private Sprite _coldIcon; // 눈송이 아이콘
        [SerializeField] private Sprite _hotIcon;  // 불꽃 아이콘

        [Header("Sound Gauge (우측)")]
        [SerializeField] private int _soundGaugeWidth = 16;
        [SerializeField] private int _soundGaugeHeight = 160;
        [SerializeField] private int _soundGaugeOffsetX = 12; // 미니맵 오른쪽으로
        [SerializeField] private Color _soundLowColor = new Color(0.3f, 0.8f, 1f, 1f);
        [SerializeField] private Color _soundMidColor = new Color(1f, 0.8f, 0.2f, 1f);
        [SerializeField] private Color _soundHighColor = new Color(1f, 0.2f, 0.2f, 1f);
        [SerializeField] private Sprite _soundIcon; // 파동 아이콘

        [Header("Time/Weather (미니맵 위)")]
        [SerializeField] private int _timeWeatherHeight = 30;
        [SerializeField] private Sprite _sunIcon;
        [SerializeField] private Sprite _rainIcon;
        [SerializeField] private Sprite _nightIcon;

        // 런타임 데이터
        private Transform _playerTransform;
        private Rect _minimapRect;
        private Rect _tempGaugeRect;
        private Rect _soundGaugeRect;
        private Rect _timeWeatherRect;

        // GC: 캐싱된 GUIStyle
        private GUIStyle _cachedLabelStyle;
        private GUIStyle _cachedTempStyle;
        private GUIStyle _cachedSoundStyle;

        // 온도/소음 시스템 참조
        private TemperatureSystem _temperatureSystem;
        private SoundSystem _soundSystem;
        // 시간/날씨 시스템 참조
        private TimeWeatherSystem _timeWeatherSystem;

        // 현재 상태
        private float _currentTemperature = 0f; // -1(추위) ~ 0(보통) ~ 1(더위)
        private float _currentSoundLevel = 0f;  // 0~1
        private float _currentTimeOfDay = 0.5f; // 0~1 (0=자정, 0.5=정오)
        private WeatherType _currentWeather = WeatherType.Clear;

        private void Awake()
        {
        }

        private void Start()
        {
            // 플레이어 찾기
            var player = GameObject.FindWithTag("Player");
            if (player != null)
                _playerTransform = player.transform;

            // 시스템 참조
            _temperatureSystem = TemperatureSystem.Instance;
            _soundSystem = SoundSystem.Instance;
            _timeWeatherSystem = TimeWeatherSystem.Instance;

            // MM-Terrain (09-08): 지형 텍스처 주입 — BakeWorldSplat이 만든 통합 월드 스플랫을
            // 그대로 재사용 (추가 베이크 없음, 게임 지형과 100% 일치). 아직 준비 안 됐으면
            // LateStart/Update에서 지연 재시도한다.
            TryApplyMapTexture();

            UpdateRectPositions();
        }

        private void TryApplyMapTexture()
        {
            if (_mapTexture != null) return;
            var splat = TerrainSplatBaker.LastWorldSplat;
            if (splat == null) return;
            SetMapTexture(splat);
            // 미니맵 지름(px)을 월드 폭(m)으로 나눈 축척 — 월드 원점(0,0)이 미니맵 중심.
            SetMapScale(_minimapDiameter / TerrainSplatBaker.WORLD_SIZE);
            Debug.Log("[MinimapUI] 지형 텍스처 주입 완료: " + splat.name + " (scale=" + _mapScale + ")");
        }

        private void CacheStyles()
        {
            _cachedLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };

            _cachedTempStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };

            _cachedSoundStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
        }

        private void Update()
        {
            // MM-Terrain: 부팅 중 스플랫 생성 전이면 지연 재시도 (준비되면 1회 주입)
            TryApplyMapTexture();
            UpdateRectPositions();
            UpdateTemperature();
            UpdateSoundLevel();
            UpdateTimeWeather();
        }

        private void UpdateRectPositions()
        {
            int cx = Screen.width - _marginRight - _minimapDiameter;
            int cy = _marginTop;

            // 미니맵 원형 영역
            _minimapRect = new Rect(cx, cy, _minimapDiameter, _minimapDiameter);

            // 온도 게이지 (미니맵 왼쪽)
            int tempX = cx + _tempGaugeOffsetX - _tempGaugeWidth;
            int tempY = cy + (_minimapDiameter - _tempGaugeHeight) / 2;
            _tempGaugeRect = new Rect(tempX, tempY, _tempGaugeWidth, _tempGaugeHeight);

            // 소음 게이지 (미니맵 오른쪽)
            int soundX = cx + _minimapDiameter + _soundGaugeOffsetX;
            int soundY = cy + (_minimapDiameter - _soundGaugeHeight) / 2;
            _soundGaugeRect = new Rect(soundX, soundY, _soundGaugeWidth, _soundGaugeHeight);

            // 시간/날씨 (미니맵 위)
            int timeX = cx;
            int timeY = cy - _timeWeatherHeight - 4;
            _timeWeatherRect = new Rect(timeX, timeY, _minimapDiameter, _timeWeatherHeight);
        }

        private void UpdateTemperature()
        {
            if (_temperatureSystem != null)
            {
                _currentTemperature = _temperatureSystem.CurrentTemperature; // -1 ~ 1
            }
            else
            {
                // 기본값: 약간 추움
                _currentTemperature = -0.2f;
            }
        }

        private void UpdateSoundLevel()
        {
            if (_soundSystem != null)
            {
                _currentSoundLevel = _soundSystem.CurrentNoiseLevel; // 0 ~ 1
            }
            else if (_playerTransform != null)
            {
                // 플레이어 속도 기반 추정
                var rb = _playerTransform.GetComponent<Rigidbody>();
                if (rb != null)
                    _currentSoundLevel = Mathf.Clamp01(rb.linearVelocity.magnitude / 10f);
                else
                    _currentSoundLevel = 0.1f;
            }
        }

        private void UpdateTimeWeather()
        {
            if (_timeWeatherSystem != null)
            {
                _currentTimeOfDay = _timeWeatherSystem.TimeOfDay; // 0 ~ 1
                _currentWeather = _timeWeatherSystem.CurrentWeather;
            }
        }

        private void OnGUI()
        {
            // 지연 초기화
            if (_cachedLabelStyle == null)
                CacheStyles();

            UpdateRectPositions();

            // 휠 줌: 미니맵 위에서 스크롤 → 80/120/200m 3단 순환
            if (Event.current != null && Event.current.type == EventType.ScrollWheel
                && _minimapRect.Contains(Event.current.mousePosition))
            {
                _zoomIndex = (_zoomIndex + (Event.current.delta.y > 0f ? 1 : _zoomRadii.Length - 1)) % _zoomRadii.Length;
                _localRadius = _zoomRadii[_zoomIndex];
                Event.current.Use();
            }

            // 1. 시간/날씨 표시 (미니맵 위)
            DrawTimeWeather();

            // 2. 미니맵 배경 (원형)
            DrawMinimapBackground();

            // 3. 영지·퀘스트·랜드마크 마커 (지형 위)
            DrawMarkerOverlay();

            // 3. 플레이어 마커
            DrawPlayerMarker();

            // 4. 온도 게이지 (좌측)
            DrawTemperatureGauge();

            // 5. 소음 게이지 (우측)
            DrawSoundGauge();
        }

        private void DrawTimeWeather()
        {
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.Box(_timeWeatherRect, "");

            GUI.color = Color.white;
            
            // 시간 텍스트
            int hour = Mathf.FloorToInt(_currentTimeOfDay * 24);
            string timeText = $"{hour:D2}:00";
            
            // 날씨 아이콘 + 시간
            Rect iconRect = new Rect(_timeWeatherRect.x + 8, _timeWeatherRect.y + 4, 22, 22);
            Sprite weatherIcon = _currentWeather switch
            {
                WeatherType.Rain => _rainIcon,
                WeatherType.Storm => _rainIcon,
                WeatherType.Snow => _coldIcon,
                WeatherType.Night => _nightIcon,
                _ => _sunIcon
            };
            
            if (weatherIcon != null)
                GUI.DrawTexture(iconRect, weatherIcon.texture);
            else
                GUI.Label(iconRect, _currentWeather == WeatherType.Rain ? "🌧" : _currentWeather == WeatherType.Night ? "🌙" : "☀️", _cachedLabelStyle);

            Rect timeRect = new Rect(_timeWeatherRect.x + 36, _timeWeatherRect.y, _timeWeatherRect.width - 44, _timeWeatherRect.height);
            GUI.Label(timeRect, timeText, _cachedLabelStyle);
        }

        private void DrawMinimapBackground()
        {
            // 원형 배경
            GUI.color = new Color(0.05f, 0.05f, 0.05f, 0.85f);
            
            // 원형 마스크 효과를 위해 텍스처 사용 (GUI.DrawTexture with alpha)
            // 여기서는 간단히 Box로 근사 + 가장자리 페이드
            GUI.Box(_minimapRect, "");

            // 테두리
            GUI.color = new Color(1f, 1f, 1f, 0.3f);
            Rect borderRect = new Rect(_minimapRect.x - 2, _minimapRect.y - 2, _minimapRect.width + 4, _minimapRect.height + 4);
            GUI.Box(borderRect, "");

            // 맵 텍스처가 있으면 그리기 — 로컬뷰(플레이어 중심 크롭), 플레이어 없으면 전체맵 폴백
            if (_mapTexture != null)
            {
                GUI.color = Color.white;
                if (_playerTransform != null)
                {
                    // 로컬뷰 uvRect 크롭 — 텍스처 매핑 규약: u=0.5+x/W, v=0.5+z/W (BakeWorldSplat 동일)
                    Vector3 p = _playerTransform.position;
                    float span = Mathf.Clamp01((_localRadius * 2f) / TerrainSplatBaker.WORLD_SIZE);
                    float u = Mathf.Clamp01(0.5f + p.x / TerrainSplatBaker.WORLD_SIZE);
                    float v = Mathf.Clamp01(0.5f + p.z / TerrainSplatBaker.WORLD_SIZE);
                    float u0 = Mathf.Clamp(u - span * 0.5f, 0f, 1f - span);
                    float v0 = Mathf.Clamp(v - span * 0.5f, 0f, 1f - span);
                    GUI.DrawTextureWithTexCoords(_minimapRect, _mapTexture, new Rect(u0, v0, span, span), true);
                }
                else
                {
                    GUI.DrawTexture(_minimapRect, _mapTexture, ScaleMode.ScaleToFit, true);
                }
            }

            GUI.color = Color.white;
        }

        /// <summary>
        /// MM-Terrain M3 → 로컬뷰 개편(2026-09-09): 영지(검은 점)·활성 퀘스트(퀘스트색 점) 마커를
        /// 플레이어 중심 로컬 좌표계로 겹친다. 반경 밖 마커는 스킵. 방위: +x=우, +z=상(북).
        /// </summary>
        private void DrawMarkerOverlay()
        {
            if (_mapTexture == null || _playerTransform == null) return;   // 지형/플레이어 준비 전에는 스킵

            float radius = _minimapDiameter * 0.5f;
            float centerX = _minimapRect.x + radius;
            float centerY = _minimapRect.y + radius;
            float pxPerWorld = _minimapDiameter / (_localRadius * 2f);
            Vector3 pp = _playerTransform.position;

            // ── ① 영지 마커 (모두, 작은 검은 점) ──
            var db = TerritoryDatabase.Instance;
            if (db != null)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.85f);
                int mSize = 4;
                foreach (var def in db.GetAllDefinitions())
                {
                    Vector3 wp = GetTerritoryWorldPosition(def);
                    if (wp == Vector3.zero) continue;
                    if (!TryLocalScreenPos(wp, pp, pxPerWorld, radius - mSize, centerX, centerY, mSize, out var r)) continue;
                    GUI.Box(r, "");
                }
                GUI.color = Color.white;
            }

            // ── ② 활성 퀘스트 마커 (QuestMarkerSystem — 퀘스트색 점, 더 큼) ──
            if (QuestMarkerSystem.Instance != null)
            {
                int qSize = 7;
                foreach (var qm in QuestMarkerSystem.Instance.GetActiveQuestMarkers())
                {
                    if (!TryLocalScreenPos(qm.worldPos, pp, pxPerWorld, radius - 2f, centerX, centerY, qSize, out var r)) continue;
                    GUI.color = qm.markerColor;
                    GUI.Box(r, "");
                }
                GUI.color = Color.white;
            }
        }

        /// <summary>월드좌표 → 로컬뷰 화면 Rect. 반경 밖이면 false. (+z=북=화면 위, GUI y 역방향 보정)</summary>
        private bool TryLocalScreenPos(Vector3 wp, Vector3 pp, float pxPerWorld, float maxDist, float cx, float cy, int size, out Rect rect)
        {
            float dx = (wp.x - pp.x) * pxPerWorld;
            float dy = -(wp.z - pp.z) * pxPerWorld;
            float distSq = dx * dx + dy * dy;
            if (distSq > maxDist * maxDist) { rect = default; return false; }
            rect = new Rect(cx + dx - size * 0.5f, cy + dy - size * 0.5f, size, size);
            return true;
        }

        /// <summary>영지 정의 → 미니맵용 월드 위치 (QuestMarkerSystem과 동일 규칙).</summary>
        private Vector3 GetTerritoryWorldPosition(TerritoryDefinition def)
        {
            Vector3 dir;
            switch (def.nation)
            {
                case NationType.North: dir = new Vector3(0f, 0f, 1f); break;
                case NationType.East:  dir = new Vector3(1f, 0f, 0f); break;
                case NationType.South: dir = new Vector3(0f, 0f, -1f); break;
                case NationType.West:  dir = new Vector3(-1f, 0f, 0f); break;
                default: return Vector3.zero;   // Empire(중앙)은 플레이어 마커와 겹침 — 스킵
            }
            float distance = def.difficulty switch
            {
                TerritoryDifficulty.Ring1 => 15f,
                TerritoryDifficulty.Ring2 => 30f,
                TerritoryDifficulty.Ring3 => 45f,
                TerritoryDifficulty.Ring4 => 60f,
                _ => 20f,
            };
            float spreadAngle = (def.id.index % 5) * 18f;
            Vector3 spread = Quaternion.Euler(0f, spreadAngle, 0f) * dir;
            if (spread.sqrMagnitude < 0.01f) spread = dir;
            return spread.normalized * distance;
        }

        private void DrawPlayerMarker()
        {
            if (_playerTransform == null) return;

            // 로컬뷰(2026-09-09): 플레이어는 항상 미니맵 중앙, 이동방향 화살표 표시
            float radius = _minimapDiameter * 0.5f;
            float centerX = _minimapRect.x + radius;
            float centerY = _minimapRect.y + radius;

            var markerRect = new Rect(
                centerX - _playerMarkerSize * 0.5f,
                centerY - _playerMarkerSize * 0.5f,
                _playerMarkerSize, _playerMarkerSize
            );

            // 플레이어 방향 표시 (화살표)
            GUI.color = _playerMarkerColor;
            GUI.Box(markerRect, "");

            // 방향 화살표 — forward 기준, +z=북=화면 위 (GUI y 역방향 보정)
            Vector3 fwd = _playerTransform.forward;
            float guiAngle = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg; // 0°=북(위), 시계방향+
            float arrowLength = _playerMarkerSize * 1.1f;
            Vector2 from = new Vector2(centerX, centerY);
            Vector2 arrowEnd = from + new Vector2(Mathf.Sin(guiAngle * Mathf.Deg2Rad), -Mathf.Cos(guiAngle * Mathf.Deg2Rad)) * arrowLength;
            DrawLine(from, arrowEnd, _playerMarkerColor, 3f);

            GUI.color = Color.white;
        }

        private void DrawTemperatureGauge()
        {
            // 배경
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.Box(_tempGaugeRect, "");

            // 게이지 채우기 (중앙 0 기준, 위로 더위, 아래로 추위)
            float gaugeCenterY = _tempGaugeRect.y + _tempGaugeRect.height * 0.5f;
            float fillHeight = (_tempGaugeRect.height * 0.5f - 2) * Mathf.Abs(_currentTemperature);
            
            Rect fillRect;
            Color fillColor;

            if (_currentTemperature < -0.1f) // 추위
            {
                fillColor = Color.Lerp(_tempNormalColor, _tempColdColor, Mathf.Abs(_currentTemperature));
                fillRect = new Rect(
                    _tempGaugeRect.x + 1,
                    gaugeCenterY - fillHeight,
                    _tempGaugeRect.width - 2,
                    fillHeight
                );
            }
            else if (_currentTemperature > 0.1f) // 더위
            {
                fillColor = Color.Lerp(_tempNormalColor, _tempHotColor, _currentTemperature);
                fillRect = new Rect(
                    _tempGaugeRect.x + 1,
                    gaugeCenterY,
                    _tempGaugeRect.width - 2,
                    fillHeight
                );
            }
            else // 보통
            {
                fillColor = _tempNormalColor;
                fillRect = new Rect(
                    _tempGaugeRect.x + 1,
                    gaugeCenterY - 2,
                    _tempGaugeRect.width - 2,
                    4
                );
            }

            GUI.color = fillColor;
            GUI.Box(fillRect, "");

            // 중앙선 (보통 온도 표시)
            GUI.color = new Color(1f, 1f, 1f, 0.3f);
            Rect centerLine = new Rect(_tempGaugeRect.x, gaugeCenterY - 1, _tempGaugeRect.width, 2);
            GUI.Box(centerLine, "");

            // 아이콘 (위: 더위, 아래: 추위)
            if (_currentTemperature > 0.3f && _hotIcon != null)
            {
                Rect iconRect = new Rect(_tempGaugeRect.x - 2, _tempGaugeRect.y - 24, 20, 20);
                GUI.color = Color.white;
                GUI.DrawTexture(iconRect, _hotIcon.texture);
            }
            else if (_currentTemperature < -0.3f && _coldIcon != null)
            {
                Rect iconRect = new Rect(_tempGaugeRect.x - 2, _tempGaugeRect.yMax + 4, 20, 20);
                GUI.color = Color.white;
                GUI.DrawTexture(iconRect, _coldIcon.texture);
            }

            // 수치 텍스트
            GUI.color = Color.white;
            string tempText = _currentTemperature > 0 ? $"+{_currentTemperature * 50:F0}°" : $"{_currentTemperature * 50:F0}°";
            Rect labelRect = new Rect(_tempGaugeRect.x - 30, _tempGaugeRect.yMax + 4, 60, 20);
            GUI.Label(labelRect, tempText, _cachedTempStyle);

            GUI.color = Color.white;
        }

        private void DrawSoundGauge()
        {
            // 배경
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.Box(_soundGaugeRect, "");

            // 게이지 채우기 (아래에서 위로)
            float fillHeight = (_soundGaugeRect.height - 4) * _currentSoundLevel;
            Rect fillRect = new Rect(
                _soundGaugeRect.x + 1,
                _soundGaugeRect.yMax - fillHeight - 1,
                _soundGaugeRect.width - 2,
                fillHeight
            );

            Color fillColor;
            if (_currentSoundLevel < 0.3f)
                fillColor = _soundLowColor;
            else if (_currentSoundLevel < 0.7f)
                fillColor = Color.Lerp(_soundLowColor, _soundMidColor, (_currentSoundLevel - 0.3f) / 0.4f);
            else
                fillColor = Color.Lerp(_soundMidColor, _soundHighColor, (_currentSoundLevel - 0.7f) / 0.3f);

            GUI.color = fillColor;
            GUI.Box(fillRect, "");

            // 아이콘 (아래)
            if (_soundIcon != null)
            {
                Rect iconRect = new Rect(_soundGaugeRect.x - 2, _soundGaugeRect.yMax + 4, 20, 20);
                GUI.color = Color.white;
                GUI.DrawTexture(iconRect, _soundIcon.texture);
            }

            // 파동 애니메이션 (소음 레벨에 따라)
            if (_currentSoundLevel > 0.5f)
            {
                float pulse = Mathf.Sin(Time.time * 8f) * 0.2f + 0.8f;
                GUI.color = new Color(fillColor.r, fillColor.g, fillColor.b, pulse * 0.5f);
                Rect pulseRect = new Rect(_soundGaugeRect.x - 4, fillRect.y - 4, _soundGaugeRect.width + 8, fillHeight + 8);
                GUI.Box(pulseRect, "");
            }

            GUI.color = Color.white;
        }

        private Vector2 WorldToMinimapLocal(Vector3 worldPos)
        {
            // 미니맵 중심 = (0, 0, 0) 또는 맵 중심으로 가정
            // 실제로는 맵 경계에 따라 정규화 필요
            return new Vector2(worldPos.x * _mapScale, worldPos.z * _mapScale);
        }

        private void DrawLine(Vector2 from, Vector2 to, Color color, float width)
        {
            // GUI로 선 그리기: 얇은 Box로 근사
            Vector2 dir = (to - from).normalized;
            float len = Vector2.Distance(from, to);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            
            GUI.color = color;
            GUIUtility.RotateAroundPivot(angle, from);
            GUI.Box(new Rect(from.x, from.y - width * 0.5f, len, width), "");
            GUI.matrix = Matrix4x4.identity;
            GUI.color = Color.white;
        }

        /// <summary>
        /// 외부에서 맵 텍스처 설정 (예: 런타임 렌더링)
        /// </summary>
        public void SetMapTexture(Texture2D texture)
        {
            _mapTexture = texture;
        }

        /// <summary>
        /// 미니맵 스케일 설정
        /// </summary>
        public void SetMapScale(float scale)
        {
            _mapScale = scale;
        }
    }
}