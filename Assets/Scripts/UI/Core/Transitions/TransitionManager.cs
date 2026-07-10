using UnityEngine;
using System.Collections.Generic;

namespace UI.Core.Transitions
{
    public class TransitionManager : MonoBehaviour
    {
        private List<Transition> activeTransitions = new List<Transition>();

        public static TransitionManager Instance { get; private set; }

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

        public void AddTransition(Transition transition)
        {
            activeTransitions.Add(transition);
        }

        public void RemoveTransition(Transition transition)
        {
            activeTransitions.Remove(transition);
        }
    }
}