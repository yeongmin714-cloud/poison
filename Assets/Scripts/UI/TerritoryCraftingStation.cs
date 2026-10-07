using UnityEngine;
using Game.UI.Core;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine.InputSystem;

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
        private PlayerStats _playerStats;
        private bool _isPlayerNearby;

        private static bool WasInteractPressed()
        {
            // A boolean OR intentionally coalesces both backends into one interaction per frame.
            bool inputSystemPressed = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
            bool legacyPressed = false;
            try
            {
                legacyPressed = Input.GetKeyDown(KeyCode.E);
            }
            catch (System.InvalidOperationException)
            {
                // Unity's legacy input API throws when the project runs Input System only.
            }
            return inputSystemPressed || legacyPressed;
        }

        private GameObject FindPlayer()
        {
            GameObject taggedPlayer = null;
            try
            {
                taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            }
            catch (UnityException)
            {
                // A scene without a Player tag can still use the movement-component fallback.
            }

            if (taggedPlayer != null) return taggedPlayer;
            PlayerMovement movement = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
            return movement != null ? movement.gameObject : null;
        }

        private static PlayerStats FindAnyPlayerStats()
        {
            return UnityEngine.Object.FindAnyObjectByType<PlayerStats>();
        }

        private PlayerStats FindPlayerStats()
        {
            if (PlayerStats.Instance != null)
            {
                _playerStats = PlayerStats.Instance;
                return _playerStats;
            }

            if (_playerStats != null) return _playerStats;
            if (_player != null)
                _playerStats = _player.GetComponentInParent<PlayerStats>();
            if (_playerStats == null)
                _playerStats = FindAnyPlayerStats();
            return _playerStats;
        }

        private void Update()
        {
            // Player tag is preferred, but some scenes/prefabs omit it.
            if (_player == null)
            {
                _player = FindPlayer();
                if (_player == null) return;
            }

            float sqrDist = (transform.position - _player.transform.position).sqrMagnitude;
            float sqrRange = _interactRange * _interactRange;
            _isPlayerNearby = sqrDist <= sqrRange;

            if (_isPlayerNearby && WasInteractPressed())
            {
                PlayerStats stats = FindPlayerStats();
                if (!CanUse())
                {
                    Debug.Log($"[TerritoryCraftingStation] 레벨 부족: 필요 {_minLevel}, 현재 {stats?.Level ?? 0} (stats null 시 CanUse 내부 경고 참조)");
                    return;
                }
                OpenCraftingUI();
            }
        }

        public string StationName => _stationName;
        public string TerritoryId => _territoryId;
        public Recipe[] AvailableRecipes => _availableRecipes;

        private void OpenCraftingUI()
        {
            Debug.Log($"[TerritoryCraftingStation] {_stationName} 열림 (영지: {_territoryId})");

            // IndoorScene is loaded additively and may run before UI bootstrap on some scene paths.
            ProjectName.UI.Toolkit.UIToolkitBootstrap.Ensure();

            // Prefer the equipment-crafting Toolkit forge when its UI root is present.
            // Keep the legacy CraftingUI path for scenes that have not migrated to Toolkit.
            if (ProjectName.UI.Toolkit.UIToolkitBootstrap.UIRoot != null)
            {
                ProjectName.UI.Toolkit.WeaponForgeUTK.Open();
                return;
            }

            Debug.LogWarning("[TerritoryCraftingStation] UIRoot 부재 — 구형 CraftingUI 폴백 시도");
            if (UIManager.Instance != null)
            {
                UIManager.Instance.OpenWindow(typeof(CraftingUI));
            }
            else
            {
                Debug.LogWarning("[TerritoryCraftingStation] UIManager가 없습니다 — 어떤 제작 창도 열 수 없음");
            }
        }

        /// <summary>
        /// 플레이어가 해당 영지 크래프팅 스테이션을 사용할 수 있는지 확인.
        /// </summary>
        public bool CanUse()
        {
            PlayerStats stats = FindPlayerStats();
            if (stats == null)
            {
                // [근본원인 수리 2026-10-08] 실내 씬에서 PlayerStats 조회 실패 시 E키가 조용히 삼켜지는 버그.
                // PlayerStats.Level은 항상 1 이상(clamp)이므로 minLevel<=1 요구는 실효 게이트가 아니다 —
                // stats 부재 = 판정 불가이지 미달이 아니다. minLevel<=1이면 허용(1회 경고), 초과 요구면 차단+로그.
                if (_minLevel <= 1)
                {
                    Debug.LogWarning("[TerritoryCraftingStation] PlayerStats 미발견 — minLevel<=1 이므로 상호작용 허용");
                    return true;
                }
                Debug.LogWarning($"[TerritoryCraftingStation] PlayerStats 미발견 — 레벨 {_minLevel} 검증 불가로 차단");
                return false;
            }
            return stats.Level >= _minLevel;
        }

        /// <summary>
        /// 런타임 AddComponent 후 외부 초기화용 (예: PlayerCastleInteriorBuilder).
        /// private [SerializeField] 필드만 설정하며, 기존 Inspector 직렬화 기본 경로는 그대로 유지됩니다.
        /// </summary>
        /// <param name="territoryId">영지 고유 키 (예: "East_01")</param>
        /// <param name="stationName">표시용 이름 (null/빈 문자열이면 기존 값 유지)</param>
        /// <param name="interactRange">상호작용 반경 (null이면 기존 값 유지)</param>
        public void Configure(string territoryId, string stationName = null, float? interactRange = null)
        {
            if (!string.IsNullOrEmpty(territoryId))
                _territoryId = territoryId;
            if (!string.IsNullOrEmpty(stationName))
                _stationName = stationName;
            if (interactRange.HasValue && interactRange.Value > 0f)
                _interactRange = interactRange.Value;
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