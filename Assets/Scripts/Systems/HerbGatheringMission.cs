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
    ///
    /// C9-27 경작지 자동 수확 연동:
    /// FarmPlot은 숙성(Ready) 시 자기 오브젝트에 HerbPickup을 부착하므로(AttachHerbPickup),
    /// 아래 HerbPickup 검색만으로 소유 경작지의 Ready 약초도 함께 채집 대상이 된다.
    /// 단, 경작지 출신 약초는 플레이어 소유 영지(FarmPlot.IsOwned)에서만 수확하며,
    /// 성장 중인 플롯은 HerbPickup이 부착되지 않아 자동으로 무시된다.
    /// AutoMissionManager가 5초 주기로 ExecuteGathering()을 호출하므로 별도 타이머는 불필요.
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
            
            // C9-27: 씬의 모든 HerbPickup 수집 — 야생 약초 + FarmPlot Ready 시 부착된 약초 모두 포함.
            // 경작지 출신 약초는 플레이어 소유 영지(FarmPlot.IsOwned)에서만 자동 수확한다.
            // (미소유/소유 상실 직후 ValidateOwnership 1.5초 주기 검사 사이의 창에서 선수확되는 것 방지)
            var herbs = Object.FindObjectsByType<HerbPickup>();
            var availableHerbs = new List<HerbPickup>(herbs.Length);
            var farmPlotHerbs = new HashSet<HerbPickup>();   // 경작지 출신 약초 (로그 태깅용)
            foreach (var h in herbs)
            {
                if (h == null || !h.IsAvailable) continue;

                var plot = h.GetComponent<FarmPlot>();
                if (plot != null)
                {
                    if (!plot.IsOwned)
                    {
                        Debug.Log($"[HerbGatheringMission] {plot.name}: 미소유 경작지 — 자동 수확 제외");
                        continue;
                    }
                    farmPlotHerbs.Add(h);
                }
                availableHerbs.Add(h);
            }

            // 경작지 Ready 약초가 채집 대상에 편입되었음을 로그로 확인 (자동 수확 연동 검증용)
            if (farmPlotHerbs.Count > 0)
                Debug.Log($"[HerbGatheringMission] 🌾 경작지(FarmPlot) Ready 약초 {farmPlotHerbs.Count}개 채집 대상에 포함");
            
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
                    if (PlayerInventory.Instance != null && PlayerInventory.Instance.AddItem(item, finalYield))
                    {
                        // C9-27: 경작지 출신 약초 자동 수확 로그 (플레이어 E키 수확은 LootBasket 드롭, 자동은 인벤토리 직행)
                        if (farmPlotHerbs.Contains(herb))
                            Debug.Log($"[HerbGatheringMission] 🌾 {herbalist.GuardName}: 경작지 {item.displayName} x{finalYield} 자동 수확 → 인벤토리");

                        results.Add(new GatherResult
                        {
                            success = true,
                            message = $"{herbalist.GuardName}: {item.displayName} x{finalYield} 채집! (기본{yield}×{bonus:F1}배)",
                            herbsGathered = finalYield,
                            herbName = item.displayName,
                            gathererName = herbalist.GuardName
                        });
                    }
                    else
                    {
                        // NOTE: TryAutoGather가 이미 약초를 소비(harvest)했으므로
                        // AddItem 실패 시 아이템이 소실됩니다. 이는 HerbPickup.TryAutoGather의
                        // 설계상 한계로, 향후에는 소비 전 인벤토리 확인 로직으로 개선 필요.
                        Debug.LogWarning($"[HerbGatheringMission] {herbalist.GuardName}: {item.displayName} x{finalYield} 인벤토리 추가 실패 (가득 참) — 약초가 소실되었습니다.");
                        results.Add(new GatherResult
                        {
                            success = false,
                            message = $"{herbalist.GuardName}: {item.displayName} 채집 실패 (인벤토리 가득 참)",
                            herbsGathered = 0,
                            herbName = item.displayName,
                            gathererName = herbalist.GuardName
                        });
                    }
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
            var guards = Object.FindObjectsByType<GuardPlaceholder>();
            foreach (var g in guards)
            {
                if (g != null && g.isActiveAndEnabled && g.IsAlive && g.IsRecruited && g.Role == GuardRole.Herbalist)
                    result.Add(g);
                if (result.Count >= MAX_GATHERERS) break;
            }
            return result;
        }
    }
}