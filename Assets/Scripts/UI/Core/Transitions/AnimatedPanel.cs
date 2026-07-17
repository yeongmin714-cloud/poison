using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectName.UI.Core.Transitions
{
    public class AnimatedPanel : Transition
    {
        [Header("Animated Panel Settings")]
        public RectTransform panelRect;
        public Vector2 fromPosition = Vector2.zero;
        public Vector2 toPosition = Vector2.zero;
        public Vector2 fromScale = Vector2.one;
        public Vector2 toScale = Vector2.one;
        
        [Header("State")]
        public bool isAnimating = false;
        
        protected override IEnumerator DoTransition()
        {
            if (panelRect == null)
            {
                Debug.LogError("Panel RectTransform not assigned");
                yield break;
            }
            
            isAnimating = true;
            float elapsedTime = 0f;
            Vector2 startPosition = fromPosition;
            Vector2 startScale = fromScale;
            
            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = curve.Evaluate(elapsedTime / duration);
                
                Vector2 newPosition = Vector2.Lerp(startPosition, toPosition, t);
                Vector2 newScale = Vector2.Lerp(startScale, toScale, t);
                
                panelRect.anchoredPosition = newPosition;
                panelRect.localScale = newScale;
                
                yield return null;
            }
            
            // Ensure final state
            panelRect.anchoredPosition = toPosition;
            panelRect.localScale = toScale;
            
            isAnimating = false;
        }
        
        public void PlayEnterTransition()
        {
            fromPosition = panelRect.anchoredPosition;
            toPosition = new Vector2(panelRect.anchoredPosition.x, 0);
            fromScale = Vector2.zero;
            toScale = Vector2.one;
            PlayTransition();
        }
        
        public void PlayExitTransition()
        {
            fromPosition = panelRect.anchoredPosition;
            toPosition = new Vector2(panelRect.anchoredPosition.x, 1000);
            fromScale = Vector2.one;
            toScale = Vector2.zero;
            PlayTransition();
        }
    }
}