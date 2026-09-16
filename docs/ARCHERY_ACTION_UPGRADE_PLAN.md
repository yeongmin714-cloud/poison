# 화살 액션 고품질 업그레이드 계획 (ARCHERY_ACTION_UPGRADE_PLAN)

> **For Hermes:** 이 계획은 검증된 실측 사실 위에 세운다(모든 경로 검증됨, 2026-09-17). 실행은 Phase 단위 — 각 Phase 완료 후 배치컴파일 `error CS=0` + 괄호균형 + ROADMAP 체크 후 다음 Phase.
> 계획서 위치는 `.hermes/plans/`가 아니라 `/mnt/c/Unity/code/docs/` (선례 `ATTACK_FEEL_UPGRADE_PLAN.md`).

**Goal:** 현재 "직발사 실린더 + 트레일" 수준의 궁술을, **드로→릴리즈 모션 + 차지 강도 + 화살 모델 + 명중 피격 FEEL + 4레이어 사운드**로 끌어올려 다른 3D RPG 수준의 고품질 화살 액션으로 만든다.

**Architecture:** 근접 공격과 동일한 5레이어 모델(모션 → 투사체 → 피격반응 → 카메라/타임 → 사운드)을 궁술에 적용하되, 기존 시스템(`HitReactionDriver`, `HitStopManager`, `AttackSoundLayerManager`, `ActionFeel`, `WeaponSwingTrail`, `PlayerCombat.TryBowShot`)을 **재구현하지 않고 재사용**한다. 투사체는 `ArrowProjectile` 유지, 발사 매커니즘은 `PlayerCombat`+`ArrowManager` 확장.

**Tech Stack:** Unity 6000 (Poison 커스텀 엔진), `using UnityEngine;` + `namespace ProjectName.Systems`, `ProjectName.Core.WeaponType.Bow`. 검증된 애닉 소스: `Player_AC.controller`(ArcheryShot/BowIdle/BowRunF/BowAimedF 상태 존재) + `Assets/DoubleL/{FBX Unity,FBX Unreal}/Bow/Attack A|B/*Bow_Attack_?_1_All.fbx` 2클립 보유.

---

## 검증된 현재 실태 (Phase 0 — 2026-09-17 직접 확인)

| 항목 | 현재 상태 | 파일 |
|:--|:--|:--|
| 화살 발사체 | 얇은 실린더 프리미티브 + TrailRenderer, 중력 포물선, LookRotation 수리됨(축 정렬 X+90°) | `ArrowProjectile.cs` |
| 탄약 소모 | 일반/강화/마법 3티어, 소모 우선순위 마법>강화>일반, SpawnPoint(손/활) | `ArrowManager.cs` |
| 화살 데이터 | 3티어 데미지 보너스+트레일 색 (마법=보라 `0.7,0.2,0.9` — 색값, MAGENTA 셰이더 함정 아님) | `ArrowData.cs` |
| 발사 경로 | 좌클릭 직발사, 커서 레이 방향, 화살없으면 근접폴백 없음(return) | `PlayerCombat.TryBowShot` |
| 발사 애니 | `ArcheryShot` 트리거 → 단일 클립, Speed 0 홀드 0.6s, BowEnter 스탠스 | `HumanoidClipDriver.cs` |
| IDamageable 명중 | `OnTriggerEnter` allowlist(Enemy/Monster/Guard/DraculaLord), 아군 RecruitedSoldier 관통, 2초 후 소멸 | `ArrowProjectile.cs` |
| 활 상태 클립 | BowIdle/BowRunF/BowAimedF 있음(Meshy 진행 클립), **활 공격 클립은 ArcheryShot 단일** | `Player_AC.controller` |
| 보유 공격 클립 | **DoubleL `Bow_Attack_A_1_All.fbx` + `Bow_Attack_B_1_All.fbx` 2개** — 현 ArcheryShot에 미활용 | `Assets/DoubleL/FBX Unity/Bow/Attack A|B/` |

**핵심 갭 (왜 지금 "촌스러운"가):**
1. **발사 모션이 단일 클립** — 드로(당김)→릴리즈 2단계 feel 없음. DoubleL A/B 2클립이 놀고 있음.
2. **차지(활시위 힘) 매커니즘 없음** — 좌클릭 즉시 직발사. 근접은 우클릭 홀드차지가 있는데 활은 파워 개념 부재 → 어렵고 무게감 없음.
3. **발사체가 실린더** — 화살 모델(GLb)/헤드/플레처 없음. 관통·박힘(스틱) 없음.
4. **명중 FEEL 절반** — IDamageable.TakeDamage가 HitReactionDriver/힛스톱 연동되지만 "화살이 박히는" 장면 연출 없음.
5. **사운드 부족** — `attack_swing_bow`/`attack_hit_bow`만 있음. 드로 스트레치·릴리즈 톡·비행 휘파람·명중 서브베이스 4레이어 부재.

---

## Phase A — 화살 발사 매커니즘: 드로→릴리즈 차지 (발사 감각)

**목표:** 활을 "좌클릭 즉발"에서 "우클릭 드로(당김) + 좌클릭 릴리즈" 또는 "좌클릭 홀드차지 → 해제 릴리즈"로 바꿔 무게감·조준감 확보.

- **A1 (매커니즘 설계)** — 기존 근접 우클릭 차지(`PlayerCombat._charging/_chargeHeldTime`, 검/창/Fist 전용)를 **활은 우클릭 드로/해제 릴리즈**로 확장. 근접과 동일 입력 우클릭 홀드를 재사용하되 활에서는 차지 누적 대신 **드로 진전 `drawGraph` 0→1**로 보고, 해제 시 발사.
  - 파일: `PlayerCombat.cs` (TryAttack Bow 분기 ~392, ReleaseCharge ~248, 우클릭 게이트 ~181)
- **A2 (파워→투사체 전달)** — `ArrowManager.TryShootArrow`에 `power`(0.5~1.0) 파라미터 오버로드 추가. 속도 `_arrowSpeed * (0.7 + 0.5*power)`, 데미지 `baseDamage + arrowData.damageBonus + Round(power*8)` 가변. 드로 짧게 당기면 약하게/가깝게, 끝까지 당기면 강하게.
  - 파일: `ArrowManager.cs` TryShootArrow 3-arg(기존 시그니처 보존, 레거시 2-arg 위임 — 회귀 안전)
- **A3 (게이트)** — 드로 중 이동/공격금지, `IsBowEquipped` + 우클릭 홀드 진입 시 `_anim.SetFloat("Draw", drawGraph)`로 드로 애니 연동 계획(Phase B). `_recoilActive`-류 플래그 게이트로 릴리즈 코루틴 중복 방지.

## Phase B — 발사 모션: 드로→릴리즈 2단계 (DoubleL A/B 클립 활용)

**목표:** 단일 ArcheryShot 클립을 "드로(당김)" + "릴리즈(쏘기)" 2단계로 분리해 활 액션을 살린다.

- **B1 (컨트롤러 실측 파이썬 파싱 먼저 — 규칙 16.1)** — `scripts/parse_animator_controller.py Player_AC.controller`로 ArcheryShot 상태→클립 guid, BowIdle/BowRunF 전이, Draw 파라미터 유무 확인. **절대 손으로 YAML 편집 금지.**
- **B2 (에디터 스크립트 신설/확장)** — 69차 `BowClipWiring.cs` 선례를 복제해 **`ArcheryClipWiring.cs`** 추가: 기존 ArcheryShot 상태를 유지하고, 드로 상태(`BowDraw`) 신설 → `DoubleL Bow_Attack_A_1_All.fbx`(드로·당김 절반)를 `BowDraw` motion으로, 릴리즈 후반/쏘기는 기존 clip 또는 `Bow_Attack_B_1_All.fbx`로 배선. `Draw` 트리거(우클릭 홀드) + `ArcheryShot`(해제 발사) 전이 추가(멱등).
  - 파일: `Assets/Editor/ArcheryClipWiring.cs` (신규) + `.meta`
- **B3 (드라이버 배선)** — `HumanoidClipDriver`: 우클릭 홀드 시 `_anim.SetTrigger("BowDraw")`/`SetFloat("Draw", t)`, 해제 즉시 `TriggerBowShot()`(기존) + Speed 0 홀드 보존. 드로 중 Speed 0 추가(발사 중뿐 아니라 당기는 중에도 정지).
  - 파일: `HumanoidClipDriver.cs` (TriggerBowShot ~95, attackStateHold ~466)

## Phase C — 화살 발사체 시각/물리: 실린더 → 화살 모델 + 박힘/관통

**목표:** 투사체를 "촌스러운 실린더"에서 실제 화살(샤프트+헤드+플레처)로, 명중 시 "박히는" 장면으로.

- **C1 (화살 모델)** — 실린더 프리미티브 대신 **원뿔+실린더 조합**(프리미티브 2개 조립: 샤프트 실린더 + 헤드 콘 + 플레처) 또는 시중 화살 GLB 프리팹(선례 46차 asset-only doctrine — 파티클 금지 규칙과 동일하게 **절차 프리미티브 조립은 허용? 검토**: 헤드는 뾰족해야 하므로 콘 필수). 헤드 방향 = 진행방향(+Z LookRotation, 이미 수리됨). 색 = ArrowData.trailColor(일반 갈색/강화 은/마법 보라).
  - 파일: `ArrowProjectile.Spawn` ~38
- **C2 (물리 감각)** — `useGravity=true` 유지(포물선), 현재 speed 45 직선에 가까움. 사거리 내린 장거리 드롭 + **비행 마찰** 추가. 발사 시 발사체 회전/흔들림(flight wobble)은 프리즈 회전으로 단순화 유지(과잉 아닌 YAGNI).
- **C3 (박힘·스틱)** — `OnTriggerEnter` 명중 시 `Destroy` 대신 **타겟에 화살 부착 잔존**: 명중 프레임에 작은 "화살 자국" 인스턴스를 `LastHitPoint`에 남기고 5s 후 제거(다운 삭제 시 함께). 관통 로직은 아군 RecruitedSoldier에만 유지(기존).
  - 파일: `ArrowProjectile.cs` OnTriggerEnter ~96

## Phase D — 명중/피격 FEEL: 히트스톱 + 반응 + 넘버 + 임팩트

**목표:** 화살이 적중했을 때 근접만큼 "맞았다"가 전달되도록 한다.

- **D1 (명중 체인 확인/통일)** — `IDamageable.TakeDamage(dmg, hitDir, "Arrow")`가 이미 `CombatFXGate`(PlayImpact+넘버+카메라+임팩트사운드)와 `HitReactionDriver`(플린치)를 타는지 **grep 확인**. 안 타면 `PlayerCombat` 화살 경유로 `CombatFXGate.PlayHitFX(target, LastHitPoint,...)` 배선(타겟 pivot 부유 함정 — 46차 후속② LastHitPoint 주입).
  - 파일: `ArrowProjectile.cs` → `CombatFXGate.cs` (호출 추가), receiver 직접 FX 호출 여부 전수 grep
- **D2 (화살 특화 임팩트)** — 직격 명중 시 근접과 다른 "날카로운" 히트스톱(`HitStopManager` DurationOf에 Bow 소구간 이미 .030 — 재사용) + 화살 박힘 순간 `ShockwaveRingFX`는 금지(46차 컷), 대신 작은 명중 번쩍(PlayHitFlash refcount 경로 — 54차, 재사용) + 임팩트 사운드.
  - 파일: `HitStopManager.cs`(이미 존재 확인), `CombatFXGate.cs`

## Phase E — 사운드 4레이어: 드로/릴리즈/휘파람/임팩트

**목표:** 55차(16.4 ②) AttackSoundLayerManager 4-AudioSource 레이어를 화살에 적용 — 사운드가 최대 레버.

- **E1 (사운드 토큰)** — 기존 `attack_swing_bow`(드로 스트레치·당김) / `attack_hit_bow`(릴리즈·시위 톡) 재활용 + 신규 `arrow_whistle`(비행 휘파람 — Fmod 피치 스윕) / `arrow_impact`(명중 서브베이스+톡). 클립 없으면 절차 70Hz 서브베이스 폴백(16.4 ②).
- **E2 (발사 시점 분리)** — 드로 시작 시 `attack_swing_bow`(0.3s 스트레치), 릴리즈 시 `attack_hit_bow`(톡), 화살 비행 중 `arrow_whistle`(발사체 수명 동안, 발사체에 AudioSource 또는 ArrowManager 추적), 명중 시 `arrow_impact`.
  - 파일: `AttackSoundLayerManager.cs` (확장, 존재 확인), `ArrowProjectile.cs`, `PlayerCombat.cs`(PlayWeaponSwingSound 시그니처 보존)

## Phase F — 조준/시각 보조 (옵션, 남는 시간에)

- 크로스헤어 조준 UI(활 장착 시만 표시), 드로 강도 게이지(우클릭 홀드 시 원형 게이지), 궤적 예측점 표시. UI는 `UIStyleManager`+`UIFont`+`_uiScale` 관례(메모리) 준수.

---

## 재사용할 기존 시스템 (재구현 금지 — 먼저 grep 확인)

- `HitReactionDriver.Apply` (플린치, 53차 상향값) — `CombatFXGate` 배선방식 16.1 Phase C
- `HitStopManager.RequestHitStop(WeaponType, scale)` / `DurationOf` (Bow .030 이미)
- `AttackSoundLayerManager` (4-AudioSource 레이어, 55차)
- `ActionFeel` Balanced/HighSpec (테스트씬 고사양 전용)
- `CombatFXGate.PlayHitFX(GameObject target, Vector3 hitPos, ...)` (46차 후속② LastHitPoint 오버로드)
- `CombatVFXController.PlayHitFlash` (54차 refcount — 레거시 `Core/HitVFX` MPB 경로는 **사용 금지**)

## ⚠ 리스크/함정 (스킬 반영)

- **파티클 금지(46차)**: 화살 발사/명중에 절차 ParticleSystem 추가 금지. 임팩트는 store-asset 프리팹 or 프리미티브 조립만.
- **MAGENTA(11차)**: 런타임 PS 생성 시 `FXPalette.ApplyTo` 필수. 화살 마법 트레일 보라색은 의도 색(셰이더 오류 아님).
- **WeaponType CS0104**: 활 분기서 반드시 `ProjectName.Core.WeaponType.Bow` 정규화.
- **클립 임포트 확인**: DoubleL A/B 클립이 실제 `AnimationClip`으로 임포트됐는지 B2에서 파서/로더로 검증. 클립 없으면 BowDraw는 기존 ArcheryShot 클립 반복 사용(절차 폴백).
- **컨트롤러 손편집 금지** — 무조건 에디터 `-executeMethod` 스크립트(BowClipWiring 선례).
- **공격 클립 단일 슬라이스 한계(16.1)** — A/B 2클립이 있으면 드로/릴리즈 분리로 충분. 거기 없으면 "단일 클립을 0.3/0.7 슬라이스" 방식은 wind-up feel이 약하므로 Phase B만 드로 홀드 게이트로 무게감을 만든다.

## 검증 (각 Phase 필수)

1. 배치컴파일 — `run_batch.bat` → `grep -cE "error CS" buildlog_monster.txt`==0 + `Exiting batchmode successfully`(에디터 락이면 실측 아님 — 팔로우업 필수).
2. 괄호 균형 0 (변경 파일별 `{`/`}` , `(`/`)`).
3. 활 장착 테스트씬: 좌클릭 직발사 회귀 → 우클릭 드로→릴리즈 모션+파워 → 화살 박힘 → 명중 히트스톱+사운드 4레이어 → 영상(고해상 연속 프레임)으로 육안 판정(로그 금지).
4. ROADMAP 체크 + QAPROGRESS.md 기록 + git commit/push (사용자 3중 저장 규칙).

## 미커밋 주의

현재 git에 `BowClipWiring.cs.meta`, Meshy bow 클립 등 **미커밋 진행분**이 있다(69차 후속16 HEAD 기준). 새 Phase 착수 전 `git log` vs `git status` 확인하고 **먼저 커밋**한다.