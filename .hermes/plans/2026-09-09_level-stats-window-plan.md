# 레벨 시스템 + 스탯 창 구현 계획 (2026-09-09)

> 실행 규약: 코드/QA delegate_task 위임 필수. 매 단계 배치컴파일 error CS=0 선행. 완료 시 3중 저장(메모리+QAPROGRESS+git push)+텔레그램.

## 1. 목표

레벨 시스템과 스탯 창을 실제 플레이에서 동작하도록 완성한다.
- 아이템: 키 입력 → 스탯 창 토글 (Lv/EXP/HP/전투/제작보너스/중독 실시간 표시)
- 레벨업 피드백: EXP 획득→레벨업→파생 스탯 상승이 화면에서 체감되도록

## 2. 현황 진단 (2026-09-09 코드 조사 완료)

### 이미 완성 (재구현 금지)
| 자산 | 상태 |
|:--|:--|
| `Assets/Scripts/Core/PlayerStats.cs` (302줄) | **완성**. Lv1~50 EXP 테이블, AddEXP(다중 레벨업 처리), OnLevelChanged 이벤트, HPBase=100+(Lv-1)*5, Alchemy/Cooking +2%/Lv, Speech +1/Lv, Combat +1%/Lv, Final* 파생스탯(공격+0.5/Lv, 방어+0.2, 치명+0.5%, 이속+0.1), 복수명부 보상. **MainScene 배치 확인됨** |
| EXP 획득원 7곳 | **연결됨**: 제작(CraftingHelper/AlchemyUI), 퀘스트(QuestManager/NpcQuestGiver/QuestChainManager), 사냥(AnimalAI 티어별), 채집(HerbPickup +3) |
| `Assets/Scripts/UI/KeyBindings.cs` | "Status" 액션 등록됨 (기본키 C) |

### 갭 (이번에 해결할 것)
1. **스탯 창이 안 뜸**: `Assets/Scripts/UI/Windows/PlayerStatusWindow.cs`(70줄)는 UIWindow 상속+테마+중독 슬라이더까지 갖췄으나 ①씬 배치 0(guid 참조 없음) ②코드 어디서도 인스턴스화 안 함 → 직렬화 Text 참조에 의존하는 죽은 코드
3. **키 라우팅 부재**: UIManager(`UI/Core/UIManager.cs`)에 "Status" 키 소비 처리 없음. KeyBindings는 등록만.
4. **C키 충돌**: KeyBindings._statusKey=C vs `PlayerMovement.cs:412` 은신(cKey.isPressed). 과거 크래프팅이 동일 충돌로 X로 이동한 전례 있음 → 스탯 창 키를 미사용 키로 변경 필요.
5. **죽은 스텁 2개**: `UI/Functions/PlayerStatsUI.cs`(21줄 TODO 스텁), `UI/Functions/UIPlayerStats.cs`(69줄 하드코딩 가짜 레벨업 — PlayerStats 미연결, 혼동/중복 유발) → 삭제 대상.
6. **UI 표준**: 최근 유지보수되는 방식은 HotbarUI의 **런타임 프로그래매틱 생성**(new GameObject + 자체 Canvas, pivot 0.5/0 등). 씬 직렬화 참조 방식은 신뢰 낮음 → 신규 창도 이 패턴 따를 것.

## 3. 접근 방식

새 `StatusWindowUI`를 HotbarUI 패턴(런타임 자체 생성, 씬 의존 0)으로 작성. PlayerStats/PlayerHealth의 공개 API만 소비. 기존 PlayerStatusWindow는 삭제(기능이 신규 창으로 흡수됨). 키는 C→미사용 키(조사 후 확정, 후보 K)로 교체해 은신과 충돌 제거.

## 4. Phase 계획

### P1 — 사전 조사 확정 (QA 에이전트, 읽기전용, ~30분)
- [ ] 현재 Q/R/I/M 창 토글 실제 처리부 추적 (UIManager.Core / 부트스트랩 / 각 윈도우 셀프처리 여부) → 스탯 창 토글을 거기에 맞춰 연결
- [ ] 미사용 키 확정: 사용 중 목록(Q,R,I,M,X,C,U,E,1~8,Ctrl,Space,Shift) 제외 → 후보 K/J/H 중 배정
- [ ] `Phase33_Themes.CreateStatusTheme()` 존재 확인 — 없으면 신규 창은 단순 다크 패널 테마로 자체 구현
- [ ] PlayerHealth 공개 API(CurrentHP 등) 확인

### P2 — 스탯 창 신규 구현 (코드 에이전트)
- [ ] `Assets/Scripts/UI/StatusWindowUI.cs` 신규:
  - 런타임 자체 Canvas 생성(핫바와 분리된 전용 캔버스, sort order 상위), UIWindow 상속 유지(Show/Hide/Toggle 재사용)
  - 좌측 상단 고정 패널(핫바 하단중앙과 안 겹침), 반투명 다크 배경
  - 표시 항목: Lv / EXP 게이지(현재·다음 레벨까지) / HP(현재/HPBase) / 공격력·방어력·치명타·이속(Final*) / 연금술·요리 보너스% / 화술 / 전투보너스% / 중독도(DrugEffectSystem) / Gold
  - UpdateDisplay(): PlayerStats.Instance/PlayerHealth null 가드 필수
  - PlayerStats.OnLevelChanged 구독 → 창 열려있으면 즉시 갱신, 닫혀있어도 구독 유지(OnDestroy 해제)
  - 텍스트 프리팹 방식: 기존 UI가 쓰는 Text/TMP 통일 확인 후 동일 컴포넌트 사용(혼합 금지)
- [ ] 키 연결: KeyBindings._statusKey 기본값 C→(P1 확정 키) 변경, 키 라우터에 Toggle 연결 (P1에서 찾은 처리부에 추가)
- [ ] 레벨업 피드백: 화면 중앙 상단 "LEVEL UP! Lv.N" 팝업 2초 페이드(StatusWindowUI 내 정적 메서드, PlayerStats.OnLevelChanged에서 발화) — victory 로코 연출은 보류(별도 Phase)
- [ ] 컴파일: 배치 error CS=0

### P3 — 사감 정리 (코드 에이전트, P2와 병행 가능)
- [ ] 삭제: `UI/Functions/PlayerStatsUI.cs`, `UI/Functions/UIPlayerStats.cs` (+.meta)
- [ ] 삭제: `UI/Windows/PlayerStatusWindow.cs` (+.meta) — 기능 신규 창으로 이관 완료 후
- [ ] guid 잔여 참조 0 확인 (씬/prefab)

### P4 — QA (QA 에이전트)
- [ ] 배치컴파일 error CS=0 (전체 재확인)
- [ ] 정적 검증: 삭제한 guid 참조 없음, 키 충돌 목록 갱신(은신 C 정상), OnLevelChanged 구독 해제 누수 없음
- [ ] Play 판정 항목 산출(사용자 스크린샷 순번 방식 병행):
  1. (키) 누르면 스탯 창 토글
  2. 잡초 채집 +3 EXP → 게이지 증가 확인
  3. 몬스터 처치 → 티어별 EXP 반영
  4. 레벨업 시 HP상한/보너스% 갱신 + LEVEL UP 팝업
  5. 은신(C) 정상 동작, 스탯 창 키와 무충돌

### P5 — 기록/마무리
- [ ] QAPROGRESS.md 스냅샷 추가, ROADMAP.md 해당 항목 체크
- [ ] 메모리 저장 (UI 런타임 생성 패턴/키 배정 결과)
- [ ] git commit + push, 텔레그램 알림

## 5. 수정 파일 목록 (예상)

| 구분 | 파일 |
|:--|:--|
| 신규 | `Assets/Scripts/UI/StatusWindowUI.cs` |
| 수정 | `Assets/Scripts/UI/KeyBindings.cs` (기본키 C→K 등), 키 라우터 1곳(P1 확정) |
| 삭제 | `UI/Functions/PlayerStatsUI.cs`, `UI/Functions/UIPlayerStats.cs`, `UI/Windows/PlayerStatusWindow.cs` (+각 .meta) |
| 문서 | `ROADMAP.md`, `QAPROGRESS.md` |

## 6. 리스크/주의

- **C키 이중사용**이 최대 함정: PlayerMovement는 cKey 직접 읽음(KeyBindings 경유 안 함) → 스탯 창을 다른 키로 빼는 게 정답. 반대로 은신을 KeyBindings 경유로 고치는 건 범위 확대라 하지 않음.
- **Text vs TMP 혼합 금지**: P1에서 기존 UI 컴포넌트 관행 확인 후 통일.
- **Phase33_Themes 부재 가능성**: 참조 실패 시 신규 창은 자체 간단 테마(기존 UIWindow 테마 API만 사용).
- PlayerStatusWindow의 중독 슬라이더/DrugEffectSystem 참조는 신규 창에 그대로 이관(누락 금지).
- Play 판정은 사용자 스크린샷 순번 보고 방식 + Editor.log grep으로 병행.

## 7. 배치/검증

- 배치컴파일: 기존 verify 스크립트 재사용(예: verify_compile_*.bat 패턴) — "Exiting batchmode successfully" + error CS=0 게이트
- Play 검증: P4 항목 5개
