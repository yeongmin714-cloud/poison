using System.Collections.Generic;
using NUnit.Framework;
using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C-O1-02: DropTable 시그니처(확정) 드랍 테스트.
    /// 시그니처 100% 지급 / 독립 롤 중복 제거 / 하위호환(빈 시그니처 = 기존 동작) / 기대치 계산.
    /// </summary>
    public class DropSignatureTests
    {
        // ── 테스트 더블: ILootBasket 최소 구현 (id 스택 집계) ──
        private class FakeBasket : ILootBasket
        {
            private readonly List<LootEntry> _items = new List<LootEntry>();

            public IReadOnlyList<LootEntry> Items => _items;
            public bool IsAvailable => true;
            public bool IsEmpty => _items.Count == 0;
            public int ItemCount => _items.Count;
            public string BasketName => "FakeBasket";

            public bool TakeItem(int index) { if (index < 0 || index >= _items.Count) return false; _items.RemoveAt(index); return true; }
            public bool TakeAll() { _items.Clear(); return true; }

            public void AddItem(PlayerInventory.ItemData item, int count = 1)
            {
                if (item == null || count <= 0) return;
                foreach (var entry in _items)
                {
                    if (entry.Item != null && entry.Item.id == item.id)
                    {
                        entry.Count += count;
                        return;
                    }
                }
                _items.Add(new LootEntry { Item = item, Count = count });
            }

            public int CountOf(string id)
            {
                foreach (var entry in _items)
                {
                    if (entry.Item != null && entry.Item.id == id) return entry.Count;
                }
                return 0;
            }

            public bool Has(string id)
            {
                foreach (var entry in _items)
                {
                    if (entry.Item != null && entry.Item.id == id) return true;
                }
                return false;
            }
        }

        private static PlayerInventory.ItemData MakeItem(string id, string name)
        {
            return new PlayerInventory.ItemData
            {
                id = id,
                displayName = name,
                description = "테스트 아이템",
                category = PlayerInventory.ItemCategory.Material,
                maxStack = 99,
            };
        }

        private static DropTable.SignatureDropEntry MakeSig(PlayerInventory.ItemData item, int min = 1, int max = 1)
        {
            return new DropTable.SignatureDropEntry { item = item, minCount = min, maxCount = max, description = "확정" };
        }

        private static DropTable.DropEntry MakeEntry(PlayerInventory.ItemData item, float chance, bool isRare = false)
        {
            return new DropTable.DropEntry { item = item, minCount = 1, maxCount = 1, dropChance = chance, isRare = isRare };
        }

        // ===================== 시그니처 100% 지급 =====================

        [Test]
        public void ApplyToBasket_EmptyEntries_SignatureAlwaysDrops()
        {
            var table = ScriptableObject.CreateInstance<DropTable>();
            var sigItem = MakeItem("sig_armor", "서명 갑옷");
            table.signatureEntries = new[] { MakeSig(sigItem) };
            table.entries = new DropTable.DropEntry[0];

            var basket = new FakeBasket();
            table.ApplyToBasket(basket);

            Assert.IsTrue(basket.Has("sig_armor"), "시그니처는 확률 롤 없이 100% 지급");
            Assert.AreEqual(1, basket.CountOf("sig_armor"));
        }

        [Test]
        public void ApplyToBasket_MultipleSignatures_AllDrop()
        {
            var table = ScriptableObject.CreateInstance<DropTable>();
            table.signatureEntries = new[]
            {
                MakeSig(MakeItem("sig_a", "A")),
                MakeSig(MakeItem("sig_b", "B")),
            };
            table.entries = new DropTable.DropEntry[0];

            var basket = new FakeBasket();
            table.ApplyToBasket(basket);

            Assert.IsTrue(basket.Has("sig_a"));
            Assert.IsTrue(basket.Has("sig_b"));
        }

        [Test]
        public void ApplyToBasket_SignatureCountRange_AppliesRange()
        {
            var table = ScriptableObject.CreateInstance<DropTable>();
            table.signatureEntries = new[] { MakeSig(MakeItem("sig_x3", "X"), 3, 3) };
            table.entries = new DropTable.DropEntry[0];

            var basket = new FakeBasket();
            table.ApplyToBasket(basket);

            Assert.AreEqual(3, basket.CountOf("sig_x3"));
        }

        // ===================== 중복 제거 (시그니처는 독립 롤에서 제외) =====================

        [Test]
        public void ApplyToBasket_SignatureExcludedFromIndependentRoll()
        {
            var table = ScriptableObject.CreateInstance<DropTable>();
            var dup = MakeItem("dup_item", "중복 아이템");
            table.signatureEntries = new[] { MakeSig(dup) };
            table.entries = new[] { MakeEntry(dup, 1f) }; // 동일 id, 확률 100% → 중복 위험

            var basket = new FakeBasket();
            table.ApplyToBasket(basket);

            Assert.AreEqual(1, basket.CountOf("dup_item"),
                "시그니처로 이미 지급된 id는 독립 롤에서 제외 — 정확히 1개");
        }

        [Test]
        public void ApplyToBasket_SignatureExcludedFromRareBonusRoll()
        {
            var table = ScriptableObject.CreateInstance<DropTable>();
            var rareDup = MakeItem("rare_dup", "희귀 중복");
            rareDup.rarity = ItemRarity.Rare;
            table.signatureEntries = new[] { MakeSig(rareDup) };
            // isRare=true + dropChance=1 → 기본 롤과 희귀 보너스 롤 양쪽에서 시도될 수 있음
            table.entries = new[] { MakeEntry(rareDup, 1f, isRare: true) };

            var basket = new FakeBasket();
            table.ApplyToBasket(basket);

            Assert.AreEqual(1, basket.CountOf("rare_dup"),
                "희귀 보너스 롤에서도 시그니처 id는 제외 — 정확히 1개");
        }

        // ===================== 하위호환 (빈 시그니처 = 기존 동작) =====================

        [Test]
        public void ApplyToBasket_NullSignature_BehavesAsBefore()
        {
            var table = ScriptableObject.CreateInstance<DropTable>();
            var a = MakeItem("normal_a", "A");
            table.signatureEntries = null;
            table.entries = new[] { MakeEntry(a, 1f) }; // 100% → 결정론적

            var basket = new FakeBasket();
            table.ApplyToBasket(basket);

            Assert.IsTrue(basket.Has("normal_a"), "시그니처 없으면 기존 동작 동일");
            Assert.AreEqual(1, basket.ItemCount);
        }

        [Test]
        public void ApplyToBasket_EmptySignature_BehavesAsBefore()
        {
            var table = ScriptableObject.CreateInstance<DropTable>();
            var a = MakeItem("normal_b", "B");
            table.signatureEntries = new DropTable.SignatureDropEntry[0];
            table.entries = new[] { MakeEntry(a, 1f) };

            var basket = new FakeBasket();
            table.ApplyToBasket(basket);

            Assert.IsTrue(basket.Has("normal_b"));
        }

        // ===================== 레벨 보정 독립성 =====================

        [Test]
        public void ApplyToBasket_LevelBonusDoesNotAffectSignature()
        {
            var table = ScriptableObject.CreateInstance<DropTable>();
            table.signatureEntries = new[] { MakeSig(MakeItem("sig_lv", "레벨 무관")) };
            table.entries = new DropTable.DropEntry[0];

            var basket = new FakeBasket();
            table.ApplyToBasket(basket, 0.5f); // 큰 레벨 보정 — 시그니처에는 영향 없음

            Assert.AreEqual(1, basket.CountOf("sig_lv"),
                "시그니처는 레벨 보정 없이 항상 1회 지급");
        }

        // ===================== 조회 API =====================

        [Test]
        public void HasSignatureDrops_NullOrEmptyOrInvalid_ReturnsFalse()
        {
            var t1 = ScriptableObject.CreateInstance<DropTable>();
            Assert.IsFalse(t1.HasSignatureDrops, "null 시그니처");

            var t2 = ScriptableObject.CreateInstance<DropTable>();
            t2.signatureEntries = new DropTable.SignatureDropEntry[0];
            Assert.IsFalse(t2.HasSignatureDrops, "빈 시그니처");

            var t3 = ScriptableObject.CreateInstance<DropTable>();
            t3.signatureEntries = new[] { new DropTable.SignatureDropEntry { item = null } };
            Assert.IsFalse(t3.HasSignatureDrops, "item 미할당 시그니처");
        }

        [Test]
        public void HasSignatureDrops_WithValidItem_ReturnsTrue()
        {
            var table = ScriptableObject.CreateInstance<DropTable>();
            table.signatureEntries = new[] { MakeSig(MakeItem("sig_has", "H")) };
            Assert.IsTrue(table.HasSignatureDrops);
        }

        [Test]
        public void GetSignatureSummary_DescribesEntries()
        {
            var table = ScriptableObject.CreateInstance<DropTable>();
            Assert.IsTrue(table.GetSignatureSummary().Contains("없음"), "빈 시그니처 요약");

            table.signatureEntries = new[] { MakeSig(MakeItem("sig_sum", "요약 검"), 1, 2) };
            string summary = table.GetSignatureSummary();
            StringAssert.Contains("요약 검", summary);
            StringAssert.Contains("확정", summary);
        }

        // ===================== 기대 드랍 개수 (C-O1-03 튜닝용) =====================

        [Test]
        public void GetExpectedDropCount_NoEntries_ReturnsZero()
        {
            var table = ScriptableObject.CreateInstance<DropTable>();
            Assert.AreEqual(0f, table.GetExpectedDropCount(), 0.0001f);
        }

        [Test]
        public void GetExpectedDropCount_SingleEntry_ChanceTimesAvgCount()
        {
            var table = ScriptableObject.CreateInstance<DropTable>();
            var e = MakeEntry(MakeItem("exp_50", "50%"), 0.5f);
            e.maxCount = 1;
            table.entries = new[] { e };
            Assert.AreEqual(0.5f, table.GetExpectedDropCount(), 0.0001f);
        }

        [Test]
        public void GetExpectedDropCount_ExcludesSignatureIds()
        {
            var table = ScriptableObject.CreateInstance<DropTable>();
            var sig = MakeItem("exp_sig", "시그니처");
            table.signatureEntries = new[] { MakeSig(sig) };

            var dupEntry = MakeEntry(sig, 1f);        // 시그니처 id — 기대치 제외
            var normalEntry = MakeEntry(MakeItem("exp_n", "일반"), 1f); // 1.0 × 평균1 = 1.0
            table.entries = new[] { dupEntry, normalEntry };

            Assert.AreEqual(1.0f, table.GetExpectedDropCount(), 0.0001f,
                "시그니처 id 항목은 기대치에서 제외");
        }
    }
}
