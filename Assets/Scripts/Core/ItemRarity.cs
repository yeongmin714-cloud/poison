namespace ProjectName.Core
{
    /// <summary>
    /// 아이템 등급 열거형 — Core 어셈블리에서 참조 가능.
    /// 명시적 정수 값 할당으로 enum 멤버 재정렬 시 깨짐 방지.
    /// EquipmentRarityData._rarityData 배열 인덱스와 동기화 필수.
    /// </summary>
    public enum ItemRarity
    {
        Common    = 0,    // 일반
        Uncommon  = 1,    // 고급
        Rare      = 2,    // 희귀
        Epic      = 3,    // 영웅
        Legendary = 4,    // 전설
        Unique    = 5,    // 유니크
    }
}
