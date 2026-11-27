# 계획서: 임팩트 고품질 파티클 승격 (무번호 76 — 시트애니 스파크 + 충격 먼지 컬럼)

**착지 기록(2026-11-18)**: FxImpactSheetSpark.cs + FxImpactDustColumn.cs 신규, CombatFXGate L147~148 배선(밴드 공유), P76 테스트 5건. 수리 1건(부모 — Emit(EmitParams) CS1503 → 5인자 오버로드). 컴파일 error CS 0 ×2, EditMode 2561/2551/10 — baseline(qa_75) 실패집합 1:1, 신규 5/5 GREEN. 2차 리플 이월 유지.

**사용자 지시(2026-11-18)**: "진행" — 76 설계도(트랙 B) 승인에 따른 착지. Play·커밋 별도 승인 유지.
**상위 설계**: docs/plans/2026-11-18_no76-vfx-graph-gpu-upgrade-design.md (트랙 B 선행 확정)

---

## 스코프 판정

- **포함**: ①시트애니 스파크(크리/강타격 확산 폭발) ②충격 먼지 컬럼(강타격 지점 상승 기둥)
- **제외(이월)**: 크리 2차 저강도 리플 — FxDistortionWave(71-L2) 잠금 계약 접촉 리스크로 본 라운드 제외, 후속 판정
- 트랙 A(.vfx 에셋) — 사용자 에셋 선정 대기(77 후보)

## 원장 근거 (실측)

- 임팩트 방향성 스파크 = 라인 3본(39-T2 — SpawnDirectionalSpark) → "점 3개" 수준. 본 항목은 라인을 대체하지 않고 **보완 레이어**로 확산 폭발 시각화 추가
- 절차 플립북 선례: FxFlipbookGenerator(37-S3 — 참격 아크 6프레임 절차 생성) + FxSlashFlipbookFlash(시트애니 재생 패턴 — sheet.frameOverTime 1패스)
- 게이트 선례: `isCrit || damage >= 15f` (CombatFXGate 141·144행 — 33-D1·37-H3 공유 밴드)

## 구현

### S1 시트애니 스파크 — 신규 `Assets/Scripts/Systems/FxImpactSheetSpark.cs`
- 절차 시트 텍스처: 4×2 프레임 8장(256×512) — 결정론 해시로 방사 스파크 확산(프레임별: 점화 소체→방사 링대→파편 사선→소멸 잔광). **사각 금지·Random 0건**(FlipbookGenerator PuffHash 선례).
- 스폰: 크리/강타격 임팩트 지점에 단발 파티클(1개, 시트애니 1패스 0.28s, 빌보드) — 크리 1.5배 스케일(70-U3 위계 준용). 색 = FXPalette.Edge 단일 소스.
- 머티리얼: 기존 임팩트 플립북 경로 선례(FxSlashFlipbookFlash) 패턴 — 인스턴스 클론+유한 파괴, isPlaying 가드 Destroy/DestroyImmediate.
- 예산: 동시 스폰 상한 소형(정적 카운터 6) + 동일 지점 쿨다운 0.12s — 링 예산(X2) 미소비.

### S2 충격 먼지 컬럼 — 신규 `Assets/Scripts/Systems/FxImpactDustColumn.cs`
- 강타격 지점에서 위로 솟는 노이즈 먼지 기둥 — EnsurePuffTexture 재사용은 private 잠금(74 확정 — 무접촉)이라 **전용 절차 텍스처**(64×256 세로 스트립, 세로 노이즈 그라데이션+하단 마스크) 1회 생성 캐시.
- 파티클 4개 수직 에미션(위로 1.2~1.8m, 0.5s, 세로 스트레치 빌보드), 사운드 없음(기존 ImpactSoundFX 위계 유지 — 72-W3 무손상), 동일 게이트(isCrit || damage >= 15f).
- 쿨다운 0.12s(스파크와 독립 카운터)·수명 유한·EditMode 즉시 정리 가드.

### S3 배선 — 수정 `Assets/Scripts/Systems/CombatFXGate.cs`
- L144(`if (isCrit || damage >= 15f) FxGroundCrackFX.Burst(position);`) 직후 2줄:
  `FxImpactSheetSpark.Burst(position, isCrit);` + `FxImpactDustColumn.Spawn(position);`
- **[70-U3]/[71-L2]/[46차 축소 주석] 문자열 무손상 — 최소 diff(2줄+마커 주석)**

## 계약 (잠금 무손상)

- MagicHit 룩·기존 스파크 라인(39-T2)·파편(33-D1)·크랙(37-H3) 전부 유지 — **추가 레이어만**
- LDR 고정(66)·블룸 캡(68): 색 HDR 성분 0, FXPalette.Edge 단일 소스
- 링 예산(X2): `_activeRingCount` 계열 문자열 0건 추가
- FxDistortionWave/FxSlashFlipbookFlash/EnsurePuffTexture 잠금 파일 무접촉
- 결정론(Random 0건)·EditMode 무동작 가드·게이트 밴드 상수 공유(damage >= 15f)

## 테스트 — 신규 `Assets/Scripts/Tests/EditMode/FxGpuQualityP76Tests.cs`

- 마커(S1·S2·S3)/게이트 밴드 문자열(CombatFXGate 배선 2줄)/시트 절차 결정론(Random 0건)/사각 금지(smoothstep 방사)/머티리얼 유한 파괴/isPlaying 가드/동시 상한·쿨다운/EnsurePuffTexture·FxDistortionWave 무참조

## QA 게이트

- 컴파일 error CS 0 + 전수 EditMode **baseline=qa_75(2556/2546/10) 실패집합 1:1** + P76 GREEN
- 배치 background+폴링. Play·커밋 별도 승인.
