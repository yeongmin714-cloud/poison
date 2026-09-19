using System.Collections.Generic;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C-O1-04: 세트 보너스 통합(GuardEquipmentSystem.CalculateSetBonus) 테스트.
    /// 세트 합산 규칙 — 2개 소효과/4개 완성, 세트 간 중첩 합산, 1개 이하 무효.
    /// </summary>
    public class SetBonusIntegrationTests
    {
        private static List<PlayerInventory.ItemData> Items(params PlayerInventory.ItemData[] items)
        {
            return new List<PlayerInventory.ItemData>(items);
        }

        [Test]
        public void CalculateSetBonus_NullOrEmpty_ReturnsZero()
        {
            var b1 = GuardEquipmentSystem.CalculateSetBonus(null);
            Assert.AreEqual(0, b1.attackBonus);
            Assert.AreEqual(0, b1.defenseBonus);

            var b2 = GuardEquipmentSystem.CalculateSetBonus(new List<PlayerInventory.ItemData>());
            Assert.AreEqual(0, b2.attackBonus);
            Assert.AreEqual(0, b2.defenseBonus);
        }

        [Test]
        public void CalculateSetBonus_TwoLeatherPieces_SmallBonus()
        {
            var bonus = GuardEquipmentSystem.CalculateSetBonus(Items(
                PlayerInventory.HelmetWood, PlayerInventory.ArmorWood));
            Assert.AreEqual(1, bonus.attackBonus, "가죽 2세트 = 공+1");
            Assert.AreEqual(0, bonus.defenseBonus, "가죽 2세트는 방어 보너스 없음");
        }

        [Test]
        public void CalculateSetBonus_FullLeatherSet_CompletionBonus()
        {
            var bonus = GuardEquipmentSystem.CalculateSetBonus(Items(
                PlayerInventory.HelmetWood, PlayerInventory.ArmorWood,
                PlayerInventory.BootWood, PlayerInventory.GloveWood));
            Assert.AreEqual(1, bonus.attackBonus, "가죽 완성 = 공+1");
            Assert.AreEqual(1, bonus.defenseBonus, "가죽 완성 = 방+1");
        }

        [Test]
        public void CalculateSetBonus_FullChainSet_CompletionBonus()
        {
            var bonus = GuardEquipmentSystem.CalculateSetBonus(Items(
                PlayerInventory.HelmetSteel, PlayerInventory.ArmorSteel,
                PlayerInventory.BootSteel, PlayerInventory.GloveSteel));
            Assert.AreEqual(2, bonus.attackBonus, "사슬 완성 = 공+2");
            Assert.AreEqual(2, bonus.defenseBonus, "사슬 완성 = 방+2");
        }

        [Test]
        public void CalculateSetBonus_FullPlateSet_CompletionBonus()
        {
            var bonus = GuardEquipmentSystem.CalculateSetBonus(Items(
                PlayerInventory.HelmetStone, PlayerInventory.ArmorStone,
                PlayerInventory.BootStone, PlayerInventory.GloveCrystal));
            Assert.AreEqual(3, bonus.attackBonus, "판금 완성 = 공+3");
            Assert.AreEqual(3, bonus.defenseBonus, "판금 완성 = 방+3");
        }

        [Test]
        public void CalculateSetBonus_MixedSets_StackBonuses()
        {
            // 사슬 2파츠(방+2) + 판금 2파츠(방+3) = 방+5, 공 0
            var bonus = GuardEquipmentSystem.CalculateSetBonus(Items(
                PlayerInventory.HelmetSteel, PlayerInventory.ArmorSteel,     // 사슬 2
                PlayerInventory.HelmetStone, PlayerInventory.ArmorStone));   // 판금 2
            Assert.AreEqual(0, bonus.attackBonus);
            Assert.AreEqual(5, bonus.defenseBonus, "세트 간 보너스는 합산된다");
        }

        [Test]
        public void CalculateSetBonus_SinglePieceOfEachSet_NoBonus()
        {
            // 세트마다 1개씩 — 어느 세트도 2개에 도달하지 못함
            var bonus = GuardEquipmentSystem.CalculateSetBonus(Items(
                PlayerInventory.HelmetWood,     // 가죽 1
                PlayerInventory.HelmetSteel,    // 사슬 1
                PlayerInventory.HelmetStone));  // 판금 1
            Assert.AreEqual(0, bonus.attackBonus);
            Assert.AreEqual(0, bonus.defenseBonus);
        }

        [Test]
        public void CalculateSetBonus_DuplicateSamePiece_CountsOnce()
        {
            // 같은 파츠 중복 입력(장착 중복 방지 규칙) — 1개로만 카운트
            var bonus = GuardEquipmentSystem.CalculateSetBonus(Items(
                PlayerInventory.HelmetWood, PlayerInventory.HelmetWood));
            Assert.AreEqual(0, bonus.attackBonus, "동일 파츠 2개는 2세트로 인정되지 않음");
        }

        [Test]
        public void CalculateSetBonus_NonSetItems_Ignored()
        {
            // 세트 소속 아님(전설 유니크/소모품) — 보너스 0
            var unique = new PlayerInventory.ItemData
            {
                id = "unique_prince_sword",
                displayName = "왕가의 검",
                category = PlayerInventory.ItemCategory.Weapon,
                rarity = ItemRarity.Legendary,
                maxStack = 1,
            };
            var bonus = GuardEquipmentSystem.CalculateSetBonus(Items(unique, unique));
            Assert.AreEqual(0, bonus.attackBonus);
            Assert.AreEqual(0, bonus.defenseBonus);
        }
    }
}
