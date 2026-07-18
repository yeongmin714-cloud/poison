using UnityEngine;
using UnityEngine.UI;

namespace UI.Utils
{
    public class UIEffectUtils : MonoBehaviour
    {
        [Header("Effect Settings")]
        public float effectDuration = 1.0f;
        public AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        public void PlayUIEffect(GameObject target, string effectName)
        {
            // Play a UI effect on target object
            Debug.Log($"Playing effect '{effectName}' on {target.name}");
        }

        public void PlayUIEffect(GameObject target, string effectName, Color effectColor)
        {
            // Play a UI effect with specific color
            Debug.Log($"Playing effect '{effectName}' with color {effectColor} on {target.name}");
        }

        public void PlayFadeEffect(Image image, float duration)
        {
            // Play fade in/out effect on image
            Debug.Log($"Playing fade effect on {image.name}");
        }
    }
}