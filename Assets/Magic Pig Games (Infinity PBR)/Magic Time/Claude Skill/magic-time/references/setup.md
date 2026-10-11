# Setting up the time system (the build how-to)

This is the how-to for getting Magic Time running in a scene and building its
assets for real. Two parts make a working setup:

1. **One `MagicTimeManager`** in the scene (a singleton; clones + serves named
   scales and ticks every live scale).
2. **`LocalTimeScale` assets** — at minimum a `Global` scale; usually a few named
   scales (`SlowMo`, `Frozen`, …) listed as the manager's *initial scales* so they
   exist from the start and can be fetched by name.

Then objects become `MagicTimeUser`s (see `magic-time-users.md`) and your code or
zones drive the scale values.

> The "Magic Time User" inspector and the manager inspector show live subscriber/
> value info in play mode — useful for the user to confirm your setup visually.

---

## Running editor C# through the unity-mcp bridge

Assets and scene wiring are created by executing editor C# inside Unity via
`mcp__unity-mcp__Unity_RunCommand`. The `Code` must follow Unity MCP's **Golden
Template** exactly — a class named `CommandScript`:

```csharp
using UnityEngine;
using UnityEditor;
using MagicPigGames.MagicTime;

internal class CommandScript : IRunCommand   // MUST be internal, MUST be named CommandScript
{
    public void Execute(ExecutionResult result)
    {
        // ... Magic Time setup here ...
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        result.Log("done");
    }
}
```

Rules: the class **must** be `internal` and named exactly `CommandScript`; it
**must** implement `IRunCommand` with `public void Execute(ExecutionResult
result)`. Use `result.Log(...)` / `result.LogError(...)` for output, and
`result.RegisterObjectCreation(obj)` / `result.RegisterObjectModification(obj)`
when you create/modify Unity objects you want tracked for undo. Always finish with
`AssetDatabase.SaveAssets(); AssetDatabase.Refresh();`.

**Sanity-check the bridge first** with a trivial command before real work:

```csharp
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
        => result.Log($"Magic Time present: {typeof(MagicTimeManager).FullName}");
}
```

If `Unity_RunCommand` errors or times out, the bridge isn't running — switch to
the **no-bridge fallback** at the bottom of this file.

---

## Recipe A — create a Local Time Scale asset

`LocalTimeScale` is a `ScriptableObject`. Create it, set its starting `value`
(serialized field — note the runtime `Value` setter is private, so set the
serialized `value` field at author time), and save.

```csharp
using System.IO;
using UnityEngine;
using UnityEditor;
using MagicPigGames.MagicTime;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        const string folder = "Assets/_MagicTime/Scales";
        Directory.CreateDirectory(folder);

        var scale = ScriptableObject.CreateInstance<LocalTimeScale>();
        var path  = AssetDatabase.GenerateUniqueAssetPath($"{folder}/Global.asset");
        AssetDatabase.CreateAsset(scale, path);

        // The runtime Value setter is private; set the serialized default field instead.
        var so = new SerializedObject(scale);
        so.FindProperty("value").floatValue = 1f;            // 1 = normal
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(scale);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        result.Log($"Created LocalTimeScale at {path}");
    }
}
```

Repeat for any named scales you need (e.g. `SlowMo` at `0.3`, `Frozen` at `0`).
The asset **name** is the key Users and code look up by — keep names clean and
unique (trailing spaces are trimmed with a warning).

---

## Recipe B — add the Magic Time Manager and list its initial scales

Find-or-create one manager GameObject, then point its **private serialized**
`initialTimeScales` array at the scale assets so they're cloned and served by name
at play time.

```csharp
using UnityEngine;
using UnityEditor;
using MagicPigGames.MagicTime;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var manager = Object.FindAnyObjectByType<MagicTimeManager>();
        if (manager == null)
        {
            var go = new GameObject("Magic Time Manager");
            manager = go.AddComponent<MagicTimeManager>();
            Undo.RegisterCreatedObjectUndo(go, "Create Magic Time Manager");
            result.Log("Created Magic Time Manager");
        }

        var global = AssetDatabase.LoadAssetAtPath<LocalTimeScale>("Assets/_MagicTime/Scales/Global.asset");

        // initialTimeScales is a private [SerializeField] array — write it via SerializedObject.
        var so   = new SerializedObject(manager);
        var prop = so.FindProperty("initialTimeScales");
        prop.arraySize = 1;
        prop.GetArrayElementAtIndex(0).objectReferenceValue = global;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(manager);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        result.Log("Manager initial scales set");
    }
}
```

After this, at play time `MagicTimeManager.Instance.TimeScale("Global")` returns a
live clone, and any `MagicTimeUser` whose `initialTimeScales` lists `Global`
auto-subscribes to it.

---

## Recipe C — drive a scale value at runtime (from your own code)

This is runtime C# (no bridge). Grab a named scale from the manager and change it.
Never assign `Value`; use `SetValue` / `Pause` / `Resume`.

```csharp
using UnityEngine;
using MagicPigGames.MagicTime;

public class SlowMoToggle : MonoBehaviour
{
    void Update()
    {
        var slow = MagicTimeManager.Instance.TimeScale("Global");  // creates it if missing
        if (Input.GetKeyDown(KeyCode.B)) slow.SetValue(0.2f);      // bullet time
        if (Input.GetKeyUp(KeyCode.B))   slow.SetValue(1f);        // back to normal
    }
}
```

For a single object instead of a shared scale, set its own scale:
`myUser.LocalTimeScaleValue = 0.2f;` (or `myUser.LocalTimeScale.Pause();`).

> **Don't call `SetValue` every frame while a User is mid-transition** — it
> restarts the transition curve and the value can stall near 0. If you're driving
> from a slider/continuous source, set the affected Users' `transitionDuration = 0`
> so changes apply immediately. (This is the documented FAQ trap.)

---

## The bundled helper — `MagicTimeAgentTools`

`scripts/MagicTimeAgentTools.cs.txt` wraps all of the above (create scale, ensure
manager, make a User, add a specialized User, create a time zone, set a serialized
field, list project scales, verify). It's **optional** — the inline recipes do the
same — but it makes multi-step builds terse.

**Install it once:** copy the file to `Assets/Editor/MagicTimeAgentTools.cs` (drop
the `.txt`), let Unity recompile. You can write it with your normal file tool then
run an `AssetDatabase.Refresh()` command, or write+refresh from one CommandScript.
After it compiles, any CommandScript can call it:

```csharp
using UnityEngine;
using UnityEditor;
using MagicPigGames.MagicTime;
using MagicPigGames.MagicTime.AgentTools;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var global = MagicTimeAgentTools.CreateLocalTimeScale("Assets/_MagicTime/Scales", "Global", 1f);
        var slow   = MagicTimeAgentTools.CreateLocalTimeScale("Assets/_MagicTime/Scales", "SlowMo", 0.3f);
        MagicTimeAgentTools.EnsureManager(global, slow);    // adds the manager + lists both as initial scales
        MagicTimeAgentTools.Verify();                       // logs what it found
        result.Log("Magic Time set up");
    }
}
```

---

## Verify, then report

After any build, confirm rather than assume:

- Read it back: `Object.FindAnyObjectByType<MagicTimeManager>() != null`, the
  scale assets load, the manager's `initialTimeScales` is populated. The helper's
  `Verify()` logs all of this.
- Check the console: `mcp__unity-mcp__Unity_GetConsoleLogs` with
  `logTypes: "Error"` (a duplicate-name or trailing-space warning from the manager
  is benign).
- Report what exists and how to drive it (which named scales, how to set values).
  The user runs the actual in-editor playtest — visual confirmation is their job.

---

## No-bridge fallback

If the unity-mcp bridge isn't available, **write an editor script** into the
project that does the same work behind a menu item the user clicks:

```csharp
#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEditor;
using MagicPigGames.MagicTime;

public static class MagicTimeSetupMenu
{
    [MenuItem("Tools/Magic Time/Set Up Time System")]
    public static void SetUp()
    {
        Directory.CreateDirectory("Assets/_MagicTime/Scales");
        var scale = ScriptableObject.CreateInstance<LocalTimeScale>();
        AssetDatabase.CreateAsset(scale, "Assets/_MagicTime/Scales/Global.asset");

        var manager = Object.FindAnyObjectByType<MagicTimeManager>()
                      ?? new GameObject("Magic Time Manager").AddComponent<MagicTimeManager>();
        var so = new SerializedObject(manager);
        var prop = so.FindProperty("initialTimeScales");
        prop.arraySize = 1;
        prop.GetArrayElementAtIndex(0).objectReferenceValue = scale;
        so.ApplyModifiedProperties();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Magic Time] Set up. Press Tools > Magic Time > Set Up Time System to re-run.");
    }
}
#endif
```

Tell the user the menu path. **Never hand-write `.asset` YAML** for a
`LocalTimeScale` unless explicitly asked — `CreateInstance` + `CreateAsset` is the
robust path; raw YAML invites GUID/format breakage.
