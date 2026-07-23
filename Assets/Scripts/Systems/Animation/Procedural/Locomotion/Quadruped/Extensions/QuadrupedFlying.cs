using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace ProjectName.Systems.Animation.Procedural.Locomotion.Quadruped.Extensions
{
    // ─────────────────────────────────────────────────────────────
    // Flying Gait Selector
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 비행형 쿼드러페드의 보행(비행 패턴) 선택기.
    /// Glide(활공), Flap(날갯짓), Hover(정지비행) 모드 선택.
    /// </summary>
    [BurstCompile]
    public struct FlyingGaitSelectorJob : IJob
    {
        public enum FlyingGait { Glide, Flap, Hover }

        [ReadOnly] public float CurrentSpeed;
        [ReadOnly] public float MaxSpeed;
        [ReadOnly] public float GlideSpeedThreshold;
        [ReadOnly] public float FlapSpeedThreshold;
        [ReadOnly] public float Altitude; // 현재 고도
        [ReadOnly] public float TargetAltitude; // 목표 고도
        [ReadOnly] public float EnergyLevel; // 0-1, 지속 비행 에너지
        [ReadOnly] public FlyingGait CurrentGait;

        public NativeArray<FlyingGait> OutSelectedGait;
        public NativeArray<float> OutGaitBlend;
        public NativeArray<float> OutWingFlapFrequency; // 날갯짓 주파수
        public NativeArray<float> OutWingAngleOffset; // 날개 각도 오프셋

        public void Execute()
        {
            float normalizedSpeed = CurrentSpeed / math.max(MaxSpeed, 0.01f);
            FlyingGait targetGait = CurrentGait;
            float blend = 1f;

            // 고도 차이에 따른 판단
            float altDiff = TargetAltitude - Altitude;

            // 저속 + 고고도 차이 → Hover
            if (normalizedSpeed < 0.15f || math.abs(altDiff) > 5f)
            {
                targetGait = FlyingGait.Hover;
            }
            // 중속 + 정상 고도 → Flap
            else if (normalizedSpeed < 0.6f)
            {
                targetGait = FlyingGait.Flap;
            }
            // 고속 → Glide (에너지 절약)
            else
            {
                targetGait = FlyingGait.Glide;
            }

            // 에너지 레벨이 낮으면 Glide 선호
            if (EnergyLevel < 0.2f && targetGait == FlyingGait.Flap)
            {
                targetGait = FlyingGait.Glide;
            }

            // 전환 블렌드
            if (targetGait != CurrentGait)
                blend = 0.4f;

            // 날갯짓 주파수
            float flapFreq = 1.0f;
            float wingAngle = 0f;

            switch (targetGait)
            {
                case FlyingGait.Glide:
                    flapFreq = 0.0f; // 날갯짓 없음
                    wingAngle = 15f; // 약간 펼침 (받음각)
                    break;
                case FlyingGait.Flap:
                    flapFreq = math.lerp(1.5f, 3.0f, normalizedSpeed); // 속도에 비례
                    wingAngle = 0f;
                    break;
                case FlyingGait.Hover:
                    flapFreq = 2.5f; // 빠른 날갯짓
                    wingAngle = -10f; // 위로 약간
                    break;
            }

            OutSelectedGait[0] = targetGait;
            OutGaitBlend[0] = blend;
            OutWingFlapFrequency[0] = flapFreq;
            OutWingAngleOffset[0] = wingAngle;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Wing IK Solver
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 날개 IK 솔버. 3본 체인(어깨-팔꿈치-날개끝)을 IK로 해결.
    /// LimbIKSolver의 확장 개념.
    /// </summary>
    [BurstCompile]
    public struct WingIKJob : IJobParallelFor
    {
        public enum WingSide { Left, Right }

        [ReadOnly] public NativeArray<float3> ShoulderPositions;   // root
        [ReadOnly] public NativeArray<float3> ElbowPositions;      // mid
        [ReadOnly] public NativeArray<float3> WingtipPositions;    // tip
        [ReadOnly] public NativeArray<float3> TargetPositions;
        [ReadOnly] public NativeArray<WingSide> Sides;
        [ReadOnly] public NativeArray<float> UpperArmLengths;
        [ReadOnly] public NativeArray<float> ForearmLengths;
        [ReadOnly] public NativeArray<float> FlapPhase;  // 0-1 날갯짓 위상
        [ReadOnly] public NativeArray<float> FlapAmplitude; // 날갯짓 진폭

        [WriteOnly] public NativeArray<float3> OutShoulderPositions;
        [WriteOnly] public NativeArray<float3> OutElbowPositions;
        [WriteOnly] public NativeArray<float3> OutWingtipPositions;
        [WriteOnly] public NativeArray<quaternion> OutShoulderRotations;
        [WriteOnly] public NativeArray<quaternion> OutElbowRotations;
        [WriteOnly] public NativeArray<quaternion> OutWingtipRotations;

        public void Execute(int index)
        {
            float3 shoulder = ShoulderPositions[index];
            float3 elbow = ElbowPositions[index];
            float3 wingtip = WingtipPositions[index];
            float3 target = TargetPositions[index];
            WingSide side = Sides[index];
            float upperLen = UpperArmLengths[index];
            float lowerLen = ForearmLengths[index];
            float totalLen = upperLen + lowerLen;
            float phase = FlapPhase[index];
            float amplitude = FlapAmplitude[index];

            // 날갯짓 움직임: 타겟 위치 자체를 변조
            float3 sideDir = side == WingSide.Left ? -1f : 1f;
            float flapAngle = math.sin(phase * 2f * math.PI) * amplitude;

            // 날갯짓은 Y축(상하) 회전으로 표현
            quaternion flapRot = quaternion.Euler(0f, 0f, flapAngle);
            float3 modTarget = target + math.mul(flapRot, new float3(0f, math.sin(phase * 2f * math.PI) * 0.5f, 0f));

            // FABRIK 2-bone IK (LimbIKJob과 동일한 알고리즘)
            float3 rootPos = shoulder;
            float3 midPos = elbow;
            float3 tipPos = wingtip;
            float3 ikTarget = modTarget;

            float3 rootToTarget = ikTarget - rootPos;
            float distToTarget = math.length(rootToTarget);

            if (distToTarget > totalLen * 0.999f)
            {
                float3 dir = math.normalize(rootToTarget);
                midPos = rootPos + dir * upperLen;
                tipPos = midPos + dir * lowerLen;
            }
            else
            {
                for (int i = 0; i < 2; i++)
                {
                    tipPos = ikTarget;

                    float3 midToTip = tipPos - midPos;
                    float midTipDist = math.length(midToTip);
                    if (midTipDist > 0.0001f)
                        midPos = tipPos - math.normalize(midToTip) * lowerLen;
                    else
                        midPos = tipPos - math.up() * lowerLen;

                    float3 rootToMid = midPos - rootPos;
                    float rootMidDist = math.length(rootToMid);
                    if (rootMidDist > 0.0001f)
                        midPos = rootPos + math.normalize(rootToMid) * upperLen;
                    else
                        midPos = rootPos + math.up() * upperLen;

                    midToTip = tipPos - midPos;
                    midTipDist = math.length(midToTip);
                    if (midTipDist > 0.0001f)
                        tipPos = midPos + math.normalize(midToTip) * lowerLen;
                    else
                        tipPos = midPos + math.up() * lowerLen;
                }
            }

            // 회전 계산
            quaternion shoulderRot = quaternion.LookRotationSafe(math.normalize(midPos - rootPos), math.up());
            quaternion elbowRot = quaternion.LookRotationSafe(math.normalize(tipPos - midPos), math.up());
            quaternion wingtipRot = quaternion.LookRotationSafe(math.normalize(tipPos - midPos), math.up());

            // 날개 특수 회전: 펼침 방향 (좌우 반전)
            float3 wingForward = math.normalize(tipPos - rootPos);
            float3 wingUp = math.up();
            quaternion wingBaseRot = quaternion.LookRotationSafe(wingForward, wingUp);

            OutShoulderPositions[index] = rootPos;
            OutElbowPositions[index] = midPos;
            OutWingtipPositions[index] = tipPos;
            OutShoulderRotations[index] = shoulderRot;
            OutElbowRotations[index] = elbowRot;
            OutWingtipRotations[index] = wingtipRot;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Flying Lift Physics
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 비행 물리: 양력, 항력, 기류 효과 계산.
    /// Burst 컴파일된 Job으로 물리 프레임마다 실행.
    /// </summary>
    [BurstCompile]
    public struct FlyingLiftPhysicsJob : IJob
    {
        [ReadOnly] public float3 Position;
        [ReadOnly] public float3 Velocity;
        [ReadOnly] public quaternion Rotation;
        [ReadOnly] public FlyingGaitSelectorJob.FlyingGait CurrentGait;
        [ReadOnly] public float WingSurfaceArea; // 날개 면적
        [ReadOnly] public float AirDensity;       // 공기 밀도
        [ReadOnly] public float LiftCoefficient;  // 양력 계수
        [ReadOnly] public float DragCoefficient;  // 항력 계수
        [ReadOnly] public float FlapFrequency;
        [ReadOnly] public float Time;

        [WriteOnly] public NativeArray<float3> OutLiftForce;
        [WriteOnly] public NativeArray<float3> OutDragForce;
        [WriteOnly] public NativeArray<float3> OutTotalForce;
        [WriteOnly] public NativeArray<float> OutDynamicPressure;
        [WriteOnly] public NativeArray<float> OutStallAngle; // 실속 각도 (디버그)

        public void Execute()
        {
            float3 forward = math.mul(Rotation, math.forward());
            float3 up = math.mul(Rotation, math.up());
            float speed = math.length(Velocity);

            // 1. 동압: q = 0.5 * rho * v^2
            float dynamicPressure = 0.5f * AirDensity * speed * speed;
            OutDynamicPressure[0] = dynamicPressure;

            // 2. 받음각 (Angle of Attack): 속도 방향과 전방 벡터 각도
            float3 velDir = speed > 0.01f ? math.normalize(Velocity) : forward;
            float aoa = math.acos(math.clamp(math.dot(velDir, forward), -1f, 1f));
            // 받음각이 π/2(90도) 넘으면 실속
            float stallAngle = math.radians(15f);
            OutStallAngle[0] = stallAngle;

            // 3. 양력: L = 0.5 * rho * v^2 * A * CL
            float effectiveLiftCoeff = LiftCoefficient;
            if (math.abs(aoa) > stallAngle)
            {
                // 실속: 양력 급감
                float stallFactor = math.max(0f, 1f - (math.abs(aoa) - stallAngle) * 5f);
                effectiveLiftCoeff *= stallFactor;
            }

            // 비행 모드에 따른 양력 계수 조정
            switch (CurrentGait)
            {
                case FlyingGaitSelectorJob.FlyingGait.Glide:
                    effectiveLiftCoeff *= 1.2f; // 활공 시 양력 상승
                    break;
                case FlyingGaitSelectorJob.FlyingGait.Flap:
                    // 날갯짓 양력 펄스
                    float flapLiftPulse = math.sin(Time * FlapFrequency * 2f * math.PI) * 0.5f + 0.5f;
                    effectiveLiftCoeff *= 1.0f + flapLiftPulse * 0.3f;
                    break;
                case FlyingGaitSelectorJob.FlyingGait.Hover:
                    // 호버링: 양력이 중력과 균형 (수직 양력)
                    effectiveLiftCoeff *= 1.5f;
                    break;
            }

            float liftMag = dynamicPressure * WingSurfaceArea * effectiveLiftCoeff;
            float3 liftForce = up * liftMag;

            // 4. 항력: D = 0.5 * rho * v^2 * A * CD
            float effectiveDragCoeff = DragCoefficient;
            // 실속 시 항력 증가
            if (math.abs(aoa) > stallAngle)
            {
                effectiveDragCoeff *= 1.0f + (math.abs(aoa) - stallAngle) * 3f;
            }

            float dragMag = dynamicPressure * WingSurfaceArea * effectiveDragCoeff;
            float3 dragForce = -velDir * dragMag;

            // 5. 총합
            float3 totalForce = liftForce + dragForce;

            OutLiftForce[0] = liftForce;
            OutDragForce[0] = dragForce;
            OutTotalForce[0] = totalForce;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Air Currents (기류)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 절차적 기류 생성. 상승기류, 돌풍, 난기류 시뮬레이션.
    /// </summary>
    [BurstCompile]
    public struct AirCurrentJob : IJob
    {
        [ReadOnly] public float3 Position;
        [ReadOnly] public float Time;
        [ReadOnly] public float3 WindDirection; // 기본 풍향
        [ReadOnly] public float WindSpeed;       // 기본 풍속
        [ReadOnly] public float TurbulenceIntensity; // 0-1 난류 강도

        [WriteOnly] public NativeArray<float3> OutWindForce;
        [WriteOnly] public NativeArray<float> OutThermalStrength; // 상승기류 강도

        public void Execute()
        {
            // 1. 기본 바람
            float3 baseWind = WindDirection * WindSpeed;

            // 2. 난류: Perlin-like 노이즈
            float turbX = math.sin(Position.x * 0.1f + Time * 2.3f) *
                          math.cos(Position.z * 0.13f + Time * 1.7f) *
                          TurbulenceIntensity * 2f;
            float turbY = math.sin(Position.x * 0.07f + Time * 1.1f) *
                          math.cos(Position.z * 0.09f + Time * 3.1f) *
                          TurbulenceIntensity * 1.5f;
            float turbZ = math.sin(Position.x * 0.11f + Time * 1.9f) *
                          math.cos(Position.z * 0.08f + Time * 2.5f) *
                          TurbulenceIntensity * 2f;

            float3 turbulence = new float3(turbX, turbY, turbZ) * 0.5f;

            // 3. 상승기류 (Thermal): 지형 기반 (간략화: 사인파 기반)
            // 실제로는 지형 높이맵을 샘플링하지만 여기서는 절차적 생성
            float thermalX = math.sin(Position.x * 0.02f + Time * 0.3f);
            float thermalZ = math.sin(Position.z * 0.025f + Time * 0.2f);
            float thermal = (thermalX * thermalZ) * 0.5f + 0.5f; // 0-1
            // 상승기류는 특정 위치에서 강하게 발생
            if (thermal > 0.7f) thermal = (thermal - 0.7f) / 0.3f;
            else thermal = 0f;

            float thermalStrength = thermal * 3f; // 최대 3m/s 상승

            // 4. 돌풍: 드문 큰 변동
            float gustNoise = math.sin(Time * 0.5f + Position.x * 0.05f) *
                              math.cos(Time * 0.3f + Position.z * 0.04f);
            float gustFactor = 0f;
            if (gustNoise > 0.85f)
                gustFactor = (gustNoise - 0.85f) / 0.15f; // 0-1 급상승
            float3 gust = baseWind * gustFactor * 2f;

            float3 totalWind = baseWind + turbulence + gust;
            totalWind.y += thermalStrength;

            OutWindForce[0] = totalWind;
            OutThermalStrength[0] = thermalStrength;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Flying Spine (비행 시 척추 안정화)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 비행 중 척추/꼬리 안정화 및 요동.
    /// 활공 시 몸통을 곧게, 호버링 시 약간 흔들림.
    /// </summary>
    [BurstCompile]
    public struct FlyingSpineJob : IJob
    {
        [ReadOnly] public float Time;
        [ReadOnly] public FlyingGaitSelectorJob.FlyingGait CurrentGait;
        [ReadOnly] public int SpineSegmentCount;
        [ReadOnly] public float3 BodyAngularVelocity;
        [ReadOnly] public float TailWagFrequency; // 꼬리 흔들림 주파수
        [ReadOnly] public float TailWagAmplitude; // 꼬리 흔들림 진폭

        public NativeArray<quaternion> OutSpineRotations;
        public NativeArray<quaternion> OutTailRotations; // 꼬리 회전 (선택)

        public void Execute()
        {
            float freq = 0f;
            float amp = 0f;

            switch (CurrentGait)
            {
                case FlyingGaitSelectorJob.FlyingGait.Glide:
                    freq = 0.5f;
                    amp = 0.05f; // 거의 고정
                    break;
                case FlyingGaitSelectorJob.FlyingGait.Flap:
                    freq = 1.5f;
                    amp = 0.15f; // 약한 요동
                    break;
                case FlyingGaitSelectorJob.FlyingGait.Hover:
                    freq = 2.0f;
                    amp = 0.25f; // 호버링 시 안정화 노력
                    break;
            }

            for (int i = 0; i < SpineSegmentCount; i++)
            {
                float segmentPhase = Time * freq + i * 0.3f;
                float wave = math.sin(segmentPhase) * amp;

                // 각속도 보상 (안정화)
                float angVelCompensation = math.length(BodyAngularVelocity) * 2f;
                float yaw = wave * (i % 2 == 0 ? 1f : -1f) - angVelCompensation * 0.1f;
                float roll = wave * 0.2f * (i % 2 == 0 ? 1f : -1f);

                OutSpineRotations[i] = quaternion.Euler(0f, math.degrees(yaw), math.degrees(roll));
            }

            // 꼬리 흔들림 (별도 체인)
            for (int i = 0; i < OutTailRotations.Length; i++)
            {
                float tailPhase = Time * TailWagFrequency + i * 0.5f;
                float tailSway = math.sin(tailPhase) * TailWagAmplitude;
                OutTailRotations[i] = quaternion.Euler(0f, math.degrees(tailSway * 0.5f), math.degrees(tailSway));
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Flying MonoBehaviour
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 비행형 쿼드러페드용 MonoBehaviour.
    /// 날개 IK 체인 설정, 비행 물리 업데이트, 기류 처리.
    /// </summary>
    [Obsolete("Use NeuralAnimationController with ONNX policies instead. See MIGRATION_GUIDE_PHASE46.md", false)]
    public class QuadrupedFlying : MonoBehaviour
    {
        [System.Serializable]
        public struct WingChain
        {
            public Transform Shoulder;
            public Transform Elbow;
            public Transform Wingtip;
            public WingIKJob.WingSide Side;
            [HideInInspector] public float UpperLength;
            [HideInInspector] public float ForearmLength;
        }

        [Header("날개 체인 (좌/우)")]
        [SerializeField] private WingChain _leftWing;
        [SerializeField] private WingChain _rightWing;

        [Header("비행 파라미터")]
        [SerializeField] private float _wingSurfaceArea = 4.0f;
        [SerializeField] private float _liftCoefficient = 1.2f;
        [SerializeField] private float _dragCoefficient = 0.3f;
        [SerializeField] private float _maxFlapAmplitude = 45f; // 도

        [Header("기류")]
        [SerializeField] private Vector3 _windDirection = Vector3.forward;
        [SerializeField] private float _windSpeed = 2f;
        [SerializeField][Range(0f, 1f)] private float _turbulenceIntensity = 0.3f;

        [Header("꼬리")]
        [SerializeField] private Transform[] _tailBones;
        [SerializeField] private float _tailWagFrequency = 2f;
        [SerializeField] private float _tailWagAmplitude = 10f;

        // 공개 속성
        public WingChain LeftWing => _leftWing;
        public WingChain RightWing => _rightWing;
        public float WingSurfaceArea => _wingSurfaceArea;
        public float LiftCoefficient => _liftCoefficient;
        public float DragCoefficient => _dragCoefficient;
        public float MaxFlapAmplitude => _maxFlapAmplitude;
        public Vector3 WindDirection => _windDirection;
        public float WindSpeed => _windSpeed;
        public float TurbulenceIntensity => _turbulenceIntensity;
        public Transform[] TailBones => _tailBones;
        public float TailWagFrequency => _tailWagFrequency;
        public float TailWagAmplitude => _tailWagAmplitude;

        private void Awake()
        {
            // 날개 길이 계산
            ComputeWingLengths(ref _leftWing);
            ComputeWingLengths(ref _rightWing);
        }

        private void ComputeWingLengths(ref WingChain wing)
        {
            if (wing.Shoulder != null && wing.Elbow != null)
                wing.UpperLength = Vector3.Distance(wing.Shoulder.position, wing.Elbow.position);
            if (wing.Elbow != null && wing.Wingtip != null)
                wing.ForearmLength = Vector3.Distance(wing.Elbow.position, wing.Wingtip.position);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            DrawWingGizmo(_leftWing);
            DrawWingGizmo(_rightWing);
        }

        private void DrawWingGizmo(WingChain wing)
        {
            if (wing.Shoulder != null && wing.Elbow != null && wing.Wingtip != null)
            {
                Gizmos.DrawLine(wing.Shoulder.position, wing.Elbow.position);
                Gizmos.DrawLine(wing.Elbow.position, wing.Wingtip.position);
                Gizmos.DrawSphere(wing.Shoulder.position, 0.1f);
                Gizmos.DrawSphere(wing.Elbow.position, 0.08f);
                Gizmos.DrawSphere(wing.Wingtip.position, 0.06f);
            }
        }
#endif
    }
}