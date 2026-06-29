using ProjectName.Core;
using ProjectName.Data;

using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 35: 잠긴 문 컴포넌트.
    /// 플레이어가 E키를 누르면 LockpickingUI.Open()을 호출합니다.
    /// 위치 ID, 난이도 설정 가능.
    /// </summary>
    public class LockedDoor : MonoBehaviour
    {
        [Header("자물쇠 설정")]
        [SerializeField] private string _locationId = "door_default";
        [SerializeField] private LockpickingSystem.LockDifficulty _difficulty = LockpickingSystem.LockDifficulty.Easy;

        [Header("트리거 설정")]
        [SerializeField] private float _interactRange = 3f;
        [SerializeField] private bool _isLocked = true;

        // 상태
        private Transform _player;
        private bool _playerNearby;
        private Camera _mainCamera;
        private bool _hasBeenOpened;

        /// <summary>위치 ID (경보 시스템 식별용).</summary>
        public string LocationId
        {
            get => _locationId;
            set => _locationId = value;
        }

        /// <summary>자물쇠 난이도.</summary>
        public LockpickingSystem.LockDifficulty Difficulty
        {
            get => _difficulty;
            set => _difficulty = value;
        }

        /// <summary>잠김 여부.</summary>
        public bool IsLocked
        {
            get => _isLocked;
            set => _isLocked = value;
        }

        /// <summary>이미 열렸는가.</summary>
        public bool HasBeenOpened => _hasBeenOpened;

        private void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (_player == null)
                Debug.LogWarning($"[LockedDoor] {_locationId}: Player 태그 오브젝트 없음");

            _mainCamera = Camera.main;
            if (_mainCamera == null)
                Debug.LogWarning("[LockedDoor] MainCamera 태그 오브젝트 없음 — 말풍선 표시 불가");

            // LockpickingSystem의 세션 종료 이벤트 구독
            LockpickingSystem.OnSessionEnded += OnLockpickingSessionEnded;
        }

        private void OnDestroy()
        {
            LockpickingSystem.OnSessionEnded -= OnLockpickingSessionEnded;
        }

        private void OnLockpickingSessionEnded(LockpickingSystem.LockpickingSession session, bool success)
        {
            if (!success) return;
            if (session.locationId != _locationId) return;

            // 성공 시 문 잠금 해제
            _isLocked = false;
            _hasBeenOpened = true;
            Debug.Log($"[LockedDoor] {_locationId} 문이 열렸습니다!");
        }

        private void Update()
        {
            if (!_isLocked || _hasBeenOpened) return;
            if (_player == null) return;

            float dist = Vector3.Distance(transform.position, _player.position);
            _playerNearby = dist <= _interactRange;

            if (_playerNearby && Input.GetKeyDown(KeyCode.E))
            {
                // 마스터 키 보유 시 스킵
                if (LockpickItem.HasMasterKey())
                {
                    Debug.Log("[LockedDoor] 마스터 키 사용 — 문 즉시 오픈");
                    _isLocked = false;
                    _hasBeenOpened = true;

                    // 마스터 키 소모
                    PlayerInventory.Instance.RemoveItem("lockpick_master_key", 1);

                    // 가짜 세션 생성 (이벤트 발생용)
                    LockpickingSystem.MasterKeyOpen(_difficulty, _locationId);
                    return;
                }

                // LockpickingUI 열기 — 이벤트로 전달 (UI → Systems 직접 참조 방지)
                Debug.Log($"[LockedDoor] E키 입력 — 자물쇠 따기 시작: 위치={_locationId}, 난이도={_difficulty}");
                OnLockpickRequested?.Invoke(_locationId, _difficulty);
            }
        }

        /// <summary>자물쇠 따기 요청 이벤트 (LockpickingUI가 구독)</summary>
        public static event System.Action<string, LockpickingSystem.LockDifficulty> OnLockpickRequested;

        private void OnGUI()
        {
            if (!_isLocked || _hasBeenOpened) return;
            if (!_playerNearby || _mainCamera == null) return;

            // 말풍선: 문 위에 표시
            Vector3 screenPos = _mainCamera.WorldToScreenPoint(transform.position + Vector3.up * 2.5f);
            if (screenPos.z < 0) return;
            screenPos.y = Screen.height - screenPos.y;

            string bubbleText = _isLocked ? "🔒 E" : "🚪 E";

            float bubbleW = 60f;
            float bubbleH = 24f;
            GUI.Box(new Rect(screenPos.x - bubbleW / 2f, screenPos.y - bubbleH - 5, bubbleW, bubbleH), string.Empty);
            GUI.Label(new Rect(screenPos.x - bubbleW / 2f, screenPos.y - bubbleH - 5, bubbleW, bubbleH),
                bubbleText, new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter });
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _isLocked ? Color.red : Color.green;
            Gizmos.DrawWireSphere(transform.position, _interactRange);

            // 난이도별 색상 Gizmo
            switch (_difficulty)
            {
                case LockpickingSystem.LockDifficulty.Easy:
                    Gizmos.color = Color.green;
                    break;
                case LockpickingSystem.LockDifficulty.Medium:
                    Gizmos.color = Color.yellow;
                    break;
                case LockpickingSystem.LockDifficulty.Hard:
                    Gizmos.color = new Color(1f, 0.5f, 0f);
                    break;
                case LockpickingSystem.LockDifficulty.VeryHard:
                    Gizmos.color = Color.red;
                    break;
                case LockpickingSystem.LockDifficulty.Legendary:
                    Gizmos.color = Color.magenta;
                    break;
            }
            Gizmos.DrawWireCube(transform.position + Vector3.up * 1.5f, new Vector3(1f, 2.5f, 0.1f));
        }
    }
}