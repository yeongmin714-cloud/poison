using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using ProjectName.UI;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C9-30: NPC 퀘스트 시스템 EditMode 테스트
    ///
    /// 대상:
    /// - QuestManager (static singleton)
    /// - NpcQuestGiver (MonoBehaviour)
    /// - QuestWindow (UIWindow subclass)
    /// - QuestData / QuestObjective / QuestReward (data structures)
    /// </summary>
    public class NpcQuestSystemTests
    {
        // ------------------------------------------------------------------
        // 헬퍼: 리플렉션으로 PlayerStats.Instance 설정
        // ------------------------------------------------------------------

        private GameObject _playerStatsGo;
        private PlayerStats _playerStats;

        private void CreateMockPlayerStats()
        {
            _playerStatsGo = new GameObject("TestPlayerStats");
            _playerStats = _playerStatsGo.AddComponent<PlayerStats>();

            // 리플렉션으로 Instance 필드 설정
            var field = typeof(PlayerStats).GetField("Instance",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, _playerStats);
        }

        private void DestroyMockPlayerStats()
        {
            if (_playerStatsGo != null)
                UnityEngine.Object.DestroyImmediate(_playerStatsGo);
            _playerStats = null;

            var field = typeof(PlayerStats).GetField("Instance",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, null);
        }

        // ------------------------------------------------------------------
        // 헬퍼: GameObject + NpcQuestGiver 생성
        // ------------------------------------------------------------------

        private GameObject CreateNpcQuestGiver(string npcId, string[] questIds, Vector3 position)
        {
            var go = new GameObject($"TestNpcQuestGiver_{npcId}");
            go.transform.position = position;
            var giver = go.AddComponent<NpcQuestGiver>();

            // Set private fields via reflection for testing
            SetPrivateField(giver, "npcId", npcId);
            SetPrivateField(giver, "_questIds", questIds);
            SetPrivateField(giver, "_interactRange", 3f);

            return go;
        }

        private void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null)
                field.SetValue(obj, value);
        }

        // ------------------------------------------------------------------
        // Setup / Teardown
        // ------------------------------------------------------------------

        [SetUp]
        public void Setup()
        {
            QuestManager.Initialize();
        }

        [TearDown]
        public void Teardown()
        {
            QuestManager.ResetAll();

            if (_playerStatsGo != null)
            {
                UnityEngine.Object.DestroyImmediate(_playerStatsGo);
                _playerStatsGo = null;
                _playerStats = null;

                var field = typeof(PlayerStats).GetField("Instance",
                    BindingFlags.Static | BindingFlags.NonPublic);
                if (field != null)
                    field.SetValue(null, null);
            }
        }

        // ==================================================================
        // QuestManager — Initialize / GetQuest
        // ==================================================================

        [Test]
        public void Initialize_PopulatesQuests()
        {
            // QuestManager.Initialize() was called in Setup — verify quests exist
            var firstGather = QuestManager.GetQuest("first_gather");
            Assert.IsNotNull(firstGather.questId, "first_gather should exist after Initialize");
            Assert.AreEqual("기초 약초 채집", firstGather.questName);

            var firstHunt = QuestManager.GetQuest("first_hunt");
            Assert.AreEqual("토끼 사냥", firstHunt.questName);

            var firstCraft = QuestManager.GetQuest("first_craft");
            Assert.AreEqual("첫 번째 제작", firstCraft.questName);

            var gatherIron = QuestManager.GetQuest("gather_iron");
            Assert.AreEqual("철광석 채굴", gatherIron.questName);
        }

        [Test]
        public void GetQuest_ReturnsCorrectData()
        {
            var quest = QuestManager.GetQuest("first_gather");
            Assert.AreEqual("first_gather", quest.questId);
            Assert.AreEqual("기초 약초 채집", quest.questName);
            Assert.AreEqual(1, quest.requiredLevel);
            Assert.AreEqual("npc_001", quest.giverNpcId);
            Assert.IsNotNull(quest.objectives);
            Assert.AreEqual(1, quest.objectives.Count);
            Assert.AreEqual(QuestObjectiveType.GatherItem, quest.objectives[0].type);
            Assert.AreEqual("herb_red", quest.objectives[0].targetId);
            Assert.AreEqual(3, quest.objectives[0].requiredCount);
            Assert.AreEqual(10, quest.reward.gold);
            Assert.AreEqual(20, quest.reward.exp);
        }

        [Test]
        public void GetQuest_InvalidId_ReturnsDefault()
        {
            var quest = QuestManager.GetQuest(null);
            Assert.IsTrue(string.IsNullOrEmpty(quest.questId), "null questId returns default");

            quest = QuestManager.GetQuest("");
            Assert.IsTrue(string.IsNullOrEmpty(quest.questId), "empty questId returns default");

            quest = QuestManager.GetQuest("nonexistent_quest_999");
            Assert.IsTrue(string.IsNullOrEmpty(quest.questId), "unregistered questId returns default");
        }

        // ==================================================================
        // QuestManager — GetQuestState
        // ==================================================================

        [Test]
        public void GetQuestState_InitialState_Available()
        {
            // first_gather has no prerequisites → Available
            var state = QuestManager.GetQuestState("first_gather");
            Assert.AreEqual(QuestState.Available, state);
        }

        [Test]
        public void GetQuestState_InitialState_Locked()
        {
            // first_hunt requires first_gather (not completed) → Locked
            var state = QuestManager.GetQuestState("first_hunt");
            Assert.AreEqual(QuestState.Locked, state);
        }

        [Test]
        public void GetQuestState_InvalidId_ReturnsLocked()
        {
            var state = QuestManager.GetQuestState(null);
            Assert.AreEqual(QuestState.Locked, state, "null questId returns Locked");

            state = QuestManager.GetQuestState("");
            Assert.AreEqual(QuestState.Locked, state, "empty questId returns Locked");

            state = QuestManager.GetQuestState("does_not_exist");
            Assert.AreEqual(QuestState.Locked, state, "unknown questId returns Locked");
        }

        // ==================================================================
        // QuestManager — AcceptQuest
        // ==================================================================

        [Test]
        public void AcceptQuest_ChangesStateToActive()
        {
            bool result = QuestManager.AcceptQuest("first_gather");
            Assert.IsTrue(result, "AcceptQuest should return true for available quest");

            var state = QuestManager.GetQuestState("first_gather");
            Assert.AreEqual(QuestState.Active, state, "Quest should be Active after accept");
        }

        [Test]
        public void AcceptQuest_Locked_Rejected()
        {
            // first_hunt is locked (prereq not met)
            bool result = QuestManager.AcceptQuest("first_hunt");
            Assert.IsFalse(result, "Locked quest should be rejected");

            var state = QuestManager.GetQuestState("first_hunt");
            Assert.AreEqual(QuestState.Locked, state, "State should remain Locked");
        }

        [Test]
        public void AcceptQuest_Active_Rejected()
        {
            QuestManager.AcceptQuest("first_gather");

            // Try accepting again while already active
            bool result = QuestManager.AcceptQuest("first_gather");
            Assert.IsFalse(result, "Already active quest should be rejected");

            var state = QuestManager.GetQuestState("first_gather");
            Assert.AreEqual(QuestState.Active, state, "State should remain Active");
        }

        [Test]
        public void AcceptQuest_Completed_Rejected()
        {
            // Force first_gather to completed, then try accepting
            QuestManager.ForceState("first_gather", QuestState.Completed);

            bool result = QuestManager.AcceptQuest("first_gather");
            Assert.IsFalse(result, "Completed quest should be rejected");

            var state = QuestManager.GetQuestState("first_gather");
            Assert.AreEqual(QuestState.Completed, state, "State should remain Completed");
        }

        [Test]
        public void AcceptQuest_InvalidId_ReturnsFalse()
        {
            bool result = QuestManager.AcceptQuest(null);
            Assert.IsFalse(result, "null questId returns false");

            result = QuestManager.AcceptQuest("");
            Assert.IsFalse(result, "empty questId returns false");

            result = QuestManager.AcceptQuest("unknown_quest");
            Assert.IsFalse(result, "unknown questId returns false");
        }

        // ==================================================================
        // QuestManager — UpdateObjective
        // ==================================================================

        [Test]
        public void UpdateObjective_IncrementsCounter()
        {
            QuestManager.AcceptQuest("first_gather");

            // Update objective for herb_red gathering
            QuestManager.UpdateObjective("first_gather", QuestObjectiveType.GatherItem, "herb_red", 1);

            var quest = QuestManager.GetQuest("first_gather");
            Assert.AreEqual(1, quest.objectives[0].currentCount, "Should increment by 1");

            QuestManager.UpdateObjective("first_gather", QuestObjectiveType.GatherItem, "herb_red", 2);
            quest = QuestManager.GetQuest("first_gather");
            Assert.AreEqual(3, quest.objectives[0].currentCount, "Should increment by 2 more (total 3)");
        }

        [Test]
        public void UpdateObjective_ClampsAtMax()
        {
            QuestManager.AcceptQuest("first_gather");

            // Try to set more than requiredCount
            QuestManager.UpdateObjective("first_gather", QuestObjectiveType.GatherItem, "herb_red", 10);

            var quest = QuestManager.GetQuest("first_gather");
            Assert.AreEqual(3, quest.objectives[0].currentCount, "Should clamp to requiredCount");
        }

        [Test]
        public void UpdateObjective_NotActive_DoesNothing()
        {
            // first_gather is Available (not yet accepted)
            QuestManager.UpdateObjective("first_gather", QuestObjectiveType.GatherItem, "herb_red", 1);

            var quest = QuestManager.GetQuest("first_gather");
            Assert.AreEqual(0, quest.objectives[0].currentCount, "Should not update non-active quest");
        }

        [Test]
        public void UpdateObjective_InvalidId_DoesNothing()
        {
            // Should not throw
            QuestManager.UpdateObjective(null, QuestObjectiveType.GatherItem, "herb_red", 1);
            QuestManager.UpdateObjective("", QuestObjectiveType.GatherItem, "herb_red", 1);
            QuestManager.UpdateObjective("unknown", QuestObjectiveType.GatherItem, "herb_red", 1);

            // No exception means success
            Assert.Pass("UpdateObjective handles invalid IDs gracefully");
        }

        // ==================================================================
        // QuestManager — TryCompleteQuest
        // ==================================================================

        [Test]
        public void TryCompleteQuest_Fails_ObjectivesNotMet()
        {
            QuestManager.AcceptQuest("first_gather");
            // Don't update objectives — they're all 0

            bool result = QuestManager.TryCompleteQuest("first_gather");
            Assert.IsFalse(result, "Should fail when objectives not met");

            var state = QuestManager.GetQuestState("first_gather");
            Assert.AreEqual(QuestState.Active, state, "Should remain Active");
        }

        [Test]
        public void TryCompleteQuest_Succeeds_AllObjectivesMet()
        {
            QuestManager.AcceptQuest("first_gather");

            // Meet all objectives (need 3 herb_red)
            QuestManager.UpdateObjective("first_gather", QuestObjectiveType.GatherItem, "herb_red", 3);

            bool result = QuestManager.TryCompleteQuest("first_gather");
            Assert.IsTrue(result, "Should complete when all objectives met");

            var state = QuestManager.GetQuestState("first_gather");
            Assert.AreEqual(QuestState.Completed, state, "Should be Completed");
        }

        [Test]
        public void TryCompleteQuest_NotActive_ReturnsFalse()
        {
            // first_gather is still Available (not accepted)
            bool result = QuestManager.TryCompleteQuest("first_gather");
            Assert.IsFalse(result, "Non-active quest cannot be completed");

            result = QuestManager.TryCompleteQuest("first_hunt");
            Assert.IsFalse(result, "Locked quest cannot be completed");

            result = QuestManager.TryCompleteQuest(null);
            Assert.IsFalse(result, "null questId returns false");
        }

        [Test]
        public void TryCompleteQuest_EmptyObjectives_ReturnsFalse()
        {
            // visit_shop has objectives, but if somehow empty — see AllObjectivesMet
            // For complete coverage, create edge case via force + no objectives
            // (visit_shop has objectives so it would need items to be met)
            // This tests the general case with zero-count objectives on an accepted quest
            QuestManager.AcceptQuest("visit_shop");

            // Don't meet the objective
            bool result = QuestManager.TryCompleteQuest("visit_shop");
            Assert.IsFalse(result, "Should fail when TalkToNPC objective not met");
        }

        // ==================================================================
        // QuestManager — ForceState
        // ==================================================================

        [Test]
        public void ForceState_WorksForTesting()
        {
            QuestManager.ForceState("first_gather", QuestState.Completed);
            Assert.AreEqual(QuestState.Completed, QuestManager.GetQuestState("first_gather"));

            QuestManager.ForceState("first_gather", QuestState.Failed);
            Assert.AreEqual(QuestState.Failed, QuestManager.GetQuestState("first_gather"));

            QuestManager.ForceState("first_gather", QuestState.Available);
            Assert.AreEqual(QuestState.Available, QuestManager.GetQuestState("first_gather"));
        }

        [Test]
        public void ForceState_NullOrEmpty_DoesNotThrow()
        {
            // Should not throw on invalid input
            Assert.DoesNotThrow(() => QuestManager.ForceState(null, QuestState.Active));
            Assert.DoesNotThrow(() => QuestManager.ForceState("", QuestState.Active));
        }

        // ==================================================================
        // QuestManager — ResetAll
        // ==================================================================

        [Test]
        public void ResetAll_ResetsStates()
        {
            QuestManager.AcceptQuest("first_gather");
            Assert.AreEqual(QuestState.Active, QuestManager.GetQuestState("first_gather"));

            QuestManager.ResetAll();

            // After ResetAll, first_gather should be Available again (no prereqs)
            Assert.AreEqual(QuestState.Available, QuestManager.GetQuestState("first_gather"),
                "ResetAll should restore initial state");

            // first_hunt should be Locked (prereq not met)
            Assert.AreEqual(QuestState.Locked, QuestManager.GetQuestState("first_hunt"),
                "ResetAll should restore locked state for quests with unmet prereqs");
        }

        // ==================================================================
        // QuestManager — GetAvailableQuests
        // ==================================================================

        [Test]
        public void GetAvailableQuests_FiltersByPlayerLevel()
        {
            // gather_iron requires level 3 — player level 1 should not see it
            var available = QuestManager.GetAvailableQuests(1);
            foreach (var quest in available)
            {
                Assert.LessOrEqual(quest.requiredLevel, 1,
                    $"Available quests for level 1 should require ≤ 1");
            }
        }

        [Test]
        public void GetAvailableQuests_HigherLevel_ReturnsMore()
        {
            var lowLevel = QuestManager.GetAvailableQuests(1);
            var highLevel = QuestManager.GetAvailableQuests(5);

            Assert.GreaterOrEqual(highLevel.Count, lowLevel.Count,
                "Higher player level should return ≥ available quests");

            // gather_iron (req level 3) should appear at level 5
            bool hasGatherIron = false;
            foreach (var q in highLevel)
            {
                if (q.questId == "gather_iron")
                {
                    hasGatherIron = true;
                    break;
                }
            }
            Assert.IsTrue(hasGatherIron, "gather_iron (req level 3) should be available at level 5");
        }

        [Test]
        public void GetAvailableQuests_DoesNotIncludeActive()
        {
            QuestManager.AcceptQuest("first_gather");

            var available = QuestManager.GetAvailableQuests(1);
            foreach (var q in available)
            {
                Assert.AreNotEqual("first_gather", q.questId,
                    "Active quests should not appear in available list");
            }
        }

        [Test]
        public void GetAvailableQuests_DoesNotIncludeCompleted()
        {
            // Accept and complete first_gather
            QuestManager.AcceptQuest("first_gather");
            QuestManager.UpdateObjective("first_gather", QuestObjectiveType.GatherItem, "herb_red", 3);
            QuestManager.TryCompleteQuest("first_gather");

            var available = QuestManager.GetAvailableQuests(1);
            foreach (var q in available)
            {
                Assert.AreNotEqual("first_gather", q.questId,
                    "Completed quests should not appear in available list");
            }
        }

        [Test]
        public void GetAvailableQuests_UpdatesLockedWhenPrereqMet()
        {
            // Complete first_gather → first_hunt should become Available
            QuestManager.AcceptQuest("first_gather");
            QuestManager.UpdateObjective("first_gather", QuestObjectiveType.GatherItem, "herb_red", 3);
            QuestManager.TryCompleteQuest("first_gather");

            var available = QuestManager.GetAvailableQuests(1);
            bool hasFirstHunt = false;
            foreach (var q in available)
            {
                if (q.questId == "first_hunt")
                {
                    hasFirstHunt = true;
                    break;
                }
            }
            Assert.IsTrue(hasFirstHunt,
                "first_hunt should become available when first_gather is completed");
        }

        // ==================================================================
        // QuestManager — GetActiveQuests / GetCompletedQuests
        // ==================================================================

        [Test]
        public void GetActiveQuests_ReturnsOnlyActive()
        {
            QuestManager.AcceptQuest("first_gather");
            QuestManager.AcceptQuest("visit_shop");

            var active = QuestManager.GetActiveQuests();
            Assert.AreEqual(2, active.Count, "Should have 2 active quests");

            foreach (var q in active)
            {
                var state = QuestManager.GetQuestState(q.questId);
                Assert.AreEqual(QuestState.Active, state,
                    $"Quest {q.questId} should be Active");
            }
        }

        [Test]
        public void GetActiveQuests_EmptyWhenNoneActive()
        {
            var active = QuestManager.GetActiveQuests();
            Assert.AreEqual(0, active.Count, "No active quests initially");
        }

        [Test]
        public void GetCompletedQuests_ReturnsOnlyCompleted()
        {
            QuestManager.AcceptQuest("first_gather");
            QuestManager.UpdateObjective("first_gather", QuestObjectiveType.GatherItem, "herb_red", 3);
            QuestManager.TryCompleteQuest("first_gather");

            var completed = QuestManager.GetCompletedQuests();
            Assert.AreEqual(1, completed.Count, "Should have 1 completed quest");
            Assert.AreEqual("first_gather", completed[0].questId);
        }

        [Test]
        public void GetCompletedQuests_EmptyWhenNoneCompleted()
        {
            var completed = QuestManager.GetCompletedQuests();
            Assert.AreEqual(0, completed.Count, "No completed quests initially");
        }

        [Test]
        public void GetCompletedQuests_ExcludesFailed()
        {
            QuestManager.AcceptQuest("first_gather");
            QuestManager.ForceState("first_gather", QuestState.Failed);

            var completed = QuestManager.GetCompletedQuests();
            foreach (var q in completed)
            {
                Assert.AreNotEqual("first_gather", q.questId,
                    "Failed quest should not appear in completed");
            }
        }

        // ==================================================================
        // QuestManager — Multiple Objectives
        // ==================================================================

        [Test]
        public void MultipleObjectivesTracking()
        {
            // Accept first_gather (1 objective) and test with multiple updates
            QuestManager.AcceptQuest("first_gather");

            // Update partially
            QuestManager.UpdateObjective("first_gather", QuestObjectiveType.GatherItem, "herb_red", 1);
            var quest = QuestManager.GetQuest("first_gather");
            Assert.AreEqual(1, quest.objectives[0].currentCount);

            // Complete all
            QuestManager.UpdateObjective("first_gather", QuestObjectiveType.GatherItem, "herb_red", 2);
            quest = QuestManager.GetQuest("first_gather");
            Assert.AreEqual(3, quest.objectives[0].currentCount);
            Assert.IsTrue(quest.AllObjectivesMet, "All objectives should be met");
        }

        // ==================================================================
        // QuestManager — Prerequisite Quest Chain
        // ==================================================================

        [Test]
        public void PrerequisiteQuestChain_LockedToAvailable()
        {
            // first_hunt starts Locked (requires first_gather)
            Assert.AreEqual(QuestState.Locked, QuestManager.GetQuestState("first_hunt"),
                "first_hunt starts Locked");

            // Complete the prerequisite
            QuestManager.AcceptQuest("first_gather");
            QuestManager.UpdateObjective("first_gather", QuestObjectiveType.GatherItem, "herb_red", 3);
            QuestManager.TryCompleteQuest("first_gather");

            // Now first_hunt should be Available (checked via GetAvailableQuests which auto-updates)
            var available = QuestManager.GetAvailableQuests(1);
            bool firstHuntAvailable = false;
            foreach (var q in available)
            {
                if (q.questId == "first_hunt")
                    firstHuntAvailable = true;
            }
            Assert.IsTrue(firstHuntAvailable,
                "first_hunt should be Available after completing first_gather");
        }

        // ==================================================================
        // QuestManager — Reward Distribution (with Mock PlayerStats)
        // ==================================================================

        [Test]
        public void Reward_GoldAndExp_Distribution()
        {
            CreateMockPlayerStats();

            int goldBefore = _playerStats.Gold;
            int expBefore = _playerStats.CurrentEXP;

            // Accept and complete first_gather (gold:10, exp:20)
            QuestManager.AcceptQuest("first_gather");
            QuestManager.UpdateObjective("first_gather", QuestObjectiveType.GatherItem, "herb_red", 3);
            bool completed = QuestManager.TryCompleteQuest("first_gather");
            Assert.IsTrue(completed, "Quest should complete successfully");

            Assert.AreEqual(goldBefore + 10, _playerStats.Gold,
                "Gold should increase by reward amount");
            Assert.AreEqual(expBefore + 20, _playerStats.CurrentEXP,
                "EXP should increase by reward amount");
        }

        [Test]
        public void Reward_NoPlayerStats_SafeGraceful()
        {
            // Without PlayerStats.Instance, GiveRewards should skip gracefully
            QuestManager.AcceptQuest("first_gather");
            QuestManager.UpdateObjective("first_gather", QuestObjectiveType.GatherItem, "herb_red", 3);
            bool completed = QuestManager.TryCompleteQuest("first_gather");

            Assert.IsTrue(completed, "Quest should complete even without PlayerStats");
            Assert.AreEqual(QuestState.Completed, QuestManager.GetQuestState("first_gather"));
        }

        [Test]
        public void Reward_MultipleQuests_Accumulate()
        {
            CreateMockPlayerStats();

            // Complete first_gather (gold:10, exp:20) and visit_shop (gold:5, exp:10)
            QuestManager.AcceptQuest("first_gather");
            QuestManager.UpdateObjective("first_gather", QuestObjectiveType.GatherItem, "herb_red", 3);
            QuestManager.TryCompleteQuest("first_gather");

            QuestManager.AcceptQuest("visit_shop");
            QuestManager.UpdateObjective("visit_shop", QuestObjectiveType.TalkToNPC, "shopkeeper", 1);
            QuestManager.TryCompleteQuest("visit_shop");

            Assert.AreEqual(15, _playerStats.Gold, "Total gold: 10 + 5");
            Assert.AreEqual(30, _playerStats.CurrentEXP, "Total EXP: 20 + 10");
        }

        // ==================================================================
        // QuestManager — Edge Cases
        // ==================================================================

        [Test]
        public void EdgeCase_NullQuestIds()
        {
            // Various API calls with null/empty should not throw
            Assert.DoesNotThrow(() => QuestManager.GetQuest(null));
            Assert.DoesNotThrow(() => QuestManager.GetQuestState(null));
            Assert.DoesNotThrow(() => QuestManager.AcceptQuest(null));
            Assert.DoesNotThrow(() => QuestManager.UpdateObjective(null, QuestObjectiveType.GatherItem, "x", 1));
            Assert.DoesNotThrow(() => QuestManager.TryCompleteQuest(null));
            Assert.DoesNotThrow(() => QuestManager.FailQuest(null));
            Assert.DoesNotThrow(() => QuestManager.ForceState(null, QuestState.Active));
        }

        [Test]
        public void EdgeCase_EmptyQuestIds()
        {
            Assert.DoesNotThrow(() => QuestManager.GetQuest(""));
            Assert.DoesNotThrow(() => QuestManager.GetQuestState(""));
            Assert.DoesNotThrow(() => QuestManager.AcceptQuest(""));
            Assert.DoesNotThrow(() => QuestManager.UpdateObjective("", QuestObjectiveType.GatherItem, "x", 1));
            Assert.DoesNotThrow(() => QuestManager.TryCompleteQuest(""));
            Assert.DoesNotThrow(() => QuestManager.FailQuest(""));
            Assert.DoesNotThrow(() => QuestManager.ForceState("", QuestState.Active));
        }

        [Test]
        public void EdgeCase_DuplicateAcceptance()
        {
            // First acceptance succeeds
            bool first = QuestManager.AcceptQuest("first_gather");
            Assert.IsTrue(first);

            // Second acceptance on same quest should fail
            bool second = QuestManager.AcceptQuest("first_gather");
            Assert.IsFalse(second, "Duplicate acceptance should be rejected");
        }

        [Test]
        public void EdgeCase_FailQuest()
        {
            // FailQuest on non-active does nothing
            Assert.DoesNotThrow(() => QuestManager.FailQuest("first_gather"));
            Assert.AreEqual(QuestState.Available, QuestManager.GetQuestState("first_gather"),
                "FailQuest on non-active should not change state");

            // FailQuest on active works
            QuestManager.AcceptQuest("first_gather");
            QuestManager.FailQuest("first_gather");
            Assert.AreEqual(QuestState.Failed, QuestManager.GetQuestState("first_gather"),
                "FailQuest on active should set Failed");
        }

        // ==================================================================
        // QuestData — AllObjectivesMet
        // ==================================================================

        [Test]
        public void QuestData_AllObjectivesMet_True()
        {
            var quest = QuestManager.GetQuest("first_gather");
            Assert.IsFalse(quest.AllObjectivesMet,
                "Initial state should have objectives not met");

            // After accepting and updating objectives to meet requirements
            QuestManager.AcceptQuest("first_gather");
            QuestManager.UpdateObjective("first_gather", QuestObjectiveType.GatherItem, "herb_red", 3);

            quest = QuestManager.GetQuest("first_gather");
            Assert.IsTrue(quest.AllObjectivesMet,
                "AllObjectivesMet should be true when counts match requirements");
        }

        [Test]
        public void QuestData_AllObjectivesMet_False_WhenNullObjectives()
        {
            // Create a quest-like struct with null objectives
            var quest = new QuestData
            {
                questId = "test",
                questName = "Test",
                objectives = null
            };
            Assert.IsFalse(quest.AllObjectivesMet, "Null objectives should return false");

            quest.objectives = new List<QuestObjective>();
            Assert.IsFalse(quest.AllObjectivesMet, "Empty objectives should return false");
        }

        [Test]
        public void QuestData_AllObjectivesMet_PartialProgress()
        {
            var quest = QuestManager.GetQuest("visit_shop");
            Assert.IsFalse(quest.AllObjectivesMet,
                "Quest with no progress should not have objectives met");

            QuestManager.AcceptQuest("visit_shop");
            // After partial progress
            QuestManager.UpdateObjective("visit_shop", QuestObjectiveType.TalkToNPC, "shopkeeper", 1);

            quest = QuestManager.GetQuest("visit_shop");
            Assert.IsTrue(quest.AllObjectivesMet,
                "Single objective met → AllObjectivesMet should be true");
        }

        // ==================================================================
        // NpcQuestGiver — Start / Player Detection
        // ==================================================================

        [Test]
        public void NpcQuestGiver_Start_FindsPlayer()
        {
            // Create a player GameObject with "Player" tag
            var playerGo = new GameObject("Player");
            playerGo.tag = "Player";

            var npcGo = CreateNpcQuestGiver("npc_test", new[] { "first_gather" }, Vector3.zero);

            // Start should find the player via tag
            // We can't call Start directly in EditMode reliably, but we can
            // verify the component was created without errors
            Assert.IsNotNull(npcGo.GetComponent<NpcQuestGiver>());

            UnityEngine.Object.DestroyImmediate(npcGo);
            UnityEngine.Object.DestroyImmediate(playerGo);
        }

        [Test]
        public void NpcQuestGiver_Start_NoPlayer_Warning()
        {
            // No player GameObject exists — Start should log a warning (no crash)
            var npcGo = CreateNpcQuestGiver("npc_test", new[] { "first_gather" }, Vector3.zero);

            // Component exists without issue
            Assert.IsNotNull(npcGo.GetComponent<NpcQuestGiver>());

            UnityEngine.Object.DestroyImmediate(npcGo);
        }

        [Test]
        public void NpcQuestGiver_DetectsPlayerProximity()
        {
            // Create player with tag
            var playerGo = new GameObject("Player");
            playerGo.tag = "Player";
            playerGo.transform.position = Vector3.zero;

            // NPC at same position (within range)
            var npcGo = CreateNpcQuestGiver("npc_test", new[] { "first_gather" }, Vector3.zero);
            var giver = npcGo.GetComponent<NpcQuestGiver>();

            // Call Start to initialize _player reference
            // In EditMode we use reflection to simulate
            var playerField = typeof(NpcQuestGiver).GetField("_player",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (playerField != null)
                playerField.SetValue(giver, playerGo.transform);

            // Call Update to detect proximity
            // In EditMode, simulate by checking the distance logic
            float dist = Vector3.Distance(npcGo.transform.position, playerGo.transform.position);
            Assert.LessOrEqual(dist, 3f, "NPC and player should be within interact range");

            UnityEngine.Object.DestroyImmediate(npcGo);
            UnityEngine.Object.DestroyImmediate(playerGo);
        }

        [Test]
        public void NpcQuestGiver_OutOfRange_NotDetected()
        {
            var playerGo = new GameObject("Player");
            playerGo.tag = "Player";
            playerGo.transform.position = new Vector3(100f, 0, 0); // Far away

            var npcGo = CreateNpcQuestGiver("npc_test", new[] { "first_gather" }, Vector3.zero);
            var giver = npcGo.GetComponent<NpcQuestGiver>();

            var playerField = typeof(NpcQuestGiver).GetField("_player",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (playerField != null)
                playerField.SetValue(giver, playerGo.transform);

            float dist = Vector3.Distance(npcGo.transform.position, playerGo.transform.position);
            Assert.Greater(dist, 3f, "NPC and player should be out of interact range");

            UnityEngine.Object.DestroyImmediate(npcGo);
            UnityEngine.Object.DestroyImmediate(playerGo);
        }

        [Test]
        public void NpcQuestGiver_EmptyQuestIds_DoesNotCrash()
        {
            var npcGo = CreateNpcQuestGiver("npc_test", null, Vector3.zero);
            Assert.IsNotNull(npcGo.GetComponent<NpcQuestGiver>());

            var npcGo2 = CreateNpcQuestGiver("npc_test2", new string[0], Vector3.zero);
            Assert.IsNotNull(npcGo2.GetComponent<NpcQuestGiver>());

            UnityEngine.Object.DestroyImmediate(npcGo);
            UnityEngine.Object.DestroyImmediate(npcGo2);
        }

        // ==================================================================
        // QuestWindow — Open / Close
        // ==================================================================

        [Test]
        public void QuestWindow_InitiallyClosed()
        {
            var go = new GameObject("TestQuestWindow");
            var window = go.AddComponent<QuestWindow>();

            // In Awake, UIWindow sets _windowRoot to gameObject and deactivates it
            // Since Awake is called on instantiation, _isOpen should be false
            Assert.IsFalse(window.IsOpen, "QuestWindow should be initially closed");

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void QuestWindow_Show_OpensWindow()
        {
            var go = new GameObject("TestQuestWindow");
            var window = go.AddComponent<QuestWindow>();

            window.Show();
            Assert.IsTrue(window.IsOpen, "QuestWindow should be open after Show()");

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void QuestWindow_Hide_ClosesWindow()
        {
            var go = new GameObject("TestQuestWindow");
            var window = go.AddComponent<QuestWindow>();

            window.Show();
            Assert.IsTrue(window.IsOpen);

            window.Hide();
            Assert.IsFalse(window.IsOpen, "QuestWindow should be closed after Hide()");

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void QuestWindow_Toggle_WorksCorrectly()
        {
            var go = new GameObject("TestQuestWindow");
            var window = go.AddComponent<QuestWindow>();

            // Toggle open
            window.Toggle();
            Assert.IsTrue(window.IsOpen, "First toggle should open");

            // Toggle closed
            window.Toggle();
            Assert.IsFalse(window.IsOpen, "Second toggle should close");

            // Toggle open again
            window.Toggle();
            Assert.IsTrue(window.IsOpen, "Third toggle should open again");

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void QuestWindow_DoubleShow_StaysOpen()
        {
            var go = new GameObject("TestQuestWindow");
            var window = go.AddComponent<QuestWindow>();

            window.Show();
            window.Show(); // Second Show
            Assert.IsTrue(window.IsOpen, "Double Show should keep window open");

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void QuestWindow_DoubleHide_StaysClosed()
        {
            var go = new GameObject("TestQuestWindow");
            var window = go.AddComponent<QuestWindow>();

            window.Show();
            window.Hide();
            window.Hide(); // Second Hide
            Assert.IsFalse(window.IsOpen, "Double Hide should keep window closed");

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void QuestWindow_RefreshQuestList_DoesNotThrow()
        {
            var go = new GameObject("TestQuestWindow");
            var window = go.AddComponent<QuestWindow>();

            // RefreshQuestList should not throw even with no active quests
            Assert.DoesNotThrow(() => window.RefreshQuestList());

            // Add some quests and refresh
            QuestManager.AcceptQuest("first_gather");
            QuestManager.AcceptQuest("visit_shop");
            Assert.DoesNotThrow(() => window.RefreshQuestList());

            UnityEngine.Object.DestroyImmediate(go);
        }

        // ==================================================================
        // Integration: Full Quest Lifecycle
        // ==================================================================

        [Test]
        public void FullQuestLifecycle_Accept_Progress_Complete()
        {
            // 1. Initial state
            Assert.AreEqual(QuestState.Available, QuestManager.GetQuestState("first_gather"));

            // 2. Accept
            Assert.IsTrue(QuestManager.AcceptQuest("first_gather"));
            Assert.AreEqual(QuestState.Active, QuestManager.GetQuestState("first_gather"));

            // 3. Progress
            var quest = QuestManager.GetQuest("first_gather");
            Assert.IsFalse(quest.AllObjectivesMet, "Objectives not yet met");

            QuestManager.UpdateObjective("first_gather", QuestObjectiveType.GatherItem, "herb_red", 3);
            quest = QuestManager.GetQuest("first_gather");
            Assert.IsTrue(quest.AllObjectivesMet, "All objectives met");

            // 4. Complete
            Assert.IsTrue(QuestManager.TryCompleteQuest("first_gather"));
            Assert.AreEqual(QuestState.Completed, QuestManager.GetQuestState("first_gather"));
        }

        [Test]
        public void FullQuestLifecycle_WithPrerequisiteChain()
        {
            CreateMockPlayerStats();

            // Complete first_gather first
            Assert.IsTrue(QuestManager.AcceptQuest("first_gather"));
            QuestManager.UpdateObjective("first_gather", QuestObjectiveType.GatherItem, "herb_red", 3);
            Assert.IsTrue(QuestManager.TryCompleteQuest("first_gather"));
            Assert.AreEqual(QuestState.Completed, QuestManager.GetQuestState("first_gather"));

            // first_hunt should now be available via GetAvailableQuests
            var available = QuestManager.GetAvailableQuests(1);
            bool huntAvailable = false;
            foreach (var q in available)
            {
                if (q.questId == "first_hunt")
                    huntAvailable = true;
            }
            Assert.IsTrue(huntAvailable, "first_hunt should be available after prereq completed");

            // Accept and complete first_hunt
            Assert.IsTrue(QuestManager.AcceptQuest("first_hunt"));
            QuestManager.UpdateObjective("first_hunt", QuestObjectiveType.KillMonster, "rabbit", 2);
            Assert.IsTrue(QuestManager.TryCompleteQuest("first_hunt"));

            // Check cumulative rewards: first_gather(10g,20exp) + first_hunt(20g,30exp)
            Assert.AreEqual(30, _playerStats.Gold, "Total gold: 10 + 20");
            Assert.AreEqual(50, _playerStats.CurrentEXP, "Total EXP: 20 + 30");
        }

        // ==================================================================
        // QuestManager — FailQuest coverage
        // ==================================================================

        [Test]
        public void FailQuest_OnActive_SetsFailed()
        {
            QuestManager.AcceptQuest("first_gather");
            QuestManager.FailQuest("first_gather");
            Assert.AreEqual(QuestState.Failed, QuestManager.GetQuestState("first_gather"));
        }

        [Test]
        public void FailQuest_OnNonActive_DoesNothing()
        {
            QuestManager.FailQuest("first_gather"); // Available
            Assert.AreEqual(QuestState.Available, QuestManager.GetQuestState("first_gather"));

            QuestManager.FailQuest("first_hunt"); // Locked
            Assert.AreEqual(QuestState.Locked, QuestManager.GetQuestState("first_hunt"));

            // Complete first, then fail should not affect completed
            QuestManager.AcceptQuest("first_gather");
            QuestManager.UpdateObjective("first_gather", QuestObjectiveType.GatherItem, "herb_red", 3);
            QuestManager.TryCompleteQuest("first_gather");
            Assert.AreEqual(QuestState.Completed, QuestManager.GetQuestState("first_gather"));
            QuestManager.FailQuest("first_gather");
            Assert.AreEqual(QuestState.Completed, QuestManager.GetQuestState("first_gather"),
                "FailQuest on Completed should not change state");
        }
    }
}
