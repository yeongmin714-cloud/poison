using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C8-33: 분사 키 입력 처리
    /// GasSprayerController와 동일 GameObject에 배치하여 분사 입력을 처리합니다.
    /// 가스 소모는 GasSprayerController.Update()에서 처리됩니다.
    /// 
    /// 분사 키: 우클릭(홀드) + G키(토글)
    /// </summary>
    [RequireComponent(typeof(GasSprayerController))]
    public class SprayInputHandler : MonoBehaviour
    {
        private GasSprayerController _controller;

        [Header("입력 설정")]
        [SerializeField, Tooltip("분사 키 (홀드)")]
        private KeyCode _sprayHoldKey = KeyCode.Mouse1;  // 우클릭 홀드

        [SerializeField, Tooltip("분사 토글 키 (G)")]
        private KeyCode _sprayToggleKey = KeyCode.G;     // G키 토글

        // 토글 모드 상태
        private bool _toggleActive = false;

        private void Awake()
        {
            _controller = GetComponent<GasSprayerController>();
            if (_controller == null)
            {
                Debug.LogError("[SprayInputHandler] GasSprayerController를 찾을 수 없습니다!");
                enabled = false;
            }
        }

        private void Update()
        {
            if (_controller == null) return;

            // 장착 해제 시 강제 중단
            if (!_controller.IsEquipped)
            {
                if (_controller.IsSpraying)
                    _controller.StopSpray();
                _toggleActive = false;
                return;
            }

            // C8-34: 재장전 중에는 입력 무시
            if (_controller.IsReloading)
            {
                if (_controller.IsSpraying)
                    _controller.StopSpray();
                _toggleActive = false;
                return;
            }

            // G키 토글 (눌렀다 떼면 상태 반전)
            if (Input.GetKeyDown(_sprayToggleKey))
            {
                if (!_controller.IsSpraying && _controller.CurrentSprayTimeRemaining > 0f)
                {
                    _toggleActive = true;
                    _controller.StartSpray();
                }
                else if (_controller.IsSpraying && _toggleActive)
                {
                    _toggleActive = false;
                    _controller.StopSpray();
                }
            }

            // 우클릭 홀드
            bool sprayHeld = Input.GetKey(_sprayHoldKey);

            // 토글 모드면 우클릭은 추가 분사 (토글과 독립)
            if (!_toggleActive)
            {
                if (sprayHeld && !_controller.IsSpraying)
                {
                    if (_controller.CurrentSprayTimeRemaining > 0f)
                    {
                        _controller.StartSpray();
                    }
                }
                else if (!sprayHeld && _controller.IsSpraying)
                {
                    _controller.StopSpray();
                }
            }
            else
            {
                // 토글 모드에서는 우클릭이 분사를 잠시 덮어쓰지 않도록
                // 우클릭 홀드 + 토글 → 우클릭 릴리즈 시 토글 상태 유지
                if (sprayHeld && !_controller.IsSpraying)
                {
                    // 토글 모드지만 우클릭으로도 분사 시작 가능
                    if (_controller.CurrentSprayTimeRemaining > 0f)
                    {
                        _controller.StartSpray();
                    }
                }
                // 우클릭을 놓아도 토글 모드면 분사 유지
            }
        }
    }
}