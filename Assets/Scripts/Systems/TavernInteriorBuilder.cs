using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// P25-01: 선술집 실내 생성기.
    /// 카운터, 테이블, 무대, 어두운 분위기 조명을 배치합니다.
    /// </summary>
    public static class TavernInteriorBuilder
    {
        private const string ROOM_NAME = "TavernRoom";
        private const float WALL_THICKNESS = 0.1f;

        /// <summary>
        /// 선술집 내부를 생성합니다.
        /// </summary>
        /// <param name="territoryId">영지 ID (랜덤 시드용, 기본값 "default")</param>
        /// <param name="tier">영지 난이도 1~5 (방 크기 결정, 기본값 1)</param>
        public static GameObject BuildTavernInterior(string territoryId = "default", int tier = 1)
        {
            tier = InteriorRandomizer.ClampTier(tier);
            var size = InteriorRandomizer.GetRoomSize(tier);
            float width = size.width;
            float height = size.height;
            float depth = size.depth;

            var rng = InteriorRandomizer.CreateRandom(territoryId);

            GameObject room = new GameObject(ROOM_NAME);
            room.transform.position = Vector3.zero;

            // 바닥
            CreateFloor(room, width, depth);

            // 벽
            CreateWalls(room, width, height, depth);

            // 천장
            CreateCeiling(room, width, height, depth);

            // 카운터 (뒷벽 기준)
            CreateCounter(room, width, depth);

            // 테이블 개수: 2~4개 랜덤
            int tableCount = rng.Next(2, 5);
            for (int i = 0; i < tableCount; i++)
            {
                float tblX = (float)(rng.NextDouble() * (width * 0.6f) - width * 0.3f);
                float tblZ = (float)(rng.NextDouble() * (depth * 0.4f) - depth * 0.1f);
                CreateTable(room, tblX, tblZ, i);
            }

            // 의자 (랜덤 위치, 회전 다양화)
            int chairCount = rng.Next(4, 9);
            for (int i = 0; i < chairCount; i++)
            {
                float angle = (float)(rng.NextDouble() * 360f);
                float radius = 0.8f + (float)(rng.NextDouble() * 0.6f);
                float cx = (float)(rng.NextDouble() * (width * 0.4f) - width * 0.2f);
                float cz = (float)(rng.NextDouble() * (depth * 0.3f));
                CreateChair(room, cx, cz, angle, i);
            }

            // 무대 (앞쪽)
            CreateStage(room, width, depth);

            // 어두운 조명 — 노란빛 PointLight, 낮은 강도
            IndoorLighting.SetupIndoorLighting(room, new Color(0.08f, 0.06f, 0.04f), 0.4f, false);

            // 추가 노란빛 포인트 라이트 (무대 위)
            CreateStageLight(room, width, depth);

            return room;
        }

        private static void CreateFloor(GameObject room, float width, float depth)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(room.transform);
            floor.transform.localPosition = new Vector3(0, -0.05f, 0);
            floor.transform.localScale = new Vector3(width, 0.1f, depth);

            var renderer = floor.GetComponent<MeshRenderer>();
            var mat = MaterialHelper.CreateLitMaterial(new Color(0.30f, 0.18f, 0.08f), "TavernFloorMat");
            if (mat != null)
                renderer.material = mat;
        }

        private static void CreateWalls(GameObject room, float width, float height, float depth)
        {
            CreateWallPiece(room, "Wall_Front", new Vector3(0, height / 2f, -depth / 2f), new Vector3(width, height, WALL_THICKNESS));
            CreateWallPiece(room, "Wall_Back", new Vector3(0, height / 2f, depth / 2f), new Vector3(width, height, WALL_THICKNESS));
            CreateWallPiece(room, "Wall_Left", new Vector3(-width / 2f, height / 2f, 0), new Vector3(WALL_THICKNESS, height, depth));
            CreateWallPiece(room, "Wall_Right", new Vector3(width / 2f, height / 2f, 0), new Vector3(WALL_THICKNESS, height, depth));
        }

        private static GameObject CreateWallPiece(GameObject room, string name, Vector3 position, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(room.transform);
            wall.transform.localPosition = position;
            wall.transform.localScale = scale;

            var renderer = wall.GetComponent<MeshRenderer>();
            var mat = MaterialHelper.CreateLitMaterial(new Color(0.50f, 0.35f, 0.20f), "TavernWallMat");
            if (mat != null)
                renderer.material = mat;

            return wall;
        }

        private static void CreateCeiling(GameObject room, float width, float height, float depth)
        {
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = "Ceiling";
            ceiling.transform.SetParent(room.transform);
            ceiling.transform.localPosition = new Vector3(0, height - 0.05f, 0);
            ceiling.transform.localScale = new Vector3(width, 0.1f, depth);

            var renderer = ceiling.GetComponent<MeshRenderer>();
            var mat = MaterialHelper.CreateLitMaterial(new Color(0.35f, 0.25f, 0.15f), "TavernCeilingMat");
            if (mat != null)
                renderer.material = mat;
        }

        private static void CreateCounter(GameObject room, float width, float depth)
        {
            float counterW = 2.5f;
            float counterH = 1.0f;
            float counterD = 0.6f;

            var counter = GameObject.CreatePrimitive(PrimitiveType.Cube);
            counter.name = "Counter";
            counter.transform.SetParent(room.transform);
            counter.transform.localPosition = new Vector3(0, counterH / 2f, depth * 0.4f);
            counter.transform.localScale = new Vector3(counterW, counterH, counterD);

            var renderer = counter.GetComponent<MeshRenderer>();
            var mat = MaterialHelper.CreateLitMaterial(new Color(0.40f, 0.25f, 0.10f), "TavernCounterMat");
            if (mat != null)
                renderer.material = mat;

            // 카운터 위 작은 선반
            var shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shelf.name = "CounterShelf";
            shelf.transform.SetParent(room.transform);
            shelf.transform.localPosition = new Vector3(0, counterH + 0.3f, depth * 0.42f);
            shelf.transform.localScale = new Vector3(counterW * 0.8f, 0.1f, counterD * 0.5f);

            var shelfRenderer = shelf.GetComponent<MeshRenderer>();
            var shelfMat = MaterialHelper.CreateLitMaterial(new Color(0.50f, 0.30f, 0.10f), "TavernShelfMat");
            if (shelfMat != null)
                shelfRenderer.material = shelfMat;
        }

        private static void CreateTable(GameObject room, float offsetX, float offsetZ, int index)
        {
            float tableW = 0.8f;
            float tableH = 0.7f;
            float tableD = 0.8f;

            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = $"Table_{index}";
            table.transform.SetParent(room.transform);
            table.transform.localPosition = new Vector3(offsetX, tableH / 2f, offsetZ);
            table.transform.localScale = new Vector3(tableW, tableH, tableD);

            var renderer = table.GetComponent<MeshRenderer>();
            var mat = MaterialHelper.CreateLitMaterial(new Color(0.45f, 0.28f, 0.12f), $"TavernTableMat_{index}");
            if (mat != null)
                renderer.material = mat;
        }

        private static void CreateChair(GameObject room, float centerX, float centerZ, float angle, int index)
        {
            float chairSize = 0.35f;

            float rad = angle * Mathf.Deg2Rad;
            float cx = centerX + Mathf.Cos(rad) * 0.7f;
            float cz = centerZ + Mathf.Sin(rad) * 0.7f;

            var chair = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chair.name = $"Chair_{index}";
            chair.transform.SetParent(room.transform);
            chair.transform.localPosition = new Vector3(cx, chairSize / 2f, cz);
            chair.transform.localScale = new Vector3(chairSize, chairSize, chairSize);
            chair.transform.localRotation = Quaternion.Euler(0, angle, 0);

            var renderer = chair.GetComponent<MeshRenderer>();
            var mat = MaterialHelper.CreateLitMaterial(new Color(0.40f, 0.22f, 0.08f), $"TavernChairMat_{index}");
            if (mat != null)
                renderer.material = mat;
        }

        private static void CreateStage(GameObject room, float width, float depth)
        {
            float stageW = 2.0f;
            float stageH = 0.2f;
            float stageD = 1.5f;

            var stage = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stage.name = "Stage";
            stage.transform.SetParent(room.transform);
            stage.transform.localPosition = new Vector3(0, stageH / 2f, -depth * 0.35f);
            stage.transform.localScale = new Vector3(stageW, stageH, stageD);

            var renderer = stage.GetComponent<MeshRenderer>();
            var mat = MaterialHelper.CreateLitMaterial(new Color(0.60f, 0.35f, 0.10f), "TavernStageMat");
            if (mat != null)
                renderer.material = mat;
        }

        private static void CreateStageLight(GameObject room, float width, float depth)
        {
            IndoorLighting.AddPointLight(
                room,
                new Vector3(0, 2.5f, -depth * 0.35f),
                new Color(1f, 0.85f, 0.5f), // 따뜻한 노란빛
                8f,
                0.8f
            );
        }
    }
}
