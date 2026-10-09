using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C9-21 동행 병사 전투 AI 테스트
    /// </summary>
    public class CombatAITests
    {
        // ===================== GuardCombatAI 타입 =====================

        [Test]
        public void GuardCombatAI_IsStatic()
        {
            Assert.IsNotNull(typeof(GuardCombatAI), "GuardCombatAI 타입이 존재해야 합니다");
            Assert.IsTrue(typeof(GuardCombatAI).IsAbstract && typeof(GuardCombatAI).IsSealed,
                "GuardCombatAI는 정적 클래스여야 합니다");
        }

        // ===================== GuardPlaceholder 전투 상태 =====================

        [Test]
        public void GuardPlaceholder_CombatState_Initial()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();

            Assert.IsFalse(guard.IsInCombat, "초기 전투 상태 = false");
            Assert.AreEqual(0f, guard.CombatTimer, "초기 타이머 = 0");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void GuardPlaceholder_SetInCombat_Works()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();

            guard.SetInCombat(true);
            Assert.IsTrue(guard.IsInCombat, "전투 시작");
            Assert.AreEqual(0f, guard.CombatTimer, "타이머 리셋");

            guard.SetInCombat(false);
            Assert.IsFalse(guard.IsInCombat, "전투 종료");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void GuardPlaceholder_UpdateCombatTimer_Increases()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();

            guard.SetInCombat(true);
            guard.UpdateCombatTimer(1f);
            Assert.AreEqual(1f, guard.CombatTimer, "타이머 증가");

            guard.UpdateCombatTimer(0.5f);
            Assert.AreEqual(1.5f, guard.CombatTimer, "타이머 누적");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void GuardPlaceholder_ResetCombatTimer_Resets()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();

            guard.SetInCombat(true);
            guard.UpdateCombatTimer(3f);
            guard.ResetCombatTimer();
            Assert.AreEqual(0f, guard.CombatTimer, "타이머 리셋");

            Object.DestroyImmediate(go);
        }

        // ===================== NotifyPlayerAttack =====================

        [Test]
        public void GuardCombatAI_NotifyPlayerAttack_NullTarget_NoError()
        {
            // null 타겟은 예외 없이 처리
            GuardCombatAI.NotifyPlayerAttack(null);
            Assert.Pass("null 타겟 처리 성공");
        }

        [Test]
        public void GuardCombatAI_NotifyPlayerAttack_InvalidTarget_NoError()
        {
            var go = new GameObject("NotDamageable");
            GuardCombatAI.NotifyPlayerAttack(go);
            Assert.Pass("IDamageable 아닌 타겟 처리 성공");
            Object.DestroyImmediate(go);
        }

        // ===================== UpdateGuardBehavior =====================

        [Test]
        public void UpdateGuardBehavior_NullGuard_NoError()
        {
            var playerGo = new GameObject("Player");
            GuardCombatAI.UpdateGuardBehavior(null, playerGo.transform);
            Assert.Pass("null 병사 처리 성공");
            Object.DestroyImmediate(playerGo);
        }

        [Test]
        public void UpdateGuardBehavior_NonRecruited_NoAction()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            // IsRecruited = false (default)

            var playerGo = new GameObject("Player");
            GuardCombatAI.UpdateGuardBehavior(guard, playerGo.transform);
            Assert.IsFalse(guard.HasCommand, "포섭되지 않은 병사는 명령 없음");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(playerGo);
        }

        [TestCase(0f)]
        [TestCase(1.25f)]
        public void ExecuteMovement_AttackCommandPersistsAtAndInsideArrivalRadius(float distanceToWaypoint)
        {
            var guardGo = new GameObject("AttackCommandGuard");
            var guard = guardGo.AddComponent<GuardPlaceholder>();
            guard.SetCommandTarget(new Vector3(distanceToWaypoint, 0f, 0f), true);

            // No target is visible yet. The explicit attack order must wait at its point
            // rather than being discarded merely because it reached the waypoint.
            guard.ExecuteMovement();

            Assert.IsTrue(guard.HasCommand, "공격 지점 도착은 명령 완료가 아니다. 공격 대상 탐색을 계속할 수 있도록 명령을 유지해야 한다.");
            Assert.IsTrue(guard.IsAttackCommand, "도착 후에도 명령 종류는 공격이어야 한다.");
            Assert.IsNull(guard.CurrentAttackTarget, "타겟이 탐색되지 않은 상태에서는 공격 대상을 임의로 지정하면 안 된다.");
            Assert.AreEqual(new Vector3(distanceToWaypoint, 0f, 0f), guard.CommandTarget,
                "대상을 찾지 못한 상태에서 원래 공격 지점을 보존해야 한다.");

            guard.ClearCommand();
            Assert.IsFalse(guard.HasCommand, "명시적 ClearCommand는 공격 명령을 해제해야 한다.");
            Object.DestroyImmediate(guardGo);
        }

        [Test]
        public void ExecuteMovement_AttackCommandTracksResolvedMovingTarget()
        {
            var guardGo = new GameObject("AttackCommandGuard");
            var guard = guardGo.AddComponent<GuardPlaceholder>();
            var targetGo = new GameObject("AttackTarget");
            targetGo.transform.position = new Vector3(4f, 0f, 0f);
            targetGo.AddComponent<SphereCollider>();
            targetGo.AddComponent<AnimalAI>();
            Physics.SyncTransforms();
            guard.SetCommandTarget(new Vector3(2f, 0f, 0f), true);

            guard.ExecuteMovement();
            Assert.IsTrue(guard.HasCommand, "유효한 공격 명령은 ExecuteMovement 이후 유지되어야 한다.");
            Assert.AreSame(targetGo.GetComponent<AnimalAI>(), guard.CurrentAttackTarget);

            targetGo.transform.position = new Vector3(5f, 0f, 0f);
            Physics.SyncTransforms();
            guard.ExecuteMovement();

            Assert.IsTrue(guard.HasCommand, "이동 중인 대상을 따라가도 공격 명령이 유지되어야 한다.");
            Assert.AreEqual(new Vector3(3f, 0f, 0f), guard.CommandTarget,
                "최초 공격 지점의 타겟 상대 오프셋을 보존하며 접근점을 갱신해야 한다.");
            Assert.AreSame(targetGo.GetComponent<AnimalAI>(), guard.CurrentAttackTarget);

            Object.DestroyImmediate(guardGo);
            Object.DestroyImmediate(targetGo);
        }

        [Test]
        public void ExecuteMovement_AttackCommandClearsAfterResolvedTargetDies()
        {
            var guardGo = new GameObject("AttackCommandGuard");
            var guard = guardGo.AddComponent<GuardPlaceholder>();
            var targetGo = new GameObject("AttackTarget");
            targetGo.transform.position = new Vector3(2f, 0f, 0f);
            targetGo.AddComponent<SphereCollider>();
            var target = targetGo.AddComponent<AnimalAI>();
            Physics.SyncTransforms();
            guard.SetCommandTarget(Vector3.zero, true);

            guard.ExecuteMovement();
            Assert.AreSame(target, guard.CurrentAttackTarget);
            target.TakeDamage(10000f, Vector3.zero);
            guard.ExecuteMovement();

            Assert.IsFalse(guard.HasCommand, "실제 지정 대상이 사망하면 공격 명령이 종료되어야 한다.");
            Assert.IsNull(guard.CurrentAttackTarget);
            Object.DestroyImmediate(guardGo);
            Object.DestroyImmediate(targetGo);
        }

        [Test]
        public void ExecuteMovement_NonAttackMoveStillClearsAtWaypoint()
        {
            var guardGo = new GameObject("MoveCommandGuard");
            var guard = guardGo.AddComponent<GuardPlaceholder>();
            guard.SetCommandTarget(Vector3.zero, false);

            guard.ExecuteMovement();

            Assert.IsFalse(guard.HasCommand, "일반 이동 명령은 목적지 도착 시 기존처럼 해제되어야 한다.");
            Object.DestroyImmediate(guardGo);
        }

        // ===================== 상수 확인 =====================

        [Test]
        public void CombatAI_Constants_Defined()
        {
            Assert.AreEqual(3f, GuardCombatAI.FOLLOW_DISTANCE);
            Assert.AreEqual(8f, GuardCombatAI.MAX_FOLLOW_DISTANCE);
            Assert.AreEqual(15f, GuardCombatAI.COMBAT_DETECT_RANGE);
            Assert.AreEqual(2f, GuardCombatAI.RETURN_AFTER_COMBAT_DELAY);
        }

        // ===================== RecallAll =====================

        [Test]
        public void RecallAll_NoPlayer_NullCheck()
        {
            GuardCombatAI.RecallAll(null);
            Assert.Pass("null 플레이어 처리");
        }
    }
}