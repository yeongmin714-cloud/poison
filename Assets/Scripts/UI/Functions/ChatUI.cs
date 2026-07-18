using UnityEngine;

public class ChatUI : MonoBehaviour
{
    public GameObject chatPanel;
    public Transform messagesContainer;
    public TMPro.TextMeshProUGUI inputField;
    
    private void Start()
    {
        // Initialize chat UI
        Debug.Log("Chat UI initialized");
    }
    
    public void AddMessage(string message)
    {
        // Add message to chat
        // TODO: Complete implementation
    }
}