using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// C9-26: 약초꾼 임무 — Herbalist 역할의 병사가 자동으로 약초 채집
    /// 
    /// 포섭된 Herbalist 병사가 주기적으로 주변 HerbPickup을 찾아
    /// 자동 채집하고 PlayerInventory에 전달합니다.
    /// </summary>
    public static class HerbGatheringMission
    {
        // 기본 채집 간격 (초)
        public const float BASE_GATHER_INTERVAL = 5f;
        
        // 채집 범위
        public const float GATHER_RANGE = 15f;
        
        // 최대 채집 가능 병사 수 (성능 제한)
        public const int MAX_GATHERERS = 10;
        
        // 임무 수행 결과
        public struct GatherResult
        {
            public bool success;
            public string message;
            public int herbsGathered;
            public string herbName;
            public string gathererName;
        }
        
        /// <summary>
        /// 모든 Herbalist 병사의 채집 수행 (주기적으로 호출)
        /// </summary>
        public static List<GatherResult> ExecuteGathering()
        {
            var results = new List<GatherResult>();
            var herbalists = GetActiveHerbalists();
            
            if (herbalists.Count == 0)
            {
                results.Add(new GatherResult { success = false, message = "활성화된 약초꾼이 없습니다." });
                return results;
            }
            
            var herbs = Object.FindObjectsOfType<HerbPickup>();
            var availableHerbs = new List<HerbPickup>();
            foreach (var h in herbs)
            {
                if (h.IsAvailable) availableHerbs.Add(h);
            }
            
            if (availableHerbs.Count == 0)
            {
                foreach (var herbalist in herbalists)
                {
                    results.Add(new GatherResult { 
                        success = false, 
                        message = $"{herbalist.GuardName}: 채집 가능한 약초 없음",
                        gathererName = herbalist.GuardName 
                    });
                }
                return results;
            }
            
            // 각 약초꾼에게 약초 할당
            int herbIndex = 0;
            foreach (var herbalist in herbalists)
            {
                if (herbIndex >= availableHerbs.Count) break;
                
                var herb = availableHerbs[herbIndex];
                float dist = Vector3.Distance(herbalist.transform.position, herb.transform.position);
                
                if (dist > GATHER_RANGE + herbalist.Level * 0.5f)
                {
                    results.Add(new GatherResult { 
                        success = false, 
                        message = $"{herbalist.GuardName}: 너무 멀리 있음 ({dist:F1}m)",
                        gathererName = herbalist.GuardName 
                    });
                    herbIndex++;
                    continue;
                }
                
                // 채집 실행
                if (herb.TryAutoGather(out var item, out int yield))
                {
                    float bonus = GuardStatusSystem.GetActivityBonus(GuardRole.Herbalist);
                    int finalYield = Mathf.RoundToInt(yield * bonus);
                    
                    // 인벤토리 전달
                    bool added = PlayerInventory.Instance != null && PlayerInventory.Instance.AddItem(item, finalYield);
                    
                    results.Add(new GatherResult
                    {
                        success = true,
                        message = $"{herbalist.GuardName}: {item.displayName} x{finalYield} 채집! (기본{yield}×{bonus:F1}배)",
                        herbsGathered = finalYield,
                        herbName = item.displayName,
                        gathererName = herbalist.GuardName
                    });
                }
                
                herbIndex++;
            }
            
            return results;
        }
        
        /// <summary>
        /// 활성 Herbalist 병사 목록 반환
        /// </summary>
        public static List<GuardPlaceholder> GetActiveHerbalists()
        {
            var result = new List<GuardPlaceholder>();
            var guards = Object.FindObjectsOfType<GuardPlaceholder>();
            foreach (var g in guards)
            {
                if (g.IsAlive && g.IsRecruited && g.Role == GuardRole.Herbalist)
                    result.Add(g);
                if (result.Count >= MAX_GATHERERS) break;
            }
            return result;
        }
    }
}