using System;
using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>EquipmentManager의 Mask 슬롯과 GasMaskSystem 면역 상태를 연결합니다.</summary>
    public static class GasMaskEquipmentLink
    {
        private static bool _registered;
        private static EquipmentManager _registeredManager;

        /// <summary>Mask 슬롯 변경을 구독하고 현재 장착 상태도 즉시 동기화합니다.</summary>
        public static void Register()
        {
            EquipmentManager manager = EquipmentManager.Get();
            if (manager == null)
            {
                Debug.LogWarning("[GasMaskEquipmentLink] EquipmentManager 미발견 — 등록 보류");
                return;
            }

            if (_registered && _registeredManager == manager)
                return;

            if (_registeredManager != null)
                _registeredManager.OnEquipmentChanged -= OnChanged;

            manager.OnEquipmentChanged -= OnChanged;
            manager.OnEquipmentChanged += OnChanged;
            _registeredManager = manager;
            _registered = true;

            // 구독 시점에 이미 장착된 Mask 슬롯도 반영합니다.
            OnChanged(EquipmentManager.EquipmentSlot.Mask,
                manager.GetEquippedItemId(EquipmentManager.EquipmentSlot.Mask));
        }

        private static void OnChanged(EquipmentManager.EquipmentSlot slot, string itemId)
        {
            if (slot != EquipmentManager.EquipmentSlot.Mask)
                return;

            if (string.IsNullOrEmpty(itemId))
            {
                GasMaskSystem.Unequip();
                return;
            }

            GasMaskGrade? grade = MapGradeFromItemId(itemId);
            if (grade.HasValue)
                GasMaskSystem.Equip(grade.Value);
            // 다른 가면/장식 아이템은 현재 면역 상태를 변경하지 않습니다.
        }

        private static GasMaskGrade? MapGradeFromItemId(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return null;

            // 관례 ID(gas_mask)와 기존 ID(gasmask) 모두 인식합니다.
            string normalizedId = itemId.Replace("_", string.Empty).Replace("-", string.Empty);
            if (normalizedId.IndexOf("gasmask", StringComparison.OrdinalIgnoreCase) < 0)
                return null;

            if (normalizedId.IndexOf("steel", StringComparison.OrdinalIgnoreCase) >= 0
                || normalizedId.IndexOf("reinforced", StringComparison.OrdinalIgnoreCase) >= 0
                || normalizedId.IndexOf("iron", StringComparison.OrdinalIgnoreCase) >= 0)
                return GasMaskGrade.Iron;
            if (normalizedId.IndexOf("stone", StringComparison.OrdinalIgnoreCase) >= 0)
                return GasMaskGrade.Stone;
            if (normalizedId.IndexOf("special", StringComparison.OrdinalIgnoreCase) >= 0
                || normalizedId.IndexOf("alloy", StringComparison.OrdinalIgnoreCase) >= 0)
                return GasMaskGrade.Special;

            return GasMaskGrade.Wood;
        }
    }
}
