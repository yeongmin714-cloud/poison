using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.XInput;
using System.Text;

namespace ProjectName.Systems
{
    /// <summary>
    /// 컨트롤러(게임패드) 감지 및 버튼 매핑 지원 클래스.
    /// 게임패드 연결 시 자동 감지하고, 키보드/게임패드 입력을 통합 처리합니다.
    /// 
    /// G3-10: 게임패드 감지, 버튼 매핑, 힌트 오버레이
    /// </summary>
    public class ControllerSupport : MonoBehaviour
    {
        [Header("Hint Overlay Settings")]
        [SerializeField] private float _hintFadeDuration = 5f;
        [SerializeField] private float _hintDisplayTime = 5f;
        [SerializeField] private Color _hintTextColor = Color.white;
        [SerializeField] private Color _hintBackgroundColor = new Color(0f, 0f, 0f, 0.6f);
        [SerializeField] private int _hintFontSize = 14;

        // 입력 모드
        private static bool _isGamepadConnected = false;
        private static string _gamepadName = null;
        private static InputMode _currentInputMode = InputMode.Keyboard;

        // 힌트 오버레이 상태
        private float _hintTimer = 0f;
        private float _hintAlpha = 0f;
        private bool _hintVisible = false;
        private bool _hintForceVisible = false;

        // 버튼 코드 (더블탭 방지)
        private const float ChordTimeWindow = 0.3f;
        private float _lastStartPressTime = -10f;
        private float _lastSelectPressTime = -10f;

        // 캐싱
        private Gamepad _gamepad;
        private Keyboard _keyboard;

        /// <summary>
        /// 현재 입력 모드
        /// </summary>
        public enum InputMode
        {
            Keyboard,
            Gamepad
        }

        /// <summary>
        /// 게임패드 연결 여부
        /// </summary>
        public static bool IsGamepadConnected => _isGamepadConnected;

        /// <summary>
        /// 현재 입력 모드
        /// </summary>
        public static InputMode CurrentInputMode => _currentInputMode;

        /// <summary>
        /// 게임패드 이름 반환 (Xbox Controller / PlayStation Controller / Gamepad)
        /// </summary>
        public static string GetGamepadName() => _gamepadName;

        // =========================================================================
        // Unity lifecycle
        // =========================================================================

        private void Awake()
        {
            _keyboard = Keyboard.current;
            _gamepad = Gamepad.current;
            DetectGamepad();
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            // 입력 장치 상태 갱신
            _gamepad = Gamepad.current;
            _keyboard = Keyboard.current;

            // 게임패드 연결 상태 갱신
            bool wasConnected = _isGamepadConnected;
            DetectGamepad();

            // 게임패드 새로 연결 시 힌트 표시
            if (_isGamepadConnected && !wasConnected)
            {
                ShowHint();
            }

            // 입력 모드 감지 (키보드 입력이 들어오면 키보드 모드, 게임패드 입력이 들어오면 게임패드 모드)
            DetectInputMode();

            // 힌트 오버레이 업데이트
            UpdateHint();

            // 힌트 토글 콤보 (Start + Select 동시에 누르기)
            HandleHintToggleChord();
        }

        // =========================================================================
        // 게임패드 감지
        // =========================================================================

        /// <summary>
        /// 게임패드 연결 상태와 종류를 감지합니다.
        /// </summary>
        private void DetectGamepad()
        {
            if (_gamepad != null)
            {
                _isGamepadConnected = true;
                string displayName = _gamepad.displayName;

                if (displayName != null)
                {
                    if (displayName.Contains("Xbox") || displayName.Contains("XInput") ||
                        _gamepad is XInputController)
                    {
                        _gamepadName = "Xbox Controller";
                    }
                    else if (displayName.Contains("DualShock") || displayName.Contains("DualSense") ||
                             _gamepad is DualShockGamepad)
                    {
                        _gamepadName = "PlayStation Controller";
                    }
                    else
                    {
                        _gamepadName = "Gamepad";
                    }
                }
                else
                {
                    _gamepadName = "Gamepad";
                }
            }
            else
            {
                _isGamepadConnected = false;
                _gamepadName = null;
                _currentInputMode = InputMode.Keyboard;
            }
        }

        // =========================================================================
        // 입력 모드 감지
        // =========================================================================

        /// <summary>
        /// 키보드/게임패드 입력을 감지하여 현재 입력 모드를 자동 전환합니다.
        /// </summary>
        private void DetectInputMode()
        {
            if (_keyboard != null)
            {
                // 키보드 입력이 들어오면 키보드 모드로 전환
                if (_keyboard.anyKey.wasPressedThisFrame)
                {
                    _currentInputMode = InputMode.Keyboard;
                    return;
                }
            }

            if (_gamepad != null && _isGamepadConnected)
            {
                // 게임패드 아무 버튼이나 눌렀는지 확인
                if (_gamepad.buttonSouth.wasPressedThisFrame ||  // A (Xbox) / X (PS)
                    _gamepad.buttonEast.wasPressedThisFrame ||   // B (Xbox) / O (PS)
                    _gamepad.buttonWest.wasPressedThisFrame ||   // X (Xbox) / Square (PS)
                    _gamepad.buttonNorth.wasPressedThisFrame ||  // Y (Xbox) / Triangle (PS)
                    _gamepad.leftStick.ReadValue().magnitude > 0.1f ||
                    _gamepad.rightStick.ReadValue().magnitude > 0.1f ||
                    _gamepad.dpad.ReadValue().magnitude > 0.1f ||
                    _gamepad.leftShoulder.wasPressedThisFrame ||
                    _gamepad.rightShoulder.wasPressedThisFrame ||
                    _gamepad.startButton.wasPressedThisFrame ||
                    _gamepad.selectButton.wasPressedThisFrame)
                {
                    _currentInputMode = InputMode.Gamepad;
                }
            }
        }

        // =========================================================================
        // 통합 입력 헬퍼 메서드 (Keyboard + Gamepad)
        // =========================================================================

        /// <summary>
        /// 이동 입력값 반환 (Vector2, x: 좌우, y: 전후)
        /// 키보드 WASD/방향키 + 게임패드 왼쪽 스틱 통합
        /// </summary>
        public static Vector2 GetMoveInput()
        {
            Keyboard kb = Keyboard.current;
            Gamepad gp = Gamepad.current;

            float horizontal = 0f;
            float vertical = 0f;

            // 키보드 입력
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) vertical += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) vertical -= 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) horizontal -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) horizontal += 1f;
            }

            // 게임패드 입력 (키보드가 없으면 게임패드 값만, 있으면 합산)
            if (gp != null && _isGamepadConnected)
            {
                Vector2 stick = gp.leftStick.ReadValue();
                if (stick.magnitude > 0.2f) // 데드존
                {
                    horizontal += stick.x;
                    vertical += stick.y;
                }

                // D-Pad도 이동에 포함
                Vector2 dpad = gp.dpad.ReadValue();
                if (dpad.magnitude > 0.1f)
                {
                    horizontal += dpad.x;
                    vertical += dpad.y;
                }
            }

            return new Vector2(Mathf.Clamp(horizontal, -1f, 1f), Mathf.Clamp(vertical, -1f, 1f));
        }

        /// <summary>
        /// 상호작용 키 입력 감지 (E 키 또는 A 버튼)
        /// </summary>
        public static bool GetInteractPressed()
        {
            Keyboard kb = Keyboard.current;
            Gamepad gp = Gamepad.current;

            if (kb != null && kb.eKey.wasPressedThisFrame) return true;
            if (gp != null && _isGamepadConnected && gp.buttonSouth.wasPressedThisFrame) return true;

            return false;
        }

        /// <summary>
        /// 취소/닫기 입력 감지 (ESC 키 또는 B 버튼)
        /// </summary>
        public static bool GetCancelPressed()
        {
            Keyboard kb = Keyboard.current;
            Gamepad gp = Gamepad.current;

            if (kb != null && kb.escapeKey.wasPressedThisFrame) return true;
            if (gp != null && _isGamepadConnected && gp.buttonEast.wasPressedThisFrame) return true;

            return false;
        }

        /// <summary>
        /// 메뉴 열기 입력 감지 (ESC 키, 또는 Start 버튼, 또는 X 버튼)
        /// </summary>
        public static bool GetMenuPressed()
        {
            Keyboard kb = Keyboard.current;
            Gamepad gp = Gamepad.current;

            if (kb != null && kb.escapeKey.wasPressedThisFrame) return true;
            if (gp != null && _isGamepadConnected)
            {
                if (gp.startButton.wasPressedThisFrame) return true;
                if (gp.buttonWest.wasPressedThisFrame) return true; // X 버튼
            }

            return false;
        }

        /// <summary>
        /// 퀘스트 저널 열기 입력 감지 (J 키 또는 Y 버튼)
        /// </summary>
        public static bool GetQuestJournalPressed()
        {
            Keyboard kb = Keyboard.current;
            Gamepad gp = Gamepad.current;

            if (kb != null && kb.jKey.wasPressedThisFrame) return true;
            if (gp != null && _isGamepadConnected && gp.buttonNorth.wasPressedThisFrame) return true;

            return false;
        }

        /// <summary>
        /// 대쉬 키 입력 감지 (Left Shift 또는 LB/L1)
        /// </summary>
        public static bool GetDashPressed()
        {
            Keyboard kb = Keyboard.current;
            Gamepad gp = Gamepad.current;

            if (kb != null && kb.leftShiftKey.isPressed) return true;
            if (gp != null && _isGamepadConnected && gp.leftShoulder.isPressed) return true;

            return false;
        }

        /// <summary>
        /// 구르기 키 입력 감지 (Q 키 또는 RB/R1)
        /// </summary>
        public static bool GetRollPressed()
        {
            Keyboard kb = Keyboard.current;
            Gamepad gp = Gamepad.current;

            if (kb != null && kb.qKey.wasPressedThisFrame) return true;
            if (gp != null && _isGamepadConnected && gp.rightShoulder.wasPressedThisFrame) return true;

            return false;
        }

        /// <summary>
        /// 점프 키 입력 감지 (Space 또는 A 버튼 — 점프 전용 컨텍스트용)
        /// </summary>
        public static bool GetJumpPressed()
        {
            Keyboard kb = Keyboard.current;
            Gamepad gp = Gamepad.current;

            if (kb != null && kb.spaceKey.wasPressedThisFrame) return true;
            if (gp != null && _isGamepadConnected && gp.buttonSouth.wasPressedThisFrame) return true;

            return false;
        }

        /// <summary>
        /// UI 내비게이션 입력 감지 (방향키 또는 D-Pad)
        /// </summary>
        public static Vector2 GetUINavigationInput()
        {
            Keyboard kb = Keyboard.current;
            Gamepad gp = Gamepad.current;

            float horizontal = 0f;
            float vertical = 0f;

            if (kb != null)
            {
                if (kb.upArrowKey.wasPressedThisFrame) vertical = 1f;
                if (kb.downArrowKey.wasPressedThisFrame) vertical = -1f;
                if (kb.leftArrowKey.wasPressedThisFrame) horizontal = -1f;
                if (kb.rightArrowKey.wasPressedThisFrame) horizontal = 1f;
            }

            if (gp != null && _isGamepadConnected)
            {
                Vector2 dpad = gp.dpad.ReadValue();
                if (dpad.y > 0.5f) vertical = 1f;
                else if (dpad.y < -0.5f) vertical = -1f;
                if (dpad.x < -0.5f) horizontal = -1f;
                else if (dpad.x > 0.5f) horizontal = 1f;
            }

            return new Vector2(horizontal, vertical);
        }

        /// <summary>
        /// 카메라 회전 입력 감지 (마우스 델타 또는 오른쪽 스틱)
        /// 참고: 마우스는 일반적으로 PlayerLook 등에서 처리하므로,
        /// 여기서는 게임패드 오른쪽 스틱 값만 반환합니다.
        /// </summary>
        public static Vector2 GetCameraLookInput()
        {
            Gamepad gp = Gamepad.current;

            if (gp != null && _isGamepadConnected)
            {
                Vector2 stick = gp.rightStick.ReadValue();
                if (stick.magnitude > 0.2f) // 데드존
                {
                    return stick;
                }
            }

            return Vector2.zero;
        }

        // =========================================================================
        // 힌트 오버레이
        // =========================================================================

        /// <summary>
        /// 컨트롤러 힌트를 화면에 표시합니다.
        /// </summary>
        public void ShowHint()
        {
            _hintTimer = _hintDisplayTime;
            _hintAlpha = 1f;
            _hintVisible = true;
        }

        /// <summary>
        /// 컨트롤러 힌트를 강제로 표시/숨깁니다.
        /// </summary>
        public void ToggleHint()
        {
            _hintForceVisible = !_hintForceVisible;

            if (_hintForceVisible)
            {
                _hintAlpha = 1f;
                _hintVisible = true;
            }
            else
            {
                _hintAlpha = 0f;
                _hintVisible = false;
            }
        }

        /// <summary>
        /// 힌트 오버레이 표시/페이드아웃 업데이트
        /// </summary>
        private void UpdateHint()
        {
            if (_hintForceVisible) return;

            if (_hintVisible)
            {
                _hintTimer -= Time.deltaTime;

                if (_hintTimer <= 0f)
                {
                    _hintAlpha -= Time.deltaTime / _hintFadeDuration;
                    if (_hintAlpha <= 0f)
                    {
                        _hintAlpha = 0f;
                        _hintVisible = false;
                    }
                }
            }
        }

        /// <summary>
        /// Start + Select 동시 입력으로 힌트 토글
        /// </summary>
        private void HandleHintToggleChord()
        {
            if (_gamepad == null || !_isGamepadConnected) return;

            if (_gamepad.startButton.wasPressedThisFrame)
            {
                _lastStartPressTime = Time.time;
            }
            if (_gamepad.selectButton.wasPressedThisFrame)
            {
                _lastSelectPressTime = Time.time;
            }

            // 두 버튼이 ChordTimeWindow 내에 눌렸는지 확인
            if (_gamepad.startButton.isPressed && _gamepad.selectButton.isPressed)
            {
                if (Time.time - _lastStartPressTime < ChordTimeWindow &&
                    Time.time - _lastSelectPressTime < ChordTimeWindow)
                {
                    ToggleHint();
                    _lastStartPressTime = -10f;
                    _lastSelectPressTime = -10f;
                }
            }
        }

        // =========================================================================
        // IMGUI 힌트 오버레이 그리기
        // =========================================================================

        private void OnGUI()
        {
            if (!_hintVisible || _hintAlpha <= 0f || !_isGamepadConnected) return;

            DrawHintOverlay();
        }

        /// <summary>
        /// 화면 하단에 게임패드 버튼 힌트를 표시합니다.
        /// </summary>
        private void DrawHintOverlay()
        {
            string gamepadType = _gamepadName ?? "Gamepad";
            bool isPlayStation = _gamepadName != null && _gamepadName.Contains("PlayStation");

            // PlayStation / Xbox 버튼 이름 매핑
            string btnA = isPlayStation ? "[✕]" : "[A]";
            string btnB = isPlayStation ? "[○]" : "[B]";
            string btnX = isPlayStation ? "[□]" : "[X]";
            string btnY = isPlayStation ? "[△]" : "[Y]";
            string btnLB = isPlayStation ? "[L1]" : "[LB]";
            string btnRB = isPlayStation ? "[R1]" : "[RB]";
            string btnStart = isPlayStation ? "[OPTIONS]" : "[START]";
            string btnSelect = isPlayStation ? "[SHARE]" : "[BACK]";

            StringBuilder sb = new StringBuilder();
            sb.Append($"=== {gamepadType} 컨트롤 힌트 ===\n");
            sb.Append($"{btnA} 상호작용    ");
            sb.Append($"{btnB} 취소/닫기\n");
            sb.Append($"{btnX} 메뉴    ");
            sb.Append($"{btnY} 퀘스트 저널\n");
            sb.Append($"왼쪽 스틱 이동    ");
            sb.Append($"오른쪽 스틱 카메라\n");
            sb.Append($"D-Pad UI 내비게이션\n");
            sb.Append($"{btnLB} 대쉬    ");
            sb.Append($"{btnRB} 구르기\n");
            sb.Append($"{btnStart} 메뉴    ");
            sb.Append($"{btnSelect} 취소\n");
            sb.Append($"[{btnStart} + {btnSelect}] 힌트 토글\n");
            sb.Append($"(ESC / 키보드 입력 시 자동 전환)");

            // 알파값 적용
            Color originalColor = GUI.color;
            Color bgColor = _hintBackgroundColor;
            bgColor.a *= _hintAlpha;
            GUI.color = new Color(1f, 1f, 1f, _hintAlpha);

            float boxWidth = 420f;
            float boxHeight = 200f;
            float boxX = Screen.width / 2f - boxWidth / 2f;
            float boxY = Screen.height - boxHeight - 20f;

            // 배경 그리기
            Texture2D bgTexture = new Texture2D(1, 1);
            bgTexture.SetPixel(0, 0, bgColor);
            bgTexture.Apply();
            GUI.DrawTexture(new Rect(boxX, boxY, boxWidth, boxHeight), bgTexture);

            // 텍스트 스타일
            GUIStyle hintStyle = new GUIStyle(GUI.skin.label);
            hintStyle.fontSize = _hintFontSize;
            hintStyle.normal.textColor = _hintTextColor;
            hintStyle.alignment = TextAnchor.MiddleCenter;
            hintStyle.fontStyle = FontStyle.Bold;

            GUI.Label(new Rect(boxX + 10, boxY + 10, boxWidth - 20, boxHeight - 20), sb.ToString(), hintStyle);

            // 원래 색상 복구
            GUI.color = originalColor;
        }

        // =========================================================================
        // 공개 설정 접근자 (테스트용)
        // =========================================================================

        public float HintFadeDuration => _hintFadeDuration;
        public float HintDisplayTime => _hintDisplayTime;
        public bool HintVisible => _hintVisible;
        public float HintAlpha => _hintAlpha;
        public bool HintForceVisible => _hintForceVisible;
        public string DetectedGamepadName => _gamepadName;
    }
}