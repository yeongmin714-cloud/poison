using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [81차] Test_15 마을+음식점 마약 경영 검증 씬 — 소스 문자열 어서션(진실치 마커 검증).
    /// ①메인씬 동일 경로 재사용 계약(BuildSingleVillage=BuildVillage / SpawnForVillage=SpawnVillageNPCs)
    /// ②음식점 실제 건설 경로(시드→TryPlace→ForceComplete→StructureCompleted) + 실패 폴백
    /// ③재고/등록가/판매 담당 병사 시딩 ④판매틱 배선(OnDayStart ticker + Y키 숏컷 — T키 충돌 회피)
    /// </summary>
    public sealed class Test15VillageDrugTests
    {
        private static string Read(string rel) => System.IO.File.ReadAllText(rel);

        [Test]
        public void VillageBuilder_Has_Single_Village_Entry_Using_Same_Path()
        {
            var src = Read("Assets/Scripts/Systems/VillageBuilder.cs");
            Assert.That(src, Does.Contain("public static bool BuildSingleVillage(VillagePlacementSystem.VillageInfo village)"),
                "[81차] 단일 마을 진입점(테스트씬용)");
            Assert.That(src, Does.Contain("int layoutHash = BuildVillage(rootGO.transform, village, out bool shopPlaced);"),
                "[81차] 메인씬과 동일 BuildVillage 경로 재사용 — 결정론 레이아웃 동일성");
        }

        [Test]
        public void VillageNpcSpawner_Has_Single_Village_Entry_Using_Same_Path()
        {
            var src = Read("Assets/Scripts/Systems/VillageNpcSpawner.cs");
            Assert.That(src, Does.Contain("public static bool SpawnForVillage(NationType nation, int villageIndex)"),
                "[81차] 단일 마을 주민 진입점(테스트씬용)");
            Assert.That(src, Does.Contain("int count = SpawnVillageNPCs(rootGO.transform, villages[villageIndex], out bool shopKeeper);"),
                "[81차] 메인씬과 동일 SpawnVillageNPCs 경로 재사용");
            Assert.That(src, Does.Contain("이미 존재 — 단일 스폰 스킵 (멱등)"),
                "[81차] 멱등 가드 — 중복 스폰 방지");
        }

        [Test]
        public void Test15Setup_Restaurant_StartsEmpty_ResetContract_116R3()
        {
            var src = Read("Assets/Scripts/Systems/TestTerritoryCombatSetup.cs");
            Assert.That(src, Does.Contain("SetupTest15VillageDrugEconomy"),
                "[81차] Test_15 마을+마약 경영 셋업 존재");
            // [116차 R3] 프리빌드 폐기 — "처음엔 없음"(사용자 지시). U키 홀로그램으로 직접 건설 검증.
            Assert.That(src, Does.Contain("VillageDrugState.SetRestaurantBuilt(best.nation, best.index, false);"),
                "[116차 R3] 시작 시 음식점 플래그 강제 리셋(세이브 복원분 포함)");
            Assert.That(src, Does.Contain("ConstructionManager.Instance.TryRemoveStructure(e.structureId)")
                .And.Contain("RestaurantPropBuilder.TryDestroyProp(e.structureId);"),
                "[116차 R3] 잔존 완료 구조물/실물 정리 — RebuildFromSave 재생성 차단");
            Assert.That(src, Does.Contain("RestaurantPropBuilder.RebuildFromSave();"),
                "[106차] 순서 계약 유지(구조물 제거 후 무동작 — 멱등)");
            Assert.That(src, Does.Not.Contain("ForceComplete(targetId);"),
                "[116차 R3] 자동 건설 경로(TryPlace→ForceComplete) 폐기 — 구 82차 계약 진화");
        }

        [Test]
        public void Test15Setup_Seeds_Stock_Price_And_Guard()
        {
            var src = Read("Assets/Scripts/Systems/TestTerritoryCombatSetup.cs");
            Assert.That(src, Does.Contain("foodItemId = \"drugged_cook_01\"")
                .And.Contain("drugStage = 3").And.Contain("basePrice = 30"),
                "[81차] 마약 요리 시딩(3단계/기본가 30)");
            Assert.That(src, Does.Contain("VillageDrugState.SetRegisteredPrice(best.nation, best.index, 30);"),
                "[81차] 등록가 = 권장가(기본가) 시작 — 수요 팩터 1.0");
            Assert.That(src, Does.Contain("VillageDrugState.AssignGuard(best.nation, best.index, stableId);"),
                "[81차] 판매 담당 병사 배치(판매 스킵 게이트 해소)");
        }

        [Test]
        public void Test15Setup_Wires_Day_Tick_And_Avoids_T_Key_Conflict()
        {
            var src = Read("Assets/Scripts/Systems/TestTerritoryCombatSetup.cs");
            Assert.That(src, Does.Contain("TimeManager.GetOrCreate();")
                .And.Contain("AddComponent<RestaurantDailyTicker>();"),
                "[81차] OnDayStart → RunAllDaily 프로덕션 틱 배선");
            Assert.That(src, Does.Contain("kb.yKey.wasPressedThisFrame"),
                "[81차] Y키 숏컷 — 일일 판매 틱 1회");
            Assert.That(src, Does.Not.Contain("kb.tKey.wasPressedThisFrame"),
                "[81차] T키 금지 — TutorialGuideUTK 토글 충돌(실측) 회피");
            Assert.That(src, Does.Contain("RestaurantSystem.RunAllDaily(day);")
                .And.Contain("VillagerDepartureSystem.RunDailyDeparture(day);"),
                "[81차] 숏컷 = 판매+이탈 판정 동일 틱");
        }

        [Test]
        public void Test15_Builds_Local_Ground_Patch_And_Moves_Player_To_Village()
        {
            var src = Read("Assets/Scripts/Systems/TestTerritoryCombatSetup.cs");
            // [147차] 패치 유지 판정 반영 — P0Time 래핑(호출 유지, 정점 전부 동일 y 평지 정합 — §6-3 ③)
            Assert.That(src, Does.Contain("P0Time(\"지면패치\", () => BuildVillageGroundPatch(best.center, 140));"),
                "[147차] 지면패치 유지 판정 + P0 계측 래핑 — [86차] 스폰 ±60m 지형 밖 마을 소실 수리 승계");
            Assert.That(src, Does.Contain("if (GameObject.Find(\"Ground_Village\") != null) return;"),
                "[86차] 패치 멱등 가드(SetupGround Ground 가드와 독립)");
            Assert.That(src, Does.Contain("playerGO.transform.position = edge;"),
                "[86차] 플레이어 마을 가장자리 이동 — 도보 455m 제거");
            Assert.That(src, Does.Contain("edge.y = SurfaceY(edge.x, edge.z);"),
                "[86차] 이동 후 접지 = GetHeightAt+1 계약");
        }

        [Test]
        public void VillageFence_WoodenGlb_Ring_In_BuildVillage()
        {
            var src = Read("Assets/Scripts/Systems/VillageBuilder.cs");
            Assert.That(src, Does.Contain("BuildVillageFence(root, village);"),
                "[86차] 울타리 링 — BuildVillage 말단 호출(메인씬/단일 마을 공용)");
            Assert.That(src, Does.Contain("float fenceRadius = village.radius + 3f;"),
                "[86차] 울타리 반경 = 마을 반경+3m");
            Assert.That(src, Does.Contain("Fence_probe"),
                "[86차] 프로브 1개 세그먼트 실측(장축/길이) — 결정론 개수 산출");
            var catalog = Read("Assets/Scripts/Systems/VillageBuildingCatalog.cs");
            Assert.That(catalog, Does.Contain("Assets/새로운 glb/fence/common-fences-split-log-ranch-fence-straight-normal.glb"),
                "[86차] 나무 펜스 GLB(사용자 지정 신규 에셋) 경로");
        }

        [Test]
        public void Test15_VillagerSpawn_Removed_ShopOnly_148()
        {
            var src = Read("Assets/Scripts/Systems/TestTerritoryCombatSetup.cs");
            // [148차] 주민 스폰 제거 계약 — Awake 15.5s 절감(147차 P0 실측 66%, 계획 §1-1)
            Assert.That(src, Does.Not.Contain("P0Time(\"주민스폰\""),
                "[148차] 주민스폰 P0 스테이지 소멸 — Test_15 경량화(주민 10명 스폰 제거)");
            Assert.That(src, Does.Not.Contain("VillageNpcSpawner.SpawnForVillage("),
                "[148차] Test_15 셋업에서 주민 스폰 경로 완전 제거 — VillageNpcSpawner 클래스 자체는 무수정(메인씬 경로 유지)");
            // 상점NPC 유지 계약 — 경량화 후 최대 항목(4.9s, 사용자 지시 유지 · 계획 §3/§6)
            Assert.That(src, Does.Contain("SpawnVillageNpc(\"test15_shopkeeper\""),
                "[148차] 상점NPC(test15_shopkeeper) 유지 — 세금제안 진입점 경로 보존");
            Assert.That(src, Does.Contain("P0Time(\"상점NPC\""),
                "[148차] 상점NPC P0 스테이지 유지 — 경량화 후 최대 항목 계측 유지");
        }

        [Test]
        public void Test15_TestScene_Seeding_Contract_149()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/TestTerritoryCombatSetup.cs");
            // [149차 P1] 농경 아군 영지 — 프로덕션 상태 API로 East_01 소유 시딩(Test_10/14 선례, Test_15 전용).
            Assert.That(src, Does.Contain("TerritoryDatabase.Instance.SetOwnership(NationType.East, 1, TerritoryOwnership.PlayerOwned)"),
                "[149차 P1] 밭 영지(East#1) 소유 시딩 — FarmPlot ValidateOwnership 통과 계약.");
            // [149차 P5] 음식점 검증 시딩 — 좌측 "판매 가능한 음식" 그리드 소스(PlayerInventory Food/Drug)에 실제 음식 지급.
            Assert.That(src, Does.Contain("id = \"cook_01\""), "[149차 P5] 일반 요리 인벤토리 시딩.");
            Assert.That(src, Does.Contain("id = \"drugged_cook_01\""), "[149차 P5] 마약 요리 인벤토리 시딩.");
            Assert.That(src, Does.Contain("[마약: stage 3]"), "[149차 P5] 약 단계 마커 — ParseDrugStage 병합 정합.");
            // [149차 P5] 고객 NPC — RestaurantVisitorSystem 스캔 계약 루트(Village_{nation}_{index:D2}) + 전용 주민 1명.
            Assert.That(src, Does.Contain("$\"Village_{best.nation}_{best.index:D2}\""), "[149차 P5] 스캔 계약명 루트 생성.");
            Assert.That(src, Does.Contain("VillageNpcSpawner.SpawnSingleVillager"), "[149차 P5] 고객 NPC 단독 스폰.");
            Assert.That(src, Does.Contain("\"Test15Customer\""), "[149차 P5] 고객 NPC 이름 계약.");
        }

        [Test]
        public void Test15_Livelihood_Pond_Restored_CompactCluster_151()
        {
            var src = Read("Assets/Scripts/Systems/TestTerritoryCombatSetup.cs");
            // [151차 P4] 호수 1개 Test_15 복귀 — 142차 블록(SetupLivelihoodNatureCluster) 복원 이식.
            //  물뿌리개 "호수에서 충전하세요" 안내와 실제 호수 정합(148차 분리 부분 복원 — 나무/돌은 Test_20 유지).
            Assert.That(src, Does.Contain("waterDisc.name = \"Livelihood_Pond\";"),
                "[151차 P4] 호수 원판 이름 계약 — CompactCluster 복원 블록 포함(NatureCluster 원본 유지)");
            Assert.That(src, Does.Contain("🌊 호수(Water) 배치[151차 P4]"),
                "[151차 P4] 호수 배치 로그 — 복원 블록 마커 갱신 확인");
            Assert.That(src, Does.Contain("try { waterDisc.tag = \"Water\"; }"),
                "[151차 P4] Water 태그 부여 유지 — FishingSystem IsNearWater(CompareTag(\"Water\")) → 물뿌리개 리필 경로 인식");
            // 나무/돌 분리 계약 유지 확인(부분 복원만) + 구 로그 소멸
            Assert.That(src, Does.Contain("[148차] dR 계산은 SetupLivelihoodNatureCluster로 이식(나무/돌 블록 전용)"),
                "[148차] 나무/돌 Test_20 분리 계약 유지 — [151차 P4]는 호수만 복귀");
            Assert.That(src, Does.Contain("나무/돌은 NatureCluster(Test_20) 분리 유지"),
                "[151차 P4] 완료 로그 — 호수 복귀 + 나무/돌 분리 유지 병기");
            Assert.That(src, Does.Not.Contain("컴팩트 클러스터 완료[148차 농경 전용]"),
                "[151차 P4] 구 완료 로그(호수 분리 표기) 소멸 — 계약 갱신");
        }
    }
}
