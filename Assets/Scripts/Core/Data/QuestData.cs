using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Core.Data
{
    /// <summary>C9-30: 퀘스트 상태</summary>
    public enum QuestState
    {
        Locked,     // 잠김 (선행 퀘스트 미완료)
        Available,  // 수락 가능
        Active,     // 진행 중
        Completed,  // 완료
        Failed      // 실패
    }

    /// <summary>C9-30: 퀘스트 목표 유형</summary>
    public enum QuestObjectiveType
    {
        GatherItem,      // 아이템 수집
        KillMonster,     // 몬스터 처치
        TalkToNPC,       // NPC 대화
        ExploreTerritory, // 영지 탐험
        CraftItem        // 아이템 제작
    }

    /// <summary>C9-30: 퀘스트 목표</summary>
    [Serializable]
    public struct QuestObjective
    {
        public QuestObjectiveType type;
        public string targetId;
        public int requiredCount;
        public int currentCount;
        public string description;

        public bool IsMet => currentCount >= requiredCount;
    }

    /// <summary>C9-30: 퀘스트 보상</summary>
    [Serializable]
    public struct QuestReward
    {
        public int gold;
        public int exp;
        public List<PlayerInventory.ItemData> items;
    }

    /// <summary>C9-30: 퀘스트 데이터 정의</summary>
    [Serializable]
    public struct QuestData
    {
        public string questId;
        public string questName;
        public string description;
        public int requiredLevel;
        public List<QuestObjective> objectives;
        public QuestReward reward;
        public string giverNpcId;
        public string[] prerequisiteQuestIds;

        public bool AllObjectivesMet
        {
            get
            {
                if (objectives == null || objectives.Count == 0) return false;
                for (int i = 0; i < objectives.Count; i++)
                {
                    if (!objectives[i].IsMet) return false;
                }
                return true;
            }
        }
    }
}