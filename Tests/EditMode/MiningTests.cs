using NUnit.Framework;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Systems;
using System.Reflection;
using System.Collections.Generic;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C9-28: 광부 임무 테스트 — ResourceNode + MiningMission
    /// </summary>
    public class MiningTests
    {
        // ===================== 헬퍼 =====================

        private static void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
                field.SetValue(obj, value);
        }

        private static GameObject CreateGuard(GuardRole role, int level, bool recruited, bool alive = true)
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            guard.Role = role;
            SetPrivateField(guard, "level", level);
            SetPrivateField(guard, "_isRecruited", recruited);
            if (!alive)
            {
                guard.TakeDamage(999f, Vector3.zero);
            }
            return go;
        }

        private static GameObject CreateResourceNode(ResourceNode.ResourceType type,
            int minYield = 1, int maxYield = 3, float respawnTime = 15f)
        {
            var go = new GameObject("TestNode");
            var node = go.AddComponent<ResourceNode>();
            SetPrivateField(node, "_resourceType", type);
            SetPrivateField(node, "_minYield", minYield);
            SetPrivateField(node, "_maxYield", maxYield);
            SetPrivateField(node, "_respawnTime", respawnTime);
            return go;
        }

        private static GameObject CreatePlayerInventory()
        {
            var go = new GameObject("TestInventory");
            var inv = go.AddComponent<PlayerInventory>();
            return go;
        }

        // ===================== ResourceNode 테스트 =====================

        [Test]
        public void ResourceNode_TryAutoMine_ReturnsItemAndYield()
        {
            var go = CreateResourceNode(ResourceNode.ResourceType.Wood, minYield: 2, maxYield: 2);
            var node = go.GetComponent<ResourceNode>();

            bool result = node.TryAutoMine(out var item, out int yield);

            Assert.IsTrue(result, "TryAutoMine은 성공해야 함");
            Assert.IsNotNull(item, "아이템 데이터가 있어야 함");
            Assert.AreEqual(2, yield, "수확량은 설정한 범위와 일치해야 함");
            Assert.AreEqual("wood", item.id, "나무 아이템이어야 함");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ResourceNode_TryAutoMine_Depleted_ReturnsFalse()
        {
            var go = CreateResourceNode(ResourceNode.ResourceType.Stone, minYield: 1, maxYield: 1);
            var node = go.GetComponent<ResourceNode>();

            // First mine succeeds
            bool firstResult = node.TryAutoMine(out _, out _);
            Assert.IsTrue(firstResult, "첫 번째 채광 성공");

            // Second mine should fail (depleted)
            bool secondResult = node.TryAutoMine(out var item, out int yield);
            Assert.IsFalse(secondResult, "고갈된 노드는 채광 실패");
            Assert.IsNull(item, "아이템은 null");
            Assert.AreEqual(0, yield, "수확량은 0");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ResourceNode_IsAvailable_AfterMine_ReturnsFalse()
        {
            var go = CreateResourceNode(ResourceNode.ResourceType.IronOre);
            var node = go.GetComponent<ResourceNode>();

            Assert.IsTrue(node.IsAvailable, "채광 전에는 사용 가능");

            node.TryAutoMine(out _, out _);

            Assert.IsFalse(node.IsAvailable, "채광 후에는 사용 불가");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ResourceNode_DifferentTypes_ReturnCorrectItems()
        {
            // Wood
            var woodGo = CreateResourceNode(ResourceNode.ResourceType.Wood);
            var woodNode = woodGo.GetComponent<ResourceNode>();
            woodNode.TryAutoMine(out var woodItem, out _);
            Assert.AreEqual("wood", woodItem.id, "Wood 타입은 wood 아이템");
            Assert.AreEqual("나무", woodItem.displayName, "한글 이름 확인");

            // Stone
            var stoneGo = CreateResourceNode(ResourceNode.ResourceType.Stone);
            var stoneNode = stoneGo.GetComponent<ResourceNode>();
            stoneNode.TryAutoMine(out var stoneItem, out _);
            Assert.AreEqual("stone", stoneItem.id, "Stone 타입은 stone 아이템");
            Assert.AreEqual("돌", stoneItem.displayName, "한글 이름 확인");

            // IronOre
            var ironGo = CreateResourceNode(ResourceNode.ResourceType.IronOre);
            var ironNode = ironGo.GetComponent<ResourceNode>();
            ironNode.TryAutoMine(out var ironItem, out _);
            Assert.AreEqual("iron_ore", ironItem.id, "IronOre 타입은 iron_ore 아이템");
            Assert.AreEqual("철광석", ironItem.displayName, "한글 이름 확인");

            Object.DestroyImmediate(woodGo);
            Object.DestroyImmediate(stoneGo);
            Object.DestroyImmediate(ironGo);
        }

        // ===================== MiningMission.ExecuteMining 테스트 =====================

        [Test]
        public void ExecuteMining_NoMiners_ReturnsFailure()
        {
            var results = MiningMission.ExecuteMining();

            Assert.IsNotEmpty(results, "결과 리스트는 비어있지 않아야 함");
            Assert.IsFalse(results[0].success, "광부가 없으면 실패");
            StringAssert.Contains("광부", results[0].message, "광부 없음 메시지");
        }

        [Test]
        public void ExecuteMining_MinerWithNode_MinesSuccess()
        {
            var invGo = CreatePlayerInventory();
            var guardGo = CreateGuard(GuardRole.Miner, 1, true);
            var nodeGo = CreateResourceNode(ResourceNode.ResourceType.Wood, minYield: 2, maxYield: 2);

            guardGo.transform.position = Vector3.zero;
            nodeGo.transform.position = Vector3.one; // ~1.7m apart, well within 15m

            var results = MiningMission.ExecuteMining();

            Assert.IsNotEmpty(results, "결과 존재");
            bool anySuccess = false;
            foreach (var r in results)
            {
                if (r.success && !r.resourceName.Contains("iron"))
                {
                    anySuccess = true;
                    Assert.Greater(r.itemsGathered, 0, "획득 아이템 수 > 0");
                    Assert.AreEqual("wood", r.resourceName, "나무 채광");
                    Assert.IsNotEmpty(r.minerName, "광부 이름 있음");
                }
            }
            Assert.IsTrue(anySuccess, "하나 이상 성공한 채광 결과가 있어야 함");

            Object.DestroyImmediate(nodeGo);
            Object.DestroyImmediate(guardGo);
            Object.DestroyImmediate(invGo);
        }

        [Test]
        public void ExecuteMining_MinerNoNodes_ReturnsNoNodes()
        {
            var guardGo = CreateGuard(GuardRole.Miner, 1, true);
            guardGo.transform.position = Vector3.zero;

            // No resource nodes in scene
            var results = MiningMission.ExecuteMining();

            Assert.IsNotEmpty(results, "결과 존재");
            foreach (var r in results)
            {
                Assert.IsFalse(r.success, "자원 노드 없으면 실패");
                StringAssert.Contains("자원 노드 없음", r.message, "자원 노드 없음 메시지");
            }

            Object.DestroyImmediate(guardGo);
        }

        [Test]
        public void ExecuteMining_DeadMiner_NotIncluded()
        {
            var guardGo = CreateGuard(GuardRole.Miner, 1, true, alive: false);
            var nodeGo = CreateResourceNode(ResourceNode.ResourceType.Wood);

            var active = MiningMission.GetActiveMiners();
            Assert.IsEmpty(active, "죽은 광부는 포함되지 않아야 함");

            Object.DestroyImmediate(nodeGo);
            Object.DestroyImmediate(guardGo);
        }

        [Test]
        public void ExecuteMining_UnrecruitedMiner_NotIncluded()
        {
            var guardGo = CreateGuard(GuardRole.Miner, 1, recruited: false);
            var nodeGo = CreateResourceNode(ResourceNode.ResourceType.Wood);

            var active = MiningMission.GetActiveMiners();
            Assert.IsEmpty(active, "포섭되지 않은 광부는 포함되지 않아야 함");

            Object.DestroyImmediate(nodeGo);
            Object.DestroyImmediate(guardGo);
        }

        [Test]
        public void ExecuteMining_BonusApplied_BasedOnRole()
        {
            var invGo = CreatePlayerInventory();
            var guardGo = CreateGuard(GuardRole.Miner, 1, true);
            var nodeGo = CreateResourceNode(ResourceNode.ResourceType.Wood, minYield: 2, maxYield: 2);

            guardGo.transform.position = Vector3.zero;
            nodeGo.transform.position = Vector3.one;

            var results = MiningMission.ExecuteMining();

            foreach (var r in results)
            {
                if (r.success && r.resourceName == "wood")
                {
                    // Base yield = 2, Miner bonus = 1.5x
                    // Mathf.RoundToInt(2 * 1.5) = 3
                    Assert.AreEqual(3, r.itemsGathered, "1.5배 보너스 적용: 2 → 3");
                    Assert.AreEqual("wood", r.resourceName);
                }
            }

            Object.DestroyImmediate(nodeGo);
            Object.DestroyImmediate(guardGo);
            Object.DestroyImmediate(invGo);
        }

        [Test]
        public void ExecuteMining_MinerOutOfRange_ReturnsTooFar()
        {
            var invGo = CreatePlayerInventory();
            var guardGo = CreateGuard(GuardRole.Miner, 1, true);
            var nodeGo = CreateResourceNode(ResourceNode.ResourceType.Wood, minYield: 1, maxYield: 1);

            guardGo.transform.position = Vector3.zero;
            nodeGo.transform.position = new Vector3(100f, 0, 0);

            var results = MiningMission.ExecuteMining();

            bool anyTooFar = false;
            foreach (var r in results)
            {
                if (!r.success && r.message.Contains("너무 멀리"))
                {
                    anyTooFar = true;
                    break;
                }
            }
            Assert.IsTrue(anyTooFar, "너무 멀리 있음 메시지가 있어야 함");

            Object.DestroyImmediate(nodeGo);
            Object.DestroyImmediate(guardGo);
            Object.DestroyImmediate(invGo);
        }

        [Test]
        public void ExecuteMining_MultipleMiners_AllMine()
        {
            var invGo = CreatePlayerInventory();
            var guardGo1 = CreateGuard(GuardRole.Miner, 1, true);
            var guardGo2 = CreateGuard(GuardRole.Miner, 2, true);
            var nodeGo1 = CreateResourceNode(ResourceNode.ResourceType.Wood, minYield: 1, maxYield: 1);
            var nodeGo2 = CreateResourceNode(ResourceNode.ResourceType.Stone, minYield: 1, maxYield: 1);

            guardGo1.transform.position = Vector3.zero;
            guardGo2.transform.position = new Vector3(5f, 0, 0);
            nodeGo1.transform.position = new Vector3(1f, 0, 0);
            nodeGo2.transform.position = new Vector3(6f, 0, 0);

            var results = MiningMission.ExecuteMining();

            int successCount = 0;
            foreach (var r in results)
            {
                if (r.success && (r.resourceName == "wood" || r.resourceName == "stone"))
                    successCount++;
            }
            Assert.AreEqual(2, successCount, "두 광부 모두 채광 성공");

            Object.DestroyImmediate(nodeGo1);
            Object.DestroyImmediate(nodeGo2);
            Object.DestroyImmediate(guardGo1);
            Object.DestroyImmediate(guardGo2);
            Object.DestroyImmediate(invGo);
        }

        // ===================== GetActiveMiners 테스트 =====================

        [Test]
        public void GetActiveMiners_ReturnsOnlyAliveRecruitedMiners()
        {
            var g1 = CreateGuard(GuardRole.Miner, 1, true);       // should be included
            var g2 = CreateGuard(GuardRole.Miner, 1, false);      // not recruited
            var g3 = CreateGuard(GuardRole.Soldier, 1, true);     // wrong role
            var g4 = CreateGuard(GuardRole.Miner, 1, true, alive: false);  // dead
            var g5 = CreateGuard(GuardRole.Miner, 2, true);       // should be included

            var active = MiningMission.GetActiveMiners();

            Assert.AreEqual(2, active.Count, "살아있고 포섭된 Miner만 포함");

            Object.DestroyImmediate(g1);
            Object.DestroyImmediate(g2);
            Object.DestroyImmediate(g3);
            Object.DestroyImmediate(g4);
            Object.DestroyImmediate(g5);
        }

        [Test]
        public void GetActiveMiners_MaxMiners_StopsAtLimit()
        {
            var guards = new List<GameObject>();
            for (int i = 0; i < MiningMission.MAX_MINERS + 5; i++)
            {
                var g = CreateGuard(GuardRole.Miner, 1, true);
                g.transform.position = new Vector3(i * 10f, 0, 0);
                guards.Add(g);
            }

            var active = MiningMission.GetActiveMiners();

            Assert.LessOrEqual(active.Count, MiningMission.MAX_MINERS, "MAX_MINERS 제한");

            foreach (var g in guards)
                Object.DestroyImmediate(g);
        }

        [Test]
        public void GetActiveMiners_SoldierNotMiner_NotIncluded()
        {
            var g1 = CreateGuard(GuardRole.Soldier, 1, true);
            var g2 = CreateGuard(GuardRole.Miner, 1, true);

            var active = MiningMission.GetActiveMiners();

            Assert.AreEqual(1, active.Count, "Soldier는 포함되지 않아야 함");
            Assert.AreEqual(GuardRole.Miner, active[0].Role, "Miner만 포함");

            Object.DestroyImmediate(g1);
            Object.DestroyImmediate(g2);
        }

        // ===================== 제련(Smelting) 테스트 =====================

        [Test]
        public void ExecuteMining_SmeltIronOre_ConvertsToIngot()
        {
            var invGo = CreatePlayerInventory();
            var guardGo = CreateGuard(GuardRole.Miner, 1, true);
            var nodeGo = CreateResourceNode(ResourceNode.ResourceType.IronOre, minYield: 2, maxYield: 2);

            guardGo.transform.position = Vector3.zero;
            nodeGo.transform.position = Vector3.one;

            // Execute mining which mines iron_ore and should trigger smelting
            var results = MiningMission.ExecuteMining();

            bool hasMineResult = false;
            bool hasSmeltResult = false;
            foreach (var r in results)
            {
                if (r.success && r.resourceName == "iron_ore")
                {
                    hasMineResult = true;
                    // Base yield = 2, Miner bonus = 1.5x => Mathf.RoundToInt(2 * 1.5) = 3
                    Assert.AreEqual(3, r.itemsGathered, "철광석 채광량 (1.5배)");
                }
                if (r.success && r.resourceName == "iron_ingot")
                {
                    hasSmeltResult = true;
                    // 3 iron_ore mined, SMELT_ORE_REQUIRED=2 => 1 ingot (2 ore used, 1 ore remains)
                    Assert.AreEqual(1, r.itemsGathered, "철괴 1개 제련");
                    StringAssert.Contains("철광석 2개", r.message, "2개 소모 메시지");
                }
            }

            Assert.IsTrue(hasMineResult, "채광 결과가 있어야 함");
            Assert.IsTrue(hasSmeltResult, "제련 결과가 있어야 함");

            // Verify inventory: should have 1 iron_ingot and 1 iron_ore remaining
            Assert.AreEqual(1, PlayerInventory.Instance.GetItemCount("iron_ingot"), "철괴 1개");
            Assert.AreEqual(1, PlayerInventory.Instance.GetItemCount("iron_ore"), "철광석 1개 남음");

            Object.DestroyImmediate(nodeGo);
            Object.DestroyImmediate(guardGo);
            Object.DestroyImmediate(invGo);
        }

        [Test]
        public void ExecuteMining_SmeltIronOre_NotEnoughOre_NoSmelt()
        {
            // Manually add only 1 iron_ore to inventory — not enough to smelt (need 2)
            var invGo = CreatePlayerInventory();
            var oreItem = new PlayerInventory.ItemData
            {
                id = "iron_ore",
                displayName = "철광석",
                category = PlayerInventory.ItemCategory.Material,
                maxStack = 99
            };
            PlayerInventory.Instance.AddItem(oreItem, 1);
            Assert.AreEqual(1, PlayerInventory.Instance.GetItemCount("iron_ore"), "1개 철광석 보유");

            // Trigger AutoSmeltIronOre via ExecuteMining (no miners, so only smelting runs)
            var results = MiningMission.ExecuteMining();

            // Verify no smelting happened (only the "no miners" message)
            bool hasSmeltResult = false;
            foreach (var r in results)
            {
                if (r.resourceName == "iron_ingot")
                {
                    hasSmeltResult = true;
                    break;
                }
            }
            Assert.IsFalse(hasSmeltResult, "철광석이 1개만 있으면 제련하지 않음");
            Assert.AreEqual(1, PlayerInventory.Instance.GetItemCount("iron_ore"), "철광석 그대로 1개");
            Assert.AreEqual(0, PlayerInventory.Instance.GetItemCount("iron_ingot"), "철괴 없음");

            Object.DestroyImmediate(invGo);
        }
    }
}