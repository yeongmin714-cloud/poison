using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Core.Transitions
{
    public abstract class Transition : MonoBehaviour
    {
        [SerializeField] private TransitionType type;
        [SerializeField] private float duration;
        
        public abstract void PerformTransition();
        
        protected virtual void Start()
        {
            // Base initialization if needed
        }
    }
}