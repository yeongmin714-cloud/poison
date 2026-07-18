using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace ProjectName.Systems.Animation.Procedural.Locomotion.Biped
{
    /// <summary>
    /// Foot placement planner: predicts next foot positions based on velocity, turning, terrain.
    /// </summary>
    [BurstCompile]
    public struct FootPlannerJob : IJob
    {
        [ReadOnly] public float3 BodyPosition;
        [ReadOnly] public quaternion BodyRotation;
        [ReadOnly] public float3 BodyVelocity;
        [ReadOnly] public float3 BodyAngularVelocity;
        [ReadOnly] public float DeltaTime;
        [ReadOnly] public float StepLength;
        [ReadOnly] public float StepWidth;
        [ReadOnly] public float MaxStepHeight;
        [ReadOnly] public float GroundCheckDistance;
        [ReadOnly] public float3 LeftFootCurrent;
        [ReadOnly] public float3 RightFootCurrent;
        [ReadOnly] public bool LeftFootGrounded;
        [ReadOnly] public bool RightFootGrounded;
        [ReadOnly] public float LeftPhase;
        [ReadOnly] public float RightPhase;
        [ReadOnly] public float DutyCycle;
        [ReadOnly] public float Speed;

        public NativeArray<float3> OutLeftTarget;
        public NativeArray<float3> OutRightTarget;
        public NativeArray<float3> OutLeftHint;
        public NativeArray<float3> OutRightHint;
        public NativeArray<float3> OutLeftGroundPos;
        public NativeArray<float3> OutRightGroundPos;
        public NativeArray<bool> OutLeftCanStep;
        public NativeArray<bool> OutRightCanStep;

        public void Execute()
        {
            float3 forward = math.mul(BodyRotation, math.forward());
            float3 right = math.mul(BodyRotation, math.right());
            float3 up = math.up();

            // Speed-based step length scaling
            float speedRatio = math.saturate(Speed / 7f); // max run ~7m/s
            float currentStepLength = StepLength * math.lerp(0.6f, 1.4f, speedRatio);
            float currentStepWidth = StepWidth * math.lerp(0.8f, 1.2f, speedRatio);

            // Turning prediction
            float turnRadius = math.abs(BodyAngularVelocity.y) > 0.01f ? Speed / BodyAngularVelocity.y : 100f;
            float3 turnCenter = BodyPosition - right * turnRadius;

            // Left foot
            float3 leftTarget = LeftFootCurrent;
            float3 leftHint = LeftFootCurrent + right * 0.3f;
            float3 leftGround = LeftFootCurrent;
            bool leftCanStep = false;

            if (LeftPhase >= DutyCycle && LeftFootGrounded)
            {
                // Swing phase: predict next step position
                float swingProgress = (LeftPhase - DutyCycle) / (1f - DutyCycle);
                float3 swingOffset = forward * currentStepLength * swingProgress;
                float3 lateralOffset = -right * currentStepWidth * 0.5f;
                float heightOffset = math.sin(swingProgress * math.PI) * MaxStepHeight;

                // Adjust for turning
                if (math.abs(turnRadius) < 50f)
                {
                    float3 toCenter = turnCenter - LeftFootCurrent;
                    float3 tangent = math.normalize(math.cross(toCenter, up));
                    swingOffset += tangent * currentStepLength * 0.3f * swingProgress;
                }

                leftTarget = LeftFootCurrent + swingOffset + lateralOffset + up * heightOffset;
                leftHint = leftTarget + right * 0.3f;
                leftCanStep = true;
            }
            else if (LeftFootGrounded)
            {
                // Stance: raycast to find ground (simplified - would need physics query in real impl)
                leftGround = LeftFootCurrent; // would be raycast hit point
                leftHint = LeftFootCurrent + right * 0.15f;
            }

            // Right foot
            float3 rightTarget = RightFootCurrent;
            float3 rightHint = RightFootCurrent - right * 0.3f;
            float3 rightGround = RightFootCurrent;
            bool rightCanStep = false;

            if (RightPhase >= DutyCycle && RightFootGrounded)
            {
                float swingProgress = (RightPhase - DutyCycle) / (1f - DutyCycle);
                float3 swingOffset = forward * currentStepLength * swingProgress;
                float3 lateralOffset = right * currentStepWidth * 0.5f;
                float heightOffset = math.sin(swingProgress * math.PI) * MaxStepHeight;

                if (math.abs(turnRadius) < 50f)
                {
                    float3 toCenter = turnCenter - RightFootCurrent;
                    float3 tangent = math.normalize(math.cross(toCenter, up));
                    swingOffset += tangent * currentStepLength * 0.3f * swingProgress;
                }

                rightTarget = RightFootCurrent + swingOffset + lateralOffset + up * heightOffset;
                rightHint = rightTarget - right * 0.3f;
                rightCanStep = true;
            }
            else if (RightFootGrounded)
            {
                rightGround = RightFootCurrent;
                rightHint = RightFootCurrent - right * 0.15f;
            }

            OutLeftTarget[0] = leftTarget;
            OutRightTarget[0] = rightTarget;
            OutLeftHint[0] = leftHint;
            OutRightHint[0] = rightHint;
            OutLeftGroundPos[0] = leftGround;
            OutRightGroundPos[0] = rightGround;
            OutLeftCanStep[0] = leftCanStep;
            OutRightCanStep[0] = rightCanStep;
        }
    }

    /// <summary>
    /// Swing trajectory: smooth foot arc with terrain clearance.
    /// </summary>
    [BurstCompile]
    public struct SwingTrajectoryJob : IJob
    {
        [ReadOnly] public float3 StartPos;
        [ReadOnly] public float3 EndPos;
        [ReadOnly] public float3 HintPos;
        [ReadOnly] public float Progress; // 0-1
        [ReadOnly] public float Height;
        [ReadOnly] public float Clearance; // minimum ground clearance

        public NativeArray<float3> OutPosition;
        public NativeArray<float3> OutVelocity;
        public NativeArray<float3> OutHint;

        public void Execute()
        {
            // Cubic bezier for smooth arc
            float3 mid1 = math.lerp(StartPos, EndPos, 0.33f) + math.up() * Height * 0.5f;
            float3 mid2 = math.lerp(StartPos, EndPos, 0.66f) + math.up() * Height * 0.8f;

            float t = Progress;
            float u = 1f - t;
            float3 pos = u * u * u * StartPos
                       + 3f * u * u * t * mid1
                       + 3f * u * t * t * mid2
                       + t * t * t * EndPos;

            // Velocity (derivative)
            float3 vel = 3f * u * u * (mid1 - StartPos)
                       + 6f * u * t * (mid2 - mid1)
                       + 3f * t * t * (EndPos - mid2);

            // Hint follows knee
            float3 hint = math.lerp(StartPos + math.normalize(HintPos - StartPos) * 0.3f,
                                    EndPos + math.normalize(HintPos - EndPos) * 0.3f, t);

            // Ensure minimum clearance
            pos.y = math.max(pos.y, Clearance);

            OutPosition[0] = pos;
            OutVelocity[0] = vel;
            OutHint[0] = hint;
        }
    }

    /// <summary>
    /// Stance stabilizer: keeps stance foot planted, handles uneven terrain.
    /// </summary>
    [BurstCompile]
    public struct StanceStabilizerJob : IJob
    {
        [ReadOnly] public float3 FootPosition;
        [ReadOnly] public float3 FootNormal; // ground normal
        [ReadOnly] public float3 BodyPosition;
        [ReadOnly] public quaternion BodyRotation;
        [ReadOnly] public float3 HipPosition;
        [ReadOnly] public float MaxStanceForce;
        [ReadOnly] public float Damping;

        public NativeArray<float3> OutCorrectedFootPos;
        public NativeArray<quaternion> OutCorrectedFootRot;
        public NativeArray<float3> OutStanceForce;

        public void Execute()
        {
            // Project foot onto ground plane
            float3 toFoot = FootPosition - BodyPosition;
            float3 projected = toFoot - math.dot(toFoot, FootNormal) * FootNormal;
            float3 correctedPos = BodyPosition + projected;

            // Align foot rotation to ground normal
            float3 forward = math.mul(BodyRotation, math.forward());
            forward = math.normalize(forward - math.dot(forward, FootNormal) * FootNormal);
            quaternion footRot = quaternion.LookRotationSafe(forward, FootNormal);

            // Compute stance force to support body (simplified)
            float3 supportDir = math.normalize(BodyPosition - FootPosition);
            float3 stanceForce = supportDir * MaxStanceForce * Damping;

            OutCorrectedFootPos[0] = correctedPos;
            OutCorrectedFootRot[0] = footRot;
            OutStanceForce[0] = stanceForce;
        }
    }

    /// <summary>
    /// Hip shift: lateral/vertical pelvis translation for weight transfer.
    /// </summary>
    [BurstCompile]
    public struct HipShiftJob : IJob
    {
        [ReadOnly] public float LeftPhase;
        [ReadOnly] public float RightPhase;
        [ReadOnly] public float DutyCycle;
        [ReadOnly] public float LeftWeight; // 0-1, how much weight on left leg
        [ReadOnly] public float RightWeight;
        [ReadOnly] public float MaxLateralShift;
        [ReadOnly] public float MaxVerticalShift;
        [ReadOnly] public float Speed;
        [ReadOnly] public float TurnAmount; // -1 to 1

        public NativeArray<float3> OutHipOffset;
        public NativeArray<float> OutHipHeightOffset;
        public NativeArray<quaternion> OutHipRotation;

        public void Execute()
        {
            // Weight-based lateral shift
            float weightDiff = LeftWeight - RightWeight; // -1 to 1
            float lateral = weightDiff * MaxLateralShift * 0.5f;

            // Add turn lean
            lateral += TurnAmount * MaxLateralShift * 0.3f * math.saturate(Speed / 5f);

            // Vertical shift (lower during double stance)
            bool doubleStance = (LeftPhase < DutyCycle) && (RightPhase < DutyCycle);
            float vertical = doubleStance ? -MaxVerticalShift * 0.5f : 0f;

            float3 offset = new float3(lateral, vertical, 0f);

            // Hip rotation: slight roll with lateral shift
            float roll = math.radians(lateral * 10f); // ~10 deg per meter shift
            quaternion rot = quaternion.AxisAngle(math.forward(), roll);

            OutHipOffset[0] = offset;
            OutHipHeightOffset[0] = vertical;
            OutHipRotation[0] = rot;
        }
    }

    /// <summary>
    /// Spine counter-rotation: upper body rotates opposite to hips for natural gait.
    /// </summary>
    [BurstCompile]
    public struct SpineCounterRotationJob : IJob
    {
        [ReadOnly] public float LeftPhase;
        [ReadOnly] public float RightPhase;
        [ReadOnly] public float DutyCycle;
        [ReadOnly] public float MaxCounterRotation; // degrees
        [ReadOnly] public float3 BodyVelocity;
        [ReadOnly] public quaternion BodyRotation;
        [ReadOnly] public int SpineSegmentCount;

        public NativeArray<quaternion> OutSpineRotations; // length = SpineSegmentCount

        public void Execute()
        {
            // Hip yaw from leg phases
            float leftStance = LeftPhase < DutyCycle ? 1f : 0f;
            float rightStance = RightPhase < DutyCycle ? 1f : 0f;

            // Counter-rotation: when left leg forward (right stance), spine rotates right
            float hipYaw = (rightStance - leftStance) * math.radians(MaxCounterRotation);

            // Distribute along spine
            for (int i = 0; i < SpineSegmentCount; i++)
            {
                float weight = (float)(i + 1) / SpineSegmentCount; // more rotation higher up
                float segmentYaw = hipYaw * weight;

                // Add velocity-based lean
                float3 localVel = math.mul(quaternion.inverse(BodyRotation), BodyVelocity);
                float leanYaw = math.atan2(localVel.x, localVel.z) * 0.1f * weight;
                segmentYaw += leanYaw;

                OutSpineRotations[i] = quaternion.AxisAngle(math.up(), segmentYaw);
            }
        }
    }

    /// <summary>
    /// Leg phase oscillator: generates anti-phase leg rhythms.
    /// </summary>
    [BurstCompile]
    public struct LegPhaseOscillatorJob : IJob
    {
        [ReadOnly] public float CurrentSpeed;
        [ReadOnly] public float WalkSpeed;
        [ReadOnly] public float RunSpeed;
        [ReadOnly] public float DeltaTime;
        [ReadOnly] public float CurrentLeftPhase;
        [ReadOnly] public float CurrentRightPhase;
        [ReadOnly] public bool IsGrounded;

        public NativeArray<float> OutLeftPhase;
        public NativeArray<float> OutRightPhase;
        public NativeArray<float> OutPhaseSpeed;

        public void Execute()
        {
            if (!IsGrounded)
            {
                // Freeze phases in air
                OutLeftPhase[0] = CurrentLeftPhase;
                OutRightPhase[0] = CurrentRightPhase;
                OutPhaseSpeed[0] = 0f;
                return;
            }

            // Speed-based frequency
            float speedRatio = math.saturate(CurrentSpeed / RunSpeed);
            float frequency = math.lerp(0.5f, 2.5f, speedRatio); // 0.5-2.5 Hz

            float phaseDelta = frequency * DeltaTime;

            float leftPhase = math.fmod(CurrentLeftPhase + phaseDelta, 1f);
            float rightPhase = math.fmod(CurrentRightPhase + phaseDelta, 1f);

            // Maintain anti-phase (0.5 offset)
            float targetOffset = 0.5f;
            float currentOffset = math.abs(leftPhase - rightPhase);
            if (currentOffset > 0.5f) currentOffset = 1f - currentOffset;
            float offsetError = targetOffset - currentOffset;

            // Gently correct
            float correction = offsetError * 0.1f;
            if (leftPhase < rightPhase)
            {
                leftPhase = math.fmod(leftPhase - correction, 1f);
                rightPhase = math.fmod(rightPhase + correction, 1f);
            }
            else
            {
                leftPhase = math.fmod(leftPhase + correction, 1f);
                rightPhase = math.fmod(rightPhase - correction, 1f);
            }

            OutLeftPhase[0] = leftPhase;
            OutRightPhase[0] = rightPhase;
            OutPhaseSpeed[0] = frequency;
        }
    }
}