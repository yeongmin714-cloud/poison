using System.Collections.Generic;
using System.Text;
using ProjectName.Systems;
using UnityEngine;
using ProjectName.UI.Themes;

namespace ProjectName.UI
{
    /// <summary>
    /// 약초 리스폰 게이지 UI (IMGUI 기반).
    /// 싱글톤으로 동작하며, 모든 약초 위에 리스폰 상태를 시각화.
    /// 
    /// - 리스폰 중: "[재생성 중 X.X초]" 텍스트 + 프로그레스바 (녹색→노랑→빨강)
    /// - 채집 가능: "[E] 채집" 텍스트 (초록색)
    /// - 카메라 거리 30m 이상이면 게이지 숨김 (far culling)
    /// </summary>
    public class HerbRespawnUI : MonoBehaviour
    {
        private static HerbRespawnUI _instance;
        public static HerbRespawnUI Instance => _instance;

        [Header("설정")]
        [SerializeField] private float _maxDisplayDistance = 30f;
        [SerializeField] private float _gaugeWidth = 120f;
        [SerializeField] private float _gaugeHeight = 12f;
        [SerializeField] private float _textOffsetY = 40f;   // 월드 위쪽 오프셋 (게이지)
        [SerializeField] private float _labelOffsetY = 24f;  // 텍스트 오프셋

        // 캐싱
        private Camera _mainCamera;
        private HerbPickup[] _herbCache;
        private List<HerbPickup> _activeHerbs = new List<HerbPickup>(64);

        private UIDesignTheme _theme;
        // GUI 스타일
        private GUIStyle _labelStyle;
        private GUIStyle _timerStyle;
        private GUIStyle _collectibleStyle;

        private static readonly Color ColorGreen  = new Color(0.2f, 0.9f, 0.2f, 1f);
        private static readonly Color ColorYellow = new Color(0.9f, 0.9f, 0.2f, 1f);
        private static readonly Color ColorRed    = new Color(0.9f, 0.2f, 0.2f, 1f);
        private static readonly Color ColorBg     = new Color(0f, 0f, 0f, 0.5f);
        private static readonly Color ColorGreenText = new Color(0.3f, 1f, 0.3f, 1f);

        // GC 방지: 재사용 StringBuilder + 화이트 텍스처
        private readonly StringBuilder _sb = new StringBuilder(32);
        private Texture2D _whiteTex;
        private GUIStyle _bgStyle;
        private float _herbSearchTimer;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            _theme = Phase33_Themes.HerbRespawnTheme();
        }

        private void Start()
        {
            _mainCamera = Camera.main;
            InitializeStyles();
            InitializeWhiteTexture();
        }

        private void OnDestroy()
        {
            if (_whiteTex != null)
            {
                Destroy(_whiteTex);
                _whiteTex = null;
            }
            if (_bgStyle != null && _bgStyle.normal.background != null)
            {
                Destroy(_bgStyle.normal.background);
                _bgStyle.normal.background = null;
            }
        }

        private void InitializeWhiteTexture()
        {
            _whiteTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _whiteTex.SetPixel(0, 0, Color.white);
            _whiteTex.Apply();
        }

        private void InitializeStyles()
        {
            _labelStyle = new GUIStyle
            {
                alignment = TextAnchor.LowerCenter,
                fontSize = 192,
                fontStyle = FontStyle.Bold
            };
            _labelStyle.normal.textColor = Color.white;

            _timerStyle = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 176,
                fontStyle = FontStyle.Bold
            };
            _timerStyle.normal.textColor = Color.white;

            _collectibleStyle = new GUIStyle
            {
                alignment = TextAnchor.LowerCenter,
                fontSize = 208,
                fontStyle = FontStyle.Bold
            };
            _collectibleStyle.normal.textColor = ColorGreenText;
        }

        private void Update()
        {
            if (_mainCamera == null)
                _mainCamera = Camera.main;
            if (_mainCamera == null) return;

            // FindObjectsByType 성능 최적화: 0.5초 간격으로 갱신
            _herbSearchTimer -= Time.deltaTime;
            if (_herbSearchTimer <= 0f)
            {
                _herbCache = GameObject.FindObjectsByType<HerbPickup>();
                _herbSearchTimer = 0.5f;
            }
        }

        private void OnGUI()
        {
            if (_mainCamera == null) return;
            if (_herbCache == null || _herbCache.Length == 0) return;

            Vector3 cameraPos = _mainCamera.transform.position;

            foreach (HerbPickup herb in _herbCache)
            {
                if (herb == null) continue;

                // 거리 컬링
                float distance = Vector3.Distance(herb.transform.position, cameraPos);
                if (distance > _maxDisplayDistance) continue;

                // 화면 내에 있는지 확인 (뒤에 있거나 화면 밖이면 스킵)
                Vector3 screenPoint = _mainCamera.WorldToScreenPoint(herb.transform.position);
                if (screenPoint.z < 0f) continue; // 카메라 뒤쪽

                // GUI 좌표계 변환
                float guiX = screenPoint.x;
                float guiY = Screen.height - screenPoint.y;

                // 화면 밖이면 스킵 (마진 포함)
                if (guiX < -50f || guiX > Screen.width + 50f) continue;
                if (guiY < -50f || guiY > Screen.height + 50f) continue;

                float progress = herb.RespawnProgress;
                bool isHarvested = herb.IsHarvested;

                if (isHarvested)
                {
                    // --- 리스폰 중: 텍스트 + 프로그레스바 ---
                    float remaining = herb.RespawnTimeLeft;

                    // GC-safe: StringBuilder 재사용 (string interpolation 대비)
                    _sb.Clear();
                    _sb.Append("[재생성 중 ");
                    _sb.Append(remaining.ToString("F1"));
                    _sb.Append("초]");
                    string timerText = _sb.ToString();

                    // 텍스트
                    Rect labelRect = new Rect(guiX - _gaugeWidth / 2f, guiY - _textOffsetY, _gaugeWidth, 20f);
                    GUI.Label(labelRect, timerText, _timerStyle);

                    // 프로그레스바 배경
                    Rect barBgRect = new Rect(guiX - _gaugeWidth / 2f, guiY - _textOffsetY + 20f, _gaugeWidth, _gaugeHeight);
                    GUI.Box(barBgRect, "", _bgStyle ?? CreateBackgroundStyle());

                    // 프로그레스바 채움 (진행률: 0 = 방금 수확, 1 = 곧 리스폰)
                    float fillWidth = _gaugeWidth * progress;
                    if (fillWidth > 0f)
                    {
                        Rect barFillRect = new Rect(guiX - _gaugeWidth / 2f, guiY - _textOffsetY + 20f, fillWidth, _gaugeHeight);

                        // 색상 그라데이션: progress 0→1 = green→yellow→red
                        Color barColor;
                        if (progress < 0.5f)
                        {
                            // green → yellow
                            float t = progress / 0.5f;
                            barColor = Color.Lerp(ColorGreen, ColorYellow, t);
                        }
                        else
                        {
                            // yellow → red
                            float t = (progress - 0.5f) / 0.5f;
                            barColor = Color.Lerp(ColorYellow, ColorRed, t);
                        }

                        // GUI.color로 색상 적용 → Texture2D 재할당/Apply 불필요
                        Color prevColor = GUI.color;
                        GUI.color = barColor;
                        GUI.DrawTexture(barFillRect, _whiteTex);
                        GUI.color = prevColor;
                    }
                }
                else
                {
                    // --- 채집 가능: "[E] 채집" 텍스트 ---
                    Rect labelRect = new Rect(guiX - _gaugeWidth / 2f, guiY - _labelOffsetY, _gaugeWidth, 20f);
                    GUI.Label(labelRect, "[E] 채집", _collectibleStyle);
                }
            }
        }

        // 백그라운드 스타일 캐싱
        private GUIStyle CreateBackgroundStyle()
        {
            _bgStyle = new GUIStyle();
            _bgStyle.normal.background = CreateBackgroundTexture();
            _bgStyle.border = new RectOffset(2, 2, 2, 2);
            return _bgStyle;
        }

        private Texture2D CreateBackgroundTexture()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, ColorBg);
            tex.Apply();
            return tex;
        }

        // --- 테스트/디버그용 ---
        public void SetCamera(Camera cam) => _mainCamera = cam;
        public HerbPickup[] GetHerbCache() => _herbCache;
        public float MaxDisplayDistance => _maxDisplayDistance;
    }
}