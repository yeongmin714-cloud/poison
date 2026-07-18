using UnityEngine;
using UnityEngine.UI;

namespace UI.Functions
{
    public class UIMessageSystem : MonoBehaviour
    {
        [Header("UI References")]
        public Text messageText;
        public GameObject messagePanel;
        
        [Header("Message Settings")]
        public float displayTime = 3f;

        public void ShowMessage(string message)
        {
            messageText.text = message;
            messagePanel.SetActive(true);
            
            // Auto-hide after specified time
            Invoke("HideMessage", displayTime);
        }

        private void HideMessage()
        {
            messagePanel.SetActive(false);
        }
    }
}