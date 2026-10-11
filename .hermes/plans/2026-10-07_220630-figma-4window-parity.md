# Figma 4창 백프로(100%) 정합 계획 — 병사관리 69:4 · 스테이터스 84:4 · 영지창고 15:4 · 요리크래프트 64:2

작성: 2026-10-07 22:06 KST / 기준: `.hermes/plans/figma-1920-baseline-2026-10-04/` (Figma `02vPXOGzFJUUYTaEPSVz3R`, 1920×1080)

## 0. 핵심 발견 — 스크린샷 4장은 "현재 소스보다 오래된" 상태다

스크린샷(`Screenshots/현재 병사 관리 창.PNG` 등 4장)과 워크트리 소스를 1:1 대조한 결과, **정합 작업의 상당 부분이 이미 미커밋 워크트리에 반영돼 있고 스크린샷은 그 이전(스테일) 상태**다.

| 창 | 스크린샷 상태 | 현재 소스 상태 (워크트리, 미커밋) |
|---|---|---|
| 병사관리 | 균등 3분할, `— 부대원 명부 —` 미니멀 헤더, 상세=[E] 버튼 | `SoldierManagementUTK` CompositionBounds(168,48,1584,984) + 3열(480/576/480, gap24) + Figma 헤더("부대원 명부 SOLDIERS LIST") + 배치 카드("N명 활성") + 적용 버튼 |
| 스테이터스 | PLAYER PACK 3슬롯, 장비 2열×4행 | `StatusWindowUTK` 84:4 2패널 조합(391.8,72,576,936 + 996.6,72,531.6,936) + P3 재구성 + **TACTICAL PACK 5×5 25셀** + EQ 태그 + 허기/중독 게이지 |
| 영지창고 | 4×5=20슬롯, +5슬롯(100G) | `WarehouseWindowUTK` 504×912 + **5×6 30셀 예약**(81.6px/gap9.6) + dashed placeholder + 용량라벨 절대배치, `WarehouseStorageFigmaParityTests` 8/8 통과 기록 |
| 요리크래프트 | 탭 전체/작물/생선/몬스터, 6×5, "인벤토리 슬롯 0/40" | `CookingWindowUTK` 64:2 구조 — 탭 **전체/주식/부식/소스**(Figma 라벨↔실데이터 매핑), StorageGrid 5열(24,139.8,456,537.6), Detail 패널(672,84,576,912)+이름/이미지(528×384)/설명 영역, "사용 슬롯 N/40"+진행바 푸터 |

→ **P0에서 4창을 새로 캡처해 현시점 증거로 교체하는 것이 첫 단계다.** 스크린샷 대상 증거는 캡처 이후의 최종 판정(P5)까지 사용하지 않는다.

## 1. 알려진 미해결 항목 (matrix Phase 9 + 본 세션 조사)

1. **EditMode 실패 3건 (2026-10-07 기록)**:
   - `CraftBenchFigmaPhase4Tests` 2건 — 소스텍스트 단정(`CraftingHelper` in shared base, `CookingBenchUTK`의 `"전체"`) vs 실제 소스 불일치 → 계약 확인 후 단정 조정
   - `SoldierManagementFigma69Tests` 1건 — 내부 요소 984±1 기대 vs 985.5 실측 → 어느 요소가 1.5px 넘치는지 특정 → **소스 수정 우선**, 불가 시 근거 명시하고 단정 보정
2. **Play 1920×1080 신규 캡처 없음** — 런타임 시각 정합 미검증 (P5까지 절대 100% 단정 금지)
3. 창별 잔여 내부 정합 아래 2절

## 2. 창별 잔여 갭 (Figma 렌더 vs 현재 소스)

### A. 병사관리 69:4 — `Assets/Scripts/UI/Toolkit/SoldierManagementUTK.cs` (685줄)
- **상세 패널 내부**: Figma = 이름 + 등급태그(퍼플 `#A371F7`) + 상세 설명 + **대형 프리뷰(~560×300)** + 4게이지 + **'고유 전술 특성' 텍스트** / 소스 = 이름/레벨/역할 + 4게이지(트랙 14px)만 → 섹션 신설 필요. ⚠ guard 데이터에 등급/특성 필드 실존 여부 확인 후, 없는 데이터는 **위조 금지** — dashed placeholder로 Figma 자리만 확보
- **카드 규격**: 목록 카드 ~440×80/gap12, 배치 카드 ~440×60/gap8 — `/v1/files` 노드트리 절대좌표로 재실측 후 코드 상수 대조 (vision 추정치 아님)
- **적용 버튼**: height 48 → Figma ~56 재실측 확인
- **배치 카드 라디오/체크 표시, 선택 카드 파랑 테두리+좌측 하이라이트 바** — 구현/보강
- **모서리 L 데코** — Figma 공통 크롬, 3패널에 적용 여부 재실측

### B. 스테이터스 84:4 — `Assets/Scripts/UI/Toolkit/StatusWindowUTK.cs` (1559줄)
- **장비 슬롯 축 불일치**: 실데이터 8종(투구/갑옷/무기/신발/장갑/등/가면/가방) vs Figma 5×2=10(방패/반지/목걸이 등 게임에 없는 슬롯 포함) → **기본안: Figma 5열×2행 그리드 지오메트리에 실 8슬롯 + 점선 빈셀 2** (가짜 슬롯 생성 금지)
- 노드트리 수치 대조: 초상화 영역(~520×280), 스탯 그리드, 게이지 두께/간격, TACTICAL PACK 셀 크기·간격
- TelemetryOverlay(240,48,1440,19)는 미구현·데이터 매핑 불명 → **본 계획 범위 밖** 명시 (원하면 별도 Phase)

### C. 영지창고 15:4 — `Assets/Scripts/UI/Toolkit/WarehouseWindowUTK.cs` (1335줄)
- 이미 고정합(테스트 8/8). 잔여: ①헤더 카피/용량 라벨 위치 재확인 ②**확장 버튼(+5슬롯 100G)** = 실게임플레이(TryExpandSlots)인데 Figma에 없음 → Figma 스타일로 정합 배치 유지(삭제 금지) ③Storage에 카테고리 탭이 Figma에 실존하는지 노드트리 재실측 ④용량 시맨틱(실 20캡 vs 모킹 14/80) = 실값+피그마 스타일 유지(기존 판정 유지)

### D. 요리크래프트 64:2 — `Assets/Scripts/UI/Toolkit/CookingWindowUTK.cs` (1229줄)
- 구조 이미 정합. 잔여: ①재료 슬롯/결과 슬롯/성공률 텍스트/레시피 행(60px)/COOK 푸터 규격 노드트리 재실측 대조 ②중앙 패널 콘텐츠(이름+영문+등급태그+이미지+사양)의 데이터 채움 상태 확인(레시피 선택 시 실제로 채워지는지) ③`CraftBenchFigmaPhase4Tests` 2건 단정 조정

## 3. Phase 실행 계획 (각 Phase: 컴파일 0 → focused EditMode → 커밋 게이트)

### P0 베이스라인 확정
1. fresh batch compile — `rm compile.log && ./compile_test.sh`, `error CS` 0 확인 (고유 로그만 판정)
2. focused Figma EditMode 재실행 → 3건 실패 현황 확인 → 계약 확인 후 소스/단정 조정
3. **4창 Play 1920×1080 캡처 신규 촬영** — 진입점: 병사관리=병영 지도 탁자 E / 스테이터스=P키 / 창고=창고 상호작용 E / 요리=요리 스테이션 E. 캡처는 `Screenshots/` 대신 `.hermes/plans/figma-1920-baseline-2026-10-04/captures/`에 저장해 판정 대상 분리 (기본안: 사용자 재촬영 요청)
4. 캡처 4장 vs Figma 렌더 4장 side-by-side(공통 표시 크기)로 확정 델타 표 작성 → P1~P4 스코프 확정

### P1 병사관리 (69:4)
- `/v1/files`에서 69:4 하위 절대좌표 재실측(frame origin 보정, absolute y=3 등) → 카드/게이지/버튼 수치표
- 상세 패널 섹션 신설(등급태그/설명/프리뷰/전술특성) — 실존 필드만, 없는 것은 dashed placeholder
- 카드 규격·간격·적용버튼 56·라디오/체크·하이라이트 바 교체
- `SoldierManagementFigma69Tests` 소스 수정 반영
- 검증: compile 0 + SoldierManagementFigma69Tests → 커밋(파일지정 add, .cs+.cs.meta)

### P2 스테이터스 (84:4)
- 노드트리 재실측 → 초상화/스탯/게이지/PACK 수치 대조 교체
- 장비 8슬롯을 Figma 5×2 지오메트리로 재배치 + 점선 2 (기본안)
- 검증: compile 0 + focused EditMode → 커밋

### P3 영지창고 (15:4)
- 헤더/탭 존재/확장버튼 노드트리 재실측 → 잔여 델타만 수정 (기존 8/8 테스트 회귀 금지)
- 검증: compile 0 + `WarehouseStorageFigmaParityTests` → 커밋

### P4 요리크래프트 (64:2)
- 슬롯/레시피 행/푸터 수치 대조 교체, 중앙 패널 데이터 바인딩 확인
- `CraftBenchFigmaPhase4Tests` 2건 단정 조정
- 검증: compile 0 + CraftBenchFigmaPhase4Tests → 커밋

### P5 최종 시각 승인
- 4창 Play 캡처 vs Figma 렌더 픽셀 대조(vision + 좌표) → 100% 승인은 이 게이트 통과로만 선언
- QAPROGRESS 선두 기록 + ROADMAP 갱신, 사용자 승인 후 잔여 커밋

## 4. 리스크/운영 주의
- **미커밋 대량 변경 보존**: 파일지정 add만, 커밋 전 `git diff <file>`로 타계획(가스/활/성내부) 변경 섞임 검증, 커밋 후 `git show --stat` 이상 diff 보고
- 노드트리 좌표는 반드시 **frame origin 보정** 후 코드 반영(절대 y=3 등) — 1920에서는 Figma 절대값 그대로, 재곱산 금지
- 대형 파일(1500줄급) patch 오버매칭 주의 — 블록 전체 read_file 확보 후 유니크 앵커로 편집, 즉시 컴파일
- Soldier 상세 신설 같은 **신규 조립은 부모 직접**(delegate 600s 타임아웃 실측)
- 확률형 EditMode(90% 크래프트) 간헐 실패 = 재실행 판정
- Figma 모킹 텍스트(레전더리/연봉/재료 12/10 등)를 게임 데이터로 위조 금지 — 실존 데이터만

## 5. 확인 필요 (기본안 있음 — 무응답 시 기본안으로 진행)
1. 스테이터스 장비 8종 vs Figma 10슬롯 → **기본안: 5×2 그리드에 실 8 + 점선 2**
2. P0 Play 캡처 주체 → **기본안: 사용자 4장 재촬영 요청** (캡처 슬롯 제공하면 그대로 비교)
3. TelemetryOverlay → **기본안: 범위 밖**
