using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 착석 시스템 (의자/침대). E 키로 착석, 3m 이상 이탈 또는 E 재입력으로 해제.
    /// 침대 모드(_isBed)는 착석 중 6초마다 몸 뒤척기(Toss) 클립 발화.
    /// 의자/침대 오브젝트에 부착. 프롬프트 패턴은 HerbPickup 단순 복사.
    /// </summary>
    public class SeatInteractable : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private bool _isBed = false;              // 침대 모드: 착석 중 주기적으로 Toss
        [SerializeField] private float _interactRadius = 2.2f;     // 착석 상호작용 거리
        [SerializeField] private float _exitDistance = 3f;         // 이 거리 이상 벗어나면 자동 해제
        [SerializeField] private float _tossInterval = 6f;         // 침대 모드: Toss 주기(초)

        [Header("UI")]
        [SerializeField] private GameObject _promptPrefab;         // "E 키로 앉기" 안내 (World Space Canvas)

        // 상태
        private bool _seated = false;
        private bool _playerNearby = false;
        private GameObject _promptInstance;
        private Transform _player;
        private float _tossTimer = 0f;

        // 플레이어 HumanoidClipDriver (발견 체인: self → parent → children)
        private HumanoidClipDriver _clipDriver;

        // Public API
        public bool IsSeated => _seated;
        public bool IsBed => _isBed;

        private void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (_player == null)
                Debug.LogWarning("[SeatInteractable] Player 태그 오브젝트를 찾을 수 없음");
        }

        /// <summary>플레이어 HumanoidClipDriver 발견 (self → InParent → InChildren).</summary>
        private HumanoidClipDriver ResolvePlayerClipDriver()
        {
            if (_player == null) return null;
            return _player.GetComponent<HumanoidClipDriver>()
                ?? _player.GetComponentInParent<HumanoidClipDriver>()
                ?? _player.GetComponentInChildren<HumanoidClipDriver>();
        }

        private void Update()
        {
            if (_player == null) return;

            float dist = Vector3.Distance(transform.position, _player.position);
            _playerNearby = dist <= _interactRadius;

            if (_seated)
            {
                // 침대 모드: _tossInterval마다 몸 뒤척기(Toss)
                if (_isBed)
                {
                    _tossTimer += Time.deltaTime;
                    if (_tossTimer >= _tossInterval)
                    {
                        _tossTimer = 0f;
                        if (_clipDriver != null) _clipDriver.TriggerToss();
                    }
                }

                // 해제 조건: 플레이어가 _exitDistance(3m) 이상 벗어나거나 E 재입력
                if (dist >= _exitDistance || Input.GetKeyDown(KeyCode.E))
                    StandUp();
                return;
            }

            // 프롬프트 표시/숨김 (HerbPickup 패턴)
            if (_playerNearby && _promptInstance == null)
                ShowPrompt();
            else if (!_playerNearby && _promptInstance != null)
                HidePrompt();

            // E 키: 착석
            if (_playerNearby && Input.GetKeyDown(KeyCode.E))
                SitDown();
        }

        private void SitDown()
        {
            _clipDriver = ResolvePlayerClipDriver();
            if (_clipDriver == null)
            {
                Debug.LogWarning("[SeatInteractable] 플레이어 HumanoidClipDriver를 찾을 수 없음");
                return;
            }

            _seated = true;
            _tossTimer = 0f;
            HidePrompt();
            _clipDriver.TriggerSitDown();
            Debug.Log("[SeatInteractable] 🪑 착석 시작" + (_isBed ? " (침대 모드: 6초마다 Toss)" : ""));
        }

        private void StandUp()
        {
            _seated = false;
            _tossTimer = 0f;
            if (_clipDriver != null) _clipDriver.TriggerSitUp();
            _clipDriver = null;
            Debug.Log("[SeatInteractable] 🚶 착석 해제");
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

        /// <summary>비활성화 시 착석 상태 안전 해제.</summary>
        private void OnDisable()
        {
            if (_seated) StandUp();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, _interactRadius);
        }
    }
}
