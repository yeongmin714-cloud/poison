using System.Collections.Generic;
using NUnit.Framework;
using ProjectName.Core;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C-O1-01: 장비 티어 세트(EquipmentTierSet) 테스트.
    /// 세트 구성 / 파밍 보상 원칙 / 소속 판정 / 파츠 카운트 / 보너스 규격.
    /// </summary>
    public class EquipmentTierSetTests
    {
        // ===================== 세트 구성 =====================

        [Test]
        public void GetSetParts_AllKinds_Have4Parts()
        {
            foreach (var kind in EquipmentTierSet.GetAllSetKinds())
            {
                var parts = EquipmentTierSet.GetSetParts(kind);
                Assert.AreEqual(4, parts.Length, $"{kind} 세트는 부위 4개(투구/갑옷/신발/장갑)");
                foreach (var part in parts)
                {
                    Assert.IsNotNull(part, $"{kind} 세트 부위는 모두 할당되어야 한다");
                }
            }
        }

        [Test]
        public void GetSetParts_AllPartsAreArmorCategory()
        {
            foreach (var kind in EquipmentTierSet.GetAllSetKinds())
            {
                foreach (var part in EquipmentTierSet.GetSetParts(kind))
                {
                    Assert.AreEqual(PlayerInventory.ItemCategory.Armor, part.category,
                        $"{kind} 세트 부위({part.id})는 방어구 카테고리");
                }
            }
        }

        [Test]
        public void GetSetParts_NoDuplicateIdsWithinSet()
        {
            foreach (var kind in EquipmentTierSet.GetAllSetKinds())
            {
                var ids = new HashSet<string>();
                foreach (var part in EquipmentTierSet.GetSetParts(kind))
                {
                    Assert.IsTrue(ids.Add(part.id), $"{kind} 세트 내 중복 id 없음: {part.id}");
                }
            }
        }

        // ===================== 파밍 보상 원칙 =====================

        [Test]
        public void FarmingPrinciple_Helmet_PlateGreaterThanChainGreaterThanLeather()
        {
            int leather = (int)PlayerInventory.HelmetWood.rarity;
            int chain   = (int)PlayerInventory.HelmetSteel.rarity;
            int plate   = (int)PlayerInventory.HelmetStone.rarity;
            Assert.GreaterOrEqual(plate, chain, "투구: 판금(돌) 등급 ≥ 사슬(강철)");
            Assert.Greater(chain, leather, "투구: 사슬(강철) 등급 > 가죽(나무)");
        }

        [Test]
        public void FarmingPrinciple_Armor_PlateGreaterThanChainGreaterThanLeather()
        {
            int leather = (int)PlayerInventory.ArmorWood.rarity;
            int chain   = (int)PlayerInventory.ArmorSteel.rarity;
            int plate   = (int)PlayerInventory.ArmorStone.rarity;
            Assert.GreaterOrEqual(plate, chain, "갑옷: 판금(돌) 등급 ≥ 사슬(강철)");
            Assert.Greater(chain, leather, "갑옷: 사슬(강철) 등급 > 가죽(나무)");
        }

        [Test]
        public void FarmingPrinciple_Boot_PlateGreaterThanChainGreaterThanLeather()
        {
            int leather = (int)PlayerInventory.BootWood.rarity;
            int chain   = (int)PlayerInventory.BootSteel.rarity;
            int plate   = (int)PlayerInventory.BootStone.rarity;
            Assert.GreaterOrEqual(plate, chain, "신발: 판금(돌) 등급 ≥ 사슬(강철)");
            Assert.Greater(chain, leather, "신발: 사슬(강철) 등급 > 가죽(나무)");
        }

        [Test]
        public void FarmingPrinciple_Glove_PlateGreaterThanChainGreaterThanLeather()
        {
            int leather = (int)PlayerInventory.GloveWood.rarity;
            int chain   = (int)PlayerInventory.GloveSteel.rarity;
            int plate   = (int)PlayerInventory.GloveCrystal.rarity;
            Assert.Greater(plate, chain, "장갑: 판금(수정) 등급 > 사슬(강철)");
            Assert.Greater(chain, leather, "장갑: 사슬(강철) 등급 > 가죽(나무)");
        }

        // ===================== 소속 판정 =====================

        [Test]
        public void GetSetForItem_WoodArmor_ReturnsLeather()
        {
            Assert.AreEqual(EquipmentSetKind.Leather, EquipmentTierSet.GetSetForItem("wood_armor"));
        }

        [Test]
        public void GetSetForItem_SteelGlove_ReturnsChain()
        {
            Assert.AreEqual(EquipmentSetKind.Chain, EquipmentTierSet.GetSetForItem("steel_glove"));
        }

        [Test]
        public void GetSetForItem_CrystalGlove_ReturnsPlate()
        {
            Assert.AreEqual(EquipmentSetKind.Plate, EquipmentTierSet.GetSetForItem("crystal_glove"));
        }

        [Test]
        public void GetSetForItem_UnknownItem_ReturnsNull()
        {
            Assert.IsNull(EquipmentTierSet.GetSetForItem("herb_silver"));
            Assert.IsNull(EquipmentTierSet.GetSetForItem(null));
            Assert.IsNull(EquipmentTierSet.GetSetForItem(""));
        }

        // ===================== 파츠 카운트 =====================

        [Test]
        public void CountSetPieces_EmptyOrNull_Returns0()
        {
            Assert.AreEqual(0, EquipmentTierSet.CountSetPieces(EquipmentSetKind.Leather, null));
            Assert.AreEqual(0, EquipmentTierSet.CountSetPieces(EquipmentSetKind.Chain, new List<PlayerInventory.ItemData>()));
        }

        [Test]
        public void CountSetPieces_PartialSet_CountsMatchingParts()
        {
            var equipped = new List<PlayerInventory.ItemData>
            {
                PlayerInventory.HelmetWood,   // 가죽 부위
                PlayerInventory.ArmorWood,    // 가죽 부위
                PlayerInventory.HelmetSteel,  // 사슬 부위 (카운트 대상 아님)
            };
            Assert.AreEqual(2, EquipmentTierSet.CountSetPieces(EquipmentSetKind.Leather, equipped));
            Assert.AreEqual(1, EquipmentTierSet.CountSetPieces(EquipmentSetKind.Chain, equipped));
            Assert.AreEqual(0, EquipmentTierSet.CountSetPieces(EquipmentSetKind.Plate, equipped));
        }

        [Test]
        public void CountSetPieces_RealPlateSet_Returns4_CrystalSet_Returns1()
        {
            // 실제 판금세트(돌 투구/갑옷/신발 + 수정 장갑) = 4개 완성
            var plate = new List<PlayerInventory.ItemData>
            {
                PlayerInventory.HelmetStone,
                PlayerInventory.ArmorStone,
                PlayerInventory.BootStone,
                PlayerInventory.GloveCrystal,
            };
            Assert.AreEqual(4, EquipmentTierSet.CountSetPieces(EquipmentSetKind.Plate, plate));

            // 전부 수정 세트라도 판금세트 소속은 장갑 1개뿐 (판금 세트는 돌 위주)
            var crystal = new List<PlayerInventory.ItemData>
            {
                PlayerInventory.HelmetCrystal,
                PlayerInventory.ArmorCrystal,
                PlayerInventory.BootCrystal,
                PlayerInventory.GloveCrystal,
            };
            Assert.AreEqual(1, EquipmentTierSet.CountSetPieces(EquipmentSetKind.Plate, crystal));
        }

        // ===================== 세트 보너스 =====================

        [Test]
        public void GetActiveSetBonus_ZeroOrOnePiece_ReturnsZero()
        {
            foreach (var kind in EquipmentTierSet.GetAllSetKinds())
            {
                var b0 = EquipmentTierSet.GetActiveSetBonus(kind, 0);
                var b1 = EquipmentTierSet.GetActiveSetBonus(kind, 1);
                Assert.AreEqual(0, b0.attackBonus, $"{kind} 0개 = 보너스 없음");
                Assert.AreEqual(0, b0.defenseBonus, $"{kind} 0개 = 보너스 없음");
                Assert.AreEqual(0, b1.attackBonus, $"{kind} 1개 = 보너스 없음");
                Assert.AreEqual(0, b1.defenseBonus, $"{kind} 1개 = 보너스 없음");
            }
        }

        [Test]
        public void GetActiveSetBonus_TwoPieces_SmallBonus()
        {
            var leather = EquipmentTierSet.GetActiveSetBonus(EquipmentSetKind.Leather, 2);
            Assert.AreEqual(1, leather.attackBonus);
            Assert.AreEqual(0, leather.defenseBonus);

            var chain = EquipmentTierSet.GetActiveSetBonus(EquipmentSetKind.Chain, 2);
            Assert.AreEqual(0, chain.attackBonus);
            Assert.AreEqual(2, chain.defenseBonus);

            var plate = EquipmentTierSet.GetActiveSetBonus(EquipmentSetKind.Plate, 3); // 3개도 소효과 유지
            Assert.AreEqual(0, plate.attackBonus);
            Assert.AreEqual(3, plate.defenseBonus);
        }

        [Test]
        public void GetActiveSetBonus_FourPieces_CompletionBonus()
        {
            var leather = EquipmentTierSet.GetActiveSetBonus(EquipmentSetKind.Leather, 4);
            Assert.AreEqual(1, leather.attackBonus);
            Assert.AreEqual(1, leather.defenseBonus);

            var chain = EquipmentTierSet.GetActiveSetBonus(EquipmentSetKind.Chain, 4);
            Assert.AreEqual(2, chain.attackBonus);
            Assert.AreEqual(2, chain.defenseBonus);

            var plate = EquipmentTierSet.GetActiveSetBonus(EquipmentSetKind.Plate, 4);
            Assert.AreEqual(3, plate.attackBonus);
            Assert.AreEqual(3, plate.defenseBonus);
        }

        [Test]
        public void GetActiveSetBonus_BonusDescriptions_NotEmpty()
        {
            foreach (var kind in EquipmentTierSet.GetAllSetKinds())
            {
                var b2 = EquipmentTierSet.GetActiveSetBonus(kind, 2);
                var b4 = EquipmentTierSet.GetActiveSetBonus(kind, 4);
                Assert.IsFalse(string.IsNullOrEmpty(b2.description), $"{kind} 2세트 설명 존재");
                Assert.IsFalse(string.IsNullOrEmpty(b4.description), $"{kind} 4세트 설명 존재");
            }
        }

        // ===================== 툴팁 =====================

        [Test]
        public void GetSetSummary_ContainsNamePartsAndBonuses()
        {
            string summary = EquipmentTierSet.GetSetSummary(EquipmentSetKind.Plate);
            StringAssert.Contains("판금세트", summary);
            StringAssert.Contains(PlayerInventory.ArmorStone.displayName, summary);
            StringAssert.Contains("2세트", summary);
            StringAssert.Contains("4세트", summary);
        }

        [Test]
        public void GetSetDisplayName_ReturnsKoreanNames()
        {
            Assert.AreEqual("가죽세트", EquipmentTierSet.GetSetDisplayName(EquipmentSetKind.Leather));
            Assert.AreEqual("사슬세트", EquipmentTierSet.GetSetDisplayName(EquipmentSetKind.Chain));
            Assert.AreEqual("판금세트", EquipmentTierSet.GetSetDisplayName(EquipmentSetKind.Plate));
        }
    }
}
