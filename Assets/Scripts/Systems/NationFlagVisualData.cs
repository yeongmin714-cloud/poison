using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 34: 국가별 국기 시각 정보 확장 메서드.
    /// NationType 열거형에 대한 이모지/시각 확장을 제공합니다.
    /// </summary>
    public static class NationFlagVisualData
    {
        /// <summary>
        /// NationType에 대응하는 국기 문양 이모지를 반환합니다.
        /// </summary>
        public static string GetSymbolEmoji(this NationType nation)
        {
            return nation switch
            {
                NationType.None => "⬜",
                NationType.East => "🌅",
                NationType.West => "🌿",
                NationType.South => "🔥",
                NationType.North => "❄️",
                NationType.Empire => "👑",
                NationType.Dracula => "🧛",
                _ => "❓"
            };
        }

        /// <summary>
        /// NationType에 대응하는 국기 배경색 이름을 반환합니다.
        /// </summary>
        public static string GetFlagColorName(this NationType nation)
        {
            return nation switch
            {
                NationType.None => "회색",
                NationType.East => "파랑",
                NationType.West => "초록",
                NationType.South => "빨강",
                NationType.North => "보라",
                NationType.Empire => "황금",
                NationType.Dracula => "검정",
                _ => "알 수 없음"
            };
        }

        /// <summary>
        /// NationType에 대응하는 문양 이름을 반환합니다.
        /// </summary>
        public static string GetSymbolName(this NationType nation)
        {
            return nation switch
            {
                NationType.None => "미소속",
                NationType.East => "떠오르는 태양",
                NationType.West => "떡갈나무 잎",
                NationType.South => "불꽃",
                NationType.North => "눈송이/산",
                NationType.Empire => "독수리/왕관",
                NationType.Dracula => "박쥐/달",
                _ => "알 수 없음"
            };
        }

        /// <summary>
        /// NationType에 대응하는 국기 설명을 반환합니다.
        /// </summary>
        public static string GetFlagDescription(this NationType nation)
        {
            return nation switch
            {
                NationType.None => "소속되지 않은 영역",
                NationType.East => "동쪽의 시작을 상징",
                NationType.West => "서쪽의 대지와 자연",
                NationType.South => "남쪽의 열정과 전투",
                NationType.North => "북쪽의 냉철함",
                NationType.Empire => "중앙 황제의 권위",
                NationType.Dracula => "밤의 공포, 드라큘라의 영역",
                _ => "알 수 없는 국가"
            };
        }
    }
}