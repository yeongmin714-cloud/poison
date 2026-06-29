using ProjectName.Core;
using ProjectName.UI;
using UnityEngine;

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
            Shader shader = ResolveShader("Universal Render Pipeline/Lit", "Standard");
            if (shader == null)
            {
                Debug.LogError("[ShopInteriorBuilder] URP Lit 및 Standard shader 모두 찾을 수 없음!");
                return null;
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

            Material officeMat = new Material(shader) { name = "Shop_OfficeMat" };
            officeMat.color = new Color(0.40f, 0.25f, 0.15f); // 어두운 나무색 (사무실)

            // ===== 방 생성 (C11-01) =====
            GameObject room = IndoorBuilder.CreateRoom(roomWidth, roomHeight, roomDepth,
                floorMat, wallMat, ceilingMat);
            if (room == null)
            {
                Debug.LogError("[ShopInteriorBuilder] IndoorBuilder.CreateRoom returned null!");
                return null;
            }

            // ===== 가구 배치 (C11-06) =====
            // 카운터 1개 (뒷벽 앞)
            float counterWidth = 2f;
            float counterHeight = 1.2f;
            float counterDepth = 0.8f;
            GameObject counter = IndoorFurniturePlacer.CreateCounter(counterWidth, counterHeight, counterDepth, furnitureMat);
            if (counter != null)
            {
                counter.transform.SetParent(room.transform);
                counter.transform.localPosition = new Vector3(0, 0, -roomDepth * 0.5f + counterDepth * 0.5f + 0.3f);
            }

            // 선반 2개 (좌우 벽)
            float shelfWidth = 1.5f;
            float shelfHeight = 2.5f;
            float shelfDepth = 0.4f;

            // 왼쪽 선반
            GameObject shelfLeft = IndoorFurniturePlacer.CreateShelf(shelfWidth, shelfHeight, shelfDepth, furnitureMat, 3);
            if (shelfLeft != null)
            {
                shelfLeft.transform.SetParent(room.transform);
                shelfLeft.transform.localPosition = new Vector3(-roomWidth * 0.5f + shelfDepth * 0.5f + 0.3f, 0, -1.5f);
            }

            // 오른쪽 선반
            GameObject shelfRight = IndoorFurniturePlacer.CreateShelf(shelfWidth, shelfHeight, shelfDepth, furnitureMat, 3);
            if (shelfRight != null)
            {
                shelfRight.transform.SetParent(room.transform);
                shelfRight.transform.localPosition = new Vector3(roomWidth * 0.5f - shelfDepth * 0.5f - 0.3f, 0, 1.5f);
            }

            // 카운터 앞에 작은 테이블 (진열용)
            Material displayMat = new Material(shader) { name = "Shop_DisplayMat" };
            displayMat.color = new Color(0.55f, 0.40f, 0.25f);
            GameObject displayTable = IndoorFurniturePlacer.CreateTable(1.2f, 0.6f, 0.9f, displayMat);
            if (displayTable != null)
            {
                displayTable.transform.SetParent(room.transform);
                displayTable.transform.localPosition = new Vector3(1.5f, 0, -roomDepth * 0.5f + 2f);
            }

            // ===== Phase 35: 카운터 뒤 사무실 (금고) =====
            // 뒷벽 쪽에 사무실 공간 표시 (벽면 큐브)
            float officeDepth = 2f;
            float officeWidth = 3f;
            GameObject officeFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            officeFloor.name = "OfficeFloor";
            officeFloor.transform.SetParent(room.transform);
            officeFloor.transform.localPosition = new Vector3(0, 0.01f, -roomDepth * 0.5f + officeDepth * 0.5f);
            officeFloor.transform.localScale = new Vector3(officeWidth, 0.05f, officeDepth);
            var officeFloorRenderer = officeFloor.GetComponent<MeshRenderer>();
            if (officeFloorRenderer != null) officeFloorRenderer.sharedMaterial = officeMat;

            // 사무실 책상
            GameObject officeDesk = IndoorFurniturePlacer.CreateTable(1.5f, 0.8f, 0.7f, officeMat);
            officeDesk.name = "OfficeDesk";
            officeDesk.transform.SetParent(room.transform);
            officeDesk.transform.localPosition = new Vector3(0, 0, -roomDepth * 0.5f + 1.2f);

            // ===== Phase 35: 잠긴 금고 문 =====
            // 사무실 내 금고 (LockedDoor 프리팹 대신 컴포넌트 추가)
            GameObject safeDoor = new GameObject("SafeDoor_Locked");
            safeDoor.transform.SetParent(room.transform);
            safeDoor.transform.localPosition = new Vector3(1.0f, 0.8f, -roomDepth * 0.5f + 1.8f);
            safeDoor.transform.localScale = new Vector3(0.6f, 0.8f, 0.2f);
            var safeRenderer = safeDoor.AddComponent<MeshRenderer>();
            var safeFilter = safeDoor.AddComponent<MeshFilter>();
            safeFilter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            if (safeRenderer != null) safeRenderer.sharedMaterial = officeMat;
            var safeLock = safeDoor.AddComponent<LockedDoor>();
            safeLock.LocationId = "shop_safe";
            safeLock.Difficulty = LockpickingSystem.LockDifficulty.Hard;

            // ===== Phase 35: 지하 창고 입구 (잠긴 문) =====
            GameObject storageDoor = new GameObject("StorageDoor_Locked");
            storageDoor.transform.SetParent(room.transform);
            storageDoor.transform.localPosition = new Vector3(roomWidth * 0.5f - 1.0f, 0.5f, roomDepth * 0.5f - 0.8f);
            storageDoor.transform.localScale = new Vector3(1.2f, 2.0f, 0.2f);
            var storageRenderer = storageDoor.AddComponent<MeshRenderer>();
            var storageFilter = storageDoor.AddComponent<MeshFilter>();
            storageFilter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            if (storageRenderer != null) storageRenderer.sharedMaterial = furnitureMat;
            var storageLock = storageDoor.AddComponent<LockedDoor>();
            storageLock.LocationId = "shop_storage";
            storageLock.Difficulty = LockpickingSystem.LockDifficulty.Medium;

            // 지하 창고 라벨
            var storageLabel = storageDoor.AddComponent<NameplateDisplay>();
            storageLabel.DisplayName = "🚪 지하 창고 (잠김)";

            // ===== 조명 설정 (C11-05) =====
            // 따뜻한 앰비언트 + 천장 중앙 Point Light + 깜빡임
            Color ambientWarm = new Color(0.15f, 0.10f, 0.05f);
            IndoorLighting.SetupIndoorLighting(room, ambientWarm, 1f, true);

            // 추가 포인트 라이트 (카운터 위)
            IndoorLighting.AddPointLight(room,
                new Vector3(0, roomHeight - 0.5f, -roomDepth * 0.5f + 1.5f),
                new Color(1f, 0.9f, 0.7f), 4f, 0.8f);

            Debug.Log("[ShopInteriorBuilder] 상점 실내 생성 완료!");

            // ===== FIX-01: 상점 NPC 생성 =====
            GameObject shopNpc = new GameObject("ShopNPC");
            shopNpc.transform.SetParent(room.transform);
            // 카운터 뒤: 카운터 localPosition 기준으로 z 방향으로 -0.8f 뒤
            float npcZ = counter != null
                ? counter.transform.localPosition.z - 0.8f
                : -roomDepth * 0.5f - 0.5f; // 폴백: 뒷벽 바깥쪽
            shopNpc.transform.localPosition = new Vector3(0, 0, npcZ);
            shopNpc.AddComponent<ShopPlaceholder>();
            // 이름표 "상인" 표시
            var nameplate = shopNpc.AddComponent<NameplateDisplay>();
            nameplate.DisplayName = "상인";

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
        /// 셰이더 찾기 — primary 실패 시 fallback, 둘 다 없으면 null 반환.
        /// </summary>
        private static Shader ResolveShader(string primary, string fallback)
        {
            Shader shader = Shader.Find(primary);
            if (shader == null)
            {
                Debug.LogWarning($"[ShopInteriorBuilder] '{primary}' shader not found, falling back to '{fallback}'.");
                shader = Shader.Find(fallback);
            }
            if (shader == null)
            {
                Debug.LogError($"[ShopInteriorBuilder] '{fallback}' shader also not found!");
            }
            return shader;
        }
    }
}