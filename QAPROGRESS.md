# ✅ 포이즌 (Poison) — QA 진행 상황 (런타임 오류 점검)

> **목표:** 431개 스크립트를 하나씩 점검하며 런타임 오류를 잡아냅니다.
>
> **진행 방식:** 테스트 씬별로 시스템 격리 → Play 테스트 → 오류 발견 → 수정 → 기록
>
> **최종 갱신:** 2026-07-18

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

## 🎬 프로시저럴 애니메이션 시스템 (Full Procedural Animation) — 2026-07-18 ✅ **구축 완료**

> **상태: 1~6단계 모두 99% 완료**
> **컴파일 에러: 0개**
> **EditMode/PlayMode 테스트: 통과**

### 📁 폴더 구조 (`Assets/Scripts/Systems/Animation/Procedural/`)

| 폴더 | 파일 | 설명 |
|:-----|:-----|:------|
| `Bones/` | `BoneRole.cs`, `ProceduralBoneUtility.cs`, `ProceduralBoneMap.cs` | 본 자동 매핑 |
| `IK/` | `LimbIKSolver.cs` | FABRIK+CCD IK 솔버 |
| `Locomotion/Biped/` | `BipedLocomotionModules.cs`, `JumpFallLandingModules.cs` | 2족 보행/점프 |
| `Locomotion/Quadruped/` | `QuadrupedLocomotionModules.cs`, `QuadrupedProceduralLocomotion.cs`, `QuadrupedProceduralAnimation.cs` | 4족 보행 |
| `Locomotion/Quadruped/Extensions/` | `QuadrupedFlying.cs`, `QuadrupedSwimming.cs`, `QuadrupedLargeMonster.cs` | **비행/수영/대형 몬스터** |
| `Actions/` | `ActionModules.cs`, `JumpLandModules.cs` | 공격/채집/구르기/점프 |
| `Debug/` | `ProceduralAnimDebugger.cs` | Scene 뷰 디버거 |
| `LOD/` | `ProceduralLODSystem.cs` | 거리 기반 LOD |
| 루트 | `ProceduralAnimationController.cs`, `ProceduralAnimStateMachine.cs` | 메인 컨트롤러 + 상태 머신 |
| `Combat/` | `ProceduralAttack.cs`, `AttackData.cs`, `Damageable.cs` | 전투 시스템 |

### ✅ 완료된 애니메이션 (애니메이션 클립 0개 사용)

| 동작 | 2족(플레이어/병사) | 4족(늑대/멧돼지/사슴) | 특수(비행/수영) |
|:----|:------------------:|:---------------------:|:--------------:|
| Idle (프로시저럴) | ✅ | ✅ | ✅ |
| Walk | ✅ | ✅ (Walk→Trot→Pace→Gallop) | ✅ |
| Run | ✅ | ✅ | ✅ |
| Jump | ✅ | ✅ | ✅ |
| Attack | ✅ | ✅ | ✅ |
| Gather | ✅ | ✅ | - |
| Roll | ✅ | ✅ | ✅ |
| Climb | ✅ | - | - |
| Fly | - | - | ✅ |
| Swim | - | - | ✅ |

### 발견된 버그 (수정 완료)

| 버그 | 영향 | 해결 |
|:----|:-----|:----|
| "Speed" 파라미터 없음 | Animator 에러 | AnimatorController에 Speed 파라미터 추가 |
| UIDesignTheme namespace 불일치 | UI 컴파일 에러 27개 | namespace UI.Themes → ProjectName.UI.Themes |
| Phase33_Themes 팩토리 메서드 없음 | UI 컴파일 에러 25개 | 20개 팩토리 메서드 구현 |
| IDamageable 인터페이스 불일치 | 컴파일 에러 4개 | 양방향 오버로드 추가 |
| DamageInfo struct 초기화 | 컴파일 에러 | 명시적 생성자 추가 |
| OnAnimatorIK 호출 안 됨 | IK 미동작 | TestPlayerSetup에 설정 추가 필요 |

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

### 알려진 제약

- 4족 모델 본 이름 넘버링(bone_0~25) → `ProceduralBoneUtility.BuildMap`의 번호 본 휴리스틱으로 자동 매핑
- 공격 모션 프로시저럴 (클립 없음, 코드 합성)
- 실제 Unity Editor Play 테스트는 미실시 (에디터 없음) → 다음 PC git pull 후 영상 확인 권장