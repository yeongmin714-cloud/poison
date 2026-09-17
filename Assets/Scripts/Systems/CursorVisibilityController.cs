using ProjectName.Core;
using UnityEngine;
using UnityEngine.InputSystem;

#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// OS 커서 가시성 제어 — Ctrl 키를 누르는 동안만 표준 OS 커서 표시, 그 외엔 숨김.
    /// 숨김 상태에서도 마우스는 자유롭게 이동 가능(CursorLockMode.None 유지) → RTS 커스텀 커서 패턴.
    ///
    /// 커서 API 결정(검증 완료):
    ///   UnityEngine.Cursor.visible / UnityEngine.Cursor.lockState 가 이 엔진(Unity 6)에서 유효함을
    ///   프로젝트 기존 사용(DoubleL/Demo/DemoCameraController.cs: Cursor.visible=true, Cursor.lockState)
    ///   으로 확인. 따라서 표준 UnityEngine.Cursor API를 사용해 Ctrl 표시/숨김을 구현한다.
    ///   (커스텀 스프라이트 폴백이 필요 없음 — 커스텀 컨텍스트 커서는 ContextCursorSystem이 담당.)
    ///
    /// UI 코드와 싸우지 않는다 — 항상 lockState=None, visible만 제어.
    /// </summary>
    public class CursorVisibilityController : MonoBehaviour
    {
        public static CursorVisibilityController Instance { get; private set; }

        public static CursorVisibilityController Ensure(GameObject parent = null)
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<CursorVisibilityController>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }

            var go = new GameObject("CursorVisibilityController");
            if (parent != null) go.transform.SetParent(parent.transform, false);
            return go.AddComponent<CursorVisibilityController>();
        }

        /// <summary>이 시스템이 커서 가시성을 완전히 소유(오버라이드) 중인지 — 외부가 visible을 임의로 바꾸지 않도록 알림.</summary>
        public static bool IsOverriding() => Instance != null;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // 시작 시 커서 숨김 (ContextCursorSystem이 대체 커서를 표시)
            Apply(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // 파괴 시 OS 커서 복구 — 영구 숨김 방지
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private void Update()
        {
            bool ctrlHeld = Keyboard.current != null &&
                (Keyboard.current.ctrlKey.isPressed
                 || Keyboard.current.leftCtrlKey.isPressed
                 || Keyboard.current.rightCtrlKey.isPressed);

            bool show = ctrlHeld || !Application.isFocused;
            Apply(show);
        }

        private void Apply(bool show)
        {
            Cursor.lockState = CursorLockMode.None;   // 마우스 자유 이동 유지 (잡꼬 locked 아님)
            if (Cursor.visible != show)
                Cursor.visible = show;
        }
    }
}
