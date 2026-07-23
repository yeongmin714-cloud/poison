using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace ProjectName.Systems.Animation.Procedural.Locomotion.Quadruped.Extensions
{
    // ─────────────────────────────────────────────────────────────
    // Large Monster Gait Selector
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 대형 몬스터 전용 보행 선택기.
    /// 느리고 무거운 걷기(Walk), 느린 트롯(Trot), 충격적인 갤럽(Gallop)만 사용.
    /// Pace는 제거하고 Stomp(발 구르기) 모드 추가.
    /// </summary>
    [BurstCompile]
    public struct LargeMonsterGaitSelectorJob : IJob
    {
        public enum LargeGait { Walk, Trot, Stomp, Gallop }

        [ReadOnly] public float CurrentSpeed;
        [ReadOnly] public float MaxSpeed;
        [ReadOnly] public float WalkSpeed;
        [ReadOnly] public float TrotSpeed;
        [ReadOnly] public float StompSpeed;
        [ReadOnly] public float GallopSpeed;
        [ReadOnly] public LargeGait CurrentGait;
        [ReadOnly] public float StompTrigger; // 0-1, 외부에서 설정 (예: 공격 타이밍)

        public NativeArray<LargeGait> OutSelectedGait;
        public NativeArray<float> OutGaitBlend;
        public NativeArray<float> OutPhaseSpeedMultiplier;
        public NativeArray<float> OutStompWeight; // 0-1, 발 구르기 강도

        public void Execute()
        {
            float normalizedSpeed = CurrentSpeed / math.max(MaxSpeed, 0.01f);
            LargeGait targetGait = CurrentGait;
            float blend = 0f;
            float stompWeight = StompTrigger;

            if (stompWeight > 0.5f && normalizedSpeed < 0.3f)
            {
                targetGait = LargeGait.Stomp;
            }
            else if (normalizedSpeed < 0.2f)
                targetGait = LargeGait.Walk;
            else if (normalizedSpeed < 0.45f)
                targetGait = LargeGait.Trot;
            else
                targetGait = LargeGait.Gallop;

            // 부드러운 전환
            if (targetGait != CurrentGait)
                blend = 0.5f;
            else
                blend = 1.0f;

            // 대형 몬스터 보행 속도: 느리지만 힘 있게
            float phaseMult = 1.0f;
            switch (targetGait)
            {
                case LargeGait.Walk:   phaseMult = 0.5f; break;
                case LargeGait.Trot:   phaseMult = 0.8f; break;
                case LargeGait.Stomp:  phaseMult = 0.3f; break;
                case LargeGait.Gallop: phaseMult = 1.2f; break;
            }

            OutSelectedGait[0] = targetGait;
            OutGaitBlend[0] = blend;
            OutPhaseSpeedMultiplier[0] = phaseMult;
            OutStompWeight[0] = stompWeight;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Large Monster Body Posture
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 대형 몬스터의 낮은 무게중심, 넓은 자세, 무거운 착지 효과를 계산.
    /// </summary>
    [BurstCompile]
    public struct LargeMonsterBodyPostureJob : IJob
    {
        [ReadOnly] public float3 BaseBodyPosition;
        [ReadOnly] public quaternion BaseBodyRotation;
        [ReadOnly] public float3 BodyVelocity;
        [ReadOnly] public float3 BodyAngularVelocity;
        [ReadOnly] public float SizeScale; // 1.0 = 기본, 2.0 = 2배
        [ReadOnly] public float MassMultiplier; // 1.0 = 기본, 2.0 = 2배
        [ReadOnly] public float StompWeight;
        [ReadOnly] public float Time;

        public NativeArray<float3> OutBodyPosition;
        public NativeArray<quaternion> OutBodyRotation;
        public NativeArray<float> OutGroundShakeMagnitude;
        public NativeArray<float> OutStanceWidthMultiplier; // 다리 벌림 배율

        public void Execute()
        {
            // 1. 무게중심 낮추기
            float centerOfMassOffset = -0.15f * SizeScale; // 기준 대비 낮춤
            float3 bodyPos = BaseBodyPosition;
            bodyPos.y += centerOfMassOffset;

            // 2. 속도에 따른 관성 하강: 빠를수록 더 낮게
            float speed = math.length(BodyVelocity);
            float speedDrop = -speed * 0.02f * SizeScale;
            bodyPos.y += speedDrop;

            // 3. 자세: 넓은 스탠스
            float stanceWidth = 1.0f + (SizeScale - 1.0f) * 0.3f; // 크기 비례 넓어짐

            // 4. 몸통 흔들림 (무거운 질량 효과)
            // 질량이 클수록 회전 관성 증가 → 각속도 감쇠
            float massDamping = 1.0f / math.max(MassMultiplier, 0.1f);
            float3 angularDamped = BodyAngularVelocity * massDamping;
            quaternion rotation = BaseBodyRotation;

            // 5. Stomp 시 지면 충격
            float groundShake = 0f;
            if (StompWeight > 0.1f)
            {
                // Stomp 중 충격파: 사인파 기반
                float stompPhase = Time * 3.0f;
                float shake = math.sin(stompPhase) * 0.5f + 0.5f; // 0-1
                groundShake = shake * StompWeight * 0.3f * SizeScale;

                // Stomp 시 몸통이 약간 올라갔다 내려옴
                float bounceOffset = math.sin(stompPhase * 2.0f) * 0.05f * StompWeight * SizeScale;
                bodyPos.y += bounceOffset;
            }

            // 6. 착지 충격 시뮬레이션 (수직 속도가 음수일 때)
            float verticalVelocity = BodyVelocity.y;
            if (verticalVelocity < -0.5f)
            {
                float impactForce = -verticalVelocity * 0.02f * SizeScale * MassMultiplier;
                groundShake = math.max(groundShake, impactForce);
                // 착지 시 약간 수축
                bodyPos.y += -impactForce * 0.1f;
            }

            OutBodyPosition[0] = bodyPos;
            OutBodyRotation[0] = rotation;
            OutGroundShakeMagnitude[0] = math.clamp(groundShake, 0f, 1f);
            OutStanceWidthMultiplier[0] = stanceWidth;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Large Monster Leg Phase (modified duty cycle)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 대형 몬스터의 넓은 듀티 사이클과 무거운 발놀림 패턴.
    /// 듀티 사이클이 더 길어서(0.65-0.75) 지면 접촉 시간이 길다.
    /// </summary>
    [BurstCompile]
    public struct LargeMonsterLegPhaseJob : IJob
    {
        public enum Leg { LF, RF, LH, RH }

        [ReadOnly] public LargeMonsterGaitSelectorJob.LargeGait CurrentGait;
        [ReadOnly] public float BasePhase;
        [ReadOnly] public float BaseDutyCycle; // 기본 0.6, 대형은 더 길게
        [ReadOnly] public float StompWeight;

        public NativeArray<float> OutLegPhases; // 4
        public NativeArray<float> OutLegDutyCycles; // 4
        public NativeArray<float> OutGroundImpactWeights; // 4, 착지 충격 가중치

        public void Execute()
        {
            float lf = 0f, rf = 0f, lh = 0f, rh = 0f;
            float dutyLf, dutyRf, dutyLh, dutyRh;
            float impactLf, impactRf, impactLh, impactRh;

            // 듀티 사이클: 대형 몬스터는 0.65-0.75 (기본 0.55-0.6)
            float dutyCycle = math.lerp(BaseDutyCycle, 0.75f, 0.5f);
            float stompDuty = 0.8f; // Stomp 시 더 긴 접촉

            switch (CurrentGait)
            {
                case LargeMonsterGaitSelectorJob.LargeGait.Walk:
                    // 4-beat: LF, RH, RF, LH (0, 0.25, 0.5, 0.75)
                    lf = 0f;
                    rh = 0.25f;
                    rf = 0.5f;
                    lh = 0.75f;
                    dutyLf = dutyCycle;
                    dutyRf = dutyCycle;
                    dutyLh = dutyCycle;
                    dutyRh = dutyCycle;
                    break;

                case LargeMonsterGaitSelectorJob.LargeGait.Trot:
                    // 2-beat diagonal: LF+RH, RF+LH
                    lf = 0f;
                    rh = 0f;
                    rf = 0.5f;
                    lh = 0.5f;
                    dutyLf = dutyCycle + 0.03f;
                    dutyRf = dutyCycle + 0.03f;
                    dutyLh = dutyCycle + 0.03f;
                    dutyRh = dutyCycle + 0.03f;
                    break;

                case LargeMonsterGaitSelectorJob.LargeGait.Stomp:
                    // Stomp: 모든 발을 순차적으로 구름
                    lf = 0f;
                    rh = 0.15f;
                    rf = 0.3f;
                    lh = 0.45f;
                    dutyLf = stompDuty;
                    dutyRf = stompDuty;
                    dutyLh = stompDuty;
                    dutyRh = stompDuty;
                    break;

                case LargeMonsterGaitSelectorJob.LargeGait.Gallop:
                    // 4-beat asymmetric (rotary): LH, RH, LF, RF
                    lh = 0f;
                    rh = 0.2f;
                    lf = 0.5f;
                    rf = 0.7f;
                    dutyLf = dutyCycle - 0.05f; // 갤럽은 약간 짧게
                    dutyRf = dutyCycle - 0.05f;
                    dutyLh = dutyCycle - 0.05f;
                    dutyRh = dutyCycle - 0.05f;
                    break;

                default:
                    dutyLf = dutyCycle;
                    dutyRf = dutyCycle;
                    dutyLh = dutyCycle;
                    dutyRh = dutyCycle;
                    break;
            }

            // Stomp 가중치가 있을 때 충격 강도 계산
            float baseImpact = 0.3f;
            impactLf = baseImpact + (lf < BasePhase + 0.05f ? StompWeight * 0.7f : 0f);
            impactRf = baseImpact + (rf < BasePhase + 0.05f ? StompWeight * 0.7f : 0f);
            impactLh = baseImpact + (lh < BasePhase + 0.05f ? StompWeight * 0.7f : 0f);
            impactRh = baseImpact + (rh < BasePhase + 0.05f ? StompWeight * 0.7f : 0f);

            // BasePhase 적용
            float phaseMod = math.fmod(BasePhase, 1f);
            OutLegPhases[0] = math.fmod(lf + phaseMod, 1f);
            OutLegPhases[1] = math.fmod(rf + phaseMod, 1f);
            OutLegPhases[2] = math.fmod(lh + phaseMod, 1f);
            OutLegPhases[3] = math.fmod(rh + phaseMod, 1f);

            OutLegDutyCycles[0] = dutyLf;
            OutLegDutyCycles[1] = dutyRf;
            OutLegDutyCycles[2] = dutyLh;
            OutLegDutyCycles[3] = dutyRh;

            OutGroundImpactWeights[0] = math.clamp(impactLf, 0f, 1f);
            OutGroundImpactWeights[1] = math.clamp(impactRf, 0f, 1f);
            OutGroundImpactWeights[2] = math.clamp(impactLh, 0f, 1f);
            OutGroundImpactWeights[3] = math.clamp(impactRh, 0f, 1f);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Large Monster Foot Target (heavier landing)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 대형 몬스터의 발목표 계산.
    /// 더 무거운 착지, 더 큰 발걸음, 더 넓은 스탠스.
    /// </summary>
    [BurstCompile]
    public struct LargeMonsterFootTargetJob : IJob
    {
        [ReadOnly] public float3 BodyPosition;
        [ReadOnly] public quaternion BodyRotation;
        [ReadOnly] public float3 BodyVelocity;
        [ReadOnly] public float LegPhase;
        [ReadOnly] public float DutyCycle;
        [ReadOnly] public float StepLength;
        [ReadOnly] public float StepHeight;
        [ReadOnly] public float3 LegDefaultPos;
        [ReadOnly] public bool IsFrontLeg;
        [ReadOnly] public float StanceWidthMultiplier;
        [ReadOnly] public float GroundImpactWeight;

        public NativeArray<float3> OutFootTarget;
        public NativeArray<float3> OutFootHint;
        public NativeArray<bool> OutIsStance;
        public NativeArray<float> OutLandingForce; // 카메라 쉐이크 등에 사용

        public void Execute()
        {
            bool stance = LegPhase < DutyCycle;
            OutIsStance[0] = stance;

            float3 forward = math.mul(BodyRotation, math.forward());
            float3 right = math.mul(BodyRotation, math.right());
            float3 up = math.up();

            // 넓은 스탠스: LegDefaultPos의 x성분에 배율 적용
            float3 adjustedDefault = LegDefaultPos;
            adjustedDefault.x *= StanceWidthMultiplier;

            float3 defaultWorld = BodyPosition + math.mul(BodyRotation, adjustedDefault);

            float landingForce = 0f;

            if (stance)
            {
                // 스탠스: 발은 고정
                OutFootTarget[0] = defaultWorld;
                OutFootHint[0] = defaultWorld + right * (IsFrontLeg ? 0.2f : -0.2f) + up * 0.4f;
            }
            else
            {
                // 스윙: 더 높고 더 넓은 호
                float swingProgress = (LegPhase - DutyCycle) / (1f - DutyCycle);
                float height = math.sin(swingProgress * math.PI) * StepHeight * 1.3f; // 더 높게
                float forwardDist = swingProgress * StepLength * math.max(0.5f, math.length(BodyVelocity) / 3f);

                // 대형 몬스터: 더 무거운 착지 시뮬레이션
                // 스윙 후반에 발이 빠르게 내려옴
                float landingCompression = 1f;
                if (swingProgress > 0.7f)
                {
                    // 스윙 후반 30%에서 급격히 하강
                    float lateProgress = (swingProgress - 0.7f) / 0.3f;
                    landingCompression = 1f - math.pow(lateProgress, 2f) * 0.3f;
                    height *= landingCompression;

                    // 착지 충격력 계산
                    landingForce = lateProgress * GroundImpactWeight * 0.5f;
                }

                float3 target = defaultWorld + forward * forwardDist + up * height;
                OutFootTarget[0] = target;
                OutFootHint[0] = target + right * (IsFrontLeg ? 0.25f : -0.25f) * StanceWidthMultiplier;
            }

            OutLandingForce[0] = math.clamp(landingForce, 0f, 1f);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Large Monster Ground Shake
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 대형 몬스터 착지 시 지면 흔들림 효과.
    /// MonoBehaviour에서 카메라 쉐이크나 파티클 트리거 용도.
    /// </summary>
    public struct LargeMonsterShakeData
    {
        public float Magnitude;     // 0-1
        public float3 Origin;       // 월드 좌표
        public float Radius;        // 영향 반경
        public float Duration;      // 지속 시간 (초)
        public float Elapsed;       // 경과 시간
        public bool IsActive;       // 활성 여부
    }

    /// <summary>
    /// 대형 몬스터용 MonoBehaviour 확장 포인트.
    /// 이 컴포넌트를 대형 몬스터 프리팹에 추가.
    /// </summary>
    [Obsolete("Use NeuralAnimationController with ONNX policies instead. See MIGRATION_GUIDE_PHASE46.md", false)]
    public class QuadrupedLargeMonster : MonoBehaviour
    {
        [Header("크기 & 질량")]
        [SerializeField] private float _sizeScale = 1.5f;
        [SerializeField] private float _massMultiplier = 3.0f;

        [Header("보행 파라미터")]
        [SerializeField] private float _baseDutyCycle = 0.65f;
        [SerializeField] private float _stepLengthMultiplier = 1.4f;
        [SerializeField] private float _stepHeightMultiplier = 1.3f;

        [Header("지면 충격")]
        [SerializeField] private float _shakeRadius = 10f;
        [SerializeField] private float _shakeDuration = 0.5f;
        [SerializeField] private AnimationCurve _shakeFalloff = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [Header("참조")]
        [SerializeField] private Transform _bodyTransform;

        // 현재 상태
        private LargeMonsterShakeData _currentShake;
        private System.Action<LargeMonsterShakeData> _onGroundShake;

        /// <summary>지면 흔들림 이벤트 (카메라 쉐이크 등에 바인딩)</summary>
        public event System.Action<LargeMonsterShakeData> OnGroundShake
        {
            add => _onGroundShake += value;
            remove => _onGroundShake -= value;
        }

        public float SizeScale => _sizeScale;
        public float MassMultiplier => _massMultiplier;
        public float BaseDutyCycle => _baseDutyCycle;
        public float StepLengthMultiplier => _stepLengthMultiplier;
        public float StepHeightMultiplier => _stepHeightMultiplier;

        /// <summary>
        /// 착지 충격 발생 시 호출.
        /// </summary>
        public void TriggerGroundShake(float magnitude, Vector3 origin)
        {
            _currentShake = new LargeMonsterShakeData
            {
                Magnitude = math.clamp(magnitude, 0f, 1f),
                Origin = origin,
                Radius = _shakeRadius,
                Duration = _shakeDuration,
                Elapsed = 0f,
                IsActive = true
            };

            _onGroundShake?.Invoke(_currentShake);
        }

        private void Update()
        {
            if (_currentShake.IsActive)
            {
                _currentShake.Elapsed += Time.deltaTime;
                if (_currentShake.Elapsed >= _currentShake.Duration)
                {
                    _currentShake.IsActive = false;
                }
            }
        }

        /// <summary>
        /// 현재 흔들림 데이터를 가져옴 (카메라 쉐이크 시스템에서 사용).
        /// </summary>
        public LargeMonsterShakeData GetCurrentShake() => _currentShake;
    }
}