# 감사보고서 + 임포트·배선 계획: Unity VFX Samples (무번호 78 — 트랙 A)

**사용자 지시(2026-11-18)**: "A로 진행" — Unity-Technologies/VisualEffectGraph-Samples 다운로드 → 전수 감사 → 임포트·배선 계획.
**라이선스**: Unity Companion License (원문 확인 — Unity 프로젝트 내 상업 사용 허용, 재판매 금지는 게임 배포에 무관).
**소스**: commit bcd8004, 1.9GB, Assets/Samples 하위 16샘플·49 .vfx.

---

## 1. 전수 감사 결과 (147차 절차 준용)

### 1-1. 파이프라인 호환성 (핵심 양호)

- .vfx 49개 전수 grep: **HDRP 직접 참조 0건**, DepthFade/CameraDepth 0건
- 출력 타입: **쿼드 출력 중심(VFXQuadOutput)** — URP 호환 표준 출력. StaticMeshOutput 1건만
- **리프랙션 1건**: SmokePortal/VFX_CrystalsStack — **블랙뿌리 교훈 적용: URP OpaqueTexture 가드 필수** (fail-closed 스킵)
- 서브그래프: MagicBook SubGraphs 2종(.vfxoperator) — 샘플 내부 의존, 동반 임포트 필요
- m_GraphVersion: 18 (2019~2020 대산물) → VFX Graph 17.4 임포트 시 **자동 업그레이드 경로**. 버전 갭 리스크 명시(컴파일 후 재검증 필요)

### 1-2. 교차 의존성 (GUID 전수 — 외부 참조 = 임포트 파손 후보)

| 샘플 | vfx | 외부의존 | 판정 |
|---|---|---|---|
| Meteorite | 14 | **0** | 자가완결 — 최우선 |
| MagicBook | 8 | **0** | 자가완결 — 최우선 |
| SmokePortal | 6 | **0** | 자가완결 (환경메시·텍스처 124M은 별도) |
| BonFire | 3 | 1 (HDRP/Steam.asset) | SSS 프로파일 — **환경메시 머티리얼 전용**, 파티클 무영향 |
| GrassWind | 2 | 2 (HDRP SSS/Steam) | 동상 |
| RibbonPack | 1 | 1 (HDRP SSS) | 동상 |
| EllenSkinnedMesh | 7 | 4 (Ellen 텍스처) | Ellen 캐릭터 텍스처 — Common 폴더 동반 필요 |
| GooBall | 1 | 1 (MagicBook RedSmoke) | 샘플 간 의존 |
| Butterflies | 1 | 1 (Common LensDirt) | 텍스처 1장 복사로 해소 |
| Portal/GenieMagicLamp/MorphingFace/EllenHologram | 5 | 0 | 자가완결 |

- HDRP/Steam·SSS01.asset = HDRP **SubsurfaceScatteringProfile** ScriptableObject — 파티클 .vfx가 아니라 샘플 환경메시 머티리얼이 참조. URP 임포트 시 환경메시는 표준으로 폴백(무해) — VFX 자체는 영향 0
- 결론: **.vfx 49개 전부 URP 로드 가능한 구조** — 환경 메시/씬/포스트프로세스만 제외하면 됨

### 1-3. 텍스처 규모 (임포트 무게)

| 샘플 | 텍스처 | 용량 | 비고 |
|---|---|---|---|
| SmokePortal | 36 | **124M** | 환경 바위 Albedo 6M급 — VFX만 추출 시 대폭 경량화 |
| Meteorite | 32 | 23M | 지면 임팩트 텍스처 포함 |
| GenieMagicLamp | 8 | 23M | 모래 FX |
| MagicBook | 25 | 15M | 경량 |
| BonFire | 6 | 10M | 경량 |

---

## 2. 전투·세계관 적합 랭킹 (용도 매핑)

| 순위 | 샘플 | .vfx | 용도 매핑 |
|---|---|---|---|
| 1 | **Meteorite** | 14 | **4단계 타격 시퀀스** (Anticipation→Meteorite→GroundImpact→Adds) — 23-X1 패링 시퀀서/강타격 웨이브 구조와 정확히 동형. ImpactBuffer(GPU 버퍼 임팩트) 포함 |
| 2 | **MagicBook** | 8 | 화염 잔불(FireEmbers)·체인·트레일·액체·연기 — 마법/향 계열 보완 레이어. 자가완결+경량 |
| 3 | **SmokePortal** | 6 | 브래지어 화염(BraziersFlames)·연기 포탈·수정 — 횃불/장작 앰비언트. 리프랙션 1건 가드 |
| 4 | **BonFire** | 3 | 화염·비행 잎사귀·구름 — 캠프파이어/가을 잎사귀 앰비언트 (73-E2 스웨이 연계) |
| 5 | **EllenSkinnedMesh** | 7 | **Desintegration(스킨 메시 기화)** — 72 사망 이완 후 몬스터 소멸 연출. Ellen 텍스처 의존 대체 필요 |
| 6 | GenieMagicLamp | 2 | 모래 FX — 사막/모래 지형 존 예약 |
| 7 | Butterflies/GrassWind | 3 | 잔디 바람·나비 — 야외 앰비언트 |
| 8 | Portal/RibbonPack/GooBall/그 외 | 5 | 포탈·리본 — 보류 |

---

## 3. 임포트·배선 계획 (4단계 — 단계당 별도 라운드)

### P1 — Meteorite 전투 임팩트 (1순위)

- **임포트 범위**: `Assets/Samples/Meteorite/VFX/` 전체(14 .vfx) + `Materials/VFXShaders/`(7 .shadergraph) + 필요 텍스처. 씬/라이팅/포스트프로세스/환경 메시·나무·풀 **제외**(URP 미사용)
- **경로**: `Assets/Resources/VFX/VFXSamples/Meteorite/` — 147차 규칙: **.meta 수기 작성 금지**(Unity 임포트 재생성)
- **배선**: 강타격 시퀀서(23-X1) 구조에 4단계 매핑 — t=0 Anticipation → GroundImpact 1차 → +0.12s Adds(77 리플 뒤). 기존 MagicHit 룩 무변경 계약 — **추가 레이어**로만
- **가드**: VFX Graph 쿼드 출력 예산(동시 인스턴스 상한), LDR 캡(66·68 계약), 76/77 링 예산 공유 유지
- **검증**: 컴파일 error CS 0 + 전수 EditMode baseline 1:1 + .vfx 자동 업그레이드 후 실패 0건 확인(런타임 로그)

### P2 — MagicBook 화염·마법 레이어

- FireEmbers(화염 잔불 — 폭발 연소 보완)·Trails(트레일)·Smoke. 서브그래프 2종 동반. 자가완결 — 외부의존 0
- BombExplosionVisual 화염(FirePath VFX_Fire_01_Big) 위 보완 레이어

### P3 — SmokePortal BraziersFlames + BonFire 앰비언트

- 횃불/화염 앰비언트 — 마을·던전 조명 소품 연계. 환경 바위 메시·텍스처 124M **제외**(VFX 그래프+연기 텍스처만)
- VFX_CrystalsStack 리프랙션 → OpaqueTexture 가드 or 제외 판정

### P4 — EllenSkinnedMesh Desintegration (몬스터 소멸)

- 스킨 메시 기화 — 72 사망 이완 후 잔존 시체 소멸 연출. Ellen 텍스처 의존을 자국 몬스터 텍스처로 대체하는 재작업 필요 → **P4는 별도 설계 후 판정**

### 공통 계약 (전 단계)

- 기존 프리팹 룩 무변경(MagicHit 사용자 선택) — 신규는 추가 레이어만
- 사각 금지·Random 0건(결정론)·X2 링 예산 계약·LDR 캡 유지
- .meta 무기 작성 금지 + 신규 .cs 스크립트만 컴파일 게이트 대상
- Unity Companion License credit 기록 (CREDITS.md 또는 문서 헤더)

---

## 4. 리스크 명시

1. **버전 갭**: m_GraphVersion 18 → 17.4 자동 업그레이드 — 대부분 무해하지만 일부 그래프(특히 GPU 버퍼 사용 Meteorite/ImpactBuffer)는 수동 확인 필요. P1 임포트 후 런타임 로그 검증 필수
2. **리프랙션**: VFX_CrystalsStack 1건 — OpaqueTexture 가드(블랙뿌리 교훈) fail-closed
3. **HDRP SSS**: 환경메시 머티리얼 폴백 — 파티클 무영향이지만 환경 메시를 임포트하지 않음으로써 원천 차단
4. **무게**: SmokePortal 환경 텍스처 124M — 부분 임포트로 경량화

**상태**: 감사 완료 — P1 임포트·배선은 승인 후 착지.
