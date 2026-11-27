using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [T2-2] 흙길 네트워크 MST 교체 검증 — NationTerrainController.BuildDirtPathSegments
    /// public static 직접 호출(Unity 에디터 실행 없이 순수 계산만 검증).
    /// ⚠ 실행 판정은 QA 서브에이전트 증거로 확정(구현 서브에이전트는 실행하지 않음).
    ///
    /// 검증 범위:
    ///  1) EdgeCount — 세그먼트 수 = 노드 수 − 1 (노드 = 허브1 + 스폰1 + 영지(동적) + 마을(동적),
    ///     상수 하드코딩 금지, 동적 합산)
    ///  2) HubConnectivity — (0,0) 끝점 존재 + 독립 구현 union-find로 전 노드 도달
    ///     (근접 병합=중복 좌표 인스턴스 통합 + 세그먼트 끝점 유니온=도로 연결 그래프, tolerance 0.01)
    ///  3) AllAnchorsOnGraph — TerritoryDatabase 전 영지 worldPosition + VillagePlacementSystem
    ///     전 마을 좌표가 세그먼트 끝점 집합에 존재(≈0.01m)
    ///  4) SpawnFootPresent — 스폰(PlayerSpawnConfig.SpawnPosition) 노드가 끝점 집합에 존재
    ///  5) Determinism — 두 호출 결과 배열 완전 동일(순서 포함, tolerance 1e-4)
    ///
    /// EditMode 함정 대응: TerritoryDatabase는 코드 생성 DB — Instance 접근 시 정의 자동 생성
    /// (기존 컨벤션, EditMode에서 안전). VillagePlacementSystem.GetAllVillages는 public static
    /// 결정론 조회이므로 씬/MonoBehaviour 의존 없음.
    /// </summary>
    public sealed class DirtPathNetworkTests
    {
        /// <summary>끝점 판정 허용 오차(m) — 0.01.</summary>
        private const float EndpointTolerance = 0.01f;
        /// <summary>결정론 비교 허용 오차(m) — 1e-4.</summary>
        private const float DeterminismTolerance = 1e-4f;

        /// <summary>테스트 대상 직접 호출 — 캐시가 아닌 매번 새로 계산(결정론이므로 동일 결과).</summary>
        private static NationTerrainController.PathSegment[] Build()
        {
            return NationTerrainController.BuildDirtPathSegments();
        }

        /// <summary>
        /// 기대 노드 수(동적 합산 — 상수 하드코딩 금지):
        /// 허브(0,0) 1 + 스폰 1 + TerritoryDatabase 전 영지 + VillagePlacementSystem 전 마을.
        /// </summary>
        private static int ExpectedNodeCount()
        {
            int count = 2; // 허브 + 스폰
            var db = TerritoryDatabase.Instance;
            if (db != null)
                foreach (var def in db.GetAllDefinitions())
                    count++;
            count += VillagePlacementSystem.GetAllVillages().Count;
            return count;
        }

        /// <summary>
        /// 기준 앵커 좌표(동적, XZ): 허브(0,0)/스폰/전 영지 worldPosition/전 마을 center —
        /// 생산 측 노드 수집과 동일 구성(순서는 연결성 판정에 불필요).
        /// </summary>
        private static List<Vector2> ExpectedAnchors()
        {
            var anchors = new List<Vector2> { Vector2.zero };

            Vector3 spawn = PlayerSpawnConfig.SpawnPosition;
            anchors.Add(new Vector2(spawn.x, spawn.z));

            var db = TerritoryDatabase.Instance;
            if (db != null)
                foreach (var def in db.GetAllDefinitions())
                {
                    Vector3 p = def.worldPosition;
                    anchors.Add(new Vector2(p.x, p.z));
                }

            foreach (var v in VillagePlacementSystem.GetAllVillages())
            {
                Vector3 c = v.center;
                anchors.Add(new Vector2(c.x, c.z));
            }
            return anchors;
        }

        /// <summary>세그먼트 전 끝점 XZ 목록(선분당 2개).</summary>
        private static List<Vector2> CollectEndpoints(NationTerrainController.PathSegment[] segments)
        {
            var endpoints = new List<Vector2>(segments.Length * 2);
            foreach (var s in segments)
            {
                endpoints.Add(new Vector2(s.X0, s.Z0));
                endpoints.Add(new Vector2(s.X1, s.Z1));
            }
            return endpoints;
        }

        /// <summary>독립 구현 Union-Find(경로 압축) — 생산 PathNetworkMath와 무관하게 테스트 전용 재구현.</summary>
        private static int FindRoot(int[] parent, int node)
        {
            int root = node;
            while (parent[root] != root)
                root = parent[root];
            while (parent[node] != root)
            {
                int next = parent[node];
                parent[node] = root;
                node = next;
            }
            return root;
        }

        /// <summary>1) 세그먼트 수 = 노드 수 − 1 (MST 스패닝 보장).</summary>
        [Test]
        public void EdgeCount_SegmentsEqualNodesMinusOne()
        {
            var segments = Build();
            int expectedEdges = ExpectedNodeCount() - 1;
            Assert.AreEqual(expectedEdges, segments.Length,
                $"세그먼트 수({segments.Length})가 노드 수−1({expectedEdges})과 다르다");
        }

        /// <summary>2) (0,0) 끝점 존재 + 독립 union-find로 전 노드가 허브에서 도달.</summary>
        [Test]
        public void HubConnectivity_AllNodesReachableFromHub()
        {
            var segments = Build();
            var endpoints = CollectEndpoints(segments);

            // (0,0) 끝점 존재 확인
            int hubIndex = -1;
            for (int i = 0; i < endpoints.Count; i++)
            {
                if (Vector2.Distance(endpoints[i], Vector2.zero) <= EndpointTolerance)
                {
                    hubIndex = i;
                    break;
                }
            }
            Assert.GreaterOrEqual(hubIndex, 0, "허브 (0,0) 끝점이 세그먼트 그래프에 존재해야 한다");

            // 독립 union-find에는 두 종류의 유니온이 모두 필요하다:
            // ① 근접 병합(tolerance 0.01) — 같은 좌표의 중복 인스턴스를 하나의 정점으로 통합
            //   (같은 앵커가 여러 세그먼트에 각각 별개 끝점 인스턴스로 들어있기 때문).
            // ② 세그먼트 유니온 — 세그먼트 i의 양 끝점을 하나의 도로(간선)로 연결.
            //   이것이 실제 도로 연결 그래프이며, ①만 있고 ②가 없으면 좌표마다 고립
            //   컴포넌트가 생겨 도달성 판정이 불가능하다.
            int n = endpoints.Count;
            var parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;

            // ① 근접 병합: 중복 좌표 인스턴스 통합(좌표 판정용 그래프 정점 통일)
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    if (Vector2.Distance(endpoints[i], endpoints[j]) <= EndpointTolerance)
                    {
                        int ri = FindRoot(parent, i);
                        int rj = FindRoot(parent, j);
                        if (ri != rj) parent[rj] = ri;
                    }

            // ② 세그먼트 유니온: CollectEndpoints가 세그먼트 순서대로 (X0,Z0)→(X1,Z1)
            //   2개씩 추가하므로 세그먼트 i의 두 끝점 = endpoints[2i], endpoints[2i+1].
            //   각 세그먼트의 두 끝점을 union해 도로 연결 그래프를 재구성한다.
            for (int i = 0; i < segments.Length; i++)
            {
                int ri = FindRoot(parent, 2 * i);
                int rj = FindRoot(parent, 2 * i + 1);
                if (ri != rj) parent[rj] = ri;
            }

            // 전 노드(허브/스폰/전 영지/전 마을)가 허브와 같은 컴포넌트여야 한다
            int hubRoot = FindRoot(parent, hubIndex);
            foreach (var anchor in ExpectedAnchors())
            {
                int matched = -1;
                for (int i = 0; i < n; i++)
                {
                    if (Vector2.Distance(endpoints[i], anchor) <= EndpointTolerance)
                    {
                        matched = i;
                        break;
                    }
                }
                Assert.GreaterOrEqual(matched, 0,
                    $"앵커({anchor.x:F1},{anchor.y:F1})가 세그먼트 끝점 집합에 없다");
                Assert.AreEqual(hubRoot, FindRoot(parent, matched),
                    $"앵커({anchor.x:F1},{anchor.y:F1})가 허브에서 도달 불가 — 네트워크 분리");
            }
        }

        /// <summary>3) 전 영지 worldPosition + 전 마을 center가 끝점 집합에 존재(≈0.01m).</summary>
        [Test]
        public void AllAnchorsOnGraph_TerritoriesAndVillagesPresent()
        {
            var segments = Build();
            var endpoints = CollectEndpoints(segments);

            var db = TerritoryDatabase.Instance;
            if (db != null)
                foreach (var def in db.GetAllDefinitions())
                {
                    Vector3 p = def.worldPosition;
                    bool found = false;
                    foreach (var e in endpoints)
                        if (Vector2.Distance(e, new Vector2(p.x, p.z)) <= EndpointTolerance)
                        {
                            found = true;
                            break;
                        }
                    Assert.IsTrue(found,
                        $"영지 {def.id} worldPosition({p.x:F1},{p.z:F1})가 끝점 집합에 없다");
                }

            foreach (var v in VillagePlacementSystem.GetAllVillages())
            {
                Vector3 c = v.center;
                bool found = false;
                foreach (var e in endpoints)
                    if (Vector2.Distance(e, new Vector2(c.x, c.z)) <= EndpointTolerance)
                    {
                        found = true;
                        break;
                    }
                Assert.IsTrue(found,
                    $"마을 {v.nation}#{v.index} center({c.x:F1},{c.z:F1})가 끝점 집합에 없다");
            }
        }

        /// <summary>4) 스폰 노드(PlayerSpawnConfig.SpawnPosition XZ)가 끝점 집합에 존재.</summary>
        [Test]
        public void SpawnFootPresent_SpawnNodeInEndpointSet()
        {
            var segments = Build();
            var endpoints = CollectEndpoints(segments);

            Vector3 spawn = PlayerSpawnConfig.SpawnPosition;
            bool found = false;
            foreach (var e in endpoints)
                if (Vector2.Distance(e, new Vector2(spawn.x, spawn.z)) <= EndpointTolerance)
                {
                    found = true;
                    break;
                }
            Assert.IsTrue(found,
                $"스폰 노드({spawn.x:F1},{spawn.z:F1})가 끝점 집합에 없다");
        }

        /// <summary>5) 두 호출 결과 배열 완전 동일(순서 포함, tolerance 1e-4).</summary>
        [Test]
        public void Determinism_TwoCallsProduceIdenticalArrays()
        {
            var first = Build();
            var second = Build();

            Assert.AreEqual(first.Length, second.Length, "두 호출의 세그먼트 수가 다르다");
            for (int i = 0; i < first.Length; i++)
            {
                bool same =
                    Mathf.Abs(first[i].X0 - second[i].X0) <= DeterminismTolerance &&
                    Mathf.Abs(first[i].Z0 - second[i].Z0) <= DeterminismTolerance &&
                    Mathf.Abs(first[i].X1 - second[i].X1) <= DeterminismTolerance &&
                    Mathf.Abs(first[i].Z1 - second[i].Z1) <= DeterminismTolerance;
                Assert.IsTrue(same, $"세그먼트 #{i}가 두 호출 간 다르다(순서 포함 동일해야 한다)");
            }
        }
    }
}
