using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using ProjectName.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.Tests.EditMode
{
    public class SoldierManagementLifecycleSummaryTests
    {
        private static readonly BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

        private GameObject _rootObject;
        private GameObject _windowManagerObject;
        private GameObject _managerObject;
        private GameObject _taskSystemObject;
        private GameObject[] _guardObjects;
        private UIDocument _previousDocument;
        private ArrayList _previousOpenStack;
        private SoldierManagementUTK _previousWindowInstance;
        private Component _previousUpdater;
        private bool _previousWarnedNoUpdater;
        private TerritoryOwnership _previousTerritoryOwnership;
        private bool _previousAnyWindowOpen;
        private GuardManager _previousGuardManager;
        private GuardTaskSystem _previousGuardTaskSystem;

        [SetUp]
        public void SetUp()
        {
            _previousWindowInstance = (SoldierManagementUTK)typeof(SoldierManagementUTK)
                .GetField("_instance", StaticPrivate).GetValue(null);
            typeof(SoldierManagementUTK).GetField("_instance", StaticPrivate).SetValue(null, null);
            _previousDocument = (UIDocument)typeof(UIToolkitBootstrap)
                .GetField("_document", StaticPrivate).GetValue(null);
            _previousOpenStack = new ArrayList(GetOpenStack());
            _previousAnyWindowOpen = UITransitionState.AnyWindowOpen;
            var warnedField = typeof(UTKWindowManager).GetField("_warnedNoUpdater", StaticPrivate);
            _previousWarnedNoUpdater = (bool)warnedField.GetValue(null);
            GetOpenStack().Clear();
            UITransitionState.AnyWindowOpen = false;
            _rootObject = new GameObject("SoldierManagementTestRoot");
            var document = _rootObject.AddComponent<UIDocument>();
            document.panelSettings = Resources.Load<PanelSettings>("UI/PanelSettings");
            typeof(UIToolkitBootstrap).GetField("_document", StaticPrivate).SetValue(null, document);

            _windowManagerObject = new GameObject("SoldierManagementTestWindowManager");
            var updaterType = typeof(UTKWindowManager).GetNestedType("Updater", StaticPrivate);
            Assert.That(updaterType, Is.Not.Null);
            _previousUpdater = (Component)typeof(UTKWindowManager)
                .GetField("_updater", StaticPrivate).GetValue(null);
            if (_previousUpdater == null)
                typeof(UTKWindowManager).GetField("_warnedNoUpdater", StaticPrivate).SetValue(null, false);
            var updater = _windowManagerObject.AddComponent(updaterType);
            typeof(UTKWindowManager).GetField("_updater", StaticPrivate).SetValue(null, updater);

            var database = TerritoryDatabase.Instance;
            var territoryState = database.GetState(NationType.East, 1);
            _previousTerritoryOwnership = territoryState.ownership;
            territoryState.ownership = TerritoryOwnership.PlayerOwned;

            var guardManagerProperty = typeof(GuardManager).GetProperty("Instance");
            _previousGuardManager = GuardManager.Instance;
            guardManagerProperty.SetValue(null, null);
            _managerObject = new GameObject("SoldierManagementTestGuardManager");
            var guardManager = _managerObject.AddComponent<GuardManager>();
            guardManagerProperty.SetValue(null, guardManager);
            _previousGuardTaskSystem = GuardTaskSystem.Instance;
            var taskSystemProperty = typeof(GuardTaskSystem).GetProperty("Instance");
            taskSystemProperty.SetValue(null, null);
            _taskSystemObject = new GameObject("SoldierManagementTestGuardTaskSystem");
            var taskSystem = _taskSystemObject.AddComponent<GuardTaskSystem>();
            taskSystemProperty.SetValue(null, taskSystem);
        }

        [TearDown]
        public void TearDown()
        {
            var window = SoldierManagementUTK.Instance;
            if (window != null && window.IsOpen)
                window.Hide();
            if (window != null)
                window.RemoveFromHierarchy();

            if (_guardObjects != null)
                foreach (var guardObject in _guardObjects)
                {
                    if (guardObject != null)
                        GuardManager.Instance?.RemoveGuardFromAllTerritories(guardObject.GetComponent<GuardPlaceholder>());
                    if (guardObject != null)
                        Object.DestroyImmediate(guardObject);
                }
            if (_taskSystemObject != null) Object.DestroyImmediate(_taskSystemObject);
            if (_managerObject != null) Object.DestroyImmediate(_managerObject);
            typeof(GuardManager).GetProperty("Instance").SetValue(null, _previousGuardManager);
            typeof(GuardTaskSystem).GetProperty("Instance").SetValue(null, _previousGuardTaskSystem);

            TerritoryDatabase.Instance.GetState(NationType.East, 1).ownership = _previousTerritoryOwnership;
            typeof(UIToolkitBootstrap).GetField("_document", StaticPrivate).SetValue(null, _previousDocument);
            GetOpenStack().Clear();
            foreach (var previousWindow in _previousOpenStack)
                GetOpenStack().Add(previousWindow);
            UITransitionState.AnyWindowOpen = _previousAnyWindowOpen;
            typeof(SoldierManagementUTK).GetField("_instance", StaticPrivate)
                .SetValue(null, _previousWindowInstance);
            typeof(UTKWindowManager).GetField("_updater", StaticPrivate).SetValue(null, _previousUpdater);
            typeof(UTKWindowManager).GetField("_warnedNoUpdater", StaticPrivate)
                .SetValue(null, _previousWarnedNoUpdater);
            if (_windowManagerObject != null) Object.DestroyImmediate(_windowManagerObject);
            if (_rootObject != null) Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void Toggle_FromClosed_CreatesOpensAndAttachesWindow()
        {
            SoldierManagementUTK.Toggle();

            Assert.That(SoldierManagementUTK.Instance, Is.Not.Null);
            Assert.That(SoldierManagementUTK.Instance.IsOpen, Is.True);
            Assert.That(SoldierManagementUTK.Instance.parent, Is.SameAs(UIToolkitBootstrap.UIRoot),
                "The window should be attached to the active UI root on the public open route.");
        }

        [Test]
        public void Toggle_FromOpen_HidesWindow()
        {
            SoldierManagementUTK.Open();
            Assert.That(SoldierManagementUTK.Instance.IsOpen, Is.True);

            SoldierManagementUTK.Toggle();

            Assert.That(SoldierManagementUTK.Instance.IsOpen, Is.False);
            Assert.That(SoldierManagementUTK.Instance.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void Open_ShowsAttachedSummaryWithLiveGuardAndAssignedTaskCounts()
        {
            TerritoryId territory = new TerritoryId(NationType.East, 1);
            GuardPlaceholder first = CreateGuard("Summary Guard A");
            GuardPlaceholder second = CreateGuard("Summary Guard B");
            GuardManager.Instance.RegisterGuard(territory, first);
            GuardManager.Instance.RegisterGuard(territory, second);
            GuardTaskSystem.Instance.AssignTask(first, GuardTaskSystem.GuardTask.Defend);

            SoldierManagementUTK.Open();

            Label summary = SoldierManagementUTK.Instance.Q<Label>("DeploySummary");
            Assert.That(summary, Is.Not.Null, "The summary label must be held in the visible list panel, outside the scroll view.");
            Assert.That(summary.parent, Is.Not.Null);
            Assert.That(summary.parent, Is.Not.SameAs(SoldierManagementUTK.Instance.Q<ScrollView>()));
            Assert.That(summary.text, Is.EqualTo("병사 2/2명 · 임무 배치 1명"));
        }

        private GuardPlaceholder CreateGuard(string guardName)
        {
            if (_guardObjects == null) _guardObjects = new GameObject[2];
            int index = _guardObjects[0] == null ? 0 : 1;
            var guardObject = new GameObject(guardName);
            _guardObjects[index] = guardObject;
            var guard = guardObject.AddComponent<GuardPlaceholder>();
            guard.SetGuardInfo(guardName, 1, NationType.East);
            typeof(GuardPlaceholder).GetField("_isRecruited", InstancePrivate).SetValue(guard, true);
            return guard;
        }


        private static IList GetOpenStack()
        {
            return (IList)typeof(UTKWindowManager).GetField("_openStack", StaticPrivate).GetValue(null);
        }
    }
}
