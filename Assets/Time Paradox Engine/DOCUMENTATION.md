# Time Paradox Engine — Manual

Rewind, slow down and freeze time — per object, not just globally.

> **New here?** Open **Tools → TimeWarp → Hub**. It contains this manual in context,
> creates objects for you, and validates your scene. This document is the reference you
> read when you want the whole picture.

---

## Contents

1. [Quick start](#1-quick-start)
2. [How the engine works](#2-how-the-engine-works)
3. [The Manager](#3-the-manager)
4. [Identity and channels](#4-identity-and-channels)
5. [Modules reference](#5-modules-reference)
6. [Characters](#6-characters)
7. [Time Zones](#7-time-zones)
8. [Kinetic force](#8-kinetic-force)
9. [Time Echoes (ghosts)](#9-time-echoes-ghosts)
10. [Your own variables](#10-your-own-variables)
11. [Interface / HUD](#11-interface--hud)
12. [Post-processing](#12-post-processing)
12b. [Force field shader](#12b-force-field-shader)
13. [Performance](#13-performance)
14. [Scripting API](#14-scripting-api)
15. [Troubleshooting](#15-troubleshooting)
16. [Deployment](#16-deployment)

---

## 1. Quick start

Three steps to a working rewind.

**1. Add the manager.**
`Tools → TimeWarp → Hub → Start here → Create Time Warp Manager`. One per scene.

**2. Make something rewindable.**
Select an object, then `Hub → Objects`, pick what it is, press Apply. The wizard inspects
the object and only adds modules that fit — it will not add physics recording to something
without a Rigidbody.

**3. Trigger it from code.**

```csharp
using TimeWarp.Core;
using TimeWarp.Core.Managers;

// Start rewinding
TimeWarpManager.Instance.TriggerAction(GlobalAction.Rewind);

// Stop
TimeWarpManager.Instance.StopGlobalAction();
```

That is the entire API for a basic rewind. A complete working example lives in
`Control System/RewindTester.cs`.

**Then run `Tools → TimeWarp → Validate Scene`.** It catches the configuration mistakes
that otherwise produce no error and simply make things quietly not work.

---

## 2. How the engine works

### Three pieces

| Piece | Count | Role |
|---|---|---|
| **Manager** | one per scene | Owns the clock. Decides what a global rewind or slow motion means, and drives every recorded object in one batched job. |
| **Identity** | one per object | Records a rolling history of that object. An object without one ignores time entirely. |
| **Modules** | as needed | Small components teaching an Identity what *else* to record: physics, animation, particles, audio, AI. |

### Global time × local time

An object's speed is the product of two numbers:

- **Global scale** — from the manager. A rewind or slow motion you trigger from code.
- **Local scale** — from Time Zones. Affects only objects inside them.

`Effective scale = Local × Global`

This is why a character standing in a 0.2× slow-motion zone during a 0.5× global slow
motion runs at 0.1×, and why "rewinding while inside a slow-motion zone" is a genuinely
distinct state from either alone.

### Unity's `Time.timeScale` is never touched

The engine scales time **per object**, reading `Time.timeScale` but never writing it. Two
consequences worth knowing:

- Your UI, menus and music keep running at full speed during a time stop, for free.
- The engine can never leave your editor in a modified state after you exit Play mode.

### The history buffer

Each Identity allocates a circular buffer of `CaptureFramerate × MaxRecordTime` frames.
The size is rounded **up to a power of two** so indexing can use a bitmask instead of a
modulo — fast, but it means 720 requested frames allocate 1024. The manager inspector
shows the real number and warns when the rounding wastes a lot.

`CaptureFramerate` is overwritten at runtime from the physics timestep. Treat the field as
a preview; change **Project Settings → Time → Fixed Timestep** if you want a different rate.

---

## 3. The Manager

Inspector tabs: **Setup · Action · Feel · Performance · Events**

### Engine Mode — read this first

| Mode | Meaning | Use when |
|---|---|---|
| **Global Only** | Anomalies are triggered from code and affect everything. | Bullet time, a rewind ability, a pause power. Most projects. |
| **Local Zones Only** | No global anomaly at all; time changes only inside Time Zones. | Puzzle games where specific rooms behave differently. |
| **Hybrid** | Both, multiplied together. | You want zones *and* a global ability. |

> If a global rewind seems to do nothing, check Mode is not **Local Zones Only**.

### History

| Setting | Notes |
|---|---|
| Capture Framerate | Snapshots per second. Driven by the physics timestep at runtime. |
| Max Record Time | How far back a rewind can reach. 10 s is generous; a rewind ability rarely needs more than 3. |

Every recorded object allocates `framerate × seconds` of history, so halving Max Record
Time halves the memory used by the whole scene. Individual objects can shorten it further
with **Record Time Override**.

### Action

| Setting | Effect |
|---|---|
| Rewind Speed | How fast history is consumed. At 3× a rewind burns Max Record Time three times as fast. |
| Rewind Acceleration Curve | Shapes speed over the rewind's life. Starting slow and accelerating reads far better than a constant rate. |
| Target Slow Mo Scale | 0.2 is strong, obvious bullet time; 0.5 is a subtle drag. |

Time stop needs no settings — it is simply scale 0.

**Duration.** With `Use Max Duration` off, an anomaly runs until your code calls
`StopGlobalAction()`. With it on, the engine stops after `MaxDuration` seconds — what you
want for a resource-limited ability. The HUD bar can only show remaining time in this mode.

### Feel — entry and exit curves

Snapping from 1.0 to 0.2 reads as a stutter, not an effect. These curves ramp the global
scale in and out.

- **Entry** slower, around 0.5 s, so the transition is felt.
- **Exit** faster, around 0.3 s, because the player already understood and wants control back.

If post-processing *pops* rather than blends, raise these — the volume weight follows the
same ramp.

---

## 4. Identity and channels

`TimeWarpIdentity` is the recorder. Its inspector is tabbed, and tabs appear only for
channels you have enabled. **Simple mode** (default) hides advanced tuning; it is a view
filter only and never changes data.

### Channels

| Channel | Records | Needs |
|---|---|---|
| Position, rotation & scale | Transform | — |
| Physics (3D) | Velocity, momentum | Rigidbody + `TimeBody` |
| Physics (2D) | Velocity, momentum | Rigidbody2D + `TimeBody2D` |
| Animation | Animator state | Animator + `TimeAnimator` |
| Particles | Full particle state | ParticleSystem + `TimeParticles` |
| Shader animation | Material properties | Renderer + `TimeShader` |
| Audio | Pitch follows time | AudioSource + `TimeAudio` |
| AI navigation | Agent suspension | NavMeshAgent + `TimeNavMeshAgent` |
| Your own variables | Float fields in your scripts | `TimeWarpBridge` |
| Time Echoes | Ghost replays | `TimeParadoxModule` |

**Only tick what the object needs.** Every channel costs memory and CPU, and Particles
costs dramatically more than the rest.

### Advanced

| Setting | Notes |
|---|---|
| Record Time Override | 0 = use the manager's value. Shorten it for objects needing only a moment of history. |
| Free Memory On Disable | Releases the buffer when disabled. Good for pooled objects; history restarts on re-enable. |
| Action Filters | Restrict which anomalies affect this object, or require it to be inside a specific zone. |

---

## 5. Modules reference

| Module | Purpose |
|---|---|
| `TimeBody` / `TimeBody2D` | Records and restores Rigidbody momentum. |
| `TimeAnimator` | Rewinds the Animator. |
| `TimeParticles` | Caches or procedurally reverses particle systems. |
| `TimeVFX` | VFX Graph equivalent. |
| `TimeShader` | Rewinds animated material properties via MaterialPropertyBlock. |
| `TimeAudio` | Pitch and playback follow the time scale. |
| `TimeNavMeshAgent` | Suspends pathfinding so it does not fight playback. |
| `TimeWarpComponentManager` | The kill switch — disables *your* scripts during anomalies. |
| `TimeWarpBridge` | Rewinds float fields inside your own scripts. |
| `TimeParadoxModule` | Spawns Time Echoes. |
| `TimeEventRecorder` | Records gameplay events for echo replay. |
| `KineticAccumulator` | Stores impacts during a time stop, releases them on resume. |
| `TimeHingeJoint` | Joint handling during anomalies. |
| `TimeWarpLifecycle` | Spawn/despawn handling across rewinds. |

Modules are the engine's extension point: implement `ITimeWarpModule` and your component
is ticked automatically by the Identity on the same GameObject.

**Fault isolation.** If a module throws three times in a row it is disabled for the rest
of the session and logs an error naming the object and the script. Without this, one
buggy module would abort the tick loop and silently stop *every object after it* — a
confusing failure that looks like "time stopped working on half the scene".

---

## 6. Characters

Characters need one decision that props do not.

### The problem

A crate is easy: only the engine writes to its transform. A character is different —
**your movement script is writing to the transform too**. During a rewind both write at
once, and the result is a character that jitters, refuses to move, or drifts off the
recorded path.

### The fix

`TimeWarpComponentManager` holds a list called **Target Components**. Everything in it is
disabled when the object starts rewinding or freezes, and re-enabled the moment it stops.

**Put in:** movement controllers, input handlers, shooting, ability scripts — anything
that moves or acts on its own.

**Leave out:** the Animator, colliders, renderers, and anything from TimeWarp itself. The
engine drives those directly and disabling them breaks playback.

The **Auto-Fill** button collects your scripts and skips Unity and TimeWarp components
automatically. `Hub → Character` shows a live checklist for the selected object.

### Finer control

Implement `ITimeWarpStateListener` and your script is *told* about time instead of merely
switched off:

```csharp
public class MyController : MonoBehaviour, ITimeWarpStateListener
{
    public void OnTimeWarpSlowMoStart(float timeScale) { }
    public void OnTimeWarpTimeStopStart() { }
    public void OnTimeWarpRewindStart() { }
    public void OnTimeWarpResume() { }

    // timeScale is the object's effective scale.
    // rewindDeltaTime is negative while rewinding — real seconds of history consumed.
    public void OnTimeWarpAnomalyUpdate(float timeScale, float rewindDeltaTime) { }
}
```

### Teleporting, respawning, loading a save

Any **non-continuous move** leaves the old route in the history buffer, so a rewind will
drag the character back across the level — through walls. Call:

```csharp
identity.ResetHistory();
```

right after moving it. The buffer is refilled with the current pose and the rewind depth
is reset.

---

## 7. Time Zones

A trigger collider that changes the **local** time scale of anything inside it.

| Effect | Behaviour |
|---|---|
| **Slow Motion** | Objects inside run at a fraction of normal speed. |
| **Stasis** | Objects inside are frozen at scale 0. |
| **Rewind** | Objects inside continuously run backwards through their own history, with no global trigger. |

Create with `Hub → Time Zones` — it makes the zone **and** its profile asset together.

### Profiles

Behaviour lives in a `TimeProfile` **asset**, not on the zone. Several zones can share one
profile, so tuning the profile retunes every zone at once.

> A zone with **no profile assigned does nothing at all**. Its gizmo turns red and says so.

### Filtering

The profile filters by **layer**, and optionally by **tag**. The two are combined with
**OR** — an object matches if either matches. To filter purely by tag, set the layer mask
to Nothing.

Layers are scarce (32 total, most reserved for the collision matrix), so tags are usually
the better choice for gameplay categories.

> **One caveat.** The scan catching objects *already standing inside* the zone at scene
> start is a physics query, and physics queries filter only by layer. A tag-only zone
> still catches anything that walks in, but not what was already there. Keeping the layer
> mask on Everything and narrowing by tag gives you both behaviours.

### Exit rules

| Rule | Meaning |
|---|---|
| On Leave Trigger | Lasts while inside; `Duration` is a grace period after leaving. |
| Timed | Fixed duration regardless of position. |
| Manual / External | Until your code releases it. |

### Gizmos

Zones draw themselves in the Scene view, colour-coded by effect — blue slow motion, pale
stasis, violet rewind, amber kinetic, green permission, **red for a missing profile** —
with a label showing the profile name and key parameters. Permission zones brighten while
active, so you can watch the gate open during Play.

---

## 8. Kinetic force

Two different features with confusingly similar names.

### Kinetic Accumulator

While time is stopped a Rigidbody cannot be pushed — any force applied is simply lost.
That ruins the set piece where you freeze time, attack something repeatedly, and watch
everything connect the instant time resumes.

`KineticAccumulator` sits on the **target** and remembers every impact received while
frozen — both scripted hits and real collisions.

| Release mode | Result |
|---|---|
| Combined Impulse | Everything lands as one enormous hit. |
| Sequential Hits | Stored hits fire one after another with a delay — reads as a rapid combo. |

`Max Force Cap` is the safety rail: without it a long freeze can launch objects out of the
level.

### Kinetic Zone

Completely different. A `TimeKineticZone` derives an object's time scale from **how fast
it is moving** — stand still and time crawls, sprint and it runs normally. The mechanic
from games where the world only moves when you do.

It has **no Time Profile**; the speed-to-time mapping is configured on the zone itself.

---

## 9. Time Echoes (ghosts)

A ghost that replays what an object just did. Add `TimeParadoxModule` and enable the
Echoes channel — or use the **Projectile** role, which does both.

### How it works

After a rewind, the module clones the source object, strips everything that must not run
on a ghost (Identities, cameras, foreign scripts), and replays the recorded path.

### Pooling

Spawning a ghost runs **five full hierarchy sweeps** and destroys every foreign component
on the clone. On a rigged character that costs far more than the `Instantiate` itself. A
retired ghost has already been through all of it, so `UsePooling` (default on) reuses it
and skips the whole stripping pass.

Turn pooling off if you suspect a reused ghost is carrying over state.

### Ghost layer

Set `GhostLayerName` to a layer you have **actually created**, then configure its collision
matrix in **Project Settings → Physics**. The engine never calls `Physics.IgnoreLayerCollision`
for you.

If the layer does not exist, ghosts fall back to **Default** and collide with everything
including the player. The engine warns about this in the Console and in the inspector.

### Pink ghosts

Magenta is Unity's error shader. The **Hologram Material** uses a shader that does not work
in your render pipeline — typically a Built-in RP shader in a URP or HDRP project. The
Console names the material and shader. Fix the shader, or clear the field to keep the
original look.

---

## 10. Your own variables

`TimeWarpBridge` rewinds float fields *inside your own scripts* — ammo, health, charge,
light intensity — without writing an adapter.

It resolves members by name using compiled expression trees (cached per type, so it is not
per-frame reflection).

| Operation | Meaning |
|---|---|
| Record Only | Value is recorded and replayed during rewind. |
| Math Only | Value is scaled by the time scale, not recorded. |
| Math And Record | Both. |

> **IL2CPP warning.** Because members are found by *name*, Managed Code Stripping cannot
> see them and may remove them from the build. See [Deployment](#16-deployment).

---

## 11. Interface / HUD

`GameObject → TimeWarp → UI → Rewind Bar` builds a working bar — canvas, background, fill
and component, wired up.

| Source | Shows |
|---|---|
| Action Time Remaining | How much of a duration-limited anomaly is left. Needs `Use Max Duration`. |
| Rewind Depth | How far into the past a tracked object currently is. |
| Available History | How much recorded history that object still has. |

Drives an `Image` (Filled), a `Slider`, and/or a `CanvasGroup`. Smoothing runs on
**unscaled** time deliberately, so the bar keeps animating during a time stop.

### Text labels

There is **no TextMeshPro dependency** by design. The formatted string is pushed through
the `On Label Changed` event — drag your `TMP_Text` (or a plain `UI.Text`) in and pick the
text setter. It fires only when the visible string changes, so it does not allocate every
frame.

`Label Format` understands `{0}` current seconds, `{1}` total seconds, `{2}` percent.

The UI module lives in `Modules/UI` with its own assembly definitions — a project without
uGUI can delete that folder entirely, runtime and editor together.

---

## 12. Post-processing

The engine **does not create effects**. It drives the *weight* of a Volume you author.

1. `Hub → Post-processing → Create Anomaly Volume` — makes a global Volume at weight 0 plus
   `TimeWarpVolumeController`.
2. Assign a Volume Profile and add the overrides that sell the effect in your art style:
   chromatic aberration, vignette, colour grading, lens distortion.
3. Leave the weight at 0. The controller owns it from then on.

The look stays entirely yours and works in URP and HDRP alike.

**Starting values**

| State | Weight | Why |
|---|---|---|
| Rewind | 1.0 | The player should be in no doubt time is running backwards. |
| Time Stop | ~0.8 | Slightly weaker so the frozen world stays readable. |
| Slow Motion | ~0.3–0.4 | Slow motion often lasts a long time; an aggressive effect becomes tiring. |

If the effect pops instead of blending, raise the manager's entry and exit durations.

---

## 12b. Force field shader

`TimeWarp/Force Field` is a URP shader that obeys the engine, plus the
`TimeWarpForceField` component that drives it.

### Why it needs a script

A shader has no idea the engine exists. Animate a surface on Unity's built-in `_Time` and
it keeps scrolling at full speed while everything around it hangs frozen in mid-air — the
illusion dies instantly.

So the component keeps **its own clock** and hands it to the shader as `_LocalTime`. The
clock advances by the object's effective time scale, and during a rewind it runs
**backwards** by the exact amount of history being consumed — derived from the change in
rewind depth, not from a guessed speed, so it stays locked to the playback however the
rewind accelerates.

Everything the shader animates then follows the engine for free:

| Engine state | Field behaviour |
|---|---|
| Normal | Pattern scrolls normally |
| Slow motion | Pattern crawls |
| Time stop | Pattern freezes |
| Rewind | Pattern un-scrolls, colour shifts to Rewind Color |

### Impacts

Objects hitting the field send an expanding ring out from the point of contact plus a
short flash. Each hit is stored with the local time it happened at, so when time runs
backwards the ripples **shrink back into the point of contact and vanish** — the impact
literally un-happens. That needed no extra code: a hit whose timestamp is in the future
simply is not drawn.

| Setting | Purpose |
|---|---|
| Impact Layers / Impact Tags | What can make the field ripple. Layer OR tag, as everywhere else in the engine. |
| React To Triggers | Ripple when something passes *through*, not just on solid collisions. |
| Impact Cooldown | Stops a body resting against the field machine-gunning ripples every frame. |
| Min / Full Impact Speed | Ignore gentle touches; clamp fast ones. |

Four simultaneous impacts are supported (`TW_MAX_HITS` in the shader, `MaxHits` in the
script — keep them equal if you change it).

For scripted hits — a bullet destroyed before it can collide, for example:

```csharp
forceField.RegisterImpact(hitPoint);
forceField.RegisterImpact(source, hitPoint, speed);
forceField.ClearImpacts();
```

### Setup

1. Put the shader on a material, assign it to a sphere or dome mesh.
2. Add `TimeWarpForceField` to that object.
3. Add a `TimeWarpIdentity` if you want it to obey the engine — without one it falls back
   to unscaled real time and still works, just without any time reaction.
4. Give the object a trigger collider if you want it to react to things passing through.

The shader is **two-sided and depth-fading**: it stays visible when the camera is inside
the bubble, and glows where the surface cuts through the floor. Both are deliberate — a
force field that disappears when you step into it is the most common way this effect
fails.


---

## 13. Performance

### Architecture

Transforms are processed with **Burst-compiled jobs** over a `TransformAccessArray`, and
history lives in native circular buffers indexed by bitmask. The main-thread cost per
object is small; the scaling limit is usually the number of recorded objects.

### Profiler markers

The engine reports its own timings under the **`TimeWarp.`** prefix:

`PreCalculateState (Fixed)` · `PreCalculateState (Update)` · `RecordFrame` ·
`ApplyRewindFrame` · `TickFixedUpdate` · `TickUpdate` · `WaitForTransformJob` ·
`ProcessDisposalQueue` · `Particles.Capture` · `Particles.Restore` · `Ghost.Spawn`

> **`WaitForTransformJob` is the diagnostic one.** It measures the blocking wait for the
> transform job. High values mean the job could not finish in time — you have more recorded
> objects than it can process, which is a different problem from slow user code.

### Where memory goes

| Cost | Notes |
|---|---|
| Transform history | ~40 bytes per frame per object. Unavoidable baseline. |
| Velocity history | Allocated lazily — objects that never use the channel allocate nothing. |
| Particle cache | By far the largest. Governed by the RAM budget below. |

### Particle RAM budget

A hard ceiling. When a particle system asks to be cached and the budget is spent, it is
refused and falls back to **procedural** playback — simulated backwards approximately
instead of replayed exactly. Cheaper, less accurate, and it can never blow your memory
limit.

Raise it if smoke and sparks look wrong during a rewind. Lower it on mobile. The cheapest
fix is usually turning the Particles channel off on objects that do not need it.

### Reducing cost

1. Turn off channels an object does not need — especially Particles.
2. Lower Max Record Time globally, or set Record Time Override per object.
3. Watch the buffer rounding warning; sometimes you can raise Max Record Time for free.
4. Enable ghost pooling (on by default).

---

## 14. Scripting API

### Triggering anomalies

```csharp
var mgr = TimeWarpManager.Instance;

mgr.TriggerAction(GlobalAction.Rewind);
mgr.TriggerAction(GlobalAction.SlowMotion, targetScale: 0.2f);
mgr.TriggerAction(GlobalAction.TimeStop, duration: 10f);
mgr.TriggerAction(GlobalAction.Rewind, delay: 0.5f);

mgr.StopGlobalAction();
```

`duration`, `delay` and `targetScale` default to `-1`, meaning "use the inspector value".

### Reading state

```csharp
TimeState state       = mgr.GlobalTimeState;       // Normal, PreAction, Rewinding, TimeStop, SlowMotion, PostAction
float globalScale     = mgr.CurrentGlobalTimeScale;
bool active           = mgr.IsActionActive;

float progress01      = mgr.ActionProgress01;      // 0..1, or 0 when unlimited
float remaining       = mgr.ActionTimeRemaining;   // seconds, or -1 when unlimited
float maxHistory      = mgr.MaxRecordSeconds;
```

### Per object

```csharp
var id = GetComponent<TimeWarpIdentity>();

bool rewinding   = id.IsRewinding;
float scale      = id.EffectiveTimeScale;   // Local × Global
float depth      = id.FractionalOffset;     // frames into the past

id.LocalTimeScale = 0.5f;   // zones write this at runtime

id.ResetHistory();          // after teleport / respawn / load
id.RefreshModules();        // after adding or removing modules at runtime
id.ResetModuleFaults();     // re-enable modules disabled by the fault breaker
```

### Events

Manager: `OnGlobalRewindStart`, `OnGlobalRewindStop`, `OnGlobalTimeStop`,
`OnGlobalSlowMoStart`.

Identity: `OnLocalRewindStart/Stop`, `OnLocalTimeStopStart/Stop`,
`OnLocalSlowMoStart/Stop`, `OnTimeScaleChanged`.

Everything hooked here runs on unscaled time, so it works during a time stop.

### Writing a module

```csharp
using TimeWarp.Core;

[RequireComponent(typeof(TimeWarpIdentity))]
public class MyModule : MonoBehaviour, ITimeWarpModule
{
    private TimeWarpIdentity _identity;
    private void Awake() => _identity = GetComponent<TimeWarpIdentity>();

    public void OnWarpFixedUpdate() { /* record here */ }
    public void OnWarpUpdate()      { /* apply here */ }
}
```

The Identity finds and ticks it automatically. Exceptions are caught and counted.

---

## 15. Troubleshooting

Run **`Tools → TimeWarp → Validate Scene`** first — it catches most of these automatically.

### Nothing rewinds at all
Is there a manager in the scene? Does the object have an Identity? Are **any channels
ticked** — an Identity with everything off records nothing. Then check the Console: a
module disabled by the fault breaker says so by name.

### The character jitters or will not move
Your control script is fighting the playback. Add `TimeWarpComponentManager` and put that
script in Target Components. See [Characters](#6-characters).

### Rewind drags the object back through walls
The buffer still holds the route from before a teleport. Call `identity.ResetHistory()`
after any non-continuous move.

### Ghosts are bright pink
The Hologram Material's shader does not work in your render pipeline. The Console names it.

### Ghosts collide with the player
The ghost layer does not exist, or its collision matrix is not configured. See
[Time Echoes](#9-time-echoes-ghosts).

### A zone does nothing
No profile assigned (the gizmo is red), the collider is not set to **Is Trigger**, or the
filter matches nothing — layer mask on Nothing with no tags.

### Memory use is too high
See [Performance](#13-performance). Particles first.

### It works in the editor but not in a build
Almost always IL2CPP stripping affecting `TimeWarpBridge`. See below.

---

## 16. Deployment

### IL2CPP and Managed Code Stripping

**Affects:** anyone shipping IL2CPP (all consoles, iOS, WebGL, and Windows/Android when the
backend is IL2CPP) **and** using `TimeWarpBridge`.

The Bridge resolves members by **string name**, which the static analyzer cannot see. On
Medium or High stripping it may remove your field entirely. The result appears **only in
the final build, never in the editor**: the Bridge silently finds nothing.

This is inherent to name-based lookup and applies to *your* scripts, which the engine
cannot annotate for you.

**Fix A — attribute on the member (preferred):**

```csharp
using UnityEngine.Scripting;

public class MyWeapon : MonoBehaviour
{
    [Preserve] private float _fireRate = 0.2f;
}
```

**Fix B — `link.xml`** for many bound members:

```xml
<linker>
  <assembly fullname="Assembly-CSharp">
    <type fullname="MyGame.MyWeapon" preserve="all"/>
  </assembly>
</linker>
```

**Verify with a development IL2CPP build**, not in the editor — the editor uses Mono and
never strips.

### Input configuration

The engine core reads **no input at all**, so it works in every configuration. Only the
examples care.

| Script | Input support |
|---|---|
| `Control System/RewindTester.cs` | **Both** — legacy Input Manager and the Input System package |
| `Demo/Scripts/CharacterControler.cs` | Legacy only |
| `Demo/Scripts/PlayerWeapon.cs` | Legacy only |

`RewindTester` — the one you are most likely to copy — uses Unity's built-in
`ENABLE_INPUT_SYSTEM` / `ENABLE_LEGACY_INPUT_MANAGER` defines and needs no project changes.

If **Active Input Handling** is set to *Input System Package (New)*, the demo scene will
not respond. Either set it to **Both**, or ignore the demo and drive the engine from your
own code — the API is one line.

### Editor setting worth enabling

**Project Settings → Editor → Enter Play Mode Options: enabled, Reload Domain: disabled**

Makes entering Play mode near-instant on large projects. The engine is explicitly safe
under this setting: every static field it owns is reset via
`[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`, and it never writes global engine
state.

### Assembly definitions

| Assembly | Contents |
|---|---|
| `TimeWarp.Runtime` | Core and modules. `autoReferenced`, so your scripts need no setup. |
| `TimeWarp.Editor` | Editor tooling. Editor-only, never enters a build. |
| `TimeWarp.Controls` | Example input. Delete its asmdef if you lack the Input System package. |
| `TimeWarp.UI` / `TimeWarp.UI.Editor` | HUD. Delete `Modules/UI` entirely if you do not use uGUI. |
| `TimeWarp.Demo` | Demo scene scripts. |
| `TimeWarp.Tests.EditMode` | Unit tests for the buffer maths. |

Required packages: **Burst**, **Collections**, **Mathematics**, **Cinemachine**,
**VFX Graph**, **Core RP Library**. If a reference fails to resolve, Unity names the
assembly — pick the correct entry in the `.asmdef` inspector.
