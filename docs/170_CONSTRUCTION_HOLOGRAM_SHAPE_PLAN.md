# 170차 — 건설 홀로그램 실물 형태 패리티 계획

> 사용자 요구: "건축에서 건물을 지을 때 건물 모양대로 홀로그램이 나오도록". 본 문서는 계획만 포함(구현 미착수).

## 0. 요구·목표

- 건설창에서 설계도 선택 → 배치 모드 진입 시, 마우스를 따라다니는 홀로그램이 **실물 건물과 동일한 형태**로 표시되게 한다(현재는 바닥판 Cube+원통 자리표시자).
- 기존 홀로그램 계약(유효/무효 틴트·반투명·콜라이더 비활성·지면 접지·Esc 취소·Confirmed 잔존)은 그대로 유지 — **형태만** 실물과 일치시킨다.

## 1. 현황 실측 (근거 — 2026-11-22 판독)

- 홀로그램: `ConstructionHologram.BuildHologramPrimitives`(L106-125) = footprint Cube + 원통 자리표시자. L108에 "TODO: Implement specific primitive recipes" — 형태 미구현 상태.
- 건설창 제공 설계도 **6종**(ConstructionWindowUTK L143): bp_cooking / bp_forge / bp_restaurant / bp_barracks / bp_alchemy / bp_traininghall — **전부 실물 시각 소스 존재**.
  - bp_restaurant → `RestaurantPropBuilder`: 몸통 박스 + 4면 지붕 + 문 + 국가색.
  - 시설 5종 → `FacilityPropBuilder`: effectId별 절차 프리미티브 레시피 + 국가색 틴트 + BuildingPlaceholder + E키 상호작용.
- bp_wall/bp_gate/bp_tower 등은 건설창 미제공(레거시 정의, 실물 시각 부재) — **범위 외**.
- 회전: 건설 플로우에 회전 개념 없음(ConstructionManager에 rotation/Quaternion 0건, StructureEntry 회전 필드 부재) — 홀로그램 회전 불요.
- 홀로그램 기존 계약(유지 대상): ValidColor(청록)/InvalidColor(적) 틴트, URP Lit 반투명(queue 3000), 콜라이더 전부 비활성(105차 결함4), LateUpdate 지면 접지(106차 B — RaycastAll 지면류 선호), Esc 취소(Following만), StructureCompleted까지 Confirmed 잔존(105차 결함2).

## 2. 접근 — 실물 빌더 시각 파트 단일 소스화 (Option A 채택)

| 대안 | 판정 |
|---|---|
| **A. 실물 빌더 시각 파트 재사용** | **채택** — 형태 패리티 완전, 실물 변경이 홀로그램에 자동 반영(단일 소스 계약) |
| B. 홀로그램 전용 레시피 신설 | 기각 — 형태 이중 관리, 실물 변경 시 드리프트 |
| C. 완성물 프리팹 스냅샷 | 기각 — 실물이 절차 생성이라 프리팹 부재 |

## 3. Phase 분해

### Phase A — 빌더 시각 파트 공유화 (구현 서브에이전트 1건, 순차 선행)

- **A1** `FacilityPropBuilder`: 시설 실물 파트 생성 블록을 `public static GameObject BuildFacilityVisual(Transform parent, BlueprintDef def, NationType nation, bool hologram)` 정적 함수로 추출.
  - 기존 StructureCompleted 경로는 동일 함수를 hologram=false로 소비 → 실물 회귀 0.
  - hologram=true: ①콜라이더 생성 금지(생성 즉시 제거 포함) ②E키 상호작용·BuildingPlaceholder·FacilityOpenRequested 이벤트 배제 ③생성된 Renderer 목록 반환(호출자가 홀로그램 머티리얼 적용).
- **A2** `RestaurantPropBuilder` 동일: `BuildRestaurantVisual(...)` 추출(몸통+지붕 4면+문).
- **A3** 범위 파일: `FacilityPropBuilder.cs`, `RestaurantPropBuilder.cs` (+ 기존 관련 테스트 회귀 확인).
- **A4** 국가색: hologram 모드에서도 nation 파라미터 유지하되 최종 색은 홀로그램 틴트가 덮는다(형태 공유, 색상은 홀로그램 계약 우선).
- **완료 기준**: 실물 경로(기존 테스트) GREEN + "실물 경로와 홀로그램 경로 파트 수 동일" 신규 어서션.

### Phase B — 홀로그램 연동 (구현 서브에이전트 1건, A 완료 후)

- **B1** `BuildHologramPrimitives` 교체: bp_restaurant → BuildRestaurantVisual(hologram:true), 시설 5종 → BuildFacilityVisual(hologram:true). 반환 렌더러에 기존 `SetHologramMaterial` 적용 → `UpdateTint` 유효/무효 계약 그대로 재사용.
- **B2** 그 외 blueprintId(건설창 미제공 정의) → 기존 자리표시자(Cube+원통) 유지(방어 경로 — 계약 변경 없음).
- **B3** 콜라이더: `DisableGhostColliders` 기존 계약 유지 — 공유 파트에 콜라이더가 생겨도 전부 비활성(LateUpdate 지면 레이캐스트 자기 히트 방지, 105차 결함4 재발 방지).
- **B4** 위치/스케일: 실물과 동일 치수, 실물 빌더의 원점 기준(바닥/중심) 실측 후 `_ghostRoot` 접지 Y 오프셋 확정(RestaurantPropBuilder의 groundY 처리 실측 포함).
- **B5** 범위 파일: `ConstructionHologram.cs` 1개.
- **완료 기준**: 6종 전부 홀로그램이 실물 형태로 렌더 + 기존 홀로그램 계약(틴트/접지/취소) 회귀 0.

### Phase C — 테스트·QA

- **C1** `ConstructionHologramTests` 확장: ①음식점 홀로그램 파트 구성(몸통+지붕 4면+문) = 실물과 동일 ②시설 5종 홀로그램 파트 수 = 실물과 동일 ③홀로그램 머티리얼(queue 3000·반투명) ④콜라이더 전부 비활성 ⑤틴트 유효/무효 반영 ⑥미제공 blueprintId 폴백 자리표시자 유지.
- **C2** QA 서브에이전트: 컴파일(error CS=0) + EditMode 전수(Phase A 리팩터 회귀 집중) + Play 배치 투어(마을씬 건설창→홀로그램 스크린샷 6종) — **Play는 별도 승인 게이트**.
- **위임 규약**: A/B 순차 위임(B가 A의 신규 API 소비), C는 별도 QA 서브에이전트. 타임아웃 시 64차 상설 승인(부모 직접 폴백) 적용.

## 4. 사용자 확인 포인트 (구현 전 답변 대기 — 무응답 시 기본값)

1. **색 계약**: 홀로그램 색은 기존 유효(청록)/무효(적) 유지, 국가색은 실물 완성 시점 그대로 — 기본값: 유지.
2. **부지 표시**: 기존 footprint 바닥판(Cube)을 홀로그램 아래 유지 — 기본값: 유지(부지 반경 표시 계약).
3. **시공 중(Confirmed) 홀로그램**: 실물 형태 유지 잔존(기존 계약 그대로) — 기본값: 유지.

## 5. 완료 기준 (전체)

- 건설창 6종 전부: 홀로그램 형태 = 실물 형태(파트 수·치수 동일), 색은 기존 유효/무효 계약, 콜라이더 0.
- 컴파일 error CS=0 + EditMode 전수 회귀 0 + 신규 테스트 GREEN.
- Play 투어 스크린샷 6종(승인 후) — 홀로그램이 건물 모양으로 보이는 것 시각 확인.
