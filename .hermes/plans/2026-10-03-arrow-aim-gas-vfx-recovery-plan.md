# 활 조준 방향 재발 + 가스 플룸 고품질 재구축 계획

> **상태: 계획 전용.** 사용자 요청은 “계획해줘”이므로 이번 단계에서는 게임 코드, 테스트, 씬, 텍스처, 렌더 설정, `QAPROGRESS.md`를 수정하지 않는다. 구현 승인을 받은 것으로 해석하지 않는다.
>
> **프로젝트:** `/mnt/c/Unity/code` · Unity `6000.4.10f1` · URP 17.x
> **참조 영상:** `Screenshots/가스 분사 테스트.mp4`, `Screenshots/화살 모션 테스트.mp4`, `Screenshots/가스 살포 예시.mp4`

## 1. 이번 요청에서 확인된 증거와 한계

### 1.1 새 테스트 영상

`Screenshots/가스 분사 테스트.mp4`는 1240×828, 30fps, 약 50초다. 1초 간격으로 만든 5×5 콘택트 시트에서 다음을 확인했다.

- 청록색 사각 표식과 작은 `x…` 라벨은 화면상 조준 UI로 보이며, 위치가 프레임에 따라 이동한다.
- 흰 선형 흔적은 일부 발사 장면에만 나타난다. 단일 콘택트 시트로는 화살촉/깃이 식별되지 않으므로 이 흔적을 모두 화살이라고 단정하지 않는다.
- 약 13초 부근 프레임에서 조준 표식은 플레이어 오른쪽, 흰 선형 흔적은 왼쪽 방향으로 보인다. 이는 사용자의 “한 방향으로 날아가는 것 같다”는 관찰과 부합하지만, 1fps 표본만으로 발사체 오브젝트/탄속/충돌을 확정할 수 없다. 수정 후엔 연속 프레임과 동일 발사 이벤트 로그를 묶어 재판정해야 한다.
- 약 21초 이후 주황색 픽셀 블롭이 나타난다. 플레이어가 움직여도 일부 블롭이 이전 위치에 남고, 뒤이어 별도 크기/위치의 블롭이 생긴다. 관찰상 원하는 라임색 연속 잔류 플룸이 아니다. 다만 이 출력이 `GasSprayer` 지속 방출인지 물약 사용 시의 `GasCloudField`인지 영상만으로 확정하지 않는다.

`Screenshots/가스 살포 예시.mp4`는 984×556, 30fps, 약 4.10초다. 직접 확인 가능한 목표는 독성 녹색의 불규칙하고 부드러운 구름, 이동 궤적에 남는 여러 밀도 층, 새 흔적은 진하고 오래된 흔적은 넓어지며 희미해짐, 이동 경로 곡률 보존이다. 영상 픽셀에서 게임 월드 미터/정확 수명/알파 값을 역산하지 않는다.

### 1.2 현재 소스 경로에서 확인한 사실

- `BowAimReticleUTK.UpdatePosition()`과 `PlayerCombat.TryBowShot()`은 `ArrowManager.GetAimScreenPoint()`를 사용한다. `TryBowShot()`은 활성 `MainCamera`를 발사 때 재조회하고, 카메라/solver 실패 시 취소하도록 최근 변경됐지만 새 테스트 영상에서는 불일치가 다시 관찰됐다.
- `ArrowManager.TrySolveAim()`은 화면 ray의 첫 비사수 충돌점을 aim point로 택하고, 실제 origin에서 aim point를 향한 방향을 만든다. 충돌이 없으면 ray 위 100m 지점을 쓴다. `ArrowManager.TryShootArrow()`는 이 방향으로 발사체 초기 속도를 설정한다.
- 따라서 “solver가 있다/같은 마우스 API를 쓴다”는 것으로 화면의 표식과 실제 발사 벡터가 같다고 증명되지는 않는다. 남은 후보는 (a) 화면↔패널 좌표계/표식 중심 오차, (b) 마우스 표본과 발사 release 시점의 불일치, (c) 카메라/ray가 고른 충돌점 또는 muzzle 원점 오차, (d) 발사체의 실제 방향·속도 적용, (e) 표식/흰 흔적이 서로 다른 발화 경로인 경우다. 증거로 분리하기 전 어느 하나를 원인으로 확정하지 않는다.
- `ArrowProjectile`은 생성 시 받은 방향을 Rigidbody 초기 속도로 설정하고, 프레임마다 속도 방향을 따라 축 정렬한다. 중력이 별도로 적용된다. 화면상 초기 발사선과 이후 곡선/착탄은 구분할 것.
- 지속 가스 경로 `GasSprayer.EmitPlume()`는 플레이어 자식 emitter의 3개 ParticleSystem과 `GasPlumeSoft/Lobe/Wisp` 마스크를 쓴다. 파티클은 World simulation이나, 새 텍스처와 단순 파티클 조정 후에도 사용자가 여전히 저품질이라고 판정했다.
- 별도 `GasCloudLauncher`→`GasCloudField.Spawn()`는 물약 사용에 따른 방사형/독립 구름 경로다. 이 경로와 지속 `GasSprayer` 경로가 구별되지 않으면 영상의 주황색 패치 원인을 잘못 고칠 수 있다.
- `AeroGasSprayRuntimeHost`/`AeroLocalizedGasVolume`은 한 개의 player-follow 볼륨을 갱신하는 선택형 renderer host이며, 과거 계획에서 이동 경로의 누적 history를 제공한다고 확인되지 않았다. 활성 MainScene 연결도 이 계획 작성 시점에 입증되지 않았다.

### 1.3 기존 QA 상태

최근 기록된 전체 EditMode 결과는 431개 중 422 통과/9 실패다. 실패는 AERO expected-log, AttackTargetRing EditMode `DontDestroyOnLoad`, 요리 카탈로그 기대 760 대 실제 2024, CastleInteriorTopology 4건, RecipeCatalog unknown-combo, ThemeUss baked-PNG 기대다. 이는 이번 재발의 원인이나 baseline으로 단정하지 않는다. 이전 compile은 CS1612를 로컬 ParticleSystem 모듈 변수 방식으로 고친 뒤 성공 기록이 있다. 이 계획의 구현 승인 후 최신 트리를 다시 검증한다.

## 2. 범위와 불변 계약

### 활

1. release 때 실제 UTK 조준 표식의 중심 pixel이 가리키는 화면 ray를 기준으로 한다.
2. 같은 발사 샘플(화면 좌표, 카메라, muzzle, ray hit/aim point)을 표시와 projectile이 소비해야 한다. “UI는 폴링 시점, 발사는 다른 fresh sample”처럼 release 한 프레임에 값이 갈라지는 것을 허용하지 않는다.
3. 화면 중앙/각 모서리/양 측면/여러 해상도에서 UI reticle center와 실제 hitpoint/초기 발사선 관계를 측정한다. click 위치에 표식이 그려지는 의미(커서점인지 world-target marker인지)를 코드를 따라 확정한다.
4. 발사 실패 시 `transform.forward`/임의 축으로 조용히 대체하지 않는다. 명확한 취소 또는 동일한 새 aim sample로 재시도한다.
5. aim vector와 중력 적용 뒤 비행 곡선/impact를 별도 기록한다. 사용자가 지시하지 않는 자동조준·탄도 명중 변경은 금지한다.
6. 탄약, 데미지, 파워, 사거리, 무기 상태/애니메이션, FX를 별도 승인 없이 바꾸지 않는다.

### 가스

1. 최종 동작은 플레이어 뒤/노즐에서 방출되고 플레이어의 실제 이동·회전을 따라 **경로에 계속 잔류**해야 한다. 새 구간은 진하고 오래된 구간은 넓어지며 투명해지고, 방출을 중단하면 기존 구간만 수명 기반으로 소산한다.
2. 레퍼런스 인상: 밝고 유독한 라임/녹색, 불규칙한 부드러운 덩어리와 얇은 wisp 디테일, 서로 겹치는 깊이 층, 지면/배경이 완전히 가려지지 않음, 정지·회전·재출발 때 연속된 trail.
3. 단일 follower 덩어리, 사각/픽셀 패치, 균일 동그라미 반복, 화면 전체 녹색 틴트는 실패다.
4. G 입력의 authoritative owner/state는 하나로 유지한다. 가스 VFX sample frequency는 gameplay effect tick(기록된 0.5초), 물약 dose, canister fuel과 독립이어야 한다. 표본률 변경으로 피해/회복/버프·자원량이 달라지면 실패.
5. Mouse1 보조 입력을 보존할지, G와 병행 시 상태가 어떻게 되는지는 기존 계약/현재 실행 경로를 조사하고 구현 전에 문서화한다.
6. 품질 계층(예: Basic/High) 또는 플랫폼 스케일링을 고려하되, 저품질 폴백이 사용자가 승인한 시각 기준 아래면 “완료”로 대체하지 않는다. Vendor AERO/공용 URP 설정/MainScene 공유 renderer 변경은 하지 않는다.

## 3. 실행 계획 (구현 승인 이후)

### Phase 0 — 정확한 영상/발사 이벤트/활성 경로 식별

**목표:** 새 영상의 불일치를 같은 발사 이벤트로 재현하고, 실제 조준 UI·화살·가스 효과 owner를 특정한다.

**대상:** `PlayerCombat.cs`, `ArrowManager.cs`, `BowAimReticleUTK.cs`, `BowAimState.cs`, `ArrowProjectile.cs`, `GasSprayer.cs`, `GasSprayerController.cs`, `SprayInputHandler.cs`, `GasCloudLauncher.cs`, `GasCloudField.cs`, 해당 Unity bootstrap/Test scene 경로. 계획 밖 파일은 읽기만 한다.

1. 새 50초 영상에서 조준표식이 보이는 발사 장면을 원본 FPS(30fps)로 발사 전후 연속 캡처하고, 화면좌표/pixel, cursor, cyan marker, muzzle, 화살 body/trail, 표적을 동일 프레임에 기록한다. 1fps 콘택트 시트는 재발 신호이지 픽셀 타이밍 판정이 아니다.
2. `TryAttack → BeginBowDraw/ReleaseBow → BowAimState → UTK scheduled Update → TryBowShot → GetAimScreenPoint → camera ray → Physics hit → muzzle direction → ArrowProjectile.Spawn → Rigidbody velocity` 경로마다 한 개의 sequence/event id와 로그를 연결한다.
3. release 시 `Mouse.current` 유무/좌표, reticle `worldBound.center`의 screen conversion, panel/display/camera identity, `ScreenPointToRay` ray, 첫 충돌 collider 이름/point, shooter child 제외 결과, muzzle origin, 최종 normalized dir, projectile velocity를 기록한다.
4. 모든 화면선/표식 producer(UTK, cursor overlay, legacy preview, test overlay, gizmo/trajectory preview)를 호출부 grep과 Play 화면으로 식별한다. 흰 흔적이 실제 화살인지 다른 impact/swing effect인지 구분한다.
5. 지속 가스 owner `GasSprayer.EmitPlume()`가 실제 호출되는지, `IsSpraying/IsEquipped/IsReloading`, loaded potion type, 각 pool의 emitting/alive, tint, nozzle world pose를 같은 시각 로그에 남긴다. 별도로 consumable `GasCloudLauncher`→`GasCloudField.Spawn()` 호출을 기록해 영상의 주황 패치 출처를 판별한다.

**Gate 0:** 표식 화면 pixel, release sample, solver world point, projectile velocity 사이에서 처음 값이 갈라지는 경계가 식별됨. 기원을 특정할 수 없으면 추측 코딩 대신 다음 Play 재현용 계측/질문만 제시한다.

### Phase 1 — 활 조준 표본을 단일화하고 UI/발사 변환을 수리

**목표:** 실제 표시 UTK 표식의 중심과 release가 사용한 ray가 같은 화면 pixel에서 시작한다.

**테스트 우선:** `BowAimAlignmentTests.cs`에 좌/우/상/하/중앙 및 가장자리 cursor cases, center+edge aspect/resolution transform, same-release sample identity, camera 교체/비활성, null mouse/camera fail-safe를 추가한다. solver 결과를 실제 projectile 초기 velocity와 비교하는 seam을 둔다. source-string assertion만으로 화면 정렬 통과 판정을 하지 않는다.

**진단에 따른 단일 원인 수정:**
- UI Toolkit transform에서 `ScreenToPanel` + `WorldToLocal`로 계산한 실제 reticle center를 `RuntimePanelUtils.PanelToScreen` 또는 동등 역변환으로 다시 screen pixel로 변환해 입력점과 비교한다. 문서/런타임 정의에 맞춰 Y origin/scale/display 차이를 교정한다.
- 마우스/카메라/muzzle/aimpoint를 release edge에서 한 번 스냅샷화하고 `BowAimState` 또는 Systems의 공유 immutable sample을 통해 UI 표시와 firing이 동일 sequence를 소비하도록 한다. 16ms UI schedule이 오래된 cursor를 보여주는 것이 원인일 경우 UI는 다음 렌더 update에서 최신 sample로 즉시 동기화한다.
- solver ray hitpoint는 무작정 “첫 collider”로 쓰지 않는다. shooter hierarchy 및 무효/trigger/비시각 테스트 collider를 배제하고, 유효 terrain/target semantics를 검증한다. unobstructed fallback plane/ray distance는 사용자-facing display semantics와 일치시키고 근거리/원거리 및 탑다운 parallax에서 시험한다.
- 현재 발사 시 `TrySolveAim` 실패를 이미 취소하는 fail-safe는 유지한다. target-point to muzzle direction이 `ArrowProjectile.Spawn`과 RB 초기 속도까지 변형되지 않음을 테스트한다.

**Gate 1:** 서로 다른 8개 이상의 cursor screen point에서 실측 reticle-center error가 사전 합의 tolerance 내(기본 계획값 ≤2 pixels, 실제 해상도/표식 크기 확인 후 고정), 실제 launch initial velocity가 동일 ray aim point 방향과 tolerance 내, 16:9 + alternate aspect ratio, null/stale camera 취소/재획득이 로그로 검증됨. gravity 후 curve/impact는 별도 표로 보고한다.

### Phase 2 — 독립 활 Play acceptance

- 고정 플레이어/muzzle와 화면 격자(센터/네 모서리/좌우), 근거리/원거리 표적에서 각 점을 2회 이상 discrete release한다.
- 화면 녹화에 OS cursor와 UTK reticle, projectile body, target, 발사 origin을 함께 남긴다. 같은 실행 `Editor.log`의 sequence id/camera id/ray hit/muzzle/dir/velocity와 대조한다.
- 왼쪽 조준은 왼쪽, 오른쪽 조준은 오른쪽, 상/하 입력은 해당 ray/표적 의미에 맞는 벡터를 내야 한다. 여러 aim sample이 모두 한 쪽으로 나오면 즉시 Gate 실패, 탄착/데미지 최적화로 덮지 않는다.
- 낮은 표본률/화살 투명도와 조준 오차를 혼동하지 않도록 별도 visual trail/body evidence 확보. 현재 사용자 요구는 화살이 UTK 방향으로 날아가는 것; projectile art 재설계는 범위 밖.

**Gate 2:** 같은 release에서 reticle pixel ↔ ray ↔ muzzle 초기 발사선이 화면 녹화와 수치 로그로 일치. Gate 0–2 통과 전 bow fix 완료 주장 금지.

### Phase 3 — 지속 가스 경로 재발 원인 분리 + 렌더 후보 구성

**목표:** pixel-edged orange patches가 어떤 owner에서 나왔는지 증명하고, reference와 같은 이동 trail을 위한 후보를 별도 비교한다.

**대상:** 기존 `GasSprayer.cs`, `GasSprayerController.cs`, `SprayInputHandler.cs`, `GasCloudField.cs`, `GasCloudLauncher.cs`, 새 mask assets, 기존 `AeroGasSprayRuntimeHost.cs`/`AeroLocalizedGasVolume.cs`/`AeroLocalizedGas.shader`, isolated test scene setup. Dirty source 변경은 정확한 baseline snapshot과 allowlist를 만든 후 파일별로 분리한다.

1. 새 영상의 21–50초에서 cyan/white overlays와 orange gas 위치를 원본 해상도로 더 추출하고, 캐릭터 world pose/카메라 이동 변화도 대조한다. 변하지 않는 patch가 world anchor인지 camera/screen artifact인지 구분한다.
2. `GasSprayer.EmitPlume`와 `GasCloudLauncher`를 각각 별도 switch/diagnostic tag로 독립 재현한다. 연속 G hold test는 기존 물약사용 radial field와 분리한다. 같은 시간·카메라·이동경로로 before captures를 저장한다.
3. 새 mask가 연결됐더라도 화면에서 너무 희미하거나 반복 원형으로 보일 수 있으므로, pixel-level alpha/RGB와 실제 imported material blend/sRGB/alpha 설정을 맞춘다. mask asset proof는 rendered scene proof를 대체하지 않는다.
4. 이동 trail candidate A: bounded object/particle pool에 distance + max-time interpolation으로 경로 샘플. 방향은 nozzle rear/heading/movement velocity 기반. 정지, 제자리 회전, 급회전, frame hitch, 재시작 시 이전 segment를 옮기지 않고 world-space 보존. 늙은 puffs scale up/soften/fade; 신규 밀도는 emission point에 집중.
5. 품질 표현을 “몇 배” 올릴 layered stack으로 제작한다: large soft base volume, organic lobed cloud breakup, fine curling wisps, subtle inner bright/lime density pockets, age-based width/opacity gradient, depth-softened ground intersection/occlusion. Hard square cards, single circular sprite repeats, saturated opaque sheet, fullscreen tint 금지. 후보가 단순 sprite replacement를 넘지 못하면 ShaderLab/URP shader 또는 VFX Graph로 isolated candidate를 추가 비교한다.
6. World path must remain anchored during player translation/rotation; use one nozzle emission owner. Particle motion needs a deterministic world-space heading or velocity-at-birth; do not parent old segment transforms to player. Cap max particles/segments/allocations/overdraw and provide lifecycle cleanup.
7. Treat `AeroLocalizedGasVolume` as a separate bounded moving-volume candidate, not a presumed trail solution. Existing `AeroGasSprayRuntimeHost` currently updates one follow volume. It needs persistent history segments/samples to meet the trail contract. Use only an isolated cloned renderer/test scene; don't enable global AERO/URP or alter vendor files.

**Gate 3:** isolated same-camera side-by-side capture shows continuous curved world-space path, distinct fresh vs old density, no 1fps patch jumps, correct lime green, translucent readable ground, age fade and no player-follow of old segments. Any candidate missing these remains rejected, regardless of package/particle count.

### Phase 4 — Continuous G integration and resource invariants

- Confirm one G reader/owner and keep configured Mouse1 secondary hold semantics unless source/runtime evidence reveals an existing conflict. Define one authoritative `GasSprayerController.IsSpraying` transition state.
- Test stationary start → move straight → curve/turn → stop while prior gas remains → resume with a separated new segment → G release → fade → unequip/reload/dose/fuel depletion/focus loss/scene exit cleanup.
- Hold over a fixed target and compare zero/low/high VFX sample configurations: gameplay ticks remain at current interval; exact damage/heal/buff count, loaded doses, fuel delta unchanged. No per-particle/callback gameplay effects.
- Keep potion-type semantic colors and radial `GasCloudField` consumable effect distinct; continuous plume should use poison lime-green unless type design states otherwise.

**Gate 4:** fixed action trace yields identical gameplay/resource totals across visual rates; old gas stops moving with player; teardown leaves no systems/materials; no global renderer state changes.

### Phase 5 — Final verification + plan execution records

Run sequentially, one Unity process at a time:
1. targeted RED/GREEN bow geometry + release-snapshot tests and continuous gas lifecycle tests;
2. focused Bow/Sprayer suites and exact XML parse; inspect every failure name/message;
3. batch compile with fresh log, source/DLL mtimes, `error CS=0`, Tundra success marker;
4. full EditMode and explicit failure classification against current baseline;
5. Play capture for bow + same-camera reference gas, plus performance/profile; no screenshot/video means visual gate pending;
6. independent spec/diff QA;
7. after actual Gate acceptance only, update `QAPROGRESS.md` (and applicable ROADMAP if a named phase is complete). Any completion record distinguishes implementation, static QA, compile, targeted/full EditMode, Play and profile.

No reset/clean, no `git add -A`, no commit/push without user direction. User videos, Screenshots, unrelated dirty/untracked assets and generated `TestOutput/compile.log` are preserved and not staged.

## 4. Stop / reporting rules

- If one reticle producer/camera cannot be tied to the visible cursor marker, report the exact producer ambiguity before changing projectile physics.
- If the isolated gas candidate still looks like orange/pixel patches or fails to leave a continuous path, stop and compare another render path; do not claim that texture/importer/test presence implies quality.
- If gas is visually excellent only in an isolated camera but not actual gameplay camera, reject it.
- Keep `BowAimAlignmentTests` and `GasSprayerPhase4ATests` as code-path tests, but never use their pass as Play visual acceptance.
- Current working tree is already dirty in related and unrelated files. Before implementation, preserve exact pre-change files/mtime/diff and edit only an explicit allowlist; do not fold prior combo/spear or unrelated UI edits into this task.

**Plan status:** written for user review only. No source/test/scene/assets/QA records changed as part of this planning request.