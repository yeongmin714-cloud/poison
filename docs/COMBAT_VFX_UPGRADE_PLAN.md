# ⚔️ 전투 이펙트 고품격화 계획 (Attack/Hit VFX Upgrade)

> **요구 (사장님):** 공격·피격 이펙트를 더 고품격(高品格)으로.
> **방식:** 기존에 방대하게 구현된 VFX 시스템을 폐기하지 말고 **통합·보강**해 Test_10(경량 전투씬)에서 눈검증 후 메인씬 확산.
> 작성일: 2026-09-09

---

## 0. 조사 결과 (검증 완료)

### ✅ 이미 구현·연결된 이펙트 (재사용, 중복 금지)

| 시스템 | 파일 | 제공 이펙트 |
|:--|:--|:--|
| `CombatVFXController`(정적) | Systems/ | 히트플래시/데미지폰트/스파크/블러드/암살VFX |
| `CombatCameraEffects`(싱글톤) | Systems/ | 카메라 셰이크/HitStop/킬슬로우모션/치명타셰이크(2×) |
| `HitVFX`(정적) | Core/ | 스파크/히트플래시/월드 데미지넘버 |
| `SprayVFX`, `BloodStain` | Systems/ | 추가 스플래터/바닥 혈흔 |
| asmdef | Effects/ProjectName.Effects | 이펙트 전용 어셈블리 |

### 핵심 연결 현황
- **몬스터 피격**(`AnimalAI.TakeDamage`) — 풍부: PlayHit(셰이크) + PlayHitFlash + SpawnHitSparks + SpawnBloodSplatter + ShowDamageNumber + HitVFX(스파크/넘버) 전부 호출 ✅
- **플레이어 공격→적**(`PlayerCombat.AttackTarget`) — **약함**: 카메라(셰이크/HitStop/슬로우) ✅ + HitVFX 스파크/플래시 ✅. **그러나 ↓**
  - ❌ **데미지 숫자 미표시** (ShowDamageNumber 미호출)
  - ❌ **블러드/스파크**(CombatVFXController) 미사용
  - ❌ **치명타(백어택) 시 데미지 숫자 색 구분 없음**
  - ❌ **영주(DraculaLord)/병사(GuardPlaceholder) 피격 경로 VFX 부재** (몬스터만 풍부)
- **영주/병사 TakeDamage**: 별도 VFX 없이 IDamageable 종료만 — 몬스터와 비대칭.

### 진짜 갭 (고품격화 대상)
1. **플레이어 공격 시 피격 이펙트가 몬스터보다 약함** → 통일된 "히트 이펙트 버스트" 부재
2. **데미지 숫자/치명타 색 구분** 미흡
3. **영주/병사·몬스터·플레이어 피격 3자 비대칭** → 중앙 게이트로 통일
4. **치명타(백어택/크리) 전용 강조 연출**(큰 숫자+불꽃+셰이크2×) 없음
5. **피격 시 타겟 백래시(넉백)*2 같은 물리적 반응** — 시스템 이펙트 전용으로는 부재

---

## 설계 원칙
- **기존 VFX 3개(CombatVFXController/HitVFX/CombatCameraEffects) 재사용** — 새 파티클 엔진 금지.
- **중앙 게이트 신설**: `CombatFXGate`(정적) — 모든 공격/피격이 이곳으로 몰려 **일관된 이펙트 세트**를 발사. 내부에서 기존 Controller들을 오케스트레이션.
- **대상 타입 구분**: 몬스터(AnimalAI)/병사(Guard)/영주(Lord) → 블러드·스파크·넘버 색·사운드 세분화.
- **치명타 연출**: 백어택/크리티컬 → 대형 데미지 숫자(노랑/주황) + 불꽃 버스트 + 셰이크 2×.
- **저사양 고려**: 파티클 수 제한, GC 할당 최소화(기존 캐싱 패턴 유지), 각 이펙트 수명 짧게(0.3~1s).

---

## Phase 1 — 중앙 이펙트 게이트 `CombatFXGate`

**새 파일:** `Assets/Scripts/Effects/CombatFXGate.cs` (Effects asmdef — Core/Systems 참조 허용 확인)

**목표:** 모든 공격/피격이 단일 진입점으로 타입·치명타에 따라 알맞은 이펙트 세트를 발사.

**작업 1-1.** 정적 클래스 + 메인 진입 메서드:
```
PlayHitFX(Vector3 pos, Vector3 dir, HitFXType type, bool isCrit, float damage, Color numColor)
```
- 내부에서: `CombatVFXController.SpawnHitSparks` + `SpawnBloodSplatter`(유기체)/`PlayHitFlash` + `ShowDamageNumber`(치명타=대형 크기/색) 오케스트레이션.
- `HitFXType` enum: `Organic(몬스터/병사/영주)/Construct/None`.

**작업 1-2.** 저사양 가드: 프레임당 이펙트 생성 캡(예: 10개/프레임), 동일 프레임 다중 중복 억제.

**작업 1-3.** 치명타 연출: `isCrit=true` → `CombatCameraEffects.PlayCrit()`(이미 2×셰이크) + 대형 데미지숫자(노랑) + 스파크 1.5배.

**작업 1-4.** 컴파일(CS=0) + 기존 시스템에 연결 전 단독 컴파일.

---

## Phase 2 — 플레이어 공격 이펙트 보강

**파일:** `Assets/Scripts/Systems/PlayerCombat.cs` (`AttackTarget` 메서드, ~256-319행)

**목표:** 공격 시 몬스터 피격만큼 풍부한 이펙트 + 데미지 숫자 + 치명타.

**작업 2-1.** `AttackTarget`에 데미지 숫자 누락 추가:
- `CombatFXGate.PlayHitFX(pos, dir, Organic, isBackAttack, damage, crit?노랑:하양)`.

**작업 2-2.** 치명타(백어택, dot>0.5) 시 이미 `PlayCrit` 호출 → 여기에 크리 데미지 숫자(노랑/주황, 1.5배 크기) 연결.

**작업 2-3.** 기존 `HitVFX.PlayHitEffect/PlayHitFlash`는 유지(스파크 중복 방지 위해 CombatVFXController로 통일하거나 둘 다 허용 — 과하지 않게 저사양 주의).

**작업 2-4.** 컴파일 + 로그(`[CombatFX] crit=N 크리티컬!`).

---

## Phase 3 — 영주/병사 피격 VFX 연결

**파일:**
- `Assets/Scripts/Systems/DraculaLord.cs` (TakeDamage)
- `Assets/Scripts/Systems/GuardPlaceholder.cs` (TakeDamage)

**목표:** 영주·병사도 몬스터처럼 피격 이펙트를 발사 (3자 대칭화).

**작업 3-1.** 두 파일의 `TakeDamage`에 `CombatFXGate.PlayHitFX(pos, hitDirection, Organic, false, amount, Color.white)` 추가.
- 대상 자기 Renderer 히트플래시 포함.

**작업 3-2.** 사망 시 블러드 버스트 + `CombatCameraEffects.PlayKill()` 연결(몬스터와 동일).

**작업 3-3.** 컴파일.

---

## Phase 4 — Test_10 씬 밸런스/검증

**파일:** `Assets/Scripts/Systems/TestTerritoryCombatSetup.cs` (수정 최소)

**목표:** 저사양에서 이펙트가 잘 보이고 과하지 않은지.

**작업 4-1.** (필요시) 이펙트 시리얼라이즈드 수치 — 셰이크 강도/파티클 수 적정값 점검.

**작업 4-2.** Play 눈검증: 몬스터/병사/영주 각각 좌클릭 → 데미지 숫자+스파크+블러드+히트플래시+셰이크+치명타(백어택) 확인.

**작업 4-3.** QAPROGRESS + ROADMAP 체크 + git commit/push + 영구메모리 (3중 저장).

---

## 실행 순서
1. Phase 1(게이트 신설) → CS=0
2. Phase 2(플레이어 공격 보강) → CS=0
3. Phase 3(영주/병사 피격) → CS=0
4. Phase 4(Test_10 검증) → Play 눈검증
5. 3중 저장

> ⚠️ 각 Phase: 코드/QA는 `delegate_task` 위임(타임아웃 시 부모가 직접). 배치컴파일 CS=0 선행 → 에디터 실행 중이면 에디터 자동재컴파일 + DLL strings grep 판정.