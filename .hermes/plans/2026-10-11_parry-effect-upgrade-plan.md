# V키 패링 이펙트 고도화 계획 (2026-10-11, 20차 예정 — 계획 전용)

> **게이트: 계획 전용** — "패링 이펙트를 조금 더 발전시킬 수 있는 계획" → 승인 후 Phase별 실행.

## 0. 현재 패링 파이프라인 (소스 실측)

- 입력: **V키**(`PlayerCombat._parryKey = KeyCode.V`, 인스펙터 변경 가능) → `TryStartParry()`
- 창: `ParryWindow = 0.45s`(실시간) — 창 내 피격 시 `PlayerHealth` typed-damage interception → `TryParry` 성공
- 성공 연출(현행): **무기 중립 흡수 + Magic Time/BulletTime 액션 슬로우**(BulletTime.Apply(0.35, 0.10, 0.38) — 크리티컬 슬로모와 공용) + Parry 클립(`Sword_Parry_Backward_1`)
- **부재**: 패링 성공/실패의 시각·청각 피드백 FX 전무 — 슬로모만으로 "패링했다"가 안 읽힘

## 1. 목표 (실내씬 예시/참조 영상 언어 계승)

"패링 성공"이 한눈에 읽히는 5계층 피드백 — 기존 검증된 FX 자산 재사용(신규 셰이더/에셋 최소화).

## 2. Phase 구성

### Phase P1 — 패링 성공 임팩트 FX (핵심)
- **확장링**: `ShockwaveRingFX.Spawn` 재사용(화살 방패 막기 8999ff49 선례) — 방패/무기 위치에서 청록→백색 확장 링 1회
- **별섬광**: `SpawnStarFlare` public static 재호출 — 패링 접촉점 화이트 스파이크
- **스파크 파티클**: 노랑 `shadow_glow` Hemisphere 5발(선례 머티리얼 패턴 — URP Particles/Unlit 폴백 Sprites/Default)
- 출력 위치: 패링 무기 끝점(그립 테이블 tip 판정 재사용 — `ApplyBoundsGripAlignment` 선례) 또는 피격점
- 테스트: FX 스폰 계약(명명/컴포넌트/수명) 소스 단정 + 컴파일

### Phase P2 — 패링 창/실패 피드백
- **창 진입**: 플레이어 주변 짧은 웜 아우라 링(SelectionRing 셰이더 재판정 — `_ArcSpeed` 활성 호 스윕 0.45s, 창 종료와 동기 소멸) — "지금 패링 가능" 가시화
- **성공 시**: 창 링 즉시 소멸 + P1 임팩트
- **실패(창 놓침) 피격**: 기존 HitReaction 유지 + 미세 화면 진동(CombatCameraEffects 선례)
- 테스트: 창 링 수명 = ParryWindow(0.45s ± 오차) 계약

### Phase P3 — 사운드 + 카메라
- **패링 성공음**: 금속 청음(절차 클립 — AttackSoundLayerManager `PlayArrowBlock` 선례 확장: 하이 하모닉+쇼트 디케이)
- **창 진입음**: 짧은 웜 틱(옵션)
- **카메라**: 성공 순간 미세 풀-인(CombatCameraEffects 킬 이펙트 계열 재사용, 슬로모와 중복하지 않게 짧게)
- 테스트: 사운드 배선 소스 계약

### Phase P4 — 슬로모 구분화(선택)
- 현행 패링 슬로모가 크리티컬 슬로모와 동일 파라미터 — 패링 전용 프리셋(진입 더 빠르게, 배경 채도 소폭 하강 — IndoorPostProcess 톤 상한 계약 내)으로 "패링 순간" 서명
- 테스트: BulletTime 파라미터 계약 갱신

## 3. Acceptance Criteria

- AC1: V 입력 → 즉시 창 아우라(0.45s) 표시, 창 내 피격 시 확장링+별섬광+스파크+청음 동시 발화
- AC2: FX는 검증된 공용 자산 재사용(공유 머티리얼 변이 금지, 그림자 미방출, 프리미티브 금지 규약 준수)
- AC3: 실패 피격은 기존 HitReaction 유지(회귀 없음), 창 타이밍/판정 로직 무변경
- AC4: Play 캡처로 판정(사용자 게이트)

## 4. 영향 파일(예상)

- 신규: `ParryImpactFX.cs`(P1 스폰 오케스트레이터) / `ParryAuraUTK` 또는 월드 링 재사용(P2)
- 수정: `PlayerCombat`(TryStartParry/TryParry에 FX 훅), `AttackSoundLayerManager`(PlayParryBlock 신설)
- 계약: 신규 `ParryFXTests` + BulletTime/카메라 계약 갱신
