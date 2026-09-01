using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>
    /// 플레이어 스폰 위치 공유 설정.
    /// 이 값을 수정하면 TestPlayerSetup + MainScene 모두 동일한 위치에 스폰됨.
    /// </summary>
    public static class PlayerSpawnConfig
    {
        /// <summary>스폰 월드 좌표 (x, z 기준. y는 각 씬에서 지형 높이에 따라 보정)
        /// 로드맵: 처음 스폰은 병사/몬스터가 약한 외각 지역(Ring1)이어야 함.
        /// East 최외곽(Ring1) 첫 영지 위치 ≈ (1173, 0, -852) — 초원/초보 구역.
        /// y = 지형 높이(GetHeightAt 계산값 0.25). PlayerMovement가 이 y+2f로 스폰해 지형 위 2m에서 안전 착지.
        public static readonly Vector3 SpawnPosition = new Vector3(1173f, 0.25f, -852f);

        /// <summary>초기 회전값</summary>
        public static readonly Vector3 SpawnEulerAngles = Vector3.zero;
    }
}