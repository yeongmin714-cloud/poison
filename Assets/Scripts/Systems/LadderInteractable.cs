using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 사다리 오르기 시스템. E 키로 Ladder 클립 발화.
    /// NOTE: 현재는 클립 연출만 담당 — 실제 y 이동(등반)은 추후 시스템에서 처리 예정.
    /// 사다리 오브젝트에 부착. 프롬프트/플레이어 발견 패턴은 HerbPickup 준용.
    /// </summary>
    public class LadderInteractable : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private float _interactRadius = 2f;       // 사다리 상호작용 거리

        [Header("UI")]
        [SerializeField] private GameObject _promptPrefab;         // "E 키로 오르기" 안내 (World Space Canvas)

        // 상태
        private bool _playerNearby = false;
        private GameObject _promptInstance;
        private Transform _player;

        // Public API
        public float InteractRadius => _interactRadius;

        private void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (_player == null)
                Debug.LogWarning("[LadderInteractable] Player 태그 오브젝트를 찾을 수 없음");
        }

        private void Update()
        {
            if (_player == null) return;

            float dist = Vector3.Distance(transform.position, _player.position);
            _playerNearby = dist <= _interactRadius;

            // 프롬프트 표시/숨김 (HerbPickup 패턴)
            if (_playerNearby && _promptInstance == null)
                ShowPrompt();
            else if (!_playerNearby && _promptInstance != null)
                HidePrompt();

            // E 키 입력: 사다리 오르기
            if (_playerNearby && Input.GetKeyDown(KeyCode.E))
                ClimbLadder();
        }

        private void ClimbLadder()
        {
            // 플레이어 HumanoidClipDriver 발견 (self → InParent → InChildren)
            var clipDriver = _player.GetComponent<HumanoidClipDriver>()
                ?? _player.GetComponentInParent<HumanoidClipDriver>()
                ?? _player.GetComponentInChildren<HumanoidClipDriver>();
            if (clipDriver == null)
            {
                Debug.LogWarning("[LadderInteractable] 플레이어 HumanoidClipDriver를 찾을 수 없음");
                return;
            }

            // Ladder 클립 연출만 발화. 실제 y 이동(등반 처리)은 추후 시스템 — 주석 명시.
            clipDriver.TriggerLadder();
            Debug.Log("[LadderInteractable] 🪜 사다리 오르기 클립 발화 (실제 y 이동은 추후 시스템)");
        }

        private void ShowPrompt()
        {
            if (_promptPrefab != null)
            {
                _promptInstance = Instantiate(_promptPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity, transform);
            }
        }

        private void HidePrompt()
        {
            if (_promptInstance != null)
            {
                Destroy(_promptInstance);
                _promptInstance = null;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _interactRadius);
        }
    }
}
