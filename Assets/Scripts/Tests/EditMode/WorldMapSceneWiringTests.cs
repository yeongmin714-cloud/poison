// [114차] Test_15 월드맵 배선 계약 테스트 — 소스 레벨 계약 검증.
// 목적: TestTerritoryCombatSetup.SetupUITestArena ensure 목록에 WorldMapWindowUTK.Ensure가
//       배선되어 있는지 + 부트 로그 갱신 여부 + WorldMapWindowUTK 자체 폴링(M키) 계약을
//       소스 텍스트 상수로 단언한다. (런타임 렌더 판정은 QA 서브 몫)
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    [TestFixture]
    public class WorldMapSceneWiringTests
    {
        private const string SetupSourcePath = "Assets/Scripts/Systems/TestTerritoryCombatSetup.cs";
        private const string WorldMapSourcePath = "Assets/Scripts/UI/Toolkit/WorldMapWindowUTK.cs";

        private string ReadSource(string path)
        {
            var full = Path.Combine(Application.dataPath, "..", path);
            Assert.IsTrue(File.Exists(full), $"소스 파일 없음: {path}");
            return File.ReadAllText(full);
        }

        [Test]
        public void SetupArena_ContainsWorldMapEnsureLine()
        {
            string src = ReadSource(SetupSourcePath);
            StringAssert.Contains("InvokeUtkStatic(uiAsm, \"ProjectName.UI.Toolkit.WorldMapWindowUTK\", \"Ensure\")", src);
        }

        [Test]
        public void SetupArena_ContainsUpdatedBootLog()
        {
            string src = ReadSource(SetupSourcePath);
            StringAssert.Contains("Journal/Clock/WorldMap + wire-up", src);
        }

        [Test]
        public void WorldMapWindowUTK_ContainsEnsureMethod()
        {
            string src = ReadSource(WorldMapSourcePath);
            StringAssert.Contains("public static void Ensure()", src);
        }

        [Test]
        public void WorldMapWindowUTK_ContainsMKeyPolling_AndSetupCallsArena()
        {
            string wmap = ReadSource(WorldMapSourcePath);
            StringAssert.Contains("kb.mKey.wasPressedThisFrame", wmap);

            string setup = ReadSource(SetupSourcePath);
            // [147차] 계약 승계 — SetupUITestArena 호출이 P0 계측 래핑(P0Time)으로 변경(호출 유지)
            StringAssert.Contains("P0Time(\"UTK\", SetupUITestArena);", setup);
        }
    }
}
