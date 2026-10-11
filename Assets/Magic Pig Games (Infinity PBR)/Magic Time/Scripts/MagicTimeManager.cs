using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace MagicPigGames.MagicTime
{
    public class MagicTimeManager : MonoBehaviour
    {
        public static MagicTimeManager Instance { get; private set; }

        [Tooltip("These are the TimeScales that will be created on Awake, and can be accessed by name.")]
        [SerializeField]
        private LocalTimeScale[] initialTimeScales;
        
        // This will hold all the Scriptable Objects in the project, for accessing later.
        public List<LocalTimeScale> allTimeScales = new List<LocalTimeScale>();

        public Dictionary<string, LocalTimeScale> TimeScales { get; } = new Dictionary<string, LocalTimeScale>();

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
                return;
            }

            // Initialize time scales
            InitializeTimeScales();
        }
        
        private void Update()
        {
            foreach (var timeScale in TimeScales.Values)
                timeScale.Tick();
        }

        /// <summary>
        /// This will get the TimeScale with the given name. If it doesn't exist, it will (by default createIfNull value) create a new one with the given initial value.
        /// </summary>
        /// <param name="timeScaleName"></param>
        /// <param name="initialValue"></param>
        /// <param name="createIfNull"></param>
        /// <returns></returns>
        public LocalTimeScale TimeScale(string timeScaleName, float initialValue = 1f, bool createIfNull = true)
        {
            if (TimeScales.TryGetValue(timeScaleName, out var timeScale))
                return timeScale;

            if (!createIfNull)
                return null;
            
            // Look for the object in allTimeScales
            foreach (var localTimeScale in allTimeScales)
            {
                if (localTimeScale.name != timeScaleName) continue;
                
                // Clone the LocalTimeScale to avoid modifying the asset. initialValue will be ignored.
                AddTimeScale(CloneTimeScale(localTimeScale));
                return localTimeScale;
            }
            
            // Create a new LocalTimeScale with the initialValue and the name we are looking for.
            timeScale = ScriptableObject.CreateInstance<LocalTimeScale>();
            timeScale.Initialize(timeScaleName, initialValue);
            AddTimeScale(timeScale);
            return timeScale;
        }

        private void AddTimeScale(LocalTimeScale timeScale)
        {
            if (!TimeScales.TryAdd(timeScale.name, timeScale))
            {
                //Debug.LogWarning($"A TimeScale with the name {timeScale.name} already exists. Skipping.");
            }
        }

        /// <summary>
        /// Removes a TimeScale from the TimeScales dictionary. This will also call Dispose on the TimeScale, removing
        /// all subscribers from it, and it from the subscribers local List.
        /// </summary>
        /// <param name="timeScaleName"></param>
        public void RemoveTimeScale(string timeScaleName)
        {
            if (TimeScales.Remove(timeScaleName, out var timeScale))
                timeScale.Dispose();
        }

        private void InitializeTimeScales()
        {
            foreach (var timeScale in initialTimeScales)
            {
                if (timeScale == null) continue;

                // Clone the initialTimeScale to avoid modifying the asset
                var clonedTimeScale = CloneTimeScale(timeScale);

                var newName = clonedTimeScale.name;
                var removedTrailingSpaces = newName.Trim();
                if (newName != removedTrailingSpaces)
                {
                    Debug.LogWarning($"TimeScale {newName} has trailing spaces. Removing them.");
                    newName = removedTrailingSpaces;
                }

                if (!TimeScales.TryAdd(newName, clonedTimeScale))
                {
                    Debug.LogWarning($"TimeScale with name {newName} already exists. Skipping.");
                    continue;
                }

                clonedTimeScale.Initialize(newName, clonedTimeScale.Value);
                if (clonedTimeScale.AutoRemoveWhenEmpty)
                {
                    Debug.LogWarning($"TimeScale {newName} has AutoRemoveWhenEmpty set to true. For initial TimeScales, this is not allowed. Will set to false.");
                    clonedTimeScale.AutoRemoveWhenEmpty = false;
                }
            }
        }

        /// <summary>
        /// Clones the given LocalTimeScale so that changes made to it during runtime do not affect the original ScriptableObject asset.
        /// </summary>
        private LocalTimeScale CloneTimeScale(LocalTimeScale original)
        {
            // Create a new instance of LocalTimeScale
            var cloned = ScriptableObject.CreateInstance<LocalTimeScale>();

            // Copy values from the original to the cloned instance
            cloned.Initialize(original.name, original.Value);
            cloned.AutoRemoveWhenEmpty = original.AutoRemoveWhenEmpty;
            
            // (Copy other properties as needed)

            return cloned;
        }
    }
}