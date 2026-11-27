# 계획서: 사망 물리 + 타격 소리 위계 (무번호 72 — W2 러그돌 이완 1단계 + W3 피치 변조)

**사용자 지시(2026-11-17)**: "진행" — 잔여 4건 중 권장순 W2·W3 먼저 착지(E2/E3는 후속). Play·커밋 별도 승인 유지.

---

## 0. 셀프감사 (중복·계약 — 2026-11-17 실측)

- **W2 근거 실측**: AnimalAI 사망 시퀀스 = 절차 눕힘(루트 90° 롤 0.5s) → sink(0.4m/≤0.5s) → 시체 숨김·리스폰(DeathDownRoutine). 현재 눕힘 중 **사지는 Idle 포즈 그대로 경직**(StartDeathSequence에서 `_rigAnim.SetStateImmediate(Idle)` — 리그가 계속 Idle 구동). "눕혔는데 다리는 서 있는" 부자연 = W2의 뿌리.
- **W3 근거 실측**: ImpactSoundFX = 피치 개념 0건(78라인 전수 확인). PlayHit(bool, bool) 시그니처 — 호출부 CombatFXGate:181 단일. SoundManagerEnhanced.PlaySFX(string, volume) = **공유 _sfxSource.PlayOneShot** — 피치는 소스 단위.
- **계약 잠금**: SoundManagerEnhanced/RigAnimationController 관련 계약테스트에 PlaySFX·사망·리스폰 문자열 잠금 0건 실측. "재생 경로는 SoundManagerEnhanced.PlaySFX 단 하나" 주석 계약 — PlaySFX 확장(선택 파라미터)으로 경로 단일성 유지.
- **중복 0**: 러그돌/물리 컴포넌트, 피치 변조 기존 구현 0건.

## W2 — 몬스터 사망 관절 이완 1단계 (눕힘 강화)

- **범위**: AnimalAI 몬스터 전용(절차 눕힘 보유 시스템). AfterlifeEnemy 등 타 사망 경로 범위 외.
- **구현**: 신규 `FxDeathRelax.Begin(GameObject, float duration)` static 유틸 —
  - 대상: 자식 SkinnedMeshRenderer의 bones 중 이름 패턴 매치(Arm/ForeArm/Hand/Leg/Shin/Foot/Paw — 대소문자 무시) 사지 본.
  - 동작: 사망 개시 시각 본 localRotation 캡처 → duration 동안 각 본을 사지별 고정 이완 오프셋(팔~40° 드롭/다리 ~20° 굽힘 — 결정론 상수)으로 Slerp. Random 0건.
  - **리그 프리즈**: DeathDownRoutine 진입 시 `_rigAnim.enabled = false`(컴포넌트 인스턴스 상태 변경 — **RigAnimationController.cs 파일 무수정** = "애니 시스템 무수정" 계약 준수) → 본 구동 충돌 제거. Respawn에서 `_rigAnim.enabled = true` 원복(1줄, 마커).
- **배선**: StartDeathSequence에서 FxDeathRelax.Begin 호출(try/catch 격리 — 46-DIE FX 격리 선례, 사망 후처리 방해 금지) + DeathDownRoutine 프리즈 1줄.
- **폴백**: 사지 본 0건(명명 미매치) = 조용 스킵 — 기존 절차 눕힘만(현행 무손상). EditMode 무동작 가드.
- **타이밍**: 이완 0.35s — 눕힘 0.5s 창과 겹침, 시체 숨김(0.6~1.2s) 전 완료. 디졸브(0.9s)와 병행 무충돌(본 회전 vs 머티리얼 램프).

## W3 — 타격 소리 피치 위계

- **SoundManagerEnhanced**: `PlaySFX(string, float volume = 1.0f, float pitch = 1.0f)` 선택 파라미터 확장 — 호출 시작 `_sfxSource.pitch = pitch` 후 PlayOneShot. 기본값 1.0 = 기존 호출부 전부 무수정·무영향. 매 호출마다 피치 재대입이라 이전 변조 잔존 없음(자기 치유). 단일 재생 경로 계약 유지.
- **ImpactSoundFX**: `PlayHit(bool isCrit, bool isKill, float damage = -1f)` 선택 파라미터 — 결정론 밴드 매핑: 피해<8 → 0.92 / 8~15 → 1.00 / ≥15 → 1.06, 크리 ×1.04 가산(상한 1.15). Random 0건, 밴드 경계는 PlayerCombat Heavy(≥15) 공유 기준.
- **호출부**: CombatFXGate:181 → `ImpactSoundFX.PlayHit(isCrit, isKill: false, damage)` 1줄 변경(이전 호출 문자열과 무관 — 잠금 확인済).
- **계약**: HighSpec 게이트 유지 · 플레이스홀더 로그 경로 유지(클립 부재 크래시 없음) · "단 하나" 재생 경로 주석 계약 보존.

## 범위 파일 (scope files)

- 신규: `Assets/Scripts/Systems/FxDeathRelax.cs` (W2)
- 수정: `Assets/Scripts/Systems/AnimalAI.cs` (W2 — StartDeathSequence 훅 1줄 + DeathDownRoutine 프리즈 1줄 + Respawn 원복 1줄) — **[무번호 71-G2] 훅 문자열 무손상**
- 수정: `Assets/Scripts/Systems/SoundManagerEnhanced.cs` (W3 — PlaySFX 선택 파라미터)
- 수정: `Assets/Scripts/Systems/ImpactSoundFX.cs` (W3 — damage 파라미터+밴드 매핑)
- 수정: `Assets/Scripts/Systems/CombatFXGate.cs` (W3 — 호출부 1줄) — [70-U3]/[71-L2] 문자열 무손상
- 신규: `Assets/Scripts/Tests/EditMode/FxDeathPhysicsP72Tests.cs` — W2(마커·본 패턴 매치·캡처/Slerp·Random 0건·프리즈/원복·RigAnimationController 무수정 간접)/W3(마커·밴드 상수·선택 파라미터 기본값·HighSpec 게이트·호출부) 소스 계약

## 계약 주의

- 부정 잠금 = 코드형 토큰만. FX 격리 try/catch(사망 후처리 방해 금지 — 46-T2 원칙).
- W2는 물리엔진(Rigidbody/ConfigurableJoint) 미사용 — 절차 본 회전만("러그돌 관절 이완 1단계"의 절차 구현 — 실제 러그돌 전환은 후속 단계 후보).
- PlayMode 불필요 — 체감은 사용자 유예. 커밋 별도 승인.

## QA 게이트

1. 컴파일 error CS 0 (qa_72_compile.log)
2. 전수 EditMode baseline(qa_71 = 2520/2510/10) 실패집합 1:1 — 신규 실패 0 (qa_72_editmode-results.xml)
3. 집중: FxDeathPhysicsP72Tests + FxCombatResidueP71Tests(71 계약 무손상) GREEN
