using UnityEngine;
using ProjectName.Core.Data;
using System.Collections.Generic;

namespace ProjectName.Systems
{
    /// <summary>
    /// 영지/왕국 진입로 (Path/Road) 계산 유틸리티
    /// 영지 중심에서 바깥 방향으로 Path 텍스처 좌표, Vertex 인덱스, 색상 계산.
    ///
    /// T5 추가: ApplyPathsToTerrain — 지형 메시 버텍스 색상으로
    /// 황제국 중앙 가장자리에서 4방위(E/N/W/S)로 흙길 4개를 그린다.
    /// URP Lit 은 vertex color 를 _BaseColor 에 곱해 반영하므로 지원됨.
    /// 지형 메시 local XZ 는 월드 XZ 와 같다 (Ground_Inner 가 (0,1,0) 에 위치해
    /// X/Z 는 오프셋이 없고 y 만 1f 가산됨).
    /// 호수와 겹치면 반경*1.4 원호 우회로 돌아간다.
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

            if (meshSize <= 0f)
            {
                Debug.LogError("[TerrainPathGenerator] meshSize는 0보다 커야 합니다.");
                return Vector4.zero;
            }

            if (pathWidth <= 0f)
            {
                Debug.LogWarning("[TerrainPathGenerator] pathWidth가 0 이하입니다. 기본값 4m 사용.");
                pathWidth = 4f;
            }

            if (pathLength <= 0f)
            {
                Debug.LogWarning("[TerrainPathGenerator] pathLength가 0 이하입니다. 기본값 20m 사용.");
                pathLength = 20f;
            }

            float halfSize = meshSize * 0.5f;

            float centerU = (territoryCenter.x + halfSize) / meshSize;
            float centerV = (territoryCenter.z + halfSize) / meshSize;

            float uvWidth = pathWidth / meshSize;
            float uvLength = pathLength / meshSize;

            float halfWidth = uvWidth * 0.5f;

            float minU = Mathf.Clamp01(centerU - halfWidth);
            float maxU = Mathf.Clamp01(centerU + halfWidth);
            float minV = Mathf.Clamp01(centerV);
            float maxV = Mathf.Clamp01(centerV + uvLength);

            return new Vector4(minU, maxU, minV, maxV);
        }

        /// <summary>
        /// 주어진 Mesh의 Vertex 중 Path 영역에 속하는 인덱스 반환
        /// </summary>
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

            if (pathWidth <= 0f)
            {
                Debug.LogError("[TerrainPathGenerator] GetPathVertexIndices: pathWidth가 0 이하입니다.");
                return System.Array.Empty<int>();
            }

            if (pathLength <= 0f)
            {
                Debug.LogError("[TerrainPathGenerator] GetPathVertexIndices: pathLength가 0 이하입니다.");
                return System.Array.Empty<int>();
            }

            float halfWidth = pathWidth * 0.5f;
            float minX = territoryCenter.x - halfWidth;
            float maxX = territoryCenter.x + halfWidth;
            float minZ = territoryCenter.z;
            float maxZ = territoryCenter.z + pathLength;

            List<int> indices = new List<int>();

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
        public static Color[] ApplyPathVertexColors(
            int vertexCount,
            int[] pathIndices,
            BiomeType biome,
            Color[] existingColors = null)
        {
            Color[] colors = new Color[vertexCount];

            if (existingColors != null)
            {
                if (existingColors.Length >= vertexCount)
                {
                    for (int i = 0; i < vertexCount; i++)
                    {
                        colors[i] = existingColors[i];
                    }
                }
                else
                {
                    Debug.LogWarning("[TerrainPathGenerator] existingColors 길이가 vertexCount보다 작습니다. 흰색으로 초기화합니다.");
                    for (int i = 0; i < vertexCount; i++)
                    {
                        colors[i] = Color.white;
                    }
                }
            }
            else
            {
                for (int i = 0; i < vertexCount; i++)
                {
                    colors[i] = Color.white;
                }
            }

            Color pathColor = GetPathColor(biome);

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
                case BiomeType.Plains: return new Color(0.5f, 0.3f, 0.15f);      // 갈색
                case BiomeType.Forest: return new Color(0.4f, 0.25f, 0.1f);     // 진한 갈색
                case BiomeType.Lake: return new Color(0.3f, 0.25f, 0.2f);     // 진흙색
                case BiomeType.Rocky: return new Color(0.45f, 0.45f, 0.4f);    // 회색 돌길
                case BiomeType.Swamp: return new Color(0.35f, 0.3f, 0.15f);    // 짙은 갈색 진흙
                case BiomeType.Reed: return new Color(0.6f, 0.5f, 0.2f);      // 연한 갈색
                case BiomeType.Desert: return new Color(0.7f, 0.6f, 0.3f);      // 더 진한 모래
                case BiomeType.Volcanic: return new Color(0.3f, 0.2f, 0.15f);     // 검은 재/용암길
                case BiomeType.Tundra: return new Color(0.5f, 0.5f, 0.5f);      // 회색 자갈
                case BiomeType.Mountain: return new Color(0.4f, 0.4f, 0.38f);     // 암석길
                case BiomeType.Empire: return new Color(0.6f, 0.6f, 0.65f);     // 돌 블록
                default: return new Color(0.5f, 0.35f, 0.2f);     // 일반 갈색길
            }
        }

        // ================================================================
        // T5 진입로 메시 버텍스 컬러 적용 (별도 진입점)
        // ================================================================

        private struct DetourArc
        {
            public float tEntry;
            public float tExit;
            public List<Vector3> points;
        }

        private const float PathWidth = 5f;            // 흙길 폭 5m
        private const float PathHalfWidth = 2.5f;
        private const float EmpireEdgeRadius = 60f;    // 황제국 가장자리
        private const float RoadLengthMeters = 700f;   // 반경 ~700m 까지
        private const float SampleStep = 8f;           // 경로 샘플 간격 (m)
        private const float BlendFactor = 0.6f;        // 흙길 블렌드 강도
        private const float ArcStepDeg = 8f;           // 원호 보간 각도 간격(도)

        /// <summary>
        /// T5 대표 진입점 — 지형 메시 버텍스 색상에 황제국 4방위 흙길을 그린다.
        /// 호출 시점은 상위 통합 페이즈가 결정한다 (TerrainTextureApplier 와 충돌 없음,
        /// 이 메서드는 버텍스 컬러만 주입).
        /// </summary>
        public static void ApplyPathsToTerrain(Mesh terrainMesh, Transform groundTransform)
        {
            if (terrainMesh == null)
            {
                Debug.LogError("[TerrainPathGenerator] ApplyPathsToTerrain: terrainMesh가 null입니다.");
                return;
            }

            Vector3[] vertices = terrainMesh.vertices;
            if (vertices == null || vertices.Length == 0)
            {
                Debug.LogWarning("[TerrainPathGenerator] ApplyPathsToTerrain: vertices가 비어 있습니다.");
                return;
            }

            float gx = groundTransform != null ? groundTransform.position.x : 0f;
            float gz = groundTransform != null ? groundTransform.position.z : 0f;

            // 4방위 진입로
            Vector3[] dirs =
            {
                Vector3.right,   // East  (+x)
                Vector3.forward, // North (+z)
                Vector3.left,    // West  (-x)
                Vector3.back     // South (-z)
            };
            List<List<Vector3>> roads = new List<List<Vector3>>();
            for (int i = 0; i < dirs.Length; i++)
            {
                Vector3 start = dirs[i] * EmpireEdgeRadius;
                Vector3 end = dirs[i] * RoadLengthMeters;
                roads.Add(BuildRoadWithLakeDetour(start, end, dirs[i]));
            }

            // 기존 버텍스 컬러 보존 (없으면 흰색)
            Color[] colors = terrainMesh.colors;
            if (colors == null || colors.Length != vertices.Length)
            {
                colors = new Color[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                    colors[i] = Color.white;
            }

            // 어두운 흙색 (Plains 갈색 계열 + 블랙 톤)
            Color dirt = GetPathColor(BiomeType.Plains);
            dirt = Color.Lerp(dirt, Color.black, 0.25f);

            float halfW2 = PathHalfWidth * PathHalfWidth;
            int marked = 0;

            for (int i = 0; i < vertices.Length; i++)
            {
                float wx = vertices[i].x + gx;
                float wz = vertices[i].z + gz;
                float bestDistSq = float.MaxValue;

                for (int r = 0; r < roads.Count; r++)
                {
                    var pts = roads[r];
                    for (int s = 0; s < pts.Count - 1; s++)
                    {
                        float d = DistToSegmentSq(wx, wz, pts[s], pts[s + 1]);
                        if (d < bestDistSq)
                            bestDistSq = d;
                    }
                }

                if (bestDistSq <= halfW2)
                {
                    colors[i] = Color.Lerp(colors[i], dirt, BlendFactor);
                    marked++;
                }
            }

            if (marked > 0)
            {
                terrainMesh.colors = colors;
                terrainMesh.UploadMeshData(false);
                Debug.Log("[TerrainPathGenerator] 진입로 적용 완료: " + marked + " vertices 흙길 처리.");
            }
            else
            {
                Debug.LogWarning("[TerrainPathGenerator] 진입로 vertices를 감지하지 못했습니다. 경로/해상도 점검 필요.");
            }
        }

        private static List<Vector3> BuildRoadWithLakeDetour(
            Vector3 start, Vector3 end, Vector3 dir)
        {
            List<Vector3> waypoints = new List<Vector3>();
            waypoints.Add(start);

            Vector3 d = dir.normalized;
            float segLen = Vector3.Distance(start, end);

            List<DetourArc> arcs = new List<DetourArc>();
            var lakes = TerrainGenerator.Lakes;
            if (lakes != null)
            {
                for (int i = 0; i < lakes.Count; i++)
                {
                    var lake = lakes[i];
                    float R = lake.radius * 1.4f;   // 호수 반경*1.4 우회

                    Vector3 c = new Vector3(lake.center.x, start.y, lake.center.z);
                    Vector3 rel = c - start;
                    float proj = Vector3.Dot(rel, d);
                    if (proj <= 0f || proj >= segLen) continue;

                    Vector3 closest = start + d * proj;
                    float dist = Vector3.Distance(
                        new Vector3(c.x, start.y, c.z),
                        new Vector3(closest.x, start.y, closest.z));
                    if (dist >= R) continue;   // 이 직선 도로는 이 호수를 건너지 않음

                    // 직선 경로상 거리=R 가 되는 진입/출구 파라미터 t1,t2
                    Vector3 cRel = new Vector3(c.x - start.x, start.y - start.y, c.z - start.z);
                    float b = -2f * Vector3.Dot(cRel, d);   // |d|=1 이므로 a=1
                    float cc = cRel.sqrMagnitude - R * R;
                    float disc = b * b - 4f * cc;
                    if (disc < 0f) continue;
                    float sq = Mathf.Sqrt(disc);
                    float t1 = (-b - sq) / 2f;
                    float t2 = (-b + sq) / 2f;
                    t1 = Mathf.Clamp(t1, 0f, segLen);
                    t2 = Mathf.Clamp(t2, 0f, segLen);
                    if (t2 - t1 < 0.5f) continue;

                    // 원호 우회 보간
                    Vector3 entryPt = start + d * t1;
                    Vector3 exitPt = start + d * t2;
                    List<Vector3> arcPts = BuildArcWaypoints(entryPt, exitPt, c, R);
                    if (arcPts.Count >= 2)
                        arcs.Add(new DetourArc { tEntry = t1, tExit = t2, points = arcPts });
                }
            }

            // arcs를 tEntry 순 정렬 후 직선+원호로 폴리라인 조립
            arcs.Sort((x, y) => x.tEntry.CompareTo(y.tEntry));
            float tCur = 0f;
            foreach (var arc in arcs)
            {
                AppendStraight(waypoints, start, d, tCur, arc.tEntry);
                foreach (var pt in arc.points)
                    waypoints.Add(pt);
                tCur = arc.tExit;
            }
            AppendStraight(waypoints, start, d, tCur, segLen);

            // 중복 점 제거
            List<Vector3> clean = new List<Vector3>();
            for (int i = 0; i < waypoints.Count; i++)
            {
                if (clean.Count == 0 ||
                    Vector3.Distance(clean[clean.Count - 1], waypoints[i]) > 0.5f)
                    clean.Add(waypoints[i]);
            }
            return clean;
        }

        private static void AppendStraight(
            List<Vector3> output, Vector3 start, Vector3 d, float tA, float tB)
        {
            if (tB < tA) return;
            for (float t = tA; t <= tB; t += SampleStep)
            {
                Vector3 p = start + d * t;
                if (output.Count == 0 || Vector3.Distance(output[output.Count - 1], p) > 0.5f)
                    output.Add(p);
            }
            Vector3 end = start + d * tB;
            if (output.Count == 0 || Vector3.Distance(output[output.Count - 1], end) > 0.1f)
                output.Add(end);
        }

        /// <summary>
        /// 호수 중심을 원점으로 반지름 R 원호 (진입점→출구점). R=반경*1.4 이므로
        /// R 거리를 유지해 호수를 우회하고 돌아온다.
        /// </summary>
        private static List<Vector3> BuildArcWaypoints(
            Vector3 entryPt, Vector3 exitPt, Vector3 center, float radius)
        {
            Vector3 c = new Vector3(center.x, entryPt.y, center.z);
            float a0 = Mathf.Atan2(entryPt.z - c.z, entryPt.x - c.x);
            float a1 = Mathf.Atan2(exitPt.z - c.z, exitPt.x - c.x);

            // [-pi, pi] 부호 있는 각차 — 짧은 원호로 우회 (반지름 R 유지).
            float signedDelta = Mathf.DeltaAngle(a0 * Mathf.Rad2Deg, a1 * Mathf.Rad2Deg) * Mathf.Deg2Rad;

            int steps = Mathf.Max(4, Mathf.CeilToInt(Mathf.Abs(signedDelta * Mathf.Rad2Deg) / ArcStepDeg));
            List<Vector3> pts = new List<Vector3>();
            for (int k = 0; k <= steps; k++)
            {
                float t = (float)k / steps;
                float a = a0 + signedDelta * t;
                pts.Add(new Vector3(
                    c.x + Mathf.Cos(a) * radius,
                    entryPt.y,
                    c.z + Mathf.Sin(a) * radius));
            }
            return pts;
        }

        private static float DistToSegmentSq(float px, float pz, Vector3 a, Vector3 b)
        {
            float ax = a.x, az = a.z;
            float bx = b.x, bz = b.z;
            float dx = bx - ax, dz = bz - az;
            float lenSq = dx * dx + dz * dz;
            float t;
            if (lenSq > 0.0001f)
                t = Mathf.Clamp01(((px - ax) * dx + (pz - az) * dz) / lenSq);
            else
                t = 0f;
            float cx = ax + dx * t, cz = az + dz * t;
            float rx = px - cx, rz = pz - cz;
            return rx * rx + rz * rz;
        }
    }
}
