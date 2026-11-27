# Test_16 실내 전쟁: RTS 직접 명령·적 반격·연속 재타겟 구현 계획

> 실행 시 Phase별로 **한 번에 하나씩** 진행한다. 구현마다 좁은 파일 화이트리스트를 주고, 구현과 독립 QA를 별도 서브에이전트에 맡긴다. 우선 Phase 0 기준선/배선 확인만 수행하고, 각 구현 단계는 해당 단계의 diff와 증거를 검토한 뒤 다음으로 넘어간다.

**목표:** Test_16에서 사용자가 아군 병사를 RTS로 선택해 적에게 공격 명령을 내리고, 적 수비군은 아군을 반격하며, 적 한 명이 쓰러져도 아군이 남은 적을 계속 상대하게 한다.

**범위:** Test_16 전용 배선과 필요한 최소 공격 명령 재타겟 수정. 플레이어 직접 melee, 메인 월드 전쟁, 새 UI 설계, 경제, loot/resurrection 공용 규칙 변경은 범위 밖이다.

## 조사로 확인한 사실

- 현 Test_16 setup은 `Assets/Scripts/Systems/IndoorWarTestSetup.cs`에서 적 3명을 `West`/`HostileToPlayerFaction=true`/`SetHold(true)`로 두고, 아군 3명을 `East`로 둔다. `SpawnMySoldier`가 세 아군 모두에게 동일한 `(0,0,4)` 공격 명령을 자동 부여한다.
- `/mnt/c/Users/korea/AppData/Local/Unity/Editor/Editor.log`에 기록된 한 실행에서는 세 아군 모두 `IndoorEnemyDefender_2`를 공격했고, 해당 적 HP가 13→7→2→-4로 줄어 사망 처리/경험치 로그가 발생했다. 이후 세 아군 모두 `공격 대상 사망/상실 → 명령 해제`를 남겼다. 이 로그는 그 실행의 동작 증거이며, 사용자 최신 Play와 같은 소스 상태라는 보장은 없다.
- `GuardPlaceholder.ExecuteMovement()`는 공격 명령이 이미 타겟을 확보한 뒤 대상이 사망/무효화되면 `ClearCommand()`하고 종료한다. `ResolveAttackTarget()`은 공격 지점 근처의 유효 적 중 가장 가까운 대상을 고른다. 이는 단일 적 집중과 사망 후 정지의 직접 원인이다.
- 적의 `TryHoldEngage()`는 다른 국가 병사를 대상으로 허용한다. 이전 로그에는 적 수비병의 `TryHoldEngage → PerformAttack → TakeDamage` 호출 및 아군 HP 감소가 있다. 적 반격이 항상 안 되는 문제인지, 최신 빌드/표시 문제인지는 최신 Play/로그로 재확인해야 한다.
- 기존 `GuardSelectionManager`는 드래그 선택과 우클릭 RTS 명령을 구현하며, 선택 후보를 포섭 아군(`IsRecruited`/`RecruitedSoldier`)에 한정한다. Test_16 setup에서 `GuardSelectionManager`/`RTSCommandSystem`을 확실히 보장하는지는 별도 확인 대상이다. `TestTerritoryCombatSetup.cs`에 둘을 보장하는 기존 선례가 있다.
- HP 표시: `GuardHeadUI`는 UIToolkit bootstrap에서 은퇴되고 `NameplateOverlayUTK`가 대체한다. 코드상 실제 피해/사망과 이름표/HP 표시 성공은 별도 사안이다.
- 워킹트리는 광범위하게 미커밋이다. 특히 `GuardPlaceholder.cs`, `RTSCommandSystem.cs`, `AttackFormationUTK.cs`에 선행 사용자 변경이 있으므로 baseline diff를 보존한다. 커밋 승인 없음.

## 위임 방식과 시간 초과 방지

- 위임당 제한은 600초로 취급한다. 시간을 늘리는 대신 감독자가 미리 메서드 위치, 계약, 허용 파일, 활성 테스트 assembly를 확인해 prompt에 넣어 에이전트의 재탐색을 줄인다.
- 한 위임은 한 논리 변경에 집중한다. 동일 파일을 만지는 태스크는 병렬화하지 않는다. 구현과 QA는 동시 실행하지 않고 순차로 분리한다.
- 구현 에이전트는 지정 범위 밖 파일 수정과 커밋/Play를 하지 않는다. Unity 컴파일/EditMode 결과는 별도 QA 에이전트가 신선한 로그/XML로 확인한다.
- 타임아웃 뒤에는 재위임 전에 allowlist 파일의 실제 diff/신규 파일/부분 산출물을 점검한다. 범위가 복잡하거나 공유 파일 위험이 발견되면 더 넓은 수정으로 몰래 진행하지 않고 계획을 갱신해 사용자 승인을 받는다.
- `GuardPlaceholder.cs`는 dirty 파일이므로 해당 단계에서 전체 현행 메서드와 baseline diff를 다시 읽고 필요한 hunk만 수정한다. 기존 사용자 변경을 덮거나 되돌리지 않는다.

## Phase 0 — 기준선 확인 (읽기 전용)

### 0A. 대상 코드와 dirty diff 확인
- 파일: 없음(읽기 전용).
- 확인: `IndoorWarTestSetup.Awake`, `EnsureGameManager`, `SpawnMySoldier`, `SpawnEnemyDefenseLine`; `GuardSelectionManager.Update`와 선택 필터; `RTSCommandSystem.IssueRightClickCommand`; `GuardPlaceholder.ExecuteMovement`, `ResolveAttackTarget`, `ValidateAttackTarget`, `TryHoldEngage`, `PerformAttack`; `UIToolkitBootstrap`/`NameplateOverlayUTK`의 병사 표시 경로.
- 활성 EditMode `.asmdef` 위치와 `AttackTargetRingTests` fixture helper를 확인한다. `.disabled` asmdef 아래 테스트는 사용하지 않는다.
- 시작 전에 `git status` 및 dirty hunk baseline을 기록한다. Unity 실행/코드 편집 금지.
- 통과 기준: Phase 1/2 각각의 구현 화이트리스트, 호출 경로와 테스트 위치가 정확히 결정됨.

## Phase 1 — Test_16에서 직접 RTS 명령 가능하게 하기

**범위:** `Assets/Scripts/Systems/IndoorWarTestSetup.cs`와 활성 EditMode 테스트 경로의 단일 wiring fixture. 기존 `GuardSelectionManager.cs`/`RTSCommandSystem.cs`는 수정하지 않는다.

### 1A. Setup 계약 RED
- Test_16에 RTS manager가 없으면 setup이 필요한 시스템을 보장하고, 이미 있으면 두 번째 인스턴스를 만들지 않는 테스트 가능한 좁은 helper/상태 계약을 만든다.
- 아군이 setup 직후 자동 공격 명령을 갖지 않는 계약을 검증한다.
- 가능한 경우 RED는 잘못된 동작에 대한 NUnit assertion이어야 하며, 컴파일/fixture 구성 오류를 RED로 오인하지 않는다.

### 1B. 최소 setup 배선 GREEN
- 기존 manager 인스턴스가 있으면 재사용, 없으면 기존 테스트 씬 패턴으로 생성한다. 새 입력 프레임워크나 새 전투창을 만들지 않는다.
- `SpawnMySoldier`의 즉시 `SetCommandTarget((0,0,4), true)` 호출을 제거한다. 병사 소속/생성/실내 위치/충돌체는 유지해 대기 상태로 둔다.
- Test_16만 드래그 선택을 허용하도록 `GuardSelectionManager.squadModeActive` 이전 값을 저장하고, 씬 이탈 때 복구한다. 적 수비병은 포섭 처리하거나 `RecruitedSoldier` 태그를 붙이지 않아 선택 필터에서 제외한다.
- manager를 Test_16이 새로 생성했을 때만 그 소유를 추적한다. 이미 있던 shared/`DontDestroyOnLoad` 시스템을 씬 종료 때 파괴하지 않는다.
- 테스트로 bootstrap/idempotence/초기 명령 없음과 teardown 상태 복구를 확인한다. 실제 마우스 raycast 성공은 Play-only이므로 EditMode 통과만으로 end-to-end 성공이라 하지 않는다.
- **실행기록:** `/mnt/c/Unity/code/Assets/Scripts/Systems/IndoorWarTestSetup.cs`에서 RTS 매니저 ensure, squadMode 임시 활성화·복구, 아군 auto-command 제거를 구현했다. 구현 테스트는 추가하지 않았고 별도 QA에서 실제 wiring fixture가 없어 Play 입력은 미검증으로 남겼다. Compile 결과 0 CS, 기존 근접 GuardSelection 테스트 2/2 pass(단 Test_16 setup 검증은 아님). 경고 150건 가운데 대상 파일에 CS0618 2건이 찍혀 후속 정리 여부를 평가한다. 증거: `/mnt/c/tmp/qa_test16_1b/compile.log`, `/mnt/c/tmp/qa_test16_1b/editmode_guardselection.xml`.
- **수명주기 판정:** read-only 검토에서 `GuardSelectionWatchdog`가 1초마다 누락된 `GuardSelectionManager`를 DDOL로 재생성하고, 저장소 선례(`TestTerritoryCombatSetup`)도 RTS 매니저를 DDOL 공유 서비스로 만든다. 따라서 이 두 매니저는 전역 공유 서비스로 유지하며 Test_16 종료 시 소유 정리하지 않는 편이 일관적이다. 단, 다른 씬에서 드래그 선택은 `squadModeActive` 원복 후 Ctrl로 시작해야 한다. 이를 넘어 워치독/매니저 씬 소유 계약을 바꾸려면 별도 승인·범위가 필요하다.
- **남은 주의:** 컴파일 로그에서 `IndoorWarTestSetup.cs`에 CS0618 2건이 발견됐지만 해당 라인은 lighting code(Phase 1A 이전부터 존재한 untracked 파일 내용)에 있다. 기존 baseline 컴파일 로그가 없어서 이번 작업이 새 경고를 유발했는지는 확정하지 않는다. 매니저 생성/실제 드래그·우클릭은 Play에서 아직 미검증.

### 1C. 독립 focused QA
- 별도 QA가 fresh compile 및 wiring fixture만 실행하고 log/XML 경로를 제출한다.
- Play/commit은 하지 않는다.

## Phase 2 — 표적 사망 뒤에도 공격을 이어가기

**범위:** `Assets/Scripts/Systems/GuardPlaceholder.cs`의 지정 attack-order/target lifecycle hunk와 대응 focused EditMode fixture. 파일 기존 dirty hunks는 보호한다. 다른 shared RTS 파일은 수정하지 않는다.

### 2A. 사망 후 명령 지속 RED
- 두 적 target A/B와 한 공격자를 구성한다.
- target A가 선택된 뒤 사망했을 때 명령이 즉시 없어지고 B를 찾지 않는 기존 동작을 assertion으로 재현한다.
- target을 못 찾은 경우, 새 move 명령, 명시적 stop/hold, 동국가/중립 대상도 분리 테스트한다.

### 2B. 최소 재타겟 GREEN
- 공격 intent/engagement anchor를 현재 target reference와 분리해 관리한다.
- 현재 target이 죽으면 target 참조/offset/resolved state만 초기화하고 공격 명령을 유지해 남은 alive 적을 다시 검색한다.
- 적이 없으면 공격 명령이 대기하고, 이후 적이 검색 범위에 들어오면 다시 획득해야 한다.
- 비공격 이동, `StopNow`, `SetHold(true)`, 새 명령이 들어오면 공격 재타겟 의도가 남지 않도록 한다.
- 적 판정은 기존 `IsEnemyGuard`/`HostileToPlayerFaction` 규칙을 따른다. 플레이어를 아군 병사가 공격하지 않도록 기존 검증 게이트는 보존한다.
- RED/GREEN 후 target fixture와 관련 기존 attack fixture만 독립 QA가 실행한다.

### 2C. 세 아군의 한 적 집중 완화 (조건부 별도 승인)
- 2B만으로는 한 번에 한 표적에 함께 때리는 경향이 남을 수 있다. 사용자가 직접 말한 “한 명만 공격” 체감을 바꾸려면, 현재 공격 대상의 점유 수를 고려해 덜 점유된 적을 택하는 분산을 검토한다.
- 구현 전 대상 선정 계약과 테스트를 2B와 분리해 제출한다. 가능하면 결정론적 기준(점유 수 → 거리 → 안정적 tie-break), 무작위 선택 금지.
- 타겟 공유 registry나 추가 전역 상태가 필요하거나 다른 전투/RTS 파일 수정이 필요해지면 이 조건부 작업은 중단하고, 영향 파일과 대안을 사용자에게 먼저 제시한다. 기본 계획에는 아직 포함하지 않는다.
- **실행기록(구현 완료, 커밋 전):** 사용자 승인 후 구현했다. `GuardPlaceholder`에 명시적 공격 focus API를 추가하고 `RTSCommandSystem`의 두 엔티티 클릭 경로(명시적 클릭 대상 고정)를 연결했다. 지면 앵커 대상 분산은 결정론적으로 (현재 대상 부하 최소 → 거리 → 이름 순서) 기준이며 무작위 선택이 없다. 추가 테스트: 공격자 3명/표적 3명 분산, 결정론성, 단독 표적, 공격자 3명/살아있는 표적 2명, 명시적 클릭 대상 고정 및 분산 edge. 이 항목의 증거는 아래 5A 최신 QA에 포함된다. 공유 registry나 전역 상태는 추가하지 않았다.

## Phase 3 — 적 수비군 반격을 보장하기

### 3A. 우선 검증 (테스트 파일 우선)
- 적 `HostileToPlayerFaction=true`/`SetHold(true)`가 이국 소속의 살아있는 포섭 아군을 공격하고 HP가 감소시키는지 테스트한다.
- 자기 자신, 동국가·중립, 죽은 대상은 제외하고, 사정거리 밖 표적을 공격하지 않음을 확인한다.
- 기존 Editor.log에는 일부 적 반격이 기록되어 있어 우선 no-change가 가능한지 판정한다.
- **실행기록(테스트 추가, 소스 변경 없음):** test-only `GuardHoldEngageTests.cs`와 meta, fake `Systems.IDamageable`을 추가했다. 검증 항목: 가장 가까운 살아있는 표적 선택, 사정거리 밖 제외, 사망 표적 제외, 쿨다운 gate. 독립 QA 신선 실행 4/4 pass (`/mnt/c/tmp/qa_test16_holdqa_retry/results.xml`). 이전 phase의 cooldown 테스트 7/7, 대상 재타겟 테스트 12/12, 명시적 focus 재시도 2/2 (`/mnt/c/tmp/qa_test16_explicit_retry/results.xml`)도 통과했다. Test_16 setup 자체의 반격 wiring은 여전히 Play 전까지 미검증이다.

### 3B. 실패 재현될 때만 수정
- RED가 setup에서 잘못된 nation/hostility/collider로 발생하면 `IndoorWarTestSetup.cs`만 최소 수정한다.
- shared `GuardPlaceholder.cs` 로직 수정이 필요하면 무엇이 공용으로 깨졌는지와 dirty hunk 충돌을 제시하고 범위를 먼저 승인받는다. 적에게 장거리 추격을 새로 넣지 않는다.

## Phase 4 — HP/사망의 화면 표시 확인

### 4A. read-only 표시 경로 (완료)
- 코드 경로를 확인: `NameplateOverlayUTK`가 `GuardPlaceholder`를 직접 수집해 `IsAlive`, `CurrentHP/MaxHP`로 표시하므로 `GuardHeadUI` 부착 자체는 필요하지 않다. `UIToolkitBootstrap`가 overlay를 ensure하고 `GuardHeadUI.s_retired=true`로 두어 overlay가 유일 표시 경로다.
- 화면 전체가 가려질 수 있는 코드 조건: `UIToolkitBootstrap.UIRoot == null`, overlay root 누락, `Camera.main == null`, `UITransitionState.AnyWindowOpen`, 0 크기 root; 카메라 투영 z가 음수인 개별 label은 숨긴다. guards에는 별도 거리 cull이 없다.
- 정적 경로는 일관적이나 Test_16 실행 중 bootstrap/root/camera/window state와 실제 HP label 가시성은 확인되지 않았다. 이 환경에서 Play 스크린샷/런타임 상태 증거가 없어 표시 결함을 확정하지 않는다.

### 4B. 표시 결함 재현 시만 수리 (현재 보류)
- 재현·근거 없음이므로 현재 UI production code를 바꾸지 않는다. Play 승인 뒤 실제 화면에서 label이 없거나 HP ratio가 갱신되지 않는 경우에만 결함 지점을 특정해 별도 좁은 수정/fixture를 계획한다.
- 수치 damage mechanics와 UI 표시를 같은 패치에 섞지 않는다.

## Phase 5 — 통합 QA 및 사용자 Play 게이트

### 5A. 독립 code QA (최신 결과로 갱신, 완료)
- 최신 통합 compile (이번 턴 fresh): `/mnt/c/tmp/qa_test16_final_latest/compile.log` — `error CS` 0, `warning CS` 0. 기존 phase5 compile 결과는 최신 결과로 대체한다.
- 최신 focused EditMode 통합 QA: `AttackTargetRingTests` + `GuardHoldCooldownTickTests` + `GuardHoldEngageTests` 합계 29/29 pass. 구성은 AttackTargetRingTests 18(distribution edge 포함) + GuardHoldCooldownTickTests 7 + GuardHoldEngageTests 4. XML `/mnt/c/tmp/qa_test16_final_latest/editmode_focused.xml`, 로그 `/mnt/c/tmp/qa_test16_final_latest/editmode_focused.log`.
- 세부 증거: GuardHoldEngageTests 4/4 (`/mnt/c/tmp/qa_test16_holdqa_retry/results.xml`), 명시적 focus 재시도 2/2 (`/mnt/c/tmp/qa_test16_explicit_retry/results.xml`).
- 주의: 최종 독립 QA 시점 dirty diff에 `RTSCommandSystem.IssueMoveCommandCore` 리팩터링이 보였으나 테스트되지 않았다. 이는 이 구현의 일부로 주장하지 않는다.
- Test_16 전용 wiring fixture는 여전히 없다. 인접한 GuardSelection 2/2는 Test_16 setup을 검증하지 않는다. 정적 분석상 실제 검증에는 seam 추가 또는 Play가 필요하며, seam은 추가하지 않았다. **Play 게이트 상태: 미실행**.
- 이전 전체 EditMode 746/737/9 결과는 Phase 3 수정 이전 산출물이므로 현재 전체 상태로 쓰지 않는다. 최신 전체 EditMode 결과는 없다.
- Compile / focused EditMode / Play는 각각 별도로 보고한다. Play는 실행하지 않았다.

### 5B. Play QA (별도 승인 필요)
- 승인 후 별도 QA: Test_16에서 아군 드래그 선택 → 적 수비병 우클릭 → 적 수비군 반격 → 양측 HP 감소/사망 → 한 적 사망 뒤 다른 적 재타겟 → 이름표/HP 표시 → 적이 다 죽었을 때 유효한 대기 상태.
- 스크린샷과 최신 Editor.log 구간으로 증명한다. Compile/EditMode 통과를 Play 증거로 대체하지 않는다.
- Commit도 별도 승인 전 금지.

## 미포함

- 플레이어 직접 근접 공격 입력/자동 조준 수리.
- 메인 월드 AI 전쟁 파이프라인, 새 Figma 전투 UI, 경제/밸런스.
- 공용 loot/resurrection 규칙 변경.

## 실행 상태

- **완료:** Phase 0A 조사, Phase 1A/1B RTS 배선/자동 공격 제거, Phase 2A/2B 타겟 사망 후 공격 intent 유지·재탐색, Phase 2C 결정론적 부하 분산 및 명시적 focus 구현(테스트 포함), Phase 3A 홀드 반격 테스트 4/4 추가(소스 변경 없음), Phase 3에서 발견한 홀드 수비 cooldown 정지 결함의 최소 수정, Phase 4A HP overlay 코드 경로 조사, Phase 5A 최신 fresh compile(0 CS, 0 warning) 및 focused EditMode 29/29.
- **미완료/조건부:** Test_16 전용 wiring fixture는 없음(seam 추가 안 함, Play 게이트). Phase 3의 실제 적 반격과 HP 감소의 Test_16 setup 계약은 focused fixture로 입증되지 않았으며, 실제 전투도 Play에서 검증되지 않음. Phase 4B는 표시 결함 재현이 없어 보류. 최신 전체 EditMode 결과 없음. 워킹트리에 테스트되지 않은 `RTSCommandSystem.IssueMoveCommandCore` 리팩터링이 있으며 이 구현과 무관하게 취급한다.
- **Play QA(Phase 5B):** 미실행 — 사용자 승인 대기. 드래그 선택, 우클릭 공격, 양측 HP/사망, overlay 가시성은 런타임 미검증.
- **Commit:** 미실행 — 별도 사용자 승인 대기. 모든 변경은 미커밋 상태.
- **Telegram:** 사용자 요청에 따라 진행 상황을 두 차례 전송했다.
