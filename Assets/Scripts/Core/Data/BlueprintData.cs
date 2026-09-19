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
        };

        /// <summary>전체 설계도 (정적 배열 — 변경 금지).</summary>
        public static BlueprintDef[] All => _all;

        /// <summary>설계도 종수 (8).</summary>
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
