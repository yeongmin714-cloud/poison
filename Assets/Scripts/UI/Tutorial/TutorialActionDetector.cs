using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Tutorial
{
    public class TutorialActionDetector : MonoBehaviour
    {
        [SerializeField] private string actionName;
        [SerializeField] private bool isCompleted = false;
        
        public string ActionName => actionName;
        public bool IsCompleted => isCompleted;
        
        public void MarkAsCompleted()
        {
            isCompleted = true;
        }
        
        public void ResetAction()
        {
            isCompleted = false;
        }
    }
}