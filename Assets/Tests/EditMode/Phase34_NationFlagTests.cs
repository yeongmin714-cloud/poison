using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// Phase 34: 국가별 국기 시스템 EditMode 테스트.
    /// NationFlagData, NationFlagDatabase, NationFlagVisualData, FlagManager 검증.
    /// </summary>
    public class Phase34_NationFlagTests
    {
        // ================================================================
        // Section 1: NationFlagDefinition 기본 데이터
        // ================================================================

        [Test]
        public void NationFlagDatabase_GetAllFlags_Returns6()
        {
            var flags = NationFlagDatabase.GetAllFlags();
            Assert.AreEqual(6, flags.Count, "6개 국가의 국기 정의가 있어야 함");
        }

        [TestCase(NationType.East)]
        [TestCase(NationType.West)]
        [TestCase(NationType.South)]
        [TestCase(NationType.North)]
        [TestCase(NationType.Empire)]
        [TestCase(NationType.Dracula)]
        public void NationFlagDatabase_HasFlag_ReturnsTrue_ForAllNations(NationType nation)
        {
            Assert.IsTrue(NationFlagDatabase.HasFlag(nation), $"{nation} 국가의 국기 정의가 있어야 함");
        }

        [Test]
        public void NationFlagDatabase_HasFlag_None_ReturnsFalse()
        {
            Assert.IsFalse(NationFlagDatabase.HasFlag(NationType.None));
        }

        // ================================================================
        // Section 2: 각 국가별 국기 데이터 정확성
        // ================================================================

        [Test]
        public void NationFlagDatabase_GetFlag_East_ReturnsCorrectData()
        {
            var flag = NationFlagDatabase.GetFlag(NationType.East);
            Assert.AreEqual(NationType.East, flag.nation);
            Assert.AreEqual("파랑", flag.colorName);
            Assert.AreEqual("동쪽의 시작을 상징", flag.description);
            Assert.AreEqual(Color.blue, flag.flagColor);
            Assert.AreEqual("떠오르는 태양", flag.symbolName);
            Assert.AreEqual("🌅", flag.symbolEmoji);
        }

        [Test]
        public void NationFlagDatabase_GetFlag_West_ReturnsCorrectData()
        {
            var flag = NationFlagDatabase.GetFlag(NationType.West);
            Assert.AreEqual(NationType.West, flag.nation);
            Assert.AreEqual("초록", flag.colorName);
            Assert.AreEqual("서쪽의 대지와 자연", flag.description);
            Assert.AreEqual(Color.green, flag.flagColor);
            Assert.AreEqual("떡갈나무 잎", flag.symbolName);
            Assert.AreEqual("🌿", flag.symbolEmoji);
        }

        [Test]
        public void NationFlagDatabase_GetFlag_South_ReturnsCorrectData()
        {
            var flag = NationFlagDatabase.GetFlag(NationType.South);
            Assert.AreEqual(NationType.South, flag.nation);
            Assert.AreEqual("빨강", flag.colorName);
            Assert.AreEqual("남쪽의 열정과 전투", flag.description);
            Assert.AreEqual(Color.red, flag.flagColor);
            Assert.AreEqual("불꽃", flag.symbolName);
            Assert.AreEqual("🔥", flag.symbolEmoji);
        }

        [Test]
        public void NationFlagDatabase_GetFlag_North_ReturnsCorrectData()
        {
            var flag = NationFlagDatabase.GetFlag(NationType.North);
            Assert.AreEqual(NationType.North, flag.nation);
            Assert.AreEqual("보라", flag.colorName);
            Assert.AreEqual("북쪽의 냉철함", flag.description);
            Assert.AreEqual(new Color(0.6f, 0.2f, 1f), flag.flagColor);
            Assert.AreEqual("눈송이/산", flag.symbolName);
            Assert.AreEqual("❄️", flag.symbolEmoji);
        }

        [Test]
        public void NationFlagDatabase_GetFlag_Empire_ReturnsCorrectData()
        {
            var flag = NationFlagDatabase.GetFlag(NationType.Empire);
            Assert.AreEqual(NationType.Empire, flag.nation);
            Assert.AreEqual("황금", flag.colorName);
            Assert.AreEqual("중앙 황제의 권위", flag.description);
            Assert.AreEqual(new Color(1f, 0.85f, 0.2f), flag.flagColor);
            Assert.AreEqual("독수리/왕관", flag.symbolName);
            Assert.AreEqual("👑", flag.symbolEmoji);
        }

        [Test]
        public void NationFlagDatabase_GetFlag_Dracula_ReturnsCorrectData()
        {
            var flag = NationFlagDatabase.GetFlag(NationType.Dracula);
            Assert.AreEqual(NationType.Dracula, flag.nation);
            Assert.AreEqual("검정", flag.colorName);
            Assert.AreEqual("밤의 어둠과 피", flag.description);
            Assert.AreEqual(new Color(0.8f, 0f, 0f), flag.flagColor);
            Assert.AreEqual("박쥐", flag.symbolName);
            Assert.AreEqual("🦇", flag.symbolEmoji);
        }

        [Test]
        public void NationFlagDatabase_GetFlag_None_ReturnsDefault()
        {
            var flag = NationFlagDatabase.GetFlag(NationType.None);
            // NationType.None에 대한 정의가 없으므로 default struct 반환
            Assert.AreEqual(NationType.None, flag.nation);
            Assert.IsNull(flag.symbolName);
        }

        // ================================================================
        // Section 3: 모든 국가 데이터 일관성 검증
        // ================================================================

        [Test]
        public void NationFlagDatabase_AllFlags_HaveNonEmptyColorName()
        {
            var flags = NationFlagDatabase.GetAllFlags();
            foreach (var flag in flags)
            {
                Assert.IsFalse(string.IsNullOrEmpty(flag.colorName),
                    $"{flag.nation}의 colorName이 비어있음");
            }
        }

        [Test]
        public void NationFlagDatabase_AllFlags_HaveNonEmptyDescription()
        {
            var flags = NationFlagDatabase.GetAllFlags();
            foreach (var flag in flags)
            {
                Assert.IsFalse(string.IsNullOrEmpty(flag.description),
                    $"{flag.nation}의 description이 비어있음");
            }
        }

        [Test]
        public void NationFlagDatabase_AllFlags_HaveNonEmptySymbolEmoji()
        {
            var flags = NationFlagDatabase.GetAllFlags();
            foreach (var flag in flags)
            {
                Assert.IsFalse(string.IsNullOrEmpty(flag.symbolEmoji),
                    $"{flag.nation}의 symbolEmoji가 비어있음");
            }
        }

        [Test]
        public void NationFlagDatabase_AllFlags_HaveNonEmptySymbolName()
        {
            var flags = NationFlagDatabase.GetAllFlags();
            foreach (var flag in flags)
            {
                Assert.IsFalse(string.IsNullOrEmpty(flag.symbolName),
                    $"{flag.nation}의 symbolName이 비어있음");
            }
        }

        [Test]
        public void NationFlagDatabase_AllFlags_HaveValidAlpha()
        {
            var flags = NationFlagDatabase.GetAllFlags();
            foreach (var flag in flags)
            {
                Assert.IsTrue(flag.flagColor.a > 0f,
                    $"{flag.nation}의 flagColor 알파값이 0입니다");
            }
        }

        // ================================================================
        // Section 4: NationFlagVisualData 확장 메서드
        // ================================================================

        [TestCase(NationType.East, "🌅")]
        [TestCase(NationType.West, "🌿")]
        [TestCase(NationType.South, "🔥")]
        [TestCase(NationType.North, "❄️")]
        [TestCase(NationType.Empire, "👑")]
        public void NationFlagVisualData_GetSymbolEmoji_ReturnsCorrect(NationType nation, string expectedEmoji)
        {
            Assert.AreEqual(expectedEmoji, nation.GetSymbolEmoji());
        }

        [Test]
        public void NationFlagVisualData_GetSymbolEmoji_None_ReturnsQuestionMark()
        {
            Assert.AreEqual("❓", NationType.None.GetSymbolEmoji());
        }

        [TestCase(NationType.East, "떠오르는 태양")]
        [TestCase(NationType.West, "떡갈나무 잎")]
        [TestCase(NationType.South, "불꽃")]
        [TestCase(NationType.North, "눈송이/산")]
        [TestCase(NationType.Empire, "독수리/왕관")]
        public void NationFlagVisualData_GetSymbolName_ReturnsCorrect(NationType nation, string expectedName)
        {
            Assert.AreEqual(expectedName, nation.GetSymbolName());
        }

        [TestCase(NationType.East, "동쪽의 시작을 상징")]
        [TestCase(NationType.West, "서쪽의 대지와 자연")]
        [TestCase(NationType.South, "남쪽의 열정과 전투")]
        [TestCase(NationType.North, "북쪽의 냉철함")]
        [TestCase(NationType.Empire, "중앙 황제의 권위")]
        public void NationFlagVisualData_GetFlagDescription_ReturnsCorrect(NationType nation, string expectedDesc)
        {
            Assert.AreEqual(expectedDesc, nation.GetFlagDescription());
        }

        // ================================================================
        // Section 5: FlagManager 싱글톤
        // ================================================================

        [Test]
        public void FlagManager_Singleton_Works()
        {
            var go = new GameObject("TestFlagManager");
            var manager = go.AddComponent<FlagManager>();

            Assert.IsNotNull(FlagManager.Instance);
            Assert.AreEqual(manager, FlagManager.Instance);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void FlagManager_GetNationFlag_East_ReturnsCorrect()
        {
            var go = new GameObject("TestFlagManager");
            go.AddComponent<FlagManager>();

            var flag = FlagManager.Instance.GetNationFlag(NationType.East);
            Assert.AreEqual(NationType.East, flag.nation);
            Assert.AreEqual("🌅", flag.symbolEmoji);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void FlagManager_GetNationFlag_AllNations()
        {
            var go = new GameObject("TestFlagManager");
            go.AddComponent<FlagManager>();

            var nations = new[] { NationType.East, NationType.West, NationType.South, NationType.North, NationType.Empire, NationType.Dracula };
            foreach (var nation in nations)
            {
                var flag = FlagManager.Instance.GetNationFlag(nation);
                Assert.AreEqual(nation, flag.nation, $"{nation}의 국기 정의가 올바르지 않음");
            }

            Object.DestroyImmediate(go);
        }

        [Test]
        public void FlagManager_OnTerritoryOwnershipChanged_DoesNotThrow()
        {
            var go = new GameObject("TestFlagManager");
            go.AddComponent<FlagManager>();

            // TerritoryBannerSystem이 없어도 예외가 발생하지 않아야 함
            Assert.DoesNotThrow(() =>
            {
                FlagManager.Instance.OnTerritoryOwnershipChanged("test_territory", NationType.East);
            });

            Object.DestroyImmediate(go);
        }

        [Test]
        public void FlagManager_RegisterFlagPole_Works()
        {
            var go = new GameObject("TestFlagManager");
            go.AddComponent<FlagManager>();

            var pole = new GameObject("TestFlagPole");
            FlagManager.Instance.RegisterFlagPole(pole);

            Assert.AreEqual(1, FlagManager.Instance.FlagPoleCount);

            Object.DestroyImmediate(pole);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void FlagManager_UnregisterFlagPole_Works()
        {
            var go = new GameObject("TestFlagManager");
            go.AddComponent<FlagManager>();

            var pole = new GameObject("TestFlagPole");
            FlagManager.Instance.RegisterFlagPole(pole);
            Assert.AreEqual(1, FlagManager.Instance.FlagPoleCount);

            FlagManager.Instance.UnregisterFlagPole(pole);
            Assert.AreEqual(0, FlagManager.Instance.FlagPoleCount);

            Object.DestroyImmediate(pole);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void FlagManager_SetContestedState_DoesNotThrow()
        {
            var go = new GameObject("TestFlagManager");
            go.AddComponent<FlagManager>();

            Assert.DoesNotThrow(() =>
            {
                FlagManager.Instance.SetContestedState("test_territory", true);
                FlagManager.Instance.SetContestedState("test_territory", false);
            });

            Object.DestroyImmediate(go);
        }
    }
}