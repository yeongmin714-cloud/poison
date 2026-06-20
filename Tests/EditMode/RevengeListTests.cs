using System.Linq;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C14-10: 복수명부 시스템 EditMode 테스트 (17개)
    /// </summary>
    [TestFixture]
    public class RevengeListTests
    {
        [SetUp]
        public void Setup()
        {
            RevengeListManager.Instance.Reset();
            RevengeListManager.Instance.Initialize();

            // PlayerStats 생성 (Interrogate 테스트용)
            if (PlayerStats.Instance == null)
            {
                var go = new GameObject("TestPlayerStats");
                go.AddComponent<PlayerStats>();
            }
        }

        [TearDown]
        public void Teardown()
        {
            RevengeListManager.Instance.Reset();
        }

        [Test]
        public void RevengeListManager_Singleton_Works()
        {
            var instance1 = RevengeListManager.Instance;
            var instance2 = RevengeListManager.Instance;
            Assert.AreSame(instance1, instance2, "RevengeListManager는 싱글톤이어야 합니다");
        }

        [Test]
        public void RevengeListManager_Initialize_Creates81Entries()
        {
            Assert.IsTrue(RevengeListManager.Instance.IsInitialized, "초기화 후 IsInitialized는 true여야 합니다");
            Assert.AreEqual(81, RevengeListManager.Instance.Entries.Count, "81개 영지가 필요합니다");
        }

        [Test]
        public void RevengeListManager_Has10PoisonConspirators()
        {
            var conspirators = RevengeListManager.Instance.GetPoisonConspirators();
            Assert.AreEqual(10, conspirators.Count, "독살 공모자는 정확히 10명이어야 합니다");
        }

        [Test]
        public void RevengeListManager_RevealReason_Works()
        {
            var entry = RevengeListManager.Instance.Entries[0];
            Assert.IsFalse(entry.isRevealed, "초기 상태는 미공개여야 합니다");

            RevengeListManager.Instance.RevealReason(entry.territoryId);
            var updated = RevengeListManager.Instance.GetEntry(entry.territoryId);

            Assert.IsTrue(updated.isRevealed, "RevealReason 후 isRevealed가 true여야 합니다");
            Assert.IsNotEmpty(updated.revengeReason, "복수 이유가 비어있지 않아야 합니다");
        }

        [Test]
        public void RevengeListManager_RevealReason_AlreadyRevealed_NoDoubleEvent()
        {
            var entry = RevengeListManager.Instance.Entries[1];
            int eventCount = 0;
            RevengeListManager.Instance.OnEntryRevealed += (id) => eventCount++;

            RevengeListManager.Instance.RevealReason(entry.territoryId);
            RevengeListManager.Instance.RevealReason(entry.territoryId); // 두 번째 호출

            Assert.AreEqual(1, eventCount, "이미 공개된 엔트리는 이벤트를 다시 발생시키지 않아야 합니다");
        }

        [Test]
        public void RevengeListManager_RevealReason_EventFires()
        {
            var entry = RevengeListManager.Instance.Entries[2];
            string firedId = null;
            RevengeListManager.Instance.OnEntryRevealed += (id) => firedId = id;

            RevengeListManager.Instance.RevealReason(entry.territoryId);

            Assert.AreEqual(entry.territoryId, firedId, "OnEntryRevealed 이벤트가 territoryId와 함께 발생해야 합니다");
        }

        [Test]
        public void RevengeListManager_CompleteEntry_Works()
        {
            var entry = RevengeListManager.Instance.Entries[3];

            RevengeListManager.Instance.CompleteEntry(entry.territoryId);
            var updated = RevengeListManager.Instance.GetEntry(entry.territoryId);

            Assert.IsTrue(updated.isCompleted, "CompleteEntry 후 isCompleted가 true여야 합니다");
        }

        [Test]
        public void RevengeListManager_CompleteEntry_AutoReveals()
        {
            var entry = RevengeListManager.Instance.Entries[4];

            RevengeListManager.Instance.CompleteEntry(entry.territoryId);
            var updated = RevengeListManager.Instance.GetEntry(entry.territoryId);

            Assert.IsTrue(updated.isRevealed, "CompleteEntry 시 미공개 엔트리는 자동 공개되어야 합니다");
        }

        [Test]
        public void RevengeListManager_GetCompletionCount_Works()
        {
            Assert.AreEqual(0, RevengeListManager.Instance.GetCompletionCount(), "초기 완료 수는 0이어야 합니다");

            RevengeListManager.Instance.CompleteEntry(RevengeListManager.Instance.Entries[0].territoryId);
            Assert.AreEqual(1, RevengeListManager.Instance.GetCompletionCount(), "1개 완료 후 카운트는 1이어야 합니다");

            RevengeListManager.Instance.CompleteEntry(RevengeListManager.Instance.Entries[1].territoryId);
            Assert.AreEqual(2, RevengeListManager.Instance.GetCompletionCount(), "2개 완료 후 카운트는 2이어야 합니다");
        }

        [Test]
        public void RevengeListManager_AllPoisonConspiratorsFound_InitialFalse()
        {
            Assert.IsFalse(RevengeListManager.Instance.AllPoisonConspiratorsFound, "초기에는 모든 독살 공모자가 발견되지 않은 상태여야 합니다");
        }

        [Test]
        public void RevengeListManager_AllPoisonFound_EventFires()
        {
            bool eventFired = false;
            RevengeListManager.Instance.AllPoisonFound += () => eventFired = true;

            // 10명 독살 공모자 모두 공개
            var conspirators = RevengeListManager.Instance.GetPoisonConspirators();
            foreach (var c in conspirators)
            {
                RevengeListManager.Instance.RevealReason(c.territoryId);
            }

            Assert.IsTrue(eventFired, "모든 독살 공모자 발견 시 AllPoisonFound 이벤트가 발생해야 합니다");
            Assert.IsTrue(RevengeListManager.Instance.AllPoisonConspiratorsFound, "AllPoisonConspiratorsFound가 true여야 합니다");
        }

        [Test]
        public void RevengeListManager_Interrogate_Success()
        {
            var stats = PlayerStats.Instance;
            if (stats == null) Assert.Ignore("PlayerStats 인스턴스 필요");

            // 낮은 레벨에서도 성공할 수 있도록 여러 번 시도
            var entry = RevengeListManager.Instance.Entries[5];
            bool succeeded = false;

            for (int i = 0; i < 50; i++)
            {
                RevengeListManager.Instance.Reset();
                RevengeListManager.Instance.Initialize();
                var testEntry = RevengeListManager.Instance.GetEntry(RevengeListManager.Instance.Entries[5].territoryId);
                if (RevengeListManager.Instance.Interrogate(testEntry.territoryId))
                {
                    succeeded = true;
                    break;
                }
            }

            Assert.IsTrue(succeeded, "추궁은 확률적으로 성공해야 합니다 (50회 시도)");
        }

        [Test]
        public void RevengeListManager_Interrogate_Failure()
        {
            var stats = PlayerStats.Instance;
            if (stats == null) Assert.Ignore("PlayerStats 인스턴스 필요");

            // 최소 레벨에서 실패 테스트
            var entry = RevengeListManager.Instance.Entries[6];
            bool anyFailed = false;

            for (int i = 0; i < 30; i++)
            {
                RevengeListManager.Instance.Reset();
                RevengeListManager.Instance.Initialize();
                var testEntry = RevengeListManager.Instance.GetEntry(RevengeListManager.Instance.Entries[6].territoryId);
                if (!RevengeListManager.Instance.Interrogate(testEntry.territoryId))
                {
                    anyFailed = true;
                    break;
                }
            }

            Assert.IsTrue(anyFailed, "추궁은 확률적으로 실패해야 합니다 (30회 시도)");
        }

        [Test]
        public void RevengeListManager_Interrogate_AlreadyRevealed_ReturnsTrue()
        {
            var entry = RevengeListManager.Instance.Entries[7];
            RevengeListManager.Instance.RevealReason(entry.territoryId);

            bool result = RevengeListManager.Instance.Interrogate(entry.territoryId);
            Assert.IsTrue(result, "이미 공개된 엔트리 추궁은 항상 성공해야 합니다");
        }

        [Test]
        public void RevengeListManager_SaveState_ReturnsCorrectData()
        {
            var entry1 = RevengeListManager.Instance.Entries[0];
            var entry2 = RevengeListManager.Instance.Entries[1];
            RevengeListManager.Instance.RevealReason(entry1.territoryId);
            RevengeListManager.Instance.CompleteEntry(entry2.territoryId);

            var saveData = RevengeListManager.Instance.SaveState();

            Assert.Contains(entry1.territoryId, saveData.revealedTerritories, "공개된 영지가 saveData에 포함되어야 합니다");
            Assert.Contains(entry2.territoryId, saveData.completedTerritories, "완료된 영지가 saveData에 포함되어야 합니다");
        }

        [Test]
        public void RevengeListManager_LoadState_RestoresCorrectly()
        {
            // 저장
            RevengeListManager.Instance.RevealReason(RevengeListManager.Instance.Entries[0].territoryId);
            RevengeListManager.Instance.CompleteEntry(RevengeListManager.Instance.Entries[3].territoryId);
            var saveData = RevengeListManager.Instance.SaveState();

            // 리셋 후 로드
            RevengeListManager.Instance.Reset();
            RevengeListManager.Instance.Initialize();
            RevengeListManager.Instance.LoadState(saveData);

            var loaded1 = RevengeListManager.Instance.GetEntry(RevengeListManager.Instance.Entries[0].territoryId);
            var loaded3 = RevengeListManager.Instance.GetEntry(RevengeListManager.Instance.Entries[3].territoryId);

            Assert.IsTrue(loaded1.isRevealed, "LoadState 후 공개 상태가 복원되어야 합니다");
            Assert.IsTrue(loaded3.isCompleted, "LoadState 후 완료 상태가 복원되어야 합니다");
            Assert.IsTrue(loaded3.isRevealed, "완료된 엔트리는 로드 후에도 공개 상태여야 합니다");
        }

        [Test]
        public void RevengeListManager_IsFullyComplete_NotCompleteInitially()
        {
            Assert.IsFalse(RevengeListManager.Instance.IsFullyComplete(), "초기에는 모든 복수가 완료되지 않은 상태여야 합니다");
        }

        [Test]
        public void RevengeListManager_IsFullyComplete_AllCompleted()
        {
            // 모든 81개 엔트리 완료
            foreach (var entry in RevengeListManager.Instance.Entries.ToList())
            {
                RevengeListManager.Instance.CompleteEntry(entry.territoryId);
            }

            Assert.IsTrue(RevengeListManager.Instance.IsFullyComplete(), "81개 전부 완료 시 IsFullyComplete가 true여야 합니다");
            Assert.AreEqual(81, RevengeListManager.Instance.GetCompletionCount(), "완료 카운트는 81이어야 합니다");
        }

        [Test]
        public void RevengeListManager_RevengeReasons_Has20Reasons()
        {
            Assert.AreEqual(20, RevengeListManager.RevengeReasons.Length, "복수 이유는 정확히 20개여야 합니다");
            Assert.AreEqual("왕의 독살에 직접 가담했다", RevengeListManager.RevengeReasons[0], "첫 번째 이유는 독살 공모자 전용이어야 합니다");
        }

        [Test]
        public void RevengeListManager_GetEntry_Works()
        {
            var first = RevengeListManager.Instance.Entries[0];
            var found = RevengeListManager.Instance.GetEntry(first.territoryId);

            Assert.AreEqual(first.territoryId, found.territoryId);
            Assert.AreEqual(first.lordName, found.lordName);
        }

        [Test]
        public void RevengeListManager_NonConspirator_HasDifferentReason()
        {
            var nonConspirators = RevengeListManager.Instance.Entries
                .Where(e => !e.isPoisonConspirator)
                .Take(5)
                .ToList();

            foreach (var nc in nonConspirators)
            {
                Assert.AreNotEqual("왕의 독살에 직접 가담했다", nc.revengeReason,
                    "독살 공모자가 아닌 영주는 다른 복수 이유를 가져야 합니다");
                Assert.IsNotEmpty(nc.revengeReason, "복수 이유가 비어있지 않아야 합니다");
            }
        }
    }
}