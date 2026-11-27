using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 29-P11] 사용자 Play 피드백 트림 — 헬멧 크기 +15% / 갑옷 전방 +0.05×BodyScale.
    /// 원값 잠금(0.34f×BodyScale 등) 무손상, 트림은 읽기 배수/앵커 가산 경로.
    /// </summary>
    public class ArmorTrimP11Tests
    {
        private static string ReadSystem() => System.IO.File.ReadAllText("Assets/Scripts/Systems/ArmorVisualAttachSystem.cs");

        [Test]
        public void Source_HelmetSizeTrim()
        {
            string src = ReadSystem();
            Assert.That(src, Does.Contain("[무번호 29-P11]"), "[29-P11] 마커.");
            Assert.That(src, Does.Contain("internal const float HelmetSizeTrim = 1.25f;"),
                "[29-P12 갱신] 헬멧 트림 상수(+25% — 1차 1.15 승격, 여전히 작음 피드백).");
            Assert.That(src, Does.Contain("ApplySlotSizeTrim(slot, t)"),
                "[29-P11] GetTargetSize 읽기 경로 트림 적용(부착+재정규화 동시 일관).");
            Assert.That(src, Does.Contain("0.34f * BodyScale"),
                "[29-P11] 원값 잠금(105차 F3 — ArmorVisualFitGuardTests) 무손상.");
        }

        [Test]
        public void Source_ArmorForwardPull()
        {
            string src = ReadSystem();
            Assert.That(src, Does.Contain("pFwd * (0.10f * BodyScale)"),
                "[29-P12 갱신] 갑옷 전방 당깨기(+0.10×BodyScale ≈ 15cm — 1차 0.05에서 승격).");
            Assert.That(src, Does.Contain("Vector3.up * (0.15f * BodyScale)"),
                "[29-P11] 수직 앵커 성분(69차 후속5) 무손상.");
        }
    }
}
