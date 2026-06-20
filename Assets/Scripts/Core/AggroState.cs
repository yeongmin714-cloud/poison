namespace ProjectName.Core
{
    /// <summary>
    /// 몬스터 어그로 상태 (Idle → Alert(3초) → Combat → Cooldown(5초) → Idle)
    /// </summary>
    public enum AggroState
    {
        /// <summary>비전투 상태. 기본 행동.</summary>
        Idle,
        /// <summary>경계 상태. 공격당한 사실을 인지. 3초 후 Combat으로 전환.</summary>
        Alert,
        /// <summary>전투 상태. 적극적으로 추격/공격.</summary>
        Combat,
        /// <summary>전투 종료 후 쿨다운. 5초 후 Idle 복귀.</summary>
        Cooldown
    }
}