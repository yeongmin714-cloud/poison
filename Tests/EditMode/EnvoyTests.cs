using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;
using ProjectName.Core.Data;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C9-24 특사 파견 시스템 테스트
    /// </summary>
    public class EnvoyTests
    {
        // ===================== 열거형 =====================

        [Test]
        public void EnvoyMission_HasAllMissions()
        {
            Assert.AreEqual(0, (int)EnvoySystem.EnvoyMission.Gift);
            Assert.AreEqual(1, (int)EnvoySystem.EnvoyMission.Friendship);
            Assert.AreEqual(2, (int)EnvoySystem.EnvoyMission.Alliance);
            Assert.AreEqual(3, (int)EnvoySystem.EnvoyMission.Assassinate);
        }

        // ===================== GetRequiredLevel =====================

        [Test]
        public void GetRequiredLevel_Gift_Level5()
        {
            Assert.AreEqual(5, EnvoySystem.GetRequiredLevel(EnvoySystem.EnvoyMission.Gift));
        }

        [Test]
        public void GetRequiredLevel_Friendship_Level10()
        {
            Assert.AreEqual(10, EnvoySystem.GetRequiredLevel(EnvoySystem.EnvoyMission.Friendship));
        }

        [Test]
        public void GetRequiredLevel_Assassinate_Level15()
        {
            Assert.AreEqual(15, EnvoySystem.GetRequiredLevel(EnvoySystem.EnvoyMission.Assassinate));
        }

        // ===================== SendEnvoy 기본 검증 =====================

        [Test]
        public void SendEnvoy_NullEnvoy_Fails()
        {
            var result = EnvoySystem.SendEnvoy(null, new TerritoryId(NationType.East, 1), EnvoySystem.EnvoyMission.Gift);
            Assert.IsFalse(result.success, "null 특사 실패");
        }

        [Test]
        public void SendEnvoy_DeadEnvoy_Fails()
        {
            var go = new GameObject("TestEnvoy");
            var envoy = go.AddComponent<GuardPlaceholder>();
            envoy.TakeDamage(999f, Vector3.zero);

            var result = EnvoySystem.SendEnvoy(envoy, new TerritoryId(NationType.East, 1), EnvoySystem.EnvoyMission.Gift);
            Assert.IsFalse(result.success, "사망 특사 실패");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void SendEnvoy_NotRecruited_Fails()
        {
            var go = new GameObject("TestEnvoy");
            var envoy = go.AddComponent<GuardPlaceholder>();

            var result = EnvoySystem.SendEnvoy(envoy, new TerritoryId(NationType.East, 1), EnvoySystem.EnvoyMission.Gift);
            Assert.IsFalse(result.success, "비포섭 특사 실패");

            Object.DestroyImmediate(go);
        }

        // ===================== 선물 전달 =====================

        [Test]
        public void SendEnvoy_Gift_Success()
        {
            var go = new GameObject("TestEnvoy");
            var envoy = go.AddComponent<GuardPlaceholder>();
            // 특사는 포섭 + 생존 + 최소 레벨 필요
            // SetGuardInfo로는 레벨 설정이 안 되므로 직접 설정
            var levelField = typeof(GuardPlaceholder).GetField("level",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            levelField.SetValue(envoy, 10);
            var recruitedField = typeof(GuardPlaceholder).GetField("_isRecruited",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            recruitedField.SetValue(envoy, true);

            var target = new TerritoryId(NationType.East, 1);
            var result = EnvoySystem.SendEnvoy(envoy, target, EnvoySystem.EnvoyMission.Gift);
            Assert.IsTrue(result.success, "선물 전달 성공");
            Assert.GreaterOrEqual(result.loyaltyChange, 5, "호감도 +5 이상");

            Object.DestroyImmediate(go);
        }

        // ===================== 독살 =====================

        [Test]
        public void SendEnvoy_Assassinate_ReturnsResult()
        {
            var go = new GameObject("TestEnvoy");
            var envoy = go.AddComponent<GuardPlaceholder>();
            var levelField = typeof(GuardPlaceholder).GetField("level",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            levelField.SetValue(envoy, 20);
            var recruitedField = typeof(GuardPlaceholder).GetField("_isRecruited",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            recruitedField.SetValue(envoy, true);

            var target = new TerritoryId(NationType.East, 1);

            // 여러 번 시도하여 성공/실패/발각 다양하게 확인
            bool sawSuccess = false, sawFail = false;
            for (int i = 0; i < 20; i++)
            {
                var result = EnvoySystem.SendEnvoy(envoy, target, EnvoySystem.EnvoyMission.Assassinate);
                if (result.success) sawSuccess = true;
                if (!result.success) sawFail = true;
            }

            Assert.IsTrue(sawSuccess, "독살 성공 가능");
            Assert.IsTrue(sawFail, "독살 실패(발각) 가능");

            Object.DestroyImmediate(go);
        }

        // ===================== GetMissionName =====================

        [Test]
        public void GetMissionName_All_NotEmpty()
        {
            foreach (EnvoySystem.EnvoyMission mission in System.Enum.GetValues(typeof(EnvoySystem.EnvoyMission)))
            {
                string name = EnvoySystem.GetMissionName(mission);
                Assert.IsNotEmpty(name, $"{mission} 이름 필요");
            }
        }

        // ===================== GetAvailableEnvoys =====================

        [Test]
        public void GetAvailableEnvoys_EmptyByDefault()
        {
            var envoys = EnvoySystem.GetAvailableEnvoys();
            Assert.IsNotNull(envoys, "null이 아닌 리스트 반환");
        }

        // ===================== CalculateDetectChance =====================

        [Test]
        public void CalculateDetectChance_WithinRange()
        {
            var target = new TerritoryId(NationType.East, 1);
            var go = new GameObject("TestEnvoy");
            var envoy = go.AddComponent<GuardPlaceholder>();

            float chance = EnvoySystem.CalculateDetectChance(envoy, target);
            Assert.GreaterOrEqual(chance, 0f, "발각 확률 >= 0");
            Assert.LessOrEqual(chance, 1f, "발각 확률 <= 1");

            Object.DestroyImmediate(go);
        }

        // ===================== GetMissionDescription =====================

        [Test]
        public void GetMissionDescription_All_NotEmpty()
        {
            foreach (EnvoySystem.EnvoyMission mission in System.Enum.GetValues(typeof(EnvoySystem.EnvoyMission)))
            {
                string desc = EnvoySystem.GetMissionDescription(mission);
                Assert.IsNotEmpty(desc, $"{mission} 설명 필요");
            }
        }
    }
}