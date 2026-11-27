# no87 — 아레나 맨손 격투 계획 (2026-11-20 — 병사 신규 애니 5종 반영, 사용자 요청 실행)

## 0. 에셋 실측 (11-20 12:00 추가 — Assets/Animations/)

| 클립 | 파일 | 용도 |
|---|---|---|
| 펀치 | 병사 펀치.fbx | 기본 공격 (사이클 1·3) |
| 돌려차기 | 병사 돌려차기.fbx | 기본 공격 (사이클 2) |
| 박치기 | 병사 박치기.fbx | 기본 공격 (사이클 4) |
| 펀치 후 돌려차기 | 병사 펀치 후 돌려차기.fbx | 강타(피해 ≥25) 콤보 |
| 막기 | 병사 막기.fbx | 약타(피해 <25) 수비 반응 |

- 임포터 animationType: 2 = **Generic** — transform 경로 바인딩이라 병사 리그 경로 정합 감사가 선결 조건(불일치 시 기록 없이 중단+사유 보고).
- 기존 경로: 아레나 결투선수 = soldier_lv1-20_rigged.fbx(Humanoid avatar) + SoldierShield_AC(믹사모 클립: Idle/Move/Attack/Hit/Death) + HumanoidClipDriver(Soldier). 공격 = TriggerAttack → Attack 상태(무기 휘두름).

## 1. Phase

### P1 — SoldierFist_AC 신설 (에디터 배치 스크립트, 감사 게이트)
- Assets/Editor/No87ArenaFistWiring.cs — ①5종 클립 실측 덤프(이름/길이/루프) ②클립→병사 FBX 리그 경로 전수 감사 ③통과 시에만 컨트롤러 생성:
  - 파라미터: Speed/MoveRate(float) + Hit/Death/Block(trigger)
  - 상태: Idle·Move(SoldierShield_AC 모션+Speed 전이 임계 승계) / Punch·Kick·Headbutt·PunchKick·Block(신규 클립, 루프 off·exit-time 0.92→Idle) / Hit·Death(기존 모션 승계 — Death는 출구 없음=쓰러짐 유지)
  - 트리거 진입: Idle·Move에서 Hit/Death/Block (공격 상태는 Play() 직결 — 트리거 불요)
- 검증: 배치 로그(감사 결과·생성 완료) + 컨트롤러 YAML 상태 전수 + 계약 테스트

### P2 — 듀엘 결합 (ArenaBettingSystem)
- 결투선수 컨트롤러: SoldierFist_AC 우선, 로드 실패 시 SoldierShield_AC 폴백(기존 경로 보존)
- StrikeRound 공격: `ReplayAction(상태)` 결정론 선정 — 데미지 ≥25 → PunchKick, 그 외 [Punch, Kick, Punch, Headbutt] 듀엘 공격 카운터 사이클. 데미지 산식/회피/베팅 수치 **불변**
- 수비 반응: KO(HP≤0)·≥40 → TriggerDeath(패자 쓰러짐 — 구 Knockdown은 SoldierShield_AC 미정의 no-op이던 것을 가시화) / ≥25 → TriggerHitLight(Hit 경직) / <25 → TriggerBlock(드라이버 신설 1줄)
- 검증: 소스 계약 테스트(선정 로직·게이트·폴백) + 전수 EditMode baseline 1:1

## 2. 계약
- 데미지/회피/정산 수치 불변(애니 레이어만 변경) · Random 추가 금지(클립 선정은 결정론) · SoldierShield_AC 무수정(공유 컨트롤러) · 카메라/물리(G3 kinematic) 무수정 · [무번호 87-*] 마커 · 커밋·Play 금지(별도 게이트)
- 클립-리그 감사 실패 시: 컨트롤러 미생성 + 로그에 불일치 경로 보고(솔직 보고 계약)
