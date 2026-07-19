using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace ProjectName.Systems.Animation.Procedural.Locomotion.Quadruped
{
    /// <summary>
    /// Gait selector: chooses Walk/Trot/Pace/Gallop based on speed.
    /// IJobParallelFor for batch processing multiple characters.
    /// </summary>
    [BurstCompile]
    public struct GaitSelectorJob : IJobParallelFor
    {
        public enum Gait { Walk, Trot, Pace, Gallop }

        [ReadOnly] public NativeArray<float> CurrentSpeeds;
        [ReadOnly] public NativeArray<float> MaxSpeeds;
        [ReadOnly] public NativeArray<float> WalkSpeeds;
        [ReadOnly] public NativeArray<float> TrotSpeeds;
        [ReadOnly] public NativeArray<float> PaceSpeeds;
        [ReadOnly] public NativeArray<float> GallopSpeeds;
        [ReadOnly] public NativeArray<Gait> CurrentGaits;

        [WriteOnly] public NativeArray<Gait> OutSelectedGaits;
        [WriteOnly] public NativeArray<float> OutGaitBlends;
        [WriteOnly] public NativeArray<float> OutPhaseSpeedMultipliers;

        public void Execute(int index)
        {
            float currentSpeed = CurrentSpeeds[index];
            float maxSpeed = MaxSpeeds[index];
            Gait currentGait = CurrentGaits[index];

            float normalizedSpeed = currentSpeed / maxSpeed;
            Gait targetGait = currentGait;
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
            if (targetGait != currentGait)
                blend = 0.5f;
            else
                blend = 1f;

            float phaseMult = 1f;
            switch (targetGait)
            {
                case Gait.Walk:   phaseMult = 0.8f; break;
                case Gait.Trot:   phaseMult = 1.2f; break;
                case Gait.Pace:   phaseMult = 1.4f; break;
                case Gait.Gallop: phaseMult = 2.0f; break;
            }

            OutSelectedGaits[index] = targetGait;
            OutGaitBlends[index] = blend;
            OutPhaseSpeedMultipliers[index] = phaseMult;
        }
    }

    /// <summary>
    /// Spine wave: S-curve undulation along spine for quadruped locomotion.
    /// IJobParallelFor for batch processing.
    /// Added LOD support: reduces/mutes wave based on LOD level.
    /// </summary>
    [BurstCompile]
    public struct SpineWaveJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> Times;
        [ReadOnly] public NativeArray<float> Frequencies;
        [ReadOnly] public NativeArray<float> Amplitudes;
        [ReadOnly] public NativeArray<float> Speeds;
        [ReadOnly] public NativeArray<GaitSelectorJob.Gait> CurrentGaits;
        [ReadOnly] public NativeArray<int> SpineSegmentCounts;
        [ReadOnly] public NativeArray<int> LODLevels; // 0=full, 1=reduced, 2+=disabled

        [WriteOnly] public NativeArray<quaternion> OutSpineRotations; // flattened: index * maxSegments

        [ReadOnly] public int MaxSpineSegments; // stride for flattened output

        public void Execute(int index)
        {
            int lod = LODLevels[index];

            // LOD2+: disable spine wave entirely
            if (lod >= 2)
            {
                int baseIdx = index * MaxSpineSegments;
                for (int i = 0; i < MaxSpineSegments; i++)
                    OutSpineRotations[baseIdx + i] = quaternion.identity;
                return;
            }

            float time = Times[index];
            float frequency = Frequencies[index];
            float amplitude = Amplitudes[index];
            float speed = Speeds[index];
            GaitSelectorJob.Gait currentGait = CurrentGaits[index];
            int spineCount = SpineSegmentCounts[index];

            float waveFreq = frequency;
            float waveAmp = amplitude;

            // LOD1: reduced amplitude
            if (lod == 1)
                waveAmp *= 0.5f;

            // Gait-specific wave parameters
            switch (currentGait)
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

            float phase = time * waveFreq * speed;
            int baseIdx2 = index * MaxSpineSegments;

            for (int i = 0; i < spineCount; i++)
            {
                float segmentPhase = phase + i * 0.5f;
                float wave = math.sin(segmentPhase) * waveAmp;
                float yaw = wave * (i % 2 == 0 ? 1f : -1f);
                float roll = wave * 0.3f * (i % 2 == 0 ? 1f : -1f);

                OutSpineRotations[baseIdx2 + i] = quaternion.Euler(0f, math.degrees(yaw), math.degrees(roll));
            }

            // Zero out remaining
            for (int i = spineCount; i < MaxSpineSegments; i++)
                OutSpineRotations[baseIdx2 + i] = quaternion.identity;
        }
    }

    /// <summary>
    /// Leg phase offsets for each gait pattern.
    /// IJobParallelFor for batch processing.
    /// </summary>
    [BurstCompile]
    public struct LegPhaseOffsetJob : IJobParallelFor
    {
        public enum Leg { LF, RF, LH, RH }

        [ReadOnly] public NativeArray<GaitSelectorJob.Gait> CurrentGaits;
        [ReadOnly] public NativeArray<float> BasePhases;
        [ReadOnly] public NativeArray<float> DutyCycles;

        [WriteOnly] public NativeArray<float> OutLegPhases; // 4 per character (LF, RF, LH, RH)

        public void Execute(int index)
        {
            GaitSelectorJob.Gait currentGait = CurrentGaits[index];
            float basePhase = BasePhases[index];

            float lf = 0f, rf = 0f, lh = 0f, rh = 0f;

            switch (currentGait)
            {
                case GaitSelectorJob.Gait.Walk:
                    lf = 0f;
                    rh = 0.25f;
                    rf = 0.5f;
                    lh = 0.75f;
                    break;

                case GaitSelectorJob.Gait.Trot:
                    lf = 0f;
                    rh = 0f;
                    rf = 0.5f;
                    lh = 0.5f;
                    break;

                case GaitSelectorJob.Gait.Pace:
                    lf = 0f;
                    lh = 0f;
                    rf = 0.5f;
                    rh = 0.5f;
                    break;

                case GaitSelectorJob.Gait.Gallop:
                    lh = 0f;
                    rh = 0.15f;
                    lf = 0.4f;
                    rf = 0.55f;
                    break;
            }

            int baseIdx = index * 4;
            OutLegPhases[baseIdx + 0] = math.fmod(lf + basePhase, 1f);
            OutLegPhases[baseIdx + 1] = math.fmod(rf + basePhase, 1f);
            OutLegPhases[baseIdx + 2] = math.fmod(lh + basePhase, 1f);
            OutLegPhases[baseIdx + 3] = math.fmod(rh + basePhase, 1f);
        }
    }

    /// <summary>
    /// Quadruped foot target calculator from phase.
    /// IJobParallelFor for batch processing.
    /// </summary>
    [BurstCompile]
    public struct QuadrupedFootTargetJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> BodyPositions;
        [ReadOnly] public NativeArray<quaternion> BodyRotations;
        [ReadOnly] public NativeArray<float3> BodyVelocities;
        [ReadOnly] public NativeArray<float> LegPhases;
        [ReadOnly] public NativeArray<float> DutyCycles;
        [ReadOnly] public NativeArray<float> StepLengths;
        [ReadOnly] public NativeArray<float> StepHeights;
        [ReadOnly] public NativeArray<float3> LegDefaultPositions; // relative to body
        [ReadOnly] public NativeArray<bool> IsFrontLegFlags;

        [WriteOnly] public NativeArray<float3> OutFootTargets;
        [WriteOnly] public NativeArray<float3> OutFootHints;
        [WriteOnly] public NativeArray<bool> OutIsStanceFlags;

        public void Execute(int index)
        {
            float3 bodyPos = BodyPositions[index];
            quaternion bodyRot = BodyRotations[index];
            float3 bodyVel = BodyVelocities[index];
            float legPhase = LegPhases[index];
            float dutyCycle = DutyCycles[index];
            float stepLength = StepLengths[index];
            float stepHeight = StepHeights[index];
            float3 legDefaultPos = LegDefaultPositions[index];
            bool isFrontLeg = IsFrontLegFlags[index];

            bool stance = legPhase < dutyCycle;
            OutIsStanceFlags[index] = stance;

            float3 forward = math.mul(bodyRot, math.forward());
            float3 right = math.mul(bodyRot, math.right());
            float3 up = math.up();

            float3 defaultWorld = bodyPos + math.mul(bodyRot, legDefaultPos);

            if (stance)
            {
                OutFootTargets[index] = defaultWorld;
                OutFootHints[index] = defaultWorld + right * (isFrontLeg ? 0.15f : -0.15f) + up * 0.3f;
            }
            else
            {
                float swingProgress = (legPhase - dutyCycle) / (1f - dutyCycle);
                float height = math.sin(swingProgress * math.PI) * stepHeight;
                float forwardDist = swingProgress * stepLength * math.max(0.5f, math.length(bodyVel) / 5f);

                float3 target = defaultWorld + forward * forwardDist + up * height;
                OutFootTargets[index] = target;
                OutFootHints[index] = target + right * (isFrontLeg ? 0.2f : -0.2f);
            }
        }
    }

    /// <summary>
    /// Neck/head stabilization: keeps head level during body motion.
    /// IJobParallelFor for batch processing.
    /// </summary>
    [BurstCompile]
    public struct NeckStabilizationJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<quaternion> BodyRotations;
        [ReadOnly] public NativeArray<float3> BodyAngularVelocities;
        [ReadOnly] public NativeArray<float3> HeadPositions;
        [ReadOnly] public NativeArray<float3> HeadForwards;
        [ReadOnly] public NativeArray<float3> LookTargets;
        [ReadOnly] public NativeArray<float> StabilizationStrengths;
        [ReadOnly] public NativeArray<float> LookWeights;

        [WriteOnly] public NativeArray<quaternion> OutHeadRotations;
        [WriteOnly] public NativeArray<quaternion> OutNeckRotations;

        public void Execute(int index)
        {
            quaternion bodyRot = BodyRotations[index];
            float3 headPos = HeadPositions[index];
            float3 lookTarget = LookTargets[index];
            float stabilizationStrength = StabilizationStrengths[index];
            float lookWeight = LookWeights[index];

            // Counter-rotate head to cancel body rotation
            quaternion counterRot = math.inverse(bodyRot);
            counterRot = math.slerp(quaternion.identity, counterRot, stabilizationStrength);

            // Look at target
            float3 toTarget = math.normalize(lookTarget - headPos);
            quaternion lookRot = quaternion.LookRotationSafe(toTarget, math.up());
            lookRot = math.slerp(quaternion.identity, lookRot, lookWeight);

            // Combine: stabilize first, then look
            quaternion headRot = math.mul(counterRot, lookRot);
            quaternion neckRot = math.mul(counterRot, math.slerp(quaternion.identity, lookRot, 0.5f));

            OutHeadRotations[index] = headRot;
            OutNeckRotations[index] = neckRot;
        }
    }
}