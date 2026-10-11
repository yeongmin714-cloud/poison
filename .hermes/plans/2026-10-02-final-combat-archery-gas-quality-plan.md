# 검 콤보 · 창 콤보/그립 · 활 조준 정렬 · 고품질 가스 VFX 통합 복구 계획

> **상태:** 계획 전용 문서. 사용자가 “계획을 자세히 세워” 요청했으므로 이 단계에서는 게임 코드·Animator·씬·렌더 설정·테스트·QAPROGRESS를 수정하지 않는다. 이 계획의 구현 승인으로 해석하지 않는다.
>
> **최우선 운영 원칙:** 이전에 성공했던 동작 계약과 사용자 수정사항을 보존한다. 회귀를 새 설계로 덮지 않는다. 모든 시각 결과는 Play 화면/영상으로 판정하고, Compile/EditMode 통과만으로 완료라 하지 않는다.

**목표:** 성공했던 `Weapon_Combo_2` 클릭별 분할 검 콤보를 복구하고 idle handoff 끊김만 고치며, 창은 실제 클립에서 찌르기/휘두르기 경계를 직접 확인해 같은 입력 계약과 별도의 타격 마커/그립을 적용하고, 활의 실제 발사 방향을 조준 방향과 정합시키고, 참조 영상 수준을 목표로 이동 경로에 잔류·확산·소산하는 고품질 가스 VFX를 제작·검증한다.

**프로젝트:** `/mnt/c/Unity/code`, Unity `6000.4.10f1`, URP `17.4`.

**작업축:** (A) Sword animation & hit segmentation, (B) Spear animation segmentation & grip orientation, (C) Bow reticle/launch/impact alignment, (D) Gas VFX reference match.

---

## 0. 계획 작성 시점에 확인된 현황과 증거 수준

### 0.1 작업 트리/보존 경계
- 브랜치: `master`.
- 코드·문서·테스트·수입 에셋·스크린샷/영상 등 수정·미추적 자료가 매우 많다. 현황 조회에서 전부를 이 작업 소유로 볼 수 없다.
- 기존 사용자 자료를 되돌리거나 정리하지 않는다. `git reset/checkout/clean`, 광범위 `git add -A`, 무단 커밋/푸시 금지.
- 이전 세션의 대형 계획 두 개(`.hermes/plans/2026-10-02-spear-bow-gas-vfx-plan.md`, `...spear-sword-animation-repair-plan.md`)가 있다. 본 문서는 실행 우선순위와 계약을 하나로 명료하게 합친 계획이며, 이전 두 문서는 읽기 전용 근거로 보존한다.

### 0.2 검 콤보 — 현재 소스 상태는 사용자가 말한 성공 동작과 반드시 재대조
- 현재 조사된 `Player_AC.controller`의 등록 상태 바인딩은 `AttackCombo → Double_Combo_Attack.fbx (0–85)`, `AttackCombo2 → Triple_Combo_Attack.fbx (0–130)`, `AttackCombo3 → Weapon_Combo_2.fbx (0–136)`이다. 현재 코드에서 Sword는 등록 상태 단계를 사용하고, `WeaponCombo`/`Weapon_Combo_2` 2.5x 경로는 별도다.
- 반면 사용자의 요구 및 이전 성공 기록은 `Weapon_Combo_2` 하나를 구간 분할해 click 1/2/3에 따라 해당 구간까지만 재생하고 각 구간별 hit를 내는 방식이다. 현재 코드·테스트와 요구 사이에 불일치가 있으므로, **구현 전에 과거의 성공 버전(현재/과거 소스, Animator 연결, 테스트, 세션 기록)을 확인해 정확한 last-known-good 설계를 복원**한다. 현재 working tree를 근거로 과거 성공 방식을 추측하지 않는다.
- Controller에서 `WeaponCombo`는 `Weapon_Combo_2`, speed `2.5`, 자동 전이 없음. 코드 marker/경계·click admission·idle 복귀가 하나의 일관된 timeline인지 검증 대상이다.
- 이전 결과: 특정 targeted combo EditMode `23/23` 및 registered melee `6/6` 통과, 최신 전체 EditMode `420/430` 통과. 이는 화면에서 클릭별 애니메이션/타격이 맞다는 증거가 아니다.
- 최신 full EditMode에서 `PlayerAttackSequenceTests.MultipleAcceptedAttacksAtTheSameTime_AdvanceSequenceForEveryAttack`가 실패함. 원인 분리를 해당 테스트 입력 타입/무기 경로/변경 회귀로 수행해야 하며, 성공하던 multi-click 동작이 회귀되지 않았다고 선언하지 않는다.

### 0.3 창
- `AttackThrust`는 `Thrust_Slash.fbx` (import 범위 0–90, non-loop)와 연결되어 있다. `Weapon_Combo_2`는 별도 클립이며 현재 후보 follow-up으로 사용된 흔적이 있다.
- 사용자의 확정 계약은 click 1=창 찌르기 1회 + hit 1회, click 2가 유효 시간 내 승인될 때만 찌르기에 이어 휘두르기 1회 + 별도 hit 1회.
- **창 클립은 검 클립과 프레임 경계가 같다고 가정하지 않는다.** importer frame range는 동작 의미 경계가 아니다. Unity Animation/FBX preview 또는 재현 가능한 프레임 캡처로 팔·창끝·몸통 궤적을 확인해 찌르기/전환/휘두르기 구간을 별도로 결정한다.
- 앞서 사용자가 명시한 문제: 창 그립/방향이 부정확해 창끝이 시선 정면으로 향하지 않고 첫 찌르기가 찌르기처럼 보이지 않는다. clip dispatch와 무기 부착 회전은 서로 다른 원인으로 조사하고 따로 검증한다.

### 0.4 활
- 최근 targeted `BowAimAlignmentTests` 결과는 `3/3` 통과했다. 이는 일부 geometry/source path에 대한 EditMode 결과이며, 실제 reticle 표시·발사체 비행·착탄 정렬을 증명하지 않는다.
- 현재 경로는 마우스 screen point → UI Toolkit `RuntimePanelUtils.ScreenToPanel` → 카메라 ray/aim point → muzzle에서 aim point를 향하는 초기 속도다. 카메라가 null일 때 전방 발사 fallback 경로가 있으며, 발사체에는 중력이 작용한다. 즉 “초기 발사 방향”과 “중력 적용 후 착탄점”은 구분해 판단한다.
- 사용자는 아직 “조준선 방향으로 화살이 가지 않는다”고 보고했다. 그러므로 해결 상태는 미해결로 고정한다.

### 0.5 가스
- 참고 영상은 `Screenshots/가스 살포 예시.mp4`, 984×556, 30fps, 약 4.10초다. 현재 읽기 전용 프레임 관찰상 독성 녹색의 부드럽고 불규칙한 구름이 캐릭터 이동 경로를 따라 남는다. 과거 구간이 즉시 캐릭터와 함께 재배치되지 않는 것이 핵심이다. world unit·정확 lifetime/opacity는 영상에서 계산하지 않는다.
- 현재 `GasSprayer`의 고정 emitter를 따라가는 ParticleSystem 경로는 월드 공간 파티클을 사용하지만, **사용자가 본 결과는 예시와 다르고 변경이 체감되지 않는다**. 현재 구현을 완료 기준으로 재사용하지 않고 렌더된 기준선부터 다시 촬영한다.
- `AeroGasSprayRuntimeHost`는 단일 국소 볼륨을 player transform에 따라 움직이는 구조다. 하나의 따라다니는 볼륨만으로 이동 궤적을 남기는 요구를 충족한다고 가정하지 않는다. 활성 MainScene renderer에 AERO 또는 프로젝트 소유 compositor가 실제로 연결됐다는 근거도 없다.
- AERO는 가능한 품질 후보이지 필수 제약은 아니다. 자체 VFX를 우선적으로 실제 시각 벤치마크하고, 필요하면 AERO와 비교한다. 어떤 패키지/브리지/파티클 카운터도 픽셀 수준 Play 판정을 대체하지 못한다.

### 0.6 알려진 전체 회귀 상태
최신 전체 EditMode 결과는 `TestOutput/final-editmode-rerun.xml`: 430개 중 420 pass/10 fail. 확인된 실패 이름/요약:
1. `AeroLocalizedGasPhase4BTests.IsolatedSessionWithoutConfiguredPipeline_LeavesGlobalPipelineSettingsUntouched` — 기대 Error 로그 미발생.
2. `AttackTargetRingTests.ExecuteMovement_AttackCommandClearsAfterResolvedTargetDies` — EditMode에서 `MonsterAggroSystem`의 `DontDestroyOnLoad` 예외.
3. `CraftBenchTests.CookingDatabase_AllRecipes_Loaded` — 760 기대, 실제 2024.
4. `PlayerAttackSequenceTests.MultipleAcceptedAttacksAtTheSameTime_AdvanceSequenceForEveryAttack` — 두 번째/후속 호출 기대치 실패.
5–8. `PlayerCastleInteriorTopologyTests` 4건 — EditMode Destroy 경고, footprint 허용오차, portal graph/opening assertions.
9. `RecipeCatalogTests.FindRecipe_UnknownCombo_ReturnsNull` — null 기대, RecipeDef 반환.
10. `ThemeUssSanityTests.ThemeUss_HasBakedBackgroundPngs` — `bg_window.png` 기대, 실제 Theme.uss.

이 열 건을 무조건 이번 네 축의 회귀라고 하거나, 무조건 baseline이라고 부르지 않는다. 변경 전 baseline이 없으면 분류는 “현재 재현된 실패/원인 미분류”다. 직접 관련된 활/콤보 실패는 먼저 분리한다. 관련 없는 테스트 파일/시스템은 사용자 승인 없이 대규모 수정하지 않는다.

---

## 1. 절대 acceptance contract — 구현 중 바꾸지 않는 행동

### 1.1 Sword
| 유효한 단발 클릭 수 | 화면 동작 | 타격 판정 |
|---:|---|---|
| 1 | `Weapon_Combo_2` 첫 휘두르기 구간만; 이어서 안정적으로 Idle 복귀 | 1회 |
| 2 | 첫 구간 + 두 번째 구간; 구간 경계에서 불필요한 Idle 틈 없음 | 각 구간별 1회, 총 2회 |
| 3 | 첫·둘째·셋째 구간까지 순차 진행 | 각 구간별 1회, 총 3회 |
| 0 / 4번째 / 쿨다운·입력 게이트 거부 | 승인된 swing/hit 추가 없음 | 0 추가 hit |

- click-and-hold는 여러 클릭으로 오인되지 않는다.
- 새 클릭의 수락과 이미 수락된 buffer의 dispatch는 별도다. 대기 중 accepted click은 새 슬롯이 아니다.
- 각 segment marker는 단 한 번만 실행된다. 시각적으로 세 번 휘두르는데 피해 1회이거나, swing 하나에 피해 2회면 실패다.
- 고칠 증상은 **첫 swing 뒤 Idle handoff 끊김**이다. 이를 고치기 위해 combo/클립/타격 판정 전체를 제거하거나 하나의 클릭으로 전 구간 자동 재생하지 않는다.
- 2.5x speed 및 사용자가 확인한 클릭 감시 속도를 먼저 last-known-good 기록에서 복원해 기준값으로 고정한다. 조정은 재현 증거·승인 없는 임의 숫자 변경 금지.

### 1.2 Spear
| 유효한 입력 | 동작 | 타격 판정 |
|---|---|---|
| 첫 클릭 | 실제 창 clip의 찌르기 구간을 보이게 재생 | 찌르기 1회 |
| 첫 클릭 후 계약된 window 안의 두 번째 click | 실제 clip에서 직접 확인한 이어지는 휘두르기 구간만 재생 | 휘두르기 별도 1회 |
| third click 또는 시간창 밖 click | 새 swing/hit 없음; 기존 재시작 정책이 있으면 증거대로 유지 | 추가 hit 없음 |

- Sword와 동일한 click sequence/queue 소유 원칙은 사용하되, **창 클립의 실제 프레임 경계, normalized start/end, hit marker는 별도**다.
- 창끝이 캐릭터 시선의 정면으로 향하고, 공격 전·중·후 손/손목 부착이 안정적이어야 한다. grip 위치/회전은 기존 `WeaponEquipManager` grip table / runtime attachment producer를 확인해 최소 수정한다. 애니메이션 root/hands와 무기 visual parent를 중복 회전시키지 않는다.
- `Thrust_Slash`의 0–90 importer range만 보고 찌르기/휘두르기 구간을 정하지 않는다. 실제 포즈 증거가 없으면 frame boundary 선택을 보류한다.
- 창의 follow-up clip/구간이 확인되지 않으면 검 동작을 복사해 창 동작인 척하지 않는다. 보유 자산/Animator mapping을 조사해 후보를 고르고 실제 자세로 판정한다.

### 1.3 Bow
- release 순간에 reticle이 표시하는 aim target/direction과 projectile **초기 발사선**은 일치해야 한다. 보이는 조준선의 의미가 target point인지 launch direction인지 먼저 producer를 확인해 일치시킨다.
- 카메라 미존재/비활성·잘못된 MainCamera인 상태에서 조준 실패를 침묵시킨 뒤 `transform.forward`로 발사하면 안 된다. 실패 시 명확한 fail-safe(발사 취소 또는 camera 재획득)와 로그/테스트를 정한다.
- 화면 좌표 변환/패널 배치는 중앙·가장자리, 서로 다른 해상도/비율에서 화면 pixel 단위로 확인한다.
- 초기 방향 일치와 탄도 후 명중은 별도 계약이다. 중력을 포함한 실제 impact가 목표에 맞아야 요구가 그렇게 정해진 경우만 탄도 solver를 추가한다. reticle이 launch direction 표시라면 중력 낙차는 예상된 곡선으로 시각화/설명하고, 직선 조준을 탄도 명중 보장이라 포장하지 않는다.
- 탄약 수/데미지/무기 레벨/화살 FX 등 범위 밖 변경은 금지한다.

### 1.4 Gas VFX
- 목표: 참조 영상의 인상(독성 라임/녹색, 불규칙한 부드러운 구름, 캐릭터 이동 경로에 누적, 새 가스가 진하고 이전 흔적이 넓어지며 옅어짐, 분사 중지 뒤 수명 기반 소산)을 사용자의 실제 MainScene 구도에서 확인한다.
- moving player에 붙은 단일 덩어리만 이동하거나 화면 전체를 녹색으로 틴트하면 실패.
- G 입력 주체 하나, controller state 하나. 시각 emission cadence는 gameplay effects tick(기존 0.5s), loaded dose drain, canister fuel drain과 분리된다. VFX 샘플 수가 피해/재고 소모 횟수를 바꾸면 실패.
- G up/focus loss/unequip/reload/depletion/scene exit 각각 새 emission 중지와 기존 segment fade 정책을 명시하고 검증한다.
- AERO 여부는 최종 비교 후 결정. 사용자의 “에셋 고집 아님”을 반영해 구현 선택을 강제하지 않는다. 다만 선택한 어떤 경로도 실제 화면 품질을 확인하기 전에는 완료 불가.

---

## 2. 비협상적 실행 규칙

1. 계획 승인 후에도 기존 dirty/untracked 자료를 원본 상태 그대로 보존한다. 작업 전·후 대상 파일 목록을 비교하고 파일 allowlist를 유지한다.
2. 수정 전 성공했던 Sword segmented 방식과 그 버전의 타이밍/타격 정책을 commit/session/테스트 근거로 확인한다. “기억나는 것 같다”는 근거가 아니다.
3. 한 번에 한 원인/축의 최소 변경만 한다. Sword dispatch/handoff와 창 grip, Bow geometry, Gas rendering을 한 patch에 섞지 않는다.
4. C# 변경은 가능한 경우 테스트 먼저(RED 확인) → 최소 수정 → GREEN. 테스트 runner의 exit code만 믿지 않고 XML과 testcase 수/실패명을 직접 확인한다.
5. Unity 프로젝트는 한 번에 하나의 batchmode 프로세스만 연다. 다른 invocation과 병렬 실행 금지(프로젝트 lock·로그/XML 경쟁 방지). QA code review만 독립 파일 범위에 한해 병렬 가능.
6. Compile, EditMode focused, EditMode full, Play visual/runtime, profiler는 별개 gate다. 결과물을 못 얻었으면 미검증으로 기록한다.
7. Play 증거는 저장된 영상/스크린샷과 Editor.log를 동일 실행·동일 변경 상태에 묶는다. 외부 사용자가 제공한 참고 영상은 사용자 자산으로 보존한다.
8. MainScene 공유 renderer/GraphicsSettings/QualitySettings/AERO vendor files는 건드리지 않는다. 가스 후보는 isolated copy/test scene 또는 GameSetup의 명명 기반 test mode로 먼저 평가한다. YAML 수동 편집은 금지.
9. 커밋/푸시는 명시 요청이 없으므로 하지 않는다. generated `TestOutput`, compile logs, screenshot/video는 git 변경/무관 자료로 취급한다.
10. 각 phase 종료 시 실제 변경과 증거를 기록하되, 사용자가 이번 요청에서 계획만 요청한 현 단계에는 문서 외 파일을 수정하지 않는다.

---

## 3. Phase 0 — 요구·last-known-good·참조 자산 고정 (분석만)

### 0.1 Preserve/identity snapshot
- 저장소 branch/status와 현재 대상 소스·테스트·컨트롤러·참조 영상의 timestamp/경로를 기록한다.
- 기존 파일이 modified/untracked인지, 새 계획/QA 결과인지 구분한다. 사용자 영상/에셋은 read-only.
- 복구 대상 baseline: `HumanoidClipDriver.cs`, `PlayerCombat.cs`, `Player_AC.controller`, 관련 tests의 직전 동작 버전. 가장 최근 commit 뿐 아니라 history에서 `Weapon_Combo_2` accepted-click segmented playback이 실제 구현된 버전을 검색한다. 해당 소스/테스트의 pristine copy를 읽기 전용으로 비교하고, unrelated current edits는 섞지 않는다.

### 0.2 Success contract evidence
- 성공 당시 저장된 Play 영상/Editor.log가 있으면 한 실행 단위로 대조: 입력 count, play state/clip, normalized time, segment boundary, hit event/target HP, Idle return.
- 기록이 없다면 이를 “성공 구현이 확인된 사실”로 꾸미지 말고, 우선 사용자 관찰을 acceptance authority로 두고 단계적으로 재현할 계획을 유지한다.
- 타이밍 값 후보 2.5x, 0.85s는 현재 코드/QA note에 존재하지만, 값이 사용자의 당시 성공값인지 last-known-good으로 확인될 때까지 고정 기준으로 단정하지 않는다. 소스·controller와 사용자 기록이 다르면 discrepancy를 plan issue로 기록한다.

### 0.3 Animator/asset map
각 상태마다 controller YAML의 transition 조건 → state → `m_Motion` GUID/fileID → meta importer clip 범위와 loop 설정을 표로 작성한다. 파일명만으로 어느 동작인지 추론하지 않는다.
- Sword: intended `WeaponCombo` / `Weapon_Combo_2` segmented route와 현재 `AttackCombo*` 등록 상태 route의 차이를 기록.
- Spear: `AttackThrust`/`Thrust_Slash`와 후보 follow-up clips를 mapping. 미리보기 캡처 없이 normalized boundary 확정 금지.
- 실제 visible character Animator / `HumanoidClipDriver`의 동일 Animator 여부 및 weapon attach parent/grip override를 확인.

**Gate 0:** 각 행동의 current, last-known-good, desired contract, missing evidence가 분리된 기록으로 정리된다. 이 phase에서 게임 파일 수정/테스트 실행은 없다.

---

## 4. Phase 1 — 통합 계측·재현 장면과 baseline

### 1.1 Reproduction protocol
- 실제 재현 MainScene/Test_10 및 MainCamera, player rig, equipped weapon을 확인한다. static test component와 runtime player object를 혼동하지 않는다.
- 재현에서 임의의 hit/movement source를 끄지 않는다. 우선 무피격 케이스로 애니 자체를 분리하고, 별도로 기존 피해/피격이 발생하는 케이스를 둔다.
- 장면 내 temporary instrumentation은 개발 전용·bounded·명시적으로 제거 가능한 구조로 설계한다. 사용자 씬/설정을 영구 변경하지 않는다.

### 1.2 Common event record
한 attack id / monotonically increasing sequence로 묶어 다음을 기록한다:
- physical mouse-down edge/time, PlayerCombat decision + reject reason, weapon type, accepted count/sequence;
- driver discovery/readiness, exact Animator instance and visible SkinnedMeshRenderer rig relation;
- current/next state full name/hash, actual `GetCurrentAnimatorClipInfo(0)` clip, normalized time, speed, transition progress;
- stage boundaries, buffered/pending inputs, dispatch result, hit-marker decision and one-shot guard;
- target identity + HP before/after per hit, duplicate legacy/direct hit path;
- hit reaction/HitLight/roll/parry/stun interruption with same attack id/time;
- Bow release geometry sample; gas particle/volume state separately.

### 1.3 Baseline shots
- Sword: exactly 1, 2, 3 separate clicks at several measured intervals; hold without a new edge; fourth click; no-target and target cases; miss/cooldown case; idle return. Capture front/side view plus target HP/log.
- Spear: one click/no extra damage; two timely clicks; late second; repeated button hold; facing changes; record hand/grip/world direction, clip normalized timeline and hit point.
- Bow: multiple resolutions/aspect ratios, near/far targets, left/right/high/low cursor, with/without camera at initialization/reacquisition, obstacles, fixed muzzle/fallback muzzle. Record crosshair, cursor, ray, launch and impact separately.
- Gas: the exact reference test path (move straight, turn, stop, resume, short hitch, terrain/doorway if practical) at fixed camera; capture current implementation and reference at matched timestamps. Measure profiler separately.

**Gate 1:** symptom can be reproduced and categorized without changing behavior. If a symptom cannot be reproduced, preserve evidence and do not “fix” a guessed mechanism.

---

## 5. Phase 2 — Sword segmented `Weapon_Combo_2` 복구 및 idle handoff

### 2.1 Restore correct ownership before tuning
- Last-known-good segmented route is the specification: one authored `Weapon_Combo_2` animation is played in click-authorized slices; accepted input count chooses the playback ceiling/segment availability. Do not replace it with one full clip per click, `AttackCombo*` state remapping, or unconditional complete-clip playback.
- Establish active dispatcher: physical click → `PlayerCombat.TryAttack` → sequence/capacity → `HumanoidClipDriver` → `WeaponCombo` state/clip normalized progress → exact strike marker → `ResolveComboSwingHit(stage)` once → target HP.
- Keep accepted sequence buffer and dispatch separate. The already-accepted event must survive a later driver Update without a second cooldown/window rejection; a pending event reserves click capacity.

### 2.2 Tests first
Add/adjust focused tests in the active EditMode assembly for:
- 1 accepted click authorizes first marker and clamps playback before second/third segments;
- second click authorizes second marker only; third authorizes third only; fourth rejected;
- each marker resolves once despite repeated Update samples or normalized-time crossing;
- same-frame accepted clicks remain separate monotonic events;
- a pending accepted delta is not admitted twice;
- rejected cooldown/UI/modal inputs do not advance sequence or hit;
- Sword segmented route is not replaced by another registered state path, and Bow/Spear routes remain separate;
- idle handoff after segment one is continuous for timely click, exits exactly once for no follow-up, and next fresh opener starts at clip time zero.

RED must be the intended assertion, not reflection type mismatch/source formatting. Fix tests to call current signatures with exactly matching boxed parameter types.

### 2.3 Minimal implementation after RED
- Restore `Weapon_Combo_2` segmented state-machine route from verified baseline; preserve authored clip GUID, current user’s asset import settings and input speed/window values.
- Isolate idle handoff defect by state transition timing: compare driver hold/exit normalized threshold with controller exit time/blend duration and clip sampling. The fix must remove only the unintended idle gap, not hold forever or jump to segment 2 without accepted click.
- One owner for actor movement during hit: preserve prior finding that simultaneous recoil/lunge/root movement can fight `PlayerMovement`/CharacterController. Only suppress the confirmed conflicting movement owner on combo-owned impacts; keep hit effects, damage and non-combo behavior unchanged.
- HitLight/other high-priority transitions retain current required behavior outside the committed attack policy; death/roll/parry/stun priority is tested explicitly.

### 2.4 Verification
- Fresh focused XML per suite; count >0 and check exact test names.
- Play test at the same stable target/camera: 1/2/3 clicks, no-gap segment chaining, per-stage HP delta=1, idle on no input, restart on new opener, no shaking. Compare recorded states to screen capture.

**Gate 2:** 1/2/3 click screen swings, clip slice limits, marker/hit count and idle handoff all pass in the same Play build. Any one failure blocks moving to the next visual completion claim; unrelated features may be developed only without changing Sword path.

---

## 6. Phase 3 — Spear actual animation segmentation + forward grip

### 3.1 Inspect motion before picking frames
- Open `Thrust_Slash.fbx` and every plausible spear follow-up clip in Unity Animation/Model preview (or a deterministic extracted animation frame/contact sheet with bone pose). Record timestamp/frame, silhouette, spear shaft axis, spear-tip position/direction and movement phase.
- Define semantic boundaries individually: (a) wind-up start, (b) forward extension/point reaches target, (c) recoil/return, (d) swing arc start/peak/end if the same clip contains it. Use the real clip frame count/sample rate and mapping; never copy Sword 1/3, 2/3, `.32`, `.44`, or `.50` as if equivalent.
- If the chosen clip has only a thrust and no swing, locate an authored compatible follow-up asset or author a validated spear swing segment; do not silently reuse sword animation as spear motion. The plan executor may decide only after seeing asset poses.

### 3.2 Input/dispatch state contract
- Use the Sword-proven accepted-click sequencing/buffering architecture, but per-weapon timing/normalized boundaries/hit markers are data specific to Spear.
- One click → thrust slice and one spear hit; second accepted within established input window → continue into swing slice and one second hit. Pending accepted input can dispatch after the window without reapplying expiry; pending click cannot permit a third new physical click.
- Spear clip completion/Idle transitions must not interrupt an accepted follow-up; no follow-up returns to Idle smoothly. Animator self-transition restart must explicitly restart at the correct normalized time when required.
- Preserve legacy immediate damage only if no ready driver owns the attack; never allow immediate fallback plus delayed marker to double-hit.

### 3.3 Grip alignment workstream
- Trace spawn/equip stack from actual item ID/rig to `WeaponEquipManager` grip table/`ApplyBoundsGripAlignment`/visual attach parent and character forward axes. Record local/world axes and weapon tip position before editing.
- Establish desired axis from player facing vector: shaft/thrust forward aligns with character aim-forward (document local axis and quaternion convention). Apply one minimal grip transform update at the correct weapon/id entry; avoid global mesh/model rotation that changes all weapon classes.
- Check every variant/fallback model resolved by the same visual ID. Verify that hand grip does not move the weapon behind wrist or invert blade/shaft, and that state transition does not detach/rotate the weapon differently.

### 3.4 Tests and gates
- Pure tests for per-clip normalized semantic boundaries and marker one-shot; test opener/follow-up clicks and pending dispatch/capacity separately; regression that sword boundaries/state route are unchanged.
- Play with no incoming damage, then with incoming damage. First click visibly points shaft/tip forward and thrusts, second click waits for actual thrust semantic completion then swings; each registers one hit. Test facing quadrants and stop/turn.

**Gate 3:** no frame/time boundary is accepted until tied to observed clip pose. If no clip asset supports two distinct motions, report the exact missing asset/validated substitute proposal in implementation checkpoint; do not invent uniform segmentation.

---

## 7. Phase 4 — Bow aim direction / actual projectile alignment

### 4.1 Define “aim line” from the live producer
- Enumerate current visible guide producers (`BowAimReticleUTK`, IMGUI `OnGUI`, trajectory preview, world `LineRenderer`/markers); identify exactly which visible line/reticle the user refers to. Do not call the UI cursor marker a trajectory predictor unless it computes the trajectory.
- State the contract in code/test names: screen reticle center corresponds to current mouse point; solver ray selects the first valid aim surface excluding shooter hierarchy; muzzle-to-target defines initial launch direction; gravity produces the actual arc. Whether a reticle means launch vector or predicted landing is fixed from the current UI design/user requirement before algorithm changes.

### 4.2 Isolate likely divergence points by measurement
- Capture same release event: display pixel, panel pixel, reticle actual rendered center, camera identity/pose, screen ray, obstruction collider/aim point, muzzle origin, launch direction/velocity, gravity, first collision/impact.
- Test if `_mainCamera` was null/stale at `Start`; ensure fail-safe does not silently fire player-forward. If camera reacquisition is necessary, only do so at the validated lifecycle boundary; don’t choose an arbitrary different camera.
- Test UI release sampling delay: immediate cursor movement at release should not leave reticle at prior 16ms location while shot samples new coordinates. Update/consume an atomic aim sample at input edge and use it for both UI display and shot where appropriate.
- Compare muzzle parallax and ray hitpoint. Do not “fix” by flattening Y, automatic enemy target assist, or replacing user input unless existing approved behavior explicitly requires it.
- For required impact alignment, use current ArrowProjectile physics values in a ballistic solver and actual collision tests. For a direction-only requirement, preserve straight initial vector and document gravity arc; do not overcorrect launch beyond target.

### 4.3 Test matrix and acceptance
- Pure solver tests: center/edge, near/far, high/low, obstacles, shooter child exclusion, null camera/invalid ray, selected muzzle/fallback muzzle, gravity/landing if solver required.
- Panel conversion tests with actual runtime panel coordinate assumptions are not source-text checks; use supported coordinate API and verify the transform across display sizes.
- Play screenshot/video: 16:9 baseline plus alternate aspect ratio, cursor center/edges, two target distances/heights, obstacle, camera absent/stale path. Measure reticle-to-cursor screen error, initial vector angular error and impact error separately. Define tolerances from resolution/pixel geometry before test; never claim hit alignment from same `GetAimScreenPoint()` function alone.
- Existing `BowAimAlignmentTests` 3/3 is a useful baseline but does not satisfy Play impact gate.

**Gate 4:** visual indicator and release sample align, initial arrow direction points along requested aim, observed collision/impact behavior matches the defined semantics. If gravity causes an intentionally curved miss, fix with validated ballistic calculation or make guide accurately convey the curve; do not conceal it.

---

## 8. Phase 5 — Gas reference breakdown and candidate authoring (isolated only)

### 5.1 Reference board and requirements
- Use extracted reference frames already inspected as visual targets; preserve original mp4. Make a timestamped comparison board for emission start, moving straight, turning, deceleration/stop, late fade.
- Write visual properties, not fabricated physical constants: local origin near rear/nozzle, path remains in world space, smooth irregular cloud volumes, new dense vs old broad/faded, lime semantic color, ground-visible gaps, depth/occlusion, no fullscreen wash.
- Unknown scale/lifetime remains parameterized. Choose candidate bounds after a Play camera calibration and measurement of player height, not screenshot pixel conversion alone.

### 5.2 Candidate A — project-authored high-quality moving path
Prototype in isolated test scene, no MainScene integration yet:
- One authoritative `GasSprayer` input/state publishes path samples. Sample by both traveled distance and maximum elapsed time so slow movement, fast movement, turns, and frame hitches form a continuous trail without gaps.
- Use bounded pooling and world-space simulation. Proposed layered treatment to evaluate: soft volumetric-looking base (noise/distorted alpha sprite or shader), denser irregular lobes, finer drifting wisps; age attributes control expansion, opacity, color/value and lifetime; emission direction/ground bias follow movement/heading while prior samples stay fixed in world coordinates.
- Provide soft depth-aware intersection/soft particles for terrain/character occlusion where URP depth is available; test the active renderer configuration rather than assume depth texture is present.
- Avoid enormous quad cards, per-frame material creation, one particle at arbitrary random positions, and a single camera-facing sheet as final quality.
- Provide performance/resource caps and scene teardown; visual sample frequency is independent from damage/tick/dose/fuel.

### 5.3 Candidate B — VFX Graph and AERO benchmark
- Create a separate cloned test scene/renderer configuration through project tooling/AssetDatabase, not hand-edit YAML and not alter shared `URPAsset.asset`, active RendererData, GraphicsSettings, QualitySettings or vendor AERO files.
- Verify candidate AERO shader’s actual density/noise/height/volume inputs and render path, package README instructions, active renderer feature availability and URP 17 compatibility. Distinguish vendor AERO from the current project-owned `AeroLocalizedGasRendererFeature`.
- AERO fullscreen raymarch/single-follow-volume can improve cloud appearance but does not inherently preserve a path. If used, path must be represented as age/density samples/segments and visually rendered as accumulated history; do not publish a single moving transform and call it a trail.
- VFX Graph 17.4 package presence is not proof. Build one GPU particle/trail prototype with explicit output and check actual pixels, alpha/depth and overdraw. Compare with authored ParticleSystem/shader path.
- Bench on target quality profile/desktop where runtime will occur. Record CPU/GPU time, render texture costs, draw calls/overdraw, memory/pool growth; never keep an effect that looks good only in isolated camera if MainScene camera path differs.

### 5.4 Candidate selection
Compare all selected candidates on identical path, camera, light and color settings. Criteria:
1. trajectory continuity, curvature/heading response and no emitter teleport;
2. old-vs-new density/lifetime gradient and soft irregular silhouette;
3. readable green cloud with ground/background still visible;
4. depth/terrain/doorway/character interactions;
5. start/stop/release/resume/depletion/scene cleanup;
6. actual capture similarity to reference assessed side-by-side;
7. stable profiler and bounded allocations/pools.

Do not preselect based on marketing name or package. The best-looking *working and integrated* candidate wins. If none meet the visual bar, iterate the prototype instead of choosing a visibly inferior fallback.

**Gate 5:** same camera path captures confirm a world-space trail and visible age fade. A payload test, material existence, VFX alive count or component presence does not pass.

---

## 9. Phase 6 — Gas gameplay integration and regression

### 6.1 Input/resource/data contract
- Inventory all G/M1 readers. Keep exactly one G owner (`GasSprayer.Update` or explicit existing InputAction), and decide/verify whether Mouse1 secondary hold remains supported without stopping G-owned spray.
- `GasSprayerController.IsSpraying` is the authoritative state. Define transitions: pressed, held, released, focus lost, UI modal, indoor transition, reload, unequip, dose empty, fuel empty, scene unload, respawn.
- Keep gameplay effect cadence `.5s` (unless existing acceptance source says otherwise); item dose/fuel accounting and G pause/resume semantics unchanged by VFX sampling. Test “one sample vs many samples → same gameplay damage/resource totals”.
- Bridge ownership: `AeroGasSprayBridge.Stop()` is global; current architecture assumes one emitter. Confirm no concurrent player/test emitter. If a scene can host multiple, use owner tokens/ref-counted publisher in a separately approved API, not silent global stop.
- GasMask/loaded potion/effect ownership remains distinct from continuous path VFX and legacy potion-use `GasCloudField` radial cloud.

### 6.2 Integration
- Only after candidate visual gate: connect final emitter to MainScene’s existing GameSetup path behind normal system bootstrap; do not turn on experimental renderer by ordinary G. If the chosen path is VFX Graph or isolated renderer, add explicit guarded scene setup with fallback behavior.
- Ensure no test-only components or editor-only asset loads are required in normal runtime. If an editor-only prototype is retained, label it and never represent it as the production effect.
- Test G release stops new samples immediately while residual trail age-fades; resume appends new path rather than moving old samples; depletion/unequip stops appropriately; scene exit clears owned objects.

**Gate 6:** gameplay effects/resources equal baseline under a fixed action trace, visual path is correct, and cleanup is leak-free in Play.

---

## 10. Phase 7 — Integration test scenes, full compile and suite repair

### 7.1 Scene strategy
- Use convention: duplicate MainScene to a clearly named isolated test scene and let `GameSetup` scene-name partial mode bootstrap only required systems, or use existing Test_10/Test_11 only after verifying their active setup. Do not hand-edit serialized scene YAML.
- Preserve MainScene active shared renderer. Temporarily selected isolated cloned renderer assets must be restored automatically/transactionally; no global persistent mutation.

### 7.2 Verification order (single Unity process at a time)
For each code change, in order:
1. focused RED proof (expected failure);
2. focused target tests and fresh XML with count > 0;
3. relevant regression suites (Sword/Spear/Bow/Gas), sequential Unity invocations only;
4. batch Compile fresh log, file mtime before log, CS error count zero and success marker;
5. whole EditMode assembly XML and every failure name/cause classification;
6. Play capture/log/profiler for visual behavior;
7. independent code/spec reviewer reads final exact files.

### 7.3 Whole-suite 10 failures
- Re-run focused associated suite after each fix. `PlayerAttackSequenceTests.MultipleAcceptedAttacksAtTheSameTime_AdvanceSequenceForEveryAttack` is directly relevant to attack sequence; isolate why and fix without weakening its intended acceptance contract.
- AERO missing expected log may be Unity log-capture/setup timing; diagnose the fixture and actual component callback, not production pipeline semantics.
- `AttackTargetRing` DontDestroy and interior topology, cooking catalog count, recipe lookup, theme baked PNG need compare to a pre-change baseline/owner. Only repair in this plan if a verified regression from these four features or explicitly authorized; otherwise record as independent unresolved failures, not claim full suite pass.
- After changing the `long→int` reflected test arguments, rerun tests; current full-suite XML is from before that correction and therefore is not final.
- If full EditMode remains non-green, report exact 430-test result and each remaining cause. Never report “regression 0” without baseline comparison.

---

## 11. Phase 8 — Full Play acceptance run (must be real editor evidence)

### Sword run
- Same character/controller/weapon and fixed target. Execute discrete 1, 2, 3 click sequences with measured click timestamps and separate no-click/late-click trials.
- For each: capture Animator current/next state+clip/normT and target HP per timestamp. Screen capture must show distinct expected number of swings; no idle gap between approved segments; no shaking on hit; after final authorized segment Idle once.
- Confirm click-hold doesn’t fabricate edges and fourth click does nothing.

### Spear run
- Fixed facing marker in world; capture front and side. Verify shaft points forward in idle/ready and tip advances on first thrust; ensure model isn’t merely swinging sideways due to attachment.
- Click 1 then separately timely click 2. Overlay clip timestamp/frame and marker logs on video. The follow-up must begin at the observed end-of-thrust boundary, not a copied sword normalized fraction.
- Verify one HP delta per swing and no double direct fallback; test all tested facing directions and weapon re-equip.

### Bow run
- Move cursor to known screen targets at near/far, center/edge, multiple aspect ratios. Video cursor/reticle/launch path; record target and impact in same run. Confirm launch line shares indicated aim, account for expected gravity, and show actual hit/miss with collision log. Verify null-camera behavior explicitly.

### Gas run
- Record direct camera captures of the identical reference-style path: stationary start, move straight, turn, stop while gas remains, restart, release and wait fade. Compare reference and candidate side-by-side at fixed timestamps.
- Confirm the plume remains behind the player, not attached/moving as one body; color/opacity silhouette, ground gaps and fade match reference impression. Record profiler in same run.
- Test no splash of global renderer, no stale particles after scene teardown and no gameplay dose/damage rate change when particle sample rate changes.

**Gate 8:** no Play screen capture/video means visual feature remains unverified. Compilation/test success cannot substitute.

---

## 12. Phase 9 — Final QA, records and stop rules

- Independent static spec review checks each acceptance table and every changed file, especially that prior Sword segmented playback/hit ownership is preserved.
- Independent runtime/scene wiring review checks actual MainScene GameSetup, actual attached Animator/weapon visual and actual chosen gas renderer path.
- Review only allowlisted changes; classify untouched dirty files as pre-existing/unrelated and keep intact.
- Update `QAPROGRESS.md` and applicable `ROADMAP.md` only after each accepted gate, with exact Compile/EditMode/Play status and unresolved failures. Do not mark phase complete if Play pending.
- Final report has separate rows: Implemented, static reviewed, compile, targeted EditMode, full EditMode, Play visual, profiler, docs, commit/push.
- Stop and report—not guess—if no valid spear follow-up asset exists, renderer output cannot be captured, MainCamera identity cannot be resolved, or existing baseline behavior conflicts with user contract. Present exact evidence and one narrow decision only when the user's choice materially changes gameplay/art direction.

---

## 13. Expected deliverables when this plan is later authorized

1. Restored click-count Sword `Weapon_Combo_2` segmented behavior, independently one hit marker per authorized swing, and no idle-handoff gap/stutter.
2. Spear click 1 thrust, click 2 timely swing with its own verified clip-segment boundaries and marker; correctly facing spear grip; no extra click or double damage.
3. Bow reticle/aim launch line alignment with verified camera/muzzle/release sampling and separately verified impact semantics.
4. High-quality custom world-space gas path that convincingly matches the supplied reference; use AERO only if isolated capture proves it improves the actual trail, not as a substitute by name.
5. Fresh source compile, precise targeted/relevant EditMode XML, full suite status with classified failures, Play evidence and profile, and accurate QA/roadmap record.

**Current status:** This file is a detailed plan only. No gameplay implementation is authorized or performed by writing this document.