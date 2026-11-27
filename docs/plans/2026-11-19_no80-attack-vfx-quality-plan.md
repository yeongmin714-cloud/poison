# no80 — 공격 VFX 고품질화 계획

- **작성일**: 2026-11-19
- **상태**: 계획(미실행) — 사용자 승인 전 구현 금지
- **전제**: [무번호 70] 공격 VFX 업그레이드(U1 틴트/U2 무기글로우/U3 크리 위계) + [79-R20] 공격 이펙트 색 통일(AttackPrimary) 착지 상태에서의 품질 상향 라운드
- **품질 참조**: 젤다/엘더스크롤 — 임팩트 프레임·계층형 히트 반응·읽기 쉬운 스윕 궤적

## 계약 상속 (기존 계약 충돌 방지 — 각 Phase 준수)

- **[79-R20] 색 통일 유지**: 공격 VFX 색은 `FXPalette.AttackPrimary` 단일 소스. 이번 라운드에서 색을 다시 나누지 않는다 — 위계는 **크기·모양·광·시간**으로만. 새 팔레트 상수 추가 금지.
- **플립북 결정론**: FxFlipbookGenerator는 해시 FBM + 시드 고정, 난수 API 0건 계약 유지(29-D1).
- **알파 캡 계약**: AttackArcVFX AlphaCap 0.62·RefTipSpeed 속도비례(53~60차) 유지 — 트레일 신설 시 동일 계약 승계.
- **블렌드 계약**: FxMaterialLibrary.CreateSoftParticle 블렌드 수리([79-R17]) — 신규 머티리얼은 이 빌더 경유. `CreateSoftParticle("SlashArcMat"` 리터럴은 FxQualityRoadmapP37Tests 잠금 → 유지.
- **파티클**: 소프트키워드 폐기(66)·링발광 캡(68)·스트림 재정합(69) 계약 유지.
- **lockstep 테스트**: 소스-계약 테스트(FxAttackUpgradeP70Tests, FxParticleDuplicateAddP48Tests, FxMaterialGuardP47Tests, FXPaletteAndMotionTests, FxSecondaryRippleP77Tests 등)는 각 Phase 수정 파일과 lockstep 갱신 — Phase 마지막 태스크로 강제.

## 현재 상태 실측 기준 (11-19)

- 플립북 아크: 128px 셀 × 4×2(512×256), 6프레임, 스윕 −60°→+60°, 두께 18°, 웜화이트 RGB(255,244,214) — FxFlipbookGenerator.cs 40~80행
- 아크 수명 0.7s(38차), 색 통일 AttackPrimary(79-R20)
- 크리 구분: 크기 앰프 + 라이트/블룸만 — 모양(플립북)은 일반과 동일
- 무기 피드백: FxWeaponGlow 펄스(HumanoidClipDriver:1663) — 트레일 잔상 없음
- 히트 반응: 틴트 0.08s(U1)·실루엣·스파크·데브리·디스토션·라이트 — hit-stop·스크린 셰이크 없음

## 갭 → Phase 매핑

| 갭 | 근거 | Phase |
|---|---|---|
| 플립북 저해상도·계단 스윕(6프레임) | 128px 셀 실측 | P1 |
| 크리 모양 위계 부재(광만 존재) | no70 갭③ | P2 |
| 무기 트레일 잔상 부재 | no70 갭② 잔여 | P3 |
| 히트 반응 시간축 부재(hit-stop·셰이크) | 젤다 참조 | P4·P5 |
| 스파크가 점형 위주 | 현 구조 | P6 |
| 피격 틴트 체감 미확정 | no70 갭① | P7 |

---

## Phase 1 — 플립북 아크 고해상화·프레임 증가 (FxFlipbookGenerator)

**목표**: 아크 플립북을 128px/6프레임 → 256px 셀/10프레임으로 격상. 스윕 계단 제거.

- Task 1.1: `FxFlipbookGenerator.cs` — 슬래시 아크 전용 상수 분리(CellSize 256, Columns 4, Rows 3 → 12슬롯, 사용 10프레임, 아틀라스 1024×768 → pow2 보정 1024×1024). 연기 플립북(128px/6f)은 건드리지 않음.
- Task 1.2: 스윕 곡선 정련 — 프레임 간 스윕 진행에 ease-out(후반 감속) 적용, 두께 18°→14°+에지 페더, 반경 프로파일에 노이즈 요철(칼날 느낌).
- Task 1.3: **코어-에지 이중 구조** — 셀 RGB를 단일 웜화이트 유지하되 알파 프로파일에 중심 코어(고알파) + 외곽 에지(저알파 페더) 2층 구조. RGB는 단일색(79-R20 계약), 모양만 이중화.
- Task 1.4: **[계약 갱신 결정 필요]** "141차 생성 해상도 표준 계열(128px 셀)" 주석/표준 갱신 — 256px을 아크 전용 예외로 할지 생성 표준 자체를 갱신할지 사용자 결정.
- Task 1.5: lockstep — FxStandardTextureTests·FxAttackUpgradeP70Tests 잠금 문자열 갱신.
- 검증: EditMode 배치(아틀라스 해상도·프레임 수·RGB 단일색·결정론 시드 assert) + 캡처 프로브 1회(아크 PNG 정량 — 프레임 경계 계단 여부).

## Phase 2 — 크리 모양 위계 (FxSlashFlipbookFlash + FxFlipbookGenerator)

**목표**: 크리가 광뿐 아니라 **모양**에서도 읽히게. 색은 통일 유지.

- Task 2.1: 크리 전용 스윕 범위 — 일반 −60°→+60°(14° 두께) 대비 크리 −75°→+75°(20° 두께·더 긴 반경). 아틀라스에 크리 프레임 세트 추가(10+10=20프레임, 4×5 그리드) 또는 두 번째 아틀라스 — Task 1.1의 그리드 산정에 포함.
- Task 2.2: `FxSlashFlipbookFlash.cs` — 크리 판별 파라미터(기존 크리 앰프 경로)를 플립북 프레임 범위 선택에 연결. 크리=크기 앰프 ×1.6(기존) + 크리 프레임 세트 + 라이트 위계(기존 유지).
- Task 2.3: lockstep — FxAttackUpgradeP70Tests U3 잠금 갱신.
- 검증: EditMode 배치(크리/일반 프레임 세트 분기 assert) + 캡처 프로브(일반 vs 크리 아크 PNG 병렬 비교).

## Phase 3 — 무기 트레일 잔상 (FxWeaponGlow 확장)

**목표**: 스윙 중 칼날 궤적 ribbon — 아크(날 궤적 VFX)와 별개로 무기 본체에 붙는 잔상.

- Task 3.1: `FxWeaponGlow.cs`에 트레일 층 추가 — TrailRenderer(칼날 팁 본 추적, AttackPrimary, 알파 캡 0.62 계약 승계, RefTipSpeed 속도비례 게이트: 기준 속도 미만 = 트레일 미발화).
- Task 3.2: 수명 짧게(0.12~0.18s) — 아크(0.7s)와 역할 분리: 아크=스윕 도형, 트레일=즉발 잔상.
- Task 3.3: HumanoidClipDriver 배선 — 기존 FxWeaponGlow 호출 지점에서 트레일 함께 토글(신규 키/파라미터 없음).
- 검증: EditMode 배치(트레일 컴포넌트·알파캡·속도 게이트 assert) + 캡처 프로브(스윙 중 프레임 캡처로 잔상 가시 확인).

## Phase 4 — 임팩트 프레임 / hit-stop (CombatFXGate)

**목표**: 크리/강타 히트 순간 시간축 정지 — 젤다식 임팩트 프레임.

- Task 4.1: `CombatFXGate.cs`에 hit-stop 게이트 신설 — 크리 전용, `Time.timeScale` 0.05 → 0.06s → 복원(코루틴, unscaled time 기준). 재진입 중복 방지 가드.
- Task 4.2: 일반 히트는 hit-stop 없음(위계 유지). 사망/수집/낚시 등 비전투 경로 영향 0건 — CombatFXGate 전투 경로만.
- Task 4.3: lockstep — FxCombatResidueP71Tests 등 CombatFXGate 잠금 갱신.
- 검증: EditMode 배치(게이트·가드·일반 히트 미개입 assert) + **Play 육안(사용자 세션)** — hit-stop 체감.

## Phase 5 — 스크린 셰이크 (신규 FxScreenShake)

**목표**: 히트 강도 비례 화면 흔들림 — 크리 > 일반, 미세 진폭.

- Task 5.1: `FxScreenShake.cs` 신규 — 카메라에 additive offset(별도 child 노드 또는 오프셋 적용), 진폭 위계: 일반 0.03 / 크리 0.08, 감쇠 0.15s, 중복 히트 시 누적 캡.
- Task 5.2: 카메라 시스템 충돌 감사 — 기존 카메라 팔로우/얼굴방향 로직과 무간섭(오프셋 additive, 포즈 덮어쓰기 금지). 사망/데칼 경로 미개입.
- Task 5.3: lockstep — 신규 파일이라 기존 잠금 무충돌 확인 1회.
- 검증: EditMode 배치 + Play 육안(셰이크 강도 체감).

## Phase 6 — 스파크 라인형 정련 (FxImpactSheetSpark)

**목표**: 히트 스파크를 점형 → 속도 방향 라인형(stretched)으로.

- Task 6.1: `FxImpactSheetSpark.cs` — 스파크 파티클에 velocity-aligned stretch(speed 비례 길이), 발사 방향 히트 노멀 반사 정렬.
- Task 6.2: 개수/수명 미변(기존 균형 유지) — 모양만 교체. AttackPrimary 유지.
- Task 6.3: lockstep — FxHitRewok39Tests 잠금 갱신.
- 검증: EditMode 배치 + 캡처 프로브(스파크 스트릭 가시).

## Phase 7 — 피격 틴트 체감 확정 (FxRimOverlay + FxHitSilhouette)

**목표**: no70 갭①(틴트 미체감) 종결 — 0.08s 틴트를 림 오버레이와 합성해 가시성 상향.

- Task 7.1: `FxHitSilhouette.cs` — 틴트 중 림 에지(FxRimOverlay 계약 경유) 동시 점화, 0.08→0.10s 미세 연장.
- Task 7.2: 크리 피격 시 틴트 + 셰이크 + hit-stop 동기화(같은 타임스탬프 발화 — Phase 4/5와 결합).
- 검증: EditMode 배치 + **Play 육안(사용자 세션)** — 이 갭만큼은 배치 판정 불가, 세션 확정 필요.

## Phase 8 — 전수 검증

- Task 8.1: EditMode 전수 배치(내 라운드 실패 0건 기준 — 기존 부채 10건 별도 표기 유지).
- Task 8.2: PlayMode 캡처 프로브(79-R17/R18 선례) — 일반/크리 아크·트레일·스파크 PNG 정량.
- Task 8.3: 사용자 세션 Play 육안(최종 게이트) — hit-stop·셰이크·틴트 체감만 배치로 대체 불가 명시.
- Task 8.4: 커밋은 별도 승인.

---

## 순서·의존

P1 → P2(아틀라스 산정 공유) → P3(독립) → P4·P5(독립) → P6(독립) → P7(P4·P5 후 동기화) → P8.

## 오픈 질문 — 사용자 결정 (2026-11-19)

1. **141차 생성 해상도 표준**: ✅ **표준 자체 갱신** — 256px 셀을 아크 전용 예외가 아니라 FxFlipbookGenerator 생성 표준으로 갱신 (P1 Task 1.4 확정)
2. **hit-stop 범위**: ✅ **플레이어 공격 히트만** — 몬스터 공격 히트에는 미적용 (P4 확정)
3. **셰이크 초기 진폭**: ✅ **승인** — 일반 0.03 / 크리 0.08, Play 체감 후 트림 (P5 확정)
4. **크리 프레임 세트**: ✅ **B안(분리) 확정 (2026-11-19)** — 크리 전용 아틀라스 512px 셀·4×4·10프레임(2048×2048 pow2),
   정상 아크 256px 셀·4×4·10프레임(1024×1024 pow2)과 동일 페이싱. 근거: 셀 해상도가 같으면 A·B 화질 동일 —
   B안은 크리만 512px로 올릴 수 있는 유일한 구조(정상 아크 파이프라인 무변동·GPU 메모리 절감), 크리는 화면에서
   크고 오래 보이는 이펙트라 4배 디테일을 셀 단위로 배정. 정상 아크와 동일 그리드로 통합하면 4배 메모리 낭비.
   첫 크리 히치는 부팅 선생성(Warmup — CoreSystemsBootstrap.Awake)으로 흡수.
