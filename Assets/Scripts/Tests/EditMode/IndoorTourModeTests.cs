using System.Collections.Generic;
using NUnit.Framework;
using ProjectName.UI;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [실내 관람 모드] IndoorTourMode 계약 6건 — EditMode 소스계약 + 순수함수 계약(런타임 순회는 Play 게이트).
    ///
    /// T1 순회 시퀀스 = IndoorTourProbe.Steps 라벨 리터럴 순서 1:1(진실 소스 — 당일 14/14 실측).
    ///   프로브의 Steps 배열은 14엔트리 전부를 인라인 보유: cave_f1=부트(BootF1), cave_f2=GoDeeper,
    ///   tavern=직접빌드(TavernDirect) 스텝이 배열 내부에 있다.
    /// T2 씬 게이트(투어 씬만 통과) / T3 링 인덱스 랩어라운드 / T4 카메라 피치 결정 /
    /// T5 동굴 부트 씬 게이트 2종 허용 소스계약 / T6 씬 빌더 부착 소스계약.
    /// 패턴 선례: CaveInteriorSceneTests/WorldMapSceneWiringTests(소스-텍스트 계약, Application.dataPath 기준).
    /// </summary>
    public class IndoorTourModeTests
    {
        private const string ProbePath = "Assets/Editor/IndoorTourProbe.cs";
        private const string SetupPath = "Assets/Scripts/Systems/CaveInteriorTestSetup.cs";
        private const string BuilderPath = "Assets/Editor/IndoorTourTestSceneBuilder.cs";

        private static string ReadSource(string path)
        {
            var full = System.IO.Path.Combine(Application.dataPath, "..", path);
            Assert.IsTrue(System.IO.File.Exists(full), $"소스 파일 없음: {path}");
            return System.IO.File.ReadAllText(full);
        }

        // ── T1. 순회 시퀀스 — 프로브 Steps 라벨 순서 1:1(이중 소스 금지) ──

        [Test]
        public void T1_TourSequence_MatchesProbeSteps_LabelOrder1to1()
        {
            string src = ReadSource(ProbePath);
            int arrayMark = src.IndexOf("TourStep[] Steps", System.StringComparison.Ordinal);
            Assert.That(arrayMark, Is.GreaterThanOrEqualTo(0),
                "프로브 TourStep[] Steps 배열 존재(투어 순서의 진실 소스).");
            int initStart = src.IndexOf('{', arrayMark);
            int initEnd = src.IndexOf("};", initStart, System.StringComparison.Ordinal);
            Assert.That(initStart, Is.GreaterThanOrEqualTo(0), "Steps 배열 initializer 시작 검출.");
            Assert.That(initEnd, Is.GreaterThan(initStart), "Steps 배열 initializer 범위 검출.");
            string initializer = src.Substring(initStart, initEnd - initStart);

            var probeLabels = new List<string>();
            int cursor = 0;
            const string key = "label = \"";
            while (true)
            {
                int at = initializer.IndexOf(key, cursor, System.StringComparison.Ordinal);
                if (at < 0) break;
                int valueStart = at + key.Length;
                int valueEnd = initializer.IndexOf('"', valueStart);
                probeLabels.Add(initializer.Substring(valueStart, valueEnd - valueStart));
                cursor = valueEnd + 1;
            }

            Assert.That(probeLabels.Count, Is.EqualTo(14),
                $"프로브 스텝 14엔트리(실측) — 검출값 [{string.Join(",", probeLabels)}]");
            Assert.That(IndoorTourMode.TourSequence.Length, Is.EqualTo(14),
                "TourSequence 14엔트리 계약.");
            for (int i = 0; i < probeLabels.Count; i++)
            {
                Assert.That(IndoorTourMode.TourSequence[i], Is.EqualTo(probeLabels[i]),
                    $"순서 {i} 불일치 — TourSequence는 프로브 Steps와 1:1이어야 한다(이중 소스 금지). " +
                    "cave_f1=부트(BootF1)/cave_f2=GoDeeper/tavern=직접빌드(TavernDirect) 스텝 포함.");
            }
        }

        // ── T2. 씬 게이트 — 투어 씬만 통과(타 씬 무동작 계약의 순수함수면) ──

        [Test]
        public void T2_SceneGate_PassesTourSceneOnly()
        {
            Assert.That(IndoorTourMode.IsTourScene(IndoorTourMode.TourSceneName), Is.True,
                "투어 씬 자신 — 게이트 통과.");
            Assert.That(IndoorTourMode.IsTourScene("Test_21_CaveInterior"), Is.False,
                "타 테스트 씬 — 무동작 게이트.");
            Assert.That(IndoorTourMode.IsTourScene("Main"), Is.False,
                "메인 씬 — 무동작 게이트.");
            Assert.That(IndoorTourMode.IsTourScene(string.Empty), Is.False,
                "빈 이름 — 무동작 게이트.");
        }

        // ── T3. 링 인덱스 — 14엔트리 랩어라운드 ──

        [Test]
        public void T3_RingIndex_WrapsAround14Entries()
        {
            Assert.That(IndoorTourMode.TourSequence.Length, Is.EqualTo(14),
                "링 길이 14엔트리 계약.");
            Assert.That(IndoorTourMode.NextIndex(13), Is.EqualTo(0),
                "링 랩어라운드 — 마지막(tavern)의 다음 = 첫(cave_f1).");
            Assert.That(IndoorTourMode.PrevIndex(0), Is.EqualTo(13),
                "링 랩어라운드 — 첫(cave_f1)의 이전 = 마지막(tavern).");
            Assert.That(IndoorTourMode.NextIndex(0), Is.EqualTo(1),
                "정방향 인접 이동.");
            Assert.That(IndoorTourMode.PrevIndex(5), Is.EqualTo(4),
                "역방향 인접 이동.");
        }

        // ── T4. 카메라 피치 결정 — 오버라이드 우선 / 기본 65° ──

        [Test]
        public void T4_CameraPitch_OverrideWins_ElseDefault57()
        {
            // [각도 v2] 기본 65→57 — 9/20 실내 예시(50~60°)와 정렬(IndoorCameraFollow.Pitch 동일값).
            Assert.That(IndoorTourMode.ResolvePitch(-1f), Is.EqualTo(57f).Within(0.001f),
                "기본(-1) — 각도 v2 기본값.");
            Assert.That(IndoorTourMode.ResolvePitch(0f), Is.EqualTo(57f).Within(0.001f),
                "0(무효 오버라이드) — 기본 57° 경로.");
            Assert.That(IndoorTourMode.ResolvePitch(90f), Is.EqualTo(90f).Within(0.001f),
                "90 — 완전 탑다운(관람 모드 지정값).");
            Assert.That(IndoorTourMode.ResolvePitch(57f), Is.EqualTo(57f).Within(0.001f),
                "명시 57 — 기본값과 동일 각도.");
        }

        // ── T5. 동굴 부트 씬 게이트 2종 허용 — 소스계약(기존 137차 핀 부분 문자열 보존) ──

        [Test]
        public void T5_CaveGate_AllowsTwoSceneNames_SourceContract()
        {
            string src = ReadSource(SetupPath);
            Assert.That(src, Does.Contain("Test_23_IndoorTour"),
                "투어 씬명 리터럴 존재(2종 게이트의 제2이름).");
            Assert.That(src, Does.Contain("TourSceneName"),
                "투어 씬명 상수 식별자 존재.");
            Assert.That(src, Does.Contain("gameObject.scene.name != TestSceneName && gameObject.scene.name != TourSceneName"),
                "씬 게이트 2종 허용 — 기존 137차 핀 부분 문자열(gameObject.scene.name != TestSceneName)을 보존한 확장.");
        }

        // ── T6. 씬 빌더 — 씬 경로 + 부트 2컴포넌트 부착 소스계약 ──

        [Test]
        public void T6_SceneBuilder_ScenePathAndBootComponents_SourceContract()
        {
            string src = ReadSource(BuilderPath);
            Assert.That(src, Does.Contain("Assets/Scenes/TestScenes/Test_23_IndoorTour.unity"),
                "투어 씬 경로 계약.");
            Assert.That(src, Does.Contain("CaveInteriorTestSetup"),
                "동굴 부트 컴포넌트 부착 계약.");
            Assert.That(src, Does.Contain("IndoorTourMode"),
                "관람 모드 컴포넌트 부착 계약.");
        }
    }
}
