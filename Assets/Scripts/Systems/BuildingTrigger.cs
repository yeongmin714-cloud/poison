using ProjectName.Core;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// C10-01: 건물 입구 트리거.
    /// 플레이어 근접 시 E 키 말풍선을 표시하고, IndoorSceneTransition.EnterBuilding()을 호출합니다.
    /// NpcQuestGiver의 말풍선 패턴과 동일한 IMGUI 스타일을 사용합니다.
    /// </summary>
    public class BuildingTrigger : MonoBehaviour
    {
        [Header("Building 설정")]
        [SerializeField] private string _buildingType = "House";

        [Header("트리거 설정")]
        [SerializeField] private float _interactRange = 3f;

        // 상태
        private Transform _player;
        private bool _playerNearby;
        private Camera _mainCamera;

        [Header("건물 추가 설정")]
        [SerializeField] private string _nationStyle;

        /// <summary>건물 유형 (House, Shop, CraftHouse, Church, Castle)</summary>
        public string BuildingType
        {
            get => _buildingType;
            set => _buildingType = value;
        }

        /// <summary>상호작용 범위</summary>
        public float InteractRange
        {
            get => _interactRange;
            set => _interactRange = value;
        }

        /// <summary>국가 스타일 (Castle 전용: Eastern, Western, Southern, Northern, Empire)</summary>
        public string NationStyle
        {
            get => _nationStyle;
            set => _nationStyle = value;
        }

        private void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (_player == null)
                Debug.LogWarning($"[BuildingTrigger] {_buildingType}: Player 태그 오브젝트 없음");

            _mainCamera = Camera.main;
            if (_mainCamera == null)
                Debug.LogWarning("[BuildingTrigger] MainCamera 태그 오브젝트 없음 — 말풍선 표시 불가");
        }

        private void Update()
        {
            if (_player == null) return;

            float dist = Vector3.Distance(transform.position, _player.position);
            _playerNearby = dist <= _interactRange;

            if (_playerNearby && Input.GetKeyDown(KeyCode.E))
            {
                if (string.Equals(_buildingType, "Exit", System.StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log($"[BuildingTrigger] E키 입력 — 출구를 통해 퇴출");
                    BuildingEvents.RequestExitBuilding();
                }
                else
                {
                    Debug.Log($"[BuildingTrigger] E키 입력 — {_buildingType} 진입 (nationStyle: {_nationStyle ?? "null"})");
                    BuildingEvents.RequestEnterBuilding(_buildingType, _nationStyle);
                }
            }
        }

        private void OnGUI()
        {
            if (!_playerNearby || _mainCamera == null) return;

            // 말풍선: 트리거 위치 위에 표시
            Vector3 screenPos = _mainCamera.WorldToScreenPoint(transform.position + Vector3.up * 2.5f);
            if (screenPos.z < 0) return;
            screenPos.y = Screen.height - screenPos.y;

            float bubbleW = 60f;
            float bubbleH = 24f;
            GUI.Box(new Rect(screenPos.x - bubbleW / 2f, screenPos.y - bubbleH - 5, bubbleW, bubbleH), string.Empty);
            GUI.Label(new Rect(screenPos.x - bubbleW / 2f, screenPos.y - bubbleH - 5, bubbleW, bubbleH),
                "💬 E", new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter });
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, _interactRange);
        }
    }
}