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
        /// East 최외곽(Ring1) 첫 영지 위치 ≈ (728, 0.25, -529) — 초원/초보 구역.
        /// ⚠ 지형은 2000×2000(±1000 범위). x가 1000을 넘으면 지형 밖으로 추락함.
        ///    따라서 경계 안(±1000 미만) 중 초원 외각인 ring≈900 지점 사용.
        /// Empire(중앙 50m)에 스폰되지 않도록 충분히 멀리 떨어뜨림.
        public static readonly Vector3 SpawnPosition = new Vector3(728f, 3f, -529f);
        /// <summary>초기 회전값</summary>
        public static readonly Vector3 SpawnEulerAngles = Vector3.zero;
    }
}