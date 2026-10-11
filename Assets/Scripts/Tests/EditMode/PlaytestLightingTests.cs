using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [2026-10-10 P1 — 플레이테스트 직사광 소등] 계약 테스트.
    /// Test_PlayerCastleInterior 씬의 태양(PlaytestDirectionalLight 1.15+Soft)이 천장 비활성화로
    /// 실내를 직사해 성내부 암전 무드(7차)와 그림자 디더링 계약을 무력화했다.
    /// 부트스트랩이 씬 직사광을 소등(intensity 0 + 섀도우 None)해야 한다 — IndoorScene 직사광 0 정합.
    /// </summary>
    public sealed class PlaytestLightingTests
    {
        private const string PlaytestPath = "Assets/Scripts/Systems/PlayerCastleInteriorPlaytest.cs";

        [Test]
        public void P1_NeutralizeSceneSunlights_DirectLights_Off()
        {
            // 태양 시뮬레이션 — 플레이테스트 씬의 PlaytestDirectionalLight와 동일 사양.
            var sunGo = new GameObject("P1_TestSun");
            try
            {
                var sun = sunGo.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.15f;
                sun.shadows = LightShadows.Soft;

                PlayerCastleInteriorPlaytest.NeutralizeSceneSunlights();

                Assert.That(sun.intensity, Is.EqualTo(0f), "직사광 강도는 0이어야 한다(7차 소등 정합).");
                Assert.That(sun.shadows, Is.EqualTo(LightShadows.None), "직사광 섀도우는 None이어야 한다(디더링 제거).");

                // 비직사광원(포인트)은 건드리지 않는다 — 성내부 광원 풀 보존.
                var pointGo = new GameObject("P1_TestPoint");
                try
                {
                    var point = pointGo.AddComponent<Light>();
                    point.type = LightType.Point;
                    point.intensity = 0.14f;
                    point.shadows = LightShadows.Soft;

                    PlayerCastleInteriorPlaytest.NeutralizeSceneSunlights();

                    Assert.That(point.intensity, Is.EqualTo(0.14f), "포인트라이트는 소등 대상이 아니다.");
                    Assert.That(point.shadows, Is.EqualTo(LightShadows.Soft), "포인트라이트 섀도우는 원본 유지.");
                }
                finally
                {
                    Object.DestroyImmediate(pointGo);
                }
            }
            finally
            {
                Object.DestroyImmediate(sunGo);
            }
        }

        [Test]
        public void P1_Bootstrap_CallsSunNeutralization_BeforeBuild()
        {
            string src = System.IO.File.ReadAllText(PlaytestPath);
            int neutralize = src.IndexOf("NeutralizeSceneSunlights();", System.StringComparison.Ordinal);
            int build = src.IndexOf("EnsureWarehouseSystem();", System.StringComparison.Ordinal);
            Assert.That(neutralize, Is.GreaterThanOrEqualTo(0), "소등 호출 존재.");
            Assert.That(build, Is.GreaterThan(neutralize), "소등은 성내부 빌드 이전에 실행되어야 한다.");
            Assert.That(src, Does.Contain("light.type != LightType.Directional"), "대상은 Directional 한정(빌더 광원 보존).");
        }
    }
}
