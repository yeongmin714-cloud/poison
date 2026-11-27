# 104차 QA 최종 보고 — EditMode 전수 확인

## 실행 정보
- Unity: 6000.4.10f1, batchmode, EditMode (quit 제외 규약 준수)
- 결과: Temp/editmode-104final.xml, 로그: Temp/editmode-104final.log
- Exit code: 2 (실패 테스트 존재 정상 — baseline 11건)

## 전수 판정 수치
| 항목 | 수치 |
|------|------|
| total | 1368 |
| passed | 1357 |
| failed | 11 |

- 103차 total 1343 → 104차 예상 ~1368 (+25 신규) → **1368 정확 일치**
- failed = 11 (baseline과 정확히 일치, 그 외 0)

## Baseline 1:1 대조 (11건)
| # | Suite | 테스트 | 상태 |
|---|-------|--------|------|
| 1 | Aero | AeroLocalizedGasPhase4BTests.IsolatedSessionWithoutConfiguredPipeline_LeavesGlobalPipelineSettingsUntouched | baseline 일치 |
| 2 | CraftBench | CraftBenchTests.CookingDatabase_AllRecipes_Loaded | baseline 일치 |
| 3 | Instrument | InstrumentTests.Performance_TryStart_Success_IsPerforming | baseline 일치 |
| 4 | Instrument | InstrumentTests.Performance_EndPerformance_ClearsState_AndCooldownBlocks | baseline 일치 |
| 5 | Instrument | InstrumentTests.Performance_DoubleStart_Blocked_WhilePerforming | baseline 일치 |
| 6 | InvadedLord | InvadedLordEncounterTests.Initialize_StartsNeutralAndRegistersAnUnresolvedLivingLord | baseline 일치 |
| 7 | InvadedLord | InvadedLordEncounterTests.DamageIsIgnoredUntilAttackIsChosen_ThenDefeatDoesNotResolveTerritory | baseline 일치 |
| 8 | MeleeClick | MeleeClickOwnershipTests.EachAcceptedAttack_IncrementsStreakUpToExpectedCount(2,2) | baseline 일치 |
| 9 | MeleeClick | MeleeClickOwnershipTests.EachAcceptedAttack_IncrementsStreakUpToExpectedCount(3,3) | baseline 일치 |
| 10 | RecipeCatalog | RecipeCatalogTests.FindRecipe_UnknownCombo_ReturnsNull | baseline 일치 |
| 11 | ThemeUss | ThemeUssSanityTests.ThemeUss_HasBakedBackgroundPngs | baseline 일치 |

## 신규 회귀 유무
- 신규 회귀: **0건**
- ConstructionHologramTests (15건): 전부 Passed
- DialogueGaze104Tests (10건): 전부 Passed
- 신규 총 25건 → 103차 1343 + 25 = 1368 수치 검증 일치

## 최종 판정
**PASS** — failed 11건 모두 baseline 1:1 일치, 신규 회귀 0, 신규 대상 25건 전부 PASS
