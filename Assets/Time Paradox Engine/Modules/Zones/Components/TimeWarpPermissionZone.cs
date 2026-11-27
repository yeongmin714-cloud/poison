using UnityEngine;
using System.Collections.Generic;
using TimeWarp.Core;

namespace TimeWarp.Modules.Zones
{
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("TimeWarp/Zones/Time Warp Permission Zone")]
    public class TimeWarpPermissionZone : MonoBehaviour
    {
        [Header("Activation Settings")]
        [Tooltip("If true, ANY TimeWarpIdentity can activate this zone.")]
        public bool AllowAnyTimeWarpObject = false;

        [Tooltip("RECOMMENDED. Objects carrying any of these tags may activate the zone.\n\n" +
                 "Prefer this over the prefab list below: prefab matching compares GameObject " +
                 "NAMES, which silently breaks when an object is renamed, when two prefabs " +
                 "share a name, or when Unity appends \" (1)\" to a scene duplicate.")]
        public List<string> AllowedActivatorTags = new List<string>();

        [Tooltip("OPTIONAL. Layers allowed to activate the zone. Set to Nothing to ignore.")]
        public LayerMask AllowedActivatorLayers = 0;

        [Tooltip("LEGACY. Drag the prefabs that are allowed to trigger this zone.\n\n" +
                 "Kept for backwards compatibility - matching is done by comparing the " +
                 "GameObject's name to the prefab's name, which is fragile. Use the tag " +
                 "list above for new setups.")]
        public List<GameObject> AllowedActivatorPrefabs = new List<GameObject>();

        // Changed to a List for the fastest iteration in a for loop
        private List<Collider> _collidersInside = new List<Collider>();
        private Collider[] _overlapResults = new Collider[64];

        // OPTIMIZATION: This is no longer a property (get). It's a standard, lightweight variable read from memory in a fraction of a nanosecond.
        [HideInInspector] 
        public bool IsZoneActive = false;

        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void Start()
        {
            Collider col = GetComponent<Collider>();
            if (col == null) return;

            int overlapCount = 0;
            if (col is BoxCollider box)
            {
                Vector3 realExtents = Vector3.Scale(box.size, transform.lossyScale) * 0.5f;
                Vector3 realCenter = transform.TransformPoint(box.center);
                overlapCount = UnityEngine.Physics.OverlapBoxNonAlloc(realCenter, realExtents, _overlapResults, transform.rotation);
            }
            else if (col is SphereCollider sphere)
            {
                float realRadius = sphere.radius * Mathf.Max(transform.lossyScale.x, Mathf.Max(transform.lossyScale.y, transform.lossyScale.z));
                Vector3 realCenter = transform.TransformPoint(sphere.center);
                overlapCount = UnityEngine.Physics.OverlapSphereNonAlloc(realCenter, realRadius, _overlapResults);
            }

            for (int i = 0; i < overlapCount; i++) OnTriggerEnter(_overlapResults[i]);
        }

        // OPTIMIZATION: We clear dead objects only ONCE PER FRAME for the zone, not 1000 times for querying particles.
        private void Update()
        {
            if (_collidersInside.Count == 0)
            {
                IsZoneActive = false;
                return;
            }

            bool hasActiveCollider = false;

            // A reverse for loop is the fastest way to remove elements from a list
            for (int i = _collidersInside.Count - 1; i >= 0; i--)
            {
                Collider c = _collidersInside[i];
                if (c == null || !c.enabled || !c.gameObject.activeInHierarchy)
                {
                    _collidersInside.RemoveAt(i);
                }
                else
                {
                    hasActiveCollider = true;
                }
            }

            IsZoneActive = hasActiveCollider;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsAllowed(other)) 
            {
                if (!_collidersInside.Contains(other)) _collidersInside.Add(other);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (_collidersInside.Contains(other)) 
            {
                _collidersInside.Remove(other);
            }
        }

        private bool IsAllowed(Collider other)
        {
            Core.TimeWarpIdentity identity = other.GetComponentInParent<Core.TimeWarpIdentity>();
            if (identity == null) return false;

            if (AllowAnyTimeWarpObject) return true;

            GameObject go = identity.gameObject;

            // 1. Tag match (robust - survives renaming, duplication and pooling).
            if (TimeWarpTagUtility.HasAnyTag(go, AllowedActivatorTags)) return true;

            // 2. Layer match, if one was configured.
            if (AllowedActivatorLayers.value != 0 &&
                TimeWarpTagUtility.IsInLayerMask(go, AllowedActivatorLayers)) return true;

            // 3. LEGACY name match, kept so existing scenes keep working.
            //
            // Note the known weaknesses: Unity appends "(Clone)" to instantiated objects
            // but " (1)", " (2)" to scene duplicates, and only the former is stripped here.
            // Two prefabs sharing a name also both match. This is exactly why the tag list
            // above exists - but removing this would silently break configured scenes.
            if (AllowedActivatorPrefabs != null && AllowedActivatorPrefabs.Count > 0)
            {
                string objName = go.name.Replace("(Clone)", "").Trim();
                foreach (var prefab in AllowedActivatorPrefabs)
                {
                    if (prefab != null && prefab.name == objName) return true;
                }
            }

            return false;
        }

        private void OnDisable() 
        {
            _collidersInside.Clear();
            IsZoneActive = false;
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

            // Green when the zone is currently satisfied, dimmed when idle, so a designer
            // can watch the gate open and close during Play mode.
            Color c = TimeWarpZoneGizmos.PermissionColor;
            if (!IsZoneActive) c = new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f);

            TimeWarpZoneGizmos.DrawCollider(col, c);

            string info = "Permission Zone\n" +
                          (AllowAnyTimeWarpObject ? "accepts any TimeWarp object" : "accepts listed objects only") +
                          (IsZoneActive ? "\nACTIVE" : "\ninactive");

            TimeWarpZoneGizmos.DrawLabel(TimeWarpZoneGizmos.LabelAnchor(col.bounds), info, c);
        }
    }
}