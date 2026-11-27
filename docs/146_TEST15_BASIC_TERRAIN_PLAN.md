# 146차 계획 — Test_15 기본 지형 복귀 (Idyllic 플롯/쇼케이스 제거 — 로딩 경량화)

작성: 2026-11-03 (감독자). 승인: 사용자 지시 "15씬은 idylic 말고 기본 지형으로 바꿔줘, 메인씬은 idylic 그대로 유지" (2026-11-03 00:3x KST, CLI).

## §0 배경 — 원인 확정 (읽기 전용 감사 완료)

- 사용자 실측: Test_15_Livelihood 씬 로딩 과다. 원인 = [145차 P2b/P3-TOUR] 블록 —
  ① `Village5DesignRenderer.BuildPlot("H1", best.center, "_Game")` — H1 호수반도 고산 미니 지형(Idyllic 텍스처 지면+호수+목교+건물)
  ② `Village5DesignRenderer.BuildAll(showcaseOrigin)` — **쇼케이스 10마을 전체 빌드**(표준 5 + 고산 4, Idyllic GLB 46종 건물 + 자연 산포(나무/덤불/잔디/꽃 수백 개) + 지면 텍스처 패치)
- 위치: `Assets/Scripts/Systems/TestTerritoryCombatSetup.cs` → `SetupTest15VillageDrugEconomy()` 내 L156~202.
- **메인씬 무영향 근거**: `Village5DesignRenderer.` 호출점 전수 = Test_15 단일(프로젝트 유일 — L165/L169). 메인씬 Idyllic은 기존 경로(NationTerrainController/TerrainTextureApplier/IdyllicDecoPlacer/WaterMaterialUpgrader)이며 Village5 미사용. → Test_15 블록 제거만으로 요구 충족, Village5 코드/카탈로그는 보존(향후 쇼케이스 재활용 — 삭제 금지).

## §1 변경 스펙 (scope files 화이트리스트 — 이 2파일만)

### 1) `Assets/Scripts/Systems/TestTerritoryCombatSetup.cs`

- `SetupTest15VillageDrugEconomy()` 내 **[145차 P2b] H1 분기 블록 전체 제거**:
  - 대상: L156~202 = `[145차 P2b] 고산 미니 지형 분기` 주석(3줄) + `Village5Design h1Design = null;` ~ `if (h1Design != null && h1Design.Profile != null) { ... return; }` 전체.
  - 제거 문자열(소멸 확정 대상): `Village5DesignRenderer.BuildPlot("H1", best.center, "_Game")`, `Village5DesignRenderer.BuildAll(showcaseOrigin)`, `SetupLivelihoodCompactCluster(clusterAnchor, plateauCenterW)`, `[145차 P2b] 생산 마을 건물 스킵 가드`, `h1Design`, `showcaseOrigin`, `clusterAnchor`, `plateauCenterW`.
- 제거 자리에 마커 주석 1줄:
  `// [146차] Test_15 기본 지형 복귀 — Idyllic 플롯/쇼케이스 10마을 빌드 제거(로딩 경량화, 사용자 지시). 메인씬 Idyllic 무영향(Village5 호출점은 본 파일이 유일). [145차 P2b] H1 분기는 Village5DesignRenderer에 보존.`
- 잔여 흐름(기존 코드 그대로, 수정 없이 자연 복귀): `BuildVillageGroundPatch(best.center, 140);` → 플레이어 마을 가장자리 이동([86차]) → `SetupLivelihoodCompactCluster(플레이어 착지지, best.center)`([142차 C2] 계약) → `VillageBuilder.BuildSingleVillage(best)` + `VillageNpcSpawner.SpawnForVillage` + 상점 NPC → 음식점/재고/판매틱(공통).
- L203 폴백 주석은 "폴백" 서술 제거 후 [146차] 기본 경로 서술로 갱신(동작 변경 없음 — 주석만).

### 2) `Assets/Scripts/Tests/EditMode/Village5DesignCatalogTests.cs`

- `Test15_HighlandBranch_Contract_145P2b`(L336~346) → **`Test15_BasicTerrain_Contract_146`** 재작성(테스트 메서드 1:1 유지 — 전수 카운트 불변):
  - a) `src` Contains `"BuildVillageGroundPatch(best.center, 140);"` — 기본 지형 경로 계약.
  - b) `src` Contains `"[146차] Test_15 기본 지형 복귀"` — 마커 계약.
  - c) `src.Contains("Village5DesignRenderer.BuildPlot(\"H1\"", )` Is.False — Idyllic 플롯 호출 소멸.
  - d) `src.Contains("BuildAll(showcaseOrigin")` Is.False — 쇼케이스 빌드 소멸.
  - e) 기존 어서션 3건(`BuildPlot("H1"...`, `SetupLivelihoodCompactCluster(clusterAnchor, plateauCenterW)`, `[145차 P2b] 생산 마을 건물 스킵 가드`) 전부 소멸.
- 그 외 테스트 **무수정**: `Renderer_*` 소스계약은 Village5DesignRenderer.cs 본문 계약(본 변경에서 미개변 — 컴파일 유지), `Test15VillageDrugTests` L88 `BuildVillageGroundPatch(best.center, 140);` 계약은 오히려 강화.

## §2 완료 기준

1. 컴파일 `error CS` 0.
2. 전수 EditMode: 총 테스트 1820 유지, 실패집합 = baseline 11 + 병행WIP 1(H2_Catalog_AndFactory) **1:1 — 신규 실패 0**.
3. 집중 GREEN: `Village5DesignCatalogTests` 전체(재작성 포함) + `Test15VillageDrugTests` 전체.
4. 범위 준수: 위 2파일 외 수정 0 (Village5DesignRenderer.cs / Village5DesignCatalog.cs 무수정).

## §3 증거 요건

- 구현: 수정 파일 경로 목록 + 파일별 diff + 컴파일 증거(로그 경로: `TestOutput/qa_146_compile.log`).
- QA: ①컴파일 ②EditMode ③Play 구분 표기 + 로그/XML 경로(`TestOutput/qa_146_editmode.log`, `TestOutput/editmode-results.xml`) + 실패집합 대조표.
- 실행 래퍼: `bash /mnt/c/Unity/code/compile_test.sh` (compile.log에 error CS grep) / `bash /mnt/c/Unity/code/run_editmode_check.sh` (TestOutput/editmode-results.xml 파싱).

## §4 게이트

- Play(사용자): Test_15 로딩 체감 + 기본 지형/마을 건물+주민/음식점/생활기술 클러스터 확인.
- 커밋: 별도 승인(128~145 누적분과 함께).
