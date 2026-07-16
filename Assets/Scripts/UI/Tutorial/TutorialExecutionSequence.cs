using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UI.Tutorial
{
    public class TutorialExecutionSequence : MonoBehaviour
    {
        [Header("Sequence Settings")]
        public TutorialStep[] steps;
        public bool loopSequence = false;
        public float delayBetweenSteps = 1f;
        
        [Header("State")]
        public int currentStepIndex = 0;
        public bool isExecuting = false;
        
        private void Start()
        {
            if (steps.Length == 0)
            {
                Debug.LogError("No tutorial steps defined");
                return;
            }
        }
        
        public void StartSequence()
        {
            if (isExecuting) return;
            
            isExecuting = true;
            currentStepIndex = 0;
            ExecuteCurrentStep();
        }
        
        private void ExecuteCurrentStep()
        {
            if (currentStepIndex >= steps.Length)
            {
                if (loopSequence)
                {
                    currentStepIndex = 0;
                }
                else
                {
                    isExecuting = false;
                    return;
                }
            }
            
            if (steps[currentStepIndex] != null)
            {
                steps[currentStepIndex].ExecuteStep();
            }
            
            // Schedule next step
            Invoke(nameof(NextStep), delayBetweenSteps);
        }
        
        private void NextStep()
        {
            currentStepIndex++;
            ExecuteCurrentStep();
        }
    }
    
    [System.Serializable]
    public class TutorialStep
    {
        public string stepName;
        public string description;
        public TutorialActionType requiredAction;
        public float duration = 5f;
        public UnityEngine.Events.UnityEvent onStepComplete;
        
        public void ExecuteStep()
        {
            // Implementation for executing the tutorial step
        }
    }
}