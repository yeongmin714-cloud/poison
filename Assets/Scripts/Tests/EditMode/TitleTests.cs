using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C-O6-01/02: 칭호 시스템 테스트 — 레지스트리 20종/임계 평가/카운터/발급/장착/저장 라운드트립.
    /// PlayerPrefs 실사용 — 테스트 오염 방지 위해 키를 사전/사후 정리.
    /// </summary>
    public class TitleTests
    {
        private TitleManager _tm;

        [SetUp]
        public void SetUp()
        {
            CleanupKeys();
            var go = new GameObject("TitleManagerTest");
            _tm = go.AddComponent<TitleManager>();
            _tm.ResetAll();
        }

        [TearDown]
        public void TearDown()
        {
            if (_tm != null && _tm.gameObject != null)
                Object.DestroyImmediate(_tm.gameObject);
            CleanupKeys();
        }

        private static void CleanupKeys()
        {
            PlayerPrefs.DeleteKey("titles_counts_v1");
            PlayerPrefs.DeleteKey("titles_unlocked_v1");
            PlayerPrefs.DeleteKey("titles_equipped_v1");
            PlayerPrefs.Save();
        }

        // ===================== 레지스트리 =====================

        [Test]
        public void All_ContainsExactly20UniqueTitles()
        {
            Assert.AreEqual(20, TitleData.Count);
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var t in TitleData.All)
                Assert.IsTrue(ids.Add(t.id), $"중복 id 없음: {t.id}");
        }

        [Test]
        public void All_EveryTitleHasDisplayAndSource()
        {
            foreach (var t in TitleData.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(t.displayName), $"{t.id} 표시명");
                Assert.IsFalse(string.IsNullOrEmpty(t.sourceType), $"{t.id} 소스");
                Assert.Greater(t.threshold, 0, $"{t.id} 임계 > 0");
            }
        }

        [Test]
        public void TryGet_RegisteredAndUnknown()
        {
            Assert.IsTrue(TitleData.TryGet("execution_10", out var def));
            Assert.AreEqual("십인의 처형자", def.displayName);
            Assert.IsFalse(TitleData.TryGet("nope_999", out _));
        }

        [Test]
        public void GetBySource_FiltersCorrectly()
        {
            Assert.AreEqual(5, TitleData.GetBySource("executions").Length);
            Assert.AreEqual(4, TitleData.GetBySource("assassinations").Length);
            Assert.AreEqual(5, TitleData.GetBySource("conquests").Length);
            Assert.AreEqual(3, TitleData.GetBySource("set_complete").Length);
            Assert.AreEqual(3, TitleData.GetBySource("crafts").Length);
            Assert.AreEqual(0, TitleData.GetBySource("unknown_src").Length);
        }

        // ===================== 임계 평가 (순수) =====================

        [Test]
        public void EvaluateUnlocks_ExecutionThresholds()
        {
            Assert.AreEqual(1, TitleManager.EvaluateUnlocks("executions", 1).Length, "1회 → 첫 칭호");
            Assert.AreEqual(2, TitleManager.EvaluateUnlocks("executions", 3).Length, "3회 → 2개");
            Assert.AreEqual(3, TitleManager.EvaluateUnlocks("executions", 5).Length);
            Assert.AreEqual(5, TitleManager.EvaluateUnlocks("executions", 25).Length, "전량 해금");
            Assert.AreEqual(0, TitleManager.EvaluateUnlocks("executions", 0).Length);
        }

        [Test]
        public void EvaluateUnlocks_Boundary_BelowThreshold()
        {
            Assert.AreEqual(2, TitleManager.EvaluateUnlocks("executions", 4).Length, "4회 = 1/3 임계만 (5 미달)");
            Assert.AreEqual(4, TitleManager.EvaluateUnlocks("conquests", 20).Length, "20회 = 1/5/10/20");
        }

        // ===================== RecordEvent/카운터/발급 =====================

        [Test]
        public void RecordEvent_CountsAndUnlocks()
        {
            _tm.RecordEvent("assassinations", 1);
            Assert.AreEqual(1, _tm.GetCount("assassinations"));
            Assert.IsTrue(_tm.IsUnlocked("assassin_1"), "1회 = 그림자의 칼");
            Assert.IsFalse(_tm.IsUnlocked("assassin_5"));

            _tm.RecordEvent("assassinations", 4);
            Assert.AreEqual(5, _tm.GetCount("assassinations"));
            Assert.IsTrue(_tm.IsUnlocked("assassin_5"));
        }

        [Test]
        public void RecordEvent_IgnoresInvalid()
        {
            _tm.RecordEvent(null);
            _tm.RecordEvent("", 5);
            _tm.RecordEvent("assassinations", 0);
            Assert.AreEqual(0, _tm.GetCount("assassinations"));
        }

        [Test]
        public void RecordEvent_ZeroOrNegativeAmount_Ignored()
        {
            _tm.RecordEvent("conquests", -3);
            Assert.AreEqual(0, _tm.GetCount("conquests"));
        }

        // ===================== 장착 =====================

        [Test]
        public void EquipTitle_UnlockedOnly()
        {
            _tm.RecordEvent("executions", 1);
            Assert.IsFalse(_tm.IsUnlocked("execution_3"), "3회 미달 — 미발급");

            _tm.EquipTitle("execution_3"); // 미발급 — 무시
            Assert.AreEqual("", _tm.GetTitleText(), "미발급 장착 무시");

            _tm.EquipTitle("execution_1");
            Assert.AreEqual("execution_1", _tm.EquippedTitleId);
            Assert.AreEqual("첫 처형을 집행한 자", _tm.GetTitleText());
        }

        [Test]
        public void GetTitleText_NoEquip_ReturnsEmpty()
        {
            Assert.AreEqual("", _tm.GetTitleText());
        }

        // ===================== 저장 라운드트립 =====================

        [Test]
        public void SaveLoad_Roundtrip_PreservesCountsUnlockedEquipped()
        {
            _tm.RecordEvent("executions", 5);
            _tm.RecordEvent("assassinations", 1);
            _tm.EquipTitle("assassin_1");

            // Save 검증 — 직렬화 문자열에 상태 반영
            string counts = PlayerPrefs.GetString("titles_counts_v1", "");
            StringAssert.Contains("executions:5", counts);
            StringAssert.Contains("assassinations:1", counts);
            string unlocked = PlayerPrefs.GetString("titles_unlocked_v1", "");
            StringAssert.Contains("execution_5", unlocked);
            StringAssert.Contains("assassin_1", unlocked);
            Assert.AreEqual("assassin_1", PlayerPrefs.GetString("titles_equipped_v1", ""));

            // Load 검증 — 수제 시리얼라이즈 복원
            PlayerPrefs.SetString("titles_counts_v1", "crafts:7|executions:2");
            PlayerPrefs.SetString("titles_unlocked_v1", "craft_50|execution_3");
            PlayerPrefs.SetString("titles_equipped_v1", "craft_50");
            _tm.Load();

            Assert.AreEqual(7, _tm.GetCount("crafts"));
            Assert.AreEqual(2, _tm.GetCount("executions"));
            Assert.IsTrue(_tm.IsUnlocked("craft_50"));
            Assert.IsTrue(_tm.IsUnlocked("execution_3"));
            Assert.AreEqual("craft_50", _tm.EquippedTitleId);
        }

        [Test]
        public void ResetAll_ClearsEverything()
        {
            _tm.RecordEvent("executions", 5);
            _tm.EquipTitle("execution_1");
            _tm.ResetAll();
            Assert.AreEqual(0, _tm.GetCount("executions"));
            Assert.IsFalse(_tm.IsUnlocked("execution_1"));
            Assert.AreEqual("", _tm.GetTitleText());
        }

        // ===================== 발급 이벤트 =====================

        [Test]
        public void TitleUnlockedEvent_FiresOnFirstUnlock()
        {
            TitleDef fired = default;
            bool firedOnce = false;
            System.Action<TitleDef> handler = d => { fired = d; firedOnce = true; };
            TitleManager.TitleUnlocked += handler;

            try
            {
                _tm.RecordEvent("executions", 1);
                Assert.IsTrue(firedOnce, "첫 해금 시 이벤트 발화");
                Assert.AreEqual("execution_1", fired.id);

                _tm.RecordEvent("executions", 1); // 재기록 — 이미 해금, 이벤트 없음
                // fired는 그대로 (재발화 없음 검증: firedOnce 후 id 동일)
                _tm.RecordEvent("assassinations", 1);
                Assert.AreEqual("assassin_1", fired.id, "새 해금 시 이벤트 갱신");
            }
            finally
            {
                TitleManager.TitleUnlocked -= handler;
            }
        }
    }
}
