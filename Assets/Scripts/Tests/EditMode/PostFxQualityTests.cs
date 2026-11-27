using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [174차 P0] 전역 고품질화 계약 — 활성 파이프라인 SSAO 튜닝·Soft Shadows·글로벌 포스트 부트스트랩.
    ///
    /// 실측(174차): 활성 파이프라인 = GraphicsSettings 기본(Assets/URP/URPAsset.asset — 품질 레벨 4),
    /// 렌더러 = UniversalRendererData.asset(SSAO·AERO Volumetric Fog·Decal 피처 기존).
    /// Scenes/의 "New Universal Render Pipeline Asset"은 비활성 품질 레벨용 — 대상 아님.
    /// 에셋 경로는 GraphicsSettings → rendererDataList[0] 동적 해석(리네임 안전).
    /// </summary>
    public class PostFxQualityTests
    {
        private const string BootstrapRelPath = "Assets/Scripts/Systems/GlobalPostFxBootstrap.cs";
        private const string SetupRelPath = "Assets/Editor/PostFxQualitySetup.cs";
        private const string SsaoScriptGuid = "f62c9c65cf3354c93be831c8bc075510"; // URP 17.4 SSAO.cs

        private static UniversalRenderPipelineAsset ResolveActivePipeline()
        {
            var pipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null)
                pipeline = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            Assert.That(pipeline, Is.Not.Null, "[174] 활성 URP 파이프라인 해석");
            return pipeline;
        }

        private static string ResolveRendererAssetYaml()
        {
            var pipeline = ResolveActivePipeline();
            Assert.That(pipeline.rendererDataList.Length, Is.GreaterThan(0), "[174] 렌더러 데이터 존재");
            string rendererPath = AssetDatabase.GetAssetPath(pipeline.rendererDataList[0]);
            Assert.That(rendererPath, Is.Not.Empty, "[174] 렌더러 에셋 경로 해석");
            return File.ReadAllText(rendererPath);
        }

        private static int CountOccurrences(string source, string needle)
        {
            return source.Split(new[] { needle }, System.StringSplitOptions.None).Length - 1;
        }

        [Test]
        public void GlobalPostFxBootstrap_SourceContract()
        {
            // [174차] 메인 씬 post ON + ACES 톤매핑 + RT 카메라 제외 + 노출 불변 계약.
            string src = File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "..", BootstrapRelPath));
            Assert.That(CountOccurrences(src, "renderPostProcessing = true"), Is.GreaterThanOrEqualTo(1),
                "[174] 카메라 post ON 계약.");
            Assert.That(CountOccurrences(src, "cam.targetTexture != null"), Is.GreaterThanOrEqualTo(1),
                "[174] RT 카메라(아이콘 베이커/미니맵) 제외 계약.");
            Assert.That(CountOccurrences(src, "TonemappingMode.ACES"), Is.GreaterThanOrEqualTo(1),
                "[174] ACES 톤매핑 계약.");
            Assert.That(CountOccurrences(src, "postExposure.overrideState = false"), Is.GreaterThanOrEqualTo(1),
                "[174] 노출 불변(화이트아웃 방지) 계약.");
            Assert.That(CountOccurrences(src, "MoodProfileSetup.SaturationBoost"), Is.GreaterThanOrEqualTo(1),
                "[174] MoodProfileSetup 상수 재사용(드리프트 방지) 계약.");
            Assert.That(CountOccurrences(src, "InvokeRepeating"), Is.GreaterThanOrEqualTo(1),
                "[174] 신규 카메라 재스캔 계약.");
        }

        [Test]
        public void PostFxSetup_SourceContract()
        {
            // [174차] 에디터 셋업 SSAO 튜닝 상수 — 기존 피처 값 변경(신규 추가 아님).
            string src = File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "..", SetupRelPath));
            Assert.That(CountOccurrences(src, "SsaoIntensity = 1.4f"), Is.EqualTo(1), "[174] SSAO Intensity 튜닝 상수.");
            Assert.That(CountOccurrences(src, "SsaoRadius = 0.4f"), Is.EqualTo(1), "[174] SSAO Radius 튜닝 상수.");
            Assert.That(CountOccurrences(src, "is ScreenSpaceAmbientOcclusion found"), Is.EqualTo(1),
                "[174] 기존 피처 탐색 계약(중복 추가 금지).");
            Assert.That(CountOccurrences(src, "SsaoIntensity = 1.2f"), Is.EqualTo(0),
                "[174] 구 상수 잔존 금지.");
        }

        [Test]
        public void ActiveRenderer_SSAO_Tuned()
        {
            // [174차] 활성 렌더러의 기존 SSAO — active + 튜닝 값 실측(YAML).
            string yaml = ResolveRendererAssetYaml();
            Assert.That(CountOccurrences(yaml, SsaoScriptGuid), Is.GreaterThanOrEqualTo(1),
                "[174] SSAO 피처 존재(기존 인프라).");
            Assert.That(CountOccurrences(yaml, "Intensity: 1.4"), Is.GreaterThanOrEqualTo(1),
                "[174] SSAO Intensity 1.4 튜닝 실측.");
            Assert.That(CountOccurrences(yaml, "Radius: 0.4"), Is.GreaterThanOrEqualTo(1),
                "[174] SSAO Radius 0.4 튜닝 실측.");
        }

        [Test]
        public void UrpAsset_SoftShadows_On()
        {
            // [174차] 활성 URP 에셋 Soft Shadows ON 실측(기존 인프라 확인).
            var pipeline = ResolveActivePipeline();
            string yaml = File.ReadAllText(AssetDatabase.GetAssetPath(pipeline));
            Assert.That(CountOccurrences(yaml, "m_SoftShadowsSupported: 1"), Is.EqualTo(1),
                "[174] Soft Shadows ON 실측.");
        }
    }
}
