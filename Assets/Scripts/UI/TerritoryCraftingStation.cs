using UnityEngine;
using ProjectName.Core;
using UnityEngine.InputSystem;
using ProjectName.UI.Core;

namespace ProjectName.UI
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

        private GameObject _player;
        private bool _isPlayerNearby;

        public string StationName => _stationName;
        public string TerritoryId => _territoryId;
        public Recipe[] AvailableRecipes => _availableRecipes;

        private void Update()
        {
            // 플레이어 참조 캐싱 (씬 전환/재생성 대비)
            if (_player == null)
            {
                _player = GameObject.FindGameObjectWithTag("Player");
                if (_player == null) return;
            }

            float sqrDist = (transform.position - _player.transform.position).sqrMagnitude;
            float sqrRange = _interactRange * _interactRange;
            _isPlayerNearby = sqrDist <= sqrRange;

            if (_isPlayerNearby && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                if (!CanUse())
                {
                    Debug.Log($"[TerritoryCraftingStation] 레벨 부족: 필요 {_minLevel}, 현재 {PlayerStats.Instance?.Level ?? 0}");
                    return;
                }
                OpenCraftingUI();
            }
        }

        private void OpenCraftingUI()
        {
            Debug.Log($"[TerritoryCraftingStation] {_stationName} 열림 (영지: {_territoryId})");

            if (UIManager.Instance != null)
            {
                UIManager.Instance.OpenWindow(typeof(CraftingUI));
            }
            else
            {
                Debug.LogWarning("[TerritoryCraftingStation] UIManager가 없습니다.");
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
            if (!_isPlayerNearby || _player == null) return;

            float labelWidth = 320;
            float labelHeight = 40;
            float x = (Screen.width - labelWidth) / 2f;
            float y = Screen.height - 90;

            string msg = $"[E] {_stationName}";
            if (!CanUse())
                msg += $" (레벨 {_minLevel} 필요)";

            GUI.Box(new Rect(x, y, labelWidth, labelHeight), msg);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, _interactRange);
        }
    }
}