using System;
using System.Collections.Generic;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// C16-04: JSON 직렬화 가능한 최상위 저장 데이터 컨테이너.
    /// JsonUtility와 완벽 호환 (System.Serializable + public 필드).
    ///
    /// ⚠ saveVersion 관리 규칙:
    ///   - 데이터 구조 변경 시 saveVersion을 증가시키고,
    ///   - SaveManager.Load()에서 버전 검증 + 마이그레이션 로직 추가 필수.
    ///   - 현재 v2: PlayerSaveData.gold 추가, QuestSaveData.questState 추가.
    ///     v1: 초기 스키마.
    ///
    /// ⚠ equipment / warehouse / church / nationReputations 필드:
    ///   - Phase 5.6.3 / 5.6.2 / 5.7.3 에서 추가된 예비 필드.
    ///   - SaveManager.Save()/Load()에서 아직 일부 연동되지 않음 (TODO).
    ///   - Null-safe하게 설계되어 있으므로 역직렬화 시 문제없음.
    /// </summary>
    [System.Serializable]
    public class SaveData
    {
        public int saveVersion = 2; // v2: PlayerSaveData.gold 추가, QuestSaveData.questState 추가
        /// <summary>저장 시점의 타임스탬프. SaveManager에서 "yyyy-MM-dd HH:mm:ss" 형식으로 설정.</summary>
        public string timestamp;
        public DifficultyMode difficulty = DifficultyMode.Normal; // C20-01
        public PlayerSaveData player;
        public InventorySaveData inventory;
        public TimeSaveData time;
        public List<TerritorySaveData> territories = new List<TerritorySaveData>();
        public List<QuestSaveData> quests = new List<QuestSaveData>();
        public RevengeListSaveData revengeList;  // C14-02: 복수명부 저장 데이터
        public EquipmentSaveData equipment;      // Phase 5.6.3: 장비창 저장 데이터
        public WarehouseSaveData warehouse;      // Phase 5.6.2: 영지 창고 저장 데이터 (from WarehouseSystem.cs)
        public ChurchSaveData church;            // Phase 5.7.3: 성당 친밀도 저장 데이터
        public NationReputationSaveData nationReputations; // 국가 호감도 저장 데이터
    }

    [System.Serializable]
    public class PlayerSaveData
    {
        public float posX, posY, posZ;
        public float rotY;
        public int level;
        public float exp;
        public float hp;
        public float maxHp;
        public float stamina;
        public int gold;
    }

    [System.Serializable]
    public class InventorySaveData
    {
        public List<ItemSlotSaveData> items;
    }

    [System.Serializable]
    public class ItemSlotSaveData
    {
        public string itemId;
        public int quantity;
    }

    [System.Serializable]
    public class TimeSaveData
    {
        public int day;
        public float gameTime;
    }

    [System.Serializable]
    public class TerritorySaveData
    {
        public string territoryId;
        public string ownerNation;
        public bool isConquered;
    }

    [System.Serializable]
    public class QuestSaveData
    {
        public string questId;
        public bool completed;
        /// <summary>퀘스트 상태 문자열: "Locked","Available","Active","Completed","Failed"</summary>
        public string questState;
    }

    // ===== Phase 5.6.3: 장비 데이터 =====
    [System.Serializable]
    public class EquipmentSaveData
    {
        public string helmetItemId;
        public string armorItemId;
        public string weaponItemId;
        public string shoesItemId;
        public string glovesItemId;
        public string backItemId;
        public int helmetDurability;
        public int armorDurability;
        public int weaponDurability;
        public int shoesDurability;
        public int glovesDurability;
        public int backDurability;
    }

    // ===== Phase 5.7.3: 성당 데이터 =====
    [System.Serializable]
    public class ChurchFavorData
    {
        public string territoryId;
        public int favor;
    }

    [System.Serializable]
    public class ChurchSaveData
    {
        public List<ChurchFavorData> territories;
    }

    // ===== 국가 호감도 저장 데이터 =====
    /// <summary>
    /// 국가 호감도 저장 컨테이너.
    /// nationKey는 NationType.ToString() 값 (East/West/South/North/Empire/Dracula).
    /// </summary>
    [System.Serializable]
    public class NationReputationSaveData
    {
        public List<NationRepEntry> reputations = new List<NationRepEntry>();
    }

    [System.Serializable]
    public class NationRepEntry
    {
        public string nationKey; // NationType.ToString()
        public int value;
    }
}