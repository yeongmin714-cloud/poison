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