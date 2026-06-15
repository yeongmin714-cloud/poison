using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C9-15 병사 포섭 시스템 테스트
    /// </summary>
    public class GuardRecruitTests
    {
        // ===================== 기본 타입 확인 =====================

        [Test]
        public void GuardRecruitSystem_IsStatic()
        {
            Assert.IsNotNull(typeof(GuardRecruitSystem), "GuardRecruitSystem 타입이 존재해야 합니다");
            Assert.IsTrue(typeof(GuardRecruitSystem).IsAbstract && typeof(GuardRecruitSystem).IsSealed,
                "GuardRecruitSystem은 정적 클래스여야 합니다");
        }

        // ===================== 자동 포섭 (Loyalty 100) =====================

        [Test]
        public void AttemptRecruit_Loyalty100_AutoSuccess()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            guard.Loyalty = 100f;

            var result = GuardRecruitSystem.AttemptRecruit(guard);
            Assert.IsTrue(result.success, "Loyalty 100은 자동 포섭");
            Assert.AreEqual("auto", result.method, "자동 포섭 방식");

            Object.DestroyImmediate(go);
        }

        // ===================== 일반 포섭 (Loyalty 70~99) =====================

        [Test]
        public void AttemptRecruit_Loyalty70_NormalSuccess()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            guard.Loyalty = 75f;

            var result = GuardRecruitSystem.AttemptRecruit(guard);
            Assert.IsTrue(result.success, "Loyalty 75는 일반 포섭 성공");
            Assert.AreEqual("normal", result.method);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void AttemptRecruit_Loyalty99_NormalSuccess()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            guard.Loyalty = 99f;

            var result = GuardRecruitSystem.AttemptRecruit(guard);
            Assert.IsTrue(result.success, "Loyalty 99는 일반 포섭");

            Object.DestroyImmediate(go);
        }

        // ===================== 선물 병행 (Loyalty 50~69) =====================

        [Test]
        public void AttemptRecruit_Loyalty50_GiftOrFail()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            guard.Loyalty = 60f;

            // 여러 번 시도하여 성공/실패 케이스 확인
            bool sawSuccess = false, sawFail = false;
            for (int i = 0; i < 20; i++)
            {
                guard.Loyalty = 60f;
                var result = GuardRecruitSystem.AttemptRecruit(guard);
                if (result.success && result.method == "gift") sawSuccess = true;
                if (!result.success) sawFail = true;
            }

            Assert.IsTrue(sawSuccess, "선물 포섭은 성공할 수 있어야 함");
            Assert.IsTrue(sawFail, "선물 포섭은 실패할 수 있어야 함");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void AttemptRecruit_GiftFail_LoyaltyDecreases()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            guard.Loyalty = 55f;

            // 실패할 때까지 시도
            for (int i = 0; i < 50; i++)
            {
                guard.Loyalty = 55f;
                var result = GuardRecruitSystem.AttemptRecruit(guard);
                if (!result.success)
                {
                    Assert.Less(guard.Loyalty, 55f, "실패 시 호감도 감소");
                    break;
                }
            }

            Object.DestroyImmediate(go);
        }

        // ===================== 위협 포섭 (Loyalty 0~49) =====================

        [Test]
        public void AttemptRecruit_Loyalty30_ThreatOrFail()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            guard.Loyalty = 30f;

            bool sawThreat = false, sawFail = false;
            for (int i = 0; i < 30; i++)
            {
                guard.Loyalty = 30f;
                var result = GuardRecruitSystem.AttemptRecruit(guard);
                if (result.success && result.method == "threat") sawThreat = true;
                if (!result.success) sawFail = true;
            }

            Assert.IsTrue(sawThreat, "위협 포섭은 성공 가능");
            Assert.IsTrue(sawFail, "위협 포섭은 실패 가능");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void AttemptRecruit_ThreatFail_LoyaltyDecreases()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            guard.Loyalty = 20f;

            for (int i = 0; i < 50; i++)
            {
                guard.Loyalty = 20f;
                var result = GuardRecruitSystem.AttemptRecruit(guard);
                if (!result.success)
                {
                    Assert.LessOrEqual(guard.Loyalty, 20f, "위협 실패 시 호감도 -20");
                    break;
                }
            }

            Object.DestroyImmediate(go);
        }

        // ===================== 포섭 불가 =====================

        [Test]
        public void AttemptRecruit_DeadGuard_Fails()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            guard.TakeDamage(999f, Vector3.zero);

            var result = GuardRecruitSystem.AttemptRecruit(guard);
            Assert.IsFalse(result.success, "죽은 병사는 포섭 불가");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void AttemptRecruit_NullGuard_Fails()
        {
            var result = GuardRecruitSystem.AttemptRecruit(null);
            Assert.IsFalse(result.success, "null 대상은 포섭 불가");
        }

        // ===================== GetMaxRecruits =====================

        [Test]
        public void GetMaxRecruits_Level1_Returns5()
        {
            Assert.AreEqual(5, GuardRecruitSystem.GetMaxRecruits(1));
        }

        [Test]
        public void GetMaxRecruits_Level10_Returns50()
        {
            Assert.AreEqual(50, GuardRecruitSystem.GetMaxRecruits(10));
        }

        // ===================== GuardPlaceholder 연동 =====================

        [Test]
        public void GuardPlaceholder_IsRecruited_InitiallyFalse()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            Assert.IsFalse(guard.IsRecruited, "초기 포섭 상태는 false");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void GuardPlaceholder_HasRecruitMethod()
        {
            var method = typeof(GuardPlaceholder).GetMethod("OnRecruit",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method, "OnRecruit 메서드가 있어야 합니다");
        }
    }
}