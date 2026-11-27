# 156차: 이계 카드 UI + 무기 16종 + 스킬 8종 + 패시브 12·특수 4 + 플레이스홀더 VFX 계획서

> **작성일**: 2026-11-09 (11-08 세션 타임아웃 0착지 → 전면 재시작)
> **범위**: 카드 풀 46장 + 3택1 플레이스홀더 UTK UI + 훅 배선 + 플레이스홀더 VFX
> **계약**: 전부 런 한정(귀환 시 소멸 — 기존 증강과 동일 계약) · 시드 기반 결정론(Fisher–Yates/가중치 추출 재사용) · 키 계약 무변경(스킬 전부 자동발동)
> **Figma 교체 전제**: 데이터 계약 고정, UI는 플레이스홀더(UTK 자가부트 — FishingUTK 선례) → 후일 UXML/USS 교체만

## 0. 실측 좌표 (2026-11-09 grep 확정)

| 훅 | 파일:라인 | 내용 |
|---|---|---|
| 런 시작 | AfterlifeSurvivalSystem.cs:118 | `AfterlifeAugmentSystem.BeginRun(_deathRing, _deathTerritoryIndex);` — 직후 Card.BeginRun 추가 |
| 런 Tick | AfterlifeSurvivalSystem.cs:443 | `AfterlifeAugmentSystem.Tick(Elapsed, _targetSec, _rngSeed, Time.unscaledDeltaTime);` — 직후 Card.Tick 추가 |
| 런 종료 3곳 | AfterlifeSurvivalSystem.cs:290 / 365 / 419 | `AfterlifeAugmentSystem.EndRun();` 직후마다 Card.EndRun() 추가 |
| 웨이브 | AfterlifeSurvivalSystem.cs:143-149 | CheckWave: `_nextWaveAt += WAVE_INTERVAL_SEC;` → Passive.WaveIntervalSeconds로 교체 + 파ardon 소비 게이트 |
| 처치 | AfterlifeSurvivalSystem.cs:232 | NotifyEnemyKilled — Passive.OnEnemyKilled 연결 |
| 오브 흡수 | AfterlifeSurvivalSystem.cs:240 | NotifyOrbAbsorbed(type, baseValue) — 오브 가치 배율 적용점 |
| 정산 | AfterlifeSurvivalSystem.cs:284 | FinishRun — EarlyReturnPending 시 수확 ×0.5 감면 |
| 탈주 | AfterlifeSurvivalSystem.cs:260 | Surrender — SURRENDER_HARVEST_RATIO(0.5f) → Passive.SurrenderHarvestRatio |
| 퍼머데스 | AfterlifeSurvivalSystem.cs:359 | ExecutePermadeath — 진입 2nd_Life 소비 게이트 |
| 귀환 문 | AfterlifeSurvivalSystem.cs:457 | OpenReturnDoor — sp_realm_gate용 ForceReturnDoor 신설 |
| 적 목록 | AfterlifeSurvivalSystem.cs:53 | `_aliveEnemies` private — `public static IReadOnlyList<AfterlifeEnemy> AliveEnemies => _aliveEnemies;` 액세서 추가 |
| 적 이동 | AfterlifeEnemy.cs:124 | `transform.position += dir * _moveSpeed * Time.deltaTime;` → ×Skill.EnemySlowFactor ×Passive.EnemyMoveFactor |
| 적 피해 | AfterlifeEnemy.cs:228 | TakeDamage(amount, dir, weaponType) — Weapon.DamageMult ×Passive.DamageMult ×사신 즉사 판정 진입 |
| 공격 쿨다운 | PlayerCombat.cs:174 | CanAttack `/ AfterlifeAugmentSystem.AttackSpeedMult` → 추가 `/ AfterlifeWeaponSystem.CooldownFactor` |
| 현재 무기 | PlayerCombat.cs:43/184 | `_currentWeapon` private + SetWeapon public — `public WeaponData CurrentWeapon => _currentWeapon;` 액세서 추가(런 진입 무기 캡처용) |
| 피해 진입 | PlayerHealth.cs:119 / 202 | TakeDamage 1인자/3인자 — 진입 첫 줄 강철 방패(전면 차단 1회) → 감쇠 ×Passive.IncomingDamageFactor |
| 구르기 | PlayerMovement.cs(Systems, L361 HandleRoll) | 롤 거리 필드 → ×Passive.RollDistanceFactor (L47 Roll Settings 헤더 근처 필드, HandleRoll 본문 scoped read) |
| PERFECT 히트스톱 | PlayerCombat.cs:1004 | `RequestHitStop(grade == Perfect ? 0.07f : 0.03f, 0.12f)` → Perfect 분기 `0.07f + Passive.ExtraPerfectHitStop` |
| 텔레그래프 | AnimalAI.cs:838 | `private const float TelegraphWindupSeconds = 0.5f;` → `static float => 0.5f + Passive.TelegraphBonusSeconds` (L858/867/876 사용처 시그니처 무변경) |
| 오브 레지스트리 | AfterlifeOrb.cs:27/36 | `_live` private — `public static IReadOnlyList<AfterlifeOrb> Live => _live;` 액세서 추가 |
| 히트스톱 API | HitStopManager.cs:45/94 | RequestHitStop(duration, scale) — 충격파 스킬 재사용 |
| 링 FX | ShockwaveRingFX.cs:39/132 | Spawn(pos, maxRadius, color, duration) / SpawnDirectionalSpark — 스킬 VFX 전용 (벡터메시 금지 계약 준수) |
| UI 자가부트 | FishingUTK.cs:22/453-478 | UTKWindowBase 상속 + static Ensure()/Open()/Toggle() + GitHubDark 팔레트 + Updater MonoBehaviour — 동일 패턴 복제 |
| 테스트 선례 | Tests/EditMode/AfterlifeAugmentOrbTests.cs | namespace/using/어트리뷰트 규약 복제 소스 |
| 자동 화살 | PlayerCombat.cs:1374 | `ArrowManager.Instance.TryShootArrow(origin, dir, damage, power, ...)` — 혼백 활 자동발사 재사용 |

## 1. 데이터 계약 (고정)

```csharp
namespace ProjectName.Systems
public enum CardKind { Weapon, WeaponUpgrade, Skill, Passive, Special }
public enum CardRarity { Common, Rare, Epic, Legendary }
public sealed class CardDef {
    public string Id; public string Name; public string Desc;
    public CardKind Kind; public CardRarity Rarity; public int MaxStacks;
}
```

## 2. 카탈로그 (CardCatalog.cs — 값 확정) — [2026-11-09 정합] 신규 40장 + 기존 증강 6종 = UI 표시 풀 46장 (무기 4+강화 8+스택II 4+스킬 8+패시브 12+특수 4)

**무기 획득 4**: w_saw_dagger(톱니 단검/Common/쿨다운×0.7·데미지×0.8) · w_grave_sword(묘지기의 검/Common/데미지×1.1+넉백) · w_pierce_spear(관통의 창/Rare/데미지×1.25·쿨다운×1.15) · w_soul_bow(혼백 활/Epic/6초 자동발사 ×0.8)
**무기 강화 12**: 각 무기 Lv2/Lv3 — 단검(쿨×0.85/데×2.0 연속타), 검(×1.3/×1.5), 창(×1.4/전방 6m 관통 60%), 활(2화살/3화살)
**스킬 8(자동발동)**: s_spin_slash(Common/8s/3.5m 15) · s_charge(Common/12s/최근접 25) · s_shockwave(Rare/15s/6m 20+넉백+히트스톱0.03) · s_holy_cross(Rare/20s/5m 18+힐10) · s_fire_zone(Rare/15s/3초 2.5m 6dmg×0.5s틱) · s_time_warp(Epic/25s/적 이속 0.4× 2초) · s_tentacle(Epic/12s/최원거리 오브 끌어당김+경로 1.5m 8dmg) · s_bone_shield(Rare/10s/1회 완전 방어)
**패시브 12**: p_vampire_fang(C/처치힐2) · p_skull_ring(C/10연속→오브×2) · p_emperor_seal(C/데미지+10%/스택 max5) · p_steel_shield(C/받는피해×0.92/스택 max3) · p_death_scythe(R/5% 즉사—정예 5% 가산) · p_tombstone(C/최대HP+50/스택) · p_time_sand(R/웨이브 간격 120→135s) · p_soul_jar(C/오브 가치×1.2) · p_shadow_boots(R/구르기 거리×1.2) · p_perfect_mark(R/PERFECT 히트스톱+0.02s) · p_chain_seal(C/적 이속×0.9/스택 max3) · p_afterlife_talisman(R/텔레그래프+0.1s)
**특수 4(Legendary)**: sp_second_life(사망 시 1회 부활 50%) · sp_pledge_receipt(탈주 수확 1.0) · sp_wave_pardon(다음 웨이브 무시 1회) · sp_realm_gate(즉시 귀환 문+수확 ×0.5)

## 3. 제시기 계약 (AfterlifeCardSystem)

- 마크 300/600(증강과 동일) — 단 **증강 오퍼 미해결 중엔 카드 오퍼 보류**(UI 단일 표시 보장 — AugmentSystem.OnOfferResolved 후 발화)
- 가중치: 기본 C70/R22/E6/L2, Epic/Legendary 링 배율 {1:0.5, 2:1, 3:2, 4:3} — 가중 추출은 `PickWeightedIndex(System.Random, int[])` 순수 함수
- 자격 필터: 무기 슬롯 4 만석→무기 배제 · 강화는 보유 무기+Lv<3만 · 비스택 카드 보유 중→배제 · 특수 1회성 보유 중→배제
- 중복 제시 금지 · 20초 폴백=결정론 index 0 자동 선택(증강 동일)
- BeginRun/EndRun — 소유/보류/배율 전부 소멸(증강 EndRun 좌표 3곳 동일 배선)
- 모든 Apply 경로 null-guard(PlayerHealth/PlayerCombat Instance null 시 no-op — EditMode 안전)

## 4. Phase 분할 (위임 순서 — 각 위임은 "Unity 배치 절대 금지")

- **Phase 1+2**: CardDef · CardCatalog · AfterlifeCardSystem · AfterlifeCardSystemTests (신규 4파일, 기존 파일 0수정)
- **Phase 3**: AfterlifeCardUIUTK (신규 1파일 — FishingUTK 패턴, 증강+카드 양쪽 구독, 클릭 라우팅)
- **Phase 4**: AfterlifeWeaponSystem + PlayerCombat(174/184근처) + AfterlifeEnemy(124/228) 패치 + 훅 계약테스트
- **Phase 5**: AfterlifeSkillSystem + AfterlifePassiveSystem + SurvivalSystem 패치 10곳 + PlayerHealth/PlayerMovement/AnimalAI/Orb 패치 (VFX=ShockwaveRingFX 호출 포함)
- **Phase 6(QA)**: 컴파일 error CS 0 + EditMode 전수 + baseline 1:1 — 별도 QA 서브

## 5. 승인/진행

- Play 판정·커밋: 별도 승인 게이트 (기존 무번호 1~6 누적분 포함)
- 진행: Phase 1+2 → 3 → 4 → 5 → QA 순차 단발 위임 (155차 "코드 전용 위임" 패턴 — 서브는 편집+grep 자체검증만)
- VFX는 기존 ShockwaveRingFX 프리미티브 재사용(신규 프리팹 0 — YAML 리스크 제거, 벡터메시 금지 계약 준수)
