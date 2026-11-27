using System.Collections.Generic;

namespace ProjectName.Core.Data
{
    /// <summary>
    /// [113차] 영주 선호 음식 카탈로그 — 음식 계약 단일소스.
    /// 실측 배경: 선호 음식 10종(구운 고기·생선 스튜·버섯 수프·야채 샐러드·빵과 치즈·포도주·
    /// 꿀 케이크·로스트 치킨·양고기 스테이크·해산물 파이)은 인벤 아이템으로 미실장이며,
    /// 실장 음식은 고기(meat_rabbit/boar/wolf)·생선(fish_common/rare/legendary)·
    /// 작물/과일(crop_carrot/corn/pumpkin/radish, fruit_apple)이다.
    /// → "선호 음식 일치"는 요리명 정확 일치가 아니라 음식 계열(재료 소속) 일치로 판정한다.
    /// 요리 아이템이 실장되면 이 카탈로그에 정확 일치(id)로 확장한다.
    /// 씨앗(*_seed)은 재료가 아니므로 exact id 집합으로만 매칭(접두사 판정 금지).
    /// </summary>
    public static class LordFoodCatalog
    {
        /// <summary>음식 난이도 티어 — T1 쉬움 / T3 고급(재료 구하기 어려움).</summary>
        public const int Tier1 = 1;
        public const int Tier2 = 2;
        public const int Tier3 = 3;

        /// <summary>음식 계열 — 선물 매칭 시 지급 아이템이 소속해야 하는 재료 그룹(None=재료 미실장).</summary>
        private enum Affinity { None, Meat, Sea, Produce }

        /// <summary>선호 음식 1건의 계약(티어 + 계열 + 주재료).</summary>
        private readonly struct FoodEntry
        {
            public readonly int Tier;
            public readonly Affinity Affinity;
            public readonly string Ingredients;

            public FoodEntry(int tier, Affinity affinity, string ingredients)
            {
                Tier = tier;
                Affinity = affinity;
                Ingredients = ingredients;
            }
        }

        // 선호 음식 10종 계약 표 — Tier는 GenerateLordInfo 링 가중 슬롯 계약과 정합
        // (T1=구운 고기·야채 샐러드·빵과 치즈·버섯 수프 / T2=생선 스튜·로스트 치킨·포도주 / T3=꿀 케이크·양고기 스테이크·해산물 파이).
        // Affinity=None = 재료 미실장(선물 매칭 불가 — 조리/교역 확장 시 갱신).
        private static readonly Dictionary<string, FoodEntry> Foods = new Dictionary<string, FoodEntry>
        {
            { "구운 고기",       new FoodEntry(Tier1, Affinity.Meat,    "고기") },
            { "야채 샐러드",     new FoodEntry(Tier1, Affinity.Produce, "작물·과일") },
            { "빵과 치즈",       new FoodEntry(Tier1, Affinity.None,    "밀·우유") },
            { "버섯 수프",       new FoodEntry(Tier1, Affinity.None,    "버섯") },
            { "생선 스튜",       new FoodEntry(Tier2, Affinity.Sea,     "생선") },
            { "로스트 치킨",     new FoodEntry(Tier2, Affinity.Meat,    "가금 고기") },
            { "포도주",          new FoodEntry(Tier2, Affinity.None,    "포도") },
            { "꿀 케이크",       new FoodEntry(Tier3, Affinity.None,    "꿀·계란") },
            { "양고기 스테이크", new FoodEntry(Tier3, Affinity.Meat,    "양고기·향신료") },
            { "해산물 파이",     new FoodEntry(Tier3, Affinity.Sea,     "해산물·버터") },
        };

        // 계열별 지급 가능 아이템 id — exact 집합(접두사 판정 아님). 씨앗(*_seed) 오매칭 방지가 목적.
        private static readonly HashSet<string> MeatIds = new HashSet<string> { "meat_rabbit", "meat_boar", "meat_wolf" };
        private static readonly HashSet<string> SeaIds = new HashSet<string> { "fish_common", "fish_rare", "fish_legendary" };
        private static readonly HashSet<string> ProduceIds = new HashSet<string> { "crop_carrot", "crop_corn", "crop_pumpkin", "crop_radish", "fruit_apple" };

        /// <summary>선호 음식의 난이도 티어. 미등록 음식(붉은 포도주 등 특이 음식)은 최고급(T3) 폴백.</summary>
        public static int GetTier(string preferredFood)
        {
            return Foods.TryGetValue(preferredFood ?? string.Empty, out var entry) ? entry.Tier : Tier3;
        }

        /// <summary>
        /// 지급한 음식(foodItemId)이 영주의 선호 음식(preferredFood) 계열과 일치하는가.
        /// 재료 미실장 음식(None 계열)과 미등록 음식은 어떤 아이템으로도 일치 판정되지 않는다.
        /// </summary>
        public static bool IsPreferredGift(string preferredFood, string foodItemId)
        {
            if (string.IsNullOrEmpty(preferredFood) || string.IsNullOrEmpty(foodItemId)) return false;
            if (!Foods.TryGetValue(preferredFood, out var entry)) return false;
            switch (entry.Affinity)
            {
                case Affinity.Meat: return MeatIds.Contains(foodItemId);
                case Affinity.Sea: return SeaIds.Contains(foodItemId);
                case Affinity.Produce: return ProduceIds.Contains(foodItemId);
                default: return false;
            }
        }

        /// <summary>선호 음식의 재료가 현재 실장 아이템으로 조달 가능한가(None 계열=미실장).</summary>
        public static bool IsIngredientObtainable(string preferredFood)
        {
            return Foods.TryGetValue(preferredFood ?? string.Empty, out var entry) && entry.Affinity != Affinity.None;
        }

        /// <summary>선호 음식의 주재료 문자열(힌트/테스트용). 미등록 음식은 정직 폴백.</summary>
        public static string GetMainIngredients(string preferredFood)
        {
            return Foods.TryGetValue(preferredFood ?? string.Empty, out var entry)
                ? entry.Ingredients
                : "상세 재료 불명";
        }

        /// <summary>
        /// [113차 B] 인텔 재료 힌트 — 선호 음식의 주재료·획득처 문구 조립 (EnvoySystem 정찰 보고 첨부).
        /// 정직성 계약: 실장되지 않은 획득처를 말하지 않는다(미실장은 "재료 미실장"으로 명시).
        /// </summary>
        public static string BuildSourceHint(string preferredFood)
        {
            string ingredients = GetMainIngredients(preferredFood);
            if (!Foods.TryGetValue(preferredFood ?? string.Empty, out var entry))
                return $"재료 힌트: {ingredients} — 재료 미실장, 교역·조리 확장 후 입수 가능";

            switch (entry.Affinity)
            {
                case Affinity.Meat:
                    return entry.Tier == Tier3
                        ? $"재료 힌트: {ingredients} — 사냥 고기로 대체 조달 가능, 향신료는 큰 교역소 규모"
                        : $"재료 힌트: {ingredients} — 사냥 고기로 조달";
                case Affinity.Sea:
                    return entry.Tier == Tier3
                        ? $"재료 힌트: {ingredients} — 낚시 생선으로 대체 조달 가능, 버터는 교역소 규모"
                        : $"재료 힌트: {ingredients} — 낚시로 조달";
                case Affinity.Produce:
                    return $"재료 힌트: {ingredients} — 농경·과일 채집으로 조달";
                default:
                    return $"재료 힌트: {ingredients} — 재료 미실장, 교역·조리 확장 후 입수 가능";
            }
        }
    }
}