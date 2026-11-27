# 132차: 구르기/달리기 키 리바인드 + 방향성 구르기 계획

## 배경
- 구르기 클립 파생 완료(131차): `Assets/Animations/MeshyUser/Roll_Back.fbx`(시간반전), `Roll_Side.fbx`(바인드 90° 회전) — Humanoid 임포트 완료, 원샷.
- 사용자 확정: Q는 퀘스트 토글과 겹치므로 해방. 구르기=쉬프트+방향, 달리기=WASD 연타(더블탭).

## 키 체계 (확정)
| 액션 | 기존 | 신규 |
|---|---|---|
| 앞구르기 | Q (또는 더블탭) | **W+Shift** |
| 뒷구르기 | 없음 | **S+Shift** |
| 왼쪽 구르기 | 없음 | **A+Shift** |
| 오른쪽 구르기 | 없음 | **D+Shift** |
| 달리기 | Shift 홀드 | **WASD 연타(더블탭)** |
| 퀘스트 토글 | Q | Q (유지 — 해방) |
| 대쉬(스태미나) | Shift 홀드 | **트리거 없음(코드 잔존, 별도 지정 대기)** |

## 설계 결정
1. **더블탭=달리기**: 기존 `DetectDoubleTap`(구르기용)을 달리기 래치로 용도 변경. 더블탭한 방향키를 누르는 동안 달리기 유지, 떼면 해제. 달리기=Run 속도+Run 애니, 스태미나 소모 없음.
2. **Shift+방향 구르기 트리거(양엣지)**:
   - 방향키 wasPressedThisFrame && Shift 홀드 → 구르기
   - Shift wasPressedThisFrame && 방향키 홀드 → 구르기 (가장 최근에 누른 방향 우선 — 키별 최근 누름 시각 추적)
   - 방향 없는 Shift 단독 = 무동작 (스펙 그대로: 항상 방향+쉬프트)
   - W/S 우선순위 > A/D (세로 우선). 방향 벡터=카메라 상대(기존 GetDirectionVector 재사용)
3. **구르기 중 자세**: 앞구르기=현행 유지(이미 이동 방향 정렬). 뒷구르기=몸은 정면 유지(뒤로 이동+후방 재주넘기 클립), 옆구르기=몸 정면 유지(측면 이동+측면 재주 클립). 이동 회전 Slerp는 구르기 중 기존대로 스킵.
4. **탑승 중 가드**: StartRoll에 HandleJump와 동일한 `MountSystem.IsMounted` 가드 추가 — 탑승 중 Shift는 마운트 질주이므로 탑승자 구르기 오발 방지(기존 갭이지만 Shift 트리거가 노출 빈도를 높이므로 이번에 봉합).
5. **Q 완전 해방**: HandleRoll의 qKey 분기 제거. KeyBindings.Q(퀘스트) 무변경.

## 파일별 작업

### A. `Assets/Scripts/Systems/PlayerMovement.cs`
- L1027-1034: Q 구르기 분기 제거.
- L1022 HandleRoll: Shift+방향 트리거 추가(양엣지·최근방향 우선). Q 주석/클래스 문서(L14, C21-02) 갱신 — "Shift+방향키 구르기, WASD 더블탭 달리기".
- L965-999 DetectDoubleTap/CheckDoubleTap: StartRoll 호출 제거 → `_doubleTapRunLatched[4]` 래치 설정. 래치 유지 조건=해당 방향키 isPressed. `_lastKeyTime` 초기값 -10 유지(시작 직후 오인 방지).
- L876: `bool sprintKey = kb.leftShiftKey.isPressed` → `bool runActive = 래치 중인 방향키 존재 && isMoving`. L893-907 속도 분기: runActive && hasStamina && isMoving → **runSpeed(대쉬 아님)**, runActive && isMoving → runSpeed, else walkSpeed. `_isDashing`은 더블탭 경로에서 true 설정 금지(스태미나 소모 없음).
- L944-952 애니: `_isDashing`/sprintKey 분기 → runActive 기준 Run 상태.
- L1838 IsSprinting: `kb.leftShiftKey.isPressed` → 래치 기반 (사용처 grep 확인 후 갱신).
- StartRoll(Vector3 direction, RollKind kind) 시그니처 확장 + `public RollKind LastRollKind { get; }` 추가. RollKind enum {Forward, Back, Left, Right} public.
- StartRoll에 마운트 가드 추가.
- 더블탭 달리기 최소 간격: 기존 `_doubleTapTimeWindow`(0.3s) 재사용.

### B. `Assets/Scripts/Systems/HumanoidClipDriver.cs`
- L943-945: IsRolling 상승엣지 → `LastRollKind`에 따라 SetTrigger("Roll"/"RollBack"/"RollSideL"/"RollSideR").
- L598-618: `RollBackStateShortNameHash`/`RollSideLStateShortNameHash`/`RollSideRStateShortNameHash` 추가, IsHigherPriorityHitLightState에 포함(Roll과 동일한 HitLight 면책).
- L1631: 상태명 매핑에 RollBack/RollSideL/RollSideR 추가.

### C. 신규 `Assets/Editor/Tools/RollDirectionsAnimSetup.cs` (129차 PlayerActionsAnimSetup 선례 복제)
- 대상: `Assets/Resources/Animation/Controllers/Player_AC.controller`
- 트리거 파라미터 3종 멱등 추가: RollBack/RollSideL/RollSideR
- 상태 3종 멱등 추가: RollBack(Assets/Animations/MeshyUser/Roll_Back.fbx 첫 클립), RollSideL(Roll_Side.fbx + **mirror=true**), RollSideR(Roll_Side.fbx, mirror=false)
- 전이: AnyState(sm, state, 트리거) + ExitTo(sm, state, idle) — MixamoControllerBuilder L402-403 Roll 선례와 동일 패턴(AnyState/ExitTo 헬퍼를 이 스크립트에 로컬 복제 — builder의 private 재사용 불가)
- MenuItem + static Run() (batchmode -executeMethod 대응), 실행 후 검증 로그(상태/파라미터/전이 수)
- 클립 로드: AssetDatabase.LoadAssetAtPath<AnimationClip> — 이름 검색(FolderName 헬퍼) 대신 명시 경로 사용

### D. `Assets/Scripts/UI/TutorialActionDetector.cs`
- L188-196 "04: Shift 대쉬 감지" → 더블탭 WASD 감지로 교체(간단 카운터: 같은 방향키 2회 press within 0.3s). 튜토리얼 문구가 "Shift"를 언급하면 더블탭으로 표기 갱신(파일 내 문자열 확인).

### E. 테스트 — 신규 `Assets/Scripts/Tests/EditMode/PlayerRollRunRebindTests.cs`
소스 계약(기존 패턴 준용):
1. HandleRoll에 `qKey` 구르기 분기 부재 (Q 해방 계약)
2. HandleRoll에 `leftShiftKey` + 방향키(wKey/sKey/aKey/dKey) 조합 배선 존재
3. DetectDoubleTap/CheckDoubleTap에 `StartRoll` 호출 부재 + 더블탭 달리기 래치 필드 존재
4. StartRoll에 `MountSystem` 가드 존재
5. PlayerMovement에 RollKind enum + LastRollKind 프로퍼티 존재
6. Driver: LastRollKind 분기 SetTrigger 4종 존재 + IsHigherPriorityHitLightState에 신규 상태 해시 3종 포함
에셋 계약(Player_AC 로드):
7. 상태 RollBack/RollSideL/RollSideR 존재 + 트리거 파라미터 3종 존재
8. RollBack.motion=Roll_Back 클립, RollSideL/R=Roll_Side 클립, RollSideL.mirror=true
9. AnyState→신규상태 트리거 전이 + 신규상태→Idle ExitTime 전이 존재

### F. 검증 (QA 서브에이전트)
1. RollDirectionsAnimSetup batchmode 실행 → Player_AC.controller에 상태/파라미터 실측
2. 컴파일 게이트: error CS=0
3. EditMode 전수 → baseline 11건 1:1 (docs/AGENT_DELEGATION_POLICY.md 절차)
4. 신규 테스트 전부 GREEN
5. 기존 테스트 중 Q/Shift/구르기/달리기 언급 건 회귀 여부 확인 — 어설션이 구체계약(Q 구르기, Shift 달리기)이면 신체계에 맞게 갱신(실제 계약 변경이므로)

## 리스크 / Play 확인 항목 (승인 게이트)
- **Roll_Side 바인드 흡수 방향**: 바인드 90° 회전이 Unity Humanoid에서 의도대로 "측면 재주"로 보이는지, 시작 프레임이 정면 서있기인지 Play 육안 필요. 시작이 90° 틀어져 보이면 → 커브 쪽 보정(루트/힙 Y 회전 채널 추가)으로 수리. 좌우 반전은 mirror 플래그 스왑으로 저비용 수정.
- **Roll_Back 시간반전 감각**: 뒷구르기가 자연스러운 후방 재주로 보이는지.
- 더블탭 달리기와 이동 입력 충돌(래치 중 방향 전환 시 달리기 유지/해제 감각) — 필요 시 튜닝.
- 대쉬 트리거 부재 — 별도 키 지정은 사용자 승인 후.

## 승인 게이트
- Play 실눈 확인 + 커밋 = 별도 승인 대기 (기존 계약 유지)
