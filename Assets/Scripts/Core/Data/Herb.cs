using UnityEngine;

namespace ProjectName.Core.Data
{
    /// <summary>
    /// ScriptableObject representing a single herb.
    /// Can be created via HerbDatabaseEditor or manually.
    /// </summary>
    [CreateAssetMenu(fileName = "New Herb", menuName = "Data/Herb")]
    public class Herb : ScriptableObject
    {
        /// <summary>Unique herb identifier (e.g., "A1", "H3", "M7", "P10").</summary>
        [field: SerializeField] public string Id { get; private set; }

        /// <summary>Localized display name shown in UI.</summary>
        [field: SerializeField] public string DisplayName { get; private set; }

        /// <summary>Descriptive text explaining the herb's use or effect.</summary>
        [field: SerializeField] public string Description { get; private set; }

        /// <summary>The herb's attribute category (Attack, Mental, Recovery, Physical).</summary>
        [field: SerializeField] public HerbAttribute Attribute { get; private set; }

        /// <summary>Position within the attribute group (1-10).</summary>
        [field: SerializeField]
        [Range(1, 10)]
        public int Index { get; private set; }

        public override string ToString()
        {
            return $"{Id}: {DisplayName} ({Attribute})";
        }
    }
}