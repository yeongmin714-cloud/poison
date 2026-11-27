# [125차 후속37] 건설창(ConstructionWindowUTK) 수리 + Figma 재정합 계획/구현

작성: 2026-10-26 (구현+셀프감사 완료 — 부모가 컴파일/테스트 사후 단일 패스 실행)
대상: Assets/Scripts/UI/Toolkit/ConstructionWindowUTK.cs + Assets/Scripts/Tests/EditMode/ConstructionWindowTests.cs
Figma: 239:683 "전초기지 건설 관리" 1600×1040 (API 재실측 2026-10-26, 키 02vPXOGzFJUUYTaEPSVz3R)

## Q1. 증상과 원인 (확정)

사용자 스크린샷 — 건설창 개방 시 **'왼쪽 목록이 하나도 안 뜸'**.

근본 원인(NRE 연쇄):
1. `Refresh()`가 선두에서 `RefreshHeaderTime()` 호출
2. `RefreshHeaderTime()`이 `_headerDayLabel`/`_headerClockLabel`를 널가드 없이 역참조
3. 후속13으로 `BuildHeaderBar()` 미호출(헤더 소실 계약) → 라벨 null → **NRE**
4. NRE로 `Refresh()` 전체 중단 → `RefreshBlueprintList()` 도달 불가 → **좌 목록 미생성**

## A. 수리/재정합 3건 (구현 완료)

### A1. RefreshHeaderTime NRE 수리 (좌 목록 복구)
- 본문 선두에 널가드 추가: `if (_headerDayLabel == null || _headerClockLabel == null) return;`
- **헤더는 소실 상태 유지가 정답** — `BuildHeaderBar()` 호출 복원 금지(후속13 계약 준수).
  정의만 남기고 호출부 0 유지(grep 실측). 헤더 복원 시 가드는 자동 무해화.
- `TimeManager` 실측 경로(tm.CurrentDay/GetFormattedTime) 무변경 — 데이터 소비 계약 유지.

### A2. Figma 239:683 현재값 재정합 (후속12 글래스 역전 — API 재실측 근거)
| 요소 | Figma 현재값(실측) | 구현 변경 |
|---|---|---|
| 루트 프레임 | fill SOLID **#0B0E14@1.00** 불투명 | 생성자 `Color.clear` → `FigmaRootBg`(기존 상수 재사용) |
| 건설 목록(좌) | 480×882 @(64,78) fill **#13181F@1.00** r14 | `UTKTheme.GlassPanel` → `PanelBg` + ApplyStroke(스트로크는 원래 유지 중) |
| 선택 시설 건설 정보(우) | 968×882 @(568,78) fill **#13181F@1.00** r14 | 상동 |
| 도움말바 | fills=[](투명) — 유지 | 무수정 |
| DisableWindowShadow | Figma 프레임 drop shadow 없음 | 유지(무해 — Figma에 섀도우 부재 확인) |

- UTKTheme.cs는 **8개 창 공유 토큰 파일 — 무수정**(GlassPanel 토큰은 다른 창이 계속 사용).
- Figma 트리 재확인: 좌 패널 구성(패널 제목/필터/시설 목록/요약 간격/보유 재료 요약 432×110) 현행 유지 —
  요약박스는 Figma에 존속하므로 432폭 그대로 유지(항목과 무관).

### A3. 시설 항목 폭 420 + 스크롤바 가시화 (Figma 사용자 수정치)
- **항목 432→420**(-12px, 스크롤바 공간): `FacilityItemW = 420f`. 내부(시설 정보 408→396 /
  시설 소개·시설명·시설 역할 276→264 / 재료 보유량과 필요량 408→396)는 좌/우 앵커 기반 배치라
  항목 폭 축소가 **전부 -12px로 자동 전파**(소스에 408/276 리터럴 없음 — read_file 확인).
- **스크롤바 신규**(Figma scrollbar-track/thumb 실측):
  - 트랙 4×537 r2 **#20262E** @ 패널 로컬 (452,168) — 목록 프레임(432 = 항목 420 + 간격 8 + 트랙 4,
    Figma '시설 목록' 432×565) 우측 가장자리 정렬
  - 썸 4×r2 **#58A6FF**(=Accent 동값) — Figma 정지값 높이 208은 '스크롤 가능 상태 예시'로 해석하고
    구현은 실측 content/viewport 비율 동기화(`UpdateItemScrollbar`: content = 항목 수×(101+8) 설계
    상수 산출 → 레이아웃 미확정 시점에도 판정 가능, 스크롤 불필요 시 썸=뷰포트 전체)
  - ScrollView 기본 수직 스크롤러는 `ScrollerVisibility.Hidden`로 숨김(중복 바 방지 — 휠/터치 스크롤 유지),
    `verticalScroller.slider` 값 변경 콜백으로 썸 실시간 동기화, 트랙/썸 `pickingMode=Ignore`(클릭 비간섭)

## B. 테스트 갱신 (ConstructionWindowTests.cs)

1. **L249 역갱신**: 루트 투명 단언(`Color.clear`) → 불투명 `FigmaRootBg` 계약 + `new StyleColor(PanelBg);`
   계약 + `UTKTheme.GlassPanel` 잔존 금지(Not.Contain) [125차 후속37].
2. **신설 테스트 1건**: `RefreshHeaderTime_NullGuard_And_FigmaRealign_125_37` — ①널가드 존재+위치(본문 선두)
   ②Refresh 호출 순서(헤더 시각 → 좌 목록) 유지 ③BuildHeaderBar 미호출(호출 복원 금지) 단언
   ④FacilityItemW=420 ⑤ItemScrollbarTrack/Thumb 요소+ScrollTrackCol+UpdateItemScrollbar 소스 계약.
3. **불변 확인**: 헤더 폴백 제거 단언(L237/310/392 — `EnsureInlineBackgroundFallback(header)` 부정)은
   헤더 미빌드 유지로 그대로 유효. MeasureGeometryAndDefend/LogOpenScreenState HeaderBar 관련 계약은
   호출부 없음 유지로 무손상.

## C. 셀프감사 결과

- UTKTheme.cs 무수정 확인(git status — 변경 파일은 ConstructionWindowUTK.cs/ConstructionWindowTests.cs뿐.
  WorldMapWindowUTK.cs는 본 위임 이전부터 있던 변경분 — 손대지 않음).
- `UTKTheme.GlassPanel` 문자열 ConstructionWindowUTK.cs 내 잔존 0(grep 실측).
- `BuildHeaderBar();` 호출 0(정의만 유지 — grep 실측). 헤더 폴백 제거 단언 3곳 불변.
- 중괄호 균형 검사 pass(awk depth 0). 벡터메시 미사용, UTKGaugeStripArc 등 기존 계약 미접촉.
- 93/94/96/99/104/112차 진단·폴백·부착 계약 전부 무손상(스케줄/로그/메서드 시그니처 미변경).

## D. 변경 파일:행 (구현 후 기준)

- ConstructionWindowUTK.cs
  - L77-78: ScrollTrackCol 신규 상수(#20262E — Figma scrollbar-track 실측)
  - L92-104: FacilityItemW 420 + 스크롤바 상수(TrackW 4/Gap 8/Radius 2/ViewportH 537)
  - L302-309: 생성자 루트 배경 FigmaRootBg 불투명 원복(후속12 역전 주석)
  - L745-749: RefreshHeaderTime 널가드(후속37 주석)
  - L799/812-831: RefreshBlueprintList 말미 UpdateItemScrollbar 호출 2곳 + UpdateItemScrollbar 신설
  - L969/1170: 좌/우 패널 PanelBg 원복
  - L1051-1088: 목록 프레임 432(=420+8+4) + ScrollerVisibility.Hidden + 트랙/썸 빌드 + 스크롤 콜백
  - L1412: BuildFacilityItem 요약 주석 432→420
- ConstructionWindowTests.cs
  - L246-256: 루트/패널 불투명 계약 역갱신(후속12 단언 교체)
  - L397-445: 신설 테스트 RefreshHeaderTime_NullGuard_And_FigmaRealign_125_37

## E. 남은 위험 / 후속 판정 필요

1. **Play 게이트**: NRE 수리로 좌 목록 5종(기타 탭 8종) 렌더 확인 필요 — 스크린샷 재검수.
2. 썸 높이/위치는 설계 상수 기반 근사(항목 수×109) — 실측 resolvedStyle 기반 전환은 Play 확인 후 판단.
3. 헤더바는 Figma에 존재하나 후속13 사용자 지시로 소실 유지 — 재부활 요청 시 BuildHeaderBar 호출만으로 복원
   (가드가 자동 무해화되어 추가 수리 불필요).
4. 기본 스크롤러 숨김에 따른 드래그 스크롤(썸 자체 드래그)은 미지원 — 휠/터치 경로만(시각 동기화 목적 바).
