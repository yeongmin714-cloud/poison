# FLAG_TAKEOVER_PLAN.md — 영주 처단 → 플레이어 깃발 게양 → 크래프트 배정 · 주간 공물 · 영주 배신

작성: 2026-10-15 (실측 기반 계획 — 구현 전, 사용자 승인 대기)
요구: ① 실내전투에서 영주 처단 시 깃발이 플레이어 깃발로 교체 ② 깃발 게양 순간 플레이어 소속 전환 + 크래프트 장소 자동 배정 ③ 풀어주면 영주가 매주 골드 바침 ④ 돌발 이벤트로 영주 배신 → 재탈환 가능

---

## 0. 이해 확인 (사용자 요구 → 시스템 대응표)

| 요구 | 현황 실측 | 대응 Phase |
|---|---|---|
| 영주 처단 시 플레이어 깃발 | 점려 있음(호출부 0) | F1 |
| 게양 순간 소속 전환 | 이미 작동(ExecuteLord/SpareLord→SetOwnership) | F1 검증 |
| 크래프트 장소 자동 배정 | 실내는 이미 작동(7049325a), 야외 없음 | F4 |
| 풀어주면 매주 골드 | 전무(신규) | F2 |
| 영주 배신 돌발 이벤트 | 전무(신규) | F3 |

---

## 1. 기존 인프라 실측 (재구축 금지 — 배선만)

**이미 존재하고 동작하는 것**
- `LordSurrenderSystem.ExecuteLord/SpareLord` — 둘 다 `TerritoryDatabase.SetOwnership(id, PlayerOwned)` 경유 → **OwnershipChanged 발화** → `TerritoryLootSystem` 몰수(금고/창고/병사 포섭) → `IndoorSceneTransition.OnTerritoryOwnershipChanged`가 실내를 플레이어 성으로 즉시 재구성(`SpawnInteriorFixtures` 상점+크래프트하우스 트리거 + `PlayerCastleInteriorBuilder` 작업대[territoryKey 기반 TerritoryCraftingStation]) — **"게양 순간 소속 전환+실내 크래프트 배정"은 커밋 7049325a로 이미 구현됨**
- `InvadedLordEncounter.Resolve(execute)` → ExecuteLord/SpareLord — 실내 영주 처단 흐름 존재
- `TimeManager.OnDayStart` + `CurrentDay` — 하루 경계 (GuardSalarySystem.TryPayDailyWages가 매일 결산하는 선례)
- `AIWarSystem._conqueredNations` — AI 국가 전쟁 소유권 (배신 영주가 AI측으로 복귀할 통로)
- `WarNotificationUTK`/`CombatLog` — 알림 채널

**유령 시스템 (코드는 있는데 배선 0 — 이번에 봉합)**
- ⚠ `TerritoryCaptureSystem.OnTerritoryCaptured(id, newOwner)` — **호출부 0건** (주석만 있음). 깃발 큐브/경계색/파티클이 만들어지지만 아무도 호출하지 않아 점령돼도 깃발 없음.
- ⚠ `FlagManager`+`FlagPoleDisplay` — 국가별 깃대 시스템. `RegisterFlagPole` 호출부 0건 → 영지에 깃대 자체가 스폰되지 않음. `OnTerritoryOwnershipChanged`는 TerritoryWarManager(AI-AI 전쟁)에서만 호출.
- ⚠ `TerritoryBannerSystem.ChangeOwnership` — FlagManager 경유로만 도달 (FlagManager 자체가 유령이라 함께 잠들어 있음).
- ⚠ 플레이어 소유 판별 = `NationType.East` 하드코딩 (FlagManager 96·138·172행 "임시") — 문장 시스템과 미연동.

**"플레이어 깃발"의 정의 (실측 확정)**
- `EmblemManager.CurrentEmblem` = 플레이어 문장 (이름/모양 10종/주색 8종, PlayerFlagUTK에서 커스터마이즈, 기본 Gold).
- `EmblemManager.GetEmblemColor(primaryColor)` → 실제 색. `CreateFlagMaterial()` = 깃발용 머티리얼.
- → 플레이어 깃발 색 = `EmblemManager.GetEmblemColor(CurrentEmblem.primaryColor)` (국가색 아님 — 요구 "플레이어 깃발"에 정합).

**세이브 갭**
- `TerritorySaveData` = territoryId/ownerNation/isConquered/territoryGold — **lordSpared·tribute·배신 상태 미저장** → 세이브 사이 배신/공물 상태 소실. v5 필드 추가 필요.

---

## 2. Phase F1 — 깃발 배선: 처단 시 플레이어 깃발 게양 (유령 봉합)

**목표**: 실내 영주전 승리(처형 OR 살려주기) → 영지 중심에 플레이어 문장색 깃발 게양 + 알림.

**작업**
1. `LordSurrenderSystem.ExecuteLord` (315행 SetOwnership 뒤) + `SpareLord` (351행 SetOwnership 뒤)에:
   `TerritoryCaptureSystem.OnTerritoryCaptured(territoryId, PlayerNation())` 1줄 호출.
2. `TerritoryCaptureSystem.GetOwnerColor`에 **플레이어 분기** 추가: newOwner가 플레이어 소유 판정(NationType.East 또는 전용 마커)이면 `EmblemManager.GetEmblemColor(CurrentEmblem.primaryColor)` 사용 — 국가색 하드코딩 대체(제외가드: EmblemManager 부재 시 기존 East=blue 폴백).
3. `SpawnFlag` 깃발에 문양 표시: 폴+색 큐브 위에 `EmblemManager.GetEmblemSymbol(shape)` TextMesh/이름표 부착 (FlagPoleDisplay.NameplateDisplay 선례) — "플레이어 깃발" 식별성.
4. 살려주기 경로는 깃발 게양 + 영주 생존 병기: `SpareLord`는 `DestroyLordObject` 대신 영주를 성 근처에 복귀 배치(개조: 현재 Placeholder 제거 → 제거 유지하되 배신 이벤트 시 재스폰 — F3 참조). **처형=깃발만, 살려주기=깃발+공물+배신 위험** — 분기 명확화.
5. (선택) `FlagManager` 유령 복활은 보류 — TerritoryCaptureSystem 큐브 깃발이 현행 비주얼 계약(J1 탑다운)과 호환. FlagPoleDisplay는 별도 검증 없이 건드리지 않음(회귀 방지).

**파일**: LordSurrenderSystem.cs(2곳), TerritoryCaptureSystem.cs(GetOwnerColor/SpawnFlag), (신규) 없음.
**검증**: CS=0 + 테스트 씬 영주전 → 처형/살려주기 각각 깃발 스폰 로그 `[TerritoryCaptureSystem] 🚩` + 문장색 일치(EmblemManager primaryColor).

---

## 3. Phase F2 — 주간 공물: 살려준 영주가 매주 골드 바침

**목표**: SpareLord된 영지에서 게임일 7일마다 공물 골드 지급 + 알림.

**작업** — 신규 `Systems/SparedLordTributeSystem.cs` (static, GuardSalarySystem 패턴)
1. 등록: SpareLord가 `_sparedLords[id] = {day basis, difficulty, personality}` 기록 (기존 lordSpared 플래그 재활용).
2. 지급: `TimeManager.OnDayStart` 구독 → `CurrentDay % 7 == 0 && day != _lastPaidWeek` → 영지별 `PlayerStats.AddGold(amount, "lord_tribute")`.
3. 액수 산정 (결정론): `GetSpareGoldReward(difficulty)`의 40%를 주간 기본액으로 + 성향 계수 — Cowardly 1.2 / Neutral 1.0 / Wise 1.0 / Brave 0.8 / Suspicious 0.9 / Greedy 0.6 (Greedy는 덜 바침 — 성격 반영) / Cruel 0.7. Round → 최소 10G 보장.
4. 충성도 연계: 지급마다 `state.loyaltyToPlayer +2` (공물=충성 표시), 미지급(불만족) 시 변동 없음.
5. 알림: `WarNotificationUTK`/CombatLog에 "🏛️ {영지명} {영주명}이(가) 주간 공물 {N}G을(를) 바쳤습니다" — 결제 요일에 1회.
6. 탈주 조건: 배신(F3)/소유권 상실 시 등록 제거.

**파일**: (신규) SparedLordTributeSystem.cs, LordSurrenderSystem.cs(SpareLord에 1줄 등록), TerritoryWarManager.cs(소유 상실 시 해제 훅 — SetOwnership 변경 지점).
**검증**: CS=0 + 테스트(시간 스킵 또는 리플렉션 day 강제) → 7일째 지급 로그 + 골드 원장 source="lord_tribute" 1건.

---

## 4. Phase F3 — 돌발 이벤트: 영주 배신 → 재탈환

**목표**: 살려준 영주가 확률적으로 배신 → 영지가 다시 LordOwned로 역전 → 플레이어는 다시 실내전투로 탈환해야 함.

**작업** — 신규 `Systems/LordBetrayalSystem.cs` (static)
1. 체크 주기: `TimeManager.OnDayStart` 구독, spare된 영지 각각에 대해 하루 1회 판정.
2. 배신 확률 (결정론 시드 — TerritorySeed/RollTerritoryGold 선례의 rng 패턴):
   `p = base(0.01) + personality( Cruel 0.04 / Brave 0.03 / Greedy 0.03 / Suspicious 0.02 / Neutral 0.01 / Wise 0.005 / Cowardly 0.001 ) + (1 − loyaltyToPlayer/100) × 0.03`
   → 최대 ~7%/일, 대략 평균 2~3주에 한 번 꼴. 충성도 높으면 거의 안 배신.
3. 발생 시:
   - `TerritoryDatabase.SetOwnership(id, LordOwned)` (OwnershipChanged → 실내 재구성 경로 자동 트리거 — 다시 영주 성 실내가 됨)
   - TerritoryState: lordSpared=false 복구, loyaltyToPlayer=0
   - 공물 등록 제거 + 알림 "⚠️ {영주명}이(가) 배신했습니다! {영지명}을(를) 되찾으려면 성을 다시 공격하십시오" (WarNotificationUTK + CombatLog)
   - 실내 수비 병사 재배치: 기존 `SpawnInteriorDefenseGuards`가 소유권 기반으로 자동 스폰(IndoorSceneTransition 분기) — 추가 작업 없음, InvadedLordEncounter도 `SpawnForInvadedTerritory`로 자동 재스폰 → **재탈환 전투 = 기존 실내전투 흐름 그대로 재사용**
4. 재탈환 성공(다시 처형/살려주기): 기존 흐름 — 살려주기 선택 시 공물 재등록(loyalty는 이번엔 +15로 시작 — 반복 배신 영주는 불신 반영), 배신 카운트 누적 → 2회 이상 배신한 영주는 배신 확률 ×1.5 (상한 12%).
5. 균형 가드: 동시에 배신 가능한 영지 = 최대 1곳 (몰아치기 방지) — 하루에 두 영지가 동시 배신하면 두 번째는 다음날로 연기.

**파일**: (신규) LordBetrayalSystem.cs, LordSurrenderSystem.cs(SpareLord에 카운트 기록 1줄), TerritoryDatabase.OwnershipChanged 역방향(플레이어→LordOwned)이 기존 구독자를 깨지 않는지 확인만.
**검증**: CS=0 + 확률 단위 리플렉션 강제 테스트(확률 100% 주입 → 배신 로그+소유 역전+공물 해제 확인) + 실내 재진입 시 영주 재스폰 로그.

---

## 5. Phase F4 — 크래프트 장소 자동 배정 (야외 보강)

**목표**: "깃발 게양 순간 각 크래프트 장소가 자동 배정" — 실내는 이미 작동, 야외에서 즉시 쓸 수 있는 크래프트 스테이션 보강.

**작업**
1. (이미 작동 — 문서화만) PlayerOwned 실내 재구성 시 `TerritoryBuilder.SpawnInteriorFixtures`(상점+크래프트하우스 트리거) + `PlayerCastleInteriorBuilder` 작업대(territoryKey 기반 TerritoryCraftingStation) 자동 배치 — 커밋 7049325a. ⚠ 유일 검증 필요: 실내 fixtures의 크래프트 박스가 **점령 영지 키**로 스테이션을 부착하는지(TestTerritoryCombatSetup 2233행 CraftStation_Box 선례) — nationStyle만 넘기는 현재 시그니처는 키 없음 → 필요 시 `SpawnInteriorFixtures(roomCenter, nationStyle, territoryKey)` 3인자 확장.
2. (신규) 야외 즉석 크래프트: 점령 완료 시 `TerritoryCaptureSystem.OnTerritoryCaptured`에서 영지 중심 근처(성 앞 정해진 슬롯)에 야외 크래프트 박스 1개 스폰 + territoryKey 기반 TerritoryCraftingStation 부착(리플렉션 — Systems→UI 순환 회피, PlayerCastleInteriorBuilder.AttachUiComponent 선례). E키 → WeaponForgeUTK. 제작대 1곳/영지 — 중복 스폰 멱등 가드(state 플래그).
3. 배정 규칙: 크래프트 종류(제작/요리/연금)는 영지 난이도 티어 기반 — Ring1=제작만, Ring2+=제작+요리, Empire=전부 (TerritoryDifficultyProfileSystem 재사용).

**파일**: TerritoryCaptureSystem.cs(스폰 훅), TerritoryBuilder.cs(시그니처 확장 시), (신규 없음 — 기존 야외 크래프트 박스 패턴 재사용).
**검증**: CS=0 + 점령 영지 야외 박스 E키 → 제작창 개방 로그 + 실내 작업대와 독립(같은 territoryKey 공유 — 충돌 시 "_outdoor" 접미사 별도 키, 무기고 "_armory" 선례).

---

## 6. Phase F5 — 세이브 보강 (v5 필드)

`TerritorySaveData`에 `lordSpared`, `tributeWeeksPaid`, `betrayalCount` 추가 → CollectTerritoryData/ApplyTerritoryData 동기화. v4 세이브 로드 시 신규 필드 부재 = 기본값(0/false) — 기존 세이브 호환. 없으면 세이브 사이 배신/공물 상태가 리셋되므로 **F2/F3와 같은 사이클에 넣는다**.

---

## 7. 실행 순서 & 게이트

F1(깃발 배선) → F5(세이브 필드) → F2(공물) → F3(배신) → F4(야외 크래프트) — 각 Phase 단위 컴파일 CS=0 게이트. F2·F3는 상호 의존(공물 등록=배신 대상 등록)이라 연속 구현. F4는 독립이라 마지막.

**병렬 위임 금지** — LordSurrenderSystem/TerritoryCaptureSystem은 컴팩트하지만 훅 지점이 겹침(46차 dual-path 교훈). 부모 직접 구현.

**미정(사용자 결정 필요 시 clarify)**:
- 공물 액수 산정의 난이도 기준액(40% 가정)과 성향 계수 값
- 배신 확률 밸런스(평균 2~3주 가정)
- 살려주기 후 영주를 성에 다시 세울지(시각 존재) vs 오프스크린(골드/알림만) — 본 계획은 오프스크린 기본
- 야외 크래프트 박스의 위치(성 앞 슬롯 가정)
