using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C16-02: 침대 상호작용 컴포넌트.
    /// <para>플레이어가 E 키로 상호작용하면 SleepUI를 표시합니다.</para>
    /// <para>
    /// 근접 감지는 BoxCollider(isTrigger)를 통해 이루어집니다.
    /// PlayerMovement.HandleInteraction()이 Physics.OverlapSphere로
    /// 이 트리거 콜라이더를 감지하고 OnInteract()를 호출합니다.
    /// Bed 자체에는 트리거 이벤트 핸들러가 없으며, 감지는 전적으로 PlayerMovement에서 수행합니다.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class Bed : MonoBehaviour
    {
        [Header("Bed Settings")]
        [SerializeField] private string _bedName = "침대";

        [Header("Interaction Range (Editor Gizmo Only)")]
        [SerializeField] private float _interactionRange = 2f;

        private BoxCollider _collider;

        /// <summary>
        /// 침대 이름 (UI 표시용)
        /// </summary>
        public string BedName => _bedName;

        /// <summary>
        /// 에디터 Gizmos 시각화용 상호작용 범위.
        /// 실제 근접 감지 범위는 PlayerMovement._interactionRadius에서 관리합니다.
        /// </summary>
        public float InteractionRange => _interactionRange;

        private void Awake()
        {
            _collider = GetComponent<BoxCollider>();
            if (_collider == null)
            {
                Debug.LogError("[Bed] BoxCollider 컴포넌트를 찾을 수 없습니다!", this);
                return;
            }
            _collider.isTrigger = true;
        }

        /// <summary>
        /// 플레이어가 이 침대와 상호작용할 때 호출됩니다.
        /// SleepUI.Instance.Show()를 통해 수면 옵션 UI를 띄웁니다.
        /// </summary>
        public void OnInteract()
        {
            if (SleepUI.Instance != null)
            {
                SleepUI.Instance.Show(this);
            }
            else
            {
                Debug.LogWarning("[Bed] SleepUI.Instance가 없습니다!");
            }
        }

        /// <summary>
        /// Gizmos로 상호작용 범위를 시각화 (에디터 전용)
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, _interactionRange);
        }
    }
}