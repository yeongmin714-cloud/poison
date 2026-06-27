using ProjectName.Core;
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
        [Tooltip("초반(Beginner) 몬스터 기본 레벨 범위")]
        private Vector2Int _beginnerLevelRange = new Vector2Int(1, 5);

        [SerializeField]
        [Tooltip("중반(Intermediate) 몬스터 기본 레벨 범위")]
        private Vector2Int _intermediateLevelRange = new Vector2Int(6, 15);

        [SerializeField]
        [Tooltip("후반(Advanced) 몬스터 기본 레벨 범위")]
        private Vector2Int _advancedLevelRange = new Vector2Int(16, 30);

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
        [Tooltip("초반(Beginner) 티어: 레벨당 HP")]
        private float _beginnerHPPerLevel = 5f;

        [SerializeField]
        [Tooltip("중반(Intermediate) 티어: 레벨당 HP")]
        private float _intermediateHPPerLevel = 10f;

        [SerializeField]
        [Tooltip("후반(Advanced) 티어: 레벨당 HP")]
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

        /// <summary>초반(Beginner) 티어 몬스터의 기본 레벨 범위</summary>
        public Vector2Int BeginnerLevelRange => _beginnerLevelRange;

        /// <summary>중반(Intermediate) 티어 몬스터의 기본 레벨 범위</summary>
        public Vector2Int IntermediateLevelRange => _intermediateLevelRange;

        /// <summary>후반(Advanced) 티어 몬스터의 기본 레벨 범위</summary>
        public Vector2Int AdvancedLevelRange => _advancedLevelRange;

        /// <summary>Ring1 (최외각) 영지 레벨 보정치</summary>
        public int Ring1Bonus => _ring1Bonus;

        /// <summary>Ring2 영지 레벨 보정치</summary>
        public int Ring2Bonus => _ring2Bonus;

        /// <summary>Ring3 영지 레벨 보정치</summary>
        public int Ring3Bonus => _ring3Bonus;

        /// <summary>Ring4 영지 레벨 보정치</summary>
        public int Ring4Bonus => _ring4Bonus;

        /// <summary>Empire (황제국) 영지 레벨 보정치</summary>
        public int EmpireBonus => _empireBonus;

        /// <summary>초반(Beginner) 티어 레벨당 HP 증가량</summary>
        public float BeginnerHPPerLevel => _beginnerHPPerLevel;

        /// <summary>중반(Intermediate) 티어 레벨당 HP 증가량</summary>
        public float IntermediateHPPerLevel => _intermediateHPPerLevel;

        /// <summary>후반(Advanced) 티어 레벨당 HP 증가량</summary>
        public float AdvancedHPPerLevel => _advancedHPPerLevel;

        /// <summary>레벨당 데미지 증가 계수</summary>
        public float DamagePerLevel => _damagePerLevel;

        /// <summary>기본 데미지 (레벨 0 기준)</summary>
        public float BaseDamage => _baseDamage;

        /// <summary>레벨 10당 추가 희귀 드랍 확률 (0.05 = 5%)</summary>
        public float RareDropBonusPer10Levels => _rareDropBonusPer10Levels;

        /// <summary>🟢 초록색 레벨 표시 임계값 (이하)</summary>
        public int GreenThreshold => _greenThreshold;

        /// <summary>🟡 노랑색 레벨 표시 임계값 (이하)</summary>
        public int YellowThreshold => _yellowThreshold;

        /// <summary>최대 허용 레벨</summary>
        public int MaxLevel => _maxLevel;

        // ===== 조회 메서드 =====

        /// <summary>
        /// 티어별 기본 레벨 범위 반환
        /// </summary>
        public Vector2Int GetBaseLevelRange(MonsterTier tier)
        {
            switch (tier)
            {
                case MonsterTier.Beginner:       return _beginnerLevelRange;
                case MonsterTier.Intermediate:   return _intermediateLevelRange;
                case MonsterTier.Advanced:       return _advancedLevelRange;
                default: return _beginnerLevelRange;
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

        // ===== 에디터 데이터 무결성 검증 =====

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 티어별 레벨 범위: x <= y 보장
            _beginnerLevelRange = new Vector2Int(
                Mathf.Max(1, _beginnerLevelRange.x),
                Mathf.Max(_beginnerLevelRange.x, _beginnerLevelRange.y));

            _intermediateLevelRange = new Vector2Int(
                Mathf.Max(_beginnerLevelRange.y + 1, _intermediateLevelRange.x),
                Mathf.Max(_intermediateLevelRange.x, _intermediateLevelRange.y));

            _advancedLevelRange = new Vector2Int(
                Mathf.Max(_intermediateLevelRange.y + 1, _advancedLevelRange.x),
                Mathf.Max(_advancedLevelRange.x, _advancedLevelRange.y));

            // 영지 보정: 음수 방지
            _ring1Bonus = Mathf.Max(0, _ring1Bonus);
            _ring2Bonus = Mathf.Max(0, _ring2Bonus);
            _ring3Bonus = Mathf.Max(0, _ring3Bonus);
            _ring4Bonus = Mathf.Max(0, _ring4Bonus);
            _empireBonus = Mathf.Max(0, _empireBonus);

            // 스탯: 양수 보장
            _beginnerHPPerLevel = Mathf.Max(0f, _beginnerHPPerLevel);
            _intermediateHPPerLevel = Mathf.Max(0f, _intermediateHPPerLevel);
            _advancedHPPerLevel = Mathf.Max(0f, _advancedHPPerLevel);
            _damagePerLevel = Mathf.Max(0f, _damagePerLevel);
            _baseDamage = Mathf.Max(0f, _baseDamage);

            // 드랍률: 0~1 범위
            _rareDropBonusPer10Levels = Mathf.Clamp01(_rareDropBonusPer10Levels);

            // 레벨 표시 임계값: Green < Yellow <= Max
            _greenThreshold = Mathf.Clamp(_greenThreshold, 1, _maxLevel - 1);
            _yellowThreshold = Mathf.Clamp(_yellowThreshold, _greenThreshold + 1, _maxLevel);
            _maxLevel = Mathf.Max(1, _maxLevel);
        }
#endif
    }
}