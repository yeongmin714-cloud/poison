# 승인된 구현 계획 — 콤보·궁술·RTS 링/병사·가스·플레이어 성 실내

> **상태: 승인됨 / Phase 0 기준선 점검 진행 중.** 프로젝트 `/mnt/c/Unity/code`, Unity 6000.4.10f1, URP 17.4. 작업자는 기존 미커밋 변경·미추적 사용자 자료를 보존하며 요청 대상 파일만 수정한다. 작업 종료 시 QA 문서 반영과 변경 파일 지정 스테이징/커밋/푸시를 수행하되, 사용자 산출물·대형 에셋·스크린샷·로그는 스테이징하지 않는다.

## 승인된 사용자 목표

1. 하나의 `Weapon_Combo_2` 애니메이션 클립을 구간으로 나눠 1/2/3회의 클릭에 따라 각각 한/두/세 번 스윙한다. 입력 없이 뒤 구간으로 자동 진행하지 않으며, 클릭 수와 피해 횟수가 정확히 대응한다.
2. UTK 조준점을 실제 활의 목표점으로 사용해 해당 화면/월드 지점을 muzzle에서 향하도록 한다. 활 표시와 피격 feedback은 품질을 개선하되 중복 발화를 만들지 않는다.
3. 기존 골든 공격 링과 같은 형상·두께·펄스·가시성 구조를 재사용해 선택/지정 병사는 파랑, 명시 공격 대상 몬스터는 빨강으로 표시한다. 기존 링과 새 링의 중복은 제거한다. 지정 병사는 명시 정지/타겟 사망 전까지 공격 명령을 유지하며 타격과 HP UI 갱신을 확인한다.
4. G키를 누르는 동안만 가스 분사 및 dose/fuel 소비. 뗄 때 즉시 멈추고 잔량을 보존하며 재홀드 시 남은 dose에서 재개한다. AERO는 설치 여부가 아니라 활성 렌더러의 실제 시각 결과로 평가한다.
5. 플레이어 소유 성 내부만 기준 이미지 `Screenshots/자기소속 영주 실내 예시.png`에 맞춰 중앙 팔각 로비 + 북 침실 + NW 제작실 + NE 창고 + SW 병영 + SE 연금실 + 남쪽 출입구를 구축한다. 기존 제공 벽/바닥 텍스처를 쓰고 벽 두께/높이/측면/그림자와 실제 방 간 이동을 구현한다. 가구는 추후 배치 원칙을 존중하고 기존 상호작용 앵커만 방에 재배치한다.
6. 컴파일/EditMode/Play 결과를 각각 구분한다. 플레이하지 않은 현장 증거를 완료라 하지 않는다. 최종 QA 문서와 기록을 갱신한다.

## Phase 0 — 기준선(현재 진행 중)

- 현재 확인: 브랜치 `master`, upstream `origin/master`; 다수 기존 미커밋 수정 및 미추적 사용자 자산 존재. `compile.log` 자체도 추적 수정 상태여서 검증 시 덮어쓰지 않고 사용자 로그를 보존/복원할 수 있는 별도 로그 경로를 쓴다. `git diff --check`는 현행 사용자 `compile.log`의 trailing whitespace만 경고했다.
- 현재 활성 콤보 코드에는 `AttackCombo/AttackCombo2/AttackCombo3` 각기 다른 상태 클립을 호출하는 경로가 남아있다. 현재 코드와 오래된 ROADMAP 메모가 서로 충돌하므로 실제 컨트롤러 상태와 클립 GUID/길이를 대조해 교체 경계를 확정한다.
- 활, 병사/링, 가스, 실내 파일의 실제 동작 경로와 기존 사용자 수정분을 분리해서 기록한다. 이미 구현된 Phase 4A G 토글·링·활 변경은 의도와 다르면 승인 요구에 맞게 갱신한다.
- 성 기준 이미지와 현재 builder/Playtest scene을 확인. 현 builder는 48×36(면적 1728)으로 이미 과거 22×16의 약 4.9배지만 기준은 기존 ‘실제 작동 내부’ 면적이며, 새 도면 topology와 다르다. 요구의 약 6배는 우선 **실제 기존 walkable area의 6배 면적**으로 계획한다(선형 6배=36배 면적은 아님). 기존 구조 자체가 48×36 면적이라 물리적 확대가 과도/목표 해석과 상충하면 면적 수치/그림 scale로 확인한 뒤 실행 전 작업결정에 명시한다. 사용자 요청 없이 다른 경로로 확대 기준을 바꾸지 않는다.
- No Unity process/Temp lockfile was seen on the initial process scan; before any batch invocation verify again. MainScene renderer assets and installed AERO integration must be inspected rather than inferred.
- 기준선 단계에서는 gameplay code를 바꾸지 않으며 Unity Play 재현 증거가 없으면 해당 증상을 baseline reproduced라고 표기하지 않는다.

## Phase 1 — 단일 클립 분할 콤보

- 기존 controller `WeaponCombo` state의 실제 motion GUID를 `Weapon_Combo_2.fbx.meta`와 대조하고 원본 FBX/clip의 frame rate, duration, loop/import/avatar를 읽는다. 활성 source state를 전수 조사한다.
- 테스트 가능한 click/segment state machine을 분리한다: 물리적 click edge 1개=stage capacity 한 단위, 입력은 최대 3, hold 반복/초과 클릭 생성 없음; 첫 타는 바로 재생. segment 경계에서 버퍼가 없으면 playhead를 정지시켜 Idle로 마감하고, 다음 클릭 시점에만 재생을 이어간다. 2/3 클릭 시 요구된 prefix 구간 전체를 연속 재생한다. 입력 만료 이후 누적 클릭/피해는 금지.
- 기존 Animator transition이 playhead를 강제로 진행/loop시키면 `AnimationClipPlayable`/PlayableGraph 또는 다른 단일 owner 제어를 선택해 입력 전진 게이트를 강제한다. 비활성 Animator fallback은 한 번만 피해 처리하고 driver 중복 hit는 막는다. Bow/Spear 분기는 보존.
- 실제 3타 hit frame/구간 경계는 영상/클립 프레임 검사 후 수치 확정; 사용자의 에디터 Play 확인 없이 동작 성립 선언 금지.
- RED→GREEN EditMode로 클릭 수/segment progression, 단일 prefix 정지, 연속 2/3클릭, timeout, max click, hold, 중복 damage owner, Animator fallback 테스트. 다음 전체 compile/EditMode.

## Phase 2 — 조준점/muzzle/impact

- UTK reticle가 사용하는 동일한 화면 좌표에서 카메라 Ray와 ground/world target point를 한 번 계산. 실제 활/손 muzzle transform에서 그 world point로 발사한다. 각도 clamp는 aim marker와 projectile solve에 공통 적용한다.
- UI 좌표/panel scale, cursor sample 시각, muzzle transform, raycast point, launch direction/speed/gravity, 실제 impact point/error를 분리 로깅.
- 기존 reticle의 Fluent GitHub-dark palette/semantic ammo colors를 보존하며 layout/PNG/size는 정확히 source를 확인하고 변경한다. 이미 있는 Figma/베이크 자산을 무조건 다시 만들지 않는다.
- Projectile impact event는 기존 target receiver FX와 중복되지 않는 exactly-once 공통 계약으로 정리한다. 정상 적중/방패 차단/지면 박힘 별 정책 확인. 사거리 개선은 baseline 범위를 먼저 측정하고 재현 가능한 launch 조건으로 비교.
- EditMode + Test_10/MainScene Play에서 screen target vs muzzle trajectory vs impact을 별도 검증.

## Phase 3 — 링 + 공격 명령 지속 + HP

- gold ring의 producer/material/shader/size/ground projection/pulse/height follow/visibility와 의미를 먼저 찾는다. SelectionRingController/AttackTargetRingManager는 최근 변경된 사용자 파일이므로 diff를 보존하면서 공통 producer/config로 이관; tint 이외 구조 차이가 없어야 한다.
- Blue는 선택/명령 병사당 정확히 1개. Red는 명시적으로 지정된 현재 살아있는 AnimalAI target 당 정확히 1개, hover만으로 표시하지 않는다. 기존 gold 표시 의미는 보존/매핑 여부를 관찰 후 결정한다. Collider 없음, child collider→parent target resolve, terrain follow, death/deselect/stop/scene cleanup.
- `GuardCombatAI.UpdateGuardBehavior`의 attack branch에서 waypoint proximity로 `ClearCommand()` 하지 않도록 `AttackTarget` 명령과 transient approach point를 분리. target alive 동안 추격/공격, stop/dead target에서만 정리. 병사 공격 cooldown/hit resolver의 owner를 추적한다.
- Monster HP 이전/이후와 NameplateOverlayUTK/MonsterInfo UI의 동일 AnimalAI 비율을 확인. 각 strike당 1 hit, 공격 간격 이후에도 반복 감소해야 한다.

## Phase 4 — G hold + AERO feasibility

- 단일 물리 G reader에서 press/release 전이를 컨트롤러 `StartSpray/StopSpray`로 전송한다. 다른 씬의 테스트 입력, GasSprayer toggle, right-click handler 모두 실제 동시 배선 및 event owner를 확인하고 중복 입력을 막는다. Right-click 선택 동작이 별도면 유지 여부는 입력상충 현황에 기반해 같은 hold owner로 중계한다.
- release/focus lost/unequip/reload/modal/scene unload에서 fail-safe stop. `AdvanceSprayTime`는 spraying+equipped 동안에만 실행되어야 한다. pause→resume은 dose/fuel의 정확한 잔량으로 재개. UI는 요청한 held spraying 상태에서만 표시하고 잔량은 보존.
- AERO 과거 QA는 설치 자산이 오프라인 bake/fullscreen feature이며 Intel HD Graphics 530 1GB에서 실시간 volumetric 비용 우려가 있다고 적었다. 이 기록은 참고 근거이지 현재 활성 Renderer에서 실제 feature를 검증한 증거가 아니다. 우선 실제 AERO API/셰이더/RendererData/Graphics·Quality assignment 및 URP17 호환성을 점검하고, 격리 복제 Renderer/테스트 씬에서 localized density·깊이·advection/성능을 조사한다. 독립 spike 실패 시 root cause와 지표를 남기고 bounded particle/volume shader 고품질 대안과 적용 경로를 테스트한다. MainScene global renderer를 직접 바꾸지 않는다. AERO/대안은 실제 화면 Play evidence가 확보되기 전 완료 아님.

## Phase 5 — PlayerCastleInteriorBuilder

- 오직 `PlayerCastleInteriorBuilder.cs`와 필요한 IndoorBuilder/IndoorMaterialFactory helpers/test scene 쪽으로 제한. 비플레이어 `CastleInteriorBuilder` 미수정.
- 이미지 topology대로 named walkable zones 및 lobby-centered portals. 도면의 세부 길이는 측정한 6×area와 image aspect ratio, CharacterController clearance에서 유도. 벽체는 닫힌 3D solid masonry geometry, bevel/corner/reveal/stone albedo+normal/UV, floor flagstone seamless grid. 벽 양면 보임, top/side depth와 shadow.
- 제공 assets `Resources/Indoor/floor_flagstone(.png/_n)`, `wall_stone_lower(.png/_n)`, `wall_plaster_upper.png`, `wood_frame.png` 실제 존재 확인. 텍스처 반복/읽기와 material setup 확인. 국가 procedural generator가 기준 텍스처를 덮어쓰지 않아야 함. 복잡한 가구 추가는 defer, 모든 기존 interaction/quest/admin/save/storage/weapon/crafting/cooking/alchemy/barracks anchor를 올바른 zone으로 옮김.
- portal/door holes align with collider-free gaps and floors are connected; no hidden collider across doorway. Player spawn in lobby/foyer with full capsule clearance. Tests topology/area/connected graph/anchors/collider gap; dedicated Test_PlayerCastleInterior Play path outside→lobby→all five rooms→outside.
- 기본 확대기준은 Phase 0에서 산출한 baseline playable area의 최소 6배 면적. 실제 baseline/assets 보고 6×가 원래 build에 이미 충분히 반영돼 있음이 확인되면 면적을 무조건 또 6배 하지 않고, 새 도면 실내 면적이 목표를 이미 충족하는지 숫자로 계산한다.

## Phase 6 — 검증/문서/전달

- 각 작업별 compile, EditMode XML total/passed/failed+case, Play scene/scenario 결과를 각각 기록. 현재 이미 존재하는 compile.log와 editmode XML은 사용자 산출물로 간주하고 보존한다. Unity lock/process를 확인하고 새 temp log/result 경로를 사용.
- 독립 QA agent는 구현자와 같은 병렬 batch에서 돌리지 않고 실제 변경 후 순차적으로 검토한다. 모든 code edit/bug fix는 code agent+QA agent 방식으로 delegate; implementer가 scope를 지키도록 allowlist된 파일 경로만 제시하고 수정 파일 목록을 검증한다. 에디터 점유 시 batchmode가 잠기므로 점유/프로세스 확인 후 실행.
- `QAPROGRESS.md` 변경로그+미검증/실패/매트릭스, 해당 ROADMAP 항목의 정확한 체크 상태, 필요시 `PROGRESS.md`, `docs/CYCLE.md`를 갱신한다. 대형 문서 global checkbox replace 금지. 기존 날짜/요약을 현재 시점으로 갱신하며 과거 기록은 보존.
- 수용된 작업의 명시적 source/test/docs만 stage. Screenshots, GLB/FBX/Mirza/Magic Pig assets, compile/editmode logs, other untracked plans/scene art는 절대 stage하지 않는다. 변경 diff 리뷰와 test proof 확인 후 커밋/push하고 commit SHA 및 결과 확인.

## 승인/결정 기록

- 사용자 승인: “승인” — 상기 구현 범위 진행 승인.
- 6× 지표: 실제 기존 플레이 가능 면적 대비 **면적 6배**를 최초 목표로 삼되, 48×36 builder가 이미 이 영역을 달성했는지 baseline에서 검산. 선형 6배 확대는 승인에 포함하지 않음.
- 청색 선택 링: 병사가 선택/명령 수신 중인 동안 표시. 명령이 명시적으로 해제되거나 선택이 풀리면 생애주기 규칙에 따라 제거.
- 커밋/푸시: 종료 전 사용자가 요구한 QAPROGRESS·ROADMAP 등 문서 업데이트와 추적 변경의 scoped commit/push 진행. 기존 user-owned assets/output 유지.
- Play 증거를 제공할 수 없는 경우 비검증 runtime 기능은 솔직하게 pending으로 기록하고, 완료를 과장하지 않는다.

## 현재 단계 진행 로그

- 2026-10-01: 사용자 승인 수신. Phase 0 점검 시작.
- 2026-10-01: git status/diff baseline 확인. 수정/미추적 자료 다수, `compile.log` 추적 수정 및 trailing whitespace 포함. 유저 데이터 보존; 전체 stage/reset 금지.
- 2026-10-01: HumanoidClipDriver에서 세 상태 clip direct playback, PlayerCombat fallback/hit ownership 확인. GuardCombatAI attack arrival에서 ClearCommand하는 코드 발견. `GasSprayer.Update` G press toggle + `TestGasBombSetup` 별도 G poll 발견. AERO localized renderer feature source 및 UniversalRendererData 확인; actual active Graphics/Quality mapping은 미완.
- 2026-10-01: 기존 `PlayerCastleInteriorBuilder` 48×36 + nation procedural wall/floor textures, current layout topology differs from user reference; provided seamless stone/floor PNG+normal assets exist. Actual walkable area calculation and door connectivity remain to inspect.
- 2026-10-01: Unity Editor/Play/runtime test not run. Current source file changes not made in approval task yet; plan only updated to reflect approval and phase gates.

## 승인된 요청 전체의 검증 한계

모든 코드/Animator/씬 변경은 이후 phase에서 이 계획에 명시된 순서로 수행. compile green, EditMode pass, Play behavior are separate claims. AERO asset existence or a local RenderFeature class alone does not prove visible volumetric gas. A controller trigger/hit counter alone does not prove 1/2/3 visual swings.
