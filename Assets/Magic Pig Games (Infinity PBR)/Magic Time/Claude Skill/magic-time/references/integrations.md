# Integrations — Magic Time with other Magic Pig Games assets

Magic Time is "just" per-object time, so anything whose motion/timers read a time
value can be made to respect it. This file covers how it combines with **Juicy
Actions**, **Projectile Factory**, and **Game Modules 4**.

**Detect before you use — don't assume from the conversation.** An integration may
or may not be imported. Check the actual project first:

```bash
# Is the other asset present at all?
grep -rl "namespace MagicPigGames.JuicyActions"     --include="*.cs" Assets | head
grep -rl "namespace MagicPigGames.ProjectileFactory" --include="*.cs" Assets | head
grep -rl "namespace InfinityPBR.Modules"            --include="*.cs" Assets | head
# Is a Magic Time bridge/integration package imported for it?
grep -rl "MagicTimeIntegration\|MagicTimeClockResolver\|MagicTime" --include="*.cs" Assets | head
```

If a bridge isn't present, fall back to the **universal pattern**: make the object
a `MagicTimeUser` and ensure its motion code reads `user.DeltaTime` (see
`writing-custom-code.md`). That works regardless of which other systems are
installed.

---

## Juicy Actions (verified — there is a real integration package)

When the **Juicy Actions ↔ Magic Time integration** is imported (files under a
`Magic Time Integration/` folder; namespace
`MagicPigGames.JuicyActions.MagicTimeIntegration`), two things light up:

### 1. The headline: JA executors on Magic Time objects run on local time — automatically

`MagicTimeClockResolver : IClockResolver` (priority `1000`) **auto-registers at
startup** via `[RuntimeInitializeOnLoadMethod]` (`ActionClock.Register(...)`). When
an `ActionExecutor` runs on a GameObject that has `IHaveLocalTime` (i.e. a Magic
Time User) in its parent chain, the resolver wraps it as a `MagicTimeObjectClock`
(an `IClock`) so **every clock-driven action that executor runs follows that
object's local time**. A slowed/zoned object's juice (shakes, tweens, flashes)
slows with it — **no per-action setup**.

So the recommended way to make JA respect Magic Time is simply: **make the object a
Magic Time User.** The resolver does the rest. (JA actions read
`Executor.Clock.DeltaTime`, never `UnityEngine.Time`, which is what makes this
work.)

### 2. Actions that animate Magic Time values

Two `ActionOverTimeWithBaseValues` actions let JA *drive* Magic Time (animate a
value over a curve, with base-value restore):

- **`SetMagicTimeTimescaleAction`** — `Create > Juicy Actions/Magic Time/Set
  Timescale`. Animates one or more `LocalTimeScale`s' value to a target over
  `duration`. It resolves your `LocalTimeScale` *assets* to their **runtime
  instances by name through `MagicTimeManager.Instance`** (`manager.TimeScale(name,
  …, createIfNull:true)`), with target-count/selection-mode options.
- **`SetTimeZoneTimescaleAction`** — the same, for `TimeZone`s.

Both **require a `MagicTimeManager` in the scene** — they `LogWarning` and bail if
it's absent (`CanExecute` returns false). Target selection is via the
`ILocalTimeScaleTargets` / `ITimeZoneTargets` interfaces.

**Use it for:** a parry that ramps a "SlowMo" scale to 0.2 and back via a JA
sequence; a hit that briefly drops an enemy's local time; any designer-driven
time-warp built from JA's curve/override system instead of hand-written C#.

> For *global* slow-mo/hitstop without Magic Time, JA has its own
> `SetTimescaleAction` / `PauseTimeAction` (those touch `Time.timeScale`). Use the
> Magic Time actions when the slowdown should be **per-object / per-zone**.

The full JA-side notes live in the Juicy Actions skill at
`references/integrations/magic-time.md`.

---

## Projectile Factory (no shipped bridge detected — use the universal pattern)

There is **no Magic Time integration package inside Projectile Factory** in the
projects checked — so don't reference integration-specific classes. Make projectiles
respect local time with the universal pattern, after detecting PF
(`grep -rl "namespace MagicPigGames.ProjectileFactory"`):

- **Make the projectile (or its mover) a Magic Time User** and ensure the code that
  advances it reads `user.DeltaTime` rather than `Time.deltaTime`. If PF's motion
  runs through its own behavior components, audit those for the time source; for
  custom behaviors you write, read local time (PF's own custom-behavior rules are in
  the Projectile Factory skill).
- **Pooling overlaps both systems.** PF pools projectiles and `MagicTimeUser`
  re-initializes on `OnEnable` — fine together — but remember `user.Time` is not
  reset on reuse (capture a spawn baseline; see `writing-custom-code.md`).
- **Slow a whole volley together** by subscribing each projectile to one shared
  `LocalTimeScale` (e.g. `"SlowMo"`), or let them inherit a `TimeZone` they fly
  through.
- **If Juicy Actions is also present**, the cleanest path is often PF → JA → Magic
  Time: a PF event fires a JA executor on the projectile, and the JA clock resolver
  makes it run on the projectile's local time automatically.

Confirm what PF actually exposes in *this* project before promising exact wiring —
report the pattern, not a fabricated API.

---

## Game Modules 4 — there IS a bridge, and a turn-based recipe

GM4 **does** ship a Magic Time integration (`GameModulesMagicTimeIntegration.unitypackage`, under
`Game Modules 4/Magic Time Integration/`). Extracted, it gives **`TimeboardMagicTimeUser`** — a
`MagicTimeUser` that each frame pushes its `TimeScale` into `timeboard.SetTimeModification(...)`, so GM4's
in-game **World Clock** (`Timeboard`/`Gametime`) advances scaled by Magic Time (a bullet-time scale slows
the game calendar too).

GM4's `Timeboard`/`Gametime` is *calendar/scheduler* time (condition durations, buffs expiring, quest
timing, time-of-day) — distinct from per-object frame time, but they're meant to **share a bullet-time
scale** so a slow-mo affects both.

🪤 **The package ships WITHOUT an asmdef.** Extracted, `TimeboardMagicTimeUser.cs` falls under GM4's
`MagicPigGames.GameModules.asmdef` — which doesn't reference Magic Time — so it can't find its own base
class: `CS0246: MagicTimeUser`. **Fix:** add a tiny asmdef in the integration's `Scripts/` folder
referencing **`MagicPigGames.GameModules` + `MagicPigGames.MagicTime`** (its own optional assembly). Do
*not* move the file into GM4's `Scripts/` — that would force GM4 itself to depend on Magic Time.

🪤 **The `Timeboard` auto-ticks.** `Timeboard.Update()` calls `gametime.Tick(timeModification)` every
frame — so you **set `timeModification`** (via `SetTimeModification` or the bridge), you do **not** call
`Tick`, and you must not have two things driving it (e.g. a `TimeboardMagicTimeUser` *and* your own driver).

### Two-clock turn-based / strategy-pause (the recipe)

**The trap:** don't pause turns by freezing a shared Magic Time scale to 0 — that freezes idle
animations, particles and projectiles into statues. Use **two clocks**:

1. **World Clock** = GM4 `Timeboard` (strategic / scheduler time). Pause it by driving its
   `timeModification` to **0**; advance it at the bullet-time scale's value otherwise.
2. **Local motion** = each object's own Magic Time (movement + animation). **Always runs** so things keep
   idling/moving; obeys zones + slow spells. Untouched by the turn pause.

Both share one **bullet-time master** scale (e.g. `"Global"`, ×1 normally): it scales the World-Clock
advance *and* cascades into local motion, so a slow-mo slows everything together.

A driver `MonoBehaviour` each frame:
```csharp
var global = manager.TimeScale("Global").Value;       // bullet-time master (×1 normally)
if (turnBased && actionWait > 0f) {                    // an action is "spacing out"
    actionWait -= Time.deltaTime * global;             // spend the wait in scaled real time
    timeboard.SetTimeModification(0f);                 // HOLD the world clock
} else {
    timeboard.SetTimeModification(global);             // advance (real-time / idle), bullet-time scaled
}
```
Actors **watch** the clock — they never touch it: when `timeboard.gametime.Now()` passes an actor's
next-action time it acts, calls `SetActionWait(0.5f)` (take the **max**, not additive — several actors in
one frame must not stack), and re-arms its next time. Movement inside the action reads the actor's
**local** `DeltaTime`, so zones/slows still apply while the world clock is held.

---

## Summary

| Other asset | Shipped Magic Time bridge? | How to make it respect local time |
|---|---|---|
| **Juicy Actions** | **Yes** | Make the object a User → the auto-registered clock resolver runs its executors on local time. Animate scales/zones with `SetMagicTimeTimescaleAction` / `SetTimeZoneTimescaleAction` (need `MagicTimeManager.Instance`). |
| **Projectile Factory** | Not detected | Universal pattern: make projectiles Users, read `DeltaTime`; shared scale or zone for a volley. |
| **Game Modules 4** | **Yes** — `TimeboardMagicTimeUser` (ships **without an asmdef** → add one) | Bridge scales the `Timeboard`'s `timeModification` by a Magic Time scale. For turn-based, drive `timeModification` (0 = hold, scale = advance) — a *World Clock* separate from always-running local motion. See the two-clock recipe above. |

Whatever the asset: **detect first, prefer making the object a Magic Time User and
reading `DeltaTime`, and report the real wiring you find — never a guessed API.**
