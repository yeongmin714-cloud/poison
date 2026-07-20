using UnityEngine;
using System.Collections.Generic;

namespace UI.Core
{
    public class SignalManager : MonoBehaviour
    {
        public static SignalManager Instance { get; private set; }

        private Dictionary<string, List<System.Action>> _signals = new Dictionary<string, List<System.Action>>();

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

        public void Subscribe(string signalName, System.Action callback)
        {
            if (!_signals.ContainsKey(signalName))
            {
                _signals.Add(signalName, new List<System.Action>());
            }
            _signals[signalName].Add(callback);
        }

        public void Unsubscribe(string signalName, System.Action callback)
        {
            if (_signals.ContainsKey(signalName))
            {
                _signals[signalName].Remove(callback);
            }
        }

        public void Publish(string signalName)
        {
            if (_signals.TryGetValue(signalName, out List<System.Action> callbacks))
            {
                foreach (var callback in callbacks)
                {
                    callback?.Invoke();
                }
            }
        }
    }
}