using System.Linq;
using UnityEngine;

namespace ProjectName.Core.Data
{
    [AddComponentMenu("")]
    public class HerbTester : MonoBehaviour
    {
        private void Awake()
        {
            Debug.Log("[HerbTester] Awake");
            var all = HerbDatabase.AllHerbs;
            Debug.Log($"Total herbs: {all.Count}");
            // Show first 5
            foreach (var h in all.Take(5))
            {
                Debug.Log($"Herb: {h.id} - {h.displayName} ({h.attribute})");
            }
            // Lookup specific
            var a1 = HerbDatabase.GetHerbInfo("A1");
            Debug.Log($"A1: {a1.displayName} - {a1.description}");
        }
    }
}