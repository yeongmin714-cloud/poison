using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjectName.Core.Data;
using ProjectName.Core.Utils;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [2026-10-15 D4-A — 음식점 건설] 계약 테스트 (EditMode — 순수 로직만 검증).
    /// - BlueprintData: bp_restaurant 존재/스펙 필드(재료 나무5·돌3/골드200/60초/effectId) + 기존 8종 무영향(재료 비용 0).
    /// - VillageDrugMath.PermitFee: 링 배율 허가세 상수표 (Ring4 150/Ring3 100/Ring2 60/Ring1 40).
    /// - ConstructionManager.HasEnoughMaterials: 재료 부족 판정 순수 헬퍼.
    /// - ConstructionManager.TryFindRestaurantVillage: 마을 반경+5m 최근접 매칭 (결정론).
    /// ※ TryPlace의 실제 소비 경로(골드/허가세/재료 소비)는 MonoBehaviour 인스턴스 런타임 경로 —
    ///   Play 게이트로 미실행(ConstructionTests O8 선례 준용). 실행 판정은 QA 서브에이전트 증거로 확정.
    /// </summary>
    public sealed class RestaurantConstructionTests
    {
        // ════════════════════════════════════════════════════════════════════
        // 1. BlueprintData — bp_restaurant 정의 스펙
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void BpRestaurant_Exists_WithSpecFields()
        {
            Assert.IsTrue(BlueprintData.TryGet("bp_restaurant", out var def), "bp_restaurant 정의 존재");

            Assert.AreEqual("마을 음식점", def.displayName, "표시명 스펙");
            Assert.AreEqual(2.5f, def.footprintRadius, 0.0001f, "footprintRadius 2.5 스펙");
            Assert.AreEqual(200, def.goldCost, "골드 비용 200 스펙");
            Assert.AreEqual(60, def.buildTimeSeconds, "건설 시간 60초 스펙");
            Assert.AreEqual("restaurant", def.effectId, "effectId 스펙");
            Assert.AreEqual(5, def.materialWoodCost, "재료 나무 5 스펙");
            Assert.AreEqual(3, def.materialStoneCost, "재료 돌 3 스펙");
            StringAssert.Contains("나무 5", def.description, "설명에 재료 요구 명시");
        }

        [Test]
        public void BlueprintData_Count14_UniqueIds_WithRestaurant()
        {
            // [D4-A] 8종 + 음식점 1종 = 9종. (기존 ConstructionTests의 8 고정 카운트는 스펙상 갱신 필요 — 보고 완료)
            // [90차] 시설 4종 추가로 9 → 13, [125차] 훈련소 추가로 13 → 14 (의미 보존 갱신).
            Assert.AreEqual(14, BlueprintData.Count, "설계도 종수 14 (8 + 음식점 + 시설 4 + 훈련소)");

            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var b in BlueprintData.GetAll())
                Assert.IsTrue(ids.Add(b.id), $"중복 id 없음: {b.id}");
            Assert.IsTrue(ids.Contains("bp_restaurant"), "음식점 id 포함");
        }

        // ════════════════════════════════════════════════════════════════════
        // 2. 기존 정의 무영향 — 재료 비용 0 + 기존 스펙 유지
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void ExistingBlueprints_Unaffected_MaterialCostsZero()
        {
            // 신규 필드(재료 비용) 기본값 0 유지 — 기존 8종 소비처 무영향 검증
            string[] existingIds = { "bp_wall", "bp_gate", "bp_tower", "bp_warehouse_ext", "bp_stable", "bp_smithy", "bp_garden", "bp_watchtower" };
            foreach (var id in existingIds)
            {
                Assert.IsTrue(BlueprintData.TryGet(id, out var def), $"기존 설계도 존재: {id}");
                Assert.AreEqual(0, def.materialWoodCost, $"{id} 나무 재료 비용 0 (무영향)");
                Assert.AreEqual(0, def.materialStoneCost, $"{id} 돌 재료 비용 0 (무영향)");
            }
        }

        [Test]
        public void ExistingBlueprints_OriginalSpecFields_Preserved()
        {
            // 재료 필드 추가가 기존 스펙을 건드리지 않았는지 실측값 스팟 체크
            Assert.IsTrue(BlueprintData.TryGet("bp_wall", out var wall));
            Assert.AreEqual(150, wall.goldCost, "bp_wall 골드 150 유지");
            Assert.AreEqual(30, wall.buildTimeSeconds, "bp_wall 30초 유지");
            Assert.AreEqual(2.0f, wall.footprintRadius, 0.0001f, "bp_wall 반경 2.0 유지");

            Assert.IsTrue(BlueprintData.TryGet("bp_gate", out var gate));
            Assert.AreEqual(300, gate.goldCost, "bp_gate 골드 300 유지");

            Assert.IsTrue(BlueprintData.TryGet("bp_tower", out var tower));
            Assert.AreEqual(500, tower.goldCost, "bp_tower 골드 500 유지");

            Assert.IsTrue(BlueprintData.TryGet("bp_warehouse_ext", out var wh));
            Assert.AreEqual("warehouse_ext", wh.effectId, "bp_warehouse_ext effectId 유지");
        }

        // ════════════════════════════════════════════════════════════════════
        // 3. VillageDrugMath.PermitFee — 링 배율 허가세 상수표 (D4-A 스펙)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void PermitFee_RingTable_MatchesSpec()
        {
            Assert.AreEqual(40, VillageDrugMath.PermitFee(TerritoryDifficulty.Ring1), "Ring1 허가세 40");
            Assert.AreEqual(60, VillageDrugMath.PermitFee(TerritoryDifficulty.Ring2), "Ring2 허가세 60");
            Assert.AreEqual(100, VillageDrugMath.PermitFee(TerritoryDifficulty.Ring3), "Ring3 허가세 100");
            Assert.AreEqual(150, VillageDrugMath.PermitFee(TerritoryDifficulty.Ring4), "Ring4 허가세 150");
        }

        [Test]
        public void PermitFee_MonotonicByRing_And_EmpireDefensive()
        {
            // 안쪽 링일수록 비싸다 (단조 증가) — Ring1 < Ring2 < Ring3 < Ring4
            Assert.That(VillageDrugMath.PermitFee(TerritoryDifficulty.Ring1), Is.LessThan(VillageDrugMath.PermitFee(TerritoryDifficulty.Ring2)));
            Assert.That(VillageDrugMath.PermitFee(TerritoryDifficulty.Ring2), Is.LessThan(VillageDrugMath.PermitFee(TerritoryDifficulty.Ring3)));
            Assert.That(VillageDrugMath.PermitFee(TerritoryDifficulty.Ring3), Is.LessThan(VillageDrugMath.PermitFee(TerritoryDifficulty.Ring4)));

            // Empire에는 마을이 없어 도달하지 않지만 방어 기본값(Ring4 요율) 고정
            Assert.AreEqual(150, VillageDrugMath.PermitFee(TerritoryDifficulty.Empire), "Empire 방어 기본값 150");
        }

        // ════════════════════════════════════════════════════════════════════
        // 4. ConstructionManager.HasEnoughMaterials — 재료 부족 판정 순수 헬퍼
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void HasEnoughMaterials_Sufficient_ReturnsTrue()
        {
            // bp_restaurant 스펙 (나무 5 + 돌 3)
            Assert.IsTrue(ConstructionManager.HasEnoughMaterials(5, 3, 5, 3), "정확히 충분 — true");
            Assert.IsTrue(ConstructionManager.HasEnoughMaterials(10, 5, 5, 3), "여유 보유 — true");
        }

        [Test]
        public void HasEnoughMaterials_Shortage_ReturnsFalse()
        {
            Assert.IsFalse(ConstructionManager.HasEnoughMaterials(4, 3, 5, 3), "나무 1개 부족 — false");
            Assert.IsFalse(ConstructionManager.HasEnoughMaterials(5, 2, 5, 3), "돌 1개 부족 — false");
            Assert.IsFalse(ConstructionManager.HasEnoughMaterials(0, 0, 5, 3), "전무 — false");
        }

        [Test]
        public void HasEnoughMaterials_ZeroNeed_TreatedAsSatisfied()
        {
            // 재료 비용 0 설계도(기존 8종) — 요구 0은 항상 충족 (기존 소비처 무영향 보장의 논리적 기반)
            Assert.IsTrue(ConstructionManager.HasEnoughMaterials(0, 0, 0, 0), "요구 0 — true");
            Assert.IsTrue(ConstructionManager.HasEnoughMaterials(5, 0, 5, 0), "돌 요구 0이면 돌 보유 무관");
        }

        // ════════════════════════════════════════════════════════════════════
        // 5. ConstructionManager.TryFindRestaurantVillage — 마을 매칭 (결정론)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void TryFindRestaurantVillage_AtVillageCenter_MatchesThatVillage()
        {
            var villages = VillagePlacementSystem.GetVillages(NationType.East);
            Assert.Greater(villages.Count, 0, "동국 마을 목록 존재");

            var target = villages[0];
            bool found = ConstructionManager.TryFindRestaurantVillage(target.center, out NationType nation, out int villageIndex);

            Assert.IsTrue(found, "마을 중심에서는 반드시 매칭");
            Assert.AreEqual(NationType.East, nation, "국가 매칭");
            Assert.AreEqual(0, villageIndex, "마을 인덱스 매칭");
        }

        [Test]
        public void TryFindRestaurantVillage_FarFromAnyVillage_ReturnsFalse()
        {
            // 전 영지 밴드 최원(~1550m)+마진을 크게 벗어난 점
            bool found = ConstructionManager.TryFindRestaurantVillage(new Vector3(20000f, 0f, 20000f), out NationType nation, out int villageIndex);

            Assert.IsFalse(found, "어느 마을 반경+5m 밖 — 매칭 실패");
            Assert.AreEqual(NationType.None, nation, "실패 시 nation None");
            Assert.AreEqual(-1, villageIndex, "실패 시 인덱스 -1");
        }

        [Test]
        public void TryFindRestaurantVillage_MarginBoundary_InsideMatches_OutsideRejects()
        {
            var villages = VillagePlacementSystem.GetVillages(NationType.South);
            var target = villages[0];
            float radius = target.radius;

            // 마진 내부 (반경+4m) — 매칭 성공
            bool inside = ConstructionManager.TryFindRestaurantVillage(
                target.center + new Vector3(radius + 4f, 0f, 0f), out _, out _);
            Assert.IsTrue(inside, "마을 반경+4m 지점은 마진 5m 내 — 매칭");

            // 마진 외부 (반경+6m) — 매칭 실패 (근접 타 마을 없음이 실측 — 최소 간격 111.9m)
            bool outside = ConstructionManager.TryFindRestaurantVillage(
                target.center + new Vector3(radius + 6f, 0f, 0f), out _, out _);
            Assert.IsFalse(outside, "마을 반경+6m 지점은 마진 5m 밖 — 매칭 실패");
        }

        // ════════════════════════════════════════════════════════════════════
        // 6. [92차 결함1] 완료 루프 사본 버그 수리 + 시공 중 재배치 가드 + 토스트 방어
        //    근거: docs/92_PANEL_ATTACH_AND_LOCOMOTION_PLAN.md "결함 1"
        //    (포렌식 — "건설 완료" 로그 3,450회 / 토스트 큐 3,449개 적층 → UIRoot 렌더 파괴)
        // ════════════════════════════════════════════════════════════════════

        private const string ConstructionManagerPath = "Assets/Scripts/Systems/ConstructionManager.cs";
        private const string UtkControlsPath = "Assets/Scripts/UI/Toolkit/UTKControls.cs";

        /// <summary>
        /// [92차 결함1] 로직 테스트 — 동일 구조물 ForceComplete 2회 호출 시 StructureCompleted 1회만 발화.
        /// 수리 전(사본 버그): CompleteStructure 내부 isComplete=true가 메서드 지역 사본에만 반영되고
        /// 미완료 사본이 write-back되어 재호출 시 재발화(실측 로그 3,450회). 수리 후: 목록 사본이
        /// 완료 상태로 write-back되어 재발화하지 않는다. 비공개 _structures에 리플렉션 직접 주입
        /// (75차 P18 리플렉션 패턴 준용 — TryPlace 런타임 경로 회피, EditMode 규약).
        /// </summary>
        [Test]
        public void ForceComplete_DuplicateCall_FiresStructureCompletedOnce_AndWritesBackComplete()
        {
            var go = new GameObject("CM92_RefireGuard");
            var cm = go.AddComponent<ConstructionManager>();
            try
            {
                var list = (List<ConstructionManager.StructureEntry>)typeof(ConstructionManager)
                    .GetField("_structures", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(cm);
                list.Add(new ConstructionManager.StructureEntry
                {
                    structureId = "struct_92_refire",
                    blueprintId = "bp_wall",
                    territoryId = "East_01",
                    pos = Vector3.zero,
                    progress = 0f,
                    isComplete = false,
                });

                var completed = new List<ConstructionManager.StructureEntry>();
                System.Action<ConstructionManager.StructureEntry> handler = completed.Add;
                ConstructionManager.StructureCompleted += handler;
                try
                {
                    cm.ForceComplete("struct_92_refire");
                    cm.ForceComplete("struct_92_refire"); // 재호출 — 수리 전엔 재발화(2회 발화)

                    Assert.AreEqual(1, completed.Count, "동일 구조물 StructureCompleted 1회만 발화");
                    Assert.IsTrue(cm.Structures[0].isComplete, "목록 write-back 사본이 완료 상태 반영");
                    Assert.AreEqual(1f, cm.Structures[0].progress, 0.0001f, "progress 1 write-back 유지");
                }
                finally
                {
                    ConstructionManager.StructureCompleted -= handler; // 정적 이벤트 위생
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        /// <summary>[92차 결함1] 완료 루프 — 루프 사본에 isComplete 직접 세팅 코드 존재 소스 가드.</summary>
        [Test]
        public void ConstructionManager_CompletionLoop_SetsIsCompleteDirectly_SourceGuard()
        {
            string src = System.IO.File.ReadAllText(ConstructionManagerPath);

            Assert.That(src, Does.Contain("[92차 결함1]"), "[92차] 수리 마커 존재");
            Assert.That(src, Does.Contain("s.isComplete = true;"), "완료 루프 isComplete 직접 세팅 코드 존재");

            // 직접 세팅이 CompleteStructure 호출에 '선행' — 사본이 완료 상태로 write-back 보장
            int setIsComplete = src.IndexOf("s.isComplete = true;", System.StringComparison.Ordinal);
            int completeCall = src.IndexOf("CompleteStructure(s);", System.StringComparison.Ordinal);
            Assert.Greater(setIsComplete, -1, "isComplete 직접 세팅 위치");
            Assert.Greater(completeCall, -1, "CompleteStructure 호출 위치");
            Assert.Less(setIsComplete, completeCall,
                "isComplete 직접 세팅이 CompleteStructure 호출에 선행 — write-back 보장");
        }

        /// <summary>[92차 결함1] TryPlace 음식점 분기 — 시공 중(진행 중 bp_restaurant) 재배치 가드 존재 소스 가드.</summary>
        [Test]
        public void TryPlace_InProgressRestaurantGuard_SourceGuard()
        {
            string src = System.IO.File.ReadAllText(ConstructionManagerPath);

            Assert.That(src, Does.Contain("이미 음식점이 있는 마을(시공 중 포함)"),
                "restaurantBuilt(완성 시점만 true)만 검사하던 기존 가드 보완 — 시공 중 포함 거부 문구");
            Assert.That(src, Does.Contain("RestaurantPropBuilder.RestaurantBlueprintId"),
                "[88차] 스팟 점유 판정 동일 기준 — bp_restaurant 구조물 매칭");
            Assert.That(src, Does.Contain("foreach (var restaurantEntry in Structures)"),
                "ConstructionManager.Structures 순회 가드");
        }

        /// <summary>[92차 결함1] UTKToastService — 동일 문구 0.5s 중복 억제 + 큐 상한 8 방어 코드 존재 소스 가드.</summary>
        [Test]
        public void UtkToastService_DuplicateSuppress_AndQueueCap_SourceGuard()
        {
            string src = System.IO.File.ReadAllText(UtkControlsPath);

            Assert.That(src, Does.Contain("DuplicateSuppressSeconds = 0.5f"),
                "동일 문구 0.5s 중복 억제 상수 존재");
            Assert.That(src, Does.Contain("QueueCap = 8"), "토스트 큐 상한 8 상수 존재");
            Assert.That(src, Does.Contain("< DuplicateSuppressSeconds"),
                "Show 경로에서 중복 억제 판정 사용");
            Assert.That(src, Does.Contain(">= QueueCap"),
                "Show 경로에서 큐 상한 판정 사용");
        }
    }
}
