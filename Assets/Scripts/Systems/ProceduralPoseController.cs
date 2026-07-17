using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 캐릭터 본에 프로시저럴 포즈 보정을 적용합니다.
    /// LateUpdate에서 Animator가 애니메이션을 업데이트한 직후 동작하여
    /// 이동 시 상체 기울임(Lean), 달리기 상하 Bob, 점프 시 구부림(Jump Crouch)을 구현합니다.
    /// AnimationRigging 패키지 의존성 없이 순수 Transform 보간으로 동작합니다.
    /// </summary>
    public class ProceduralPoseController : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        //  Public tuning parameters (SerializeField)
        // ──────────────────────────────────────────────

        [Header("Effect Amount (master sliders)")]
        [SerializeField, Range(0f, 1f)] private float _leanAmount = 1f;
        [SerializeField, Range(0f, 1f)] private float _bobAmount = 1f;
        [SerializeField, Range(0f, 1f)] private float _jumpCrouchAmount = 1f;

        [Header("Lean Settings")]
        [SerializeField] private float _maxLeanPitch = 15f;       // 전진 시 X축 최대 피치 (도)
        [SerializeField] private float _maxLeanRoll = 8f;         // 좌우 시 Z축 최대 롤 (도)
        [SerializeField] private float _leanSmoothTime = 0.15f;

        [Header("Run Bob Settings")]
        [SerializeField] private float _bobFrequency = 9f;
        [SerializeField] private float _bobAmplitude = 0.08f;
        [SerializeField] private float _bobSmoothTime = 0.1f;

        [Header("Jump Crouch Settings")]
        [SerializeField] private float _jumpCrouchSmoothTime = 0.2f;
        [SerializeField] private float _jumpThighAngle = 30f;    // 허벅지 X축 회전 (도)
        [SerializeField] private float _jumpShinAngle = -25f;    // 종아리 X축 회전 (도)
        [SerializeField] private float _jumpForearmAngle = -40f; // 팔뚝 X축 회전 (도)

        // ──────────────────────────────────────────────
        //  Private fields — Animator
        // ──────────────────────────────────────────────

        private Animator _animator;

        // ──────────────────────────────────────────────
        //  Private fields — Bone caches
        // ──────────────────────────────────────────────

        // Individual bone references with initial pose snapshots
        private Transform _topSpineBone;        // 최상위 spine = 상체 기울기
        private Transform _rootSpineBone;       // 최하위 spine = 전신 Bob
        private Quaternion _topSpineInitRot;
        private Vector3 _topSpineInitPos;
        private Quaternion _rootSpineInitRot;
        private Vector3 _rootSpineInitPos;

        private Transform _leftThigh;
        private Transform _rightThigh;
        private Quaternion _leftThighInitRot;
        private Quaternion _rightThighInitRot;

        private Transform _leftShin;
        private Transform _rightShin;
        private Quaternion _leftShinInitRot;
        private Quaternion _rightShinInitRot;

        private Transform _leftForearm;
        private Transform _rightForearm;
        private Quaternion _leftForearmInitRot;
        private Quaternion _rightForearmInitRot;

        // ──────────────────────────────────────────────
        //  Private fields — Smoothing state
        // ──────────────────────────────────────────────

        // Lean
        private float _currentLeanPitch;
        private float _currentLeanRoll;
        private float _leanPitchVelocity;
        private float _leanRollVelocity;

        // Bob
        private float _currentBobY;
        private float _bobYVelocity;

        // Jump crouch
        private float _currentJumpThigh;
        private float _currentJumpShin;
        private float _currentJumpForearm;

        // ──────────────────────────────────────────────
        //  Private fields — Runtime state
        // ──────────────────────────────────────────────

        private float _speed;
        private bool _isJumping;
        private Vector3 _lastPosition;
        private Vector3 _velocity;

        // ──────────────────────────────────────────────
        //  Unity lifecycle
        // ──────────────────────────────────────────────

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
            if (_animator == null)
            {
                Debug.LogWarning("[ProceduralPoseController] Animator not found in children. Disabling.");
                enabled = false;
                return;
            }

            CacheBones();
            _lastPosition = transform.position;
        }

        private void LateUpdate()
        {
            if (_animator == null || !enabled)
                return;

            // 1. 현재 상태 계산 (속도, 점프 여부, 속도 벡터)
            UpdateState();

            // 2. 모든 본을 캐시된 초기 포즈로 리셋 (프레임 간 누적 방지)
            ResetBonesToInitial();

            // 3. 각 보정 효과 계산 및 적용
            ApplyLean();
            ApplyBob();
            ApplyJumpCrouch();
        }

        // ──────────────────────────────────────────────
        //  Bone discovery
        // ──────────────────────────────────────────────

        /// <summary>
        /// Animator 하위의 모든 Transform을 순회하며 본 이름으로 동적 탐색하여 캐시합니다.
        /// </summary>
        private void CacheBones()
        {
            Transform[] allTransforms = _animator.GetComponentsInChildren<Transform>(true);
            Transform rootTransform = _animator.transform;

            // spine 본들 — 계층 깊이 순으로 정렬
            Transform lowestSpine = null;   // hips에 가까움
            int lowestSpineDepth = int.MaxValue;
            Transform highestSpine = null;  // head에 가까움
            int highestSpineDepth = -1;

            foreach (Transform t in allTransforms)
            {
                if (t == rootTransform)
                    continue;

                string lowerName = t.name.ToLowerInvariant();

                // ── Spine ──
                if (lowerName.Contains("spine"))
                {
                    int depth = GetHierarchyDepth(t);
                    if (depth < lowestSpineDepth)
                    {
                        lowestSpineDepth = depth;
                        lowestSpine = t;
                    }
                    if (depth > highestSpineDepth)
                    {
                        highestSpineDepth = depth;
                        highestSpine = t;
                    }
                    continue;
                }

                // ── Thigh ──
                bool isThigh = lowerName.Contains("thigh")
                            || lowerName.Contains("upper_leg")
                            || lowerName.Contains("upleg")
                            || lowerName.Contains("upperleg");
                if (isThigh)
                {
                    if (IsLeftBone(t.name))
                    {
                        _leftThigh = t;
                        _leftThighInitRot = t.localRotation;
                    }
                    else if (IsRightBone(t.name))
                    {
                        _rightThigh = t;
                        _rightThighInitRot = t.localRotation;
                    }
                    continue;
                }

                // ── Shin / Calf ── (thigh가 아닌 것만)
                bool isShin = !isThigh
                           && (lowerName.Contains("shin")
                            || lowerName.Contains("calf")
                            || lowerName.Contains("leg"));
                if (isShin)
                {
                    if (IsLeftBone(t.name))
                    {
                        _leftShin = t;
                        _leftShinInitRot = t.localRotation;
                    }
                    else if (IsRightBone(t.name))
                    {
                        _rightShin = t;
                        _rightShinInitRot = t.localRotation;
                    }
                    continue;
                }

                // ── Forearm ──
                bool isForearm = lowerName.Contains("forearm")
                              || lowerName.Contains("lower_arm")
                              || lowerName.Contains("elbow");
                if (isForearm)
                {
                    if (IsLeftBone(t.name))
                    {
                        _leftForearm = t;
                        _leftForearmInitRot = t.localRotation;
                    }
                    else if (IsRightBone(t.name))
                    {
                        _rightForearm = t;
                        _rightForearmInitRot = t.localRotation;
                    }
                }
            }

            // Spine 참조 저장
            _rootSpineBone = lowestSpine;
            _topSpineBone = highestSpine;

            if (_rootSpineBone != null)
            {
                _rootSpineInitRot = _rootSpineBone.localRotation;
                _rootSpineInitPos = _rootSpineBone.localPosition;
            }
            if (_topSpineBone != null)
            {
                _topSpineInitRot = _topSpineBone.localRotation;
                _topSpineInitPos = _topSpineBone.localPosition;
            }

            // 디버그 로그
            if (_topSpineBone == null)
                Debug.LogWarning("[ProceduralPoseController] No spine bone found. Lean disabled.");
            if (_leftThigh == null || _rightThigh == null)
                Debug.LogWarning("[ProceduralPoseController] Thigh bones not found. Jump crouch (thigh) disabled.");
            if (_leftForearm == null || _rightForearm == null)
                Debug.LogWarning("[ProceduralPoseController] Forearm bones not found. Jump crouch (arm) disabled.");
        }

        /// <summary>
        /// Animator root로부터의 Transform 계층 깊이를 반환합니다.
        /// </summary>
        private int GetHierarchyDepth(Transform t)
        {
            int depth = 0;
            while (t.parent != null && t.parent != _animator.transform)
            {
                depth++;
                t = t.parent;
            }
            return depth;
        }

        /// <summary>
        /// 본 이름으로 왼쪽/오른쪽 판별 (Blender ".L"/".R" 접미사 포함).
        /// </summary>
        private static bool IsLeftBone(string name)
        {
            return name.Contains(".L") || name.Contains("_L") || name.Contains("Left");
        }

        private static bool IsRightBone(string name)
        {
            return name.Contains(".R") || name.Contains("_R") || name.Contains("Right");
        }

        // ──────────────────────────────────────────────
        //  State update
        // ──────────────────────────────────────────────

        private void UpdateState()
        {
            // Speed 파라미터 읽기 (RigAnimationController가 "Speed" float 설정)
            // 파라미터가 없으면 에러 방지를 위해 0 반환
            if (_animator.isActiveAndEnabled && _animator.runtimeAnimatorController != null)
            {
                if (HasParameter(_animator, "Speed"))
                {
                    _speed = _animator.GetFloat("Speed");
                }
                else
                {
                    _speed = 0f;
                }
            }

            // 속도 벡터 계산 (프레임 간 위치 변화)
            _velocity = (transform.position - _lastPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
            _lastPosition = transform.position;

            // 점프 판단: 수직 속도가 양수(상승 중)이거나 Animator가 Jump 상태
            bool animatorInJump = false;
            if (_animator.isActiveAndEnabled)
            {
                AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
                animatorInJump = stateInfo.IsName("Jump");
            }

            _isJumping = (_velocity.y > 0.15f) || animatorInJump;
        }

        /// <summary>
        /// Animator에 특정 파라미터가 존재하는지 확인합니다.
        /// </summary>
        private bool HasParameter(Animator animator, string paramName)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return false;

            foreach (var param in animator.parameters)
            {
                if (param.name == paramName)
                    return true;
            }
            return false;
        }

        // ──────────────────────────────────────────────
        //  Bone reset
        // ──────────────────────────────────────────────

        /// <summary>
        /// 모든 캐시된 본을 초기 로컬 회전/위치로 리셋하여 프레임 간 누적을 방지합니다.
        /// </summary>
        private void ResetBonesToInitial()
        {
            if (_rootSpineBone != null)
            {
                _rootSpineBone.localRotation = _rootSpineInitRot;
                _rootSpineBone.localPosition = _rootSpineInitPos;
            }
            if (_topSpineBone != null && _topSpineBone != _rootSpineBone)
            {
                _topSpineBone.localRotation = _topSpineInitRot;
                _topSpineBone.localPosition = _topSpineInitPos;
            }

            if (_leftThigh != null)
                _leftThigh.localRotation = _leftThighInitRot;
            if (_rightThigh != null)
                _rightThigh.localRotation = _rightThighInitRot;
            if (_leftShin != null)
                _leftShin.localRotation = _leftShinInitRot;
            if (_rightShin != null)
                _rightShin.localRotation = _rightShinInitRot;
            if (_leftForearm != null)
                _leftForearm.localRotation = _leftForearmInitRot;
            if (_rightForearm != null)
                _rightForearm.localRotation = _rightForearmInitRot;
        }

        // ──────────────────────────────────────────────
        //  Effect A: Lean (상체 기울임)
        // ──────────────────────────────────────────────

        private void ApplyLean()
        {
            if (_topSpineBone == null || _leanAmount <= 0f)
                return;

            // Speed → lean intensity (Speed 0=Idle, 0.5=Walk, 1.0=Run)
            float leanIntensity = Mathf.Clamp01(_speed);

            // 전진/후진 피치 (X축 회전)
            float targetPitch = leanIntensity * _maxLeanPitch;

            // 좌우 롤 (Z축 회전) — 로컬 속도 X 성분 기반
            Vector3 localVelocity = transform.InverseTransformDirection(_velocity);
            float lateralFactor = Mathf.Clamp(localVelocity.x / 5f, -1f, 1f);
            float targetRoll = -lateralFactor * _maxLeanRoll;

            // SmoothDamp 보간
            _currentLeanPitch = Mathf.SmoothDamp(_currentLeanPitch, targetPitch * _leanAmount,
                ref _leanPitchVelocity, _leanSmoothTime);
            _currentLeanRoll = Mathf.SmoothDamp(_currentLeanRoll, targetRoll * _leanAmount,
                ref _leanRollVelocity, _leanSmoothTime);

            // 최상위 spine에 로컬 회전 오프셋 적용
            _topSpineBone.localRotation = _topSpineInitRot
                * Quaternion.Euler(_currentLeanPitch, 0f, _currentLeanRoll);
        }

        // ──────────────────────────────────────────────
        //  Effect B: Run Bob (달리기 상하 바운스)
        // ──────────────────────────────────────────────

        private void ApplyBob()
        {
            if (_rootSpineBone == null || _bobAmount <= 0f)
                return;

            // 점프 중에는 Bob 중단
            if (_isJumping)
            {
                _currentBobY = Mathf.SmoothDamp(_currentBobY, 0f,
                    ref _bobYVelocity, _bobSmoothTime);
            }
            // 달리기(Speed > 0.5)일 때만 Bob 활성
            else if (_speed > 0.5f)
            {
                float bobValue = Mathf.Sin(Time.time * _bobFrequency)
                    * _bobAmplitude * _bobAmount;
                _currentBobY = Mathf.SmoothDamp(_currentBobY, bobValue,
                    ref _bobYVelocity, _bobSmoothTime);
            }
            else
            {
                _currentBobY = Mathf.SmoothDamp(_currentBobY, 0f,
                    ref _bobYVelocity, _bobSmoothTime);
            }

            // 최하위 spine(Root에 가까움)에 Y 위치 오프셋 → 전신 바운스
            _rootSpineBone.localPosition = _rootSpineInitPos
                + new Vector3(0f, _currentBobY, 0f);
        }

        // ──────────────────────────────────────────────
        //  Effect C: Jump Crouch (점프 시 구부림)
        // ──────────────────────────────────────────────

        private void ApplyJumpCrouch()
        {
            if (_jumpCrouchAmount <= 0f)
                return;

            // 목표 각도 계산
            float targetThigh = _isJumping ? _jumpThighAngle * _jumpCrouchAmount : 0f;
            float targetShin = _isJumping ? _jumpShinAngle * _jumpCrouchAmount : 0f;
            float targetForearm = _isJumping ? _jumpForearmAngle * _jumpCrouchAmount : 0f;

            // 부드러운 Lerp 보간 (framerate-independent)
            float smoothFactor = 1f - Mathf.Exp(-Time.deltaTime / _jumpCrouchSmoothTime);

            _currentJumpThigh = Mathf.Lerp(_currentJumpThigh, targetThigh, smoothFactor);
            _currentJumpShin = Mathf.Lerp(_currentJumpShin, targetShin, smoothFactor);
            _currentJumpForearm = Mathf.Lerp(_currentJumpForearm, targetForearm, smoothFactor);

            // ── Thigh (X축 +회전 = 무릎 올리기) ──
            if (_leftThigh != null)
                _leftThigh.localRotation = _leftThighInitRot
                    * Quaternion.Euler(_currentJumpThigh, 0f, 0f);
            if (_rightThigh != null)
                _rightThigh.localRotation = _rightThighInitRot
                    * Quaternion.Euler(_currentJumpThigh, 0f, 0f);

            // ── Shin (X축 -회전 = 종아리 접기) ──
            if (_leftShin != null)
                _leftShin.localRotation = _leftShinInitRot
                    * Quaternion.Euler(_currentJumpShin, 0f, 0f);
            if (_rightShin != null)
                _rightShin.localRotation = _rightShinInitRot
                    * Quaternion.Euler(_currentJumpShin, 0f, 0f);

            // ── Forearm (X축 -회전 = 팔을 앞으로) ──
            if (_leftForearm != null)
                _leftForearm.localRotation = _leftForearmInitRot
                    * Quaternion.Euler(_currentJumpForearm, 0f, 0f);
            if (_rightForearm != null)
                _rightForearm.localRotation = _rightForearmInitRot
                    * Quaternion.Euler(_currentJumpForearm, 0f, 0f);
        }
    }
}
