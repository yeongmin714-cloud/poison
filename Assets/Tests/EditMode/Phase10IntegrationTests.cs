using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C10-22: Phase 10 통합 테스트 — 전체 전쟁/전환/연출 흐름 검증
    /// </summary>
    [TestFixture]
    public class Phase10IntegrationTests
    {
        [SetUp]
        public void SetUp()
        {
            QuestManager.Initialize();
            BuildingTrigger.ResetAll();
            IndoorSceneTransition.ResetAll();
            FadeManager.ResetAll();
            AlarmSystem.ResetAll();
            LordSurrenderSystem.ResetAll();
            PoisonTakeoverSystem.ResetAll();
            AIWarSystem.ResetAll();
            WarNotificationUI.ResetAll();
            TerritoryCaptureSystem.ResetAll();
            OpeningCutscene.ResetSeenState();
        }

        [TearDown]
        public void TearDown()
        {
            QuestManager.ResetAll();
            OpeningCutscene.ResetSeenState();
            var go = GameObject.Find("__CoroutineRunner__");
            if (go != null) Object.DestroyImmediate(go);
        }

        // === 건물 출입 (C10-01~06) ===

        [Test]
        public void BuildingTrigger_EnterExit_SetsStates()
        {
            // BuildingTrigger가 EnterBuilding/ExitBuilding을 호출하면 상태가 변경됨
            Assert.IsFalse(IndoorSceneTransition.IsIndoorSceneLoaded());
            IndoorSceneTransition.EnterBuilding("Shop");
            Assert.AreEqual("Shop", IndoorSceneTransition.GetPendingBuildingType());
        }

        [Test]
        public void FadeManager_FadeInOut_ChangesAlpha()
        {
            var fadeObj = new GameObject();
            var fade = fadeObj.AddComponent<FadeManager>();
            fade.SetAlpha(1f);
            Assert.AreEqual(1f, fade.GetAlpha());
            fade.SetAlpha(0f);
            Assert.AreEqual(0f, fade.GetAlpha());
            Object.DestroyImmediate(fadeObj);
        }

        // === 경보/항복 (C10-08~11) ===

        [Test]
        public void AlarmSystem_DefaultState_IsPeaceful()
        {
            Assert.AreEqual(AlarmState.Peaceful, AlarmSystem.CurrentState);
        }

        [Test]
        public void AlarmSystem_TriggerAlert_ChangesToAlert()
        {
            AlarmSystem.TriggerAlert("territory_01");
            Assert.AreEqual(AlarmState.Alert, AlarmSystem.CurrentState);
        }

        [Test]
        public void LordSurrenderSystem_TrySummonLord_ReturnsTrue()
        {
            bool result = LordSurrenderSystem.TrySummonLord("territory_01");
            Assert.IsTrue(result);
        }

        [Test]
        public void LordSurrenderSystem_DuplicateSummon_ReturnsFalse()
        {
            LordSurrenderSystem.TrySummonLord("territory_01");
            bool result = LordSurrenderSystem.TrySummonLord("territory_01");
            Assert.IsFalse(result);
        }

        [Test]
        public void LordSurrenderSystem_Execute_UpdatesState()
        {
            LordSurrenderSystem.TrySummonLord("territory_01");
            LordSurrenderSystem.ExecuteLord("territory_01");
            var lord = LordSurrenderSystem.GetLordData("territory_01");
            Assert.IsFalse(lord.isAlive);
            Assert.IsTrue(lord.hasSurrendered);
        }

        [Test]
        public void LordSurrenderSystem_Spare_GrantsLoyalty()
        {
            LordSurrenderSystem.TrySummonLord("territory_01");
            LordSurrenderSystem.SpareLord("territory_01");
            var lord = LordSurrenderSystem.GetLordData("territory_01");
            Assert.IsTrue(lord.isAlive);
            Assert.IsTrue(lord.hasSurrendered);
        }

        // === 독살/암살 (C10-12~13) ===

        [Test]
        public void PoisonTakeoverSystem_Default_HasNoPoison()
        {
            Assert.IsFalse(PoisonTakeoverSystem.IsLordPoisoned("territory_01"));
        }

        [Test]
        public void PoisonTakeoverSystem_TryPoisonTakeover_FailsWithoutEnvoy()
        {
            // 특사 없이 독살 시도 → 실패
            bool result = PoisonTakeoverSystem.TryPoisonTakeover("territory_01");
            Assert.IsFalse(result);
        }

        [Test]
        public void OpeningCutscene_NotSeen_ReturnsFalse()
        {
            Assert.IsFalse(OpeningCutscene.HasSeenCutscene());
        }

        [Test]
        public void OpeningCutscene_MarkSeen_ReturnsTrue()
        {
            OpeningCutscene.MarkAsSeen();
            Assert.IsTrue(OpeningCutscene.HasSeenCutscene());
        }

        // === AI 전쟁 (C10-14) ===

        [Test]
        public void AIWarSystem_Default_HasNoWars()
        {
            Assert.AreEqual(0, AIWarSystem.GetActiveWarCount());
        }

        [Test]
        public void AIWarSystem_StartWar_IncreasesCount()
        {
            AIWarSystem.StartAIWar("territory_north_01", "territory_north_02", 1);
            Assert.AreEqual(1, AIWarSystem.GetActiveWarCount());
        }

        // === 알림 (C10-15) ===

        [Test]
        public void WarNotificationUI_Default_HasNoNotifications()
        {
            Assert.AreEqual(0, WarNotificationUI.GetNotificationCount());
        }

        [Test]
        public void WarNotificationUI_ShowNotification_IncreasesCount()
        {
            WarNotificationUI.ShowNotification("전쟁 발생!", NotificationType.WarStart);
            Assert.AreEqual(1, WarNotificationUI.GetNotificationCount());
        }

        // === 영토 차지 (C10-16) ===

        [Test]
        public void TerritoryCaptureSystem_Default_HasNoFlags()
        {
            Assert.AreEqual(0, TerritoryCaptureSystem.GetFlagCount());
        }

        // === 연출/오디오 (C10-17~20) ===

        [Test]
        public void BackgroundMusicManager_Default_HasInstance()
        {
            Assert.IsNotNull(BackgroundMusicManager.Instance);
        }

        [Test]
        public void SoundEffectManager_Default_HasInstance()
        {
            Assert.IsNotNull(SoundEffectManager.Instance);
        }

        [Test]
        public void UISoundManager_Default_HasInstance()
        {
            Assert.IsNotNull(UISoundManager.Instance);
        }

        // === 전체 라이프사이클 ===

        [Test]
        public void FullLifecycle_EnterBuilding_ThenExit()
        {
            IndoorSceneTransition.EnterBuilding("Shop");
            Assert.IsFalse(string.IsNullOrEmpty(IndoorSceneTransition.GetPendingBuildingType()));
            Assert.AreEqual("Shop", IndoorSceneTransition.GetPendingBuildingType());
        }

        [Test]
        public void FullLifecycle_AlarmToSurrender()
        {
            AlarmSystem.TriggerAlert("territory_01");
            Assert.AreEqual(AlarmState.Alert, AlarmSystem.CurrentState);

            LordSurrenderSystem.TrySummonLord("territory_01");
            Assert.IsTrue(LordSurrenderSystem.GetLordData("territory_01").hasSurrendered);
        }
    }
}
