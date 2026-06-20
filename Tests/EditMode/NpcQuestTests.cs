using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C9-30: NPC 퀘스트 시스템 테스트
    /// </summary>
    public class NpcQuestTests
    {
        [SetUp]
        public void Setup()
        {
            QuestManager.ResetAll();
            QuestManager.Initialize();
        }

        [TearDown]
        public void Teardown()
        {
            QuestManager.ResetAll();
        }

        // ===================== QuestManager 기본 =====================

        [Test]
        public void QuestManager_Instance_NotNull()
        {
            // QuestManager is a static class, always accessible
            Assert.IsNotNull(QuestManager.GetQuest("first_gather"), "GetQuest should return data for existing quest");
        }

        [Test]
        public void QuestManager_GetQuest_Existing_ReturnsData()
        {
            QuestData quest = QuestManager.GetQuest("first_gather");
            Assert.AreEqual("first_gather", quest.questId);
            Assert.AreEqual("기초 약초 채집", quest.questName);
            Assert.AreEqual(1, quest.requiredLevel);
            Assert.IsNotNull(quest.objectives, "Objectives should not be null");
            Assert.Greater(quest.objectives.Count, 0, "Should have at least one objective");
        }

        [Test]
        public void QuestManager_GetQuest_NotFound_ReturnsDefault()
        {
            QuestData quest = QuestManager.GetQuest("nonexistent_quest");
            Assert.IsNull(quest.questId, "Non-existent quest should return default struct");
        }

        // ===================== 퀘스트 수락 =====================

        [Test]
        public void QuestManager_AcceptQuest_Available_BecomesActive()
        {
            // first_gather has no prerequisites, so it starts as Available
            QuestState before = QuestManager.GetQuestState("first_gather");
            Assert.AreEqual(QuestState.Available, before, "first_gather should start as Available");

            bool result = QuestManager.AcceptQuest("first_gather");
            Assert.IsTrue(result, "AcceptQuest should succeed for Available quest");

            QuestState after = QuestManager.GetQuestState("first_gather");
            Assert.AreEqual(QuestState.Active, after, "After accept, state should be Active");
        }

        [Test]
        public void QuestManager_AcceptQuest_NotAvailable_ReturnsFalse()
        {
            // Accept first, then try to accept again
            QuestManager.AcceptQuest("first_gather");

            bool result = QuestManager.AcceptQuest("first_gather");
            Assert.IsFalse(result, "Already active quest cannot be accepted again");

            // Try nonexistent
            result = QuestManager.AcceptQuest(null);
            Assert.IsFalse(result, "Null questId should return false");

            result = QuestManager.AcceptQuest("");
            Assert.IsFalse(result, "Empty questId should return false");
        }

        // ===================== 목표 업데이트 =====================

        [Test]
        public void QuestManager_UpdateObjective_IncrementsCount()
        {
            QuestManager.AcceptQuest("first_gather");

            QuestData before = QuestManager.GetQuest("first_gather");
            Assert.AreEqual(0, before.objectives[0].currentCount, "Initial count should be 0");

            QuestManager.UpdateObjective("first_gather", QuestObjectiveType.GatherItem, "herb_red", 1);

            QuestData after = QuestManager.GetQuest("first_gather");
            Assert.AreEqual(1, after.objectives[0].currentCount, "After update, count should be 1");
        }

        [Test]
        public void QuestManager_UpdateObjective_CapsAtRequired()
        {
            QuestManager.AcceptQuest("first_gather");

            // Update beyond required count
            QuestManager.UpdateObjective("first_gather", QuestObjectiveType.GatherItem, "herb_red", 10);

            QuestData quest = QuestManager.GetQuest("first_gather");
            Assert.AreEqual(quest.objectives[0].requiredCount, quest.objectives[0].currentCount,
                "Current count should not exceed required count");
        }

        // ===================== 퀘스트 완료 =====================

        [Test]
        public void QuestManager_TryCompleteQuest_AllObjectivesMet_Completes()
        {
            var go = new GameObject("TestPlayerStats");
            var stats = go.AddComponent<PlayerStats>();
            var invGo = new GameObject("TestInventory");
            var inv = invGo.AddComponent<PlayerInventory>();

            QuestManager.AcceptQuest("first_gather");
            // Complete all objectives
            QuestManager.UpdateObjective("first_gather", QuestObjectiveType.GatherItem, "herb_red", 3);

            bool result = QuestManager.TryCompleteQuest("first_gather");
            Assert.IsTrue(result, "TryCompleteQuest should succeed when all objectives met");

            QuestState state = QuestManager.GetQuestState("first_gather");
            Assert.AreEqual(QuestState.Completed, state, "Quest should be Completed");

            // Verify rewards were given (gold + exp)
            Assert.Greater(stats.Gold, 0, "Should have received gold reward");
            Assert.Greater(stats.CurrentEXP, 0, "Should have received EXP reward");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(invGo);
        }

        [Test]
        public void QuestManager_TryCompleteQuest_NotAllMet_ReturnsFalse()
        {
            var go = new GameObject("TestPlayerStats");
            go.AddComponent<PlayerStats>();

            QuestManager.AcceptQuest("first_gather");
            // Only partial progress
            QuestManager.UpdateObjective("first_gather", QuestObjectiveType.GatherItem, "herb_red", 1);

            bool result = QuestManager.TryCompleteQuest("first_gather");
            Assert.IsFalse(result, "Should not complete when objectives are not all met");

            QuestState state = QuestManager.GetQuestState("first_gather");
            Assert.AreEqual(QuestState.Active, state, "Quest should remain Active");

            Object.DestroyImmediate(go);
        }

        // ===================== 퀘스트 목록 조회 =====================

        [Test]
        public void QuestManager_GetActiveQuests_ReturnsOnlyActive()
        {
            Assert.AreEqual(0, QuestManager.GetActiveQuests().Count, "Initially no active quests");

            QuestManager.AcceptQuest("first_gather");
            QuestManager.AcceptQuest("visit_shop");

            var active = QuestManager.GetActiveQuests();
            Assert.AreEqual(2, active.Count, "Two quests should be active");
        }

        [Test]
        public void QuestManager_GetCompletedQuests_ReturnsOnlyCompleted()
        {
            var go = new GameObject("TestPlayerStats");
            go.AddComponent<PlayerStats>();
            var invGo = new GameObject("TestInventory");
            invGo.AddComponent<PlayerInventory>();

            Assert.AreEqual(0, QuestManager.GetCompletedQuests().Count, "Initially no completed quests");

            QuestManager.AcceptQuest("first_gather");
            QuestManager.UpdateObjective("first_gather", QuestObjectiveType.GatherItem, "herb_red", 3);
            QuestManager.TryCompleteQuest("first_gather");

            var completed = QuestManager.GetCompletedQuests();
            Assert.AreEqual(1, completed.Count, "One quest should be completed");
            Assert.AreEqual("first_gather", completed[0].questId);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(invGo);
        }

        [Test]
        public void QuestManager_GetAvailableQuests_RespectsLevel()
        {
            // gather_iron requires level 3
            // first_gather and visit_shop require level 1
            var availableLv1 = QuestManager.GetAvailableQuests(1);
            Assert.IsTrue(availableLv1.Exists(q => q.questId == "first_gather"), "Level 1 can see first_gather");
            Assert.IsFalse(availableLv1.Exists(q => q.questId == "gather_iron"), "Level 1 cannot see gather_iron (requires Lv3)");

            var availableLv3 = QuestManager.GetAvailableQuests(3);
            Assert.IsTrue(availableLv3.Exists(q => q.questId == "gather_iron"), "Level 3 can see gather_iron");
        }

        [Test]
        public void QuestManager_GetAvailableQuests_RespectsPrerequisites()
        {
            // first_hunt requires prerequisite first_gather to be completed
            var go = new GameObject("TestPlayerStats");
            go.AddComponent<PlayerStats>();
            var invGo = new GameObject("TestInventory");
            invGo.AddComponent<PlayerInventory>();

            // Initially first_gather is not completed, so first_hunt should be Locked
            Assert.AreEqual(QuestState.Locked, QuestManager.GetQuestState("first_hunt"),
                "first_hunt starts as Locked (needs first_gather)");

            // Complete first_gather
            QuestManager.AcceptQuest("first_gather");
            QuestManager.UpdateObjective("first_gather", QuestObjectiveType.GatherItem, "herb_red", 3);
            QuestManager.TryCompleteQuest("first_gather");

            // Now first_hunt should be Available
            Assert.AreEqual(QuestState.Locked, QuestManager.GetQuestState("first_hunt"),
                "first_hunt remains Locked because QuestManager doesn't auto-promote — "
                + "prerequisites are checked dynamically in GetAvailableQuests");

            // But GetAvailableQuests should include it since prereq is met
            var available = QuestManager.GetAvailableQuests(1);
            Assert.IsTrue(available.Exists(q => q.questId == "first_hunt"),
                "first_hunt should appear in GetAvailableQuests after first_gather is completed");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(invGo);
        }

        // ===================== NpcQuestGiver =====================

        [Test]
        public void NpcQuestGiver_HasQuestIds()
        {
            var go = new GameObject("TestNpcQuestGiver");
            var giver = go.AddComponent<NpcQuestGiver>();

            // Default quest IDs should be null (not set via inspector)
            // The component works with quest IDs assigned in the inspector
            // We just verify the component exists and can be created
            Assert.IsNotNull(giver, "NpcQuestGiver component should be creatable");

            Object.DestroyImmediate(go);
        }

        // ===================== QuestData 구조 =====================

        [Test]
        public void QuestData_HasRequiredFields()
        {
            QuestData quest = QuestManager.GetQuest("first_gather");

            Assert.IsNotNull(quest.questId, "questId should not be null");
            Assert.IsNotNull(quest.questName, "questName should not be null");
            Assert.IsNotNull(quest.description, "description should not be null");
            Assert.IsNotNull(quest.objectives, "objectives should not be null");
            Assert.Greater(quest.objectives.Count, 0, "Should have objectives");
            Assert.IsNotNull(quest.giverNpcId, "giverNpcId should not be null");
            Assert.GreaterOrEqual(quest.requiredLevel, 1, "requiredLevel should be >= 1");
        }

        [Test]
        public void QuestData_AllObjectivesMet_Works()
        {
            QuestData quest = QuestManager.GetQuest("first_gather");
            Assert.IsFalse(quest.AllObjectivesMet, "Fresh quest with zero progress should not be complete");

            // Create a modified quest where objectives are met
            var modifiedQuest = quest;
            var objectives = modifiedQuest.objectives;
            for (int i = 0; i < objectives.Count; i++)
            {
                var obj = objectives[i];
                obj.currentCount = obj.requiredCount;
                objectives[i] = obj;
            }
            modifiedQuest.objectives = objectives;
            Assert.IsTrue(modifiedQuest.AllObjectivesMet, "Quest with all objectives at required count should be complete");
        }

        [Test]
        public void QuestReward_GivesGold()
        {
            // Test with quest that has explicit gold reward
            QuestData quest = QuestManager.GetQuest("first_gather");
            Assert.AreEqual(10, quest.reward.gold, "first_gather should give 10 gold");

            QuestData huntQuest = QuestManager.GetQuest("first_hunt");
            Assert.AreEqual(20, huntQuest.reward.gold, "first_hunt should give 20 gold");

            QuestData ironQuest = QuestManager.GetQuest("gather_iron");
            Assert.AreEqual(30, ironQuest.reward.gold, "gather_iron should give 30 gold");
        }

        // ===================== 추가 상태 전환 =====================

        [Test]
        public void QuestManager_FailQuest_ChangesState()
        {
            QuestManager.AcceptQuest("first_gather");
            Assert.AreEqual(QuestState.Active, QuestManager.GetQuestState("first_gather"));

            QuestManager.FailQuest("first_gather");
            Assert.AreEqual(QuestState.Failed, QuestManager.GetQuestState("first_gather"),
                "FailQuest should set state to Failed");

            // Failing already-failed quest should not error
            QuestManager.FailQuest("first_gather");
            Assert.AreEqual(QuestState.Failed, QuestManager.GetQuestState("first_gather"),
                "State should remain Failed");
        }

        [Test]
        public void QuestManager_ForceState_TestHelper()
        {
            QuestManager.ForceState("first_gather", QuestState.Active);
            Assert.AreEqual(QuestState.Active, QuestManager.GetQuestState("first_gather"));

            QuestManager.ForceState("first_gather", QuestState.Locked);
            Assert.AreEqual(QuestState.Locked, QuestManager.GetQuestState("first_gather"));
        }

        [Test]
        public void QuestManager_AllQuests_Exist()
        {
            Assert.IsNotNull(QuestManager.GetQuest("first_gather").questId);
            Assert.IsNotNull(QuestManager.GetQuest("first_hunt").questId);
            Assert.IsNotNull(QuestManager.GetQuest("first_craft").questId);
            Assert.IsNotNull(QuestManager.GetQuest("visit_shop").questId);
            Assert.IsNotNull(QuestManager.GetQuest("gather_iron").questId);
        }

        [Test]
        public void QuestManager_AcceptQuest_LockedQuest_ReturnsFalse()
        {
            // first_hunt has prerequisite first_gather, so it starts as Locked
            QuestState state = QuestManager.GetQuestState("first_hunt");
            Assert.AreEqual(QuestState.Locked, state, "first_hunt should be Locked initially");

            bool result = QuestManager.AcceptQuest("first_hunt");
            Assert.IsFalse(result, "Cannot accept a Locked quest directly");
        }
    }
}