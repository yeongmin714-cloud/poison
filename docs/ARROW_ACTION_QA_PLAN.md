# 🏹 화살 액션 완전 고품질 계획 (Arrow Action QA Plan)

> 기준: `Screenshots/젤다 화살예시.mp4`(BotW) + 기존 `docs/ARCHERY_ACTION_UPGRADE_PLAN.md`(5레이어).
> 방식: **재구현 금지** — 기존 시스템 전부 재사용하고 '감각 갭'만 메운다. Phase 단위 배치컴파일 `error CS=0` + Play 눈검증 gate.
> UI 계열은 **항상 베이크 PNG/셰이더/VFX**(F-UI GitHub-dark 상속, poison-unity-bake-png), 프리미티브/IMGUI 금지.

---

## 🎯 현재 실태 (실측 — 2026-09-26)

| 계층 | 현재 | 파일 |
|:--|:--|:--|
| 드로→릴리즈 | 좌클릭 홀드 0.5s 차지(파워 0~1), 탭(<0.18)=캔슬, ReleaseBow→TryBowShot(power) | PlayerCombat.cs (78~296) |
| 조준 리티클 | 베이크 PNG 점/꺾쇠 4개 + UTKCircularGauge 파워링, 파워 수렴, ×N 화살수 | BowAimReticleUTK.cs (190줄) |
| 궤적 예측 | Pooled 빌보드 28점 소프트 글로우, 물리 일치(ArrowSpeed 70×(0.7+0.5p), 0.22g) | BowTrajectoryPreview.cs |
| 발사체 | GLB 모델(arrow→2→3 폴백)+절차조립(샤프트/촉/플레처), 축정렬, 박힘(wobble+먼지), 트레일(0.12s 흰→옅은청 Additive) | ArrowProjectile.cs (575줄) |
| 화살 3티어 | 일반(황갈)/강화(은백)/마법(보라) trailColor만 차이 | ArrowData.cs (73줄) |
| 사운드 | PlayBowDraw(드로 스트레치)+attack_hit_bow(임팩트) | AttackSoundLayerManager.cs |
| 명중 | 카메라 PlayHit/PlayCrit, 데미지숫자(골드), 히트스톱, 별섬광+화살소멸 | CombatCameraEffects/ArrowProjectile |
| 방패 막기 | 방금 구현 — 무피해+ArrowShieldBlockFX(확장링/별섬광/노랑스파크) | 8999ff49 |

## ⚠️ 계층별 갭 (왜 아직 '완전 고품질'이 아닌가)

1. **드로 모션이 단일 클립** — ArcheryShot 하나뿐. DoubleL `Bow_Attack_A/B_1_All.fbx` 2클립 미활용(ARCHERY B scoped-out). → "당김→쏘기" 2단계 feel 없음.
2. **궤적 예측이 단조** — 빛 점만. 엔드포인트(착지점) 마커·파워별 곡률 축소 표시·화살촉 방향 아이콘이 없음.
3. **화살 3티어 비주얼 동일** — trailColor만 다름. 강화=은광 스파크, 마법=보라 리본/헤드 발광 없음.
4. **명중 반응 1종** — Organic 무조건. 몬스터/병사/방어구별 분기·관통(멀티히트)·파편 없음.
5. **사운드 2레이어뿐** — 기준 4레이어(드로/릴리즈/휘파람/임팩트). 비행 휘파람·방패 탁·크리틱 딩 부재.
6. **리티클 고정 중앙** — 게임이 탑다운 고정이라 마우스 대신 **플레이어 기준 전방 궤적**이 더 적합. (현재 이미 궤적점이 월드 좌표라서 OK — 리티클은 잔존 유지.)

---

## 🛠️ Phase 계획 (전부 기존 재사용 + 감각 보강)

### Phase A — 드로→릴리즈 2단계 모션 + 그립 (무게감)
- **A1** DoubleL `Bow_Attack_A(fbx 드로)` → **BowDraw** 상태로 배선, `Bow_Attack_B(릴리즈)` → **ArcheryShot** 강화. 컨트롤러 편집은 반드시 **에디터 `-executeMethod` 스크립트**(`ArcheryClipWiring.cs` 신설, BowClipWiring 선례) — 손편집 금지.
- **A2** `HumanoidClipDriver`에 `SetFloat("Draw", drawGraph)` — 홀드 동안 당김 그래프(0→1) 연동 + 드로 중 Speed 0 홀드(보행 정지) 유지.
- **A3** 드로 최대 0.5s→**0.8s**(더 긴 무게감) + **파워 풀 딜레이 보너스**(끝까지 당기면 살짝 비틀며 유지 연출). `BowMinFire` 유지.
- 검증: Play에서 당김→발사 2클립 순차 재생, 발사 중 보행 정지.

### Phase B — 궤적 예측 + 조준 보조 (젤다 핵심)
- **B1** `BowTrajectoryPreview`에 **착지 엔드포인트 마커**(궤적 끝 지면에 고품질 원형 마커, 베이크 PNG `AimLandMarker`) — 파워 풀수록 마커가 멀리.
- **B2** 파워 <0.3이면 **곡률 축소 궤적**(탄도 완만) + 파워 근접 시 궤적 점 간격이 촘촘(강조).
- **B3** 리티클과 궤적 일관 — 리티클 꺾쇠 수렴이 파워링+궤적 엔드포인트와 동일 파워원(단일 소스 `BowAimState.Power`) — 이미 단일 소스라 OK, **리티클 유지**.
- 검증: 풀/중력 시 파워별 착지점 마커 이동.

### Phase C — 화살 3티어 비주얼/물리 차별화
- **C1** 화살 GLB 3벌 각각 배선(현재 폴백 체인): 일반=arrow, 강화=arrow2, 마법=arrow3 → 실패 시 절차 조립에서 **헤드/플레처 색**만 3티어 분기.
- **C2** 트레일 고도화 — 마법=보라 애더티브 리본+헤드 발광, 강화=은백 미세 스파크, 일반=현재 흰→청 유지. (ArrowProjectile trail 파라미터 3티어 테이블)
- **C3** 마법 화살 **궤적 발광 파티클**(shadow_glow 3입자, 과장 없음) — 명중 시 보라 스타번스트.
- 검증: 3종 발사 시 트레일/헤드색 구분, 마법 발광.

### Phase D — 명중/피격 반응 고도화 (피격계 '동일' 지침 준수 — 신규만)
- **D1** 파워 풀 필드 바디샷 크리틱(≥0.95) → 기존 PlayCrit + **짧은 히트스톱 연장 + 흰 별섬광 크게**.
- **D2** 방패 막기는 이미 구현 — **사운드**(방패 탁) 추가가 갭 → `AttackSoundLayerManager`에 `PlayArrowBlock()`(방패/금속 톡, 없으면 폴백).
- **D3** 멀티히트 관통 — 마법 화살만 적 1기 추가 관통(2연속 대상, 아군/지면은 기존 방지) — 고품질 화살의 대표 feel.
- ⚠ 기존 Organic 히트FX·데미지숫자·카메라는 **불변**(사용자 '피격 동일' 지침).
- 검증: 크리틱 연출, 방패 탁 소리, 마법 관통 2히트.

### Phase E — 사운드 4레이어
- **E1** 비행 **휘파람**(arrow_whistle, 미존재 → 절차 70Hz 피치 스윕 폴백) — 발사체 수명 동안.
- **E2** 릴리즈 톡(attack_hit_bow 재활용)+크리틱 딩(PlayCrit와 동시).
- **E3** 방패 탁(D2 재사용) + 먼지/박힘 러스틀(선택).
- 검증: 드로→릴리즈→비행→명중 4사운드 순차.

### Phase F — 상점/창고/인벤 화살 UX (선택, 시간 남으면)
- **F1** 창고 화살 탭 3티어 아이콘 실제 렌더(이미 존재 — 확인만).
- **F2** 인벤/퀵슬롯에서 화살 종류 선택 → 발사 시 해당 화살 우선 소모 표시(리티클 ×N이 마법/강화/일반 표기).

---

## 🧵 실행 순서 & 병렬
- **Batch 1**: A(드로 모션) — 클립 배선이라 독립.
- **Batch 2** (병렬 2): B(궤적 보조) / C(화살 3티어).
- **Batch 3** (병렬 2): D(명중/피격) / E(사운드).
- **Batch 4**: F(U x) + 전체 통합 Play 검증.

## 게이트 (매 Batch)
1. 배치컴파일 `grep -cE "error CS" == 0` (에디터 락이면 Play로, 관례).
2. Play: 드로 2클립 / 궤적 엔드포인트 / 3티어 트레일색 / 크리틱·방패탁·관통 / 4사운드.
3. ROADMAP ✅ + QAPROGRESS.md 스냅샷 + git commit/push (사용자 3중 저장).

## ⚠ 함정
- 컨트롤러 손편집 금지 — 무조건 에디터 `-executeMethod`(BowClipWiring 선례). 클립 없으면 절차 폴백.
- UI는 베이크 PNG만(리티클 기존 유지, 새 마커는 `AimLandMarker` 베이크).
- 파티클 과장 금지(no over-density) — 짧고 또렷.
- 피격계(히트FX/데미지숫자/카메라) 불변 — D는 신규 추가만.
- delegate_task 대형 1회 위임 금지 → Phase 단위로 나눠 위임/부모 직접.
- 클립 임포트 확인: DoubleL A/B가 AnimationClip으로 진짜 임포트됐는지 B에서 파서/로더 검증.