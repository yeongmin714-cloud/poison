using System.Collections.Generic;
using NUnit.Framework;
using ProjectName.Core.Utils;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [2026-10-15 T2-1 — 흙길 MST 순수 수학] 계약 테스트.
    /// - 간선 수: n=2→1, n=5→4, n=20→19, n=0/1→0 (n≥2면 정확히 n−1).
    /// - 연결성: 독립 구현 Union-Find로 전 노드 도달 — 고정 시드 다수(n=12 랜덤점 포함).
    /// - 비사이클: 간선 수 n−1 + 유니온 순서 재현 시 모든 간선이 이질 집합 연결.
    /// - 최적성(소규모): n≤6 고정 시드 점집합 5종 — 생성 트리 전수(brute-force 재귀 열거) 최소
    ///   총길이와 BuildMst 총길이 일치(≤ 비교).
    /// - 기하 케이스: 정사각형(변 3개·대각선 배제), 나침반 다이아몬드(인접변 3개·대각선 배제).
    /// - 결정론: 동일 입력 2회 → 간선 목록 완전 동일(순서 포함).
    /// - null 처리: nodes=null → ArgumentNullException(한국어 메시지).
    /// 실행 판정은 QA 서브에이전트 증거로 확정한다(구현 서브에이전트는 실행하지 않음).
    /// </summary>
    public sealed class PathNetworkMathTests
    {
        private const float LengthTolerance = 1e-3f; // 총길이 비교 허용 오차 (float 누적 오차)
        private const float BruteTolerance = 1e-2f;  // brute-force 비교 허용 오차 (합산 순서 차이 누적 여유)

        // ════════════════════════════════════════════════════════════════════
        // 1. 간선 수 — n≥2면 정확히 n−1, n≤1이면 0
        // ════════════════════════════════════════════════════════════════════

        [TestCase(2, 1)]
        [TestCase(5, 4)]
        [TestCase(20, 19)]
        [TestCase(0, 0)]
        [TestCase(1, 0)]
        public void BuildMst_EdgeCount_IsExactlyNodeCountMinusOne(int nodeCount, int expectedEdges)
        {
            List<Vector2> nodes = MakeRandomNodes(42, nodeCount);

            List<(int a, int b)> mst = PathNetworkMath.BuildMst(nodes);

            Assert.That(mst, Has.Count.EqualTo(expectedEdges),
                $"n={nodeCount}일 때 MST 간선 수는 정확히 {expectedEdges}개여야 한다.");
        }

        [Test]
        public void BuildMst_DuplicateCoordinates_StillYieldsSpanningTree()
        {
            // 중복 좌표(길이 0 간선) 규약 — 트리 성질 훼손 없이 n−1 간선 유지.
            List<Vector2> nodes = new List<Vector2>
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),   // 노드 1과 완전 중복
                new Vector2(0f, 0f),   // 노드 1, 2와 완전 중복
                new Vector2(10f, 0f),
            };

            List<(int a, int b)> mst = PathNetworkMath.BuildMst(nodes);

            Assert.That(mst, Has.Count.EqualTo(3),
                "중복 좌표가 있어도 n=4면 MST 간선 수는 정확히 3개여야 한다.");
        }

        // ════════════════════════════════════════════════════════════════════
        // 2. 연결성 — 독립 구현 Union-Find로 전 노드 도달
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void BuildMst_Connectivity_AllNodesReachableAcrossSeededSets()
        {
            // 고정 시드 다수 — 직선 배치(극단), 삼각형, 그리고 n=12 고정 시드 랜덤점 포함.
            List<List<Vector2>> nodeSets = new List<List<Vector2>>
            {
                MakeLineNodes(7, 30f),          // n=7 — 한 직선 위 점들(동거리 다수)
                MakeRandomNodes(101, 4),        // n=4
                MakeRandomNodes(2024, 12),      // n=12 — 고정 시드 랜덤점
                MakeRandomNodes(31337, 12),     // n=12 — 다른 시드 랜덤점
                MakeGridNodes(3, 4, 50f),       // n=12 — 격자(직교 동거리 다수)
            };

            for (int s = 0; s < nodeSets.Count; s++)
            {
                List<Vector2> nodes = nodeSets[s];
                List<(int a, int b)> mst = PathNetworkMath.BuildMst(nodes);

                Assert.That(mst, Has.Count.EqualTo(nodes.Count - 1),
                    $"점집합 #{s}: 간선 수는 n−1이어야 한다 (n={nodes.Count}).");

                // 독립 구현 Union-Find(테스트 전용)로 전 노드 연결 확인.
                int[] parent = MakeSingletons(nodes.Count);
                foreach ((int a, int b) in mst)
                {
                    Assert.That(UnionIfDifferent(parent, a, b), Is.True,
                        $"점집합 #{s}: 간선 ({a},{b})이 이미 같은 집합을 연결 — 사이클 의심.");
                }

                int root = FindRootTest(parent, 0);
                for (int i = 1; i < nodes.Count; i++)
                {
                    Assert.That(FindRootTest(parent, i), Is.EqualTo(root),
                        $"점집합 #{s}: 노드 {i}가 노드 0과 연결되어야 한다 (전 노드 도달 실패).");
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // 3. 비사이클 — 간선 수 n−1 + 유니온 순서 재현 시 이질 집합 연결만
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void BuildMst_NoCycle_EveryEdgeMergesDistinctSetsInOrder()
        {
            List<Vector2> nodes = MakeRandomNodes(777, 15);

            List<(int a, int b)> mst = PathNetworkMath.BuildMst(nodes);

            Assert.That(mst, Has.Count.EqualTo(nodes.Count - 1),
                "간선 수가 n−1이어야 한다.");

            // 반환 순서 그대로 유니온 재현 — 모든 간선이 서로 다른 집합을 연결해야 한다.
            // (간선 수 n−1 + 전부 이질 연결이면 사이클 부재 + 스패닝이 동시에 성립)
            int[] parent = MakeSingletons(nodes.Count);
            for (int e = 0; e < mst.Count; e++)
            {
                (int a, int b) = mst[e];
                Assert.That(UnionIfDifferent(parent, a, b), Is.True,
                    $"간선 #{e} ({a},{b}): 유니온 순서 재현 시 이질 집합을 연결해야 한다(사이클 형성 간선 부재).");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // 4. 최적성(소규모) — n≤6 생성 트리 전수 열거 최소값과 일치
        // ════════════════════════════════════════════════════════════════════

        [TestCase(3, 11)]
        [TestCase(4, 42)]
        [TestCase(5, 101)]
        [TestCase(6, 2024)]
        [TestCase(6, 31337)]
        public void BuildMst_OptimalitySmall_MatchesBruteForceMinimum(int nodeCount, int seed)
        {
            List<Vector2> nodes = MakeRandomNodes(seed, nodeCount);
            int n = nodes.Count;

            // 전 간선 사전 계산 (n≤6 → 최대 15간선, 2^15 재귀 경로 — 즉시 완료)
            int edgeCount = n * (n - 1) / 2;
            float[] dist = new float[edgeCount];
            int[] edgeA = new int[edgeCount];
            int[] edgeB = new int[edgeCount];
            int idx = 0;
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    edgeA[idx] = i;
                    edgeB[idx] = j;
                    dist[idx] = Vector2.Distance(nodes[i], nodes[j]);
                    idx++;
                }
            }

            // brute-force 재귀 열거: 모든 생성 트리의 총길이 최소값
            float best = float.PositiveInfinity;
            EnumerateSpanningTrees(edgeA, edgeB, dist, n, 0, 0, MakeSingletons(n), 0f, 0, ref best);

            Assert.That(best, Is.LessThan(float.PositiveInfinity),
                $"n={n}이면 생성 트리가 최소 1개 존재해야 한다 (seed={seed}).");

            List<(int a, int b)> mst = PathNetworkMath.BuildMst(nodes);
            float mstTotal = TotalLength(nodes, mst);

            // ≤ 비교 — MST가 최소 생성 트리임을 검증 (부동소수 합산 순서 차이 허용)
            Assert.That(mstTotal, Is.LessThanOrEqualTo(best + BruteTolerance),
                $"BuildMst 총길이 {mstTotal}은 brute-force 최소 {best} 이하(오차 {BruteTolerance})여야 한다 " +
                $"(n={n}, seed={seed}).");
        }

        // ════════════════════════════════════════════════════════════════════
        // 5. 기하 케이스 — 정사각형 4모서리: 변 3개, 대각선 배제, 총길이 3·변
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void BuildMst_KnownSquare_UsesThreeSidesAndRejectsDiagonals()
        {
            const float side = 100f;
            List<Vector2> nodes = new List<Vector2>
            {
                new Vector2(0f, 0f),    // 0: 좌하
                new Vector2(0f, side),  // 1: 좌상
                new Vector2(side, 0f),  // 2: 우하
                new Vector2(side, side),// 3: 우상
            };

            List<(int a, int b)> mst = PathNetworkMath.BuildMst(nodes);

            Assert.That(mst, Has.Count.EqualTo(3), "정사각형 MST는 변 3개여야 한다.");

            float total = 0f;
            foreach ((int a, int b) in mst)
            {
                float len = Vector2.Distance(nodes[a], nodes[b]);
                Assert.That(len, Is.EqualTo(side).Within(LengthTolerance),
                    $"간선 ({a},{b}) 길이 {len}은 변 {side}이어야 한다 — 대각선({side * Mathf.Sqrt(2f)}) 배제.");
                total += len;
            }

            Assert.That(total, Is.EqualTo(3f * side).Within(LengthTolerance),
                $"정사각형 MST 총길이는 3·변 = {3f * side}여야 한다 (실제 {total}).");
        }

        // ════════════════════════════════════════════════════════════════════
        // 6. 기하 케이스 — 나침반 4점(다이아몬드): 인접변 3개, 대각선 배제
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void BuildMst_KnownDiamond_UsesThreeAdjacentEdgesAndRejectsDiagonals()
        {
            const float r = 50f;
            List<Vector2> nodes = new List<Vector2>
            {
                new Vector2(0f, r),    // 0: 북
                new Vector2(0f, -r),   // 1: 남
                new Vector2(r, 0f),    // 2: 동
                new Vector2(-r, 0f),   // 3: 서
            };
            float adjacent = r * Mathf.Sqrt(2f); // 인접 변 길이 (북동·북서·남동·남서)
            float diagonal = 2f * r;             // 대각선(북남·동서) 길이

            List<(int a, int b)> mst = PathNetworkMath.BuildMst(nodes);

            Assert.That(mst, Has.Count.EqualTo(3), "다이아몬드 MST는 인접변 3개여야 한다.");

            float total = 0f;
            foreach ((int a, int b) in mst)
            {
                float len = Vector2.Distance(nodes[a], nodes[b]);
                Assert.That(len, Is.EqualTo(adjacent).Within(LengthTolerance),
                    $"간선 ({a},{b}) 길이 {len}은 인접변 {adjacent}이어야 한다 — 대각선({diagonal}) 배제.");
                total += len;
            }

            Assert.That(total, Is.EqualTo(3f * adjacent).Within(LengthTolerance),
                $"다이아몬드 MST 총길이는 3·인접변 = {3f * adjacent}여야 한다 (실제 {total}).");
        }

        // ════════════════════════════════════════════════════════════════════
        // 7. 결정론 — 동일 입력 2회 → 간선 목록 완전 동일(순서 포함)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void BuildMst_Determinism_SameInputYieldsIdenticalEdgeListOrder()
        {
            List<Vector2> nodes = MakeRandomNodes(909, 12);

            List<(int a, int b)> first = PathNetworkMath.BuildMst(nodes);
            List<(int a, int b)> second = PathNetworkMath.BuildMst(nodes);

            Assert.That(second, Has.Count.EqualTo(first.Count),
                "동일 입력이면 간선 수도 동일해야 한다.");

            for (int e = 0; e < first.Count; e++)
            {
                Assert.That((second[e].a, second[e].b),
                    Is.EqualTo((first[e].a, first[e].b)),
                    $"간선 #{e}: 동일 입력 2회는 순서까지 완전 동일해야 한다 " +
                    $"(1회차 ({first[e].a},{first[e].b}) vs 2회차 ({second[e].a},{second[e].b})).");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // 8. null 처리 — nodes=null → ArgumentNullException
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void BuildMst_NullNodes_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                PathNetworkMath.BuildMst(null),
                "nodes가 null이면 한국어 메시지의 ArgumentNullException을 던져야 한다.");
        }

        // ════════════════════════════════════════════════════════════════════
        // 헬퍼
        // ════════════════════════════════════════════════════════════════════

        /// <summary>고정 시드 랜덤 점집합 생성 — 테스트 재현성 보장용.</summary>
        private static List<Vector2> MakeRandomNodes(int seed, int count)
        {
            System.Random rng = new System.Random(seed);
            var nodes = new List<Vector2>(count);
            for (int i = 0; i < count; i++)
                nodes.Add(new Vector2(
                    (float)rng.NextDouble() * 1000f - 500f,
                    (float)rng.NextDouble() * 1000f - 500f));
            return nodes;
        }

        /// <summary>한 직선 위 균등 간격 점들 — 동거리 다수 정렬 tie-break 경로 커버.</summary>
        private static List<Vector2> MakeLineNodes(int count, float spacing)
        {
            var nodes = new List<Vector2>(count);
            for (int i = 0; i < count; i++)
                nodes.Add(new Vector2(i * spacing, 0f));
            return nodes;
        }

        /// <summary>격자 점들 — 직교 동거리 다수 정렬 tie-break 경로 커버.</summary>
        private static List<Vector2> MakeGridNodes(int cols, int rows, float spacing)
        {
            var nodes = new List<Vector2>(cols * rows);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                    nodes.Add(new Vector2(c * spacing, r * spacing));
            }
            return nodes;
        }

        /// <summary>MST 간선 목록의 총길이 합 (double 누적 후 float 반올림 — 오차 최소화).</summary>
        private static float TotalLength(List<Vector2> nodes, List<(int a, int b)> edges)
        {
            double total = 0.0;
            foreach ((int a, int b) in edges)
                total += Vector2.Distance(nodes[a], nodes[b]);
            return (float)total;
        }

        /// <summary>brute-force 재귀 열거 — 각 간선마다 포함/제외 분기, (n−1)개 선정 시 연결성 판정.</summary>
        private static void EnumerateSpanningTrees(
            int[] edgeA, int[] edgeB, float[] dist, int n,
            int edgeIndex, int picked, int[] parent, float total, int rootsMerged, ref float best)
        {
            // 가지치기: 남은 간선으로 부족하면 중단 (n−1−picked개 더 필요)
            if (n - 1 - picked > (edgeA.Length - edgeIndex))
                return;

            if (picked == n - 1)
            {
                // n−1개 간선 + 유니온 성공 n−1회(rootsMerged) → 전체 연결 = 생성 트리
                if (rootsMerged == n - 1 && total < best)
                    best = total;
                return;
            }

            if (edgeIndex >= edgeA.Length)
                return;

            // 분기 1: 현재 간선 포함 (이질 집합 연결 시에만)
            int[] withParent = (int[])parent.Clone();
            int a = edgeA[edgeIndex];
            int b = edgeB[edgeIndex];
            int rootA = FindRootTest(withParent, a);
            int rootB = FindRootTest(withParent, b);
            if (rootA != rootB)
            {
                if (rootA < rootB) withParent[rootB] = rootA;
                else withParent[rootA] = rootB;
                EnumerateSpanningTrees(edgeA, edgeB, dist, n, edgeIndex + 1,
                    picked + 1, withParent, total + dist[edgeIndex], rootsMerged + 1, ref best);
            }

            // 분기 2: 현재 간선 제외
            EnumerateSpanningTrees(edgeA, edgeB, dist, n, edgeIndex + 1,
                picked, parent, total, rootsMerged, ref best);
        }

        /// <summary>테스트 전용 단일 집합 Union-Find 초기화 (구현과 독립 — 검증 신뢰성 확보).</summary>
        private static int[] MakeSingletons(int n)
        {
            int[] parent = new int[n];
            for (int i = 0; i < n; i++)
                parent[i] = i;
            return parent;
        }

        /// <summary>테스트 전용 루트 탐색(경로 압축 없음) — 구현체와 독립 구현.</summary>
        private static int FindRootTest(int[] parent, int node)
        {
            while (parent[node] != node)
                node = parent[node];
            return node;
        }

        /// <summary>테스트 전용 유니온 — 이질 집합이면 병합하고 true, 동질이면 false(사이클 신호).</summary>
        private static bool UnionIfDifferent(int[] parent, int a, int b)
        {
            int rootA = FindRootTest(parent, a);
            int rootB = FindRootTest(parent, b);
            if (rootA == rootB)
                return false;
            if (rootA < rootB) parent[rootB] = rootA;
            else parent[rootA] = rootB;
            return true;
        }
    }
}
