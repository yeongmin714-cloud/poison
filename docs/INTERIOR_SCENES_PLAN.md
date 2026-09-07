# 🏰 영지 내부씬 2종 분리 계획 (Interior Scenes: Owned vs Lord-Owned)

> **요구 (사장님):** 영지(성)에 진입할 때 내부씬으로 전환. **점령 후 자신 소유** 영지 내부씬과 **점령 전 타 영주 소유** 내부씬을 **두 개 별도로** 만든다.
> 참고: `Screenshots/영지 내부 예시.PNG`(타 영주 웅장한 왕좌/집무실), `Screenshots/내부씬 예시.PNG`(기능적인 기지/창고·작업·장비)
> 작성일: 2026-09-07

---

## 현재 구조 (조사 완료)

| 파일 | 역할 | 상태 |
|:-----|:----|:----:|
| `Systems/CastleInteriorBuilder.cs` | 성 내부 생성 — 왕좌실+집무실+무기고+금고+문서고(잠금문 4개), 국가별 텍스처. `BuildCastleInterior(nationStyle)` | 단일 |
| `Systems/IndoorSceneTransition.cs` | `EnterBuilding(type,nation)` → Additive 로드 → `case "castle"`: BuildCastleInterior + SpawnInteriorFixtures | 단일 |
| `Systems/BuildingTrigger.cs` | 성문 근접 E키 → `BuildingEvents.RequestEnterBuilding("castle", nationStyle)` | 단일 |
| `Systems/IndoorTransitionSetup.cs` | `CreateBuildingTrigger(pos,type,range,parent,nationStyle)` — 트리거 생성 | 단일 |
| `Systems/TerritoryBuilder.cs` | 성/성문+buildingTrigger 생성, nationStyle 매핑, SpawnInteriorFixtures(상점/크래프트) | 단일 |
| `Core/Data/TerritoryData.cs` | `TerritoryOwnership` enum: `Unoccupied/PlayerOwned/LordOwned/Contested` | ✅ |
| `Core/Data/TerritoryDatabase.cs` | `GetState(nation,index)` / `GetState(TerritoryId)` / `GetState(key)` / `SetOwnership(...)` | ✅ |

핵심: 현재 모든 성 내부 = CastleInteriorBuilder 1개. **소유 상태를 인자 삼아 2종을 분기**해야 함.

---

## 설계 원칙

- **색/느낌 = 소유 상태가 결정**(기존 방위 국가색은 유지하되, 소유 상태에 따라 배치·장식·조명이 달라짐).
- **타 영주 소유 (LordOwned)** = 기존 `CastleInteriorBuilder` 유지/개선 — **웅장한 중세 왕좌실 + 집무실 + 잠금문**(침략 전, 적 영주의 거점). "영지 내부 예시" 성격.
- **자신 소유 (PlayerOwned)** = **신규 `PlayerCastleInteriorBuilder`** — **기능적인 내 기지/집무실**: 저장고(상자)/작업대(크래프트)/무기고(무기 걸이)/문서/병참 접근 가능, 침실/환영 배너, 관리자 분위기. "내부씬 예시" 성격.
- 두 빌더 모두 `NationTerrainController` 국가색(동/서/남/북/황제국)은 유지하되, **소유 상태만으로 레이아웃/가구/기능 분기**.
- 진입 결정: 성문 BuildingTrigger 위치의 소속 영지가 `PlayerOwned`인지 여부 → `IndoorSceneTransition`에 isPlayerOwned 전달 → 빌더 선택.

---

## 구현 단계

### Phase A — 소유 조회 인터페이스
- `TerritoryBuilder.BuildBuildingsAt`가 해당 영지의 `TerritoryKey`(예: `"East.1"` 또는 `TerritoryId`)를 알고 있음(parent.name = `Territory_{nation}_{index:02}`).
- `BuildingTrigger`에 추가: `public string TerritoryKey;` — 성문 트리거 생성 시 세팅하면, E입력 시 `TerritoryDatabase.GetState(territoryKey).ownership == PlayerOwned` 판정 가능.

### Phase B — BuildingEvents/Transition에 소유 플래그 전달
- `BuildingEvents.RequestEnterBuilding(string, string nationStyle=null, bool isPlayerOwned=false)` — 인자 추가(선택).
- `BuildingTrigger.Update` E입력 시: castle이면 `TerritoryDatabase.GetState(TerritoryKey).ownership==PlayerOwned` → 플래그 세팅해 전달.
- `IndoorSceneTransition.EnterBuilding(..., bool isPlayerOwned=false)` → `_pendingIsPlayerOwned` → `OnIndoorSceneLoaded` case "castle"에서 빌더 분기.

### Phase C — PlayerCastleInteriorBuilder 신규
- `Systems/PlayerCastleInteriorBuilder.cs` 신규: `BuildPlayerCastleInterior(nationStyle)`.
- 기능적 레이아웃: 저장·선반(상점), 크래프트 작업대, 무기/방어구 걸이, 문서 책상, 침운/권리 공간, 환영 배너(플레이어 소유지 표시).
- 조명: 밝고 정돈된 중세 가옥풍(내부씬 예시의 "실용적 기지").
- 국가색 텍스처 재사용(IndoorTextureGenerator 의 country*).
- `IndoorFurniturePlacer` 재사용(테이블/의자/예술 사물함/원목).

### Phase D — IndoorSceneTransition castle 분기
```csharp
case "castle":
    string nation = _pendingNationStyle ?? "Empire";
    GameObject interior = _pendingIsPlayerOwnedForCastle
        ? PlayerCastleInteriorBuilder.BuildPlayerCastleInterior(nation)
        : CastleInteriorBuilder.BuildCastleInterior(nation);
    if (interior != null) TerritoryBuilder.SpawnInteriorFixtures(interior.transform.position, nation);
    break;
```

### 검증
- 배치컴파일 error CS=0 선행.
- Play: ① 타 영주 영지(점령 전) 성문 E → 웅장한 왕좌실 ② 자신 소유 영지(점령 후) → 기능적 내부+창고/작업대 ③ 국가별 텍스처 유지 ④ 상점/크래프하 트리거 여전히 동작.

---

## 파일 매핑 (변경/생성)
| 파일 | 작업 |
|:-----|:----:|
| `Systems/CastleInteriorBuilder.cs` | (유지·필요시 가구 강화) |
| `Systems/PlayerCastleInteriorBuilder.cs` | **신규** — 기능 내부 |
| `Systems/BuildingTrigger.cs` | TerritoryKey + isPlayerOwned 전달 |
| `Systems/BuildingEvents.cs` | RequestEnterBuilding에 bool 인자 |
| `Systems/IndoorSceneTransition.cs` | castle 분기(_pendingIsPlayerOwned) |
| `Systems/TerritoryBuilder.cs` | 성문 트리거에 TerritoryKey 전달 |
| `Systems/IndoorTransitionSetup.cs` | CreateBuildingTrigger에 territoryKey 파라미터(선택) |