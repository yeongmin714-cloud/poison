using ProjectName.Core.Data;

namespace ProjectName.Core
{
    /// <summary>
    /// C20-02: 난이도 배율 시스템.
    /// GameManager.CurrentDifficulty에 따라 몬스터/드랍/리스폰 배율을 반환합니다.
    /// </summary>
    public static class DifficultyManager
    {
        /// <summary>
        /// 난이도별 몬스터 데미지 배율
        /// Easy=0.6, Normal=1.0, Hard=1.5
        /// </summary>
        public static float GetDamageMultiplier(DifficultyMode difficulty)
        {
            return difficulty switch
            {
                DifficultyMode.Easy => 0.6f,
                DifficultyMode.Hard => 1.5f,
                _ => 1.0f // Normal
            };
        }

        /// <summary>
        /// 난이도별 몬스터 HP 배율
        /// Easy=0.7, Normal=1.0, Hard=1.5
        /// </summary>
        public static float GetHpMultiplier(DifficultyMode difficulty)
        {
            return difficulty switch
            {
                DifficultyMode.Easy => 0.7f,
                DifficultyMode.Hard => 1.5f,
                _ => 1.0f // Normal
            };
        }

        /// <summary>
        /// 난이도별 드랍률 배율
        /// Easy=1.5, Normal=1.0, Hard=0.7
        /// </summary>
        public static float GetDropRateMultiplier(DifficultyMode difficulty)
        {
            return difficulty switch
            {
                DifficultyMode.Easy => 1.5f,
                DifficultyMode.Hard => 0.7f,
                _ => 1.0f // Normal
            };
        }

        /// <summary>
        /// 난이도별 리스폰 속도 배율
        /// Easy=0.7, Normal=1.0, Hard=1.3
        /// </summary>
        public static float GetRespawnRateMultiplier(DifficultyMode difficulty)
        {
            return difficulty switch
            {
                DifficultyMode.Easy => 0.7f,
                DifficultyMode.Hard => 1.3f,
                _ => 1.0f // Normal
            };
        }
    }
}