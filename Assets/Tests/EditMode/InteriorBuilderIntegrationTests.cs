using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C11-14: Interior Builder 통합 테스트.
    /// 모든 Builder 호출 테스트 + 자식 GameObject 존재 확인 + Material/Texture null 체크.
    /// </summary>
    public class InteriorBuilderIntegrationTests
    {
        private Shader _litShader;

        [SetUp]
        public void Setup()
        {
            _litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (_litShader == null)
            {
                _litShader = Shader.Find("Standard");
            }
            Assert.IsNotNull(_litShader, "셰이더를 찾을 수 없음 — Lit 또는 Standard 필요");
        }

        [TearDown]
        public void TearDown()
        {
            // 생성된 모든 GameObject 정리
            var objects = Object.FindObjectsOfType<GameObject>();
            foreach (var obj in objects)
            {
                if (obj.name.Contains("Room") || obj.name.Contains("Table") ||
                    obj.name.Contains("Shelf") || obj.name.Contains("Chair") ||
                    obj.name.Contains("Bed") || obj.name.Contains("Counter") ||
                    obj.name.Contains("PointLight") || obj.name.Contains("Pillar") ||
                    obj.name.Contains("Bench") || obj.name.Contains("StainedGlass") ||
                    obj.name.Contains("Altar") || obj.name.Contains("Forge") ||
                    obj.name.Contains("Stove") || obj.name.Contains("Meeting") ||
                    obj.name.Contains("Throne") || obj.name.Contains("Craft") ||
                    obj.name.Contains("Church") || obj.name.Contains("House") ||
                    obj.name.Contains("Castle") || obj.name.Contains("Shop") ||
                    obj.name.Contains("FlickerRoutine"))
                {
                    Object.DestroyImmediate(obj);
                }
            }
        }

        // ===================================================================
        // C11-08: 크래프트하우스
        // ===================================================================

        [Test]
        public void CraftHouseInteriorBuilder_ReturnsGameObject()
        {
            GameObject result = CraftHouseInteriorBuilder.BuildCraftHouseInterior();
            Assert.IsNotNull(result, "BuildCraftHouseInterior()가 null을 반환함");
        }

        [Test]
        public void CraftHouseInteriorBuilder_HasChildren()
        {
            GameObject result = CraftHouseInteriorBuilder.BuildCraftHouseInterior();
            Assert.Greater(result.transform.childCount, 0, "크래프트하우스 방에 자식 GameObject가 없음");
        }

        [Test]
        public void CraftHouseInteriorBuilder_MaterialsNotNull()
        {
            GameObject result = CraftHouseInteriorBuilder.BuildCraftHouseInterior();
            var renderers = result.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in renderers)
            {
                Assert.IsNotNull(renderer.sharedMaterial, $"MeshRenderer {renderer.name}의 Material이 null");
                if (renderer.sharedMaterial.mainTexture != null)
                {
                    Assert.IsNotNull(renderer.sharedMaterial.mainTexture,
                        $"{renderer.name}의 mainTexture가 null");
                }
            }
        }

        // ===================================================================
        // C11-09: 교회
        // ===================================================================

        [Test]
        public void ChurchInteriorBuilder_ReturnsGameObject()
        {
            GameObject result = ChurchInteriorBuilder.BuildChurchInterior();
            Assert.IsNotNull(result, "BuildChurchInterior()가 null을 반환함");
        }

        [Test]
        public void ChurchInteriorBuilder_HasChildren()
        {
            GameObject result = ChurchInteriorBuilder.BuildChurchInterior();
            Assert.Greater(result.transform.childCount, 0, "교회 방에 자식 GameObject가 없음");
        }

        [Test]
        public void ChurchInteriorBuilder_MaterialsNotNull()
        {
            GameObject result = ChurchInteriorBuilder.BuildChurchInterior();
            var renderers = result.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in renderers)
            {
                Assert.IsNotNull(renderer.sharedMaterial, $"MeshRenderer {renderer.name}의 Material이 null");
            }
        }

        [Test]
        public void ChurchInteriorBuilder_HasStainedGlass()
        {
            GameObject result = ChurchInteriorBuilder.BuildChurchInterior();
            var stainedGlass = result.GetComponentsInChildren<MeshRenderer>();
            bool foundGlass = false;
            foreach (var renderer in stainedGlass)
            {
                if (renderer.name.Contains("StainedGlass"))
                {
                    foundGlass = true;
                    break;
                }
            }
            Assert.IsTrue(foundGlass, "교회에 스테인드글라스(StainedGlass)가 없음");
        }

        // ===================================================================
        // C11-10: NPC 주택
        // ===================================================================

        [Test]
        public void HouseInteriorBuilder_ReturnsGameObject()
        {
            GameObject result = HouseInteriorBuilder.BuildHouseInterior();
            Assert.IsNotNull(result, "BuildHouseInterior()가 null을 반환함");
        }

        [Test]
        public void HouseInteriorBuilder_HasChildren()
        {
            GameObject result = HouseInteriorBuilder.BuildHouseInterior();
            Assert.Greater(result.transform.childCount, 0, "주택 방에 자식 GameObject가 없음");
        }

        [Test]
        public void HouseInteriorBuilder_MaterialsNotNull()
        {
            GameObject result = HouseInteriorBuilder.BuildHouseInterior();
            var renderers = result.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in renderers)
            {
                Assert.IsNotNull(renderer.sharedMaterial, $"MeshRenderer {renderer.name}의 Material이 null");
            }
        }

        // ===================================================================
        // C11-07: 상점 (기존 Builder)
        // ===================================================================

        [Test]
        public void ShopInteriorBuilder_ReturnsGameObject()
        {
            GameObject result = ShopInteriorBuilder.BuildShopInterior();
            Assert.IsNotNull(result, "BuildShopInterior()가 null을 반환함");
        }

        [Test]
        public void ShopInteriorBuilder_HasChildren()
        {
            GameObject result = ShopInteriorBuilder.BuildShopInterior();
            Assert.Greater(result.transform.childCount, 0, "상점 방에 자식 GameObject가 없음");
        }

        [Test]
        public void ShopInteriorBuilder_MaterialsNotNull()
        {
            GameObject result = ShopInteriorBuilder.BuildShopInterior();
            var renderers = result.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in renderers)
            {
                Assert.IsNotNull(renderer.sharedMaterial, $"MeshRenderer {renderer.name}의 Material이 null");
            }
        }

        // ===================================================================
        // C11-12~13: 성 내부 (모든 국가 스타일)
        // ===================================================================

        [TestCase("Eastern")]
        [TestCase("Western")]
        [TestCase("Southern")]
        [TestCase("Northern")]
        [TestCase("Empire")]
        public void CastleInteriorBuilder_ReturnsGameObject(string nationStyle)
        {
            GameObject result = CastleInteriorBuilder.BuildCastleInterior(nationStyle);
            Assert.IsNotNull(result, $"BuildCastleInterior({nationStyle})가 null을 반환함");
        }

        [TestCase("Eastern")]
        [TestCase("Western")]
        [TestCase("Southern")]
        [TestCase("Northern")]
        [TestCase("Empire")]
        public void CastleInteriorBuilder_HasChildren(string nationStyle)
        {
            GameObject result = CastleInteriorBuilder.BuildCastleInterior(nationStyle);
            Assert.Greater(result.transform.childCount, 0, $"성({nationStyle}) 방에 자식 GameObject가 없음");
        }

        [TestCase("Eastern")]
        [TestCase("Western")]
        [TestCase("Southern")]
        [TestCase("Northern")]
        [TestCase("Empire")]
        public void CastleInteriorBuilder_MaterialsNotNull(string nationStyle)
        {
            GameObject result = CastleInteriorBuilder.BuildCastleInterior(nationStyle);
            var renderers = result.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in renderers)
            {
                Assert.IsNotNull(renderer.sharedMaterial, $"MeshRenderer {renderer.name}의 Material이 null");
            }
        }

        [TestCase("Eastern")]
        [TestCase("Western")]
        [TestCase("Southern")]
        [TestCase("Northern")]
        [TestCase("Empire")]
        public void CastleInteriorBuilder_HasPillars(string nationStyle)
        {
            GameObject result = CastleInteriorBuilder.BuildCastleInterior(nationStyle);
            var pillars = result.GetComponentsInChildren<MeshFilter>();
            bool foundPillar = false;
            foreach (var filter in pillars)
            {
                if (filter.name.Contains("Pillar"))
                {
                    foundPillar = true;
                    break;
                }
            }
            Assert.IsTrue(foundPillar, $"성({nationStyle})에 기둥(Pillar)이 없음");
        }

        // ===================================================================
        // C11-13: IndoorTextureGenerator 국가별 텍스처 null 체크
        // ===================================================================

        [Test]
        public void IndoorTextureGenerator_CastleFloorTexturesNotNull()
        {
            Assert.IsNotNull(IndoorTextureGenerator.GenerateCastleFloorEastern(), "GenerateCastleFloorEastern() null");
            Assert.IsNotNull(IndoorTextureGenerator.GenerateCastleFloorWestern(), "GenerateCastleFloorWestern() null");
            Assert.IsNotNull(IndoorTextureGenerator.GenerateCastleFloorSouthern(), "GenerateCastleFloorSouthern() null");
            Assert.IsNotNull(IndoorTextureGenerator.GenerateCastleFloorNorthern(), "GenerateCastleFloorNorthern() null");
            Assert.IsNotNull(IndoorTextureGenerator.GenerateCastleFloorEmpire(), "GenerateCastleFloorEmpire() null");
        }

        [Test]
        public void IndoorTextureGenerator_CastleWallTexturesNotNull()
        {
            Assert.IsNotNull(IndoorTextureGenerator.GenerateCastleWallEastern(), "GenerateCastleWallEastern() null");
            Assert.IsNotNull(IndoorTextureGenerator.GenerateCastleWallWestern(), "GenerateCastleWallWestern() null");
            Assert.IsNotNull(IndoorTextureGenerator.GenerateCastleWallSouthern(), "GenerateCastleWallSouthern() null");
            Assert.IsNotNull(IndoorTextureGenerator.GenerateCastleWallNorthern(), "GenerateCastleWallNorthern() null");
            Assert.IsNotNull(IndoorTextureGenerator.GenerateCastleWallEmpire(), "GenerateCastleWallEmpire() null");
        }

        [Test]
        public void IndoorTextureGenerator_ExistingPresetsNotNull()
        {
            Assert.IsNotNull(IndoorTextureGenerator.GenerateShopFloor(), "GenerateShopFloor() null");
            Assert.IsNotNull(IndoorTextureGenerator.GenerateShopWall(), "GenerateShopWall() null");
            Assert.IsNotNull(IndoorTextureGenerator.GenerateCraftHouseFloor(), "GenerateCraftHouseFloor() null");
            Assert.IsNotNull(IndoorTextureGenerator.GenerateCraftHouseWall(), "GenerateCraftHouseWall() null");
            Assert.IsNotNull(IndoorTextureGenerator.GenerateChurchFloor(), "GenerateChurchFloor() null");
            Assert.IsNotNull(IndoorTextureGenerator.GenerateChurchWall(), "GenerateChurchWall() null");
            Assert.IsNotNull(IndoorTextureGenerator.GenerateHouseFloor(), "GenerateHouseFloor() null");
            Assert.IsNotNull(IndoorTextureGenerator.GenerateHouseWall(), "GenerateHouseWall() null");
            Assert.IsNotNull(IndoorTextureGenerator.GenerateCastleFloor(), "GenerateCastleFloor() null");
            Assert.IsNotNull(IndoorTextureGenerator.GenerateCastleWall(), "GenerateCastleWall() null");
        }
    }
}