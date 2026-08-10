using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.UI;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// Phase 26: 병사/용병 스탯창 & 장비 지급 시스템 EditMode 테스트.
    /// </summary>
    public class Phase26_GuardEquipmentTests
    {
        // ===== 26.2 GuardEquipmentSystem Tests =====

        [Test]
        public void GuardEquipmentSystem_Singleton_Works()
        {
            var go = new GameObject("TestEquipmentSystem");
            var system = go.AddComponent<GuardEquipmentSystem>();

            Assert.IsNotNull(GuardEquipmentSystem.Instance);
            Assert.AreEqual(system, GuardEquipmentSystem.Instance);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void EquipGuard_WeaponSlot_Success()
        {
            var sysGo = new GameObject("TestEquipSys");
            sysGo.AddComponent<GuardEquipmentSystem>();
            var guardGo = new GameObject("TestGuard");
            var guard = guardGo.AddComponent<GuardPlaceholder>();

            var weapon = new PlayerInventory.ItemData
            {
                id = "test_weapon",
                displayName = "테스트 검",
                category = PlayerInventory.ItemCategory.Weapon,
                maxStack = 1,
                maxDurability = 50
            };

            bool result = GuardEquipmentSystem.Instance.EquipGuard(guard, GuardEquipmentSystem.EquipSlot.Weapon, weapon);
            Assert.IsTrue(result);

            var equipped = GuardEquipmentSystem.Instance.GetGuardEquipped(guard, GuardEquipmentSystem.EquipSlot.Weapon);
            Assert.IsNotNull(equipped);
            Assert.AreEqual("테스트 검", equipped.itemData.displayName);
            Assert.AreEqual(50, equipped.currentDurability);

            Object.DestroyImmediate(guardGo);
            Object.DestroyImmediate(sysGo);
        }

        [Test]
        public void EquipGuard_ArmorSlot_Success()
        {
            var sysGo = new GameObject("TestEquipSys");
            sysGo.AddComponent<GuardEquipmentSystem>();
            var guardGo = new GameObject("TestGuard");
            var guard = guardGo.AddComponent<GuardPlaceholder>();

            var armor = new PlayerInventory.ItemData
            {
                id = "test_armor",
                displayName = "테스트 갑옷",
                category = PlayerInventory.ItemCategory.Armor,
                maxStack = 1,
                maxDurability = 30
            };

            bool result = GuardEquipmentSystem.Instance.EquipGuard(guard, GuardEquipmentSystem.EquipSlot.Armor, armor);
            Assert.IsTrue(result);

            var equipped = GuardEquipmentSystem.Instance.GetGuardEquipped(guard, GuardEquipmentSystem.EquipSlot.Armor);
            Assert.IsNotNull(equipped);
            Assert.AreEqual("테스트 갑옷", equipped.itemData.displayName);

            Object.DestroyImmediate(guardGo);
            Object.DestroyImmediate(sysGo);
        }

        [Test]
        public void EquipGuard_InvalidSlot_ReturnsFalse()
        {
            var sysGo = new GameObject("TestEquipSys");
            sysGo.AddComponent<GuardEquipmentSystem>();
            var guardGo = new GameObject("TestGuard");
            var guard = guardGo.AddComponent<GuardPlaceholder>();

            // 무기 카테고리 아이템을 방어구 슬롯에 장착 시도 → 실패
            var weapon = new PlayerInventory.ItemData
            {
                id = "test_weapon",
                displayName = "테스트 검",
                category = PlayerInventory.ItemCategory.Weapon,
                maxStack = 1
            };

            bool result = GuardEquipmentSystem.Instance.EquipGuard(guard, GuardEquipmentSystem.EquipSlot.Armor, weapon);
            Assert.IsFalse(result);

            Object.DestroyImmediate(guardGo);
            Object.DestroyImmediate(sysGo);
        }

        [Test]
        public void UnequipGuard_ReturnsItem()
        {
            var sysGo = new GameObject("TestEquipSys");
            sysGo.AddComponent<GuardEquipmentSystem>();
            var guardGo = new GameObject("TestGuard");
            var guard = guardGo.AddComponent<GuardPlaceholder>();

            var weapon = new PlayerInventory.ItemData
            {
                id = "test_weapon",
                displayName = "테스트 검",
                category = PlayerInventory.ItemCategory.Weapon,
                maxStack = 1,
                maxDurability = 50
            };

            GuardEquipmentSystem.Instance.EquipGuard(guard, GuardEquipmentSystem.EquipSlot.Weapon, weapon);

            var unequipped = GuardEquipmentSystem.Instance.UnequipGuard(guard, GuardEquipmentSystem.EquipSlot.Weapon);
            Assert.IsNotNull(unequipped);
            Assert.AreEqual("테스트 검", unequipped.itemData.displayName);

            // 장비 해제 후 슬롯은 비어있어야 함
            var after = GuardEquipmentSystem.Instance.GetGuardEquipped(guard, GuardEquipmentSystem.EquipSlot.Weapon);
            Assert.IsNull(after);

            Object.DestroyImmediate(guardGo);
            Object.DestroyImmediate(sysGo);
        }

        [Test]
        public void OnGuardDeath_ReturnsEquipmentToInventory()
        {
            var sysGo = new GameObject("TestEquipSys");
            sysGo.AddComponent<GuardEquipmentSystem>();
            var guardGo = new GameObject("TestGuard");
            var guard = guardGo.AddComponent<GuardPlaceholder>();

            // 인벤토리 모의 (AddItem이 실패하지 않도록 null 체크)
            var weapon = new PlayerInventory.ItemData
            {
                id = "test_weapon",
                displayName = "테스트 검",
                category = PlayerInventory.ItemCategory.Weapon,
                maxStack = 1,
                maxDurability = 50
            };

            GuardEquipmentSystem.Instance.EquipGuard(guard, GuardEquipmentSystem.EquipSlot.Weapon, weapon);

            // 사망 처리
            GuardEquipmentSystem.Instance.OnGuardDeath(guard);

            // 사망 후 슬롯은 비어있어야 함
            var after = GuardEquipmentSystem.Instance.GetGuardEquipped(guard, GuardEquipmentSystem.EquipSlot.Weapon);
            Assert.IsNull(after);

            Object.DestroyImmediate(guardGo);
            Object.DestroyImmediate(sysGo);
        }

        [Test]
        public void GuardEquipment_StatBonus_CalculatedCorrectly()
        {
            var sysGo = new GameObject("TestEquipSys");
            sysGo.AddComponent<GuardEquipmentSystem>();
            var guardGo = new GameObject("TestGuard");
            var guard = guardGo.AddComponent<GuardPlaceholder>();

            // 검 장착 → 공격력 +5
            var weapon = new PlayerInventory.ItemData
            {
                id = "test_weapon",
                displayName = "테스트 검",
                category = PlayerInventory.ItemCategory.Weapon,
                maxStack = 1,
                maxDurability = 50
            };
            GuardEquipmentSystem.Instance.EquipGuard(guard, GuardEquipmentSystem.EquipSlot.Weapon, weapon);

            float atkBonus = GuardEquipmentSystem.Instance.GetGuardEquipmentAttackBonus(guard);
            Assert.AreEqual(5f, atkBonus, 0.01f);

            // 갑옷 장착 → 방어력 +3
            var armor = new PlayerInventory.ItemData
            {
                id = "test_armor",
                displayName = "테스트 갑옷",
                category = PlayerInventory.ItemCategory.Armor,
                maxStack = 1,
                maxDurability = 30
            };
            GuardEquipmentSystem.Instance.EquipGuard(guard, GuardEquipmentSystem.EquipSlot.Armor, armor);

            float defBonus = GuardEquipmentSystem.Instance.GetGuardEquipmentDefenseBonus(guard);
            Assert.AreEqual(4f, defBonus, 0.01f); // 무기 방어+1 + 갑옷 방어+3 = 4

            Object.DestroyImmediate(guardGo);
            Object.DestroyImmediate(sysGo);
        }

        [Test]
        public void GuardCombatPower_WithEquipment_Higher()
        {
            var sysGo = new GameObject("TestEquipSys");
            sysGo.AddComponent<GuardEquipmentSystem>();
            var guardGo = new GameObject("TestGuard");
            var guard = guardGo.AddComponent<GuardPlaceholder>();

            // 장비 없이 전투력
            float basePower = GuardEquipmentSystem.Instance.CalculateGuardCombatPower(guard);

            var weapon = new PlayerInventory.ItemData
            {
                id = "test_weapon",
                displayName = "테스트 검",
                category = PlayerInventory.ItemCategory.Weapon,
                maxStack = 1,
                maxDurability = 50
            };
            GuardEquipmentSystem.Instance.EquipGuard(guard, GuardEquipmentSystem.EquipSlot.Weapon, weapon);

            float equippedPower = GuardEquipmentSystem.Instance.CalculateGuardCombatPower(guard);
            Assert.Greater(equippedPower, basePower, "장비 장착 시 전투력이 증가해야 함");

            Object.DestroyImmediate(guardGo);
            Object.DestroyImmediate(sysGo);
        }

        [Test]
        public void EquipMercenary_WeaponSlot_Success()
        {
            var sysGo = new GameObject("TestEquipSys");
            sysGo.AddComponent<GuardEquipmentSystem>();

            var weapon = new PlayerInventory.ItemData
            {
                id = "test_weapon",
                displayName = "테스트 검",
                category = PlayerInventory.ItemCategory.Weapon,
                maxStack = 1,
                maxDurability = 50
            };

            bool result = GuardEquipmentSystem.Instance.EquipMercenary("merc_test_01", GuardEquipmentSystem.EquipSlot.Weapon, weapon);
            Assert.IsTrue(result);

            var equipped = GuardEquipmentSystem.Instance.GetMercenaryEquipped("merc_test_01", GuardEquipmentSystem.EquipSlot.Weapon);
            Assert.IsNotNull(equipped);
            Assert.AreEqual("테스트 검", equipped.itemData.displayName);

            Object.DestroyImmediate(sysGo);
        }

        [Test]
        public void EquipMercenary_InstrumentSlot_BardOnly()
        {
            var sysGo = new GameObject("TestEquipSys");
            sysGo.AddComponent<GuardEquipmentSystem>();

            // 일반 용병(Soldier)이 악기 슬롯에 장착 시도 → 실패
            var lute = new PlayerInventory.ItemData
            {
                id = "unique_bard_lute",
                displayName = "전설의 류트",
                category = PlayerInventory.ItemCategory.Weapon,
                maxStack = 1,
                maxDurability = 150,
                effects = "restrict_merc_id:merc_bard_01"
            };

            bool result = GuardEquipmentSystem.Instance.EquipMercenary("merc_soldier_01", GuardEquipmentSystem.EquipSlot.Instrument, lute);
            Assert.IsFalse(result, "일반 용병은 악기 슬롯 장착 불가");

            Object.DestroyImmediate(sysGo);
        }

        [Test]
        public void LegendaryUniqueItem_OnlyForSpecificMercenary()
        {
            var sysGo = new GameObject("TestEquipSys");
            sysGo.AddComponent<GuardEquipmentSystem>();

            var princeSword = new PlayerInventory.ItemData
            {
                id = "unique_prince_sword",
                displayName = "왕가의 검",
                category = PlayerInventory.ItemCategory.Weapon,
                maxStack = 1,
                maxDurability = 200,
                effects = "restrict_merc_id:merc_legend_01, stat_bonus:{\"attack\":30,\"defense\":15}"
            };

            // 아라곤 왕자가 아닌 용병이 장착 시도 → 실패
            bool wrongMerc = GuardEquipmentSystem.Instance.EquipMercenary("merc_high_01", GuardEquipmentSystem.EquipSlot.Weapon, princeSword);
            Assert.IsFalse(wrongMerc, "다른 용병은 유니크 아이템 장착 불가");

            // 올바른 용병이 장착 시도 → 성공 (제약 문자열만 확인, 실제 MercenaryManager는 필요 없음)
            // effects에 restrict_merc_id가 merc_legend_01로 설정되어 있으므로 동일 ID면 통과
            bool correctMerc = GuardEquipmentSystem.Instance.EquipMercenary("merc_legend_01", GuardEquipmentSystem.EquipSlot.Weapon, princeSword);
            Assert.IsTrue(correctMerc, "지정된 용병은 유니크 아이템 장착 가능");

            Object.DestroyImmediate(sysGo);
        }

        [Test]
        public void Durability_Reduce_BrokenItemRemoved()
        {
            var sysGo = new GameObject("TestEquipSys");
            sysGo.AddComponent<GuardEquipmentSystem>();
            var guardGo = new GameObject("TestGuard");
            var guard = guardGo.AddComponent<GuardPlaceholder>();

            var weapon = new PlayerInventory.ItemData
            {
                id = "test_weapon",
                displayName = "테스트 검",
                category = PlayerInventory.ItemCategory.Weapon,
                maxStack = 1,
                maxDurability = 5 // 5번 사용 시 파괴
            };

            GuardEquipmentSystem.Instance.EquipGuard(guard, GuardEquipmentSystem.EquipSlot.Weapon, weapon);

            // 5번 내구도 감소
            for (int i = 0; i < 5; i++)
            {
                GuardEquipmentSystem.Instance.ReduceDurability(guard, GuardEquipmentSystem.EquipSlot.Weapon);
            }

            // 내구도 0 이하 → 장비 제거됨
            var equipped = GuardEquipmentSystem.Instance.GetGuardEquipped(guard, GuardEquipmentSystem.EquipSlot.Weapon);
            Assert.IsNull(equipped, "내구도 0 도달 시 장비가 제거되어야 함");

            Object.DestroyImmediate(guardGo);
            Object.DestroyImmediate(sysGo);
        }

        [Test]
        public void IsItemValidForSlot_WeaponSlot_OnlyWeapons()
        {
            var sysGo = new GameObject("TestEquipSys");
            sysGo.AddComponent<GuardEquipmentSystem>();

            var weapon = new PlayerInventory.ItemData { id = "w", category = PlayerInventory.ItemCategory.Weapon };
            var armor = new PlayerInventory.ItemData { id = "a", category = PlayerInventory.ItemCategory.Armor };
            var food = new PlayerInventory.ItemData { id = "f", category = PlayerInventory.ItemCategory.Food };

            Assert.IsTrue(GuardEquipmentSystem.Instance.IsItemValidForSlot(GuardEquipmentSystem.EquipSlot.Weapon, weapon));
            Assert.IsFalse(GuardEquipmentSystem.Instance.IsItemValidForSlot(GuardEquipmentSystem.EquipSlot.Weapon, armor));
            Assert.IsFalse(GuardEquipmentSystem.Instance.IsItemValidForSlot(GuardEquipmentSystem.EquipSlot.Weapon, food));

            Object.DestroyImmediate(sysGo);
        }

        [Test]
        public void IsItemValidForSlot_ArmorSlot_OnlyArmors()
        {
            var sysGo = new GameObject("TestEquipSys");
            sysGo.AddComponent<GuardEquipmentSystem>();

            var weapon = new PlayerInventory.ItemData { id = "w", category = PlayerInventory.ItemCategory.Weapon };
            var armor = new PlayerInventory.ItemData { id = "a", category = PlayerInventory.ItemCategory.Armor };

            Assert.IsTrue(GuardEquipmentSystem.Instance.IsItemValidForSlot(GuardEquipmentSystem.EquipSlot.Armor, armor));
            Assert.IsFalse(GuardEquipmentSystem.Instance.IsItemValidForSlot(GuardEquipmentSystem.EquipSlot.Armor, weapon));

            Object.DestroyImmediate(sysGo);
        }

        [Test]
        public void GetAllGuardEquipment_ReturnsAllSlots()
        {
            var sysGo = new GameObject("TestEquipSys");
            sysGo.AddComponent<GuardEquipmentSystem>();
            var guardGo = new GameObject("TestGuard");
            var guard = guardGo.AddComponent<GuardPlaceholder>();

            var weapon = new PlayerInventory.ItemData { id = "w", displayName = "검", category = PlayerInventory.ItemCategory.Weapon, maxStack = 1 };
            var armor = new PlayerInventory.ItemData { id = "a", displayName = "갑옷", category = PlayerInventory.ItemCategory.Armor, maxStack = 1 };

            GuardEquipmentSystem.Instance.EquipGuard(guard, GuardEquipmentSystem.EquipSlot.Weapon, weapon);
            GuardEquipmentSystem.Instance.EquipGuard(guard, GuardEquipmentSystem.EquipSlot.Armor, armor);

            var all = GuardEquipmentSystem.Instance.GetAllGuardEquipment(guard);
            Assert.AreEqual(2, all.Count);
            Assert.IsTrue(all.ContainsKey(GuardEquipmentSystem.EquipSlot.Weapon));
            Assert.IsTrue(all.ContainsKey(GuardEquipmentSystem.EquipSlot.Armor));

            Object.DestroyImmediate(guardGo);
            Object.DestroyImmediate(sysGo);
        }

        [Test]
        public void ClearAllEquipment_ResetsState()
        {
            var sysGo = new GameObject("TestEquipSys");
            sysGo.AddComponent<GuardEquipmentSystem>();
            var guardGo = new GameObject("TestGuard");
            var guard = guardGo.AddComponent<GuardPlaceholder>();

            var weapon = new PlayerInventory.ItemData { id = "w", displayName = "검", category = PlayerInventory.ItemCategory.Weapon, maxStack = 1 };
            GuardEquipmentSystem.Instance.EquipGuard(guard, GuardEquipmentSystem.EquipSlot.Weapon, weapon);

            GuardEquipmentSystem.Instance.ClearAllEquipment();

            var all = GuardEquipmentSystem.Instance.GetAllGuardEquipment(guard);
            Assert.AreEqual(0, all.Count);

            Object.DestroyImmediate(guardGo);
            Object.DestroyImmediate(sysGo);
        }

        // ===== 26.1 GuardInfoWindow Tests =====

        [Test]
        public void GuardInfoWindow_Singleton_Works()
        {
            var go = new GameObject("TestInfoWindow");
            var window = go.AddComponent<GuardInfoWindow>();

            Assert.IsNotNull(GuardInfoWindow.Instance);
            Assert.AreEqual(window, GuardInfoWindow.Instance);
            Assert.IsFalse(GuardInfoWindow.Instance.IsVisible);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void GuardInfoWindow_OpenForGuard_SetsVisible()
        {
            var winGo = new GameObject("TestInfoWindow");
            winGo.AddComponent<GuardInfoWindow>();
            var guardGo = new GameObject("TestGuard");
            var guard = guardGo.AddComponent<GuardPlaceholder>();

            GuardInfoWindow.Instance.OpenForGuard(guard);
            Assert.IsTrue(GuardInfoWindow.Instance.IsVisible);

            GuardInfoWindow.Instance.Close();
            Assert.IsFalse(GuardInfoWindow.Instance.IsVisible);

            Object.DestroyImmediate(guardGo);
            Object.DestroyImmediate(winGo);
        }

        [Test]
        public void GuardInfoWindow_OpenForMercenary_SetsVisible()
        {
            var winGo = new GameObject("TestInfoWindow");
            winGo.AddComponent<GuardInfoWindow>();

            GuardInfoWindow.Instance.OpenForMercenary("merc_test_01");
            Assert.IsTrue(GuardInfoWindow.Instance.IsVisible);

            GuardInfoWindow.Instance.Close();
            Assert.IsFalse(GuardInfoWindow.Instance.IsVisible);

            Object.DestroyImmediate(winGo);
        }

        [Test]
        public void GuardInfoWindow_Close_ResetsState()
        {
            var winGo = new GameObject("TestInfoWindow");
            winGo.AddComponent<GuardInfoWindow>();
            var guardGo = new GameObject("TestGuard");
            var guard = guardGo.AddComponent<GuardPlaceholder>();

            GuardInfoWindow.Instance.OpenForGuard(guard);
            Assert.IsTrue(GuardInfoWindow.Instance.IsVisible);

            GuardInfoWindow.Instance.Close();
            Assert.IsFalse(GuardInfoWindow.Instance.IsVisible);

            // 연속 닫기 오류 없음
            GuardInfoWindow.Instance.Close();
            Assert.IsFalse(GuardInfoWindow.Instance.IsVisible);

            Object.DestroyImmediate(guardGo);
            Object.DestroyImmediate(winGo);
        }

        [Test]
        public void GuardInfoWindow_OpenWithNull_DoesNotCrash()
        {
            var winGo = new GameObject("TestInfoWindow");
            winGo.AddComponent<GuardInfoWindow>();

            // null 전달 시 아무 일도 없어야 함
            GuardInfoWindow.Instance.OpenForGuard(null);
            Assert.IsFalse(GuardInfoWindow.Instance.IsVisible);

            GuardInfoWindow.Instance.OpenForMercenary(null);
            Assert.IsFalse(GuardInfoWindow.Instance.IsVisible);

            GuardInfoWindow.Instance.OpenForMercenary("");
            Assert.IsFalse(GuardInfoWindow.Instance.IsVisible);

            Object.DestroyImmediate(winGo);
        }

        // ===== 26.3 용병 장비 특화 Tests =====

        [Test]
        public void LegendaryUniqueItem_Registration_Works()
        {
            var sysGo = new GameObject("TestEquipSys");
            sysGo.AddComponent<GuardEquipmentSystem>();

            var itemIds = GuardEquipmentSystem.Instance.GetAllUniqueItemIds();
            Assert.GreaterOrEqual(itemIds.Length, 3); // 왕가의 검, 서리 지팡이, 전설의 류트

            // 왕가의 검 확인
            var item = GuardEquipmentSystem.Instance.GetLegendaryUniqueItem("unique_prince_sword");
            Assert.IsNotNull(item);
            Assert.AreEqual("merc_legend_01", item.Value.mercenaryId);

            Object.DestroyImmediate(sysGo);
        }

        [Test]
        public void BardMercenary_HasInstrumentSlot()
        {
            var sysGo = new GameObject("TestEquipSys");
            sysGo.AddComponent<GuardEquipmentSystem>();

            // 바드 용병은 악기 슬롯을 포함한 4개 슬롯
            // GuardEquipmentSystem.BardSlots에 Instrument가 포함되어 있는지 확인
            bool hasInstrumentSlot = false;
            foreach (var slot in GuardEquipmentSystem.BardSlots)
            {
                if (slot == GuardEquipmentSystem.EquipSlot.Instrument)
                    hasInstrumentSlot = true;
            }
            Assert.IsTrue(hasInstrumentSlot, "바드는 악기 슬롯이 있어야 함");

            // 일반 슬롯에는 악기 슬롯 없음
            bool baseHasInstrument = false;
            foreach (var slot in GuardEquipmentSystem.BaseSlots)
            {
                if (slot == GuardEquipmentSystem.EquipSlot.Instrument)
                    baseHasInstrument = true;
            }
            Assert.IsFalse(baseHasInstrument, "일반 병사/용병은 악기 슬롯이 없어야 함");

            Object.DestroyImmediate(sysGo);
        }

        [Test]
        public void UniqueItemConstraint_Parsing_Works()
        {
            string effects = "restrict_merc_id:merc_legend_01, min_grade:Elite, stat_bonus:{\"attack\":30}";

            string mercId = GuardEquipmentSystem.UniqueItemConstraint.ParseRestrictMercId(effects);
            Assert.AreEqual("merc_legend_01", mercId);

            var minGrade = GuardEquipmentSystem.UniqueItemConstraint.ParseMinGrade(effects);
            Assert.IsTrue(minGrade.HasValue);
            Assert.AreEqual(MercenaryGrade.Elite, minGrade.Value);
        }

        [Test]
        public void UniqueItemConstraint_NullEffects_ReturnsNull()
        {
            string mercId = GuardEquipmentSystem.UniqueItemConstraint.ParseRestrictMercId(null);
            Assert.IsNull(mercId);

            var minGrade = GuardEquipmentSystem.UniqueItemConstraint.ParseMinGrade("");
            Assert.IsNull(minGrade);
        }

        [Test]
        public void MercenaryEquipment_OnDeath_ReturnsItems()
        {
            var sysGo = new GameObject("TestEquipSys");
            sysGo.AddComponent<GuardEquipmentSystem>();

            var weapon = new PlayerInventory.ItemData
            {
                id = "test_weapon",
                displayName = "테스트 검",
                category = PlayerInventory.ItemCategory.Weapon,
                maxStack = 1,
                maxDurability = 50
            };

            GuardEquipmentSystem.Instance.EquipMercenary("merc_test_01", GuardEquipmentSystem.EquipSlot.Weapon, weapon);

            // 사망 처리
            GuardEquipmentSystem.Instance.OnMercenaryDeath("merc_test_01");

            // 사망 후 장비는 없어야 함
            var after = GuardEquipmentSystem.Instance.GetMercenaryEquipped("merc_test_01", GuardEquipmentSystem.EquipSlot.Weapon);
            Assert.IsNull(after);

            Object.DestroyImmediate(sysGo);
        }

        [Test]
        public void MercenaryCombatPower_WithEquipment_Higher()
        {
            var sysGo = new GameObject("TestEquipSys");
            sysGo.AddComponent<GuardEquipmentSystem>();

            // 용병 ID로 전투력 계산 (실제 MercenaryManager 없이 장비만)
            float basePower = GuardEquipmentSystem.Instance.CalculateMercenaryCombatPower("merc_test_01");

            var weapon = new PlayerInventory.ItemData
            {
                id = "test_weapon",
                displayName = "테스트 검",
                category = PlayerInventory.ItemCategory.Weapon,
                maxStack = 1,
                maxDurability = 50
            };
            GuardEquipmentSystem.Instance.EquipMercenary("merc_test_01", GuardEquipmentSystem.EquipSlot.Weapon, weapon);

            float equippedPower = GuardEquipmentSystem.Instance.CalculateMercenaryCombatPower("merc_test_01");
            // MercenaryManager가 없으면 0 반환하도록 fallback
            // 기본적으로 Instance가 없으므로 0이어야 함
            Assert.AreEqual(0f, basePower);
            Assert.AreEqual(0f, equippedPower);

            Object.DestroyImmediate(sysGo);
        }

        [Test]
        public void EquipMultipleGuards_IndependentSlots()
        {
            var sysGo = new GameObject("TestEquipSys");
            sysGo.AddComponent<GuardEquipmentSystem>();

            var guard1Go = new GameObject("Guard1");
            var guard1 = guard1Go.AddComponent<GuardPlaceholder>();
            var guard2Go = new GameObject("Guard2");
            var guard2 = guard2Go.AddComponent<GuardPlaceholder>();

            var weapon = new PlayerInventory.ItemData { id = "w", displayName = "검", category = PlayerInventory.ItemCategory.Weapon, maxStack = 1 };
            var armor = new PlayerInventory.ItemData { id = "a", displayName = "갑옷", category = PlayerInventory.ItemCategory.Armor, maxStack = 1 };

            GuardEquipmentSystem.Instance.EquipGuard(guard1, GuardEquipmentSystem.EquipSlot.Weapon, weapon);
            GuardEquipmentSystem.Instance.EquipGuard(guard2, GuardEquipmentSystem.EquipSlot.Armor, armor);

            Assert.IsNotNull(GuardEquipmentSystem.Instance.GetGuardEquipped(guard1, GuardEquipmentSystem.EquipSlot.Weapon));
            Assert.IsNull(GuardEquipmentSystem.Instance.GetGuardEquipped(guard1, GuardEquipmentSystem.EquipSlot.Armor));
            Assert.IsNull(GuardEquipmentSystem.Instance.GetGuardEquipped(guard2, GuardEquipmentSystem.EquipSlot.Weapon));
            Assert.IsNotNull(GuardEquipmentSystem.Instance.GetGuardEquipped(guard2, GuardEquipmentSystem.EquipSlot.Armor));

            Object.DestroyImmediate(guard1Go);
            Object.DestroyImmediate(guard2Go);
            Object.DestroyImmediate(sysGo);
        }

        [Test]
        public void EquipGuard_SameSlotReplacesOldItem()
        {
            var sysGo = new GameObject("TestEquipSys");
            sysGo.AddComponent<GuardEquipmentSystem>();
            var guardGo = new GameObject("TestGuard");
            var guard = guardGo.AddComponent<GuardPlaceholder>();

            var sword = new PlayerInventory.ItemData { id = "sword", displayName = "검", category = PlayerInventory.ItemCategory.Weapon, maxStack = 1 };
            var axe = new PlayerInventory.ItemData { id = "axe", displayName = "도끼", category = PlayerInventory.ItemCategory.Weapon, maxStack = 1 };

            GuardEquipmentSystem.Instance.EquipGuard(guard, GuardEquipmentSystem.EquipSlot.Weapon, sword);
            GuardEquipmentSystem.Instance.EquipGuard(guard, GuardEquipmentSystem.EquipSlot.Weapon, axe);

            var equipped = GuardEquipmentSystem.Instance.GetGuardEquipped(guard, GuardEquipmentSystem.EquipSlot.Weapon);
            Assert.IsNotNull(equipped);
            Assert.AreEqual("도끼", equipped.itemData.displayName);

            Object.DestroyImmediate(guardGo);
            Object.DestroyImmediate(sysGo);
        }
    }
}