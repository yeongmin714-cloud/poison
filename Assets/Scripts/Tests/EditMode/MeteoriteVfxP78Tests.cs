using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests
{
    /// <summary>
    /// [무번호 78-P1] Meteorite VFX 배선 계약테스트 — 강타격 보완 레이어(4단계 시퀀스).
    /// 계획서: no78 P1 (MeteoriteVFXRunner 신규 + CombatFXGate 배선 1라인).
    /// 소스 계약 패턴(FxEnvReactionP73Tests 선례) — EditMode 안전(정적 문자열 검증 + 스폰 0건 가드).
    ///   T1: Resources.Load 경로 문자열 ↔ 실제 에셋 존재 일치(7종 .vfx + 3 셰이더그래프 — File.Exists)
    ///   T2: Burst 미호출 시 인스턴스 0
    ///   T3: Burst 호출 후 동시 인스턴스 ≤ 6(예산 가드)
    ///   T4: [무번호 76-S3] 주석 카운트 보존(CombatFXGate.cs 문자열 grep — 1)
    ///   T5: 수명 스케줄 — 0.4s/1.2s 시간 계수 결정론(unscaled)
    ///   T6: LightFlash 스폰 시점 = GroundImpact와 동일 프레임(t=0.12s)
    ///   T7: ImpactBuffer 발화 금지(정적 검증 — Runner 본문에 ImpactBuffer 리소스 경로 미존재)
    /// </summary>
    public class MeteoriteVfxP78Tests
    {
        private static string Read(string path) => System.IO.File.ReadAllText(path);

        private static string RunnerSrc => Read("Assets/Scripts/Systems/MeteoriteVFXRunner.cs");
        private static string GateSrc => Read("Assets/Scripts/Systems/CombatFXGate.cs");

        private static int Count(string src, string needle)
            => src.Split(new[] { needle }, System.StringSplitOptions.None).Length - 1;

        // ── T1: 에셋 존재 + 경로 문자열 일치 ─────────────────────────

        /// <summary>[T1] 7종 .vfx + 3 셰이더그래프 실재(File.Exists) + Runner 경로 상수 정합(확장자 제외).</summary>
        [Test]
        public void T1_ResourcePaths_MatchImportedAssets()
        {
            // 7종 .vfx — 부모 임포트 완료 상태(Assets/Resources/FX/Meteorite/VFX/)
            string[] vfxFiles =
            {
                "01_Anticipation", "02_Meteorite", "03_GroundImpact", "04_GroundImpactAdds",
                "MeteoriteMain", "ImpactBuffer", "LightFlash"
            };
            foreach (var name in vfxFiles)
                Assert.IsTrue(System.IO.File.Exists($"Assets/Resources/FX/Meteorite/VFX/{name}.vfx"),
                    $"VFX 에셋 실재: {name}.vfx");

            // 3 셰이더그래프 — Assets/Resources/FX/Meteorite/Shaders/
            string[] shaderGraphs = { "BubbleDissolve", "MeteoriteBlastMesh", "MeteoriteTest" };
            foreach (var name in shaderGraphs)
                Assert.IsTrue(System.IO.File.Exists($"Assets/Resources/FX/Meteorite/Shaders/{name}.shadergraph"),
                    $"셰이더그래프 실재: {name}.shadergraph");

            // Runner 경로 상수 — Resources.Load 규약(에셋명에서 확장자 제외) 정합
            var src = RunnerSrc;
            Assert.IsTrue(src.Contains("\"FX/Meteorite/VFX/01_Anticipation\""), "Runner 경로: 01_Anticipation");
            Assert.IsTrue(src.Contains("\"FX/Meteorite/VFX/03_GroundImpact\""), "Runner 경로: 03_GroundImpact");
            Assert.IsTrue(src.Contains("\"FX/Meteorite/VFX/04_GroundImpactAdds\""), "Runner 경로: 04_GroundImpactAdds");
            Assert.IsTrue(src.Contains("\"FX/Meteorite/VFX/LightFlash\""), "Runner 경로: LightFlash");
        }

        // ── T2/T3: 인스턴스 수 계약 ─────────────────────────────────

        /// <summary>[T2] Burst 미호출 시 Meteorite 인스턴스 0 — EditMode 테스트 씬 기준(스폰 가드와 무관하게 0).</summary>
        [Test]
        public void T2_NoBurstCall_ZeroInstances()
        {
            int count = 0;
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (go.name.StartsWith("MeteoriteVFX_")) count++;
            Assert.AreEqual(0, count, "Burst 미호출 상태에서 Meteorite 인스턴스 0");
        }

        /// <summary>[T3] Burst 호출 후 동시 인스턴스 ≤ 6 — EditMode 가드로 스폰 0건(≤ 6 충족) +
        /// 예산 가드 상수/검사 코드 정적 검증(MaxConcurrentInstances == 6).</summary>
        [Test]
        public void T3_Burst_ConcurrentInstances_WithinBudget()
        {
            // EditMode 호출 — Runner의 !Application.isPlaying 가드로 스폰 0건(77 선례).
            ProjectName.Systems.MeteoriteVFXRunner.Burst(new Vector3(0f, 0f, 0f), true);

            int count = 0;
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (go.name.StartsWith("MeteoriteVFX_")) count++;
            Assert.LessOrEqual(count, 6, "동시 인스턴스 상한 6(예산 가드)");

            // 예산 가드 코드 정적 검증 — 상수 값과 가드 분기 존재(스폰 전 초과 스킵 + 1회 경고).
            var src = RunnerSrc;
            Assert.AreEqual(6, ProjectName.Systems.MeteoriteVFXRunner.MaxConcurrentInstances,
                "동시 활성 인스턴스 상한 상수 값(X2 링 예산 계약)");
            Assert.IsTrue(src.Contains("if (_activeCount >= MaxConcurrentInstances)"), "스폰 전 예산 가드 분기");
            Assert.IsTrue(src.Contains("_budgetWarned"), "예산 초과 1회 경고(static bool)");
        }

        // ── T4: 기존 주석 문자열 보존 ───────────────────────────────

        /// <summary>[T4] [무번호 76-S3] 주석 카운트 보존 — CombatFXGate.cs grep 카운트 전후 동일(1).</summary>
        [Test]
        public void T4_GateCommentCount_Preserved()
        {
            Assert.AreEqual(1, Count(GateSrc, "[무번호 76-S3]"),
                "[무번호 76-S3] 주석 문자열 카운트 보존(최소 침범 배선 — 기존 라인 무변경)");
            // 신규 배선 라인 존재 확인(부가 검증 — 기존 라인 뒤 추가).
            Assert.IsTrue(GateSrc.Contains("MeteoriteVFXRunner.Burst(position, isCrit);   // [무번호 78-P1]"),
                "[무번호 78-P1] 배선 라인 존재");
        }

        // ── T5: 수명/시간 계수 결정론 ───────────────────────────────

        /// <summary>[T5] 수명 스케줄 결정론 — 0.4s/1.2s 시간 계수 + unscaled 단일 언어(Random 0건).</summary>
        [Test]
        public void T5_LifetimeConstants_DeterministicUnscaled()
        {
            var src = RunnerSrc;
            Assert.AreEqual(0.4f, ProjectName.Systems.MeteoriteVFXRunner.AnticipationLifetimeSec, "예고 수명 0.4s");
            Assert.AreEqual(1.2f, ProjectName.Systems.MeteoriteVFXRunner.ImpactLifetimeSec, "임팩트 계열 수명 1.2s");
            Assert.AreEqual(0.12f, ProjectName.Systems.MeteoriteVFXRunner.GroundImpactDelaySec, "1차 임팩트 지연 0.12s");
            Assert.AreEqual(0.08f, ProjectName.Systems.MeteoriteVFXRunner.AddsGapSec, "1차→2차 파장 간격 0.08s");
            Assert.AreEqual(0.6f, ProjectName.Systems.MeteoriteVFXRunner.AnticipationScale, "예고 스케일 0.6");
            Assert.AreEqual(1.5f, ProjectName.Systems.MeteoriteVFXRunner.CritScaleMultiplier, "크리 위계 1.5배");

            // unscaled 단일 언어 — WaitForSecondsRealtime(지연) + unscaledDeltaTime(수명 카운트다운).
            Assert.IsTrue(src.Contains("WaitForSecondsRealtime"), "지연 스케줄 unscaled(77 선례)");
            Assert.IsTrue(src.Contains("Time.unscaledDeltaTime"), "수명 카운트다운 unscaled([23-X1] 규약)");
            Assert.IsFalse(src.Contains("WaitForSeconds("), "scaled 지연 금지(unscaled 계약)");
            // 결정론 — Random 0건(계약).
            Assert.That(src, Does.Not.Contain("UnityEngine.Random"), "결정론 — 난수 0건");
            Assert.That(src, Does.Not.Contain("System.Random"), "결정론 — 난수 0건");
        }

        // ── T6: LightFlash = GroundImpact 동일 프레임 ───────────────

        /// <summary>[T6] LightFlash 스폰 시점 = GroundImpact와 동일 프레임 — 두 SpawnStage 호출 사이에
        /// 지연 yield가 없음(첫 번째 WaitForSecondsRealtime과 두 번째 사이 배치).</summary>
        [Test]
        public void T6_LightFlash_SameFrameAsGroundImpact()
        {
            var src = RunnerSrc;
            int yield1 = src.IndexOf("WaitForSecondsRealtime(GroundImpactDelaySec)");
            int giSpawn = src.IndexOf("SpawnStage(GroundImpactPath");
            int lfSpawn = src.IndexOf("SpawnStage(LightFlashPath");
            int yield2 = src.IndexOf("WaitForSecondsRealtime(AddsGapSec)");
            int addsSpawn = src.IndexOf("SpawnStage(GroundImpactAddsPath");

            Assert.Greater(yield1, -1, "1차 지연 yield 존재(0.12s)");
            Assert.Greater(yield2, -1, "2차 지연 yield 존재(0.08s 간격)");
            // 시퀀스 순서: yield1 → GroundImpact 스폰 → LightFlash 스폰 → yield2 → Adds 스폰.
            Assert.Less(yield1, giSpawn, "GroundImpact는 첫 yield 이후(t=0.12s)");
            Assert.Less(giSpawn, lfSpawn, "LightFlash는 GroundImpact 직후");
            Assert.Less(lfSpawn, yield2, "LightFlash와 GroundImpact 사이에 지연 yield 없음(동일 프레임)");
            Assert.Less(yield2, addsSpawn, "Adds는 두 번째 yield 이후(t=0.12s+0.08s)");
        }

        // ── T7: ImpactBuffer 발화 금지 ──────────────────────────────

        /// <summary>[T7] ImpactBuffer.vfx 발화 금지 — Runner 본문에 ImpactBuffer 리소스 경로 미존재(P1 후속 — 예산 계산 미검증).</summary>
        [Test]
        public void T7_ImpactBuffer_NotFired()
        {
            var src = RunnerSrc;
            Assert.IsFalse(src.Contains("FX/Meteorite/VFX/ImpactBuffer"),
                "Runner 본문에 ImpactBuffer 리소스 경로 미존재(발화 금지 — P1 후속 계약)");
            Assert.IsFalse(src.Contains("ImpactBufferPath"), "ImpactBuffer 경로 상수 미존재");
            // 발화 금지 사유 주석만 존재(문서화 — 계약 흔적).
            Assert.IsTrue(src.Contains("ImpactBuffer"), "발화 금지 사유 주석 존재(문서화)");
        }
    }
}
