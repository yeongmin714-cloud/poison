using ProjectName.UI;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// FIX-01: 교회 NPC — E키로 기부 메뉴(ChurchUI)를 엽니다.
    /// ShopPlaceholder와 유사한 패턴으로 ChurchUI 인스턴스를 생성하여 토글합니다.
    /// </summary>
    public class ChurchNPCInteraction : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private string _npcName = "성당 관리인";
        [SerializeField] private float _interactRange = 3f;

        private UIManager _uiManager;
        private GameObject _churchUIInstance;
        private ChurchUI _churchUI;

        private void Awake()
        {
            _uiManager = UIManager.Instance;
            if (_uiManager == null)
                Debug.LogWarning("[ChurchNPCInteraction] UIManager를 찾을 수 없습니다!");
        }

        private void Start()
        {
            Debug.Log($"[ChurchNPCInteraction] {_npcName} 생성됨");
        }

        private void Update()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            float dist = Vector3.Distance(transform.position, player.transform.position);
            bool nearby = dist <= _interactRange;

            if (nearby && Input.GetKeyDown(KeyCode.E))
            {
                ToggleChurchUI();
            }

            CheckAndCleanupClosedWindow();
        }

        public void ToggleChurchUI()
        {
            if (_churchUIInstance == null)
            {
                CreateChurchUIInstance();
            }

            if (_churchUIInstance == null || _churchUI == null)
            {
                Debug.LogWarning("[ChurchNPCInteraction] ChurchUI 인스턴스를 생성할 수 없습니다!");
                return;
            }

            if (_uiManager != null)
            {
                _uiManager.ToggleWindow(_churchUI);
            }

            if (_churchUI.IsOpen)
            {
                _churchUIInstance.SetActive(true);
                Debug.Log("[ChurchNPCInteraction] ChurchUI 열림");
            }
        }

        private void CreateChurchUIInstance()
        {
            _churchUIInstance = new GameObject("ChurchUI_Runtime");
            _churchUI = _churchUIInstance.AddComponent<ChurchUI>();

            if (_churchUI == null)
            {
                Debug.LogError("[ChurchNPCInteraction] ChurchUI 컴포넌트를 추가할 수 없습니다!");
                _churchUIInstance = null;
                return;
            }

            _churchUIInstance.SetActive(false);
            Debug.Log("[ChurchNPCInteraction] 런타임 ChurchUI 인스턴스 생성");
        }

        private void CheckAndCleanupClosedWindow()
        {
            if (_churchUIInstance != null && _churchUI != null && !_churchUI.IsOpen)
            {
                Destroy(_churchUIInstance);
                _churchUIInstance = null;
                _churchUI = null;
                Debug.Log("[ChurchNPCInteraction] 닫힌 ChurchUI 인스턴스 정리");
            }
        }

        private void OnGUI()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist > _interactRange) return;

            string msg = $"[E] {_npcName} — 기부 메뉴";
            GUI.Label(new Rect(Screen.width / 2 - 150, Screen.height / 2 + 50, 300, 30), msg);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _interactRange);
        }
    }
}