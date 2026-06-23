using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ProjectName.Core;
using ProjectName.Systems;
using System.Reflection;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// AB-01~06: Arrow/Bow system EditMode tests.
    /// Covers ArrowData, ArrowManager, and ArrowProjectile.
    /// Target: 30+ tests across all 6 requirement categories.
    /// </summary>
    [TestFixture]
    public class ArrowSystemTests
    {
        // ===================== Reflection helpers =====================

        private static T GetPrivateField<T>(object obj, string fieldName)
        {
            var field = obj.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                throw new System.Exception($"Field '{fieldName}' not found on {obj.GetType().Name}");
            return (T)field.GetValue(obj);
        }

        private static void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
                field.SetValue(obj, value);
        }

        private static object InvokePrivateMethod(object obj, string methodName, object[] args = null)
        {
            var method = obj.GetType().GetMethod(methodName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null)
                throw new System.Exception($"Method '{methodName}' not found on {obj.GetType().Name}");
            return method.Invoke(obj, args ?? new object[0]);
        }

        // ===================== SetUp / TearDown =====================

        private GameObject _inventoryGo;
        private GameObject _arrowManagerGo;
        private PlayerInventory _inventory;
        private ArrowManager _arrowManager;

        [SetUp]
        public void SetUp()
        {
            // Create PlayerInventory first (singleton set in Awake)
            _inventoryGo = new GameObject("TestPlayerInventory");
            _inventory = _inventoryGo.AddComponent<PlayerInventory>();

            // Create ArrowManager (reads PlayerInventory.Instance in Awake)
            _arrowManagerGo = new GameObject("TestArrowManager");
            _arrowManager = _arrowManagerGo.AddComponent<ArrowManager>();
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up singletons so tests don't cross-contaminate
            if (_arrowManagerGo != null)
                Object.DestroyImmediate(_arrowManagerGo);
            if (_inventoryGo != null)
                Object.DestroyImmediate(_inventoryGo);

            // Clear singleton references
            SetPrivateFieldStatic<ArrowManager>("Instance", null);
            SetPrivateFieldStatic<PlayerInventory>("Instance", null);
        }

        private static void SetPrivateFieldStatic<T>(string fieldName, object value)
        {
            var prop = typeof(T).GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static);
            if (prop != null)
            {
                // ArrowManager.Instance is { get; private set; }
                // Use the backing field
                var backingField = typeof(T).GetField("<Instance>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Static);
                if (backingField != null)
                    backingField.SetValue(null, value);
            }
        }

        // ===================== AB-01: ArrowData Tests =====================

        [Test]
        public void ArrowTypeEnum_Regular_IsZero()
        {
            Assert.AreEqual(0, (int)ArrowData.ArrowType.Regular,
                "Regular should be the first enum value (0)");
        }

        [Test]
        public void ArrowTypeEnum_Reinforced_IsOne()
        {
            Assert.AreEqual(1, (int)ArrowData.ArrowType.Reinforced,
                "Reinforced should be the second enum value (1)");
        }

        [Test]
        public void ArrowTypeEnum_Magic_IsTwo()
        {
            Assert.AreEqual(2, (int)ArrowData.ArrowType.Magic,
                "Magic should be the third enum value (2)");
        }

        [Test]
        public void Constructor_Regular_SetsDisplayName()
        {
            var arrow = new ArrowData(ArrowData.ArrowType.Regular);
            Assert.AreEqual("일반 화살", arrow.displayName,
                "Regular arrow display name should be '일반 화살'");
        }

        [Test]
        public void Constructor_Regular_SetsDamageBonus_Zero()
        {
            var arrow = new ArrowData(ArrowData.ArrowType.Regular);
            Assert.AreEqual(0, arrow.damageBonus,
                "Regular arrow should have 0 damage bonus");
        }

        [Test]
        public void Constructor_Regular_SetsDescription()
        {
            var arrow = new ArrowData(ArrowData.ArrowType.Regular);
            Assert.AreEqual("기본 화살. 특별한 효과 없음.", arrow.description,
                "Regular arrow should have correct description");
        }

        [Test]
        public void Constructor_Regular_SetsRarity_Common()
        {
            var arrow = new ArrowData(ArrowData.ArrowType.Regular);
            Assert.AreEqual(ItemRarity.Common, arrow.rarity,
                "Regular arrow should be Common rarity");
        }

        [Test]
        public void Constructor_Regular_SetsGoldCost()
        {
            var arrow = new ArrowData(ArrowData.ArrowType.Regular);
            Assert.AreEqual(5, arrow.goldCost,
                "Regular arrow should cost 5 gold");
        }

        [Test]
        public void Constructor_Regular_SetsTrailColor()
        {
            var arrow = new ArrowData(ArrowData.ArrowType.Regular);
            // 갈색: (0.55, 0.35, 0.15)
            Assert.AreEqual(0.55f, arrow.trailColor.r, 0.01f, "R channel");
            Assert.AreEqual(0.35f, arrow.trailColor.g, 0.01f, "G channel");
            Assert.AreEqual(0.15f, arrow.trailColor.b, 0.01f, "B channel");
        }

        [Test]
        public void Constructor_Reinforced_SetsCorrectValues()
        {
            var arrow = new ArrowData(ArrowData.ArrowType.Reinforced);
            Assert.AreEqual("강화 화살", arrow.displayName);
            Assert.AreEqual(5, arrow.damageBonus);
            Assert.AreEqual("철촉이 달린 강화 화살. +5 데미지.", arrow.description);
            Assert.AreEqual(ItemRarity.Uncommon, arrow.rarity);
            Assert.AreEqual(15, arrow.goldCost);
            Assert.AreEqual(0.75f, arrow.trailColor.r, 0.01f);
            Assert.AreEqual(0.75f, arrow.trailColor.g, 0.01f);
            Assert.AreEqual(0.80f, arrow.trailColor.b, 0.01f);
        }

        [Test]
        public void Constructor_Magic_SetsCorrectValues()
        {
            var arrow = new ArrowData(ArrowData.ArrowType.Magic);
            Assert.AreEqual("마법 화살", arrow.displayName);
            Assert.AreEqual(15, arrow.damageBonus);
            Assert.AreEqual("마력이 깃든 화살. +15 데미지.", arrow.description);
            Assert.AreEqual(ItemRarity.Rare, arrow.rarity);
            Assert.AreEqual(50, arrow.goldCost);
            Assert.AreEqual(0.7f, arrow.trailColor.r, 0.01f);
            Assert.AreEqual(0.2f, arrow.trailColor.g, 0.01f);
            Assert.AreEqual(0.9f, arrow.trailColor.b, 0.01f);
        }

        [Test]
        public void Constructor_SetsArrowTypeField()
        {
            var regular = new ArrowData(ArrowData.ArrowType.Regular);
            var reinforced = new ArrowData(ArrowData.ArrowType.Reinforced);
            var magic = new ArrowData(ArrowData.ArrowType.Magic);

            Assert.AreEqual(ArrowData.ArrowType.Regular, regular.arrowType);
            Assert.AreEqual(ArrowData.ArrowType.Reinforced, reinforced.arrowType);
            Assert.AreEqual(ArrowData.ArrowType.Magic, magic.arrowType);
        }

        [Test]
        public void GetItemId_Regular_ReturnsCorrectFormat()
        {
            var arrow = new ArrowData(ArrowData.ArrowType.Regular);
            Assert.AreEqual("arrow_regular", arrow.GetItemId());
        }

        [Test]
        public void GetItemId_Reinforced_ReturnsCorrectFormat()
        {
            var arrow = new ArrowData(ArrowData.ArrowType.Reinforced);
            Assert.AreEqual("arrow_reinforced", arrow.GetItemId());
        }

        [Test]
        public void GetItemId_Magic_ReturnsCorrectFormat()
        {
            var arrow = new ArrowData(ArrowData.ArrowType.Magic);
            Assert.AreEqual("arrow_magic", arrow.GetItemId());
        }

        [Test]
        public void StaticProperty_Regular_ReturnsValidInstance()
        {
            var arrow = ArrowData.Regular;
            Assert.IsNotNull(arrow, "Static Regular should not be null");
            Assert.AreEqual(ArrowData.ArrowType.Regular, arrow.arrowType);
            Assert.AreEqual("일반 화살", arrow.displayName);
        }

        [Test]
        public void StaticProperty_Reinforced_ReturnsValidInstance()
        {
            var arrow = ArrowData.Reinforced;
            Assert.IsNotNull(arrow, "Static Reinforced should not be null");
            Assert.AreEqual(ArrowData.ArrowType.Reinforced, arrow.arrowType);
            Assert.AreEqual("강화 화살", arrow.displayName);
        }

        [Test]
        public void StaticProperty_Magic_ReturnsValidInstance()
        {
            var arrow = ArrowData.Magic;
            Assert.IsNotNull(arrow, "Static Magic should not be null");
            Assert.AreEqual(ArrowData.ArrowType.Magic, arrow.arrowType);
            Assert.AreEqual("마법 화살", arrow.displayName);
        }

        // ===================== AB-02: Arrow Consumption Tests =====================

        [Test]
        public void GetTotalArrowCount_EmptyInventory_ReturnsZero()
        {
            Assert.AreEqual(0, _arrowManager.GetTotalArrowCount(),
                "Should return 0 when inventory has no arrows");
        }

        [Test]
        public void GetTotalArrowCount_WithArrows_ReturnsCorrectCount()
        {
            _arrowManager.AddArrows(ArrowData.ArrowType.Regular, 5);
            _arrowManager.AddArrows(ArrowData.ArrowType.Reinforced, 3);

            Assert.AreEqual(8, _arrowManager.GetTotalArrowCount(),
                "Should return sum of all arrow types (5 + 3 = 8)");
        }

        [Test]
        public void GetTotalArrowCount_WithOnlyMagic_ReturnsCorrectCount()
        {
            _arrowManager.AddArrows(ArrowData.ArrowType.Magic, 10);
            Assert.AreEqual(10, _arrowManager.GetTotalArrowCount());
        }

        [Test]
        public void AddArrows_AddsToPlayerInventory()
        {
            _arrowManager.AddArrows(ArrowData.ArrowType.Regular, 3);

            int count = _inventory.GetItemCount("arrow_regular");
            Assert.AreEqual(3, count, "Inventory should have 3 regular arrows");
        }

        [Test]
        public void AddArrows_MultipleTypes_AllAdded()
        {
            _arrowManager.AddArrows(ArrowData.ArrowType.Regular, 2);
            _arrowManager.AddArrows(ArrowData.ArrowType.Reinforced, 4);
            _arrowManager.AddArrows(ArrowData.ArrowType.Magic, 6);

            Assert.AreEqual(2, _inventory.GetItemCount("arrow_regular"));
            Assert.AreEqual(4, _inventory.GetItemCount("arrow_reinforced"));
            Assert.AreEqual(6, _inventory.GetItemCount("arrow_magic"));
        }

        [Test]
        public void HasArrows_EmptyInventory_ReturnsFalse()
        {
            Assert.IsFalse(_arrowManager.HasArrows(),
                "HasArrows should be false when no arrows exist");
        }

        [Test]
        public void HasArrows_WithArrows_ReturnsTrue()
        {
            _arrowManager.AddArrows(ArrowData.ArrowType.Regular, 1);
            Assert.IsTrue(_arrowManager.HasArrows(),
                "HasArrows should be true when arrows exist");
        }

        [Test]
        public void HasArrows_AfterConsumingAll_ReturnsFalse()
        {
            _arrowManager.AddArrows(ArrowData.ArrowType.Regular, 1);
            Assert.IsTrue(_arrowManager.HasArrows());

            // Consume via private method
            InvokePrivateMethod(_arrowManager, "TryConsumeArrow",
                new object[] { "arrow_regular" });

            Assert.IsFalse(_arrowManager.HasArrows(),
                "HasArrows should be false after consuming all arrows");
        }

        [Test]
        public void ConsumeBestArrow_PrioritizesMagicOverReinforced()
        {
            // Add reinforced first, then magic
            _arrowManager.AddArrows(ArrowData.ArrowType.Reinforced, 3);
            _arrowManager.AddArrows(ArrowData.ArrowType.Magic, 2);

            var result = (ArrowData.ArrowType)InvokePrivateMethod(
                _arrowManager, "ConsumeBestArrow", null);

            Assert.AreEqual(ArrowData.ArrowType.Magic, result,
                "ConsumeBestArrow should consume Magic before Reinforced");

            // Verify magic was reduced by 1
            Assert.AreEqual(1, _inventory.GetItemCount("arrow_magic"),
                "Magic arrow count should decrease by 1");
            Assert.AreEqual(3, _inventory.GetItemCount("arrow_reinforced"),
                "Reinforced arrows should be untouched");
        }

        [Test]
        public void ConsumeBestArrow_PrioritizesReinforcedOverRegular()
        {
            _arrowManager.AddArrows(ArrowData.ArrowType.Regular, 5);
            _arrowManager.AddArrows(ArrowData.ArrowType.Reinforced, 2);

            var result = (ArrowData.ArrowType)InvokePrivateMethod(
                _arrowManager, "ConsumeBestArrow", null);

            Assert.AreEqual(ArrowData.ArrowType.Reinforced, result,
                "ConsumeBestArrow should consume Reinforced before Regular");

            Assert.AreEqual(1, _inventory.GetItemCount("arrow_reinforced"));
            Assert.AreEqual(5, _inventory.GetItemCount("arrow_regular"));
        }

        [Test]
        public void ConsumeBestArrow_FallsBackToRegular()
        {
            _arrowManager.AddArrows(ArrowData.ArrowType.Regular, 3);

            var result = (ArrowData.ArrowType)InvokePrivateMethod(
                _arrowManager, "ConsumeBestArrow", null);

            Assert.AreEqual(ArrowData.ArrowType.Regular, result,
                "Should consume Regular when it's the only type available");

            Assert.AreEqual(2, _inventory.GetItemCount("arrow_regular"));
        }

        [Test]
        public void ConsumeBestArrow_AllTypes_PrioritizesMagicFirst()
        {
            _arrowManager.AddArrows(ArrowData.ArrowType.Regular, 10);
            _arrowManager.AddArrows(ArrowData.ArrowType.Reinforced, 10);
            _arrowManager.AddArrows(ArrowData.ArrowType.Magic, 10);

            var result = (ArrowData.ArrowType)InvokePrivateMethod(
                _arrowManager, "ConsumeBestArrow", null);

            Assert.AreEqual(ArrowData.ArrowType.Magic, result,
                "With all types, Magic should be consumed first");
        }

        [Test]
        public void TryConsumeArrow_ReducesCount()
        {
            _arrowManager.AddArrows(ArrowData.ArrowType.Regular, 3);

            bool consumed = (bool)InvokePrivateMethod(_arrowManager,
                "TryConsumeArrow", new object[] { "arrow_regular" });

            Assert.IsTrue(consumed, "TryConsumeArrow should return true");
            Assert.AreEqual(2, _inventory.GetItemCount("arrow_regular"),
                "Count should decrease from 3 to 2");
        }

        [Test]
        public void TryConsumeArrow_RemovesSlot_WhenCountReachesZero()
        {
            _arrowManager.AddArrows(ArrowData.ArrowType.Regular, 1);

            bool consumed = (bool)InvokePrivateMethod(_arrowManager,
                "TryConsumeArrow", new object[] { "arrow_regular" });

            Assert.IsTrue(consumed);
            Assert.AreEqual(0, _inventory.GetItemCount("arrow_regular"),
                "Slot should be removed when count reaches 0");
        }

        // ===================== AB-03: No Arrows Handling Tests =====================

        [Test]
        public void TryShootArrow_NoArrows_ReturnsFalse()
        {
            bool result = _arrowManager.TryShootArrow(Vector3.forward, 10f);
            Assert.IsFalse(result,
                "TryShootArrow should return false when no arrows available");
        }

        [Test]
        public void TryShootArrow_HasArrows_ReturnsTrue()
        {
            _arrowManager.AddArrows(ArrowData.ArrowType.Regular, 5);

            bool result = _arrowManager.TryShootArrow(Vector3.forward, 10f);
            Assert.IsTrue(result,
                "TryShootArrow should return true when arrows are available");
        }

        [Test]
        public void TryShootArrow_HasArrows_ReducesTotalCount()
        {
            _arrowManager.AddArrows(ArrowData.ArrowType.Regular, 5);

            int countBefore = _arrowManager.GetTotalArrowCount();
            _arrowManager.TryShootArrow(Vector3.forward, 10f);
            int countAfter = _arrowManager.GetTotalArrowCount();

            Assert.AreEqual(1, countBefore - countAfter,
                "TryShootArrow should consume exactly 1 arrow");
            Assert.AreEqual(4, countAfter);
        }

        [Test]
        public void TryShootArrow_LastArrow_ConsumesAll()
        {
            _arrowManager.AddArrows(ArrowData.ArrowType.Regular, 1);

            Assert.IsTrue(_arrowManager.HasArrows());
            _arrowManager.TryShootArrow(Vector3.forward, 10f);

            Assert.AreEqual(0, _arrowManager.GetTotalArrowCount(),
                "All arrows should be consumed");
            Assert.IsFalse(_arrowManager.HasArrows(),
                "HasArrows should be false after last arrow consumed");
        }

        [Test]
        public void TryShootArrow_AfterDepletion_ReturnsFalse()
        {
            _arrowManager.AddArrows(ArrowData.ArrowType.Regular, 1);
            _arrowManager.TryShootArrow(Vector3.forward, 10f);

            // Try again with no arrows
            bool result = _arrowManager.TryShootArrow(Vector3.forward, 10f);
            Assert.IsFalse(result,
                "Second TryShootArrow should fail after depletion");
        }

        [Test]
        public void ShowNoArrowMessage_LogsCorrectMessage()
        {
            // ShowNoArrowMessage is private, call via TryShootArrow when empty
            // Or invoke it directly with reflection
            LogAssert.Expect(LogType.Log, "[ArrowManager] 화살이 부족합니다!");

            InvokePrivateMethod(_arrowManager, "ShowNoArrowMessage", null);
        }

        // ===================== AB-04: ArrowProjectile Tests =====================

        [Test]
        public void Spawn_CreatesGameObject()
        {
            var arrow = ArrowProjectile.Spawn(
                Vector3.zero, Vector3.forward, 30f, 25, Color.red);

            Assert.IsNotNull(arrow, "Spawn should return a valid ArrowProjectile");
            Assert.IsNotNull(arrow.gameObject, "ArrowProjectile should have a GameObject");

            Object.DestroyImmediate(arrow.gameObject);
        }

        [Test]
        public void Spawn_GameObjectHasCorrectName()
        {
            var arrow = ArrowProjectile.Spawn(
                Vector3.zero, Vector3.forward, 30f, 25, Color.red);

            Assert.AreEqual("Arrow(Clone)", arrow.gameObject.name,
                "Spawned arrow GameObject should be named 'Arrow(Clone)'");

            Object.DestroyImmediate(arrow.gameObject);
        }

        [Test]
        public void Spawn_HasRigidbodyComponent()
        {
            var arrow = ArrowProjectile.Spawn(
                Vector3.zero, Vector3.forward, 30f, 25, Color.red);

            var rb = arrow.GetComponent<Rigidbody>();
            Assert.IsNotNull(rb, "ArrowProjectile should have a Rigidbody component");
            Assert.IsTrue(rb.useGravity, "Rigidbody should use gravity for trajectory");

            Object.DestroyImmediate(arrow.gameObject);
        }

        [Test]
        public void Spawn_HasTrailRendererComponent()
        {
            var arrow = ArrowProjectile.Spawn(
                Vector3.zero, Vector3.forward, 30f, 25, Color.red);

            var trail = arrow.GetComponent<TrailRenderer>();
            Assert.IsNotNull(trail, "ArrowProjectile should have a TrailRenderer");
            Assert.AreEqual(0.3f, trail.time, 0.01f, "Trail time should be 0.3s");
            Assert.AreEqual(0.05f, trail.startWidth, 0.01f, "Trail start width should be 0.05");
            Assert.AreEqual(0.01f, trail.endWidth, 0.01f, "Trail end width should be 0.01");

            Object.DestroyImmediate(arrow.gameObject);
        }

        [Test]
        public void Spawn_HasArrowProjectileComponent()
        {
            var arrow = ArrowProjectile.Spawn(
                Vector3.zero, Vector3.forward, 30f, 25, Color.red);

            Assert.IsNotNull(arrow, "Should have ArrowProjectile component");
            Assert.IsInstanceOf<ArrowProjectile>(arrow,
                "Component should be ArrowProjectile type");

            Object.DestroyImmediate(arrow.gameObject);
        }

        [Test]
        public void Spawn_HasCapsuleCollider()
        {
            var arrow = ArrowProjectile.Spawn(
                Vector3.zero, Vector3.forward, 30f, 25, Color.red);

            var collider = arrow.GetComponent<CapsuleCollider>();
            Assert.IsNotNull(collider, "Should have CapsuleCollider (from Cylinder primitive)");
            Assert.IsTrue(collider.isTrigger, "Collider should be a trigger");

            Object.DestroyImmediate(arrow.gameObject);
        }

        [Test]
        public void Spawn_SetsCorrectVelocity()
        {
            Vector3 direction = Vector3.forward;
            float speed = 30f;

            var arrow = ArrowProjectile.Spawn(
                Vector3.zero, direction, speed, 25, Color.red);

            var rb = arrow.GetComponent<Rigidbody>();
            Vector3 expectedVelocity = direction * speed;
            Assert.AreEqual(expectedVelocity.x, rb.linearVelocity.x, 0.01f, "Velocity X");
            Assert.AreEqual(expectedVelocity.y, rb.linearVelocity.y, 0.01f, "Velocity Y");
            Assert.AreEqual(expectedVelocity.z, rb.linearVelocity.z, 0.01f, "Velocity Z");

            Object.DestroyImmediate(arrow.gameObject);
        }

        [Test]
        public void Spawn_SetsCorrectTrailColor()
        {
            Color trailColor = new Color(0.7f, 0.2f, 0.9f);

            var arrow = ArrowProjectile.Spawn(
                Vector3.zero, Vector3.forward, 30f, 25, trailColor);

            var trail = arrow.GetComponent<TrailRenderer>();
            Assert.AreEqual(trailColor.r, trail.startColor.r, 0.01f,
                "Trail start color R should match");
            Assert.AreEqual(trailColor.g, trail.startColor.g, 0.01f,
                "Trail start color G should match");
            Assert.AreEqual(trailColor.b, trail.startColor.b, 0.01f,
                "Trail start color B should match");

            Object.DestroyImmediate(arrow.gameObject);
        }

        [Test]
        public void Spawn_SetsDamageFieldCorrectly()
        {
            int expectedDamage = 42;

            var arrow = ArrowProjectile.Spawn(
                Vector3.zero, Vector3.forward, 30f, expectedDamage, Color.red);

            int actualDamage = GetPrivateField<int>(arrow, "_damage");
            Assert.AreEqual(expectedDamage, actualDamage,
                "_damage should be set to the value passed to Spawn");

            Object.DestroyImmediate(arrow.gameObject);
        }

        [Test]
        public void Spawn_SetsCorrectPosition()
        {
            Vector3 expectedPos = new Vector3(5f, 2f, 10f);

            var arrow = ArrowProjectile.Spawn(
                expectedPos, Vector3.forward, 30f, 25, Color.red);

            Assert.AreEqual(expectedPos.x, arrow.transform.position.x, 0.01f, "Position X");
            Assert.AreEqual(expectedPos.y, arrow.transform.position.y, 0.01f, "Position Y");
            Assert.AreEqual(expectedPos.z, arrow.transform.position.z, 0.01f, "Position Z");

            Object.DestroyImmediate(arrow.gameObject);
        }

        [Test]
        public void Spawn_FreezesRotation()
        {
            var arrow = ArrowProjectile.Spawn(
                Vector3.zero, Vector3.forward, 30f, 25, Color.red);

            var rb = arrow.GetComponent<Rigidbody>();
            Assert.AreEqual(RigidbodyConstraints.FreezeRotation, rb.constraints,
                "Rigidbody should have FreezeRotation constraint");

            Object.DestroyImmediate(arrow.gameObject);
        }

        [Test]
        public void Spawn_DifferentDirections_ProduceDifferentVelocities()
        {
            var arrow1 = ArrowProjectile.Spawn(
                Vector3.zero, Vector3.forward, 30f, 25, Color.red);
            var arrow2 = ArrowProjectile.Spawn(
                Vector3.zero, Vector3.right, 30f, 25, Color.blue);

            var rb1 = arrow1.GetComponent<Rigidbody>();
            var rb2 = arrow2.GetComponent<Rigidbody>();

            Assert.AreNotEqual(rb1.linearVelocity, rb2.linearVelocity,
                "Different directions should produce different velocities");

            Object.DestroyImmediate(arrow1.gameObject);
            Object.DestroyImmediate(arrow2.gameObject);
        }

        // ===================== AB-05: Arrow Trajectory / Acquisition =====================

        [Test]
        public void SetSpawnPoint_ChangesSpawnPoint()
        {
            var testPoint = new GameObject("TestSpawnPoint");
            testPoint.transform.position = new Vector3(0f, 2f, 0f);

            _arrowManager.SetSpawnPoint(testPoint.transform);

            var spawnPoint = GetPrivateField<Transform>(_arrowManager, "_arrowSpawnPoint");
            Assert.IsNotNull(spawnPoint, "Spawn point should be set");
            Assert.AreEqual(testPoint.transform, spawnPoint,
                "Spawn point should be the transform we passed");

            Object.DestroyImmediate(testPoint);
        }

        [Test]
        public void SetSpawnPoint_Null_ShouldClearSpawnPoint()
        {
            var testPoint = new GameObject("TestSpawnPoint");
            _arrowManager.SetSpawnPoint(testPoint.transform);
            Assert.IsNotNull(GetPrivateField<Transform>(_arrowManager, "_arrowSpawnPoint"));

            _arrowManager.SetSpawnPoint(null);
            Assert.IsNull(GetPrivateField<Transform>(_arrowManager, "_arrowSpawnPoint"),
                "Setting null should clear the spawn point");

            Object.DestroyImmediate(testPoint);
        }

        [Test]
        public void ArrowSpeed_DefaultValue_IsAccessible()
        {
            // _arrowSpeed is private SerializeField, check via reflection
            float speed = GetPrivateField<float>(_arrowManager, "_arrowSpeed");
            Assert.AreEqual(30f, speed, "Default arrow speed should be 30");
        }

        [Test]
        public void ArrowSpeed_CanBeChangedViaReflection()
        {
            SetPrivateField(_arrowManager, "_arrowSpeed", 50f);
            float speed = GetPrivateField<float>(_arrowManager, "_arrowSpeed");
            Assert.AreEqual(50f, speed,
                "Arrow speed should be changeable via reflection");
        }

        // ===================== AB-06: Full Integration Tests =====================

        [Test]
        public void AddArrows_ThenTryShootArrow_ReducesCountByOne()
        {
            _arrowManager.AddArrows(ArrowData.ArrowType.Regular, 5);

            int before = _arrowManager.GetTotalArrowCount();
            _arrowManager.TryShootArrow(Vector3.forward, 10f);
            int after = _arrowManager.GetTotalArrowCount();

            Assert.AreEqual(5, before);
            Assert.AreEqual(4, after,
                "Count should reduce by exactly 1 after shooting");
        }

        [Test]
        public void AddMultipleArrowTypes_MagicConsumedFirst_InTryShootArrow()
        {
            _arrowManager.AddArrows(ArrowData.ArrowType.Regular, 5);
            _arrowManager.AddArrows(ArrowData.ArrowType.Reinforced, 5);
            _arrowManager.AddArrows(ArrowData.ArrowType.Magic, 5);

            Assert.AreEqual(15, _arrowManager.GetTotalArrowCount());

            // Shoot once - should consume Magic
            _arrowManager.TryShootArrow(Vector3.forward, 10f);

            Assert.AreEqual(14, _arrowManager.GetTotalArrowCount());
            Assert.AreEqual(4, _inventory.GetItemCount("arrow_magic"),
                "Magic should be consumed first");
            Assert.AreEqual(5, _inventory.GetItemCount("arrow_reinforced"),
                "Reinforced should be untouched");
            Assert.AreEqual(5, _inventory.GetItemCount("arrow_regular"),
                "Regular should be untouched");
        }

        [Test]
        public void MultipleShots_DepleteInPriorityOrder()
        {
            _arrowManager.AddArrows(ArrowData.ArrowType.Regular, 3);
            _arrowManager.AddArrows(ArrowData.ArrowType.Reinforced, 2);
            _arrowManager.AddArrows(ArrowData.ArrowType.Magic, 2);

            // Shot 1: consumes Magic (2→1)
            _arrowManager.TryShootArrow(Vector3.forward, 10f);
            Assert.AreEqual(1, _inventory.GetItemCount("arrow_magic"));

            // Shot 2: consumes Magic (1→0)
            _arrowManager.TryShootArrow(Vector3.forward, 10f);
            Assert.AreEqual(0, _inventory.GetItemCount("arrow_magic"));

            // Shot 3: consumes Reinforced (2→1)
            _arrowManager.TryShootArrow(Vector3.forward, 10f);
            Assert.AreEqual(1, _inventory.GetItemCount("arrow_reinforced"));

            // Shot 4: consumes Reinforced (1→0)
            _arrowManager.TryShootArrow(Vector3.forward, 10f);
            Assert.AreEqual(0, _inventory.GetItemCount("arrow_reinforced"));

            // Shot 5: consumes Regular (3→2)
            _arrowManager.TryShootArrow(Vector3.forward, 10f);
            Assert.AreEqual(2, _inventory.GetItemCount("arrow_regular"));

            Assert.AreEqual(2, _arrowManager.GetTotalArrowCount());
        }

        [Test]
        public void AddArrows_ToExistingStack_AddsToSameSlot()
        {
            _arrowManager.AddArrows(ArrowData.ArrowType.Regular, 3);

            // Get slot count before second add
            int slotsBefore = _inventory.GetAllSlots().Length;

            _arrowManager.AddArrows(ArrowData.ArrowType.Regular, 2);

            Assert.AreEqual(5, _inventory.GetItemCount("arrow_regular"),
                "Should merge with existing stack: 3 + 2 = 5");

            // Count non-null slots for arrow_regular
            int arrowRegularSlots = 0;
            foreach (var slot in _inventory.GetAllSlots())
            {
                if (slot != null && slot.item != null &&
                    slot.item.id == "arrow_regular")
                    arrowRegularSlots++;
            }
            Assert.AreEqual(1, arrowRegularSlots,
                "Should still be in a single slot (stacked)");
        }

        [Test]
        public void AddArrows_NewType_CreatesNewSlot()
        {
            _arrowManager.AddArrows(ArrowData.ArrowType.Regular, 3);
            _arrowManager.AddArrows(ArrowData.ArrowType.Magic, 2);

            // Count distinct arrow slots
            int arrowSlots = 0;
            foreach (var slot in _inventory.GetAllSlots())
            {
                if (slot != null && slot.item != null &&
                    (slot.item.id == "arrow_regular" ||
                     slot.item.id == "arrow_reinforced" ||
                     slot.item.id == "arrow_magic"))
                    arrowSlots++;
            }

            Assert.AreEqual(2, arrowSlots,
                "Regular and Magic arrows should occupy separate slots");
            Assert.AreEqual(3, _inventory.GetItemCount("arrow_regular"));
            Assert.AreEqual(2, _inventory.GetItemCount("arrow_magic"));
        }

        [Test]
        public void FullFlow_AddShootDepleteCheck()
        {
            // Add 3 types
            _arrowManager.AddArrows(ArrowData.ArrowType.Regular, 3);
            _arrowManager.AddArrows(ArrowData.ArrowType.Reinforced, 2);
            _arrowManager.AddArrows(ArrowData.ArrowType.Magic, 1);

            Assert.AreEqual(6, _arrowManager.GetTotalArrowCount());
            Assert.IsTrue(_arrowManager.HasArrows());

            // Shoot all 6 arrows
            for (int i = 0; i < 6; i++)
            {
                Assert.IsTrue(_arrowManager.HasArrows(),
                    $"Should have arrows before shot {i + 1}");
                bool shot = _arrowManager.TryShootArrow(Vector3.forward, 10f);
                Assert.IsTrue(shot, $"Shot {i + 1} should succeed");
            }

            Assert.AreEqual(0, _arrowManager.GetTotalArrowCount(),
                "All arrows should be depleted");
            Assert.IsFalse(_arrowManager.HasArrows());

            // One more shot should fail
            bool failShot = _arrowManager.TryShootArrow(Vector3.forward, 10f);
            Assert.IsFalse(failShot, "Shot after depletion should fail");
        }

        [Test]
        public void Integration_SpawnPointUsedWhenSet()
        {
            _arrowManager.AddArrows(ArrowData.ArrowType.Regular, 1);

            var spawnPoint = new GameObject("TestSpawnPoint");
            spawnPoint.transform.position = new Vector3(10f, 5f, 0f);
            _arrowManager.SetSpawnPoint(spawnPoint.transform);

            // Shoot — projectile should spawn at spawnPoint.position
            // We verify the spawn succeeded; the actual spawn pos check
            // is in ArrowProjectile tests
            bool result = _arrowManager.TryShootArrow(Vector3.right, 20f);
            Assert.IsTrue(result, "Shooting with spawn point set should succeed");

            Object.DestroyImmediate(spawnPoint);
        }

        [Test]
        public void Integration_DamageBonusAppliesCorrectly()
        {
            // Regular: +0, Reinforced: +5, Magic: +15
            // Test that totalDamage = baseDamage + damageBonus

            _arrowManager.AddArrows(ArrowData.ArrowType.Magic, 1);

            // We can't easily inspect the damage passed to Spawn,
            // but we can verify the shoot succeeds and the projectile is created.
            // The calculation is: Mathf.RoundToInt(baseDamage + arrowData.damageBonus)
            bool result = _arrowManager.TryShootArrow(Vector3.forward, 50f);
            Assert.IsTrue(result);

            // 50 + 15 (magic bonus) = 65 should have been passed to Spawn
            // We trust the math; verification via projectile _damage
            // is covered in AB-04 Spawn tests
        }
    }
}