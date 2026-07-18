using UnityEngine;
using ProjectName.Systems.Animation.Procedural;

namespace ProjectName.Systems
{
    /// <summary>
    /// 부모 Transform의 위치 변화량(델타)으로 속도를 계산하는 IVelocityProvider.
    /// PlayerMovement가 없는 NPC/몬스터 모델 자식에 붙어서 ProceduralAnimationController에 속도 제공.
    /// </summary>
    [RequireComponent(typeof(ProceduralAnimationController))]
    public class ParentVelocityProvider : MonoBehaviour, IVelocityProvider
    {
        private Transform _parent;
        private Vector3 _lastPosition;
        private Vector3 _currentVelocity;
        private float _currentSpeed;
        private bool _initialized;

        // IVelocityProvider 구현
        public Vector3 CurrentVelocity => _currentVelocity;
        public float CurrentSpeed => _currentSpeed;
        public bool IsGrounded => true; // NPC/몬스터는 지면 체크 생략

        private void Awake()
        {
            _parent = transform.parent;
            if (_parent == null)
            {
                Debug.LogWarning("[ParentVelocityProvider] 부모가 없습니다. 비활성화.");
                enabled = false;
                return;
            }
            _lastPosition = _parent.position;
            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized || _parent == null) return;

            Vector3 currentPos = _parent.position;
            Vector3 delta = currentPos - _lastPosition;
            _lastPosition = currentPos;

            // 속도 계산 (m/s)
            _currentVelocity = delta / Mathf.Max(Time.deltaTime, 0.001f);
            _currentSpeed = _currentVelocity.magnitude;
        }
    }
}