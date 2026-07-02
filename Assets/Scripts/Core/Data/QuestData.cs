using System;
using System.Collections.Generic;
using ProjectName.Core;

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
        /// <summary>목표 유형 (수집, 처치, 대화, 탐험, 제작)</summary>
        public QuestObjectiveType type;
        /// <summary>대상 ID (아이템 ID, 몬스터 ID, NPC ID 등)</summary>
        public string targetId;
        /// <summary>필요 수량</summary>
        public int requiredCount;
        /// <summary>현재 진행 수량</summary>
        public int currentCount;
        /// <summary>목표 설명</summary>
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
        /// <summary>영주 호감도</summary>
        public int affinity;

        /// <summary>보상이 전혀 없는지 여부</summary>
        public bool IsEmpty => gold == 0 && exp == 0 && affinity == 0 && (items == null || items.Count == 0);
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

        /// <summary>대상 영지 ID (예: "East_01") — 퀘스트 마커/웨이포인트에 사용. null/빈 값 = 대상 영지 없음.</summary>
        public string targetTerritoryId;

        public bool AllObjectivesMet
        {
            get
            {
                if (objectives == null) return false;
                if (objectives.Count == 0) return true;
                for (int i = 0; i < objectives.Count; i++)
                {
                    if (!objectives[i].IsMet) return false;
                }
                return true;
            }
        }
    }
}