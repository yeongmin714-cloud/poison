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
                    nationReputations = CollectNationReputationData()
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
                if (data.saveVersion < 1 || data.saveVersion > 2)
                {
                    Debug.LogWarning($"[SaveManager] 슬롯 {slotIndex}의 saveVersion({data.saveVersion})이 현재 버전(2)과 다릅니다. 호환을 시도합니다.");
                }

                ApplyTimeData(data.time);
                ApplyPlayerData(data.player);
                ApplyInventoryData(data.inventory);
                ApplyTerritoryData(data.territories);
                ApplyQuestData(data.quests);
                ApplyRevengeListData(data.revengeList);
                ApplyNationReputationData(data.nationReputations);

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
                                isConquered = state.ownership == TerritoryOwnership.PlayerOwned
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

        private void ApplyTerritoryData(List<TerritorySaveData> data)
        {
            if (data == null) return;

            try
            {
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
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
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