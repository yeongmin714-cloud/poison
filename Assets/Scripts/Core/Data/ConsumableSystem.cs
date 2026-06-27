using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Core.Data
{
    /// <summary>
    /// Consumable system: applies effects of food items when consumed.
    /// </summary>
    public static class ConsumableSystem
    {
        /// <summary>
        /// Use (consume) an item from inventory.
        /// </summary>
        /// <param name="item">ItemData to consume</param>
        public static void UseItem(PlayerInventory.ItemData item)
        {
            if (item == null)
            {
                Debug.LogWarning("[ConsumableSystem] Trying to use null item.");
                return;
            }

            if (item.category != PlayerInventory.ItemCategory.Food)
            {
                Debug.LogWarning($"[ConsumableSystem] Item {item.displayName} is not food (category {item.category}).");
                return;
            }

            // Get dish info to retrieve effect
            var dish = DishDatabase.GetDishInfoByName(item.displayName);
            if (dish == null)
            {
                Debug.LogWarning($"[ConsumableSystem] Could not find dish info for {item.displayName}.");
                return;
            }

            ApplyEffect(dish.Effect);
            Debug.Log($"[ConsumableSystem] Consumed {item.displayName}. Effect: {dish.Effect}");
        }

        private static void ApplyEffect(string effect)
        {
            if (string.IsNullOrEmpty(effect))
            {
                Debug.LogWarning("[ConsumableSystem] Empty effect — no effect applied.");
                return;
            }

            // Heal effects
            if (effect.Contains("체력 회복") || effect.Contains("재생") || effect.Contains("치유"))
            {
                // Heal a flat amount; could be scaled based on dish but we keep simple.
                HealPlayer(25f);
                Debug.Log("[ConsumableSystem] Applied heal 25 HP.");
                return;
            }

            // Attack boost
            if (effect.Contains("공격력"))
            {
                if (BuffManager.Instance != null)
                {
                    BuffManager.Instance.AddBuff("AttackUp", 5f, 10f);
                    Debug.Log("[ConsumableSystem] Attack increase buff added (+5 attack for 10s).");
                }
                else
                {
                    Debug.LogWarning("[ConsumableSystem] BuffManager instance not found.");
                }
                return;
            }

            // Defense boost
            if (effect.Contains("방어력") || effect.Contains("생명 보호막"))
            {
                if (BuffManager.Instance != null)
                {
                    BuffManager.Instance.AddBuff("DefenseUp", 3f, 10f);
                    Debug.Log("[ConsumableSystem] Defense increase buff added (+3 defense for 10s).");
                }
                else
                {
                    Debug.LogWarning("[ConsumableSystem] BuffManager instance not found.");
                }
                return;
            }

            // Speed boost
            if (effect.Contains("속도"))
            {
                if (BuffManager.Instance != null)
                {
                    BuffManager.Instance.AddBuff("SpeedUp", 2f, 10f);
                    Debug.Log("[ConsumableSystem] Speed increase buff added (+2 speed for 10s).");
                }
                else
                {
                    Debug.LogWarning("[ConsumableSystem] BuffManager instance not found.");
                }
                return;
            }

            // Critical chance
            if (effect.Contains("크리티컬") || effect.Contains("치명타"))
            {
                if (BuffManager.Instance != null)
                {
                    BuffManager.Instance.AddBuff("CritUp", 0.1f, 10f);
                    Debug.Log("[ConsumableSystem] Critical chance buff added (+10% crit for 10s).");
                }
                else
                {
                    Debug.LogWarning("[ConsumableSystem] BuffManager instance not found.");
                }
                return;
            }

            // Affinity / intimacy
            if (effect.Contains("친밀도") || effect.Contains("매력"))
            {
                if (BuffManager.Instance != null)
                {
                    BuffManager.Instance.AddBuff("IntimacyUp", 10f, 10f);
                    Debug.Log("[ConsumableSystem] Intimacy increase buff added (+10 affinity for 10s).");
                }
                else
                {
                    Debug.LogWarning("[ConsumableSystem] BuffManager instance not found.");
                }
                return;
            }

            // Stealth / invisibility
            if (effect.Contains("은신") || effect.Contains("시야 차단"))
            {
                // We could add a stealth buff that reduces enemy detection; placeholder.
                Debug.Log("[ConsumableSystem] Stealth/invisibility effect applied (placeholder).");
                return;
            }

            // Mana / energy recovery
            if (effect.Contains("마력 회복") || effect.Contains("에너지 보충") || effect.Contains("정신력 회복"))
            {
                // For now, just log; could add a buff that increases mana regen.
                Debug.Log("[ConsumableSystem] Mana/energy recovery effect applied (placeholder).");
                return;
            }

            // Detox
            if (effect.Contains("해독") || effect.Contains("독 제거"))
            {
                // Remove poison etc.; placeholder.
                Debug.Log("[ConsumableSystem] Detox effect applied (placeholder).");
                return;
            }

            // Status cure
            if (effect.Contains("상태이상 방지") || effect.Contains("상태 이상"))
            {
                // Cure status effects; placeholder.
                Debug.Log("[ConsumableSystem] Status cure effect applied (placeholder).");
                return;
            }

            // Default: just log
            Debug.Log($"[ConsumableSystem] Effect '{effect}' applied (no specific gameplay change).");
        }

        private static void HealPlayer(float amount)
        {
            var health = PlayerHealth.Instance;
            if (health != null)
            {
                health.Heal(amount);
            }
            else
            {
                Debug.LogWarning("[ConsumableSystem] PlayerHealth instance not found.");
            }
        }
    }
}