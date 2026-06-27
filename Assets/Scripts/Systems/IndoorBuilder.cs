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
