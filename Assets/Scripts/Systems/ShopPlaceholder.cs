using UnityEngine;
using ProjectName.Core;
using ProjectName.UI;
using UnityEngine.InputSystem;

namespace ProjectName.Systems
{
    /// <summary>
    /// 상점Placeholder - 사장님이 GLB를 제공하기 전까지 사용할 임시 상점 모델.
    /// 현재는 간단한 큐브로 표현하며, 나중에 GLB 모델로 교체됩니다.
    /// 플레이어가 E키로 상호작용하면 상점 UI를 엽니다.
    /// </summary>
    public class ShopPlaceholder : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private string shopkeeperName = "상인";
        [SerializeField] private float _interactRange = 3f;

        // 참조를 위한 UIManager
        private UIManager _uiManager;
        
        // 현재 활성화된 상점 창 인스턴스 (없으면 null)
        private GameObject _shopWindowInstance;
        private ShopWindow _shopWindow;

        private void Awake()
        {
            // UIManager 찾기
            _uiManager = UIManager.Instance;
            if (_uiManager == null)
            {
                Debug.LogWarning("[ShopPlaceholder] UIManager를 찾을 수 없습니다!");
            }
        }

        private void Start()
        {
            Debug.Log($"[ShopPlaceholder] {shopkeeperName} 상점 생성됨");
        }

        private void Update()
        {
            // 플레이어 찾기
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            float dist = Vector3.Distance(transform.position, player.transform.position);
            bool nearby = dist <= _interactRange;

            // E키로 상호작용 (Input System)
            if (nearby && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                ToggleShop();
            }
            
            // 창이 닫혔는지 확인하여 인스턴스 정리
            CheckAndCleanupClosedWindow();
        }

        /// <summary>
        /// 상점 토클 (열림/닫힘)
        /// </summary>
        public void ToggleShop()
        {
            // 상점 창 인스턴스가 없으면 생성
            if (_shopWindowInstance == null)
            {
                CreateShopWindowInstance();
            }
            
            if (_shopWindowInstance == null || _shopWindow == null)
            {
                Debug.LogWarning("[ShopPlaceholder] ShopWindow 인스턴스를 생성할 수 없습니다!");
                return;
            }

            // UIManager를 통해 토글 (윈도우 스택 관리)
            _uiManager.ToggleWindow(_shopWindow);
            
            // Show/Hide에 따라 게임 오브젝트 활성화 상태 업데이트
            // UIWindow.Show()/Hide()가 내부에서 처리하므로 여기서는 활성화만 관리
            if (_shopWindow.IsOpen)
            {
                _shopWindowInstance.SetActive(true);
                Debug.Log("[ShopPlaceholder] 상점 UI 열림");
            }
            else
            {
                // 닫힐 때는 잠시 비활성화 상태로 두지만, 실제 정리는 CheckAndCleanupClosedWindow에서 함
                Debug.Log("[ShopPlaceholder] 상점 UI 닫힘 요청");
            }
        }

        /// <summary>
        /// 상점 창 인스턴스 생성
        /// </summary>
        private void CreateShopWindowInstance()
        {
            // 새 GameObject 생성
            _shopWindowInstance = new GameObject("ShopWindow_Runtime");
            
            // ShopWindow 컴포넌트 추가
            _shopWindow = _shopWindowInstance.AddComponent<ShopWindow>();
            
            if (_shopWindow == null)
            {
                Debug.LogError("[ShopPlaceholder] ShopWindow 컴포넌트를 추가할 수 없습니다!");
                _shopWindowInstance = null;
                return;
            }
            
            // 초기에는 비활성화 상태
            _shopWindowInstance.SetActive(false);
            
            Debug.Log("[ShopPlaceholder] 런타임 시 ShopWindow 인스턴스 생성");
        }

        /// <summary>
        /// 닫힌 창이 있는지 확인하고 정리 (메모리 누수 방지)
        /// </summary>
        private void CheckAndCleanupClosedWindow()
        {
            // 인스턴스가 있고, 창이 닫혔으면 인스턴스 파괴
            if (_shopWindowInstance != null && _shopWindow != null && !_shopWindow.IsOpen)
            {
                // 조금 delay를 줘서 창이 완전히 닫혔는지 확인 (같은 프레임에서 열렸다가 닫힐 수 있음)
                // 하지만 여기서는 간단히 바로 정리
                Destroy(_shopWindowInstance);
                _shopWindowInstance = null;
                _shopWindow = null;
                Debug.Log("[ShopPlaceholder] 닫힌 ShopWindow 인스턴스 정리");
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _interactRange);
        }
    }
}