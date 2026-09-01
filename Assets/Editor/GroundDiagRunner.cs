using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ProjectName.Systems;
using ProjectName.Core.Data;

/// <summary>
/// 배치 모드 들어 스폰 지점의 정확한 지형 높이 y를 계산한다.
/// 플레이어가 지형 밑으로 추락하지 않도록 스폰 y를 알맞게 맞추기 위함.
/// 실행: Unity -batchmode -executeMethod GroundDiagRunner.Run
/// </summary>
public static class GroundDiagRunner
{
    public static void Run()
    {
        // 1) 각 방위별 대표 스폰 후보 좌표의 지형 높이 계산
        var candidates = new (string name, float x, float z)[]
        {
            ("Empire중심", 0f, 0f),
            ("East현재스폰", 728f, -529f),
            ("East_Ring1첫영지", 1173.42f, -852.12f),
            ("East경계안900m", 728f, -529f),
            ("East 500m", 500f, 0f),
            ("동쪽 초원", 300f, 100f),
        };

        foreach (var (name, x, z) in candidates)
        {
            try
            {
                // East는 Plains(초원) biome — 실제 스폰 구역에 맞춤
                float h2 = TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42);
                Debug.Log($"[SpawnHeight] {name} ({x:F0},{z:F0}) → 지형 y={h2:F2}, 추천 플레이어 y={h2 + 2f:F2}");
            }
            catch (System.Exception e)
            {
                Debug.Log($"[SpawnHeight] {name} 계산 실패: {e.Message}");
            }
        }

        EditorApplication.Exit(0);
    }
}