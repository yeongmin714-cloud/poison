using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [129차 Phase 2] 플레이어 액션 애니 배선 — 소스계약 테스트.
    /// 진실 출처: docs/129_PLAYER_ACTION_CHEER_ANIM_PLAN.md §2·§3-Phase 2·§6 결정 기록(범위표).
    ///  · 채집(Gather)/크래프트(Craft)/환호(Cheer) 트리거 5지점+드라이버 단일화(Q4 — TriggerHarvest 3메서드 위임).
    ///  · EditMode 규약: 런타임 인스턴스화 없이 File.ReadAllText 문자열 단정만 수행
    ///    (Npc2026AssetContractTests 128-2 P3 소스계약 선례 준용).
    /// </summary>
    public class PlayerActionAnimContractTests
    {
        /// <summary>
        /// [129차] 계약 1 — ActionWiring_SourceContract: 5지점 배선 존재.
        /// HerbPickup(TriggerGather+TriggerCheer), WeaponForgeUTK(TriggerCraft+TriggerCheer),
        /// CookingUI(TriggerCheer), LootBasket(TriggerGather), ConstructionManager(TriggerCraft).
        /// </summary>
        [Test]
        public void ActionWiring_SourceContract_129()
        {
            var herbSrc = System.IO.File.ReadAllText("Assets/Scripts/Systems/HerbPickup.cs");
            Assert.That(herbSrc, Does.Contain("TriggerGather"),
                "[129차] HerbPickup — TriggerGather 배선 존재.");
            Assert.That(herbSrc, Does.Contain("TriggerCheer"),
                "[129차] HerbPickup — TriggerCheer 배선 존재.");

            var forgeSrc = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/WeaponForgeUTK.cs");
            Assert.That(forgeSrc, Does.Contain("TriggerCraft"),
                "[129차] WeaponForgeUTK — TriggerCraft 배선 존재.");
            Assert.That(forgeSrc, Does.Contain("TriggerCheer"),
                "[129차] WeaponForgeUTK — TriggerCheer 배선 존재.");

            var cookingSrc = System.IO.File.ReadAllText("Assets/Scripts/UI/CookingUI.cs");
            Assert.That(cookingSrc, Does.Contain("TriggerCheer"),
                "[129차] CookingUI — TriggerCheer 배선 존재.");

            var lootSrc = System.IO.File.ReadAllText("Assets/Scripts/Systems/LootBasket.cs");
            Assert.That(lootSrc, Does.Contain("TriggerGather"),
                "[129차] LootBasket — TriggerGather 배선 존재.");

            var constructionSrc = System.IO.File.ReadAllText("Assets/Scripts/Systems/ConstructionManager.cs");
            Assert.That(constructionSrc, Does.Contain("TriggerCraft"),
                "[129차] ConstructionManager — TriggerCraft 배선 존재.");
        }

        /// <summary>
        /// [130차] 계약 5 — HarvestCheer_SourceContract: 벌목/채광 완료(플레이어 경로) TriggerCheer 배선.
        /// HarvestForPlayer 메서드 본문 내에만 존재(병사 TryAutoMine 경로 오염 방지 — 지역 슬라이스 판정).
        /// </summary>
        [Test]
        public void HarvestCheer_SourceContract_130()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/ResourceNode.cs");
            int begin = src.IndexOf("public void HarvestForPlayer()");
            Assert.That(begin, Is.GreaterThan(0), "[130차] HarvestForPlayer 존재.");
            int next = src.IndexOf("private void EnsureStump()", begin);
            string body = next > begin ? src.Substring(begin, next - begin) : src.Substring(begin);
            Assert.That(body, Does.Contain("TriggerCheer"),
                "[130차] 벌목/채광 완료(플레이어 경로) → TriggerCheer 배선 존재.");
        }

        /// <summary>
        /// [130차] 계약 6 — FishingCheer_SourceContract: 낚시 성공 2분기(릴 성공/스페이스 잡기) TriggerCheer 배선.
        /// </summary>
        [Test]
        public void FishingCheer_SourceContract_130()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/FishingSystem.cs");
            int count = src.Split(new[] { "TriggerCheer" }, System.StringSplitOptions.None).Length - 1;
            Assert.That(count, Is.GreaterThanOrEqualTo(2),
                "[130차] 낚시 성공 2분기 TriggerCheer 배선 존재.");
        }

        /// <summary>
        /// [130차 후속] 계약 7 — FarmSoldierExcluded_SourceContract: 농경(밭) 수확 연출은 플레이어 경로
        /// (FarmPlot 수확 채널 → HerbPickup 완료 콜백 — 129차 기존 계약)로만 커버된다.
        /// FarmingSystem.Harvest는 병사 자동 농경(GuardTaskSystem) 전용 — 연출 publish/환호 배선 부재(사용자 확정).
        /// </summary>
        [Test]
        public void FarmSoldierExcluded_SourceContract_130()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/FarmingSystem.cs");
            Assert.That(src, Does.Not.Contain("HarvestResultBridge"),
                "[130차 후속] 병사 농경 전용 경로 — 획득 연출 publish 없음(인벤 적립만).");
            Assert.That(src, Does.Not.Contain("HumanoidClipDriver"),
                "[130차 후속] 병사 농경 전용 경로 — 플레이어 환호 드라이버 접근 없음.");

            // 플레이어 밭 수확 연출 커버 근거 — HerbPickup 완료 콜백 배선(129차 계약) 존재 재확인.
            var herbSrc = System.IO.File.ReadAllText("Assets/Scripts/Systems/HerbPickup.cs");
            Assert.That(herbSrc, Does.Contain("HarvestResultBridge.Publish"),
                "[130차 후속] 플레이어 밭 수확 연출 — HerbPickup 완료 콜백 publish 경로 커버.");
        }

        /// <summary>
        /// [129차 Q4] 계약 2 — DriverUnify_SourceContract: HumanoidClipDriver의 TriggerHarvest가
        /// TriggerGather() 위임으로 단일화 + 구 Harvest 리터럴("Harvest") 소멸(주석 포함 패러프레이즈 규약).
        /// </summary>
        [Test]
        public void DriverUnify_SourceContract_129()
        {
            var driverSrc = System.IO.File.ReadAllText("Assets/Scripts/Systems/HumanoidClipDriver.cs");
            Assert.That(driverSrc, Does.Contain("public void TriggerHarvest() { TriggerGather(); }"),
                "[129차 Q4] TriggerHarvest → TriggerGather 위임 단일화 존재.");
            Assert.That(driverSrc, Does.Not.Contain("SetTrigger(\"Harvest\")"),
                "[129차 Q4] 구 Harvest 리터럴 소멸 — 주석에도 리터럴 금지(패러프레이즈).");
        }

        /// <summary>
        /// [129차] 계약 3 — ControllerYamlContract: Player_AC.controller YAML 문자열 계약.
        /// Gather/Craft/Cheer 상태명 + 트리거 파라미터 4종(Gather/Craft/Cheer/ActionEnd) 존재.
        /// </summary>
        [Test]
        public void ControllerYamlContract_129()
        {
            var controllerSrc = System.IO.File.ReadAllText("Assets/Resources/Animation/Controllers/Player_AC.controller");
            foreach (var state in new[] { "Gather", "Craft", "Cheer" })
                Assert.That(controllerSrc, Does.Contain(state),
                    $"[129차] Player_AC.controller — 상태명 '{state}' 존재.");
            foreach (var trig in new[] { "Gather", "Craft", "Cheer", "ActionEnd" })
                Assert.That(controllerSrc, Does.Contain(trig),
                    $"[129차] Player_AC.controller — 트리거 파라미터 '{trig}' 존재.");
        }

        /// <summary>
        /// [129차] 계약 4 — TriggerGuard_SourceContract: HumanoidClipDriver에
        /// CanFireAction 가드(Gather/Craft/Cheer 상태 중 재발화 무시) 존재.
        /// </summary>
        [Test]
        public void TriggerGuard_SourceContract_129()
        {
            var driverSrc = System.IO.File.ReadAllText("Assets/Scripts/Systems/HumanoidClipDriver.cs");
            Assert.That(driverSrc, Does.Contain("CanFireAction"),
                "[129차] CanFireAction 가드 존재.");
            Assert.That(driverSrc, Does.Contain("IsName(\"Gather\") || cur.IsName(\"Craft\") || cur.IsName(\"Cheer\")"),
                "[129차] 가드 조건 — Gather/Craft/Cheer 상태 중 재발화 무시 조건 존재.");
        }
    }
}
