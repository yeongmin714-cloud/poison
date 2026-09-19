# Phase O4 — 컴뱃 스탯 고도화 (C-O4-03 방어 계수 대조 결론)

> **참조:** docs/reference/openmmo/COMBAT.md (GUARD=명중 목표식) vs 포이즌 방어 체계

## 1. 두 모델 대조

| 항목 | OpenMMO (COMBAT.md) | 포이즌 (현행) |
|:-----|:--------------------|:--------------|
| 방어의 역할 | **GUARD = 명중 목표값** (히트 롤: d20+attackBonus > GUARD → 명중) | **데미지 감쇠 계수** (actualDamage = damage × 100/(100+defense)) |
| 방어 출처 | DEX 파생 (생성 시 고정, 장비로 상승) | 레벨 파생 GuardLevelSystem.CalculateDefense = (L−1)×0.5 + 장비 보너스 |
| 링 계수 | 없음 (몬스터 attackBonus = level) | RingDifficultyData.GetDefenseMultiplier: Low 0.8 / Med 1.0 / High 1.3 / VHigh 1.6 — **전쟁 전투력 계산용** (TerritoryWarManager) |

## 2. 결론 — 포이즌 모델 유지 (이식 금지)

- 두 모델은 방어의 "위치"가 다르다(명중 목표 vs 감쇠). 히트 롤 도입은 전투 체감 전면 재조정 = 파급 과다 → **YAGNI**.
- 벤치마크에서 채용한 것: ①자연 재생 공식(RegenRules) ②영주 6속성 생성(LordStatGenerator) — 방어 체계는 현행 유지.
- GUARD 개념은 영주 스탯에만 존재(LordStats.guard) — 추후 영주전투(O9 연계)에서 명중 목표로 활용 가능.

## 3. 수치 정합 확인 (C-O4-03 실측)

- PlayerHealth 감쇠식: 방어 100 → 50% 감쇠, 방어 300 → 25% — Guard Lv50 기본 방어 24.5 + 장비(3/부위) + 세트(최대 3) ≈ 35~40 → 감쇠 26~29% — 병사 전투력 계산(CalculateGuardCombatPower)과 정합.
- RingDifficultyData 계수(0.8~1.6)는 AI 전쟁(TerritoryWarManager)의 영지 방어력 배율로만 사용 — 플레이어 전투와 무관(설계 분리 정상).
- 결론: **변경 0건 — 문서화만으로 마감.**
