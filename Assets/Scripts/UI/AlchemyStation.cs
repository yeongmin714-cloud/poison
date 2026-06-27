using UnityEngine;
using ProjectName.Core.UI;

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
        private bool _isOpen = false;
        private GameObject _alchemyUIGameObject;

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
                _player = GameObject.FindGameObjectWithTag("Player")?.transform;
                if (_player == null) return;
            }

            float distSq = (transform.position - _player.position).sqrMagnitude;
            bool nearby = distSq <= _interactRange * _interactRange;

            if (nearby && Input.GetKeyDown(KeyCode.E) && !_isOpen)
                OpenAlchemyUI();
            else if (_isOpen && Input.GetKeyDown(KeyCode.Escape))
                CloseAlchemyUI();
        }

        private void OpenAlchemyUI()
        {
            _isOpen = true;
            Debug.Log($"[AlchemyStation] {_stationName} 열림");

            // AlchemyUI를 가진 GameObject 생성
            _alchemyUIGameObject = new GameObject("AlchemyUI");
            _alchemyUIGameObject.AddComponent<AlchemyUI>();
            Debug.Log("[AlchemyStation] AlchemyUI 인스턴스 생성 및 UI 생성됨");
        }

        private void CloseAlchemyUI()
        {
            _isOpen = false;
            Debug.Log($"[AlchemyStation] {_stationName} 닫힘");

            // AlchemyUI GameObject 파괴 (메모리 정리)
            if (_alchemyUIGameObject != null)
            {
                Destroy(_alchemyUIGameObject);
                _alchemyUIGameObject = null;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.4f, 0.8f); // 보라색 계열로 연금술 스테이션 표시
            Gizmos.DrawWireSphere(transform.position, _interactRange);
        }

        private void OnDestroy()
        {
            // 스테이션이 파괴될 때 열려있는 UI도 정리
            if (_isOpen && _alchemyUIGameObject != null)
            {
                Destroy(_alchemyUIGameObject);
            }
        }
    }
}