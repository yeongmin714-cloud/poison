using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Core.Data
{
    /// <summary>
    /// ScriptableObject representing a single herb.
    /// Can be created via HerbDatabaseEditor or manually.
    /// </summary>
    [CreateAssetMenu(fileName = "New Herb", menuName = "Data/Herb")]
    public class Herb : ScriptableObject
    {
        [field: SerializeField] public string id { get; private set; }
        [field: SerializeField] public string displayName { get; private set; }
        [field: SerializeField] public string description { get; private set; }
        [field: SerializeField] public HerbAttribute attribute { get; private set; }
        [field: SerializeField] public int index { get; private set; } // 1-10 within attribute

        public override string ToString()
        {
            return $"{id}: {displayName} ({attribute})";
        }
    }
}