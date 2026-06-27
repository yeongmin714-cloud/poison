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
    public struct LevelGroup : IEquatable<LevelGroup>
    {
        /// <summary>그룹 식별자</summary>
        public LevelGroupId groupId;

        /// <summary>그룹 표시명 (예: "Novice", "Adept")</summary>
        public string groupName;

        /// <summary>그룹 최소 레벨 (inclusive)</summary>
        public int minLevel;

        /// <summary>그룹 최대 레벨 (inclusive)</summary>
        public int maxLevel;

        /// <summary>시각적 변형 접미사 (예: "_tier1", "_tier2")</summary>
        public string visualSuffix;

        /// <summary>Placeholder 틴트 색상 (레벨 그룹별 시각 구분용)</summary>
        public Color placeholderColor;

        /// <summary>
        /// LevelGroup 생성자.
        /// </summary>
        /// <param name="id">그룹 식별자</param>
        /// <param name="name">그룹 표시명</param>
        /// <param name="minLevel">최소 레벨 (inclusive, 1 이상)</param>
        /// <param name="maxLevel">최대 레벨 (inclusive, minLevel 이상)</param>
        /// <param name="visualSuffix">시각적 접미사</param>
        /// <param name="color">Placeholder 틴트 색상</param>
        /// <exception cref="ArgumentNullException">name 또는 visualSuffix가 null인 경우</exception>
        /// <exception cref="ArgumentOutOfRangeException">minLevel이 1 미만인 경우</exception>
        /// <exception cref="ArgumentException">minLevel이 maxLevel보다 큰 경우</exception>
        public LevelGroup(LevelGroupId id, string name, int minLevel, int maxLevel, string visualSuffix, Color color)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (visualSuffix == null)
                throw new ArgumentNullException(nameof(visualSuffix));
            if (minLevel < 1)
                throw new ArgumentOutOfRangeException(nameof(minLevel), minLevel, "minLevel은 1 이상이어야 합니다.");
            if (maxLevel < minLevel)
                throw new ArgumentException($"maxLevel({maxLevel})은 minLevel({minLevel}) 이상이어야 합니다.", nameof(maxLevel));

            groupId = id;
            groupName = name;
            this.minLevel = minLevel;
            this.maxLevel = maxLevel;
            this.visualSuffix = visualSuffix;
            placeholderColor = color;
        }

        /// <summary>
        /// 이 인스턴스가 지정된 LevelGroup과 값이 같은지 여부를 확인합니다.
        /// </summary>
        public bool Equals(LevelGroup other)
        {
            return groupId == other.groupId
                && minLevel == other.minLevel
                && maxLevel == other.maxLevel
                && groupName == other.groupName
                && visualSuffix == other.visualSuffix
                && placeholderColor.Equals(other.placeholderColor);
        }

        /// <summary>
        /// 이 인스턴스가 지정된 객체와 값이 같은지 여부를 확인합니다.
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is LevelGroup other && Equals(other);
        }

        /// <summary>
        /// 이 인스턴스의 해시 코드를 반환합니다.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)2166136261;
                hash = (hash * 16777619) ^ (int)groupId;
                hash = (hash * 16777619) ^ (minLevel);
                hash = (hash * 16777619) ^ (maxLevel);
                hash = (hash * 16777619) ^ (groupName?.GetHashCode() ?? 0);
                hash = (hash * 16777619) ^ (visualSuffix?.GetHashCode() ?? 0);
                hash = (hash * 16777619) ^ placeholderColor.GetHashCode();
                return hash;
            }
        }

        /// <summary>
        /// 두 LevelGroup 인스턴스의 값이 같은지 비교합니다.
        /// </summary>
        public static bool operator ==(LevelGroup left, LevelGroup right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// 두 LevelGroup 인스턴스의 값이 다른지 비교합니다.
        /// </summary>
        public static bool operator !=(LevelGroup left, LevelGroup right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// 이 인스턴스의 문자열 표현을 반환합니다.
        /// </summary>
        public override string ToString()
        {
            return $"{groupName}[{minLevel}-{maxLevel}]({groupId})";
        }
    }
}