using UnityEngine;
using UnityEngine.UI;

namespace ProjectName.UI.Core
{
    public class ToolTipManager : MonoBehaviour
    {
        public static ToolTipManager Instance { get; private set; }

        [SerializeField] private GameObject tooltipPrefab;
        [SerializeField] private Canvas canvas;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void ShowTooltip(string text, Vector2 position)
        {
            // Implementation for showing tooltip
            Debug.Log($"Showing tooltip: {text} at {position}");
        }

        public void HideTooltip()
        {
            // Implementation for hiding tooltip
            Debug.Log("Hiding tooltip");
        }
    }
}