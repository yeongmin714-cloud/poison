# 146차 계획 — 던전 1→2층 하강 "외부 판정" 수리 + 실내씬 미니맵 전환

작성일: 2026-11-05 · 상태: **계획(미승인) — 구현 전 사용자 승인 필요**

## 목표

1. **이슈 A**: 동굴 던전 테스트씬(Test_21)에서 1층→2층 하강 시 플레이어가 "건물 외부"로 판정되어 2층에 들어가지 못하는 문제 수리.
2. **이슈 B**: 모든 실내씬 진입 시 미니맵이 **실내씬의 간단한 지형(평면도)** 로 전환. 메인씬=메인씬 지도, 실내씬=실내씬 지도.

## 정적 조사로 확정된 현재 상태

### 이슈 A 관련 (동굴 다층 — 139차 P4 / 143차 P7·P9 1단, 미커밋 진행중)
- `Assets/Scripts/Systems/CaveLevelDirector.cs` — GoDeeper/GoUp = 룸 파괴 → 리빌드 → 텔레포트 → 토스트. `TeleportPlayerToFloorSpawn()` → `GetFloorSpawnPosition(CurrentFloor)`.
- `Assets/Scripts/Systems/CaveInteriorBuilder.cs`
  - `FloorLayouts[1]` = F2 채굴 갱도층 15각(EntranceEdge **−1 = 전면 폐쇄, 개구부 없음**).
  - `GetFloorSpawnPosition(floor, tier)` L254 — F2 스폰 `(−8.0, 1.5, 0.6)` 하드박제. **1인자 경유 시 tier=1 고정** → tierScale(1+0.25×(tier−1)) 미동행. F1 입구 스폰은 "tier 무관 유효(스케일 동행)" 계약인데 F2 스폰은 미동행 — 불일치.
  - F2 포탈: 상승(−9.0, 0.6) / 하강(10.2, −0.6) — F2 스폰(−8.0, 0.6)이 상승 포탈 2.2m 판정 반경 **내부**에서 시작.
  - `CreateFloorPortals` L977 / `CreateFloor2Zones` L896 — 좌표 하드박제.
- `Assets/Scripts/Systems/CaveInteriorTestSetup.cs` — Test_21은 단독 씬: `CaveTier = 2`(scale 1.25), `UITransitionState.IndoorActive = true`만 세팅(**IndoorSceneTransition.IsIndoor는 false 유지** — 리플렉션 `IsIndoorScene()` 조회 시스템들은 실내로 인식 못함).
- "건물 외부 판정"의 코드 단일 원인은 **아직 미확정** — 후보 4종:
  - ① 텔레포트 좌표↔tierScale/룸 오프셋 불일치 (F2 스폰 미스케일 동행)
  - ② F2 전면 폐쇄(개구부 −1) — 스폰/추락/벽 콜라이더로 물리적 고립
  - ③ E키 경합 — 포탈 반경 2.2m 내 다른 E 상호작용(GemChest·ResourceNode·InteractionPanel)과 경합해 하강 입력이 묵살
  - ④ 실내 판정 플래그 이원화(IsIndoor vs IndoorActive) — Test_21에서 리플렉션 조회 시스템 오판

### 이슈 B 관련 (미니맵)
- `Assets/Scripts/UI/Toolkit/MinimapUTK.cs` (637줄) — 싱글턴+DDOL, 배경=`TerrainSplatBaker.LastWorldSplat`(월드 스플랫), 플레이어 중심 크롭=`UpdateMapCrop`(WORLD_SIZE UV 산식 — `MinimapRouteOverlay`와 단일소스 공유 계약), 영지/퀘스트 마커, 유닛 블립 풀, 온도/소음/날씨 필, `Refresh()` 말단 훅 계약(WorldMapFigma124Tests — "훅 1곳만 배선" 최소 변경 계약).
- **실내 전용 처리 전무** — 실내 룸은 씬 원점 부근이라 월드 지도 상 중앙(Empire_1) 핀으로 표시되는 게 현재 동작.
- 실내 룸 생성 단일 지점 존재: `IndoorSceneTransition.BuildAndSettleIndoor`(재로드/제자리 리빌드 공용 경로 — 후속12~16에서 공용화 확인) + 동굴 층전환은 `CaveLevelDirector.BuildFloor`.

---

## Phase 계획

### P0 — 이슈 A 진단 (근거 확정, 수리 없음 — 확정 전 수리금지 계약)
- **0.1 정적 검산 스크립트/덤프**: F2 스폰(−8, 0.6)×tierScale별 다각형 내부 판정, 룸 루트 오프셋 실측, F2 zone/노드/포탈 좌표 전부 다각형 내부 검산, E키 경합 컴포넌트 목록화 → `TestOutput/146_cave_f2_audit.txt`
- **0.2 로그 계측 지점 선정**(수리 아님, 진단용): CaveLevelPortal.Update E수신 로그, GoDeeper 단계별(파괴→빌드→텔레포트) 로그, 텔레포트 전후 플레이어 위치 로그
- **0.3 사용자 Play 재현**(별도 승인 게이트) — 영상→프레임+vision+log 판정(사용자 워크플로 준용)
- **산출**: 후보 4종 중 원인 확정 → P1 수리안 확정. (로그상 원인이 복합이면 모두 기록)

### P1 — 이슈 A 수리 + 회귀 계약
- 확정 원인별 수리안(사전 설계 — P0 확정 후 채택):
  - (a) 좌표계: `GetFloorSpawnPosition`에 tier 동행(2인자 경유 강제) — F1 스케일 동행 계약과 정합. 층별 스폰을 tierScale 곱셈 단일화.
  - (b) 판정 게이트: 실내 판정 단일소스 정리 — `IsIndoor ∨ UITransitionState.IndoorActive` 공용 헬퍼(리플렉션 조회 시스템 대상).
  - (c) E키 경합: 포탈-상호작용 거리 정렬/우선순위 (필요 시만).
- **계약테스트**(`Assets/Scripts/Tests/EditMode/CaveInteriorQualityTests.cs` 확장): 층별 스폰 ∈ 해당 다각형 내부(전 정의층 × tier 1~3), 포탈 좌표 내부 검산, 소스계약.
- **검증**: compile_test.sh(CS 0) + EditMode 전수(기존 baseline 1:1, 신규 실패 0).

### P2 — 실내 미니맵 (A안: RT 탑다운 캡처 — 권장)
- **2.1 `IndoorMinimapBaker` 신설**(UI/Toolkit): `Capture(GameObject room)` — 임시 ortho 탑다운 카메라(태그 없음, MainCamera 비침범) → RT 캡처(512², ReadPixels→Texture2D) + roomBounds(AABB) 반환. 선례: SoldierManagementUTK RT 레시피(카메라+ReadPixels). 캡처 1회/룸 재빌드 — 프레임 상시 비용 없음.
- **2.2 캡처 훅**: `BuildAndSettleIndoor` 완료 지점 + `CaveLevelDirector.BuildFloor` 말미에 이벤트 발화(try-catch — 크래시 금지 계약). 두 경로 모두 커버(실내↔실내 전환 포함).
- **2.3 MinimapUTK 실내 브랜치** — `Refresh()` 내 분기(**훅 1곳 최소 변경 계약 유지**):
  - 실내: 배경=실내 RT, 좌표산식=roomBounds 매핑(**순수 함수 분리** — 월드 UV 산식 단일소스 파손 금지), 영지/퀘스트 마커+`MinimapRouteOverlay` 숨김, 플레이어 핀+유닛 블립(실내 로컬) 유지.
  - 퇴출: 월드 경로 원복(LastWorldSplat 재사용 — 재베이크 없음).
- **2.4 실내 암전 대책**: 캡처 시 임시 앰비언트 상향 후 복원(Flat 강제 계약 유지) 또는 무광 — 구현 중 실측 후 선택.
- **검증**: EditMode 계약테스트(소스계약 + bounds 매핑 순수함수 수치 어서션) + 컴파일 + **사용자 Play 3컷 판정**(실내 진입 / 동굴 층전환 / 퇴출 복귀).

### 대안 (기각 제안 — 기록용)
- **B안(스키마틱 폴리곤 맵)**: 빌더별 레이아웃 데이터(UI 배선) 필요 — 빌더 8종+성 8변형+동굴 층별 데이터 배선 비용 대비 A안(범용 RT 캡처)이 전 실내씬 일괄 커버. 부속 실루엣 품질 이슈는 2.4로 흡수.

---

## 셀프감사 (중복·키충돌)
- 신규 입력 키 없음(E키 재사용 — 경합은 P1 (c)에서만 다룸).
- MinimapUTK "훅 1곳" 최소 변경 계약 — 실내 브랜치도 Refresh 내부 분기로 충족(구조 재편 없음).
- `UpdateMapCrop` 산식 공유 계약(MinimapRouteOverlay) — 실내 매핑은 별도 순수함수, 실내 중 오버레이 비활성으로 충돌 회피.
- 월드맵 패널(패널@0.80+현재지핀+전송=미니맵경로) 계약 무손상 — 본 계획은 WorldMap* 파일 전부 미변경.
- 동굴 계약(탑다운 천장 금지·룸 1개·리빌드 방식) 무변경 — 수리는 좌표/판정 계층만.

## 리스크 / 열린 질문
1. 실내 RT 캡처 품질(암전 실루엣 vs 밝은 평면도) — 사용자 취향 확인 필요(기본: 임시 앰비언트 상향).
2. Test_21(가짜 플래그)에서 실내 브랜치 동작 여부 — P1 (b) 단일소스 정리로 함께 해결 가능.
3. 캡처 타이밍 — 룸 빌드 직후 1프레임 지연 필요 가능성(메시 완성 보장).
4. 이슈 A 원인이 정적 검산으로 불확정 시 — P0 로그 계측이 필수 선행(수리 선불 금지).

## 게이트
- 이 계획 승인 → P0 진행 → 원인 보고 → P1 수리 → P2 실내 미니맵.
- Play 실행·커밋은 각각 별도 승인.
