using UnityEngine;
using ProjectName.Core;
using ProjectName.UI;

namespace ProjectName.Systems
{
    /// <summary>
    /// 건물Placeholder - 사장님이 GLB를 제공하기 전까지 사용할 임시 건물 모델.
    /// 건물 종류에 따라 다른 색상과 크기로 표현됩니다.
    /// 상점 건물이면 플레이어가 E키로 상호작용하여 상점 UI를 엽니다.
    /// </summary>
    public class BuildingPlaceholder : MonoBehaviour
    {
        public enum BuildingType
        {
            Shop,
            CraftHouse,
            Church,
            NPCHouse,
            Tavern,
            Other
        }

        [Header("설정")]
        [SerializeField] public BuildingType buildingType = BuildingType.Other;
        [SerializeField] public string buildingName = "알 수 없는 건물";

        // 상점 관련 참조
        private UIManager _uiManager;
        private GameObject _shopWindowInstance;
        public ShopWindow _shopWindow;

        public void Awake()
        {
            // UIManager 찾기
            _uiManager = UIManager.Instance;
            if (_uiManager == null)
            {
                Debug.LogWarning("[BuildingPlaceholder] UIManager를 찾을 수 없습니다!");
            }
        }

        public void Start()
        {
            Debug.Log($"[BuildingPlaceholder] {buildingName} ({buildingType}) 생성됨");
        }

        private void Update()
        {
            // 상점 건물인 경우에만 상호작용 처리
            if (buildingType == BuildingType.Shop)
            {
                HandleShopInteraction();
            }
        }

        private void HandleShopInteraction()
        {
            // 플레이어 찾기
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            float dist = Vector3.Distance(transform.position, player.transform.position);
            bool nearby = dist <= 3f; // 상점 상호작용 범위

            // E키로 상호작용
            if (nearby && Input.GetKeyDown(KeyCode.E))
            {
                ToggleShop();
            }
        }

        public void ToggleShop()
        {
            // 상점 창 인스턴스가 없으면 생성
            if (_shopWindowInstance == null)
            {
                CreateShopWindowInstance();
            }
            
            if (_shopWindowInstance == null || _shopWindow == null)
            {
                Debug.LogWarning("[BuildingPlaceholder] ShopWindow 인스턴스를 생성할 수 없습니다!");
                return;
            }

            // UIManager를 통해 토글 (윈도우 스택 관리)
            _uiManager.ToggleWindow(_shopWindow);
            
            // Show/Hide에 따라 게임 오브젝트 활성화 상태 업데이트
            if (_shopWindow.IsOpen)
            {
                _shopWindowInstance.SetActive(true);
                Debug.Log($"[BuildingPlaceholder] {buildingName} 상점 UI 열림");
            }
            else
            {
                // 닫힐 때는 잠시 비활성화 상태로 두지만, 실제 정리는 CheckAndCleanupClosedWindow에서 함
                Debug.Log($"[BuildingPlaceholder] {buildingName} 상점 UI 닫힘 요청");
            }
        }

        private void CreateShopWindowInstance()
        {
            // 새 GameObject 생성
            _shopWindowInstance = new GameObject($"ShopWindow_{buildingName}");
            
            // ShopWindow 컴포넌트 추가
            _shopWindow = _shopWindowInstance.AddComponent<ShopWindow>();
            
            if (_shopWindow == null)
            {
                Debug.LogError("[BuildingPlaceholder] ShopWindow 컴포넌트를 추가할 수 없습니다!");
                _shopWindowInstance = null;
                return;
            }
            
            // 초기에는 비활성화 상태
            _shopWindowInstance.SetActive(false);
            
            Debug.Log($"[BuildingPlaceholder] {buildingName} 상점 윈도우 인스턴스 생성");
        }

        private void CheckAndCleanupClosedWindow()
        {
            // 인스턴스가 있고, 창이 닫혔으면 인스턴스 파괴
            if (_shopWindowInstance != null && _shopWindow != null && !_shopWindow.IsOpen)
            {
                Destroy(_shopWindowInstance);
                _shopWindowInstance = null;
                _shopWindow = null;
                Debug.Log($"[BuildingPlaceholder] {buildingName} 닫힌 ShopWindow 인스턴스 정리");
            }
        }

        private void OnDestroy()
        {
            // 건물 오브젝트가 파괴될 때 상점 창도 정리
            CheckAndCleanupClosedWindow();
        }

        // 건물별 기본 색상
        private Color GetDefaultColor()
        {
            switch (buildingType)
            {
                case BuildingType.Shop: return new Color(0.8f, 0.6f, 0.2f); // 노란빛
                case BuildingType.CraftHouse: return new Color(0.6f, 0.8f, 0.2f); // 연두색
                case BuildingType.Church: return new Color(0.2f, 0.6f, 0.8f); // 파란색
                case BuildingType.NPCHouse: return new Color(0.8f, 0.2f, 0.6f); // 분홍색
                case BuildingType.Tavern: return new Color(0.6f, 0.3f, 0.1f); // 갈색 (선술집)
                default: return Color.grey;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = GetDefaultColor();
            Gizmos.DrawCube(transform.position, new Vector3(3, 2, 3)); // 간단한 박스 표시
        }

        // 건물 이름 표시 (선택 사항)
        private void OnGUI()
        {
            if (buildingType != BuildingType.Shop) return; // 상점만 이름 표시하거나 모든 건물에 적용 가능
            
            // 플레이어와 가까운 경우에만 이름 표시
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            
            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist <= 10f) // 이름 표시 범위
            {
                Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2);
                if (screenPos.z > 0) // 앞쪽에 있을 때만
                {
                    GUIStyle style = new GUIStyle(GUI.skin.label);
                    style.alignment = TextAnchor.UpperCenter;
                    style.fontSize = 14;
                    style.normal.textColor = Color.yellow;
                    
                    float labelWidth = 100;
                    float labelHeight = 25;
                    GUI.Label(new Rect(screenPos.x - labelWidth/2, Screen.height - screenPos.y - labelHeight/2, labelWidth, labelHeight), buildingName, style);
                }
            }
        }
    }
}