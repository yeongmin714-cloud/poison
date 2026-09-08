using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// 문 상호작용 시스템 (Phase B). E 키 근접 토글로 문을 열고 닫는다.
    /// 지형에 배치된 문 오브젝트에 붙이고 _doorPivot에 문짝(경첩 축) Transform을 연결.
    /// 플레이어 발견/프롬프트/클립드라이버 패턴은 HerbPickup과 동일.
    /// </summary>
    public class DoorInteractable : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private Transform _doorPivot;        // 문짝 회전 축 (경첩 위치)
        [SerializeField] private float _openAngle = 100f;     // 개방 각도 (도). 문 방향이 반대면 음수로 설정 (±)
        [SerializeField] private float _openDuration = 0.8f;  // 개폐 소요 시간 (초)
        [SerializeField] private float _interactRadius = 2.5f;
        [SerializeField] private KeyCode _interactKey = KeyCode.E;

        [Header("UI")]
        [SerializeField] private GameObject _promptPrefab;    // "E 키로 열기/닫기" 안내 (World Space Canvas)

        // 상태
        private bool _isOpen = false;
        private bool _playerNearby = false;
        private GameObject _promptInstance;
        private Transform _player;
        private Coroutine _rotateRoutine;
        private Quaternion _closedLocalRotation; // 닫힘 기준 자세 (초기 회전)

        private void Awake()
        {
            _closedLocalRotation = _doorPivot != null ? _doorPivot.localRotation : Quaternion.identity;
        }

        private void Start()
        {
            // 플레이어 발견 (HerbPickup 패턴)
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (_player == null)
                Debug.LogWarning("[DoorInteractable] Player 태그 오브젝트를 찾을 수 없음");
            if (_doorPivot == null)
                Debug.LogWarning("[DoorInteractable] Door Pivot이 할당되지 않음 — 회전 없이 토글만 동작");
        }

        private void Update()
        {
            if (_player == null) return;

            // 근접 판정
            float dist = Vector3.Distance(transform.position, _player.position);
            _playerNearby = dist <= _interactRadius;

            // 프롬프트 표시/숨김 (HerbPickup 패턴)
            if (_playerNearby && _promptInstance == null)
                ShowPrompt();
            else if (!_playerNearby && _promptInstance != null)
                HidePrompt();

            // 상호작용 키 입력 → 열기/닫기 토글
            if (_playerNearby && Input.GetKeyDown(_interactKey))
                ToggleDoor();
        }

        /// <summary>문 열기/닫기 토글. 토글마다 플레이어 애니 1회 + 회전 코루틴 1회.</summary>
        public void ToggleDoor()
        {
            _isOpen = !_isOpen;

            // ① 플레이어 문 여닫기 애니메이션 (토글마다 1회)
            var clipDriver = _player != null
                ? (_player.GetComponent<HumanoidClipDriver>() ?? _player.GetComponentInChildren<HumanoidClipDriver>())
                : null;
            if (clipDriver != null)
                clipDriver.TriggerOpenDoor();

            // ② 문 회전 코루틴 (회전 중이면 새 목표로 교체)
            if (_rotateRoutine != null)
                StopCoroutine(_rotateRoutine);
            _rotateRoutine = StartCoroutine(RotateDoorRoutine(_isOpen));
        }

        /// <summary>닫힘(초기 자세) ↔ 열림(로컬 Y ±_openAngle)으로 Slerp 회전.</summary>
        private System.Collections.IEnumerator RotateDoorRoutine(bool open)
        {
            if (_doorPivot == null)
            {
                _rotateRoutine = null;
                yield break;
            }

            Quaternion start = _doorPivot.localRotation;
            Quaternion target = open
                ? _closedLocalRotation * Quaternion.Euler(0f, _openAngle, 0f)
                : _closedLocalRotation;

            float elapsed = 0f;
            while (elapsed < _openDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _openDuration);
                _doorPivot.localRotation = Quaternion.Slerp(start, target, t);
                yield return null;
            }
            _doorPivot.localRotation = target;
            _rotateRoutine = null;
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
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, _interactRadius);
        }
    }
}
