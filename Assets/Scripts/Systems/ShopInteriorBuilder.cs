using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// C11-07: 상점 실내 인테리어 빌더.
    /// IndoorBuilder, IndoorFurniturePlacer, IndoorLighting, IndoorTextureGenerator를
    /// 조합하여 완성된 상점 실내를 생성.
    /// </summary>
    public static class ShopInteriorBuilder
    {
        /// <summary>
        /// 완성된 상점 실내 GameObject 반환.
        /// 방(10x3.5x8) + 카운터 1개 + 선반 2~3개 + 따뜻한 조명.
        /// </summary>
        public static GameObject BuildShopInterior()
        {
            const float roomWidth = 10f;
            const float roomHeight = 3.5f;
            const float roomDepth = 8f;

            // ===== 텍스처 생성 (C11-03, C11-04 프리셋) =====
            Texture2D floorTex = IndoorTextureGenerator.GenerateShopFloor();
            Texture2D wallTex = IndoorTextureGenerator.GenerateShopWall();
            // 천장은 회반죽 기본
            Texture2D ceilingTex = IndoorTextureGenerator.GeneratePlasterWall(256, 256,
                new Color(0.90f, 0.87f, 0.80f));

            // ===== 재질 생성 (URP Lit) =====
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[ShopInteriorBuilder] URP Lit shader not found!");
                shader = Shader.Find("Standard");
            }

            Material floorMat = new Material(shader) { name = "Shop_FloorMat" };
            floorMat.mainTexture = floorTex;
            floorMat.color = Color.white;

            Material wallMat = new Material(shader) { name = "Shop_WallMat" };
            wallMat.mainTexture = wallTex;
            wallMat.color = Color.white;

            Material ceilingMat = new Material(shader) { name = "Shop_CeilingMat" };
            ceilingMat.mainTexture = ceilingTex;
            ceilingMat.color = Color.white;

            Material furnitureMat = new Material(shader) { name = "Shop_FurnitureMat" };
            furnitureMat.color = new Color(0.50f, 0.35f, 0.20f); // 짙은 나무색

            // ===== 방 생성 (C11-01) =====
            GameObject room = IndoorBuilder.CreateRoom(roomWidth, roomHeight, roomDepth,
                floorMat, wallMat, ceilingMat);

            // ===== 가구 배치 (C11-06) =====
            // 카운터 1개 (뒷벽 앞)
            float counterWidth = 2f;
            float counterHeight = 1.2f;
            float counterDepth = 0.8f;
            GameObject counter = IndoorFurniturePlacer.CreateCounter(counterWidth, counterHeight, counterDepth, furnitureMat);
            counter.transform.SetParent(room.transform);
            counter.transform.localPosition = new Vector3(0, 0, -roomDepth * 0.5f + counterDepth * 0.5f + 0.3f);

            // 선반 2개 (좌우 벽)
            float shelfWidth = 1.5f;
            float shelfHeight = 2.5f;
            float shelfDepth = 0.4f;

            // 왼쪽 선반
            GameObject shelfLeft = IndoorFurniturePlacer.CreateShelf(shelfWidth, shelfHeight, shelfDepth, furnitureMat, 3);
            shelfLeft.transform.SetParent(room.transform);
            shelfLeft.transform.localPosition = new Vector3(-roomWidth * 0.5f + shelfDepth * 0.5f + 0.3f, 0, -1.5f);

            // 오른쪽 선반
            GameObject shelfRight = IndoorFurniturePlacer.CreateShelf(shelfWidth, shelfHeight, shelfDepth, furnitureMat, 3);
            shelfRight.transform.SetParent(room.transform);
            shelfRight.transform.localPosition = new Vector3(roomWidth * 0.5f - shelfDepth * 0.5f - 0.3f, 0, 1.5f);

            // 카운터 앞에 작은 테이블 (진열용)
            Material displayMat = new Material(shader) { name = "Shop_DisplayMat" };
            displayMat.color = new Color(0.55f, 0.40f, 0.25f);
            GameObject displayTable = IndoorFurniturePlacer.CreateTable(1.2f, 0.6f, 0.9f, displayMat);
            displayTable.transform.SetParent(room.transform);
            displayTable.transform.localPosition = new Vector3(1.5f, 0, -roomDepth * 0.5f + 2f);

            // ===== 조명 설정 (C11-05) =====
            // 따뜻한 앰비언트 + 천장 중앙 Point Light + 깜빡임
            Color ambientWarm = new Color(0.15f, 0.10f, 0.05f);
            IndoorLighting.SetupIndoorLighting(room, ambientWarm, 1f, true);

            // 추가 포인트 라이트 (카운터 위)
            IndoorLighting.AddPointLight(room,
                new Vector3(0, roomHeight - 0.5f, -roomDepth * 0.5f + 1.5f),
                new Color(1f, 0.9f, 0.7f), 4f, 0.8f);

            Debug.Log("[ShopInteriorBuilder] 상점 실내 생성 완료!");
            return room;
        }
    }
}