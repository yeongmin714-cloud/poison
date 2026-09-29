     1|# 전투 애니메이션·활 조준·병사 RTS 개선 계획
     2|
     3|> 계획 전용. 사용자 요청대로 아직 게임 소스/씬/에셋은 수정하지 않는다.
     4|
     5|## 목표
     6|1. 1/2/3회 클릭에 맞춰 실제로 다른 근접 스윙 애니메이션이 재생되도록 한다.
     7|2. 활 조준 중 남는 노란 궤적선을 제거하고, 조준 시각과 실발사 방향을 일치시키며 화살 사거리를 현재의 약 2배로 늘린다.
     8|3. 병사가 드래그 선택됐을 때도 기존 부대 선택과 동일하게 우클릭 이동/공격을 수행한다.
     9|4. 병사 선택 상태에서 몬스터를 가리키면 UTK 커서 아이콘을 공격으로 바꾼다.
    10|5. 선택된 병사의 기존 푸른 원반을 없애고, Fluent/GitHub-dark 기반의 선명한 푸른 링으로 표시한다.
    11|
    12|## 현재 코드에서 확인된 출발점
    13|- `HumanoidClipDriver`의 Fist/Sword 경로는 아직 `Weapon_Combo_2` 한 클립의 플레이헤드를 3구간으로 나누는 `_comboStage` 로직을 실행한다. 따라서 클릭 횟수별로 독립적인 서로 다른 Animator state/clip 전환을 사용하지 않는다. 1/2/3회 각각 별개의 애니메이션을 원한다는 요구와 현재 구현을 먼저 조정해야 한다.
    14|- `Player_AC.controller`에는 `AttackCombo`, `AttackCombo2`, `AttackCombo3`, `WeaponCombo`, `Attack` 상태/트리거가 남아 있다. 이전 구현/주석은 서로 다른 시기의 동작 모델을 설명하므로, 컨트롤러 각 상태의 실제 `m_Motion` clip GUID와 전이 조건을 함께 검사한다.
    15|- `BowTrajectoryPreview.Ensure()`는 현재 no-op이고 `BowTrajectoryPreview.Kill()`은 기존 호스트를 제거한다. 그러나 `BowTrajectoryHost` 구현은 남아 있고, `PlayerCombat`에서 그 Kill을 언제 확실히 호출하는지와 씬에 이미 생성된 잔존 호스트가 재생성되는지 확인할 필요가 있다.
    16|- 활 실제 발사는 `PlayerCombat.TryBowShot()`에서 화면 마우스 위치로 만든 `ScreenPointToRay`의 방향을 사용한다(하향각만 제한). 하지만 활 발사 원점의 수평 방향은 `transform.forward`이고, 궤적 예측 호스트의 현재 구현도 `player.forward`를 수평 투영한다. 따라서 둘은 동일한 3D 조준 벡터가 아니며, 현재 예측 호스트가 다른 곳에서 재활성화되면 조준선과 발사 방향이 달라질 수 있다.
    17|- 사거리 상향 파라미터는 이미 일부 수정돼 있다: `ArrowManager._arrowSpeed=84`, `ArrowProjectile.GravityScale=0.15`. 반면 `BowTrajectoryHost` 상수는 `ArrowSpeed=70`, `GravityScale=0.22`로 오래된 값이며 예측과 발사 물리가 불일치한다. “현재의 두 배”는 파워 범위·각도·수명·충돌 거리와 함께 정확히 정의하고 맞춘다.
    18|- `GuardSelectionManager.SelectedGuards`가 선택의 단일 데이터 소스다. 우클릭 명령은 `RTSCommandSystem`이 지형이면 이동, `IDamageable` 적이면 공격 처리한다. 기존 직전 변경에서 카메라 재탐색과 부모 체인 `IDamageable` 판정이 추가됐으며 아직 이 수정의 최신 통합 플레이 검증은 없다.
    19|- 선택 몬스터 커서 분류는 `UTKCursorOverlay` → `HoverTargetClassifier`에 이미 Monster 종류 분기가 있고, 선택 여부와 조합해 아이콘을 정하는지 확인할 대상이다. 현재 코멘트/기존 규칙은 Ctrl+몬스터=정보, 비Ctrl=공격이다. 선택 중 강제 공격 커서 요구가 이 UX 우선순위를 바꾼다.
    20|- 병사 월드 선택 표시는 `GuardSelectionManager`의 `SelectionRingController`다. 이 컨트롤러는 `Custom/SelectionRing` 셰이더를 사용하고, 국가별 색상을 주입한다. 별도 IMGUI 선택 원반은 이미 생략 표시돼 있으므로, Play에서 보이는 “푸른 원반”이 실제 셰이더 링인지, `GuardPlaceholder.SetSelected`/이전 오라 경로인지 먼저 소스를 확인한다. `SelectionRing.shader`는 Additive 합성·두꺼운 띠·내외 글로우·펄스/회전 호를 제공하지만 밝기/두께가 디스크처럼 보일 수 있다.
    21|- 작업 트리에는 이미 `PlayerCombat.cs`, `GuardSelectionManager.cs`, `RTSCommandSystem.cs` 수정이 존재한다. 기존 수정·사용자 미추적 자료는 보존하고, 계획 수행 전 최종 diff 기준선을 확인한다.
    22|
    23|## Phase 0 — 기준선/요구사항 고정 (읽기 전용)
    24|1. 변경 중인 세 파일 diff와 기존 비관련 미추적 자료를 기록한다. 롤백/정리/`git add -A`는 하지 않는다.
    25|2. `Player_AC.controller`에서 각 공격 state의 motion GUID와 transition을 확인해 1·2·3타에 지정 가능한 실제 clip을 확정한다. `Weapon_Combo_2`를 3구간 분할할지, 1/2/3타 전용 상태/클립을 사용할지 코드 경로·컨트롤러 실재 상태에 따라 결정한다.
    26|3. “1회 클릭=1회 스윙”, “2회=1+2”, “3회=1+2+3” 버퍼 의미, 재클릭 입력 허용 시간, 무기별 동작(검/Fist/창/활)을 acceptance criteria로 고정한다.
    27|4. 활 “사거리 두 배”는 같은 조준각·파워에서 기존 도달거리 대비 약 2x를 목표로 하고, 비행 파라미터(84/0.15)의 실제 현재 상태부터 기준화한다. 사거리 테스트 지형/표적 거리 기준을 선정한다.
    28|
    29|## Phase 1 — 근접 클릭 수별 애니메이션 분할
    30|**대상:** `HumanoidClipDriver.cs`, `PlayerCombat.cs`, 필요한 경우 `Player_AC.controller` 및 애니메이션 에셋 메타/설정.
    31|1. 타격 판정·애니메이션 호출의 소유자를 하나로 정리한다. 현재 `PlayerCombat`의 공격 호출과 `HumanoidClipDriver.LastAttackTime` 폴링/콤보 트리거가 같은 클릭을 중복 처리할 위험을 차단한다.
    32|2. 1~3 타를 클릭 카운트/버퍼로 결정하고 각 타마다 서로 다른 Animator state/clip trigger를 발화한다. 클립 상태가 실제 Controller에 없으면 있는 척 전제하지 않고, Controller 배선 변경을 별도 작업 항목으로 명시한다.
    33|3. 각 타격의 hit frame을 해당 클립의 실제 타격 프레임에 연결하고, 단계별 타격은 정확히 1회만 발생시킨다. 4번째 클릭은 버퍼 넘침으로 3타 이후 다음 입력 사이클로 처리한다.
    34|4. Animator 미연결/무효 컨트롤러에서 중복 피해 없이 기존 fallback을 보존한다. Spear의 찌르기와 Bow의 Draw/Release는 근접 3타 루프에 넣지 않는다.
    35|5. EditMode/정적 테스트: 단계별 trigger 선택, 1~3 hit count, buffer limit, fallback, Spear/Bow 분기.
    36|
    37|## Phase 2 — 활 노란선 제거 + 조준/발사 방향 동일화 + 2x 사거리
    38|**대상:** `BowTrajectoryPreview.cs`, `PlayerCombat.cs`, `ArrowManager.cs`, `ArrowProjectile.cs`, `BowAimReticleUTK.cs`.
    39|1. 노란선이 나오는 실제 producer/host를 추적한다. `Ensure()` no-op만 믿지 않고 활성 호스트, 이전 persistent GO, legacy trajectory system 호출처를 검색한다. Draw 시작/Update/릴리즈/씬 초기화에서 모두 숨김·Kill이 보장되게 하고, 활 발사 외 UI 조준 링은 유지한다.
    40|2. 단일 조준 벡터를 정의한다. 발사 계산과 시각 리티클/예측 계산이 동일한 screen point→ray, 동일한 하향 클램프, 동일한 발사 원점을 사용하게 한다. 현재 궤적선은 노란선 제거 대상이므로, 런타임 선 대신 리티클 위치/방향과 발사 로그·테스트 표적으로 정합성을 검증한다.
    41|3. `ArrowManager` speed/power multiplier, `ArrowProjectile.GravityScale`, `_lifetime`, 궤적 예측 상수(사용 여부에 따라), 충돌/낙하 경로의 물리 파라미터를 단일 설정값 또는 동기화된 상수로 맞춘다. 중복 상수 70/84, 0.22/0.15 불일치를 제거한다.
    42|4. 같은 높이/각도/파워의 비교로 사거리 2x를 목표로 조정한다. 사거리 증가 후 수명 만료 전 도달 여부, 발사체가 지면·벽에 정상 충돌하는지 확인한다. 속도만 기계적으로 두 배로 올리면 공중시간/포물선·조준과 불일치할 수 있으므로 실제 범위로 판정한다.
    43|5. UI 리티클의 dot/brackets는 조준 지점을 나타내고 화살은 그 점으로 날아가야 한다. Test_10 표적 2~3개(가까운/먼/고저차)에서 중앙점과 화살 착탄 위치 오차를 비교한다.
    44|
    45|## Phase 3 — 드래그 선택 후 우클릭 이동/공격
    46|**대상:** `GuardSelectionManager.cs`, `RTSCommandSystem.cs`, `ContextCommandRouter.cs`, Test_10 setup; UI cursor overlay/classifier.
    47|1. 선택 source of truth는 `GuardSelectionManager.SelectedGuards` 하나로 유지한다. 부대 슬롯 선택과 화면 드래그 선택 모두 같은 selection list로 들어가야 한다.
    48|2. 드래그 선택 종료 후 일반 우클릭을 RTSCommandSystem에 전달한다. 현재 일부 경로의 Ctrl modifier/부대 모드 게이트가 우클릭 캡처를 막는지 입력 순서와 함께 확인한다. 좌클릭 드래그가 PlayerCombat 공격/패링으로 소비되지 않도록 기존 consume flag를 보존한다.
    49|3. 지형 우클릭은 선택 병사 이동, 적의 자식/부모 콜라이더 적중은 공격 명령. 선택 병사 수 0이면 command 명시 거부/기존 fallback 정책을 지킨다.
    50|4. 클릭 UI 위, 병사 선택 링 collider, 드래그 release, 상호작용 Ctrl-left 경로는 RTS right-click을 오염시키지 않는다.
    51|5. Test_10: ①드래그 선택→우클릭 지형 이동 ②동일 선택→우클릭 몬스터 공격 ③부대 슬롯 선택→같은 결과 ④MainScene 비교.
    52|
    53|## Phase 4 — 선택 병사 위 공격 커서
    54|**대상:** `HoverTargetClassifier.cs`, `UTKCursorOverlay.cs`, `GuardSelectionManager` 상태 조회.
    55|1. 기존 TargetKind.Monster 분류를 유지하면서 `SelectedCount>0` 상태를 UI 계층에서 참조한다. UI→Systems 참조는 기존 단방향 UI asmdef에 있으므로 Systems가 UI를 호출하지 않는다.
    56|2. 몬스터 위일 때: 선택 병사 없음은 기존 커서 동작 그대로; 선택 병사가 하나 이상이면 명시적으로 `attack` 커서로 표시한다(Ctrl 누름으로 정보 커서로 덮어써지지 않게 우선순위 정리).
    57|3. 빈 지형·아군/NPC/영주/일반 적의 아이콘은 기존 분류 규칙 유지.
    58|4. Test_10에서 선택 전/후, 몬스터 위/바깥, Ctrl 누름/해제 조합을 확인한다.
    59|
    60|## Phase 5 — 푸른 원반 대신 Fluent 고품질 푸른 링
    61|**대상:** `SelectionRingController.cs`, `Assets/Resources/FX/Selection/SelectionRing.shader`, 선택 표시 생성부. 기존 `RTSSelectionOverlayUTK`는 화면 선택 박스/수량용이며 월드 링 대체 대상이 아니다.
    62|1. Play에서 원반의 실제 source를 추적한다: `SelectionRingController` renderer/material, `GuardPlaceholder.SetSelected`, legacy selection texture/VFX 등 활성 표시를 구분한다.
    63|2. 선택 오브젝트당 디스크/채움 면은 끄고 바닥에 얇은 원형 띠만 남긴다. Fluent 스타일: 깔끔한 accent blue(`#58A6FF` 계열), 은은한 외곽 glow/낮은 alpha, 절제된 pulse. 기존 국가별 색상 요구가 유지돼야 하는지는 기존 함수/씬을 근거로 유지·통일 여부를 결정하고, 요청된 “푸른 링”을 기본 팀 선택색으로 한다.
    64|3. Shader를 GPU 저비용 절차 원형 라인으로 조정(기존 Additive `Blend One One`가 하얗고 면적감 강하면 premultiplied/alpha blend 검토). 링 quad collider는 제거 유지, Y-fighting/지형 경사 적응·캐릭터 스케일을 검증한다.
    65|4. 선택/해제/사망/이동/씬전환에서 링 생성·추종·파괴를 확인하고, 드래그 선택 박스는 UTK 표시, 병사 아래 선택 링은 월드 공간 표시로 역할 분리한다.
    66|
    67|## Phase 6 — 통합 검증 및 문서
    68|1. `./compile_test.sh`: 실제 성공 마커 + CS errors 0 확인.
    69|2. EditMode XML: `<test-run total/passed/failed>` 숫자를 판독하고, 기존 실패(요리 카탈로그·레시피 lookup·확률 제작)와 신규 회귀를 구분한다. 결과 XML이 없으면 통과라고 기록하지 않는다.
    70|3. Play: Test_10부터 1/2/3 타격 애니/버퍼/피해, 활 조준·사거리·노란선, 드래그 선택 우클릭 이동/공격, 커서, 링을 확인. MainScene에서 같은 RTS·UI 입력을 회귀 점검한다.
    71|4. 최신 소스/로그 증거에 따라 `QAPROGRESS.md`, `ROADMAP.md`에 pending/verified를 나눠 기록한다. 이번 요청은 계획만이므로 문서도 수정하지 않는다. 구현 단계 별도 승인 후에만 commit/push.
    72|
    73|## 완료 기준 (모두 충족 전 “완료” 아님)
    74|- 한 번 클릭은 1타 전용 모션/1회 피해, 두 번은 1→2타/최대 2회, 세 번은 1→2→3타/최대 3회(명시적 버퍼 범위 포함).
    75|- 활 드로/조준 중 노란 궤적선이 0개, 리티클의 조준 방향과 발사체 궤적/착탄이 같은 방향, 사거리가 baseline의 약 2배.
    76|- 드래그로 선택된 병사에 우클릭 지형 이동/몬스터 공격, 몬스터 위 공격 커서, 선택 시 고품질 푸른 링/비선택 시 0개 링.
    77|- 최신 Compile 성공, EditMode 결과 수치 보고, Test_10+MainScene Play 실측 성공.
    78|
    79|## 리스크/주의
    80|- Animator Controller의 state/clip 연결은 소스 코드의 `SetTrigger()`만으로 만들어지지 않는다. 상태/클립 GUID가 확인되기 전에는 클릭별 모션을 “배선돼 있음”으로 가정하지 않는다.
    81|- 현재 Controller에 남은 상태와 코드의 실제 선택이 일치하지 않을 가능성이 크다. Phase 1은 파라미터·전이·clip·hit timing을 함께 다룬다.
    82|- 잔존 사용자 수정 파일과 대용량 미추적 에셋은 보존한다. 이 계획은 계획서/현재 응답만 작성하며 프로젝트 코드를 변경하거나 커밋하지 않는다.
    83|- 사거리 2배는 파워·조준각 별 차이가 있으므로 하나의 값만 기계적으로 배수하지 말고 최소/최대 파워에서 대표 각도를 검증한다.
    84|- 과거 문서가 1~3 타 콤보 구현 완료라고 적어도, 현재 소스에서 실제 `Weapon_Combo_2` 분할 상태를 확인했으므로 이 계획은 그 구현과 컨트롤러를 재검증하는 것으로 시작한다.
    85|
    86|## 수정 예정 파일 범위
    87|- Core Systems: `Assets/Scripts/Systems/HumanoidClipDriver.cs`, `PlayerCombat.cs`, `ArrowManager.cs`, `ArrowProjectile.cs`, `BowTrajectoryPreview.cs`, `GuardSelectionManager.cs`, `RTSCommandSystem.cs`, `ContextCommandRouter.cs`, `HoverTargetClassifier.cs`, `SelectionRingController.cs`.
    88|- UI: `Assets/Scripts/UI/Toolkit/UTKCursorOverlay.cs`, 필요 시 `BowAimReticleUTK.cs`.
    89|- Animator/visual: `Assets/Resources/Animation/Controllers/Player_AC.controller`, `Assets/Resources/FX/Selection/SelectionRing.shader` 및 해당 `.meta`.
    90|- Tests: `Assets/Scripts/Tests/EditMode/` 신규/기존 combat/RTS tests.
    91|- Test target: `Assets/Scenes/TestScenes/Test_10_TerritoryCombat.unity` setup script `TestTerritoryCombatSetup.cs`; 씬 YAML은 셋업 변경으로 해결되지 않는 경우에만 최소 수정.
    92|
    93|