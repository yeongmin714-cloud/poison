using UnityEngine;
using ProjectName.Core.Data;
using System.Collections.Generic;
using System.Reflection;

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

        // ===== 캐시된 Reflection FieldInfo (성능 최적화) =====
        private static FieldInfo _levelField;
        private static FieldInfo _maxHPField;

        /// <summary>
        /// GuardPlaceholder.level 필드 캐시 (읽기 전용 Level 프로퍼티 우회)
        /// </summary>
        private static FieldInfo LevelField
        {
            get
            {
                if (_levelField == null)
                {
                    _levelField = typeof(GuardPlaceholder).GetField("level",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                }
                return _levelField;
            }
        }

        /// <summary>
        /// GuardPlaceholder._maxHP 필드 캐시 (읽기 전용 MaxHP 프로퍼티 우회)
        /// </summary>
        private static FieldInfo MaxHPField
        {
            get
            {
                if (_maxHPField == null)
                {
                    _maxHPField = typeof(GuardPlaceholder).GetField("_maxHP",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                }
                return _maxHPField;
            }
        }

        // ===== 경험치 저장소 (GuardPlaceholder에 xp 필드가 없으므로 정적 사전 사용) =====
        private static readonly Dictionary<GuardPlaceholder, float> _guardXP = new Dictionary<GuardPlaceholder, float>();

        /// <summary>
        /// GuardPlaceholder가 파괴될 때 XP 사전에서 정리하기 위해 구독
        /// </summary>
        static GuardLevelSystem()
        {
            GuardPlaceholder.OnAnyGuardDied += OnGuardDied;
        }

        private static void OnGuardDied(GuardPlaceholder guard)
        {
            if (guard != null && _guardXP.ContainsKey(guard))
            {
                _guardXP.Remove(guard);
            }
        }

        // ===== XP 공개 API =====

        /// <summary>
        /// 해당 병사의 누적 경험치 반환
        /// </summary>
        public static float GetGuardXP(GuardPlaceholder guard)
        {
            if (guard == null) return 0f;
            return _guardXP.TryGetValue(guard, out float xp) ? xp : 0f;
        }

        /// <summary>
        /// 병사의 누적 경험치를 수동 설정 (GuardManager 로드 등)
        /// </summary>
        public static void SetGuardXP(GuardPlaceholder guard, float xp)
        {
            if (guard == null) return;
            _guardXP[guard] = Mathf.Max(0f, xp);
        }

        // ===== 영지 난이도별 기본 레벨 범위 =====

        /// <summary>
        /// 영지 난이도에 따른 병사 기본 레벨 범위 반환
        /// </summary>
        public static Vector2Int GetLevelRange(TerritoryDifficulty difficulty)
        {
            switch (difficulty)
            {
                case TerritoryDifficulty.Ring1: return new Vector2Int(1, 10);
                case TerritoryDifficulty.Ring2: return new Vector2Int(11, 20);
                case TerritoryDifficulty.Ring3: return new Vector2Int(16, 25);
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
        /// 전투 경험치 획득 — XP가 충분하면 자동 레벨업 및 스탯 갱신
        /// </summary>
        public static void AddCombatXP(GuardPlaceholder guard, float xpAmount)
        {
            if (guard == null || !guard.IsAlive) return;

            // XP 누적
            if (!_guardXP.ContainsKey(guard))
            {
                _guardXP[guard] = 0f;
            }
            _guardXP[guard] += xpAmount;

            // 레벨업 체크
            int newLevel = CalculateLevelFromXP(_guardXP[guard]);
            if (newLevel > guard.Level)
            {
                SetGuardLevel(guard, newLevel);
                ApplyStatsFromLevel(guard);
                Debug.Log($"[GuardLevel] {guard.GuardName} 레벨업! Lv.{newLevel}");
            }

            Debug.Log($"[GuardLevel] {guard.GuardName} 전투 경험치 +{xpAmount} (총:{_guardXP[guard]})");
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

            SetGuardLevel(guard, initialLevel);
            ApplyStatsFromLevel(guard);

            Debug.Log($"[GuardLevel] {guard.GuardName} 초기 레벨 설정: Lv.{initialLevel} (난이도: {difficulty})");
        }

        /// <summary>
        /// 리플렉션 없이 병사 레벨을 안전하게 설정 (캐시된 FieldInfo 사용)
        /// </summary>
        private static void SetGuardLevel(GuardPlaceholder guard, int level)
        {
            if (guard == null) return;
            int clampedLevel = Mathf.Clamp(level, LV_MIN, LV_MAX);

            var field = LevelField;
            if (field != null)
            {
                field.SetValue(guard, clampedLevel);
            }
            else
            {
                Debug.LogError($"[GuardLevel] GuardPlaceholder.level 필드를 찾을 수 없습니다! " +
                    $"클래스 구조가 변경되었을 수 있습니다.");
            }
        }

        /// <summary>
        /// 병사에게 레벨 기반 스탯 적용 (MaxHP, 현재 체력 등)
        /// </summary>
        public static void ApplyStatsFromLevel(GuardPlaceholder guard)
        {
            if (guard == null) return;

            float newMaxHP = CalculateMaxHP(guard.Level);
            float newDamage = CalculateDamage(guard.Level);
            float newDefense = CalculateDefense(guard.Level);

            // _maxHP 설정 (읽기 전용 MaxHP 프로퍼티 우회)
            var field = MaxHPField;
            if (field != null)
            {
                float oldMaxHP = guard.MaxHP;
                field.SetValue(guard, newMaxHP);

                // 최대HP가 증가했다면 현재 체력도 비율 유지
                if (newMaxHP > oldMaxHP && guard.HP > 0f)
                {
                    float ratio = guard.HP / oldMaxHP;
                    guard.SetHP(newMaxHP * ratio);
                }
            }
            else
            {
                Debug.LogWarning($"[GuardLevel] GuardPlaceholder._maxHP 필드를 찾을 수 없어 스탯을 적용할 수 없습니다.");
            }

            Debug.Log($"[GuardLevel] {guard.GuardName} Lv.{guard.Level} 스탯 적용: " +
                $"HP {guard.MaxHP}(+{newMaxHP}), 공격 +{newDamage}, 방어 +{newDefense}");
        }
    }
}