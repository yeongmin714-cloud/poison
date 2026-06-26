using UnityEngine;
using ProjectName.Core.Data;

namespace ProjectName.Core
{
    /// <summary>
    /// 레벨 그룹 정의를 관리하는 정적 매니저.
    /// 5단계 레벨 그룹(Novice, Adept, Veteran, Elite, Legendary)에 대한
    /// 조회, 시각적 변형명 생성, Placeholder 틴트 색상 제공 기능을 제공합니다.
    /// </summary>
    public static class LevelGroupManager
    {
        private static LevelGroup[] s_groups;
        private static bool s_initialized;

        /// <summary>
        /// 5개 레벨 그룹 정의를 초기화합니다.
        /// 최초 호출 시 한 번만 실행됩니다.
        /// </summary>
        public static void Initialize()
        {
            if (s_initialized)
                return;

            s_groups = new LevelGroup[5];

            s_groups[0] = new LevelGroup(
                LevelGroupId.Novice,
                "Novice",
                1, 10,
                "_tier1",
                new Color(0.678f, 0.847f, 0.902f)  // 연한 하늘색
            );

            s_groups[1] = new LevelGroup(
                LevelGroupId.Adept,
                "Adept",
                11, 20,
                "_tier2",
                new Color(0.565f, 0.792f, 0.565f)  // 연한 초록
            );

            s_groups[2] = new LevelGroup(
                LevelGroupId.Veteran,
                "Veteran",
                21, 30,
                "_tier3",
                new Color(1.0f, 0.843f, 0.4f)      // 황금색
            );

            s_groups[3] = new LevelGroup(
                LevelGroupId.Elite,
                "Elite",
                31, 40,
                "_tier4",
                new Color(1.0f, 0.647f, 0.0f)      // 주황색
            );

            s_groups[4] = new LevelGroup(
                LevelGroupId.Legendary,
                "Legendary",
                41, 50,
                "_tier5",
                new Color(0.863f, 0.078f, 0.235f)  // 선명한 빨강
            );

            s_initialized = true;
        }

        /// <summary>
        /// 주어진 레벨에 해당하는 LevelGroup을 반환합니다.
        /// </summary>
        /// <param name="level">대상 레벨</param>
        /// <returns>해당 레벨 그룹 정의</returns>
        /// <remarks>
        /// 레벨 0 이하는 Novice, 레벨 50 이상은 Legendary를 반환합니다.
        /// </remarks>
        public static LevelGroup GetGroup(int level)
        {
            if (!s_initialized)
                Initialize();

            if (level <= 0)
                return s_groups[(int)LevelGroupId.Novice];

            if (level >= 50)
                return s_groups[(int)LevelGroupId.Legendary];

            for (int i = 0; i < s_groups.Length; i++)
            {
                if (level >= s_groups[i].minLevel && level <= s_groups[i].maxLevel)
                    return s_groups[i];
            }

            // Fallback (should not reach here for valid level ranges)
            return s_groups[(int)LevelGroupId.Novice];
        }

        /// <summary>
        /// 지정한 LevelGroupId에 해당하는 LevelGroup 정의를 반환합니다.
        /// </summary>
        /// <param name="id">조회할 그룹 식별자</param>
        /// <returns>해당 그룹 정의</returns>
        public static LevelGroup GetGroup(LevelGroupId id)
        {
            if (!s_initialized)
                Initialize();

            return s_groups[(int)id];
        }

        /// <summary>
        /// 모든 5개의 레벨 그룹 정의를 반환합니다.
        /// </summary>
        /// <returns>LevelGroup 배열 (길이 5)</returns>
        public static LevelGroup[] GetLevelGroups()
        {
            if (!s_initialized)
                Initialize();

            return s_groups;
        }

        /// <summary>
        /// 기본 이름에 레벨 그룹의 시각적 접미사를 붙여 변형명을 생성합니다.
        /// </summary>
        /// <param name="baseName">기본 이름 (예: "soldier")</param>
        /// <param name="level">대상 레벨</param>
        /// <returns>접미사가 추가된 이름 (예: "soldier_tier1")</returns>
        /// <example>
        /// <code>
        /// string name = LevelGroupManager.GetVisualVariantName("soldier", 5);
        /// // 결과: "soldier_tier1"
        /// </code>
        /// </example>
        public static string GetVisualVariantName(string baseName, int level)
        {
            LevelGroup group = GetGroup(level);
            return baseName + group.visualSuffix;
        }

        /// <summary>
        /// 주어진 레벨에 해당하는 Placeholder 틴트 색상을 반환합니다.
        /// </summary>
        /// <param name="level">대상 레벨</param>
        /// <returns>레벨 그룹에 대응하는 Color</returns>
        /// <example>
        /// <code>
        /// Color tint = LevelGroupManager.GetPlaceholderColor(5);
        /// // 결과: 연한 하늘색 (Novice 그룹)
        /// </code>
        /// </example>
        public static Color GetPlaceholderColor(int level)
        {
            LevelGroup group = GetGroup(level);
            return group.placeholderColor;
        }
    }
}