using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// C16-04: 저장/로드 코어 싱글톤 매니저.
    /// JsonUtility를 사용하여 JSON 직렬화/역직렬화를 수행합니다.
    /// 슬롯 개수는 _slotCount로 설정 가능 (기본값 5).
    /// 각 슬롯 파일은 Application.persistentDataPath/saves/slot_{n}.json에 저장됩니다.
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        [Header("Save Settings")]
        [Range(1, 20)]
        [SerializeField] private int _slotCount = 5;
        [SerializeField] private string _saveDirectoryName = "saves";
        [SerializeField] private string _saveFilePrefix = "slot_";
        [SerializeField] private string _saveFileExtension = ".json";

        [Header("Debug")]
        [SerializeField] private bool _verbose;

        // ===== 이벤트 =====
        /// <summary>
        /// 저장 완료 시 발생. 인자는 슬롯 인덱스 (0-based).
        /// </summary>
        public event Action<int> OnSaveCompleted;

        /// <summary>
        /// 로드 완료 시 발생. 인자는 슬롯 인덱스 (0-based).
        /// </summary>
        public event Action<int> OnLoadCompleted;

        // ===== 싱글톤 =====

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (_verbose) Debug.LogWarning($"[SaveManager] 중복 인스턴스 파괴: {gameObject.name}");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // _slotCount가 0 이하이면 기본값 3으로 설정
            if (_slotCount <= 0)
            {
                _slotCount = 3;
                Debug.LogWarning("[SaveManager] _slotCount가 0 이하입니다. 기본값 3으로 설정합니다.");
            }

            EnsureSaveDirectory();
            if (_verbose) Debug.Log($"[SaveManager] 초기화 완료. 저장 경로: {GetSaveDirectory()}, 슬롯 수: {_slotCount}");
        }

        // ===== 디렉토리 관리 =====

        private string GetSaveDirectory()
        {
            return Path.Combine(Application.persistentDataPath, _saveDirectoryName);
        }

        private void EnsureSaveDirectory()
        {
            try
            {
                string dir = GetSaveDirectory();
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                    if (_verbose) Debug.Log($"[SaveManager] 저장 디렉토리 생성: {dir}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] 저장 디렉토리 생성/확인 중 오류: {ex.Message}");
            }
        }

        private string GetSlotFilePath(int slotIndex)
        {
            return Path.Combine(GetSaveDirectory(), $"{_saveFilePrefix}{slotIndex}{_saveFileExtension}");
        }

        // ===== 저장 (Serialize) =====

        /// <summary>
        /// 현재 게임 상태를 JSON으로 직렬화하여 지정된 슬롯에 저장합니다.
        /// </summary>
        /// <param name="slotIndex">슬롯 인덱스 (0~4)</param>
        public void Save(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slotCount)
            {
                Debug.LogError($"[SaveManager] 잘못된 슬롯 인덱스: {slotIndex}. 유효 범위: 0~{_slotCount - 1}");
                return;
            }

            try
            {
                SaveData data = new SaveData
                {
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    difficulty = (DifficultyMode)GameManager.CurrentDifficulty, // C20-01
                    player = CollectPlayerData(),
                    inventory = CollectInventoryData(),
                    time = CollectTimeData(),
                    territories = CollectTerritoryData(),
                    quests = CollectQuestData(),
                    revengeList = CollectRevengeListData(),
                    nationReputations = CollectNationReputationData(),
                    isNewGamePlus = NewGamePlusSystem.IsNewGamePlus,
                    constructions = CollectConstructionData(),
                    // [D5-A] 마을 음식점/마약 상태 수출 — 재고 있는 마을만 저장(용량 절감, VillageDrugState 규약)
                    villageDrugs = VillageDrugState.ExportSaveEntries(),
                    // v4: 경제/용병 영속 상태 (static 시스템은 항상 non-null 전송 → 로드 시 null = 레거시 세이브)
                    soldierWage = SoldierWageSystem.ExportSaveData(),
                    aiEconomy = AITerritoryEconomySystem.ExportSaveData(),
                    mercenaries = CollectMercenaryData(),
                    // v4: 고용 시장 AI 고용 카운트 (static 시스템 → 항상 non-null 전송. 방출 풀은 저장하지 않음)
                    laborMarket = LaborMarketSystem.ExportSaveData(),
                    // [F5] 살려준 영주 공물 멱등 상태
                    sparedLords = SparedLordTributeSystem.ExportSaveData(),
                    // [무번호 86-P1] 말 상태(품종/길들임/위치) — 구세이브 호환은 DTO 폴백(빈 리스트)
                    horses = HorseMountSystem.ExportSaveData(),
                };

                string json = JsonUtility.ToJson(data, prettyPrint: true);
                string filePath = GetSlotFilePath(slotIndex);

                EnsureSaveDirectory();
                File.WriteAllText(filePath, json);

                if (_verbose) Debug.Log($"[SaveManager] 슬롯 {slotIndex} 저장 완료: {filePath}");

                OnSaveCompleted?.Invoke(slotIndex);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] 슬롯 {slotIndex} 저장 중 오류 발생: {ex.Message}\n{ex.StackTrace}");
            }
        }

        // ===== 로드 (Deserialize) =====

        /// <summary>
        /// 지정된 슬롯의 JSON 파일을 읽어 게임 상태를 복원합니다.
        /// </summary>
        /// <param name="slotIndex">슬롯 인덱스 (0~4)</param>
        public void Load(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slotCount)
            {
                Debug.LogError($"[SaveManager] 잘못된 슬롯 인덱스: {slotIndex}. 유효 범위: 0~{_slotCount - 1}");
                return;
            }

            try
            {
                string filePath = GetSlotFilePath(slotIndex);

                if (!File.Exists(filePath))
                {
                    Debug.LogWarning($"[SaveManager] 슬롯 {slotIndex}에 저장된 데이터가 없습니다: {filePath}");
                    return;
                }

                string json = File.ReadAllText(filePath);
                SaveData data = JsonUtility.FromJson<SaveData>(json);

                if (data == null)
                {
                    Debug.LogError($"[SaveManager] 슬롯 {slotIndex} JSON 역직렬화 실패");
                    return;
                }

                // saveVersion 검증 및 마이그레이션
                if (data.saveVersion < 1 || data.saveVersion > 6)
                {
                    Debug.LogWarning($"[SaveManager] 슬롯 {slotIndex}의 saveVersion({data.saveVersion})이 현재 버전(6)과 다릅니다. 호환을 시도합니다.");
                }

                // 복원 순서: 시간 → 플레이어/영지(금고 포함) → 퀘스트/기타 → 용병 로스터 → 병사 원장/계약일 → AI 세수 멱등 상태
                ApplyTimeData(data.time);
                ApplyPlayerData(data.player);
                ApplyInventoryData(data.inventory);
                ApplyTerritoryData(data.territories, data.saveVersion >= 4);
                ApplyQuestData(data.quests);
                ApplyRevengeListData(data.revengeList);
                ApplyNationReputationData(data.nationReputations);
                // [D5-A] 마을 음식점/마약 상태 복원 — ApplyConstructionData(음식점 실물 복원 훅)보다
                // 먼저 실행해야 한다. ImportSaveEntries가 상태를 전면 클리어하므로, 실물 복원
                // (RestaurantPropBuilder.RebuildFromSave)이 세팅하는 restaurantBuilt 플래그를
                // 클리어로 지우지 않으려면 마을 상태 대입이 선행되어야 한다.
                VillageDrugState.ImportSaveEntries(data?.villageDrugs);
                ApplyConstructionData(data.constructions);
                HorseMountSystem.ImportSaveData(data?.horses); // [무번호 86-P1] 말 상태 복원 — 널/빈 리스트는 무시(전부 야생 유지)
                ApplyEconomyAndMercenaryData(data);

                // [A2 이계] 마지막 로드 슬롯 기록 — 이계 퍼머데스가 "현재 플레이 슬롯" 삭제에 사용
                _lastLoadedSlot = slotIndex;

                // C20-01: 난이도 복원
                GameManager.CurrentDifficulty = (int)data.difficulty;
                Debug.Log($"[SaveManager] 난이도 복원: {data.difficulty}");

                if (_verbose) Debug.Log($"[SaveManager] 슬롯 {slotIndex} 로드 완료 (Day {data.time?.day}, 저장시각: {data.timestamp})");

                OnLoadCompleted?.Invoke(slotIndex);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] 슬롯 {slotIndex} 로드 중 오류 발생: {ex.Message}\n{ex.StackTrace}");
            }
        }

        // ===== 슬롯 정보/조회 =====

        /// <summary>
        /// 지정된 슬롯의 SaveData를 반환합니다. 미리보기/메타데이터용.
        /// </summary>
        public SaveData GetSlotInfo(int slotIndex)
        {
            try
            {
                string filePath = GetSlotFilePath(slotIndex);
                if (!File.Exists(filePath)) return null;

                string json = File.ReadAllText(filePath);
                return JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception ex)
            {
                if (_verbose) Debug.LogWarning($"[SaveManager] 슬롯 {slotIndex} 정보 로드 중 오류: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 모든 슬롯의 정보를 배열로 반환합니다. 없는 슬롯은 null.
        /// </summary>
        public SaveData[] GetAllSlotInfos()
        {
            SaveData[] infos = new SaveData[_slotCount];
            for (int i = 0; i < _slotCount; i++)
            {
                infos[i] = GetSlotInfo(i);
            }
            return infos;
        }

        /// <summary>
        /// 지정된 슬롯에 저장된 데이터가 있는지 확인합니다.
        /// </summary>
        /// <param name="slotIndex">슬롯 인덱스 (0~4)</param>
        public bool HasSave(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slotCount) return false;
            return File.Exists(GetSlotFilePath(slotIndex));
        }

        /// <summary>
        /// 지정된 슬롯의 저장 파일을 삭제합니다.
        /// </summary>
        /// <param name="slotIndex">슬롯 인덱스 (0~4)</param>
        public void DeleteSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slotCount)
            {
                Debug.LogError($"[SaveManager] 잘못된 슬롯 인덱스: {slotIndex}. 유효 범위: 0~{_slotCount - 1}");
                return;
            }

            try
            {
                string filePath = GetSlotFilePath(slotIndex);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    if (_verbose) Debug.Log($"[SaveManager] 슬롯 {slotIndex} 삭제됨: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] 슬롯 {slotIndex} 삭제 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 유효한 슬롯 수를 반환합니다.
        /// </summary>
        public int SlotCount => _slotCount;

        /// <summary>[A2 이계] 마지막으로 로드한 슬롯 (퍼머데스 삭제 대상 — 로드 이력 없으면 -1 = 삭제할 세이브 없음).</summary>
        public int CurrentSaveSlot => _lastLoadedSlot;
        private int _lastLoadedSlot = -1;

        /// <summary>
        /// 모든 슬롯 중 가장 오래된(타임스탬프가 가장 이른) 슬롯 인덱스를 반환합니다.
        /// 슬롯이 하나도 없으면 0을 반환합니다.
        /// </summary>
        private int FindOldestSlot()
        {
            int oldest = 0;
            string oldestTimestamp = null;

            for (int i = 0; i < _slotCount; i++)
            {
                var info = GetSlotInfo(i);
                if (info == null) return i; // 빈 슬롯이 발견되면 즉시 반환

                if (oldestTimestamp == null || string.CompareOrdinal(info.timestamp, oldestTimestamp) < 0)
                {
                    oldestTimestamp = info.timestamp;
                    oldest = i;
                }
            }

            return oldest;
        }

        /// <summary>
        /// 자동 저장: 첫 번째 빈 슬롯 또는 가장 오래된 슬롯에 저장합니다.
        /// </summary>
        public void AutoSave()
        {
            // 빈 슬롯 우선 찾기
            for (int i = 0; i < _slotCount; i++)
            {
                if (!HasSave(i))
                {
                    Save(i);
                    Debug.Log($"[SaveManager] 자동 저장 완료 (슬롯 {i})");
                    return;
                }
            }
            // 모두 차있으면 가장 오래된 슬롯에 덮어쓰기
            int oldestSlot = FindOldestSlot();
            Save(oldestSlot);
            Debug.Log($"[SaveManager] 자동 저장 완료 (슬롯 {oldestSlot} 덮어쓰기)");
        }

        // ===== 데이터 수집 (Collect) 메서드 =====
        // 각 시스템에서 현재 상태를 읽어 SaveData 구조체로 변환합니다.
        // TODO: 실제 시스템 연동 시 확장

        private PlayerSaveData CollectPlayerData()
        {
            PlayerSaveData data = new PlayerSaveData();

            // Player 위치/회전
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                Transform t = player.transform;
                data.posX = t.position.x;
                data.posY = t.position.y;
                data.posZ = t.position.z;
                data.rotY = t.rotation.eulerAngles.y;
            }

            // PlayerStats (레벨, 경험치, 골드)
            if (PlayerStats.Instance != null)
            {
                data.level = PlayerStats.Instance.Level;
                data.exp = PlayerStats.Instance.CurrentEXP;
                data.gold = PlayerStats.Instance.Gold;
                data.stamina = 100f;
            }

            // PlayerHealth (HP)
            if (PlayerHealth.Instance != null)
            {
                data.hp = PlayerHealth.Instance.CurrentHP;
                data.maxHp = PlayerHealth.Instance.MaxHP;
            }

            return data;
        }

        private InventorySaveData CollectInventoryData()
        {
            InventorySaveData data = new InventorySaveData
            {
                items = new List<ItemSlotSaveData>()
            };

            if (PlayerInventory.Instance != null)
            {
                var slots = PlayerInventory.Instance.GetAllSlots();
                foreach (var slot in slots)
                {
                    if (slot != null && slot.item != null && !string.IsNullOrEmpty(slot.item.id))
                    {
                        data.items.Add(new ItemSlotSaveData
                        {
                            itemId = slot.item.id,
                            quantity = slot.count
                        });
                    }
                }
            }

            return data;
        }

        private TimeSaveData CollectTimeData()
        {
            TimeSaveData data = new TimeSaveData();
            if (TimeManager.Instance != null)
            {
                data.day = TimeManager.Instance.CurrentDay;
                data.gameTime = TimeManager.Instance.GameTime;
            }
            return data;
        }

        /// <summary>
        /// v4: 용병 로스터 수집. MercenaryManager 부재 시 빈 블록을 반환해 로드 시 레거시와 구분되게 한다.
        /// </summary>
        private MercenarySaveData CollectMercenaryData()
        {
            var mgr = MercenaryManager.Instance;
            return mgr != null ? mgr.ExportSaveData() : new MercenarySaveData();
        }

        private List<TerritorySaveData> CollectTerritoryData()
        {
            List<TerritorySaveData> list = new List<TerritorySaveData>();

            try
            {
                if (TerritoryDatabase.Instance != null)
                {
                    foreach (var def in TerritoryDatabase.Instance.GetAllDefinitions())
                    {
                        var state = TerritoryDatabase.Instance.GetState(def.id);
                        if (state != null)
                        {
                            list.Add(new TerritorySaveData
                            {
                                territoryId = def.id.ToString(),
                                ownerNation = state.ownership.ToString(),
                                isConquered = state.ownership == TerritoryOwnership.PlayerOwned,
                                territoryGold = state.territoryGold, // v4: 영지 금고
                                // [F5] 살려준 영주 공물/배신 상태
                                lordSpared = state.lordSpared,
                                tributeWeeksPaid = state.tributeWeeksPaid,
                                betrayalCount = state.betrayalCount,
                                loyaltyToPlayer = state.loyaltyToPlayer,
                                // [113차] 영주 선호 음식 마지막 지급 일차 (미지급/구세이브 = -1)
                                lastPreferredFeedDay = state.lastPreferredFeedDay,
                                // [123차] 소유 영주 이름 (v6) — 영주↔영주 점령 시 승리 영주 기록
                                ownerLordName = state.ownerLordName,
                                // [126차] 영주 딸 호감도/결혼 상태 (구세이브는 기본값 0/false)
                                daughterAffection = state.daughterAffection,
                                daughterMarried = state.daughterMarried
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (_verbose) Debug.LogWarning($"[SaveManager] 영토 데이터 수집 중 오류: {ex.Message}");
            }

            return list;
        }

        private List<QuestSaveData> CollectQuestData()
        {
            List<QuestSaveData> list = new List<QuestSaveData>();

            try
            {
                // QuestManager는 static class — 모든 퀘스트 상태 읽기
                foreach (var def in QuestManager.GetAllDefinitions())
                {
                    var state = QuestManager.GetQuestState(def.questId);
                    list.Add(new QuestSaveData
                    {
                        questId = def.questId,
                        completed = state == QuestState.Completed,
                        questState = state.ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                if (_verbose) Debug.LogWarning($"[SaveManager] 퀘스트 데이터 수집 중 오류: {ex.Message}");
            }

            return list;
        }

        // ===== 데이터 적용 (Apply) 메서드 =====
        // 역직렬화된 데이터를 각 게임 시스템에 설정합니다.

        private void ApplyTimeData(TimeSaveData data)
        {
            if (data == null) return;
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.GameTime = data.gameTime;
                // CurrentDay는 읽기 전용이므로 리플렉션으로 설정
                SetPrivateField(TimeManager.Instance, "_currentDay", data.day);
            }
        }

        private void ApplyPlayerData(PlayerSaveData data)
        {
            if (data == null) return;

            // Player 위치/회전 복원
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                player.transform.position = new Vector3(data.posX, data.posY, data.posZ);
                player.transform.rotation = Quaternion.Euler(0, data.rotY, 0);
            }

            // PlayerStats 레벨/경험치 복원 (읽기 전용 속성이므로 리플렉션)
            try
            {
                if (PlayerStats.Instance != null)
                {
                    SetPrivateField(PlayerStats.Instance, "_level", data.level);
                    SetPrivateField(PlayerStats.Instance, "_currentEXP", (int)data.exp);
                    SetPrivateField(PlayerStats.Instance, "_gold", data.gold);
                }
            }
            catch (Exception ex)
            {
                if (_verbose) Debug.LogWarning($"[SaveManager] PlayerStats 복원 중 오류: {ex.Message}");
            }

            // PlayerHealth HP 복원
            try
            {
                if (PlayerHealth.Instance != null)
                {
                    SetPrivateField(PlayerHealth.Instance, "_currentHP", data.hp);
                    SetPrivateField(PlayerHealth.Instance, "_maxHP", (float)data.maxHp);
                }
            }
            catch (Exception ex)
            {
                if (_verbose) Debug.LogWarning($"[SaveManager] PlayerHealth 복원 중 오류: {ex.Message}");
            }
        }

        private void ApplyInventoryData(InventorySaveData data)
        {
            if (data == null) return;
            if (data.items == null)
            {
                if (_verbose) Debug.LogWarning("[SaveManager] 인벤토리 데이터(items)가 null입니다. 복원을 건너뛰니다.");
                return;
            }
            if (PlayerInventory.Instance == null) return;

            try
            {
                // 인벤토리 복원: 저장된 itemId → ItemData 조회 후 추가
                foreach (var itemSlot in data.items)
                {
                    if (string.IsNullOrEmpty(itemSlot.itemId)) continue;

                    PlayerInventory.ItemData item = FindItemDataById(itemSlot.itemId);
                    if (item != null)
                    {
                        PlayerInventory.Instance.AddItem(item, itemSlot.quantity);
                    }
                    else if (_verbose)
                    {
                        Debug.LogWarning($"[SaveManager] 알 수 없는 아이템 ID: {itemSlot.itemId}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] 인벤토리 복원 중 오류: {ex.Message}");
            }
        }

        private void ApplyTerritoryData(List<TerritorySaveData> data, bool restoreGold)
        {
            if (data == null) return;

            try
            {
                // [P1] 복원 모드 — 로드 중 소유 복원은 "새 점령"이 아니므로 OwnershipChanged 억제(칭호 카운터 오염 방지)
                ProjectName.Core.Data.TerritoryDatabase.OwnershipRestoreMode = true;
                if (TerritoryDatabase.Instance != null)
                {
                    foreach (var tData in data)
                    {
                        if (string.IsNullOrEmpty(tData.territoryId)) continue;

                        // territoryId 문자열 → TerritoryOwnership 파싱
                        TerritoryOwnership ownership;
                        if (System.Enum.TryParse(tData.ownerNation, out ownership))
                        {
                            // 모든 정의를 순회하며 ID 매칭
                            foreach (var def in TerritoryDatabase.Instance.GetAllDefinitions())
                            {
                                if (def.id.ToString() == tData.territoryId)
                                {
                                    TerritoryDatabase.Instance.SetOwnership(def.id, ownership);
                                    // v4: 금고 복원 (레거시 v3 이하는 territoryGold 필드가 없으므로 현재 값 유지)
                                    if (restoreGold)
                                    {
                                        var restored = TerritoryDatabase.Instance.GetState(def.id);
                                        if (restored != null) restored.territoryGold = tData.territoryGold;
                                    }
                                    // [F5] 살려준 영주 공물/배신 상태 복원 (필드 부재 세이브는 기본값 — JsonUtility가 false/0으로 채움)
                                    var f5state = TerritoryDatabase.Instance.GetState(def.id);
                                    if (f5state != null)
                                    {
                                        f5state.lordSpared = tData.lordSpared;
                                        f5state.tributeWeeksPaid = tData.tributeWeeksPaid;
                                        f5state.betrayalCount = tData.betrayalCount;
                                        f5state.loyaltyToPlayer = tData.loyaltyToPlayer;
                                        // [113차] 영주 선호 음식 마지막 지급 일차 복원 (필드 부재 시 JsonUtility가 초기값 -1 유지)
                                        f5state.lastPreferredFeedDay = tData.lastPreferredFeedDay;
                                        // [123차] 소유 영주 복원 (v6) — 구세이브(필드 부재 = 빈 값)는
                                        // 원래 영주(def.lord.lordName)로 폴백. OwnershipRestoreMode 경로도 동일 적용.
                                        f5state.ownerLordName = string.IsNullOrEmpty(tData.ownerLordName)
                                            ? def.lord.lordName
                                            : tData.ownerLordName;
                                        // [126차] 영주 딸 호감도/결혼 상태 복원 (필드 부재 세이브는 기본값 0/false)
                                        f5state.daughterAffection = tData.daughterAffection;
                                        f5state.daughterMarried = tData.daughterMarried;
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
                ProjectName.Core.Data.TerritoryDatabase.OwnershipRestoreMode = false; // [P1] 정상 종료 해제
            }
            catch (Exception ex)
            {
                ProjectName.Core.Data.TerritoryDatabase.OwnershipRestoreMode = false; // 오류 시에도 해제
                if (_verbose) Debug.LogWarning($"[SaveManager] 영토 데이터 복원 중 오류: {ex.Message}");
            }
        }

        private void ApplyQuestData(List<QuestSaveData> data)
        {
            if (data == null) return;

            try
            {
                foreach (var qData in data)
                {
                    if (string.IsNullOrEmpty(qData.questId)) continue;

                    // questState 필드가 있으면 원래 상태를 복원, 없으면 completed 호환 유지
                    QuestState newState;
                    if (!string.IsNullOrEmpty(qData.questState))
                    {
                        if (!System.Enum.TryParse(qData.questState, out newState))
                            newState = qData.completed ? QuestState.Completed : QuestState.Available;
                    }
                    else
                    {
                        newState = qData.completed ? QuestState.Completed : QuestState.Available;
                    }
                    QuestManager.ForceState(qData.questId, newState);
                }
            }
            catch (Exception ex)
            {
                if (_verbose) Debug.LogWarning($"[SaveManager] 퀘스트 데이터 복원 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// C14-09: 복수명부 데이터 수집
        /// </summary>
        private RevengeListSaveData CollectRevengeListData()
        {
            try
            {
                return RevengeListManager.Instance.SaveState();
            }
            catch (Exception ex)
            {
                if (_verbose) Debug.LogWarning($"[SaveManager] 복수명부 데이터 수집 중 오류: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// C14-09: 복수명부 데이터 복원
        /// </summary>
        private void ApplyRevengeListData(RevengeListSaveData data)
        {
            if (data == null) return;

            try
            {
                if (!RevengeListManager.Instance.IsInitialized)
                {
                    RevengeListManager.Instance.Initialize();
                    if (_verbose) Debug.Log("[SaveManager] RevengeListManager 자동 초기화 완료");
                }
                RevengeListManager.Instance.LoadState(data);
            }
            catch (Exception ex)
            {
                if (_verbose) Debug.LogWarning($"[SaveManager] 복수명부 데이터 복원 중 오류: {ex.Message}");
            }
        }

        // ===== 국가 호감도 저장/로드 =====

        /// <summary>국가 호감도 데이터 수집</summary>
                // ===== Phase O8: 건설 데이터 수집/적용 =====

        private List<ConstructionSaveEntry> CollectConstructionData()
        {
            var entries = ConstructionManager.Instance != null
                ? ConstructionManager.Instance.GetSaveEntries()
                : null;
            return entries ?? new List<ConstructionSaveEntry>();
        }

        private void ApplyConstructionData(List<ConstructionSaveEntry> entries)
        {
            var mgr = ConstructionManager.Instance ?? FindAnyObjectByType<ConstructionManager>();
            if (mgr == null) return;
            mgr.ApplySaveEntries(entries ?? new List<ConstructionSaveEntry>());
        }

private NationReputationSaveData CollectNationReputationData()
        {
            var data = new NationReputationSaveData();
            try
            {
                if (NationReputationSystem.Instance != null)
                {
                    var reps = NationReputationSystem.Instance.GetAllReputations();
                    foreach (var kvp in reps)
                    {
                        data.reputations.Add(new NationRepEntry
                        {
                            nationKey = kvp.Key,
                            value = kvp.Value
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                if (_verbose) Debug.LogWarning($"[SaveManager] 국가 호감도 데이터 수집 중 오류: {ex.Message}");
            }
            return data;
        }

        /// <summary>국가 호감도 데이터 복원</summary>
        private void ApplyNationReputationData(NationReputationSaveData data)
        {
            if (data == null) return;
            if (data.reputations == null)
            {
                if (_verbose) Debug.LogWarning("[SaveManager] 국가 호감도 데이터(reputations)가 null입니다.");
                return;
            }
            try
            {
                if (NationReputationSystem.Instance != null)
                {
                    var dict = new Dictionary<string, int>();
                    foreach (var entry in data.reputations)
                    {
                        if (!string.IsNullOrEmpty(entry.nationKey))
                            dict[entry.nationKey] = entry.value;
                    }
                    NationReputationSystem.Instance.LoadAllReputations(dict);
                }
            }
            catch (Exception ex)
            {
                if (_verbose) Debug.LogWarning($"[SaveManager] 국가 호감도 데이터 복원 중 오류: {ex.Message}");
            }
        }

        // ===== 리플렉션 / 아이템 조회 헬퍼 =====

        /// <summary>
        /// v4 경제/용병 복원. 의존 순서: 용병 로스터 → 병사 입단 원장/계약 청구일 → AI 세수 멱등 상태.
        /// 영지 금고는 ApplyTerritoryData에서 이미 복원된 뒤다. 복원 자체는 골드를 청구하지 않는다.
        /// 레거시(해당 블록 null) 세이브: 경제 블록은 현재 게임일 기준으로 시딩하여 소급 청구/과세를 막고,
        /// 고용 시장 블록은 기본 상태로 리셋한다.
        /// </summary>
        private void ApplyEconomyAndMercenaryData(SaveData data)
        {
            int currentDay = data.time != null ? data.time.day : SoldierWageSystem.CurrentDay();
            try
            {
                // 1) 용병 로스터 (레거시 null이면 현재 로스터를 건드리지 않음 — 기존 동작 보존)
                if (data.mercenaries != null)
                    MercenaryManager.Instance?.ImportSaveData(data.mercenaries);

                // 2) 병사 입단 원장 + 용병 계약 청구일 (로스터 이후)
                SoldierWageSystem.ImportSaveData(data.soldierWage, currentDay);

                // 3) AI 세수/유지비 멱등 상태 (영지 금고 복원 이후)
                AITerritoryEconomySystem.ImportSaveData(data.aiEconomy, currentDay);

                // 4) 고용 시장 AI 카운트/누적/마지막 처리일 (영지 금고 이후).
                //    레거시(null): API의 null 처리는 lastProcessedDay를 -1로 리셋하므로, 현재 게임일을 처리 완료로
                //    시딩하는 기본 블록을 넘긴다. 같은 날 고용 틱이 로드 직후 다시 실행되는 것을 막고,
                //    이전 세션의 인메모리 카운트도 함께 비운다 (레거시 고용 이력은 복원하지 않음).
                LaborMarketSystem.ImportSaveData(data.laborMarket ?? new LaborMarketSaveData { lastProcessedDay = currentDay });

                // 5) [F5] 살려준 영주 공물/배신 — 등록 복원(lordSpared 스캔) + 주차 멱등 상태
                SparedLordTributeSystem.ImportSaveData(data.sparedLords, currentDay);
                LordBetrayalSystem.EnsureSubscribed();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] 경제/용병 데이터 복원 중 오류: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 리플렉션으로 private 필드 값을 설정합니다.
        /// </summary>
        private void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null)
            {
                field.SetValue(target, value);
            }
            else if (_verbose)
            {
                Debug.LogWarning($"[SaveManager] 필드를 찾을 수 없음: {target.GetType().Name}.{fieldName}");
            }
        }

        /// <summary>
        /// PlayerInventory의 정적 ItemData 필드에서 itemId로 ItemData를 찾습니다.
        /// </summary>
        private PlayerInventory.ItemData FindItemDataById(string itemId)
        {
            // PlayerInventory의 모든 public static ItemData 필드 검색
            var fields = typeof(PlayerInventory).GetFields(BindingFlags.Public | BindingFlags.Static);
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(PlayerInventory.ItemData))
                {
                    var item = field.GetValue(null) as PlayerInventory.ItemData;
                    if (item != null && item.id == itemId)
                        return item;
                }
            }
            return null;
        }
    }
}