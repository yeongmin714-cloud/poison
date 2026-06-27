using NUnit.Framework;
using ProjectName.Core;
using ProjectName.UI;
using UnityEngine;

public class TooltipTests
{
    private PlayerInventory.ItemData _testItemData;
    private PlayerInventory.ItemSlot _testSlot;
    private GameObject _tooltipGo;
    private TooltipWindow _tooltip;

    [SetUp]
    public void SetUp()
    {
        _testItemData = new PlayerInventory.ItemData
        {
            id = "test_item",
            displayName = "테스트 아이템",
            description = "이것은 테스트 아이템입니다",
            effects = "HP +20",
            rarity = ItemRarity.Rare,
            category = PlayerInventory.ItemCategory.Potion,
            maxDurability = 50,
            maxStack = 99
        };

        _testSlot = new PlayerInventory.ItemSlot
        {
            item = _testItemData,
            count = 3,
            currentDurability = 30
        };

        _tooltipGo = new GameObject("TooltipWindow");
        _tooltip = _tooltipGo.AddComponent<TooltipWindow>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_tooltipGo != null)
            Object.DestroyImmediate(_tooltipGo);
    }

    [Test]
    public void ItemTooltipData_FromSlot_IsValid()
    {
        var data = _testSlot.ToTooltipData();
        Assert.IsTrue(data.IsValid);
        Assert.AreEqual("테스트 아이템", data.itemName);
        Assert.AreEqual("이것은 테스트 아이템입니다", data.description);
        Assert.AreEqual("HP +20", data.effects);
        Assert.AreEqual(ItemRarity.Rare, data.rarity);
        Assert.AreEqual(PlayerInventory.ItemCategory.Potion, data.category);
    }

    [Test]
    public void ItemTooltipData_FromSlot_HasDurability()
    {
        var data = _testSlot.ToTooltipData();
        Assert.IsTrue(data.hasDurability);
        Assert.AreEqual(50, data.maxDurability);
        Assert.AreEqual(30, data.currentDurability);
        Assert.AreEqual(0.6f, data.durabilityRatio, 0.01f);
    }

    [Test]
    public void ItemTooltipData_FromSlot_Count()
    {
        var data = _testSlot.ToTooltipData();
        Assert.AreEqual(3, data.count);
    }

    [Test]
    public void ItemTooltipData_FromItemData_IsValid()
    {
        var data = _testItemData.ToTooltipData();
        Assert.IsTrue(data.IsValid);
        Assert.AreEqual("테스트 아이템", data.itemName);
        Assert.AreEqual(1, data.count);
    }

    [Test]
    public void ItemTooltipData_NullSlot_ReturnsDefault()
    {
        var data = ((PlayerInventory.ItemSlot)null).ToTooltipData();
        Assert.IsFalse(data.IsValid);
    }

    [Test]
    public void ItemTooltipData_NullItemData_ReturnsDefault()
    {
        var data = ((PlayerInventory.ItemData)null).ToTooltipData();
        Assert.IsFalse(data.IsValid);
    }

    [Test]
    public void ItemTooltipData_RarityDisplayName()
    {
        Assert.AreEqual("일반", ItemTooltipData.GetRarityDisplayName(ItemRarity.Common));
        Assert.AreEqual("고급", ItemTooltipData.GetRarityDisplayName(ItemRarity.Uncommon));
        Assert.AreEqual("희귀", ItemTooltipData.GetRarityDisplayName(ItemRarity.Rare));
        Assert.AreEqual("영웅", ItemTooltipData.GetRarityDisplayName(ItemRarity.Epic));
        Assert.AreEqual("전설", ItemTooltipData.GetRarityDisplayName(ItemRarity.Legendary));
        Assert.AreEqual("유니크", ItemTooltipData.GetRarityDisplayName(ItemRarity.Unique));
    }

    [Test]
    public void ItemTooltipData_RarityBorderColor()
    {
        Assert.AreEqual(new Color(0.8f, 0.8f, 0.8f, 1f), ItemTooltipData.GetRarityBorderColor(ItemRarity.Common));
        Assert.AreEqual(new Color(1.0f, 0.7f, 0.1f, 1f), ItemTooltipData.GetRarityBorderColor(ItemRarity.Legendary));
        Assert.AreEqual(new Color(1.0f, 0.85f, 0.0f, 1f), ItemTooltipData.GetRarityBorderColor(ItemRarity.Unique));
    }

    [Test]
    public void ItemTooltipData_DurabilityColor()
    {
        Assert.AreEqual(Color.green, ItemTooltipData.GetDurabilityColor(0.8f));
        Assert.AreEqual(Color.yellow, ItemTooltipData.GetDurabilityColor(0.5f));
        Assert.AreEqual(Color.red, ItemTooltipData.GetDurabilityColor(0.2f));
    }

    [Test]
    public void TooltipWindow_ShowTooltip_ValidData()
    {
        var data = _testSlot.ToTooltipData();
        // ShowTooltip sets internal state - just verify no exception
        Assert.DoesNotThrow(() => _tooltip.ShowTooltip(data, new Vector2(100, 100)));
    }

    [Test]
    public void TooltipWindow_ShowTooltip_InvalidData_NoEffect()
    {
        var data = new ItemTooltipData(); // invalid: empty itemName
        Assert.DoesNotThrow(() => _tooltip.ShowTooltip(data, new Vector2(100, 100)));
    }

    [Test]
    public void TooltipWindow_ForceHide_ClearsState()
    {
        var data = _testSlot.ToTooltipData();
        _tooltip.ShowTooltip(data, new Vector2(100, 100));
        _tooltip.ForceHide();
        Assert.IsFalse(_tooltip.IsShowing);
        Assert.IsFalse(_tooltip.IsHovering);
    }

    [Test]
    public void TooltipWindow_IsShowing_InitiallyFalse()
    {
        Assert.IsFalse(_tooltip.IsShowing);
    }

    [Test]
    public void ItemTooltipData_NoDurability()
    {
        var noDurItem = new PlayerInventory.ItemData
        {
            id = "potion",
            displayName = "포션",
            description = "체력 회복",
            maxDurability = 0
        };
        var data = noDurItem.ToTooltipData();
        Assert.IsFalse(data.hasDurability);
        Assert.AreEqual(1f, data.durabilityRatio, 0.01f);
    }
}
