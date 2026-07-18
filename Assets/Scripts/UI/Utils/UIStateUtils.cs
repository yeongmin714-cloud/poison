using UnityEngine;
using UnityEngine.UI;

namespace UI.Utils
{
    public class UIStateUtils : MonoBehaviour
    {
        [Header("State Settings")]
        public string currentState = "Idle";

        public void SetState(string state)
        {
            currentState = state;
            Debug.Log($"State changed to: {state}");
        }

        public string GetState()
        {
            return currentState;
        }

        public bool IsInState(string state)
        {
            return currentState == state;
        }

        public void UpdateState()
        {
            // Update state logic
            Debug.Log("Updating state");
        }
    }
}