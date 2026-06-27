using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// C9-27: 사냥꾼 임무 — Hunter 역할의 병사가 자동으로 몬스터 사냥
    /// 
    /// 포섭된 Hunter 병사가 주기적으로 주변 AnimalAI를 찾아
    /// 자동 사냥하고 고기/재료를 PlayerInventory에 전달합니다.
    /// </summary>
    public static class HuntingMission
    {
        // 기본 사냥 간격 (초)
        public const float BASE_HUNT_INTERVAL = 8f;
        
        // 사냥 범위
        public const float HUNT_RANGE = 20f;
        
        // 최대 사냥 가능 병사 수
        public const int MAX_HUNTERS = 10;
        
        // 결과
        public struct HuntResult
        {
            public bool success;
            public string message;
            public int itemsGathered;      // 총 아이템 수
            public string monsterName;     // 사냥한 몬스터 ID
            public string hunterName;      // 사냥꾼 이름
        }
        
        /// <summary>
        /// 모든 Hunter 병사의 사냥 수행
        /// </summary>
        public static List<HuntResult> ExecuteHunting()
        {
            var results = new List<HuntResult>();
            var hunters = GetActiveHunters();
            
            if (hunters.Count == 0)
            {
                results.Add(new HuntResult { success = false, message = "활성화된 사냥꾼이 없습니다." });
                return results;
            }
            
            var animals = Object.FindObjectsByType<AnimalAI>(FindObjectsSortMode.None);
            var availableAnimals = new List<AnimalAI>();
            foreach (var a in animals)
            {
                if (a.IsAlive) availableAnimals.Add(a);
            }
            
            if (availableAnimals.Count == 0)
            {
                foreach (var hunter in hunters)
                    results.Add(new HuntResult { success = false, message = $"{hunter.GuardName}: 사냥 가능한 몬스터 없음", hunterName = hunter.GuardName });
                return results;
            }
            
            int animalIndex = 0;
            foreach (var hunter in hunters)
            {
                if (animalIndex >= availableAnimals.Count) break;
                
                var animal = availableAnimals[animalIndex];
                float dist = Vector3.Distance(hunter.transform.position, animal.transform.position);
                
                if (dist > HUNT_RANGE + hunter.Level * 0.5f)
                {
                    results.Add(new HuntResult { success = false, message = $"{hunter.GuardName}: 너무 멀리 있음 ({dist:F1}m)", hunterName = hunter.GuardName });
                    animalIndex++;
                    continue;
                }
                
                if (animal.TryAutoHunt(out var drops))
                {
                    float bonus = GuardStatusSystem.GetActivityBonus(GuardRole.Hunter);
                    int totalItems = 0;
                    string monsterName = animal.MonsterId;
                    
                    foreach (var (item, count) in drops)
                    {
                        int finalCount = Mathf.RoundToInt(count * bonus);
                        bool added = PlayerInventory.Instance != null && PlayerInventory.Instance.AddItem(item, finalCount);
                        totalItems += finalCount;
                    }
                    
                    results.Add(new HuntResult
                    {
                        success = true,
                        message = $"{hunter.GuardName}: {monsterName} 사냥! 아이템 x{totalItems} 획득 ({bonus:F1}배)",
                        itemsGathered = totalItems,
                        monsterName = monsterName,
                        hunterName = hunter.GuardName
                    });
                }
                
                animalIndex++;
            }
            
            return results;
        }
        
        /// <summary>
        /// 활성 Hunter 병사 목록
        /// </summary>
        public static List<GuardPlaceholder> GetActiveHunters()
        {
            var result = new List<GuardPlaceholder>();
            var guards = Object.FindObjectsByType<GuardPlaceholder>(FindObjectsSortMode.None);
            foreach (var g in guards)
            {
                if (g.IsAlive && g.IsRecruited && g.Role == GuardRole.Hunter)
                    result.Add(g);
                if (result.Count >= MAX_HUNTERS) break;
            }
            return result;
        }
    }
}