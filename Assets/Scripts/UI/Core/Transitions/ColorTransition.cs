using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Core.Transitions
{
    public class ColorTransition : Transition
    {
        [SerializeField] private Color targetColor;

        public override void PerformTransition()
        {
            // Implementation for color transition
            Debug.Log("Performing color transition to: " + targetColor);
        }
    }
}