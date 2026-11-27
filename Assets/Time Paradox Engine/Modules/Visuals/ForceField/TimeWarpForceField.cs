using System.Collections.Generic;
using UnityEngine;
using TimeWarp.Core;
using TimeWarp.Core.Managers;

namespace TimeWarp.Modules.Visuals
{
    /// <summary>
    /// Drives the "TimeWarp/Force Field" shader and feeds it impacts.
    ///
    /// WHY A SCRIPT IS NEEDED AT ALL: a shader has no idea the engine exists. If the
    /// surface animates on Unity's built-in _Time it will keep scrolling at full speed
    /// while everything around it is frozen mid-air, which instantly breaks the illusion.
    ///
    /// So this component keeps its own clock and hands it to the shader as _LocalTime.
    /// The clock is advanced by the object's effective time scale, and during a rewind it
    /// is advanced BACKWARDS by the exact amount of history being consumed. Everything the
    /// shader animates then follows the engine for free - the pattern crawls in slow
    /// motion, freezes in a time stop, and un-scrolls during a rewind.
    ///
    /// Impacts get the same treatment. A hit is stored with the local time it happened at,
    /// so when time runs backwards the ripples shrink back into the point of contact and
    /// disappear - the impact literally un-happens.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    [AddComponentMenu("TimeWarp/Visuals/Time Warp Force Field")]
    public class TimeWarpForceField : MonoBehaviour, ITimeWarpModule
    {
        // Must match TW_MAX_HITS in the shader.
        private const int MaxHits = 4;

        [Header("Reaction")]
        [Tooltip("Layers that can make the field ripple. Everything else passes through unnoticed.")]
        public LayerMask ImpactLayers = ~0;

        [Tooltip("Objects with any of these tags can make the field ripple. Combined with " +
                 "Impact Layers using OR - leave empty to filter by layer only.")]
        public List<string> ImpactTags = new List<string>();

        [Tooltip("React to objects passing through the trigger, not just to solid collisions. " +
                 "This is what you want for a bubble the player can walk into.")]
        public bool ReactToTriggers = true;

        [Tooltip("Ignore repeat hits from the same object for this long, so a body resting " +
                 "against the field does not machine-gun ripples every frame.")]
        public float ImpactCooldown = 0.15f;

        [Header("Impact strength")]
        [Tooltip("Collisions slower than this are ignored entirely.")]
        public float MinImpactSpeed = 0.5f;

        [Tooltip("Speed that counts as a full-strength hit. Anything faster is clamped.")]
        public float FullImpactSpeed = 8f;

        [Header("Proximity")]
        [Tooltip("Track objects that are INSIDE or touching the field and glow around them " +
                 "continuously. This is what makes the field react to a player standing in it, " +
                 "rather than only at the moment of contact.")]
        public bool TrackIntruders = true;

        [Header("Overload")]
        [Tooltip("Impacts landing close together push the field toward overload: it swells, " +
                 "brightens and wobbles harder. Set to 0 to disable.")]
        [Range(0f, 1f)] public float OverloadPerHit = 0.25f;

        [Tooltip("How fast overload drains back to zero, per second.")]
        [Range(0.05f, 5f)] public float OverloadRecovery = 0.6f;

        [Tooltip("Raised when the field reaches full overload. Use it to break the shield, " +
                 "play a sound, or trigger a time anomaly.")]
        public UnityEngine.Events.UnityEvent OnOverload;

        [Header("Rewind look")]
        [Tooltip("How quickly the field shifts to its Rewind Color when time reverses.")]
        [Range(0.5f, 20f)] public float RewindColorSpeed = 8f;

        // --- Shader property IDs, resolved once ---
        private static readonly int LocalTimeId   = Shader.PropertyToID("_LocalTime");
        private static readonly int RewindBlendId = Shader.PropertyToID("_RewindBlend");
        private static readonly int HitsId        = Shader.PropertyToID("_Hits");
        private static readonly int IntrudersId   = Shader.PropertyToID("_Intruders");
        private static readonly int OverloadId    = Shader.PropertyToID("_Overload");

        private Renderer _renderer;
        private MaterialPropertyBlock _block;
        private TimeWarpIdentity _identity;

        /// <summary>The field's own clock. Can run backwards - that is the whole point.</summary>
        private float _localTime;

        private float _rewindBlend;
        private float _lastOffset;
        private float _lastLocalTime;

        // Ring buffer of impacts. Oldest entry is overwritten once full, which is fine
        // because an old ripple has already faded out by then.
        private readonly Vector4[] _hits = new Vector4[MaxHits];
        private int _nextHitSlot;

        // Per-object cooldown, keyed by the collider's owner.
        private readonly Dictionary<Object, float> _lastHitTime = new Dictionary<Object, float>();

        // Objects currently inside the field. A list rather than a count because the shader
        // needs their positions every frame, and they move.
        private readonly List<Collider> _intruders = new List<Collider>();
        private readonly Vector4[] _intruderData = new Vector4[MaxHits];

        private float _overload;
        private bool _overloadFired;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _block = new MaterialPropertyBlock();
            _identity = GetComponent<TimeWarpIdentity>();

            // Park every slot far in the past so nothing renders before the first real hit.
            for (int i = 0; i < MaxHits; i++) _hits[i] = new Vector4(0f, -9999f, 0f, -9999f);

            PushToShader();
        }

        // ==========================================
        // CLOCK
        // ==========================================
        // Driven from OnWarpUpdate when an Identity is present so the field ticks in the
        // engine's own order, and from Update otherwise so it still works standalone.

        public void OnWarpFixedUpdate() { }

        public void OnWarpUpdate() => Tick();

        private void Update()
        {
            // Without an Identity nothing else will tick us, so do it here.
            if (_identity == null) Tick();
        }

        private void Tick()
        {
            float dt = Time.unscaledDeltaTime;
            bool rewinding = false;

            if (_identity != null && _identity.IsRegistered)
            {
                rewinding = _identity.IsRewinding;

                if (rewinding && TimeWarpManager.Instance != null)
                {
                    // Convert the change in rewind depth into real seconds and run the
                    // clock backwards by exactly that much. Using the offset delta rather
                    // than a guessed speed keeps the visuals locked to the actual playback,
                    // however the rewind accelerates.
                    float framerate = Mathf.Max(1f, TimeWarpManager.Instance.CaptureFramerate);
                    float offset = _identity.FractionalOffset;

                    _localTime -= (offset - _lastOffset) / framerate;
                    _lastOffset = offset;
                }
                else
                {
                    _lastOffset = _identity.FractionalOffset;
                    _localTime += dt * _identity.EffectiveTimeScale;
                }
            }
            else
            {
                _localTime += dt;
            }

            // Ease the colour shift instead of snapping, so a short rewind does not strobe.
            float target = rewinding ? 1f : 0f;
            _rewindBlend = Mathf.MoveTowards(_rewindBlend, target, RewindColorSpeed * dt);

            // Overload drains on the field's OWN clock, so a frozen field stays overloaded
            // and a rewound one recovers backwards - consistent with everything else here.
            float clockDelta = Mathf.Abs(_localTime - _lastLocalTime);
            _lastLocalTime = _localTime;

            if (_overload > 0f)
            {
                _overload = Mathf.Max(0f, _overload - OverloadRecovery * clockDelta);
                if (_overload < 0.99f) _overloadFired = false;
            }

            UpdateIntruders();
            PushToShader();
        }

        /// <summary>
        /// Refreshes the positions of everything currently inside the field. They move, so
        /// this has to happen every frame rather than only on enter.
        /// </summary>
        private void UpdateIntruders()
        {
            for (int i = 0; i < MaxHits; i++) _intruderData[i] = Vector4.zero;

            if (!TrackIntruders) return;

            // Drop anything destroyed or disabled while inside.
            for (int i = _intruders.Count - 1; i >= 0; i--)
            {
                if (_intruders[i] == null || !_intruders[i].gameObject.activeInHierarchy)
                    _intruders.RemoveAt(i);
            }

            int slot = 0;

            foreach (Collider col in _intruders)
            {
                if (slot >= MaxHits) break;

                Vector3 p = col.bounds.center;
                _intruderData[slot] = new Vector4(p.x, p.y, p.z, 1f); // w = occupied flag
                slot++;
            }
        }

        private void PushToShader()
        {
            _renderer.GetPropertyBlock(_block);

            _block.SetFloat(LocalTimeId, _localTime);
            _block.SetFloat(RewindBlendId, _rewindBlend);
            _block.SetFloat(OverloadId, _overload);
            _block.SetVectorArray(HitsId, _hits);
            _block.SetVectorArray(IntrudersId, _intruderData);

            _renderer.SetPropertyBlock(_block);
        }

        // ==========================================
        // IMPACTS
        // ==========================================
        private void OnCollisionEnter(Collision collision)
        {
            if (!Accepts(collision.collider)) return;

            // Contact point is exact for a real collision - use it.
            Vector3 point = collision.contactCount > 0
                ? collision.GetContact(0).point
                : collision.collider.bounds.center;

            RegisterImpact(collision.collider, point, collision.relativeVelocity.magnitude);
        }

        private void OnTriggerStay(Collider other)
        {
            // Membership is refreshed here rather than only on enter, because an object can
            // be spawned already inside the field and would otherwise never be registered.
            if (!TrackIntruders) return;
            if (_intruders.Contains(other)) return;

            GameObject body = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
            if (!TimeWarpTagUtility.MatchesLayerOrTag(body, ImpactLayers, ImpactTags)) return;

            _intruders.Add(other);
        }

        private void OnTriggerExit(Collider other)
        {
            _intruders.Remove(other);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!ReactToTriggers || !Accepts(other)) return;

            // A trigger gives no contact point, so approximate with the closest point on
            // the field's own collider - visually that is where the object crossed.
            Vector3 point = GetComponent<Collider>() != null
                ? GetComponent<Collider>().ClosestPoint(other.bounds.center)
                : other.bounds.center;

            float speed = other.attachedRigidbody != null
                ? other.attachedRigidbody.linearVelocity.magnitude
                : FullImpactSpeed; // no rigidbody to measure - treat it as a solid hit

            RegisterImpact(other, point, speed);
        }

        private bool Accepts(Collider col)
        {
            if (col == null) return false;

            GameObject go = col.attachedRigidbody != null
                ? col.attachedRigidbody.gameObject
                : col.gameObject;

            // Layer OR tag, matching how the rest of the engine filters.
            if (!TimeWarpTagUtility.MatchesLayerOrTag(go, ImpactLayers, ImpactTags)) return false;

            // Cooldown, so a leaning crate does not spam ripples.
            if (_lastHitTime.TryGetValue(go, out float last))
            {
                if (Mathf.Abs(_localTime - last) < ImpactCooldown) return false;
            }

            _lastHitTime[go] = _localTime;
            return true;
        }

        /// <summary>
        /// Adds an impact to the field. Safe to call from your own code - use it for
        /// scripted hits like a bullet that is destroyed before it can collide.
        /// </summary>
        public void RegisterImpact(Object source, Vector3 worldPoint, float speed)
        {
            if (speed < MinImpactSpeed) return;

            // Stored as (position, time). Strength is not stored separately - a weak hit
            // simply starts closer to the fade threshold via the cooldown and the shader's
            // lifetime, which keeps the per-pixel loop as cheap as possible.
            _hits[_nextHitSlot] = new Vector4(worldPoint.x, worldPoint.y, worldPoint.z, _localTime);
            _nextHitSlot = (_nextHitSlot + 1) % MaxHits;

            // Harder hits load the field faster.
            float strength = Mathf.InverseLerp(MinImpactSpeed, FullImpactSpeed, speed);
            _overload = Mathf.Clamp01(_overload + OverloadPerHit * Mathf.Max(strength, 0.25f));

            if (_overload >= 0.99f && !_overloadFired)
            {
                _overloadFired = true;
                OnOverload?.Invoke();
            }

            PushToShader();
        }

        /// <summary>Convenience overload for scripted hits with no source object.</summary>
        public void RegisterImpact(Vector3 worldPoint) =>
            RegisterImpact(null, worldPoint, FullImpactSpeed);

        /// <summary>Clears every ripple immediately.</summary>
        public void ClearImpacts()
        {
            for (int i = 0; i < MaxHits; i++) _hits[i] = new Vector4(0f, -9999f, 0f, -9999f);
            _lastHitTime.Clear();
            _overload = 0f;
            _overloadFired = false;
            PushToShader();
        }

        /// <summary>Current overload, 0 to 1. Read it to drive a shield health bar.</summary>
        public float Overload => _overload;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (FullImpactSpeed < MinImpactSpeed) FullImpactSpeed = MinImpactSpeed;

            var rnd = GetComponent<Renderer>();
            if (rnd == null || rnd.sharedMaterial == null) return;

            Shader shader = rnd.sharedMaterial.shader;

            if (shader != null && shader.name != "TimeWarp/Force Field")
            {
                Debug.LogWarning(
                    $"[TimeWarp] \"{name}\" has a Time Warp Force Field component but its material uses " +
                    $"\"{shader.name}\". This component only drives the TimeWarp/Force Field shader - " +
                    $"nothing will animate.", this);
            }
        }
#endif
    }
}
