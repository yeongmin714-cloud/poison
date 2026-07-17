using UnityEngine;
using System.Collections.Generic;

namespace ProjectName.Core
{
    /// <summary>
    /// 피해를 입을 수 있는 객체를 나타냅니다.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// 지정된 양의 피해를 적용합니다.
        /// </summary>
        /// <param name="amount">적용할 피해량</param>
        /// <param name="hitDirection">공격자에서 대상 방향의 정규화된 벡터 (넉백 등에 사용)</param>
        /// <param name="weaponType">무기 타입 식별자 (예: "melee", "Explosion", "Arrow")</param>
        void TakeDamage(float amount, Vector3 hitDirection, string weaponType);

        /// <summary>
        /// 이 객체가 아직 살아있는지 여부.
        /// </summary>
        bool IsAlive { get; }
    }
}