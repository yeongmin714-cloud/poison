using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 플레이어 소유 영지의 성 내부 절차 생성 (C11 계열 확장).
    /// 기존 CastleInteriorBuilder(타 영주용, 잠긴 문 다수)와 달리
    /// 플레이어가 점령한 "내 영지"이므로 중세 판타지 성 대전당으로 만든다:
    ///   - 지휘 책상 + 관리용 책상/문서 (집무 공간)
    ///   - 저장고 (선반/상자), 무기고 (무기 스탠드), 작업대
    ///   - 중세 석재 기둥 2열 + 화로/토치 (따뜻한 주황빛 조명)
    ///   - 문장 방패/붉은 러그 장식, 플레이어 환영 배너 (유지)
    ///   - 잠금문 없음 (이미 내 것이므로 접근 가능)
    ///   - NameplateDisplay 기능 안내 라벨
    /// 국가별 텍스처/방 생성 틀은 CastleInteriorBuilder와 동일한 API 사용.
    /// </summary>
    public static class PlayerCastleInteriorBuilder
    {
        /// <summary>
        /// 국가 스타일에 맞는 플레이어 소유 중세 판타지 성 내부 생성.
        /// </summary>
        /// <param name="nationStyle">
        /// "Eastern" (동부), "Western" (서부), "Southern" (남부),
        /// "Northern" (북부), "Empire" (황제국)
        /// </param>
        public static GameObject BuildPlayerCastleInterior(string nationStyle)
        {
            // ===== 방 크기 (기존 20x6x15보다 약간 넓게) =====
            const float roomWidth = 22f;
            const float roomHeight = 6f;
            const float roomDepth = 16f;

            // 영지 고유 키 (작업대/저장고 상호작용 컴포넌트 설정용 — 국가 스타일 기반 매핑)
            string territoryKey = GetTerritoryKeyForNationStyle(nationStyle);

            // ===== 국가별 텍스처 생성 (CastleInteriorBuilder와 동일) =====
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
                    Debug.LogWarning($"[PlayerCastleInteriorBuilder] 알 수 없는 국가 스타일: '{nationStyle}'. 기본(Empire) 사용.");
                    floorTex = IndoorTextureGenerator.GenerateCastleFloorEmpire();
                    wallTex = IndoorTextureGenerator.GenerateCastleWallEmpire();
                    break;
            }

            Texture2D ceilingTex = IndoorTextureGenerator.GeneratePlasterWall(256, 256,
                new Color(0.65f, 0.62f, 0.58f));

            // ===== 재질 생성 (URP Lit, fallback Standard) =====
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning("[PlayerCastleInteriorBuilder] URP Lit shader not found. Falling back to Standard.");
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                Debug.LogError("[PlayerCastleInteriorBuilder] No shader found (URP Lit nor Standard)! Using default material.");
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            Material floorMat = new Material(shader) { name = $"PlayerCastle_FloorMat_{nationStyle}" };
            floorMat.mainTexture = floorTex;
            floorMat.color = Color.white;

            Material wallMat = new Material(shader) { name = $"PlayerCastle_WallMat_{nationStyle}" };
            wallMat.mainTexture = wallTex;
            wallMat.color = Color.white;

            Material ceilingMat = new Material(shader) { name = "PlayerCastle_CeilingMat" };
            ceilingMat.mainTexture = ceilingTex;
            ceilingMat.color = Color.white;

            // 기능적 가구 재질들 (실용적 톤)
            Material deskMat = new Material(shader) { name = "PlayerCastle_DeskMat" };
            deskMat.color = new Color(0.45f, 0.30f, 0.16f); // 집무 나무

            Material shelfMat = new Material(shader) { name = "PlayerCastle_ShelfMat" };
            shelfMat.color = new Color(0.50f, 0.36f, 0.22f); // 선반 나무

            Material crateMat = new Material(shader) { name = "PlayerCastle_CrateMat" };
            crateMat.color = new Color(0.55f, 0.42f, 0.26f); // 창고 상자

            Material workbenchMat = new Material(shader) { name = "PlayerCastle_WorkbenchMat" };
            workbenchMat.color = new Color(0.35f, 0.24f, 0.14f); // 작업대 짙은 나무

            Material standMat = new Material(shader) { name = "PlayerCastle_StandMat" };
            standMat.color = new Color(0.25f, 0.25f, 0.28f); // 무기 스탠드 철제

            Material bladeMat = new Material(shader) { name = "PlayerCastle_BladeMat" };
            bladeMat.color = new Color(0.65f, 0.66f, 0.70f); // 무기 강철

            Material bannerMat = new Material(shader) { name = "PlayerCastle_BannerMat" };
            bannerMat.color = new Color(0.20f, 0.45f, 0.75f); // 플레이어 환영 (청색)

            Material trimMat = new Material(shader) { name = "PlayerCastle_TrimMat" };
            trimMat.color = new Color(0.85f, 0.70f, 0.25f); // 금장 트림

            Material paperMat = new Material(shader) { name = "PlayerCastle_PaperMat" };
            paperMat.color = new Color(0.92f, 0.90f, 0.82f); // 문서 용지

            // 중세 판타지 장식 재질들 (석재 기둥/화로/러그)
            Material pillarMat = new Material(shader) { name = "PlayerCastle_PillarMat" };
            pillarMat.color = new Color(0.40f, 0.38f, 0.35f); // 회색 석재 기둥

            Material hearthMat = new Material(shader) { name = "PlayerCastle_HearthMat" };
            hearthMat.color = new Color(0.18f, 0.17f, 0.16f); // 화로 받침 짙은 회색

            Material rugMat = new Material(shader) { name = "PlayerCastle_RugMat" };
            rugMat.color = new Color(0.55f, 0.18f, 0.15f); // 붉은 카펫/문장 자수

            // ===== 방 생성 =====
            GameObject room = IndoorBuilder.CreateRoom(roomWidth, roomHeight, roomDepth,
                floorMat, wallMat, ceilingMat);

            if (room == null)
            {
                Debug.LogError("[PlayerCastleInteriorBuilder] 방 생성 실패: IndoorBuilder.CreateRoom returned null.");
                var fallback = new GameObject("PlayerCastleRoom_Fallback");
                return fallback;
            }

            room.name = "PlayerCastleRoom";

            // ===================================================================
            // 1. 지휘 책상 + 관리용 책상/문서 (왕좌 대체 — 뒷벽 중앙, +z 방향)
            // ===================================================================
            GameObject commandDesk = IndoorFurniturePlacer.CreateTable(3.2f, 1.2f, 1.1f, deskMat);
            commandDesk.name = "CommandDesk";
            commandDesk.transform.SetParent(room.transform);
            commandDesk.transform.localPosition = new Vector3(0f, 0f, roomDepth * 0.5f - 1.8f);
            AddNameplate(commandDesk, "🪑 지휘 책상");

            // 지휘관 의자 (책상 뒤에서 방 중앙을 향함)
            GameObject commandChair = IndoorFurniturePlacer.CreateChair(1.1f, deskMat);
            commandChair.name = "CommandChair";
            commandChair.transform.SetParent(room.transform);
            commandChair.transform.localPosition = new Vector3(0f, 0f, roomDepth * 0.5f - 2.9f);

            // 책상 위 문서들
            CreateBoxPrimitive(room, "DeskDocument_1", new Vector3(0.35f, 0.02f, 0.25f),
                new Vector3(-0.8f, 1.12f, roomDepth * 0.5f - 1.7f), paperMat);
            CreateBoxPrimitive(room, "DeskDocument_2", new Vector3(0.35f, 0.02f, 0.25f),
                new Vector3(-0.4f, 1.12f, roomDepth * 0.5f - 1.5f), paperMat);
            CreateBoxPrimitive(room, "DeskInkwell", new Vector3(0.08f, 0.12f, 0.08f),
                new Vector3(0.9f, 1.17f, roomDepth * 0.5f - 1.6f), standMat);

            // 관리용 사이드 책상 (집무실 보조) + 문서 더미
            GameObject adminDesk = IndoorFurniturePlacer.CreateTable(1.8f, 0.9f, 1.0f, deskMat);
            adminDesk.name = "AdminDesk";
            adminDesk.transform.SetParent(room.transform);
            adminDesk.transform.localPosition = new Vector3(3.2f, 0f, roomDepth * 0.5f - 1.6f);
            AddNameplate(adminDesk, "📜 관리 사무소");

            CreateBoxPrimitive(room, "AdminPaperStack_1", new Vector3(0.30f, 0.06f, 0.22f),
                new Vector3(2.9f, 1.04f, roomDepth * 0.5f - 1.5f), paperMat);
            CreateBoxPrimitive(room, "AdminPaperStack_2", new Vector3(0.30f, 0.06f, 0.22f),
                new Vector3(3.5f, 1.04f, roomDepth * 0.5f - 1.7f), paperMat);

            // 중앙 작전 회의 테이블 + 의자 2개
            GameObject planningTable = IndoorFurniturePlacer.CreateTable(3.0f, 1.6f, 1.0f, deskMat);
            planningTable.name = "PlanningTable";
            planningTable.transform.SetParent(room.transform);
            planningTable.transform.localPosition = new Vector3(0f, 0f, 0.5f);

            CreateBoxPrimitive(room, "PlanningMap", new Vector3(0.8f, 0.02f, 0.6f),
                new Vector3(0f, 1.02f, 0.5f), bannerMat); // 작전 지도

            for (int side = -1; side <= 1; side += 2)
            {
                GameObject planChair = IndoorFurniturePlacer.CreateChair(1.0f, deskMat);
                planChair.name = $"PlanningChair_{side}";
                planChair.transform.SetParent(room.transform);
                planChair.transform.localPosition = new Vector3(side * 1.0f, 0f, 0.5f);
                // 테이블 중앙을 향하도록 회전
                planChair.transform.localRotation = Quaternion.Euler(0f, side > 0 ? -90f : 90f, 0f);
            }

            // ===================================================================
            // 2. 플레이어 환영 배너 (뒷벽 위 — 점령지 표시)
            // ===================================================================
            GameObject bannerRoot = new GameObject("PlayerBanner");
            bannerRoot.transform.SetParent(room.transform);
            bannerRoot.transform.localPosition = new Vector3(0f, 0f, roomDepth * 0.5f - 0.45f);

            // 깃대 2개
            CreateCylinderPrimitive(bannerRoot, "BannerPole_Left", 0.05f, 4.6f,
                new Vector3(-1.9f, 2.3f, 0f), standMat);
            CreateCylinderPrimitive(bannerRoot, "BannerPole_Right", 0.05f, 4.6f,
                new Vector3(1.9f, 2.3f, 0f), standMat);

            // 가로 바
            CreateBoxPrimitive(bannerRoot, "BannerBar", new Vector3(3.8f, 0.06f, 0.06f),
                new Vector3(0f, 4.5f, 0f), standMat);

            // 깃천 (플레이어 청색) + 금장 트림 + 문장
            CreateBoxPrimitive(bannerRoot, "BannerCloth", new Vector3(2.6f, 1.7f, 0.04f),
                new Vector3(0f, 3.4f, 0f), bannerMat);
            CreateBoxPrimitive(bannerRoot, "BannerTrim_Top", new Vector3(2.6f, 0.08f, 0.05f),
                new Vector3(0f, 4.2f, -0.03f), trimMat);
            CreateBoxPrimitive(bannerRoot, "BannerTrim_Bottom", new Vector3(2.6f, 0.08f, 0.05f),
                new Vector3(0f, 2.6f, -0.03f), trimMat);
            CreateBoxPrimitive(bannerRoot, "BannerEmblem", new Vector3(0.5f, 0.5f, 0.05f),
                new Vector3(0f, 3.4f, -0.06f), trimMat);
            AddNameplate(bannerRoot, "🚩 플레이어 영지");

            // ===================================================================
            // 3. 저장고 (왼쪽 벽 — 선반 + 상자)
            // ===================================================================
            GameObject storageShelf1 = IndoorFurniturePlacer.CreateShelf(2.2f, 2.2f, 0.6f, shelfMat, 3);
            storageShelf1.name = "StorageShelf_1";
            storageShelf1.transform.SetParent(room.transform);
            storageShelf1.transform.localPosition = new Vector3(-roomWidth * 0.5f + 0.4f, 0f, 2.0f);
            storageShelf1.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            GameObject storageShelf2 = IndoorFurniturePlacer.CreateShelf(2.2f, 2.2f, 0.6f, shelfMat, 3);
            storageShelf2.name = "StorageShelf_2";
            storageShelf2.transform.SetParent(room.transform);
            storageShelf2.transform.localPosition = new Vector3(-roomWidth * 0.5f + 0.4f, 0f, -1.5f);
            storageShelf2.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            // Phase B: 저장고 상호작용 — StorageShelf_2 기준점에 TerritoryWarehouse 부착.
            // E키 근접 상호작용으로 창고 UI를 열며, 실제 데이터는 WarehouseSystem(영지 키)에 위임.
            // Systems asmdef는 UI asmdef를 참조할 수 없으므로(순환 참조) 리플렉션으로 부착.
            AttachUiComponent(storageShelf2, TerritoryWarehouseTypeName, territoryKey);

            // 창고 상자 더미 (큐브 프리미티브)
            CreateBoxPrimitive(room, "StorageCrate_1", new Vector3(0.7f, 0.7f, 0.7f),
                new Vector3(-9.6f, 0.35f, 4.2f), crateMat);
            CreateBoxPrimitive(room, "StorageCrate_2", new Vector3(0.7f, 0.7f, 0.7f),
                new Vector3(-9.6f, 0.35f, 5.1f), crateMat);
            CreateBoxPrimitive(room, "StorageCrate_3", new Vector3(0.7f, 0.7f, 0.7f),
                new Vector3(-9.6f, 1.05f, 4.65f), crateMat);
            CreateBoxPrimitive(room, "StorageCrate_4", new Vector3(0.9f, 0.9f, 0.9f),
                new Vector3(-8.7f, 0.45f, 5.8f), crateMat);
            CreateBoxPrimitive(room, "StorageCrate_5", new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(-9.7f, 0.25f, 6.3f), crateMat);

            // 저장고 통 (Cylinder)
            CreateCylinderPrimitive(room, "StorageBarrel", 0.4f, 1.0f,
                new Vector3(-8.9f, 0.5f, 3.0f), crateMat);

            // 저장고 안내 팻말 (왼쪽 벽)
            GameObject storageSign = CreateBoxPrimitive(room, "StorageSign", new Vector3(0.05f, 0.7f, 2.4f),
                new Vector3(-roomWidth * 0.5f + 0.1f, 2.5f, 0.25f), bannerMat);
            AddNameplate(storageSign, "🎒 저장고");

            // ===================================================================
            // 4. 무기고 (오른쪽 벽 — 무기 스탠드 3개 + 벽걸이 무기고)
            // ===================================================================
            for (int i = 0; i < 3; i++)
            {
                float zPos = 3.0f - i * 3.0f;
                CreateWeaponStand(room, $"WeaponStand_{i}",
                    new Vector3(roomWidth * 0.5f - 0.6f, 0f, zPos), -90f, standMat, bladeMat);
            }

            // 벽걸이 무기고 (오른쪽 벽 앞쪽)
            GameObject wallRack = CreateBoxPrimitive(room, "WeaponWallRack", new Vector3(0.08f, 1.8f, 2.0f),
                new Vector3(roomWidth * 0.5f - 0.15f, 1.6f, -5.5f), standMat);
            if (wallRack != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    float bladeZ = -6.1f + i * 0.4f;
                    CreateBoxPrimitive(room, $"WallWeapon_{i}", new Vector3(0.05f, 1.2f, 0.14f),
                        new Vector3(roomWidth * 0.5f - 0.22f, 1.5f, bladeZ), bladeMat);
                }
            }

            // 무기고 안내 팻말 (오른쪽 벽)
            GameObject armorySign = CreateBoxPrimitive(room, "ArmorySign", new Vector3(0.05f, 0.7f, 2.4f),
                new Vector3(roomWidth * 0.5f - 0.1f, 2.6f, 0f), bannerMat);
            AddNameplate(armorySign, "⚔️ 무기고");

            // ===================================================================
            // 5. 작업대 (앞벽 왼쪽 — 제작/수리 공간)
            // ===================================================================
            GameObject workbench = IndoorFurniturePlacer.CreateCounter(2.6f, 1.0f, 1.0f, workbenchMat);
            workbench.name = "Workbench";
            workbench.transform.SetParent(room.transform);
            workbench.transform.localPosition = new Vector3(-5f, 0f, -roomDepth * 0.5f + 0.6f);
            AddNameplate(workbench, "🛠️ 작업대");

            // Phase A: 작업대 상호작용 — TerritoryCraftingStation 부착 (E키 → CraftingUI).
            // NameplateDisplay("🛠️ 작업대")는 유지. 프로젝트 ProjectSettings의 activeInputHandler가
            // 2(Both)로 확인되어 InputSystem(Keyboard.current) 기반 상호작용 동작 가능.
            // 참고: CraftingUI 프리팹이 씬/UIManager에 없으면 창이 열리지 않음(기존 인프라 동작, 범위 밖).
            // Systems asmdef는 UI asmdef를 참조할 수 없으므로(순환 참조) 리플렉션으로 부착.
            AttachUiComponent(workbench, TerritoryCraftingStationTypeName, territoryKey, "영지 작업대");

            // 작업대 위 도구들 (모루 + 공구)
            CreateBoxPrimitive(room, "WorkbenchAnvil", new Vector3(0.5f, 0.25f, 0.35f),
                new Vector3(-5.6f, 1.13f, -roomDepth * 0.5f + 0.6f), bladeMat);
            CreateBoxPrimitive(room, "WorkbenchTool_1", new Vector3(0.12f, 0.12f, 0.30f),
                new Vector3(-4.5f, 1.07f, -roomDepth * 0.5f + 0.45f), standMat);
            CreateBoxPrimitive(room, "WorkbenchTool_2", new Vector3(0.12f, 0.12f, 0.30f),
                new Vector3(-4.3f, 1.07f, -roomDepth * 0.5f + 0.75f), standMat);

            // 작업대 안내 팻말 (앞벽)
            GameObject workbenchSign = CreateBoxPrimitive(room, "WorkbenchSign", new Vector3(1.6f, 0.6f, 0.05f),
                new Vector3(-5f, 2.4f, -roomDepth * 0.5f + 0.1f), bannerMat);
            AddNameplate(workbenchSign, "🛠️ 작업대");

            // ===================================================================
            // 6. 중세 석재 기둥 2열 (대전당 느낌 — 중앙 복도 양쪽)
            //    x=±4 (벽 x=±11 안쪽), z=-5..5에 4개씩 — 가구와 겹치지 않게 배치
            // ===================================================================
            for (int side = -1; side <= 1; side += 2)
            {
                for (int pi = 0; pi < 4; pi++)
                {
                    float pillarZ = -5f + pi * (10f / 3f); // -5, -1.67, 1.67, 5
                    GameObject pillarRoot = new GameObject($"StonePillar_{(side > 0 ? "R" : "L")}{pi}");
                    pillarRoot.transform.SetParent(room.transform);
                    pillarRoot.transform.localPosition = new Vector3(side * 4f, 0f, pillarZ);

                    // 기둥 받침 (석재 베이스)
                    CreateBoxPrimitive(pillarRoot, "Base", new Vector3(1.0f, 0.3f, 1.0f),
                        new Vector3(0f, 0.15f, 0f), pillarMat);
                    // 몸통 (Cylinder)
                    CreateCylinderPrimitive(pillarRoot, "Shaft", 0.35f, 5.4f,
                        new Vector3(0f, 3.0f, 0f), pillarMat);
                    // 머리 받침 (캐피털 — 천장 바로 아래)
                    CreateBoxPrimitive(pillarRoot, "Capital", new Vector3(1.0f, 0.3f, 1.0f),
                        new Vector3(0f, 5.75f, 0f), pillarMat);
                }
            }

            // ===================================================================
            // 7. 화로/토치 (입구 양쪽 벽 — 중세 성의 따스함)
            // ===================================================================
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject hearthRoot = new GameObject($"Hearth_{(side > 0 ? "Right" : "Left")}");
                hearthRoot.transform.SetParent(room.transform);
                hearthRoot.transform.localPosition = new Vector3(side * 10.0f, 0f, -4.5f);

                // 화로 받침 (짙은 회색 석재 통)
                CreateCylinderPrimitive(hearthRoot, "BrazierBowl", 0.55f, 0.9f,
                    new Vector3(0f, 0.45f, 0f), hearthMat);
                // 붉은 숯 (불멍 느낌)
                CreateCylinderPrimitive(hearthRoot, "BrazierCoals", 0.35f, 0.25f,
                    new Vector3(0f, 0.95f, 0f), rugMat);
                AddNameplate(hearthRoot, "🔥 화로");

                // 화로 불빛 (따뜻한 주황빛)
                IndoorLighting.AddPointLight(room,
                    new Vector3(side * 10.0f, 1.5f, -4.5f),
                    new Color(1f, 0.55f, 0.25f), 7f, 0.9f);
            }

            // 앞쪽 기둥 토치 빛 (입구 쪽, 기둥 옆 은은한 주황빛)
            IndoorLighting.AddPointLight(room, new Vector3(-4.55f, 4.5f, -5f), new Color(1f, 0.6f, 0.3f), 6f, 0.7f);
            IndoorLighting.AddPointLight(room, new Vector3(4.55f, 4.5f, -5f), new Color(1f, 0.6f, 0.3f), 6f, 0.7f);

            // ===================================================================
            // 8. 중세 장식 — 왕실 문장 방패 + 붉은 러그 (기존 청색 배너는 유지)
            // ===================================================================
            // 뒷벽 문장 방패 2개 (배너 양옆 — 왕실/영지 상징)
            for (int sx = -1; sx <= 1; sx += 2)
            {
                string shieldSide = sx > 0 ? "R" : "L";
                CreateBoxPrimitive(room, $"HeraldicShield_Back{shieldSide}",
                    new Vector3(0.7f, 0.85f, 0.06f),
                    new Vector3(sx * 4.5f, 3.0f, roomDepth * 0.5f - 0.04f), trimMat);
                // 방패 안쪽 붉은 자수 문장
                CreateBoxPrimitive(room, $"HeraldicEmblem_Back{shieldSide}",
                    new Vector3(0.35f, 0.45f, 0.04f),
                    new Vector3(sx * 4.5f, 3.0f, roomDepth * 0.5f - 0.10f), rugMat);
            }

            // 화로 위 좌/우벽 문장 방패 2개
            for (int sx = -1; sx <= 1; sx += 2)
            {
                string shieldSide = sx > 0 ? "R" : "L";
                CreateBoxPrimitive(room, $"HeraldicShield_Wall{shieldSide}",
                    new Vector3(0.06f, 0.85f, 0.7f),
                    new Vector3(sx * (roomWidth * 0.5f - 0.04f), 2.9f, -4.5f), trimMat);
                CreateBoxPrimitive(room, $"HeraldicEmblem_Wall{shieldSide}",
                    new Vector3(0.04f, 0.45f, 0.35f),
                    new Vector3(sx * (roomWidth * 0.5f - 0.10f), 2.9f, -4.5f), rugMat);
            }

            // 바닥 러그 (붉은 카펫 — 바닥에서 0.005 위 부착, z-fighting 방지)
            CreateBoxPrimitive(room, "Rug_CentralAisle", new Vector3(2.8f, 0.03f, 6.0f),
                new Vector3(0f, 0.02f, -3.5f), rugMat);
            CreateBoxPrimitive(room, "Rug_Throne", new Vector3(4.0f, 0.03f, 2.4f),
                new Vector3(0f, 0.02f, 4.2f), rugMat);

            // ===================================================================
            // 9. 조명 — 중세 화로/토치의 따뜻한 주황빛 (촛불빛 톤, 은은한 점멸)
            // ===================================================================
            Color ambient = new Color(0.22f, 0.18f, 0.15f); // 살짝 어둡고 따뜻한 중세 톤
            IndoorLighting.SetupIndoorLighting(room, ambient, 1.15f, true); // 화로 깜빡임(점멸) — 과하지 않게

            // 천장 중앙 메인 조명 (촛불빛톤)
            IndoorLighting.AddPointLight(room,
                new Vector3(0f, roomHeight - 0.6f, 0f),
                new Color(1f, 0.9f, 0.7f), 18f, 1.2f);

            // 지휘 책상 위 조명 (촛불톤)
            IndoorLighting.AddPointLight(room,
                new Vector3(0f, 4.2f, roomDepth * 0.5f - 1.8f),
                new Color(1f, 0.85f, 0.6f), 9f, 1.0f);

            // 작업대 위 조명
            IndoorLighting.AddPointLight(room,
                new Vector3(-5f, 3.8f, -roomDepth * 0.5f + 0.8f),
                new Color(1f, 0.9f, 0.7f), 7f, 0.9f);

            // 저장고 조명
            IndoorLighting.AddPointLight(room,
                new Vector3(-9.5f, 3.8f, 0.5f),
                new Color(1f, 0.88f, 0.65f), 8f, 0.8f);

            // 무기고 조명
            IndoorLighting.AddPointLight(room,
                new Vector3(9.5f, 3.8f, 0.5f),
                new Color(1f, 0.88f, 0.65f), 8f, 0.8f);

            Debug.Log($"[PlayerCastleInteriorBuilder] 플레이어 소유 중세 판타지 성 내부 생성 완료! (스타일: {nationStyle}) — 석재 기둥 2열·화로 2기·문장 방패·러그 장식 포함");
            return room;
        }

        // ===================================================================
        // 프라이빗 헬퍼 (기존 빌더 클래스에는 손대지 않음 — 이 클래스 내부 전용)
        // ===================================================================

        /// <summary>ProjectName.UI 어셈블리 상호작용 컴포넌트의 어셈블리 정식 타입 이름 (리플렉션 전용).</summary>
        private const string TerritoryCraftingStationTypeName =
            "ProjectName.UI.TerritoryCraftingStation, ProjectName.UI";
        private const string TerritoryWarehouseTypeName =
            "ProjectName.UI.TerritoryWarehouse, ProjectName.UI";

        /// <summary>
        /// 어셈블리 경계(ProjectName.UI)를 넘어 상호작용 컴포넌트를 리플렉션으로 부착한다.
        /// Systems asmdef가 UI asmdef를 참조하면 순환 참조(UI → Systems)가 되어 불가능하므로,
        /// 컴파일 타임 참조 없이 어셈블리 정식 타입 이름으로 타입을 찾아 AddComponent(Type) 후
        /// Configure를 호출한다. 타입/메서드를 못 찾으면 경고만 남기고 컴포넌트 없이 진행.
        /// </summary>
        /// <param name="target">컴포넌트를 부착할 GameObject</param>
        /// <param name="assemblyQualifiedTypeName">
        /// 예: "ProjectName.UI.TerritoryWarehouse, ProjectName.UI" (asmdef name과 정확히 일치)
        /// </param>
        /// <param name="configureArgs">Configure에 전달할 앞쪽 인자들 (나머지는 선언된 기본값 사용)</param>
        private static void AttachUiComponent(GameObject target, string assemblyQualifiedTypeName,
            params object[] configureArgs)
        {
            if (target == null) return;

            System.Type uiType = System.Type.GetType(assemblyQualifiedTypeName);
            if (uiType == null)
            {
                Debug.LogWarning($"[PlayerCastleInteriorBuilder] '{assemblyQualifiedTypeName}' 타입을 찾지 못함 (ProjectName.UI 어셈블리 미로드?). 상호작용 컴포넌트 없이 진행.");
                return;
            }

            Component component = target.AddComponent(uiType);
            if (component == null)
            {
                Debug.LogWarning($"[PlayerCastleInteriorBuilder] '{uiType.Name}' AddComponent 실패. 상호작용 컴포넌트 없이 진행.");
                return;
            }

            System.Reflection.MethodInfo configure = uiType.GetMethod("Configure");
            if (configure == null)
            {
                Debug.LogWarning($"[PlayerCastleInteriorBuilder] '{uiType.Name}'.Configure 메서드를 찾지 못함 — 기본값 상태로 부착만 진행.");
                return;
            }

            // MethodInfo.Invoke는 C# 선택적 매개변수 기본값을 채워주지 않으므로
            // 전달되지 않은 뒤쪽 인자를 선언된 기본값(예: maxSlots=20, interactRange=null)으로 채운다.
            System.Reflection.ParameterInfo[] parameters = configure.GetParameters();
            var invokeArgs = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i < configureArgs.Length && configureArgs[i] != null)
                {
                    invokeArgs[i] = configureArgs[i];
                }
                else if (parameters[i].HasDefaultValue)
                {
                    invokeArgs[i] = parameters[i].DefaultValue;
                }
                else
                {
                    invokeArgs[i] = parameters[i].ParameterType.IsValueType
                        ? System.Activator.CreateInstance(parameters[i].ParameterType)
                        : null;
                }
            }

            try
            {
                configure.Invoke(component, invokeArgs);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PlayerCastleInteriorBuilder] '{uiType.Name}'.Configure 호출 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 국가 스타일 → 영지 고유 키 매핑 (작업대/저장고 상호작용 설정용).
        /// 알 수 없는 스타일은 빌더 본문 switch에서 이미 경고하므로 여기서는 조용히 기본값 사용.
        /// </summary>
        private static string GetTerritoryKeyForNationStyle(string nationStyle)
        {
            switch (nationStyle?.ToLower())
            {
                case "eastern": return "East_01";
                case "western": return "West_01";
                case "southern": return "South_01";
                case "northern": return "North_01";
                case "empire": return "Empire_01";
                default: return "Empire_01";
            }
        }

        /// <summary>Cube 프리미티브 생성 + 재질 적용.</summary>
        private static GameObject CreateBoxPrimitive(GameObject parent, string name, Vector3 size,
            Vector3 localPosition, Material mat)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent.transform);
            box.transform.localPosition = localPosition;
            box.transform.localScale = size;

            var renderer = box.GetComponent<MeshRenderer>();
            if (renderer != null && mat != null)
            {
                renderer.sharedMaterial = mat;
            }
            return box;
        }

        /// <summary>Cylinder 프리미티브 생성 + 재질 적용 (기둥/깃대/통).</summary>
        private static GameObject CreateCylinderPrimitive(GameObject parent, string name, float radius,
            float height, Vector3 localPosition, Material mat)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.SetParent(parent.transform);
            cylinder.transform.localPosition = localPosition;
            // Unity Cylinder: scale.y = height * 0.5, scale.xz = radius * 2
            cylinder.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);

            var renderer = cylinder.GetComponent<MeshRenderer>();
            if (renderer != null && mat != null)
            {
                renderer.sharedMaterial = mat;
            }
            return cylinder;
        }

        /// <summary>
        /// 무기 스탠드 생성: 받침대 + 세움기둥 + 가로 바 + 걸린 무기(blade) 4개.
        /// rotationY로 벽을 따라 회전 배치.
        /// </summary>
        private static void CreateWeaponStand(GameObject parent, string name, Vector3 position,
            float rotationY, Material standMat, Material bladeMat)
        {
            GameObject stand = new GameObject(name);
            stand.transform.SetParent(parent.transform);
            stand.transform.localPosition = position;
            stand.transform.localRotation = Quaternion.Euler(0f, rotationY, 0f);

            // 받침대
            CreateBoxPrimitive(stand, "Base", new Vector3(0.9f, 0.1f, 0.5f),
                new Vector3(0f, 0.05f, 0f), standMat);

            // 세움 기둥 (Cylinder)
            CreateCylinderPrimitive(stand, "Pole", 0.04f, 1.6f,
                new Vector3(0f, 0.85f, 0f), standMat);

            // 가로 바 (무기 걸이)
            CreateBoxPrimitive(stand, "Crossbar", new Vector3(0.8f, 0.06f, 0.06f),
                new Vector3(0f, 1.55f, 0f), standMat);

            // 걸린 무기 4개 (얇은 세로 박스 — 검/창 느낌)
            for (int i = 0; i < 4; i++)
            {
                float xOffset = -0.3f + i * 0.2f;
                CreateBoxPrimitive(stand, $"Weapon_{i}", new Vector3(0.05f, 0.9f, 0.12f),
                    new Vector3(xOffset, 1.05f, 0f), bladeMat);

                // 칼등 가드
                CreateBoxPrimitive(stand, $"WeaponGuard_{i}", new Vector3(0.12f, 0.05f, 0.16f),
                    new Vector3(xOffset, 1.45f, 0f), standMat);
            }
        }

        /// <summary>NameplateDisplay 부착 (잠금문 없음 — 기능 안내 전용 라벨).</summary>
        private static void AddNameplate(GameObject target, string label)
        {
            if (target == null) return;

            var nameplate = target.AddComponent<NameplateDisplay>();
            nameplate.DisplayName = label;
        }
    }
}
