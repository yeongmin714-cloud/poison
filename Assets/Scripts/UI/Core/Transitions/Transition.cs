using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.UI.Core.Transitions
{
    public abstract class Transition : MonoBehaviour
    {
        [Header("Transition Settings")]
        public float duration = 0.5f;
        public AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        
        public void PlayTransition()
        {
            StartCoroutine(DoTransition());
        }
        
        protected abstract IEnumerator DoTransition();

        // Update transition progress (for Update loop if needed)
        public virtual void UpdateTransition()
        {
            // Override in subclasses if frame-by-frame update needed
        }
    }
}