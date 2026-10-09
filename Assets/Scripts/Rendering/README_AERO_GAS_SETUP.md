# Safe AERO localized gas test setup

The gameplay link is opt-in. `GasSprayer` publishes potion tint and the player's transform
through `ProjectName.Core.AeroGasSprayBridge`; Core has no Systems/Rendering dependency.
`AeroIsolatedRendererSession` is the only runtime switch that enables the compositor and adds
the Rendering-side subscriber. Without that session, the legacy `GasSprayer_Fog` visuals and
all gameplay effects continue as before.

Safe test route (do not use the shared project renderer):

1. In Unity, run `Tools > AERO > Phase 4B > Create Isolated Test Renderer`. This clones the
   currently selected URP pipeline and its first renderer into `Assets/Settings/AEROPhase4B/`
   and installs `AeroLocalizedGasRendererFeature` only on that cloned renderer. The menu does
   not modify Graphics Settings, Quality Settings, the source renderer, or AERO assets.
2. Use a dedicated test scene (for example `Test_10`) and a scene-local GameObject with
   `AeroIsolatedRendererSession`. Assign that scene's `AERO_Test_Pipeline` to its
   `Isolated Pipeline` field. The session temporarily overrides `QualitySettings.renderPipeline`
   while enabled and restores its prior value on disable/destroy.
3. Put the session on the player (or a player-root host) so it can attach
   `AeroGasSprayRuntimeHost` there. Press G while a supported gas potion is loaded: the local
   field follows the player's transform and takes the same tint as the legacy potion fog. G off
   or an empty/invalid potion fades the emitter; disabling the session/host removes it immediately.
4. Verify in the dedicated test scene only. Do not assign `AERO_Test_Pipeline` in Project
   Graphics/Quality settings and do not add the feature to the shared renderer.

The session additionally refuses activation (leaving legacy visuals active) unless the assigned
pipeline has an active `AeroLocalizedGasRendererFeature` with its shader assigned. The feature
also checks `AeroGasSprayBridge.IsolatedRendererEnabled` before recording its local composite,
so it does not draw outside an active session. Asset generation, renderer activation, and visuals
have not been Unity Play-verified in this change. In particular, the
menu-generated clone is created only when run in the Editor; no AERO pipeline/renderer asset is
checked in or assigned to the normal scene.

The compositor and gameplay bridge are project-owned. They do not alter shared AERO materials,
global fog state, or vendor files.
