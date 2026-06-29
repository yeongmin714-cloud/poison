using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Data
{
    /// <summary>
    /// Phase 35: 자물쇠 따기 도구 아이템 데이터.
    /// LockpickItem 등급별 내구도와 사용 가능 난이도 정의.
    /// 크래프트 테이블에서 철 + 희귀 재료로 제작 가능.
    /// </summary>
    [CreateAssetMenu(fileName = "NewLockpickItem", menuName = "Items/LockpickItem")]
    public class LockpickItem : ScriptableObject
    {
        [Header("아이템 기본 정보")]
        public string itemId;
        public string displayName;
        public string description;
        public Sprite icon;

        [Header("픽 도구 등급")]
        public LockpickingSystem.PickGrade pickGrade = LockpickingSystem.PickGrade.Basic;

        [Header("내구도")]
        public int maxDurability = 5;

        [Header("사용 가능 난이도")]
        public LockpickingSystem.LockDifficulty maxLockDifficulty = LockpickingSystem.LockDifficulty.Medium;

        /// <summary>
        /// PlayerInventory ItemData로 변환.
        /// </summary>
        public PlayerInventory.ItemData ToItemData()
        {
            var data = new PlayerInventory.ItemData
            {
                id = itemId,
                displayName = displayName,
                description = description,
                category = PlayerInventory.ItemCategory.Tool,
                icon = icon,
                maxStack = 1,
                maxDurability = maxDurability
            };
            return data;
        }

        // ===== 정적 아이템 데이터 =====

        /// <summary>
        /// 기본 픽 (내구도 5, 쉬움/보통).
        /// </summary>
        public static readonly PlayerInventory.ItemData BasicLockpick = new PlayerInventory.ItemData
        {
            id = "lockpick_basic",
            displayName = "기본 자물쇠 따개",
            description = "기본 철제 픽. 쉬움~보통 난이도 자물쇠 해제 가능. 내구도 5.",
            category = PlayerInventory.ItemCategory.Tool,
            maxStack = 5,
            maxDurability = 5
        };

        /// <summary>
        /// 고급 픽 (내구도 10, 어려움까지).
        /// </summary>
        public static readonly PlayerInventory.ItemData AdvancedLockpick = new PlayerInventory.ItemData
        {
            id = "lockpick_advanced",
            displayName = "고급 자물쇠 따개",
            description = "강화 강철 픽. 쉬움~어려움 난이도 자물쇠 해제 가능. 내구도 10.",
            category = PlayerInventory.ItemCategory.Tool,
            maxStack = 5,
            maxDurability = 10
        };

        /// <summary>
        /// 마스터 픽 (내구도 20, 전설까지).
        /// </summary>
        public static readonly PlayerInventory.ItemData MasterLockpick = new PlayerInventory.ItemData
        {
            id = "lockpick_master",
            displayName = "마스터 자물쇠 따개",
            description = "전설적인 도구. 모든 난이도 자물쇠 해제 가능. 내구도 20.",
            category = PlayerInventory.ItemCategory.Tool,
            maxStack = 1,
            maxDurability = 20
        };

        /// <summary>
        /// 마스터 키 (모든 문 즉시 오픈, 소모품).
        /// </summary>
        public static readonly PlayerInventory.ItemData MasterKey = new PlayerInventory.ItemData
        {
            id = "lockpick_master_key",
            displayName = "마스터 키",
            description = "모든 자물쇠를 즉시 열 수 있는 마스터 키. 1회용.",
            category = PlayerInventory.ItemCategory.Tool,
            maxStack = 3,
            maxDurability = 1
        };

        /// <summary>
        /// LockpickGrade → PlayerInventory.ItemData 매핑.
        /// </summary>
        public static PlayerInventory.ItemData GetLockpickData(LockpickingSystem.PickGrade grade)
        {
            switch (grade)
            {
                case LockpickingSystem.PickGrade.Basic:    return BasicLockpick;
                case LockpickingSystem.PickGrade.Advanced: return AdvancedLockpick;
                case LockpickingSystem.PickGrade.Master:   return MasterLockpick;
                default:                                   return BasicLockpick;
            }
        }

        /// <summary>
        /// PlayerInventory에 픽 아이템이 있는지 확인하고, 있다면 가장 높은 등급 반환.
        /// </summary>
        public static LockpickingSystem.PickGrade? GetHighestAvailablePick()
        {
            if (PlayerInventory.Instance == null) return null;

            if (PlayerInventory.Instance.HasItem("lockpick_master"))
                return LockpickingSystem.PickGrade.Master;
            if (PlayerInventory.Instance.HasItem("lockpick_advanced"))
                return LockpickingSystem.PickGrade.Advanced;
            if (PlayerInventory.Instance.HasItem("lockpick_basic"))
                return LockpickingSystem.PickGrade.Basic;

            return null;
        }

        /// <summary>
        /// 마스터 키 보유 여부 확인.
        /// </summary>
        public static bool HasMasterKey()
        {
            return PlayerInventory.Instance != null && PlayerInventory.Instance.HasItem("lockpick_master_key");
        }
    }
}
