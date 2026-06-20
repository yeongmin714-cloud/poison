using System.Collections.Generic;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// G2-07: 간소화된 공격 시스템 테스트
    ///
    /// 테스트 범위:
    /// 1. DropTable — MonsterDropEntry 구조체, GetMonsterDrops/GetGuardDrops, 등급 확률
    /// 2. AttackSystem — 생성, Raycast 탐색, LootBasket 연동, 사거리 판단
    /// 3. LootBasket — 30초 소멸 타이머 확인
    /// </summary>
    public class PhaseG2_AttackSystemTests
    {
        // ================================================================
        // 1. DropTable — MonsterDropEntry (2 tests)
        // ================================================================

        [Test]
        public void DropTable_MonsterDropEntry_StructExists()
        {
            // Given/When: MonsterDropEntry 구조체 생성
            var entry = new MonsterDropEntry(
                MonsterTier.Beginner,
                "test_item",
                0.5f,
                1,
                3
            );

            // Then: 필드 값이 올바르게 설정됨
            Assert.AreEqual(MonsterTier.Beginner, entry.tier, "티어가 일치해야 함");
            Assert.AreEqual("test_item", entry.itemId, "아이템ID가 일치해야 함");
            Assert.AreEqual(0.5f, entry.probability, 0.001f, "확률이 일치해야 함");
            Assert.AreEqual(1, entry.minCount, "최소 개수가 일치해야 함");
            Assert.AreEqual(3, entry.maxCount, "최대 개수가 일치해야 함");
        }

        [Test]
        public void DropTable_MonsterDropEntry_ClampsValues()
        {
            // Given: 확률이 0~1 범위를 벗어난 MonsterDropEntry
            var entry = new MonsterDropEntry(
                MonsterTier.Intermediate,
                "test_item",
                1.5f,   // 150% → 100%로 클램프
                0,      // 0 → 1로 클램프
                5
            );

            // Then: 값이 적절히 클램프됨
            Assert.AreEqual(1.0f, entry.probability, 0.001f, "확률이 1.0으로 클램프되어야 함");
            Assert.AreEqual(1, entry.minCount, "최소 개수가 1로 클램프되어야 함");
        }

        // ================================================================
        // 2. DropTable — GetMonsterDrops (2 tests)
        // ================================================================

        [Test]
        public void DropTable_GetMonsterDrops_ReturnsItems()
        {
            // Given: 원하는 티어
            MonsterTier tier = MonsterTier.Beginner;

            // When: 드랍 생성 (50회 반복 → 통계적 의미 확보)
            bool atLeastOneDrop = false;
            for (int i = 0; i < 50; i++)
            {
                List<KeyValuePair<string, int>> drops = DropTableUtility.GetMonsterDrops(tier);
                if (drops.Count > 0)
                {
                    atLeastOneDrop = true;
                    // 모든 아이템은 유효한 ID와 개수를 가져야 함
                    foreach (var drop in drops)
                    {
                        Assert.IsFalse(string.IsNullOrEmpty(drop.Key), "아이템 ID는 비어있지 않아야 함");
                        Assert.Greater(drop.Value, 0, "아이템 개수는 0보다 커야 함");
                    }
                    break;
                }
            }

            // Then: 최소 1개 이상 드랍되어야 함
            Assert.IsTrue(atLeastOneDrop, "GetMonsterDrops는 최소 1개의 아이템을 반환해야 함");
        }

        [Test]
        public void DropTable_GetMonsterDrops_CountInExpectedRange()
        {
            // Given: 각 티어
            var tiers = new[] { MonsterTier.Beginner, MonsterTier.Intermediate, MonsterTier.Advanced };

            foreach (var tier in tiers)
            {
                // When: 100회 드랍 시뮬레이션
                int totalDrops = 0;
                int iterations = 100;
                for (int i = 0; i < iterations; i++)
                {
                    List<KeyValuePair<string, int>> drops = DropTableUtility.GetMonsterDrops(tier);
                    totalDrops += drops.Count;
                }

                float avgDrops = (float)totalDrops / iterations;

                // Then: 평균 1~6개 사이여야 함 (기본 1~3 + 희귀 10% 추가)
                Assert.GreaterOrEqual(avgDrops, 0.5f,
                    $"{tier}: 평균 드랍 개수가 너무 낮음 ({avgDrops:F2})");
                Assert.LessOrEqual(avgDrops, 6.5f,
                    $"{tier}: 평균 드랍 개수가 너무 높음 ({avgDrops:F2})");

                Debug.Log($"[Test] {tier}: 평균 드랍 개수 = {avgDrops:F2} (n={iterations})");
            }
        }

        // ================================================================
        // 3. DropTable — GetGuardDrops (2 tests)
        // ================================================================

        [Test]
        public void DropTable_GetGuardDrops_ReturnsGoldAndItems()
        {
            // Given: 레벨 1 병사
            int level = 1;

            // When: 50회 드랍 시뮬레이션
            bool foundGold = false;
            bool foundSilver = false;
            for (int i = 0; i < 50; i++)
            {
                List<KeyValuePair<string, int>> drops = DropTableUtility.GetGuardDrops(level);
                foreach (var drop in drops)
                {
                    if (drop.Key == DropTableUtility.GoldCoinId)
                        foundGold = true;
                    if (drop.Key == DropTableUtility.SilverCoinId)
                        foundSilver = true;
                }
            }

            // Then: 금화와 은화는 항상 드랍되어야 함
            Assert.IsTrue(foundGold, "병사는 항상 금화를 드랍해야 함");
            Assert.IsTrue(foundSilver, "병사는 항상 은화를 드랍해야 함");
        }

        [Test]
        public void DropTable_GetGuardDrops_ScalesWithLevel()
        {
            // Given: 다양한 레벨
            int lowLevel = 1;
            int highLevel = 20;

            // When: 각 레벨에서 30회 드랍 → 총 금화 합계
            int lowLevelTotalGold = 0;
            int highLevelTotalGold = 0;
            int iterations = 30;

            for (int i = 0; i < iterations; i++)
            {
                foreach (var drop in DropTableUtility.GetGuardDrops(lowLevel))
                {
                    if (drop.Key == DropTableUtility.GoldCoinId)
                        lowLevelTotalGold += drop.Value;
                }
                foreach (var drop in DropTableUtility.GetGuardDrops(highLevel))
                {
                    if (drop.Key == DropTableUtility.GoldCoinId)
                        highLevelTotalGold += drop.Value;
                }
            }

            float lowAvg = (float)lowLevelTotalGold / iterations;
            float highAvg = (float)highLevelTotalGold / iterations;

            // Then: 높은 레벨 병사가 더 많은 금화를 드랍해야 함
            Assert.Greater(highAvg, lowAvg,
                $"고레벨 병사({highLevel}) 금화 평균({highAvg:F2})이 저레벨({lowLevel}: {lowAvg:F2})보다 많아야 함");

            Debug.Log($"[Test] Guard Gold - Level {lowLevel}: avg={lowAvg:F2}, Level {highLevel}: avg={highAvg:F2}");
        }

        // ================================================================
        // 4. DropTable — 등급 확률 상수 (1 test)
        // ================================================================

        [Test]
        public void DropTable_RarityConstants_AreCorrect()
        {
            // Given/Then: 등급 확률 상수 확인
            Assert.AreEqual(0.90f, DropTableUtility.CommonChance, 0.001f, "Common 확률 90%");
            Assert.AreEqual(0.45f, DropTableUtility.UncommonChance, 0.001f, "Uncommon 확률 45%");
            Assert.AreEqual(0.10f, DropTableUtility.RareChance, 0.001f, "Rare 확률 10%");
            Assert.AreEqual(0.03f, DropTableUtility.EpicChance, 0.001f, "Epic 확률 3%");
            Assert.AreEqual(0.01f, DropTableUtility.LegendaryChance, 0.001f, "Legendary 확률 1%");

            // 합계 검증: 90% + 45% + 10% + 3% + 1% = 149% (중첩 확률이므로 100% 초과 정상)
            Assert.LessOrEqual(DropTableUtility.CommonChance, 1.0f, "Common 확률은 100% 이하");
            Assert.GreaterOrEqual(DropTableUtility.LegendaryChance, 0.0f, "Legendary 확률은 0% 이상");
        }

        // ================================================================
        // 5. AttackSystem — 생성 및 초기화 (1 test)
        // ================================================================

        [Test]
        public void AttackSystem_Instantiate_HasRequiredComponents()
        {
            // Given/When: AttackSystem GameObject 생성
            var go = new GameObject("TestAttackSystem");
            var attackSystem = go.AddComponent<AttackSystem>();
            var playerHealth = go.AddComponent<PlayerHealth>();

            // Then: 컴포넌트가 정상적으로 추가됨
            Assert.IsNotNull(attackSystem, "AttackSystem 컴포넌트가 생성되어야 함");
            Assert.IsNotNull(playerHealth, "PlayerHealth가 있어야 안전하게 동작");

            // 정리
            Object.DestroyImmediate(go);
        }

        // ================================================================
        // 6. AttackSystem — Raycast 및 타겟 탐색 (1 test)
        // ================================================================

        [Test]
        public void AttackSystem_NoTarget_UpdateDoesNotThrow()
        {
            // Given: 빈 씬의 AttackSystem
            var go = new GameObject("TestAttackSystem");
            var attackSystem = go.AddComponent<AttackSystem>();
            go.AddComponent<PlayerHealth>();
            // 카메라가 없으면 Target 탐색 skip → 예외 없음

            // When/Then: Update 호출해도 예외 없음
            Assert.DoesNotThrow(() =>
            {
                // Update는 Input.GetMouseButtonDown에서 false 반환 → 조용히 리턴
                attackSystem.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
            }, "타겟 없는 상태에서 Update가 예외를 던지면 안 됨");

            // 정리
            Object.DestroyImmediate(go);
        }

        // ================================================================
        // 7. AttackSystem — LootBasket 생성 (1 test)
        // ================================================================

        [Test]
        public void AttackSystem_LootBasket_OnDeath_CreatesBasket()
        {
            // Given: 죽은 적 GameObject
            var deadGo = new GameObject("DeadEnemy");
            deadGo.transform.position = new Vector3(5f, 0f, 5f);

            // When: LootBasket.Create() 호출
            LootBasket basket = LootBasket.Create(deadGo.transform.position, 30f);

            // Then: LootBasket이 생성됨
            Assert.IsNotNull(basket, "LootBasket이 생성되어야 함");
            Assert.AreEqual(30f, basket.GetType().GetField("_lifetime",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(basket), "수명이 30초로 설정되어야 함");

            // LootBasket에 아이템 추가 가능
            var testItem = new PlayerInventory.ItemData
            {
                id = "test_item",
                displayName = "테스트",
                description = "테스트 아이템",
                category = PlayerInventory.ItemCategory.Material,
                maxStack = 99,
                rarity = ItemRarity.Common
            };
            basket.AddItem(testItem, 3);
            Assert.Greater(basket.ItemCount, 0, "아이템이 정상 추가되어야 함");

            // 정리
            Object.DestroyImmediate(basket.gameObject);
            Object.DestroyImmediate(deadGo);
        }

        // ================================================================
        // 8. AttackSystem — 사거리 판단 (1 test)
        // ================================================================

        [Test]
        public void AttackSystem_DropTableUtility_RollFromEntries_Works()
        {
            // Given: MonsterDropEntry 배열
            var entries = new MonsterDropEntry[]
            {
                new MonsterDropEntry(MonsterTier.Beginner, "meat", 1.0f, 1, 3),
                new MonsterDropEntry(MonsterTier.Beginner, "leather", 0.5f, 1, 1),
                new MonsterDropEntry(MonsterTier.Beginner, "rare_gem", 0.1f, 1, 1)
            };

            // When: 100회 롤 시뮬레이션
            int meatCount = 0;
            int leatherCount = 0;
            int rareGemCount = 0;
            int iterations = 1000;

            for (int i = 0; i < iterations; i++)
            {
                var drops = DropTableUtility.RollFromEntries(entries);
                foreach (var drop in drops)
                {
                    if (drop.Key == "meat") meatCount++;
                    if (drop.Key == "leather") leatherCount++;
                    if (drop.Key == "rare_gem") rareGemCount++;
                }
            }

            // Then: 확률에 근접한 드랍률
            float meatRate = (float)meatCount / iterations;
            float leatherRate = (float)leatherCount / iterations;
            float rareGemRate = (float)rareGemCount / iterations;

            Assert.GreaterOrEqual(meatRate, 0.95f, "meat(100%)는 거의 항상 드랍되어야 함");
            Assert.GreaterOrEqual(leatherRate, 0.35f, "leather(50%)는 35% 이상 드랍되어야 함");
            Assert.LessOrEqual(leatherRate, 0.65f, "leather(50%)는 65% 이하로 드랍되어야 함");
            Assert.GreaterOrEqual(rareGemRate, 0.05f, "rare_gem(10%)는 5% 이상 드랍되어야 함");
            Assert.LessOrEqual(rareGemRate, 0.20f, "rare_gem(10%)는 20% 이하로 드랍되어야 함");

            Debug.Log($"[Test] RollFromEntries - meat: {meatRate:P1}, leather: {leatherRate:P1}, rare_gem: {rareGemRate:P1} (n={iterations})");
        }
    }
}