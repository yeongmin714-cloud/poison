# 성 내부씬 배치 8종 랜덤 변형 (INTERIOR-VAR)

작성: 2026-09-08 | 상태: 실행중 | 선행: CastleInteriorBuilder(타영주)/PlayerCastleInteriorBuilder(내영지) 고정 레이아웃 2종 확인

## 배경
- 현재 성 내부씬은 2종(소유/타소유): IndoorSceneTransition.L139이 `_pendingIsPlayerOwned`로 분기.
- 각 빌더는 고정 배치 1종뿐 → 사용자 "두 종류 각각 8종씩 16개" 요구.

## 설계
1. **시그니처 확장**: 각 빌더에 `layoutVariant`(0~7) 파라미터 추가.
   - `BuildCastleInterior(string nationStyle)` → `BuildCastleInterior(string nationStyle, int layoutVariant)`
   - 기존 1인자 시그니처는 **오버로드로 유지**(내부에서 0 호출) → 외부 호출/QA 회귀 방지.
2. **8종 변형 요소** (각 빌더 내 가구 배치를 variant로 분기):
   - 변형 축: ①좌우 대칭 플립 ②앞뒤(±z) 배치 시프트 ③가구 세트 교체(2~3세트 × 게이트) ④기둥/문 위치 미세 변형.
   - 변형은 "유효한 방" 유지 — 벽 밖/가구 겹침 금지, 잠금문·기능 컴포넌트(저장고/무기고/작업대)는 항상 존재.
3. **진입 체인(IndoorSceneTransition)**: `_pendingTerritoryKey`(또는 nation+isPlayerOwned+진입 위치 해시)로 **결정론 시드** → `layoutVariant = hash % 8`. 같은 영지 재방문 시 같은 배치.
   - 단, 현재는 `EnterBuilding(buildingType, nationStyle, isPlayerOwned)` 시그니처로 territoryKey 미전달 — BuildingTrigger가 호출부. territoryKey가 있으면 그것으로, 없으면 nation+isPlayerOwned 해시로 폴백(결정론).

## 불변
- 1인자 호출(기존)은 항상 variant 0(기존 배치) — 동작 100% 보존.
- 잠금문/상호작용(TerritoryWarehouse/CraftingStation 이름 위치)은 variant와 무관하게 항상 생성(단 위치는 variant에 따라 움직이되 이름 유지).
- 시스템 무접촉: 빌더는 Systems, 참조 순환 주의.

## 파일
- Assets/Scripts/Systems/CastleInteriorBuilder.cs (타영주 8종)
- Assets/Scripts/Systems/PlayerCastleInteriorBuilder.cs (내영지 8종)
- Assets/Scripts/UI/IndoorSceneTransition.cs (결정론 시드 전달)

## 검증
- 배치컴파일 error CS=0.
- Play: 서로 다른 영지 2곳 진입 → 내부 배치 다름 확인. 재진입 → 동일 배치(결정론).