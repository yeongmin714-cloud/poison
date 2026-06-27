using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;
using ProjectName.UI.Themes;
#pragma warning disable 0414

namespace ProjectName.UI
{
    /// <summary>
    /// [5.3.1] World Space HUD — 병사/몬스터 머리 위 오버레이.
    /// IMGUI 기반, Camera.WorldToScreenPoint로 화면 좌표 변환.
    /// IWorldSpaceHUD 인터페이스를 통해 표시 대상 결정.
    /// </summary>
    public class GuardWorldSpaceHUD : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private float _displayRange = 15f;
        [SerializeField] private float _hudWidth = 160f;
        [SerializeField] private float _hudHeight = 70f;
        [SerializeField] private KeyCode _toggleKey = KeyCode.F6;

        // 캐시된 오브젝트 참조
        private Transform _playerTransform;
        private Camera _mainCamera;

        // 표시 대상 (매 프레임 갱신)
        private readonly List<IWorldSpaceHUD> _activeHUDs = new List<IWorldSpaceHUD>();

        // 스타일 (lazy init)
        private GUIStyle _styleName;
        private GUIStyle _styleLevel;
        private GUIStyle _styleLabel;
        private GUIStyle _styleValue;
        private UIDesignTheme _theme;

        // HUD 표시 온/오프
        private bool _hudEnabled = true;

        // 재사용 가능한 단색 텍스처 캐시 (GC 할당 방지)
        private static Texture2D _bgTexture;
        private static Texture2D _loyaltyFillTexture;
        private static Texture2D _addictionFillTexture;
        private static Color _lastBgColor = Color.clear;
        private static Color _lastLoyaltyColor = Color.clear;
        private static Color _lastAddictionColor = Color.clear;

        // 갱신 주기 (매 프레임 FindObjects 대신 0.5초마다)
        private float _refreshTimer;
        private const float REFRESH_INTERVAL = 0.5f;

        // ===== 싱글톤 =====
        public static GuardWorldSpaceHUD Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _theme = Phase33_Themes.GuardHUDTheme();
            CacheReferences();
        }

        private void OnEnable()
        {
            // 재활성화 시 참조 갱신
            CacheReferences();
        }

        /// <summary>플레이어 및 카메라 참조 캐싱</summary>
        private void CacheReferences()
        {
            _mainCamera = Camera.main;
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
                _playerTransform = playerGo.transform;
        }

        private void Update()
        {
            // F6 토글
            if (Input.GetKeyDown(_toggleKey))
            {
                _hudEnabled = !_hudEnabled;
                Debug.Log($"[GuardWorldSpaceHUD] HUD 표시: {(_hudEnabled ? "ON" : "OFF")}");
            }

            if (!_hudEnabled) return;

            // 주기적으로만 HUD 목록 갱신 (매 프레임 FindObjects 방지)
            _refreshTimer += Time.deltaTime;
            if (_refreshTimer >= REFRESH_INTERVAL)
            {
                _refreshTimer = 0f;
                RefreshActiveHUDs();

                // 주기적으로 캐시 참조도 갱신 (씬 전환 대비)
                if (_playerTransform == null || _mainCamera == null)
                    CacheReferences();
            }
        }

        private void OnGUI()
        {
            if (!_hudEnabled) return;

            EnsureStyles();

            // 캐시된 참조 사용
            var mainCam = _mainCamera;
            if (mainCam == null) return;

            var playerTr = _playerTransform;
            if (playerTr == null) return;

            Vector3 playerPos = playerTr.position;

            foreach (var hud in _activeHUDs)
            {
                if (hud == null || !hud.ShouldShowHUD) continue;

                float dist = Vector3.Distance(hud.WorldPosition, playerPos);
                if (dist > _displayRange) continue;

                // World → Screen 좌표 변환
                Vector3 screenPos = mainCam.WorldToScreenPoint(hud.WorldPosition);

                // 화면 뒤쪽이거나 화면 밖이면 스킵
                if (screenPos.z < 0) continue;
                if (screenPos.x < -_hudWidth || screenPos.x > Screen.width + _hudWidth) continue;
                if (screenPos.y < -_hudHeight || screenPos.y > Screen.height + _hudHeight) continue;

                // 거리에 따라 투명도/크기 조절 (선택사항)
                float alpha = Mathf.Clamp01(1f - (dist / _displayRange));

                // 화면 좌표 (IMGUI는 Y축 반전)
                float hudX = screenPos.x - _hudWidth / 2f;
                float hudY = Screen.height - screenPos.y - _hudHeight / 2f;

                DrawHUD(hudX, hudY, hud, alpha);
            }
        }

        private void DrawHUD(float x, float y, IWorldSpaceHUD hud, float alpha)
        {
            Color originalColor = GUI.color;
            GUI.color = new Color(1, 1, 1, alpha);

            // 배경 박스
            GUI.Box(new Rect(x, y, _hudWidth, _hudHeight), "");

            // 이름 + 레벨
            GUI.Label(new Rect(x + 5, y + 4, _hudWidth - 10, 18),
                $"{hud.HUDName} Lv.{hud.HUDLevel}", _styleName);

            // 호감도 (하트 프로그레스바)
            float loyaltyNorm = Mathf.Clamp01((hud.HUDLoyalty + 100f) / 200f); // -100~100 → 0~1
            GUI.Label(new Rect(x + 5, y + 24, 20, 16), "♥", _styleLabel);
            float barX = x + 24;
            float barY = y + 25;
            float barW = _hudWidth - 32;
            float barH = 14;

            // 호감도에 따른 채움 색상
            Color loyaltyColor = hud.HUDLoyalty >= 0
                ? Color.Lerp(Color.gray, Color.blue, loyaltyNorm)
                : Color.Lerp(Color.red, Color.gray, Mathf.Clamp01((hud.HUDLoyalty + 100f) / 100f));

            // 캐시된 텍스처 사용 (매 프레임 new Texture2D 방지)
            GUI.DrawTexture(new Rect(barX, barY, barW, barH), GetCachedBgTexture(alpha));
            GUI.DrawTexture(new Rect(barX, barY, barW * loyaltyNorm, barH), GetCachedTex(ref _loyaltyFillTexture, ref _lastLoyaltyColor, loyaltyColor));
            // 텍스트
            GUI.Label(new Rect(barX + 2, barY + 1, barW - 4, barH - 2),
                $"{(int)hud.HUDLoyalty}", _styleValue);

            // 중독도 (독약 아이콘 + 게이지)
            float addictionNorm = Mathf.Clamp01(hud.HUDAddiction / 100f);
            GUI.Label(new Rect(x + 5, y + 42, 20, 16), "☠", _styleLabel);
            float addBarX = x + 24;
            float addBarY = y + 43;
            float addBarW = _hudWidth - 32;
            float addBarH = 14;

            Color addictionColor = Color.Lerp(Color.green, Color.magenta, addictionNorm);

            GUI.DrawTexture(new Rect(addBarX, addBarY, addBarW, addBarH), GetCachedBgTexture(alpha));
            GUI.DrawTexture(new Rect(addBarX, addBarY, addBarW * addictionNorm, addBarH), GetCachedTex(ref _addictionFillTexture, ref _lastAddictionColor, addictionColor));
            GUI.Label(new Rect(addBarX + 2, addBarY + 1, addBarW - 4, addBarH - 2),
                $"{(int)hud.HUDAddiction}%", _styleValue);

            GUI.color = originalColor;
        }

        // ===== 내부 =====

        private void RefreshActiveHUDs()
        {
            _activeHUDs.Clear();

            // 씬의 모든 IWorldSpaceHUD 찾기 (주로 GuardPlaceholder)
            var guards = Object.FindObjectsOfType<GuardPlaceholder>();
            foreach (var guard in guards)
            {
                if (guard is IWorldSpaceHUD hud)
                {
                    _activeHUDs.Add(hud);
                }
            }

            // TODO: 몬스터 IWorldSpaceHUD 추가 (확장 포인트)
        }

        private void EnsureStyles()
        {
            if (_styleName != null) return;
            _styleName = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleLeft
            };
            _styleLevel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = Color.yellow },
                alignment = TextAnchor.MiddleRight
            };
            _styleLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleLeft
            };
            _styleValue = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleLeft
            };
        }

        /// <summary>배경용 어두운 텍스처 (알파만 다름, 캐시됨)</summary>
        private static Texture2D GetCachedBgTexture(float alpha)
        {
            Color c = new Color(0.3f, 0.3f, 0.3f, alpha);
            if (_bgTexture == null || _lastBgColor != c)
            {
                _bgTexture = MakeTexStatic(1, 1, c);
                _lastBgColor = c;
            }
            return _bgTexture;
        }

        /// <summary>색상별 텍스처 캐시 (매 프레임 new Texture2D 방지)</summary>
        private static Texture2D GetCachedTex(ref Texture2D cache, ref Color lastColor, Color newColor)
        {
            if (cache == null || lastColor != newColor)
            {
                Object.DestroyImmediate(cache);
                cache = MakeTexStatic(1, 1, newColor);
                lastColor = newColor;
            }
            return cache;
        }

        private static Texture2D MakeTexStatic(int w, int h, Color c)
        {
            var tex = new Texture2D(w, h);
            for (int i = 0; i < w; i++)
                for (int j = 0; j < h; j++)
                    tex.SetPixel(i, j, c);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave; // DontDestroy와 GC 마킹
            return tex;
        }

        // ===== 퍼블릭 API =====

        /// <summary>HUD 표시 강제 갱신</summary>
        public void Refresh() => RefreshActiveHUDs();

        /// <summary>HUD 표시 범위 설정</summary>
        public float DisplayRange { get => _displayRange; set => _displayRange = value; }

        /// <summary>HUD 활성화/비활성화</summary>
        public bool HUDEnabled { get => _hudEnabled; set => _hudEnabled = value; }
    }
}