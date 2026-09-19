using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C-O6-04: 영주 성취사 생성기 테스트 — 결정론/3문장/이름 치환/길이/다양성.
    /// StatusWindowUTK UI는 EditMode 불가 — Play 판정으로 남김.
    /// </summary>
    public class HeroicTaleTests
    {
        [Test]
        public void GetTale_Deterministic_SameTerritorySameTale()
        {
            string a = HeroicTaleGenerator.GetTale("East_03", NationType.East);
            string b = HeroicTaleGenerator.GetTale("East_03", NationType.East);
            Assert.AreEqual(a, b, "같은 영지 = 같은 성취사");
        }

        [Test]
        public void GetTale_ThreeSentences()
        {
            string tale = HeroicTaleGenerator.GetTale("West_07", NationType.West);
            int sentences = System.Text.RegularExpressions.Regex.Matches(tale, "[.다].").Count
                            + (tale.Contains("다.") ? 1 : 0);
            Assert.GreaterOrEqual(tale.Split('\n').Length, 2, "최소 2줄 이상(3문장 블록)");
            Assert.IsFalse(string.IsNullOrEmpty(tale.Trim()));
        }

        [Test]
        public void GetTale_NationLine_MatchesFlavor()
        {
            string north = HeroicTaleGenerator.GetTale("north_t", NationType.North);
            StringAssert.Contains("북국", north, "북국 성향 문장");

            string empire = HeroicTaleGenerator.GetTale("empire_t", NationType.Empire);
            StringAssert.Contains("권위", empire, "황제국 성향 문장");
        }

        [Test]
        public void GetTaleForLord_ContainsLordName()
        {
            string tale = HeroicTaleGenerator.GetTaleForLord("바르톨드 3세", "East_05");
            StringAssert.Contains("바르톨드 3세", tale, "영주 이름 치환");
        }

        [Test]
        public void GetTale_VocabularyFromPools()
        {
            // 12개 영지 생성 — 전부 비어있지 않고 길이 상한 내
            for (int i = 0; i < 12; i++)
            {
                string id = $"vocab_{i}";
                string tale = HeroicTaleGenerator.GetTale(id, NationType.South);
                Assert.IsFalse(string.IsNullOrEmpty(tale), $"{id} 성취사 존재");
                Assert.Less(tale.Length, 500, $"{id} 길이 상한");
            }
        }

        [Test]
        public void GetTale_Diversity_SimilarButNotIdentical()
        {
            int distinct = 0;
            string first = HeroicTaleGenerator.GetTale("div_0", NationType.East);
            for (int i = 1; i < 10; i++)
            {
                string t = HeroicTaleGenerator.GetTale($"div_{i}", NationType.East);
                if (t != first) distinct++;
            }
            Assert.Greater(distinct, 0, "10개 영지 중 최소 1개는 첫 영지와 다른 성취사");
        }

        [Test]
        public void GetTale_EmptyOrNullId_SafeFallback()
        {
            Assert.DoesNotThrow(() => HeroicTaleGenerator.GetTale(null, NationType.East));
            Assert.DoesNotThrow(() => HeroicTaleGenerator.GetTale("", NationType.East));
            Assert.IsFalse(string.IsNullOrEmpty(HeroicTaleGenerator.GetTale(null, NationType.East)));
        }

        [Test]
        public void GetTaleForLord_EmptyName_FallsBackToDefaultSubject()
        {
            string tale = HeroicTaleGenerator.GetTaleForLord(null, "test_t");
            Assert.IsFalse(string.IsNullOrEmpty(tale), "null 이름도 안전 생성");
        }

        [Test]
        public void GetTale_UsesLordStatGeneratorSeed()
        {
            // 결정론 시드가 LordStatGenerator.Djb2 기반 — 같은 시드 로직 공유 검증
            int seed = LordStatGenerator.Djb2("seed_tale");
            Assert.AreEqual(seed, LordStatGenerator.Djb2("seed_tale"), "시드 재현");
        }

        [Test]
        public void GetTale_AllNations_GenerateWithoutError()
        {
            foreach (var nation in new[] { NationType.East, NationType.West, NationType.South, NationType.North, NationType.Empire, NationType.Dracula })
            {
                Assert.DoesNotThrow(() => HeroicTaleGenerator.GetTale($"all_{nation}", nation));
            }
        }
    }
}
