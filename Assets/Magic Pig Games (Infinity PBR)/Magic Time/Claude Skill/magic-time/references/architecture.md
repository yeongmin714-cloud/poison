# Architecture — the Magic Time object model

Everything is in namespace **`MagicPigGames.MagicTime`**. There is no second
runtime namespace. (Editor code references `MagicPigGames.Shared` for inspector
chrome only.)

Two layers:

- **Assets + the singleton** — `LocalTimeScale` ScriptableObjects on disk, and one
  `MagicTimeManager` that clones them and serves them by name.
- **Runtime objects in the scene** — `MagicTimeUser`s (and `TimeZone`s) that
  subscribe to scales and expose local time.

The data flow: a `MagicTimeUser` has its **own** `LocalTimeScale` plus a list of
**subscribed** scales. Each frame it computes a **combined** scale (the product of
all of them) and exposes it as `TimeScale`; `DeltaTime` is just
`UnityEngine.Time.deltaTime * TimeScale`. Your code reads `DeltaTime` and motion
slows/speeds/freezes accordingly.

---

## `LocalTimeScale` (ScriptableObject) — the unit of time control

```csharp
[CreateAssetMenu(fileName = "New Time Scale", menuName = "Local Time Scale/Time Scale")]
public class LocalTimeScale : ScriptableObject
```

Create manually via **Create > Local Time Scale > Time Scale**. It is the only
authorable Magic Time asset type.

A scale holds one `Value` and a set of subscribers. `1` = normal, `0.5` = half
speed, `0` = paused/frozen, `2` = double speed. **The `Value` setter is private** —
never assign it; use the methods below.

**Serialized fields**

```csharp
[SerializeField] protected float value = 1f;                // edit the default in the inspector
[SerializeField] protected bool  autoRemoveWhenEmpty = true; // forced FALSE for manager/initial scales
// Inspector events:
[SerializeField] protected UnityEvent<float>  onTimeScaleChangedUnityEvent;
[SerializeField] protected UnityEvent<object> onSubscribedUnityEvent;
[SerializeField] protected UnityEvent<object> onUnsubscribedUnityEvent;
[SerializeField] protected UnityEvent         onPausedUnityEvent;
[SerializeField] protected UnityEvent         onResumedUnityEvent;
```

> There is **no transition curve / transition duration** on `LocalTimeScale`.
> Smoothing is a `MagicTimeUser` concept (see below).

**Key properties**

```csharp
public float Value { get; private set; }     // read it; change via SetValue/Pause/Resume
public bool  IsPaused { get; }               // true when Value == 0
public int   SubscriberCount { get; }
public string[]     SubscriberNames { get; }
public GameObject[] SubscriberGameObjects { get; }
public bool  AutoRemoveWhenEmpty { get; set; }
public float DeltaTime { get; }              // Time.deltaTime         * Value
public float UnscaledDeltaTime { get; }      // Time.unscaledDeltaTime * Value
public float FixedDeltaTime { get; }         // Time.fixedDeltaTime    * Value
public float Time { get; }                   // accumulated scaled time (advanced by Tick)
public float UnscaledTime { get; }
```

**Key methods**

```csharp
public void SetValue(float newValue);   // no-op if ~equal; clamps negatives to 0; fires change/pause/resume
public void Pause();                    // caches current value, sets Value = 0
public void Resume();                   // restores the cached value
public void Initialize(string name, float initialValue = 1f);  // after CreateInstance
public void Tick();                     // _time += DeltaTime — the MANAGER calls this each frame
public void Subscribe(IHaveLocalTime subscriber);
public void Unsubscribe(IHaveLocalTime subscriber);
public void UnsubscribeAll();
public void Dispose();                  // notifies subscribers, unsubscribes all, asks manager to remove it
```

**Events (C# `Action` delegates, code-only)** — plus the inspector `UnityEvent`
accessors `OnTimeScaleChangedUnityEvent`, `OnSubscribedUnityEvent`, etc.

```csharp
public event Action<float, LocalTimeScale> OnTimeScaleChanged;
public event Action<LocalTimeScale> OnPaused;
public event Action<LocalTimeScale> OnResumed;
public event Action<object> OnSubscribed;
public event Action<object> OnUnsubscribed;
```

`OnDisable` (in play mode, with a manager present) calls `Dispose()`.

---

## `MagicTimeManager` (singleton MonoBehaviour)

```csharp
public class MagicTimeManager : MonoBehaviour
{
    public static MagicTimeManager Instance { get; private set; }
}
```

Put **one** in the scene (empty GameObject + this component). `Awake` assigns
`Instance` and `DontDestroyOnLoad`; a **second instance destroys itself**.

**Fields**

```csharp
[SerializeField] private LocalTimeScale[] initialTimeScales;   // cloned on Awake, served by name
public List<LocalTimeScale> allTimeScales = new();            // every LocalTimeScale asset (filled by the editor)
public Dictionary<string, LocalTimeScale> TimeScales { get; } = new();  // live runtime scales by name
```

**What it does**

- `Awake` → `InitializeTimeScales()`: clones each `initialTimeScales` entry into a
  live scale (so the asset is never mutated), trims trailing-space names (warns),
  and forces `AutoRemoveWhenEmpty = false` on these initial scales.
- `Update` → calls `Tick()` on **every** live scale, accumulating their `Time`.
- Lookup / create:
  ```csharp
  public LocalTimeScale TimeScale(string name, float initialValue = 1f, bool createIfNull = true);
  ```
  Returns the live scale named `name` (creating/cloning it if needed). This is how
  Users attach to shared scales by name and how your code grabs one to drive.
- `public void RemoveTimeScale(string name)` — removes + disposes a live scale.

**If the manager is missing:** a User's `SubscribeToInitialTimeScales()` waits
forever for `Instance`, so its **named/initial** subscriptions never attach and
`_subscriptionsComplete` stays false. The User's **own personal scale still
works** (it's created in `Awake`, never registered with the manager), and a
`TimeZone` can still subscribe an object directly. But always include a manager
when you use named scales or zones.

---

## `MagicTimeUser` (MonoBehaviour : IHaveLocalTime) — the base you inherit

```csharp
public class MagicTimeUser : MonoBehaviour, IHaveLocalTime
```

Inherit your gameplay scripts from this (or attach it as a component — see
`magic-time-users.md`).

**Serialized fields**

```csharp
[Header("TimeScale Subscriptions")]
public List<LocalTimeScale> initialTimeScales = new();   // named scales to subscribe on enable

[Header("Transition Settings")]
public float transitionDuration = 0.2f;                  // smooth ramp toward a new combined scale
public AnimationCurve transitionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

[Header("Options")]
[Min(0f)] public float defaultTimeScale = 1f;            // applied when the object enables
```

**Time properties — INSTANCE and `virtual` (NOT static)**

```csharp
public float LocalTimeScaleValue { get => _localTimeScale.Value;
                                   set => _localTimeScale.SetValue(value); }  // this object's own scale

public virtual float TimeScale         => _currentTimeScale;                  // live, transition-smoothed combined scale
public virtual float InverseTimeScale  => UnityEngine.Time.timeScale / TimeScale;
public virtual float DeltaTime         => UnityEngine.Time.deltaTime      * TimeScale;
public virtual float UnscaledDeltaTime => UnityEngine.Time.unscaledDeltaTime;   // NOTE: not scaled
public virtual float FixedDeltaTime    => UnityEngine.Time.fixedDeltaTime * TimeScale;
public virtual float Time              => _timeSinceStart;                    // per-object scaled clock
public virtual float UnscaledTime      => _unscaledTimeSinceStart;
```

Always go through an instance: `sphere.DeltaTime`, `this.DeltaTime`. The docs'
shorthand `MagicTimeUser.DeltaTime` does **not** exist as a static.

**How scales combine — MULTIPLY**

```csharp
protected virtual float CombinedTimeScale()
{
    RemoveNullTimeScales();
    var combined = _localTimeScale.Value;       // start with this object's own scale
    foreach (var ts in SubscribedTimeScales)
        combined *= ts.Value;                   // each subscribed scale multiplies in
    return combined;
}
```

Two 0.5 scales → 0.25. The personal scale (default 1) is part of the product.
`_currentTimeScale` lerps toward this combined value over `transitionDuration`
shaped by `transitionCurve` (snaps instantly if `transitionDuration <= 0`).

**Subscription API**

```csharp
public List<LocalTimeScale> SubscribedTimeScales { get; }   // all scales affecting this object (incl. its own)
public LocalTimeScale LocalTimeScale { get; }               // == ObjectTimeScale, this object's personal scale
public LocalTimeScale ObjectTimeScale { get; }

public virtual void SubscribeToLocalTimeScale(LocalTimeScale scale);     // the public "add a scale" path
public virtual void UnsubscribeFromLocalTimeScale(LocalTimeScale scale);
public virtual void UnsubscribeFromAll();                                // removes its own scale too
public virtual IEnumerator SubscribeToInitialTimeScales();               // waits for the manager, attaches initialTimeScales
```

There is **no `AddTimeScale`** on the User — use `SubscribeToLocalTimeScale`.

**Lifecycle (virtual; subclasses MUST call base)**

```csharp
protected virtual void Awake();      // creates the personal _localTimeScale (Value 1)
protected virtual void OnEnable();   // sets desired/current = defaultTimeScale; starts the initial-subscribe coroutine; subscribes its own scale
protected virtual void Update();     // advances _timeSinceStart += DeltaTime, handles the transition lerp
protected virtual void OnDisable();  // UnsubscribeFromAll()
protected virtual void OnDestroy();  // UnsubscribeFromAll(); disposes the personal scale
```

No `Start`, no `FixedUpdate`. If you override any of the five, call the matching
`base.X()` or the object's clock and transitions stop. (See
`writing-custom-code.md`.)

---

## `IHaveLocalTime` (interface)

`MagicTimeUser` implements this. Implement it yourself only when you can't change
a class's base class (recipe in `writing-custom-code.md`).

```csharp
public interface IHaveLocalTime
{
    float LocalTimeScaleValue { get; set; }
    LocalTimeScale LocalTimeScale { get; }
    void SubscribeToLocalTimeScale(LocalTimeScale myTimeScale);
    void UnsubscribeFromLocalTimeScale(LocalTimeScale timeScale);
    List<LocalTimeScale> SubscribedTimeScales { get; }
    float TimeScale { get; }
    float Time { get; }
    float UnscaledTime { get; }
    float DeltaTime { get; }
    float UnscaledDeltaTime { get; }
    float FixedDeltaTime { get; }
    void OnTimeScaleDestroyed(LocalTimeScale timeScale);
    IEnumerator SubscribeToInitialTimeScales();
    void OnTimeScaleChanged(float value, LocalTimeScale localTimeScale);
    GameObject GameObject { get; }
}
```

A `TimeZone` looks for `IHaveLocalTime` (not `MagicTimeUser` specifically), so any
class implementing it participates in zones.

---

## `TimeZone` (MonoBehaviour)

```csharp
public class TimeZone : MonoBehaviour
```

A trigger volume that owns a `LocalTimeScale` and subscribes entering objects.
Requires a **`Collider` set as trigger** (`Awake` forces `isTrigger = true`,
warns if it had to). Full detail in `time-zones.md`. Key surface:

```csharp
public LocalTimeScale timeScaleToUse;                  // optional asset; instantiated per-zone if set
[SerializeField] protected float timeScaleValue = 1f;  // starting value when the zone creates its own scale
public LocalTimeScale TimeScale => timeScale;          // the live per-zone scale
public float TimeScaleValue => timeScaleValue;
public void SetTimeScaleStartingValue(float value);
public TimeZoneUnityEvent OnObjectEnter;               // UnityEvent<TimeZoneEventData>
public TimeZoneUnityEvent OnObjectExit;
```

On trigger enter it finds `GetComponentInParent<IHaveLocalTime>()` on the (non-
trigger) entering collider and calls `SubscribeToLocalTimeScale(timeScale)`; on
exit it unsubscribes. The zone's scale **stacks multiplicatively** with whatever
else affects the object. Helper types in the same file:
`TimeZoneEventData { GameObject gameObject; Collider collider; Vector3 contactPoint; }`
and `TimeZoneUnityEvent : UnityEvent<TimeZoneEventData>`.

---

## The four specialized Users

All four derive from the **empty** marker base `MagicTimeMonoBehaviour` (NOT from
`MagicTimeUser`). Each finds a sibling/parent `MagicTimeUser` (via `GetComponent`
in `OnValidate`) and, each `Update`, reads `magicTimeUser.TimeScale` and drives a
component property to match. They cache original values and restore them on enable.

- **`MagicTimeUserAudioSource`** — drives `AudioSource.pitch` on child
  `AudioSource`s. Maps `TimeScale [0..2]` → `pitch [0.5..1.5]`. Toggle `affectPitch`.
- **`MagicTimeUserAudioMixer`** — drives an `AudioMixer` exposed float
  (`pitchParameter`, default `"Pitch"`) in dB (`Log10(pitch)*20`). Errors if the
  mixer is unassigned.
- **`MagicTimeUserParticleSystem`** — drives `main.simulationSpeed = TimeScale`
  and `main.startLifetime = cached * (1/TimeScale)` on child `ParticleSystem`s.
  Toggles `handleSimulationSpeed`, `handleLifetime`.
- **`MagicTimeUserTrailRenderer`** — drives `TrailRenderer.time = original /
  InverseTimeScale` on child `TrailRenderer`s so trails stretch when time slows.

These let audio/particles/trails track a slowed object **without custom code** —
reach for them before writing your own.

---

## Editor utilities

- `MagicTimeEditorUtilities.FindAllLocalTimeScalesInProject()` — returns every
  `LocalTimeScale` asset (`AssetDatabase.FindAssets("t:LocalTimeScale")`). The
  `MagicTimeManager` inspector calls this in edit mode to fill `allTimeScales`.
- `LocalTimeEditorUtilities.DocsUrl` — the docs URL constant.
- There are **no `[MenuItem]`s** in the asset and **no helper that auto-creates a
  `MagicTimeManager`** — add the manager manually or via the agent tools.

---

## Quick reference — what reads what

| You want | Read / call |
|---|---|
| This object's combined slowdown | `user.TimeScale` |
| Frame-rate-independent motion on local time | `user.DeltaTime` (replaces `Time.deltaTime`) |
| Physics step on local time | `user.FixedDeltaTime` (in your own stepping) |
| Per-object elapsed time | `user.Time` / `user.UnscaledTime` |
| Set this object's own scale | `user.LocalTimeScaleValue = 0.5f` |
| A shared scale many objects use | `MagicTimeManager.Instance.TimeScale("SlowMo").SetValue(0.2f)` |
| Pause/resume one object | `user.LocalTimeScale.Pause()` / `.Resume()` |
