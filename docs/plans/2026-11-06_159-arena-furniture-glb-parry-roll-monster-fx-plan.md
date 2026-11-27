# 159차 계획 — 아레나 가구 GLB 전면 교체 + 패링 고품질 + 실내 구르기 수리 + 몬스터 공격 VFX 고급화

**작성일**: 2026-11-06 · **상위 근거**: 사용자 지시 (2026-11-06)

## 배경

1. **아레나(여관 "The Rusty Dragon Inn")** 가구가 전부 프리미티브(Cube/Cylinder) 단색 기하 — 레스토랑 GLB 팩(650종 임포트 완료, Resources/Models/Restaurant/)으로 전면 교체.
2. **패링** 95차 기반(Perfect/Good 판정·확산링·별섬광·방향성 스파크·SFX·슬로우·FOV 펀치)은 유지하고 VFX를 고급화.
3. **실내 영지 씬 구르기 불가** 결함 수리.
4. **몬스터가 플레이어를 발견하고 공격 시 VFX 단조로움** — 고급화.

## P1 — 실내 구르기 수리 (최우선, 결함)

- **원인 가설**: 실내 씬에서 `_isGrounded` 판정 실패 → HandleRoll의 접지 게이트에서 차단. PlayerMovement는 [146차] 실내에서 지형 클램프를 스킵(UITransitionState.IndoorActive)하지만, CharacterController.isGrounded가 실내 바닥(y=0 지대)에서 false로 떨어지는 경로 존재 가능(콜라이더 갭/박힘).
- **수리 방식**: ①재현 프로브 — 실내 씬 배치 모드에서 플레이어 지면 판정 실측 로그 ②실측에 따라 접지 폴백 강화(실내용 raycast 마스크에 실내 바닥 레이어 포함 여부 점검) 또는 구르기 게이트 조건 완화(실내 전용 폴백). PlayMode 배치 계약 준수(-quit/-nographics 금지).

## P2 — 아레나 가구 GLB 전면 교체 (사용자: "있는대로 모두 교체")

**교체 대상 전부** (ArenaInteriorBuilder.cs 프리미티브 사이트):

| 사이트 | 현재 | GLB 교체 매핑 |
|---|---|---|
| BuildTableArea 원형탁자 2 | Cylinder 단색 | Table 계열 원형(실측 dims 후 선정) |
| BuildTableArea 사각탁자 3+의자 3 | Cube 단색 | Table_1 계열 + Chair_1 계열 |
| BuildPrivateBooths 탁자+벤치+격벽 (4~6세트) | Cube 3종 | 탁자=Table 계열, 벤치=Furnit 계열(Furnit_A_10/15 — 1.8m×0.36m 벤치형), 격벽=Furnit 캐비닛형 |
| BuildStorage 오크통 | Cylinder | Furnit 통/서랍형 (dims 실측 후 선정) |
| BuildKitchen 조리대/개수대/화로/통 | Cube/Cylinder | StationTable_1_A(조리대)+Sink/화로형 Furnit |
| PlaceBettingDesk 베팅데스크 | Cube 8×2×1.5 | 카운터형 GLB (RestaurantCounter 별칭 승계, MeshCollider 유지 계약) |
| 무대 악기/배럴/문·기둥 | 구조물 — **유지**(건축 요소는 프리미티브 계약) |

- **변형(사용자: "변형 가능하면 변형해줘")**: 동일 종류 연속 배치 시 GLB 변형 알파 계열(A~G)을 해시 기반 순환 배정(정수 해시 — Random API 0건 계약). 예: 의자 8개 → Chair_1_A/B/C… 순환. 국가별(nation) offset은 hash(nation)으로만.
- **로드 경로**: RuntimeModelLoader.TryGetModel + restaurant_* 별칭 확장(필요 종류만 추가), 폴백=기존 프리미티브(부재 시 소실 금지).
- **정규화**: NormalizeNpcVisual 선례 — 목표 높이 정규화(탁자 0.75m/의자 0.9m/카운터 1.0m) + 바닥 접지(y 보정) + MeshCollider 유지 계약(데스크).
- **접지 블롭**: 기존 IndoorQualityLayers.AddBlobAt 위치 유지(개수 계약 1~11번 무손상).

## P3 — 패링 고품질 (사용자: "전부 추가")

PlayerCombat.TryParryImpactFX 확장(기존 4요소 무손상 위 추가):

1. **히트 스톱** — HitStopManager 짧은 프리징(PERFECT 0.07s/GOOD 0.03s, PlayerCombat 선례 정적 2.0 강도 계약 승계 아님 — 새 파라미터).
2. **지면 링 파동 2겹** — 158차 P4-D 패턴 재사용(지면 링 쿼드, 스태거 0.2s, PERFECT=금색 2겹/GOOD=청색 1겹).
3. **별빛 스파클 버스트** — AcquisitionCinema.GetStarTexture 재사용(사각 금지 계약), 흡수점에서 방사 버스트.
4. **PERFECT 전용 아우터 링** — 반대 위상(팽창+축소) 금색 외곽 링 — 카운터 타격감.

## P4 — 몬스터 공격 VFX 고급화 (사용자: 고급화 지시)

AnimalAI.TryAttack/텔레그래프+ConcludeMeleeTelegraph 타격 시점에 연결(기존 HitFlash 무손상):

1. **공격 예고(텔레그래프) 강화** — 몬스터 전방 부채꼴 지면 표시(AttackArcVFX 파편 재사용 가능하면, 없으면 ShockwaveRingFX 방향형).
2. **타격 임팩트** — ①HitStopManager 0.05s ②ShockwaveRingFX.SpawnDirectionalSpark(공격자→플레이어 방향) ③CombatVFXController.SpawnHitSparks(흡수점) ④CombatFXGate로 플레이어 근접 시 카메라 셰이크(CameraShake 기존 파이프 재사용).
3. **빈공격(플레이어 회피 성공)** — 스윕 공기 파동 링(희미한 흰색) — 막았을 때와 피했을 때 피드백 분리.

## 검증 (독립 QA 체인)

- 컴파일 error CS 0 → EditMode 전수(baseline 10건 1:1 + 신규 계약테스트) → 배치 Play 프로브(실내 구르기 재현 + 아레나 가구 로드 실측 + 패링/몬스터 VFX 발화).
- 신규 테스트: P1 구르기 접지 계약, P2 GLB 로드 폴백+변형 순환 결정론, P3 패링 FX 발화 계약, P4 몬스터 공격 VFX 발화 계약.

## 위험/계약

- 650종 GLB 임베드 텍스처 추출 금지 계약(141차) — 머티리얼 교체 금지, 원본 렌더러 사용.
- Random API 0건 — 변형 배정은 정수 해시.
- 아레나 접지 블롭 개수 계약(1~11) 무손상, 베팅 데스크 MeshCollider 유지.
- Play 판정은 사용자 승인 게이트.

## 진행 순서

P1(구르기) → P2(아레나 가구) → P3(패링) → P4(몬스터 VFX) → 게이트 → 기록/보고.

**승인 대기: "진행" 답변 주시면 위 순서로 실행합니다.**
