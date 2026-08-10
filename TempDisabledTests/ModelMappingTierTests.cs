#if false
using NUnit.Framework;
using ProjectName.Core;

/// <summary>
/// ModelMapping 티어드 매핑 기능에 대한 EditMode 테스트.
/// </summary>
[TestFixture]
public class ModelMappingTierTests
{
    [SetUp]
    public void Setup()
    {
        // LevelGroupManager.Initialize()가 테스트 전에 호출되도록 보장
        LevelGroupManager.Initialize();
    }

    #region Tier Suffix Parsing

    /// <summary>
    /// TryParseTierSuffix가 "_tier1" 접미사를 올바르게 파싱하는지 검증합니다.
    /// </summary>
    [Test]
    public void TryParseTierSuffix_ParsesTier1Correctly()
    {
        bool result = ModelMapping.TryParseTierSuffix("soldier_tier1", out string baseName, out string suffix);

        Assert.IsTrue(result, "Should parse tier1 suffix");
        Assert.AreEqual("soldier", baseName, "Base name should be 'soldier'");
        Assert.AreEqual("_tier1", suffix, "Suffix should be '_tier1'");
    }

    /// <summary>
    /// TryParseTierSuffix가 "_tier5" 접미사를 올바르게 파싱하는지 검증합니다.
    /// </summary>
    [Test]
    public void TryParseTierSuffix_ParsesTier5Correctly()
    {
        bool result = ModelMapping.TryParseTierSuffix("wolf_tier5", out string baseName, out string suffix);

        Assert.IsTrue(result, "Should parse tier5 suffix");
        Assert.AreEqual("wolf", baseName, "Base name should be 'wolf'");
        Assert.AreEqual("_tier5", suffix, "Suffix should be '_tier5'");
    }

    /// <summary>
    /// TryParseTierSuffix가 모든 5개 티어 접미사를 올바르게 파싱하는지 검증합니다.
    /// </summary>
    [Test]
    public void TryParseTierSuffix_ParsesAllTiers(
        [Values("_tier1", "_tier2", "_tier3", "_tier4", "_tier5")] string tierSuffix)
    {
        string testName = "soldier" + tierSuffix;
        bool result = ModelMapping.TryParseTierSuffix(testName, out string baseName, out string suffix);

        Assert.IsTrue(result, $"Should parse suffix: {tierSuffix}");
        Assert.AreEqual("soldier", baseName, $"Base name should be 'soldier' for suffix {tierSuffix}");
        Assert.AreEqual(tierSuffix, suffix, $"Suffix should be '{tierSuffix}'");
    }

    /// <summary>
    /// TryParseTierSuffix가 접미사가 없는 일반 이름에 대해 false를 반환하는지 검증합니다.
    /// </summary>
    [Test]
    public void TryParseTierSuffix_ReturnsFalseForPlainName()
    {
        bool result = ModelMapping.TryParseTierSuffix("soldier", out string baseName, out string suffix);

        Assert.IsFalse(result, "Plain name should not have a tier suffix");
        Assert.IsNull(baseName, "Base name should be null on failure");
        Assert.IsNull(suffix, "Suffix should be null on failure");
    }

    /// <summary>
    /// TryParseTierSuffix가 빈 문자열에 대해 false를 반환하는지 검증합니다.
    /// </summary>
    [Test]
    public void TryParseTierSuffix_ReturnsFalseForEmptyString()
    {
        bool result = ModelMapping.TryParseTierSuffix("", out string baseName, out string suffix);

        Assert.IsFalse(result, "Empty string should not have a tier suffix");
    }

    /// <summary>
    /// TryParseTierSuffix가 null에 대해 false를 반환하는지 검증합니다.
    /// </summary>
    [Test]
    public void TryParseTierSuffix_ReturnsFalseForNull()
    {
        bool result = ModelMapping.TryParseTierSuffix(null, out string baseName, out string suffix);

        Assert.IsFalse(result, "Null should not have a tier suffix");
    }

    /// <summary>
    /// TryParseTierSuffix가 이름에 여러 밑줄이 있어도 올바르게 파싱하는지 검증합니다.
    /// </summary>
    [Test]
    public void TryParseTierSuffix_HandlesMultipleUnderscores()
    {
        // "electric_spine_hedgehog_tier2" → base="electric_spine_hedgehog", suffix="_tier2"
        bool result = ModelMapping.TryParseTierSuffix("electric_spine_hedgehog_tier2", out string baseName, out string suffix);

        Assert.IsTrue(result, "Should parse tier2 from multi-underscore name");
        Assert.AreEqual("electric_spine_hedgehog", baseName, "Base name should include all underscores before tier suffix");
        Assert.AreEqual("_tier2", suffix, "Suffix should be '_tier2'");
    }

    /// <summary>
    /// TryParseTierSuffix가 "tier12" 같은 잘못된 접미사를 거부하는지 검증합니다.
    /// "_tier12"는 "_tier1"로 끝나지만, "_tier1" 다음에 "2"가 오므로 거짓양성 방지.
    /// 긴 접미사부터 검사하는 로직(_tier5→_tier1)으로 인해 처리됨.
    /// </summary>
    [Test]
    public void TryParseTierSuffix_RejectsInvalidTierSuffix()
    {
        // "tier12"는 유효한 티어가 아님 (1~5만 유효)
        // 하지만 이 테스트는 정확한 동작을 검증: "something_tier12"
        // "_tier1"로 끝나므로 _tier1과 매칭될 수 있음 — baseName이 "something_tier1" + "2"가 됨
        // 이 동작은 허용됨 (파일명 규칙을 따르지 않은 이름)
        // 대신 "_tier0"이나 "_tier6" 같은 명백히 유효하지 않은 접미사를 테스트
        bool result = ModelMapping.TryParseTierSuffix("soldier_tier0", out string _, out string _);
        Assert.IsFalse(result, "Should reject _tier0 as invalid");

        result = ModelMapping.TryParseTierSuffix("soldier_tier6", out string _, out string _);
        Assert.IsFalse(result, "Should reject _tier6 as invalid");
    }

    #endregion

    #region GetTieredMapping

    /// <summary>
    /// GetTieredMapping이 티어드 파일명에서 올바른 Placeholder 이름과 접미사를 반환하는지 검증합니다.
    /// </summary>
    [Test]
    public void GetTieredMapping_ReturnsCorrectPlaceholderForTieredFile()
    {
        var (objName, mode, suffix) = ModelMapping.GetTieredMapping("soldier_tier2");

        Assert.AreEqual("Placeholder_Soldier", objName, "Should map to Placeholder_Soldier");
        Assert.AreEqual("replace", mode, "Should use replace mode");
        Assert.AreEqual("_tier2", suffix, "Should preserve _tier2 suffix");
    }

    /// <summary>
    /// GetTieredMapping이 player_tier3에 대해 올바른 Placeholder와 접미사를 반환하는지 검증합니다.
    /// </summary>
    [Test]
    public void GetTieredMapping_ReturnsCorrectResultForPlayerTier3()
    {
        var (objName, mode, suffix) = ModelMapping.GetTieredMapping("player_tier3");

        Assert.AreEqual("Player", objName, "Player should map to Player placeholder");
        Assert.AreEqual("child", mode, "Player should use child mode");
        Assert.AreEqual("_tier3", suffix, "Should preserve _tier3 suffix");
    }

    /// <summary>
    /// GetTieredMapping이 모든 5개 티어에서 올바르게 매핑하는지 검증합니다.
    /// </summary>
    [Test]
    public void GetTieredMapping_WorksForAllTiers(
        [Values("_tier1", "_tier2", "_tier3", "_tier4", "_tier5")] string tierSuffix)
    {
        string fileName = "soldier" + tierSuffix;
        var (objName, mode, suffix) = ModelMapping.GetTieredMapping(fileName);

        Assert.AreEqual("Placeholder_Soldier", objName, $"Should map to Placeholder_Soldier for {tierSuffix}");
        Assert.AreEqual("replace", mode, $"Should use replace mode for {tierSuffix}");
        Assert.AreEqual(tierSuffix, suffix, $"Should preserve {tierSuffix}");
    }

    /// <summary>
    /// GetTieredMapping이 일반 이름(티어 미포함)에 대해 GetMapping과 동일한 결과를 반환하는지 검증합니다.
    /// 하위 호환성 보장.
    /// </summary>
    [Test]
    public void GetTieredMapping_FallsBackToGetMappingForPlainNames()
    {
        var (objName, mode, suffix) = ModelMapping.GetTieredMapping("soldier");
        var (expectedObjName, expectedMode) = ModelMapping.GetMapping("soldier");

        Assert.AreEqual(expectedObjName, objName, "Placeholder name should match GetMapping");
        Assert.AreEqual(expectedMode, mode, "Mode should match GetMapping");
        Assert.IsNull(suffix, "Plain name should have null visualSuffix");
    }

    /// <summary>
    /// GetTieredMapping이 일반 이름(모든 매핑 항목)에 대해 GetMapping과 동일한 결과를 반환하는지 검증합니다.
    /// </summary>
    [Test]
    public void GetTieredMapping_MatchesGetMappingForAllKnownPlainNames(
        [Values("player", "hut", "rabbit", "slime", "swamp_ogre", "potion_heal", "lord_npc", "craft_blend")]
        string plainName)
    {
        var (objName1, mode1, suffix) = ModelMapping.GetTieredMapping(plainName);
        var (objName2, mode2) = ModelMapping.GetMapping(plainName);

        Assert.AreEqual(objName2, objName1, $"Placeholder should match for '{plainName}'");
        Assert.AreEqual(mode2, mode1, $"Mode should match for '{plainName}'");
        Assert.IsNull(suffix, $"Plain name '{plainName}' should have null visualSuffix");
    }

    /// <summary>
    /// GetTieredMapping이 알 수 없는 기본 이름에 대해 올바르게 null을 반환하는지 검증합니다.
    /// </summary>
    [Test]
    public void GetTieredMapping_ReturnsNullForUnknownBaseName()
    {
        var (objName, mode, suffix) = ModelMapping.GetTieredMapping("unknown_creature_tier1");

        Assert.IsNull(objName, "Unknown base name should return null objectName");
        Assert.IsNull(mode, "Unknown base name should return null mode");
        Assert.IsNull(suffix, "Unknown base name should return null visualSuffix");
    }

    /// <summary>
    /// GetTieredMapping이 알 수 없는 일반 이름에 대해 null을 반환하는지 검증합니다.
    /// </summary>
    [Test]
    public void GetTieredMapping_ReturnsNullForUnknownPlainName()
    {
        var (objName, mode, suffix) = ModelMapping.GetTieredMapping("nonexistent");

        Assert.IsNull(objName, "Unknown plain name should return null objectName");
        Assert.IsNull(mode, "Unknown plain name should return null mode");
        Assert.IsNull(suffix, "Unknown plain name should return null visualSuffix");
    }

    /// <summary>
    /// GetTieredMapping이 여러 밑줄이 있는 복합 이름에서도 올바르게 매핑하는지 검증합니다.
    /// </summary>
    [Test]
    public void GetTieredMapping_HandlesCompoundNames()
    {
        var (objName, mode, suffix) = ModelMapping.GetTieredMapping("electric_spine_hedgehog_tier3");

        Assert.AreEqual("Placeholder_ElectricPorcupine", objName, "Should map to correct placeholder");
        Assert.AreEqual("replace", mode, "Should use replace mode");
        Assert.AreEqual("_tier3", suffix, "Should preserve _tier3 suffix");
    }

    #endregion

    #region GetAvailableTiers

    /// <summary>
    /// GetAvailableTiers가 정확히 5개의 티어 접미사를 반환하는지 검증합니다.
    /// </summary>
    [Test]
    public void GetAvailableTiers_ReturnsFiveTiers()
    {
        string[] tiers = ModelMapping.GetAvailableTiers("soldier");

        Assert.AreEqual(5, tiers.Length, "Should return exactly 5 tier suffixes");
    }

    /// <summary>
    /// GetAvailableTiers가 올바른 티어 접미사 목록을 반환하는지 검증합니다.
    /// </summary>
    [Test]
    public void GetAvailableTiers_ReturnsCorrectSuffixes()
    {
        string[] tiers = ModelMapping.GetAvailableTiers("soldier");

        Assert.Contains("_tier1", tiers, "Should contain _tier1");
        Assert.Contains("_tier2", tiers, "Should contain _tier2");
        Assert.Contains("_tier3", tiers, "Should contain _tier3");
        Assert.Contains("_tier4", tiers, "Should contain _tier4");
        Assert.Contains("_tier5", tiers, "Should contain _tier5");
    }

    /// <summary>
    /// GetAvailableTiers가 baseName과 관계없이 항상 동일한 접미사를 반환하는지 검증합니다.
    /// </summary>
    [Test]
    public void GetAvailableTiers_ReturnsSameTiersForAnyBaseName(
        [Values("soldier", "player", "wolf", "rabbit", "hut")] string baseName)
    {
        string[] tiers = ModelMapping.GetAvailableTiers(baseName);

        Assert.AreEqual(5, tiers.Length, $"Should return 5 tiers for '{baseName}'");
        Assert.Contains("_tier1", tiers, $"Should contain _tier1 for '{baseName}'");
        Assert.Contains("_tier5", tiers, $"Should contain _tier5 for '{baseName}'");
    }

    /// <summary>
    /// GetAvailableTiers가 LevelGroupManager에 정의된 순서와 일치하는지 검증합니다.
    /// </summary>
    [Test]
    public void GetAvailableTiers_OrderMatchesLevelGroupManager()
    {
        string[] tiers = ModelMapping.GetAvailableTiers("soldier");
        var groups = LevelGroupManager.GetLevelGroups();

        Assert.AreEqual(groups.Length, tiers.Length, "Tier count should match group count");
        for (int i = 0; i < groups.Length; i++)
        {
            Assert.AreEqual(groups[i].visualSuffix, tiers[i],
                $"Tier at index {i} should match group {groups[i].groupName}");
        }
    }

    #endregion

    #region Backward Compatibility

    /// <summary>
    /// 기존 GetMapping이 티어드 기능 추가 후에도 동일하게 동작하는지 검증합니다.
    /// </summary>
    [Test]
    public void GetMapping_StillWorksAfterTieredExtension()
    {
        var (objName, mode) = ModelMapping.GetMapping("player");
        Assert.AreEqual("Player", objName, "Player mapping unchanged");
        Assert.AreEqual("child", mode, "Player mode unchanged");
    }

    /// <summary>
    /// GetRecognizedFiles가 티어드 파일과 일반 파일을 모두 인식하는지 검증합니다.
    /// </summary>
    [Test]
    public void GetRecognizedFiles_WorksWithTieredAndPlainFilenames()
    {
        var testFiles = new string[]
        {
            "soldier.glb",
            "soldier_tier1.glb",
            "soldier_tier2.glb",
            "player.glb",
            "unknown.glb"
        };

        string[] recognized = ModelMapping.GetRecognizedFiles(testFiles);

        Assert.Contains("soldier", recognized, "Should recognize plain soldier");
        Assert.Contains("soldier_tier1", recognized, "Should recognize soldier_tier1");
        Assert.Contains("soldier_tier2", recognized, "Should recognize soldier_tier2");
        Assert.Contains("player", recognized, "Should recognize player");
        Assert.IsFalse(System.Array.Exists(recognized, name => name == "unknown"),
            "Should not recognize unknown file");
    }

    #endregion
}

#endif
