using UnityEngine;
using System.Collections.Generic;

namespace UI.Core
{
    public class EventSystemManager : MonoBehaviour
    {
        public static EventSystemManager Instance { get; private set; }

        private Dictionary<string, List<System.Action>> _eventHandlers = new Dictionary<string, List<System.Action>>();

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

        public void Subscribe(string eventName, System.Action handler)
        {
            if (!_eventHandlers.ContainsKey(eventName))
            {
                _eventHandlers.Add(eventName, new List<System.Action>());
            }
            _eventHandlers[eventName].Add(handler);
        }

        public void Unsubscribe(string eventName, System.Action handler)
        {
            if (_eventHandlers.ContainsKey(eventName))
            {
                _eventHandlers[eventName].Remove(handler);
            }
        }

        public void Publish(string eventName)
        {
            if (_eventHandlers.TryGetValue(eventName, out List<System.Action> handlers))
            {
                foreach (var handler in handlers)
                {
                    handler?.Invoke();
                }
            }
        }
    }
}