using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [155차 P0] 공동묘지 카탈로그 계약테스트 — 키/카테고리/실물(Resources) 1:1.
    /// </summary>
    public class AfterlifeCemeteryCatalogTests
    {
        [Test]
        public void Catalog_Contains_24_Unique_Keys()
        {
            var dict = AfterlifeCemeteryCatalog.All;
            Assert.AreEqual(24, dict.Count, "[155차 P0] 카탈로그 24종 — 선별 수용 실측 일치");
            foreach (var kv in dict)
                Assert.That(kv.Key, Does.StartWith("Cem_"), $"키 규약 Cem_*: {kv.Key}");
        }

        [Test]
        public void All_Keys_Loadable_From_Resources()
        {
            foreach (var kv in AfterlifeCemeteryCatalog.All)
            {
                var prefab = Resources.Load<GameObject>("Models/Cemetery/" + kv.Key);
                Assert.That(prefab, Is.Not.Null,
                    $"[155차 P0] 실물 존재: Models/Cemetery/{kv.Key}.prefab — 오타/누락 금지");
            }
        }

        [Test]
        public void Statues_AtLeast_Seven_And_Graves_AtLeast_Five()
        {
            int statues = 0, graves = 0;
            foreach (var kv in AfterlifeCemeteryCatalog.All)
            {
                if (kv.Value == AfterlifeCemeteryCatalog.Category.Statue) statues++;
                if (kv.Value == AfterlifeCemeteryCatalog.Category.Grave) graves++;
            }
            Assert.GreaterOrEqual(statues, 7, "[155차 P0] 석상 7종 이상 — 공동묘지 석상 요구");
            Assert.GreaterOrEqual(graves, 5, "[155차 P0] 무덤 5종 이상");
        }

        [Test]
        public void Loader_Has_Cemetery_Third_Fallback()
        {
            // 소스계약 — RuntimeModelLoader 3차 폴백(UserProvided→Dungeon→Cemetery)
            var src = System.IO.File.ReadAllText(
                "Assets/Scripts/Systems/RuntimeModelLoader.cs");
            Assert.That(src, Does.Contain("Models/Cemetery/"),
                "[155차 P0] RuntimeModelLoader 3차 폴백 경로 존재");
            int iUser = src.IndexOf("Models/UserProvided/", System.StringComparison.Ordinal);
            int iDungeon = src.IndexOf("Models/Dungeon/", System.StringComparison.Ordinal);
            int iCemetery = src.IndexOf("Models/Cemetery/", System.StringComparison.Ordinal);
            Assert.That(iUser < iDungeon && iDungeon < iCemetery, Is.True,
                "[155차 P0] 폴백 순서 계약 — UserProvided → Dungeon → Cemetery");
        }
    }
}
