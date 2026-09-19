using System.Collections.Generic;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Core
{
    /// <summary>
    /// Phase O1 (C-O1-03): 세트 파밍 튜닝 계산기 (OpenMMO ITEM_TIERS.md 벤치마크).
    ///
    /// 목표: "필요 파츠 K개를 각각 확률 p로 독립 드랍할 때, 세트 완성까지의 기대 파밍 횟수 ≈ 5회".
    /// 기대 횟수 닫힌식: E = H_K / p (H_K = 1 + 1/2 + ... + 1/K — 쿠폰 콜렉터).
    ///   K=1, p=20% → 5.00회   K=2, p=30% → 5.00회
    ///   K=3, p=33% → 5.56회   K=4, p=37% → 5.63회
    ///
    /// 월드 드랍(희소·거래템) 규약: 상위 지역 처치 시 0.1~0.5% — 확정 파밍 경로 없음(의도적 예외).
    /// Core 계층(static) — Systems/UI 참조 금지.
    /// </summary>
    public static class SetChestTuning
    {
        /// <summary>남은 파츠 수 K에 따른 권장 독립 롤 확률 (기대 ≈5회 기준표).</summary>
        public static float GetChanceForRemainingParts(int remainingParts)
        {
            switch (Mathf.Clamp(remainingParts, 1, 4))
            {
                case 1: return 0.20f;
                case 2: return 0.30f;
                case 3: return 0.33f;
                default: return 0.37f;
            }
        }

        /// <summary>
        /// 세트 완성까지의 기대 파밍 횟수 (닫힌식 H_K/p).
        /// </summary>
        /// <param name="missingParts">아직 수집하지 못한 파츠 수 (K)</param>
        /// <param name="perPartChance">파츠당 독립 드랍 확률 p (0~1)</param>
        public static float ExpectedRunsToComplete(int missingParts, float perPartChance)
        {
            if (missingParts <= 0 || perPartChance <= 0f) return 0f;
            float harmonic = 0f;
            for (int k = 1; k <= missingParts; k++) harmonic += 1f / k;
            return harmonic / perPartChance;
        }

        /// <summary>레벨 밴드별 세트 종류 — Lv1~10 가죽 / Lv11~20 사슬 / Lv21+ 판금.</summary>
        public static EquipmentSetKind GetSetKindForLevel(int level)
        {
            if (level <= 10) return EquipmentSetKind.Leather;
            if (level <= 20) return EquipmentSetKind.Chain;
            return EquipmentSetKind.Plate;
        }

        /// <summary>레벨 밴드별 세트 파츠 독립 롤 확률 (하위 밴드일수록 낮게 — 파밍 페이스 유지).</summary>
        public static float GetRollChanceForLevel(int level)
        {
            switch (GetSetKindForLevel(level))
            {
                case EquipmentSetKind.Leather: return 0.30f;
                case EquipmentSetKind.Chain:   return 0.33f;
                default:                       return 0.37f;
            }
        }

        /// <summary>월드 드랍(희소·거래템) 상한/하한 — 확정 파밍 경로 없음. 상위 지역일수록 가중.</summary>
        public const float WorldRareDropChanceMin = 0.001f; // 0.1%
        public const float WorldRareDropChanceMax = 0.005f; // 0.5%

        /// <summary>
        /// 순수 함수: 병사/상자 레벨 밴드에 맞는 세트 파츠 롤 결과.
        /// 소유 중(=바구니에 이미 존재)인 파츠는 제외하고, 나머지를 각각 독립 롤한다.
        /// rng는 0~1 균등 난수 공급자(테스트에서 결정론 주입 가능).
        /// </summary>
        public static List<PlayerInventory.ItemData> RollSetPiecesForLevel(
            int level, List<string> ownedItemIds, System.Func<double> nextRandom01)
        {
            var result = new List<PlayerInventory.ItemData>();
            if (nextRandom01 == null) return result;

            var kind = GetSetKindForLevel(level);
            float chance = GetRollChanceForLevel(level);

            foreach (var part in EquipmentTierSet.GetSetParts(kind))
            {
                if (part == null || string.IsNullOrEmpty(part.id)) continue;
                if (ownedItemIds != null && ownedItemIds.Contains(part.id)) continue;

                if (nextRandom01() <= chance)
                    result.Add(part);
            }
            return result;
        }
    }
}
