# 151차 계획 — 음식점 금액 입력 수리 + Test_15 즉시 구매 + 대화판넬 중앙하단 고정 + 농경 문구 패널화 + 호수 복귀

작성: 2026-11-03 (151차). 승인 전까지 구현 없음.

## 0. 사용자 지시 (Telegram 2026-11-03)

1. 음식점 배치에서 **금액 직접 입력 불가** — 수리
2. **NPC가 바로 음식을 사러 오게** — Test_15에서만
3. **대화판넬은 항상 중앙 하단** 배치로 고정
4. **농경 상호작용 문구**(씨앗이 필요합니다 / 물뿌리개가 필요합니다 등)를 대화판넬로 출력
5. **호수를 근처에 1개** 배치 (Test_15)

## 1. 사전 실측 (계획 단계에서 확정한 근거)

- **Editor.log (/mnt/c/Users/korea/AppData/Local/Unity/Editor/Editor.log, 11-03 13:33) P4 판독**:
  - `[150차][P4] 가격 필드 포커스 획득` **2회 확인** / `[150차][P4] 가격 필드 키다운` **0회**
  - → 150차 P4의 두 갈래 가설 중 **"키보드 전달 차단"으로 확정** (포커스 미획득/클릭 가로채기 기각)
- **동시 실측**: `[150차 P2] 패널 투영 무효 — 폴백(중앙 하단) cam=(0.0,0.0,0.0)` — 패널 위치 문제는 애초 앵커 투영이 불안정 → 사용자 지시 3번("항상 중앙 하단")과 방향 일치.
- **RestaurantVisitorSystem.cs (100차) + TestTerritoryCombatSetup.cs (149차 P5)**: Test_15 고객 NPC(Test15Customer)+방문 시스템 이미 존재. 단, (a) 영업시간 8≤h<22 게이트 (b) 재고>0 필수 (c) 2.5s 스캔 (d) 음식점 건설 마을만 → "바로" 체감의 방해 요인.
- **TestTerritoryCombatSetup.cs 148차**: 호수/나무/돌을 Test_20(NatureCluster)으로 분리 — **Test_15에는 현재 호수 없음** (물뿌리개 리필 전제인 "호수에서 충전하세요" 문구와 불일치 상태).
- **FarmPlot.cs**: ShowStatus 메시지 15건 — 현재 OnGUI IMGUI 라벨로만 표출 (대화판넬 미연동).

## 2. Phase 분해

### P0 — 음식점 등록가(금액) 입력 수리 [키보드 전달 차단]
- **목표**: 마약/일반 등록가 TextField에 숫자 입력 → CommitRegisteredPrice 정상화.
- **진단(구현 전 1회)**: 루트/조상 요소의 KeyDownEvent 핸들러 전수에서 `StopPropagation/StopImmediatePropagation` 스캔 + 전역 키보드 인터셉터(Keyboard.current 직접 소비/EventSystem 차단) 후보 목록화.
- **수리 후보 (진단 결과에 따라 선택)**:
  1. 조상 KeyDown 소비자 제거/가드 (TextField 포커스 시 얼리리턴 — 401행 IsAnyTextFieldFocused 가드 패턴 전수 적용)
  2. priceField 클릭 시 `Focus()` 명시 + `pickingMode` 정리
  3. 최후 폴백: 포커스 상태에서 `Keyboard.current.onTextInput` 우회 수집 → field.value 직접 반영 (단, 최후 수단 — 선호 순위 최하)
- **파일**: `Assets/Scripts/UI/Toolkit/RestaurantWindowUTK.cs`
- **검증**: 계약테스트 신설(등록가 필드 포커스 시 조상 소비자 부재 소스계약 또는 동작계약) + RestaurantWindowTests/FigmaTests 집중 GREEN.

### P1 — Test_15 NPC 즉시 구매 [씬 게이트 한정]
- **목표**: 음식점 건설+입고 직후 고객이 **즉시(첫 스캔 틱)** 방문·구매 연출.
- **구현**:
  1. Test_15(Livelihood) 한정: 영업시간 게이트 완화는 **불변(계약 유지)** 대신 — 씬 진입 시각이 이미 8시([134차 B-2])이므로 **스캔 트리거 즉시화**: 음식점 건설 완료 콜백/재고 입고 시점에 `ScanAndAssign()` 1회 즉시 호출 (Livelihood 씬 게이트).
  2. Test_15 한정 스캔 간격 2.5s→1s 단축 (씬 이름 게이트 — 프로덕션 무영향).
  3. 재고 0이면 방문 안 하는 현재 계약 유지 + **대화판넬 안내 연동**(P3 경유): "음식점에 음식을 입고하세요" 1회 안내.
- **파일**: `Assets/Scripts/Systems/RestaurantVisitorSystem.cs` (+건설 완료 훅 위치: RestaurantSystem/ConstructionManager 완료 지점)
- **검증**: RestaurantVisitorTests 집중 GREEN + Livelihood 즉시배정 소스계약 테스트.

### P2 — 대화판넬 항상 중앙 하단 고정
- **목표**: NPCDialoguePanelUTK 모든 개설 경로(Open/ForGuard/ForVillager/ForStatic/ForAcquisition)에서 패널을 **항상 화면 중앙 하단(50%, 85%)**에 배치. 앵커 투영/클램프/프록시/재배치 루프 경로 전부 폴백 배치로 일원화.
- **계약 변경 명시**: 74차 "대상 앞 배치" 계약 폐기, 150차 P2 폴백 계약을 "상시"로 승격. 투영 무효 재배치 루프 제거(항상 고정이라 무의미).
- **파일**: `Assets/Scripts/UI/Toolkit/NPCDialoguePanelUTK.cs`
- **검증**: 패널 위치 소스계약 테스트 갱신(PlaceAtAnchor 호출부 제거/폴백 상시) — 기존 대화 계약테스트 중 위치 단언분 전수 갱신.

### P3 — 농경 상호작용 문구 → 대화판넬
- **목표**: FarmPlot.ShowStatus 15건 전부 대화판넬(중앙 하단 — P2 완료 전제) 표출.
- **구현**:
  1. `NPCDialoguePanelUTK.ShowTransientMessage(string)` 신설 — 3.5s 표출 후 자동 닫힘(ShowStatus의 _statusUntil 창 계약 승계). 연속 메시지 스팸 방지(동일 메시지 재표출 시 타이머 연장만).
  2. FarmPlot.ShowStatus → 패널 브로드캐스트 추가 (OnGUI 근접 프롬프트는 유지 — 조작 힌트와 상태 안내 역할 분리).
  3. 문구 전수 목록: 아군 영지 / 굉이질 완료 / 씨앗 부족 / 파종 / 물주기 / 물뿌리개 필요 / 물뿌리개 물 없음 / 말라죽음 / 부패 / 수확 준비.
- **파일**: `Assets/Scripts/UI/Toolkit/NPCDialoguePanelUTK.cs`, `Assets/Scripts/Systems/FarmPlot.cs`
- **검증**: FarmPlot 상태 메시지 → 패널 라우팅 소스계약 + 신규 ShowTransientMessage 동작계약.

### P4 — Test_15 호수 1개 복귀 배치
- **목표**: 148차에 Test_20으로 분리된 호수를 **Test_15로 1개 복귀**(밭/마을 근처 — 물뿌리개 리필·낚시 전제 복원).
- **구현**: SetupLivelihoodCompactCluster에 142차 호수 블록 복원(anchor+dOut×16, 원판 r12 — 결정론 수식 재사용, Random 0건 규약). 밭 2x2(좌14m)와 무충돌 위치 재확인. 물뿌리개 리필(WateringCanSystem 리필 지점)이 호수 인식하는지 연동 확인.
- **파일**: `Assets/Scripts/Systems/TestTerritoryCombatSetup.cs`
- **검증**: Test15VillageDrugTests 지면패치/배치 계약 갱신 + 호수 배치 로그.

## 3. 순서·의존성

P0(독립) / P2 → P3(패널 위치 고정이 문구 표출 전제) / P1·P4(독립). 병행 위임 시: P0+P1+P4 동시, P2 완료 후 P3.

## 4. 검증 공통

- 컴파일 error CS 0 → EditMode 전수 baseline 1:1 (기존 10건+Melee 플래키 2건 부분집합 허용) → 신규/집중 계약 GREEN.
- Play(사용자 승인): ①음식점 등록가 숫자 입력→Commit 로그 ②입고 직후 고객 즉시 방문+코인 연출 ③대화판넬이 항상 중앙 하단 ④농경 문구가 패널에 표출 ⑤호수에서 물뿌리개 리필.

## 5. 미확정(사용자 확인 대기)

- 호수 위치 해석: "근처" = **마을 가장자리 클러스터(밭 근처, 142차 수식 위치)**로 해석 — 다른 위치 원하면 지정 요망.
- P1의 "바로": 음식점 건설+입고 직후 즉시 방문으로 해석. 재고 0 상태에서도 방문하게 할지(→"재고 없음" 문구 연출)는 기본 안 함(계약 유지).
