using ProjectName.Systems;

namespace ProjectName.Systems
{
    /// <summary>
    /// [U8 배선] 병사 상호작용 F키 브리지 — Systems(PlayerMovement)에서 발화, UI(UTK)가 구독.
    /// Systems→UI 순환참조 회피용 이벤트 패턴 (LootBasket.OnOpenLootWindowRequestedUTK 동일).
    ///
    /// [P13 분리]
    ///   RaiseInteract  → SoldierInteractUTK (통합 상호작용 창 — 말걸기/지급/포섭/정보/닫기)
    ///   Raise          → GuardInfoUTK      (병사 정보창 — 상호작용 창 내 "병사 정보보기" 버튼 경로 유지)
    ///
    /// [P28 추가] 몬스터 관찰
    ///   RaiseMonsterInfo → MonsterInfoUTK (몬스터 정보창 — 이름/체력/스탯/레벨)
    ///   Systems 어셈블리(ContextCommandRouter)에서 UI 계층을 직접 호출하지 않고
    ///   이 이벤트 브리지로 발화(순환참조 회피) — SoldierInteract/RaiseInteract와 동일 패턴.
    /// </summary>
    public static class SoldierInteractBridge
    {
        /// <summary>병사 정보창 요청 — GuardInfoUTK 구독 (기존 경로 유지).</summary>
        public static event System.Action<GuardPlaceholder> OnSoldierInfoRequested;

        /// <summary>[P13] 병사 상호작용 창 요청 — SoldierInteractUTK 구독.</summary>
        public static event System.Action<GuardPlaceholder> OnInteractRequested;

        /// <summary>[P28] 몬스터 정보창 요청 — MonsterInfoUTK 구독.</summary>
        public static event System.Action<AnimalAI> OnMonsterInfoRequested;

        /// <summary>병사 정보창 열기 발화 (GuardInfoUTK 경유).</summary>
        public static void Raise(GuardPlaceholder guard)
        {
            if (guard != null)
                OnSoldierInfoRequested?.Invoke(guard);
        }

        /// <summary>[P13] 병사 상호작용 창 열기 발화 (SoldierInteractUTK 경유).</summary>
        public static void RaiseInteract(GuardPlaceholder guard)
        {
            if (guard != null)
                OnInteractRequested?.Invoke(guard);
        }

        /// <summary>[P28] 몬스터 정보창 열기 발화 (MonsterInfoUTK 경유).</summary>
        public static void RaiseMonsterInfo(AnimalAI monster)
        {
            if (monster != null)
                OnMonsterInfoRequested?.Invoke(monster);
        }
    }
}
