using UnityEngine;
using UnityEngine.InputSystem;
using ProjectName.Core;

namespace ProjectName.UI
{
    /// <summary>
    /// 상점Placeholder - 사장님이 GLB를 제공하기 전까지 사용할 임시 상점 모델.
    /// 현재는 간단한 큐브로 표현하며, 나중에 GLB 모델로 교체됩니다.
    /// 플레이어가 E키로 상호작용하면 상점 UI를 엽니다.
    /// </summary>
    public class ShopPlaceholder : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private string _shopkeeperName = "상인";
        [SerializeField] private float _interactRange = 3f;

        // 참조를 위한 UIManager
        private UIManager _uiManager;
        
        // 캐싱된 플레이어 참조 (매 프레임 Find 대신)
        private GameObject _player;
        
        // 재사용되는 상점 창 인스턴스
        private GameObject _shopWindowInstance;
        private ShopWindow _shopWindow;

        private void Awake()
        {
            _uiManager = UIManager.Instance;
            if (_uiManager == null)
            {
                Debug.LogWarning("[ShopPlaceholder] UIManager를 찾을 수 없습니다! NPC가 작동하지 않을 수 있습니다.");
            }
        }

        private void Start()
        {
            Debug.Log($"[ShopPlaceholder] {_shopkeeperName} 상점 생성됨");
        }

        private void Update()
        {
            // 플레이어 참조 캐싱 (씬 전환 시 재탐색)
            if (_player == null)
            {
                _player = GameObject.FindGameObjectWithTag("Player");
                if (_player == null) return;
            }

            // sqrMagnitude로 거리 비교 (Distance보다 GC/성능 우수)
            float sqrDist = (transform.position - _player.transform.position).sqrMagnitude;
            if (sqrDist > _interactRange * _interactRange) return;

            // E키로 상호작용 (Input System)
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                ToggleShop();
            }
        }

        /// <summary>
        /// 상점 토글 (열림/닫힘)
        /// </summary>
        public void ToggleShop()
        {
            if (_uiManager == null)
            {
                Debug.LogWarning("[ShopPlaceholder] UIManager가 없어 상점을 열 수 없습니다!");
                return;
            }

            // 최초 요청 시에만 ShopWindow 인스턴스 생성 (이후 재사용)
            if (_shopWindowInstance == null)
            {
                CreateShopWindowInstance();
            }
            
            if (_shopWindowInstance == null || _shopWindow == null)
            {
                Debug.LogWarning("[ShopPlaceholder] ShopWindow 인스턴스를 생성할 수 없습니다!");
                return;
            }

            // UIManager를 통해 열기/닫기 (윈도우 스택 관리)
            _uiManager.OpenWindow(_shopWindow);
            
            // Show/Hide에 따라 게임 오브젝트 활성화 상태 업데이트
            if (_shopWindow.IsOpen)
            {
                _shopWindowInstance.SetActive(true);
                Debug.Log("[ShopPlaceholder] 상점 UI 열림");
            }
            else
            {
                // 닫힐 때는 UIWindow Hide()가 CloseAnimation 코루틴을 실행하므로
                // 오브젝트는 활성 상태로 두고 애니메이션 완료 후 비활성화는 UIWindow가 처리
                Debug.Log("[ShopPlaceholder] 상점 UI 닫힘 요청");
            }
        }

        /// <summary>
        /// 상점 창 인스턴스 생성 (최초 1회, 이후 재사용)
        /// </summary>
        private void CreateShopWindowInstance()
        {
            _shopWindowInstance = new GameObject("ShopWindow_Runtime");
            
            _shopWindow = _shopWindowInstance.AddComponent<ShopWindow>();
            
            if (_shopWindow == null)
            {
                Debug.LogError("[ShopPlaceholder] ShopWindow 컴포넌트를 추가할 수 없습니다!");
                Destroy(_shopWindowInstance);
                _shopWindowInstance = null;
                return;
            }
            
            // 초기에는 비활성화 상태
            _shopWindowInstance.SetActive(false);
            
            Debug.Log("[ShopPlaceholder] 런타임 시 ShopWindow 인스턴스 생성 (재사용)");
        }

        private void OnDestroy()
        {
            // ShopPlaceholder가 파괴될 때 ShopWindow 인스턴스 정리 (메모리 누수 방지)
            if (_shopWindowInstance != null)
            {
                Destroy(_shopWindowInstance);
                _shopWindowInstance = null;
                _shopWindow = null;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _interactRange);
        }
    }
}