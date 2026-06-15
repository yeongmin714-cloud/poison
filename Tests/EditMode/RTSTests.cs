using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C9-20 RTS 기본 명령 테스트
    /// </summary>
    public class RTSTests
    {
        // ===================== GuardPlaceholder RTS 메서드 =====================

        [Test]
        public void GuardPlaceholder_HasRTSMethods()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();

            // SetSelected/IsSelected
            Assert.IsFalse(guard.IsSelected, "초기 선택 안 됨");
            guard.SetSelected(true);
            Assert.IsTrue(guard.IsSelected, "선택됨");
            guard.SetSelected(false);
            Assert.IsFalse(guard.IsSelected, "선택 해제");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void GuardPlaceholder_SetCommandTarget_Works()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();

            Assert.IsFalse(guard.HasCommand, "초기 명령 없음");

            guard.SetCommandTarget(new Vector3(10, 0, 20), true);
            Assert.IsTrue(guard.HasCommand, "명령 설정됨");
            Assert.IsTrue(guard.IsAttackCommand, "공격 명령");
            Assert.AreEqual(new Vector3(10, 0, 20), guard.CommandTarget);

            guard.ClearCommand();
            Assert.IsFalse(guard.HasCommand, "명령 해제");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void GuardPlaceholder_ClearCommand_Works()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            guard.SetCommandTarget(Vector3.zero, true);
            guard.ClearCommand();
            Assert.IsFalse(guard.HasCommand);
            Assert.IsFalse(guard.IsAttackCommand);
            Object.DestroyImmediate(go);
        }

        // ===================== GuardSelectionManager 타입 확인 =====================

        [Test]
        public void GuardSelectionManager_Type_Exists()
        {
            Assert.IsNotNull(typeof(GuardSelectionManager), "GuardSelectionManager 타입 존재");
        }

        [Test]
        public void GuardSelectionManager_IsMonoBehaviour()
        {
            Assert.IsTrue(typeof(GuardSelectionManager).IsSubclassOf(typeof(MonoBehaviour)),
                "GuardSelectionManager는 MonoBehaviour");
        }

        // ===================== SelectGuardsInRect =====================

        [Test]
        public void GuardSelectionManager_HasSelectInRect()
        {
            var method = typeof(GuardSelectionManager).GetMethod("SelectGuardsInRect");
            Assert.IsNotNull(method, "SelectGuardsInRect 메서드 필요");
        }

        [Test]
        public void GuardSelectionManager_HasStopAllGuards()
        {
            var method = typeof(GuardSelectionManager).GetMethod("StopAllGuards",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method, "StopAllGuards 메서드 필요");
        }

        // ===================== AddToSelection / ClearSelection =====================

        [Test]
        public void GuardSelectionManager_AddClearSelection_Works()
        {
            var mgrGo = new GameObject("TestMgr");
            var mgr = mgrGo.AddComponent<GuardSelectionManager>();

            var guardGo = new GameObject("TestGuard");
            var guard = guardGo.AddComponent<GuardPlaceholder>();

            mgr.AddToSelection(guard);
            // Clear via ClearSelection
            mgr.ClearSelection();
            Assert.IsFalse(guard.IsSelected, "ClearSelection 후 선택 해제");

            Object.DestroyImmediate(guardGo);
            Object.DestroyImmediate(mgrGo);
        }

        // ===================== 명령 메서드 확인 =====================

        [Test]
        public void GuardSelectionManager_HasIssueMethods()
        {
            var issueAttack = typeof(GuardSelectionManager).GetMethod("IssueAttackCommand",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var issueMove = typeof(GuardSelectionManager).GetMethod("IssueMoveCommand",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Assert.IsNotNull(issueAttack, "IssueAttackCommand 필요");
            Assert.IsNotNull(issueMove, "IssueMoveCommand 필요");
        }
    }
}