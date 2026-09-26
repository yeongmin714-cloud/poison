# 화살 방패 막기 (Arrow Shield Block) — 젤다 화살예시 적용

> 기준: `Screenshots/젤다 화살예시.mp4` — BotW에서 방패 NPC를 향해 화살을 쏘면, 방패가 막아낸다.
> 시각(영상 실측): 명중 순간 **청록/흰 별섬광**(sharp starburst) + **확장하는 흰 원형 링**(충격파) + **노란 스파크**(튀김), 화살은 방패에 막혀 **피해 없음**.
> 현행: `ArrowProjectile.OnTriggerEnter`가 `Guard`/`Enemy`/`Monster` 등 모든 타겟에 무조건 `TakeDamage` — **방패 막기 없음**.

## 목표
방패를 착용한 적 병사(`GuardPlaceholder.ShieldItem != null`)에게 화살이 맞으면, 피해를 주는 대신 **방패로 막는 연출**(청/흰 별섬광 + 확장 링 + 노랑 스파크 + 화살 소멸/부딪침)을 보여주고 **무피해** 처리한다. 이는 젤다 화살예시의 핵심이고, 현재 완전히 부재한 메커니즘이다.

## 재사용 자산 (기존, 재구현 금지)
- `ArrowProjectile.SpawnStarFlare(pos)` — StarFlare.png Quad 빌보드 0.4s 스케일업+페이드. (현재는 "적 명중 소멸"용 — 방패 막기에도 적합하나 색/형태가 명중과 겹침)
- `ShockwaveRingFX.Spawn(pos, maxRadius, color, duration)` — 지면 확장 링(발모양 충격파). ⚠ 지면에 평평한 링이라 탑다운 뷰에서 잘 보임.
- `Resources.Load<Texture2D>("UI/StarFlare")` / `"UI/shadow_glow"` — 고품질 베이크 텍스처.
- `CombatVFXController` 히트플래시 레지스트리.

## 구현 (Phase 단위, 각 단계에서 컴파일 확인)

### Phase A — 방패 막기 판정 + 무피해
- `ArrowProjectile.OnTriggerEnter`: `isTarget` 분기에서 먼저 `GetComponentInParent<GuardPlaceholder>` → `gp != null && gp.IsAlive && gp.ShieldItem != null`이면 **방패 막기 경로**로 분기.
  - `TakeDamage`/데미지 숫자/크리틱/히트스톱 **전부 스킵** (무피해).
  - `AttackSoundLayerManager`로 방패 막기 사운드(기존 토큰 재활용·폴백 조용히).
  - 화살은 소멸 처리(`Destroy(gameObject)`) — 부딪히고 떨어지는 연출 대신 즉각 소멸 + 섬광(기존 지면 박힘과 구분).

### Phase B — 방패 막기 VFX (고품질)
- 신규 `ArrowShieldBlockFX.cs` (static): 명중점에
  1. **확장 링** — `ShockwaveRingFX.Spawn(hitPos+up0.1, 최대0.9m, 흰·알파0.9, 0.25s)` (탑다운에서 확장 원형 충격파).
  2. **청/흰 별섬광** — StarFlare Quad를 `SpawnStarFlare`와 다른 밝은 흰색·짧은 0.2s 스케일업으로 재사용(모듈 안에서 새 정적 함수 또는 파라미터로 색/시간 분리).
  3. **노랑 스파크 파티클** — shadow_glow Unlit 파티클 4~6입자 방사(화살 머즐 퍼프 패턴 재사용, 색 노랑).
- ⚠ 파티클 규칙(46차) 준수 — 프리미티브+Bake 텍스처, MAGENTA 셰이더 함정 회피(`FXPalette`/검증된 `Particles/Unlit` 폴백).
- 배선: `ArrowProjectile` Phase A의 방패 막기 경로에서 `ArrowShieldBlockFX.Play(pos)` 호출.

### Phase C — AI 방패병사가 플레이어 화살에 반응 (선택/보류)
- 방패를 든 게이트/일반 병사가 화살에 맞으면 짧게 방패 앞세우는 애니(위험 animate) → SoldierShield_AC에 Block 상태 없음(Idle/Move/Attack/Hit/Death만) → **별도 클립 없음으로 이번엔 제외**(VFX만). 영상의 방패 막기 시각(섬광+링+스파크) 핵심은 Phase A+B로 달성.

## 검증 (각 Phase)
1. 배치컴파일 `grep -cE "error CS" == 0`.
2. 정적: 괄호 균형·화이트스페이스 `git diff --check`.
3. Play: 방패 들 병사 있는 캠프/테스트 씬에서 활로 화살 → 방패병사는 피가 줄지 않고(무피해) 섬광+링+노랑 스파크 표시. 방패 없는 병사/몬스터는 기존대로 데미지(회귀 0).
   - ⚠ 테스트 씬에서 방패 든 적: `GuardEquipmentSpawner.SpawnEquipment`가 ShieldItem 25% 확률 지급 → 병사 여럿 스폰하면 방패병사 포함.
4. ROADMAP 체크 + QAPROGRESS + git commit/push (사용자 3중 저장 규칙).

## ⚠ 함정
- `GuardPlaceholder.ShieldItem`은 public 프로퍼티(1255행) — 직접 읽기 OK. `IsAlive`(497~498행)도 public.
- 방패 막기 판정이 `RecruitedSoldier`(아군)에도 걸리면 안 됨 — 아군은 기존 관통 유지. `IsAlly`(IsRecruited)면 막기 체크에서 제외.
- 파티클/섬광 과장 금지(no over-density) — 젤다는 짧고 또렷(1~2프레임)한 스파크.
- 데미지 숫자(골드)는 방패 막기 시 **표시 금지** — 막힘은 무피해라는 명확한 시각 신호.
- VFX 정적 호출(`ArrowShieldBlockFX.Play` 타입 한정, 인스턴스 아님) — CS0176 회피.