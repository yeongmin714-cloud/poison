using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>
    /// Phase 39: 튜토리얼 퀘스트 체인 정의.
    /// 기존 Phase 2.6 (길 잃은 영주)를 선택지 포함 체인으로 확장.
    /// 4개 선택지: 처형/독살/선처/몸값, 각 선택지별 이후 퀘스트 체인 분기.
    /// </summary>
    public static class TutorialQuestChainDefinitions
    {
        /// <summary>
        /// 길 잃은 영주 체인 ID
        /// </summary>
        public const string CHAIN_ID_LOST_LORD = "chain_tutorial_lost_lord";

        /// <summary>
        /// 튜토리얼 퀘스트 체인을 생성하고 QuestChainManager에 등록합니다.
        /// 게임 초기화 시 호출됩니다.
        /// </summary>
        public static void RegisterTutorialChains()
        {
            var mgr = QuestChainManager.Instance;
            if (mgr == null)
            {
                Debug.LogError("[TutorialQuestChainDefinitions] QuestChainManager.Instance is null");
                return;
            }

            // ================================================================
            // 길 잃은 영주 체인
            // ================================================================
            var lostLordChain = ScriptableObject.CreateInstance<QuestChainData>();
            lostLordChain.chainId = CHAIN_ID_LOST_LORD;
            lostLordChain.chainTitle = "길 잃은 영주";
            lostLordChain.chainDescription = "헛간에 나타난 수상한 영주를 처리하라. 처형, 독살, 선처, 몸값 중 선택하라.";
            lostLordChain.requiredLevel = 1;
            lostLordChain.prerequisiteChainId = null;

            lostLordChain.nodes = new QuestChainNode[]
            {
                // ===== 노드 1: 영주 등장 =====
                new QuestChainNode
                {
                    id = "lost_lord_01_appear",
                    title = "수상한 방문자",
                    description = "한 영주가 길을 잃고 당신의 헛간에 도움을 요청했다. 그는 배가 고프다고 말하지만, 뭔가 수상하다.",
                    objectives = new[] { "영주와 대화하기" },
                    choices = new QuestChoice[]
                    {
                        new QuestChoice
                        {
                            text = "⚔️ 처형 — 즉시 처단한다",
                            condition = new QuestChoiceCondition
                            {
                                type = QuestChoiceConditionType.Level,
                                value = 1,
                                failMessage = ""
                            },
                            result = new QuestChoiceResult
                            {
                                nextNodeId = "lost_lord_02_execution",
                                goldReward = 0,
                                expReward = 30,
                                affinityChanges = new System.Collections.Generic.List<AffinityChange>
                                {
                                    new AffinityChange { territoryId = "East_01", delta = -10 }
                                },
                                resultText = "당신은 칼을 뽑아 영주의 목을 베었다.\n\"이런... 예상보다 쉬운 상대였군.\"\n영주가 쓰러지자 그의 주머니에서 영지 증서가 떨어졌다.\n[처형 완료 — 영지 증서 획득]"
                            }
                        },
                        new QuestChoice
                        {
                            text = "🧪 독살 — 음식에 독을 넣는다",
                            condition = new QuestChoiceCondition { type = QuestChoiceConditionType.None },
                            result = new QuestChoiceResult
                            {
                                nextNodeId = "lost_lord_03_poison",
                                goldReward = 0,
                                expReward = 50,
                                resultText = "당신은 음식에 설사약을 몰래 넣었다.\n영주가 음식을 먹자마자 배를 움켜쥐며 고통스러워한다.\n\"으윽... 이... 이 음식에 무슨 짓을 한 거냐!\"\n10초 후 영주는 쓰러졌다.\n[독살 완료 — 영지 증서 획득]"
                            }
                        },
                        new QuestChoice
                        {
                            text = "🤝 선처 — 음식을 주고 보내준다",
                            condition = new QuestChoiceCondition { type = QuestChoiceConditionType.None },
                            result = new QuestChoiceResult
                            {
                                nextNodeId = "lost_lord_04_mercy",
                                goldReward = 20,
                                expReward = 30,
                                affinityChanges = new System.Collections.Generic.List<AffinityChange>
                                {
                                    new AffinityChange { territoryId = "East_01", delta = 15 }
                                },
                                resultText = "당신은 영주에게 따뜻한 음식을 건넸다.\n\"정말 고맙소! 이 은혜는 절대 잊지 않겠소.\"\n영주는 감사 인사를 남기고 떠났다.\n얼마 후, 감사 표시로 금화 20닢이 도착했다.\n[선처 완료 — 호감도 +15, 골드 +20]"
                            }
                        },
                        new QuestChoice
                        {
                            text = "💰 몸값 — 인질로 잡고 몸값을 요구한다",
                            condition = new QuestChoiceCondition
                            {
                                type = QuestChoiceConditionType.Level,
                                value = 2,
                                failMessage = "필요 레벨: 2"
                            },
                            result = new QuestChoiceResult
                            {
                                nextNodeId = "lost_lord_05_ransom",
                                goldReward = 100,
                                expReward = 80,
                                affinityChanges = new System.Collections.Generic.List<AffinityChange>
                                {
                                    new AffinityChange { territoryId = "East_01", delta = -20 }
                                },
                                reputationChanges = new System.Collections.Generic.List<ReputationChange>
                                {
                                    new ReputationChange { nationType = "East", delta = -5 }
                                },
                                resultText = "당신은 영주를 묶고 몸값을 요구했다.\n\"제발 목숨만 살려주시오! 원하는 대로 다 주겠소!\"\n며칠 후, 영지에서 몸값 100닢이 도착했다.\n그러나 영주는 당신을 영원히 기억할 것이다...\n[몸값 수령 — 골드 +100, 동부 평판 -5]"
                            }
                        }
                    }
                },

                // ===== 노드 2: 처형 후 =====
                new QuestChainNode
                {
                    id = "lost_lord_02_execution",
                    title = "처형의 대가",
                    description = "영주를 처형했다. 그의 주머니에서 영지 증서를 발견했다. 그러나 소문이 퍼지고 있다...",
                    objectives = new[] { "영지 증서 획득", "소문 수습" },
                    choices = new QuestChoice[]
                    {
                        new QuestChoice
                        {
                            text = "📜 증서를 사용해 영지를 차지한다",
                            condition = new QuestChoiceCondition { type = QuestChoiceConditionType.None },
                            result = new QuestChoiceResult
                            {
                                nextNodeId = "",
                                expReward = 50,
                                goldReward = 30,
                                affinityChanges = new System.Collections.Generic.List<AffinityChange>
                                {
                                    new AffinityChange { territoryId = "East_01", delta = -30 }
                                },
                                triggerEventType = "MonsterRaid",
                                resultText = "영지 증서를 사용하여 동부의 작은 영지를 차지했다.\n그러나 영주를 처형했다는 소문에 마을 사람들이 두려워한다.\n[영지 획득 — 동부 평판 -10, 몬스터 습격 위험 증가]"
                            }
                        },
                        new QuestChoice
                        {
                            text = "🕊️ 시신을 숨기고 모른 척한다",
                            condition = new QuestChoiceCondition { type = QuestChoiceConditionType.None },
                            result = new QuestChoiceResult
                            {
                                nextNodeId = "",
                                expReward = 20,
                                resultText = "시신을 깊은 숲에 묻고 아무 일도 없었던 척했다.\n아무도 당신을 의심하지 않는다... 아직은.\n[은밀 처리 — 추가 영향 없음]"
                            }
                        }
                    }
                },

                // ===== 노드 3: 독살 후 =====
                new QuestChainNode
                {
                    id = "lost_lord_03_poison",
                    title = "독살의 여파",
                    description = "영주가 독에 쓰러졌다. 그의 시신에서 영지 증서를 발견했다. 그러나 증서의 진위가 의심스럽다...",
                    objectives = new[] { "영지 증서 확인", "증서 처분 결정" },
                    choices = new QuestChoice[]
                    {
                        new QuestChoice
                        {
                            text = "📜 증서를 사용한다",
                            condition = new QuestChoiceCondition { type = QuestChoiceConditionType.None },
                            result = new QuestChoiceResult
                            {
                                nextNodeId = "",
                                goldReward = 50,
                                expReward = 40,
                                affinityChanges = new System.Collections.Generic.List<AffinityChange>
                                {
                                    new AffinityChange { territoryId = "East_02", delta = 10 }
                                },
                                resultText = "증서는 진짜였다! 당신은 동부의 작은 영지를 차지했다.\n아무도 당신을 의심하지 않는다. 완벽한 범죄다.\n[영지 획득 — 골드 +50]"
                            }
                        },
                        new QuestChoice
                        {
                            text = "💰 증서를 암시장에 판다",
                            condition = new QuestChoiceCondition { type = QuestChoiceConditionType.None },
                            result = new QuestChoiceResult
                            {
                                nextNodeId = "",
                                goldReward = 150,
                                expReward = 20,
                                resultText = "암시장에서 증서를 비싸게 팔았다.\n누군가 이 증서로 영지를 차지할 것이다.\n당신에게는 알 바 아니다.\n[골드 +150 — 증서 판매]"
                            }
                        }
                    }
                },

                // ===== 노드 4: 선처 후 =====
                new QuestChainNode
                {
                    id = "lost_lord_04_mercy",
                    title = "선처의 결과",
                    description = "영주를 선처하여 보냈다. 그는 감사의 표시로 금화를 보내왔다. 또한 앞으로 당신을 돕겠다고 약속했다.",
                    objectives = new[] { "선물 수령", "영주의 약속" },
                    choices = new QuestChoice[]
                    {
                        new QuestChoice
                        {
                            text = "🤝 우호 관계를 유지한다",
                            condition = new QuestChoiceCondition { type = QuestChoiceConditionType.None },
                            result = new QuestChoiceResult
                            {
                                nextNodeId = "",
                                goldReward = 30,
                                expReward = 30,
                                affinityChanges = new System.Collections.Generic.List<AffinityChange>
                                {
                                    new AffinityChange { territoryId = "East_01", delta = 20 }
                                },
                                resultText = "영주는 당신의 은혜를 잊지 않았다.\n이후 동부 영지와의 교역이 순조로워졌다.\n[호감도 +20 — 동부와의 관계 개선]"
                            }
                        },
                        new QuestChoice
                        {
                            text = "⚠️ 영주의 약점을 기록한다",
                            condition = new QuestChoiceCondition { type = QuestChoiceConditionType.None },
                            result = new QuestChoiceResult
                            {
                                nextNodeId = "",
                                goldReward = 10,
                                expReward = 50,
                                resultText = "영주의 약한 모습을 기록으로 남겼다.\n이 정보는 언젠가 유용하게 쓰일 것이다...\n[정보 획득 — 향후 선택지에 활용 가능]"
                            }
                        }
                    }
                },

                // ===== 노드 5: 몸값 후 =====
                new QuestChainNode
                {
                    id = "lost_lord_05_ransom",
                    title = "몸값 거래의 끝",
                    description = "몸값을 받고 영주를 풀어주었다. 영주는 떠나면서도 당신을 노려보았다. 원한을 샀다.",
                    objectives = new[] { "몸금 확인", "영주의 복수 대비" },
                    choices = new QuestChoice[]
                    {
                        new QuestChoice
                        {
                            text = "🛡️ 방어를 강화한다",
                            condition = new QuestChoiceCondition
                            {
                                type = QuestChoiceConditionType.Level,
                                value = 2,
                                failMessage = "필요 레벨: 2"
                            },
                            result = new QuestChoiceResult
                            {
                                nextNodeId = "",
                                expReward = 40,
                                goldReward = -20,
                                soldierChanges = new System.Collections.Generic.List<SoldierChange>
                                {
                                    new SoldierChange { territoryId = "East_01", delta = 5 }
                                },
                                resultText = "몸값으로 번 돈으로 용병을 고용했다.\n영주의 복수에 대비한 현명한 선택이다.\n[병사 +5 — 방어 강화, 골드 -20]"
                            }
                        },
                        new QuestChoice
                        {
                            text = "🏃 도망친다 — 영지를 떠난다",
                            condition = new QuestChoiceCondition { type = QuestChoiceConditionType.None },
                            result = new QuestChoiceResult
                            {
                                nextNodeId = "",
                                goldReward = 80,
                                expReward = 10,
                                triggerEventType = "Storm",
                                resultText = "몸값을 챙겨 영지를 떠났다.\n뒤에서 누군가 당신을 찾는 소리가 들린다...\n[도주 — 골드 +80, 악천후 발생]"
                            }
                        }
                    }
                }
            };

            mgr.RegisterChain(lostLordChain);

            Debug.Log($"[TutorialQuestChainDefinitions] 튜토리얼 체인 등록 완료: {CHAIN_ID_LOST_LORD} (5개 노드, 4개 분기)");
        }
    }
}