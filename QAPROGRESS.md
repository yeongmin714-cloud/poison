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
| **Test_06_TimeWeather** | 🌙 시간+날씨 | ⬜ | — | — |
| **Test_07_GasBomb** | 💨 가스분사기+폭탄 | ⬜ | — | — |
| **Test_08_Dracula** | 🧛 드라큘라+야간 | ⬜ | — | — |
| **Test_09_AllInOne** | 🛡️ 모든 시스템 | ⬜ | — | — |

---

## 📝 발견된 오류 로그

| 일시 | 씬 | 파일 | 오류 유형 | 내용 | 수정 | 상태 |
|:----:|:--:|:----|:---------|:-----|:----|:----:|
| 2026-07-13 | Test_01_Player | TestSceneGenerator.cs | 🔴 **씬 구조 오류** | Player/Camera가 없는 빈 씬 생성 | 메인씬 복제 → 불필요 제거 방식으로 변경 | ✅ |
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