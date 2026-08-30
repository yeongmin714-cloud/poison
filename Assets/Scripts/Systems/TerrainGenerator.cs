using UnityEngine;
using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// Perlin Noise 기반 절차적 지형 생성기 (FBM 다중 옥타브 노이즈 + 고원 구역)
    /// BiomeDefinition의 파라미터를 사용해 높이맵 생성 → Mesh 변환
    /// </summary>
    public static class TerrainGenerator
    {
        // === FBM (Fractal Brownian Motion) 하드코딩 상수 ===
        // 노이즈 옥타브 수 — 클수록 디테일이 증가 (표준 4)
        private const int FBM_OCTAVES = 4;
        // 주파수 배율 — 옥타브가 올라갈 때마다 주파수가 이 배율만큼 증가
        private const float FBM_LACUNARITY = 2.0f;
        // 진폭 감쇠 — 옥타브가 올라갈 때마다 진폭이 이 배율만큼 감소
        private const float FBM_GAIN = 0.5f;
        // 고원(plateau) 평탄화 시작 임계값
        private const float PLATEAU_THRESHOLD = 0.55f;
        // 고원 평탄화 감쇠 계수
        private const float PLATEAU_SLOPE = 0.2f;
        // === 방위별 지형 블렌딩 상수 ===
        // 국경 전환 폭 (size=2000f 기준 약 size*0.06 ≈ 120m) — 각도 경계 근처에서 이웃 방위와 크로스페이드
        private const float TRANSITION_WIDTH = 120f;
        // 황제국 중앙 영역 반경 (NationTerrainController.GetNationFromPosition 기준 50m)
        private const float EMPIRE_RADIUS = 50f;

        /// <summary>
        /// 주어진 월드 좌표에서 지형 높이 반환 (FBM + 고원 기반)
        /// TerrainModelPlacer 등에서 Raycast 없이 높이 샘플링용
        /// </summary>
        public static float GetHeightAt(float worldX, float worldZ, BiomeType biome, int seed = 42)
        {
            BiomeDefinition def = BiomeData.GetDefinition(biome);
            return GetHeightAtWithDefinition(worldX, worldZ, def, seed);
        }

        /// <summary>
        /// BiomeDefinition으로 높이 샘플링
        /// </summary>
        public static float GetHeightAtWithDefinition(float worldX, float worldZ, BiomeDefinition def, int seed = 42)
        {
            float height = ComputeTerrainHeight(worldX, worldZ, def.type, seed);

            // waterThreshold가 있으면 물 높이로 클램프 (물 로직 유지)
            if (def.waterThreshold > 0f && height < def.waterThreshold)
            {
                height = def.waterThreshold;
            }
            
            return height;
        }

        /// <summary>
        /// 공통 높이 계산 헬퍼 — FBM 노이즈(0~1) → 고원 변환 → def.noiseAmplitude 곱
        /// </summary>
        private static float ComputeBaseHeight(float x, float z, BiomeDefinition def, int seed)
        {
            // FBM 다중 옥타브 노이즈 (0~1 정규화)
            float fbm = FbmNoise(
                x * def.noiseFrequency,
                z * def.noiseFrequency,
                FBM_OCTAVES,
                FBM_LACUNARITY,
                FBM_GAIN,
                seed);

            // 고원(plateau) 변환으로 상위 높이 구간 평탄화
            float plateau = ApplyPlateau(fbm);

            // 최종 높이 = 평탄화된 노이즈 × 진폭
            return plateau * def.noiseAmplitude;
        }

        /// <summary>
        /// FBM (Fractal Brownian Motion) — 다중 옥타브 Perlin 노이즈를 누적해 더 자연스러운 지형 생성.
        /// 각 옥타브에 seed 기반 오프셋을 섞어 옥타브마다 서로 다른 패턴이 나오게 하고,
        /// amplitude 합으로 정규화해 결과를 0~1 범위로 유지한다.
        /// </summary>
        private static float FbmNoise(float x, float z, int octaves, float lacunarity, float gain, int seed)
        {
            float total = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float amplitudeSum = 0f;

            for (int o = 0; o < octaves; o++)
            {
                // seed 기반 옥타브별 오프셋 — 옥타브마다 다른 패턴 유도
                float noiseX = x * frequency + seed * 0.371f;
                float noiseZ = z * frequency + seed * 0.713f;

                total += Mathf.PerlinNoise(noiseX, noiseZ) * amplitude;
                amplitudeSum += amplitude;

                frequency *= lacunarity;
                amplitude *= gain;
            }

            // amplitude 합으로 정규화 → 0~1 유지
            return amplitudeSum > 0f ? total / amplitudeSum : 0f;
        }

        /// <summary>
        /// 고원(plateau) 변환 — 상위 높이 구간(t>0.55)을 평탄화해 대지/고원 모양 생성.
        /// 임계값을 초과한 만큼의 일부(0.2배)만 치우쳐 높이가 빠르게 올라가지 않도록 억제.
        /// </summary>
        private static float ApplyPlateau(float t)
        {
            if (t > PLATEAU_THRESHOLD)
            {
                // 임계값 초과분의 0.2배만 반영 — 완만한 고원 평면 유도
                return PLATEAU_THRESHOLD + (t - PLATEAU_THRESHOLD) * PLATEAU_SLOPE;
            }
            return t;
        }

        /// <summary>
        /// 방위별 지형 파라미터 (NationType → BiomeType + 부가 계수 + 고유 시드 오프셋).
        /// 각 방위는 고유한 높이 시드(base+offset)와 FBM 계수 셋(진폭/빈도/plateau 강도)을 가진다.
        /// </summary>
        private struct NationTerrainParams
        {
            public BiomeType biome;          // 방위 대표 Biome
            public float amplitude;          // 노이즈 진폭
            public float frequency;          // 노이즈 빈도
            public float plateauStrength;    // 고원 평탄화 강도 (0=없음, 1=완전)
            public int seedOffset;           // 고유 시드 오프셋 (base + offset)
        }

        /// <summary>
        /// 방위별 고유 파라미터 반환.
        ///   East   → Plains(낮고 완만한 초원)
        ///   South  → Desert(평탄 사막)
        ///   North  → Tundra(높고 험준한 설산, plateau)
        ///   West   → Volcanic(화산 굴곡, 약간 plateau)
        ///   Empire → Empire(평탄 대리석/황실, plateau)
        /// </summary>
        private static NationTerrainParams GetNationParams(NationType nation)
        {
            switch (nation)
            {
                case NationType.East:
                    return new NationTerrainParams { biome = BiomeType.Plains, amplitude = 0.5f, frequency = 2.5f, plateauStrength = 0.0f, seedOffset = 10 };
                case NationType.South:
                    return new NationTerrainParams { biome = BiomeType.Desert, amplitude = 0.8f, frequency = 2.0f, plateauStrength = 0.0f, seedOffset = 20 };
                case NationType.North:
                    return new NationTerrainParams { biome = BiomeType.Tundra, amplitude = 4.0f, frequency = 1.5f, plateauStrength = 1.0f, seedOffset = 30 };
                case NationType.West:
                    return new NationTerrainParams { biome = BiomeType.Volcanic, amplitude = 2.0f, frequency = 2.5f, plateauStrength = 0.5f, seedOffset = 40 };
                case NationType.Empire:
                    return new NationTerrainParams { biome = BiomeType.Empire, amplitude = 0.2f, frequency = 1.0f, plateauStrength = 1.0f, seedOffset = 50 };
                default:
                    // 미소속(None/Dracula) — East(Plains) 기본값
                    return new NationTerrainParams { biome = BiomeType.Plains, amplitude = 0.5f, frequency = 2.5f, plateauStrength = 0.0f, seedOffset = 10 };
            }
        }

        /// <summary>
        /// 단일 방위 기준 높이 계산. ComputeBaseHeight/FbmNoise/ApplyPlateau 재사용.
        /// 방위별 BiomeDefinition(진폭·빈도 오버라이드)과 고유 시드를 사용해 높이 산출.
        /// </summary>
        private static float ComputeNationHeight(float x, float z, NationType nation, int seed)
        {
            NationTerrainParams p = GetNationParams(nation);
            BiomeDefinition def = BiomeData.GetDefinition(p.biome);

            // 방위별 진폭/빈도 반영 (구조체 복사본이므로 안전)
            def.noiseAmplitude = p.amplitude;
            def.noiseFrequency = p.frequency;

            int nationSeed = seed + p.seedOffset;

            // FBM 다중 옥타브 → 고원 변환 (plateauStrength로 블렌드)
            float fbm = FbmNoise(
                x * def.noiseFrequency,
                z * def.noiseFrequency,
                FBM_OCTAVES, FBM_LACUNARITY, FBM_GAIN, nationSeed);

            float plateau = ApplyPlateau(fbm);
            float shaped = Mathf.Lerp(fbm, plateau, p.plateauStrength);

            return shaped * def.noiseAmplitude;
        }

        /// <summary>
        /// 각도(방위, 황제국 제외)만으로 방향성 국가 판정.
        /// GetNationFromPosition과 동일한 각도 경계를 사용하되 중앙 Empire 판정을 제외.
        /// </summary>
        private static NationType GetDirectionalNation(float angle)
        {
            // angle은 [0, 360)
            if (angle < 45f || angle >= 315f)
                return NationType.East;
            if (angle < 135f)
                return NationType.North;
            if (angle < 225f)
                return NationType.West;
            return NationType.South;
        }

        /// <summary>
        /// 방위별 절차적 지형 높이 계산.
        /// 월드 좌표 (x, z) → NationType 판정 → 방위별 고유 FBM 파라미터/시드로 높이 산출.
        /// 국경(내각 경계 각도 근처) 및 황제국 경계에서는 이웃 방위와 부드럽게 크로스페이드해
        /// 급격한 절벽/단차가 생기지 않도록 한다.
        /// </summary>
        /// <param name="x">월드 X 좌표</param>
        /// <param name="z">월드 Z 좌표</param>
        /// <param name="biome">기본 Biome (방위 판정이 우선함)</param>
        /// <param name="seed">기저 시드</param>
        private static float ComputeTerrainHeight(float x, float z, BiomeType biome, int seed)
        {
            float dist = Mathf.Sqrt(x * x + z * z);

            // 각도 계산 (0~360)
            float angle = Mathf.Atan2(z, x) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;

            NationType nation = NationTerrainController.GetNationFromPosition(new Vector3(x, 0f, z));

            float h = ComputeNationHeight(x, z, nation, seed);

            // === 1) 방향성 국가 간 각도 경계 크로스페이드 ===
            // 내각 경계 각도: 45°(동-북), 135°(북-서), 225°(서-남), 315°(남-동)
            // 경계 광선(ray)에 대한 수직 거리를 블렌드 계수로 사용.
            // 각 경계는 (음측 국가 nA, 양측 국가 nB, 광선 성분 ux, uz)로 정의.
            float halfWidth = TRANSITION_WIDTH;

            // 동-북 경계 (45°)
            BlendBoundary(
                ref h, x, z, seed,
                NationType.East, NationType.North,
                0.70710678f, 0.70710678f, halfWidth);

            // 북-서 경계 (135°)
            BlendBoundary(
                ref h, x, z, seed,
                NationType.North, NationType.West,
                -0.70710678f, 0.70710678f, halfWidth);

            // 서-남 경계 (225°)
            BlendBoundary(
                ref h, x, z, seed,
                NationType.West, NationType.South,
                -0.70710678f, -0.70710678f, halfWidth);

            // 남-동 경계 (315°)
            BlendBoundary(
                ref h, x, z, seed,
                NationType.South, NationType.East,
                0.70710678f, -0.70710678f, halfWidth);

            // === 2) 황제국 방사형 경계 크로스페이드 ===
            // 중앙(반경 50m)은 황제국 평탄 지형, 바깥은 방향성 국가 지형.
            // 전환 구간 [EMPIRE_RADIUS - width, EMPIRE_RADIUS + width]에서 부드럽게 혼합.
            float empireT = Mathf.Clamp01((dist - (EMPIRE_RADIUS - TRANSITION_WIDTH)) / (2f * TRANSITION_WIDTH));
            if (empireT < 1f)
            {
                float empireH = ComputeNationHeight(x, z, NationType.Empire, seed);
                float dirH = ComputeNationHeight(x, z, GetDirectionalNation(angle), seed);
                h = Mathf.Lerp(empireH, dirH, empireT);
            }

            return h;
        }

        /// <summary>
        /// 단일 각도 경계에 대한 크로스페이드 헬퍼.
        /// 점의 경계 광선 수직 거리가 전환 폭 안에 들어오면 이웃 국가 높이와 Lerp.
        /// </summary>
        private static void BlendBoundary(
            ref float height, float x, float z, int seed,
            NationType negNation, NationType posNation,
            float ux, float uz, float halfWidth)
        {
            NationType nation = NationTerrainController.GetNationFromPosition(new Vector3(x, 0f, z));

            // 점이 해당 경계에 인접한 방향성 국가가 아니면 무시
            if (nation != negNation && nation != posNation)
                return;

            // 광선(u)에 대한 점 p=(x,z)의 수직(부호) 거리: cross = u.x * p.z - u.z * p.x
            float cross = ux * z - uz * x;

            // 블렌드 계수: cross가 [-width, width]일 때 0(음측)~1(양측)
            float t = Mathf.Clamp01(cross / (2f * halfWidth) + 0.5f);

            // 전환 구간 내에서만 실제 블렌딩 (바깥은 클램프로 무의미 → 생략)
            if (t > 0f && t < 1f)
            {
                float hNeg = ComputeNationHeight(x, z, negNation, seed);
                float hPos = ComputeNationHeight(x, z, posNation, seed);
                height = Mathf.Lerp(hNeg, hPos, t);
            }
        }
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

            if (size <= 0f)
            {
                Debug.LogError("[TerrainGenerator] Size must be > 0");
                size = 100f;
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

            float waterThreshold = def.waterThreshold;

            // === 1. FBM + 고원 높이맵 생성 ===
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

                    // 방위별 FBM + 고원 높이 (월드 좌표 사용 — 해상도 변화에 일관된 결과)
                    // waterThreshold 클램프는 하지 않음 (물 메시가 threshold 이하 삼각형 판별 담당)
                    float height = ComputeTerrainHeight(wx, wz, def.type, seed);

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

                Vector3 edge1 = v2 - v1;
                Vector3 edge2 = v3 - v1;
                Vector3 normal = Vector3.Cross(edge1, edge2);

                // Degenerate triangle 방어
                if (normal.sqrMagnitude > 0f)
                    normal.Normalize();

                calculatedNormals[i1] += normal;
                calculatedNormals[i2] += normal;
                calculatedNormals[i3] += normal;
            }

            // 노멀 정규화 (제로벡터 방어)
            for (int i = 0; i < vertexCount; i++)
            {
                Vector3 n = calculatedNormals[i];
                normals[i] = n.sqrMagnitude > 0f ? n.normalized : Vector3.up;
            }

            // === 4. 지형 메시 생성 ===
            Mesh terrainMesh = new Mesh();
            terrainMesh.indexFormat = vertexCount > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            terrainMesh.vertices = vertices;
            terrainMesh.triangles = triangles;
            terrainMesh.uv = uv;
            terrainMesh.normals = normals;
            terrainMesh.RecalculateBounds();
            terrainMesh.name = $"Terrain_{def.displayName}_{resolution}x{resolution}";

            // === 5. 물 메시 생성 (waterThreshold > 0) ===
            Mesh waterMesh = null;
            if (waterThreshold > 0f)
            {
                waterMesh = GenerateWaterMesh(vertices, triangles, resolution, step, halfSize, size, waterThreshold, def.waterColor);
            }

            return (terrainMesh, waterMesh);
        }

        /// <summary>
        /// 물 메시 생성 — waterThreshold 이하 영역만 물로 처리
        /// </summary>
        private static Mesh GenerateWaterMesh(
            Vector3[] terrainVertices, int[] terrainTriangles,
            int resolution, float step, float halfSize, float size,
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

            // 물 UV — 평면 투영
            Vector2[] waterUV = new Vector2[waterVerts.Count];
            for (int i = 0; i < waterVerts.Count; i++)
            {
                Vector3 wv = waterVerts[i];
                waterUV[i] = new Vector2(wv.x / size + 0.5f, wv.z / size + 0.5f);
            }
            waterMesh.uv = waterUV;

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
        /// <param name="pathCenter">진입로 중심 월드 좌표 (null이면 진입로 미생성)</param>
        /// <param name="pathWidth">진입로 폭 (월드 유닛, 기본 5m)</param>
        /// <param name="pathLength">진입로 길이 (월드 유닛, 기본 40m)</param>
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
            else if (mf.sharedMesh != null)
            {
                // 이전 메시 해제 (메모리 누수 방지)
                Object.Destroy(mf.sharedMesh);
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
                else if (waterMf.sharedMesh != null)
                    Object.Destroy(waterMf.sharedMesh);
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
                    Object.Destroy(existingWater.gameObject);
                }
            }
        }
    }
}