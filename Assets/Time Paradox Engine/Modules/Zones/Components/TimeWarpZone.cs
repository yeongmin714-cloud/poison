using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events; 
using TimeWarp.Core;
using TimeWarp.Core.Managers;
using TimeWarp.Modules.Zones.Profiles;

namespace TimeWarp.Modules.Zones.Components
{
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("TimeWarp/Zones/Time Warp Zone")]
    public class TimeWarpZone : MonoBehaviour
    {
        public TimeProfile Profile;

        [Header("Zone Events")]
        public UnityEvent<TimeWarpIdentity> OnObjectAffected;
        public UnityEvent<TimeWarpIdentity> OnObjectReleased;

        private class AffectedTarget
        {
            public TimeWarpIdentity Identity;
            public float TransitionTimer;
            public float ActiveTimer; 
            public float ExitDelayTimer; 
            public bool IsExiting;
            public bool IsInsideTrigger;
            public float AssignedTargetScale;
            public float PreviousOffset; 
            
            public bool IsLocallyRewoundByZone;
            public float LocalRewindOffset;
        }

        private List<AffectedTarget> _targets = new List<AffectedTarget>();

        // ==========================================
        // PERFORMANCE FIX 1: OBJECT POOLING
        // ==========================================
        // Prevents Garbage Collector spikes when many projectiles enter/exit the zone rapidly.
        private Stack<AffectedTarget> _targetPool = new Stack<AffectedTarget>();

        private AffectedTarget GetTargetFromPool()
        {
            return _targetPool.Count > 0 ? _targetPool.Pop() : new AffectedTarget();
        }

        private void ReturnTargetToPool(AffectedTarget target)
        {
            target.Identity = null; // Clear reference to prevent object retention memory leaks
            _targetPool.Push(target);
        }

        // ==========================================
        // PERFORMANCE FIX 2: NON-ALLOC PHYSICS
        // ==========================================
        // Pre-allocated array to prevent memory allocation during the initial overlap check.
        private Collider[] _overlapResults = new Collider[64];

        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void Start()
        {
            CheckInitialOverlaps();
        }

        private void CheckInitialOverlaps()
        {
            Collider col = GetComponent<Collider>();
            if (col == null || Profile == null) return;

            int overlapCount = 0;

            if (col is BoxCollider box)
            {
                Vector3 realExtents = Vector3.Scale(box.size, transform.lossyScale) * 0.5f;
                Vector3 realCenter = transform.TransformPoint(box.center);
                
                // Uses NonAlloc to prevent returning a new array and generating Garbage
                overlapCount = UnityEngine.Physics.OverlapBoxNonAlloc(realCenter, realExtents, _overlapResults, transform.rotation, Profile.AffectedLayers);
            }
            else if (col is SphereCollider sphere)
            {
                float realRadius = sphere.radius * Mathf.Max(transform.lossyScale.x, Mathf.Max(transform.lossyScale.y, transform.lossyScale.z));
                Vector3 realCenter = transform.TransformPoint(sphere.center);
                
                // Uses NonAlloc to prevent returning a new array and generating Garbage
                overlapCount = UnityEngine.Physics.OverlapSphereNonAlloc(realCenter, realRadius, _overlapResults, Profile.AffectedLayers);
            }

            // Process only the active overlaps found
            for (int i = 0; i < overlapCount; i++)
            {
                OnTriggerEnter(_overlapResults[i]);
            }
        }

        private void RemoveTargetFast(int index)
        {
            int lastIndex = _targets.Count - 1;
            _targets[index] = _targets[lastIndex];
            _targets.RemoveAt(lastIndex);
        }

        private AffectedTarget GetTargetByIdentity(TimeWarpIdentity identity)
        {
            for (int i = 0; i < _targets.Count; i++)
            {
                if (_targets[i].Identity == identity) return _targets[i];
            }
            return null;
        }

        private void OnTriggerEnter(Collider other)
        {
            // Layer OR optional tag. The initial-overlap scan above still queries physics
            // by layer only (that is a broad-phase query and cannot filter by tag), so a
            // tag-only zone catches objects when they move, not objects already inside it
            // at scene start.
            if (Profile == null) return;
            if (!TimeWarpTagUtility.MatchesLayerOrTag(other.gameObject, Profile.AffectedLayers, Profile.AffectedTags)) return;

            // Retrieve from parent to ensure the system is not blind to ragdoll bones
            TimeWarpIdentity identity = other.GetComponentInParent<TimeWarpIdentity>();

            if (identity != null)
            {
                var target = GetTargetByIdentity(identity);

                if (target != null)
                {
                    if (Profile.ToggleOnEnter)
                    {
                        target.IsExiting = !target.IsExiting;
                        target.TransitionTimer = 0f;
                    }
                    else
                    {
                        target.IsExiting = false;
                        target.ExitDelayTimer = 0f; 
                    }
                    target.IsInsideTrigger = true;
                    return;
                }

                // Fetch from Object Pool instead of using 'new'
                var newTarget = GetTargetFromPool();
                newTarget.Identity = identity;
                newTarget.TransitionTimer = 0f;
                newTarget.ActiveTimer = 0f;
                newTarget.ExitDelayTimer = 0f;
                newTarget.IsExiting = false;
                newTarget.IsInsideTrigger = true;
                newTarget.AssignedTargetScale = Profile.GetTargetScale();
                newTarget.PreviousOffset = identity.FractionalOffset;
                newTarget.IsLocallyRewoundByZone = false;
                newTarget.LocalRewindOffset = 0f;

                _targets.Add(newTarget);
                OnObjectAffected?.Invoke(identity);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            TimeWarpIdentity identity = other.GetComponentInParent<TimeWarpIdentity>();

            if (identity != null)
            {
                var target = GetTargetByIdentity(identity);
                if (target == null) return;

                target.IsInsideTrigger = false;

                if (Profile.ToggleOnEnter) return;

                if (Profile.ExitRule == ExitCondition.OnLeaveTrigger && Profile.Duration <= 0f)
                {
                    target.IsExiting = true;
                    target.TransitionTimer = 0f;
                }
            }
        }

        private void Update()
        {
            if (Profile == null || _targets.Count == 0 || TimeWarpManager.Instance == null) return;

            float framerate = TimeWarpManager.Instance.CaptureFramerate;

            for (int i = _targets.Count - 1; i >= 0; i--)
            {
                var target = _targets[i];
                if (target.Identity == null || !target.Identity.IsRegistered) 
                { 
                    ReturnTargetToPool(target); // Safely return to pool
                    RemoveTargetFast(i); 
                    continue; 
                }

                float currentOffset = target.Identity.FractionalOffset;
                
                // SAFEGUARD: Differentiate between global keyboard rewind and local zone rewind
                bool isGlobalRewind = (target.Identity.IsRewinding && !target.IsLocallyRewoundByZone) || 
                                      (currentOffset > 0.001f && !target.IsLocallyRewoundByZone);
                
                float dt = 0f;

                if (!isGlobalRewind)
                {
                    dt = Time.deltaTime; 
                    target.PreviousOffset = 0f;
                }
                else
                {
                    float offsetDelta = currentOffset - target.PreviousOffset;
                    dt = -(offsetDelta / framerate); 
                    target.PreviousOffset = currentOffset;
                }

                if (Mathf.Abs(dt) < 0.0001f) continue;

                // --- ZONE DURATION LOGIC ---
                if (dt > 0f)
                {
                    target.ActiveTimer += dt;

                    if (!target.IsExiting)
                    {
                        if (Profile.ExitRule == ExitCondition.Timed && target.ActiveTimer >= Profile.Duration)
                        {
                            target.IsExiting = true;
                            target.TransitionTimer = 0f;
                        }
                        else if (Profile.ExitRule == ExitCondition.OnLeaveTrigger && !target.IsInsideTrigger && Profile.Duration > 0f)
                        {
                            target.ExitDelayTimer += dt;
                            if (target.ExitDelayTimer >= Profile.Duration)
                            {
                                target.IsExiting = true;
                                target.TransitionTimer = 0f;
                            }
                        }
                    }
                    target.TransitionTimer += dt;
                }
                else
                {
                    target.ActiveTimer += dt; 

                    if (target.ActiveTimer <= 0f)
                    {
                        // ==========================================
                        // REWIND "UN-ENTERS" THE ZONE
                        // ==========================================
                        // Rewinding runs the zone's clock backwards, so ActiveTimer can
                        // reach zero - the object is being pulled back to before it ever
                        // entered. Releasing the effect here is correct.
                        //
                        // What was NOT correct was also dropping the target from the list
                        // while the object is STILL PHYSICALLY INSIDE the trigger. Unity
                        // only fires OnTriggerEnter on an actual crossing, so nothing ever
                        // re-registered the object: after one rewind inside a zone, that
                        // zone stopped affecting it permanently - until the player walked
                        // out and back in.
                        //
                        // Now the entry is kept (with its timers rewound to the start) if
                        // the object is still inside, so the effect blends back in as soon
                        // as time flows forward again.
                        target.ActiveTimer = 0f;
                        ReleaseTarget(target);

                        if (!target.IsInsideTrigger)
                        {
                            ReturnTargetToPool(target);
                            RemoveTargetFast(i);
                            continue;
                        }

                        target.TransitionTimer = 0f;
                        target.ExitDelayTimer = 0f;
                        target.IsExiting = false;
                        target.PreviousOffset = target.Identity.FractionalOffset;
                        continue;
                    }

                    if (target.IsExiting)
                    {
                        target.TransitionTimer += dt; 
                        if (target.TransitionTimer <= 0f)
                        {
                            target.IsExiting = false;
                            target.TransitionTimer = Profile.BlendInDuration; 
                            
                            if (Profile.ExitRule == ExitCondition.Timed) 
                                target.ActiveTimer = Profile.Duration + target.TransitionTimer; 
                            
                            if (Profile.ExitRule == ExitCondition.OnLeaveTrigger) 
                                target.ExitDelayTimer = Profile.Duration + target.TransitionTimer; 
                        }
                    }
                    else
                    {
                        target.TransitionTimer += dt; 
                        if (target.TransitionTimer < 0f) target.TransitionTimer = 0f;

                        if (Profile.ExitRule == ExitCondition.OnLeaveTrigger && !target.IsInsideTrigger)
                        {
                            target.ExitDelayTimer += dt;
                            if (target.ExitDelayTimer < 0f) target.ExitDelayTimer = 0f;
                        }
                    }
                }

                // --- APPLY EFFECT ---
                if (!target.IsExiting)
                {
                    float t = Mathf.Clamp01(target.TransitionTimer / Profile.BlendInDuration);
                    float curveVal = Profile.BlendInCurve.Evaluate(t);
                    ApplyEffect(target, curveVal, dt, framerate);
                }
                else
                {
                    float t = Mathf.Clamp01(target.TransitionTimer / Profile.BlendOutDuration);
                    float curveVal = Profile.BlendOutCurve.Evaluate(t);
                    ApplyEffectExit(target, curveVal);

                    if (t >= 1.0f && dt > 0f)
                    {
                        ReleaseTarget(target);
                        ReturnTargetToPool(target); // Return to pool
                        RemoveTargetFast(i); 
                    }
                }
            }
        }

        private void ApplyEffect(AffectedTarget target, float blend, float dt, float framerate)
        {
            if (Profile.EffectType == TimeEffectType.SlowMotion)
            {
                target.Identity.LocalTimeScale = Mathf.Lerp(1.0f, target.AssignedTargetScale, blend);
            }
            else if (Profile.EffectType == TimeEffectType.Stasis)
            {
                // Stasis forces timescale to 0
                target.Identity.LocalTimeScale = Mathf.Lerp(1.0f, 0.0f, blend);
            }
            else if (Profile.EffectType == TimeEffectType.Rewind)
            {
                // During rewind, the forward game time (physics) must stand still
                target.Identity.LocalTimeScale = Mathf.Lerp(1.0f, 0.0f, blend);
                
                // Calculate rewind speed based on the curve
                float currentSpeed = Mathf.Lerp(0f, Profile.RewindSpeed, blend);
                target.LocalRewindOffset += currentSpeed * framerate * dt;

                target.IsLocallyRewoundByZone = true;
                
                // Force rewind via offset
                target.Identity.FractionalOffset = target.LocalRewindOffset;
            }
        }

        private void ApplyEffectExit(AffectedTarget target, float blend)
        {
            if (Profile.EffectType == TimeEffectType.SlowMotion)
            {
                target.Identity.LocalTimeScale = Mathf.Lerp(target.AssignedTargetScale, 1.0f, blend);
            }
            else if (Profile.EffectType == TimeEffectType.Stasis)
            {
                target.Identity.LocalTimeScale = Mathf.Lerp(0.0f, 1.0f, blend);
            }
            else if (Profile.EffectType == TimeEffectType.Rewind)
            {
                target.Identity.LocalTimeScale = Mathf.Lerp(0.0f, 1.0f, blend);
            }
        }

        private void ReleaseTarget(AffectedTarget target)
        {
            target.Identity.LocalTimeScale = 1.0f;
            
            if (target.IsLocallyRewoundByZone)
            {
                target.Identity.FractionalOffset = 0f;
            }

            OnObjectReleased?.Invoke(target.Identity); 
        }


        // ==========================================
        // SCENE VIEW VISUALISATION
        // ==========================================
        [Header("Editor")]
        [Tooltip("Draw this zone in the Scene view. Editor only - has no effect in a build.")]
        public bool DrawGizmo = true;

#if UNITY_EDITOR
        /// <summary>
        /// Editor-time sanity checks. These catch the silent misconfigurations that
        /// otherwise only show up as "the zone does nothing" at runtime.
        /// </summary>
        private void OnValidate()
        {
            Collider col = GetComponent<Collider>();

            if (col != null && !col.isTrigger)
            {
                Debug.LogWarning(
                    $"[TimeWarp] Zone \"{name}\" has a Collider that is NOT set to Is Trigger, " +
                    $"so it will never fire OnTriggerEnter and the zone cannot work.", this);
            }

            if (Profile == null)
            {
                Debug.LogWarning(
                    $"[TimeWarp] Zone \"{name}\" has no Time Profile assigned - it is inactive. " +
                    $"Assign a profile, or delete the zone.", this);
            }
            else if (Profile.AffectedLayers.value == 0 &&
                     (Profile.AffectedTags == null || Profile.AffectedTags.Count == 0))
            {
                Debug.LogWarning(
                    $"[TimeWarp] Zone \"{name}\" matches nothing: its profile has Affected Layers " +
                    $"set to Nothing and no Affected Tags.", this);
            }
        }
#endif

        private void OnDrawGizmos()
        {
            if (!DrawGizmo) return;

            Collider col = GetComponent<Collider>();
            if (col == null) return;

            Color c = TimeWarpZoneGizmos.ColorForProfile(Profile);
            TimeWarpZoneGizmos.DrawCollider(col, c);
            TimeWarpZoneGizmos.DrawLabel(
                TimeWarpZoneGizmos.LabelAnchor(col.bounds),
                TimeWarpZoneGizmos.DescribeProfile(Profile, "Time Warp Zone"),
                c);
        }

        private void OnDestroy()
        {
            foreach (var target in _targets)
            {
                if (target.Identity != null) ReleaseTarget(target);
            }
            _targets.Clear();
            _targetPool.Clear();
        }
    }
}