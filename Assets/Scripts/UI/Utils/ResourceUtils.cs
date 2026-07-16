using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UI.Utils
{
    public static class ResourceUtils
    {
        public static int GetResourceAmount(ResourceType type, Dictionary<ResourceType, int> resources)
        {
            if (resources.TryGetValue(type, out int amount))
            {
                return amount;
            }
            return 0;
        }
        
        public static void AddResource(ResourceType type, int amount, Dictionary<ResourceType, int> resources)
        {
            if (resources.ContainsKey(type))
            {
                resources[type] += amount;
            }
            else
            {
                resources[type] = amount;
            }
        }
        
        public static bool CanAfford(Dictionary<ResourceType, int> costs, Dictionary<ResourceType, int> resources)
        {
            foreach (var cost in costs)
            {
                if (GetResourceAmount(cost.Key, resources) < cost.Value)
                {
                    return false;
                }
            }
            return true;
        }
        
        public static void SpendResources(Dictionary<ResourceType, int> costs, Dictionary<ResourceType, int> resources)
        {
            foreach (var cost in costs)
            {
                resources[cost.Key] -= cost.Value;
            }
        }
    }
    
    public enum ResourceType
    {
        Gold,
        Wood,
        Stone,
        Food,
        Iron,
        Crystal
    }
}