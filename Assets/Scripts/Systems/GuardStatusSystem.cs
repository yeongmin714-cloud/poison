#pragma warning disable 0414
namespace ProjectName.Systems
{
    /// <summary>
    /// C9-16: 병사 역할 (ROADMAP 5.3.6)
    /// </summary>
    public enum GuardRole
    {
        Soldier,     // 일반 병사 (기본)
        Herbalist,   // 약초꾼
        Hunter,      // 사냥꾼
        Informant,   // 정보원
        Miner        // 광부
    }

    /// <summary>
    /// C9-16: 병사 상태 체계 — 역할 관리, 역할별 위험, 통합 상태 데이터
    /// </summary>
    public static class GuardStatusSystem
    {
        // 역할별 사망 확률 (일일)
        public const float HUNTER_DEATH_CHANCE = 0.03f;    // 3%
        public const float INFORMANT_DEATH_CHANCE = 0.02f; // 2%
        public const float HERBALIST_DEATH_CHANCE = 0.005f; // 0.5%
        public const float MINER_DEATH_CHANCE = 0.01f;     // 1%
        public const float SOLDIER_DEATH_CHANCE = 0f;      // 0% (전투 중 사망 별도)

        // 역할별 행동 보너스
        public const float HERBALIST_GATHER_BONUS = 1.5f;   // 채집량 1.5배
        public const float HUNTER_HUNT_BONUS = 1.5f;        // 사냥량 1.5배
        public const float INFORMANT_INFO_BONUS = 1.5f;     // 정보 수집 1.5배
        public const float MINER_MINE_BONUS = 1.5f;         // 채광량 1.5배

        /// <summary>
        /// 역할 이름 (한글)
        /// </summary>
        public static string GetRoleName(GuardRole role)
        {
            switch (role)
            {
                case GuardRole.Soldier: return "일반 병사";
                case GuardRole.Herbalist: return "🌿 약초꾼";
                case GuardRole.Hunter: return "🏹 사냥꾼";
                case GuardRole.Informant: return "🕵️ 정보원";
                case GuardRole.Miner: return "⛏️ 광부";
                default: return "알 수 없음";
            }
        }

        /// <summary>
        /// 역할별 일일 사망 확률
        /// </summary>
        public static float GetDailyDeathChance(GuardRole role)
        {
            switch (role)
            {
                case GuardRole.Hunter: return HUNTER_DEATH_CHANCE;
                case GuardRole.Informant: return INFORMANT_DEATH_CHANCE;
                case GuardRole.Herbalist: return HERBALIST_DEATH_CHANCE;
                case GuardRole.Miner: return MINER_DEATH_CHANCE;
                default: return SOLDIER_DEATH_CHANCE;
            }
        }

        /// <summary>
        /// 역할별 활동 보너스 계수
        /// </summary>
        public static float GetActivityBonus(GuardRole role)
        {
            switch (role)
            {
                case GuardRole.Herbalist: return HERBALIST_GATHER_BONUS;
                case GuardRole.Hunter: return HUNTER_HUNT_BONUS;
                case GuardRole.Informant: return INFORMANT_INFO_BONUS;
                case GuardRole.Miner: return MINER_MINE_BONUS;
                default: return 1f;
            }
        }

        /// <summary>
        /// 병사 상태 요약 문자열 (HUD 표시용)
        /// </summary>
        public static string GetStatusSummary(GuardPlaceholder guard)
        {
            if (guard == null) return "";
            if (!guard.IsAlive) return "💀 사망";

            string loyalTag = GetLoyaltyTag(guard.Loyalty);
            string addictTag = GetAddictionTag(guard.Addiction);

            return $"❤️{guard.HP:F0}/{guard.MaxHP:F0} | 🤝{loyalTag} | 💊{addictTag}";
        }

        private static string GetLoyaltyTag(float loyalty)
        {
            if (loyalty >= 90) return "충성";
            if (loyalty >= 70) return "우호";
            if (loyalty >= 40) return "보통";
            if (loyalty >= 20) return "냉담";
            return "적대";
        }

        private static string GetAddictionTag(float addiction)
        {
            if (addiction <= 20) return "정상";
            if (addiction <= 40) return "약함";
            if (addiction <= 60) return "중독";
            if (addiction <= 80) return "심각";
            return "위험";
        }

        /// <summary>
        /// 역할별 활동 설명
        /// </summary>
        public static string GetRoleDescription(GuardRole role)
        {
            switch (role)
            {
                case GuardRole.Soldier: return "기본 전투 및 경비 업무";
                case GuardRole.Herbalist: return "영지 주변 약초 자동 채집";
                case GuardRole.Hunter: return "영지 주변 몬스터 사냥 및 고기/재료 획득";
                case GuardRole.Informant: return "적 영지 정보 수집 및 파견";
                case GuardRole.Miner: return "광산에서 자원 채굴 (나무/돌/철)";
                default: return "";
            }
        }

        /// <summary>
        /// 역할별 전투 가능 여부
        /// </summary>
        public static bool CanFight(GuardRole role)
        {
            return role == GuardRole.Soldier || role == GuardRole.Hunter;
        }
    }
}