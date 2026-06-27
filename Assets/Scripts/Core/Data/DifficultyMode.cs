namespace ProjectName.Core.Data
{
    /// <summary>
    /// C20-01: 게임 난이도 열거형.
    /// <br/>
    /// ⚠ Easy=0 이므로 struct/class 필드 기본값(default)은 Easy입니다.
    /// 직렬화 클래스에서 DifficultyMode 필드를 선언할 때는 반드시
    /// <c>= DifficultyMode.Normal</c> 로 명시적 초기화하세요.
    /// (예: SaveData.cs의 difficulty 필드)
    /// </summary>
    public enum DifficultyMode
    {
        Easy = 0,
        Normal = 1,
        Hard = 2
    }
}