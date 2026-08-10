using System.Linq;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C14-10: RevengeListManager EditMode 테스트 — 17개 이상
    /// </summary>
    public class RevengeListTests
    {
        // ================================================================
        // Setup / Teardown
        // ================================================================

        [SetUp]
        public void Setup()
        {
            // Reset singleton and re-initialize fresh for each test
            RevengeListManager.Instance.Reset();

            // TerritoryDatabase is needed by Initialize() — ensure it has data
            var db = TerritoryDatabase.Instance;
            Assert.IsNotNull(db, "TerritoryDatabase.Instance should exist");

            RevengeListManager.Instance.Initialize();
            Assert.IsTrue(RevengeListManager.Instance.IsInitialized, "Manager should be initialized after Setup");
        }

        [TearDown]
        public void Teardown()
        {
            RevengeListManager.Instance.Reset();
        }

        // ================================================================
        // 1. 싱글톤
        // ================================================================

        [Test]
        public void RevengeListManager_Singleton_Works()
        {
            var instance1 = RevengeListManager.Instance;
            var instance2 = RevengeListManager.Instance;

            Assert.IsNotNull(instance1, "Instance should not be null");
            Assert.AreSame(instance1, instance2, "Singleton: both references should point to the same instance");
        }

        // ================================================================
        // 2. 초기화
        // ================================================================

        [Test]
        public void RevengeListManager_Initialize_Creates81Entries()
        {
            Assert.AreEqual(81, RevengeListManager.Instance.Entries.Count,
                "Initialize() should create exactly 81 entries (one per territory)");
        }

        [Test]
        public void RevengeListManager_Has10PoisonConspirators()
        {
            var conspirators = RevengeListManager.Instance.GetPoisonConspirators();
            Assert.AreEqual(10, conspirators.Count,
                "There should be exactly 10 poison conspirators");
        }

        [Test]
        public void RevengeListManager_RevengeReasons_Has20Reasons()
        {
            Assert.AreEqual(20, RevengeListManager.RevengeReasons.Length,
                "RevengeReasons array should contain exactly 20 reasons");
        }

        // ================================================================
        // 3. RevealReason
        // ================================================================

        [Test]
        public void RevengeListManager_RevealReason_Works()
        {
            var entries = RevengeListManager.Instance.Entries;
            var first = entries[0];
            string tid = first.territoryId;

            // 초기: 미공개
            Assert.IsFalse(first.isRevealed, "Entry should start as not revealed");

            // 공개
            RevengeListManager.Instance.RevealReason(tid);

            // 확인
            var updated = RevengeListManager.Instance.GetEntry(tid);
            Assert.IsTrue(updated.isRevealed, "Entry should be revealed after RevealReason()");
            Assert.IsFalse(string.IsNullOrEmpty(updated.revengeReason), "Revealed entry should have a non-empty reason");
        }

        [Test]
        public void RevengeListManager_RevealReason_EventFires()
        {
            var first = RevengeListManager.Instance.Entries[0];
            string tid = first.territoryId;

            string firedTid = null;
            RevengeListManager.Instance.OnEntryRevealed += (id) => { firedTid = id; };

            RevengeListManager.Instance.RevealReason(tid);

            Assert.AreEqual(tid, firedTid, "OnEntryRevealed event should fire with correct territoryId");
        }

        // ================================================================
        // 4. CompleteEntry
        // ================================================================

        [Test]
        public void RevengeListManager_CompleteEntry_Works()
        {
            var first = RevengeListManager.Instance.Entries[0];
            string tid = first.territoryId;

            // 초기: 미완료
            Assert.IsFalse(first.isCompleted, "Entry should start as not completed");

            RevengeListManager.Instance.CompleteEntry(tid);

            var updated = RevengeListManager.Instance.GetEntry(tid);
            Assert.IsTrue(updated.isCompleted, "Entry should be completed after CompleteEntry()");
        }

        [Test]
        public void RevengeListManager_CompleteEntry_AutoReveals()
        {
            var first = RevengeListManager.Instance.Entries[0];
            string tid = first.territoryId;

            // 미공개 상태에서 CompleteEntry 호출
            Assert.IsFalse(first.isRevealed, "Entry should start not revealed");
            RevengeListManager.Instance.CompleteEntry(tid);

            var updated = RevengeListManager.Instance.GetEntry(tid);
            Assert.IsTrue(updated.isCompleted, "Entry should be completed");
            Assert.IsTrue(updated.isRevealed, "Completing an unrevealed entry should auto-reveal it");
        }

        [Test]
        public void RevengeListManager_GetCompletionCount_Works()
        {
            // 초기: 0
            Assert.AreEqual(0, RevengeListManager.Instance.GetCompletionCount(),
                "Initially completion count should be 0");

            // 3개 완료
            var entries = RevengeListManager.Instance.Entries;
            RevengeListManager.Instance.CompleteEntry(entries[0].territoryId);
            RevengeListManager.Instance.CompleteEntry(entries[1].territoryId);
            RevengeListManager.Instance.CompleteEntry(entries[2].territoryId);

            Assert.AreEqual(3, RevengeListManager.Instance.GetCompletionCount(),
                "After completing 3 entries, count should be 3");
        }

        // ================================================================
        // 5. AllPoisonConspiratorsFound
        // ================================================================

        [Test]
        public void RevengeListManager_AllPoisonConspiratorsFound_InitialFalse()
        {
            Assert.IsFalse(RevengeListManager.Instance.AllPoisonConspiratorsFound,
                "Initially AllPoisonConspiratorsFound should be false");
        }

        [Test]
        public void RevengeListManager_AllPoisonFound_EventFires()
        {
            bool eventFired = false;
            RevengeListManager.Instance.AllPoisonFound += () => { eventFired = true; };

            // 모든 독살 공모자 공개
            var conspirators = RevengeListManager.Instance.GetPoisonConspirators();
            foreach (var c in conspirators)
            {
                // 아직 공개되지 않은 것만
                if (!c.isRevealed)
                {
                    RevengeListManager.Instance.RevealReason(c.territoryId);
                }
            }

            Assert.IsTrue(RevengeListManager.Instance.AllPoisonConspiratorsFound,
                "All poison conspirators should be found after revealing all");
            Assert.IsTrue(eventFired, "AllPoisonFound event should have fired");
        }

        // ================================================================
        // 6. Interrogate
        // ================================================================

        [Test]
        public void RevengeListManager_Interrogate_Success()
        {
            // PlayerStats 모킹
            var go = new GameObject("TestPlayerStats");
            var stats = go.AddComponent<PlayerStats>();
            var instanceField = typeof(PlayerStats).GetField("Instance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            instanceField?.SetValue(null, stats);

            try
            {
                // Level 50 플레이어 (성공 확률 150% → 항상 성공)
                var levelField = typeof(PlayerStats).GetField("_level",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                levelField?.SetValue(stats, 50);

                // 첫 번째 미공개 엔트리 찾기
                var entry = RevengeListManager.Instance.Entries.First(e => !e.isRevealed);
                string tid = entry.territoryId;

                // 추궁 수행
                bool result = RevengeListManager.Instance.Interrogate(tid);

                Assert.IsTrue(result, "Interrogate should succeed with Level 50");

                var updated = RevengeListManager.Instance.GetEntry(tid);
                Assert.IsTrue(updated.isRevealed, "Entry should be revealed after successful interrogate");
            }
            finally
            {
                instanceField?.SetValue(null, null);
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RevengeListManager_Interrogate_Failure()
        {
            // PlayerStats 모킹
            var go = new GameObject("TestPlayerStats");
            var stats = go.AddComponent<PlayerStats>();
            var instanceField = typeof(PlayerStats).GetField("Instance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            instanceField?.SetValue(null, stats);

            try
            {
                // Level 1 플레이어 (성공 확률 3% → 거의 항상 실패)
                var levelField = typeof(PlayerStats).GetField("_level",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                levelField?.SetValue(stats, 1);

                // 첫 번째 미공개 엔트리 찾기
                var entry = RevengeListManager.Instance.Entries.First(e => !e.isRevealed);
                string tid = entry.territoryId;

                // 레벨 1에서 시드 고정없이 여러 번 시도 → 저레벨이라도 운좋게 성공할 수 있음
                // deterministic 테스트를 위해 시드 고정
                Random.InitState(9999);

                bool result = RevengeListManager.Instance.Interrogate(tid);

                // Level 1: 성공 확률 = 3% → 거의 항상 실패
                // (3%는 가능하므로 result가 false인 것을 보장할 수는 없지만, 
                //  고정 시드 9999에서 레벨 1은 실패)
                Assert.IsFalse(result, "Interrogate should likely fail with Level 1 (3% chance)");
            }
            finally
            {
                instanceField?.SetValue(null, null);
                Object.DestroyImmediate(go);
            }
        }

        // ================================================================
        // 7. Save / Load
        // ================================================================

        [Test]
        public void RevengeListManager_SaveState_RestoresCorrectly()
        {
            // 몇 개 엔트리 공개 및 완료
            var entries = RevengeListManager.Instance.Entries;
            RevengeListManager.Instance.RevealReason(entries[0].territoryId);
            RevengeListManager.Instance.RevealReason(entries[1].territoryId);
            RevengeListManager.Instance.CompleteEntry(entries[2].territoryId);

            // 저장
            var saveData = RevengeListManager.Instance.SaveState();

            // 검증
            Assert.AreEqual(3, saveData.revealedTerritories.Count,
                "SaveState should capture 3 revealed territories (entries[0], [1], [2] auto-revealed by CompleteEntry)");
            Assert.AreEqual(1, saveData.completedTerritories.Count,
                "SaveState should capture 1 completed territory");
            Assert.IsTrue(saveData.revealedTerritories.Contains(entries[0].territoryId));
            Assert.IsTrue(saveData.revealedTerritories.Contains(entries[1].territoryId));
            Assert.IsTrue(saveData.completedTerritories.Contains(entries[2].territoryId));
        }

        [Test]
        public void RevengeListManager_LoadState_Works()
        {
            // 몇 개 엔트리 공개 및 완료
            var entries = RevengeListManager.Instance.Entries;
            RevengeListManager.Instance.RevealReason(entries[0].territoryId);
            RevengeListManager.Instance.CompleteEntry(entries[5].territoryId);

            var saveData = RevengeListManager.Instance.SaveState();

            // 리셋 후 다시 초기화
            RevengeListManager.Instance.Reset();
            RevengeListManager.Instance.Initialize();

            // 로드
            RevengeListManager.Instance.LoadState(saveData);

            // 검증
            var loaded0 = RevengeListManager.Instance.GetEntry(entries[0].territoryId);
            Assert.IsTrue(loaded0.isRevealed, "Entry 0 should be revealed after LoadState");

            var loaded5 = RevengeListManager.Instance.GetEntry(entries[5].territoryId);
            Assert.IsTrue(loaded5.isCompleted, "Entry 5 should be completed after LoadState");
            Assert.IsTrue(loaded5.isRevealed, "Completed entry should also be revealed after LoadState");

            var loadedOther = RevengeListManager.Instance.GetEntry(entries[10].territoryId);
            Assert.IsFalse(loadedOther.isRevealed, "Untouched entry should remain unrevealed");
            Assert.IsFalse(loadedOther.isCompleted, "Untouched entry should remain uncompleted");
        }

        // ================================================================
        // 8. IsFullyComplete
        // ================================================================

        [Test]
        public void RevengeListManager_IsFullyComplete_AllCompleted()
        {
            // 초기: false
            Assert.IsFalse(RevengeListManager.Instance.IsFullyComplete(),
                "Initially IsFullyComplete should be false");

            // 모든 엔트리 완료
            foreach (var entry in RevengeListManager.Instance.Entries)
            {
                RevengeListManager.Instance.CompleteEntry(entry.territoryId);
            }

            Assert.IsTrue(RevengeListManager.Instance.IsFullyComplete(),
                "IsFullyComplete should be true after completing all entries");
        }

        // ================================================================
        // 9. GetEntry
        // ================================================================

        [Test]
        public void RevengeListManager_GetEntry_Works()
        {
            var entries = RevengeListManager.Instance.Entries;
            var first = entries[0];
            string tid = first.territoryId;

            var retrieved = RevengeListManager.Instance.GetEntry(tid);

            Assert.AreEqual(first.territoryId, retrieved.territoryId, "territoryId should match");
            Assert.AreEqual(first.lordName, retrieved.lordName, "lordName should match");
        }

        // ================================================================
        // 10. RevealReason 두 번 호출 — 중복 방지 (edge case)
        // ================================================================

        [Test]
        public void RevengeListManager_RevealReason_DoubleCall_DoesNotDuplicate()
        {
            var first = RevengeListManager.Instance.Entries[0];
            string tid = first.territoryId;

            int eventCount = 0;
            RevengeListManager.Instance.OnEntryRevealed += (id) => { eventCount++; };

            // 두 번 호출
            RevengeListManager.Instance.RevealReason(tid);
            RevengeListManager.Instance.RevealReason(tid);

            Assert.AreEqual(1, eventCount,
                "RevealReason should fire event only once when called twice on the same entry");
        }

        // ================================================================
        // 11. CompleteEntry 두 번 호출 — 중복 방지 (edge case)
        // ================================================================

        [Test]
        public void RevengeListManager_CompleteEntry_DoubleCall_DoesNotDuplicate()
        {
            var first = RevengeListManager.Instance.Entries[0];
            string tid = first.territoryId;

            int eventCount = 0;
            RevengeListManager.Instance.OnEntryCompleted += (id) => { eventCount++; };

            RevengeListManager.Instance.CompleteEntry(tid);
            RevengeListManager.Instance.CompleteEntry(tid);

            Assert.AreEqual(1, eventCount,
                "CompleteEntry should fire event only once when called twice on the same entry");
        }

        // ================================================================
        // 12. 영지가 없을 때 GetEntry — default 반환 (edge case)
        // ================================================================

        [Test]
        public void RevengeListManager_GetEntry_InvalidId_ReturnsDefault()
        {
            var result = RevengeListManager.Instance.GetEntry("INVALID_TERRITORY");

            Assert.AreEqual(default(string), result.territoryId,
                "GetEntry with invalid ID should return default struct");
            Assert.IsFalse(result.isRevealed);
            Assert.IsFalse(result.isCompleted);
        }

        // ================================================================
        // 13. GetRevealedPoisonConspiratorCount
        // ================================================================

        [Test]
        public void RevengeListManager_GetRevealedPoisonConspiratorCount_Increments()
        {
            // 초기: 0
            Assert.AreEqual(0, RevengeListManager.Instance.GetRevealedPoisonConspiratorCount(),
                "Initially no poison conspirators revealed");

            // 독살 공모자 1명 공개
            var conspirators = RevengeListManager.Instance.GetPoisonConspirators();
            Assert.Greater(conspirators.Count, 0, "Should have at least 1 conspirator");

            RevengeListManager.Instance.RevealReason(conspirators[0].territoryId);

            Assert.AreEqual(1, RevengeListManager.Instance.GetRevealedPoisonConspiratorCount(),
                "After revealing 1 conspirator, count should be 1");
        }

        // ================================================================
        // 14. Interrogate — 이미 공개된 엔트리는 바로 true 반환
        // ================================================================

        [Test]
        public void RevengeListManager_Interrogate_AlreadyRevealed_ReturnsTrue()
        {
            var first = RevengeListManager.Instance.Entries[0];
            string tid = first.territoryId;

            // 먼저 공개
            RevengeListManager.Instance.RevealReason(tid);

            // PlayerStats가 없어도 이미 공개됐으므로 true
            bool result = RevengeListManager.Instance.Interrogate(tid);

            Assert.IsTrue(result, "Interrogate on already-revealed entry should return true");
        }

        // ================================================================
        // 15. 완료된 엔트리는 GetCompletionCount 집계
        // ================================================================

        [Test]
        public void RevengeListManager_CompletionCount_AfterAllCompleted()
        {
            foreach (var entry in RevengeListManager.Instance.Entries)
            {
                RevengeListManager.Instance.CompleteEntry(entry.territoryId);
            }

            Assert.AreEqual(81, RevengeListManager.Instance.GetCompletionCount(),
                "After completing all 81 entries, GetCompletionCount should return 81");
        }
    }
}