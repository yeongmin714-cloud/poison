# 스탯창 UI 정비 + 핫바 직접 드래그 + 시선/카메라 끊김 + 내부씬 렌더링 + 경고 정리 계획 (2026-09-09 4차)

> 실행 규약: 코드/QA 위임(타임아웃 시 부모 직접), CS=0 게이트, 3중 저장+텔레그램.
> 선행: 2026-09-09 3차(인벤 삼분활) 계획서.

## 0. 유저 요구 정리

1. **인벤토리 예시 2 그대로**: 중간 설명창 **세로길이 축소** + **퀵슬롯은 설명창에 두지 않고** 항상 떠 있는 하단 핫바로 **인벤토리에서 바로 드래그**
2. **63.PNG 스탯창 UI 깨짐** 수정 (뷰포트 검은 화면/십자선, 스탯 텍스트 겹침·패널 밖 넘침, 장비슬롯 라벨 겹침, 노란 노이즈, 퀵슬롯과 창 겹침)
3. **시선 처리 끊김**: 마우스 커서 따라 시선은 되는데 카메라가 계속 정면을 쳐다보려 하고 시점 전환 시 끊김 → 원인 분석 + 자연스럽게
4. **내부 씬**: ①"The referenced script (Unknown) missing" ②"씬에 카메라 없음 — 말풍선 표시 불가"(BuildingTrigger) ③**내부 카메라 렌더링 안 됨**(실내 지형 안 보임)
5. **TerrainBaseMapFixer 경고** 재발 제거

## 1. 조사 결과 (루트원인)

### 스탯창 UI 깨짐 (63.PNG 분석)
- **뷰포트 검은 화면**: RT 카메라 버그 — SetupPreview에서 카메라(0,-1997.2,-4.2) pitch 8°인데 클론은 (0,-2000,0) → 약 34° 하향이 필요한데 8°만 기울어 **클론이 프레임 밖** → RT 검정. 회색 십자선은 RT 위 겹침 요소.
- **스탯 행 텍스트 패널 밖 넘침**: SetBonusNote 문자열이 길고(예: "민첩 5 — 치명 +2.5% / 속도 +0.25") Text가 Overflow(줄바꿈 없음)+폭 280px → 삐져나감. 라벨-값 수직 정렬 미스도 동일 라인.
- **장비슬롯 라벨 겹침**: BuildEquipSlot 라벨(y+SlotSize+2)과 아이템명(y+SlotSize+20) 간격 18px 부족.
- **노란 노이즈/글리프 깨짐**: LegacyRuntime.ttf의 한글/이모지 글리프 한계 추정 — 프로젝트 내 한글 폰트 에셋 존재 여부 확인 후 교체.
- **퀵슬롯과 스탯창 겹침**: WinH 900 중앙 정렬 → 하단 990까지 내려옴(핫바 top 918) → 창 높이 축소 필요.

### 시선/카메라 끊김 — 경쟁 구조 확인
- **카메라 드라이버 3개가 모두 LateUpdate**: ①PlayerMovement 내장 커스텀 탑다운 카메라(1044행, "vcam/Brain 완전 비활성 후 강제 적용" 주석 — 현재 주력) ②CameraZoomControllerRuntime.LateUpdate ③CinemachineCameraBinder.LateUpdate
- **이중 반응 루프**: 마우스 이동 → ①커서 지면 조준으로 플레이어 회전(Slerp 10/s) + ②동시에 커스텀 카메라 yaw도 마우스로 회전 → 카메라 움직임 → 커서 지면점 이동 → 플레이어 재회전 **피드백 루프** = 끊김/부자연
- "카메라 정면을 쳐다보려고 함": 3개 드라이버가 같은 카메라 transform을 서로 다른 값으로 덮어쓰는 충돌로 추정(어느 것이 최종 승자인지 P0에서 확정)
- Cinemachine vcam/Brain 비활성 주석 존재 → 활성 경로 확정이 최우선 진단 과제

### 내부 씬
- **IndoorScene에 Camera 오브젝트 0개** — 메인 카메라가 플레이어 추적으로 실내를 비추는 구조. 렌더링 실패 원인 후보: ①실내 조명 부재(URP는 라이트 없으면 검정) ②Unknown 스크립트로 전환 체인 일부 미실행 ③컬링/레이어
- **"The referenced script (Unknown) missing"**: 삭제된 스크립트 guid를 참조하는 잔존 MonoBehaviour — 전 씬/프리팹 m_Script guid 유효성 스캔으로 소유 오브젝트 특정 필요(3차에서 IndoorScene IndoorRoot 1건 제거한 전례 — 다른 곳에 추가 잔존)
- **BuildingTrigger:80 경고**: Start 시점에 Camera.main/FindFirstObjectByType 둘 다 null(카메라가 GameSetup.Start에서 런타임 생성되어 실행 순서상 아직 없음) → Start 1회 체크 대신 Update 재시도 필요. 2개 트리거가 각 1회씩 출력.

### TerrainBaseMapFixer 재발
- Ground_Grass_Mat.mat의 _BaseMap에는 이미 guid 22d5b657... 존재 → 그런데도 "비어 있어 복구함" 발화 = ①체크 조건이 잘못됐거나 ②복구 후 **AssetDatabase.SaveAssets 누락**으로 에셋에 저장 안 되어 세션마다 재발. P에서 확인 후 수정.

## 2. Phase 계획

### P0 — 진단 확정 (QA 에이전트, 읽기전용)
- [ ] 카메라 활성 경로 확정: GameSetup/씬에서 vcam/Brain 활성 여부, CameraZoomControllerRuntime/CinemachineCameraBinder/PlayerMovement 내장 카메라의 enabled·구독 상태 → 실제 승자 1개 특정
- [ ] Unknown 스크립트 소유 오브젝트 특정: 전 씬/프리팹 m_Script guid ↔ .cs.meta guid 대조 스캔 (Missing 스크립트 목록+오브젝트명 산출)
- [ ] IndoorScene 전환 런타임 흐름 리뷰: IndoorSceneTransition 전체 + 실내 조명/라이트 존재 여부(IndoorScene 라이트 오브젝트 수) → 렌더 실패 원인 순위
- [ ] TerrainBaseMapFixer.cs 체크 조건+저장 로직 확인
- [ ] 프로젝트 한글 폰트 에셋 존재 여부(Assets 내 ttf/otf + Font 에셋)

### P1 — 스탯창 정비 (코드 에이전트)
- [ ] 뷰포트: SetupPreview 카메라를 `transform.LookAt(클론+상단1m)`로 수정 + 클론 렌더 확인 로그 → 검은 화면 해소
- [ ] 스탯 행: Text 줄바꿈 활성(horizontalOverflow Wrap)+폭 확대, SetBonusNote 문자열 축약("민첩 +치명2.5%/속도+0.25" 형태), 라벨-값 수직정렬 정렬 값 통일
- [ ] 장비 슬롯: 라벨/아이템명 간격 재배치(라벨 y+2, 아이템명 y+24, 높이 22)
- [ ] 창 크기: WinH 900→760(중앙 유지, 하단 920 종료 → 핫바 top 918과 비겹침) + 레이아웃 y 좌표 일괄 재계산
- [ ] 폰트: P0에서 한글 폰트 발견 시 전면 교체, 없으면 이모지/특수문자 사용처 제거로 글리프 깨짐 최소화
- [ ] 배치컴파일 CS=0

### P2 — 설명창 축소 + 핫바 직접 드래그 (코드 에이전트)
- [ ] 설명 패널 세로 축소: WINDOW_HEIGHT 전용 높이 → 620 수준(내용: 이름/등급/아이콘/설명/효과), **핫바 미니패드 제거**
- [ ] HotbarUI에 `public static int GetSlotIndexAtScreenPoint(Vector2 pos)` 추가(_slotBgs RectTransform + RectTransformUtility.RectangleContainsScreenPoint, Overlay 캔버스)
- [ ] InventoryWindow.ProcessDrag: MouseUp 시 미니패드 대신 `HotbarUI.GetSlotIndexAtScreenPoint(Input.mousePosition)` → AssignItem (IMGUI MouseUp이 UGUI 위에서도 좌표만 맞으면 판정 가능 — 실패 시 Input.GetMouseButtonUp(0) 폴링 방식으로 전환)
- [ ] 인벤 그리드에서 드래그 시작은 유지, 설명창 축소에 맞춰 중앙 패널 레이아웃 정리(여백은 게임 화면이 비치는 예시3 스타일 유지)
- [ ] 배치컴파일 CS=0

### P3 — 시선/카메라 자연화 (코드 에이전트)
- [ ] 경쟁 드라이버 정리: 승자(PlayerMovement 내장) 외 2개(CameraZoomControllerRuntime/CinemachineCameraBinder)를 컴포넌트 비활성 or 조기 return — 기존 조작 기능(휠 줌 등)은 내장 카메라에 이식되어 있는지 확인 후 흡수
- [ ] 이중 반응 완화: 커서 조준 회전 유지하되 카메라 yaw는 마우스 X 직결 대신 **우클릭 홀드 시에만 회전**(또는 yaw 고정+부드러운 감쇠) — 유저 체감 기준 택1, 기본은 "마우스 이동=커서만, 카메라 yaw 고정"
- [ ] 카메라 자체 스무딩: 1182행 LookRotation 직접 대입 → yaw/pitch 지수 평활(Slerp)로 끊김 제거
- [ ] 배치컴파일 CS=0

### P4 — 내부 씬 3종 (코드 에이전트)
- [ ] Unknown 스크립트: P0 목록 기반 오브젝트에서 제거(씬 YAML 수술 또는 에디터 유틸 MissingScriptRemover 신설 — 재사용 가치 높아 유틸 권장)
- [ ] BuildingTrigger: 카메라 탐색을 Start 1회 → Update 재시도(60프레임 주기, _player와 동일 패턴) — 경고 소멸
- [ ] 내부 렌더링: P0 진단 결과에 따라 ①실내 라이트 부재 → IndoorSceneTransition 진입 시 PointLight 1~2개 자동 생성 ②카메라 미추종 → 추적 보강 ③컬링/레이어 문제 → 수정. (수정 후 실내 지형/벽/바닥 렌더 확인)
- [ ] 배치컴파일 CS=0

### P5 — TerrainBaseMapFixer (코드 에이전트, 소규모)
- [ ] 재발 원인 수정: 복구 후 SaveAssets 보장 + 체크 조건 정확화(_BaseMap 텍스처 guid 유효성) → 세션마다 경고 소멸
- [ ] 배치컴파일 CS=0

### P6 — 통합 QA (QA 에이전트)
- [ ] 최종 배치컴파일 CS=0 + 정적 검증(카메라 드라이버 단일화, 구독/RT 해제, Missing 0건)
- [ ] Play 판정 체크리스트:
  1. I키 → [인벤(장비5x2+가방6줄스크롤)][설명(축소)] 2패널
  2. 인벤 아이템 드래그 → 하단 핫바 슬롯 드롭 → 등록(숫자키 발동)
  3. P키 스택창: 뷰포트에 캐릭터 렌더(검정 아님), 텍스트 겹침/넘침 0, 퀵슬롯 비겹침
  4. 마우스 이동 → 카메라 흔들림/끊김 없이 부드러운 시선
  5. 성문 E → 실내 렌더(지형/벽 보임) + 말풍선 경고 소멸 + Unknown 스크립트 경고 소멸
  6. TerrainBaseMapFixer 경고 소멸
- [ ] P7 기록: QAPROGRESS/메모리/git push/텔레그램

## 3. 수정 파일 예상

| 구분 | 파일 |
|:--|:--|
| 수정 | StatusWindowUI.cs(뷰포트/텍스트/크기), InventoryWindow.cs(설명창 축소+드래그 대상 변경), HotbarUI.cs(GetSlotIndexAtScreenPoint), PlayerMovement.cs(카메라 스무딩/yaw 정책 — 위임 하에 최소 침습), CameraZoomControllerRuntime.cs/CinemachineCameraBinder.cs(비활성화), BuildingTrigger.cs(카메라 재시도), TerrainBaseMapFixer.cs(저장/조건) |
| 신규 | (필요 시) 에디터 MissingScriptRemover.cs, 실내 임시 라이트 생성 코드(IndoorSceneTransition 내) |
| 씬 | IndoorScene/기타 씬의 유령 스크립트 제거 |
| 문서 | QAPROGRESS/메모리 |

## 4. 리스크/주의

- **카메라 3중 드라이버 정리**: 조작감 회귀 위험 — 승자 1개만 남기고 나머지는 "비활성화(코드 유지)" 방식, 기능(휠 줌) 흡수 여부 P0에서 확인
- **시선 정책 택1**: 마우스=카메라 yaw + 플레이어=커서 조준이 본질적 이중 반응 — 기본안은 "카메라 yaw 고정, 플레이어만 커서 조준"(Diablo/Hades 표준), 유저 체감 후 조정
- **IMGUI→UGUI 드롭 좌표**: CanvasScaler 스케일 고려 — RectangleContainsScreenPoint 사용으로 해결(오버레이 캔버스는 카메라 null)
- **한글 폰트**: 프로젝트에 한글 폰트 없으면 신규 임포트는 범위 외 — 글리프 깨짐은 문자 선택으로 완화
- **실내 라이트**: URP에서 라이트 없는 실내는 검정 — 자동 생성 방식은 전환 해제 시 정리 필수
- PlayerMovement는 대형 파일(1200행+) — 수정은 카메라 블록에 한정, 이동 로직 무손상

## 5. 검증/배치
- build_stats.bat 재사용, Phase별 CS=0 게이트
- Play 판정: P6 체크리스트 6항목 (스크린샷 순번 보고 병행)
