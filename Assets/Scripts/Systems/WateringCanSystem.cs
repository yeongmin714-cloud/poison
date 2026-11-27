using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [26차 농경시스템 업그레이드 — docs/FARM_UPGRADE_PLAN.md §Phase 4] 물뿌리개 물 저장량.
    /// 내구도 개념 없음 — 물 저장량 0~20회만. 1회 물주기 = 1 소비.
    /// 충전은 호수 물가 상호작용(물담기 — FishingSystem 연동)으로만: 1회 충전 = 20/20.
    /// 플레이어 소지 검사는 PlayerInventory.HasItem("tool_watering_can").
    /// </summary>
    public static class WateringCanSystem
    {
        public const string CanItemId = "tool_watering_can";
        public const int MaxWater = 20;      // 저장량 상한 20회 (사용자 확정)
        public const float RefillChannelSeconds = 2f;   // 물담기 채널 2초

        private static float _water = MaxWater;   // 세션 시작 만水量(도구 구매 직후 가득 — 상점 구매 플로우에서 재확인)

        /// <summary>현재 물 저장량(회).</summary>
        public static int Water => Mathf.FloorToInt(_water);

        public static int Max => MaxWater;

        public static bool HasCan()
        {
            var inv = ProjectName.Core.PlayerInventory.Instance;
            return inv != null && inv.HasItem(CanItemId);
        }

        /// <summary>물이 1회분 이상 남았는가.</summary>
        public static bool HasWater(int count = 1) => _water >= count;

        /// <summary>물 1회분(또는 count) 소비. 부족하면 false — 소비 없음.</summary>
        public static bool Consume(int count = 1)
        {
            if (_water < count) return false;
            _water -= count;
            return true;
        }

        /// <summary>호수 물담기 — 저장량을 20/20으로 충전. 충전량 반환.</summary>
        public static int Refill()
        {
            int added = MaxWater - Mathf.FloorToInt(_water);
            _water = MaxWater;
            Debug.Log($"[WateringCanSystem] 💧 물담기 완료 — 물뿌리개 {Max}/{Max} (+{added})");
            return added;
        }

        /// <summary>테스트/씬 초기화용 리셋.</summary>
        public static void Reset(float water = MaxWater) => _water = Mathf.Clamp(water, 0f, MaxWater);
    }
}
