# 실내 관람 모드(탑다운 걸어보기) 계획서 — 2026-11-06

## 0. 사용자 지시 + 확정 답변
- 지시: "모든 씬에 플레이어가 탑다운 뷰로 움직일 수 있도록 해서 실내씬 모두를 움직이면서 확인" —
  정적 스크린샷 투어(수조 라운드 42장)를 넘어, 게임 안에서 직접 걸으며 실내 전 씬을 점검하는 모드.
- clarify 확정(기본안 그대로): **정투영 완전 탑다운 + 휠 줌 / [ ]키 순회 + 현재 씬명 라벨 /
  신규 Test_23 경량 씬 / 14엔트리 전부**. 라운드 번호는 수조 라운드와 함께 사용자 부여 대기(무번호로 진행).

## 1. 재사용 부품 (전부 실측 검증됨 — 오늘 텔레그램 투어 프로브 14/14)
- `IndoorSceneTransition.EnterBuilding(type, nation, owned, territoryKey)` — 건물 11종 전환 공용 경로
  (플레이어 스폰/미니맵 캡처/수조 훅 자연 발화 실측). ExitBuilding()(L663) 존재 확인.
- `CaveLevelDirector.GoDeeper(room)` — 동굴 F2 하강(Cave146 동일 경로, 리빌드+캡처 훅 2 발화).
- `CaveInteriorBuilder.BuildCaveInterior("cave_test", 2)` — 동굴 F1 부트(CaveInteriorTestSetup 실측 경로).
- `IndoorDioramaKit` — 천장 제거+남벽 시각 절단(콜라이더 유지) 8빌더 적용 완료 → 탑다운 가시성 이미 확보.
- `TavernInteriorBuilder.BuildTavernInterior("default", 1)` — EnterBuilding에 case 부재 → 직접 빌드(투어 선례).
- 키 셀프감사: 기존 배정 = E·R·B·V·F·Q·U·F6·F12·Space·Shift+방향·더블탭WASD·Mouse0/1·Escape —
  **[ ] 충돌 0**(전수 grep 실측).

## 2. P1 — IndoorTourMode.cs 신규 (Assets/Scripts/Systems, ~250행)
씬 게이트 `gameObject.scene.name == "Test_23_IndoorTour"`(다른 씬 무동작 — Test_19/21 선례).
- **순회 시퀀스(14엔트리, 결정론)**: cave_f1 → cave_f2(GoDeeper) → house → castle_player(owned=true) →
  castle_lord(owned=false, territoryKey="tour_lord_key") → shop → restaurant → barn → crafthouse →
  church → arena → arenapit → afterlife → tavern(직접빌드+CaptureFor+플레이어 중앙 스폰) —
  IndoorTourRunner 목록과 상수 1:1 공유(이중 소스 금지).
- **키**: `]` 다음 / `[` 이전 (링 순회 — tavern → house 랩어라운드).
- **cave 재진입 경로(순회 중 cave_f1/f2 도달)**: ExitBuilding() → 잔존 CaveRoom 파괴 →
  BuildCaveInterior("cave_test",2) 재빌드 → 플레이어 입구 스폰 + IndoorMinimapBaker.CaptureFor.
  cave_f2는 항상 cave_f1 경유 후 GoDeeper(단방향 계약 준수).
  ⚠ 실측 항목: ExitBuilding 후 재빌드 시 실내 상태/플레이어 스폰 정합 — 실패 시 폴백
  **cave=시작 지점 고정 + 12건물 링 순회**로 축소(보고에 명시).
- **탑다운 카메라 접수**: 메인 카메라 상태 백업 → orthographic=true, pitch 90° 고정,
  플레이어 추적, 휠 줌(orthoSize 3~30, 기본 10), 기존 TopDownCameraController enabled=false
  (DialogueCameraDirector 92차 접수/복원 선례 — 파괴 아님, PlayerMovement 구버전 제거 규칙 무충돌).
  복원 경로도 제공(관람 종료 = 컴포넌트 비활성 시 원복).
- **라벨**: OnGUI 좌상단 소형 라벨 — "실내 관람 — 현재: {label}  ([ 이전 / ] 다음 / 휠 줌)"
  (테스트씬 OnGUI 선례 — CaveInteriorTestSetup. 신규 UTK 창 불필요).

## 3. P2 — Test_23 씬 + 부트 (위임 or 부모 소형)
- `Assets/Editor/IndoorTourTestSceneBuilder.cs` 신규(~80행) — 155차 Afterlife155TestSceneBuilder 선례:
  메뉴 "Tools/IndoorTour/Test_23_IndoorTour 씬 생성" → Assets/Scenes/TestScenes/Test_23_IndoorTour.unity
  (기존 파일 있으면 덮어씀). 씬 내용: CaveInteriorTestSetup + IndoorTourMode 부착 GameObject + 조명.
- `CaveInteriorTestSetup.cs` **1줄 수정**: 씬 이름 게이트에 Test_23 허용
  (현재 `!= TestSceneName` 단일 비교 → 2종 허용). ⚠ 계약테스트가 게이트 문자열을 핀 중인지 확인 후 진행
  (핀 있으면 갱신 — 무단 변경 금지).

## 4. P3 — 계약테스트 + 전수 게이트
- `Assets/Scripts/Tests/EditMode/IndoorTourModeTests.cs` 신설(6건 목표):
  ①순회 시퀀스 14엔트리 = IndoorTourRunner 목록 1:1 ②씬 게이트(타 씬 무동작) ③카메라 접수
  (ortho 전환+기존 컨트롤러 disabled)·복원 계약 ④[ ] 키 배정 소스계약(중복 배정 0) ⑤라벨 문자열 계약
  ⑥cave 재진입 헬퍼 결정론(폴백 시 해당 테스트 생략 아님 — 축소판 계약으로 재편).
- 게이트: 컴파일 error CS 0 + EditMode 전수 baseline 10건 1:1(현행 1978/1968/10 기준), 신규 실패 0.

## 5. P4 — 프로브 + 사용자 판정
- `IndoorTourModeProbe`(에디터 배치, IndoorTourProbe 선례 승계): Test_23 부트 → 자동으로
  cave_f1→cave_f2→house 3엔트리 순회 캡처(탑다운 실측) → DONE/FAIL. 부모 직접 실행(-quit/-nographics 금지).
- 사용자 Play 판정 대기: ①탑다운 시점 체감 ②[ ] 순회 동작 ③각 실내 걸어보기(가구/경로/조명)
  ④cave 재진입 동작(폴백 적용 시 축소 범위 안내).

## 6. 리스크
- cave 재진입 재빌드 경로 미실측(§2 폴백 명시) — 검증된 경로(투어 14/14) 외 신규 경로는 최소화.
- 성내부 대형 홀은 기존 조명 어두움 이슈(125차 후속8)가 탑다운에서도 그대로 보임 —
  별도 판정 항목(수조 라운드 대기 4항목)과 연결, 본 라운드에서 수리하지 않음.
- arenapit/afterlife/arena는 수조 제외 확정(실외/관전) — 탑다운에서 지붕 문제 없음.

## 7. 대기
- "진행" 승인 → P1~P3 위임 구현+독립 QA → P4 프로브 → 사용자 Play 판정(별도 승인).
- 커밋은 기존 누적(154차+텔레그램 146~159차+수조 무번호+본 라운드)과 함께 별도 승인.
