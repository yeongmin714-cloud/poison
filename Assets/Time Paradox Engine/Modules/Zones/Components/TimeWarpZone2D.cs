using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events; 
using TimeWarp.Core;
using TimeWarp.Core.Managers;
using TimeWarp.Modules.Zones.Profiles;

namespace TimeWarp.Modules.Zones.Components
{
    [RequireComponent(typeof(Collider2D))]
    [AddComponentMenu("TimeWarp/Zones/Time Warp Zone 2D")]
    public class TimeWarpZone2D : MonoBehaviour
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
            
            // NEW REWIND FLAGS (Synchronized with the 3D version)
            public bool IsLocallyRewoundByZone;
            public float LocalRewindOffset;
        }

        private List<AffectedTarget> _targets = new List<AffectedTarget>();

        // ==========================================
        // PERFORMANCE FIX 1: OBJECT POOLING
        // ==========================================
        private Stack<AffectedTarget> _targetPool = new Stack<AffectedTarget>();

        private AffectedTarget GetTargetFromPool()
        {
            return _targetPool.Count > 0 ? _targetPool.Pop() : new AffectedTarget();
        }

        private void ReturnTargetToPool(AffectedTarget target)
        {
            target.Identity = null; 
            _targetPool.Push(target);
        }

        // ==========================================
        // PERFORMANCE FIX 2: NON-ALLOC PHYSICS 2D
        // ==========================================
        private Collider2D[] _overlapResults2D = new Collider2D[64];

        private void Awake()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void Start()
        {
            CheckInitialOverlaps();
        }

        private void CheckInitialOverlaps()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col == null || Profile == null) return;

            // ==========================================
            // UNITY 6 MODERN API (Non-Alloc Replacement)
            // ==========================================
            // We use ContactFilter2D to define the layer mask. 
            // Passing a pre-allocated array to OverlapArea natively prevents Garbage Collection.
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(Profile.AffectedLayers);
            filter.useLayerMask = true;
            filter.useTriggers = true; // Ensure we catch other triggers if needed

            int overlapCount = Physics2D.OverlapArea(
                col.bounds.min,
                col.bounds.max,
                filter,
                _overlapResults2D
            );

            for (int i = 0; i < overlapCount; i++)
            {
                OnTriggerEnter2D(_overlapResults2D[i]);
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

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Layer OR optional tag - see the note in TimeWarpZone about the initial scan.
            if (Profile == null) return;
            if (!TimeWarpTagUtility.MatchesLayerOrTag(other.gameObject, Profile.AffectedLayers, Profile.AffectedTags)) return;

            if (other.TryGetComponent(out TimeWarpIdentity identity))
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

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.TryGetComponent(out TimeWarpIdentity identity))
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
                    ReturnTargetToPool(target);
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
                        // Rewinding runs the zone clock backwards, so the object can be
                        // pulled back to before it entered. Releasing the effect is right;
                        // dropping the entry while the object is STILL INSIDE the trigger
                        // was not - OnTriggerEnter2D only fires on an actual crossing, so
                        // the zone would never affect that object again.
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
                        ReturnTargetToPool(target);
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
                target.Identity.LocalTimeScale = Mathf.Lerp(1.0f, 0.0f, blend);
            }
            else if (Profile.EffectType == TimeEffectType.Rewind)
            {
                target.Identity.LocalTimeScale = Mathf.Lerp(1.0f, 0.0f, blend);
                
                float currentSpeed = Mathf.Lerp(0f, Profile.RewindSpeed, blend);
                target.LocalRewindOffset += currentSpeed * framerate * dt;

                target.IsLocallyRewoundByZone = true;
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
        private void OnValidate()
        {
            Collider2D col = GetComponent<Collider2D>();

            if (col != null && !col.isTrigger)
            {
                Debug.LogWarning(
                    $"[TimeWarp] Zone \"{name}\" has a Collider2D that is NOT set to Is Trigger, " +
                    $"so it will never fire OnTriggerEnter2D and the zone cannot work.", this);
            }

            if (Profile == null)
            {
                Debug.LogWarning(
                    $"[TimeWarp] Zone \"{name}\" has no Time Profile assigned - it is inactive.", this);
            }
            else if (Profile.AffectedLayers.value == 0 &&
                     (Profile.AffectedTags == null || Profile.AffectedTags.Count == 0))
            {
                Debug.LogWarning(
                    $"[TimeWarp] Zone \"{name}\" matches nothing: Affected Layers is Nothing " +
                    $"and there are no Affected Tags.", this);
            }
        }
#endif

        private void OnDrawGizmos()
        {
            if (!DrawGizmo) return;

            Collider2D col = GetComponent<Collider2D>();
            if (col == null) return;

            Color c = TimeWarpZoneGizmos.ColorForProfile(Profile);
            TimeWarpZoneGizmos.DrawCollider2D(col, c);

            Bounds b = col.bounds;
            TimeWarpZoneGizmos.DrawLabel(
                new Vector3(b.center.x, b.max.y + 0.15f, b.center.z),
                TimeWarpZoneGizmos.DescribeProfile(Profile, "Time Warp Zone 2D"),
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