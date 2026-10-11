# Magic Time — AI Agent Skill

This folder bundles a **Claude Code / Cowork Skill** that teaches AI coding
agents how to use, set up, and extend Magic Time (Infinity PBR) — its per-object
"local" time scales, the Magic Time Manager, Magic Time Users, time zones, and
the runtime API for writing custom code that respects local time.

Once installed, you can ask your agent things like:

> "Set up Magic Time in this scene with a Global time scale."
> "Make this enemy move in slow motion while everything else runs normally."
> "Create a slow-mo bubble zone that doesn't affect the player."
> "Make my custom movement script obey Magic Time."
> "Add a bullet-time toggle that ramps the world to 0.2× and back."

…and it will know the Magic Time architecture, add the manager and time-scale
assets for real, make your objects (and your own scripts) into Magic Time Users,
build time zones, and write the custom C# that drives motion off local time
instead of `Time.deltaTime`.

## What's in here

```
Claude Skill/
├── README.md               ← you are here
├── install.sh              ← copies the skill into your project's .claude/skills/
└── magic-time/             ← the skill itself
    ├── SKILL.md            ← the entry point the agent reads
    ├── references/         ← deep-dive docs (architecture, setup, users, time zones, custom code, integrations)
    └── scripts/            ← optional editor helper (MagicTimeAgentTools)
```

The skill is plain Markdown. **Claude Code and Cowork** discover it
automatically once it lives in a `.claude/skills/` folder. **Other AI tools**
(Cursor, Copilot, etc.) can be pointed at `magic-time/SKILL.md` and the files in
`magic-time/references/` directly — it's all readable docs.

## Install

From a terminal **inside your Unity project**:

```bash
bash "Assets/Magic Pig Games (Infinity PBR)/Magic Time/Claude Skill/install.sh"
```

That copies `magic-time/` into `<your project>/.claude/skills/` so Claude Code
picks it up for this project. To install it for **every** project on your
machine instead, add `--global` (installs to `~/.claude/skills/`):

```bash
bash ".../Claude Skill/install.sh" --global
```

Manual install (any OS): copy the `magic-time/` folder into either
`<your project>/.claude/skills/` or `~/.claude/skills/`.

## Verify it's working

Start your agent in the project and ask: *"What can you do with Magic Time?"* If
the skill is installed, it will describe setting up the Magic Time Manager,
creating local time scales, making objects (and your scripts) Magic Time Users,
building time zones, and writing custom code that runs on local time.

## Best results: connect the Unity MCP

The skill can build Magic Time assets and wire scene objects directly while the
Unity Editor is open by running editor C# through the **Unity MCP bridge**. It
also has a no-bridge fallback (it writes a small editor script you run from a
menu), so the skill still works without it — the MCP just makes the loop instant.
Writing custom C# (making your scripts respect local time) needs no bridge at
all. See `magic-time/references/setup.md` for setup.
