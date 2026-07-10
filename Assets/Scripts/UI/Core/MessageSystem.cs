using UnityEngine;
using System.Collections.Generic;

namespace UI.Core
{
    public class MessageSystem : MonoBehaviour
    {
        private List<string> messages = new List<string>();

        public static MessageSystem Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void AddMessage(string message)
        {
            messages.Add(message);
        }

        public string GetMessage(int index)
        {
            if (index >= 0 && index < messages.Count)
            {
                return messages[index];
            }
            return string.Empty;
        }

        public void ClearMessages()
        {
            messages.Clear();
        }
    }
}