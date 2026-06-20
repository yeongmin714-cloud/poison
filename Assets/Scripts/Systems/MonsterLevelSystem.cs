using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// C9-14: 몬스터 레벨 시스템 (티어별 기본Lv + 영지 보정)
    /// 
    /// 몬스터 티어(초반/중반/후반)에 따라 기본 레벨이 결정되고,
    /// 영지 난이도에 따라 레벨이 보정됩니다.
    /// </summary>
    public static class MonsterLevelSystem
    {
        // 티어별 기본 레벨 범위
        public static Vector2Int GetBaseLevelRange(MonsterTier tier)
        {
            switch (tier)
            {
                case MonsterTier.Beginner:       return new Vector2Int(1, 5);
                case MonsterTier.Intermediate:   return new Vector2Int(6, 15);
                case MonsterTier.Advanced:       return new Vector2Int(16, 30);
                default: return new Vector2Int(1, 3);
            }
        }

        /// <summary>
        /// 영지 난이도 보정치 계산 (ROADMAP 5.3.5)
        /// Ring1=+0, Ring2=+2, Ring3=+5, Ring4=+8, Empire=+15
        /// </summary>
        public static int GetDifficultyBonus(TerritoryDifficulty difficulty)
        {
            switch (difficulty)
            {
                case TerritoryDifficulty.Ring1: return 0;   // 최외각 — 보정 없음
                case TerritoryDifficulty.Ring2: return 2;   // +2
                case TerritoryDifficulty.Ring3: return 5;   // +5
                case TerritoryDifficulty.Ring4: return 8;   // +8
                case TerritoryDifficulty.Empire: return 15; // +15
                default: return 0;
            }
        }

        /// <summary>
        /// 몬스터 최종 레벨 생성
        /// </summary>
        public static int GenerateMonsterLevel(MonsterTier tier, TerritoryDifficulty territoryDifficulty)
        {
            var baseRange = GetBaseLevelRange(tier);
            int baseLevel = Random.Range(baseRange.x, baseRange.y + 1);
            int bonus = GetDifficultyBonus(territoryDifficulty);
            return Mathf.Clamp(baseLevel + bonus, 1, 50);
        }

        // ===== 레벨당 스탯 =====

        /// <summary>
        /// 레벨 기반 HP 계산 (티어별 배수)
        /// </summary>
        public static float CalculateHP(MonsterTier tier, int level)
        {
            float hpPerLevel;
            switch (tier)
            {
                case MonsterTier.Beginner:       hpPerLevel = 5f;  break; // 초반+5
                case MonsterTier.Intermediate:   hpPerLevel = 10f; break; // 중반+10
                case MonsterTier.Advanced:       hpPerLevel = 20f; break; // 후반+20
                default: hpPerLevel = 5f; break;
            }
            return hpPerLevel * level;
        }

        /// <summary>
        /// 레벨 기반 데미지 계산
        /// </summary>
        public static float CalculateDamage(int level)
        {
            return 1f + level * 1.5f;
        }

        /// <summary>
        /// 레벨 기반 경험치 보상
        /// </summary>
        public static float CalculateXP(int level)
        {
            return 5f + level * 2f;
        }

        // ===== 레벨 표시 및 색상 =====

        /// <summary>
        /// 레벨 색상 태그 반환 (머리 위 표시용)
        /// 🟢 Lv.1~10, 🟡 Lv.11~20, 🔴 Lv.21~30+
        /// </summary>
        public static string GetLevelColorTag(int level)
        {
            if (level <= 10) return "🟢";    // 초급
            if (level <= 20) return "🟡";   // 중급
            return "🔴";                     // 고급
        }

        /// <summary>
        /// 레벨 표시 문자열
        /// </summary>
        public static string GetLevelDisplay(int level)
        {
            return $"{GetLevelColorTag(level)} Lv.{level}";
        }

        // ===== 드랍률 보정 =====

        /// <summary>
        /// 레벨 기반 희귀 드랍 확률 보정 (레벨 10당 +5%)
        /// </summary>
        public static float GetRareDropBonus(int level)
        {
            return (level / 10) * 0.05f; // 5% per 10 levels
        }

        /// <summary>
        /// 최종 드랍 확률 계산 (기본 확률 + 레벨 보정)
        /// </summary>
        public static float GetFinalDropChance(float baseChance, int level)
        {
            return Mathf.Clamp01(baseChance + GetRareDropBonus(level));
        }

        // ===== 몬스터별 티어 매핑 (이름 기반) =====

        /// <summary>
        /// 몬스터 이름으로 티어 추정
        /// </summary>
        public static MonsterTier EstimateTierByName(string monsterName)
        {
            // 초반 몬스터
            string[] beginner = { "토끼", "까마귀", "박쥐", "쥐", "설치류", "거미" };
            // 중반 몬스터
            string[] intermediate = { "늑대", "멧돼지", "사슴", "악어", "슬라임", "골렘", "도마뱀", "트롤", "오우거" };
            // 후반 몬스터
            string[] advanced = { "만티코어", "암살자", "미노타우로스", "드래곤", "히드라", "리치", "데몬" };

            foreach (var name in beginner)
                if (monsterName.Contains(name)) return MonsterTier.Beginner;
            foreach (var name in intermediate)
                if (monsterName.Contains(name)) return MonsterTier.Intermediate;
            foreach (var name in advanced)
                if (monsterName.Contains(name)) return MonsterTier.Advanced;

            return MonsterTier.Beginner; // default
        }
    }
}