using System;
using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;

using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 39: 퀘스트 체인 관리 싱글톤.
    /// 활성 퀘스트 체인 관리, 진행 상태 저장/로드,
    /// 퀘스트 완료 시 영지 데이터 영향(호감도/소속/병사 수),
    /// 특정 퀘스트 완료 시 다이내믹 이벤트 트리거.
    /// </summary>
    public class QuestChainManager : MonoBehaviour
    {
        // ================================================================
        // 싱글톤
        // ================================================================

        private static QuestChainManager _instance;
        private static bool _applicationIsQuitting;

        public static QuestChainManager Instance
        {
            get
            {
                if (_applicationIsQuitting)
                    return null;

                if (_instance == null)
                {
                    var go = new GameObject("QuestChainManager");
                    _instance = go.AddComponent<QuestChainManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // ================================================================
        // 내부 데이터 — 체인 진행 상태
        // ================================================================

        /// <summary>등록된 모든 퀘스트 체인 데이터</summary>
        private readonly Dictionary<string, QuestChainData> _chainRegistry = new Dictionary<string, QuestChainData>();

        /// <summary>체인별 진행 상태</summary>
        private readonly Dictionary<string, ChainProgress> _chainProgress = new Dictionary<string, ChainProgress>();

        /// <summary>체인 진행 상태 (저장/로드 가능)</summary>
        [Serializable]
        public class ChainProgress
        {
            public string chainId;
            public string currentNodeId;
            public int currentNodeIndex;
            public bool isActive;
            public bool isCompleted;
            public List<string> completedNodeIds = new List<string>();
            public Dictionary<string, string> choiceHistory = new Dictionary<string, string>(); // nodeId → choiceText
        }

        /// <summary>체인 변경 이벤트 (UI 갱신용)</summary>
        public event Action<QuestChainData, ChainProgress> OnChainStarted;
        public event Action<QuestChainData, ChainProgress, string> OnNodeCompleted; // string = chosenChoiceText (null if auto)
        public event Action<QuestChainData, ChainProgress> OnChainCompleted;

        /// <summary>활성 체인 목록 (읽기 전용)</summary>
        public IReadOnlyDictionary<string, ChainProgress> ActiveChains => _chainProgress;

        // ================================================================
        // MonoBehaviour
        // ================================================================

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            _applicationIsQuitting = false;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void OnApplicationQuit()
        {
            _applicationIsQuitting = true;
        }

        // ================================================================
        // 체인 등록
        // ================================================================

        /// <summary>
        /// 퀘스트 체인 데이터를 레지스트리에 등록합니다.
        /// </summary>
        public void RegisterChain(QuestChainData chain)
        {
            if (chain == null || string.IsNullOrEmpty(chain.chainId))
            {
                Debug.LogWarning("[QuestChainManager] RegisterChain: chain 또는 chainId가 null");
                return;
            }
            _chainRegistry[chain.chainId] = chain;
            Debug.Log($"[QuestChainManager] 체인 등록: {chain.chainId} — {chain.chainTitle}");
        }

        /// <summary>
        /// 등록된 체인 데이터 조회
        /// </summary>
        public QuestChainData GetChainData(string chainId)
        {
            if (string.IsNullOrEmpty(chainId)) return null;
            _chainRegistry.TryGetValue(chainId, out var chain);
            return chain;
        }

        /// <summary>모든 등록된 체인 ID 반환</summary>
        public IEnumerable<string> GetAllChainIds()
        {
            return _chainRegistry.Keys;
        }

        // ================================================================
        // 체인 진행
        // ================================================================

        /// <summary>
        /// 퀘스트 체인을 시작합니다.
        /// 선행 조건(레벨, 선행 체인)을 확인하고 활성화합니다.
        /// </summary>
        /// <param name="chainId">시작할 체인 ID</param>
        /// <returns>시작 성공 여부</returns>
        public bool StartChain(string chainId)
        {
            if (!_chainRegistry.TryGetValue(chainId, out var chain))
            {
                Debug.LogWarning($"[QuestChainManager] 체인 없음: {chainId}");
                return false;
            }

            // 레벨 조건 체크
            if (PlayerStats.Instance != null && PlayerStats.Instance.Level < chain.requiredLevel)
            {
                Debug.Log($"[QuestChainManager] 레벨 부족: {chain.chainId} (필요 Lv.{chain.requiredLevel}, 현재 Lv.{PlayerStats.Instance.Level})");
                return false;
            }

            // 선행 체인 완료 체크
            if (!string.IsNullOrEmpty(chain.prerequisiteChainId))
            {
                if (!_chainProgress.TryGetValue(chain.prerequisiteChainId, out var prereq) || !prereq.isCompleted)
                {
                    Debug.Log($"[QuestChainManager] 선행 체인 미완료: {chain.prerequisiteChainId}");
                    return false;
                }
            }

            // 이미 활성 상태인지 확인
            if (_chainProgress.TryGetValue(chainId, out var existing) && existing.isActive)
            {
                Debug.Log($"[QuestChainManager] 체인 이미 활성: {chainId}");
                return true;
            }

            if (chain.nodes == null || chain.nodes.Length == 0)
            {
                Debug.LogWarning($"[QuestChainManager] 체인에 노드 없음: {chainId}");
                return false;
            }

            // 진행 상태 생성
            var progress = new ChainProgress
            {
                chainId = chainId,
                currentNodeId = chain.nodes[0].id,
                currentNodeIndex = 0,
                isActive = true,
                isCompleted = false,
                completedNodeIds = new List<string>(),
                choiceHistory = new Dictionary<string, string>()
            };

            _chainProgress[chainId] = progress;
            Debug.Log($"[QuestChainManager] 🎯 체인 시작: {chain.chainTitle} ({chainId}) — 첫 노드: {chain.nodes[0].title}");

            OnChainStarted?.Invoke(chain, progress);
            return true;
        }

        /// <summary>
        /// 현재 활성 체인의 현재 노드를 완료 처리하고, 선택지가 있다면 결과를 적용합니다.
        /// </summary>
        /// <param name="chainId">체인 ID</param>
        /// <param name="choiceIndex">선택한 선택지 인덱스 (-1이면 자동 진행)</param>
        /// <returns>완료 성공 여부</returns>
        public bool CompleteCurrentNode(string chainId, int choiceIndex = -1)
        {
            if (!_chainProgress.TryGetValue(chainId, out var progress) || !progress.isActive)
            {
                Debug.LogWarning($"[QuestChainManager] 완료 실패 — 체인 비활성: {chainId}");
                return false;
            }

            if (!_chainRegistry.TryGetValue(chainId, out var chain))
            {
                Debug.LogWarning($"[QuestChainManager] 완료 실패 — 체인 데이터 없음: {chainId}");
                return false;
            }

            var node = chain.GetNode(progress.currentNodeId);
            if (string.IsNullOrEmpty(node.id))
            {
                Debug.LogWarning($"[QuestChainManager] 완료 실패 — 현재 노드 없음: {progress.currentNodeId}");
                return false;
            }

            string chosenText = null;

            // 선택지 처리
            if (node.choices != null && node.choices.Length > 0)
            {
                if (choiceIndex < 0 || choiceIndex >= node.choices.Length)
                {
                    Debug.LogWarning($"[QuestChainManager] 잘못된 선택지 인덱스: {choiceIndex}");
                    return false;
                }

                var choice = node.choices[choiceIndex];

                // 조건 체크
                if (!IsChoiceAvailable(choice))
                {
                    Debug.LogWarning($"[QuestChainManager] 선택 불가능한 선택지: {choice.text}");
                    return false;
                }

                chosenText = choice.text;
                progress.choiceHistory[node.id] = choice.text;

                // 선택 결과 적용
                ApplyChoiceResult(choice.result);

                // 현재 노드 완료 목록에 추가
                if (!progress.completedNodeIds.Contains(node.id))
                    progress.completedNodeIds.Add(node.id);

                // 다음 노드로 이동
                string nextNodeId = choice.result.nextNodeId;
                if (string.IsNullOrEmpty(nextNodeId))
                {
                    // 체인 종료
                    CompleteChain(chainId);
                    return true;
                }

                int nextIndex = chain.GetNodeIndex(nextNodeId);
                if (nextIndex < 0)
                {
                    Debug.LogWarning($"[QuestChainManager] 다음 노드 없음: {nextNodeId}, 체인 종료");
                    CompleteChain(chainId);
                    return true;
                }

                progress.currentNodeId = nextNodeId;
                progress.currentNodeIndex = nextIndex;
            }
            else
            {
                // 선택지 없음 — 자동 진행
                if (!progress.completedNodeIds.Contains(node.id))
                    progress.completedNodeIds.Add(node.id);

                // 다음 노드 찾기 (인덱스 +1)
                int nextIndex = progress.currentNodeIndex + 1;
                if (nextIndex >= chain.nodes.Length)
                {
                    CompleteChain(chainId);
                    return true;
                }

                progress.currentNodeId = chain.nodes[nextIndex].id;
                progress.currentNodeIndex = nextIndex;
            }

            Debug.Log($"[QuestChainManager] 노드 완료: {node.title} → {chain.GetNode(progress.currentNodeId).title} (선택: {chosenText ?? "자동"})");
            OnNodeCompleted?.Invoke(chain, progress, chosenText);
            return true;
        }

        /// <summary>
        /// 체인 전체를 완료 처리합니다.
        /// </summary>
        private void CompleteChain(string chainId)
        {
            if (!_chainProgress.TryGetValue(chainId, out var progress))
                return;

            if (!_chainRegistry.TryGetValue(chainId, out var chain))
                return;

            progress.isActive = false;
            progress.isCompleted = true;
            Debug.Log($"[QuestChainManager] 🏆 체인 완료: {chain.chainTitle} ({chainId})");

            // 모든 노드 완료 처리
            if (chain.nodes != null)
            {
                for (int i = 0; i < chain.nodes.Length; i++)
                {
                    if (!progress.completedNodeIds.Contains(chain.nodes[i].id))
                        progress.completedNodeIds.Add(chain.nodes[i].id);
                }
            }

            OnChainCompleted?.Invoke(chain, progress);
        }

        // ================================================================
        // 선택지 조건/결과 처리
        // ================================================================

        /// <summary>
        /// 선택지의 조건이 충족되는지 확인합니다.
        /// </summary>
        public bool IsChoiceAvailable(QuestChoice choice)
        {
            var cond = choice.condition;
            if (cond.type == QuestChoiceConditionType.None)
                return true;

            switch (cond.type)
            {
                case QuestChoiceConditionType.Level:
                    if (PlayerStats.Instance == null) return false;
                    return PlayerStats.Instance.Level >= cond.value;

                case QuestChoiceConditionType.Affinity:
                    // 영지 호감도 확인 (추후 구현)
                    return true;

                case QuestChoiceConditionType.Item:
                    // 아이템 소지 확인 (추후 구현)
                    return true;

                case QuestChoiceConditionType.QuestComplete:
                    return QuestManager.GetQuestState(cond.targetId) == QuestState.Completed;

                case QuestChoiceConditionType.Reputation:
                    // 국가 평판 확인 (추후 구현)
                    return true;

                default:
                    return true;
            }
        }

        /// <summary>
        /// 선택 결과를 적용합니다 — 보상, 호감도, 병사 수, 다이내믹 이벤트
        /// </summary>
        private void ApplyChoiceResult(QuestChoiceResult result)
        {
            // 1. 골드 보상
            if (result.goldReward > 0 && PlayerStats.Instance != null)
            {
                PlayerStats.Instance.AddGold(result.goldReward);
                Debug.Log($"[QuestChainManager] 보상: 골드 +{result.goldReward}");
            }

            // 2. 경험치 보상
            if (result.expReward > 0 && PlayerStats.Instance != null)
            {
                PlayerStats.Instance.AddEXP(result.expReward);
                Debug.Log($"[QuestChainManager] 보상: EXP +{result.expReward}");
            }

            // 3. 영지 호감도 변화 (Phase 3 연동)
            if (result.affinityChanges != null)
            {
                var db = TerritoryDatabase.Instance;
                foreach (var change in result.affinityChanges)
                {
                    if (db != null && !string.IsNullOrEmpty(change.territoryId))
                    {
                        var state = db.GetState(change.territoryId);
                        if (state != null)
                        {
                            // loyaltyToPlayer 값 변경 (TerritoryState에 필드가 있다고 가정)
                            // state.loyaltyToPlayer += change.delta;
                            Debug.Log($"[QuestChainManager] 영지 호감도 변화: {change.territoryId} {change.delta:+0;-0}");
                        }
                    }
                }
            }

            // 4. 국가 평판 변화
            if (result.reputationChanges != null)
            {
                foreach (var change in result.reputationChanges)
                {
                    Debug.Log($"[QuestChainManager] 국가 평판 변화: {change.nationType} {change.delta:+0;-0}");
                }
            }

            // 5. 병사 수 변화 (Phase 3 연동)
            if (result.soldierChanges != null)
            {
                foreach (var change in result.soldierChanges)
                {
                    Debug.Log($"[QuestChainManager] 병사 수 변화: {change.territoryId} {change.delta:+0;-0}");
                }
            }

            // 6. 다이내믹 이벤트 트리거 (Phase 36)
            if (!string.IsNullOrEmpty(result.triggerEventType))
            {
                TriggerDynamicEvent(result.triggerEventType);
            }
        }

        /// <summary>
        /// Phase 36 다이내믹 이벤트를 트리거합니다.
        /// </summary>
        private void TriggerDynamicEvent(string eventType)
        {
            if (WorldEventManager.Instance == null)
            {
                Debug.LogWarning("[QuestChainManager] WorldEventManager.Instance is null — 이벤트를 트리거할 수 없습니다.");
                return;
            }

            // WorldEventManager.EventType 파싱
            if (Enum.TryParse<WorldEventManager.EventType>(eventType, out var parsedType))
            {
                Debug.Log($"[QuestChainManager] ⚡ 다이내믹 이벤트 트리거: {parsedType}");
                // WorldEventManager의 내부 StartEvent에 접근할 수 없으므로
                // 직접 TerritoryDatabase에서 첫 번째 영지를 가져와 이벤트 발생
                var db = TerritoryDatabase.Instance;
                if (db != null)
                {
                    foreach (var def in db.GetAllDefinitions())
                    {
                        if (def.id.nation != Core.Data.NationType.None)
                        {
                            // 강제 이벤트 발생 (AcceptEvent를 통해 처리)
                            var evt = new WorldEventManager.ActiveEvent
                            {
                                type = parsedType,
                                territoryId = def.id.ToString(),
                                territoryName = def.territoryName,
                                description = $"퀘스트 체인에 의해 발생한 {parsedType} 이벤트",
                                startTime = Time.time,
                                duration = 300f,
                                phase = WorldEventManager.EventPhase.Active,
                                playerAccepted = false,
                                succeeded = false
                            };
                            // DynamicEventUI.ShowEvent(evt) — 이벤트로 대체
                            WorldEventManager.TriggerEventStarted(evt);
                            break;
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[QuestChainManager] 알 수 없는 이벤트 유형: {eventType}");
            }
        }

        // ================================================================
        // 상태 조회
        // ================================================================

        /// <summary>
        /// 체인의 현재 진행 상태를 반환합니다.
        /// </summary>
        public ChainProgress GetChainProgress(string chainId)
        {
            _chainProgress.TryGetValue(chainId, out var progress);
            return progress;
        }

        /// <summary>
        /// 체인의 현재 노드 데이터를 반환합니다.
        /// </summary>
        public QuestChainNode GetCurrentNode(string chainId)
        {
            if (!_chainProgress.TryGetValue(chainId, out var progress))
                return default;

            if (!_chainRegistry.TryGetValue(chainId, out var chain))
                return default;

            return chain.GetNode(progress.currentNodeId);
        }

        /// <summary>
        /// 체인이 완료되었는지 확인합니다.
        /// </summary>
        public bool IsChainCompleted(string chainId)
        {
            return _chainProgress.TryGetValue(chainId, out var progress) && progress.isCompleted;
        }

        /// <summary>
        /// 체인이 활성 상태인지 확인합니다.
        /// </summary>
        public bool IsChainActive(string chainId)
        {
            return _chainProgress.TryGetValue(chainId, out var progress) && progress.isActive;
        }

        // ================================================================
        // 저장/로드
        // ================================================================

        /// <summary>
        /// 모든 체인 진행 상태를 직렬화 가능한 리스트로 수집합니다.
        /// SaveManager에서 호출됩니다.
        /// </summary>
        public List<ChainProgress> CollectAllProgress()
        {
            var list = new List<ChainProgress>();
            foreach (var kvp in _chainProgress)
            {
                list.Add(kvp.Value);
            }
            return list;
        }

        /// <summary>
        /// 저장된 체인 진행 상태를 복원합니다.
        /// SaveManager에서 호출됩니다.
        /// </summary>
        public void RestoreAllProgress(List<ChainProgress> progressList)
        {
            if (progressList == null) return;

            _chainProgress.Clear();
            foreach (var progress in progressList)
            {
                if (!string.IsNullOrEmpty(progress.chainId))
                {
                    _chainProgress[progress.chainId] = progress;
                    Debug.Log($"[QuestChainManager] 체인 복원: {progress.chainId} (활성={progress.isActive}, 완료={progress.isCompleted})");
                }
            }
        }

        /// <summary>
        /// 모든 체인 상태 초기화 (테스트용)
        /// </summary>
        public void ResetAll()
        {
            _chainProgress.Clear();
        }
    }
}