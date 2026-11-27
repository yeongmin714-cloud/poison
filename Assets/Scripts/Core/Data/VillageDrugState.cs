using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Core.Data
{
    /// <summary>
    /// 음식점 재고 슬롯 1건 — 음식 1종 × 마약 여부/마약 단계 조합을 슬롯 단위로 관리한다.
    /// (struct: 복사 전달 용도 — 실제 재고 소유권은 VillageDrugState 내부에 있다)
    /// ⚠ CS0246 교훈(중첩 타입의 외부 파일 미한정 참조 금지) — 최상위 public struct로 선언한다.
    /// </summary>
    public struct RestaurantStockItem
    {
        public string foodItemId;    // 음식 아이템 ID (재고 병합 키의 일부)
        public string displayName;   // 표시명 (예: "마약 뿌려진 고기구이")
        public int count;            // 재고 수량
        public bool isDrugged;       // 마약 음식 여부 (병합 키의 일부)
        public int drugStage;        // 마약 단계 1~10 (DrugDatabase stage — 일반 음식은 0)
        public int basePrice;        // 일반 기본가 (권장가/수요 수학의 기준 입력)
        public int placedTotal;      // [88차] 누적 배치 수 — AddStock 병합/신규 무관 가산(Figma "판매 / 배치" 진행바)
        public int soldTotal;        // [88차] 누적 판매 수 — ConsumeStock 실소모 가산(슬롯 존속 기간 집계)
    }

    /// <summary>
    /// 마을 단위 음식점/마약 상태의 조회용 스냅샷 (읽기 전용 복사본).
    /// ⚠ CS0246 교훈 — 최상위 public struct로 선언한다.
    /// </summary>
    public struct VillageDrugSnapshot
    {
        public bool restaurantBuilt;   // 음식점 건설 여부
        public string assignedGuardId; // 배치 병사 ID (빈 문자열 = 미배치)
        public float addiction;        // 마을 주민 중독도 (0~100, 마을 단위 공유값)
        public int registeredPrice;    // 플레이어 등록 판매가 (권장가와 별개 — 수요 곡선 입력)
        public int normalRegisteredPrice; // [100차] 마을 단위 일반 음식 등록가 (0=미설정 → 재고 basePrice 사용)
        public int departedVillagers;  // 이탈 주민 누적 수
    }

    // ===== [D5-A] 세이브 DTO =====

    /// <summary>
    /// 마을 음식점/마약 상태 저장 DTO 1건 (마을 단위).
    /// ⚠ 위치 규약: SaveData.cs(Systems 어셈블리)가 아니라 본 파일(Core 어셈블리)에 둔다.
    ///   Core asmdef는 Systems 참조 불가(asmdef 순환 금지) — DTO를 SaveData.cs 쪽에 두면
    ///   VillageDrugState의 수출/수입 헬퍼가 컴파일되지 않는다. SaveData 루트는 기존
    ///   using ProjectName.Core.Data 로 본 타입을 그대로 참조하며 JsonUtility 직렬화 결과는 동일하다.
    /// </summary>
    [System.Serializable]
    public class VillageDrugSaveEntry
    {
        public int nation;              // NationType enum 정수 (결정론 키의 국가 축)
        public int villageIndex;        // 마을 인덱스 0..19 (결정론 키의 마을 축)
        public bool restaurantBuilt;    // 음식점 건설 여부
        public string assignedGuardId;  // 배치 병사 ID (빈 문자열 = 미배치)
        public float addiction;         // 마을 중독도 (0~100)
        public int registeredPrice;     // 플레이어 등록 판매가
        public int normalRegisteredPrice; // [100차] 일반 음식 등록가 (구세이브 누락 → 0 호환)
        public int departedVillagers;   // 이탈 주민 누적 수
        public int pendingDrugGold;     // [88차] 미수령 마약 판매금 적립 (구세이브 누락 → 0 호환)
        public int pendingNormalGold;   // [88차] 미수령 일반 판매금 적립 (구세이브 누락 → 0 호환)
        public List<VillageDrugStockSaveEntry> stock; // 재고 슬롯 (입고 순서 보존)
    }

    /// <summary>음식점 재고 슬롯 1건의 저장 DTO — RestaurantStockItem 필드와 1:1 대응.</summary>
    [System.Serializable]
    public class VillageDrugStockSaveEntry
    {
        public string foodItemId;
        public string displayName;
        public int count;
        public bool isDrugged;
        public int drugStage;
        public int basePrice;
        public int placedTotal; // [88차] 누적 배치 수 (구세이브 누락 → 0 호환)
        public int soldTotal;   // [88차] 누적 판매 수 (구세이브 누락 → 0 호환)
    }

    /// <summary>
    /// 마을 단위 음식점/마약 상태 정적 저장소 (TerritoryDatabase의 static Dictionary 패턴 준용).
    ///
    /// 키 규약: (NationType nation, int villageIndex 0..19) — VillagePlacementSystem의
    ///   국가별 마을 20개(villageIndex 0~19 ↔ 영지 인덱스 1~20 1:1 대응)와 일치하는 결정론 키다.
    ///   NationType은 enum 정수로 치환해 저장하므로 프로세스 재시작 간에도 동일 키가 재현된다(결정론).
    ///
    /// 조회 규약: 상태를 만지지 않은 마을은 "기본값 스냅샷"을 반환한다
    ///   (restaurantBuilt=false, assignedGuardId="", addiction=0, registeredPrice=0, departedVillagers=0).
    ///   쓰기 메서드는 해당 마을 엔트리를 즉시 생성한다(TryGetValue 실패 시 신규 엔트리).
    ///
    /// 재고 규약: 병합 키 = (foodItemId, isDrugged, drugStage) — 동일 키 슬롯은 수량 합산,
    ///   이질 키 슬롯은 분리 보관. 표시명/기본가는 최신 입고값으로 갱신(마지막 입고 기준).
    ///
    /// 스레드/결정론 규약: 런타임 상태는 유니티 메인 스레드 단일 접근을 전제로 하며(TerritoryDatabase
    ///   패턴 준용 — 접근별 잠금 없음), 난수를 사용하지 않아 동일 호출 시퀀스는 항상 동일 상태 전이를 만든다.
    ///
    /// 세이브 연동 [D5-A]: ExportSaveEntries(수집)/ImportSaveEntries(복원) — 재고 있는 마을만 저장한다.
    /// </summary>
    public static class VillageDrugState
    {
        /// <summary>마을 1곳의 내부 상태. 외부 노출 금지 — 외부 조회는 VillageDrugSnapshot으로만 이루어진다.</summary>
        private class VillageDrugEntry
        {
            public bool restaurantBuilt;
            public string assignedGuardId = string.Empty;
            public float addiction;
            public int registeredPrice;
            public int normalRegisteredPrice; // [100차] 일반 음식 등록가 (0=미설정 → 재고 basePrice)
            public int departedVillagers;
            public int pendingDrugGold;   // [88차] 미수령 마약 판매금 적립 (RunDailySale → CollectRevenue)
            public int pendingNormalGold; // [88차] 미수령 일반 판매금 적립
            public List<RestaurantStockItem> stock = new List<RestaurantStockItem>();
        }

        /// <summary>마을 상태 사전 — 키 (국가 enum 정수, 마을 인덱스 0..19). 결정론 키 (static Dictionary 패턴).</summary>
        private static readonly Dictionary<(int nation, int villageIndex), VillageDrugEntry> _villages =
            new Dictionary<(int nation, int villageIndex), VillageDrugEntry>();

        // ===== 조회 =====

        /// <summary>
        /// 마을 상태 스냅샷 조회. 상태가 없는 마을은 기본값(restaurantBuilt=false, 가드 빈문자열,
        /// addiction=0, registeredPrice=0, departedVillagers=0)을 반환한다 — 예외는 던지지 않는다.
        /// </summary>
        public static VillageDrugSnapshot GetSnapshot(NationType nation, int villageIndex)
        {
            if (!_villages.TryGetValue(Key(nation, villageIndex), out VillageDrugEntry entry))
                return DefaultSnapshot();

            return new VillageDrugSnapshot
            {
                restaurantBuilt = entry.restaurantBuilt,
                assignedGuardId = string.IsNullOrEmpty(entry.assignedGuardId)
                    ? string.Empty
                    : entry.assignedGuardId,
                addiction = entry.addiction,
                registeredPrice = entry.registeredPrice,
                normalRegisteredPrice = entry.normalRegisteredPrice, // [100차] 스냅샷 1:1 노출
                departedVillagers = entry.departedVillagers,
            };
        }

        /// <summary>마을 중독도 조회 (0~100). 상태 없는 마을은 0.</summary>
        public static float GetAddiction(NationType nation, int villageIndex)
        {
            return _villages.TryGetValue(Key(nation, villageIndex), out VillageDrugEntry entry)
                ? entry.addiction
                : 0f;
        }

        /// <summary>플레이어 등록 판매가 조회. 상태 없는 마을은 0 (미등록).</summary>
        public static int GetRegisteredPrice(NationType nation, int villageIndex)
        {
            return _villages.TryGetValue(Key(nation, villageIndex), out VillageDrugEntry entry)
                ? entry.registeredPrice
                : 0;
        }

        /// <summary>
        /// 재고 목록 복사본 반환 — 외부에서 목록/항목을 변경해도 내부 상태에 영향이 없다.
        /// 상태 없는 마을은 빈 목록. 슬롯 순서는 입고 순서를 유지한다(결정론).
        /// </summary>
        public static List<RestaurantStockItem> GetStock(NationType nation, int villageIndex)
        {
            var copy = new List<RestaurantStockItem>();
            if (_villages.TryGetValue(Key(nation, villageIndex), out VillageDrugEntry entry))
                copy.AddRange(entry.stock);
            return copy;
        }

        // ===== 음식점/병사 =====

        /// <summary>음식점 건설 플래그 설정 (true=건설 완료).</summary>
        public static void SetRestaurantBuilt(NationType nation, int villageIndex, bool built)
        {
            GetOrCreate(nation, villageIndex).restaurantBuilt = built;
        }

        /// <summary>판매 담당 병사 배치. guardId가 null이면 빈 문자열(미배치)로 정규화한다.</summary>
        public static void AssignGuard(NationType nation, int villageIndex, string guardId)
        {
            GetOrCreate(nation, villageIndex).assignedGuardId =
                string.IsNullOrEmpty(guardId) ? string.Empty : guardId;
        }

        /// <summary>배치 병사 제거 (예: 발각 즉시 처형 후 해제). 상태 없는 마을이면 무시한다.</summary>
        public static void RemoveGuard(NationType nation, int villageIndex)
        {
            if (_villages.TryGetValue(Key(nation, villageIndex), out VillageDrugEntry entry))
                entry.assignedGuardId = string.Empty;
        }

        // ===== 재고 =====

        /// <summary>
        /// 재고 입고. 병합 키 (foodItemId, isDrugged, drugStage)가 같은 슬롯은 수량 합산하고,
        /// 이질 슬롯은 신규로 추가한다. 표시명은 빈 슬롯에만 채우고 기본가는 최신 입고값으로 갱신한다.
        /// 음수 수량은 0으로 방어 처리한다(경고 로그).
        /// </summary>
        public static void AddStock(NationType nation, int villageIndex, RestaurantStockItem item)
        {
            if (item.count < 0)
            {
                Debug.LogWarning($"[VillageDrugState] 음수 재고 입고 무시: {item.foodItemId} count={item.count}");
                item.count = 0;
            }

            VillageDrugEntry entry = GetOrCreate(nation, villageIndex);

            for (int i = 0; i < entry.stock.Count; i++)
            {
                RestaurantStockItem slot = entry.stock[i];
                bool sameKey = slot.foodItemId == item.foodItemId
                    && slot.isDrugged == item.isDrugged
                    && slot.drugStage == item.drugStage;
                if (!sameKey)
                    continue;

                // 동일 슬롯 병합 — 수량 합산, 표시명은 비어 있을 때만 보완, 기본가는 최신값
                slot.count += item.count;
                slot.placedTotal += item.count; // [88차] 병합 시에도 배치 누적 가산(기존 누적 보존)
                if (string.IsNullOrEmpty(slot.displayName) && !string.IsNullOrEmpty(item.displayName))
                    slot.displayName = item.displayName;
                slot.basePrice = item.basePrice;
                entry.stock[i] = slot;
                return;
            }

            // 이질 슬롯 — 신규 추가
            if (!string.IsNullOrEmpty(item.foodItemId))
            {
                item.placedTotal += item.count; // [88차] 신규 슬롯도 배치 누적 = 최초 입고 수량
                entry.stock.Add(item);
            }
        }

        /// <summary>
        /// 재고 소모 시도. 지정 병합 키 슬롯에서 min(요청 수량, 보유 수량)만큼 소모하고
        /// 실제 소모 수를 반환한다. 슬롯 부재/요청 0 이하면 0. 슬롯 수량이 0 이하가 되면 슬롯을 정리한다.
        /// </summary>
        public static int ConsumeStock(
            NationType nation, int villageIndex,
            string foodItemId, bool isDrugged, int drugStage, int count)
        {
            if (count <= 0)
                return 0;
            if (!_villages.TryGetValue(Key(nation, villageIndex), out VillageDrugEntry entry))
                return 0;

            for (int i = 0; i < entry.stock.Count; i++)
            {
                RestaurantStockItem slot = entry.stock[i];
                bool sameKey = slot.foodItemId == foodItemId
                    && slot.isDrugged == isDrugged
                    && slot.drugStage == drugStage;
                if (!sameKey)
                    continue;

                int consumed = Mathf.Min(count, slot.count);
                slot.count -= consumed;
                slot.soldTotal += consumed; // [88차] 실소모 수만큼 판매 누적 가산
                if (slot.count <= 0)
                    entry.stock.RemoveAt(i); // 0 이하 슬롯 정리
                else
                    entry.stock[i] = slot;
                return consumed;
            }

            return 0; // 해당 슬롯 없음
        }

        // ===== 중독/가격/이탈 =====

        /// <summary>마을 중독도 가산 — 누적값을 0..100으로 클램프하고 갱신된 중독도를 반환한다.</summary>
        public static float AddAddiction(NationType nation, int villageIndex, float gain)
        {
            VillageDrugEntry entry = GetOrCreate(nation, villageIndex);
            entry.addiction = Mathf.Clamp(entry.addiction + gain, 0f, 100f);
            return entry.addiction;
        }

        /// <summary>플레이어 등록 판매가 저장. 검증(음수 등)은 호출부(Systems/UI) 책임 — 원본 그대로 저장.</summary>
        public static void SetRegisteredPrice(NationType nation, int villageIndex, int price)
        {
            GetOrCreate(nation, villageIndex).registeredPrice = price;
        }

        /// <summary>
        /// [100차] 일반 음식 등록가 저장 — 0=미설정(재고 basePrice 사용 규약).
        /// 마약 registeredPrice와 독립 축. 검증은 호출부(UI CommitNormalRegisteredPrice) 책임.
        /// </summary>
        public static void SetNormalRegisteredPrice(NationType nation, int villageIndex, int price)
        {
            GetOrCreate(nation, villageIndex).normalRegisteredPrice = price;
        }

        /// <summary>주민 이탈 1건 기록 — departedVillagers 누적 +1.</summary>
        public static void RecordDeparture(NationType nation, int villageIndex)
        {
            GetOrCreate(nation, villageIndex).departedVillagers++;
        }

        // ===== 판매금 적립 [88차] =====

        /// <summary>
        /// [88차] 판매금 적립 — RunDailySale의 마약/일반 판매 수입을 마을 단위 pending으로 누적한다.
        /// 수령은 RestaurantSystem.CollectRevenue(Figma "판매금 받기")가 담당하며, 그 전까지
        /// PlayerStats 잔액은 불변이다(즉시 지급 폐지 계약). 음수 입력은 0 취급(방어 — 결정론 유지).
        /// </summary>
        public static void AddPendingRevenue(NationType nation, int villageIndex, int drugGold, int normalGold)
        {
            VillageDrugEntry entry = GetOrCreate(nation, villageIndex);
            if (drugGold > 0)
                entry.pendingDrugGold += drugGold;
            if (normalGold > 0)
                entry.pendingNormalGold += normalGold;
        }

        /// <summary>
        /// [88차] pending 적립금 조회 — drugGold/normalGold 분리 out. 상태 없는 마을은 (0, 0)이며
        /// 읽기 전용(상태 엔트리 생성 없음 — GetSnapshot 규약 동일).
        /// </summary>
        public static void GetPendingRevenue(NationType nation, int villageIndex, out int drugGold, out int normalGold)
        {
            drugGold = 0;
            normalGold = 0;
            if (!_villages.TryGetValue(Key(nation, villageIndex), out VillageDrugEntry entry))
                return;
            drugGold = entry.pendingDrugGold;
            normalGold = entry.pendingNormalGold;
        }

        /// <summary>
        /// [88차] pending 적립금 인출 — 보유한 (마약, 일반) 적립금을 반환하고 두 필드를 0으로 리셋한다(결정론).
        /// 상태 없는 마을은 (0, 0) 반환(상태 생성 없음). 반환 합계 = 수령 총액.
        /// </summary>
        public static (int drugGold, int normalGold) TakePendingRevenue(NationType nation, int villageIndex)
        {
            if (!_villages.TryGetValue(Key(nation, villageIndex), out VillageDrugEntry entry))
                return (0, 0);

            (int drugGold, int normalGold) taken = (entry.pendingDrugGold, entry.pendingNormalGold);
            entry.pendingDrugGold = 0;
            entry.pendingNormalGold = 0;
            return taken;
        }

        // ===== 세이브 수출/수입 [D5-A] =====

        /// <summary>
        /// 세이브 수집용 — 전 마을 상태를 결정론 순서(국가 → 마을 인덱스 오름차순)로 수출한다.
        /// 규약: 재고(슬롯 1개 이상)를 보유한 마을만 저장한다(용량 절감). 재고 없이 건설만 된
        /// 마을의 restaurantBuilt 플래그는 ConstructionManager 구조물 세이브 +
        /// RestaurantPropBuilder.RebuildFromSave(로드 훅) 경로가 복원한다.
        /// </summary>
        public static List<VillageDrugSaveEntry> ExportSaveEntries()
        {
            var list = new List<VillageDrugSaveEntry>();
            var keys = new List<(int nation, int villageIndex)>(_villages.Keys);
            // 결정론 순회 — 사전 내부 순서에 의존하지 않고 (국가, 마을 인덱스) 오름차순으로 정렬한다.
            keys.Sort((a, b) =>
            {
                int byNation = a.nation.CompareTo(b.nation);
                return byNation != 0 ? byNation : a.villageIndex.CompareTo(b.villageIndex);
            });

            foreach ((int nation, int villageIndex) key in keys)
            {
                VillageDrugEntry entry = _villages[key];
                // [88차] 스킵 규약 확장 — pending 적립금이 있으면 재고가 비어도 수출 대상
                //        (판매금 받기 전 세이브/로드에서 적립금이 유실되는 것을 방지).
                if ((entry.stock == null || entry.stock.Count == 0)
                    && entry.pendingDrugGold == 0 && entry.pendingNormalGold == 0
                    && entry.normalRegisteredPrice == 0) // [100차] 등록가 단독 마을도 유실 방지 수출([88차] 규약 확장 준용)
                    continue; // 재고와 적립금이 모두 없는 마을 스킵 (용량 절감 규약)

                var save = new VillageDrugSaveEntry
                {
                    nation = key.nation,
                    villageIndex = key.villageIndex,
                    restaurantBuilt = entry.restaurantBuilt,
                    assignedGuardId = entry.assignedGuardId ?? string.Empty,
                    addiction = entry.addiction,
                    registeredPrice = entry.registeredPrice,
                    normalRegisteredPrice = entry.normalRegisteredPrice, // [100차] 1:1 수출
                    departedVillagers = entry.departedVillagers,
                    pendingDrugGold = entry.pendingDrugGold,   // [88차] 1:1 수출
                    pendingNormalGold = entry.pendingNormalGold, // [88차] 1:1 수출
                    stock = new List<VillageDrugStockSaveEntry>(),
                };

                // 복사 기입 — 내부 슬롯(struct) 참조 노출 없이 스냅샷 고정
                foreach (RestaurantStockItem item in entry.stock)
                {
                    save.stock.Add(new VillageDrugStockSaveEntry
                    {
                        foodItemId = item.foodItemId,
                        displayName = item.displayName,
                        count = item.count,
                        isDrugged = item.isDrugged,
                        drugStage = item.drugStage,
                        basePrice = item.basePrice,
                        placedTotal = item.placedTotal, // [88차] 1:1 수출
                        soldTotal = item.soldTotal,     // [88차] 1:1 수출
                    });
                }
                list.Add(save);
            }
            return list;
        }

        /// <summary>
        /// 세이브 복원용 — 기존 상태를 전면 클리어(ResetAll 재사용)한 뒤 수출 데이터를 대입한다.
        /// null(구버전 세이브 — 필드 부재)이면 클리어만 수행한다. 대입은 필드 직접 기입(병합 없음)으로
        /// 수출 시점 상태를 그대로 복원한다.
        /// SaveManager.Load 순서 규약: 본 메서드가 ApplyConstructionData(음식점 실물 복원 훅)보다
        /// 먼저 호출되어야 한다 — 클리어가 실물 복원이 세팅하는 restaurantBuilt 플래그를 지우지 않도록.
        /// </summary>
        public static void ImportSaveEntries(List<VillageDrugSaveEntry> entries)
        {
            ResetAll(); // 기존 상태 전면 클리어 — 기존 "테스트 전용" 계약을 세이브 복원 경로가 재사용
            if (entries == null)
                return;

            foreach (VillageDrugSaveEntry save in entries)
            {
                if (save == null)
                    continue; // 방어 — 직렬화 결과에 null 항목은 없지만 세이브 파일 변형에 대비

                VillageDrugEntry entry = GetOrCreate((NationType)save.nation, save.villageIndex);
                entry.restaurantBuilt = save.restaurantBuilt;
                entry.assignedGuardId = string.IsNullOrEmpty(save.assignedGuardId)
                    ? string.Empty
                    : save.assignedGuardId; // 규약: 미배치는 null이 아닌 빈 문자열
                entry.addiction = Mathf.Clamp(save.addiction, 0f, 100f); // 방어 클램프(수출값은 이미 유효)
                entry.registeredPrice = save.registeredPrice;
                entry.normalRegisteredPrice = Mathf.Max(save.normalRegisteredPrice, 0); // [100차] 구세이브 누락 → 0 호환(음수 방어)
                entry.departedVillagers = save.departedVillagers;
                // [88차] pending 적립금 복원 — 구세이브(필드 누락)는 JsonUtility 기본값 0으로 호환
                entry.pendingDrugGold = Mathf.Max(save.pendingDrugGold, 0);
                entry.pendingNormalGold = Mathf.Max(save.pendingNormalGold, 0);

                // 재고 병합 없이 대입 — 로드는 "수출 시점 상태의 그대로 복원"이 원칙
                entry.stock = new List<RestaurantStockItem>();
                if (save.stock == null)
                    continue;
                foreach (VillageDrugStockSaveEntry slot in save.stock)
                {
                    if (slot == null)
                        continue;
                    entry.stock.Add(new RestaurantStockItem
                    {
                        foodItemId = slot.foodItemId,
                        displayName = slot.displayName,
                        count = slot.count,
                        isDrugged = slot.isDrugged,
                        drugStage = slot.drugStage,
                        basePrice = slot.basePrice,
                        placedTotal = Mathf.Max(slot.placedTotal, 0), // [88차] 1:1 복원(음수 방어)
                        soldTotal = Mathf.Max(slot.soldTotal, 0),     // [88차] 1:1 복원(음수 방어)
                    });
                }
            }
        }

        // ===== 테스트 전용 =====

        /// <summary>
        /// 전체 마을 상태 초기화 — EditMode 테스트 독립성 확보 전용.
        /// [D5-A] 세이브 복원(ImportSaveEntries)의 전면 클리어 용도로도 재사용한다.
        /// 그 외 런타임(Systems/UI)에서 호출 금지.
        /// </summary>
        public static void ResetAll()
        {
            _villages.Clear();
        }

        // ===== 내부 헬퍼 =====

        /// <summary>결정론 사전 키 — (국가 enum 정수, 마을 인덱스). 튜플 동등성으로 비교된다.</summary>
        private static (int nation, int villageIndex) Key(NationType nation, int villageIndex)
        {
            return ((int)nation, villageIndex);
        }

        /// <summary>상태 엔트리 취득(없으면 생성) — 쓰기 경로용.</summary>
        private static VillageDrugEntry GetOrCreate(NationType nation, int villageIndex)
        {
            (int, int) key = Key(nation, villageIndex);
            if (!_villages.TryGetValue(key, out VillageDrugEntry entry))
            {
                entry = new VillageDrugEntry();
                _villages[key] = entry;
            }
            return entry;
        }

        /// <summary>미접촉 마을 기본 스냅샷 — assignedGuardId는 null이 아닌 빈 문자열(규약: 빈문자열=미배치).</summary>
        private static VillageDrugSnapshot DefaultSnapshot()
        {
            return new VillageDrugSnapshot { assignedGuardId = string.Empty };
        }
    }
}
