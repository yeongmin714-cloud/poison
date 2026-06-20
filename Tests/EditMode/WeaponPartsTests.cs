using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C9-29: 병사 무기 파츠 교체 시스템 테스트
    /// </summary>
    public class WeaponPartsTests
    {
        // ===================== EquipSlot enum 확인 =====================

        [Test]
        public void EquipSlot_Enum_HasFourValues()
        {
            var values = System.Enum.GetValues(typeof(EquipSlot));
            Assert.AreEqual(4, values.Length, "EquipSlot은 4개 값이어야 합니다");
            Assert.Contains(EquipSlot.Weapon, values);
            Assert.Contains(EquipSlot.Helmet, values);
            Assert.Contains(EquipSlot.Armor, values);
            Assert.Contains(EquipSlot.Shield, values);
        }

        // ===================== WeaponPartsSystem 기본 구조 =====================

        [Test]
        public void WeaponPartsSystem_IsStaticClass()
        {
            Assert.IsTrue(typeof(WeaponPartsSystem).IsAbstract && typeof(WeaponPartsSystem).IsSealed,
                "WeaponPartsSystem은 static 클래스여야 합니다");
        }

        [Test]
        public void WeaponPartsSystem_HasEquipResultStruct()
        {
            var resultType = typeof(WeaponPartsSystem.EquipResult);
            Assert.IsTrue(resultType.IsValueType, "EquipResult는 struct여야 합니다");
            var successField = resultType.GetField("success");
            var messageField = resultType.GetField("message");
            Assert.IsNotNull(successField, "EquipResult에 success 필드가 있어야 합니다");
            Assert.IsNotNull(messageField, "EquipResult에 message 필드가 있어야 합니다");
        }

        // ===================== GetSlotName 테스트 =====================

        [Test]
        public void GetSlotName_AllSlots_ReturnsStrings()
        {
            Assert.AreEqual("⚔️ 무기", WeaponPartsSystem.GetSlotName(EquipSlot.Weapon), "무기 슬롯 이름 확인");
            Assert.AreEqual("🪖 투구", WeaponPartsSystem.GetSlotName(EquipSlot.Helmet), "투구 슬롯 이름 확인");
            Assert.AreEqual("🛡️ 갑옷", WeaponPartsSystem.GetSlotName(EquipSlot.Armor), "갑옷 슬롯 이름 확인");
            Assert.AreEqual("🔰 방패", WeaponPartsSystem.GetSlotName(EquipSlot.Shield), "방패 슬롯 이름 확인");
        }

        // ===================== GetRequiredCategory 테스트 =====================

        [Test]
        public void GetRequiredCategory_Weapon_ReturnsWeaponCategory()
        {
            Assert.AreEqual(PlayerInventory.ItemCategory.Weapon,
                WeaponPartsSystem.GetRequiredCategory(EquipSlot.Weapon), "무기 슬롯의 필수 카테고리는 Weapon");
        }

        [Test]
        public void GetRequiredCategory_ArmorSlots_ReturnsArmorCategory()
        {
            Assert.AreEqual(PlayerInventory.ItemCategory.Armor,
                WeaponPartsSystem.GetRequiredCategory(EquipSlot.Helmet), "투구 슬롯의 필수 카테고리는 Armor");
            Assert.AreEqual(PlayerInventory.ItemCategory.Armor,
                WeaponPartsSystem.GetRequiredCategory(EquipSlot.Armor), "갑옷 슬롯의 필수 카테고리는 Armor");
            Assert.AreEqual(PlayerInventory.ItemCategory.Armor,
                WeaponPartsSystem.GetRequiredCategory(EquipSlot.Shield), "방패 슬롯의 필수 카테고리는 Armor");
        }

        // ===================== EquipItem 테스트 =====================

        [Test]
        public void EquipItem_NullGuard_ReturnsFail()
        {
            var itemSlot = new PlayerInventory.ItemSlot
            {
                item = PlayerInventory.SwordWood,
                count = 1,
                currentDurability = 20
            };

            var result = WeaponPartsSystem.EquipItem(null, EquipSlot.Weapon, itemSlot);
            Assert.IsFalse(result.success, "null 병사는 실패");
            Assert.IsNotEmpty(result.message, "실패 메시지가 있어야 함");
        }

        [Test]
        public void EquipItem_WrongCategory_ReturnsFail()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();

            // Force guard alive + recruited via reflection for EquipItem to pass
            SetGuardRecruitedAlive(guard);

            var itemSlot = new PlayerInventory.ItemSlot
            {
                item = new PlayerInventory.ItemData
                {
                    id = "test_food",
                    displayName = "테스트 음식",
                    category = PlayerInventory.ItemCategory.Food,
                    maxDurability = 0
                },
                count = 1
            };

            var result = WeaponPartsSystem.EquipItem(guard, EquipSlot.Weapon, itemSlot);
            Assert.IsFalse(result.success, "카테고리 불일치 시 실패");
            StringAssert.Contains("카테고리", result.message, "카테고리 관련 메시지");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void EquipItem_NoDurability_ReturnsFail()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            SetGuardRecruitedAlive(guard);

            var itemSlot = new PlayerInventory.ItemSlot
            {
                item = new PlayerInventory.ItemData
                {
                    id = "test_no_durability",
                    displayName = "내구도 없는 아이템",
                    category = PlayerInventory.ItemCategory.Weapon,
                    maxDurability = 0
                },
                count = 1
            };

            var result = WeaponPartsSystem.EquipItem(guard, EquipSlot.Weapon, itemSlot);
            Assert.IsFalse(result.success, "내구도 없는 아이템은 장착 불가");
            StringAssert.Contains("내구도", result.message);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void EquipItem_NullSlot_ReturnsFail()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            SetGuardRecruitedAlive(guard);

            var result = WeaponPartsSystem.EquipItem(guard, EquipSlot.Weapon, null);
            Assert.IsFalse(result.success, "null 슬롯은 실패");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void EquipItem_Weapon_SetsSlot()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            SetGuardRecruitedAlive(guard);

            var itemSlot = new PlayerInventory.ItemSlot
            {
                item = PlayerInventory.SwordWood,
                count = 1,
                currentDurability = 20
            };

            var result = WeaponPartsSystem.EquipItem(guard, EquipSlot.Weapon, itemSlot);
            Assert.IsTrue(result.success, "무기 장착 성공");

            var equipped = WeaponPartsSystem.GetEquippedItem(guard, EquipSlot.Weapon);
            Assert.IsNotNull(equipped, "장착된 무기가 있어야 함");
            Assert.AreEqual("weapon_sword_wood", equipped.id, "장착된 무기 ID 확인");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void EquipItem_ReplacesExistingItem()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            SetGuardRecruitedAlive(guard);

            // 첫 번째 무기 장착
            var firstSlot = new PlayerInventory.ItemSlot
            {
                item = PlayerInventory.SwordWood,
                count = 1,
                currentDurability = 20
            };

            var result1 = WeaponPartsSystem.EquipItem(guard, EquipSlot.Weapon, firstSlot);
            Assert.IsTrue(result1.success, "첫 번째 무기 장착 성공");

            // 두 번째 무기로 교체
            var secondSlot = new PlayerInventory.ItemSlot
            {
                item = PlayerInventory.SpearWood,
                count = 1,
                currentDurability = 20
            };

            var result2 = WeaponPartsSystem.EquipItem(guard, EquipSlot.Weapon, secondSlot);
            Assert.IsTrue(result2.success, "두 번째 무기 장착 성공");

            var equipped = WeaponPartsSystem.GetEquippedItem(guard, EquipSlot.Weapon);
            Assert.IsNotNull(equipped);
            Assert.AreEqual("weapon_spear_wood", equipped.id, "교체 후 창이 장착되어야 함");

            Object.DestroyImmediate(go);
        }

        // ===================== UnequipItem 테스트 =====================

        [Test]
        public void UnequipItem_EmptySlot_ReturnsFail()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            SetGuardRecruitedAlive(guard);

            var result = WeaponPartsSystem.UnequipItem(guard, EquipSlot.Weapon);
            Assert.IsFalse(result.success, "빈 슬롯 해제 시 실패");
            StringAssert.Contains("장비가 없습니다", result.message);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void UnequipItem_RemovesFromSlot()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            SetGuardRecruitedAlive(guard);

            // 먼저 장착
            guard.WeaponItem = PlayerInventory.SwordWood;

            var result = WeaponPartsSystem.UnequipItem(guard, EquipSlot.Weapon);
            Assert.IsTrue(result.success, "장비 해제 성공");
            Assert.IsNull(WeaponPartsSystem.GetEquippedItem(guard, EquipSlot.Weapon), "해제 후 슬롯이 비어있어야 함");

            Object.DestroyImmediate(go);
        }

        // ===================== GetEquippedItem / GetAllEquipped 테스트 =====================

        [Test]
        public void GetEquippedItem_ReturnsCorrectItem()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();

            guard.WeaponItem = PlayerInventory.SwordWood;
            guard.HelmetItem = PlayerInventory.LeatherArmor;

            var weapon = WeaponPartsSystem.GetEquippedItem(guard, EquipSlot.Weapon);
            Assert.AreEqual("weapon_sword_wood", weapon.id, "무기 아이템 확인");

            var helmet = WeaponPartsSystem.GetEquippedItem(guard, EquipSlot.Helmet);
            Assert.AreEqual("armor_leather", helmet.id, "투구 아이템 확인");

            var armor = WeaponPartsSystem.GetEquippedItem(guard, EquipSlot.Armor);
            Assert.IsNull(armor, "갑옷 슬롯은 비어있어야 함");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void GetAllEquipped_ReturnsAllSlots()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();

            guard.WeaponItem = PlayerInventory.SwordWood;
            guard.ArmorItem = PlayerInventory.LeatherArmor;

            var all = WeaponPartsSystem.GetAllEquipped(guard);
            Assert.AreEqual(2, all.Count, "2개의 장비가 장착되어 있어야 함");

            bool foundWeapon = false;
            bool foundArmor = false;
            foreach (var (slot, item) in all)
            {
                if (slot == EquipSlot.Weapon && item.id == "weapon_sword_wood") foundWeapon = true;
                if (slot == EquipSlot.Armor && item.id == "armor_leather") foundArmor = true;
            }
            Assert.IsTrue(foundWeapon, "무기가 목록에 있어야 함");
            Assert.IsTrue(foundArmor, "갑옷이 목록에 있어야 함");

            Object.DestroyImmediate(go);
        }

        // ===================== UpdateVisual 테스트 =====================

        [Test]
        public void UpdateVisual_ChangesColorWithArmor()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();

            // Add a Renderer for visual update
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.material = new Material(Shader.Find("Standard"));
            renderer.material.color = Color.blue;

            guard.ArmorItem = PlayerInventory.LeatherArmor;
            guard.UpdateVisual();

            var newColor = renderer.material.color;
            Assert.AreNotEqual(Color.blue, newColor, "갑옷 장착 시 색상이 변경되어야 함");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void UpdateVisual_ChangesScaleWithShield()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.material = new Material(Shader.Find("Standard"));

            guard.ShieldItem = PlayerInventory.LeatherArmor;
            guard.UpdateVisual();

            Assert.Greater(go.transform.localScale.x, 1f, "방패 장착 시 스케일 증가");
            Assert.Greater(go.transform.localScale.y, 1f, "방패 장착 시 스케일 증가");
            Assert.Greater(go.transform.localScale.z, 1f, "방패 장착 시 스케일 증가");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void UpdateVisual_WeaponAndHelmet_CombinedEffect()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.material = new Material(Shader.Find("Standard"));

            guard.WeaponItem = PlayerInventory.SwordWood;
            guard.HelmetItem = PlayerInventory.LeatherArmor;
            guard.UpdateVisual();

            var newColor = renderer.material.color;
            Assert.AreNotEqual(Color.blue, newColor, "무기+투구 장착 시 색상 변경");
            Assert.Greater(go.transform.localScale.x, 1f, "무기+투구 장착 시 스케일 증가");

            Object.DestroyImmediate(go);
        }

        // ===================== GuardPlaceholder 장비 속성 확인 =====================

        [Test]
        public void GuardPlaceholder_HasEquipProperties()
        {
            Assert.IsNotNull(typeof(GuardPlaceholder).GetProperty("WeaponItem"), "WeaponItem 속성 필요");
            Assert.IsNotNull(typeof(GuardPlaceholder).GetProperty("HelmetItem"), "HelmetItem 속성 필요");
            Assert.IsNotNull(typeof(GuardPlaceholder).GetProperty("ArmorItem"), "ArmorItem 속성 필요");
            Assert.IsNotNull(typeof(GuardPlaceholder).GetProperty("ShieldItem"), "ShieldItem 속성 필요");
        }

        [Test]
        public void GuardPlaceholder_HasUpdateVisualMethod()
        {
            var method = typeof(GuardPlaceholder).GetMethod("UpdateVisual");
            Assert.IsNotNull(method, "UpdateVisual 메서드 필요");
            Assert.AreEqual(typeof(void), method.ReturnType, "void 반환");
        }

        [Test]
        public void GuardPlaceholder_EquipFields_Serializable()
        {
            var fields = new[]
            {
                typeof(GuardPlaceholder).GetField("_weaponItem",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance),
                typeof(GuardPlaceholder).GetField("_helmetItem",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance),
                typeof(GuardPlaceholder).GetField("_armorItem",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance),
                typeof(GuardPlaceholder).GetField("_shieldItem",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance),
            };

            foreach (var field in fields)
            {
                Assert.IsNotNull(field, "필드가 있어야 합니다");
                Assert.IsTrue(field.IsDefined(typeof(SerializeField), false),
                    $"{field.Name}에 [SerializeField]가 있어야 합니다");
            }
        }

        // ===================== 헬퍼 메서드 =====================

        /// <summary>리플렉션으로 guard의 IsRecruited와 생존 상태를 강제 설정</summary>
        private void SetGuardRecruitedAlive(GuardPlaceholder guard)
        {
            var isRecruitedField = typeof(GuardPlaceholder).GetField("_isRecruited",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (isRecruitedField != null)
                isRecruitedField.SetValue(guard, true);

            // Force _isDead = false via the _currentHP field
            var hpField = typeof(GuardPlaceholder).GetField("_currentHP",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (hpField != null)
                hpField.SetValue(guard, 10f);
        }
    }
}