using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [계획 v2 Phase 4] AERO 볼류메트릭 포그 실내 게이트 계약 단정.
    ///
    /// GPU 제약 해제(사용자 확정 2026-10-09 — 메인 실행은 별도 데스크톱)로 AERO 포그를
    /// 실내 전용 게이트로 도입한다. 계약:
    ///  ①포그 머티리얼 기본 _Density=0(야외·테스트씬 새지 않음) + 웜톤 튜닝값
    ///  ②UniversalRendererData에 "AERO Volumetric Fog" FullScreenPass feature 부착(injectionPoint 550 — AERO 데모 동일)
    ///  ③컨트롤러는 활성 씬==IndoorScene 게이트 + AERO _FrameCount 계약(IGN 안정화)
    ///  ④실내 화광 강도 0.55→0.85(IndoorScene 3개) — 포그 속 빛기둥 가시화
    ///  ⑤Rendering/AeroLocalizedGas*(가스 격리 파이프라인) 파일 미건드림(별도 경로 유지).
    ///
    /// 소스 계약 테스트 스타일 — HudBottomAnchorTests/FigmaCanvasLayoutTests 선례.
    /// </summary>
    public class IndoorAeroFogTests
    {
        private const string FogMaterialGuid = "b7491fa00c4047029c7a4e0a49791479";
        private const string FeatureFileId = "-7219846203561004741";
        private const string ExistingSsaoFeatureFileId = "-512343952430453960";

        [Test]
        public void IndoorFogMaterial_DefaultsToZeroDensity_AndWarmInteriorTuning()
        {
            var mat = Resources.Load<Material>("AeroIndoorFog");
            Assert.That(mat, Is.Not.Null, "Resources/AeroIndoorFog 머티리얼이 존재해야 한다(컨트롤러 로드 경로).");
            Assert.That(mat.HasProperty("_Density"), Is.True, "AERO 볼류메트릭 포그 셰이더 머티리얼이어야 한다(_Density 속성).");
            Assert.That(mat.GetFloat("_Density"), Is.EqualTo(0f).Within(0.001f),
                "기본 _Density는 0이어야 한다 — 컨트롤러 게이트 없이 야외/테스트씬에 포그가 새지 않는다.");
            var c = mat.GetColor("_Colour");
            Assert.That(c.r, Is.GreaterThan(c.b).And.GreaterThanOrEqualTo(c.g),
                "실내 웜 헤이즈 색상 — 적성 우위(따뜻한 갈색 회광)여야 한다.");
            Assert.That(mat.GetFloat("_Max_Distance"), Is.EqualTo(60f).Within(0.5f),
                "실내 규모 레이마치 최대거리 60으로 튜닝돼야 한다.");
        }

        [Test]
        public void UniversalRendererData_ContainsAeroFullScreenPassFeature_BoundToIndoorFogMaterial()
        {
            string src = System.IO.File.ReadAllText("Assets/URP/UniversalRendererData.asset");
            Assert.That(src, Does.Contain("m_Name: AERO Volumetric Fog"),
                "렌더러에 AERO 볼류메트릭 포그 feature가 부착돼야 한다.");
            Assert.That(src, Does.Contain("guid: b00045f12942b46c698459096c89274e"),
                "feature 스크립트는 URP FullScreenPassRendererFeature(guid b00045f...)여야 한다.");
            Assert.That(src, Does.Contain("injectionPoint: 550"),
                "injectionPoint 550(AERO 데모 PC_Renderer와 동일 — BeforeRenderingPostProcessing).");
            Assert.That(src, Does.Contain("fetchColorBuffer: 1"),
                "색상 버퍼 fetch(포그 블러 텍스처 계약) 유지.");
            Assert.That(src, Does.Contain("guid: " + FogMaterialGuid),
                "passMaterial은 튜닝된 Resources/AeroIndoorFog(guid " + FogMaterialGuid + ")를 가리켜야 한다.");
            Assert.That(src, Does.Contain("{fileID: " + FeatureFileId + "}"),
                "신규 feature fileID(" + FeatureFileId + ")가 m_RendererFeatures 리스트에 등록돼야 한다.");
            Assert.That(src, Does.Contain("{fileID: " + ExistingSsaoFeatureFileId + "}"),
                "기존 feature(SSAO) 리스트 항목이 유지돼야 한다.");
        }

        [Test]
        public void Controller_GatesByIndoorActiveScene_AndFollowsAeroFrameCountContract()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/IndoorAeroFogController.cs");
            Assert.That(src, Does.Contain("activeScene == IndoorSceneName || activeScene.Contains(\"Interior\")"),
                "포그 밀도 게이트는 활성 씬==IndoorScene 또는 성 내부 테스트 씬(씬명 Interior 포함)이어야 한다" +
                "(애디티브 실내 전환/복귀 자동 처리 — 2026-10-09 테스트 씬 미발동 수리).");
            Assert.That(src, Does.Contain("Time.renderedFrameCount % 60"),
                "AERO 계약 — _FrameCount=renderedFrameCount%60 매 프레임(IGN 지터 안정화).");
            Assert.That(src, Does.Contain("Mathf.MoveTowards"),
                "_Density는 페이드 전환(MoveTowards)이어야 한다.");
            Assert.That(src, Does.Contain("SetFloat(\"_Density\", 0f)"),
                "부팅 안전 리셋 — 시작 시 밀도 0 보장.");
            Assert.That(src, Does.Not.Contain("AeroLocalizedGas"),
                "가스 격리 파이프라인(Rendering/AeroLocalizedGas*)과 결합하지 않는다(별도 경로 유지).");
        }

        [Test]
        public void IndoorScene_FlameLightsBoostedTo085_NoStale055()
        {
            string src = System.IO.File.ReadAllText("Assets/Scenes/IndoorScene.unity");
            int boosted = System.Text.RegularExpressions.Regex.Matches(src, @"m_Intensity: 0\.85\b").Count;
            int stale = System.Text.RegularExpressions.Regex.Matches(src, @"m_Intensity: 0\.55\b").Count;
            Assert.That(boosted, Is.EqualTo(3),
                "IndoorScene FlameLight 3개가 계획 v2 Phase 4 강도 0.85로 상향돼야 한다(포그 속 화광 가시화).");
            Assert.That(stale, Is.EqualTo(0), "구 강도 0.55 FlameLight 잔여가 없어야 한다.");
            Assert.That(src, Does.Contain("m_Intensity: 0.12"),
                "직사광 0.12(어두운 실내 계약)는 유지돼야 한다.");
        }

        [Test]
        public void FogMaterial_ResidesUnderResources_AndIndoorTransitionStillUsesIndoorSceneName()
        {
            // 컨트롤러 로드 경로(Resources.Load)와 전환 씬 이름 계약의 정합 확인.
            Assert.That(System.IO.File.Exists("Assets/Resources/AeroIndoorFog.mat"), Is.True,
                "포그 머티리얼은 Resources 경로여야 한다(런타임 Resources.Load).");
            string transition = System.IO.File.ReadAllText("Assets/Scripts/UI/IndoorSceneTransition.cs");
            Assert.That(transition, Does.Contain("\"IndoorScene\""),
                "실내 전환 씬 이름(IndoorScene) 계약 유지 — 컨트롤러 게이트와 동일 문자열.");
        }
    }
}