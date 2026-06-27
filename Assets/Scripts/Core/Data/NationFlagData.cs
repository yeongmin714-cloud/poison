using UnityEngine;

namespace ProjectName.Core.Data
{
    /// <summary>
    /// 국가별 국기 정의 데이터.
    /// 각 국가(NationType)의 색상, 문양, 설명을 담습니다.
    /// </summary>
    [System.Serializable]
    public struct NationFlagDefinition
    {
        /// <summary>국가</summary>
        [field: SerializeField]
        public NationType nation { get; set; }

        /// <summary>상징 색상 이름 (예: "파랑", "초록")</summary>
        [field: SerializeField]
        public string colorName { get; set; }

        /// <summary>국기 설명</summary>
        [field: SerializeField]
        public string description { get; set; }

        /// <summary>국기 배경색 실제 Color 값</summary>
        [field: SerializeField]
        public Color flagColor { get; set; }

        /// <summary>문양 이름 (예: "떠오르는 태양")</summary>
        [field: SerializeField]
        public string symbolName { get; set; }

        /// <summary>문양 이모지 (예: "🌅")</summary>
        [field: SerializeField]
        public string symbolEmoji { get; set; }
    }
}