using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>
    /// ScriptableObject representing a single herb.
    /// Can be created via HerbDatabaseEditor or manually.
    /// </summary>
    [CreateAssetMenu(fileName = "New Herb", menuName = "Data/Herb")]
    public class Herb : ScriptableObject
    {
        public string id;
        public string displayName;
        public string description;
        public HerbAttribute attribute;
        public int index; // 1-10 within attribute

        public override string ToString()
        {
            return $"{id}: {displayName} ({attribute})";
        }
    }
}