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

## [144차 Figma 패리티] Phase 4 — 상태/포션/대화/상호작용/컴팩트 3종 (2026-10-31)

### 적용 범위 (감사: TestOutput/figma_audit_144/{status_potion,dialogue,interaction,side_panels}.md · 진실치: docs/figma_truth_144/)
- **StatusWindowUTK(CSP 본체, status-only)**: P0 8건 — 코너 데칼 4개(14.4·#58A6FF 1.8·코너 밖), IdentitySection 카드(528×84 @0.02 r9.6 pad14.4 gap7.2)+Rarity 14.4/700 #BC8CFF+ClassType 행(실데이터 소스 부재→"클래스: —" 플레이스홀더 기록), StatsSection 카드화(@0.012 r9.6 pad14.4·LevelBlock marginRight 19.2), SpecialStatsSection 카드+(24,724) 528×157, 게이지 VERTICAL 2단 재구성(도트 7.2[O10 치환색]·게이지명 15.6/700 #F0F6FC·값 16.8/700·BarBg 7.2 @0.059 r3.6), 특수헤더 2분할(14.4/700 #8B949E + SPECIAL VITALS 12/400 #484F58). P1: 글래스 override 제거(BuildPanel 0.702 상속)·테두리 4면 균일 1.2·pad24(보상 80→74)·타이틀 24/부제 13.2·CloseBtn @0.059 r7.2 stroke 1.2 pad7.2+필드 갱신·EXP 12/400 #56E39F·CoreStatsGrid 무배경·라벨 "공격력 (ATK)" 등 4종·값 다색(ATK #58A6FF/DEF #F0F6FC/HP #FF7B72/AGI #F2C94C)·라벨 자동폭+구분선 제거·hsep #30363D@0.502·Portrait r9.6(요소 단위)·HudOvelay 투명화·오버레이 라벨 #58A6FF·LEVEL 13.2. 유지(계약): CharacterName 미구현(위조 금지 주석), BarFill [O10] 치환색, 우측 LegacySupplementaryDetails, 게임 전용 Lv.- 배지/HP 트랙.
- ⭐ 잠재버그 수리: `special-gauge-root` 클래스가 미부여되어 ApplyStatusOnlyComposition의 폴백 부모 워크가 _statusContent를 반환하던 결함 — _figSpecialSection에 AddToClassList로 수리(Q 경로 확보).
- **PotionStatusUTK**: 데칼 4개, fill 0.851/stroke #30363D@0.8 1.2/r14.4/pad19.2/간격 19.2 체계, wrapper 187.2×274.8 @0.502 r9.6(게임 전용 목록=내부 하단 오버레이 이동 보존), **btn-spray-toggle "G키 분사/종료"+"활성화 전환" 2단 = GasSprayerController StartSpray/StopSpray 실배선**, 복용=목록 행 클릭 경로 보존, 닫기 14.4/500 #8B949E glass r7.2, badge #58A6FF@0.082 체계(텍스트는 실상태 유지), 게이지 트랙/fill/라벨 수치 정합.
- **NPCDialoguePanelUTK**: bg #161B22@0.45(74차 정정), 테두리 #30363D@0.3 1.2 추가, 패딩 14.4, 간격 gap 9.6 체계, NpcNameRow h15, ContinueHint 96px 고정, **187:14 순서 교정(Dialogue→Hint→Grid)**, 카드 테두리 @0.302 1.2+패딩 9.6/4.8, 데칼을 공용 UTKTheme.AddCornerDecals(코너 밖·#58A6FF·1.8)로 일원화(로컬 구현 삭제), LSB 색 4건 정정(#33E5D9/#30363D/#EBEDF0/#58A6FF), 3열 주석 오기 정정. 계약테스트 NPCDialoguePanelFigmaTests 5건 갱신.
- **InteractionPanelUTK(7판본)**: P0 — **Ally(81:51)=상태보기★/대화하기/물약주기/음식주기**(하드코딩 뇌물/포섭 정정, IsActionSupported·SoldierInteractUTK 기존 경로 연결), 버튼 아이콘 16.8(Resources/UI/icons 실에셋 근사 매핑 16종+좌측행 pad8/gap6), 7판본 공통 코너 데칼(Show 멱등 부착, Fishing 제외). P1/P2 — Theme.uss .utk-btn(0.6알파·보더 #30363D@0.502 1.2·r4.8 전 창 공용), 하이라이트 크롬(bg #30363D@0.8+보더 #58A6FF 인라인), SingleRowGridHeight 38→30(Figma 36px), HighlightText #58A6FF/NormalText #8B949E LSB, "세금 제안" 라벨+라우팅키 3곳 동기(계약테스트 TaxProposalWiringTests 갱신), Fishing 데칼 inside→outside 정정.
- **컴팩트 3종 D2(163:45/64/83)**: TargetKind.Bed 신설(ClassifyOne/ResolveInteractionTarget Bed 컴포넌트), Door 리졸버 회복(구: 미소비로 패널 개방 경로 부재), GetLayoutSize(288×60/288×64.8), Bed="잠자기" 전폭 카드(moon ☾ 글리프 근사, 클릭=Bed.OnInteract()=E키 동일), Door=영주실 문(house_lord_room→"E키를 눌러 영주실 들어가기")/일반 문("E키를 눌러 자물쇠 따기") keycap-E 칩+라벨, 클릭=**LockedDoor.RequestLockpick() 신설 공용 래퍼**(마스터 키 우선→OnLockpickRequested — E키와 동일 처리, CS0079 회피). IsActionSupported(잠긴 문만 활성 — 피드백 계약). E키 프롬프트 병행 유지(후속 사용자 판정 항목 기록).
- Theme.uss는 전 창 공용 — .utk-btn 변경이 타 창 버튼에도 적용(§2 공용 어휘 1회 정비 방향).

### 검증
- compile_test.sh: **exit 0, error CS 0** (최종 3회차 — CS0826 혼합배열, CS0079 이벤트 발화 2건 자가 수리 후 통과).
- 전수 EditMode(run_editmode_check.sh): **total=1777 / passed=1767 / failed=10** — 실패집합은 기존 baseline+병행WIP의 부분집합(AERO expected-log, CookingDatabase 760≠2024, RecipeCatalog unknown-combo, ThemeUss bg_window.png, Instrument×3, InvadedLord×2, 실내 H2 blob-shadow) — **신규 실패 0**, 직전 게이트(12건) 대비 2건 감소(기존 flaky/baseline 통과).
- 미완료: Play 스크린샷 픽셀 비교(사용자 승인 게이트), 커밋(별도 승인). Phase 5(퀘스트/월드맵/전투일지) 대기.

## [144차 Figma 패리티] Phase 5 — 퀘스트/전투일지 매핑/월드맵 (2026-10-31)

### 적용 범위 (감사: figma_audit_144/{quest,worldmap}.md · 진실치: figma_truth_144/)
- **QuestWindowUTK(93:230)**: P0 — 루트 디밍 #0B0E14@0.82(_content 배경 — RuntimePanelRoots 계약 보존), 패널 데칼 ×3(14.4·#58A6FF 1.8), 히어로 부제 행(tag 정보 재배치), 보상행 부제(데이터 가드), **DeploymentFooter 구축**(TrackingRow 레이더+라벨+TogglePill / 포기·추적 적용 버튼). P1 ~40건 — 글래스 0.702(D1), 헤더 구분선, 제목 "퀘스트 창/줄거리"+색 #F0F6FC, 서브타이틀 3종+letterSpacing 1.2, 탭 간격/색/r, 슬롯 배경 @0.078(선택)/@0.02+테두리 체계+r9.6+선택 하이라이트 분기, Pill #0B0E14+스트로크, Lv 우측정렬, 게이지 간격/트랙/필/#라벨, 푸터 2요소 분리+보더, 히어로/스토리/목표/보상 카드화+폰트·색·간격 전반. 유지(기록): 닫기×3·수락/완료 버튼·체인 UI(기능 보존), 보상 설명문(데이터 부재), TierStrip 상태색/아이콘 등급색(의미색 사용자 판정 항목), 이모지 어휘.
- **QuestJournalUTK**: ⭐ **daily-combat-log 매핑 폐기 확정** — 123차 사용자 요청 전투로그 폐기(no-op 스텁)와 충돌 → 퀘스트 저널로서 현상 유지 + 93:230 공유 크롬 정합(ApplyFigmaGlass 0.702/stroke 1.2/r14.4/데칼 #58A6FF 공용 어휘/헤더 구분선).
- **WorldMapWindowUTK**: P0 — **좌/우 패널 독립 창화**: 신규 WorldMapSidePanelWindow(풀캔버스 투명 호스트+디자인 공간 1920×1080 축별 스케일 동일 모델, picking Ignore로 지도 클릭 통과)가 기존 패널 트리를 통째 재부착 — 필드/갱신/폴링 경로 무재배선, 맵 Show/Hide 동기 개폐. 패널 CloseButton ×2(288:7/9 32×32 r16 #161B22@0.8+⊗글리프) 단독 닫힘. TitleGroup 가로 조합 ×2(HORIZONTAL gap12). 영지 핀 라벨 2행화 — 제2행 난이도 **실데이터 링 표기**(링N/황제국, Figma 샘플 "Lv.45" 수치 부재 — 샘플 치환 프로토콜). P1 — 패널/SystemGroup/Coordinates y −3 정정, letterSp 1.2 ×3, 비선택 카드 @0.02+스트로크 1.2(선택만 accent 1.8), 카드 간격 9.6, Decal 12, 플레이스홀더 문자열 전문 복원, 브리핑 lineH(기록 — IStyle lineHeight 미지원), LordItem x/y, TrackingRow top 14.4, radar y16, 루트 fill #0B0E14+r0, 플레이어 핀 라벨 13.2. 유지: 패널@0.80 계약, 전송=미니맵경로, 핀 폴링/툴팁/소유색 계약.
- ⚠ 위임 2건 타임아웃 → 퀘스트=부모 착지분 검증+letterSpacing 오타 수리(unityCharacterSpacing→letterSpacing), 월드맵=부모 직접 수술(폴백).

### 검증
- compile_test.sh: **exit 0, error CS 0** (중간 CS1061 lineHeight 미지원 1건 기록 전환).
- 전수 EditMode: **total=1777 / passed=1765 / failed=12 = baseline 11+병행WIP 1 정확 일치, 신규 실패 0**. WorldMapFigma124 계약테스트 3건은 같은 차 소스-어서션 갱신으로 해소(칩 r4.8/문자열 전문/핀폰트 13.2). MeleeClickOwnership ×2는 격리 재현 확인 — PlayerCombat mtime 10-24 무변경으로 본 변경 귀속 불가(타이밍 민감 기저 계열 — 분리 조사 항목 기록).
- 미완료: Play 스크린샷 픽셀 비교(사용자 승인), 커밋(별도 승인). Phase 6(투기장/병사/훈련) 대기.

## [144차] Phase 6 — 투기장/병사관리/훈련소 (2026-10-31)

### 작업
- **ArenaResultWindowUTK(280:163/381)**: P0 8건(베팅한선수 가이드·레벨칩 Lv.— 실데이터부재기록·특성 슬롯=출신 이식·상대 썸네일 프레임 52×68·상대결과 텍스트·획득 골드 라벨·지급상태 x826+순이익 라인·정산 요약 2행 JSON 원문) + P1 전면(판테두리 0.502/폭1.0, 제목 fs22 y24, 사진테두리 액센트1.5, 통산전적 fs18, 상태배지 JSON 문구, 마크 76×76 α0.078, 메시지 fs34, 금액 fs56 mono, 정산칩 무배경 x16/333/649, 내역 fs15/내행 fs12·fs14 right=920, 최종행 stroke만, 보유골드 fs13/최종값 fs25 #F0F6FC, 확인버튼, 도움말바 배경제거 x1334 w138, 패배판 구분선 1×40 ×2 신설). 기록: Geist Mono 폰트 폴백(UIFont 단일 정책 — 사용자 판정 항목), 코인 'G'/마크 '✓✕' 글리프 유지.
- **ArenaWindowUTK(베팅 255:647)**: P0 12건(닫기버튼 30×30, 크레스트 42×44 shield 부재→프레임기록, 레벨칩 Lv.— 기록, circle-check 선택 전용, 상태배지 실데이터 바인딩, 개요 카드 920×144 재배치, 전적 3칸 — 최근5경기 모델 부재 '—' 기록, 능력치 가로3열+게이지, 금액 입력박스 통합, 예상수령액 카드 재배치, 확인 카드, 유의사항 JSON 원문) + P1 전면(판테두리 0.502, 카드 #191F27 r6/선택 #1A2839+액센트1.5, 배율 fs21, 전적 fs11, 능력치 스트립, 보유골드 coins 15×15, 빠른금액 '+100/+500/+1,000/최대' fs12, 수령금액 fs32 #58A6FF 좌정렬, 확정버튼 244×44+coins, 도움말 x1202 w270). 계약: [125차 후속12/13] 루트투명+헤더제거 유지, 베팅 상한 5000 유지(Figma 10,000 미채택 — 실상한 동적 표기). 이전 중단 세션 무마커 잔여 흔적 정정(BuildInfoGlyph 정의 보완).
- **SoldierManagementUTK(69:4)**: P0 2건(MissionText 2행 한국명+영문 부제 7개 JSON 원문, 데칼 TR/BL/BR 오배치→UTKTheme.AddCornerDecals 전환) + P1 14건 전부(글래스 0.702, 비활성 탭 투명+스트로크, PanelHeader 테두리, Title↔Subtitle mt8, StatusPill α0.251, AvatarFrame α0.6, 아바타 24, SummaryFooter 테두리+pt, Footer '총원 / 최대 수용력'+'N / M 명'(침대 수용력 API 부재→recruited/totalRegistered 실데이터 바인딩 기록), 등급배지 #BC8CFF r4.8, ImageFrame 배경, Stat 라벨 영문 4개 JSON 원문, Stat max 400/400/500/300, MissionOption pad, CountBadge r7.2). 임무명 5개 교정(경작지 농사단 등 — 표시 문자열만, 라우팅 무손상). 계약 유지: 라디오 원 제거(39차)·8번째 대기 카드·침대 표시(37차).
- **TrainingHallWindowUTK(269:322)**: P0 3건(헤더 VECTOR 2종→map-pin/clock 근사 매핑, 대기중 '취소' 버튼, 루트 pad-top 36 계통 시프트) + P1 핵심(크롬 r14+테두리 0.502, 폰트 스케일 상향, 4열 우측 정렬, 상태 pill 폭) — 마커 #1~#16 착지.
- **InteractionPanelUTK 261:3 잔여**: 베팅하기 x 148→138(gap 7.2, 행 우단 오버플로 정정) — P1 #6. 142차 B′ 배경판 계약으로 크롬 수치는 기록 전용.

### 검증
- compile_test.sh: exit 0, error CS 0 (중간 CS0246 StyleFontStyle 6건 → FontStyle 암시변환 직접 수리 — 구현/QA 게이트 경계 내 컴파일 수리).
- 전수 EditMode: **total=1778 / passed=1768 / failed=10 = 문서화 baseline 10건과 완전 동일(AERO·CookingDatabase·실내H2·Instrument×3·InvadedLord×2·RecipeCatalog·ThemeUss), 신규 실패 0**. Phase 5의 병행WIP MeleeClick×2 이번 게이트 자연 해소(통과). Phase 5에서 발생했던 SoldierManagementLifecycleSummary 1건 신규 → Figma 69:117 진실치 갱신(계약 갱신: 푸터 'N / M 명', 임무배치 수는 배치 패널 소관) 후 집중 4/4 PASS.
- 위임 운용: 3병렬은 OpenRouter in-flight 402+600s 미착지 → 순차 단일 위임으로 전환 후 성공(병사→결과 P0→결과 P1→베팅 P0 6건→베팅 P0 6건→베팅 P1). 베팅 1회 타임아웃 무착지 후 재분할 성공.
- 미완료: Play 스크린샷 픽셀 비교(사용자 승인), 커밋(별도 승인). 다음: Phase 7(건설/음식점/세금/수면/고용비).

## [144차] Phase 7 — 건설/음식점/세금/수면/고용비 (2026-11-01)

### 작업
- **ConstructionWindowUTK(239:683)**: P0 11건(재료표 ColX 5열 전면 재매핑 14/374/490/606/752 — 구 한 열씩 좌시프트, 충족수 'N / M' 실데이터 라벨, 설계도 벡터 그리드+입체선, 조건 카드 아이콘 3종 글리프, 부족 안내 circle-alert, 유의사항 info, 좌 닫기 버튼 제거) + P1 45건 전부(V2 표 stroke, 제목행 가로 병렬, 상태 배지 (332,21), '수정석'→'마력석' D7 표시만, DROP_SHADOW 기록 전용, 닫기 y24, 비활성 필터 700 등). 계약: 루트 불투명 헤더 소실([125차 후속13]) 유지, '기타' 탭 역반영 제안 기록.
- **RestaurantWindowUTK(237:792)**: P0 3건(빈슬롯 square-dashed 근사 25×25, 수령버튼 wallet 글리프 18×18 gap8, 정산 안내 info 13×13 + y 852) + P1 62건 전부(창 DisableWindowShadow, 우 목록 열 x −24px 14/394/500/610/680/768/888, 열 정렬 R/C, 하단 스택 −10~13px, 4블록 보더 V2, TextSub→#8B949E 6요소, 슬롯 열간 8(구10), 닫기 y24, 영업배지 r6 @0.078, 판매 목록 fill 제거, GeistMono 헬퍼). 134차 좌패널 진실치 유효(재실측 일치).
- **RecommendedTaxWindowUTK(244:281)**: P1 27건 전부(테두리 실측 1.2/1.8/2.4/1 요소별 — 코드 0.5 고정 관행 수리, 호감도 항목 x −33.6, 권장 표시선 x 182, 색 7건 #58A6FF/#F0887D/#D29922 체계, '감소 기준' 후단 문구 추가, 버튼/문구 Bold). 구조·좌표 PASS 22건 유지.
- **WeeklyTaxWindowUTK(243:3) 신규 창 (D4 승인)**: 624×984 — 제목/보조, 영지 필터 3탭, 세금 목록 7행(등급바 4색 TierColor 체계+상징+이름+주간 징수액 GeistMono), 주간 요약(합계 박스 @0.078/stroke@0.251 r9.6), TaxManager/AITerritoryEconomySystem 실데이터 연결. 배선(UTKWireUp 등록)은 후속 wiring 사이클(기록). 계약테스트 WeeklyTaxWindowFigmaTests.cs 신규.
- **SleepPanelUTK(227:262)**: P0 3건(회복 항목명 '체력'/'기력' 라벨, 슬롯 정보 3행(저장 상태+PLAY TIME — SaveData에 플레이타임 필드 부재 '플레이 시간 —' 위조금지, file-check-2 글리프 20×20) + P1 전부(루트 테두리 V2, 보조제목 인라인, moon/bed/shield 색 액센트, 기상미리보기, 칩 스트로크, 활성칩, 스크롤바, 스위치 손잡이 #0B0E14 등). BACKGROUND_BLUR 36은 기록 전용(UTK 미지원).
- **MercenaryPaymentNoticeUTK(210:169)**: P0 4건(코너 데칼 V1 UTKTheme.AddCornerDecals, 행 등급바 4.8×86.4 4색(grade DTO 신설), Lv.NN 배지 GeistMono 700 액센트 — 별표 폐기, 지불 요약 카드 구조(divider+단일 라벨 → 수용 인원 행+합계 카드 @0.078, y 852→844)) + P1 21건(루트 #13181F+테두리 1.2, 제목 fs21.6, 탭 h36.2, 선택 탭 Accent, 명부 y151, 행 r9.6/pad14.4, 행 배경/테두리, 아바타 원형 r26.4@0.6, 아이콘 24×24, 병과, 상태 배지 프레임, 고용비 GeistMono 700 액센트, 승인/방출 버튼, 방출 버튼 Secondary). Lv 배지는 MercenaryData에 level 필드 부재 → '—' 표기+기록(위조 금지).

### 게이트 경계 내 컴파일 수리 (구현/QA 경계)
- ConstructionWindowUTK.cs `style.lineHeight`(CS1061 미지원) → white-space+maxHeight 근사 [144차 P7-CON #44 보정]
- WeeklyTaxWindowFigmaTests.cs `.value.value` 이중 접근(CS1061) → 단일 `.value` [144차 P7-WTX 보정]

### 검증
- compile_test.sh: exit 0, error CS 0.
- 전수 EditMode: **total 1782 / passed 1772 / failed 10 = baseline 10건과 이름·메시지 1:1 동일, 신규 실패 0**. 주시 계열 교차 검증: Restaurant 103/103 · Construction 63/63 · Sleep 10/10 · Tax 49/49 · Mercenary 27/27 · WeeklyTax 15/15 · SoldierManagement 4/4. Phase 6의 SoldierManagementLifecycle 잔여 1건도 이번 차 통과 확인.
- 위임 운용: 600s 무착지 타임아웃 3회(건설 P1 전반 → 행 분할, 음식점 P1 전반 → 행 분할, 음식점 P1-C → 보고서 행을 인라인 진실치로 동봉하는 전략 전환 — 이후 위임 13~25콜로 안정화). 신규 창(WeeklyTax) 위임은 검증 단계 타임아웃이나 산출물 완결 확인.
- 미완료: ① WeeklyTaxWindowUTK UTKWireUp 등록 배선(후속 wiring 사이클) ② Play 스크린샷 픽셀 비교(사용자 승인) ③ 커밋(별도 승인). 다음: Phase 8(HUD 계열).

## [144차] Phase 8 — HUD 계열 (2026-11-01)

### 작업
- **HUDUTK(23:6 stamina-gauge×2)**: P1 4건 전부 — bg-radial-glow 틴트 알파 0.30→0.094, outer-track white 0.28→0.102, inner-plate stroke white 0.92→0.2, center-light 0.62→0.149(시맨틱색 계약 유지, 알파만) + P2 GaugeGap 24→24.4(JSON raw 4433−(4271+137.6)). 도넛 채움(텍스처 위임)/베이크 스트립(85차 계약)/rim 단색 근사 유지 기록, inner-plate blur/inner-shadow 미지원 기록.
- **HotbarUIUTK(23:68 + 39:30)**: P0 3건 — corner-brackets 8개(#8C734B@0.251 w2.4, PickingMode.Ignore), 캡션 'HOTKEY QUICK SLOTS · ACTIVE COMBAT MODE'(23:142), HintIcon 배지 36×36 #F0F3F5+rotate-cw. P1 9건 — Theme.uss .utk-slot #0D1015@0.902(itemBg #120D08@0.85는 2026-10-10 사용자 계약 유지), r 9.6, HotkeyBadge #F0F3F5 밝은배지+숫자색 Geist Mono+stroke #0D1015 1.8, 활성 슬롯 #D4AF37 2.4 fill #1D2430@0.902, TabBadge 골드+TAB 텍스트 #0D1015, 힌트 배경판 제거(Figma 투명), INNER_SHADOW 기록. P2 2건 — 바 x 630, 힌트 (926,28).
- **NameplateOverlayUTK(101:5 + 110:14)**: P0 1건 — floating-stats 수직 3행(HP/공격/의약 — JSON 원문 패널 30.6×27.2 정정, 의약 데이터 부재 '—' 위조 금지). P1 9건 — pill r, 글래스 배경 #161B22@0.702, 루트/container stroke 2종 분리, trough fill #1F242C(101:5 기준), level-badge 15.6×19.2+stroke 0.6, monster-name 13.2, track 98.4×6, stats fill @0.22 + stroke #7CE2FE@0.4 0.46 r1.8. [80차] 폭 통일 계약 유지+기록, 그라디언트 fill P2 근사 유지.
- **MinimapUTK(40:44)**: P1 3건 — Diameter 220→264, 여백 24/24 + dead FigmaBezel 상수 제거, mapFrame border white@0.3 2px → #E5E7EB 1.2(outer-ring). 3링→텍스처 1매 구조 근사 기록, 베젤 다크 팔레트 유지 판단 기록.
- **DynamicEventUTK(246:141)**: P0 3건 — 코너 데칼 8×8(#58A6FF), 이벤트 유형 배지 58×19 @0.078/stroke@0.251, 발생 시각 '방금 발생'. 영상 플레이어 체계 신설 — 프레임 280×256 r7.2 stroke@0.251, 장면+그라데이션, 재생 44×44 r22, 탐색 막대 260×3(#58A6FF 65), 시간 Geist Mono 10, volume/maximize — 전부 정적(영상 소스 부재, 위조 금지 기록), PickingMode.Ignore. P1 4건 — 패널 312×396, fill #13181F/stroke@0.502, 장소 표기 장면 내부 이동, 제목 fs16. P2 2건 — 설명 fs13.2, 타이틀 0.8pt 근사. 게임 전용(타이머 카드/이동하기·무시) 유지.
- **기록만**: QuickSlotUTK — [U8 은퇴] 게이트 계약(사용자 지정 제거) → 수리 대상 아님 대조 기록 주석. MonsterInfoUTK — 매핑 정정 기록(101:5/110:14는 오버헤드 이름표 목업, 실 대응=NameplateOverlayUTK). UTKGaugeStripArc — 어휘 계약 PASS(형태 강요 금지 준수).

### 게이트 경계 내 컴파일 수리 (구현/QA 경계)
- DynamicEventUTK.cs:383 `Background{scaleMode}` → 1인자 Background(기본 ScaleAndStretch) [144차 P8-EV #5 보정2]
- HudBottomAnchorTests.cs GaugeSpacing 어서션 24.0→24.4 [144차 P8-HUD #3 승계] — JSON raw 갱신(같은 차 계약 갱신)

### 검증
- compile_test.sh: exit 0, error CS 0.
- 전수 EditMode: **total 1782 / passed 1770 / failed 12 — baseline 전 건(12개 테스트) 1:1 매칭, 신규 실패 0**. 집중 HudBottomAnchorTests 6/6(간격 24.4 갱신 포함). MeleeClickOwnership×2는 144차 이전부터 기록된 타이밍 민감 기저 계열(분리 조사 항목).
- QA 경계: 위임 자가검증 이후 부모(QA) 3차 재검증 — 1차(scaleMode CS0117)/2차(2인자 오버로드 CS1729) 컴파일 차단 2건은 구현 수리가 아닌 QA 게이트 경계 수리 기록.
- 미완료: Play 스크린샷 픽셀 비교(사용자 승인), 커밋(별도 승인). 다음: Phase 9(부재 창 마무리).

## [144차] Phase 9 — Figma 부재 창(D6) + 마무리 (2026-11-01, 텔레그램)

### 작업
- **환호 GLB 폴백 [144차 P9-ACQ]** — 143 감사표 §2-1① 잔존분 종결:
  · 갭 진단(TestOutput/acq_resolver_gap.txt): Publish 도달 id 전수 vs resolver 커버리지 대조 — 커버 유지(fish_glb_* 333/herb_*/crop_* 100/mat_wood·mat_stone 임시), **매핑 추가 3건(실GLB File.Exists 실측)**: fruit_apple→crop-apple.glb · flower_wild→angular-nature-flowering GLB(에디터 전용) · wood_dagger→UserProvided/wood_dagger.glb(MissingWeaponGlbKeys 제외 해제 — 130차 부재 판정이 현재 부정확). **폴백 대상**(GLB 실측 부재 → 위조 매핑 금지): iron_ore·mat_silver/gold/mythril_ore+미상 전부. meat_*/herb_seed_*는 Publish 미호출(연출 미도달, 기록만).
  · AcquisitionCinema 생략 계약 폐기: visual==null → CreateHandFallback — SpriteRenderer(ProceduralIconGenerator 기존 아이콘 서비스)+TextMesh(아이템명), 결정론(스케일 0.25m 고정, 아이콘 실패 시에도 텍스트 반드시 표시), 기존 _handItem 정리 경로 재사용. 로그 [144차 P9-ACQ].
  · 계약테스트 AcquisitionCinemaTests 5건 신규(경로 단정+File.Exists 실측+부재 id null 유지+폴백 소스계약).
- **WeeklyTaxWindowUTK 배선 보류 확정 [144차 P9]** — 진입 경로 후보(단일 영지 창 내부 버튼 vs 상점NPC 패널 5번 버튼) 사용자 지명 요청(10분 무응답) → 기존검증 최소설계 원칙으로 **기록만**. 소스 헤더 계약 주석 갱신(상점NPC 패널 신설은 81:134 Figma 1:1 위반 우려 명기).
- **D6 결정 기록 + 상태표 승계**: docs/144_figma_parity_audit.md §6 신설 — A그룹(실배선 24종)=현상 유지 기록(역반영은 사용자 지명 시 창 단위 별도 사이클), B그룹(미배선 22종+FastTravel+SleepUTK)=폐기 후보(실제 삭제는 별도 승인 게이트 — 기록 우선, SleepUTK만 선행 폐기 완료). 143 감사표의 승계처 확정.

### 검증
- 컴파일: compile_test.sh exit 0, error CS 0 (위임 자가검증 + 부모 마커 착지 확인 7/5/14건).
- 전수 EditMode: **total 1787 / passed 1775 / failed 12 = 기존 baseline 12건 1:1 매칭(AERO·CookingDatabase·Instrument×3·InvadedLord×2·MeleeClick×2·RecipeCatalog·ThemeUss + 병행WIP H2_Catalog), 신규 실패 0**. 신규 테스트 5건(+1782→1787) 전부 통과.
- **[144차 Figma 패리티 Phase 0~9 전체 완료]** — 남은 공통 게이트: ①Play 스크린샷 픽셀 비교(사용자 승인) ②커밋(별도 승인, 128~144 누적) ③B그룹 폐기 실행 여부(별도 승인) ④WeeklyTax 진입 경로(사용자 지명).

## [145차] 마을 설계도 5종+마을 GLB 46종 — 10마을 설계/배치 + Test_15 고산 검증 씬 (2026-11-01, 텔레그램)

- **계획**: docs/145_VILLAGE_5_DESIGNS_PLAN.md — 사용자 승인(표준 5종+고산 5종 확장+Test_15 검증 지정). 설계도 1~5 vision 판독(강변자급/종교교역/광산군사/성벽행정/숲속휴양) → 10마을 테마 확정.
- **P0 실측**: 마을 GLB 46종 파서 전수(TestOutput/village5_glb_audit.txt+detail.json) — 46/46 glTF 정상, 5~6.2k 삼각형/머티리얼 1/이미지 2(4K+2K)/minY=0. **핵심: 전종목 유닛 큐브 정규화(1unit 스케일) → 역할별 목표높이 스케일 계수 필수(주거 4~5.5/상점 5~6.5/랜드마크 8~14m)**. Bell Tower "(1)"=원본 완전 동일(중복→2인스턴스 배치로 전수 충족), 변형 1종(a5ae8861)은 별개 에셋→V2 제2종탑. Whimsical Fantasy Village=마을 통째 미니어처 디오라마(단일 병합 메시/봉우리 분산 근거)→V1 외곽 기념물 배치. Wooden Dock=4말뚝+데크 1m 모듈→**목교 세그먼트 재활용 확정**. 시스템 실측(TestOutput/village5_p0_systems.txt): LakeGenerator(_radius/_depth/_surfaceY/EnforceSurfaceAboveTerrain 리스크), TerrainPathGenerator(공개 경로 주입 API 부재→흙길은 별도 스트립 경로), TerrainLayerDef L4 dirt_path=west_dirt_albedo 실존.
- **P1 [145차 P1]**: Village5DesignCatalog.cs — 46종 BuildingDef(목표높이표 하드코딩)+10마을 VillageDesign(표준 5+고산 5, 배치 엔트리 82+44)+HighlandProfile(가우시안 고원+분지, **SampleHeight 단일소스** — 메시/접지 동일 함수)+전수 검산 API(FindMissingStandard 등). 좌표 설계=workspace 그리디 검산 0 위반(MinSpacing 6m+footprint 1.2×TargetHeight/광장 반경 6m/플롯 경계/프로파일 불변식/경사 ≤25°).
- **P2 [145차 P2]**: Village5DesignRenderer.cs — BuildAll(200m 피치 2줄)+표준 플롯(지면 패치)+고산 플롯(64×64 미니 지형 메시+MeshCollider 동일 샘플러+수면 원판+**Wooden Dock 1m 모듈 목교**+절차 지지 기둥)+프리미티브 폴백. 빌드 중 발견·수리: 월드/로컬 좌표 오프셋 누락(InstantiateAtGround 계약).
- **P2b [145차 P2b]**: ①렌더러 BuildPlot(string,Vector3) 단일 플롯 API 추출(BuildAll 동작 동일성 유지) ②Test_15 고산 분기 — SetupTest15VillageDrugEconomy: 기존 BuildVillageGroundPatch 자리에 H1 호수반도 고지 실배치, 생활기술 클러스터 앵커=고원 가장자리(계약 유지), 플레이어 텔레포트=브릿지 진입 하단, **생산 마을 건물 스킵 가드**(겹침 방지 — 마약 경영 상태 유지), H1 미발견 시 기존 흐름 폴백 유지 ③부모 직접 수리(위임 600s 타임아웃 후 빈 부분): 수면 머티리얼을 **메인씬 동일 WaterMaterialUpgrader.CreateNationTintedWaterMaterial**(Idyllic 워터)로 교체+폴백 유지, **Idyllic 흙길 신설**(BuildDirtPaths — 브릿지 상륙지→광장 직선+광장 링 24분할, west_dirt_albedo 텍스처 스트립, SampleHeight 접지 추종) ④계약테스트 3건 추가(BuildPlot API/Idyllic 소스/Test_15 분기).
- **검증**: 컴파일 error CS **0**(자가+부모 3회 — CS0103 MaterialHelper using 누락/CS1503 Transform 전달/테스트 리터럴 오염 자가 수리 포함) + EditMode 전수 **1807/1795 — 실패 12 = 기존 baseline 12건 1:1(병행WIP H2 포함), 신규 실패 0**. Village5DesignCatalogTests **20/20 GREEN**(46종 전수/유일성·랜드마크 1마을 1개·MinSpacing·광장 반경·프로파일 불변식·다리 육지착지·P2b 계약). 과정 기록: HTML 엔티티 일괄 치환이 테스트 자체 리터럴("&gt;" 단언문)까지 오염시켜 1건 실패→원복 후 집중 20/20→전수 재게이트 통과.
- **미완료/대기**: ①**사용자 Test_15 Play 판정** — 고도차(분지 호수→목교→고원 마을)+Idyllic 지면/흙길/호수+건물 접지 실감 확인 ②나머지 9마을 쇼케이스/프로덕션 반영(P4 별도 승인) ③LakeGenerator 완전 재사용(파라미터 주입)은 15씬 판정 후 ④커밋(별도 승인).

## [145차 후속] P3 쇼케이스 전체 빌드 — 10마을 한 번에 확인 (2026-11-01, 텔레그램)

- **사용자 지시**: "일단 모두 진행하고 한번에 확인하자" → P3 전면 실행.
- **[145차 P3-STD] 표준 5종 Idyllic 승격**: 지면 머티리얼을 고산과 동일 east_grass1_albedo 공용 팩토리로(폴백 유지) + 표준 플롯 흙길(외곽 진입→광장 직선+광장 링 — BuildDirtPaths/BuildDirtStrip 샘플러 파라미터화 공용화).
- **[145차 P3-NATURE] Idyllic 자연 산포**: Resources/IdyllicPrefabs Trees 122종/Bushes 88종 실측 → 나무 14(3종 사이클 6~9m)+덤불 8(0.8~1.2m) 마을당 결정론 산포(xorshift32 지터 — Random API 0건). 제약 검산: 건물 footprint+2m/흙길 3m/수면·분지/다리 폴리라인 밖/플롯 경계 내/서로 4m. 프리팹 로드 실패 시 스킵(자연물 위조 금지 원칙).
- **[145차 P3-TOUR] 쇼케이스 투어**: BuildAll(Vector3 origin) 오버로드 + PlotPitch 130m(2줄×5). Test_15 고산 분기에서 showcaseOrigin = 마을 중심+(300,0,−300)에 10마을 전체 빌드 — H1 게임 플롯(best.center, "_Game" 접미사 인스턴스)에서 도보 투어 가능.
- **부모 직접 수리 2건**: ①BuildAll 루트 존재 가드가 BuildPlot 선빈(테스트 씬 순서) 시 전체를 스킵하는 충돌 → 플롯 단위 멱등 가드로 교정 ②동일 설계 다중 인스턴스(게임 H1 vs 쇼케이스 H1) 플롯명 충돌 → BuildPlot suffix 파라미터(기본 시그니처 보존).
- **계약 진화(94차 규칙)**: Random 0건 계약 2건 → "Random API 사용 0건"으로(주석 언급 허용 — xorshift32 문서 주석이 문자열 계약과 충돌), Test_15 분기 계약 → _Game 시그니처 갱신.
- **검증**: 컴파일 error CS **0** + EditMode 전수 **1811/1799 — 실패 12 = 기존 baseline 1:1, 신규 실패 0**. Village5DesignCatalogTests **24/24 GREEN**.
- **사용자 확인 대기(단일 Play)**: Test_15 Play → ①H1 게임 플롯(분지 호수→목교→고원 마을+생활기술) ②남동쪽 300m 쇼케이스 10마을 투어(표준 5종 Idyllic+흙길+나무 / 고산 4종 호수·목교·흙길) — 건물 46종 전수·접지·과밀·자연감 일괄 판정.

## [145차 후속2] P4 접지감 패키지 A~E — "붙여놓은 느낌" 해결 (2026-11-01, 텔레그램)

- **사용자 질문**: "지형에 GLB를 부착하면 어색한 부분" → 원인 5건 진단(점 접지/지형 미순응/바닥 전이 부재/수면 하드엣지/섀도우 부재) → **A~E 패키지 사용자 승인** 후 전부 적용.
- **[145차 P4-PAD] A. 고산 지형 평탄화 패드**: Village5HighlandProfile.SampleHeightWithPads 래퍼 신설(기존 SampleHeight 무수정 — 계약 보존) — 패드 반경 75% 내 padY 평탄, +4m smoothstep 블렌드. 렌더러 고산 빌드 순서 재배치: 자연 산포 후보 선결정 → 패드 목록(건물 footprint/2+1.5m, 나무 1.6m, 다리 양끝 2.5m) → **메시/흙길/다리/건물/나무 전부 동일 래퍼 샘플러**(단일소스 계약 유지). 표준 플롯은 이미 평탄(기록).
- **[145차 P4-FOUNDATION] B. 기초 링**: 건물마다 코블스톤 실린더(footprint×0.62, 상면 지면+0.25 — 바닥 0.25m 파묻힘), empire_cobble_albedo(Idyllic, Resources 실존), 공유 머티리얼 캐시.
- **[145차 P4-DIRTDECAL] C. 베이스 디퍼링**: footprint×0.85 흙 디스크(west_dirt_albedo — 흙길과 동일 CreateDirtMaterial 팩토리 일원화), B보다 하층.
- **[145차 P4-SHORE] D. 수변 처리**: 분지 수변 24지점 링(15° 스텝), y>WaterY+0.3(수면 위 육지) 스킵 — 잠김 지점만, Idyllic Shore 프리팹 3종 사이클(Cattail_01/Reeds_01/Cattail_02 — 폴더 실측), 스케일 1.0~1.4, 로드 실패 스킵.
- **[145차 P4-BLOB] E. 블롭섀도우**: 기존 BlobShadow.cs는 전역 TerrainGenerator.GetHeightAt(seed42) 고정이라 미니 지형에 부적합 → 렌더러 로컬 정적 쿼드 섀도우(IndoorMaterialFactory.GetBlobShadowTexture/CreateBlobShadowMat 공유+static 캐시), 건물 footprint×1.6/나무 3.0, 사유 주석 기록.
- **계약테스트 +3**: SampleHeightWithPads 수치 어서션(코어=padY 정확일치/블렌드 밖=자연 1e-4 일치/밴드 내 범위/null 위임) + 패키지 소스계약. 기존 계약 파손 0.
- **검증**: 컴파일 error CS **0** + EditMode 전수 **1814/1804 — 실패 10 = 기존 baseline 부분집합(이번 게이트 flaky 2건 자연 통과), 신규 실패 0**. Village5DesignCatalogTests **27/27 GREEN**.
- **대기**: 사용자 Test_15 Play 일괄 판정(접지감 패키지 반영 상태).

## [145차 후속3] P5 완전 자연화 패키지 1~6 (2026-11-01, 텔레그램)

- **사용자 질문**: "더 자연스럽게 하려면?" → 원인/방법 6건 제시 → **1~6 전부 승인**.
- **[145차 P5-EDGE] 표준 플롯 가장자리 블렌딩**: 박스 패치 완전 제거 → BuildTerrainMeshCore 공용 미니 지형(표준/고산 단일 팩토리 65×65+MeshCollider) — 중심부 2옥타브 밸류 노이즈(진폭 ≤0.3m, id 해시 시드)+가장자리 smoothstep falloff(기저 GROUND_BASE−0.1). **표준도 고산과 동일 단일소스 샘플러**(StandardGroundSampler — 접지/흙길/기초/디퍼링/섀도우 전부 동일 함수).
- **[145차 P5-CURVE] 곡선 보행로**: CurvedRoads 수학 이식(진폭 2.5~3.5m/주기 1~1.5, 설계 id별 정적 캐시) + 세그먼트별 폭 변화(halfW 1.0~1.4) + 광장→랜드마크 가지길(곡률 0.8). 산포 제약도 곡선 폴리라인 판정으로 갱신.
- **[145차 P5-GRASS] 풀/꽃 산포**: 마을당 잔디 105+개별 꽃 16+FlowerMeadow 덩어리 2 — IdyllicGrassCover는 플레이어 추종 컬링 구조라 **미채용 판정**(근거 주석) → 정적 프리팹 산포, 콜라이더 제거.
- **[145차 P5-TEXMIX] 지면 텍스처 혼합**: east_moss_albedo 이끼 패치 6~8(지면추종 팬 메시 — 기복 클리핑 없음)+고원 스커트 empire_rock_albedo 바위 노출 3.
- **[145차 P5-CLUSTER] 나무 클러스터링**: 균등 링→클러스터 3~5개×3~5그루(가우시안 유사, 반경 3~6m)+단독 외곽 링 예약+덤불 페어링(1.2m 이내)+크기 분산 5~10m. 패드 재계산(빌드 순서 유지).
- **[145차 P5-WATERFX] 물·불 마감**: 수련/수련잎 7(Waterlily/LilyPads 5종 사이클, WaterY+0.02)+수변 바위 4(Rock_Big, 갈대와 각도 오프셋)+캠프파이어 화염 VFX_Fire_01_Big(0.35~0.5 부착, 파티클 재생 로그). 전부 실존 에셋만(위조 금지).
- **플롯당 GO 추정**: 표준 ≈241 / 고산 ≈280~300(상한 350 내).
- **검증**: 컴파일 error CS **0** + EditMode 전수 **1820/1808 — 실패 12 = 기존 baseline 1:1, 신규 실패 0**. Village5DesignCatalogTests **33/33 GREEN**(같은 차 갱신: 박스 패치 제거 확인 역전 어서션/크기 분산 5~10m 등 + 신규 6건).
- **대기**: 사용자 Test_15 Play 일괄 판정.

## [146차] 던전 1→2층 하강 수리(P0진단+P1수리) + 실내 미니맵 P2 (2026-11-05, 텔레그램)

- **계획**: docs/plans/2026-11-05_dungeon-floor2-outside-judgment-and-indoor-minimap.md — 사용자 승인("진행") 후 P0→P1→P2 순행.
- **[P0 진단]** 정적 검산(다각형 내부 판정 — 스폰/포탈 전부 내부, 후보 ①② 배제) + Cave146LevelProbe 배치 Play 프로브 3회 실측(run_cave146_probe.sh): ①GoDeeper 시 `_tier` 미초기화(부트 2인자 경유) → 2층 리빌드가 tier1(스케일 1.0, 바닥 21.6×7.0 실측) ②실내에서 ClampToGroundByHeight 월드 공식(GetHeightAt+1) 발동 → 플레이어 바닥 위 1.2m 영구 부유(y=2.21=GetHeightAt+2 정확 일치 실측) ③F2 스폰이 상승 포탈 2.2m 반경 내부(1.0m)+하강 직후 E프롬프트 타깃=채굴노드 실측. 카메라 (0,0,0) 고정은 프로브 배치환경 한계 미확정(에디터 재현 대조 필요). 프로브 부작용 수리: ArenaWindowUTK 배치모드 GLB 초상화 부트 크래시 → isBatchMode 가드 1줄.
- **[P1 수리]** CaveLevelDirector.SyncContext(territoryId,tier,floor) 신설+BuildCaveInteriorCore 초반 동기화+TeleportPlayerToFloorSpawn 2인자화 / PlayerMovement ClampToGroundByHeight·ApplySlopeAlignment에 UITransitionState.IndoorActive 게이트 / GetFloorSpawnPosition 2인자 tierScale 동행+F2 스폰 (−8.0,0.6)→(−7.0,2.2)(포탈 2.56m 이격 — 재상승 바운스 차단). 위임 600s 타임아웃 → 부모 폴백 검증(반영 전량 확인).
- **[P2 실내 미니맵]** IndoorMinimapBaker(상공 직교 RT 512² 캡처+MapToUv 순수함수+앰비언트 ×6 임시 상향/정확 복원) 신설, MinimapUTK 실내 브랜치(Refresh 내 분기 — UpdateMapCrop 본체 무수정, 영지/퀘스트/경로 숨김+핀·블립 유지, IsIndoor∨(IndoorActive∧HasCapture)), 훅 3곳(BuildAndSettleIndoor L409/CaveLevelDirector.BuildFloor/CaveInteriorTestSetup — Systems→UI는 FindCrossAssemblyType 리플렉션).
- **검증**: 컴파일 CS 0 + Cave 집중 27/27 + Minimap 집중 20/20 + IndoorMinimapTests 17/17 GREEN + 프로브 회귀 실측(F2 바닥 27.0×8.8m=×1.25 ✓, 플레이어 착지 y=1.08/feet 0.03m ✓, 스폰-포탈 이격 ✓) + EditMode 전수 1925/1913 — 실패 12 = 기존 baseline 1:1, 신규 실패 0.
- **대기**: ①사용자 Play 판정(테스트씬 1→2층 하강+실내 미니맵 표시) ②카메라 (0,0,0) 미확정분 — 유저 영상 대조 ③커밋(별도 승인).

## [147차] Phase A — 던전 팩 수용 파이프라인 (2026-11-05, 텔레그램)

- **계획**: docs/plans/2026-11-05_asset-rebuild-dungeon-interior-arena.md — B0(방 크기 고정 계약, tierScale 폐기·tier는 난이도 파라미터로 전환) 사용자 확정 포함.
- **P-A1 파서 감사**: DUNGEON EXPORT GLB 320종 전수(glTF JSON 파싱) — 파싱 에러 0, 삼각형 중앙값 314/최대 3,648(저폴리), 실스케일(벽 4×3×1.03m/바닥타일 2×2×0.13m/의자 0.95m), 모델당 텍스처 1장 임베드 → TestOutput/147_dungeon_glb_audit.txt.
- **P-A2 반영 경로**: GLB 320종+New Atlas.png → Assets/Resources/Models/Dungeon/ 복사 + RuntimeModelLoader.LoadModelByKey에 "Models/Dungeon/" 2차 폴백(UserProvided 무손상).
- **P-A3 임포트 계약**: Dungeon147ImportQuality(에디터 배치) — 스탠드얼론 아틀라스 BC7+quality100/maxTextureSize 4096 적용(141차 패턴), GLB 임베드 320종 실측 리포트(전부 New Atlas 1024² — 2048 초과 0, 포프로 계약 유지) → TestOutput/147_dungeon_import_quality.txt. 위임 600s 타임아웃 2회 → 부모 폴백 완료(CS0623 튜플 배열 초기화/TextureImporter API 교정 포함).
- **P-A4 매핑**: DungeonAssetCatalog 신설 — 소문자 접두어 규칙표 단일소스(Structural 105/Decor 71/Props 96/Furniture 39/Lighting 9/Misc 0 = 320), ResourceRoot="Models/Dungeon/", CategoryOf/NamesIn/TotalCount API.
- **검증**: 컴파일 CS 0 + Dungeon147CatalogTests 5/5 GREEN + EditMode 전수 1930/1920 — 실패 10 = 기존 baseline 부분집합(flaky 2건 자연 통과), 신규 실패 0.
- **대기**: Phase B(던전 테스트씬 리빌딩) 승인 — B2 바닥 방식(타일 그리드 vs 리스킨) 시안 후 사용자 Play 판정.

## [147차] Phase B — 던전 테스트씬 에셋화 시안 (2026-11-05, 텔레그램)

- **계획**: docs/plans/2026-11-05_asset-rebuild-dungeon-interior-arena.md Phase B — 승인("승인") 후 B0~B3 실행. 위임 600s 타임아웃 → 부모 폴백(전량 반영 확인).
- **B0 방 크기 고정(사용자 확정 계약)**: tierScale 3 지점(Core/입구스폰/층스폰) `= 1f` 고정 — tier는 난이도 파라미터로 전환, 기하 적용 폐기. 실측: CaveFloor 21.6×7.0 유지.
- **B1 에셋 벽**: CreateAssetWalls — 변별 4m 세그먼트 분할(결정론 사이클 Wall_1_1A/Wall_2_1A/Wall_3_1A), 잔여 구간 X스케일(Clamp 0.4~1.3), 밑면 접지, 입구 변 스킵, StripColliders(충돌=기존 박스 유지 계약), 로드 실패 시 박스 벽 렌더러 복원 폴백.
- **B2 에셋 바닥 타일**: CreateAssetFloorTiles — 2×2m 그리드(코너/중심 내부+꼭짓점 포함 규칙), 상면 +0.01(z-fight 회피), 콜라이더 스트립(충돌=CaveFloor 유지), 실패 시 레이어 스킵 폴백.
- **B3 소품 승격**: 횟불 비주얼→Torch GLB(Light+TorchFlicker 무수정, 폴백 로그 1행 플래그), 채굴노드→Stone=Rocks_1_1/IronOre=Crystal_1(루트·콜라이더·ResourceNode 무수정). 보석상자 무수정(기존 GLB).
- **검증**: 컴파일 CS 0 + Dungeon147|Cave 집중 39/39 GREEN + 프로브 회귀(F2 스폰 (−7.0,2.2) 착지 y=1.08 ✓, 바닥 레이캐스트 0.03m ✓) + 시안 캡처 판독(1층: 벽돌 벽/바위/횟불 렌더 정상 — 검은 사각형 2건=143차 포탈 장식 터널 박스 기존 요소; 2층 탑다운: 에셋 돌담 링/노드 반영, 바닥 타일 어두움+벽캡 부유 의심 — Play 판정 지점).
- **대기**: 사용자 Test_21 Play 판정(1→2층 하강+시안 외관) — 벽캡 3.2m↔에셋 벽 3m 정합, 바닥 타일 밝기는 판정 후 후속 수리. 커밋 별도 승인.



