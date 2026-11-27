using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using TimeWarp.Core;

namespace TimeWarp.Modules.Gameplay
{
    /// <summary>
    /// Acts as a "Kill Switch" for the TimeWarp system.
    /// Safely disables foreign logic components (e.g., shooting, AI, movement) during 
    /// time anomalies (Rewind, Time Stop) to prevent them from interfering with the time buffer.
    /// Manages the lifecycle of scripts implementing the ITimeWarpStateListener interface.
    /// </summary>
    [RequireComponent(typeof(TimeWarpIdentity))]
    [AddComponentMenu("TimeWarp/Gameplay/Time Warp Component Manager")]
    public class TimeWarpComponentManager : MonoBehaviour, ITimeWarpModule
    {
        [Header("Rules (Disable Rules)")]
        [Tooltip("If true, target components will be disabled while the object is rewinding.")]
        public bool DisableOnRewind = true;
        
        [Tooltip("If true, target components will be disabled during a complete Time Stop (Scale <= 0.001f).")]
        public bool DisableOnTimeStop = true;

        [Header("Target Components")]
        [Tooltip("List of foreign scripts (Behaviours) that should be managed by this Kill Switch.")]
        public List<Behaviour> TargetComponents = new List<Behaviour>();

        [Header("Foreign Script Events")]
        [Tooltip("Invoked every frame while rewinding. Useful for updating visual-only scripts without an interface.")]
        public UnityEvent OnRewindUpdate;
        
        [Tooltip("Invoked once when time returns to normal (1.0).")]
        public UnityEvent OnResume;

        private TimeWarpIdentity _identity;
        private bool _componentsAreDisabledByUs = false;
        
        // Array of scripts on this object that explicitly asked to be informed about time changes
        private ITimeWarpStateListener[] _autoListeners;

        /// <summary>
        /// Represents the current logical state of the object regarding time anomalies.
        /// </summary>
        private enum ComponentState { Normal, SlowMo, TimeStop, Rewind }
        private ComponentState _lastState = ComponentState.Normal;
        
        // Memory of the previous offset to calculate precise Rewind DeltaTime
        private float _lastOffset = 0f;

        private void Awake()
        {
            _identity = GetComponent<TimeWarpIdentity>();
            // Automatically find all scripts implementing the interface on this object and its children
            _autoListeners = GetComponentsInChildren<ITimeWarpStateListener>(true);
        }

        public void OnWarpFixedUpdate() { }

        public void OnWarpUpdate()
        {
            if (_identity == null || !_identity.IsRegistered || TargetComponents.Count == 0) return;

            // 1. Gather parameters from the Identity
            float scale = _identity.EffectiveTimeScale;
            float currentOffset = _identity.FractionalOffset;
            
            bool isRewinding = _identity.IsRewinding || currentOffset > 0.001f;
            bool isTimeStopped = Mathf.Abs(scale) <= 0.001f && !isRewinding;
            bool isSlowMo = scale < 0.99f && !isTimeStopped && !isRewinding;

            // 2. Determine the current logical state
            ComponentState newState = ComponentState.Normal;
            if (isRewinding) newState = ComponentState.Rewind;
            else if (isTimeStopped) newState = ComponentState.TimeStop;
            else if (isSlowMo) newState = ComponentState.SlowMo;

            // ==========================================
            // 3. KILL SWITCH - CONTINUOUS RECONCILIATION
            // ==========================================
            // BUG FIX: re-enabling used to live inside the "newState == Normal" branch, so
            // components were only ever restored when time returned to EXACTLY normal.
            //
            // That broke the very common case of rewinding while standing in a Slow Motion
            // zone: Rewind -> scripts disabled, then the rewind ends but the object is still
            // in the zone, so the new state is SlowMo, not Normal. The transition was
            // handled by the "else" branch, which only ever DISABLES - nothing switched the
            // scripts back on. The character stayed frozen and unresponsive until it left
            // the zone entirely.
            //
            // The enable/disable decision is now derived from the CURRENT state on every
            // frame instead of from a specific transition, so any path out of an anomaly
            // restores the scripts. It is two boolean tests per frame and cannot drift.
            bool shouldBeDisabled =
                (newState == ComponentState.Rewind   && DisableOnRewind) ||
                (newState == ComponentState.TimeStop && DisableOnTimeStop);

            bool justRestored = false;

            if (shouldBeDisabled && !_componentsAreDisabledByUs)
            {
                ToggleComponents(false);
                _componentsAreDisabledByUs = true;
            }
            else if (!shouldBeDisabled && _componentsAreDisabledByUs)
            {
                ToggleComponents(true);
                _componentsAreDisabledByUs = false;
                justRestored = true;
            }

            // 4. Detect STATE CHANGES (Fire One-Time Events from the Interface)
            if (newState != _lastState)
            {
                // Resume fires when time is fully normal again, and also whenever the
                // scripts were actually switched back on - e.g. Rewind -> SlowMo, where
                // the object is running again even though time is not back to 1.0.
                if (newState == ComponentState.Normal || justRestored)
                {
                    OnResume?.Invoke();

                    if (_autoListeners != null) 
                    {
                        foreach (var listener in _autoListeners) 
                        {
                            // Safely check if the object hasn't been destroyed
                            if (listener as MonoBehaviour != null) listener.OnTimeWarpResume();
                        }
                    }
                }

                if (newState != ComponentState.Normal)
                {
                    // Inform listening scripts EXACTLY what state just started
                    if (newState == ComponentState.SlowMo && _autoListeners != null)
                    {
                        foreach (var listener in _autoListeners) 
                            if (listener as MonoBehaviour != null) listener.OnTimeWarpSlowMoStart(scale);
                    }
                    else if (newState == ComponentState.TimeStop && _autoListeners != null)
                    {
                        foreach (var listener in _autoListeners) 
                            if (listener as MonoBehaviour != null) listener.OnTimeWarpTimeStopStart();
                    }
                    else if (newState == ComponentState.Rewind && _autoListeners != null)
                    {
                        foreach (var listener in _autoListeners) 
                            if (listener as MonoBehaviour != null) listener.OnTimeWarpRewindStart();
                    }
                }
                
                _lastState = newState;
            }

            // 5. CONTINUOUS UPDATE (When scripts are asleep/disabled by the Kill Switch)
            if (newState == ComponentState.Rewind || newState == ComponentState.TimeStop)
            {
                if (newState == ComponentState.Rewind) OnRewindUpdate?.Invoke();
                
                // Calculate precise DeltaTime for time rewinding
                float framerate = Core.Managers.TimeWarpManager.Instance.CaptureFramerate;
                float rewindDt = isRewinding ? -((currentOffset - _lastOffset) / framerate) : 0f;

                if (_autoListeners != null) 
                {
                    foreach (var listener in _autoListeners) 
                    {
                        if (listener as MonoBehaviour != null) listener.OnTimeWarpAnomalyUpdate(scale, rewindDt);
                    }
                }
            }

            _lastOffset = currentOffset;
        }

        /// <summary>
        /// Enables or disables all registered target components.
        /// </summary>
        private void ToggleComponents(bool state)
        {
            for (int i = 0; i < TargetComponents.Count; i++)
            {
                if (TargetComponents[i] != null) TargetComponents[i].enabled = state;
            }
        }

        private void OnDisable()
        {
            // Failsafe: if this manager is disabled or destroyed, ensure we don't leave foreign scripts permanently disabled
            if (_componentsAreDisabledByUs)
            {
                ToggleComponents(true);
                _componentsAreDisabledByUs = false;
            }
        }

        // ==========================================
        // EDITOR TOOLS
        // ==========================================

        /// <summary>
        /// Automatically finds and adds all foreign scripts on this object and its children.
        /// Ignores Unity Core components and TimeWarp internal components.
        /// </summary>
        [ContextMenu("Auto-Fill: Find foreign scripts")]
        public void AutoFillComponents()
        {
            Behaviour[] allBehaviours = GetComponentsInChildren<Behaviour>(true);
            foreach (var comp in allBehaviours)
            {
                if (comp == null) continue;
                string nameSpace = comp.GetType().Namespace;
                bool isUnityCore = nameSpace != null && nameSpace.StartsWith("UnityEngine");
                bool isTimeWarpCore = nameSpace != null && nameSpace.StartsWith("TimeWarp");
                bool isMe = comp == this;

                if (!isUnityCore && !isTimeWarpCore && !isMe && !TargetComponents.Contains(comp))
                {
                    TargetComponents.Add(comp);
                }
            }
        }

        /// <summary>
        /// Adds specific components by their class name. Used heavily by TimeWarpAutoSetup.
        /// </summary>
        public void AddComponentsByName(List<string> componentNames)
        {
            if (componentNames == null || componentNames.Count == 0) return;
            Behaviour[] allBehaviours = GetComponentsInChildren<Behaviour>(true);
            foreach (var comp in allBehaviours)
            {
                if (comp == null) continue;
                if (componentNames.Contains(comp.GetType().Name) && !TargetComponents.Contains(comp))
                {
                    TargetComponents.Add(comp);
                }
            }
        }
        
        [ContextMenu("Clear list")]
        private void ContextMenuClear() { TargetComponents.Clear(); }
    }
}