# 창 클릭 콤보 · 활 조준/UI · 고품질 가스 VFX 구현 계획

> **실행 상태:** 사용자가 전체 구현을 승인하고, 세 요청이 끝날 때까지 멈추지 말고 계속 진행하라고 지시했다. 구현은 Phase별로 진행하며 별도 요구가 없는 한 사용자 기존 변경을 보존하고 커밋/푸시는 하지 않는다.

**목표:** 창은 첫 클릭에 찌르고 후속 클릭에 검 콤보와 같은 입력 버퍼/승인 규칙으로 후속 휘두르기를 재생하며, 활은 UTK 조준 위치에서 화살이 정확히 발사되고 화살 HUD를 기존 Fluent 스타일로 다듬고, 가스 분사는 참조 영상처럼 플레이어 이동 경로에 고품질·유기적·시간에 따라 소산하는 가스 궤적을 남긴다.

**설계 개요:** 창은 검의 `MeleeComboStateMachine`/accepted-click 파이프라인을 재사용하되 무기 타입별 첫/후속 모션과 한 타당 hit owner를 분리한다. 활은 UI가 사용하는 동일 screen point → 카메라 ray → 월드 target point → 실제 활 muzzle 방향 흐름을 단일 소스로 유지하고, 조준 UI를 Fluent tokens로 정리한다. 가스는 시각 효과와 gameplay 판정을 분리한다. 실제 VFX Graph/GPU particle 후보와 URP-safe fallback은 분리 씬에서 검증 후에만 통합한다.

**기술 스택:** Unity 6000.4.10f1, URP 17.4.0, Input System, UIToolkit, Unity VFX Graph 17.4.0 / Shader Graph 17.4.0 사용 가능(설치 확인), C# ParticleSystem/URP ShaderLab fallback.

---

## 현재 코드 근거 및 미확정 사항

### 창/검
- `Assets/Scripts/Systems/PlayerCombat.cs`: `TryAttack()`의 콤보 분기는 현재 Fist/Sword만 대상으로 한다. Spear는 기존 legacy 근접 피해 경로를 타고, 별도 `Attack` trigger 감시가 `HumanoidClipDriver.cs`에 있다.
- `Assets/Scripts/Systems/HumanoidClipDriver.cs`: 검 콤보는 accepted attack sequence를 최대 3클릭으로 받고 단일 `WeaponCombo` 0.85초 후속 창, 구간별 타격, 입력 버퍼를 관리한다.
- `Player_AC.controller`에 `AttackThrust` trigger/state가 있다. 조사 결과 `AttackThrust`는 실제 thrust FBX `Thrust_Slash`와 연결되어 있지만, 기존 창 입력 분기는 `Attack`을 트리거하고 있어 해당 찌르기 상태에 도달하지 못했다. 후속 휘두르기 후보는 `Weapon_Combo_2`로, `WeaponCombo` 상태에 연결된다.
- 전용 `Spear_Attack`/`Spear_Followup_Swing` 애니메이션은 확인되지 않았다. 이번 진행에서는 기존 `AttackThrust` + `Weapon_Combo_2` asset을 매핑하여 1회 찌르기 후 1회 후속 휘두르기를 시도한다. 콤보 계약은 우선 “첫 클릭 찌르기, 두 번째 승인 클릭 1회 휘두르기”로 제한한다. Sword의 3타 시스템은 유지하며 창의 세 번째 클릭은 현재 설계상 거부한다. 후속 검증 중 클릭 3 설계 변경 필요가 발견되면 임의 확장하지 않고 보고한다.

### 활 조준/화살 UI
- `ArrowManager`에 `GetAimScreenPoint()`, `GetArrowSpawnOrigin()`, `TrySolveAim()`가 있고, `PlayerCombat.TryBowShot()`는 solver 방향으로 화살을 발사한다.
- `BowAimReticleUTK.UpdatePosition()`도 같은 `GetAimScreenPoint()`를 참조하나 자체 screen→panel 배율 변환을 한다. 이 변환의 multi-resolution/PanelSettings 정합성과 실제 화면 정렬은 미검증.
- `BowTrajectoryPreview`는 의도적으로 제거/비활성화된 legacy façade이므로 노란 궤적선을 복원하지 않는다.

### 가스 분사/VFX
- `Screenshots/가스 살포 예시.mp4`: H.264, 984×556, 30fps, 약 4.10초. 주요 프레임에서 황록/독성 녹색 구름이 캐릭터 뒤 이동 경로에 world-space로 남고, 곡선 경로를 이루며, 새 부분은 상대적으로 진하고 오래된 부분·외곽은 넓고 투명하게 확산·소산하는 모습이 관찰됐다. 정확한 월드 단위·물리 opacity는 영상만으로 확정되지 않는다.
- 현재 `GasSprayer.SpawnFogEffect()`는 분사 중 랜덤 위치 빌보드 Quad를 만들며 Material도 매 생성마다 만든다. `GasCloudField`는 별도 물약 사용 시 생성되는 stationary radial cloud이므로 G 지속분사 path trail과 혼동하지 않는다.
- `AeroLocalizedGasVolume/RendererFeature/AeroLocalizedGas.shader`는 project-owned fullscreen custom compositor의 isolated experiment이며, 활성 기본 MainScene renderer에는 연결되어 있지 않다. 단일 follow volume이므로 이동 후 잔류하는 곡선 path 요구를 곧바로 충족하지 않는다.
- G는 `GasSprayer.Update()`가 InputSystem으로 읽고, `SprayInputHandler`는 별도의 Mouse1 경로를 가진다. Gameplay tick/dose/fuel 처리와 visual emission은 분리 유지해야 한다.

---

## Phase 0 — 변경 없는 기준선·자산·배선 감사 ✅

### 확인 결과
- **창**: 기존 창 분기는 콤보 미지원, `Attack` trigger mismatch 확인. 유효 opener는 `AttackThrust`/`Thrust_Slash`, follow-up candidate는 `WeaponCombo`/`Weapon_Combo_2`.
- **활**: reticle와 shot solver가 동일 screen point source를 공유하지만 UTK panel 변환과 projectile gravity 후 실제 impact 정렬은 미검증.
- **가스**: MainScene `GameSetup`이 `GasSprayerController/GasSprayer/SprayInputHandler`를 연결. G key reader는 `GasSprayer.Update()` 1곳, Mouse1은 별도 보조 입력. 일반 visible path는 random billboard Quad. 실제 기본 URP renderer는 SSAO만 사용, AERO local feature는 별도 격리 실험 경로.
- 기존 tree에 광범위한 dirty/untracked 변경이 있다. 이번 요청 외 파일/자산을 정리하거나 되돌리지 않는다.

**Phase gate:** 수정 전 자산과 연결 흐름을 확인했다. 창은 기존 clip candidates를 재사용 가능; 창 전용 후속 전용 clip은 부재.

## Phase 1 — 창 콤보 동작 명세·입력 수락 및 타격 소유권

### 계약
- 클릭 1: Spear 전용 찌르기, 드라이버 소유 hit attempt 1회.
- 0.85초 follow-up window 내 클릭 2: WeaponCombo의 두 번째 segment를 이어서 재생하고 hit attempt 1회.
- Sword/Fist는 기존 1~3 accepted clicks/segments 그대로 둔다. Spear는 2 total clicks만 허용한다.
- accepted input은 sequence를 한 번만 증가시키며, 마우스 홀드는 새 클릭을 만들지 않는다. 쿨다운을 통과하지 못한 입력은 stage를 소모하지 않는다. follow-up은 Sword처럼 일반 cooldown을 우회한다.
- Driver가 준비되지 않은 Spear는 기존 legacy immediate damage fallback을 유지. Ready driver가 있으면 즉시 PlayerCombat hit path는 skip하고 animation marker가 hit을 소유한다.

### 구현
- `PlayerCombat.TryAttack()`에 spear ownership/readiness gate 및 follow-up acceptance/restart gate 추가.
- `HumanoidClipDriver`에서 spear stage 1은 정확히 `AttackThrust` trigger, stage 2는 `WeaponCombo` state의 normalizedTime 1/3에서 시작하여 두 번째 segment marker에서 hit을 실행한다.
- AcceptedAttackSequence는 같은 driver Update에서 먼저 opener/후속 순서로 소모될 수 있으므로 pending sequence를 반영한 capacity/time gate를 사용.
- `ResolveComboSwingHit(stage)`의 combo-owned hit path는 기존 jitter 억제 정책 적용; non-combo hit/recoil/lunge는 유지.
- Animator state transition interruption, expiry, weapon switch, hit marker replay를 별도 처리한다.

### TDD/테스트
- 대상: pending opener와 same-frame click, valid/expired follow-up, 2-click cap, callback/hit idempotence, delayed dispatch, opener restart, animation interruption, no-ready-driver fallback, Sword/Bow isolation.
- compile CS=0, focused EditMode XML의 실제 totals 확인. `run_tests.sh editmode`만으로는 결과 XML/exit verdict가 부정확할 수 있으므로 explicit `-runTests -testPlatform EditMode -testResults C:/Unity/code/TestOutput/...xml -logFile ...` 실행과 XML 확인을 병행.
- Test_10 Play: click 1 thrust + delayed single hit, click 2 slash + delayed single hit, cooldown rejection, hold/no fabricated input, empty click expiry. Animator state/motion/normalized time/hit logs와 실제 target HP를 맞춘다.

## Phase 2 — 창 구현 품질/Play 회귀
- Phase 1의 독립 review에서 opener restart(self-transition), accepted follow-up re-gating, interrupted opener cleanup, stale cooldown flag 등을 검토해 해결한다.
- UI interaction block, parry queue, weapon switch와 Spear driver owner 중복 확인.
- Sword/Fist combo baseline regression 및 Bow route isolation.
- Play에서 다음 확인: 1회 입력은 thrust 한 번, 후속 클릭은 swing 한 번, stage별 hit attempt·HP delta가 정확히 한 번, 공격 후 플레이어 transform 떨림이 없는지.

## Phase 3 — 활 UTK 조준점에서 발사/착탄 정렬
- `ArrowManager.GetAimScreenPoint()`을 reticle 및 solver 단일 source로 유지하고, 가능하면 `RuntimePanelUtils.ScreenToPanel`/Unity 지원 API로 panel coordinate conversion을 교정한다. PanelSettings reference resolution/scale, screen y flip, DPI, root worldBound, multi-resolution/ratio를 실제 render panel에서 검사한다.
- release frame에 reticle position, panel coords, camera ray, obstruction/world aim point, actual muzzle, launch vector, projectile velocity, impact point를 bounded diagnostic sample로 기록한다.
- Projectile gravity means muzzle launch vector to aim point does not necessarily equal final impact; if user-visible reticle means aim direction use same target point, if intended impact point then solve ballistic compensation using current speed/gravity and test. No invented assumption.
- EditMode pure geometry cases center/edge/near/far/high/low/self-hit exclusion + Play camera with multiple aspect ratios, marker/arrow impact comparison. Keep ammo/damage/lifetime invariants.

## Phase 4 — 활 reticle와 arrow HUD Fluent
- `BowAimReticleUTK.cs`에서 shared `UTKTheme` tokens, Fluent-compatible type/count badge/cards, empty/low ammo states, semantic tier colors, focus overlay `PickingMode.Ignore`, release fade and DPI/resolution remain.
- No IMGUI or flat primitive replacement. Reuse high-quality existing baked dots/corners; if assets need updates, verify alpha/readability imports and visual screenshot.
- UI tree tests + screenshots at varied resolution/PanelSettings; verify no cursor/target occlusion and cursor click-through.

## Phase 5 — 참조 영상용 가스 VFX 연구 씬

### Visual target
- Nozzle/back area emission; residual world-space path not following player.
- Curved path follows travel and heading change, soft cloud lobes with noisy edges; dense fresh area, wider/lower opacity older tail, smooth age fade.
- Ground-hugging gas plus lighter rising wisps; depth/geometry occlusion and indoor/outdoor/terrain slope.
- Vivid toxic lime-green semantic tint, controllable emission and scene visibility without full-screen green tint.
- No giant planar camera billboards as finished result.

### Candidate prototype sequence
1. Measure existing 4.1 sec reference at fixed frame timestamps, recreate neutral test scene & player path driver.
2. GPU ParticleSystem layered prototype: soft base, billowing puffs, fine wisps, pooled materials/objects; emission follows interpolated travel distance, world-space and age attributes.
3. VFX Graph 17.4 GPU particle prototype with explicit URP output; verify asset import, VisualEffect alive count, and actual output pixels separately.
4. If visual blending/depth demands it, compare bounded depth-aware localized compositor in isolated cloned renderer only. It is a candidate, not presumed final.
5. Compare matched camera frame sheets, turn/stop/teleport/hitch/slope/doorway; profile CPU/GPU, overdraw, allocations, memory and 30/60/120fps path continuity.
6. Select the highest-quality path that visibly works with recorded profile, retain ParticleSystem/ShaderLab fallback. A package/import/alive counter alone is not a visual pass.

## Phase 6 — G spray lifecycle & gameplay integration
- Keep a single authoritative physical G hold owner. Mouse1 remains separate only if existing behavior is intended; no conflicting start/stop semantics.
- Separate path VFX sample rate from gameplay 0.5s effect tick, dose/fuel drain, and reload. VFX frame count must not change damage/resources.
- Release stops new emission; world-space residual fades by age. Resume/unequip/depletion/focus loss/scene teardown cleanly stop emitters and no stale visual tails beyond chosen lifetime.
- Preserve distinct `GasCloudField` potion-use area cloud and effect logic. Integrate exactly one visual emitter path in a tested scene; retire old billboard producer there only after new renderer visibly works.
- Test resource accounting, masks, effects tick dedup, lifecycle, Play path movement and screenshots/profiler.

## Phase 7 — Integration and final gates
- Integrate into isolated MainScene-clone test scene per project convention; don't hand-edit scene YAML. Shared URP renderer, vendor AERO package, GraphicsSettings and QualitySettings untouched unless independently approved; any test session restores temporary settings.
- Every source change: independent spec/quality review, fresh compile CS=0, focused EditMode current XML/results, relevant suite comparison, Play screenshot/video and Editor.log. Report compile/EditMode/Play evidence separately.
- Preserve unrelated dirty/untracked files, do not `git add -A`, do not commit/push without explicit request. Only when completing implementation phases update QAPROGRESS/ROADMAP per established project workflow.

### Completion checklist
- [ ] Spear one click thrust and exactly one hit attempt.
- [ ] Spear second accepted click follows with one swing and exactly one hit attempt; third total click rejected.
- [ ] Sword/Fist and Bow behavior unaffected; non-combo hits unchanged.
- [ ] Bow reticle/panel coordinates/shot vector/impact behavior demonstrated across aspect ratios, near/far.
- [ ] Arrow HUD visually Fluent, tier and count/empty states legible, click-through and release fade preserved.
- [ ] Gas plume is high-quality, world-space path, curved, layered and age-dissipating against the supplied reference; real renderer output visually verified.
- [ ] G release, dose/fuel, reload, depletion, mask and gameplay effect cadence correct and independent of VFX FPS.
- [ ] Fresh compile, focused and relevant regression EditMode, Play visual/runtime evidence documented separately.
- [ ] QAPROGRESS.md and applicable ROADMAP statuses/log updated; no commit/push without request.
