using UnityEngine;
using UnityEngine.UI;

namespace UI.Utils
{
    public class UIAnimationUtils : MonoBehaviour
    {
        [Header("Animation Settings")]
        public float animationDuration = 1.0f;

        public void AnimatePosition(RectTransform target, Vector2 from, Vector2 to)
        {
            // Animate position change
            // Debug.Log($"Animating position for {target?.name ?? "null"}");
        }

        public void AnimateScale(RectTransform target, Vector3 from, Vector3 to)
        {
            // Animate scale change
            // Debug.Log($"Animating scale for {target?.name ?? "null"}");
        }

        public void AnimateAlpha(Graphic graphic, float from, float to)
        {
            // Animate alpha/transparency
            // Debug.Log($"Animating alpha for {graphic?.name ?? "null"}");
        }
    }
}