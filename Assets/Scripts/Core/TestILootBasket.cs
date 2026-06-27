using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Core
{
    /// <summary>
    /// ILootBasket 인터페이스 테스트/디버그 컴포넌트.
    /// 
    /// Inspector 또는 코드에서 ILootBasket 참조를 할당하여
    /// 드랍 테이블 적용, 바구니 상태 확인 등 인터페이스 동작을 테스트합니다.
    /// 
    /// 사용법:
    ///   1. 씬의 빈 GameObject에 이 컴포넌트를 추가합니다.
    ///   2. 코드에서 Basket 속성에 ILootBasket 인스턴스를 할당합니다.
    ///   3. ContextMenu ("드랍 테이블 적용") 또는 ("바구니 상태 로그")로 테스트.
    /// </summary>
    public class TestILootBasket : MonoBehaviour
    {
        [Header("테스트 설정")]
        [SerializeField] private DropTable _dropTable;

        /// <summary>테스트 대상이 되는 ILootBasket 참조</summary>
        public ILootBasket Basket { get; set; }

        /// <summary>
        /// 할당된 드랍 테이블을 바구니에 적용합니다.
        /// Editor ContextMenu ("드랍 테이블 적용")로 호출 가능.
        /// </summary>
        [ContextMenu("드랍 테이블 적용")]
        public void ApplyDropTable()
        {
            if (Basket == null)
            {
                Debug.LogWarning("[TestILootBasket] Basket이 설정되지 않았습니다. 코드에서 Basket 속성에 ILootBasket 인스턴스를 할당해주세요.");
                return;
            }
            if (_dropTable == null)
            {
                Debug.LogWarning("[TestILootBasket] 드랍 테이블이 설정되지 않았습니다.");
                return;
            }

            _dropTable.ApplyToBasket(Basket);
            Debug.Log($"[TestILootBasket] ✅ 드랍 테이블 적용 완료: {_dropTable.TableName}");
            LogBasketState();
        }

        /// <summary>
        /// 현재 바구니 상태를 로그로 출력합니다.
        /// Editor ContextMenu ("바구니 상태 로그")로 호출 가능.
        /// </summary>
        [ContextMenu("바구니 상태 로그")]
        public void LogBasketState()
        {
            if (Basket == null)
            {
                Debug.Log("[TestILootBasket] 바구니: null (할당되지 않음)");
                return;
            }

            Debug.Log($"[TestILootBasket] 바구니: \"{Basket.BasketName}\" | 항목: {Basket.ItemCount}개 | 사용 가능: {Basket.IsAvailable} | 비어있음: {Basket.IsEmpty}");
        }

        private void Awake()
        {
            if (Basket == null)
            {
                Debug.Log("[TestILootBasket] Awake: Basket이 할당되지 않았습니다. Setter를 통해 ILootBasket 인스턴스를 주입해주세요.");
            }
        }
    }
}