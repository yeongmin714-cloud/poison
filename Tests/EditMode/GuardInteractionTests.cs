using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;
using ProjectName.Core.Data;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C9-08 병사 기본 상호작용 테스트
    /// </summary>
    public class GuardInteractionTests
    {
        // ===================== 기본 상호작용 필드 =====================

        [Test]
        public void GuardPlaceholder_HasInteractionFields()
        {
            var type = typeof(GuardPlaceholder);

            var interactField = type.GetField("_interactRange",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(interactField, "_interactRange 필드가 있어야 합니다");
            Assert.AreEqual(typeof(float), interactField.FieldType, "_interactRange는 float");
        }

        [Test]
        public void GuardPlaceholder_HasLoyaltyField()
        {
            var field = typeof(GuardPlaceholder).GetField("_loyalty",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "_loyalty 필드가 있어야 합니다");
            Assert.AreEqual(typeof(float), field.FieldType, "_loyalty는 float");
        }

        [Test]
        public void GuardPlaceholder_HasAddictionField()
        {
            var field = typeof(GuardPlaceholder).GetField("_addiction",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "_addiction 필드가 있어야 합니다");
        }

        [Test]
        public void GuardPlaceholder_HasJobTitleField()
        {
            var field = typeof(GuardPlaceholder).GetField("jobTitle",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "jobTitle 필드가 있어야 합니다");
        }

        // ===================== 퍼블릭 속성 테스트 =====================

        [Test]
        public void GuardPlaceholder_PublicProperties_Work()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();

            Assert.IsNotNull(guard.GuardName, "GuardName 속성이 null이 아니어야 합니다");
            Assert.GreaterOrEqual(guard.Level, 1, "Level이 1 이상이어야 합니다");
            Assert.IsNotNull(guard.Nation, "Nation 속성이 null이 아니어야 합니다");
            Assert.IsNotNull(guard.JobTitle, "JobTitle 속성이 null이 아니어야 합니다");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void GuardPlaceholder_Loyalty_Clamped()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();

            guard.Loyalty = 150;
            Assert.AreEqual(100, guard.Loyalty, "Loyalty는 100으로 클램프되어야 합니다");

            guard.Loyalty = -10;
            Assert.AreEqual(0, guard.Loyalty, "Loyalty는 0으로 클램프되어야 합니다");

            guard.Loyalty = 75;
            Assert.AreEqual(75, guard.Loyalty, "Loyalty 설정값이 정확히 반영되어야 합니다");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void GuardPlaceholder_Addiction_Clamped()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();

            guard.Addiction = 200;
            Assert.AreEqual(100, guard.Addiction, "Addiction은 100으로 클램프");

            guard.Addiction = 0;
            Assert.AreEqual(0, guard.Addiction, "Addiction은 0 이상");

            Object.DestroyImmediate(go);
        }

        // ===================== 초기 상태 테스트 =====================

        [Test]
        public void GuardPlaceholder_DefaultValues()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();

            Assert.AreEqual(50f, guard.Loyalty, "기본 호감도는 50");
            Assert.AreEqual(0f, guard.Addiction, "기본 중독도는 0");
            Assert.IsFalse(guard.IsPlayerNearby, "초기에는 플레이어 근접 아님");
            Assert.IsFalse(guard.IsShowingInfo, "초기에는 정보 표시 안 함");

            Object.DestroyImmediate(go);
        }

        // ===================== SetGuardInfo 메서드 =====================

        [Test]
        public void GuardPlaceholder_SetGuardInfo_Works()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();

            guard.SetGuardInfo("테스트 병사", 5, NationType.East);
            Assert.AreEqual("테스트 병사", guard.GuardName);
            Assert.AreEqual(5, guard.Level);
            Assert.AreEqual("동", guard.Nation);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void GuardPlaceholder_SetGuardInfo_AllNations()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();

            guard.SetGuardInfo("A", 1, NationType.West);
            Assert.AreEqual("서", guard.Nation);

            guard.SetGuardInfo("B", 2, NationType.South);
            Assert.AreEqual("남", guard.Nation);

            guard.SetGuardInfo("C", 3, NationType.North);
            Assert.AreEqual("북", guard.Nation);

            guard.SetGuardInfo("D", 4, NationType.Empire);
            Assert.AreEqual("황제국", guard.Nation);

            Object.DestroyImmediate(go);
        }

        // ===================== 상호작용 메서드 테스트 =====================

        [Test]
        public void GuardPlaceholder_HasOnTalkMethod()
        {
            var method = typeof(GuardPlaceholder).GetMethod("OnTalk",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method, "OnTalk 메서드가 있어야 합니다 (말걸기)");
        }

        [Test]
        public void GuardPlaceholder_HasOnGiveFoodMethod()
        {
            var method = typeof(GuardPlaceholder).GetMethod("OnGiveFood",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method, "OnGiveFood 메서드가 있어야 합니다 (음식주기)");
        }

        [Test]
        public void GuardPlaceholder_HasOnGiveDrugMethod()
        {
            var method = typeof(GuardPlaceholder).GetMethod("OnGiveDrug",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method, "OnGiveDrug 메서드가 있어야 합니다 (약주기)");
        }

        // ===================== JobTitle 속성 테스트 =====================

        [Test]
        public void GuardPlaceholder_JobTitle_CanSet()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();

            guard.JobTitle = "창병";
            Assert.AreEqual("창병", guard.JobTitle);

            guard.JobTitle = "검병";
            Assert.AreEqual("검병", guard.JobTitle);

            Object.DestroyImmediate(go);
        }

        // ===================== HP 관리 테스트 =====================

        [Test]
        public void GuardPlaceholder_HP_Initialized()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();

            Assert.AreEqual(guard.MaxHP, guard.HP, "초기 HP = MaxHP");
            Assert.IsTrue(guard.IsAlive, "초기에는 살아있음");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void GuardPlaceholder_IDamageable_Works()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();

            guard.TakeDamage(5f, Vector3.zero);
            Assert.AreEqual(guard.MaxHP - 5f, guard.HP, "데미지 5 적용 확인");
            Assert.IsTrue(guard.IsAlive, "데미지 후에도 살아있어야 함");

            guard.TakeDamage(100f, Vector3.zero);
            Assert.IsFalse(guard.IsAlive, "치명적 데미지 후 사망");

            Object.DestroyImmediate(go);
        }
    }
}