#if false
using System.Linq;
using NUnit.Framework;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// G3-09: QuestJournalUI EditMode 테스트
    ///
    /// 대상:
    /// - AddQuest / UpdateProgress / CompleteQuest 공개 메서드
    /// - 탭 전환 (Active ↔ Completed)
    /// - 완료 이펙트 큐 동작
    /// - 경계값: 빈 목표, 중복 추가, null/빈 ID
    /// </summary>
    public class QuestJournalUITests
    {
        private GameObject _journalGo;
        private QuestJournalUI _journal;

        // ================================================================
        // Setup / Teardown
        // ================================================================

        [SetUp]
        public void Setup()
        {
            _journalGo = new GameObject("TestQuestJournal");
            _journal = _journalGo.AddComponent<QuestJournalUI>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_journalGo != null)
                Object.DestroyImmediate(_journalGo);
        }

        // ================================================================
        // 헬퍼
        // ================================================================

        /// <summary>단일 목표 퀘스트 생성</summary>
        private QuestObjective[] MakeObjectives(string desc, int required)
        {
            return new[]
            {
                new QuestObjective
                {
                    type = QuestObjectiveType.GatherItem,
                    targetId = "test_item",
                    requiredCount = required,
                    currentCount = 0,
                    description = desc
                }
            };
        }

        /// <summary>다중 목표 퀘스트 생성</summary>
        private QuestObjective[] MakeMultiObjectives()
        {
            return new[]
            {
                new QuestObjective
                {
                    type = QuestObjectiveType.GatherItem,
                    targetId = "herb_red",
                    requiredCount = 3,
                    currentCount = 0,
                    description = "약초 3개 채집"
                },
                new QuestObjective
                {
                    type = QuestObjectiveType.KillMonster,
                    targetId = "rabbit",
                    requiredCount = 2,
                    currentCount = 0,
                    description = "토끼 2마리 사냥"
                }
            };
        }

        /// <summary>단일 QuestObjective 배열 생성</summary>
        private QuestObjective[] SingleObjective(QuestObjectiveType type, string targetId, int required, string desc)
        {
            return new[]
            {
                new QuestObjective
                {
                    type = type,
                    targetId = targetId,
                    requiredCount = required,
                    currentCount = 0,
                    description = desc
                }
            };
        }

        // ================================================================
        // 1. AddQuest
        // ================================================================

        [Test]
        public void AddQuest_AddsToActiveList()
        {
            // Given
            var objs = MakeObjectives("테스트 아이템 5개 수집", 5);

            // When
            _journal.AddQuest("q_test_01", "테스트 퀘스트", "설명", objs);

            // Then
            Assert.AreEqual(1, _journal.ActiveQuestCount, "퀘스트가 Active 목록에 추가되어야 함");
            Assert.AreEqual(0, _journal.CompletedQuestCount, "Completed는 0이어야 함");
        }

        [Test]
        public void AddQuest_DuplicateId_DoesNotAddDuplicate()
        {
            // Given
            var objs = MakeObjectives("아이템 수집", 3);
            _journal.AddQuest("q_dup", "중복 퀘스트", "설명", objs);

            // When — 동일 ID로 다시 추가
            _journal.AddQuest("q_dup", "중복 퀘스트", "다른 설명", objs);

            // Then
            Assert.AreEqual(1, _journal.ActiveQuestCount, "중복 추가되지 않아야 함");
        }

        [Test]
        public void AddQuest_MultipleQuests_CountMatches()
        {
            // Given
            var objs = MakeObjectives("아이템 수집", 3);

            // When
            _journal.AddQuest("q_01", "퀘스트1", "설명1", objs);
            _journal.AddQuest("q_02", "퀘스트2", "설명2", objs);
            _journal.AddQuest("q_03", "퀘스트3", "설명3", objs);

            // Then
            Assert.AreEqual(3, _journal.ActiveQuestCount, "3개의 Active 퀘스트");
            Assert.AreEqual(3, _journal.Quests.Count, "전체 3개 퀘스트");
        }

        [Test]
        public void AddQuest_EmptyObjectives_AllowsNoObjectives()
        {
            // When — 빈 목표 배열
            _journal.AddQuest("q_empty_obj", "목표 없는 퀘스트", "설명", System.Array.Empty<QuestObjective>());

            // Then
            Assert.AreEqual(1, _journal.ActiveQuestCount, "빈 목표도 추가 가능");
        }

        // ================================================================
        // 2. UpdateProgress
        // ================================================================

        [Test]
        public void UpdateProgress_IncrementsObjectiveCount()
        {
            // Given
            var objs = MakeObjectives("아이템 5개", 5);
            _journal.AddQuest("q_gather", "수집 퀘스트", "설명", objs);

            // When
            _journal.UpdateProgress("q_gather", 0, 2);

            // Then — 내부 objectives 확인
            var entry = _journal.Quests.First(q => q.questId == "q_gather");
            Assert.AreEqual(2, entry.objectives[0].currentCount, "2만큼 증가해야 함");
        }

        [Test]
        public void UpdateProgress_ClampsAtRequiredCount()
        {
            // Given
            var objs = MakeObjectives("아이템 3개", 3);
            _journal.AddQuest("q_clamp", "클램프 테스트", "설명", objs);

            // When — requiredCount(3)를 초과
            _journal.UpdateProgress("q_clamp", 0, 10);

            // Then — requiredCount로 클램프
            var entry = _journal.Quests.First(q => q.questId == "q_clamp");
            Assert.AreEqual(3, entry.objectives[0].currentCount, "requiredCount로 클램프되어야 함");
            Assert.IsTrue(entry.objectives[0].IsMet, "목표 달성 상태여야 함");
        }

        [Test]
        public void UpdateProgress_MultipleObjectives_TracksIndependently()
        {
            // Given
            var objs = MakeMultiObjectives();
            _journal.AddQuest("q_multi", "다중 목표", "설명", objs);

            // When — 각 목표 개별 갱신
            _journal.UpdateProgress("q_multi", 0, 2); // herb_red 2/3
            _journal.UpdateProgress("q_multi", 1, 1); // rabbit 1/2

            // Then
            var entry = _journal.Quests.First(q => q.questId == "q_multi");
            Assert.AreEqual(2, entry.objectives[0].currentCount, "첫 번째 목표: 2");
            Assert.AreEqual(1, entry.objectives[1].currentCount, "두 번째 목표: 1");
            Assert.IsFalse(entry.objectives[0].IsMet, "첫 번째 목표 미달성");
            Assert.IsFalse(entry.objectives[1].IsMet, "두 번째 목표 미달성");
        }

        [Test]
        public void UpdateProgress_InvalidQuestId_DoesNothing()
        {
            // Should not throw
            Assert.DoesNotThrow(() => _journal.UpdateProgress(null, 0, 1));
            Assert.DoesNotThrow(() => _journal.UpdateProgress("", 0, 1));
            Assert.DoesNotThrow(() => _journal.UpdateProgress("nonexistent", 0, 1));
        }

        [Test]
        public void UpdateProgress_InvalidIndex_DoesNothing()
        {
            // Given
            var objs = MakeObjectives("아이템", 3);
            _journal.AddQuest("q_invalid_idx", "인덱스 테스트", "설명", objs);

            // When — 유효하지 않은 인덱스
            Assert.DoesNotThrow(() => _journal.UpdateProgress("q_invalid_idx", -1, 1));
            Assert.DoesNotThrow(() => _journal.UpdateProgress("q_invalid_idx", 99, 1));

            // Then — 변경 없음
            var entry = _journal.Quests.First(q => q.questId == "q_invalid_idx");
            Assert.AreEqual(0, entry.objectives[0].currentCount, "변경되지 않아야 함");
        }

        [Test]
        public void UpdateProgress_CompletedQuest_DoesNothing()
        {
            // Given — 퀘스트를 완료 상태로 만듦
            var objs = MakeObjectives("아이템", 1);
            _journal.AddQuest("q_done", "완료된 퀘스트", "설명", objs);
            _journal.UpdateProgress("q_done", 0, 1);
            _journal.CompleteQuest("q_done");

            // When — 완료된 퀘스트에 업데이트 시도
            _journal.UpdateProgress("q_done", 0, 1);

            // Then
            var entry = _journal.Quests.First(q => q.questId == "q_done");
            Assert.AreEqual(QuestState.Completed, entry.state, "Completed 상태 유지");
            Assert.AreEqual(1, entry.objectives[0].currentCount, "진행도 변경 없음");
        }

        // ================================================================
        // 3. CompleteQuest
        // ================================================================

        [Test]
        public void CompleteQuest_Success_ChangesState()
        {
            // Given
            var objs = MakeObjectives("아이템 1개", 1);
            _journal.AddQuest("q_complete", "완료 테스트", "설명", objs);
            _journal.UpdateProgress("q_complete", 0, 1);

            // When
            bool result = _journal.CompleteQuest("q_complete");

            // Then
            Assert.IsTrue(result, "완료 성공");
            var entry = _journal.Quests.First(q => q.questId == "q_complete");
            Assert.AreEqual(QuestState.Completed, entry.state, "Completed 상태");
            Assert.IsFalse(string.IsNullOrEmpty(entry.completionTime), "완료 시간 기록됨");
            Assert.AreEqual(0, _journal.ActiveQuestCount, "Active 퀘스트 0");
            Assert.AreEqual(1, _journal.CompletedQuestCount, "Completed 퀘스트 1");
        }

        [Test]
        public void CompleteQuest_Fails_ObjectivesNotMet()
        {
            // Given — 목표 미달성 상태
            var objs = MakeObjectives("아이템 5개", 5);
            _journal.AddQuest("q_fail_obj", "실패 테스트", "설명", objs);

            // When — 목표 달성 없이 완료 시도
            bool result = _journal.CompleteQuest("q_fail_obj");

            // Then
            Assert.IsFalse(result, "목표 미달성 시 완료 실패");
            var entry = _journal.Quests.First(q => q.questId == "q_fail_obj");
            Assert.AreEqual(QuestState.Active, entry.state, "Active 상태 유지");
        }

        [Test]
        public void CompleteQuest_Fails_AlreadyCompleted()
        {
            // Given
            var objs = MakeObjectives("아이템 1개", 1);
            _journal.AddQuest("q_already", "이중 완료", "설명", objs);
            _journal.UpdateProgress("q_already", 0, 1);
            _journal.CompleteQuest("q_already");

            // When — 이미 완료된 퀘스트 다시 완료 시도
            bool result = _journal.CompleteQuest("q_already");

            // Then
            Assert.IsFalse(result, "이미 완료된 퀘스트는 다시 완료 불가");
        }

        [Test]
        public void CompleteQuest_InvalidId_ReturnsFalse()
        {
            Assert.IsFalse(_journal.CompleteQuest(null), "null ID는 false");
            Assert.IsFalse(_journal.CompleteQuest(""), "빈 ID는 false");
            Assert.IsFalse(_journal.CompleteQuest("unknown"), "없는 ID는 false");
        }

        [Test]
        public void CompleteQuest_TriggersEffect()
        {
            // Given
            var objs = MakeObjectives("아이템 1개", 1);
            _journal.AddQuest("q_effect", "이펙트 테스트", "설명", objs);
            _journal.UpdateProgress("q_effect", 0, 1);

            // When
            _journal.CompleteQuest("q_effect");

            // Then — 효과는 Update에서 처리되므로 CompleteQuest 호출 자체로 검증
            // 완료 자체가 성공했는지만 확인
            var entry = _journal.Quests.First(q => q.questId == "q_effect");
            Assert.AreEqual(QuestState.Completed, entry.state);
        }

        // ================================================================
        // 4. Tab Switching
        // ================================================================

        [Test]
        public void TabSwitching_DefaultIsActive()
        {
            // IsOpen is private — but we can test via the Quests filtering logic
            // by checking where entries end up
            var objs = MakeObjectives("테스트", 1);

            // Active quests should exist
            _journal.AddQuest("q_01", "액티브", "진행 중", objs);

            // Non-public tab state — verify by quest counts
            Assert.AreEqual(1, _journal.ActiveQuestCount);
            Assert.AreEqual(0, _journal.CompletedQuestCount);
        }

        [Test]
        public void TabSwitching_CompletedQuestAppearsInCompleted()
        {
            // Given — 퀘스트 완료
            var objs = MakeObjectives("아이템 1개", 1);
            _journal.AddQuest("q_tab", "탭 테스트", "설명", objs);
            _journal.UpdateProgress("q_tab", 0, 1);
            _journal.CompleteQuest("q_tab");

            // Then
            Assert.AreEqual(0, _journal.ActiveQuestCount, "Active 탭: 0");
            Assert.AreEqual(1, _journal.CompletedQuestCount, "Completed 탭: 1");
        }

        [Test]
        public void TabSwitching_MixedQuests_CountsCorrect()
        {
            // Given
            var objs = MakeObjectives("테스트", 1);

            // 2개의 Active 퀘스트
            _journal.AddQuest("q_a1", "액티브1", "설명", objs);
            _journal.AddQuest("q_a2", "액티브2", "설명", objs);

            // 1개의 Completed 퀘스트
            _journal.AddQuest("q_c1", "완료1", "설명", objs);
            _journal.UpdateProgress("q_c1", 0, 1);
            _journal.CompleteQuest("q_c1");

            // Then
            Assert.AreEqual(2, _journal.ActiveQuestCount, "Active 2개");
            Assert.AreEqual(1, _journal.CompletedQuestCount, "Completed 1개");
            Assert.AreEqual(3, _journal.Quests.Count, "전체 3개");
        }

        // ================================================================
        // 5. Toggle / Open / Close
        // ================================================================

        [Test]
        public void Toggle_InitiallyClosed()
        {
            Assert.IsFalse(_journal.IsOpen, "처음에는 닫힌 상태");
        }

        [Test]
        public void Open_OpensJournal()
        {
            _journal.Open();
            Assert.IsTrue(_journal.IsOpen, "Open() 후 열림");
        }

        [Test]
        public void Close_ClosesJournal()
        {
            _journal.Open();
            _journal.Close();
            Assert.IsFalse(_journal.IsOpen, "Close() 후 닫힘");
        }

        [Test]
        public void Toggle_TogglesState()
        {
            Assert.IsFalse(_journal.IsOpen);
            _journal.Toggle();
            Assert.IsTrue(_journal.IsOpen, "첫 Toggle → 열림");
            _journal.Toggle();
            Assert.IsFalse(_journal.IsOpen, "두 번째 Toggle → 닫힘");
        }

        // ================================================================
        // 6. 전체 워크플로우
        // ================================================================

        [Test]
        public void FullWorkflow_AddUpdateComplete_EndToEnd()
        {
            // Given — 다중 목표 퀘스트
            var objs = SingleObjective(QuestObjectiveType.GatherItem, "iron_ore", 5, "철광석 5개 채굴");
            _journal.AddQuest("q_iron", "철광석 채굴", "광산에서 철광석을 5개 채굴하세요.", objs);

            // When — 단계별 진행
            _journal.UpdateProgress("q_iron", 0, 2); // 2/5
            Assert.AreEqual(2, _journal.Quests.First(q => q.questId == "q_iron").objectives[0].currentCount);

            _journal.UpdateProgress("q_iron", 0, 1); // 3/5
            Assert.AreEqual(3, _journal.Quests.First(q => q.questId == "q_iron").objectives[0].currentCount);

            _journal.UpdateProgress("q_iron", 0, 2); // 5/5
            Assert.AreEqual(5, _journal.Quests.First(q => q.questId == "q_iron").objectives[0].currentCount);
            Assert.IsTrue(_journal.Quests.First(q => q.questId == "q_iron").objectives[0].IsMet);

            // Then — 완료
            bool completed = _journal.CompleteQuest("q_iron");
            Assert.IsTrue(completed);
            Assert.AreEqual(QuestState.Completed, _journal.Quests.First(q => q.questId == "q_iron").state);
            Assert.AreEqual(0, _journal.ActiveQuestCount);
            Assert.AreEqual(1, _journal.CompletedQuestCount);
        }

        [Test]
        public void FullWorkflow_MultipleMixedQuests()
        {
            // Given — 3개의 퀘스트 추가
            var objs1 = MakeObjectives("고기 3개", 3);
            var objs2 = MakeObjectives("나무 5개", 5);
            var objs3 = MakeObjectives("돌 2개", 2);

            _journal.AddQuest("q_meat", "고기 수집", "신선한 고기 3개", objs1);
            _journal.AddQuest("q_wood", "나무 수집", "장작 5개", objs2);
            _journal.AddQuest("q_stone", "돌 수집", "돌멩이 2개", objs3);

            Assert.AreEqual(3, _journal.ActiveQuestCount);
            Assert.AreEqual(0, _journal.CompletedQuestCount);

            // When — 2개 완료, 1개 미완료
            _journal.UpdateProgress("q_meat", 0, 3);
            _journal.CompleteQuest("q_meat");

            _journal.UpdateProgress("q_stone", 0, 2);
            _journal.CompleteQuest("q_stone");

            // Then
            Assert.AreEqual(1, _journal.ActiveQuestCount, "Active 1개 (wood)");
            Assert.AreEqual(2, _journal.CompletedQuestCount, "Completed 2개 (meat, stone)");
            Assert.AreEqual(3, _journal.Quests.Count, "전체 3개");

            // 각 상태 확인
            Assert.AreEqual(QuestState.Completed, _journal.Quests.First(q => q.questId == "q_meat").state);
            Assert.AreEqual(QuestState.Active, _journal.Quests.First(q => q.questId == "q_wood").state);
            Assert.AreEqual(QuestState.Completed, _journal.Quests.First(q => q.questId == "q_stone").state);
        }

        // ================================================================
        // 7. 경계값 엣지 케이스
        // ================================================================

        [Test]
        public void EmptyJournal_CountsAreZero()
        {
            Assert.AreEqual(0, _journal.ActiveQuestCount);
            Assert.AreEqual(0, _journal.CompletedQuestCount);
            Assert.AreEqual(0, _journal.Quests.Count);
        }

        [Test]
        public void AddQuest_NullDescription_DoesNotThrow()
        {
            var objs = MakeObjectives("테스트", 1);
            Assert.DoesNotThrow(() => _journal.AddQuest("q_null_desc", "이름", null, objs));
        }

        [Test]
        public void CompleteQuest_PartialProgress_ReturnsFalse()
        {
            // Given — 목표가 3개 필요한데 2개만 채움
            var objs = MakeObjectives("아이템 3개", 3);
            _journal.AddQuest("q_partial", "부분 진행", "설명", objs);
            _journal.UpdateProgress("q_partial", 0, 2);

            // When
            bool result = _journal.CompleteQuest("q_partial");

            // Then
            Assert.IsFalse(result, "부분 진행 시 완료 불가");
            Assert.AreEqual(QuestState.Active, _journal.Quests.First(q => q.questId == "q_partial").state);
        }
    }
}
#endif
