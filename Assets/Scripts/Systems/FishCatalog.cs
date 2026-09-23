using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 낚시 물고기 50종 카탈로그 (09-23 신규) — fish GLB 300종 중 결정론 선택 50종.
    ///
    /// 선택 규칙(결정론, 시드 20260924 — NaturalResourceSpawner.DetRng 동형 xorshift64*):
    ///   1) Assets/새로운 glb/crops-fish/ 의 fish-*.glb 300개를 이름순(ordinal) 정렬.
    ///   2) 이름형 5종(atlantic-salmon/catfish/clownfish/mackerel/olive-flounder)은 항상 포함.
    ///   3) 번호형 295종(f002~f300, 결번 다수)에서 균등 스트라이드 step=6, 시작 오프셋 4
    ///      (시드 유도값: DetRng(20260924) NextDouble()*6 → 4)로 45종 추출:
    ///      번호형 인덱스 4,10,16,...,268 → f006, f012, ..., f274.
    ///   산출값을 정적 배열로 고정 — 실행 시점 파일시스템 조회 없음(빌드 안전, 재현 가능).
    ///
    /// 등급 밴드(배열 인덱스 기반): Common [0..29] / Rare [30..44] / Legendary [45..49].
    /// FishingSystem.GetRandomFish의 기존 등급 롤(60/30/10 가중치 + 밤 2배/비 1.5배 보정)은
    /// 그대로 유지하고, 그 위에 밴드 내 종 선택만 얹는다. System.Random 미사용 —
    /// 인게임 종 추첨은 기존 낚시 시스템의 UnityEngine.Random 관례를 따른다(배치 결정론과 무관).
    /// </summary>
    public static class FishCatalog
    {
        /// <summary>낚시 드롭 등급 밴드 (기존 Fish_Common/Rare/Legendary 3티어 체계와 1:1 대응)</summary>
        public enum FishTier { Common, Rare, Legendary }

        const string GLB_DIR = "Assets/새로운 glb/crops-fish/";
        const string ID_PREFIX = "fish_glb_";   // 기존 fish_common/fish_rare/fish_legendary 와 충돌 없는 별도 접두어

        /// <summary>
        /// 결정론 선택된 50종 키(GLB 파일명에서 fish- 접두사/.glb 제거).
        /// 순서 계약: [0..29]=Common / [30..44]=Rare / [45..49]=Legendary.
        /// </summary>
        public static readonly string[] SpeciesKeys =
        {
            // === 이름형 5종 (항상 포함) — Common 밴드 ===
            "atlantic-salmon", "catfish", "clownfish", "mackerel", "olive-flounder",
            // === 번호형 45종 (stride=6, start=4, 시드 20260924) ===
            // Common 밴드 (25종)
            "f006", "f012", "f018", "f024", "f030", "f036", "f042", "f048",
            "f054", "f060", "f066", "f072", "f078", "f084", "f090", "f096",
            "f103", "f109", "f115", "f121", "f127", "f133", "f139", "f145", "f151",
            // Rare 밴드 (15종)
            "f157", "f163", "f170", "f176", "f182", "f188", "f194", "f200",
            "f206", "f212", "f218", "f224", "f231", "f237", "f243",
            // Legendary 밴드 (5종)
            "f249", "f255", "f262", "f268", "f274",
        };

        public const int TotalCount = 50;
        public const int CommonEnd = 30;   // Common: [0..29]
        public const int RareEnd = 45;     // Rare: [30..44], Legendary: [45..49]

        // 이름형 5종 한국어 표시명 (번호형은 "물고기 Fnnn" 형식)
        static readonly string[] NamedKoreanNames =
        {
            "대서양 연어", "메기", "흰동가리", "고등어", "광어",
        };

        static readonly PlayerInventory.ItemData[] _items = BuildItems();

        /// <summary>50종 전체 ItemData (정적 캐시 — id 스태킹은 PlayerInventory.AddItem이 처리)</summary>
        public static PlayerInventory.ItemData[] AllItems => _items;

        public static string SpeciesKey(int index) => SpeciesKeys[index];

        /// <summary>GLB 에셋 경로 (에디터 AssetDatabase 로드용 — NaturalResourceSpawner 선례)</summary>
        public static string GlbPath(int index) => GLB_DIR + "fish-" + SpeciesKeys[index] + ".glb";

        public static PlayerInventory.ItemData GetItem(int index) => _items[index];

        /// <summary>
        /// 등급 밴드 내에서 1종 추첨 (UnityEngine.Random — 인게임 드롭용, 기존 GetRandomFish 관례).
        /// 기존 3티어 아이템(Fish_Common/Rare/Legendary) 경로를 깨지 않고 그 위에 얹는 매핑.
        /// </summary>
        public static PlayerInventory.ItemData GetRandomFishItem(FishTier tier)
        {
            int lo, hi;
            switch (tier)
            {
                case FishTier.Legendary: lo = RareEnd;    hi = TotalCount;  break;
                case FishTier.Rare:      lo = CommonEnd;  hi = RareEnd;     break;
                default:                 lo = 0;          hi = CommonEnd;   break;
            }
            return _items[Random.Range(lo, hi)];
        }

        static PlayerInventory.ItemData[] BuildItems()
        {
            var items = new PlayerInventory.ItemData[TotalCount];
            for (int i = 0; i < TotalCount; i++)
            {
                string key = SpeciesKeys[i];
                bool isNamed = i < NamedKoreanNames.Length;
                string display = isNamed
                    ? "🐟 " + NamedKoreanNames[i]
                    : "🐟 물고기 " + key.ToUpperInvariant();   // f006 → F006

                FishTier tier = i < CommonEnd ? FishTier.Common
                              : i < RareEnd   ? FishTier.Rare
                              :                 FishTier.Legendary;

                items[i] = new PlayerInventory.ItemData
                {
                    id = ID_PREFIX + key,   // fish_glb_atlantic-salmon / fish_glb_f006 ...
                    displayName = display,
                    description = isNamed
                        ? "낚시로 잡은 " + NamedKoreanNames[i] + ". 신선하다."
                        : "낚시로 잡은 물고기. (개체 " + key.ToUpperInvariant() + ")",
                    category = PlayerInventory.ItemCategory.Material,
                    maxStack = 99,
                    rarity = tier == FishTier.Common ? ItemRarity.Common
                           : tier == FishTier.Rare   ? ItemRarity.Rare
                           :                           ItemRarity.Epic,
                };
            }
            return items;
        }
    }
}
