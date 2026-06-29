using System.Collections;
using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.UI
{
    /// <summary>
    /// Phase 35: 자물쇠 따기 미니게임 UI (IMGUI).
    /// 핀 3~7개 표시, 픽 도구 선택, 내구도 표시, 타이머.
    /// </summary>
    public class LockpickingUI : MonoBehaviour
    {
        [Header("UI 스타일")]
        [SerializeField] private int _pinDisplayWidth = 60;
        [SerializeField] private int _pinDisplayHeight = 150;
        [SerializeField] private int _pinSpacing = 10;

        // 상태
        private bool _isOpen;
        private string _currentLocationId;
        private LockpickingSystem.LockDifficulty _currentDifficulty;
        private LockpickingSystem.PickGrade _currentPickGrade = LockpickingSystem.PickGrade.Basic;
        private int _currentDurability;
        private int _maxDurability;
        private float _pickTimer; // 핀 튕김 후 재시도 쿨다운

        // 마스터 키 보유 여부 (외부에서 설정)
        private bool _hasMasterKey;
        public bool HasMasterKey
        {
            get => _hasMasterKey;
            set => _hasMasterKey = value;
        }

        // 스타일 캐시
        private GUIStyle _styleTitle;
        private GUIStyle _styleLabel;
        private GUIStyle _styleValue;
        private GUIStyle _styleWarning;
        private GUIStyle _stylePinBg;
        private GUIStyle _stylePinFill;
        private GUIStyle _stylePinTarget;
        private Texture2D _whiteTex;

        // 키보드 상태 (반복 입력 방지)
        private bool _upKeyHeld;
        private bool _downKeyHeld;
        private float _keyRepeatTimer;

        // 싱글톤
        public static LockpickingUI Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // LockedDoor 이벤트 구독 (Systems → UI 직접 참조 방지)
            LockedDoor.OnLockpickRequested += (locationId, difficulty) =>
            {
                OpenLockpicker(locationId, difficulty);
            };

            // 더미 텍스처
            _whiteTex = new Texture2D(1, 1);
            _whiteTex.SetPixel(0, 0, Color.white);
            _whiteTex.Apply();
        }

        private void OnDestroy()
        {
            if (_whiteTex != null)
                Destroy(_whiteTex);
        }

        private void Update()
        {
            if (!_isOpen) return;

            // LockpickingSystem 업데이트
            LockpickingSystem.UpdateSession(Time.deltaTime);

            // 키보드 입력 처리 (상/하)
            HandleKeyboardInput();

            // 쿨다운 타이머
            if (_pickTimer > 0)
                _pickTimer -= Time.deltaTime;
        }

        /// <summary>
        /// 자물쇠 따기 UI 열기.
        /// </summary>
        public void Open(string locationId, LockpickingSystem.LockDifficulty difficulty)
        {
            if (_isOpen)
            {
                Debug.LogWarning("[LockpickingUI] 이미 열려있음");
                return;
            }

            _currentLocationId = locationId;
            _currentDifficulty = difficulty;

            // 마스터 키 체크
            if (_hasMasterKey)
            {
                Debug.Log("[LockpickingUI] 마스터 키 보유 중 → 미니게임 스킵");
                LockpickingSystem.MasterKeyOpen(difficulty, locationId);

                // 플레이어 인벤토리에서 마스터 키 제거
                if (PlayerInventory.Instance != null)
                {
                    PlayerInventory.Instance.RemoveItem("lockpick_master", 1);
                }

                _hasMasterKey = false;
                OpenDoorEffect();
                _isOpen = false;
                return;
            }

            // 세션 시작
            LockpickingSystem.StartSession(difficulty, _currentPickGrade, locationId);

            // 세션 종료 이벤트 구독
            LockpickingSystem.OnSessionEnded += OnSessionEndedHandler;

            _currentDurability = LockpickingSystem.GetMaxDurability(_currentPickGrade);
            _maxDurability = _currentDurability;
            _pickTimer = 0;
            _isOpen = true;

            Debug.Log($"[LockpickingUI] 자물쇠 따기 UI 열림: 위치={locationId}, 난이도={difficulty}");
        }

        /// <summary>
        /// UI 닫기.
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            LockpickingSystem.OnSessionEnded -= OnSessionEndedHandler;
            LockpickingSystem.AbortSession();
            _isOpen = false;
        }

        private void OnSessionEndedHandler(LockpickingSystem.LockpickingSession session, bool success)
        {
            LockpickingSystem.OnSessionEnded -= OnSessionEndedHandler;

            if (success)
            {
                OpenDoorEffect();
            }
            else
            {
                FailEffect();
            }

            _isOpen = false;
        }

        /// <summary>
        /// 문 열림 효과.
        /// </summary>
        private void OpenDoorEffect()
        {
            Debug.Log($"[LockpickingUI] 🔓 문 열림! 위치={_currentLocationId}");
            // 효과: 추후 사운드/파티클 추가 가능
        }

        /// <summary>
        /// 실패 효과.
        /// </summary>
        private void FailEffect()
        {
            Debug.Log($"[LockpickingUI] ❌ 자물쇠 따기 실패! 위치={_currentLocationId}");
            // 효과: 추후 경보 사운드/화면 흔들림 추가 가능
        }

        // ===== 키보드 입력 처리 =====

        private void HandleKeyboardInput()
        {
            if (LockpickingSystem.CurrentSession == null || !LockpickingSystem.CurrentSession.isActive)
                return;

            // 상승 (W/Up)
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            {
                if (!_upKeyHeld || _keyRepeatTimer <= 0)
                {
                    LockpickingSystem.AdjustCurrentPin(0.05f);
                    _upKeyHeld = true;
                    _keyRepeatTimer = 0.1f;
                }
                _keyRepeatTimer -= Time.deltaTime;
            }
            else
            {
                _upKeyHeld = false;
            }

            // 하강 (S/Down)
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            {
                if (!_downKeyHeld || _keyRepeatTimer <= 0)
                {
                    LockpickingSystem.AdjustCurrentPin(-0.05f);
                    _downKeyHeld = true;
                    _keyRepeatTimer = 0.1f;
                }
                _keyRepeatTimer -= Time.deltaTime;
            }
            else
            {
                _downKeyHeld = false;
            }

            // 핀 고정 시도 (Space/E)
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E))
            {
                if (_pickTimer <= 0)
                {
                    bool result = LockpickingSystem.TrySetCurrentPin();
                    if (!result)
                    {
                        // 실패 시 내구도 감소
                        _currentDurability--;
                        _pickTimer = 0.5f; // 쿨다운

                        if (_currentDurability <= 0)
                        {
                            Debug.Log("[LockpickingUI] 🔧 픽 내구도 소진!");
                            Close();
                        }
                    }
                }
            }
        }

        // ===== IMGUI 렌더링 =====

        private void OnGUI()
        {
            if (!_isOpen) return;
            if (LockpickingSystem.CurrentSession == null) return;

            EnsureStyles();

            var session = LockpickingSystem.CurrentSession;

            // 전체 패널 크기
            float panelW = 500f;
            float panelH = 400f;
            float panelX = (Screen.width - panelW) / 2f;
            float panelY = (Screen.height - panelH) / 2f;

            // 배경
            GUI.Box(new Rect(panelX, panelY, panelW, panelH), "");

            float cy = panelY + 10f;

            // ===== 헤더 =====
            string diffName = GetDifficultyDisplayName(session.difficulty);
            GUI.Label(new Rect(panelX + 10, cy, panelW - 20, 24), $"🔒 자물쇠 따기 [{diffName}]", _styleTitle);
            cy += 30f;

            // ===== 🔧 픽 도구 선택 =====
            DrawPickToolSelector(panelX, ref cy, panelW);

            // ===== ❤️ 내구도 표시 =====
            DrawDurabilityBar(panelX, ref cy, panelW);

            // ===== ⏱️ 타이머 =====
            GUI.Label(new Rect(panelX + 10, cy, panelW - 20, 20), $"⏱️ 남은 시간: {session.timeRemaining:F1}초", _styleLabel);
            cy += 24f;

            // ===== 핀 표시 영역 =====
            DrawPins(panelX, ref cy, panelW);

            // ===== 조작 안내 =====
            cy += 10f;
            GUI.Label(new Rect(panelX + 10, cy, panelW - 20, 20), "W/S: 핀 조절 | Space/E: 고정 | ESC: 취소", _styleLabel);
            cy += 22f;

            // ===== 실패 횟수 =====
            if (session.failCount > 0)
            {
                GUI.Label(new Rect(panelX + 10, cy, panelW - 20, 20), $"❌ 실패: {session.failCount}회", _styleWarning);
                cy += 22f;
            }

            // ===== 연속 실패 경고 =====
            if (LockpickingSystem.GlobalConsecutiveFails >= 2)
            {
                GUI.Label(new Rect(panelX + 10, cy, panelW - 20, 20),
                    $"🚨 연속 실패 {LockpickingSystem.GlobalConsecutiveFails}/3! 다음 실패 시 전역 경계!", _styleWarning);
                cy += 22f;
            }

            // ===== 닫기 버튼 =====
            if (GUI.Button(new Rect(panelX + panelW - 100, panelY + 10, 80, 24), "✕ 닫기"))
            {
                Close();
            }

            // ESC 키로 닫기
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }

        private void DrawPickToolSelector(float panelX, ref float cy, float panelW)
        {
            GUI.Label(new Rect(panelX + 10, cy, 80, 20), "🔧 픽 도구:", _styleLabel);

            float btnX = panelX + 100;

            // Basic
            Color originalColor = GUI.color;
            if (_currentPickGrade == LockpickingSystem.PickGrade.Basic)
                GUI.color = Color.green;
            if (GUI.Button(new Rect(btnX, cy, 100, 22), "기본 (내구5)"))
            {
                _currentPickGrade = LockpickingSystem.PickGrade.Basic;
                _currentDurability = LockpickingSystem.GetMaxDurability(LockpickingSystem.PickGrade.Basic);
                _maxDurability = _currentDurability;
            }
            GUI.color = originalColor;

            btnX += 105;

            if (_currentPickGrade == LockpickingSystem.PickGrade.Advanced)
                GUI.color = Color.green;
            if (GUI.Button(new Rect(btnX, cy, 100, 22), "고급 (내구10)"))
            {
                _currentPickGrade = LockpickingSystem.PickGrade.Advanced;
                _currentDurability = LockpickingSystem.GetMaxDurability(LockpickingSystem.PickGrade.Advanced);
                _maxDurability = _currentDurability;
            }
            GUI.color = originalColor;

            btnX += 105;

            if (_currentPickGrade == LockpickingSystem.PickGrade.Master)
                GUI.color = Color.green;
            if (GUI.Button(new Rect(btnX, cy, 100, 22), "마스터 (내구20)"))
            {
                _currentPickGrade = LockpickingSystem.PickGrade.Master;
                _currentDurability = LockpickingSystem.GetMaxDurability(LockpickingSystem.PickGrade.Master);
                _maxDurability = _currentDurability;
            }
            GUI.color = originalColor;

            cy += 28f;
        }

        private void DrawDurabilityBar(float panelX, ref float cy, float panelW)
        {
            GUI.Label(new Rect(panelX + 10, cy, 80, 20), "❤️ 내구도:", _styleLabel);

            float barX = panelX + 90;
            float barW = 200f;
            float barH = 18f;

            // 배경
            var prevColor = GUI.color;
            GUI.color = Color.gray;
            GUI.DrawTexture(new Rect(barX, cy, barW, barH), _whiteTex);

            // 채움
            float ratio = _maxDurability > 0 ? (float)_currentDurability / _maxDurability : 0;
            GUI.color = ratio > 0.3f ? Color.green : Color.red;
            GUI.DrawTexture(new Rect(barX, cy, barW * Mathf.Clamp01(ratio), barH), _whiteTex);

            GUI.color = prevColor;

            GUI.Label(new Rect(barX + barW + 5, cy, 60, 20), $"{_currentDurability}/{_maxDurability}", _styleValue);
            cy += 24f;
        }

        private void DrawPins(float panelX, ref float cy, float panelW)
        {
            var session = LockpickingSystem.CurrentSession;
            if (session == null) return;

            int pinCount = session.pins.Length;
            float totalPinWidth = pinCount * _pinDisplayWidth + (pinCount - 1) * _pinSpacing;
            float startX = panelX + (panelW - totalPinWidth) / 2f;

            // 핀 영역 라벨
            GUI.Label(new Rect(panelX + 10, cy, panelW - 20, 20), "📌 핀 상태:", _styleLabel);
            cy += 22f;

            for (int i = 0; i < pinCount; i++)
            {
                var pin = session.pins[i];
                float pinX = startX + i * (_pinDisplayWidth + _pinSpacing);

                // 핀 배경 (어두운 영역)
                GUI.Box(new Rect(pinX, cy, _pinDisplayWidth, _pinDisplayHeight), "");

                // 목표 위치 표시 (밝은 선)
                float targetY = cy + _pinDisplayHeight * (1f - pin.targetHeight);
                var prevColor = GUI.color;

                // 현재 선택된 핀 강조
                if (i == session.currentPinIndex && !pin.isSet)
                {
                    GUI.color = Color.cyan;
                    GUI.Box(new Rect(pinX - 2, cy - 2, _pinDisplayWidth + 4, _pinDisplayHeight + 4), "");
                    GUI.color = Color.white;
                }

                // 목표 위치 (밝은 노란색 선)
                GUI.color = pin.isSet ? Color.green : new Color(1f, 0.8f, 0.2f);
                GUI.DrawTexture(new Rect(pinX + 5, targetY - 2, _pinDisplayWidth - 10, 4), _whiteTex);
                GUI.color = prevColor;

                // 현재 위치 (어두운 파란색 핀)
                float currentY = cy + _pinDisplayHeight * (1f - pin.currentHeight);
                float pinRadius = 8f;
                GUI.color = pin.isSet ? new Color(0f, 0.8f, 0f, 0.8f) : new Color(0.2f, 0.4f, 0.8f, 0.9f);
                GUI.DrawTexture(new Rect(pinX + _pinDisplayWidth / 2f - pinRadius,
                    currentY - pinRadius, pinRadius * 2, pinRadius * 2), _whiteTex);
                GUI.color = prevColor;

                // 핀 번호
                GUI.Label(new Rect(pinX, cy + _pinDisplayHeight + 2, _pinDisplayWidth, 16),
                    $"{i + 1}", new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 10,
                        alignment = TextAnchor.UpperCenter,
                        normal = { textColor = pin.isSet ? Color.green : Color.gray }
                    });

                // 고정 완료 표시
                if (pin.isSet)
                {
                    GUI.Label(new Rect(pinX, cy + _pinDisplayHeight / 2 - 10, _pinDisplayWidth, 20),
                        "✅", new GUIStyle(GUI.skin.label)
                        {
                            fontSize = 16,
                            alignment = TextAnchor.MiddleCenter
                        });
                }
            }

            cy += _pinDisplayHeight + 20f;
        }

        // ===== 유틸리티 =====

        private string GetDifficultyDisplayName(LockpickingSystem.LockDifficulty difficulty)
        {
            switch (difficulty)
            {
                case LockpickingSystem.LockDifficulty.Easy:     return "쉬움";
                case LockpickingSystem.LockDifficulty.Medium:   return "보통";
                case LockpickingSystem.LockDifficulty.Hard:     return "어려움";
                case LockpickingSystem.LockDifficulty.VeryHard: return "매우 어려움";
                case LockpickingSystem.LockDifficulty.Legendary:return "전설";
                default: return "???";
            }
        }

        private void EnsureStyles()
        {
            if (_styleTitle != null) return;

            _styleTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            _styleLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = Color.white }
            };

            _styleValue = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.yellow }
            };

            _styleWarning = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.red }
            };
        }

        /// <summary>
        /// 특정 위치의 자물쇠 UI를 엽니다 (LockedDoor E키 → 호출).
        /// </summary>
        public static void OpenLockpicker(string locationId, LockpickingSystem.LockDifficulty difficulty)
        {
            if (Instance != null)
            {
                Instance.Open(locationId, difficulty);
            }
            else
            {
                Debug.LogError("[LockpickingUI] Instance가 없습니다! LockpickingUI 프리팹이 씬에 있어야 합니다.");
            }
        }
    }
}