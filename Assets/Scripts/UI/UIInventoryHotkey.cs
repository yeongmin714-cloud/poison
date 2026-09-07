using UnityEngine;

namespace ProjectName.UI
{
    /// <summary>
    /// 인벤토리 윈도우 I키 토글 핸들러.
    /// 씬에 UI 윈도우가 없어 I키가 무시되던 문제의 런타임 배선(09-07).
    /// GameSetup이 InventoryWindow 인스턴스를 생성한 뒤 Bind()로 연결한다.
    /// </summary>
    public class UIInventoryHotkey : MonoBehaviour
    {
        private ProjectName.UI.InventoryWindow _inv;

        public void Bind(ProjectName.UI.InventoryWindow inv) => _inv = inv;

        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || _inv == null) return;

            // I 키 상승 에지 1회 → 토글 (스테일 참조 방지: 매 프레임 즉시 조회)
            if (kb.iKey.wasPressedThisFrame)
            {
                _inv.Toggle();
                Debug.Log($"[UIInventoryHotkey] 인벤토리 토글 → {(_inv.IsOpen ? "열림" : "닫힘")}");
            }
        }
    }
}
