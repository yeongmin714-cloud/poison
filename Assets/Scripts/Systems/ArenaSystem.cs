using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// ⚔️ 아레나/투기장 시스템 — MonoBehaviour 싱글톤.
    /// 각 영지에 아레나 존재, 참가비/전투 시뮬레이션/보상/랭킹 담당.
    /// </summary>
    public class ArenaSystem : MonoBehaviour
    {
        public static ArenaSystem Instance { get; private set; }

        [Header("아레나 설정")]
        [SerializeField] private float _interactRange = 3f;       // E키 감지 거리
        [SerializeField] private int _minPlayerLevel = 3;         // 참가 최소 레벨

        // 연승 기록 (PlayerPrefs)
        private const string PREFS_WINSTREAK = "Arena_WinStreak";
        private const string PREFS_BESTSTREAK = "Arena_BestStreak";
        private const string PREFS_TOTALWINS = "Arena_TotalWins";

        // 현재 연승
        private int _currentWinStreak = 0;
        private int _bestWinStreak = 0;
        private int _totalWins = 0;

        // 전투 상태
        private bool _isInBattle = false;
        private ArenaBattleState _battleState = ArenaBattleState.None;

        // 선택된 병사/용병 참조
        private object _selectedFighter = null; // GuardPlaceholder or MercenaryInstance
        private bool _isMercenaryMode = false;

        // ===== 이벤트 =====
        public event System.Action<ArenaCombatantData, ArenaCombatantData> OnArenaMatchStart;
        public event System.Action<ArenaResult> OnArenaMatchEnd;

        // ===== 공개 읽기 전용 =====
        public int CurrentWinStreak => _currentWinStreak;
        public int BestWinStreak => _bestWinStreak;
        public int TotalWins => _totalWins;
        public bool IsInBattle => _isInBattle;
        public ArenaBattleState BattleState => _battleState;
        public object SelectedFighter => _selectedFighter;
        public bool IsMercenaryMode => _isMercenaryMode;

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
            LoadRecords();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            // E키로 아레나 NPC 감지 (NPC 스스로 처리하도록 위임)
            // ArenaNPCPlaceholder가 직접 처리
        }

        // ================================================================
        // 참가비 계산 (Ring 기반)
        // ================================================================

        /// <summary>Ring 기반 참가비 (10~100 골드)</summary>
        public int GetEntryFee(TerritoryDifficulty difficulty)
        {
            switch (difficulty)
            {
                case TerritoryDifficulty.Ring1: return 10;
                case TerritoryDifficulty.Ring2: return 25;
                case TerritoryDifficulty.Ring3: return 50;
                case TerritoryDifficulty.Ring4: return 75;
                case TerritoryDifficulty.Empire: return 100;
                default: return 10;
            }
        }

        /// <summary>현재 영지의 참가비</summary>
        public int GetCurrentEntryFee()
        {
            if (TerritoryManager.Instance == null) return 10;
            var def = TerritoryDatabase.Instance?.GetDefinition(TerritoryManager.Instance.CurrentTerritoryId);
            if (def == null || def.Value.id.nation == NationType.None) return 10;
            return GetEntryFee(def.Value.difficulty);
        }

        /// <summary>현재 영지의 난이도</summary>
        public TerritoryDifficulty GetCurrentDifficulty()
        {
            if (TerritoryManager.Instance == null) return TerritoryDifficulty.Ring1;
            var def = TerritoryDatabase.Instance?.GetDefinition(TerritoryManager.Instance.CurrentTerritoryId);
            if (def == null || def.Value.id.nation == NationType.None) return TerritoryDifficulty.Ring1;
            return def.Value.difficulty;
        }

        // ================================================================
        // NPC 상대 생성
        // ================================================================

        /// <summary>영지 난이도 기반 NPC 상대 생성</summary>
        public ArenaCombatantData GenerateOpponent(TerritoryDifficulty difficulty)
        {
            int levelMin, levelMax;
            switch (difficulty)
            {
                case TerritoryDifficulty.Ring1: levelMin = 3;  levelMax = 8;  break;
                case TerritoryDifficulty.Ring2: levelMin = 10; levelMax = 18; break;
                case TerritoryDifficulty.Ring3: levelMin = 20; levelMax = 28; break;
                case TerritoryDifficulty.Ring4: levelMin = 30; levelMax = 40; break;
                case TerritoryDifficulty.Empire: levelMin = 35; levelMax = 50; break;
                default: levelMin = 3; levelMax = 8; break;
            }

            int level = Random.Range(levelMin, levelMax + 1);
            string[] npcNames = {
                "투사 가로드", "검투사 베린", "야수 케인", "무사 히데오",
                "격투가 마크", "아마조네스 리나", "권사 장팔", "투기장의 제왕"
            };
            string name = npcNames[Random.Range(0, npcNames.Length)];

            float hp = 20f + level * 8f + Random.Range(0f, 15f);
            float attack = 3f + level * 2f + Random.Range(0f, 4f);
            float defense = 1f + level * 0.8f + Random.Range(0f, 2f);

            return new ArenaCombatantData
            {
                name = name,
                level = level,
                maxHP = hp,
                currentHP = hp,
                attack = attack,
                defense = defense,
                isPlayer = false,
                sourceType = ArenaFighterType.NPC
            };
        }

        // ================================================================
        // 참가 조건 확인
        // ================================================================

        /// <summary>참가 가능 여부 확인</summary>
        public string CanParticipate()
        {
            if (PlayerStats.Instance == null)
                return "플레이어 스탯을 불러올 수 없습니다.";

            if (PlayerStats.Instance.Level < _minPlayerLevel)
                return $"참가하려면 Lv.{_minPlayerLevel} 이상이 필요합니다. (현재 Lv.{PlayerStats.Instance.Level})";

            int fee = GetCurrentEntryFee();
            if (PlayerStats.Instance.Gold < fee)
                return $"참가비가 부족합니다. ({fee}G 필요, 현재 {PlayerStats.Instance.Gold}G)";

            if (_isInBattle)
                return "이미 전투 중입니다.";

            return null; // 참가 가능
        }

        // ================================================================
        // 전투 시작 (플레이어 모드)
        // ================================================================

        /// <summary>직접 싸우기 모드로 전투 시작</summary>
        public IEnumerator StartPlayerFight()
        {
            string check = CanParticipate();
            if (check != null)
            {
                Debug.LogWarning($"[ArenaSystem] 참가 불가: {check}");
                yield break;
            }

            // 참가비 지불
            int fee = GetCurrentEntryFee();
            if (PlayerStats.Instance != null)
                PlayerStats.Instance.SpendGold(fee);

            _isInBattle = true;
            _battleState = ArenaBattleState.InProgress;

            // 플레이어 데이터
            float playerHP = PlayerHealth.Instance != null ? PlayerHealth.Instance.CurrentHP : 100f;
            float playerMaxHP = PlayerHealth.Instance != null ? PlayerHealth.Instance.MaxHP : 100f;
            float playerAttack = PlayerStats.Instance != null ? PlayerStats.Instance.AttackDamageBase : 10f;
            float playerDefense = PlayerStats.Instance != null ? PlayerStats.Instance.DefenseBase : 0f;

            ArenaCombatantData playerData = new ArenaCombatantData
            {
                name = "플레이어",
                level = PlayerStats.Instance?.Level ?? 1,
                maxHP = playerMaxHP,
                currentHP = playerHP,
                attack = playerAttack,
                defense = playerDefense,
                isPlayer = true,
                sourceType = ArenaFighterType.Player
            };

            TerritoryDifficulty diff = GetCurrentDifficulty();
            ArenaCombatantData opponentData = GenerateOpponent(diff);

            // 이벤트 발생
            OnArenaMatchStart?.Invoke(playerData, opponentData);

            // 전투 시뮬레이션 실행
            yield return StartCoroutine(SimulateBattle(playerData, opponentData, fee));
        }

        // ================================================================
        // 전투 시작 (병사/용병 출전 모드)
        // ================================================================

        /// <summary>병사 출전 모드로 전투 시작</summary>
        public IEnumerator StartGuardFight(GuardPlaceholder guard)
        {
            if (guard == null)
            {
                Debug.LogError("[ArenaSystem] guard가 null입니다.");
                yield break;
            }

            string check = CanParticipate();
            if (check != null)
            {
                Debug.LogWarning($"[ArenaSystem] 참가 불가: {check}");
                yield break;
            }

            // 참가비 지불
            int fee = GetCurrentEntryFee();
            if (PlayerStats.Instance != null)
                PlayerStats.Instance.SpendGold(fee);

            _isInBattle = true;
            _battleState = ArenaBattleState.InProgress;
            _selectedFighter = guard;
            _isMercenaryMode = false;

            // 병사 데이터
            float hp = guard.HP;
            float maxHP = guard.MaxHP;
            float attack = 5f + guard.Level * 2f;
            float defense = 1f + guard.Level * 0.5f;

            ArenaCombatantData fighterData = new ArenaCombatantData
            {
                name = guard.GuardName,
                level = guard.Level,
                maxHP = maxHP,
                currentHP = hp,
                attack = attack,
                defense = defense,
                isPlayer = false,
                sourceType = ArenaFighterType.Guard
            };

            TerritoryDifficulty diff = GetCurrentDifficulty();
            ArenaCombatantData opponentData = GenerateOpponent(diff);

            OnArenaMatchStart?.Invoke(fighterData, opponentData);

            yield return StartCoroutine(SimulateBattle(fighterData, opponentData, fee));
        }

        /// <summary>용병 출전 모드로 전투 시작</summary>
        public IEnumerator StartMercenaryFight(MercenaryInstance merc)
        {
            if (!merc.isAlive)
            {
                Debug.LogWarning("[ArenaSystem] 용병이 사망했습니다.");
                yield break;
            }

            string check = CanParticipate();
            if (check != null)
            {
                Debug.LogWarning($"[ArenaSystem] 참가 불가: {check}");
                yield break;
            }

            int fee = GetCurrentEntryFee();
            if (PlayerStats.Instance != null)
                PlayerStats.Instance.SpendGold(fee);

            _isInBattle = true;
            _battleState = ArenaBattleState.InProgress;
            _selectedFighter = merc;
            _isMercenaryMode = true;

            float bonus = merc.AffinityBonus; // 호감도 보너스
            float hp = merc.currentHP;
            float maxHP = merc.data.maxHP;
            float attack = merc.data.attack * (1f + bonus);
            float defense = merc.data.defense * (1f + bonus);

            ArenaCombatantData fighterData = new ArenaCombatantData
            {
                name = merc.data.mercenaryName,
                level = (int)(merc.data.maxHP / 10f), // 추정 레벨
                maxHP = maxHP,
                currentHP = hp,
                attack = attack,
                defense = defense,
                isPlayer = false,
                sourceType = ArenaFighterType.Mercenary
            };

            TerritoryDifficulty diff = GetCurrentDifficulty();
            ArenaCombatantData opponentData = GenerateOpponent(diff);

            OnArenaMatchStart?.Invoke(fighterData, opponentData);

            yield return StartCoroutine(SimulateBattle(fighterData, opponentData, fee));
        }

        // ================================================================
        // 전투 시뮬레이션 (코루틴)
        // ================================================================

        private ArenaBattleLog _currentLog;

        private IEnumerator SimulateBattle(ArenaCombatantData fighter, ArenaCombatantData opponent, int fee)
        {
            _currentLog = new ArenaBattleLog
            {
                fighterName = fighter.name,
                opponentName = opponent.name,
                rounds = new List<ArenaRoundLog>(),
                totalRounds = 0,
                isVictory = false,
                rewardGold = 0,
                bonusMultiplier = 1f
            };

            // 최대 5라운드
            int maxRounds = 5;

            for (int round = 1; round <= maxRounds; round++)
            {
                // 라운드 로그
                ArenaRoundLog roundLog = new ArenaRoundLog
                {
                    roundNumber = round,
                    fighterHPBefore = fighter.currentHP,
                    opponentHPBefore = opponent.currentHP,
                    fighterAttackAmount = 0f,
                    opponentAttackAmount = 0f,
                    fighterDamageDealt = 0f,
                    opponentDamageDealt = 0f,
                    isFighterDead = false,
                    isOpponentDead = false
                };

                // --- 파이터 공격 ---
                float fighterRawDamage = Mathf.Max(1f, fighter.attack - opponent.defense * 0.5f);
                float fighterVariance = Random.Range(0.8f, 1.2f);
                float fighterDamage = fighterRawDamage * fighterVariance;
                roundLog.fighterAttackAmount = fighter.attack;
                roundLog.fighterDamageDealt = fighterDamage;
                opponent.currentHP -= fighterDamage;

                // --- 상대 공격 ---
                float opponentRawDamage = Mathf.Max(1f, opponent.attack - fighter.defense * 0.5f);
                float opponentVariance = Random.Range(0.8f, 1.2f);
                float opponentDamage = opponentRawDamage * opponentVariance;
                roundLog.opponentAttackAmount = opponent.attack;
                roundLog.opponentDamageDealt = opponentDamage;
                fighter.currentHP -= opponentDamage;

                // 사망 체크
                if (opponent.currentHP <= 0)
                {
                    roundLog.isOpponentDead = true;
                    opponent.currentHP = 0;
                }
                if (fighter.currentHP <= 0)
                {
                    roundLog.isFighterDead = true;
                    fighter.currentHP = 0;
                }

                _currentLog.rounds.Add(roundLog);

                if (roundLog.isOpponentDead || roundLog.isFighterDead)
                    break;

                // 3초 대기
                yield return new WaitForSeconds(3f);
            }

            // 승패 판정
            bool fighterWon = fighter.currentHP > 0 && opponent.currentHP <= 0;
            if (fighter.currentHP > 0 && opponent.currentHP > 0)
            {
                // 5라운드 종료 시 HP 높은 쪽 승리
                fighterWon = fighter.currentHP >= opponent.currentHP;
            }

            _currentLog.totalRounds = _currentLog.rounds.Count;
            _currentLog.isVictory = fighterWon;

            // 보상 계산
            int reward = CalculateReward(fee, fighterWon);
            _currentLog.rewardGold = reward;

            if (fighterWon)
            {
                _currentWinStreak++;
                _totalWins++;
                if (_currentWinStreak > _bestWinStreak)
                {
                    _bestWinStreak = _currentWinStreak;
                    SaveRecords();
                }

                // 골드 보상 지급
                if (PlayerStats.Instance != null)
                    PlayerStats.Instance.AddGold(reward);

                // 연승 보너스 배율 표시
                _currentLog.bonusMultiplier = GetWinStreakMultiplier();

                // 전설 보상 (10연승)
                if (_currentWinStreak >= 10)
                {
                    _currentLog.legendaryReward = true;
                    GiveLegendaryReward();
                }

                // 플레이어 모드: 체력 반영
                if (fighter.isPlayer && PlayerHealth.Instance != null)
                {
                    // HP 동기화 (최소 1)
                    float finalHP = Mathf.Max(1f, fighter.currentHP);
                    // PlayerHealth에 직접 반영 (SetHP 없으므로 TakeDamage 차감 방식으로)
                    float hpDiff = (PlayerHealth.Instance.MaxHP - finalHP);
                    if (hpDiff > 0)
                    {
                        // 데미지 방식으로 HP 조정 (체력 소모 반영)
                        PlayerHealth.Instance.TakeDamage(hpDiff, Vector3.zero, "arena");
                    }
                }
                // 병사 모드: 체력 반영
                else if (_selectedFighter is GuardPlaceholder guard && !_isMercenaryMode)
                {
                    guard.SetHP(Mathf.Max(1f, fighter.currentHP));
                }
                // 용병 모드: 체력 반영
                else if (_selectedFighter is MercenaryInstance mercInst && _isMercenaryMode)
                {
                    mercInst.currentHP = Mathf.Max(1f, fighter.currentHP);
                }
            }
            else
            {
                // 패배 시 연승 초기화
                _currentWinStreak = 0;
                SaveRecords();

                // 플레이어 사망 처리
                if (fighter.isPlayer && PlayerHealth.Instance != null)
                {
                    PlayerHealth.Instance.TakeDamage(9999f, Vector3.zero, "arena");
                }
                // 병사 사망 처리
                else if (_selectedFighter is GuardPlaceholder guardF && !_isMercenaryMode)
                {
                    guardF.SetHP(0f);
                }
                // 용병 사망 처리
                else if (_selectedFighter is MercenaryInstance mercInst2 && _isMercenaryMode)
                {
                    mercInst2.currentHP = 0f;
                    mercInst2.isAlive = false;
                }
            }

            // 결과 이벤트
            ArenaResult result = new ArenaResult
            {
                isVictory = fighterWon,
                rewardGold = reward,
                winStreak = _currentWinStreak,
                bonusMultiplier = _currentLog.bonusMultiplier,
                legendaryReward = _currentWinStreak >= 10,
                totalRounds = _currentLog.totalRounds
            };

            OnArenaMatchEnd?.Invoke(result);

            _battleState = fighterWon ? ArenaBattleState.Victory : ArenaBattleState.Defeat;

            // 결과 표시를 위한 짧은 대기
            yield return new WaitForSeconds(3f);

            // 상태 초기화
            _isInBattle = false;
            _battleState = ArenaBattleState.None;
            _selectedFighter = null;
            _isMercenaryMode = false;
            _currentLog = null;
        }

        // ================================================================
        // 보상 계산
        // ================================================================

        /// <summary>참가비 × 2 ~ × 5 골드</summary>
        private int CalculateReward(int fee, bool victory)
        {
            if (!victory) return 0;
            int baseReward = fee * Random.Range(2, 6); // 2~5배
            float multiplier = GetWinStreakMultiplier();
            return Mathf.RoundToInt(baseReward * multiplier);
        }

        /// <summary>연승 보너스 배율</summary>
        public float GetWinStreakMultiplier()
        {
            if (_currentWinStreak >= 5) return 3f;
            if (_currentWinStreak >= 3) return 2f;
            if (_currentWinStreak >= 2) return 1.5f;
            return 1f;
        }

        /// <summary>전설 보상: arena_champion_token 지급</summary>
        private void GiveLegendaryReward()
        {
            if (PlayerInventory.Instance == null) return;

            var token = new PlayerInventory.ItemData
            {
                id = "arena_champion_token",
                displayName = "🏆 아레나 챔피언 토큰",
                description = "10연승을 달성한 전설적인 투사의 증표.",
                category = PlayerInventory.ItemCategory.Material,
                maxStack = 1
            };

            PlayerInventory.Instance.AddItem(token, 1);
            Debug.Log("[ArenaSystem] 🏆 전설 보상 지급: 아레나 챔피언 토큰!");
        }

        // ================================================================
        // 랭킹 기록 저장/로드
        // ================================================================

        private void SaveRecords()
        {
            PlayerPrefs.SetInt(PREFS_WINSTREAK, _currentWinStreak);
            PlayerPrefs.SetInt(PREFS_BESTSTREAK, _bestWinStreak);
            PlayerPrefs.SetInt(PREFS_TOTALWINS, _totalWins);
            PlayerPrefs.Save();
        }

        private void LoadRecords()
        {
            _currentWinStreak = PlayerPrefs.GetInt(PREFS_WINSTREAK, 0);
            _bestWinStreak = PlayerPrefs.GetInt(PREFS_BESTSTREAK, 0);
            _totalWins = PlayerPrefs.GetInt(PREFS_TOTALWINS, 0);
        }

        /// <summary>현재 전투 로그 (UI용)</summary>
        public ArenaBattleLog GetCurrentBattleLog()
        {
            return _currentLog;
        }

        // ================================================================
        // 아레나 NPC 배치 확인
        // ================================================================

        /// <summary>모든 영지에 아레나 존재 여부 확인 (facility 플래그 활용)</summary>
        public static bool HasArenaInTerritory(TerritoryId territoryId)
        {
            // TerritoryDatabase의 definition에서 facility 확인
            var db = TerritoryDatabase.Instance;
            if (db == null) return true; // 기본적으로 모든 영지에 할당

            var def = db.GetDefinition(territoryId);
            if (def.id.nation == NationType.None) return true;

            // TODO: facility 플래그 확인 (현재 TerritoryDefinition에 facility 필드 없음)
            // 일단 모든 영지에 아레나 존재
            return true;
        }

        // ===== 테스트 헬퍼 =====

        public void SetWinStreakForTest(int streak)
        {
            _currentWinStreak = streak;
        }

        public void ResetRecordsForTest()
        {
            _currentWinStreak = 0;
            _bestWinStreak = 0;
            _totalWins = 0;
            SaveRecords();
        }
    }

    // ================================================================
    // 데이터 구조체
    // ================================================================

    /// <summary>아레나 전투원 데이터</summary>
    [System.Serializable]
    public struct ArenaCombatantData
    {
        public string name;
        public int level;
        public float maxHP;
        public float currentHP;
        public float attack;
        public float defense;
        public bool isPlayer;
        public ArenaFighterType sourceType;
    }

    /// <summary>전투원 유형</summary>
    public enum ArenaFighterType
    {
        Player,
        Guard,
        Mercenary,
        NPC
    }

    /// <summary>라운드별 로그</summary>
    [System.Serializable]
    public class ArenaRoundLog
    {
        public int roundNumber;
        public float fighterHPBefore;
        public float opponentHPBefore;
        public float fighterAttackAmount;
        public float opponentAttackAmount;
        public float fighterDamageDealt;
        public float opponentDamageDealt;
        public bool isFighterDead;
        public bool isOpponentDead;
    }

    /// <summary>전투 전체 로그</summary>
    [System.Serializable]
    public class ArenaBattleLog
    {
        public string fighterName;
        public string opponentName;
        public List<ArenaRoundLog> rounds;
        public int totalRounds;
        public bool isVictory;
        public int rewardGold;
        public float bonusMultiplier;
        public bool legendaryReward;
    }

    /// <summary>전투 결과 (이벤트용)</summary>
    [System.Serializable]
    public class ArenaResult
    {
        public bool isVictory;
        public int rewardGold;
        public int winStreak;
        public float bonusMultiplier;
        public bool legendaryReward;
        public int totalRounds;
    }

    /// <summary>아레나 전투 상태</summary>
    public enum ArenaBattleState
    {
        None,
        InProgress,
        Victory,
        Defeat
    }

    /// <summary>
    /// 아레나 NPC — E키 상호작용으로 ArenaMenuUI 오픈.
    /// 각 영지에 자동 배치됨.
    /// </summary>
    public class ArenaNPCPlaceholder : MonoBehaviour
    {
        [Header("아레나 NPC")]
        [SerializeField] private string _npcName = "아레나 관리자";
        [SerializeField] private float _interactRange = 3f;

        private GameObject _playerCache;
        private bool _playerNearby = false;
        private bool _uiOpen = false;

        private void Start()
        {
            _playerCache = GameObject.FindGameObjectWithTag("Player");
        }

        private void Update()
        {
            if (_playerCache == null || !_playerCache.activeInHierarchy)
                _playerCache = GameObject.FindGameObjectWithTag("Player");
            if (_playerCache == null) return;

            float dist = Vector3.Distance(transform.position, _playerCache.transform.position);
            _playerNearby = dist <= _interactRange;

            // E키로 메뉴 열기
            if (_playerNearby && UnityEngine.InputSystem.Keyboard.current != null
                && UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
            {
                if (!_uiOpen)
                {
                    OpenArenaMenu();
                }
                else
                {
                    CloseArenaMenu();
                }
            }

            // ESC로 닫기
            if (_uiOpen && UnityEngine.InputSystem.Keyboard.current != null
                && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CloseArenaMenu();
            }

            // 너무 멀어지면 자동 닫기
            if (_uiOpen && dist > _interactRange * 1.5f)
            {
                CloseArenaMenu();
            }
        }

        private void OpenArenaMenu()
        {
            _uiOpen = true;
            UI.ArenaMenuUI.Instance?.Show();
        }

        private void CloseArenaMenu()
        {
            _uiOpen = false;
            UI.ArenaMenuUI.Instance?.Hide();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _interactRange);
        }
    }
}