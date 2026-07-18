using UnityEngine;
using UnityEngine.UI;

namespace UI.Functions
{
    public class UINotificationSystem : MonoBehaviour
    {
        [Header("UI References")]
        public Text notificationText;
        public Image notificationIcon;
        public GameObject notificationPanel;
        
        [Header("Notification Settings")]
        public float displayTime = 3f;

        public void ShowNotification(string message, Sprite icon = null)
        {
            notificationText.text = message;
            if (icon != null)
            {
                notificationIcon.sprite = icon;
            }
            notificationPanel.SetActive(true);
            
            // Auto-hide after specified time
            Invoke("HideNotification", displayTime);
        }

        private void HideNotification()
        {
            notificationPanel.SetActive(false);
        }
    }
}