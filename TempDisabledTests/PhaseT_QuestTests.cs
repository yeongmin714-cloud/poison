using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// T-Cycle-05: 튜토리얼 퀘스트 자동 발급 + 가이드 큐 테스트
    /// </summary>
    public class PhaseT_QuestTests
    {
        private GameObject _managerGo;
        private TutorialQuestManager _manager;

        [SetUp]
        public void Setup()
        {
            // QuestManager 초기화
            QuestManager.ResetAll();
            QuestManager.Initialize();

            // PlayerStats 생성 (보상 지급 테스트용)
            if (PlayerStats.Instance == null)
            {
                var statsGo = new GameObject("TestPlayerStats");
                statsGo.AddComponent<PlayerStats>();
            }

            // PlayerInventory 생성 (보상 지급 테스트용)
            if (PlayerInventory.Instance == null)
            {
                var invGo = new GameObject("TestPlayerInventory");
                invGo.AddComponent<PlayerInventory>();
            }
        }

        [TearDown]
        public void Teardown()
        {
            // 매니저 제거 (OnDestroy에서 이벤트 구독 해제)
            if (_managerGo != null)
                Object.DestroyImmediate(_managerGo);

            // 정리
            if (PlayerStats.Instance != null)
                Object.DestroyImmediate(PlayerStats.Instance.gameObject);

            if (PlayerInventory.Instance != null)
                Object.DestroyImmediate(PlayerInventory.Instance.gameObject);

            // TutorialGuideSystem도 정리
            if (TutorialGuideSystem.Instance != null)
                Object.DestroyImmediate(TutorialGuideSystem.Instance.gameObject);

            QuestManager.ResetAll();
        }

        // ================================================================
        // 헬퍼
        // ================================================================

        private TutorialQuestManager CreateManager()
        {
            _managerGo = new GameObject("TestTutorialQuestManager");
            _manager = _managerGo.AddComponent<TutorialQuestManager>();

            // private static _instance 설정 (싱글톤 우회)
            var instanceField = typeof(TutorialQuestManager)
                .GetField("_instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (instanceField != null)
                instanceField.SetValue(null, _manager);

            return _manager;
        }

        private TutorialGuideSystem EnsureGuideSystem()
        {
            if (TutorialGuideSystem.Instance == null)
            {
                var guideGo = new GameObject("TestTutorialGuideSystem");
                guideGo.AddComponent<TutorialGuideSystem>();
            }
            return TutorialGuideSystem.Instance;
        }

        /// <summary>리플렉션으로 private 메서드 호출</summary>
        private object CallPrivateMethod(object target, string methodName, params object[] args)
        {
            var method = target.GetType()
                .GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return method?.Invoke(target, args);
        }

        // ================================================================
        // 1. 싱글톤 인스턴스
        // ================================================================

        [Test]
        public void TutorialQuestManager_Instance_NotNull()
        {
            Assert.IsNotNull(TutorialQuestManager.Instance,
                "TutorialQuestManager.Instance는 null이 아니어야 함");
        }

        // ================================================================
        // 2. 퀘스트 등록
        // ================================================================

        [Test]
        public void TutorialQuestManager_StartTutorialQuests_RegistersQuests()
        {
            // Given
            CreateManager();
            EnsureGuideSystem();

            // When
            _manager.StartTutorialQuests();

            // Then — 두 튜토리얼 퀘스트가 QuestManager에 등록되어야 함
            QuestData q1 = QuestManager.GetQuest("tutorial_q1_food");
            Assert.IsNotNull(q1.questId, "Q1 튜토리얼 퀘스트가 등록되어야 함");
            Assert.AreEqual("tutorial_q1_food", q1.questId);

            QuestData q2 = QuestManager.GetQuest("tutorial_q2_diarrhea");
            Assert.IsNotNull(q2.questId, "Q2 튜토리얼 퀘스트가 등록되어야 함");
            Assert.AreEqual("tutorial_q2_diarrhea", q2.questId);
        }

        // ================================================================
        // 3. 퀘스트 자동 수락
        // ================================================================

        [Test]
        public void TutorialQuestManager_StartTutorialQuests_AcceptsQuests()
        {
            // Given
            CreateManager();
            EnsureGuideSystem();

            // When
            _manager.StartTutorialQuests();

            // Then — 두 퀘스트 모두 Active 상태여야 함
            Assert.AreEqual(QuestState.Active, QuestManager.GetQuestState("tutorial_q1_food"),
                "Q1은 Active 상태여야 함");
            Assert.AreEqual(QuestState.Active, QuestManager.GetQuestState("tutorial_q2_diarrhea"),
                "Q2는 Active 상태여야 함");
        }

        // ================================================================
        // 4. 퀘스트 ID와 데이터 확인
        // ================================================================

        [Test]
        public void TutorialQuestManager_QuestIds_Correct()
        {
            // Given
            CreateManager();
            EnsureGuideSystem();

            // When
            _manager.StartTutorialQuests();

            // Then
            Assert.AreEqual("tutorial_q1_food", _manager.QuestIdFood, "Q1 questId 일치");
            Assert.AreEqual("tutorial_q2_diarrhea", _manager.QuestIdDiarrhea, "Q2 questId 일치");

            // Q1 데이터 확인
            QuestData q1 = QuestManager.GetQuest("tutorial_q1_food");
            Assert.AreEqual("음식 재료 구하기", q1.questName);
            Assert.AreEqual(1, q1.requiredLevel);
            Assert.AreEqual(3, q1.objectives.Count, "Q1은 3개의 목표가 있어야 함");

            // Q2 데이터 확인
            QuestData q2 = QuestManager.GetQuest("tutorial_q2_diarrhea");
            Assert.AreEqual("설사약 재료 구하기", q2.questName);
            Assert.AreEqual(1, q2.requiredLevel);
            Assert.AreEqual(2, q2.objectives.Count, "Q2는 2개의 목표가 있어야 함");
        }

        // ================================================================
        // 5. 가이드 큐 초기화
        // ================================================================

        [Test]
        public void TutorialQuestManager_GuideQueue_Initialized()
        {
            // Given
            CreateManager();
            EnsureGuideSystem();

            // When
            _manager.StartTutorialQuests();

            // Then — 큐에 11개 가이드 중 첫 번째가 표시됨 (나머지 10개는 대기)
            // 첫 번째 가이드는 ShowGuide를 통해 표시되었고, 큐에서 제거됨
            // 초기에는 11개지만, 첫 번째 가이드가 ShowNextGuide로 Dequeue됐으므로
            // RemainingGuideCount는 10이어야 함
            Assert.AreEqual(10, _manager.RemainingGuideCount,
                "첫 번째 가이드 표시 후 큐에 10개 남아있어야 함");
        }

        // ================================================================
        // 6. 첫 번째 가이드 자동 표시
        // ================================================================

        [Test]
        public void TutorialQuestManager_GuideQueue_ShowsFirstGuide()
        {
            // Given
            CreateManager();
            var guideSys = EnsureGuideSystem();
            _manager.StartTutorialQuests();

            // When — 첫 번째 가이드 완료 시뮬레이션
            // TutorialGuideSystem의 OnGuideProcessed 이벤트를 통해
            // TutorialQuestManager가 다음 가이드를 표시
            guideSys.OnGuideProcessed?.Invoke("01_movement", false);

            // Then — 큐가 하나 줄었어야 함 (10 → 9)
            Assert.AreEqual(9, _manager.RemainingGuideCount,
                "한 개 가이드 완료 후 큐에 9개 남아있어야 함");
        }

        // ================================================================
        // 7. ESC 스킵 처리
        // ================================================================

        [Test]
        public void TutorialQuestManager_SkipAllGuides_Completes()
        {
            // Given
            CreateManager();
            EnsureGuideSystem();
            _manager.StartTutorialQuests();

            // When — ESC 스킵
            _manager.SkipAllGuides();

            // Then
            Assert.IsTrue(_manager.IsAllGuidesComplete, "모든 가이드가 완료 상태여야 함");
            Assert.AreEqual(0, _manager.RemainingGuideCount, "큐가 비어있어야 함");
        }

        // ================================================================
        // 8. 모든 가이드 완료 — 메시지 출력
        // ================================================================

        [Test]
        public void TutorialQuestManager_AllGuidesComplete_Message()
        {
            // Given
            CreateManager();
            EnsureGuideSystem();
            _manager.StartTutorialQuests();

            // When — 모든 가이드 완료 (SkipAllGuides로 시뮬레이션)
            _manager.SkipAllGuides();

            // Then — IsAllGuidesComplete가 true여야 함
            Assert.IsTrue(_manager.IsAllGuidesComplete,
                "SkipAllGuides 후 모든 가이드가 완료 상태여야 함");

            // IsAllGuidesComplete가 true면
            // Update에서 CheckQuestCompletion이 호출되므로
            // 퀘스트 완료 체크 준비 완료
            Assert.IsTrue(_manager.IsQuestsRegistered,
                "퀘스트가 등록 상태여야 함");
        }

        // ================================================================
        // 9. 퀘스트 보상 데이터 확인
        // ================================================================

        [Test]
        public void TutorialQuestManager_QuestRewards_Correct()
        {
            // Given
            CreateManager();
            EnsureGuideSystem();
            _manager.StartTutorialQuests();
            _manager.SkipAllGuides();

            // When & Then — 보상 데이터 확인
            QuestData q1 = QuestManager.GetQuest("tutorial_q1_food");
            Assert.AreEqual(50, q1.reward.exp, "Q1 보상 경험치는 50");
            Assert.AreEqual(20, q1.reward.gold, "Q1 보상 골드는 20");

            QuestData q2 = QuestManager.GetQuest("tutorial_q2_diarrhea");
            Assert.AreEqual(30, q2.reward.exp, "Q2 보상 경험치는 30");
            Assert.AreEqual(0, q2.reward.gold, "Q2 보상 골드는 0");
        }

        // ================================================================
        // 10. Q1 목표 확인 — 고기3, 나무5, 돌3
        // ================================================================

        [Test]
        public void TutorialQuestManager_Q1_Objectives_Correct()
        {
            // Given
            CreateManager();
            EnsureGuideSystem();
            _manager.StartTutorialQuests();

            // When
            QuestData q1 = QuestManager.GetQuest("tutorial_q1_food");

            // Then
            Assert.AreEqual(3, q1.objectives.Count, "Q1은 3개의 목표");

            // 목표 1: 고기 3개
            Assert.AreEqual(QuestObjectiveType.GatherItem, q1.objectives[0].type);
            Assert.AreEqual("meat", q1.objectives[0].targetId);
            Assert.AreEqual(3, q1.objectives[0].requiredCount);
            Assert.AreEqual("고기 3개 모으기", q1.objectives[0].description);

            // 목표 2: 나무 5개
            Assert.AreEqual(QuestObjectiveType.GatherItem, q1.objectives[1].type);
            Assert.AreEqual("wood", q1.objectives[1].targetId);
            Assert.AreEqual(5, q1.objectives[1].requiredCount);
            Assert.AreEqual("나무 5개 모으기", q1.objectives[1].description);

            // 목표 3: 돌 3개
            Assert.AreEqual(QuestObjectiveType.GatherItem, q1.objectives[2].type);
            Assert.AreEqual("stone", q1.objectives[2].targetId);
            Assert.AreEqual(3, q1.objectives[2].requiredCount);
            Assert.AreEqual("돌 3개 모으기", q1.objectives[2].description);
        }

        // ================================================================
        // 11. Q2 목표 확인 — 설사초2, 쓴풀1
        // ================================================================

        [Test]
        public void TutorialQuestManager_Q2_Objectives_Correct()
        {
            // Given
            CreateManager();
            EnsureGuideSystem();
            _manager.StartTutorialQuests();

            // When
            QuestData q2 = QuestManager.GetQuest("tutorial_q2_diarrhea");

            // Then
            Assert.AreEqual(2, q2.objectives.Count, "Q2는 2개의 목표");

            // 목표 1: 설사초 2개
            Assert.AreEqual(QuestObjectiveType.GatherItem, q2.objectives[0].type);
            Assert.AreEqual("herb_diarrhea", q2.objectives[0].targetId);
            Assert.AreEqual(2, q2.objectives[0].requiredCount);
            Assert.AreEqual("설사초 2개 채집 (E키)", q2.objectives[0].description);

            // 목표 2: 쓴풀 1개
            Assert.AreEqual(QuestObjectiveType.GatherItem, q2.objectives[1].type);
            Assert.AreEqual("herb_bitter", q2.objectives[1].targetId);
            Assert.AreEqual(1, q2.objectives[1].requiredCount);
            Assert.AreEqual("쓴풀 1개 채집 (E키)", q2.objectives[1].description);
        }

        // ================================================================
        // 12. 두 퀘스트 완료 — 완료 처리 확인
        // ================================================================

        [Test]
        public void TutorialQuestManager_CompleteAllQuests()
        {
            // Given
            int goldBefore = PlayerStats.Instance != null ? PlayerStats.Instance.Gold : 0;
            int expBefore = PlayerStats.Instance != null ? PlayerStats.Instance.CurrentEXP : 0;

            CreateManager();
            EnsureGuideSystem();
            _manager.StartTutorialQuests();
            _manager.SkipAllGuides();

            // When — Q1 목표 모두 달성
            QuestManager.UpdateObjective("tutorial_q1_food", QuestObjectiveType.GatherItem, "meat", 3);
            QuestManager.UpdateObjective("tutorial_q1_food", QuestObjectiveType.GatherItem, "wood", 5);
            QuestManager.UpdateObjective("tutorial_q1_food", QuestObjectiveType.GatherItem, "stone", 3);

            // Q1 완료 시도
            bool q1Result = QuestManager.TryCompleteQuest("tutorial_q1_food");

            // When — Q2 목표 모두 달성
            QuestManager.UpdateObjective("tutorial_q2_diarrhea", QuestObjectiveType.GatherItem, "herb_diarrhea", 2);
            QuestManager.UpdateObjective("tutorial_q2_diarrhea", QuestObjectiveType.GatherItem, "herb_bitter", 1);

            // Q2 완료 시도
            bool q2Result = QuestManager.TryCompleteQuest("tutorial_q2_diarrhea");

            // Then — 두 퀘스트 모두 완료
            Assert.IsTrue(q1Result, "Q1이 완료되어야 함");
            Assert.AreEqual(QuestState.Completed, QuestManager.GetQuestState("tutorial_q1_food"),
                "Q1은 Completed 상태여야 함");

            Assert.IsTrue(q2Result, "Q2가 완료되어야 함");
            Assert.AreEqual(QuestState.Completed, QuestManager.GetQuestState("tutorial_q2_diarrhea"),
                "Q2는 Completed 상태여야 함");

            // AreTutorialQuestsComplete() 확인
            Assert.IsTrue(_manager.AreTutorialQuestsComplete(),
                "두 퀘스트 모두 완료되었으므로 AreTutorialQuestsComplete()는 true");

            // 보상 지급 확인
            if (PlayerStats.Instance != null)
            {
                Assert.AreEqual(goldBefore + 20, PlayerStats.Instance.Gold,
                    "Q1 보상으로 골드 20이 지급되어야 함");
                Assert.AreEqual(expBefore + 80, PlayerStats.Instance.CurrentEXP,
                    "두 퀘스트 보상으로 경험치 80(50+30)이 지급되어야 함");
            }
        }
    }
}