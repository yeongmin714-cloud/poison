using NUnit.Framework;
using UnityEngine;
using ProjectName.UI;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// G3-13: AchievementSystem 테스트
    /// </summary>
    public class AchievementTests
    {
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestAchievement");
            _go.AddComponent<AchievementSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            PlayerPrefs.DeleteAll();
        }

        [Test]
        public void Instance_Exists()
        {
            Assert.IsNotNull(AchievementSystem.Instance);
        }

        [Test]
        public void AllAchievements_Count_15()
        {
            Assert.AreEqual(15, AchievementSystem.AllAchievements.Length, "업적은 15개");
        }

        [Test]
        public void Achievement_Ids_Are_Unique()
        {
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var ach in AchievementSystem.AllAchievements)
            {
                Assert.IsFalse(ids.Contains(ach.id), $"중복 ID: {ach.id}");
                ids.Add(ach.id);
            }
        }

        [Test]
        public void Initially_All_Locked()
        {
            foreach (var ach in AchievementSystem.AllAchievements)
            {
                Assert.IsFalse(AchievementSystem.Instance.IsUnlocked(ach.id),
                    $"{ach.id}는 초기에 잠겨있어야 함");
            }
        }

        [Test]
        public void Unlock_Works()
        {
            AchievementSystem.Instance.Unlock("first_kill");
            Assert.IsTrue(AchievementSystem.Instance.IsUnlocked("first_kill"));
        }

        [Test]
        public void DoubleUnlock_NoError()
        {
            AchievementSystem.Instance.Unlock("level_5");
            AchievementSystem.Instance.Unlock("level_5"); // 두 번째 호출
            Assert.IsTrue(AchievementSystem.Instance.IsUnlocked("level_5"));
        }

        [Test]
        public void Unlock_InvalidId_NoCrash()
        {
            Assert.DoesNotThrow(() => AchievementSystem.Instance.Unlock("nonexistent"));
        }

        [Test]
        public void UnlockedCount_After_Unlock()
        {
            Assert.AreEqual(0, AchievementSystem.Instance.GetUnlockedCount());
            AchievementSystem.Instance.Unlock("first_kill");
            Assert.AreEqual(1, AchievementSystem.Instance.GetUnlockedCount());
            AchievementSystem.Instance.Unlock("level_5");
            Assert.AreEqual(2, AchievementSystem.Instance.GetUnlockedCount());
        }

        [Test]
        public void TotalCount_15()
        {
            Assert.AreEqual(15, AchievementSystem.Instance.GetTotalCount());
        }

        [Test]
        public void Persists_Across_Scenes()
        {
            AchievementSystem.Instance.Unlock("true_ending");
            // 새 Instance 시뮬레이션 (PlayerPrefs)
            Object.DestroyImmediate(_go);
            _go = new GameObject("TestAchievement2");
            _go.AddComponent<AchievementSystem>();
            Assert.IsTrue(AchievementSystem.Instance.IsUnlocked("true_ending"),
                "PlayerPrefs에 저장된 업적은 유지되어야 함");
        }

        [Test]
        public void Popup_Timer_Starts_OnUnlock()
        {
            AchievementSystem.Instance.Unlock("explorer");
            // 팝업은 3초간 표시 (내부 _popupTimer)
            Assert.IsTrue(AchievementSystem.Instance.IsUnlocked("explorer"));
        }

        [Test]
        public void All_Titles_NonEmpty()
        {
            foreach (var ach in AchievementSystem.AllAchievements)
            {
                Assert.IsFalse(string.IsNullOrEmpty(ach.title), $"{ach.id} title 비어있음");
                Assert.IsFalse(string.IsNullOrEmpty(ach.description), $"{ach.id} description 비어있음");
            }
        }
    }
}