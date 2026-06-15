using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C9-16 병사 상태 체계 테스트
    /// </summary>
    public class GuardStatusTests
    {
        // ===================== GuardRole 열거형 =====================

        [Test]
        public void GuardRole_HasAllRoles()
        {
            Assert.AreEqual(0, (int)GuardRole.Soldier);
            Assert.AreEqual(1, (int)GuardRole.Herbalist);
            Assert.AreEqual(2, (int)GuardRole.Hunter);
            Assert.AreEqual(3, (int)GuardRole.Informant);
            Assert.AreEqual(4, (int)GuardRole.Miner);
        }

        // ===================== GetRoleName =====================

        [Test]
        public void GetRoleName_All_NotEmpty()
        {
            foreach (GuardRole role in System.Enum.GetValues(typeof(GuardRole)))
            {
                string name = GuardStatusSystem.GetRoleName(role);
                Assert.IsNotEmpty(name, $"{role}의 이름이 비어있지 않아야 합니다");
            }
        }

        [Test]
        public void GetRoleName_Soldier_ReturnsSoldier()
        {
            Assert.IsTrue(GuardStatusSystem.GetRoleName(GuardRole.Soldier).Contains("일반"));
        }

        // ===================== GetDailyDeathChance =====================

        [Test]
        public void GetDailyDeathChance_Soldier_Zero()
        {
            Assert.AreEqual(0f, GuardStatusSystem.GetDailyDeathChance(GuardRole.Soldier));
        }

        [Test]
        public void GetDailyDeathChance_Hunter_Highest()
        {
            float hunter = GuardStatusSystem.GetDailyDeathChance(GuardRole.Hunter);
            float informant = GuardStatusSystem.GetDailyDeathChance(GuardRole.Informant);
            float herbalist = GuardStatusSystem.GetDailyDeathChance(GuardRole.Herbalist);
            float miner = GuardStatusSystem.GetDailyDeathChance(GuardRole.Miner);

            Assert.Greater(hunter, informant, "사냥꾼 > 정보원 사망률");
            Assert.Greater(hunter, herbalist, "사냥꾼 > 약초꾼 사망률");
            Assert.Greater(hunter, miner, "사냥꾼 > 광부 사망률");
        }

        // ===================== GetActivityBonus =====================

        [Test]
        public void GetActivityBonus_Soldier_Default()
        {
            Assert.AreEqual(1f, GuardStatusSystem.GetActivityBonus(GuardRole.Soldier));
        }

        [Test]
        public void GetActivityBonus_SpecialRole_Higher()
        {
            Assert.AreEqual(1.5f, GuardStatusSystem.GetActivityBonus(GuardRole.Herbalist));
            Assert.AreEqual(1.5f, GuardStatusSystem.GetActivityBonus(GuardRole.Hunter));
            Assert.AreEqual(1.5f, GuardStatusSystem.GetActivityBonus(GuardRole.Informant));
            Assert.AreEqual(1.5f, GuardStatusSystem.GetActivityBonus(GuardRole.Miner));
        }

        // ===================== GetStatusSummary =====================

        [Test]
        public void GetStatusSummary_LivingGuard_ReturnsSummary()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();

            string summary = GuardStatusSystem.GetStatusSummary(guard);
            Assert.IsNotEmpty(summary, "살아있는 병사는 요약 문자열 반환");
            Assert.IsTrue(summary.Contains("❤️"), "HP 표시 포함");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void GetStatusSummary_DeadGuard_ReturnsDead()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            guard.TakeDamage(999f, Vector3.zero);

            string summary = GuardStatusSystem.GetStatusSummary(guard);
            Assert.IsTrue(summary.Contains("사망"), "죽은 병사는 '사망' 표시");

            Object.DestroyImmediate(go);
        }

        // ===================== GetRoleDescription =====================

        [Test]
        public void GetRoleDescription_All_NotEmpty()
        {
            foreach (GuardRole role in System.Enum.GetValues(typeof(GuardRole)))
            {
                string desc = GuardStatusSystem.GetRoleDescription(role);
                Assert.IsNotEmpty(desc, $"{role} 설명 필요");
            }
        }

        // ===================== CanFight =====================

        [Test]
        public void CanFight_SoldierAndHunter_True()
        {
            Assert.IsTrue(GuardStatusSystem.CanFight(GuardRole.Soldier));
            Assert.IsTrue(GuardStatusSystem.CanFight(GuardRole.Hunter));
        }

        [Test]
        public void CanFight_Others_False()
        {
            Assert.IsFalse(GuardStatusSystem.CanFight(GuardRole.Herbalist));
            Assert.IsFalse(GuardStatusSystem.CanFight(GuardRole.Informant));
            Assert.IsFalse(GuardStatusSystem.CanFight(GuardRole.Miner));
        }

        // ===================== GuardPlaceholder 연동 =====================

        [Test]
        public void GuardPlaceholder_DefaultRole_Soldier()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            Assert.AreEqual(GuardRole.Soldier, guard.Role, "기본 역할은 Soldier");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void GuardPlaceholder_Role_CanChange()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            guard.Role = GuardRole.Herbalist;
            Assert.AreEqual(GuardRole.Herbalist, guard.Role, "역할 변경 가능");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void GuardPlaceholder_StatusSummary_Works()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            Assert.IsNotEmpty(guard.StatusSummary, "StatusSummary가 비어있지 않아야 함");
            Object.DestroyImmediate(go);
        }
    }
}