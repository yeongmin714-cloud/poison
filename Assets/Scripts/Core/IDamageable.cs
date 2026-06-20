using UnityEngine;

namespace ProjectName.Core
{
    public interface IDamageable
    {
        void TakeDamage(float amount, Vector3 hitDirection, string weaponType = "melee");
        bool IsAlive { get; }
    }
}