// File: GhostEventReceiver.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TimeWarp.Core.Data;

namespace TimeWarp.Modules.Paradox
{
    /// <summary>
    /// ADD THIS INTERFACE TO ANY SCRIPT (e.g., weapon, boss logic),
    /// so the script automatically listens to past actions.
    /// </summary>
    public interface IGhostEventHandler
    {
        void OnGhostEvent(TimeEventPayload payload);
    }

    public enum PayloadType { None, SingleFloat, SingleInt, SingleVector, FullPayload }

    [Serializable]
    public class GhostEventBinding
    {
        [Tooltip("Exact name of the event used in RecordEvent (e.g., 'Shoot', 'ThrowGrenade')")]
        public string EventName;
        public PayloadType ExpectedPayload = PayloadType.None;
        
        public UnityEvent OnEventTriggered;
        public UnityEvent<float> OnEventFloatTriggered;
        public UnityEvent<int> OnEventIntTriggered;
        public UnityEvent<Vector3> OnEventVectorTriggered;
        public UnityEvent<TimeEventPayload> OnEventFullPayload;
        
        [HideInInspector] public int EventHash;
    }

    [AddComponentMenu("TimeWarp/Paradox/Ghost Event Receiver")]
    public class GhostEventReceiver : MonoBehaviour
    {
        public List<GhostEventBinding> Bindings = new List<GhostEventBinding>();
        
        // Variable storing all scripts on the Ghost that want to listen to actions
        private IGhostEventHandler[] _eventHandlers;

        private void Awake()
        {
            // 1. Prepare hashes for the visual system (UnityEvents)
            foreach (var binding in Bindings)
            {
                if (!string.IsNullOrEmpty(binding.EventName))
                    binding.EventHash = Animator.StringToHash(binding.EventName);
            }

            // 2. MAGIC LINE: Find all attached scripts with the interface
            _eventHandlers = GetComponentsInChildren<IGhostEventHandler>(true);
        }

        public void ReceiveEvent(TimeEventPayload payload)
        {
            if (payload.EventHash == 0) return;

            // --- UNIVERSAL CODE SYSTEM (Bypasses Unity Inspector) ---
            if (_eventHandlers != null)
            {
                for (int i = 0; i < _eventHandlers.Length; i++)
                {
                    _eventHandlers[i].OnGhostEvent(payload);
                }
            }

            // --- VISUAL SYSTEM (Optional, for UnityEvents) ---
            foreach (var binding in Bindings)
            {
                if (binding.EventHash == payload.EventHash)
                {
                    switch (binding.ExpectedPayload)
                    {
                        case PayloadType.None: binding.OnEventTriggered?.Invoke(); break;
                        case PayloadType.SingleFloat: binding.OnEventFloatTriggered?.Invoke(payload.Float1); break;
                        case PayloadType.SingleInt: binding.OnEventIntTriggered?.Invoke(payload.Int1); break;
                        case PayloadType.SingleVector: binding.OnEventVectorTriggered?.Invoke(payload.Vector1); break;
                        case PayloadType.FullPayload: binding.OnEventFullPayload?.Invoke(payload); break;
                    }
                    return; 
                }
            }
        }
    }
}