# 123차 계획 — 영지 소유 영주 추적 + 월드맵 호버 영주 툴팁 + 전투로그 폐기

제정: 2026-10-24 (123차) / 상태: 구현 전

---

## 발단

사용자 요청 3건:
1. 전투에서 영주 A가 영주 B의 영지를 점령(탈환)하면 지도에 승리한 영주가 반영되도록.
2. 월드맵에서 각 영지에 커서를 대면 그 영지 소속 영주의 이름이 나오도록.
3. 전투로그 시스템은 폐기.

**clarify 확정 (2026-10-24)**:
- 영지 표시 이름 = **지역명 유지**("동쪽 평화로운 초원" 등 — 리네임 폐기), 영주 반영은 **호버 툴팁**으로. (사용자 직접 선택)
- 전투로그 범위 무응답 → **기존검증 최소설계 진행**(60차 선례): UI 창 제거 + `CombatLog` no-op 스텁. 호출부 12파일 무수정 — "일단 폐기" 취지(복원 용이) 부합.
- 플레이어 본인이 점령한 경우 이름 변경 없음(기본안) — 툴팁에 "플레이어" 표기.

## 실측 근거 (조사 서브에이전트, 2026-10-24)

### A. 소유권 체계
- 소유 상태: `TerritoryState.ownership` — enum `TerritoryOwnership { Unoccupied, PlayerOwned, LordOwned, Contested }` (TerritoryData.cs:151/198).
- **"어느 영주가 소유했는지"는 현재 저장되지 않음** — LordOwned는 무차별 플립. 소유 영주 추적 필드 신설 필요.
- 영주 정보: `TerritoryDefinition.lord` — `LordInfo.lordName` (TerritoryData.cs:62~71, struct 불변). 영주명은 "홍 경"식 "경" 접미사(TerritoryDatabase.cs:624). 영지명은 `territoryName`(L104), 결정론 지역명 생성(L495~539).
- 정식 쓰기 API: `TerritoryDatabase.SetOwnership`(TerritoryDatabase.cs:189/201 — PlayerOwned 전환 `OwnershipChanged`, 상실 `OwnershipLost` 발화).
- 영주↔영주 점령 경로(이번 배선 대상):
  - `AIWarSystem.CompleteWar`(AIWarSystem.cs:510) — AI 전쟁 완료 → LordOwned + 토스트 "⚔️ {공격영지명} → {방어영지명} 점령!"(L531~534). **전쟁에 공격 영지 식별 존재**.
  - `TerritoryWarManager.HandleVictory`(TerritoryWarManager.cs:704) — 국가 간 전쟁 승리 → state.ownership 직접 쓰기(이벤트 미발화) + `_currentNationOwners` 갱신(L703).
  - `LordBetrayalSystem.ExecuteBetrayal`(LordBetrayalSystem.cs:144) — 살려준 영주 배신 → 소유 역전(LordOwned).
  - `NewGamePlusSystem`(L208/210) — 뉴게임+ 리셋 → LordOwned/Unoccupied.
- 플레이어 점령 경로(무수정): PoisonTakeoverSystem/LordSurrenderSystem/AssassinationCutscene/TerritoryWarManager.RegisterPlayerTerritory → PlayerOwned.
- 세이브: `TerritorySaveData`(SaveData.cs:103~120) — ownership("ownerNation" 필드에 enum 문자열)/isConquered/territoryGold(v4+)/lordSpared/tributeWeeksPaid/betrayalCount/loyaltyToPlayer/lastPreferredFeedDay(v5+). **영지명·영주명 미저장**. 복원은 ApplyTerritoryData(SaveManager.cs:591~632), `OwnershipRestoreMode=true`로 이벤트 억제.

### B. 월드맵
- 마커: `WorldMapWindowUTK.RebuildMarkers`(WorldMapWindowUTK.cs:514~541) — 82영지 `BuildTerritoryMarker`(L552~566), 라벨 = 영지명(`TerritoryMarker` 중첩 클래스 L879~970, Empire "👑"/Dracula "🧛" 접두 L916~918). 클릭만 있음(ClickEvent L564, Debug.Log만).
- **호버 이벤트 없음**(PointerEnter/Leave 0). 재사용 패턴: `UTKTooltip`(UTKControls.cs:162~204, 커서 추적 static 툴팁 — **현재 호출부 0개, 미사용**) + PointerEnter/Leave 스타일 토글 표준(InventoryWindowUTK.cs:342~343).
- 레거시 참조 구현: MapWindow.cs `BuildHoverTooltipText`(L1182) — 소유주 라인에 `def.lord.lordName` 포함(L1211~1213). `TerritoryTooltipContent` 순수 포맷터 + 테스트 선례.
- 월드맵 관련 기존 테스트: WorldMapSceneWiringTests 4, MapWindowColorContractTests 4, TerritoryTooltipContentTests 11. 마커 호버 테스트: 없음.

### C. 전투로그
- UI: `Assets/Scripts/UI/Toolkit/CombatLogUTK.cs`(487행) — L키 토글+ESC(Updater L465~485 자가 부트 RuntimeInitializeOnLoad). **외부 참조 0**(자기 파일뿐 — grep 실측).
- 내부: `Assets/Scripts/Systems/CombatLog.cs`(69행) — static `AddEntry/GetRecentEntries/GetLastEntry/Clear/Count`, enum LogType{Normal,Damage,Heal,Kill,Warning}, struct CombatLogEntry, 링버퍼 100.
- 호출부: 12파일/22곳(AfterlifeAugmentSystem/AfterlifeEnemy/AfterlifeSurvivalSystem/AnimalAI/GuardPlaceholder/LordBetrayalSystem/PlayerCombat/RestaurantSystem/SkeletonGuardPlaceholder/SparedLordTributeSystem/WandererDriver/CombatLogUTK).
- 테스트/세이브/GAME_DATA.md 의존: **전부 0건**.

## 계획

### Phase 1 — 영주 소유 추적 (Impl1)
범위 파일: TerritoryData.cs / TerritoryDatabase.cs / AIWarSystem.cs / TerritoryWarManager.cs / LordBetrayalSystem.cs / NewGamePlusSystem.cs / SaveData.cs / SaveManager.cs + 신규 테스트.
- P1-1 `TerritoryState`에 `ownerLordName`(string) 신설 — 초기값 = `def.lord.lordName`(결정론 매핑).
- P1-2 `TerritoryDatabase`에 `SetOwnerLord(territoryId, lordName)` 공개 API + 조회 헬퍼(있다면 GetDefinition 경유로 충분).
- P1-3 점령 배선(영주↔영주 경로만):
  - `AIWarSystem.CompleteWar` — 정복 영지 `SetOwnerLord(공격 영지의 def.lord.lordName)`.
  - `TerritoryWarManager.HandleVictory` — 공격 국가 대표 영주를 결정론 규칙으로 산정(전쟁 데이터에 공격 영지 식별이 있으면 그 영주, 없으면 공격 국가 영지 중 인덱스 0의 영주 — 구현 서브가 코드 확인 후 규칙 확정·주석 문서화).
  - `LordBetrayalSystem.ExecuteBetrayal` — 해당 영지 원래 영주(`def.lord.lordName`)로 복귀.
  - `NewGamePlusSystem` 리셋 — 전 영지 ownerLordName = 원래 영주(결정론 재생성 값).
  - 플레이어 점령 경로 4종 **무수정** — PlayerOwned는 툴팁 포맷터에서 "플레이어"로 표기(저장 불필요).
- P1-4 세이브: `TerritorySaveData`에 `ownerLordName` 신설(기존 버전 필드 규약 준용 — v6), CollectTerritoryData 저장, ApplyTerritoryData 복원. **구세이브(필드 없음) → 원래 영주 폴백**. OwnershipRestoreMode 경로 동일 적용.
- P1-5 테스트 `TerritoryOwnerLordTests` 신설(EditMode): ①초기 상태 = 결정론 영주 매핑 ②SetOwnerLord 라운드트립 ③구/신 세이브 라운드트립(구세이브 폴백) ④점령 배선 소스 계약 어서션(소스-텍스트 방식 — 프로젝트 표준) ⑤뉴게임+ 리셋 복원.

### Phase 2 — 월드맵 호버 영주 툴팁 (Impl2)
범위 파일: WorldMapWindowUTK.cs (+필요 시 UTKControls.cs) + 신규 테스트.
- P2-1 `TerritoryMarker`에 `PointerEnterEvent/PointerMoveEvent/PointerLeaveEvent` 등록 → `UTKTooltip.Show/Hide`(기존 static 재사용 — 호출부 0, 충돌 없음).
- P2-2 툴팁 내용(최소설계): 영지명 / 국가 / **영주: {ownerLordName}** / 소유 상태(영주·플레이어·무주). PlayerOwned는 "플레이어". 호버 시점 실시간 조회 → 점령·탈환 즉시 반영.
- P2-3 포맷터는 순수 함수로 분리(MapWindow.TerritoryTooltipContent와 이름 충돌 회피 — `WorldMapTerritoryTooltipContent` 등) + 계약 테스트.
- P2-4 마커 라벨(지역명)·클릭·250ms 폴링 기존 계약 무변경.

### Phase 3 — 전투로그 폐기 (Impl2와 병행, 파일 분리)
범위 파일: CombatLogUTK.cs(+.meta 삭제) / CombatLog.cs.
- P3-1 `CombatLogUTK.cs` + `.cs.meta` 삭제 — L키·자가 부트 Updater 소멸. 외부 참조 0 실측이라 컴파일 무영향.
- P3-2 `CombatLog.cs` no-op 스텁: AddEntry=빈 몸체, GetRecentEntries=빈 배열, GetLastEntry=null, Clear/Count 무동작 — **시그니처 전부 유지**(호출부 12파일/22곳 무수정 — 119차 미커밋 WandererDriver.cs:269, GuardPlaceholder.cs:589~598 보호). LogType/CombatLogEntry 유지.
- P3-3 복원 경로 주석 명시(git 이력 + 스텁 교체 1파일).

### 셀프 감사 (중복·충돌)
- L키 소멸: L키 다른 용도 조사 결과 전투로그 전용 — 충돌 없음.
- UTKTooltip: 호출부 0 — 중복 툴팁 없음.
- 포맷터 이름: MapWindow.TerritoryTooltipContent와 별개 명명 — 중복 없음.
- baseline 11건(CombatLog 미참조)·세이브 구조·GAME_DATA.md 영향 없음.
- 더티 워킹트리(105~122 미커밋) 보호: 전부 수정(추가)만, revert/stash 없음.

## 검증

- 구현 서브 각 단계 종료 시 컴파일 확인(배치 `-batchmode -nographics`, error CS=0).
- **QA(별도 서브)**: 배치컴파일 error CS=0 / EditMode 전수 — 기존 baseline 11건 1:1, 비baseline 0 / 신규 테스트 전부 Passed / 인접 테스트(WorldMapSceneWiring 4, MapWindowColorContract 4, TerritoryTooltipContent 11, WandererDriver 5, WandererConversion 6) GREEN 증거.
- **Play 게이트(별도 승인)**: ①M키 월드맵 → 마커 호버 시 영주 툴팁 가시 ②AI 전쟁 점령 발생 → 해당 영지 호버 영주 = 승리 영주 ③L키 무반응(전투로그 소멸) ④세이브-로드 후 영주 매핑 유지.
- 커밋: 별도 승인(105~123차 누적 상태 유지).

## 승인 게이트
- Play 검증 실행 전 사용자 승인 / 커밋 전 사용자 승인.
