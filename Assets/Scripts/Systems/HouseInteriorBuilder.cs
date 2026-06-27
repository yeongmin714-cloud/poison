using System.Collections.Generic;
using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C11-10: NPC 주택 실내 인테리어 빌더.
    /// 방(8x3x6) + 나무 바닥 + 회반죽 벽 + 침대 + 테이블+의자 2개 + 난로.
    /// </summary>
    public static class HouseInteriorBuilder
    {
        /// <summary>
        /// 완성된 NPC 주택 실내 GameObject 반환.
        /// 생성된 Material은 자동 추적되어 GameObject 파괴 시 함께 정리됩니다.
        /// </summary>
        public static GameObject BuildHouseInterior()
        {
            const float roomWidth = 8f;
            const float roomHeight = 3f;
            const float roomDepth = 6f;

            // ===== 텍스처 생성 =====
            Texture2D floorTex = IndoorTextureGenerator.GenerateHouseFloor();
            Texture2D wallTex = IndoorTextureGenerator.GenerateHouseWall();
            Texture2D ceilingTex = IndoorTextureGenerator.GeneratePlasterWall(256, 256,
                new Color(0.78f, 0.72f, 0.60f));

            // ===== 재질 생성 (URP Lit) =====
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[HouseInteriorBuilder] URP Lit shader not found!");
                shader = Shader.Find("Standard");
            }

            Material floorMat = new Material(shader) { name = "House_FloorMat" };
            floorMat.mainTexture = floorTex;
            floorMat.color = Color.white;

            Material wallMat = new Material(shader) { name = "House_WallMat" };
            wallMat.mainTexture = wallTex;
            wallMat.color = Color.white;

            Material ceilingMat = new Material(shader) { name = "House_CeilingMat" };
            ceilingMat.mainTexture = ceilingTex;
            ceilingMat.color = Color.white;

            Material woodMat = new Material(shader) { name = "House_WoodMat" };
            woodMat.color = new Color(0.52f, 0.35f, 0.18f); // 중간 갈색 나무

            Material fabricMat = new Material(shader) { name = "House_FabricMat" };
            fabricMat.color = new Color(0.60f, 0.50f, 0.40f); // 천/매트리스 색

            // ===== 방 생성 =====
            GameObject room = IndoorBuilder.CreateRoom(roomWidth, roomHeight, roomDepth,
                floorMat, wallMat, ceilingMat);

            // ===== Material 누수 방지: 생성된 재질을 room에 추적 등록 =====
            var matTracker = room.AddComponent<MaterialTracker>();
            matTracker.Track(floorMat);
            matTracker.Track(wallMat);
            matTracker.Track(ceilingMat);
            matTracker.Track(woodMat);
            matTracker.Track(fabricMat);

            // ===== 침대 =====
            GameObject bed = IndoorFurniturePlacer.CreateBed(1.8f, 2.0f, fabricMat);
            bed.transform.SetParent(room.transform);
            bed.transform.localPosition = new Vector3(roomWidth * 0.5f - 2.0f, 0, 0);

            // ===== 테이블 + 의자 2개 =====
            GameObject table = IndoorFurniturePlacer.CreateTable(1.0f, 0.8f, 0.8f, woodMat);
            table.transform.SetParent(room.transform);
            table.transform.localPosition = new Vector3(-1.0f, 0, 0);

            // 의자 1 (테이블 왼쪽)
            GameObject chair1 = IndoorFurniturePlacer.CreateChair(0.8f, woodMat);
            chair1.name = "Chair_1";
            chair1.transform.SetParent(room.transform);
            chair1.transform.localPosition = new Vector3(-2.0f, 0, 0.8f);
            chair1.transform.localRotation = Quaternion.Euler(0, 90, 0);

            // 의자 2 (테이블 오른쪽)
            GameObject chair2 = IndoorFurniturePlacer.CreateChair(0.8f, woodMat);
            chair2.name = "Chair_2";
            chair2.transform.SetParent(room.transform);
            chair2.transform.localPosition = new Vector3(0f, 0, 0.8f);
            chair2.transform.localRotation = Quaternion.Euler(0, -90, 0);

            // ===== 난로 (CreateTable + 주황 Point Light) =====
            GameObject stove = IndoorFurniturePlacer.CreateTable(1.2f, 0.8f, 0.8f, woodMat);
            stove.name = "Stove";
            stove.transform.SetParent(room.transform);
            stove.transform.localPosition = new Vector3(-roomWidth * 0.5f + 2.0f, 0, -roomDepth * 0.5f + 1.0f);

            // 난로 위 주황 Point Light
            IndoorLighting.AddPointLight(room,
                new Vector3(-roomWidth * 0.5f + 2.0f, 2.0f, -roomDepth * 0.5f + 1.0f),
                new Color(1f, 0.6f, 0.2f), 6f, 0.8f);

            // ===== 조명 설정 =====
            // 따뜻한 앰비언트 + 깜빡임(난로 불빛 연출)
            Color ambientWarm = new Color(0.12f, 0.08f, 0.04f);
            IndoorLighting.SetupIndoorLighting(room, ambientWarm, 0.9f, true);

            Debug.Log("[HouseInteriorBuilder] NPC 주택 실내 생성 완료!");

            // ===== 마을 주민 NPC 생성 (퀘스트) =====
            GameObject villagerNpc = new GameObject("VillagerNPC");
            villagerNpc.transform.SetParent(room.transform);
            // 테이블 근처
            villagerNpc.transform.localPosition = new Vector3(-1.0f, 0, -1.0f);
            villagerNpc.AddComponent<NPCQuestGiver>();

            // ===== 출구 트리거 생성 =====
            GameObject exitTrigger = new GameObject("ExitTrigger");
            exitTrigger.transform.SetParent(room.transform);
            exitTrigger.transform.localPosition = new Vector3(0, 0, roomDepth * 0.5f - 0.5f);
            var exitBt = exitTrigger.AddComponent<BuildingTrigger>();
            exitBt.BuildingType = "Exit";
            exitBt.InteractRange = 3f;

            return room;
        }

        /// <summary>
        /// 런타임에 생성된 Material을 추적하여 GameObject 파괴 시 함께 정리합니다.
        /// (메모리 누수 방지)
        /// </summary>
        private class MaterialTracker : MonoBehaviour
        {
            private readonly List<Material> _materials = new List<Material>();

            public void Track(Material mat)
            {
                if (mat != null)
                    _materials.Add(mat);
            }

            private void OnDestroy()
            {
                for (int i = _materials.Count - 1; i >= 0; i--)
                {
                    if (_materials[i] != null)
                        Destroy(_materials[i]);
                }
                _materials.Clear();
            }
        }
    }
}
