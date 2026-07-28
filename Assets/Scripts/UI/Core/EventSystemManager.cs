using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Core
{
    public class EventSystemManager : MonoBehaviour
    {
        private Dictionary<string, System.Action> eventHandlers;

        public void Initialize()
        {
            eventHandlers = new Dictionary<string, System.Action>();
        }

        public void RegisterEvent(string eventName, System.Action callback)
        {
            if(!eventHandlers.ContainsKey(eventName))
            {
                eventHandlers[eventName] = callback;
            }
            else
            {
                eventHandlers[eventName] += callback;
            }
        }

        public void TriggerEvent(string eventName)
        {
            if(eventHandlers.ContainsKey(eventName))
            {
                eventHandlers[eventName]();
            }
        }
    }
}