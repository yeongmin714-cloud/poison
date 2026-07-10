using UnityEngine;
using UnityEngine.UI;

namespace UI.Core
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
        }

        public void HideTooltip()
        {
            // Implementation for hiding tooltip
        }
    }
}