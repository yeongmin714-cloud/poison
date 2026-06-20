using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C16-02: 침대 상호작용 컴포넌트.
    /// 플레이어가 E 키로 상호작용하면 SleepUI를 표시합니다.
    /// BoxCollider 트리거를 통해 근접 감지를 지원합니다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class Bed : MonoBehaviour
    {
        [Header("Bed Settings")]
        [SerializeField] private string _bedName = "침대";

        [Header("Interaction Range")]
        [SerializeField] private float _interactionRange = 2f;

        private BoxCollider _collider;

        /// <summary>
        /// 침대 이름 (UI 표시용)
        /// </summary>
        public string BedName => _bedName;

        /// <summary>
        /// 상호작용 가능 거리
        /// </summary>
        public float InteractionRange => _interactionRange;

        private void Awake()
        {
            _collider = GetComponent<BoxCollider>();
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