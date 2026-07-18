using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.UI.Core.Transitions
{
    public class TransitionManager : MonoBehaviour
    {
        [Header("Transition Settings")]
        public TransitionType defaultTransition = TransitionType.Fade;
        public float defaultDuration = 0.5f;
        
        [Header("Active Transitions")]
        public List<Transition> activeTransitions = new List<Transition>();
        
        private static TransitionManager _instance;
        public static TransitionManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<TransitionManager>();
                    if (_instance == null)
                    {
                        GameObject obj = new GameObject("TransitionManager");
                        _instance = obj.AddComponent<TransitionManager>();
                    }
                }
                return _instance;
            }
        }
        
        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }
        
        public void StartTransition(TransitionType type, float duration = 0f)
        {
            if (duration <= 0) duration = defaultDuration;
            
            switch (type)
            {
                case TransitionType.Fade:
                    // Start fade transition
                    break;
                case TransitionType.Slide:
                    // Start slide transition
                    break;
                case TransitionType.Scale:
                    // Start scale transition
                    break;
            }
        }
        
        public void AddActiveTransition(Transition transition)
        {
            if (!activeTransitions.Contains(transition))
            {
                activeTransitions.Add(transition);
            }
        }
        
        public void RemoveActiveTransition(Transition transition)
        {
            activeTransitions.Remove(transition);
        }
    }
}