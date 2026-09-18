using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U0 — 윈도우 레지스트리 & ESC 처리.
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    ///
    /// static 레지스트리: 열려있는 UTKWindowBase의 순서 보장 스택.
    ///   Register/Unregister는 UTKWindowBase가 Show/Hide 시 자동 호출.
    ///   Update에서 ESC 입력 감지 → 최상단(가장 최근) 윈도우 Close().
    ///
    /// Update 루프는 내장 Updater MonoBehaviour가 의존성 없이 실행하기 위해
    /// UTKWindowBase/매니저 사용 시 Ensure()로 단일 인스턴스를 만든다.
    /// </summary>
    public static class UTKWindowManager
    {
        // 순서 보장 스택 (Register 순 = 열린 순). 최상단 = [^1].
        private static readonly List<UTKWindowBase> _openStack = new List<UTKWindowBase>();

        private static Updater _updater;
        private static bool _warnedNoUpdater;

        /// <summary>가장 위에 열린 윈도우 (없으면 null).</summary>
        public static UTKWindowBase TopWindow =>
            _openStack.Count > 0 ? _openStack[_openStack.Count - 1] : null;

        /// <summary>열린 윈도우 수.</summary>
        public static int OpenCount => _openStack.Count;

        /// <summary>읽기 전용 열린 윈도우 목록 (순회용).</summary>
        public static IReadOnlyList<UTKWindowBase> OpenWindows => _openStack;

        /// <summary>Update 루프 보장 (멱등). 부트스트랩/윈도우가 자동 호출.</summary>
        public static void Ensure()
        {
            if (_updater != null)
                return;

            var go = new GameObject("UTKWindowManager");
            Object.DontDestroyOnLoad(go);
            _updater = go.AddComponent<Updater>();
        }

        /// <summary>윈도우 등록 — UTKWindowBase.Show에서 호출. 이미 있으면 무시.</summary>
        public static void Register(UTKWindowBase window)
        {
            Ensure();
            if (window == null || _openStack.Contains(window))
                return;
            _openStack.Add(window);
        }

        /// <summary>윈도우 해제 — UTKWindowBase.Hide/OnDestroy에서 호출.</summary>
        public static void Unregister(UTKWindowBase window)
        {
            if (window == null)
                return;
            int i = _openStack.Count - 1;
            while (i >= 0)
            {
                if (_openStack[i] == window)
                {
                    _openStack.RemoveAt(i);
                    return;
                }
                i--;
            }
        }

        /// <summary>ESC로 맨 위 윈도우 닫기 (게이트: 일시정지 시 무시 — 추후 스펙).</summary>
        private static void HandleEscape()
        {
            var top = TopWindow;
            if (top == null)
                return;
            // 선택적 Pause 게이트 지점 — 일시정지 중 무시 여부는 추후 결정.
            top.Close();
        }

        /// <summary>Update 전용 MonoBehaviour. 씬과 무관하게 DOM 로드 시 존재.</summary>
        private class Updater : MonoBehaviour
        {
            private void Update()
            {
                // [U8 수리] 드래그 유착 자가 해제 — 버튼이 모두 떨어졌는데 Active가 남아있으면 강제 취소
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (mouse != null && UTKDragDrop.Active
                    && !mouse.leftButton.isPressed && !mouse.rightButton.isPressed)
                {
                    UTKDragDrop.Cancel();
                }

                // [U8 수리] 포인터-over-UI 플래그 갱신 — Systems 측 게임 입력 게이트(PlayerCombat 등)가 읽음
                ProjectName.Core.UITransitionState.PointerOverUI = UTKInputGate.IsPointerOverUI();

                var kb = Keyboard.current;
                if (kb == null || !kb.escapeKey.wasPressedThisFrame)
                    return;
                if (_openStack.Count == 0)
                    return;
                HandleEscape();
            }
        }
    }
}