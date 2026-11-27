using System.Collections.Generic;
using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [138차 Cave-A] 동굴 실내 토폴로지 테스트 — docs/138_CAVE_BUILD_NPC_FACING_FIX_PLAN.md §2 P-B.
    /// PlayerCastleInteriorTopology 선례 구조(실빌드 + DestroyImmediate/LogAssert 규약) 준용.
    /// BuildCaveInterior("test", 1) 실호출 후 계약 검증:
    ///   ① TorchFlicker 부착 Light 수 == 8(≤8 상한) ② CaveCeiling 부재(탑다운 천장 금지 계약)
    ///   ③ GemChest 수 == 4(고정 배치 — 랜덤 폐기) ④ ResourceNode 수 ≥ 3(Stone×2+IronOre×1)
    ///   ⑤ 화로/갱도 이름 규약 오브젝트 존재 ⑥ 결정론 — 동일 territoryId 재빌드 시 동일 배치(요터 허용오차 0.01).
    /// </summary>
    public sealed class CaveInteriorTopologyTests
    {
        private const string TestTerritoryId = "test";
        private const int TestTier = 1;

        [Test]
        public void TorchFlickerLights_AreExactlyNine_UnderRenewedCap_156()
        {
            GameObject room = null;
            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            try
            {
                LogAssert.ignoreFailingMessages = true;
                room = CaveInteriorBuilder.BuildCaveInterior(TestTerritoryId, TestTier);
                Assert.That(room, Is.Not.Null, "[138차 Cave-A] BuildCaveInterior 반환");

                int torchCount = 0;
                foreach (var flicker in room.GetComponentsInChildren<TorchFlicker>(true))
                    if (flicker.GetComponent<Light>() != null)
                        torchCount++;

                Assert.That(torchCount, Is.EqualTo(9),
                    "[156차 명세 갱신] 횟불 총계는 9(벽횟불 8 + 중앙화로 1, ≤9 상한) — 면적 ×4 셀맵 확장으로 광 밀도 유지 목적 상향(구 [139차 P4] 7/≤8). 전부 TorchFlicker 부착 Light");
            }
            finally
            {
                if (room != null) Object.DestroyImmediate(room);
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }

        [Test]
        public void CaveCeiling_MustNotExist_TopDownContract()
        {
            GameObject room = null;
            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            try
            {
                LogAssert.ignoreFailingMessages = true;
                room = CaveInteriorBuilder.BuildCaveInterior(TestTerritoryId, TestTier);

                Assert.That(room.transform.Find("CaveCeiling"), Is.Null,
                    "[138차 Cave-A] CaveCeiling 직속 자식 부재 — 천장 생성 제거 계약");
                foreach (var transform in room.GetComponentsInChildren<Transform>(true))
                    Assert.That(transform.name, Is.Not.EqualTo("CaveCeiling"),
                        "[138차 Cave-A] 탑다운 천장 금지 계약 — 어떤 자식도 CaveCeiling일 수 없다");
            }
            finally
            {
                if (room != null) Object.DestroyImmediate(room);
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }

        [Test]
        public void GemChests_AreExactlyFour_FixedPlacement()
        {
            GameObject room = null;
            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            try
            {
                LogAssert.ignoreFailingMessages = true;
                room = CaveInteriorBuilder.BuildCaveInterior(TestTerritoryId, TestTier);

                var chests = room.GetComponentsInChildren<GemChest>(true);
                Assert.That(chests.Length, Is.EqualTo(4),
                    "[138차 Cave-A] 보석상자 고정 4개(저장고 3 + 심부 1) — 랜덤 배치 폐기");

                // [156차 셀맵] 고정 좌표 박제 — 동측 저장고실 마커 C 4개: 셀 (42,13)(45,13)(42,22)(45,22)
                // → 월드 (cx−24)×2, (cy−18)×2 = (36,−10)(42,−10)(36,8)(42,8), y=0.3.
                var expected = new HashSet<string>
                {
                    "36.0, 0.3, -10.0", "42.0, 0.3, -10.0", "36.0, 0.3, 8.0", "42.0, 0.3, 8.0"
                };
                foreach (var chest in chests)
                    Assert.That(expected.Contains(FormatXyz(chest.transform.localPosition)), Is.True,
                        $"[138차 Cave-A] {chest.name} 고정 좌표 박제 이탈: {chest.transform.localPosition}");
            }
            finally
            {
                if (room != null) Object.DestroyImmediate(room);
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }

        [Test]
        public void ResourceNodes_AtLeastThree_StoneTwiceAndIronOreOnce()
        {
            GameObject room = null;
            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            try
            {
                LogAssert.ignoreFailingMessages = true;
                room = CaveInteriorBuilder.BuildCaveInterior(TestTerritoryId, TestTier);

                var nodes = room.GetComponentsInChildren<ResourceNode>(true);
                Assert.That(nodes.Length, Is.GreaterThanOrEqualTo(3),
                    "[138차 Cave-A] 서쪽 채굴 갱도 ResourceNode 3개 이상");

                int stone = 0, ironOre = 0;
                foreach (var node in nodes)
                {
                    if (node.NodeType == ResourceNode.ResourceType.Stone) stone++;
                    if (node.NodeType == ResourceNode.ResourceType.IronOre) ironOre++;
                }
                Assert.That(stone, Is.GreaterThanOrEqualTo(2), "[138차 Cave-A] Stone×2 — SetupMiningNodes 선례 구성");
                Assert.That(ironOre, Is.GreaterThanOrEqualTo(1), "[138차 Cave-A] IronOre×1 — SetupMiningNodes 선례 구성");
            }
            finally
            {
                if (room != null) Object.DestroyImmediate(room);
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }

        [Test]
        public void ZoneNameConvention_ObjectsExist_HearthAndGalleries()
        {
            GameObject room = null;
            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            try
            {
                LogAssert.ignoreFailingMessages = true;
                room = CaveInteriorBuilder.BuildCaveInterior(TestTerritoryId, TestTier);

                Assert.That(FindDescendant(room.transform, "CaveHearth"), Is.Not.Null,
                    "[138차 Cave-A] 중앙 광장 화로(CaveHearth) 이름 규약 오브젝트 존재");

                // [156차 셀맵] 구역 5(입구사면/중앙화로/서채굴/동저장고/북심부) → 층 단일 존 CaveZone_F{n}으로 격상
                // (구획은 벽 기하가 담당 — 존 오브젝트는 배치 컨테이너 1개).
                string[] zoneNames = { "CaveZone_F1" };
                foreach (string zoneName in zoneNames)
                    Assert.That(FindDescendant(room.transform, zoneName), Is.Not.Null,
                        $"[138차 Cave-A] 구역 이름 규약 오브젝트 존재: {zoneName}");
            }
            finally
            {
                if (room != null) Object.DestroyImmediate(room);
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }

        [Test]
        public void Rebuild_SameTerritoryId_IsDeterministic_PositionsMatchWithin001()
        {
            GameObject first = null, second = null;
            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            try
            {
                LogAssert.ignoreFailingMessages = true;

                first = CaveInteriorBuilder.BuildCaveInterior(TestTerritoryId, TestTier);
                Dictionary<string, Vector3> firstLayout = CaptureTorchLayout(first);
                Object.DestroyImmediate(first);
                first = null;

                second = CaveInteriorBuilder.BuildCaveInterior(TestTerritoryId, TestTier);
                Dictionary<string, Vector3> secondLayout = CaptureTorchLayout(second);

                Assert.That(secondLayout.Count, Is.EqualTo(firstLayout.Count),
                    "[138차 Cave-A] 재빌드 — 횟불 오브젝트 집합 동일(결정론)");

                foreach (var kvp in firstLayout)
                {
                    Assert.That(secondLayout.ContainsKey(kvp.Key), Is.True,
                        $"[138차 Cave-A] 재빌드 누락 오브젝트: {kvp.Key}");
                    Assert.That(secondLayout[kvp.Key], Is.EqualTo(kvp.Value).Within(0.01f),
                        $"[138차 Cave-A] 결정론 — 동일 territoryId 재빌드 시 동일 배치(요터 허용오차 0.01): {kvp.Key}");
                }
            }
            finally
            {
                if (first != null) Object.DestroyImmediate(first);
                if (second != null) Object.DestroyImmediate(second);
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }

        // ===================================================================
        // 헬퍼 — 이름 규약 탐색 / 횟불 배치 캡처
        // ===================================================================

        /// <summary>이름으로 후손 탐색(구역 컨테이너 아래 중첩 대응 — CastleInterior 선례의 Find 계약 확장).</summary>
        private static Transform FindDescendant(Transform root, string name)
        {
            var queue = new Queue<Transform>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                for (int i = 0; i < current.childCount; i++)
                {
                    var child = current.GetChild(i);
                    if (child.name == name) return child;
                    queue.Enqueue(child);
                }
            }
            return null;
        }

        /// <summary>
        /// 횟불(TorchFlicker 부착 오브젝트) Room 로컬 좌표 캡처 — 결정론 대조용.
        /// 첫 오브젝트(방의 첫 자식) 위치도 sentinel 키로 포함(좌표 박제 선두 고정 검증).
        /// </summary>
        private static Dictionary<string, Vector3> CaptureTorchLayout(GameObject room)
        {
            var layout = new Dictionary<string, Vector3>();
            if (room.transform.childCount > 0)
            {
                var firstChild = room.transform.GetChild(0);
                layout["<firstChild>" + firstChild.name] = firstChild.localPosition;
            }
            foreach (var flicker in room.GetComponentsInChildren<TorchFlicker>(true))
                layout[flicker.gameObject.name] = flicker.transform.localPosition;
            return layout;
        }

        private static string FormatXyz(Vector3 v)
        {
            return $"{Round(v.x):0.0}, {Round(v.y):0.0}, {Round(v.z):0.0}";
        }

        private static float Round(float value)
        {
            return Mathf.Round(value * 10f) / 10f;
        }
    }
}
