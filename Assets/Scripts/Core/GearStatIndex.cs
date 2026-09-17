using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>
    /// 2026-09-17: 정적 장비 스탯 인덱스 — 장착된 무기/방어구 id를 해석해
    /// 공격력/방어력 보너스를 반환한다. WeaponData 티어 배율(GetTierMultiplier) 재사용.
    ///
    /// 무기: id가 "sword"/"spear"/"bow"/"dagger"/"fist"를 포함하면 해당 타입 기본 데미지 × 티어.
    /// 방어구: id가 부위 키워드("helmet"/"armor"/"boot"/"glove"/"shield"/"mask"/"pack")를 포함하면
    ///         해당 부위 기본 방어 × 티어. 티어 키워드는 "crystal"/"stone"/"steel"(이상 WeaponData
    ///         GetTierMultiplier와 동일) / 그 외(wood 등)는 1.0.
    /// unknown → 0 (아무 보너스 없음).
    /// </summary>
    public static class GearStatIndex
    {
        // ── 무기 타입 기본 데미지 (WeaponData.Fist/Sword/Spear/Bow 동일 값. Dagger는 WeaponData에
        //    정의가 없어 관례상 7f 사용 — 가볍고 빠른 근접 무기로 Sword(12)와 Fist(5) 사이.) ──
        private const float DmgFist  = 5f;
        private const float DmgSword = 12f;
        private const float DmgSpear = 10f;
        private const float DmgBow   = 8f;
        private const float DmgDagger = 7f;

        // ── 방어구 부위 기본 방어 ──
        private const float DefHelmet = 4f;
        private const float DefArmor  = 8f;
        private const float DefBoot   = 3f;
        private const float DefGlove  = 2f;
        private const float DefShield = 6f;
        private const float DefMask   = 2f;
        private const float DefPack   = 2f;

        private static float Round1(float v) => Mathf.Round(v * 10f) / 10f;

        /// <summary>
        /// 무기 id → 공격력 보너스 (기본 데미지 × WeaponData.GetTierMultiplier, 소수 1자리).
        /// 무기로 인식되지 않으면 0 반환.
        /// </summary>
        public static float GetWeaponAttackBoost(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0f;
            string s = itemId.ToLowerInvariant();

            float baseDmg = 0f;
            if (s.Contains("sword"))      baseDmg = DmgSword;
            else if (s.Contains("spear")) baseDmg = DmgSpear;
            else if (s.Contains("bow"))   baseDmg = DmgBow;
            else if (s.Contains("dagger")) baseDmg = DmgDagger;
            else if (s.Contains("fist"))  baseDmg = DmgFist;
            else if (s.Contains("weapon")) baseDmg = DmgFist; // equip_weapon_* 등 일반 무기 폴백 → 맨손
            if (baseDmg <= 0f) return 0f;

            return Round1(baseDmg * WeaponData.GetTierMultiplier(itemId));
        }

        /// <summary>
        /// 방어구/부속 id → 방어력 보너스 (부위 기본 방어 × 티어, 소수 1자리).
        /// 방어구로 인식되지 않으면 0 반환.
        /// </summary>
        public static float GetArmorDefenseBoost(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0f;
            string s = itemId.ToLowerInvariant();

            float baseDef = 0f;
            if (s.Contains("helmet")) baseDef = DefHelmet;
            else if (s.Contains("armor"))  baseDef = DefArmor;
            else if (s.Contains("boot"))   baseDef = DefBoot;
            else if (s.Contains("glove"))  baseDef = DefGlove;
            else if (s.Contains("shield")) baseDef = DefShield;
            else if (s.Contains("mask"))   baseDef = DefMask;
            else if (s.Contains("pack"))   baseDef = DefPack;
            if (baseDef <= 0f) return 0f;

            return Round1(baseDef * WeaponData.GetTierMultiplier(itemId));
        }
    }
}