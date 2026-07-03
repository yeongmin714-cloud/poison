using UnityEngine;
using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// 🗺️ 오토루트 (Auto-Route) 시스템 — 아이템을 획득할 수 있는 영지로의 자동 이동 경로를 제공합니다.
    /// MonoBehaviour 싱글톤. IMGUI 기반.
    /// </summary>
    [DefaultExecutionOrder(-45)] // AutoMoveManager(-50)와 FastTravelSystem(-40) 사이
    public class AutoRouteSystem : MonoBehaviour
    {
        private static AutoRouteSystem _instance;
        public static AutoRouteSystem Instance => _instance;

        /// <summary>
        /// 아이템 경로 데이터 구조
        /// </summary>
        public struct ItemRouteData
        {
            public string itemId;
            public string territoryId;     // TerritoryId.ToString() (예: "East_01")
            public string routeDescription; // 예: "동쪽 평화로운 초원에서 채집"
        }

        /// <summary>
        /// 아이템 ID → 경로 데이터 매핑
        /// </summary>
        private Dictionary<string, ItemRouteData> _routeDatabase;
        public IReadOnlyDictionary<string, ItemRouteData> RouteDatabase => _routeDatabase;

        // ===== Unity Lifecycle =====

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[AutoRouteSystem] 중복 인스턴스 감지 — 제거합니다.");
                Destroy(gameObject);
                return;
            }
            _instance = this;
            _routeDatabase = new Dictionary<string, ItemRouteData>();
            InitializeRoutes();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // ===== Public API =====

        /// <summary>
        /// 아이템 ID로 해당 아이템을 획득할 수 있는 영지 경로를 조회합니다.
        /// </summary>
        /// <param name="itemId">아이템 ID</param>
        /// <returns>ItemRouteData (경로 없으면 null)</returns>
        public ItemRouteData? GetRouteForItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return null;

            if (_routeDatabase != null && _routeDatabase.TryGetValue(itemId, out var route))
                return route;

            return null;
        }

        // ===== 데이터베이스 초기화 =====

        /// <summary>
        /// 아이템-영지 매핑 데이터베이스를 구축합니다.
        /// 약초 → 자생 영지, 몬스터 드롭 → 스폰 영지, 자원 → 채광 영지, 상점 → 구매 영지
        /// </summary>
        private void InitializeRoutes()
        {
            if (_routeDatabase == null)
            {
                _routeDatabase = new Dictionary<string, ItemRouteData>();
            }
            _routeDatabase.Clear();

            RegisterHerbRoutes();
            RegisterMonsterDropRoutes();
            RegisterResourceRoutes();
            RegisterShopRoutes();

            Debug.Log($"[AutoRouteSystem] 🗺️ 오토루트 DB 초기화 완료 — {_routeDatabase.Count}개 경로 등록");
        }

        /// <summary>
        /// 약초 아이템 → 자생 영지 매핑
        /// Herb의 속성(Attack/Mental/Recovery/Physical)에 따라 적합한 Biome의 영지에 매핑
        /// </summary>
        private void RegisterHerbRoutes()
        {
            var db = TerritoryDatabase.Instance;
            if (db == null) return;

            // Herb 속성별 선호 Biome 및 국가 매핑
            // Attack(공격성) → Volcanic/Desert (South)
            // Mental(정신성) → Swamp/Rocky (West)
            // Recovery(회복성) → Plains/Forest (East)
            // Physical(물리성) → Tundra/Mountain (North)

            var herbs = HerbDatabase.AllHerbs;
            if (herbs == null || herbs.Count == 0) return;

            foreach (var herb in herbs)
            {
                if (string.IsNullOrEmpty(herb.id)) continue;

                // 속성별로 적합한 영지 찾기
                string targetTerritoryId = null;
                string routeDesc = "";

                // 허브 속성 → 국가 분류
                NationType preferredNation;
                int preferredIndex;

                switch (herb.attribute)
                {
                    case HerbAttribute.Attack:
                        preferredNation = NationType.South;
                        preferredIndex = Mathf.Clamp(herb.index, 1, 20);
                        routeDesc = $"{herb.displayName} — 남부 화산/사막 지대에서 채집 가능";
                        break;
                    case HerbAttribute.Mental:
                        preferredNation = NationType.West;
                        preferredIndex = Mathf.Clamp(herb.index + 5, 1, 20); // 서부 6~15번
                        routeDesc = $"{herb.displayName} — 서부 늪/암석 지대에서 채집 가능";
                        break;
                    case HerbAttribute.Recovery:
                        preferredNation = NationType.East;
                        preferredIndex = Mathf.Clamp(herb.index, 1, 20);
                        routeDesc = $"{herb.displayName} — 동쪽 초원/숲 지대에서 채집 가능";
                        break;
                    case HerbAttribute.Physical:
                        preferredNation = NationType.North;
                        preferredIndex = Mathf.Clamp(herb.index + 10, 1, 20); // 북부 11~20번
                        routeDesc = $"{herb.displayName} — 북부 설원/산악 지대에서 채집 가능";
                        break;
                    default:
                        continue;
                }

                // 영지 정의 확인
                var def = db.GetDefinition(preferredNation, preferredIndex);
                if (def.id.nation != NationType.None)
                {
                    targetTerritoryId = def.id.ToString();
                    routeDesc = $"{herb.displayName} — {def.territoryName}에서 채집 가능";

                    var route = new ItemRouteData
                    {
                        itemId = herb.id,
                        territoryId = targetTerritoryId,
                        routeDescription = routeDesc
                    };

                    // 중복 방지
                    if (!_routeDatabase.ContainsKey(herb.id))
                    {
                        _routeDatabase[herb.id] = route;
                    }
                }
            }
        }

        /// <summary>
        /// 몬스터 드롭 아이템 → 몬스터 스폰 영지 매핑
        /// </summary>
        private void RegisterMonsterDropRoutes()
        {
            var db = TerritoryDatabase.Instance;
            if (db == null) return;

            var monsters = MonsterDataReader.All;
            if (monsters == null || monsters.Count == 0) return;

            int monsterIndex = 0;
            foreach (var kvp in monsters)
            {
                monsterIndex++;
                var monster = kvp.Value;
                if (monster == null || monster.DropItems == null) continue;

                // 몬스터 인덱스 기반 영지 매핑
                // 초급 몬스터(M01~M10) → Ring1 영지 (East/West)
                // 중급 몬스터(M11~M20) → Ring2 영지 (South/North)
                // 고급 몬스터(M21~) → Ring3~4 영지

                NationType nation;
                int index;
                // string biomeHint;

                if (monsterIndex <= 10)
                {
                    nation = NationType.East;
                    index = monsterIndex;
                    biomeHint = "초원";
                }
                else if (monsterIndex <= 20)
                {
                    nation = NationType.West;
                    index = monsterIndex - 10;
                    biomeHint = "황무지";
                }
                else if (monsterIndex <= 30)
                {
                    nation = NationType.South;
                    index = monsterIndex - 20;
                    biomeHint = "화산";
                }
                else
                {
                    nation = NationType.North;
                    index = Mathf.Clamp(monsterIndex - 30, 1, 20);
                    biomeHint = "설원";
                }

                var def = db.GetDefinition(nation, index);
                if (def.id.nation == NationType.None) continue;

                string territoryId = def.id.ToString();

                // 드롭 아이템 각각에 대해 경로 등록
                foreach (var dropItem in monster.DropItems)
                {
                    if (string.IsNullOrEmpty(dropItem)) continue;

                    // 몬스터 이름 기반 ID 생성
                    string dropItemId = $"drop_{monster.Name}_{dropItem}";

                    var route = new ItemRouteData
                    {
                        itemId = dropItemId,
                        territoryId = territoryId,
                        routeDescription = $"{dropItem} — {def.territoryName}에서 {monster.Name} 사냥"
                    };

                    if (!_routeDatabase.ContainsKey(dropItemId))
                    {
                        _routeDatabase[dropItemId] = route;
                    }
                }
            }
        }

        /// <summary>
        /// 자원 아이템(wood/stone/iron_ore) → ResourceNode가 있는 영지 매핑
        /// </summary>
        private void RegisterResourceRoutes()
        {
            var db = TerritoryDatabase.Instance;
            if (db == null) return;

            // 자원 종류별 적합 Biome
            // Wood → Forest (East)
            // Stone → Rocky/Mountain (West/North)
            // IronOre → Volcanic/Mountain (South/North)

            var resourceMappings = new Dictionary<string, (NationType nation, int index, string desc)>
            {
                ["wood"] = (NationType.East, 2, "동쪽 푸른 언덕 — 나무 채집"),
                ["stone"] = (NationType.West, 1, "서부 바위 계곡 — 돌 채광"),
                ["iron_ore"] = (NationType.North, 5, "북부 얼음 계곡 — 철광석 채광")
            };

            foreach (var kvp in resourceMappings)
            {
                string itemId = kvp.Key;
                var (nation, index, desc) = kvp.Value;

                var def = db.GetDefinition(nation, index);
                if (def.id.nation == NationType.None) continue;

                var route = new ItemRouteData
                {
                    itemId = itemId,
                    territoryId = def.id.ToString(),
                    routeDescription = $"{desc} — {def.territoryName}"
                };

                if (!_routeDatabase.ContainsKey(itemId))
                {
                    _routeDatabase[itemId] = route;
                }
            }
        }

        /// <summary>
        /// 상점 아이템 → 상점이 있는 영지 매핑
        /// 각 국가별 주요 영지에 상점이 있다고 가정
        /// </summary>
        private void RegisterShopRoutes()
        {
            var db = TerritoryDatabase.Instance;
            if (db == null) return;

            // 국가별 상점 영지 (Ring1 첫 번째 영지 = 가장 접근성 좋은 영지)
            var shopTerritories = new (NationType nation, int index)[]
            {
                (NationType.East, 1),
                (NationType.West, 1),
                (NationType.South, 1),
                (NationType.North, 1),
                (NationType.Empire, 1)
            };

            // 상점에서 판매하는 일반 아이템
            string[] shopItemIds = {
                "potion_hp", "potion_mp", "bandage", "antidote",
                "torch", "rope", "fishing_rod", "pickaxe"
            };

            foreach (var (nation, index) in shopTerritories)
            {
                var def = db.GetDefinition(nation, index);
                if (def.id.nation == NationType.None) continue;

                string territoryId = def.id.ToString();

                foreach (var itemId in shopItemIds)
                {
                    var route = new ItemRouteData
                    {
                        itemId = itemId,
                        territoryId = territoryId,
                        routeDescription = $"{def.territoryName} — 상점에서 구매 가능"
                    };

                    if (!_routeDatabase.ContainsKey(itemId))
                    {
                        _routeDatabase[itemId] = route;
                    }
                }
            }
        }

        /// <summary>
        /// 외부에서 추가 경로 등록 (런타임 확장용)
        /// </summary>
        public void RegisterRoute(string itemId, string territoryId, string routeDescription)
        {
            if (string.IsNullOrEmpty(itemId) || string.IsNullOrEmpty(territoryId))
                return;

            _routeDatabase[itemId] = new ItemRouteData
            {
                itemId = itemId,
                territoryId = territoryId,
                routeDescription = routeDescription ?? ""
            };
        }

        /// <summary>
        /// DB 재초기화 (런타임 리로드용)
        /// </summary>
        public void ReloadDatabase()
        {
            InitializeRoutes();
        }
    }
}