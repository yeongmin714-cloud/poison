using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C11-09: 교회 실내 인테리어 빌더.
    /// 방(14x5x10) + 대리석 바닥 + 회반죽 벽 + 제단 + 벤치 3줄 + 스테인드글라스.
    /// </summary>
    public static class ChurchInteriorBuilder
    {
        // ===== 레이아웃 상수 =====
        private const float ROOM_WIDTH = 14f;
        private const float ROOM_HEIGHT = 5f;
        private const float ROOM_DEPTH = 10f;
        private const float ALTAR_TABLE_WIDTH = 2.5f;
        private const float ALTAR_TABLE_HEIGHT = 1.2f;
        private const float ALTAR_TABLE_DEPTH = 1.5f;
        private const float ALTAR_OBJECT_SCALE = 0.3f;
        private const float ALTAR_OBJECT_HEIGHT = 0.4f;
        private const float BENCH_SPACING = 2.2f;
        private const float BENCH_X_OFFSET = 2.0f;
        private const float ALTAR_Z_OFFSET = 2.0f;
        private const float BENCH_Z_OFFSET = 4.0f;
        private const float STAINED_GLASS_WIDTH = 1.5f;
        private const float STAINED_GLASS_HEIGHT = 2.0f;
        private const float STAINED_GLASS_Y_FROM_TOP = 0.5f;
        private const float STAINED_GLASS_Z_OFFSET = 0.05f;
        private const float LIGHT_Y_RATIO = 0.7f;
        private const float LIGHT_Z_OFFSET = 2.5f;
        private const float NPC_Z_OFFSET = 3.5f;
        private const float EXIT_Z_OFFSET = 0.5f;
        private const float LIGHT_RANGE = 6f;
        private const float LIGHT_INTENSITY = 0.6f;
        private const float AMBIENT_INTENSITY = 0.5f;
        private static readonly Color AMBIENT_DARK = new Color(0.04f, 0.04f, 0.06f);
        private static readonly Color ALTAR_MAT_COLOR = new Color(0.80f, 0.75f, 0.65f);
        private static readonly Color BENCH_MAT_COLOR = new Color(0.55f, 0.40f, 0.25f);
        private static readonly Color ALTAR_OBJ_COLOR = new Color(0.95f, 0.85f, 0.60f);
        private static readonly Color LIGHT_CANDLE_COLOR = new Color(1f, 0.8f, 0.5f);
        private static readonly Color CEILING_COLOR = new Color(0.92f, 0.90f, 0.85f);

        /// <summary>
        /// 완성된 교회 실내 GameObject 반환.
        /// </summary>
        public static GameObject BuildChurchInterior()
        {
            // ===== 텍스처 생성 =====
            Texture2D floorTex = IndoorTextureGenerator.GenerateChurchFloor();
            Texture2D wallTex = IndoorTextureGenerator.GenerateChurchWall();
            Texture2D ceilingTex = IndoorTextureGenerator.GeneratePlasterWall(256, 256, CEILING_COLOR);

            // ===== 재질 생성 (URP Lit) =====
            Shader shader = ResolveShader("Universal Render Pipeline/Lit", "Standard");
            if (shader == null) return null;

            Material floorMat = CreateMaterial(shader, "Church_FloorMat", floorTex, Color.white);
            Material wallMat = CreateMaterial(shader, "Church_WallMat", wallTex, Color.white);
            Material ceilingMat = CreateMaterial(shader, "Church_CeilingMat", ceilingTex, Color.white);
            Material altarMat = CreateMaterial(shader, "Church_AltarMat", null, ALTAR_MAT_COLOR);
            Material benchMat = CreateMaterial(shader, "Church_BenchMat", null, BENCH_MAT_COLOR);

            // ===== 방 생성 =====
            GameObject room = IndoorBuilder.CreateRoom(ROOM_WIDTH, ROOM_HEIGHT, ROOM_DEPTH,
                floorMat, wallMat, ceilingMat);
            if (room == null)
            {
                Debug.LogError("[ChurchInteriorBuilder] IndoorBuilder.CreateRoom returned null!");
                return null;
            }

            // ===== 제단 (CreateTable + 위에 작은 Cube) =====
            CreateAltar(room, shader, altarMat);

            // ===== 벤치 3줄 (CreateChair 변형) =====
            CreateBenches(room, benchMat);

            // ===== 스테인드글라스 효과: 천장 가까이 벽에 반투명 컬러 Quad =====
            Shader unlitShader = ResolveShader("Universal Render Pipeline/Unlit", "Unlit/Texture");
            if (unlitShader != null)
            {
                float glassZ = -ROOM_DEPTH * 0.5f + STAINED_GLASS_Z_OFFSET;
                float glassY = ROOM_HEIGHT - STAINED_GLASS_Y_FROM_TOP;

                CreateStainedGlass(room, "StainedGlass_Red", STAINED_GLASS_WIDTH, STAINED_GLASS_HEIGHT,
                    new Vector3(-3.0f, glassY, glassZ),
                    new Color(1f, 0.2f, 0.2f, 0.5f), unlitShader);

                CreateStainedGlass(room, "StainedGlass_Blue", STAINED_GLASS_WIDTH, STAINED_GLASS_HEIGHT,
                    new Vector3(0, glassY, glassZ),
                    new Color(0.2f, 0.3f, 1f, 0.5f), unlitShader);

                CreateStainedGlass(room, "StainedGlass_Yellow", STAINED_GLASS_WIDTH, STAINED_GLASS_HEIGHT,
                    new Vector3(3.0f, glassY, glassZ),
                    new Color(1f, 1f, 0.2f, 0.5f), unlitShader);
            }

            // ===== 조명 설정 =====
            SetupLighting(room);

            // ===== 교회 NPC 생성 (E키로 기부 메뉴) =====
            CreateChurchNPC(room);

            // ===== 출구 트리거 생성 =====
            CreateExitTrigger(room);

            Debug.Log("[ChurchInteriorBuilder] 교회 실내 생성 완료!");

            return room;
        }

        private static Shader ResolveShader(string primary, string fallback)
        {
            Shader shader = Shader.Find(primary);
            if (shader == null)
            {
                Debug.LogWarning($"[ChurchInteriorBuilder] '{primary}' shader not found, falling back to '{fallback}'.");
                shader = Shader.Find(fallback);
            }
            if (shader == null)
            {
                Debug.LogError($"[ChurchInteriorBuilder] '{fallback}' shader also not found!");
            }
            return shader;
        }

        private static Material CreateMaterial(Shader shader, string name, Texture2D texture, Color color)
        {
            Material mat = new Material(shader) { name = name };
            if (texture != null) mat.mainTexture = texture;
            mat.color = color;
            return mat;
        }

        private static void CreateAltar(GameObject room, Shader shader, Material altarMat)
        {
            GameObject altarTable = IndoorFurniturePlacer.CreateTable(ALTAR_TABLE_WIDTH, ALTAR_TABLE_HEIGHT, ALTAR_TABLE_DEPTH, altarMat);
            if (altarTable == null) return;
            altarTable.transform.SetParent(room.transform);
            altarTable.transform.localPosition = new Vector3(0, 0, ROOM_DEPTH * 0.5f - ALTAR_Z_OFFSET);

            // 제단 위 작은 Cube (성스러운 물체) — Collider 불필요하므로 직접 생성
            GameObject altarObject = new GameObject("AltarObject");
            altarObject.transform.SetParent(altarTable.transform);
            altarObject.transform.localPosition = new Vector3(0, ALTAR_TABLE_HEIGHT, 0);
            altarObject.transform.localScale = new Vector3(ALTAR_OBJECT_SCALE, ALTAR_OBJECT_HEIGHT, ALTAR_OBJECT_SCALE);

            var filter = altarObject.AddComponent<MeshFilter>();
            filter.sharedMesh = GetCubeMesh();

            var renderer = altarObject.AddComponent<MeshRenderer>();
            Material altarObjMat = new Material(shader) { name = "Church_AltarObjMat" };
            altarObjMat.color = ALTAR_OBJ_COLOR;
            renderer.sharedMaterial = altarObjMat;
        }

        private static Mesh _cubeMesh;

        private static Mesh GetCubeMesh()
        {
            if (_cubeMesh != null) return _cubeMesh;
            _cubeMesh = new Mesh();
            _cubeMesh.name = "SharedCubeMesh";

            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f,  0.5f, -0.5f), new Vector3(-0.5f,  0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f,  0.5f), new Vector3(0.5f, -0.5f,  0.5f),
                new Vector3(0.5f,  0.5f,  0.5f), new Vector3(-0.5f,  0.5f,  0.5f)
            };
            int[] triangles = new int[]
            {
                0, 2, 1, 0, 3, 2, // Back
                4, 5, 6, 4, 6, 7, // Front
                0, 1, 5, 0, 5, 4, // Bottom
                2, 3, 7, 2, 7, 6, // Top
                0, 7, 3, 0, 4, 7, // Left
                1, 2, 6, 1, 6, 5  // Right
            };
            Vector2[] uv = new Vector2[]
            {
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1)
            };

            _cubeMesh.vertices = vertices;
            _cubeMesh.triangles = triangles;
            _cubeMesh.uv = uv;
            _cubeMesh.RecalculateNormals();
            _cubeMesh.UploadMeshData(true);

            return _cubeMesh;
        }

        private static void CreateBenches(GameObject room, Material benchMat)
        {
            for (int i = 0; i < 3; i++)
            {
                float zPos = ROOM_DEPTH * 0.5f - BENCH_Z_OFFSET - i * BENCH_SPACING;

                // 왼쪽 벤치
                GameObject benchLeft = IndoorFurniturePlacer.CreateChair(1.0f, benchMat);
                if (benchLeft == null) continue;
                benchLeft.name = $"Bench_Left_{i}";
                benchLeft.transform.SetParent(room.transform);
                benchLeft.transform.localPosition = new Vector3(-BENCH_X_OFFSET, 0, zPos);
                benchLeft.transform.localRotation = Quaternion.identity;

                // 오른쪽 벤치
                GameObject benchRight = IndoorFurniturePlacer.CreateChair(1.0f, benchMat);
                if (benchRight == null) continue;
                benchRight.name = $"Bench_Right_{i}";
                benchRight.transform.SetParent(room.transform);
                benchRight.transform.localPosition = new Vector3(BENCH_X_OFFSET, 0, zPos);
                benchRight.transform.localRotation = Quaternion.identity;
            }
        }

        private static void SetupLighting(GameObject room)
        {
            // 어두운 앰비언트 + 천장 중앙 Point Light + 깜빡임(양초)
            IndoorLighting.SetupIndoorLighting(room, AMBIENT_DARK, AMBIENT_INTENSITY, true);

            float lightY = ROOM_HEIGHT * LIGHT_Y_RATIO;
            float lightZ = ROOM_DEPTH * 0.5f - LIGHT_Z_OFFSET;

            // 제단 왼쪽 Point Light
            IndoorLighting.AddPointLight(room,
                new Vector3(-BENCH_X_OFFSET, lightY, lightZ),
                LIGHT_CANDLE_COLOR, LIGHT_RANGE, LIGHT_INTENSITY);

            // 제단 오른쪽 Point Light
            IndoorLighting.AddPointLight(room,
                new Vector3(BENCH_X_OFFSET, lightY, lightZ),
                LIGHT_CANDLE_COLOR, LIGHT_RANGE, LIGHT_INTENSITY);
        }

        private static void CreateChurchNPC(GameObject room)
        {
            GameObject churchNpc = new GameObject("ChurchNPC");
            churchNpc.transform.SetParent(room.transform);
            churchNpc.transform.localPosition = new Vector3(0, 0, ROOM_DEPTH * 0.5f - NPC_Z_OFFSET);
            churchNpc.AddComponent<ChurchNPCInteraction>();
        }

        private static void CreateExitTrigger(GameObject room)
        {
            GameObject exitTrigger = new GameObject("ExitTrigger");
            exitTrigger.transform.SetParent(room.transform);
            exitTrigger.transform.localPosition = new Vector3(0, 0, ROOM_DEPTH * 0.5f - EXIT_Z_OFFSET);
            var exitBt = exitTrigger.AddComponent<BuildingTrigger>();
            exitBt.BuildingType = "Exit";
            exitBt.InteractRange = 3f;
        }

        /// <summary>
        /// 반투명 컬러 스테인드글라스 Quad 생성.
        /// 뒷벽(Z-) 방향을 보도록 Y축 180도 회전 적용.
        /// </summary>
        private static void CreateStainedGlass(GameObject parent, string name, float width, float height,
            Vector3 position, Color color, Shader shader)
        {
            GameObject glass = new GameObject(name);
            glass.transform.SetParent(parent.transform);
            glass.transform.localPosition = position;
            // 뒷벽(Z-) 방향을 향하도록 180도 회전 (Quad 기본 앞면: Z+)
            glass.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            Mesh mesh = new Mesh();
            mesh.name = $"{name}_Mesh";

            float hw = width * 0.5f;
            float hh = height * 0.5f;
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-hw, -hh, 0),
                new Vector3(hw, -hh, 0),
                new Vector3(-hw, hh, 0),
                new Vector3(hw, hh, 0)
            };

            Vector2[] uv = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1)
            };

            int[] triangles = new int[] { 0, 2, 1, 2, 3, 1 };

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.UploadMeshData(true); // 읽기 전용 GPU 업로드 (CPU 데이터 해제)

            var filter = glass.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var renderer = glass.AddComponent<MeshRenderer>();
            Material mat = new Material(shader) { name = $"{name}_Mat" };
            mat.color = color;
            renderer.sharedMaterial = mat;
        }
    }
}
