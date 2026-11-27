# TERRITORY_SCATTER_VILLAGES_PLAN.md — 영지 산포 재배치 + 흙길 재계산 + 영지당 마을

작성: 2026-10-15 (79차 — 전수 실측 기반, **구현 전 / 사용자 승인 대기**)
요구(사용자 확정): ①원형 링 배치 폐기 ②중앙에 가까울수록 난이도 증가만 유지 ③영지마다 마을 ④폴리헤븐 폐기(지형=Idyllic 완성 — 지형 재생성/확장 없음) ⑤저사양 배려 장치 없음(고사양 데스크톱 실행)

---

## 0. 실측 확정 사항 (계획의 근거 — 전부 코드 실측)

**난이도 = 링 enum 기반 — 좌표와 무관 (산포해도 안 깨짐)**
- 영지 난이도: `TerritoryDifficulty` enum (Ring1~4/Empire) — `GenerateAllDefinitions`가 좌표와 **함께** 배정하므로 산포식에서 difficulty 유지만 하면 됨 (`TerritoryDatabase.cs` L226~252)
- 몬스터 레벨 보정: `MonsterLevelData` Ring1~4/EmpireBonus (enum 기반)
- 병사 수: `GetGuardCount(nation, difficulty)` / 살려주기 보상·공물: 링 배율
- 이계 티어(78차): `GetTierFactor(ring)` — enum 기반
- **몬스터 스폰 난이도 = 거리 밴드 기반** (`MonsterSpawner.SpawnConfig`: safe 0~250 / intermediate 250~800 / beginner 800~1000m — 중심 거리) → "중앙 가까울수록 어려움" 철학이 이미 스폰 쪽 구현돼 있음. 산포와 무충돌.

**국가 = 순수 각도 쐐기 기반 (반경 무관)**
- `NationTerrainController.GetNationFromPosition` L1006~1030: `atan2(z,x)` 각도만 봄 (East −45~45 / North 45~135 / West 135~225 / South 225~315, 중앙 50m=Empire)
- 지형 국가색/링색 지대 = **거리 밴드** 기반 (좌표와 무관) — 산포해도 지형색 정합 유지
- 따라서 **영지가 쐐기 안에만 있으면** 국가색·지형·모든 소비처 정합

**기존 좌표·구조 실측**
- 영지 82곳: 4국가×4링×5 + Empire(0,0 고정) + Dracula(1350m @ 150° 고정, 밤 전용)
- 현 배치: 링 원 위 (Ring1 1450/Ring2 1000/Ring3 550/Ring4 150m × 결정론 지터 0.85~1.10, 18° 슬라이스)
- 문제: Ring4 150m 링 = 같은 링 간격 **47m** — 마을(반경 40m) 불가능. 산포의 주 이유.
- 흙길: `NationTerrainController.BuildDirtPathSegments` L601~656 — **하드코딩** (스포크 4개 국가 기저각 0/90/180/270° 정확히, 링도로 550/1000m 원호) → 산포 시 영지를 안 지나감. + `AppendVillageConnections` L663 (마을→최근접 도로 수선발 접속 — 동적, 유지 가능)
- 마을: **기존 24곳** (국가당 6 — 영지 인덱스 {1,4,6,9,11,14} 선정, 성에서 38~46m 오프셋, 반경 40m, 광장+집4~6+창고+우물, 대표마을 4곳 실외 상점) — `VillagePlacementSystem` (결정론, 순수 static) + `VillageBuilder` (GLB hut/프리미티브 폴백, 국가 틴트) + `VillageNpcSpawner` (마을당 주민 10~15, NPCAmbientDialogue)
- 지형 보호: `TerrainGenerator` — 성+마을 앵커 반경 45m **완전 평탄화**+15m 페이드, 절벽 보호 60m — **마을/영지 앵커를 실측 좌표에서 자동 수집** (L1246, L1407) → 산포 좌표 따라감 (무수정)
- 데코: `IdyllicDecoPlacer.PlacePathEdgeDeco` — `NationTerrainController.DirtPaths` 소비 → 흙길 재계산 따라감 (무수정)
- NPC/기타 소비처: WorldMap(±1600 extent), Minimap, FastTravel, WarMarch, GuardManager.FindNearestPlayerTerritory, `ResolveTerritoryAt(pos, 60m)` — **전부 좌표를 동적으로 소비** (무수정)

**세이브/호환**
- 영지 좌표는 코드 생성 데이터 — 세이브 미포함 → 기존 세이브 호환 무문제
- 영지 이름/영주/병사수/성향 = 좌표와 무관하게 생성 유지

---

## 1. 설계 확정 (사용자 지시 반영)

### 1-1. 난이도 밴드 (원 폐기 — 거리 구간)
| 난이도 | 거리 밴드 (중심 기준) | 비고 |
|---|---|---|
| Ring4 (최난) | 50~300m | 5개/국가 |
| Ring3 | 300~650m | 5개/국가 |
| Ring2 | 650~1100m | 5개/국가 |
| Ring1 (최약) | 1100~1550m | 외곽 한계 1550 < 지도 extent 1600 |

- **단조 원칙**: 밴드 간 경계 명확 — Ring4 영지는 항상 Ring3 영지보다 중심에 가까움 = "중앙 가까울수록 어려움" 보장
- **쐐기 유지**: 각 영지는 자기 국가 쐐기(90°) 안에 배치 — 쐐기 경계 ±6° 안쪽 마진 (국가색 경계 혼선 방지)
- **Empire(0,0)/Dracula(1350m@150°) 고정 유지** — 산포 대상 아님. 단, 최소 간격 규약에 Dracula 포함

### 1-2. 최소 간격 (판정 정합 규약)
- 영지 중심 간 최소 **120m** (전 국가/링 포함 전역 — `ResolveTerritoryAt` 반경 60m ×2)
- 스폰 지점(728,−529) 제외 앵커: 영지 중심 ≥ **100m**
- 마을 중심 간 간격: 마을은 성 옆 38~46m이므로 영지 간격 120m가 자동 보장 (마을↔타 영지 중심 ≥ 120−46=74m > 60m ✓)

### 1-3. 산포 알고리즘 (결정론 — 재시작 동일)
1. 각 영지 (nation, ring, t)에 대해 밴드 내 후보: 시드 = 기존 `GetDeterministicHash("{nation}_{ring}_{t}_scatter")` → `System.Random`
2. 후보 = 밴드 [min,max] 균등 거리 + 쐐기 마진 각도 (국가 기저각 ±(45−6)° 균등)
3. **리젝션**: 기존 확정 영지들과 거리 < 120m 또는 스폰 < 100m면 재추첨 (최대 200회)
4. 실패 폴백: 밴드 중간값 + 슬라이스 각도(기존 18° 등분식) — 절대 실패 없음(기존 배치와 유사 위치)
5. 전부 확정 후 **전역 재검증** (어설션 로그) — 위반 0건 보장

### 1-4. 흙길 = 결정론 MST (하드코딩 폐기)
- 노드: 중앙 허브(0,0) + 스폰 수선발 + 영지 82 + 마을 80 = **164 노드**
- 간선: Kruskal MST (거리 오름차순, tie-break = 노드 인덱스 — 결정론) → 163 세그먼트, **전체 연결 보장 + 사이클 없음**
- 마을은 자기 성에 짧은 가지(38~46m)로 붙음 — MST가 자연히 선택
- `PathSegment` 구조/`PaintDirtPaths`/`PlacePathEdgeDeco` 소비부 **무수정** — `BuildDirtPathSegments` 본체만 교체 (`AppendVillageConnections` 제거 — MST에 흡수)
- 스포크 직선감 개선 옵션: 세그먼트 중간에 결정론 지터 꺾임점 1개 (계획 보류 — 우선 직선, 플레이 감상 후 결정)

### 1-5. 마을 — 영지당 1개 (24 → 80)
- `VillagePlacementSystem`: `VillagesPerNation` 6 → **20**, 영지 인덱스 = **1~20 전체** (Ring4 포함)
- 마을 등급 (과밀 배제 — 링 밀도 반영):
  - **일반 마을** (Ring1~3, 60곳): 현행 유지 (광장 r6 + 집 4~6 + 창고 + 우물, 주민 6~10)
  - **촌락** (Ring4, 20곳): 축소 (광장 r4 + 집 2~3 + 우물, 주민 3~5) — 반경 22m
- 대표 마을(실외 상점): 현행 인덱스 0 → **각국 Ring1 대표 1곳** 유지 (index 0 = 외곽 안전 마을 — 상점 접근성 유지)
- 주민 수: 10~15 → 일반 6~10 / 촌락 3~5 (총 ~560명, 현행 ~288명의 약 2배 — 고사양 PC 기준 무리 없음, 저사양 장치 없음=사용자 지시)
- 배치/접지/모델/GLB 폴백/국가 틴트/멱등 가드 = VillageBuilder 기존 로직 재사용 — **건물 배치기 무수정**, 좌표 소스만 변경

### 1-6. 무수정 확인 목록 (산포 따라감 — 건드리지 않음)
TerrainGenerator(평탄/절벽 앵커 자동 수집) · IdyllicDecoPlacer(길변 데코) · IdyllicGrassCover/Culler · TerritoryBuilder(성/병사/플래그) · MonsterSpawner(밴드 스폰) · WorldMap/Minimap · FastTravel · WarMarch · GuardManager · VillageNpcSpawner(수만) · NPC 이동/AI

---

## 2. Phase T1 — 영지 산포 (결정론 + TDD)

### Task T1-1: 산포 순수 수학 추출
- **Create**: `Assets/Scripts/Core/Utils/TerritoryScatterMath.cs` (static, 순수 — EditMode 검증)
- API:
  - `public struct ScatterInput { public string key; public float bandMin, bandMax; public float wedgeBaseDeg; public float wedgeMarginDeg; }`
  - `public static bool TryScatter(ScatterInput input, System.Random rng, List<Vector2> taken, float minSpacing, Vector2 avoidPoint, float avoidRadius, int maxTries, out Vector2 result)`
  - 폴백: `band 중간 + 기저각` (200회 초과 시)
- **Step 1 실패 테스트** `Assets/Scripts/Tests/EditMode/TerritoryScatterMathTests.cs`:
  - 밴드 클램프 (결과 거리 ∈ [bandMin, bandMax])
  - 쐐기 마진 (각도 ∈ [base−45+margin, base+45−margin])
  - 최소 간격 (taken 60m 지점 주변 배제)
  - 회피점 (스폰 100m 배제)
  - 결정론 (동일 시드 = 동일 결과)
  - maxTries 초과 폴백 동작
- **Step 2 RED 확인** → **Step 3 구현** → **Step 4 GREEN**
- 컴파일 게이트: 배치컴파일 CS=0

### Task T1-2: TerritoryDatabase 산포 적용
- **Modify**: `Assets/Scripts/Core/Data/TerritoryDatabase.cs` `GenerateAllDefinitions` L226~252
- 변경: 링 거리 원 1개 (ringDistance × radiusScale) → **밴드 산포**
  - 밴드 테이블: `GetRingBand(difficulty)` 신설 (Ring4 50~300 / Ring3 300~650 / Ring2 650~1100 / Ring1 1100~1550)
  - `TerritoryScatterMath.TryScatter` 호출 (순서: Ring4→Ring3→Ring2→Ring1 — 안쪽부터 확정, 밴드 단조 보장)
  - **difficulty/이름/영주/병사수 생성부 무수정**
  - taken 리스트 = 확정 좌표 누적 + Dracula(1350@150°) 사전 시드 + 스폰 회피점
- ⚠ Empire(0,0)/Dracula AddDefinition 블록 **무수정**
- **Step 1 실패 테스트** `Assets/Scripts/Tests/EditMode/TerritoryScatterPlacementTests.cs`:
  - 82영지 전수: 밴드 소속 단조 (Ring4 max ≤ Ring3 min 간격 보장 — 링별 거리 구간 중첩 없음)
  - 전역 최소 간격 120m (전 쌍 O(n²) — n=82 충분)
  - 쐐기 소속 (각 영지 angle ∈ 자기 국가 쐐기 — NationTerrainController.GetNationFromPosition 좌표 판정과 **교차 검증**: def.worldPosition의 GetNationFromPosition == def.nation)
  - 스폰 ≥100m
  - 결정론: 두 번 생성 = 동일 좌표 (layoutHash 방식과 동일 검증)
  - Empire=(0,0)·Dracula=1350@150° 불변
- **Step 2 RED → Step 3 구현 → Step 4 GREEN → Step 5 컴파일+풀런 회귀** (baseline 11 — 영지 좌표 의존 테스트 실패 시 실측 후 개별 수리: 예상 없음 — 좌표는 세이브/테스트 어설션 미포함)
- **Step 6 QAPROGRESS 기록**

### Task T1-3: Play 정합 스팟체크 (스포크 대체 전 상태)
- 컴파일+EditMode 통과 상태에서 Play 1회: 영지가 밴드 산포로 흩어졌는지 / 성·병사·플래그가 영지 좌표 따라 이동했는지 / 흙길이 **여전히 구 하드코딩** (영지 안 지나감 — T2 예고 상태) 확인
- 스크린샷 1장 기록 — T2 진행 근거

---

## 3. Phase T2 — 흙길 MST 재계산

### Task T2-1: MST 순수 수학
- **Create**: `Assets/Scripts/Core/Utils/PathNetworkMath.cs`
- API: `public static List<(int a, int b)> BuildMst(IReadOnlyList<Vector2> nodes)` — Kruskal + 인덱스 tie-break (결정론), 반환 = 간선 목록 (n−1개)
- **실패 테스트** `PathNetworkMathTests.cs`:
  - 간선 수 = n−1
  - 연결성 (Union-Find로 전 노드 도달)
  - 사이클 없음 (간선 수로 자명 + 유니온 검증)
  - 결정론 (동일 입력 = 동일 간선 목록)
  - 총 길이 ≤ 기존 스포크+링 방식보다 크지 않음 아님 — MST는 최소라 단정 어려움 → 대신 **전체 길이 합산 상한** 어설션 (n=5 삼각형 등 소형 케이스에서 최적해 일치)
- RED → GREEN → 컴파일

### Task T2-2: BuildDirtPathSegments 교체
- **Modify**: `Assets/Scripts/Systems/NationTerrainController.cs` L601~656
- 변경:
  - 노드 수집: 허브(0,0) + 스폰 수선발 + TerritoryDatabase 전 영지 + VillagePlacementSystem 전 마을
  - ⚠ 순환 참조 주의: NationTerrainController(Systems) → TerritoryDatabase(Core.Data)/VillagePlacementSystem(Systems) — **기존 AppendVillageConnections가 이미 같은 참조** (L667 VillagePlacementSystem.GetAllVillages 호출) → 참조 방향 문제 없음 확인됨
  - MST 간선 → PathSegment 변환 (직선)
  - `AppendVillageConnections` **삭제** (MST 흡수)
  - 세그먼트 수 로그 갱신 (53 → ~163)
- **Step 1 실패 테스트** `DirtPathNetworkTests.cs` (EditMode — PathSegment는 static 캐시라 테스트 접근 가능):
  - 전 세그먼트 그래프 연결성 (허브에서 전 노드 도달 — 노드 좌표 매칭)
  - 모든 영지/마을 좌표가 세그먼트 끝점에 존재 (MST 노드 = 실제 좌표)
  - 결정론 (두 호출 = 동일 배열)
- RED → GREEN → 컴파일 + 풀런
- **Step 2 Play 스포크 시각 확인**: 흙길이 모든 영지/마을을 지나는지 스크린샷 — Idyllic 길변 데코가 새 경로 따라가는지 (PlacePathEdgeDeco 소비 자동)
- QAPROGRESS 기록

---

## 4. Phase T3 — 영지당 마을 (24 → 80)

### Task T3-1: VillagePlacementSystem 확장
- **Modify**: `Assets/Scripts/Systems/VillagePlacementSystem.cs`
- 변경: `VillagesPerNation` 6→20 / `_territoryIndices` {1,4,6,9,11,14} → **1~20 전체** / `VillageInfo`에 `bool isHamlet` 신설 (Ring4 = index 1~5)
- 마을 반경: 일반 40 / 촌락 22 (`GetRadiusFor` — index 기반 결정론)
- **실패 테스트** `VillagePlacementTests.cs`:
  - 마을 80개 (4×20), 결정론
  - 각 마을 중심 ↔ 자기 영지 성 거리 38~46m
  - 마을↔타 영지 중심 ≥ 74m (간격 규약 연동)
  - 촌락 판정 (index 1~5 = isHamlet)
  - 촌락끼리 간격 ≥ 44m (반경 22 ×2) — 영지 간격 120m에서 자동, 검증 어설션
- RED → GREEN → 컴파일

### Task T3-2: VillageBuilder/NpcSpawner 촌락 지원
- **Modify**: `Assets/Scripts/Systems/VillageBuilder.cs`
  - `BuildVillage` 분기: isHamlet → 광장 r4 + 집 2~3 + 우물 (창고/상점 생략), 건물 간격/배치 알고리즘 재사용
  - 대표 마을 판정: `isRepresentative && !isHamlet` (상점은 Ring1 대표에만)
- **Modify**: `Assets/Scripts/Systems/VillageNpcSpawner.cs`
  - 주민 수: 일반 6~10 / 촌락 3~5 (기존 10~15 산식 → 마을 등급별 상수로 교체, 결정론 시드 유지)
- **실패 테스트**: 빌드 로그 파싱은 Play 전용 — EditMode는 `VillageInfo` 등급 판정만. 촌락 건물 수/NPC 수는 Play 게이트 (T3-3)
- 컴파일 + 풀런 + QAPROGRESS

### Task T3-3: Play 종합 확인
- ①마을 80개 가시 (장거리 이동/월드맵 패스트래블로 2~3곳 견학) ②촌락 축소형 ③대표마을 상점 E키 ④주민 상호작용 ⑤길변 데코/마을 평탄화 자동 적용 ⑥프레임레이트 (고사양 PC — 목표: 기존 대비 체감 저하 없음, 수치 기록)
- 스크린샷/영상 기록 — 사용자 리포트 대기

---

## 5. 위험/롤백

- **영지 좌표 변경 = 기존 진행 세이브의 위치 맥락 변화** (세이브 데이터는 무결 — 플레이어 위치/영지 소유는 유지, 지형 위 배치물만 재배치된 영지 좌표에 맞춰 재스폰됨) → 사용자 확인 사항: 기존 세이브 이어서 플레이 시 영지 위치가 새 배치로 보이는 게 맞는지 (예상: 코드 생성이라 자동 — QA에서 확인)
- 롤백: 좌표 산포식만 되돌리면 됨 (단일 커밋 단위 — T1/T2/T3 각각 커밋 분리)
- 계통 위험: MST 세그먼트 163개 → PaintDirtPaths 픽셀 루프 AABB 필터링 성능 (1024² 기준 실측 — T2에서 기록)

## 6. 커밋/게이트 규약

- 각 Task마다: 실패 테스트 → 구현 → GREEN → 배치컴파일 CS=0 → 커밋 (T1/T2/T3 분리)
- 최종: EditMode 풀런 (baseline 11 유지) + Play 기록 → 사용자 확인 대기
- Play/커밋 게이트: T1-3·T2-2 Play·T3-3은 별도 승인 후 (사용자 규약 — "진행"으로 승인 범위 내 구현+QA는 연속 진행, Play는 게이트 유지)
