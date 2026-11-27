using System.Collections.Generic;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [90차 Phase A] 크래프트 시설 설계도 + 재료 확장 + 음식점 실내 금지 + 병사 건설 지원 계약 테스트.
    /// 스펙: docs/90_CONSTRUCTION_MANAGEMENT_FIGMA_PLAN.md Part A (A1~A6).
    /// ① 5종 설계도 존재 + 재료 확정치(감독자 확정 표) ② HasEnoughMaterials 10인자 규약(요구 0 = 무관 충족)
    /// ③ TryPlace 재료 소비/롤백 5종 확장 소스 어서션 ④ 음식점 실내 금지(IsIndoorPlacement + 거부 메시지)
    /// ⑤ 병사 건설 지원(배율 1+0.25N, 상한 2.0) ⑥ FacilityPropBuilder(4종 실물 + E키 개방 라우팅).
    /// ※ TryPlace 실소비/시공 경로는 Play 게이트(ConstructionTests O8 선례) — 본 파일은 순수 로직 + 소스 어서션.
    /// </summary>
    public sealed class BlueprintMaterialTests
    {
        private const string ConstructionManagerSource = "Assets/Scripts/Systems/ConstructionManager.cs";
        private const string FacilityPropBuilderSource = "Assets/Scripts/Systems/FacilityPropBuilder.cs";
        private const string BlueprintDataSource = "Assets/Scripts/Core/Data/BlueprintData.cs";

        // ════════════════════════════════════════════════════════════════════
        // ① [90차 A2] 5종 설계도 존재 + 재료 확정치 (감독자 확정 표 — Figma 목업 수량 미채용)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void A1_FacilityBlueprints_Exist_WithConfirmedMaterials()
        {
            // bp_cooking — 요리 크래프트: 골드 50 · 45초 · 나무 8 · 돌 4 (footprint 2.0)
            Assert.IsTrue(BlueprintData.TryGet("bp_cooking", out var cooking), "bp_cooking 존재");
            Assert.AreEqual("요리 크래프트", cooking.displayName, "요리 표시명");
            Assert.AreEqual(2.0f, cooking.footprintRadius, 0.0001f, "요리 footprint 2.0");
            Assert.AreEqual(50, cooking.goldCost, "요리 골드 50");
            Assert.AreEqual(45, cooking.buildTimeSeconds, "요리 시공 45s");
            Assert.AreEqual("cooking_facility", cooking.effectId, "요리 effectId");
            Assert.AreEqual(8, cooking.materialWoodCost, "요리 나무 8");
            Assert.AreEqual(4, cooking.materialStoneCost, "요리 돌 4");
            Assert.AreEqual(0, cooking.materialIronCost, "요리 철 0 (미사용)");
            Assert.AreEqual(0, cooking.materialLeatherCost, "요리 가죽 0 (미사용)");
            Assert.AreEqual(0, cooking.materialManaCost, "요리 수정석 0 (미사용)");
            StringAssert.Contains("나무 8", cooking.description, "요리 설명에 재료 반영");
            StringAssert.Contains("돌 4", cooking.description, "요리 설명에 재료 반영");

            // bp_forge — 장비 크래프트: 골드 100 · 90초 · 나무 12 · 돌 8 · 철 6 · 가죽 4 (footprint 3.0)
            Assert.IsTrue(BlueprintData.TryGet("bp_forge", out var forge), "bp_forge 존재");
            Assert.AreEqual("장비 크래프트", forge.displayName, "장비 표시명");
            Assert.AreEqual(3.0f, forge.footprintRadius, 0.0001f, "장비 footprint 3.0");
            Assert.AreEqual(100, forge.goldCost, "장비 골드 100");
            Assert.AreEqual(90, forge.buildTimeSeconds, "장비 시공 90s");
            Assert.AreEqual("forge", forge.effectId, "장비 effectId");
            Assert.AreEqual(12, forge.materialWoodCost, "장비 나무 12");
            Assert.AreEqual(8, forge.materialStoneCost, "장비 돌 8");
            Assert.AreEqual(6, forge.materialIronCost, "장비 철 6");
            Assert.AreEqual(4, forge.materialLeatherCost, "장비 가죽 4");
            Assert.AreEqual(0, forge.materialManaCost, "장비 수정석 0 (미사용)");
            StringAssert.Contains("철 6", forge.description, "장비 설명에 재료 반영");
            StringAssert.Contains("가죽 4", forge.description, "장비 설명에 재료 반영");

            // bp_barracks — 병사배치: 골드 150 · 120초 · 나무 10 · 돌 8 · 철 4 (footprint 3.5)
            Assert.IsTrue(BlueprintData.TryGet("bp_barracks", out var barracks), "bp_barracks 존재");
            Assert.AreEqual("병사배치", barracks.displayName, "병사배치 표시명");
            Assert.AreEqual(3.5f, barracks.footprintRadius, 0.0001f, "병사배치 footprint 3.5");
            Assert.AreEqual(150, barracks.goldCost, "병사배치 골드 150");
            Assert.AreEqual(120, barracks.buildTimeSeconds, "병사배치 시공 120s");
            Assert.AreEqual("barracks", barracks.effectId, "병사배치 effectId");
            Assert.AreEqual(10, barracks.materialWoodCost, "병사배치 나무 10");
            Assert.AreEqual(8, barracks.materialStoneCost, "병사배치 돌 8");
            Assert.AreEqual(4, barracks.materialIronCost, "병사배치 철 4");
            Assert.AreEqual(0, barracks.materialLeatherCost, "병사배치 가죽 0 (미사용)");
            Assert.AreEqual(0, barracks.materialManaCost, "병사배치 수정석 0 (미사용)");

            // bp_alchemy — 연금술 크래프트: 골드 120 · 75초 · 나무 7 · 돌 5 · 수정석 2 (footprint 2.0)
            Assert.IsTrue(BlueprintData.TryGet("bp_alchemy", out var alchemy), "bp_alchemy 존재");
            Assert.AreEqual("연금술 크래프트", alchemy.displayName, "연금술 표시명");
            Assert.AreEqual(2.0f, alchemy.footprintRadius, 0.0001f, "연금술 footprint 2.0");
            Assert.AreEqual(120, alchemy.goldCost, "연금술 골드 120");
            Assert.AreEqual(75, alchemy.buildTimeSeconds, "연금술 시공 75s");
            Assert.AreEqual("alchemy_facility", alchemy.effectId, "연금술 effectId");
            Assert.AreEqual(7, alchemy.materialWoodCost, "연금술 나무 7");
            Assert.AreEqual(5, alchemy.materialStoneCost, "연금술 돌 5");
            Assert.AreEqual(0, alchemy.materialIronCost, "연금술 철 0 (미사용)");
            Assert.AreEqual(0, alchemy.materialLeatherCost, "연금술 가죽 0 (미사용)");
            Assert.AreEqual(2, alchemy.materialManaCost, "연금술 수정석 2");
            StringAssert.Contains("수정석 2", alchemy.description, "연금술 설명에 재료 반영");

            // [90차] bp_restaurant 기존 계약 유지 — 88차 D4 보호 (골드/재료/시공 무변경)
            Assert.IsTrue(BlueprintData.TryGet("bp_restaurant", out var restaurant), "bp_restaurant 존재");
            Assert.AreEqual(200, restaurant.goldCost, "음식점 골드 200 유지");
            Assert.AreEqual(60, restaurant.buildTimeSeconds, "음식점 시공 60s 유지");
            Assert.AreEqual(5, restaurant.materialWoodCost, "음식점 나무 5 유지");
            Assert.AreEqual(3, restaurant.materialStoneCost, "음식점 돌 3 유지");
            Assert.AreEqual(0, restaurant.materialIronCost, "음식점 철 0 (기존 계약)");
        }

        [Test]
        public void A1_BlueprintCount14_Existing8_UnaffectedByNewMaterialFields()
        {
            // [90차] 8종(기존) + 음식점 1 + 시설 4 + [125차] 훈련소 1 = 14종. 신규 재료 필드 3종 기본값 0 — 기존 8종 무영향.
            Assert.AreEqual(14, BlueprintData.Count, "설계도 종수 14");

            string[] existingIds =
            {
                "bp_wall", "bp_gate", "bp_tower", "bp_warehouse_ext",
                "bp_stable", "bp_smithy", "bp_garden", "bp_watchtower"
            };
            foreach (var id in existingIds)
            {
                Assert.IsTrue(BlueprintData.TryGet(id, out var def), $"기존 설계도 존재: {id}");
                Assert.AreEqual(0, def.materialWoodCost, $"{id} 나무 0 (무영향)");
                Assert.AreEqual(0, def.materialStoneCost, $"{id} 돌 0 (무영향)");
                Assert.AreEqual(0, def.materialIronCost, $"{id} [90차] 철 0 (무영향)");
                Assert.AreEqual(0, def.materialLeatherCost, $"{id} [90차] 가죽 0 (무영향)");
                Assert.AreEqual(0, def.materialManaCost, $"{id} [90차] 수정석 0 (무영향)");
            }
        }

        [Test]
        public void A1_MaterialItemIds_MatchPlayerInventorySingleSource()
        {
            // [90차 A1] 재료 id 단일소스 — ConstructionManager 상수와 PlayerInventory 실측 정적 정의 일치.
            Assert.AreEqual(PlayerInventory.Mat_Wood.id, ConstructionManager.MaterialWoodItemId, "나무 id 단일소스");
            Assert.AreEqual(PlayerInventory.Mat_Stone.id, ConstructionManager.MaterialStoneItemId, "돌 id 단일소스");
            Assert.AreEqual(PlayerInventory.IronOre.id, ConstructionManager.MaterialIronItemId, "[90차] 철광석 id 단일소스");
            Assert.AreEqual(PlayerInventory.BoarLeather.id, ConstructionManager.MaterialLeatherItemId, "[90차] 가죽 id 단일소스");
            Assert.AreEqual(PlayerInventory.CrystalShard.id, ConstructionManager.MaterialManaItemId, "[90차] 수정석 id 단일소스('마력석' 라벨 통일)");
        }

        // ════════════════════════════════════════════════════════════════════
        // ② [90차 A1] HasEnoughMaterials 10인자 오버로드 — 요구 0 = 무관 충족 규약
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void A1_HasEnoughMaterials_10Arg_Contract()
        {
            // bp_forge 스펙 (나무 12 + 돌 8 + 철 6 + 가죽 4) — 정확히 충분/여유 보유
            Assert.IsTrue(ConstructionManager.HasEnoughMaterials(12, 8, 6, 4, 0, 12, 8, 6, 4, 0),
                "정확히 충분 — true");
            Assert.IsTrue(ConstructionManager.HasEnoughMaterials(20, 10, 9, 6, 5, 12, 8, 6, 4, 0),
                "여유 보유 — true");

            // 부족 — 각 재료 1개씩 모자란 경우 false
            Assert.IsFalse(ConstructionManager.HasEnoughMaterials(11, 8, 6, 4, 0, 12, 8, 6, 4, 0),
                "나무 1개 부족 — false");
            Assert.IsFalse(ConstructionManager.HasEnoughMaterials(12, 7, 6, 4, 0, 12, 8, 6, 4, 0),
                "돌 1개 부족 — false");
            Assert.IsFalse(ConstructionManager.HasEnoughMaterials(12, 8, 5, 4, 0, 12, 8, 6, 4, 0),
                "철 1개 부족 — false");
            Assert.IsFalse(ConstructionManager.HasEnoughMaterials(12, 8, 6, 3, 0, 12, 8, 6, 4, 0),
                "가죽 1개 부족 — false");
            Assert.IsFalse(ConstructionManager.HasEnoughMaterials(12, 8, 6, 4, 1, 12, 8, 6, 4, 2),
                "수정석 부족 — false");
            Assert.IsFalse(ConstructionManager.HasEnoughMaterials(0, 0, 0, 0, 0, 12, 8, 6, 4, 2),
                "전무 — false");

            // bp_alchemy 스펙 (나무 7 + 돌 5 + 수정석 2)
            Assert.IsTrue(ConstructionManager.HasEnoughMaterials(7, 5, 0, 0, 2, 7, 5, 0, 0, 2),
                "연금술 정확히 충분 — true");
        }

        [Test]
        public void A1_HasEnoughMaterials_10Arg_ZeroNeed_Irrelevant()
        {
            // 규약: 요구량 ≤ 0 재료는 보유와 무관하게 충족 — 기존 8종(재료 0) 무영향의 논리적 기반.
            Assert.IsTrue(ConstructionManager.HasEnoughMaterials(0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
                "전 요구 0 — true");
            Assert.IsTrue(ConstructionManager.HasEnoughMaterials(0, 0, 0, 0, 99, 0, 0, 0, 0, 2),
                "수정석 외 전무 — 수정석만 충족이면 true (요구 0 재료 무관)");
            Assert.IsTrue(ConstructionManager.HasEnoughMaterials(5, 0, 9, 0, 0, 5, 0, 6, 0, 0),
                "돌/가죽 요구 0 — 보유 무관");
        }

        [Test]
        public void A1_HasEnoughMaterials_4Arg_Signature_Preserved()
        {
            // [90차 A1] 기존 4인자 시그니처 유지 — RestaurantConstructionTests 등 기존 소비처 호환.
            Assert.IsTrue(ConstructionManager.HasEnoughMaterials(5, 3, 5, 3), "4인자 정확히 충분 — true");
            Assert.IsFalse(ConstructionManager.HasEnoughMaterials(4, 3, 5, 3), "4인자 부족 — false");
        }

        // ════════════════════════════════════════════════════════════════════
        // ③ [90차 A1] TryPlace 재료 소비/롤백 5종 확장 — 소스 어서션 (소비 경로는 Play 게이트)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void A1_TryPlace_MaterialConsumptionRollback_ExtendedTo5Materials()
        {
            string src = System.IO.File.ReadAllText(ConstructionManagerSource);

            // [90차] 마커 + 사전 합산 판정(부분 소비 금지 원칙) 유지
            Assert.That(src, Does.Contain("[90차 A1]")
                .Or.Contain("[90차 A4]"), "[90차] 마커 존재");
            StringAssert.Contains("사전 합산 판정", src, "사전 합산 판정 주석 유지");

            // 신규 재료 id 상수 3종 — 실측 단일소스
            StringAssert.Contains("MaterialIronItemId = \"iron_ore\"", src, "철광석 상수");
            StringAssert.Contains("MaterialLeatherItemId = \"mat_boar_leather\"", src, "가죽 상수");
            StringAssert.Contains("MaterialManaItemId = \"crystal_shard\"", src, "수정석 상수");

            // 소비 — 철/가죽/수정석 RemoveItem 확장
            StringAssert.Contains("inv.RemoveItem(MaterialIronItemId", src, "철 소비");
            StringAssert.Contains("inv.RemoveItem(MaterialLeatherItemId", src, "가죽 소비");
            StringAssert.Contains("inv.RemoveItem(MaterialManaItemId", src, "수정석 소비");

            // 롤백 — 실패 시 전액 환불(실제 소비분만 복구) 확장
            StringAssert.Contains("inv.AddItem(PlayerInventory.IronOre", src, "철 롤백");
            StringAssert.Contains("inv.AddItem(PlayerInventory.BoarLeather", src, "가죽 롤백");
            StringAssert.Contains("inv.AddItem(PlayerInventory.CrystalShard", src, "수정석 롤백");
            StringAssert.Contains("stats.AddGold(def.goldCost, \"construction\")", src, "골드 전액 환불 유지");
            StringAssert.Contains("stats.AddGold(permitFee, \"restaurant_permit\")", src, "허가세 환불 유지");
        }

        // ════════════════════════════════════════════════════════════════════
        // ④ [90차 A4] 음식점 실내 배치 금지 — IsIndoorPlacement + 거부 메시지
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void A4_IndoorBan_SourceAssertions()
        {
            string src = System.IO.File.ReadAllText(ConstructionManagerSource);

            Assert.That(src, Does.Contain("IsIndoorPlacement(pos)")
                .And.Contain("음식점은 실내에 배치할 수 없습니다 — 마을에서 건설하세요")
                .And.Contain("[90차 A4]"), "실내 금지 검사(분기 최상단) + 거부 메시지");
            StringAssert.Contains("CastleInteriorRadius", src, "성 내부 반경 상수");
        }

        [Test]
        public void A4_CastleInteriorRadius_Is25m_BelowVillageRadius()
        {
            // [90차 A4] 실측 상수 — 권장 상한 25m 이하(마을 반경 40m/마을 중심 최소 이격 38m와 충돌 없음).
            // 근거: 월드 성 GLB 정규화 반경 25m(TerritoryBuilder.NormalizeCastleScale) + keep 로비 팔각
            // 바닥 정점 최대 반경 √964 ≈ 31.05m(실내 씬 기준 — 월드보다 넓어 미채용).
            Assert.AreEqual(25f, ConstructionManager.CastleInteriorRadius, 0.0001f, "성 내부 반경 25m");
            Assert.Less(ConstructionManager.CastleInteriorRadius, VillagePlacementSystem.VillageRadius,
                "마을 반경(40m)보다 작은 값 — 스펙 하드 제약");
        }

        [Test]
        public void A4_IsIndoorPlacement_TerritoryCenter_True_VillageCenter_False()
        {
            // 영지 중심(성 위치) = 실내 → 음식점 거부 대상.
            var db = TerritoryDatabase.Instance;
            Assert.IsNotNull(db, "TerritoryDatabase lazy 생성");
            var eastDef = db.GetDefinition("East_01");
            Assert.IsTrue(ConstructionManager.IsIndoorPlacement(eastDef.worldPosition),
                "영지 중심(성) 반경 내 — 실내 판정 true");

            // 마을 중심 = 실외(성 중심에서 38~46m 이격 — VillagePlacementSystem 실측) → 배치 가능.
            var villages = VillagePlacementSystem.GetVillages(NationType.East);
            Assert.Greater(villages.Count, 0, "동국 마을 존재");
            Assert.IsFalse(ConstructionManager.IsIndoorPlacement(villages[0].center),
                "마을 중심은 성 내부 반경 밖 — 실내 판정 false");

            // 전 마을 80개 — 성(최근접 영지 중심) 반경 25m 밖 보장(마을 배치와 충돌 없음의 실증).
            foreach (var nation in new[] { NationType.East, NationType.West, NationType.South, NationType.North })
            {
                foreach (var village in VillagePlacementSystem.GetVillages(nation))
                {
                    Assert.IsFalse(ConstructionManager.IsIndoorPlacement(village.center),
                        $"{nation} 마을#{village.index} 중심은 실내 아님");
                }
            }

            // 극단 원거리 — false (판정 안전).
            Assert.IsFalse(ConstructionManager.IsIndoorPlacement(new Vector3(20000f, 0f, 20000f)),
                "전 영지 밖 원거리 — 실내 판정 false");
        }

        [Test]
        public void A4_Test15RestaurantPlacementSpot_RemainsOutdoor()
        {
            // [90차 A4] Test15(TestTerritoryCombatSetup) 음식점 배치 지점 — 마을 중심에서 성 방향 12m.
            // 성 중심까지 최소 26m(offsetDist 38~46 − 12) > 성 내부 반경 25m → 실제 건설 경로 보존 검증.
            var villages = VillagePlacementSystem.GetVillages(NationType.East);
            var village = villages[0];
            Vector3 toCastle = village.castleCenter - village.center;
            Vector3 restPos = village.center + (toCastle.sqrMagnitude > 0.01f ? toCastle.normalized : Vector3.back) * 12f;
            restPos.y = 0f;

            Assert.IsFalse(ConstructionManager.IsIndoorPlacement(restPos),
                "Test15 음식점 배치 지점(성에서 최소 26m) — 실내 거부 없음");
        }

        // ════════════════════════════════════════════════════════════════════
        // ⑤ [90차 A5] 병사 건설 자동 지원 — 배율 1 + 0.25×N, 상한 2.0
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void A5_ComputeAssistMultiplier_Formula_AndCap()
        {
            Assert.AreEqual(1f, ConstructionManager.ComputeAssistMultiplier(0), 0.0001f, "병사 0명 — 배율 1.0");
            Assert.AreEqual(1.25f, ConstructionManager.ComputeAssistMultiplier(1), 0.0001f, "병사 1명 — 배율 1.25");
            Assert.AreEqual(1.5f, ConstructionManager.ComputeAssistMultiplier(2), 0.0001f, "병사 2명 — 배율 1.5");
            Assert.AreEqual(1.75f, ConstructionManager.ComputeAssistMultiplier(3), 0.0001f, "병사 3명 — 배율 1.75");
            Assert.AreEqual(2f, ConstructionManager.ComputeAssistMultiplier(4), 0.0001f, "병사 4명 — 배율 2.0");
            Assert.AreEqual(2f, ConstructionManager.ComputeAssistMultiplier(10), 0.0001f, "병사 10명 — 상한 2.0 캡");
            Assert.AreEqual(1f, ConstructionManager.ComputeAssistMultiplier(-5), 0.0001f,
                "음수 방어 — Max(0, N) 클램프로 배율 1.0");

            // 상수 실측 — 스펙 확정치
            Assert.AreEqual(0.25f, ConstructionManager.AssistMultiplierPerGuard, 0.0001f, "1명당 가산 0.25");
            Assert.AreEqual(2.0f, ConstructionManager.AssistMultiplierCap, 0.0001f, "배율 상한 2.0");
            Assert.AreEqual(12f, ConstructionManager.AssistRadius, 0.0001f, "지원 반경 12m");
        }

        [Test]
        public void A5_AssistApis_NoStructures_ReturnDefaults()
        {
            // EditMode — 구조물 미등록 상태: GetAssistCount 0 / GetAssistMultiplier 1 (진행 중이 아니면 1/0 규약).
            var go = new GameObject("CM_90");
            var cm = go.AddComponent<ConstructionManager>();
            try
            {
                Assert.AreEqual(0, cm.GetAssistCount("unknown_struct"), "미등록 구조물 — 지원 수 0");
                Assert.AreEqual(1f, cm.GetAssistMultiplier("unknown_struct"), 0.0001f, "미등록 구조물 — 배율 1");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void A5_Assist_SourceAssertions()
        {
            string src = System.IO.File.ReadAllText(ConstructionManagerSource);

            Assert.That(src, Does.Contain("[90차 A5]")
                .And.Contain("GetAllPlayerGuards()")
                .And.Contain("guard.IsRecruited")
                .And.Contain("guard.IsAlive")
                .And.Contain("_lastAssistCount")
                .And.Contain("GetAssistMultiplier(string structureId)")
                .And.Contain("GetAssistCount(string structureId)"), "지원 소비처/상태 조회 API");

            // 로그 1회성 문구 — "병사 N명 건설 지원 — 배율 X"
            StringAssert.Contains("명 건설 지원 — 배율 ", src, "지원 시작/변화 로그 문구");
        }

        // ════════════════════════════════════════════════════════════════════
        // ⑥ [90차 A3] FacilityPropBuilder — 4종 실물 + E키 시설 개방 라우팅
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void A6_FacilityPropBuilder_SourceAssertions()
        {
            string src = System.IO.File.ReadAllText(FacilityPropBuilderSource);

            Assert.That(src, Does.Contain("[90차 A3]")
                .And.Contain("bp_cooking")
                .And.Contain("bp_forge")
                .And.Contain("bp_barracks")
                .And.Contain("bp_alchemy")
                .And.Contain("bp_restaurant는 스킵"), "시설 4종 설계도 + 음식점 스킵(기존 소유)");

            // 4종 실물 프리미티브 — 결정론 조합 (모닥불 원+솥 / 모루+화덕 / 막사+사면 지붕 / 탁자+플라스크)
            StringAssert.Contains("Campfire_Base", src, "요리 — 모닥불 원");
            StringAssert.Contains("Cooking_Pot", src, "요리 — 솥 실린더");
            StringAssert.Contains("Anvil", src, "장비 — 모루 박스");
            StringAssert.Contains("Forge_Hearth", src, "장비 — 화덕");
            StringAssert.Contains("Barracks_Body", src, "병사배치 — 막사 박스");
            StringAssert.Contains("Roof_Front", src, "병사배치 — 사면 지붕");
            StringAssert.Contains("Alchemy_Table", src, "연금술 — 탁자 박스");
            StringAssert.Contains("Alchemy_Flask", src, "연금술 — 플라스크 원통");

            // BuildingPlaceholder(Other, 시설명) + 접지/구독/캐시 패턴
            StringAssert.Contains("BuildingType.Other", src, "BuildingPlaceholder Other");
            StringAssert.Contains("RuntimeInitializeOnLoadMethod", src, "구독 선례 패턴");
            StringAssert.Contains("FacilityProps_Root", src, "실물 컨테이너");
            StringAssert.Contains("GetHeightAt(pos.x, pos.z, BiomeType.Plains, TerrainSeed)", src, "접지 GetHeightAt(Plains,42)");
        }

        [Test]
        public void A6_FacilityOpenRouting_Prompts_AndSaveRebuild()
        {
            string facilitySrc = System.IO.File.ReadAllText(FacilityPropBuilderSource);
            string constructionSrc = System.IO.File.ReadAllText(ConstructionManagerSource);

            // E키 개방 라우팅 — 정적 이벤트(effectId 문자열 페이로드 — NationType 아님) + 프롬프트 문구
            Assert.That(facilitySrc, Does.Contain("FacilityOpenRequested")
                .And.Contain("event System.Action<string>")
                .And.Contain("\"E: 요리 크래프트\"")
                .And.Contain("\"E: 장비 크래프트\"")
                .And.Contain("\"E: 병사배치\"")
                .And.Contain("\"E: 연금술 크래프트\""), "FacilityOpenRequested(effectId) + 프롬프트 4종");

            // RestaurantPropInteractable 패턴 준용 — E키 폴링 + 프롬프트 브리지
            StringAssert.Contains("Keyboard.current.eKey.wasPressedThisFrame", facilitySrc, "E키 폴링");
            StringAssert.Contains("EInteractionPromptBridge.RaiseRegister", facilitySrc, "프롬프트 등록");

            // 세이브 복원 훅 — ApplySaveEntries 마지막 훅에서 RebuildFromSave 호출
            StringAssert.Contains("FacilityPropBuilder.RebuildFromSave()", constructionSrc, "ApplySaveEntries 복원 훅");
            StringAssert.Contains("RebuildFromSave", facilitySrc, "복원 메서드");
        }

        [Test]
        public void A6_BlueprintDataSource_Markers()
        {
            // [90차] 설계도 원장 마커 + 신규 필드 3종 순서(materialStoneCost 뒤) 소스 어서션.
            string src = System.IO.File.ReadAllText(BlueprintDataSource);
            Assert.That(src, Does.Contain("[90차 A1]")
                .And.Contain("[90차 A2]")
                .And.Contain("materialIronCost")
                .And.Contain("materialLeatherCost")
                .And.Contain("materialManaCost"), "[90차] 원장 마커 + 신규 재료 필드 3종");
        }
    }
}
