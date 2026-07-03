using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// C9-28: 광부 임무 — Miner 역할의 병사가 자동으로 자원 채광
    /// 
    /// 포섭된 Miner 병사가 주기적으로 주변 ResourceNode를 찾아
    /// 자동 채광하고 Wood/Stone/IronOre를 PlayerInventory에 전달합니다.
    /// 채광 후 철광석이 충분하면 자동 제련합니다 (철광석 2개 → 철괴 1개).
    /// </summary>
    public static class MiningMission
    {
        // 기본 채광 간격 (초)
        public const float BASE_MINE_INTERVAL = 6f;

        // 채광 범위 (기본)
        public const float MINE_RANGE = 15f;

        // 최대 채광 가능 병사 수
        public const int MAX_MINERS = 10;

        // 제련 비율: 철광석 2개 → 철괴 1개
        public const int SMELT_ORE_REQUIRED = 2;
        public const string SMELT_OUTPUT_ID = "iron_ingot";

        // 철괴 ItemData (정적 참조용)
        public static PlayerInventory.ItemData IronIngot { get; } = new PlayerInventory.ItemData
        {
            id = "iron_ingot",
            displayName = "철괴",
            description = "철광석을 제련하여 만든 철괴. 무기/방어구 제작 재료.",
            category = PlayerInventory.ItemCategory.Material,
            maxStack = 99
        };

        // 결과
        public struct MineResult
        {
            public bool success;
            public string message;
            public int itemsGathered;      // 총 아이템 수
            public string resourceName;    // 채광한 자원 ID
            public string minerName;       // 광부 이름
        }

        /// <summary>
        /// 모든 Miner 병사의 채광 수행 + 자동 제련
        /// </summary>
        public static List<MineResult> ExecuteMining()
        {
            var results = new List<MineResult>();
            var miners = GetActiveMiners();

            if (miners.Count == 0)
            {
                results.Add(new MineResult { success = false, message = "활성화된 광부가 없습니다." });
                return results;
            }

            var nodes = Object.FindObjectsByType<ResourceNode>();
            var availableNodes = new List<ResourceNode>();
            foreach (var n in nodes)
            {
                if (n.IsAvailable) availableNodes.Add(n);
            }

            if (availableNodes.Count == 0)
            {
                foreach (var miner in miners)
                    results.Add(new MineResult
                    {
                        success = false,
                        message = $"{miner.GuardName}: 채광 가능한 자원 노드 없음",
                        minerName = miner.GuardName
                    });
                return results;
            }

            int nodeIndex = 0;
            foreach (var miner in miners)
            {
                if (nodeIndex >= availableNodes.Count) break;

                var node = availableNodes[nodeIndex];
                float dist = Vector3.Distance(miner.transform.position, node.transform.position);

                if (dist > MINE_RANGE + miner.Level * 0.5f)
                {
                    results.Add(new MineResult
                    {
                        success = false,
                        message = $"{miner.GuardName}: 너무 멀리 있음 ({dist:F1}m)",
                        minerName = miner.GuardName
                    });
                    nodeIndex++;
                    continue;
                }

                if (node.TryAutoMine(out var item, out int yield))
                {
                    float bonus = GuardStatusSystem.GetActivityBonus(GuardRole.Miner);
                    int finalCount = Mathf.RoundToInt(yield * bonus);

                    if (PlayerInventory.Instance != null && PlayerInventory.Instance.AddItem(item, finalCount))
                    {
                        results.Add(new MineResult
                        {
                            success = true,
                            message = $"{miner.GuardName}: {item.displayName} x{finalCount} 채광! ({bonus:F1}배)",
                            itemsGathered = finalCount,
                            resourceName = item.id,
                            minerName = miner.GuardName
                        });
                    }
                    else
                    {
                        results.Add(new MineResult
                        {
                            success = false,
                            message = $"{miner.GuardName}: 인벤토리 공간 부족 — {item.displayName} x{finalCount} 손실",
                            itemsGathered = 0,
                            resourceName = item.id,
                            minerName = miner.GuardName
                        });
                    }
                }

                nodeIndex++;
            }

            // 자동 제련: 채광 후 철광석이 충분하면 제련
            var smeltResults = AutoSmeltIronOre();
            if (smeltResults.Count > 0)
                results.AddRange(smeltResults);

            return results;
        }

        /// <summary>
        /// 인벤토리의 철광석을 자동 제련 (2개 → 철괴 1개)
        /// </summary>
        private static List<MineResult> AutoSmeltIronOre()
        {
            var results = new List<MineResult>();

            if (PlayerInventory.Instance == null)
                return results;

            int oreCount = PlayerInventory.Instance.GetItemCount("iron_ore");

            if (oreCount < SMELT_ORE_REQUIRED)
                return results;

            int smeltCount = oreCount / SMELT_ORE_REQUIRED;
            int oreToRemove = smeltCount * SMELT_ORE_REQUIRED;

            // 먼저 철괴를 넣을 수 있는지 확인 후 제련 실행
            // AddItem이 실패하면 철광석을 제거하지 않음 — 아이템 소실 방지
            if (!PlayerInventory.Instance.AddItem(IronIngot, smeltCount))
            {
                results.Add(new MineResult
                {
                    success = false,
                    message = $"⚙️ 자동 제련 실패: 인벤토리 공간 부족 (철광석 {oreToRemove}개 → 철괴 {smeltCount}개)",
                    itemsGathered = 0,
                    resourceName = "iron_ingot",
                    minerName = "제련소"
                });
                return results;
            }

            // 철광석 제거 (AddItem 성공 후에만 실행)
            if (!PlayerInventory.Instance.RemoveItem("iron_ore", oreToRemove))
            {
                // 이론상 도달 불가: 방어 코드 — 철괴를 이미 추가했으므로 경고만 남김
                UnityEngine.Debug.LogWarning("[MiningMission] AutoSmeltIronOre: 철광석 제거 실패 — 철괴는 이미 추가됨");
            }

            results.Add(new MineResult
            {
                success = true,
                message = $"⚙️ 자동 제련: 철광석 {oreToRemove}개 → 철괴 {smeltCount}개",
                itemsGathered = smeltCount,
                resourceName = "iron_ingot",
                minerName = "제련소"
            });

            return results;
        }

        /// <summary>
        /// 활성 Miner 병사 목록
        /// </summary>
        public static List<GuardPlaceholder> GetActiveMiners()
        {
            var result = new List<GuardPlaceholder>();
            var guards = Object.FindObjectsByType<GuardPlaceholder>();
            foreach (var g in guards)
            {
                if (g.IsAlive && g.IsRecruited && g.Role == GuardRole.Miner)
                    result.Add(g);
                if (result.Count >= MAX_MINERS) break;
            }
            return result;
        }
    }
}