using UnityEngine;
using UnityEngine.UI;

namespace UI.Utils
{
    public class UIEffectManager : MonoBehaviour
    {
        [Header("UI References")]
        public Image effectImage;
        public Text effectText;
        public RectTransform effectPanel;
        
        [Header("Effect Settings")]
        public float duration = 1.0f;
        public bool isPlaying = false;

        public void PlayEffect(string effectName)
        {
            // Play specific UI effect
            Debug.Log($"Playing effect: {effectName}");
        }

        public void PlayEffect(string effectName, Color effectColor)
        {
            // Play effect with specific color
            Debug.Log($"Playing effect: {effectName} with color {effectColor}");
        }

        public void StopEffect()
        {
            // Stop current effect
            Debug.Log("Stopping effect");
        }
    }
}