using UnityEngine;
using System.Collections.Generic;

namespace UI.Core
{
    public class GameEventSystem : MonoBehaviour
    {
        private Dictionary<string, System.Action> eventListeners = new Dictionary<string, System.Action>();

        public static GameEventSystem Instance { get; private set; }

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

        public void Subscribe(string eventName, System.Action callback)
        {
            if (eventListeners.ContainsKey(eventName))
            {
                eventListeners[eventName] += callback;
            }
            else
            {
                eventListeners[eventName] = callback;
            }
        }

        public void Unsubscribe(string eventName, System.Action callback)
        {
            if (eventListeners.ContainsKey(eventName))
            {
                eventListeners[eventName] -= callback;
            }
        }

        public void RaiseEvent(string eventName)
        {
            if (eventListeners.TryGetValue(eventName, out System.Action callback))
            {
                callback?.Invoke();
            }
        }
    }
}