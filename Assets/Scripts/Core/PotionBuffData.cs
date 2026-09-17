using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ProjectName.Core
{
    /// <summary>
    /// 2026-09-17: 물약 → 능력치 버프 맵. 복용 물약이 병사/플레이어에게 주는 효과를 정적 표로 정의.
    ///
    /// PotionUseSystem은 id 내 키워드(heal/hp/attack/speed/stealth 등)로 효과를 분기하므로,
    /// 아래 표준 물약 id는 그 키워드 규칙과도 호환된다(예: "potion_hp_small"은 "hp" → 회복 경로).
    /// </summary>
    public readonly struct PotionBuffEffect
    {
        public readonly float healFlat;      // 즉시 회복 고정량
        public readonly float healPercent;   // 최대체력 비율 회복 (0이면 미사용)
        public readonly float attackBuff;    // 공격력 버프
        public readonly float defenseBuff;   // 방어력 버프
        public readonly float agilityBuff;   // 민첩 버프
        public readonly float buffSeconds;   // 버프 지속시간(초). 0이면 즉시 효과만.

        public PotionBuffEffect(float healFlat, float healPercent, float attackBuff,
            float defenseBuff, float agilityBuff, float buffSeconds)
        {
            this.healFlat = healFlat;
            this.healPercent = healPercent;
            this.attackBuff = attackBuff;
            this.defenseBuff = defenseBuff;
            this.agilityBuff = agilityBuff;
            this.buffSeconds = buffSeconds;
        }
    }

    /// <summary>표준 물약 id → PotionBuffEffect 정적 조회 맵.</summary>
    public static class PotionBuffData
    {
        private static readonly Dictionary<string, PotionBuffEffect> _map =
            new Dictionary<string, PotionBuffEffect>
            {
                { "potion_hp_small",  new PotionBuffEffect(20f, 0f, 0f, 0f, 0f, 0f) },          // 체력 +20 즉시
                { "potion_hp_big",    new PotionBuffEffect(50f, 0f, 0f, 0f, 0f, 0f) },          // 체력 +50 즉시
                { "potion_attack",    new PotionBuffEffect(0f, 0f, 5f, 0f, 0f, 30f) },          // 공격 +5, 30초
                { "potion_defense",   new PotionBuffEffect(0f, 0f, 0f, 5f, 0f, 30f) },          // 방어 +5, 30초
                { "potion_agility",   new PotionBuffEffect(0f, 0f, 0f, 0f, 3f, 30f) },          // 민첩 +3, 30초
            };

        /// <summary>전체 버프 목록 (UI 표시용 — ReadOnlyCollection).</summary>
        public static readonly ReadOnlyCollection<KeyValuePair<string, PotionBuffEffect>> AllEffects =
            new ReadOnlyCollection<KeyValuePair<string, PotionBuffEffect>>(
                new List<KeyValuePair<string, PotionBuffEffect>>(_map));

        /// <summary>물약 id → 버프 효과. 알 수 없는 id면 false 반환.</summary>
        public static bool GetBuff(string itemId, out PotionBuffEffect effect)
        {
            effect = default(PotionBuffEffect);
            if (string.IsNullOrEmpty(itemId)) return false;
            return _map.TryGetValue(itemId.ToLowerInvariant(), out effect);
        }
    }
}