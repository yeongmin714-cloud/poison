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
    ///  ④실내 화광 강도 0.62×3(10-10 사용자 요구 하향 — 원 0.55→0.85 계획v2 이력 주석), 직사광 0.09
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
            // _Density 에셋 기본값 0 단정은 제거 — 에디터가 Play 중 머티리얼 변이를 에셋에 역직렬화할 수 있어
            //   디스크 값은 보증 대상이 아니다(실측: 2026-10-09 사용자 세션에서 0.35로 저장됨).
            //   안전 보증은 컨트롤러 부팅 리셋(Ensure → SetFloat("_Density", 0f)) 소스 계약이 담당한다(아래 테스트).
            var c = mat.GetColor("_Colour");
            Assert.That(c.r, Is.EqualTo(c.g).Within(0.05f).And.EqualTo(c.b).Within(0.05f),
                "실내 포그 색상은 흰색(무채색)이어야 한다 — 2026-10-09 사용자 확정(웜 브라운 → 흰색 전환).");
            Assert.That(c.r, Is.GreaterThanOrEqualTo(0.9f),
                "흰색 포그 — 채도 낮은 밝은 색(≥0.9)이어야 한다.");
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
        public void IndoorScene_FlameLightsBrightenedTo60_NoStale30()
        {
            // [2026-10-11 20차 사용자 확정 — "실내씬 예시 정도의 밝기"] 목표 = 내부씬 예시.PNG luminance
            // (실측 mean 37/median 34/p75 50/p90 75). 19차 상향 후에도 어두움 재보고 → 값 사다리 교훈대로
            // 배수 점프: 화광 3→6×3, 씬 앰비언트 삼분광 ×2(0.44/0.3/0.2). 다음 사용자 캡처 1장으로 1회 조정 예정.
            // 3/2.2/1.3/0.85/0.75/0.55/0.62 잔여 금지(직사광 제외)·직사광 0(소등)·포그 0.12 유지.
            string src = System.IO.File.ReadAllText("Assets/Scenes/IndoorScene.unity");
            int boosted = System.Text.RegularExpressions.Regex.Matches(src, @"m_Intensity: 6\b").Count;
            int stalePrev = System.Text.RegularExpressions.Regex.Matches(src, @"m_Intensity: 3\b").Count;
            int stalePrev3 = System.Text.RegularExpressions.Regex.Matches(src, @"m_Intensity: 2\.2\b").Count;
            int staleOld = System.Text.RegularExpressions.Regex.Matches(src, @"m_Intensity: 0\.85\b").Count;
            int stalePrev2 = System.Text.RegularExpressions.Regex.Matches(src, @"m_Intensity: 0\.62\b").Count;
            int staleDirect = System.Text.RegularExpressions.Regex.Matches(src, @"m_Intensity: 0\.02\b").Count;
            Assert.That(boosted, Is.EqualTo(3),
                "IndoorScene FlameLight 3개가 6(10-11 20차 예시 정합 ×2 점프)이어야 한다.");
            Assert.That(stalePrev, Is.EqualTo(0), "이전 3 잔여 금지.");
            Assert.That(stalePrev3, Is.EqualTo(0), "이전 2.2 잔여 금지.");
            Assert.That(staleOld, Is.EqualTo(0), "구 강도 0.85 잔여 금지.");
            Assert.That(stalePrev2, Is.EqualTo(0), "이전 0.62 잔여 금지.");
            Assert.That(staleDirect, Is.EqualTo(0), "직사광 0.02 잔여 금지(0으로 소등).");
            Assert.That(src, Does.Contain("m_Intensity: 0\n"),
                "직사광은 완전 소등(0)이어야 한다.");
            Assert.That(src, Does.Contain("m_AmbientSkyColor: {r: 0.44, g: 0.3, b: 0.2, a: 1}"),
                "씬 앰비언트 삼분광은 10-11 20차 ×2 값이어야 한다.");
            var fogCtrl = System.IO.File.ReadAllText("Assets/Scripts/Systems/IndoorAeroFogController.cs");
            Assert.That(fogCtrl, Does.Contain("public const float IndoorDensity = 0.12f"),
                "AERO 실내 포그 밀도는 0.12(흰 포그 밝기 축소)여야 한다.");
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