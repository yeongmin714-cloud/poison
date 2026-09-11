using UnityEngine;

namespace ProjectName.Core
{
    public enum WeaponType { Fist, Sword, Spear, Bow }

    [System.Serializable]
    public class WeaponData
    {
        public string weaponName = "맨손";
        public float damage = 5f;
        public float attackSpeed = 1f;
        public float range = 2f;
        public WeaponType weaponType = WeaponType.Fist;

        public WeaponData() { }

        public WeaponData(string name, float damage, float speed, float range, WeaponType type)
        {
            weaponName = name;
            this.damage = Mathf.Max(0f, damage);
            attackSpeed = Mathf.Max(0.1f, speed);
            this.range = Mathf.Max(0f, range);
            weaponType = type;
        }

        public static readonly WeaponData Fist = new WeaponData("맨손", 5f, 0.8f, 2f, WeaponType.Fist);
        public static readonly WeaponData Sword = new WeaponData("검", 12f, 1.0f, 2.5f, WeaponType.Sword);
        public static readonly WeaponData Spear = new WeaponData("창", 10f, 1.5f, 4f, WeaponType.Spear);
        public static readonly WeaponData Bow = new WeaponData("활", 8f, 2.0f, 10f, WeaponType.Bow);

        // ── 등급(티어) 보정 테이블 (2026-09-11) ──
        // wood 1.0 / steel 1.8 / stone 2.5 / crystal 3.75
        // Sword(12) 기준: wood 12 → steel 22 → stone 30 → crystal 45
        public static float GetTierMultiplier(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId)) return 1f;
            string s = weaponId.ToLowerInvariant();
            if (s.Contains("crystal")) return 3.75f;
            if (s.Contains("stone"))   return 2.5f;
            if (s.Contains("steel"))   return 1.8f;
            return 1f; // wood 및 기타
        }

        /// <summary>등급 보정 복제본 — damage만 Round(base × multiplier), 공속/사거리/타입 유지. 정적 스탯 오염 방지용.</summary>
        public WeaponData CreateTieredCopy(float tierMultiplier)
            => new WeaponData(weaponName, Mathf.Round(damage * tierMultiplier), attackSpeed, range, weaponType);

        public override string ToString()
        {
            return $"{weaponName} (DMG:{damage} SPD:{attackSpeed} RNG:{range} TYPE:{weaponType})";
        }
    }
}