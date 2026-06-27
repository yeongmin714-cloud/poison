using System.Collections.Generic;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 영지별 난이도(Tier 1~5)에 따른 퀘스트 정의 풀.
    /// 각 퀘스트는 QuestData 구조체로 정의되며 Phase6B_GenerateTerritoryNPCs에 의해 등록됩니다.
    /// </summary>
    public static class TerritoryQuestDefinitions
    {
        // =====================================================================
        //  Tier 1 (Ring1 — 쉬움, 레벨 1~3)
        // =====================================================================
        private static readonly QuestData[] _tier1Quests = new QuestData[]
        {
            new QuestData
            {
                questId = "t1_herb_01", questName = "기초 약초 수집",
                description = "빨간 약초 3개를 채집해오세요. 주변 숲에서 쉽게 찾을 수 있습니다.",
                requiredLevel = 1,
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.GatherItem, targetId = "herb_red", requiredCount = 3, description = "빨간 약초 3개 채집" }
                },
                reward = new QuestReward { gold = 10, exp = 20 }
            },
            new QuestData
            {
                questId = "t1_hunt_01", questName = "토끼 사냥",
                description = "토끼 2마리를 처치하고 고기를 가져오세요.",
                requiredLevel = 1,
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.KillMonster, targetId = "rabbit", requiredCount = 2, description = "토끼 2마리 처치" }
                },
                reward = new QuestReward { gold = 15, exp = 30 }
            },
            new QuestData
            {
                questId = "t1_deliver_01", questName = "빵 배달",
                description = "마을 빵집에서 신선한 빵을 전해주세요.",
                requiredLevel = 1,
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.TalkToNPC, targetId = "baker", requiredCount = 1, description = "빵집 방문" },
                    new QuestObjective { type = QuestObjectiveType.ExploreTerritory, targetId = "t1_deliver_spot", requiredCount = 1, description = "배달 장소 도착" }
                },
                reward = new QuestReward { gold = 8, exp = 15 }
            },
            new QuestData
            {
                questId = "t1_explore_01", questName = "영지 탐험",
                description = "영지 내 주요 지점 2곳을 방문하세요.",
                requiredLevel = 1,
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.ExploreTerritory, targetId = "t1_spot_01", requiredCount = 1, description = "첫 번째 지점 방문" },
                    new QuestObjective { type = QuestObjectiveType.ExploreTerritory, targetId = "t1_spot_02", requiredCount = 1, description = "두 번째 지점 방문" }
                },
                reward = new QuestReward { gold = 5, exp = 10 }
            },
            new QuestData
            {
                questId = "t1_craft_01", questName = "첫 제작",
                description = "크래프트 테이블에서 아이템 1개를 제작하세요.",
                requiredLevel = 1,
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.CraftItem, targetId = "any", requiredCount = 1, description = "아이템 1개 제작" }
                },
                reward = new QuestReward { gold = 12, exp = 25 }
            },
            new QuestData
            {
                questId = "t1_herb_02", questName = "노란 약초 모으기",
                description = "노란 약초 2개를 채집해오세요.",
                requiredLevel = 1,
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.GatherItem, targetId = "herb_yellow", requiredCount = 2, description = "노란 약초 2개 채집" }
                },
                reward = new QuestReward { gold = 12, exp = 22 }
            }
        };

        // =====================================================================
        //  Tier 2 (Ring2 — 보통, 레벨 3~5)
        // =====================================================================
        private static readonly QuestData[] _tier2Quests = new QuestData[]
        {
            new QuestData
            {
                questId = "t2_herb_01", questName = "중간 약초 수집",
                description = "초록 약초 5개를 채집해오세요. 깊은 숲에서 자랍니다.",
                requiredLevel = 3,
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.GatherItem, targetId = "herb_green", requiredCount = 5, description = "초록 약초 5개 채집" }
                },
                reward = new QuestReward { gold = 25, exp = 45 }
            },
            new QuestData
            {
                questId = "t2_hunt_01", questName = "멧돼지 사냥",
                description = "멧돼지 2마리를 처치하고 고기를 가져오세요.",
                requiredLevel = 3,
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.KillMonster, targetId = "boar", requiredCount = 2, description = "멧돼지 2마리 처치" }
                },
                reward = new QuestReward { gold = 30, exp = 50 }
            },
            new QuestData
            {
                questId = "t2_craft_01", questName = "가죽 제작",
                description = "멧돼지 가죽을 사용하여 기본 방어구 1개를 제작하세요.",
                requiredLevel = 3,
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.CraftItem, targetId = "armor_leather", requiredCount = 1, description = "가죽 방어구 1개 제작" }
                },
                reward = new QuestReward { gold = 35, exp = 60 }
            },
            new QuestData
            {
                questId = "t2_deliver_01", questName = "약초 전달",
                description = "수집한 약초를 치료사에게 전달하세요.",
                requiredLevel = 3,
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.GatherItem, targetId = "herb_silver", requiredCount = 3, description = "은빛 이끼 3개 채집" },
                    new QuestObjective { type = QuestObjectiveType.TalkToNPC, targetId = "healer", requiredCount = 1, description = "치료사에게 전달" }
                },
                reward = new QuestReward { gold = 20, exp = 40 }
            },
            new QuestData
            {
                questId = "t2_explore_01", questName = "동굴 탐험",
                description = "영지 근처 동굴을 탐험하고 입구를 발견하세요.",
                requiredLevel = 3,
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.ExploreTerritory, targetId = "t2_cave", requiredCount = 1, description = "동굴 입구 발견" }
                },
                reward = new QuestReward { gold = 20, exp = 35 }
            },
            new QuestData
            {
                questId = "t2_hunt_02", questName = "멧돼지 엄니 수집",
                description = "멧돼지 엄니 3개를 수집하세요.",
                requiredLevel = 3,
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.KillMonster, targetId = "boar", requiredCount = 3, description = "멧돼지 3마리 처치" }
                },
                reward = new QuestReward { gold = 35, exp = 55 }
            }
        };

        // =====================================================================
        //  Tier 3 (Ring3 — 어려움, 레벨 5~8)
        // =====================================================================
        private static readonly QuestData[] _tier3Quests = new QuestData[]
        {
            new QuestData
            {
                questId = "t3_herb_01", questName = "고급 약초 수집",
                description = "희귀한 은빛 이끼 4개를 깊은 숲에서 채집하세요.",
                requiredLevel = 5,
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.GatherItem, targetId = "herb_silver", requiredCount = 4, description = "은빛 이끼 4개 채집" }
                },
                reward = new QuestReward { gold = 40, exp = 80 }
            },
            new QuestData
            {
                questId = "t3_hunt_01", questName = "늑대 사냥",
                description = "늑대 3마리를 처치하고 늑대 이빨을 수집하세요.",
                requiredLevel = 5,
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.KillMonster, targetId = "wolf", requiredCount = 3, description = "늑대 3마리 처치" }
                },
                reward = new QuestReward { gold = 50, exp = 100 }
            },
            new QuestData
            {
                questId = "t3_craft_01", questName = "요리 제작",
                description = "토끼고기 2개를 사용하여 요리 1개를 만들어 오세요.",
                requiredLevel = 5,
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.CraftItem, targetId = "food_rabbit_cooked", requiredCount = 1, description = "요리 1개 제작" }
                },
                reward = new QuestReward { gold = 45, exp = 90 }
            },
            new QuestData
            {
                questId = "t3_deliver_01", questName = "귀중한 전달",
                description = "무기 재료를 대장장이에게 안전하게 전달하세요.",
                requiredLevel = 5,
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.GatherItem, targetId = "mat_boar_tusk", requiredCount = 3, description = "멧돼지 엄니 3개 수집" },
                    new QuestObjective { type = QuestObjectiveType.TalkToNPC, targetId = "blacksmith", requiredCount = 1, description = "대장장이에게 전달" }
                },
                reward = new QuestReward { gold = 40, exp = 75 }
            },
            new QuestData
            {
                questId = "t3_explore_01", questName = "고대 유적 탐험",
                description = "영지 외곽의 고대 유적을 탐험하세요.",
                requiredLevel = 5,
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.ExploreTerritory, targetId = "t3_ruins", requiredCount = 1, description = "고대 유적 방문" }
                },
                reward = new QuestReward { gold = 35, exp = 65 }
            }
        };

        // =====================================================================
        //  Tier 4 (Ring4 — 매우 어려움, 레벨 8~12)
        // =====================================================================
        private static readonly QuestData[] _tier4Quests = new QuestData[]
        {
            new QuestData
            {
                questId = "t4_herb_01", questName = "희귀 약초 수집",
                description = "보라색 독나물 3개를 위험한 지역에서 채집하세요.",
                requiredLevel = 8,
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.GatherItem, targetId = "herb_purple", requiredCount = 3, description = "보라 독나물 3개 채집" }
                },
                reward = new QuestReward { gold = 60, exp = 130 }
            },
            new QuestData
            {
                questId = "t4_hunt_01", questName = "강력한 짐승 사냥",
                description = "늑대 무리 5마리를 처치하세요. 위험한 지역입니다.",
                requiredLevel = 8,
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.KillMonster, targetId = "wolf", requiredCount = 5, description = "늑대 5마리 처치" }
                },
                reward = new QuestReward { gold = 70, exp = 150 }
            },
            new QuestData
            {
                questId = "t4_craft_01", questName = "고급 무기 제작",
                description = "늑대 이빨과 멧돼지 가죽으로 고급 무기를 제작하세요.",
                requiredLevel = 8,
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.CraftItem, targetId = "weapon_sword_wood", requiredCount = 1, description = "고급 무기 1개 제작" }
                },
                reward = new QuestReward { gold = 80, exp = 160 }
            },
            new QuestData
            {
                questId = "t4_explore_01", questName = "비밀 통로 탐험",
                description = "영지 아래 숨겨진 비밀 통로를 발견하세요.",
                requiredLevel = 8,
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.ExploreTerritory, targetId = "t4_secret_passage", requiredCount = 1, description = "비밀 통로 발견" }
                },
                reward = new QuestReward { gold = 55, exp = 120 }
            },
            new QuestData
            {
                questId = "t4_hunt_02", questName = "늑대 무리 정리",
                description = "늑대 우두머리를 포함한 늑대 4마리를 처치하세요.",
                requiredLevel = 8,
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.KillMonster, targetId = "wolf_alpha", requiredCount = 1, description = "늑대 우두머리 1마리 처치" },
                    new QuestObjective { type = QuestObjectiveType.KillMonster, targetId = "wolf", requiredCount = 3, description = "늑대 3마리 처치" }
                },
                reward = new QuestReward { gold = 85, exp = 170 }
            }
        };

        // =====================================================================
        //  Tier 5 (Empire — 최종, 레벨 12+)
        // =====================================================================
        private static readonly QuestData[] _tier5Quests = new QuestData[]
        {
            new QuestData
            {
                questId = "t5_herb_01", questName = "전설의 약초",
                description = "전설의 신비한 약초를 위험한 황제국 영토에서 채집하세요.",
                requiredLevel = 12,
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.GatherItem, targetId = "herb_legendary", requiredCount = 2, description = "전설의 약초 2개 채집" }
                },
                reward = new QuestReward { gold = 100, exp = 300 }
            },
            new QuestData
            {
                questId = "t5_hunt_01", questName = "보스 사냥",
                description = "황제국 영토의 강력한 보스 몬스터를 처치하세요.",
                requiredLevel = 12,
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.KillMonster, targetId = "boss_empire", requiredCount = 1, description = "보스 몬스터 1마리 처치" }
                },
                reward = new QuestReward { gold = 120, exp = 400 }
            },
            new QuestData
            {
                questId = "t5_craft_01", questName = "전설의 장비 제작",
                description = "희귀 재료를 모아 전설의 장비 1개를 제작하세요.",
                requiredLevel = 12,
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.CraftItem, targetId = "weapon_legendary", requiredCount = 1, description = "전설의 무기 1개 제작" }
                },
                reward = new QuestReward { gold = 150, exp = 500 }
            },
            new QuestData
            {
                questId = "t5_explore_01", questName = "황제국 심장부 탐험",
                description = "황제국 성채의 가장 깊은 곳까지 탐험하세요.",
                requiredLevel = 12,
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.ExploreTerritory, targetId = "t5_citadel_core", requiredCount = 1, description = "성채 심장부 도달" }
                },
                reward = new QuestReward { gold = 100, exp = 350 }
            }
        };

        // =====================================================================
        //  Chain 1: 💌 전쟁으로 헤어진 연인 (Tier 2) — 동(East) → 서(West)
        // =====================================================================
        private static readonly QuestData[] _chain1Quests = new QuestData[]
        {
            new QuestData
            {
                questId = "t2_chain_love_01", questName = "전쟁으로 헤어진 연인",
                description = "동부 영지의 노인이 전쟁 때문에 헤어진 딸에게 편지를 전해달라고 부탁했습니다. 편지를 받아 서부 영지로 가져가세요.",
                requiredLevel = 3,
                giverNpcId = "east_elderly",
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.TalkToNPC, targetId = "east_elderly", requiredCount = 1, description = "동부 영지 노인과 대화" },
                    new QuestObjective { type = QuestObjectiveType.GatherItem, targetId = "quest_love_letter", requiredCount = 1, description = "사랑의 편지 수령" }
                },
                reward = new QuestReward { gold = 20, exp = 30 }
            },
            new QuestData
            {
                questId = "t2_chain_love_02", questName = "편지 전달",
                description = "서부 영지의 한 여성에게 노인의 편지를 전해주세요.",
                requiredLevel = 3,
                giverNpcId = "west_woman",
                prerequisiteQuestIds = new string[] { "t2_chain_love_01" },
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.TalkToNPC, targetId = "west_woman", requiredCount = 1, description = "서부 영지 여성에게 편지 전달" }
                },
                reward = new QuestReward { gold = 30, exp = 50 }
            }
        };

        // =====================================================================
        //  Chain 2: 📜 약초 연구 자료 (Tier 3) — 남(South) → 북(North)
        // =====================================================================
        private static readonly QuestData[] _chain2Quests = new QuestData[]
        {
            new QuestData
            {
                questId = "t3_chain_herb_01", questName = "약초 연구 의뢰",
                description = "남부 영지의 약초꾼이 북부 영지 학자에게 보낼 연구 자료를 준비 중입니다. 은빛 이끼 3개를 채집해 약초꾼에게 가져다주세요.",
                requiredLevel = 5,
                giverNpcId = "south_herbalist",
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.GatherItem, targetId = "herb_silver", requiredCount = 3, description = "은빛 이끼 3개 채집" },
                    new QuestObjective { type = QuestObjectiveType.TalkToNPC, targetId = "south_herbalist", requiredCount = 1, description = "약초꾼에게 연구 자료 요청" }
                },
                reward = new QuestReward { gold = 40, exp = 60 }
            },
            new QuestData
            {
                questId = "t3_chain_herb_02", questName = "연구 자료 전달",
                description = "약초 연구 자료를 북부 영지 학자에게 전달하세요.",
                requiredLevel = 5,
                giverNpcId = "north_scholar",
                prerequisiteQuestIds = new string[] { "t3_chain_herb_01" },
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.TalkToNPC, targetId = "north_scholar", requiredCount = 1, description = "북부 학자에게 연구 자료 전달" }
                },
                reward = new QuestReward { gold = 50, exp = 80 }
            }
        };

        // =====================================================================
        //  Chain 3: 💎 가보 전달 (Tier 1) — 서(West) → 동(East)
        // =====================================================================
        private static readonly QuestData[] _chain3Quests = new QuestData[]
        {
            new QuestData
            {
                questId = "t1_chain_heirloom_01", questName = "할머니의 부탁",
                description = "서부 영지의 노파가 먼 동쪽에 사는 손주에게 가보를 전해달라고 부탁합니다.",
                requiredLevel = 1,
                giverNpcId = "west_grandma",
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.TalkToNPC, targetId = "west_grandma", requiredCount = 1, description = "서부 영지 노파와 대화" },
                    new QuestObjective { type = QuestObjectiveType.GatherItem, targetId = "quest_heirloom", requiredCount = 1, description = "가보 수령" }
                },
                reward = new QuestReward { gold = 15, exp = 25 }
            },
            new QuestData
            {
                questId = "t1_chain_heirloom_02", questName = "가보 전달",
                description = "동부 영지의 젊은이에게 가보를 전해주세요.",
                requiredLevel = 1,
                giverNpcId = "east_young",
                prerequisiteQuestIds = new string[] { "t1_chain_heirloom_01" },
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.TalkToNPC, targetId = "east_young", requiredCount = 1, description = "동부 영지 젊은이에게 가보 전달" }
                },
                reward = new QuestReward { gold = 25, exp = 40 }
            }
        };

        // =====================================================================
        //  Chain 4: 🗡️ 군사 정보 (Tier 4) — 북(North) → 서(West)
        // =====================================================================
        private static readonly QuestData[] _chain4Quests = new QuestData[]
        {
            new QuestData
            {
                questId = "t4_chain_intel_01", questName = "군사 정보 입수",
                description = "북부 영지 장교가 서부 전선의 아군에게 전달할 중요한 군사 정보 문서를 준비했습니다.",
                requiredLevel = 8,
                giverNpcId = "north_officer",
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.TalkToNPC, targetId = "north_officer", requiredCount = 1, description = "북부 장교와 대화" },
                    new QuestObjective { type = QuestObjectiveType.GatherItem, targetId = "quest_military_intel", requiredCount = 1, description = "군사 정보 문서 수령" }
                },
                reward = new QuestReward { gold = 60, exp = 120 }
            },
            new QuestData
            {
                questId = "t4_chain_intel_02", questName = "정보 전달",
                description = "서부 영지의 아군 병사에게 군사 정보 문서를 안전하게 전달하세요.",
                requiredLevel = 8,
                giverNpcId = "west_soldier",
                prerequisiteQuestIds = new string[] { "t4_chain_intel_01" },
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.TalkToNPC, targetId = "west_soldier", requiredCount = 1, description = "서부 병사에게 정보 전달" }
                },
                reward = new QuestReward { gold = 80, exp = 150 }
            }
        };

        // =====================================================================
        //  Chain 5: 👑 저항군 연결 (Tier 5, 3단계) — 황제국 인접 → 황제국 내부 → 저항군 보고
        // =====================================================================
        private static readonly QuestData[] _chain5Quests = new QuestData[]
        {
            new QuestData
            {
                questId = "t5_chain_resistance_01", questName = "저항군 접촉",
                description = "황제국 인접 영지의 저항군이 내부에 우리 사람이 있다며 연락문을 전해줄 것을 요청합니다.",
                requiredLevel = 12,
                giverNpcId = "empire_border_rebel",
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.TalkToNPC, targetId = "empire_border_rebel", requiredCount = 1, description = "저항군과 대화" },
                    new QuestObjective { type = QuestObjectiveType.GatherItem, targetId = "quest_resistance_letter", requiredCount = 1, description = "저항군 연락문 수령" }
                },
                reward = new QuestReward { gold = 80, exp = 200 }
            },
            new QuestData
            {
                questId = "t5_chain_resistance_02", questName = "내부 동조자 접촉",
                description = "황제국 내부에 숨어 있는 동조자에게 연락문을 전달하세요.",
                requiredLevel = 12,
                giverNpcId = "empire_insider",
                prerequisiteQuestIds = new string[] { "t5_chain_resistance_01" },
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.TalkToNPC, targetId = "empire_insider", requiredCount = 1, description = "황제국 내부 동조자 접촉" }
                },
                reward = new QuestReward { gold = 100, exp = 300 }
            },
            new QuestData
            {
                questId = "t5_chain_resistance_03", questName = "저항군 보고",
                description = "임무를 완수했음을 저항군에게 보고하세요.",
                requiredLevel = 12,
                giverNpcId = "empire_border_rebel",
                prerequisiteQuestIds = new string[] { "t5_chain_resistance_02" },
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.TalkToNPC, targetId = "empire_border_rebel", requiredCount = 1, description = "저항군에게 임무 완수 보고" }
                },
                reward = new QuestReward { gold = 150, exp = 500 }
            }
        };

        // =====================================================================
        //  Arc 1: 💀 "마약왕의 몰락" (5단계, Tier 2~3) — 동(East) → 서(West)
        // =====================================================================
        private static readonly QuestData[] _arc1DrugQuests = new QuestData[]
        {
            new QuestData
            {
                questId = "arc1_drug_01", questName = "마약왕 정보 수집",
                description = "동부 영지의 술집주인이 마약왕에 대한 정보를 가지고 있다고 합니다. 술집주인을 찾아가 이야기를 들어보세요.",
                requiredLevel = 3,
                giverNpcId = "east_bartender",
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.TalkToNPC, targetId = "east_bartender", requiredCount = 1, description = "술집주인과 대화" }
                },
                reward = new QuestReward { gold = 20, exp = 40 }
            },
            new QuestData
            {
                questId = "arc1_drug_02", questName = "마약 재료 확인",
                description = "술집주인이 마약왕이 서쪽 영지에서 보라색 독나물을 대량으로 들여온다고 알려줬습니다. 보라 독나물을 채집해 증거를 확보하세요.",
                requiredLevel = 3,
                giverNpcId = "east_bartender",
                prerequisiteQuestIds = new string[] { "arc1_drug_01" },
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.GatherItem, targetId = "herb_purple", requiredCount = 5, description = "보라 독나물 5개 채집" },
                    new QuestObjective { type = QuestObjectiveType.TalkToNPC, targetId = "east_bartender", requiredCount = 1, description = "술집주인에게 보고" }
                },
                reward = new QuestReward { gold = 30, exp = 60 }
            },
            new QuestData
            {
                questId = "arc1_drug_03", questName = "은닉처 탐색",
                description = "정보원이 서쪽 영지에 마약 재료 은닉처가 있다고 알려줬습니다. 서쪽 영지의 은닉처를 찾아 탐색하세요.",
                requiredLevel = 4,
                giverNpcId = "west_informant",
                prerequisiteQuestIds = new string[] { "arc1_drug_02" },
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.ExploreTerritory, targetId = "west_hideout", requiredCount = 1, description = "은닉처 발견" }
                },
                reward = new QuestReward { gold = 40, exp = 80 }
            },
            new QuestData
            {
                questId = "arc1_drug_04", questName = "증거 확보",
                description = "은닉처에서 마약 제조와 유통의 증거 서류를 발견했습니다. 증거를 확보하세요.",
                requiredLevel = 4,
                giverNpcId = "west_informant",
                prerequisiteQuestIds = new string[] { "arc1_drug_03" },
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.GatherItem, targetId = "quest_evidence_doc", requiredCount = 1, description = "증거 서류 획득" }
                },
                reward = new QuestReward { gold = 50, exp = 100 }
            },
            new QuestData
            {
                questId = "arc1_drug_05", questName = "마약왕의 최후",
                description = "증거를 손에 넣었습니다. 서부 영주에게 증거를 제출하여 마약왕을 법의 심판대에 세우세요. (독살 선택 시 NationReputation(West) -30)",
                requiredLevel = 5,
                giverNpcId = "west_lord",
                prerequisiteQuestIds = new string[] { "arc1_drug_04" },
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.TalkToNPC, targetId = "west_lord", requiredCount = 1, description = "서부 영주에게 증거 제출" }
                },
                reward = new QuestReward { gold = 80, exp = 200, items = new List<PlayerInventory.ItemData> { new PlayerInventory.ItemData { id = "LegendaryDagger", displayName = "전설의 단검", description = "전설적인 힘을 가진 단검", category = PlayerInventory.ItemCategory.Weapon, maxStack = 1 } } }
            }
        };

        // =====================================================================
        //  Arc 2: 🏰 "성의 비밀" (6단계, Tier 3~4) — 남(South)
        // =====================================================================
        private static readonly QuestData[] _arc2CastleQuests = new QuestData[]
        {
            new QuestData
            {
                questId = "arc2_castle_01", questName = "수상한 움직임",
                description = "남부 영지의 한 상인이 성 근처에서 수상한 움직임을 목격했습니다. 상인에게 무슨 일인지 물어보세요.",
                requiredLevel = 5,
                giverNpcId = "south_merchant",
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.TalkToNPC, targetId = "south_merchant", requiredCount = 1, description = "수상한 상인과 대화" }
                },
                reward = new QuestReward { gold = 30, exp = 50 }
            },
            new QuestData
            {
                questId = "arc2_castle_02", questName = "비밀 통로 탐색",
                description = "상인이 성 아래에 비밀 통로가 있다고 귀뜸해줬습니다. 비밀 통로 입구를 찾아 탐색하세요.",
                requiredLevel = 5,
                giverNpcId = "south_merchant",
                prerequisiteQuestIds = new string[] { "arc2_castle_01" },
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.ExploreTerritory, targetId = "south_secret_passage", requiredCount = 1, description = "비밀 통로 입구 발견" },
                    new QuestObjective { type = QuestObjectiveType.TalkToNPC, targetId = "south_merchant", requiredCount = 1, description = "상인에게 발견 보고" }
                },
                reward = new QuestReward { gold = 40, exp = 80 }
            },
            new QuestData
            {
                questId = "arc2_castle_03", questName = "통로 내부 탐험",
                description = "비밀 통로를 발견했습니다. 통로 안으로 들어가 내부를 탐험하세요.",
                requiredLevel = 6,
                giverNpcId = "south_guide",
                prerequisiteQuestIds = new string[] { "arc2_castle_02" },
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.ExploreTerritory, targetId = "south_passage_interior", requiredCount = 1, description = "통로 내부 탐험" }
                },
                reward = new QuestReward { gold = 50, exp = 100 }
            },
            new QuestData
            {
                questId = "arc2_castle_04", questName = "반역 증거 발견",
                description = "통로 안에서 반역을 계획한 증거 문서를 발견했습니다! 누군가가 성을 장악하려는 음모를 꾸미고 있습니다.",
                requiredLevel = 6,
                giverNpcId = "south_guide",
                prerequisiteQuestIds = new string[] { "arc2_castle_03" },
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.GatherItem, targetId = "quest_treason_doc", requiredCount = 1, description = "반역 증거 문서 획득" }
                },
                reward = new QuestReward { gold = 60, exp = 120 }
            },
            new QuestData
            {
                questId = "arc2_castle_05", questName = "선택: 증거 제출",
                description = "반역의 증거를 손에 넣었습니다. 남부 영주에게 증거를 제출하여 반역자를 처단하세요.",
                requiredLevel = 7,
                giverNpcId = "south_lord",
                prerequisiteQuestIds = new string[] { "arc2_castle_04" },
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.TalkToNPC, targetId = "south_lord", requiredCount = 1, description = "남부 영주에게 증거 제출" }
                },
                reward = new QuestReward { gold = 80, exp = 160 }
            },
            new QuestData
            {
                questId = "arc2_castle_06", questName = "반역자 처단 완료",
                description = "영주가 증거를 확인하고 반역자를 체포했습니다. 영주에게서 최종 보상을 수령하세요.",
                requiredLevel = 7,
                giverNpcId = "south_lord",
                prerequisiteQuestIds = new string[] { "arc2_castle_05" },
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.TalkToNPC, targetId = "south_lord", requiredCount = 1, description = "영주에게 보상 수령" }
                },
                reward = new QuestReward { gold = 120, exp = 250, items = new List<PlayerInventory.ItemData> { new PlayerInventory.ItemData { id = "LegendaryShield", displayName = "전설의 방패", description = "전설적인 힘을 가진 방패", category = PlayerInventory.ItemCategory.Armor, maxStack = 1 } } }
            }
        };

        // =====================================================================
        //  Arc 3: 👑 "왕국의 운명" (7단계, Tier 4~5) — 최종 아크
        // =====================================================================
        private static readonly QuestData[] _arc3EmpireQuests = new QuestData[]
        {
            new QuestData
            {
                questId = "arc3_empire_01", questName = "저항군의 부름",
                description = "황제국 경계의 저항군 지도자가 당신을 찾습니다. 황제국을 무너뜨릴 때가 왔습니다.",
                requiredLevel = 10,
                giverNpcId = "empire_rebel_leader",
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.TalkToNPC, targetId = "empire_rebel_leader", requiredCount = 1, description = "저항군 지도자와 대화" }
                },
                reward = new QuestReward { gold = 60, exp = 150 }
            },
            new QuestData
            {
                questId = "arc3_empire_02", questName = "4국 동맹 설득",
                description = "저항군 지도자가 동/서/남/북 4개 국가의 협력이 필요하다고 합니다. 각국 대표를 만나 동맹을 설득하세요.",
                requiredLevel = 10,
                giverNpcId = "empire_rebel_leader",
                prerequisiteQuestIds = new string[] { "arc3_empire_01" },
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.TalkToNPC, targetId = "east_representative", requiredCount = 1, description = "동부 대표 설득" },
                    new QuestObjective { type = QuestObjectiveType.TalkToNPC, targetId = "west_representative", requiredCount = 1, description = "서부 대표 설득" },
                    new QuestObjective { type = QuestObjectiveType.TalkToNPC, targetId = "south_representative", requiredCount = 1, description = "남부 대표 설득" },
                    new QuestObjective { type = QuestObjectiveType.TalkToNPC, targetId = "north_representative", requiredCount = 1, description = "북부 대표 설득" }
                },
                reward = new QuestReward { gold = 100, exp = 200 }
            },
            new QuestData
            {
                questId = "arc3_empire_03", questName = "동맹 증표 수집",
                description = "각국 지도자가 동맹의 증표로 인장을 주었습니다. 4개의 인장을 모두 모아 저항군 지도자에게 가져가세요.",
                requiredLevel = 11,
                giverNpcId = "empire_rebel_leader",
                prerequisiteQuestIds = new string[] { "arc3_empire_02" },
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.GatherItem, targetId = "quest_seal_token", requiredCount = 4, description = "국가 인장 4개 수집" }
                },
                reward = new QuestReward { gold = 120, exp = 250 }
            },
            new QuestData
            {
                questId = "arc3_empire_04", questName = "황제국 정찰",
                description = "동맹이 결성되었습니다. 황제국 성채 내부를 정찰하여 적의 배치를 파악하세요.",
                requiredLevel = 12,
                giverNpcId = "empire_rebel_leader",
                prerequisiteQuestIds = new string[] { "arc3_empire_03" },
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.ExploreTerritory, targetId = "empire_citadel", requiredCount = 1, description = "황제국 성채 도달" }
                },
                reward = new QuestReward { gold = 150, exp = 300 }
            },
            new QuestData
            {
                questId = "arc3_empire_05", questName = "핵심 정보 탈취",
                description = "황제국 성채 내부에서 핵심 군사 정보를 탈취하세요. 이 정보가 전쟁의 승패를 가릅니다.",
                requiredLevel = 12,
                giverNpcId = "empire_insider",
                prerequisiteQuestIds = new string[] { "arc3_empire_04" },
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.GatherItem, targetId = "quest_empire_intel", requiredCount = 1, description = "황제국 정보 탈취" }
                },
                reward = new QuestReward { gold = 180, exp = 350 }
            },
            new QuestData
            {
                questId = "arc3_empire_06", questName = "최종 작전",
                description = "모든 준비가 끝났습니다. 황제를 독살하거나 정면 공격을 감행하세요. (독살 선택 시 NationReputation(Empire) -50)",
                requiredLevel = 13,
                giverNpcId = "empire_rebel_leader",
                prerequisiteQuestIds = new string[] { "arc3_empire_05" },
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.TalkToNPC, targetId = "empire_emperor", requiredCount = 1, description = "황제 처단" }
                },
                reward = new QuestReward { gold = 250, exp = 500 }
            },
            new QuestData
            {
                questId = "arc3_empire_07", questName = "왕국의 승리",
                description = "황제국이 무너졌습니다! 저항군 지도자에게 돌아가 최종 승리를 보고하세요. 왕국에 평화가 찾아왔습니다.",
                requiredLevel = 13,
                giverNpcId = "empire_rebel_leader",
                prerequisiteQuestIds = new string[] { "arc3_empire_06" },
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.TalkToNPC, targetId = "empire_rebel_leader", requiredCount = 1, description = "저항군 지도자에게 승리 보고" }
                },
                reward = new QuestReward { gold = 500, exp = 1000, items = new List<PlayerInventory.ItemData> { new PlayerInventory.ItemData { id = "LegendaryStaff", displayName = "전설의 지팡이", description = "전설적인 힘을 가진 지팡이", category = PlayerInventory.ItemCategory.Weapon, maxStack = 1 }, new PlayerInventory.ItemData { id = "LegendaryBow", displayName = "전설의 활", description = "전설적인 힘을 가진 활", category = PlayerInventory.ItemCategory.Weapon, maxStack = 1 } } }
            }
        };

        // =====================================================================
        //  독살 퀘스트 4개 (각 국가별 1개씩)
        // =====================================================================
        private static readonly QuestData[] _assassinQuests = new QuestData[]
        {
            new QuestData
            {
                questId = "assassin_east", questName = "동부 경호원 암살",
                description = "(비밀 의뢰) 동부 영주의 경호원을 제거해줘. 방법은 네가 알아서 해. 독약을 제조해 그에게 먹여라.",
                requiredLevel = 10,
                giverNpcId = "assassin_guild",
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.CraftItem, targetId = "poison_lethal", requiredCount = 1, description = "치명적인 독약 제작" },
                    new QuestObjective { type = QuestObjectiveType.TalkToNPC, targetId = "east_guard_target", requiredCount = 1, description = "대상에게 독약 투여" }
                },
                reward = new QuestReward { gold = 200, exp = 500, items = new List<PlayerInventory.ItemData> { new PlayerInventory.ItemData { id = "LegendaryDagger", displayName = "전설의 단검", description = "전설적인 힘을 가진 단검", category = PlayerInventory.ItemCategory.Weapon, maxStack = 1 } } }
            },
            new QuestData
            {
                questId = "assassin_west", questName = "서부 상단 대장 암살",
                description = "(비밀 의뢰) 서부 상단의 대장을 제거해줘. 그는 적대 세력에 정보를 팔고 있어. 독약으로 조용히 처리해라.",
                requiredLevel = 10,
                giverNpcId = "assassin_guild",
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.CraftItem, targetId = "poison_lethal", requiredCount = 1, description = "치명적인 독약 제작" },
                    new QuestObjective { type = QuestObjectiveType.TalkToNPC, targetId = "west_merchant_target", requiredCount = 1, description = "대상에게 독약 투여" }
                },
                reward = new QuestReward { gold = 200, exp = 500, items = new List<PlayerInventory.ItemData> { new PlayerInventory.ItemData { id = "LegendaryShield", displayName = "전설의 방패", description = "전설적인 힘을 가진 방패", category = PlayerInventory.ItemCategory.Armor, maxStack = 1 } } }
            },
            new QuestData
            {
                questId = "assassin_south", questName = "남부 사령관 암살",
                description = "(비밀 의뢰) 남부 군대의 사령관을 제거해줘. 그는 황제국과 내통하고 있어. 독약으로 처리해라.",
                requiredLevel = 10,
                giverNpcId = "assassin_guild",
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.CraftItem, targetId = "poison_lethal", requiredCount = 1, description = "치명적인 독약 제작" },
                    new QuestObjective { type = QuestObjectiveType.TalkToNPC, targetId = "south_commander_target", requiredCount = 1, description = "대상에게 독약 투여" }
                },
                reward = new QuestReward { gold = 200, exp = 500, items = new List<PlayerInventory.ItemData> { new PlayerInventory.ItemData { id = "LegendaryBow", displayName = "전설의 활", description = "전설적인 힘을 가진 활", category = PlayerInventory.ItemCategory.Weapon, maxStack = 1 } } }
            },
            new QuestData
            {
                questId = "assassin_north", questName = "북부 원로원장 암살",
                description = "(비밀 의뢰) 북부 원로원장을 제거해줘. 그는 전쟁을 부추기고 있어. 독약으로 조용히 처리해라.",
                requiredLevel = 10,
                giverNpcId = "assassin_guild",
                objectives = new List<QuestObjective>
                {
                    new QuestObjective { type = QuestObjectiveType.CraftItem, targetId = "poison_lethal", requiredCount = 1, description = "치명적인 독약 제작" },
                    new QuestObjective { type = QuestObjectiveType.TalkToNPC, targetId = "north_senator_target", requiredCount = 1, description = "대상에게 독약 투여" }
                },
                reward = new QuestReward { gold = 200, exp = 500, items = new List<PlayerInventory.ItemData> { new PlayerInventory.ItemData { id = "LegendaryStaff", displayName = "전설의 지팡이", description = "전설적인 힘을 가진 지팡이", category = PlayerInventory.ItemCategory.Weapon, maxStack = 1 } } }
            }
        };

        // =====================================================================
        //  조회 메서드
        // =====================================================================

        /// <summary>tier에 해당하는 퀘스트 배열 반환</summary>
        public static QuestData[] GetQuestsForTier(int tier)
        {
            return tier switch
            {
                1 => _tier1Quests,
                2 => _tier2Quests,
                3 => _tier3Quests,
                4 => _tier4Quests,
                5 => _tier5Quests,
                _ => _tier1Quests
            };
        }

        /// <summary>모든 tier의 퀘스트를 반환 (QuestManager 등록용)</summary>
        public static IEnumerable<QuestData> GetAllQuests()
        {
            var result = new List<QuestData>();
            result.AddRange(_tier1Quests);
            result.AddRange(_tier2Quests);
            result.AddRange(_tier3Quests);
            result.AddRange(_tier4Quests);
            result.AddRange(_tier5Quests);
            result.AddRange(_chain1Quests);
            result.AddRange(_chain2Quests);
            result.AddRange(_chain3Quests);
            result.AddRange(_chain4Quests);
            result.AddRange(_chain5Quests);
            result.AddRange(_arc1DrugQuests);
            result.AddRange(_arc2CastleQuests);
            result.AddRange(_arc3EmpireQuests);
            result.AddRange(_assassinQuests);
            return result;
        }

        /// <summary>
        /// 영지 tier에 맞는 퀘스트 중 NPC에게 할당할 퀘스트를 시드 기반으로 선택합니다.
        /// </summary>
        public static List<string> PickQuestIdsForNPC(string territoryId, int npcIndex, int tier)
        {
            var tierQuests = GetQuestsForTier(tier);
            if (tierQuests == null || tierQuests.Length == 0)
                return new List<string>();

            int count = Mathf.Min(tierQuests.Length, InteriorRandomizer.Range($"{territoryId}_npc{npcIndex}_qcount", 1, 4));

            string seedKey = $"{territoryId}_npc{npcIndex}_qpick";
            var rng = InteriorRandomizer.CreateRandom(seedKey);

            var selected = new List<QuestData>(tierQuests);
            var result = new List<string>();

            for (int i = selected.Count - 1; i >= 0 && result.Count < count; i--)
            {
                int idx = rng.Next(0, i + 1);
                result.Add(selected[idx].questId);
                selected[idx] = selected[i];
            }

            return result;
        }
    }
}