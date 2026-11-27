# 계획서: 공격 VFX 업그레이드 (무번호 70 — U1 틴트 가시화 + U2 스윙 무기 발광 + U3 크리 색 위계)

**사용자 지시(2026-11-17)**: "플레이 확인은 나중에 하고 이번엔 공격 vfx를 더 업그레이드 해보자" — Play 판정 유예, 공격 VFX 업그레이드 라운드 즉시 진행. 커밋 별도 승인.

---

## 0. 상태 재구성 실측 (2026-11-17 — vfx-round-state-reconstruction 프로토콜 준수)

- **38-2R 착지 판정**: T2/T3/T4는 39라운드로 착지([무번호 39-T2] CombatFXGate.cs:141 · [39-T3] :130,133 · [39-T4] FxDissolveUtility.cs:25 마커 실측). **T1(몬스터 틴트 가시화)만 미착지** — 38 계획서 "영상 실측 최대 격차" 항목이자 최우선 추천순 1위.
- **37 계층 착지 판정**: H1(PlayerCombat:572,1901)·G3(ScreenEdgeDangerFX)·H3(FxGroundCrackFX)·G1(ArrowProjectile:180) 마커 실측 완료.
- **E1(타격 지면 스커치) = 중복 판정 → 본 라운드 제외**: 37-H3 지면 크랙 데칼(강타격 지면 마킹)이 동일 역할 커버 — 재정의 금지(원장 이월 규칙).
- **플레이어 스윙 궤적**: 53~60차 알파캡·블렌드·속도비례 튜닝 완료(AttackArcVFX AlphaCap 0.62·RefTipSpeed 등) — 궤적 자체 성숙, 본 라운드 손대지 않음.
- **갭 확정 3건**: ①피격 틴트 미체감(38-T1) ②스윙 발화 시 무기 본체 피드백 0건(트레일/아크=날 궤적만 — 무기 본체는 무반응) ③크리 등급 위계가 광(라이트/블룸)에만 존재 — 모양(플립북)은 단일색.

## U1 — T1 이월 착지: 히트 틴트 가시화

| 항목 | 내용 |
|---|---|
| 현황 실측 | `CombatVFXController.PlayHitFlash` = `r.sharedMaterial`(슬롯 0만)에 `m.color = Color.white` 0.15s — ①슬롯 0만 커버(멀티머티리얼 FBX 미체감 뿌리) ②shared 직접 수정(동일 재질 타겟 동시 백플래시 부작용) |
| 수리 | ①`r.sharedMaterials` **전 슬롯** 대상 ②**인스턴스 클론 전환**(shared 무손상) → 흰색 틴트 → 종료 시 원본 배열 원복+인스턴스 파괴 ③지속 0.15→**0.08s**(38-T1 스펙 — 짧고 강렬) |
| 계약 | 머티리얼 대입은 47-a 단일 관문(`FxMaterialLibrary.TryAssignSharedMaterials`/`TryAssignSlot0`) 경유 · 원본 판독=sharedMaterials 직접 판독(49-T1 `.material` 게터 금지 승계) · `_Color` 부재 머티리얼 스킵 가드 유지(47차 후속5) · 기존 HitFlashRunner 아웃라인 스케일(Phase A)·플래시 레지스트리 유지 · **FxHitSilhouette(림) 무수정 — 이원 계층 유지(35-S2 계약)** |

## U2 — 스윙 발화 무기 발광 펄스 (신규)

| 항목 | 내용 |
|---|---|
| 근거 | 패망만 무기 플래시 존재([28-P1] WeaponFlashPulseRoutine — PlayerCombat private 코루틴, 재사용 불가 구조). 공격 스윙은 무기 본체 반응 0건 |
| 구현 | 신규 `FxWeaponGlow` static 유틸 — `Pulse(GameObject visual, Color c, float duration)`: 무기 비주얼 전체 Renderer emissive 캡처 → `_EMISSION` 활성+펄스 → 원복(28-P1 캡처/원복 계약 승계). 유한 수명 러너(누출 금지) |
| 배선 | `HumanoidClipDriver.FireComboSlash`의 `AttackArcVFX.Swing` 호출 지점 인접 1줄 — 플레이어 모드 전용(`WeaponEquipManager.CurrentVisual`, 부재=주먹 → 조용 스킵) |
| 범위 | **병사 스윙은 범위 외**(SoldierSwingTrail 기존 계약 무수정). 색=`FXPalette.Core` 단일 소스 · 지속 0.15s · 결정론(Random 0건) · EditMode 무동작 가드 |

## U3 — 크리 슬래시 플립북 색 위계 (소)

| 항목 | 내용 |
|---|---|
| 근거 | 크리 위계가 광(FxImpactLight 4.2·블룸 2.8)에만 존재 — 모양(플립북)은 항상 동일색. 플레이북 기본 바 "등급 위계" |
| 구현 | `FxSlashFlipbookFlash.Play(position, scale, bool isCrit = false)` — 크리 시 파티클 틴트 Core(백)→`FXPalette.ParryGold`(골드). 기존 머티리얼 구조·TryAssignSharedMaterials 가드 무손상 |
| 호출부 | CombatFXGate — `Play(position, isCrit ? 1.5f : 1f, isCrit)` |

## 범위 파일 (scope files)

- 수정: `Assets/Scripts/Systems/CombatVFXController.cs` (U1)
- 신규: `Assets/Scripts/Systems/FxWeaponGlow.cs` (U2)
- 수정: `Assets/Scripts/Systems/HumanoidClipDriver.cs` (U2 — 1줄+마커)
- 수정: `Assets/Scripts/Systems/FxSlashFlipbookFlash.cs` (U3)
- 수정: `Assets/Scripts/Systems/CombatFXGate.cs` (U3 — 호출부 1줄)
- 갱신: `Assets/Scripts/Tests/EditMode/FxQualityRoadmapP37Tests.cs` — 잠금 문자열 `FxSlashFlipbookFlash.Play(position, isCrit ? 1.5f : 1f);` 정밀 갱신(같은 변경세트 — 식별자 도피 금지)
- 신규: `Assets/Scripts/Tests/EditMode/FxAttackUpgradeP70Tests.cs` — U1 슬롯전수/인스턴스 원복 마커·U2 유한수명/Random0건/FXPalette 위임·U3 색위계+호출부 계약

## 계약 주의 (구현 서브 전달)

- 부정 잠금(Does.Not.Contain)은 코드형 토큰만(`UnityEngine.Random` — 단어형 "Random" 금지, 자기 주석 오염 선례).
- 주석 문구도 잠김 — 기존 계약 문자열 유지 또는 같은 커밋 정밀 갱신.
- 위임 정책: 구현/QA 서브 분리. Play 미실시(사용자 유예) — EditMode+컴파일까지만.
- namespace `ProjectName.Systems`, 마커 형식 `[무번호 70-U1]` 등.

## QA 게이트

1. 컴파일 error CS 0 (qa_70_compile.log)
2. 전수 EditMode baseline(153차 qa = 1876/1866/10) 1:1 — 신규 실패 0 (qa_70_editmode-results.xml)
3. Play 미실시 — 체감 판정은 사용자 다음 Play에서. 커밋 별도 승인.
