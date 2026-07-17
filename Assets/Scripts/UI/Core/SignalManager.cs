using UnityEngine;
using System.Collections.Generic;

namespace ProjectName.UI.Core
{
    public class SignalManager : MonoBehaviour
    {
        private Dictionary<string, System.Action<object>> signalListeners = new Dictionary<string, System.Action<object>>();

        public static SignalManager Instance { get; private set; }

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

        public void Subscribe(string signalName, System.Action<object> callback)
        {
            if (signalListeners.ContainsKey(signalName))
            {
                signalListeners[signalName] += callback;
            }
            else
            {
                signalListeners[signalName] = callback;
            }
        }

        public void Unsubscribe(string signalName, System.Action<object> callback)
        {
            if (signalListeners.ContainsKey(signalName))
            {
                signalListeners[signalName] -= callback;
            }
        }

        public void SendSignal(string signalName, object data = null)
        {
            if (signalListeners.TryGetValue(signalName, out System.Action<object> callback))
            {
                callback?.Invoke(data);
            }
        }
    }
}