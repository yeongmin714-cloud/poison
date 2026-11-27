using UnityEngine;
using ProjectName.Systems.Animation.Procedural;

namespace ProjectName.Systems.Animation
{
    /// <summary>
    /// [125차 후속11] 변위 기반 속도 공급자 — 트랜스폼의 프레임 간 이동을 IVelocityProvider로 변환.
    /// 근거: ProceduralAnimationController는 IVelocityProvider(CurrentVelocity/CurrentSpeed/IsGrounded)로만
    /// 보행을 구동한다(AnimalAI 선례 — SetVelocityProvider(this)). 코드 생성 캐릭터(테스트씬 플레이어/
    /// 아레나 투기장 선수)는 공급자가 없어 T포즈로 고정됐다(아레나 테스트 2 영상 실측 — 플레이어/선수 전원 T포즈).
    /// 추적 트랜스폼의 프레임 간 변위를 실측해 공급 — 이동 중이면 보행, 정지면 대기로 자동 전이.
    /// </summary>
    public class TransformVelocityProvider : MonoBehaviour, IVelocityProvider   // 네임스페이스 레벨 인터페이스(클래스 중첩 아님)
    {
        private Transform _tracked;
        private Vector3 _lastPos;
        private Vector3 _velocity;
        private bool _primed;

        /// <summary>추적 대상 지정(미지정 = 자기 트랜스폼). 부모 루트 이동을 자식 GLB 애니로 전달할 때 루트를 지정.</summary>
        public void Track(Transform target)
        {
            _tracked = target != null ? target : transform;
            _lastPos = _tracked.position;
            _primed = true;
        }

        private void LateUpdate()
        {
            if (_tracked == null)
            {
                _tracked = transform;
                _lastPos = _tracked.position;
                _primed = true;
                return;
            }
            if (!_primed)
            {
                _lastPos = _tracked.position;
                _primed = true;
                _velocity = Vector3.zero;
                return;
            }
            Vector3 delta = _tracked.position - _lastPos;
            _lastPos = _tracked.position;
            _velocity = delta / Mathf.Max(Time.deltaTime, 0.0001f);
        }

        public Vector3 CurrentVelocity => _velocity;
        public float CurrentSpeed => new Vector3(_velocity.x, 0f, _velocity.z).magnitude;

        /// <summary>루트 접지는 이동 시스템(CharacterController/중력)이 보장 — 절차 보행은 지면 가정(단순화).</summary>
        public bool IsGrounded => true;
    }
}
