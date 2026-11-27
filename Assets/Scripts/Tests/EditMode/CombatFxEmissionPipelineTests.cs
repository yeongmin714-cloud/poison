using NUnit.Framework;

namespace ProjectName.Tests
{
    /// <summary>
    /// [무번호 25-BL] 발광 파이프라인 계약테스트 — ShockwaveRingFX 확산/수렴 링에 HDR 임시션 부여
    /// (EmissionIntensityBase 1.5 / EmissionIntensityLarge 2.5 등급화, Bloom Threshold 0.9 흡수 유도).
    /// 계획서: docs/plans/2026-11-12_combat-vfx-glow-pipeline-round25-plan.md §Phase BL.
    /// BL3 색 계약 무손상(알베도 color 경로·P3 밝기 부스트 유지), Unlit 전환 금지(Play 비교 대기).
    /// </summary>
    public class CombatFxEmissionPipelineTests
    {
        /// <summary>[BL1] 발광 강도 상수 — 일반 ×1.5 / 대형 ×2.5 등급화(Play 판정 대기값).</summary>
        [Test]
        public void BL1_EmissionIntensity_Constants()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.IsTrue(src.Contains("internal const float EmissionIntensityBase = 1.5f;"),
                "일반 링 발광 강도 상수(제안 ×1.5)");
            Assert.IsTrue(src.Contains("internal const float EmissionIntensityLarge = 2.5f;"),
                "대형 링 발광 강도 상수(등급화 ×2.5)");
        }

        /// <summary>[BL1] ApplyEmissionBoost 헬퍼 — _EMISSION 키워드+HDR 발광색+GI 비기여.</summary>
        [Test]
        public void BL1_ApplyEmissionBoost_Wired()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.IsTrue(src.Contains("private static void ApplyEmissionBoost(Material mat, Color tint, float radius)"),
                "헬퍼 메서드 시그니처");
            Assert.IsTrue(src.Contains("mat.EnableKeyword(\"_EMISSION\")"),
                "발광 키워드 활성화");
            Assert.IsTrue(src.Contains("SetColor(\"_EmissionColor\""),
                "HDR 발광색 설정");
            Assert.IsTrue(src.Contains("MaterialGlobalIlluminationFlags.EmissiveIsBlack"),
                "GI 비기여(EmissiveIsBlack)");
        }

        /// <summary>[BL1] SpawnCore 배선 — 밴드 텍스처 할당 이후 발광 부여 호출.</summary>
        [Test]
        public void BL1_SpawnCore_EmissionCall()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.IsTrue(src.Contains("ApplyEmissionBoost(mat, color, maxRadius);"),
                "확산 링 발광 호출");
            int idxTex = src.IndexOf("mat.mainTexture = EnsureBandTexture();");
            int idxBoost = src.IndexOf("ApplyEmissionBoost(mat, color, maxRadius);");
            Assert.IsTrue(idxTex > 0 && idxBoost > 0 && idxBoost > idxTex,
                "mainTexture 할당보다 뒤에서 호출");
        }

        /// <summary>[BL1] SpawnConvergingRingCore 배선 — 수렴 링에도 동일 발광 부여.</summary>
        [Test]
        public void BL1_ConvergingRing_EmissionCall()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.IsTrue(src.Contains("ApplyEmissionBoost(mat, color, startRadius);"),
                "수렴 링 발광 호출(무번호 25-BL1)");
        }

        /// <summary>[BL3] 색 계약 무손상 — P3 대형 링 밝기 부스트 문자열 유지(본 라운드 미변경).</summary>
        [Test]
        public void BL3_ColorContract_Preserved()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.IsTrue(src.Contains("if (maxRadius >= 1.0f) color = new Color(color.r * 1.15f,"),
                "P3 부스트 문자열 유지(CombatFxPolishPhasePQSTTests 잠금 계약)");
        }

        /// <summary>[BL2] 발광 등급화 경계 — 반경 1m 기준 삼항 판정.</summary>
        [Test]
        public void BL2_EmissionBoundary_RadiusTernary()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.IsTrue(src.Contains("float intensity = radius >= 1.0f ? EmissionIntensityLarge : EmissionIntensityBase;"),
                "발광 강도 등급화 삼항(≥1m 경계)");
        }
    }
}
