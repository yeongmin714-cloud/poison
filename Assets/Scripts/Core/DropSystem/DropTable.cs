using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>
    /// G2-07: 드랍 테이블 ScriptableObject (향상된 버전)
    ///
    /// 몬스터/병사 등급별 드랍 항목 + 희귀 확률 + 레벨 보정을 지원합니다.
    /// 
    /// 사용 예:
    ///   MonsterTier.Beginner: 고기 1~2개 (100%), 재료 1개 (60%), 희귀 20%
    ///   MonsterTier.Intermediate: 고기 2~3개 (100%), 재료 1~2개 (80%), 희귀 30%
    ///   MonsterTier.Advanced: 고기 3~5개 (100%), 재료 2~3개 (90%), 희귀 50%
    ///   Soldier: 장비/아이템 (등급별 확률)
    /// </summary>
    [CreateAssetMenu(fileName = "NewDropTable", menuName = "Game/Drop Table", order = 51)]
    public class DropTable : ScriptableObject
    {
        [System.Serializable]
        public class DropEntry
        {
            [Header("아이템")]
            public PlayerInventory.ItemData item;

            [Header("개수 범위")]
            [Tooltip("최소 드랍 개수 (1 이상)")]
            public int minCount = 1;
            [Tooltip("최대 드랍 개수 (minCount 이상)")]
            public int maxCount = 1;

            [Header("드랍 확률")]
            [Range(0f, 1f)]
            public float dropChance = 1f;

            [Header("속성")]
            [Tooltip("희귀 아이템 여부 (레벨 보정 확률 적용)")]
            public bool isRare = false;

            [Tooltip("드랍 설명 (디버그/UI용)")]
            public string description = "";
        }

        [Header("=== 기본 정보 ===")]
        [SerializeField] private string _tableName = "Drop Table";
        [SerializeField] private MonsterTier _tier = MonsterTier.Beginner;

        [Header("=== 드랍 항목 ===")]
        public DropEntry[] entries;

        [Header("=== 희귀 드랍 보정 ===")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("기본 희귀 드랍 확률")]
        private float _baseRareChance = 0.2f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("티어 상승 시 추가 희귀 확률")]
        private float _rareChanceBonusPerTier = 0.1f;

        [SerializeField]
        [Tooltip("레벨 10당 추가 희귀 드랍 확률 (0.05 = 5%)")]
        private float _rareDropBonusPer10Levels = 0.05f;

        // ===== Public 접근자 =====
        public string TableName => _tableName;
        public MonsterTier Tier => _tier;
        public float BaseRareChance => _baseRareChance;
        public float RareChanceBonusPerTier => _rareChanceBonusPerTier;
        public float RareDropBonusPer10Levels => _rareDropBonusPer10Levels;

        /// <summary>
        /// 드랍 테이블을 LootBasket에 적용합니다.
        /// 각 항목의 dropChance 확률로 드랍을 결정하고,
        /// 희귀 아이템에는 레벨 보정을 적용합니다.
        /// </summary>
        /// <param name="basket">대상 LootBasket</param>
        public void ApplyToBasket(ILootBasket basket)
        {
            ApplyToBasket(basket, 0f);
        }

        /// <summary>
        /// 레벨 보정을 포함하여 드랍 테이블을 LootBasket에 적용합니다.
        /// </summary>
        /// <param name="basket">대상 LootBasket</param>
        /// <param name="levelDropBonus">레벨 기반 희귀 드랍 보정 (0~1)</param>
        public void ApplyToBasket(ILootBasket basket, float levelDropBonus)
        {
            if (basket == null) return;
            if (entries == null || entries.Length == 0) return;

            foreach (var entry in entries)
            {
                if (entry == null || entry.item == null || entry.dropChance <= 0f) continue;

                float finalChance = entry.dropChance;

                // 희귀 아이템에 레벨 보정 적용
                if (entry.isRare)
                {
                    finalChance = Mathf.Clamp01(finalChance + levelDropBonus);
                }

                if (Random.value <= finalChance)
                {
                    int safeMin = Mathf.Max(1, entry.minCount);
                    int safeMax = Mathf.Max(safeMin, entry.maxCount);
                    int count = Random.Range(safeMin, safeMax + 1);
                    if (count > 0)
                    {
                        basket.AddItem(entry.item, count);
                        Debug.Log($"[DropTable] 🎁 {entry.item.displayName} x{count} 드랍! (확률: {finalChance * 100:F1}%)");
                    }
                }
            }

            // 티어 기반 희귀 보너스 롤
            TryRollRareBonus(basket, levelDropBonus);
        }

        /// <summary>
        /// 등급별 희귀 드랍 보너스 확률 롤.
        /// _baseRareChance + (티어 인덱스 * _rareChanceBonusPerTier) + 레벨 보정
        /// </summary>
        private void TryRollRareBonus(ILootBasket basket, float levelDropBonus)
        {
            int tierIndex = (int)_tier; // Beginner=0, Intermediate=1, Advanced=2
            float rareChance = _baseRareChance + (tierIndex * _rareChanceBonusPerTier) + levelDropBonus;
            rareChance = Mathf.Clamp01(rareChance);

            // 희귀 드랍 항목이 별도로 있으면 추가 롤
            foreach (var entry in entries)
            {
                if (entry == null || entry.item == null || !entry.isRare) continue;

                if (Random.value <= rareChance)
                {
                    int safeMin = Mathf.Max(1, entry.minCount);
                    int safeMax = Mathf.Max(safeMin, entry.maxCount);
                    int count = Random.Range(safeMin, safeMax + 1);
                    if (count > 0)
                    {
                        basket.AddItem(entry.item, count);
                        Debug.Log($"[DropTable] ★ 희귀 드랍! {entry.item.displayName} x{count} (확률: {rareChance * 100:F1}%)");
                    }
                }
            }
        }

        /// <summary>
        /// 드랍 테이블 요약 문자열 (디버그/UI/테스트용)
        /// </summary>
        public string GetDropSummary()
        {
            string summary = $"[{_tableName}] (티어: {_tier})\n";
            summary += $"기본 희귀 확률: {_baseRareChance * 100:F1}%\n";
            summary += $"티어별 희귀 보너스: {_rareChanceBonusPerTier * 100:F1}%\n";

            if (entries == null || entries.Length == 0)
            {
                summary += "항목 수: 0 (할당되지 않음)\n";
                return summary;
            }

            summary += $"항목 수: {entries.Length}\n";

            foreach (var entry in entries)
            {
                if (entry == null || entry.item == null) continue;
                string rareMark = entry.isRare ? " ★" : "";
                summary += $"  - {entry.item.displayName} x{entry.minCount}~{entry.maxCount} ({entry.dropChance * 100:F1}%){rareMark}\n";
            }

            return summary;
        }
    }
}