using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 32] 영지 실내 타일 씬 통일 계약 — "같은 씬 내 방/구역마다 타일이 나뉘면 통일성이 없다"(사용자 지시).
    /// 전 영지 실내 빌더(성/집/공방/교회/상점/외양간/주점/식당) 바닥 = InteriorQualityBaseline 공통 석타일 단일 소스.
    /// 던전(156차 흙 단일화 계약 승계)·이계·아레나는 제외(특수 씬 정체성 유지).
    /// </summary>
    public class InteriorTileUnificationTests
    {
        private static string Read(string p) => System.IO.File.ReadAllText(p);

        // ── 공통 석타일 단일 소스(InteriorQualityBaseline) ──
        [Test]
        public void Source_32_CommonFloorHelper()
        {
            string src = Read("Assets/Scripts/Systems/InteriorQualityBaseline.cs");
            Assert.That(src, Does.Contain("[무번호 32]"), "[32] 마커.");
            Assert.That(src, Does.Contain("public static Material GetCommonInteriorFloorMat()"),
                "[32] 공통 석타일 조회 — 전 실내 빌더 단일 소스.");
            Assert.That(src, Does.Contain("public static Material CreateCommonInteriorFloorMat(float widthMeters, float depthMeters, string owner)"),
                "[32] 크기별 인스턴스 팩토리 — UV 0..1 쿼드 1m 타일 정합.");
            Assert.That(src, Does.Contain("GetFloorTileMat(\"castleStone\", new Color(0.38f, 0.37f, 0.35f), new Color(0.26f, 0.25f, 0.24f))"),
                "[32] 무번호 15 성 석타일 팔레트 승계(값 동일 — 시각 연속성).");
            Assert.That(src, Does.Contain("m.mainTextureScale = new Vector2(Mathf.Max(1f, widthMeters), Mathf.Max(1f, depthMeters));"),
                "[32] 1m 타일 스케일 — 방 크기와 무관하게 동일 밀도.");
            Assert.That(src, Does.Contain("new Material(GetCommonInteriorFloorMat())"),
                "[32] 공유 캐시 인스턴스 복제 — 캐시 직접 변조 금지(빌더 간 오염 방지).");
        }

        // ── InteriorFurnishingPass — 국가 키 폐기(무번호 15 오버레이도 동일 단일 소스) ──
        [Test]
        public void Source_32_FurnishingPass_Delegates()
        {
            string src = Read("Assets/Scripts/Systems/InteriorFurnishingPass.cs");
            Assert.That(src, Does.Contain("InteriorQualityBaseline.GetCommonInteriorFloorMat();"),
                "[32] 성 가구 패스 오버레이도 공통 석타일 위임.");
            Assert.That(src, Does.Not.Contain("castleStone_"),
                "[32] 국가별 바닥 키(castleStone_{nationStyle}) 폐기 — 씬 통일 계약.");
        }

        // ── 8빌더 교체 + 구 바닥 식별자 잔존 금지(파일별 스코프 — 신규 블록만 아니라 단일 목적 문자열이므로 안전) ──
        [Test]
        public void Source_32_AllTerritoryBuilders_Unified()
        {
            string castle = Read("Assets/Scripts/Systems/PlayerCastleInteriorBuilder.cs");
            Assert.That(castle, Does.Contain("CreateCommonInteriorFloorMat(roomWidth, roomDepth, \"PlayerCastle\")"),
                "[32] 성 바닥 — 공통 석타일.");
            Assert.That(castle, Does.Not.Contain("PlayerCastle_FloorMat"), "[32] 구 성 바닥 머티리얼 잔존 금지.");
            Assert.That(castle, Does.Not.Contain("GenerateCastleFloor"), "[32] 국가별 성 바닥 텍스처 폐기 — 벽 텍스처 계약만 유지.");

            string house = Read("Assets/Scripts/Systems/HouseInteriorBuilder.cs");
            Assert.That(house, Does.Contain("CreateCommonInteriorFloorMat(roomWidth, roomDepth, \"House\")"), "[32] 집 바닥.");
            Assert.That(house, Does.Not.Contain("House_FloorMat"), "[32] 구 집 바닥 잔존 금지.");
            Assert.That(house, Does.Not.Contain("GenerateHouseFloor"), "[32] 집 전용 바닥 텍스처 폐기.");

            string craft = Read("Assets/Scripts/UI/CraftHouseInteriorBuilder.cs");
            Assert.That(craft, Does.Contain("CreateCommonInteriorFloorMat(roomWidth, roomDepth, \"CraftHouse\")"), "[32] 공방 바닥.");
            Assert.That(craft, Does.Not.Contain("CraftHouse_FloorMat"), "[32] 구 공방 바닥 잔존 금지.");
            Assert.That(craft, Does.Not.Contain("GenerateCraftHouseFloor"), "[32] 공방 전용 바닥 텍스처 폐기.");

            string church = Read("Assets/Scripts/UI/ChurchInteriorBuilder.cs");
            Assert.That(church, Does.Contain("CreateCommonInteriorFloorMat(ROOM_WIDTH, ROOM_DEPTH, \"Church\")"), "[32] 교회 바닥.");
            Assert.That(church, Does.Not.Contain("GenerateChurchFloor"), "[32] 교회 전용 바닥 텍스처 폐기.");

            string shop = Read("Assets/Scripts/UI/ShopInteriorBuilder.cs");
            Assert.That(shop, Does.Contain("CreateCommonInteriorFloorMat(roomWidth, roomDepth, \"Shop\")"), "[32] 상점 바닥.");
            Assert.That(shop, Does.Not.Contain("Shop_FloorMat"), "[32] 구 상점 바닥 잔존 금지.");
            Assert.That(shop, Does.Not.Contain("GenerateShopFloor"), "[32] 상점 전용 바닥 텍스처 폐기.");

            string barn = Read("Assets/Scripts/Systems/BarnInteriorBuilder.cs");
            Assert.That(barn, Does.Contain("CreateCommonInteriorFloorMat(ROOM_WIDTH, ROOM_DEPTH, \"Barn\")"), "[32] 외양간 바닥.");
            Assert.That(barn, Does.Not.Contain("0.6f, 0.4f, 0.2f"), "[32] 구 목재색 바닥 폐기.");

            string tavern = Read("Assets/Scripts/Systems/TavernInteriorBuilder.cs");
            Assert.That(tavern, Does.Contain("CreateCommonInteriorFloorMat(width, depth, \"Tavern\")"), "[32] 주점 바닥.");
            Assert.That(tavern, Does.Not.Contain("TavernFloorMat"), "[32] 구 주점 바닥 잔존 금지.");

            string restaurant = Read("Assets/Scripts/Systems/RestaurantInteriorBuilder.cs");
            Assert.That(restaurant, Does.Contain("CreateCommonInteriorFloorMat(width, depth, \"Restaurant\")"), "[32] 식당 바닥.");
            Assert.That(restaurant, Does.Not.Contain("RestaurantFloorMat"), "[32] 구 식당 바닥 잔존 금지.");
        }

        // ── 제외 확정 — 던전(156차 흙 단일화 승계)·이계(잿빛 흙)는 특수 정체성 유지 ──
        [Test]
        public void Source_32_Exclusions_Preserved()
        {
            Assert.That(Read("Assets/Scripts/Systems/CaveInteriorBuilder.cs"), Does.Not.Contain("CreateCommonInteriorFloorMat"),
                "[32] 던전 제외 — 156차 흙 단일화 계약 그대로(지하 5층 전부 동일 흙 바닥).");
            Assert.That(Read("Assets/Scripts/Systems/AfterlifeInteriorBuilder.cs"), Does.Not.Contain("CreateCommonInteriorFloorMat"),
                "[32] 이계(공동묘지) 제외 — 잿빛 흙 정체성 유지.");
        }
    }
}
