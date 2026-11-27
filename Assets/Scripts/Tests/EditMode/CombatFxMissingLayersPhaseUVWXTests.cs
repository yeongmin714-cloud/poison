using NUnit.Framework;

namespace ProjectName.Tests
{
    /// <summary>
    /// [무번호 23] 전투 VFX 미계층 고도화 계약테스트 — W 시간 언어(패러독스 비네트) / V 화면(피격·판정 비네트)
    /// / U 공간(깊이 페이드/서브 밴드) / X 타이밍(시퀀서/예산 가드/몬스터 아크).
    /// 계획서: docs/plans/2026-11-12_combat-vfx-missing-layers-plan.md
    /// V3 수차 = Play 판단 연기(계획서 권장), W2 = 확인 후 보고(구조 변경 금지), 22-P3 Unlit = 이월.
    /// </summary>
    public class CombatFxMissingLayersPhaseUVWXTests
    {
        // ── Phase W ────────────────────────────────────────────────

        /// <summary>[W1/W3] 패러독스 시각 언어 — Runner의 진입/해제/정리 3박자 배선.</summary>
        [Test]
        public void W1W3_ParadoxVisualLanguage()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/ParadoxTimeStop.cs");
            Assert.IsTrue(src.Contains("ScreenVignetteFX.BeginManual(Camera.main,"),
                "정지 진입 청록 비네트(무번호 23-W1)");
            Assert.IsTrue(src.Contains("if (_visual != null) _visual.Release();"),
                "해제 릴 배선(무번호 23-W3)");
            Assert.IsTrue(src.Contains("if (_visual != null) _visual.ForceEnd();"),
                "러너 소멸 안전망 정리");
        }

        /// <summary>[W] 비네트 수동 모드 — 진입 페이드 후 Release 대기 구조.</summary>
        [Test]
        public void W_VignetteManualMode()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/ScreenVignetteFX.cs");
            Assert.IsTrue(src.Contains("public static VignettePulse BeginManual(Camera cam, Color color, float peakAlpha, float fadeIn)"),
                "BeginManual 신설(무번호 23-W)");
            Assert.IsTrue(src.Contains("public void Release() { _released = true; }"), "Release 수동 해제");
        }

        // ── Phase V ────────────────────────────────────────────────

        /// <summary>[V1] 피격 비네트 — Core 이벤트 구독 부트스트랩(Core 무수정 계약).</summary>
        [Test]
        public void V1_DamageVignette_EventWired()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/ScreenVignetteFX.cs");
            Assert.IsTrue(src.Contains("[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]"),
                "부트스트랩(무번호 23-V1)");
            Assert.IsTrue(src.Contains("ProjectName.Core.PlayerHealth.OnPlayerDamaged += OnPlayerDamagedVignette;"),
                "Core 이벤트 구독(Systems→Core 허용 방향 — Core 무수정)");
            Assert.IsTrue(src.Contains("0.32f + 0.25f * k"), "피해량 비례 강도(상한 클램프)");
            // Core 무수정 증명 — PlayerHealth에 비네트 코드 침범 없음
            var ph = System.IO.File.ReadAllText("Assets/Scripts/Core/PlayerHealth.cs");
            Assert.IsFalse(ph.Contains("ScreenVignetteFX"), "PlayerHealth(Core) 무수정 — 이벤트 구독 방식 계약");
        }

        /// <summary>[V2] 패링 PERFECT 골드 비네트 — 시퀀서 wave0 배선.</summary>
        [Test]
        public void V2_ParryGoldVignette()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/PlayerCombat.cs");
            Assert.IsTrue(src.Contains("ScreenVignetteFX.Begin(vc, new Color(FXPalette.ParryWhite.r, FXPalette.ParryWhite.g, FXPalette.ParryWhite.b, 0.25f), 0.4f, 0.05f, 0.08f, 0.3f);"),
                "PERFECT 흰 비네트 배선(무번호 23-V2 — [171차] 흰 단일)");
            Assert.IsTrue(src.Contains("[무번호 23-V2] 패링 PERFECT 흰 비네트"), "계약 마커");
        }

        // ── Phase U ────────────────────────────────────────────────

        /// <summary>[U1] 깊이 페이드 — 지형 교차 소프트 소멸(fail-open 안전 설계).</summary>
        [Test]
        public void U1_DepthFade()
        {
            var src = System.IO.File.ReadAllText("Assets/Shaders/TelegraphRing.shader");
            Assert.IsTrue(src.Contains("ShaderLibrary/DeclareDepthTexture.hlsl"), "깊이 샘플러 include(무번호 23-U1)");
            Assert.IsTrue(src.Contains("LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams)"), "장면 깊이 → 눈 깊이 변환");
            Assert.IsTrue(src.Contains("saturate((sceneEye - IN.positionHCS.w) / 0.12f)"), "경계 근거리 0.12m 소멸 곡선");
            Assert.IsTrue(src.Contains("col.a *= depthFade;"), "최종 알파 적용");
        }

        /// <summary>[U2] 밴드 서브 디테일 — 트리플 레이어 구조.</summary>
        [Test]
        public void U2_BandSubDetail()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.IsTrue(src.Contains("서브 밴드 — 밴드 내부 얇은 2차 광대"), "서브 밴드 마커(무번호 23-U2)");
            Assert.IsTrue(src.Contains("(glow + 0.18f * sub)"), "합성 배선");
        }

        // ── Phase X ────────────────────────────────────────────────

        /// <summary>[X1] 패링 시퀀서 — 계층 파열 타임라인 + EditMode 동기 폴백 + 정리.</summary>
        [Test]
        public void X1_ParrySequencer()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/PlayerCombat.cs");
            Assert.IsTrue(src.Contains("private System.Collections.IEnumerator ParryImpactSequence(ParryGrade grade, Vector3 kbDir)"),
                "시퀀서 코루틴(무번호 23-X1)");
            // [176차 갱신] 스태거(0.04s/0.02s) 폐기 — 방사형 산란(wave 1~3) 제거로 wave 0 단일 발화.
            Assert.IsTrue(src.Split(new[] { "WaitForSecondsRealtime(0.04f)" },
                System.StringSplitOptions.None).Length - 1 == 0,
                "[176차] 시퀀서 스태거 폐기 — wave 0 단일 발화(산란 요소 소멸)");
            Assert.IsTrue(src.Contains("SpawnParryImpactElements(grade, kbDir, 0);   // [176차]"),
                "EditMode 동기 폴백 wave 0 단일(테스트 안전)");
            Assert.IsTrue(src.Contains("if (_parryImpactSequence != null)   // [무번호 23-X1] 시퀀서 정리"),
                "OnDisable 정리");
            // 기존 FX 경로 — [172c] 팬 철수: 선형 1차원 팬 → 곡선 샤드(ArcWisps 승계)로 교체
            Assert.IsFalse(src.Contains("ShockwaveRingFX.SpawnParryDeflectFan(p, sparkDir.normalized, ringColor, 0.25f)"),
                "[172c] 팬 경로 철수(선형 1차원 폐기 — ArcWisps 곡선 샤드 승계)");
            Assert.IsTrue(src.Contains("SoundEffectManager.SFXType.Parry_Perfect"), "SFX 경로 보존");
        }

        /// <summary>[X2] FX 예산 가드 — 동시 링 상한 24+카운터 정합.</summary>
        [Test]
        public void X2_FxBudgetGuard()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.IsTrue(src.Contains("internal const int MaxConcurrentRings = 24;"), "상한 24(무번호 23-X2)");
            Assert.IsTrue(src.Contains("if (_activeRingCount >= MaxConcurrentRings) return;"), "가드 배선");
            Assert.IsTrue(src.Split(new[] { "if (_activeRingCount >= MaxConcurrentRings) return;" },
                System.StringSplitOptions.None).Length - 1 == 2,
                "가드 배선 2곳(확산 SpawnCore/임팩트 SpawnImpactRingCore — 무번호 25-A4 공유 예산)");
            Assert.IsTrue(src.Split(new[] { "_activeRingCount++;" }, System.StringSplitOptions.None).Length - 1 == 2,
                "증가 지점 2곳(확산/수렴) — 임팩트 링은 등가 산술 등록(아래 계약)");
            Assert.IsTrue(src.Contains("_activeRingCount = _activeRingCount + 1;"),
                "임팩트 링 예산 등록(등가 산술 — 무번호 25-A4, X2 증가 리터럴 2곳 계약 보존)");
            Assert.IsTrue(src.Split(new[] { "NotifyRingDestroyed();   // [무번호 23-X2] 예산 반환" }, System.StringSplitOptions.None).Length - 1 == 2,
                "감소 지점 2곳(Runner OnDestroy)");
        }

        /// <summary>[X3] 몬스터 스윙 아크 — 위스프 재사용 2분기 배선.</summary>
        [Test]
        public void X3_MonsterArcWisp()
        {
            var ai = System.IO.File.ReadAllText("Assets/Scripts/Systems/AnimalAI.cs");
            Assert.IsTrue(ai.Split(new[] { "ShockwaveRingFX.SpawnParryArcWisps(new Vector3(hitGround.x, hitGround.y + 0.9f, hitGround.z)," },
                System.StringSplitOptions.None).Length - 1 == 2,
                "몬스터 아크 2분기 배선(무번호 23-X3)");
            Assert.IsTrue(ai.Contains("[무번호 23-X3] 몬스터 스윙 아크"), "계약 마커");
        }
    }
}
