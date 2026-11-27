using System.Reflection;
using NUnit.Framework;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [153차 P2] 농경 채널 애니메이션+리듬 타격 피드백 계약 테스트 — 소스계약 + 매핑 결정론.
    /// 진실 출처: docs/153_FARMING_QUALITY_ACQ_VFX_PLAN.md §2+§3 P2(+§4 확정결정 1·3번).
    /// 규약 복제 소스: GatherFeedbackContractTests(133차) — 상대경로 소스 판독 + Does.Contain 패턴 준용.
    /// </summary>
    public class Farm153ChannelFeedbackTests
    {
        private const string FarmPath = "Assets/Scripts/Systems/FarmPlot.cs";
        private const string ClipDriverPath = "Assets/Scripts/Systems/HumanoidClipDriver.cs";
        private const string HerbPath = "Assets/Scripts/Systems/HerbPickup.cs";
        private const string FeedbackPath = "Assets/Scripts/Systems/GatherFeedbackSystem.cs";
        private const string EditorToolsPath = "Assets/Scripts/Editor/Farm153EditorTools.cs";

        /// <summary>[153차 P2] 계약 1 — FarmChannelWiring_SourceContract: FarmPlot 채널 애니+타격 피드백 배선 존재.</summary>
        [Test]
        public void FarmChannelWiring_SourceContract_153()
        {
            var src = System.IO.File.ReadAllText(FarmPath);
            Assert.That(src, Does.Contain("GatherFeedbackKind.Soil"),
                "[153차 P2] FarmPlot 굉이질/정리·파종 채널 흙 타격 피드백 배선 존재.");
            Assert.That(src, Does.Contain("OnImpact"),
                "[153차 P2] FarmPlot 채널 타격 리듬 이벤트 구독 존재.");
            Assert.That(src, Does.Contain("TriggerTill"),
                "[153차 P2] FarmPlot 굉이질/정리 채널 시작 애니 발화 존재.");
        }

        /// <summary>[153차 P2] 계약 2 — FarmSoldierPathExcluded_SourceContract: FarmPlot은 농경 보상에
        /// 자체 발행 경로를 두지 않는다(병사 워커 수확 경로 원계약 유지 — 130차 소스계약 계승).</summary>
        [Test]
        public void FarmSoldierPathExcluded_SourceContract_153()
        {
            var src = System.IO.File.ReadAllText(FarmPath);
            Assert.That(src, Does.Not.Contain("Publish("),
                "[153차 P2] FarmPlot 자체 발행 경로 없음 — 병사 경로 무배선(원계약) 유지.");
        }

        /// <summary>[153차 P2] 계약 3 — ClipDriverFarmTriggers_SourceContract: 굉이질 트리거+상태 재시작 API 존재.</summary>
        [Test]
        public void ClipDriverFarmTriggers_SourceContract_153()
        {
            var src = System.IO.File.ReadAllText(ClipDriverPath);
            Assert.That(src, Does.Contain("public void TriggerTill"),
                "[153차 P2] TriggerTill(파라미터 가드) 존재.");
            Assert.That(src, Does.Contain("public void ReplayAction"),
                "[153차 P2] ReplayAction(상태 재시작) 존재.");
        }

        /// <summary>[153차 P2] 계약 4 — HerbRhythmReplay_SourceContract: 약초 수확 채널 타격 리듬 Gather 재발화 존재.</summary>
        [Test]
        public void HerbRhythmReplay_SourceContract_153()
        {
            var src = System.IO.File.ReadAllText(HerbPath);
            Assert.That(src, Does.Contain("ReplayAction(\"Gather\")"),
                "[153차 P2] HerbPickup 타격 리듬 Gather 동작 재발화 존재.");
        }

        /// <summary>[153차 P2] 계약 5 — SoilMapping_Deterministic: 흙 타격 프리픽스/프리펩 매핑 결정론(직접 호출).
        /// 매핑 함수는 private 구현이라 리플렉션 1회 호출로 판정(Unity 오브젝트 미사용 — EditMode 안전).</summary>
        [Test]
        public void SoilMapping_Deterministic_153()
        {
            var t = typeof(GatherFeedbackSystem);
            var prefix = t.GetMethod("ClipPrefix", BindingFlags.NonPublic | BindingFlags.Static);
            var vfxName = t.GetMethod("VfxPrefabName", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(prefix, Is.Not.Null, "[153차 P2] 프리픽스 매핑 함수 존재.");
            Assert.That(vfxName, Is.Not.Null, "[153차 P2] 프리펩 매핑 함수 존재.");
            Assert.That(prefix.Invoke(null, new object[] { GatherFeedbackKind.Soil }),
                Is.EqualTo("SFX_Gather_Soil"), "[153차 P2] Soil 프리픽스 결정론.");
            Assert.That(vfxName.Invoke(null, new object[] { GatherFeedbackKind.Soil }),
                Is.EqualTo("GatherVFX_SoilBurst"), "[153차 P2] Soil 프리펩 결정론.");
        }

        /// <summary>[153차 P2] 계약 6 — EditorTool_SourceContract: 배치 도구 진입점+프리펩 짝 계약 존재.</summary>
        [Test]
        public void EditorTool_SourceContract_153()
        {
            var src = System.IO.File.ReadAllText(EditorToolsPath);
            Assert.That(src, Does.Contain("playOnAwake"),
                "[153차 P2] 프리펩 playOnAwake=false 저장(런타임 명시 Play 짝 계약).");
            Assert.That(src, Does.Contain("RunAll"),
                "[153차 P2] RunAll 배치 진입점 존재.");
        }
    }
}
