using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 프로시저럴 IK 솔버 (FABRIK + CCD 하이브리드).
    /// Burst/Job System 호환을 위해 순수 C# + struct 기반.
    /// </summary>
    public static class LimbIKSolver
    {
        public struct Chain
        {
            public Transform Root;
            public Transform Mid;
            public Transform Tip;

            public float UpperLength;
            public float LowerLength;
            public float TotalLength => UpperLength + LowerLength;
        }

        public struct SolverResult
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
        /// 3-본 체인 IK 풀이 (FABRIK 2회 반복 + CCD 마무리).
        /// </summary>
        public static SolverResult Solve(Chain chain, Vector3 target, Vector3 hint, int iterations = 2)
        {
            var result = new SolverResult { Success = false };

            if (chain.Root == null || chain.Mid == null || chain.Tip == null)
                return result;

            // Initial positions
            Vector3 rootPos = chain.Root.position;
            Vector3 midPos = chain.Mid.position;
            Vector3 tipPos = chain.Tip.position;

            Vector3 rootToTarget = target - rootPos;
            float distToTarget = rootToTarget.magnitude;

            // 도달 불가능하면 최대한 뻗기
            if (distToTarget > chain.TotalLength * 0.99f)
            {
                Vector3 dir = rootToTarget.normalized;
                midPos = rootPos + dir * chain.UpperLength;
                tipPos = midPos + dir * chain.LowerLength;
            }
            else
            {
                // FABRIK Forward reaching
                for (int i = 0; i < iterations; i++)
                {
                    // Stage 1: Forward (tip to target)
                    tipPos = target;

                    // Mid -> Tip
                    Vector3 midToTip = tipPos - midPos;
                    float midTipDist = midToTip.magnitude;
                    if (midTipDist > 0.0001f)
                        midPos = tipPos - midToTip.normalized * chain.LowerLength;
                    else
                        midPos = tipPos - Vector3.up * chain.LowerLength;

                    // Root -> Mid
                    Vector3 rootToMid = midPos - rootPos;
                    float rootMidDist = rootToMid.magnitude;
                    if (rootMidDist > 0.0001f)
                        midPos = rootPos + rootToMid.normalized * chain.UpperLength;
                    else
                        midPos = rootPos + Vector3.up * chain.UpperLength;

                    // Stage 2: Backward (root fixed)
                    // Root stays
                    // Root -> Mid
                    rootToMid = midPos - rootPos;
                    rootMidDist = rootToMid.magnitude;
                    if (rootMidDist > 0.0001f)
                        midPos = rootPos + rootToMid.normalized * chain.UpperLength;

                    // Mid -> Tip
                    midToTip = tipPos - midPos;
                    midTipDist = midToTip.magnitude;
                    if (midTipDist > 0.0001f)
                        tipPos = midPos + midToTip.normalized * chain.LowerLength;
                }

                // CCD refinement for hint alignment
                AlignToHint(ref midPos, chain.Root.position, chain.UpperLength, hint);
            }

            // Compute rotations
            Quaternion rootRot = chain.Root.rotation;
            Quaternion midRot = chain.Mid.rotation;
            Quaternion tipRot = chain.Tip.rotation;

            if (chain.Mid != chain.Root)
            {
                Vector3 rootToMidDir = (midPos - rootPos).normalized;
                Vector3 midToTipDir = (tipPos - midPos).normalized;

                if (rootToMidDir != Vector3.zero)
                    rootRot = Quaternion.LookRotation(rootToMidDir, Vector3.up);
                if (midToTipDir != Vector3.zero)
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

        /// <summary>
        /// 힌트 방향으로 무릎/팔꿈치 정렬 (CCD 1회).
        /// </summary>
        private static void AlignToHint(ref Vector3 midPos, Vector3 rootPos, float upperLength, Vector3 hint)
        {
            Vector3 toHint = (hint - rootPos).normalized;
            Vector3 toMid = (midPos - rootPos).normalized;

            // 현재 평면에서 힌트 방향으로 회전
            Vector3 planeNormal = Vector3.Cross(toMid, toHint);
            float angle = Vector3.SignedAngle(toMid, toHint, planeNormal);

            if (Mathf.Abs(angle) > 0.1f)
            {
                Quaternion rot = Quaternion.AngleAxis(Mathf.Clamp(angle, -30f, 30f), planeNormal);
                midPos = rootPos + rot * (midPos - rootPos);
            }
        }

        /// <summary>
        /// 체인 길이 자동 계산 (초기화 시 1회 호출).
        /// </summary>
        public static void ComputeLengths(ref Chain chain)
        {
            if (chain.Root != null && chain.Mid != null)
                chain.UpperLength = (chain.Mid.position - chain.Root.position).magnitude;
            if (chain.Mid != null && chain.Tip != null)
                chain.LowerLength = (chain.Tip.position - chain.Mid.position).magnitude;
        }

        /// <summary>
        /// 2-본 체인 풀이 (팔/다리 공용, 힌트 없음).
        /// </summary>
        public static SolverResult Solve2Bone(Transform root, Transform mid, Transform tip, Vector3 target, int iterations = 2)
        {
            var chain = new Chain { Root = root, Mid = mid, Tip = tip };
            ComputeLengths(ref chain);
            return Solve(chain, target, root.position + Vector3.right);
        }
    }
}