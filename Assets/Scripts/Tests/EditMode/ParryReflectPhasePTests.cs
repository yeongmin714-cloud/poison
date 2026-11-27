using NUnit.Framework;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 28-P2] 리플렉 스파크 궤적 — ShockwaveRingFX 소스 계약(Source 잠금).
    /// SpawnParryReflectWisps 신설 + WispRunner reflect 모드 — 기존 아크 경로 무손상 계약.
    /// </summary>
    public class ParryReflectPhasePTests
    {
        private static string ReadFx() => System.IO.File.ReadAllText("Assets/Scripts/Systems/ShockwaveRingFX.cs");

        [Test]
        public void Source_ReflectApi()
        {
            string fx = ReadFx();
            Assert.That(fx, Does.Contain("public static void SpawnParryReflectWisps(Vector3 origin, Vector3 reflectDir, Color color, float duration = 0.3f)"),
                "[무번호 28-P2] 리플렉 위스프 API가 사전협의 시그니처로 존재해야 한다.");
        }

        [Test]
        public void Source_InitReflect()
        {
            string fx = ReadFx();
            Assert.That(fx, Does.Contain("public void InitReflect(Vector3 origin, Vector3 axis, int count, Color color, float duration, Shader shader)"),
                "[무번호 28-P2] WispRunner reflect 모드 진입 경로가 존재해야 한다.");
            Assert.That(fx, Does.Contain("private bool _reflect;"),
                "[무번호 28-P2] reflect 플래그 필드가 존재해야 한다.");
        }

        [Test]
        public void Source_OldArcUnharmed()
        {
            string fx = ReadFx();
            Assert.That(fx, Does.Contain("public static void SpawnParryArcWisps(Vector3 origin, Vector3 attackerDir, Color color, float duration = 0.3f)"),
                "[무번호 28-P2] 기존 위스프 아크 시그니처 무손상(22-T1 계약).");
            Assert.That(fx, Does.Contain("host.AddComponent<WispRunner>().Init(origin, attackerDir, ParryWispCount, color, duration, _litShader);"),
                "[무번호 28-P2] 기존 Init 호출 경로 무손상.");
        }

        [Test]
        public void Source_Deterministic()
        {
            string fx = ReadFx();
            int wispIdx = fx.IndexOf("private class WispRunner");
            Assert.That(wispIdx, Is.GreaterThanOrEqualTo(0));
            string block = fx.Substring(wispIdx);
            int end = block.IndexOf("private class", 10);
            if (end > 0) block = block.Substring(0, end);
            Assert.That(block, Does.Not.Contain("Random."),
                "[무번호 28-P2] WispRunner(리플렉 포함) 결정론 — 난수 API 코드형 0건.");
        }

        [Test]
        public void Source_ReflectTrajectory()
        {
            string fx = ReadFx();
            Assert.That(fx, Does.Contain("(0.06f - 0.10f * Mathf.Abs(spread))"),
                "[무번호 28-P2] 리플렉 낮은 반사 궤적 프로파일이 존재해야 한다.");
            Assert.That(fx, Does.Contain("p1 = _origin + dir * (len * 0.35f) + Vector3.up * 0.12f;"),
                "[무번호 28-P2] 얕은 바운스 중간점이 존재해야 한다.");
            Assert.That(fx, Does.Contain("p2 = _origin + dir * len - Vector3.up * 0.06f;"),
                "[무번호 28-P2] 지면 쪽 반사 끝점이 존재해야 한다.");
        }

        [Test]
        public void Source_Marker_And_Material()
        {
            string fx = ReadFx();
            Assert.That(fx, Does.Contain("[무번호 28-P2]"), "[무번호 28-P2] 마커 존재.");
            Assert.That(fx, Does.Contain("\"ParryReflectWispMat\""), "[무번호 28-P2] 리플렉 머티리얼 식별자 존재.");
        }
    }
}
