namespace ProjectName.Core.Data
{
    /// <summary>
    /// Phase 30: 국가별 표시 이름 상수.
    /// NationType 열거형 값에 대응하는 한글 국가명을 제공합니다.
    /// </summary>
    public static class NationNames
    {
        /// <summary>동방 비르텐시아 왕국</summary>
        public const string KingdomNameEast = "동방 비르텐시아 왕국";

        /// <summary>서부 아르델리아 대공국</summary>
        public const string KingdomNameWest = "서부 아르델리아 대공국";

        /// <summary>남부 이그니스 제국</summary>
        public const string KingdomNameSouth = "남부 이그니스 제국";

        /// <summary>북부 프로스트가드 왕국</summary>
        public const string KingdomNameNorth = "북부 프로스트가드 왕국";

        /// <summary>중앙 아우레우스 제국</summary>
        public const string EmpireName = "중앙 아우레우스 제국";

        /// <summary>
        /// NationType에 대응하는 국가명 반환.
        /// </summary>
        public static string GetName(NationType nation)
        {
            return nation switch
            {
                NationType.East => KingdomNameEast,
                NationType.West => KingdomNameWest,
                NationType.South => KingdomNameSouth,
                NationType.North => KingdomNameNorth,
                NationType.Empire => EmpireName,
                _ => "알 수 없는 국가"
            };
        }
    }
}
