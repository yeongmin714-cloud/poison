# Writing custom code that uses Magic Time

Read this before writing or modifying any C# that should respect local time. The
rules here are the difference between "it slows down" and "nothing happens" /
"ghost motion at the wrong speed." Write custom code into the **developer's own
scripts folder**, never inside the Magic Time asset (so package updates don't
clobber it). Custom C# needs **no unity-mcp bridge** — it's just `.cs` files.

The core idea: Magic Time gives each object a `TimeScale` (its combined slowdown)
and a `DeltaTime` (`Time.deltaTime * TimeScale`). **Your code has to actually read
them** — Magic Time only changes the *numbers*, it can't reach into your loops.

---

## Rule 1 — replace `Time.deltaTime` with the User's `DeltaTime`

Anywhere a per-frame value advances on a Magic Time object, swap the global time
source for the instance's:

| Global (ignores local time) | Local (respects it) |
|---|---|
| `Time.deltaTime` | `user.DeltaTime` |
| `Time.fixedDeltaTime` | `user.FixedDeltaTime` |
| `Time.time` (as an elapsed clock) | `user.Time` |
| `Time.unscaledDeltaTime` | `user.UnscaledDeltaTime` |

`DeltaTime`, `TimeScale`, `Time`, etc. are **instance** properties — always
`user.DeltaTime`, never a static. Audit **every** per-frame consumer on the object
(movement, timers, lerps, cooldowns, animation drivers); a single missed
`Time.deltaTime` is why "only some of it slows."

---

## Rule 2 — when you inherit `MagicTimeUser`, call `base.*` in every override

The base runs its per-object clock and transition in `Update`, sets up its scale in
`Awake`, subscribes in `OnEnable`, and cleans up in `OnDisable`/`OnDestroy`. The
five lifecycle methods are `virtual`; **omitting `base.X()` breaks the object.** The
source warns: *"If this isn't firing, make sure you are not overriding Update in a
subclass!!!"*

```csharp
public class Spinner : MagicTimeUser
{
    public float degreesPerSecond = 90f;

    protected override void Awake()    { base.Awake();    /* your setup */ }
    protected override void OnEnable()  { base.OnEnable(); /* your enable */ }
    protected override void OnDisable() { base.OnDisable();/* your disable */ }

    protected override void Update()
    {
        base.Update();   // advances Time + transition — DO NOT omit
        transform.Rotate(Vector3.up, degreesPerSecond * DeltaTime);
    }
}
```

There is **no `Start` and no `FixedUpdate`** on `MagicTimeUser`. You may add your
own `Start`; if you add a `FixedUpdate`, it's yours alone (no base to call) — see
Rule 5 for physics.

---

## Rule 3 — when you can't inherit, compose a `MagicTimeUser`

If the class already has a base you can't change, **attach a `MagicTimeUser`
component and read time off it** (this is the recommended "can't inherit" path):

```csharp
[RequireComponent(typeof(MagicTimeUser))]
public class Turret : SomeOtherBase
{
    MagicTimeUser _time;
    void Awake()  => _time = GetComponent<MagicTimeUser>();
    void Update() => AimAndFire(_time.DeltaTime);
}
```

The component runs its own lifecycle, so you don't manage `base.*` calls — you just
consume `_time.DeltaTime` / `_time.TimeScale`. A `TimeZone` finds the object through
that component too.

**Implementing `IHaveLocalTime` by hand is rarely worth it** — the interface
requires you to reproduce `MagicTimeUser`'s subscribe bookkeeping, the multiply
combine, and the per-frame tick. Prefer composition above. If you genuinely must
(e.g. a non-MonoBehaviour participating in zones), delegate every member to an
internal `MagicTimeUser` rather than re-deriving the math.

---

## Rule 4 — scales multiply; drive values correctly

- Combined scale = personal scale × each subscribed scale. Two 0.5s → 0.25.
  Account for any scale the object already carries (its own, a zone it's in) before
  layering another.
- **Never assign `LocalTimeScale.Value`** (private setter). Use:
  - one object: `user.LocalTimeScaleValue = 0.5f;` or `user.LocalTimeScale.Pause()/.Resume()`
  - a shared scale: `MagicTimeManager.Instance.TimeScale("SlowMo").SetValue(0.2f);`
- **Don't call `SetValue` every frame while a transition is running.** It restarts
  the transition curve and can stick near 0 (the documented FAQ trap). If you must
  drive continuously (a slider, a curve you evaluate each frame), set the affected
  Users' `transitionDuration = 0` so values apply immediately and there's no
  transition to interrupt.

---

## Rule 5 — physics, coroutines, animation, NavMesh

These don't read your `DeltaTime` automatically:

- **Coroutines.** `yield return new WaitForSeconds(t)` follows the *global*
  `Time.timeScale`, not local time. To wait in an object's local time, accumulate:
  ```csharp
  float elapsed = 0f;
  while (elapsed < duration) { elapsed += DeltaTime; yield return null; }
  ```
- **Rigidbody / FixedUpdate.** Unity integrates dynamic rigidbodies on the global
  fixed step — there's no per-object physics clock. For **kinematic** movement you
  control, multiply by `user.FixedDeltaTime` in your own `FixedUpdate`. For
  **dynamic** bodies, approximate local time by scaling what you apply (e.g.
  `rb.velocity *= user.TimeScale` on change, or scale forces) — true per-object
  physics slowdown is an approximation, so call that out to the user rather than
  promising exact behaviour.
- **Animator.** Drive `animator.speed = user.TimeScale;` each frame (or on change)
  to slow animation with local time.
- **NavMeshAgent.** Scale `agent.speed`/`agent.angularSpeed` by `user.TimeScale`,
  or drive the agent manually — the agent otherwise moves on global time.
- **Particles / audio / trails.** Don't hand-roll these — use the shipped
  specialized Users (`MagicTimeUserParticleSystem`, `MagicTimeUserAudioSource`,
  `MagicTimeUserAudioMixer`, `MagicTimeUserTrailRenderer`). See `magic-time-users.md`.

---

## Rule 6 — frozen (0) and `InverseTimeScale`

- `Value = 0` (or `Pause()`) freezes the object's local time: anything reading
  `DeltaTime` stops, but Unity **still calls `Update()`** — guard work that must not
  run while frozen (e.g. `if (TimeScale <= 0f) return;`).
- `InverseTimeScale` is `Time.timeScale / TimeScale`, so it **divides by zero when
  the object is fully paused** (→ `Infinity`). If you consume `InverseTimeScale`
  directly, guard against a 0 `TimeScale`.

---

## Rule 7 — pooling / reuse

`MagicTimeUser` re-initializes on `OnEnable` (re-subscribes its own scale, restarts
initial-scale subscriptions, resets to `defaultTimeScale`), so it pools fine. But
the per-object accumulators **`Time` / `UnscaledTime` are not reset on enable** — a
reused object carries its previous elapsed time. If your logic means "seconds since
this spawn," capture a baseline yourself:

```csharp
float _spawnTime;
protected override void OnEnable() { base.OnEnable(); _spawnTime = Time; }   // Time = user clock
float AliveFor => Time - _spawnTime;
```

---

## Custom `LocalTimeScale`-driven logic (events)

`LocalTimeScale` raises events you can subscribe to for reactive logic (UI, audio
ducking, gameplay gates) — both C# `Action`s and inspector `UnityEvent`s:

```csharp
var slow = MagicTimeManager.Instance.TimeScale("SlowMo");
slow.OnTimeScaleChanged += (value, scale) => UpdateVignette(value);
slow.OnPaused  += s => ShowFrozenOverlay();
slow.OnResumed += s => HideFrozenOverlay();
```

Unsubscribe in `OnDisable`/`OnDestroy` to avoid leaks, and remember a scale with
`AutoRemoveWhenEmpty` can be disposed when its last subscriber leaves (you'll get
`OnTimeScaleDestroyed`).

---

## Pre-ship checklist

Before claiming custom code is done:

- [ ] Every per-frame consumer on the object uses `user.DeltaTime` /
      `user.FixedDeltaTime` / `user.Time` — no stray `Time.deltaTime`.
- [ ] If inheriting `MagicTimeUser`: every overridden `Awake/OnEnable/Update/
      OnDisable/OnDestroy` calls its `base.X()`.
- [ ] If not inheriting: a `MagicTimeUser` component is on the object and you read
      time off it (`[RequireComponent(typeof(MagicTimeUser))]`).
- [ ] Values set via `SetValue`/`Pause`/`Resume`, never `Value =`. No per-frame
      `SetValue` during a transition (or `transitionDuration = 0`).
- [ ] Combined (multiplied) scale reasoned through, not each scale alone.
- [ ] Frozen (`TimeScale == 0`) handled; no `InverseTimeScale` divide-by-zero.
- [ ] Coroutine waits use local-time accumulation, not `WaitForSeconds`, where the
      delay should slow with the object.
- [ ] Pooled objects don't assume `user.Time == 0` on enable.
- [ ] Code compiles — check `mcp__unity-mcp__Unity_GetConsoleLogs` (`logTypes:
      "Error"`) before reporting success. Then hand the playtest to the user.
