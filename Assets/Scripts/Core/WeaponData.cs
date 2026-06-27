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

        public override string ToString()
        {
            return $"{weaponName} (DMG:{damage} SPD:{attackSpeed} RNG:{range} TYPE:{weaponType})";
        }
    }
}