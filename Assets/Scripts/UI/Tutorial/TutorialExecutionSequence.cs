using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Tutorial
{
    public class TutorialExecutionSequence : MonoBehaviour
    {
        [SerializeField] private List<TutorialActionDetector> actions;
        private int currentActionIndex = 0;
        
        public void Initialize()
        {
            currentActionIndex = 0;
        }

        public void ExecuteSequence()
        {
            if(actions == null || actions.Count == 0)
            {
                Debug.LogWarning("No actions defined in tutorial sequence");
                return;
            }
            
            if(currentActionIndex < actions.Count)
            {
                var action = actions[currentActionIndex];
                if(action != null)
                {
                    Debug.Log($"Executing action: {action.ActionName}");
                    currentActionIndex++;
                }
                else
                {
                    Debug.LogWarning("Found null action in sequence");
                    currentActionIndex++;
                }
            }
        }
        
        public void ResetSequence()
        {
            currentActionIndex = 0;
        }
        
        public bool IsSequenceComplete()
        {
            return actions != null && currentActionIndex >= actions.Count;
        }
    }
}