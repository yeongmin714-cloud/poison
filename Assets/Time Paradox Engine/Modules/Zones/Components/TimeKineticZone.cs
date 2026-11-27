using UnityEngine;
using System.Collections.Generic;
using TimeWarp.Core;
using TimeWarp.Core.Managers;

namespace TimeWarp.Modules.Zones.Components
{
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("TimeWarp/Zones/Time Kinetic Zone")]
    public class TimeKineticZone : MonoBehaviour
    {
        [Header("Tracking")]
        public Transform PlayerTransform;

        [Header("Kinetic Settings")]
        [Range(0f, 1f)] public float IdleTimeScale = 0.05f;
        [Range(0f, 3f)] public float MaxTimeScale = 1.0f;
        public float SpeedRequiredForMaxTime = 5.0f;
        public float TimeSmoothing = 15f;

        [Header("Zone Restrictions")]
        public bool BlockRewindInside = true;
        public bool YieldToGlobalActions = true;

        // ==========================================
        // NEW: OBJECT FILTERING SYSTEM
        // ==========================================
        [Header("Filtering")]
        [Tooltip("Layers the zone should affect. Defaults to 'Everything'.")]
        public LayerMask AffectedLayers = ~0; 

        [Tooltip("OPTIONAL tag filter, combined with Affected Layers using OR.\n\n" +
                 "Note: this zone does not use a TimeProfile, so its filtering is configured " +
                 "here rather than on a shared asset.\n\n" +
                 "Leave empty for the previous layer-only behaviour.")]
        public List<string> AffectedTags = new List<string>();

        [Tooltip("Leave empty for the zone to affect everyone on the selected layer. If you add specific objects here, the zone will ONLY affect them.")]
        public List<TimeWarpIdentity> SpecificTargets = new List<TimeWarpIdentity>();

        private Vector3 _lastPlayerPos;
        private float _currentZoneScale;
        private bool _isPlayerInZone = false; 
        
        private HashSet<TimeWarpIdentity> _affectedTargets = new HashSet<TimeWarpIdentity>();
        private Dictionary<TimeWarpIdentity, bool> _originalRewindStates = new Dictionary<TimeWarpIdentity, bool>();

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void Start()
        {
            if (PlayerTransform != null) _lastPlayerPos = PlayerTransform.position;
            _currentZoneScale = IdleTimeScale;
        }

        private void Update()
        {
            if (PlayerTransform == null) return;

            float moveDistance = Vector3.Distance(PlayerTransform.position, _lastPlayerPos);
            float playerSpeed = moveDistance / Time.unscaledDeltaTime;
            _lastPlayerPos = PlayerTransform.position;

            float speedRatio = Mathf.Clamp01(playerSpeed / SpeedRequiredForMaxTime);
            float targetScale = Mathf.Lerp(IdleTimeScale, MaxTimeScale, speedRatio);
            _currentZoneScale = Mathf.Lerp(_currentZoneScale, targetScale, Time.unscaledDeltaTime * TimeSmoothing);

            if (!_isPlayerInZone) return;

            bool isGlobalActionActive = TimeWarpManager.Instance != null && TimeWarpManager.Instance.GlobalTimeState != TimeState.Normal;

            _affectedTargets.RemoveWhere(t => t == null || t.gameObject == null);

            foreach (var target in _affectedTargets)
            {
                if (YieldToGlobalActions && isGlobalActionActive) continue; 
                
                target.LocalTimeScale = _currentZoneScale;
            }
        }

        // Changed IsValidTarget method: now checks specific references!
        private bool IsValidTarget(TimeWarpIdentity identity)
        {
            // 1. Check layer (LayerMask)
            if (!TimeWarpTagUtility.MatchesLayerOrTag(identity.gameObject, AffectedLayers, AffectedTags))
                return false;

            // 2. Check specific objects (if the list is not empty)
            if (SpecificTargets != null && SpecificTargets.Count > 0)
            {
                // If this specific object is not on our selected list, ignore it
                if (!SpecificTargets.Contains(identity))
                    return false;
            }

            return true;
        }

        private void OnTriggerEnter(Collider other)
        {
            TimeWarpIdentity identity = null;
            if (other.attachedRigidbody != null) other.attachedRigidbody.TryGetComponent(out identity);
            else other.TryGetComponent(out identity);

            if (identity != null)
            {
                if (identity.transform == PlayerTransform)
                {
                    _isPlayerInZone = true;
                }
                else
                {
                    // Pass the identity through our new object filter
                    if (IsValidTarget(identity))
                    {
                        _affectedTargets.Add(identity);

                        if (BlockRewindInside && identity.ActionFilters != null)
                        {
                            _originalRewindStates[identity] = identity.ActionFilters.AllowRewind;
                            identity.ActionFilters.AllowRewind = false;
                        }
                    }
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            TimeWarpIdentity identity = null;
            if (other.attachedRigidbody != null) other.attachedRigidbody.TryGetComponent(out identity);
            else other.TryGetComponent(out identity);

            if (identity != null)
            {
                if (identity.transform == PlayerTransform)
                {
                    _isPlayerInZone = false;
                    foreach (var target in _affectedTargets)
                    {
                        if (target != null) target.LocalTimeScale = 1.0f;
                    }
                }
                else if (_affectedTargets.Contains(identity))
                {
                    _affectedTargets.Remove(identity);
                    identity.LocalTimeScale = 1.0f; 

                    if (BlockRewindInside && _originalRewindStates.ContainsKey(identity))
                    {
                        if (identity.ActionFilters != null) 
                            identity.ActionFilters.AllowRewind = _originalRewindStates[identity];
                        
                        _originalRewindStates.Remove(identity);
                    }
                }
            }
        }

        // ==========================================
        // SCENE VIEW VISUALISATION
        // ==========================================
        [Header("Editor")]
        [Tooltip("Draw this zone in the Scene view. Editor only - has no effect in a build.")]
        public bool DrawGizmo = true;

        private void OnDrawGizmos()
        {
            if (!DrawGizmo) return;

            Collider col = GetComponent<Collider>();
            if (col == null) return;

            // Kinetic zones have no TimeProfile - time scale is driven by object speed,
            // so the label reports the speed->time mapping instead of a profile name.
            Color c = TimeWarpZoneGizmos.KineticColor;
            TimeWarpZoneGizmos.DrawCollider(col, c);

            string info = "Time Kinetic Zone\n" +
                          "idle x" + IdleTimeScale.ToString("0.##") +
                          " -> max x" + MaxTimeScale.ToString("0.##") + "\n" +
                          "full speed at " + SpeedRequiredForMaxTime.ToString("0.##") + " u/s" +
                          (BlockRewindInside ? "\nrewind blocked" : string.Empty);

            TimeWarpZoneGizmos.DrawLabel(TimeWarpZoneGizmos.LabelAnchor(col.bounds), info, c);
        }
    }
}