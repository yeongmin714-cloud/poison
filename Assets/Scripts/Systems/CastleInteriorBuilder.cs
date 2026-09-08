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
            // 기존 1인자 호출 100% 보존: variant 0 = 기존 배치
            return BuildCastleInterior(nationStyle, 0);
        }

        /// <summary>
        /// 국가 스타일 + 레이아웃 변형(0~7)에 맞는 성 내부 생성.
        /// 결정론: layoutVariant 정수만으로 모든 배치가 고정됨 (Random/전역 상태 없음).
        /// 모든 variant에서 잠긴 문 4개(집무실/무기고/금고실/문서고)가 존재하며
        /// LocationId/Difficulty/Nameplate 라벨은 유지됨 (위치만 variant에 따라 변경).
        /// </summary>
        /// <param name="nationStyle">"Eastern"/"Western"/"Southern"/"Northern"/"Empire"</param>
        /// <param name="layoutVariant">0~7 (범위 밖 값은 8로 나눈 나머지로 정규화)</param>
        public static GameObject BuildCastleInterior(string nationStyle, int layoutVariant)
        {
            // ===== 결정론 정규화: 임의 정수 → 0..7 =====
            const int variantCount = 8;
            layoutVariant = ((layoutVariant % variantCount) + variantCount) % variantCount;

            const float roomWidth = 20f;
            const float roomHeight = 6f;
            const float roomDepth = 15f;

            // ===== 레이아웃 변형 파라미터 (결정론 테이블) =====
            // flipSides: 좌우 대칭 플립 (잠긴 문 4개 위치 좌/우 스왑, 기능 유지)
            // pillarCountPerSide: 기둥 개수(3~6), pillarXFactor: 기둥 x 위치 계수
            // meetingZ/throneZ: 회의테이블/왕좌 z 시프트
            // extraFurniture: 소형 가구(선반/랙/카운터) 추가 여부
            // lightXShift: 중앙 조명 x 미세 변형
            GetLayoutVariantParams(layoutVariant,
                out bool flipSides, out int pillarCountPerSide, out float pillarXFactor,
                out float meetingZ, out float throneZ, out bool extraFurniture,
                out float lightXShift);

            // 좌우 플립 부호: variant 0 = -1 (기존: 집무실/금고실 왼쪽)
            float doorSideSign = flipSides ? 1f : -1f;

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

            // ===== 기둥 좌우 2열 (Cylinder) — variant별 개수/간격/위치 =====
            float pillarSpacing = roomDepth / (pillarCountPerSide + 1);
            float pillarRadius = 0.3f;
            float pillarHeight = roomHeight;
            float pillarX = roomWidth * pillarXFactor;

            for (int i = 0; i < pillarCountPerSide; i++)
            {
                float zPos = -roomDepth * 0.5f + pillarSpacing * (i + 1);

                // 왼쪽 기둥
                CreatePillar(room, $"Pillar_Left_{i}", pillarRadius, pillarHeight,
                    new Vector3(-pillarX, pillarHeight * 0.5f, zPos), pillarMat);

                // 오른쪽 기둥
                CreatePillar(room, $"Pillar_Right_{i}", pillarRadius, pillarHeight,
                    new Vector3(pillarX, pillarHeight * 0.5f, zPos), pillarMat);
            }

            // ===== 왕좌 공간 (뒷벽 중앙) — variant별 z 시프트 =====
            // 왕좌 테이블
            GameObject throneTable = IndoorFurniturePlacer.CreateTable(3.0f, 1.2f, 1.5f, throneMat);
            throneTable.name = "ThroneTable";
            throneTable.transform.SetParent(room.transform);
            throneTable.transform.localPosition = new Vector3(0, 0, throneZ);

            // 왕좌 의자 (큰 의자)
            GameObject throneChair = IndoorFurniturePlacer.CreateChair(1.5f, throneMat);
            throneChair.name = "ThroneChair";
            throneChair.transform.SetParent(room.transform);
            throneChair.transform.localPosition = new Vector3(0, 0, throneZ + 1.0f);
            throneChair.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);

            // ===== 회의 테이블 (중앙) — variant별 z 시프트 =====
            GameObject meetingTable = IndoorFurniturePlacer.CreateTable(4.0f, 1.0f, 2.0f, throneMat);
            meetingTable.name = "MeetingTable";
            meetingTable.transform.SetParent(room.transform);
            meetingTable.transform.localPosition = new Vector3(0, 0, meetingZ);

            // 회의용 의자 4개
            float chairOffset = 1.5f;
            for (int side = -1; side <= 1; side += 2)
            {
                for (int pos = -1; pos <= 1; pos += 2)
                {
                    GameObject meetingChair = IndoorFurniturePlacer.CreateChair(1.0f, throneMat);
                    meetingChair.name = $"MeetingChair_{side}_{pos}";
                    meetingChair.transform.SetParent(room.transform);
                    meetingChair.transform.localPosition = new Vector3(side * chairOffset, 0, meetingZ + pos * 1.2f);
                    // pos=-1: 뒤쪽 (z<meetingZ), pos=1: 앞쪽 (z>meetingZ) — 모두 테이블 중앙을 향함
                    meetingChair.transform.localRotation = Quaternion.Euler(0, side > 0 ? -90 : 90, 0);
                }
            }

            // ===== 소형 추가 가구 (variant별 유무) =====
            // 방 경계/기둥/문과 겹치지 않는 고정 좌표(전면벽 쪽 + 측벽 사이 공백)에만 배치
            if (extraFurniture)
            {
                // 옷장(선반형) — 전면벽 왼쪽
                GameObject wardrobe = IndoorFurniturePlacer.CreateShelf(1.4f, 2.2f, 0.5f, officeMat, 2);
                wardrobe.name = "Wardrobe";
                wardrobe.transform.SetParent(room.transform);
                wardrobe.transform.localPosition = new Vector3(-3.5f, 0, -roomDepth * 0.5f + 0.6f);

                // 무기 랙(선반형) — 전면벽 오른쪽
                GameObject weaponRack = IndoorFurniturePlacer.CreateShelf(1.6f, 1.8f, 0.5f, armoryMat, 2);
                weaponRack.name = "WeaponRack";
                weaponRack.transform.SetParent(room.transform);
                weaponRack.transform.localPosition = new Vector3(4.5f, 0, -roomDepth * 0.5f + 0.6f);

                // 소형 탁자(카운터형) — 왼쪽 벽 공백(측벽 문과 기둥 사이)
                GameObject sideTable = IndoorFurniturePlacer.CreateCounter(1.2f, 1.0f, 0.8f, throneMat);
                sideTable.name = "SideTable";
                sideTable.transform.SetParent(room.transform);
                sideTable.transform.localPosition = new Vector3(-roomWidth * 0.5f + 1.0f, 0, 0.5f);
            }

            // ===== Phase 35: 성 내부 추가 구역 =====
            // 1. 영주 집무실 (왼쪽 벽 — VeryHard)
            GameObject lordOfficeDoor = new GameObject("LordOfficeDoor_Locked");
            lordOfficeDoor.transform.SetParent(room.transform);
            lordOfficeDoor.transform.localPosition = new Vector3(doorSideSign * (roomWidth * 0.5f - 0.5f), 1.5f, -roomDepth * 0.25f);
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
            armoryDoor.transform.localPosition = new Vector3(-doorSideSign * (roomWidth * 0.5f - 0.5f), 1.5f, -roomDepth * 0.25f);
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
            vaultDoor.transform.localPosition = new Vector3(doorSideSign * roomWidth * 0.25f, 1.5f, roomDepth * 0.5f - 1.5f);
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
            archiveDoor.transform.localPosition = new Vector3(-doorSideSign * roomWidth * 0.25f, 1.5f, roomDepth * 0.5f - 1.5f);
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

            // 추가 샹들리에 조명 (복도 중앙 — variant별 x 미세 변형)
            IndoorLighting.AddPointLight(room,
                new Vector3(lightXShift, roomHeight - 0.5f, 0),
                new Color(1f, 0.9f, 0.7f), 12f, 1.0f);

            // 왕좌 근처 추가 조명 (왕좌 시프트 추종)
            IndoorLighting.AddPointLight(room,
                new Vector3(0, roomHeight - 0.5f, throneZ),
                new Color(1f, 0.85f, 0.6f), 8f, 0.8f);

            Debug.Log($"[CastleInteriorBuilder] 성 내부 생성 완료! (스타일: {nationStyle}, 레이아웃 변형: {layoutVariant})");
            return room;
        }

        /// <summary>
        /// 레이아웃 변형(0~7)별 결정론 파라미터 테이블.
        /// Random/전역 상태 없음 — layoutVariant 정수만으로 배치가 완전히 결정됨.
        /// variant 0 = 기존 배치와 정확히 동일 (회귀 방지).
        /// 모든 variant에서 문 4개는 z 경계(±7.5)와 서로 겹치지 않는 벽면에 존재.
        /// </summary>
        private static void GetLayoutVariantParams(int variant,
            out bool flipSides, out int pillarCountPerSide, out float pillarXFactor,
            out float meetingZ, out float throneZ, out bool extraFurniture,
            out float lightXShift)
        {
            //              flip  pillars  xFactor  meetZ  throneZ  extra  lightX
            // 0 (기본):    no    5        0.25     -1.0   5.5      no     0.0
            // 1 (플립):    yes   5        0.25     -1.0   5.5      no     0.0
            // 2 (소형):    no    3        0.25     -1.5   5.3      yes    0.0
            // 3 (촘촘):    yes   6        0.25     -0.5   5.7      no     +1.0
            // 4 (넓은):    no    4        0.20     -2.0   5.5      yes    -1.0
            // 5 (외곽):    yes   4        0.30     -1.0   5.2      yes    0.0
            // 6 (대형):    no    6        0.20     -1.5   5.7      yes    +1.5
            // 7 (소플립):  yes   3        0.30     -0.5   5.5      no     -1.5
            switch (variant)
            {
                case 1:
                    flipSides = true;  pillarCountPerSide = 5; pillarXFactor = 0.25f;
                    meetingZ = -1.0f;  throneZ = 5.5f;  extraFurniture = false; lightXShift = 0f;
                    break;
                case 2:
                    flipSides = false; pillarCountPerSide = 3; pillarXFactor = 0.25f;
                    meetingZ = -1.5f;  throneZ = 5.3f;  extraFurniture = true;  lightXShift = 0f;
                    break;
                case 3:
                    flipSides = true;  pillarCountPerSide = 6; pillarXFactor = 0.25f;
                    meetingZ = -0.5f;  throneZ = 5.7f;  extraFurniture = false; lightXShift = 1.0f;
                    break;
                case 4:
                    flipSides = false; pillarCountPerSide = 4; pillarXFactor = 0.20f;
                    meetingZ = -2.0f;  throneZ = 5.5f;  extraFurniture = true;  lightXShift = -1.0f;
                    break;
                case 5:
                    flipSides = true;  pillarCountPerSide = 4; pillarXFactor = 0.30f;
                    meetingZ = -1.0f;  throneZ = 5.2f;  extraFurniture = true;  lightXShift = 0f;
                    break;
                case 6:
                    flipSides = false; pillarCountPerSide = 6; pillarXFactor = 0.20f;
                    meetingZ = -1.5f;  throneZ = 5.7f;  extraFurniture = true;  lightXShift = 1.5f;
                    break;
                case 7:
                    flipSides = true;  pillarCountPerSide = 3; pillarXFactor = 0.30f;
                    meetingZ = -0.5f;  throneZ = 5.5f;  extraFurniture = false; lightXShift = -1.5f;
                    break;
                case 0:
                default:
                    // 기존 배치와 정확히 동일 (기둥 5개/사이드, x=±5, 왕좌 z=5.5, 회의 z=-1, 문 좌측 배치)
                    flipSides = false; pillarCountPerSide = 5; pillarXFactor = 0.25f;
                    meetingZ = -1.0f;  throneZ = 5.5f;  extraFurniture = false; lightXShift = 0f;
                    break;
            }
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
