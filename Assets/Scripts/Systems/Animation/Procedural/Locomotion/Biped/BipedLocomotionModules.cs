using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace ProjectName.Systems.Animation.Procedural.Locomotion.Biped
{
    /// <summary>
    /// Foot placement planner: predicts next foot positions based on velocity, turning, terrain.
    /// IJobParallelFor for batch processing multiple characters.
    /// </summary>
    [BurstCompile]
    public struct FootPlannerJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> BodyPositions;
        [ReadOnly] public NativeArray<quaternion> BodyRotations;
        [ReadOnly] public NativeArray<float3> BodyVelocities;
        [ReadOnly] public NativeArray<float3> BodyAngularVelocities;
        [ReadOnly] public NativeArray<float> DeltaTimes;
        [ReadOnly] public NativeArray<float> StepLengths;
        [ReadOnly] public NativeArray<float> StepWidths;
        [ReadOnly] public NativeArray<float> MaxStepHeights;
        [ReadOnly] public NativeArray<float> GroundCheckDistances;
        [ReadOnly] public NativeArray<float3> LeftFootCurrents;
        [ReadOnly] public NativeArray<float3> RightFootCurrents;
        [ReadOnly] public NativeArray<bool> LeftFootGroundedFlags;
        [ReadOnly] public NativeArray<bool> RightFootGroundedFlags;
        [ReadOnly] public NativeArray<float> LeftPhases;
        [ReadOnly] public NativeArray<float> RightPhases;
        [ReadOnly] public NativeArray<float> DutyCycles;
        [ReadOnly] public NativeArray<float> Speeds;

        [WriteOnly] public NativeArray<float3> OutLeftTargets;
        [WriteOnly] public NativeArray<float3> OutRightTargets;
        [WriteOnly] public NativeArray<float3> OutLeftHints;
        [WriteOnly] public NativeArray<float3> OutRightHints;
        [WriteOnly] public NativeArray<float3> OutLeftGroundPositions;
        [WriteOnly] public NativeArray<float3> OutRightGroundPositions;
        [WriteOnly] public NativeArray<bool> OutLeftCanStepFlags;
        [WriteOnly] public NativeArray<bool> OutRightCanStepFlags;

        public void Execute(int index)
        {
            float3 bodyPos = BodyPositions[index];
            quaternion bodyRot = BodyRotations[index];
            float3 bodyVel = BodyVelocities[index];
            float3 bodyAngVel = BodyAngularVelocities[index];
            float deltaTime = DeltaTimes[index];
            float stepLength = StepLengths[index];
            float stepWidth = StepWidths[index];
            float maxStepHeight = MaxStepHeights[index];
            float3 leftFootCurrent = LeftFootCurrents[index];
            float3 rightFootCurrent = RightFootCurrents[index];
            bool leftFootGrounded = LeftFootGroundedFlags[index];
            bool rightFootGrounded = RightFootGroundedFlags[index];
            float leftPhase = LeftPhases[index];
            float rightPhase = RightPhases[index];
            float dutyCycle = DutyCycles[index];
            float speed = Speeds[index];

            float3 forward = math.mul(bodyRot, math.forward());
            float3 right = math.mul(bodyRot, math.right());
            float3 up = math.up();

            // Speed-based step length scaling
            float speedRatio = math.saturate(speed / 7f);
            float currentStepLength = stepLength * math.lerp(0.6f, 1.4f, speedRatio);
            float currentStepWidth = stepWidth * math.lerp(0.8f, 1.2f, speedRatio);

            // Turning prediction
            float turnRadius = math.abs(bodyAngVel.y) > 0.01f ? speed / bodyAngVel.y : 100f;
            float3 turnCenter = bodyPos - right * turnRadius;

            // Left foot
            float3 leftTarget = leftFootCurrent;
            float3 leftHint = leftFootCurrent + right * 0.3f;
            float3 leftGround = leftFootCurrent;
            bool leftCanStep = false;

            if (leftPhase >= dutyCycle && leftFootGrounded)
            {
                float swingProgress = (leftPhase - dutyCycle) / (1f - dutyCycle);
                float3 swingOffset = forward * currentStepLength * swingProgress;
                float3 lateralOffset = -right * currentStepWidth * 0.5f;
                float heightOffset = math.sin(swingProgress * math.PI) * maxStepHeight;

                if (math.abs(turnRadius) < 50f)
                {
                    float3 toCenter = turnCenter - leftFootCurrent;
                    float3 tangent = math.normalize(math.cross(toCenter, up));
                    swingOffset += tangent * currentStepLength * 0.3f * swingProgress;
                }

                leftTarget = leftFootCurrent + swingOffset + lateralOffset + up * heightOffset;
                leftHint = leftTarget + right * 0.3f;
                leftCanStep = true;
            }
            else if (leftFootGrounded)
            {
                leftGround = leftFootCurrent;
                leftHint = leftFootCurrent + right * 0.15f;
            }

            // Right foot
            float3 rightTarget = rightFootCurrent;
            float3 rightHint = rightFootCurrent - right * 0.3f;
            float3 rightGround = rightFootCurrent;
            bool rightCanStep = false;

            if (rightPhase >= dutyCycle && rightFootGrounded)
            {
                float swingProgress = (rightPhase - dutyCycle) / (1f - dutyCycle);
                float3 swingOffset = forward * currentStepLength * swingProgress;
                float3 lateralOffset = right * currentStepWidth * 0.5f;
                float heightOffset = math.sin(swingProgress * math.PI) * maxStepHeight;

                if (math.abs(turnRadius) < 50f)
                {
                    float3 toCenter = turnCenter - rightFootCurrent;
                    float3 tangent = math.normalize(math.cross(toCenter, up));
                    swingOffset += tangent * currentStepLength * 0.3f * swingProgress;
                }

                rightTarget = rightFootCurrent + swingOffset + lateralOffset + up * heightOffset;
                rightHint = rightTarget - right * 0.3f;
                rightCanStep = true;
            }
            else if (rightFootGrounded)
            {
                rightGround = rightFootCurrent;
                rightHint = rightFootCurrent - right * 0.15f;
            }

            OutLeftTargets[index] = leftTarget;
            OutRightTargets[index] = rightTarget;
            OutLeftHints[index] = leftHint;
            OutRightHints[index] = rightHint;
            OutLeftGroundPositions[index] = leftGround;
            OutRightGroundPositions[index] = rightGround;
            OutLeftCanStepFlags[index] = leftCanStep;
            OutRightCanStepFlags[index] = rightCanStep;
        }
    }

    /// <summary>
    /// Swing trajectory: smooth foot arc with terrain clearance.
    /// IJobParallelFor for batch processing.
    /// </summary>
    [BurstCompile]
    public struct SwingTrajectoryJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> StartPositions;
        [ReadOnly] public NativeArray<float3> EndPositions;
        [ReadOnly] public NativeArray<float3> HintPositions;
        [ReadOnly] public NativeArray<float> ProgressValues;
        [ReadOnly] public NativeArray<float> Heights;
        [ReadOnly] public NativeArray<float> Clearances;

        [WriteOnly] public NativeArray<float3> OutPositions;
        [WriteOnly] public NativeArray<float3> OutVelocities;
        [WriteOnly] public NativeArray<float3> OutHints;

        public void Execute(int index)
        {
            float3 startPos = StartPositions[index];
            float3 endPos = EndPositions[index];
            float3 hintPos = HintPositions[index];
            float progress = ProgressValues[index];
            float height = Heights[index];
            float clearance = Clearances[index];

            // Cubic bezier for smooth arc
            float3 mid1 = math.lerp(startPos, endPos, 0.33f) + math.up() * height * 0.5f;
            float3 mid2 = math.lerp(startPos, endPos, 0.66f) + math.up() * height * 0.8f;

            float t = progress;
            float u = 1f - t;
            float3 pos = u * u * u * startPos
                       + 3f * u * u * t * mid1
                       + 3f * u * t * t * mid2
                       + t * t * t * endPos;

            // Velocity (derivative)
            float3 vel = 3f * u * u * (mid1 - startPos)
                       + 6f * u * t * (mid2 - mid1)
                       + 3f * t * t * (endPos - mid2);

            // Hint follows knee
            float3 hint = math.lerp(startPos + math.normalize(hintPos - startPos) * 0.3f,
                                    endPos + math.normalize(hintPos - endPos) * 0.3f, t);

            // Ensure minimum clearance
            pos.y = math.max(pos.y, clearance);

            OutPositions[index] = pos;
            OutVelocities[index] = vel;
            OutHints[index] = hint;
        }
    }

    /// <summary>
    /// Stance stabilizer: keeps stance foot planted, handles uneven terrain.
    /// IJobParallelFor for batch processing.
    /// </summary>
    [BurstCompile]
    public struct StanceStabilizerJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> FootPositions;
        [ReadOnly] public NativeArray<float3> FootNormals;
        [ReadOnly] public NativeArray<float3> BodyPositions;
        [ReadOnly] public NativeArray<quaternion> BodyRotations;
        [ReadOnly] public NativeArray<float3> HipPositions;
        [ReadOnly] public NativeArray<float> MaxStanceForces;
        [ReadOnly] public NativeArray<float> Dampings;

        [WriteOnly] public NativeArray<float3> OutCorrectedFootPositions;
        [WriteOnly] public NativeArray<quaternion> OutCorrectedFootRotations;
        [WriteOnly] public NativeArray<float3> OutStanceForces;

        public void Execute(int index)
        {
            float3 footPos = FootPositions[index];
            float3 footNormal = FootNormals[index];
            float3 bodyPos = BodyPositions[index];
            quaternion bodyRot = BodyRotations[index];
            float maxStanceForce = MaxStanceForces[index];
            float damping = Dampings[index];

            // Project foot onto ground plane
            float3 toFoot = footPos - bodyPos;
            float3 projected = toFoot - math.dot(toFoot, footNormal) * footNormal;
            float3 correctedPos = bodyPos + projected;

            // Align foot rotation to ground normal
            float3 forward = math.mul(bodyRot, math.forward());
            forward = math.normalize(forward - math.dot(forward, footNormal) * footNormal);
            quaternion footRot = quaternion.LookRotationSafe(forward, footNormal);

            // Compute stance force to support body (simplified)
            float3 supportDir = math.normalize(bodyPos - footPos);
            float3 stanceForce = supportDir * maxStanceForce * damping;

            OutCorrectedFootPositions[index] = correctedPos;
            OutCorrectedFootRotations[index] = footRot;
            OutStanceForces[index] = stanceForce;
        }
    }

    /// <summary>
    /// Hip shift: lateral/vertical pelvis translation for weight transfer.
    /// IJobParallelFor for batch processing.
    /// </summary>
    [BurstCompile]
    public struct HipShiftJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> LeftPhases;
        [ReadOnly] public NativeArray<float> RightPhases;
        [ReadOnly] public NativeArray<float> DutyCycles;
        [ReadOnly] public NativeArray<float> LeftWeights;
        [ReadOnly] public NativeArray<float> RightWeights;
        [ReadOnly] public NativeArray<float> MaxLateralShifts;
        [ReadOnly] public NativeArray<float> MaxVerticalShifts;
        [ReadOnly] public NativeArray<float> Speeds;
        [ReadOnly] public NativeArray<float> TurnAmounts;

        [WriteOnly] public NativeArray<float3> OutHipOffsets;
        [WriteOnly] public NativeArray<float> OutHipHeightOffsets;
        [WriteOnly] public NativeArray<quaternion> OutHipRotations;

        public void Execute(int index)
        {
            float leftPhase = LeftPhases[index];
            float rightPhase = RightPhases[index];
            float dutyCycle = DutyCycles[index];
            float leftWeight = LeftWeights[index];
            float rightWeight = RightWeights[index];
            float maxLateralShift = MaxLateralShifts[index];
            float maxVerticalShift = MaxVerticalShifts[index];
            float speed = Speeds[index];
            float turnAmount = TurnAmounts[index];

            // Weight-based lateral shift
            float weightDiff = leftWeight - rightWeight;
            float lateral = weightDiff * maxLateralShift * 0.5f;

            // Add turn lean
            lateral += turnAmount * maxLateralShift * 0.3f * math.saturate(speed / 5f);

            // Vertical shift (lower during double stance)
            bool doubleStance = (leftPhase < dutyCycle) && (rightPhase < dutyCycle);
            float vertical = doubleStance ? -maxVerticalShift * 0.5f : 0f;

            float3 offset = new float3(lateral, vertical, 0f);

            // Hip rotation: slight roll with lateral shift
            float roll = math.radians(lateral * 10f);
            quaternion rot = quaternion.AxisAngle(math.forward(), roll);

            OutHipOffsets[index] = offset;
            OutHipHeightOffsets[index] = vertical;
            OutHipRotations[index] = rot;
        }
    }

    /// <summary>
    /// Spine counter-rotation: upper body rotates opposite to hips for natural gait.
    /// IJobParallelFor for batch processing.
    /// </summary>
    [BurstCompile]
    public struct SpineCounterRotationJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> LeftPhases;
        [ReadOnly] public NativeArray<float> RightPhases;
        [ReadOnly] public NativeArray<float> DutyCycles;
        [ReadOnly] public NativeArray<float> MaxCounterRotations;
        [ReadOnly] public NativeArray<float3> BodyVelocities;
        [ReadOnly] public NativeArray<quaternion> BodyRotations;
        [ReadOnly] public NativeArray<int> SpineSegmentCounts; // per-character spine segment count

        [WriteOnly] public NativeArray<quaternion> OutSpineRotations; // flattened: index * maxSegments

        [ReadOnly] public int MaxSpineSegments; // stride for flattened output

        public void Execute(int index)
        {
            float leftPhase = LeftPhases[index];
            float rightPhase = RightPhases[index];
            float dutyCycle = DutyCycles[index];
            float maxCounterRotation = MaxCounterRotations[index];
            float3 bodyVel = BodyVelocities[index];
            quaternion bodyRot = BodyRotations[index];
            int spineCount = SpineSegmentCounts[index];

            // Hip yaw from leg phases
            float leftStance = leftPhase < dutyCycle ? 1f : 0f;
            float rightStance = rightPhase < dutyCycle ? 1f : 0f;

            float hipYaw = (rightStance - leftStance) * math.radians(maxCounterRotation);

            // Distribute along spine
            for (int i = 0; i < spineCount; i++)
            {
                float weight = (float)(i + 1) / spineCount;
                float segmentYaw = hipYaw * weight;

                // Add velocity-based lean
                float3 localVel = math.mul(math.inverse(bodyRot), bodyVel);
                float leanYaw = math.atan2(localVel.x, localVel.z) * 0.1f * weight;
                segmentYaw += leanYaw;

                int flatIdx = index * MaxSpineSegments + i;
                OutSpineRotations[flatIdx] = quaternion.AxisAngle(math.up(), segmentYaw);
            }

            // Zero out remaining spine segments
            for (int i = spineCount; i < MaxSpineSegments; i++)
            {
                OutSpineRotations[index * MaxSpineSegments + i] = quaternion.identity;
            }
        }
    }

    /// <summary>
    /// Leg phase oscillator: generates anti-phase leg rhythms.
    /// IJobParallelFor for batch processing.
    /// </summary>
    [BurstCompile]
    public struct LegPhaseOscillatorJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> CurrentSpeeds;
        [ReadOnly] public NativeArray<float> WalkSpeeds;
        [ReadOnly] public NativeArray<float> RunSpeeds;
        [ReadOnly] public NativeArray<float> DeltaTimes;
        [ReadOnly] public NativeArray<float> CurrentLeftPhases;
        [ReadOnly] public NativeArray<float> CurrentRightPhases;
        [ReadOnly] public NativeArray<bool> IsGroundedFlags;

        [WriteOnly] public NativeArray<float> OutLeftPhases;
        [WriteOnly] public NativeArray<float> OutRightPhases;
        [WriteOnly] public NativeArray<float> OutPhaseSpeeds;

        public void Execute(int index)
        {
            float currentSpeed = CurrentSpeeds[index];
            float runSpeed = RunSpeeds[index];
            float deltaTime = DeltaTimes[index];
            float currentLeftPhase = CurrentLeftPhases[index];
            float currentRightPhase = CurrentRightPhases[index];
            bool isGrounded = IsGroundedFlags[index];

            if (!isGrounded)
            {
                OutLeftPhases[index] = currentLeftPhase;
                OutRightPhases[index] = currentRightPhase;
                OutPhaseSpeeds[index] = 0f;
                return;
            }

            // Speed-based frequency
            float speedRatio = math.saturate(currentSpeed / runSpeed);
            float frequency = math.lerp(0.5f, 2.5f, speedRatio);

            float phaseDelta = frequency * deltaTime;

            float leftPhase = math.fmod(currentLeftPhase + phaseDelta, 1f);
            float rightPhase = math.fmod(currentRightPhase + phaseDelta, 1f);

            // Maintain anti-phase (0.5 offset)
            float targetOffset = 0.5f;
            float currentOffset = math.abs(leftPhase - rightPhase);
            if (currentOffset > 0.5f) currentOffset = 1f - currentOffset;
            float offsetError = targetOffset - currentOffset;

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

            OutLeftPhases[index] = leftPhase;
            OutRightPhases[index] = rightPhase;
            OutPhaseSpeeds[index] = frequency;
        }
    }
}