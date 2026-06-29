using UnityEngine;
using ProjectName.Core;
using System.Collections.Generic;

namespace ProjectName.UI
{
    /// <summary>
    /// 플레이어 체력바 HUD (IMGUI 기반)
    /// - 좌측 상단 HP 바 (녹색→노랑→빨강 그라데이션)
    /// - HP 수치 텍스트
    /// - 사망 시 "사망" 표시
    /// - 버프 아이콘 표시 (우측에 HP 바 옆)
    /// - 가스 분사기 타이머 (장착 시 분사 가능 시간 프로그레스바 + 숫자)
    /// </summary>
    public class HUD : MonoBehaviour
    {
        [Header("HP Bar")]
        [SerializeField] private int _barWidth = 700;
        [SerializeField] private int _barHeight = 70;
        [SerializeField] private int _barX = 40;
        private int _barY; // 동적 계산: 좌하단
        [SerializeField] private GUISkin _customSkin;

        [Header("Colors")]
        [SerializeField] private Color _highColor = Color.green;
        [SerializeField] private Color _midColor = Color.yellow;
        [SerializeField] private Color _lowColor = Color.red;

        [Header("Text")]
        [SerializeField] private int _fontSize = 48;
        [SerializeField] private Color _textColor = Color.white;

        [Header("Death Overlay")]
        [SerializeField] private Color _deathOverlayColor = new Color(0.5f, 0f, 0f, 0.4f);

        [Header("Buff Icons")]
        [SerializeField] private int _iconSize = 60;
        [SerializeField] private int _iconSpacing = 10;
        [SerializeField] private int _iconOffsetX = 760; // X offset from left (barX + barWidth + 10)
        private int _iconOffsetY; // 동적 계산: 좌하단 기준
        private static readonly Dictionary<string, Color> _buffColors = new Dictionary<string, Color>
        {
            { "AttackUp", Color.red },
            { "DefenseUp", Color.blue },
            { "SpeedUp", Color.cyan },
            { "AlchemyBoost", Color.magenta },
            { "CookingBoost", Color.yellow },
            { "CritUp", Color.white },
            { "HealOverTime", Color.green }
        };

        [Header("가스 분사기 타이머")]
        [SerializeField] private int _gasTimerWidth = 300;
        [SerializeField] private int _gasTimerHeight = 24;
        [SerializeField] private int _gasTimerY = 10; // 상단 고정

        // 캐싱
        private float _currentHP;
        private float _maxHP = 100f;
        private bool _isDead = false;
        private string _lastHpText; // GC: 이전 HP 텍스트 캐싱 (변경 시에만 재할당)
        private bool _hpTextDirty = true; // HP 텍스트 갱신 필요 플래그

        // GC: 캐싱된 GUIStyle — OnGUI에서 new GUIStyle() 방지
        private GUIStyle _cachedLabelStyle;
        private GUIStyle _cachedLegendStyle;
        private GUIStyle _cachedDeathStyle;
        private GUIStyle _cachedRespawnStyle;
        private GUIStyle _cachedBuffTimerStyle;
        private GUIStyle _cachedBuffIdStyle;
        private GUIStyle _cachedGasTimerStyle;

        // GC: 캐싱된 Rect — OnGUI에서 new Rect() 방지 (구조체지만 스택 할당 최적화)
        private Rect _rectBg;
        private Rect _rectHp;
        private Rect _rectDeathOverlay;
        private Rect _rectDeathLabel;
        private Rect _rectRespawnLabel;
        private Rect _rectLegendGreen;
        private Rect _rectLegendYellow;
        private Rect _rectLegendRed;

        // 버프 아이콘용 재사용 Rect
        private Rect _rectBuffBg;
        private Rect _rectBuffInner;

        // 가스 분사기 타이머용 Rect
        private Rect _rectGasBarBg;
        private Rect _rectGasBarFill;
        private Rect _rectGasLabel;

        // 가스 분사기 상태 캐시
        private bool _gasSprayerEquipped;
        private float _gasRemaining;
        private float _gasMax;
        private bool _gasUnlimited;
        private bool _gasReloading;
        private float _gasReloadRemaining;
        private float _gasReloadDuration;
        private string _gasCachedLabel;

        // Phase 4: GasSprayUI 연동
        [Header("GasSprayUI Integration")]
        [SerializeField] private GasSprayUI _gasSprayUI;

        private void Start()
        {
            // GC: Rect 캐싱 — 고정 위치 Rect는 미리 계산
            CacheStaticRects();

            // PlayerHealth 구독
            if (PlayerHealth.Instance != null)
            {
                PlayerHealth.Instance.OnHPChanged += OnHealthChanged;
                _currentHP = PlayerHealth.Instance.CurrentHP;
                _maxHP = PlayerHealth.Instance.MaxHP;
                _hpTextDirty = true; // 초기 텍스트 생성
            }
        }

        private void CacheStyles()
        {
            // 모든 GUIStyle을 미리 캐싱 (OnGUI에서 new GUIStyle() 호출 금지)
            _cachedLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = _fontSize,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };

            _cachedLegendStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24
            };

            _cachedDeathStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 96,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };

            _cachedRespawnStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 40,
                alignment = TextAnchor.MiddleCenter
            };

            _cachedBuffTimerStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter
            };

            _cachedBuffIdStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter
            };

            _cachedGasTimerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }

        private void CacheStaticRects()
        {
            _rectLegendGreen = new Rect(0, 0, 120, 40);
            _rectLegendYellow = new Rect(140, 0, 120, 40);
            _rectLegendRed = new Rect(280, 0, 120, 40);
        }

        private void UpdateStaticRectPositions()
        {
            // 바 위치 업데이트
            _barY = Screen.height - _barHeight - 60;
            _iconOffsetY = _barY;

            _rectBg = new Rect(_barX, _barY, _barWidth, _barHeight);
            _rectHp = new Rect(_barX + 1, _barY + 1, _barWidth - 2, _barHeight - 2);

            // 티어 범례 위치
            int legendY = _barY + _barHeight + 20;
            _rectLegendGreen.x = _barX;
            _rectLegendGreen.y = legendY;
            _rectLegendYellow.x = _barX + 140;
            _rectLegendYellow.y = legendY;
            _rectLegendRed.x = _barX + 280;
            _rectLegendRed.y = legendY;

            // 사망 오버레이 위치 (매 프레임 Screen 크기로 갱신)
            _rectDeathOverlay = new Rect(0, 0, Screen.width, Screen.height);
            _rectDeathLabel = new Rect(0, Screen.height * 0.35f, Screen.width, 120);
            _rectRespawnLabel = new Rect(0, Screen.height * 0.35f + 120, Screen.width, 60);

            // 가스 분사기 타이머 위치 (상단 중앙)
            float gasX = (Screen.width - _gasTimerWidth) / 2;
            _rectGasBarBg = new Rect(gasX, _gasTimerY, _gasTimerWidth, _gasTimerHeight);
            _rectGasBarFill = new Rect(gasX + 1, _gasTimerY + 1, _gasTimerWidth - 2, _gasTimerHeight - 2);
            _rectGasLabel = new Rect(gasX - 100, _gasTimerY, _gasTimerWidth + 200, _gasTimerHeight);
        }

        private void OnDestroy()
        {
            if (PlayerHealth.Instance != null)
            {
                PlayerHealth.Instance.OnHPChanged -= OnHealthChanged;
            }
        }

        private void OnHealthChanged(float current, float max)
        {
            _currentHP = current;
            _maxHP = max;
            _isDead = current <= 0;
            _hpTextDirty = true; // GC: HP 변경 시에만 텍스트 재생성
        }

        private void OnGUI()
        {
            // 지연 초기화: GUI.skin 및 GUIStyle 캐싱 — GUI.skin은 OnGUI 내에서만 접근 가능
            if (_cachedLabelStyle == null)
            {
                if (_customSkin != null)
                    GUI.skin = _customSkin;
                CacheStyles();
            }

            UpdateStaticRectPositions();

            DrawHPBar();
            DrawBuffIcons();
            DrawDeathOverlay();
            DrawGasSprayerTimer();

            // Phase 4: GasSprayUI 연동 — 물약 정보 + 타이머 패널
            if (_gasSprayUI != null)
            {
                _gasSprayUI.OnDrawGUI();
            }
        }

        private void UpdateGasSprayerState()
        {
            var controller = ProjectName.Systems.GasSprayerController.Instance;
            if (controller == null || !controller.IsEquipped)
            {
                _gasSprayerEquipped = false;
                return;
            }

            _gasSprayerEquipped = true;
            var data = ProjectName.Systems.GasSprayerManager.GetGradeData(controller.CurrentGrade);

            if (data.isUnlimited)
            {
                _gasUnlimited = true;
                _gasRemaining = 0f;
                _gasMax = 1f;
                _gasCachedLabel = "♾️ 무제한";
            }
            else
            {
                _gasUnlimited = false;
                _gasMax = data.maxSprayTime;
                _gasRemaining = controller.CurrentSprayTimeRemaining;
                _gasReloading = controller.IsReloading;
                _gasReloadRemaining = controller.ReloadTimeRemaining;
                _gasReloadDuration = ProjectName.Systems.GasSprayerManager.GetReloadTime(controller.CurrentGrade);

                if (_gasReloading)
                {
                    _gasCachedLabel = $"🔄 재장전... {_gasReloadRemaining:F1}s";
                }
                else
                {
                    _gasCachedLabel = $"💨 분사: {Mathf.Max(0, _gasRemaining):F1}s / {_gasMax:F0}s";
                }
            }
        }

        private void DrawGasSprayerTimer()
        {
            UpdateGasSprayerState();
            if (!_gasSprayerEquipped) return;

            // 배경
            GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            GUI.Box(_rectGasBarBg, "");

            if (!_gasUnlimited)
            {
                float ratio = _gasMax > 0 ? Mathf.Clamp01(_gasRemaining / _gasMax) : 0f;

                if (_gasReloading)
                {
                    float reloadRatio = _gasReloadDuration > 0 ? Mathf.Clamp01(1f - (_gasReloadRemaining / _gasReloadDuration)) : 0f;
                    // 재장전 프로그레스바 (파란색)
                    GUI.color = new Color(0.3f, 0.5f, 1f, 0.9f);
                    _rectGasBarFill.width = (_gasTimerWidth - 2) * reloadRatio;
                    GUI.Box(_rectGasBarFill, "");
                }
                else
                {
                    // 분사 가능 시간 프로그레스바 (초록→노랑→빨강)
                    Color barColor = ratio > 0.5f
                        ? Color.Lerp(Color.yellow, Color.green, (ratio - 0.5f) * 2f)
                        : Color.Lerp(Color.red, Color.yellow, ratio * 2f);
                    GUI.color = barColor;
                    _rectGasBarFill.width = (_gasTimerWidth - 2) * ratio;
                    GUI.Box(_rectGasBarFill, "");
                }
            }
            else
            {
                // 무제한 — 파란색 풀바
                GUI.color = new Color(0.3f, 0.6f, 1f, 0.9f);
                _rectGasBarFill.width = _gasTimerWidth - 2;
                GUI.Box(_rectGasBarFill, "");
            }

            // 테두리
            GUI.color = Color.white;
            GUI.Box(_rectGasBarBg, "");

            // 레이블 텍스트
            GUI.color = Color.white;
            GUI.Label(_rectGasLabel, _gasCachedLabel, _cachedGasTimerStyle);
            GUI.color = Color.white;
        }

        private void DrawHPBar()
        {
            float ratio = _maxHP > 0 ? Mathf.Clamp01(_currentHP / _maxHP) : 0f;

            // 배경 (어두운 회색)
            GUI.Box(_rectBg, "");

            // HP 바 (색상 그라데이션)
            Color barColor = ratio > 0.5f
                ? Color.Lerp(_midColor, _highColor, (ratio - 0.5f) * 2f)
                : Color.Lerp(_lowColor, _midColor, ratio * 2f);

            GUI.color = barColor;
            _rectHp.width = (_barWidth - 2) * ratio;
            GUI.Box(_rectHp, "");

            // 테두리
            GUI.color = Color.white;
            GUI.Box(_rectBg, "");

            // HP 텍스트 — Dirty Flag 패턴: 변경 시에만 문자열 할당
            if (_hpTextDirty)
            {
                _lastHpText = _isDead ? "💀 사망" : $"❤️ HP: {Mathf.Ceil(_currentHP)} / {_maxHP}";
                _hpTextDirty = false;
            }
            GUI.color = _textColor;
            GUI.Label(_rectBg, _lastHpText, _cachedLabelStyle);

            // 티어 정보 표시 (MonsterTier 범례)
            DrawTierLegend();
        }

        /// <summary>
        /// 몬스터 티어 색상 범례
        /// </summary>
        private void DrawTierLegend()
        {
            // 초반 🟢
            GUI.color = Color.green;
            GUI.Label(_rectLegendGreen, "🟢 초급", _cachedLegendStyle);

            // 중반 🟡
            GUI.color = Color.yellow;
            GUI.Label(_rectLegendYellow, "🟡 중급", _cachedLegendStyle);

            // 후반 🔴
            GUI.color = Color.red;
            GUI.Label(_rectLegendRed, "🔴 고급", _cachedLegendStyle);

            GUI.color = Color.white;
        }

        private void DrawBuffIcons()
        {
            if (BuffManager.Instance == null) return;

            var activeBuffs = BuffManager.Instance.GetActiveBuffs();
            if (activeBuffs == null) return;
            float x = _iconOffsetX;
            float y = _iconOffsetY;
            float size = _iconSize;
            float spacing = _iconSpacing;

            _cachedBuffTimerStyle.fontSize = Mathf.Max(9, (int)(size * 0.3f));
            _cachedBuffIdStyle.fontSize = Mathf.Max(9, (int)(size * 0.2f));

            foreach (var buff in activeBuffs)
            {
                if (buff.BuffId == null) continue;
                float remaining = buff.EndTime - Time.time;
                if (remaining <= 0f) continue;

                // Rect 재사용 (구조체, 스택 할당)
                _rectBuffBg = new Rect(x, y, size, size);
                _rectBuffInner = new Rect(x + 1, y + 1, size - 2, size - 2);

                Color buffColor;
                if (_buffColors.TryGetValue(buff.BuffId, out buffColor))
                {
                    // draw background
                    GUI.color = new Color(0f, 0f, 0f, 0.5f);
                    GUI.Box(_rectBuffBg, string.Empty);
                    // draw icon color
                    GUI.color = buffColor;
                    GUI.Box(_rectBuffInner, string.Empty);
                    // draw timer text
                    GUI.color = Color.white;
                    string timerText = remaining.ToString("0.0");
                    GUI.Label(_rectBuffBg, timerText, _cachedBuffTimerStyle);
                }
                else
                {
                    // fallback: draw gray icon with buffId text
                    GUI.color = new Color(0f, 0f, 0f, 0.5f);
                    GUI.Box(_rectBuffBg, string.Empty);
                    GUI.color = Color.gray;
                    GUI.Box(_rectBuffInner, string.Empty);
                    GUI.color = Color.white;
                    GUI.Label(_rectBuffBg, buff.BuffId, _cachedBuffIdStyle);
                }

                x += size + spacing;
            }
        }

        private void DrawDeathOverlay()
        {
            if (!_isDead) return;

            // 화면 전체 붉은 반투명 오버레이
            GUI.color = _deathOverlayColor;
            GUI.Box(_rectDeathOverlay, "");

            // "사망" 메시지
            GUI.color = Color.white;
            GUI.Label(_rectDeathLabel, "💀 사망", _cachedDeathStyle);

            // 리스폰 안내
            GUI.Label(_rectRespawnLabel, "리스폰 중...", _cachedRespawnStyle);

            GUI.color = Color.white;
        }
    }
}