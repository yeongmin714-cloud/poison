using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectName.Core.Data;

namespace ProjectName.Editor
{
    /// <summary>
    /// Automatically imports herb data on script reload if no herb assets exist yet.
    /// </summary>
    [InitializeOnLoad]
    public static class HerbAutoImporter
    {
        static HerbAutoImporter()
        {
            // Delay the import until after all assets are loaded
            EditorApplication.delayCall += ImportIfNeeded;
        }

        private static void ImportIfNeeded()
        {
            // Check if we already have any herb assets
            string[] guids = AssetDatabase.FindAssets("t:Herb", new[] { "Assets/Data/Herbs" });
            if (guids.Length > 0)
            {
                // Already imported, skip
                return;
            }

            // Run the import
            HerbDatabaseEditor.ImportHerbData();
        }
    }
}