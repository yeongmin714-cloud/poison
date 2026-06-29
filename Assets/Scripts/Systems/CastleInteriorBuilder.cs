using ProjectName.Core;
using UnityEngine;
#pragma warning disable 0414

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

            // ===== 재질 생성 (URP Lit, fallback Standard) =====
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning("[CastleInteriorBuilder] URP Lit shader not found. Falling back to Standard.");
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                Debug.LogError("[CastleInteriorBuilder] No shader found (URP Lit nor Standard)! Using default material.");
                shader = Shader.Find("Universal Render Pipeline/Lit");
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

            Material officeMat = new Material(shader) { name = "Castle_OfficeMat" };
            officeMat.color = new Color(0.35f, 0.25f, 0.15f); // 집무실 나무

            Material armoryMat = new Material(shader) { name = "Castle_ArmoryMat" };
            armoryMat.color = new Color(0.40f, 0.35f, 0.30f); // 무기고 석재

            Material vaultMat = new Material(shader) { name = "Castle_VaultMat" };
            vaultMat.color = new Color(0.25f, 0.20f, 0.15f); // 금고실 어두운 석재

            Material archiveMat = new Material(shader) { name = "Castle_ArchiveMat" };
            archiveMat.color = new Color(0.50f, 0.40f, 0.30f); // 문서고

            // ===== 방 생성 =====
            GameObject room = IndoorBuilder.CreateRoom(roomWidth, roomHeight, roomDepth,
                floorMat, wallMat, ceilingMat);

            if (room == null)
            {
                Debug.LogError("[CastleInteriorBuilder] 방 생성 실패: IndoorBuilder.CreateRoom returned null.");
                var fallback = new GameObject("Room_Fallback");
                return fallback;
            }

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
                    // pos=-1: 뒤쪽 (z<-1), pos=1: 앞쪽 (z>-1) — 모두 테이블 중앙(0,0,-1)을 향함
                    meetingChair.transform.localRotation = Quaternion.Euler(0, side > 0 ? -90 : 90, 0);
                }
            }

            // ===== Phase 35: 성 내부 추가 구역 =====
            // 1. 영주 집무실 (왼쪽 벽 — VeryHard)
            GameObject lordOfficeDoor = new GameObject("LordOfficeDoor_Locked");
            lordOfficeDoor.transform.SetParent(room.transform);
            lordOfficeDoor.transform.localPosition = new Vector3(-roomWidth * 0.5f + 0.5f, 1.5f, -roomDepth * 0.25f);
            lordOfficeDoor.transform.localScale = new Vector3(1.0f, 2.5f, 0.3f);
            var officeRenderer = lordOfficeDoor.AddComponent<MeshRenderer>();
            var officeFilter = lordOfficeDoor.AddComponent<MeshFilter>();
            officeFilter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            if (officeRenderer != null) officeRenderer.sharedMaterial = officeMat;
            var officeLock = lordOfficeDoor.AddComponent<LockedDoor>();
            officeLock.LocationId = "castle_lord_office";
            officeLock.Difficulty = LockpickingSystem.LockDifficulty.VeryHard;
            var officeLabel = lordOfficeDoor.AddComponent<NameplateDisplay>();
            officeLabel.DisplayName = "🚪 영주 집무실 (잠김)";

            // 2. 무기고 (오른쪽 벽 — Hard)
            GameObject armoryDoor = new GameObject("ArmoryDoor_Locked");
            armoryDoor.transform.SetParent(room.transform);
            armoryDoor.transform.localPosition = new Vector3(roomWidth * 0.5f - 0.5f, 1.5f, -roomDepth * 0.25f);
            armoryDoor.transform.localScale = new Vector3(1.0f, 2.5f, 0.3f);
            var armoryRenderer = armoryDoor.AddComponent<MeshRenderer>();
            var armoryFilter = armoryDoor.AddComponent<MeshFilter>();
            armoryFilter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            if (armoryRenderer != null) armoryRenderer.sharedMaterial = armoryMat;
            var armoryLock = armoryDoor.AddComponent<LockedDoor>();
            armoryLock.LocationId = "castle_armory";
            armoryLock.Difficulty = LockpickingSystem.LockDifficulty.Hard;
            var armoryLabel = armoryDoor.AddComponent<NameplateDisplay>();
            armoryLabel.DisplayName = "⚔️ 무기고 (잠김)";

            // 3. 금고실 (왼쪽 뒷벽 — Legendary)
            GameObject vaultDoor = new GameObject("VaultDoor_Locked");
            vaultDoor.transform.SetParent(room.transform);
            vaultDoor.transform.localPosition = new Vector3(-roomWidth * 0.25f, 1.5f, roomDepth * 0.5f - 1.5f);
            vaultDoor.transform.localScale = new Vector3(1.2f, 2.5f, 0.3f);
            var vaultRenderer = vaultDoor.AddComponent<MeshRenderer>();
            var vaultFilter = vaultDoor.AddComponent<MeshFilter>();
            vaultFilter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            if (vaultRenderer != null) vaultRenderer.sharedMaterial = vaultMat;
            var vaultLock = vaultDoor.AddComponent<LockedDoor>();
            vaultLock.LocationId = "castle_vault";
            vaultLock.Difficulty = LockpickingSystem.LockDifficulty.Legendary;
            var vaultLabel = vaultDoor.AddComponent<NameplateDisplay>();
            vaultLabel.DisplayName = "💰 금고실 (전설 잠김)";

            // 4. 문서고 (오른쪽 뒷벽 — Easy)
            GameObject archiveDoor = new GameObject("ArchiveDoor_Locked");
            archiveDoor.transform.SetParent(room.transform);
            archiveDoor.transform.localPosition = new Vector3(roomWidth * 0.25f, 1.5f, roomDepth * 0.5f - 1.5f);
            archiveDoor.transform.localScale = new Vector3(1.0f, 2.5f, 0.3f);
            var archiveRenderer = archiveDoor.AddComponent<MeshRenderer>();
            var archiveFilter = archiveDoor.AddComponent<MeshFilter>();
            archiveFilter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            if (archiveRenderer != null) archiveRenderer.sharedMaterial = archiveMat;
            var archiveLock = archiveDoor.AddComponent<LockedDoor>();
            archiveLock.LocationId = "castle_archive";
            archiveLock.Difficulty = LockpickingSystem.LockDifficulty.Easy;
            var archiveLabel = archiveDoor.AddComponent<NameplateDisplay>();
            archiveLabel.DisplayName = "📜 문서고 (잠김)";

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
