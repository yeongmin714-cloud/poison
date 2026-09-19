using System.Collections.Generic;
using UnityEngine;
#pragma warning disable 0414

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

        /// <summary>
        /// C-O1-02: 시그니처(확정) 드랍 항목.
        /// 확률 롤 없이 100% 드랍되는 확정 보상 (ITEM_TIERS.md "시그니처 + 아이템별 독립 롤").
        /// 시그니처와 동일한 id의 항목은 entries의 독립 롤에서 제외되어 중복을 막는다.
        /// </summary>
        [System.Serializable]
        public class SignatureDropEntry
        {
            [Header("아이템")]
            public PlayerInventory.ItemData item;

            [Header("개수 범위")]
            [Tooltip("최소 드랍 개수 (1 이상)")]
            public int minCount = 1;
            [Tooltip("최대 드랍 개수 (minCount 이상)")]
            public int maxCount = 1;

            [Tooltip("시그니처 드랍 설명 (디버그/UI용)")]
            public string description = "";
        }

        [Header("=== 기본 정보 ===")]
        [SerializeField] private string _tableName = "Drop Table";
        [SerializeField] private MonsterTier _tier = MonsterTier.Beginner;

        [Header("=== 드랍 항목 ===")]
        public DropEntry[] entries;

        [Header("=== 시그니처 드랍 (확정) ===")]
        [Tooltip("확률 없이 100% 드랍되는 확정 보상. 비어 있으면 기존 동작과 완전히 동일 (하위호환)")]
        public SignatureDropEntry[] signatureEntries;

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
        /// C-O1-02: 유효한 시그니처(확정) 드랍이 하나라도 있는가? (item이 할당된 항목 기준)
        /// </summary>
        public bool HasSignatureDrops
        {
            get
            {
                if (signatureEntries == null) return false;
                foreach (var sig in signatureEntries)
                {
                    if (sig != null && sig.item != null) return true;
                }
                return false;
            }
        }

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

            // C-O1-02: 시그니처(확정) 드랍을 독립 롤 전에 먼저 적용.
            // 확정 보상이므로 확률 롤도, 레벨 보정(levelDropBonus)도 없이 100% 추가된다.
            ApplySignatureEntries(basket);

            if (entries == null || entries.Length == 0)
            {
                // entries가 비었을 때의 기존 동작 유지 (시그니처는 이미 적용됨)
                return;
            }

            // C-O1-02: 시그니처와 동일한 id의 항목은 독립 롤에서 제외 (중복 방지 — id 비교).
            // 시그니처가 없으면 집합이 비어 기존 동작과 완전히 동일하다.
            HashSet<string> signatureIds = HasSignatureDrops ? CollectSignatureIds() : null;

            foreach (var entry in entries)
            {
                if (entry == null || entry.item == null || entry.dropChance <= 0f) continue;
                if (signatureIds != null && !string.IsNullOrEmpty(entry.item.id) && signatureIds.Contains(entry.item.id)) continue;

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

            // 티어 기반 희귀 보너스 롤 (시그니처 중복 id는 여기서도 제외)
            TryRollRareBonus(basket, levelDropBonus, signatureIds);
        }

        /// <summary>
        /// 등급별 희귀 드랍 보너스 확률 롤.
        /// _baseRareChance + (티어 인덱스 * _rareChanceBonusPerTier) + 레벨 보정
        /// </summary>
        /// <param name="excludeSignatureIds">C-O1-02: 시그니처(확정)로 이미 지급된 id 집합 — 해당 항목은 롤에서 제외 (null 허용)</param>
        private void TryRollRareBonus(ILootBasket basket, float levelDropBonus, HashSet<string> excludeSignatureIds = null)
        {
            int tierIndex = (int)_tier; // Beginner=0, Intermediate=1, Advanced=2
            float rareChance = _baseRareChance + (tierIndex * _rareChanceBonusPerTier) + levelDropBonus;
            rareChance = Mathf.Clamp01(rareChance);

            // 희귀 드랍 항목이 별도로 있으면 추가 롤
            foreach (var entry in entries)
            {
                if (entry == null || entry.item == null || !entry.isRare) continue;
                if (excludeSignatureIds != null && !string.IsNullOrEmpty(entry.item.id) && excludeSignatureIds.Contains(entry.item.id)) continue;

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

        // ===== 시그니처(확정) 드랍 (C-O1-02) =====

        /// <summary>
        /// 시그니처(확정) 드랍을 basket에 적용합니다 (독립 롤 전에 호출됨).
        /// 확률 롤 없이 100% 추가되며, basket에 이미 같은 id가 있어도
        /// 중복 방지 스킵 규칙을 적용하지 않고 그대로 추가합니다 (확정 보상).
        /// 레벨 보정(levelDropBonus)의 영향을 받지 않습니다.
        /// signatureEntries가 비어 있으면 아무 동작도 하지 않고 Random도 소모하지 않습니다 (하위호환).
        /// </summary>
        private void ApplySignatureEntries(ILootBasket basket)
        {
            if (!HasSignatureDrops) return;

            foreach (var sig in signatureEntries)
            {
                if (sig == null || sig.item == null) continue;

                int safeMin = Mathf.Max(1, sig.minCount);
                int safeMax = Mathf.Max(safeMin, sig.maxCount);
                int count = Random.Range(safeMin, safeMax + 1);
                if (count > 0)
                {
                    basket.AddItem(sig.item, count);
                    string desc = string.IsNullOrEmpty(sig.description) ? "" : $" — {sig.description}";
                    Debug.Log($"[DropTable] 🎯 시그니처(확정) 드랍! {sig.item.displayName} x{count}{desc}");
                }
            }
        }

        /// <summary>
        /// 시그니처 항목들의 id 집합을 수집합니다 (독립 롤 중복 제외 판정용 — id 비교).
        /// </summary>
        private HashSet<string> CollectSignatureIds()
        {
            var ids = new HashSet<string>();
            if (signatureEntries == null) return ids;

            foreach (var sig in signatureEntries)
            {
                if (sig == null || sig.item == null || string.IsNullOrEmpty(sig.item.id)) continue;
                ids.Add(sig.item.id);
            }
            return ids;
        }

        /// <summary>
        /// C-O1-02: 시그니처(확정) 드랍 요약 문자열 (디버그/UI/테스트용)
        /// </summary>
        public string GetSignatureSummary()
        {
            if (!HasSignatureDrops)
                return "시그니처 드랍: 없음";

            int validCount = 0;
            foreach (var sig in signatureEntries)
            {
                if (sig != null && sig.item != null) validCount++;
            }

            string summary = $"시그니처 드랍: {validCount}건 (100% 확정)\n";
            foreach (var sig in signatureEntries)
            {
                if (sig == null || sig.item == null) continue;
                string desc = string.IsNullOrEmpty(sig.description) ? "" : $" — {sig.description}";
                summary += $"  - {sig.item.displayName} x{sig.minCount}~{sig.maxCount} (확정){desc}\n";
            }
            return summary;
        }

        /// <summary>
        /// C-O1-02: 독립 롤 항목의 기대 드랍 개수 합 (∑ dropChance × 평균 개수).
        /// 시그니처 항목과 시그니처와 동일한 id의 항목은 실제 롤에서 제외되므로 기대치에서도 제외.
        /// 희귀 보너스 롤과 레벨 보정은 미포함한 순수 기대치 (C-O1-03 튜닝용).
        /// </summary>
        public float GetExpectedDropCount()
        {
            if (entries == null || entries.Length == 0) return 0f;

            HashSet<string> signatureIds = HasSignatureDrops ? CollectSignatureIds() : null;
            float expected = 0f;

            foreach (var entry in entries)
            {
                if (entry == null || entry.item == null || entry.dropChance <= 0f) continue;
                if (signatureIds != null && !string.IsNullOrEmpty(entry.item.id) && signatureIds.Contains(entry.item.id)) continue;

                int safeMin = Mathf.Max(1, entry.minCount);
                int safeMax = Mathf.Max(safeMin, entry.maxCount);
                float avgCount = (safeMin + safeMax) * 0.5f;
                expected += entry.dropChance * avgCount;
            }
            return expected;
        }

        /// <summary>
        /// 드랍 테이블 요약 문자열 (디버그/UI/테스트용)
        /// </summary>
        public string GetDropSummary()
        {
            string summary = $"[{_tableName}] (티어: {_tier})\n";
            summary += $"기본 희귀 확률: {_baseRareChance * 100:F1}%\n";
            summary += $"티어별 희귀 보너스: {_rareChanceBonusPerTier * 100:F1}%\n";

            // C-O1-02: 시그니처(확정) 드랍 섹션 (시그니처가 있을 때만 추가 — 기존 출력 유지)
            if (HasSignatureDrops)
                summary += GetSignatureSummary();

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