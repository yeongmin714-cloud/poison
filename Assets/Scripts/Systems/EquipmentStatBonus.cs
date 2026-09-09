using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 장비 → 스탯 보너스 적용기 (2026-09-09, 계획서 P3).
    ///
    /// [역할]
    /// - EquipmentManager의 6슬롯(Helmet/Armor/Weapon/Shoes/Gloves/Back)에 장착된 itemId를
    ///   정적 보너스 테이블 + 키워드 폴백으로 합산해 PlayerStats.SetEquipmentBonuses()로 푸시.
    /// - Core→Systems 역참조 방지를 위해 "풀(pull)"이 아닌 "푸시(push)" 구조:
    ///   OnEquipmentChanged 이벤트 때마다 재계산 → PlayerStats.Final*이 로컬 필드를 합산.
    ///
    /// [설계 규약]
    /// - EquipmentManager/WeaponEquipManager는 타 소유 — 본 파일은 읽기만 수행(수정 금지).
    /// - 무기 핫바(1~4키, WeaponEquipManager.Equip)는 EquipmentManager.Weapon 슬롯과 별계.
    ///   스탯 반영은 EquipmentManager 슬롯 기준(장비창 착용분) — 핫바 무기 전투력은 무기 시스템 자체 데미지로 반영됨.
    /// </summary>
    public class EquipmentStatBonusApplier : MonoBehaviour
    {
        private static EquipmentStatBonusApplier _instance;

        /// <summary>씬 편집 없이 상시 적용기 부트 (HotbarUI 선례).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var existing = Object.FindFirstObjectByType<EquipmentStatBonusApplier>();
            if (existing != null) { _instance = existing; return; }

            var go = new GameObject("EquipmentStatBonusApplier");
            _instance = go.AddComponent<EquipmentStatBonusApplier>();
        }

        private EquipmentManager _subscribed;

        private void OnDestroy()
        {
            if (_subscribed != null)
            {
                _subscribed.OnEquipmentChanged -= OnEquipmentChanged;
                _subscribed = null;
            }
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            // 씬 전환 대비 재구독 (EquipmentManager가 씬 소속일 수 있음)
            var em = EquipmentManager.Instance;
            if (ReferenceEquals(_subscribed, em)) return;
            if (_subscribed != null) _subscribed.OnEquipmentChanged -= OnEquipmentChanged;
            _subscribed = em;
            if (_subscribed != null)
            {
                _subscribed.OnEquipmentChanged += OnEquipmentChanged;
                ApplyNow(); // 재구독 시점에도 현재 장착분 반영
            }
        }

        private void OnEquipmentChanged(EquipmentManager.EquipmentSlot slot, string itemId) => ApplyNow();

        private void ApplyNow()
        {
            var stats = ProjectName.Core.PlayerStats.Instance;
            if (stats == null) return;
            stats.SetEquipmentBonuses(Total(b => b.attack), Total(b => b.defense), Total(b => b.crit), Total(b => b.speed));
        }

        // ── 보너스 테이블 ──

        public struct Bonus
        {
            public float attack;
            public float defense;
            public float crit;   // 0~1 확률 가산
            public float speed;
        }

        // 알려진 itemId 정확 매칭 테이블 (DropTable/무기 스펙 기준)
        private static readonly Dictionary<string, Bonus> _table = new Dictionary<string, Bonus>
        {
            { "steel_sword",  new Bonus { attack = 5f } },
            { "iron_sword",   new Bonus { attack = 4f } },
            { "spear",        new Bonus { attack = 3f } },
            { "war_axe",      new Bonus { attack = 6f } },
            { "mace",         new Bonus { attack = 5f } },
            { "crystal_bow",  new Bonus { attack = 4f, crit = 0.02f } },
            { "wood_bow",     new Bonus { attack = 3f, crit = 0.01f } },
            { "wood_spear",   new Bonus { attack = 3f } },
        };

        /// <summary>테이블에 없는 id는 키워드 폴백 (방어구/무기 계열 추론).</summary>
        private static Bonus FromKeyword(string id)
        {
            var b = new Bonus();
            if (string.IsNullOrEmpty(id)) return b;
            string s = id.ToLowerInvariant();

            if (s.Contains("helmet") || s.Contains("투구")) b.defense += 3f;
            if (s.Contains("armor") || s.Contains("갑옷") || s.Contains("chest")) b.defense += 5f;
            if (s.Contains("glove") || s.Contains("장갑")) b.defense += 2f;
            if (s.Contains("shoe") || s.Contains("boot") || s.Contains("신발")) b.speed += 0.2f;
            if (s.Contains("cape") || s.Contains("망토") || s.EndsWith("_back")) b.defense += 2f;
            if (s.Contains("sword") || s.Contains("검")) b.attack += 5f;
            if (s.Contains("bow") || s.Contains("활")) { b.attack += 4f; b.crit += 0.02f; }
            if (s.Contains("spear") || s.Contains("창")) b.attack += 3f;
            if (s.Contains("axe") || s.Contains("도끼")) b.attack += 6f;
            return b;
        }

        private static Bonus GetBonusFor(string itemId)
            => string.IsNullOrEmpty(itemId) ? default : (_table.TryGetValue(itemId, out var b) ? b : FromKeyword(itemId));

        // ── 집계 API (UI 표시용 — PlayerStats 파생 스탯은 푸시 방식으로 반영됨) ──

        public static float GetAttackBonus() => Total(b => b.attack);
        public static float GetDefenseBonus() => Total(b => b.defense);
        public static float GetCritBonus() => Total(b => b.crit);
        public static float GetSpeedBonus() => Total(b => b.speed);

        private delegate float Selector(Bonus b);

        private static float Total(Selector sel)
        {
            var em = EquipmentManager.Instance;
            if (em == null) return 0f;

            float total = 0f;
            var slots = em.GetAllSlots();
            if (slots == null) return 0f;
            foreach (var slot in slots)
            {
                if (slot == null || string.IsNullOrEmpty(slot.itemId)) continue;
                total += sel(GetBonusFor(slot.itemId));
            }
            return total;
        }

        // ── UI 표시용 (스탯창 장비 보너스 내역) ──

        /// <summary>현재 착용 장비의 보너스 요약 라인 ("강철검 공격+5" 등). 착용분 없으면 빈 리스트.</summary>
        public static List<string> GetActiveBonusLabels()
        {
            var labels = new List<string>();
            var em = EquipmentManager.Instance;
            if (em == null) return labels;

            var slots = em.GetAllSlots();
            if (slots == null) return labels;
            foreach (var slot in slots)
            {
                if (slot == null || string.IsNullOrEmpty(slot.itemId)) continue;
                Bonus b = GetBonusFor(slot.itemId);
                string parts = "";
                if (b.attack > 0f) parts += $" 공격+{b.attack:F0}";
                if (b.defense > 0f) parts += $" 방어+{b.defense:F0}";
                if (b.crit > 0f) parts += $" 치명+{b.crit * 100f:F0}%";
                if (b.speed > 0f) parts += $" 속도+{b.speed:F1}";
                if (parts.Length > 0)
                    labels.Add($"{DisplayName(slot.itemId)}:{parts}");
            }
            return labels;
        }

        /// <summary>itemId → 한글 표기 (스탯창 장비슬롯/보너스 라벨 공용).</summary>
        public static string DisplayName(string id)
        {
            switch (id)
            {
                case "steel_sword": return "강철검";
                case "iron_sword": return "철검";
                case "crystal_bow": return "수정활";
                case "wood_bow": return "나무활";
                case "wood_spear": return "나무창";
                case "spear": return "창";
                case "war_axe": return "전투도끼";
                case "mace": return "둔기";
                default: return id;
            }
        }
    }
}
