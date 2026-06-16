using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C11-12: 성 내부 씬 전환.
    /// 방(20x6x15) 대형 복도 + 왕좌 공간 + 기둥 2열.
    /// C11-13: 국가별 텍스처 적용.
    /// </summary>
    public static class CastleInteriorBuilder
    {
        /// <summary>
        /// 국가 스타일에 맞는 성 내부 생성.
        /// </summary>
        /// <param name="nationStyle">
        /// "Eastern" (동부), "Western" (서부), "Southern" (남부),
        /// "Northern" (북부), "Empire" (황제국)
        /// </param>
        public static GameObject BuildCastleInterior(string nationStyle)
        {
            const float roomWidth = 20f;
            const float roomHeight = 6f;
            const float roomDepth = 15f;

            // ===== 국가별 텍스처 생성 =====
            Texture2D floorTex;
            Texture2D wallTex;

            switch (nationStyle?.ToLower())
            {
                case "eastern":
                    floorTex = IndoorTextureGenerator.GenerateCastleFloorEastern();
                    wallTex = IndoorTextureGenerator.GenerateCastleWallEastern();
                    break;
                case "western":
                    floorTex = IndoorTextureGenerator.GenerateCastleFloorWestern();
                    wallTex = IndoorTextureGenerator.GenerateCastleWallWestern();
                    break;
                case "southern":
                    floorTex = IndoorTextureGenerator.GenerateCastleFloorSouthern();
                    wallTex = IndoorTextureGenerator.GenerateCastleWallSouthern();
                    break;
                case "northern":
                    floorTex = IndoorTextureGenerator.GenerateCastleFloorNorthern();
                    wallTex = IndoorTextureGenerator.GenerateCastleWallNorthern();
                    break;
                case "empire":
                    floorTex = IndoorTextureGenerator.GenerateCastleFloorEmpire();
                    wallTex = IndoorTextureGenerator.GenerateCastleWallEmpire();
                    break;
                default:
                    Debug.LogWarning($"[CastleInteriorBuilder] 알 수 없는 국가 스타일: '{nationStyle}'. 기본(Empire) 사용.");
                    floorTex = IndoorTextureGenerator.GenerateCastleFloorEmpire();
                    wallTex = IndoorTextureGenerator.GenerateCastleWallEmpire();
                    break;
            }

            Texture2D ceilingTex = IndoorTextureGenerator.GeneratePlasterWall(256, 256,
                new Color(0.60f, 0.55f, 0.50f));

            // ===== 재질 생성 (URP Lit) =====
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[CastleInteriorBuilder] URP Lit shader not found!");
                shader = Shader.Find("Standard");
            }

            Material floorMat = new Material(shader) { name = $"Castle_FloorMat_{nationStyle}" };
            floorMat.mainTexture = floorTex;
            floorMat.color = Color.white;

            Material wallMat = new Material(shader) { name = $"Castle_WallMat_{nationStyle}" };
            wallMat.mainTexture = wallTex;
            wallMat.color = Color.white;

            Material ceilingMat = new Material(shader) { name = "Castle_CeilingMat" };
            ceilingMat.mainTexture = ceilingTex;
            ceilingMat.color = Color.white;

            Material pillarMat = new Material(shader) { name = "Castle_PillarMat" };
            pillarMat.color = new Color(0.40f, 0.38f, 0.35f); // 석재 기둥

            Material throneMat = new Material(shader) { name = "Castle_ThroneMat" };
            throneMat.color = new Color(0.55f, 0.40f, 0.20f); // 나무 왕좌

            // ===== 방 생성 =====
            GameObject room = IndoorBuilder.CreateRoom(roomWidth, roomHeight, roomDepth,
                floorMat, wallMat, ceilingMat);

            // ===== 기둥 좌우 2열 (Cylinder) =====
            int pillarCountPerSide = 5;
            float pillarSpacing = roomDepth / (pillarCountPerSide + 1);
            float pillarRadius = 0.3f;
            float pillarHeight = roomHeight;

            for (int i = 0; i < pillarCountPerSide; i++)
            {
                float zPos = -roomDepth * 0.5f + pillarSpacing * (i + 1);

                // 왼쪽 기둥
                CreatePillar(room, $"Pillar_Left_{i}", pillarRadius, pillarHeight,
                    new Vector3(-roomWidth * 0.25f, pillarHeight * 0.5f, zPos), pillarMat);

                // 오른쪽 기둥
                CreatePillar(room, $"Pillar_Right_{i}", pillarRadius, pillarHeight,
                    new Vector3(roomWidth * 0.25f, pillarHeight * 0.5f, zPos), pillarMat);
            }

            // ===== 왕좌 공간 (뒷벽 중앙) =====
            // 왕좌 테이블
            GameObject throneTable = IndoorFurniturePlacer.CreateTable(3.0f, 1.2f, 1.5f, throneMat);
            throneTable.name = "ThroneTable";
            throneTable.transform.SetParent(room.transform);
            throneTable.transform.localPosition = new Vector3(0, 0, roomDepth * 0.5f - 2.0f);

            // 왕좌 의자 (큰 의자)
            GameObject throneChair = IndoorFurniturePlacer.CreateChair(1.5f, throneMat);
            throneChair.name = "ThroneChair";
            throneChair.transform.SetParent(room.transform);
            throneChair.transform.localPosition = new Vector3(0, 0, roomDepth * 0.5f - 1.0f);
            throneChair.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);

            // ===== 회의 테이블 (중앙) =====
            GameObject meetingTable = IndoorFurniturePlacer.CreateTable(4.0f, 1.0f, 2.0f, throneMat);
            meetingTable.name = "MeetingTable";
            meetingTable.transform.SetParent(room.transform);
            meetingTable.transform.localPosition = new Vector3(0, 0, -1.0f);

            // 회의용 의자 4개
            float chairOffset = 1.5f;
            for (int side = -1; side <= 1; side += 2)
            {
                for (int pos = -1; pos <= 1; pos += 2)
                {
                    GameObject meetingChair = IndoorFurniturePlacer.CreateChair(1.0f, throneMat);
                    meetingChair.name = $"MeetingChair_{side}_{pos}";
                    meetingChair.transform.SetParent(room.transform);
                    meetingChair.transform.localPosition = new Vector3(side * chairOffset, 0, -1.0f + pos * 1.2f);
                    meetingChair.transform.localRotation = Quaternion.Euler(0, side > 0 ? 180 : 0, 0);
                }
            }

            // ===== 조명 설정 =====
            Color ambient = new Color(0.08f, 0.07f, 0.06f);
            IndoorLighting.SetupIndoorLighting(room, ambient, 0.7f, false);

            // 추가 샹들리에 조명 (복도 중앙)
            IndoorLighting.AddPointLight(room,
                new Vector3(0, roomHeight - 0.5f, 0),
                new Color(1f, 0.9f, 0.7f), 12f, 1.0f);

            // 왕좌 근처 추가 조명
            IndoorLighting.AddPointLight(room,
                new Vector3(0, roomHeight - 0.5f, roomDepth * 0.5f - 2.0f),
                new Color(1f, 0.85f, 0.6f), 8f, 0.8f);

            Debug.Log($"[CastleInteriorBuilder] 성 내부 생성 완료! (스타일: {nationStyle})");
            return room;
        }

        /// <summary>
        /// Cylinder Primitive 기둥 생성.
        /// </summary>
        private static void CreatePillar(GameObject parent, string name, float radius, float height,
            Vector3 position, Material mat)
        {
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = name;
            pillar.transform.SetParent(parent.transform);
            pillar.transform.localPosition = position;
            pillar.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);

            var renderer = pillar.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = mat;
            }
        }
    }
}
