using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Tutorial
{
    public class TutorialExecutionSequence : MonoBehaviour
    {
        [SerializeField] private List<TutorialActionDetector> actions;
        
        public void ExecuteSequence()
        {
            foreach (var action in actions)
            {
                Debug.Log($"Executing action: {action.ActionName}");
            }
        }
    }
}