using System.Collections.Generic;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// [Milestone C] 비밀상점 — 은밀한 상인. 조건(비밀 통행증 소모, ConsumeSecretPass)으로 등장,
    /// 제작 불가(NonCraftableCatalog) 최상위 무기/방어구를 골드로 판매. UI 렌더는 Milestone D(상점 창).
    /// </summary>
    public static class SecretShopSystem
    {
        public struct SecretItem
        {
            public string itemId;
            public int price;
            public SecretItem(string id, int p) { itemId = id; price = p; }
        }

        /// <summary>비밀상점 입장권 아이템 ID — 소모 시 Reveal().</summary>
        public const string SecretPassItemId = "item_secret_pass";

        public static bool Active { get; private set; }

        private static readonly List<SecretItem> _stock = new List<SecretItem>
        {
            new SecretItem("weapon_unique_abyss", 5000),
            new SecretItem("weapon_unique_dawnblade", 8000),
            new SecretItem("armor_unique_voidplate", 12000),
            new SecretItem("weapon_legendary", 20000),
        };
        private static readonly List<SecretItem> _empty = new List<SecretItem>();

        /// <summary>활성 시 재고(비활성 빈 목록).</summary>
        public static IReadOnlyList<SecretItem> Stock => Active ? _stock : _empty;

        /// <summary>비밀 통행증 소모로 비밀상점 등장 (없으면 false).</summary>
        public static bool ConsumeSecretPass()
        {
            var inv = PlayerInventory.Instance;
            if (inv == null || inv.GetItemCount(SecretPassItemId) <= 0) return false;
            inv.RemoveItem(SecretPassItemId, 1);
            Active = true;
            Debug2();
            return true;
        }

        public static void Reveal() => Active = true;
        public static void Hide() => Active = false;

        /// <summary>구매 시도 — Active + 골드 충분 + 재고 → 골드 차감(PlayerStats.SpendGold) 후 아이템 지급.</summary>
        public static bool TryBuy(string itemId)
        {
            if (!Active) return false;
            if (PlayerInventory.Instance == null) return false;

            SecretItem? target = null;
            foreach (var s in _stock)
            {
                if (s.itemId == itemId) { target = s; break; }
            }
            if (target == null) return false;

            var stats = PlayerStats.Instance;
            int price = target.Value.price;
            if (stats == null || stats.Gold < price) return false;
            if (!stats.SpendGold(price, "secret_shop")) return false;

            var item = PlayerInventory.GetItemById(itemId);
            if (item == null)
            {
                stats.AddGold(price, "secret_shop_refund"); // 롤백
                return false;
            }
            if (!PlayerInventory.Instance.AddItem(item, 1))
            {
                stats.AddGold(price, "secret_shop_refund");
                return false;
            }
            UnityEngine.Debug.Log($"[SecretShop] {item.displayName} 구매 완료 ({price}G)");
            return true;
        }

        private static void Debug2()
        {
            UnityEngine.Debug.Log("[SecretShop] 🔮 비밀 통행증 사용 — 은밀한 상인이 나타났습니다!");
        }
    }
}
