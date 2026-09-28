using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// C11-01: 실내 방 Procedural Mesh 생성기.
    /// 바닥, 벽 4면, 천장을 각각 별도 GameObject로 생성.
    /// 추후 개별 면 교체 가능.
    /// </summary>
    public static class IndoorBuilder
    {
        /// <summary>
        /// 방(Room) 생성 — 바닥, 벽 4면, 천장을 Quad Mesh로 구성.
        /// </summary>
        /// <param name="width">X축 폭</param>
        /// <param name="height">Y축 높이</param>
        /// <param name="depth">Z축 깊이</param>
        /// <param name="floorMat">바닥 재질</param>
        /// <param name="wallMat">벽 재질</param>
        /// <param name="ceilingMat">천장 재질</param>
        /// <returns>"Room" GameObject에 모든 면이 자식으로 포함</returns>
        public static GameObject CreateRoom(float width, float height, float depth,
            Material floorMat, Material wallMat, Material ceilingMat)
        {
            GameObject room = new GameObject("Room");

            // 바닥 (XZ 평면, y=0)
            CreateQuad(room, "Floor", width, depth,
                new Vector3(0, 0, 0), Quaternion.Euler(90, 0, 0),
                floorMat);

            // 벽 4면
            // 앞벽 (Z+ 방향)
            CreateQuad(room, "Wall_Front", width, height,
                new Vector3(0, height * 0.5f, depth * 0.5f), Quaternion.identity,
                wallMat);

            // 뒷벽 (Z- 방향)
            CreateQuad(room, "Wall_Back", width, height,
                new Vector3(0, height * 0.5f, -depth * 0.5f), Quaternion.Euler(0, 180, 0),
                wallMat);

            // 왼쪽 벽 (X- 방향)
            CreateQuad(room, "Wall_Left", depth, height,
                new Vector3(-width * 0.5f, height * 0.5f, 0), Quaternion.Euler(0, -90, 0),
                wallMat);

            // 오른쪽 벽 (X+ 방향)
            CreateQuad(room, "Wall_Right", depth, height,
                new Vector3(width * 0.5f, height * 0.5f, 0), Quaternion.Euler(0, 90, 0),
                wallMat);

            // 천장 (XZ 평면, y=height)
            CreateQuad(room, "Ceiling", width, depth,
                new Vector3(0, height, 0), Quaternion.Euler(-90, 0, 0),
                ceilingMat);

            return room;
        }

        /// <summary>
        /// Interior partition wall(실내 내부 칸막이 벽) — 방을 나누는 벽. 출입구(Doorway) 갭 1개 포함.
        /// 벽은 두 겹(양면) Quad + MeshCollider, 중간에 문으로 통과할 수 있는 갭(아무것도 없음).
        /// </summary>
        /// <param name="parent">부모 (Room transform)</param>
        /// <param name="name">벽 GameObject 이름</param>
        /// <param name="length">벽 길이 (X축, meters)</param>
        /// <param name="height">벽 높이 (Y축, meters) — 실내 CreateRoom height와 동일 전달</param>
        /// <param name="thickness">벽 두께 (Z축, meters) — 보통 0.3~0.4</param>
        /// <param name="center">벽 중심 위치 (XZ 평면, y는 자동으로 height*0.5)</param>
        /// <param name="rotationY">벽 Y축 회전 (세로벽=90, 가로벽=0)</param>
        /// <param name="doorCenterZ">문 갭 중심의 로컬 Z (벽 길이 방향 좌표, 중심 0 기준)</param>
        /// <param name="doorWidth">문 갭 폭 (meters, 보통 2.0)</param>
        /// <param name="material">벽 재질</param>
        public static void CreateInteriorWall(GameObject parent, string name, float length, float height,
            float thickness, Vector3 center, float rotationY, float doorCenterZ, float doorWidth, Material material)
        {
            GameObject wall = new GameObject(name);
            wall.transform.SetParent(parent.transform);
            wall.transform.localPosition = new Vector3(center.x, height * 0.5f, center.z);
            wall.transform.localRotation = Quaternion.Euler(0, rotationY, 0);

            float leftFrom = -length * 0.5f;
            float leftTo = doorCenterZ - doorWidth * 0.5f;
            float rightFrom = doorCenterZ + doorWidth * 0.5f;
            float rightTo = length * 0.5f;

            AddWallSegment(wall, leftFrom, leftTo, height, thickness * 0.5f, 0, material);
            AddWallSegment(wall, rightFrom, rightTo, height, thickness * 0.5f, 0, material);
            AddWallSegment(wall, leftFrom, leftTo, height, -thickness * 0.5f, 180, material);
            AddWallSegment(wall, rightFrom, rightTo, height, -thickness * 0.5f, 180, material);
        }

        private static void AddWallSegment(GameObject wall, float fromX, float toX, float height,
            float zOff, float rotationY, Material material)
        {
            float len = toX - fromX;
            if (len <= 0.01f) return;

            float midX = (fromX + toX) * 0.5f;
            string face = Mathf.Approximately(rotationY, 180f) ? "Back" : "Front";
            string segmentName = $"Seg_{face}_{fromX:R}_{toX:R}";
            CreateQuad(wall, segmentName, len, height, new Vector3(midX, height * 0.5f, zOff),
                Quaternion.Euler(0, rotationY, 0), material);
        }

        /// <summary>
        /// 단일 Quad Mesh를 가진 GameObject 생성.
        /// </summary>
        private static void CreateQuad(GameObject parent, string name, float quadWidth, float quadHeight,
            Vector3 position, Quaternion rotation, Material material)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;

            // Mesh 생성
            Mesh mesh = new Mesh();
            mesh.name = $"{name}_Mesh";

            // 4 vertices
            float hw = quadWidth * 0.5f;
            float hh = quadHeight * 0.5f;
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-hw, -hh, 0),
                new Vector3( hw, -hh, 0),
                new Vector3(-hw,  hh, 0),
                new Vector3( hw,  hh, 0)
            };

            // UV: 0,0 ~ 1,1
            Vector2[] uv = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1)
            };

            // 2 triangles (Quad)
            int[] triangles = new int[]
            {
                0, 2, 1,
                2, 3, 1
            };

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();

            // MeshFilter + MeshRenderer
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            // Collider
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }
    }
}
