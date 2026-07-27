using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Core
{
    public class MessageSystem : MonoBehaviour
    {
        private Queue<string> messageQueue;
        private const int MAX_MESSAGES = 100;
        
        public void Initialize()
        {
            messageQueue = new Queue<string>();
        }
        
        public void SendMessage(string message)
        {
            if(messageQueue == null)
                Initialize();
                
            if(messageQueue.Count >= MAX_MESSAGES)
            {
                messageQueue.Dequeue();
            }
            
            messageQueue.Enqueue(message);
            Debug.Log(message);
        }
        
        public string PeekMessage()
        {
            if(messageQueue != null && messageQueue.Count > 0)
                return messageQueue.Peek();
            return null;
        }
        
        public string GetMessage()
        {
            if(messageQueue != null && messageQueue.Count > 0)
                return messageQueue.Dequeue();
            return null;
        }
    }
}