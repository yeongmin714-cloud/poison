using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>
    /// Represents a dish (cooked food item) parsed from GAME_DATA.md.
    /// </summary>
    public class DishInfo
    {
        public string Id;           // e.g., "D01"
        public string DisplayName;  // e.g., "토끼 허브 구이"
        public string Description;  // e.g., "토끼 고기 + 회복꽃"
        public string Effect;       // from 주요 효과 column
        public Sprite Icon;         // optional, set via editor or resources
        public int StarRating;      // 1~5 미식 등급 (0 = unrated)
        public PlayerInventory.ItemData ToItemData()
        {
            return new PlayerInventory.ItemData
            {
                id = Id,
                displayName = DisplayName,
                description = Description,
                category = PlayerInventory.ItemCategory.Food,
                icon = Icon,
                maxStack = 99
            };
        }
    }
}