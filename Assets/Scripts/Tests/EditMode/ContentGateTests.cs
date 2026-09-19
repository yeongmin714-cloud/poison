using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// O3 C-O3-03: 컨텐츠 의존성 게이팅 테스트.
    /// 게이트 표 등록/정렬 / IsUnlocked 경계 / 미등록 id 개방 정책 /
    /// GetLockedMessage 형식 / GetGateInfo Nullable 동작.
    /// PlayerStats 싱글턴은 EditMode에서 미생성 → 단일 인자 GetLockedMessage는 기본 레벨 1 폴백 경로만 검증.
    /// </summary>
    public class ContentGateTests
    {
        // ── GetAllGates ───────────────────────────────────────────────────

        [Test]
        public void GetAllGates_Count_Is6()
        {
            Assert.AreEqual(6, ContentGate.GetAllGates().Length, "등록 게이트 6종");
        }

        [Test]
        public void GetAllGates_SortedByMinLevel_Ascending()
        {
            var gates = ContentGate.GetAllGates();
            int prev = int.MinValue;
            foreach (var gate in gates)
            {
                Assert.GreaterOrEqual(gate.minLevel, prev,
                    $"minLevel 오름차순 정렬 위반 @ {gate.contentId} ({prev} → {gate.minLevel})");
                prev = gate.minLevel;
            }
        }

        [Test]
        public void GetAllGates_ContainsAllSixRegisteredIds()
        {
            var gates = ContentGate.GetAllGates();
            Assert.AreEqual(6, gates.Length);
            int found = 0;
            foreach (var gate in gates)
            {
                if (gate.contentId == "tavern_mercenary") found++;
                if (gate.contentId == "gem_chest") found++;
                if (gate.contentId == "bomb_craft") found++;
                if (gate.contentId == "warehouse_expansion_2") found++;
                if (gate.contentId == "warehouse_expansion_3") found++;
                if (gate.contentId == "dracula_territory") found++;
            }
            Assert.AreEqual(6, found, "6종 id 전부 등록");
        }

        [Test]
        public void GetAllGates_AllEntries_HaveDisplayNameAndDescription()
        {
            foreach (var gate in ContentGate.GetAllGates())
            {
                Assert.IsFalse(string.IsNullOrEmpty(gate.displayName), $"{gate.contentId} displayName 누락");
                Assert.IsFalse(string.IsNullOrEmpty(gate.description), $"{gate.contentId} description 누락");
                Assert.Greater(gate.minLevel, 0, $"{gate.contentId} minLevel 양수");
            }
        }

        // ── IsUnlocked ────────────────────────────────────────────────────

        [Test]
        public void IsUnlocked_TavernMercenary_Lv4_False()
        {
            Assert.IsFalse(ContentGate.IsUnlocked("tavern_mercenary", 4), "용병 고용 Lv4 = 잠금");
        }

        [Test]
        public void IsUnlocked_TavernMercenary_Lv5_True()
        {
            Assert.IsTrue(ContentGate.IsUnlocked("tavern_mercenary", 5), "용병 고용 Lv5 = 개방");
        }

        [Test]
        public void IsUnlocked_GemChest_Lv9_False_Lv10_True()
        {
            Assert.IsFalse(ContentGate.IsUnlocked("gem_chest", 9), "보석상자 Lv9 = 잠금");
            Assert.IsTrue(ContentGate.IsUnlocked("gem_chest", 10), "보석상자 Lv10 = 개방");
        }

        [Test]
        public void IsUnlocked_BombCraft_Lv7_False_Lv8_True()
        {
            Assert.IsFalse(ContentGate.IsUnlocked("bomb_craft", 7), "폭탄 제작 Lv7 = 잠금");
            Assert.IsTrue(ContentGate.IsUnlocked("bomb_craft", 8), "폭탄 제작 Lv8 = 개방");
        }

        [Test]
        public void IsUnlocked_WarehouseExpansion2_Lv11_False_Lv12_True()
        {
            Assert.IsFalse(ContentGate.IsUnlocked("warehouse_expansion_2", 11), "창고 2차 확장 Lv11 = 잠금");
            Assert.IsTrue(ContentGate.IsUnlocked("warehouse_expansion_2", 12), "창고 2차 확장 Lv12 = 개방");
        }

        [Test]
        public void IsUnlocked_DraculaTerritory_Lv19_False_Lv20_True()
        {
            Assert.IsFalse(ContentGate.IsUnlocked("dracula_territory", 19), "드라큘라 영지 Lv19 = 잠금");
            Assert.IsTrue(ContentGate.IsUnlocked("dracula_territory", 20), "드라큘라 영지 Lv20 = 개방");
        }

        [Test]
        public void IsUnlocked_WarehouseExpansion3_Lv19_False_Lv20_True()
        {
            Assert.IsFalse(ContentGate.IsUnlocked("warehouse_expansion_3", 19), "창고 3차 확장 Lv19 = 잠금");
            Assert.IsTrue(ContentGate.IsUnlocked("warehouse_expansion_3", 20), "창고 3차 확장 Lv20 = 개방");
        }

        [Test]
        public void IsUnlocked_UnregisteredId_AlwaysTrue_EvenLowLevel()
        {
            // 게이팅 없음 정책 — 미등록 id는 전 레벨 개방
            Assert.IsTrue(ContentGate.IsUnlocked("crafting", 1), "미등록 id Lv1 = 개방");
            Assert.IsTrue(ContentGate.IsUnlocked("cooking", 3), "미등록 id Lv3 = 개방");
            Assert.IsTrue(ContentGate.IsUnlocked("herb_gathering", 1), "미등록 id = 개방");
            Assert.IsTrue(ContentGate.IsUnlocked("unknown_content", 50), "미등록 id = 개방");
        }

        [Test]
        public void IsUnlocked_NullOrEmptyId_True()
        {
            Assert.IsTrue(ContentGate.IsUnlocked(null, 1), "null id = 개방");
            Assert.IsTrue(ContentGate.IsUnlocked("", 1), "빈 id = 개방");
        }

        // ── GetLockedMessage ──────────────────────────────────────────────

        [Test]
        public void GetLockedMessage_BelowMinLevel_ContainsLevelKeywordAndNumbers()
        {
            string msg = ContentGate.GetLockedMessage("tavern_mercenary", 3);
            StringAssert.Contains("레벨", msg, "안내 문구에 '레벨' 포함");
            StringAssert.Contains("5", msg, "요구 레벨 5 표기");
            StringAssert.Contains("3", msg, "현재 레벨 3 표기");
            StringAssert.Contains("용병 고용", msg, "컨텐츠 표시명 포함");
        }

        [Test]
        public void GetLockedMessage_RequirementMet_ReturnsEmpty()
        {
            Assert.AreEqual("", ContentGate.GetLockedMessage("tavern_mercenary", 5), "레벨 충족 = 빈 문자열");
            Assert.AreEqual("", ContentGate.GetLockedMessage("gem_chest", 99), "레벨 충족 = 빈 문자열");
        }

        [Test]
        public void GetLockedMessage_UnregisteredId_ReturnsEmpty()
        {
            Assert.AreEqual("", ContentGate.GetLockedMessage("crafting", 1), "미등록 id = 안내 없음");
            Assert.AreEqual("", ContentGate.GetLockedMessage("nope", 1), "미등록 id = 안내 없음");
        }

        [Test]
        public void GetLockedMessage_SingleArg_NoPlayerStats_FallsBackToLevel1()
        {
            // EditMode에서 PlayerStats 싱글턴 미생성 → 기본 레벨 1 폴백 (안전 경로만 검증)
            string msg = ContentGate.GetLockedMessage("tavern_mercenary");
            if (PlayerStats.Instance == null)
            {
                Assert.IsFalse(string.IsNullOrEmpty(msg), "레벨 1 폴백 → 미달 안내 생성");
                StringAssert.Contains("레벨", msg);
            }
        }

        // ── GetGateInfo (Nullable struct) ─────────────────────────────────

        [Test]
        public void GetGateInfo_Registered_HasValue_AndFieldsPopulated()
        {
            var gate = ContentGate.GetGateInfo("gem_chest");
            Assert.IsTrue(gate.HasValue, "등록 id → HasValue true");
            Assert.AreEqual(10, gate.Value.minLevel);
            Assert.AreEqual("동굴 보석상자", gate.Value.displayName);
            Assert.AreEqual("gem_chest", gate.Value.contentId);
            Assert.IsFalse(string.IsNullOrEmpty(gate.Value.description));
        }

        [Test]
        public void GetGateInfo_Unregistered_HasValueFalse()
        {
            var gate = ContentGate.GetGateInfo("unknown_content");
            Assert.IsFalse(gate.HasValue, "미등록 id → HasValue false (null)");
        }
    }
}
