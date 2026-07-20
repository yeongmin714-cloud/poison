using UnityEngine;
using System.Collections.Generic;

namespace UI.Core
{
    public class GameEventSystem : MonoBehaviour
    {
        public static GameEventSystem Instance { get; private set; }

        private Dictionary<string, List<System.Action<object>>> _eventHandlers = new Dictionary<string, List<System.Action<object>>>();

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

        public void Subscribe(string eventName, System.Action<object> handler)
        {
            if (!_eventHandlers.ContainsKey(eventName))
            {
                _eventHandlers.Add(eventName, new List<System.Action<object>>());
            }
            _eventHandlers[eventName].Add(handler);
        }

        public void Unsubscribe(string eventName, System.Action<object> handler)
        {
            if (_eventHandlers.ContainsKey(eventName))
            {
                _eventHandlers[eventName].Remove(handler);
            }
        }

        public void Publish(string eventName, object data)
        {
            if (_eventHandlers.TryGetValue(eventName, out List<System.Action<object>> handlers))
            {
                foreach (var handler in handlers)
                {
                    handler?.Invoke(data);
                }
            }
        }
    }
}