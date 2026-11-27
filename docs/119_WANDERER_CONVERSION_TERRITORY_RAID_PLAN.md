# 119차 계획서 — 방출 병사 → 방랑자 전환 + 방랑자의 플레이어/영지 공격

> 작성: 2026-10-23 · 사용자 확정: **모든 이탈 경로 확장** (플레이어 방출 + AI 영지 감축 + 충성도 탈영)
> 기본안 확정: 약탈+경보까지(점령 없음) · 기존 AI 재고용 시장 루프 유지

## 0. 현실 실측 (계획 근거)

| 항목 | 실측 |
|---|---|
| 방출 경로 | 유일 런타임 경로 = `GuardSalarySystem`(골드 부족 시 최고가 병사 자동 방출, L176) → `LaborMarketSystem.ReleaseGuard` → 시장 풀(`_pool`). 방출 병사는 GameObject 그대로 제자리(배회/공격 AI 없음) |
| AI 영지 감축 | `AITerritoryEconomySystem.PayAILordMaintenance` — 유지비 부족 시 최고가 병사부터 `Object.Destroy` (L153~160). 방출 아님 |
| 충성도 이탈 | 신규 경로 없음 — 현재 충성도는 적대 임계(0/-30/-50)만 존재(GuardHostilitySystem), 이탈(탈영) 메커니즘 부재 |
| 방랑자(전투형 배회) | 부재 — 주민용 `VillagerWanderer`(평화 배회)만 존재 |
| 공격 명령 확립 경로 | 3종 세트: `HostileToPlayerFaction=true + SetCommandTarget(pos, true) + SetInCombat(true)` — GuardHostilitySystem.InitiateAttackVsPlayerAndSoldiers / HostileTerritoryCombat.NotifyHostile 실사용 |
| 영지 물리 행진 선례 | `WarMarchSimulation` — 병사 스폰 → SetCommandTarget(적 영지, 공격) → 행진 → 교전 → 정산 |
| 영지 판정 | `TerritoryDatabase.ResolveTerritoryAt(pos, radius=60f)` |
| 병사 간 적대 게이트 | `IsEnemyGuard` = 국가 문자열 상이만 적. `ResolveAttackTarget`(L1573)/`ValidateAttackTarget`(L1499)이 후보 필터. **아웃로(무소속 적대) 개념 없음** |
| 피격 반격 | GuardPlaceholder.TakeDamage에 자동 반격 없음 — 반격은 명령 발행 방식(WarMarchSimulation 선례: 양측 모두에 명령) |
| 경보 | `AlarmSystem.TriggerAlert(locationId, position)` |
| 게임일 | `TimeManager.Instance.CurrentDay` / `OnDayStart` 이벤트 |

## 1. 설계 확정 사항

- **방랑자 = 무소속 아웃로**: 원 국가 문자열은 유지(세이브/급료/표시 파괴 방지)하되 `IsOutlawWanderer` 플래그로 모든 병사·플레이어·영지와 교전 가능하게 한다.
- **게이트키퍼(IsGatekeeper)는 방랑자화 제외** — TerritoryGateSystem 구조 보호(문지기 전원 포섭 게이트 규약). 감축/탈영 대상이 게이트키퍼면 기존 경로(파괴/유지) 그대로.
- **방랑자 세이브 미영속** — 기존 시장 풀(_pool)이 런타임 전용인 것과 동일 규약.
- **약탈 = 영지 금고 10% 탈취**(소멸 — 세계 경제 손실) + `AlarmSystem.TriggerAlert` + CombatLog. 영지당 게임일 1회.
- **점령 없음** — 소유권 이전은 기존 계통(Assassination/LordSurrender/PoisonTakeover/AIWarSystem) 전용 유지.

## 2. Phase 분해

### Phase 1 — 방랑자 코어
1. **`GuardPlaceholder` 확장**
   - `public bool IsWanderer { get; private set; }` + `public bool IsOutlawWanderer => IsWanderer`
   - `BecomeWanderer(string reasonLog)`: 방랑자 플래그 ON + 부모를 씬 루트로 재배치(Territory_ 계층 탈출 — AI 경제 재계산/영지 재구축 파괴 방지) + IsGatekeeper=False(예외 대상은 호출 전 필터) + 로그
   - `EndWanderer(Vector3 gotoPos)`: 플래그 OFF + 드라이버 정지 + 이동 명령
   - **아웃로 게이트 2줄 확장**: `ResolveAttackTarget` L1573 → `if (!IsEnemyGuard(otherGuard) && !otherGuard.IsOutlawWanderer) continue;` / `ValidateAttackTarget` L1499 동일 확장 → 동일 국가 방랑자도 교전 성립
2. **`WandererDriver.cs` 신규** (Systems, MonoBehaviour — 방랑자 GuardPlaceholder에 부착)
   - 배회: 랜덤 방향 웨이포인트 40~90m → `SetCommandTarget(pos, false)` 재사용(기존 이동/애니/정체 우회 파이프 그대로) → 도착 후 휴식 3~8s → 반복. 결정론: System.Random 금지 규약 준용 — 인스턴스 해시 기반 결정론 시퀀스
   - 초기 웨이포인트 = 방출 원 영지 바깥 방향(원 영지 즉시 습격 방지)
   - 플레이어 조우 선공: 0.5s 주기 스캔, 반경 10m → 3종 세트(플레이어)
   - 영지 습격: 현재 위치가 영지 내(ResolveTerritoryAt) → 주둔 병사 조회(GuardManager.GetGuardsInTerritory + 게이트키퍼 제외) → **양측 명령 발행**(WarMarchSimulation 선례): 방랑자→최근접 수비병 공격, 수비병→방랑자 공격(3종 세트)
   - 약탈: 영지 내 살아있는 수비병 0명 + 미약탈(영지×게임일 1회) → territoryGold 10% 탈취 + 경보 + 로그
   - 전투 종료 감지: `!HasCommand` → 배회 재개(SetInCombat(false))
3. **방출 경로 배선 (3종)**
   - `LaborMarketSystem.ReleaseGuard` 성공 직후 → `guard.BecomeWanderer()` (풀 등록은 유지 — AI 재고용 대상 유지)
   - `AITerritoryEconomySystem.PayAILordMaintenance` 감축 분기: 게이트키퍼는 기존 Destroy, 일반 병사는 `manager.RemoveGuardFromAllTerritories` + `BecomeWanderer()` (guardAliveRatio 감소 로직은 유지 — 영지 병력 기준 동일)
   - 탈영: `GuardSalaryManager.OnDayStart` 신규 4단계 — 전 병사 스캔, `Loyalty <= -70`(ALARM -50 아래 신규 임계, 상수 public) → 포섭 병사: SetRecruited(false)+방랑자화 / AI 고용(IsAIHired): 로스터 제거+방랑자화 / 게이트키퍼·사망자 제외

### Phase 2 — 시장/포섭 상호작용 정리
4. `LaborMarketSystem.AssignToLord`(재고용 성공) → `guard.EndWanderer(영지 중심)` — 방랑 종료 후 해당 영지로 보행
5. `GuardPlaceholder.OnRecruit` 성공 경로 → 방랑자였다면 `EndWanderer(플레이어 위치)` (포섭 시 방랑 해제)

### Phase 3 — 테스트 + QA (구현/QA 서브 분리)
6. 신규 EditMode: `WandererConversionTests` — 방출→방랑자(플래그+재부모+풀 유지) / AI 고용→방랑 해제 / 감축→방랑자(게이트키퍼 파괴 유지) / 탈영 임계(-70 이하 1일 1회)
7. 신규 EditMode: `WandererRaidTests` — 영지 진입→양측 공격 명령 발행 / 수비병 전멸 시 약탈(금고 −10%, 영지×일 1회 멱등) / 경보 발화
8. 기존 영향 조사: AILaborRehireFeeTests(15건 — ReleaseGuard 시그니처 무변경, 방랑자화 추가만) / VillageEconomyTests(감축 계약) / baseline 11건
9. 검증: 컴파일 error CS = 0 + EditMode 전수(baseline 1:1, 비baseline 0)

## 3. 리스크/주의

- ⚠ 위임 서브 지연 플러시 파손 가능 — 위임 직후 파일 size/brace 무결성 검사
- 서브 write_file/patch 경로 따옴표 리터럴 금지, .meta 생성 금지(Unity 재생성 위임)
- 방랑자 수 상한: 세션 누적 방출이 계속 쌓이면 성능 영향 — Driver 상한 관리(최대 N명 활성, 초과분은 정지 상태 대기)는 Phase 1에 단순 카운트 가드로 포함
- 충성도 이탈(탈영) 임계 -70은 GuardHostilitySystem 임계(0/-30/-50)와 독립 신설 — 기존 적대 규약 무변경
- PlayerCombat 조준/호버(HoverTargetClassifier)에 방랑자가 기존 적병사 경로로 자연 노출되는지 Play에서 확인(별도 승인 게이트)
