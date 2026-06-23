namespace ProjectName.Core
{
    /// <summary>
    /// 몬스터 레벨 라벨 인터페이스.
    /// Systems에서 UI.MonsterLevelLabel에 직접 접근하지 않고
    /// 이 인터페이스를 통해 간접 접근합니다. (순환 참조 방지)
    /// </summary>
    public interface ILevelLabel
    {
        void SetLevel(int level);
        int CurrentLevel { get; }
    }
}