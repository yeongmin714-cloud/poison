using System.Collections.Generic;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 38.2: 6개 주요 영지별 축제 데이터 정의.
    /// 게임 시작 시 FestivalManager.LoadDefinitions()에서 호출되어
    /// 코드 기반 FestivalData 인스턴스를 생성합니다.
    /// </summary>
    public static class FestivalDefinitions
    {
        /// <summary>모든 축제 정의를 생성하여 반환합니다.</summary>
        public static List<FestivalData> CreateAll()
        {
            var list = new List<FestivalData>(6);

            list.Add(CreateIceFestival());
            list.Add(CreateDesertCarnival());
            list.Add(CreateFireFestival());
            list.Add(CreateHarvestFestival());
            list.Add(CreateEmpireDay());
            list.Add(CreateSeaFestival());

            return list;
        }

        // ================================================================
        // 1. 북부 영주 (Ice_Crown) — 얼음 축제 (Day 3~5)
        // ================================================================
        private static FestivalData CreateIceFestival()
        {
            var data = ScriptableObject.CreateInstance<FestivalData>();
            data.Initialize(
                id: "festival_ice_crown",
                name: "\u2744\uFE0F 얼음 축제",
                desc: "북부 영지 Ice_Crown에서 열리는 얼음 축제입니다. " +
                      "거대한 얼음 조각상이 전시되고, 주민들은 냉기 저항을 높이는 비법을 공유합니다. " +
                      "얼음 와인과 북극 딸기 등 특별한 음식도 맛볼 수 있습니다.",
                nation: NationType.North,
                index: 1,
                startDay: 3,
                endDay: 5,
                startHour: 8,
                endHour: 22,
                effect: new FestivalEffect
                {
                    coldResistanceBonus = 30,
                    strengthBonus = 3,
                    specialItemsForSale = new[] { "얼음 와인", "북극 딸기", "냉기 저항 부적" }
                },
                decorTag: "IceFestival",
                color: new Color(0.6f, 0.8f, 1.0f),
                emoji: "\u2744\uFE0F"
            );
            return data;
        }

        // ================================================================
        // 2. 서부 영주 (Sand) — 사막 카니발 (Day 8~12)
        // ================================================================
        private static FestivalData CreateDesertCarnival()
        {
            var data = ScriptableObject.CreateInstance<FestivalData>();
            data.Initialize(
                id: "festival_desert_carnival",
                name: "\uD83C\uDFAA 사막 카니발",
                desc: "서부 영지 Sand의 사막 한가운데서 펼쳐지는 화려한 카니발! " +
                      "상인들이 대거 몰려와 30% 할인된 가격에 물건을 판매합니다. " +
                      "사막 특산 조미료와 향신료도 구입할 수 있는 절호의 기회입니다.",
                nation: NationType.West,
                index: 1,
                startDay: 8,
                endDay: 12,
                startHour: 8,
                endHour: 22,
                effect: new FestivalEffect
                {
                    shopDiscountPercent = 30f,
                    stealthBonus = 5,
                    specialItemsForSale = new[] { "사막 조미료", "이국적 향신료", "실크 터번" }
                },
                decorTag: "DesertCarnival",
                color: new Color(1.0f, 0.8f, 0.3f),
                emoji: "\uD83C\uDFAA"
            );
            return data;
        }

        // ================================================================
        // 3. 남부 영주 (Red_Desert) — 불의 축제 (Day 15~20)
        // ================================================================
        private static FestivalData CreateFireFestival()
        {
            var data = ScriptableObject.CreateInstance<FestivalData>();
            data.Initialize(
                id: "festival_fire",
                name: "\uD83D\uDD25 불의 축제",
                desc: "남부 영지 Red_Desert의 불의 축제! " +
                      "화염 마법사들의 퍼포먼스와 함께 축제가 열리며, 전사의 화염 데미지가 20% 증가합니다. " +
                      "화염 검과 불의 활 등 화염 무기를 할인된 가격에 구매할 수 있습니다.",
                nation: NationType.South,
                index: 1,
                startDay: 15,
                endDay: 20,
                startHour: 8,
                endHour: 23,
                effect: new FestivalEffect
                {
                    fireDamageBonusPercent = 20,
                    strengthBonus = 5,
                    shopDiscountPercent = 15f,
                    specialItemsForSale = new[] { "화염 검", "불의 활", "내화 망토" }
                },
                decorTag: "FireFestival",
                color: new Color(1.0f, 0.3f, 0.1f),
                emoji: "\uD83D\uDD25"
            );
            return data;
        }

        // ================================================================
        // 4. 동부 영주 (East_Forest) — 수확제 (Day 22~28)
        // ================================================================
        private static FestivalData CreateHarvestFestival()
        {
            var data = ScriptableObject.CreateInstance<FestivalData>();
            data.Initialize(
                id: "festival_harvest",
                name: "\uD83C\uDF3E 수확제",
                desc: "동부 영지 East_Forest의 풍성한 수확제! " +
                      "가을 햇살 아래 온 마을이 풍년을 기념합니다. " +
                      "요리 성공률이 15% 증가하고, 약초를 2배로 획득할 수 있습니다. " +
                      "신선한 제철 재료들로 특별 요리를 만들어보세요.",
                nation: NationType.East,
                index: 1,
                startDay: 22,
                endDay: 28,
                startHour: 6,
                endHour: 21,
                effect: new FestivalEffect
                {
                    cookingSuccessRateBonus = 15,
                    herbGatherMultiplier = 2f,
                    cookingBonus = 10,
                    specialItemsForSale = new[] { "제철 과일 바구니", "황금 곡식", "꿀 항아리" }
                },
                decorTag: "HarvestFestival",
                color: new Color(0.3f, 0.8f, 0.3f),
                emoji: "\uD83C\uDF3E"
            );
            return data;
        }

        // ================================================================
        // 5. 중앙 영주 (Empire) — 제국의 날 (Day 35~40)
        // ================================================================
        private static FestivalData CreateEmpireDay()
        {
            var data = ScriptableObject.CreateInstance<FestivalData>();
            data.Initialize(
                id: "festival_empire_day",
                name: "\uD83D\uDC51 제국의 날",
                desc: "황제국 Empire에서 열리는 가장 성대한 축제! " +
                      "아우구스투스 황제가 주관하는 이 날, 모든 능력치가 5 상승합니다. " +
                      "전설 아이템 판매자들이 황실 광장에 모여 평소엔 볼 수 없는 희귀한 물건들을 선보입니다.",
                nation: NationType.Empire,
                index: 1,
                startDay: 35,
                endDay: 40,
                startHour: 7,
                endHour: 23,
                effect: new FestivalEffect
                {
                    allStatsBonus = 5,
                    sellsLegendaryItems = true,
                    specialItemsForSale = new[] { "전설의 검", "황제의 갑옷", "불멸의 반지" }
                },
                decorTag: "EmpireDay",
                color: new Color(0.9f, 0.7f, 0.1f),
                emoji: "\uD83D\uDC51"
            );
            return data;
        }

        // ================================================================
        // 6. 항구 영주 (Port_Town) — 바다 축제 (Day 45~50)
        // ================================================================
        private static FestivalData CreateSeaFestival()
        {
            var data = ScriptableObject.CreateInstance<FestivalData>();
            data.Initialize(
                id: "festival_sea",
                name: "\uD83C\uDF0A 바다 축제",
                desc: "항구 영지 Port_Town의 바다 축제! " +
                      "온 마을이 바다를 기념하며 물고기 아이템 획득량이 3배로 증가합니다. " +
                      "선선한 해풍 덕분에 이동속도가 20% 빨라지고, 항구 주변 상점에서는 " +
                      "다양한 해산물과 항해 용품을 할인된 가격에 판매합니다.",
                nation: NationType.East,
                index: 2, // 동부 2번째 영지 = 항구
                startDay: 45,
                endDay: 50,
                startHour: 6,
                endHour: 22,
                effect: new FestivalEffect
                {
                    speedBonusPercent = 20,
                    fishGatherMultiplier = 3f,
                    shopDiscountPercent = 20f,
                    specialItemsForSale = new[] { "황금 물고기", "진주 목걸이", "항해 지도" }
                },
                decorTag: "SeaFestival",
                color: new Color(0.2f, 0.5f, 0.9f),
                emoji: "\uD83C\uDF0A"
            );
            return data;
        }
    }
}