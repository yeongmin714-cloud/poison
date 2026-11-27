using NUnit.Framework;

namespace ProjectName.Tests
{
    /// <summary>
    /// [무번호 26-DF] 깊이 페이드 링 셰이더 계약테스트 — FXBandingRing(Unlit 밴드+깊이 페이드 소프트 교차).
    /// 계획서: docs/plans/2026-11-12_combat-vfx-depthfade-soldier-trail-plan.md §Phase DF.
    /// DF3 잠금 보존: "Universal Render Pipeline/Lit" 폴백 리터럴 유지(CombatFxParryFlashPhaseDTests.cs:23)
    ///   + _litShader 필드·FanRunner.Init 시그니처 무수정(CombatFxPolishPhasePQSTTests.cs:124).
    /// </summary>
    public class CombatFxDepthFadePhaseDFTests
    {
        private const string ShaderPath = "Assets/Shaders/FXBandingRing.shader";
        private const string FxPath = "Assets/Scripts/Systems/ShockwaveRingFX.cs";

        /// <summary>[DF1] 셰이더 파일 존재 + ProjectName/FXBandingRing 선언.</summary>
        [Test]
        public void DF1_ShaderFile_ExistsAndDeclared()
        {
            Assert.IsTrue(System.IO.File.Exists(ShaderPath), "FXBandingRing 셰이더 파일 존재");
            var src = System.IO.File.ReadAllText(ShaderPath);
            Assert.IsTrue(src.Contains("Shader \"ProjectName/FXBandingRing\""),
                "셰이더 이름 선언(ProjectName/FXBandingRing)");
        }

        /// <summary>[DF2] 호환 계약 4속성 — _Color/_MainTex/_EmissionColor/_DepthFadeDist 선언.</summary>
        [Test]
        public void DF2_ShaderProperties_FourDeclared()
        {
            var src = System.IO.File.ReadAllText(ShaderPath);
            Assert.IsTrue(src.Contains("_Color (\"Tint\", Color)"), "_Color(mat.color 매핑) 선언");
            Assert.IsTrue(src.Contains("_MainTex (\"Band\", 2D)"), "_MainTex(mat.mainTexture 매핑) 선언");
            Assert.IsTrue(src.Contains("_EmissionColor (\"Emission\", Color)"), "_EmissionColor(BL 발광 경로) 선언");
            Assert.IsTrue(src.Contains("_DepthFadeDist (\"Soft Distance\", Range(0.02, 0.5))"), "_DepthFadeDist 선언");
        }

        /// <summary>[DF1] 깊이 페이드 수식 토큰 — TelegraphRing.shader 선례 동일(SampleSceneDepth/LinearEyeDepth/_ScaledScreenParams).</summary>
        [Test]
        public void DF1_DepthFadeTokens_Present()
        {
            var src = System.IO.File.ReadAllText(ShaderPath);
            Assert.IsTrue(src.Contains("SampleSceneDepth"), "SampleSceneDepth(씬 깊이 샘플)");
            Assert.IsTrue(src.Contains("LinearEyeDepth"), "LinearEyeDepth(선형 눈 깊이 변환)");
            Assert.IsTrue(src.Contains("_ScaledScreenParams"), "_ScaledScreenParams(스크린 UV 정규화)");
        }

        /// <summary>[DF2 진화 68-P1] HDR 계약 변경 — 발광 가산은 유지하되 최종 rgb LDR 캡.
        ///   [계약 진화 68-P1] 구 계약("rgb 클램프 금지")은 임팩트 링 HDR emissive ×2.0과 조합될 때
        ///   프레임버퍼 포화로 피격 지점을 따라다니는 순백/암색 판을 낳았다(테스트 12~14 픽셀 실측).
        ///   발광 연출은 saturate(col.rgb + emission)로 유지 — 1.0 이하 강도에서도 블룸 흡수 작동.</summary>
        [Test]
        public void DF2_HdrEmission_NoRgbClamp()
        {
            var src = System.IO.File.ReadAllText(ShaderPath);
            Assert.IsTrue(src.Contains("col.rgb = saturate(col.rgb + _EmissionColor.rgb);"),
                "[68-P1] 발광 가산+LDR 캡(포화 차단 — 발광 연출 유지)");
            Assert.IsTrue(src.Contains("[무번호 68-P1] saturate 캡"),
                "[68-P1] 진화 근거 주석 보존");
        }

        /// <summary>[DF3] EnsureAssets 교체 — FXBandingRing 1순위 + 기존 Lit 폴백 리터럴 보존(잠금).</summary>
        [Test]
        public void DF3_EnsureAssets_FxBandingRingFirst_LitFallbackPreserved()
        {
            var src = System.IO.File.ReadAllText(FxPath);
            Assert.IsTrue(src.Contains("Shader.Find(\"ProjectName/FXBandingRing\")"),
                "링 전용 셰이더 1순위 선택");
            Assert.IsTrue(src.Contains("Shader.Find(\"Universal Render Pipeline/Lit\")"),
                "기존 URP Lit 폴백 리터럴 보존(CombatFxParryFlashPhaseDTests 잠금)");
            Assert.IsTrue(src.Contains("Shader.Find(\"Standard\")"), "Standard 폴백 유지");
        }

        /// <summary>[DF1] 규약 — Fallback Off + URP UniversalPipeline 태그.</summary>
        [Test]
        public void DF1_ShaderConvention_FallbackAndPipelineTag()
        {
            var src = System.IO.File.ReadAllText(ShaderPath);
            Assert.IsTrue(src.Contains("Fallback Off"), "Fallback Off");
            Assert.IsTrue(src.Contains("\"RenderPipeline\" = \"UniversalPipeline\""),
                "URP UniversalPipeline 태그");
        }
    }
}
