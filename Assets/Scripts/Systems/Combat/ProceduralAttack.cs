using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems.Animation.Procedural;
using ProjectName.Systems.Animation.Procedural.Bones;
using ProjectName.Systems.Animation.Procedural.IK;
using UnityEngine.InputSystem;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// 공격 타입 정의
    /// </summary>
    public enum AttackType
    {
        Light,      // 약공격 (빠름, 짧은 리치, 콤보 시작)
        Heavy,      // 강공격 (느림, 긴 리치, 높은 데미지, 슈퍼아머)
        Thrust,     // 찌르기 (중간 속도, 긴 리치, 관통)
        Slash,      // 베기 (광역, 넉백)
        Uppercut,   // 어퍼컷 (공중 띄우기)
        DownStrike, // 내려찍기 (지면 강타, 충격파)
        Dash,       // 돌진 공격
        Aerial,     // 공중 공격
        Counter,    // 카운터/페리
        Special     // 필살기/궁극기
    }

    /// <summary>
    /// 콤보 스텝 정의
    /// </summary>
    [System.Serializable]
    public class ComboStep
    {
        public AttackType attackType = AttackType.Light;
        public string animationTrigger = "Attack";
        public float damage = 10f;
        public float knockback = 2f;
        public float hitStun = 0.3f;
        public float hitStop = 0.05f;      // 히트스탑 시간
        public float cancelWindowStart = 0.4f; // 캔슬 가능 시작 (정규화 0~1)
        public float cancelWindowEnd = 0.8f;   // 캔슬 가능 끝
        public Vector3 hitboxOffset = Vector3.forward * 1f;
        public Vector3 hitboxSize = new Vector3(1f, 1.5f, 1f);
        public LayerMask targetLayers = -1;
        public bool canAirCancel = false;
        public float rootMotionDistance = 0f;  // 전진 거리
    }

    /// <summary>
    /// 콤보 데이터 (ScriptableObject로 관리)
    /// </summary>
    [CreateAssetMenu(fileName = "ComboData", menuName = "ProjectName/Combat/Combo Data")]
    public class ComboData : ScriptableObject
    {
        [Header("Combo Identity")]
        public string comboName = "Basic Combo";
        public AttackType starterType = AttackType.Light;

        [Header("Steps")]
        public List<ComboStep> steps = new List<ComboStep>();

        [Header("Combo Rules")]
        public float maxComboTime = 1.5f;       // 입력 버퍼 시간
        public int maxRepeats = 3;              // 동일 스텝 반복 횟수
        public bool allowDirectionalVariants = true; // 방향키 조합 변형

        [Header("Recovery")]
        public float whiffRecovery = 0.5f;      // 헛쳤을 때 회복 시간
        public float blockRecovery = 0.3f;      // 가드당했을 때 회복 시간
    }

    /// <summary>
    /// 런타임 콤보 상태
    /// </summary>
    public class ComboState
    {
        public ComboData Data { get; private set; }
        public int CurrentStepIndex { get; private set; } = -1;
        public float StepTimer { get; private set; }
        public float InputBufferTimer { get; private set; }
        public bool IsInCombo => CurrentStepIndex >= 0;
        public bool CanCancel => IsInCombo && IsInCancelWindow;
        public bool IsInCancelWindow { get; private set; }
        public AttackType LastAttackType { get; private set; }
        public int RepeatCount { get; private set; }

        public void Initialize(ComboData data)
        {
            Data = data;
            Reset();
        }

        public void Reset()
        {
            CurrentStepIndex = -1;
            StepTimer = 0f;
            InputBufferTimer = 0f;
            LastAttackType = AttackType.Light;
            RepeatCount = 0;
        }

        public bool TryStartCombo(AttackType inputType)
        {
            if (Data == null) return false;

            // 스타터 타입 매칭
            var starter = Data.steps.Find(s => s.attackType == inputType);
            if (starter == null) return false;

            CurrentStepIndex = Data.steps.IndexOf(starter);
            StepTimer = 0f;
            InputBufferTimer = Data.maxComboTime;
            LastAttackType = inputType;
            RepeatCount = 1;
            UpdateCancelWindow();
            return true;
        }

        public bool TryAdvanceCombo(AttackType inputType)
        {
            if (!IsInCombo || Data == null) return false;
            if (!IsInCancelWindow) return false;

            // 다음 스텝 찾기
            int nextIndex = CurrentStepIndex + 1;
            if (nextIndex >= Data.steps.Count)
            {
                // 콤보 종료 -> 스타터로 루프 허용?
                if (Data.allowDirectionalVariants && inputType == LastAttackType && RepeatCount < Data.maxRepeats)
                {
                    // 동일 공격 반복
                    RepeatCount++;
                    StepTimer = 0f;
                    UpdateCancelWindow();
                    return true;
                }
                return false;
            }

            var nextStep = Data.steps[nextIndex];
            if (nextStep.attackType != inputType && !Data.allowDirectionalVariants)
                return false;

            CurrentStepIndex = nextIndex;
            StepTimer = 0f;
            InputBufferTimer = Data.maxComboTime;
            LastAttackType = inputType;
            UpdateCancelWindow();
            return true;
        }

        public void UpdateTimers(float deltaTime)
        {
            if (IsInCombo)
            {
                StepTimer += deltaTime;
                UpdateCancelWindow();

                // 스텝 완료 체크
                var current = GetCurrentStep();
                if (current != null && StepTimer >= 1f) // 애니메이션 길이 기반으로 수정 필요
                {
                    if (CurrentStepIndex + 1 >= Data.steps.Count)
                    {
                        // 콤보 종료
                        Reset();
                    }
                }
            }

            if (InputBufferTimer > 0)
                InputBufferTimer -= deltaTime;
        }

        private void UpdateCancelWindow()
        {
            if (!IsInCombo) return;

            var step = GetCurrentStep();
            if (step == null) { IsInCancelWindow = false; return; }

            float normalizedTime = StepTimer; // 0~1 가정
            IsInCancelWindow = normalizedTime >= step.cancelWindowStart && normalizedTime <= step.cancelWindowEnd;
        }

        public ComboStep GetCurrentStep()
        {
            if (CurrentStepIndex < 0 || CurrentStepIndex >= Data?.steps.Count) return null;
            return Data.steps[CurrentStepIndex];
        }
    }

    /// <summary>
    /// 프로시저럴 공격 실행기
    /// - 상체 IK 스윙/회수
    /// - 히트박스 생성/관리
    /// - 히트스탑/히트스탑/넉백 적용
    /// </summary>
    public class ProceduralAttack : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ProceduralAnimationController _animController;
        [SerializeField] private ProceduralBoneMap _boneMap;

        [Header("Attack Settings")]
        [SerializeField] private float _swingSpeed = 720f;    // deg/s
        [SerializeField] private float _recoverySpeed = 360f; // deg/s
        [SerializeField] private float _hitStopTime = 0.05f;
        [SerializeField] private float _screenShakeIntensity = 0.1f;

        // State
        private ComboState _comboState = new ComboState();
        private ComboData _currentComboData;
        private bool _isAttacking;
        private float _attackTimer;
        private ComboStep _currentStep;
        private Vector3 _swingStartRotation;
        private Vector3 _swingTargetRotation;
        private float _swingProgress;
        private bool _hitRegistered;

        // Hitbox
        private struct ActiveHitbox
        {
            public ComboStep step;
            public Vector3 position;
            public Quaternion rotation;
            public float timer;
            public HashSet<Collider> hitTargets;
        }
        private List<ActiveHitbox> _activeHitboxes = new List<ActiveHitbox>();

        // Events
        public System.Action<ComboStep, Collider> OnHit;
        public System.Action<ComboStep> OnAttackStart;
        public System.Action<ComboStep> OnAttackEnd;
        public System.Action<int> OnComboAdvance; // step index

        // ──────────────────────────────────────────────
        // Unity Lifecycle
        // ──────────────────────────────────────────────

        private void Awake()
        {
            _animController = GetComponent<ProceduralAnimationController>();
            _boneMap = GetComponent<ProceduralBoneMap>();
        }

        private void Update()
        {
            if (_isAttacking)
            {
                UpdateAttackSwing();
                UpdateHitboxes();
            }

            _comboState.UpdateTimers(Time.deltaTime);
        }

        // ──────────────────────────────────────────────
        // 공개 API
        // ──────────────────────────────────────────────

        /// <summary>
        /// 콤보 데이터 설정 및 콤보 시작 시도
        /// </summary>
        public bool TryStartCombo(ComboData comboData, AttackType inputType)
        {
            if (_isAttacking) return false;

            _currentComboData = comboData;
            if (_comboState.TryStartCombo(inputType))
            {
                StartAttack(_comboState.GetCurrentStep());
                return true;
            }
            return false;
        }

        /// <summary>
        /// 콤보 연계 입력
        /// </summary>
        public bool TryAdvanceCombo(AttackType inputType)
        {
            if (!_isAttacking || !_comboState.CanCancel) return false;

            if (_comboState.TryAdvanceCombo(inputType))
            {
                StartAttack(_comboState.GetCurrentStep());
                return true;
            }
            return false;
        }

        public void SetComboData(ComboData data) => _currentComboData = data;

        public ComboState GetComboState() => _comboState;

        // ──────────────────────────────────────────────
        // 공격 실행
        // ──────────────────────────────────────────────

        private void StartAttack(ComboStep step)
        {
            if (step == null) return;

            _currentStep = step;
            _isAttacking = true;
            _attackTimer = 0f;
            _swingProgress = 0f;
            _hitRegistered = false;

            // 상체 목표 회전 계산 (타겟 방향)
            var rHand = _boneMap.Get(BoneRole.R_Hand);
            var rShoulder = _boneMap.Get(BoneRole.R_Shoulder);

            if (rHand != null && rShoulder != null)
            {
                Vector3 toTarget = (_animController.CurrentActionTarget - rHand.position).normalized;
                _swingTargetRotation = Quaternion.LookRotation(toTarget, Vector3.up).eulerAngles;
                _swingStartRotation = rShoulder.rotation.eulerAngles;
            }

            // 히트박스 초기화
            _activeHitboxes.Clear();
            var hb = new ActiveHitbox
            {
                step = _currentStep,
                position = _boneMap.Get(BoneRole.R_Hand).position,
                rotation = _boneMap.Get(BoneRole.R_Hand).rotation,
                timer = 0f,
                hitTargets = new HashSet<Collider>()
            };
            _activeHitboxes.Add(hb);

            OnAttackStart?.Invoke(step);
        }

        private void UpdateAttackSwing()
        {
            if (_currentStep == null) { EndAttack(); return; }

            _attackTimer += Time.deltaTime;

            // 스윙 진행도 (0~1)
            float swingDuration = 0.3f; // 스윙 단계
            float recoveryDuration = 0.4f; // 회수 단계
            float totalDuration = swingDuration + recoveryDuration;

            _attackTimer += Time.deltaTime;
            float normalized = Mathf.Clamp01(_attackTimer / totalDuration);

            if (normalized <= swingDuration / totalDuration)
            {
                // 스윙 단계: 목표 회전으로 보간
                float swingProgress = normalized / (swingDuration / totalDuration);
                _swingProgress = Mathf.SmoothStep(0f, 1f, swingProgress);

                // 상체 회전 적용
                ApplySwingRotation(_swingProgress);
            }
            else
            {
                // 회수 단계: 원래 자세로 복귀
                float recoveryProgress = (normalized - swingDuration / totalDuration) / (recoveryDuration / totalDuration);
                _swingProgress = 1f - Mathf.SmoothStep(0f, 1f, recoveryProgress);
                ApplySwingRotation(_swingProgress);

                if (normalized >= 1f)
                {
                    EndAttack();
                }
            }

            // 히트박스 업데이트 타이머
            if (_currentStep != null && _attackTimer >= _currentStep.hitStop)
            {
                // 첫 프레임에 히트스탑 적용
                if (!_hitRegistered)
                {
                    _hitRegistered = true;
                    // 히트스탑은 별도 처리
                }
            }
        }

        private void ApplySwingRotation(float progress)
        {
            var rShoulder = _boneMap.Get(BoneRole.R_Shoulder);
            if (rShoulder == null) return;

            Vector3 currentEuler = Vector3.Lerp(_swingStartRotation, _swingTargetRotation, progress);
            rShoulder.rotation = Quaternion.Euler(currentEuler);

            // 척추도 약간 따라가게
            var spine1 = _boneMap.Get(BoneRole.Spine1);
            if (spine1 != null)
            {
                spine1.Rotate(Vector3.up, progress * 15f, Space.Self);
            }
        }

        // ──────────────────────────────────────────────
        // 히트박스 관리
        // ──────────────────────────────────────────────

        private void UpdateHitboxes()
        {
            for (int i = _activeHitboxes.Count - 1; i >= 0; i--)
            {
                var hb = _activeHitboxes[i];
                hb.timer += Time.deltaTime;

                // 위치/회전 업데이트 (손 따라가기)
                var rHand = _boneMap.Get(BoneRole.R_Hand);
                if (rHand != null)
                {
                    hb.position = rHand.position + hb.step.hitboxOffset;
                    hb.rotation = rHand.rotation;
                }

                // 충돌 체크
                CheckHitboxCollisions(ref hb);

                _activeHitboxes[i] = hb;

                // 지속 시간 체크
                if (hb.timer > 0.5f) // 최대 0.5초 유지
                    _activeHitboxes.RemoveAt(i);
            }
        }

        private void CheckHitboxCollisions(ref ActiveHitbox hb)
        {
            if (hb.step == null) return;

            Collider[] hits = Physics.OverlapBox(
                hb.position,
                hb.step.hitboxSize * 0.5f,
                hb.rotation,
                hb.step.targetLayers
            );

            foreach (var col in hits)
            {
                if (hb.hitTargets.Contains(col)) continue; // 중복 방지

                // 동일 대상 다중 히트 방지 (멀티히트 공격인 경우 제외)
                if (hb.step.attackType != AttackType.Light || hb.hitTargets.Count == 0)
                {
                    hb.hitTargets.Add(col);

                    // 데미지 적용
                    var damageable = col.GetComponentInParent<Damageable>();
                    if (damageable != null)
                    {
                        var info = new DamageInfo
                        {
                            amount = hb.step.damage,
                            knockback = (col.transform.position - transform.position).normalized * hb.step.knockback,
                            hitStun = hb.step.hitStun
                        };
                        damageable.TakeDamage(info);
                    }

                    // 히트 이펙트
                    OnHit?.Invoke(hb.step, col);

                    // 히트스탑/히트스탑
                    ApplyHitPause(hb.step.hitStop);
                    ApplyHitStop(hb.step.hitStop);

                    // 화면 흔들림
                    if (Camera.main != null)
                        CameraShake.Instance?.Shake(_screenShakeIntensity, _screenShakeDuration);
                }
            }
        }

        // ──────────────────────────────────────────────
        // 히트 페이즈/스탑
        // ──────────────────────────────────────────────

        private float _hitPauseTimer;
        private bool _isHitPaused;

        private void ApplyHitPause(float duration)
        {
            _hitPauseTimer = duration;
            _isHitPaused = true;
            Time.timeScale = 0f;
        }

        private void ApplyHitStop(float duration)
        {
            // 공격자만 정지 (타겟은 별도 처리)
            _hitPauseTimer = duration;
            _isHitPaused = true;
        }

        private void UpdateHitPause()
        {
            if (_isHitPaused)
            {
                _hitPauseTimer -= Time.unscaledDeltaTime;
                if (_hitPauseTimer <= 0)
                {
                    Time.timeScale = 1f;
                    _isHitPaused = false;
                }
            }
        }

        // ──────────────────────────────────────────────
        // 종료/리셋
        // ──────────────────────────────────────────────

        private void EndAttack()
        {
            _isAttacking = false;
            _activeHitboxes.Clear();
            _hitRegistered = false;

            // 상체 복원
            var rShoulder = _boneMap.Get(BoneRole.R_Shoulder);
            if (rShoulder != null)
            {
                rShoulder.localRotation = Quaternion.identity;
            }

            OnAttackEnd?.Invoke(_currentStep);
            _currentStep = null;

            // 콤보 연계 체크
            if (_comboState.IsInCombo && _comboState.InputBufferTimer <= 0)
            {
                _comboState.Reset();
            }
        }

        public void CancelAttack()
        {
            if (_isAttacking)
            {
                EndAttack();
                _comboState.Reset();
            }
        }

        // ──────────────────────────────────────────────
        // 디버그
        // ──────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            if (_currentStep != null && _isAttacking)
            {
                var rHand = _boneMap?.Get(BoneRole.R_Hand);
                if (rHand != null)
                {
                    Gizmos.color = Color.red;
                    Gizmos.matrix = Matrix4x4.TRS(rHand.position + rHand.rotation * _currentStep.hitboxOffset, rHand.rotation, Vector3.one);
                    Gizmos.DrawWireCube(Vector3.zero, _currentStep.hitboxSize);
                }
            }
        }
    }
}
