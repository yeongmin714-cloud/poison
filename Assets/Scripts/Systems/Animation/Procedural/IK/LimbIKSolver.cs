using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace ProjectName.Systems.Animation.Procedural.IK
{
    /// <summary>
    /// Burst-compiled 2-bone IK solver (FABRIK + CCD hybrid).
    /// Designed for parallel execution via Job System.
    /// </summary>
    [BurstCompile]
    public struct LimbIKJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> RootPositions;
        [ReadOnly] public NativeArray<float3> MidPositions;
        [ReadOnly] public NativeArray<float3> TipPositions;
        [ReadOnly] public NativeArray<float3> TargetPositions;
        [ReadOnly] public NativeArray<float3> HintPositions;
        [ReadOnly] public NativeArray<float> UpperLengths;
        [ReadOnly] public NativeArray<float> LowerLengths;

        [WriteOnly] public NativeArray<float3> OutRootPositions;
        [WriteOnly] public NativeArray<float3> OutMidPositions;
        [WriteOnly] public NativeArray<float3> OutTipPositions;
        [WriteOnly] public NativeArray<quaternion> OutRootRotations;
        [WriteOnly] public NativeArray<quaternion> OutMidRotations;
        [WriteOnly] public NativeArray<quaternion> OutTipRotations;
        [WriteOnly] public NativeArray<bool> OutSuccess;

        public int Iterations;

        public void Execute(int index)
        {
            float3 rootPos = RootPositions[index];
            float3 midPos = MidPositions[index];
            float3 tipPos = TipPositions[index];
            float3 target = TargetPositions[index];
            float3 hint = HintPositions[index];

            float upperLen = UpperLengths[index];
            float lowerLen = LowerLengths[index];
            float totalLen = upperLen + lowerLen;

            float3 rootToTarget = target - rootPos;
            float distToTarget = math.length(rootToTarget);

            bool success = true;

            // Unreachable: fully extend
            if (distToTarget > totalLen * 0.999f)
            {
                float3 dir = math.normalize(rootToTarget);
                midPos = rootPos + dir * upperLen;
                tipPos = midPos + dir * lowerLen;
                success = false;
            }
            else
            {
                // FABRIK forward + backward passes
                for (int i = 0; i < Iterations; i++)
                {
                    // Stage 1: Forward reaching (tip to target)
                    tipPos = target;

                    // Mid -> Tip constraint
                    float3 midToTip = tipPos - midPos;
                    float midTipDist = math.length(midToTip);
                    if (midTipDist > 0.0001f)
                        midPos = tipPos - math.normalize(midToTip) * lowerLen;
                    else
                        midPos = tipPos - math.up() * lowerLen;

                    // Root -> Mid constraint
                    float3 rootToMid = midPos - rootPos;
                    float rootMidDist = math.length(rootToMid);
                    if (rootMidDist > 0.0001f)
                        midPos = rootPos + math.normalize(rootToMid) * upperLen;
                    else
                        midPos = rootPos + math.up() * upperLen;

                    // Stage 2: Backward reaching (root fixed)
                    rootPos = rootPos; // Root stays fixed

                    // Root -> Mid constraint
                    rootToMid = midPos - rootPos;
                    rootMidDist = math.length(rootToMid);
                    if (rootMidDist > 0.0001f)
                        midPos = rootPos + math.normalize(rootToMid) * upperLen;
                    else
                        midPos = rootPos + math.up() * upperLen;

                    // Mid -> Tip constraint
                    midToTip = tipPos - midPos;
                    midTipDist = math.length(midToTip);
                    if (midTipDist > 0.0001f)
                        tipPos = midPos + math.normalize(midToTip) * lowerLen;
                    else
                        tipPos = midPos + math.up() * lowerLen;
                }
            }

            // Compute rotations
            quaternion rootRot = quaternion.LookRotationSafe(math.normalize(midPos - rootPos), math.up());
            quaternion midRot = quaternion.LookRotationSafe(math.normalize(tipPos - midPos), math.up());
            quaternion tipRot = quaternion.LookRotationSafe(math.normalize(tipPos - midPos), math.up());

            OutRootPositions[index] = rootPos;
            OutMidPositions[index] = midPos;
            OutTipPositions[index] = tipPos;
            OutRootRotations[index] = rootRot;
            OutMidRotations[index] = midRot;
            OutTipRotations[index] = tipRot;
            OutSuccess[index] = success;
        }
    }

    /// <summary>
    /// Chain definition for single-instance IK.
    /// </summary>
    public struct Chain
    {
        public Transform Root;
        public Transform Mid;
        public Transform Tip;
        public float UpperLength;
        public float LowerLength;
    }

    /// <summary>
    /// IK Solve result for single instance.
    /// </summary>
    public struct SolveResult
    {
        public quaternion RootRot;
        public quaternion MidRot;
        public quaternion TipRot;
        public bool Success;
    }

    /// <summary>
    /// Static IK solver utilities.
    /// </summary>
    public static class LimbIKSolver
    {
        /// <summary>
        /// Pre-compute bone lengths from Transforms.
        /// </summary>
        public static void ComputeLengths(ref Chain chain)
        {
            if (chain.Root != null && chain.Mid != null)
                chain.UpperLength = Vector3.Distance(chain.Root.position, chain.Mid.position);
            if (chain.Mid != null && chain.Tip != null)
                chain.LowerLength = Vector3.Distance(chain.Mid.position, chain.Tip.position);
        }

        /// <summary>
        /// Solve 2-bone IK for a single chain.
        /// </summary>
        public static SolveResult Solve(Chain chain, Vector3 target, Vector3 hint, int iterations = 2)
        {
            if (chain.Root == null || chain.Mid == null || chain.Tip == null)
                return new SolveResult { Success = false };

            Vector3 rootPos = chain.Root.position;
            Vector3 midPos = chain.Mid.position;
            Vector3 tipPos = chain.Tip.position;

            float upperLen = chain.UpperLength;
            float lowerLen = chain.LowerLength;
            float totalLen = upperLen + lowerLen;

            Vector3 rootToTarget = target - rootPos;
            float distToTarget = rootToTarget.magnitude;

            bool success = true;

            if (distToTarget > totalLen * 0.999f)
            {
                Vector3 dir = rootToTarget.normalized;
                midPos = rootPos + dir * upperLen;
                tipPos = midPos + dir * lowerLen;
                success = false;
            }
            else
            {
                for (int i = 0; i < iterations; i++)
                {
                    tipPos = target;

                    Vector3 midToTip = tipPos - midPos;
                    float midTipDist = midToTip.magnitude;
                    if (midTipDist > 0.0001f)
                        midPos = tipPos - midToTip.normalized * lowerLen;
                    else
                        midPos = tipPos + Vector3.up * lowerLen;

                    Vector3 rootToMid = midPos - rootPos;
                    float rootMidDist = rootToMid.magnitude;
                    if (rootMidDist > 0.0001f)
                        midPos = rootPos + rootToMid.normalized * upperLen;
                    else
                        midPos = rootPos + Vector3.up * upperLen;

                    midToTip = tipPos - midPos;
                    midTipDist = midToTip.magnitude;
                    if (midTipDist > 0.0001f)
                        tipPos = midPos + midToTip.normalized * lowerLen;
                    else
                        tipPos = midPos + Vector3.up * lowerLen;
                }
            }

            Quaternion rootRot = Quaternion.LookRotation((midPos - rootPos).normalized, Vector3.up);
            Quaternion midRot = Quaternion.LookRotation((tipPos - midPos).normalized, Vector3.up);
            Quaternion tipRot = Quaternion.LookRotation((tipPos - midPos).normalized, Vector3.up);

            return new SolveResult
            {
                RootRot = rootRot,
                MidRot = midRot,
                TipRot = tipRot,
                Success = success
            };
        }

        /// <summary>
        /// Schedule parallel IK for multiple limbs.
        /// </summary>
        public static JobHandle ScheduleParallel(
            NativeArray<float3> rootPositions,
            NativeArray<float3> midPositions,
            NativeArray<float3> tipPositions,
            NativeArray<float3> targets,
            NativeArray<float3> hints,
            NativeArray<float> upperLengths,
            NativeArray<float> lowerLengths,
            NativeArray<float3> outRootPositions,
            NativeArray<float3> outMidPositions,
            NativeArray<float3> outTipPositions,
            NativeArray<quaternion> outRootRotations,
            NativeArray<quaternion> outMidRotations,
            NativeArray<quaternion> outTipRotations,
            NativeArray<bool> outSuccess,
            int iterations,
            JobHandle dependency = default)
        {
            int count = rootPositions.Length;
            var job = new LimbIKJob
            {
                RootPositions = rootPositions,
                MidPositions = midPositions,
                TipPositions = tipPositions,
                TargetPositions = targets,
                HintPositions = hints,
                UpperLengths = upperLengths,
                LowerLengths = lowerLengths,
                OutRootPositions = outRootPositions,
                OutMidPositions = outMidPositions,
                OutTipPositions = outTipPositions,
                OutRootRotations = outRootRotations,
                OutMidRotations = outMidRotations,
                OutTipRotations = outTipRotations,
                OutSuccess = outSuccess,
                Iterations = iterations
            };
            return job.Schedule(count, 32, dependency);
        }
    }

    // ──────────────────────────────────────────────
    // Single-instance IK Job (for ProceduralAnimationController)
    // ──────────────────────────────────────────────

    /// <summary>
    /// Single-instance version of LimbIKJob for use in main controller.
    /// </summary>
    [BurstCompile]
    public struct LimbIKSingleJob : IJob
    {
        [ReadOnly] public float3 RootPos;
        [ReadOnly] public float3 MidPos;
        [ReadOnly] public float3 TipPos;
        [ReadOnly] public float3 TargetPos;
        [ReadOnly] public float3 HintPos;
        [ReadOnly] public float UpperLen;
        [ReadOnly] public float LowerLen;
        [ReadOnly] public int Iterations;

        public float3 OutRootPos;
        public float3 OutMidPos;
        public float3 OutTipPos;
        public quaternion OutRootRot;
        public quaternion OutMidRot;
        public quaternion OutTipRot;
        public bool OutSuccess;

        public void Execute()
        {
            float3 rootPos = RootPos;
            float3 midPos = MidPos;
            float3 tipPos = TipPos;
            float3 target = TargetPos;
            float3 hint = HintPos;

            float upperLen = UpperLen;
            float lowerLen = LowerLen;
            float totalLen = upperLen + lowerLen;

            float3 rootToTarget = target - rootPos;
            float distToTarget = math.length(rootToTarget);

            bool success = true;

            if (distToTarget > totalLen * 0.999f)
            {
                float3 dir = math.normalize(rootToTarget);
                midPos = rootPos + dir * upperLen;
                tipPos = midPos + dir * lowerLen;
                success = false;
            }
            else
            {
                for (int i = 0; i < Iterations; i++)
                {
                    tipPos = target;

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

                    float3 midToTip2 = tipPos - midPos;
                    float midTipDist2 = math.length(midToTip2);
                    if (midTipDist2 > 0.0001f)
                        tipPos = midPos + math.normalize(midToTip2) * lowerLen;
                    else
                        tipPos = midPos + math.up() * lowerLen;
                }
            }

            quaternion rootRot = quaternion.LookRotationSafe(math.normalize(midPos - rootPos), math.up());
            quaternion midRot = quaternion.LookRotationSafe(math.normalize(tipPos - midPos), math.up());
            quaternion tipRot = quaternion.LookRotationSafe(math.normalize(tipPos - midPos), math.up());

            OutRootPos = rootPos;
            OutMidPos = midPos;
            OutTipPos = tipPos;
            OutRootRot = rootRot;
            OutMidRot = midRot;
            OutTipRot = tipRot;
            OutSuccess = success;
        }
    }
}