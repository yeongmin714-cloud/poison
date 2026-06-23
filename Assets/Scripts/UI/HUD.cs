using UnityEngine;
using ProjectName.Core;
using System.Collections.Generic;
using ProjectName.Core.Data;

namespace ProjectName.UI
{
    /// <summary>
    /// 플레이어 체력바 HUD (IMGUI 기반)
    /// - 좌측 상단 HP 바 (녹색→노랑→빨강 그라데이션)
    /// - HP 수치 텍스트
    /// - 사망 시 "사망" 표시
    /// - 버프 아이콘 표시 (우측에 HP 바 옆)
    /// </summary>
    public class HUD : MonoBehaviour
    {
        [Header("HP Bar")]
        [SerializeField] private int _barWidth = 350;
        [SerializeField] private int _barHeight = 35;
        [SerializeField] private int _barX = 20;
        private int _barY; // 동적 계산: 좌하단
        [SerializeField] private GUISkin _customSkin;

        [Header("Colors")]
        [SerializeField] private Color _highColor = Color.green;
        [SerializeField] private Color _midColor = Color.yellow;
        [SerializeField] private Color _lowColor = Color.red;

        [Header("Text")]
        [SerializeField] private int _fontSize = 24;
        [SerializeField] private Color _textColor = Color.white;

        [Header("Death Overlay")]
        [SerializeField] private Color _deathOverlayColor = new Color(0.5f, 0f, 0f, 0.4f);

        [Header("Buff Icons")]
        [SerializeField] private int _iconSize = 30;
        [SerializeField] private int _iconSpacing = 5;
        [SerializeField] private int _iconOffsetX = 380; // X offset from left (barX + barWidth + 10)
        private int _iconOffsetY; // 동적 계산: 좌하단 기준
        private Dictionary<string, Color> _buffColors = new Dictionary<string, Color>
        {
            { "AttackUp", Color.red },
            { "DefenseUp", Color.blue },
            { "SpeedUp", Color.cyan },
            { "AlchemyBoost", Color.magenta },
            { "CookingBoost", Color.yellow },
            { "CritUp", Color.white },
            { "HealOverTime", Color.green }
        };

        // 캐싱
        private float _currentHP;
        private float _maxHP = 100f;
        private bool _isDead = false;

        private void Start()
        {
            // PlayerHealth 구독
            if (PlayerHealth.Instance != null)
            {
                PlayerHealth.Instance.OnHPChanged += OnHealthChanged;
                _currentHP = PlayerHealth.Instance.CurrentHP;
                _maxHP = PlayerHealth.Instance.MaxHP;
            }
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
        }

        private void OnGUI()
        {
            _barY = Screen.height - _barHeight - 30;
            _iconOffsetY = _barY;
            if (_customSkin != null)
                GUI.skin = _customSkin;

            DrawHPBar();
            DrawBuffIcons();
            DrawDeathOverlay();
        }

        private void DrawHPBar()
        {
            float ratio = _maxHP > 0 ? Mathf.Clamp01(_currentHP / _maxHP) : 0f;

            // 배경 (어두운 회색)
            Rect bgRect = new Rect(_barX, _barY, _barWidth, _barHeight);
            GUI.Box(bgRect, "");

            // HP 바 (색상 그라데이션)
            Color barColor = ratio > 0.5f
                ? Color.Lerp(_midColor, _highColor, (ratio - 0.5f) * 2f)
                : Color.Lerp(_lowColor, _midColor, ratio * 2f);

            GUI.color = barColor;
            Rect hpRect = new Rect(_barX + 1, _barY + 1, (_barWidth - 2) * ratio, _barHeight - 2);
            GUI.Box(hpRect, "");

            // 테두리
            GUI.color = Color.white;
            GUI.Box(bgRect, "");

            // HP 텍스트
            GUI.color = _textColor;
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = _fontSize;
            labelStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.fontStyle = FontStyle.Bold;

            string hpText = _isDead ? "💀 사망" : $"❤️ HP: {Mathf.Ceil(_currentHP)} / {_maxHP}";
            GUI.Label(bgRect, hpText, labelStyle);

            // 티어 정보 표시 (MonsterTier 범례)
            DrawTierLegend();
        }

        /// <summary>
        /// 몬스터 티어 색상 범례
        /// </summary>
        private void DrawTierLegend()
        {
            GUIStyle legendStyle = new GUIStyle(GUI.skin.label);
            legendStyle.fontSize = 12;

            int legendY = _barY + _barHeight + 10;
            int legendX = _barX;

            // 초반 🟢
            GUI.color = Color.green;
            GUI.Label(new Rect(legendX, legendY, 60, 20), "🟢 초급", legendStyle);

            // 중반 🟡
            GUI.color = Color.yellow;
            GUI.Label(new Rect(legendX + 70, legendY, 60, 20), "🟡 중급", legendStyle);

            // 후반 🔴
            GUI.color = Color.red;
            GUI.Label(new Rect(legendX + 140, legendY, 60, 20), "🔴 고급", legendStyle);

            GUI.color = Color.white;
        }

        private void DrawBuffIcons()
        {
            if (BuffManager.Instance == null) return;

            var activeBuffs = BuffManager.Instance.GetActiveBuffs();
            float x = _iconOffsetX;
            float y = _iconOffsetY;
            float size = _iconSize;
            float spacing = _iconSpacing;

            foreach (var buff in activeBuffs)
            {
                if (buff.BuffId == null) continue;
                float remaining = buff.EndTime - Time.time;
                if (remaining <= 0f) continue;

                Color buffColor;
                if (_buffColors.TryGetValue(buff.BuffId, out buffColor))
                {
                    // draw background
                    GUI.color = new Color(0f, 0f, 0f, 0.5f);
                    GUI.Box(new Rect(x, y, size, size), string.Empty);
                    // draw icon color
                    GUI.color = buffColor;
                    GUI.Box(new Rect(x + 1, y + 1, size - 2, size - 2), string.Empty);
                    // draw timer text
                    GUI.color = Color.white;
                    GUIStyle style = new GUIStyle(GUI.skin.label);
                    style.alignment = TextAnchor.MiddleCenter;
                    style.fontSize = Mathf.Max(9, (int)(size * 0.3f));
                    string timerText = remaining.ToString("0.0");
                    GUI.Label(new Rect(x, y, size, size), timerText, style);
                }
                else
                {
                    // fallback: draw gray icon with buffId text
                    GUI.color = new Color(0f, 0f, 0f, 0.5f);
                    GUI.Box(new Rect(x, y, size, size), string.Empty);
                    GUI.color = Color.gray;
                    GUI.Box(new Rect(x + 1, y + 1, size - 2, size - 2), string.Empty);
                    GUI.color = Color.white;
                    GUIStyle style = new GUIStyle(GUI.skin.label);
                    style.alignment = TextAnchor.MiddleCenter;
                    style.fontSize = Mathf.Max(9, (int)(size * 0.2f));
                    GUI.Label(new Rect(x, y, size, size), buff.BuffId, style);
                }

                x += size + spacing;
            }
        }

        private void DrawDeathOverlay()
        {
            if (!_isDead) return;

            // 화면 전체 붉은 반투명 오버레이
            GUI.color = _deathOverlayColor;
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");

            // "사망" 메시지
            GUI.color = Color.white;
            GUIStyle deathStyle = new GUIStyle(GUI.skin.label);
            deathStyle.fontSize = 48;
            deathStyle.alignment = TextAnchor.MiddleCenter;
            deathStyle.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(0, Screen.height * 0.35f, Screen.width, 60), "💀 사망", deathStyle);

            // 리스폰 안내
            GUIStyle respawnStyle = new GUIStyle(GUI.skin.label);
            respawnStyle.fontSize = 20;
            respawnStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(0, Screen.height * 0.35f + 60, Screen.width, 30), "리스폰 중...", respawnStyle);

            GUI.color = Color.white;
        }
    }
}