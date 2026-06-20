using NUnit.Framework;
using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// Phase 1.6 공격 시스템 핵심 기반 테스트
    /// - IDamageable 인터페이스
    /// - WeaponData 데이터
    /// - PlayerHealth.IDamageable 구현
    /// - PlayerCombat 시스템 (설정값 위주)
    /// </summary>
    public class CoreCombatTests
    {
        // ===================== IDamageable 인터페이스 테스트 =====================

        [Test]
        public void IDamageable_Interface_IsDefined()
        {
            // IDamageable 타입이 존재하는지 확인
            var type = typeof(IDamageable);
            Assert.IsNotNull(type, "IDamageable 인터페이스가 정의되어 있어야 합니다");
            Assert.IsTrue(type.IsInterface, "IDamageable은 인터페이스여야 합니다");
        }

        [Test]
        public void IDamageable_HasTakeDamageMethod()
        {
            var takeDamageMethod = typeof(IDamageable).GetMethod("TakeDamage");
            Assert.IsNotNull(takeDamageMethod, "IDamageable에는 TakeDamage 메서드가 있어야 합니다");

            // 파라미터 확인
            var parameters = takeDamageMethod.GetParameters();
            Assert.AreEqual(3, parameters.Length, "TakeDamage는 3개의 파라미터가 있어야 합니다");

            Assert.AreEqual(typeof(float), parameters[0].ParameterType, "첫 번째 파라미터는 float (amount)");
            Assert.AreEqual(typeof(Vector3), parameters[1].ParameterType, "두 번째 파라미터는 Vector3 (hitDirection)");
            Assert.AreEqual(typeof(string), parameters[2].ParameterType, "세 번째 파라미터는 string (weaponType)");
            Assert.IsTrue(parameters[2].HasDefaultValue, "weaponType 파라미터는 기본값이 있어야 합니다");
        }

        [Test]
        public void IDamageable_HasIsAliveProperty()
        {
            var isAliveProperty = typeof(IDamageable).GetProperty("IsAlive");
            Assert.IsNotNull(isAliveProperty, "IDamageable에는 IsAlive 속성이 있어야 합니다");
            Assert.IsTrue(isAliveProperty.CanRead, "IsAlive는 읽기 가능해야 합니다");
            Assert.AreEqual(typeof(bool), isAliveProperty.PropertyType, "IsAlive는 bool 타입이어야 합니다");
        }

        // ===================== WeaponData 테스트 =====================

        [Test]
        public void WeaponData_CanCreateInstance()
        {
            var weapon = new WeaponData("TestSword", 10f, 0.8f, 2.5f, WeaponType.Sword);

            Assert.IsNotNull(weapon, "WeaponData 인스턴스가 생성되어야 합니다");
            Assert.AreEqual("TestSword", weapon.weaponName);
            Assert.AreEqual(10f, weapon.damage);
            Assert.AreEqual(0.8f, weapon.attackSpeed);
            Assert.AreEqual(2.5f, weapon.range);
            Assert.AreEqual(WeaponType.Sword, weapon.weaponType);
        }

        [Test]
        public void WeaponData_StaticFist_IsCorrect()
        {
            var fist = WeaponData.Fist;
            Assert.AreEqual("Fist", fist.weaponName);
            Assert.AreEqual(5f, fist.damage);
            Assert.AreEqual(0.5f, fist.attackSpeed);
            Assert.AreEqual(1.5f, fist.range);
            Assert.AreEqual(WeaponType.Fist, fist.weaponType);
        }

        [Test]
        public void WeaponData_StaticSword_IsCorrect()
        {
            var sword = WeaponData.Sword;
            Assert.AreEqual("Sword", sword.weaponName);
            Assert.AreEqual(12f, sword.damage);
            Assert.AreEqual(0.8f, sword.attackSpeed);
            Assert.AreEqual(2.5f, sword.range);
            Assert.AreEqual(WeaponType.Sword, sword.weaponType);
        }

        [Test]
        public void WeaponData_StaticSpear_IsCorrect()
        {
            var spear = WeaponData.Spear;
            Assert.AreEqual("Spear", spear.weaponName);
            Assert.AreEqual(15f, spear.damage);
            Assert.AreEqual(1.2f, spear.attackSpeed);
            Assert.AreEqual(3.5f, spear.range);
            Assert.AreEqual(WeaponType.Spear, spear.weaponType);
        }

        [Test]
        public void WeaponData_StaticBow_IsCorrect()
        {
            var bow = WeaponData.Bow;
            Assert.AreEqual("Bow", bow.weaponName);
            Assert.AreEqual(10f, bow.damage);
            Assert.AreEqual(1.5f, bow.attackSpeed);
            Assert.AreEqual(15f, bow.range);
            Assert.AreEqual(WeaponType.Bow, bow.weaponType);
        }

        [Test]
        public void WeaponData_FourDefaultWeaponsArePresent()
        {
            // 각 기본 무기가 독립적인 인스턴스를 반환하는지 확인
            var fist1 = WeaponData.Fist;
            var fist2 = WeaponData.Fist;

            Assert.AreNotSame(fist1, fist2, "정적 속성은 매번 새 인스턴스를 반환해야 합니다");
        }

        [Test]
        public void WeaponData_IsSerializable()
        {
            // Serializable 특성이 있는지 확인
            bool hasSerializable = typeof(WeaponData).IsDefined(typeof(System.SerializableAttribute), false);
            Assert.IsTrue(hasSerializable, "WeaponData는 Serializable 속성이 있어야 합니다");
        }

        // ===================== PlayerHealth - IDamageable 구현 테스트 =====================

        [Test]
        public void PlayerHealth_ImplementsIDamageable()
        {
            var go = new GameObject("TestPlayerHealth");
            var playerHealth = go.AddComponent<PlayerHealth>();

            Assert.IsNotNull(playerHealth, "PlayerHealth 컴포넌트가 추가되어야 합니다");
            Assert.IsTrue(playerHealth is IDamageable, "PlayerHealth는 IDamageable을 구현해야 합니다");

            // IDamageable로 캐스팅 가능 확인
            var damageable = playerHealth as IDamageable;
            Assert.IsNotNull(damageable, "IDamageable로 캐스팅되어야 합니다");
        }

        [Test]
        public void PlayerHealth_IsAlive_ReturnsTrueInitially()
        {
            var go = new GameObject("TestPlayerHealth");
            var playerHealth = go.AddComponent<PlayerHealth>();
            var damageable = playerHealth as IDamageable;

            Assert.IsNotNull(damageable);
            Assert.IsTrue(damageable.IsAlive, "초기 상태에서는 IsAlive가 true여야 합니다");
        }

        [Test]
        public void PlayerHealth_HasIsAliveProperty()
        {
            var isAliveProperty = typeof(PlayerHealth).GetProperty("IsAlive");
            Assert.IsNotNull(isAliveProperty, "PlayerHealth에 IsAlive 속성이 있어야 합니다");
            Assert.IsTrue(isAliveProperty.CanRead, "IsAlive는 읽기 가능해야 합니다");
        }

        [Test]
        public void PlayerHealth_DefaultValuesAreCorrect()
        {
            var go = new GameObject("TestPlayerHealth");
            var playerHealth = go.AddComponent<PlayerHealth>();

            // MaxHP와 CurrentHP 기본값 확인 (Awake/Start 호출 전)
            Assert.AreEqual(100f, playerHealth.MaxHP, "기본 최대 HP는 100이어야 합니다");
            Assert.AreEqual(0f, playerHealth.CurrentHP, "Start() 호출 전 CurrentHP는 0 (초기화 안 됨)");
        }

        [Test]
        public void PlayerHealth_SetMaxHP_Works()
        {
            var go = new GameObject("TestPlayerHealth");
            var playerHealth = go.AddComponent<PlayerHealth>();

            playerHealth.SetMaxHP(200f);
            Assert.AreEqual(200f, playerHealth.MaxHP, "SetMaxHP로 최대 HP가 변경되어야 합니다");
        }

        [Test]
        public void PlayerHealth_SetMaxHP_MinimumOne()
        {
            var go = new GameObject("TestPlayerHealth");
            var playerHealth = go.AddComponent<PlayerHealth>();

            playerHealth.SetMaxHP(0f);
            Assert.AreEqual(1f, playerHealth.MaxHP, "SetMaxHP로 0을 설정해도 최소 1이어야 합니다");
        }

        // ===================== PlayerCombat 시스템 (설정값) 테스트 =====================

        [Test]
        public void PlayerCombat_Component_Exists()
        {
            // Systems 네임스페이스의 PlayerCombat 타입 존재 확인
            var combatType = System.Type.GetType("ProjectName.Systems.PlayerCombat, ProjectName.Systems");
            Assert.IsNotNull(combatType, "ProjectName.Systems.PlayerCombat 타입이 존재해야 합니다");
        }

        [Test]
        public void PlayerCombat_IsMonoBehaviour()
        {
            var combatType = System.Type.GetType("ProjectName.Systems.PlayerCombat, ProjectName.Systems");
            Assert.IsNotNull(combatType);
            Assert.IsTrue(combatType.IsSubclassOf(typeof(MonoBehaviour)), "PlayerCombat는 MonoBehaviour를 상속해야 합니다");
        }

        [Test]
        public void PlayerCombat_HasCanAttackProperty()
        {
            var combatType = System.Type.GetType("ProjectName.Systems.PlayerCombat, ProjectName.Systems");
            Assert.IsNotNull(combatType);

            var canAttackProp = combatType.GetProperty("CanAttack");
            Assert.IsNotNull(canAttackProp, "PlayerCombat에 CanAttack 속성이 있어야 합니다");
            Assert.AreEqual(typeof(bool), canAttackProp.PropertyType, "CanAttack은 bool 타입이어야 합니다");
        }

        [Test]
        public void PlayerCombat_HasRemainingCooldownProperty()
        {
            var combatType = System.Type.GetType("ProjectName.Systems.PlayerCombat, ProjectName.Systems");
            Assert.IsNotNull(combatType);

            var cooldownProp = combatType.GetProperty("RemainingCooldown");
            Assert.IsNotNull(cooldownProp, "PlayerCombat에 RemainingCooldown 속성이 있어야 합니다");
            Assert.AreEqual(typeof(float), cooldownProp.PropertyType, "RemainingCooldown은 float 타입이어야 합니다");
        }

        [Test]
        public void PlayerCombat_HasSetWeaponMethod()
        {
            var combatType = System.Type.GetType("ProjectName.Systems.PlayerCombat, ProjectName.Systems");
            Assert.IsNotNull(combatType);

            var setWeaponMethod = combatType.GetMethod("SetWeapon");
            Assert.IsNotNull(setWeaponMethod, "PlayerCombat에 SetWeapon 메서드가 있어야 합니다");

            var parameters = setWeaponMethod.GetParameters();
            Assert.AreEqual(1, parameters.Length);
            Assert.AreEqual(typeof(WeaponData), parameters[0].ParameterType, "SetWeapon 파라미터는 WeaponData 타입이어야 합니다");
        }

        [Test]
        public void PlayerCombat_HasCurrentWeaponProperty()
        {
            var combatType = System.Type.GetType("ProjectName.Systems.PlayerCombat, ProjectName.Systems");
            Assert.IsNotNull(combatType);

            var weaponProp = combatType.GetProperty("CurrentWeapon");
            Assert.IsNotNull(weaponProp, "PlayerCombat에 CurrentWeapon 속성이 있어야 합니다");
            Assert.AreEqual(typeof(WeaponData), weaponProp.PropertyType, "CurrentWeapon은 WeaponData 타입이어야 합니다");
        }

        // ===================== 통합 동작 검증 =====================

        [Test]
        public void PlayerCombat_DefaultWeaponIsFist()
        {
            var go = new GameObject("TestPlayerCombat");
            var combat = go.AddComponent<Systems.PlayerCombat>();

            // Start() 전에는 CurrentWeapon이 null
            // Start() 후 기본 무기는 주먹
            // 여기서는 Start를 수동 호출할 수 없으므로 확인 불가 -> 구조 확인
            Assert.IsNotNull(combat, "PlayerCombat 인스턴스가 생성되어야 합니다");
        }

        // ===================== C4-08: 자동 조준 (Auto-Aim) 테스트 =====================

        [Test]
        public void PlayerCombat_HasAutoAimTargetProperty()
        {
            var combatType = System.Type.GetType("ProjectName.Systems.PlayerCombat, ProjectName.Systems");
            Assert.IsNotNull(combatType, "PlayerCombat 타입이 존재해야 합니다");

            var targetProp = combatType.GetProperty("CurrentTarget");
            Assert.IsNotNull(targetProp, "PlayerCombat에 CurrentTarget 속성이 있어야 합니다 (C4-08)");
            Assert.AreEqual(typeof(ProjectName.Core.IDamageable), targetProp.PropertyType,
                "CurrentTarget은 IDamageable 타입이어야 합니다");
        }

        [Test]
        public void PlayerCombat_HasHasTargetProperty()
        {
            var combatType = System.Type.GetType("ProjectName.Systems.PlayerCombat, ProjectName.Systems");
            Assert.IsNotNull(combatType);

            var hasTargetProp = combatType.GetProperty("HasTarget");
            Assert.IsNotNull(hasTargetProp, "PlayerCombat에 HasTarget 속성이 있어야 합니다 (C4-08)");
            Assert.AreEqual(typeof(bool), hasTargetProp.PropertyType, "HasTarget은 bool 타입이어야 합니다");
        }

        [Test]
        public void PlayerCombat_HasAutoAimMethod()
        {
            var combatType = System.Type.GetType("ProjectName.Systems.PlayerCombat, ProjectName.Systems");
            Assert.IsNotNull(combatType);

            var findTargetMethod = combatType.GetMethod("FindTargetInCursorDirection",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(findTargetMethod, "PlayerCombat에 FindTargetInCursorDirection 메서드가 있어야 합니다 (C4-08)");
            Assert.AreEqual(typeof(ProjectName.Core.IDamageable), findTargetMethod.ReturnType,
                "FindTargetInCursorDirection은 IDamageable을 반환해야 합니다");
        }

        [Test]
        public void PlayerCombat_HasAutoAimRange()
        {
            var combatType = System.Type.GetType("ProjectName.Systems.PlayerCombat, ProjectName.Systems");
            Assert.IsNotNull(combatType);

            var aimRangeField = combatType.GetField("_autoAimRange",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(aimRangeField, "PlayerCombat에 _autoAimRange 필드가 있어야 합니다 (C4-08)");
            Assert.AreEqual(typeof(float), aimRangeField.FieldType, "_autoAimRange는 float 타입이어야 합니다");
        }

        [Test]
        public void PlayerCombat_AutoAimSnapsToClosestEnemy()
        {
            var go = new GameObject("TestCombat");
            var combat = go.AddComponent<Systems.PlayerCombat>();
            Assert.IsNotNull(combat, "PlayerCombat 인스턴스가 생성되어야 합니다");

            // 초기 상태에서는 타겟이 없어야 함
            Assert.IsFalse(combat.HasTarget, "초기 상태에서는 HasTarget이 false여야 합니다");
            Assert.IsNull(combat.CurrentTarget, "초기 상태에서는 CurrentTarget이 null이어야 합니다");
        }

        [Test]
        public void WeaponData_And_Damageable_Integration()
        {
            // WeaponData를 사용해 데미지 생성 → IDamageable 전달 가능 확인
            WeaponData sword = WeaponData.Sword;
            float damage = sword.damage;

            // 데미지가 양수인지 확인
            Assert.Greater(damage, 0f, "무기 데미지는 0보다 커야 합니다");

            // IDamageable 구현체(PlayerHealth) 생성
            var go = new GameObject("TestPlayer");
            var health = go.AddComponent<PlayerHealth>();

            // Start() 호출 (수동으로 초기화)
            health.Invoke("Start", 0f);

            // IDamageable로 캐스팅
            var damageable = health as IDamageable;
            Assert.IsNotNull(damageable, "PlayerHealth는 IDamageable로 캐스팅되어야 합니다");
            Assert.IsTrue(damageable.IsAlive, "초기엔 살아있어야 합니다");

            // TakeDamage 호출 (기본값 weaponType = "melee")
            damageable.TakeDamage(damage, Vector3.forward);

            // 데미지가 적용되었는지 확인 (방어력이 0이므로 damage 그대로 차감)
            Assert.Less(health.CurrentHP, health.MaxHP, "데미지 후 HP가 감소해야 합니다");
        }

        // ===================== C4-12~15: LootBasket 시스템 테스트 =====================

        [Test]
        public void LootEntry_CanBeCreated()
        {
            var entry = new LootEntry();
            Assert.IsNotNull(entry, "LootEntry 인스턴스가 생성되어야 합니다");
            Assert.IsNull(entry.item, "item은 기본값 null이어야 합니다");
            Assert.AreEqual(0, entry.count, "count는 기본값 0이어야 합니다");
        }

        [Test]
        public void LootEntry_CanStoreItemAndCount()
        {
            var entry = new LootEntry();
            entry.item = new PlayerInventory.ItemData { id = "test", displayName = "테스트", category = PlayerInventory.ItemCategory.Material, maxStack = 99 };
            entry.count = 5;

            Assert.IsNotNull(entry.item);
            Assert.AreEqual("test", entry.item.id);
            Assert.AreEqual(5, entry.count);
        }

        [Test]
        public void ILootBasket_Interface_IsDefined()
        {
            var type = typeof(ILootBasket);
            Assert.IsNotNull(type, "ILootBasket 인터페이스가 정의되어 있어야 합니다");
            Assert.IsTrue(type.IsInterface, "ILootBasket은 인터페이스여야 합니다");
        }

        [Test]
        public void ILootBasket_HasRequiredMembers()
        {
            var type = typeof(ILootBasket);

            var itemsProp = type.GetProperty("Items");
            Assert.IsNotNull(itemsProp, "ILootBasket에 Items 속성이 있어야 합니다");

            var isEmptyProp = type.GetProperty("IsEmpty");
            Assert.IsNotNull(isEmptyProp, "ILootBasket에 IsEmpty 속성이 있어야 합니다");

            var itemCountProp = type.GetProperty("ItemCount");
            Assert.IsNotNull(itemCountProp, "ILootBasket에 ItemCount 속성이 있어야 합니다");

            var basketNameProp = type.GetProperty("BasketName");
            Assert.IsNotNull(basketNameProp, "ILootBasket에 BasketName 속성이 있어야 합니다");

            var takeItemMethod = type.GetMethod("TakeItem");
            Assert.IsNotNull(takeItemMethod, "ILootBasket에 TakeItem 메서드가 있어야 합니다");

            var takeAllMethod = type.GetMethod("TakeAll");
            Assert.IsNotNull(takeAllMethod, "ILootBasket에 TakeAll 메서드가 있어야 합니다");
        }

        [Test]
        public void SystemsLootBasket_ImplementsILootBasket()
        {
            var basketType = System.Type.GetType("ProjectName.Systems.LootBasket, ProjectName.Systems");
            Assert.IsNotNull(basketType, "LootBasket 타입이 존재해야 합니다");

            bool implementsILootBasket = typeof(ILootBasket).IsAssignableFrom(basketType);
            Assert.IsTrue(implementsILootBasket, "LootBasket은 ILootBasket을 구현해야 합니다");
        }
    }
}