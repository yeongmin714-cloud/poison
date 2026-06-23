using UnityEngine;
using ProjectName.Core;
using ProjectName.UI;

namespace ProjectName.Systems
{
    /// <summary>
    /// 요리 스테이션 — E 키로 요리 UI 열기.
    /// </summary>
    public class CookingStation : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private float _interactRange = 3f;
        [SerializeField] private string _stationName = "요리 테이블";

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