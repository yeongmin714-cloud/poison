using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 30] VFX 잔여 계약 — D-4 절차 디졸브(사망/소멸) + S4 선택 링 지속 호흡.
    /// 폴백/누출/결정론/84차 무손상 검증.
    /// </summary>
    public class FxDissolveS4Tests
    {
        private static string Read(string p) => System.IO.File.ReadAllText(p);

        // ── D-4a: 디졸브 셰이더 자산 ──
        [Test]
        public void Source_D4_DissolveShader()
        {
            string src = Read("Assets/Shaders/FxDissolve.shader");
            Assert.That(src, Does.Contain("Shader \"ProjectName/FX/Dissolve\""), "[30-D4] 셰이더 이름 계약.");
            Assert.That(src, Does.Contain("\"RenderPipeline\" = \"UniversalPipeline\""), "[30-D4] URP 파이프라인.");
            Assert.That(src, Does.Contain("_DissolveAmount"), "[30-D4] 램프 파라미터.");
            Assert.That(src, Does.Contain("43758.5453"), "[30-D4] 해시 노이즈(결정론 — 156차 선례).");
            Assert.That(src, Does.Contain("CBUFFER_START(UnityPerMaterial)"), "[30-D4] SRP Batcher 호환.");
            Assert.That(src, Does.Contain("clip(n - th);"), "[30-D4] 임계 소거(디졸브 본체).");
            Assert.That(src, Does.Not.Contain("Random"), "[30-D4] Random 0건.");
        }

        // ── D-4b: 유틸 폴백/누출/가드 ──
        [Test]
        public void Source_D4_UtilityFallbackAndLeak()
        {
            string src = Read("Assets/Scripts/Systems/FxDissolveUtility.cs");
            Assert.That(src, Does.Contain("[무번호 30-D4]"), "[30-D4] 마커.");
            Assert.That(src, Does.Contain("if (!Application.isPlaying) return false;"),
                "[30-D4] EditMode 가드 — 인스턴스 스왑 자체 차단.");
            Assert.That(src, Does.Contain("if (shader == null) return false;"),
                "[30-D4] 셰이더 부재 폴백(기존 사망 연출만 유지).");
            Assert.That(src, Does.Contain("Object.Destroy(inst);"),
                "[30-D4→49-T1] 머티리얼 인스턴스 파괴(누출 금지) — 승계 인스턴스 직접 파괴로 승계(.material 게터의 클론 부작용 제거).");
            Assert.That(src, Does.Contain("r.enabled = false;"),
                "[30-D4] 소멸 완료 = 렌더러 비활성(파괴는 기존 사망 시퀀스 담당).");
            Assert.That(src, Does.Not.Contain("UnityEngine.Random"), "[30-D4] Random 0건.");
        }

        // ── D-4b: Die 경로 훅 + EA2/기존 시퀀스 무손상 ──
        [Test]
        public void Source_D4_DeathHook()
        {
            string src = Read("Assets/Scripts/Systems/AnimalAI.cs");
            Assert.That(src, Does.Contain("[무번호 30-D4] 사망 디졸브"), "[30-D4] 사망 훅 마커.");
            Assert.That(src, Does.Contain("FxDissolveUtility.TryBeginDissolve(gameObject, FXPalette.MonsterDanger, 0.9f, (int)_tier)"),
                "[30-D4] 엣지색 = FXPalette 단일 소스(MonsterDanger), 0.9s 램프.");
            Assert.That(src, Does.Contain("StartCoroutine(DeathDownRoutine(respawnDelay));"),
                "[30-D4] 기존 눕힘/sink/파괴 시퀀스 무손상(EA2 계약).");
            int iHook = src.IndexOf("[무번호 30-D4] 사망 디졸브");
            int iSeq = src.IndexOf("StartCoroutine(DeathDownRoutine(respawnDelay));");
            Assert.That(iHook, Is.LessThan(iSeq), "[30-D4] 디졸브가 사망 시퀀스 시작과 동시 개시(순서 계약).");
        }

        // ── S4: 선택 유지 호흡 — 84차 무손상 설계 ──
        [Test]
        public void Source_S4_SelectedBreathing()
        {
            string src = Read("Assets/Scripts/Systems/SelectionRingController.cs");
            Assert.That(src, Does.Contain("[무번호 30-S4]"), "[30-S4] 마커.");
            Assert.That(src, Does.Contain("UpdateSelectedBreathing();"), "[30-S4] 갱신 배선.");
            Assert.That(src, Does.Contain("if (!Application.isPlaying) return;"),
                "[30-S4] Play 전용 가드 — 84차 EditMode 잠금 무영향.");
            Assert.That(src, Does.Contain("0.82f + 0.18f * FXMotion.SpawnPulse"),
                "[30-S4] 미세 호흡(0.82~1.0× — SpawnPulse 이징 재사용).");
            Assert.That(src, Does.Contain("_mat.SetColor(\"_TeamColor\", c);"),
                "[30-S4] 인스턴스 전용 기록(생성 경로/공유 머티리얼 무손상).");
            int iC1 = src.IndexOf("[무번호 19-C1]");
            int iS4 = src.IndexOf("UpdateSelectedBreathing();");
            Assert.That(iC1, Is.GreaterThanOrEqualTo(0), "[30-S4] 기존 19-C1 블록 존재 유지.");
        }

        // ── S4: 84차 생성 경로 무수정(잠금 대상 문자열 생존) ──
        [Test]
        public void Source_S4_84ContractUnharmed()
        {
            string src = Read("Assets/Scripts/Systems/SelectionRingController.cs");
            Assert.That(src, Does.Contain("public static Material CreateRingMaterial(Color color) => CommandRingPresentation.CreateMaterial(color);"),
                "[30-S4] 84차 생성 경로 무수정.");
            Assert.That(src, Does.Contain("public Color color = new Color(0.2f, 0.5f, 1f);"),
                "[30-S4] 기본 틴트 상수 무손상.");
        }

        // ── 전체 결정론 스윕 ──
        [Test]
        public void Source_30_AllDeterministic()
        {
            Assert.That(Read("Assets/Scripts/Systems/FxDissolveUtility.cs"), Does.Not.Contain("UnityEngine.Random"),
                "[30] FxDissolveUtility Random 0건.");
            Assert.That(Read("Assets/Scripts/Systems/SelectionRingController.cs"), Does.Not.Contain("UnityEngine.Random"),
                "[30] SelectionRingController Random 0건.");
        }
    }
}
