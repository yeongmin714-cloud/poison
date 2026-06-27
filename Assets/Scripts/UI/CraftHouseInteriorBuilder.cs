using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.UI
{
    /// <summary>
    /// C11-08: 크래프트하우스 실내 인테리어 빌더.
    /// 방(12x4x10) + 돌 바닥 + 석재 벽 + 제작대 2개 + 화덕 + 재료선반 2개.
    /// </summary>
    public static class CraftHouseInteriorBuilder
    {
        /// <summary>
        /// 완성된 크래프트하우스 실내 GameObject 반환.
        /// </summary>
        public static GameObject BuildCraftHouseInterior()
        {
            const float roomWidth = 12f;
            const float roomHeight = 4f;
            const float roomDepth = 10f;

            // ===== 텍스처 생성 =====
            Texture2D floorTex = IndoorTextureGenerator.GenerateCraftHouseFloor();
            Texture2D wallTex = IndoorTextureGenerator.GenerateCraftHouseWall();
            Texture2D ceilingTex = IndoorTextureGenerator.GeneratePlasterWall(256, 256,
                new Color(0.65f, 0.62f, 0.58f));

            // ===== 재질 생성 (URP Lit) =====
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning("[CraftHouseInteriorBuilder] URP Lit shader not found, falling back to Standard.");
                shader = Shader.Find("Standard");
            }
            if (shader == null)
            {
                Debug.LogError("[CraftHouseInteriorBuilder] Standard shader also not found! Cannot create materials.");
                return null;
            }

            Material floorMat = new Material(shader) { name = "CraftHouse_FloorMat" };
            floorMat.mainTexture = floorTex;
            floorMat.color = Color.white;

            Material wallMat = new Material(shader) { name = "CraftHouse_WallMat" };
            wallMat.mainTexture = wallTex;
            wallMat.color = Color.white;

            Material ceilingMat = new Material(shader) { name = "CraftHouse_CeilingMat" };
            ceilingMat.mainTexture = ceilingTex;
            ceilingMat.color = Color.white;

            Material furnitureMat = new Material(shader) { name = "CraftHouse_FurnitureMat" };
            furnitureMat.color = new Color(0.45f, 0.35f, 0.25f); // 짙은 나무색

            Material forgeMat = new Material(shader) { name = "CraftHouse_ForgeMat" };
            forgeMat.color = new Color(0.30f, 0.25f, 0.20f); // 어두운 금속

            // ===== 방 생성 =====
            GameObject room = IndoorBuilder.CreateRoom(roomWidth, roomHeight, roomDepth,
                floorMat, wallMat, ceilingMat);
            if (room == null)
            {
                Debug.LogError("[CraftHouseInteriorBuilder] IndoorBuilder.CreateRoom returned null!");
                return null;
            }

            // ===== 제작대 2개 (CreateTable 변형) =====
            // 첫 번째 제작대 (중앙 근처)
            GameObject workbench1 = IndoorFurniturePlacer.CreateTable(2.0f, 1.0f, 1.2f, furnitureMat);
            workbench1.transform.SetParent(room.transform);
            workbench1.transform.localPosition = new Vector3(-2.5f, 0, 1.0f);

            // 두 번째 제작대
            GameObject workbench2 = IndoorFurniturePlacer.CreateTable(2.0f, 1.0f, 1.2f, furnitureMat);
            workbench2.transform.SetParent(room.transform);
            workbench2.transform.localPosition = new Vector3(2.5f, 0, 1.0f);

            // ===== 화덕 (CreateTable + 빨간 Point Light) =====
            GameObject forge = IndoorFurniturePlacer.CreateTable(1.5f, 1.0f, 1.5f, forgeMat);
            forge.transform.SetParent(room.transform);
            forge.transform.localPosition = new Vector3(0, 0, -roomDepth * 0.5f + 2.0f);

            // 화덕 위 붉은 Point Light
            IndoorLighting.AddPointLight(room,
                new Vector3(0, roomHeight - 1.0f, -roomDepth * 0.5f + 2.0f),
                new Color(1f, 0.3f, 0.1f), 8f, 1.0f);

            // ===== 재료선반 2개 (좌우 벽) =====
            float shelfWidth = 2.0f;
            float shelfHeight = 3.0f;
            float shelfDepth = 0.5f;

            GameObject shelfLeft = IndoorFurniturePlacer.CreateShelf(shelfWidth, shelfHeight, shelfDepth, furnitureMat, 4);
            shelfLeft.transform.SetParent(room.transform);
            shelfLeft.transform.localPosition = new Vector3(-roomWidth * 0.5f + shelfDepth * 0.5f + 0.3f, 0, -2.0f);

            GameObject shelfRight = IndoorFurniturePlacer.CreateShelf(shelfWidth, shelfHeight, shelfDepth, furnitureMat, 4);
            shelfRight.transform.SetParent(room.transform);
            shelfRight.transform.localPosition = new Vector3(roomWidth * 0.5f - shelfDepth * 0.5f - 0.3f, 0, -2.0f);

            // ===== 조명 설정 =====
            // 중간 밝기, 깜빡임 없음
            Color ambientMid = new Color(0.12f, 0.12f, 0.12f);
            IndoorLighting.SetupIndoorLighting(room, ambientMid, 0.8f, false);

            Debug.Log("[CraftHouseInteriorBuilder] 크래프트하우스 실내 생성 완료!");

            // ===== FIX-01: 크래프트 스테이션 생성 =====
            GameObject craftStation = new GameObject("CraftStation");
            craftStation.transform.SetParent(room.transform);
            // 첫 번째 제작대(workbench1) 옆에 배치
            craftStation.transform.localPosition = new Vector3(-2.5f, 0.6f, 2.2f);
            craftStation.AddComponent<CraftingStation>();

            // ===== FIX-01: 출구 트리거 생성 =====
            GameObject exitTrigger = new GameObject("ExitTrigger");
            exitTrigger.transform.SetParent(room.transform);
            exitTrigger.transform.localPosition = new Vector3(0, 0, roomDepth * 0.5f - 0.5f);
            var exitBt = exitTrigger.AddComponent<BuildingTrigger>();
            exitBt.BuildingType = "Exit";
            exitBt.InteractRange = 3f;

            return room;
        }
    }
}
