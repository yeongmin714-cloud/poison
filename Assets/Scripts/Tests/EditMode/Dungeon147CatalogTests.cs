using NUnit.Framework;
using System.IO;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [147차 P-A4] 던전 팩 카탈로그 계약테스트.
    /// ①대표 파일명 카테고리 판정(접두어 규칙 수치 어서션) ②전수 320 분류 합계·누락 0
    /// ③ResourceRoot/로더 폴백 소스계약. Random 0건 — 결정론.
    /// </summary>
    public sealed class Dungeon147CatalogTests
    {
        [Test]
        public void A1_Representative_Names_Category_Assertions()
        {
            Assert.AreEqual(DungeonAssetCatalog.Category.Structural, DungeonAssetCatalog.CategoryOf("Wall_5_1A.glb"));
            Assert.AreEqual(DungeonAssetCatalog.Category.Structural, DungeonAssetCatalog.CategoryOf("Floor_1_1A.glb"));
            Assert.AreEqual(DungeonAssetCatalog.Category.Structural, DungeonAssetCatalog.CategoryOf("Stairs_2.glb"));
            Assert.AreEqual(DungeonAssetCatalog.Category.Structural, DungeonAssetCatalog.CategoryOf("Wall_Door.glb"));
            Assert.AreEqual(DungeonAssetCatalog.Category.Decor, DungeonAssetCatalog.CategoryOf("Crystal_1.glb"));
            Assert.AreEqual(DungeonAssetCatalog.Category.Decor, DungeonAssetCatalog.CategoryOf("Rocks_1_1.glb"));
            Assert.AreEqual(DungeonAssetCatalog.Category.Decor, DungeonAssetCatalog.CategoryOf("Vines_1_1.glb"));
            Assert.AreEqual(DungeonAssetCatalog.Category.Props, DungeonAssetCatalog.CategoryOf("Chest_1.glb"));
            Assert.AreEqual(DungeonAssetCatalog.Category.Props, DungeonAssetCatalog.CategoryOf("Potion_1_1.glb"));
            Assert.AreEqual(DungeonAssetCatalog.Category.Props, DungeonAssetCatalog.CategoryOf("Sztandar1_1.glb"));
            Assert.AreEqual(DungeonAssetCatalog.Category.Furniture, DungeonAssetCatalog.CategoryOf("Chair_1.glb"));
            Assert.AreEqual(DungeonAssetCatalog.Category.Furniture, DungeonAssetCatalog.CategoryOf("hanging_shelf_1.glb"));
            Assert.AreEqual(DungeonAssetCatalog.Category.Lighting, DungeonAssetCatalog.CategoryOf("Torch.glb"));
            Assert.AreEqual(DungeonAssetCatalog.Category.Lighting, DungeonAssetCatalog.CategoryOf("Candle_1.glb"));
        }

        [Test]
        public void A2_TotalCount_320_NoMisc()
        {
            Assert.AreEqual(320, DungeonAssetCatalog.TotalCount, "[147차 P-A4] 던전 GLB 전수 320 계약.");
            Assert.AreEqual(0, DungeonAssetCatalog.NamesIn(DungeonAssetCatalog.Category.Misc).Count,
                "규칙 미적중(Misc) 0건 — 전수 분류 완결 계약.");
        }

        [Test]
        public void A3_Category_Counts_Match_Audit()
        {
            Assert.AreEqual(105, DungeonAssetCatalog.NamesIn(DungeonAssetCatalog.Category.Structural).Count);
            Assert.AreEqual(71, DungeonAssetCatalog.NamesIn(DungeonAssetCatalog.Category.Decor).Count);
            Assert.AreEqual(96, DungeonAssetCatalog.NamesIn(DungeonAssetCatalog.Category.Props).Count);
            Assert.AreEqual(39, DungeonAssetCatalog.NamesIn(DungeonAssetCatalog.Category.Furniture).Count);
            Assert.AreEqual(9, DungeonAssetCatalog.NamesIn(DungeonAssetCatalog.Category.Lighting).Count);
        }

        [Test]
        public void A4_All_Names_Are_Glb_And_Unique()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var cat in new[] {
                DungeonAssetCatalog.Category.Structural, DungeonAssetCatalog.Category.Decor,
                DungeonAssetCatalog.Category.Props, DungeonAssetCatalog.Category.Furniture,
                DungeonAssetCatalog.Category.Lighting, DungeonAssetCatalog.Category.Misc })
            {
                foreach (var n in DungeonAssetCatalog.NamesIn(cat))
                {
                    Assert.True(n.EndsWith(".glb"), $"GLB 확장자 계약 위반: {n}");
                    Assert.True(seen.Add(n), $"중복 파일명: {n}");
                }
            }
            Assert.AreEqual(320, seen.Count);
        }

        [Test]
        public void A5_ResourceRoot_And_Loader_Fallback_SourceContracts()
        {
            string cat = File.ReadAllText("Assets/Scripts/Systems/DungeonAssetCatalog.cs");
            Assert.That(cat, Does.Contain("ResourceRoot = \"Models/Dungeon/\""),
                "[147차 P-A4] 카탈로그 리소스 루트 계약.");

            string loader = File.ReadAllText("Assets/Scripts/Systems/RuntimeModelLoader.cs");
            Assert.That(loader, Does.Contain("\"Models/Dungeon/\" + key"),
                "[147차 P-A2] 로더 던전 폴백 경로 계약 — UserProvided 실패 후 2차 시도.");
            int provided = loader.IndexOf("Models/UserProvided/\" + key");
            int dungeon = loader.IndexOf("\"Models/Dungeon/\" + key");
            Assert.That(dungeon, Is.GreaterThan(provided), "폴백 순서 계약 — UserProvided 우선.");
        }
    }
}
