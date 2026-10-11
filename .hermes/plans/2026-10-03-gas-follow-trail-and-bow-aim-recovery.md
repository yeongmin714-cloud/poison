# G-held Player-Following Gas Trail + Bow Aim Recovery Plan

> **상태: 계획 전용.** 이번 요청은 심층 계획 재작성이다. 이 문서를 만드는 동안 production code, tests, scenes, textures, renderer assets, QA handoff, ROADMAP을 수정하지 않는다. 구현 승인으로 간주하지 않는다.
>
> Project: `/mnt/c/Unity/code` · Unity `6000.4.10f1` · URP 17.x
> Latest supplied test asset found: `Screenshots/가스 분사 테스트 3.mp4` (1240×828, 30fps, 31.467s; modified 2026-10-03 20:55 KST). The directory also contains `가스 분사 테스트.mp4` (50.005s) and `가스 살포 예시.mp4` (4.10s). The latest filename is “3”, not “2”; treat it as the newest supplied capture without assuming the user's numbering meant a different file.
> Latest bow capture currently found: `Screenshots/화살 모션 테스트.mp4` (1240×828, 30fps, 36.48s; modified 17:39 KST, before the bow source edits recorded later that evening).

## 0. Accountability / acceptance reset

Earlier work was reported as if cursor sample sharing and a three-layer particle change had meaningfully solved what the user sees. That was not supported: code-level focused tests do not prove rendered reticle alignment or VFX quality, and the prior gas video predated the plume changes. The user has now supplied a newer capture and reports both problems are still visible. This plan treats both as **unresolved** and makes actual Play imagery the acceptance authority.

The high-level gas contract is now explicit:

- While G is held and the sprayer is active, the **emitter/nozzle follows the player** and continuously emits toward the trail side. The player may translate, turn, stop, and restart while holding G.
- Newly emitted gas remains at its **world-space path positions** after the player moves away; it must not be a single cloud parented to/following the player. Old trail portions broaden/soften/fade; new portions remain denser. Releasing G stops new emission and lets already-emitted gas fade.
- Use the reference footage for visible qualities, not an inferred champion/game identity or guessed world dimensions.

The bow contract is similarly concrete: the visible reticle's actual rendered center for a specific release event, its camera ray/selected aim point, the actual bow muzzle, the projectile's initial velocity, and the visible projectile axis must be measured together. Gravity after launch is assessed separately.

## 1. Re-inspected evidence and confirmed code facts

### 1.1 New gas video

Direct metadata/frame inspection of `Screenshots/가스 분사 테스트 3.mp4` establishes a 31.467s, 30fps capture. Sampled frames show a very conspicuous orange, block-edged/stepped patch at/near the player at some times and a broad diagonal green/dark region elsewhere. Across the contact sheet, these do not read as the reference's delicate, irregular, semi-transparent gas puffs. The sampled frames do **not** establish which runtime system owns the orange patch or whether the broad diagonal region is gas, lighting/shadow, or terrain. The exact emitter attribution is therefore a Phase 0 requirement, not a premise.

The reference (`가스 살포 예시.mp4`) shows a moving character and an irregular, overlapping green cloud trail. Puffs look softer and more organic than the test's orange blocks; the older trail remains visible behind the character while the newest activity is near the character. Do not infer exact world scale, particle count, shader architecture, stationary accumulation rules, or a precise seconds-long lifetime from screen pixels alone.

### 1.2 Gas code: multiple owners and a strong wiring risk

Verified current source paths:

1. `GasSprayer.Update()` (`Assets/Scripts/Systems/GasSprayer.cs`) polls `Keyboard.current.gKey.isPressed`, passes it to `GasSprayerController.SetGInputHeld()`, runs gameplay effects at `_effectInterval`, and calls `EmitPlume()` each active frame.
2. `GasSprayer` creates three child particle systems with a Cone shape, World simulation, `GasPlumeSoft/Lobe/Wisp` masks, rates 15/7/5, and a fixed local child offset `(0, 0.3, 0.25)`. The rotation is identity; the actual +Z cone direction/nozzle relationship to the player's model and movement is not runtime-proven. Current `EmitPlume()` stops visual output when `ClassifyPotion(LoadedPotionId)` yields `None` (transparent tint). Test_10 gives the player test potions but `SetupGasVerifyScene()` does not itself load one into the canister. Thus “controller says spraying” does not prove continuous plume particles are being emitted.
3. `GasSprayerController.SetGInputHeld()` is hold-to-spray and tracks whether the G hold started that spray. `SprayInputHandler.Update()` is a second start/stop owner for Mouse1 and calls the same public `StartSpray()/StopSpray()` methods. This may be a supported secondary control, but it is not yet demonstrated harmless. All input paths and held-state ownership must be audited before choosing a single visual owner.
4. `GasSprayerController.StartSpray()` calls `SpecialEffectsController.OnGasSprayStart()` once when that singleton exists. `SpecialEffectsController.OnGasSprayStart()` spawns a detached `PoisonFog` at `player.position + player.forward * 1.5m`; it builds a one-shot sphere burst with an internally generated 16×16 texture, additive material, and expanding particles. That effect does not follow the player and is a second visual producer separate from the continuous `GasSprayer` pool. It is a concrete competing-path candidate, not yet proven to be the orange patch in the newest video.
5. `GasCloudLauncher → GasCloudField.Spawn()` is a third, separate potion-deployment effect. `GasCloudField` intentionally remains fixed at its spawn point and expands radially. Do not conflate it with G-held continuous spray.
6. `GasSprayer.EmitPlume()` also publishes `AeroGasSprayBridge`; `AeroGasSprayRuntimeHost` consumes that only when an `AeroIsolatedRendererSession` and configured isolated URP pipeline are active. `AeroIsolatedRendererSession` explicitly rejects missing/mismatched cloned pipeline and retains legacy visuals. No evidence yet says this optional volume rendered in the latest capture; active render path must be logged/inspected.
7. The Test_10 setup (`TestTerritoryCombatSetup.SetupGasVerifyScene`) dynamically adds Controller then GasSprayer, equips Wood, and gives test items. It does not add the optional AERO host/session and does not preload a potion. Scene/runtime wiring and order remain part of the reproduction check.

### 1.2.1 Test_10 vs MainScene — confirmed asymmetries that can change what is visible

The user is testing in **Test_10**, not MainScene. The plan must not treat Test_10 as a neutral copy of production startup.

| Concern | MainScene startup | Test_10 startup | Consequence to test explicitly |
|---|---|---|---|
| Gas component wiring | `GameSetup.Start()` runs `SetupPlayerComponents()` and later `EnsureGasSystem()`: Controller, GasSprayer, `SprayInputHandler`, `GasSprayUTK`, cloud hook, mask bridge/controller. | `TestTerritoryCombatSetup.Awake()` creates its own minimal player, then `SetupGasVerifyScene()` adds Controller, GasSprayer, mask controller, cloud hook/mask bridge and equips Wood. It does **not** add `SprayInputHandler` or call `GasSprayUTK.Ensure()`. | G's direct polling belongs to `GasSprayer.Update()` and may work in both, but secondary Mouse1 input and gas HUD/status path differ. Verify which input actually generated the newest capture and whether reticle/overlay UI consumed focus.
| Potion state | MainScene allows canister load through normal inventory/quickslot/UI; actual load is player state, not guaranteed simply by giving an inventory item. | Test_10 gives `Poison_TestPotion`/`Heal_TestPotion` to inventory but the inspected setup does **not** call `GasPotionLoader.LoadPotion()` or `controller.LoadPotion()`. | This is a strong, source-grounded candidate: `GasSprayer.EmitPlume()` returns/stops for `PotionType.None`, while the G start calls the legacy burst. The test may therefore show a detached legacy puff without the continuous three-layer trail. Confirm live `LoadedPotionId`, tint, emitting/alive counts in Test_10 and MainScene before attributing the orange area.
| Legacy one-shot fog | MainScene serializes `SpecialEffectsController` in its scene. | Test_10 scene does not serialize that controller in its setup. But `GasSprayerController.StartSpray()` accesses `SpecialEffectsController.Instance`; the singleton getter lazily creates one, and `StartSpray()` calls `OnGasSprayStart()`, which emits a detached `PoisonFog` at player + forward×1.5. | MainScene and Test_10 may differ in initialization timing/player reference, but the old one-shot path is not necessarily absent in Test_10. Correlate its one-shot spawn ID/material/tint with visible orange pixels. Do not conclude it is the owner until the isolated A/B run proves it.
| Player/camera/runtime presentation | Uses serialized scene Player + MainCamera and GameSetup configuration. | Player and camera are constructed/configured dynamically by `TestTerritoryCombatSetup`; the test setup also attaches `TestPlayerAnimatorBoot`. | Bow sample, panel root and camera ray can differ even if `PlayerCombat`/`ArrowManager` scripts are shared. The launch investigation must run first in the user's exact Test_10 view, then cross-check MainScene; a MainScene-only pass is insufficient.

**Diagnostic rule:** first reproduce the exact latest capture in Test_10 with its current no-dose state and input path. Then perform one-variable comparisons: load poison dose only; disable only legacy `SpecialEffectsController.OnGasSprayStart` burst in an isolated test harness; add the missing MainScene UI/secondary-input components only if needed. Keep each run labeled; no bundled changes. The key expected distinction is `Controller.IsSpraying=true` versus continuous visual emission actually active (`LoadedPotionId`, classified tint, `EmitPlume`, particle `isEmitting/alive`, legacy burst spawn).

### 1.3 Bow code: code path is not visual proof; a presentation hole is visible in source

1. UI `BowAimReticleUTK.UpdatePosition()` reads `ArrowManager.GetAimScreenPoint()`, stores it in `BowAimState`, converts it using `RuntimePanelUtils.ScreenToPanel()` then `root.WorldToLocal()`, and sets absolute left/top. `PanelSettings.asset` is Scale With Screen Size (reference 1440×900, width matching). No live rendered-center-to-screen round-trip has been measured.
2. `PlayerCombat.TryBowShot()` consumes the last UI sample, re-resolves `Camera.main`, calls `ArrowManager.TrySolveAim()`, and spawns from an explicit muzzle if available or fallback origin. `TrySolveAim()` chooses the first non-shooter physics hit or `ray.GetPoint(100m)`, then aims from muzzle to that world point. The actual selection semantics and 100m fallback have not been validated against the user's visible aiming guide.
3. `ArrowProjectile.Spawn()` sets Rigidbody velocity from the normalized direction and aligns local +Y at spawn. `Update()` then adds gravity and realigns to velocity. These are distinct stages and must be measured.
4. Confirmed source presentation concern: `ArrowProjectile.Spawn()` disables the root primitive `MeshRenderer`; a GLB mounting helper exists, but the inspected source search did not find a call to `MountArrowModel()`. The short TrailRenderer is therefore potentially the only visible flight cue. This can make perceived direction ambiguous, but is **not yet the proven cause** of the user's reported directional error.
5. The last bow video predates some of the changed source, so it cannot validate current code. Existing off-center tests call `ArrowManager.TrySolveAim()` and `ArrowProjectile.Spawn()` directly; they do not exercise the full `PlayerCombat` draw/release → UI rendering → shot path and do not render Play frames.

## 2. Hard scope and invariants

- Plan order is evidence/reproduction first, then one root cause at a time. No speculative patches, blanket visual asset swap, or “same API means aligned” completion claim.
- Preserve inventory/dose/fuel/damage/heal/buff/mask behavior and gameplay effect cadence. Visual sample rate must never determine damage or consumable use.
- Preserve bow ammo, power, damage, base speed, gravity, collisions, accepted draw/release/animation paths unless a later narrowly proven correction is necessary and explicitly within approved scope.
- Keep `GasCloudField`'s stationary potion cloud contract distinct from continuous G-held trail.
- Do not alter MainScene/shared URP renderer/vendor AERO assets for experiments. Renderer experiments use an isolated cloned renderer/test setup only.
- Preserve the very dirty/untracked worktree. Before any future implementation, re-read status/diffs and record an exact allowlist. No reset/clean, no `git add -A`, no commit/push unless explicitly requested.
- All code tasks use test-first behavioral tests (RED observed, then GREEN). Source-string tests may supplement but never substitute for geometry, state transition, rendered pixel, or Play tests.

## 3. Recovery roadmap — sequential gates

### Phase 0 — Freeze inputs, reproduce newest video semantics, baseline ownership

**Files read-only:** latest video(s), `GasSprayer.cs`, `GasSprayerController.cs`, `SprayInputHandler.cs`, `SpecialEffectsController.cs`, `GasCloudLauncher.cs`, `GasCloudField.cs`, `TestTerritoryCombatSetup.cs`, `GameSetup.cs`, active MainScene/Test_10 YAML, `ArrowManager.cs`, `BowAimState.cs`, `PlayerCombat.cs`, `BowAimReticleUTK.cs`, `ArrowProjectile.cs`, `PanelSettings.asset`, existing tests, fresh `Editor.log`.

Tasks:
1. Extract timestamped 30fps snippets around G press, first orange appearance, player movement/turn, G release, and post-release fade from newest gas footage. Track player image position and orange RGB/connected regions per frame, but don't label a region “gas” from color alone. Extract bow footage around one actual release at source fps; include cursor/reticle/player/projectile/target.
2. Inspect the current Editor.log by timestamp and search for a unique start/stop/source marker, missing Resources, shader errors, ParticleSystem warnings, or `GasSprayer` disabled/controller missing. Video and logs must be from the same Play run for causal correlation.
3. Map exact active scene path: in Test_10 verify GameSetup isn't bypassed unexpectedly, component order, `GasSprayerController`/`GasSprayer`/`SprayInputHandler`/`SpecialEffectsController` presence, `IsEquipped`, `LoadedPotionId/Count`, input focus/UI blockers, selected camera, renderer. Log one concise `[GasTrace run=...]` record on transitions, not per frame.
4. Run four isolated variants in the same camera/setup: (A) G only, no potion, (B) G with Poison_TestPotion explicitly loaded, (C) Mouse1 only, (D) potion quickslot only. Mark each run with a unique mode/source label and do not activate two paths together. Record `IsSpraying`, active producer, particle counts, tint, nozzle transform/world forward, renderer-host/session and visible pixel masks.
5. Check whether G is hold-to-spray per current controller or the user intended a toggle; current source implements hold. This plan preserves hold/release pending evidence and does not silently redefine it.

**Gate 0:** every orange/green visible feature in newest clip is either assigned to an exact source or remains an explicit unknown with the smallest isolated reproduction to resolve it. Record the actual G-only result. If the expected continuous plume is gated by missing dose or uninitialized controller, the design plan branches to that specific fix before VFX architecture work.

### Phase 1 — Bow release ID, displayed pixel, selected world point, initial velocity

**Use an isolated Test_10-compatible controlled setup.** Add only diagnostic capture and behavioral tests after user implementation approval.

For each monotonically increasing `shotId`, capture in one event:
- input type/path (normal Bow draw/release, quick shot, queued/parry release), `Time.frameCount`, screen resolution, panel/display, actual cursor pixel;
- visible reticle element `worldBound.center` transformed back to screen pixels, sample passed to `ScreenPointToRay`, their signed pixel delta;
- active camera instance/tag/enabled/pixelRect/projection, ray origin/direction;
- all ray hits ordered by distance with collider/root/layer/tag/trigger/active/IDamageable classification, and the exact selected point;
- actual spawn/muzzle Transform path and world position, player pose before/after yaw, spawn position;
- solver direction, projectile transform forward/up/axis, Rigidbody velocity before first physics step; at +1/+2 FixedUpdate, record velocity/position and label gravity/impact separately.

TDD/coverage, before changing aim code:
1. Geometry tests that map actual visible element center panel→screen and assert round-trip cursor error at center/edges/corners at 16:9 and a second aspect/resolution, against real production PanelSettings and root transforms.
2. Ray ordering tests with shooter child, terrain, intended target, invisible/non-target collider, trigger, overlapping target/terrain; define intended selection policy from user-visible marker semantics.
3. Full input-path behavioral test that invokes the same draw/release owner as gameplay and verifies immutable release snapshot is consumed; include cursor movement immediately after release, UI schedule race, camera handoff, stale/no sample, no ammo.
4. Projectile test measures actual initial velocity and render axis, then one/two physics updates for gravity; verify any displayed body exists and follows that axis. Current helper test directly spawns a projectile and is insufficient for UI/input integration.

After seeing RED, implement only the proven fault:
- If reticle center round-trip differs, fix conversion from the actual rendered `worldBound.center`, not a fresh raw mouse sample.
- If scheduler/release state races, snapshot cursor+camera at release and make visible reticle and solver consume the same immutable release sample; prevent scheduled updates overwriting it before shot.
- If a wrong collider is selected, define target/terrain semantics and filter only based on tested intentional classes; do not choose an unrelated first collider.
- If the chosen aim point is right but muzzle/spawn origin differs, bind/verify the actual bow socket or calculate from observed spawn transform. Avoid post-spawn actor yaw as an unmeasured compensation.
- If velocity is right but user-visible shaft is absent/wrong, repair the presentation path, including the confirmed root renderer disabled / uncalled GLB mount possibility, while preserving projectile physics/collision.

**Gate 1:** on at least 9 screen positions × 2 aspect ratios, every release's displayed reticle center→ray→selected point→muzzle→initial velocity is within the test's declared screen/vector tolerance; no stale sample/camera; actual visible arrow axis matches initial velocity. Then record actual Play capture and compare gravity separately. A solver-only test pass cannot clear this gate.

### Phase 2 — Select one gas rendering contract and compare high-quality candidates

Only after Gate 0 attributes the effect and proves the active G-only producer.

**A. Contract and one owner**
- G input owner: `GasSprayer.Update()` / `GasSprayerController` state. Audit Mouse1 `SprayInputHandler` and legacy `SpecialEffectsController.OnGasSprayStart()` as secondary paths; decide from observed gameplay whether to retain Mouse1 compatibility, but all paths must call one authoritative state transition and must not create a second competing gas VFX.
- The active emitter is anchored to the actual fitted/back-slot nozzle or verified equivalent transform, follows player position and yaw while G is held, and emits continuously while allowed. The visible direction must match measured nozzle forward. Reference trail is created behind/across travel path; whether it should use nozzle heading, inverse facing, or movement velocity is settled via a side-by-side Play spike, not guessed from names.
- Every emitted puff is world-space/history data at its birth position. Old samples must not inherit the player transform. On stop, no new samples; old samples continue age/fade. On moving, turning, in-place yaw, frame hitches, stop/restart, ensure no gaps/teleport smear/identical-position pileup.
- Empty/unloaded dose state: separate “canister gas spray visible?” from “potion effect payload loaded?” The latest test must make it explicit whether G should produce neutral/visible carrier gas with no potion, or be blocked. Current source allows `IsSpraying` but `EmitPlume` returns transparent when no classifiable potion; choose acceptance from user requirement and implement only after approval.

**B. Candidate A — high quality world-space trail (default comparison path)**
- Deterministic pooled path sampler: emit by distance + max time, interpolate missed distances across frame hitches, use nozzle world pose and heading. Add a small stationary cadence while G remains held only if needed for reference/visible continuous spray; strictly bound maximum active puffs and do not change gameplay tick cadence.
- Compare a layered stack with genuinely different scales/functions: dense irregular cloud-body puffs, broader soft fringe, sparse fine turbulent wisps. Replace repeated uniform lobes with authored FBM/noise-broken density masks or a verified animated flipbook; use a URP-compatible custom ShaderLab particle material for organic edge breakup and soft depth/ground intersection. Age drives expansion, density breakup and alpha fade. Keep lime/green semantic identity and background readable. No one big follower cloud, orange mask, flat rectangular strip, repeated perfect circles, or simply raising emission rate.
- Bound pool/overdraw/material count, no per-frame allocations, explicit owner lifecycle for reload/unequip/focus loss/scene unload, and clear old history on new scene. Test world positions remain fixed as emitter/player moves.

**Candidate B — VFX Graph trail (only if actual project package/runtime supports reliable render)**
- Confirm package version and tested graph assets; create via editor-authored path or an already working graph, never hand-write serialized `.vfx` YAML. Render in isolated test setup, prove it on actual game camera and capture image pixels. Test world-history buffers, stop/fade, shader compatibility, overdraw and CPU/GPU. Reject if package presence doesn't translate to visible output.

**Candidate C — localized volumetric path (isolated option)**
- Existing project-owned `AeroLocalizedGasVolume`/feature is a *single moving analytic volume*, not historical trail storage; optional host requires an isolated configured cloned URP pipeline. Do not misrepresent it as active or wire it globally. Only prototype if path history is added in a cloned renderer and passes render/perf comparison. Mirza AERO vendor files remain untouched.

**Comparison protocol:** same Test_10 camera, ground, player path, lighting, loaded poison dose and input timings. Capture A/B/C independently: straight walk, curved/S turn, in-place rotation, sprint/stop/restart, G release and 3–5s tail. Reference inset at matching scale. Compare irregular silhouette, green hue, ground readability, new-vs-old density, path continuity, fade, visible card edges, max particles/segments and GPU/CPU/GC. Candidate must look materially closer than the new video; source assertions/particle alive count alone cannot pass.

**Gate 2:** one candidate shows a nozzle that stays with player during G hold and a curved, world-anchored cloud trail that remains after leaving, with new/old density and fade resembling reference; G release halts emission; no square/stepped orange effect remains in the isolated G-only capture. User-facing Play capture is required to approve visual quality.

### Phase 3 — Integrate verified candidate and preserve gameplay

- Connect exactly one continuous plume owner to the verified emission state. Retire/disable only the proven duplicate visual trigger with a regression test; preserve unrelated SpecialEffects, potion field, UI and input compatibility.
- Keep `_effectTimer/_effectInterval` and `ApplySprayEffects()` separate from VFX sampling. Add deterministic tests for G edge/hold/release, no-dose behavior chosen by contract, gas/dose time accounting, poison/heal/buff counts, mask immunity, and unchanged fuel/dose deltas across different visual rates.
- Cleanup tests cover held focus loss, equip/unequip, reload, dose/fuel depletion, death/disable, scene exit; all spawned pools/materials/bridge state reset and no duplicate active input/VFX listeners.
- Runtime transition logs once at start/stop/source selection show run id, producer, loaded dose/tint, particle/segment count, and nozzle transform. No per-frame log spam.

### Phase 4 — Sequential verification and live acceptance

1. Run new focused behavioral tests and observe exact RED then GREEN.
2. Run Unity compile with fresh output and verify source/DLL mtime coherence, `error CS=0`; read fresh Editor.log for shader/material/ParticleSystem warnings.
3. Run actual Test_10 Play capture for bow and gas; no substituted screenshots from earlier code.
4. Parse fresh full EditMode XML and list every fail. Existing previous full suite was 434/425/9 at 20:40 KST; compare but don't silently call them passing or baseline without current evidence.
5. Independent read-only QA on exact scoped diff; re-read status, keep all unrelated dirty files untouched. Update `docs/QAPROGRESS.md` with current evidence and explicitly pending any failed visual gate. No ROADMAP phase completion before all acceptance checks.

## 4. Acceptance checklist — no “close enough” by code-only evidence

### Bow
- [ ] `shotId` identifies the displayed marker, actual pixel, selected camera, hit point, muzzle and initial velocity.
- [ ] Marker center is within declared tolerance (goal ≤1–2 screen pixels after measuring actual UI dimensions) of canonical ray pixel at center/edges/multiple aspects.
- [ ] Visible shaft/body or agreed projectile cue initially tracks that velocity; root disabled renderer/GLB attachment is resolved deliberately.
- [ ] Post-launch gravity is distinguishable from wrong initial direction.
- [ ] Actual Play evidence matches tests; no ammo/power/damage/gravity/collision regression.

### Gas
- [ ] Isolated G-only capture has one identified source and a working hold/release transition.
- [ ] No-dose behavior is explicit and matches requested G spray; a controller `IsSpraying=true` with no visible plume is not accepted silently.
- [ ] Emitter/nozzle follows player; new puffs form trail; old puffs stay in world when player moves/turns away.
- [ ] Irregular soft translucent green multi-scale puffs/wisps, dense fresh core/soft fringe, age-based expansion/fade; no orange pixel blocks, billboard cards, or static map-sized sheet.
- [ ] G release stops emission but preserves tail fade; scene/input lifecycle is leak-free.
- [ ] Gameplay effect/dose/fuel/mask invariants and bounded rendering/performance are verified.
- [ ] User can inspect actual new Play video side by side with reference before visual completion is recorded.

## 5. Current status and not claimed

- New video 3 was extracted and inspected. It visibly still contains orange block-shaped patches and a broad green diagonal feature, but the feature source has not been proven. No game/source changes were made for this plan.
- Code review identifies two concrete high-priority investigation points: (1) G may show `IsSpraying` but no continuous particle if there is no classifiable loaded potion; (2) G `StartSpray()` can also spawn detached legacy `SpecialEffectsController` fog, while the continuous child-emitter pool, Mouse1 secondary input and optional isolated gas bridge coexist. These are source facts/candidates, not proof that any one creates the video pixels.
- Current arrow raw screen sample and solver helper tests do not test actual rendered UTK center or full release path. Projectile root renderer is disabled and the GLB mounting helper appears not to be called; this may cause a visible-line/presentation discrepancy and is explicitly tested in Phase 1, not assumed to be the sole aim root cause.
- Prior focused tests (1/1 each) only checked an off-center solver→spawn velocity/axis path and plume parameter configuration. They do not prove the visible problems resolved.
- Latest prior full EditMode run was 434 total, 425 passed, 9 failed. Prior compile log had zero CS errors/warnings but DLL timestamps were older than that log; freshness was inconsistent. No newest Play proof/Editor.log after the new gas video is currently established.
- Do not amend ROADMAP or claim complete during planning. After plan review, implementation remains a separate approved phase.

> **Next implementation priority after approval:** Phase 0 controlled reproduction and owner attribution first; do not start tuning or adding VFX assets until the newest orange/green effect and the G-only visual path have been separated. In parallel, acquire one same-release bow capture and determine whether the failure is visible marker geometry, selected ray point, muzzle, velocity, or invisible projectile presentation.
