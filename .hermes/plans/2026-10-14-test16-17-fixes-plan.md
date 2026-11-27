# 64차 계획 — Test_16/17 실측 5건 수리 (2026-10-14)

## 사용자 보고 5건
1. Test_16 걷는 애니 여전히 끊김
2. 이번엔 **내 병사 쪽 체력이 줄지 않아**(수비병 반격 0건 — Editor.log 실측: 내 병사 피해열 0건, 수비병 피해 11건만)
3. 영주를 **침대가 아닌 곳**에 방에 배치 + **F키 상호작용**
4. 상호작용 프롬프트가 피그마 형태 아님
5. 대화하기 시 dialog 패널 피그마 형태 아님 + **대화 UI 바로 표시**

## 실측 결론 (감독자)

### A. 걷기 끊김의 뿌리 확정(63차 잔여)
- RC.ApplyAnimatorState(Idle)가 **SetFloat("Speed", 0) 직접 호출** — 63차 게이트는 RC.Update만 감쌌고 SetState 경로는 우회됨.
- 병사 도착/명령 해제 시 RC.SetState(Idle) → Speed=0 튀기 → 드라이버 감쇠값(3→0 진행 중)과 충돌 → Idle↔Move 전이 경계 진동 → 끊김.
- 수리: RC.ApplyAnimatorState의 Idle/Walk/Run case의 Speed 기록을 **HasActiveDriverOn(gameObject) 게이트**로 차단(63차 Update 게이트 확장).

### B. 수비병 반격 0건 — 계측 후 판별
- 61차 딜레이 로직(0.2s 스캔 × 3회 후 반격)은 코드상 정확. 그러나 실측 반격 0건.
- 계측: TryHoldEngage best 감지/딜레이 경과/공격 시도에 1회성 로그 + Play 재검증.

### C. 영주 위치 + F키
- 현재 영주실 앵커 (0,0,28) = Bedroom 존 중심 — 침대(LordBed @ (-16,0,12))는 별개. 유저 관측 "침대 위/옆" — 앵커를 방 안 다른 곳으로 이동(침대와 무충돌 + 벽측 배치 — (6,0,26) 등).
- F키: PlayerMovement F키 스캔(542~558)에 NPC/영주(TerritoryNPCBehaviour) 추가 — SoldierInteractBridge.RaiseTargetInteraction(NPC kind) 발화 → InteractionPanelUTK Open.
- 프롬프트: EInteractionPromptUTK.Register(transform, "F키를 눌러 대화", range) — NPC 스폰 시 등록(피그마 82:45 형식 — 키캡 F로 교체 라벨).

### D. UI 피그마 정합
- NPCDialoguePanelUTK: 피그마 163:23/187:14(528×148.8/171.8 — 논리 440×124/143) — **DecalTL/TR/BL/BR 부재** + 선택 카드 스타일(피그마 ChoiceCard — 카드 배경/라벨) 부분 차이 → 피그마 정합 보강.
- 대화 UI 바로 표시: 대화하기 클릭 시 InteractionPanel 닫힘 + NPCDialoguePanelUTK 즉시 Open(현재도 그렇게 보이나 — 겹침/지연 체감 검증).
- EInteractionPromptUTK: 이미 피그마 82:45 정합(베이크 PNG) — NPC에는 Register 확장만.

## 실행 Phase (순서)
- P1: RC ApplyAnimatorState 게이트(걷기 끊김) + TryHoldEngage 계측 로그
- P2: 영주 앵커 이동(침대 회피) + Test_16/17 영주 배치 좌표 반영
- P3: F키 NPC 상호작용(PlayerMovement 확장 + 브리지) + NPC 프롬프트 Register
- P4: NPCDialoguePanelUTK 피그마 정합(데칼/카드/높이)
- P5: 컴파일 + EditMode 전체 회귀 + Play 게이트 목록

## 타임아웃 폴백 규칙
- delegate_task 600s 타임아웃 나면 **부모가 즉시 직접 코딩 진행**(사용자 명시 승인) — Phase별 단일 위임/부분 위임 우선, 2회 이상 타임아웃 세션은 남은 Phase 전부 부모 직접.
