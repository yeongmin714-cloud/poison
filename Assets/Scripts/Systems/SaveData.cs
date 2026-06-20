using System;
using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// C16-04: JSON 직렬화 가능한 최상위 저장 데이터 컨테이너.
    /// JsonUtility와 완벽 호환 (System.Serializable + public 필드).
    /// </summary>
    [System.Serializable]
    public class SaveData
    {
        public int saveVersion = 1;
        public string timestamp;
        public DifficultyMode difficulty = DifficultyMode.Normal; // C20-01
        public PlayerSaveData player;
        public InventorySaveData inventory;
        public TimeSaveData time;
        public List<TerritorySaveData> territories;
        public List<QuestSaveData> quests;
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
        public int maxHp;
        public float stamina;
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

    // ===== Phase 5.6.2: 창고 데이터 =====
    [System.Serializable]
    public class WarehouseSlotData
    {
        public string itemId;
        public int quantity;
    }

    [System.Serializable]
    public class TerritoryWarehouseData
    {
        public string territoryId;
        public List<WarehouseSlotData> slots;
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