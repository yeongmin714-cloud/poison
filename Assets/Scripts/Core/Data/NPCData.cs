using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>
    /// 영지 NPC 데이터 — 이름, 나이 유형, 대화 문구를 정의합니다.
    /// NPC별 시드(territoryId + NPC 인덱스)로 항상 동일한 데이터를 반환합니다.
    /// </summary>
    public static class NPCData
    {
        public enum NPCAgeType { Child, Adult, Elderly }

        // ===== 이름 풀 =====

        public static string[] NamePool_Child =
        {
            "똘똘이", "꼬마", "아이돌", "별이", "꿈나무",
            "장난꾸러기", "천재소년", "하늘이", "뽀송이", "깨꼬",
            "방울이", "콩콩이", "알뜰이", "나비", "꼬꼬"
        };

        public static string[] NamePool_Adult =
        {
            "청년", "농부", "상인", "대장장이", "목수",
            "사냥꾼", "어부", "주정뱅이", "현자", "여행자",
            "치료사", "행상인", "음유시인", "약초상", "제빵사",
            "양치기", "재단사", "광부", "나무꾼", "고기잡이"
        };

        public static string[] NamePool_Elderly =
        {
            "노인", "할아버지", "현자", "원로", "장로",
            "이야기꾼", "퇴역군인", "대현자", "은발의마법사", "백발노인",
            "고목", "석학", "전설의사냥꾼", "은퇴한대장장이", "현명한할머니"
        };

        // ===== 인사말 =====

        public static string[] Greetings_Child =
        {
            "안녕!", "놀아줘!", "심심해...", "뭐하고 있어?", "나랑 게임하자!",
            "오늘 날씨 좋지?", "배고파...", "엄마가 심부름 시켰어!", "재밌는 얘기 해줘!"
        };

        public static string[] Greetings_Adult =
        {
            "안녕하세요", "무슨 일이신가요?", "어서 오십시오.", "도움이 필요하신가요?",
            "날씨가 좋군요.", "요즘 장사가 잘 안 돼요.", "뭘 도와드릴까요?",
            "반갑습니다, 나그네.", "허허, 손님이 오셨군요.", "무엇을 찾으시나요?"
        };

        public static string[] Greetings_Elderly =
        {
            "어서 오게, 젊은이.", "요즘 젊은이들은...", "허허, 오랜만이야.",
            "내가 젊었을 적에는...", "자네, 얘기 좀 하지 않겠나?",
            "세상이 많이 변했어.", "지혜가 필요하면 언제든 찾아오게.",
            "앉게, 이야기 좀 나누세.", "과거 전쟁 얘기를 해줄까?", "자네 눈빛에 뭔가 있군."
        };

        // ===== 추가 대화 (퀘스트 관련) =====

        public static string[] QuestOffer_Child =
        {
            "이거 좀 도와줄 수 있어?", "내가 심부름을 맡았는데... 같이 할래?",
            "도움이 필요해!", "내가 재미있는 걸 발견했어!"
        };

        public static string[] QuestOffer_Adult =
        {
            "일감이 하나 있는데, 들어보겠나?", "조금 부탁이 있습니다.",
            "보수는 넉넉히 드리겠소.", "일손이 필요해서 말이오."
        };

        public static string[] QuestOffer_Elderly =
        {
            "내 대신 심부름을 해줄 사람이 필요하네.",
            "젊은이, 내 부탁 하나 들어주겠나?",
            "이 근방에 문제가 생겼어. 해결해 주겠나?",
            "오래 전부터 준비해온 일인데..."
        };

        // ===== 이름 선택 (시드 기반) =====

        /// <summary>
        /// NPC 인덱스 기반 시드로 이름을 선택합니다.
        /// </summary>
        public static string PickName(string territoryId, int npcIndex, NPCAgeType ageType)
        {
            string seedKey = $"{territoryId}_npc{npcIndex}_name";
            string[] pool = ageType switch
            {
                NPCAgeType.Child => NamePool_Child,
                NPCAgeType.Elderly => NamePool_Elderly,
                _ => NamePool_Adult
            };
            int idx = InteriorRandomizer.Range(seedKey, 0, pool.Length);
            return pool.Length > 0 ? pool[idx] : "NPC";
        }

        /// <summary>
        /// NPC 인덱스 기반 시드로 나이 유형을 선택합니다.
        /// Tier에 따라 Child/Elderly 확률 조정.
        /// </summary>
        public static NPCAgeType PickAgeType(string territoryId, int npcIndex, int tier)
        {
            string seedKey = $"{territoryId}_npc{npcIndex}_age";
            float roll = InteriorRandomizer.Range(seedKey, 0f, 1f);

            // Tier가 낮을수록 Child 확률 ↑, Tier가 높을수록 Elderly 확률 ↑
            float childChance = Mathf.Max(0.1f, 0.40f - tier * 0.05f);
            float elderlyChance = Mathf.Min(0.40f, 0.10f + tier * 0.07f);

            if (roll < childChance)
                return NPCAgeType.Child;
            if (roll < childChance + 1.0f - childChance - elderlyChance)
                return NPCAgeType.Adult;
            return NPCAgeType.Elderly;
        }

        /// <summary>
        /// NPC 인덱스 기반 시드로 인사말을 선택합니다.
        /// </summary>
        public static string PickGreeting(string territoryId, int npcIndex, NPCAgeType ageType)
        {
            string seedKey = $"{territoryId}_npc{npcIndex}_greeting";
            string[] pool = ageType switch
            {
                NPCAgeType.Child => Greetings_Child,
                NPCAgeType.Elderly => Greetings_Elderly,
                _ => Greetings_Adult
            };
            int idx = InteriorRandomizer.Range(seedKey, 0, pool.Length);
            return pool.Length > 0 ? pool[idx] : "...";
        }

        /// <summary>
        /// NPC 인덱스 기반 시드로 퀘스트 제안 대사를 선택합니다.
        /// </summary>
        public static string PickQuestOffer(string territoryId, int npcIndex, NPCAgeType ageType)
        {
            string seedKey = $"{territoryId}_npc{npcIndex}_qoffer";
            string[] pool = ageType switch
            {
                NPCAgeType.Child => QuestOffer_Child,
                NPCAgeType.Elderly => QuestOffer_Elderly,
                _ => QuestOffer_Adult
            };
            int idx = InteriorRandomizer.Range(seedKey, 0, pool.Length);
            return pool.Length > 0 ? pool[idx] : "부탁이 있습니다.";
        }
    }
}