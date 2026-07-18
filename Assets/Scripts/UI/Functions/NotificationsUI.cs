using UnityEngine;

public class NotificationsUI : MonoBehaviour
{
    public GameObject notificationsPanel;
    public Transform notificationList;
    
    private void Start()
    {
        // Initialize notifications UI
        Debug.Log("Notifications UI initialized");
    }
    
    public void AddNotification(string message)
    {
        // Add a notification
        Debug.Log($"Notification: {message}");
    }
}