# 계획서: V3 크로매틱 수차 스파이크 (무번호 31 — 화면 계층 마지막 조각)

**선행**: 무번호 30(D-4 디졸브+S4 링 호흡) 착지(qa42 2351/2341/10). 무번호 23 계획서의 V3가 유일한 미구현 화면 계층 항목.
**판단 항목→구현 전환 근거**: "채택 여부 Play 체감 판단 권장" — Play에서 판단하려면 구현체가 존재해야 함. 보수 기본값+상수 트림으로 구현해 Play 판정 대상에 올림(과자극 시 상수 1줄/제거).

## 구현

| 항목 | 내용 | 파일 | 계약 주의 |
|---|---|---|---|
| V3 수차 스파이크 | 런타임 Volume(isGlobal)+VolumeProfile에 ChromaticAberration 오버라이드만 추가 — intensity 0→peak→0 스파이크(전체 0.1s, 진입 0.02s). IndoorPostProcessController 선례(런타임 프로필 생성). 인스턴스 1개 재사용(오브젝트 누적 금지), 스파이크 중 재호출=타이머 리스타트 | 신규 Assets/Scripts/Systems/ScreenChromaticFX.cs | unscaled 시간(VignettePulse 선례 — 히트스톱 중 재생), EditMode 무동작 가드, try/catch(시각 보조 — 전투 방해 금지), Random 0건, OnDestroy 프로필 파괴(누출 금지) |
| 훅 | 패링 PERFECT 임팩트(wave(0) 블록, V2 골드 비네트 직후) — ScreenChromaticFX.Spike(vc, 0.35f, 0.1f). 상수는 호출 인라인 | PlayerCombat.cs SpawnParryImpactElements | 기존 V2/히트스톱/화이트프레임 경로 무수정 — 추가 1~2줄만. GOOD은 미발화(과자극 최소화 — Play 판정 후 등급 확장 판단) |

## 검증

1. rg 사전검사: ScreenChromaticFX/ChromaticAberration 기존 충돌 0건(확인 완료)
2. 계약테스트 ScreenChromaticV3Tests(6~7케이스 — 파일 잠금: 클래스/시그니처/EditMode가드/isGlobal/unscaled/훅 호출/Random 블록스코프 부재 — 100차 함정 준수, 부정 잠금은 신규 클래스 블록 내 한정)
3. 컴파일 error CS 0 → EditMode 전수 + baseline 10건 1:1
4. Play 통합 판정(사용자): PERFECT 패링 순간 수차 체감 — 강도 0.35 트림 상수 1줄 조절

## 범위 제외(보류 유지)

22-P3 Unlit 전환(Play 비교 대기)·S1-4 지면 틴트(지형 조회 API 부재 — 설계 확정 필요)·VFX Graph(기각)·W2 구조 변경(보고 완료)·AfterlifeEnemy _attackRange(설계 판단).
