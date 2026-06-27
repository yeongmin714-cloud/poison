using UnityEngine;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// 영지/왕국 진입로 (Path/Road) 계산 유틸리티
    /// 영지 중심에서 바깥 방향으로 Path 텍스처 좌표, Vertex 인덱스, 색상 계산
    /// </summary>
    public static class TerrainPathGenerator
    {
        /// <summary>
        /// 영지 위치 기준으로 Path 텍스처 UV 좌표 계산
        /// </summary>
        /// <param name="territoryCenter">영지 중심 월드 좌표</param>
        /// <param name="meshResolution">지형 메시 해상도 (N×N)</param>
        /// <param name="meshSize">지형 크기 (월드 유닛)</param>
        /// <param name="pathWidth">진입로 폭 (4~6m)</param>
        /// <param name="pathLength">진입로 길이 (20~60m)</param>
        /// <returns>Path 영역의 (minU, maxU, minV, maxV) — UV 좌표 범위</returns>
        public static Vector4 CalculatePathUVBounds(
            Vector3 territoryCenter,
            int meshResolution,
            float meshSize,
            float pathWidth,
            float pathLength)
        {
            if (meshResolution < 2)
            {
                Debug.LogError("[TerrainPathGenerator] meshResolution은 2 이상이어야 합니다.");
                return Vector4.zero;
            }

            float halfSize = meshSize * 0.5f;

            // 영지 중심 → UV 좌표
            float centerU = (territoryCenter.x + halfSize) / meshSize;
            float centerV = (territoryCenter.z + halfSize) / meshSize;

            // UV 단위로 변환
            float uvWidth = pathWidth / meshSize;
            float uvLength = pathLength / meshSize;

            // 영지 중심에서 +Z 방향(북쪽)으로 진입로
            float halfWidth = uvWidth * 0.5f;

            return new Vector4(
                centerU - halfWidth,  // minU
                centerU + halfWidth,  // maxU
                centerV,              // minV (영지 중심부터)
                centerV + uvLength    // maxV (북쪽 방향)
            );
        }

        /// <summary>
        /// 주어진 Mesh의 Vertex 중 Path 영역에 속하는 인덱스 반환
        /// </summary>
        /// <param name="vertices">메시 버텍스 배열</param>
        /// <param name="territoryCenter">영지 중심 월드 좌표</param>
        /// <param name="pathWidth">진입로 폭</param>
        /// <param name="pathLength">진입로 길이</param>
        /// <returns>Path 영역에 속하는 버텍스 인덱스 배열</returns>
        public static int[] GetPathVertexIndices(
            Vector3[] vertices,
            Vector3 territoryCenter,
            float pathWidth,
            float pathLength)
        {
            if (vertices == null || vertices.Length == 0)
            {
                Debug.LogError("[TerrainPathGenerator] vertices가 null이거나 비어 있습니다.");
                return System.Array.Empty<int>();
            }

            // Path AABB 계산
            float halfWidth = pathWidth * 0.5f;
            float minX = territoryCenter.x - halfWidth;
            float maxX = territoryCenter.x + halfWidth;
            float minZ = territoryCenter.z;
            float maxZ = territoryCenter.z + pathLength;

            // 영역 내 vertex 수집
            System.Collections.Generic.List<int> indices = new System.Collections.Generic.List<int>();

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                if (v.x >= minX && v.x <= maxX &&
                    v.z >= minZ && v.z <= maxZ)
                {
                    indices.Add(i);
                }
            }

            return indices.ToArray();
        }

        /// <summary>
        /// Path 영역 Vertex 색상 배열 생성 (기존 색상 위에 블렌딩)
        /// </summary>
        /// <param name="vertexCount">전체 버텍스 수</param>
        /// <param name="pathIndices">Path 영역 버텍스 인덱스</param>
        /// <param name="biome">현재 Biome 타입</param>
        /// <param name="existingColors">기존 Vertex 색상 (null이면 흰색 기본)</param>
        /// <returns>Path 색상이 적용된 Vertex 색상 배열</returns>
        public static Color[] ApplyPathVertexColors(
            int vertexCount,
            int[] pathIndices,
            BiomeType biome,
            Color[] existingColors = null)
        {
            Color[] colors = new Color[vertexCount];

            // 기본 색상 설정 (기존 색상 또는 흰색)
            if (existingColors != null && existingColors.Length >= vertexCount)
            {
                for (int i = 0; i < vertexCount; i++)
                    colors[i] = existingColors[i];
            }
            else
            {
                for (int i = 0; i < vertexCount; i++)
                    colors[i] = Color.white;
            }

            // Path 색상
            Color pathColor = GetPathColor(biome);

            // Path 영역 Vertex에 Path 색상 블렌딩 (50% 혼합)
            if (pathIndices != null)
            {
                foreach (int idx in pathIndices)
                {
                    if (idx >= 0 && idx < vertexCount)
                    {
                        Color baseColor = colors[idx];
                        colors[idx] = Color.Lerp(baseColor, pathColor, 0.5f);
                    }
                }
            }

            return colors;
        }

        /// <summary>
        /// Path 텍스처 색상 반환 (Biome 기반)
        /// </summary>
        public static Color GetPathColor(BiomeType biome)
        {
            switch (biome)
            {
                case BiomeType.Plains:
                    return new Color(0.5f, 0.3f, 0.15f);      // 갈색
                case BiomeType.Forest:
                    return new Color(0.4f, 0.25f, 0.1f);     // 진한 갈색
                case BiomeType.Lake:
                    return new Color(0.3f, 0.25f, 0.2f);     // 진흙색
                case BiomeType.Rocky:
                    return new Color(0.45f, 0.45f, 0.4f);    // 회색 돌길
                case BiomeType.Swamp:
                    return new Color(0.35f, 0.3f, 0.15f);    // 짙은 갈색 진흙
                case BiomeType.Reed:
                    return new Color(0.6f, 0.5f, 0.2f);      // 연한 갈색
                case BiomeType.Desert:
                    return new Color(0.7f, 0.6f, 0.3f);      // 더 진한 모래
                case BiomeType.Volcanic:
                    return new Color(0.3f, 0.2f, 0.15f);     // 검은 재/용암길
                case BiomeType.Tundra:
                    return new Color(0.5f, 0.5f, 0.5f);      // 회색 자갈
                case BiomeType.Mountain:
                    return new Color(0.4f, 0.4f, 0.38f);     // 암석길
                case BiomeType.Empire:
                    return new Color(0.6f, 0.6f, 0.65f);     // 돌 블록
                default:
                    return new Color(0.5f, 0.35f, 0.2f);     // 일반 갈색길
            }
        }
    }
}
