# 무번호 180 — 몬스터 스킬 Intermediate+ 확장 + 보유 VFX 배선 계획

## 사용자 확정
"몇몇 상위 몬스터는 스킬을 사용할 수 있게" + "에셋중에 스킬 관련 vfx 에셋들이 있으니" + "Intermediate도 적용해줘"

## 현황 실측 (조사 근거)
- `MonsterSkillSystem`은 **이미 존재·배선**(810줄) — TryAttack에서 50% 확률로 스킬 우선 사용(L791).
- 그러나 ①**스킬 커버리지**: Intermediate+ 14종 중 스킬 없는 10종(Intermediate: electric_porcupine·swamp_croc·forest_spirit·wild_troll / Advanced: ogre·banshee·griffin·minotaur·salamander·shadow_assassin) ②**VFX 전무**(46차 "절차 파티클 제외"로 눈에 안 보임 — Fireball=무광 구체).
- 보유 스킬 VFX 에셋(Resources 실존): `FX/Meteorite/VFX/`(캐스트 시퀀스 완비)·`FX/Selection/MagicCircle2.prefab`·`FX/Impact/MagicHit·BasicHit·BasicHit2.prefab`·`FX/Death/DeathSmoke.prefab`·`FX/Selection/Buff.prefab`.
- 기술 선례: `MeteoriteVFXRunner`(78차, .vfx 지연로드+캐시+예산 가드) — "절차 금지·에셋 경로" 승인 경로.

## 설계 원칙 (기존 계약 무손상)
1. **게임밸런스 동결**: 스킬 데미지·쿨다운·선택확률(50% Random)·즉시 판정 타이밍 기존 유지 — 이번 라운드는 **커버리지 확장+VFX 배선만** (텔레그래프 재설계 없음 — 회피/패막 판정은 Play 육안 후 별도).
2. **실행부 재사용**: 신규 스킬 메커니즘 0 — 기존 Execute{Fireball,Charge,Leap,Teleport,AoEExplosion,PoisonSting,Debuff} 재사용.
3. **VFX = 에셋 경로만**: 절차 파티클 금지(46차)·벡터메시 금지(84차)·결정론(Unscaled/Random 조작 없음 — 기존 Random 선택 유지)·패막 블록리스트 무수정("aoe"/기존 소스 태그 유지 — 기존 판정 보존).

## 스킬 배정 (Intermediate+ 전수 — 유저 확정 "Intermediate도")
| 몬스터 | 티어 | 스킬 (기존 enum 재사용) | 설명 대응 |
|---|---|---|---|
| slime | 중반 | 없음(코드 분열) | 기존 |
| stone_golem | 중반 | AoEExplosion | 기존 |
| fire_lizard | 중반 | Fireball | 기존 |
| **electric_porcupine** | 중반 | AoEExplosion(4m, 1.2) | "전기 가시 접근 감전" |
| **swamp_croc** | 중반 | Leap(6m, 1.8) | "숨어있다가 덤벼든다" |
| **forest_spirit** | 중반 | Debuff(이동감소) | "빛나는 이끼" |
| **wild_troll** | 중반 | Charge(8m, 1.5) | "강력한 재생/힘" |
| **ogre** | 후반 | AoEExplosion(5m, 2) | "강력한 타격" |
| **banshee** | 후반 | Debuff | "영혼 공격" |
| **griffin** | 후반 | Leap(8m, 2) | "하늘에서 덤벼든다" |
| **minotaur** | 후반 | Charge(9m, 2) | "강력한 돌진" |
| **salamander** | 후반 | Fireball(10m, 1.8) | "용의 혈통 화염" |
| **shadow_assassin** | 후반 | Teleport | "은신 후 기습" |
| manticore | 후반 | 기존 3종 유지 | — |

## VFX 배선 매핑 (에셋 전부 Resources 실존)
| 스킬 | 발화 시각 | 에셋/경로 |
|---|---|---|
| Fireball | 캐스트: 발밑 마법진 페이드인(0.5s, 시각만) / 히트: 임팩트 | MagicCircle2.prefab + MagicHit.prefab |
| AoEExplosion | 폭발 지점 풀 시퀀스(임팩트+플래시+2차 파장) | `MeteoriteVFXRunner` 신규 public `PlayImpactSequence(pos)` 추가(additive — 기존 Burst 무수정, 01_Anticipation 포함X로 판정-시각 정합 유지) |
| Charge | 히트 시 소소한 임팩트 | BasicHit.prefab |
| Leap | 착지 시 임팩트 | BasicHit.prefab |
| Teleport | 사라짐+등장 스모크 | DeathSmoke.prefab ×2 |
| PoisonSting | 피격점 임팩트 | BasicHit2.prefab |
| Debuff | 대상 발밑 마법진(감쇠) | MagicCircle2.prefab |
| Heal(드라큘라) | 자기 발밑 버프 링 | Buff.prefab |
- 공용: `MonsterSkillVfx` 정적 헬퍼(프리팹 지연로드+캐시+생성+수명 자동 소멸, VFX 경로 문자열 단일소스).

## 구현 단계 (위임 1회 — 정확 파일)
- **P1**: `MonsterSkillSystem.cs` — _skillMap 10종 추가(위 표) + 각 Execute에 VFX 호출 삽입(로직 무수정 — 데미지/쿨다운 기존 그대로) + `MonsterSkillVfx` 헬퍼(같은 파일 private static 또는 신규 파일) + `MeteoriteVFXRunner.PlayImpactSequence` additive public.
- **P2**: 신규 `MonsterSkillVfxTests`(EditMode) — ①Intermediate+ 전 id 스킬 커버리지 소스계약 ②VFX 경로 문자열 실재(Resources 존재 확인 계약) ③절차 파티클 금지(가드: 새 코드에 ParticleSystem 수동 생성 부재) ④벡터메시 금지 ⑤기존 밸런스 상수 무수정 핀(50% 선택·기존 쿨다운 값).
- **P3 (부모 QA)**: 컴파일 CS 0 → 집중 신규테스트 → 전수 EditMode 20기준선 1:1 대조 → QAPROGRESS 기록.

## 대기 게이트
Play 육안(스킬 발동 시 VFX 확인 — 특히 Fireball 마법진/히트, 폭발 임팩트 시퀀스) · 커밋 별도 승인.
