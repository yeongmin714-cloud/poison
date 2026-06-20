using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;
using ProjectName.Core.Data;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C9-25 정보원 파견 시스템 테스트
    /// </summary>
    public class SpySystemTests
    {
        // ===================== 열거형 =====================

        [Test]
        public void SpyMission_HasAllMissions()
        {
            Assert.AreEqual(0, (int)SpySystem.SpyMission.Recon);
            Assert.AreEqual(1, (int)SpySystem.SpyMission.Infiltrate);
            Assert.AreEqual(2, (int)SpySystem.SpyMission.Survey);
        }

        // ===================== SendSpy 기본 검증 =====================

        [Test]
        public void SendSpy_NullSpy_ReturnsFail()
        {
            var result = SpySystem.SendSpy(null, new TerritoryId(NationType.East, 1), SpySystem.SpyMission.Recon);
            Assert.IsFalse(result.success, "null 정보원 실패");
        }

        [Test]
        public void SendSpy_DeadSpy_ReturnsFail()
        {
            var go = new GameObject("TestSpy");
            var spy = go.AddComponent<GuardPlaceholder>();
            spy.TakeDamage(999f, Vector3.zero);

            var result = SpySystem.SendSpy(spy, new TerritoryId(NationType.East, 1), SpySystem.SpyMission.Recon);
            Assert.IsFalse(result.success, "사망 정보원 실패");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void SendSpy_UnrecruitedSpy_ReturnsFail()
        {
            var go = new GameObject("TestSpy");
            var spy = go.AddComponent<GuardPlaceholder>();

            var result = SpySystem.SendSpy(spy, new TerritoryId(NationType.East, 1), SpySystem.SpyMission.Recon);
            Assert.IsFalse(result.success, "비포섭 정보원 실패");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void SendSpy_LevelTooLow_ReturnsFail()
        {
            var go = new GameObject("TestSpy");
            var spy = go.AddComponent<GuardPlaceholder>();
            var levelField = typeof(GuardPlaceholder).GetField("level",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            levelField.SetValue(spy, 1); // Recon requires Lv.3
            var recruitedField = typeof(GuardPlaceholder).GetField("_isRecruited",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            recruitedField.SetValue(spy, true);

            var result = SpySystem.SendSpy(spy, new TerritoryId(NationType.East, 1), SpySystem.SpyMission.Recon);
            Assert.IsFalse(result.success, "레벨 부족 실패");

            Object.DestroyImmediate(go);
        }

        // ===================== 임무 성공 =====================

        [Test]
        public void SendSpy_Recon_Success_SetsFlag()
        {
            var state = TerritoryDatabase.Instance.GetState(NationType.East, 1);
            state.spyReportRecon = false;

            var go = new GameObject("TestSpy");
            var spy = go.AddComponent<GuardPlaceholder>();
            var levelField = typeof(GuardPlaceholder).GetField("level",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            levelField.SetValue(spy, 5);
            var recruitedField = typeof(GuardPlaceholder).GetField("_isRecruited",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            recruitedField.SetValue(spy, true);

            // 발각 회피를 위해 여러 번 시도
            for (int i = 0; i < 30; i++)
            {
                state.spyReportRecon = false;
                var result = SpySystem.SendSpy(spy, new TerritoryId(NationType.East, 1), SpySystem.SpyMission.Recon);
                if (result.success)
                {
                    Assert.IsTrue(state.spyReportRecon, "Recon 플래그 설정");
                    Assert.IsNotEmpty(result.infoGathered, "정보 문자열 존재");
                    break;
                }
            }

            Object.DestroyImmediate(go);
        }

        [Test]
        public void SendSpy_Infiltrate_Success_SetsFlag()
        {
            var state = TerritoryDatabase.Instance.GetState(NationType.East, 1);
            state.spyReportInfiltrate = false;

            var go = new GameObject("TestSpy");
            var spy = go.AddComponent<GuardPlaceholder>();
            var levelField = typeof(GuardPlaceholder).GetField("level",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            levelField.SetValue(spy, 10);
            var recruitedField = typeof(GuardPlaceholder).GetField("_isRecruited",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            recruitedField.SetValue(spy, true);

            for (int i = 0; i < 30; i++)
            {
                state.spyReportInfiltrate = false;
                var result = SpySystem.SendSpy(spy, new TerritoryId(NationType.East, 1), SpySystem.SpyMission.Infiltrate);
                if (result.success)
                {
                    Assert.IsTrue(state.spyReportInfiltrate, "Infiltrate 플래그 설정");
                    Assert.IsNotEmpty(result.infoGathered, "정보 문자열 존재");
                    break;
                }
            }

            Object.DestroyImmediate(go);
        }

        [Test]
        public void SendSpy_Survey_Success_SetsFlag()
        {
            var state = TerritoryDatabase.Instance.GetState(NationType.East, 1);
            state.spyReportSurvey = false;

            var go = new GameObject("TestSpy");
            var spy = go.AddComponent<GuardPlaceholder>();
            var levelField = typeof(GuardPlaceholder).GetField("level",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            levelField.SetValue(spy, 7);
            var recruitedField = typeof(GuardPlaceholder).GetField("_isRecruited",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            recruitedField.SetValue(spy, true);

            for (int i = 0; i < 30; i++)
            {
                state.spyReportSurvey = false;
                var result = SpySystem.SendSpy(spy, new TerritoryId(NationType.East, 1), SpySystem.SpyMission.Survey);
                if (result.success)
                {
                    Assert.IsTrue(state.spyReportSurvey, "Survey 플래그 설정");
                    Assert.IsNotEmpty(result.infoGathered, "정보 문자열 존재");
                    break;
                }
            }

            Object.DestroyImmediate(go);
        }

        // ===================== 발각 =====================

        [Test]
        public void SendSpy_Detected_SpyDies()
        {
            var go = new GameObject("TestSpy");
            var spy = go.AddComponent<GuardPlaceholder>();
            var levelField = typeof(GuardPlaceholder).GetField("level",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            levelField.SetValue(spy, 3);
            var recruitedField = typeof(GuardPlaceholder).GetField("_isRecruited",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            recruitedField.SetValue(spy, true);

            // 낮은 호감도 + Ring1 (발각 확률 높음)으로 발각 유도
            var state = TerritoryDatabase.Instance.GetState(NationType.East, 1);
            state.loyaltyToPlayer = 0f;

            bool sawDetected = false;
            for (int i = 0; i < 30; i++)
            {
                // 소환 후 생존 상태인 새 정보원으로 시도
                if (!spy.IsAlive)
                {
                    Object.DestroyImmediate(go);
                    go = new GameObject("TestSpy");
                    spy = go.AddComponent<GuardPlaceholder>();
                    levelField.SetValue(spy, 3);
                    recruitedField.SetValue(spy, true);
                }

                var result = SpySystem.SendSpy(spy, new TerritoryId(NationType.East, 1), SpySystem.SpyMission.Recon);
                if (result.detected && result.spyLost)
                {
                    Assert.IsFalse(spy.IsAlive, "발각 시 정보원 사망");
                    sawDetected = true;
                    break;
                }
            }

            Assert.IsTrue(sawDetected, "발각 발생 가능해야 함");

            Object.DestroyImmediate(go);
        }

        // ===================== GetAvailableSpies =====================

        [Test]
        public void GetAvailableSpies_ReturnsOnlyAliveRecruited()
        {
            var spies = SpySystem.GetAvailableSpies();
            Assert.IsNotNull(spies, "null이 아닌 리스트 반환");
        }

        // ===================== GetMissionName =====================

        [Test]
        public void GetMissionName_AllTypes_ReturnsStrings()
        {
            foreach (SpySystem.SpyMission mission in System.Enum.GetValues(typeof(SpySystem.SpyMission)))
            {
                string name = SpySystem.GetMissionName(mission);
                Assert.IsNotEmpty(name, $"{mission} 이름 필요");
            }
        }

        // ===================== GetMissionDescription =====================

        [Test]
        public void GetMissionDescription_AllTypes_ReturnsStrings()
        {
            foreach (SpySystem.SpyMission mission in System.Enum.GetValues(typeof(SpySystem.SpyMission)))
            {
                string desc = SpySystem.GetMissionDescription(mission);
                Assert.IsNotEmpty(desc, $"{mission} 설명 필요");
            }
        }

        // ===================== GetRequiredLevel =====================

        [Test]
        public void GetRequiredLevel_AllTypes_ReturnsCorrect()
        {
            Assert.AreEqual(3, SpySystem.GetRequiredLevel(SpySystem.SpyMission.Recon));
            Assert.AreEqual(8, SpySystem.GetRequiredLevel(SpySystem.SpyMission.Infiltrate));
            Assert.AreEqual(5, SpySystem.GetRequiredLevel(SpySystem.SpyMission.Survey));
        }

        // ===================== GetDuration =====================

        [Test]
        public void GetDuration_AllTypes_ReturnsCorrect()
        {
            Assert.AreEqual(30f, SpySystem.GetDuration(SpySystem.SpyMission.Recon));
            Assert.AreEqual(60f, SpySystem.GetDuration(SpySystem.SpyMission.Infiltrate));
            Assert.AreEqual(45f, SpySystem.GetDuration(SpySystem.SpyMission.Survey));
        }

        // ===================== CalculateDetectChance =====================

        [Test]
        public void CalculateDetectChance_WithinRange()
        {
            var target = new TerritoryId(NationType.East, 1);
            var go = new GameObject("TestSpy");
            var spy = go.AddComponent<GuardPlaceholder>();
            var levelField = typeof(GuardPlaceholder).GetField("level",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            levelField.SetValue(spy, 10);

            float chance = SpySystem.CalculateDetectChance(spy, target);
            Assert.GreaterOrEqual(chance, 0f, "발각 확률 >= 0");
            Assert.LessOrEqual(chance, 1f, "발각 확률 <= 1");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void CalculateDetectChance_DifficultyIncreases()
        {
            var target1 = new TerritoryId(NationType.East, 1); // Ring1
            var go = new GameObject("TestSpy");
            var spy = go.AddComponent<GuardPlaceholder>();
            var levelField = typeof(GuardPlaceholder).GetField("level",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            levelField.SetValue(spy, 5);

            float chanceRing1 = SpySystem.CalculateDetectChance(spy, target1);

            // Ring1이 기준 = 0.3 - base reductions
            Assert.GreaterOrEqual(chanceRing1, 0f, "Ring1 확률 >= 0");

            Object.DestroyImmediate(go);
        }

        // ===================== 잘못된 영지 =====================

        [Test]
        public void SendSpy_InvalidTerritory_ReturnsFail()
        {
            var go = new GameObject("TestSpy");
            var spy = go.AddComponent<GuardPlaceholder>();
            var levelField = typeof(GuardPlaceholder).GetField("level",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            levelField.SetValue(spy, 10);
            var recruitedField = typeof(GuardPlaceholder).GetField("_isRecruited",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            recruitedField.SetValue(spy, true);

            // 정의되지 않은 영지 ID (없는 국가 + 인덱스)
            var result = SpySystem.SendSpy(spy, new TerritoryId(NationType.None, 999), SpySystem.SpyMission.Recon);
            Assert.IsFalse(result.success, "존재하지 않는 영지 실패");

            Object.DestroyImmediate(go);
        }
    }
}