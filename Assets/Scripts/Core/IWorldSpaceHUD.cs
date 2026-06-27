using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>
    /// World Space HUD 인터페이스 — 몬스터/병사 구분 없이 머리 위 오버레이 표시
    /// </summary>
    public interface IWorldSpaceHUD
    {
        /// <summary>월드 좌표 (머리 위 표시 기준점)</summary>
        Vector3 WorldPosition { get; }
        /// <summary>표시 여부</summary>
        bool ShouldShowHUD { get; }
        /// <summary>레벨</summary>
        int HUDLevel { get; }
        /// <summary>호감도 (-100~100, 음수는 적대)</summary>
        float HUDLoyalty { get; }
        /// <summary>중독도 (0~100)</summary>
        float HUDAddiction { get; }
        /// <summary>이름</summary>
        string HUDName { get; }
    }
}
