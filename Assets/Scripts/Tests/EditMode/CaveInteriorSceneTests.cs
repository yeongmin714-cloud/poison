using NUnit.Framework;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [137차 T2] Test_21_CaveInterior 소스계약 — 동굴 실내 테스트씬.
    /// 계획서: docs/137_RESOURCE_TEST_CAVE_PLAN.md §2 T2.
    /// EditMode 소스 어서션 원칙(LordPeaceVisitSceneTests 선례) — 런타임 씬 구축은 Play 게이트.
    /// 계약 축: ①씬 파일 존재 + 씬-스크립트 guid 결합 ②빌더 호출(현행 CaveInteriorBuilder 무수정)
    /// ③천장 숨김(탑다운 천장 금지 — 파괴 아닌 enabled만) ④씬 이름 게이트.
    /// </summary>
    public sealed class CaveInteriorSceneTests
    {
        private const string SetupPath = "Assets/Scripts/Systems/CaveInteriorTestSetup.cs";
        private const string SetupMetaPath = "Assets/Scripts/Systems/CaveInteriorTestSetup.cs.meta";
        private const string ScenePath = "Assets/Scenes/TestScenes/Test_21_CaveInterior.unity";

        // ── 1. 씬 파일 존재 + 씬-스크립트 guid 결합 ──

        [Test]
        public void SceneFile_Exists_And_ReferencesSetupScriptGuid_CouplingContract()
        {
            Assert.That(System.IO.File.Exists(ScenePath), Is.True,
                "Test_21_CaveInterior 씬 파일이 존재한다(137차 T2 산출물).");
            string scene = System.IO.File.ReadAllText(ScenePath);
            string meta = System.IO.File.ReadAllText(SetupMetaPath);
            string guid = ExtractGuid(meta);
            Assert.That(guid, Is.Not.Empty, "부트스트래퍼 .cs.meta에 유효 guid가 있다(Unity 임포트 생성본).");
            Assert.That(scene, Does.Contain(guid),
                "Test_21 씬 yaml이 부트스트래퍼 스크립트 guid를 참조한다(씬-스크립트 결합 계약).");
            Assert.That(scene, Does.Contain("CaveInteriorTestSetup"),
                "씬의 부트 GameObject명이 부트스트래퍼와 일치한다.");
        }

        // ── 2. 빌더 호출 — 현행 CaveInteriorBuilder 그대로(수정 금지 계약) ──

        [Test]
        public void Setup_CallsCaveInteriorBuilder_WithSpecifiedArgs()
        {
            string src = System.IO.File.ReadAllText(SetupPath);
            Assert.That(src, Does.Contain("CaveInteriorBuilder.BuildCaveInterior(CaveTerritoryId, CaveTier)"),
                "동굴 생성은 현행 CaveInteriorBuilder.BuildCaveInterior 호출 — 빌더 무수정 계약.");
            Assert.That(src, Does.Contain("cave_test"),
                "동굴 시드 인자 cave_test — 결정론 시드 계약.");
            Assert.That(src, Does.Contain("CaveTier = 2"),
                "동굴 tier 2 — 계획서 §2 T2 명시 인자.");
        }

        // ── 3. 천장 숨김 — 탑다운 천장 금지 계약(파괴 아닌 enabled만) ──

        [Test]
        public void Setup_HidesCaveCeiling_WithoutDestroying()
        {
            string src = System.IO.File.ReadAllText(SetupPath);
            Assert.That(src, Does.Contain("HideCaveCeiling"),
                "천장 숨김 헬퍼 호출이 존재한다(탑다운 천장 금지 계약).");
            Assert.That(src, Does.Contain("CaveCeiling"),
                "대상은 CaveInteriorBuilder가 만든 CaveCeiling 자식이다.");
            Assert.That(src, Does.Contain("renderer.enabled = false"),
                "천장은 enabled=false로만 숨긴다 — 원본 파괴 금지 계약.");
            Assert.That(src.Contains("Destroy(ceiling"), Is.False,
                "천장 GameObject 파괴 금지 — enabled 전환만 허용(원본 보존).");
        }

        // ── 4. 씬 이름 게이트 + 씬명 상수 ──

        [Test]
        public void Setup_HasSceneNameGate_And_TestSceneNameConstant()
        {
            string src = System.IO.File.ReadAllText(SetupPath);
            Assert.That(src, Does.Contain("gameObject.scene.name != TestSceneName"),
                "Awake 최상단 씬 이름 게이트 — 불일치 시 즉시 return(Test_19 선례).");
            Assert.That(src, Does.Contain("Test_21_CaveInterior"),
                "씬명 상수 Test_21_CaveInterior — 씬 파일명과 결합.");
            Assert.That(src, Does.Contain("[137차 T2]"),
                "137차 T2 마커 주석 존재(출처 추적 계약).");
        }

        // ── 5. 조명 보정 — DNC 봉쇄 + 방향광 0, 앰비언트 부스트 없음(암흑 유지) ──

        [Test]
        public void Setup_NeutralizesSunlights_And_DoesNotBoostAmbient()
        {
            string src = System.IO.File.ReadAllText(SetupPath);
            Assert.That(src, Does.Contain("NeutralizeSceneSunlights"),
                "방향광 0 경로 존재(Test_19 선례 동일).");
            Assert.That(src, Does.Contain("dnc.enabled = false"),
                "DayNightCycle 봉쇄 — 조명 보정 계약(Test_19 선례).");
            Assert.That(src.Contains("TestOnlyAmbientBoost"), Is.False,
                "앰비언트 부스트 생략 — 동굴 암흑 유지가 목적(137차 T2 명시).");
        }

        // ── 6. 보석상자 확인 — 1~3 기대 카운트 로그 ──

        [Test]
        public void Setup_ValidatesGemChestCount()
        {
            string src = System.IO.File.ReadAllText(SetupPath);
            Assert.That(src, Does.Contain("FindAnyObjectByType<GemChest>"),
                "보석상자 확인 경로 — FindAnyObjectByType<GemChest> 존재.");
        }

        private static string ExtractGuid(string metaText)
        {
            foreach (string line in metaText.Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("guid: "))
                    return trimmed.Substring("guid: ".Length).Trim();
            }
            return string.Empty;
        }
    }
}
