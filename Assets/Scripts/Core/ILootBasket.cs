using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Core
{
    // ================================================================
    // LootEntry — 전리품 바구니의 각 아이템 슬롯
    // Core에 위치하여 Systems와 UI가 모두 참조 가능
    // ================================================================

    /// <summary>
    /// 전리품 바구니의 단일 아이템 항목. item + count 쌍.
    /// ProjectName.Core에 위치하여 Systems와 UI 어셈블리 모두에서 접근 가능.
    /// </summary>
    [System.Serializable]
    public class LootEntry
    {
        [field: SerializeField]
        public PlayerInventory.ItemData Item { get; set; }
        [field: SerializeField]
        public int Count { get; set; }
    }

    // ================================================================
    // ILootBasket — LootBasket의 인터페이스 (UI 의존성 제거용)
    // ================================================================

    /// <summary>
    /// LootBasket의 핵심 API를 정의하는 인터페이스.
    /// ProjectName.UI 어셈블리가 ProjectName.Systems에 의존하지 않고도
    /// LootBasket과 상호작용할 수 있게 합니다.
    /// </summary>
    public interface ILootBasket
    {
        /// <summary>읽기 전용 아이템 목록</summary>
        IReadOnlyList<LootEntry> Items { get; }

        /// <summary>아직 획득 가능한 상태인가?</summary>
        bool IsAvailable { get; }

        /// <summary>바구니가 비었는가?</summary>
        bool IsEmpty { get; }

        /// <summary>유효한 아이템 종류 개수</summary>
        int ItemCount { get; }

        /// <summary>내용물 기반 자동 생성된 바구니 이름</summary>
        string BasketName { get; }

        /// <summary>지정된 인덱스의 아이템 획득</summary>
        bool TakeItem(int index);

        /// <summary>모든 아이템 획득 후 바구니 소멸</summary>
        bool TakeAll();

        /// <summary>
        /// 바구니에 아이템을 추가합니다. 같은 ID의 아이템은 자동 스택 처리됩니다.
        /// </summary>
        void AddItem(PlayerInventory.ItemData item, int count = 1);
    }
}