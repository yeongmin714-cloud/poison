using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [159차 P2] 아레나(여관 "The Rusty Dragon Inn") 가구 GLB 전면 교체 계약 테스트.
    /// 사용자 지시: "프리미티브 기하 가구를 전부 교체. 변형(A~G) 가능하면 변형 배정."
    /// - G1: 6개 프리미티브 가구 사이트가 Restaurant GLB로 교체됨(원본 렌더러 — 머티리얼 교체 금지 계약).
    /// - G2: 변형 배정 결정론 — StableHash(FNV-1a) 재호출 동일 + TryGetVariantModel 동일 시드 동일 키.
    /// - G3: 폴백 계약 — 부재 가족 키는 null/미배치(소실 금지 → 프리미티브 폴백 경로 존재).
    /// - G4: 접지 블롭 개수 계약(1~11번 + GLB 12번) 무손상.
    /// - G5: 베팅 데스크 충돌 유지 계약(BoxCollider 승계) + 루트 MeshRenderer 보장(I2 계약).
    /// </summary>
    public sealed class ArenaFurnitureGlbTests
    {
        [Test]
        public void G1_TableArea_Furniture_IsGlb_NotUnityPrimitive()
        {
            GameObject room = ArenaInteriorBuilder.BuildArenaInterior("Empire");
            Assert.That(room, Is.Not.Null);
            try
            {
                // 원형 탁자 2 / 사각 탁자 3 / 의자 3 — 전부 GLB 계열로 교체(프리미티브 폴백은 팩 부재 시에만).
                string[] sites =
                {
                    "Arena_Table_Circular_0", "Arena_Table_Circular_1",
                    "Arena_Table_Square_0", "Arena_Table_Square_1", "Arena_Table_Square_2",
                    "Arena_Table_Square_0_Chair", "Arena_Table_Square_1_Chair", "Arena_Table_Square_2_Chair",
                };
                foreach (string site in sites)
                {
                    Transform t = room.transform.Find(site);
                    Assert.That(t, Is.Not.Null, site + " — 가구 오브젝트 존재(소실 금지).");
                    var mf = t.GetComponent<MeshFilter>();
                    // Unity 프리미티브(Cube/Cylinder) 메시 잔존 금지 — GLB 로드 성공 시 원본 GLB 메시.
                    bool isUnityPrimitive = mf != null && mf.sharedMesh != null &&
                        (mf.sharedMesh.name == "Cube" || mf.sharedMesh.name == "Cylinder");
                    Assert.That(isUnityPrimitive, Is.False,
                        site + " — 프리미티브 기하 잔존(159차 P2 GLB 교체 계약 위반).");
                }
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void G1_Booths_Kitchen_Storage_ReplacedWithGlb()
        {
            GameObject room = ArenaInteriorBuilder.BuildArenaInterior("Empire");
            Assert.That(room, Is.Not.Null);
            try
            {
                // 부스 4세트(탁자/벤치2/격벽) + 주방 4종 + 창고 오크통 5 — 프리미티브 잔존 금지.
                for (int i = 0; i < 4; i++)
                {
                    AssertBoothSiteIsGlb(room, $"Arena_Booth_{i}_Table");
                    AssertBoothSiteIsGlb(room, $"Arena_Booth_{i}_Bench_Left");
                    AssertBoothSiteIsGlb(room, $"Arena_Booth_{i}_Bench_Right");
                    AssertBoothSiteIsGlb(room, $"Arena_Booth_{i}_Partition");
                }
                AssertBoothSiteIsGlb(room, "Arena_Kitchen_Counter");
                AssertBoothSiteIsGlb(room, "Arena_Kitchen_Sink");
                AssertBoothSiteIsGlb(room, "Arena_Kitchen_Furnace");
                for (int i = 0; i < 5; i++)
                    AssertBoothSiteIsGlb(room, $"Arena_Storage_Barrel_{i}");
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }

        private static void AssertBoothSiteIsGlb(GameObject room, string siteName)
        {
            Transform t = room.transform.Find(siteName);
            Assert.That(t, Is.Not.Null, siteName + " — 가구 오브젝트 존재(소실 금지).");
            var mf = t.GetComponent<MeshFilter>();
            bool isUnityPrimitive = mf != null && mf.sharedMesh != null &&
                (mf.sharedMesh.name == "Cube" || mf.sharedMesh.name == "Cylinder");
            Assert.That(isUnityPrimitive, Is.False,
                siteName + " — 프리미티브 기하 잔존(159차 P2 GLB 교체 계약 위반).");
        }

        [Test]
        public void G2_VariantAssignment_IsDeterministic_IntegerHashOnly()
        {
            // Random API 0건 계약 — 동일 시드 해시는 항상 동일 값(결정론).
            int a1 = ArenaInteriorBuilder.StableHash("arena_booth_bench_0_0_empire");
            int a2 = ArenaInteriorBuilder.StableHash("arena_booth_bench_0_0_empire");
            Assert.That(a2, Is.EqualTo(a1), "동일 시드 해시 재호출 — 동일 값(결정론).");

            // 서로 다른 시드 → 해시 분포(전부 동일하면 순환이 아니라 고정 배정).
            int b = ArenaInteriorBuilder.StableHash("arena_booth_bench_0_1_empire");
            int c = ArenaInteriorBuilder.StableHash("arena_booth_bench_1_0_empire");
            Assert.That(a1 == b && b == c, Is.False,
                "배치 인덱스가 다른 시드의 해시가 전부 동일 — 변형 순환 무의미.");

            // TryGetVariantModel — 동일 시드 재호출 시 동일 키(결정론), 국가 오프셋 반영.
            string[] keys = { "Table_2_A", "Table_2_B", "Table_2_C" };
            Assert.That(ArenaInteriorBuilder.TryGetVariantModel(keys, "arena_round_table_0_empire", out var m1, out var k1),
                Is.True, "존재하는 변형 키 후보 — 로드 성공.");
            Assert.That(ArenaInteriorBuilder.TryGetVariantModel(keys, "arena_round_table_0_empire", out var m2, out var k2),
                Is.True, "동일 시드 재호출 — 로드 성공.");
            Assert.That(k2, Is.EqualTo(k1), "동일 시드 변형 배정 — 동일 키(결정론).");
            Assert.That(m2, Is.EqualTo(m1), "동일 시드 변형 배정 — 동일 프리팹.");
        }

        [Test]
        public void G3_VariantModel_MissingFamily_FallsBackFalse()
        {
            // 부재 가족 — false 반환(호출자가 프리미티브 폴백: 가구 소실 금지 계약의 근거).
            string[] missing = { "Arena_NoSuchFurniture_A", "Arena_NoSuchFurniture_B" };
            bool found = ArenaInteriorBuilder.TryGetVariantModel(missing, "seed", out var model, out var usedKey);
            Assert.That(found, Is.False, "부재 가족 — 폴백 신호(false).");
            Assert.That(model, Is.Null);
            Assert.That(usedKey, Is.Null);
        }

        [Test]
        public void G4_GroundingBlobs_Count_ContractUnbroken()
        {
            GameObject room = ArenaInteriorBuilder.BuildArenaInterior("Empire");
            Assert.That(room, Is.Not.Null);
            try
            {
                int blobs = 0;
                foreach (Transform t in room.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.StartsWith("BlobShadow_")) blobs++;
                }
                // 원계약 1~11번(무대1+부스4+창고1+주방1+화로1+원형탁자2+데스크1) + GLB 12번 — 하한 12.
                Assert.That(blobs, Is.GreaterThanOrEqualTo(12),
                    "접지 블롭 개수 계약(1~12번) 무손상 — GLB 교체로 블롭 감소 금지.");
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void G5_BettingDesk_CollisionAndRenderer_ContractKept()
        {
            GameObject room = ArenaInteriorBuilder.BuildArenaInterior("Empire");
            Assert.That(room, Is.Not.Null);
            try
            {
                Transform desk = room.transform.Find("ArenaBettingDesk");
                Assert.That(desk, Is.Not.Null, "베팅 데스크 존재(ArenaInteriorTests 계약 승계).");
                Assert.That(desk.GetComponent<MeshRenderer>(), Is.Not.Null,
                    "I2 계약 — ArenaBettingDesk 루트 MeshRenderer 보장(GLB 루트 캐리어).");
                Assert.That(desk.GetComponentInChildren<Collider>(), Is.Not.Null,
                    "충돌 유지 계약 — GLB 교체 후에도 데스크 콜라이더 존재(BoxCollider 승계).");
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }
    }
}
