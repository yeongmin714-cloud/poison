using System.Collections.Generic;
using UnityEngine;
#pragma warning disable 0649

namespace ProjectName.Core.Data
{
    /// <summary>
    /// [Phase O8 C-O8-01] 성 영지 건설 설계도 정적 DB.
    /// 벤치: docs/reference/openmmo/HOUSE_BUILDING.md (설계도 + 재료/위치 검증 + 성공 시 소비).
    /// - 8종 설계도 (footprint 반경 / 골드 / 건설 시간): 골드 유출구(O2) + 시간 부담 밸런스.
    /// - TryGet / GetAll / Count — foreach 순회 전용 (LINQ 금지 규약).
    /// - ValidatePlacement: 영지 경계 + 기존 건설물 겹침 순수 검증 (지형/경사는 TerrainFlatCheck — 런타임 전용).
    /// Core.Data — Systems 참조 없음 (계층 규약 준수).
    /// </summary>
    public static class BlueprintData
    {
        /// <summary>설계도 정의 (struct — 생성 후 변경 불가, 복사본 수정 버그 방지).</summary>
        public struct BlueprintDef
        {
            public string id;
            public string displayName;
            public float footprintRadius;   // 기존 건설물과의 겹침 판정 반경 (m)
            public int goldCost;            // 시공 착수 시 즉시 지출 (골드 유출구 — O2 원장 태그 "construction")
            public int buildTimeSeconds;    // 시공 진행 시간 (progress += dt / buildTimeSeconds)
            public string description;
            public string effectId;         // 완성 효과 식별자 ("none" = 효과 없음)

            // [D4-A] 건설 재료 비용 — 0이면 재료 소비 없음(기존 설계도 전부 0 유지 → 기존 소비처 무영향).
            // TryPlace 규약: 재료 부족 시 골드/재료 "전부" 소비 없음(벤치 "검증 실패 시 아이템 유지" 준용).
            public int materialWoodCost;    // 필요 나무 수량 (PlayerInventory "mat_wood" — 실측 id)
            public int materialStoneCost;   // 필요 돌 수량 (PlayerInventory "mat_stone" — 실측 id)

            // [90차 A1] 신규 재료 비용 3종 — materialStoneCost 뒤에 추가(기존 필드 순서 유지 규약).
            // 0 기본값 → 기존 8종 설계도 무영향(요구 0 = 보유 무관 충족, HasEnoughMaterials 규약).
            public int materialIronCost;    // 필요 철광석 수량 (PlayerInventory.IronOre 실측 id "iron_ore")
            public int materialLeatherCost; // 필요 가죽 수량 (PlayerInventory.BoarLeather 실측 id "mat_boar_leather")
            public int materialManaCost;    // 필요 수정석 수량 (PlayerInventory.CrystalShard 실측 id "crystal_shard" — '마력석' 라벨은 수정석 단일소스)
        }

        private static readonly BlueprintDef[] _all =
        {
            new BlueprintDef
            {
                id = "bp_wall", displayName = "외벽 구간", footprintRadius = 2.0f,
                goldCost = 150, buildTimeSeconds = 30, effectId = "none",
                description = "영지 경계를 따라 올리는 석벽."
            },
            new BlueprintDef
            {
                id = "bp_gate", displayName = "대문", footprintRadius = 2.5f,
                goldCost = 300, buildTimeSeconds = 60, effectId = "none",
                description = "사람과 마차가 오가는 영지의 정문."
            },
            new BlueprintDef
            {
                id = "bp_tower", displayName = "감시탑", footprintRadius = 3.0f,
                goldCost = 500, buildTimeSeconds = 120, effectId = "none",
                description = "영지를 내려다보는 감시탑. 원거리 경계에 쓰인다."
            },
            new BlueprintDef
            {
                id = "bp_warehouse_ext", displayName = "창고 증축", footprintRadius = 2.5f,
                goldCost = 250, buildTimeSeconds = 90, effectId = "warehouse_ext",
                description = "창고를 증축한다. 완성 시 슬롯 +5 (골드 미지출 무료 확장)."
            },
            new BlueprintDef
            {
                id = "bp_stable", displayName = "축사", footprintRadius = 3.0f,
                goldCost = 400, buildTimeSeconds = 150, effectId = "stable",
                description = "탈것을 돌보는 축사. 군마 운용의 기반 (O10 연계 예약)."
            },
            new BlueprintDef
            {
                id = "bp_smithy", displayName = "대장간", footprintRadius = 3.0f,
                goldCost = 450, buildTimeSeconds = 120, effectId = "smithy",
                description = "무구를 벼리는 대장간. 수리비 할인 판정(HasStructure)용."
            },
            new BlueprintDef
            {
                id = "bp_garden", displayName = "정원", footprintRadius = 2.5f,
                goldCost = 200, buildTimeSeconds = 60, effectId = "garden",
                description = "병사들의 사기를 끌어올리는 정원 (사기 시스템 연계 예약)."
            },
            new BlueprintDef
            {
                id = "bp_watchtower", displayName = "망루", footprintRadius = 2.8f,
                goldCost = 350, buildTimeSeconds = 100, effectId = "watchtower",
                description = "국경 방향을 감시하는 망루."
            },
            new BlueprintDef
            {
                // [D4-A] 마을 음식점 — 마을 반경 내 전용 배치 + 영주 허가세 + 재료(나무5/돌3).
                // 재료 비용 외 검증은 ConstructionManager.TryPlace의 음식점 전용 분기(TryFindRestaurantVillage)가 담당.
                // [90차 A4] 음식점은 성(영지 중심) 내부 배치 금지 — TryPlace 음식점 분기 최상단 IsIndoorPlacement.
                id = "bp_restaurant", displayName = "마을 음식점", footprintRadius = 2.5f,
                goldCost = 200, buildTimeSeconds = 60, effectId = "restaurant",
                materialWoodCost = 5, materialStoneCost = 3,
                description = "마을에 음식점을 짓는다. 나무 5 + 돌 3 필요. 병사가 요리를 판매한다."
            },
            new BlueprintDef
            {
                // [90차 A2] 요리 크래프트 시설 — 생산 · 식사와 회복 음식 제작. 완공 후 E키 시설 개방(FacilityPropBuilder).
                id = "bp_cooking", displayName = "요리 크래프트", footprintRadius = 2.0f,
                goldCost = 50, buildTimeSeconds = 45, effectId = "cooking_facility",
                materialWoodCost = 8, materialStoneCost = 4,
                description = "식사와 회복 음식을 제작하는 요리 시설. 나무 8 + 돌 4 필요. 완공 후 E키로 요리 크래프트를 사용한다."
            },
            new BlueprintDef
            {
                // [90차 A2] 장비 크래프트 시설 — 생산 · 무기와 방어구 제작.
                id = "bp_forge", displayName = "장비 크래프트", footprintRadius = 3.0f,
                goldCost = 100, buildTimeSeconds = 90, effectId = "forge",
                materialWoodCost = 12, materialStoneCost = 8, materialIronCost = 6, materialLeatherCost = 4,
                description = "무기와 방어구를 제작하는 장비 시설. 나무 12 + 돌 8 + 철 6 + 가죽 4 필요. 완공 후 E키로 장비 크래프트를 사용한다."
            },
            new BlueprintDef
            {
                // [90차 A2] 병사배치 시설 — 방어 · 병력 주둔과 거점 수비. 주변 병사는 건설을 자동 지원한다(A5).
                id = "bp_barracks", displayName = "병사배치", footprintRadius = 3.5f,
                goldCost = 150, buildTimeSeconds = 120, effectId = "barracks",
                materialWoodCost = 10, materialStoneCost = 8, materialIronCost = 4,
                description = "병력이 주둔하고 거점을 수비하는 병사배치 시설. 나무 10 + 돌 8 + 철 4 필요. 완공 후 E키로 병사배치를 사용한다."
            },
            new BlueprintDef
            {
                // [90차 A2] 연금술 크래프트 시설 — 생산 · 물약과 강화 재료 제작. 수정석(마력석 라벨 단일소스) 사용.
                id = "bp_alchemy", displayName = "연금술 크래프트", footprintRadius = 2.0f,
                goldCost = 120, buildTimeSeconds = 75, effectId = "alchemy_facility",
                materialWoodCost = 7, materialStoneCost = 5, materialManaCost = 2,
                description = "물약과 강화 재료를 제작하는 연금술 시설. 나무 7 + 돌 5 + 수정석 2 필요. 완공 후 E키로 연금술 크래프트를 사용한다."
            },

            new BlueprintDef
            {
                // [125차] 훈련소 — 병사를 단련시키는 시설. 영지당 1개(ConstructionManager 검증).
                id = "bp_traininghall", displayName = "훈련소", footprintRadius = 3.0f,
                goldCost = 600, buildTimeSeconds = 120, effectId = "training_hall",
                materialWoodCost = 12, materialStoneCost = 8,
                description = "병사를 단련시키는 훈련소. 나무 12 + 돌 8 필요. 완공 후 E키로 훈련을 시작한다."
            },
        };

        /// <summary>전체 설계도 (정적 배열 — 변경 금지).</summary>
        public static BlueprintDef[] All => _all;

        /// <summary>설계도 종수 (8 + [D4-A] 음식점 1 + [90차 A2] 크래프트 시설 4 + [125차] 훈련소 1 = 14).</summary>
        public static int Count => _all.Length;

        /// <summary>모든 설계도 열거 (foreach 순회용).</summary>
        public static IEnumerable<BlueprintDef> GetAll()
        {
            foreach (var def in _all)
                yield return def;
        }

        /// <summary>id로 설계도 조회. 성공 true + def, 실패 false (def는 default).</summary>
        public static bool TryGet(string id, out BlueprintDef def)
        {
            def = default;
            if (string.IsNullOrEmpty(id))
                return false;
            foreach (var candidate in _all)
            {
                if (candidate.id == id)
                {
                    def = candidate;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 시공 위치 순수 검증 (지형/경사 불포함 — 런타임 Raycast는 TerrainFlatCheck).
        /// ① 영지 경계 내: dist(pos, center) + footprintRadius &lt;= territoryRadius
        /// ② 기존 건설물 겹침 없음: dist(pos, e.pos) &gt;= footprintRadius + e.radius (접촉=인접 허용)
        /// </summary>
        public static bool ValidatePlacement(
            Vector3 territoryCenter, float territoryRadius, Vector3 pos, float footprintRadius,
            List<(Vector3 pos, float radius)> existing)
        {
            // ① 영지 경계 검증
            if (Vector3.Distance(pos, territoryCenter) + footprintRadius > territoryRadius)
                return false;

            // ② 기존 건설물 겹침 검증
            if (existing != null)
            {
                foreach (var e in existing)
                {
                    if (Vector3.Distance(pos, e.pos) < footprintRadius + e.radius)
                        return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 지형 평탄 검증 — 런타임 전용 (EditMode 테스트 미대상).
        /// pos 위 2m에서 아래로 5m 레이캐스트, 법선 y &gt;= 0.9 (≈26° 이하 경사)면 평지.
        /// 충돌체 없는 환경(테스트/프리뷰)에서는 평지로 취급하는 안전 폴백.
        /// </summary>
        public static bool TerrainFlatCheck(Vector3 pos)
        {
            if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f))
                return hit.normal.y >= 0.9f;
            return true; // 지형 충돌체 없음 → 평지 폴백
        }
    }
}
