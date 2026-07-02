using System.Collections.Generic;
using ProjectName.Core.Data;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Core
{
    /// <summary>
    /// C9-30: 퀘스트 진행 관리자 (static singleton)
    /// </summary>
    public static class QuestManager
    {
        private static Dictionary<string, QuestData> _allQuests = new Dictionary<string, QuestData>();
        private static Dictionary<string, QuestState> _questStates = new Dictionary<string, QuestState>();

        /// <summary>모든 퀘스트 정의 초기화</summary>
        public static void Initialize()
        {
            _allQuests.Clear();
            _questStates.Clear();
            DefineQuests();
        }

        /// <summary>모든 상태 및 진행도 초기화 (테스트용).</summary>
        public static void ResetAll()
        {
            _questStates.Clear();
            // 각 퀘스트의 목표 진행도(currentCount)도 함께 리셋
            foreach (var kvp in _allQuests)
            {
                var quest = kvp.Value;
                if (quest.objectives != null)
                {
                    for (int i = 0; i < quest.objectives.Count; i++)
                    {
                        var obj = quest.objectives[i];
                        obj.currentCount = 0;
                        quest.objectives[i] = obj;
                    }
                    _allQuests[kvp.Key] = quest;
                }
                _questStates[kvp.Key] = CalculateInitialState(quest);
            }
        }

        private static QuestState CalculateInitialState(QuestData quest)
        {
            if (quest.prerequisiteQuestIds != null && quest.prerequisiteQuestIds.Length > 0)
            {
                // 선행 퀘스트가 모두 Completed여야 Available
                for (int i = 0; i < quest.prerequisiteQuestIds.Length; i++)
                {
                    string prereqId = quest.prerequisiteQuestIds[i];
                    if (!_questStates.TryGetValue(prereqId, out QuestState prereqState) || prereqState != QuestState.Completed)
                        return QuestState.Locked;
                }
            }
            return QuestState.Available;
        }

        // ===== 데이터 조회 =====

        public static QuestData GetQuest(string questId)
        {
            if (string.IsNullOrEmpty(questId))
                return default;
            if (_allQuests.TryGetValue(questId, out QuestData quest))
                return quest;
            return default;
        }

        public static QuestState GetQuestState(string questId)
        {
            if (string.IsNullOrEmpty(questId))
                return QuestState.Locked;
            if (_questStates.TryGetValue(questId, out QuestState state))
                return state;
            return QuestState.Locked;
        }

        public static List<QuestData> GetActiveQuests()
        {
            var result = new List<QuestData>();
            foreach (var kvp in _questStates)
            {
                if (kvp.Value == QuestState.Active && _allQuests.ContainsKey(kvp.Key))
                    result.Add(_allQuests[kvp.Key]);
            }
            return result;
        }

        public static List<QuestData> GetCompletedQuests()
        {
            var result = new List<QuestData>();
            foreach (var kvp in _questStates)
            {
                if (kvp.Value == QuestState.Completed && _allQuests.ContainsKey(kvp.Key))
                    result.Add(_allQuests[kvp.Key]);
            }
            return result;
        }

        /// <summary>
        /// Returns all quest definitions registered in the system.
        /// </summary>
        public static List<QuestData> GetAllDefinitions()
        {
            return new List<QuestData>(_allQuests.Values);
        }

        public static List<QuestData> GetAvailableQuests(int playerLevel)
        {
            var result = new List<QuestData>();
            foreach (var kvp in _allQuests)
            {
                if (kvp.Value.requiredLevel > playerLevel) continue;
                _questStates.TryGetValue(kvp.Key, out QuestState state);

                if (state == QuestState.Available)
                {
                    result.Add(kvp.Value);
                }
                else if (state == QuestState.Locked)
                {
                    // 선행 조건 다시 확인 (동적 업데이트)
                    bool prereqsMet = true;
                    if (kvp.Value.prerequisiteQuestIds != null)
                    {
                        for (int i = 0; i < kvp.Value.prerequisiteQuestIds.Length; i++)
                        {
                            string pid = kvp.Value.prerequisiteQuestIds[i];
                            if (!_questStates.TryGetValue(pid, out QuestState prereqState) || prereqState != QuestState.Completed)
                            { prereqsMet = false; break; }
                        }
                    }
                    if (prereqsMet)
                    {
                        _questStates[kvp.Key] = QuestState.Available;
                        result.Add(kvp.Value);
                    }
                }
            }
            return result;
        }

        // ===== 퀘스트 진행 =====

        public static bool AcceptQuest(string questId)
        {
            if (string.IsNullOrEmpty(questId) || !_allQuests.ContainsKey(questId))
                return false;

            if (!_questStates.TryGetValue(questId, out QuestState state) || state != QuestState.Available)
                return false;

            _questStates[questId] = QuestState.Active;

            // Current count 초기화
            var quest = _allQuests[questId];
            if (quest.objectives != null)
            {
                for (int i = 0; i < quest.objectives.Count; i++)
                {
                    var obj = quest.objectives[i];
                    obj.currentCount = 0;
                    quest.objectives[i] = obj;
                }
                _allQuests[questId] = quest;
            }

            Debug.Log($"[QuestManager] 📋 퀘스트 수락: {quest.questName} ({questId})");
            return true;
        }

        public static void UpdateObjective(string questId, QuestObjectiveType type, string targetId, int count = 1)
        {
            if (string.IsNullOrEmpty(questId) || !_allQuests.ContainsKey(questId))
                return;

            if (!_questStates.TryGetValue(questId, out QuestState state) || state != QuestState.Active)
                return;

            var quest = _allQuests[questId];
            if (quest.objectives == null) return;

            bool changed = false;
            for (int i = 0; i < quest.objectives.Count; i++)
            {
                var obj = quest.objectives[i];
                if (obj.type == type && obj.targetId == targetId && obj.currentCount < obj.requiredCount)
                {
                    int newCount = Mathf.Min(obj.requiredCount, obj.currentCount + count);
                    obj.currentCount = newCount;
                    quest.objectives[i] = obj;
                    changed = true;
                    break;
                }
            }

            if (changed)
                _allQuests[questId] = quest;
        }

        public static bool TryCompleteQuest(string questId)
        {
            if (string.IsNullOrEmpty(questId) || !_allQuests.ContainsKey(questId))
                return false;

            if (!_questStates.TryGetValue(questId, out QuestState state) || state != QuestState.Active)
                return false;

            var quest = _allQuests[questId];
            if (!quest.AllObjectivesMet)
                return false;

            // 보상 지급
            GiveRewards(quest);

            _questStates[questId] = QuestState.Completed;
            Debug.Log($"[QuestManager] ✅ 퀘스트 완료: {quest.questName} ({questId})");
            return true;
        }

        public static void FailQuest(string questId)
        {
            if (string.IsNullOrEmpty(questId))
                return;

            if (_questStates.TryGetValue(questId, out QuestState state) && state == QuestState.Active)
            {
                _questStates[questId] = QuestState.Failed;
                Debug.Log($"[QuestManager] ❌ 퀘스트 실패: {questId}");
            }
        }

        /// <summary>테스트 헬퍼: 강제 상태 변경</summary>
        public static void ForceState(string questId, QuestState state)
        {
            if (!string.IsNullOrEmpty(questId))
                _questStates[questId] = state;
        }

        // ===== 보상 =====

        private static void GiveRewards(QuestData quest)
        {
            if (quest.reward.gold > 0 && PlayerStats.Instance != null)
                PlayerStats.Instance.AddGold(quest.reward.gold);

            if (quest.reward.exp > 0 && PlayerStats.Instance != null)
                PlayerStats.Instance.AddEXP(quest.reward.exp);

            if (quest.reward.items != null && PlayerInventory.Instance != null)
            {
                for (int i = 0; i < quest.reward.items.Count; i++)
                {
                    PlayerInventory.Instance.AddItem(quest.reward.items[i], 1);
                }
            }

            if (quest.reward.affinity > 0)
            {
                // 영주 호감도 지급
                Debug.Log($"[QuestManager] 🏰 영주 호감도 +{quest.reward.affinity} (퀘스트: {quest.questName})");
            }
        }

        // ===== 퀘스트 정의 =====

        private static void DefineQuests()
        {
            AddQuest(new QuestData
            {
                questId = "first_gather",
                questName = "기초 약초 채집",
                description = "주변에서 약초 3개를 채집하세요.",
                requiredLevel = 1,
                giverNpcId = "npc_001",
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.GatherItem, targetId = "herb_red", requiredCount = 3, currentCount = 0, description = "약초 3개 채집" }
                },
                reward = new QuestReward { gold = 10, exp = 20 }
            });

            AddQuest(new QuestData
            {
                questId = "first_hunt",
                questName = "토끼 사냥",
                description = "토끼 2마리를 사냥하여 고기를 얻으세요.",
                requiredLevel = 1,
                giverNpcId = "npc_001",
                prerequisiteQuestIds = new[] { "first_gather" },
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.KillMonster, targetId = "rabbit", requiredCount = 2, currentCount = 0, description = "토끼 2마리 사냥" }
                },
                reward = new QuestReward { gold = 20, exp = 30 }
            });

            AddQuest(new QuestData
            {
                questId = "first_craft",
                questName = "첫 번째 제작",
                description = "크래프트 테이블에서 아이템 1개를 제작하세요.",
                requiredLevel = 1,
                giverNpcId = "npc_002",
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.CraftItem, targetId = "any", requiredCount = 1, currentCount = 0, description = "아이템 1개 제작" }
                },
                reward = new QuestReward { gold = 15, exp = 25 }
            });

            AddQuest(new QuestData
            {
                questId = "visit_shop",
                questName = "상점 방문",
                description = "영지의 상점을 방문하여 물건을 구경하세요.",
                requiredLevel = 1,
                giverNpcId = "npc_002",
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.TalkToNPC, targetId = "shopkeeper", requiredCount = 1, currentCount = 0, description = "상점 방문" }
                },
                reward = new QuestReward { gold = 5, exp = 10 }
            });

            AddQuest(new QuestData
            {
                questId = "gather_iron",
                questName = "철광석 채굴",
                description = "철광석 5개를 채굴하여 제련소로 가져오세요.",
                requiredLevel = 3,
                giverNpcId = "npc_miner",
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.GatherItem, targetId = "iron_ore", requiredCount = 5, currentCount = 0, description = "철광석 5개 채굴" }
                },
                reward = new QuestReward { gold = 30, exp = 50 }
            });
        }

        private static void AddQuest(QuestData quest)
        {
            _allQuests[quest.questId] = quest;
            _questStates[quest.questId] = quest.prerequisiteQuestIds != null && quest.prerequisiteQuestIds.Length > 0
                ? QuestState.Locked : QuestState.Available;
        }

        /// <summary>
        /// 외부에서 새 퀘스트를 등록합니다 (예: 튜토리얼 퀘스트).
        /// 동일한 questId가 이미 존재하면 덮어씁니다.
        /// </summary>
        public static void RegisterQuest(QuestData quest)
        {
            if (string.IsNullOrEmpty(quest.questId))
            {
                Debug.LogWarning("[QuestManager] RegisterQuest: questId가 null/비어있음");
                return;
            }
            _allQuests[quest.questId] = quest;
            _questStates[quest.questId] = quest.prerequisiteQuestIds != null && quest.prerequisiteQuestIds.Length > 0
                ? QuestState.Locked : QuestState.Available;
            Debug.Log($"[QuestManager] 퀘스트 등록됨: '{quest.questId}' — {quest.questName}");
        }
    }
}