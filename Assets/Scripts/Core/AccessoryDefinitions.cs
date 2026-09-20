using System.Collections.Generic;

namespace ProjectName.Core
{
    /// <summary>
    /// [Milestone B] 장신구(반지/목걸이) 스탯 정의 — 아이템 id → 스탯 종류 + %값.
    /// 장착/효과 적용(PlayerStats 연동)은 후속 마일스톤; 여기는 데이터 맵(단일 소스).
    /// </summary>
    public static class AccessoryDefinitions
    {
        public enum AccStat { Evasion, MaxHp, Attack, Defense }

        public struct AccBonus
        {
            public AccStat stat;
            public float percent;
            public AccBonus(AccStat s, float p) { stat = s; percent = p; }
        }

        private static readonly Dictionary<string, AccBonus> _map = new Dictionary<string, AccBonus>
        {
            { "ring_evasion",   new AccBonus(AccStat.Evasion, 3f) },
            { "ring_vitality",  new AccBonus(AccStat.MaxHp,   10f) },
            { "ring_power",     new AccBonus(AccStat.Attack,  6f) },
            { "necklace_hp",    new AccBonus(AccStat.MaxHp,   15f) },
            { "necklace_guard", new AccBonus(AccStat.Defense, 8f) },
        };

        /// <summary>장신구 아이템 ID로 %버프 정의 조회 (없으면 false).</summary>
        public static bool TryGet(string itemId, out AccBonus bonus)
        {
            return _map.TryGetValue(itemId, out bonus);
        }
    }
}
