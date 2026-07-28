using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Core
{
    public class SignalManager : MonoBehaviour
    {
        private Dictionary<string, System.Action> signalHandlers;

        public void Initialize()
        {
            signalHandlers = new Dictionary<string, System.Action>();
        }

        public void SendSignal(string signalName)
        {
            if(signalHandlers.ContainsKey(signalName))
            {
                signalHandlers[signalName]();
            }
        }

        public void RegisterSignalHandler(string signalName, System.Action handler)
        {
            if(!signalHandlers.ContainsKey(signalName))
            {
                signalHandlers[signalName] = handler;
            }
            else
            {
                signalHandlers[signalName] += handler;
            }
        }
    }
}