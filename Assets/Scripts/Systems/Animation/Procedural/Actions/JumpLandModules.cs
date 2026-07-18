using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace ProjectName.Systems.Animation.Procedural.Actions
{
    /// <summary>
    /// Jump arc: ballistic trajectory calculation for jump height/distance.
    /// </summary>
    [BurstCompile]
    public struct JumpArcJob : IJob
    {
        [ReadOnly] public float3 StartPosition;
        [ReadOnly] public float3 StartVelocity;
        [ReadOnly] public float Gravity;
        [ReadOnly] public float TargetHeight; // desired apex height
        [ReadOnly] public float TimeSinceJump;

        public NativeArray<float3> OutPosition;
        public NativeArray<float3> OutVelocity;
        public NativeArray<float> OutHeight;
        public NativeArray<bool> OutIsRising;
        public NativeArray<float> OutTimeToApex;
        public NativeArray<float> OutTimeToLand;

        public void Execute()
        {
            float t = TimeSinceJump;

            // Position
            float3 pos = StartPosition + StartVelocity * t + 0.5f * math.up() * Gravity * t * t;
            float3 vel = StartVelocity + math.up() * Gravity * t;
            float height = pos.y - StartPosition.y;
            bool isRising = vel.y > 0;

            // Time to apex
            float timeToApex = isRising ? -vel.y / Gravity : 0f;

            // Time to land (solve quadratic: 0 = h + v*t + 0.5*g*t^2)
            float timeToLand = 0f;
            float h = height;
            float v = vel.y;
            float disc = v * v - 2f * Gravity * h;
            if (disc >= 0)
            {
                timeToLand = (-v + math.sqrt(disc)) / Gravity;
            }

            OutPosition[0] = pos;
            OutVelocity[0] = vel;
            OutHeight[0] = height;
            OutIsRising[0] = isRising;
            OutTimeToApex[0] = timeToApex;
            OutTimeToLand[0] = timeToLand;
        }
    }

    /// <summary>
    /// Pre-jump crouch: anticipatory squat before jump.
    /// </summary>
    [BurstCompile]
    public struct PreJumpCrouchJob : IJob
    {
        public enum Phase { None, Crouch, Extend, Airborne }

        [ReadOnly] public Phase CurrentPhase;
        [ReadOnly] public float PhaseTimer;
        [ReadOnly] public float CrouchDuration;
        [ReadOnly] public float ExtendDuration;
        [ReadOnly] public float MaxCrouchDepth; // meters
        [ReadOnly] public float MaxKneeAngle; // degrees
        [ReadOnly] public float MaxHipAngle; // degrees

        public NativeArray<float> OutCrouchDepth;
        public NativeArray<float> OutKneeBend;
        public NativeArray<float> OutHipBend;
        public NativeArray<float> OutArmSwing; // degrees back
        public NativeArray<float> OutPhaseProgress;

        public void Execute()
        {
            float depth = 0f, knee = 0f, hip = 0f, arm = 0f;
            float progress = 0f;

            switch (CurrentPhase)
            {
                case Phase.Crouch:
                    progress = math.saturate(PhaseTimer / CrouchDuration);
                    float smoothCrouch = math.smoothstep(0f, 1f, progress);
                    depth = MaxCrouchDepth * smoothCrouch;
                    knee = MaxKneeAngle * smoothCrouch;
                    hip = MaxHipAngle * smoothCrouch;
                    arm = 45f * smoothCrouch; // arms swing back
                    break;

                case Phase.Extend:
                    progress = math.saturate(PhaseTimer / ExtendDuration);
                    float smoothExtend = 1f - math.smoothstep(0f, 1f, progress);
                    depth = MaxCrouchDepth * smoothExtend;
                    knee = MaxKneeAngle * smoothExtend;
                    hip = MaxHipAngle * smoothExtend;
                    arm = 45f * smoothExtend; // arms swing forward
                    break;

                case Phase.Airborne:
                    // Hold extended pose briefly
                    progress = 1f;
                    break;
            }

            OutCrouchDepth[0] = depth;
            OutKneeBend[0] = knee;
            OutHipBend[0] = hip;
            OutArmSwing[0] = arm;
            OutPhaseProgress[0] = progress;
        }
    }

    /// <summary>
    /// Airborne pose: limb positions during flight.
    /// </summary>
    [BurstCompile]
    public struct AirbornePoseJob : IJob
    {
        [ReadOnly] public float3 BodyVelocity;
        [ReadOnly] public float TimeAirborne;
        [ReadOnly] public float TimeToLand;
        [ReadOnly] public bool WasRunning; // affects arm/leg positions

        public NativeArray<float3> OutLeftFootTarget;
        public NativeArray<float3> OutRightFootTarget;
        public NativeArray<float3> OutLeftHandTarget;
        public NativeArray<float3> OutRightHandTarget;
        public NativeArray<float> OutSpineLean; // forward lean
        public NativeArray<float> OutLegSpread; // leg abduction

        public void Execute()
        {
            float speed = math.length(BodyVelocity);
            float horizontalSpeed = math.length(new float2(BodyVelocity.x, BodyVelocity.z));

            // Feet: tuck under body, slight spread for stability
            float tuckAmount = math.saturate(1f - TimeAirborne * 2f); // tuck quickly
            float spread = WasRunning ? 0.3f : 0.15f;

            float3 baseFootPos = new float3(0f, -0.9f, 0.1f); // relative to hips
            OutLeftFootTarget[0] = baseFootPos + new float3(-spread, 0f, 0f);
            OutRightFootTarget[0] = baseFootPos + new float3(spread, 0f, 0f);

            // Hands: forward for balance, or streamlined if fast
            float armForward = math.lerp(0.5f, 0.2f, math.saturate(horizontalSpeed / 5f));
            float armDown = 0.3f;
            OutLeftHandTarget[0] = new float3(-0.3f, -armDown, armForward);
            OutRightHandTarget[0] = new float3(0.3f, -armDown, armForward);

            // Spine: slight forward lean proportional to horizontal velocity
            float lean = math.atan2(horizontalSpeed, 9.8f) * 0.5f; // ~lean into velocity
            OutSpineLean[0] = math.degrees(lean);

            // Leg spread increases near landing for stability
            float landPrep = math.saturate(1f - TimeToLand / 0.3f);
            OutLegSpread[0] = spread * (1f + landPrep);
        }
    }

    /// <summary>
    /// Landing predictor: raycasts ahead to predict landing point and time.
    /// </summary>
    [BurstCompile]
    public struct LandingPredictorJob : IJob
    {
        [ReadOnly] public float3 BodyPosition;
        [ReadOnly] public float3 BodyVelocity;
        [ReadOnly] public float Gravity;
        [ReadOnly] public float GroundHeight; // would come from raycast
        [ReadOnly] public float BodyHeight; // hip height

        public NativeArray<float3> OutPredictedLandingPos;
        public NativeArray<float> OutTimeToLand;
        public NativeArray<float3> OutLandingNormal;
        public NativeArray<bool> OutWillLandSoon;

        public void Execute()
        {
            // Simple ballistic prediction
            float h = BodyPosition.y - GroundHeight - BodyHeight;
            float v = BodyVelocity.y;

            float timeToLand = 0f;
            float disc = v * v - 2f * Gravity * h;
            if (disc >= 0 && h > 0)
            {
                timeToLand = (-v + math.sqrt(disc)) / Gravity;
            }

            float3 landingPos = BodyPosition + BodyVelocity * timeToLand;
            landingPos.y = GroundHeight;

            OutPredictedLandingPos[0] = landingPos;
            OutTimeToLand[0] = timeToLand;
            OutLandingNormal[0] = math.up();
            OutWillLandSoon[0] = timeToLand > 0 && timeToLand < 0.5f;
        }
    }

    /// <summary>
    /// Impact absorption: sequential joint bending on landing.
    /// </summary>
    [BurstCompile]
    public struct ImpactAbsorptionJob : IJob
    {
        public enum Phase { Contact, Compression, Extension, Stabilize }

        [ReadOnly] public Phase CurrentPhase;
        [ReadOnly] public float PhaseTimer;
        [ReadOnly] public float ImpactVelocity; // vertical speed at contact
        [ReadOnly] public float CompressionDuration;
        [ReadOnly] public float ExtensionDuration;
        [ReadOnly] public float MaxKneeBend; // degrees
        [ReadOnly] public float MaxAnkleBend;
        [ReadOnly] public float MaxHipBend;
        [ReadOnly] public float MaxSpineBend;

        public NativeArray<float> OutKneeBend;
        public NativeArray<float> OutAnkleBend;
        public NativeArray<float> OutHipBend;
        public NativeArray<float> OutSpineBend;
        public NativeArray<float> OutGroundReactionForce; // normalized 0-1
        public NativeArray<bool> OutAbsorptionComplete;

        public void Execute()
        {
            float knee = 0f, ankle = 0f, hip = 0f, spine = 0f, grf = 0f;
            bool complete = false;

            float totalDur = CompressionDuration + ExtensionDuration;
            float progress = math.saturate(PhaseTimer / totalDur);

            if (CurrentPhase == Phase.Contact)
            {
                // Initial impact - instant bend
                float intensity = math.saturate(ImpactVelocity / 10f);
                knee = MaxKneeBend * 0.3f * intensity;
                ankle = MaxAnkleBend * 0.3f * intensity;
                hip = MaxHipBend * 0.2f * intensity;
                spine = MaxSpineBend * 0.1f * intensity;
                grf = intensity;
            }
            else if (CurrentPhase == Phase.Compression)
            {
                // Deepening bend
                float p = math.saturate(PhaseTimer / CompressionDuration);
                float smoothP = math.smoothstep(0f, 1f, p);
                float intensity = math.saturate(ImpactVelocity / 10f);
                knee = MaxKneeBend * smoothP * intensity;
                ankle = MaxAnkleBend * smoothP * intensity;
                hip = MaxHipBend * smoothP * intensity * 0.7f;
                spine = MaxSpineBend * smoothP * intensity * 0.5f;
                grf = intensity * (1f + p);
            }
            else if (CurrentPhase == Phase.Extension)
            {
                // Spring back
                float p = math.saturate(PhaseTimer / ExtensionDuration);
                float smoothP = 1f - math.smoothstep(0f, 1f, p);
                float intensity = math.saturate(ImpactVelocity / 10f);
                knee = MaxKneeBend * smoothP * intensity;
                ankle = MaxAnkleBend * smoothP * intensity;
                hip = MaxHipBend * smoothP * intensity * 0.7f;
                spine = MaxSpineBend * smoothP * intensity * 0.5f;
                grf = intensity * (1f - p);
            }
            else if (CurrentPhase == Phase.Stabilize)
            {
                // Settle
                knee = 0f; ankle = 0f; hip = 0f; spine = 0f; grf = 1f;
                complete = true;
            }

            OutKneeBend[0] = knee;
            OutAnkleBend[0] = ankle;
            OutHipBend[0] = hip;
            OutSpineBend[0] = spine;
            OutGroundReactionForce[0] = grf;
            OutAbsorptionComplete[0] = complete;
        }
    }

    /// <summary>
    /// Recovery blend: smooth transition from landing back to locomotion.
    /// </summary>
    [BurstCompile]
    public struct RecoveryBlendJob : IJob
    {
        [ReadOnly] public float TimeSinceLand;
        [ReadOnly] public float RecoveryDuration;
        [ReadOnly] public float LeftPhase;
        [ReadOnly] public float RightPhase;
        [ReadOnly] public float DutyCycle;
        [ReadOnly] public float3 BodyVelocity;

        public NativeArray<float> OutLocomotionWeight; // 0-1
        public NativeArray<float> OutLandingWeight; // 1-0
        public NativeArray<float> OutLeftPhaseCorrection;
        public NativeArray<float> OutRightPhaseCorrection;

        public void Execute()
        {
            float progress = math.saturate(TimeSinceLand / RecoveryDuration);
            float smoothProgress = math.smoothstep(0f, 1f, progress);

            float locomotionW = smoothProgress;
            float landingW = 1f - smoothProgress;

            // Phase correction: align phases to current velocity
            float speed = math.length(BodyVelocity);
            float targetPhaseSpeed = math.lerp(0.5f, 2.5f, math.saturate(speed / 7f));

            // Small correction to sync with velocity
            float leftCorr = 0f, rightCorr = 0f;
            if (speed > 0.5f)
            {
                // Nudge phases toward natural rhythm
                float idealLeft = LeftPhase < DutyCycle ? 0.25f : 0.75f;
                float idealRight = RightPhase < DutyCycle ? 0.75f : 0.25f;
                leftCorr = (idealLeft - LeftPhase) * 0.1f * landingW;
                rightCorr = (idealRight - RightPhase) * 0.1f * landingW;
            }

            OutLocomotionWeight[0] = locomotionW;
            OutLandingWeight[0] = landingW;
            OutLeftPhaseCorrection[0] = leftCorr;
            OutRightPhaseCorrection[0] = rightCorr;
        }
    }
}