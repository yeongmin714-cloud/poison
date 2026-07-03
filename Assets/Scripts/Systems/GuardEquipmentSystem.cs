using System.Collections.Generic;
using ProjectName.Core;
using UnityEngine;
using ProjectName.Core.Data;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 26: 병사/용병 장비 할당 시스템 (싱글톤).
    /// 
    /// 장비 슬롯: 무기(Weapon), 방어구(Armor), 액세서리(Accessory)
    /// 바드 전용: 악기(Instrument) 슬롯 추가
    /// 전설 용병 전용: 유니크 아이템 (RestrictToMercenaryId로 제한)
    /// 
    /// 장착 즉시 능력치 반영, 사망 시 인벤토리 반환, 내구도 연동.
    /// </summary>
    public class GuardEquipmentSystem : MonoBehaviour
    {
        public static GuardEquipmentSystem Instance { get; private set; }

        // ===== 장비 슬롯 정의 =====
        public enum EquipSlot
        {
            Weapon,       // 무기
            Armor,        // 방어구
            Accessory,    // 액세서리
            Instrument    // 악기 (바드 전용)
        }

        public static readonly EquipSlot[] BaseSlots = new[] { EquipSlot.Weapon, EquipSlot.Armor, EquipSlot.Accessory };
        public static readonly EquipSlot[] BardSlots = new[] { EquipSlot.Weapon, EquipSlot.Armor, EquipSlot.Accessory, EquipSlot.Instrument };

        // ===== 장비 데이터 래퍼 =====
        /// <summary>
        /// 장착된 장비 항목. 내구도와 장착 시간을 추적합니다.
        /// </summary>
        [System.Serializable]
        public class EquippedItem
        {
            public PlayerInventory.ItemData itemData;
            public int currentDurability;

            public EquippedItem(PlayerInventory.ItemData item)
            {
                itemData = item;
                currentDurability = item.maxDurability > 0 ? item.maxDurability : 9999;
            }

            /// <summary>내구도 비율 (0~1)</summary>
            public float DurabilityRatio => itemData.maxDurability > 0
                ? Mathf.Clamp01((float)currentDurability / itemData.maxDurability)
                : 1f;

            /// <summary>내구도가 0 이하인가 (파괴됨)</summary>
            public bool IsBroken => itemData.maxDurability > 0 && currentDurability <= 0;
        }

        // ===== 장비 제약 데이터 =====
        /// <summary>
        /// 특정 용병만 장착할 수 있는 유니크 아이템을 위한 메타데이터.
        /// PlayerInventory.ItemData.effects 필드에 JSON 또는 특수 문자열로 저장.
        /// </summary>
        public static class UniqueItemConstraint
        {
            /// <summary>이 아이템을 장착할 수 있는 용병 ID (null/empty이면 제약 없음)</summary>
            public const string RESTRICT_TO_MERC_ID = "restrict_merc_id:";

            /// <summary>이 아이템을 장착하려면 필요한 최소 등급</summary>
            public const string MIN_GRADE = "min_grade:";

            /// <summary>장착 시 추가 능력치 (JSON 형식)</summary>
            public const string STAT_BONUS = "stat_bonus:";

            /// <summary>아이템 effects 문자열에서 용병 제한 파싱</summary>
            public static string ParseRestrictMercId(string effects)
            {
                if (string.IsNullOrEmpty(effects)) return null;
                foreach (var part in effects.Split(','))
                {
                    var trimmed = part.Trim();
                    if (trimmed.StartsWith(RESTRICT_TO_MERC_ID))
                        return trimmed.Substring(RESTRICT_TO_MERC_ID.Length).Trim();
                }
                return null;
            }

            /// <summary>아이템 effects 문자열에서 최소 등급 파싱</summary>
            public static MercenaryGrade? ParseMinGrade(string effects)
            {
                if (string.IsNullOrEmpty(effects)) return null;
                foreach (var part in effects.Split(','))
                {
                    var trimmed = part.Trim();
                    if (trimmed.StartsWith(MIN_GRADE))
                    {
                        var gradeStr = trimmed.Substring(MIN_GRADE.Length).Trim();
                        if (System.Enum.TryParse<MercenaryGrade>(gradeStr, out var grade))
                            return grade;
                    }
                }
                return null;
            }
        }

        // ===== 내부 데이터 =====
        // 병사 장비: GuardPlaceholder instance ID → Dictionary<EquipSlot, EquippedItem>
        private Dictionary<int, Dictionary<EquipSlot, EquippedItem>> _guardEquipment =
            new Dictionary<int, Dictionary<EquipSlot, EquippedItem>>();

        // 용병 장비: mercenaryId → Dictionary<EquipSlot, EquippedItem>
        private Dictionary<string, Dictionary<EquipSlot, EquippedItem>> _mercenaryEquipment =
            new Dictionary<string, Dictionary<EquipSlot, EquippedItem>>();

        // 전설 용병 유니크 아이템 정의: 아이템 ID → (용병 ID, 설명)
        private Dictionary<string, LegendaryUniqueItem> _legendaryUniqueItems =
            new Dictionary<string, LegendaryUniqueItem>();

        [System.Serializable]
        public struct LegendaryUniqueItem
        {
            public string mercenaryId;
            public string description;
            public PlayerInventory.ItemData itemData;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeLegendaryUniqueItems();
        }

        /// <summary>
        /// 전설 용병 전용 유니크 아이템 초기화.
        /// </summary>
        private void InitializeLegendaryUniqueItems()
        {
            // 아라곤 왕자 전용: 왕가의 검
            var princeSword = new PlayerInventory.ItemData
            {
                id = "unique_prince_sword",
                displayName = "왕가의 검",
                description = "아라곤 왕조의 상징. 오직 아라곤 왕자만이 휘둘를 수 있다.",
                category = PlayerInventory.ItemCategory.Weapon,
                rarity = ItemRarity.Legendary,
                maxStack = 1,
                maxDurability = 200,
                effects = "restrict_merc_id:merc_legend_01, stat_bonus:{\"attack\":30,\"defense\":15}"
            };
            RegisterLegendaryUniqueItem("unique_prince_sword", "merc_legend_01", princeSword);

            // 얼음 마녀 전용: 서리 지팡이
            var frostStaff = new PlayerInventory.ItemData
            {
                id = "unique_frost_staff",
                displayName = "서리 지팡이",
                description = "영원한 겨울의 정수가 담긴 지팡이. 얼음 마녀만이 다룰 수 있다.",
                category = PlayerInventory.ItemCategory.Weapon,
                rarity = ItemRarity.Legendary,
                maxStack = 1,
                maxDurability = 200,
                effects = "restrict_merc_id:merc_legend_02, stat_bonus:{\"attack\":40,\"defense\":10}"
            };
            RegisterLegendaryUniqueItem("unique_frost_staff", "merc_legend_02", frostStaff);

            // 리리엔 (바드) 전용: 전설의 류트
            var legendaryLute = new PlayerInventory.ItemData
            {
                id = "unique_bard_lute",
                displayName = "전설의 류트",
                description = "고대 바드의 영혼이 깃든 류트. 류트 연주자 리리엔만이 연주할 수 있다.",
                category = PlayerInventory.ItemCategory.Weapon,
                rarity = ItemRarity.Legendary,
                maxStack = 1,
                maxDurability = 150,
                effects = "restrict_merc_id:merc_bard_01, min_grade:Elite, stat_bonus:{\"attack\":5,\"defense\":20,\"buff_power\":25}"
            };
            RegisterLegendaryUniqueItem("unique_bard_lute", "merc_bard_01", legendaryLute);
        }

        /// <summary>전설 유니크 아이템 등록</summary>
        public void RegisterLegendaryUniqueItem(string itemId, string mercenaryId, PlayerInventory.ItemData itemData)
        {
            _legendaryUniqueItems[itemId] = new LegendaryUniqueItem
            {
                mercenaryId = mercenaryId,
                description = itemData.description,
                itemData = itemData
            };
        }

        /// <summary>유니크 아이템 데이터 조회</summary>
        public LegendaryUniqueItem? GetLegendaryUniqueItem(string itemId)
        {
            if (_legendaryUniqueItems.TryGetValue(itemId, out var item))
                return item;
            return null;
        }

        /// <summary>모든 유니크 아이템 ID 반환</summary>
        public string[] GetAllUniqueItemIds()
        {
            var keys = new string[_legendaryUniqueItems.Count];
            _legendaryUniqueItems.Keys.CopyTo(keys, 0);
            return keys;
        }

        // ===== 병사 장비 API =====

        /// <summary>병사에게 아이템 장착. 성공 시 true.</summary>
        public bool EquipGuard(GuardPlaceholder guard, EquipSlot slot, PlayerInventory.ItemData item)
        {
            if (guard == null || item == null) return false;

            // 유니크 아이템 제약 확인 (병사는 유니크 장착 불가 — 용병 전용)
            string restrictMercId = UniqueItemConstraint.ParseRestrictMercId(item.effects);
            if (!string.IsNullOrEmpty(restrictMercId))
            {
                Debug.LogWarning($"[GuardEquipment] 유니크 아이템 {item.displayName}은(는) 용병 전용입니다.");
                return false;
            }

            // 슬롯 적합성 확인
            if (!IsItemValidForSlot(slot, item)) return false;

            int guardId = guard.GetHashCode();

            if (!_guardEquipment.ContainsKey(guardId))
                _guardEquipment[guardId] = new Dictionary<EquipSlot, EquippedItem>();

            // 기존 장비 해제
            if (_guardEquipment[guardId].ContainsKey(slot))
                ReturnToInventory(_guardEquipment[guardId][slot]);

            // 새 장비 장착
            var equipped = new EquippedItem(item);
            _guardEquipment[guardId][slot] = equipped;

            // 인벤토리에서 제거
            if (PlayerInventory.Instance != null)
                PlayerInventory.Instance.RemoveItem(item.id);

            Debug.Log($"[GuardEquipment] {guard.GuardName} {slot} 장착: {item.displayName}");
            return true;
        }

        /// <summary>병사 장비 해제. 해제된 아이템 반환.</summary>
        public EquippedItem UnequipGuard(GuardPlaceholder guard, EquipSlot slot)
        {
            if (guard == null) return null;

            int guardId = guard.GetHashCode();
            if (!_guardEquipment.ContainsKey(guardId) || !_guardEquipment[guardId].ContainsKey(slot))
                return null;

            var item = _guardEquipment[guardId][slot];
            _guardEquipment[guardId].Remove(slot);

            ReturnToInventory(item);
            Debug.Log($"[GuardEquipment] {guard.GuardName} {slot} 해제: {item.itemData.displayName}");
            return item;
        }

        /// <summary>병사의 특정 슬롯 장비 조회</summary>
        public EquippedItem GetGuardEquipped(GuardPlaceholder guard, EquipSlot slot)
        {
            if (guard == null) return null;
            int guardId = guard.GetHashCode();
            if (_guardEquipment.TryGetValue(guardId, out var slots) && slots.TryGetValue(slot, out var item))
                return item;
            return null;
        }

        /// <summary>병사의 모든 장비 조회</summary>
        public Dictionary<EquipSlot, EquippedItem> GetAllGuardEquipment(GuardPlaceholder guard)
        {
            if (guard == null) return null;
            int guardId = guard.GetHashCode();
            if (_guardEquipment.TryGetValue(guardId, out var slots))
                return slots;
            return new Dictionary<EquipSlot, EquippedItem>();
        }

        // ===== 용병 장비 API =====

        /// <summary>용병에게 아이템 장착. 성공 시 true.</summary>
        public bool EquipMercenary(string mercenaryId, EquipSlot slot, PlayerInventory.ItemData item)
        {
            if (string.IsNullOrEmpty(mercenaryId) || item == null) return false;

            // 유니크 아이템 제약 확인
            string restrictMercId = UniqueItemConstraint.ParseRestrictMercId(item.effects);
            if (!string.IsNullOrEmpty(restrictMercId) && restrictMercId != mercenaryId)
            {
                Debug.LogWarning($"[GuardEquipment] {item.displayName}은(는) {restrictMercId}만 장착 가능!");
                return false;
            }

            // 최소 등급 확인 + 슬롯 적합성 확인 (바드 직업은 악기 슬롯 가능)
            var mercData = MercenaryManager.Instance?.GetMercenaryData(mercenaryId);
            bool isBard = mercData.HasValue && mercData.Value.jobType == "Bard";

            var minGrade = UniqueItemConstraint.ParseMinGrade(item.effects);
            if (minGrade.HasValue)
            {
                if (mercData.HasValue && mercData.Value.grade < minGrade.Value)
                {
                    Debug.LogWarning($"[GuardEquipment] {item.displayName}은(는) {minGrade} 이상 등급 필요!");
                    return false;
                }
            }

            if (!IsItemValidForSlot(slot, item, isBard)) return false;

            // (IsItemValidForSlot에서 Instrument 슬롯은 isBard=true 때만 허용하므로 별도 차단 불필요)

            if (!_mercenaryEquipment.ContainsKey(mercenaryId))
                _mercenaryEquipment[mercenaryId] = new Dictionary<EquipSlot, EquippedItem>();

            // 기존 장비 해제
            if (_mercenaryEquipment[mercenaryId].ContainsKey(slot))
                ReturnToInventory(_mercenaryEquipment[mercenaryId][slot]);

            // 새 장비 장착
            var equipped = new EquippedItem(item);
            _mercenaryEquipment[mercenaryId][slot] = equipped;

            // 인벤토리에서 제거
            if (PlayerInventory.Instance != null)
                PlayerInventory.Instance.RemoveItem(item.id);

            Debug.Log($"[GuardEquipment] 용병 {mercenaryId} {slot} 장착: {item.displayName}");
            return true;
        }

        /// <summary>용병 장비 해제</summary>
        public EquippedItem UnequipMercenary(string mercenaryId, EquipSlot slot)
        {
            if (string.IsNullOrEmpty(mercenaryId)) return null;

            if (!_mercenaryEquipment.ContainsKey(mercenaryId) || !_mercenaryEquipment[mercenaryId].ContainsKey(slot))
                return null;

            var item = _mercenaryEquipment[mercenaryId][slot];
            _mercenaryEquipment[mercenaryId].Remove(slot);

            ReturnToInventory(item);
            Debug.Log($"[GuardEquipment] 용병 {mercenaryId} {slot} 해제: {item.itemData.displayName}");
            return item;
        }

        /// <summary>용병의 특정 슬롯 장비 조회</summary>
        public EquippedItem GetMercenaryEquipped(string mercenaryId, EquipSlot slot)
        {
            if (string.IsNullOrEmpty(mercenaryId)) return null;
            if (_mercenaryEquipment.TryGetValue(mercenaryId, out var slots) && slots.TryGetValue(slot, out var item))
                return item;
            return null;
        }

        /// <summary>용병의 모든 장비 조회</summary>
        public Dictionary<EquipSlot, EquippedItem> GetAllMercenaryEquipment(string mercenaryId)
        {
            if (string.IsNullOrEmpty(mercenaryId)) return null;
            if (_mercenaryEquipment.TryGetValue(mercenaryId, out var slots))
                return slots;
            return new Dictionary<EquipSlot, EquippedItem>();
        }

        // ===== 사망 시 처리 =====

        /// <summary>병사 사망 시 모든 장비 인벤토리 반환</summary>
        public void OnGuardDeath(GuardPlaceholder guard)
        {
            if (guard == null) return;
            int guardId = guard.GetHashCode();

            if (_guardEquipment.TryGetValue(guardId, out var slots))
            {
                var itemsToReturn = new List<EquippedItem>(slots.Values);
                slots.Clear();

                foreach (var item in itemsToReturn)
                {
                    ReturnToInventory(item);
                }

                Debug.Log($"[GuardEquipment] {guard.GuardName} 사망 — 장비 {itemsToReturn.Count}개 반환");
            }

            _guardEquipment.Remove(guardId);
        }

        /// <summary>용병 사망 시 모든 장비 인벤토리 반환</summary>
        public void OnMercenaryDeath(string mercenaryId)
        {
            if (string.IsNullOrEmpty(mercenaryId)) return;

            if (_mercenaryEquipment.TryGetValue(mercenaryId, out var slots))
            {
                var itemsToReturn = new List<EquippedItem>(slots.Values);
                slots.Clear();

                foreach (var item in itemsToReturn)
                {
                    ReturnToInventory(item);
                }

                Debug.Log($"[GuardEquipment] 용병 {mercenaryId} 사망 — 장비 {itemsToReturn.Count}개 반환");
            }

            _mercenaryEquipment.Remove(mercenaryId);
        }

        // ===== 능력치 계산 =====

        /// <summary>병사의 장비로 인한 추가 공격력 계산</summary>
        public float GetGuardEquipmentAttackBonus(GuardPlaceholder guard)
        {
            if (guard == null) return 0f;
            return CalculateEquipmentStatBonus(GetAllGuardEquipment(guard), "attack");
        }

        /// <summary>병사의 장비로 인한 추가 방어력 계산</summary>
        public float GetGuardEquipmentDefenseBonus(GuardPlaceholder guard)
        {
            if (guard == null) return 0f;
            return CalculateEquipmentStatBonus(GetAllGuardEquipment(guard), "defense");
        }

        /// <summary>용병의 장비로 인한 추가 공격력 계산</summary>
        public float GetMercenaryEquipmentAttackBonus(string mercenaryId)
        {
            return CalculateEquipmentStatBonus(GetAllMercenaryEquipment(mercenaryId), "attack");
        }

        /// <summary>용병의 장비로 인한 추가 방어력 계산</summary>
        public float GetMercenaryEquipmentDefenseBonus(string mercenaryId)
        {
            return CalculateEquipmentStatBonus(GetAllMercenaryEquipment(mercenaryId), "defense");
        }

        /// <summary>용병의 장비로 인한 추가 이동속도 계산</summary>
        public float GetMercenaryEquipmentSpeedBonus(string mercenaryId)
        {
            return CalculateEquipmentStatBonus(GetAllMercenaryEquipment(mercenaryId), "speed");
        }

        /// <summary>용병 전투력 계산 (기본 스탯 + 장비 보너스)</summary>
        public float CalculateMercenaryCombatPower(string mercenaryId)
        {
            var merc = MercenaryManager.Instance?.GetHiredMercenary(mercenaryId);
            if (merc == null || merc.Value.data.id == null) return 0f;

            var data = merc.Value.data;
            float baseAtk = data.attack;
            float baseDef = data.defense;
            float baseHp = data.maxHP;

            float equipAtk = GetMercenaryEquipmentAttackBonus(mercenaryId);
            float equipDef = GetMercenaryEquipmentDefenseBonus(mercenaryId);

            float affinityBonus = merc.Value.AffinityBonus;

            float totalAtk = (baseAtk + equipAtk) * (1f + affinityBonus);
            float totalDef = (baseDef + equipDef) * (1f + affinityBonus);

            return totalAtk * 2f + totalDef * 1.5f + baseHp * 0.1f;
        }

        /// <summary>병사 전투력 계산</summary>
        public float CalculateGuardCombatPower(GuardPlaceholder guard)
        {
            if (guard == null) return 0f;

            float baseAtk = GuardLevelSystem.CalculateDamage(guard.Level);
            float baseDef = GuardLevelSystem.CalculateDefense(guard.Level);
            float baseHp = GuardLevelSystem.CalculateMaxHP(guard.Level);

            float equipAtk = GetGuardEquipmentAttackBonus(guard);
            float equipDef = GetGuardEquipmentDefenseBonus(guard);

            float totalAtk = baseAtk + equipAtk;
            float totalDef = baseDef + equipDef;

            return totalAtk * 2f + totalDef * 1.5f + baseHp * 0.1f;
        }

        // ===== 내구도 시스템 =====

        /// <summary>장비 내구도 감소. 파괴된 장비는 인벤토리 반환.</summary>
        public void ReduceDurability(GuardPlaceholder guard, EquipSlot slot, int amount = 1)
        {
            if (guard == null) return;
            int guardId = guard.GetHashCode();

            if (!_guardEquipment.TryGetValue(guardId, out var slots) || !slots.TryGetValue(slot, out var equipped))
                return;

            if (equipped.itemData.maxDurability <= 0) return; // 내구도 없음

            equipped.currentDurability -= amount;
            if (equipped.IsBroken)
            {
                Debug.Log($"[GuardEquipment] {guard.GuardName}의 {equipped.itemData.displayName} 파괴!");
                slots.Remove(slot);
                // 파괴된 장비는 인벤토리에 반환하지 않음 (파괴됨)
            }
        }

        /// <summary>용병 장비 내구도 감소</summary>
        public void ReduceMercenaryDurability(string mercenaryId, EquipSlot slot, int amount = 1)
        {
            if (string.IsNullOrEmpty(mercenaryId)) return;

            if (!_mercenaryEquipment.TryGetValue(mercenaryId, out var slots) || !slots.TryGetValue(slot, out var equipped))
                return;

            if (equipped.itemData.maxDurability <= 0) return;

            equipped.currentDurability -= amount;
            if (equipped.IsBroken)
            {
                Debug.Log($"[GuardEquipment] 용병 {mercenaryId}의 {equipped.itemData.displayName} 파괴!");
                slots.Remove(slot);
            }
        }

        // ===== 헬퍼 메서드 =====

        /// <summary>아이템이 특정 슬롯에 적합한지 확인</summary>
        public bool IsItemValidForSlot(EquipSlot slot, PlayerInventory.ItemData item, bool isBard = false)
        {
            if (item == null) return false;

            switch (slot)
            {
                case EquipSlot.Weapon:
                    return item.category == PlayerInventory.ItemCategory.Weapon
                        || item.category == PlayerInventory.ItemCategory.Tool; // 도구도 무기 슬롯 가능
                case EquipSlot.Armor:
                    return item.category == PlayerInventory.ItemCategory.Armor;
                case EquipSlot.Accessory:
                    // 액세서리: 액세서리 카테고리 또는 일반 재료/기타
                    return item.category == PlayerInventory.ItemCategory.Material
                        || item.category == PlayerInventory.ItemCategory.Quest
                        || item.category == PlayerInventory.ItemCategory.Potion;
                case EquipSlot.Instrument:
                    return isBard && (item.id.Contains("lute") || item.id.Contains("instrument")
                        || item.displayName.Contains("류트") || item.displayName.Contains("악기")
                        || item.id.Contains("bard") || item.id == "unique_bard_lute");
                default:
                    return false;
            }
        }

        /// <summary>장비 아이템에서 stat_bonus 문자열 파싱</summary>
        private static float CalculateEquipmentStatBonus(Dictionary<EquipSlot, EquippedItem> equipment, string statName)
        {
            if (equipment == null) return 0f;

            float total = 0f;
            foreach (var kvp in equipment)
            {
                var item = kvp.Value;
                if (item == null || item.itemData == null || item.IsBroken) continue;

                // 기본: 무기는 공격+5, 방어구는 방어+3, 액세서리는 능력치+1~3
                switch (kvp.Key)
                {
                    case EquipSlot.Weapon:
                        if (statName == "attack") total += 5f;
                        if (statName == "defense") total += 1f;
                        break;
                    case EquipSlot.Armor:
                        if (statName == "defense") total += 3f;
                        if (statName == "attack") total += 1f;
                        break;
                    case EquipSlot.Accessory:
                        if (statName == "attack") total += 2f;
                        if (statName == "defense") total += 2f;
                        if (statName == "speed") total += 0.5f;
                        break;
                    case EquipSlot.Instrument:
                        if (statName == "attack") total += 3f;
                        if (statName == "defense") total += 2f;
                        if (statName == "speed") total += 1f;
                        break;
                }

                // effects 문자열에서 stat_bonus 파싱 (추가 보너스)
                string effects = item.itemData.effects;
                if (!string.IsNullOrEmpty(effects) && effects.Contains("stat_bonus:"))
                {
                    foreach (var part in effects.Split(','))
                    {
                        var trimmed = part.Trim();
                        if (trimmed.StartsWith("stat_bonus:"))
                        {
                            string json = trimmed.Substring("stat_bonus:".Length);
                            // 간단한 키-값 파싱: {"attack":30,"defense":15}
                            var statParts = json.Trim('{', '}', ' ', '\n', '\r').Split(',');
                            foreach (var sp in statParts)
                            {
                                var kv = sp.Split(':');
                                if (kv.Length == 2)
                                {
                                    string key = kv[0].Trim(' ', '"', '\n', '\r');
                                    if (key == statName && float.TryParse(kv[1].Trim(' ', '"', '\n', '\r'), out float val))
                                    {
                                        total += val;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return total;
        }

        /// <summary>아이템을 인벤토리로 반환</summary>
        private void ReturnToInventory(EquippedItem item)
        {
            if (item == null || item.itemData == null) return;
            if (PlayerInventory.Instance != null)
            {
                PlayerInventory.Instance.AddItem(item.itemData, 1);
            }
        }

        /// <summary>모든 장비 데이터 초기화 (테스트용)</summary>
        public void ClearAllEquipment()
        {
            _guardEquipment.Clear();
            _mercenaryEquipment.Clear();
        }
    }
}