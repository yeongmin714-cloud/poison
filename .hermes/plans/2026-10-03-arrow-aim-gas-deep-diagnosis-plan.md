# 활 조준 불일치 + 가스 분사 품질 — 심층 진단 및 복구 계획

> **상태: 계획 전용.** 이번 요청은 “심층적으로 접근해서 계획”이다. 이 문서 작성 시 게임 코드, 테스트, 씬, 이미지, 셰이더, 렌더 파이프라인 설정은 수정하지 않는다. 기존 생성 산출물/사용자 변경을 덮거나 정리하지 않는다.
>
> 프로젝트 `/mnt/c/Unity/code` · Unity `6000.4.10f1` · URP 17.x
> 입력 자료: `Screenshots/가스 분사 테스트.mp4` (1240×828, 30fps, 50.005s), `Screenshots/화살 모션 테스트.mp4` (1240×828, 30fps, 36.48s), `Screenshots/가스 살포 예시.mp4` (984×556, 30fps, 4.10s)
> 현존 전단계 계획 `.hermes/plans/2026-10-03-arrow-aim-gas-vfx-deep-recovery-plan.md`보다 이후 사용자 영상/코드 상태를 반영한 업데이트 계획이다.

## 1. 이번 분석의 관찰과 증거 수준

### 1.1 가스 reference — 실제 영상에서 확인되는 목표

참조 4.10초를 원본 30fps(123 프레임)로 timestamp를 붙여 검토했다. 확대 시트에서 보이는 기준:

- 가스는 하나의 균일 띠나 큰 평면이 아니라, **서로 겹치는 덩어리 puff**로 구성된다. 실루엣은 scalloped/불규칙하고, 각 puff의 중심은 더 조밀하며 바깥 가장자리는 부드럽고 환경이 비친다.
- 플레이어가 커브를 그리며 이동하는 동안 신규 밀도는 현재 위치 뒤에 쌓이고, 이미 만들어진 trail은 플레이어가 떠난 뒤에도 배경 지형 좌표에 남는다. trail path는 움직임 곡률을 기록한다.
- 오래된 구간은 캐릭터 가까운 새 구간보다 옅고 덜 조밀해 보인다. 정확한 lifetime/확산률은 이 4.1초, 압축 영상만으로 산정하지 않는다.
- 캐릭터는 보이는 구간 동안 계속 이동한다. **정지 때 계속 방출/한곳에 쌓이는지 영상에서 증거 없음.**
- reference가 “유체 시뮬레이션” 또는 특정 renderer라고 단정할 수 없다. 필요한 것은 관찰된 화면 성질이지 내부 기술을 복제하는 것이 아니다.

### 1.2 사용자의 50초 가스 테스트 — 재발 신호

- 19–24초 구간 6fps 시트와 21.0/22.5/24.0초 원본 프레임에서 오렌지/갈색의 격자형·계단 경계 영역이 플레이어 주변에 나타난다. 그 모양은 reference의 녹색 puff trail과 명백히 다르다.
- 해당 영역의 source는 아직 확정되지 않았다. 후보는 `GasCloudLauncher → GasCloudField`, continuous `GasSprayer`, 다른 VFX/debug overlay다. 이 셋을 서로 다른 run_id로 격리해 재현해야 한다.
- 12–15초 10fps 시트에서 선형 흰/노랑 흔적이 나타나지만, 동일-shot source 로그/명확한 arrow silhouette가 연결되지 않아 화살·트레일·다른 시각 표식을 단정하지 않는다.
- 앞서 1fps 전체 contact sheet를 기반으로 “연속 spray는 보이지 않는다/오렌지 영역은 특정 이벤트다”라고 단정한 설명은 충분한 근거가 아니었다. 이번 계획은 source attribution 전에 VFX를 새로 만드는 것을 금한다.

### 1.3 화살 36.48초 테스트 영상 — 아직 해결되지 않은 사항

- 1fps overview는 UI/플레이어/일부 streak를 보여주지만 화살 비행을 검증하기엔 낮은 표본률이다. 8fps 처리 결과도 reticle/shot/muzzle을 동일 release 순간에 확정하지 못했다.
- 해당 영상만으로 “초기 발사 벡터가 정말 어긋난다”와 “다음 프레임의 cursor를 이전 shot과 잘못 비교했다”를 완전히 분리하지 못했다. 그러나 **사용자가 최근 게임에서 여전히 어긋난다고 직접 보고**했으므로 문제는 미해결로 보고 계획한다. 영상 증거가 불충분하다고 사용자의 관찰을 무효화하지 않는다.
- 플레이 중 로그에서 실제로 확인해야 할 값: 같은 release id의 표시 리티클 center(screen), cursor pixel, active camera, ray, 받아들인 hit collider/point, muzzle, solved aim point, spawn direction, initial Rigidbody velocity, arrow render-axis. 중력 이후 곡선/충돌은 이와 따로 기록한다.

## 2. 실패한 전작에서 드러난 설계 문제

### 활

최근 코드에 UI 표본을 발사와 공유하는 `BowAimState`가 들어갔지만, **raw mouse pixel을 reticle center pixel이라고 전제**하고 있다. `BowAimReticleUTK.UpdatePosition()`은 `RuntimePanelUtils.ScreenToPanel(root.panel, screen)` 후 `root.WorldToLocal(panelPoint)`를 쓰며 panel settings는 ScaleWithScreenSize다. 실제 렌더된 reticle center를 다시 screen으로 변환해 raw sample과 비교하는 계측이 없다. 같은 입력 API를 쓴다는 사실은 같은 화면 픽셀이라는 증거가 아니다.

다른 후보도 남아 있다:
- `TrySolveAim()`은 첫 non-shooter collider를 terrain/target semantics 없이 택한다. 화면 ray가 닿는 sensor/trigger 제외 외에도 visual target과 충돌 target이 같다는 보장이 없다.
- ray miss 시 `ray.GetPoint(100f)`로 임의 aim point를 택한다. 먼 경사의 ray/고정 조준선 의미와 맞는지 확인되지 않았다.
- 실제 muzzle을 조회하되 아직 `SetSpawnPoint()` 호출 경로는 검색에서 확인하지 못했다. 그래서 대부분 fallback origin일 수 있으나 실제 Play/runtime hierarchy 확인 전 미연결로 단정하지 않는다.
- `TryBowShot()`은 mouse screen point 뿐 아니라 `Camera.main`을 별도 시점에서 재조회한다. 같은 frame camera를 UI placement와 projectile ray가 공유하는 snapshot은 없다.
- 화살 root의 primitive renderer는 꺼져 있고 사용자 GLB 자식 장착이 조건부다. 시각적으로 보이는 줄/이펙트가 실제 투사체 자체인지 먼저 식별한다.
- 회전/런지/Animator event/physics timestep이 release 후 위치를 바꿀 수 있다. 표식 픽셀에서 발사한 것처럼 보이는 원점, 실제 rigidbody spawn world point, 초기 방향을 분리 계측해야 한다.

### 지속 가스

`GasSprayer`에 새 베이크 soft/lobe/wisp 마스크 3종과 world-space particle pool이 이미 들어가 있지만, 사용자 화면에서 품질 개선으로 인정되지 않았다. 동일 세 개 sprite를 rate/alpha만 조절하는 것은 반복하지 않는다.

- 현재 particle system은 player child cone emitter를 world-space로 시뮬레이션한다. 이는 old particles가 남을 수 있다는 것만 보장하며, **emission transform이 실제 분사구이고 trajectory를 정확히 따라가는지** 보장하지 않는다.
- 매 `Update`마다 pool `Play()`를 유지하고 `StopEmission`은 StopEmitting으로만 요청한다. leak/warning 여부는 최신 Editor.log 및 alive particle count로 다시 확인해야 한다.
- 단일 billboard puff mask는 반복적인 카드나 동그란 덩어리로 보일 수 있다. Reference는 반복 패턴이 아니라 변형된 겹침, 고밀도 중심, 연한 fringe, 다양한 puff 스케일, 옅어지는 tail이다.
- `GasCloudField` 소비형 radial expanding effect는 separate contract다. G-held trail의 reference에 맞추려고 이를 제거/변경하지 않는다.

## 3. Scope/불변 계약

- 이 단계는 분석/계획뿐. 구현 때 사용자 “진행” 승인 이후만 수정한다.
- 활: 표시된 실제 UTK marker center를 기준으로 한다. ammo, damage, power, speed, gravity, collision, animation, auto-aim behavior 불변. misfire는 임의 forward shot가 아닌 명시적 cancel.
- 가스: 사용자 reference와 시각적으로 견줄 world-trailing green puff/wisp. 기존 G hold, canister fuel/dose, damage/heal/buff interval, mask immunity 불변. `GasCloudField` 물약 사용은 별도 보존.
- MainScene shared URP renderer, project default pipeline, vendor AERO files 변경 금지. Renderer/shader 실험은 `Test_10` clone/isolate only.
- 모든 새 런타임 메모리/renderer/particles는 bounded pool, explicit teardown, allocations/performance cap 필요.
- 관련 트리에는 기존 dirty/untracked 파일이 많다. 수정 허용목록을 실행 직전 다시 구하고 exact files만 수정, `git add -A` 금지, commit/push 금지.

## 4. 복구 페이즈 — 각 단계 통과 전 다음 단계 코드 변경 금지

### Phase 0 — 테스트/프로젝트 상태와 기존 수정분 동결

1. `git status --short`, `git diff`에서 이 문제 관련 이전 변경을 모두 분류: original baseline vs prior assistant patches vs user-owned unrelated changes.
2. 현행 QA snapshot: current source/hash, compile.log/XML timestamps/results, Unity Editor process/lock, latest Editor.log timestamp.
3. 기존 계획 및 이 문서 경로/범위를 일치시킨다. 이전 plan의 “계획 전용” 상태는 사용자의 이후 실행 요청이 있더라도 실제 코드 변경/Play 검증이 됐다는 뜻은 아니다.

**Gate 0:** 재현 가능한 현재 baseline과 보존해야 할 dirty files 목록이 확보됨.

### Phase 1 — 동일 release로 화살 실패 재현 / 1차 좌표 불일치 경계 특정

**Instrumentation test fixture** (test-only, main gameplay unaffected): fixed ground + target + player + known muzzle + active camera + actual `BowAimReticleUTK` root. Cursor centers: 9 screen points(center, edges, corners), aspect 16:9 and 4:3, 0.5/1.0 scale if possible. If Unity batch cannot render UI, automated Play fixture/capture is mandatory before claiming UI-level fix.

On each press/draw/update/release/shot, one monotonic `shotId` writes structured fields:
- `Mouse.current.position`, event `PointerEvent.position`, `RuntimePanelUtils.ScreenToPanel` result;
- `root.worldBound`, reticle `worldBound`, reticle center converted with `PanelToScreen`, raw input delta in pixels;
- display index, Screen dimensions, panel size/scale/reference resolution, screen Y origin; camera name/id/enabled/rect/pixelRect/projection/viewport, frame count;
- camera `ScreenPointToRay` O/D, every relevant hit ordered by distance with collider/root/layer/tag/trigger, hit classification;
- intended muzzle reference path and world position, player transform before/after rotation, world target;
- computed launch dir and projectile transform/rigidbody initial velocity before first FixedUpdate; samples at 1/2/3 FixedUpdates before significant gravity.

Test each point with player/muzzle fixed; release once; do not move cursor for 0.5s. Then separately run moving cursor to test temporal mismatch. Use a calibration cross/line **only in isolated diagnostic scene**, not to decide visually without pixel evidence. Capture true 30fps or higher.

**Gate 1:** actual rendered reticle center pixel vs ray input numeric error measured; same shotId all ray/muzzle/velocity points; root first divergence documented. If each subsystem values align but perceived line differs, identify visible line owner (debug line, legacy trajectory, selection ray, trail, other projectile) before changing bow solver.

### Phase 2 — Fix only the proven aim source; preserve weapon contract

TDD write before any prod edit:
- Geometric tests for screen/panel round-trip all 9 points/resolutions/aspects with tolerance ≤1 pixel or justified device tolerance.
- ray selection tests with ground/target/shooter/overlapping sensors and colliders.
- multi-input path tests for normal bow draw/release, immediate fallback click, queued-after-parry release, camera handoff, no ammo, no UI sample.
- projectile test asserts actual spawned Rigidbody initial `velocity.normalized == expectedMuzzleToSelectedWorldPoint.normalized` and no gravity contribution until fixed step.

Choose code correction strictly by Phase 1 proof:
- If center conversion is wrong, establish one authoritative Screen↔Panel mapping and compute the **ray from actual rendered center**. Never use raw mouse simply because it produced the initial placement.
- If asynchronous UI sample is wrong, snapshot sample + camera at input release, then set reticle’s release state to the same snapshot; don't let scheduler overwrite it with later mouse. Keep new draw fade/sample independent.
- If wrong collider is chosen, define click semantic target policy (ground/terrain vs valid target); exclude dead/inactive sensors and irrelevant colliders. Don't globally select first non-shooter collider.
- If muzzle is wrong, bind to measured active bow hand/muzzle; determine attachment at pre/post rotation and resolve origin consistently.
- If Rigidbody direction is correct but visible path differs, isolate axis/renderer/trajectory and collision/gravity. Do not recalc aim to compensate a rendering/physics artifact.

**Gate 2:** same-shot true UI center/ray/muzzle/initial velocity pass all 9 points twice, two resolutions/aspects, empty/blocked ray/camera replacement; full Play recording and diagnostic log agree. Failing any point blocks aim completion.

### Phase 3 — Reference gas frame-by-frame profile + live-effect attribution

First establish exactly which game feature user means. The 50s test has at least two possible visual behaviors; no layer/source shall be guessed:
1. frame-segment original 30fps: record transitions around 0–20s continuous movement, 20–25s orange-square, remaining sequence. Avoid contact sheet alone; crop/player mask/time labels.
2. For same time register sprite/video image RGB masks; threshold orange, toxic green, white streak; measure area, bbox shape, centroid, edge roughness, overlaps/contrast to ground and previous frame displacement.
3. In an isolated runtime scene log single input-owner events tagged `[GasHold]` and `[GasConsumable]`; record `shot/effect id`, G edge/state, Mouse1 state, controller state, loaded type, call stack/source, active particle systems/renderer features, visual origin/heading. Capture G-held alone with no consumable; then consumable alone with no G; never both for attribution.
4. Attach unique temporary debug outlines / render tags only in test setup if needed; inspect all `OnGUI`, custom overlay, AERO volume, sprite/card and `GasCloudField` owners. Remove test-only markers after diagnosis.

Reference measurement (30fps 123 frames):
- Track actor position path vs plume newest lobe center every frame. Old puffs remain on map while player turns/moves; individual puffs partially overlap; scalloped irregular perimeter; center greener/darker, edge softer, ground visible; old area lower contrast than fresh area.
- Record observed tail visual lifetime as lower/upper bound from earliest visible and first not-visible frames; do not assume exact fade physics from compressed footage.
- Stationary emission cannot be accepted/rejected based on provided example because actor never visibly stops. Explicitly classify as not observed; gameplay remains continuous while G held per current user design and no ad hoc pooling toggle.

**Gate 3:** identify orange block emitter and separate held-spray source; reproduce each alone; measured image features establish exact failure type: shape/card alias/color/opacity/rigid decal/source path/no emission/particle trail shortness.

### Phase 4 — High-quality visual candidate in isolated test scene, side-by-side

User asks “최대한의 품질”; deliver render candidates that are visibly different architecture/appearance, not just tune old rates. Scope after Gate 3:

**A. Recommended primary: age-aware, path-sampled volumetric puff trail**
- Emitter head uses actual sprayer nozzle attachment point; every sample determined by max world distance between points, elapsed time and heading-angle threshold. Spawn fixed orientation/world-space point; keep old puffs world-anchored.
- Use authored irregular puff density masks/flipbook with distinct scales and individual seed/rotation, not current three uniform textures repeated. Core + fringe color response comes from URP-compatible custom particle ShaderLab material: animated 3D/simplex noise breakup, low-frequency billow and edge distortion, depth soft intersection, alpha-preserving base mask, per-particle `_Age01`/color gradients. Fresh plume has 2–3 density layers (core puff, broad low-alpha volume, slender sparse curl wisps) without a continuous opaque ribbon.
- Sample interpolation from last pos to current pos on hitch; deposit at bounded regular distances. On tight turns make curved overlapping segments. When stopped, keep low but capped stationary emission because G state remains active; rate limit by time and volume cap. On stop, emission halts immediately, existing puff age continues to fade. No transform-parenting old puffs to player.
- Tune for readability on green flat QA floor and dark real environment; preserve green semantic poison hue, adequate translucency; do not set ground to orange. Default max active set hard cap; pool/recycle oldest only after expiry, not while visible.

**B. Optional higher-end: URP localized volumetric ribbon in cloned renderer**
- Existing AERO local renderer is one moving analytic volume and does not store trail history. Add/test path-history segment buffer and age/density field only if bounded per-camera cost fits; view cap for number of segments, shader raymarch resolution/profile, depth clipping, alpha/compositing. Must run only isolated cloned URP Renderer asset. Compare visual/CPU/GPU vs Candidate A.
- Avoid assuming AERO plugin automatically provides local persistent trail; prove its resource/per-pixel cost in test scene.

**C. Optional VFX Graph**
- Check manifest/editor package availability. If installed, create graph via editor-authored/test pipeline; no hand-written .vfx YAML. Use for high particle counts/turbulent puffs if hardware allows. Do not make this sole candidate if cannot validate render in current automated environment.

**Comparison protocol:** exact same player path/test timestamps, same camera/zoom/ground/light, reference inset; build three motion paths straight 4m, S turn, 180° stop-turn plus dwell/restart, then stop fade. Capture source 30fps. Show Candidate IDs A/B/C separately. A render is rejected if image is only many billboards/cards or still reads as orange/pixel blob. Require both pixel metrics and human/user visual acceptance; compile success isn’t visual approval.

**Gate 4:** reference-comparable soft organic green overlapping trail; world history preserves exact curve; fresh/old contrast visible; no blocks/card edges/repeating uniform circles; environment remains readable; bounded perf; no temporary effect stuck after disable/scene unload. At least one capture is judged against user’s original reference clip.

### Phase 5 — Regression, acceptance, record

- G-hold alone and potion button alone triggers exactly distinct intended sources; toggle/focus loss/unequip/reload/dose/fuel depletion/indoor state unchanged.
- Fixed time interval regression checks same poison damage count/amount range, heal/buff count, mask immunity, same loaded potion and fuel delta. VFX sample rate does not affect gameplay ticks.
- Teardown leaves 0 active pooled material/puff leaks; bounded max active, no per-frame GC spikes; log is freshly generated and no duration warning, shader errors or per-frame warnings.
- Run targeted tests, sequential compile, full EditMode; classify exact failure messages vs baseline. Then real Play captures for both systems. No Play means both visual gates remain pending.
- Update `/mnt/c/Unity/code/docs/QAPROGRESS.md` with current actual status and run info. Do not set ROADMAP phase completed until gate pass. Preserve root QAPROGRESS handoff. No staging/commit/push unless explicit ask.

## 5. Main implementation scope after user approval

Likely inspect/change allowlist after Gate 1/3:
- Aim: `PlayerCombat.cs`, `ArrowManager.cs`, `BowAimState.cs`, `BowAimReticleUTK.cs`, perhaps `ArrowProjectile.cs`; new pure coordinate/solver utility/tests only if it reduces coupling.
- Gas: first identify active producer. Then `GasSprayer.cs` or `GasCloudField.cs`/launcher only if attributed, new `GasTrailEmitter`/custom shader or candidate assets, isolated test setup and focused tests.
- Renderer pipeline assets/vendor packages/MainScene configuration remain outside scope.

## 6. Records and explicit not-yet-done

- This plan does not claim the arrow is fixed; same-release 30fps screen/ray/muzzle/velocity evidence is absent.
- This plan does not claim gas is fixed; orange event’s producer and isolated held-spray appearance are unproven.
- Recent source/UI changes compile and the latest full EditMode result is 423/432 passed with 9 unrelated failures; no image-level acceptance was run. The older Editor.log’s ParticleSystem duration warnings are stale evidence; require new Play log.
- The 30fps gas reference plainly shows a moving, world-anchored trail of overlapping irregular green puffs whose old regions fade relative to new ones. Its stationary behavior is not seen.
- All earlier broad image-model statements about champion/game identity or exact numeric fade should not enter requirements; those were not established by user evidence.

> This is the planned, deeper correction; implementation remains unstarted pending user review/approval.