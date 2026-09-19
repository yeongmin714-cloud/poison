using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C-O2-03a: 수리비 골드 원장 연결 테스트.
    /// GetRepairCost 공식(등급/내구도 비례), ParseGrade 알 수 없는 등급 폴백,
    /// RepairItem ref gold 순수 로직(정확 차감/부족 실패), cost<=0 무차감,
    /// RepairInventorySlot null 가드 경로(EditMode에서 PlayerStats 인스턴스 생성 불가).
    /// </summary>
    public class RepairLedgerTests
    {
        // ── GetRepairCost: 등급/내구도 비례 공식 ────────────────────────────

        [Test]
        public void GetRepairCost_CommonGrade_ProportionalToDurabilityLoss()
        {
            // Common 계수 2g: 손실 50 × 2 = 100G
            int cost = EquipmentRepairSystem.GetRepairCost(50, 100, "Common");

            Assert.AreEqual(100, cost);
        }

        [Test]
        public void GetRepairCost_LegendaryGrade_ProportionalToDurabilityLoss()
        {
            // Legendary 계수 200g: 손실 90 × 200 = 18000G
            int cost = EquipmentRepairSystem.GetRepairCost(10, 100, "Legendary");

            Assert.AreEqual(18000, cost);
        }

        [Test]
        public void GetRepairCost_UnknownGrade_FallsBackToCommonMultiplier()
        {
            // ParseGrade 폴백: 알 수 없는 등급 문자열 → Common(2g) 계수 적용
            // 손실 10 × 2 = 20G (Common 동일 입력과 일치해야 함)
            int unknownCost = EquipmentRepairSystem.GetRepairCost(90, 100, "obsidian_glass");
            int commonCost = EquipmentRepairSystem.GetRepairCost(90, 100, "Common");

            Assert.AreEqual(commonCost, unknownCost);
            Assert.AreEqual(20, unknownCost);
        }

        // ── GetRepairCost: cost <= 0 케이스 ─────────────────────────────────

        [Test]
        public void GetRepairCost_ZeroLossOrInvalidMax_ReturnsZero()
        {
            // 내구도 손실 없음
            Assert.AreEqual(0, EquipmentRepairSystem.GetRepairCost(100, 100, "Common"));
            // 최대 내구도 0 이하 (내구도 개념 없는 아이템)
            Assert.AreEqual(0, EquipmentRepairSystem.GetRepairCost(0, 0, "Common"));
            Assert.AreEqual(0, EquipmentRepairSystem.GetRepairCost(50, -10, "Rare"));
        }

        // ── RepairItem: cost <= 0 → 차감 0 ──────────────────────────────────

        [Test]
        public void RepairItem_FullDurability_NoGoldDeducted()
        {
            int goldRef = 1000;

            var result = EquipmentRepairSystem.RepairItem(
                "sword_01", 100, 100, ref goldRef,
                ItemRarity.Common, PlayerInventory.ItemCategory.Weapon);

            Assert.IsFalse(result.success);
            Assert.AreEqual(1000, goldRef);       // 골드 무차감
            Assert.AreEqual(0, result.goldCost);  // 비용 0
        }

        // ── RepairItem: ref gold 흐름 (순수 로직 직접 호출) ──────────────────

        [Test]
        public void RepairItem_DeductsExactCost_FromRefGold()
        {
            int goldRef = 1000;

            // Common 2g × 손실 50 = 100G
            var result = EquipmentRepairSystem.RepairItem(
                "sword_01", 50, 100, ref goldRef,
                ItemRarity.Common, PlayerInventory.ItemCategory.Weapon);

            Assert.IsTrue(result.success);
            Assert.AreEqual(100, result.goldCost);
            Assert.AreEqual(900, goldRef);          // 정확히 차감
            Assert.AreEqual(100, result.newDurability); // 최대치 회복
        }

        [Test]
        public void RepairItem_InsufficientGold_FailureMessageAndNoDeduction()
        {
            int goldRef = 50;

            // 필요 100G, 보유 50G → 실패
            var result = EquipmentRepairSystem.RepairItem(
                "sword_01", 50, 100, ref goldRef,
                ItemRarity.Common, PlayerInventory.ItemCategory.Weapon);

            Assert.IsFalse(result.success);
            Assert.AreEqual(50, goldRef);         // 실패 시 무차감
            Assert.AreEqual(100, result.goldCost);
            StringAssert.Contains("골드 부족", result.message);
            StringAssert.Contains("필요: 100G", result.message);
            StringAssert.Contains("보유: 50G", result.message);
        }

        // ── RepairInventorySlot: 지갑 연결부 null 가드 경로 ─────────────────

        [Test]
        public void RepairInventorySlot_NoSingletons_FailsSafely()
        {
            // EditMode: PlayerInventory/PlayerStats 인스턴스 없음 →
            // 지갑(PlayerStats.Gold) 조회 전 null 가드에서 안전 실패 (예외 없음)
            var result = EquipmentRepairSystem.RepairInventorySlot(0);

            Assert.IsFalse(result.success);
            StringAssert.Contains("인벤토리를 찾을 수 없습니다", result.message);
        }
    }
}
