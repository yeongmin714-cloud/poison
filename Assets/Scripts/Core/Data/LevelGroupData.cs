using System;
using UnityEngine;

namespace ProjectName.Core.Data
{
    /// <summary>
    /// 아바타 레벨 그룹 식별자.
    /// 5단계 레벨 그룹: Novice(1-10), Adept(11-20), Veteran(21-30), Elite(31-40), Legendary(41-50).
    /// </summary>
    public enum LevelGroupId
    {
        /// <summary>초심자 (레벨 1~10)</summary>
        Novice = 0,

        /// <summary>숙련자 (레벨 11~20)</summary>
        Adept = 1,

        /// <summary>베테랑 (레벨 21~30)</summary>
        Veteran = 2,

        /// <summary>정예 (레벨 31~40)</summary>
        Elite = 3,

        /// <summary>전설 (레벨 41~50)</summary>
        Legendary = 4
    }

    /// <summary>
    /// 아바타 레벨 그룹 데이터.
    /// 각 그룹은 레벨 범위(minLevel~maxLevel, inclusive), 표시명, 시각적 접미사,
    /// 및 Placeholder 틴트 색상을 정의합니다.
    /// </summary>
    [Serializable]
    public struct LevelGroup
    {
        /// <summary>그룹 식별자</summary>
        [SerializeField]
        public LevelGroupId groupId;

        /// <summary>그룹 표시명 (예: "Novice", "Adept")</summary>
        [SerializeField]
        public string groupName;

        /// <summary>그룹 최소 레벨 (inclusive)</summary>
        [SerializeField]
        public int minLevel;

        /// <summary>그룹 최대 레벨 (inclusive)</summary>
        [SerializeField]
        public int maxLevel;

        /// <summary>시각적 변형 접미사 (예: "_tier1", "_tier2")</summary>
        [SerializeField]
        public string visualSuffix;

        /// <summary>Placeholder 틴트 색상 (레벨 그룹별 시각 구분용)</summary>
        [SerializeField]
        public Color placeholderColor;

        /// <summary>
        /// LevelGroup 생성자.
        /// </summary>
        /// <param name="id">그룹 식별자</param>
        /// <param name="name">그룹 표시명</param>
        /// <param name="min">최소 레벨 (inclusive)</param>
        /// <param name="max">최대 레벨 (inclusive)</param>
        /// <param name="suffix">시각적 접미사</param>
        /// <param name="color">Placeholder 틴트 색상</param>
        public LevelGroup(LevelGroupId id, string name, int min, int max, string suffix, Color color)
        {
            groupId = id;
            groupName = name;
            minLevel = min;
            maxLevel = max;
            visualSuffix = suffix;
            placeholderColor = color;
        }
    }
}