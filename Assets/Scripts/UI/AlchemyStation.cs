using UnityEngine;
using ProjectName.Systems;
using ProjectName.Core;
#pragma warning disable 0414

namespace ProjectName.UI
{
    /// <summary>
    /// 연금술 스테이션 — E 키로 연금술 UI 열기.
    /// 전 세계에 배치하여 연금술 제조를 가능하게 함.
    /// </summary>
    public class AlchemyStation : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private float _interactRange = 3f;
        [SerializeField] private string _stationName = "연금술 테이블";

        private Transform _player;
        private bool _isPlayerNearby = false;

        private void Start()
        {
            // Add visual placeholder if no model assigned
            if (GetComponent<MeshRenderer>() == null && GetComponentInChildren<MeshRenderer>() == null)
            {
                string modelKey = "craft_blend";
                // Try GLB model
                if (!RuntimeModelLoader.TryGetModel(modelKey, out var stationModel))
                {
                    // Fallback: create a visible placeholder
                    var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    visual.transform.SetParent(transform);
                    visual.transform.localPosition = Vector3.zero;
                    visual.transform.localScale = new Vector3(1f, 1f, 1f);
                    // CreatePrimitive creates BoxCollider — remove to avoid unintended physics
                    DestroyImmediate(visual.GetComponent<Collider>());
                    var renderer = visual.GetComponent<Renderer>();
                    renderer.sharedMaterial.color = new Color(0.5f, 0.3f, 0.1f); // Brown
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
            // Retry player lookup if not found yet (e.g. player spawned after this station)
            if (_player == null)
            {
                _player = GameObject.FindGameObjectWithTag("Player" )?.transform;
                if (_player == null) return;
            }

            float distSq = (transform.position - _player.position).sqrMagnitude;
            _isPlayerNearby = distSq <= _interactRange * _interactRange;

            if (_isPlayerNearby && Input.GetKeyDown(KeyCode.E))
            {
                OpenAlchemyUI();
            }
        }

        private void OpenAlchemyUI()
        {
            Debug.Log($"[AlchemyStation] {_stationName} 열림");
            if (UIManager.Instance != null)
            {
                UIManager.Instance.OpenWindow(typeof(AlchemyUI));
            }
            else
            {
                Debug.LogWarning("[AlchemyStation] UIManager가 없습니다.");
            }
        }

        // ── OnGUI 상호작용 프롬프트 ──
        private void OnGUI()
        {
            if (!_isPlayerNearby) return;
            if (_player == null) return;

            float labelWidth = 200;
            float labelHeight = 30;
            float x = (Screen.width - labelWidth) / 2f;
            float y = Screen.height - 80;

            GUI.Box(new Rect(x, y, labelWidth, labelHeight), "E - 연금술");
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.4f, 0.8f); // 보라색 계열로 연금술 스테이션 표시
            Gizmos.DrawWireSphere(transform.position, _interactRange);
        }
    }
}