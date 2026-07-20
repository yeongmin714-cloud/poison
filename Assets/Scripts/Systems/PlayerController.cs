using UnityEngine;
using UnityEngine.InputSystem;
using ProjectName.Systems.Animation.Procedural;

namespace ProjectName.Systems
{
    /// <summary>
    /// 3인칭 플레이어 컨트롤러 - 주로 PlayerMovement과 PlayerCombat에 의해 관리되는 스크립트
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        private void Start()
        {
            // 초기화 코드
            Debug.Log("PlayerController initialized");
        }
        
        // PlayerMovement과 PlayerCombat에서 사용될 공통 메서드들을 여기에 정의합니다
        public void ToggleThirdPersonCamera()
        {
            // 3인칭 카메라 토글 로직
        }
        
        public void EnablePlayerCamera()
        {
            // 플레이어 카메라 활성화 로직
        }
        
        public void DisablePlayerCamera()
        {
            // 플레이어 카메라 비활성화 로직
        }
    }
}