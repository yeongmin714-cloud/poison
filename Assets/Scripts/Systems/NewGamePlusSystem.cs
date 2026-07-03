using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// 🔄 뉴게임+ 시스템 — 정적 클래스.
    /// 플레이어의 일부 진행을 유지한 채 게임을 재시작합니다.
    /// </summary>
    public static class NewGamePlusSystem
    {
        // ===== PlayerPrefs 키 =====
        private const string PREFS_NG_PLUS = "NewGamePlus";
        private const string PREFS_DIFFICULTY_BONUS = "NGP_DifficultyBonus";
        private const string PREFS_PRESERVED_LEVEL = "NGP_PreservedLevel";
        private const string PREFS_PRESERVED_EXP = "NGP_PreservedEXP";
        private const string PREFS_PRESERVED_GOLD = "NGP_PreservedGold";
        private const string PREFS_PRESERVED_ATTACK_BASE = "NGP_PreservedAttackBase";
        private const string PREFS_PRESERVED_DEFENSE_BASE = "NGP_PreservedDefenseBase";
        private const string PREFS_PRESERVED_MOVE_SPEED_BASE = "NGP_PreservedMoveSpeedBase";
        private const string PREFS_PRESERVED_CRIT_CHANCE = "NGP_PreservedCritChance";

        // ===== NG+ 보너스 상수 =====
        private const float NG_EXP_MULTIPLIER = 1.5f;
        private const int NG_DIFFICULTY_BONUS = 5;

        // ===== 프로퍼티 =====

        /// <summary>
        /// 현재 게임이 뉴게임+ 모드인지 확인합니다.
        /// </summary>
        public static bool IsNewGamePlus
        {
            get
            {
                try
                {
                    return PlayerPrefs.GetInt(PREFS_NG_PLUS, 0) == 1;
                }
                catch (System.Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// NG+ 난이도 보너스 (적 레벨 +N)를 반환합니다.
        /// </summary>
        public static int DifficultyBonus
        {
            get
            {
                try
                {
                    return PlayerPrefs.GetInt(PREFS_DIFFICULTY_BONUS, 0);
                }
                catch (System.Exception)
                {
                    return 0;
                }
            }
        }

        // ===== 공개 메서드 =====

        /// <summary>
        /// NG+ 보너스 배율을 반환합니다.
        /// </summary>
        /// <returns>(expMultiplier, goldMultiplier, difficultyBonus) 튜플</returns>
        public static (float expMultiplier, float goldMultiplier, int difficultyBonus) GetNGPlusBonus()
        {
            if (!IsNewGamePlus)
                return (1f, 1f, 0);

            int bonusTier = PlayerPrefs.GetInt(PREFS_DIFFICULTY_BONUS, NG_DIFFICULTY_BONUS);
            float expMult = NG_EXP_MULTIPLIER;
            float goldMult = 1.2f; // 골드 20% 추가 보너스

            return (expMult, goldMult, bonusTier);
        }

        /// <summary>
        /// 뉴게임+를 시작합니다.
        /// 현재 데이터를 백업하고, 유지할 데이터를 보존한 후 게임을 재시작합니다.
        /// </summary>
        public static void StartNewGamePlus()
        {
            Debug.Log("[NewGamePlusSystem] 🔄 뉴게임+ 시작!");

            try
            {
                // 1. 현재 데이터 백업 (PlayerPrefs에 저장)
                BackupCurrentData();

                // 2. 게임 상태 리셋
                ResetGameState();

                // 3. NG+ 플래그 설정
                PlayerPrefs.SetInt(PREFS_NG_PLUS, 1);

                // 다음 NG+는 난이도 보너스 중첩 (최대 +20)
                int currentBonus = PlayerPrefs.GetInt(PREFS_DIFFICULTY_BONUS, 0);
                int nextBonus = Mathf.Min(currentBonus + NG_DIFFICULTY_BONUS, 20);
                PlayerPrefs.SetInt(PREFS_DIFFICULTY_BONUS, nextBonus);

                PlayerPrefs.Save();

                Debug.Log($"[NewGamePlusSystem] NG+ 설정 완료. 난이도 보너스: +{nextBonus}");

                // 4. MainScene 로드
                LoadMainScene();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[NewGamePlusSystem] 뉴게임+ 시작 중 오류: {ex.Message}\n{ex.StackTrace}");
            }
        }

        // ===== 데이터 백업 =====

        private static void BackupCurrentData()
        {
            Debug.Log("[NewGamePlusSystem] 현재 데이터 백업 중...");

            // PlayerStats 데이터 백업
            if (PlayerStats.Instance != null)
            {
                PlayerPrefs.SetInt(PREFS_PRESERVED_LEVEL, PlayerStats.Instance.Level);
                PlayerPrefs.SetInt(PREFS_PRESERVED_EXP, PlayerStats.Instance.CurrentEXP);
                PlayerPrefs.SetInt(PREFS_PRESERVED_GOLD, PlayerStats.Instance.Gold);
                PlayerPrefs.SetFloat(PREFS_PRESERVED_ATTACK_BASE, PlayerStats.Instance.AttackDamageBase);
                PlayerPrefs.SetFloat(PREFS_PRESERVED_DEFENSE_BASE, PlayerStats.Instance.DefenseBase);
                PlayerPrefs.SetFloat(PREFS_PRESERVED_MOVE_SPEED_BASE, PlayerStats.Instance.MoveSpeedBase);
                PlayerPrefs.SetFloat(PREFS_PRESERVED_CRIT_CHANCE, PlayerStats.Instance.CritChanceBase);

                Debug.Log($"[NewGamePlusSystem] PlayerStats 백업: Lv.{PlayerStats.Instance.Level}, Gold {PlayerStats.Instance.Gold}");
            }
            else
            {
                Debug.LogWarning("[NewGamePlusSystem] PlayerStats.Instance가 null입니다. 기본값으로 백업합니다.");
                PlayerPrefs.SetInt(PREFS_PRESERVED_LEVEL, 1);
                PlayerPrefs.SetInt(PREFS_PRESERVED_EXP, 0);
                PlayerPrefs.SetInt(PREFS_PRESERVED_GOLD, 0);
                PlayerPrefs.SetFloat(PREFS_PRESERVED_ATTACK_BASE, 10f);
                PlayerPrefs.SetFloat(PREFS_PRESERVED_DEFENSE_BASE, 0f);
                PlayerPrefs.SetFloat(PREFS_PRESERVED_MOVE_SPEED_BASE, 5f);
                PlayerPrefs.SetFloat(PREFS_PRESERVED_CRIT_CHANCE, 0f);
            }

            // RecipeDiscoverySystem 데이터는 이미 PlayerPrefs에 저장되어 있음
            // AchievementSystem 데이터도 이미 PlayerPrefs에 저장되어 있음
            // GameStatsCollector 데이터도 이미 PlayerPrefs에 저장되어 있음
            // -> 리셋 후에도 자동으로 유지됨 (PlayerPrefs DeleteAll 하지 않음)

            PlayerPrefs.Save();
        }

        // ===== 게임 상태 리셋 =====

        private static void ResetGameState()
        {
            Debug.Log("[NewGamePlusSystem] 게임 상태 리셋 중...");

            // 1. 영토 소유권 리셋 (모든 영지를 LordOwned로)
            ResetTerritoryOwnership();

            // 2. 퀘스트 진행 리셋
            ResetQuestProgress();

            // 3. 인벤토리 아이템 리셋 (골드 제외)
            ResetInventory();

            // 4. 가드 명단 리셋
            ResetGuardRoster();

            Debug.Log("[NewGamePlusSystem] 게임 상태 리셋 완료");
        }

        private static void ResetTerritoryOwnership()
        {
            var db = TerritoryDatabase.Instance;
            if (db == null)
            {
                Debug.LogWarning("[NewGamePlusSystem] TerritoryDatabase.Instance가 null입니다. 영토 리셋 불가.");
                return;
            }

            var definitions = db.GetAllDefinitions();
            if (definitions == null) return;

            int resetCount = 0;
            foreach (var def in definitions)
            {
                if (def.id.nation == NationType.None) continue;

                var state = db.GetState(def.id);
                if (state == null) continue;

                // PlayerOwned → LordOwned (또는 Unoccupied)로 리셋
                if (state.ownership == TerritoryOwnership.PlayerOwned ||
                    state.ownership == TerritoryOwnership.Contested)
                {
                    // 초기 소유권: 대부분 LordOwned, 일부는 Unoccupied
                    // 간단히 LordOwned로 통일 (게임 시작 상태와 유사하게)
                    if (def.id.nation == NationType.Dracula)
                        state.ownership = TerritoryOwnership.Unoccupied;
                    else
                        state.ownership = TerritoryOwnership.LordOwned;

                    // 기타 상태 리셋
                    state.loyaltyToPlayer = 0f;
                    state.flagRaised = false;
                    state.lordSurrendered = false;
                    state.lordDefeated = false;
                    state.lordExecuted = false;
                    state.lordSpared = false;
                    state.guardAliveRatio = 1f;
                    state.battleState = TerritoryBattleState.Peaceful;

                    resetCount++;
                }
            }

            Debug.Log($"[NewGamePlusSystem] 영토 소유권 리셋: {resetCount}개 영토 초기화");
        }

        private static void ResetQuestProgress()
        {
            // QuestManager.ResetAll() — 모든 퀘스트 상태를 초기 상태로 리셋
            QuestManager.ResetAll();
            Debug.Log("[NewGamePlusSystem] 퀘스트 진행 리셋 완료");
        }

        private static void ResetInventory()
        {
            var inventory = PlayerInventory.Instance;
            if (inventory == null)
            {
                Debug.LogWarning("[NewGamePlusSystem] PlayerInventory.Instance가 null입니다.");
                return;
            }

            // 인벤토리 초기화 (골드는 제외)
            var slots = inventory.GetAllSlots();
            if (slots == null) return;

            int removedCount = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].item != null)
                {
                    // 골드 아이템은 유지
                    if (slots[i].item.id == "gold") continue;

                    slots[i] = null;
                    removedCount++;
                }
            }

            Debug.Log($"[NewGamePlusSystem] 인벤토리 리셋: {removedCount}개 아이템 제거 (골드 제외)");
        }

        private static void ResetGuardRoster()
        {
            try
            {
                var guardManagerType = System.Type.GetType("ProjectName.Systems.GuardManager");
                if (guardManagerType == null)
                    guardManagerType = FindTypeInAssemblies("GuardManager");

                if (guardManagerType != null)
                {
                    var propInfo = guardManagerType.GetProperty("Instance",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    object instance = null;
                    if (propInfo != null)
                        instance = propInfo.GetValue(null);
                    else
                    {
                        var fieldInfo = guardManagerType.GetField("Instance",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        if (fieldInfo != null)
                            instance = fieldInfo.GetValue(null);
                    }
                    if (instance != null)
                    {
                        var resetMethod = guardManagerType.GetMethod("ResetAllGuards",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (resetMethod != null)
                        {
                            resetMethod.Invoke(instance, null);
                            Debug.Log("[NewGamePlusSystem] 가드 명단 리셋 완료");
                        }
                        else
                        {
                            Debug.Log("[NewGamePlusSystem] GuardManager.ResetAllGuards() 메서드 없음 — 스킵");
                        }
                    }
                }
                else
                {
                    Debug.Log("[NewGamePlusSystem] GuardManager 타입 없음 — 스킵");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[NewGamePlusSystem] 가드 명단 리셋 중 오류: {ex.Message}");
            }
        }

        // ===== 씬 로드 =====

        private static void LoadMainScene()
        {
            var loadingManager = LoadingManager.Instance;
            if (loadingManager != null)
            {
                loadingManager.LoadSceneAsync("MainScene", 0.5f, 0.5f);
            }
            else
            {
                Debug.LogWarning("[NewGamePlusSystem] LoadingManager.Instance가 null입니다. SceneManager로 직접 로드합니다.");
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainScene");
            }
        }

        // ===== 유틸리티 =====

        private static System.Type FindTypeInAssemblies(string typeName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = asm.GetType(typeName);
                if (type != null) return type;
                type = asm.GetType("ProjectName.Systems." + typeName);
                if (type != null) return type;
                type = asm.GetType("ProjectName.Core." + typeName);
                if (type != null) return type;
            }
            return null;
        }
    }
}
