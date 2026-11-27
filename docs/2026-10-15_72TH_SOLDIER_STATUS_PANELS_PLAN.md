# 72차 계획 — 병사 상태보기 피그마 패널 2종 + 상호작용 전면 활성화 + 이동 링 1s 소멸 + 걷기 클립 교체

> 작성: 2026-10-15. 사용자 요구 5건:
> ①병사 상태보기 = 피그마 equipment-inventory-panel(84:4) 형태 ②적병사 상태보기 = character-status-panel(83:4)
> ③가운데 Portrait에 **현재 캐릭터 3D 실물** 렌더 ④상호작용 패널이 대상 바로 앞에 뜨고 상태보기 외
> 대화하기/뇌물주기/포섭하기/물약주기/음식주기도 활성화 ⑤이동 지칭 푸른 링 표시 후 1초 소멸(미반영)
> ⑥걸음 여전히 어색.

## 진단 (실측)

1. **상태보기 현재 = GuardInfoUTK(780폭 구창)** — 피그마 2종 패널과 무관.
2. **패널 액션 대부분 미지원** — InteractionPanelUTK.OnActionRequested: 상태보기/대화(NPC·영주)/상점/처형·풀어주기만
   구현, 병사 대화하기/뇌물주기/포섭하기/물약주기/음식주기 = "미구현 액션" 로그만. 그러나 기존 시스템이
   **전부 존재** — GuardPlaceholder.BeginTalk/BeginRecruit/AttemptBribe/BeginFoodSelection/BeginDrugSelection/
   GetInventoryItemsByMode(GiveFood/GiveDrug) — SoldierInteractUTK(구창)이 쓰던 경로.
   → 패널에서 구창 래퍼(메서드)를 재사용하면 전부 활성화 가능.
3. **패널 위치 = 클릭(마우스) 위치** 기준 배치(167-173행) — "대상 바로 앞" 아님.
4. **이동 지칭 푸른 링 = CommandMarker 지속형**(#58A6FF — 도착까지 잔존 설계). 유저가 말한 대상.
   선택 링(GuardSelectionManager)은 68차에 이미 1s 소멸 구현 — 본 대상은 이동 명령 링.
5. **걷기 어색 뿌리 추가 발견(클립 방향 불일치)** — 컨트롤러 Move state 배선 클립 실측:
   - Archer = **OneHand_Up_Walk_B(뒤로 걷기)** ba58…, Shield/GreatSword = **OneHand_Up_Run_B(뒤로 러닝)** 282509…
   - 전진 이동인데 뒤로 걷기/러닝 클립 = 자세 방향 불일치(어색함의 확정 뿌리 하나).
   - 클립 전부 RootT.z 진행 커브 없음(원위치형 — 코드 이동과 이중 없음, 이 부분은 정상).
   - 교체 대상: **OneHand_Up_Run_F_InPlace**(8b7e…, 0.733s 사이클) — 3.0m/s 이동 × 0.733s = 2.2m/사이클
     (러닝 보폭 정합), InPlace(원위치), 전진(F). 3종 컨트롤러 Move state m_Motion guid 교체.
   - MoveRate(=Speed/3.0) 파라미터는 유지(보폭-속도 정합 배속).

## Figma 진실치 (2026-10-15 실측 — 84:4 / 83:4)

### 공통 스펙
- 창 576×936(83:4) — glass #161B22/70% radius 14.4 + 코너 데칼 4(AddCornerDecal 기존 패턴) + PanelHeader
  ("상태 정보" 24/700 #F0F6FC + "CHARACTER STATUS" 13.2/500 #8B949E + CloseBtn 31.2 #FFFFFF/6% radius 7.2)
- Identity: #FFFFFF/2% radius 9.6 — RarityRow("전설적인 에이전트 (LEGENDARY)" 15.6/700 #BC8CFF + "클래스: 뱅가드"
  15.6/400 #8B949E 우측) + CharacterName("코드네임: {이름}" 26.4/700 #F0F6FC)
- **Portrait: (24,199.2) 528×312 — bg #0B0E14 radius 9.6 + HudOverlay("SYS_SCAN_ACTIVE" 좌상/ "HP 100%" 우상 12/400 #58A6FF)** — 3D 실물 렌더 영역
- StatsSection (24,530.4) 528×174.4 #FFFFFF/2% radius 9.6:
  - LevelBlock (38.4,544.8) 120×145.6 #FFFFFF/2% radius 7.2 — "LEVEL" 13.2/500 #8B949E / 값 38.4/800 #58A6FF /
    "EXP 94.2%" 12/400 #56E39F
  - CoreStatsGrid (177.6,544.8) 360×145.6 — StatRow(라벨 15.6/500 #8B949E / 값 16.8/700 우측 정렬: 공격 #58A6FF,
    방어 #F0F6FC, 최대체력 #FF7B72) + 구분 Line(#30363D 계열 1px)
- SpecialStatsSection (24,724) 528×157 #FFFFFF/2% radius 9.6 — "특수 상태 게이지" 14.4/700 + "SPECIAL VITALS"
  12/400 #484F58 + 게이지 2(라벨 15.6/700 #F0F6FC / 우측 값 16.8/700 + BarBg #FFFFFF/6% radius 3.6 + fill
  #BC8CFF(호감도)/#F2C94C(중독도)) — 호감도(주기상태 문자열 13.2/400 #8B949E)/중독도
- 84:4 = 위 CharacterStatusPanel + **EquipmentPanel 531.6×936(우측)**:
  - SectionHeader "장착 장비" 24/700 + "LOADOUT MATRIX" 13.2/500 + "SYS_EQ_STABLE" 12/400 #56E39F
  - EquippedSection (24,96) 483.6×230.4 #FFFFFF/2% radius 9.6 — "장착 중인 장비" 19.2/700 #58A6FF +
    EquipGrid (52.2,145.2) 427.2×172.8(10슬롯 81.6 — **인벤과 동일 카드 구조**, 단 **슬롯 라벨 있음**:
    투구/갑옷/신발/방패/무기 1행 + 방독면/분사기/물약/반지/목걸이 2행, 9.6/500 #8B949E 하단)
  - TacticalPackSection (24,345.6) 483.6×567 — "전술 배낭" 19.2/700 #F0F6FC + "TACTICAL PACK" 12/400 +
    InventoryGrid 5열×5행(81.6, #21262D/60% radius 9.6) + Count(13.2/500 #8B949E 우하단)

### 구현 규약
- 패널 2종은 **신규 창** GuardStatusWindowUTK 1개로 구현(모드 2개: Ally=장비패널 결합/Enemy=단독) —
  UTKWindowBase + glass 크롬(AddCornerDecal) 재사용. 상태보기 클릭 시 GuardInfoUTK 대신 이 창 개방
  (기존 GuardInfoUTK는 유지 — 다른 호출부(Raise) 존재 없으면 은퇴, 기록만).
- **3D 인물 렌더**: 대상 병사 GLB를 대상 Transform 기준 사전 베이크 대신 — **런타임 카메라 RT 캡처**:
  대상 병사를 임시 각도(정면 약간 위)에서 찍은 RT(RenderTexture 512×288)를 Portrait 영역 backgroundImage로.
  GblItemIconRenderer의 RT 베이크 마운트 선례(453-546행) 재사용 — RenderTexture 유지(전투 중 갱신은 폴링 1s).
  대상 없음/캡처 실패 = bg만(위조 데이터 금지).
- 액션 라우팅 — InteractionPanelUTK에서 **새 경유지**: 병사 액션은 SoldierInteractUTK 동일 래퍼 사용:
  - 대화하기 → guard.BeginTalk()(호감도 응답/상태 메시지 — 기존 로직)
  - 뇌물주기 → guard.AttemptBribe()(아군이면 무시)
  - 포섭하기 → guard.BeginRecruit()
  - 물약주기/음식주기 → **인벤 아이템 선택 필요** — 기존 SoldierInteractUTK의 itemSection 흐름은 구창 전용
    (UTK 창 내부 UI). 패널에서는: 패널 위(또는 대체 UI)로 병사 상호작용창(SoldierInteractUTK)을 해당 모드로 개방
    하는 1단 위임이 안전 — `SoldierInteractBridge`에 `RaiseInteract(guard, itemMode)` 확장 or SoldierInteractUTK
    정적 Open(guard, mode) 경유. **IsActionSupported 전부 true로** (라우팅만 확장).
  - 상태보기 → 새 GuardStatusWindowUTK 개방(모드 별 분기 — Ally=combined/Enemy=status-only).
- 패널 배치(대상 바로 앞): OpenForTarget에서 대상 Transform의 **WorldToScreenPoint → canvas 좌표 → 패널 하단 중앙이
  대상 위 0.5m** 되게 배치 + 화면 클램프(기존 167-173 패턴). 대상 위치 = target transform(GuardPlaceholder/
  TerritoryNPCBehaviour/InvadedLordEncounter 공통 — Component.gameObject.transform). 개방 중 이동 추적은
  1초 폴링(패널 Update schedule) — 가볍게.
- 이동 명령 링(CommandMarker): 지속형이라도 **표시 후 1s → 0.3s 축소 페이드 소멸**(SelectionRingController 규약 동일).
  persistent 소멸 조건(도착)은 폐기 — 유저 규약(1s 소멸) 우선. _persistent 라도 life=1s/fade=0.3로.
- 걷기: 3종 컨트롤러 Move state m_Motion guid → Run_F_InPlace(8b7e…). 대상 파일:
  Assets/Resources/Animation/Controllers/SoldierShield_AC / SoldierArcher_AC / SoldierGreatSword_AC(.controller).

## Phase 분할 (위임)

- **Phase A(구현 1)**: GuardStatusWindowUTK 신설 + 상태보기 라우팅 교체 + RT 3D 렌더(84:4/83:4 진실치 위 표 전체).
- **Phase B(구현 2)**: InteractionPanelUTK — 병사 액션 5종 활성화(래퍼 경유) + IsActionSupported 전면 true +
  대상 앞 배치(WorldToScreenPoint+폴링 추적) + CommandMarker 1s 소멸 + 3종 컨트롤러 Move 클립 교체.
- 테스트: 신규 GuardStatusPanelTests(소스 계약 — 패널 진실치 마커/RT 캡처 마커/액션 라우팅 마커/배치 마커/
  컨트롤러 guid 교체 검증= 3종 YAML에 8b7e 존재+구 guid 부재)+ CommandMarker 1s 소멸 계약 마커.

## 검증/게이트

- 컴파일 error CS 0 ×2회 / EditMode 전수(873/862/11 유지 — 신규 회귀 0).
- Play 게이트(사용자): ①아군/적병사 상태보기 = 피그마 2종 패널(Portrait에 병사 3D 실물) ②상태보기 패널도
  대상 바로 앞 배치 ③대화/뇌물/포섭/물약/음식 클릭 → 각 기존 시스템 반응(호감도/중독도/포섭 대화상자) ④이동
  지정 파란 링 1s 후 소멸 ⑤걷기 자세(전진 러닝 정합) 체감 ⑥기존 창/핫바 무사고.
- stage/commit/push는 사용자 승인 후.
