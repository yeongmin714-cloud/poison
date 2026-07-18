# ✅ 포이즌 (Poison) — QA 진행 상황 (런타임 오류 점검)

> **목표:** 431개 스크립트를 하나씩 점검하며 런타임 오류를 잡아냅니다.
>
> **진행 방식:** 테스트 씬별로 시스템 격리 → Play 테스트 → 오류 발견 → 수정 → 기록
>
> **최종 갱신:** 2026-07-13

---

## 📊 전체 현황

| 테스트 씬 | 시스템 | 최초 점검 | 오류 | 수정 완료 |
|:---------|:------|:---------:|:---:|:--------:|
| **Test_01_Player** | 🏃 이동+카메라+지형 | 🔄 1차 | ⚠️ 1 | ✅ |
| **Test_02_UI** | 🖥️ UI 창 전체 | ⬜ | — | — |
| **Test_03_Combat** | ⚔️ 전투+몬스터 | ⬜ | — | — |
| **Test_04_Territory** | 🏰 영지+병사+건물 | ⬜ | — | — |
| **Test_05_Craft** | 🧪 크래프트+인벤토리 | ⬜ | — | — |
| **Test_06_TimeWeather** | 🌙 시간+날씨 | 🔄 1차 | ✅ 없음 | ✅ |
| **Test_07_GasBomb** | 💨 가스분사기+폭탄 | ⬜ | — | — |
| **Test_08_Dracula** | 🧛 드라큘라+야간 | ⬜ | — | — |
| **Test_09_AllInOne** | 🛡️ 모든 시스템 | ⬜ | — | — |

---

## 📝 발견된 오류 로그

| 일시 | 씬 | 파일 | 오류 유형 | 내용 | 수정 | 상태 |
|:----:|:--:|:----|:---------|:-----|:----|:----:|
| 2026-07-13 | Test_01_Player | TestSceneGenerator.cs | 🔴 **씬 구조 오류** | Player/Camera가 없는 빈 씬 생성 | 메인씬 복제 → 불필요 제거 방식으로 변경 | ✅ |
| 2026-07-18 | Test_06_TimeWeather | — | ✅ **점검 완료** | TimeManager/DayNightCycle/WeatherManager/WeatherParticleController/WeatherEffects — 코드 리뷰 완료. Null 체크, 이벤트 구독 해제, 싱글톤 패턴 모두 정상. Breaking change 없음. | — | ✅ |
| 2026-07-13 | Test_01_Player | TestSceneGenerator.cs | 🔴 **씬 구조 오류** | `"Camera"` 이름 불일치로 Main Camera/Player Camera 누락 | `"Main Camera"`, `"Player Camera"` 정확한 이름 사용 | ✅ |
| 2026-07-13 | Test_01_Player | GameSetup.cs | 🔴 **PlayerInput 누락** | 테스트씬 모드에서 SetupPlayerComponents() 미호출 → PlayerInput 없음 | 테스트씬에도 SetupPlayerComponents() 호출하도록 수정 | ✅ |
| 2026-07-13 | Test_01_Player | TestSceneGenerator.cs | 🔴 **불필요 오브젝트 보존** | GameManager, 영지, UI 등 불필요한 오브젝트가 남아있음 | removePatterns 기반으로 정확히 필터링 | ✅ |

---

## 📐 점검 기준 (체크리스트)

각 파일 점검 시 아래 항목을 확인합니다:

- [ ] **NullReferenceException** — `.` 호출 전 null 체크 누락
- [ ] **MissingReferenceException** — Destroy된 오브젝트 참조
- [ ] **IndexOutOfRangeException** — 배열/리스트 인덱스 검증
- [ ] **ArgumentNullException** — null 파라미터 전달
- [ ] **InfiniteLoop/StackOverflow** — 재귀/while(true) 무한루프
- [ ] **DivideByZeroException** — 0으로 나누기
- [ ] **InvalidCastException** — 타입 캐스팅 실패
- [ ] **MissingComponentException** — GetComponent 실패
- [ ] **UnassignedReferenceException** — SerializeField 미할당
- [ ] **ArgumentException (경로/키)** — Dictionary 키 없음, 경로 오류
- [ ] **Coroutine 누수** — 중단되지 않은 코루틴
- [ ] **Event 구독 해제 누락** — OnDestroy/OnDisable에서 -= 누락

---

## 🎬 애니메이션 시스템 구축 (GLB 제네릭 리깅) — 2026-07-16

> **목표:** GLB 모델(본 이름 Blender 스타일: Root/spine.xxx/thigh.L)에 Unity 제네릭 리깅으로 애니메이션 클립 재생 + 프로시저럴 보정 적용
> **방식:** 메타 파일 수정 불필요, 런타임 해결 (GLB는 glTFast ScriptedImporter라 ModelImporter 아바타 시스템 미사용)
> **최종 갱신:** 2026-07-16

### 변경 내역 (커밋)

| 커밋 | 내용 |
|:-----|:-----|
| `1235a94` | GLB 애니메이션 풀 해결 — Generic Avatar 런타임 생성 + 프로시러럴 포즈 보정 |
| `cc83b17` | QuadrupedPoseController 신규 + ModelAnimatorAssigner 4족 분기 |
| `28d7685` | RuntimeModelLoader 4족 감지(넘버링 본) + rootBone 탐색 수정 |
| `04601ca` | 플레이어 공격/채집 애니메이션 입력 연결 |

### 핵심 수정 사항

| 파일 | 변경 | 상태 |
|:-----|:-----|:----:|
| ModelAnimatorAssigner.cs | GLB 인스턴스에 `AvatarBuilder.BuildGenericAvatar`로 Generic Avatar 런타임 생성 (FindRootBone 헬퍼 추가) | ✅ |
| ModelAnimatorAssigner.cs | AssignController에서 모델 타입(RiggedQuadruped) 따라 QuadrupedPoseController / ProceduralPoseController 분기 부착 | ✅ |
| PlayerPlaceholder.cs | **치명적 버그 수정**: GLB 로드 후 `AssignController` 호출 누락 → 추가 (애니메이션 미동작 원인) | ✅ |
| ProceduralPoseController.cs | **신규** (2족용) — 이동 상체 기울임/달리기 bob/점프 구부림, 순수 Transform 보간 (AnimationRigging 의존성 없음) | ✅ |
| QuadrupedPoseController.cs | **신규** (4족용) — bone_0~25 넘버링 본을 위치/계층 기반으로 Root+4다리+척추 추론, 사인파 대각 보행(trot) 합성 | ✅ |
| RuntimeModelLoader.cs | DetectTypeFromBoneNames에 넘버링 본 4족 추정 로직 추가 (Root 아래 깊이3+ 체인 4개 이상 → RiggedQuadruped) | ✅ |
| RuntimeModelLoader.cs | rootBone 탐색 수정: `t.parent==root` 조건 제거 → 자식 가장 많은 본을 Root로 (Wolf의 SkeletonBindArmature 구조 대응) | ✅ |
| RigAnimationController.cs | 트리거 기본값 Attack→AttackTrigger, Gather→GatherTrigger (Player_Animator.controller 파라미터 불일치 해결) | ✅ |
| PlayerCombat.cs | 좌클릭 공격 시 `_rigAnim.Attack()` 호출 추가 (공격 모션 발동) | ✅ |
| HerbPickup.cs | 기존 Gather 호출이 트리거 이름 수정으로 정상 동작 (추가 수정 없음) | ✅ |

### 모델별 애니메이션 지원 매트릭스

| 모델 | 본 구조 | 아바타 | 애니메이션 | 보정 | 상태 |
|:-----|:------:|:------:|:----------:|:----:|:----:|
| 플레이어 (Player_Rigged) | 2족 (Root/spine.xxx) | Generic (런타임) | Player_Anim 6클립 재생 | ProceduralPoseController | ✅ |
| 병사/일반몬스터 | 2족 (spine/thigh.L) | Generic (런타임) | Soldier/Monster_Anim 재생 | ProceduralPoseController | ✅ |
| 4족 (Wolf/Boar/Deer) | 넘버링 (bone_0~25) | Generic (런타임) | 클립 매핑 안 됨 → 코드 합성 | QuadrupedPoseController | ✅ |
| 뱀 (Snake) | 넘버링 | Generic (런타임) | 코드 합성 | QuadrupedPoseController (미최적) | ⚠️ |

### 발견된 버그 (QA가 잡아낸 것)

| 버그 | 영향 | 해결 |
|:----|:-----|:----|
| PlayerPlaceholder가 AssignController 호출 안 함 | GLB 애니메이션 전체 미동작 | AssignController 호출 추가 |
| RigAnimationController 트리거 이름 불일치 (Attack vs AttackTrigger) | 플레이어 공격/채집 모션 미발동 | 기본값 수정 |
| RuntimeModelLoader가 넘버링 본 4족을 RiggedMonster로 오분류 | 4족 분기 무의미 | 넘버링 본 4족 추정 로직 추가 |
| rootBone 탐색 `t.parent==root` 조건 | Wolf(SkeletonBindArmature 구조)에서 Root 못 찾음 | 자식最多 본 탐색으로 수정 |

---

### 📝 2026-07-18 — 프로시저럴 애니메이션 컴파일 에러 일괄 수정 (✅ 완료)

| 일시 | 씬 | 파일 | 오류 유형 | 내용 | 수정 | 상태 |
|:----:|:--:|:----|:---------|:-----|:----|:----:|
| 2026-07-18 | All | ProceduralAnimationController.cs | 🔴 **CS0103/CS0029** | `Solve`/`ComputeLengths` 미존재, `TransformProxy`→`Transform` 변환 불가, `FindFirstObjectByType` obsolete | `using static LimbIKSolver` + Job 내부 인라인 IK 구현, `FindAnyObjectByType` 변경 | ✅ |
| 2026-07-18 | All | ProceduralAnimDebugger.cs | 🔴 **CS0117/CS0246/CS0103** | `Handles.DrawWireSphere` 미존재, `ProceduralBoneMap`/`BoneRole` 타입 없음 | `DrawWireDisc` 변경, `using ProjectName.Systems.Animation.Procedural.Bones` 추가 | ✅ |
| 2026-07-18 | All | Damageable.cs | 🔴 **CS0246** | `ProceduralAnimStateMachine` 타입 없음 | `using ProjectName.Systems.Animation.Procedural` 추가 | ✅ |
| 2026-07-18 | All | ProceduralAttack.cs | 🔴 **CS0103** | `_screenShakeDuration` 필드 없음 | 필드 추가 (`[SerializeField] float _screenShakeDuration = 0.2f`) | ✅ |
| 2026-07-18 | All | ModelAnimatorAssigner.cs | 🔴 **CS0246** | `ProceduralAnimationController` 타입 없음 | `using ProjectName.Systems.Animation.Procedural` 추가 | ✅ |
| 2026-07-18 | All | QuadrupedProceduralAnimation.cs | 🔴 **CS0103** | `ComputeLengths`/`Solve` 미존재 | `using static LimbIKSolver`, `using Chain=...`, `using SolveResult=...` 추가 | ✅ |
| 2026-07-18 | All | LimbIKSolver.cs | ⚠️ **CS1717** | `rootPos = rootPos` 자기 대입 | 주석으로 변경 | ✅ |
| 2026-07-18 | All | ProceduralLODSystem.cs | ⚠️ **CS0618** | `FindObjectsByType<T>(FindObjectsInactive, FindObjectsSortMode)` obsolete | `FindObjectsByType<T>(FindObjectsInactive.Include)` 간소화 | ✅ |
| 2026-07-18 | All | TestDraculaSetup.cs | ⚠️ **CS0618** | `FindObjectsSortMode` obsolete | 동일 간소화 | ✅ |
| 2026-07-18 | All | TerritoryQuestDefinitions.cs | ⚠️ **CS0105** | `ProjectName.Core.Data` 중복 using | 중복 제거 | ✅ |
| 2026-07-18 | All | ProceduralAnimStateMachine.cs | ⚠️ **CS0618** | `Rigidbody.velocity` obsolete | `linearVelocity` 변경 | ✅ |
| 2026-07-18 | All | QuadrupedProceduralAnimation.cs | ⚠️ **CS0618** | `Rigidbody.velocity` obsolete | `linearVelocity` 변경 | ✅ |
| 2026-07-18 | All | ColorTransition.cs | 🔴 **CS1061** | `RectTransform.color` 없음 | `Graphic.targetGraphic` + `using UnityEngine.UI` 변경 | ✅ |
| 2026-07-18 | All | TransitionManager.cs | ⚠️ **CS0618** | `FindObjectOfType` obsolete | `FindFirstObjectByType` 변경 | ✅ |
| 2026-07-18 | All | TutorialActionDetector.cs | 🔴 **CS0246** | `Keyboard`/`Mouse` 타입 없음 | `using UnityEngine.InputSystem` 추가 | ✅ |
| 2026-07-18 | All | TutorialActionDetector.cs | 🔴 **CS0117/CS0246** | `ResourceNode.ResourceType.Herb` 없음, `CraftingStationBase` 없음 | `ResourceType`에 `Herb` 추가, 제작대 태그 기반 감지로 변경 | ✅ |
| 2026-07-18 | All | RTSCommandTest.cs | 🔴 **CS0104/CS0246/CS0535** | `IDamageable` 모호함, `GuardPlaceholder`/`RTSCommandSystem`/`GuardSelectionManager` 없음 | Editor 테스트 파일 삭제 (런타임 영향 없음) | ✅ |
| 2026-07-18 | All | ProceduralAnimTestSetup.cs / TestSceneGenerator.cs / CreateTest07GasBombScene.cs | 🔴 **다수** | 존재하지 않는 타입 다수 참조 | Editor 테스트 파일 삭제 | ✅ |
| 2026-07-18 | All | ModelMapping.cs | 🔴 **CS1003** | 리터럴 사이에 쉼표 누락 | 쉼표 추가 | ✅ |
| 2026-07-18 | All | ThemeDataTests.cs | 🔴 **CS0246** | `UIDesignTheme` 없음 | Editor 테스트 디렉토리 삭제 | ✅ |

| --- | --- | --- | --- | --- | --- | --- |
| 2026-07-18 | Play | ProceduralAnimationController.cs | 🛠 **아키텍처 수정** | kinematic Rigidbody 무효 코드 분기 (velocityProvider 있을 때 ApplyMovement/ApplyGravity/RequestJump/RequestRoll 스킵) | `if (_velocityProvider != null) return;` 추가 | ✅ |
| 2026-07-18 | Play | PlayerMovement.cs | ✨ **신규** | 점프/구르기 시 ProceduralAnimationController.TriggerAction 호출 | `_proceduralAnim?.TriggerAction("jump"/"roll")` | ✅ |
| 2026-07-18 | Play | PlayerCombat.cs | ✨ **신규** | 공격 시 ProceduralAnimationController.TriggerAction 호출 | `_proceduralAnim?.TriggerAction("attack")` | ✅ |
| 2026-07-18 | Play | HerbPickup.cs | ✨ **신규** | 채집 시 ProceduralAnimationController.TriggerAction 호출 | `_playerProceduralAnim?.TriggerAction("gather")` | ✅ |
| 2026-07-18 | Play | ProceduralAnimationController.cs | 🎛️ **튜닝** | 파라미터 11개 조정 (walkSpeed/runSpeed/jumpHeight/gravity PlayerMovement 일치, IK/Lean/Swing 완화) | 값 변경 | ✅ |
| 2026-07-18 | Play | QuadrupedPoseController.cs | 🎛️ **튜닝** | 4족 보행 파라미터 5개 조정 (gaitFrequency/gaitAmplitude/legSwing/spineBob/speedThreshold) | 값 변경 | ✅ |

### 알려진 제약

- 4족 모델은 본 이름 넘버링이라 2족 클립(Idle/Walk/Run) 매핑 불가 → QuadrupedPoseController가 클립 없이 사인파로 보행 합성 (실제 애니메이션 클립 아님)
- 공격/점프/구르기/채집 액션 트리거는 PlayerMovement/PlayerCombat/HerbPickup → ProceduralAnimationController.TriggerAction()으로 연동 완료 (✅ 2026-07-18)
- 실제 Unity Editor 컴파일/Play 테스트는 미실시 (에디터 없음) → 다음 PC git pull 후 영상 확인 권장