# 계획서: VFX 고품질 마무리 고도화 (무번호 33 — 2026-11-13, 계획 전용)

**배경**: 무번호 19~31 누적(물체 품질→빠진 계층→표준화→디테일) 이후의 다음 품질 점프. 실측 기반 빈 곳만 건드린다 — 과잉 개선 방지.

**전제 계약 유지**: 결정론(Random 0건 — InteriorRandomizer 단일 시드 계약) / FXPalette 단일 소스(기존 잠긴 상수 이관 금지) / 계약테스트 소스 잠금 / 컴파일 error CS 0 + EditMode baseline 10건 1:1 / Play·커밋 별도 승인 / VFX Graph 기각(결정론·프로브 재현성 리스크).

---

## P0 — Play 통합 판정 선행 권장 (게이트 아님, 권장)

무번호 19~32가 Play 미판정 상태로 누적. 본 계획 착수 전 1회 Play로 체감 기준선을 확보하면 강도 상수(수차 0.35·림 강도·먼지 밀도 등)를 체감 기준으로 확정 가능 — 과잉 개선 리스크 감소. **사용자가 바로 "진행"하면 P0 생략하고 Phase A부터.**

## Phase A — 빛 계층: 임팩트 실시간 라이트 (체감 최대 기회)

**실측 근거**: 전투 FX 전체가 발광 쿼드/링뿐 — 실제 라이트 반응 0건(전리품 골드 라이트 LootSpawnFX.SpawnGoldLight가 유일 선례). 어두운 실내(성/던전)에서 빛이 없어 "그려진 그림"으로 읽힘.

| 항목 | 내용 | 파일 | 계약 주의 |
|---|---|---|---|
| A1 임팩트 라이트 펄스 | 히트 순간 포인트 라이트(플레이어 타격·몬스터 피격) — 강도 스파이크 0.15s 소멸, 반경 3~4m, 색=FXPalette 단일 소스. LootSpawnFX 라이트 러너 패턴 승계(유한 수명+파괴) | 신규 FxImpactLight.cs | 동시 라이트 예산 가드(상한 3 — 초과 시 조용 스킵, X2 선례). MaxLightsPerRoom 4 계약과 합산 주의 — 임팩트 라이트는 방 조명과 별도 풀. EditMode 무동작 가드 |
| A2 패망 PERFECT 골드 라이트 | 패망 흡수점(플레이어 앞 1m)에 골드 라이트 0.3s — 23-X1 시퀀서 wave0 연동(파열 리듬에 빛 추가) | PlayerCombat.cs SpawnParryImpactElements wave(0) + FxImpactLight 재사용 | 기존 V2 비네트/31-V3 수차/28-P1 무기 플래시 경로 무수정 — 추가 발화만 |
| A3 몬스터 사망 라이트 소멸 연출 | 사망 시 몬스터 위치 은은한 소멸 광(디졸브 램프와 동기 — D-4 0.9s) | AnimalAI.StartDeathSequence 훅 | 30-D4 디졸브 경로 무수정, 셰이더 부재 폴백 시 라이트도 스킵 |

## Phase B — 포스트 계층 2: 블룸 펄스

| 항목 | 내용 | 파일 | 계약 주의 |
|---|---|---|---|
| B1 블룸 임팩트 펄스 | 임팩트 순간 런타임 볼륨에 Bloom intensity 스파이크(0.1~0.15s) — ScreenChromaticFX 볼륨 인스턴스 패턴 재사용(31-V3 선례: isGlobal+priority 10+인스턴스 재사용) | 신규 ScreenBloomFX.cs 또는 ScreenChromaticFX 확장(별도 클래스 권장 — 잠금 분리) | 31-V3와 동시 발화 시 볼륨 2개 충돌 주의 — 같은 러너에서 CA+Bloom 동기가 깨끗함(확장안 채택 시 계약테스트 갱신 1건). PhaseG2 글로벌 Bloom 프로필 무접촉 |
| B2 피격 채도 드랍 (판단 항목) | 피격 순간 채도 −0.1 0.2s — 과자극 리스크라 기본 미발화 상수 게이트 | (B1 확장) | Play 체감 판단 후 채택 — V3 선례(구현 후 판단) |

## Phase C — 잔상/트레일 고도화

| 항목 | 내용 | 파일 | 계약 주의 |
|---|---|---|---|
| C1 무기 스윙 트레일 디테일 | WeaponSwingTrail(TrailRenderer 평면 잔상) — 폭 테이퍼(끝으로 가늘게)+알파 그레이디언트+팁 엣지 밝기. AttackArcVFX(3D 경로)와 병존 — Trail은 잔상 감각, Arc는 형태 | WeaponSwingTrail 소재 파일 확인 후 최소 침습 | 잠금 충돌 사전검사 선행. 머티리얼은 FXPalette/기존 색 무손상 |
| C2 몬스터 공격 잔상 | 몬스터 근접 공격에 AttackArcVFX 위스프 버전 재사용(23-X3 아크와 동일 경로 — 잔상 레이어 추가 발화) | AnimalAI 공격 지점 | 기존 X3 아크 무수정 — 색만 MonsterDanger 계열 |

## Phase D — 2차 파티클(물리감)

| 항목 | 내용 | 파일 | 계약 주의 |
|---|---|---|---|
| D1 파편 데브리 | 강타격(isCrit/Heavy — CombatFXGate HitSeverity 실측 존재) 시 절차 파편 3~5조각(결정론 포물선+지면 바운스 1회, 유한 수명) | 신규 FxDebrisSpawner.cs | Random 0건 — StableHash 시드. 예산 가드(동시 12). 충돌 없는 비주얼 전용(콜라이더 부착 금지) |
| D2 불티 상승 | 화염/화로/디졸브 엣지에서 불티 파티클 상승 — FxMaterialLibrary ScorchEmberMat(D-2) 재사용 | FxMaterialLibrary 확장 or 재사용 발화부 | 머티리얼 유한 수명 계약 유지 |
| D3 잔연 | 사망 디졸브 완료 지점에 플립북 잔연 1.5s — D-1 FxFlipbookGenerator 캐시 재사용(신규 텍스처 생성 금지) | FxDissolveUtility 완료 지점 | D-4 경로 무수정 — 후속 발화만 |

## Phase E — 이월/판단 항목 (변경 없음)

- **22-P3 Unlit 전환**: Play 비교 대기 유지(병렬 비교 캡처 필요).
- **S1-4 지면 틴트**: 지형 타입 조회 API 부재 — 별도 설계 확정 필요(전수 재검증 0건 확인 완료).
- **W2 몬스터 FX 동결**: LocalTimeScale만 제어 구조 — 구조 변경은 별도 확정(보고 완료).
- **VFX Graph**: 기각 유지.

## 진행 방식

- 우선순위: **A(빛) → B(포스트) → D(2차 파티클) → C(트레일)** — 체감 기여 순.
- 각 Phase: 구현+계약테스트 → 컴파일 → EditMode 전수 baseline 1:1 → Play 별도 승인(강도 상수 트림).
- 신규 파일만 잠금 신설, 기존 잠금 계약은 rg 사전검사 후 최소 갱신(100차 함정 — 부정 잠금은 신규 블록 스코프 한정).

## 착지 결과(2026-11-13 실행 후 기록)

- **착지**: Phase A(FxImpactLight+A1/A2/A3)·B1(ScreenBloomFX+2훅)·D1(FxDebrisSpawner)·D2(FxEmberRise) — QA 2369/2359/10(baseline 1:1), 계약 10건.
- **중복 셀프감사로 기각**: C1(45차 P4가 테이퍼+그레이디언트 이미 구현 — 확인 실측)·D3(29-M3-2 플립북 스모크가 커버)·C2(23-X3 아크가 잔상 역할 — 중복 리스크).
- **B2 채도 드랍**: 기본 미발화 설계 유지 — Play 체감 판단 후 채택.
