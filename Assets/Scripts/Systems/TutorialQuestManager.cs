using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// T-Cycle-05: 튜토리얼 퀘스트 자동 발급 + 가이드 큐 관리자.
    ///
    /// - TutorialLordSequence 완료 후 StartTutorialQuests() 호출
    /// - 2개의 튜토리얼 퀘스트를 QuestManager에 등록하고 자동 수락
    /// - 11개 가이드 큐 초기화 (01_movement~11_recipe_book)
    /// - 가이드 완료 시 자동으로 다음 가이드 표시
    /// - 모든 가이드 완료 시 안내 메시지 출력
    /// - 퀘스트 완료 감지 및 처리
    /// - ESC 스킵 처리 (큐 초기화 + 바로 퀘스트 진행 안내)
    /// </summary>
    public class TutorialQuestManager : MonoBehaviour
    {
        // ================================================================
        // 싱글톤
        // ================================================================

        private static TutorialQuestManager _instance;
        private static bool _applicationIsQuitting;

        public static TutorialQuestManager Instance
        {
            get
            {
                if (_applicationIsQuitting)
                    return null;

                if (_instance == null)
                {
                    var go = new GameObject("TutorialQuestManager");
                    _instance = go.AddComponent<TutorialQuestManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // ================================================================
        // 상수
        // ================================================================

        private const string QUEST_ID_FOOD = "tutorial_q1_food";
        private const string QUEST_ID_DIARRHEA = "tutorial_q2_diarrhea";

        private static readonly string[] GUIDE_IDS = new string[]
        {
            TutorialGuideData.ID_01_MOVEMENT,
            TutorialGuideData.ID_02_CAMERA,
            TutorialGuideData.ID_03_ATTACK,
            TutorialGuideData.ID_04_DASH,
            TutorialGuideData.ID_05_ROLL,
            TutorialGuideData.ID_06_CHOP_TREE,
            TutorialGuideData.ID_07_MINE_STONE,
            TutorialGuideData.ID_08_HERB_PICK,
            TutorialGuideData.ID_09_INVENTORY,
            TutorialGuideData.ID_10_CRAFT,
            TutorialGuideData.ID_11_RECIPE_BOOK,
        };

        // ================================================================
        // 내부 상태
        // ================================================================

        private Queue<string> _guideQueue = new Queue<string>();
        private bool _questsRegistered;
        private bool _allGuidesComplete;
        private bool _isSkipped;

        // CheckQuestCompletion 타이머 — 매 프레임 대신 간격 체크
        private float _completionCheckTimer;
        private const float COMPLETION_CHECK_INTERVAL = 0.5f;

        // ================================================================
        // MonoBehaviour 생명주기
        // ================================================================

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                var guideSystem = TutorialGuideSystem.Instance;
                if (guideSystem != null)
                    guideSystem.OnGuideProcessed -= OnGuideProcessed;
                _instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            _applicationIsQuitting = true;
        }

        private void Update()
        {
            // 퀘스트 등록 + 모든 가이드 완료 후에만 퀘스트 완료 체크 (간격 기반)
            if (_questsRegistered && _allGuidesComplete)
            {
                _completionCheckTimer -= Time.deltaTime;
                if (_completionCheckTimer <= 0f)
                {
                    _completionCheckTimer = COMPLETION_CHECK_INTERVAL;
                    CheckQuestCompletion();
                }
            }
        }

        // ================================================================
        // 공개 메서드
        // ================================================================

        /// <summary>
        /// 튜토리얼 퀘스트 등록 + 가이드 큐 시작.
        /// TutorialLordSequence Step7에서 호출됩니다.
        /// </summary>
        public void StartTutorialQuests()
        {
            Debug.Log("[TutorialQuestManager] StartTutorialQuests() 시작");

            // 1. 튜토리얼 퀘스트 등록
            RegisterTutorialQuests();

            // 2. 퀘스트 자동 수락
            AcceptTutorialQuests();

            // 3. TutorialGuideSystem 이벤트 구독
            var guideSystem = TutorialGuideSystem.Instance;
            if (guideSystem != null)
            {
                guideSystem.OnGuideProcessed -= OnGuideProcessed; // 중복 방지
                guideSystem.OnGuideProcessed += OnGuideProcessed;
            }

            // 4. 가이드 큐 초기화
            InitializeGuideQueue();

            // 5. 첫 번째 가이드 자동 표시
            ShowNextGuide();
        }

        /// <summary>
        /// 모든 가이드를 스킵합니다 (ESC 스킵).
        /// 큐를 비우고 바로 퀘스트 진행 안내 메시지를 표시합니다.
        /// </summary>
        public void SkipAllGuides()
        {
            if (_allGuidesComplete)
                return;

            Debug.Log("[TutorialQuestManager] 모든 가이드 스킵 (ESC)");
            _isSkipped = true;

            // 큐 초기화
            _guideQueue.Clear();

            // 현재 표시 중인 가이드 종료 (TutorialGuideSystem에서 ESC 처리)

            // 모든 가이드 완료 처리
            CompleteAllGuides();
        }

        /// <summary>
        /// T-Cycle-06: TutorialActionDetector에서 11종 액션 모두 감지 시 호출.
        /// 가이드 큐의 남은 항목과 무관하게 모든 가이드를 완료 처리합니다.
        /// </summary>
        public void OnAllGuidesComplete()
        {
            CompleteAllGuides();
        }

        /// <summary>
        /// 퀘스트가 이미 등록되었는지 확인합니다.
        /// </summary>
        public bool IsQuestsRegistered => _questsRegistered;

        /// <summary>
        /// 모든 가이드가 완료되었는지 확인합니다.
        /// </summary>
        public bool IsAllGuidesComplete => _allGuidesComplete;

        /// <summary>
        /// 현재 가이드 큐에 남은 개수를 반환합니다.
        /// </summary>
        public int RemainingGuideCount => _guideQueue.Count;

        /// <summary>
        /// 튜토리얼 퀘스트가 모두 완료되었는지 확인합니다.
        /// </summary>
        public bool AreTutorialQuestsComplete()
        {
            QuestState q1 = QuestManager.GetQuestState(QUEST_ID_FOOD);
            QuestState q2 = QuestManager.GetQuestState(QUEST_ID_DIARRHEA);
            return q1 == QuestState.Completed && q2 == QuestState.Completed;
        }

        /// <summary>
        /// Q1의 questId를 반환합니다.
        /// </summary>
        public string QuestIdFood => QUEST_ID_FOOD;

        /// <summary>
        /// Q2의 questId를 반환합니다.
        /// </summary>
        public string QuestIdDiarrhea => QUEST_ID_DIARRHEA;

        // ================================================================
        // 내부 메서드 — 퀘스트 등록
        // ================================================================

        private void RegisterTutorialQuests()
        {
            if (_questsRegistered) return;

            // Q1: 음식 재료 구하기 — 고기 3개, 나무 5개, 돌 3개
            QuestManager.RegisterQuest(new QuestData
            {
                questId = QUEST_ID_FOOD,
                questName = "음식 재료 구하기",
                description = "영주를 위해 음식을 만들어 주세요. 고기 3개, 나무 5개, 돌 3개가 필요합니다.",
                requiredLevel = 1,
                giverNpcId = "npc_tutorial_lord",
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.GatherItem, targetId = "meat", requiredCount = 3, currentCount = 0, description = "고기 3개 모으기" },
                    new QuestObjective { type = QuestObjectiveType.GatherItem, targetId = "wood", requiredCount = 5, currentCount = 0, description = "나무 5개 모으기" },
                    new QuestObjective { type = QuestObjectiveType.GatherItem, targetId = "stone", requiredCount = 3, currentCount = 0, description = "돌 3개 모으기" }
                },
                reward = new QuestReward { gold = 20, exp = 50 }
            });

            // Q2: 설사약 재료 구하기 — 설사초 2개, 쓴풀 1개
            QuestManager.RegisterQuest(new QuestData
            {
                questId = QUEST_ID_DIARRHEA,
                questName = "설사약 재료 구하기",
                description = "영주가 배가 아파요! 설사초 2개와 쓴풀 1개를 채집해 주세요. (E키로 약초 채집)",
                requiredLevel = 1,
                giverNpcId = "npc_tutorial_lord",
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.GatherItem, targetId = "herb_diarrhea", requiredCount = 2, currentCount = 0, description = "설사초 2개 채집 (E키)" },
                    new QuestObjective { type = QuestObjectiveType.GatherItem, targetId = "herb_bitter", requiredCount = 1, currentCount = 0, description = "쓴풀 1개 채집 (E키)" }
                },
                reward = new QuestReward { gold = 0, exp = 30 }
            });

            _questsRegistered = true;
            Debug.Log($"[TutorialQuestManager] 튜토리얼 퀘스트 2개 등록 완료");
        }

        private void AcceptTutorialQuests()
        {
            bool q1 = QuestManager.AcceptQuest(QUEST_ID_FOOD);
            bool q2 = QuestManager.AcceptQuest(QUEST_ID_DIARRHEA);

            if (q1)
                Debug.Log($"[TutorialQuestManager] ✅ Q1 수락: {QUEST_ID_FOOD}");
            else
                Debug.LogWarning($"[TutorialQuestManager] ❌ Q1 수락 실패: {QUEST_ID_FOOD} (이미 활성화됨?)");

            if (q2)
                Debug.Log($"[TutorialQuestManager] ✅ Q2 수락: {QUEST_ID_DIARRHEA}");
            else
                Debug.LogWarning($"[TutorialQuestManager] ❌ Q2 수락 실패: {QUEST_ID_DIARRHEA} (이미 활성화됨?)");
        }

        // ================================================================
        // 내부 메서드 — 가이드 큐
        // ================================================================

        private void InitializeGuideQueue()
        {
            _guideQueue.Clear();
            _allGuidesComplete = false;
            _isSkipped = false;

            // 아직 표시되지 않은 가이드만 큐에 추가
            var guideSystem = TutorialGuideSystem.Instance;
            foreach (string guideId in GUIDE_IDS)
            {
                if (guideSystem != null && !guideSystem.HasGuideBeenShown(guideId))
                {
                    _guideQueue.Enqueue(guideId);
                }
            }

            Debug.Log($"[TutorialQuestManager] 가이드 큐 초기화: {_guideQueue.Count}개 대기 (총 {GUIDE_IDS.Length}개 중)");
        }

        private void ShowNextGuide()
        {
            if (_guideQueue.Count == 0)
            {
                CompleteAllGuides();
                return;
            }

            var guideSystem = TutorialGuideSystem.Instance;
            if (guideSystem == null)
            {
                Debug.LogError("[TutorialQuestManager] TutorialGuideSystem.Instance is null — 가이드를 표시할 수 없습니다");
                return;
            }

            string nextId = _guideQueue.Dequeue();
            guideSystem.ShowGuide(nextId);
            Debug.Log($"[TutorialQuestManager] 가이드 표시: '{nextId}' (남은 큐: {_guideQueue.Count})");
        }

        private void CompleteAllGuides()
        {
            if (_allGuidesComplete) return;

            _allGuidesComplete = true;
            Debug.Log("[TutorialQuestManager] 모든 가이드 완료");

            // "이제 퀘스트 재료를 모으세요!" 메시지 표시
            ShowQuestStartMessage();
        }

        private void ShowQuestStartMessage()
        {
            string msg = "📋 이제 퀘스트 재료를 모으세요!\n\n"
                + "Q1: 음식 재료 구하기\n"
                + "  - 고기 3개\n"
                + "  - 나무 5개\n"
                + "  - 돌 3개\n"
                + "  → 보상: 경험치 50, 골드 20\n\n"
                + "Q2: 설사약 재료 구하기\n"
                + "  - 설사초 2개 (E키로 약초 채집)\n"
                + "  - 쓴풀 1개\n"
                + "  → 보상: 경험치 30\n";

            Debug.Log($"[TutorialQuestManager] {msg}");
        }

        // ================================================================
        // 이벤트 핸들러
        // ================================================================

        private void OnGuideProcessed(string guideId, bool wasSkipped)
        {
            if (_allGuidesComplete)
                return;

            Debug.Log($"[TutorialQuestManager] 가이드 처리됨: '{guideId}' (스킵={wasSkipped})");

            // ESC 스킵 시 모든 가이드 스킵
            if (wasSkipped)
            {
                SkipAllGuides();
                return;
            }

            // 다음 가이드 표시
            ShowNextGuide();
        }

        // ================================================================
        // 퀘스트 완료 체크
        // ================================================================

        private void CheckQuestCompletion()
        {
            // Q1 체크
            if (QuestManager.GetQuestState(QUEST_ID_FOOD) == QuestState.Active)
            {
                if (QuestManager.TryCompleteQuest(QUEST_ID_FOOD))
                {
                    Debug.Log("[TutorialQuestManager] ✅ Q1 완료: 음식 재료 구하기!");
                    OnTutorialQuestComplete(QUEST_ID_FOOD);
                }
            }

            // Q2 체크
            if (QuestManager.GetQuestState(QUEST_ID_DIARRHEA) == QuestState.Active)
            {
                if (QuestManager.TryCompleteQuest(QUEST_ID_DIARRHEA))
                {
                    Debug.Log("[TutorialQuestManager] ✅ Q2 완료: 설사약 재료 구하기!");
                    OnTutorialQuestComplete(QUEST_ID_DIARRHEA);
                }
            }

            // 모든 퀘스트 완료 확인
            if (AreTutorialQuestsComplete())
            {
                Debug.Log("[TutorialQuestManager] 🎉 모든 튜토리얼 퀘스트 완료!");
            }
        }

        private void OnTutorialQuestComplete(string questId)
        {
            Debug.Log($"[TutorialQuestManager] 튜토리얼 퀘스트 완료 처리: {questId}");

            if (questId == QUEST_ID_FOOD)
            {
                Debug.Log("[TutorialQuestManager] 음식 재료 퀘스트 완료 — 보상: 경험치 50, 골드 20 지급됨");
            }
            else if (questId == QUEST_ID_DIARRHEA)
            {
                Debug.Log("[TutorialQuestManager] 설사약 재료 퀘스트 완료 — 보상: 경험치 30 지급됨");
            }
        }
    }
}