# 계획서: 전투 후속 FX 3종 (무번호 71 — W1 전투 잔상 + G2 처치 콤보 + L2 디스토션 히트 웨이브)

**사용자 지시(2026-11-17)**: "다음 후보 모두 진행" — 70차 보고의 후보 3건 전부 착지. Play·커밋 별도 승인 유지.

---

## 0. 셀프감사 (중복·계약 — 2026-11-17 실측)

- **W1**: ScreenVignetteFX 재사용 계획(38-3R 원문) — `Begin(cam, color, peakAlpha, fadeIn, hold, fadeOut)` API 실측 완료. 화면 계층 기존 점유: V1 피격 적색/패망 골드/패러독스 — 전투 잔상(중립) 없음 = 신규 색·트리거, 중복 0.
- **G2**: "콤보" 검색 = 공격 콤보 스테이지(HumanoidClipDriver 등)만 존재 — 처치 콤보 카운터 0건 = 신규. 데미지 숫자 패턴 = `DamageNumberRunner`(OnGUI — CombatVFXController:554) 재사용 방침(37 계획 원문 "텍스트 페이스 — 기존 데미지 숫자 패턴").
- **L2**: Dissolve(30-D4/48/49)·플립북(37-S3)·Fresnel 림(29-D3) 착지 완료 — Distortion만 미구현. `_CameraOpaqueTexture` **비활성 실측**(Assets/URP/URPAsset.asset m_RequireOpaqueTexture: 0 — 활성 URP 자산, m_RequireDepthTexture:1과 동일 파일) → 플래그 ON + 런타임 가드 필수(블랙 링 방어 — 66~69 블랙뿌리 선례).
- 계약 잠금 확인: CombatCameraEffects.PlayKill 내부 문자열 잠금 0건(DeathFeedbackPhaseMTests는 PlayHitShake만) · ScreenChromaticV3Tests는 본 라운드 미접촉 파일.

## W1 — 전투 잔상 페이드 (처치 직후 긴장 잔광)

- **트리거**: `CombatCameraEffects.PlayKill()` — 처치 진입 단일 관문(AnimalAI:1513 직접 · PlayKillVFX:825 양 경로 수렴 실측) 1줄 훅.
- **구현**: 신규 `FxCombatResidueFX.Pulse()` — 활성 창 가드(단일 인스턴스, 연속 처치 중첩 방지) 후 `ScreenVignetteFX.Begin(cam, 색, peakAlpha 0.22, fadeIn 0.15, hold 0.35, fadeOut 1.0)` = 총 1.5s.
- **색**: FXPalette 단일 소스(기존 미소색이면 출처 리터럴 주석 — 28-U 규약). 중립 청록·어두운 톤(피격 적색/패망 골드와 구별).
- **계약**: ScreenVignetteFX 무수정(69-R7 신선 텍스처 계약 보존) · unscaled 시간(선례 승계) · EditMode 무동작 가드.

## G2 — 처치 콤보 카운터

- **구현**: 신규 `FxKillComboCounter.RegisterKill(Vector3 position)` — 3s 창(unscaled — 슬로모/히트스톱 정합) 내 연속 처치 계수, **2연속부터** 팝. 팝업 = DamageNumberRunner와 동일 OnGUI 패턴(신규 러너 — 기존 러너 무수정), "xN" 텍스트+페이드, 위치 = 최신 처치 지점 상단.
- **색**: 데미지 숫자 Critical과 동일 골드(색 소스 위임 — 구현 서브가 소스 확인 후 단일 소스 참조).
- **훅**: PlayKillVFX(CombatVFXController:806 인근, position 보유) + AnimalAI:1513 인근(transform.position) 각 1줄.
- **계약**: OnGUI 내 라인당 new 금지(static 캐시 — DamageNumberRunner 선례) · Random 0건 · 유한 수명.

## L2 — 디스토션 히트 웨이브 (Distortion 링)

- **셰이더**: 신규 `Assets/Shaders/FXDistortionRing.shader` — URP Unlit, `_CameraOpaqueTexture` 방사 UV 오프셋(링 밴드 내 리프랙션 룩) + 깊이 페이드(FXBandingRing.shader 수식 승계 — Assets/Shaders/FXBandingRing.shader). 속성 계약 스타일 준수(FXBandingRing 패밀리와 동일 명명 — `_Color`/`_MainTex`/`_EmissionColor`/`_DepthFadeDist`), rgb saturate 금지(HDR 출력).
- **에셋**: `Assets/URP/URPAsset.asset` **m_RequireOpaqueTexture 0→1** (진짜 씬 색 리프랙션 활성화 — 히트 웨이브/열기 향후 공용 인프라).
- **런타임 가드(필수)**: 스포너가 `UniversalRenderPipelineAsset`의 opaque 지원 속성을 리플렉션 확인(try/catch) — 미지원 환경에서 **조용 스킵**(씬 색 부재 = 검은 링 결함 원천 차단).
- **스포너**: 신규 `FxDistortionWave.Burst(Vector3 position, float scale)` — 링 확장 0.5→1.6m × scale, 수명 0.4s, 동시 상한 예산 가드(FxImpactLight 선례), EditMode 무동작 가드, Random 0건.
- **훅**: CombatFXGate.PlayHitFXInternal — FxSlashFlipbookFlash.Play 다음 줄, `FxDistortionWave.Burst(position, isCrit ? 1.5f : 1f)`.
- **검증 특이**: 배치 C# 컴파일은 .shader 미검증 — QA가 ShaderUtil.ShaderHasError 에디터 프로브로 셰이더 컴파일 에러 확인.

## 범위 파일 (scope files)

- 수정: `Assets/Scripts/Systems/CombatCameraEffects.cs` (W1 훅 1줄)
- 신규: `Assets/Scripts/Systems/FxCombatResidueFX.cs` (W1)
- 신규: `Assets/Scripts/Systems/FxKillComboCounter.cs` (G2)
- 수정: `Assets/Scripts/Systems/CombatVFXController.cs` (G2 훅 1줄) — **[무번호 70-U1] 문자열 무손상**
- 수정: `Assets/Scripts/Systems/AnimalAI.cs` (G2 훅 1줄)
- 신규: `Assets/Scripts/Systems/FxDistortionWave.cs` (L2)
- 신규: `Assets/Shaders/FXDistortionRing.shader` (L2)
- 수정: `Assets/Scripts/Systems/CombatFXGate.cs` (L2 훅 1줄) — [70-U3] 갱신된 호출부 문자열 무손상
- 수정: `Assets/URP/URPAsset.asset` (m_RequireOpaqueTexture 0→1)
- 신규: `Assets/Scripts/Tests/EditMode/FxCombatResidueP71Tests.cs` — W1/G2/L2 소스 계약(마커·단일 인스턴스 가드·3s 창·리플렉션 가드·URPAsset 플래그·호출부)

## 계약 주의

- 부정 잠금 = 코드형 토큰만(`UnityEngine.Random`) — 자기 주석 오염 방지(P70 선례).
- 신규 마커 `[무번호 71-W1/G2/L2]` · namespace ProjectName.Systems · FXPalette 색 단일 소스.
- PlayMode 테스트 불필요 — Play 체감은 사용자 유예. 커밋 별도 승인.

## QA 게이트

1. 컴파일 error CS 0 (qa_71_compile.log)
2. 전수 EditMode baseline(qa_70 = 2514/2504/10) 실패집합 1:1 — 신규 실패 0 (qa_71_editmode-results.xml)
3. 셰이더 ShaderUtil.ShaderHasError 프로브(에디터 진실원)
4. 집중: FxCombatResidueP71Tests + FxAttackUpgradeP70Tests(70 계약 무손상) GREEN
