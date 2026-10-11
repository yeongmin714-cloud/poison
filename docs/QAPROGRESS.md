# QA 진행 상황 — 2026-10-03 활 조준/연속 가스 분사 재개

## 현재 상태 (2026-10-04 10:05 KST — 최신 검증)
- 사용자가 명확히 지정한 화살 요구: 시각 추가물/메시 없이 기존 흰 네온 비행 잔상만 유지하고, 조준선 방향으로 발사. 최신 `가스분사 테스트 6.mp4`(42.624s, 1240×828, 27.35fps)는 가스 효과 테스트이며 실제 활 드로·발사는 보이지 않아 활 방향 증거로 사용하지 않음. 가장 최신 bow 명명 영상은 10/03 캡처로 현재 변경보다 이전.
- `ArrowManager.TrySolveAim()`는 이전에 화면 Ray를 발사하면서도 그 Ray 첫 non-shooter collider의 hit point(특히 Test_10 기본 layer 지면 MeshCollider)와 발사 muzzle 간 벡터를 방향으로 사용했다. 화면 ray의 aim line과 발사 각도가 달라질 수 있는 수학적/코드 경로를 특정. 이제 screen `Camera.ScreenPointToRay`의 정규화된 `ray.direction` 자체를 초기 발사 방향으로 사용하고, ray-hit point는 진단/표적 데이터로 보존. Test의 displaced muzzle + ground plane + 두 픽셀에서 방향/초기 속도 distinct 및 ray 정합 검증 `bow-ray-neon-only-final.xml` 15/15.
- 요청한 neon-only 화살: `ArrowProjectile.Spawn()`은 추가한 GLB/절차 head/fletching object를 생성하지 않고 root MeshRenderer를 꺼 둠. 기존 Rigidbody/capsule collider/flight physics 및 white additive `TrailRenderer` 유지. fixture `SpawnCreatesOnlyWhiteNeonTrailAndPhysicsBody`와 no-mesh off-center velocity tests 통과. 새 외형/오브젝트 추가 없음.
- 가스 test6 화면은 구름이 흐리고 드문드문해 사용자의 “훨씬 더” 기준보다 양이 적어 보임. 색·텍스처·layer shape/lifetime/size/speed/noise/fade 유지, rate만 24/12/9→48/24/18 particles/s, bounded caps 96/48/72→192/96/72. TDD RED `gas-amount-much-more-red.xml` 1 fail → GREEN `gas-amount-much-more-green.xml` 28/28. Gameplay effect/dose/fuel/range unchanged. Test6 timing은 source build/run attribution을 확정하지 않으므로 updated amount는 fresh Play pixels로 확인할 것.
- Compile `compile_test.sh` exit 0, `compile.log` 09:56 success, Systems/EditMode DLL 09:48–09:50 (fresh). Focused bow 15/15, gas 28/28. Fresh final full XML `arrow-neon-ray-gas-volume-final-full-rerun.xml`: 449 total, 440 pass, 9 fail, 0 skip. 9 same baseline groups: AERO expected-log; AttackTargetRing EditMode DontDestroyOnLoad; CookingDatabase 760≠2024; CastleInteriorTopology 4; RecipeCatalog unknown combo; ThemeUSS bg_window.png.
- **Test_10 Play 미실행** (Unity Editor process unavailable). Source/tests now encode exact aim-ray direction and no mesh visual but user-visible direction remains pending a Test_10 run with two separated aim points, inspecting initial white neon trail vs gravity after launch. Gas volume increase likewise awaits actual rendered frame. No visual acceptance claim.

---

## 이전 상태 (2026-10-04 09:05 KST — 이전 검증)
- 사용자가 화살 한 방향 문제가 여전하다고 재보고. 최신 활 영상 `Screenshots/화살 모션 테스트.mp4`(36.48s, 1240×828)는 2026-10-03 08:39Z 기록으로 현재 변경들보다 앞서며, 개별 release와 aim pixels 연결을 입증하지 못함. Unity Editor 프로세스 부재로 current-source Play 확인 불가.
- 새 원인 가설(증거 기반): `BowAimReticleUTK` 16ms schedule과 `PlayerCombat.Update` release는 서로 다른 tick이라 이전 cursor sample이 shot solver에 사용될 수 있음. 정확 release pixel을 `BowAimState`에 동기 스냅샷으로 저장; `TryBowShot`은 snapshot 사용, reticle fade도 같은 pixel에 pin. TDD RED 1 failure → `bow-release-postcompile.xml` 15/15; 소스 경로 개선이나 UI+camera+live launch의 시각적 해결 증거는 아님.
- 가스 디자인/색은 유지하고 양만 추가 증가: rates 18/8.4/6→24/12/9 particles/sec; bounded pool caps Soft/Puff/Wisp=96/48/72 (기존 72/40/72). TDD RED에서는 새 rate assert가 기존 설정에서 실패; `gas-volume-postcompile.xml` 28/28 통과. Lifetime, size curve, shape/nozzle, tint, gameplay effect tick/dose/fuel/range 변경 없음. Test_10 실제 화면 비교 대기.
- 최신 compile `compile_test.sh` exit 0, `compile.log` batch success 09:04; Systems/EditMode DLL 09:02. 전체 EditMode는 이번 pass에서 미실행(이전 full은 447/438/9/0; 아래 이전 인계 참고).
- Test_10 Play 미실행: 두 개 다른 reticle 위치의 release ID→camera ray/hit/muzzle/initial velocity 같은 run 비교는 여전히 미승인; gas screen-density comparison도 대기. 작업 트리 보존, no stage/commit/push.

---

## 이전 상태 (2026-10-04 01:12 KST — 최신 검증)
- 최신 `Screenshots/가스 분사 테스트 5.mp4`는 1240×828, 34.88초. 우클릭 원인을 실제 입력경로에서 확인: `TestTerritoryCombatSetup.SetupGasVerifyScene()`가 Mouse1=`KeyCode.Mouse1`인 `SprayInputHandler`를 붙였고, 이 핸들러는 `StartSpray()`→one-shot `SpecialEffectsController` burst와 continuous plume을 함께 발동. Test_10에선 이 secondary handler를 제거하고 G direct input은 유지. RED `right-click-spray-input-red.xml` 2 fail → GREEN `right-click-spray-input-green.xml` 26/26. 레거시 Mouse1 handler는 다른 씬/사용처를 위해 유지. 지그재그 띠의 화면 주체는 미확정이며 우클릭 분리 후 Play 검증 대기.
- 가스 디자인은 기존 부드러운 층/형태/수명/world trail을 보존하며 시각만 조정: particle rate 15/7/5→18/8.4/6, AERO payload density .65→.78, poison tint `(.62,1,.12,.5)`→더 진한 초록 `(.2,.65,.08,.5)`. 피해/연료/물약/범위/효과 tick은 변경하지 않음. `gas-rightclick-density-final.xml` 27/27. 실제 렌더 비교는 Play 미실행이라 미승인.
- 활 stale sample 경로: parry-queued release 및 `TryAttack` Bow fallback가 새 draw session 없이 이전 reticle sample을 쓸 수 있었음. 두 비정상 경로는 stale 방향으로 발사하지 않고 취소하도록 수정; 정상 좌클릭 hold→release 유지. TDD `bow-stale-entry-red.xml` 1 fail → `bow-stale-entry-green.xml` 13/13. 카메라 freeze 테스트도 포함한 최종 `bow-aim-glb-final.xml` 14/14. 이는 stale 재사용 방지이지 사용자의 ‘항상 한 방향’ 증상을 Test_10 Play에서 해결 입증한 것은 아님.
- 요청대로 GLB 3종(`arrow.glb`, `arrow2.glb`, `arrow3.glb`)과 `.meta` 제거. Assets 전체 경로/GUID 검색에 다른 참조 없음. Runtime GLB mount 제거, procedural shaft/head/fletchings + Rigidbody/collider/trail 유지.
- 최신 compile `compile_test.sh`: exit 0, batch success; `compile.log` 01:08, Systems DLL 01:02, EditMode DLL 01:06. Full EditMode `user-request-gas-bow-final-full.xml`: 447 total, 438 pass, 9 fail, 0 skipped. 9개는 이전 실패군과 같음(AERO expected-log, AttackTargetRing EditMode DontDestroyOnLoad, CookingDatabase 760≠2024, CastleInteriorTopology 4, RecipeCatalog unknown combo, ThemeUSS bg_window.png).
- Unity Editor process가 없어 Test_10 Play를 이번 작업에서 실행하지 못함. 우클릭 단독 이상효과/띠 owner, 서로 다른 aim point에서 화살 방향, 가스 양·색의 실제 화면 변화 모두 승인 전. dirty worktree 보존, stage/commit/push 없음.

---

## 이전 상태 (2026-10-04 00:06 KST — 이전 검증)
- Test_10 G-held/player-following gas trail은 그대로 보존. 영상 4의 딱딱한 초록 사각은 PNG alpha가 아니라 runtime URP Particles/Unlit 재질이 Opaque 기본값(Surface 0, One/Zero, ZWrite 1)이던 것이 원인으로 판명. Alpha surface/blend RGB·A, ZWrite off, keyword/tag/queue를 명시. RED `TestOutput/gas-material-red.xml` 1 fail → GREEN `gas-material-focused.xml` 25/25; 재실행 `gas-transparent-focused-final.xml` 25/25; 전체 `gas-transparent-full-editmode.xml` 438/429/9. 컴파일 exit 0, CS error/warning 0. **실제 영상 픽셀 검증은 아직 안 했으므로 개선 추정이지 시각 승인 아님.**
- 활의 한 방향 의심에 대해 확인: `TopDownCameraController.LateUpdate`는 절대 마우스로 카메라 yaw/pitch를 계속 변경하고, 활 release는 PlayerCombat Update에서 같은 프레임의 이전 camera pose로 screen pixel→ray를 풀었다. 최소 일관성 수정으로 드로 중 카메라를 freeze하고, release pending+left-button release frame에도 freeze해 그 샘플을 같은 view의 ray로 푼 뒤 다음 프레임부터 camera update를 재개. TDD RED `bow-camera-stability-red.xml` 5 fail(새 5 cases) → GREEN `bow-camera-stability-full-final.xml` 12/12. 이건 카메라/view race hypothesis에 대한 보정이지, “항상 같은 방향” 문제의 Play상 해결 입증은 아니다.
- 최신 compile `compile.log`(2026-10-04 00:04): exit 0, CS errors/warnings 0. 전체 최신 EditMode XML `gas-transparent-full-editmode.xml`: 438 total, 429 pass, 9 fail, 0 skip. BowAim 7/7 및 GasSprayer 25/25 pass. 같은 9 baseline failures: AERO expected-log, AttackTargetRing EditMode DontDestroyOnLoad, CookingDatabase 760≠2024, CastleInteriorTopology 4, RecipeCatalog unknown combo, ThemeUss bg_window.png.
- 보존: 작업 트리는 다수 사용자 변경 포함, stage/commit/push 안 함.

## 미완료 Play 게이트
- Test_10 Play 미실행. 가스의 feathered alpha 실제 화면 품질/reference match와 화살의 서로 다른 조준점별 launch 방향을 승인하지 않음. 다음 검증은 G-hold 이동/회전/정지 trail과, 커서/reticle을 분리된 위치로 두 번 발사하며 같은 화면/로그의 ray-hit-muzzle-dir-initial velocity를 1:1 연동하는 것이다. 2026-10-03 `화살 모션 테스트.mp4`는 최신 수정 전이며 bow launch 확인용으로 불충분.

---

## 이전 상태 (22:38 KST 검증)
- 사용자가 확인한 G-held nozzle/player-following 동작은 그대로 보존했다. 새 `Screenshots/가스 분사 테스트 4.mp4`(1240×828, 30fps, 87.1s)는 밝고 각진 초록 사각 스프라이트/분리 퍼프를 보여 주고, 참고 `가스 살포 예시.mp4`(984×556, 30fps, 4.1s)의 부드럽게 페이드되는 녹색 연무와 외관 차이가 있다. 원본 PNG 마스크는 이미 graded alpha(최대 alpha 198/212/185)를 갖고 있었고 importer도 alpha transparency를 보존한다.
- 원인 확정: `GasSprayer.GetOrCreateGasMaterial()`이 URP Particles/Unlit를 runtime 생성할 때 texture만 지정했고 shader 기본 `_Surface=Opaque`, One/Zero blending, `_ZWrite=1`을 그대로 뒀다. 따라서 PNG·particle alpha가 있음에도 화면에서 각 billboard가 단단한 밝은 사각처럼 보일 수 있었다. 이제 RGB와 alpha blend factors, Transparent surface/keyword/tag/queue, ZWrite off를 명시했다. 입자 수·emit rate·range·offset·lifetime·노즐 추종·World simulation·G 입력/dose/fuel/damage/effect tick은 이번 material 수정에서 바꾸지 않았다.
- TDD: 기존 material-state 테스트를 확장해 세 layer 각각의 `_Surface`, blend mode, RGB/alpha blend factors, ZWrite, tag, queue, keyword와 실제 partial-alpha texture를 검증. RED XML `TestOutput/gas-material-red.xml` 1 failed (alpha-channel `_DstBlendAlpha`가 0, 기대 OneMinusSrcAlpha) → GREEN `TestOutput/gas-material-focused.xml` 25/25 passed. `compile_test.sh` 최신 실행 exit 0, `compile.log` normal exit, C# errors/warnings 0. 전체 EditMode `TestOutput/gas-transparent-full-editmode.xml`: 438 total, 429 passed, 9 failed, 0 skipped. BowAimAlignment 7/7, GasSprayerPhase4A 25/25. 같은 9개 기존 실패명/원인은 아래 섹션 참조.
- Visual output has NOT been captured after the shader fix. 따라서 예상되는 soft alpha correction은 code/config-level fix로만 보고하며 Test_10에서 실제 feathering, blending, cloud overlap, fade/continuous trailing 및 reference match Play 미확인이다.
- 활: 추가로 보고된 “한 방향으로만” 증상은 여전히 원인 입증/수정 완료가 아니다. 기존 화살 영상(36.48s)은 조준·발사 순간을 확정하지 못하며 소스보다 이전 자료다. 소스에는 normal release path reticle sample→camera ray→muzzle→velocity chain 있고, fixed direction assignment는 normal launch 경로에서 확인되지 않았다. Test_10 동일 실행의 서로 다른 두 개 이상 조준 release를 로그+화면으로 재현하는 게이트가 남아 있어 이번 turn에는 speculative bow patch 하지 않았다.

## 다음 게이트
1. Test_10 Play에서 가스 분사 테스트 4와 같은 조건으로 홀드 이동/회전→정지→release를 캡처해 soft alpha가 실제 반영됐는지 확인한다.
2. 활은 서로 반대쪽/상하 조준점으로 각각 release하여 `[BowAim] screen/camera/ray/hit/muzzle/dir`와 `fired velocity`를 화면의 reticle/cursor와 같은 shot에 대조한다. 현재 증거만으로 화살 버그를 해결했다고 선언하지 않는다.

---

## 이전 인계 (21:52 KST)
- 더러운 작업 트리 및 사용자 미추적 자산을 그대로 보존했다. 이번 범위의 소스/테스트만 확인했으며 stage/commit/push 하지 않았다.
- Test_10은 `EnsureGameManager()`에서 PlayerInventory보다 먼저 ArrowManager를 만들고, PlayerInventory를 한 번만 캐시하던 순서 문제로 발사 가능 화살이 0으로 보일 수 있었다. ArrowManager가 조회 시 지연된 Inventory를 재획득하도록 바꿨고 TDD 검증을 통과했다.
- ArrowProjectile.Spawn은 기존에 충돌용 primitive renderer만 끄고 GLB mount/procedural body 헬퍼를 호출하지 않아 보이는 화살 몸체가 없었다. 이제 child GLB를 mount하고 실패하면 procedural body로 폴백하며 root Rigidbody/Collider와 TrailRenderer는 유지한다. EditMode에서 child renderer 및 발사축 정렬을 확인했다. 이는 UI 조준점이 실제 화면에서 정확히 맞는지 증명하지 않는다.
- Test_10 가스 씬은 물약을 지급만 하고 장전하지 않아 `EmitPlume()`이 `PotionType.None`으로 연속 플룸을 중단할 수 있었다. `SetupGasVerifyScene()`에서 분사 입력/HUD를 보장하고 독 물약 한 개를 장전하도록 맞췄다(자동 분사 아님). G-held 경로는 기존 일회성 `SpecialEffectsController` burst를 호출하지 않고 지속 world-space 플룸을 사용하며, 공개 `StartSpray()` 및 legacy Mouse1 경로는 기존 일회성 효과 호출을 유지한다.
- 최신 컴파일: `compile_test.sh` exit 0, `compile.log` 정상 종료, CS 오류/경고 0. 최신 EditMode XML `TestOutput/gas-bow-model-full-editmode.xml`: 437 total, 428 passed, 9 failed, 0 skipped. BowAimAlignment 7/7, GasSprayerPhase4A 24/24 통과. 9 실패는 이전 `full-current.xml`과 동일한 실패명/설명으로 재현됨: AERO expected-log, AttackTargetRing EditMode DontDestroyOnLoad, CookingDatabase 760≠2024, CastleInteriorTopology 4(파괴 로그 2·면적 기대값·portal graph), RecipeCatalog unknown combo, ThemeUss bg_window.png.
- 컨트롤러의 광범위한 기존 diff에는 물약 투여량 타이머/재고 소모·자동 장전 생명주기 변경도 포함되어 있다. 독립 QA에서 이 변경은 순수 시각 효과 조정이 아닌 gameplay semantics임을 확인했으므로, 이를 “dose 동작 불변”이라고 말하지 않는다. 연료 배율/기존 damage tick owner는 별도로 보존한다.
- 실제 Test_10 Play 캡처는 이번 턴에서 실행하지 못했다. 따라서 화면상 reticle-dot→ray→muzzle 정합, 초기 비행의 실제 카메라 픽셀 정합, 새 화살 모델의 화면 크기, 가스의 이동 후 world trail·회전·페이드·참조 영상 대비 품질은 아직 미검증이며 해결/시각 승인 완료가 아니다. 이전 사용 영상들은 변경 전 자료다.

## 다음 게이트
1. Test_10 Play에서 오프센터 조준, release 동일 프레임의 표시 reticle/ray/hit/muzzle/초기 velocity를 캡처해 비교한다(이후 중력 궤적은 분리).
2. Test_10에서 G를 누른 채 이동/회전→정지→재출발→release를 캡처하고, 노즐 추종·world-space trail 잔류·3초 확산/페이드 및 one-shot burst 부재를 참고 영상과 시각 비교한다.
3. Play 후 최신 Editor.log에서 ParticleSystem 경고를 확인하고, 결과에 따라 후보를 조정한다. 지금은 코드/컴파일/EditMode 게이트만 통과했다.

> 테스트 통과 및 컴파일은 실제 Play 시각 승인과 다르다. 현재 코드는 반영됐고 전체 EditMode는 428/437, 실제 Play 미확인이다.

## 현재 작업 트리에 반영된 관련 변경
- 기존 bow 경로는 release마다 활성 MainCamera를 다시 확인하고, 새 draw 시작 뒤 reticle sample이 없거나 solver 실패 시 발사를 취소한다. ray 결과와 muzzle을 연결하고, projectile 참조/초기 velocity를 진단한다.
- 신규 `OffCenterAimHitReachesTheProjectileInitialVelocityAndLongAxis`는 off-center camera sample이 비중심 collider hit를 맞춘 후, `ArrowProjectile.Spawn` 초기 속도와 local +Y 축이 같은 방향이며 목표점을 향하는지 행동 단위로 검사한다. 전체 PlayerCombat UI 경로 및 실제 Play 비행과는 별개다.
- 연속 가스 풀은 제한된 3개 World-space ParticleSystem과 기존 베이크 텍스처를 사용한다. 수명을 3.0–3.5s로 늘리고, 시작 회전을 0–2π로 랜덤화해 반복적인 stamp silhouette를 완화하려 했다. Size/Color over lifetime으로 확산·페이드를 유지한다. gas input/damage/effect tick/dose/fuel gameplay 경로는 이 조정 대상이 아니다.
- `ContinuousSprayPuffsExpandAndFadeAcrossAnApproximatelyThreeSecondLifetime`는 lifetime range, 랜덤 회전 범위 및 fade/size 모듈 설정을 검사한다.
- 기존 `BowAimReticleUTK`, `BowAimState`, `ArrowManager`, `PlayerCombat`, `GasSprayer` 등 미커밋 파일들은 선행 작업부터 있던 더 큰 diff다. 본 갱신은 이를 통합 기록할 뿐, 모두 이번 변경이라고 귀속하지 않는다.

## 검증 결과
- 최신 `compile_test.sh` 실행: exit 0, `compile.log`에 `Exiting batchmode successfully now!`, `error CS=0`, `warning CS=0`. 단, Unity script DLL 시각은 compile.log보다 이전(Assembly-CSharp 20:30, ProjectName.Systems 20:30, ProjectName.UI 19:42)이므로 freshness 근거가 서로 일치하지 않는다. 컴파일 스크립트의 성공 표기는 있으나 이를 최신 모든 소스의 재빌드 증거라고 과장하지 않는다.
- Focused bow XML `TestOutput/aim-focused.xml` (20:35): `total=1, passed=1, failed=0`; 위 off-center hit→초기 velocity/axis 테스트가 통과.
- Focused gas XML `TestOutput/gas-focused.xml` (20:38): `total=1, passed=1, failed=0`; lifetime/rotation/fade 설정 테스트 통과.
- 최신 전체 EditMode XML `TestOutput/full-current.xml` (20:40): `total=434, passed=425, failed=9, skipped=0`. 9 실패: AERO expected-log 1; AttackTargetRing의 EditMode `DontDestroyOnLoad` 1; CookingDatabase 760 기대/2024 실제 1; CastleInteriorTopology 4(Destroy edit-mode 2, footprint 6.0 기대/6.00249958 실제 1, portal graph 1); RecipeCatalog unknown combo 1; ThemeUss baked `bg_window.png` 기대 1. 전체 테스트 통과가 아니며, 관련 신규 focused 테스트들은 이 전체 결과에 포함되어 통과했다.
- 스크린샷/영상 실측 시 이전 arrow 영상은 17:39, 가스 영상은 18:29로 해당 변경 코드보다 이른 시점이다. 기존 Editor.log는 19:47이며 최신 Play 기록이 없어 수정 이후 ParticleSystem 경고 및 시각 변화 여부를 입증하지 않는다.
- 독립 리뷰는 새 행동 테스트와 파티클 수명/회전 설정을 read-only 확인했다. 리뷰의 핵심 한계: 테스트는 실제 렌더 품질, 마우스-UTK center 위치, 전체 PlayerCombat 발사 인테그레이션, 중력 비행을 입증하지 않는다.
- 변경 관련 경로에서 `git diff --check` 통과. 저장소 전체에는 별도 사용자 변경/산출물이 있으므로 전역 정리/커밋은 하지 않았다.

## 아직 미검증 / 완료 게이트
- 실제 Play 캡처로 커서/표시 dot 중심과 ray, hit, muzzle, 화살 초기 방향을 동일 release에서 대조하지 못했다. 따라서 사용자가 지적한 in-game 화살 방향 문제 해결 완료라고 선언할 수 없다.
- 실제 Play에서 16:9/다른 화면비율·화면 가장자리, 오브젝트 차단/무차단, 초기 방향과 중력 후 궤적을 구분 검증해야 한다.
- 수정된 GasSprayer의 실제 색/형태/카메라 가시성, 이동/회전 trail 잔류, 정지 후 확산/페이드와 성능을 검증하지 못했다. 가스 시각 개선은 구현 의도/설정 반영 단계이며 사용자 기준 품질 승인 대기다.
- 현재 source→DLL 시간 불일치를 해소할 fresh compile freshness 증거 및 최신 Editor.log의 ParticleSystem 경고 확인이 남아 있다.
- 9개 전체 EditMode 실패는 아직 별도 조사/수리 범위가 아니다.

## 다음 게이트
1. 사용자 Unity Editor를 덮어쓰거나 다른 Unity 인스턴스를 종료하지 말고 현재 상태를 확인한다.
2. 변경된 source에 대해 실제 DLL timestamp/로그와 일치하는 compile 증거를 확보한다.
3. Test_10에서 동일 release 계측을 포함한 Play 캡처로 reticle-dot→ray→hit→muzzle→initial velocity를 비교한다. 중력 후 편차는 분리한다.
4. GasSprayer의 G-held 연속 분사와 potion GasCloudField를 분리 재현하고, 참조 영상과 최근접한 화면 크기에서 trail/fade를 시각 비교한다.
5. 증거에 맞춰 수정 후 docs와 루트 QA handoff를 동기화한다. Play 확인 전 완료/시각 승인 표현은 금지한다.

> 테스트 통과와 실제 Play/화질 검증은 서로 다른 게이트다. 현재는 focused code tests 통과, 전체 EditMode 부분 실패, Play 시각 승인 대기 상태다.
