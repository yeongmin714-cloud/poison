using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// [5.3.5] 몬스터 레벨 매니저 (싱글톤)
    ///
    /// MonsterLevelData를 로드하여 몬스터 레벨 계산, HP/데미지 산출,
    /// 드랍률 보정 등을 담당합니다.
    /// MonsterSpawner에서 스폰 시 호출하여 레벨 적용합니다.
    /// </summary>
    public class MonsterLevelManager : MonoBehaviour
    {
        private const string DataResourcePath = "Data/MonsterLevelData";

        private static MonsterLevelManager _instance;
        public static MonsterLevelManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<MonsterLevelManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject(nameof(MonsterLevelManager));
                        _instance = go.AddComponent<MonsterLevelManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        [Header("Monster Level Data")]
        [SerializeField] private MonsterLevelData _data;

        /// <summary>로드된 데이터</summary>
        public MonsterLevelData Data => _data;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            if (_data == null)
            {
                _data = Resources.Load<MonsterLevelData>(DataResourcePath);
                if (_data == null)
                {
                    Debug.LogWarning($"[MonsterLevelManager] MonsterLevelData를 찾을 수 없습니다 ({DataResourcePath}). 기본값으로 ScriptableObject를 생성합니다.");
                    _data = ScriptableObject.CreateInstance<MonsterLevelData>();
                }
            }
        }

        // ===== 레벨 계산 =====

        /// <summary>
        /// 몬스터 최종 레벨 계산: 티어 기본 레벨 + 영지 난이도 보정
        /// </summary>
        /// <param name="difficulty">영지 난이도 (Ring1~Empire)</param>
        /// <param name="tier">몬스터 티어 (Beginner/Intermediate/Advanced)</param>
        /// <returns>최종 레벨 (1~MaxLevel)</returns>
        public int GetMonsterLevel(TerritoryDifficulty difficulty, MonsterTier tier)
        {
            Vector2Int baseRange = _data.GetBaseLevelRange(tier);
            int baseLevel = Random.Range(baseRange.x, baseRange.y + 1);
            int bonus = _data.GetDifficultyBonus(difficulty);
            return Mathf.Clamp(baseLevel + bonus, 1, _data.MaxLevel);
        }

        // ===== 스탯 계산 =====

        /// <summary>
        /// 레벨과 티어 기반 최대 HP 계산
        /// </summary>
        /// <param name="level">최종 레벨</param>
        /// <param name="tier">몬스터 티어</param>
        /// <returns>최대 HP</returns>
        public float GetMonsterHP(int level, MonsterTier tier)
        {
            float hpPerLevel = _data.GetHPPerLevel(tier);
            return hpPerLevel * level;
        }

        /// <summary>
        /// 레벨 기반 추가 데미지 계산
        /// </summary>
        /// <param name="level">최종 레벨</param>
        /// <returns>추가 데미지</returns>
        public float GetMonsterDamage(int level)
        {
            return _data.BaseDamage + level * _data.DamagePerLevel;
        }

        /// <summary>
        /// 레벨 기반 희귀 드랍률 보정
        /// </summary>
        /// <param name="level">최종 레벨</param>
        /// <returns>추가 확률 (0.05 = 5%)</returns>
        public float GetDropRateBonus(int level)
        {
            return (level / 10) * _data.RareDropBonusPer10Levels;
        }

        /// <summary>
        /// 최종 드랍 확률 계산 (기본 확률 + 레벨 보정)
        /// </summary>
        public float GetFinalDropChance(float baseChance, int level)
        {
            return Mathf.Clamp01(baseChance + GetDropRateBonus(level));
        }

        // ===== 레벨 표시 =====

        /// <summary>
        /// 레벨에 따른 색상 태그 반환
        /// 🟢 Lv.1~10, 🟡 Lv.11~20, 🔴 Lv.21~30+
        /// </summary>
        public string GetLevelColorTag(int level)
        {
            if (level <= _data.GreenThreshold) return "🟢";   // 초급
            if (level <= _data.YellowThreshold) return "🟡";  // 중급
            return "🔴";                                       // 고급
        }

        /// <summary>
        /// 레벨 표시 문자열 (예: "🟢 Lv.5")
        /// </summary>
        public string GetLevelDisplay(int level)
        {
            return $"{GetLevelColorTag(level)} Lv.{level}";
        }

        /// <summary>
        /// 기본 인스턴스 생성 확인
        /// </summary>
        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}