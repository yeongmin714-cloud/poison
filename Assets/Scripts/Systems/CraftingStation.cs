using UnityEngine;
using ProjectName.Core;
using ProjectName.UI;

namespace ProjectName.Systems
{
    /// <summary>
    /// 크래프트 스테이션 — E 키로 크래프트 테이블 UI 열기.
    /// 튜토리얼 집 안에 배치.
    /// </summary>
    public class CraftingStation : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private float _interactRange = 3f;
        [SerializeField] private string _stationName = "크래프트 테이블";

        // For testing: hardcoded recipe (or load from RecipeDatabase)
        [Header("테스트 레시피 (하드코딩)")]
        [SerializeField] private Recipe testRecipe;

        private Transform _player;
        private bool _isPlayerNearby = false;

        private void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        private void Update()
        {
            if (_player == null) return;

            float dist = Vector3.Distance(transform.position, _player.position);
            _isPlayerNearby = dist <= _interactRange;

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

            if (UIManager.Instance != null && UIManager.Instance.craftingWindow != null)
            {
                UIManager.Instance.craftingWindow.Open();
            }
            else
            {
                Debug.LogWarning("[CraftingStation] CraftingUI가 UIManager에 설정되지 않았습니다.");
            }
        }

        private void OpenRepairStation()
        {
            Debug.Log($"[CraftingStation] {_stationName} 수리 스테이션 열림");

            if (UIManager.Instance != null && UIManager.Instance.repairWindow != null)
            {
                // 크래프트 UI가 열려있으면 닫고 수리 UI 열기
                if (UIManager.Instance.craftingWindow != null && UIManager.Instance.craftingWindow.IsOpen)
                    UIManager.Instance.craftingWindow.Hide();

                UIManager.Instance.repairWindow.Open();
            }
            else
            {
                Debug.LogWarning("[CraftingStation] RepairStationUI가 UIManager에 설정되지 않았습니다.");
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