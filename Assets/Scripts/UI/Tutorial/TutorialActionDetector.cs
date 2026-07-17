using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.UI.Tutorial
{
    public class TutorialActionDetector : MonoBehaviour
    {
        [Header("Action Detection Settings")]
        public TutorialActionType actionType;
        public GameObject targetObject;
        public string requiredTag = "Player";
        public float detectionRadius = 5f;
        
        [Header("Events")]
        public UnityEngine.Events.UnityEvent onActionDetected;
        
        private void Update()
        {
            if (actionType == TutorialActionType.Interact && Input.GetButtonDown("Interact"))
            {
                DetectAction();
            }
        }
        
        private void DetectAction()
        {
            if (targetObject != null)
            {
                // Check if player is in range
                if (Vector3.Distance(transform.position, targetObject.transform.position) <= detectionRadius)
                {
                    // Check if target has required tag
                    if (targetObject.CompareTag(requiredTag))
                    {
                        onActionDetected?.Invoke();
                    }
                }
            }
        }
    }
    
    public enum TutorialActionType
    {
        Interact,
        Move,
        Jump,
        Attack
    }
}