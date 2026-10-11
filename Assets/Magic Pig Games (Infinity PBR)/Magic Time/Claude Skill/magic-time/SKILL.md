---
name: magic-time
description: >-
  Set up and extend Magic Time (Infinity PBR) in a Unity project — add the Magic
  Time Manager to a scene, create Local Time Scale assets (a Global scale, named
  scales), make GameObjects and your own scripts into Magic Time Users (per-object
  / "local" time), build and manage Time Zones, and WRITE C# that drives motion,
  timers, and animation off local time instead of UnityEngine.Time. Use this skill
  whenever the user is in a Unity project that contains Magic Time / Magic Pig Games
  / the MagicPigGames.MagicTime namespace and asks to create, add, set up, configure,
  or script anything about per-object or per-area time — slow motion, bullet time,
  hitstop / freeze-frame, time stop / pause, stasis, time dilation, haste / slow /
  speed-up, time bubbles, slow zones, time-warp fields, ramping or scaling time for
  one object or a group — even when they don't say "Magic Time" explicitly (e.g.
  "make this enemy move in slow motion while the player is normal", "freeze
  everything except the player", "add a slow-mo bubble the player can walk through",
  "make my custom mover respect slow-motion", "ramp the world to 0.2× and back on a
  parry"). It knows the LocalTimeScale (ScriptableObject) / MagicTimeManager
  (singleton) / MagicTimeUser / IHaveLocalTime / TimeZone architecture, that local
  scales MULTIPLY and time properties are per-instance (myUser.DeltaTime, never a
  static), drives the Unity Editor through the unity-mcp bridge to add the manager,
  create the scales, make objects Users, and build the zones for real, and writes
  the runtime C# that makes existing and custom classes obey local time correctly.
---

# Magic Time

Magic Time is a Unity asset by Magic Pig Games / Infinity PBR
(namespace `MagicPigGames.MagicTime`). It brings **independent, stackable,
per-object time scales** to a project: instead of one global `Time.timeScale`,
each object (or area) can have its own "local time" — one enemy in slow motion,
a bullet-time field the player walks through, a frozen statue while everything
else moves. The guiding promise for the user is: *give any object its own time
scale, combine scales freely, and write motion code that reads
`myUser.DeltaTime` instead of `Time.deltaTime` so it just works.*

This skill does all of it for the developer: **set up the time system in a scene,
create Local Time Scale assets, make GameObjects and the developer's own scripts
into Magic Time Users, build and manage Time Zones, and write the custom C# that
runs on local time** — getting the non-obvious rules (scales multiply, `base.*`
calls are mandatory, time properties are per-instance) right.

## First: confirm this is a Magic Time project

Before doing anything, verify Magic Time is present (don't assume from the
conversation):

- Look for the source folder `Assets/**/Magic Time/Scripts/` and the namespace
  `MagicPigGames.MagicTime` in `.cs` files.
- Quick check: `grep -rl "namespace MagicPigGames.MagicTime" --include=*.cs Assets | head`.
- Component tell: a `MagicTimeManager`, `MagicTimeUser`, or `TimeZone` component
  in the scene, or `LocalTimeScale` assets in the project.
- The editor scripts reference the `MagicPigGames.Shared` assembly ("MagicPig
  Shared", ships alongside Magic Time) for inspector chrome — it's a normal
  dependency, don't treat it as removable.

If Magic Time is **not** found, say so and stop — this skill only applies to
Magic Time projects.

## Mental model — read this before acting

Five types, in two layers (assets on disk + the singleton; runtime objects in the
scene):

1. **`LocalTimeScale`** — the **unit of time control. A `ScriptableObject`** with
   one `Value` (1 = normal, 0.5 = half speed, 0 = paused/frozen, 2 = double). Its
   `Value` **setter is private** — change it with `SetValue(float)`, `Pause()`, or
   `Resume()`. Objects *subscribe* to a scale to be affected by it. The asset on
   disk is **never mutated at runtime** — the manager and time zones clone it. It
   carries change/pause/subscribe events (C# `Action`s + inspector `UnityEvent`s).
2. **`MagicTimeManager`** — a **singleton MonoBehaviour** (`MagicTimeManager.Instance`,
   `DontDestroyOnLoad`). Put **one** in the scene. It clones the `LocalTimeScale`
   assets you list as *initial scales*, hands them out by name
   (`Instance.TimeScale("Global")`), and **`Tick()`s every live scale each frame**.
   **Missing manager = named/initial subscriptions block forever** (the object's
   own personal scale still works, but shared/named scales never attach). Always
   have one if you use named scales or Time Zones.
3. **`MagicTimeUser`** — the **base class you make objects from** (a `MonoBehaviour`
   implementing `IHaveLocalTime`). Each User has its **own personal scale** *and*
   can subscribe to any number of shared scales. Read time off the **instance**:
   `myUser.DeltaTime`, `myUser.TimeScale`, `myUser.Time` — **there is no static
   `DeltaTime`.** Subscribed scales **MULTIPLY** (0.5 × 0.5 = 0.25, not 1.0).
   Transition smoothing (`transitionDuration`, `transitionCurve`) lives **here**,
   not on the scale.
4. **`IHaveLocalTime`** — the **interface** `MagicTimeUser` implements. Implement
   it directly only when you can't change a class's base (covered in
   `writing-custom-code.md`); otherwise inherit `MagicTimeUser`.
5. **`TimeZone`** — a **trigger volume** (`MonoBehaviour` on a trigger `Collider`)
   that owns its own `LocalTimeScale` and **subscribes/unsubscribes any entering
   object that has `IHaveLocalTime`** (found via `GetComponentInParent`). Walk a
   slowed object into the zone → it slows further (zones stack multiplicatively
   with the object's other scales); walk out → it reverts.

**The two non-negotiable code rules** (their absence is the #1 source of "it
doesn't work"): scales **multiply**, and when you subclass `MagicTimeUser` you
**must call `base.Awake()/OnEnable()/Update()/OnDisable()/OnDestroy()`** in any
override — the source itself warns *"If this isn't firing, make sure you are not
overriding Update in a subclass!!!"*. There is **no `Start` and no `FixedUpdate`**
on the base.

Four ready-made specialized Users also ship — `MagicTimeUserAudioSource`,
`MagicTimeUserAudioMixer`, `MagicTimeUserParticleSystem`,
`MagicTimeUserTrailRenderer` — drop-on components that pitch audio, slow particle
simulation/lifetime, and stretch trails to match an object's local time. See
`magic-time-users.md`.

The deep details for each area are in `references/`. **Load the relevant file
before writing code or building assets** — don't invent field names; the
references list the real ones, pulled from source.

## How you actually build and edit things

| The user wants… | You… | Reference |
|---|---|---|
| **the time system set up** | add one `MagicTimeManager` to the scene, create the `LocalTimeScale` assets (a `Global`, named scales), list them as the manager's initial scales | `setup.md` |
| **an object to run on local time** | make it a `MagicTimeUser` (inherit it, or add the component), set its `defaultTimeScale` / `initialTimeScales` | `magic-time-users.md` |
| **an existing class / actor to obey local time** | inherit `MagicTimeUser` (and fix `base.*` calls) *or* attach a `MagicTimeUser` component and read `DeltaTime` off it; or implement `IHaveLocalTime` | `magic-time-users.md`, `writing-custom-code.md` |
| **a slow / fast / frozen area** | add a trigger `Collider` + `TimeZone`, set its time-scale value | `time-zones.md` |
| **custom motion/timers that respect local time** | replace `Time.deltaTime`→`user.DeltaTime`, `SetValue`/`Pause`/`Resume` for ramps | `writing-custom-code.md` |
| **assets + scene wiring built for real** | run editor C# through the unity-mcp bridge (or the no-bridge fallback) | `setup.md` |

Asset creation and scene wiring are done by **running editor C# inside Unity
through the `unity-mcp` bridge** (Unity Editor open, bridge running — the user's
"Unity MCP setup" memory covers it). Execute C# with `mcp__unity-mcp__Unity_RunCommand`;
the `Code` must follow Unity MCP's **"Golden Template"** — a class named
`CommandScript`:

```csharp
using UnityEngine;
using UnityEditor;
using MagicPigGames.MagicTime;

internal class CommandScript : IRunCommand   // MUST be internal, MUST be named CommandScript
{
    public void Execute(ExecutionResult result)
    {
        // ... create LocalTimeScale assets / add the manager / make Users / build zones here ...
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        result.Log("done");
    }
}
```

`references/setup.md` has the full recipes, the bundled `MagicTimeAgentTools`
helper (`scripts/MagicTimeAgentTools.cs.txt`), verification, and the **no-bridge
fallback** (write an editor script with a `[MenuItem]` the user clicks). After any
change, **verify**: read the component/asset back and check
`mcp__unity-mcp__Unity_GetConsoleLogs` (with `logTypes: "Error"`) for compile/
runtime errors.

Writing **custom C#** (making a class respect local time) is just adding a `.cs`
file to the project — **no bridge needed** for that; the bridge is for *assets and
scene wiring*.

## How to work with the user

A wrong scale or a missing `base.Update()` is invisible until playtest, so **plan
before you build** and confirm anything non-obvious:

1. **Restate the goal and decompose it into Magic Time parts.** "Make this enemy
   move in slow motion while the player is normal" → ensure a `MagicTimeManager`
   exists, make the enemy a `MagicTimeUser` (and its movement code read
   `DeltaTime`), and set its scale (its own personal scale to 0.5, or subscribe it
   to a shared "SlowMo" scale). The player stays untouched.
2. **Reach for what ships before writing C#.** For audio/particles/trails on a
   slowed object, use the specialized User components rather than custom code. For
   "slow everything," one shared `Global`/`SlowMo` scale many objects subscribe to
   beats per-object tweaking.
3. **Propose smart defaults and surface the choices Magic Time implies** — this is
   where you add value:
   - **Per-object vs shared scale.** One object slowing → set its personal scale.
     A group slowing together → one shared `LocalTimeScale` they all subscribe to,
     driven from one place.
   - **Scales multiply.** Subscribing an object to two 0.5 scales gives 0.25.
     Factor in any scale it already has (its personal scale, a zone it's standing in).
   - **Transition or snap.** A smooth ramp (bullet-time fade) uses the User's
     `transitionDuration`/`transitionCurve`; instant changes (a hard freeze) want
     `transitionDuration = 0`. Don't call `SetValue` every frame while a transition
     is running — it restarts the curve (a documented trap).
   - **What "frozen" means.** `Value = 0` pauses an object's local time; its
     `Update`/animation that reads `DeltaTime` stops, but Unity still calls
     `Update()`. Physics on a frozen object needs care (it isn't a global pause).
   - **Manager presence.** Named scales and Time Zones need a `MagicTimeManager`
     in the scene; offer to add one if it's missing.
4. **Ask only what you can't sensibly default** (which object(s), the scale value,
   ramp vs snap, one object or a shared group, should the player be affected). One
   concise batch, then proceed.
5. **Build, verify, and report** what you created, where it lives, and how to drive
   it (e.g. "set `enemy.LocalTimeScaleValue = 0.5f`" or "call
   `manager.TimeScale(\"SlowMo\").SetValue(0.2f)`").

### Flagship walkthrough — "Add a slow-mo bubble the player can walk through, that doesn't affect the player"

1. Confirm Magic Time is present and (if building) the bridge responds (a trivial
   `CommandScript` that logs `typeof(MagicTimeUser).FullName`).
2. **Ensure the time system exists.** Add one `MagicTimeManager` to the scene if
   absent (zones and named scales rely on it).
3. **Make the things that SHOULD slow into Users.** Enemies / projectiles / props
   inside the bubble must be `MagicTimeUser`s (inherit or component) with their
   motion reading `DeltaTime`. **Leave the player a plain object — not a
   `MagicTimeUser`** — so the zone finds no `IHaveLocalTime` on it and can't slow
   it. (If the player must be a User for other reasons, exclude it by layer on the
   zone collider or a tag check in a `TimeZone` subclass — see `time-zones.md`.)
4. **Build the bubble.** Create a GameObject with a **trigger** `SphereCollider`
   and a `TimeZone`, set its `timeScaleValue` (e.g. `0.3`). On enter it subscribes
   the entering User to its scale (stacking multiplicatively with any scale the
   object already has); on exit it unsubscribes and the object reverts.
5. **Verify** (read the zone/manager back, check the console) and **report**: "The
   bubble is a trigger volume at 0.3× local time. Enemies and projectiles that are
   Magic Time Users slow inside it and recover on exit; the player is unaffected
   because it isn't a Magic Time User. Tune the slowdown with the zone's
   `timeScaleValue`, and the recovery feel with each object's `transitionDuration`."

## Object catalog

What you work with, where it comes from (the manual right-click / AddComponent
route — you normally do this from code or the bridge), and the reference with the
details.

| Thing | Where it comes from | Purpose | Reference |
|---|---|---|---|
| `LocalTimeScale` | `Create > Local Time Scale > Time Scale` | The unit of time control: one `Value` objects subscribe to | `setup.md` |
| `MagicTimeManager` | `AddComponent` on one scene GameObject | Singleton: clones/serves named scales by name, ticks all live scales | `setup.md` |
| `MagicTimeUser` | inherit it, or `AddComponent` | Makes an object/class run on local time; exposes `DeltaTime`/`TimeScale`/`Time` | `magic-time-users.md` |
| `MagicTimeUserAudioSource` | `AddComponent` | Pitches child `AudioSource`s to match the object's `TimeScale` | `magic-time-users.md` |
| `MagicTimeUserAudioMixer` | `AddComponent` | Pitches an `AudioMixer` parameter (dB) to match `TimeScale` | `magic-time-users.md` |
| `MagicTimeUserParticleSystem` | `AddComponent` | Drives `ParticleSystem` simulation speed + lifetime from `TimeScale` | `magic-time-users.md` |
| `MagicTimeUserTrailRenderer` | `AddComponent` | Stretches `TrailRenderer.time` by `InverseTimeScale` | `magic-time-users.md` |
| `TimeZone` | `AddComponent` on a trigger `Collider` | A volume that applies its scale to entering `IHaveLocalTime` objects | `time-zones.md` |
| `IHaveLocalTime` | implement on your own class | Make a class a User without inheriting `MagicTimeUser` | `writing-custom-code.md` |

## Reference files

Load the file that matches the task; each lists exact field names, real APIs,
worked recipes, and gotchas pulled from source.

- `references/architecture.md` — the object model in depth: `LocalTimeScale`
  (value, events, subscribers, clone-not-mutate), `MagicTimeManager` (singleton,
  named scales, the per-frame `Tick`), `MagicTimeUser` (personal + subscribed
  scales, the multiply-combine, transitions, the per-instance time properties, the
  lifecycle hooks), `IHaveLocalTime`, `TimeZone`, and the four specialized Users.
  Read this to understand how it all fits.
- `references/setup.md` — **the build how-to.** Setting up the time system: the
  unity-mcp `CommandScript` recipe for adding the `MagicTimeManager` and creating
  `LocalTimeScale` assets (a `Global` + named scales), listing initial scales,
  driving a scale's value/pause at runtime, the bundled `MagicTimeAgentTools`
  helper, verification, and the no-bridge fallback. Start here for "set up Magic Time."
- `references/magic-time-users.md` — **creating & configuring Users.** Making a
  GameObject a `MagicTimeUser` (inherit vs attach-a-component), making an existing
  class / actor a User, `defaultTimeScale` / `initialTimeScales`, subscribing to
  shared scales, and the four specialized audio/particle/trail Users.
- `references/time-zones.md` — building and managing `TimeZone`s: the required
  trigger collider, per-zone scale value, enter/exit events, stacking with other
  scales, excluding objects (layers/tags), and runtime control.
- `references/writing-custom-code.md` — **read before writing any custom C#.**
  Subclassing `MagicTimeUser` (and the mandatory `base.*` calls), replacing
  `Time.deltaTime` with `user.DeltaTime`, implementing `IHaveLocalTime` when you
  can't inherit, the multiply rule, the transition-every-frame trap, `Pause(0)` /
  `InverseTimeScale` divide-by-zero, pooling/reset of the per-object clock, and a
  pre-ship checklist.
- `references/integrations.md` — how Magic Time combines with **Projectile
  Factory**, **Juicy Actions**, and **Game Modules 4**: a detect-before-use rule
  and how to make each system's time-based motion respect local time.

Official docs (authoritative; deeper tables, demos, FAQ):
<https://infinitypbr.gitbook.io/magic-pig-games/magic-time-local-time-scale>.

## Conventions & guardrails

- **Scales multiply, never add.** Combining a 0.5 and a 0.5 scale on one object
  gives 0.25. A User's own personal scale is part of the product. Reason about the
  *combined* result, not each scale in isolation.
- **Time is per-instance.** Use `myUser.DeltaTime` / `myUser.FixedDeltaTime` /
  `myUser.Time` / `myUser.TimeScale` — **there is no static `DeltaTime`**. Replace
  `Time.deltaTime` in motion/timer code with the User's `DeltaTime`.
- **Always call `base.*` in overrides.** Overriding `Awake`/`OnEnable`/`Update`/
  `OnDisable`/`OnDestroy` on a `MagicTimeUser` subclass without the matching
  `base.X()` stops the per-object clock and freezes transitions. There is no
  `Start`/`FixedUpdate` on the base.
- **Never set `LocalTimeScale.Value` directly** (the setter is private). Use
  `SetValue(float)`, `Pause()`, `Resume()`. Don't call `SetValue` every frame
  during a transition — it restarts the curve.
- **One `MagicTimeManager` per scene** if you use named scales or Time Zones.
  Without it, initial/named subscriptions block; with two, the duplicate
  self-destructs.
- **Assets are cloned, not mutated.** The manager and `TimeZone`s clone the source
  `LocalTimeScale` asset at runtime — edit the asset to change defaults, but expect
  per-object/per-zone instances at play time. Don't rely on writing back to the asset.
- **`TimeZone` needs a trigger collider; entering objects need `IHaveLocalTime`.**
  The zone uses `GetComponentInParent<IHaveLocalTime>()` and ignores trigger-vs-
  trigger overlaps — the entering object's own collider must not be a trigger.
- **Editor-only APIs stay in the editor.** `AssetDatabase`, `CreateInstance` for
  assets, `SerializedObject` edits — keep inside the `CommandScript` / `#if
  UNITY_EDITOR`, never in shipped runtime code. Write custom runtime C# into the
  developer's own scripts folder, not inside the Magic Time asset.
- **Don't restructure the developer's existing scales/Users** without confirming —
  references are by object/asset reference and break quietly until playtest.
- **Verify, then report.** State plainly what you built and where; if the console
  shows errors or a component didn't read back, say so rather than assuming
  success. (The user runs the actual in-editor playtest — visual smoke testing is
  their job, not yours.)
