using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [137차 T1] Test_20_LivelihoodResources 소스계약 — 자원 채널(채광/채집/벌목/농경) 테스트 씬 재건.
    /// 계획서: docs/137_RESOURCE_TEST_CAVE_PLAN.md §2 T1.
    /// Test_15_Livelihood.unity의 yaml을 그대로 복제한 전용 씬 — 부트스트래퍼(TestTerritoryCombatSetup,
    /// guid 6e49b159612c4ba5a0d20cdd6263ae62)를 공유하므로 SetupLivelihoodTestScene 배선
    /// (낚시/벌목/채광/채집/농경 전 채널)이 그대로 재현된다. 씬명에 "Livelihood"가 포함되어
    /// scene.name.Contains("Livelihood") 게이트(TestTerritoryCombatSetup)가 자동 발동 — 프로덕션 코드 0수정.
    /// EditMode 소스 어서션 원칙(LordPeaceVisitSceneTests 128-1 선례) — 런타임 씬 구축은 Play 게이트.
    /// </summary>
    public sealed class Test20ResourceSceneTests
    {
        private const string Scene20Path = "Assets/Scenes/TestScenes/Test_20_LivelihoodResources.unity";
        private const string Scene15Path = "Assets/Scenes/TestScenes/Test_15_Livelihood.unity";
        private const string SetupPath = "Assets/Scripts/Systems/TestTerritoryCombatSetup.cs";
        private const string BootstrapperGuid = "6e49b159612c4ba5a0d20cdd6263ae62";

        [Test]
        public void SceneFile_Exists_And_ReferencesSharedBootstrapperGuid_SourceContract()
        {
            // ① Test_20 씬 파일 존재
            Assert.That(System.IO.File.Exists(Scene20Path), Is.True,
                "Test_20_LivelihoodResources.unity 씬 파일이 존재한다(137차 T1 신규 복제 씬).");

            string scene20 = System.IO.File.ReadAllText(Scene20Path);
            string scene15 = System.IO.File.ReadAllText(Scene15Path);

            // ② 씬 yaml이 부트스트래퍼 m_Script guid를 참조
            Assert.That(scene20, Does.Contain(BootstrapperGuid),
                "Test_20 씬 yaml의 m_Script guid가 TestTerritoryCombatSetup 부트스트래퍼(6e49b159…)을 참조한다.");

            // ③ Test_15와 m_Script guid 동일 — 같은 부트스트래퍼 공유(복제 씬 계약)
            string guid20 = ExtractScriptGuid(scene20);
            string guid15 = ExtractScriptGuid(scene15);
            Assert.That(guid20, Is.Not.Empty, "Test_20 씬 yaml에 m_Script guid 행이 있다.");
            Assert.That(guid15, Is.Not.Empty, "Test_15 씬 yaml에 m_Script guid 행이 있다(원본 계약 — 수정 금지).");
            Assert.That(guid20, Is.EqualTo(guid15),
                "Test_20과 Test_15의 m_Script guid는 동일하다 — 부트스트래퍼 공유로 생채널 배선이 동일하게 발동한다.");

            // 게이트 자동 발동 근거 — 부트스트래퍼의 씬명 게이트가 "Livelihood" 부분문자열 판정임을 소스 계약으로 고정
            string setup = System.IO.File.ReadAllText(SetupPath);
            Assert.That(setup, Does.Contain("Contains(\"Livelihood\")"),
                "TestTerritoryCombatSetup의 씬명 게이트는 Contains(\"Livelihood\") — Test_20_LivelihoodResources는 수정 없이 게이트를 통과한다.");
        }

        [Test]
        public void Test20_NatureCluster_SourceContract_148()
        {
            string setup = System.IO.File.ReadAllText(SetupPath);
            // [148차] 씬 분리 소스 계약 — 자원/낚시 전용 경량 씬(Test_20 재정의, 계획 §1-3/§1-2)
            Assert.That(setup, Does.Contain("_isLivelihoodResourcesScene"),
                "[148차] Resources 전용 분기 게이트 존재 — Test_20 전용 경량 경로");
            Assert.That(setup, Does.Contain("SetupLivelihoodNatureCluster")
                .And.Contain("Livelihood_Pond"),
                "[148차] 자원 클러스터(호수/나무/돌) 존재 — Test_15에서 오프셋 수식 그대로 이식");
            Assert.That(setup, Does.Contain("_isLivelihoodScene && !_isLivelihoodResourcesScene"),
                "[148차] 마을/마약 경로는 Resources 씬 제외 — 마을 씬/자원 씬 분리 계약");
        }

        /// <summary>씬 yaml의 m_Script 행에서 guid 값만 추출한다(동일성 비교용).</summary>
        private static string ExtractScriptGuid(string sceneYaml)
        {
            foreach (string line in sceneYaml.Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("m_Script:"))
                {
                    int index = trimmed.IndexOf("guid: ", System.StringComparison.Ordinal);
                    if (index < 0) return string.Empty;
                    string rest = trimmed.Substring(index + "guid: ".Length);
                    int comma = rest.IndexOf(',');
                    return (comma < 0 ? rest : rest.Substring(0, comma)).Trim();
                }
            }
            return string.Empty;
        }
    }
}
