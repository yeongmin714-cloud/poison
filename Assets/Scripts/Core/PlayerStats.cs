using UnityEngine;
using System;

namespace ProjectName.Core
{
    /// <summary>
    /// 플레이어 스테이터스 및 경험치 관리 싱글톤
    /// - 경험치 획득, 레벨업 로직
    /// - 레벨당 스탯 증가 적용 (HP, Alchemy, Cooking, Speech, Combat)
    /// - PlayerHealth와 연동하여 최대 HP 업데이트
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PlayerStats : MonoBehaviour
    {
        public static PlayerStats Instance { get; private set; }

        /// <summary>
        /// [RuntimeInitializeOnLoadMethod] 폴백: 씬에 PlayerStats가 없으면 자동 생성.
        /// GameManager.InitializeSystems()보다 먼저 실행되어 Awake() 타이밍 문제를 방지합니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreateFallback()
        {
            if (Instance != null) return;

            var existing = UnityEngine.Object.FindAnyObjectByType<PlayerStats>();
            if (existing != null)
            {
                Instance = existing;
                return;
            }

            var go = new GameObject("PlayerStats");
            go.AddComponent<PlayerStats>();
            UnityEngine.Object.DontDestroyOnLoad(go);
            Debug.Log("[PlayerStats] Auto-created via RuntimeInitializeOnLoadMethod fallback.");
        }

        [Header("Experience Settings")]
        [SerializeField] private int _currentEXP = 0;
        [SerializeField] private int _level = 1;

        [Header("Money Settings")]
        [SerializeField] private int _gold = 0;

        // 경험치 테이블 (레벨별 누적 경험치)
        // Level 1: 0, Level 2: 100, Level 3: 350, ..., Level 10: 13850
        private readonly int[] _levelUpThresholds = new int[]
        {
            0,   // Level 1
            100, // Level 2
            350, // Level 3
            850, // Level 4
            1650, // Level 5
            2850, // Level 6
            4550, // Level 7
            6850, // Level 8
            9850, // Level 9
            13850 // Level 10
        };

        // 최대 레벨 (설계 문서에 따라)
        public const int MaxLevel = 50;

        // 레벨 변경 이벤트 (UI 등에서 구독)
        public event Action<int, int> OnLevelChanged; // (newLevel, oldLevel)

        // 경험치 관련 속성
        public int CurrentEXP => _currentEXP;
        public int Level => _level;

        public int Gold => _gold;

        // Base stats (can be modified by buffs, equipment, etc.)
        [Header("Base Stats")]
        [SerializeField] public float _attackDamageBase = 10f;   // 기본 공격력
        [SerializeField] public float _defenseBase = 0f;         // 기본 방어력 (뎀지 감소량)
        [SerializeField] public float _moveSpeedBase = 5f;       // 기본 이동 속도
        [SerializeField] public float _alchemyTempBonus = 0f;    // 임시 연금술 보너스 (버프 등)
        [SerializeField] public float _cookingTempBonus = 0f;    // 임시 요리 보너스 (버프 등)
        [SerializeField] public float _critChanceBase = 0f;      // 기본 치명타 확률

        // 스탯 계산 속성 (레벨에 따라 동적으로 계산)
        public int HPBase => 100 + (_level - 1) * 5; // Lv1=100, Lv50=345
        public float AlchemySuccessBonus => _level * 0.02f; // +2% per level
        public float CookingSuccessBonus => _level * 0.02f; // +2% per level
        public int SpeechAffinityBonus => _level; // +1 affinity modifier per level
        public float CombatDamageBonus => _level * 0.01f; // +1% damage per level

        /// <summary>
        /// 연금술 성공률 반환 (0~1.0)
        /// </summary>
        public float GetAlchemySuccessRate() => FinalAlchemyBonus;

        /// <summary>
        /// 요리 성공률 반환 (0~1.0)
        /// </summary>
        public float GetCookingSuccessRate() => FinalCookingBonus;

        // Final stats that include base + level bonuses + buffs
        public float FinalAttackDamage => _attackDamageBase + (_level * 0.5f); // 예: 레벨당 +0.5 공격력
        public float FinalDefense => _defenseBase + (_level * 0.2f);           // 예: 레벨당 +0.2 방어력
        public float FinalMoveSpeed => _moveSpeedBase + (_level * 0.1f);       // 예: 레벨당 +0.1 속도
        public float FinalAlchemyBonus => AlchemySuccessBonus + _alchemyTempBonus;
        public float FinalCookingBonus => CookingSuccessBonus + _cookingTempBonus;
        public float FinalCritChance => _critChanceBase + (_level * 0.005f); // 예: 레벨당 +0.5% 치명타

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // 초기 레벨에 맞춰 PlayerHealth의 최대 HP 설정
            UpdatePlayerHealthMaxHP();
        }

        /// <summary>
        /// 경험치 추가 및 레벨업 처리
        /// </summary>
        /// <param name="amount">추가할 경험치량</param>
        public void AddEXP(int amount)
        {
            if (amount <= 0 || _level >= MaxLevel) return;

            _currentEXP += amount;
            int originalLevel = _level;

            // 레벨업 루프 (여러 레벨 상승 가능)
            while (_level < MaxLevel && _currentEXP >= GetExpForLevel(_level + 1))
            {
                _level++;
            }

            if (_level > originalLevel)
            {
                // 레벨 상승 처리
                OnLevelChanged?.Invoke(_level, originalLevel);
                ApplyLevelUpStats();
                UpdatePlayerHealthMaxHP();
                Debug.Log($"🎉 레벨업! Lv.{_level} 달성!");
            }
        }

        /// <summary>
        /// AddEXP의 별칭 — 편의 메서드
        /// </summary>
        public void AddExp(int amount) => AddEXP(amount);

        /// <summary>
        /// 주어진 레벨에 도달하기 위해 필요한 누적 경험치를 반환합니다.
        /// </summary>
        /// <param name="level">목표 레벨 (1 이상)</param>
        /// <returns>누적 경험치</returns>
        public int GetExpForLevel(int level)
        {
            if (level <= 1) return 0;
            if (level <= 10)
            {
                return _levelUpThresholds[level - 1]; // 배열은 0-indexed (레벨 1은 인덱스 0)
            }

            // 레벨 10 이후 계산
            int exp = _levelUpThresholds[9]; // 레벨 10의 누적 경험치 (13850)
            for (int i = 10; i < level; i++)
            {
                // 레벨 i에서 i+1로 가기 위한 증가 경험치: 4000 + (i - 9) * 1000
                exp += 4000 + (i - 9) * 1000;
            }
            return exp;
        }

        /// <summary>
        /// 레벨 상승 시 스탯 증가 적용 (여기서는 주로 알림 역할을 하며, 실제 스탯은 계산 속성으로 제공)
        /// 필요 시 다른 시스템에 알릴 수 있음.
        /// </summary>
        private void ApplyLevelUpStats()
        {
            // 이 메서드는 레벨 상승 시 호출되며,
            // 실제 스탯은 속성으로 계산되므로 여기서는 추가 로직이 필요 없음.
            // 다만, 레벨 상승 효과를 위한 이벤트나 알림이 필요하면 여기에 추가.
            Debug.Log($"[PlayerStats] 레벨 상승! 현재 레벨: {_level}");
        }

        /// <summary>
        /// PlayerHealth의 최대 HP를 현재 레벨에 맞춰 업데이트합니다.
        /// </summary>
        private void UpdatePlayerHealthMaxHP()
        {
            if (PlayerHealth.Instance != null)
            {
                float newMaxHP = HPBase;
                PlayerHealth.Instance.SetMaxHP(newMaxHP);
                // 현재 HP는 이전 최대 HP 내에서였으므로, 새로운 최대 HP가 증가했을 때는 자동으로 범위 내에 있음.
                // 따라서 별도 조정이 필요하지 않음.
            }
        }


        /// <summary>
        /// 골드 추가
        /// </summary>
        public void AddGold(int amount)
        {
            if (amount < 0)
            {
                SpendGold(-amount); // Use SpendGold for negative amounts
                return;
            }
            _gold += amount;
            Debug.Log($"[PlayerStats] Gold increased by {amount}. Total: {_gold}");
        }

        /// <summary>
        /// 골드 사용. 충분하지 않으면 false 반환.
        /// </summary>
        public bool SpendGold(int amount)
        {
            if (amount < 0)
            {
                Debug.LogWarning("[PlayerStats] SpendGold called with negative amount. Use AddGold instead.");
                return false;
            }
            if (_gold >= amount)
            {
                _gold -= amount;
                Debug.Log($"[PlayerStats] Gold spent: {amount}. Remaining: {_gold}");
                return true;
            }
            else
            {
                Debug.LogWarning($"[PlayerStats] Not enough gold! Need {amount}, have {_gold}");
                return false;
            }
        }

        /// <summary>
        /// C14-07: 복수명부 보상 — 모든 독살 공모자 발견 시 영구 능력치 상승
        /// attackDamageBase +5, _alchemyTempBonus +0.10, SpeechAffinityBonus +10레벨 상당, Gold +1000
        /// </summary>
        public void ApplyRevengeListReward()
        {
            _attackDamageBase += 5f;
            _alchemyTempBonus += 0.10f;

            // SpeechAffinityBonus 상당 효과 (+10 레벨 효과를 위해 경험치 추가)
            // SpeechAffinityBonus = _level 이므로, 레벨 10 상승 효과
            int targetLevel = Mathf.Min(_level + 10, MaxLevel);
            int neededExp = GetExpForLevel(targetLevel) - _currentEXP;
            if (neededExp > 0)
            {
                AddEXP(neededExp);
            }

            _gold += 1000;

            Debug.Log($"[PlayerStats] 🎉 복수명부 보상 적용! ATK+5, Alchemy+10%, Speech+10레벨, Gold+1000");
        }

        /// <summary>
        /// 디버그용: 경험치와 레벨을 직접 설정 (테스트 목적)
        /// </summary>
        [ContextMenu("Set EXP to 0")]
        private void DebugResetEXP()
        {
            _currentEXP = 0;
            _level = 1;
            UpdatePlayerHealthMaxHP();
        }
    }
}