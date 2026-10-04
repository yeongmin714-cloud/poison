using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// 《독의 이름》 복수명단 20명의 메인 퀘스트 정의.
    /// 이름과 증언을 남기고, 누구의 죄도 서둘러 확정하지 않는 흐름을 담는다.
    /// </summary>
    public static class RevengeMainQuestDefinitions
    {
        private static bool _registered;

        private struct CharacterContext
        {
            public readonly string Name;
            public readonly string SurfaceCharge;
            public readonly string TrueReason;
            public readonly string Region;

            public CharacterContext(string name, string surfaceCharge, string trueReason, string region)
            {
                Name = name;
                SurfaceCharge = surfaceCharge;
                TrueReason = trueReason;
                Region = region;
            }
        }

        // 출처: docs/REVENGE_LIST_NOVEL_20TH_STORIES.md 「복수명단 20명 총람」.
        // 표면 죄목은 명단의 주장, 진짜 이유는 문서가 제시한 인물의 이면으로 구분해 둔다.
        private static readonly Dictionary<string, CharacterContext> CharacterContexts =
            new Dictionary<string, CharacterContext>
            {
                { "revenge_01", new CharacterContext("알데바란", "국고를 탕진한 주모자", "수탈을 감사하려던 재무관", "서쪽 영지") },
                { "revenge_02", new CharacterContext("에드릭", "군대를 분열시킨 자", "부패한 궁정에 반기든 애국 장군", "북부") },
                { "revenge_03", new CharacterContext("베르나", "숙청 명단을 빼돌린 자", "사람들을 구하려던 역참지기", "여러 역참") },
                { "revenge_04", new CharacterContext("라실", "징수량을 속인 자", "실종자들을 지킨 세금 징수관", "남부") },
                { "revenge_05", new CharacterContext("오델", "부당한 판결을 남긴 자", "압박을 받고 서명한 법관", "남부 법정") },
                { "revenge_06", new CharacterContext("카르덴", "왕자를 생포한 자", "명령을 받아 적은 사냥꾼 (세 갈래 표식)", "왕도·북부") },
                { "revenge_07", new CharacterContext("이셀드릭", "외홮교역을 어지른 자", "감염지의 경계를 지킨 여성 의사", "동부 접경") },
                { "revenge_08", new CharacterContext("가우든", "지도를 위조한 자", "없어진 마을의 이름을 지킨 도안가", "북부 설원") },
                { "revenge_09", new CharacterContext("마이단", "사형 영장을 남발한 자", "가족을 살펴야만 했던 집행관", "황제국") },
                { "revenge_10", new CharacterContext("루왼", "임금을 체불한 자", "광산 사고를 덮으려던 광업 책임자", "서부 사막") },
                { "revenge_11", new CharacterContext("타르벤", "병참식을 몰래 판 자", "전염지의 병사를 살린 식료 관리인", "남부") },
                { "revenge_12", new CharacterContext("올브레이", "학자금을 바겐세일한 자", "진실한 기록을 지킨 왕궁 서기관", "왕도 기록실") },
                { "revenge_13", new CharacterContext("에몽", "조세 부과를 속인 자", "빈 마을을 살피던 측량관", "동부 초원") },
                { "revenge_14", new CharacterContext("세레딘", "시장을 독점한 자", "도시 빈민을 먹인 상인", "황제국 시장") },
                { "revenge_15", new CharacterContext("도므나", "반역 병사를 숨긴 자", "병사 처벌을 피시켜 주던 여장부", "북부 성곽") },
                { "revenge_16", new CharacterContext("에일린", "운문을 위조한 자", "옛 국경을 그린 지도자", "서부") },
                { "revenge_17", new CharacterContext("크라덴", "사자를 가로막은 자", "증언을 옮긴 통역사", "남부 해안") },
                { "revenge_18", new CharacterContext("마르텐", "병기를 훔친 자", "민병을 무장시킨 대장장이", "동부") },
                { "revenge_19", new CharacterContext("오레몽", "신뢰를 배신한 자", "어린이들을 은닉한 교사", "황제국") },
                { "revenge_20", new CharacterContext("발로라", "금서를 유포한 자", "잃어버린 노래를 기록한 시인", "서부 사막 변방") }
            };

        /// <summary>복수명단 인물의 LLM 시스템 프롬프트. 해당하지 않는 questId는 null.</summary>
        public static string GetCharacterContext(string questId)
        {
            if (string.IsNullOrEmpty(questId) || !CharacterContexts.TryGetValue(questId, out var context))
                return null;

            return "당신은 " + context.Name + "입니다. 명단에는 '" + context.SurfaceCharge +
                   "'로 적혔으나, 이 인물의 진짜 이유는 '" + context.TrueReason +
                   "'입니다. 소속/지역 힌트는 " + context.Region + "입니다. " +
                   "명단의 죄목과 이면을 구분해 인물의 관점으로 말하고, 확인되지 않은 배후·이름·사실은 추측해 확정하지 마세요. " +
                   "항상 인물에 어울리는 자연스러운 한국어로 짧게 답하세요.";
        }

        /// <summary>복수명단의 메인 퀘스트를 한 번 등록한다.</summary>
        public static void RegisterAll()
        {
            if (_registered) return;

            var quests = new QuestData[]
            {
                new QuestData
                {
                    questId = "revenge_01", questName = "알데바란 — 지킨 장부와 못 지킨 장부",
                    description = "수탈을 감사하려던 재무관 알데바란을 서쪽 영지에서 찾으십시오. 지킨 장부와 다친 주민의 이름을 함께 기록하고, 그의 이름을 지우지 마십시오.",
                    requiredLevel = 5, targetTerritoryId = "West_06", isMain = true,
                    objectives = new List<QuestObjective> { new QuestObjective { type = QuestObjectiveType.ExploreTerritory, targetId = "West_06", requiredCount = 1, description = "서쪽 영지에서 알데바란의 장부와 증언을 조사" } },
                    reward = new QuestReward { gold = 55, exp = 100, items = new List<PlayerInventory.ItemData> { PlayerInventory.SwordSteel } }
                },
                new QuestData
                {
                    questId = "revenge_02", questName = "에드릭 — 실효와 명령 사이",
                    description = "북부 장군 에드릭의 봉쇄 지도를 살피십시오. 모순된 명령과 그가 택한 실효를 베껴 남기되, 어느 쪽이 옳았는지는 섣불리 단정하지 마십시오.",
                    requiredLevel = 8, targetTerritoryId = "North_11", isMain = true,
                    objectives = new List<QuestObjective> { new QuestObjective { type = QuestObjectiveType.ExploreTerritory, targetId = "North_11", requiredCount = 1, description = "북부에서 에드릭의 봉쇄 지도 조사" } },
                    reward = new QuestReward { gold = 70, exp = 125, items = new List<PlayerInventory.ItemData> { PlayerInventory.SwordCrystal } }
                },
                new QuestData
                {
                    questId = "revenge_03", questName = "베르나 — 모두를 구하지 못한 것",
                    description = "역참지기 베르나가 빼돌린 숙청 명단과 남겨 둔 이름을 확인하십시오. 구하지 못한 이들의 흔적까지 증언으로 남기십시오.",
                    requiredLevel = 5, targetTerritoryId = "East_06", isMain = true,
                    objectives = new List<QuestObjective> { new QuestObjective { type = QuestObjectiveType.ExploreTerritory, targetId = "East_06", requiredCount = 1, description = "역참에서 베르나의 숙청 명단 확인" } },
                    reward = new QuestReward { gold = 50, exp = 95, items = new List<PlayerInventory.ItemData> { PlayerInventory.HelmetSteel } }
                },
                new QuestData
                {
                    questId = "revenge_04", questName = "라실 — 실종자의 편지",
                    description = "남부의 세금 징수관 라실이 보관한 편지 수신 기록을 찾으십시오. 실종자의 이름과 확인되지 않은 사연을 지우지 말고 남기십시오.",
                    requiredLevel = 6, targetTerritoryId = "South_06", isMain = true,
                    objectives = new List<QuestObjective> { new QuestObjective { type = QuestObjectiveType.ExploreTerritory, targetId = "South_06", requiredCount = 1, description = "남부에서 라실의 실종자 기록 조사" } },
                    reward = new QuestReward { gold = 60, exp = 110, items = new List<PlayerInventory.ItemData> { PlayerInventory.BowSteel } }
                },
                new QuestData
                {
                    questId = "revenge_05", questName = "오델 — 압박받은 서명",
                    description = "남부 법정의 법관 오델이 서명한 판결과 그 아래 덧붙인 압박의 기록을 대조하십시오. 잘못된 판결의 결과도 함께 기록하십시오.",
                    requiredLevel = 8, targetTerritoryId = "South_11", isMain = true,
                    objectives = new List<QuestObjective> { new QuestObjective { type = QuestObjectiveType.ExploreTerritory, targetId = "South_11", requiredCount = 1, description = "남부 법정에서 오델의 판결 기록 조사" } },
                    reward = new QuestReward { gold = 75, exp = 130, items = new List<PlayerInventory.ItemData> { PlayerInventory.ArmorStone } }
                },
                new QuestData
                {
                    questId = "revenge_06", questName = "카르덴 — 세 갈래 표식의 명령서",
                    description = "왕도와 북부를 오간 사냥꾼 카르덴에게서 명령서 사본을 확인하십시오. 옅은 세 갈래 표식은 보되, 보낸 이의 이름은 확인되지 않은 채로 둡니다.",
                    requiredLevel = 10, targetTerritoryId = "North_16", isMain = true,
                    objectives = new List<QuestObjective> { new QuestObjective { type = QuestObjectiveType.ExploreTerritory, targetId = "North_16", requiredCount = 1, description = "북부에서 카르덴의 명령서 사본 조사" } },
                    reward = new QuestReward { gold = 90, exp = 155, items = new List<PlayerInventory.ItemData> { PlayerInventory.SpearCrystal } }
                },
                new QuestData
                {
                    questId = "revenge_07", questName = "이셀드릭 — 감염지의 경계",
                    description = "동부 접경의 의사 이셀드릭이 지킨 경계와 통과하지 못한 이들의 기록을 살피십시오. 선택의 결과를 대신 판결하지 말고 증언으로 남기십시오.",
                    requiredLevel = 7, targetTerritoryId = "East_11", isMain = true,
                    objectives = new List<QuestObjective> { new QuestObjective { type = QuestObjectiveType.ExploreTerritory, targetId = "East_11", requiredCount = 1, description = "동부 접경에서 이셀드릭의 경계 기록 조사" } },
                    reward = new QuestReward { gold = 70, exp = 120, items = new List<PlayerInventory.ItemData> { PlayerInventory.BowCrystal } }
                },
                new QuestData
                {
                    questId = "revenge_08", questName = "가우든 — 없어진 마을의 이름",
                    description = "북부 설원의 지도 도안가 가우든이 다시 그린 마을의 이름을 확인하십시오. 지도에서 지워진 땅과 그 이름을 함께 베껴 두십시오.",
                    requiredLevel = 8, targetTerritoryId = "North_12", isMain = true,
                    objectives = new List<QuestObjective> { new QuestObjective { type = QuestObjectiveType.ExploreTerritory, targetId = "North_12", requiredCount = 1, description = "북부 설원에서 지워진 마을 지도 조사" } },
                    reward = new QuestReward { gold = 80, exp = 140, items = new List<PlayerInventory.ItemData> { PlayerInventory.SwordCrystal } }
                },
                new QuestData
                {
                    questId = "revenge_09", questName = "마이단 — 집행관의 서명",
                    description = "황제국의 집행관 마이단이 서명한 사형 영장과 그가 헤아린 이름들을 대조하십시오. 지우지 않은 서명과 죽은 이들의 기록을 함께 남기십시오.",
                    requiredLevel = 12, targetTerritoryId = "Empire_01", isMain = true,
                    objectives = new List<QuestObjective> { new QuestObjective { type = QuestObjectiveType.ExploreTerritory, targetId = "Empire_01", requiredCount = 1, description = "황제국에서 마이단의 집행 기록 조사" } },
                    reward = new QuestReward { gold = 105, exp = 170, items = new List<PlayerInventory.ItemData> { PlayerInventory.ArmorCrystal } }
                },
                new QuestData
                {
                    questId = "revenge_10", questName = "루왼 — 덮은 사고",
                    description = "서부 사막의 광산 책임자 루왼이 줄여 적었다가 다시 펼친 사고 장부를 확인하십시오. 덮인 사고의 규모와 광부들의 이름을 원본대로 기록하십시오.",
                    requiredLevel = 7, targetTerritoryId = "West_11", isMain = true,
                    objectives = new List<QuestObjective> { new QuestObjective { type = QuestObjectiveType.ExploreTerritory, targetId = "West_11", requiredCount = 1, description = "서부 사막에서 루왼의 광산 사고 장부 조사" } },
                    reward = new QuestReward { gold = 70, exp = 120, items = new List<PlayerInventory.ItemData> { PlayerInventory.ArmorStone } }
                },
                new QuestData
                {
                    questId = "revenge_11", questName = "타르벤 — 전염지의 병참식",
                    description = "남부 병참 관리인 타르벤이 전염지의 병사들에게 식료를 보낸 기록을 살피십시오. 군수품보다 먼저 지킨 사람들의 이름을 증언으로 남기십시오.",
                    requiredLevel = 7, targetTerritoryId = "South_07", isMain = true,
                    objectives = new List<QuestObjective> { new QuestObjective { type = QuestObjectiveType.ExploreTerritory, targetId = "South_07", requiredCount = 1, description = "남부에서 타르벤의 병참 기록 조사" } },
                    reward = new QuestReward { gold = 65, exp = 115, items = new List<PlayerInventory.ItemData> { PlayerInventory.SpearSteel } }
                },
                new QuestData
                {
                    questId = "revenge_12", questName = "올브레이 — 진실한 기록",
                    description = "왕도 기록실의 서기관 올브레이가 보존해 온 장부를 확인하십시오. 아는 것과 확인되지 않은 것을 구분해 적고, 기록자의 이름을 지우지 마십시오.",
                    requiredLevel = 11, targetTerritoryId = "Empire_01", isMain = true,
                    objectives = new List<QuestObjective> { new QuestObjective { type = QuestObjectiveType.ExploreTerritory, targetId = "Empire_01", requiredCount = 1, description = "왕도 기록실에서 올브레이의 장부 조사" } },
                    reward = new QuestReward { gold = 100, exp = 165, items = new List<PlayerInventory.ItemData> { PlayerInventory.BowCrystal } }
                },
                new QuestData
                {
                    questId = "revenge_13", questName = "에몽 — 빈 마을을 살핀 측량관",
                    description = "동부 초원의 측량관 에몽이 기록한 빈 마을의 수를 확인하십시오. 사람이 사라진 땅을 살아 있는 것처럼 셈하지 않도록 장부를 베껴 두십시오.",
                    requiredLevel = 6, targetTerritoryId = "East_07", isMain = true,
                    objectives = new List<QuestObjective> { new QuestObjective { type = QuestObjectiveType.ExploreTerritory, targetId = "East_07", requiredCount = 1, description = "동부 초원에서 빈 마을 측량 장부 조사" } },
                    reward = new QuestReward { gold = 55, exp = 100, items = new List<PlayerInventory.ItemData> { PlayerInventory.SwordSteel } }
                },
                new QuestData
                {
                    questId = "revenge_14", questName = "세레딘 — 도시를 먹인 상인",
                    description = "황제국 시장의 상인 세레딘이 빈민에게 식료를 나눈 기록을 창고에서 찾으십시오. 시장의 죄목 아래 감춰진 이들의 이름을 남기십시오.",
                    requiredLevel = 12, targetTerritoryId = "Empire_01", isMain = true,
                    objectives = new List<QuestObjective> { new QuestObjective { type = QuestObjectiveType.ExploreTerritory, targetId = "Empire_01", requiredCount = 1, description = "황제국 시장에서 세레딘의 배급 기록 조사" } },
                    reward = new QuestReward { gold = 110, exp = 175, items = new List<PlayerInventory.ItemData> { PlayerInventory.SpearCrystal } }
                },
                new QuestData
                {
                    questId = "revenge_15", questName = "도므나 — 병사를 지킨 여장부",
                    description = "북부 성곽의 도므나가 숨긴 병사들의 기록을 살피십시오. 반역이라는 이름만으로 결론 내리지 말고, 전염 속에서 지킨 이들의 증언을 나란히 둡니다.",
                    requiredLevel = 9, targetTerritoryId = "North_16", isMain = true,
                    objectives = new List<QuestObjective> { new QuestObjective { type = QuestObjectiveType.ExploreTerritory, targetId = "North_16", requiredCount = 1, description = "북부 성곽에서 도므나의 병사 기록 조사" } },
                    reward = new QuestReward { gold = 85, exp = 145, items = new List<PlayerInventory.ItemData> { PlayerInventory.HelmetCrystal } }
                },
                new QuestData
                {
                    questId = "revenge_16", questName = "에일린 — 옛 국경을 그린 지도자",
                    description = "서부의 지도자 에일린이 그린 옛 물길과 국경을 대조하십시오. 움직인 경계의 흔적을 남기되, 누가 옮겼는지는 증언 없이 확정하지 마십시오.",
                    requiredLevel = 8, targetTerritoryId = "West_07", isMain = true,
                    objectives = new List<QuestObjective> { new QuestObjective { type = QuestObjectiveType.ExploreTerritory, targetId = "West_07", requiredCount = 1, description = "서부에서 에일린의 옛 국경 지도 조사" } },
                    reward = new QuestReward { gold = 75, exp = 130, items = new List<PlayerInventory.ItemData> { PlayerInventory.BowCrystal } }
                },
                new QuestData
                {
                    questId = "revenge_17", questName = "크라덴 — 증언을 옮긴 통역사",
                    description = "남부 해안의 통역사 크라덴이 옮긴 익명의 증언을 찾아 원문과 번역을 대조하십시오. 증언은 남기되, 말하지 않은 이름을 덧붙이지 마십시오.",
                    requiredLevel = 6, targetTerritoryId = "South_08", isMain = true,
                    objectives = new List<QuestObjective> { new QuestObjective { type = QuestObjectiveType.ExploreTerritory, targetId = "South_08", requiredCount = 1, description = "남부 해안에서 크라덴의 번역 증언 조사" } },
                    reward = new QuestReward { gold = 60, exp = 105, items = new List<PlayerInventory.ItemData> { PlayerInventory.DaggerSteel } }
                },
                new QuestData
                {
                    questId = "revenge_18", questName = "마르텐 — 민병을 무장시킨 대장장이",
                    description = "동부 대장간에서 마르텐이 민병에게 벼려 준 칼과 마을의 기록을 살피십시오. 사라질 뻔한 마을을 지킨 이름들을 함께 적으십시오.",
                    requiredLevel = 4, targetTerritoryId = "East_02", isMain = true,
                    objectives = new List<QuestObjective> { new QuestObjective { type = QuestObjectiveType.ExploreTerritory, targetId = "East_02", requiredCount = 1, description = "동부 대장간에서 마르텐의 민병 기록 조사" } },
                    reward = new QuestReward { gold = 45, exp = 85, items = new List<PlayerInventory.ItemData> { PlayerInventory.SwordSteel } }
                },
                new QuestData
                {
                    questId = "revenge_19", questName = "오레몽 — 어린이를 가르친 교사",
                    description = "황제국 지하 교실에서 오레몽이 숨겨 가르친 아이들의 기록을 찾으십시오. 아이들이 자기 이름을 지킬 수 있도록 남긴 흔적을 보존하십시오.",
                    requiredLevel = 12, targetTerritoryId = "Empire_01", isMain = true,
                    objectives = new List<QuestObjective> { new QuestObjective { type = QuestObjectiveType.ExploreTerritory, targetId = "Empire_01", requiredCount = 1, description = "황제국에서 오레몽의 지하 교실 조사" } },
                    reward = new QuestReward { gold = 115, exp = 180, items = new List<PlayerInventory.ItemData> { PlayerInventory.ArmorCrystal } }
                },
                new QuestData
                {
                    questId = "revenge_20", questName = "발로라 — 잃어버린 노래를 기록한 시인",
                    description = "서부 사막 변방에서 시인 발로라가 남긴 노래책을 찾으십시오. 노래 속 이름을 다시 부를 수 있도록 기록을 지우지 말고 간직하십시오.",
                    requiredLevel = 9, targetTerritoryId = "West_12", isMain = true,
                    objectives = new List<QuestObjective> { new QuestObjective { type = QuestObjectiveType.ExploreTerritory, targetId = "West_12", requiredCount = 1, description = "서부 사막 변방에서 발로라의 노래책 조사" } },
                    reward = new QuestReward { gold = 80, exp = 140, items = new List<PlayerInventory.ItemData> { PlayerInventory.SwordSteel } }
                }
            };

            foreach (var quest in quests)
                QuestManager.RegisterQuest(quest);

            _registered = true;
        }
    }
}
