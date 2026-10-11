using UnityEditor;

namespace MagicPigGames.MagicTime
{
    public static class MagicTimeEditorUtilities
    {
        /// <summary>
        /// Returns all LocalTimeScales in the project.
        /// </summary>
        /// <returns></returns>
        public static LocalTimeScale[] FindAllLocalTimeScalesInProject()
        {
            var guids = AssetDatabase.FindAssets("t:LocalTimeScale");
            var timeScales = new LocalTimeScale[guids.Length];
            for (var i = 0; i < guids.Length; i++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                timeScales[i] = AssetDatabase.LoadAssetAtPath<LocalTimeScale>(assetPath);
            }
            return timeScales;
        }
    }
}