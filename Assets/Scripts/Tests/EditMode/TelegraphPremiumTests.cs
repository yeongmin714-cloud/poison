using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [프리미엄] 적 텔레그래프 링 고급화+패링 추가 고품질화 계약 — 사용자 지시("적색링 저품질→고급", "패링 더 고품질").
    /// 계획: docs/plans/2026-11-07_attack-parry-premium-timeparadox-plan.md P1/P2.
    /// 소스 계약(문자열 어서션) 원칙 — InteriorInteractionTests 선례.
    /// </summary>
    public class TelegraphPremiumTests
    {
        [Test]
        public void TelegraphRing_ShaderExists_WithFillAndEdge()
        {
            string src = System.IO.File.ReadAllText("Assets/Shaders/TelegraphRing.shader");
            Assert.That(src, Does.Contain("Shader \"ProjectName/TelegraphRing\""),
                "[텔레그래프P1] 프리미엄 셰이더 신설 — URP 파이프라인 태그.");
            Assert.That(src, Does.Contain("_Fill01"),
                "[텔레그래프P1-B] 방사형 채움 파라미터(예고 진행 0→1).");
            Assert.That(src, Does.Contain("_EdgePulse"),
                "[텔레그래프P1-C] 에지 발광 펄스 파라미터(블룸 흡수).");
            Assert.That(src, Does.Contain("UniversalPipeline"),
                "URP 렌더 파이프라인 태그 — 프로젝트 렌더 계약.");
            Assert.That(src, Does.Contain("Blend SrcAlpha OneMinusSrcAlpha"),
                "투명 블렌딩 — 지면 합성 계약.");
        }

        [Test]
        public void TelegraphIndicator_UsesShader_NoLowResTexture()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/AttackTelegraphIndicator.cs");
            Assert.That(src, Does.Not.Contain("TEX_SIZE"),
                "[텔레그래프P1] 64px 절차 텍스처 경로 폐기(블러 원인) — 셰이더 절차 렌더로 대체.");
            Assert.That(src, Does.Contain("ShaderName = \"ProjectName/TelegraphRing\""),
                "[텔레그래프P1] 프리미엄 셰이더 우선 경로.");
            Assert.That(src, Does.Contain("Universal Render Pipeline/Unlit"),
                "폴백 계약 유지(URP Unlit 부재 환경).");
            Assert.That(src, Does.Contain("CONCLUDE_FLASH_SECONDS"),
                "[텔레그래프P1-D] 종료 화이트 플래시+확산 소멸 페이즈.");
            Assert.That(src, Does.Not.Contain("Random.Range"),
                "결정론 계약 — Random 0건.");
        }

        [Test]
        public void ParryPremium_WhiteFrameCounterImpactEaseOut()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/PlayerCombat.cs");
            Assert.That(src, Does.Contain("SpawnParryScreenFlash()"),
                "[패링P2-A] PERFECT 화이트 프레임 훅 존재.");
            Assert.That(src, Does.Contain("ParryFlashFader"),
                "[패링P2-A] 화이트 프레임 페이더(카메라 공간 쿼드) 사용.");
            Assert.That(src, Does.Contain("반격 해방 임팩트"),
                "[패링P2-C] 반격 해방 시 지면 링+히트스톱 임팩트 존재.");
            Assert.That(src, Does.Contain("1f - (1f - rt) * (1f - rt)"),
                "[패링P2-D] 슬로우 복원 ease-out 커브.");
            Assert.That(src, Does.Not.Contain("Random.Range"),
                "결정론 계약 — 신규 Random 추가 금지(기존 선대 Random 불변).");
        }

        [Test]
        public void ParryFlashFader_UnscaledTimeSelfDestruct()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/ParryFlashFader.cs");
            Assert.That(src, Does.Contain("Time.unscaledDeltaTime"),
                "히트스톱 중에도 재생 — unscaled 시간 계약.");
            Assert.That(src, Does.Contain("Destroy(gameObject)"),
                "페이드 완료 후 자가 파괴 — 누수 없음.");
        }

        [Test]
        public void DirectionalSpark_EnhancedParams()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.That(src, Does.Contain("SparkFragmentMin = 14"),
                "[패링P2-B] 스파크 파편 수 8→14 강화.");
            Assert.That(src, Does.Contain("SparkFragmentMax = 16"),
                "[패링P2-B] 스파크 파편 수 상한 12→16.");
            Assert.That(src, Does.Contain("SparkStreakMin = 0.585f"),
                "[패링P2-B] 스트릭 길이 ×1.3(속도 체감 상향).");
            Assert.That(src, Does.Contain("SparkConeHalfAngle = 30f"),
                "[패링P2-B] 확산 폭 25°→30°.");
        }
    }
}
