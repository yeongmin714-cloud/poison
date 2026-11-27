using System.Collections.Generic;
using ProjectName.Core;   // WeaponType

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// [68차 P3] 핫바 숫자키 장착용 무기 id 해석기 — HotbarUIUTK용.
    /// InventoryWindowUTK private본(_weaponIdMap + TryResolveWeaponEquip, ~1092-1136행)의
    /// 로직 복사 생성 — InventoryWindowUTK private본과 임시 중복(후속 사이클 통합 예정).
    /// 규약: 레거시 InventoryWindow의 장비 id 해석 규칙과 동일하게 유지한다.
    /// </summary>
    public static class WeaponItemResolver
    {
        private static readonly Dictionary<string, (string equipId, WeaponType type)> _weaponIdMap =
            new Dictionary<string, (string, WeaponType)>
            {
                { "steel_sword", ("steel", WeaponType.Sword) },
                { "iron_sword", ("iron", WeaponType.Sword) },
                { "crystal_bow", ("crystal", WeaponType.Bow) },
                { "wood_bow", ("wood", WeaponType.Bow) },
                { "wood_spear", ("wood", WeaponType.Spear) },
                { "spear", ("wood", WeaponType.Spear) },
            };

        /// <summary>
        /// 아이템 id → 무기 장착 파라미터(equipId + WeaponType) 해석.
        /// 1) 명시 맵 조회(레거시 짧은 id) → 2) weapon_{type}_{tier} 3파트 파싱 → 3) 파트 기반 등급 폴백.
        /// 호출자는 category==Weapon인 id에만 사용해야 한다(비무기도 폴백 규약상 true가 될 수 있음).
        /// </summary>
        public static bool TryResolve(string itemId, out string equipId, out WeaponType type)
        {
            if (_weaponIdMap.TryGetValue(itemId, out var w))
            {
                equipId = w.equipId;
                type = w.type;
                return true;
            }
            string s = (itemId ?? string.Empty).ToLowerInvariant();
            var parts = s.Split('_');
            if (parts.Length == 3 && parts[0] == "weapon")
            {
                // [68차 수리] 타입 키워드는 parts[2] 우선 → parts[1] 폴백 — "weapon_iron_spear"(티어_타형)도
                //   Spear로 판정(legacy는 parts[1]=티어로만 판정해 Sword 오분류). equipId는 legacy 규약 유지(원본 id).
                type = TypeFromParts(parts[2], parts[1], s);
                equipId = s;
                return true;
            }
            type = TypeFromParts(null, null, s);
            equipId = null;
            foreach (var p in parts)
            {
                if (p == "weapon" || p == "sword" || p == "bow" || p == "spear" || p == "dagger") continue;
                if (p == "steel" || p == "iron" || p == "crystal" || p == "wood" || p == "stone") { equipId = p; break; }
            }
            if (string.IsNullOrEmpty(equipId) && parts.Length > 1)
                equipId = parts[parts.Length - 1];
            return !string.IsNullOrEmpty(equipId);
        }

        /// <summary>[68차] 타입 키워드 판정 공용화 — 우선 인자(spear/bow) → 폴백 인자 → 전체 contains → 기본 Sword.</summary>
        private static WeaponType TypeFromParts(string first, string second, string full)
        {
            foreach (var p in new[] { first, second })
            {
                if (string.IsNullOrEmpty(p)) continue;
                if (p == "spear") return WeaponType.Spear;
                if (p == "bow") return WeaponType.Bow;
                if (p == "sword" || p == "dagger") return WeaponType.Sword;
            }
            if (!string.IsNullOrEmpty(full))
            {
                if (full.Contains("bow")) return WeaponType.Bow;
                if (full.Contains("spear")) return WeaponType.Spear;
            }
            return WeaponType.Sword;
        }
    }
}
