using NUnit.Framework;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [128차] Test_19_LordPeaceVisit 소스계약 — 병사 포섭 → 생존 영주 성 평화 방문 테스트씬.
    /// 계획서: docs/128_1_LORD_PEACE_VISIT_TEST_SCENE.md.
    /// EditMode 소스 어서션 원칙(InteractionActionActivationTests 선례) — 런타임 씬 구축은 Play 게이트.
    /// 계약 축: ①전쟁 경로(SpawnInteriorDefenseGuards) 미호출 ②딸 = 실제 훅 경로(강제 스폰 금지)
    /// ③포섭 대상 병사 중립+결정론 호감도 ④시드(골드/들꽃/딸 호감도) ⑤씬-스크립트 guid 결합.
    /// </summary>
    public sealed class LordPeaceVisitSceneTests
    {
        private const string SetupPath = "Assets/Scripts/Systems/LordPeaceVisitTestSetup.cs";
        private const string ScenePath = "Assets/Scenes/TestScenes/Test_19_LordPeaceVisit.unity";
        private const string SetupMetaPath = "Assets/Scripts/Systems/LordPeaceVisitTestSetup.cs.meta";

        // ── 1. 전쟁 경로 배제 — 수비병 스폰(적대) 미호출 ──

        [Test]
        public void Setup_DoesNotSpawnInteriorDefenseGuards_PeacefulVisitContract()
        {
            string src = System.IO.File.ReadAllText(SetupPath);
            Assert.That(src.Contains("SpawnInteriorDefenseGuards"), Is.False,
                "전쟁 경로 배제 — 씬 셋업은 SpawnInteriorDefenseGuards(실내 수비병 적대 스폰)를 호출하지 않는다.");
        }

        // ── 2. 딸 등장 = 실제 훅 경로 — 강제 스폰 금지 + 결정론 롤 통과 키 ──

        [Test]
        public void DaughterSpawn_UsesRealHookPath_NoForceSpawn()
        {
            string src = System.IO.File.ReadAllText(SetupPath);
            Assert.That(src.Contains("LordDaughterNPC.TrySpawnForTerritory"), Is.False,
                "딸 강제 스폰 금지 — SpawnForInvadedTerritory 말미 훅(TrySpawnLordDaughterForTerritory) 경로로만 등장한다.");
            Assert.That(src, Does.Contain("InvadedLordEncounter.SpawnForInvadedTerritory(room"),
                "영주 스폰은 실제 본경로 함수 — 그 말미 훅이 딸까지 스폰한다.");
        }

        [Test]
        public void DaughterDeterministicRoll_West7Passes_FirstScannedKeyIsWest7()
        {
            // 결정론 FNV-1a 40% — 사전 검증(Python 재현): West_7 hash 0x1743bf9c → roll 32 < 40.
            Assert.That(LordDaughterNPC.HasLordDaughter("West_7"), Is.True,
                "West_7은 딸 등장 결정론 롤을 통과한다(고정값 — 영지 풀/산식 변경 시 갱신).");
            Assert.That(LordDaughterNPC.GetDaughterName("West_7"), Is.EqualTo("다은"),
                "West_7 딸 이름은 결정론 풀 선택(고정값).");

            // 셋업 스캔 계약 — West_01..20 첫 통과 키 == West_7(런타임 ResolveDaughterTerritoryKey와 동일 산식).
            string expectedFirst = null;
            for (int i = 1; i <= 20; i++)
            {
                string key = "West_" + i;
                if (!LordDaughterNPC.HasLordDaughter(key)) continue;
                expectedFirst = key;
                break;
            }
            Assert.That(expectedFirst, Is.EqualTo("West_7"),
                "West_01..20 결정론 스캔의 첫 통과 키는 West_7 — ResolveDaughterTerritoryKey의 채택 키.");
        }

        // ── 3. 포섭 대상 병사 — 중립 + 결정론 포섭 성공 시드 ──

        [Test]
        public void RecruitSoldier_IsNeutralNotRecruited_DeterministicLoyalty()
        {
            string src = System.IO.File.ReadAllText(SetupPath);
            Assert.That(src, Does.Contain("SetRecruited(false)"),
                "포섭 대상 병사는 미영입 상태로 스폰 — SetRecruited(false).");
            Assert.That(src, Does.Contain("HostileToPlayerFaction = false"),
                "중립 병사 — 적대 플래그 false(전쟁 요소 배제).");
            Assert.That(src, Does.Contain("RecruitLoyaltySeed = 75f"),
                "호감도 75 시드 — LOYALTY_NORMAL(70) 이상으로 포섭 결정론 성공(랜덤 분기 미진입).");
        }

        // ── 4. 시드 계약 — 골드/들꽃/딸 호감도 사다리 ──

        [Test]
        public void SeedAssets_GoldFlowersAndDaughterAffectionLadder()
        {
            string src = System.IO.File.ReadAllText(SetupPath);
            Assert.That(src, Does.Contain("AddGold"), "골드 시드 경로 존재(입단비 검증용).");
            Assert.That(src, Does.Contain("SeedGoldAmount = 5000"), "골드 시드 5,000(ArenaTestSetup 선례 규모).");
            Assert.That(src, Does.Contain("Flower_Wild"), "들꽃 시드 — PlayerInventory.Flower_Wild 단일 소스.");
            Assert.That(src, Does.Contain("SeedFlowerCount = 3"), "들꽃 3개(선물 여유).");
            Assert.That(src, Does.Contain("SeedDaughterAffection = 70"), "딸 호감도 70 시드 — 선물 1회(→80) 후 프로포즈 사다리.");
        }

        // ── 5. 소유권 — 생존 영주(LordOwned) 유지, 점령 전환 없음 ──

        [Test]
        public void VisitTerritory_StaysLordOwned_LivingLordScenario()
        {
            string src = System.IO.File.ReadAllText(SetupPath);
            Assert.That(src, Does.Contain("TerritoryOwnership.LordOwned"),
                "방문 영지는 LordOwned 선행세팅 — 생존 영주 시나리오(용어계약: 포섭≠점령).");
            Assert.That(src.Contains("TerritoryOwnership.PlayerOwned"), Is.False,
                "PlayerOwned 세팅 금지 — 점령 성에는 영주/딸이 등장하지 않는다(126차 계약).");
        }

        // ── 6. 씬-스크립트 결합 — 씬 yaml이 부트스트래퍼 guid를 참조 ──

        [Test]
        public void SceneFile_ReferencesSetupScriptGuid_CouplingContract()
        {
            string scene = System.IO.File.ReadAllText(ScenePath);
            string meta = System.IO.File.ReadAllText(SetupMetaPath);
            string guid = ExtractGuid(meta);
            Assert.That(guid, Is.Not.Empty, "부트스트래퍼 .cs.meta에 유효 guid가 있다(Unity 임포트 생성본).");
            Assert.That(scene, Does.Contain(guid),
                "Test_19 씬 yaml이 부트스트래퍼 스크립트 guid를 참조한다(씬-스크립트 결합 계약).");
            Assert.That(scene, Does.Contain("LordPeaceVisitTestSetup"),
                "씬의 부트 GameObject명이 부트스트래퍼와 일치한다.");
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
