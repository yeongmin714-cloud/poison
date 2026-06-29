using System;
using System.Collections.Generic;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 38: 영지별 축제 정의 ScriptableObject.
    /// 축제 ID, 이름, 설명, 대상 영지, 기간, 시간대, 효과 및 장식 정보를 포함합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewFestival", menuName = "ProjectName/Festival Data", order = 200)]
    public class FestivalData : ScriptableObject
    {
        // ===== 직렬화 필드 (Inspector에서 설정 가능) =====

        [Header("=== 기본 정보 ===")]
        [SerializeField] private string _festivalId;
        [SerializeField] private string _festivalName;
        [SerializeField][TextArea(3, 6)] private string _description;

        [Header("=== 대상 영지 ===")]
        [SerializeField] private NationType _territoryNation;
        [SerializeField] private int _territoryIndex = 1;

        [Header("=== 축제 기간 (게임 Day 기준) ===")]
        [SerializeField] private int _startDay;
        [SerializeField] private int _endDay;

        [Header("=== 축제 시간대 (0~23시) ===")]
        [SerializeField] private int _startHour = 8;
        [SerializeField] private int _endHour = 22;

        [Header("=== 축제 효과 ===")]
        [SerializeField] private int _strengthBonus;
        [SerializeField] private int _stealthBonus;
        [SerializeField] private int _cookingBonus;
        [SerializeField] private int _speedBonusPercent;

        [Header("=== 상점 효과 ===")]
        [SerializeField][Range(0f, 100f)] private float _shopDiscountPercent;
        [SerializeField] private string[] _specialItemsForSale;

        [Header("=== 전투 효과 ===")]
        [SerializeField] private int _fireDamageBonusPercent;
        [SerializeField] private int _coldResistanceBonus;

        [Header("=== 수집 효과 ===")]
        [SerializeField] private float _herbGatherMultiplier = 1f;
        [SerializeField] private float _fishGatherMultiplier = 1f;
        [SerializeField] private int _cookingSuccessRateBonus;

        [Header("=== 전설/특수 ===")]
        [SerializeField] private bool _sellsLegendaryItems;
        [SerializeField] private int _allStatsBonus;

        [Header("=== 표시 설정 ===")]
        [SerializeField] private string _decorationTag;
        [SerializeField] private Color _festivalColor = Color.yellow;
        [SerializeField] private string _emoji = "\uD83C\uDF89";

        // ===== 읽기 전용 프로퍼티 =====

        public string festivalId => _festivalId;
        public string festivalName => _festivalName;
        public string description => _description;

        /// <summary>대상 영지 TerritoryId</summary>
        public TerritoryId territoryId => new TerritoryId(_territoryNation, _territoryIndex);

        public NationType territoryNation => _territoryNation;
        public int territoryIndex => _territoryIndex;
        public int startDay => _startDay;
        public int endDay => _endDay;
        public int startHour => _startHour;
        public int endHour => _endHour;

        // 효과 프로퍼티
        public int strengthBonus => _strengthBonus;
        public int stealthBonus => _stealthBonus;
        public int cookingBonus => _cookingBonus;
        public int speedBonusPercent => _speedBonusPercent;
        public float shopDiscountPercent => _shopDiscountPercent;
        public string[] specialItemsForSale => _specialItemsForSale ?? Array.Empty<string>();
        public int fireDamageBonusPercent => _fireDamageBonusPercent;
        public int coldResistanceBonus => _coldResistanceBonus;
        public float herbGatherMultiplier => Mathf.Max(1f, _herbGatherMultiplier);
        public float fishGatherMultiplier => Mathf.Max(1f, _fishGatherMultiplier);
        public int cookingSuccessRateBonus => _cookingSuccessRateBonus;
        public bool sellsLegendaryItems => _sellsLegendaryItems;
        public int allStatsBonus => _allStatsBonus;
        public string decorationTag => _decorationTag;
        public Color festivalColor => _festivalColor;
        public string emoji => _emoji;

        /// <summary>FestivalEffect 구조체로 내보내기</summary>
        public FestivalEffect GetEffect()
        {
            return new FestivalEffect
            {
                strengthBonus = _strengthBonus,
                stealthBonus = _stealthBonus,
                cookingBonus = _cookingBonus,
                speedBonusPercent = _speedBonusPercent,
                shopDiscountPercent = _shopDiscountPercent,
                specialItemsForSale = _specialItemsForSale,
                fireDamageBonusPercent = _fireDamageBonusPercent,
                coldResistanceBonus = _coldResistanceBonus,
                herbGatherMultiplier = _herbGatherMultiplier,
                fishGatherMultiplier = _fishGatherMultiplier,
                cookingSuccessRateBonus = _cookingSuccessRateBonus,
                sellsLegendaryItems = _sellsLegendaryItems,
                allStatsBonus = _allStatsBonus
            };
        }

        // ===== 상태 체크 =====

        /// <summary>현재 게임 Day가 축제 기간 내에 있는지 확인</summary>
        public bool IsInDateRange(int currentDay)
        {
            return currentDay >= _startDay && currentDay <= _endDay;
        }

        /// <summary>현재 게임 시간(Hour)이 축제 시간대 내에 있는지 확인</summary>
        public bool IsInTimeRange(int currentHour)
        {
            return currentHour >= _startHour && currentHour < _endHour;
        }

        /// <summary>축제가 현재 완전히 활성화되었는지 (날짜 + 시간)</summary>
        public bool IsActive(int currentDay, int currentHour)
        {
            return IsInDateRange(currentDay) && IsInTimeRange(currentHour);
        }

        // ===== 코드 기반 초기화 (FestivalDefinitions에서 사용) =====

        /// <summary>Inspector 직렬화 필드를 코드로 설정합니다.</summary>
        public void Initialize(
            string id, string name, string desc,
            NationType nation, int index,
            int startDay, int endDay,
            int startHour, int endHour,
            FestivalEffect effect,
            string decorTag = "", Color? color = null, string emoji = "\uD83C\uDF89")
        {
            _festivalId = id;
            _festivalName = name;
            _description = desc;
            _territoryNation = nation;
            _territoryIndex = index;
            _startDay = startDay;
            _endDay = endDay;
            _startHour = startHour;
            _endHour = endHour;
            _decorationTag = decorTag;
            _festivalColor = color ?? Color.yellow;
            _emoji = emoji;

            _strengthBonus = effect.strengthBonus;
            _stealthBonus = effect.stealthBonus;
            _cookingBonus = effect.cookingBonus;
            _speedBonusPercent = effect.speedBonusPercent;
            _shopDiscountPercent = effect.shopDiscountPercent;
            _specialItemsForSale = effect.specialItemsForSale;
            _fireDamageBonusPercent = effect.fireDamageBonusPercent;
            _coldResistanceBonus = effect.coldResistanceBonus;
            _herbGatherMultiplier = effect.herbGatherMultiplier;
            _fishGatherMultiplier = effect.fishGatherMultiplier;
            _cookingSuccessRateBonus = effect.cookingSuccessRateBonus;
            _sellsLegendaryItems = effect.sellsLegendaryItems;
            _allStatsBonus = effect.allStatsBonus;
        }

        /// <summary>디버그용 요약 문자열</summary>
        public override string ToString()
        {
            return $"[{_festivalId}] {_festivalName} ({_territoryNation}_{_territoryIndex:D2}, Day {_startDay}~{_endDay}, {_startHour}:00~{_endHour}:00)";
        }
    }

    /// <summary>
    /// 축제 효과 데이터 구조체.
    /// FestivalData.Initialize()에 전달하여 코드 기반 초기화에 사용됩니다.
    /// </summary>
    [Serializable]
    public struct FestivalEffect
    {
        [Header("능력치 보너스")]
        public int strengthBonus;
        public int stealthBonus;
        public int cookingBonus;
        public int speedBonusPercent;

        [Header("상점 효과")]
        [Range(0f, 100f)] public float shopDiscountPercent;
        public string[] specialItemsForSale;

        [Header("전투 효과")]
        public int fireDamageBonusPercent;
        public int coldResistanceBonus;

        [Header("수집 효과")]
        public float herbGatherMultiplier;
        public float fishGatherMultiplier;
        public int cookingSuccessRateBonus;

        [Header("전설/특수")]
        public bool sellsLegendaryItems;
        public int allStatsBonus;

        /// <summary>효과 요약 문자열</summary>
        public string GetSummary()
        {
            var parts = new List<string>();
            if (strengthBonus != 0) parts.Add($"힘 +{strengthBonus}");
            if (stealthBonus != 0) parts.Add($"은신 +{stealthBonus}");
            if (cookingBonus != 0) parts.Add($"요리 +{cookingBonus}");
            if (speedBonusPercent != 0) parts.Add($"이속 +{speedBonusPercent}%");
            if (shopDiscountPercent > 0f) parts.Add($"상점 {shopDiscountPercent}% 할인");
            if (fireDamageBonusPercent != 0) parts.Add($"화염 데미지 +{fireDamageBonusPercent}%");
            if (coldResistanceBonus != 0) parts.Add($"냉기 저항 +{coldResistanceBonus}");
            if (herbGatherMultiplier > 1f) parts.Add($"약초 {herbGatherMultiplier:F1}배");
            if (fishGatherMultiplier > 1f) parts.Add($"물고기 {fishGatherMultiplier:F1}배");
            if (cookingSuccessRateBonus != 0) parts.Add($"요리 성공률 +{cookingSuccessRateBonus}%");
            if (sellsLegendaryItems) parts.Add("전설 아이템 판매");
            if (allStatsBonus != 0) parts.Add($"모든 능력치 +{allStatsBonus}");
            if (specialItemsForSale != null && specialItemsForSale.Length > 0)
                parts.Add($"특수 아이템: {string.Join(", ", specialItemsForSale)}");

            return parts.Count > 0 ? string.Join(", ", parts) : "효과 없음";
        }
    }
}