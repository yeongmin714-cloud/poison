using NUnit.Framework;
using ProjectName.Core.Data;
using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// Phase 3.1 — RingDifficultyData 방사형 난이도 시스템 테스트.
    /// ROADMAP.md Phase 3.1 표의 모든 값을 검증합니다.
    /// </summary>
    public class Phase31_RingDifficultyDataTests
    {
        // ========================================================
        // 1. GetGuardLevelRange — Ring별 (ROADMAP 링별 상세 난이도 표)
        // ========================================================

        [Test]
        public void GetGuardLevelRange_Ring1_Returns1to10()
        {
            var result = RingDifficultyData.GetGuardLevelRange(TerritoryDifficulty.Ring1);
            Assert.AreEqual(new Vector2Int(1, 10), result);
        }

        [Test]
        public void GetGuardLevelRange_Ring2_Returns11to20()
        {
            var result = RingDifficultyData.GetGuardLevelRange(TerritoryDifficulty.Ring2);
            Assert.AreEqual(new Vector2Int(11, 20), result);
        }

        [Test]
        public void GetGuardLevelRange_Ring3_Returns21to30()
        {
            var result = RingDifficultyData.GetGuardLevelRange(TerritoryDifficulty.Ring3);
            Assert.AreEqual(new Vector2Int(21, 30), result);
        }

        [Test]
        public void GetGuardLevelRange_Ring4_Returns31to40()
        {
            var result = RingDifficultyData.GetGuardLevelRange(TerritoryDifficulty.Ring4);
            Assert.AreEqual(new Vector2Int(31, 40), result);
        }

        [Test]
        public void GetGuardLevelRange_Empire_Returns41to50()
        {
            var result = RingDifficultyData.GetGuardLevelRange(TerritoryDifficulty.Empire);
            Assert.AreEqual(new Vector2Int(41, 50), result);
        }

        // ========================================================
        // 2. GetGuardLevelRange — 국가별 × Ring별 (ROADMAP 지역별 링 난이도 가중치 표)
        // ========================================================

        // 동 (East) — 초급, 시작 지역
        [Test]
        public void GetGuardLevelRange_East_Ring1_Returns1to3()
        {
            var result = RingDifficultyData.GetGuardLevelRange(NationType.East, TerritoryDifficulty.Ring1);
            Assert.AreEqual(new Vector2Int(1, 3), result);
        }

        [Test]
        public void GetGuardLevelRange_East_Ring2_Returns4to8()
        {
            var result = RingDifficultyData.GetGuardLevelRange(NationType.East, TerritoryDifficulty.Ring2);
            Assert.AreEqual(new Vector2Int(4, 8), result);
        }

        [Test]
        public void GetGuardLevelRange_East_Ring3_Returns9to14()
        {
            var result = RingDifficultyData.GetGuardLevelRange(NationType.East, TerritoryDifficulty.Ring3);
            Assert.AreEqual(new Vector2Int(9, 14), result);
        }

        [Test]
        public void GetGuardLevelRange_East_Ring4_Returns15to20()
        {
            var result = RingDifficultyData.GetGuardLevelRange(NationType.East, TerritoryDifficulty.Ring4);
            Assert.AreEqual(new Vector2Int(15, 20), result);
        }

        // 서 (West) — 중상급
        [Test]
        public void GetGuardLevelRange_West_Ring1_Returns3to6()
        {
            var result = RingDifficultyData.GetGuardLevelRange(NationType.West, TerritoryDifficulty.Ring1);
            Assert.AreEqual(new Vector2Int(3, 6), result);
        }

        [Test]
        public void GetGuardLevelRange_West_Ring2_Returns7to12()
        {
            var result = RingDifficultyData.GetGuardLevelRange(NationType.West, TerritoryDifficulty.Ring2);
            Assert.AreEqual(new Vector2Int(7, 12), result);
        }

        [Test]
        public void GetGuardLevelRange_West_Ring3_Returns13to18()
        {
            var result = RingDifficultyData.GetGuardLevelRange(NationType.West, TerritoryDifficulty.Ring3);
            Assert.AreEqual(new Vector2Int(13, 18), result);
        }

        [Test]
        public void GetGuardLevelRange_West_Ring4_Returns19to25()
        {
            var result = RingDifficultyData.GetGuardLevelRange(NationType.West, TerritoryDifficulty.Ring4);
            Assert.AreEqual(new Vector2Int(19, 25), result);
        }

        // 남 (South) — 고급
        [Test]
        public void GetGuardLevelRange_South_Ring1_Returns5to9()
        {
            var result = RingDifficultyData.GetGuardLevelRange(NationType.South, TerritoryDifficulty.Ring1);
            Assert.AreEqual(new Vector2Int(5, 9), result);
        }

        [Test]
        public void GetGuardLevelRange_South_Ring2_Returns10to15()
        {
            var result = RingDifficultyData.GetGuardLevelRange(NationType.South, TerritoryDifficulty.Ring2);
            Assert.AreEqual(new Vector2Int(10, 15), result);
        }

        [Test]
        public void GetGuardLevelRange_South_Ring3_Returns16to22()
        {
            var result = RingDifficultyData.GetGuardLevelRange(NationType.South, TerritoryDifficulty.Ring3);
            Assert.AreEqual(new Vector2Int(16, 22), result);
        }

        [Test]
        public void GetGuardLevelRange_South_Ring4_Returns23to30()
        {
            var result = RingDifficultyData.GetGuardLevelRange(NationType.South, TerritoryDifficulty.Ring4);
            Assert.AreEqual(new Vector2Int(23, 30), result);
        }

        // 북 (North) — 최고 난이도
        [Test]
        public void GetGuardLevelRange_North_Ring1_Returns8to12()
        {
            var result = RingDifficultyData.GetGuardLevelRange(NationType.North, TerritoryDifficulty.Ring1);
            Assert.AreEqual(new Vector2Int(8, 12), result);
        }

        [Test]
        public void GetGuardLevelRange_North_Ring2_Returns13to20()
        {
            var result = RingDifficultyData.GetGuardLevelRange(NationType.North, TerritoryDifficulty.Ring2);
            Assert.AreEqual(new Vector2Int(13, 20), result);
        }

        [Test]
        public void GetGuardLevelRange_North_Ring3_Returns21to28()
        {
            var result = RingDifficultyData.GetGuardLevelRange(NationType.North, TerritoryDifficulty.Ring3);
            Assert.AreEqual(new Vector2Int(21, 28), result);
        }

        [Test]
        public void GetGuardLevelRange_North_Ring4_Returns29to40()
        {
            var result = RingDifficultyData.GetGuardLevelRange(NationType.North, TerritoryDifficulty.Ring4);
            Assert.AreEqual(new Vector2Int(29, 40), result);
        }

        // 황제국
        [Test]
        public void GetGuardLevelRange_AnyNation_Empire_Returns41to50()
        {
            Assert.AreEqual(new Vector2Int(41, 50), RingDifficultyData.GetGuardLevelRange(NationType.East, TerritoryDifficulty.Empire));
            Assert.AreEqual(new Vector2Int(41, 50), RingDifficultyData.GetGuardLevelRange(NationType.West, TerritoryDifficulty.Empire));
            Assert.AreEqual(new Vector2Int(41, 50), RingDifficultyData.GetGuardLevelRange(NationType.South, TerritoryDifficulty.Empire));
            Assert.AreEqual(new Vector2Int(41, 50), RingDifficultyData.GetGuardLevelRange(NationType.North, TerritoryDifficulty.Empire));
        }

        // ========================================================
        // 3. GetGuardCountRange (ROADMAP 링별 상세 난이도 표)
        // ========================================================

        [Test]
        public void GetGuardCountRange_Ring1_Returns3to5()
        {
            Assert.AreEqual(new Vector2Int(3, 5), RingDifficultyData.GetGuardCountRange(TerritoryDifficulty.Ring1));
        }

        [Test]
        public void GetGuardCountRange_Ring2_Returns6to10()
        {
            Assert.AreEqual(new Vector2Int(6, 10), RingDifficultyData.GetGuardCountRange(TerritoryDifficulty.Ring2));
        }

        [Test]
        public void GetGuardCountRange_Ring3_Returns11to20()
        {
            Assert.AreEqual(new Vector2Int(11, 20), RingDifficultyData.GetGuardCountRange(TerritoryDifficulty.Ring3));
        }

        [Test]
        public void GetGuardCountRange_Ring4_Returns21to40()
        {
            Assert.AreEqual(new Vector2Int(21, 40), RingDifficultyData.GetGuardCountRange(TerritoryDifficulty.Ring4));
        }

        [Test]
        public void GetGuardCountRange_Empire_Returns50()
        {
            Assert.AreEqual(new Vector2Int(50, 50), RingDifficultyData.GetGuardCountRange(TerritoryDifficulty.Empire));
        }

        // ========================================================
        // 4. GetLordTasteTier (ROADMAP 링별 상세 난이도 표)
        // ========================================================

        [Test]
        public void GetLordTasteTier_Ring1_IsBasic()
        {
            Assert.AreEqual(RingDifficultyData.LordTasteTier.Basic, RingDifficultyData.GetLordTasteTier(TerritoryDifficulty.Ring1));
        }

        [Test]
        public void GetLordTasteTier_Ring2_IsStandard()
        {
            Assert.AreEqual(RingDifficultyData.LordTasteTier.Standard, RingDifficultyData.GetLordTasteTier(TerritoryDifficulty.Ring2));
        }

        [Test]
        public void GetLordTasteTier_Ring3_IsGourmet()
        {
            Assert.AreEqual(RingDifficultyData.LordTasteTier.Gourmet, RingDifficultyData.GetLordTasteTier(TerritoryDifficulty.Ring3));
        }

        [Test]
        public void GetLordTasteTier_Ring4_IsRoyal()
        {
            Assert.AreEqual(RingDifficultyData.LordTasteTier.Royal, RingDifficultyData.GetLordTasteTier(TerritoryDifficulty.Ring4));
        }

        [Test]
        public void GetLordTasteTier_Empire_IsRoyal()
        {
            Assert.AreEqual(RingDifficultyData.LordTasteTier.Royal, RingDifficultyData.GetLordTasteTier(TerritoryDifficulty.Empire));
        }

        // ========================================================
        // 5. GetLordDiseaseCountRange (ROADMAP 링별 상세 난이도 표)
        // ========================================================

        [Test]
        public void GetLordDiseaseCountRange_Ring1_Returns0to0()
        {
            Assert.AreEqual(new Vector2Int(0, 0), RingDifficultyData.GetLordDiseaseCountRange(TerritoryDifficulty.Ring1));
        }

        [Test]
        public void GetLordDiseaseCountRange_Ring2_Returns0to1()
        {
            Assert.AreEqual(new Vector2Int(0, 1), RingDifficultyData.GetLordDiseaseCountRange(TerritoryDifficulty.Ring2));
        }

        [Test]
        public void GetLordDiseaseCountRange_Ring3_Returns1to2()
        {
            Assert.AreEqual(new Vector2Int(1, 2), RingDifficultyData.GetLordDiseaseCountRange(TerritoryDifficulty.Ring3));
        }

        [Test]
        public void GetLordDiseaseCountRange_Ring4_Returns2to3()
        {
            Assert.AreEqual(new Vector2Int(2, 3), RingDifficultyData.GetLordDiseaseCountRange(TerritoryDifficulty.Ring4));
        }

        [Test]
        public void GetLordDiseaseCountRange_Empire_Returns3to4()
        {
            Assert.AreEqual(new Vector2Int(3, 4), RingDifficultyData.GetLordDiseaseCountRange(TerritoryDifficulty.Empire));
        }

        // ========================================================
        // 6. GetDefenseRating (ROADMAP 링별 상세 난이도 표)
        // ========================================================

        [Test]
        public void GetDefenseRating_Ring1_IsLow()
        {
            Assert.AreEqual(RingDifficultyData.DefenseRating.Low, RingDifficultyData.GetDefenseRating(TerritoryDifficulty.Ring1));
        }

        [Test]
        public void GetDefenseRating_Ring2_IsMedium()
        {
            Assert.AreEqual(RingDifficultyData.DefenseRating.Medium, RingDifficultyData.GetDefenseRating(TerritoryDifficulty.Ring2));
        }

        [Test]
        public void GetDefenseRating_Ring3_IsHigh()
        {
            Assert.AreEqual(RingDifficultyData.DefenseRating.High, RingDifficultyData.GetDefenseRating(TerritoryDifficulty.Ring3));
        }

        [Test]
        public void GetDefenseRating_Ring4_IsVeryHigh()
        {
            Assert.AreEqual(RingDifficultyData.DefenseRating.VeryHigh, RingDifficultyData.GetDefenseRating(TerritoryDifficulty.Ring4));
        }

        [Test]
        public void GetDefenseRating_Empire_IsVeryHigh()
        {
            Assert.AreEqual(RingDifficultyData.DefenseRating.VeryHigh, RingDifficultyData.GetDefenseRating(TerritoryDifficulty.Empire));
        }

        // ========================================================
        // 7. GetDefenseMultiplier
        // ========================================================

        [Test]
        public void GetDefenseMultiplier_Ring1_Is0_8()
        {
            Assert.AreEqual(0.8f, RingDifficultyData.GetDefenseMultiplier(TerritoryDifficulty.Ring1), 0.001f);
        }

        [Test]
        public void GetDefenseMultiplier_Ring2_Is1_0()
        {
            Assert.AreEqual(1.0f, RingDifficultyData.GetDefenseMultiplier(TerritoryDifficulty.Ring2), 0.001f);
        }

        [Test]
        public void GetDefenseMultiplier_Ring3_Is1_3()
        {
            Assert.AreEqual(1.3f, RingDifficultyData.GetDefenseMultiplier(TerritoryDifficulty.Ring3), 0.001f);
        }

        [Test]
        public void GetDefenseMultiplier_Ring4_Is1_6()
        {
            Assert.AreEqual(1.6f, RingDifficultyData.GetDefenseMultiplier(TerritoryDifficulty.Ring4), 0.001f);
        }

        [Test]
        public void GetDefenseMultiplier_Empire_Is1_6()
        {
            Assert.AreEqual(1.6f, RingDifficultyData.GetDefenseMultiplier(TerritoryDifficulty.Empire), 0.001f);
        }

        // ========================================================
        // 8. GetRewardTier (ROADMAP 링별 상세 난이도 표)
        // ========================================================

        [Test]
        public void GetRewardTier_Ring1_IsSmall()
        {
            Assert.AreEqual(RingDifficultyData.RewardTier.Small, RingDifficultyData.GetRewardTier(TerritoryDifficulty.Ring1));
        }

        [Test]
        public void GetRewardTier_Ring2_IsMedium()
        {
            Assert.AreEqual(RingDifficultyData.RewardTier.Medium, RingDifficultyData.GetRewardTier(TerritoryDifficulty.Ring2));
        }

        [Test]
        public void GetRewardTier_Ring3_IsLarge()
        {
            Assert.AreEqual(RingDifficultyData.RewardTier.Large, RingDifficultyData.GetRewardTier(TerritoryDifficulty.Ring3));
        }

        [Test]
        public void GetRewardTier_Ring4_IsVeryLarge()
        {
            Assert.AreEqual(RingDifficultyData.RewardTier.VeryLarge, RingDifficultyData.GetRewardTier(TerritoryDifficulty.Ring4));
        }

        [Test]
        public void GetRewardTier_Empire_IsVeryLarge()
        {
            Assert.AreEqual(RingDifficultyData.RewardTier.VeryLarge, RingDifficultyData.GetRewardTier(TerritoryDifficulty.Empire));
        }

        // ========================================================
        // 9. GetRewardMultiplier
        // ========================================================

        [Test]
        public void GetRewardMultiplier_Ring1_Is1x()
        {
            Assert.AreEqual(1.0f, RingDifficultyData.GetRewardMultiplier(TerritoryDifficulty.Ring1), 0.001f);
        }

        [Test]
        public void GetRewardMultiplier_Ring2_Is2x()
        {
            Assert.AreEqual(2.0f, RingDifficultyData.GetRewardMultiplier(TerritoryDifficulty.Ring2), 0.001f);
        }

        [Test]
        public void GetRewardMultiplier_Ring3_Is4x()
        {
            Assert.AreEqual(4.0f, RingDifficultyData.GetRewardMultiplier(TerritoryDifficulty.Ring3), 0.001f);
        }

        [Test]
        public void GetRewardMultiplier_Ring4_Is8x()
        {
            Assert.AreEqual(8.0f, RingDifficultyData.GetRewardMultiplier(TerritoryDifficulty.Ring4), 0.001f);
        }

        [Test]
        public void GetRewardMultiplier_Empire_Is8x()
        {
            Assert.AreEqual(8.0f, RingDifficultyData.GetRewardMultiplier(TerritoryDifficulty.Empire), 0.001f);
        }

        // ========================================================
        // 10. GetDifficultyStars (ROADMAP 링별 상세 난이도 표)
        // ========================================================

        [Test]
        public void GetDifficultyStars_Ring1_Returns1to2Stars()
        {
            Assert.AreEqual("⭐~⭐⭐", RingDifficultyData.GetDifficultyStars(TerritoryDifficulty.Ring1));
        }

        [Test]
        public void GetDifficultyStars_Ring2_Returns2to3Stars()
        {
            Assert.AreEqual("⭐⭐~⭐⭐⭐", RingDifficultyData.GetDifficultyStars(TerritoryDifficulty.Ring2));
        }

        [Test]
        public void GetDifficultyStars_Ring3_Returns3to4Stars()
        {
            Assert.AreEqual("⭐⭐⭐~⭐⭐⭐⭐", RingDifficultyData.GetDifficultyStars(TerritoryDifficulty.Ring3));
        }

        [Test]
        public void GetDifficultyStars_Ring4_Returns4to5Stars()
        {
            Assert.AreEqual("⭐⭐⭐⭐~⭐⭐⭐⭐⭐", RingDifficultyData.GetDifficultyStars(TerritoryDifficulty.Ring4));
        }

        [Test]
        public void GetDifficultyStars_Empire_Returns5Stars()
        {
            Assert.AreEqual("⭐⭐⭐⭐⭐", RingDifficultyData.GetDifficultyStars(TerritoryDifficulty.Empire));
        }

        // ========================================================
        // 11. GetDifficultyTier
        // ========================================================

        [Test]
        public void GetDifficultyTier_Ring1_Is1()
        {
            Assert.AreEqual(1, RingDifficultyData.GetDifficultyTier(TerritoryDifficulty.Ring1));
        }

        [Test]
        public void GetDifficultyTier_Ring2_Is2()
        {
            Assert.AreEqual(2, RingDifficultyData.GetDifficultyTier(TerritoryDifficulty.Ring2));
        }

        [Test]
        public void GetDifficultyTier_Ring3_Is3()
        {
            Assert.AreEqual(3, RingDifficultyData.GetDifficultyTier(TerritoryDifficulty.Ring3));
        }

        [Test]
        public void GetDifficultyTier_Ring4_Is4()
        {
            Assert.AreEqual(4, RingDifficultyData.GetDifficultyTier(TerritoryDifficulty.Ring4));
        }

        [Test]
        public void GetDifficultyTier_Empire_Is5()
        {
            Assert.AreEqual(5, RingDifficultyData.GetDifficultyTier(TerritoryDifficulty.Empire));
        }

        // ========================================================
        // 12. GetMonsterTiersForDifficulty (ROADMAP 3.6 몬스터 배치 표)
        // ========================================================

        [Test]
        public void GetMonsterTiersForDifficulty_Ring1_HasBeginnerOnly()
        {
            var result = RingDifficultyData.GetMonsterTiersForDifficulty(TerritoryDifficulty.Ring1);
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual(MonsterTier.Beginner, result[0]);
        }

        [Test]
        public void GetMonsterTiersForDifficulty_Ring2_HasBeginnerAndIntermediate()
        {
            var result = RingDifficultyData.GetMonsterTiersForDifficulty(TerritoryDifficulty.Ring2);
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual(MonsterTier.Beginner, result[0]);
            Assert.AreEqual(MonsterTier.Intermediate, result[1]);
        }

        [Test]
        public void GetMonsterTiersForDifficulty_Ring3_HasIntermediateOnly()
        {
            var result = RingDifficultyData.GetMonsterTiersForDifficulty(TerritoryDifficulty.Ring3);
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual(MonsterTier.Intermediate, result[0]);
        }

        [Test]
        public void GetMonsterTiersForDifficulty_Ring4_HasIntermediateAndAdvanced()
        {
            var result = RingDifficultyData.GetMonsterTiersForDifficulty(TerritoryDifficulty.Ring4);
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual(MonsterTier.Intermediate, result[0]);
            Assert.AreEqual(MonsterTier.Advanced, result[1]);
        }

        [Test]
        public void GetMonsterTiersForDifficulty_Empire_HasAdvancedOnly()
        {
            var result = RingDifficultyData.GetMonsterTiersForDifficulty(TerritoryDifficulty.Empire);
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual(MonsterTier.Advanced, result[0]);
        }

        // ========================================================
        // 13. GetMonsterCountRange (ROADMAP 3.6 몬스터 배치 표)
        // ========================================================

        [Test]
        public void GetMonsterCountRange_Ring1_Returns3to4()
        {
            Assert.AreEqual(new Vector2Int(3, 4), RingDifficultyData.GetMonsterCountRange(TerritoryDifficulty.Ring1));
        }

        [Test]
        public void GetMonsterCountRange_Ring2_Returns4to5()
        {
            Assert.AreEqual(new Vector2Int(4, 5), RingDifficultyData.GetMonsterCountRange(TerritoryDifficulty.Ring2));
        }

        [Test]
        public void GetMonsterCountRange_Ring3_Returns3to5()
        {
            Assert.AreEqual(new Vector2Int(3, 5), RingDifficultyData.GetMonsterCountRange(TerritoryDifficulty.Ring3));
        }

        [Test]
        public void GetMonsterCountRange_Ring4_Returns4to6()
        {
            Assert.AreEqual(new Vector2Int(4, 6), RingDifficultyData.GetMonsterCountRange(TerritoryDifficulty.Ring4));
        }

        [Test]
        public void GetMonsterCountRange_Empire_Returns8to12()
        {
            Assert.AreEqual(new Vector2Int(8, 12), RingDifficultyData.GetMonsterCountRange(TerritoryDifficulty.Empire));
        }

        // ========================================================
        // 14. GetTerritoryIndicesForRing
        // ========================================================

        [Test]
        public void GetTerritoryIndicesForRing_Ring1_Returns1to5()
        {
            Assert.AreEqual(new Vector2Int(1, 5), RingDifficultyData.GetTerritoryIndicesForRing(TerritoryDifficulty.Ring1));
        }

        [Test]
        public void GetTerritoryIndicesForRing_Ring2_Returns6to10()
        {
            Assert.AreEqual(new Vector2Int(6, 10), RingDifficultyData.GetTerritoryIndicesForRing(TerritoryDifficulty.Ring2));
        }

        [Test]
        public void GetTerritoryIndicesForRing_Ring3_Returns11to15()
        {
            Assert.AreEqual(new Vector2Int(11, 15), RingDifficultyData.GetTerritoryIndicesForRing(TerritoryDifficulty.Ring3));
        }

        [Test]
        public void GetTerritoryIndicesForRing_Ring4_Returns16to20()
        {
            Assert.AreEqual(new Vector2Int(16, 20), RingDifficultyData.GetTerritoryIndicesForRing(TerritoryDifficulty.Ring4));
        }

        // ========================================================
        // 15. GetNationDisplayName
        // ========================================================

        [Test]
        public void GetNationDisplayName_East_ReturnsKorean()
        {
            Assert.AreEqual("동 (East)", RingDifficultyData.GetNationDisplayName(NationType.East));
        }

        [Test]
        public void GetNationDisplayName_West_ReturnsKorean()
        {
            Assert.AreEqual("서 (West)", RingDifficultyData.GetNationDisplayName(NationType.West));
        }

        [Test]
        public void GetNationDisplayName_South_ReturnsKorean()
        {
            Assert.AreEqual("남 (South)", RingDifficultyData.GetNationDisplayName(NationType.South));
        }

        [Test]
        public void GetNationDisplayName_North_ReturnsKorean()
        {
            Assert.AreEqual("북 (North)", RingDifficultyData.GetNationDisplayName(NationType.North));
        }

        [Test]
        public void GetNationDisplayName_Empire_ReturnsKorean()
        {
            Assert.AreEqual("황제국", RingDifficultyData.GetNationDisplayName(NationType.Empire));
        }

        [Test]
        public void GetNationDisplayName_Dracula_ReturnsKorean()
        {
            Assert.AreEqual("드라큘라", RingDifficultyData.GetNationDisplayName(NationType.Dracula));
        }

        // ========================================================
        // 16. GenerateGuardLevel (시드 기반 결정론적)
        // ========================================================

        [Test]
        public void GenerateGuardLevel_IsDeterministic_SameSeedSameResult()
        {
            int a = RingDifficultyData.GenerateGuardLevel(NationType.East, TerritoryDifficulty.Ring2, 42);
            int b = RingDifficultyData.GenerateGuardLevel(NationType.East, TerritoryDifficulty.Ring2, 42);
            Assert.AreEqual(a, b, "같은 시드는 같은 결과를 반환해야 함");
        }

        [Test]
        public void GenerateGuardLevel_DifferentSeedDifferentResult()
        {
            int a = RingDifficultyData.GenerateGuardLevel(NationType.East, TerritoryDifficulty.Ring1, 1);
            int b = RingDifficultyData.GenerateGuardLevel(NationType.East, TerritoryDifficulty.Ring1, 2);
            Assert.AreNotEqual(a, b, "다른 시드는 다른 결과를 반환해야 함");
        }

        [Test]
        public void GenerateGuardLevel_ResultInRange()
        {
            int level = RingDifficultyData.GenerateGuardLevel(NationType.North, TerritoryDifficulty.Ring4, 99);
            Assert.GreaterOrEqual(level, 29, "북 Ring4 최소 레벨 29");
            Assert.LessOrEqual(level, 40, "북 Ring4 최대 레벨 40");
        }

        [Test]
        public void GenerateGuardLevel_Seed0_GivesSpecificValue()
        {
            int level = RingDifficultyData.GenerateGuardLevel(NationType.East, TerritoryDifficulty.Ring1, 0);
            Assert.GreaterOrEqual(level, 1);
            Assert.LessOrEqual(level, 3);
        }

        // ========================================================
        // 17. GetDifficultyDescription
        // ========================================================

        [Test]
        public void GetDifficultyDescription_Ring1_ContainsOuterRing()
        {
            string desc = RingDifficultyData.GetDifficultyDescription(TerritoryDifficulty.Ring1);
            Assert.IsTrue(desc.Contains("최외곽"));
        }

        [Test]
        public void GetDifficultyDescription_Ring2_ContainsMiddleOuter()
        {
            string desc = RingDifficultyData.GetDifficultyDescription(TerritoryDifficulty.Ring2);
            Assert.IsTrue(desc.Contains("중간 바깥"));
        }

        [Test]
        public void GetDifficultyDescription_Ring3_ContainsMiddleInner()
        {
            string desc = RingDifficultyData.GetDifficultyDescription(TerritoryDifficulty.Ring3);
            Assert.IsTrue(desc.Contains("중간 안쪽"));
        }

        [Test]
        public void GetDifficultyDescription_Ring4_ContainsEmpireAdjacent()
        {
            string desc = RingDifficultyData.GetDifficultyDescription(TerritoryDifficulty.Ring4);
            Assert.IsTrue(desc.Contains("황제국 인접"));
        }

        [Test]
        public void GetDifficultyDescription_Empire_ContainsFinal()
        {
            string desc = RingDifficultyData.GetDifficultyDescription(TerritoryDifficulty.Empire);
            Assert.IsTrue(desc.Contains("최종"));
        }

        // ========================================================
        // 18. GetGuardLevelRange_DefaultCase
        // ========================================================

        [Test]
        public void GetGuardLevelRange_DefaultCase_Returns1to5()
        {
            // Force invalid enum value
            var result = RingDifficultyData.GetGuardLevelRange((TerritoryDifficulty)99);
            Assert.AreEqual(new Vector2Int(1, 5), result);
        }
    }
}