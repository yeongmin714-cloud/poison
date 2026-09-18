using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U8 — 호출부 배선 (Systems → UI 순환참조 회피용 이벤트 구독 브리지).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    ///
    /// 패턴: Systems 측 정적 이벤트(OnXxxRequestedUTK)를 UI 측에서 구독.
    ///   - UTK 루트 준비됨 → UTK 창 표시
    ///   - 준비 안 됨   → 기존 IMGUI 원본 경로로 폴백(기존 이벤트/메서드 직접 호출)
    /// 되돌리기: 본 파일 제거(또는 구독 해제)만으로 100% 원본 경로 회귀 — Systems 이벤트는 무구독 시 무효.
    /// [UTKWire] 실측 로그.
    /// </summary>
    public static class UTKWireUp
    {
        private static bool _wired;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            Wire();
        }

        /// <summary>배선 실행(멱등) — 외부에서 강제 호출 가능.</summary>
        public static void Wire()
        {
            if (_wired) return;
            _wired = true;

            // 침대 상호작용 → 수면 UTK (폴백: 원본 SleepUI)
            Bed.OnInteractRequestedUTK += bed =>
            {
                if (UIToolkitBootstrap.UIRoot != null)
                {
                    SleepUTK.Ensure();
                    SleepUTK.Open(bed);
                    Debug.Log("[UTKWire] 침대 → SleepUTK");
                }
                else if (SleepUI.Instance != null)
                {
                    Debug.Log("[UTKWire] UTK 루트 미준비 — 원본 SleepUI 폴백");
                    SleepUI.Instance.Show(bed);
                }
            };

            // 전리품 바구니 → 전리품 UTK (폴백: 원본 LootWindow 경유 공개 메서드)
            LootBasket.OnOpenLootWindowRequestedUTK += basket =>
            {
                if (UIToolkitBootstrap.UIRoot != null)
                {
                    LootWindowUTK.Ensure();
                    LootWindowUTK.Open(basket);
                    InventoryWindowUTK.Open();            // [U8 요구] 전리품 열림 시 인벤 동시 표시
                    ItemDescriptionWindowUTK.Show();
                    Debug.Log("[UTKWire] 바구니 → LootWindowUTK + 인벤/설명 동시 표시");
                }
                else
                {
                    Debug.Log("[UTKWire] UTK 루트 미준비 — 원본 LootWindow 폴백");
                    var concrete = basket as LootBasket;
                    if (concrete != null) concrete.InvokeLegacyOpenRequest();
                    else Debug.LogWarning("[UTKWire] LootBasket 캐스트 실패 — 폴백 불가");
                }
            };

            // 상점 상호작용 → 상점 UTK (폴백: 원본 ShopPlaceholder IMGUI 경로 자동 — 미구독)
            ProjectName.UI.ShopPlaceholder.ToggleShopRequestedUTK += shop =>
            {
                if (UIToolkitBootstrap.UIRoot != null)
                {
                    ShopWindowUTK.Open();
                    Debug.Log("[UTKWire] 상점 → ShopWindowUTK");
                }
                else
                {
                    Debug.Log("[UTKWire] UTK 루트 미준비 — 원본 ShopPlaceholder 폴백");
                    shop.InvokeLegacyToggleShop();
                }
            };

            // I키 → 인벤+설명 쌍 토글 (독립 창 2종 — 폴백: 원본 InventoryWindow IMGUI 토글)
            UIInventoryHotkey.InventoryToggleRequestedUTK += () =>
            {
                if (UIToolkitBootstrap.UIRoot != null)
                {
                    bool closing = InventoryWindowUTK.Instance != null && InventoryWindowUTK.Instance.IsOpen;
                    if (closing)
                    {
                        InventoryWindowUTK.Instance.Close();
                        ItemDescriptionWindowUTK.Hide();
                    }
                    else
                    {
                        InventoryWindowUTK.Open();
                        ItemDescriptionWindowUTK.Show();
                    }
                    Debug.Log($"[UTKWire] I키 → 인벤+설명 쌍 ({(closing ? "닫힘" : "열림")})");
                }
                else
                {
                    Debug.Log("[UTKWire] UTK 루트 미준비 — 원본 인벤 폴백");
                    var inv = ProjectName.UI.InventoryWindow.Instance;
                    if (inv != null) inv.TogglePlayerInventory();
                }
            };

            Debug.Log("[UTKWire] 호출부 배선 완료 (침대 수면/전리품 바구니/상점/인벤 I키)");
        }
    }
}
