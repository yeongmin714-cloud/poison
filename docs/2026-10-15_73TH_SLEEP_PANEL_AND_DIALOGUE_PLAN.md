# 73차 계획 — 수면 시스템 패널(피그마 227:262) 정합 + 상호작용 피드백(말풍선/토스트) + 대화 카메라 확대

> 작성: 2026-10-15. 사용자 요구 3건:
> ①세이브 패널(피그마 "수면 시스템 패널" 227:262) 형태로 정합 + **게임 로직도 피그마에 맞춰 변경**
> ②여전히 대화하기/나머지 상호작용 불가 → 가능하게 ③대화하기 클릭 → 대화 패널 표시 + **대상 카메라 확대**
> 로 직접 대화하는 느낌.

## 진단 (실측 — Editor.log 유저 Play 세션 11:57~12:00 재구성)

패널 클릭 → 로그는 찍힘(뇌물주기/포섭하기/대화하기 — 33013/33233/33426행) — **라우팅은 작동**.
불가능의 실체 3가지:
1. **결과 피드백 부재** — 뇌물/포섭/대화는 `GuardPlaceholder._statusMessage` 세팅만. 표시 경로가 없어
   유저는 아무 변화를 못 본다(말풍선/패널 라벨 부재).
2. **조용한 실패** — 뇌물 `TryBribe`는 골드 부족 시 조용히 false(16씬 초기 골드 0 — 세이브 gold=0/무세이브
   세션 AddGold 없음). 포섭 `OnRecruit`도 입단비 부족 → statusMessage만.
3. **대화하기는 상태메시지 세팅뿐** — "대화 패널" 자체가 없음(피그마 npc-dialogue-panel 163:23/187:14 존재 —
   NPC 대화용은 NPCDialoguePanelUTK 구현돼 있으나 병사 대화는 미연결).

## Figma 진실치 — 수면 시스템 패널 (227:262 — 2026-10-15 실측)

창 624×1101 bg **#13181F** radius 14 (glass 스타일 아님 — 불투명 다크). 패널-로컬 좌표:
- **패널 제목 (24,24) 576×48** — moon 아이콘 24×24(#8B949E 계열 벡터) + "수면 시스템" 22/700 #F0F6FC +
  "휴식 및 진행 상황 저장" 12/400 #8B949E + 닫기 30×30 #FFFFFF/2% radius 5 (우측, x 16×16)
- **수면 설정 (24,92) 576×452**:
  - 휴식 장소 (24,92) 576×70 #FFFFFF/2% radius 10 — 장소 아이콘 42×42 #1C232C radius 7(bed-double) +
    장소 이름 "전초기지 · 개인 숙소" 14/700 #F0F6FC + 설명 12/400 #8B949E + 안전 상태 배지 (504,116) 82×22
    #58A6FF/8% radius 5(shield-check + "안전 지역" 12/500 #58A6FF)
  - 수면 시간 (24,182) 576×193 — "수면 시간" 16/700 + 현재 날짜(우측) 12/400 #8B949E +
    **기상 미리보기 (24,213) 576×92 #58A6FF/8% radius 10**: 현재(44,229) 시간항목 12/400+시간 32/500 #F0F6FC /
    중앙 "8시간 수면" 12/500 #58A6FF + 화살표 / 기상(484,229) 시간항목 "기상 예정 · N일차"+시간 32/500
  - **수면 시간 선택 (24,317) 576×58** — 3버튼 188×58 radius 7: "2시간/짧은 휴식"·"4시간/가벼운 수면"·"8시간/충분한 수면"
    비활성 #FFFFFF/2% 텍스트 14/700 #F0F6FC+11/400 #8B949E / **활성 #58A6FF** 텍스트 #0B0E14
  - 회복 예상 (24,395) 576×149 — "회복 예상" 16/700 + "8시간 수면 기준" 12/400 우측 +
    회복 항목 2개(283×92 #FFFFFF/2% radius 10): 체력(heart 16×16 + "+36" 12/400 #58A6FF + 64→100 18/400→18/700 +
    게이지 255×4 #58A6FF/8% bg + fill #58A6FF radius 2) / 기력(zap + "+72" + 게이지)
  - 수면 안내 (24,530) — info 14×14 + "수면 중에는 게임 시간이 흐르며, 부대원의 임무는 계속" 12/400 #8B949E
- **세이브 (24,564) 576×403**:
  - 항목 제목 (24,584) — save 아이콘 18×18 + "세이브" 16/700 + "현재 진행 상황을 안전하게 보관" 12/400 우측
  - 자동 저장 설정 (24,615) 576×63 #FFFFFF/2% radius 10 — "수면 전 자동 저장" 14/500 + 설명 12/400 +
    **스위치 (548,635.5) 38×22 radius 11** — ON #58A6FF(손잡이 16×16 우측) / OFF #FFFFFF/2%(손잡이 좌측) + "켬" 12/400 #58A6FF
  - 수동 저장 목록 (24,690) — "수동 저장 슬롯" 14/700 + "5개 슬롯" 12/400 우측 +
    목록 영역 (24,719) 556×248 스크롤 — **슬롯 행 556×81 #FFFFFF/2% radius 10 간격 10**(아이콘 40×44 #1C232C radius 5 +
    슬롯 정보 381×53 + **"지금 저장" 버튼 (483,741.5) 83×36 #58A6FF/8% radius 7** — 텍스트 12/700 #58A6FF)
    ×5(스크롤바 8×248 thumb #58A6FF radius 999)
- **수면 실행 (24,987) 576×90** — 취소 버튼 (24,1005) 124×46 #FFFFFF/2% radius 7("취소" 14/700 #F0F6FC) +
  **수면 시작 (160,1005) 440×46 #58A6FF radius 7**(moon 18×18 + "저장 후 수면 시작" 14/700 #0B0E14) +
  단축키 안내 (24,1063) "Enter 확인 · Esc 닫기" 12/400 #8B949E

## Phase 분할

### Phase A (구현 위임) — SleepPanelUTK 신설(피그마 227:262 전면 수용) + 게임 로직 신설
파일: 신규 `Assets/Scripts/UI/Toolkit/SleepPanelUTK.cs`, `Assets/Scripts/Systems/SleepAutoSaveToggle.cs`(신규 시스템), 테스트 1.
1. **SleepPanelUTK** — 위 진실치 그대로 구현(UTKWindowBase Frameless + 절대 배치 + 배지/버튼/스위치/스크롤).
   Bed.OnInteractRequestedUTK(UTKWireUp 33행 경로)를 SleepPanelUTK로 교체 — 구 SleepUTK는 유지(은퇴).
   - 데이터: TimeManager(Hour/CurrentDay/IsNight/GameTime)/Bed(BedName)/PlayerHealth(HP) — 실측만.
   - 기상 미리보기 = 현재시간+선택시간(24h wrap + 일차 가산) — 실측 계산(위조 없음).
   - 회복 예상: **체력 = HP 한도까지 +36/8시간 기준**(계수 = 8h 기준 36% 비례: min(36, 누락분)) 실측 계산 —
     실내 씬(스태미너 부재)은 기력 행 "-". 월드 씬은 PlayerMovement.Stamina 실측.
2. **게임 로직 신설(피그마에 맞춘 변경)**:
   - **자동 저장 스위치** — SleepAutoSaveToggle(정적 bool, 기본 ON) — ON: 수면 시작 직전 SaveManager.AutoSave
     (토큰 "sleep_autosave") / OFF: 저장 스킵. 값은 PlayerPrefs 영속.
   - **수면 회복** — 수면 완료 시점(SleepCoroutine 콜백 또는 SleepPanelUTK 자체 코루틴 경유)에
     PlayerHealth.Heal(누락분×비례) + PlayerMovement 스태미너 회복(리플렉션/공개 프로퍼티 — Systems 내부라 직접).
     회복량 = 휴식시간 비례(8h 기준 체력 36% — 피그마 목업치를 계수로 채용) — 실측 현재/최대 기반 계산.
   - **수동 저장 5슬롯** — "지금 저장" 버튼 = SaveManager.SaveToSlot(i) 기존 API 사용(존재 확인 — 없으면 AutoSave 계열
     public 메서드 확인 후 연결). 슬롯 행 정보 = SaveManager 기존 슬롯 메타(타임스탬프/일차) — 조회 API 없으면
     파일 시스템 직접 조회(GetSlotFilePath public 확인).
   - **"저장 후 수면 시작"** — 자동저장 ON이면 저장→수면, OFF면 수면만. 취소/닫기/Esc/Enter 단축키(피그마 안내).
   - 수면 오버레이: 기존 SleepUI DoSleep 동일(0.3~2s 클램프) — SleepPanelUTK 내부 실행(구 SleepUI와 이중 방지).
3. 테스트: SleepPanelFigmaTests — 진실치 마커(수면 시스템/휴식 및 진행 상황 저장/전초기지/안전 지역/회복 예상/
   수면 전 자동 저장/수동 저장 슬롯/지금 저장/저장 후 수면 시작/Enter  확인)+ 로직 마커(SleepAutoSaveToggle/
   PlayerPrefs/Heal/스태미너/기상 미리보기 계산/SaveToSlot 연결).

### Phase B (구현 위임) — 상호작용 피드백 + 대화 카메라 확대
파일: InteractionPanelUTK.cs, 신규 `Assets/Scripts/Systems/GuardTalkBubble.cs`(월드 말풍선), 신규
`Assets/Scripts/UI/Toolkit/DialogueCameraDirector.cs`, GuardPlaceholder.cs(로그 정합 최소), 테스트 1.
1. **결과 피드백(불가능 해소의 본질)**:
   - GuardTalkBubble(월드 말풍선 — Systems): guard._statusMessage를 잡아 3D 말풍선(TextMesh — NameplateDisplay 선례)
     4초 표시. `GuardPlaceholder.StatusMessageChanged` 이벤트 신설(세터 래핑 — 기존 로직 무변경) → 구독.
   - 실패 피드백: 뇌물/포섭 결과 bool을 InteractionPanelUTK가 받아 UTKToastService.Show("골드 부족 — 뇌물 실패(XX골드 필요)"/
     "입단비 부족"/"포섭 완료") — 조용한 실패 제거. AttemptBribe/OnRecruit은 bool/메시지 반환 유지(외부 API 불변).
   - **16씬 초기 골드 세팅** — IndoorWarTestSetup.EnsurePlayer 후 AddGold(500, "test_seed")(유저가 뇌물/포섭을
     시험할 수 있게 — 15씬 선례).
2. **대화 카메라 확대(DialogueCameraDirector — Systems, 정적)**:
   - 대화하기 클릭 → `DialogueCameraDirector.Begin(target)` — 대상을 2m 앞에서 보는 사선 카메라로 전환
     (기존 카메라 저장/복원 계약), 대상 병사 시선을 플레이어로 회전(대화 자세), 말풍선은 대상 머리 위.
   - 대화 진행: 말풍선 상태메시지 1개 출력 후 4s → 카메라 복원 + 패널/말풍선 정리. 이동 입력/ESC로도 즉시 복원.
   - Main Camera 치환 방식: Camera.main 저장 → 신규 카메라를 primary로(depth 우선) — PlayerCastleInteriorPlaytest/
   IndoorWar 카메라 체계와 충돌 없게: 기존 Camera.enabled=false(복원 시 원복), AudioListener 비활성 주의(없으면 스킵).
   - 말풍선 갱신: StatusMessageChanged 재구독 — 다중 대사 연속 표시(4s 간격).
3. 패널 정합 — 대화하기 라우팅을 `DialogueCameraDirector.Begin(guard)` 후 말풍선 표시로 교체(기존 패널 유지/닫힘).
   물약/음식 — 구창 아이템 선택 경로 유지(72차) + 지급 후 말풍선.
4. 테스트: DialogueFeedbackTests — StatusMessageChanged 이벤트/말풍선 라이프(4s)/카메라 저장·복원 마커/토스트 마커/
   16씬 시드 골드 마커.

## 공통
- 컴파일 error CS 0 ×2회 / EditMode 전수(883/872/11 유지 — 신규 회귀 0).
- Play 게이트(사용자): ①침대 E → 피그마 수면 시스템 패널(시간/기상 미리보기/회복 예상/자동저장 스위치/수동 5슬롯/
  저장 후 수면 시작) ②수면 완료 후 HP/스태미너 실제 회복 + 자동저장 동작 ③병사 대화하기 → 카메라 확대 + 대상
  말풍선(대사) + 복원 ④뇌물/포섭 → 토스트(골드 부족 시 안내 포함) ⑤기존 창/링/걷기 무사고.
- stage/commit/push는 사용자 승인 후.
