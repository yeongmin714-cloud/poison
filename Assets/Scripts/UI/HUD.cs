using UnityEngine;
using ProjectName.Core;
using System.Collections.Generic;

namespace ProjectName.UI
{
    /// <summary>
    /// 플레이어 HUD (IMGUI 기반) - BotW 스타일 하트 시스템
    /// - 좌측 상단 하트 시스템 (BotW 스타일)
    ///   * 하트 1개 = 20HP, MaxHP 100 = 5개 하트
    ///   * Full(빨강), Half(반만 빨강), Empty(회색) 상태
    ///   * 하트 아이콘: 절차 생성 하트 모양 마스크 텍스처 + GUI.color 틴트 (GUI.Box 사각형 근사 대체)
    ///   * 데미지 시 흔들림 애니메이션
    ///   * 임시 하트(노랑, 버프 초과 체력) 지원
    /// - 우상단 버프 아이콘 표시
    /// - 사망 시 "사망" 오버레이 표시
    /// - 가스 분사기 타이머 (상단 중앙)
    /// - Phase 34: 은신 상태 아이콘 + 발각 게이지 (하트 아래 배치)
    /// - 하단 중앙 경험치 바 (Lv 라벨 + EXP 수치, 플랫 스타일, 레벨업 펄스, MAX 처리)
    /// </summary>
    public class HUD : MonoBehaviour
    {
        [Header("Hearts (BotW Style)")]
            [SerializeField] private int _heartSize = 40;
            [SerializeField] private int _heartSpacing = 6;
            [SerializeField] private int _heartsPerRow = 10;
            [SerializeField] private int _heartStartX = 20;
            [SerializeField] private int _heartStartY = 20;
            [SerializeField] private float _hpPerHeart = 20f;
            [SerializeField] private GUISkin _customSkin;

            [Header("Heart Colors")]
            [SerializeField] private Color _heartFullColor = new Color(0.9f, 0.15f, 0.15f, 1f);   // 빨강
            [SerializeField] private Color _heartHalfColor = new Color(0.9f, 0.15f, 0.15f, 0.5f); // 반투명 빨강
            [SerializeField] private Color _heartEmptyColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);  // 회색
            [SerializeField] private Color _heartTempColor = new Color(1f, 0.85f, 0.1f, 1f);      // 노랑 (임시/버프 체력)

            [Header("Death Overlay")]
            [SerializeField] private Color _deathOverlayColor = new Color(0.5f, 0f, 0f, 0.4f);

            [Header("Buff Icons")]
            [SerializeField] private int _iconSize = 60;
            [SerializeField] private int _iconSpacing = 10;
            [SerializeField] private int _iconOffsetX = -200; // 우상단 기준 오프셋 (음수 = 우측에서 왼쪽으로)
            private int _iconOffsetY; // 동적 계산: 우상단
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
        private float _lastDamageTime = float.NegativeInfinity; // 데미지 애니메이션용
        private float _tempMaxHP = 0f; // 임시 최대 체력 (버프로 인한 초과 체력)

        // GC: 캐싱된 GUIStyle — OnGUI에서 new GUIStyle() 방지
        private GUIStyle _cachedLabelStyle;
        private GUIStyle _cachedDeathStyle;
        private GUIStyle _cachedRespawnStyle;
        private GUIStyle _cachedBuffTimerStyle;
        private GUIStyle _cachedBuffIdStyle;
        private GUIStyle _cachedGasTimerStyle;
        // Phase 34: 은신 스타일
        private GUIStyle _cachedStealthIconStyle;
        private GUIStyle _cachedDetectionLabelStyle;
        // 숫자 HP 표시 스타일 ("85 / 140")
        private GUIStyle _cachedHPTextStyle;

        // GC: 캐싱된 Rect — OnGUI에서 new Rect() 방지 (구조체지만 스택 할당 최적화)
        private Rect _rectDeathOverlay;
        private Rect _rectDeathLabel;
        private Rect _rectRespawnLabel;

        // 버프 아이콘용 재사용 Rect
        private Rect _rectBuffBg;
        private Rect _rectBuffInner;

        // HP 숫자 표시용 재사용 Rect
        private Rect _rectHPText;

        // 가스 분사기 타이머용 Rect
        private Rect _rectGasBarBg;
        private Rect _rectGasBarFill;
        private Rect _rectGasLabel;

        // 하트용 재사용 Rect (절차 생성 하트 마스크는 정사각 Rect 하나로 그린다)
        private Rect _rectHeart;

        // 캔버스 스케일 보정 (핫바 HotbarUI/cb45e8ec 선례와 동일 공식):
        // 1080p 게임뷰 = 1.0 → 기존 픽셀값 그대로(원래 크기). 저해상도 게임뷰에서도
        // 동일한 화면 비율을 유지하도록 하트/은신 HUD/HP 라벨 크기·간격을 비례 축소한다.
        // UpdateStaticRectPositions에서 프레임당 1회 산출해 전파한다.
        private float _canvasScale = 1f;

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

        // ===== Phase 34: 은신 HUD =====
        [Header("Stealth HUD (Phase 34)")]
        [SerializeField] private int _stealthIconSize = 48;
        [SerializeField] private int _stealthIconX = 40;
        [SerializeField] private int _detectionBarWidth = 200;
        [SerializeField] private int _detectionBarHeight = 12;
        [SerializeField] private Color _stealthActiveColor = new Color(0.3f, 0.6f, 1f, 1f);
        [SerializeField] private Color _stealthDangerColor = new Color(1f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color _detectionBarBgColor = new Color(0.1f, 0.1f, 0.1f, 0.7f);
        [SerializeField] private Color _detectionBarLowColor = Color.green;
        [SerializeField] private Color _detectionBarMidColor = Color.yellow;
        [SerializeField] private Color _detectionBarHighColor = Color.red;

        // 캐싱
        private Rect _rectStealthIcon;
        private Rect _rectDetectionBarBg;
        private Rect _rectDetectionBarFill;
        private Rect _rectStealthLabel;
        #pragma warning disable 0414
        private bool _stealthIconDirty = true;
#pragma warning restore 0414

        // ===== 경험치 바 HUD (플랫 스타일) =====
        [Header("EXP Bar (Flat)")]
        [SerializeField] private int _expBarWidth = 360;
        [SerializeField] private int _expBarHeight = 12;
        [SerializeField] private float _expBarHotbarGap = 14f; // 핫바 패널 상단과의 간격(px)
        [SerializeField] private Color _expBgColor = UIStyleManager.BgColor;      // 다크 네이비 반투명
        [SerializeField] private Color _expFillColor = UIStyleManager.AccentColor; // 스카이블루 채움
        [SerializeField] private Color _expBorderColor = UIStyleManager.BorderColor; // 회백 테두리

        // EXP 상태 캐시 (폴링 + 레벨업 엣지 감지)
        private int _prevExpLevel = -1; // -1 = 미초기화 (첫 프레임 오탐 방지)
        private float _lastLevelUpTime = float.NegativeInfinity;

        // GC: EXP 바 스타일/Rect 캐싱
        private GUIStyle _cachedExpLevelStyle;
        private GUIStyle _cachedExpValueStyle;
        private Rect _rectExpBarBorder;
        private Rect _rectExpBarBg;
        private Rect _rectExpBarFill;
        private Rect _rectExpLevelLabel;
        private Rect _rectExpValueText;

        // 파괴 시 구독 해제용
        private System.Action<bool> _stealthStateHandler;
        private System.Action<float> _detectionGaugeHandler;

        private void Start()
        {
            // GC: Rect 캐싱 — 고정 위치 Rect는 미리 계산
            CacheStaticRects();

            // 직렬화 값 방어: 칸당 HP 0/음수 → CeilToInt(무한대) 폭발, 행당 칸수 < 1 → 0 나눔.
            if (_hpPerHeart <= 0f) _hpPerHeart = 20f;
            if (_heartsPerRow < 1) _heartsPerRow = 10;

            // PlayerHealth 구독
            if (PlayerHealth.Instance != null)
            {
                PlayerHealth.Instance.OnHPChanged += OnHealthChanged;
                _currentHP = PlayerHealth.Instance.CurrentHP;
                _maxHP = PlayerHealth.Instance.MaxHP;
            }

            // Phase 34: StealthSystem 이벤트 구독
            SubscribeStealthEvents();
        }

        /// <summary>
        /// Phase 34: StealthSystem 이벤트 구독
        /// </summary>
        private void SubscribeStealthEvents()
        {
            var stealth = ProjectName.Systems.StealthSystem.Instance;
            if (stealth != null)
            {
                _stealthStateHandler = (stealthed) => { _stealthIconDirty = true; };
                _detectionGaugeHandler = (gauge) => { /* 매 프레임 갱신, OnGUI에서 직접 읽음 */ };

                stealth.OnStealthStateChanged += _stealthStateHandler;
                stealth.OnDetectionGaugeChanged += _detectionGaugeHandler;
            }
        }

        private void CacheStyles()
        {
            // 모든 GUIStyle을 미리 캐싱 (OnGUI에서 new GUIStyle() 호출 금지)
            _cachedLabelStyle = new GUIStyle(GUI.skin.label)
            {
                font = UIFont.Load(), // P7-1: 한글 서포트 커스텀 폰트
                fontSize = UIFont.Body, // P7-2: 24 → Body
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };

            _cachedDeathStyle = new GUIStyle(GUI.skin.label)
            {
                font = UIFont.Load(),
                fontSize = UIFont.Display, // P7-2: 96 → Display(60)
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };

            _cachedRespawnStyle = new GUIStyle(GUI.skin.label)
            {
                font = UIFont.Load(),
                fontSize = UIFont.Title, // P7-2: 40 → Title(38)
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
                font = UIFont.Load(),
                fontSize = UIFont.Badge, // P7-2: 14 → Badge(13)
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            // Phase 34: 은신 스타일
            _cachedStealthIconStyle = new GUIStyle(GUI.skin.label)
            {
                font = UIFont.Load(),
                fontSize = UIFont.Badge, // P7-2: 14 → Badge(13)
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _cachedDetectionLabelStyle = new GUIStyle(GUI.skin.label)
            {
                font = UIFont.Load(),
                fontSize = UIFont.Badge, // P7-2: 11 → Badge(13)
                alignment = TextAnchor.MiddleLeft
            };

            // 숫자 HP 표시 스타일 (하트 아래 "현재HP / 최대HP")
            _cachedHPTextStyle = new GUIStyle(GUI.skin.label)
            {
                font = UIFont.Load(),
                fontSize = UIFont.Caption, // P7-2: 18 → Caption(17)
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };

            // 경험치 바 스타일 — 좌측 "Lv.{n}" (11px Bold 흰색) + 바 위 수치 (10px 흰색)
            _cachedExpLevelStyle = new GUIStyle(GUI.skin.label)
            {
                font = UIFont.Load(),
                fontSize = UIFont.Badge, // P7-2: 11 → Badge(13)
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = Color.white }
            };
            _cachedExpValueStyle = new GUIStyle(GUI.skin.label)
            {
                font = UIFont.Load(),
                fontSize = UIFont.Badge, // P7-2: 10 → Badge(13)
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
        }

        private void CacheStaticRects()
        {
            // 하트용 Rect 초기화 (절차 생성 하트 마스크는 정사각 Rect 하나로 그린다)
            _rectHeart = new Rect(0, 0, _heartSize, _heartSize);
        }

        private void UpdateStaticRectPositions()
        {
            // 캔버스 스케일 프레임당 1회 산출 — 핫바(cb45e8ec)와 동일 공식.
            // 하트/은신 HUD/HP 라벨/EXP 바 전부 이 필드를 재사용한다 (중복 계산 제거).
            _canvasScale = Mathf.Sqrt((Screen.width / 1920f) * (Screen.height / 1080f));

            // 하트 Rect 크기 갱신 (x/y는 DrawHearts에서 하트마다 설정)
            _rectHeart.width = _heartSize * _canvasScale;
            _rectHeart.height = _heartSize * _canvasScale;

            // 사망 오버레이 위치 (매 프레임 Screen 크기로 갱신)
            _rectDeathOverlay = new Rect(0, 0, Screen.width, Screen.height);
            _rectDeathLabel = new Rect(0, Screen.height * 0.35f, Screen.width, 120);
            _rectRespawnLabel = new Rect(0, Screen.height * 0.35f + 120, Screen.width, 60);

            // 버프 아이콘 위치: 우상단
            _iconOffsetY = 20; // 상단 여백

            // 가스 분사기 타이머 위치 (상단 중앙)
            float gasX = (Screen.width - _gasTimerWidth) / 2;
            _rectGasBarBg = new Rect(gasX, _gasTimerY, _gasTimerWidth, _gasTimerHeight);
            _rectGasBarFill = new Rect(gasX + 1, _gasTimerY + 1, _gasTimerWidth - 2, _gasTimerHeight - 2);
            _rectGasLabel = new Rect(gasX - 100, _gasTimerY, _gasTimerWidth + 200, _gasTimerHeight);

            // 경험치 바 위치: 하단 중앙, 핫바(UGUI) 위 — 겹침 방지.
            // HotbarUI 패널 = BottomMargin 12 + PanelHeight 150 = 162 (1080p 캔버스 단위).
            // CanvasScaler(ScaleWithScreenSize, ref 1920x1080, match 0.5) 스케일을 픽셀로 환산:
            // scale = sqrt((w/1920) * (h/1080)) → 어떤 해상도에서도 핫바 위 14px에 배치.
            // (산식은 상단에서 _canvasScale로 1회 계산 — 중복 제거)
            float hotbarTopY = Screen.height - 162f * _canvasScale;
            float expX = (Screen.width - _expBarWidth) * 0.5f;
            float expY = hotbarTopY - _expBarHotbarGap - _expBarHeight;

            _rectExpBarBorder = new Rect(expX - 1f, expY - 1f, _expBarWidth + 2f, _expBarHeight + 2f);
            _rectExpBarBg = new Rect(expX, expY, _expBarWidth, _expBarHeight);
            _rectExpBarFill = new Rect(expX, expY, 0f, _expBarHeight); // width는 DrawExpBar에서 ratio로 갱신
            _rectExpLevelLabel = new Rect(expX - 64f, expY + _expBarHeight * 0.5f - 9f, 58f, 18f);
            _rectExpValueText = new Rect(expX, expY - 20f, _expBarWidth, 16f);
        }

        private void OnDestroy()
        {
            if (PlayerHealth.Instance != null)
            {
                PlayerHealth.Instance.OnHPChanged -= OnHealthChanged;
            }

            // Phase 34: StealthSystem 이벤트 구독 해제
            UnsubscribeStealthEvents();
        }

        /// <summary>
        /// Phase 34: StealthSystem 이벤트 구독 해제
        /// </summary>
        private void UnsubscribeStealthEvents()
        {
            var stealth = ProjectName.Systems.StealthSystem.Instance;
            if (stealth != null)
            {
                if (_stealthStateHandler != null)
                    stealth.OnStealthStateChanged -= _stealthStateHandler;
                if (_detectionGaugeHandler != null)
                    stealth.OnDetectionGaugeChanged -= _detectionGaugeHandler;
            }
        }

        private void OnHealthChanged(float current, float max)
        {
            // 데미지 감지 (체력이 감소했을 때)
            if (current < _currentHP)
            {
                _lastDamageTime = Time.time;
            }
            
            _currentHP = current;
            _maxHP = max;
            _isDead = current <= 0;
            
            // 임시 하트(노랑)는 현재 HP가 MaxHP를 초과하는 버프 체력일 때만.
            // 기존 'max > 100 → 임시 체력' 판정은 레벨업으로 MaxHP가 커진 것을 임시 체력으로
            // 오인했다 — 스펙상 레벨업 MaxHP 증가는 총칸 증가(빈 하트)로 렌더되어야 한다.
            if (current > max)
                _tempMaxHP = current;
            else
                _tempMaxHP = 0f;
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

            // 2026-09-11(4): PlayerHealth 라이브 폴링 — 하트 렌더 소스 동기화.
            // PlayerHealth.SetMaxHP()는 OnHPChanged 이벤트를 발생시키지 않아 MaxHP 증가 시
            // 하트 개수가 늘어나지 않았다. 매 프레임 폴링으로 ceil(MaxHP/20)개 전체 하트
            // (Full/Half/Empty 링) + 임시하트(노랑)가 항상 실제 값으로 렌더된다.
            // (HUD가 플레이어보다 먼저 생성되어 구독에 실패한 씬까지 커버)
            var ph = PlayerHealth.Instance;
            if (ph != null)
            {
                _currentHP = ph.CurrentHP;
                _maxHP = ph.MaxHP;
            }

            // 2026-09-11(5): PlayerStats 라이브 폴링 — EXP/Lv 표시 + 레벨업 엣지 감지(펄스 트리거).
            // 하트 HP 폴링 선례와 동일 패턴. PlayerStats.Instance는 null-safe 스킵
            // (플레이어 부재 씬에서 HUD 오류 방지).
            var ps = PlayerStats.Instance;
            if (ps != null)
            {
                int lv = ps.Level;
                if (_prevExpLevel >= 0 && lv > _prevExpLevel)
                    _lastLevelUpTime = Time.time; // 레벨업 펄스 시작
                _prevExpLevel = lv;
            }

            UpdateStaticRectPositions();

            DrawHearts();
            DrawHPNumberText(); // 하트 아래 숫자 HP 표시 ("85 / 140")
            DrawExpBar(); // 하단 중앙 경험치 바 (Lv + EXP, 플랫)
            DrawBuffIcons();
            DrawDeathOverlay();
            DrawGasSprayerTimer();
            DrawStealthHUD(); // Phase 34: 은신 HUD

            // Phase 4: GasSprayUI 연동 — 물약 정보 + 타이머 패널
            if (_gasSprayUI != null)
            {
                _gasSprayUI.OnDrawGUI();
            }
        }

        // ===== Phase 34: 은신 HUD =====

        /// <summary>
        /// 은신 상태 아이콘 + 발각 게이지 표시 (하트 아래 배치)
        /// </summary>
        private void DrawStealthHUD()
        {
            var stealth = ProjectName.Systems.StealthSystem.Instance;
            if (stealth == null) return;

            bool isStealthed = stealth.IsStealthed;
            float detectionGauge = stealth.DetectionGauge;

            if (!isStealthed && detectionGauge <= 0f)
                return;

            // Rect 위치 계산: 하트 영역 아래 (하트 시작 Y + 하트 크기 * 줄 수 + 여백)
            // 하트 렌더와 동일한 canvasScale 비례 좌표를 써야 2행 이상일 때도 안 겹친다.
            int totalHearts = Mathf.CeilToInt(_maxHP / _hpPerHeart);
            int rows = Mathf.CeilToInt((float)totalHearts / _heartsPerRow);
            float heartStep = (_heartSize + _heartSpacing) * _canvasScale;
            float heartsBottomY = _heartStartY * _canvasScale + rows * heartStep + 10f * _canvasScale;

            float iconSize = _stealthIconSize * _canvasScale;
            float iconX = _stealthIconX * _canvasScale;
            float iconY = heartsBottomY;

            _rectStealthIcon = new Rect(iconX, iconY, iconSize, iconSize);

            if (isStealthed)
            {
                // 은신 아이콘 (파란색 원)
                GUI.color = _stealthActiveColor;
                GUI.Box(_rectStealthIcon, "");

                // 아이콘 내부 텍스트
                GUI.color = Color.white;
                GUI.Label(_rectStealthIcon, "🥷", _cachedStealthIconStyle);

                // 발각 게이지 바 (아이콘 아래)
                float barX = iconX;
                float barY = iconY + iconSize + 4f * _canvasScale;
                float barWidth = _detectionBarWidth * _canvasScale;
                float barHeight = _detectionBarHeight * _canvasScale;

                _rectDetectionBarBg = new Rect(barX, barY, barWidth, barHeight);
                _rectDetectionBarFill = new Rect(barX + 1, barY + 1, (barWidth - 2) * Mathf.Clamp01(detectionGauge / 100f), barHeight - 2);

                // 배경
                GUI.color = _detectionBarBgColor;
                GUI.Box(_rectDetectionBarBg, "");

                // 채움 (색상 그라데이션)
                Color barColor;
                float ratio = detectionGauge / 100f;
                if (ratio < 0.5f)
                    barColor = Color.Lerp(_detectionBarLowColor, _detectionBarMidColor, ratio * 2f);
                else
                    barColor = Color.Lerp(_detectionBarMidColor, _detectionBarHighColor, (ratio - 0.5f) * 2f);
                GUI.color = barColor;
                GUI.Box(_rectDetectionBarFill, "");

                // 테두리
                GUI.color = Color.white;
                GUI.Box(_rectDetectionBarBg, "");

                // 레이블
                _rectStealthLabel = new Rect(barX + barWidth + 8f * _canvasScale, barY, 60f * _canvasScale, barHeight);
                GUI.color = Color.white;
                string labelText = detectionGauge >= 100f ? "🔴 발각!" : $"발각: {detectionGauge:F0}%";
                GUI.Label(_rectStealthLabel, labelText, _cachedDetectionLabelStyle);

                // 위험 상태 (70% 이상)
                if (detectionGauge >= 70f)
                {
                    GUI.color = new Color(1f, 0.2f, 0.2f, 0.3f + Mathf.Sin(Time.time * 4f) * 0.2f);
                    // 위험 표시 테두리
                    GUI.Box(new Rect(barX - 2f * _canvasScale, barY - 2f * _canvasScale, barWidth + 4f * _canvasScale, barHeight + 4f * _canvasScale), "");
                }
            }
            else if (detectionGauge > 0f)
            {
                // 은신 해제 후 게이지 잔여 표시 (서서히 사라짐)
                GUI.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                GUI.Box(_rectStealthIcon, "🥷");
            }

            GUI.color = Color.white;
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

        /// <summary>
        /// BotW 스타일 하트 시스템으로 HP 표시
        /// - 하트 1개 = 20HP (_hpPerHeart)
        /// - Full(빨강), Half(반 빨강), Empty(회색) 상태 지원
        /// - 하트 렌더링: 절차 생성 하트 마스크(흰색) + GUI.color 틴트 → GUI.DrawTexture
        /// - 데미지 시 흔들림 애니메이션
        /// - 임시 하트(노랑, 버프 초과 체력) 지원
        /// </summary>
        private void DrawHearts()
        {
            // 최대 체력 기준 전체 하트 수 계산
            // 스펙: 기본 5칸(MaxHP 100). 레벨업으로 MaxHP가 오르면 ceil(MaxHP/20)만큼 총칸이
            // 늘고, CurrentHP가 미달하는 초과분은 빈 하트(회색 외곽)로 렌더된다 ("95/150" 정합).
            // _heartsPerRow(기존값 10) 초과 시 다음 행으로 흐른다 (row = i / _heartsPerRow).
            int totalHearts = Mathf.CeilToInt(_maxHP / _hpPerHeart);
            if (totalHearts <= 0) totalHearts = 1;

            // 임시 하트 수 (버프로 인한 초과 체력)
            int tempHearts = 0;
            if (_tempMaxHP > _maxHP)
            {
                tempHearts = Mathf.CeilToInt((_tempMaxHP - _maxHP) / _hpPerHeart);
            }

            int displayHearts = totalHearts + tempHearts;

            // 데미지 흔들림 효과 (최근 0.5초 내 피격 시)
            float shakeOffset = 0f;
            if (Time.time - _lastDamageTime < 0.5f)
            {
                shakeOffset = Mathf.Sin(Time.time * 10f) * 2f;
            }

            // 캔버스 스케일 비례 시작 위치/간격 (1080p = 원래 픽셀값, _rectHeart 크기는
            // UpdateStaticRectPositions에서 스케일 반영 완료)
            float heartStep = (_heartSize + _heartSpacing) * _canvasScale;
            float startX = _heartStartX * _canvasScale + shakeOffset * _canvasScale;
            float startY = _heartStartY * _canvasScale;

            for (int i = 0; i < displayHearts; i++)
            {
                int row = i / _heartsPerRow;
                int col = i % _heartsPerRow;

                float heartX = startX + col * heartStep;
                float heartY = startY + row * heartStep;

                _rectHeart.x = heartX;
                _rectHeart.y = heartY;

                float heartHPThreshold = (i + 1) * _hpPerHeart;
                bool isTempHeart = i >= totalHearts;

                if (isTempHeart)
                {
                    // 임시 하트 (노랑) - 버프로 인한 초과 체력 (Full 마스크를 노랑 틴트로 재사용)
                    DrawHeart(_rectHeart, _heartTempColor, HeartState.Full);
                }
                else if (_currentHP >= heartHPThreshold)
                {
                    // 풀 하트 (빨강)
                    DrawHeart(_rectHeart, _heartFullColor, HeartState.Full);
                }
                else if (_currentHP >= heartHPThreshold - _hpPerHeart * 0.5f)
                {
                    // 반 하트 (좌측 절반만 빨강)
                    DrawHeart(_rectHeart, _heartHalfColor, HeartState.Half);
                }
                else
                {
                    // 빈 하트 (회색 외곽만)
                    DrawHeart(_rectHeart, _heartEmptyColor, HeartState.Empty);
                }
            }
        }

        /// <summary>
        /// 숫자 HP 표시 ("85 / 140") — 하트 영역 바로 아래에 그린다.
        /// 하트 외에 피격 시 감소하는 HP를 숫자로도 확인할 수 있게 한다.
        /// </summary>
        private void DrawHPNumberText()
        {
            if (_cachedHPTextStyle == null) return;

            // 하트 영역 맨 아래 Y 계산 (maxHP 기준 전체 하트 + 버프 임시 하트 포함 — 겹침 방지)
            int totalHearts = Mathf.CeilToInt(_maxHP / _hpPerHeart);
            if (totalHearts <= 0) totalHearts = 1;
            int tempHearts = 0;
            if (_tempMaxHP > _maxHP)
            {
                tempHearts = Mathf.CeilToInt((_tempMaxHP - _maxHP) / _hpPerHeart);
            }
            int displayHearts = totalHearts + tempHearts;
            int rows = Mathf.CeilToInt((float)displayHearts / _heartsPerRow);
            float heartStep = (_heartSize + _heartSpacing) * _canvasScale;
            float heartsBottomY = _heartStartY * _canvasScale + rows * heartStep;

            // 하트 아래 +6 지점에 숫자 HP 라벨 (Rect 재사용 — GC 방지, 위치/크기 스케일 비례)
            _rectHPText.x = _heartStartX * _canvasScale;
            _rectHPText.y = heartsBottomY + 6f * _canvasScale;
            _rectHPText.width = 200f * _canvasScale;
            _rectHPText.height = 24f * _canvasScale;

            // 가독성: 체력 여유 시 흰색, 30% 이하로 떨어지면 노랑(경고)
            float hpRatio = _maxHP > 0f ? _currentHP / _maxHP : 0f;
            GUI.color = hpRatio <= 0.3f ? Color.yellow : Color.white;

            GUI.Label(_rectHPText, $"{(int)_currentHP} / {(int)_maxHP}", _cachedHPTextStyle);

            // GUI.color 원복 (다음 Draw 호출에 영향 없도록)
            GUI.color = Color.white;
        }

        // ================================================================
        // 경험치 바 (하단 중앙, 플랫 스타일)
        // - 소스: PlayerStats.CurrentEXP / Level / GetExpForLevel(level) (누적 임계값)
        // - 채움 비율: (CurrentEXP - GetExpForLevel(lv)) / (GetExpForLevel(lv+1) - GetExpForLevel(lv))
        // - MaxLevel(50) 도달: 100% 채움 + "MAX" 라벨 (0 나눔 방지)
        // - 레벨업 펄스: _prevExpLevel 엣지 감지 → 스카이블루→흰색 플래시 0.5s (Time.time 기반 감쇠)
        // - 렌더: 1px 회백 테두리 → 다크 네이비 반투명 배경 → 스카이블루 채움 (단색 Rect, 9slice 불필요)
        // ================================================================

        /// <summary>
        /// 하단 중앙 경험치 바 표시 (좌측 "Lv.{n}" + 바 위 "현재/다음" 수치).
        /// PlayerStats.Instance가 없으면 스킵 (null-safe).
        /// </summary>
        private void DrawExpBar()
        {
            if (_cachedExpLevelStyle == null) return;

            var ps = PlayerStats.Instance;
            if (ps == null) return;

            int level = ps.Level;
            bool isMaxLevel = level >= PlayerStats.MaxLevel;

            // 채움 비율: 현재 구간 누적 경험치 기준 (GetExpForLevel = 해당 레벨 도달 누적치)
            int curExp;
            int spanExp;
            float ratio;
            if (isMaxLevel)
            {
                curExp = 0;
                spanExp = 0;
                ratio = 1f; // MAX: 100% 채움
            }
            else
            {
                curExp = ps.CurrentEXP - ps.GetExpForLevel(level);
                spanExp = ps.GetExpForLevel(level + 1) - ps.GetExpForLevel(level);
                if (spanExp <= 0) spanExp = 1; // 0 나눔 방어
                ratio = Mathf.Clamp01((float)curExp / spanExp);
            }

            // 레벨업 펄스: 0.5s 동안 흰색 → 스카이블루 감쇠 (GUI.color 틴트 — DrawFlatRect가 복원)
            Color fillColor = _expFillColor;
            float pulse = 1f - (Time.time - _lastLevelUpTime) / 0.5f;
            if (pulse > 0f)
                fillColor = Color.Lerp(_expFillColor, Color.white, Mathf.Clamp01(pulse));

            // 1px 테두리 → 반투명 배경 → 채움 (Rect 재사용 — GC 방지)
            DrawFlatRect(_rectExpBarBorder, _expBorderColor);
            DrawFlatRect(_rectExpBarBg, _expBgColor);
            _rectExpBarFill.width = _rectExpBarBg.width * ratio;
            DrawFlatRect(_rectExpBarFill, fillColor);

            // 라벨: 좌측 "Lv.{n}" (11px Bold 흰색) + 바 위 중앙 수치 ("320/500" 또는 "MAX")
            GUI.Label(_rectExpLevelLabel, $"Lv.{level}", _cachedExpLevelStyle);
            GUI.Label(_rectExpValueText, isMaxLevel ? "MAX" : $"{curExp}/{spanExp}", _cachedExpValueStyle);
        }

        /// <summary>단색 Rect 렌더용 1x1 흰색 텍스처 (static lazy 캐시 — 하트 마스크 선례, OnGUI GC 방지)</summary>
        private static Texture2D _texFlatWhite;

        /// <summary>
        /// 단색 Rect 그리기 — 흰색 텍스처 + GUI.color 틴트.
        /// DrawHeart 선례와 동일하게 호출 후 GUI.color를 반드시 복원 (틴트 누출 방지).
        /// </summary>
        private static void DrawFlatRect(Rect rect, Color color)
        {
            if (_texFlatWhite == null)
            {
                _texFlatWhite = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _texFlatWhite.name = "HUD_FlatWhite";
                _texFlatWhite.hideFlags = HideFlags.HideAndDontSave; // 씬/에셋 관리 대상 제외 (런타임 전용)
                _texFlatWhite.SetPixel(0, 0, Color.white);
                _texFlatWhite.Apply();
            }

            Color prevColor = GUI.color; // 틴트 복원용
            GUI.color = color;
            GUI.DrawTexture(rect, _texFlatWhite, ScaleMode.StretchToFill);
            GUI.color = prevColor;
        }

        /// <summary>
        /// 하트 상태 열거형
        /// </summary>
        private enum HeartState
        {
            Empty,
            Half,
            Full
        }

        // ================================================================
        // 절차 생성 하트 아이콘 (하트 모양 마스크 텍스처 + GUI.color 틴트)
        // - GUI.Box 사각형 근사 대신 진짜 하트 형상 아이콘을 그린다
        // - 형상 판정: 입방 하트 암시방정식 (u² + v² − 1)³ − u²·v³ ≤ 0 (하트 내부)
        // - 마스크는 흰색(RGB 255) 기저 + 알파로 형상 표현 → GUI.color 틴트로 상태색 칠하기
        //   (ArenaBattleUI._texWhite + GUI.color 패턴과 동일, 임시 하트 노랑도 마스크 재사용)
        // - 텍스처는 static 캐시: 최초 1회 lazy 생성 후 재사용 (OnGUI GC 방지)
        // ================================================================

        /// <summary>하트 마스크 텍스처 해상도(픽셀) — 64px면 40px 하트로 축소해도 충분히 부드럽다</summary>
        private const int HeartTexSize = 64;

        /// <summary>생성할 하트 마스크 종류</summary>
        private enum HeartMaskMode
        {
            Full,   // 하트 전체 내부 (풀 하트 채움용)
            Half,   // 좌측 절반만 (반 하트 채움용)
            Empty   // 외곽 링만 (테두리 / 빈 하트용)
        }

        // GC: 하트 마스크 텍스처 static 캐시 — 최초 1회 lazy 생성, 이후 모든 하트가 재사용
        private static Texture2D _texHeartFullWhite;   // 하트 전체 내부 마스크(흰색)
        private static Texture2D _texHeartHalfWhite;   // 하트 좌측 절반 마스크(흰색)
        private static Texture2D _texHeartEmptyWhite;  // 하트 외곽 링 마스크(흰색)

        /// <summary>
        /// 하트 마스크 텍스처 3종 lazy 생성 (최초 DrawHeart 호출 시 단 1회)
        /// </summary>
        private static void EnsureHeartTextures()
        {
            if (_texHeartFullWhite != null && _texHeartHalfWhite != null && _texHeartEmptyWhite != null)
                return;

            _texHeartFullWhite  = CreateHeartMaskTexture(HeartMaskMode.Full);
            _texHeartHalfWhite  = CreateHeartMaskTexture(HeartMaskMode.Half);
            _texHeartEmptyWhite = CreateHeartMaskTexture(HeartMaskMode.Empty);
        }

        /// <summary>
        /// 하트 암시방정식: f(u,v) = (u² + v² − 1)³ − u²·v³ ≤ 0 이면 하트 내부.
        /// 수학 좌표계(v 위쪽 양수) 기준 — 아래가 뾰족하고 위에 로브 2개인 하트 형상.
        /// </summary>
        private static float HeartImplicit(float u, float v)
        {
            float q = u * u + v * v - 1f;
            return q * q * q - u * u * v * v * v;
        }

        /// <summary>
        /// 하트 모양 "흰색 마스크" 텍스처를 절차 생성한다. (최초 1회만 호출)
        /// - 좌표 변환: 텍셀 중심을 [-1,1] 정규화(u: 오른쪽+, v: 위쪽+).
        ///   Texture2D 픽셀 (0,0)은 좌하단(UV 원점)이고 GUI.DrawTexture는 텍스처를 뒤집지 않고
        ///   그대로 그리므로, 수학 y축(v)을 픽셀 y에 그대로 매핑하면 하트가 세워진 채
        ///   렌더링된다 (아래 뾰족 부분이 Rect 하단에 위치). → 별도 y 반전 불필요.
        /// - RGB는 항상 순수 흰색, 형상은 알파로만 표현 → GUI.color 틴트로 어떤 상태색이든 칠해진다.
        ///   (알파 0 픽셀도 RGB를 흰색으로 유지해 bilinear 축소 시 어두운 외곽 번짐 방지)
        /// - 외곽 계단 현상 완화: 텍셀당 4서브샘플(±1/4 텍셀) 내부 판정 평균을 알파로 사용.
        /// - Empty 마스크: 내부 영역을 4방향 이웃 기준 2회 침식해 남은 코어를 빼면 2px 외곽 링.
        /// </summary>
        private static Texture2D CreateHeartMaskTexture(HeartMaskMode mode)
        {
            int size = HeartTexSize;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "HUD_HeartMask_" + mode;
            tex.hideFlags = HideFlags.HideAndDontSave;   // 씬/에셋 관리 대상 제외 (런타임 전용)
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;        // 축소 시 부드러운 외곽

            // 하트 묘형은 y ∈ [-1, 약 1.26] — 묘형 중심(≈0.13)을 텍스처 정중앙에 맞추는 보정
            const float scale = 26f;   // 하트 폭(≈2.14)이 64px 안에 여백 2~3px를 두고 들어가도록
            const float vShift = 0.13f;

            var inside = new bool[size * size];     // 외곽 링 판정용 내부 플래그
            var alphaFill = new float[size * size]; // Full 마스크 알파
            var alphaLeft = new float[size * size]; // Half 마스크 알파 (u ≤ 0 좌측 절반)

            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    // 텍셀 중심 → 하트 수학좌표 (v는 위쪽 양수, 픽셀 y도 위로 증가 → 무반전)
                    float u = (px + 0.5f - size * 0.5f) / scale;
                    float v = (py + 0.5f - size * 0.5f) / scale + vShift;

                    // 텍셀당 4서브샘플로 내부 판정 (간이 안티앨리어싱)
                    int hit = 0;
                    int hitLeft = 0;
                    for (int sy = 0; sy < 2; sy++)
                    {
                        for (int sx = 0; sx < 2; sx++)
                        {
                            float su = u + (sx == 0 ? -0.25f : 0.25f) / scale;
                            float sv = v + (sy == 0 ? -0.25f : 0.25f) / scale;
                            if (HeartImplicit(su, sv) <= 0f)
                            {
                                hit++;
                                if (su <= 0f) hitLeft++;   // 좌측 절반(u ≤ 0)만 카운트
                            }
                        }
                    }

                    int idx = py * size + px;
                    inside[idx] = hit >= 2;            // 커버리지 절반 이상을 내부로 간주
                    alphaFill[idx] = hit * 0.25f;      // 0/0.25/0.5/0.75/1 단계 알파
                    alphaLeft[idx] = hitLeft * 0.25f;  // 반 하트: 좌측 절반 커버리지
                }
            }

            // 모드별 최종 알파 선택
            float[] alpha;
            if (mode == HeartMaskMode.Full)
            {
                alpha = alphaFill;
            }
            else if (mode == HeartMaskMode.Half)
            {
                alpha = alphaLeft;
            }
            else // Empty: 내부를 2회 침식 → 침식 코어 바깥의 내부 픽셀 = 외곽 2px 링
            {
                var core = new bool[size * size];
                System.Array.Copy(inside, core, core.Length);

                for (int pass = 0; pass < 2; pass++)
                {
                    var eroded = new bool[size * size];
                    for (int py = 0; py < size; py++)
                    {
                        for (int px = 0; px < size; px++)
                        {
                            int idx = py * size + px;
                            // 4방향 이웃이 모두 코어일 때만 유지 (테두리는 즉시 침식)
                            eroded[idx] = core[idx]
                                && px > 0 && core[idx - 1]
                                && px < size - 1 && core[idx + 1]
                                && py > 0 && core[idx - size]
                                && py < size - 1 && core[idx + size];
                        }
                    }
                    core = eroded;
                }

                var alphaRing = new float[size * size];
                for (int i = 0; i < alphaRing.Length; i++)
                    alphaRing[i] = (inside[i] && !core[i]) ? 1f : 0f;
                alpha = alphaRing;
            }

            // 픽셀 확정: RGB 흰색 고정 + 알파로 형상 (GUI.color 틴트용 마스크)
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha[i]) * 255f);
                pixels[i] = new Color32(255, 255, 255, a);
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// 단일 하트 그리기 — 절차 생성 하트 마스크 + GUI.color 틴트 (진짜 하트 형상)
        /// 1) 상태 채움: Full → 전체 마스크, Half → 좌측 절반 마스크를 상태색으로 틴트해 DrawTexture
        ///    (임시 하트는 color가 노랑이므로 동일 마스크를 그대로 재사용)
        /// 2) 외곽 링: 채워진 하트는 흰 테두리, 빈 하트는 회색 외곽을 채움 위에 덮어 그림
        /// 3) GUI.color는 반드시 원복 — 틴트가 이후 GUI 호출에 누출되지 않도록
        /// </summary>
        private void DrawHeart(Rect rect, Color color, HeartState state)
        {
            EnsureHeartTextures();

            Color prevColor = GUI.color;   // 틴트 복원용

            // 1) 상태 채움 (Empty는 채움 없음)
            if (state == HeartState.Full)
            {
                GUI.color = color;
                GUI.DrawTexture(rect, _texHeartFullWhite);
            }
            else if (state == HeartState.Half)
            {
                GUI.color = color;
                GUI.DrawTexture(rect, _texHeartHalfWhite);
            }

            // 2) 외곽 링 (하트 윤곽) — 채움 위에 덮어 그려 테두리를 선명하게
            GUI.color = (state == HeartState.Empty) ? _heartEmptyColor : Color.white;
            GUI.DrawTexture(rect, _texHeartEmptyWhite);

            // 3) GUI.color 복원 (틴트 누출 방지)
            GUI.color = prevColor;
        }

        private void DrawBuffIcons()
        {
            if (BuffManager.Instance == null) return;

            var activeBuffs = BuffManager.Instance.GetActiveBuffs();
            if (activeBuffs == null) return;
            
            // 우상단에서 시작 (Screen.width - 200에서 왼쪽으로)
            float x = Screen.width + _iconOffsetX; // _iconOffsetX는 음수 (예: -200)
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

                x -= size + spacing; // 왼쪽으로 이동
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