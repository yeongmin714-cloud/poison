using NUnit.Framework;
using System.IO;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [147차 P-B] 동굴/던전 빌더 에셋화 계약테스트 — 소스 어서션 원칙(Cave146P1ContractTests 선례,
    /// 런타임 검증은 Play 게이트 — 부모 몫). Random 0건 — 결정론.
    /// ①B0 스케일 고정 — tierScale=2f 3지점(Core/GetEntranceSpawnPosition/GetFloorSpawnPosition 2인자)
    ///   ([148차 P2] 방 면적 4배(선형×2)로 고정값 1f→2f 갱신), 구 ×1.25 수식 부재, ClampTier 파라미터 검증 유지.
    /// ②B1 에셋 벽 — CreateAssetWalls 존재 + Core에서 CreatePolygonWalls 호출 직후 배치, 박스 벽
    ///   MeshRenderer off/BoxCollider 유지, 입구 변 가드 패턴, 로드 실패 시 레이어 전체 스킵(폴백).
    /// ③B2 에셋 바닥 타일 — CreateAssetFloorTiles 존재 + CreatePolygonFloor 호출 직후 배치,
    ///   CaveFloor(MeshRenderer+MeshCollider) 유지, 상면 y=+0.01, ray casting 내부 판정 유틸.
    /// ④B3 횟불/채굴노드 GLB 승격 — Torch GLB 시도+프리미티브 폴백, 노드 타입별 매핑
    ///   (Stone→Rocks_1_1 / IronOre→Crystal_1), 보석상자 무수정.
    /// ⑤에셋 키 9종 카탈로그 실물 일치 — Structural/Decor/Lighting 전부 CategoryOf+NamesIn 교차 검증(오타 금지).
    /// ⑥Random 사용 0건 — CaveInteriorBuilder에 "Random." 토큰 미등장(InteriorRandomizer 단일 시드 계약).
    /// </summary>
    public sealed class Dungeon147BTests
    {
        private const string BuilderPath = "Assets/Scripts/Systems/CaveInteriorBuilder.cs";
        private const string CatalogPath = "Assets/Scripts/Systems/DungeonAssetCatalog.cs";
        private const string LoaderPath = "Assets/Scripts/Systems/RuntimeModelLoader.cs";

        // ── 1. B0 스케일 고정 계약 ──

        [Test]
        public void B0_TierScaleAbolished_CellmapInternalized_156()
        {
            string src = CodeOnly(ReadSource(BuilderPath));

            // [156차 셀맵] tierScale 전면 폐기 — 방 크기는 셀맵에 내재화(본 명세 변경: 직각 꺽임 재설계).
            Assert.That(CountOccurrences(src, "tierScale"), Is.EqualTo(0),
                "[156차 셀맵] tierScale 식별자 전면 부재 — 구 [148차 P2] 고정 3지점 계약도 셀맵 내재화로 승계 종료.");
            Assert.That(src.Contains("1f + 0.25f * (tier - 1)"), Is.False,
                "[147차 P-B0] 구 ×1.25 스케일 수식 전면 부재 — tier 기하 적용 폐기 계약.");

            // ClampTier 호출은 유지(파라미터 검증 계약 — 고정 후에도 tier 입력 검증 존속).
            Assert.That(CountOccurrences(src, "tier = InteriorRandomizer.ClampTier(tier);"), Is.EqualTo(3),
                "ClampTier 파라미터 검증 3지점 유지 — B0은 기하 고정만, 검증 폐기 아님.");
        }

        // ── 2. B1 에셋 벽 계약 ──

        [Test]
        public void B1_ThinStoneWalls_ReplaceAssetWalls_156()
        {
            string src = CodeOnly(ReadSource(BuilderPath));

            // [156차 셀맵] 던전 팩 벽 GLB 폐기 → 얇은 돌 벽(셀 경계 런 박스) — 사용자 지시(돌 느낌 교체).
            Assert.That(src.Contains("CreateAssetWalls"), Is.False,
                "[156차] CreateAssetWalls(던전 팩 벽 GLB) 소스 잔존 금지 — 폐기 완료.");
            Assert.That(src, Does.Contain("private static void CreateCellWalls(GameObject room)"),
                "[156차 셀맵] 얇은 돌 벽 신설 시그니처 계약.");
            int floor = src.IndexOf("CreateCellFloor(room);");
            int walls = src.IndexOf("CreateCellWalls(room);");
            Assert.That(floor, Is.GreaterThanOrEqualTo(0), "Core의 CreateCellFloor 호출 존재.");
            Assert.That(walls, Is.GreaterThan(floor),
                "CreateCellWalls는 CreateCellFloor 직후 호출 — 바닥→벽 순서 계약.");
            Assert.That(src, Does.Contain("private const float WALL_THICKNESS = 0.5f;"),
                "[156차] 얇은 벽 두께 0.5m — 셀 2m의 1/4(예시 사진 문법: 얇은 벽).");

            // 런 머징 — 같은 직선 인접 세그먼트 병합 + 내부 벽 중심선 규칙(이중 벽 방지).
            Assert.That(src, Does.Contain("private static List<WallRun> MergeWallRuns(List<WallRun> candidates)"),
                "[156차 셀맵] 벽 런 머징 — 같은 직선 위 인접 세그먼트는 박스 1개로 병합.");
            Assert.That(src, Does.Contain("if (west && east)"),
                "[156차 셀맵] 내부 '#'(양쪽이 바닥)는 셀 중심선에 벽 1개 — 이중 벽 방지 규칙.");

            // 벽 캡 — 두께 0 평면 짙은 돌 선(성내부 F1 패턴 승계), 콜라이더 없음.
            Assert.That(src, Does.Contain("private static void CreateWallCap(GameObject room, Material capMat, WallRun run, float length, float mid, int index)"),
                "[156차 셀맵] 벽 톱면 평면 캡 — 성내부 짙은 돌 선 패턴 승계.");
        }

        // ── 3. B2 에셋 바닥 타일 계약 ──

        [Test]
        public void B2_CellFloorMesh_ReplacesAssetFloorTiles_156()
        {
            string src = CodeOnly(ReadSource(BuilderPath));

            // [156차 셀맵] Concrate 타일 GLB 폐기 → 병합 쿼드 셀 바닥(충돌 단일소스 유지).
            Assert.That(src.Contains("CreateAssetFloorTiles"), Is.False,
                "[156차] CreateAssetFloorTiles(Concrate 타일) 소스 잔존 금지 — 회색 타일 폐기(흙 단일화).");
            Assert.That(src.Contains("IsPointInPolygon"), Is.False,
                "[156차 셀맵] 다각형 ray casting 유틸 폐기 — 셀맵 전환 잔존 금지.");
            Assert.That(src.Contains("FloorTileKeys"), Is.False,
                "[156차] 타일 키(Floor_1_1A/Floor_2_1A) 소스 잔존 금지.");
            Assert.That(src, Does.Contain("floor.AddComponent<MeshCollider>();"),
                "[156차 셀맵] CaveFloor MeshCollider 유지 — 충돌 단일소스 계약 승계.");
            Assert.That(src, Does.Contain("CaveMaterialFactory.CreateDirtFloor()"),
                "[156차 P1-3] 바닥 머티리얼 = 흙 단일화(FarmPlotVisuals 절차 흙 텍스처 단일소스).");
            Assert.That(src.Contains("CreateFloorToneStrips"), Is.False,
                "[156차] 갈색/회색 톤 스트립 폐기 — 바닥 구분 제거(사용자 지시).");
        }

        // ── 4. B3 횟불/채굴노드 비주얼 승격 계약 ──

        [Test]
        public void B3_TorchAndMiningNode_GlbPromotion_WithPrimitiveFallback()
        {
            string src = ReadSource(BuilderPath);

            // 횟불 — Torch GLB 교체 시도 + 실패 시 기존 프리미티브 폴백(로그 1행).
            Assert.That(src, Does.Contain("RuntimeModelLoader.TryGetModel(\"Torch\", out var torchModel)"),
                "[147차 P-B3] 횟불 Torch GLB 승격 시도.");
            Assert.That(src, Does.Contain("Torch GLB 부재 — 기존 프리미티브 토치 폴백(조명 로직 무수정)."),
                "횟불 폴백 로그 — 기존 프리미티브 유지, Light/TorchFlicker 로직 무수정.");
            Assert.That(src, Does.Contain("torchGo.AddComponent<TorchFlicker>();"),
                "TorchFlicker 부착 유지 — 조명 로직 무수정 계약.");

            // 채굴노드 — Decor 사이클 아닌 노드 타입별 고정 매핑(하드박제).
            Assert.That(src, Does.Contain("nodeType == ResourceNode.ResourceType.IronOre ? \"Crystal_1\" : \"Rocks_1_1\""),
                "노드 타입별 고정 매핑 — IronOre→Crystal_1 / Stone→Rocks_1_1.");
            Assert.That(src, Does.Contain("AlignVisualBottomToWorldY(visual, 0f);"),
                "노드 비주얼 밑면 접지(루트 스케일 0.6 보정 헬퍼).");

            // 상호작용 로직 무수정 — ResourceNode 구성/보석상자(이미 GLB 기반) 원형 유지.
            Assert.That(src, Does.Contain("node.ConfigureResourceType(nodeType);"),
                "ResourceNode 구성 유지 — 상호작용 로직 무수정.");
            Assert.That(src, Does.Contain("go.AddComponent<BoxCollider>();"),
                "노드 루트 BoxCollider 유지 — TryAutoMine 토글 대상 계약.");
            Assert.That(src, Does.Contain("IndoorFurnitureCatalog.CreateCrateAt(poolIndex % 2"),
                "보석상자 무수정 — 이미 GLB 기반(GemGlow 실측) 계약.");
        }

        // ── 5. 에셋 키 카탈로그 실물 일치(오타 금지) ──

        [Test]
        public void AssetKeys_MatchCatalog_RealNames()
        {
            // B1 벽 3종 + B2 바닥 2종 — Structural 실물(ls 재확인 계약).
            // [148차 P1-1] Wall_3_1A(목재 지보 기둥) → Wall_Crack(벽돌 크랙) 교체 — 벽돌 3종 사이클.
            string[] structuralKeys = { "Wall_1_1A", "Wall_2_1A", "Wall_Crack", "Floor_1_1A", "Floor_2_1A" };
            foreach (string key in structuralKeys)
            {
                Assert.AreEqual(DungeonAssetCatalog.Category.Structural, DungeonAssetCatalog.CategoryOf(key + ".glb"),
                    $"Structural 판정: {key} — 벽/바닥 계층은 구조물만.");
                Assert.That(DungeonAssetCatalog.NamesIn(DungeonAssetCatalog.Category.Structural),
                    Does.Contain(key + ".glb"), $"카탈로그 실물 존재: {key}.glb — 오타 금지 계약.");
            }

            // B3 채굴노드 매핑 — Decor 실물.
            foreach (string key in new[] { "Rocks_1_1", "Crystal_1" })
            {
                Assert.AreEqual(DungeonAssetCatalog.Category.Decor, DungeonAssetCatalog.CategoryOf(key + ".glb"),
                    $"Decor 판정: {key} — 채굴노드 비주얼은 장식 카테고리.");
                Assert.That(DungeonAssetCatalog.NamesIn(DungeonAssetCatalog.Category.Decor),
                    Does.Contain(key + ".glb"), $"카탈로그 실물 존재: {key}.glb — 오타 금지 계약.");
            }

            // B3 횟불 — Lighting 실물.
            Assert.AreEqual(DungeonAssetCatalog.Category.Lighting, DungeonAssetCatalog.CategoryOf("Torch.glb"),
                "Lighting 판정: Torch — 횟불 비주얼은 조명 카테고리.");
            Assert.That(DungeonAssetCatalog.NamesIn(DungeonAssetCatalog.Category.Lighting),
                Does.Contain("Torch.glb"), "카탈로그 실물 존재: Torch.glb — 오타 금지 계약.");
        }

        // ── 6. 로더 폴백 경로 계약(에셋 계층의 유일 진입점) ──

        [Test]
        public void LoaderFallbackContract_UserProvided_ThenDungeon()
        {
            string loader = ReadSource(LoaderPath);
            Assert.That(loader, Does.Contain("TryGetModel(string key, out GameObject model, out ModelMetadata metadata)"),
                "3인자 TryGetModel 존재 — 에셋 계층 로드 진입점 계약.");
            Assert.That(loader, Does.Contain("prefab = Resources.Load<GameObject>(\"Models/Dungeon/\" + key);"),
                "[147차 P-A2] 던전 팩 2차 폴백 경로 — 320종 실스케일 에셋.");
        }

        // ── 7. Random 사용 0건 계약 ──

        [Test]
        public void Random_ZeroOccurrence_InCaveInteriorBuilder()
        {
            string src = ReadSource(BuilderPath);
            Assert.That(src.Contains("Random."), Is.False,
                "Random API 0건 계약 — 유일한 무작위원은 InteriorRandomizer 시드 결정론(에셋 키/배치 전부 고정 사이클).");

            string catalog = ReadSource(CatalogPath);
            Assert.That(catalog.Contains("Random."), Is.False,
                "카탈로그 결정론 — Random 0건(Dungeon147CatalogTests 계약과 정합).");
        }

        // ── 공용 헬퍼 ──

        private static string ReadSource(string path)
        {
            Assert.That(File.Exists(path), Is.True, $"소스 파일 존재: {path}");
            return File.ReadAllText(path);
        }

        /// <summary>[156차] 소스 텍스트 계약 보조 — 주석 절단 후 반환(폐기 이력을 기술하는 문서
        /// 주석은 계약 위반이 아니므로 코드 식별자만 판정한다).</summary>
        private static string CodeOnly(string src)
        {
            var lines = src.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                int idx = lines[i].IndexOf("//");
                if (idx >= 0) lines[i] = lines[i].Substring(0, idx);
            }
            return string.Join("\n", lines);
        }

        private static int CountOccurrences(string text, string token)
        {
            int count = 0, idx = 0;
            while ((idx = text.IndexOf(token, idx, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += token.Length;
            }
            return count;
        }
    }
}
