# Phase 4B — isolated AERO local gas renderer

This addition is deliberately opt-in and does not change project scenes, existing gameplay scripts, AERO assets, or the globally assigned URP pipeline.

## Architecture

- `AeroLocalizedGasVolume` is a lightweight per-emitter data/lifecycle component. It follows a world transform, holds radius/height/density/tint/plume parameters, fades on stop, and provides an executable world-space density reference.
- `AeroLocalizedGasRendererFeature` is a custom local full-screen compositor implemented in this project, not AERO's vendor fog implementation. It resolves the nearest active emitter independently for each camera, uses per-camera runtime materials, samples scene depth/world positions, and limits density to a local height/radius mask plus directional plume. It does not modify AERO's shared fog shader/material or global fog settings.
- `AeroIsolatedRendererSession` temporarily assigns a separately cloned URP asset through `QualitySettings.renderPipeline` at runtime and restores that quality-level override on teardown; it does not write `GraphicsSettings.defaultRenderPipeline`.
- `AeroPhase4BIsolatedSetup` (Unity menu `Tools > AERO > Phase 4B > Create Isolated Test Renderer`) clones the active renderer/pipeline into `Assets/Settings/AEROPhase4B`, preserving existing renderer features in the clone and adding this feature. It refuses to overwrite generated files. Assign the cloned pipeline to an `AeroIsolatedRendererSession` on a test-only scene object.
- Shader `AeroLocalizedGas.shader` performs this project's depth-reconstructed local-space composite. Vendor AERO fog remains separate and unchanged; the renderer package files also remain unchanged.

## Scope/known integration boundary

This is the isolated renderer bridge/spike, not an activation of the G-key gas gameplay path. Existing gas scripts are intentionally untouched per task constraints; gameplay can opt in with `AeroLocalizedGasVolume.Attach` or `IAeroLocalizedGasBridge` after a separate approved integration. No scene receives the cloned pipeline automatically. The renderer needs the assigned local gas shader and should be validated in Test_10 Play before promoting it anywhere.

## Tests

`Tests/EditMode/AeroLocalizedGasPhase4BTests.cs` covers parameter clamping, emitter tracking, enable/fade selection, local bounds/directional plume, camera-nearest isolation, and interface wiring. This test folder has its own Editor-only assembly definition, so the existing test assembly and other user's concurrent edits were not touched.

## Setup

1. Let Unity compile, then run the menu item above.
2. Add `AeroIsolatedRendererSession` to a test-only Test_10 object and assign `AERO_Test_Pipeline`.
3. Add/configure `AeroLocalizedGasVolume` on the isolated test emitter and drive `SetEmitting(true/false)`.
4. Validate Test_10 visuals, camera stacking, and runtime logs before any MainScene adoption.
