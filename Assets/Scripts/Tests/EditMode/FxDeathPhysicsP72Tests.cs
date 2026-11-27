using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 72] 사망 물리 + 타격 소리 위계 계약 — W2 몬스터 사망 관절 이완 1단계(절차 본 회전, 리그 프리즈/원복)
    /// + W3 타격 소리 피치 변조(PlaySFX pitch 선택 파라미터 + 결정론 밴드 매핑).
    /// 소스 문자열 계약 방식(FxAttackUpgradeP70Tests 스타일). 부정 잠금은 코드형 토큰만 사용(단어형 금지 — 자기 주석 오염 주의).
    /// </summary>
    public class FxDeathPhysicsP72Tests
    {
        private static string Read(string path)
            => System.IO.File.ReadAllText(path).Replace("\r\n", "\n");

        /// <summary>start 문자열 이후부터 end 문자열 직전까지 절단(메서드 범위 잠금용 — P70 선례).</summary>
        private static string Cut(string src, string start, string end)
        {
            int s = src.IndexOf(start, System.StringComparison.Ordinal);
            Assert.That(s, Is.GreaterThanOrEqualTo(0), $"범위 시작 마커 존재: {start}");
            int e = src.IndexOf(end, s + start.Length, System.StringComparison.Ordinal);
            Assert.That(e, Is.GreaterThan(s), $"범위 종료 마커 존재: {end}");
            return src.Substring(s, e - s);
        }

        // ── W2: FxDeathRelax — 절차 본 회전 이완 유틸(물리엔진 미사용) ──
        [Test]
        public void Source_W2_FxDeathRelax_ProceduralBoneRelax()
        {
            string src = Read("Assets/Scripts/Systems/FxDeathRelax.cs");
            Assert.That(src, Does.Contain("[무번호 72-W2]"), "[72-W2] 마커.");
            // ① 진입점 — Begin(GameObject, duration) static 유틸.
            Assert.That(src, Does.Contain("public static void Begin(GameObject target, float duration)"),
                "[72-W2] Begin static 진입점 시그니처.");
            // ② 사지 본 선별 — SkinnedMeshRenderer.bones 이름 패턴 매치(대소문자 무시).
            Assert.That(src, Does.Contain("GetComponentsInChildren<SkinnedMeshRenderer>(true)"),
                "[72-W2] 자식 SMR 전수 스캔.");
            Assert.That(src, Does.Contain("boneName.ToLowerInvariant().Contains(token)"),
                "[72-W2] 본 이름 대소문자 무시 매치.");
            Assert.That(src, Does.Contain("bool isArm = NameMatches(b.name, \"arm\") || NameMatches(b.name, \"hand\");"),
                "[72-W2] 팔 본 패턴 — Arm/ForeArm(부분 일치)/Hand.");
            Assert.That(src, Does.Contain("NameMatches(b.name, \"foot\") || NameMatches(b.name, \"paw\")"),
                "[72-W2] 다리 본 패턴 — Foot/Paw.");
            Assert.That(src, Does.Contain("NameMatches(b.name, \"leg\") || NameMatches(b.name, \"shin\")"),
                "[72-W2] 다리 본 패턴 — Leg/Shin.");
            // ③ 사망 개시 시각 캡처 + Slerp — localRotation 캡처 후 이완 포즈로 보간.
            Assert.That(src, Does.Contain("capList.Add(b.localRotation);"),
                "[72-W2] 사망 개시 시각 localRotation 캡처.");
            Assert.That(src, Does.Contain("Quaternion.Slerp(_captured[i], _relaxed[i], p)"),
                "[72-W2] 캡처→이완 포즈 Slerp 보간.");
            // ④ 결정론 상수 — 팔 40° 드롭 / 다리 20° 굽힘(로컬 오일러).
            Assert.That(src, Does.Contain("public const float ArmDropDegrees = 40f;"), "[72-W2] 팔 40° 드롭 상수.");
            Assert.That(src, Does.Contain("public const float LegBendDegrees = 20f;"), "[72-W2] 다리 20° 굽힘 상수.");
            // ⑤ 이징 재사용 규약 — FXMotion.Progress(재구현 금지).
            Assert.That(src, Does.Contain("FXMotion.Progress(_t / _duration)"),
                "[72-W2] FXMotion.Progress 표준 이징 재사용.");
            // ⑥ 가드/폴백 — EditMode 무동작 + 본 0건 조용 스킵.
            Assert.That(src, Does.Contain("if (!Application.isPlaying) return;"), "[72-W2] EditMode 무동작 가드.");
            Assert.That(src, Does.Contain("if (boneList.Count == 0) return false;"), "[72-W2] 사지 본 0건 조용 스킵(폴백).");
            // ⑦ 유한 수명 러너 — 자가 파괴 + OnDestroy 단일 감산(FxImpactLight 선례, 누출 금지).
            Assert.That(src, Does.Contain("if (_t >= _duration) Destroy(gameObject);"), "[72-W2] 유한 수명 자가 파괴.");
            Assert.That(src, Does.Contain("private void OnDestroy()"), "[72-W2] OnDestroy 소멸 경로 존재.");
            Assert.That(src, Does.Contain("FxDeathRelax.Release();"), "[72-W2] OnDestroy 단일 감산 경로(누출 금지).");
            // ⑧ 결정론 — 부정 잠금(코드형 토큰만).
            Assert.That(src, Does.Not.Contain("UnityEngine.Random"), "[72-W2] 결정론 — 난수 경로 0건.");
            Assert.That(src, Does.Not.Contain("Rigidbody"), "[72-W2] 물리엔진 미사용 — 절차 본 회전만.");
        }

        // ── W2: AnimalAI 배선 — 이완 훅(FX 격리) + 리그 프리즈/원복 3줄 ──
        [Test]
        public void Source_W2_AnimalAI_Hook_Freeze_Restore()
        {
            string src = Read("Assets/Scripts/Systems/AnimalAI.cs");
            Assert.That(src, Does.Contain("[무번호 72-W2]"), "[72-W2] 배선부 마커.");
            // ① StartDeathSequence 훅 — try/catch FX 격리(사망 후처리 방해 금지, 71-G2 선례).
            Assert.That(src, Does.Contain("try { FxDeathRelax.Begin(gameObject, 0.35f); } catch (System.Exception) { }"),
                "[72-W2] 사망 이완 훅 — try/catch 격리 1줄, 0.35s(눕힘 0.5s 창과 겹침).");
            // ② DeathDownRoutine 프리즈 — SetStateImmediate(Idle) 인근, 컴포넌트 인스턴스 토글만.
            string down = Cut(src, "private IEnumerator DeathDownRoutine", "private void Respawn()");
            Assert.That(down, Does.Contain("if (_rigAnim != null) _rigAnim.SetStateImmediate(AnimationState.Idle);"),
                "[72-W2] 기존 Idle 즉시 전이 유지(현행 무손상).");
            Assert.That(down, Does.Contain("if (_rigAnim != null) _rigAnim.enabled = false;"),
                "[72-W2] 리그 프리즈 — 본 구동 충돌 제거(DeathDownRoutine 진입 시).");
            // ③ Respawn 원복 — 리스폰 후 정상 구동.
            string respawn = Cut(src, "private void Respawn()", "private void OnDrawGizmosSelected()");
            Assert.That(respawn, Does.Contain("if (_rigAnim != null) _rigAnim.enabled = true;"),
                "[72-W2] 리그 원복 — Respawn에서 enabled=true.");
            // ④ 기존 잠금 무손상 — [무번호 71-G2] 처치 콤보 훅 전수 보존.
            Assert.That(src, Does.Contain("[무번호 71-G2]"), "[71-G2] 마커 보존.");
            Assert.That(src, Does.Contain("try { FxKillComboCounter.RegisterKill(transform.position); } catch (System.Exception) { }"),
                "[71-G2] 처치 콤보 훅 문자열 보존.");
        }

        // ── W2: 애니 시스템 무수정 간접확인 — RigAnimationController/HumanoidClipDriver 파일에 72 마커 부재 ──
        [Test]
        public void Source_W2_AnimSystemFiles_Untouched()
        {
            // "애니 시스템 무수정" 계약 — 컴포넌트 enabled 토글만 허용(AnimalAI), 파일 수정 금지.
            // 간접확인: 두 파일에 [무번호 72] 마커가 존재하면 파일 수정이 발생한 것(마커 부재 단언).
            string rig = Read("Assets/Scripts/Systems/RigAnimationController.cs");
            Assert.That(rig, Does.Not.Contain("[무번호 72"), "[72-W2] RigAnimationController.cs 파일 무수정(마커 부재).");
            string driver = Read("Assets/Scripts/Systems/HumanoidClipDriver.cs");
            Assert.That(driver, Does.Not.Contain("[무번호 72"), "[72-W2] HumanoidClipDriver.cs 파일 무수정(마커 부재).");
        }

        // ── W3: SoundManagerEnhanced — PlaySFX pitch 선택 파라미터 확장 ──
        [Test]
        public void Source_W3_SoundManagerEnhanced_PlaySFX_PitchParam()
        {
            string src = Read("Assets/Scripts/Systems/SoundManagerEnhanced.cs");
            Assert.That(src, Does.Contain("[무번호 72-W3]"), "[72-W3] 마커.");
            // ① 선택 파라미터 확장 — 기본값 1.0 = 기존 호출부 전부 무수정·무영향.
            Assert.That(src, Does.Contain("public void PlaySFX(string clipName, float volume = 1.0f, float pitch = 1.0f)"),
                "[72-W3] PlaySFX pitch 선택 파라미터(기본 1.0).");
            // ② 변조 진입점 — 재대입 방식(이전 변조 잔존 없음, 자기 치유).
            Assert.That(src, Does.Contain("_sfxSource.pitch = pitch;"), "[72-W3] 소스 단위 피치 재대입.");
            // ③ 기존 로직 유지 — PlayOneShot 재생 + 플레이스홀더 로그 경로 보존.
            Assert.That(src, Does.Contain("_sfxSource.PlayOneShot(clip, vol);"), "[72-W3] PlayOneShot 재생 로직 유지.");
            Assert.That(src, Does.Contain("SFX 요청: {clipName} (플레이스홀더 — clip 없음)"),
                "[72-W3] 플레이스홀더 로그 경로 보존(클립 부재 크래시 없음).");
            // ④ 기존 호출부 무수정 — PlaySFXByType 위임 경로 유지.
            Assert.That(src, Does.Contain("PlaySFX(clipName, volume);"), "[72-W3] PlaySFXByType 기존 호출부 무수정.");
        }

        // ── W3: ImpactSoundFX — damage 파라미터 + 결정론 피치 밴드 매핑 ──
        [Test]
        public void Source_W3_ImpactSoundFX_DamageBandPitch()
        {
            string src = Read("Assets/Scripts/Systems/ImpactSoundFX.cs");
            Assert.That(src, Does.Contain("[무번호 72-W3]"), "[72-W3] 마커.");
            // ① 선택 파라미터 — damage 기본 -1(구호환, 기존 2인자 호출 호환).
            Assert.That(src, Does.Contain("public static void PlayHit(bool isCrit, bool isKill, float damage = -1f)"),
                "[72-W3] PlayHit damage 선택 파라미터(구호환 -1).");
            // ② 밴드 상수 3종 + 크리 가산 + 상한 — 결정론 매핑 테이블.
            Assert.That(src, Does.Contain("private const float PitchBandLow = 0.92f;"), "[72-W3] 경타 밴드 피치 0.92.");
            Assert.That(src, Does.Contain("private const float PitchBandMid = 1.00f;"), "[72-W3] 중간 밴드 피치 1.00.");
            Assert.That(src, Does.Contain("private const float PitchBandHigh = 1.06f;"), "[72-W3] 강타 밴드 피치 1.06.");
            Assert.That(src, Does.Contain("private const float PitchCritMul = 1.04f;"), "[72-W3] 크리 피치 가산 ×1.04.");
            Assert.That(src, Does.Contain("private const float PitchCeiling = 1.15f;"), "[72-W3] 피치 상한 1.15 클램프.");
            Assert.That(src, Does.Contain("private const float BandLowThreshold = 8f;"), "[72-W3] 경타/중간 밴드 경계 8.");
            Assert.That(src, Does.Contain("private const float BandHighThreshold = 15f;"), "[72-W3] 중간/강타 밴드 경계 15.");
            // ③ 구호환 — damage < 0 → 1.0 기준.
            Assert.That(src, Does.Contain("float band = damage < 0f"), "[72-W3] 구호환(damage<0) 1.0 기준 분기.");
            // ④ 크리 가산 + 상한 클램프.
            Assert.That(src, Does.Contain("if (isCrit) band *= PitchCritMul;"), "[72-W3] 크리 ×1.04 가산.");
            Assert.That(src, Does.Contain("Mathf.Clamp(band, 0f, PitchCeiling)"), "[72-W3] 상한 1.15 클램프.");
            // ⑤ HighSpec 게이트 유지 — Balanced 무음 계약.
            Assert.That(src, Does.Contain("if (!ActionFeel.HighSpec) return;"), "[72-W3] HighSpec 게이트 유지.");
            // ⑥ 모든 레이어 pitch 전달 — Play(clipId, volume, pitch) 8레이어 전수.
            Assert.That(src, Does.Contain("Play(SFX_KILL, 1.0f, pitch);"), "[72-W3] kill 메인 레이어 pitch 전달.");
            Assert.That(src, Does.Contain("Play(SFX_CRIT, 0.55f, pitch);"), "[72-W3] kill 서브 레이어 pitch 전달.");
            Assert.That(src, Does.Contain("Play(SFX_RATTLE, 0.45f, pitch);"), "[72-W3] kill 래틀 레이어 pitch 전달.");
            Assert.That(src, Does.Contain("Play(SFX_CRIT, 0.9f, pitch);"), "[72-W3] crit 메인 레이어 pitch 전달.");
            Assert.That(src, Does.Contain("Play(SFX_THUD, 0.5f, pitch);"), "[72-W3] crit 바디 레이어 pitch 전달.");
            Assert.That(src, Does.Contain("Play(SFX_RATTLE, 0.4f, pitch);"), "[72-W3] crit 래틀 레이어 pitch 전달.");
            Assert.That(src, Does.Contain("Play(SFX_THUD, 0.8f, pitch);"), "[72-W3] hit 메인 레이어 pitch 전달.");
            Assert.That(src, Does.Contain("Play(SFX_RATTLE, 0.35f, pitch);"), "[72-W3] hit 래틀 레이어 pitch 전달.");
            // ⑦ 단일 재생 경로 계약 — 헬퍼가 PlaySFX 3인자로 위임.
            Assert.That(src, Does.Contain("sm.PlaySFX(clipId, volume, pitch);"), "[72-W3] 재생 위임 — PlaySFX pitch 전달.");
            Assert.That(src, Does.Contain("재생 경로는 SoundManagerEnhanced.PlaySFX(string, float) 단 하나."),
                "[72-W3] 단일 재생 경로 주석 계약 보존.");
            // ⑧ 결정론 — 부정 잠금(코드형 토큰만).
            Assert.That(src, Does.Not.Contain("UnityEngine.Random"), "[72-W3] 결정론 — 난수 경로 0건.");
        }

        // ── W3: CombatFXGate 호출부 — damage 전달 1줄 + 기존 FX 체인 잠금 보존 ──
        [Test]
        public void Source_W3_CombatFXGate_CallSite_DamageWired()
        {
            string gate = Read("Assets/Scripts/Systems/CombatFXGate.cs");
            Assert.That(gate, Does.Contain("[무번호 72-W3]"), "[72-W3] 호출부 마커.");
            // ① 갱신된 호출부 — damage 파라미터 전달(스코프 내 float damage).
            Assert.That(gate, Does.Contain("ImpactSoundFX.PlayHit(isCrit, isKill: false, damage);"),
                "[72-W3] PlayHit 호출부 damage 전달.");
            Assert.That(gate, Does.Contain("int damageInt = Mathf.RoundToInt(damage);"),
                "[72-W3] damage 파라미터 스코프 존재(피해량 원본 전달).");
            // ② HighSpec 블록 내 유지 — 게이트 경로 무손상.
            int hs = gate.IndexOf("if (ActionFeel.HighSpec)", System.StringComparison.Ordinal);
            int call = gate.IndexOf("ImpactSoundFX.PlayHit(isCrit, isKill: false, damage);", System.StringComparison.Ordinal);
            Assert.That(hs, Is.GreaterThanOrEqualTo(0), "[72-W3] HighSpec 게이트 블록 존재.");
            Assert.That(call, Is.GreaterThan(hs), "[72-W3] 호출부 HighSpec 블록 내 유지.");
            // ③ 기존 잠금 무손상 — 70-U3 슬래시/71-L2 웨이브/33-A1 라이트 호출 문자열 전수 보존.
            Assert.That(gate, Does.Contain("FxSlashFlipbookFlash.Play(position, isCrit ? 1.5f : 1f, isCrit);"),
                "[70-U3] 슬래시 플립북 호출 보존.");
            Assert.That(gate, Does.Contain("FxDistortionWave.Burst(position, isCrit ? 1.5f : 1f);"),
                "[71-L2] 디스토션 웨이브 호출 보존.");
            Assert.That(gate, Does.Contain("FxImpactLight.Spike(position, FXPalette.AttackPrimary"), "[33-A1 갱신 79-R20] 임팩트 라이트 호출 보존(통일색).");
        }

        // ── 기존 계약 무손상 — 인접 파일 잠금 문자열 전수 보존 확인 ──
        [Test]
        public void Source_Locks_ExistingContractsPreserved()
        {
            // [70-U1] 히트 틴트 — CombatVFXController 본 계약(미수정 파일, 승계 확인).
            string vfx = Read("Assets/Scripts/Systems/CombatVFXController.cs");
            Assert.That(vfx, Does.Contain("[무번호 70-U1]"), "[70-U1] 마커 보존.");
            Assert.That(vfx, Does.Contain("\"HitFlashTint70\""), "[70-U1] 히트 틴트 관문 ID 보존.");
            // 사망 디졸브/불티 기존 경로 — AnimalAI 현행 무손상(이완 훅은 추가만, 기존 라인 편집 없음).
            string ai = Read("Assets/Scripts/Systems/AnimalAI.cs");
            Assert.That(ai, Does.Contain("FxDissolveUtility.TryBeginDissolve(gameObject, FXPalette.MonsterDanger, 0.9f, (int)_tier);"),
                "[39-T4] 사망 디졸브 경로 보존.");
            Assert.That(ai, Does.Contain("FxEmberRise.Burst(transform.position);"), "[33-D2] 소멸 불티 경로 보존.");
        }
    }
}
