# 148차 계획 — Test_15 경량화(주민 제거·농경만) + 자원/낚시 Test_20 이관

## §0 목표와 근거
- 사용자 지시(2026-11-03): "최대한 가볍게 테스트씬을 나누자 — NPC=상점NPC만 두고 설계된 마을 모습이 보이게. 나무와 호수·돌 등은 다른 씬에서 테스트하고 농경시스템만 배치."
- 확인 답변(같은 세션): ①음식점/마약경영 = **유지** ②약초 3종 = **유지** → "유지 하고 진행해줘" 승인.
- 실측 근거(Editor-prev.log [147차][P0], 1차 Play): Awake 합계 23.3s = 주민스폰 15,545ms(66%) / 상점NPC 4,903 / UTK 1,387 / 마을플롯 388 / 지면패치 17 / 음식점리셋 72 / SetupGround 31 / 생활씬 1.
- Play 진입 1차 고정비 14.0s(강제 동기 리컴파일+리프레시)는 스크립트 변경 후 1회 한정 — 본 계획 범위 외.

## §1 변경 설계 (파일 3개)

### T1. Assets/Scripts/Systems/TestTerritoryCombatSetup.cs — Test_15 경량화
1. **주민스폰 제거**: `P0Time("주민스폰", () => VillageNpcSpawner.SpawnForVillage(best.nation, best.index));` 라인 삭제 + `[148차]` 마커 주석("주민 10명 스폰 제거 — Awake 15.5s 절감, 자원/낚시 검증은 Test_20 분리").
   - VillageNpcSpawner 클래스 자체는 **무수정**(메인씬 경로 유지 — Test15VillageDrugTests 시그니처 계약 보존).
2. **클러스터 분리**:
   - `SetupLivelihoodCompactCluster` → **밭 2x2 + 약초 3만 유지**(농경). 호수(Livelihood_Pond)/나무 3/돌 3 블록 제거.
   - 신설 `SetupLivelihoodNatureCluster(Vector3 anchor, Vector3 villageCenter)` — 호수/나무/돌 블록을 **오프셋 수식 그대로 이식**(142차 C2 결정론 보존, Random 0건). 메서드 요약 주석에 [148차] 출처 표기.
   - 블록 내 로그 접두어 `[Test15Livelihood]`는 이동 후에도 유지(문자열 계약 — 검색 가능성 우선, 별도 계약 없음 확인).
3. **씬 분기 신설**: `_isLivelihoodResourcesScene = scene.name.Contains("LivelihoodResources")` (Awake 초반 isLivelihoodEarly 산정 지점과 동일 패턴).
   - `if (_isLivelihoodScene) SetupTest15VillageDrugEconomy();` → `if (_isLivelihoodScene && !_isLivelihoodResourcesScene) ...` [148차] 마커.
   - `if (_isLivelihoodScene) P0Time("생활씬", SetupLivelihoodTestScene);` → `if (_isLivelihoodResourcesScene) P0Time("생활씬", SetupLivelihoodResourcesScene);` — 생활씬 스테이지는 Resources 전용.
   - 신설 `SetupLivelihoodResourcesScene()`: `SetupFishingArea()`(낚시 인프라 — 물 의존이므로 호수와 함께 이관) + `SetupLivelihoodNatureCluster(Vector3.zero, Vector3.forward * 10f)` — 기존 폴백 호출과 동일 지오메트리(dOut=남 고정 계약).
4. 폴백(마을 미발견)의 `SetupLivelihoodCompactCluster(Vector3.zero, Vector3.forward * 10f)` 호출은 그대로(메서드가 farm-only가 되어 자동 축소).

### T2. Assets/Scripts/Tests/EditMode/Test15VillageDrugTests.cs — 신설 1건
- `Test15_VillagerSpawn_Removed_ShopOnly_148`: setup 소스 문자열 계약 —
  - `Does.Not.Contain("P0Time(\"주민스폰\"")` + `Does.Not.Contain("VillageNpcSpawner.SpawnForVillage(")` (TestTerritoryCombatSetup.cs 한정)
  - `Does.Contain("SpawnVillageNpc(\"test15_shopkeeper\"")` + `Does.Contain("P0Time(\"상점NPC\"")` (상점NPC 유지 계약)
  - 기존 7건 무수정.

### T3. Assets/Scripts/Tests/EditMode/Test20ResourceSceneTests.cs — 신설 1건
- `Test20_NatureCluster_SourceContract_148`: setup 소스 문자열 계약 —
  - `Does.Contain("_isLivelihoodResourcesScene")` (분기 게이트)
  - `Does.Contain("SetupLivelihoodNatureCluster")` + `Does.Contain("Livelihood_Pond")` (자원 클러스터 존재)
  - `Does.Contain("_isLivelihoodScene && !_isLivelihoodResourcesScene")` (마을/마약 경로의 Resources 제외)
  - 기존 테스트 무수정.

## §2 물뿌리개 귀결 (의존성 실측 — 코드 확인 완료)
- WateringCanSystem: `Refill()` = 호수 물가 물담기(2채널)로만 충전, 세션 시작 `_water = MaxWater(20)`.
- 농경 1부지 3일 성장 = 물 ~3회 → 초기 20회로 검증 충분. **Test_15 무호수 영향 없음** — 리필(물담기) 검증은 Test_20 호수에서 수행.

## §3 불변 계약 (수정 금지)
- TerrainGenerator 평지 게이트(Contains("Livelihood") — Test_20 자동 포함) / 마을플롯 V1 BuildPlotFlat / 상점NPC(test15_shopkeeper, 4.9s — 사용자 지시 유지) / 음식점 시드·리셋·틱·Y키 / 약초 3 / 밭 2x2 / 142차 C2 오프셋 수식 / P0Time 키 "SetupGround"·"UTK"·"지면패치"·"마을플롯"·"상점NPC"·"음식점리셋".
- Village5DesignCatalogTests.Test15_BasicTerrain_Contract_147 = 주민스폰 미참조(실측) → 무수정 통과.
- Test15VillageDrugTests 기존 7건 = SpawnForVillage 시그니처(VillageNpcSpawner.cs)만 참조 → 무수정 통과.

## §4 예상 로딩
- Test_15: Awake 23.3s → **약 7.5s** (상점NPC 4.9 + UTK 1.4 + 마을플롯 0.4 + farm 클러스터 + 기타).
- Test_20: **약 2s** (UTK 1.4 + 자원 프리미티브 + 낚시 인프라).

## §5 위임·검증 (AGENT_DELEGATION_POLICY 준수)
- 구현 서브 1회: 위 T1~T3. 규칙 — patch 우선, 전면 재작성 금지, read_file total_lines 대조 필수(147차 절단 사고 예방), [148차] 마커 부착.
- 독립 QA(별도 서브/부모):
  1. 컴파일 error CS 0 (run_tests.sh 또는 compile_test.sh — qa_148_compile.log)
  2. 전수 EditMode — 신규 +2 반영 예상 **1823/1813/10 = baseline 10건 1:1, 신규 실패 0** (qa_148_editmode-results.xml)
  3. 집중: Test15VillageDrugTests 8/8 · Test20ResourceSceneTests 2/2 · Village5DesignCatalogTests 34/34
  4. 범위 감사: 당일 Assets 수정 = 화이트리스트 3파일(TestTerritoryCombatSetup/Test15VillageDrugTests/Test20ResourceSceneTests) mtime 실측
- Play·커밋: 별도 사용자 승인 게이트.

## §6 리스크
- 상점NPC 4.9s가 경량화 후 최대 항목(전체의 ~60%) — 본 라운드 착수 금지(사용자 지시 유지), L1 스테이징 후보로 기록.
- Test_20이 기존엔 Test_15와 동일 전체 경로(마을+마약경영 포함)였음 → 본 변경으로 자원 전용 경량 씬으로 재정의(사용자 "씬을 나누자" 지시의 직접 귀결).
