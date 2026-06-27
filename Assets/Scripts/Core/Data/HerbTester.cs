using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectName.Core.Data
{
    [AddComponentMenu("")] // Hidden from Add Component menu — tester-only
    public class HerbTester : MonoBehaviour
    {
        private void Awake()
        {
            Debug.Log("[HerbTester] Awake — initializing herb database...");

            IReadOnlyList<HerbInfo> all = HerbDatabase.AllHerbs;
            Debug.Log($"[HerbTester] Total herbs: {all.Count}");

            if (all.Count == 0)
            {
                Debug.LogError("[HerbTester] HerbDatabase returned 0 herbs — GAME_DATA.md may be missing or malformed.");
                return;
            }

            // Show first 5
            foreach (HerbInfo h in all.Take(5))
            {
                Debug.Log($"[HerbTester] Herb: {h.id} - {h.displayName} ({h.attribute})");
            }

            // Lookup specific
            HerbInfo a1 = HerbDatabase.GetHerbInfo("A1");
            if (!string.IsNullOrEmpty(a1.id))
            {
                Debug.Log($"[HerbTester] A1: {a1.displayName} - {a1.description}");
            }
            else
            {
                Debug.LogWarning("[HerbTester] Herb 'A1' not found in database.");
            }
        }
    }
}