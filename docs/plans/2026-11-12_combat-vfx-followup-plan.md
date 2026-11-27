# 계획서: 전투 VFX 후속 4항목 (무번호 25)

**사용자 요구 원문**: ① 전리품 등장 연기가 한 번만 나와야 하는데 계속 나오는 버그 ② 장비가 불필요하게 너무 커서 맞지 않음 ③ 몬스터 공격 적색링을 지금보다 절반 크기로 ④ 병사 이동·몬스터·기타 VFX를 셰이더 등으로 더 고품질화

## A1. 전리품 등장 연기 무한 반복 (버그)
- **조사 완료** — 최유력 근원: `AnimalAI.Die()`에서 `Instantiate(deathEffectPrefab)`를 호출하며 loop-off도 없고 파괴 예약도 없음. `DeathSmoke.prefab`은 `playOnAwake=0`이지만 burst 루핑(서브시스템 looping 타입)이 main.loop와 별개로 계속 방출됨.
- `LootSpawnFX.PlaySpawn`은 이미 80차 수리로 `main.loop=false` 강제 + AutoDestroy 8s + 동일 프레임·동일 위치 sqrMagnitude<0.25 중복 제거만 있음 → LootSpawnFX는 안전.
- **수리 방향**:
  1. `AnimalAI.Die()`에서 사망 FX를 반드시 `DeathVFX.PlayDeathVFX`(기존 유한 수명 경로, `Destroy(go, 2~3f)` + maxBurstsPerSecond=8)로 우회시키고, `Instantiate(deathEffectPrefab)` 직접 호출은 제거.
  2. `DeathVFX.PlayDeathVFX`에 `PlayAllParticleSystems` 방식으로 `main.loop=false` 강제 + 서브시스템 looping 타입의 `Stop` + `Destroy(go, duration)` 마무리 고정.
  3. `GaugeTrailAndLootLoopTests`(LootSpawnFX loop-off 소스 검사)는 소문자 매칭 기반으로 `LootSpawnFX.cs`는 건드리지 않으므로 영향 없음.
- **검증**: EditMode 전수(에러 CS 0 + baseline 10건 1:1) + Play 확인.

## A2. 장비 과대 스케일 (비주얼 버그)
- **조사 완료** — `ArmorVisualAttachSystem.cs`에서 두 단계 이중 스케일링:
  1. `NormalizeVisualScale`: 목표 최장축 값 (Helmet 0.34 · Shoes 0.62 · Gloves 0.62 · Back 0.85 · Mask 0.45 · Armor 0.635) × BodyScale 계수(1.03) · 최소 0.12 · 최대 5.0 클램프.
  2. `FitVisualToBodyPart`: 부위 측정 성공 시 추가 배율 (Helmet ×1.14 · Armor ×1.15 · Shoes ×1.30 · Gloves ×2.75 · Back ×1.05 · Mask ×1.15) × 수치(1.02) · 최소 0.12 · 최대 5.0 클램프.
- **수리 방향**:
  1. `FitVisualToBodyPart`의 단계별 배율을 전 부위 1.0으로 고정(수리) — 측정 게이트는 유지하되, 목표 최장축이 실측 bounds 대신 1.8m 고정계수(캐릭터키 계약, 전씬 1.8m)에 둔감하도록.
  2. `NormalizeVisualScale`의 목표값은 유지하되, 로그 출력(`✅ {slot} 비주얼`)에 실측 bounds 최장축을 함께 기록해 Play 후 실측값 대비로 후속 조정.
- **검증**: EditMode 전수 + Play 후 장비 부착 로그에서 실측 최장축이 목표 최장축 × BodyScale 계수 ±0.02 범위 수렴 확인.

## A3. 몬스터 공격 적색링 절반 축소
- **조사 완료** — `AttackTelegraphIndicator.cs`: 링 쿼드 `localScale = radius*2`이고, `Spawn(transform.position, _attackRange, TelegraphWindupSeconds)`로 `AnimalAI.cs:859`, `AfterlifeEnemy.cs:859`가 호출. radius는 `_attackRange` (1.6/1.6/1.6/1.6/1.6/1.6) — 즉 게임플레이 사정거리와 동일.
- **수리 방향**:
  1. `AttackTelegraphIndicator.Spawn`에 `radiusVisualScale` 파라미터(기본 0.5) 추가. 쿼드 localScale = `radius*2*radiusVisualScale`.
  2. `AnimalAI.cs:859` 호출부에서 `0.5f` 전달.
  3. 셰이더 룩은 `ProjectName/AttackTelegraphIndicator` 셰이더 유지(톤은 EliteColor 변주 허용).
- **검증**: EditMode 전수 + Play 후 링 반경 실측(사거리 대비 0.5배).

## A4. 병사 이동·몬스터·기타 VFX 고품질화
- **조사 완료** — 병사 이동 FX: `ShockwaveRingFX.cs`(이미 Emission 1.0, Hovl 파티클 시스템 사용, Hovl 에셋 프리팹 사용 계약). `FXBandingRing.shader` — URP Unlit+HDR emissive+깊이 페이드. 스포이블: 스포이블용은 Hovl 에셋 프리팹 계약(46/47차).
- **수리 방향**:
  1. 병사 이동 잔상: `ShockwaveRingFX`에 **깊이 페이드(shader fade)** 옵션 추가 — `FXBandingRing.shader`를 `FadeLength` 프로퍼티 추가해 URP Transparent + depth fade + HDR emissive 삼중 구조로 확장.
  2. 몬스터 임팩트 FX: 임팩트 링에 emissive 색 강도(1.0→2.0 HDR) + 깊이 페이드 + 톤 변주(EliteColor 변주 계약) 적용.
  3. `FXBandingRing.shader`에 `DepthFade` 추가 및 프로퍼티 노출 — 46/47차 에셋 계약(에셋 프리팹 파일 무변경)은 유지하되, 런타임에서 `material.SetProperty` 방식으로 HDR 톤 조정.
- **검증**: EditMode 전수 + Play 후 실카메라 캡처 비전 판독(컴파일/테스트는 별도).

---

**QA 게이트 공통**: 컴파일 error CS 0, EditMode 전수 baseline 10건 1:1 유지(2196/2186/10). 커밋은 별도 승인 대기.
