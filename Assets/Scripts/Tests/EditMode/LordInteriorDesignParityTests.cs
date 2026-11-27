using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 88] 영주 성 실내 — 설계도 정밀 정합 계약(13패스 격자 실측 — docs/plans/2026-11-20_no88-lord-interior-furnishing-analysis.md).
    /// 방별 가구 종류·개수·크기(방 폭 비율 목표치)를 소스 리터럴로 고정. 좌표·요우 세부는 Play 튜닝 여지.
    /// ⚠ 음수 어서션은 자기 파일 주석 오염 회로 주의 — 패스 소스의 주석 리터럴을 잡지 않는 이름만 사용.
    /// </summary>
    public class LordInteriorDesignParityTests
    {
        private const string PassRelPath = "Assets/Scripts/Systems/InteriorFurnishingPass.cs";
        private const string BuilderRelPath = "Assets/Scripts/Systems/PlayerCastleInteriorBuilder.cs";

        private static string Read(string rel) =>
            System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "..", rel));

        private static int Count(string src, string needle) =>
            src.Split(new[] { needle }, System.StringSplitOptions.None).Length - 1;

        // ── 로비: 벽난로 북벽(12.7m)·사대각 벤치 4(10.6m)·카펫 24×11·데칼 3·벽 횃불 E/W ──

        [Test]
        public void Lobby_BlueprintContract()
        {
            string src = Read(PassRelPath);
            Assert.That(Count(src, "Lobby_Bench_NE") + Count(src, "Lobby_Bench_SE")
                + Count(src, "Lobby_Bench_SW") + Count(src, "Lobby_Bench_NW"), Is.EqualTo(4),
                "[88] 사대각 벤치 4(구 N/S/E/W 직교 배치 교체).");
            Assert.That(src, Does.Contain("BuildFireplace(lobbyRoot.transform, \"Lobby_Fireplace\", new Vector3(0f, 0f, z0 - 1.6f), 0f, 12.7f)"),
                "[88] 로비 벽난로 북벽 밀착 12.7m(절차 석재 — 정확 치수).");
            Assert.That(src, Does.Contain("24f, 11f"), "[88] 로비 카펫 24×11m(격자 36%×27%).");
            Assert.That(Count(src, "Lobby_Decal_"), Is.EqualTo(3), "[88] 바닥 석판 데칼 3(입구 좌우 2+중앙 1).");
            Assert.That(Count(src, "Lobby_Torch_W") + Count(src, "Lobby_Torch_E"), Is.EqualTo(2),
                "[88] 로비 벽부착 횃불 2(E/W).");
            Assert.That(Count(src, "Lobby_Torch_NW") + Count(src, "Lobby_Torch_NE")
                + Count(src, "Lobby_Torch_SW") + Count(src, "Lobby_Torch_SE"), Is.EqualTo(0),
                "[88] 구 모서리 브레이저 스탠드 4 제거 역어서션.");
        }

        // ── 침실: 캐노피 기능침대 9×11 북벽 밀착·옷장 10m·책상 2세트·소파+벽난로·카펫 15.5×9.3·횃불 4 ──

        [Test]
        public void Bedroom_BlueprintContract()
        {
            string pass = Read(PassRelPath);
            string builder = Read(BuilderRelPath);
            Assert.That(builder, Does.Contain("CreateBed(3.67f, 4.49f"), "[88] 기능침대 authored 3.67×4.49(×2.45→최종 ~9×11m).");
            Assert.That(builder, Does.Contain("new Vector2(0f, 35.5f)"), "[88] 기능침대 최종 (0,35.5) 북벽 밀착.");
            Assert.That(Count(pass, "Bedroom_CanopyPost_"), Is.EqualTo(1), "[88] 캐노피 기둥 — 보간 루프 선언 1건.");
            Assert.That(pass, Does.Contain("for (int i = 0; i < 4; i++)"), "[88] 캐노피 기둥 4회 루프.");
            Assert.That(Count(pass, "Bedroom_CanopyBar_"), Is.EqualTo(4), "[88] 캐노피 상부 프레임 4재.");
            Assert.That(pass, Does.Contain("BuildWardrobe(bedroomRoot.transform, \"Bedroom_Wardrobe\", new Vector3(-northHalfWidth + 1.8f, 0f, 33.5f), 90f, 10f)"),
                "[88] 옷장 세로 10m(서벽 — 절차 장롱형).");
            Assert.That(pass, Does.Contain("Bedroom_Desk_E"), "[88] 동벽 책상 세트.");
            Assert.That(builder, Does.Contain("SetAnchorPosition(room, \"CommandDesk\", new Vector2(12.8f, 39.3f))"),
                "[88] 북동 책상 = 기능 지휘 책상(빌더 승계 — UI/네임플레이트 유지).");
            Assert.That(pass, Does.Contain("BuildSofa(bedroomRoot.transform, \"Bedroom_Sofa\", new Vector3(-8f, 0f, 20.9f), 180f, 7.2f)"),
                "[88] 소파 7.2m(남측 서측 — 문 회피, 절차).");
            Assert.That(pass, Does.Contain("BuildFireplace(bedroomRoot.transform, \"Bedroom_Fireplace\", new Vector3(-8f, 0f, 15.6f), 180f, 9f)"),
                "[88] 침실 벽난로 9m(남측 서측 — 문 회피, 절차 석재).");
            Assert.That(pass, Does.Contain("15.5f, 9.3f"), "[88] 침대 카펫 15.5×9.3m(격자 43%×31%).");
            Assert.That(Count(pass, "Bedroom_Torch_"), Is.EqualTo(4), "[88] 침실 횃불 4(북벽 2+남입구 2).");
            Assert.That(Count(pass, "Bedroom_Nightstand") + Count(pass, "Bedroom_Candle"), Is.EqualTo(0),
                "[88] 구 협탁 2·촛불 2 제거 역어서션(설계도 부재).");
        }

        // ── 제작소: 화로+원형 가마+모루+작업대 4종+장작 2 ──

        [Test]
        public void CraftRoom_BlueprintContract()
        {
            string src = Read(PassRelPath);
            Assert.That(src, Does.Contain("blacksmith-forge-09fa85"), "[88] 중앙 화로(북벽 5.4m) — 대장 화로 GLB.");
            Assert.That(src, Does.Contain("NormalizeMaxAxis(craftSmelter, 8.2f)"), "[88] 원형 가마 8.2m(북동).");
            Assert.That(src, Does.Contain("NormalizeMaxAxis(craftAnvil, 3.3f)"), "[88] 모루 3.3m(북서).");
            Assert.That(Count(src, "Craft_ToolBench") + Count(src, "Craft_ArmourBench")
                + Count(src, "Craft_LeatherBench") + Count(src, "Craft_HideRack"), Is.EqualTo(4),
                "[88] 작업대 4종(도구/갑옷/가죽/가죽가공).");
            Assert.That(Count(src, "Craft_Firewood_"), Is.EqualTo(2), "[88] 장작 더미 2.");
            Assert.That(Count(src, "Craft_Material_"), Is.EqualTo(3), "[88] 원자재 상자 3.");
        }

        // ── 창고: 북벽 선반 3(8.2m)+동벽 긴 선반 15m+서벽 긴 탁자 15m+자루 6+책상·바구니·상자 3·통 3 ──

        [Test]
        public void StorageRoom_BlueprintContract()
        {
            string src = Read(PassRelPath);
            Assert.That(Count(src, "Storage_NShelf_"), Is.EqualTo(1), "[88] 북벽 선반 — 보간 루프 선언 1건.");
            Assert.That(src, Does.Contain("i < 3"), "[88] 북벽 선반 3회 루프.");
            Assert.That(src, Does.Contain("BuildShelf(storageRoot.transform, $\"Storage_NShelf_{i}\""), "[88] 북벽 선반 절차 8.2×4.5(3단).");
            Assert.That(src, Does.Contain("BuildShelf(storageRoot.transform, \"Storage_EastShelf\", new Vector3(57.4f, 0f, 26f), -90f, 15f"),
                "[88] 동벽 긴 선반 15m(절차).");
            Assert.That(src, Does.Contain("BuildWoodTable(storageRoot.transform, \"Storage_LongTable\", new Vector3(19.9f, 0f, 21.5f), 90f, 15f, 2.2f, 2.3f)"),
                "[88] 서벽 긴 탁자 15m(절차 정확 치수).");
            Assert.That(Count(src, "Storage_Sack_F") + Count(src, "Storage_Sack_T"), Is.EqualTo(6), "[88] 자루 6(바닥 3+상판 3).");
            Assert.That(src, Does.Contain("8.6f, 2.6f"), "[88] 물류 책상 8.6×2.6m(남동).");
            Assert.That(src, Does.Contain("Storage_Basket"), "[88] 바구니 1.");
            Assert.That(src, Does.Contain("NormalizeMaxAxis(storageStrongbox, 5.7f)"), "[88] 보물상자 5.7m(북서).");
            Assert.That(Count(src, "Storage_Crate_"), Is.EqualTo(3), "[88] 상자 3.");
            Assert.That(Count(src, "Storage_Barrel_"), Is.EqualTo(3), "[88] 통 3.");
            Assert.That(Count(src, "Storage_Jar_"), Is.EqualTo(0), "[88] 구 항아리 3 제거 역어서션(설계도 부재).");
        }

        // ── 병영: 작전 테이블 2(지도)+집무 책상+보관함+훈련 더미(기능침대 20 = 기능 계약 유지) ──

        [Test]
        public void BarracksRoom_BlueprintContract()
        {
            string src = Read(PassRelPath);
            Assert.That(Count(src, "Barracks_WarTable_W") + Count(src, "Barracks_WarTable_E"), Is.EqualTo(2),
                "[88] 작전 테이블 2(북서 11m+북중앙 12m).");
            Assert.That(src, Does.Contain("campaign-map-scroll-d4a382"), "[88] 원정 지도(작전 테이블 위).");
            Assert.That(src, Does.Contain("battle-map-tile-69e0b1"), "[88] 전술 지판(작전 테이블 2 위 — 재배선).");
            Assert.That(src, Does.Contain("Barracks_Desk"), "[88] 집무 책상+의자(북동).");
            Assert.That(src, Does.Contain("NormalizeMaxAxis(barracksStrongbox, 5.7f)"), "[88] 중앙 보관함 5.7m.");
            Assert.That(src, Does.Contain("straw-training-dummy-64eeb4"), "[88] 훈련 더미(Polyfork 신규 수용).");
        }

        // ── 연금술소: 시약 선반 8.2m+가마솥 6m+증류 실험대 11.3m+증류기+절구 2+동벽 실험판 ──

        [Test]
        public void AlchemyRoom_BlueprintContract()
        {
            string src = Read(PassRelPath);
            Assert.That(src, Does.Contain("BuildShelf(alchemyRoot.transform, \"Alchemy_Shelf\", new Vector3(13f, 0f, -41.5f), 0f, 8.2f"),
                "[88] 시약 선반 8.2m(남서 — 절차).");
            Assert.That(src, Does.Contain("NormalizeMaxAxis(alchemyCauldron, 6f)"), "[88] 가마솥 6m(남중앙).");
            Assert.That(src, Does.Contain("BuildWoodTable(alchemyRoot.transform, \"Alchemy_DistillBench\", new Vector3(41f, 0f, -41f), 180f, 11.3f, 2.6f, 2.3f)"),
                "[88] 증류 실험대 11.3m(남중앙 — 절차).");
            Assert.That(src, Does.Contain("BuildShelf(alchemyRoot.transform, \"Alchemy_WallBoard\""), "[88] 동벽 실험판 7.5m(절차 3단).");
            Assert.That(src, Does.Contain("bubbling-retort-4f9721"), "[88] 증류기(Polyfork 신규 수용).");
            Assert.That(Count(src, "Alchemy_Mortar_") , Is.EqualTo(2), "[88] 절구 2(남동 석기).");
            Assert.That(src, Does.Contain("Alchemy_WallBoard"), "[88] 동벽 실험판(세로 7.5m).");
        }
    }
}
