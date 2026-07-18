using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace ProjectName.Systems.Animation.Procedural.Locomotion.Quadruped
{
    /// <summary>
    /// Gait selector: chooses Walk/Trot/Pace/Gallop based on speed.
    /// </summary>
    [BurstCompile]
    public struct GaitSelectorJob : IJob
    {
        public enum Gait { Walk, Trot, Pace, Gallop }

        [ReadOnly] public float CurrentSpeed;
        [ReadOnly] public float MaxSpeed;
        [ReadOnly] public float WalkSpeed;
        [ReadOnly] public float TrotSpeed;
        [ReadOnly] public float PaceSpeed;
        [ReadOnly] public float GallopSpeed;
        [ReadOnly] public Gait CurrentGait;

        public NativeArray<Gait> OutSelectedGait;
        public NativeArray<float> OutGaitBlend; // 0-1 for smooth transitions
        public NativeArray<float> OutPhaseSpeedMultiplier;

        public void Execute()
        {
            float normalizedSpeed = CurrentSpeed / MaxSpeed;
            Gait targetGait = CurrentGait;
            float blend = 0f;

            if (normalizedSpeed < 0.25f)
                targetGait = Gait.Walk;
            else if (normalizedSpeed < 0.5f)
                targetGait = Gait.Trot;
            else if (normalizedSpeed < 0.75f)
                targetGait = Gait.Pace;
            else
                targetGait = Gait.Gallop;

            // Smooth transition
            if (targetGait != CurrentGait)
            {
                blend = 0.5f; // transitioning
            }
            else
            {
                blend = 1f; // stable
            }

            float phaseMult = 1f;
            switch (targetGait)
            {
                case Gait.Walk:   phaseMult = 0.8f; break;
                case Gait.Trot:   phaseMult = 1.2f; break;
                case Gait.Pace:   phaseMult = 1.4f; break;
                case Gait.Gallop: phaseMult = 2.0f; break;
            }

            OutSelectedGait[0] = targetGait;
            OutGaitBlend[0] = blend;
            OutPhaseSpeedMultiplier[0] = phaseMult;
        }
    }

    /// <summary>
    /// Spine wave: S-curve undulation along spine for quadruped locomotion.
    /// </summary>
    [BurstCompile]
    public struct SpineWaveJob : IJob
    {
        [ReadOnly] public float Time;
        [ReadOnly] public float Frequency;
        [ReadOnly] public float Amplitude;
        [ReadOnly] public float Speed;
        [ReadOnly] public GaitSelectorJob.Gait CurrentGait;
        [ReadOnly] public int SpineSegmentCount; // typically 3-5

        public NativeArray<quaternion> OutSpineRotations; // length = SpineSegmentCount

        public void Execute()
        {
            float waveFreq = Frequency;
            float waveAmp = Amplitude;

            // Gait-specific wave parameters
            switch (CurrentGait)
            {
                case GaitSelectorJob.Gait.Walk:
                    waveFreq *= 0.8f;
                    waveAmp *= 0.5f;
                    break;
                case GaitSelectorJob.Gait.Trot:
                    waveFreq *= 1.0f;
                    waveAmp *= 1.0f;
                    break;
                case GaitSelectorJob.Gait.Pace:
                    waveFreq *= 1.2f;
                    waveAmp *= 0.8f;
                    break;
                case GaitSelectorJob.Gait.Gallop:
                    waveFreq *= 1.5f;
                    waveAmp *= 1.2f;
                    break;
            }

            float phase = Time * waveFreq * Speed;

            for (int i = 0; i < SpineSegmentCount; i++)
            {
                float segmentPhase = phase + i * 0.5f; // phase lag along spine
                float wave = math.sin(segmentPhase) * waveAmp;
                // Alternate yaw/roll for S-curve
                float yaw = wave * (i % 2 == 0 ? 1f : -1f);
                float roll = wave * 0.3f * (i % 2 == 0 ? 1f : -1f);

                OutSpineRotations[i] = quaternion.Euler(0f, math.degrees(yaw), math.degrees(roll));
            }
        }
    }

    /// <summary>
    /// Leg phase offsets for each gait pattern.
    /// </summary>
    [BurstCompile]
    public struct LegPhaseOffsetJob : IJob
    {
        public enum Leg { LF, RF, LH, RH } // LeftFront, RightFront, LeftHind, RightHind

        [ReadOnly] public GaitSelectorJob.Gait CurrentGait;
        [ReadOnly] public float BasePhase; // 0-1 master phase
        [ReadOnly] public float DutyCycle; // stance fraction

        public NativeArray<float> OutLegPhases; // length 4: LF, RF, LH, RH

        public void Execute()
        {
            float lf = 0f, rf = 0f, lh = 0f, rh = 0f;

            switch (CurrentGait)
            {
                case GaitSelectorJob.Gait.Walk:
                    // 4-beat: LF, RH, RF, LH (0, 0.25, 0.5, 0.75)
                    lf = 0f;
                    rh = 0.25f;
                    rf = 0.5f;
                    lh = 0.75f;
                    break;

                case GaitSelectorJob.Gait.Trot:
                    // 2-beat diagonal: LF+RH, RF+LH
                    lf = 0f;
                    rh = 0f;
                    rf = 0.5f;
                    lh = 0.5f;
                    break;

                case GaitSelectorJob.Gait.Pace:
                    // 2-beat lateral: LF+LH, RF+RH
                    lf = 0f;
                    lh = 0f;
                    rf = 0.5f;
                    rh = 0.5f;
                    break;

                case GaitSelectorJob.Gait.Gallop:
                    // 4-beat asymmetric (rotary): LH, RH, LF, RF
                    // Lead leg depends on turn direction; assume right lead
                    lh = 0f;
                    rh = 0.15f;
                    lf = 0.4f;
                    rf = 0.55f;
                    break;
            }

            // Apply base phase
            OutLegPhases[0] = math.fmod(lf + BasePhase, 1f);
            OutLegPhases[1] = math.fmod(rf + BasePhase, 1f);
            OutLegPhases[2] = math.fmod(lh + BasePhase, 1f);
            OutLegPhases[3] = math.fmod(rh + BasePhase, 1f);
        }
    }

    /// <summary>
    /// Quadruped foot target calculator from phase.
    /// </summary>
    [BurstCompile]
    public struct QuadrupedFootTargetJob : IJob
    {
        [ReadOnly] public float3 BodyPosition;
        [ReadOnly] public quaternion BodyRotation;
        [ReadOnly] public float3 BodyVelocity;
        [ReadOnly] public float LegPhase; // 0-1
        [ReadOnly] public float DutyCycle;
        [ReadOnly] public float StepLength;
        [ReadOnly] public float StepHeight;
        [ReadOnly] public float3 LegDefaultPos; // relative to body
        [ReadOnly] public bool IsFrontLeg;

        public NativeArray<float3> OutFootTarget;
        public NativeArray<float3> OutFootHint;
        public NativeArray<bool> OutIsStance;

        public void Execute()
        {
            bool stance = LegPhase < DutyCycle;
            OutIsStance[0] = stance;

            float3 forward = math.mul(BodyRotation, math.forward());
            float3 right = math.mul(BodyRotation, math.right());
            float3 up = math.up();

            float3 defaultWorld = BodyPosition + math.mul(BodyRotation, LegDefaultPos);

            if (stance)
            {
                // Stance: foot stays planted, hint is knee position
                OutFootTarget[0] = defaultWorld;
                OutFootHint[0] = defaultWorld + right * (IsFrontLeg ? 0.15f : -0.15f) + up * 0.3f;
            }
            else
            {
                // Swing: arc forward and up
                float swingProgress = (LegPhase - DutyCycle) / (1f - DutyCycle);
                float height = math.sin(swingProgress * math.PI) * StepHeight;
                float forwardDist = swingProgress * StepLength * math.max(0.5f, math.length(BodyVelocity) / 5f);

                float3 target = defaultWorld + forward * forwardDist + up * height;
                OutFootTarget[0] = target;
                OutFootHint[0] = target + right * (IsFrontLeg ? 0.2f : -0.2f);
            }
        }
    }

    /// <summary>
    /// Neck/head stabilization: keeps head level during body motion.
    /// </summary>
    [BurstCompile]
    public struct NeckStabilizationJob : IJob
    {
        [ReadOnly] public quaternion BodyRotation;
        [ReadOnly] public float3 BodyAngularVelocity;
        [ReadOnly] public float3 HeadPosition;
        [ReadOnly] public float3 HeadForward;
        [ReadOnly] public float3 LookTarget; // world position to look at
        [ReadOnly] public float StabilizationStrength; // 0-1
        [ReadOnly] public float LookWeight; // 0-1

        public NativeArray<quaternion> OutHeadRotation;
        public NativeArray<quaternion> OutNeckRotation;

        public void Execute()
        {
            // Counter-rotate head to cancel body rotation
            quaternion counterRot = quaternion.inverse(BodyRotation);
            counterRot = math.slerp(quaternion.identity, counterRot, StabilizationStrength);

            // Look at target
            float3 toTarget = math.normalize(LookTarget - HeadPosition);
            quaternion lookRot = quaternion.LookRotationSafe(toTarget, math.up());
            lookRot = math.slerp(quaternion.identity, lookRot, LookWeight);

            // Combine: stabilize first, then look
            quaternion headRot = math.mul(counterRot, lookRot);
            quaternion neckRot = math.mul(counterRot, math.slerp(quaternion.identity, lookRot, 0.5f));

            OutHeadRotation[0] = headRot;
            OutNeckRotation[0] = neckRot;
        }
    }
}