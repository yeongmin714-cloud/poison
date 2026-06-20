using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// Phase 31: 영지 점령 상징 교체 시스템 EditMode 테스트.
    /// </summary>
    public class Phase31_EmblemTests
    {
        [Test]
        public void PlayerEmblemData_DefaultValues()
        {
            var emblem = new PlayerEmblemData();
            Assert.AreEqual("내 문장", emblem.emblemName);
            Assert.AreEqual(EmblemShape.Shield, emblem.shape);
            Assert.AreEqual(EmblemColor.Gold, emblem.primaryColor);
            Assert.AreEqual(EmblemColor.Red, emblem.secondaryColor);
        }

        [Test]
        public void PlayerEmblemData_Clone_IsIndependent()
        {
            var original = new PlayerEmblemData
            {
                emblemName = "테스트",
                shape = EmblemShape.Dragon,
                primaryColor = EmblemColor.Purple
            };

            var clone = original.Clone();
            clone.emblemName = "변경됨";

            Assert.AreEqual("테스트", original.emblemName);
            Assert.AreEqual("변경됨", clone.emblemName);
        }

        [Test]
        public void EmblemManager_Singleton_Works()
        {
            var go = new GameObject("TestEmblem");
            var manager = go.AddComponent<EmblemManager>();

            Assert.IsNotNull(EmblemManager.Instance);
            Assert.AreEqual(manager, EmblemManager.Instance);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void EmblemManager_ChangeCost_Returns100()
        {
            var go = new GameObject("TestEmblem");
            var manager = go.AddComponent<EmblemManager>();

            Assert.AreEqual(100, manager.ChangeCost);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void EmblemManager_ChangeEmblem_WithEnoughGold()
        {
            var go = new GameObject("TestEmblem");
            var manager = go.AddComponent<EmblemManager>();

            var newEmblem = new PlayerEmblemData
            {
                emblemName = "용의 문장",
                shape = EmblemShape.Dragon,
                primaryColor = EmblemColor.Red
            };

            bool result = manager.ChangeEmblem(newEmblem, 200);
            Assert.IsTrue(result);
            Assert.AreEqual("용의 문장", manager.CurrentEmblem.emblemName);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void EmblemManager_ChangeEmblem_WithoutEnoughGold()
        {
            var go = new GameObject("TestEmblem");
            var manager = go.AddComponent<EmblemManager>();

            var newEmblem = new PlayerEmblemData { emblemName = "테스트", shape = EmblemShape.Sword };
            bool result = manager.ChangeEmblem(newEmblem, 50);

            Assert.IsFalse(result);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void EmblemManager_GetEmblemColor_ReturnsCorrect()
        {
            Assert.AreEqual(Color.red, EmblemManager.GetEmblemColor(EmblemColor.Red));
            Assert.AreEqual(Color.blue, EmblemManager.GetEmblemColor(EmblemColor.Blue));
            Assert.AreEqual(Color.white, EmblemManager.GetEmblemColor(EmblemColor.White));
            Assert.AreEqual(new Color(0.2f, 0.2f, 0.2f), EmblemManager.GetEmblemColor(EmblemColor.Black));
        }

        [Test]
        public void EmblemManager_GetEmblemSymbol_AllShapes()
        {
            var shapes = (EmblemShape[])System.Enum.GetValues(typeof(EmblemShape));
            Assert.AreEqual(10, shapes.Length);
            foreach (var s in shapes)
            {
                Assert.IsNotEmpty(EmblemManager.GetEmblemSymbol(s));
            }
        }

        [Test]
        public void EmblemManager_CreateFlagMaterial_ReturnsMaterial()
        {
            var go = new GameObject("TestEmblem");
            var manager = go.AddComponent<EmblemManager>();

            var mat = manager.CreateFlagMaterial();
            Assert.IsNotNull(mat);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void TerritoryBannerSystem_GetOccupationMessage_ReturnsFormatted()
        {
            // EmblemManager 없이도 동작해야 함
            var msg = TerritoryBannerSystem.GetOccupationMessage("테스트 영지");
            Assert.IsNotEmpty(msg);
            Assert.IsTrue(msg.Contains("테스트 영지"));
        }

        [Test]
        public void EmblemManager_EmblemColors_AllDefined()
        {
            var colors = (EmblemColor[])System.Enum.GetValues(typeof(EmblemColor));
            Assert.AreEqual(8, colors.Length);
            foreach (var c in colors)
            {
                var color = EmblemManager.GetEmblemColor(c);
                Assert.IsTrue(color.a > 0);
            }
        }
    }
}