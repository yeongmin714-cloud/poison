// [115차] 세금제안 배선 계약 테스트 — 소스 레벨 계약 검증 (WorldMapSceneWiringTests 114차 패턴 준용).
// 목적: 상점 NPC 패널 "밀매제안→세금제안" 교체, RecommendedTaxWindowUTK 개방 라우팅,
//       TaxManager 7일 훅/골드 원장 배선을 소스 텍스트 상수로 단언한다. (런타임 렌더 판정은 QA 서브 몫)
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    [TestFixture]
    public class TaxProposalWiringTests
    {
        private const string InteractionPanelPath = "Assets/Scripts/UI/Toolkit/InteractionPanelUTK.cs";
        private const string TaxWindowPath = "Assets/Scripts/UI/Toolkit/RecommendedTaxWindowUTK.cs";
        private const string TaxManagerPath = "Assets/Scripts/Systems/TaxManager.cs";
        private const string Test15SetupPath = "Assets/Scripts/Systems/TestTerritoryCombatSetup.cs";
        private const string PlayerMovementPath = "Assets/Scripts/Systems/PlayerMovement.cs";
        private const string VillageNpcSpawnerPath = "Assets/Scripts/Systems/VillageNpcSpawner.cs";
        private const string RestaurantPropBuilderPath = "Assets/Scripts/Systems/RestaurantPropBuilder.cs";

        private string ReadSource(string path)
        {
            var full = Path.Combine(Application.dataPath, "..", path);
            Assert.IsTrue(File.Exists(full), $"소스 파일 없음: {path}");
            return File.ReadAllText(full);
        }

        [Test]
        public void InteractionPanel_ContainsTaxProposalAction()
        {
            string src = ReadSource(InteractionPanelPath);
            StringAssert.Contains("AddAction(\"세금 제안\", 3)", src);   // [144차] Figma 81:134 라벨 공백 정합
        }

        [Test]
        public void InteractionPanel_SmugglingActionRemoved()
        {
            // AddAction( 접두 포함 문자열로만 판정 — 주석의 "밀매제안 대체" 문구는 계약 위반이 아니다.
            string src = ReadSource(InteractionPanelPath);
            Assert.That(src, Does.Not.Contain("AddAction(\"밀매제안\""), "밀매제안 액션은 세금제안으로 교체됐어야 한다");
        }

        [Test]
        public void InteractionPanel_RoutesTaxProposalToWindow()
        {
            string src = ReadSource(InteractionPanelPath);
            StringAssert.Contains("case \"세금 제안\":", src);   // [144차] 라벨 공백 정합 동기
            StringAssert.Contains("RecommendedTaxWindowUTK.Open()", src);
            // [117차 R2] 단일 영지 컨텍스트 전달 — 상점 NPC의 TerritoryId.
            StringAssert.Contains("RecommendedTaxWindowUTK.Open(taxShopNpc.NPCData.TerritoryId)", src);
        }

        [Test]
        public void RecommendedTaxWindow_ContainsApplyAndSlider_117R2()
        {
            // [117차 R2] 피그마 244:281 정합 — 수금 버튼 폐기(자동 징수 계약) → 세율 적용/슬라이더로 대체.
            string src = ReadSource(TaxWindowPath);
            StringAssert.Contains("세율 적용", src);
            StringAssert.Contains("SetTaxRate", src);
            StringAssert.Contains("public void RefreshData()", src);
            Assert.That(src, Does.Not.Contain("CollectPendingTax"),
                "[117차 R2] 수금 버튼 폐기 — 창에서 CollectPendingTax 직접 호출 금지(자동 징수 계약)");
        }

        [Test]
        public void RecommendedTaxWindow_ContainsOrphanGuard()
        {
            // [114차 WorldMap 고아 수리 계약 준용] 씬 재로드 시 부모 유실 인스턴스 재부착 경로.
            string src = ReadSource(TaxWindowPath);
            StringAssert.Contains("RemoveFromHierarchy", src);
        }

        [Test]
        public void RecommendedTaxWindow_Figma244_TruthValues_117R2()
        {
            // [117차 R2] 피그마 244:281 "단일 영지 세금 관리" 진실치 — 재실측(2026-10-23) 기반 소스 어서션.
            string src = ReadSource(TaxWindowPath);
            StringAssert.Contains("new Vector2(624, 974)", src);
            StringAssert.Contains("TrackSpan = 493.6f", src);
            StringAssert.Contains("HandleSize = 24f", src);
            StringAssert.Contains("권장값으로 설정", src);
            StringAssert.Contains("매주 월요일 00:00 자동 징수", src);
            StringAssert.Contains("권장 세율 초과 1%p당 호감도 −1 / 주", src);
            StringAssert.Contains("다음 징수 후 예상", src);
            StringAssert.Contains("세금 관리", src);
        }

        [Test]
        public void TaxManager_ContainsWeeklyHookAndPlayerGold()
        {
            string src = ReadSource(TaxManagerPath);
            StringAssert.Contains("[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]", src);
            StringAssert.Contains("public const int TAX_INTERVAL_DAYS = 7", src);
            StringAssert.Contains("tm.OnDayStart += Instance.OnDayStart", src);
            // 골드 원장 — PlayerStats 싱글턴 취득 후 source 태그 부착 지급 (2줄 패턴).
            StringAssert.Contains("var stats = PlayerStats.Instance;", src);
            StringAssert.Contains("stats.AddGold(collected, \"tax\")", src);
            StringAssert.Contains("state.ownership != TerritoryOwnership.PlayerOwned", src);
        }

        [Test]
        public void Test15Setup_ContainsShopNpcSpawn()
        {
            // [115차 후속] Test_15 상점 NPC — 세금제안(권장세금 UI) 진입점.
            //   VillageNpcSpawner 대표 마을 상점주인은 NPCAmbientDialogue 경로라 ShopNPC 분류 대상이
            //   아니므로(실측 — HoverTargetClassifier는 TerritoryNPCBehaviour.IsShopNPC만 ShopNPC로
            //   분류), Test_13/14 SpawnVillageNpc(isShop=true) 패턴으로 별도 배치한다.
            string src = ReadSource(Test15SetupPath);
            StringAssert.Contains("SpawnVillageNpc(\"test15_shopkeeper\"", src);
        }

        [Test]
        public void PlayerMovement_RoutesShopNpcFKeyToShopKind()
        {
            // [116차 R2] PlayerMovement F키(상점 NPC 조사)가 HoverTargetClassifier의
            //   IsShopTerritoryNpc → ShopNPC 분류를 좌클릭 경로와 동일 단일소스로 재사용하는지 검증.
            string src = ReadSource(PlayerMovementPath);
            StringAssert.Contains("HoverTargetClassifier.IsShopTerritoryNpc", src);
            StringAssert.Contains("HoverTargetClassifier.TargetKind.ShopNPC", src);
            StringAssert.Contains("[116차 R2]", src);
        }

        [Test]
        public void Test15Setup_RestaurantNotPrebuilt()
        {
            // [116차 R3] Test_15 식당 선완공(ForceComplete 즉시 완공) 제거 계약 —
            //   SetRestaurantBuilt(false)로 미완공 유지, TryRemoveStructure 철거 경로 확보.
            //   판정은 "ForceComplete(targetId)" 전체 문자열만 적용(타 ForceComplete 호출 잔존 허용).
            string src = ReadSource(Test15SetupPath);
            StringAssert.Contains("SetRestaurantBuilt(best.nation, best.index, false)", src);
            StringAssert.Contains("TryRemoveStructure", src);
            StringAssert.Contains("[116차 R3]", src);
            Assert.That(src, Does.Not.Contain("ForceComplete(targetId)"), "구 Test15 선완공 블록은 [116차 R3] 교체로 소멸해야 한다");
        }

        [Test]
        public void VillageNpcSpawner_ExposesHeightAndAnimationHelpers()
        {
            // [116차 R4] 마을 NPC 높이/애니메이션 보정이 public static 헬퍼로 노출되는지 검증.
            string src = ReadSource(VillageNpcSpawnerPath);
            StringAssert.Contains("public static void EnsureVillageNpcHeight(GameObject npc, float groundY, string npcName)", src);
            StringAssert.Contains("public static void EnsureVillageNpcAnimation(GameObject npc, string modelKey, string npcName)", src);
            StringAssert.Contains("[116차 R4]", src);
        }

        [Test]
        public void Test15ShopNpc_UsesHeightAndAnimationHelpers()
        {
            // [116차 R4 후속] Test_15 상점 NPC 배치가 VillageNpcSpawner 헬퍼를 재사용하는지 검증.
            string src = ReadSource(Test15SetupPath);
            StringAssert.Contains("VillageNpcSpawner.EnsureVillageNpcHeight(npc", src);
            StringAssert.Contains("VillageNpcSpawner.EnsureVillageNpcAnimation(npc", src);
        }

        [Test]
        public void RestaurantPropBuilder_HasVisibilityDiagnostics()
        {
            // [116차 R3] 식당 실물 가시성 진단 로그 — QA 실측용 worldY 좌표 출력 계약.
            string src = ReadSource(RestaurantPropBuilderPath);
            StringAssert.Contains("[RestaurantPropBuilder] 진단: worldY=", src);
            StringAssert.Contains("[116차 R3]", src);
        }
    }
}
