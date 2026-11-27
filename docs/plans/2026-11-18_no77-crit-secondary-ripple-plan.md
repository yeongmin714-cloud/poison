# 계획서: 크리 2차 리플 (무번호 77 — 디스토션 에코)

**착지 기록(2026-11-18)**: FxSecondaryRipple.cs 신규(63행 — Burst 재사용·unscaled 지연) + CombatFXGate 1줄 배선 + P77 테스트 4건. FxDistortionWave.cs 무수정(71-L2 잠금 카운트 보존 테스트 포함). 컴파일 error CS 0, EditMode 2565/2555/10 — baseline(qa_76) 실패집합 1:1, 신규 4/4 GREEN. 쿨다운 실측 확정값 **0.12s**(계획서 초안 0.08s 정정 — 76-S1/S2 선례 정합).

**사용자 지시(2026-11-18)**: "크리 2차 리플까지 진행" — 무번호 76 이월 항목 착지. Play·커밋 별도 승인 유지.

---

## 원장 근거

- 76 설계도 제외 이월: "크리 전용 파장 잔광 — 기존 디스토션 웨이브(71-L2) 뒤 2차 저강도 리플"
- **FxDistortionWave(71-L2) 실측**: `Burst(Vector3 position, float scale)` — 확장 0.5→1.6m × scale / 0.4s / unscaled 시간 / 동시 상한 4 예산 / opaque 텍스처 리플렉션 가드(fail-closed). **잠금 문자열(테스트 FxCombatResidueP71Tests:91~95)로 수정 불필요 & 수정 금지** — 재사용 경로로만 구현.

## 설계 — 에코 스케줄러 (FxDistortionWave 무수정)

- 신규 `Assets/Scripts/Systems/FxSecondaryRipple.cs`:
  - `public static void Schedule(Vector3 position, float scale, float delaySec)` — 지연 발화 전용:
    - Host GameObject("SecondaryRipple", HideAndDontSave) + 러너 컴포넌트
    - 지연 = **WaitForSecondsRealtime(delaySec)** — 히트스톱 프리즈 중에도 시간 언어가 이어지는 23-X1 unscaled 규약 준용(디스토션 웨이브 자체도 unscaled — L2)
    - 지연 후 `FxDistortionWave.Burst(position, scale)` 1회 호출 → 자가 파괴(유한 수명 — 누출 금지)
    - EditMode 가드: `!Application.isPlaying` 시 즉시 스킵(Burst 내부 가드와 이중 안전)
    - 예산: Burst 내부 MaxConcurrentWaves 4를 공유(별도 카운터 없음 — 이중 가드 금지), 초과 시 조용 스킵(기존 계약)
  - 동일 지점 중복 스케줄 가드: 정적 마지막 스케줄 시각 + 0.08s 쿨다운(결정론)
- **게이트: 크리 전용** — 일반 타격에는 발화하지 않음(76-S3와 다른 밴드. 2차 리플은 크리 시그니처의 여운).
- 파라미터 결정론 상수: scale = `0.55f`(1차 웨이브 대비 저강도·소형), delay = `0.12f`(1차 확장 중반부에 뒤따르는 잔광).

## 배선 — 수정 `Assets/Scripts/Systems/CombatFXGate.cs` (1줄)

- `FxDistortionWave.Burst(position, isCrit ? 1.5f : 1f);`([71-L2] 호출부) 직후:
  `if (isCrit) FxSecondaryRipple.Schedule(position, 0.55f, 0.12f);   // [무번호 77] 크리 2차 리플 — 1차 웨이브 잔광`
- 기존 문자열 카운트 무변경(70-U3·71-L2·46차·76-S3 전부) — 최소 diff.

## 계약 (잠금 무손상)

- **FxDistortionWave.cs 파일 자체 무수정** — 71-L2 잠금 테스트 문자열 전부 보존
- MaxConcurrentWaves 4 공유 — 새 예산 상수/카운터 신설 금지
- 결정론(Random 0건)·opaque 미지원 환경 조용 스킵(fail-closed — Burst 내부 가드 승계)
- MagicHit 룩·LDR/블룸 캡 계약 무접촉

## 테스트 — 신규 `Assets/Scripts/Tests/EditMode/FxSecondaryRippleP77Tests.cs`

- 마커·Schedule 시그니처·WaitForSecondsRealtime(unscaled 규약)·Burst 재사용 호출 문자열·크리 전용 게이트·쿨다운 상수·FxDistortionWave 무수정(71-L2 잠금 문자열 카운트 보존 어서션)·Random 0건

## QA 게이트

- 컴파일 error CS 0 + 전수 EditMode **baseline=qa_76(2561/2551/10) 실패집합 1:1** + P77 GREEN
- 배치 background+폴링. Play·커밋 별도 승인.
