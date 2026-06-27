using System;
using UnityEngine;
using ProjectName.Core.Data;

namespace ProjectName.Core
{
    /// <summary>
    /// 레벨 그룹 정의를 관리하는 정적 매니저.
    /// 5단계 레벨 그룹(Novice, Adept, Veteran, Elite, Legendary)에 대한
    /// 조회, 시각적 변형명 생성, Placeholder 틴트 색상 제공 기능을 제공합니다.
    /// Thread-safe합니다.
    /// </summary>
    public static class LevelGroupManager
    {
        private static readonly Lazy<LevelGroup[]> s_groups = new Lazy<LevelGroup[]>(
            BuildGroups, LazyThreadSafetyMode.ExecutionAndPublication
        );

        private static LevelGroup[] BuildGroups()
        {
            return new LevelGroup[]
            {
                new LevelGroup(
                    LevelGroupId.Novice,
                    "Novice",
                    1, 10,
                    "_tier1",
                    new Color(0.678f, 0.847f, 0.902f)  // 연한 하늘색
                ),
                new LevelGroup(
                    LevelGroupId.Adept,
                    "Adept",
                    11, 20,
                    "_tier2",
                    new Color(0.565f, 0.792f, 0.565f)  // 연한 초록
                ),
                new LevelGroup(
                    LevelGroupId.Veteran,
                    "Veteran",
                    21, 30,
                    "_tier3",
                    new Color(1.0f, 0.843f, 0.4f)      // 황금색
                ),
                new LevelGroup(
                    LevelGroupId.Elite,
                    "Elite",
                    31, 40,
                    "_tier4",
                    new Color(1.0f, 0.647f, 0.0f)      // 주황색
                ),
                new LevelGroup(
                    LevelGroupId.Legendary,
                    "Legendary",
                    41, 50,
                    "_tier5",
                    new Color(0.863f, 0.078f, 0.235f)  // 선명한 빨강
                ),
            };
        }

        /// <summary>
        /// 5개 레벨 그룹 정의를 초기화합니다.
        /// 최초 호출 시 한 번만 실행됩니다. (이후 호출은 무시됩니다.)
        /// Thread-safe합니다.
        /// </summary>
        public static void Initialize()
        {
            _ = s_groups.Value;
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
            LevelGroup[] groups = s_groups.Value;

            if (level <= 0)
                return groups[(int)LevelGroupId.Novice];

            if (level >= groups[(int)LevelGroupId.Legendary].maxLevel)
                return groups[(int)LevelGroupId.Legendary];

            for (int i = 0; i < groups.Length; i++)
            {
                if (level >= groups[i].minLevel && level <= groups[i].maxLevel)
                    return groups[i];
            }

            // Fallback (should not reach here for valid level ranges)
            return groups[(int)LevelGroupId.Novice];
        }

        /// <summary>
        /// 지정한 LevelGroupId에 해당하는 LevelGroup 정의를 반환합니다.
        /// </summary>
        /// <param name="id">조회할 그룹 식별자</param>
        /// <returns>해당 그룹 정의</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// id가 유효한 LevelGroupId 값이 아닌 경우 발생합니다.
        /// </exception>
        public static LevelGroup GetGroup(LevelGroupId id)
        {
            if (!Enum.IsDefined(typeof(LevelGroupId), id))
                throw new ArgumentOutOfRangeException(nameof(id), id,
                    $"Invalid LevelGroupId value: {id}");

            return s_groups.Value[(int)id];
        }

        /// <summary>
        /// 모든 5개의 레벨 그룹 정의를 반환합니다.
        /// 반환된 배열을 수정해도 내부 상태에 영향을 주지 않습니다 (방어적 복사).
        /// </summary>
        /// <returns>LevelGroup 배열 (길이 5, 방어적 복사본)</returns>
        public static LevelGroup[] GetLevelGroups()
        {
            return (LevelGroup[])s_groups.Value.Clone();
        }

        /// <summary>
        /// 기본 이름에 레벨 그룹의 시각적 접미사를 붙여 변형명을 생성합니다.
        /// </summary>
        /// <param name="baseName">기본 이름 (예: "soldier")</param>
        /// <param name="level">대상 레벨</param>
        /// <returns>접미사가 추가된 이름 (예: "soldier_tier1")</returns>
        /// <exception cref="ArgumentNullException">baseName이 null인 경우</exception>
        /// <example>
        /// <code>
        /// string name = LevelGroupManager.GetVisualVariantName("soldier", 5);
        /// // 결과: "soldier_tier1"
        /// </code>
        /// </example>
        public static string GetVisualVariantName(string baseName, int level)
        {
            if (baseName == null)
                throw new ArgumentNullException(nameof(baseName));

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