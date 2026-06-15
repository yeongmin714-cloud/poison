using UnityEngine;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// C9-13: 병사 레벨 시스템 (Lv.1~50, 영지 난이도 기반)
    /// 
    /// 영지 난이도에 따라 병사 기본 레벨이 결정됩니다.
    /// 시간 경과 + 전투 경험으로 레벨업하며, 레벨당 스탯이 증가합니다.
    /// </summary>
    public static class GuardLevelSystem
    {
        // 레벨 범위 (영지 난이도 기반)
        public const int LV_MIN = 1;
        public const int LV_MAX = 50;

        // 레벨당 스탯 증가 (ROADMAP 5.3.2)
        public const float HP_PER_LEVEL = 10f;
        public const float DAMAGE_PER_LEVEL = 1f;
        public const float DEFENSE_PER_LEVEL = 0.5f;

        // 레벨업 필요 경험치
        public const float BASE_XP_REQUIRED = 100f;
        public const float XP_SCALE_FACTOR = 1.2f; // 레벨당 필요 XP 1.2배

        // 전투 경험치
        public const float XP_PER_COMBAT = 10f;
        public const float XP_PER_KILL = 50f;
        public const float XP_PER_DAY_AUTO = 5f;  // 자동 성장 (1일당)

        // 영지 난이도별 기본 레벨 범위
        public static Vector2Int GetLevelRange(TerritoryDifficulty difficulty)
        {
            switch (difficulty)
            {
                case TerritoryDifficulty.Ring1: return new Vector2Int(1, 10);
                case TerritoryDifficulty.Ring2: return new Vector2Int(11, 25);
                case TerritoryDifficulty.Ring3: return new Vector2Int(11, 25);
                case TerritoryDifficulty.Ring4: return new Vector2Int(26, 40);
                case TerritoryDifficulty.Empire: return new Vector2Int(41, 50);
                default: return new Vector2Int(1, 5);
            }
        }

        /// <summary>
        /// 영지 난이도와 랜덤성을 기반으로 초기 병사 레벨 생성
        /// </summary>
        public static int GenerateInitialLevel(TerritoryDifficulty difficulty)
        {
            var range = GetLevelRange(difficulty);
            return Random.Range(range.x, range.y + 1);
        }

        // ===== 스탯 계산 =====

        /// <summary>
        /// 레벨 기반 최대 HP 계산
        /// </summary>
        public static float CalculateMaxHP(int level)
        {
            return 10f + (level - 1) * HP_PER_LEVEL;
        }

        /// <summary>
        /// 레벨 기반 공격력 계산
        /// </summary>
        public static float CalculateDamage(int level)
        {
            return 2f + (level - 1) * DAMAGE_PER_LEVEL;
        }

        /// <summary>
        /// 레벨 기반 방어력 계산
        /// </summary>
        public static float CalculateDefense(int level)
        {
            return (level - 1) * DEFENSE_PER_LEVEL;
        }

        // ===== 경험치 & 레벨업 =====

        /// <summary>
        /// 특정 레벨에 도달하는 데 필요한 총 경험치 계산
        /// </summary>
        public static float GetXPRequiredForLevel(int level)
        {
            if (level <= 1) return 0;
            float total = 0;
            for (int i = 1; i < level; i++)
            {
                total += BASE_XP_REQUIRED * Mathf.Pow(XP_SCALE_FACTOR, i - 1);
            }
            return total;
        }

        /// <summary>
        /// 현재 경험치로 레벨 계산
        /// </summary>
        public static int CalculateLevelFromXP(float totalXP)
        {
            int level = 1;
            float remaining = totalXP;
            while (level < LV_MAX)
            {
                float needed = BASE_XP_REQUIRED * Mathf.Pow(XP_SCALE_FACTOR, level - 1);
                if (remaining >= needed)
                {
                    remaining -= needed;
                    level++;
                }
                else break;
            }
            return level;
        }

        /// <summary>
        /// 현재 레벨에서 다음 레벨까지 필요한 경험치
        /// </summary>
        public static float GetXPToNextLevel(int currentLevel)
        {
            if (currentLevel >= LV_MAX) return 0;
            return BASE_XP_REQUIRED * Mathf.Pow(XP_SCALE_FACTOR, currentLevel - 1);
        }

        /// <summary>
        /// 전투 경험치 획득
        /// </summary>
        public static void AddCombatXP(GuardPlaceholder guard, float xpAmount)
        {
            if (guard == null || !guard.IsAlive) return;
            int beforeLevel = guard.Level;

            // XP는 GuardPlaceholder에 xp 필드 추가 필요
            // 현재는 이 메서드가 호출될 때 로그만 출력
            // 실제 XP 저장은 GuardPlaceholder 확장 후 구현
            Debug.Log($"[GuardLevel] {guard.GuardName} 전투 경험치 +{xpAmount}");
        }

        /// <summary>
        /// 일일 자동 성장 경험치
        /// </summary>
        public static void AddDailyAutoXP(GuardPlaceholder guard)
        {
            if (guard == null || !guard.IsAlive) return;
            AddCombatXP(guard, XP_PER_DAY_AUTO);
        }

        /// <summary>
        /// 영지 난이도 기반 병사 초기화 (TerritoryBuilder용)
        /// </summary>
        public static void ApplyLevelFromTerritory(GuardPlaceholder guard, TerritoryDifficulty difficulty)
        {
            if (guard == null) return;
            int initialLevel = GenerateInitialLevel(difficulty);

            // 리플렉션으로 private level 필드 설정
            var levelField = typeof(GuardPlaceholder).GetField("level",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (levelField != null)
            {
                levelField.SetValue(guard, initialLevel);
            }

            Debug.Log($"[GuardLevel] {guard.GuardName} 초기 레벨 설정: Lv.{initialLevel} (난이도: {difficulty})");
        }

        /// <summary>
        /// 병사에게 레벨 기반 스탯 적용
        /// </summary>
        public static void ApplyStatsFromLevel(GuardPlaceholder guard)
        {
            if (guard == null) return;
            // GuardPlaceholder에 MaxHP가 있다면 여기서 설정
            // 현재는 GuardPlaceholder.MaxHP가 [SerializeField]라 직접 설정 불가
            Debug.Log($"[GuardLevel] {guard.GuardName} Lv.{guard.Level} 스탯: HP+{CalculateMaxHP(guard.Level)}, 공격+{CalculateDamage(guard.Level)}, 방어+{CalculateDefense(guard.Level)}");
        }
    }
}