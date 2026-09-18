using ProjectName.Systems;

namespace ProjectName.Systems
{
    /// <summary>
    /// [U8 배선] 병사 상호작용 F키 브리지 — Systems(PlayerMovement)에서 발화, UI(UTK)가 구독.
    /// Systems→UI 순환참조 회피용 이벤트 패턴 (LootBasket.OnOpenLootWindowRequestedUTK 동일).
    /// </summary>
    public static class SoldierInteractBridge
    {
        public static event System.Action<GuardPlaceholder> OnSoldierInfoRequested;

        public static void Raise(GuardPlaceholder guard)
        {
            if (guard != null)
                OnSoldierInfoRequested?.Invoke(guard);
        }
    }
}
