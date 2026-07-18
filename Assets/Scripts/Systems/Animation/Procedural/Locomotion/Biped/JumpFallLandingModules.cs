using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace ProjectName.Systems.Animation.Procedural.Locomotion.Biped
{
    /// <summary>
    /// Jump arc calculator: computes jump trajectory parameters from desired height.
    /// </summary>
    [BurstCompile]
    public struct JumpArcJob : IJob
    {
        [ReadOnly] public float JumpHeight;
        [ReadOnly] public float Gravity;
        [ReadOnly] public float3 InitialVelocity;
        [ReadOnly] public float3 TargetPosition; // optional target for directional jump

        public NativeArray<float> OutInitialVerticalVelocity;
        public NativeArray<float> OutTimeToApex;
        public NativeArray<float> OutTotalFlightTime;
        public NativeArray<float3> OutLaunchVelocity;

        public void Execute()
        {
            // v = sqrt(2 * g * h) for desired height
            float verticalVel = math.sqrt(2f * -Gravity * JumpHeight);
            float timeToApex = verticalVel / -Gravity;
            float totalTime = timeToApex * 2f;

            float3 launchVel = InitialVelocity;
            launchVel.y = verticalVel;

            // If target specified, adjust horizontal velocity to reach it
            if (math.lengthsq(TargetPosition) > 0.001f)
            {
                float3 toTarget = TargetPosition - float3(0f, 0f, 0f); // relative
                float3 horizontalTarget = new float3(toTarget.x, 0, toTarget.z);
                float horizontalDist = math.length(horizontalTarget);
                if (horizontalDist > 0.001f && totalTime > 0.001f)
                {
                    float3 horizVel = horizontalTarget / totalTime;
                    launchVel.x = horizVel.x;
                    launchVel.z = horizVel.z;
                }
            }

            OutInitialVerticalVelocity[0] = verticalVel;
            OutTimeToApex[0] = timeToApex;
            OutTotalFlightTime[0] = totalTime;
            OutLaunchVelocity[0] = launchVel;
        }
    }

    /// <summary>
    /// Pre-jump crouch: anticipatory squat before jump launch.
    /// </summary>
    [BurstCompile]
    public struct PreJumpCrouchJob : IJob
    {
        [ReadOnly] public float TimeUntilJump; // negative = after jump started
        [ReadOnly] public float CrouchDuration; // time before jump to start crouching
        [ReadOnly] public float MaxCrouchAngle; // degrees
        [ReadOnly] public float CrouchSpeed;

        public NativeArray<float> OutThighAngle;
        public NativeArray<float> OutShinAngle;
        public NativeArray<float> OutSpineForwardLean;

        public void Execute()
        {
            float progress = 0f;

            if (TimeUntilJump < 0f)
            {
                // Jump already started, recover
                progress = math.saturate(-TimeUntilJump / 0.2f);
                progress = 1f - progress; // reverse
            }
            else if (TimeUntilJump < CrouchDuration)
            {
                // Crouching phase
                progress = 1f - (TimeUntilJump / CrouchDuration);
                progress = math.smoothstep(0f, 1f, progress);
            }

            float crouchAmount = progress * MaxCrouchAngle;

            OutThighAngle[0] = crouchAmount * 0.6f;   // thighs forward
            OutShinAngle[0] = -crouchAmount * 0.4f;   // shins back
            OutSpineForwardLean[0] = crouchAmount * 0.3f; // spine forward
        }
    }

    /// <summary>
    /// Airborne pose: limb positions during flight.
    /// </summary>
    [BurstCompile]
    public struct AirbornePoseJob : IJob
    {
        [ReadOnly] public float TimeSinceJump;
        [ReadOnly] public float TotalFlightTime;
        [ReadOnly] public float3 BodyVelocity;
        [ReadOnly] public quaternion BodyRotation;

        public NativeArray<float3> OutLeftArmTarget;
        public NativeArray<float3> OutRightArmTarget;
        public NativeArray<float3> OutLeftLegTarget;
        public NativeArray<float3> OutRightLegTarget;
        public NativeArray<float> OutSpineLean;

        public void Execute()
        {
            float progress = math.saturate(TimeSinceJump / TotalFlightTime);

            // Arms: spread slightly for balance
            float armSpread = math.lerp(0.2f, 0.5f, progress);
            float3 forward = math.mul(BodyRotation, math.forward());
            float3 right = math.mul(BodyRotation, math.right());
            float3 up = math.up();

            OutLeftArmTarget[0] = forward * 0.3f + right * -armSpread + up * 0.5f;
            OutRightArmTarget[0] = forward * 0.3f + right * armSpread + up * 0.5f;

            // Legs: tuck during ascent, extend during descent
            float tuckAmount = progress < 0.5f
                ? math.lerp(0f, 0.4f, progress * 2f)      // tuck up
                : math.lerp(0.4f, 0f, (progress - 0.5f) * 2f); // extend down

            OutLeftLegTarget[0] = up * -tuckAmount + right * -0.1f;
            OutRightLegTarget[0] = up * -tuckAmount + right * 0.1f;

            // Spine lean based on velocity direction
            float3 horizVel = new float3(BodyVelocity.x, 0, BodyVelocity.z);
            float speed = math.length(horizVel);
            if (speed > 0.1f)
            {
                float3 velDir = math.normalize(horizVel);
                float3 localVelDir = math.mul(math.inverse(BodyRotation), velDir);
                OutSpineLean[0] = localVelDir.z * 10f; // lean into movement
            }
            else
            {
                OutSpineLean[0] = 0f;
            }
        }
    }

    /// <summary>
    /// Landing predictor: raycasts ahead to predict landing time and position.
    /// </summary>
    [BurstCompile]
    public struct LandingPredictorJob : IJob
    {
        [ReadOnly] public float3 BodyPosition;
        [ReadOnly] public float3 BodyVelocity;
        [ReadOnly] public float Gravity;
        [ReadOnly] public float GroundCheckDistance;
        [ReadOnly] public float3 Up; // world up

        // Raycast would be done externally, here we compute predicted values
        [ReadOnly] public float TimeSinceJump;
        [ReadOnly] public float TotalFlightTime;
        [ReadOnly] public bool IsGrounded;

        public NativeArray<float> OutTimeToLand;
        public NativeArray<float3> OutPredictedLandPos;
        public NativeArray<bool> OutWillLandSoon;

        public void Execute()
        {
            float timeToLand = TotalFlightTime - TimeSinceJump;
            bool willLandSoon = timeToLand < 0.3f && timeToLand > 0f && !IsGrounded;

            // Predict landing position from current velocity
            float3 predictedPos = BodyPosition + BodyVelocity * timeToLand;
            predictedPos.y = BodyPosition.y; // ground level

            OutTimeToLand[0] = math.max(0f, timeToLand);
            OutPredictedLandPos[0] = predictedPos;
            OutWillLandSoon[0] = willLandSoon;
        }
    }

    /// <summary>
    /// Impact absorption: sequential joint bending on landing.
    /// </summary>
    [BurstCompile]
    public struct ImpactAbsorptionJob : IJob
    {
        [ReadOnly] public float TimeSinceLanding;
        [ReadOnly] public float ImpactVelocity; // vertical speed at impact
        [ReadOnly] public float AbsorptionDuration; // total time to absorb

        public NativeArray<float> OutThighAngle;
        public NativeArray<float> OutShinAngle;
        public NativeArray<float> OutHipAngle;
        public NativeArray<float> OutSpineCompression;

        public void Execute()
        {
            if (TimeSinceLanding <= 0f || TimeSinceLanding > AbsorptionDuration)
            {
                OutThighAngle[0] = 0f;
                OutShinAngle[0] = 0f;
                OutHipAngle[0] = 0f;
                OutSpineCompression[0] = 0f;
                return;
            }

            float progress = TimeSinceLanding / AbsorptionDuration;
            // Ease out: fast initial bend, slow recovery
            float curve = 1f - math.pow(1f - progress, 3f);

            // Scale by impact severity
            float severity = math.saturate(ImpactVelocity / 15f); // 15 m/s = severe
            float maxBend = 40f * severity; // degrees

            OutThighAngle[0] = maxBend * curve * 0.6f;
            OutShinAngle[0] = -maxBend * curve * 0.4f;
            OutHipAngle[0] = maxBend * curve * 0.3f;
            OutSpineCompression[0] = maxBend * curve * 0.2f;
        }
    }

    /// <summary>
    /// Recovery blend: smooth transition from landing back to locomotion.
    /// </summary>
    [BurstCompile]
    public struct RecoveryBlendJob : IJob
    {
        [ReadOnly] public float TimeSinceLanding;
        [ReadOnly] public float RecoveryDuration;
        [ReadOnly] public float CurrentLegPhase;
        [ReadOnly] public float TargetLegPhase; // locomotion phase to blend to

        public NativeArray<float> OutBlendedPhase;
        public NativeArray<float> OutBlendWeight; // 0 = landing, 1 = locomotion

        public void Execute()
        {
            float blendWeight = 0f;
            if (TimeSinceLanding > 0f)
            {
                blendWeight = math.saturate(TimeSinceLanding / RecoveryDuration);
                blendWeight = math.smoothstep(0f, 1f, blendWeight);
            }

            float blendedPhase = math.lerp(CurrentLegPhase, TargetLegPhase, blendWeight);

            OutBlendedPhase[0] = blendedPhase;
            OutBlendWeight[0] = blendWeight;
        }
    }
}