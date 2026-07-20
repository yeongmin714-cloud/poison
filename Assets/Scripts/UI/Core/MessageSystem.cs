using UnityEngine;
using System.Collections.Generic;

namespace UI.Core
{
    public class MessageSystem : MonoBehaviour
    {
        public static MessageSystem Instance { get; private set; }

        private Queue<string> _messages = new Queue<string>();

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
            _messages.Enqueue(message);
        }

        public string GetMessage()
        {
            if (_messages.Count > 0)
            {
                return _messages.Dequeue();
            }
            return null;
        }

        public int GetMessageCount()
        {
            return _messages.Count;
        }
    }
}