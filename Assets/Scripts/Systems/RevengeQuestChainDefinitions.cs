using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>
    /// 복수명단 20명의 메인 퀘스트 체인 정의.
    /// 죄목과 이면의 증언을 조사한 뒤 각 인물의 처분을 선택한다.
    /// </summary>
    public static class RevengeQuestChainDefinitions
    {
        public const string CHAIN_ID_REVENGE_MAIN = "revenge_main";
        private static bool _registered;

        /// <summary>복수명단 메인 체인을 생성하고 QuestChainManager에 한 번 등록합니다.</summary>
        public static void RegisterAll()
        {
            if (_registered) return;

            var mgr = QuestChainManager.Instance;
            if (mgr == null)
            {
                Debug.LogError("[RevengeQuestChainDefinitions] QuestChainManager.Instance is null");
                return;
            }

            var revengeChain = ScriptableObject.CreateInstance<QuestChainData>();
            revengeChain.chainId = CHAIN_ID_REVENGE_MAIN;
            revengeChain.chainTitle = "복수명단 — 이름을 지우지 않는 자";
            revengeChain.chainDescription = "선왕의 명단에 적힌 스무 사람의 죄목과 그 이면을 조사하라. 각자의 증언을 듣고 처형할지, 살려 둘지, 혹은 확인되지 않은 이름을 지우지 않은 채 추궁할지 선택하는 여정이다.";
            revengeChain.requiredLevel = 1;
            revengeChain.prerequisiteChainId = null;
            revengeChain.nodes = new QuestChainNode[]
            {
                CreateNode("revenge_01", "알데바란 — 지킨 장부와 못 지킨 장부", "서쪽 영지에서 알데바란을 찾아 장부와 주민의 증언을 듣고 선택하십시오.", "revenge_02", "알데바란"),
                CreateNode("revenge_02", "에드릭 — 실효와 명령 사이", "북부에서 에드릭의 봉쇄 지도와 서로 어긋난 명령을 조사하고 선택하십시오.", "revenge_03", "에드릭"),
                CreateNode("revenge_03", "베르나 — 모두를 구하지 못한 것", "역참지기 베르나가 남긴 숙청 명단과 구하지 못한 이들의 흔적을 확인하고 선택하십시오.", "revenge_04", "베르나"),
                CreateNode("revenge_04", "라실 — 실종자의 편지", "남부에서 라실의 편지 수신 기록과 실종자들의 이름을 조사하고 선택하십시오.", "revenge_05", "라실"),
                CreateNode("revenge_05", "오델 — 압박받은 서명", "남부 법정에서 오델의 판결과 서명 아래 감춰진 압박의 기록을 대조하고 선택하십시오.", "revenge_06", "오델"),
                CreateNode("revenge_06", "카르덴 — 세 갈래 표식의 명령서", "카르덴의 명령서 사본에서 세 갈래 표식을 확인하되, 드러나지 않은 이름은 단정하지 말고 선택하십시오.", "revenge_07", "카르덴"),
                CreateNode("revenge_07", "이셀드릭 — 감염지의 경계", "동부 접경에서 의사 이셀드릭이 지킨 경계와 그 너머의 증언을 듣고 선택하십시오.", "revenge_08", "이셀드릭"),
                CreateNode("revenge_08", "가우든 — 없어진 마을의 이름", "북부 설원에서 가우든의 지도에 다시 그려진 마을과 지워진 이름을 확인하고 선택하십시오.", "revenge_09", "가우든"),
                CreateNode("revenge_09", "마이단 — 집행관의 서명", "황제국에서 마이단의 사형 영장과 그가 헤아린 이름들을 대조하고 선택하십시오.", "revenge_10", "마이단"),
                CreateNode("revenge_10", "루왼 — 덮은 사고", "서부 사막 광산에서 루왼의 사고 장부와 광부들의 이름을 원본과 대조하고 선택하십시오.", "revenge_11", "루왼"),
                CreateNode("revenge_11", "타르벤 — 전염지의 병참식", "남부 병참 기록에서 타르벤이 전염지의 병사들에게 보낸 식료와 이름을 확인하고 선택하십시오.", "revenge_12", "타르벤"),
                CreateNode("revenge_12", "올브레이 — 진실한 기록", "왕도 기록실에서 올브레이의 장부를 살펴 아는 것과 확인되지 않은 것을 가르고 선택하십시오.", "revenge_13", "올브레이"),
                CreateNode("revenge_13", "에몽 — 빈 마을을 살핀 측량관", "동부 초원에서 측량관 에몽의 장부를 확인하고 빈 마을의 이름을 남길지 선택하십시오.", "revenge_14", "에몽"),
                CreateNode("revenge_14", "세레딘 — 도시를 먹인 상인", "황제국 시장에서 세레딘이 빈민에게 나눈 식료의 기록을 찾아 증언을 듣고 선택하십시오.", "revenge_15", "세레딘"),
                CreateNode("revenge_15", "도므나 — 병사를 지킨 여장부", "북부 성곽에서 도므나가 숨긴 병사들의 기록과 전염 속의 증언을 살피고 선택하십시오.", "revenge_16", "도므나"),
                CreateNode("revenge_16", "에일린 — 옛 국경을 그린 지도자", "서부에서 에일린의 지도와 움직인 국경의 흔적을 대조하고 증언을 들은 뒤 선택하십시오.", "revenge_17", "에일린"),
                CreateNode("revenge_17", "크라덴 — 증언을 옮긴 통역사", "남부 해안에서 크라덴이 옮긴 익명의 증언을 원문과 대조하고 선택하십시오.", "revenge_18", "크라덴"),
                CreateNode("revenge_18", "마르텐 — 민병을 무장시킨 대장장이", "동부 대장간에서 마르텐의 칼과 마을을 지킨 이들의 기록을 조사하고 선택하십시오.", "revenge_19", "마르텐"),
                CreateNode("revenge_19", "오레몽 — 어린이를 가르친 교사", "황제국 지하 교실에서 오레몽이 숨겨 가르친 아이들의 이름과 흔적을 확인하고 선택하십시오.", "revenge_20", "오레몽"),
                CreateNode("revenge_20", "발로라 — 잃어버린 노래를 기록한 시인", "서부 사막 변방에서 발로라의 노래책과 그 속에 남은 이름을 확인하고 마지막 선택을 하십시오.", null, "발로라")
            };

            mgr.RegisterChain(revengeChain);
            _registered = true;
        }

        private static QuestChainNode CreateNode(string id, string title, string description, string nextNodeId, string characterName)
        {
            return new QuestChainNode
            {
                id = id,
                title = title,
                description = description,
                objectives = new[]
                {
                    characterName + "을 찾아 죄목과 이면의 증언을 듣기",
                    "기록을 남기고 처형·생존·추궁 중 하나를 선택하기"
                },
                choices = new QuestChoice[]
                {
                    CreateChoice("처형", "칼을 내려놓았다. " + characterName + "의 이름은 명단에서 지워졌지만, 남겨진 증언까지 사라진 것은 아니었다.", nextNodeId),
                    CreateChoice("생존", "살려 두었다. " + characterName + "은(는) 다시 걸어 나갔고, 그가 남긴 말은 기록 속에 살아남았다.", nextNodeId),
                    CreateChoice("추궁", "확인되지 않은 이름을 지우지 않았다. " + characterName + "에게 더 물었고, 대답과 침묵을 모두 기록했다.", nextNodeId)
                }
            };
        }

        private static QuestChoice CreateChoice(string text, string resultText, string nextNodeId)
        {
            return new QuestChoice
            {
                text = text,
                condition = new QuestChoiceCondition { type = QuestChoiceConditionType.None },
                result = new QuestChoiceResult
                {
                    nextNodeId = nextNodeId,
                    resultText = resultText
                }
            };
        }
    }
}
