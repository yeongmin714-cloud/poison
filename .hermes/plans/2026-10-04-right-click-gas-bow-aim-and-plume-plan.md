# Test_10 우클릭 가스 이상효과·활 방향·가스 밀도 수정 계획

> **실행 기록 — 2026-10-04.** 사용자가 “진행” 승인 후 Phase 0~4를 순차 실행했다. 현재 구현/검증 사실 및 남은 Play 게이트는 아래 진행 결과에 기록한다. 이번 계획은 작업 이력이며, 아직 시각적 완료 승인을 뜻하지 않는다.
>
> Project: `/mnt/c/Unity/code` · Unity `6000.4.10f1` · 실제 확인 씬: `Assets/Scenes/TestScenes/Test_10_TerritoryCombat.unity`

## 목표

1. 우클릭 때 보이는 비정상적인 가스처럼 보이는 효과의 실제 생성 주체를 분리 재현해, 우클릭의 정상 RTS/카메라 동작은 보존하면서 잘못된 시각 효과만 제거한다.
2. 화살이 조준점을 바꿔도 한 방향으로만 나가는 현상을 실제 Test_10 발사 경로에서 계측하고 고친다. 화살 GLB는 제거하되 발사 방향 계산과 무관하게 처리한다.
3. 사용자가 이제 마음에 든다고 한 가스 디자인/실루엣은 보존하고, 시각 효과의 양을 늘리고 독 가스 색을 더 진하게 한다. 연료, 물약 소모, 피해/효과 주기, 범위 등 게임플레이 수치는 바꾸지 않는다.

## 지금 확인된 증거와 미확정 사항

- 새 `Screenshots/가스 분사 테스트 5.mp4`: 1240×828, 약 22.46fps, 34.88초. 파일 수정 시각 2026-10-04 00:16 KST이며 embedded creation time은 2026-10-04 00:15:49 KST이다. 해당 시점은 직전 가스 재질/카메라 코드 변경 이후로 보인다.
- 프레임상 밝고 부드러운 초록색 plume은 플레이어 근처에 보인다. 화면을 가로지르는 크고 옅은 초록색 지그재그 띠는 plume과 외형이 다르고, 일부 sampled frames에서는 plume 발생 전부터 존재하고 plume이 사라진 뒤에도 남아 있다. 따라서 그 넓은 띠를 ‘가스 입자’로 단정하지 않는다. 실제로 어느 우클릭 프레임에서 시작됐는지 및 화면상 책임 컴포넌트는 아직 확인되지 않았다.
- 우클릭 코드 경로 조사에서 `GuardSelectionManager`는 우클릭 down을 `RTSCommandSystem.IssueRightClickCommand()`에 전달하고 `TopDownCameraController`는 우클릭 drag를 카메라 조작으로 사용한다. 해당 입력 핸들러에서 `GasSprayer.StartSpray()`를 직접 호출하는 경로는 찾지 못했다. 이 사실만으로 다른 간접 호출/렌더링 경로를 배제하지 않는다.
- `GasSprayer`는 세 world-space particle layer를 만들고 투명 블렌딩 수정이 적용돼 있다. 현재 poison tint는 `new Color(0.62f, 1f, 0.12f, 0.5f)`이고 레이어 emission rate는 15/7/5다. 밀도/색 조정은 이 시각 설정에서 제한적으로 시험한다.
- `PlayerCombat`의 일반 활 계약은 좌클릭 홀드로 드로하고 놓을 때 발사한다. 최근 추가한 카메라 freeze(EditMode 12/12)는 조준 중 화면 projection 안정화 시도지만 사용자가 방향 문제가 여전하다고 보고했으므로 충분한 해결로 취급하지 않는다. 사용자 증상을 우선한다.
- `ArrowProjectile.Spawn()`은 현재 GLB `Resources.Load` 경로(arrow → arrow2 → arrow3)를 시도하고 실패하면 절차 생성 화살 형상으로 폴백한다. 요청은 GLB 불필요/삭제다. GLB 제거는 조준 로직을 고치는 단계와 분리한다.
- 현재 작업 트리는 매우 dirty하고 사용자 미추적 변경/미디어가 많다. 기존 변경은 보존하고 작업 대상 allowlist만 수정한다. `git add -A`, reset/clean, 자동 커밋은 금지한다.

## Phase 0 — 기준 보존·입력 재현과 효과 소유자 식별 (읽기 전용 우선)

**대상 소스/증거:** 최신 `Screenshots/가스 분사 테스트 5.mp4`, `Assets/Scripts/Systems/GasSprayer.cs`, `GasSprayerController.cs`, `SprayInputHandler.cs`, `SpecialEffectsController.cs`, `GasCloudLauncher.cs`, `GasCloudField.cs`, `GuardSelectionManager.cs`, `RTSCommandSystem.cs`, `TopDownCameraController.cs`, `TestTerritoryCombatSetup.cs`, 최신 `Editor.log` 및 현재 source/editor 상태.

1. 테스트5를 원본 프레임레이트로 우클릭 직전/입력/직후/해제 구간 추출. 플레이어, 포인터, 카메라, plume, 넓은 지그재그 띠, gas HUD를 구분해 타임라인을 기록한다. 화면 색/모양만으로 생성 컴포넌트를 단정하지 않는다.
2. Test_10 상태를 확인: gas controller의 `IsSpraying`, `LoadedPotionId/Count`, `IsEquipped/IsReloading`, 활의 실제 장착 weapon, 활성 카메라, RTS 선택/명령 상태. Editor.log는 이번 영상의 실행 시각과 대응하는 최신 부분만 읽는다.
3. 같은 씬/카메라에서 다음 입력을 **한 번에 하나씩** 재현해 화면과 transition 로그를 함께 남긴다: (A) 입력 없는 대기, (B) 우클릭 단순 클릭, (C) 우클릭 드래그, (D) 우클릭으로 유닛/지형 명령, (E) G 홀드만, (F) potion quickslot만. 각 전환에 frame/time, input source, player/camera pose, command target/result, loaded potion, `GasSprayer` emission, one-shot fog, `GasCloudField`, renderer/overlay 상태를 기록한다.
4. 넓은 지그재그 띠가 (i) 우클릭 시작에 반응하는지, (ii) 커서/카메라/선택 대상에 묶여 움직이는지, (iii) G plume과 무관하게 유지되는지로 분류한다. 그 결과로만 owner를 특정한다.

**Gate 0:** 이상효과 owner 또는 최초 생성 경계가 증거로 특정되어야 한다. 아직 특정되지 않으면 추측 코드 수정 없이 추가 재현으로 돌아간다. 수정 시 우클릭 RTS 명령/카메라 drag의 기존 의미가 보존됨을 수용 기준으로 명시한다.

## Phase 1 — 우클릭 부작용 최소 수정

**조건:** Gate 0에서 우클릭과 직접/간접 연결된 잘못된 경로가 특정된 경우만 수행.

1. 찾은 실제 입력/시각 경로에 대한 회귀 테스트를 먼저 작성하고 우클릭만으로 비정상 가스/띠가 생성되는 RED를 관찰한다. 동시에 정상 G-held gas 경로와 우클릭 RTS command/camera drag가 유지되는 테스트를 둔다.
2. 가장 좁은 원인(중복 listener, 잘못된 right-button mapping, stale preview/renderer, 잘못 연결된 one-shot effect 등)만 수정한다. 정상 G 분사, Mouse1이 지원되는 경우의 동작, 물약 quickslot, RTS 명령은 임의로 제거하지 않는다.
3. EditMode/compile 후 Test_10 Play에서 A/B 재현한다. 우클릭 단독에는 잘못된 plume/띠가 없고, G 단독에는 기대한 plume만 남으며, 우클릭 명령/drag가 보존됨을 화면 및 로그로 검증한다.

**추측 금지:** 현재 확보된 소스 경로상 RTS 우클릭이 직접 gas spray를 시작한다는 증거는 없다. 최초 재현 전에 `StartSpray()` 차단이나 넓은 띠 삭제를 먼저 하지 않는다.

## Phase 2 — 화살 조준 방향 근본원인 해결 (GLB와 별도 트랙)

**대상:** `Assets/Scripts/Systems/PlayerCombat.cs`, `BowAimState.cs`, `ArrowManager.cs`, `BowAimReticleUTK.cs`, `TopDownCameraController.cs`, `ArrowProjectile.cs`, `Assets/Scripts/Tests/EditMode/BowAimAlignmentTests.cs` 및 Test_10 런타임 weapon/equip setup.

1. 현재 Test_10에서 실제 Bow가 장착되는지 확인하고, 사용자가 말한 좌클릭 draw→release를 수행한다. 화면의 좌/우 또는 상/하 서로 다른 두 지점으로 각각 한 발을 발사한다. 영상의 포인터와 실제 draw/release를 동일 shot ID에 연결한다.
2. 한 shot ID마다 같은 이벤트에서 계측한다: 입력 경로/draw/release/frame, 실제 그려진 reticle 중심 픽셀(UI layout→screen 변환), `BowAimState` pixel, active camera id/pixelRect/pose, `ScreenPointToRay`, 선택 collider/hit 또는 fallback, muzzle, aim point, solver direction, 정확한 spawned Rigidbody 초기 velocity. FixedUpdate 이후 중력 궤적은 별도 기록한다.
3. 두 조준점 사이에서 최초로 값이 같아지거나 어긋나는 지점을 찾는다. UI sample stale/표시-샘플 변환, 카메라 view race/freeze 적용, 큐/패링 경로, 무기 입력 경로, 무시 collider/aim fallback, muzzle/spawn, 화면상의 trail을 각각 분리한다. 로그의 solver/velocity가 달라지는데 화면상 선만 같으면 방향 계산 대신 trail/시각 표현을 조사한다.
4. 해당 경계만 재현하는 행동 테스트를 RED→최소 수정→GREEN으로 만든다. 서로 다른 화면 조준점이 같은 release/launch 경로를 통과할 때 초기 velocity 방향도 서로 달라지고 각 aim ray를 향해야 한다. 실패 시 `transform.forward` 고정 폴백을 추가하지 않는다.
5. Play acceptance: 움직이지 않는 커서로 두 지점에서 발사하고, 초기 수 프레임의 화살 경로가 각각 해당 조준 방향에 맞는 것을 Test_10 녹화로 확인한다. 카메라 freeze만으로 고쳤다고 추정하지 않는다.

## Phase 3 — GLB 제거와 비-GLB 화살 표시 보존

**대상:** `Assets/Scripts/Systems/ArrowProjectile.cs`, 관련 EditMode 테스트, `Assets/Resources/Models/UserProvided/arrow.glb`, `arrow2.glb`, `arrow3.glb` 및 각 `.meta`.

1. 코드 참조/Resources.Load 사용을 재검색한다. 조사 시점에는 ArrowProjectile만 이 세 모델 경로를 직접 참조한다. 변경 직전 전체 저장소 참조를 다시 확인한다.
2. `ArrowProjectile.Spawn()`에서 GLB 검색·장착 경로와 GLB 전용 보정 함수를 제거하고, 현재 절차 생성 화살 몸체 및 TrailRenderer/physics를 유지한다. 화살 방향/발사 계산은 여기서 변경하지 않는다.
3. 절차 화살 메시의 축이 projectile 초기 속도와 일치하고 GLB 없이도 표시되는 회귀 테스트를 작성한다. Test_10 Play에서 실제 shaft/head/trail이 보이는지 확인한다.
4. 참조가 더 이상 없음을 확인한 뒤에만 요청된 세 GLB와 `.meta`를 삭제한다. 다른 리소스/모델 파일은 삭제하지 않는다.

**Gate 3:** GLB 파일 및 런타임 참조가 제거됐고, non-GLB projectile visibility/axis와 조준 solver 동작이 유지된다.

## Phase 4 — 가스 양 증가·색 진하게 (디자인 방향 보존)

**대상:** `Assets/Scripts/Systems/GasSprayer.cs`, `Assets/Scripts/Tests/EditMode/GasSprayerPhase4ATests.cs`; 필요 시 가스 재질/텍스처는 먼저 출력 비교 후에만.

1. 테스트5의 현재 디자인(부드러운 유기적 plume, 겹치는 puff, world-space trail)을 기준 캡처로 고정한다. 먼저 우클릭 이상효과가 정리된 별도 G-only 실행에서 출발한다.
2. 색은 기존 독성 녹색 계열을 유지하면서 밝은 lime-white에 치우친 인상을 낮춰 더 진하고 읽히는 녹색으로 조정한다. Alpha compositing은 유지하고 과도한 불투명도/밝기 클리핑을 피한다. 구체 RGB/alpha는 현재 화면과 reference를 같은 카메라에서 비교해 정한다.
3. 양은 세 레이어의 emission rate/size/중첩만 작은 단계로 조정해 plume coverage와 연속성을 늘린다. 균일하게 모든 값만 배수 증가시키지 말고, overlap·가독성·fill-rate를 확인한다. 사용자 마음에 든 실루엣/형태와 수명/월드 잔류는 우선 보존한다.
4. 회귀 테스트로 poison tint와 각 layer 설정을 고정하되, `ApplySprayEffects`, `_effectInterval`, fuel/dose consumption, range, `GasCloudField`와 mask/damage immunity는 변경 전후 동일함을 검증한다. 시각 emission 양을 게임플레이 tick이나 소모량에 연결하지 않는다.
5. Test_10 Play에서 같은 경로/이동/회전/카메라로 수정 전후를 비교한다. 평가: 전체 분사량, 색 진하기, plume silhouette, trail continuity, 바닥/캐릭터 가독성, stop/fade, 과밀/overdraw. 사용자 기준에 맞지 않으면 값을 더 올리는 대신 결과 프레임을 보고 원인을 다시 좁힌다.

**Gate 4:** 디자인 형태는 유지되면서 양과 녹색 채도가 눈에 띄게 개선되고, 프레임 가독성/성능 및 게임플레이 불변 테스트를 통과한다. 화면 확인 전 시각 개선을 완료라고 하지 않는다.

## Phase 5 — 검증·인계

1. 코드 변경마다 수정 파일을 특정한 strict TDD RED→GREEN, `compile_test.sh`, 관련 focused EditMode를 순차 실행한다.
2. 최종 full EditMode XML의 total/pass/fail과 모든 실패를 기록하고 기존 9개 실패와 비교한다. 전체 통과로 과장하지 않는다.
3. Test_10 실제 Play 영상/캡처와 해당 run Editor.log를 검토한다. 서로 다른 화살 방향 두 발, right-click isolated test, G-only gas 양/색을 각각 승인한다.
4. `docs/QAPROGRESS.md`와 루트 `QAPROGRESS.md`의 업데이트는 구현/검증 완료 후에만 한다. 이번 계획 작성은 plan file 한 개만 추가한다. 현재 dirty 작업 트리는 보존하며 commit/push는 별도 요청 전 하지 않는다.

## 완료 기준

- [ ] 우클릭만 재현했을 때 이상 gas/띠가 발생하지 않으며 우클릭 RTS/camera 동작은 그대로 작동.
- [ ] G-only continuous gas는 화면에서 의도한 plume/trail만 표현.
- [ ] 서로 다른 두 reticle pixel의 실제 활 발사에서 screen sample→ray→aim/muzzle→초기 velocity가 각각 일치하며 한 방향으로 고정되지 않음.
- [ ] GLB 3종과 호출 경로가 제거되고, 절차 생성 화살/트레일 및 physics가 정상 표시·작동.
- [ ] Gas plume 양과 색이 Test_10 렌더에서 사용자의 요청대로 개선되고 실루엣/가독성이 유지.
- [ ] 연료·dose·effect/damage cadence·range 및 무관한 사용자 변경 무변경 확인.
- [ ] 컴파일/focused/full suite 결과와 실제 Play 증거를 구분해 기록.

**현재 결론:** 테스트5의 대형 지그재그 띠는 plume과 시간적으로 독립돼 보이지만 실제 owner는 미확정이다. 활 카메라 안정화 코드 및 가스 투명 재질 상태는 테스트됐어도 사용자가 보고한 방향 문제와 렌더 품질을 아직 승인하지 않았다. 구현 전 Gate 0 재현이 가장 먼저다.

---

## 실제 대화형 구현 참고

다음 단계에서 명시적 구현 승인을 받으면 `systematic-debugging`과 `poison-unity-game-systems/references/gas-alpha-and-bow-direction-followup.md` 절차를 적용한다. 영상/로그/Play 검증은 코드 테스트와 별도의 승인 게이트로 취급한다.

---

(작성 당시 수정 대상은 이 계획 파일뿐이며, gameplay/source/assets/QA handoff는 변경하지 않음.)
