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
        /// <summary>
        /// 완성된 교회 실내 GameObject 반환.
        /// </summary>
        public static GameObject BuildChurchInterior()
        {
            const float roomWidth = 14f;
            const float roomHeight = 5f;
            const float roomDepth = 10f;

            // ===== 텍스처 생성 =====
            Texture2D floorTex = IndoorTextureGenerator.GenerateChurchFloor();
            Texture2D wallTex = IndoorTextureGenerator.GenerateChurchWall();
            Texture2D ceilingTex = IndoorTextureGenerator.GeneratePlasterWall(256, 256,
                new Color(0.92f, 0.90f, 0.85f));

            // ===== 재질 생성 (URP Lit) =====
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[ChurchInteriorBuilder] URP Lit shader not found!");
                shader = Shader.Find("Standard");
            }

            Material floorMat = new Material(shader) { name = "Church_FloorMat" };
            floorMat.mainTexture = floorTex;
            floorMat.color = Color.white;

            Material wallMat = new Material(shader) { name = "Church_WallMat" };
            wallMat.mainTexture = wallTex;
            wallMat.color = Color.white;

            Material ceilingMat = new Material(shader) { name = "Church_CeilingMat" };
            ceilingMat.mainTexture = ceilingTex;
            ceilingMat.color = Color.white;

            Material altarMat = new Material(shader) { name = "Church_AltarMat" };
            altarMat.color = new Color(0.80f, 0.75f, 0.65f); // 밝은 석재

            Material benchMat = new Material(shader) { name = "Church_BenchMat" };
            benchMat.color = new Color(0.55f, 0.40f, 0.25f); // 나무색

            // ===== 방 생성 =====
            GameObject room = IndoorBuilder.CreateRoom(roomWidth, roomHeight, roomDepth,
                floorMat, wallMat, ceilingMat);

            // ===== 제단 (CreateTable + 위에 작은 Cube) =====
            GameObject altarTable = IndoorFurniturePlacer.CreateTable(2.5f, 1.2f, 1.5f, altarMat);
            altarTable.transform.SetParent(room.transform);
            altarTable.transform.localPosition = new Vector3(0, 0, roomDepth * 0.5f - 2.0f);

            // 제단 위 작은 Cube (성스러운 물체)
            GameObject altarObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            altarObject.name = "AltarObject";
            altarObject.transform.SetParent(altarTable.transform);
            altarObject.transform.localPosition = new Vector3(0, 1.2f, 0);
            altarObject.transform.localScale = new Vector3(0.3f, 0.4f, 0.3f);
            var altarRenderer = altarObject.GetComponent<MeshRenderer>();
            if (altarRenderer != null)
            {
                Material altarObjMat = new Material(shader) { name = "Church_AltarObjMat" };
                altarObjMat.color = new Color(0.95f, 0.85f, 0.60f); // 황금빛
                altarRenderer.sharedMaterial = altarObjMat;
            }

            // ===== 벤치 3줄 (CreateChair 변형) =====
            float benchSpacing = 2.2f;
            for (int i = 0; i < 3; i++)
            {
                // 왼쪽 벤치
                GameObject benchLeft = IndoorFurniturePlacer.CreateChair(1.0f, benchMat);
                benchLeft.name = $"Bench_Left_{i}";
                benchLeft.transform.SetParent(room.transform);
                benchLeft.transform.localPosition = new Vector3(-2.0f, 0, roomDepth * 0.5f - 4.0f - i * benchSpacing);
                benchLeft.transform.localRotation = Quaternion.identity;

                // 오른쪽 벤치
                GameObject benchRight = IndoorFurniturePlacer.CreateChair(1.0f, benchMat);
                benchRight.name = $"Bench_Right_{i}";
                benchRight.transform.SetParent(room.transform);
                benchRight.transform.localPosition = new Vector3(2.0f, 0, roomDepth * 0.5f - 4.0f - i * benchSpacing);
                benchRight.transform.localRotation = Quaternion.identity;
            }

            // ===== 스테인드글라스 효과: 천장 가까이 벽에 반투명 컬러 Quad =====
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null)
            {
                unlitShader = Shader.Find("Unlit/Texture");
            }

            // 빨강 스테인드글라스 (뒷벽)
            CreateStainedGlass(room, "StainedGlass_Red", 1.5f, 2.0f,
                new Vector3(-3.0f, roomHeight - 0.5f, -roomDepth * 0.5f + 0.05f),
                new Color(1f, 0.2f, 0.2f, 0.5f), unlitShader);

            // 파랑 스테인드글라스 (뒷벽)
            CreateStainedGlass(room, "StainedGlass_Blue", 1.5f, 2.0f,
                new Vector3(0, roomHeight - 0.5f, -roomDepth * 0.5f + 0.05f),
                new Color(0.2f, 0.3f, 1f, 0.5f), unlitShader);

            // 노랑 스테인드글라스 (뒷벽)
            CreateStainedGlass(room, "StainedGlass_Yellow", 1.5f, 2.0f,
                new Vector3(3.0f, roomHeight - 0.5f, -roomDepth * 0.5f + 0.05f),
                new Color(1f, 1f, 0.2f, 0.5f), unlitShader);

            // ===== 조명 설정 =====
            // 어두운 앰비언트 + 제단 양옆 Point Light 2개 + 깜빡임(양초)
            Color ambientDark = new Color(0.04f, 0.04f, 0.06f);
            IndoorLighting.SetupIndoorLighting(room, ambientDark, 0.5f, true);

            // 제단 왼쪽 Point Light
            IndoorLighting.AddPointLight(room,
                new Vector3(-2.0f, roomHeight * 0.7f, roomDepth * 0.5f - 2.5f),
                new Color(1f, 0.8f, 0.5f), 6f, 0.6f);

            // 제단 오른쪽 Point Light
            IndoorLighting.AddPointLight(room,
                new Vector3(2.0f, roomHeight * 0.7f, roomDepth * 0.5f - 2.5f),
                new Color(1f, 0.8f, 0.5f), 6f, 0.6f);

            Debug.Log("[ChurchInteriorBuilder] 교회 실내 생성 완료!");

            // ===== FIX-01: 교회 NPC 생성 (E키로 기부 메뉴) =====
            GameObject churchNpc = new GameObject("ChurchNPC");
            churchNpc.transform.SetParent(room.transform);
            // 벤치 근처 (제단 쪽)
            churchNpc.transform.localPosition = new Vector3(0, 0, roomDepth * 0.5f - 3.5f);
            churchNpc.AddComponent<ChurchNPCInteraction>();

            // ===== FIX-01: 출구 트리거 생성 =====
            GameObject exitTrigger = new GameObject("ExitTrigger");
            exitTrigger.transform.SetParent(room.transform);
            exitTrigger.transform.localPosition = new Vector3(0, 0, roomDepth * 0.5f - 0.5f);
            var exitBt = exitTrigger.AddComponent<BuildingTrigger>();
            exitBt.BuildingType = "Exit";
            exitBt.InteractRange = 3f;

            return room;
        }

        /// <summary>
        /// 반투명 컬러 스테인드글라스 Quad 생성.
        /// </summary>
        private static void CreateStainedGlass(GameObject parent, string name, float width, float height,
            Vector3 position, Color color, Shader shader)
        {
            GameObject glass = new GameObject(name);
            glass.transform.SetParent(parent.transform);
            glass.transform.localPosition = position;
            glass.transform.localRotation = Quaternion.identity;

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

            var filter = glass.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var renderer = glass.AddComponent<MeshRenderer>();
            Material mat = new Material(shader) { name = $"{name}_Mat" };
            mat.color = color;
            renderer.sharedMaterial = mat;
        }
    }
}
