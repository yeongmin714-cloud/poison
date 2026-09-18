using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// [U8 수리] UTK UI 위 포인터 판정 게이트 — 게임 월드 입력(공격/RTS 선택/카메라 드래그)이
    /// UI 조작(특히 인벤 드래그)과 경합하는 것을 차단. Systems 어셈블리는 Core.UITransitionState
    /// .PointerOverUI 플래그만 읽는다(순환참조 회피 — UTKWindowManager가 매 프레임 갱신).
    /// </summary>
    public static class UTKInputGate
    {
        /// <summary>포인터가 UTK 패널의 실제 엘리먼트 위에 있거나 드래그 중이면 true.</summary>
        public static bool IsPointerOverUI()
        {
            if (UTKDragDrop.Active) return true;   // 드래그 중 — 무조건 차단

            var root = UIToolkitBootstrap.UIRoot;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (root == null || mouse == null) return false;

            var screen = mouse.position.ReadValue();
            // Input System y 하단 원점 → 패널 y 상단 원점 변환
            var panelPos = new Vector2(screen.x, root.worldBound.height - screen.y);
            var picked = root.panel.Pick(panelPos);
            return picked != null && picked != root;
        }
    }
}
