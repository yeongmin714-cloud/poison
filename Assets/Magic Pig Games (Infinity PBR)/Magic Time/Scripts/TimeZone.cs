using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace MagicPigGames.MagicTime
{
    [Serializable]
    public class TimeZoneEventData
    {
        public GameObject gameObject;
        public Collider collider;
        public Vector3 contactPoint;

        public TimeZoneEventData(GameObject obj, Collider col, Vector3 point)
        {
            gameObject = obj;
            collider = col;
            contactPoint = point;
        }
    }

    [Serializable]
    public class TimeZoneUnityEvent : UnityEvent<TimeZoneEventData> { }

    public class TimeZone : MonoBehaviour
    {
        [Tooltip("The LocalTimeScale applied to objects in this zone. If none is assigned, a new one will be created.")]
        public LocalTimeScale timeScaleToUse;

        [Tooltip("If a new LocalTimeScale is created, this value will be used as its initial value.")]
        [SerializeField]
        protected float timeScaleValue = 1f;
        
        protected LocalTimeScale timeScale;
        public LocalTimeScale TimeScale => timeScale;
        public float TimeScaleValue => timeScaleValue;
        
        [HideInInspector] public bool createLocalTime = true;

        public TimeZoneUnityEvent OnObjectEnter;
        public TimeZoneUnityEvent OnObjectExit;
        
        public void SetTimeScaleStartingValue(float value) => timeScaleValue = value;

        protected virtual void Awake()
        {
            // Ensure the Collider is set as a trigger
            var col = GetComponent<Collider>();
            if (col != null && !col.isTrigger)
            {
                col.isTrigger = true;
                Debug.LogWarning($"{name}'s Collider was not set as a trigger. It has been set to trigger mode.");
            }

            // If we are creating a new LocalTimeScale, do so now
            if (createLocalTime || timeScaleToUse == null)
            {
                CreateLocalTimeScale();
                return;
            }
            
            // Otherwise create a new instance of the assigned timeScaleToUse Scriptable Object
            timeScale = Instantiate(timeScaleToUse);
            var timeScaleName = string.Concat(name, "_", timeScaleToUse.name, "_TimeZoneTimeScale");
            timeScale.Initialize(timeScaleName, timeScaleToUse.Value);
        }

        private void CreateLocalTimeScale()
        {
            timeScale = ScriptableObject.CreateInstance<LocalTimeScale>();
            var timeScaleName = string.Concat(name, "_TimeZoneTimeScale");
            timeScale.Initialize(timeScaleName, timeScaleValue);
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            if (other.isTrigger) return;

            var localTimeUser = other.GetComponentInParent<IHaveLocalTime>();
            if (localTimeUser == null) return;

            OnEnter(localTimeUser, other);
        }

        protected virtual void OnTriggerExit(Collider other)
        {
            if (other.isTrigger) return;
            
            var localTimeUser = other.GetComponentInParent<IHaveLocalTime>();
            if (localTimeUser == null) return;
            
            OnExit(localTimeUser, other);
        }

        protected virtual void OnTriggerEnter2D(Collider2D other)
        {
            if (other.isTrigger) return;
            
            var localTimeUser = other.GetComponentInParent<IHaveLocalTime>();
            if (localTimeUser == null) return;
            
            OnEnter(localTimeUser, null);
        }

        protected virtual void OnTriggerExit2D(Collider2D other)
        {
            if (other.isTrigger) return;
            
            var localTimeUser = other.GetComponentInParent<IHaveLocalTime>();
            if (localTimeUser == null) return;
            
            OnExit(localTimeUser, null);
        }

        /// <summary>
        /// Called when an IHaveLocalTime object enters the zone.
        /// </summary>
        /// <param name="localTimeUser">The object that entered the zone.</param>
        /// <param name="otherCollider">The collider that triggered the event.</param>
        protected virtual void OnEnter(IHaveLocalTime localTimeUser, Collider otherCollider)
        {
            localTimeUser.SubscribeToLocalTimeScale(timeScale);
            
            if (OnObjectEnter != null && OnObjectEnter.GetPersistentEventCount() > 0)
            {
                var contactPoint = otherCollider != null ? otherCollider.ClosestPoint(transform.position) : localTimeUser.GameObject.transform.position;
                var eventData = new TimeZoneEventData(localTimeUser.GameObject, otherCollider, contactPoint);
                OnObjectEnter.Invoke(eventData);
            }
        }

        /// <summary>
        /// Called when an IHaveLocalTime object exits the zone.
        /// </summary>
        /// <param name="localTimeUser">The object that exited the zone.</param>
        /// <param name="otherCollider">The collider that triggered the event.</param>
        protected virtual void OnExit(IHaveLocalTime localTimeUser, Collider otherCollider)
        {
            localTimeUser.UnsubscribeFromLocalTimeScale(timeScale);
            
            if (OnObjectExit != null && OnObjectExit.GetPersistentEventCount() > 0)
            {
                var contactPoint = otherCollider != null ? otherCollider.ClosestPoint(transform.position) : localTimeUser.GameObject.transform.position;
                var eventData = new TimeZoneEventData(localTimeUser.GameObject, otherCollider, contactPoint);
                OnObjectExit.Invoke(eventData);
            }
        }

        protected virtual void OnDestroy()
        {
            if (timeScale == null) return;
         
            // Dispose of the timeScale if it was created by this zone
            timeScale.Dispose();
            timeScale = null;
        }

        protected virtual void OnDisable()
        {
            if (timeScale == null) return;

            TimeScale.UnsubscribeAll();
        }
    }
}