# [167차] 히트 임팩트 = Magic Hit 2 원형 복원 + 히트음 강화 + Free Slash 재부착

상태: **계획 승인 대기** (구현 0건 — 본 문서는 실측 결과와 계획만 담음)
사용자 요구 (11-21 텔레그램):
1. 피격 시 퍼지는 보라색 파티클 대신 **Magic Hit 2 에셋**이 부착되게
2. "Magic Hit 2가 안 나온다" — 배치 프로브에선 나온다고 했지만 유저 화면에선 안 보임
3. 효과음을 **더 "때리는 듯한"** 사운드로
4. **Free Slash 에셋 여전히 미부착** — 확인 후 계획

---

## 실측 결과 (읽기전용, 2026-11-21)

### ① Magic Hit 2는 "부착돼 있지만" 에셋 원형이 아니다
- 배선 정상: `CombatFXGate.cs:123` → `SlashVFXRunner.PlayImpactMulti(position, type, isCrit ? 1.5f : 1f)` → `Resources/FX/Impact/MagicHit.prefab` (Magic Hit 2 수동 복사본, 실존 474,720B).
- 프리팹 구성: 파티클 시스템 **4개** — 루트(burst 파티클) / `Simbol`(마법진) / `Shockwave`(링) / `Flash`(섬광). 전부 활성.
- 그러나 런타임에서 **재색상 레이어 3종이 에셋 고유 룩을 덮어씀**:
  - `[무번호 41-b]` `SlashVFXRunner.cs:598-625` — 보라 판별 시스템의 startColor를 **딥퍼플(0.45,0.25,0.85)로 강제** + 원본 텍스처를 스무스 매직 서클로 교체(L612-620).
  - `[무번호 40-b]` L618 — HDR 과노출(네온 핑크) 방지 틴트 강등.
  - `NormalizePurpleParticles` L636 — 잔여 보라를 **골드화이트(1,0.9,0.6)** 로 교체(`IsPurple` L806-807, `ToGoldWhite` L810-811).
  - `[무번호 38-F1R]` L595 — "보라 지면 링 = 의도적 히트 시그니처(사용자 재판정 11-15)" — 6일 전 판정. **현재 유저 판정(11-21)과 충돌** → 재판정 필요.
- 결론: 유저가 보는 "보라색 파티클 퍼짐" = 딥퍼플 강제된 루트 burst + 링. 에셋 데모의 풀 임팩트(진+링+섬광+버스트)로 안 읽힘. 166차 2.5× 트림(`ImpactBaseScale`, L576)은 크기만 건드림 — 색/구성은 그대로.

### ② 히트 사운드 현황
- 현재 유일한 히트음 = `[166-D] MonsterHitSfx.cs:20` → `SFX_Combat_Hit` 단일 클립 (Resources/Audio/SFX/SFX_Combat_Hit.wav 실존). 무기별 pitch/vol 프로파일(AttackSoundLayerManager.ProfileOf) + ±6% 지터.
- `ImpactSoundFX.cs`(레이어드 impact_thud/crit/kill/rattle)는 **클립 전무** — `Resources/Sounds/SFX/`에 Gather만 존재 → 플레이스홀더 로그 후 무음 경로.
- Guz 히트 팩·Free Slash 팩에 동봉 오디오 0건. 기존 미수용 오디오는 Fire/Trail 계열(히트음 부적합).

### ③ Free Slash 스윙 아크 — 호출부 주석 상태
- 리소스 실존: `Resources/FX/Slash/Slash VFX.prefab` (478,075B) + 러너 경로 생존(`PlaySlashStage` → `[60차] Free Slash 우선 전환` L223-235 → `SpawnFallbackSlashStage`) + 계약테스트 존재(AttackSwingJudgmentContractTests.cs:63-65).
- **호출부 주석**: `HumanoidClipDriver.cs:1750-1754` — `[2026-09-25 공격 FX] 사용자 지정: 스윙 슬래쉬 아크 에셋 제거 — 공격 연출을 젤다식 무기 궤적 흰 트레일(WeaponSwingTrail.SetEmitting)로 일원화... 복원 필요 시 아래 주석만 해제.`
- 현재 스윙 비주얼 = WeaponSwingTrail(흰 트레일) + FxWeaponGlow 펄스만. 아크 0건.

---

## 계획

### Phase A — Magic Hit 2 원형 복원 (피격 임팩트)
- **A1. 재색상 레이어 임팩트 경로 제외**: L598-625(41-b 딥퍼플 강제+서클 교체)과 L636(NormalizePurpleParticles)을 PlayImpactMulti에서 제거 — 에셋 고유 머티리얼/색으로 렌더. 단 `NormalizeMagicHitMaterials`(L627, [69] URP 스트림 정합)과 `DetectShaderErrorOnce`는 **유지**(마젠타/블랙사각 안전망).
- **A2. 시스템별 실측**: HitForensic166Probe 재사용 — 4 시스템(루트/Simbol/Shockwave/Flash) 격리 발화 캡처 → 유저 테스트씬 카메라 기준 화면 점유율/색 실측. 안 나오는 시스템 확정 후 상수 트림(스케일/앵커)만 적용.
- **A3. 계약 갱신**: HitDefectClosure_66Tests(딥퍼플 강제 어서션) → "에셋 원형 유지" 계약으로 교체(66/69 회귀 방지).

### Phase B — 히트 사운드 펀치업 (판정 필요 — 아래 ②)
- **B안(신규 에셋)**: 무료 임팩트 SFX 수용(주먹/참격 히트 계열) → `SFX_Combat_Hit` 교체 or `impact_thud`/`impact_crit` 수용으로 ImpactSoundFX 레이어드 경로 활성(일반=thud+rattle, 크리=crit+thud+rattle — 72-W3 피치 위계 그대로 유효화).
- **C안(무수용 트림)**: SFX_Combat_Hit 유지 + 저피치 레이어(동일 클립 pitch 0.8 동시 재생 = 저역 "쿵" 추가) + impactVol 프로파일 상향 — 새 에셋 0건.
- 어느 쪽이든 MonsterHitSfx 훅 위치(데미지 적용 직후) 무수정 — 훅은 166차 검증 완료.

### Phase C — Free Slash 스윙 아크 재부착
- **C1.** `HumanoidClipDriver.cs:1754` 주석 해제(1줄) — `[60차]` Free Slash 우선 경로가 자동 발화. 쿨다운/부착/스테이지 롤/사거리 스케일 규격은 기존 그대로.
- **C2. 중첩 정책 판정 필요 — 아래 ③**: (a) 트레일 유지 + 아크 추가 (b) 아크 단독(트레일 OFF).

### Phase D — 검증 게이트
- 컴파일 error CS 0 + EditMode 전수(baseline 10건 부분집합 대조, 신규 실패 0) + A2 캡처 판독 → **사용자 Play 일괄 판정**(히트 룩/사운드/아크 3종). 커밋 별도 승인.

---

## 판정 필요 (구현 전 답변 대기)
1. **보라 처리 방향** — 에셋 원형(퍼플 마법 임팩트) 그대로 복원이 맞는지, 아니면 보라 자체를 빼고 골드화이트 통일인지. (요구 문구는 "에셋 부착"으로 읽힘 → 기본안 = 원형 복원)
2. **사운드** — B안(무료 SFX 팩 1개 수용, 정공법) vs C안(기존 클립 레이어링 트림).
3. **스윙 중첩** — 흰 트레일 유지+아크 추가 vs 아크 단독.
