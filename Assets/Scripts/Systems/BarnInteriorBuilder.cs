using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// T-Cycle-02: 헛간 실내 절차적 생성 빌더.
    /// IndoorSceneTransition.EnterBuilding("barn") 에서 호출.
    /// 나무 벽/바닥, 어두운 조명, 짚더미, 문 오브젝트.
    /// </summary>
    public static class BarnInteriorBuilder
    {
        private const float ROOM_WIDTH = 8f;
        private const float ROOM_DEPTH = 6f;
        private const float ROOM_HEIGHT = 3f;

        private static GameObject _root;

        public static void BuildBarnInterior()
        {
            // 중복 방지
            ClearExisting();

            _root = new GameObject("[BarnInterior]");
            _root.transform.position = Vector3.zero;

            BuildWalls();
            BuildFloor();
            BuildCeiling();
            BuildDoor();
            BuildHayBales();
            BuildShelf();
            SetupLighting();

            Debug.Log("[BarnInteriorBuilder] 헛간 실내 생성 완료!");
        }

        private static void ClearExisting()
        {
            var existing = GameObject.Find("[BarnInterior]");
            if (existing != null)
                Object.DestroyImmediate(existing);
        }

        private static void BuildWalls()
        {
            // 뒷벽 (-Z)
            CreateWall(new Vector3(0, ROOM_HEIGHT / 2, -ROOM_DEPTH / 2),
                       new Vector3(ROOM_WIDTH, ROOM_HEIGHT, 0.1f),
                       new Color(0.45f, 0.25f, 0.1f)); // wood brown

            // 좌벽 (-X)
            CreateWall(new Vector3(-ROOM_WIDTH / 2, ROOM_HEIGHT / 2, 0),
                       new Vector3(0.1f, ROOM_HEIGHT, ROOM_DEPTH),
                       new Color(0.45f, 0.25f, 0.1f));

            // 우벽 (+X)
            CreateWall(new Vector3(ROOM_WIDTH / 2, ROOM_HEIGHT / 2, 0),
                       new Vector3(0.1f, ROOM_HEIGHT, ROOM_DEPTH),
                       new Color(0.45f, 0.25f, 0.1f));
        }

        private static void BuildFloor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(_root.transform);
            floor.transform.localPosition = new Vector3(0, -0.05f, 0);
            floor.transform.localScale = new Vector3(ROOM_WIDTH / 10f, 1, ROOM_DEPTH / 10f);

            var renderer = floor.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.6f, 0.4f, 0.2f); // light wood
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.2f);
            renderer.material = mat;
        }

        private static void BuildCeiling()
        {
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ceiling.name = "Ceiling";
            ceiling.transform.SetParent(_root.transform);
            ceiling.transform.localPosition = new Vector3(0, ROOM_HEIGHT, 0);
            ceiling.transform.localScale = new Vector3(ROOM_WIDTH, ROOM_DEPTH, 1);
            ceiling.transform.localRotation = Quaternion.Euler(90, 0, 0);

            var renderer = ceiling.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.3f, 0.2f, 0.1f);
            renderer.material = mat;
        }

        private static void BuildDoor()
        {
            // 문틀
            var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "DoorFrame";
            frame.transform.SetParent(_root.transform);
            frame.transform.localPosition = new Vector3(0, 1.5f, ROOM_DEPTH / 2);
            frame.transform.localScale = new Vector3(1.5f, 2.5f, 0.1f);
            var frameRend = frame.GetComponent<MeshRenderer>();
            frameRend.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            frameRend.material.color = new Color(0.35f, 0.2f, 0.08f);

            // 문짝
            var door = GameObject.CreatePrimitive(PrimitiveType.Quad);
            door.name = "Door";
            door.transform.SetParent(_root.transform);
            door.transform.localPosition = new Vector3(0, 1.3f, ROOM_DEPTH / 2 + 0.06f);
            door.transform.localScale = new Vector3(1.3f, 2.3f, 1);
            var doorRend = door.GetComponent<MeshRenderer>();
            doorRend.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            doorRend.material.color = new Color(0.4f, 0.25f, 0.1f);

            // BuildingTrigger 추가 (E키 상호작용)
            var trigger = door.AddComponent<BuildingTrigger>();
            trigger.BuildingType = "Exit";
        }

        private static void BuildHayBales()
        {
            // 짚더미 2개
            for (int i = 0; i < 2; i++)
            {
                var hay = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                hay.name = $"HayBale_{i}";
                hay.transform.SetParent(_root.transform);
                hay.transform.localPosition = new Vector3(-2f + i * 4f, 0.3f, -1.5f);
                hay.transform.localScale = new Vector3(0.6f, 0.3f, 0.6f);
                hay.transform.localRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

                var rend = hay.GetComponent<MeshRenderer>();
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.85f, 0.75f, 0.3f); // straw yellow
                rend.material = mat;
            }
        }

        private static void BuildShelf()
        {
            var shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shelf.name = "Shelf";
            shelf.transform.SetParent(_root.transform);
            shelf.transform.localPosition = new Vector3(-3f, 1.2f, 1.5f);
            shelf.transform.localScale = new Vector3(1.5f, 0.8f, 0.3f);

            var rend = shelf.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.4f, 0.25f, 0.12f);
            rend.material = mat;
        }

        private static void CreateWall(Vector3 position, Vector3 scale, Color color)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.SetParent(_root.transform);
            wall.transform.localPosition = position;
            wall.transform.localScale = scale;

            // Wood plank 텍스처 느낌의 단색
            var rend = wall.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color;
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.1f);
            rend.material = mat;
        }

        private static void SetupLighting()
        {
            // 메인 라이트 (dim)
            var dirLight = new GameObject("Barn_DirectionalLight");
            dirLight.transform.SetParent(_root.transform);
            var light = dirLight.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.6f, 0.5f, 0.4f);
            light.intensity = 0.3f;
            dirLight.transform.rotation = Quaternion.Euler(50, -30, 0);

            // 촛불 분위기 포인트 라이트 2개
            for (int i = 0; i < 2; i++)
            {
                var ptLight = new GameObject($"Barn_PointLight_{i}");
                ptLight.transform.SetParent(_root.transform);
                var pl = ptLight.AddComponent<Light>();
                pl.type = LightType.Point;
                pl.color = new Color(1f, 0.6f, 0.2f); // orange
                pl.intensity = 0.8f;
                pl.range = 4f;
                ptLight.transform.localPosition = new Vector3(-2f + i * 4f, 2.5f, 0f);
            }
        }

        /// <summary>
        /// 디버그용: 생성된 헛간 제거
        /// </summary>
        public static void ClearBarn()
        {
            ClearExisting();
        }
    }
}