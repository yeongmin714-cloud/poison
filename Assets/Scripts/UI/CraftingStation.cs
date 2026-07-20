using UnityEngine;
using ProjectName.Systems;
using ProjectName.UI.Core;

namespace ProjectName.UI
{
    /// <summary>
    /// 크래프트 스테이션 — E 키로 크래프트 테이블 UI 열기, R 키로 수리 스테이션 열기.
    /// 튜토리얼 집 안에 배치.
    /// </summary>
    public class CraftingStation : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private float _interactRange = 3f;
        [SerializeField] private string _stationName = "크래프트 테이블";

        private Transform _player;
        private bool _isPlayerNearby = false;

        private void Start()
        {
            // Add visual placeholder if no model assigned
            if (GetComponent<MeshRenderer>() == null && GetComponentInChildren<MeshRenderer>() == null)
            {
                string modelKey = "craft_equip";
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
            if (Time.timeScale == 0) return;  // 메뉴/일시정지 시 상호작용 방지

            float distSqr = (_player.position - transform.position).sqrMagnitude;
            _isPlayerNearby = distSqr <= _interactRange * _interactRange;

            // E 키로 크래프트 UI 열기
            if (_isPlayerNearby && Input.GetKeyDown(KeyCode.E))
            {
                OpenCrafting();
            }

            // R 키로 수리 스테이션 UI 열기
            if (_isPlayerNearby && Input.GetKeyDown(KeyCode.R))
            {
                OpenRepairStation();
            }
        }

        private void OpenCrafting()
        {
            Debug.Log($"[CraftingStation] {_stationName} 열림");

            if (UIManager.Instance != null)
            {
                // UIManager.OpenWindow를 통해 윈도우 스택 관리와 함께 열기
                UIManager.Instance.OpenWindow(typeof(CraftingUI));
            }
            else
            {
                Debug.LogWarning("[CraftingStation] UIManager가 존재하지 않습니다.");
            }
        }

        private void OpenRepairStation()
        {
            Debug.Log($"[CraftingStation] {_stationName} 수리 스테이션 열림");

            if (UIManager.Instance != null)
            {
                // 크래프트 UI가 열려있으면 먼저 닫기
                if (UIManager.craftingWindow != null && UIManager.craftingWindow.IsOpen)
                    UIManager.craftingWindow.Hide();

                // UIManager.OpenWindow를 통해 윈도우 스택 관리와 함께 열기
                UIManager.Instance.OpenWindow(typeof(RepairStationUI));
            }
            else
            {
                Debug.LogWarning("[CraftingStation] UIManager가 존재하지 않습니다.");
            }
        }

        // ── OnGUI 상호작용 프롬프트 ──
        private void OnGUI()
        {
            if (!_isPlayerNearby) return;
            if (_player == null) return;

            // 화면 중앙 하단에 프롬프트 표시
            float labelWidth = 320;
            float labelHeight = 40;
            float x = (Screen.width - labelWidth) / 2f;
            float y = Screen.height - 90;

            GUI.Box(new Rect(x, y, labelWidth, labelHeight), "E - 크래프트 테이블 사용\nR - 장비 수리");
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.8f, 0.6f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, _interactRange);
        }
    }
}