using System;
using System.Collections.Generic;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Core
{
    /// <summary>
    /// C12-03: 게임 팁 데이터베이스.
    /// 로딩 화면에 랜덤으로 표시할 팁을 카테고리별로 제공합니다.
    /// </summary>
    public enum TipCategory
    {
        Gameplay,
        Combat,
        Strategy,
        Lore
    }

    /// <summary>팁 정보 구조체 (불변)</summary>
    public struct TipInfo
    {
        public readonly string Text;
        public readonly TipCategory Category;

        public TipInfo(string text, TipCategory category)
        {
            Text = text;
            Category = category;
        }
    }

    public static class TipDatabase
    {
        private static readonly TipInfo[] Tips =
        {
            // ===== Gameplay =====
            new TipInfo("약초는 던전 입구 주변과 숲에서 자주 발견됩니다.", TipCategory.Gameplay),
            new TipInfo("가스 분사기는 등에 메는 장비입니다. 인벤토리 장비창(Back 슬롯)에서 장착하세요.", TipCategory.Gameplay),
            new TipInfo("마약 아이템은 효과가 강력하지만 중독 위험이 있습니다.", TipCategory.Gameplay),
            new TipInfo("재료를 조합해 더 강력한 아이템을 제작할 수 있습니다.", TipCategory.Gameplay),
            new TipInfo("요리를 만들면 일시적인 버프 효과를 얻을 수 있습니다.", TipCategory.Gameplay),
            new TipInfo("장비의 내구도가 0이 되면 파괴됩니다. 수선소에서 수리하세요.", TipCategory.Gameplay),
            new TipInfo("퀘스트를 완료하면 경험치와 보상을 얻을 수 있습니다.", TipCategory.Gameplay),
            new TipInfo("지도를 사용해 현재 위치와 주변 지역을 확인하세요.", TipCategory.Gameplay),
            new TipInfo("채광 미션으로 광석을 획득해 장비를 업그레이드하세요.", TipCategory.Gameplay),
            new TipInfo("사냥 미션으로 고기와 가죽 등 재료를 확보할 수 있습니다.", TipCategory.Gameplay),

            // ===== Combat =====
            new TipInfo("가스 분사기에 물약을 장전하면 속성별 안개 효과가 발동됩니다.", TipCategory.Combat),
            new TipInfo("치유초 안개는 아군을 치유합니다. 전투 중 유용합니다.", TipCategory.Combat),
            new TipInfo("독나물 안개는 적에게 독 피해를 줍니다.", TipCategory.Combat),
            new TipInfo("황혼초 안개는 적을 혼란 상태로 만듭니다.", TipCategory.Combat),
            new TipInfo("폭탄은 우클릭으로 멀리 던질 수 있습니다.", TipCategory.Combat),
            new TipInfo("화염 폭탄은 넓은 범위에 불 피해를 줍니다.", TipCategory.Combat),
            new TipInfo("연막 폭탄은 적의 시야를 차단해 도망칠 시간을 벌어줍니다.", TipCategory.Combat),
            new TipInfo("폭탄은 재료를 모아 제작할 수 있습니다.", TipCategory.Combat),

            // ===== Strategy =====
            new TipInfo("밤에는 몬스터가 더 강해집니다. 충분히 준비하세요.", TipCategory.Strategy),
            new TipInfo("영주를 고용하면 영지 발전에 큰 도움이 됩니다.", TipCategory.Strategy),
            new TipInfo("영지 내 자원 생산량은 영주의 능력치에 영향을 받습니다.", TipCategory.Strategy),
            new TipInfo("여행하는 상인에게서 희귀 아이템을 구매할 기회가 있습니다.", TipCategory.Strategy),
            new TipInfo("특사 파견으로 다른 영지와 외교 관계를 맺을 수 있습니다.", TipCategory.Strategy),
            new TipInfo("부하의 충성도가 낮으면 배신할 수 있습니다. 선물을 줘서 관리하세요.", TipCategory.Strategy),

            // ===== Lore =====
            new TipInfo("허브는 5가지 종류가 있습니다. 각각 다른 효과를 가집니다.", TipCategory.Lore),
            new TipInfo("몬스터 레벨이 높을수록 더 좋은 전리품을 드롭합니다.", TipCategory.Lore),
        };

        /// <summary>랜덤 팁 한 개 반환 (하위 호환). 팁이 없으면 빈 문자열 반환.</summary>
        /// <returns>랜덤 팁 텍스트, 또는 빈 문자열</returns>
        public static string GetRandomTip()
        {
            if (Tips.Length == 0) return string.Empty;
            return Tips[UnityEngine.Random.Range(0, Tips.Length)].Text;
        }

        /// <summary>랜덤 팁 한 개의 TipInfo 반환. 팁이 없으면 기본값 반환.</summary>
        /// <returns>랜덤으로 선택된 TipInfo 구조체, 팁이 없으면 default(TipInfo)</returns>
        public static TipInfo GetRandomTipInfo()
        {
            if (Tips.Length == 0) return default;
            return Tips[UnityEngine.Random.Range(0, Tips.Length)];
        }

        /// <summary>
        /// 서로 다른 카테고리의 팁 2개를 반환합니다.
        /// 가능하면 다른 카테고리에서 선택하며, 팁이 2개 미만이면 같은 팁/카테고리일 수 있습니다.
        /// </summary>
        /// <returns>(text1, cat1, text2, cat2). 팁이 없으면 빈 문자열과 기본 카테고리.</returns>
        public static (string text1, TipCategory cat1, string text2, TipCategory cat2) GetTwoRandomTips()
        {
            // 빈 배열 보호
            if (Tips.Length == 0)
                return (string.Empty, TipCategory.Gameplay, string.Empty, TipCategory.Gameplay);

            int idx1 = UnityEngine.Random.Range(0, Tips.Length);
            TipInfo t1 = Tips[idx1];
            TipInfo t2;

            // 팁이 1개만 있으면 같은 팁 두 번 반환
            if (Tips.Length == 1)
                return (t1.Text, t1.Category, t1.Text, t1.Category);

            // 같은 카테고리인 팁 중 랜덤 선택 시도 (최대 5회)
            int attempts = 0;
            do
            {
                int idx2 = UnityEngine.Random.Range(0, Tips.Length);
                t2 = Tips[idx2];
                attempts++;
            } while (t2.Category == t1.Category && attempts < 5);

            // 만약 끝까지 다른 카테고리를 못 찾았으면 인접 팁 사용
            if (t2.Category == t1.Category)
            {
                int idx2 = (idx1 + 1) % Tips.Length;
                t2 = Tips[idx2];
            }

            return (t1.Text, t1.Category, t2.Text, t2.Category);
        }

        /// <summary>특정 카테고리의 모든 팁을 반환합니다. 해당 카테고리의 팁이 없으면 빈 배열을 반환합니다.</summary>
        /// <param name="category">조회할 팁 카테고리</param>
        /// <returns>해당 카테고리의 TipInfo 배열</returns>
        public static TipInfo[] GetTipsByCategory(TipCategory category)
        {
            var list = new List<TipInfo>();
            foreach (var tip in Tips)
            {
                if (tip.Category == category)
                    list.Add(tip);
            }
            return list.ToArray();
        }

        /// <summary>등록된 전체 팁의 개수를 반환합니다.</summary>
        public static int TotalTips => Tips.Length;
    }
}