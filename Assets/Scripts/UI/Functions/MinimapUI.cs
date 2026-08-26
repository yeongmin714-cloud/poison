using UnityEngine;
using UnityEngine.UI;
using ProjectName.Core;
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
        [SerializeField] private float _mapScale = 0.001f; // 월드 단위 → 미니맵 픽셀
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

            UpdateRectPositions();
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

            // 1. 시간/날씨 표시 (미니맵 위)
            DrawTimeWeather();

            // 2. 미니맵 배경 (원형)
            DrawMinimapBackground();

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

            // 맵 텍스처가 있으면 그리기
            if (_mapTexture != null)
            {
                GUI.color = Color.white;
                // 원형 마스크 적용을 위해 DrawTextureWithTexCoords 사용 (고급)
                GUI.DrawTexture(_minimapRect, _mapTexture, ScaleMode.ScaleToFit, true);
            }

            GUI.color = Color.white;
        }

        private void DrawPlayerMarker()
        {
            if (_playerTransform == null) return;

            // 플레이어 월드 위치 → 미니맵 로컬 좌표 변환
            Vector3 playerPos = _playerTransform.position;
            Vector2 localPos = WorldToMinimapLocal(playerPos);

            // 미니맵 원형 내부에 클램핑
            float radius = _minimapDiameter * 0.5f;
            float maxDist = radius - _playerMarkerSize * 0.5f - 4;
            float dist = localPos.magnitude;
            if (dist > maxDist)
                localPos = localPos.normalized * maxDist;

            // 미니맵 중심 기준
            float centerX = _minimapRect.x + radius;
            float centerY = _minimapRect.y + radius;

            Rect markerRect = new Rect(
                centerX + localPos.x - _playerMarkerSize * 0.5f,
                centerY + localPos.y - _playerMarkerSize * 0.5f,
                _playerMarkerSize, _playerMarkerSize
            );

            // 플레이어 방향 표시 (화살표)
            GUI.color = _playerMarkerColor;
            GUI.Box(markerRect, "");

            // 방향 화살표
            float angle = _playerTransform.eulerAngles.y * Mathf.Deg2Rad;
            float arrowLength = _playerMarkerSize * 0.7f;
            Vector2 arrowEnd = new Vector2(
                centerX + localPos.x + Mathf.Sin(angle) * arrowLength,
                centerY + localPos.y + Mathf.Cos(angle) * arrowLength
            );
            
            // 방향 선 그리기 (GUI.DrawTexture로 선 대체)
            DrawLine(new Vector2(centerX + localPos.x, centerY + localPos.y), arrowEnd, _playerMarkerColor, 2f);

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