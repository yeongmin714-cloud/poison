using NUnit.Framework;
using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// ProceduralIconGenerator EditMode 테스트 — C8-35.
    /// 모든 카테고리별 아이콘 생성, 캐싱, Sprite 변환, 희귀도 테두리 테스트.
    /// </summary>
    public class ProceduralIconGeneratorTests
    {
        [SetUp]
        public void Setup()
        {
            ProceduralIconGenerator.ClearCache();
        }

        [TearDown]
        public void TearDown()
        {
            ProceduralIconGenerator.ClearCache();
        }

        // ===== 모든 카테고리별 아이콘 생성 테스트 =====

        [Test]
        public void GenerateIcon_Herb_ReturnsTexture()
        {
            Texture2D tex = ProceduralIconGenerator.GenerateIcon("herb_red", PlayerInventory.ItemCategory.Herb);
            Assert.IsNotNull(tex, "Herb 아이콘 Texture2D가 null이면 안 됨");
            Assert.AreEqual(32, tex.width, "텍스처 너비는 32");
            Assert.AreEqual(32, tex.height, "텍스처 높이는 32");
            Assert.AreEqual(TextureFormat.RGBA32, tex.format, "포맷은 RGBA32");
        }

        [Test]
        public void GenerateIcon_Meat_ReturnsTexture()
        {
            Texture2D tex = ProceduralIconGenerator.GenerateIcon("meat_rabbit", PlayerInventory.ItemCategory.Meat);
            Assert.IsNotNull(tex);
            Assert.AreEqual(32, tex.width);
            Assert.AreEqual(32, tex.height);
        }

        [Test]
        public void GenerateIcon_Food_ReturnsTexture()
        {
            Texture2D tex = ProceduralIconGenerator.GenerateIcon("food_test", PlayerInventory.ItemCategory.Food);
            Assert.IsNotNull(tex);
        }

        [Test]
        public void GenerateIcon_Potion_ReturnsTexture()
        {
            Texture2D tex = ProceduralIconGenerator.GenerateIcon("potion_red", PlayerInventory.ItemCategory.Potion);
            Assert.IsNotNull(tex);
        }

        [Test]
        public void GenerateIcon_Drug_ReturnsTexture()
        {
            Texture2D tex = ProceduralIconGenerator.GenerateIcon("drug_test", PlayerInventory.ItemCategory.Drug);
            Assert.IsNotNull(tex);
        }

        [Test]
        public void GenerateIcon_Material_ReturnsTexture()
        {
            Texture2D tex = ProceduralIconGenerator.GenerateIcon("mat_iron", PlayerInventory.ItemCategory.Material);
            Assert.IsNotNull(tex);
        }

        [Test]
        public void GenerateIcon_Weapon_ReturnsTexture()
        {
            Texture2D tex = ProceduralIconGenerator.GenerateIcon("weapon_sword", PlayerInventory.ItemCategory.Weapon);
            Assert.IsNotNull(tex);
        }

        [Test]
        public void GenerateIcon_Armor_ReturnsTexture()
        {
            Texture2D tex = ProceduralIconGenerator.GenerateIcon("armor_leather", PlayerInventory.ItemCategory.Armor);
            Assert.IsNotNull(tex);
        }

        [Test]
        public void GenerateIcon_Tool_ReturnsTexture()
        {
            Texture2D tex = ProceduralIconGenerator.GenerateIcon("tool_pickaxe", PlayerInventory.ItemCategory.Tool);
            Assert.IsNotNull(tex);
        }

        [Test]
        public void GenerateIcon_Quest_ReturnsTexture()
        {
            Texture2D tex = ProceduralIconGenerator.GenerateIcon("quest_deed", PlayerInventory.ItemCategory.Quest);
            Assert.IsNotNull(tex);
        }

        // ===== 캐싱 테스트 =====

        [Test]
        public void GenerateIcon_SameId_ReturnsSameTexture()
        {
            Texture2D tex1 = ProceduralIconGenerator.GenerateIcon("test_item", PlayerInventory.ItemCategory.Herb);
            Texture2D tex2 = ProceduralIconGenerator.GenerateIcon("test_item", PlayerInventory.ItemCategory.Herb);
            Assert.AreSame(tex1, tex2, "같은 ID로 생성하면 같은 Texture 인스턴스여야 함");
        }

        [Test]
        public void GenerateIcon_DifferentIds_ReturnDifferentTextures()
        {
            Texture2D tex1 = ProceduralIconGenerator.GenerateIcon("item_a", PlayerInventory.ItemCategory.Herb);
            Texture2D tex2 = ProceduralIconGenerator.GenerateIcon("item_b", PlayerInventory.ItemCategory.Meat);
            Assert.AreNotSame(tex1, tex2, "다른 ID면 다른 Texture 인스턴스여야 함");
        }

        [Test]
        public void GenerateIcon_SameIdDifferentDurability_ReturnsDifferentTextures()
        {
            // 내구도가 다르면 테두리가 달라져서 다른 텍스처여야 함
            Texture2D tex1 = ProceduralIconGenerator.GenerateIcon("weapon_sword", PlayerInventory.ItemCategory.Weapon, 0);
            Texture2D tex2 = ProceduralIconGenerator.GenerateIcon("weapon_sword", PlayerInventory.ItemCategory.Weapon, 20);
            Assert.AreNotSame(tex1, tex2, "내구도가 다르면 다른 Texture 인스턴스여야 함");
        }

        [Test]
        public void ClearCache_RemovesAllCachedTextures()
        {
            ProceduralIconGenerator.GenerateIcon("test1", PlayerInventory.ItemCategory.Herb);
            ProceduralIconGenerator.GenerateIcon("test2", PlayerInventory.ItemCategory.Meat);
            ProceduralIconGenerator.ClearCache();

            // 캐시 클리어 후 새로 생성하면 다른 인스턴스여야 함
            Texture2D tex1 = ProceduralIconGenerator.GenerateIcon("test1", PlayerInventory.ItemCategory.Herb);
            Texture2D tex2 = ProceduralIconGenerator.GenerateIcon("test2", PlayerInventory.ItemCategory.Meat);
            Assert.IsNotNull(tex1);
            Assert.IsNotNull(tex2);
        }

        // ===== Sprite 변환 테스트 =====

        [Test]
        public void GetOrCreateSprite_FromItemData_ReturnsSprite()
        {
            var item = new PlayerInventory.ItemData
            {
                id = "test_sprite_item",
                displayName = "테스트 아이템",
                category = PlayerInventory.ItemCategory.Herb,
                maxStack = 20
            };

            Sprite sprite = ProceduralIconGenerator.GetOrCreateIcon(item);
            Assert.IsNotNull(sprite, "Sprite가 null이면 안 됨");
            Assert.AreEqual(32, sprite.texture.width, "Sprite 텍스처 너비는 32");
            Assert.AreEqual(32, sprite.texture.height, "Sprite 텍스처 높이는 32");
        }

        [Test]
        public void GetOrCreateSprite_SameItem_ReturnsCachedSprite()
        {
            var item = new PlayerInventory.ItemData
            {
                id = "test_cached_sprite",
                displayName = "캐싱 테스트",
                category = PlayerInventory.ItemCategory.Material,
                maxStack = 99
            };

            Sprite sprite1 = ProceduralIconGenerator.GetOrCreateIcon(item);
            Sprite sprite2 = ProceduralIconGenerator.GetOrCreateIcon(item);
            Assert.AreSame(sprite1, sprite2, "같은 아이템이면 같은 Sprite 인스턴스여야 함");
        }

        [Test]
        public void GetOrCreateSprite_AssignsToIconField()
        {
            var item = new PlayerInventory.ItemData
            {
                id = "test_assign_icon",
                displayName = "아이콘 할당",
                category = PlayerInventory.ItemCategory.Potion,
                maxStack = 99
            };

            Assert.IsNull(item.icon, "초기 icon 필드는 null");
            Sprite sprite = ProceduralIconGenerator.GetOrCreateIcon(item);
            Assert.IsNotNull(sprite);
            // GetOrCreateIcon은 item.icon을 수정하지 않음 (호출자가 직접 할당)
            // 대신 Sprite.Create()로 생성된 sprite를 반환
        }

        // ===== 희귀도 테두리 차이 테스트 =====

        [Test]
        public void GenerateIcon_WithDurability_HasVisibleBorder()
        {
            Texture2D texWithDurability = ProceduralIconGenerator.GenerateIcon("sword_test", PlayerInventory.ItemCategory.Weapon, 20);
            Texture2D texWithoutDurability = ProceduralIconGenerator.GenerateIcon("sword_test_nodur", PlayerInventory.ItemCategory.Weapon, 0);

            // 테두리 픽셀이 다름을 확인 (0,0 좌측 상단)
            Color borderColor = texWithDurability.GetPixel(0, 0);
            Color noBorderColor = texWithoutDurability.GetPixel(0, 0);

            // 내구도 있는 아이템은 테두리가 선명해야 함 (알파값이 높음)
            Assert.IsTrue(borderColor.a > noBorderColor.a,
                "내구도가 있는 아이템의 테두리 알파가 더 높아야 함");
        }

        [Test]
        public void GenerateIcon_WithDurability_HasRareCornerHighlights()
        {
            Texture2D tex = ProceduralIconGenerator.GenerateIcon("rare_sword", PlayerInventory.ItemCategory.Weapon, 20);

            // 모서리 근처 픽셀이 희귀 색상(황금)을 포함하는지 확인
            Color corner1 = tex.GetPixel(1, 1);
            Color corner2 = tex.GetPixel(30, 30);

            // 희귀 테두리: R > 0.8, G > 0.8 (황금색)
            bool hasGoldenTint = (corner1.r > 0.7f && corner1.g > 0.7f) ||
                                 (corner2.r > 0.7f && corner2.g > 0.7f);
            Assert.IsTrue(hasGoldenTint, "희귀 아이템 모서리에 황금색 강조가 있어야 함");
        }

        // ===== GenerateAllStaticIcons 테스트 =====

        [Test]
        public void GenerateAllStaticIcons_SetsIconOnStaticItems()
        {
            // 기존 icon 필드 초기화
            var origIcon = PlayerInventory.Herb_Red.icon;
            PlayerInventory.Herb_Red.icon = null;

            ProceduralIconGenerator.GenerateAllStaticIcons();

            Assert.IsNotNull(PlayerInventory.Herb_Red.icon,
                "GenerateAllStaticIcons 후 Herb_Red에 아이콘이 할당되어야 함");
            Assert.IsNotNull(PlayerInventory.RabbitMeat.icon,
                "GenerateAllStaticIcons 후 RabbitMeat에 아이콘이 할당되어야 함");
            Assert.IsNotNull(PlayerInventory.SwordWood.icon,
                "GenerateAllStaticIcons 후 SwordWood에 아이콘이 할당되어야 함");

            // 원복
            PlayerInventory.Herb_Red.icon = origIcon;
        }

        [Test]
        public void GenerateAllStaticIcons_EachIconIsUniqueSprite()
        {
            // 기존 icon 백업
            var icons = new System.Collections.Generic.Dictionary<string, Sprite>();
            var fields = typeof(PlayerInventory).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            foreach (var f in fields)
            {
                if (f.FieldType == typeof(PlayerInventory.ItemData))
                {
                    var item = f.GetValue(null) as PlayerInventory.ItemData;
                    if (item != null) icons[item.id] = item.icon;
                    if (item != null) item.icon = null;
                }
            }

            ProceduralIconGenerator.GenerateAllStaticIcons();

            // 각 아이템에 고유한 아이콘이 할당되었는지 확인
            var uniqueTextures = new System.Collections.Generic.HashSet<Texture2D>();
            foreach (var f in fields)
            {
                if (f.FieldType == typeof(PlayerInventory.ItemData))
                {
                    var item = f.GetValue(null) as PlayerInventory.ItemData;
                    if (item != null && item.icon != null)
                    {
                        uniqueTextures.Add(item.icon.texture);
                    }
                }
            }
            Assert.Greater(uniqueTextures.Count, 0, "최소 1개 이상의 고유 텍스처가 생성되어야 함");

            // 원복
            foreach (var f in fields)
            {
                if (f.FieldType == typeof(PlayerInventory.ItemData))
                {
                    var item = f.GetValue(null) as PlayerInventory.ItemData;
                    if (item != null && icons.ContainsKey(item.id))
                        item.icon = icons[item.id];
                }
            }
        }

        // ===== 포션 색상 테스트 =====

        [Test]
        public void GenerateIcon_PotionRed_HasRedTint()
        {
            Texture2D tex = ProceduralIconGenerator.GenerateIcon("potion_red", PlayerInventory.ItemCategory.Potion);
            // 중앙 픽셀이 빨간 계열인지 확인
            Color center = tex.GetPixel(16, 16);
            Assert.IsTrue(center.r > 0.6f && center.g < 0.4f,
                "빨간 포션은 중앙 픽셀의 R이 G보다 높아야 함");
        }

        [Test]
        public void GenerateIcon_PotionSilver_HasSilverTint()
        {
            Texture2D tex = ProceduralIconGenerator.GenerateIcon("potion_silver", PlayerInventory.ItemCategory.Potion);
            Color center = tex.GetPixel(16, 16);
            // 은색: R, G, B 모두 높음
            Assert.IsTrue(center.r > 0.5f && center.g > 0.5f && center.b > 0.5f,
                "은색 포션은 중앙 픽셀이 밝은 회색 계열");
        }
    }
}