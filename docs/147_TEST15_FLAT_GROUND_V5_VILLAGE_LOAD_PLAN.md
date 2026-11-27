# 147차 계획 — Test_15 평지 전환 + 설계 마을(신규 GLB) 1종 + 로딩 단축 (계획 — 승인 대기)

작성: 2026-11-03 (감독자). 트리거: 사용자 지시 — "테스트15에 idyllic 아닌 기본 평지, 마을은 미리 설계해둔 마을 중 하나, glb도 새로운 마을 glb로 교체, 여전히 로딩 긴데 원인 재분석 후 수정 계획".

## §0 원인 재분석 (증거 기반 — 2026-11-03 01:3x 사용자 Play 세션 Editor.log, 47,734라인)

**선확인**: 해당 세션에서 [146차] 기본 경로 실제 동작 확인([142차 C3] 컴팩트 클러스터 로그·마약경영 기본 흐름 존재, [145차 P2b]/[P3-TOUR] H1·쇼케이스 로그 0건) — 즉 146차 수정은 반영됐고 그 위에서 "여전히 느림"이 관측됨.

| 구분 | 원인 | 증거 | 대략 규모 |
|---|---|---|---|
| **A. Play 진입 고정비** | 스크립트 변경 후 첫 Play = 강제 동기 리컴파일 + 에셋 리프레시 | L664-665 "Reloading assemblies for play mode / after forced synchronous recompile" → L756 BeginReloadAssembly **1.069s** → L778 Asset Pipeline Refresh **Total 9.369s** (StopAssetImportingV2(ForceSynchronousImport \| ForceDomainReload)) | **~10.5s** |
| **B. 에디터 오픈 고정비** | 146차 스크립트 변경분 임포트 | L276/299 Tundra 0.85s+5.04s(1,316 evaluated) → L354 domain reset **9.408s** | ~15s (오픈 시 1회) |
| **C. Awake 체인 자산** | 마을 1 = 건물 6 GLB + **울타리 링 270 인스턴스** + **주민 10**(리깅 FBX + 4096² BC7 알베도+노멀 — 141차 고품질 계약) + 상점 NPC + 무기 GLB 3 + 생활 4채널/컴팩트 클러스터 + UTK 8창 부트·창고/크래프트 시딩 | 지연 로드 16키(모델 캐시 정상 — 키당 1회), [VillageBuilder] buildings=6/segments=270/270, 바인딩 교정 10건 | 미측정(P0 필요) |
| **D. Play 중 예외 스팸(별개 결함)** | WorldMapWindowUTK.TerritoryMarker.RefreshVisual(:2146) NullReferenceException 매 프레임 다발 | 로그 내 **1,692회** — 로그 4.7MB 폭증 + 예외 처리 CPU churn (로딩과 별개, 세션 체감 저하) | 세션 전반 |
| 배제 항목 | 셰이더 컴파일 버스트 없음(shadercompiler 48Cmd — 캐시 웜), TerrainTextureApplier/IdyllicDecoPlacer는 Test_15 미관여(호출점 없음/에디터 전용), 텍스처 재임포트 없음 | — | — |

**결론**: 146차로 쇼케이스(10마을×수십 GLB+수백 산포)는 제거됐고, 잔여 로딩의 **확정 최대항은 A(Play 진입 리컴파일/리프레시 ~10.5s)** — 이것은 씬 내용과 무관한 에디터 고정비라서 "설계 마을을 넣어도/빼도" 첫 Play는 느리다. 2회차 Play(무변경) 기준으로 재판정해야 C의 실비중이 보인다. C는 울타리 270 + 주민 10(고해상도 텍스처)이 유력. D는 별도 수리 대상.

## §1 설계 변경 (사용자 요구 반영)

### 1) Test_15 = 기본 평지
- 레버: `SetupGround`에 Test_15 게이트 — 표면 높이를 평면(고정 y)으로 통일(수식 굴곡 제거).
- 계약 유지: `GetHeightAt`/`ClampToGroundByHeight`/`SurfaceY` 단일소스 — "평지 상수"를 높이 함수에 Test_15 전용 분기로 반영(오브젝트 접지·블롭섀도우 정합 무손상).
- `BuildVillageGroundPatch(±140m)`는 평지와 중복 → 평지 전환 시 생략/통합(구현 단계 확정 — 지형 단일 소스화).

### 2) 마을 = 미리 설계해둔 마을 중 1종 + 신규 GLB
- `Village5DesignRenderer`에 **평지 플롯 모드 신설**: `BuildPlotFlat(designId, center, suffix)`.
  - **재사용**: `PlaceEntries(설계 배치표 전수)` — 신규 마을 GLB(46종 카탈로그) 배치 + 기초 링/디퍼링/블롭 섀도우(공통 경로).
  - **스킵(평지 모드)**: 지면 메시(BuildTerrainMeshCore)/Idyllic 잔디 머티리얼/자연 산포(나무·덤불)/잔디·꽃 산포/텍스처 패치. 고산 설계는 프로필 지형(분지/고원/목교)이 전제라 평지 모드 대상에서 제외 — **표준 설계만**.
  - **흙길(진입로+광장 링)**: 캐시 메시 저가 — 유지(기본)/생략 토글.
- 기존 `VillageBuilder.BuildSingleVillage`(구형 hut GLB+울타리 270) → 설계 플롯으로 대체 → **울타리 270 인스턴스 자동 소멸**(오브젝트 수 최대 절감).
- 기능 유지: 주민 스폰/상점 NPC/음식점/마약 경영은 VillagePlacement `best`(nation/index) 키 — 위치 기반이라 무영향. 단, `BuildingPlaceholder` 의존 기능(출입문/상호작용) 유무를 구현 전 확인(있으면 설계 건물에 앵커 재배선).
- 기본 설계 id: **표준 1종(S1) 기본값 — 사용자 지명 가능** (const 1줄로 교체 가능하게).

### 3) 로딩 단축 레버 (P0 측정 선행 — 확정 전 수리 금지)
- **P0 계측**: Awake 하위 단계별 Stopwatch 로그(SetupGround/UTK/4채널/마을/주민/음식점 각 ms) + **첫 Play vs 2회차 Play 구분 실측** — A(고정비)와 C(자산)의 실비중 분리. (1급 단서: 단계별 ms 표.)
- **L1 주민 스폰 스테이징**: Awake 동기 10연발 → 셋업 완료 후 코루틴 순차 스폰(프레임 분산) — 체감 로딩 단축, 텍스처 품질 계약 무손상.
- **L2 울타리 270 소멸**: §1-2 부수 효과(확정).
- **L3 UTK 시딩 경량화**: P0에서 유의미(수백 ms+)로 확인될 때만.
- **L4 WorldMap NRE 수리**: 별도 결함 — 폴링 게이트 null 가드 수리(소규모). 147차에 포함할지 분리 승인.
- **L5 Enter Play Mode Options(domain reload skip)**: 에디터 설정 변경 — 프로젝트 코드 불변, 사용자 별도 승인 대상으로만 제안.
- **텍스처 계약**: 141차 "무조건 고품질" 유지 — **다운스케일/압축 절충 제안 금지**. 모든 레버는 품질 무손상 구조적 조치만.

## §3 위임/검증 (정책 준수)
- 구현 서브 scope(화이트리스트): `TestTerritoryCombatSetup.cs`(평지 게이트+설계 플롯 전환+계측), `Village5DesignRenderer.cs`(BuildPlotFlat 추가 — 기존 경로 무수정), `Village5DesignCatalogTests.cs`/`Test15VillageDrugTests.cs`(계약 갱신 — 테스트 수 1:1).
- QA 서브: 컴파일 error CS 0 + 전수 EditMode baseline 1:1 + 집중(Village5/Test15 계약) + P0 계측 로그 증거.
- Play(사용자): 로딩 체감(1차/2차 Play 구분) + 평지 + 설계 마을 GLB + 주민/음식점 동작.

## §4 게이트
- **본 계획 승인 대기** → 구현 위임 → QA 위임 → Play 승인(사용자) → 커밋 승인(사용자).
- 사용자 결정 대기: ①기본 설계 id(S1? 지명 환영) ②흙길 유지/생략 ③주민 수 유지(10) or 축소(예: 4 — 승인 필요) ④WorldMap NRE 수리 동시 진행 여부 ⑤Enter Play Mode 옵션 적용 여부(에디터 설정).


## §5 승인/결정 확정 (2026-11-03 — 사용자 "진행")
- 미응답 항목 = 최소설계 기본값 적용: ①기본 설계 id = **V1(강변 자급자족 마을)** — const 1줄로 교체 가능하게 노출 ②흙길 = **유지**(캐시 메시 저가) ③주민 수 = **유지(10)** ④WorldMap NRE 수리 = **포함** ⑤Enter Play Mode 옵션 = **미적용**(에디터 설정 — 사용자 별도 실행 대상, 문서화만).
- L1 주민 스폰 스테이징/L3 UTK 경량화 = **P0 측정 후 판정**(확정 전 수리 금지 — 이번 라운드 미구현, 다음 판정 대상).

## §6 구현 스펙 세부 (위임용 — 감독자 확정)

### scope files 화이트리스트
1. `Assets/Scripts/Systems/TerrainGenerator.cs` — `GetHeightAt`에 Test_15(Livelihood) 평지 게이트: 해당 씬에서 **상수 높이 반환**(단일소스 — SurfaceY/ClampToGround/블롭 전파 정합). [147차] 마커.
2. `Assets/Scripts/Systems/Village5DesignRenderer.cs` — `BuildPlotFlat(designId, center, suffix)` **신규 추가만**(기존 경로 무수정): 평지 상수 샘플러 + `PlaceEntries`(GLB 배치/기초링/디퍼링/블롭) + `BuildStandardDirtPaths`(흙길 유지). 스킵: BuildTerrainCore 지면 메시/Idyllic 잔지 머티리얼/자산 산포/잔디·꽃 산포/텍스처 패치. Play 가드/루트 중복 가드/플롯 명명 기존 계약 준수.
3. `Assets/Scripts/Systems/TestTerritoryCombatSetup.cs` — ①`BuildSingleVillage` 호출 → `BuildPlotFlat("V1", best.center, "_Game")` 교체([147차] 마커 — 주민/상점NPC/음식점/마약경영 키는 best 유지) ②`BuildVillageGroundPatch` 생략 판정: SetupGround 지형이 마을 중심(-446,-85) 커버 확인 후 — 커버 부족 시 패치 유지(평지 상수라 중복 무해, 커버 충분 시 제거+마커) ③**P0 계측**: Awake 주 단계 Stopwatch ms 로그(SetupGround/UTK/생활4채널/마을플롯/주민/음식점 — 1줄 요약 로그).
4. `Assets/Scripts/UI/Toolkit/WorldMapWindowUTK.cs` — `TerritoryMarker.RefreshVisual`(:2146) NRE 최소 수리: 미초기화 UI 요소 참조 null 가드(동작 무변경 — 예외 소멸만). 원인 우선 특정(어느 필드가 null인지 로그 1회) 후 가드.
5. 계약 테스트: `Village5DesignCatalogTests.cs`(BuildPlotFlat 소스계약 신설 + Test15_BasicTerrain_Contract_146 → 147 갱신: BuildVillageGroundPatch 계약 제거/BuildPlotFlat 계약 신설 — 메서드 수 1:1), `Test15VillageDrugTests.cs`(BuildVillageGroundPatch 계약 → 평지+플랫 계약 갱신).

### 완료 기준/증거
- 컴파일 error CS 0(`TestOutput/qa_147_compile.log`).
- 전수 EditMode: baseline 1:1(신규 실패 0 — qa_146 실패 10건 집합 준수, MeleeClick 2건 재현 여부 관찰만).
- 집중 GREEN: Village5DesignCatalogTests + Test15VillageDrugTests 전체.
- 범위 준수: 위 5파일 외 수정 0. VillageNpcSpawner.cs **수정 금지**(L1 미룸 — 스테이징 미구현).


## §7 구현 진행상황 + 서브 확정 사실 (1차 위임 — max_iterations 부분 완료, 증거 기반 이어받기)

### 완료됨 (워킹트리 반영 — 재확인 불필요, 중복 작업 금지)
- §6-1 `TerrainGenerator.cs` ✅: `GetHeightAt` 선두 `[147차]` 평지 게이트 — `IsTest15FlatScene()`(씬 핸들 캐시) + `public const float Test15FlatHeight = 0f` (표면 y = GROUND_BASE(1)). 게이트 = scene.name.Contains("Livelihood").
- §6-2 `Village5DesignRenderer.cs` ✅: `BuildPlotFlat(designId, center, suffix)` 신규 — Play 가드/루트 중복 가드/플롯명명 `Village5_{id}{suffix}` 동일 계약, 평지 상수 샘플러 `(x,z) => GROUND_BASE + TerrainGenerator.Test15FlatHeight`, BuildStandardDirtPaths(흙길 유지) + PlaceEntries 재사용, 지면 메시/산포/텍스처 패치 스킵, 고산 설계 거부 가드.

### 1차 서브가 확정한 사실 (그대로 채용)
1. **WorldMap NRE 원인 확정**: `TerritoryMarker.RefreshVisual:2146` = `SetLabelStroke(_labelChip, …)` — **`_labelChip` 필드(L1992)는 생성자가 로컬 `labelChip`(L2046)만 만들고 미대입 → 항상 null**. PollTick(250ms) 예외로 폴링 전체가 매 틱 중단. 수치: null 시 **1회 경고 로그 후 SetLabelStroke 스킵**(동작 무변경). `_labelChip = labelChip` 대입 금지(동작 변경됨).
2. **BuildVillageGroundPatch = 유지 판정**: SetupGround는 원점 ±60m만 커버, 마을 중심 (-446,-85)은 ~454m 이격 → 미커버. 평지 게이트로 패치 정점 전부 동일 y=1(중복 무해) — **호출 유지 + 판정 마커 주석만**.
3. BuildingPlaceholder 소비처 = 성/영지 경로뿐(LordSurrender/TerritoryBuilder/TerritoryCapture) — Test_15 무관, 재배선 불필요.
4. SpawnForVillage는 VillagePlacement 결정론 좌표만 사용 — BuildSingleVillage 제거 무영향.
5. 테스트 충돌 주의: Test15VillageDrugTests는 VillageBuilder의 BuildSingleVillage **정의 존재** 어서션(파일 미수정 — 유지됨, 위반 아님). FarmPlotLifecycleTests는 클러스터 `HerbPickup.HerbType.Red, 3` 문자열 — P0 람다 래핑 시 내부 문자열 무손상 필수. 음식점 리셋 블록 문자열 전수 보존(SetRestaurantBuilt/TryRemoveStructure/TryDestroyProp/RebuildFromSave).
6. baseline(qa_146) = 1820/1810/10 — 집중 GREEN = Village5DesignCatalogTests 33 + Test15VillageDrugTests 7.

### 잔여 작업 (2차 위임 지시 — 이것만 수행)
- §6-3 `TestTerritoryCombatSetup.cs`: ①L31 근처 `private const string Test15DefaultVillageDesignId = "V1";` + [147차] 주석 ②Awake의 SetupGround/SetupUITestArena/SetupLivelihoodTestScene을 P0Time 래핑 ③BuildVillageGroundPatch 호출부에 [147차] 판정 마커(유지 근거)+P0Time 래핑 ④`VillageBuilder.BuildSingleVillage(best);` → `Village5DesignRenderer.BuildPlotFlat(Test15DefaultVillageDesignId, best.center, "_Game")` 교체(P0Time "마을플롯") — SpawnForVillage/상점NPC/음식점 리셋은 유지(각각 P0Time 래핑, 내부 문자열 전수 보존) ⑤P0 헬퍼: 동일 키 누적 딕셔너리 + `P0LogSummary()` 1줄 요약 로그 — `System.Diagnostics.Stopwatch` 정규한정(using System.Diagnostics 추가 금지 — Debug 모호성).
- §6-4 `WorldMapWindowUTK.cs`: `_labelChipNullLogged` bool + RefreshVisual 가드 + 1회 원인 로그.
- §6-5 테스트: `Village5DesignCatalogTests.cs` — Test15_BasicTerrain_Contract_146 → **Test15_BasicTerrain_Contract_147** 1:1 갱신(마커/BuildPlotFlat("V1"/const/TerrainGenerator 게이트 마커 포함 + NOT-Contain BuildPlot("H1"·BuildAll(showcaseOrigin·VillageBuilder.BuildSingleVillage(best); — BuildVillageGroundPatch 어서션은 **제거**) + 신규 `Renderer_BuildPlotFlat_FlatPlotApi_147`(+1 → 총 34). `Test15VillageDrugTests.cs` — 지면패치 계약 갱신 1:1(7개 유지, 패치 유지 판정 반영).
- 컴파일: `bash compile_test.sh` → `TestOutput/qa_147_compile.log` + error CS = 0. (EditMode는 QA 서브 몫 — 구현 서브는 EditMode 실행 금지.)


## §8 QA 판정 + 수리 지시 (2차 — 감독자 확정)

### QA 1차 판정: FAIL — 신규 회귀 1건
- 실측 1821/1810/11 vs baseline(qa_146) 10건 부분집합 — **초과분 = `ArenaStagingTerrainTests.T3_SetupStagingDeco_Exists_AndCalledAfterSetupGroundInAwake`**.
- 근원: T3는 `Assets/Scripts/UI/ArenaTestSetup.cs`(미개질, 평문 `SetupGround();`)를 감사하는데, 구현 서브가 T3 needle을 `IndexOf("P0Time(\"SetupGround\", SetupGround);")`로 잘못 승계(그 래핑은 TestTerritoryCombatSetup.cs에만 존재 → IndexOf -1). ArenaStagingTerrainTests.cs는 git 미추적 신규 파일이라 감독자 1차 검토의 git diff에서 누락됐던 것.
- WorldMapSceneWiringTests 갱신은 정확(대상 TestTerritoryCombatSetup 실제 P0Time 래핑 일치 — Passed) → **유지 확정**.
- 나머지 전부 충족: 컴파일 CS 0 / Village5DesignCatalogTests 34/34 / Test15VillageDrugTests 7/7 / 소스계약 전수 / VillageNpcSpawner 무수정.

### 수리 지시 (구현 서브 — 이것만)
- `ArenaStagingTerrainTests.cs` T3: `int idxGround = src.IndexOf("P0Time(\"SetupGround\", SetupGround);");` → **`int idxGround = src.IndexOf("SetupGround();");`** 복원 + 어서션 메시지에서 "[147차] P0Time 래핑" 서술 제거(ArenaTestSetup은 본 라운드 미개질 — 원계약 유지가 맞음). `[147차] 계약 승계` 주석은 "ArenaTestSetup 미개질 — 원계약 유지" 취지로 정정. **그 외 전 줄 무수정**(파일 내 다른 T1/T2/T4 어서션 손대지 말 것).
- 컴파일 1회(`TestOutput/qa_147b_compile.log` — error CS 0).
- EditMode 실행 금지(QA 서브 몫).

### QA 2차 (별도 서브)
- 전수 EditMode: 기대 **1821/1810/10** — 실패집합 = qa_146 10건 부분집합(신규 0) + 집중 Village5DesignCatalogTests 34/34·Test15VillageDrugTests 7/7·ArenaStagingTerrainTests 전체 GREEN. 증거 `TestOutput/qa_147b_*`.


## §9 수리 실행 기록 (3차 — 위임 타임아웃 → 부모 직접 수리, 144차 선례 적용)

- 3차 수리 서브 타임아웃(600s): T3 needle 수정은 적용됐으나 write 페이로드 생성 단계 토큰 손실로 **파일이 80행에서 절단**(T5 배열 도입부에서 종료 — CS1513/CS1002 6건).
- 복구: state.db에서 ①2차 구현의 write 페이로드(완전본 후보 — T4명 오탈자 확인으로 기각) 대신 ②절단 전 디스크 헤드(1~80행 = QA 통과 상태 + needle 수정본) + ③DB 보관본의 T5 꼬리(프리팹 8종 경로/T6 Ground-자식 계약/T7 금지구역 상수/종결 괄호)를 앵커 조립. 검증: 괄호 균형 0, T1~T7 명칭 정합, needle 수정 유지, P0Time needle 부재, 디스크 헤드 47행 일치.
- 결과: ArenaStagingTerrainTests.cs **124행 복원** + `bash compile_test.sh` **error CS 0**(`TestOutput/qa_147b_compile.log` — 3차 복구본).
- 교훈(스킬 반영 예정): read_file limit 부분읽기 → write_file 전면재작성 = 절단 사고 패턴. "부분읽기로 전면재작성 금지" 규칙 필요.
