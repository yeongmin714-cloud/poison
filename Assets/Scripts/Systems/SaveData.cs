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
    ///   - v6: SaveData.horses(HorseSaveEntry — 말 품종/길들임/위치) 추가 [무번호 86-P1].
    ///     v5 이하 세이브는 horses 필드가 없으므로 빈 리스트로 로드되며, 말 전부 야생으로 취급된다.
    ///   - 현재 v5: LaborMarketSaveData.hiredGuards(AI 고용 병사 로스터, AIHiredGuardSaveEntry) 추가.
    ///     v4 이하 세이브는 hiredGuards가 없으므로 null/빈 리스트로 로드되며, "AI 고용 병사 없음"으로 취급된다
    ///     (LaborMarketSystem.ImportSaveData가 기존 마커 병사를 정리). 구버전 카운트는 기록용으로만 남는다.
    ///   - v4: 영지 금고(TerritorySaveData.territoryGold), 병사 입단 원장(soldierWage),
    ///     AI 영지 세수/유지비 멱등 상태(aiEconomy), 용병 로스터(mercenaries), 고용 시장 카운트(laborMarket) 추가.
    ///     v3 이하 세이브에는 해당 필드가 없으므로 로드 시 현재 날짜 기준으로 시딩하여 소급 청구를 막는다.
    ///   - v3: SaveData.isNewGamePlus 추가.
    ///   - v2: PlayerSaveData.gold 추가, QuestSaveData.questState 추가.
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
        public int saveVersion = 6; // v6: SaveData.horses(HorseSaveEntry — 말 품종/길들임/위치) 추가 [무번호 86-P1]. v5: LaborMarketSaveData.hiredGuards(AI 고용 병사 로스터) 추가 + [F5] TerritorySaveData.lordSpared/tributeWeeksPaid/betrayalCount/loyaltyToPlayer + sparedLords 블록. v4: 영지 금고/병사 입단 원장/AI 세수 상태/용병 로스터/고용 시장 카운트 추가 (v3: isNewGamePlus)
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
        public bool isNewGamePlus = false;       // NG+ 모드로 저장된 데이터인지 여부
        public List<ConstructionSaveEntry> constructions = new List<ConstructionSaveEntry>(); // Phase O8: 성 영지 건설
        public List<VillageDrugSaveEntry> villageDrugs = new List<VillageDrugSaveEntry>(); // [D5-A] 마을 음식점/마약 상태. DTO는 Core/Data(VillageDrugState.cs) 소속 — Core asmdef는 Systems 참조 불가(순환 금지)라 Systems 쪽 DTO를 Core 수출 헬퍼가 못 본다. 구버전 세이브는 필드 부재 → 빈 리스트로 로드되며 ImportSaveEntries가 기존 상태를 클리어 후 복원. 재고 있는 마을만 저장(용량 절감)

        // ===== v4 추가 필드 (v3 이하 세이브에서는 null) =====
        public SoldierWageSaveData soldierWage;     // 병사 입단 원장 + 용병 계약 다음 청구일
        public AIEconomySaveData aiEconomy;         // AI 영지 세수/유지비 멱등 상태
        public MercenarySaveData mercenaries;       // 고용 용병 로스터 (Unity 참조 없음, id 기반)
        public LaborMarketSaveData laborMarket;     // 고용 시장 AI 고용 카운트 (LaborMarketSystem.Export/ImportSaveData 연동; 방출 풀의 병사 객체는 미저장)
        public SparedLordSaveData sparedLords;      // [F5] 살려준 영주 공물 멱등 상태 (v5+; 영지별 spare/충성도는 TerritorySaveData)
        public List<HorseSaveEntry> horses = new List<HorseSaveEntry>(); // [무번호 86-P1] v6: 말 상태(품종/길들임/위치). 구세이브는 필드 부재 → 빈 리스트로 로드(전부 야생 유지)
    }

    [System.Serializable]
    public class ConstructionSaveEntry
    {
        public string structureId;
        public string blueprintId;
        public string territoryId;
        public float posX, posY, posZ;
        public float progress;
        public bool isComplete;
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
        /// <summary>영지 금고 (v4+). v3 이하 세이브에서는 필드가 없어 0이며, 복원 시 무시된다.</summary>
        public int territoryGold;
        /// <summary>[F5] 영주 살려주기 여부 (v5+). 없는 세이브는 false — 배신/공물 상태와 함께 복원.</summary>
        public bool lordSpared;
        /// <summary>[F5] 공물 지급 누적 주차 수 (v5+).</summary>
        public int tributeWeeksPaid;
        /// <summary>[F5] 배신 누적 횟수 (v5+).</summary>
        public int betrayalCount;
        /// <summary>[F5] 플레이어 충성도 (v5+) — 공물 액수/배신 확률 재계산에 사용.</summary>
        public float loyaltyToPlayer;
        /// <summary>[113차] 마지막 선호 음식 지급 일차 (v5+). 구세이브 누락 시 -1(미지급) 유지가 핵심 — 초기값 -1.</summary>
        public int lastPreferredFeedDay = -1;
        /// <summary>[123차] 소유 영주 이름 (v6+) — 영주↔영주 점령 시 승리 영주 기록.
        /// 구세이브(필드 부재 = 빈 값)는 복원 시 원래 영주(def.lord.lordName) 폴백.</summary>
        public string ownerLordName;
        /// <summary>[126차] 영주 딸 호감도 (0~100). 구세이브는 JsonUtility 기본값 0 — saveVersion 미증가.</summary>
        public int daughterAffection = 0;
        /// <summary>[126차] 영주 딸 결혼 여부. 구세이브는 JsonUtility 기본값 false.</summary>
        public bool daughterMarried = false;
    }

    // ===== F5: 살려준 영주 공물/배신 (v5+) =====
    /// <summary>
    /// 살려준 영주 공물 멱등 상태. 영지별 spare/충성도/누적은 TerritorySaveData에 있다.
    /// </summary>
    [System.Serializable]
    public class SparedLordSaveData
    {
        /// <summary>마지막으로 공물을 지급한 주차 인덱스 (게임일/7). -1 = 없음. 재로드 동일 주 재지급 방지.</summary>
        public int lastTributeWeek = -1;
    }

    // ===== v4: 병사 입단 원장 / 용병 계약 스케줄 =====
    [System.Serializable]
    public class SoldierWageSaveData
    {
        /// <summary>병사 입단 누적 횟수 (stableId 기준). 세션 내 생성 순서 기반 ID이므로 재로드 후 대응 병사가 없을 수 있다.</summary>
        public List<AdmissionCountSaveEntry> admissionCounts = new List<AdmissionCountSaveEntry>();
        /// <summary>용병별 다음 주간 계약비 청구일 (게임일).</summary>
        public List<ContractDaySaveEntry> nextContractDays = new List<ContractDaySaveEntry>();
    }

    [System.Serializable]
    public class AdmissionCountSaveEntry
    {
        public string stableId;
        public int count;
    }

    [System.Serializable]
    public class ContractDaySaveEntry
    {
        public string mercenaryId;
        public int nextContractDay;
    }

    // ===== v4: AI 영지 세수/유지비 멱등 상태 =====
    /// <summary>
    /// 로드 후 같은 주/같은 날 세수·유지비가 다시 청구되지 않도록 마지막 처리 주차/일을 보존한다.
    /// 영지 금고 잔액은 TerritorySaveData.territoryGold에 있다.
    /// </summary>
    [System.Serializable]
    public class AIEconomySaveData
    {
        /// <summary>마지막으로 과세한 주차 인덱스 (AITerritoryEconomySystem.GetTaxWeekIndex). -1 = 없음.</summary>
        public int lastTaxWeek = -1;
        /// <summary>마지막으로 유지비를 지불한 게임일. -1 = 없음.</summary>
        public int lastMaintenanceDay = -1;
    }

    // ===== v4: 용병 로스터 =====
    [System.Serializable]
    public class MercenarySaveData
    {
        /// <summary>고용 중인 용병 인스턴스. 용병 정의(MercenaryData)는 id로 DB에서 재조회한다.</summary>
        public List<MercenaryInstanceSaveData> hired = new List<MercenaryInstanceSaveData>();
        /// <summary>용병별 호감도 (고용 여부와 무관하게 MercenaryManager의 단일 호감도 테이블).</summary>
        public List<AffinitySaveEntry> affinities = new List<AffinitySaveEntry>();
    }

    [System.Serializable]
    public class MercenaryInstanceSaveData
    {
        public string mercenaryId;
        public float currentHP;
        public bool isAlive;
        public float affinity;
        // 물약 슬롯(potionSlots)은 아이템 참조라 저장하지 않는다 — 로드 시 빈 슬롯.
    }

    [System.Serializable]
    public class AffinitySaveEntry
    {
        public string mercenaryId;
        public float value;
    }

    // ===== v4: 고용 시장 AI 고용 카운트 =====
    /// <summary>
    /// LaborMarketSystem의 AI 고용 카운트 영속 데이터. 방출 풀(_pool)은 Unity 병사 객체 참조이므로 저장하지 않는다.
    /// 카운트는 기록용 — 실제 월드 병사 객체는 이 데이터로 재생성되지 않는다.
    /// </summary>
    [System.Serializable]
    public class LaborMarketSaveData
    {
        public int lastProcessedDay = -1;
        public int totalHiredByAI;
        public List<TerritoryHireCountEntry> hiredCounts = new List<TerritoryHireCountEntry>();
        /// <summary>AI 고용 병사 로스터 (v5+). 구버전 세이브에서는 null/빈 리스트.</summary>
        public List<AIHiredGuardSaveEntry> hiredGuards = new List<AIHiredGuardSaveEntry>();
    }

    /// <summary>
    /// AI 고용 병사 1명의 영속 DTO. GuardPlaceholder.ToPersistentRosterEntry / ApplyPersistentRosterEntry와 대응.
    /// stableId = 병사 식별자("G" + 6자리 시퀀스). 스탯은 장비 보정 전 순수 롤 값.
    /// </summary>
    [System.Serializable]
    public class AIHiredGuardSaveEntry
    {
        public string stableId;
        public string territoryId;      // TerritoryId.ToString() (예: "East_01")
        public string guardName;
        public int level;
        public string nation;           // 병사 국가 표시명 (동/서/남/북/황제국/무소속)
        public float hp;
        public float maxHP;             // 장비/Vit 보정 포함 최대 HP (GetMaxHP 기준)
        public float loyalty;
        public int atk;
        public int def;
        public int vit;
        public int agi;
        public bool isAlive = true;
        public int gearDifficulty;      // GearDifficultyBonus (0~4)
    }

    [System.Serializable]
    public class TerritoryHireCountEntry
    {
        /// <summary>TerritoryId.ToString() 값 (예: "East_01").</summary>
        public string territoryId;
        public int count;
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

    // ===== [무번호 86-P1] 말 상태 저장 데이터 (v6+) =====
    /// <summary>
    /// 말 1핀 상태 — breed는 HorseMountSystem.Breeds 품종 문자열(백마/적토마).
    /// 구버전 세이브에는 horses 필드 자체가 없어 빈 리스트로 로드된다(전부 야생 유지).
    /// </summary>
    [System.Serializable]
    public class HorseSaveEntry
    {
        public string breed;
        public bool tamed;
        public float posX, posY, posZ;
    }
}