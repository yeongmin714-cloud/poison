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
                    // rootPos stays fixed

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
        public Quaternion RootRot;
        public Quaternion MidRot;
        public Quaternion TipRot;
        public bool Success;
    }

    /// <summary>
    /// Blittable IK solve result for batch/parallel usage with job-compatible types.
    /// Includes computed positions and rotations for all three bones.
    /// </summary>
    public struct SolverResult
    {
        public float3 RootPos;
        public float3 MidPos;
        public float3 TipPos;
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

        /// <summary>
        /// Solve IK for multiple chains in parallel via the Job System (Burst-compiled).
        /// Extracts Transform data from each Chain on the main thread, schedules a
        /// LimbIKJob across all chains, and writes results into the SolverResult array.
        /// All input arrays must have the same length.
        /// </summary>
        /// <param name="chains">Chains with pre-computed UpperLength/LowerLength (call ComputeLengths first).</param>
        /// <param name="targets">Target positions for each chain.</param>
        /// <param name="hints">Hint/direction vectors for each chain.</param>
        /// <param name="results">Output array of solver results (positions + rotations + success).</param>
        /// <param name="iterations">FABRIK iterations per chain (default: 2).</param>
        /// <param name="allocator">Allocator for temporary NativeArrays (default: TempJob).</param>
        public static void BatchSolve(
            NativeArray<Chain> chains,
            NativeArray<Vector3> targets,
            NativeArray<Vector3> hints,
            NativeArray<SolverResult> results,
            int iterations = 2,
            Allocator allocator = Allocator.TempJob)
        {
            int count = chains.Length;
            int batchSize = math.max(1, count / SystemInfo.processorCount);

            var rootPositions = new NativeArray<float3>(count, allocator, NativeArrayOptions.UninitializedMemory);
            var midPositions = new NativeArray<float3>(count, allocator, NativeArrayOptions.UninitializedMemory);
            var tipPositions = new NativeArray<float3>(count, allocator, NativeArrayOptions.UninitializedMemory);
            var targetPositions = new NativeArray<float3>(count, allocator, NativeArrayOptions.UninitializedMemory);
            var hintPositions = new NativeArray<float3>(count, allocator, NativeArrayOptions.UninitializedMemory);
            var upperLengths = new NativeArray<float>(count, allocator, NativeArrayOptions.UninitializedMemory);
            var lowerLengths = new NativeArray<float>(count, allocator, NativeArrayOptions.UninitializedMemory);

            var outRootPositions = new NativeArray<float3>(count, allocator, NativeArrayOptions.UninitializedMemory);
            var outMidPositions = new NativeArray<float3>(count, allocator, NativeArrayOptions.UninitializedMemory);
            var outTipPositions = new NativeArray<float3>(count, allocator, NativeArrayOptions.UninitializedMemory);
            var outRootRotations = new NativeArray<quaternion>(count, allocator, NativeArrayOptions.UninitializedMemory);
            var outMidRotations = new NativeArray<quaternion>(count, allocator, NativeArrayOptions.UninitializedMemory);
            var outTipRotations = new NativeArray<quaternion>(count, allocator, NativeArrayOptions.UninitializedMemory);
            var outSuccess = new NativeArray<bool>(count, allocator, NativeArrayOptions.UninitializedMemory);

            try
            {
                // --- Main thread: extract Transform data into NativeArrays ---
                for (int i = 0; i < count; i++)
                {
                    var chain = chains[i];
                    rootPositions[i] = chain.Root.position;
                    midPositions[i] = chain.Mid.position;
                    tipPositions[i] = chain.Tip.position;
                    targetPositions[i] = targets[i];
                    hintPositions[i] = hints[i];
                    upperLengths[i] = chain.UpperLength;
                    lowerLengths[i] = chain.LowerLength;
                }

                // --- Schedule and complete the Burst-compiled parallel job ---
                var job = new LimbIKJob
                {
                    RootPositions = rootPositions,
                    MidPositions = midPositions,
                    TipPositions = tipPositions,
                    TargetPositions = targetPositions,
                    HintPositions = hintPositions,
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

                job.Schedule(count, batchSize, default).Complete();

                // --- Write results back to the output array ---
                for (int i = 0; i < count; i++)
                {
                    results[i] = new SolverResult
                    {
                        RootPos = outRootPositions[i],
                        MidPos = outMidPositions[i],
                        TipPos = outTipPositions[i],
                        RootRot = outRootRotations[i],
                        MidRot = outMidRotations[i],
                        TipRot = outTipRotations[i],
                        Success = outSuccess[i]
                    };
                }
            }
            finally
            {
                // --- Guaranteed cleanup of all temporary allocations ---
                rootPositions.Dispose();
                midPositions.Dispose();
                tipPositions.Dispose();
                targetPositions.Dispose();
                hintPositions.Dispose();
                upperLengths.Dispose();
                lowerLengths.Dispose();
                outRootPositions.Dispose();
                outMidPositions.Dispose();
                outTipPositions.Dispose();
                outRootRotations.Dispose();
                outMidRotations.Dispose();
                outTipRotations.Dispose();
                outSuccess.Dispose();
            }
        }
    }

    /// <summary>
    /// LOD-aware iterations: returns reduced iteration count based on LOD level.
    /// LOD0=full(2), LOD1=1, LOD2+=0 (approx fallback)
    /// </summary>
    public static int GetLODIterations(int lodLevel)
    {
        if (lodLevel <= 0) return 2;
        if (lodLevel == 1) return 1;
        return 0;
    }
}