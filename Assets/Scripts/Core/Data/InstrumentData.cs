using ProjectName.Core;
#pragma warning disable 0414

namespace ProjectName.Core.Data
{
    /// <summary>
    /// Phase O7: 바드 악기 프로필 1건 (불변 struct).
    /// BardMercenary 기본 버프(공+15/방+10/속+10 = 류트 기준)를 악기별로 치우침을 준 확장.
    /// </summary>
    public struct InstrumentProfile
    {
        public string itemId;             // 예: "instrument_bard_lute"
        public string displayName;        // 예: "바드의 류트"
        public float attackBuffPercent;   // 공격력 버프 %
        public float defenseBuffPercent;  // 방어력 버프 %
        public float speedBuffPercent;    // 이동속도 버프 %
        public string flavorText;         // 아이템 설문
    }

    /// <summary>
    /// Phase O7: 바드 악기 데이터베이스 (정적, 총 5종).
    /// - Core는 데이터만 제공: 버프 적용은 BardMercenary/InstrumentPerformanceSystem(Systems)이 수행
    /// - ToItemData로 PlayerInventory.ItemData 변환 → id에 "instrument" 포함 →
    ///   GuardEquipmentSystem.IsItemValidForSlot Instrument 슬롯 통과 (바드 전용 장착)
    /// </summary>
    public static class InstrumentData
    {
        /// <summary>5종 악기 프로필 (등록 순서 = 상점 진열 순서 기준).</summary>
        public static readonly InstrumentProfile[] All =
        {
            new InstrumentProfile
            {
                itemId = "instrument_bard_lute", displayName = "바드의 류트",
                attackBuffPercent = 15f, defenseBuffPercent = 10f, speedBuffPercent = 10f,
                flavorText = "익숙한 류트. 버프 밸런스형."
            },
            new InstrumentProfile
            {
                itemId = "instrument_flute_war", displayName = "전쟁 피리",
                attackBuffPercent = 20f, defenseBuffPercent = 5f, speedBuffPercent = 15f,
                flavorText = "아군 사기를 돋우는 날카로운 음색."
            },
            new InstrumentProfile
            {
                itemId = "instrument_drum_march", displayName = "행군 드럼",
                attackBuffPercent = 10f, defenseBuffPercent = 15f, speedBuffPercent = 20f,
                flavorText = "박자에 맞춰 발이 빨라진다."
            },
            new InstrumentProfile
            {
                itemId = "instrument_harp_joy", displayName = "환희의 하프",
                attackBuffPercent = 10f, defenseBuffPercent = 10f, speedBuffPercent = 10f,
                flavorText = "정적인 평온을 주는 하프."
            },
            new InstrumentProfile
            {
                itemId = "instrument_horn_charge", displayName = "돌격 나팔",
                attackBuffPercent = 25f, defenseBuffPercent = 5f, speedBuffPercent = 5f,
                flavorText = "돌격 신호 — 공격 특화."
            },
        };

        /// <summary>등록된 악기 종류 수.</summary>
        public static int Count => 5;

        /// <summary>itemId로 프로필 조회 — 미등록이면 null. Nullable struct (HasValue/Value로 접근).</summary>
        public static InstrumentProfile? GetProfile(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            foreach (var p in All)
            {
                if (p.itemId == itemId) return p;
            }
            return null;
        }

        /// <summary>basePrice (상점 배율 적용 전 기본가 — 재료 등급 Uncommon 기준 150G).</summary>
        public const int BasePrice = 150;

        /// <summary>프로필 → 상점/장비용 ItemData 변환. id에 "instrument" 포함 → Instrument 슬롯 통과.</summary>
        public static ProjectName.Core.PlayerInventory.ItemData ToItemData(InstrumentProfile p)
        {
            return new ProjectName.Core.PlayerInventory.ItemData
            {
                id = p.itemId,
                displayName = p.displayName,
                description = p.flavorText,
                category = ProjectName.Core.PlayerInventory.ItemCategory.Material, // ItemCategory에 악기 전용 항목 없음 — Material 사용, id/displayName으로 식별
                rarity = ItemRarity.Uncommon,
                maxStack = 1,
                basePrice = BasePrice
            };
        }

        /// <summary>5종 전체를 ItemData로 변환 (상점 재고/드랍 테이블 등 일괄 사용).</summary>
        public static ProjectName.Core.PlayerInventory.ItemData[] AllItemData
        {
            get
            {
                var items = new System.Collections.Generic.List<ProjectName.Core.PlayerInventory.ItemData>();
                foreach (var p in All)
                    items.Add(ToItemData(p));
                return items.ToArray();
            }
        }
    }
}
