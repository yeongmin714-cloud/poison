using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>
    /// 플레이어 스폰 위치 공유 설정.
    /// 이 값을 수정하면 TestPlayerSetup + MainScene 모두 동일한 위치에 스폰됨.
    /// </summary>
    public static class PlayerSpawnConfig
    {
        /// <summary>스폰 월드 좌표 (x, z 기준. y는 각 씬에서 지형 높이에 따라 보정)</summary>
        public static readonly Vector3 SpawnPosition = new Vector3(0f, 0f, 0f);

        /// <summary>초기 회전값</summary>
        public static readonly Vector3 SpawnEulerAngles = Vector3.zero;
    }
}