using UnityEngine;
using ProjectName.UI;
using ProjectName.Systems;

namespace ProjectName.UI
{
    /// <summary>
    /// 요리 스테이션 — E 키로 요리 UI 열기.
    /// </summary>
    public class CookingStation : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private float _interactRange = 3f;
        [SerializeField] private string _stationName = "요리 테이블";
        [SerializeField] private string _modelKey = "craft_cook";

        private Transform _player;
        private bool _isPlayerNearby = false;

        private void Start()
        {
            // Add visual placeholder if no model assigned
            if (GetComponent<MeshRenderer>() == null && GetComponentInChildren<MeshRenderer>() == null)
            {
                string modelKey = _modelKey;
                // Try GLB model
                if (!RuntimeModelLoader.TryGetModel(modelKey, out var stationModel))
                {
                    // Fallback: create a visible placeholder
                    var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    visual.transform.SetParent(transform);
                    visual.transform.localPosition = Vector3.zero;
                    visual.transform.localScale = new Vector3(1f, 1f, 1f);
                    var renderer = visual.GetComponent<Renderer>();
                    renderer.material.color = new Color(0.5f, 0.3f, 0.1f); // Brown
                }
                else
                {
                    var instance = Instantiate(stationModel, transform);
                    instance.transform.localPosition = Vector3.zero;
                }
            }

            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        private void Update()
        {
            if (_player == null) return;

            float dist = (transform.position - _player.position).sqrMagnitude;
            _isPlayerNearby = dist <= _interactRange * _interactRange;

            // E 키로 요리 UI 열기
            if (_isPlayerNearby && Input.GetKeyDown(KeyCode.E))
            {
                OpenCooking();
            }
        }

        private void OpenCooking()
        {
            Debug.Log($"[CookingStation] {_stationName} 열림");

            if (UIManager.Instance != null)
            {
                UIManager.Instance.OpenWindow(typeof(CookingUI));
            }
            else
            {
                Debug.LogWarning("[CookingStation] UIManager가 없습니다.");
            }
        }

        // ── OnGUI 상호작용 프롬프트 ──
        private void OnGUI()
        {
            if (!_isPlayerNearby) return;
            if (_player == null) return;

            // 화면 중앙 하단에 프롬프트 표시
            float labelWidth = 200;
            float labelHeight = 30;
            float x = (Screen.width - labelWidth) / 2f;
            float y = Screen.height - 80;

            GUI.Box(new Rect(x, y, labelWidth, labelHeight), "E - 요리하기");
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, _interactRange);
        }
    }
}