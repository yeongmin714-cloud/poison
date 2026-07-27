using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Core
{
    public class GameEventSystem : MonoBehaviour
    {
        private Dictionary<string, System.Action> eventHandlers;
        
        public void Initialize()
        {
            eventHandlers = new Dictionary<string, System.Action>();
        }
        
        public void TriggerEvent(string eventName)
        {
            if(eventHandlers.ContainsKey(eventName))
            {
                eventHandlers[eventName]();
            }
        }
        
        public void RegisterEventHandler(string eventName, System.Action handler)
        {
            if(!eventHandlers.ContainsKey(eventName))
            {
                eventHandlers[eventName] = handler;
            }
            else
            {
                eventHandlers[eventName] += handler;
            }
        }
    }
}