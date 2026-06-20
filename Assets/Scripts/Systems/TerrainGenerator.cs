using UnityEngine;
using System.Collections.Generic;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// Perlin Noise 기반 절차적 지형 생성기
    /// BiomeDefinition의 파라미터를 사용해 높이맵 생성 → Mesh 변환
    /// </summary>
    public static class TerrainGenerator
    {
        /// <summary>
        /// Perlin Noise로 지형 메시 + 물 메시 생성
        /// </summary>
        /// <param name="biome">생성할 Biome 타입</param>
        /// <param name="seed">랜덤 시드 (결정론적 생성용)</param>
        /// <param name="resolution">그리드 해상도 (N×N vertices)</param>
        /// <param name="size">지형 크기 (월드 유닛)</param>
        /// <returns>(terrainMesh, waterMesh) — waterMesh는 waterThreshold<=0이면 null</returns>
        public static (Mesh terrainMesh, Mesh waterMesh) GenerateTerrain(
            BiomeType biome, int seed, int resolution = 50, float size = 100f)
        {
            BiomeDefinition def = BiomeData.GetDefinition(biome);
            return GenerateTerrainWithDefinition(def, seed, resolution, size);
        }

        /// <summary>
        /// BiomeDefinition을 직접 전달받아 지형 생성
        /// </summary>
        public static (Mesh terrainMesh, Mesh waterMesh) GenerateTerrainWithDefinition(
            BiomeDefinition def, int seed, int resolution = 50, float size = 100f)
        {
            if (resolution < 2)
            {
                Debug.LogError("[TerrainGenerator] Resolution must be >= 2");
                resolution = 2;
            }

            int vertexCount = resolution * resolution;
            int quadCount = (resolution - 1) * (resolution - 1);
            int triangleCount = quadCount * 2;

            // Offset to center the terrain
            float halfSize = size * 0.5f;
            float step = size / (resolution - 1);

            // Vertex arrays
            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uv = new Vector2[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            int[] triangles = new int[triangleCount * 3];

            float freq = def.noiseFrequency;
            float amp = def.noiseAmplitude;
            float waterThreshold = def.waterThreshold;

            // === 1. Perlin Noise 높이맵 생성 ===
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int index = z * resolution + x;

                    // UV (0~1)
                    float u = (float)x / (resolution - 1);
                    float v = (float)z / (resolution - 1);
                    uv[index] = new Vector2(u, v);

                    // 월드 좌표
                    float wx = -halfSize + x * step;
                    float wz = -halfSize + z * step;

                    // Perlin Noise 높이
                    float noiseX = x * freq + seed;
                    float noiseZ = z * freq + seed;
                    float noise = Mathf.PerlinNoise(noiseX, noiseZ);
                    float height = noise * amp;

                    vertices[index] = new Vector3(wx, height, wz);
                }
            }

            // === 2. Triangle 인덱스 생성 ===
            int triIndex = 0;
            for (int z = 0; z < resolution - 1; z++)
            {
                for (int x = 0; x < resolution - 1; x++)
                {
                    int topLeft = z * resolution + x;
                    int topRight = topLeft + 1;
                    int bottomLeft = (z + 1) * resolution + x;
                    int bottomRight = bottomLeft + 1;

                    // Triangle 1: topLeft - topRight - bottomLeft
                    triangles[triIndex++] = topLeft;
                    triangles[triIndex++] = topRight;
                    triangles[triIndex++] = bottomLeft;

                    // Triangle 2: topRight - bottomRight - bottomLeft
                    triangles[triIndex++] = topRight;
                    triangles[triIndex++] = bottomRight;
                    triangles[triIndex++] = bottomLeft;
                }
            }

            // === 3. 노멀 계산 (flat shading) ===
            Vector3[] calculatedNormals = new Vector3[vertexCount];

            for (int i = 0; i < triangleCount; i++)
            {
                int triStart = i * 3;
                int i1 = triangles[triStart];
                int i2 = triangles[triStart + 1];
                int i3 = triangles[triStart + 2];

                Vector3 v1 = vertices[i1];
                Vector3 v2 = vertices[i2];
                Vector3 v3 = vertices[i3];

                Vector3 normal = Vector3.Cross(v2 - v1, v3 - v1).normalized;

                calculatedNormals[i1] += normal;
                calculatedNormals[i2] += normal;
                calculatedNormals[i3] += normal;
            }

            // 노멀 정규화
            for (int i = 0; i < vertexCount; i++)
            {
                normals[i] = calculatedNormals[i].normalized;
            }

            // === 4. 지형 메시 생성 ===
            Mesh terrainMesh = new Mesh();
            terrainMesh.indexFormat = vertexCount > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            terrainMesh.vertices = vertices;
            terrainMesh.triangles = triangles;
            terrainMesh.uv = uv;
            terrainMesh.normals = normals;
            terrainMesh.name = $"Terrain_{def.displayName}_{resolution}x{resolution}";

            // === 5. 물 메시 생성 (waterThreshold > 0) ===
            Mesh waterMesh = null;
            if (waterThreshold > 0f)
            {
                waterMesh = GenerateWaterMesh(vertices, triangles, resolution, step, halfSize, waterThreshold, def.waterColor);
            }

            return (terrainMesh, waterMesh);
        }

        /// <summary>
        /// 물 메시 생성 — waterThreshold 이하 영역만 물로 처리
        /// </summary>
        private static Mesh GenerateWaterMesh(
            Vector3[] terrainVertices, int[] terrainTriangles,
            int resolution, float step, float halfSize,
            float waterThreshold, Color waterColor)
        {
            // 물 높이: threshold의 절반 정도로 설정하여 지형보다 약간 낮게
            float waterLevel = waterThreshold * 0.5f;

            // waterThreshold 이하인 vertex 판별, 물 메시용 vertex/triangle 수집
            List<Vector3> waterVerts = new List<Vector3>();
            List<int> waterTris = new List<int>();
            Dictionary<int, int> vertexMap = new Dictionary<int, int>(); // terrain vert index → water vert index

            int triCount = terrainTriangles.Length / 3;

            for (int t = 0; t < triCount; t++)
            {
                int i1 = terrainTriangles[t * 3];
                int i2 = terrainTriangles[t * 3 + 1];
                int i3 = terrainTriangles[t * 3 + 2];

                // 세 vertex 모두 waterThreshold 이하인 triangle만 물로
                if (terrainVertices[i1].y <= waterThreshold &&
                    terrainVertices[i2].y <= waterThreshold &&
                    terrainVertices[i3].y <= waterThreshold)
                {
                    int wi1 = GetOrAddWaterVertex(waterVerts, vertexMap, i1, terrainVertices, waterLevel);
                    int wi2 = GetOrAddWaterVertex(waterVerts, vertexMap, i2, terrainVertices, waterLevel);
                    int wi3 = GetOrAddWaterVertex(waterVerts, vertexMap, i3, terrainVertices, waterLevel);

                    waterTris.Add(wi1);
                    waterTris.Add(wi2);
                    waterTris.Add(wi3);
                }
            }

            if (waterVerts.Count < 3)
                return null;

            Mesh waterMesh = new Mesh();
            waterMesh.indexFormat = waterVerts.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            waterMesh.vertices = waterVerts.ToArray();
            waterMesh.triangles = waterTris.ToArray();

            // 물 노멀은 항상 위쪽
            Vector3[] waterNormals = new Vector3[waterVerts.Count];
            for (int i = 0; i < waterVerts.Count; i++)
                waterNormals[i] = Vector3.up;

            waterMesh.normals = waterNormals;
            waterMesh.name = $"Water_{resolution}x{resolution}";

            return waterMesh;
        }

        private static int GetOrAddWaterVertex(
            List<Vector3> waterVerts, Dictionary<int, int> vertexMap,
            int terrainIndex, Vector3[] terrainVertices, float waterLevel)
        {
            if (vertexMap.TryGetValue(terrainIndex, out int existing))
                return existing;

            Vector3 src = terrainVertices[terrainIndex];
            Vector3 waterVertex = new Vector3(src.x, waterLevel, src.z);
            int newIndex = waterVerts.Count;
            waterVerts.Add(waterVertex);
            vertexMap[terrainIndex] = newIndex;
            return newIndex;
        }

        /// <summary>
        /// 기존 GameObject의 MeshFilter/MeshRenderer를 업데이트
        /// </summary>
        /// <param name="groundObject">적용할 GameObject (MeshFilter 보유)</param>
        /// <param name="biome">Biome 타입</param>
        /// <param name="seed">랜덤 시드</param>
        public static void ApplyTerrainToGameObject(
            GameObject groundObject, BiomeType biome, int seed,
            Vector3? pathCenter = null, float pathWidth = 5f, float pathLength = 40f)
        {
            BiomeDefinition def = BiomeData.GetDefinition(biome);

            // Mesh 생성
            var (terrainMesh, waterMesh) = GenerateTerrainWithDefinition(def, seed);

            // MeshFilter에 지형 메시 할당
            MeshFilter mf = groundObject.GetComponent<MeshFilter>();
            if (mf == null)
            {
                mf = groundObject.AddComponent<MeshFilter>();
            }
            mf.sharedMesh = terrainMesh;

            // MeshRenderer에 Biome 색상 Material 적용
            MeshRenderer mr = groundObject.GetComponent<MeshRenderer>();
            if (mr == null)
            {
                mr = groundObject.AddComponent<MeshRenderer>();
            }

            // 기본 URP/Lit Material 생성 및 색상 설정
            Material mat = mr.sharedMaterial;
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader != null)
                {
                    mat = new Material(shader);
                }
                else
                {
                    mat = new Material(Shader.Find("Standard"));
                }
                mr.sharedMaterial = mat;
            }

            mat.color = def.surfaceColor;
            mat.name = $"Mat_{def.displayName}";

            // === 진입로 (Path) Vertex 색상 적용 ===
            if (pathCenter.HasValue)
            {
                Mesh mesh = mf.sharedMesh;
                if (mesh != null)
                {
                    Vector3[] vertices = mesh.vertices;
                    int[] pathIndices = TerrainPathGenerator.GetPathVertexIndices(
                        vertices, pathCenter.Value, pathWidth, pathLength);

                    if (pathIndices.Length > 0)
                    {
                        Color[] vertexColors = TerrainPathGenerator.ApplyPathVertexColors(
                            vertices.Length, pathIndices, biome);
                        mesh.colors = vertexColors;
                    }
                }
            }

            // === 물 메시가 있으면 자식 GameObject로 추가 ===
            if (waterMesh != null)
            {
                Transform waterTransform = groundObject.transform.Find("Water");
                GameObject waterObj;
                if (waterTransform == null)
                {
                    waterObj = new GameObject("Water");
                    waterObj.transform.SetParent(groundObject.transform);
                    waterObj.transform.localPosition = Vector3.zero;
                }
                else
                {
                    waterObj = waterTransform.gameObject;
                }

                MeshFilter waterMf = waterObj.GetComponent<MeshFilter>();
                if (waterMf == null)
                    waterMf = waterObj.AddComponent<MeshFilter>();
                waterMf.sharedMesh = waterMesh;

                MeshRenderer waterMr = waterObj.GetComponent<MeshRenderer>();
                if (waterMr == null)
                    waterMr = waterObj.AddComponent<MeshRenderer>();

                if (waterMr.sharedMaterial == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader != null)
                    {
                        Material waterMat = new Material(shader);
                        waterMat.color = def.waterColor;

                        // 반투명 설정
                        waterMat.SetFloat("_Surface", 1.0f);  // Transparent
                        waterMat.SetFloat("_Blend", 0.0f);    // Alpha
                        waterMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        waterMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        waterMat.SetInt("_ZWrite", 0);
                        waterMat.renderQueue = 3000;
                        waterMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        waterMat.EnableKeyword("_ALPHAPREMULTIPLY_ON");

                        waterMr.sharedMaterial = waterMat;
                    }
                    else
                    {
                        waterMr.sharedMaterial = new Material(Shader.Find("Standard"))
                        {
                            color = def.waterColor
                        };
                    }
                }
            }
            else
            {
                // 기존 Water 자식 제거 (biome이 물 없는 타입으로 바뀐 경우)
                Transform existingWater = groundObject.transform.Find("Water");
                if (existingWater != null)
                {
                    Object.DestroyImmediate(existingWater.gameObject);
                }
            }
        }
    }
}