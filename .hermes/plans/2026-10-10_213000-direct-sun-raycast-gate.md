# 직사광 레이캐스트 게이트 (P2)

## 문제
모든 실내 방의 AERO 직사광 Directional Light는 `rotation = (-50, -35, 0)` 고정 틸트로 생성된다
(InteriorLightingDirector.cs, Indirect/Direct/Performance 경로 3곳 모두 고정 값, 토폴로지 무시).

탑다운 플레이에서 저각도(-50° 피치) 직사광은 실내 벽에 차단되어야 하지만 AERO 파이프라인의
직사광은 섀도우 캐스팅 없이 룸 전체에 균일하게 입혀져, "창 없는 실내인데 벽을 뚫고
대각선으로 셀이 밝아지는" 위화감을 낳는다. 게다가 고밀도 룸 경계를 가로질러
광 강성 불연속선(stray light seam)이 생긴다.

## 접근: DirectionalLightFrameData에 시작 타깃 집계 추가
직사광 색/강도는 프레임당 유지하고, 렌더러가 프레임당 단일 수동 tilt 레이캐스트를 수행해
"직사광이 이 룸에 실제로 도달하는가"를 판정한다. 도달하지 않으면 프레임 데이터의
직사광 강도를 0으로 설정한다(라이트 컴포넌트 비활성화가 아니라 단일 프레임 데이터 억제).

### 기존 코드와의 인터페이스
- `AeroRenderCore.FramePrepare`는 프레임당 라이트 정렬을 이미 수행함 (PerFrameStats.DirectLightSortMs 존재).
- 새 정적 `AeroSunAngleGate.CalculateDirectLightData(AeroLightColor)`를 프레임 정렬 후 단 1회 호출.
- 기존 강도를 계속 억제하는 PerFrameStats 카운터를 재사용(제로 썸 동작).

### 구조
1. **새 게이트 클래스 `AeroSunAngleGate`** (AeroRenderCore가 활성화한 프레임마다 정적 사용):
   - `static bool IsDirectLightBlockedInRoom(Vector3 sunDirection, Bounds roomBounds)` —
     룸 AABB에서 태양 방향으로 6면 레이캐스트(Physics.queriesHitBackfaces = true) 수행,
     먼 레이가 룸 밖에서 종료되면 직사광 도달 성공으로 간주.
   - `static float GetBlockedLightAttenuation(...)` — 도달 성공 1.0, 실패 0.0 반환.
   - 레이 캐스트는 매 프레임 최대 6회, Physics.queriesHitBackfaces 상태 저장/복원으로 사이드 이펙트 최소화.
2. **`InteriorLightingDirector` 확장**: 프레임당 1회 게이트 평가 수행,
   결과를 `AeroRenderCore` 프레임 데이터로 전달하는 신규 콜백/필드.
3. **`AeroRenderCore` 프레임 데이터 확장**: 직사광 렌더링 시 게이트 감쇠를
   최종 직사광 강도에 승수로 적용. 직사광 색/각도/기타 경로 데이터는 불변.

### 성능
- 프레임당 6회 AABB 레이캐스트 = 무시 가능 (루믹 4-cast/칼리퍼스 대비 1000배 저렴).
- 레이캐스트 대상: 룸 AABB 표면만. LOD 레벨 3(최소 분해능)이어도 정확.
- 프레임당 1회. 라이트 수와 무관(단일 Directional).

### 리스크
- 룸이 매우 좁으면(벽 0.5m 간격) 6면 중 4면이 즉시 밖으로 나가 게이트가 오작동할 수 있음 →
  레이캐스트 시작점을 룸 AABB 중심으로, 레이 길이를 `roomBounds.size.magnitude * 2`로 설정해 완화.
- Physics.queriesHitBackfaces는 전역 상태 → 진입/종료 저장-복원.

## 테스트 (EditMode, ProjectName.Tests.EditMode.AeroSunAngleGateTests)
1. `Gate_OpenRoom_AllowsFullDirect` — 룸 AABB에 장애물 없음 → 감쇠 1.0.
2. `Gate_RoofObstruction_NullsDirect` — 룸 위에 박스 콜라이더 → 감쇠 0.0.
3. `Gate_BackfaceQueryState_RestoredAfter` — 게이트 실행 후 Physics.queriesHitBackfaces 값 복원 단정.
4. `Gate_RaycastBudget_Within6` — 룸 중심에서 max 6회 레이캐스트 상한 단정(스파이 히트 카운트).
5. `Gate_FarLight_HitsRoomCenter` — 저각도 태양이 벽 위 중심 룸에 도달하는 케이스 단정(감쇠 1.0).

## 게이트 판정
- 컴파일 error CS 0
- focused: AeroSunAngleGateTests 5/5
- 전체 EditMode 회귀: baseline 7건 외 신규 실패 0

## 위험 신호
- 게이트가 오픈 룸에서도 0.0을 반환하면(과도 억제) 피벗 방향 6면 레이캐스트 대신
  룸 내부 시작점+태양 반대 방향으로 단일 레이캐스트 방식으로 전환.
