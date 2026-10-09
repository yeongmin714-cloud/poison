using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    public class GuardSelectionManagerStaleSelectionTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private SpecialEffectsController _previousSpecialEffectsInstance;
        private bool _specialEffectsInstanceOverridden;

        [TearDown]
        public void TearDown()
        {
            GuardSelectionManager.SelectionChanged -= OnSelectionChanged;
            if (_specialEffectsInstanceOverridden)
            {
                SetStaticField(typeof(SpecialEffectsController), "_instance", _previousSpecialEffectsInstance);
                _specialEffectsInstanceOverridden = false;
            }
            foreach (var go in _objects)
                if (go != null) Object.DestroyImmediate(go);
            _objects.Clear();
        }

        private void InstallEditModeSpecialEffectsStub()
        {
            var instanceField = typeof(SpecialEffectsController).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(instanceField, "Expected SpecialEffectsController's singleton field.");
            _previousSpecialEffectsInstance = (SpecialEffectsController)instanceField.GetValue(null);
            _specialEffectsInstanceOverridden = true;

            var stubObject = Create("edit mode special effects stub");
            stubObject.SetActive(false);
            var stub = stubObject.AddComponent<SpecialEffectsController>();
            instanceField.SetValue(null, stub);
        }

        private static void SetStaticField(System.Type type, string fieldName, object value)
        {
            var field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Expected field " + fieldName);
            field.SetValue(null, value);
        }

        private int _notificationCount;
        private int _lastNotifiedCount;

        [Test]
        public void LateUpdate_RemovesDeadAndInactiveGuards_NotifiesOnce_AndRemovesRings()
        {
            InstallEditModeSpecialEffectsStub();
            var manager = Create("selection manager").AddComponent<GuardSelectionManager>();
            var deadGuard = Create("dead guard").AddComponent<GuardPlaceholder>();
            var inactiveGuard = Create("inactive guard").AddComponent<GuardPlaceholder>();
            var selectedGuardsField = typeof(GuardSelectionManager).GetField("_selectedGuards", InstanceFlags);
            Assert.IsNotNull(selectedGuardsField, "Expected the manager's selected-guard list.");
            var selectedGuards = (List<GuardPlaceholder>)selectedGuardsField.GetValue(manager);
            selectedGuards.Add(deadGuard);
            selectedGuards.Add(inactiveGuard);

            SetField(deadGuard, "_isSelected", true);
            SetField(inactiveGuard, "_isSelected", true);
            SetField(deadGuard, "_isDead", true);
            inactiveGuard.gameObject.SetActive(false);
            var auraField = typeof(GuardSelectionManager).GetField("_selectionAuras", InstanceFlags);
            var auras = (Dictionary<GuardPlaceholder, GameObject>)auraField.GetValue(manager);
            var deadRing = Create("dead ring");
            var inactiveRing = Create("inactive ring");
            deadRing.SetActive(false);
            inactiveRing.SetActive(false);
            auras[deadGuard] = deadRing;
            auras[inactiveGuard] = inactiveRing;

            _notificationCount = 0;
            _lastNotifiedCount = -1;
            GuardSelectionManager.SelectionChanged += OnSelectionChanged;

            const string editModeDestroyMessage = "Destroy may not be called from edit mode! Use DestroyImmediate instead.\nDestroying an object in edit mode destroys it permanently.";
            LogAssert.Expect(UnityEngine.LogType.Error, editModeDestroyMessage);
            LogAssert.Expect(UnityEngine.LogType.Error, editModeDestroyMessage);
            InvokeLateUpdate(manager);

            Assert.AreEqual(0, manager.SelectedCount);
            Assert.IsFalse(deadGuard.IsSelected);
            Assert.IsFalse(inactiveGuard.IsSelected);
            Assert.AreEqual(1, _notificationCount, "UI receives one update for the pruned selection set.");
            Assert.AreEqual(0, _lastNotifiedCount);
            Assert.IsFalse(auras.ContainsKey(deadGuard), "dead guard ring is removed from the manager cache.");
            Assert.IsFalse(auras.ContainsKey(inactiveGuard), "inactive guard ring is removed from the manager cache.");

            InvokeLateUpdate(manager);
            Assert.AreEqual(1, _notificationCount, "repeated sync must not notify or remove a ring twice.");
        }

        private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;

        private void OnSelectionChanged(IReadOnlyList<GuardPlaceholder> guards, int count)
        {
            _notificationCount++;
            _lastNotifiedCount = count;
        }

        private static void InvokeLateUpdate(GuardSelectionManager manager)
        {
            var method = typeof(GuardSelectionManager).GetMethod("LateUpdate", InstanceFlags);
            Assert.IsNotNull(method);
            method.Invoke(manager, null);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, InstanceFlags);
            Assert.IsNotNull(field, "Expected field " + fieldName);
            field.SetValue(target, value);
        }

        private GameObject Create(string name)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            return go;
        }
    }
}
