using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
#pragma warning disable 0414

namespace ProjectName.UI
{
    /// <summary>
    /// Phase 5.5: 성당 UI — 씬 상호작용 브릿지.
    /// ChurchSystem(데이터/로직)과 ChurchUI(표시)를 연결.
    /// 씬의 ChurchSystem 오브젝트에 부착.
    /// </summary>
    public class ChurchSystemUI : MonoBehaviour
    {
        [Header("성당 설정")]
        [SerializeField] private string _territoryId = "default";
        [SerializeField] private float _interactRange = 3f;

        private Transform _player;
        private bool _isPlayerNearby;

        // OnGUI GC 방지: 캐시
        private bool _guiDirty = true;
        private string _cachedGuiLabel = "";
        private Rect _guiLabelRect;

        public string TerritoryId => _territoryId;

        private void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        private void Update()
        {
            if (_player == null)
            {
                _player = GameObject.FindGameObjectWithTag("Player")?.transform;
                if (_player == null) return;
            }

            float sqrDist = (transform.position - _player.position).sqrMagnitude;
            float rangeSqr = _interactRange * _interactRange;
            bool wasNearby = _isPlayerNearby;
            _isPlayerNearby = sqrDist <= rangeSqr;

            if (_isPlayerNearby != wasNearby)
                _guiDirty = true;

            if (_isPlayerNearby && Input.GetKeyDown(KeyCode.E))
            {
                OpenChurchUI();
            }
        }

        private void OpenChurchUI()
        {
            if (ChurchSystem.Instance == null)
            {
                Debug.LogWarning("[ChurchSystemUI] ChurchSystem.Instance가 없습니다.");
                return;
            }

            // UIManager를 통해 ChurchUI 열기
            if (UIManager.Instance != null)
            {
                // UIManager에 churchWindow가 없으면 FindObjectOfType 폴백
                UIWindow targetWindow = null;

                // UIManager의 모든 UIWindow 중 ChurchUI 타입 검색
                var allWindows = Resources.FindObjectsOfTypeAll<ChurchUI>();
                if (allWindows.Length > 0)
                {
                    targetWindow = allWindows[0];
                }

                if (targetWindow != null)
                {
                    var churchUI = targetWindow as ChurchUI;
                    if (churchUI != null)
                    {
                        churchUI.SetTerritory(_territoryId);
                    }

                    if (targetWindow.IsOpen)
                    {
                        targetWindow.Hide();
                    }
                    else
                    {
                        targetWindow.Show();
                        Debug.Log($"[ChurchSystemUI] 성당 UI 열림 (영지: {_territoryId})");
                    }
                }
                else
                {
                    Debug.LogWarning("[ChurchSystemUI] ChurchUI를 찾을 수 없습니다.");
                }
            }
            else
            {
                Debug.LogWarning("[ChurchSystemUI] UIManager.Instance가 없습니다.");
            }
        }

        private void OnGUI()
        {
            if (!_isPlayerNearby) return;

            if (_guiDirty)
            {
                int favor = ChurchSystem.Instance != null ? ChurchSystem.Instance.GetFavor() : 0;
                _cachedGuiLabel = $"[E] 성당 — 친밀도: {favor}/100";
                _guiLabelRect = new Rect(Screen.width / 2 - 150, Screen.height / 2 + 50, 300, 30);
                _guiDirty = false;
            }

            GUI.Label(_guiLabelRect, _cachedGuiLabel);
        }
    }
}
