using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.UI
{
    /// <summary>
    /// Phase 4: 가스 분사기 HUD 오버레이 — GasSprayTimer 전용 UI.
    /// HUD.cs의 가스 분사기 타이머 섹션과 연동,
    /// 현재 삽입된 물약 정보 + 분사 가능 시간 표시.
    /// </summary>
    public class GasSprayUI : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private int _panelWidth = 280;
        [SerializeField] private int _panelHeight = 90;
        [SerializeField] private int _panelY = 78; // 상단 가스 타이머 아래
        [SerializeField] private int _barHeight = 16;
        [SerializeField] private int _fontSize = 14;

        [Header("Colors")]
        [SerializeField] private Color _panelBg = new Color(0.1f, 0.1f, 0.1f, 0.75f);
        [SerializeField] private Color _barBg = new Color(0.15f, 0.15f, 0.15f, 0.8f);
        [SerializeField] private Color _textColor = Color.white;
        [SerializeField] private Color _potionNameColor = new Color(0.8f, 0.7f, 0.3f, 1f);
        [SerializeField] private Color _emptyColor = new Color(0.4f, 0.4f, 0.4f, 1f);

        [Header("Potion Type Colors")]
        [SerializeField] private Color _poisonColor = new Color(1f, 0.2f, 0.2f);
        [SerializeField] private Color _mentalColor = new Color(0.7f, 0.2f, 1f);
        [SerializeField] private Color _healColor = new Color(0.2f, 0.8f, 0.2f);
        [SerializeField] private Color _buffColor = new Color(0.2f, 0.5f, 1f);

        // ── Cached state ────────────────────────────────────────────────
        private GasSprayerController _controller;
        private bool _stylesInitialized;
        private GUIStyle _styleLabel;
        private GUIStyle _stylePotionLabel;
        private GUIStyle _styleTimerLabel;
        private GUIStyle _styleEmptyLabel;

        // GC: 캐싱된 Rect
        private Rect _rectPanel;
        private Rect _rectBarBg;
        private Rect _rectBarFill;
        private Rect _rectPotionLabel;
        private Rect _rectTimerLabel;
        private Rect _rectTypeLabel;

        // 프레임별 상태 캐시
        private bool _showUI;
        private string _potionText;
        private string _timerText;
        private Color _potionColor;
        private float _barRatio;
        private bool _isReloading;
        private float _reloadRatio;

        private void Awake()
        {
            _controller = GasSprayerController.Instance;
        }

        /// <summary>
        /// HUD.OnGUI에서 호출 — 위치 계산 및 렌더링
        /// </summary>
        public void OnDrawGUI()
        {
            if (_controller == null) return;
            if (!_controller.IsEquipped) return;

            // 지연 스타일 초기화
            if (!_stylesInitialized)
            {
                InitStyles();
                _stylesInitialized = true;
            }

            UpdateState();
            if (!_showUI) return;

            CalculateRects();
            DrawPanel();
        }

        private void InitStyles()
        {
            _styleLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = _fontSize - 2,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = _textColor }
            };

            _stylePotionLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = _fontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = _potionNameColor }
            };

            _styleTimerLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = _fontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _textColor }
            };

            _styleEmptyLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = _fontSize,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = _emptyColor }
            };
        }

        private void UpdateState()
        {
            string potionId = _controller.LoadedPotionId;
            int potionCount = _controller.LoadedPotionCount;
            bool hasPotion = !string.IsNullOrEmpty(potionId) && potionCount > 0;

            if (!hasPotion)
            {
                _showUI = true; // 빈 상태도 표시
                _potionText = "⚠️ 물약 없음";
                _potionColor = _emptyColor;
                _timerText = _controller.IsReloading ? "🔄 재장전 중..." : "💨 준비됨";
                _barRatio = 0f;
                _isReloading = _controller.IsReloading;
                _reloadRatio = 0f;
                return;
            }

            _showUI = true;

            // 물약 속성 타입 확인
            PotionType type = GasSprayer.ClassifyPotion(potionId);
            _potionColor = GetTypeColor(type);

            string typeEmoji = GetTypeEmoji(type);
            string typeName = GetTypeName(type);
            _potionText = $"{typeEmoji} {potionId} x{potionCount} ({typeName})";

            // 타이머 정보
            if (_controller.IsReloading)
            {
                GasSprayerData data = GasSprayerManager.GetGradeData(_controller.CurrentGrade);
                float reloadDuration = GasSprayerManager.GetReloadTime(_controller.CurrentGrade);
                _isReloading = true;
                _reloadRatio = reloadDuration > 0f
                    ? Mathf.Clamp01(1f - (_controller.ReloadTimeRemaining / reloadDuration))
                    : 1f;
                _timerText = $"🔄 재장전... {_controller.ReloadTimeRemaining:F1}s";
                _barRatio = _reloadRatio;
            }
            else
            {
                _isReloading = false;
                GasSprayerData data = GasSprayerManager.GetGradeData(_controller.CurrentGrade);
                float remaining = Mathf.Max(0f, _controller.CurrentSprayTimeRemaining);
                float maxTime = data.maxSprayTime;

                if (data.isUnlimited || remaining >= float.MaxValue / 2)
                {
                    _timerText = "♾️ 무제한";
                    _barRatio = 1f;
                }
                else
                {
                    _barRatio = maxTime > 0f ? Mathf.Clamp01(remaining / maxTime) : 0f;
                    _timerText = $"💨 {remaining:F1}s / {maxTime:F0}s";
                }
            }
        }

        private void CalculateRects()
        {
            float panelX = (Screen.width - _panelWidth) / 2;

            _rectPanel = new Rect(panelX, _panelY, _panelWidth, _panelHeight);
            _rectBarBg = new Rect(panelX + 4, _panelY + _panelHeight - _barHeight - 4, _panelWidth - 8, _barHeight);
            _rectBarFill = new Rect(panelX + 5, _panelY + _panelHeight - _barHeight - 3, _panelWidth - 10, _barHeight - 2);
            _rectPotionLabel = new Rect(panelX + 6, _panelY + 6, _panelWidth - 12, 22);
            _rectTimerLabel = new Rect(panelX + 6, _panelY + 28, _panelWidth - 12, 20);
            _rectTypeLabel = new Rect(panelX + 6, _panelY + 48, _panelWidth - 12, 18);
        }

        private void DrawPanel()
        {
            // 패널 배경
            Color originalColor = GUI.color;
            GUI.color = _panelBg;
            GUI.Box(_rectPanel, "");

            // 테두리
            GUI.color = Color.white;
            GUI.Box(_rectPanel, "");

            // 물약 정보
            GUI.color = _potionColor;
            GUI.Label(_rectPotionLabel, _potionText, _stylePotionLabel);

            // 타이머 텍스트
            GUI.color = _textColor;
            GUI.Label(_rectTimerLabel, _timerText, _styleTimerLabel);

            // 프로그레스 바 배경
            GUI.color = _barBg;
            GUI.Box(_rectBarBg, "");

            // 프로그레스 바 채움
            if (_barRatio > 0f)
            {
                Color barColor;
                if (_isReloading)
                {
                    barColor = new Color(0.3f, 0.5f, 1f, 0.9f); // 파란색 (재장전)
                }
                else if (_barRatio > 0.5f)
                {
                    barColor = Color.Lerp(Color.yellow, Color.green, (_barRatio - 0.5f) * 2f);
                }
                else
                {
                    barColor = Color.Lerp(Color.red, Color.yellow, _barRatio * 2f);
                }

                GUI.color = barColor;
                _rectBarFill.width = (_panelWidth - 10) * _barRatio;
                GUI.Box(_rectBarFill, "");
            }

            // 타입 표시 (작은 텍스트)
            if (!string.IsNullOrEmpty(_controller.LoadedPotionId))
            {
                PotionType type = GasSprayer.ClassifyPotion(_controller.LoadedPotionId);
                string typeDesc = GetTypeDescription(type);
                GUI.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
                GUI.Label(_rectTypeLabel, typeDesc, _styleLabel);
            }

            GUI.color = originalColor;
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private Color GetTypeColor(PotionType type)
        {
            return type switch
            {
                PotionType.Poison => _poisonColor,
                PotionType.Mental => _mentalColor,
                PotionType.Heal   => _healColor,
                PotionType.Buff   => _buffColor,
                _                 => _emptyColor
            };
        }

        private static string GetTypeEmoji(PotionType type)
        {
            return type switch
            {
                PotionType.Poison => "☠️",
                PotionType.Mental => "🌀",
                PotionType.Heal   => "💚",
                PotionType.Buff   => "💪",
                _                 => "🧪"
            };
        }

        private static string GetTypeName(PotionType type)
        {
            return type switch
            {
                PotionType.Poison => "공격성(독)",
                PotionType.Mental => "정신성(마약)",
                PotionType.Heal   => "회복성(치료)",
                PotionType.Buff   => "물리성(강화)",
                _                 => "알 수 없음"
            };
        }

        private static string GetTypeDescription(PotionType type)
        {
            return type switch
            {
                PotionType.Poison => "🔴 붉은 안개 — 적 지속 데미지 5~15",
                PotionType.Mental => "🟣 보라색 안개 — 적 환각/혼란",
                PotionType.Heal   => "🟢 초록색 안개 — 아군 체력 회복",
                PotionType.Buff   => "🔵 파란색 안개 — 아군 버프",
                _                 => ""
            };
        }
    }
}