# 95차 스펙 — 패링 액션감 강화: 퍼펙트 판정/히스톱/카메라 펀치/방향성 스파크 + 적 공격 텔레그래프

> 발행: 감독자 (2026-10-16). 근거: PlayerCombat/PlayerHealth/AnimalAI 소스 실측. 사용자 요구: 패링을 액션감 있고 고품질로 — 1+2단계 묶음 즉시 진행(승인).
> 현황 실측: V키/창 0.45s 단일/성공 시 슬로우(로컬 0.10×, dip0.10-hold0.38-restore0.28)+ShockwaveRing+StarFlare+입력큐(3) — 파이프라인: 적 접촉공격 → PlayerHealth.TakeDamage → TryParryReflection → PlayerCombat.TryParry.

## F1 — 퍼펙트/굿 2단계 판정 (PlayerCombat.cs)
- 창 경과 산정: `elapsed = ParryWindow - (_parryActiveUntil - Time.unscaledTime)` (0~0.45s).
- **순수 함수 분리**: `internal static ParryGrade EvaluateParryGrade(float elapsed)` → PERFECT(elapsed ≤ ParryPerfectWindow=0.15f) / GOOD(그 외) — 테스트 가능(시간 주입 불요). [95차 F1] 마커.
- **PERFECT 보상**: ①강 슬로우(현행 0.10 유지)+**금색 FX**(링 색상 0.65,0.9,1 → 금색 1,0.85,0.3 + 별섬광 유지) ②**즉시 반격 보상**: 큐된 공격 첫 1타를 슬로우 복원 전 자동 해방 + 그 타 데미지 ×ParryCounterMultiplier=1.5f ③공격자 강 넉백(kbDir×1.5 — 기존 넉백 파이프라인 재사용).
- **GOOD**: 현행 프레젠테이션 무변경.
- 로그: `[PlayerCombat] 🛡️ 패링 성공(PERFECT/GOOD) — ...` [95차 F1] 마커.
- 기존 슬로우 재발동 baseline 보존 로직(89차~) 유지 — 히스톱(F2)과 상호 간섭 없게 ParryImpactRoutine 선두에서만.

## F2 — 히스톱 (PlayerCombat.cs — ParryImpactRoutine)
- 진입 직전 **완전 정지 히스톱**: LocalTimeScale 0.05×50ms(hold) 후 기존 dip(0.10→…→복원) 진입 — '물림' 임팩트. [95차 F2] 마커.
- 패링 재발동 시 기존 코루틴 정지+재시작 경로 유지(히스톱도 재시작 — 이중 슬로우 방지 기존 baseline 로직 재사용).

## F3 — 카메라 펀치 (PlayerCombat.cs — TriggerCameraEffects 재사용)
- 성공 시 FOV 45→38 급줌(0.08s)→복원(0.25s)+미세 셰이크 — 기존 TriggerCameraEffects/카메라 이펙트 인프라 확장. [95차 F3] 마커. 미스(빈 차지) 경로 무간섭.

## F4 — 방향성 스파크 (FX)
- 흡수점(전방 1m+0.9up)에서 **공격자 방향(kbDir 역방향) 원뿔 스파크** — 파편 8~12개 라인/작은 쿼드 0.3s — ShockwaveRing+StarFlare와 병행(교체 아님). 기존 FX 스폰 패턴(ShockwaveRingFX.Spawn 선례) 준수. [95차 F4] 마커.

## F5 — 적 공격 텔레그래프 (AnimalAI.cs + AfterlifeEnemy.cs)
- **대상**: AnimalAI(819행 TakeDamage(melee) 접촉 근접)+AfterlifeEnemy(쿨다운 접촉 공격) — 즉발 TakeDamage를 **예고→타격 2페이즈**로:
  1. 공격 조건 충족(사정거리/쿨다운) → **예고 페이즈 0.5s**: ①선동작(기존 애니/절차 스케일 펄스 1.0→1.12→1.0 — 애니 에셋 불변) ②발밑 경고 인디케이터(붉은 반투명 링 쿼드 펄스 — 월드 스페이스, HUD 게이지 계약 무관 — BlobShadow 쿼드 선례 재사용).
  2. 예고 종료 시 **거리 재확인**(플레이어가 빠졌으면 공격 공백 — 빈 스윙) → TakeDamage 기존 시그니처 그대로.
  3. 쿨다운/데미지 밸런스 무변경 — 예고만 추가. [95차 F5] 마커.
- 몬스터/적 AI가 다중이어도 이번 범위는 위 2개 클래스(실측된 접촉 공격 원천) 한정 — 스킬 체계(MonsterSkillSystem)는 후속.
- 텔레그래프 중 패링 창과의 정합: 예고 0.5s ≥ 패링 반응 여유 — 별도 튜닝 없이 기존 0.45s 창으로 커버 가능.

## 범위 화이트리스트 (구현 시)
- Core: PlayerHealth.cs(판정 전달부 무변경 — 필요 시 소폭: 판정 결과 전달만)
- Systems: PlayerCombat.cs(F1~F4), AnimalAI.cs, AfterlifeEnemy.cs(F5), FX 파일(방향성 스파크 — ShockwaveRingFX 동일 파일/신규 스폰 함수)
- Tests: PlayerCombatParryInputTests/PlayerParryDamageTests 확장 + AnimalAI 텔레그래프 상태 테스트 — [95차] 마커

## 검증 계획
- 컴파일 error CS 0 / EditMode baseline 11 외 0.
- Play(승인 게이트): ①선책정 패링 → PERFECT 로그+금색 FX+반격 자동 소화 ②히스톱 체감(물림) ③FOV 펀치 ④동물 공격 전 붉은 링 예고 0.5s ⑤기존 전투 밸런스 무변경.

## 확정 사항(감독자)
- 키/충돌 셀프감사: 신규 키 없음(반격=큐 재사용), HUD 게이지 계약 무관(월드 쿼드), 슬로우 중첩 방지 기존 로직 재사용.
- 밸런스(데미지/쿨다운) 무변경 — 텔레그래프는 순수 예고 추가.
- 3단계(믹사모 패링 클립/실 SFX)는 에셋 확보 후 별도.
