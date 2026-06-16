using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C8-33: 분사 키 입력 처리
    /// GasSprayerController와 동일 GameObject에 배치하여 분사 입력을 처리합니다.
    /// 가스 소모는 GasSprayerController.Update()에서 처리됩니다.
    /// </summary>
    [RequireComponent(typeof(GasSprayerController))]
    public class SprayInputHandler : MonoBehaviour
    {
        private GasSprayerController _controller;

        [Header("입력 설정")]
        [SerializeField] private KeyCode _sprayKey = KeyCode.Mouse1;  // 우클릭 홀드

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
            if (_controller == null)
                return;

            // 장착 해제 시 강제 중단
            if (!_controller.IsEquipped)
            {
                if (_controller.IsSpraying)
                    _controller.StopSpray();
                return;
            }

            // C8-34: 재장전 중에는 입력 무시
            if (_controller.IsReloading)
            {
                // 분사 중이었다면 강제 중단
                if (_controller.IsSpraying)
                    _controller.StopSpray();
                return;
            }

            // 분사 입력 처리 (우클릭 홀드)
            bool sprayHeld = Input.GetKey(_sprayKey);

            if (sprayHeld && !_controller.IsSpraying)
            {
                // 가스가 남아있을 때만 분사 시작
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
    }
}