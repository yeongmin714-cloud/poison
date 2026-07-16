using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UI.Core.Transitions
{
    public class ColorTransition : Transition
    {
        [Header("Color Transition Settings")]
        public Color fromColor = Color.clear;
        public Color toColor = Color.clear;
        public RectTransform targetRect;
        
        protected override IEnumerator DoTransition()
        {
            if (targetRect == null)
            {
                Debug.LogError("TargetRectTransform not assigned for ColorTransition");
                yield break;
            }
            
            float elapsedTime = 0f;
            Color startColor = fromColor;
            
            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = curve.Evaluate(elapsedTime / duration);
                Color newColor = Color.Lerp(startColor, toColor, t);
                
                // Apply color to target
                // targetRect.color = newColor;
                
                yield return null;
            }
            
            // Ensure final color is applied
            // targetRect.color = toColor;
        }
    }
}