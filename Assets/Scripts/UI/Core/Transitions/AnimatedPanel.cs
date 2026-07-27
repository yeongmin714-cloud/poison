using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Core.Transitions
{
    public class AnimatedPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private CanvasGroup canvasGroup;
        
        public void AnimateOpen()
        {
            // Implementation for animated open
            if (rectTransform != null)
            {
                // Add animation logic here
                rectTransform.gameObject.SetActive(true);
            }
        }
    }
}