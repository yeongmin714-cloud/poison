# 활 조준 편차 + 가스 VFX 품질 실패 — 심층 복구 계획

> **상태: 계획 전용. 이 문서 작성 중 생산 코드/씬/텍스처/렌더 설정/테스트/QAPROGRESS는 수정하지 않는다.** 사용자가 요청한 것은 더 깊은 분석과 계획이다. 구현은 계획 확인 뒤에 수행한다. 이전 시도에서 “테스트/컴파일은 됐지만 화면은 그대로”가 발생했으므로, 이번에는 Play 영상과 같은-shot 계측 통과 전 완료로 표시하지 않는다.
>
> 프로젝트: `/mnt/c/Unity/code` · Unity 6000.4.10f1 · URP 17.x
> 문제 영상: `Screenshots/화살 모션 테스트.mp4` (1240×828, 30fps, 36.48s), `Screenshots/가스 분사 테스트.mp4` (1240×828, 30fps, 50.005s)
> 가스 참조: `Screenshots/가스 살포 예시.mp4` (984×556, 30fps, 약 4.10s)

## 0. 받아들인 실패와 핵심 원칙

사용자가 여전히 화면상 조준선/표식과 화살 비행이 다르고, 가스가 예시보다 저품질이라고 보고했다. 앞선 수정은 화면 검증 없이 입력 표본/solver 경로를 더했을 뿐이며 결과 수용으로 연결되지 않았다. 그러므로 **기존의 “같은 Mouse API” 또는 소스 테스트 통과를 해결 증거로 취급하지 않는다.**

앞선 계획의 세 가지 취약점도 교정한다:
1. 서로 다른 프레임의 커서/화살 흔적을 비교한 것을 불일치 증거로 충분하다고 보지 않는다.
2. 가스 사용 이벤트의 블록형 오렌지 영역과 G-held 지속 분사를 같은 효과라고 가정하지 않는다.
3. “더 많은 입자/새 텍스처” 자체를 품질 개선으로 인정하지 않는다. 실제 렌더 캡처를 기준으로 한다.

## 1. 현재까지의 영상 실측 — 신뢰도 명시

### 1.1 테스트 영상: 가스 분사

- 50초, 30fps 원본이다. 1fps 전체 시트는 상태 전환 위치를 찾는 데만 쓸 수 있고, 한 발사의 조준/탄도나 가스 수명을 판정할 해상도가 아니다.
- 12–15초 구간을 10fps로 확장해 본 표본에서는 캐릭터 주변으로 뚜렷한 밝은 라임색 안개 플룸이 이어지는 모습은 확인되지 않는다. 가느다란 흰 선형 흔적이 일부 컷에 나타나지만, 표본만으로 해당 흔적이 가스인지 화살인지, 또는 발사 전에 발생한 다른 이벤트인지 확정되지 않는다.
- 19–24초 구간의 6fps 시트와 21.0/22.5/24.0초 원본 개별 프레임에서는 **플레이어 근처에 픽셀/격자형 오렌지-갈색 영역이 나타나 크기가 커지는 현상**이 보인다. 부드러운 경계·내부 연기 질감·녹색 안개 농도층이 아닌 블록/계단 경계가 주된 시각 신호다.
- 테스트 영상만으론 이 영역이 `GasSprayer.EmitPlume()`인지 `GasCloudLauncher → GasCloudField`인지, 조준/공격 효과인지, 다른 디버그/렌더 경로인지 입증되지 않았다. 같은 시각에 입력 및 시스템 발화 ID를 기록해야 귀속 가능하다.
- 1fps 시트에서 보이는 캐릭터 뒤 노랑/갈색 선은 그림자, 무기/화살 흔적, 렌더 artifact와 구분되지 않았다. 아직 “실제 가스 trail”이라고 부르지 않는다.

### 1.2 테스트 영상: 화살 모션

- 36.48초, 30fps 원본. 1fps 개요에서 UI 조준표식, 캐릭터, 일부 흰 흔적은 찾을 수 있었지만 짧은 한 발을 상관관계 있게 보일만큼 표본률이 높지 않다.
- 8fps 확대 contact sheet에서 흰/노란 투사체형 픽셀과 오렌지 블록형 효과가 보이나, 캡처 자체가 발사 순간의 reticle center/muzzle/화살 첫 이동을 같은 시각으로 고정하지 않았다. 따라서 이전의 “흰 선은 왼쪽, reticle은 오른쪽” 판정은 가설일 뿐 확정된 계측 결과가 아니다.
- 비디오를 더 잘게 자르는 것만으론 cursor input와 GPU/UI 렌더 시각을 보장해 동기화하지 못한다. 다음 증거는 **실제 Play에서 한 번의 release를 식별자로 묶은 화면 녹화+런타임 데이터**여야 한다.

### 1.3 참조 가스 영상에서 직접 관찰되는 품질 기준

가스 참조는 984×556, 30fps, 4.10초다. 약 12fps 시트에서 판독 가능한 시각 속성:

- 밝은 라임/독성 녹색의 연속 trail 인상. 큰 단일 판/사각형이 아니라, 가장자리가 부드럽고 불규칙한 여러 덩어리가 서로 겹쳐 볼륨을 형성한다.
- 최신 plume은 캐릭터의 뒤쪽/이동 경로 부근에서 생기고, 앞부분은 캐릭터를 따라오며, 지나간 trail은 화면/월드에 남아 곡선 경로를 보존한다.
- 개별 덩어리의 중심은 상대적으로 진하고 외곽은 투명하며, 겹치는 구간에서 밀도가 강해진다. 지면 텍스처가 읽힐 정도로 투명해야 한다.
- 가장 오래된 조각은 최신 조각보다 옅고 사라지는 것으로 보인다. 프레임/노출만으로 정확한 초 단위 lifetime이나 stationary 상태에서 누적되는 규칙을 역산하지 않는다. 참조 clip 마지막에도 충분히 멈춘 구간이 없어 정지 누적 방식을 단정하지 않는다.
- 시각 품질 목표는 “움직이는 플레이어에 붙는 기체 구름”이 아니라, 이동을 기록하는 world-space 잔류 trail이다. 비슷한 효과라 주장할 시 게임 카메라와 실제 배경 위에서 증명한다.

## 2. 앞선 코드 변경과 반복 실패 후보

### 2.1 활 경로

현재 소스상 경로는 `PlayerCombat.Update release → BowAimState → BowAimReticleUTK scheduled UpdateTick/UpdatePosition → GetAimScreenPoint → ScreenToPanel/WorldToLocal → ArrowManager.TrySolveAim → Physics.RaycastAll 첫 비사수 collider 또는 ray.GetPoint(100m) → 실제 muzzle에서 point 방향 → ArrowManager.TryShootArrow → ArrowProjectile initial velocity → 이후 gravity`다.

이미 추가된 보호:
- UI 위치에 사용한 raw screen point를 Systems bridge에 기록하고, draw 이후 fresh sample이 없으면 취소.
- 카메라 재조회/비활성 카메라 및 화면/camera.pixelRect 밖 좌표에서 fail closed.
- aim ray, 첫 hit, muzzle, direction과 실제 발사 velocity 로그.

하지만 이것은 **증명된 해결이 아니다.** 미검증/잠재 불일치:
1. 표시 sample은 `RuntimePanelUtils.ScreenToPanel()` + `root.WorldToLocal()` 후 고정 200-unit element의 좌상단에 놓인다. OS/Game View cursor 픽셀에서 생성한 raw sample이 실제 렌더된 표식의 중심과 1:1이라는 Play 측정이 없다. PanelSettings는 ScaleWithScreenSize이며 1440×900 reference resolution과 화면비 매칭 설정을 사용한다.
2. `BowAimState` sample generation은 RTK에서 새 draw 동안 샘플이 갱신됐는지 볼 뿐이다. 발사순간의 UI 렌더된 world/screen bounds, panel/display, camera identity를 스냅샷하지 않는다.
3. solver는 mouse ray의 **첫 non-shooter collider**를 ground/terrain/target 의미와 무관하게 택한다. 투명한 테스트 collider, 지면 mesh, 가까운 물체가 목표가 될 수 있다. miss 시 임의 100m ray point는 reticle/화살의 의미 있는 ground target과 불일치할 수 있다.
4. 화살 모델은 `ArrowProjectile.Spawn()`에서 GameObject 생성 후 현재 **renderer disabled**이며, GLB mount 경로도 호출되는지 별도 검증이 필요하다. 사용자가 보는 “날아가는 것”이 오브젝트 body인지 trail인지 반드시 식별한다. Rigidbody를 고정 회전시키는 코드와 매 프레임 velocity 정렬/후속 hit/ground 충돌도 독립 검사한다.
5. 정상 bow draw와 기존 즉발 `TryAttack → TryBowShot(1f)` / parry queued release 등 여러 진입이 존재한다. 동일 sample/failure status가 모든 경로에서 지켜지는지 sequence-log 대조가 없다.
6. 일단 solver 방향이 정상이어도 중력 곡선, 투사체 충돌/튕김, 모델 축/렌더러 비가시와 3D→화면 투영 오류를 조준 오류로 혼동할 수 있다.

### 2.2 지속 가스와 소비형 구름 경로

기존 지속 `GasSprayer`는 3개의 child ParticleSystem, 서로 다른 baked `GasPlumeSoft/Lobe/Wisp` 텍스처, World simulation, rate 15/7/5, life 약 1.9–2.4s, speed 0.3–1.2, max particle 40/72를 구성한다. 이미 이런 sprite/pool을 넣은 후에도 사용자가 품질을 거부했다. 따라서 같은 기술의 알파/크기만 임의 조정하지 않는다.

- 스프라이트는 “soft mass + metaball lobes + ribbon wisps”의 마스크다. shader-level turbulent breakup/noise 없이 반복되면 개별 동그라미/늘어진 blob로 읽힌다.
- World-space particles는 방출된 점을 고정하지만 emitter의 nozzle shape/방향이 지금대로 정말 뒤쪽 trail을 만드는지 계측되지 않았다. 현재 shape는 Cone, child local offset `(0,0.3,0.25)`인데 nozzle 방향/캐릭터 속도/회전 연결에 대한 같은-frame 증거가 없다.
- 코드상 poison tint는 라임이지만 test video의 blocky orange/brown 영역과 색/형태가 다르다. 그 오렌지 영역의 owner를 로그로 식별하기 전에는 continuous `GasSprayer`만 고쳐선 사용자 화면에서 변화가 없을 수 있다.
- 별도 소비형 `GasCloudField`는 local-space particles와 `transform.localScale` expansion을 사용하고 `Destroy(gameObject)`는 duration 이후이므로 영상의 square patch / expanding zone이 이쪽, 다른 shader, 또는 debug renderer일 가능성을 독립 추적한다.
- 기존 Editor.log의 Unity 경고 `Setting the duration while system is still playing`는 예전 타임스탬프다. 소스에서 Stop→Configure 순서를 더했으나 최신 Editor.log가 없으므로 해소 확인은 보류.

## 3. 불변조건 / 금지

### 화살
- 사용자가 말하는 조준선의 **정의와 producer를 먼저 확인**한다. cursor marker, bow reticle, trajectory ghost, OS cursor, debug line 중 실제 기준 하나만 선택해 문서화.
- 모든 화살 release 경로에서 displayed reticle center screen pixel와 ray start pixel이 동일 releaseId여야 한다.
- hit point의 수평/수직 plane 의미를 의도적으로 정한다. 자동 aim assist/aim snapping을 조용히 도입하지 않는다. 화살 speed/power/damage/ammo/gravity/collision은 이 작업에선 불변.
- launch vector/initial Rigidbody velocity/arrow render axis는 각기 계측한다. gravity 후 trajectory는 초기 조준과 분리한다.
- failure는 발사 취소로 나타내며 전방/center cursor fallback을 쓰지 않는다. 실패 원인은 diagnostic에 노출.

### 가스
- 최종 가스 기준은 실제 예시의 불규칙·다중 스케일·겹치는 투명 puff trail, 밝은 녹색 core + soft fringe, world-space 경로 보존과 age fade다. 단일 sphere, single follower, uniform alpha quad, pixel-cell spread는 실패.
- G 입력/장비 상태/물약 dose/fuel/cooldown/gameplay tick은 시각 효과 변경에서 독립한다. source effects update를 0.5초 현재 tick에서 변경하지 않는다.
- 가스 emission sample은 player 이동 거리/회전량/elapsed time으로 제어하여 정지 시 중복 동일 위치 과밀 폭증을 방지하고, 제자리 회전/급회전 시 연결 가능한 ribbon-like puff trail을 만든다.
- GameSetup, URP global asset/default pipeline/vendor AERO assets/MainScene 공유 renderer를 손대지 않는다. custom renderer는 isolated clone/test scene에서만 비교한다.
- `git add -A`, 사용자 소유 Screenshots/산출물 staging, 무관한 기존 dirty changes 취소, commit/push 금지.

## 4. 단계별 실행 계획 및 통과 게이트

### Gate A — “같은 release에서 실제로 무엇이 그려졌나” 데이터 캡처

**변경 전 진단 harness/test scene 준비.** Test_10/분리 씬의 기존 설정을 재사용하고 메인 씬에 전역으로 넣지 않는다.

한 화면 캡처 및 structured log의 단일 `shotId`에 포함:
- UI `BowAimReticleUTK.worldBound.center`/`panelPoint`, `PanelToScreen(root.LocalToWorld(...))` 또는 동등 변환으로 환산한 실제 screen center, raw `Mouse.current.position`, panel scale/display dimensions.
- current MainCamera instance id/name/rect/pixelRect/projection/cinemachine brain active camera; `ScreenPointToRay` origin/dir.
- `Physics.RaycastAll` 정렬 후 모든 근거리 hit(거리, collider, layer, root owner, trigger, IDamageable/terrain flags)와 기존 solver가 고른 hit.
- muzzle transform path/position, actor transform/forward, world aim point, normalized launch dir, `ArrowProjectile.Spawn` result velocity pre-FixedUpdate, t+1/2/4 frame positions/velocity/rotation and current visible arrow renderer/model source.
- उसी captured GameView cursor overlay, reticle, player bow, arrow, all debug blockers.

Test video: cursor을 화면 중앙·상/하·좌/우 가장자리와 8방향 격자에 두고 매 좌표 3발, 근/원거리 clear-ground, terrain/obstacle, collidable enemy 각. 클릭 후 최소 0.5초 같은 커서 위치 고정; GameView 30/60fps source 녹화. 1fps tiles는 final evidence에 쓰지 않는다.

**Gate A pass:** 조준 `screen center` pixel vs raw/ ray point 비교 오차가 수치로 분류되고, 한 release id에 모든 발사 경계값이 귀속됨. 불일치 경계를 특정하기 전 projectile solver, arrow speed, gas gameplay는 수정하지 않는다. 여전히 구별 안 되면 계측만 강화.

### Gate B — 실제 조준선 contract 선택 및 재현 테스트

원인 전제에 따라 수정 branch 하나만 수행:
1. **UI coordinate mismatch**: canonical screen point는 worldBound center를 Screen 좌표로 역변환한 값이다. `ScreenToPanel` input raw cursor와 실제 element center를 비교한다. Root transform/panelScale/targetDisplay/Y origin에 맞는 공식으로 배치하고, render callback 또는 `GeometryChangedEvent` 이후 저장한 actual center와 같은 center를 ray input으로 소비. UI Toolkit UI update와 PlayerCombat release 사이 stale sample/version는 shot snapshot immutable record (`shotId, screenPoint, camera, muzzle, ray...`)로 닫는다.
2. **Wrong ray hit**: 모든 hit sequence에서 잘못 선택된 가까운 blocker를 확인하면 `IDamageable` target click priority vs terrain intersection policy를 명시. shooter only filter만으론 충분하지 않다. Layer/QueryTrigger/target bounds/ground plane과 click intent에 따른 선택 순서를 문서화하고 Geometry tests를 만든다.
3. **Muzzle/parallax**: actual muzzle missing/incorrect origin이라면 serialized/reference attachment source를 확인. world aim point는 cursor ray와 일치하고 initial velocity은 muzzle-to-aim vector. Actor yaw change 후 child muzzle를 다시 읽지 않고 pre-rotation origin으로 발사하는 이유를 검증한다. If actor rotates but does no longer line up, compute final muzzle position from intended post-rotation pose before spawn while preserving animation/grip contract.
4. **Projectile/render/collision, not aim solver**: initial velocity vs ray passes but visible shot diverges, separately inspect `useGravity=false` + manual gravity, constraints, collision impulses, trail interpolation, body renderer state/GLB attachment/axis. Displayed trajectory direction in screen projection must be logged. Disable no gameplay collision unless root proven.
5. **different line producer/artifact**: identify visible streak/line owner via per-frame object logger/camera render and editor overlay. If it's debug gizmo/shadow/old projectile/impact, do not change bow vector to chase it.

TDD tests: (i) runtime round-trip panel center -> screen error at corners and scale 1.0/1440 ref/alt res, (ii) hit ordering with shooter + obstruction/target/ground, (iii) camera/muzzle replacement/disable, (iv) solver dir and actual spawned Rigidbody initial velocity alignment (<0.25° or agreed vector tolerance), (v) zero arrows/missing UI/camera no projectile. Source-string assertions supplement, never substitute geometric tests. Run targeted tests.

**Gate B pass:** exact screen point and screen ray agree; each of the ≥27 release vectors points from real muzzle toward chosen reticle world target; arrow initial velocity matches passed dir; next frames explain gravity/collision separately. Play screenshots/video and logs attached to QA record.

### Gate C — gas reference shot-by-shot deconstruction

Before effect changes, make image analysis report with controlled comparison:
- Extract the full 4.10s reference at 30fps in 1-second strips, preserve timestamp labels, crop plume+actor and sample representative region pixels over shadow/terrain for color/alpha contrast. The existing 12fps sheet produced a mistaken LOL/Singed attribution in a vision pass; correct it: the game/champion is unconfirmed from available evidence and is irrelevant. Only measured pixels and actor/world relative motion matter.
- Obtain 4 windows from user's 50s test at original fps around (a) pre-orange, (b) first orange, (c) orange spread, (d) after actor leaves. For each 30fps frame, record player bbox/position, orange/green/dark shadow region masks and bbox/area/centroid, shape solidity/edge roughness, RGB histogram, opacity proxy, motion/age.
- Contrast actual current gameplay frames on same unchanging green test floor with reference appearance. Confirm source path with timestamp logs: GasSprayer G-state/emitter count/alive count/tint/emission origins; GasCloudLauncher item event id/calls; debug/render overlays labels. Input log only once per state transition, not per frame.
- Explicitly label “not inferable”: exact particle architecture, world meters, intended input in clip, exact lifetime/stationary behavior unless visible; don't convert assumptions to fact.

**Gate C pass:** exact visible blocky orange event has a producer, and continuous G-held spray’s visual appearance is described separately with certainty level.

### Gate D — highest-quality candidate build/compare (isolated, not mainline first)

Current 3 sprites+3 low-rate particle systems already failed visual acceptance. No alpha tweak-only result is an acceptable deliverable. Build and compare minimum two candidate renderers in Test_10 isolated scene while same camera path executes:

**Candidate 1: authored high-end Shuriken / GPU particle trail with custom URP shader**
- Use pooled persistent emitters/particles, deterministic path samples on distance+time thresholds, sample position/heading from actual nozzle; separate fine micro wisps from dense cloud body. Persist particles in world space, not parent to moving owner.
- Replace repeated soft circular billboards with organically irregular authored RGBA density/flipbook masks plus animated low-frequency curl/noise breakup in a custom URP ShaderLab particle material; preserve true alpha/soft depth intersection, non-banded gradients, no square card borders, controlled hue/linear color and low overdraw. Use per-particle age/seed/size streams for irregular morphology/variation; newest puffs tight/darker, age widens/softens/fades, oldest reaches alpha 0.
- Add camera-facing but gently flattened ground-hugging clumps and small vertical wisps only as multiple visibly distinct density layers; break exact circles and obvious repeated tiling via noise/rotation/scale phase. Solid core must remain translucent enough to see environment.
- Emission rate by velocity/distance plus low stationary rate while spraying; clamp max active particles and max total world segments; no per-frame allocation. Stop prevents new emission; existing world segments fade then pooled cleanup. Turning leaves curved world trail.

**Candidate 2: VFX Graph only if installed and deployable in isolated renderer**
- Confirm manifest/PackageCache and renderer compatibility first. Use GPU event-driven strip/puff trail with recorded world positions and age. If Graph is absent or cannot build reproducibly, don't spend task on hand-authored .vfx; use custom ShaderLab/URP particle route. No global AERO mutation.

**Optional Candidate 3: localized density-volume renderer**
- Existing `AeroLocalizedGasVolume` models one moving volume, has no historical polyline. Only evaluate if it can accept bounded persistent path segments/samples and performs acceptably in cloned renderer. A single player-follow volume fails the reference trail contract.

Create a same-camera side-by-side acceptance sheet/video with candidate IDs hidden until review if practical. For each candidate: stationary, straight 4m, S-turn, sharp 180°, sprint, walk, stop/release, re-start over old trail, shadow transition. Compare plume envelope width, occupied pixel area, foreground opacity, edge texture entropy, median hue, visible density around feet, path error to player trajectory, fade duration. Numeric metrics are screening only; visual side-by-side still required.

**Gate D visual pass criteria:** (a) curving 3D/world path stays where laid, not a rectangle, (b) multi-scale irregular bright green puff/wisp, (c) newest/aged distinction, (d) trail fades after stop, (e) environment visible through plume, (f) no flat geometry or visible quad borders, (g) reference-quality close enough on game background by user/reviewer capture. Rejected candidate remains isolated and is removed, not silently mainlined.

### Gate E — integration, deterministic gameplay invariants, regression

- Integrate only chosen visual candidate to existing G input/state event owner. Keep `ApplySprayEffects()` current 0.5s cadence, dose time, canister fuel, effect tick target range, poison/heal/buff/mask contract unchanged.
- Fixed-time automated test advances visual spawn rate while same gameplay state clock is controlled; assert exact same effects tick count, dose/fuel totals, once-only transition/UI events. Assert max alive puff/segment caps, world-space older positions unchanged when source moves, deterministic teardown/material release, no leftovers after reload/unequip/scene transition/focus loss.
- Add runtime diagnostic summary per start/stop/reload with active plume segment/system count, max bound, tint/id, sequence; throttle once per start/stop, no per-frame spam.
- Fix the stale visible-block producer separately if it is another owner; don't repurpose `GasCloudField` radial consumable mechanics unless the measured video says that is the event being judged.

### Gate F — verification order and delivery

1. Write behavioral tests before implementation, observe expected RED, then patch only the proven root cause.
2. Run focused geometry/projectile tests and gas functional/render-data tests.
3. Run compile with editor closed, inspect fresh `compile.log` for CS=0 and successful shutdown.
4. Run full EditMode, parse NUnit root `<test-run total/passed/failed>` and list failure `fullname`/messages. Current baseline is 432/423/9 at last recorded run; rerun against the new exact tree.
5. Play/capture at 30/60fps with same-release logs. A successful compile, test, or editor startup is not visual acceptance.
6. Independent code-review agent after implementation; because prior runs had tool timeouts, parent inspects status/diff and results rather than treating delegation success as artifact verification.
7. Update `/mnt/c/Unity/code/docs/QAPROGRESS.md` with exact implementation/test/Play evidence, failure list, unresolved risks. Update ROADMAP only for a named phase. No commit/push absent explicit user request.

## 5. Evidence, ownership, and edit scope

Relevant current paths:
- Aim: `Assets/Scripts/Systems/PlayerCombat.cs`, `ArrowManager.cs`, `BowAimState.cs`, `ArrowProjectile.cs`; UI: `Assets/Scripts/UI/Toolkit/BowAimReticleUTK.cs`, root transform path `UIToolkitBootstrap`/`PanelSettings`.
- Continuous spray: `GasSprayer.cs` + `GasSprayerController.cs` + `SprayInputHandler.cs`.
- Deployed/consumable cloud: `GasCloudLauncher.cs` + `GasCloudField.cs` + `GasCloudFieldEffect.cs`.
- Optional isolated path: `AeroGasSprayRuntimeHost.cs`, `AeroLocalizedGasVolume.cs`, `AeroLocalizedGasRendererFeature.cs`, `AeroLocalizedGas.shader`, `AeroIsolatedRendererSession.cs`.
- Current Unity compile known-good and EditMode known 432/423/9; new tests related paths passed in last full run, but neither indicates visual quality. Fresh root QA progress: `docs/QAPROGRESS.md`.

**Before future implementation:** build exact modified-file allowlist from git status and diff; preserve other dirty/untracked files. Initial scope is measurement harness, relevant tests, then only files needed by diagnosis; candidate FX assets go in isolated Resources/Test scene with unique `.meta` GUIDs. No bulk `git add -A`.

## 6. Current request status

This plan is a deeper replacement proposal. In this response's task, only video inspection and plan writing are performed. No source/scene/texture/renderer settings are changed and no compile/test/Play is claimed.
