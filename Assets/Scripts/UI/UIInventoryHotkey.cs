using UnityEngine;
using System.Collections.Generic;

namespace ProjectName.UI
{
    /// <summary>
    /// 인벤토리 윈도우 I키 토글 핸들러.
    /// 씬에 UI 윈도우가 없어 I키가 무시되던 문제의 런타임 배선(09-07).
    /// GameSetup이 InventoryWindow 인스턴스를 생성한 뒤 Bind()로 연결한다.
    ///
    /// 2026-09-11(4) 범용 수리:
    /// - Bind 미호출 씬(Test_10 등 부착만 하고 Bind 없음) 폴백 — InventoryWindow.Instance로 토글.
    /// - 다중 인스턴스 중복 토글 방지 — 살아있는 인스턴스 중 첫 번째만 입력을 처리한다.
    ///   (InventoryWindow.Awake의 자가 등록 + 셋업 리플렉션 Bind가 공존해도 토글 1회 보장)
    /// </summary>
    public class UIInventoryHotkey : MonoBehaviour
    {
        private ProjectName.UI.InventoryWindow _inv;

        // 살아있는 핫키 레지스트리 (선착순 단일 처리 — GC 캐시 관례: 정적 리스트)
        private static readonly List<UIInventoryHotkey> s_live = new List<UIInventoryHotkey>(4);

        public void Bind(ProjectName.UI.InventoryWindow inv) => _inv = inv;

        private void OnEnable() => s_live.Add(this);
        private void OnDisable() => s_live.Remove(thisMigrationGuard());
        private void OnDestroy() => s_live.Remove(this);

        // OnDisable 중 this가 파괴되는 경합 방지용 래퍼(단순 위임)
        private UIInventoryHotkey thisMigrationGuard() => this;

        private void Update()
        {
            // 중복 토글 방지: 여러 핫키 인스턴스가 있어도 첫 번째(선착순)만 처리
            if (s_live.Count == 0 || s_live[0] != this) return;

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;

            // Bind 누락 폴백 — 싱글턴 인스턴스로 토글 (Test_10 등 셋업이 Bind 없이 부착만 한 씬 커버)
            var inv = _inv != null ? _inv : ProjectName.UI.InventoryWindow.Instance;
            if (inv == null) return;

            // I 키 상승 에지 1회 → 플레이어 인벤 토글 (2026-09-12 P4: 컨텍스트 리셋 포함 —
            // 창고/상점 컨텍스트 잔존 상태로 열려 플레이어 인벤이 안 보이던 문제 방지)
            if (kb.iKey.wasPressedThisFrame)
            {
                inv.TogglePlayerInventory();
                Debug.Log($"[UIInventoryHotkey] 인벤토리 토글 → {(inv.IsOpen ? "열림" : "닫힘")}");
            }
        }
    }
}
