# Magic Time Users — creating & configuring

A **Magic Time User** is any object that owns a local time scale and exposes
per-instance time (`DeltaTime`, `TimeScale`, `Time`). There are three ways to make
one, depending on whether you control the class's code:

| Situation | Approach |
|---|---|
| New behaviour, or you can change the class's base | **Inherit `MagicTimeUser`** (best — exposes everything, full lifecycle) |
| Existing script you'd rather not re-parent (or it already inherits something) | **Attach a `MagicTimeUser` component** and read time off it |
| You need the interface but truly can't inherit | **Implement `IHaveLocalTime`** (advanced — see `writing-custom-code.md`) |

In all cases, motion/timer code must read the User's `DeltaTime` instead of
`Time.deltaTime` for the slowdown to actually show. That coding step is in
`writing-custom-code.md`; this file is about *making the User and configuring it*.

---

## Configuration fields (every User has these)

On the `MagicTimeUser` (inherited or as a component):

- **`defaultTimeScale`** (`float`, default `1`) — the value the object's own scale
  starts at when enabled. Set `< 1` for a permanently-slow object, `0` for one
  that starts frozen.
- **`initialTimeScales`** (`List<LocalTimeScale>`) — named scales the object
  subscribes to on enable (e.g. `Global`, `EnemyTime`). These are looked up by name
  through `MagicTimeManager.Instance`, so the manager and matching named scales must
  exist. Subscribed scales **multiply** with the object's own scale.
- **`transitionDuration`** / **`transitionCurve`** — how the object's combined
  scale smoothly ramps when a value changes (e.g. a 0.2s bullet-time fade). Set
  `transitionDuration = 0` for instant snaps.

---

## Approach 1 — inherit `MagicTimeUser` (preferred)

```csharp
using UnityEngine;
using MagicPigGames.MagicTime;

public class Mover : MagicTimeUser   // was : MonoBehaviour
{
    public float speed = 5f;

    protected override void Update()
    {
        base.Update();   // REQUIRED — advances this object's clock + transition
        transform.position += transform.forward * speed * DeltaTime;  // DeltaTime, not Time.deltaTime
    }
}
```

The object now has its own scale plus everything in `IHaveLocalTime`. Set its scale
with `LocalTimeScaleValue = 0.5f`, pause it with `LocalTimeScale.Pause()`.
**Remember `base.X()` in any overridden `Awake/OnEnable/Update/OnDisable/OnDestroy`** —
this is the most common mistake (full rules in `writing-custom-code.md`).

---

## Approach 2 — attach a `MagicTimeUser` component (don't re-parent)

When a class already inherits something (or you simply don't want to change its
base), add a `MagicTimeUser` **component** to the same GameObject and read time off
it. This is also the cleanest way to make an **existing actor** a User.

```csharp
using UnityEngine;
using MagicPigGames.MagicTime;

[RequireComponent(typeof(MagicTimeUser))]
public class ExistingMover : MonoBehaviour   // unchanged base class
{
    MagicTimeUser _time;
    public float speed = 5f;

    void Awake()  => _time = GetComponent<MagicTimeUser>();
    void Update() => transform.position += transform.forward * speed * _time.DeltaTime;
}
```

The `MagicTimeUser` component runs its own lifecycle (no `base` calls needed by
you), so all you do is read `_time.DeltaTime` / `_time.TimeScale`. A `TimeZone`
will find this object (the component implements `IHaveLocalTime`) and slow it just
like an inherited User.

---

## Subscribing to shared scales (a group that slows together)

To slow many objects with one control, have them share a named scale instead of
each holding its own value:

- **In the inspector / at author time:** add the named `LocalTimeScale` to each
  User's `initialTimeScales`.
- **At runtime:**
  ```csharp
  var slow = MagicTimeManager.Instance.TimeScale("SlowMo");  // shared scale
  user.SubscribeToLocalTimeScale(slow);
  // ...later
  user.UnsubscribeFromLocalTimeScale(slow);
  ```
- **Drive them all at once:** `MagicTimeManager.Instance.TimeScale("SlowMo").SetValue(0.2f);`
  changes every subscriber simultaneously. (Remember it multiplies with each
  object's own scale.)

---

## Making a GameObject a User via the bridge

With `MagicTimeAgentTools` installed (see `setup.md`):

```csharp
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var go    = GameObject.Find("Enemy");
        var slow  = AssetDatabase.LoadAssetAtPath<LocalTimeScale>("Assets/_MagicTime/Scales/SlowMo.asset");
        var user  = MagicTimeAgentTools.MakeUser(go, defaultScale: 1f, initial: slow);  // adds MagicTimeUser, lists SlowMo
        result.Log($"Enemy is now a Magic Time User, subscribed to {slow.name}");
    }
}
```

`MakeUser` adds the `MagicTimeUser` component if absent and sets
`defaultTimeScale` + `initialTimeScales`. Note: this makes the GameObject a User,
but its **own movement scripts still need to read `DeltaTime`** to visibly slow —
remind the user, or update those scripts (no bridge needed for that).

---

## The four specialized Users (audio / particles / trails)

Drop-on components that make a slowed object's **audio, particles, and trails**
track its local time — **no custom code**. Each derives from the empty
`MagicTimeMonoBehaviour`, auto-finds a `MagicTimeUser` on the object, and reads its
`TimeScale` every frame. Add them alongside a `MagicTimeUser`:

| Component | What it drives | Key fields |
|---|---|---|
| `MagicTimeUserAudioSource` | `pitch` on child `AudioSource`s | `affectPitch`, `minPitch`/`maxPitch`, `minTimeScale`/`maxTimeScale` |
| `MagicTimeUserAudioMixer` | an `AudioMixer` exposed float (dB) | `audioMixer`, `pitchParameter` (default `"Pitch"`), min/max |
| `MagicTimeUserParticleSystem` | `simulationSpeed` + `startLifetime` on child `ParticleSystem`s | `handleSimulationSpeed`, `handleLifetime` |
| `MagicTimeUserTrailRenderer` | `time` on child `TrailRenderer`s | (uses the User's `InverseTimeScale`) |

```csharp
// e.g. make a slowed projectile's whoosh and trail slow with it:
MagicTimeAgentTools.AddSpecializedUser<MagicTimeUserAudioSource>(go);
MagicTimeAgentTools.AddSpecializedUser<MagicTimeUserTrailRenderer>(go);
```

Notes: `MagicTimeUserAudioMixer` needs the mixer assigned (it logs an error
otherwise) and is meant to live near your Audio Mixer. The particle/trail drivers
cache original values and restore them on enable, so they pool cleanly. They map
`TimeScale` through a min/max range — for audio pitch the default range slows pitch
as time slows and raises it when sped up.

---

## Pooling note

`MagicTimeUser` re-initializes cleanly on `OnEnable` (re-subscribes its own scale,
restarts initial-scale subscriptions, resets to `defaultTimeScale`), so it pools
well. One caveat: the per-object accumulators `Time` / `UnscaledTime` are **not**
reset on enable — a reused pooled object carries its prior elapsed time. If your
logic depends on "time since this spawn," capture a baseline in your own
`OnEnable` rather than trusting `user.Time` to be zero. (More in
`writing-custom-code.md`.)
