using UnityEngine;
using UnityEngine.UI;

namespace UI.Functions
{
    public class UIChatSystem : MonoBehaviour
    {
        [Header("UI References")]
        public Text chatMessages;
        public InputField messageInput;
        public Button sendButton;
        public RectTransform chatPanel;
        
        [Header("Chat Settings")]
        public int maxMessages = 100;

        private void Start()
        {
            InitializeChat();
        }

        public void InitializeChat()
        {
            // Set up chat UI
            messageInput.onEndEdit.AddListener(SendMessage);
            sendButton.onClick.AddListener(SendMessage);
        }

        public void SendMessage(string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                // Add message to chat
                chatMessages.text += $"{message}\n";
                
                // Limit messages to maxMessages
                string[] messages = chatMessages.text.Split('\n');
                if (messages.Length > maxMessages)
                {
                    chatMessages.text = string.Join("\n", messages, messages.Length - maxMessages, maxMessages);
                }
                
                // Clear input
                messageInput.text = "";
            }
        }

        public void AddMessage(string message)
        {
            chatMessages.text += $"{message}\n";
        }
    }
}