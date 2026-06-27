using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Core.Data
{
    /// <summary>
    /// Represents a dish (cooked food item) parsed from GAME_DATA.md.
    /// Maps to an inventory item via <see cref="ToItemData"/>.
    /// </summary>
    public class DishInfo
    {
        /// <summary>Unique dish identifier, e.g., "D01".</summary>
        public string Id { get; set; }

        /// <summary>Display name, e.g., "토끼 허브 구이".</summary>
        public string DisplayName { get; set; }

        /// <summary>Short recipe description, e.g., "토끼 고기 + 회복꽃".</summary>
        public string Description { get; set; }

        /// <summary>Gameplay effect string from the 주요 효과 column (e.g., "체력 회복 25").</summary>
        public string Effect { get; set; }

        /// <summary>Optional icon sprite, set via editor or Resources load.</summary>
        public Sprite Icon { get; set; }

        /// <summary>Gourmet star rating 1–5 (0 = unrated). Populated separately by GourmetDatabase.</summary>
        public int StarRating { get; set; }

        /// <summary>
        /// Converts this dish info into a <see cref="PlayerInventory.ItemData"/>
        /// suitable for adding to the player's inventory.
        /// </summary>
        public PlayerInventory.ItemData ToItemData()
        {
            return new PlayerInventory.ItemData
            {
                id = Id ?? string.Empty,
                displayName = DisplayName ?? string.Empty,
                description = Description ?? string.Empty,
                category = PlayerInventory.ItemCategory.Food,
                icon = Icon,
                maxStack = 99,
                effects = Effect ?? string.Empty
            };
        }
    }
}
