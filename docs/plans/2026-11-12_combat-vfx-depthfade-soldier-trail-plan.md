# 전투 VFX 깊이 페이드 + 병사 스윙 트레일 계획 (무번호 26 계획서 — 2026-11-12)

## 배경

- 사용자 지시: 추가 고품질화 경로 요청 후 "진행" 승인 — 추천안 A-1(깊이 페이드)+A-2(병사 리본) 실행.
- 무번호 25 완료 상태: BL 발광 파이프라인(HDR 임시션 ×1.5/×2.5)·EA1 개성 틴트·EA2 사망 FX 배선·IN1 날씨 먼지·FA2 스콜치 고도화 구현+QA 통과(2208/2198/10 — 신규 실패 0).
- 실측 기반 범위 확정:
  - **23-U1 깊이 페이드는 TelegraphRing.shader에만 존재**(L152-157 — SampleSceneDepth/LinearEyeDepth 0.12m 소프트 페이드, fail-open). ShockwaveRingFX 패밀리는 URP/Lit라 깊이 페이드 불가 — 본 라운드로 확장.
  - **WeaponSwingTrail(45차 P4)은 플레이어 전용 static** — 병사는 HumanoidClipDriver 경로에서 SetEmitting을 호출해도 _trail null로 무음(no-op 실측: EnsureBareFist는 `mode == DriveMode.Player` 가드 — 병사 미부착 확정). AttackArcVFX.Swing(프리미엄 아크)은 양 모드 공유 발화 — 리본과 병존(아크=잔상 호, 리본=궤적, 다른 연출).

## 중복 셀프감사 결과

- TelegraphRing 셰이더 패턴(절차 셰이더+폴백 계약+깊이 페이드) = 신규 FX 셰이더의 선례 재사용 — 중복 구현 아님.
- 병사 스윙 FX 기존: 22-S2 스윙 지면 파동+23-X3 곡선 아크 — **휘적 리본(TrailRenderer) 부재**가 본 갭.
- VFX Graph/히트 디스토션/로컬 라이트 펄스 = 미채택(비용·결정론 계약 — 별도 검토 라운드).
- 사운드 = ImpactSoundFX/AttackSoundLayerManager/텔레그래프 경고 SFX 기존 — 제외.

## Phase DF — FX 링 셰이더 + 깊이 페이드(전 링 동시 수혜)

- **DF1 신규 셰이더** `Assets/Shaders/FXBandingRing.shader`(ProjectName/FXBandingRing) — TelegraphRing.shader 규약 복제: URP UniversalPipeline/Transparent/ZWrite Off/Blend SrcAlpha OneMinusSrcAlpha/Cull Off/SRP Batcher(CBUFFER UnityPerMaterial).
- **DF2 호환 계약(기존 코드 무수정 원칙)**: 프로퍼티명 `_Color`(mat.color 매핑)·`_MainTex`(mat.mainTexture 매핑)·`_EmissionColor`(BL ApplyEmissionBoost 그대로 유효)·`_DepthFadeDist`. frag: tex2D(_MainTex)×_Color + _EmissionColor 가산(rgb saturate 없음 — HDR 출력 유지, 알파만 saturate) + 깊이 페이드(Telegraph L152-157 동일 수식, fail-open).
- **DF3 EnsureAssets 교체**: `Shader.Find("ProjectName/FXBandingRing")` 1순위 → 기존 Lit 폴백 유지(**`Shader.Find("Universal Render Pipeline/Lit")` 리터럴 보존 필수** — CombatFxParryFlashPhaseDTests.cs:23 잠금). `_litShader` 필드명·FanRunner Init 시그니처 무수정(CombatFxPolishPhasePQSTTests.cs:124 잠금).
- **DF4 수혜**: 확산 링/echo/수렴 링/스윙 스트릭/위스프 LineRenderer 머티리얼이 _litShader를 공유하므로 전 패밀리 소프트 교차+Unlit 발광 룩 — 22-P3 "Unlit 전환 Play 비교 대기"의 실질 해소안(본 라운드가 비교 안 제공).
- 색 계약 무손상: 틴트/밴드 텍스처/P3 부스트/BL 발광 경로 전부 기존 코드 그대로(셰이더만 교체).

## Phase ST — 병사 스윙 휘적 리본

- **ST1 신규 컴포넌트** `Assets/Scripts/Systems/SoldierSwingTrail.cs`(MonoBehaviour, 인스턴스별) — 무기 GO 자식에 TrailRenderer(팁 앵커 = bounds 최장축 그립 반대편 끝, WeaponEquipManager/WeaponSwingTrail.Attach 수식 재사용), 청백색(0.75,0.92,1.0 — 패망 청 계열), width 3키 곡선/0.22s(BOTW 규격 동일), URP Particles/Unlit additive.
- **ST2 머티리얼 중복 금지**: WeaponSwingTrail.CreateTrailMaterial에 public 위임자 추가(additive 변경만 — 사양 동일 보장), SoldierSwingTrail은 이를 재사용.
- **ST3 부착 배선**: GuardVisualAttachSystem 병사 무기 GLB Instantiate 직후 SoldierSwingTrail 부착(좌표는 브리프에서 사전 실측). 무기 부재 병사는 손 본 폴백(EnsureBareFist 선례).
- **ST4 발화 분기**: HumanoidClipDriver의 WeaponSwingTrail.SetEmitting 4곳(레거시 진입/탈출·콤보 시작/종료)을 `SetSwingTrailEmitting(bool)` 헬퍼로 분기 — mode==DriveMode.Soldier이면 root의 SoldierSwingTrail(캐시), 아니면 기존 static 경로. **`WeaponSwingTrail.SetEmitting(` 문자열 잠금 보존**(분기 내 문자열 유지) — 테스트 사전검사 결과 동봉.
- **ST5 예산**: 리본 동시 상한 간단 가드(활성 병사 리본 수) — 23-X2 선례의 인스턴스판(과도 스폰 방지).

## 진행 방식

- DF/ST 병렬 위임(파일 소유권 분리: DF=ShockwaveRingFX+셰이더 / ST=SoldierSwingTrail+WeaponSwingTrail+GuardVisualAttachSystem+HumanoidClipDriver), 컴파일은 부모 단일 실행.
- 검증 흐름 불변: 구현+계약테스트 → 컴파일 error CS 0 + EditMode baseline(10건 1:1) → Play 판정(별도 승인).
- Play 판정 항목: DF 링 외형(Unlit 전환 체감 — 기존 Lit 대비), 깊이 페이드 강도(0.12m), ST 병사 리본 체감/색, 기존 누적 미판정(TestOutput/diff-25-QA.md 및 Play 체크리스트 문서 참조).
- 결정론/Play·커밋 별도 승인/위조 배선 금지 유지.

## 판단 항목(사용자 선택 대기)

1. DF 링 외형 Unlit 전환 체감 — Play 비교 후 확정(폴백 라인 1개로 즉시 원복 가능).
2. ST 병사 리본 색(청백 제안)·수명 — Play 판정.
3. VFX Graph 도입 검토 라운드 별도 진행 여부.
