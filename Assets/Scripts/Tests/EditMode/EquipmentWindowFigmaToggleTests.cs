using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ProjectName.UI.Toolkit;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    public class EquipmentWindowFigmaToggleTests
    {
        private static readonly FieldInfo InstanceField = typeof(EquipmentWindowUTK).GetField(
            "_instance", BindingFlags.Static | BindingFlags.NonPublic);

        private GameObject _managerObject;
        private ArrayList _previousOpenStack;

        [SetUp]
        public void SetUp()
        {
            ResetSingleton();
            _previousOpenStack = new ArrayList(GetOpenStack());
            GetOpenStack().Clear();

            // Supply the manager's updater so Register/Unregister avoid DontDestroyOnLoad in EditMode.
            _managerObject = new GameObject("EquipmentToggleTestWindowManager");
            var updaterType = typeof(UTKWindowManager).GetNestedType("Updater", BindingFlags.NonPublic);
            Assert.That(updaterType, Is.Not.Null, "Expected the UTK window manager updater type.");
            var updater = _managerObject.AddComponent(updaterType);
            typeof(UTKWindowManager).GetField("_updater", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, updater);
        }

        [TearDown]
        public void TearDown()
        {
            var window = EquipmentWindowUTK.Instance;
            if (window != null && window.IsOpen)
                window.Hide();
            ResetSingleton();
            GetOpenStack().Clear();
            if (_previousOpenStack != null)
                foreach (var previousWindow in _previousOpenStack)
                    GetOpenStack().Add(previousWindow);
            typeof(UTKWindowManager).GetField("_updater", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, null);
            if (_managerObject != null)
                Object.DestroyImmediate(_managerObject);
        }

        private static System.Collections.IList GetOpenStack()
        {
            return (System.Collections.IList)typeof(UTKWindowManager)
                .GetField("_openStack", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        }

        [Test]
        public void Toggle_FromClosed_OpensWindow()
        {
            Assert.That(EquipmentWindowUTK.Instance, Is.Null, "The test starts without a singleton window.");

            InvokeToggle();

            Assert.That(EquipmentWindowUTK.Instance, Is.Not.Null, "Toggle creates the singleton window.");
            Assert.That(EquipmentWindowUTK.Instance.IsOpen, Is.True,
                "A toggle request must show the window when it starts closed.");
        }

        [Test]
        public void Toggle_FromOpen_ClosesWindow()
        {
            EquipmentWindowUTK.Open();
            Assert.That(EquipmentWindowUTK.Instance.IsOpen, Is.True, "Open establishes the initial state.");

            InvokeToggle();

            Assert.That(EquipmentWindowUTK.Instance.IsOpen, Is.False,
                "A toggle request must close the window when it starts open.");
        }

        private static void InvokeToggle()
        {
            typeof(EquipmentWindowUTK).GetMethod("Toggle", BindingFlags.Static | BindingFlags.Public)
                .Invoke(null, null);
        }

        private static void ResetSingleton()
        {
            if (InstanceField == null)
                Assert.Fail("Expected the EquipmentWindowUTK singleton field.");
            InstanceField.SetValue(null, null);
        }
    }
}
