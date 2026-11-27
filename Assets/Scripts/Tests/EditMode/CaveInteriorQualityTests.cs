using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [143차 P6] 동굴 고품질 계약 테스트 — docs/143_CAVE_INTERIOR_QUALITY_PLAN.md §Stage A P6.
    /// CaveInteriorTopologyTests 선례 구조(실빌드 + DestroyImmediate/LogAssert 규약) 준용.
    /// BuildCaveInterior("test", 1) 실호출 후 P0~P5 고품질 요소 계약 검증:
    ///   ① 벽 캡 16(입구 변 제외)+콜라이더 없음 ② 입구 지보목 3종+콜라이더 없음
    ///   ③ 접지 데칼(블롭 4/스탬프 12/빛웅덩이 1) 렌더러 전용 ④ 불꽃 쿼드 14+CaveFlame 아래 Light 0
    ///   ⑤ TorchFlicker 클래스 기본 레거시 절대값(_useBaseRelative false)+빌더 옵트인 2(화로/심부 true)
    ///   ⑥ ConfigureFlicker 옵트인 토글 ⑦ 중앙광 0.6 정확 1개 ⑧ 입구 스폰 헬퍼(tier 1/2)
    ///   ⑨ 화로 암석 링 8+콜라이더 없음 ⑩ 보석상자 Visual(GLB/폴백)+루트 렌더러 비가시.
    /// LogType 모호성 함정 회피 — LogAssert.Expect 미사용(ignoreFailingMessages 토글만).
    /// </summary>
    public sealed class CaveInteriorQualityTests
    {
        private const string TestTerritoryId = "test";
        private const int TestTier = 1;

        /// <summary>[156차] 빌더 소스 텍스트 계약용 — Cave146P1ContractTests.ReadSource 선례 경로.</summary>
        private static string ReadBuilderSource()
        {
            return System.IO.File.ReadAllText("Assets/Scripts/Systems/CaveInteriorBuilder.cs");
        }

        /// <summary>[156차] 소스 텍스트 계약 보조 — 주석 절단 후 반환(폐기 이력 문서 주석은 위반 아님).</summary>
        private static string BuilderCodeOnly()
        {
            var lines = ReadBuilderSource().Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                int idx = lines[i].IndexOf("//");
                if (idx >= 0) lines[i] = lines[i].Substring(0, idx);
            }
            return string.Join("\n", lines);
        }

        [Test]
        public void WallCaps_Are51_ThinPlane_156()
        {
            GameObject room = null;
            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            try
            {
                LogAssert.ignoreFailingMessages = true;
                room = CaveInteriorBuilder.BuildCaveInterior(TestTerritoryId, TestTier);
                Assert.That(room, Is.Not.Null, "[143차 P6] BuildCaveInterior 반환");

                // [156차 셀맵] 얇은 돌 벽 런(F1)별 평면 캡 — 성 내부 F1 패턴(평면 캡, 짙은 돌 선) 승계.
                var caps = CollectByNamePrefix(room, "CaveWallCap_");
                Assert.That(caps.Count, Is.EqualTo(51),
                    "[156차 셀맵] 벽 톱면 얇은 캡 = 벽 런 51(F1 직각 꺽임 아웃라인 경계 런 수) — 성 내부 짙은 돌 선 패턴");

                foreach (var cap in caps)
                {
                    Assert.That(cap.GetComponent<Collider>(), Is.Null,
                        $"[148차 P1-6] 캡은 렌더러 전용 평면 — 콜라이더 없음: {cap.name}");
                    Assert.That(cap.GetComponent<MeshFilter>() != null && cap.GetComponent<MeshRenderer>() != null,
                        Is.True, $"[148차 P1-6] 캡은 MeshFilter+MeshRenderer 평면 — 박스 프리미티브 폐기: {cap.name}");
                }
            }
            finally
            {
                if (room != null) Object.DestroyImmediate(room);
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }

        [Test]
        public void EntranceTimbering_Absent_156()
        {
            GameObject room = null;
            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            try
            {
                LogAssert.ignoreFailingMessages = true;
                room = CaveInteriorBuilder.BuildCaveInterior(TestTerritoryId, TestTier);
                Assert.That(room, Is.Not.Null, "[143차 P6] BuildCaveInterior 반환");

                // [156차 사용자 지시] 나무 기둥 전량 폐기 — 지보목 포스트 2(CaveTimberPost_L/R) 부재 단언.
                //   구 [143차 P2 존재]/[148차 P1-2 린틀만 폐기] 계약은 156차 명세 변경으로 역전.
                string[] timberNames = { "CaveTimberPost_L", "CaveTimberPost_R" };
                foreach (string timberName in timberNames)
                {
                    var timber = FindDescendant(room.transform, timberName);
                    Assert.That(timber, Is.Null,
                        $"[156차] 나무 기둥 폐기 — 지보목 부재 단언: {timberName}");
                }
                Assert.That(BuilderCodeOnly().Contains("CreateEntranceTimbering"), Is.False,
                    "[156차] 지보목 생성 메서드/호출부 소스 잔존 금지 — 폐기 완료.");
            }
            finally
            {
                if (room != null) Object.DestroyImmediate(room);
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }

        [Test]
        public void GroundDecals_Present_RenderOnly()
        {
            GameObject room = null;
            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            try
            {
                LogAssert.ignoreFailingMessages = true;
                room = CaveInteriorBuilder.BuildCaveInterior(TestTerritoryId, TestTier);
                Assert.That(room, Is.Not.Null, "[143차 P6] BuildCaveInterior 반환");

                var blobs = CollectByNamePrefix(room, "CaveBlob_");
                Assert.That(blobs.Count, Is.EqualTo(4),
                    "[143차 P3] 접지 블롭 4(채굴노드 3+화로 1) — 보석상자 블롭은 P5 GLB 내장 담당");
                var stamps = CollectByNamePrefix(room, "CaveWearStamp_");
                Assert.That(stamps.Count, Is.EqualTo(12),
                    "[143차 P3] 마모/재 스탬프 12(CaveWearStamp_00~11)");
                var pools = CollectByNameExact(room, "CaveEntranceLightPool");
                Assert.That(pools.Count, Is.EqualTo(1),
                    "[143차 P3] 입구 빛웅덩이 1");

                foreach (var decal in blobs)
                    AssertDecalIsRenderOnly(decal);
                foreach (var decal in stamps)
                    AssertDecalIsRenderOnly(decal);
                foreach (var decal in pools)
                    AssertDecalIsRenderOnly(decal);
            }
            finally
            {
                if (room != null) Object.DestroyImmediate(room);
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }

        [Test]
        public void TorchFlame_QuadsPresent_NoLights()
        {
            GameObject room = null;
            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            try
            {
                LogAssert.ignoreFailingMessages = true;
                room = CaveInteriorBuilder.BuildCaveInterior(TestTerritoryId, TestTier);
                Assert.That(room, Is.Not.Null, "[143차 P6] BuildCaveInterior 반환");

                Assert.That(CountByNamePrefix(room, "CaveFlameQuad_"), Is.EqualTo(18),
                    "[156차 명세 갱신] 불꽃 크로스 쿼드 18 — 횟불 9기(벽 8+화로 1)×2쿼드(A/B)");

                int flameCount = 0;
                int totalLightsUnderFlames = 0;
                foreach (var flame in CollectByNameExact(room, "CaveFlame"))
                {
                    flameCount++;
                    totalLightsUnderFlames += flame.GetComponentsInChildren<Light>(true).Length;
                }
                Assert.That(flameCount, Is.EqualTo(9),
                    "[156차 명세 갱신] 불꽃 부모(CaveFlame) 9기 — 횟불 총계와 동일");
                Assert.That(totalLightsUnderFlames, Is.EqualTo(0),
                    "[143차 P4] 불꽃 시각은 렌더러 전용 — CaveFlame 아래 Light 추가 0건(횟불 ≤8 계약 유지)");
            }
            finally
            {
                if (room != null) Object.DestroyImmediate(room);
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }

        [Test]
        public void TorchFlicker_ClassDefaultIsLegacy_AndTwoOptInTorches()
        {
            GameObject room = null;
            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            try
            {
                // 설계 의도: 두 관점 분할.
                //  (a) 클래스 기본값 계약 — AddComponent만으로 기본값 검증(빌더 무영향):
                //      [SerializeField] private bool _useBaseRelative = false; 가 진짜 계약.
                //  (b) 빌더 옵트인 계약 — 실빌드 7개 중 true는 정확히 2개(화로 불꽃+심부 횟불),
                //      나머지 5개 벽횟불은 레거시 절대값 불변(CreateWallTorch는 ConfigureFlicker 미호출).
                LogAssert.ignoreFailingMessages = true;

                var field = typeof(TorchFlicker).GetField("_useBaseRelative",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.That(field, Is.Not.Null,
                    "[143차 P4] TorchFlicker._useBaseRelative 필드 실존 — 옵트인 플래그 계약");

                // (a) 클래스 기본값 — 임시 프로브: 값 판독 후 즉시 파괴(단정 실패 시에도 씬 오염 없음).
                var probe = new GameObject("TorchFlicker_ClassDefaultProbe");
                var probeFlicker = probe.AddComponent<TorchFlicker>();
                bool probeUseBaseRelative = (bool)field.GetValue(probeFlicker);
                Object.DestroyImmediate(probe);
                Assert.That(probeUseBaseRelative, Is.False,
                    "[143차 P4] 클래스 기본값은 레거시 절대값 플리커(_useBaseRelative false — 옵트인 기본값 계약)");

                // (b) 빌더 옵트인 — 실빌드 검증.
                room = CaveInteriorBuilder.BuildCaveInterior(TestTerritoryId, TestTier);
                Assert.That(room, Is.Not.Null, "[143차 P6] BuildCaveInterior 반환");

                var flickers = room.GetComponentsInChildren<TorchFlicker>(true);
                Assert.That(flickers.Length, Is.EqualTo(9),
                    "[156차 명세 갱신] 횟불 TorchFlicker 총 9개(벽 8+화로 1, ≤9 상한 — 면적 ×4 광 밀도 유지)");

                var optIns = new List<string>();
                foreach (var flicker in flickers)
                    if ((bool)field.GetValue(flicker))
                        optIns.Add(flicker.name);

                // [157차 B-C] 전 벽횟불 base-relative 자연화 — 옵트인 = 전체(9: 벽 8+화로 1).
                Assert.That(optIns.Count, Is.EqualTo(flickers.Length),
                    "[157차 B-C] 빌더 옵트인(base 상대 플리커) = 전 횟불 — 조명 자연화 패키지 적용");
            }
            finally
            {
                if (room != null) Object.DestroyImmediate(room);
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }

        [Test]
        public void TorchFlicker_ConfigureFlicker_TogglesRelativeMode()
        {
            GameObject room = null;
            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            try
            {
                LogAssert.ignoreFailingMessages = true;
                room = CaveInteriorBuilder.BuildCaveInterior(TestTerritoryId, TestTier);
                Assert.That(room, Is.Not.Null, "[143차 P6] BuildCaveInterior 반환");

                var hearthFlame = FindDescendant(room.transform, "CaveHearthFlame");
                Assert.That(hearthFlame, Is.Not.Null, "[143차 P4] 화로 불꽃(CaveHearthFlame) 존재");
                var flicker = hearthFlame.GetComponent<TorchFlicker>();
                Assert.That(flicker, Is.Not.Null, "[143차 P4] 화로 불꽃에 TorchFlicker 부착");

                var field = typeof(TorchFlicker).GetField("_useBaseRelative",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.That(field, Is.Not.Null,
                    "[143차 P4] TorchFlicker._useBaseRelative 필드 실존 — 옵트인 플래그 계약");

                flicker.ConfigureFlicker(true, 0.9f, 1.1f, 0.9f, 1.1f);
                Assert.That((bool)field.GetValue(flicker), Is.True,
                    "[143차 P4] ConfigureFlicker(true,…) 호출 — 베이스 상대 모드로 토글");

                field.SetValue(flicker, false);   // 판정 후 필드 원복 false — 옵트인 전 상태 회복
            }
            finally
            {
                if (room != null) Object.DestroyImmediate(room);
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }

        [Test]
        public void CentralLight_IsExactlyOne_At06Intensity()
        {
            GameObject room = null;
            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            try
            {
                LogAssert.ignoreFailingMessages = true;
                room = CaveInteriorBuilder.BuildCaveInterior(TestTerritoryId, TestTier);
                Assert.That(room, Is.Not.Null, "[143차 P6] BuildCaveInterior 반환");

                int centralCount = 0;
                foreach (var light in room.GetComponentsInChildren<Light>(true))
                    if (Mathf.Abs(light.intensity - 0.6f) <= 0.01f)
                        centralCount++;

                Assert.That(centralCount, Is.EqualTo(1),
                    "[143차 P4] 중앙광 0.6 정확 1개 — 횟불 1.2/앰비언트 0.3과 구분(EditMode: chest 라이트 부재)");
            }
            finally
            {
                if (room != null) Object.DestroyImmediate(room);
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }

        [Test]
        public void SpawnHelper_Tier1InsideNearEntrance()
        {
            GameObject room = null;
            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            try
            {
                LogAssert.ignoreFailingMessages = true;
                room = CaveInteriorBuilder.BuildCaveInterior(TestTerritoryId, TestTier);
                Assert.That(room, Is.Not.Null, "[143차 P6] BuildCaveInterior 반환");

                Vector3 tier1 = CaveInteriorBuilder.GetEntranceSpawnPosition(1);
                // [156차 셀맵] F1 입구 통로 셀 (23,31) — 월드 x=(23−24)×2=−2, z=(31−18)×2=26 (문 블로커 E행 안쪽).
                Assert.That(tier1.x, Is.EqualTo(-2f).Within(0.3f),
                    "[156차 셀맵] 입구 통로 중심 x=−2 — 통로 폭 4m(셀 2개) 중심");
                Assert.That(tier1.z, Is.InRange(25.5f, 26.5f),
                    "[156차 셀맵] 입구 통로 내측 z=26 — 통로(rows27~31) 최심부, 봉합 EE행 바로 앞");
                Assert.That(tier1.y, Is.EqualTo(1.5f),
                    "[143차 P0] 스폰 높이 1.5 고정");

                // [147차 P-B0] 계약 갱신 — 방 크기 고정: 스폰도 tier 기하 무동작(구 ×1.25 스케일 좌표 폐기).
                Vector3 tier2 = CaveInteriorBuilder.GetEntranceSpawnPosition(2);
                Assert.That(tier2, Is.EqualTo(tier1).Within(0.001f),
                    "[147차 P-B0] tier2 스폰 — tierScale 고정 계약(tier 기하 무동작, tier1과 동일 좌표)");
            }
            finally
            {
                if (room != null) Object.DestroyImmediate(room);
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }

        [Test]
        public void HearthRockRing_EightRocksDirectUnderHearth_NoColliders()
        {
            GameObject room = null;
            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            try
            {
                LogAssert.ignoreFailingMessages = true;
                room = CaveInteriorBuilder.BuildCaveInterior(TestTerritoryId, TestTier);
                Assert.That(room, Is.Not.Null, "[143차 P6] BuildCaveInterior 반환");

                // 실빌드 계약 — 링 전용 컨테이너(CaveHearthRockRing) 없음. hearth 직속 배치.
                var hearth = FindDescendant(room.transform, "CaveHearth");
                Assert.That(hearth, Is.Not.Null,
                    "[143차 P4] 화로(CaveHearth) 존재 — 암석 링의 부모");

                var rocks = new List<Transform>();
                for (int i = 0; i < hearth.childCount; i++)
                {
                    var child = hearth.GetChild(i);
                    if (child.name.StartsWith("CaveHearthRock_"))
                        rocks.Add(child);
                }
                Assert.That(rocks.Count, Is.EqualTo(8),
                    "[143차 P4] 암석 링 8 — CaveHearth 직속 자식(CaveHearthRock_00~07, 별도 컨테이너 없음), 45° 간격 하드박제");
                foreach (var rock in rocks)
                    Assert.That(rock.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(0),
                        $"[143차 P4] 링 암석은 장식 — 자손 포함 콜라이더 없음(StripColliders 계약): {rock.name}");
            }
            finally
            {
                if (room != null) Object.DestroyImmediate(room);
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }

        [Test]
        public void ChestVisuals_GlbOrFallback_HiddenRootRenderer()
        {
            GameObject room = null;
            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            try
            {
                LogAssert.ignoreFailingMessages = true;
                room = CaveInteriorBuilder.BuildCaveInterior(TestTerritoryId, TestTier);
                Assert.That(room, Is.Not.Null, "[143차 P6] BuildCaveInterior 반환");

                var chests = room.GetComponentsInChildren<GemChest>(true);
                Assert.That(chests.Length, Is.EqualTo(4),
                    "[138차 Cave-A] 보석상자 4(저장고 3+심부 1)");

                foreach (var chest in chests)
                {
                    Assert.That(chest.transform.Find("Visual"), Is.Not.Null,
                        $"[143차 P5] 상자 시각 자식 Visual(GLB 또는 폴백) 존재: {chest.name}");
                    var rootRenderer = chest.GetComponent<MeshRenderer>();
                    Assert.That(rootRenderer, Is.Not.Null,
                        $"[143차 P5] 상자 루트 MeshRenderer 존재(큐브 루트 불변 계약): {chest.name}");
                    Assert.That(rootRenderer.enabled, Is.False,
                        $"[143차 P5] 루트 큐브 시각 비활성 — GLB 치환 시 루트(이름/좌표/콜라이더) 불변: {chest.name}");
                }
            }
            finally
            {
                if (room != null) Object.DestroyImmediate(room);
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }

        // ===================================================================
        // 헬퍼 — 이름 규약 탐색(TopologyTests 선례 복제)/수집
        // ===================================================================

        /// <summary>이름으로 후손 탐색(구역 컨테이너 아래 중첩 대응 — TopologyTests 선례 복제).</summary>
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

        /// <summary>이름 정확 일치 후손 수집(전 전수 — 중첩 컨테이너 대응).</summary>
        private static List<GameObject> CollectByNameExact(GameObject room, string name)
        {
            return CollectDescendants(room, name, exact: true);
        }

        /// <summary>이름 접두사 후손 수집(전 전수 — CaveWallCap_/CaveBlob_ 등 규약 카운트).</summary>
        private static List<GameObject> CollectByNamePrefix(GameObject room, string prefix)
        {
            return CollectDescendants(room, prefix, exact: false);
        }

        private static int CountByNamePrefix(GameObject room, string prefix)
        {
            return CollectByNamePrefix(room, prefix).Count;
        }

        private static List<GameObject> CollectDescendants(GameObject room, string name, bool exact)
        {
            var results = new List<GameObject>();
            var queue = new Queue<Transform>();
            queue.Enqueue(room.transform);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                for (int i = 0; i < current.childCount; i++)
                {
                    var child = current.GetChild(i);
                    bool matches = exact ? child.name == name : child.name.StartsWith(name);
                    if (matches) results.Add(child.gameObject);
                    queue.Enqueue(child);
                }
            }
            return results;
        }

        /// <summary>데칼 계약 단정 — MeshRenderer 보유 + Collider 부재(렌더러 전용).</summary>
        private static void AssertDecalIsRenderOnly(GameObject decal)
        {
            Assert.That(decal.GetComponent<MeshRenderer>(), Is.Not.Null,
                $"[143차 P3] 접지 데칼 MeshRenderer 보유: {decal.name}");
            Assert.That(decal.GetComponent<Collider>(), Is.Null,
                $"[143차 P3] 접지 데칼은 렌더러 전용 — 콜라이더 없음: {decal.name}");
        }
    }
}
