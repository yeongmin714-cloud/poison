using UnityEngine;
using ProjectName.Core;
using ProjectName.UI;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 5.6.1: 영지 크래프팅 시설.
    /// 영지 내에 배치되는 크래프팅 워크스테이션.
    /// E 키로 제작 UI를 열고 영지 고유 레시피를 사용합니다.
    /// </summary>
    public class TerritoryCraftingStation : MonoBehaviour
    {
        [Header("영지 크래프팅 설정")]
        [SerializeField] private string _stationName = "영지 작업대";
        [SerializeField] private float _interactRange = 3f;
        [SerializeField] private string _territoryId = "East_01";

        [Header("레시피")]
        [SerializeField] private Recipe[] _availableRecipes;

        [Header("레벨 제한")]
        [SerializeField] private int _minLevel = 1;

        private Transform _player;
        private bool _isPlayerNearby;

        public string StationName => _stationName;
        public string TerritoryId => _territoryId;
        public Recipe[] AvailableRecipes => _availableRecipes;

        private void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        private void Update()
        {
            if (_player == null) return;

            float dist = Vector3.Distance(transform.position, _player.position);
            _isPlayerNearby = dist <= _interactRange;

            if (_isPlayerNearby && Input.GetKeyDown(KeyCode.E))
            {
                OpenCraftingUI();
            }
        }

        private void OpenCraftingUI()
        {
            if (UIManager.Instance != null && UIManager.Instance.craftingWindow != null)
            {
                UIManager.Instance.craftingWindow.Open();
                Debug.Log($"[TerritoryCraftingStation] {_stationName} 열림 (영지: {_territoryId})");
            }
            else
            {
                Debug.LogWarning("[TerritoryCraftingStation] CraftingWindow가 UIManager에 없습니다.");
            }
        }

        /// <summary>
        /// 플레이어가 해당 영지 크래프팅 스테이션을 사용할 수 있는지 확인.
        /// </summary>
        public bool CanUse()
        {
            if (PlayerStats.Instance == null) return false;
            return PlayerStats.Instance.Level >= _minLevel;
        }

        private void OnGUI()
        {
            if (!_isPlayerNearby) return;

            string msg = $"[E] {_stationName}";
            if (!CanUse())
                msg += $" (레벨 {_minLevel} 필요)";

            GUI.Label(new Rect(Screen.width / 2 - 150, Screen.height / 2 + 50, 300, 30), msg);
        }
    }
}