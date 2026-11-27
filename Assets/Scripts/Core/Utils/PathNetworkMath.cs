using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Core.Utils
{
    /// <summary>
    /// 흙길 네트워크 MST(최소 신장 트리) 결정론 순수 수학 유틸리티.
    /// MonoBehaviour 아님 — UnityEngine API는 Vector2만 사용하므로 EditMode에서 검증 가능하다.
    /// (TerritoryScatterMath와 동일 폴더·컨벤션: 입력 검증 실패 시에만 예외, 그 외 결정론 동작.)
    ///
    /// 범용 그래프 규약: 노드는 인덱스(0..n−1)일 뿐이다 — 허브/스폰발/영지/마을 등
    ///   노드의 의미와 순서는 호출부가 책임진다. 본 클래스는 도로 특화 해석을 하지 않는다.
    ///
    /// 알고리즘 규약: Kruskal MST + Union-Find(경로 압축).
    ///   1) 완전 그래프의 모든 간선(i &lt; j)을 (거리 오름차순, i 오름차순, j 오름차순)으로 정렬한다.
    ///      (i, j) 쌍은 간선마다 유일하므로 정렬 안정성과 무관하게 완전 결정론 순서가 성립한다.
    ///   2) 정렬 순서대로 스캔하며, 서로 다른 컴포넌트를 잇는 간선만 채택한다(사이클 방지).
    ///   3) n ≥ 2 → 정확히 n−1개 간선, n ≤ 1 → 빈 리스트.
    ///
    /// 중복 좌표 규약: 길이 0 간선(중복 좌표)도 허용하며 정렬 규약대로 안정 처리한다 —
    ///   이미 같은 컴포넌트이므로 Kruskal 스캔에서 자연스럽게 배제된다(트리 성질 훼손 없음).
    ///
    /// 결정론: 동일 입력 → 항상 동일 간선 목록(순서 포함). 난수를 소비하지 않는다.
    /// </summary>
    public static class PathNetworkMath
    {
        /// <summary>
        /// 주어진 노드들의 완전 그래프에 대한 MST를 Kruskal 알고리즘으로 구성한다.
        /// 간선은 (거리 오름차순, i 오름차순, j 오름차순) 결정론 tie-break로 선정되며,
        /// 반환 목록도 그 선정 순서 그대로이다.
        /// </summary>
        /// <param name="nodes">노드 좌표 목록(인덱스 = 노드 번호). null이면 ArgumentNullException.</param>
        /// <returns>
        /// MST 간선 목록 (a, b) — 항상 a &lt; b, 선정 순서대로.
        /// n ≤ 1이면 빈 리스트, n ≥ 2이면 정확히 n−1개.
        /// </returns>
        public static List<(int a, int b)> BuildMst(IReadOnlyList<Vector2> nodes)
        {
            if (nodes == null)
                throw new ArgumentNullException(
                    nameof(nodes),
                    "PathNetworkMath.BuildMst: 노드 목록이 null이다. " +
                    "유효한 Vector2 목록을 전달할 것(빈 목록은 빈 MST를 반환한다).");

            int n = nodes.Count;
            var mst = new List<(int a, int b)>();
            if (n <= 1)
                return mst;

            // 1단계 — 완전 그래프 전 간선(i < j) 열거 후 결정론 정렬:
            //   (거리 오름차순, i 오름차순, j 오름차순). 동거리 동률도 (i, j)로 완전 분해되므로
            //   List.Sort의 불안정성이 결과에 영향을 주지 않는다 — 완전 결정론.
            //   중첩 타입 대신 튜플 리스트 사용(CS0246 교훈: 중첩 타입 public API 금지).
            var allEdges = new List<(int i, int j, float dist)>(n * (n - 1) / 2);
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                    allEdges.Add((i, j, Vector2.Distance(nodes[i], nodes[j])));
            }

            allEdges.Sort((x, y) =>
            {
                if (x.dist < y.dist) return -1;
                if (x.dist > y.dist) return 1;
                if (x.i != y.i) return x.i.CompareTo(y.i);
                return x.j.CompareTo(y.j);
            });

            // 2단계 — Kruskal 스캔: Union-Find(경로 압축)로 사이클 방지.
            //   서로 다른 루트를 잇는 간선만 채택 → 종료 시 정확히 n−1개 간선(스패닝 트리 보장).
            int[] parent = new int[n];
            for (int i = 0; i < n; i++)
                parent[i] = i;

            for (int e = 0; e < allEdges.Count && mst.Count < n - 1; e++)
            {
                (int i, int j, float _) = allEdges[e];
                int rootI = FindRoot(parent, i);
                int rootJ = FindRoot(parent, j);
                if (rootI == rootJ)
                    continue; // 같은 컴포넌트 — 채택 시 사이클이 되므로 배제

                // 유니온: 작은 인덱스 루트를 대표로 (결정론 — 어느 쪽이든 결과 간선 집합은 동일)
                if (rootI < rootJ)
                    parent[rootJ] = rootI;
                else
                    parent[rootI] = rootJ;

                mst.Add((i, j));
            }

            return mst;
        }

        /// <summary>
        /// Union-Find 루트 탐색 + 경로 압축. 스캔 중 트리를 평평하게 만들어 이후 Find를 상수화한다.
        /// parent 배열은 호출부(BuildMst 스캔) 지역 상태이므로 외부 결정론에 영향을 주지 않는다.
        /// </summary>
        private static int FindRoot(int[] parent, int node)
        {
            // 1) 루트까지 올라가기
            int root = node;
            while (parent[root] != root)
                root = parent[root];

            // 2) 경로 압축 — 경로상 모든 노드를 루트에 직결
            while (parent[node] != root)
            {
                int next = parent[node];
                parent[node] = root;
                node = next;
            }

            return root;
        }
    }
}
