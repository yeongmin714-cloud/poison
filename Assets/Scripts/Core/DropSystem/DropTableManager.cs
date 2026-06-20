using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Core
{
    /// <summary>
    /// G2-07: 드랍 테이블 관리자 (싱글톤)
    ///
    /// 몬스터/병사 티어별 드랍 테이블을 제공합니다.
    /// Resources 폴더에서 자동 로드하거나 Inspector에서 직접 할당 가능.
    /// 
    /// AnimalAI.Die()에서 LootBasket.Create() 후 호출됩니다.
    /// 
    /// ND-05: 드라큘라 전용 드랍 테이블 추가.
    /// </summary>
    public class DropTableManager : MonoBehaviour
    {
        public static DropTableManager Instance { get; private set; }

        [Header("몬스터 드랍 테이블 (티어별)")]
        [SerializeField] private DropTable _earlyMonsterTable;
        [SerializeField] private DropTable _midMonsterTable;
        [SerializeField] private DropTable _lateMonsterTable;

        [Header("병사 드랍 테이블")]
        [SerializeField] private DropTable _soldierTable;

        [Header("드라큘라 전용 (ND-05)")]
        [SerializeField] private DropTable _draculaLordTable;
        [SerializeField] private DropTable _skeletonGuardTable;

        [Header("설정")]
        [SerializeField] private bool _autoLoadFromResources = true;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (_autoLoadFromResources)
                LoadDefaultTables();
        }

        /// <summary>
        /// Resources/DropTables/ 경로에서 기본 테이블 로드
        /// </summary>
        private void LoadDefaultTables()
        {
            if (_earlyMonsterTable == null)
                _earlyMonsterTable = Resources.Load<DropTable>("DropTables/EarlyMonsterDropTable");
            if (_midMonsterTable == null)
                _midMonsterTable = Resources.Load<DropTable>("DropTables/MidMonsterDropTable");
            if (_lateMonsterTable == null)
                _lateMonsterTable = Resources.Load<DropTable>("DropTables/LateMonsterDropTable");
            if (_soldierTable == null)
                _soldierTable = Resources.Load<DropTable>("DropTables/SoldierDropTable");
            if (_draculaLordTable == null)
                _draculaLordTable = Resources.Load<DropTable>("DropTables/DraculaLordDropTable");
            if (_skeletonGuardTable == null)
                _skeletonGuardTable = Resources.Load<DropTable>("DropTables/SkeletonGuardDropTable");
        }

        /// <summary>
        /// 몬스터 티어별 드랍 테이블 반환
        /// </summary>
        public DropTable GetMonsterTable(MonsterTier tier)
        {
            return tier switch
            {
                MonsterTier.Beginner => _earlyMonsterTable,
                MonsterTier.Intermediate => _midMonsterTable,
                MonsterTier.Advanced => _lateMonsterTable,
                _ => _earlyMonsterTable,
            };
        }

        /// <summary>
        /// 병사 드랍 테이블 반환
        /// </summary>
        public DropTable GetSoldierTable() => _soldierTable;

        /// <summary>
        /// 모든 드랍 테이블을 LootBasket에 적용합니다.
        /// 레벨 보정 확률을 포함합니다.
        /// </summary>
        /// <param name="table">드랍 테이블</param>
        /// <param name="basket">LootBasket</param>
        /// <param name="level">몬스터/병사 레벨 (레벨 보정용)</param>
        public void ApplyTableToBasket(DropTable table, ILootBasket basket, int level = 1)
        {
            if (table == null || basket == null) return;

            // 레벨 기반 희귀 드랍 보정 (레벨 10당 +5%)
            float levelDropBonus = Mathf.Min(level * 0.005f, 0.5f);

            table.ApplyToBasket(basket, levelDropBonus);
        }

        /// <summary>
        /// 티어 기반으로 몬스터 드랍 테이블을 찾아 LootBasket에 적용합니다.
        /// </summary>
        public void ApplyMonsterDrops(MonsterTier tier, ILootBasket basket, int level = 1)
        {
            DropTable table = GetMonsterTable(tier);
            ApplyTableToBasket(table, basket, level);
        }

        /// <summary>
        /// 병사 드랍 테이블을 LootBasket에 적용합니다.
        /// </summary>
        public void ApplySoldierDrops(ILootBasket basket, int level = 1)
        {
            ApplyTableToBasket(_soldierTable, basket, level);
        }

        // ===== 드라큘라 전용 (ND-05) =====

        /// <summary>
        /// 드라큘라 영주 드랍 테이블 반환
        /// </summary>
        public DropTable GetDraculaLordTable() => _draculaLordTable;

        /// <summary>
        /// 드라큘라 영주 드랍 아이템 목록 반환 (프리뷰/UI용)
        /// 전설 장비 3종 + 희귀 재료 2종 + 전설 레시피 1종
        /// </summary>
        public List<PlayerInventory.ItemData> GetDraculaLordDrops()
        {
            var items = new List<PlayerInventory.ItemData>();

            // 전설 장비 3종
            items.Add(new PlayerInventory.ItemData
            {
                id = "dracula_vampire_sword", displayName = "흡혈의 검",
                description = "적의 피를 빨아들이는 전설의 검",
                category = PlayerInventory.ItemCategory.Weapon, maxStack = 1
            });
            items.Add(new PlayerInventory.ItemData
            {
                id = "dracula_dark_cloak", displayName = "드라큘라의 망토",
                description = "드라큘라 백작이 사용하던 전설의 망토",
                category = PlayerInventory.ItemCategory.Armor, maxStack = 1
            });
            items.Add(new PlayerInventory.ItemData
            {
                id = "dracula_red_moon_ring", displayName = "붉은 달의 반지",
                description = "붉은 달의 힘이 깃든 전설의 반지",
                category = PlayerInventory.ItemCategory.Material, maxStack = 1
            });

            // 희귀 재료 2종
            items.Add(new PlayerInventory.ItemData
            {
                id = "dracula_dragon_breath_herb", displayName = "용의 숨결 풀",
                description = "용의 숨결이 닿은 희귀한 약초",
                category = PlayerInventory.ItemCategory.Material, maxStack = 99
            });
            items.Add(new PlayerInventory.ItemData
            {
                id = "dracula_golden_herb", displayName = "황금 약초",
                description = "금빛으로 빛나는 희귀한 약초",
                category = PlayerInventory.ItemCategory.Material, maxStack = 99
            });

            // 전설 레시피 1종
            items.Add(new PlayerInventory.ItemData
            {
                id = "dracula_night_elixir_recipe", displayName = "야간의 비약 레시피",
                description = "야간의 비약을 제조하는 전설의 레시피",
                category = PlayerInventory.ItemCategory.Quest, maxStack = 1
            });

            return items;
        }

        /// <summary>
        /// 드라큘라 영주 사망 시 호출 — 전설 등급 아이템 드랍
        /// </summary>
        public void ApplyDraculaLordDrops(ILootBasket basket)
        {
            if (_draculaLordTable != null)
            {
                ApplyTableToBasket(_draculaLordTable, basket, 50);
                return;
            }

            // Fallback: Resources에 테이블이 없으면 프로그램적으로 생성
            Debug.Log("[DropTableManager] 드라큘라 영주 드랍 테이블이 없습니다. 기본 드랍을 생성합니다.");
            ApplyDraculaLordDropsFallback(basket);
        }

        /// <summary>
        /// 스켈레톤 병사 드랍 테이블 반환
        /// </summary>
        public DropTable GetSkeletonGuardTable() => _skeletonGuardTable;

        /// <summary>
        /// 스켈레톤 병사 드랍 아이템 목록 반환 (프리뷰/UI용)
        /// 중급 재료 2종 + 희귀 재료 10% 확률
        /// </summary>
        public List<PlayerInventory.ItemData> GetSkeletonGuardDrops()
        {
            var items = new List<PlayerInventory.ItemData>();

            // 중급 재료 2종
            items.Add(new PlayerInventory.ItemData
            {
                id = "skeleton_bone_powder", displayName = "뼛가루",
                description = "스켈레톤을 갈아 만든 중급 재료",
                category = PlayerInventory.ItemCategory.Material, maxStack = 99
            });
            items.Add(new PlayerInventory.ItemData
            {
                id = "skeleton_rusty_armor", displayName = "녹슨 갑옷 조각",
                description = "스켈레톤 병사가 사용하던 낡은 갑옷 조각",
                category = PlayerInventory.ItemCategory.Material, maxStack = 99
            });

            // 희귀 재료 (10% 확률로 표시)
            var rareItem = new PlayerInventory.ItemData
            {
                id = "skeleton_ancient_coin", displayName = "고대의 주화",
                description = "스켈레톤이 지니고 있던 희귀한 고대 주화",
                category = PlayerInventory.ItemCategory.Material, maxStack = 99
            };
            items.Add(rareItem);

            return items;
        }

        /// <summary>
        /// 스켈레톤 병사 사망 시 호출
        /// </summary>
        public void ApplySkeletonGuardDrops(ILootBasket basket, int level = 30)
        {
            if (_skeletonGuardTable != null)
            {
                ApplyTableToBasket(_skeletonGuardTable, basket, level);
                return;
            }

            Debug.Log("[DropTableManager] 스켈레톤 병사 드랍 테이블이 없습니다. 기본 드랍을 생성합니다.");
            ApplySkeletonGuardDropsFallback(basket, level);
        }

        /// <summary>
        /// 드라큘라 영주 기본 드랍 (Resources에 테이블이 없을 때 fallback)
        /// 전설 등급 장비 3종 + 희귀 재료 2종 + 전설 레시피 1종
        /// </summary>
        private void ApplyDraculaLordDropsFallback(ILootBasket basket)
        {
            // 전설 장비 3종 (100% 드랍)
            var legendSword = new PlayerInventory.ItemData
            {
                id = "dracula_legend_sword", displayName = "드라큘라의 검 (전설)",
                description = "밤의 힘이 깃든 전설의 검",
                category = PlayerInventory.ItemCategory.Weapon, maxStack = 1
            };
            basket.AddItem(legendSword, 1);

            var legendArmor = new PlayerInventory.ItemData
            {
                id = "dracula_legend_armor", displayName = "드라큘라의 갑옷 (전설)",
                description = "흡혈귀의 힘을 가진 전설의 갑옷",
                category = PlayerInventory.ItemCategory.Armor, maxStack = 1
            };
            basket.AddItem(legendArmor, 1);

            var legendRing = new PlayerInventory.ItemData
            {
                id = "dracula_legend_ring", displayName = "흡혈귀의 반지 (전설)",
                description = "적의 피를 흡수하는 전설의 반지",
                category = PlayerInventory.ItemCategory.Material, maxStack = 1
            };
            basket.AddItem(legendRing, 1);

            // 희귀 재료 2종 (100% 드랍)
            var rareBlood = new PlayerInventory.ItemData
            {
                id = "dracula_rare_blood", displayName = "드라큘라의 피 (희귀)",
                description = "순수한 흡혈귀의 피, 강력한 연금술 재료",
                category = PlayerInventory.ItemCategory.Material, maxStack = 99
            };
            basket.AddItem(rareBlood, Random.Range(2, 5));

            var rareCrystal = new PlayerInventory.ItemData
            {
                id = "dracula_rare_crystal", displayName = "야광 수정 (희귀)",
                description = "밤에 빛나는 신비한 수정",
                category = PlayerInventory.ItemCategory.Material, maxStack = 99
            };
            basket.AddItem(rareCrystal, Random.Range(1, 3));

            // 전설 레시피 1종 (100% 드랍)
            var legendRecipe = new PlayerInventory.ItemData
            {
                id = "dracula_legend_recipe", displayName = "흡혈귀의 비전 레시피 (전설)",
                description = "전설의 아이템을 제작하는 비법",
                category = PlayerInventory.ItemCategory.Quest, maxStack = 1
            };
            basket.AddItem(legendRecipe, 1);

            Debug.Log("[DropTableManager] 🧛 드라큘라 영주 드랍 완료! (fallback)");
        }

        /// <summary>
        /// 스켈레톤 병사 기본 드랍 (Resources에 테이블이 없을 때 fallback)
        /// 중급 재료 2종 + 희귀 재료 1종 (10% 확률)
        /// </summary>
        private void ApplySkeletonGuardDropsFallback(ILootBasket basket, int level)
        {
            // 중급 재료 1: 뼈 조각 (90% 확률)
            if (Random.value < 0.9f)
            {
                var boneShard = new PlayerInventory.ItemData
                {
                    id = "skeleton_bone_shard", displayName = "뼈 조각",
                    description = "스켈레톤에서 떨어진 뼈 조각",
                    category = PlayerInventory.ItemCategory.Material, maxStack = 99
                };
                basket.AddItem(boneShard, Random.Range(1, 3));
            }

            // 중급 재료 2: 녹슨 검 (60% 확률)
            if (Random.value < 0.6f)
            {
                var rustySword = new PlayerInventory.ItemData
                {
                    id = "skeleton_rusty_sword", displayName = "녹슨 검",
                    description = "오래된 스켈레톤 병사의 검",
                    category = PlayerInventory.ItemCategory.Material, maxStack = 99
                };
                basket.AddItem(rustySword, 1);
            }

            // 희귀 재료: 영혼의 잔재 (10% 확률, 레벨 보정)
            float rareChance = 0.1f + (level - 30) * 0.005f;
            rareChance = Mathf.Clamp(rareChance, 0.05f, 0.3f);
            if (Random.value < rareChance)
            {
                var soulRemnant = new PlayerInventory.ItemData
                {
                    id = "skeleton_soul_remnant", displayName = "영혼의 잔재 (희귀)",
                    description = "스켈레톤에 남아있는 영혼의 파편",
                    category = PlayerInventory.ItemCategory.Material, maxStack = 99
                };
                basket.AddItem(soulRemnant, 1);
            }

            Debug.Log($"[DropTableManager] 💀 스켈레톤 병사 드랍 완료! (fallback, level={level})");
        }
    }
}