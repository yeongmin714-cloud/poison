using UnityEngine;
using ProjectName.UI.Core;

namespace ProjectName.UI
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
        private Transform _player;

        private void Awake()
        {
            _uiManager = UIManager.Instance;
            if (_uiManager == null)
                Debug.LogWarning("[ChurchNPCInteraction] UIManager를 찾을 수 없습니다!");
        }

        private void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            Debug.Log($"[ChurchNPCInteraction] {_npcName} 생성됨");
        }

        private void Update()
        {
            if (_player == null)
            {
                _player = GameObject.FindGameObjectWithTag("Player")?.transform;
                if (_player == null) return;
            }

            float dist = Vector3.Distance(transform.position, _player.position);
            bool nearby = dist <= _interactRange;

            if (nearby && Input.GetKeyDown(KeyCode.E))
            {
                ToggleChurchUI();
            }

            if (_churchUIInstance != null)
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
            else
            {
                Debug.LogWarning("[ChurchNPCInteraction] UIManager 인스턴스가 없어 ChurchUI를 토글할 수 없습니다!");
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
            if (_player == null)
            {
                _player = GameObject.FindGameObjectWithTag("Player")?.transform;
                if (_player == null) return;
            }

            float dist = Vector3.Distance(transform.position, _player.position);
            if (dist > _interactRange) return;

            // 화면 하단 중앙 (CraftingStation/CookingStation과 일관성)
            float labelWidth = 300;
            float labelHeight = 30;
            float x = (Screen.width - labelWidth) / 2f;
            float y = Screen.height - 80;

            string msg = $"[E] {_npcName} — 기부 메뉴";
            GUI.Box(new Rect(x, y, labelWidth, labelHeight), msg);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _interactRange);
        }
    }
}