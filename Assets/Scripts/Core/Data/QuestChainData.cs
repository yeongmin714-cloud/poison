using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Core.Data
{
    /// <summary>
    /// Phase 39: 선택지 조건 유형
    /// </summary>
    public enum QuestChoiceConditionType
    {
        None,       // 조건 없음
        Level,      // 플레이어 레벨 조건
        Affinity,   // 호감도 조건 (영지/국가)
        Item,       // 아이템 소지 조건
        QuestComplete, // 특정 퀘스트 완료 조건
        Reputation  // 국가 평판 조건
    }

    /// <summary>
    /// Phase 39: 선택지 결과 — 다음 노드, 보상, 영향
    /// </summary>
    [Serializable]
    public struct QuestChoiceResult
    {
        /// <summary>선택 시 이동할 다음 노드 ID (비우면 체인 종료)</summary>
        public string nextNodeId;
        /// <summary>선택 시 즉시 지급될 골드</summary>
        public int goldReward;
        /// <summary>선택 시 즉시 지급될 경험치</summary>
        public int expReward;
        /// <summary>영지 호감도 변화 (territoryId_to_affinityDelta)</summary>
        public List<AffinityChange> affinityChanges;
        /// <summary>국가 평판 변화 (nationType_to_reputationDelta)</summary>
        public List<ReputationChange> reputationChanges;
        /// <summary>병사 수 변화 (영지 ID → 변화량)</summary>
        public List<SoldierChange> soldierChanges;
        /// <summary>선택 시 트리거할 다이내믹 이벤트 (Phase 36)</summary>
        public string triggerEventType;
        /// <summary>선택 완료 후 표시할 결과 텍스트</summary>
        public string resultText;
    }

    /// <summary>호감도 변화</summary>
    [Serializable]
    public struct AffinityChange
    {
        public string territoryId;
        public int delta;
    }

    /// <summary>국가 평판 변화</summary>
    [Serializable]
    public struct ReputationChange
    {
        public string nationType; // NationType.ToString()
        public int delta;
    }

    /// <summary>병사 수 변화</summary>
    [Serializable]
    public struct SoldierChange
    {
        public string territoryId;
        public int delta;
    }

    /// <summary>
    /// Phase 39: 선택지 조건
    /// </summary>
    [Serializable]
    public struct QuestChoiceCondition
    {
        public QuestChoiceConditionType type;
        /// <summary>조건 대상 ID (영지ID, 아이템ID, 퀘스트ID 등)</summary>
        public string targetId;
        /// <summary>비교 값 (레벨, 호감도, 수량 등)</summary>
        public int value;
        /// <summary>조건 불만족 시 표시할 회색 툴팁</summary>
        public string failMessage;
    }

    /// <summary>
    /// Phase 39: 퀘스트 체인 선택지
    /// </summary>
    [Serializable]
    public struct QuestChoice
    {
        /// <summary>선택지 표시 텍스트</summary>
        public string text;
        /// <summary>선택 조건 (없으면 항상 선택 가능)</summary>
        public QuestChoiceCondition condition;
        /// <summary>선택 결과</summary>
        public QuestChoiceResult result;
    }

    /// <summary>
    /// Phase 39: 퀘스트 체인 노드 (단일 퀘스트 단계)
    /// </summary>
    [Serializable]
    public struct QuestChainNode
    {
        /// <summary>노드 고유 ID (체인 내 유일)</summary>
        public string id;
        /// <summary>노드 제목</summary>
        public string title;
        /// <summary>노드 설명</summary>
        public string description;
        /// <summary>목표 설명 배열 (진행도 표시용)</summary>
        public string[] objectives;
        /// <summary>선택지 배열 (1~4개, 없으면 자동 진행)</summary>
        public QuestChoice[] choices;
    }

    /// <summary>
    /// Phase 39: 퀘스트 체인 ScriptableObject
    /// 체인 ID, 제목, 노드 배열을 포함합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewQuestChain", menuName = "Quest/Quest Chain", order = 1)]
    public class QuestChainData : ScriptableObject
    {
        /// <summary>체인 고유 ID</summary>
        public string chainId;
        /// <summary>체인 제목</summary>
        public string chainTitle;
        /// <summary>체인 설명</summary>
        public string chainDescription;
        /// <summary>필요 최소 레벨</summary>
        public int requiredLevel;
        /// <summary>체인 노드 배열</summary>
        public QuestChainNode[] nodes;
        /// <summary>선행 퀘스트 체인 ID (없으면 없음)</summary>
        public string prerequisiteChainId;

        /// <summary>
        /// 특정 노드를 ID로 조회
        /// </summary>
        public QuestChainNode GetNode(string nodeId)
        {
            if (nodes == null) return default;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].id == nodeId)
                    return nodes[i];
            }
            Debug.LogWarning($"[QuestChainData] 노드 없음: {nodeId} in chain {chainId}");
            return default;
        }

        /// <summary>
        /// 특정 노드의 인덱스 반환
        /// </summary>
        public int GetNodeIndex(string nodeId)
        {
            if (nodes == null) return -1;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].id == nodeId)
                    return i;
            }
            return -1;
        }
    }
}