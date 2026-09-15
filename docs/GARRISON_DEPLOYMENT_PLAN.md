# ⚔️ 병사 배치(공격/수비) + 가시적 전쟁 + 차지/패링 — 구현 로드맵

> 요구(사장님): ① 차지/패링 절차 구현(애니 FBX는 나중에 교체) ② 실내씬에서 점령 영지에 배치된 병사를 **공격/수비**로 배치 ③ 전쟁이 콘솔 로그가 아니라 **병사가 실제 움직여 보이는** 구조.
> 작성: 2026-09-15 (55차 후속)

---

## 분석 결론 (조사 완료)
- 실제 전쟁 = `TerritoryWarManager.ExecuteWar`(숫자+로그만), `AIWarSystem.SpawnGarrison`은 데드코드(호출부 無)
- 플레이어 영지 = `TerritoryState.ownership == PlayerOwned` (기록: LordSurrenderSystem/PoisonTakeoverSystem)
- 수비 개념 = `TerritoryBuilder.BuildGuardsAt`/`GateGuardPlaceholder`(문지기) 이미 존재
- 전쟁→수비 개입 통로 없음(PlayerOwned 제외+이벤트 구독자 無)
- 병사 수 = `guardCount`(불변)+`guardAliveRatio`(가변)
- 차지/패링 = PlayerCombat에 입력/상태 無

---

## Phase 1 — 차지(강공) & 패링(방어) 절차 구현
**파일:** `Systems/PlayerCombat.cs`, `Systems/Animation/Procedural/ProceduralAnimStateMachine.cs`(+Controller)
- [ ] 1-1 차지: 우클릭 유지 → 0.5s 충전(오라 파티클 절차, 히트스톱 준비) → 해제/0.8s 최대 시 강력 1타(데미지×1.8, 임팩트 1.5배, 충전 보너스). 클립 플러그인 포인트(`_chargedClipName` — FBX 교체 시 클립명만 연결)
- [ ] 1-2 패링: 좌클릭 직후 짧은 창(0.25s) 방어 판정 → 근접 공격 흡수(0.8배 감쇠)+상대 플린치. 상태 생성
- [ ] 1-3 애니: 상태 머신에 Charge/Parry 상태 추가(절차 동작 — 후일 FBX 클립 연결 대비 `TriggerAction("charge"/"parry")`)
- [ ] 1-4 1·2-tone 사운드/화면 틴트(기존 FX 스택 재사용)

## Phase 2 — 점령 영지 병사 공격/수비 배치 (실내씬)
**파일:** `Systems/PlayerCastleInteriorBuilder.cs`, `Systems/GuardManager.cs`, `Systems/TerritoryData.cs`, 신규 `Systems/TerritoryDeploymentSystem.cs` + UI
- [ ] 2-1 배치 상태 필드: `TerritoryState.garrisonRole`(None/Attack/Defense) + `deployedAttackPower`
- [ ] 2-2 실내씬에서 점령 영지 목록 + 병사(guardCount) → 공격/수비 토글 UI(성내부 위원회/지도 인터랙션)
- [ ] 2-3 공격 배치: `TerritoryBuilder.SpawnGarrison`로 해당 영지 병사 생성 → 지정 공격 대상 영지로 **절차 이동**(WalkPoint 방식, 갱/통로) → 도착 시 전투
- [ ] 2-4 수비 배치: `BuildGuardsAt`/GateGuard 재활용 — 플레이어 영지 성문에 문지기 배치+재충원
- [ ] 2-5 전쟁 시각화 연동: 배치된 공격/수비 병사가 실제 움직여 보이도록

## Phase 3 — 가시적 전쟁 (병사가 실제 움직임)
**파일:** `Systems/TerritoryWarManager.cs`(실동작), `Systems/AIWarSystem.cs`(스폰 경로 재활성), 신규 `Systems/WarMarchSimulation.cs`
- [ ] 3-1 전쟁 시작 시 양측 `SpawnGarrison` 병사 스폰(기존 AIWarSystem 경로 재배선)
- [ ] 3-2 병사 이동: 공격군이 공격자 영지→방어자 영지 경로를 **실제 월드에서 걸어감**(WalkPoint 시퀀스, 진행률=이동 진도 연동)
- [ ] 3-3 도착 후 소규모 전투(기존 수치 결과 + 가시적 깃발/파티클/플린치) → 승패 컷씬 최소화
- [ ] 3-4 onRobot/겹침 방지, 저사양 캡(동시 행진 군 수 제한)

## Phase 4 — 검증 & 3중 저장
- [ ] 4-1 배치컴파일 error CS=0 (에디터 닫은 상태)
- [ ] 4-2 정적 QA + 로드맵/기획 반영
- [ ] 4-3 QAPROGRESS + ROADMAP + 영구메모리 + git commit/push