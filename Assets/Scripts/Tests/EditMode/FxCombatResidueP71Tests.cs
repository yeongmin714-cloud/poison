using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 71] 전투 후속 FX 3종 계약 — W1 전투 잔상 비네트 / G2 처치 콤보 카운터 / L2 디스토션 히트 웨이브.
    /// 소스 문자열 계약 방식(FxAttackUpgradeP70Tests 스타일). 부정 잠금은 코드형 토큰만 사용
    /// (단어형 금지 — 자기 주석 오염 주의). 계획서: docs/plans/2026-11-17_no71-combat-residue-combo-distortion-plan.md.
    /// </summary>
    public class FxCombatResidueP71Tests
    {
        private static string Read(string path)
            => System.IO.File.ReadAllText(path).Replace("\r\n", "\n");

        // ── W1: 전투 잔상 비네트 — PlayKill 훅 + 단일 인스턴스 가드 + ScreenVignetteFX 재사용 ──
        [Test]
        public void Source_W1_CombatResidue_Wired()
        {
            string src = Read("Assets/Scripts/Systems/FxCombatResidueFX.cs");
            Assert.That(src, Does.Contain("[무번호 71-W1]"), "[71-W1] 마커.");
            Assert.That(src, Does.Contain("public static void Pulse()"), "[71-W1] Pulse static 진입점.");
            Assert.That(src, Does.Contain("if (!Application.isPlaying) return;"), "[71-W1] EditMode 무동작 가드.");
            // 단일 인스턴스 가드 — 활성 창 내 재진입 무시(연속 처치 중첩 방지).
            Assert.That(src, Does.Contain("if (_runner != null) return;"), "[71-W1] 활성 창 가드 — 단일 인스턴스.");
            // static 상태 해제는 OnDestroy 단일 경로(이중 감산 방지 — FxImpactLight 감사 선례).
            Assert.That(src, Does.Contain("private void OnDestroy() { FxCombatResidueFX.ReleaseWindow(); }"),
                "[71-W1] 창 해제는 OnDestroy 단일 경로.");
            // ScreenVignetteFX.Begin 재사용 — 파라미터 스펙(peak 0.22 / fadeIn 0.15 / hold 0.35 / fadeOut 1.0).
            Assert.That(src, Does.Contain("ScreenVignetteFX.Begin(cam, FXPalette.MoveCommand, PeakAlpha, FadeInSec, HoldSec, FadeOutSec)"),
                "[71-W1] ScreenVignetteFX.Begin 재사용(23-V API) — 색은 FXPalette.MoveCommand 위임(청록 단일 소스).");
            // 부정 잠금 — 코드형 토큰만(결정론).
            Assert.That(src, Does.Not.Contain("UnityEngine.Random"), "[71-W1] 결정론 — 난수 경로 0건.");

            // PlayKill 훅 — Application.isPlaying 가드 + try/catch 1줄(전투 방해 금지 — 예외 격리 선례).
            string camFx = Read("Assets/Scripts/Systems/CombatCameraEffects.cs");
            Assert.That(camFx, Does.Contain("[무번호 71-W1]"), "[71-W1] 훅부 마커.");
            Assert.That(camFx, Does.Contain("if (Application.isPlaying) { try { FxCombatResidueFX.Pulse(); } catch (System.Exception) { } }"),
                "[71-W1] PlayKill() 진입 훅 — 가드+try/catch 격리 1줄.");
        }

        // ── W1: ScreenVignetteFX 무수정 간접 확인(69-R7 신선 텍스처 계약 보존) ──
        [Test]
        public void Source_W1_ScreenVignetteFX_Unmodified()
        {
            string src = Read("Assets/Scripts/Systems/ScreenVignetteFX.cs");
            // 본 라운드 접촉 없음 — 71 마커 부재로 간접 확인.
            Assert.That(src, Does.Not.Contain("[무번호 71"), "[71-W1] ScreenVignetteFX 본 라운드 무수정 — 71 마커 부재.");
            // 기존 API 계약 보존 — Begin 시그니처 + 신선 텍스처 경로(69-R7) 잠금 유지.
            Assert.That(src, Does.Contain("public static VignettePulse Begin(Camera cam, Color color, float peakAlpha, float fadeIn, float hold, float fadeOut)"),
                "[71-W1] Begin API 무손상(23-V).");
            Assert.That(src, Does.Contain("CreateVignetteTextureFresh()"), "[71-W1] 69-R7 신선 텍스처 계약 보존.");
        }

        // ── G2: 처치 콤보 카운터 — 3s 창(unscaled) + 2연속 게이트 + OnGUI 캐시 + 훅 2곳 ──
        [Test]
        public void Source_G2_KillComboCounter_Wired()
        {
            string src = Read("Assets/Scripts/Systems/FxKillComboCounter.cs");
            Assert.That(src, Does.Contain("[무번호 71-G2]"), "[71-G2] 마커.");
            Assert.That(src, Does.Contain("public static void RegisterKill(Vector3 position)"), "[71-G2] RegisterKill static 진입점.");
            Assert.That(src, Does.Contain("float now = Time.realtimeSinceStartup;"), "[71-G2] 3s 창 unscaled 기준(히트스톱/슬로모 정합).");
            Assert.That(src, Does.Contain("if (now - _lastKillRealtime <= WindowSec) _count++;"), "[71-G2] 창 내 연속 처치 계수.");
            Assert.That(src, Does.Contain("else _count = 1;"), "[71-G2] 창 만료 시 계수 리셋.");
            Assert.That(src, Does.Contain("if (_count < MinCombo) return;"), "[71-G2] 2연속 게이트(1연속 무연출).");
            // OnGUI 패턴 — static 스타일 캐시(DamageNumberRunner 선례 — OnGUI 내 라인당 new 금지).
            Assert.That(src, Does.Contain("private static GUIStyle _styleCache;"), "[71-G2] GUI 스타일 static 캐시.");
            Assert.That(src, Does.Contain("new GUIStyle(GUI.skin.label)"), "[71-G2] EnsureStyles 1회 생성 패턴.");
            // 색 — Critical 데미지 숫자와 동일 골드 단일 소스(FXPalette.Accent 위임).
            Assert.That(src, Does.Contain("var gold = FXPalette.Accent;"), "[71-G2] 골드색 = FXPalette.Accent 단일 소스(Critical 경로 위임).");
            // 유한 수명.
            Assert.That(src, Does.Contain("if (_elapsed >= PopupLifetime) Destroy(gameObject);"), "[71-G2] 팝업 유한 수명.");
            // 부정 잠금 — 코드형 토큰만(결정론).
            Assert.That(src, Does.Not.Contain("UnityEngine.Random"), "[71-G2] 결정론 — 난수 경로 0건.");

            // 훅 2곳 — PlayKillVFX(position 보유) + AnimalAI PlayKill 호출부 인근(transform.position).
            string vfx = Read("Assets/Scripts/Systems/CombatVFXController.cs");
            Assert.That(vfx, Does.Contain("try { FxKillComboCounter.RegisterKill(position); } catch (System.Exception) { }"),
                "[71-G2] PlayKillVFX 훅 — try/catch 격리 1줄.");
            string ai = Read("Assets/Scripts/Systems/AnimalAI.cs");
            Assert.That(ai, Does.Contain("try { FxKillComboCounter.RegisterKill(transform.position); } catch (System.Exception) { }"),
                "[71-G2] AnimalAI PlayKill 호출부 인근 훅 — try/catch 격리 1줄.");
            // 기존 데미지 넘버 러너 무수정 간접 확인(클래스 선언 잠금 유지).
            Assert.That(vfx, Does.Contain("private class DamageNumberRunner : MonoBehaviour"), "[71-G2] DamageNumberRunner 무수정(패턴 참조만).");
        }

        // ── L2: 디스토션 히트 웨이브 — 리플렉션 가드 + 예산 + FXGate 호출부 ──
        [Test]
        public void Source_L2_DistortionWave_GuardAndHook()
        {
            string src = Read("Assets/Scripts/Systems/FxDistortionWave.cs");
            Assert.That(src, Does.Contain("[무번호 71-L2]"), "[71-L2] 마커.");
            Assert.That(src, Does.Contain("public static void Burst(Vector3 position, float scale)"), "[71-L2] Burst static 진입점.");
            Assert.That(src, Does.Contain("if (!Application.isPlaying) return;"), "[71-L2] EditMode 무동작 가드.");
            // 런타임 리플렉션 가드(필수) — URP opaque 지원 속성 실존 이름 리플렉션 확인, 실패 = 조용 스킵.
            Assert.That(src, Does.Contain("type.GetProperty(\"supportsCameraOpaqueTexture\")"), "[71-L2] 리플렉션 가드 — URP 6000.x 속성명.");
            Assert.That(src, Does.Contain("type.GetProperty(\"supportsOpaqueTexture\")"), "[71-L2] 리플렉션 가드 — 구 URP 속성명 폴백.");
            Assert.That(src, Does.Contain("catch (System.Exception) { _opaqueSupported = false; }"), "[71-L2] 판정 실패 = 미지원 취급(fail-closed — 블랙 링 차단).");
            Assert.That(src, Does.Contain("if (!OpaqueTextureSupported()) return;"), "[71-L2] 미지원 조용 스킵.");
            // 동시 상한 예산 가드(FxImpactLight 선례).
            Assert.That(src, Does.Contain("if (_activeCount >= MaxConcurrentWaves) return;"), "[71-L2] 동시 상한 4 예산 가드.");
            // 셰이더 부재 null 폴백 + 표준 이징.
            Assert.That(src, Does.Contain("if (_shader == null || _quadMesh == null) return;"), "[71-L2] 셰이더 부재 null 폴백(무음 스킵).");
            Assert.That(src, Does.Contain("FXMotion.Progress(tRaw)"), "[71-L2] FXMotion.Progress 표준 이징(28-U3).");
            // 부정 잠금 — 코드형 토큰만(결정론).
            Assert.That(src, Does.Not.Contain("UnityEngine.Random"), "[71-L2] 결정론 — 난수 경로 0건.");

            // 훅 — CombatFXGate PlayHitFXInternal, 플립북 다음 줄(크리 1.5배 위계 동일). 기존 잠금 문자열 무손상.
            string gate = Read("Assets/Scripts/Systems/CombatFXGate.cs");
            Assert.That(gate, Does.Contain("FxSlashFlipbookFlash.Play(position, isCrit ? 1.5f : 1f, isCrit);"),
                "[71-L2] 기존 [37-S3]/[70-U3] 잠금 호출부 무손상.");
            Assert.That(gate, Does.Contain("FxDistortionWave.Burst(position, isCrit ? 1.5f : 1f);"),
                "[71-L2] FXGate 훅 — 크리 1.5배 위계.");
            int slash = gate.IndexOf("FxSlashFlipbookFlash.Play(position, isCrit ? 1.5f : 1f, isCrit);", System.StringComparison.Ordinal);
            int wave = gate.IndexOf("FxDistortionWave.Burst(position, isCrit ? 1.5f : 1f);", System.StringComparison.Ordinal);
            Assert.That(slash, Is.GreaterThanOrEqualTo(0), "[71-L2] 플립북 호출부 존재.");
            Assert.That(wave, Is.GreaterThan(slash), "[71-L2] 웨이브 호출은 플립북 다음(계획서 배치 계약).");
        }

        // ── L2: 셰이더 계약(_CameraOpaqueTexture/_DepthFadeDist) + URPAsset 플래그 ──
        [Test]
        public void Source_L2_ShaderAndURPAssetFlag()
        {
            string shader = Read("Assets/Shaders/FXDistortionRing.shader");
            Assert.That(shader, Does.Contain("[무번호 71-L2]"), "[71-L2] 셰이더 마커.");
            Assert.That(shader, Does.Contain("_CameraOpaqueTexture"), "[71-L2] 씬 색 리프랙션 샘플러.");
            Assert.That(shader, Does.Contain("_DepthFadeDist"), "[71-L2] 깊이 페이드 속성(FXBandingRing 패밀리 명명 승계).");
            Assert.That(shader, Does.Contain("_Color"), "[71-L2] 패밀리 속성 계약 — _Color.");
            Assert.That(shader, Does.Contain("_MainTex"), "[71-L2] 패밀리 속성 계약 — _MainTex.");
            Assert.That(shader, Does.Contain("_EmissionColor"), "[71-L2] 패밀리 속성 계약 — _EmissionColor.");
            // HDR 계약 — 최종 rgb 클램프 없음(부정 — 코드형 토큰만).
            Assert.That(shader, Does.Not.Contain("saturate(col.rgb"), "[71-L2] rgb 클램프 금지(HDR 유지 — 계약).");

            // URPAsset — 활성 자산(m_RequireDepthTexture:1과 동일 파일)의 opaque 플래그 ON.
            string urp = Read("Assets/URP/URPAsset.asset");
            Assert.That(urp, Does.Contain("m_RequireDepthTexture: 1"), "[71-L2] 활성 URP 자산 확인(m_RequireDepthTexture:1 동일 파일).");
            Assert.That(urp, Does.Contain("m_RequireOpaqueTexture: 1"), "[71-L2] m_RequireOpaqueTexture 0→1(씬 색 활성화).");
            Assert.That(urp, Does.Not.Contain("m_RequireOpaqueTexture: 0"), "[71-L2] 구값 0 잔존 금지(단일 플래그 갱신).");
        }

        // ── 기존 계약 무손상 — 70-U1 / 37-S3·33-A1 / 28-M2 잠금 문자열 보존 ──
        [Test]
        public void Source_ExistingContracts_Unharmed()
        {
            // [무번호 70-U1] 잠금 문자열 전수(FxAttackUpgradeP70Tests 잠금 — 대표 문자열 승계 확인).
            string vfx = Read("Assets/Scripts/Systems/CombatVFXController.cs");
            Assert.That(vfx, Does.Contain("[무번호 70-U1]"), "[70-U1] 마커 보존.");
            Assert.That(vfx, Does.Contain("var inst = Object.Instantiate(m);"), "[70-U1] 인스턴스 클론 계약 보존.");
            Assert.That(vfx, Does.Contain("const float FlashDuration = 0.10f;"), "[70-U1→80-P7] 플래시 지속 계약 보존(0.08→0.10 — 림 실루엣 창 합일).");

            // [무번호 37-S3]/[33-A1] 잠금 문자열 보존.
            string gate = Read("Assets/Scripts/Systems/CombatFXGate.cs");
            Assert.That(gate, Does.Contain("[무번호 37-S3]"), "[37-S3] 마커 보존.");
            Assert.That(gate, Does.Contain("if (isCrit || damage >= 15f) FxGroundCrackFX.Burst(position);"), "[37-H3] 훅 계약 보존.");
            Assert.That(gate, Does.Contain("FxImpactLight.Spike(position, FXPalette.AttackPrimary, isCrit ? 4.2f : 2.2f, 3.8f, 0.18f);"), "[33-A1 갱신 79-R20] 라이트 펄스 계약 보존(통일색).");

            // [무번호 28-M2] CombatCameraEffects 기존 본문 리터럴 보존(DeathFeedbackPhaseMTests 잠금 승계).
            string camFx = Read("Assets/Scripts/Systems/CombatCameraEffects.cs");
            Assert.That(camFx, Does.Contain("Instance.PlayHitShake(Instance._hitShakeIntensity);"), "[28-M2] PlayHit 본문 보존.");
            Assert.That(camFx, Does.Contain("BulletTime.Get().Apply(0.4f, 0.08f, 0.10f);"), "[28-M2] PlayCrit 본문 보존.");
            Assert.That(camFx, Does.Not.Contain("UnityEngine.Random"), "[28-M] 결정론 계약 보존.");
        }

        // ── [80-P4] 임팩트 프레임 — 크리 전용 hit-stop 게이트(HitStopManager 승계) ──
        [Test]
        public void Source_P4_HitStop_CritOnly()
        {
            string gate = Read("Assets/Scripts/Systems/CombatFXGate.cs");
            Assert.That(gate, Does.Contain("[무번호 80-P4]"), "[80-P4] 마커.");
            Assert.That(gate, Does.Contain("HitStopManager.RequestHitStop(0.06f, 0.05f);"),
                "[80-P4] 크리 전용 hit-stop — 0.05 스케일 0.06s(HitStopManager 승계 — 재진입 가드+쿨다운+FrozenScaleGuard 내장).");
            // 크리 분기 내 배치 — PlayCrit 직후(임팩트 프레임 동기).
            int crit = gate.IndexOf("CombatCameraEffects.PlayCrit();", System.StringComparison.Ordinal);
            int stop = gate.IndexOf("HitStopManager.RequestHitStop(0.06f, 0.05f);", System.StringComparison.Ordinal);
            Assert.That(crit, Is.GreaterThanOrEqualTo(0), "[80-P4] PlayCrit 호출부 존재.");
            Assert.That(stop, Is.GreaterThan(crit), "[80-P4] hit-stop은 크리 분기 내(PlayCrit 직후).");
            // 일반 히트 미개입 — PlayHit(else 분기)가 hit-stop 이후(크리 분기 밖) 배치 계약.
            int hit = gate.IndexOf("CombatCameraEffects.PlayHit();", System.StringComparison.Ordinal);
            Assert.That(hit, Is.GreaterThan(stop), "[80-P4] 일반 히트는 hit-stop 미개입(else 분기 — 위계 유지).");
        }

        // ── [80-P4] BulletTime 딥 프리즈 존중 가드 — 가드 없으면 dip lerp가 프리즈를 즉시 덮어써 사망 ──
        [Test]
        public void Source_P4_BulletTime_FreezeGuard()
        {
            string bt = Read("Assets/Scripts/Systems/BulletTime.cs");
            Assert.That(bt, Does.Contain("[무번호 80-P4]"), "[80-P4] BulletTime 가드 마커.");
            // Update 가드 — 프리즈 창에서 타이머/쓰기 정지(딥 프리즈가 지배).
            Assert.That(bt, Does.Contain("if (Time.timeScale <= 0.05f) return;"),
                "[80-P4] 프리즈 가드(<= 0.05 — HitStopManager FrozenScaleGuard 선례) — Update+Apply 양쪽.");
            // Apply 흡수 가드 — 프리즈 중 재적용 시 _phaseStartScale 오염 방지(역디핑 왜곡 방지).
            Assert.That(bt.IndexOf("if (Time.timeScale <= 0.05f) return;", System.StringComparison.Ordinal)
                < bt.IndexOf("private void Update()", System.StringComparison.Ordinal),
                "[80-P4] 첫 가드는 Apply 내(캡처 오염 방지), 둘째는 Update 내(쓰기 정지) — 순서 계약.");
        }

        // ── [80-P5] additive 셰이크 — 코루틴→LateUpdate(order 1000) 전환(팔로우 클로버링 수리) ──
        [Test]
        public void Source_P5_Shake_AdditiveLateUpdate()
        {
            string camFx = Read("Assets/Scripts/Systems/CombatCameraEffects.cs");
            Assert.That(camFx, Does.Contain("[DefaultExecutionOrder(1000)]"),
                "[80-P5] LateUpdate 지배 — TopDownCameraController.LateUpdate 팔로우 이후 실행.");
            Assert.That(camFx, Does.Contain("[무번호 80-P5]"), "[80-P5] 마커.");
            // additive 계약 — 마지막 적용분 strip 후 신규 오프셋 얹기(포즈 덮어쓰기 없음 — Task 5.2).
            Assert.That(camFx, Does.Contain("Vector3 basePos = _mainCamera.transform.localPosition - _shakeLastOffset;"),
                "[80-P5] additive strip — 팔로우 출력 위에 얹음(포즈 덮어쓰기 금지).");
            Assert.That(camFx, Does.Contain("_mainCamera.transform.localPosition -= _shakeLastOffset;"),
                "[80-P5] 종료 시 오프셋 제거 원복 — stale-capture 스냅백 없음.");
            // 코루틴 경로 소멸 — 클로버링 원인 제거(소스에 coroutine 셰이크 잔존 금지).
            Assert.That(camFx, Does.Not.Contain("StartCoroutine(ShakeRoutine"),
                "[80-P5] 코루틴 셰이크 소멸(Update 후 실행은 팔로우 LateUpdate에 클로버링 — 근원 제거).");
            // stale-capture 원인 제거 — Awake/Start 고정 캡처는 팔로우 이동 시 스냅백 버그.
            Assert.That(camFx, Does.Not.Contain("_originalCamLocalPos"),
                "[80-P5] stale-capture 필드 소멸(additive 전환 — 팔로우 이동 후 스냅백 방지).");
            // 기존 계약 보존 — PlayHit/PlayCrit 본문 리터럴(DeathFeedbackPhaseMTests 잠금 승계).
            Assert.That(camFx, Does.Contain("Instance.PlayHitShake(Instance._hitShakeIntensity);"),
                "[28-M2] PlayHit 본문 보존.");
            Assert.That(camFx, Does.Contain("BulletTime.Get().Apply(0.4f, 0.08f, 0.10f);"),
                "[28-M2] PlayCrit 본문 보존.");
        }
    }
}
