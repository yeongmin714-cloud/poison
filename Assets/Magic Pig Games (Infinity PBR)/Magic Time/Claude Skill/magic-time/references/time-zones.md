# Time Zones — building & managing

A **`TimeZone`** is a trigger volume that owns its own `LocalTimeScale` and
**subscribes any entering Magic Time object to it**, then unsubscribes on exit. Walk
a Magic Time User into a slow zone → it slows (the zone's scale stacks
multiplicatively with whatever else affects it); walk out → it reverts.

```csharp
public class TimeZone : MonoBehaviour
```

---

## Requirements (get these right or it silently does nothing)

1. **A `Collider` set as a trigger** on the zone GameObject. `TimeZone.Awake`
   forces `isTrigger = true` (and warns if it had to flip it), but add the collider
   yourself and size it to the area (a `SphereCollider` for a bubble, `BoxCollider`
   for a room). 2D works too (`Collider2D` + the 2D trigger callbacks).
2. **Entering objects must have `IHaveLocalTime`** — in practice a `MagicTimeUser`
   (inherited or as a component) on the entering collider's GameObject or an
   ancestor. The zone uses `GetComponentInParent<IHaveLocalTime>()`. Plain objects
   are simply unaffected — this is exactly how you exclude things (see below).
3. **The entering object's own collider must NOT be a trigger.** The zone ignores
   trigger-vs-trigger overlaps (`if (other.isTrigger) return;`). The mover needs a
   normal (non-trigger) collider to register.
4. A **`MagicTimeManager`** in the scene is recommended (the zone's scale is
   disposed through it on teardown, and your Users need it for their other scales).

---

## Configuration

| Field | Meaning |
|---|---|
| `timeScaleValue` (`float`, default `1`) | The starting value of the scale the zone creates. `0.3` = 30% speed inside; `0` = frozen; `2` = double. |
| `timeScaleToUse` (`LocalTimeScale`) | Optional source asset. If set (and `createLocalTime` is off), the zone **instantiates a per-zone copy** of it (each zone gets its own unique instance — the asset is never shared/mutated). |
| `createLocalTime` (`bool`, hidden) | When true (default), the zone makes a fresh scale from `timeScaleValue` instead of copying `timeScaleToUse`. Toggled via the custom inspector. |
| `OnObjectEnter` / `OnObjectExit` | `UnityEvent<TimeZoneEventData>` fired on enter/exit. `TimeZoneEventData` carries `{ gameObject, collider, contactPoint }`. |

Runtime surface:

```csharp
public LocalTimeScale TimeScale => timeScale;     // the live per-zone scale
public float TimeScaleValue => timeScaleValue;
public void SetTimeScaleStartingValue(float value);   // set before Awake/first use
```

---

## Build a zone via the bridge

```csharp
using UnityEngine;
using UnityEditor;
using MagicPigGames.MagicTime;
using MagicPigGames.MagicTime.AgentTools;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var zone = MagicTimeAgentTools.CreateTimeZone("Slow Bubble", value: 0.3f, size: Vector3.one * 4f);
        // CreateTimeZone makes a GameObject + trigger collider + TimeZone, sets timeScaleValue.
        result.Log($"Created time zone '{zone.name}' at 0.3x");
    }
}
```

Or by hand in a CommandScript: `new GameObject`, `AddComponent<SphereCollider>()`
with `isTrigger = true`, `AddComponent<TimeZone>()`, then set `timeScaleValue` via
`SerializedObject` (it's a `protected [SerializeField]`) or call
`SetTimeScaleStartingValue` before play.

---

## Stacking & combining

Zone scales **multiply** with everything else affecting the object:

- An object at its own `0.5` that enters a `0.3` zone runs at `0.5 × 0.3 = 0.15`.
- Two overlapping zones (`0.5` and `0.5`) stack to `0.25` while the object is in
  both; leaving one returns it to `0.5`.

This is automatic — entering subscribes the object to the zone's scale, exiting
unsubscribes it, and `MagicTimeUser` recomputes the product. The recovery feel when
leaving is governed by **the object's** `transitionDuration`/`transitionCurve`, not
the zone's.

---

## Excluding objects (e.g. "doesn't affect the player")

Three clean options, in order of preference:

1. **Don't make the excluded object a Magic Time User.** The zone finds no
   `IHaveLocalTime` on it and can't subscribe it. Simplest when the player never
   needs local time.
2. **Layer-based collision filtering.** Put the player on a layer the zone's
   collider doesn't collide with (Physics layer collision matrix, or set the zone
   collider's `includeLayers`/`excludeLayers`). Triggers still obey the matrix.
3. **A `TimeZone` subclass with a tag/type check** when the player must be a User
   but shouldn't be slowed:

   ```csharp
   using UnityEngine;
   using MagicPigGames.MagicTime;

   public class FilteredTimeZone : TimeZone
   {
       protected override void OnEnter(IHaveLocalTime user, Collider other)
       {
           if (other.CompareTag("Player")) return;   // skip the player
           base.OnEnter(user, other);
       }
       // override OnExit similarly if you filter on exit
   }
   ```

   `OnEnter` / `OnExit` are `protected virtual`, so subclassing is the supported
   extension point.

---

## Managing zones at runtime

- **Change the slowdown live:** `zone.TimeScale.SetValue(0.1f);` (the per-zone
  scale is a normal `LocalTimeScale` — use `SetValue`/`Pause`/`Resume`, never assign
  `Value`).
- **Pause/freeze the zone:** `zone.TimeScale.Pause();` / `.Resume();`.
- **React to entries:** wire `OnObjectEnter` / `OnObjectExit` in the inspector, or
  in code, to trigger VFX/SFX/UI when something enters the field. The event payload
  tells you which GameObject and where it touched.
- **Teardown:** `OnDisable` unsubscribes everyone; `OnDestroy` disposes the zone's
  scale. Objects revert automatically.

---

## Gotchas

- **Nothing slows?** Check: zone collider is a trigger; the mover has a
  *non*-trigger collider; the mover is a Magic Time User (has `IHaveLocalTime`); and
  the mover's motion code reads `user.DeltaTime`, not `Time.deltaTime` (a zone only
  changes the *number* `DeltaTime` returns — code still has to use it).
- **Only the visuals slow, not movement (or vice-versa)?** Some scripts on the
  object read `DeltaTime`, others read `Time.deltaTime`. Audit every per-frame
  consumer on the slowed object.
- **Each zone has its own scale instance** even when several reference the same
  `timeScaleToUse` asset — they don't share a value. Drive them individually, or
  have all the objects subscribe to one shared named scale instead of using zones if
  you want a single global control.
