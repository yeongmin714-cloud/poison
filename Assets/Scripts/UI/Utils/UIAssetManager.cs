using UnityEngine;
using UnityEngine.UI;

namespace UI.Utils
{
    public class UIAssetManager : MonoBehaviour
    {
        [Header("Asset Settings")]
        public string assetPath = "Assets/UI/";

        public void LoadAsset(string assetName)
        {
            // Load UI asset
        }

        public void UnloadAsset(string assetName)
        {
            // Unload UI asset
        }

        public void ReloadAssets()
        {
            // Reload all assets
        }
    }
}