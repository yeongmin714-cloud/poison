using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace ProjectName.Systems.Animation.Procedural.Locomotion.Quadruped.Extensions
{
    // ─────────────────────────────────────────────────────────────
    // Swimming Gait Selector
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 수영형 쿼드러페드의 보행(수영 패턴) 선택기.
    /// Paddle(노젓기), Dive(잠수), Surface(부상), Drift(표류).
    /// </summary>
    [BurstCompile]
    public struct SwimmingGaitSelectorJob : IJob
    {
        public enum SwimmingGait { Paddle, Dive, Surface, Drift }

        [ReadOnly] public float CurrentSpeed;
        [ReadOnly] public float MaxSpeed;
        [ReadOnly] public float Depth; // 현재 수심 (양수 = 수면 아래)
        [ReadOnly] public float TargetDepth; // 목표 수심
        [ReadOnly] public float PaddleSpeedThreshold;
        [ReadOnly] public float SurfaceProximity; // 0 = 깊음, 1 = 수면 근처
        [ReadOnly] public SwimmingGait CurrentGait;

        public NativeArray<SwimmingGait> OutSelectedGait;
        public NativeArray<float> OutGaitBlend;
        public NativeArray<float> OutStrokeFrequency; // 노젓기 주파수

        public void Execute()
        {
            SwimmingGait targetGait = CurrentGait;
            float blend = 1f;
            float depthDiff = TargetDepth - Depth;

            // 수면 근처 + 속도 없음 → Drift (표류)
            if (SurfaceProximity > 0.8f && CurrentSpeed < 0.1f)
            {
                targetGait = SwimmingGait.Drift;
            }
            // 목표 수심이 더 깊음 → Dive
            else if (depthDiff > 1f)
            {
                targetGait = SwimmingGait.Dive;
            }
            // 목표 수심이 더 얕음 → Surface
            else if (depthDiff < -1f)
            {
                targetGait = SwimmingGait.Surface;
            }
            // 정상 수영
            else if (CurrentSpeed > PaddleSpeedThreshold)
            {
                targetGait = SwimmingGait.Paddle;
            }
            else
            {
                targetGait = SwimmingGait.Drift;
            }

            if (targetGait != CurrentGait)
                blend = 0.4f;

            // 노젓기 주파수 (속도 비례)
            float strokeFreq = 0f;
            switch (targetGait)
            {
                case SwimmingGait.Paddle:  strokeFreq = math.lerp(0.5f, 2.0f, CurrentSpeed / math.max(MaxSpeed, 0.01f)); break;
                case SwimmingGait.Dive:    strokeFreq = 1.2f; break;
                case SwimmingGait.Surface: strokeFreq = 1.5f; break;
                case SwimmingGait.Drift:   strokeFreq = 0.2f; break;
            }

            OutSelectedGait[0] = targetGait;
            OutGaitBlend[0] = blend;
            OutStrokeFrequency[0] = strokeFreq;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Buoyancy (부력)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 부력 계산: 물에 잠긴 정도에 따라 상향력 적용.
    /// 중력과 부력의 균형으로 자연스러운 수중 부양.
    /// </summary>
    [BurstCompile]
    public struct BuoyancyJob : IJob
    {
        [ReadOnly] public float3 Position;
        [ReadOnly] public float3 Velocity;
        [ReadOnly] public float WaterSurfaceY; // 수면 Y 좌표
        [ReadOnly] public float BodyVolume;     // 몸통 부피 (부력 계수)
        [ReadOnly] public float WaterDensity;   // 물 밀도 (기본 1.0)
        [ReadOnly] public float GravityMagnitude; // 중력 가속도 (기본 9.81)

        [WriteOnly] public NativeArray<float3> OutBuoyancyForce;
        [WriteOnly] public NativeArray<float> OutSubmergedRatio; // 0-1, 잠긴 정도
        [WriteOnly] public NativeArray<float> OutFloatHeight; // 부력 평형 높이

        public void Execute()
        {
            // 1. 잠긴 정도 계산
            float bodyHeight = 0.3f; // 몸통 높이 (근사값)
            float halfBody = bodyHeight * 0.5f;

            // 몸통 하단 ~ 상단 범위 내 수면 위치
            float bodyBottom = Position.y - halfBody;
            float bodyTop = Position.y + halfBody;

            float submerged = 0f;
            if (WaterSurfaceY < bodyBottom)
            {
                // 완전 잠김
                submerged = 1f;
            }
            else if (WaterSurfaceY < bodyTop)
            {
                // 부분 잠김
                submerged = (bodyTop - WaterSurfaceY) / bodyHeight;
            }
            // else: 수면 위 → submerged = 0

            submerged = math.clamp(submerged, 0f, 1f);
            OutSubmergedRatio[0] = submerged;

            // 2. 부력: Fb = rho * V * g * submergedRatio
            float buoyancyMag = WaterDensity * BodyVolume * GravityMagnitude * submerged;
            float3 buoyancyForce = new float3(0f, buoyancyMag, 0f);

            // 3. 부력 평형 높이 (물 위에 떠있는 자연 높이)
            // 부력 = 중력일 때의 높이
            float floatHeight = WaterSurfaceY + BodyVolume * WaterDensity * 0.1f;

            OutBuoyancyForce[0] = buoyancyForce;
            OutFloatHeight[0] = floatHeight;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Undulatory Spine Wave (물결 척추)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 수영 시 척추를 따라 진행하는 물결파(Undulation).
    /// 뱀/어류의 S자 웨이브 모션 생성.
    /// </summary>
    [BurstCompile]
    public struct SwimmingSpineWaveJob : IJob
    {
        [ReadOnly] public float Time;
        [ReadOnly] public float WaveFrequency;  // 진행파 주파수
        [ReadOnly] public float WaveAmplitude;  // 진행파 진폭
        [ReadOnly] public float WaveSpeed;       // 파동 전파 속도
        [ReadOnly] public SwimmingGaitSelectorJob.SwimmingGait CurrentGait;
        [ReadOnly] public int SpineSegmentCount; // 3-5

        public NativeArray<quaternion> OutSpineRotations; // length = SpineSegmentCount
        public NativeArray<float3> OutSpineOffsets; // 척추 위치 오프셋

        public void Execute()
        {
            float freq = WaveFrequency;
            float amp = WaveAmplitude;
            float speed = WaveSpeed;

            // 수영 패턴별 파라미터 조정
            switch (CurrentGait)
            {
                case SwimmingGaitSelectorJob.SwimmingGait.Paddle:
                    freq *= 1.0f;
                    amp *= 1.0f;
                    break;
                case SwimmingGaitSelectorJob.SwimmingGait.Dive:
                    freq *= 1.3f;
                    amp *= 1.2f; // 잠수 시 강한 웨이브
                    break;
                case SwimmingGaitSelectorJob.SwimmingGait.Surface:
                    freq *= 0.8f;
                    amp *= 0.7f; // 부상 시 약한 웨이브
                    break;
                case SwimmingGaitSelectorJob.SwimmingGait.Drift:
                    freq *= 0.3f;
                    amp *= 0.2f; // 표류 시 거의 없음
                    break;
            }

            // 진행파 (Traveling Wave): 시간과 위치에 따라 위상 이동
            for (int i = 0; i < SpineSegmentCount; i++)
            {
                float t = (float)i / math.max(SpineSegmentCount - 1, 1);
                float phase = Time * freq * speed - t * 2f; // 머리 → 꼬리 방향

                // S자 파동: 좌우 + 상하 결합
                float lateralWave = math.sin(phase) * amp;          // 좌우
                float verticalWave = math.cos(phase * 0.7f) * amp * 0.3f; // 상하 (약하게)

                // 회전으로 변환
                float yaw = lateralWave * math.degrees(1f);
                float pitch = verticalWave * math.degrees(1f);
                float roll = lateralWave * 0.3f * math.degrees(1f);

                OutSpineRotations[i] = quaternion.Euler(pitch, yaw, roll);

                // 위치 오프셋 (선택적)
                float3 offset = new float3(
                    lateralWave * 0.1f,
                    verticalWave * 0.1f,
                    0f
                );
                OutSpineOffsets[i] = offset;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Fin IK (지느러미)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 지느러미 IK: 등지느러미, 꼬리지느러미, 가슴지느러미.
    /// 1-bone 및 2-bone IK로 펼침/접힘 제어.
    /// </summary>
    [BurstCompile]
    public struct FinIKJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> FinRootPositions;
        [ReadOnly] public NativeArray<float3> FinTipPositions;
        [ReadOnly] public NativeArray<float3> FinTargetPositions;
        [ReadOnly] public NativeArray<float> FinLengths;
        [ReadOnly] public NativeArray<float> FinSpreadAngles; // 0 = 접힘, 1 = 완전 펼침
        [ReadOnly] public NativeArray<SwimmingGaitSelectorJob.SwimmingGait> Gaits;

        [WriteOnly] public NativeArray<float3> OutFinTipPositions;
        [WriteOnly] public NativeArray<quaternion> OutFinRootRotations;
        [WriteOnly] public NativeArray<quaternion> OutFinTipRotations;

        public void Execute(int index)
        {
            float3 rootPos = FinRootPositions[index];
            float3 tipPos = FinTipPositions[index];
            float3 target = FinTargetPositions[index];
            float finLen = FinLengths[index];
            float spread = FinSpreadAngles[index]; // 0-1
            SwimmingGaitSelectorJob.SwimmingGait gait = Gaits[index];

            // 펼침 각도에 따른 타겟 위치 변조
            float3 rootToTarget = target - rootPos;
            float dist = math.length(rootToTarget);

            // 1-bone IK: 단순히 root→tip 방향 제어
            float3 direction = math.normalize(rootToTarget);

            // 지느러미 펼침: spread 각도에 따라 회전
            float spreadAngleRad = spread * math.radians(60f); // 최대 60도 펼침
            quaternion spreadRot = quaternion.Euler(spreadAngleRad, 0f, 0f);
            float3 spreadDir = math.mul(spreadRot, direction);

            float3 finalTip = rootPos + spreadDir * finLen;

            // 수영 패턴별 지느러미 각도 조정
            float extraAngle = 0f;
            switch (gait)
            {
                case SwimmingGaitSelectorJob.SwimmingGait.Paddle:
                    extraAngle = 15f; // 펼침
                    break;
                case SwimmingGaitSelectorJob.SwimmingGait.Dive:
                    extraAngle = -10f; // 접힘 (저항 감소)
                    break;
                case SwimmingGaitSelectorJob.SwimmingGait.Surface:
                    extraAngle = 20f; // 넓게 펼침
                    break;
                case SwimmingGaitSelectorJob.SwimmingGait.Drift:
                    extraAngle = 5f; // 약간 펼침
                    break;
            }

            quaternion finalRootRot = quaternion.LookRotationSafe(math.normalize(finalTip - rootPos), math.up());
            quaternion finalTipRot = quaternion.LookRotationSafe(math.normalize(finalTip - rootPos), math.up());

            // 추가 각도 적용
            quaternion extraRot = quaternion.Euler(math.radians(extraAngle), 0f, 0f);
            finalRootRot = math.mul(extraRot, finalRootRot);

            OutFinTipPositions[index] = finalTip;
            OutFinRootRotations[index] = finalRootRot;
            OutFinTipRotations[index] = finalTipRot;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Drag-Based Propulsion (항력 추진)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 수중 항력 기반 추진력 계산.
    /// 다리/지느러미 움직임에 따른 추력 생성.
    /// </summary>
    [BurstCompile]
    public struct DragPropulsionJob : IJob
    {
        [ReadOnly] public float3 Velocity;
        [ReadOnly] public float3 Forward;
        [ReadOnly] public float StrokeFrequency;
        [ReadOnly] public float Time;
        [ReadOnly] public float StrokeAmplitude; // 노젓기 진폭
        [ReadOnly] public float LimbDragCoefficient; // 다리/지느러미 항력 계수
        [ReadOnly] public float WaterDensity;
        [ReadOnly] public float LimbArea; // 다리 단면적

        [WriteOnly] public NativeArray<float3> OutPropulsionForce;
        [WriteOnly] public NativeArray<float> OutStrokePhase; // 0-1

        public void Execute()
        {
            // 1. 노젓기 위상
            float strokePhase = math.fmod(Time * StrokeFrequency, 1f);
            OutStrokePhase[0] = strokePhase;

            // 2. 노젓기 사이클: Power Stroke(추진) + Recovery(회수)
            // Power stroke: 0-0.5 (앞으로 저음)
            // Recovery: 0.5-1 (저항 줄임)
            float strokeProgress = strokePhase;
            float isPowerStroke = strokeProgress < 0.5f ? 1f : 0f;

            // Power stroke 중 추진력 (최대 중간에서)
            float powerCurve = 0f;
            if (isPowerStroke > 0.5f)
            {
                float t = strokeProgress / 0.5f; // 0-1 power stroke 내
                powerCurve = math.sin(t * math.PI); // 0→1→0
            }

            // 3. 속도 기반 항력 감소 (효율)
            float speed = math.length(Velocity);
            float efficiency = 1f - math.min(speed / 10f, 0.8f); // 빠를수록 효율 감소

            // 4. 추력: F = 0.5 * rho * v^2 * Cd * A * powerCurve
            float relativeSpeed = math.max(0.5f, speed); // 최소 속도 보장
            float thrustMag = 0.5f * WaterDensity * relativeSpeed * relativeSpeed *
                              LimbDragCoefficient * LimbArea * powerCurve * efficiency * StrokeAmplitude;

            float3 thrust = Forward * thrustMag;

            OutPropulsionForce[0] = thrust;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Swimming Hydrodynamics (수중 유체역학)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 수중 움직임에 대한 유체역학적 힘 (항력, 추가질량, 점성).
    /// </summary>
    [BurstCompile]
    public struct HydrodynamicsJob : IJob
    {
        [ReadOnly] public float3 Velocity;
        [ReadOnly] public float3 AngularVelocity;
        [ReadOnly] public float WaterDensity;
        [ReadOnly] public float CrossSectionalArea; // 단면적
        [ReadOnly] public float DragCoefficient;     // 항력 계수 (Cd)
        [ReadOnly] public float AddedMassCoefficient; // 추가질량 계수

        [WriteOnly] public NativeArray<float3> OutHydroDrag;
        [WriteOnly] public NativeArray<float3> OutHydroTorque;

        public void Execute()
        {
            float speed = math.length(Velocity);

            // 1. 항력: Fd = -0.5 * rho * v^2 * A * Cd * v_hat
            float dragMag = 0.5f * WaterDensity * speed * speed *
                            CrossSectionalArea * DragCoefficient;

            float3 dragDirection = speed > 0.01f ? math.normalize(Velocity) : math.forward();
            float3 drag = -dragDirection * dragMag;

            // 2. 추가질량 효과 (Acceleration reaction)
            // 유체가 가속을 방해하는 힘 (간략화)
            float addedMassForceMag = AddedMassCoefficient * WaterDensity * 0.5f;
            float3 addedMass = -Velocity * addedMassForceMag;

            // 3. 합계
            float3 totalDrag = drag + addedMass;

            // 4. 회전 항력 (Torque)
            float angSpeed = math.length(AngularVelocity);
            float torqueMag = angSpeed * angSpeed * CrossSectionalArea * DragCoefficient * 0.1f;
            float3 torqueDir = angSpeed > 0.01f ? math.normalize(AngularVelocity) : math.up();
            float3 dragTorque = -torqueDir * torqueMag;

            OutHydroDrag[0] = totalDrag;
            OutHydroTorque[0] = dragTorque;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Swimming Foot Target (수영 다리 움직임)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 수영 중 다리 움직임 (노젓기 모션).
    /// 각 다리가 별도의 위상으로 노를 저음.
    /// </summary>
    [BurstCompile]
    public struct SwimmingFootTargetJob : IJob
    {
        [ReadOnly] public float3 BodyPosition;
        [ReadOnly] public quaternion BodyRotation;
        [ReadOnly] public float3 BodyVelocity;
        [ReadOnly] public float LegPhase; // 0-1
        [ReadOnly] public float StrokeFrequency;
        [ReadOnly] public float StrokeAmplitude;
        [ReadOnly] public float3 LegDefaultPos; // 몸 기준 기본 위치
        [ReadOnly] public bool IsFrontLeg;
        [ReadOnly] public SwimmingGaitSelectorJob.SwimmingGait CurrentGait;
        [ReadOnly] public float Time;

        public NativeArray<float3> OutFootTarget;
        public NativeArray<float3> OutFootHint;
        public NativeArray<float> OutStrokeForce; // 추진력 가중치

        public void Execute()
        {
            float3 forward = math.mul(BodyRotation, math.forward());
            float3 right = math.mul(BodyRotation, math.right());
            float3 up = math.up();

            float3 defaultWorld = BodyPosition + math.mul(BodyRotation, LegDefaultPos);

            // 노젓기 사이클
            float strokePhase = math.fmod(LegPhase + Time * StrokeFrequency, 1f);
            float strokeProgress = strokePhase;

            // Power stroke (0-0.5): 발을 뒤로 저음
            // Recovery (0.5-1): 발을 앞으로 가져옴
            bool isPowerStroke = strokeProgress < 0.5f;
            float strokeT = isPowerStroke
                ? strokeProgress / 0.5f          // 0-1 power
                : (strokeProgress - 0.5f) / 0.5f; // 0-1 recovery

            float powerForce = 0f;
            float3 targetOffset = float3.zero;

            if (isPowerStroke)
            {
                // Power stroke: 뒤로 + 약간 아래
                float backDist = strokeT * StrokeAmplitude;
                targetOffset = -forward * backDist + up * math.sin(strokeT * math.PI) * (-0.2f);
                powerForce = math.sin(strokeT * math.PI);
            }
            else
            {
                // Recovery: 앞으로 + 위로 (저항 최소화)
                float forwardDist = strokeT * StrokeAmplitude * 0.6f;
                targetOffset = forward * forwardDist + up * math.sin(strokeT * math.PI) * 0.3f;
                powerForce = 0f;
            }

            // 다리별 좌우 오프셋
            float lateralOffset = IsFrontLeg ? 0.3f : -0.3f;
            targetOffset += right * lateralOffset;

            // 수영 패턴별 변형
            switch (CurrentGait)
            {
                case SwimmingGaitSelectorJob.SwimmingGait.Dive:
                    targetOffset += -up * 0.2f; // 아래로 힘
                    break;
                case SwimmingGaitSelectorJob.SwimmingGait.Surface:
                    targetOffset += up * 0.3f; // 위로 힘
                    break;
                case SwimmingGaitSelectorJob.SwimmingGait.Drift:
                    targetOffset *= 0.2f; // 거의 움직이지 않음
                    powerForce = 0f;
                    break;
            }

            float3 target = defaultWorld + targetOffset;
            float3 hint = target + right * (IsFrontLeg ? 0.3f : -0.3f) + up * 0.2f;

            OutFootTarget[0] = target;
            OutFootHint[0] = hint;
            OutStrokeForce[0] = math.clamp(powerForce, 0f, 1f);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Swimming MonoBehaviour
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 수영형 쿼드러페드용 MonoBehaviour.
    /// 부력, 물결파, 지느러미, 유체역학 통합.
    /// </summary>
    [Obsolete("Use NeuralAnimationController with ONNX policies instead. See MIGRATION_GUIDE_PHASE46.md", false)]
    public class QuadrupedSwimming : MonoBehaviour
    {
        [System.Serializable]
        public struct FinChain
        {
            public Transform Root;
            public Transform Tip;
            [HideInInspector] public float Length;
            [Range(0f, 1f)] public float DefaultSpread;
        }

        [Header("부력")]
        [SerializeField] private float _bodyVolume = 1.0f;
        [SerializeField] private float _waterDensity = 1.0f;
        [SerializeField] private float _waterSurfaceY = 0f;

        [Header("척추 물결")]
        [SerializeField] private int _spineSegmentCount = 4;
        [SerializeField] private float _waveFrequency = 2.0f;
        [SerializeField] private float _waveAmplitude = 1.5f;
        [SerializeField] private float _waveSpeed = 2.0f;

        [Header("지느러미")]
        [SerializeField] private FinChain[] _fins;
        [SerializeField] private Transform[] _finTransforms; // IK 타겟

        [Header("유체역학")]
        [SerializeField] private float _crossSectionalArea = 0.5f;
        [SerializeField] private float _dragCoefficient = 0.4f;
        [SerializeField] private float _addedMassCoefficient = 0.5f;

        [Header("노젓기")]
        [SerializeField] private float _strokeAmplitude = 1.0f;
        [SerializeField] private float _limbDragCoefficient = 0.8f;
        [SerializeField] private float _limbArea = 0.2f;

        // 공개 속성
        public float BodyVolume => _bodyVolume;
        public float WaterDensity => _waterDensity;
        public float WaterSurfaceY => _waterSurfaceY;
        public int SpineSegmentCount => _spineSegmentCount;
        public float WaveFrequency => _waveFrequency;
        public float WaveAmplitude => _waveAmplitude;
        public float WaveSpeed => _waveSpeed;
        public FinChain[] Fins => _fins;
        public float CrossSectionalArea => _crossSectionalArea;
        public float DragCoefficient => _dragCoefficient;
        public float AddedMassCoefficient => _addedMassCoefficient;
        public float StrokeAmplitude => _strokeAmplitude;
        public float LimbDragCoefficient => _limbDragCoefficient;
        public float LimbArea => _limbArea;

        private void Awake()
        {
            // 지느러미 길이 계산
            for (int i = 0; i < _fins.Length; i++)
            {
                if (_fins[i].Root != null && _fins[i].Tip != null)
                {
                    _fins[i].Length = Vector3.Distance(_fins[i].Root.position, _fins[i].Tip.position);
                }
            }
        }

        /// <summary>
        /// 수면 Y 좌표 업데이트 (외부 Water 시스템에서 호출).
        /// </summary>
        public void SetWaterSurfaceY(float y)
        {
            _waterSurfaceY = y;
        }

        /// <summary>
        /// 현재 잠긴 정도를 계산 (외부 모니터링용).
        /// </summary>
        public float CalculateSubmergedRatio(Vector3 position)
        {
            float bodyHeight = 0.3f;
            float bodyTop = position.y + bodyHeight * 0.5f;
            float bodyBottom = position.y - bodyHeight * 0.5f;

            if (_waterSurfaceY < bodyBottom) return 1f;
            if (_waterSurfaceY < bodyTop)
                return (bodyTop - _waterSurfaceY) / bodyHeight;
            return 0f;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // 수면 표시
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
            Vector3 waterCenter = transform.position;
            waterCenter.y = _waterSurfaceY;
            Gizmos.DrawCube(waterCenter, new Vector3(5f, 0.05f, 5f));

            // 지느러미
            Gizmos.color = Color.yellow;
            foreach (var fin in _fins)
            {
                if (fin.Root != null && fin.Tip != null)
                {
                    Gizmos.DrawLine(fin.Root.position, fin.Tip.position);
                    Gizmos.DrawSphere(fin.Root.position, 0.05f);
                    Gizmos.DrawSphere(fin.Tip.position, 0.03f);
                }
            }
        }
#endif
    }
}