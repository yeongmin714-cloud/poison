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

        /// <summary>
        /// _data가 null이면 경고 로그를 출력하고 false 반환
        /// </summary>
        private bool TryGetData(out MonsterLevelData data)
        {
            if (_data == null)
            {
                Debug.LogError("[MonsterLevelManager] MonsterLevelData가 로드되지 않았습니다. Awake()가 호출되었는지 확인하세요.");
                data = null;
                return false;
            }
            data = _data;
            return true;
        }

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
            if (!TryGetData(out var data))
                return 1;

            Vector2Int baseRange = data.GetBaseLevelRange(tier);
            int baseLevel = Random.Range(baseRange.x, baseRange.y + 1);
            int bonus = data.GetDifficultyBonus(difficulty);
            return Mathf.Clamp(baseLevel + bonus, 1, data.MaxLevel);
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
            if (!TryGetData(out var data))
                return 0f;

            float hpPerLevel = data.GetHPPerLevel(tier);
            return hpPerLevel * level;
        }

        /// <summary>
        /// 레벨 기반 추가 데미지 계산
        /// </summary>
        /// <param name="level">최종 레벨</param>
        /// <returns>추가 데미지</returns>
        public float GetMonsterDamage(int level)
        {
            if (!TryGetData(out var data))
                return 0f;

            return data.BaseDamage + level * data.DamagePerLevel;
        }

        /// <summary>
        /// 레벨 기반 희귀 드랍률 보정 (10레벨 단위 계단식)
        /// 정수 나눗셈: Lv.1~9 → 0, Lv.10~19 → 1, Lv.20~29 → 2 ...
        /// </summary>
        /// <param name="level">최종 레벨</param>
        /// <returns>추가 확률 (0.05 = 5%)</returns>
        public float GetDropRateBonus(int level)
        {
            if (!TryGetData(out var data))
                return 0f;

            return (level / 10) * data.RareDropBonusPer10Levels;
        }

        /// <summary>
        /// 레벨 기반 경험치 보상 계산
        /// </summary>
        public float GetMonsterXP(int level)
        {
            return 5f + level * 2f;
        }

        /// <summary>
        /// 최종 드랍 확률 계산 (기본 확률 + 레벨 보정)
        /// </summary>
        public float GetFinalDropChance(float baseChance, int level)
        {
            if (!TryGetData(out var _))
                return Mathf.Clamp01(baseChance);

            return Mathf.Clamp01(baseChance + GetDropRateBonus(level));
        }

        // ===== 레벨 표시 =====

        /// <summary>
        /// 레벨에 따른 색상 태그 반환
        /// 🟢 Lv.1~10, 🟡 Lv.11~20, 🔴 Lv.21~30+
        /// </summary>
        public string GetLevelColorTag(int level)
        {
            if (!TryGetData(out var data))
            {
                if (level <= 10) return "🟢";
                if (level <= 20) return "🟡";
                return "🔴";
            }

            if (level <= data.GreenThreshold) return "🟢";   // 초급
            if (level <= data.YellowThreshold) return "🟡";  // 중급
            return "🔴";                                       // 고급
        }

        /// <summary>
        /// 레벨 표시 문자열 (예: "🟢 Lv.5")
        /// </summary>
        public string GetLevelDisplay(int level)
        {
            return $"{GetLevelColorTag(level)} Lv.{level}";
        }

        // ===== 몬스터별 티어 매핑 (이름 기반) =====

        /// <summary>
        /// 몬스터 이름으로 티어 추정
        /// </summary>
        public MonsterTier EstimateTierByName(string monsterName)
        {
            string[] beginner = { "토끼", "까마귀", "박쥐", "쥐", "설치류", "거미" };
            string[] intermediate = { "늑대", "멧돼지", "사슴", "악어", "슬라임", "골렘", "도마뱀", "트롤", "오우거" };
            string[] advanced = { "만티코어", "암살자", "미노타우로스", "드래곤", "히드라", "리치", "데몬" };

            foreach (var name in beginner)
                if (monsterName.Contains(name)) return MonsterTier.Beginner;
            foreach (var name in intermediate)
                if (monsterName.Contains(name)) return MonsterTier.Intermediate;
            foreach (var name in advanced)
                if (monsterName.Contains(name)) return MonsterTier.Advanced;

            return MonsterTier.Beginner;
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