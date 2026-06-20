using UnityEngine;

namespace ProjectName.Core.Data
{
    /// <summary>
    /// [5.3.5] 몬스터 레벨 시스템 설정 데이터 (ScriptableObject)
    /// 
    /// 몬스터 티어별 기본 레벨 범위, 영지 난이도 보정치,
    /// 레벨당 HP/데미지 증가 공식을 정의합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "MonsterLevelData", menuName = "ProjectName/Monster Level Data")]
    public class MonsterLevelData : ScriptableObject
    {
        [Header("=== 티어별 기본 레벨 범위 ===")]

        [SerializeField]
        [Tooltip("초반 몬스터 기본 레벨 범위")]
        private Vector2Int _basicLevelRange = new Vector2Int(1, 5);

        [SerializeField]
        [Tooltip("중반 몬스터 기본 레벨 범위")]
        private Vector2Int _midLevelRange = new Vector2Int(6, 15);

        [SerializeField]
        [Tooltip("후반 몬스터 기본 레벨 범위")]
        private Vector2Int _highLevelRange = new Vector2Int(16, 30);

        [Header("=== 영지 난이도 보정 (Ring별 추가 레벨) ===")]

        [SerializeField]
        [Tooltip("Ring1 (최외각): 보정 없음")]
        private int _ring1Bonus = 0;

        [SerializeField]
        [Tooltip("Ring2: +2")]
        private int _ring2Bonus = 2;

        [SerializeField]
        [Tooltip("Ring3: +5")]
        private int _ring3Bonus = 5;

        [SerializeField]
        [Tooltip("Ring4: +8")]
        private int _ring4Bonus = 8;

        [SerializeField]
        [Tooltip("Empire (황제국): +15")]
        private int _empireBonus = 15;

        [Header("=== 레벨당 스탯 증가 ===")]

        [SerializeField]
        [Tooltip("초반 티어: 레벨당 HP")]
        private float _beginnerHPPerLevel = 5f;

        [SerializeField]
        [Tooltip("중반 티어: 레벨당 HP")]
        private float _intermediateHPPerLevel = 10f;

        [SerializeField]
        [Tooltip("후반 티어: 레벨당 HP")]
        private float _advancedHPPerLevel = 20f;

        [SerializeField]
        [Tooltip("레벨당 데미지 증가 계수")]
        private float _damagePerLevel = 1.5f;

        [SerializeField]
        [Tooltip("기본 데미지")]
        private float _baseDamage = 1f;

        [Header("=== 드랍률 보정 ===")]

        [SerializeField]
        [Tooltip("레벨 10당 추가 희귀 드랍 확률 (0.05 = 5%)")]
        private float _rareDropBonusPer10Levels = 0.05f;

        [Header("=== 레벨 표시 색상 임계값 ===")]

        [SerializeField]
        [Tooltip("🟢 초록: 이 레벨 이하")]
        private int _greenThreshold = 10;

        [SerializeField]
        [Tooltip("🟡 노랑: 이 레벨 이하")]
        private int _yellowThreshold = 20;

        [Header("=== 최대 레벨 ===")]

        [SerializeField]
        [Tooltip("최대 허용 레벨")]
        private int _maxLevel = 50;

        // ===== Public 접근자 =====

        public Vector2Int BasicLevelRange => _basicLevelRange;
        public Vector2Int MidLevelRange => _midLevelRange;
        public Vector2Int HighLevelRange => _highLevelRange;

        public int Ring1Bonus => _ring1Bonus;
        public int Ring2Bonus => _ring2Bonus;
        public int Ring3Bonus => _ring3Bonus;
        public int Ring4Bonus => _ring4Bonus;
        public int EmpireBonus => _empireBonus;

        public float BeginnerHPPerLevel => _beginnerHPPerLevel;
        public float IntermediateHPPerLevel => _intermediateHPPerLevel;
        public float AdvancedHPPerLevel => _advancedHPPerLevel;
        public float DamagePerLevel => _damagePerLevel;
        public float BaseDamage => _baseDamage;

        public float RareDropBonusPer10Levels => _rareDropBonusPer10Levels;
        public int GreenThreshold => _greenThreshold;
        public int YellowThreshold => _yellowThreshold;
        public int MaxLevel => _maxLevel;

        // ===== 조회 메서드 =====

        /// <summary>
        /// 티어별 기본 레벨 범위 반환
        /// </summary>
        public Vector2Int GetBaseLevelRange(MonsterTier tier)
        {
            switch (tier)
            {
                case MonsterTier.Beginner:       return _basicLevelRange;
                case MonsterTier.Intermediate:   return _midLevelRange;
                case MonsterTier.Advanced:       return _highLevelRange;
                default: return _basicLevelRange;
            }
        }

        /// <summary>
        /// 영지 난이도별 보정치 반환
        /// </summary>
        public int GetDifficultyBonus(TerritoryDifficulty difficulty)
        {
            switch (difficulty)
            {
                case TerritoryDifficulty.Ring1:  return _ring1Bonus;
                case TerritoryDifficulty.Ring2:  return _ring2Bonus;
                case TerritoryDifficulty.Ring3:  return _ring3Bonus;
                case TerritoryDifficulty.Ring4:  return _ring4Bonus;
                case TerritoryDifficulty.Empire: return _empireBonus;
                default: return 0;
            }
        }

        /// <summary>
        /// 티어별 레벨당 HP 증가량 반환
        /// </summary>
        public float GetHPPerLevel(MonsterTier tier)
        {
            switch (tier)
            {
                case MonsterTier.Beginner:       return _beginnerHPPerLevel;
                case MonsterTier.Intermediate:   return _intermediateHPPerLevel;
                case MonsterTier.Advanced:       return _advancedHPPerLevel;
                default: return _beginnerHPPerLevel;
            }
        }
    }
}