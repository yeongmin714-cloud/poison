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
                    // Root stays fixed
                    rootToMid = midPos - rootPos;
                    rootMidDist = math.length(rootToMid);
                    if (rootMidDist > 0.0001f)
                        midPos = rootPos + math.normalize(rootToMid) * upperLen;

                    // Mid -> Tip constraint
                    midToTip = tipPos - midPos;
                    midTipDist = math.length(midToTip);
                    if (midTipDist > 0.0001f)
                        tipPos = midPos + math.normalize(midToTip) * lowerLen;
                }

                // CCD refinement for hint alignment (knee/elbow direction)
                AlignToHint(ref midPos, rootPos, upperLen, hint);
            }

            // Compute rotations
            quaternion rootRot = quaternion.identity;
            quaternion midRot = quaternion.identity;
            quaternion tipRot = quaternion.identity;

            if (math.distance(rootPos, midPos) > 0.0001f)
            {
                float3 rootToMidDir = math.normalize(midPos - rootPos);
                rootRot = quaternion.LookRotationSafe(rootToMidDir, math.up());
            }

            if (math.distance(midPos, tipPos) > 0.0001f)
            {
                float3 midToTipDir = math.normalize(tipPos - midPos);
                midRot = quaternion.LookRotationSafe(midToTipDir, math.up());
            }

            OutRootPositions[index] = rootPos;
            OutMidPositions[index] = midPos;
            OutTipPositions[index] = tipPos;
            OutRootRotations[index] = rootRot;
            OutMidRotations[index] = midRot;
            OutTipRotations[index] = tipRot;
            OutSuccess[index] = success;
        }

        /// <summary>
        /// CCD single iteration: rotate mid joint around root to align with hint direction.
        /// </summary>
        private void AlignToHint(ref float3 midPos, float3 rootPos, float upperLen, float3 hint)
        {
            float3 toHint = math.normalize(hint - rootPos);
            float3 toMid = math.normalize(midPos - rootPos);

            float3 planeNormal = math.cross(toMid, toHint);
            float planeNormalLen = math.length(planeNormal);

            if (planeNormalLen > 0.001f)
            {
                float angle = math.asin(math.min(1f, planeNormalLen));
                float3 axis = math.normalize(planeNormal);
                angle = math.clamp(angle, -math.radians(30f), math.radians(30f));

                quaternion rot = quaternion.AxisAngle(axis, angle);
                midPos = rootPos + math.mul(rot, midPos - rootPos);
            }
        }
    }

    /// <summary>
    /// Single limb IK solver for non-parallel use (e.g., from MonoBehaviour).
    /// </summary>
    [BurstCompile]
    public static class LimbIKSolver
    {
        public struct Chain
        {
            public Transform Root;
            public Transform Mid;
            public Transform Tip;
            public float UpperLength;
            public float LowerLength;
        }

        public struct Result
        {
            public Vector3 RootPos;
            public Vector3 MidPos;
            public Vector3 TipPos;
            public Quaternion RootRot;
            public Quaternion MidRot;
            public Quaternion TipRot;
            public bool Success;
        }

        /// <summary>
        /// Compute bone lengths once at startup.
        /// </summary>
        public static void ComputeLengths(ref Chain chain)
        {
            if (chain.Root != null && chain.Mid != null)
                chain.UpperLength = Vector3.Distance(chain.Root.position, chain.Mid.position);
            if (chain.Mid != null && chain.Tip != null)
                chain.LowerLength = Vector3.Distance(chain.Mid.position, chain.Tip.position);
        }

        /// <summary>
        /// Solve 2-bone IK (FABRIK 2-pass + CCD hint).
        /// </summary>
        public static Result Solve(Chain chain, Vector3 target, Vector3 hint, int iterations = 2)
        {
            var result = new Result { Success = false };

            if (chain.Root == null || chain.Mid == null || chain.Tip == null)
                return result;

            Vector3 rootPos = chain.Root.position;
            Vector3 midPos = chain.Mid.position;
            Vector3 tipPos = chain.Tip.position;

            float upperLen = chain.UpperLength;
            float lowerLen = chain.LowerLength;
            float totalLen = upperLen + lowerLen;

            Vector3 rootToTarget = target - rootPos;
            float distToTarget = rootToTarget.magnitude;

            // Unreachable: fully extend
            if (distToTarget > totalLen * 0.999f)
            {
                Vector3 dir = rootToTarget.normalized;
                midPos = rootPos + dir * upperLen;
                tipPos = midPos + dir * lowerLen;
            }
            else
            {
                // FABRIK iterations
                for (int i = 0; i < iterations; i++)
                {
                    // Forward: tip reaches target
                    tipPos = target;

                    // Mid -> Tip
                    Vector3 midToTip = tipPos - midPos;
                    float midTipDist = midToTip.magnitude;
                    if (midTipDist > 0.0001f)
                        midPos = tipPos - midToTip.normalized * lowerLen;
                    else
                        midPos = tipPos - Vector3.up * lowerLen;

                    // Root -> Mid
                    Vector3 rootToMid = midPos - rootPos;
                    float rootMidDist = rootToMid.magnitude;
                    if (rootMidDist > 0.0001f)
                        midPos = rootPos + rootToMid.normalized * upperLen;
                    else
                        midPos = rootPos + Vector3.up * upperLen;

                    // Backward: root fixed
                    rootToMid = midPos - rootPos;
                    rootMidDist = rootToMid.magnitude;
                    if (rootMidDist > 0.0001f)
                        midPos = rootPos + rootToMid.normalized * upperLen;

                    // Mid -> Tip
                    midToTip = tipPos - midPos;
                    midTipDist = midToTip.magnitude;
                    if (midTipDist > 0.0001f)
                        tipPos = midPos + midToTip.normalized * lowerLen;
                }

                // CCD hint alignment
                AlignToHint(ref midPos, rootPos, upperLen, hint);
            }

            // Compute rotations
            Quaternion rootRot = chain.Root.rotation;
            Quaternion midRot = chain.Mid.rotation;
            Quaternion tipRot = chain.Tip.rotation;

            if (Vector3.Distance(rootPos, midPos) > 0.0001f)
            {
                Vector3 rootToMidDir = (midPos - rootPos).normalized;
                rootRot = Quaternion.LookRotation(rootToMidDir, Vector3.up);
            }

            if (Vector3.Distance(midPos, tipPos) > 0.0001f)
            {
                Vector3 midToTipDir = (tipPos - midPos).normalized;
                midRot = Quaternion.LookRotation(midToTipDir, Vector3.up);
            }

            result.RootPos = rootPos;
            result.MidPos = midPos;
            result.TipPos = tipPos;
            result.RootRot = rootRot;
            result.MidRot = midRot;
            result.TipRot = tipRot;
            result.Success = true;

            return result;
        }

        private static void AlignToHint(ref Vector3 midPos, Vector3 rootPos, float upperLen, Vector3 hint)
        {
            Vector3 toHint = (hint - rootPos).normalized;
            Vector3 toMid = (midPos - rootPos).normalized;

            Vector3 planeNormal = Vector3.Cross(toMid, toHint);
            float planeNormalLen = planeNormal.magnitude;

            if (planeNormalLen > 0.001f)
            {
                float angle = Mathf.Asin(Mathf.Min(1f, planeNormalLen));
                Vector3 axis = planeNormal / planeNormalLen;
                angle = Mathf.Clamp(angle, -Mathf.Deg2Rad * 30f, Mathf.Deg2Rad * 30f);

                Quaternion rot = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, axis);
                midPos = rootPos + rot * (midPos - rootPos);
            }
        }

        /// <summary>
        /// Batch solve multiple limbs in parallel using Job System.
        /// </summary>
        public static JobHandle SolveBatch(
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
}